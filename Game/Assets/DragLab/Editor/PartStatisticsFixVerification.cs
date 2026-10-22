using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    // Isolated models only: never opens the player's save or changes audio tuning.
    public static class PartStatisticsFixVerification
    {
        static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/part-statistics-fixes-20261021"));
        static int checks;
        static readonly StringBuilder log = new StringBuilder();
        [Serializable] sealed class JournalData { public int version=1; public long baseRevision; public RunCheckpoint checkpoint; }
        [Serializable] sealed class JournalEnvelope { public string payload,sha256; }
        static void Check(bool ok,string label) { if(!ok)throw new InvalidOperationException(label);checks++;log.AppendLine("PASS "+label); }
        static void Near(double a,double b,double tolerance,string label) => Check(Math.Abs(a-b)<=tolerance,label+": "+a+" / "+b);
        static CarSimulation Car(BuildResolution b) => new CarSimulation(b.spec,b.tune,new DriverSettings{wearMode=WearMode.Full,throttleAssist=false,tractionAssist=false});
        static double Rev(BuildResolution b) {
            var car=Car(b);car.state.rpm=1500;
            for(int i=0;i<24;i++)car.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=1},true,false);
            return car.state.rpm;
        }
        public static void Run()
        {
            Directory.CreateDirectory(Folder);checks=0;log.Clear();
            try {
                var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
                foreach(var v in vehicles) {
                    string id=v.spec.id;var catalog=new MechanicalCatalog(new[]{v.spec});var map=catalog.Recipe(id);var tune=v.CreateStockTune();
                    var stock=catalog.Resolve(id,map,tune);var baseline=Car(stock);
                    var fly=catalog.Find(map[PartCategory.Flywheel]);var original=fly.Copy();
                    fly.massKg+=2;var heavier=catalog.Resolve(id,map,tune);
                    Near(Car(heavier).mass.TranslatingKg-baseline.mass.TranslatingKg,2,1e-5,id+" absolute mass changes gravitational load exactly once");
                    Near(FlywheelDynamics.Inertia(heavier.spec,heavier.tune),FlywheelDynamics.Inertia(stock.spec,stock.tune),1e-10,id+" independent mass edit preserves inertia");
                    fly.massKg=original.massKg;fly.inertiaKgM2*=1.5f;var slower=catalog.Resolve(id,map,tune);var energy=Car(slower);energy.state.rpm=3000;
                    FlywheelDynamics.Publish(energy.spec,energy.tune,energy.state);
                    Near(energy.mass.TranslatingKg,baseline.mass.TranslatingKg,1e-6,id+" independent inertia edit preserves mass");
                    Near(energy.state.flywheelEnergyJ,.5*fly.inertiaKgM2*Math.Pow(3000/FlywheelDynamics.RpmPerRadian,2),1e-7,id+" actual stored rotational energy follows rating");
                    Check(Rev(slower)<Rev(stock)-1,id+" higher inertia slows actual neutral rev response");
                    var copied=slower.spec.Copy();Near(FlywheelDynamics.Inertia(copied,slower.tune),fly.inertiaKgM2,1e-10,id+" copied snapshot rebuilds lookup");
                    copied.fittedParts=Array.FindAll(copied.fittedParts,p=>p.category!=PartCategory.Flywheel);
                    Near(FlywheelDynamics.Mass(copied,slower.tune),0,0,id+" missing flywheel has no mass");Near(FlywheelDynamics.Inertia(copied,slower.tune),0,0,id+" missing flywheel has no inertia");
                    var serialized=JsonUtility.FromJson<VehicleSpec>(JsonUtility.ToJson(slower.spec));
                    Near(FlywheelDynamics.Inertia(serialized,slower.tune),fly.inertiaKgM2,1e-8,id+" serialized explicit rating survives");
                    fly.massKg=fly.inertiaKgM2=0;fly.massScale=.8f;var legacy=catalog.Resolve(id,map,tune);
                    Near(FlywheelDynamics.Mass(legacy.spec,legacy.tune),v.spec.flywheel.massKg*legacy.tune.FlywheelMass,1e-8,id+" legacy mass fallback");
                    Near(FlywheelDynamics.Inertia(legacy.spec,legacy.tune),v.spec.flywheel.Inertia*legacy.tune.FlywheelMass,1e-8,id+" legacy inertia fallback");
                    fly.massKg=original.massKg;fly.inertiaKgM2=original.inertiaKgM2;fly.massScale=original.massScale;
                    var block=catalog.Find(map[PartCategory.CylinderBlock]);float stockRpm=block.rpmLimit,stockTorque=block.torqueLimitNm;
                    var reinforced=catalog.Find(catalog.Id(id,PartCategory.CylinderBlock,1));
                    Check(stockRpm>v.spec.redlineRpm&&reinforced.rpmLimit>stockRpm&&reinforced.torqueLimitNm>stockTorque,id+" block reinforcement adds rated headroom");
                    foreach(var category in new[]{PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft,PartCategory.CylinderBlock}) {
                        var d=catalog.Find(map[category]);float old=d.rpmLimit;d.rpmLimit=v.spec.redlineRpm*.85f;
                        var low=catalog.Resolve(id,map,tune);Check(low.spec.Validate()==null,id+" lower rated build remains valid "+category);
                        Near(low.safeRpm,d.rpmLimit,1e-4,id+" resolver uses weakest rating "+category);
                        Near(low.spec.redlineRpm*low.spec.engineBehavior.safeRpmMultiplier,d.rpmLimit,.002,id+" global threshold agrees "+category);
                        var car=Car(low);BuildLimits.Capture(car,out double rpm,out _,out _,out _);Near(rpm,d.rpmLimit,.002,id+" inspection agrees "+category);
                        var state=new CarState{rpm=v.spec.redlineRpm*.9,wearRate=1};
                        EngineBehaviorDynamics.FinishTick(low.spec,new EngineBehaviorProfile(low.spec,low.tune),state,false,.25);
                        Check(state.engineDamage>0,id+" global overrev damage below factory redline "+category);d.rpmLimit=old;
                    }
                    block.torqueLimitNm=stockTorque*.25f;var weak=catalog.Resolve(id,map,tune);var weakCar=Car(weak);
                    BuildLimits.Capture(weakCar,out _,out _,out double strength,out string strengthPart);
                    Near(strength,block.torqueLimitNm,.002,id+" block limits displayed torque");Check(strengthPart==block.name,id+" block names limiting part");
                    // Identical operating state: only fitted strength differs.
                    Action<CarSimulation> load=car=>{car.state.rpm=v.spec.idleRpm+500;car.state.stalled=car.state.engineFailed=false;car.state.throttle=1;car.state.engineTorqueNm=stockTorque*.6;car.state.fluids.oilTemperatureC=90;car.state.lubricationFactor=1;};
                    load(weakCar);load(baseline);ServiceDynamics.Tick(weakCar,1);ServiceDynamics.Tick(baseline,1);
                    var weakPart=weakCar.mechanics.Part(PartCategory.CylinderBlock);var normalPart=baseline.mechanics.Part(PartCategory.CylinderBlock);
                    Check(weakPart.condition.health<normalPart.condition.health-.001,id+" actual block overload damages condition");
                    weakCar.driver.wearMode=WearMode.None;double health=weakPart.condition.health;ServiceDynamics.Tick(weakCar,1);Near(weakPart.condition.health,health,0,id+" wear disabled remains authoritative");
                    block.torqueLimitNm=0;block.rpmLimit=0;var oldBlock=Car(catalog.Resolve(id,map,tune));BuildLimits.Capture(oldBlock,out double legacyRpm,out _,out double legacyTorque,out _);
                    Check(legacyRpm>v.spec.redlineRpm&&legacyTorque>0,id+" unrated legacy block does not invent a zero-strength engine");
                    oldBlock.mechanics.Part(PartCategory.CylinderBlock).condition.failed=true;BuildLimits.Capture(oldBlock,out legacyRpm,out _,out legacyTorque,out _);
                    Check(legacyRpm==0&&legacyTorque==0,id+" failed legacy block has zero usable headroom");
                    block.rpmLimit=stockRpm;block.torqueLimitNm=stockTorque;
                    var fastBlock=Car(catalog.Resolve(id,map,tune));var fastPart=fastBlock.mechanics.Part(PartCategory.CylinderBlock);
                    load(fastBlock);fastBlock.state.rpm=stockRpm*1.2;ServiceDynamics.Tick(fastBlock,1);
                    Check(fastPart.condition.health<normalPart.condition.health-.001&&fastPart.condition.stressExposure>0,id+" block RPM rating drives actual overspeed wear");
                    var capped=stock.spec.Copy();capped.fuelPowerCapacityHp=20;capped.ignitionPowerCapacityHp=15;
                    var profile=new InductionProfile(capped,stock.tune);
                    for(int i=0;i<25;i++) {
                        double rpm=capped.idleRpm+(capped.redlineRpm-capped.idleRpm)*i/24.0;
                        double helper=DragMath.Torque(capped,stock.tune,rpm),physical=InductionDynamics.SteadyOutput(profile,rpm).engineTorqueNm;
                        Near(helper,physical,.02,id+" helper follows physical capacity at "+rpm);
                        Check(helper*rpm/7127<=15.001,id+" weakest delivery rating caps HP");
                    }
                    var snapshot=new RunSnapshot{vehicle=stock.spec,tune=stock.tune,driving=new DriverSettings{wearMode=WearMode.Full}};
                    var session=new PhysicalDrivingSession(snapshot);
                    for(int i=0;i<60;i++)session.Step(new TickInput{tick=i,neutral=true,input=new DriverInput{throttle=i<30?1:0}});
                    var roundtrip=JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.snapshot));var replay=PhysicalDrivingSession.Replay(roundtrip);
                    Check(JsonUtility.ToJson(session.car.CaptureDynamics())==JsonUtility.ToJson(replay.car.CaptureDynamics()),id+" exact serialized physical replay");
                    roundtrip.model="draglab-variety-v21";Check(PhysicalDrivingSession.Validate(roundtrip)?.Contains("Different physics model")==true,id+" obsolete input replay rejected explicitly");
                }
                VerifyLegacyJournals(vehicles);
                VerifyOldShaftFixture(vehicles);
                VerifyZeroCoreRatings(vehicles[0]);
                log.AppendLine("Checks: "+checks+"\nModels: "+vehicles.Length+"\nNo audio source/tuning or player state changed.");
            } catch(Exception e) {log.AppendLine("FAILED "+e);throw;}
            finally {
                File.WriteAllText(Path.Combine(Folder,"focused-verification.log"),log.ToString(),new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(Folder,"focused-verification.txt"),(log.ToString().Contains("FAILED ")?"FAIL\n":"PASS\n")+"Checks: "+checks+"\nFull per-configuration log is local-only.\n",new UTF8Encoding(false));
                Debug.Log("Part stat fixes: "+checks+" checks.");
            }
        }
        static void VerifyZeroCoreRatings(VehicleDefinition vehicle)
        {
            var catalog=new MechanicalCatalog(new[]{vehicle.spec});var stock=catalog.Resolve(vehicle.spec.id,catalog.Recipe(vehicle.spec.id),vehicle.CreateStockTune());
            foreach(var category in new[]{PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft}) {
                var spec=stock.spec.Copy();var d=Array.Find(spec.fittedParts,p=>p.category==category);d.rpmLimit=d.torqueLimitNm=0;
                var car=Car(new BuildResolution{spec=spec,tune=stock.tune});BuildLimits.Capture(car,out double rpm,out _,out double torque,out _);
                Check(rpm==0&&torque==0,"Zero "+category+" ratings remain zero headroom; only legacy blocks may be unrated");
            }
        }
        static void VerifyOldShaftFixture(VehicleDefinition[] vehicles)
        {
            var v=vehicles.Single(x=>x.spec.id=="fjord-sport");var cat=new MechanicalCatalog(vehicles.Select(x=>x.spec));
            var build=cat.Resolve(v.spec.id,cat.Recipe(v.spec.id),v.CreateStockTune());build.tune.transmissionUpgrade=TransmissionUpgrade.Manual;
            var legacy=build.spec.Copy();
            foreach(var p in legacy.fittedParts){if(p.category==PartCategory.CylinderBlock)p.rpmLimit=p.torqueLimitNm=0;if(p.category==PartCategory.Flywheel)p.massKg=p.inertiaKgM2=0;}
            Func<VehicleSpec,float,PhysicalDrivingSession> run=(spec,health)=>{
                var driver=new DriverSettings();driver.ApplyPreset(DrivingPreset.Manual);
                var session=new PhysicalDrivingSession(new RunSnapshot{vehicle=spec,tune=build.tune,driving=driver},false);
                session.car.mechanics.Part(PartCategory.Driveshaft).condition.health=health;
                foreach(var c in PhysicalScenarios.MoneyShiftTimeline(session.snapshot).Take(600)){session.Step(c);if(session.car.state.drivetrainFailed)break;}
                return session;
            };
            var old=run(legacy,.001f);var current=run(build.spec,.001f);
            double oldHealth=old.car.mechanics.Part(PartCategory.Driveshaft).condition.health,newHealth=current.car.mechanics.Part(PartCategory.Driveshaft).condition.health;
            Check(oldHealth>0&&newHealth>0&&!old.car.state.drivetrainFailed&&!current.car.state.drivetrainFailed,"Old short shaft-failure fixture survives with both legacy and corrected hardware semantics");
            Near(newHealth,oldHealth,1e-7,"Fjord Sport legacy/current shaft fixture wear agrees");
            var failed=run(build.spec,.000001f);
            Check(failed.car.state.drivetrainFailed&&!failed.car.state.engineFailed&&failed.car.mechanics.Part(PartCategory.Driveshaft).condition.failureReason=="Torque overload","Corrected failure fixture breaks through transmitted torque without changing gameplay rates");
        }
        static void VerifyLegacyJournals(VehicleDefinition[] vehicles)
        {
            var vehicle=vehicles[0];var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            foreach(string model in new[]{"draglab-variety-v18","draglab-variety-v19","draglab-variety-v20","draglab-variety-v21","unknown-model"}) {
                var p=PlayerProfile.New(DateTime.UtcNow.Ticks);p.SelectedCar.vehicleId=p.selectedVehicleId=vehicle.spec.id;MechanicsOwnership.Ensure(p,catalog);
                var b=MechanicsOwnership.Resolve(p,p.SelectedCar,catalog);var car=new CarSimulation(b.spec,b.tune,new DriverSettings(),p.parts);
                car.SetFluids(p.SelectedCar.fluids);car.SetMassInputs(75,0,1.5,.45,p.SelectedCar.fuel.litres);
                string run=MechanicsOwnership.NewId("rating-recovery");MechanicsTransactions.ApplyCheckpoint(p,RunPersistence.Capture(run,p.SelectedCar.instanceId,0,car));
                var cp=RunPersistence.Capture(run,p.SelectedCar.instanceId,120,car);cp.components[0].condition.health=.7f;cp.model=model;
                string payload=JsonUtility.ToJson(new JournalData{baseRevision=p.revision,checkpoint=cp}),hash;
                using(var sha=System.Security.Cryptography.SHA256.Create())hash=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
                string path=Path.Combine(Folder,"isolated-existing-checks",model+".json");Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(RaceCheckpointStore.JournalPath(path),JsonUtility.ToJson(new JournalEnvelope{payload=payload,sha256=hash}),new UTF8Encoding(false));
                string before=JsonUtility.ToJson(p);bool merged=RaceCheckpointStore.Merge(path,p,true,out string warning);
                if(model=="unknown-model")Check(!merged&&JsonUtility.ToJson(p)==before,model+" resource journal rejected without mutations");
                else Check(merged&&warning==null&&p.runCheckpoints.Single(r=>r.runId==run).tick==120&&p.parts.Single(x=>x.instanceId==cp.components[0].instanceId).condition.health==.7f,model+" monotonic resources recovered without replaying obsolete physics");
            }
        }
        public static void RunRegressions()
        {
            Directory.CreateDirectory(Folder);var receipt=new StringBuilder();int failed=0;
            string root=Path.Combine(Folder,"isolated-existing-checks");Directory.CreateDirectory(root);
            Action<string,Func<int>> suite=(name,run)=>{Debug.Log("Starting "+name);try{int n=run();receipt.AppendLine("PASS "+name+": "+n+" checks");}catch(Exception e){failed++;receipt.AppendLine("FAIL "+name+": "+e);}File.WriteAllText(Path.Combine(Folder,"regression-verification.txt"),receipt.ToString(),new UTF8Encoding(false));Debug.Log(receipt.ToString());};
            suite("Part effects",()=>PartEffectsVerification.Run(root));
            suite("Mass and load",()=>MassLoadVerification.Run(root));
            suite("Race/bench parity",()=>SharedMechanicsVerification.Run(root));
            suite("Individual engine components",()=>IndividualEngineVerification.Run(root));
            suite("Parts recipes and calibration",()=>PartsVerification.Run(root));
            suite("Mechanical recovery",()=>MechanicalRecoveryVerification.Run(root));
            suite("Physical snapshot replay",()=>PhysicalBenchVerification.Run(root));
            suite("Async race save/recovery",()=>{string prior=RaceCheckpointVerification.OutputFolder;try{RaceCheckpointVerification.OutputFolder=Path.Combine(root,"race-checkpoints");RaceCheckpointVerification.RunStandalone();string text=File.ReadAllText(Path.Combine(RaceCheckpointVerification.OutputFolder,"verification.txt"),Encoding.UTF8);return int.Parse(System.Text.RegularExpressions.Regex.Match(text,@"Checks: (\d+)").Groups[1].Value);}finally{RaceCheckpointVerification.OutputFolder=prior;}});
            File.WriteAllText(Path.Combine(Folder,"regression-verification.txt"),receipt.ToString(),new UTF8Encoding(false));Debug.Log(receipt);
            if(failed>0)throw new InvalidOperationException(failed+" regression suites failed; see receipt.");
        }
        public static void RunPresentation()
        {
            int count=0;var report=new StringBuilder();
            try {
                DragLab.Runtime.NativePartComparisonChecks.Run(Resources.LoadAll<VehicleDefinition>("Data/Vehicles"),(ok,label)=>{if(!ok)throw new InvalidOperationException(label);count++;},new[]{PartCategory.Flywheel,PartCategory.CylinderBlock},new[]{"starter-hatch","wren","sport-coupe","utility-pickup"});
                report.AppendLine("PASS Flywheel/block comparison scopes, shared cache and four representative rating platforms: "+count+" checks");
            } catch(Exception e){report.AppendLine("FAIL "+e);throw;}
            finally{File.WriteAllText(Path.Combine(Folder,"comparison-verification.txt"),report.ToString(),new UTF8Encoding(false));Debug.Log(report);}
        }
        public static void RunFinalChecks()
        {
            Run();
            Debug.Log("Starting corrected race/bench parity suite");
            try{int count=SharedMechanicsVerification.Run(Path.Combine(Folder,"isolated-existing-checks"));File.WriteAllText(Path.Combine(Folder,"parity-verification.txt"),"PASS race/bench parity and simulation rules: "+count+" checks\n",new UTF8Encoding(false));}
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"parity-verification.txt"),"FAIL "+e,new UTF8Encoding(false));throw;}
            Debug.Log("Starting focused native comparison checks");RunPresentation();
            Debug.Log("Starting complete catalogue statistics audit");PartStatisticsAudit.Run();
        }
        public static void RunRemainingRegressions()
        {
            Directory.CreateDirectory(Folder);var report=new StringBuilder();int failed=0;
            string root=Path.Combine(Folder,"isolated-existing-checks");
            Action<string,Func<int>> suite=(name,run)=>{Debug.Log("Starting "+name);try{int n=run();report.AppendLine("PASS "+name+": "+n+" checks");}catch(Exception e){failed++;report.AppendLine("FAIL "+name+": "+e);}File.WriteAllText(Path.Combine(Folder,"remaining-regression-verification.txt"),report.ToString(),new UTF8Encoding(false));Debug.Log(report);};
            suite("Race/bench parity",()=>SharedMechanicsVerification.Run(root));
            var completed=File.ReadAllLines(Path.Combine(Folder,"recovery-completed-before-timeout.csv"),Encoding.UTF8).Skip(1).Select(l=>l.Split(',')[0]).ToArray();
            string priorRecovery=MechanicalRecoveryVerification.OutputFolder;
            try{MechanicalRecoveryVerification.OutputFolder=Path.Combine(root,"recovery-remainder");suite("Mechanical recovery remaining models",()=>MechanicalRecoveryVerification.Run(root,v=>!completed.Contains(v.spec.id)));}finally{MechanicalRecoveryVerification.OutputFolder=priorRecovery;}
            suite("Physical snapshot replay",()=>PhysicalBenchVerification.Run(root));
            suite("Async race save/recovery",()=>{string prior=RaceCheckpointVerification.OutputFolder;try{RaceCheckpointVerification.OutputFolder=Path.Combine(root,"race-checkpoints");RaceCheckpointVerification.RunStandalone();string text=File.ReadAllText(Path.Combine(RaceCheckpointVerification.OutputFolder,"verification.txt"),Encoding.UTF8);return int.Parse(System.Text.RegularExpressions.Regex.Match(text,@"Checks: (\d+)").Groups[1].Value);}finally{RaceCheckpointVerification.OutputFolder=prior;}});
            if(failed>0)throw new InvalidOperationException(failed+" remaining regression suites failed; see receipt.");
        }
    }
}
