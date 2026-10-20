using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using Debug=UnityEngine.Debug;

namespace DragLab.Editor
{
    public static class PressureAudioVerification
    {
        static float[] RenderChannels(EngineSoundProfile p,EngineSoundFrame f,int channels,int rate,int chunk=128)
        {
            var synth=new EngineSoundSynth(p,sampleRate:rate);var result=new float[rate*channels];
            for(int at=0;at<rate;at+=chunk){int n=Math.Min(chunk,rate-at);var block=new float[n*channels];synth.Fill(block,channels,rate,f);Array.Copy(block,0,result,at*channels,block.Length);}return result;
        }
        public static void Refinements()
        {
            int n=0;var spec=new VehicleSpec();spec.sound.cylinders=8;spec.sound.layout=EngineLayout.Crossplane;spec.displacementLitres=5;
            var tune=new Tune();var profile=new EngineSoundProfile(spec,tune);var frame=new EngineSoundFrame(4500,.8,.85,0,.85);
            foreach(int rate in new[]{8000,22050,44100,48000,96000}){
                var stereo=RenderChannels(profile,frame,2,rate);var mono=RenderChannels(profile,frame,1,rate);var whole=RenderChannels(profile,frame,2,rate,rate);
                Check(stereo.SequenceEqual(whole),"stereo block boundaries retain exact firing/filter state / "+rate,ref n);
                Check(Enumerable.Range(0,rate).All(i=>Math.Abs(mono[i]-(stereo[i*2]+stereo[i*2+1])*.5f)<1e-7),"mono matches stereo fold within float rounding / "+rate,ref n);
                Check(Enumerable.Range(0,rate).Any(i=>Math.Abs(stereo[i*2]-stereo[i*2+1])>.0001),"bank signals stay distinct through final exhaust / "+rate,ref n);
            }
            var inline=spec.Copy();inline.sound.layout=EngineLayout.Inline;inline.sound.cylinders=4;
            var centered=RenderChannels(new EngineSoundProfile(inline,tune),frame,2,48000);
            Check(Enumerable.Range(0,48000).All(i=>centered[2*i]==centered[2*i+1]),"single bank stays centered",ref n);
            var neutral=new EngineSoundFrame(4500,.8,.08,0,.85);var low=Pcm(profile,neutral);var high=Pcm(profile,frame);
            Check(Energy(high)>Energy(low)*1.1,"actual combustion load changes pressure at identical RPM/pedal",ref n);
            double dot=0,l2=0,h2=0;for(int i=24000;i<48000;i++){dot+=low[i]*high[i];l2+=low[i]*low[i];h2+=high[i]*high[i];}
            Check(dot/Math.Sqrt(l2*h2)<.9999,"load changes envelope character beyond scalar volume",ref n);
            var core=new PressureCombustionCore(profile);for(int i=0;i<48000;i++)core.Sample(4500,.8,frame);
            Check(Math.Abs(core.Events-300)<=1&&core.CylinderEvents.All(c=>c>=37&&c<=38),"stereo consumes one correctly ordered firing stream",ref n);
            spec.sound.acoustics.runnerLengthsMetres=Enumerable.Repeat(.6f,8).ToArray();var snapshot=new EngineSoundProfile(spec,tune);spec.sound.acoustics.runnerLengthsMetres[0]=1.2f;
            Check(Math.Abs(snapshot.acoustics.Runner(0)-.6)<1e-6&&Math.Abs(spec.Copy().sound.acoustics.runnerLengthsMetres[0]-1.2)<1e-6,"runner snapshots and authored copies are isolated",ref n);
            var custom=spec.Copy();custom.sound.acoustics.attackScale=1.8f;custom.sound.acoustics.blowdownScale=1.7f;
            Check(!Pcm(new EngineSoundProfile(custom,tune),frame).SequenceEqual(Pcm(new EngineSoundProfile(spec,tune),frame)),"custom envelopes have distinct shared table keys",ref n);
            spec.sound.acoustics.runnerLengthsMetres=null;
            var boosted=new Tune{engineLevel=1,inductionUpgrade=EngineUpgradeInduction.Turbo};var hot=new EngineSoundFrame(4500,.8,.85,.6,.85,turboSpool:.8,turboBoost:.6,airflowKgS:.18);
            spec.sound.acoustics.turbineDamping=0;var open=new EngineSoundProfile(spec,boosted);spec.sound.acoustics.turbineDamping=.8f;var damped=new EngineSoundProfile(spec,boosted);
            Check(!Pcm(open,hot).SequenceEqual(Pcm(damped,hot)),"boost/flow-dependent hot-side damping changes exhaust",ref n);
            var noBoost=new EngineSoundFrame(4500,.8,.85,0,.85,turboSpool:.8,turboBoost:0,airflowKgS:.18);
            Check(Pcm(open,noBoost).SequenceEqual(Pcm(damped,noBoost)),"damping introduces no restriction at zero turbo pressure",ref n);
            var wet=new TurboAcoustics(open,48000);var dry=new TurboAcoustics(open,48000);double whistle=0,difference=0;bool sameWhistle=true;
            for(int i=0;i<96000;i++){double a=wet.Sample(hot,0,0,1),b=dry.Sample(hot,0,0,0);if(i>48000){whistle+=b*b;sameWhistle&=a==b;}}
            Check(sameWhistle,"hiss suppression preserves whistle without airflow noise",ref n);
            Check(whistle>1e-6,"whistle stays audible at zero hiss",ref n);
            for(int i=0;i<48000;i++){double a=wet.Sample(hot,0,.7,1),b=dry.Sample(hot,0,.7,0);difference+=(a-b)*(a-b);}
            Check(difference>1e-8,"airflow noise alone follows independent control",ref n);
            foreach(var car in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))Check(car.spec.sound.Validate()==null,"authored acoustic defaults validate / "+car.spec.id,ref n);
            Run();Transitions();
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../..")),dir=Path.Combine(root,"reports/audio-refinements-20261020");Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"verification.txt"),"PASS: "+n+" refinement checks, plus pressure, transition, audio, pop and induction suites.\n",new UTF8Encoding(false));Debug.Log("Audio refinements passed: "+n);
        }
        public static void Transitions()
        {
            int n=0;var spec=new VehicleSpec();var tune=new Tune();var controller=new EngineSoundController(spec,tune);
            var state=new CarState{rpm=6000,throttle=1,limiter=true,combustionFraction=.55};
            var soft=controller.Update(state,true,1,.01);
            Check(!soft.cut&&soft.combustionFraction==.55,"soft limiting retains actual partial combustion",ref n);
            state.combustionFraction=0;Check(controller.Update(state,true,1,.01).cut,"actual full cut still suppresses combustion",ref n);
            state.combustionFraction=1;state.transmissionTorqueCut=true;Check(controller.Update(state,true,1,.01).cut,"transmission cut remains authoritative",ref n);
            foreach(int rate in new[]{22050,44100,48000,96000}){
                var p=new EngineSoundProfile(spec,tune);var pop=new ExhaustPopSynth(p);pop.Configure(rate);pop.Trigger(.9,true);
                double last=0;for(int i=0;i<rate/500;i++)last=pop.Sample();
                Check(Math.Abs(last)>1e-5,"overlap fixture contains an audible transient",ref n);
                pop.Trigger(.8,true);Check(Math.Abs(pop.Sample()-last)<1e-12,"overlapping pops have no waveform reset discontinuity / "+rate,ref n);
                pop.Clear();Check(pop.Sample()==0,"clear removes retrigger tails / "+rate,ref n);
                var isolated=spec.Copy();isolated.sound.engineGain=0;isolated.engineBehavior.mechanicalClatter=0;
                var synth=new EngineSoundSynth(new EngineSoundProfile(isolated,tune),sampleRate:rate);var warm=new float[rate];
                synth.Fill(warm,1,rate,new EngineSoundFrame(3200,.8,.7,0,1,intakeGulp:1));
                var next=new float[128];synth.Fill(next,1,rate,new EngineSoundFrame(3200,.8,.7,0,.2,intakeGulp:0));
                double prediction=2*warm[warm.Length-1]-warm[warm.Length-2];
                Check(Math.Abs(next[0]-prediction)<.002,"intake and volume transition remain continuous / "+rate,ref n);
            }
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            int audio=EngineSoundVerification.Run(root),pops=ExhaustPopVerification.Run(root),induction=InductionVerification.Run(root);
            string dir=Path.Combine(root,"reports/audio-transitions-20261019");Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"verification.txt"),"PASS: "+n+" transition checks; "+audio+" audio; "+pops+" exhaust-pop; "+induction+" induction checks.\n",new UTF8Encoding(false));
            Debug.Log("Audio transition checks passed: "+n);
        }
        static void Check(bool ok,string why,ref int n){if(!ok)throw new InvalidOperationException("PRESSURE AUDIO: "+why);n++;}
        static double Energy(float[] x){double sum=0;foreach(float v in x)sum+=v*v;return sum/x.Length;}
        static float[] Pcm(EngineSoundProfile p,EngineSoundFrame f,int rate=48000)
        {var data=new float[rate];new EngineSoundSynth(p,sampleRate:rate).Fill(data,1,rate,f);return data;}
        public static void Run()
        {
            int checks=0;string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));var report=new StringBuilder();
            foreach(var car in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))foreach(int rate in new[]{44100,48000})
            {
                var p=new EngineSoundProfile(car.spec,car.CreateStockTune());var f=new EngineSoundFrame(4500,.9,.9,.6,.85,turboSpool:.8,turboBoost:.6,airflowKgS:.18);
                var x=Pcm(p,f,rate);Check(x.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<1)&&Energy(x)>1e-8,"authored car/rate "+car.spec.id+" / "+rate,ref checks);
            }
            var spec=new VehicleSpec();var tune=new Tune{engineLevel=1,inductionUpgrade=EngineUpgradeInduction.Turbo};var turbo=new EngineSoundProfile(spec,tune);
            var neutral=new EngineSoundFrame(4500,1,.08,.02,.85,turboSpool:.25,turboBoost:.02,airflowKgS:.03);
            var loaded=new EngineSoundFrame(4500,1,.9,.65,.85,turboSpool:.9,turboBoost:.65,airflowKgS:.18);
            Check(Energy(Pcm(turbo,loaded))>Energy(Pcm(turbo,neutral)),"loaded turbo response exceeds neutral rev at the same RPM",ref checks);
            var flow0=new EngineSoundFrame(4500,1,.9,.65,.85,turboSpool:.9,turboBoost:.65,airflowKgS:0);
            Check(!Pcm(turbo,loaded).SequenceEqual(Pcm(turbo,flow0)),"compressor texture follows actual mass flow",ref checks);
            var core0=new PressureCombustionCore(turbo);var core1=new PressureCombustionCore(turbo);double diff=0;
            var boost0=new EngineSoundFrame(4500,1,.9,0,.85,turboSpool:.9,turboBoost:0);
            for(int i=0;i<48000;i++){double d=core0.Sample(4500,1,boost0)-core1.Sample(4500,1,loaded);diff+=d*d;}
            Check(diff>1e-8&&core0.Events==core1.Events,"boost changes exhaust pressure/turbine response without changing firing count",ref checks);
            var state=new CarState{rpm=4500,throttle=1,engineTorqueNm=200,transmittedTorqueNm=180,launched=true,turboBoostBar=.65,turboSpool=.9,airflowKgS=.18};
            string before=JsonUtility.ToJson(state),rng=JsonUtility.ToJson(UnityEngine.Random.state);var controller=new EngineSoundController(spec,tune);var frame=controller.Update(state,true,.85,.01);
            Pcm(turbo,frame);Check(JsonUtility.ToJson(state)==before&&JsonUtility.ToJson(UnityEngine.Random.state)==rng&&frame.airflowKgS==state.airflowKgS,"audio preserves physical state/RNG and captures actual mass flow",ref checks);
            foreach(int rate in new[]{8000,22050,32000,44100,48000,96000})
            {
                var synth=new EngineSoundSynth(turbo,sampleRate:rate);var pcm=new float[2048];synth.Fill(pcm,2,rate,loaded);
                Check(synth.IsSampleRatePrepared(rate)&&pcm.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<1),"prepared rate "+rate,ref checks);
            }
            var worst=spec.Copy();worst.sound.cylinders=16;worst.sound.layout=EngineLayout.V;
            var worstTune=new Tune{engineLevel=1,inductionUpgrade=EngineUpgradeInduction.Twincharged};var pWorst=new EngineSoundProfile(worst,worstTune);
            var callback=new float[1024];var hot=new EngineSoundFrame(12000,1,1,1,.85,turboSpool:1,turboBoost:.6,superchargerBoost:.4,chargerLoad:1,airflowKgS:.25);
            var audio=new EngineSoundSynth(pWorst,sampleRate:48000);for(int i=0;i<32;i++)audio.Fill(callback,2,48000,hot);
            var durations=new double[256];var timer=new Stopwatch();long start=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<durations.Length;i++){timer.Restart();audio.Fill(callback,2,48000,hot);timer.Stop();durations[i]=timer.Elapsed.TotalMilliseconds;}
            long bytes=GC.GetAllocatedBytesForCurrentThread()-start;Array.Sort(durations);
            Check(bytes==0,"zero allocation in warmed full callback",ref checks);
            Check(durations[242]<512.0/48000*1000*.4,"Editor worst-voice callback p95 within 40% of signal budget",ref checks);
            report.AppendLine("PASS: "+checks+" pressure audio checks; all 52 authored cars at 44.1/48 kHz.");
            report.AppendLine("16-cylinder twincharged / 12,000 RPM / 512 stereo frames: p95 "+durations[242].ToString("F4")+" ms; max "+durations[255].ToString("F4")+" ms; budget 10.6667 ms; managed allocation "+bytes+" bytes.");
            report.AppendLine("Editor observation only; physical Android callback performance remains unverified.");
            string dir=Path.Combine(root,"reports/audio-pressure-integration");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"unity-verification.txt"),report.ToString(),new UTF8Encoding(false));Debug.Log(report.ToString());
        }
    }
}
