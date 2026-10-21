using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    // Listening prototype ONLY. No runtime switch, source modification or build.
    // A recorded flame's complete chemical energy is available offline. Freeze
    // its acoustic strength before rendering, instead of growing an old envelope.
    // This is not a causal live implementation: future integration must obtain
    // an onset energy estimate / bounded collection latency from the simulation.
    public static class AfterfireEnvelopePreview
    {
        const int Rate=48000,Lead=36000,Gap=48000;
        static readonly string Root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/afterfire-envelope-preview-20261021"));
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        static readonly MethodInfo Record=typeof(ExhaustMixtureModel).GetMethod("Record",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly StringBuilder Log=new StringBuilder();
        static int checks;
        [Serializable] sealed class Receipt
        {
            public int rate=Rate,preset,frontId,eventCount,firstSample,leadFrames=Lead,segmentFrames,gapFrames=Gap;
            public uint seed;
            public double firstIgnitionSeconds,lastReactionSeconds,acousticJ,constantGain,commonGain,beforeMatchGain;
            public double beforePeak,afterPeak,beforeRms,afterRms,attackToTailBefore,attackToTailAfter;
            public string replaySha256;
        }
        static string N(double x)=>x.ToString("R",Inv);
        static void Check(bool yes,string label)
        {Log.AppendLine((yes?"PASS ":"FAIL ")+label);checks++;if(!yes)throw new InvalidOperationException(label);}
        static double Peak(float[] x)=>x.Max(v=>Math.Abs((double)v));
        static double Rms(float[] x)=>Math.Sqrt(x.Sum(v=>(double)v*v)/x.Length);
        static double Energy(float[] x,int from,int count)
        {double sum=0;for(int i=Math.Max(0,from);i<Math.Min(x.Length,from+count);i++)sum+=(double)x[i]*x[i];return sum;}
        static void Floats(string name,float[] x)
        {var bytes=new byte[x.Length*4];Buffer.BlockCopy(x,0,bytes,0,bytes.Length);File.WriteAllBytes(Path.Combine(Root,name),bytes);}
        static string Hash(byte[] bytes)
        {using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","");}
        static List<ExhaustMixtureModel.Event> Capture(RaceAudioTestFixture f,out Dictionary<int,int> availability,out float[] nativeRaw)
        {
            var race=f.CreateRace();var events=new List<ExhaustMixtureModel.Event>();int cursor=0;
            var arrivals=new Dictionary<int,int>();var captured=new float[(f.ticks.Count+1)*400];int offset=0;
            availability=arrivals;nativeRaw=captured;
            try
            {
                // Match the real audition's initial idle frame: binding the
                // controller creates the per-car physical exhaust transport.
                var controller=new EngineSoundController(race.player.spec,race.player.tune);
                var synth=new EngineSoundSynth(new EngineSoundProfile(race.player.spec,race.player.tune),sampleRate:Rate);
                synth.Audit=(frame,x)=>captured[offset+frame]=(float)x.afterRaw;
                var block=new float[800];
                synth.Fill(block,2,Rate,controller.Update(race.player.state,true,1,DragMath.FixedStep));
                foreach(var tick in f.ticks)
                {
                    offset+=400;
                    race.player.state.warmupLineLock=tick.lineLock;
                    if(tick.beginCountdown&&!race.BeginCountdown())throw new InvalidOperationException("Countdown failed");
                    if(race.phase==RacePhase.Finished)race.Shutdown(tick.input);else race.Step(tick.input);
                    var frame=controller.Update(race.player.state,true,1,DragMath.FixedStep);
                    var model=race.player.state.audioExhaust.mixture;
                    while(cursor<model.Sequence)
                    {CheckRead(model,++cursor,out var e);events.Add(e);arrivals.Add(e.index,offset);}
                    synth.Fill(block,2,Rate,frame);
                }
                Check(Math.Abs(race.player.state.elapsed-f.elapsed)<1e-9&&race.player.state.exhaustPopCount==f.pops,"original Tempest physics elapsed and pop trace preserved");
                Check(Math.Abs(events.Sum(e=>e.acousticJ)-race.player.state.audioExhaust.mixture.AcousticEnergyJ)<1e-8,"all recorded physical acoustic energy retained without history loss");
                return events;
            }
            finally{race.CloseOpponentWorker();}
        }
        static void CheckRead(ExhaustMixtureModel model,int cursor,out ExhaustMixtureModel.Event e)
        {if(!model.Read(cursor,out e))throw new InvalidOperationException("Lost chemical event "+cursor);}
        // Uses the unchanged production history reader and source. Raw is taken
        // before hardware colour/path/mix/outdoor, exactly as the previous export.
        static float[] Before(EngineSoundProfile p,ExhaustMixtureModel.Event[] events,Dictionary<int,int> availability,int frames,out int firstSample)
        {
            var history=new ExhaustMixtureModel(123,"envelope-preview");
            var synth=new AfterfireAudioSynth(p,Rate){AuditEnabled=true};synth.Connect(history);
            var pcm=new float[frames];firstSample=-1;int next=0;
            for(int i=0;i<frames;i++)
            {
                // Preserve actual tick-to-audio availability as well as chemical
                // timestamps. Prequeuing future packets would change the BEFORE.
                while(next<events.Length&&availability[events[next].index]<=i)Record.Invoke(history,new object[]{events[next++]});
                synth.Sample();pcm[i]=(float)synth.AuditRaw;
                if(firstSample<0&&synth.StartedBursts>0)firstSample=i;
            }
            Check(synth.Delivered==events.Length&&synth.StartedBursts==1&&synth.TailSteals==0&&synth.HistoryOverflows==0,"unchanged production before renders exactly one actual flame with every continuation");
            Check(Math.Abs(synth.FundedAcousticEnergy-events.Sum(e=>e.acousticJ))<1e-10,"before funds every physical reaction exactly once");
            return pcm;
        }
        static float[] Frozen(EngineSoundProfile p,ExhaustMixtureModel.Event[] events,int frames,int start,double energyScale=1)
        {
            var shape=new AfterfireLabSource(Rate);
            shape.Begin(AfterfireLabSettings.Hardware(p),(uint)(events[0].seed^(events[0].seed>>32)));
            double gain=Math.Sqrt(events.Sum(e=>e.acousticJ)*energyScale/.2)*p.popGain*.35;
            var pcm=new float[frames];for(int i=start;i<frames;i++)pcm[i]=(float)(shape.Sample()*gain);
            return pcm;
        }
        static float[] Crop(float[] x,int from,int count)
        {var y=new float[count];Array.Copy(x,from,y,0,count);return y;}
        static void Wav(string name,float[] before,float[] after,double beforeGain,double common)
        {
            int frames=before.Length+Gap+after.Length;
            using(var w=new BinaryWriter(File.Create(Path.Combine(Root,name))))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+frames*6);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(Rate);w.Write(Rate*6);w.Write((short)6);w.Write((short)24);
                w.Write(Encoding.ASCII.GetBytes("data"));w.Write(frames*6);
                for(int i=0;i<frames;i++)
                {
                    double value=i<before.Length?before[i]*beforeGain*common:i<before.Length+Gap?0:after[i-before.Length-Gap]*common;
                    if(double.IsNaN(value)||Math.Abs(value)>.951)throw new InvalidOperationException("Invalid WAV peak");
                    int q=(int)Math.Round(value*8388607);
                    for(int channel=0;channel<2;channel++){w.Write((byte)q);w.Write((byte)(q>>8));w.Write((byte)(q>>16));}
                }
            }
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root);Log.Clear();checks=0;
            try
            {
                string replay=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/race-audio-audition/26-launch-hard-ignition.race.json"));
                var bytes=File.ReadAllBytes(replay);var f=JsonUtility.FromJson<RaceAudioTestFixture>(Encoding.UTF8.GetString(bytes));
                var all=Capture(f,out var availability,out var nativeRaw);var profile=new EngineSoundProfile(f.spec,f.tune);
                // First actual flame, not an arbitrarily cropped segment or a
                // hand-authored event made louder to resemble a desired sound.
                var first=all.First(e=>e.acousticJ>=1e-12);
                var events=all.Where(e=>e.frontId==first.frontId&&e.acousticJ>=1e-12).ToArray();
                Check(events.Select(e=>e.zone).Distinct().Count()==1,"chosen real Tempest flame has one zone: freeze one physical strength without duplicating paths");
                Check(events.Length<ExhaustMixtureModel.HistoryCapacity,"entire selected flame fits the native event reader");
                int frames=(int)Math.Ceiling((events.Last().time+2)*Rate);
                var before=Before(profile,events,availability,frames,out int start);var after=Frozen(profile,events,frames,start);
                int nextFlame=all.Where(e=>e.frontId!=first.frontId&&e.acousticJ>=1e-12)
                    .Select(e=>Math.Max(availability[e.index],(int)Math.Ceiling(e.time*Rate))).Min();
                double nativeError=0;for(int i=0;i<nextFlame;i++)nativeError=Math.Max(nativeError,Math.Abs(before[i]-nativeRaw[i]));
                Check(nativeError<2e-6,"before reproduces the real native replay up to the next independent flame, including event availability");
                Check(start>=Lead,"preview starts 750 ms before the first chemical ignition");
                Check(Peak(before)>1e-6&&Peak(after)>1e-6,"both original and fixed envelopes are audible and nonzero");
                var repeat=Frozen(profile,events,frames,start);var quarter=Frozen(profile,events,frames,start,.25);var zero=Frozen(profile,events,frames,start,0);
                Check(after.SequenceEqual(repeat),"same physical history and seed reproduce every preview PCM sample");
                Check(after.Select((v,i)=>Math.Abs(quarter[i]-.5*v)).Max()<1e-7,"quarter physical energy halves pressure with identical burst shape");
                Check(zero.All(x=>x==0),"zero physical energy produces exact silence");
                int length=Lead+(int)Math.Ceiling(Math.Max(2,events.Last().time-first.time+1)*Rate);
                var old=Crop(before,start-Lead,length);var revised=Crop(after,start-Lead,length);
                Check(old.Take(Lead).All(x=>x==0)&&revised.Take(Lead).All(x=>x==0),"both isolated chapters retain an exact silent lead-in before the whole attack");
                Check(old.Skip(length-Rate/4).All(x=>x==0)&&revised.Skip(length-Rate/4).All(x=>x==0),"neither chapter truncates its finite decay");
                double match=Rms(revised)/Rms(old),common=.9/Math.Max(Peak(old)*match,Peak(revised));
                double natural=.9/Math.Max(Peak(old),Peak(revised));
                Wav("01-tempest-envelope-before-after-matched.wav",old,revised,match,common);
                Wav("02-tempest-envelope-before-after-natural.wav",old,revised,1,natural);
                Floats("before.f32",old);Floats("after.f32",revised);
                double beforeAttack=Energy(old,Lead,Rate/100)/Math.Max(1e-30,Energy(old,Lead+Rate/100,Rate/2));
                double afterAttack=Energy(revised,Lead,Rate/100)/Math.Max(1e-30,Energy(revised,Lead+Rate/100,Rate/2));
                var receipt=new Receipt{preset=AfterfireLabSettings.HardwarePreset(profile),frontId=first.frontId,eventCount=events.Length,
                    seed=(uint)(first.seed^(first.seed>>32)),firstSample=start,segmentFrames=length,
                    firstIgnitionSeconds=first.time,lastReactionSeconds=events.Last().time,acousticJ=events.Sum(e=>e.acousticJ),
                    constantGain=Math.Sqrt(events.Sum(e=>e.acousticJ)/.2)*profile.popGain*.35,commonGain=common,beforeMatchGain=match,
                    beforePeak=Peak(old),afterPeak=Peak(revised),beforeRms=Rms(old),afterRms=Rms(revised),
                    attackToTailBefore=beforeAttack,attackToTailAfter=afterAttack,replaySha256=Hash(bytes)};
                File.WriteAllText(Path.Combine(Root,"receipt.json"),JsonUtility.ToJson(receipt,true)+"\n",new UTF8Encoding(false));
                var csv=new StringBuilder("time_seconds,available_audio_frame,front_id,zone,seed,acoustic_j,gas_k,burn_seconds\n");
                foreach(var e in events)csv.AppendLine(N(e.time)+","+availability[e.index]+","+e.frontId+","+e.zone+","+e.seed+","+N(e.acousticJ)+","+N(e.gasK)+","+N(e.burnSeconds));
                File.WriteAllText(Path.Combine(Root,"selected-reactions-matrix.csv"),csv.ToString(),new UTF8Encoding(false));
                Log.AppendLine("Flame "+first.frontId+"; reactions "+events.Length+"; acoustic J "+N(receipt.acousticJ));
                Log.AppendLine("Physical ignition "+N(first.time)+" s; same native start sample "+start+"; preset "+receipt.preset+"; seed "+receipt.seed);
                Log.AppendLine("Before vs actual native replay maximum PCM difference before next independent flame: "+N(nativeError));
                Log.AppendLine("Attack (first 10 ms) / next 500 ms energy: before "+N(beforeAttack)+"; after "+N(afterAttack));
                Log.AppendLine("Matched WAV: before 0 s; after "+N(length/(double)Rate+1)+" s; 750 ms lead in each; whole-chapter before match gain "+N(match)+"; common peak guard "+N(common));
                Log.AppendLine("Natural WAV: same chapters; shared peak guard "+N(natural)+"; no relative gain adjustment");
                Log.AppendLine("OFFLINE ONLY: full recorded flame energy fixes its strength before playback; no causal live-runtime change or build is made.");
                Debug.Log("AFTERFIRE ENVELOPE PREVIEW PASS "+checks);
            }
            finally{File.WriteAllText(Path.Combine(Root,"verification.txt"),Log+"Checks: "+checks+"\n",new UTF8Encoding(false));}
        }
    }
}
