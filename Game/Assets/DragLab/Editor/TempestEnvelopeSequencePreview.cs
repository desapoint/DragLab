using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    // Offline full-race listening only. Replace the captured afterfire at the
    // PREMIX boundary, then rerun the unchanged shared bus and outdoor path.
    // Engine, intake, turbo, BOV, wear/cuts and physics remain captured verbatim.
    public static class TempestEnvelopeSequencePreview
    {
        const int Rate=48000,Block=400,TailTicks=240,Gap=48000;
        static readonly string Root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/tempest-envelope-sequence-20261021"));
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        static readonly StringBuilder Log=new StringBuilder();
        static int checks;
        // The runtime settings struct deliberately has no Unity serialization
        // contract. Use an Editor-only DTO rather than changing Domain metadata.
        [Serializable] sealed class Controls
        {
            public double crack,edge,pulse,thump,size,bark,tube,tune,decay,jet,flameTime,scatter,smooth,air,space;
            public Controls(AfterfireLabSettings s)
            {crack=s.crack;edge=s.edge;pulse=s.pulse;thump=s.thump;size=s.size;bark=s.bark;tube=s.tube;tune=s.tune;decay=s.decay;jet=s.jet;flameTime=s.flameTime;scatter=s.scatter;smooth=s.smooth;air=s.air;space=s.space;}
        }
        [Serializable] sealed class Flame
        {
            public int id,startFrame=-1,eventCount;
            public uint seed;
            public double physicalTime,lastTime,acousticJ,gain;
            public string source;
            public Controls controls;
            [NonSerialized] public AfterfireLabSettings settings;
            [NonSerialized] public ExhaustMixtureModel.Event first;
            [NonSerialized] public AfterfireLabSource shape;
            [NonSerialized] public AfterfirePipeResponse pipe;
            [NonSerialized] public bool active;
            [NonSerialized] public double funded,current,target,lastChemical;
            [NonSerialized] public float[] pcm;
            [NonSerialized] public int samples;
        }
        [Serializable] sealed class Receipt
        {
            public int rate=Rate,frames,gapFrames=Gap,eventCount,flameCount;
            public double commonGain,afterStartSeconds,nativeMixError,sourceReconstructionError,totalAcousticJ;
            public string replaySha256;
            public Flame[] flames;
        }
        static string N(double x)=>x.ToString("R",Inv);
        static void Check(bool ok,string text)
        {Log.AppendLine((ok?"PASS ":"FAIL ")+text);checks++;if(!ok)throw new InvalidOperationException(text);}
        static string Hash(byte[] bytes)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
        static void Floats(string name,float[] x)
        {var b=new byte[x.Length*4];Buffer.BlockCopy(x,0,b,0,b.Length);File.WriteAllBytes(Path.Combine(Root,name),b);}
        static double Error(float[] a,float[] b)
        {double d=0;for(int i=0;i<a.Length;i++)d=Math.Max(d,Math.Abs(a[i]-b[i]));return d;}
        static double Peak(float[] a)=>a.Max(v=>Math.Abs((double)v));
        static Flame[] Prepare(List<ExhaustMixtureModel.Event> events,EngineSoundProfile p)
        {
            var hardware=new AfterfireHardwareAcoustics(p);
            var groups=events.Where(e=>e.acousticJ>=1e-12).GroupBy(e=>e.frontId).ToArray();
            Check(groups.All(g=>g.Select(e=>e.zone).Distinct().Count()==1),"all actual Tempest flames use one path each; no cross-zone approximation needed");
            return groups.Select(g=>{
                var first=g.First();var settings=hardware.At(first);return new Flame{id=g.Key,seed=(uint)(first.seed^(first.seed>>32)),eventCount=g.Count(),
                    first=first,physicalTime=first.time,lastTime=g.Last().time,acousticJ=g.Sum(e=>e.acousticJ),
                    gain=Math.Sqrt(g.Sum(e=>e.acousticJ)/.2)*p.popGain*.35,settings=settings,controls=new Controls(settings),source="flame-"+g.Key+".f32"};
            }).ToArray();
        }
        // In variable mode reproduce the actual production gain update, source,
        // path and retirement. Fixed mode changes ONLY the intra-burst gain.
        static float[] Render(List<ExhaustMixtureModel.Event> events,Dictionary<int,int> arrivals,Flame[] flames,
            EngineSoundProfile p,int frames,bool frozen,bool saveSources=false)
        {
            var lookup=flames.ToDictionary(f=>f.id);double clock=0,alpha=1-Math.Exp(-1/(Rate*.005));int next=0;
            foreach(var f in flames)
            {
                f.shape=new AfterfireLabSource(Rate);f.pipe=new AfterfirePipeResponse(p,Rate);
                f.startFrame=-1;f.active=false;f.funded=f.current=f.target=0;f.samples=0;
                if(saveSources){var s=new AfterfireLabSource(Rate);s.Begin(f.settings,f.seed);f.pcm=new float[(int)Math.Ceiling(s.Duration*Rate)+16];}
            }
            var output=new float[frames];
            for(int i=0;i<frames;i++)
            {
                while(next<events.Count&&arrivals[events[next].index]<=i&&events[next].time<=clock)
                {
                    var e=events[next++];if(e.acousticJ<1e-12)continue;var f=lookup[e.frontId];
                    if(!f.active)
                    {
                        if(f.startFrame>=0)throw new InvalidOperationException("Flame restarted after retirement");
                        f.startFrame=i;f.active=true;f.shape.Begin(f.settings,f.seed);f.pipe.Begin(e);
                    }
                    f.lastChemical=e.time;f.funded+=e.acousticJ;f.target=Math.Sqrt(f.funded/.2);
                    if(e.time==f.first.time)f.current=f.target;
                }
                double sum=0;
                foreach(var f in flames)
                {
                    if(!f.active)continue;
                    // Same finite-source/path retirement as the production voice.
                    if(f.shape.Quiet&&clock-f.lastChemical>.2&&clock-f.first.time>f.shape.Duration+.2){f.active=false;continue;}
                    double wave=f.shape.Sample();
                    if(saveSources&&f.samples<f.pcm.Length)f.pcm[f.samples]=(float)(wave*f.gain);
                    f.samples++;f.current+=alpha*(f.target-f.current);
                    double strength=frozen?f.gain:f.current*p.popGain*.35;
                    sum+=f.pipe.Sample(wave*strength,f.shape.PressureSample*strength);
                }
                // VoiceGain is after the pipe in production. Its linear path
                // makes the two placements equivalent to rounding precision.
                output[i]=(float)sum;clock+=1.0/Rate;
            }
            Check(next==events.Count&&flames.All(f=>f.startFrame>=0),"all physical packets/flames delivered in "+(frozen?"fixed":"original")+" replay with native availability");
            Check(flames.All(f=>Math.Abs(f.funded-f.acousticJ)<1e-10),"every chemical continuation contributes exactly once to "+(frozen?"fixed":"original")+" flame energy");
            if(saveSources)foreach(var f in flames)Floats(f.source,f.pcm);
            return output;
        }
        static float[] Bus(double[] premix)
        {
            var pcm=new float[premix.Length];double pl=0,pr=0,yl=0,yr=0;
            for(int i=0;i<pcm.Length;i+=2)
            {double l=premix[i],r=premix[i+1];yl=l-pl+.995*yl;yr=r-pr+.995*yr;pl=l;pr=r;pcm[i]=(float)(yl/(1+Math.Abs(yl)));pcm[i+1]=(float)(yr/(1+Math.Abs(yr)));}
            return pcm;
        }
        static void Outside(float[] pcm,EngineSoundProfile p,RaceAudioTestFixture fixture)
        {
            var scene=new TracksidePropagation.Scene(fixture.spec.induction.ambientTemperatureK,.5,fixture.spec.induction.ambientPressureBar*100000).At();
            var dsp=new TracksidePropagation(Rate);var block=new float[Block*2];
            for(int i=0;i<pcm.Length;i+=block.Length){Array.Copy(pcm,i,block,0,block.Length);dsp.Process(block,2,scene);Array.Copy(block,0,pcm,i,block.Length);}
        }
        static void Wav(float[] before,float[] after,double gain)
        {
            int samples=before.Length+after.Length+Gap*2;
            using(var w=new BinaryWriter(File.Create(Path.Combine(Root,"tempest-full-sequence-before-after.wav"))))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+samples*3);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(Rate);w.Write(Rate*6);w.Write((short)6);w.Write((short)24);
                w.Write(Encoding.ASCII.GetBytes("data"));w.Write(samples*3);
                for(int i=0;i<samples;i++)
                {
                    double v=(i<before.Length?before[i]:i<before.Length+Gap*2?0:after[i-before.Length-Gap*2])*gain;
                    if(double.IsNaN(v)||Math.Abs(v)>.901)throw new InvalidOperationException("Invalid WAV peak");
                    int q=(int)Math.Round(v*8388607);w.Write((byte)q);w.Write((byte)(q>>8));w.Write((byte)(q>>16));
                }
            }
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root);Log.Clear();checks=0;
            try
            {
                string file=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/race-audio-audition/26-launch-hard-ignition.race.json"));
                var bytes=File.ReadAllBytes(file);var fixture=JsonUtility.FromJson<RaceAudioTestFixture>(Encoding.UTF8.GetString(bytes));
                int frames=(fixture.ticks.Count+1+TailTicks)*Block;
                var premix=new double[frames*2];var nativeAfter=new float[frames];var nativeMix=new float[frames*2];
                var all=AfterfireEnvelopePreview.Capture(fixture,out var arrivals,out var nativeRaw,(i,a)=>{
                    premix[i*2]=a.beforeDcL;premix[i*2+1]=a.beforeDcR;nativeAfter[i]=(float)(a.afterOutlet+a.afterWall);
                    nativeMix[i*2]=(float)a.finalL;nativeMix[i*2+1]=(float)a.finalR;
                },TailTicks);
                Check(all.Count==5387&&all.Select(e=>e.frontId).Distinct().Count()==54,"original complete Tempest trace retains 5387 reactions and 54 flame bursts");
                var profile=new EngineSoundProfile(fixture.spec,fixture.tune);var flames=Prepare(all,profile);
                var original=Render(all,arrivals,flames,profile,frames,false);
                double sourceError=Error(original,nativeAfter);
                Check(sourceError<2e-6,"independent original source/path replay reproduces actual native afterfire over the ENTIRE race");
                var starts=flames.Select(f=>f.startFrame).ToArray();
                var revised=Render(all,arrivals,flames,profile,frames,true,true);
                Check(starts.SequenceEqual(flames.Select(f=>f.startFrame)),"every revised burst retains the original native sample of ignition");
                var repeated=Render(all,arrivals,flames,profile,frames,true);
                Check(revised.SequenceEqual(repeated),"all 54 fixed bursts reproduce identical seeded PCM over the full replay");
                var oldBus=Bus(premix);double nativeError=Error(oldBus,nativeMix);
                Check(nativeError<2e-6,"unchanged DC and shared soft limiter reconstruct the actual native full-engine output");
                var revisedPremix=(double[])premix.Clone();double bedError=0;
                for(int i=0;i<frames;i++)for(int ch=0;ch<2;ch++)
                {int j=i*2+ch;revisedPremix[j]+=(double)revised[i]-nativeAfter[i];bedError=Math.Max(bedError,Math.Abs((revisedPremix[j]-revised[i])-(premix[j]-nativeAfter[i])));}
                Check(bedError<2e-7,"both halves use exactly the same captured engine/intake/turbo/BOV bed before the shared bus");
                Check(revised.Take(flames.Min(f=>f.startFrame)).All(v=>v==0),"no revised afterfire occurs before the actual first ignition");
                var newBus=Bus(revisedPremix);Outside(oldBus,profile,fixture);Outside(newBus,profile,fixture);
                Check(Peak(oldBus)>1e-4&&Peak(newBus)>1e-4,"both complete engine sequences contain audible output");
                double gain=Math.Min(1,.9/Math.Max(Peak(oldBus),Peak(newBus)));
                Wav(oldBus,newBus,gain);Floats("before-context.f32",oldBus);Floats("after-context.f32",newBus);
                var receipt=new Receipt{frames=frames,eventCount=all.Count,flameCount=flames.Length,commonGain=gain,afterStartSeconds=frames/(double)Rate+1,
                    nativeMixError=nativeError,sourceReconstructionError=sourceError,totalAcousticJ=all.Sum(e=>e.acousticJ),replaySha256=Hash(bytes),flames=flames};
                File.WriteAllText(Path.Combine(Root,"receipt.json"),JsonUtility.ToJson(receipt,true)+"\n",new UTF8Encoding(false));
                var csv=new StringBuilder("flame_id,start_frame,physical_seconds,seed,reactions,acoustic_j,fixed_gain\n");
                foreach(var f in flames)csv.AppendLine(f.id+","+f.startFrame+","+N(f.physicalTime)+","+f.seed+","+f.eventCount+","+N(f.acousticJ)+","+N(f.gain));
                File.WriteAllText(Path.Combine(Root,"flames-matrix.csv"),csv.ToString(),new UTF8Encoding(false));
                Log.AppendLine("Replay SHA256 "+receipt.replaySha256+"; events "+all.Count+"; flames "+flames.Length+"; physical acoustic J "+N(receipt.totalAcousticJ));
                Log.AppendLine("Original source/path maximum error "+N(sourceError)+"; actual native engine reconstruction error "+N(nativeError)+"; bed error "+N(bedError));
                Log.AppendLine("Before starts 0 s; after starts "+N(receipt.afterStartSeconds)+" s; each sequence "+N(frames/(double)Rate)+" s; common gain "+N(gain));
                Log.AppendLine("Both halves: same fitted hardware, path travel/loss/shell, shared DC/limiter, 12 m trackside air/stereo/reflections. No extra backfire EQ or level boost.");
                Log.AppendLine("OFFLINE ONLY: whole recorded flame energy fixes each strength; no runtime code, settings or player build changed.");
                // Shared capture now supports context callbacks and a held tail;
                // rerun its original defaults to preserve the short A/B checkpoint.
                AfterfireEnvelopePreview.Run();
                Debug.Log("TEMPEST CONTEXT PREVIEW PASS "+checks);
            }
            finally{File.WriteAllText(Path.Combine(Root,"verification.txt"),Log+"Checks: "+checks+"\n",new UTF8Encoding(false));}
        }
    }
}
