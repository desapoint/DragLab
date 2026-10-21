using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    public static class CrowdAudioVerification
    {
        static readonly string Root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/trackside-audio-20261021"));
        static readonly StringBuilder report=new StringBuilder();static int checks;
        static void Check(bool yes,string label)
        {
            report.AppendLine((yes?"PASS ":"FAIL ")+label);checks++;Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root,"outdoor-verification.txt"),report+"Checks: "+checks+"\n",new UTF8Encoding(false));
            if(!yes)throw new InvalidOperationException(label);
        }
        static double Energy(float[] pcm,int start=0,int end=-1){double sum=0;for(int i=start;i<(end<0?pcm.Length:end);i++)sum+=pcm[i]*pcm[i];return sum;}
        static float[] Tone(int rate,int channels,double hz)
        {var pcm=new float[rate*channels];for(int i=0;i<rate;i++)for(int ch=0;ch<channels;ch++)pcm[i*channels+ch]=(float)(.15*Math.Sin(2*Math.PI*hz*i/rate));return pcm;}
        static double BurstPeak(ReactivePressureBurst core,int frames)
        {double peak=0;for(int i=0;i<frames;i++)peak=Math.Max(peak,Math.Abs(core.Sample()));return peak;}
        public static void Run()
        {
            report.Clear();checks=0;
            double absorption=TracksidePropagation.AirAbsorptionDb(1000,293.15,.5,101325);
            Check(absorption>.0044&&absorption<.0049,"ISO atmospheric absorption at 1 kHz / 20 C / 50% RH / sea level");
            Check(TracksidePropagation.AirAbsorptionDb(10000,293.15,.5,101325)>absorption*10,"air absorbs high frequencies more strongly");
            var cold=new TracksidePropagation.Scene(273.15);var hot=new TracksidePropagation.Scene(313.15);
            Check(cold.At().direct.leftSeconds>hot.At().direct.leftSeconds,"cold atmosphere increases propagation time");
            foreach(int rate in new[]{44100,48000})
            {
                var dry=new TracksidePropagation.Scene(reflectionScale:0);var scene=new TracksidePropagation.Scene();var frame=scene.At();
                var impulse=new float[rate*2];impulse[0]=impulse[1]=1;
                var direct=(float[])impulse.Clone();var reflected=(float[])impulse.Clone();
                new TracksidePropagation(rate).Process(direct,2,dry.At());new TracksidePropagation(rate).Process(reflected,2,frame);
                int before=(int)(Math.Min(frame.direct.leftSeconds,frame.direct.rightSeconds)*rate)-1;
                Check(direct.Take(before*2).All(x=>x==0),"no signal arrives before its sound-travel time / "+rate);
                int late=(int)(frame.barrier.leftSeconds*rate)*2;
                Check(Energy(reflected,late-8,late+100)>Energy(direct,late-8,late+100)*1000+1e-8,"trackside reflection arrives on its own longer path / "+rate);
                var near=Tone(rate,1,100);var far=(float[])near.Clone();
                new TracksidePropagation(rate).Process(near,1,dry.At(lateral:6,sourceHeight:1,listenerHeight:1));
                new TracksidePropagation(rate).Process(far,1,dry.At(lateral:12,sourceHeight:1,listenerHeight:1));
                double ratio=Math.Sqrt(Energy(far,rate/3,rate*2/3)/Energy(near,rate/3,rate*2/3));
                Check(ratio>.48&&ratio<.52,"doubling distance halves pressure, not pressure squared / "+rate);
                var original=Tone(rate,2,700);var whole=(float[])original.Clone();var split=(float[])original.Clone();
                new TracksidePropagation(rate).Process(whole,2,frame);var chunks=new TracksidePropagation(rate);
                for(int at=0;at<split.Length;at+=800){int n=Math.Min(800,split.Length-at);var part=new float[n];Array.Copy(original,at,part,0,n);chunks.Process(part,2,frame);Array.Copy(part,0,split,at,n);}
                Check(whole.SequenceEqual(split),"exact propagation state across callback block boundaries / "+rate);
                chunks.Clear();var zero=new float[800];chunks.Process(zero,2,frame);
                Check(zero.All(x=>x==0),"pause/session clear cannot replay an old echo / "+rate);
                var block=new float[800];for(int i=0;i<block.Length;i++)block[i]=.01f;
                chunks.Process(block,2,frame);long allocated=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<120;i++)chunks.Process(block,2,frame);
                Check(GC.GetAllocatedBytesForCurrentThread()==allocated,"zero warmed propagation callback allocations / "+rate);
                var times=new double[120];var watch=new Stopwatch();
                for(int i=0;i<times.Length;i++){watch.Restart();chunks.Process(block,2,frame);watch.Stop();times[i]=watch.Elapsed.TotalMilliseconds;}
                Array.Sort(times);report.AppendLine("MEASURE six-path propagation p95 "+times[113].ToString("F4")+" ms / 400 frames / "+rate);
                Check(times[113]<400.0/rate*1000*.35,"outdoor path fits callback budget / "+rate);
                var primary=new ExhaustMixtureModel.Event{acousticJ=.2,pressurePa=60000,riseSeconds=.0007,burnSeconds=.012,flowMps=40,seed=12345};
                var first=new ReactivePressureBurst(rate);var repeat=new ReactivePressureBurst(rate);first.Begin(primary);repeat.Begin(primary);
                bool identical=true;double peak=0;
                for(int i=0;i<rate/5;i++){double value=first.Sample();identical&=value==repeat.Sample();peak=Math.Max(peak,Math.Abs(value));}
                Check(identical&&peak>0,"live pressure source is seeded, finite and reproducible / "+rate);
                var smaller=primary;smaller.acousticJ=.02;smaller.pressurePa=12000;first.Excite(smaller);
                double secondary=BurstPeak(first,rate/5);smaller.acousticJ=.004;smaller.pressurePa=3000;first.Excite(smaller);
                double third=BurstPeak(first,rate/5);
                Check(secondary<peak&&third<secondary,"finite declining pocket gives a larger bang then smaller funded burns / "+rate);
                Check(Math.Abs(first.FundedEnergy-.224)<1e-12,"secondary pressure sounds cannot invent additional reaction energy / "+rate);
                for(int i=0;i<rate;i++)first.Sample();
                var renewed=primary;renewed.acousticJ=.8;renewed.pressurePa=120000;first.Excite(renewed);
                Check(BurstPeak(first,rate/5)>secondary,"fresh fuel/pressure can produce another larger bang instead of a forced decay train / "+rate);
                var a=new ReactivePressureBurst(rate);var b=new ReactivePressureBurst(rate);a.Begin(primary);primary.seed++;b.Begin(primary);
                bool differs=false;for(int i=0;i<rate/10;i++)differs|=a.Sample()!=b.Sample();
                Check(differs,"new seeds change live texture rather than selecting a stored clip / "+rate);
                var longer=new ReactivePressureBurst(rate,2);var sharper=new ReactivePressureBurst(rate,1,2);var standard=new ReactivePressureBurst(rate);
                longer.Begin(primary);sharper.Begin(primary);standard.Begin(primary);bool decayDiffers=false,sharpDiffers=false;
                for(int i=0;i<rate/5;i++){double value=standard.Sample();decayDiffers|=longer.Sample()!=value;sharpDiffers|=sharper.Sample()!=value;}
                Check(decayDiffers&&sharpDiffers,"authored decay and sharpness still affect live excitation / "+rate);
                long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<rate;i++)a.Sample();
                Check(GC.GetAllocatedBytesForCurrentThread()==start,"zero pressure-source sample allocations / "+rate);
            }
            var moving=Tone(48000,1,1000);var stationary=(float[])moving.Clone();var environment=new TracksidePropagation.Scene(reflectionScale:0);
            new TracksidePropagation(48000).Process(stationary,1,environment.At(20));var renderer=new TracksidePropagation(48000);
            for(int at=0;at<moving.Length;at+=800){var part=new float[800];Array.Copy(moving,at,part,0,800);renderer.Process(part,1,environment.At(20+at/48000.0*50));Array.Copy(part,0,moving,at,800);}
            Func<float[],int> crossings=data=>Enumerable.Range(24001,23998).Count(i=>data[i-1]<=0&&data[i]>0);
            Check(crossings(moving)<crossings(stationary)&&moving.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x)),"receding source gets Doppler from changing travel time, with finite output");
            var pocket=new ExhaustMixtureModel(1718,"isolated-pocket",initialK:1000);
            pocket.zones[0].fuelKg=5e-5;pocket.zones[0].oxygenKg=5e-5*pocket.stoichOxygenPerFuel;
            var hold=new ExhaustMixtureModel.Input{dt=.0005,intakeK=350,wallTargetK=1000};
            bool depletes=true;double retained=pocket.RetainedFuel;
            for(int i=0;i<2000;i++){pocket.Step(hold);depletes&=pocket.RetainedFuel<=retained+1e-15;retained=pocket.RetainedFuel;}
            int ended=pocket.Sequence;for(int i=0;i<1000;i++)pocket.Step(hold);
            Check(depletes&&ended>1&&pocket.Sequence==ended&&pocket.FuelIn==0&&pocket.FuelBurned<=5e-5,"physical finite pocket depletes and stops its burn sequence without new fuel or scripted pops");
            UnityEngine.Debug.Log("CROWD AUDIO PASS "+checks);
        }
        public static void Checkpoint()
        {
            Run();ConditionDrivenAfterfireVerification.Run();
            File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/condition-afterfire-20261021/causality.txt")),Path.Combine(Root,"causality.txt"),true);
            RuntimeAudioIntegrationVerification.OutputDirectory=Root;RuntimeAudioIntegrationVerification.TracksideListening=true;
            RuntimeAudioIntegrationVerification.Run();RuntimeAudioIntegrationVerification.CheckRaces();
            TireSoundVerification.Run();TireFeedbackVerification.VerifyEffectsAt(Root);
        }
        public static void Build(){PlayerBuild.BuildPlayerOnly();}
        public static void FinalAndBuild(){Checkpoint();Build();}
    }
}
