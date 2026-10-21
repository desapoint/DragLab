using System;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    public static class ReactiveAfterfireVerification
    {
        const int Rate=48000;
        static readonly string Root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/reactive-afterfire-20261021"));
        static readonly StringBuilder Log=new StringBuilder();static int checks;
        static void Check(bool ok,string text)
        {Log.AppendLine((ok?"PASS ":"FAIL ")+text);checks++;if(!ok)throw new InvalidOperationException(text);}
        static ExhaustMixtureModel.Event Reaction(double energy=.2)=>new ExhaustMixtureModel.Event{
            acousticJ=energy,burnSeconds=.02,riseSeconds=.0015,reactingVolumeM3=.0004,gasK=900,pressurePa=60000};
        static AfterfireLabSource Source(uint seed=15124311)
        {var s=new AfterfireLabSource(Rate);s.BeginReactive(AfterfireLabSettings.Preset(3),seed);return s;}
        static double Compare(AfterfireLabSource a,AfterfireLabSource b,int count)
        {double max=0;for(int i=0;i<count;i++)max=Math.Max(max,Math.Abs(a.Sample()-b.Sample()));return max;}
        static double Energy(AfterfireLabSource a,int count)
        {double sum=0;for(int i=0;i<count;i++){double x=a.Sample();sum+=x*x;}return sum;}
        static void ReactiveChecks()
        {
            var a=Source();var b=Source();a.React(Reaction());b.React(Reaction(.1));b.React(Reaction(.1));
            Check(Compare(a,b,Rate)<1e-7,"same-time packet subdivision preserves the complete live waveform");
            a=Source();b=Source();a.React(Reaction());b.React(Reaction());
            Check(Compare(a,b,Rate/5)==0,"identical past reactions give identical PCM before any future fuel is supplied");
            b.React(Reaction(.05));double changed=Compare(a,b,Rate/2);
            Check(changed>1e-4,"new burning fuel changes an already running sound instead of being ignored by a fixed envelope");
            a=Source();b=Source();a.React(Reaction());b.React(Reaction());
            Compare(a,b,Rate/5);b.React(Reaction(.05));double pressureError=0;
            for(int i=0;i<Rate/2;i++){a.Sample();b.Sample();pressureError=Math.Max(pressureError,Math.Abs(a.PressureSample-b.PressureSample));}
            Check(pressureError==0,"a continuing numerical reaction energizes body/tail without restarting or turning up the old pressure bang");
            a=Source();b=Source();var shortBurn=Reaction();shortBurn.burnSeconds=.002;var longBurn=Reaction();longBurn.burnSeconds=.12;
            a.React(shortBurn);b.React(longBurn);double shortEnergy=Energy(a,Rate),longEnergy=Energy(b,Rate);
            Check(longEnergy>shortEnergy*1.05,"actual burn time changes the energy-release decay");
            a=Source();b=Source();var hot=Reaction();hot.gasK=1400;hot.reactingVolumeM3=.00008;hot.pressurePa=180000;hot.riseSeconds=.0003;
            a.React(Reaction());b.React(hot);Check(Compare(a,b,Rate/5)>1e-3,"gas temperature, reacting volume, pressure and rise alter the compression response without pitch resampling");
            a=Source();b=Source();var labels=Reaction();labels.twoStep=labels.limiter=true;labels.cause=AfterfireCause.AntiLag;
            a.React(Reaction());b.React(labels);Check(Compare(a,b,Rate)==0,"control labels alone do not create or reshape combustion");
            a=Source();a.React(Reaction(0));Check(Energy(a,Rate)==0,"no actual acoustic energy is exact silence");
            a=Source();b=Source(15124312);a.React(Reaction());b.React(Reaction());Check(Compare(a,b,Rate)>1e-3,"different physical seeds vary the pressure/scattering/turbulence texture");
            a=Source();a.React(Reaction());for(int i=0;i<Rate*8;i++)a.Sample();Check(a.Quiet&&a.Sample()==0,"unfed live reservoirs drain to exact finite silence");
            a=Source();a.React(Reaction());long allocated=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<Rate;i++)a.Sample();
            Check(GC.GetAllocatedBytesForCurrentThread()==allocated,"first live excitation and full source playback allocate no callback memory");
        }
        static void Export()
        {
            string file=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/race-audio-audition/26-launch-hard-ignition.race.json"));
            var f=JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(file,Encoding.UTF8));int frames=(f.ticks.Count+241)*400;
            var after=new float[frames*2];
            var events=AfterfireEnvelopePreview.Capture(f,out var arrivals,out var raw,(i,a)=>{after[i*2]=(float)a.finalL;after[i*2+1]=(float)a.finalR;},240);
            Check(events.Count==5387&&events.Select(e=>e.frontId).Distinct().Count()==54,"real Tempest chemistry remains the same 5387 reactions and 54 flames");
            var outside=new TracksidePropagation(Rate);var scene=new TracksidePropagation.Scene(f.spec.induction.ambientTemperatureK,.5,f.spec.induction.ambientPressureBar*100000).At();var block=new float[800];
            for(int i=0;i<after.Length;i+=800){Array.Copy(after,i,block,0,800);outside.Process(block,2,scene);Array.Copy(block,0,after,i,800);}
            string baseline=Path.Combine(Application.dataPath,"../../reports/tempest-envelope-sequence-20261021/before-context.f32");
            if(!File.Exists(baseline)){Log.AppendLine("Audition comparison not exported: the historical local PCM baseline is absent.");return;}
            var bytes=File.ReadAllBytes(baseline);var before=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,before,0,bytes.Length);
            Check(before.Length==after.Length,"old native baseline and current integrated engine have identical complete sequence lengths");
            int first=events.Where(e=>e.acousticJ>=1e-12).Min(e=>arrivals[e.index]);double pre=0;
            for(int i=0;i<first*2;i++)pre=Math.Max(pre,Math.Abs(before[i]-after[i]));
            Check(pre==0,"engine/intake/turbo mix before the first backfire remains bit-identical");
            double gain=Math.Min(1,.9/Math.Max(before.Max(v=>Math.Abs((double)v)),after.Max(v=>Math.Abs((double)v))));
            using(var w=new BinaryWriter(File.Create(Path.Combine(Root,"tempest-game-before-after.wav"))))
            {
                int count=before.Length+after.Length+Rate*2;
                w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*3);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(Rate);w.Write(Rate*6);w.Write((short)6);w.Write((short)24);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(count*3);
                for(int i=0;i<count;i++){double v=(i<before.Length?before[i]:i<before.Length+Rate*2?0:after[i-before.Length-Rate*2])*gain;int q=(int)Math.Round(v*8388607);w.Write((byte)q);w.Write((byte)(q>>8));w.Write((byte)(q>>16));}
            }
            Log.AppendLine("Tempest before 0 s / actual live integration after "+(frames/(double)Rate+1).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" s; common gain "+gain);
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root);Log.Clear();checks=0;
            try
            {
                AfterfireLabVerification.OutputDirectory=Root;AfterfireLabVerification.Run();
                ReactiveChecks();AfterfireLabVerification.VerifyCausality();
                RuntimeAudioIntegrationVerification.OutputDirectory=Root;RuntimeAudioIntegrationVerification.TracksideListening=true;RuntimeAudioIntegrationVerification.Run();
                Export();
            }
            finally{File.WriteAllText(Path.Combine(Root,"reactive-verification.txt"),Log+"Checks: "+checks+"\n",new UTF8Encoding(false));}
        }
        public static void CheckAllRacesAndBuild()
        {
            RuntimeAudioIntegrationVerification.OutputDirectory=Root;RuntimeAudioIntegrationVerification.TracksideListening=true;
            RuntimeAudioIntegrationVerification.CheckRaces();PlayerBuild.BuildPlayerOnly();
        }
    }
}
