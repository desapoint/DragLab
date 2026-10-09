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
    public static class RaceThermalVerification
    {
        public static void RunStandalone()
        {
            int checks=0;var log=new StringBuilder("Race thermal difficulty / real elapsed parked time / shared warm-up physics\n");
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);checks++;log.AppendLine("PASS: "+label);};
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            long now=new DateTime(2026,10,9,12,0,0,DateTimeKind.Utc).Ticks;
            foreach(var v in vehicles) {
                var build=catalog.Resolve(v.spec.id,catalog.Recipe(v.spec.id),v.CreateStockTune());
                Func<CarSimulation> create=()=>new CarSimulation(build.spec,build.tune,new DriverSettings {throttleAssist=false,tractionAssist=false});
                foreach(var mode in new[]{RaceTemperatureMode.FullyCooled,RaceTemperatureMode.Optimal,RaceTemperatureMode.Live}) {
                    var car=create();var box=car.mechanics.Part(PartCategory.Gearbox);box.condition.temperatureC=130;box.condition.health=.7f;
                    car.state.fluids.oilTemperatureC=120;double fuel=car.state.fuelLitres,oil=car.state.fluids.oilLitres;
                    car.ApplyRaceTemperatures(mode,now-TimeSpan.FromMinutes(10).Ticks,now);
                    double expected=mode==RaceTemperatureMode.FullyCooled?20:mode==RaceTemperatureMode.Optimal?70:20+110*Math.Exp(-600*car.mechanics.Definition(box).coolingWattsPerC/car.mechanics.Definition(box).heatCapacityJPerC);
                    check(Math.Abs(box.condition.temperatureC-expected)<1e-8,v.spec.id+" / "+mode+": authored temperature policy without repairing health");
                    check(box.condition.health==.7f&&car.state.fuelLitres==fuel&&car.state.fluids.oilLitres<=oil,v.spec.id+" / "+mode+": resources/health never replenished");
                    var tire=car.mechanics.Part(PartCategory.Tires);check(mode!=RaceTemperatureMode.Optimal||tire.condition.tires.All(t=>t.temperatureC==car.mechanics.Definition(tire).tireOptimalC),v.spec.id+": optimum is compound-specific");
                }
                var live=create();var gearbox=live.mechanics.Part(PartCategory.Gearbox);gearbox.condition.temperatureC=130;
                live.ApplyRaceTemperatures(RaceTemperatureMode.Live,now,now-TimeSpan.TicksPerHour);
                check(gearbox.condition.temperatureC==130&&live.state.fluids.serviceElapsedSeconds==0,v.spec.id+": clock rollback cannot fabricate cooling");
                live.ApplyRaceTemperatures(RaceTemperatureMode.Live,0,now);
                check(gearbox.condition.temperatureC==130,v.spec.id+": legacy missing clock preserves heat until first checkpoint");
                live.ApplyRaceTemperatures(RaceTemperatureMode.Live,now-TimeSpan.TicksPerDay,now);
                check(Math.Abs(gearbox.condition.temperatureC-20)<.01&&Math.Abs(live.state.fluids.serviceElapsedSeconds-86400)<1e-8,v.spec.id+": offline day advances real parked time without a real wait");
                var ideal=create();ideal.driver.optimalThermals=true;ideal.ApplyRaceTemperatures(RaceTemperatureMode.FullyCooled,now,now);
                check(ideal.mechanics.Part(PartCategory.Gearbox).condition.temperatureC==70&&ideal.state.fluids.oilTemperatureC==90,v.spec.id+": heat management OFF overrides the disabled start policy");
                foreach(var mode in new[]{RaceTemperatureMode.FullyCooled,RaceTemperatureMode.Optimal,RaceTemperatureMode.Live}) {
                    var leaky=create();leaky.mechanics.Part(PartCategory.Cooling).condition.leakSeverity=.5;leaky.mechanics.Part(PartCategory.OilPan).condition.leakSeverity=.5;
                    double oil=leaky.state.fluids.oilLitres,coolant=leaky.state.fluids.coolantLitres;
                    leaky.ApplyRaceTemperatures(mode,now-TimeSpan.FromMinutes(2).Ticks,now);
                    check(leaky.state.fluids.oilLitres<oil&&leaky.state.fluids.coolantLitres<coolant,v.spec.id+" / "+mode+": start-temperature assist cannot bypass independent parked leaks");
                }
                var assisted=create();assisted.driver.optimalThermals=true;assisted.mechanics.Part(PartCategory.Cooling).condition.leakSeverity=.5;double quantity=assisted.state.fluids.coolantLitres;
                assisted.ApplyRaceTemperatures(RaceTemperatureMode.Live,now-TimeSpan.FromMinutes(2).Ticks,now);
                check(assisted.state.fluids.coolantLitres<quantity,v.spec.id+": disabling heat management does not disable fluid depletion");
                var noDepletion=create();noDepletion.driver.fluidDepletion=ConsumptionMode.None;noDepletion.mechanics.Part(PartCategory.Cooling).condition.leakSeverity=.5;quantity=noDepletion.state.fluids.coolantLitres;
                noDepletion.ApplyRaceTemperatures(RaceTemperatureMode.Live,now-TimeSpan.FromMinutes(2).Ticks,now);
                check(noDepletion.state.fluids.coolantLitres==quantity,v.spec.id+": explicit None fluid difficulty still prevents parked depletion");
            }
            var coupe=vehicles.First(v=>v.spec.id=="sport-coupe");var b=catalog.Resolve(coupe.spec.id,catalog.Recipe(coupe.spec.id),coupe.CreateStockTune());
            var d=new DriverSettings {shifting=ShiftMode.Sequential,launch=LaunchMode.Assisted,throttleAssist=false,tractionAssist=false};
            var race=new RaceSimulation(new CarSimulation(b.spec,b.tune,d),new CarSimulation(b.spec,b.tune,d.Copy()),402.336);race.BeginWarmup();
            for(int i=0;i<120;i++)race.Step(new DriverInput());
            check(race.warmup&&race.time==0&&race.player.state.gear==0&&race.player.state.rpm>=b.spec.idleRpm*.8,"Warm-up idles in neutral without consuming race countdown");
            double fuelBefore=race.player.state.fuelLitres;race.player.state.warmupLineLock=true;
            race.Step(new DriverInput {throttle=1,brake=1,shiftUp=true});
            for(int i=0;i<600;i++)race.Step(new DriverInput {throttle=1,brake=1});
            var pack=race.player.mechanics.Part(PartCategory.Tires);
            check(pack.condition.tires[2].slipEnergyJ>0&&pack.condition.tires[2].temperatureC>20&&race.player.state.fuelLitres<fuelBefore,"RWD line-lock burnout uses actual wheel slip, tire heat and fuel");
            check(pack.condition.tires[2].health<1&&race.time==0&&!race.player.state.disqualified,"Burnout retains wear without starting or red-lighting race");
            check(!race.BeginCountdown(),"Moving/spinning wheels cannot be silently teleported to starting line");
            race.player.state.warmupLineLock=false;race.Step(new DriverInput {brake=1,shiftDown=true});
            for(int i=0;i<2400&&!race.WarmupStopped;i++)race.Step(new DriverInput {brake=1,shiftDown=race.player.state.gear>0});
            double heat=pack.condition.tires[2].temperatureC,health=pack.condition.tires[2].health,fuelAfter=race.player.state.fuelLitres;
            check(race.BeginCountdown(),"Stopped warm-up advances explicitly to lights");
            check(pack.condition.tires[2].temperatureC==heat&&pack.condition.tires[2].health==health&&race.player.state.fuelLitres==fuelAfter&&!race.player.state.warmupLineLock,"Staging preserves warm-up heat/wear/fuel and releases line lock");
            race.Step(new DriverInput());check(race.time>0&&race.player.state.gear==0&&!race.player.state.launched,"Normal countdown resumes in staged neutral");
            var profile=PlayerProfile.New(now);MechanicsOwnership.Ensure(profile,catalog);var owned=profile.SelectedCar;
            var resolved=MechanicsOwnership.Resolve(profile,owned,catalog);var sim=new CarSimulation(resolved.spec,resolved.tune,new DriverSettings(),profile.parts.Where(p=>p.ownerId==owned.instanceId||p.ownerId==owned.engineAssemblyId));
            var cp=RunPersistence.Capture("thermal-clock",owned.instanceId,0,sim);cp.thermalUtcTicks=now;
            MechanicsTransactions.ApplyCheckpoint(profile,cp);var reloaded=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));
            check(reloaded.SelectedCar.thermalUtcTicks==now,"Thermal timestamp persists through checkpoint and save serialization");
            var prefs=new DriverSettings {raceTemperatures=RaceTemperatureMode.Live,warmupBeforeRace=true};
            check(JsonUtility.FromJson<DriverSettings>(JsonUtility.ToJson(prefs)).warmupBeforeRace&&prefs.Copy().raceTemperatures==RaceTemperatureMode.Live,"Temperature/warm-up preferences copy and serialize");
            // Controlled delivered-load fixture tests the production stress integrator;
            // it does not pretend these injected loads are a road-simulation result.
            var stressed=new CarSimulation(b.spec,b.tune,new DriverSettings());var weak=stressed.mechanics.Part(PartCategory.Gearbox);var definition=stressed.mechanics.Definition(weak);
            definition.torqueLimitNm=300;definition.overloadSeconds=3;stressed.state.gear=1;stressed.state.transmittedTorqueNm=350;
            for(int i=0;i<120;i++)stressed.mechanics.Tick(stressed,DragMath.FixedStep,0,0);
            check(!weak.condition.failed&&weak.condition.health>.98,"Controlled undersized gearbox survives one second above its torque rating");
            for(int i=0;i<20000&&!weak.condition.failed;i++){stressed.state.transmittedTorqueNm=350;stressed.mechanics.Tick(stressed,DragMath.FixedStep,0,0);}
            check(weak.condition.failed&&weak.condition.failureReason=="Torque overload","Same undersized gearbox fails after sustained overload without changing its rating");
            var collection=PlayerProfile.New(now);foreach(var vehicle in vehicles.Where(v=>v.spec.id!="starter-hatch"))collection.cars.Add(new OwnedCar{vehicleId=vehicle.spec.id,tune=vehicle.CreateStockTune()});
            MechanicsOwnership.Ensure(collection,catalog);string original=JsonUtility.ToJson(collection);var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<5;i++)MechanicsOwnership.Ensure(collection,catalog);timer.Stop();
            check(original==JsonUtility.ToJson(collection),"Repeated eight-car owner-cached normalization preserves every identity/condition/resource");
            log.AppendLine("Informational: five eight-car normalizations / "+timer.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms; no machine-dependent pass threshold.");
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));Directory.CreateDirectory(Path.Combine(root,"reports/phases/3h"));
            File.WriteAllText(Path.Combine(root,"reports/phases/3h/thermal-verification.txt"),log+"Checks: "+checks+"\n",new UTF8Encoding(false));Debug.Log(log.ToString());
        }
    }
}
