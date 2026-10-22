using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Editor
{
    // Prepare real fixed-tick inputs; optionally render them through the production DSP.
    // No invented RPM/boost/cut/release events, career assets or playback preferences.
    public static class RaceAudioAuditionVerification
    {
        const int Rate=48000, FramesPerTick=400;
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        [Serializable] public sealed class Cue {public double time;public string label;}
        [Serializable] public sealed class Point {public double time,rpm,speed,boost;public int gear;}
        [Serializable] public sealed class Result
        {
            public string id,title,source,file,layout,fuel,induction,valve,exhaust,limiter,cut,launchLimiter,launchCut;
            public RaceAudioCutCounts cuts=new RaceAudioCutCounts();
            public double minSlide=10000,maxSlide;
            public int cylinders,shifts,limiterTicks,releases,pops;
            public double duration,elapsed,reaction,peakRpm,peakBoost,peak,rms,idleRms,revRms,shutdownRms;
            public bool laboratory,finished;
            public List<Cue> cues=new List<Cue>();public List<Point> points=new List<Point>();
        }
        [Serializable] public sealed class Manifest {public int sampleRate=Rate,channels=2;public string generatedUtc;public List<Result> cases=new List<Result>();}
        sealed class Case {public string id,title,source;public VehicleSpec spec;public Tune tune;public bool laboratory,tireTest,burnout,singleWheel,variableBurnout,requirePops,requireLaunch;public string tireProduct;public PartInstance[] components;}
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException("RACE AUDIO: "+message);}
        static void Mark(Result r,double time,string label){r.cues.Add(new Cue{time=Math.Round(time,3),label=label});}
        // Reuse versioned acoustic hardware definitions without rendering or touching generated audition fixtures.
        internal static RaceAudioTestFixture[] AcousticHardware(bool prepareTires=false)=>Cases(prepareTires).Select(c=>new RaceAudioTestFixture{id=c.id,title=c.title,spec=c.spec.Copy(),tune=c.tune.Copy(),components=c.components}).ToArray();
        internal static RaceAudioTestFixture CaptureBackfireComparison(string directory)
        {
            Require(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-audio-wav")>=0,"Comparison requires production WAV capture");
            var c=Cases(false).Single(x=>x.id=="26-launch-hard-ignition");
            c.id="tempest-turbo-open";c.title="Tempest turbo: free-flow exhaust, ignition cuts, two-step and anti-lag";c.laboratory=true;
            c.tune.exhaustLevel=3;c.tune.limiterCut=CutMethod.Ignition;c.tune.antiLag=EquipmentSetting.On;
            c.spec.sound.exhaustSystem=new ExhaustSetup{version=1,parts=new[]{ExhaustPart.Create(ExhaustPartKind.Pipe)}};
            Render(c,directory);
            return JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(Path.Combine(directory,c.id+".race.json"),Encoding.UTF8));
        }
        internal static Result[] CaptureV8Matrix(string directory)
        {
            Require(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-audio-wav")>=0,"V8 matrix requires production capture");
            var ids=new[]{"01-i3-na","19-injection-soft-fuel","07-i6-carb","30-exhaust-2","31-exhaust-3","09-v8-supercharger","26-launch-hard-ignition","14-i4-twincharged-dct","12-v12-twin","23-diesel-soft-fuel"};
            var cases=Cases(false);var results=new List<Result>();
            foreach(string id in ids){
                var c=cases.Single(x=>x.id==id);
                if(id=="01-i3-na"||id=="12-v12-twin"){
                    c.laboratory=true;c.requirePops=true;c.tune.limiterCut=CutMethod.Ignition;c.title+=" · ignition-cut laboratory control";
                }
                if(id=="26-launch-hard-ignition"){
                    c.laboratory=true;c.tune.exhaustLevel=3;c.tune.antiLag=EquipmentSetting.On;
                    c.spec.sound.exhaustSystem=new ExhaustSetup{version=1,parts=new[]{ExhaustPart.Create(ExhaustPartKind.Pipe)}};
                }
                results.Add(Render(c,directory));Debug.Log("V8 PHYSICAL CAPTURE "+results.Count+" / "+ids.Length+" "+id);
            }
            return results.ToArray();
        }
        static List<Case> Cases(bool prepareTires=true)
        {
            var cars=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");var list=new List<Case>();
            Action<string,string,string,Action<Case>> add=(id,title,source,configure)=>{
                var car=cars.Single(v=>v.name==source);var c=new Case{id=id,title=title,source=car.spec.displayName,spec=car.spec.Copy(),tune=car.CreateStockTune()};
                // Explicit laboratory control/traction hardware, not player purchases.
                c.tune.controlLevel=1;c.tune.limiter=LimiterStyle.Hard;c.tune.revLimitRpm=car.spec.redlineRpm-200;
                c.tune.tireLevel=2;c.tune.gearboxLevel=2;configure?.Invoke(c);c.tune.ClampTo(c.spec);if(prepareTires)PrepareTires(c);list.Add(c);
            };
            add("01-i3-na","Sprig · inline 3 · NA","CityThree",null);
            add("02-i4-na","Juniper · inline 4 · NA","StarterHatch",null);
            add("03-i4-turbo-hiss","Tempest · inline 4 turbo · hiss BOV","RallySedan",c=>c.tune.blowOffValve=BlowOffType.Hiss);
            add("04-i4-twin-chirp","Tempest · inline 4 twin turbo · chirp BOV","RallySedan",c=>{c.laboratory=true;c.tune.engineLevel=1;c.tune.inductionUpgrade=EngineUpgradeInduction.TwinTurbo;c.tune.blowOffValve=BlowOffType.Chirp;c.tune.exhaustLevel=1;});
            add("05-i5-turbo-flutter","Laboratory inline 5 turbo · flutter BOV","RallySedan",c=>{c.laboratory=true;Cylinders(c,5,EngineLayout.Inline,2.5);c.tune.blowOffValve=BlowOffType.Flutter;});
            add("06-i6-na","Meridian · inline 6 · NA","TouringSix",null);
            add("07-i6-carb","Timber · inline 6 · carburetor","UtilityPickup",c=>c.tune.exhaustLevel=1);
            add("08-v8-carb","Bison 5.0 · cross-plane V8 · carburetor","V8Muscle",c=>c.tune.exhaustLevel=2);
            add("09-v8-supercharger","Bison R · V8 supercharger · soft limiter","SuperV8",c=>c.tune.limiter=LimiterStyle.Soft);
            add("10-diesel-turbo","Timber · diesel 4 turbo · recirculated","timber-diesel",c=>c.tune.blowOffValve=BlowOffType.Recirculated);
            add("11-v10-na","Laboratory V10 · NA · open exhaust","V8Muscle",c=>{c.laboratory=true;Cylinders(c,10,EngineLayout.V,5);c.spec.engineBehavior.fueling=FuelSystem.Injection;c.tune.exhaustLevel=3;});
            add("12-v12-twin","Laboratory V12 twin turbo · recirculated","V8Muscle",c=>{c.laboratory=true;Cylinders(c,12,EngineLayout.V,6);c.spec.engineBehavior.fueling=FuelSystem.Injection;c.tune.engineLevel=1;c.tune.inductionUpgrade=EngineUpgradeInduction.TwinTurbo;c.tune.blowOffValve=BlowOffType.Recirculated;});
            add("13-v8-twin-supercharger","Laboratory V8 twin supercharger","SuperV8",c=>{c.laboratory=true;c.tune.engineLevel=1;c.tune.inductionUpgrade=EngineUpgradeInduction.TwinSupercharger;c.tune.exhaustLevel=1;});
            add("14-i4-twincharged-dct","Laboratory inline 4 twincharged · DCT","RallySedan",c=>{c.laboratory=true;c.tune.engineLevel=1;c.tune.inductionUpgrade=EngineUpgradeInduction.Twincharged;c.tune.transmissionUpgrade=TransmissionUpgrade.DCT;c.tune.blowOffValve=BlowOffType.Hiss;});
            add("15-v8-street-wheelspin","Bison · narrow street tires · wheelspin","V8Muscle",c=>{c.tireTest=true;c.tune.tireLevel=0;c.tune.engineLevel=2;c.tune.rearWheelWidth=.8f;});
            add("16-v8-wide-wheelspin","Bison R · wide drag tires · wheelspin","SuperV8",c=>{c.tireTest=true;c.tune.tireLevel=2;c.tune.engineLevel=3;c.tune.inductionUpgrade=EngineUpgradeInduction.TwinSupercharger;c.tune.rearWheelWidth=1.5f;c.tune.rearWheelDiameter=1.1f;});
            add("17-v8-burnout","Bison · street tires · line-lock burnout","V8Muscle",c=>{c.tireTest=c.burnout=true;c.tune.tireLevel=0;});
            add("18-i4-open-diff-single-wheel","Juniper · open diff · front-left wheelspin","StarterHatch",c=>{c.tireTest=c.singleWheel=true;c.tune.tireLevel=0;c.tune.engineLevel=2;c.spec.frontDifferential=new DifferentialSpec{kind=DifferentialKind.Open};c.tune.frontLeftGrip=.18f;});
            add("19-injection-soft-fuel","Juniper · petrol injection · soft fuel cut","StarterHatch",c=>{c.tune.twoStep=EquipmentSetting.Off;c.tune.limiter=LimiterStyle.Soft;c.tune.limiterCut=CutMethod.Fuel;});
            add("20-injection-hard-ignition","Juniper · petrol injection · hard ignition cut","StarterHatch",c=>{c.tune.twoStep=EquipmentSetting.Off;c.tune.limiterCut=CutMethod.Ignition;c.requirePops=true;});
            add("21-injection-soft-ignition","Juniper · petrol injection · soft ignition cut","StarterHatch",c=>{c.tune.twoStep=EquipmentSetting.Off;c.tune.limiter=LimiterStyle.Soft;c.tune.limiterCut=CutMethod.Ignition;});
            add("22-carb-soft-ignition","Bison · carburetor · soft ignition cut","V8Muscle",c=>{c.tune.twoStep=EquipmentSetting.Off;c.tune.limiter=LimiterStyle.Soft;c.tune.limiterCut=CutMethod.Ignition;});
            add("23-diesel-soft-fuel","Timber · diesel injection · soft fuel cut","timber-diesel",c=>{c.tune.twoStep=EquipmentSetting.Off;c.tune.limiter=LimiterStyle.Soft;c.tune.limiterCut=CutMethod.Fuel;});
            add("24-factory-limiter","Juniper · factory limiter hardware","StarterHatch",c=>{c.tune.twoStep=EquipmentSetting.Off;c.tune.limiter=LimiterStyle.Factory;c.tune.limiterCut=CutMethod.Factory;});
            add("25-no-limiter","Bison · no limiter · driver-controlled rev","V8Muscle",c=>{c.tune.twoStep=EquipmentSetting.Off;c.tune.limiter=LimiterStyle.None;});
            add("26-launch-hard-ignition","Tempest · two-step hard ignition cut","RallySedan",c=>{c.requireLaunch=c.requirePops=true;c.tune.twoStep=EquipmentSetting.On;c.tune.twoStepStyle=LimiterStyle.Hard;c.tune.twoStepCut=CutMethod.Ignition;});
            add("27-launch-soft-fuel","Tempest · two-step soft fuel cut","RallySedan",c=>{c.requireLaunch=true;c.tune.twoStep=EquipmentSetting.On;c.tune.twoStepStyle=LimiterStyle.Soft;c.tune.twoStepCut=CutMethod.Fuel;});
            foreach(int stage in Enumerable.Range(0,4)){
                int level=stage;add((28+stage).ToString("00")+"-exhaust-"+stage,"Bison · "+new[]{"stock silenced","street absorptive","sport chambered","open pipe"}[stage]+" exhaust / backfires","V8Muscle",c=>{
                    c.requirePops=true;c.tune.twoStep=EquipmentSetting.Off;c.spec.sound.exhaust=0;c.tune.exhaustLevel=level;
                    c.spec.sound.exhaustSystem=new ExhaustSetup{version=1,parts=level==0?new[]{ExhaustPart.Create(ExhaustPartKind.CatalyticConverter),ExhaustPart.Create(ExhaustPartKind.AbsorptiveSilencer),ExhaustPart.Create(ExhaustPartKind.Resonator)}:level==1?new[]{ExhaustPart.Create(ExhaustPartKind.AbsorptiveSilencer)}:level==2?new[]{ExhaustPart.Create(ExhaustPartKind.ChamberedSilencer)}:new[]{ExhaustPart.Create(ExhaustPartKind.Pipe)}};
                });
            }
            int number=32;
            foreach(var type in new[]{EngineUpgradeInduction.Turbo,EngineUpgradeInduction.TwinTurbo,EngineUpgradeInduction.Twincharged})foreach(var valve in new[]{BlowOffType.Recirculated,BlowOffType.Hiss,BlowOffType.Chirp,BlowOffType.Flutter}){
                var hardware=type;var release=valve;
                add((number++).ToString("00")+"-boost-"+type+"-"+valve,"Tempest · "+type+" · "+valve+" release","RallySedan",c=>{c.laboratory=true;c.tune.engineLevel=1;c.tune.inductionUpgrade=hardware;c.tune.blowOffValve=release;c.tune.twoStep=EquipmentSetting.Off;});
            }
            add("44-tires-street-ramp","Bison · narrow street tires · variable burnout","V8Muscle",c=>{c.tireTest=c.variableBurnout=true;c.tune.tireLevel=0;c.tune.rearWheelWidth=.75f;});
            add("45-tires-radial-ramp","Bison R · pro radial tires · variable burnout","SuperV8",c=>{c.tireTest=c.variableBurnout=true;c.tireProduct="ProRadial";c.tune.engineLevel=3;c.tune.inductionUpgrade=EngineUpgradeInduction.TwinSupercharger;c.tune.rearWheelWidth=1.2f;});
            add("46-tires-slick-ramp","Laboratory Bison R 7.5 · soft slicks · variable burnout","SuperV8",c=>{c.laboratory=c.tireTest=c.variableBurnout=true;Cylinders(c,8,EngineLayout.Crossplane,7.5);c.tireProduct="SlickSoft";c.tune.engineLevel=3;c.tune.inductionUpgrade=EngineUpgradeInduction.TwinSupercharger;c.tune.rearWheelWidth=.85f;c.tune.rearWheelDiameter=.9f;c.tune.finalDrive=4.5f;});
            add("47-tires-fwd-ramp","Laboratory Juniper · front street tires · variable burnout","StarterHatch",c=>{c.laboratory=c.tireTest=c.variableBurnout=true;c.tune.engineLevel=3;c.tune.inductionUpgrade=EngineUpgradeInduction.Turbo;c.tune.tireLevel=0;c.tune.frontWheelWidth=.7f;Cylinders(c,4,EngineLayout.Inline,2.4);});
            return list;
        }
        static void PrepareTires(Case c)
        {
            if(c.tireProduct==null)return;
            var catalog=new MechanicalCatalog(new[]{c.spec});var d=catalog.parts.Single(p=>p.category==PartCategory.Tires&&p.id.EndsWith("/"+c.tireProduct,StringComparison.Ordinal));
            var baseline=new CarSimulation(c.spec,c.tune,Driver());c.components=baseline.mechanics.parts.Select(p=>p.Copy()).ToArray();
            foreach(var slot in new[]{PartCategory.FuelSystem,PartCategory.Ignition}){
                var supply=c.components.SingleOrDefault(p=>p.slot==slot.ToString());var upgrade=catalog.Find(catalog.Id(c.spec.id,slot,3));
                if(supply!=null&&upgrade!=null)supply.definitionId=upgrade.id;
            }
            var pack=c.components.Single(p=>p.slot=="Tires");pack.definitionId=d.id;foreach(var t in pack.condition.tires)t.definitionId=d.id;
            c.spec.cornerTireDefinitions=Enumerable.Range(0,4).Select(i=>d.Copy()).ToArray();
        }
        static void Cylinders(Case c,int count,EngineLayout layout,double litres)
        {
            double factor=litres/c.spec.DisplacementLitres;c.spec.sound.cylinders=count;c.spec.sound.layout=layout;
            c.spec.displacementLitres=(float)litres;c.spec.sound.displacementLitres=(float)litres;
            for(int i=0;i<c.spec.torque.Length;i++)c.spec.torque[i].torqueNm*=(float)factor;
        }
        static DriverSettings Driver()=>new DriverSettings{shifting=ShiftMode.Sequential,launch=LaunchMode.Automatic,
            throttleAssist=false,tractionAssist=true,revMatchAssist=true,wearMode=WearMode.None,fuelUsage=FuelUsageMode.None,
            fluidDepletion=ConsumptionMode.None,fluidAging=ConsumptionMode.None,optimalThermals=true,shutdownBrakeAssist=true};
        static void Header(BinaryWriter w,int samples)
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+samples*2);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)2);w.Write(Rate);w.Write(Rate*4);w.Write((short)4);w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));w.Write(samples*2);
        }
        static Result Render(Case c,string directory)
        {
            string authored=JsonUtility.ToJson(c.spec);var driver=Driver();driver.tractionAssist=!c.tireTest;
            var fixture=new RaceAudioTestFixture{id=c.id,title=c.title,spec=c.spec,tune=c.tune,driver=driver,tireTest=c.tireTest,components=c.components};
            // Exercise the same JSON boundary as the standalone player, including optional arrays.
            fixture=JsonUtility.FromJson<RaceAudioTestFixture>(JsonUtility.ToJson(fixture));
            var race=fixture.CreateRace();
            var car=race.player;var profile=new EngineSoundProfile(car.spec,car.tune);
            var controller=new EngineSoundController(car.spec,car.tune);var synth=new EngineSoundSynth(profile,sampleRate:Rate);
            fixture.configuration=(car.engine.data.fueling==FuelSystem.Diesel?"Diesel injection":car.engine.data.fueling==FuelSystem.Carburetor?"Petrol carburetor":"Petrol injection")+" · "+(car.engine.limiter==LimiterStyle.None?"No limiter (driver throttle)":car.engine.limiter+" / "+car.engine.cut+" cut")+(car.engine.twoStep?" · launch "+car.engine.launchLimiter+" / "+car.engine.launchCut:"");
            bool capture=Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-audio-wav")>=0;
            var r=new Result{id=c.id,title=c.title,source=c.source,file=capture?c.id+".wav":null,laboratory=c.laboratory,cylinders=profile.cylinders,
                layout=profile.layout.ToString(),fuel=profile.fueling.ToString(),induction=profile.induction.ToString(),valve=profile.valve.ToString(),exhaust="Stage "+profile.exhaust,limiter=car.engine.limiter.ToString(),cut=car.engine.cut.ToString(),launchLimiter=car.engine.launchLimiter.ToString(),launchCut=car.engine.launchCut.ToString()};
            var pcm=new float[FramesPerTick*2];var tirePcm=new float[FramesPerTick*2];var tireSynth=new TireSoundSynth();var tireProfile=new TireSoundProfile(car.spec,car.tune);double tireEnergy=0;int frictionTicks=0,singleWheelTicks=0;double now=0,energy=0,liftStart=-1,finishedAt=-1,lastShift=-10,loadedSeconds=0;
            int samples=0,lastGear=0,lastRelease=0,lastPop=0,idleSamples=0,revSamples=0,shutdownSamples=0;double idleEnergy=0,revEnergy=0,shutdownEnergy=0;bool didLift=false,limited=false;string stage="";
            using(Stream file=capture?(Stream)new FileStream(Path.Combine(directory,r.file),FileMode.Create,FileAccess.Write):Stream.Null)using(var writer=new BinaryWriter(file)){
                if(capture)Header(writer,0);
                for(int tick=0;tick<Rate/FramesPerTick*95;tick++){
                    var input=new DriverInput{brake=race.warmup?1:0};string next;bool beginCountdown=false;
                    if(c.variableBurnout&&now<2){next="Idle before tire speed sweep";input.throttle=0;}
                    else if(c.variableBurnout&&now<10.5){next=now<4?"Slow wheelspin":now<6?"Faster wheelspin":now<7.5?"Ease gas / slow wheels":now<9.5?"Full gas / fastest wheels":"Ease gas again";car.state.warmupLineLock=true;input.shiftUp=race.warmupNeutral;input.throttle=now<4?.4:now<6?.85:now<7.5?.12:now<9.5?1:.2;}
                    else if(c.variableBurnout&&race.warmup&&now<15){next="Stop burnout / regain grip";car.state.warmupLineLock=false;input.throttle=0;input.shiftDown=car.state.gear>0;}
                    else if(c.burnout&&now<7){next="Line-lock burnout";car.state.warmupLineLock=true;input.throttle=now<5?1:0;input.shiftUp=tick==0;}
                    else if(c.burnout&&race.warmup&&now<11){next="Stop burnout / regain grip";car.state.warmupLineLock=false;input.throttle=0;input.shiftDown=car.state.gear>0;}
                    else if(now<2){next="Idle";input.throttle=0;}
                    else if(now<5){next="Throttle blips";double t=now-2;input.throttle=t<.85?.60:t<1.45?0:t<2.25?.85:0;}
                    else if(now<9){next=car.engine.limiter==LimiterStyle.None?"Unrestricted rev / driver throttle":"Neutral rev / limiter";input.throttle=car.engine.limiter==LimiterStyle.None?DragMath.Clamp((car.spec.redlineRpm-250-car.state.rpm)/500,0,.7):1;}
                    else if(race.warmup&&now<10.8){next="Lift / return to idle";input.throttle=0;}
                    else if(race.warmup){Require(race.BeginCountdown(),c.id+" cannot stage after warm-up");beginCountdown=true;next="Countdown";}
                    else if(race.player.state.finished){next="Finish / shutdown";if(finishedAt<0)finishedAt=now;}
                    else if(!car.state.launched){next="Countdown";input.throttle=c.requireLaunch?1:race.time>2.3?.55:0;}
                    else{
                        next="Acceleration / shifting";input.throttle=1;
                        if(car.state.turboBoostBar>.12)loadedSeconds+=DragMath.FixedStep;
                        if(!didLift&&car.state.gear>=2&&car.state.distance>50&&(profile.turboCount==0||loadedSeconds>.5)){
                            didLift=true;liftStart=now;Mark(r,now,"Loaded throttle lift");
                        }
                        if(liftStart>=0&&now-liftStart<.65){input.throttle=0;next="Loaded lift / pressure release";}
                        else if(car.state.gear<car.spec.gears.Length&&car.state.rpm>=Math.Min(car.tune.shiftRpm,car.ShiftPoint(car.state.gear))&&car.state.shiftRemaining<=0&&now-lastShift>.4){input.shiftUp=true;lastShift=now;}
                    }
                    fixture.ticks.Add(new RaceAudioTestTick{input=input,beginCountdown=beginCountdown,lineLock=car.state.warmupLineLock,stage=next!=stage?next:null});
                    if(next!=stage){stage=next;Mark(r,now,next);}
                    if(race.phase==RacePhase.Finished)race.Shutdown(input);else race.Step(input);
                    var s=car.state;if(s.limiter){r.limiterTicks++;if(!limited){limited=true;Mark(r,now,"Actual limiter intervention");}}
                    if(s.launched&&s.gear!=lastGear){if(lastGear>0){r.shifts++;Mark(r,now,"Shift "+lastGear+" → "+s.gear);}else Mark(r,now,"Launch");lastGear=s.gear;}
                    if(s.boostReleaseCount!=lastRelease){r.releases+=s.boostReleaseCount-lastRelease;lastRelease=s.boostReleaseCount;Mark(r,now,"Actual pressure-release event");}
                    if(s.exhaustPopCount!=lastPop){r.pops+=s.exhaustPopCount-lastPop;lastPop=s.exhaustPopCount;Mark(r,now,"Actual exhaust pop / backfire");}
                    r.peakRpm=Math.Max(r.peakRpm,s.rpm);r.peakBoost=Math.Max(r.peakBoost,s.boostBar);
                    if(tick%12==0)r.points.Add(new Point{time=Math.Round(now,3),rpm=s.rpm,speed=s.speed*3.6,boost=s.boostBar,gear=s.gear});
                    var tireFrame=TireSoundFrame.From(s,true,1,car.spec.drive,tireProfile);
                    if(capture){tireSynth.Fill(tirePcm,2,Rate,tireFrame);foreach(var value in tirePcm)tireEnergy+=value*value;}
                    if(c.variableBurnout&&now>=2&&now<10.5)for(int w=0;w<4;w++)if(tireFrame.Wheel(w).intensity>.1){r.minSlide=Math.Min(r.minSlide,tireFrame.Wheel(w).slidingSpeed);r.maxSlide=Math.Max(r.maxSlide,tireFrame.Wheel(w).slidingSpeed);}
                    if(Enumerable.Range(0,4).Any(w=>tireFrame.Wheel(w).intensity>.15))frictionTicks++;
                    if(tireFrame.fl.intensity>.15&&tireFrame.fr.intensity==0&&tireFrame.rl.intensity==0&&tireFrame.rr.intensity==0)singleWheelTicks++;
                    // The rival is simulated but has no controller/synth in this recording.
                    var soundFrame=controller.Update(s,true,.85,DragMath.FixedStep);
                    if(r.cuts.Observe(car))Require(soundFrame.cutMethod==s.activeCutMethod&&soundFrame.combustionFraction==s.combustionFraction,c.id+" limiter cut lost in audio publication");
                    if(capture)synth.Fill(pcm,2,Rate,soundFrame);
                    if(capture)foreach(float v in pcm){Require(!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<1,c.id+" invalid PCM");r.peak=Math.Max(r.peak,Math.Abs(v));energy+=v*v;
                        if(now<2){idleEnergy+=v*v;idleSamples++;}else if(now<9){revEnergy+=v*v;revSamples++;}else if(finishedAt>=0){shutdownEnergy+=v*v;shutdownSamples++;}
                        writer.Write((short)Math.Round(v*32767));}
                    samples+=pcm.Length;now+=DragMath.FixedStep;
                    Require(!s.engineFailed&&!s.drivetrainFailed&&!s.stalled,c.id+" failed/stalled during audition / "+stage);
                    if(finishedAt>=0&&now-finishedAt>=6)break;
                }
                if(capture){writer.Seek(0,SeekOrigin.Begin);Header(writer,samples);}
            }
            r.duration=now;r.rms=Math.Sqrt(energy/samples);r.finished=car.state.finished;r.elapsed=car.state.elapsed;r.reaction=car.state.reaction;
            r.idleRms=Math.Sqrt(idleEnergy/Math.Max(1,idleSamples));r.revRms=Math.Sqrt(revEnergy/Math.Max(1,revSamples));r.shutdownRms=Math.Sqrt(shutdownEnergy/Math.Max(1,shutdownSamples));
            if(capture)FadeBoundaries(Path.Combine(directory,r.file));
            Require(r.finished,c.id+" did not finish the real quarter-mile");if(!c.burnout&&!c.variableBurnout&&car.engine.limiter==LimiterStyle.Hard)Require(r.limiterTicks>3,c.id+" never reached its physical limiter");
            Require(r.shifts>=2,c.id+" needs at least two physical shifts");Require(didLift,c.id+" missed loaded lift");
            if(profile.turboCount>0){Require(r.peakBoost>.12,c.id+" produced no meaningful boost");if(profile.fueling!=FuelSystem.Diesel)Require(r.releases>0,c.id+" produced no actual BOV/recirculation event");}
            if(profile.turboCount==0)Require(r.releases==0,c.id+" invented a non-turbo BOV event");
            if(profile.fueling==FuelSystem.Diesel)Require(r.releases==0,c.id+" invented a petrol-style diesel BOV event");
            if(capture)Require(r.idleRms>1e-5&&r.revRms>1e-5&&r.shutdownRms>1e-5,c.id+" missing audible coverage / idle "+r.idleRms+" / rev "+r.revRms+" / shutdown "+r.shutdownRms);
            if(capture)Require(r.rms>1e-5,c.id+" silent recording");Require(JsonUtility.ToJson(c.spec)==authored,c.id+" modified source configuration");
            if(c.tireTest){Debug.Log(c.id+" tire ticks "+frictionTicks+" energy "+tireEnergy);Require(frictionTicks>60&&(!capture||tireEnergy>1),c.id+" lacks sustained audible tire contact");}
            if(c.singleWheel){Require(singleWheelTicks>60,c.id+" lacks actual isolated wheelspin");Debug.Log(c.id+" single-wheel ticks "+singleWheelTicks);}
            if(car.engine.limiter==LimiterStyle.None)Require(r.cuts.fuel+r.cuts.ignition==0,c.id+" has an invented limiter cut");
            else if(!c.variableBurnout&&!c.burnout)Require((car.engine.cut==CutMethod.Fuel?r.cuts.fuel:r.cuts.ignition)>3,c.id+" missing expected main cut method");
            if(car.engine.limiter==LimiterStyle.Soft)Require(r.cuts.softPartial>3,c.id+" missing partial soft limiting");
            if(c.requireLaunch)Require((car.engine.launchCut==CutMethod.Fuel?r.cuts.launchFuel:r.cuts.launchIgnition)>3,c.id+" missing actual launch cut");
            if(c.requirePops)Require(r.pops>1,c.id+" missing actual exhaust backfire/pop events");
            if(c.variableBurnout)Require(r.maxSlide-r.minSlide>5,c.id+" lacks meaningful actual wheel-speed variation: "+r.minSlide+" to "+r.maxSlide);
            fixture.elapsed=r.elapsed;fixture.shifts=r.shifts;fixture.limiterTicks=r.limiterTicks;fixture.releases=r.releases;fixture.pops=r.pops;fixture.cuts=r.cuts;
            File.WriteAllText(Path.Combine(directory,c.id+".race.json"),JsonUtility.ToJson(fixture),new UTF8Encoding(false));
            race.CloseOpponentWorker();return r;
        }
        static void FadeBoundaries(string path)
        {
            // File boundaries only: retain the full six-second post-finish engine,
            // then fade the audition clip edge rather than cutting a nonzero sample.
            using(var file=new FileStream(path,FileMode.Open,FileAccess.ReadWrite)){
                foreach(bool end in new[]{false,true}){
                    int frames=(int)(Rate*(end?.08:.02)),bytes=frames*4;long offset=end?file.Length-bytes:44;file.Position=offset;
                    var data=new byte[bytes];int read=0;while(read<bytes){int n=file.Read(data,read,bytes-read);if(n==0)throw new EndOfStreamException();read+=n;}
                    for(int i=0;i<frames;i++)for(int ch=0;ch<2;ch++){
                        int at=i*4+ch*2;short sample=(short)(data[at]|data[at+1]<<8);double gain=(end?frames-1-i:i)/(double)(frames-1);
                        short value=(short)Math.Round(sample*gain);data[at]=(byte)value;data[at+1]=(byte)(value>>8);
                    }
                    file.Position=offset;file.Write(data,0,data.Length);
                }
            }
        }
        static void WriteCoverage(Manifest m,string directory,bool complete)
        {
            if(complete){
                foreach(var key in new[]{"Injection/Hard/Fuel","Injection/Soft/Fuel","Injection/Hard/Ignition","Injection/Soft/Ignition","Carburetor/Hard/Ignition","Carburetor/Soft/Ignition","Diesel/Hard/Fuel","Diesel/Soft/Fuel"})
                    Require(m.cases.Any(r=>r.fuel+"/"+r.limiter+"/"+r.cut==key&&(r.cuts.fuel+r.cuts.ignition)>3),"Missing limiter/fuel-system coverage "+key);
                foreach(InductionType type in Enum.GetValues(typeof(InductionType)))Require(m.cases.Any(r=>r.induction==type.ToString()),"Missing induction "+type);
                foreach(var type in new[]{InductionType.Turbo,InductionType.TwinTurbo,InductionType.Twincharged})foreach(var valve in new[]{BlowOffType.Recirculated,BlowOffType.Hiss,BlowOffType.Chirp,BlowOffType.Flutter})
                    Require(m.cases.Any(r=>r.induction==type.ToString()&&r.valve==valve.ToString()&&r.releases>0),"Missing real release "+type+" / "+valve);
                for(int i=0;i<4;i++)Require(m.cases.Any(r=>r.id.StartsWith((28+i).ToString("00")+"-exhaust",StringComparison.Ordinal)&&r.pops>1&&r.exhaust=="Stage "+i),"Missing matched exhaust/backfires "+i);
                Require(m.cases.Any(r=>r.limiter=="None"&&r.cuts.fuel+r.cuts.ignition==0),"Missing unprotected rev example");
                Require(m.cases.Count(r=>r.id.Contains("-ramp")&&r.maxSlide-r.minSlide>5)>=4,"Missing variable-speed tire examples");
            }
            var csv=new StringBuilder("case,fuel_system,limiter,cut,launch_limiter,launch_cut,main_fuel_ticks,main_ignition_ticks,launch_fuel_ticks,launch_ignition_ticks,soft_partial_ticks,induction,valve,release_events,exhaust,pop_events,slide_min_mps,slide_max_mps,seconds\n");
            foreach(var r in m.cases)csv.AppendLine(string.Join(",",new[]{r.id,r.fuel,r.limiter,r.cut,r.launchLimiter,r.launchCut,r.cuts.fuel.ToString(Inv),r.cuts.ignition.ToString(Inv),r.cuts.launchFuel.ToString(Inv),r.cuts.launchIgnition.ToString(Inv),r.cuts.softPartial.ToString(Inv),r.induction,r.valve,r.releases.ToString(Inv),r.exhaust,r.pops.ToString(Inv),(r.maxSlide>0?r.minSlide:0).ToString("F2",Inv),r.maxSlide.ToString("F2",Inv),r.duration.ToString("F3",Inv)}));
            File.WriteAllText(Path.Combine(directory,"coverage-matrix.csv"),csv.ToString(),new UTF8Encoding(false));
        }
        public static void PrepareInputs(){CheckTires();Run();}
        public static void PrepareLive(){PrepareInputs();PlayerBuild.BuildPlayerOnly();}
        public static void CheckTires(){TireSoundVerification.Run();SharedAudioVerification.Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../..")));}
        public static void Run()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../..")),directory=Path.Combine(root,"reports/race-audio-audition");Directory.CreateDirectory(directory);
            var manifest=new Manifest{generatedUtc=DateTime.UtcNow.ToString("O",Inv)};var report=new StringBuilder("Deterministic live-race input generation / actual cuts, backfires, releases and tire contact / optional --draglab-audio-wav capture\n");
            string rng=JsonUtility.ToJson(UnityEngine.Random.state);
            try{
                var cases=Cases();var args=Environment.GetCommandLineArgs();int only=Array.IndexOf(args,"--draglab-audio-case");if(only>=0)cases=cases.Where(c=>args[only+1].Split(',').Any(prefix=>c.id.StartsWith(prefix,StringComparison.Ordinal))).ToList();Require(cases.Count>0,"No matching configurations");foreach(var c in cases){
                    var r=Render(c,directory);manifest.cases.Add(r);report.AppendLine("PASS "+r.id+" / "+r.duration.ToString("F2",Inv)+" s / limiter ticks "+r.limiterTicks+" / shifts "+r.shifts+" / releases "+r.releases+" / peak boost "+r.peakBoost.ToString("F3",Inv)+" / "+r.fuel+" / "+r.limiter+" "+r.cut+" / normal fuel/ignition ticks "+r.cuts.fuel+"/"+r.cuts.ignition+" / launch fuel/ignition "+r.cuts.launchFuel+"/"+r.cuts.launchIgnition+" / pops "+r.pops+" / tire slide range "+(r.maxSlide>0?r.maxSlide-r.minSlide:0).ToString("F1",Inv));
                    Debug.Log("Race audio audition: "+r.id+" passed");
                }
                var repeatCase=cases.FirstOrDefault(c=>c.id=="03-i4-turbo-hiss");if(repeatCase!=null&&Array.IndexOf(args,"--draglab-audio-wav")>=0){byte[] firstHash;
                using(var hash=System.Security.Cryptography.SHA256.Create())firstHash=hash.ComputeHash(File.ReadAllBytes(Path.Combine(directory,repeatCase.id+".wav")));
                Render(repeatCase,directory);
                using(var hash=System.Security.Cryptography.SHA256.Create())Require(firstHash.SequenceEqual(hash.ComputeHash(File.ReadAllBytes(Path.Combine(directory,repeatCase.id+".wav")))),"same configuration and private audio seed did not reproduce exact WAV bytes");
                report.AppendLine("PASS exact WAV repeatability for a complete turbo race, including lift, limiter and shifts.");}
                Require(JsonUtility.ToJson(UnityEngine.Random.state)==rng,"audition modified Unity global RNG");
                WriteCoverage(manifest,directory,only<0);
                File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));
                report.AppendLine("PASS: "+manifest.cases.Count+" complete real races; no rival voice constructed; no player saves/preferences accessed.");
            }catch(Exception e){report.AppendLine("FAIL: "+e);throw;}
            finally{File.WriteAllText(Path.Combine(directory,"verification.txt"),report.ToString(),new UTF8Encoding(false));}
        }
    }
}
