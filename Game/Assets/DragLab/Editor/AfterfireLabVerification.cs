using System;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    public static class AfterfireLabVerification
    {
        static readonly string Root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/afterfire-lab-v3-20261021"));
        static readonly StringBuilder Log=new StringBuilder();static int checks;
        static void Check(bool value,string label)
        {
            Log.AppendLine((value?"PASS ":"FAIL ")+label);checks++;
            File.WriteAllText(Path.Combine(Root,"source-verification.txt"),Log+"Checks: "+checks+"\n",new UTF8Encoding(false));
            if(!value)throw new InvalidOperationException(label);
        }
        static ExhaustMixtureModel.Event Reaction(double energy=.2,double pressure=60000,double rise=.0015,double burn=.02)
            =>new ExhaustMixtureModel.Event{acousticJ=energy,pressurePa=pressure,riseSeconds=rise,burnSeconds=burn,reactingVolumeM3=.0004,gasK=900,flowMps=40,seed=15124311};
        static EngineSoundProfile Profile()
        {
            var fixture=JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/race-audio-audition/26-launch-hard-ignition.race.json")),Encoding.UTF8));
            return new EngineSoundProfile(fixture.spec,fixture.tune);
        }
        public static void ProfileCost()
        {
            var e=Reaction();var p=AfterfireLabSettings.Reaction(AfterfireLabSettings.Hardware(Profile()),Profile(),e);
            var live=new AfterfireLabSource(48000);var reference=new AfterfireLabSource(48000);var old=new ReactivePressureBurst(48000);
            var pipe=new AfterfirePipeResponse(Profile(),48000);pipe.Begin(e,5000);live.Begin(p,e);reference.BeginReference(p,15124311);old.Begin(e);
            var w=new System.Diagnostics.Stopwatch();var result=new StringBuilder();double sink=0;
            for(int pass=0;pass<3;pass++)
            {
                w.Restart();for(int i=0;i<96000;i++){if(i%400==0){e.time=i/48000.0;live.Excite(e);}sink+=live.Sample();}w.Stop();result.AppendLine("live source 96000 frames: "+w.Elapsed.TotalMilliseconds);
                w.Restart();for(int i=0;i<96000;i++)sink+=reference.Sample();w.Stop();result.AppendLine("reference source 96000 frames: "+w.Elapsed.TotalMilliseconds);
                w.Restart();for(int i=0;i<96000;i++){if(i%400==0){e.time=i/48000.0;old.Excite(e);}sink+=old.Sample();}w.Stop();result.AppendLine("old source 96000 frames: "+w.Elapsed.TotalMilliseconds);
                w.Restart();for(int i=0;i<96000;i++)sink+=pipe.Sample(.001,.001);w.Stop();result.AppendLine("installed pipe 96000 frames: "+w.Elapsed.TotalMilliseconds);
            }
            result.AppendLine("sink "+sink);File.WriteAllText(Path.Combine(Root,"dsp-cost.txt"),result.ToString(),new UTF8Encoding(false));
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root);Log.Clear();checks=0;
            foreach(int rate in new[]{24000,44100,48000,96000,192000})for(int preset=0;preset<10;preset++)
            {
                string file=Path.Combine(Root,"reference-"+rate+"-"+preset+".f32");
                byte[] bytes=File.ReadAllBytes(file);var expected=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,expected,0,bytes.Length);
                var source=new AfterfireLabSource(rate);source.BeginReference(AfterfireLabSettings.Preset(preset),15124311);
                double max=0,error=0,energy=0;
                for(int i=0;i<source.LatencyFrames;i++)source.Sample();
                // The final look-ahead FIR padding is zero in the finite HTML
                // render; live DSP keeps generating until its physical tail ends.
                for(int i=0;i<expected.Length-32;i++)
                {
                    double actual=source.Sample(),d=actual-expected[i];max=Math.Max(max,Math.Abs(d));error+=d*d;energy+=expected[i]*expected[i];
                }
                double relative=Math.Sqrt(error/Math.Max(1e-30,energy));
                Log.AppendLine("MEASURE preset "+preset+" / "+rate+" / max "+max.ToString("R")+" / relative RMS "+relative.ToString("R"));
                Check(max<2e-5&&relative<2e-5,"streaming C# matches original JavaScript pressure core / preset "+preset+" / "+rate);
            }
            var profile=Profile();var hardware=AfterfireLabSettings.Hardware(profile);
            foreach(int rate in new[]{8000,16000,24000,44100,48000,192000})
            {
                var e=Reaction();var p=AfterfireLabSettings.Reaction(hardware,profile,e);
                var a=new AfterfireLabSource(rate);var b=new AfterfireLabSource(rate);a.Begin(p,e);b.Begin(p,e);
                var cold=new AfterfireLabSource(rate);long firstAllocated=GC.GetAllocatedBytesForCurrentThread();cold.Begin(p,e);for(int i=0;i<100;i++)cold.Sample();
                Check(GC.GetAllocatedBytesForCurrentThread()==firstAllocated,"first reaction and first samples allocate nothing after hardware construction / "+rate);
                bool exact=true,finite=true;double energy=0;
                for(int i=0;i<rate;i++){double x=a.Sample();exact&=x==b.Sample();finite&=!double.IsNaN(x)&&!double.IsInfinity(x);energy+=x*x;}
                Check(exact&&finite&&energy>0,"deterministic finite live source / "+rate);
                a.Begin(p,e);var half=e;half.acousticJ*=.5;b.Begin(p,half);b.Excite(half);double delta=0;
                for(int i=0;i<rate/4;i++)delta=Math.Max(delta,Math.Abs(a.Sample()-b.Sample()));
                Check(delta<1e-6&&b.FundedEnergy==.2,"same-time packet subdivision retains energy and waveform / "+rate);
                var rising=e;rising.pressurePa*=1.5;rising.time=.0001;
                a.Begin(p,e);b.Begin(p,e);a.Excite(rising);var split=rising;split.acousticJ*=.5;b.Excite(split);b.Excite(split);delta=0;
                for(int i=0;i<rate/4;i++)delta=Math.Max(delta,Math.Abs(a.Sample()-b.Sample()));
                Check(delta<1e-6,"splitting a renewal cannot amplify a different pressure front queued in the same sample / "+rate);
                a.Begin(p,e);var quiet=e;quiet.acousticJ*=.25;b.Begin(p,quiet);delta=0;
                for(int i=0;i<rate/4;i++)delta=Math.Max(delta,Math.Abs(a.Sample()*.5-b.Sample()));
                Check(delta<1e-6,"quarter chemical acoustic energy halves pressure amplitude / "+rate);
                var flags=e;flags.twoStep=true;flags.limiter=true;flags.cause=AfterfireCause.AntiLag;
                a.Begin(p,e);b.Begin(AfterfireLabSettings.Reaction(hardware,profile,flags),flags);exact=true;
                for(int i=0;i<rate/4;i++)exact&=a.Sample()==b.Sample();
                Check(exact,"diagnostic limiter/launch/anti-lag labels cannot choose a recording or invent pulses / "+rate);
                a.Begin(p,e);var different=e;different.seed++;b.Begin(p,different);delta=0;
                for(int i=0;i<rate/4;i++)delta+=Math.Abs(a.Sample()-b.Sample());
                Check(delta>0,"seed changes turbulent detail / "+rate);
                a.Begin(p,Reaction(energy:0));energy=0;for(int i=0;i<rate/4;i++)energy+=Math.Abs(a.Sample());
                Check(energy==0,"no chemical acoustic funding gives exact silence / "+rate);
                a.Begin(p,e);double initial=0,settled=0;
                for(int i=0;i<rate;i++)
                {
                    if(i>0&&i%(rate/120)==0){var feed=e;feed.time=i/(double)rate;feed.acousticJ=.002;a.Excite(feed);}
                    a.Sample();if(i<rate/20)initial+=a.PressureSample*a.PressureSample;if(i>rate*.95)settled+=a.PressureSample*a.PressureSample;
                }
                Check(settled<initial*.001,"steady pressure funds combustion without another shock per integration tick / "+rate);
                for(int i=0;i<rate*3;i++)a.Sample();Check(a.Quiet&&a.Sample()==0,"finite fuel-funded tail retires to silence / "+rate);
                long allocated=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<rate;i++){if(i%200==0){e.time=4+i/(double)rate;a.Excite(e);}a.Sample();}
                Check(allocated==GC.GetAllocatedBytesForCurrentThread()&&a.PulseOverflows==0,"zero warmed sample/event allocations or pressure-pulse overflow / "+rate);
                a.Clear();Check(a.Sample()==0,"clear removes all source history / "+rate);
            }
            // All fitted combinations use the same core and physically selected
            // paths. Document rather than silently replace the engine bed.
            var modern=new VehicleSpec();var old=modern.Copy();old.sound.cylinders=8;old.sound.layout=EngineLayout.Crossplane;old.engineBehavior.fueling=FuelSystem.Carburetor;
            var small=AfterfireLabSettings.Hardware(new EngineSoundProfile(modern,new Tune()));var classic=AfterfireLabSettings.Hardware(new EngineSoundProfile(old,new Tune()));
            Check(small.tune!=classic.tune&&small.bark!=classic.bark,"hardware/fueling choose different preset-derived acoustic settings");
            var path=new AfterfirePipeResponse(profile,48000);var wallEvent=Reaction();wallEvent.position=.6;wallEvent.zone=4;path.Begin(wallEvent,5000);
            for(int i=0;i<48000;i++)path.Sample(.001,i==0?1:0);
            var modes=(Array)typeof(AfterfirePipeResponse).GetField("walls",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(path);
            bool silent=true;foreach(object mode in modes)foreach(string field in new[]{"z1","z2"})silent&=(double)mode.GetType().GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(mode)==0;
            Check(silent,"silent wall modes retire to zero while the gas body still flows");
            UnityEngine.Debug.Log("AFTERFIRE LAB V3 PASS "+checks);
        }
        public static void VerifyAndBuild()
        {
            Run();ProfileCost();
            RuntimeAudioIntegrationVerification.OutputDirectory=Root;RuntimeAudioIntegrationVerification.TracksideListening=true;
            RuntimeAudioIntegrationVerification.Run();RuntimeAudioIntegrationVerification.CheckRaces();
            PlayerBuild.BuildPlayerOnly();
        }
        public static void FinalCheckAndBuild()
            =>VerifyAndBuild();
        public static void SourceCheckAndBuild(){Run();PlayerBuild.BuildPlayerOnly();}
    }
}
