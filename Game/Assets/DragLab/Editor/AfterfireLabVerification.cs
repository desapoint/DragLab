using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    public static class AfterfireLabVerification
    {
        static readonly string Root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/afterfire-preset-fidelity-20261021"));
        static readonly StringBuilder Log=new StringBuilder();static int checks;
        // Inject a known chemical history packet, then use the ordinary live
        // Connect/Read/Sample path. No reference-mode production switch exists.
        static readonly MethodInfo Record=typeof(ExhaustMixtureModel).GetMethod("Record",BindingFlags.Instance|BindingFlags.NonPublic);
        static void Queue(ExhaustMixtureModel model,ExhaustMixtureModel.Event e)=>Record.Invoke(model,new object[]{e});
        static void Check(bool yes,string label)
        {
            Log.AppendLine((yes?"PASS ":"FAIL ")+label);checks++;
            // One final receipt avoids repeatedly truncating a large file while
            // Windows indexers/readers may have it mapped. Failures still flush.
            if(!yes){Save();throw new InvalidOperationException(label);}
        }
        static float[] Golden(string name)
        {var bytes=File.ReadAllBytes(Path.Combine(Root,name));var x=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,x,0,bytes.Length);return x;}
        static void Save()=>File.WriteAllText(Path.Combine(Root,"fidelity-verification.txt"),Log+"Checks: "+checks+"\n",new UTF8Encoding(false));
        static ExhaustMixtureModel.Event Reaction(double energy=.2)
            =>new ExhaustMixtureModel.Event{frontId=1,zone=4,position=4.5/8,acousticJ=energy,pressurePa=60000,riseSeconds=.0015,burnSeconds=.02,reactingVolumeM3=.0004,gasK=900,flowMps=40,seed=15124311};
        static EngineSoundProfile Profile()
        {
            var fixture=JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/race-audio-audition/26-launch-hard-ignition.race.json")),Encoding.UTF8));
            return new EngineSoundProfile(fixture.spec,fixture.tune);
        }
        static double At(float[] x,int i)=>i<0||i>=x.Length?0:x[i];
        public static void Run()
        {
            Directory.CreateDirectory(Root);Log.Clear();checks=0;var profile=Profile();
            Check(AfterfireLabSettings.HardwarePreset(profile)==3,"Tempest uses exact Turbo Pop & Flame preset, without Shotgun blend or reaction remapping");
            foreach(int rate in new[]{24000,44100,48000,96000,192000})for(int preset=0;preset<10;preset++)
            {
                var golden=Golden("reference-"+rate+"-"+preset+".f32");var coherent=Golden("pressure-"+rate+"-"+preset+".f32");
                var p=AfterfireLabSettings.Preset(preset);var shape=new AfterfireLabSource(rate);shape.Begin(p,15124311);
                int latency=shape.LatencyFrames;double max=0,err=0,energy=0,pressureError=0;
                for(int i=0;i<latency;i++)shape.Sample();
                for(int i=0;i<golden.Length;i++)
                {double d=shape.Sample()-golden[i];pressureError=Math.Max(pressureError,Math.Abs(shape.PressureSample-coherent[i]));max=Math.Max(max,Math.Abs(d));err+=d*d;energy+=golden[i]*golden[i];}
                Log.AppendLine("MEASURE source / preset "+preset+" / "+rate+" / max "+max.ToString("R")+" / relative RMS "+Math.Sqrt(err/Math.Max(1e-30,energy)).ToString("R"));
                Log.AppendLine("MEASURE coherent pressure error "+pressureError.ToString("R"));
                Check(max<2e-5&&err<energy*4e-10&&pressureError<2e-5,"one actual source path matches full JavaScript preset including final padded tail / "+preset+" / "+rate);
                var model=new ExhaustMixtureModel(123,"preset-parity");var live=new AfterfireAudioSynth(profile,rate,p);live.Connect(model);var e=Reaction();Queue(model,e);
                var expectedPipe=new AfterfirePipeResponse(profile,rate);expectedPipe.Begin(e,AfterfireAudioSynth.SourceCutoffHz);
                // Compare the REAL history reader, burst/zone mixer and installed
                // path against JS PCM passed through that same physical path.
                var outdoorsA=new TracksidePropagation(rate);var outdoorsB=new TracksidePropagation(rate);
                var geometry=new TracksidePropagation.Scene(298,.5,101325).At();
                var actualBlock=new float[256];var expectedBlock=new float[256];int offset=0,dryIndex=0;double dryError=0;max=err=energy=0;
                while(offset<golden.Length+rate/4)
                {
                    for(int i=0;i<128;i++,offset++)
                    {
                        double expected=expectedPipe.Sample(At(golden,offset-latency),At(coherent,offset-latency))*profile.popGain*.35;
                        double actual=live.Sample();if(Math.Abs(actual-expected)>dryError){dryError=Math.Abs(actual-expected);dryIndex=offset;}actualBlock[2*i]=actualBlock[2*i+1]=(float)actual;expectedBlock[2*i]=expectedBlock[2*i+1]=(float)expected;
                    }
                    outdoorsA.Process(actualBlock,2,geometry);outdoorsB.Process(expectedBlock,2,geometry);
                    for(int i=0;i<actualBlock.Length;i++){double d=actualBlock[i]-expectedBlock[i];max=Math.Max(max,Math.Abs(d));err+=d*d;energy+=expectedBlock[i]*expectedBlock[i];}
                }
                Log.AppendLine("MEASURE gameplay/outdoor / preset "+preset+" / "+rate+" / max "+max.ToString("R")+" / relative RMS "+Math.Sqrt(err/Math.Max(1e-30,energy)).ToString("R")+" / dry error "+dryError.ToString("R")+" at "+dryIndex);
                Check(max<2e-5&&err<energy*4e-10&&live.StartedBursts==1&&live.Delivered==1,"actual gameplay plus installed exhaust/outdoors preserves JS preset / "+preset+" / "+rate);
            }
            CheckFunding(profile);CheckZones(profile);
            Save();UnityEngine.Debug.Log("PRESET FIDELITY PASS "+checks);
        }
        static void CheckFunding(EngineSoundProfile profile)
        {
            foreach(int rate in new[]{8000,16000,24000,44100,48000,192000})
            {
                var ma=new ExhaustMixtureModel(123,"a");var mb=new ExhaustMixtureModel(123,"b");
                var a=new AfterfireAudioSynth(profile,rate);var b=new AfterfireAudioSynth(profile,rate);a.Connect(ma);b.Connect(mb);
                var e=Reaction();Queue(ma,e);Queue(mb,Reaction(.05));double delta=0,energy=0;bool finite=true;
                long allocated=GC.GetAllocatedBytesForCurrentThread();a.Sample();
                Check(GC.GetAllocatedBytesForCurrentThread()==allocated,"first actual gameplay reaction/sample allocates nothing after construction / "+rate);
                // Reset both test readers, preserving the same immutable hardware.
                a.Clear();b.Clear();ma=new ExhaustMixtureModel(123,"a");mb=new ExhaustMixtureModel(123,"b");a.Connect(ma);b.Connect(mb);Queue(ma,e);Queue(mb,Reaction(.05));
                for(int i=0;i<rate;i++){double x=a.Sample(),y=b.Sample();delta=Math.Max(delta,Math.Abs(y-.5*x));finite&=!double.IsNaN(x)&&!double.IsInfinity(x);energy+=x*x;}
                Check(finite&&energy>0&&delta<1e-7,"quarter simulated energy halves real gameplay pressure without changing timbre / "+rate);
                a.Clear();b.Clear();ma=new ExhaustMixtureModel(123,"a");mb=new ExhaustMixtureModel(123,"b");a.Connect(ma);b.Connect(mb);
                long beforeA=a.StartedBursts,beforeB=b.StartedBursts;double energyA=a.FundedAcousticEnergy,energyB=b.FundedAcousticEnergy;
                Queue(ma,e);Queue(mb,Reaction(.1));Queue(mb,Reaction(.1));delta=0;
                for(int i=0;i<rate/2;i++)delta=Math.Max(delta,Math.Abs(a.Sample()-b.Sample()));
                Check(delta<1e-7&&a.StartedBursts==beforeA+1&&b.StartedBursts==beforeB+1&&Math.Abs((a.FundedAcousticEnergy-energyA)-(b.FundedAcousticEnergy-energyB))<1e-12,"splitting one same-time chemical packet cannot add a bang or amplify it / "+rate);
                a.Clear();b.Clear();ma=new ExhaustMixtureModel(123,"a");mb=new ExhaustMixtureModel(123,"b");a.Connect(ma);b.Connect(mb);Queue(ma,e);
                var flags=e;flags.pressurePa=100;flags.twoStep=true;flags.limiter=true;flags.cause=AfterfireCause.AntiLag;flags.riseSeconds=.00025;flags.burnSeconds=.1;Queue(mb,flags);delta=0;
                for(int i=0;i<rate/2;i++)delta=Math.Max(delta,Math.Abs(a.Sample()-b.Sample()));
                Check(delta<1e-7,"flags and numerical pressure/rise/burn details cannot remap the preset waveform at equal acoustic energy / "+rate);
                a.Clear();ma=new ExhaustMixtureModel(123,"a");a.Connect(ma);Queue(ma,Reaction(0));energy=0;
                for(int i=0;i<rate/10;i++)energy+=Math.Abs(a.Sample());Check(energy==0,"no chemical energy gives exact silence / "+rate);
                long previous=a.StartedBursts;double funded=a.FundedAcousticEnergy;
                for(int tick=0;tick<240;tick++)
                {
                    var next=Reaction(.002);next.time=a.Clock;next.pressurePa=1000+(tick%7)*10000;next.limiter=tick%2==0;Queue(ma,next);
                    for(int i=0;i<rate/120;i++)a.Sample();funded+=next.acousticJ;
                }
                Check(a.StartedBursts==previous+1&&Math.Abs(a.FundedAcousticEnergy-funded)<1e-10,"all 240 continuing reactions fund one flame envelope instead of 240 sharp attacks / "+rate);
                allocated=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<rate;i++)a.Sample();
                Check(GC.GetAllocatedBytesForCurrentThread()==allocated&&a.PressurePulseOverflows==0&&a.TailSteals==0,"zero warmed callback allocations, pressure overflow or stolen tails / "+rate);
                for(int i=0;i<rate*2;i++)a.Sample();Check(a.Sample()==0,"finite preset and installed paths retire to exact silence / "+rate);
                a.Clear();Check(a.Sample()==0,"clear discards all burst/zone history / "+rate);
            }
        }
        static void CheckZones(EngineSoundProfile profile)
        {
            const int rate=48000;var p=AfterfireLabSettings.Hardware(profile);int preset=AfterfireLabSettings.HardwarePreset(profile);
            var x=Golden("reference-48000-"+preset+".f32");var pressure=Golden("pressure-48000-"+preset+".f32");
            var model=new ExhaustMixtureModel(123,"zones");var live=new AfterfireAudioSynth(profile,rate);live.Connect(model);
            var first=Reaction();var later=first;later.time=.04;later.zone=6;later.position=6.5/8;
            Queue(model,first);Queue(model,later);
            var pipeA=new AfterfirePipeResponse(profile,rate);pipeA.Begin(first,AfterfireAudioSynth.SourceCutoffHz);
            var pipeB=new AfterfirePipeResponse(profile,rate);bool began=false;double gain=0,alpha=1-Math.Exp(-1/(rate*.005)),max=0;
            for(int i=0;i<rate;i++)
            {
                double dry=At(x,i-8),wave=At(pressure,i-8);double expected=pipeA.Sample(dry,wave);
                if(!began&&live.Clock>=later.time){pipeB.Begin(later,AfterfireAudioSynth.SourceCutoffHz);began=true;}
                if(began){gain+=alpha*(1-gain);expected+=pipeB.Sample(dry*gain,wave*gain);}
                max=Math.Max(max,Math.Abs(live.Sample()-expected*profile.popGain*.35));
            }
            Check(began&&max<2e-5&&live.StartedBursts==1&&live.Delivered==2,"later exhaust zone uses the same JS envelope phase with its own physical transfer, without another attack");
            var newFlame=Reaction();newFlame.frontId=2;newFlame.time=live.Clock;Queue(model,newFlame);live.Sample();
            Check(live.StartedBursts==2,"a genuinely new chemical flame starts another preset burst");
        }
        public static void VerifyAndBuild()
        {
            Run();VerifyCausality();RuntimeAudioIntegrationVerification.OutputDirectory=Root;RuntimeAudioIntegrationVerification.TracksideListening=true;
            RuntimeAudioIntegrationVerification.Run();RuntimeAudioIntegrationVerification.CheckRaces();PlayerBuild.BuildPlayerOnly();
        }
        public static void VerifyCausality(){ConditionDrivenAfterfireVerification.OutputDirectory=Root;ConditionDrivenAfterfireVerification.Run();}
    }
}
