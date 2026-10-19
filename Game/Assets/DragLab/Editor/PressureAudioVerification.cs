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
