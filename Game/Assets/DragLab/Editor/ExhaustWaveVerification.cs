using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DragLab.Editor
{
    public static class ExhaustWaveVerification
    {
        static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/exhaust-waveguide-20261022"));
        static readonly string[] Models = { "starter-hatch", "v8-muscle", "rally-sedan", "super-v8", "timber-diesel" };
        static VehicleDefinition Car(string id) => Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Single(v => v.spec.id == id);
        public static void Baseline()
        {
            Directory.CreateDirectory(Folder);
            foreach (string id in Models)
            {
                var car = Car(id); var original = car.spec.Copy(); original.sound.exhaustAcoustics=ExhaustWaveSpec.Approximate(); var pcm = EngineSoundAudition.Render(original, car.CreateStockTune());
                using (var writer = new BinaryWriter(File.Create(Path.Combine(Folder, id + "-baseline.f32")))) foreach (float x in pcm) writer.Write(x);
                EngineSoundAudition.WriteWav(Path.Combine(Folder, id + "-baseline.wav"), pcm);
            }
            File.WriteAllText(Path.Combine(Folder, "baseline.txt"), "Rendered five 8-second Approximate-path diagnostics; immutable pre-change hashes are versioned under docs/reference/audio-phase35.\n", new UTF8Encoding(false));
            Debug.Log("EXHAUST WAVE BASELINE PASS: 5 configurations");
        }
        static readonly StringBuilder Log = new StringBuilder();
        static int checks;
        static string ReceiptName="verification.txt";
        static void Check(bool value,string label)
        {
            Log.AppendLine((value?"PASS: ":"FAIL: ")+label);checks++;
            File.WriteAllText(Path.Combine(Folder,ReceiptName),Log+"Checks: "+checks+"\n",new UTF8Encoding(false));
            if(!value)throw new InvalidOperationException(label);
        }
        [Serializable] public sealed class LegacyHash { public string model,sha256; }
        [Serializable] public sealed class LegacyHashes { public LegacyHash[] cases; }
        static string PcmHash(float[] pcm)
        { using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))foreach(float value in pcm)writer.Write(value);using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();} }
        static LegacyHashes Hashes()=>JsonUtility.FromJson<LegacyHashes>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/reference/audio-phase35/legacy-pcm-hashes.json")),Encoding.UTF8));
        static ExhaustWaveSpec Mode(int mask) => new ExhaustWaveSpec { version=1,pipeMode=(mask&1)!=0?AcousticModelMode.Simulated:AcousticModelMode.Approximate,mufflerMode=(mask&2)!=0?AcousticModelMode.Simulated:AcousticModelMode.Approximate };
        [Serializable] public sealed class ReferenceCase { public string file;public int rate;public ExhaustWaveSpec spec;public double position; }
        [Serializable] public sealed class ReferenceManifest { public ReferenceCase[] cases; }
        static double Input(int i,bool right)=>i==0?(right?.3:1):i==137?(right?-.07:.12):0;
        public static void Run()
        {
            Directory.CreateDirectory(Folder);ReceiptName="verification.txt";checks=0;Log.Clear();
            var references=new System.Collections.Generic.List<ReferenceCase>();
            foreach(int rate in new[]{22050,44100,48000})foreach(int mode in new[]{0,1,2,3})foreach(double position in new[]{.2,.55,.9})
            {
                var settings=Mode(mode);var guide=new ExhaustWaveguide(settings,rate);string name="impulse-"+rate+"-"+mode+"-"+(int)(position*100)+".f64";
                double power=0;using(var writer=new BinaryWriter(File.Create(Path.Combine(Folder,name))))
                    for(int i=0;i<rate;i++){guide.Process(Input(i,false),Input(i,true),i==47?.2:0,i==79?.1:0,position);writer.Write(guide.Left);writer.Write(guide.Right);power+=guide.Left*guide.Left+guide.Right*guide.Right;}
                Check(power>0&&power<100,"Finite impulse energy / "+name);
                guide.Clear();guide.Process(0,0);Check(guide.Left==0&&guide.Right==0,"Clearing retires every delayed/filter tail / "+name);
                references.Add(new ReferenceCase{file=name,rate=rate,spec=settings,position=position});
            }
            File.WriteAllText(Path.Combine(Folder,"reference-cases.json"),JsonUtility.ToJson(new ReferenceManifest{cases=references.ToArray()},true),new UTF8Encoding(false));
            var fixedTemperature=Mode(3);fixedTemperature.gasTemperatureK=300;
            var cold=new ExhaustWaveguide(fixedTemperature,48000);var hotSpec=fixedTemperature.Copy();hotSpec.gasTemperatureK=1200;var hot=new ExhaustWaveguide(hotSpec,48000);
            Check(Math.Abs(cold.HeaderDelayFrames/hot.HeaderDelayFrames-2)<1e-12,"Hotter gas halves travel delay at four times absolute temperature");
            var dynamicTemperature=new ExhaustWaveguide(Mode(3),48000);dynamicTemperature.SetGasTemperature(1200);Check(dynamicTemperature.HeaderDelayFrames==hot.HeaderDelayFrames,"Unpinned acoustics follows the captured exhaust temperature");
            cold.SetGasTemperature(1200);Check(cold.HeaderDelayFrames==hot.HeaderDelayFrames*2,"Explicit fixed-temperature diagnostic remains pinned");
            var bad=Mode(3);bad.headerLengthM=float.NaN;Check(bad.Validate()!=null,"Nonfinite geometry is rejected before playback");bad=Mode(3);bad.pipeMode=(AcousticModelMode)99;Check(bad.Validate()!=null,"Unknown model flag is rejected");
            foreach(string id in Models)
            {
                var definition=Car(id);var original=definition.spec.Copy();string authored=JsonUtility.ToJson(definition.spec);
                var old=original.Copy();old.sound.exhaustAcoustics=ExhaustWaveSpec.Approximate();
                var pcm=EngineSoundAudition.Render(old,definition.CreateStockTune());
                bool exact=PcmHash(pcm)==Hashes().cases.Single(row=>row.model==id).sha256;
                Check(exact,"Both approximations reproduce pre-change production PCM exactly / "+id);
                float[] after=null;
                for(int mode=0;mode<4;mode++)
                {
                    var variant=original.Copy();var resolved=ExhaustWaveSpec.Resolve(null,variant.sound.exhaustSystem,variant.DisplacementLitres,new InductionProfile(variant,definition.CreateStockTune()).turboCount>0);
                    resolved.pipeMode=Mode(mode).pipeMode;resolved.mufflerMode=Mode(mode).mufflerMode;variant.sound.exhaustAcoustics=resolved;
                    var rendered=EngineSoundAudition.Render(variant,definition.CreateStockTune());
                    Check(rendered.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)&&Math.Abs(x)<1)&&rendered.Any(x=>Math.Abs(x)>.001),"All independent modes render finite audible PCM / "+id+" / "+mode);
                    EngineSoundAudition.WriteWav(Path.Combine(Folder,id+"-mode-"+mode+".wav"),rendered);if(mode==3)after=rendered;
                }
                var joined=new float[pcm.Length+EngineSoundAudition.Rate/2+after.Length];Array.Copy(pcm,joined,pcm.Length);Array.Copy(after,0,joined,pcm.Length+EngineSoundAudition.Rate/2,after.Length);
                EngineSoundAudition.WriteWav(Path.Combine(Folder,id+"-before-after.wav"),joined);
                Check(authored==JsonUtility.ToJson(definition.spec),"Audition never edits authored vehicle / "+id);
                var profile=new EngineSoundProfile(original,definition.CreateStockTune());
                Check(profile.exhaustAcoustics.pipeMode==AcousticModelMode.Simulated&&profile.exhaustAcoustics.mufflerMode==AcousticModelMode.Simulated,"Existing content resolves the new hardware-driven default / "+id);
            }
            var noParts=ExhaustSetup.Preset(3);var resolvedEmpty=ExhaustWaveSpec.Resolve(null,noParts,2,false);var empty=new ExhaustWaveguide(resolvedEmpty,48000,noParts);
            Check(empty.Waves&&!empty.PhysicalMuffler,"Open headers cannot acquire a fabricated muffler");
            var authoredCopy=new EngineSoundSpec{exhaustAcoustics=Mode(3)};var copied=authoredCopy.Copy();copied.exhaustAcoustics.headerLengthM=2;Check(authoredCopy.exhaustAcoustics.headerLengthM==.75f,"Acoustic authoring copies own their geometry");
            var aSpec=Car("rally-sedan").spec.Copy();var bSpec=aSpec.Copy();aSpec.sound.exhaustAcoustics=ExhaustWaveSpec.Approximate();var tuning=Car("rally-sedan").CreateStockTune();var driver=new DriverSettings();
            var aCar=new CarSimulation(aSpec,tuning,driver);var bCar=new CarSimulation(bSpec,tuning,driver);var ac=new EngineSoundController(aSpec,tuning);var bc=new EngineSoundController(bSpec,tuning);
            ac.Update(aCar.state,true,.8,1.0/120);bc.Update(bCar.state,true,.8,1.0/120);bool parity=true;
            for(int i=0;i<1200;i++){var command=new DriverInput{throttle=i%200<135?.85:0};aCar.IndependentStep(1.0/120,command,true,false);bCar.IndependentStep(1.0/120,command,true,false);parity&=JsonUtility.ToJson(aCar.state)==JsonUtility.ToJson(bCar.state);ac.Update(aCar.state,true,.8,1.0/120);bc.Update(bCar.state,true,.8,1.0/120);}
            Check(parity,"Acoustic model selection leaves actual vehicle dynamics, resources and seeded failures identical");
            var ma=aCar.state.audioExhaust.mixture;var mb=bCar.state.audioExhaust.mixture;bool events=ma.Sequence==mb.Sequence;
            for(int i=1;i<=ma.Sequence&&events;i++){ma.Read(i,out var x);mb.Read(i,out var y);events=JsonUtility.ToJson(x)==JsonUtility.ToJson(y);}
            Check(events&&ma.FuelIn==mb.FuelIn&&ma.FuelBurned==mb.FuelBurned,"Afterfire event timing/energy/seeds and fuel chemistry remain identical across modes");
            foreach(int rate in new[]{8000,22050,44100,48000,96000})
            {
                var synth=new EngineSoundSynth(new EngineSoundProfile(bSpec,tuning),sampleRate:rate);var state=new EngineSoundFrame(4500,.8,.8,.6,.85,turboSpool:.8,turboBoost:.6,airflowKgS:.18);
                var whole=new float[rate*2];synth.Fill(whole,2,rate,state);var split=new float[whole.Length];var splitSynth=new EngineSoundSynth(new EngineSoundProfile(bSpec,tuning),sampleRate:rate);
                for(int i=0;i<rate;){int length=Math.Min(127,rate-i);var chunk=new float[length*2];splitSynth.Fill(chunk,2,rate,state);Array.Copy(chunk,0,split,i*2,chunk.Length);i+=length;}
                Check(whole.SequenceEqual(split),"Wave state survives arbitrary callback chunk boundaries / "+rate);
                var mono=new float[rate];new EngineSoundSynth(new EngineSoundProfile(bSpec,tuning),sampleRate:rate).Fill(mono,1,rate,state);
                Check(Enumerable.Range(0,rate).All(i=>Math.Abs(mono[i]-(whole[i*2]+whole[i*2+1])*.5f)<1e-7),"Mono is the stereo fold without duplicate firing / "+rate);
            }
            var worst=bSpec.Copy();worst.sound.cylinders=16;worst.sound.layout=EngineLayout.V;var voice=new EngineSoundSynth(new EngineSoundProfile(worst,tuning),sampleRate:48000);var buffer=new float[1024];var full=new EngineSoundFrame(12000,1,1,.8,.85,turboSpool:1,turboBoost:.8,airflowKgS:.3);
            for(int i=0;i<32;i++)voice.Fill(buffer,2,48000,full);var timings=new double[256];var watch=new Stopwatch();long start=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<timings.Length;i++){watch.Restart();voice.Fill(buffer,2,48000,full);watch.Stop();timings[i]=watch.Elapsed.TotalMilliseconds;}long allocated=GC.GetAllocatedBytesForCurrentThread()-start;Array.Sort(timings);
            Check(allocated==0,"Warmed complete 16-cylinder turbo callback allocates zero managed bytes");
            Check(timings[242]<512.0/48000*1000*.6,"Editor callback p95 is below 60% of the 512-frame audio budget");
            Log.AppendLine("MEASURE 16 cylinders / 48 kHz / 512 stereo frames: p95 "+timings[242].ToString("F4")+" ms, max "+timings[255].ToString("F4")+" ms; wave delay buffers "+voice.WaveDiagnostics(48000).BufferBytes+" bytes; allocated "+allocated+" bytes.");
            string root=Path.Combine(Folder,"existing-regressions");Directory.CreateDirectory(root);
            Check(EngineSoundVerification.Run(root)>0,"Existing full engine-sound contracts pass with the new default");
            Check(SharedAudioVerification.Run(root)>0,"Existing shared audio controls pass");
            Check(ExhaustPopVerification.Run(root)>0,"Existing exhaust-pop contracts pass");
            Check(InductionVerification.Run(root)>0,"Existing induction contracts pass");
            File.WriteAllText(Path.Combine(Folder,ReceiptName),Log+"Checks: "+checks+"\n",new UTF8Encoding(false));Debug.Log("EXHAUST WAVE PASS: "+checks);
        }
        static double[] Impulse(ExhaustWaveSpec spec)
        {
            var path=new ExhaustWaveguide(spec,48000);var result=new double[8192];for(int i=0;i<result.Length;i++){path.Process(i==0?1:0,0);result[i]=path.Left;}return result;
        }
        public static void Geometry()
        {
            Directory.CreateDirectory(Folder);ReceiptName="geometry-verification.txt";checks=0;Log.Clear();
            var fields=new[]{"headerLengthM","collectorLengthM","tailLengthM","headerDiameterMm","collectorDiameterMm","tailDiameterMm","wallLoss","reflectionStrength","gasTemperatureK","outputGain","chamberLengthM","expansionRatio","packing","resonatorHz","resonatorQ","resonatorDepth"};
            float[] values={1.5f,1.1f,2.3f,55,72,85,.65f,.35f,1100,2,1.1f,6,.85f,340,6,.1f};
            var baseline=Impulse(Mode(3));
            for(int field=0;field<fields.Length;field++){var changed=Mode(3);typeof(ExhaustWaveSpec).GetField(fields[field]).SetValue(changed,values[field]);var output=Impulse(changed);double difference=0;for(int i=0;i<output.Length;i++)difference+=Math.Abs(output[i]-baseline[i]);Check(difference>1e-8,"Explicit parameter changes the actual acoustic transfer / "+fields[field]);}
            var bypass=Mode(0);var changedBypass=bypass.Copy();changedBypass.headerLengthM=2;changedBypass.packing=.9f;
            Check(Impulse(bypass).SequenceEqual(Impulse(changedBypass)),"Approximate path ignores inactive physical geometry");
            foreach(var expected in Hashes().cases){var car=Car(expected.model);var old=car.spec.Copy();old.sound.exhaustAcoustics=ExhaustWaveSpec.Approximate();Check(PcmHash(EngineSoundAudition.Render(old,car.CreateStockTune()))==expected.sha256,"Versioned original PCM hash independently verifies the fallback / "+expected.model);}
            var atmosphere=new TracksidePropagation.Scene().At();
            foreach(string id in Models){var car=Car(id);var old=car.spec.Copy();old.sound.exhaustAcoustics=ExhaustWaveSpec.Approximate();float[] before=EngineSoundAudition.Render(old,car.CreateStockTune()),after=EngineSoundAudition.Render(car.spec,car.CreateStockTune());var outBefore=new float[before.Length+EngineSoundAudition.Rate/2];var outAfter=new float[outBefore.Length];Array.Copy(before,outBefore,before.Length);Array.Copy(after,outAfter,after.Length);new TracksidePropagation(EngineSoundAudition.Rate).Process(outBefore,1,atmosphere);new TracksidePropagation(EngineSoundAudition.Rate).Process(outAfter,1,atmosphere);var combined=new float[outBefore.Length+EngineSoundAudition.Rate/2+outAfter.Length];Array.Copy(outBefore,combined,outBefore.Length);Array.Copy(outAfter,0,combined,outBefore.Length+EngineSoundAudition.Rate/2,outAfter.Length);EngineSoundAudition.WriteWav(Path.Combine(Folder,id+"-trackside-before-after.wav"),combined);Check(combined.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)&&Math.Abs(x)<1),"Actual outdoor listener produces bounded before/after PCM / "+id);}
            foreach(int mask in new[]{0,1,2,3}){
                var car=Car("rally-sedan");var vehicle=car.spec.Copy();vehicle.sound.exhaustAcoustics=ExhaustWaveSpec.Resolve(null,vehicle.sound.exhaustSystem,vehicle.DisplacementLitres,true);vehicle.sound.exhaustAcoustics.pipeMode=Mode(mask).pipeMode;vehicle.sound.exhaustAcoustics.mufflerMode=Mode(mask).mufflerMode;
                var profile=new EngineSoundProfile(vehicle,car.CreateStockTune());var plain=new EngineSoundSynth(profile,sampleRate:48000);var observed=new EngineSoundSynth(profile,sampleRate:48000);bool isolated=true;int observedFrames=0;
                observed.Audit=(i,x)=>{observedFrames++;isolated&=Math.Abs(x.fittedControlsL)<1e-12&&Math.Abs(x.fittedControlsR)<1e-12;};
                var signal=new EngineSoundFrame(4200,.8,.8,.5,.85,turboSpool:.8,turboBoost:.5,airflowKgS:.18);var a=new float[96000];var b=new float[a.Length];plain.Fill(a,2,48000,signal);observed.Fill(b,2,48000,signal);
                Check(a.SequenceEqual(b)&&isolated&&observedFrames==48000,"Per-layer fitted-exhaust tap follows the selected model without altering playback / "+mask);
            }
            var popSpec=new VehicleSpec();popSpec.sound.engineGain=popSpec.sound.turboGain=popSpec.sound.superchargerGain=popSpec.sound.blowOffGain=0;popSpec.engineBehavior.mechanicalClatter=0;popSpec.sound.exhaustSystem=ExhaustSetup.Preset(1);var legacy=popSpec.Copy();legacy.sound.exhaustAcoustics=ExhaustWaveSpec.Approximate();
            var model=new ExhaustMixtureModel(20261020,"wave-afterfire-isolation",initialK:1000);var feed=new ExhaustMixtureModel.Input{dt=1.0/120,fuelKg=3e-6,airKg=5e-5,chamberBurnFraction=.1,intakeK=350,wallTargetK=1000,rpm=4200};
            var previous=new EngineSoundSynth(new EngineSoundProfile(legacy,new Tune()),sampleRate:48000);var current=new EngineSoundSynth(new EngineSoundProfile(popSpec,new Tune()),sampleRate:48000);var frame=new EngineSoundFrame(4200,0,0,0,.85,afterfire:model);var oldPcm=new float[800];var newPcm=new float[800];bool same=true;double energy=0;
            for(int tick=0;tick<240;tick++){model.Step(feed);previous.Fill(oldPcm,2,48000,frame);current.Fill(newPcm,2,48000,frame);for(int i=0;i<newPcm.Length;i++){same&=oldPcm[i]==newPcm[i];energy+=newPcm[i]*newPcm[i];}}
            Check(model.Sequence>0&&energy>0&&same,"Funded hot afterfires retain exact approved PCM across acoustic flags, with nonzero real reaction events");
            File.WriteAllText(Path.Combine(Folder,ReceiptName),Log+"Checks: "+checks+"\n",new UTF8Encoding(false));Debug.Log("EXHAUST GEOMETRY PASS: "+checks);
        }
    }
}
