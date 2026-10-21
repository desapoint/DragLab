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
    // Offline diagnostic only. Capture the production generators once, then reuse
    // the same PCM for every processing comparison. Never retune a player save.
    public static class TempestProcessingAudition
    {
        const int Rate=48000,Block=400,Gap=48000;
        static readonly string Root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/tempest-processing-20261021"));
        static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
        static readonly Dictionary<string,float[]> Audio=new Dictionary<string,float[]>();
        static readonly StringBuilder Log=new StringBuilder();
        static int frames,offset,checks,raceFrames;
        static double maxPeak;
        static EngineSoundProfile profile;
        static TracksidePropagation.Frame listener,tireListener;
        static readonly string[] Keys={"engine","engine-fitted","controls","controls-fitted","mechanical","clatter","intake-raw","intake","turbo-whistle","turbo-airflow","turbo-turbine","bov","after-raw","after-shaped","after-attenuated","after-outlet","after-wall","after-raw-routed","after-raw-delayed","after-unattenuated","tire-fl","tire-fr","tire-rl","tire-rr","engine-live","tires-live","premix-live","dc-live","core-raw","core-header","core-collector","core-band","core-turbine","core-body"};
        sealed class Track
        {
            public string name;public string[] keys;
            public Track(string name,params string[] keys){this.name=name;this.keys=keys;}
        }
        sealed class Pass
        {
            public string title;public int source,outdoor;public bool dc,limit,noColour,noLoss,noTravel,noShell,noSpatial,noAir,noReflections;
            public Pass(string title,int source=3,bool dc=true,bool limit=true,int outdoor=3)
            {this.title=title;this.source=source;this.dc=dc;this.limit=limit;this.outdoor=outdoor;}
        }
        static readonly Pass[] Passes={
            new Pass("00 Raw generators",0,false,false,0),
            new Pass("01 Add hardware voicing and fitted exhaust",1,false,false,0),
            new Pass("02 Add source power attenuation",2,false,false,0),
            new Pass("03 Add source travel and shell radiation",3,false,false,0),
            new Pass("04 Add final engine DC filter",3,true,false,0),
            new Pass("05 Add engine and tire soft limiters",3,true,true,0),
            new Pass("06 Add direct distance delay and stereo",3,true,true,1),
            new Pass("07 Add atmospheric absorption",3,true,true,2),
            new Pass("08 FULL production processing"),
            new Pass("09 Full WITHOUT hardware coloration"){noColour=true},
            new Pass("10 Full WITHOUT source power loss"){noLoss=true},
            new Pass("11 Full WITHOUT source travel"){noTravel=true},
            new Pass("12 Full WITHOUT shell radiation"){noShell=true},
            new Pass("13 Full WITHOUT final engine DC filter",3,false),
            new Pass("14 Full WITHOUT soft limiters",3,true,false),
            new Pass("15 Full WITHOUT outdoor delays and stereo"){noSpatial=true},
            new Pass("16 Full WITHOUT atmospheric absorption"){noAir=true},
            new Pass("17 Full WITHOUT reflected paths"){noReflections=true},
            new Pass("18 Raw plus final DC only",0,true,false,0),
            new Pass("19 Raw plus soft limiters only",0,false,true,0),
            new Pass("20 Raw plus outdoor only",0,false,false,3),
            new Pass("21 Full WITHOUT DC and soft limiters",3,false,false,3)
        };
        static readonly string[] Base={"engine","controls","mechanical","clatter","intake"};
        static readonly string[] Turbo={"turbo-whistle","turbo-airflow","turbo-turbine"};
        static readonly string[] Tires={"tire-fl","tire-fr","tire-rl","tire-rr"};
        static readonly List<Track> Tracks=new List<Track>();
        static void Check(bool yes,string text)
        {Log.AppendLine((yes?"PASS ":"FAIL ")+text);checks++;if(!yes){Save();throw new InvalidOperationException(text);}}
        static void Save()=>File.WriteAllText(Path.Combine(Root,"verification.txt"),Log+"Checks: "+checks+"\n",new UTF8Encoding(false));
        static string N(double x)=>x.ToString("R",Invariant);
        static double Soft(double x)=>x/(1+Math.Abs(x));
        static void Put(string key,int frame,double l,double r)
        {var a=Audio[key];a[frame*2]=(float)l;a[frame*2+1]=(float)r;}
        static void Mono(string key,int frame,double x)=>Put(key,frame,x,x);
        static void Capture(int frame,EngineSoundAuditSample x)
        {
            int i=offset+frame;Put("engine",i,x.combustionL,x.combustionR);Put("engine-fitted",i,x.fittedL,x.fittedR);
            Put("controls",i,x.controlsL,x.controlsR);Put("controls-fitted",i,x.fittedControlsL,x.fittedControlsR);
            Mono("mechanical",i,x.mechanical);Mono("clatter",i,x.clatter);Mono("intake-raw",i,x.intakeRaw);Mono("intake",i,x.intake);
            Mono("turbo-whistle",i,x.turboWhistle);Mono("turbo-airflow",i,x.turboFlow);Mono("turbo-turbine",i,x.turboTurbine);Mono("bov",i,x.release);
            Mono("after-raw",i,x.afterRaw);Mono("after-shaped",i,x.afterShaped);Mono("after-attenuated",i,x.afterAttenuated);
            Mono("after-outlet",i,x.afterOutlet);Mono("after-wall",i,x.afterWall);Mono("after-raw-routed",i,x.afterRawRouted);
            Mono("after-raw-delayed",i,x.afterRawDelayed);Mono("after-unattenuated",i,x.afterUnattenuated);
            Put("premix-live",i,x.beforeDcL,x.beforeDcR);Put("dc-live",i,x.afterDcL,x.afterDcR);
            Put("core-raw",i,x.core.rawL*x.pressureGain,x.core.rawR*x.pressureGain);
            Put("core-header",i,x.core.headerL*x.pressureGain,x.core.headerR*x.pressureGain);
            Put("core-collector",i,x.core.collectorL*x.pressureGain,x.core.collectorR*x.pressureGain);
            Put("core-band",i,x.core.bandL*x.pressureGain,x.core.bandR*x.pressureGain);
            Put("core-turbine",i,x.core.turbineL*x.pressureGain,x.core.turbineR*x.pressureGain);
            Put("core-body",i,x.core.bodyL*x.pressureGain,x.core.bodyR*x.pressureGain);
        }
        static void CaptureTires(int frame,double[] left,double[] right,double gain)
        {for(int w=0;w<4;w++)Put(Tires[w],offset+frame,left[w]*gain,right[w]*gain);}
        static void CaptureRace()
        {
            string file=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/race-audio-audition/26-launch-hard-ignition.race.json"));
            var f=JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(file,Encoding.UTF8));
            var race=f.CreateRace();raceFrames=(f.ticks.Count+1+240)*Block;frames=raceFrames+14*Rate;
            foreach(string key in Keys)Audio.Add(key,new float[frames*2]);
            try
            {
                var car=race.player;profile=new EngineSoundProfile(car.spec,car.tune);
                listener=new TracksidePropagation.Scene(car.spec.induction.ambientTemperatureK,.5,car.spec.induction.ambientPressureBar*100000).At();tireListener=listener.TireHeight();
                var controller=new EngineSoundController(car.spec,car.tune);var synth=new EngineSoundSynth(profile,sampleRate:Rate){Audit=Capture};
                var plain=new EngineSoundSynth(profile,sampleRate:Rate);var tires=new TireSoundSynth{Audit=CaptureTires};var plainTires=new TireSoundSynth();
                var tireProfile=new TireSoundProfile(car.spec,car.tune);var a=new float[Block*2];var b=new float[Block*2];var ta=new float[Block*2];var tb=new float[Block*2];
                bool exact=true,tireExact=true;var timeline=new StringBuilder("seconds,stage\n");string previous=null;int tickIndex=-1;
                EngineSoundFrame last=null;TireSoundFrame tireLast=null;
                for(int tick=-1;tick<f.ticks.Count+240;tick++)
                {
                    if(tick>=0&&tick<f.ticks.Count)
                    {
                        var input=f.ticks[tick];car.state.warmupLineLock=input.lineLock;
                        if(input.beginCountdown&&!race.BeginCountdown())throw new InvalidOperationException("Countdown failed");
                        if(race.phase==RacePhase.Finished)race.Shutdown(input.input);else race.Step(input.input);
                        if(!string.IsNullOrEmpty(input.stage)&&input.stage!=previous){previous=input.stage;timeline.AppendLine(N((tick+1)/120.0)+",\""+input.stage+"\"");}
                    }
                    offset=(++tickIndex)*Block;
                    // Same fixed-tick controller/render order as the actual audition.
                    if(tick<f.ticks.Count){last=controller.Update(car.state,true,1,DragMath.FixedStep);tireLast=TireSoundFrame.From(car.state,true,1,car.spec.drive,tireProfile);}
                    synth.Fill(a,2,Rate,last);plain.Fill(b,2,Rate,last);tires.Fill(ta,2,Rate,tireLast);plainTires.Fill(tb,2,Rate,tireLast);
                    for(int i=0;i<a.Length;i++){exact&=a[i]==b[i];tireExact&=ta[i]==tb[i];Audio["engine-live"][offset*2+i]=a[i];Audio["tires-live"][offset*2+i]=ta[i];}
                }
                Check(exact&&tireExact,"passive capture is bit-identical to capture-disabled production engine and tires for the entire replay");
                var diagnostics=synth.AfterfireDiagnostics(Rate);var releases=synth.ReleaseDiagnostics(Rate);
                Check(diagnostics.Delivered==car.state.audioExhaust.mixture.Sequence&&diagnostics.HistoryOverflows==0&&diagnostics.TailSteals==0,"all real afterfire events delivered without history loss or tail stealing");
                Check(releases.Delivered==car.state.audioExhaust.ReleaseSequence&&releases.Overflows==0&&releases.TailSteals==0,"all real blow-off events delivered");
                Check(Math.Abs(car.state.elapsed-f.elapsed)<1e-9&&car.state.exhaustPopCount==f.pops,"actual elapsed time and physical pop trace unchanged");
                Check(Math.Abs(diagnostics.FundedAcousticEnergy-car.state.audioExhaust.mixture.AcousticEnergyJ)<1e-8,"actual chemical acoustic-energy funding preserved");
                Check(profile.superchargerCount==0,"Tempest has no supercharger: no fabricated blower recording is added");
                Log.AppendLine("Replay SHA256: "+Hash(File.ReadAllBytes(file)));Log.AppendLine("Native frames: "+frames+"; chemical events "+diagnostics.Delivered+"; bursts "+diagnostics.StartedBursts+"; BOV "+releases.Delivered);
                timeline.AppendLine(N(raceFrames/(double)Rate)+",\"TEST ONLY: Tempest low-grip tires, traction assistance OFF\"");
                CaptureWheelspin(f,timeline);
                File.WriteAllText(Path.Combine(Root,"race-timeline.csv"),timeline.ToString(),new UTF8Encoding(false));
            }
            finally{race.CloseOpponentWorker();}
        }
        static void CaptureWheelspin(RaceAudioTestFixture original,StringBuilder timeline)
        {
            var f=JsonUtility.FromJson<RaceAudioTestFixture>(JsonUtility.ToJson(original));
            f.driver.tractionAssist=false;f.tune.tireLevel=0;
            f.tune.frontLeftGrip=f.tune.frontRightGrip=f.tune.rearLeftGrip=f.tune.rearRightGrip=.20f;
            var race=f.CreateRace();
            try
            {
                var car=race.player;var p=new EngineSoundProfile(car.spec,car.tune);
                var controller=new EngineSoundController(car.spec,car.tune);var synth=new EngineSoundSynth(p,sampleRate:Rate){Audit=Capture};var plain=new EngineSoundSynth(p,sampleRate:Rate);
                var tires=new TireSoundSynth{Audit=CaptureTires};var plainTires=new TireSoundSynth();var tireProfile=new TireSoundProfile(car.spec,car.tune);
                var a=new float[Block*2];var b=new float[Block*2];var ta=new float[Block*2];var tb=new float[Block*2];
                bool exact=true;int slipping=0;
                for(int tick=0;tick<14*120;tick++)
                {
                    double t=tick/120.0;
                    if(race.warmup&&t>=.5&&!race.BeginCountdown())throw new InvalidOperationException("Wheelspin countdown failed");
                    var command=new DriverInput{brake=t>=10.5?.65:0,throttle=t<.5?0:t<5?1:t<6?.12:t<8.5?1:t<10.5?.3:0};
                    if(car.state.launched&&car.state.gear<car.spec.gears.Length&&car.state.rpm>car.ShiftPoint(car.state.gear)&&car.state.shiftRemaining<=0)command.shiftUp=true;
                    race.Step(command);offset=raceFrames+tick*Block;
                    var frame=controller.Update(car.state,true,1,DragMath.FixedStep);var contacts=TireSoundFrame.From(car.state,true,1,car.spec.drive,tireProfile);
                    for(int w=0;w<4;w++)if(contacts.Wheel(w).intensity>.05)slipping++;
                    synth.Fill(a,2,Rate,frame);plain.Fill(b,2,Rate,frame);tires.Fill(ta,2,Rate,contacts);plainTires.Fill(tb,2,Rate,contacts);
                    for(int i=0;i<a.Length;i++){exact&=a[i]==b[i]&&ta[i]==tb[i];Audio["engine-live"][offset*2+i]=a[i];Audio["tires-live"][offset*2+i]=ta[i];}
                }
                Check(exact,"appended low-grip Tempest contact segment also preserves capture-disabled production PCM");
                Check(slipping>120,"appended Tempest scenario produces actual per-wheel contact work with traction assistance off");
                Log.AppendLine("Test-only contact segment: 14 s, unchanged Tempest engine, street tires, local grip multipliers 0.20, traction assistance OFF; slipping wheel-ticks "+slipping);
                timeline.AppendLine(N(raceFrames/(double)Rate+1)+",\"Test wheelspin: medium throttle\"");
                timeline.AppendLine(N(raceFrames/(double)Rate+3)+",\"Test wheelspin: full throttle\"");
                timeline.AppendLine(N(raceFrames/(double)Rate+5)+",\"Test wheelspin: ease throttle\"");
                timeline.AppendLine(N(raceFrames/(double)Rate+6)+",\"Test wheelspin: full throttle again\"");
                timeline.AppendLine(N(raceFrames/(double)Rate+10.5)+",\"Test wheelspin: stop and regain grip\"");
            }
            finally{race.CloseOpponentWorker();}
        }
        static string Hash(byte[] x){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(x)).Replace("-","");}
        static void DefineTracks()
        {
            Tracks.Clear();
            Tracks.Add(new Track("01-combustion-exhaust","engine"));Tracks.Add(new Track("02-block-and-manifold","mechanical"));
            Tracks.Add(new Track("03-cut-and-control-colour","controls"));Tracks.Add(new Track("04-mechanical-clatter","clatter"));Tracks.Add(new Track("05-intake-rush","intake"));
            Tracks.Add(new Track("06-turbo-whistle","turbo-whistle"));Tracks.Add(new Track("07-turbo-airflow","turbo-airflow"));Tracks.Add(new Track("08-turbine-interaction","turbo-turbine"));
            Tracks.Add(new Track("09-blow-off-valve","bov"));Tracks.Add(new Track("10-backfire","after"));Tracks.Add(new Track("11-pressure-driven-shell","after-wall"));
            for(int i=0;i<4;i++)Tracks.Add(new Track((12+i).ToString("00")+"-"+Tires[i],Tires[i]));
            Tracks.Add(new Track("16-all-tires",Tires));Tracks.Add(new Track("17-engine-and-intake",Base));
            Tracks.Add(new Track("18-engine-and-turbo",Base.Concat(Turbo).ToArray()));Tracks.Add(new Track("19-engine-and-backfires",Base.Concat(new[]{"after"}).ToArray()));
            Tracks.Add(new Track("20-induction-bov-backfires",Turbo.Concat(new[]{"bov","after"}).ToArray()));
            Tracks.Add(new Track("21-full-engine",Base.Concat(Turbo).Concat(new[]{"bov","after"}).ToArray()));
            Tracks.Add(new Track("22-full-vehicle",Base.Concat(Turbo).Concat(new[]{"bov","after"}).Concat(Tires).ToArray()));
            Tracks.Add(new Track("23-backfires-and-bov","after","bov"));
        }
        static void Add(float[] target,float[] x){for(int i=0;i<target.Length;i++)target[i]+=x[i];}
        static void Subtract(float[] target,float[] x){for(int i=0;i<target.Length;i++)target[i]-=x[i];}
        static void Choose(float[] engine,float[] tire,string key,Pass p)
        {
            if(key.StartsWith("tire-",StringComparison.Ordinal)){Add(tire,Audio[key]);return;}
            if(key=="after")
            {
                if(p.source==0){Add(engine,Audio["after-raw"]);return;}
                if(p.source==1){Add(engine,Audio["after-shaped"]);return;}
                if(p.source==2){Add(engine,Audio["after-attenuated"]);return;}
                if(p.noColour){Add(engine,Audio[p.noShell?"after-raw-delayed":"after-raw-routed"]);return;}
                Add(engine,Audio[p.noTravel?"after-attenuated":p.noLoss?"after-unattenuated":"after-outlet"]);
                if(!p.noShell)Add(engine,Audio["after-wall"]);return;
            }
            string selected=key;
            if(key=="engine"||key=="controls")selected=key+((p.source>=1&&!p.noColour)?"-fitted":"");
            if(key=="intake"&&p.source==0)selected="intake-raw";
            Add(engine,Audio[selected]);
        }
        static void Dc(float[] x)
        {double pl=0,pr=0,yl=0,yr=0;for(int i=0;i<x.Length;i+=2){if(i==raceFrames*2)pl=pr=yl=yr=0;double l=x[i],r=x[i+1];yl=l-pl+.995*yl;yr=r-pr+.995*yr;pl=l;pr=r;x[i]=(float)yl;x[i+1]=(float)yr;}}
        static void Limit(float[] x){for(int i=0;i<x.Length;i++)x[i]=(float)Soft(x[i]);}
        static void Outside(float[] x,TracksidePropagation.Frame at,Pass p)
        {
            var dsp=new TracksidePropagation(Rate){AuditSpatial=!p.noSpatial,AuditAir=p.outdoor>=2&&!p.noAir,AuditReflections=p.outdoor>=3&&!p.noReflections};
            var block=new float[Block*2];
            for(int i=0;i<x.Length;i+=block.Length){if(i==raceFrames*2)dsp.Clear();Array.Copy(x,i,block,0,block.Length);dsp.Process(block,2,at);Array.Copy(block,0,x,i,block.Length);}
        }
        static float[] Render(Track track,Pass p,bool exactFinal=false)
        {
            var engine=new float[frames*2];var tire=new float[frames*2];bool hasEngine=false,hasTire=false;
            foreach(string key in track.keys){Choose(engine,tire,key,p);if(key.StartsWith("tire-",StringComparison.Ordinal))hasTire=true;else hasEngine=true;}
            if(hasEngine){if(p.dc)Dc(engine);if(p.limit)Limit(engine);}
            if(hasTire&&p.limit)Limit(tire);
            if(exactFinal&&track.name=="22-full-vehicle")
            {Array.Copy(Audio["engine-live"],engine,engine.Length);Array.Copy(Audio["tires-live"],tire,tire.Length);}
            if(p.outdoor>0){if(hasEngine)Outside(engine,listener,p);if(hasTire)Outside(tire,tireListener,p);}
            Add(engine,tire);return engine;
        }
        static double Error(float[] x,float[] y){double max=0;for(int i=0;i<x.Length;i++)max=Math.Max(max,Math.Abs(x[i]-y[i]));return max;}
        static void VerifyReconstruction()
        {
            var engine=Tracks.Single(x=>x.name=="21-full-engine");var pre=Render(engine,Passes[3]);var dc=Render(engine,Passes[4]);var final=Render(engine,Passes[5]);
            Check(Error(pre,Audio["premix-live"])<2e-6,"separate captured generators sum back to the real engine premix");
            Check(Error(dc,Audio["dc-live"])<2e-6,"offline DC pass matches production DC output");
            Check(Error(final,Audio["engine-live"])<2e-6,"offline final limiter matches production engine PCM");
            var tires=Render(Tracks.Single(x=>x.name=="16-all-tires"),Passes[5]);
            Check(Error(tires,Audio["tires-live"])<2e-6,"four contact generators sum to production tire PCM through their shared limiter");
            Check(Audio["after-raw"].Any(x=>Math.Abs(x)>1e-5)&&Audio["bov"].Any(x=>Math.Abs(x)>1e-5),"actual replay includes clearly nonzero chemical afterfires and BOV events");
            Check(Tires.Any(k=>Audio[k].Any(x=>Math.Abs(x)>1e-5)),"actual replay contains genuine wheelspin/contact audio");
            Log.AppendLine("Reconstructed engine maximum PCM difference: "+N(Error(final,Audio["engine-live"])));
        }
        static void Stats(float[] x,out double peak,out double rms)
        {double sum=0;peak=0;foreach(float f in x){if(float.IsNaN(f)||float.IsInfinity(f))throw new InvalidOperationException("Nonfinite captured PCM");peak=Math.Max(peak,Math.Abs(f));sum+=(double)f*f;}rms=Math.Sqrt(sum/x.Length);}
        static string Time(double t)=>TimeSpan.FromSeconds(t).ToString(@"mm\:ss\.fff",Invariant);
        static void WriteCue(string path,string wav,string[] titles,int length)
        {
            var s=new StringBuilder("FILE \""+wav+"\" WAVE\n");for(int i=0;i<titles.Length;i++)
            {double seconds=i*(length/(double)Rate+1);int sector=(int)Math.Round(seconds*75);s.AppendLine("  TRACK "+(i+1).ToString("00")+" AUDIO\n    TITLE \""+titles[i]+"\"\n    INDEX 01 "+(sector/4500).ToString("00")+":"+((sector/75)%60).ToString("00")+":"+(sector%75).ToString("00"));}
            File.WriteAllText(path,s.ToString(),new UTF8Encoding(false));
        }
        static void WriteWav(string path,float[][] sections,double[] gains,int beginFrame,int length,string[] titles)
        {
            long sampleFrames=(long)sections.Length*length+(long)(sections.Length-1)*Gap,dataBytes=sampleFrames*6;
            using(var w=new BinaryWriter(new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.Read,1<<20)))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write((uint)(36+dataBytes));w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(Rate);w.Write(Rate*6);w.Write((short)6);w.Write((short)24);
                w.Write(Encoding.ASCII.GetBytes("data"));w.Write((uint)dataBytes);var bytes=new byte[65532];
                for(int part=0;part<sections.Length;part++)
                {
                    int used=0;for(int i=beginFrame*2;i<(beginFrame+length)*2;i++)
                    {
                        double value=sections[part][i]*gains[part];if(Math.Abs(value)>.999999)throw new InvalidOperationException("WAV clipping");
                        int q=(int)Math.Round(value*8388607);bytes[used++]=(byte)q;bytes[used++]=(byte)(q>>8);bytes[used++]=(byte)(q>>16);
                        if(used==bytes.Length){w.Write(bytes);used=0;}
                    }
                    if(used>0)w.Write(bytes,0,used);
                    if(part+1<sections.Length){Array.Clear(bytes,0,bytes.Length);int remaining=Gap*6;while(remaining>0){int n=Math.Min(remaining,bytes.Length);w.Write(bytes,0,n);remaining-=n;}}
                }
                // Embed named sample-accurate chapter markers, in addition to CUE/CSV.
                w.Write(Encoding.ASCII.GetBytes("cue "));w.Write(4+24*titles.Length);w.Write(titles.Length);
                for(int i=0;i<titles.Length;i++){uint at=(uint)((long)i*(length+Gap));w.Write(i+1);w.Write(at);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(0);w.Write(0);w.Write(at);}
                using(var labels=new MemoryStream())using(var l=new BinaryWriter(labels,Encoding.UTF8,true))
                {
                    l.Write(Encoding.ASCII.GetBytes("adtl"));for(int i=0;i<titles.Length;i++){var title=Encoding.UTF8.GetBytes(titles[i]+"\0");l.Write(Encoding.ASCII.GetBytes("labl"));l.Write(4+title.Length);l.Write(i+1);l.Write(title);if((title.Length&1)!=0)l.Write((byte)0);}
                    w.Write(Encoding.ASCII.GetBytes("LIST"));w.Write((int)labels.Length);w.Write(labels.ToArray());
                }
                long end=w.BaseStream.Position;w.BaseStream.Position=4;w.Write((uint)(end-8));
            }
            WriteCue(Path.ChangeExtension(path,".cue"),Path.GetFileName(path),titles,length);
        }
        static void WriteTrack(Track track)
        {
            Debug.Log("TEMPEST EXPORT "+track.name);var sections=new float[Passes.Length][];var peaks=new double[Passes.Length];var rms=new double[Passes.Length];
            for(int i=0;i<sections.Length;i++){sections[i]=Render(track,Passes[i],i==8);Stats(sections[i],out peaks[i],out rms[i]);maxPeak=Math.Max(maxPeak,peaks[i]);}
            // One fixed gain for the whole fixed-level file: never hide a quieter
            // hardware/atmosphere stage by normalizing its segment independently.
            double fixedGain=Math.Min(1,.90/peaks.Max());var fixedGains=Enumerable.Repeat(fixedGain,sections.Length).ToArray();
            var matched=new double[sections.Length];double reference=rms[8]>1e-12?rms[8]:rms.Max(),matchedPeak=0;
            for(int i=0;i<matched.Length;i++){matched[i]=rms[i]>1e-12?reference/rms[i]:1;matchedPeak=Math.Max(matchedPeak,peaks[i]*matched[i]);}
            double common=matchedPeak>0?.85/matchedPeak:1;for(int i=0;i<matched.Length;i++)matched[i]*=common;
            var titles=Passes.Select(x=>x.title).ToArray();
            WriteWav(Path.Combine(Root,"fixed-level",track.name+".wav"),sections,fixedGains,0,frames,titles);
            WriteWav(Path.Combine(Root,"level-matched",track.name+".wav"),sections,matched,0,frames,titles);
            var csv=new StringBuilder("pass,start_seconds,end_seconds,processing,source_peak,source_rms,fixed_gain,matched_gain\n");
            for(int i=0;i<Passes.Length;i++)csv.AppendLine(i+","+N(i*(frames/(double)Rate+1))+","+N(i*(frames/(double)Rate+1)+frames/(double)Rate)+",\""+titles[i]+"\","+N(peaks[i])+","+N(rms[i])+","+N(fixedGains[i])+","+N(matched[i]));
            File.WriteAllText(Path.Combine(Root,track.name+"-passes.csv"),csv.ToString(),new UTF8Encoding(false));
            if(track.name=="10-backfire"||track.name=="22-full-vehicle")
                WriteWav(Path.Combine(Root,"quick",track.name+"-launch-and-lift.wav"),sections,matched,(int)(13.5*Rate),6*Rate,titles);
            Check(peaks.All(x=>!double.IsNaN(x))&&fixedGain>0,"finite, unclipped stereo WAV and level-matched passes / "+track.name);
            Save();
        }
        static void Internals()
        {
            string[] keys={"core-raw","core-header","core-collector","core-band","core-turbine","core-body","engine","engine-fitted"};
            string[] titles={"00 Cylinder pressure before headers","01 Header echoes and modes","02 Collector echoes","03 Outlet band limiting","04 Boost-dependent turbine filtering","05 Body EQ and resonances","06 Generator body saturation","07 Fitted exhaust filter"};
            var sections=keys.Select(k=>Audio[k]).ToArray();var gains=new double[sections.Length];double peak=0;
            foreach(var x in sections){Stats(x,out double p,out double rms);peak=Math.Max(peak,p);}double g=Math.Min(1,.9/peak);
            for(int i=0;i<gains.Length;i++)gains[i]=g;
            WriteWav(Path.Combine(Root,"fixed-level","24-engine-internal-filters.wav"),sections,gains,0,frames,titles);
            double target=0;Stats(sections.Last(),out double unused,out target);double matchedPeak=0;
            for(int i=0;i<gains.Length;i++){Stats(sections[i],out double p,out double r);gains[i]=r>1e-12?target/r:1;matchedPeak=Math.Max(matchedPeak,p*gains[i]);}
            for(int i=0;i<gains.Length;i++)gains[i]*=.85/Math.Max(1e-12,matchedPeak);
            WriteWav(Path.Combine(Root,"level-matched","24-engine-internal-filters.wav"),sections,gains,0,frames,titles);
            var csv=new StringBuilder("pass,start_seconds,processing\n");for(int i=0;i<titles.Length;i++)csv.AppendLine(i+","+N(i*(frames/(double)Rate+1))+",\""+titles[i]+"\"");
            File.WriteAllText(Path.Combine(Root,"24-engine-internal-filters-passes.csv"),csv.ToString(),new UTF8Encoding(false));
            Check(true,"engine-internal header/collector/band/turbine/EQ/saturation/fitted-filter boundaries exported separately");
        }
        public static void Run()
        {
            CultureInfo.CurrentCulture=Invariant;Directory.CreateDirectory(Root);Log.Clear();checks=0;Audio.Clear();maxPeak=0;
            foreach(string folder in new[]{"fixed-level","level-matched","quick"})Directory.CreateDirectory(Path.Combine(Root,folder));
            CaptureRace();DefineTracks();VerifyReconstruction();Save();
            foreach(var track in Tracks){WriteTrack(track);GC.Collect();}
            Internals();Save();
            File.WriteAllText(Path.Combine(Root,"LISTENING_GUIDE.md"),Guide(),new UTF8Encoding(false));
            Debug.Log("TEMPEST PROCESSING EXPORT PASS "+checks+" / "+Root);
        }
        public static void VerifyCaptureAndBuild()
        {
            string regression=Path.Combine(Root,"regression");Directory.CreateDirectory(regression);
            AfterfireLabVerification.OutputDirectory=regression;AfterfireLabVerification.Run();AfterfireLabVerification.VerifyCausality();
            RuntimeAudioIntegrationVerification.OutputDirectory=regression;RuntimeAudioIntegrationVerification.TracksideListening=true;
            RuntimeAudioIntegrationVerification.Run();RuntimeAudioIntegrationVerification.CheckRaces();
            // Build proves all audit-only types/fields compile out of players.
            PlayerBuild.BuildPlayerOnly();
        }
        static string Guide()
        {
            var s=new StringBuilder("# Tempest processing comparisons\n\n48 kHz / 24-bit stereo PCM. One real 26-launch-hard-ignition replay, player only.\nNo game sound calibration/settings/save changes. All passes reuse the same captured\nphysics, events, seeds and PCM. No opponent, UI, music or invented supercharger.\n\nStart with quick/10-backfire-launch-and-lift.wav and quick/22-full-vehicle-launch-and-lift.wav.\nEach quick segment is the same six seconds at race time 13.5–19.5 s.\n\nEvery full WAV repeats the entire "+N(frames/(double)Rate)+" s scenario in the order below,\nwith exactly one second of digital silence between passes. The original replay plus a two-second end hold is followed by a labelled\n14-second test-only low-grip Tempest segment: traction assistance OFF, street\ntires, local grip multipliers 0.20; contact audio comes from actual physics.\nThe first race and its engine configuration remain unchanged. CUE files and embedded cue/LIST labels mark passes.\nPer-file *-passes.csv gives exact times and gains; race-timeline.csv gives events\nrelative to each full segment. The engine-internals file has its own eight passes.\n\nfixed-level/ uses a constant gain within each file, lowered only if needed to avoid\nclipping a raw peak. Consult CSV for that gain. Do not compare volume across files\nwithout accounting for the gain. level-matched/ matches whole-segment RMS within\neach file, then applies one common peak guard; this is for timbre comparisons.\nIt can make normally subtle intake/airflow layers much louder than gameplay.\nNo per-event normalization, extra compressor, fades or dither is added.\n\nRaw means the defined generator output with physical event timing/envelopes and\nits intrinsic source synthesis/anti-aliasing intact, before added routing/mix/outdoor\nprocessing. It is not unfiltered white noise or a physics-free oscillator. The\nengine-internals WAV additionally reveals the engine generator's internal filters.\nPass 09 removes fitted engine exhaust coloration and backfire source hardware\ncolour; it does not bypass the engine core's intrinsic pressure model.\n\nProcessing is applied at the actual bus boundaries: engine DC/limiter on the engine\nsum, tires through their own limiter, engine outdoor at .55 m source height and\ntires at .08 m, followed by summation. Reflections include ground/barrier/stands\nand diffuse paths. Listener follows the car at 12 m lateral separation with fixed\nlongitudinal offset, avoiding camera motion as an uncontrolled pitch change.\nThe WAV includes native DSP, not Windows/phone speaker/driver processing.\nThe full final pass of 22-full-vehicle uses the actual captured production engine\nand tire PCM before the same native outdoor renderer. Other combinations are\nrebuilt from captured generators, preserving nonlinear bus placement. Solos\ntherefore do not simply sum after limiting to reproduce a limited full mixture.\nA processing step that does not apply to a source correctly leaves it unchanged.\n\n## Pass order\n\n| Pass | Full-file start | Quick-file start | Processing |\n|---|---|---|---|\n");
            for(int i=0;i<Passes.Length;i++)s.AppendLine("| "+i+" | "+Time(i*(frames/(double)Rate+1))+" | "+Time(i*7)+" | "+Passes[i].title+" |");
            s.AppendLine("\n## Files\n");foreach(var t in Tracks)s.AppendLine("- "+t.name+".wav: "+string.Join(" + ",t.keys));
            s.AppendLine("- 24-engine-internal-filters.wav: eight engine-source boundaries.\n\nAll source captures, timestamps, stage filters and packaging are generated by\nDragLab.Editor.TempestProcessingAudition.Run. Listening acceptance remains pending.");return s.ToString();
        }
    }
}
