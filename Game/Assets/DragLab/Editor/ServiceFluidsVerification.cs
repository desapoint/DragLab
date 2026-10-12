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
    public static class ServiceFluidsVerification
    {
        public static void RunStandalone() { Debug.Log("Service / fluids checks: " + Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../..")))); }
        static void Refresh(CarSimulation c) { c.mechanics.Prepare(c.state); ServiceDynamics.Prepare(c); }
        static CarSimulation Car(BuildResolution b) { return new CarSimulation(b.spec,b.tune,new DriverSettings {wearMode=WearMode.None,optimalThermals=true}); }
        public static int Run(string root)
        {
            int count=0;var log=new StringBuilder();var matrix=new StringBuilder("car,scenario,rpm,hp,oil_l,coolant_l,oil_bar,valve_float,engine_failed,mechanical_power_factor,combustion_efficiency,extra_friction_nm\n");var catalog=MechanicsTransactions.Catalog();
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);count++;log.AppendLine("PASS: "+label);};
            Action<CarSimulation,string,string> row=(c,id,scenario)=>{var s=c.state;matrix.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{4:R},{5:R},{6:R},{7:R},{8},{9:R},{10:R},{11:R}\n",id,scenario,s.rpm,s.engineHorsepower,s.fluids.oilLitres,s.fluids.coolantLitres,s.fluids.oilPressureBar,s.valveFloat,s.engineFailed,s.mechanicalPowerFactor,s.conditionCombustionEfficiency,s.extraEngineFrictionNm);};
            foreach(var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) {
                string id=v.spec.id;var recipe=catalog.Recipe(id);var stock=catalog.Resolve(id,recipe,v.CreateStockTune());var healthy=Car(stock);
                check(stock.Drivable && healthy.state.engineSupplyAvailable,id+": factory service hardware is complete and runs.");
                foreach(var d in catalog.parts.Where(p=>p.id.StartsWith(id+"/")&&ServiceHardware.IsCategory(p.category)&&catalog.Fits(v.spec,p))) {
                    var map=catalog.Recipe(id);map[d.category]=d.id;var build=catalog.Resolve(id,map,v.CreateStockTune());
                    if(!build.Drivable&&build.errors.Any(e=>e.Contains("followers"))){check(true,id+"/"+d.id+": incompatible follower pairing rejected");map[PartCategory.Camshaft]=id+"/Camshaft/RollerStreet";map[PartCategory.Followers]=id+"/Followers/RollerConversion";build=catalog.Resolve(id,map,v.CreateStockTune());}
                    var car=Car(build);
                    check(build.Drivable && car.spec.Validate()==null && PartSemantics.Stats(d).All(s=>MechanicsSchema.Finite(s.value)),id+"/"+d.id+": hardware resolves with finite meaningful stats.");
                    car.IndependentStep(DragMath.FixedStep,new DriverInput {throttle=.4},true,false);
                    check(!car.state.engineFailed && car.state.fluids.Validate(car.spec.fluids)==null,id+"/"+d.id+": installed option ticks without invalid state.");
                }
                var repeated=Car(stock);repeated.driver.wearMode=WearMode.Full;
                repeated.driver.optimalThermals=false;
                for(int pass=0;pass<4;pass++) {
                    if(pass>0){var previous=repeated;repeated=new CarSimulation(stock.spec,stock.tune,previous.driver,previous.mechanics.parts);repeated.SetFluids(previous.state.fluids);}
                    for(int i=0;i<120;i++)repeated.Stage(DragMath.FixedStep,new DriverInput {throttle=.3});
                    repeated.Launch(0);for(int i=0;i<3600&&repeated.state.distance<402.336;i++)repeated.Step(DragMath.FixedStep,new DriverInput {throttle=1});
                    check(repeated.state.distance>=402.336 && !repeated.state.engineFailed && repeated.mechanics.parts.All(p=>!p.condition.failed),id+": stock repeated quarter-mile pass "+pass+" survives with physical thermals and retained condition. "+string.Join(";",repeated.mechanics.parts.Where(p=>p.condition.failed).Select(p=>p.slot+":"+p.condition.failureReason)));
                    repeated.CoolDown(60);row(repeated,id,"stock-pass-"+pass);
                }
                foreach(var category in new[]{PartCategory.PistonRings,PartCategory.CylinderBlock,PartCategory.Valves,PartCategory.Camshaft,PartCategory.Followers,PartCategory.TimingDrive,PartCategory.Bearings,PartCategory.Ignition}.Where(c=>c!=PartCategory.Ignition||!CombustionHardware.Diesel(v.spec))) {
                    var worn=Car(stock);worn.state.rpm=4000;worn.state.throttle=.7;var part=worn.mechanics.Part(category);part.condition.health=.55f;Refresh(worn);
                    check(!worn.state.engineFailed && worn.state.mechanicalPowerFactor<healthy.state.mechanicalPowerFactor || category==PartCategory.Bearings&&worn.state.extraEngineFrictionNm>0,id+"/"+category+": wear changes output/friction before failure.");
                    row(worn,id,"worn-"+category);double full=worn.state.mechanicalPowerFactor;worn.driver.conditionEffects=ConditionEffectMode.Reduced;worn.state.conditionSeverity=worn.driver.ConditionSeverity;Refresh(worn);
                    check(worn.state.mechanicalPowerFactor>=full,id+"/"+category+": Reduced condition effects reduce performance loss.");
                    worn.driver.conditionEffects=ConditionEffectMode.Assisted;worn.state.conditionSeverity=0;Refresh(worn);check(Math.Abs(worn.state.mechanicalPowerFactor-1)<1e-9 && worn.state.extraEngineFrictionNm==0,id+"/"+category+": Assisted removes gradual performance loss, retaining the worn part.");
                }
                foreach(var category in new[]{PartCategory.ValveSprings,PartCategory.Followers}){var broken=Car(stock);var item=broken.mechanics.Part(category);item.condition.health=0;item.condition.failed=true;item.condition.failureReason="Assigned complete valvetrain-set failure";Refresh(broken);check(!broken.state.engineSupplyAvailable&&broken.state.stalled&&!broken.state.engineFailed,id+"/"+category+": complete failure stops combustion without declaring all engine internals destroyed.");}
                var seized=Car(stock);seized.mechanics.Part(PartCategory.Bearings).condition.health=0;Refresh(seized);check(seized.state.engineFailed,id+": zero structural bearing condition cannot be masked by an unset failure flag.");
                var efficiency=Car(stock);efficiency.state.rpm=3500;efficiency.state.throttle=.6;efficiency.state.engineHorsepower=100;double normalFuel=FuelDynamics.Flow(efficiency);efficiency.mechanics.Part(PartCategory.PistonRings).condition.health=.5f;Refresh(efficiency);efficiency.state.throttle/=efficiency.state.conditionCombustionEfficiency;
                check(FuelDynamics.Flow(efficiency)>normalFuel,id+": worn sealing consumes more fuel for equal output, without a universal part-wear fuel multiplier.");
                efficiency.mechanics.Part(PartCategory.FuelSystem).condition.deposits=.8;Refresh(efficiency);check(efficiency.state.fuelMeteringWaste>0,id+": metering deposits affect efficiency.");
                var raceCam=catalog.Recipe(id);raceCam[PartCategory.Camshaft]=catalog.Id(id,PartCategory.Camshaft,2);var cam=catalog.Resolve(id,raceCam,v.CreateStockTune());
                check(new EngineSoundProfile(cam.spec,cam.tune).camOverlap>new EngineSoundProfile(stock.spec,stock.tune).camOverlap,id+": aggressive cam changes the idle voice while preserving engine family.");
                check(ServiceDynamics.CamMultiplier(cam.spec,1500)<1 && ServiceDynamics.CamMultiplier(cam.spec,cam.spec.redlineRpm) >1,id+": aggressive cam reshapes low/high RPM torque.");
                var unmatched=Car(cam);unmatched.state.rpm=cam.spec.redlineRpm;Refresh(unmatched);check(unmatched.state.valveFloat>0,id+": aggressive cam reveals factory spring mismatch.");
                raceCam[PartCategory.ValveSprings]=catalog.Id(id,PartCategory.ValveSprings,1);var matched=Car(catalog.Resolve(id,raceCam,v.CreateStockTune()));matched.state.rpm=matched.spec.redlineRpm;Refresh(matched);check(matched.state.valveFloat==0,id+": matched springs restore valve control.");
                var tune=v.CreateStockTune();tune.camTimingDegrees=6;check(catalog.Resolve(id,recipe,tune).tune.camTimingDegrees==0,id+": fixed timing drive rejects cam tuning.");recipe[PartCategory.TimingDrive]=catalog.Id(id,PartCategory.TimingDrive,1);check(catalog.Resolve(id,recipe,tune).spec.valvetrain.camTimingDegrees==6,id+": adjustable timing drive unlocks saved cam timing.");
                var fluids=Car(stock);double initialMass=fluids.mass.TranslatingKg;var f=fluids.state.fluids.Copy();f.oilLitres-=1;f.coolantLitres-=1;fluids.SetFluids(f);
                check(Math.Abs(initialMass-fluids.mass.TranslatingKg-1.91)<1e-8,id+": oil and coolant quantity affect mass exactly once.");
                double before=fluids.state.fluids.oilLitres;check(FluidDynamics.Add(fluids,true,99)==stock.spec.fluids.oilCapacityLitres-before && fluids.state.fluids.oilLitres==stock.spec.fluids.oilCapacityLitres,id+": top-up clamps to installed free capacity.");
                fluids.mechanics.Part(PartCategory.Cooling).condition.leakSeverity=.6;fluids.mechanics.Part(PartCategory.Cooling).condition.temperatureC=105;fluids.state.rpm=3000;fluids.state.throttle=.5;FluidDynamics.Tick(fluids,1);double afterRace=fluids.state.fluids.coolantLitres;fluids.CoolDown(60);
                check(fluids.state.fluids.coolantLitres<afterRace && fluids.state.fluids.oilPressureBar==0 && !fluids.state.engineFailed,id+": radiator can still cool while leaking and loses coolant in engine-off cooldown.");
                FluidDynamics.Add(fluids,false,99);check(fluids.mechanics.Part(PartCategory.Cooling).condition.leakSeverity==.6,id+": top-up does not seal a radiator leak.");
                var rad=fluids.mechanics.Part(PartCategory.Cooling);float health=rad.condition.health;ServiceOperations.Apply(fluids.mechanics.Definition(rad),rad.condition,ServiceAction.Reseal);check(rad.condition.leakSeverity==0 && rad.condition.health==health,id+": reseal removes only the leak fault.");row(fluids,id,"leak-resealed");
                var rates=Car(stock);rates.state.rpm=4000;rates.state.throttle=.7;rates.mechanics.Part(PartCategory.OilPan).condition.leakSeverity=.5;rates.driver.fluidDepletion=ConsumptionMode.Realistic;var rateStart=rates.CaptureDynamics();FluidDynamics.Tick(rates,.1);double burn=rates.state.fluids.oilBurnLps,leak=rates.state.fluids.oilLeakLps;
                rates.RestoreDynamics(rateStart);rates.driver.fluidDepletion=ConsumptionMode.Reduced;FluidDynamics.Tick(rates,.1);check(Math.Abs(rates.state.fluids.oilBurnLps/burn-.25)<1e-12&&Math.Abs(rates.state.fluids.oilLeakLps/leak-.25)<1e-12,id+": Reduced fluid loss and telemetry are exactly 25%.");
                rates.driver.fluidDepletion=ConsumptionMode.None;rates.driver.fluidAging=ConsumptionMode.None;f=rates.state.fluids.Copy();FluidDynamics.Tick(rates,1);check(rates.state.fluids.oilLitres==f.oilLitres&&rates.state.fluids.coolantLitres==f.coolantLitres&&rates.state.fluids.oilCondition==f.oilCondition&&rates.state.fluids.oilBurnLps==0,id+": None freezes fluid quantity and aging without repairing faults.");
                var hot=Car(stock);hot.driver.optimalThermals=false;hot.state.optimalThermals=false;hot.mechanics.Part(PartCategory.Cooling).condition.temperatureC=150;hot.state.fluids.coolantPressureBar=1;FluidDynamics.Tick(hot,1);check(hot.state.fluids.coolantVentLps>0 && hot.mechanics.Part(PartCategory.Cooling).condition.health==1,id+": coolant can vent from overheating without instantly destroying radiator hardware.");
                var empty=Car(stock);f=empty.state.fluids.Copy();f.oilLitres=0;empty.SetFluids(f);Refresh(empty);check(empty.state.fluids.oilPressureBar==0&&empty.state.lubricationFactor==0,id+": empty oil system loses pressure.");empty.driver.wearMode=WearMode.Full;empty.state.rpm=4000;empty.state.throttle=1;for(int i=0;i<5000;i++)FluidDynamics.Tick(empty,DragMath.FixedStep);
                check(empty.mechanics.Part(PartCategory.Bearings).condition.failed&&empty.state.engineFailed,id+": sustained starvation damages bearings and can break the engine.");row(empty,id,"oil-starvation");
                var rings=healthy.mechanics.Part(PartCategory.PistonRings);rings.condition.health=.5f;bool refused=false;try{ServiceOperations.Apply(healthy.mechanics.Definition(rings),rings.condition,ServiceAction.Recondition,.5);}catch(InvalidOperationException){refused=true;}check(refused,id+": ring-only repair rejected with an unhealthy bore.");ServiceOperations.Apply(healthy.mechanics.Definition(rings),rings.condition,ServiceAction.Recondition,1);check(rings.condition.health>.5&&rings.condition.health<1,id+": usable bore permits limited ring reconditioning, not complete restoration.");
                var profile=PlayerProfile.New(DateTime.UtcNow.Ticks);profile.cars.Clear();profile.cars.Add(new OwnedCar{vehicleId=id,tune=v.CreateStockTune()});profile.cash=100000;profile.selectedVehicleId=id;MechanicsOwnership.Ensure(profile,catalog);var owner=profile.cars[0];int identities=profile.parts.Count;owner.fluids.oilLitres=0;owner.fluids.coolantLitres=0;
                var loaded=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));MechanicsOwnership.Ensure(loaded,catalog);check(loaded.parts.Count==identities&&loaded.cars[0].fluids.oilLitres==0&&loaded.cars[0].fluids.coolantLitres==0,id+": save/reload does not duplicate factory parts or refill deliberately empty fluids.");
                ServiceOperations.TopUp(loaded,owner.instanceId,true,99);check(loaded.cars[0].fluids.oilLitres==stock.spec.fluids.oilCapacityLitres,id+": owned service uses resolved capacity.");
                var oldFilter=loaded.parts.Find(p=>p.instanceId==loaded.cars[0].installed.slots.Find(s=>s.slot=="OilFilter").partInstanceId);oldFilter.condition.deposits=.8;ServiceOperations.ChangeOil(loaded,owner.instanceId);check(oldFilter.location==PartLocation.Inventory&&oldFilter.condition.deposits==.8&&loaded.cars[0].fluids.oilCondition==1,id+": oil service replaces filter while retaining dirty original identity.");
                var quantities=loaded.cars[0].fluids.Copy();PartOperations.Buy(loaded,catalog,owner.instanceId,catalog.Id(id,PartCategory.OilPan,1),true);
                check(loaded.cars[0].fluids.oilLitres==quantities.oilLitres && MechanicsOwnership.Resolve(loaded,loaded.cars[0],catalog).spec.fluids.oilCapacityLitres>quantities.oilLitres,id+": larger pan does not create free oil during a part swap.");
                var spare=loaded.parts.Find(p=>p.location==PartLocation.Inventory&&catalog.Find(p.definitionId).category==PartCategory.OilPan);PartOperations.Install(loaded,catalog,owner.instanceId,spare.instanceId);
                check(loaded.cars[0].fluids.oilLitres==quantities.oilLitres,id+": pan downgrade retains existing liquid quantity.");
                PartOperations.Remove(loaded,owner.instanceId,PartCategory.CoolantPump);int removedCount=loaded.parts.Count;MechanicsOwnership.Ensure(loaded,catalog);
                check(loaded.parts.Count==removedCount&&!loaded.cars[0].installed.slots.Any(b=>b.slot=="CoolantPump"),id+": one-time migration never recreates a deliberately removed service part.");
                var oldProfile=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(loaded));oldProfile.cars[0].serviceHardwareVersion=0;oldProfile.cars[0].fluids=new FluidState();
                var oldHardwareIds=oldProfile.parts.Select(p=>p.instanceId).ToArray();MechanicsOwnership.Ensure(oldProfile,catalog);
                check(oldProfile.cars[0].fluids.oilLitres==5&&oldProfile.cars[0].installed.slots.Any(b=>b.slot=="CoolantPump")&&oldHardwareIds.All(part=>oldProfile.parts.Any(p=>p.instanceId==part)),id+": legacy migration initializes fluids once and adds OEM hardware without replacing prior identities.");
                PlayerProfile committed;string error;double savedOil=loaded.cars[0].fluids.oilLitres;
                bool transaction=MechanicsTransactions.Try(loaded,catalog,loaded.revision,p=>p.cars[0].fluids.oilLitres=0,p=>false,out committed,out error);
                check(!transaction&&committed==loaded&&loaded.cars[0].fluids.oilLitres==savedOil,id+": failed persistence rolls back a fluid service transaction.");
                var checkpoint=new RunCheckpoint {runId="fluid-checkpoint-"+id,ownedVehicleId=owner.instanceId,tick=1,fluids=loaded.cars[0].fluids.Copy()};checkpoint.fluids.oilLitres+=.1;
                bool rewind=false;try{MechanicsTransactions.ApplyCheckpoint(loaded,checkpoint);}catch(InvalidOperationException){rewind=true;}check(rewind&&loaded.cars[0].fluids.oilLitres==savedOil,id+": checkpoint refuses unauthorized refill.");
                var seals=Car(stock);seals.state.rpm=3500;seals.state.throttle=.7;FluidDynamics.Tick(seals,1);double ordinaryBurn=seals.state.fluids.oilBurnLps;seals.mechanics.Part(PartCategory.ValveSeals).condition.health=.4f;FluidDynamics.Tick(seals,1);check(seals.state.fluids.oilBurnLps>ordinaryBurn&&!seals.state.engineFailed,id+": worn stem seals increase oil consumption without an instant engine failure.");
                var bearings=Car(stock);bearings.state.rpm=3500;Refresh(bearings);double normalPressure=bearings.state.fluids.oilPressureBar;bearings.mechanics.Part(PartCategory.Bearings).condition.health=.4f;Refresh(bearings);check(bearings.state.fluids.oilPressureBar<normalPressure,id+": worn bearing clearances reduce oil pressure.");
                var dirty=Car(stock);dirty.mechanics.Part(PartCategory.HeadIntake).condition.deposits=.8;Refresh(dirty);check(dirty.state.mechanicalPowerFactor<1,id+": intake deposits reduce breathing.");var intake=dirty.mechanics.Part(PartCategory.HeadIntake);ServiceOperations.Apply(dirty.mechanics.Definition(intake),intake.condition,ServiceAction.Clean);Refresh(dirty);check(dirty.state.mechanicalPowerFactor==1,id+": deposit cleaning restores breathing without replacing healthy hardware.");
                var coolerMap=catalog.Recipe(id);coolerMap[PartCategory.OilCooler]=catalog.Id(id,PartCategory.OilCooler,1);var cooled=Car(catalog.Resolve(id,coolerMap,v.CreateStockTune()));var bare=Car(stock);
                foreach(var c in new[]{cooled,bare}){c.state.optimalThermals=false;c.state.fluids.oilTemperatureC=130;c.state.rpm=4000;c.state.engineHorsepower=100;FluidDynamics.Tick(c,1);}
                check(cooled.state.fluids.oilTemperatureC<bare.state.fluids.oilTemperatureC,id+": actual oil cooler heat rejection lowers hot-oil temperature.");
                var coolingPart=cooled.mechanics.Part(PartCategory.OilCooler);coolingPart.condition.failed=true;coolingPart.condition.failureReason="Test oil cooler failure";cooled.state.fluids.oilTemperatureC=130;bare.state.fluids.oilTemperatureC=130;FluidDynamics.Tick(cooled,1);FluidDynamics.Tick(bare,1);check(Math.Abs(cooled.state.fluids.oilTemperatureC-bare.state.fluids.oilTemperatureC)<1e-6&&cooled.state.fluids.oilLeakLps>0&&!cooled.state.engineFailed,id+": failed oil cooler loses heat rejection and can leak without instant engine destruction.");
                var assisted=new DriverSettings();assisted.ApplySimulationPreset(SimulationDifficulty.Arcade);check(assisted.fluidDepletion==ConsumptionMode.None&&assisted.fluidAging==ConsumptionMode.None&&assisted.conditionEffects==ConditionEffectMode.Assisted&&assisted.serviceAssist==ServiceAssist.AutoTopUp,id+": Arcade selects fluid and service assistance coherently.");
                assisted.ApplySimulationPreset(SimulationDifficulty.Balanced);check(assisted.FluidDepletionRate==.25&&assisted.FluidAgingRate==.25&&assisted.ConditionSeverity==.25,id+": Balanced selects independent 25% service rules.");
                var snapshot=new RunSnapshot{vehicle=cam.spec,tune=cam.tune,driving=new DriverSettings{wearMode=WearMode.Full}};var session=new PhysicalDrivingSession(snapshot);
                session.car.mechanics.Part(PartCategory.Cooling).condition.leakSeverity=.3;session=session.ContinueRecording();
                for(int i=0;i<720;i++)session.Step(new TickInput{tick=i,input=new DriverInput{throttle=i<500?.75:0},neutral=i<120});
                var capture=JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.snapshot));var replay=PhysicalDrivingSession.Replay(capture);check(session.Csv()==replay.Csv(),id+": fluids, faults, thermals and wear replay exactly after JSON continuation capture.");
                capture.startingDynamics.state.fluids.oilLitres=double.NaN;capture.startingDynamics.exactNumbers="";check(PhysicalDrivingSession.Validate(capture)!=null,id+": corrupt checkpoint fluids rejected.");
            }
            string folder=Path.Combine(root,"reports/service-fluids");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString()+"TOTAL "+count);File.WriteAllText(Path.Combine(folder,"matrix.csv"),matrix.ToString());return count;
        }
    }
}
