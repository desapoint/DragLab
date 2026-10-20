using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    // Explicit acoustic laboratory: controlled frames/events, not new race physics or automatic event prediction.
    public static class SharedBackfireAudition
    {
        const int Rate=48000,Block=400,Seconds=64;
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        static int checks;
        [Serializable] public sealed class Chapter {public double start,end;public string title,note;public int accepted,suppressed;public double eventRms;}
        [Serializable] public sealed class EventReceipt {public double time,energyJ;public string label;public bool accepted;public PreviewCombustionEvent input;}
        [Serializable] public sealed class Result
        {
            public string id,title,file,source,fuel,induction,valve,exhaust,geometry,sha256;
            public int cylinders,accepted,suppressed,overflows;public double litres,duration=Seconds,peak,rms,elapsedSeconds,lengthM,diameterScale,temperatureK,callbackMillisecondsPerSecond;
            public List<Chapter> chapters=new List<Chapter>();public List<EventReceipt> events=new List<EventReceipt>();
            public PreviewCombustionVoicing voicing;
        }
        [Serializable] public sealed class Manifest {public int sampleRate=Rate,channels=2;public string scope="Preview-only controlled acoustic bench. No race/save/runtime changes.";public List<Result> cases=new List<Result>();}
        sealed class Config {public string id,title,source,geometry="Assumed laboratory geometry";public VehicleSpec spec;public Tune tune;public double lengthScale=1,diameterScale=1,temperature=650,leak;}
        sealed class PathData {public PreviewDuctSegment[] segments;public double before,inside,after,manifold;public bool silencer;}
        sealed class Scheduled {public int tick,chapter;public string label;public PreviewCombustionEvent input;}
        static void Check(bool yes,string message){checks++;if(!yes)throw new InvalidOperationException("SHARED BACKFIRE: "+message);}
        static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        static string DirectoryPath=>Path.Combine(Root,"reports/shared-backfire-audition-20261020");
        static List<Config> Configurations()
        {
            var list=new List<Config>();var hardware=RaceAudioAuditionVerification.AcousticHardware().ToDictionary(f=>f.id,StringComparer.Ordinal);
            var ids=new List<string>{"01-i3-na","02-i4-na","05-i5-turbo-flutter","06-i6-na","07-i6-carb","09-v8-supercharger","10-diesel-turbo","11-v10-na","12-v12-twin","13-v8-twin-supercharger"};
            ids.AddRange(new[]{"28-exhaust-0","29-exhaust-1","30-exhaust-2","31-exhaust-3"});
            ids.AddRange(hardware.Keys.Where(n=>int.Parse(n.Substring(0,2),Inv)>=32&&int.Parse(n.Substring(0,2),Inv)<=43).OrderBy(n=>n,StringComparer.Ordinal));
            foreach(string id in ids){
                var f=hardware[id];
                list.Add(new Config{id=id,title=f.title,source=id,spec=f.spec.Copy(),tune=f.tune.Copy()});
            }
            var basis=list.Single(c=>c.id=="30-exhaust-2");
            Action<string,string,Action<Config>> add=(id,title,edit)=>{
                var c=new Config{id=id,title="Bison controlled comparison: "+title,source=basis.id,spec=basis.spec.Copy(),tune=basis.tune.Copy()};edit(c);list.Add(c);
            };
            add("50-short-pipe","shorter pipe",c=>c.lengthScale=.65);
            add("51-long-pipe","longer pipe",c=>c.lengthScale=1.5);
            add("52-narrow-pipe","narrower pipe",c=>c.diameterScale=.75);
            add("53-wide-pipe","wider pipe",c=>c.diameterScale=1.35);
            add("54-cool-exhaust","cooler propagation path",c=>c.temperature=450);
            add("55-hot-exhaust","hotter propagation path",c=>c.temperature=1000);
            add("56-catalyst-resonator","catalyst, resonator and absorptive silencer",c=>c.spec.sound.exhaustSystem=ExhaustSetup.Preset(0));
            add("57-leaking-silencer","explicit silencer leak fault",c=>c.leak=.18);
            return list;
        }
        static PathData Exhaust(Config c,EngineSoundProfile p)
        {
            var list=new List<PreviewDuctSegment>();double position=0,before=0,inside=0,after=0;bool hasSilencer=false;
            Action<double,double,ExhaustPart,bool,double> add=(length,diameter,part,turbine,leak)=>{
                var segment=new PreviewDuctSegment{lengthM=length*c.lengthScale,diameterM=diameter*c.diameterScale,temperatureK=c.temperature,component=part,turbine=turbine,leak=leak};
                if(part!=null&&(part.kind==ExhaustPartKind.AbsorptiveSilencer||part.kind==ExhaustPartKind.ChamberedSilencer)){
                    before=position-.035*c.lengthScale;inside=position+segment.lengthM*.5;after=position+segment.lengthM+.035*c.lengthScale;hasSilencer=true;
                }
                list.Add(segment);position+=segment.lengthM;
            };
            add(.45,.048,null,false,0);
            if(p.turboCount>0)add(.18,.06,null,true,0);
            add(.65,.06,null,false,0);
            foreach(var part in p.exhaustSystem.parts){
                bool muffler=part.kind==ExhaustPartKind.AbsorptiveSilencer||part.kind==ExhaustPartKind.ChamberedSilencer;
                add(muffler?.65:part.kind==ExhaustPartKind.Resonator?.32:.22,muffler?.13:part.kind==ExhaustPartKind.CatalyticConverter?.085:.09,part.Copy(),false,muffler?c.leak:0);
            }
            if(!hasSilencer){before=position*.35;inside=position*.65;after=position+.2*c.lengthScale;}
            add(.8,.06,null,false,0);
            // Prepare exact injection nodes for this controlled flow; no source jumps straight to a distant junction.
            var injectionSites=new[]{before,inside,after,.12*c.lengthScale};var refined=new List<PreviewDuctSegment>();position=0;
            foreach(var segment in list){
                double end=position+segment.lengthM,last=position;
                var cuts=injectionSites.Where(s=>s>position+1e-9&&s<end-1e-9).Concat(new[]{end}).OrderBy(s=>s);
                foreach(double cut in cuts){
                    double fraction=(cut-last)/segment.lengthM;var part=segment.component?.Copy();if(part!=null)part.amount*=(float)fraction;
                    refined.Add(new PreviewDuctSegment{lengthM=cut-last,diameterM=segment.diameterM,temperatureK=segment.temperatureK,turbine=segment.turbine,component=part,leak=segment.leak*fraction});last=cut;
                }
                position=end;
            }
            return new PathData{segments=refined.ToArray(),before=before,inside=inside,after=after,manifold=.12*c.lengthScale,silencer=hasSilencer};
        }
        static PreviewDuctSegment[] Intake(Config c)=>new[]{
            new PreviewDuctSegment{lengthM=.18,diameterM=.05,temperatureK=330},
            new PreviewDuctSegment{lengthM=.4,diameterM=.085,temperatureK=310,component=new ExhaustPart{kind=ExhaustPartKind.Pipe,lowPassHz=2300,attenuationDb=3,pulseSoftening=.2f}}
        };
        static readonly string[] Titles={"Idle baseline","Throttle blips","Fuel limiter: hard then soft","Ignition limiter: hard then soft","Two-step: ignition then fuel","Loaded pull / shift","Small overrun pop","Medium afterfire","Large afterfire: upstream","Same event: inside silencer","Same event: downstream","Anti-lag / manifold source","Delayed fuel / misfire fault","Intake backfire fault","Oxygen-starved control","Return to idle"};
        static List<Scheduled> Events(EngineSoundProfile p,PathData path,Result r)
        {
            bool diesel=p.fueling==FuelSystem.Diesel;var list=new List<Scheduled>();
            for(int chapter=0;chapter<Titles.Length;chapter++){
                string note="Controlled laboratory input; not automatic game behavior.";
                if(chapter==2)note="Fuel is cut: engine limiter audible; no added combustion energy.";
                if(chapter==3)note="Hard ignition cuts first; partial soft cuts after the 2-second midpoint.";
                if(chapter==4)note="Ignition-cut two-step first; fuel-cut/no-explosion control after the 2-second midpoint.";
                if((chapter==3||chapter==4||chapter==13)&&diesel)note="Not applicable to normal diesel operation: no petrol spark-cut or intake-fuel event.";
                if(chapter>=6&&chapter<=10&&diesel)note="Normal diesel overrun control: no injected exhaust fuel.";
                if(chapter==11&&(diesel||p.turboCount==0))note="No supported petrol turbo anti-lag: control only.";
                if((chapter==9||chapter==10)&&!path.silencer)note="No fitted silencer: corresponding source is in mid/rear pipe.";
                if(chapter==12)note=diesel?"Deliberately forced hot-exhaust fueling fault, not ordinary diesel behavior.":"Deliberately forced delayed unburned-fuel fault.";
                r.chapters.Add(new Chapter{start=chapter*4,end=chapter*4+4,title=Titles[chapter],note=note});
            }
            Action<int,double,string,double,double,double,double,bool,bool> add=(chapter,offset,label,mg,burn,mix,source,intake,enabled)=>{
                double displacement=DragMath.Clamp(p.litres/5,.3,2);
                var input=new PreviewCombustionEvent{
                    fuelMg=mg*displacement,oxygenMg=mg*displacement*3.5,heatingJPerKg=diesel?43e6:44e6,oxygenPerFuel=diesel?3.36:3.43,burnEfficiency=.72,
                    burnSeconds=burn,mixingSeconds=mix,volumeLitres=intake?.65:Math.Max(.6,p.litres*.65),temperatureK=chapter==12&&diesel?1000:650,pressureKPa=chapter==11?130:101.3,
                    sourceMetres=source,intake=intake,ignited=enabled,seed=(uint)(171818+(chapter>=8&&chapter<=10?8:chapter)*7919+(int)(offset*120))
                };
                if(chapter==14)input.oxygenMg=0;
                list.Add(new Scheduled{tick=(int)Math.Round((chapter*4+offset)*120),chapter=chapter,label=label,input=input});
            };
            add(2,1,"Fuel-cut control",0,.002,.006,path.manifold,false,true);
            for(int i=0;i<19;i++)add(3,.55+i*.15,i<10?"Hard ignition-cut combustion":"Partial soft ignition-cut combustion",i<10?1.1:.55,i<10?.0014:.0021,.006,path.manifold,false,!diesel);
            for(int i=0;i<18;i++)add(4,.55+i*.08,"Ignition-cut two-step combustion",1.4,.0018,.008,path.manifold,false,!diesel);
            add(4,3,"Fuel-cut two-step control",0,.0018,.008,path.manifold,false,false);
            add(5,1.1,"Shift combustion",2.4,.003,.014,path.manifold,false,!diesel);
            add(5,2.6,"Second shift combustion",2.4,.003,.014,path.manifold,false,!diesel);
            add(6,1,"Small overrun combustion",4,.006,.024,path.before,false,!diesel);
            add(7,1,"Medium exhaust combustion",16,.020,.070,path.before,false,!diesel);
            add(8,1,"Large exhaust combustion",48,.065,.150,path.before,false,!diesel);
            add(9,1,"Same energy moved inside silencer",48,.065,.150,path.inside,false,!diesel);
            add(10,1,"Same energy moved downstream",48,.065,.150,path.after,false,!diesel);
            for(int i=0;i<16;i++)add(11,.6+i*.15,"Manifold anti-lag combustion",2.1,.0035,.014,path.manifold,false,!diesel&&p.turboCount>0);
            add(12,1,"Forced hot delayed-fuel fault",32,.045,.12,path.manifold,false,true);
            add(12,2.5,"Smaller delayed-fuel fault",7,.014,.04,path.before,false,true);
            add(13,1,"Intake fault combustion",2.8,.005,.025,.045,true,!diesel);
            add(14,1,"Fuel without available oxygen",48,.065,.15,path.before,false,true);
            return list.OrderBy(x=>x.tick).ToList();
        }
        static EngineSoundFrame Frame(EngineSoundProfile p,int tick,ref int release,double previousGas)
        {
            double time=tick/120.0,local=time%4;int chapter=(int)(time/4);
            double pedal=.06,load=.08,rpm=p.idleRpm;bool cut=false,twoStep=false,overrun=false;
            double combustion=1;
            double upper=p.redlineRpm*.91,range=upper-p.idleRpm;
            if(chapter==1){double pulse=local<1.6?Math.Sin(Math.PI*local/1.6):local<3.2?Math.Sin(Math.PI*(local-1.6)/1.6):0;pedal=Math.Max(0,pulse)*.9;rpm+=range*pedal*.7;}
            else if(chapter>=2&&chapter<=4){pedal=.92;twoStep=chapter==4;rpm=twoStep?Math.Min(3200,upper*.7):upper;cut=((int)(local*(chapter==4?25:12))%2)==0;rpm-=cut?180:0;load=.25;}
            else if(chapter==5){pedal=.95;load=.85;double since=local<1.1?local:local<2.6?local-1.1:local-2.6;rpm=p.idleRpm+range*(.45+Math.Min(.5,since*.3));cut=(local>=1.1&&local<1.22)||(local>=2.6&&local<2.72);}
            else if(chapter>=6&&chapter<=14){pedal=local<.8?.8:local<2.1?.02:.45;load=local<.8?.8:.15;rpm=p.idleRpm+range*(local<.8?.7:Math.Max(.12,.7-(local-.8)*.18));overrun=pedal<.1;}
            if(cut){combustion=local>=2&&(chapter==2||chapter==3)?.4:0;if(combustion>0)cut=false;}
            if(p.turboCount>0&&previousGas-pedal>.3)release++;
            double pressure=p.boostBar*Math.Max(pedal*.8,overrun?.15:0),turbo=p.turboCount>0?pressure:0,blower=p.superchargerCount>0?pressure:0;
            double flow=Math.Max(.005,p.litres*rpm/120000*.8*(1+pressure));
            return new EngineSoundFrame(rpm,pedal,load,pressure,.72,cut:cut,releaseId:release,releaseStrength:.65,turboSpool:p.turboCount>0?Math.Sqrt(Math.Max(0,pressure/Math.Max(.1,p.boostBar))):0,
                superchargerBoost:blower,chargerLoad:pedal*.6,turboBoost:turbo,combustionFraction:combustion,overrun:overrun,twoStep:twoStep,
                cutMethod:chapter==2||(chapter==4&&local>=2)||p.fueling==FuelSystem.Diesel?CutMethod.Fuel:CutMethod.Ignition,airflowKgS:flow);
        }
        static void Header(BinaryWriter writer,int frames)
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+frames*4);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
            writer.Write((short)1);writer.Write((short)2);writer.Write(Rate);writer.Write(Rate*4);writer.Write((short)4);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(frames*4);
        }
        static Result Render(Config c,PreviewCombustionVoicing voicing=null,string directory=null)
        {
            directory=directory??DirectoryPath;
            var originalProfile=new EngineSoundProfile(c.spec,c.tune);var path=Exhaust(c,originalProfile);
            var bedSpec=c.spec.Copy();bedSpec.sound.exhaustPopGain=0;
            var profile=new EngineSoundProfile(bedSpec,c.tune);var bed=new EngineSoundSynth(profile,sampleRate:Rate);
            var source=new SharedBackfirePreviewSynth(path.segments,Intake(c),Rate,profile.exhaust,voicing);
            var result=new Result{id=c.id,title=c.title,file=c.id+".wav",source=c.source,cylinders=profile.cylinders,litres=profile.litres,fuel=profile.fueling.ToString(),induction=profile.induction.ToString(),valve=profile.valve.ToString(),exhaust=string.Join(" + ",profile.exhaustSystem.parts.Select(p=>p.kind.ToString())),geometry=c.geometry,lengthM=source.ExhaustLength,diameterScale=c.diameterScale,temperatureK=c.temperature};
            result.voicing=(voicing??new PreviewCombustionVoicing()).Copy();
            var events=Events(profile,path,result);int eventAt=0,release=0;double previousGas=0,energy=0;var pcm=new float[Block*2];var bursts=new double[Block];var eventEnergy=new double[16];var eventSamples=new int[16];
            var timer=Stopwatch.StartNew();double dspMilliseconds=0;
            using(var writer=new BinaryWriter(File.Create(Path.Combine(directory,result.file)))){
                Header(writer,Seconds*Rate);
                for(int tick=0;tick<Seconds*120;tick++){
                    var frame=Frame(profile,tick,ref release,previousGas);previousGas=frame.throttle;
                    bed.Fill(pcm,2,Rate,frame);source.SetBoost(frame.turboBoost);
                    while(eventAt<events.Count&&events[eventAt].tick==tick){
                        var ev=events[eventAt++];bool accepted=source.Trigger(ev.input);
                        result.events.Add(new EventReceipt{time=ev.tick/120.0,label=ev.label,energyJ=ev.input.EnergyJ,accepted=accepted,input=ev.input});
                        if(accepted)result.chapters[ev.chapter].accepted++;else result.chapters[ev.chapter].suppressed++;
                    }
                    long before=Stopwatch.GetTimestamp();
                    for(int sample=0;sample<Block;sample++)bursts[sample]=source.Sample()*.72*originalProfile.popGain;
                    dspMilliseconds+=(Stopwatch.GetTimestamp()-before)*1000.0/Stopwatch.Frequency;
                    for(int sample=0;sample<Block;sample++){
                        double burst=bursts[sample];
                        int chapter=tick/480;eventEnergy[chapter]+=burst*burst;eventSamples[chapter]++;
                        for(int channel=0;channel<2;channel++){
                            float engine=pcm[sample*2+channel];double clean=engine/(1-Math.Abs(engine));
                            double value=burst==0?engine:(clean+burst)/(1+Math.Abs(clean+burst));
                            if(double.IsNaN(value)||double.IsInfinity(value)||Math.Abs(value)>=1)throw new InvalidOperationException("Invalid mixed preview PCM");
                            energy+=value*value;result.peak=Math.Max(result.peak,Math.Abs(value));writer.Write((short)Math.Round(value*32767));
                        }
                    }
                }
            }
            timer.Stop();result.elapsedSeconds=timer.Elapsed.TotalSeconds;result.callbackMillisecondsPerSecond=dspMilliseconds/Seconds;
            for(int i=0;i<16;i++)result.chapters[i].eventRms=Math.Sqrt(eventEnergy[i]/Math.Max(1,eventSamples[i]));
            result.accepted=source.Accepted;result.suppressed=source.Suppressed;result.overflows=source.RejectedOverlap;result.rms=Math.Sqrt(energy/(Rate*Seconds*2.0));
            using(var hash=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(Path.Combine(directory,result.file)))result.sha256=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            Check(eventAt==events.Count,c.id+" scheduled events consumed");Check(result.overflows==0,c.id+" no dropped overlapping voices");
            Check(result.chapters[2].accepted==0&&result.chapters[14].accepted==0,c.id+" fuel/oxygen controls suppress combustion");
            Check(result.chapters[15].eventRms<1e-6,c.id+" tails decay before final idle");
            if(profile.fueling==FuelSystem.Diesel)Check(result.chapters[3].accepted==0&&result.chapters[4].accepted==0&&result.chapters[13].accepted==0,c.id+" no diesel petrol-only events");
            return result;
        }
        public static void Run()
        {
            System.IO.Directory.CreateDirectory(DirectoryPath);checks=0;
            SharedBackfirePreviewVerification.RunChecks();
            var manifest=new Manifest();var configs=Configurations();
            Check(configs.Count==34,"expected 34 controlled configurations");
            foreach(var config in configs){manifest.cases.Add(Render(config));File.WriteAllText(Path.Combine(DirectoryPath,"manifest.json"),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));UnityEngine.Debug.Log("SHARED BACKFIRE "+manifest.cases.Count+" / "+configs.Count+" "+config.id);}
            var csv=new StringBuilder("id,cylinders,fuel,induction,valve,events,suppressed,peak,rms,length_m,temperature_k,sha256\n");
            foreach(var r in manifest.cases)csv.AppendLine(string.Join(",",new[]{r.id,r.cylinders.ToString(Inv),r.fuel,r.induction,r.valve,r.accepted.ToString(Inv),r.suppressed.ToString(Inv),r.peak.ToString("F6",Inv),r.rms.ToString("F6",Inv),r.lengthM.ToString("F3",Inv),r.temperatureK.ToString("F1",Inv),r.sha256}));
            File.WriteAllText(Path.Combine(DirectoryPath,"coverage-matrix.csv"),csv.ToString(),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(DirectoryPath,"verification.txt"),"PASS "+checks+" matrix checks; 34 controlled configurations, 16 chapters each, same generator; no production integration.\n"+SharedBackfirePreviewVerification.Summary,new UTF8Encoding(false));
            UnityEngine.Debug.Log("SHARED BACKFIRE AUDITION PASS");
        }
        public static void BisonVersions()
        {
            checks=0;SharedBackfirePreviewVerification.RunChecks();
            string folder=Path.Combine(Root,"reports/bison-backfire-versions-20261020");System.IO.Directory.CreateDirectory(folder);
            var donor=Configurations().Single(c=>c.id=="30-exhaust-2");var manifest=new Manifest();
            var settings=new[]{
                new PreviewCombustionVoicing{pressureCoupling=3.2,turbulenceCoupling=.12,frontCoupling=.9,frontMilliseconds=1.6,expansionScale=1},
                new PreviewCombustionVoicing{pressureCoupling=5,turbulenceCoupling=.10,frontCoupling=1.5,frontMilliseconds=2.3,expansionScale=1.15},
                new PreviewCombustionVoicing{pressureCoupling=7,turbulenceCoupling=.08,frontCoupling=.65,frontMilliseconds=4,expansionScale=1.6},
                new PreviewCombustionVoicing{pressureCoupling=4.5,turbulenceCoupling=.38,frontCoupling=2,frontMilliseconds=.85,expansionScale=1.1},
                new PreviewCombustionVoicing{pressureCoupling=5.5,turbulenceCoupling=.22,frontCoupling=1.1,frontMilliseconds=3,expansionScale=2.1}
            };
            var names=new[]{"01-pressure-pop","02-heavy-bang","03-deep-boom","04-raspy-bang","05-rolling-thump"};
            for(int i=0;i<settings.Length;i++){
                var c=new Config{id=names[i],title="Bison chambered: "+names[i],source=donor.source,spec=donor.spec.Copy(),tune=donor.tune.Copy()};
                manifest.cases.Add(Render(c,settings[i],folder));UnityEngine.Debug.Log("BISON VERSION "+(i+1)+" / 5");
            }
            // Default source remains the original reference, including every limiter and intake event.
            donor.id="reference-default";var reference=Render(donor,null,folder);
            string previous=Path.Combine(DirectoryPath,"30-exhaust-2.wav");
            if(File.Exists(previous))Check(File.ReadAllBytes(previous).SequenceEqual(File.ReadAllBytes(Path.Combine(folder,reference.file))),"default render stays PCM/byte-identical to original Bison");
            File.WriteAllText(Path.Combine(folder,"manifest.json"),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder,"verification.txt"),"PASS "+checks+" Bison sequence/reference checks; five complete 64-second versions; no HTML or runtime changes.\n"+SharedBackfirePreviewVerification.Summary,new UTF8Encoding(false));
            UnityEngine.Debug.Log("BISON FIVE VERSIONS PASS");
        }
        // Refresh input receipts independently of PCM; refuses to alter event identity, timing or energy.
        public static void ExportInputs()
        {
            SharedBackfirePreviewVerification.RunChecks();
            string file=Path.Combine(DirectoryPath,"manifest.json");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(file,Encoding.UTF8));
            var configs=Configurations().ToDictionary(c=>c.id,StringComparer.Ordinal);
            Check(manifest.cases.Count==configs.Count,"complete PCM manifest required");
            foreach(var result in manifest.cases){
                var c=configs[result.id];var profile=new EngineSoundProfile(c.spec,c.tune);var expected=new Result();
                var events=Events(profile,Exhaust(c,profile),expected);
                Check(events.Count==result.events.Count,result.id+" unchanged event count");
                for(int i=0;i<events.Count;i++){
                    var ev=events[i];var receipt=result.events[i];
                    Check(Math.Abs(ev.tick/120.0-receipt.time)<1e-8&&receipt.label==ev.label&&Math.Abs(ev.input.EnergyJ-receipt.energyJ)<1e-6,result.id+" unchanged event identity and energy");
                    receipt.input=ev.input;
                }
            }
            File.WriteAllText(file,JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));
            string report=Path.Combine(DirectoryPath,"verification.txt");
            var lines=File.ReadAllLines(report,Encoding.UTF8);
            File.WriteAllText(report,lines[0]+"\n"+SharedBackfirePreviewVerification.Summary+"PASS exact event-input export checked against recorded event identities/times/energies; PCM files not rewritten.\n",new UTF8Encoding(false));
            UnityEngine.Debug.Log("SHARED BACKFIRE EVENT INPUTS PASS");
        }
    }
}
