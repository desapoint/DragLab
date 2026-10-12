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
    public static class FuelingConversionVerification
    {
        public static void RunStandalone() { Debug.Log("EFI conversion checks: " + Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../..")))); }
        public static int Run(string root)
        {
            int count=0; var log=new StringBuilder(); var catalog=MechanicsTransactions.Catalog();
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);count++;log.AppendLine("PASS: "+label);};
            foreach(var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) {
                string id=v.spec.id; var stock=catalog.Resolve(id,catalog.Recipe(id),v.CreateStockTune());
                check(stock.Drivable && new CarSimulation(stock.spec,stock.tune,new DriverSettings()).state.engineSupplyAvailable,id+": factory fuel hardware still runs.");
                var legacy=stock.spec.Copy(); foreach(var p in legacy.fittedParts)if(p.category==PartCategory.Controls){p.injectionControlMetadataVersion=0;p.supportsInjection=false;p.controllerCylinderCapacity=0;}
                var originalSession=new PhysicalDrivingSession(new RunSnapshot{vehicle=stock.spec,tune=stock.tune,driving=new DriverSettings{wearMode=WearMode.None}});
                var legacySession=new PhysicalDrivingSession(new RunSnapshot{vehicle=legacy,tune=stock.tune,driving=new DriverSettings{wearMode=WearMode.None}});
                for(int i=0;i<240;i++){var input=new TickInput{tick=i,input=new DriverInput{throttle=.5},neutral=true};originalSession.Step(input);legacySession.Step(input);}
                if(v.spec.engineBehavior.fueling==FuelSystem.Diesel) {
                    check(stock.spec.fittedParts.Any(p=>p.category==PartCategory.Controls && p.injectionControlMetadataVersion>0 && p.supportsDiesel && p.controllerCylinderCapacity>=v.spec.sound.cylinders),id+": newly introduced diesel requires explicit injector-controller metadata, with no pre-diesel legacy capture.");
                } else check(originalSession.Csv()==legacySession.Csv(),id+": captures predating controller metadata retain exact factory behavior.");
                if(v.spec.engineBehavior.fueling!=FuelSystem.Carburetor) {check(!catalog.parts.Any(p=>p.id.StartsWith(id+"/FuelSystem/Efi")),id+": factory injected engine has no pointless carb conversion kit.");continue;}
                foreach(string kit in new[]{"EfiThrottleBody","EfiPortInjection"}) {
                    var map=catalog.Recipe(id);map[PartCategory.FuelSystem]=id+"/FuelSystem/"+kit;
                    var bad=catalog.Resolve(id,map,v.CreateStockTune());
                    check(!bad.Drivable && bad.errors.Any(e=>e.Contains("injection-capable")),id+"/"+kit+": mechanical carb controller cannot run injectors.");
                    var unpowered=new CarSimulation(bad.spec,bad.tune,new DriverSettings());
                    check(!unpowered.state.engineSupplyAvailable && !unpowered.state.engineFailed,id+"/"+kit+": missing injector power stops engine without destroying its internals.");
                    map.Remove(PartCategory.Controls);
                    check(!catalog.Resolve(id,map,v.CreateStockTune()).Drivable,id+"/"+kit+": controller is required after conversion.");
                    var four=catalog.Find(catalog.Id("starter-hatch",PartCategory.Controls));
                    check(!catalog.Fits(v.spec,four),id+"/"+kit+": four-cylinder factory controller cannot run this V8 conversion.");
                    foreach(string ecu in new[]{id+"/Controls/EfiStreet",catalog.Id(id,PartCategory.Controls,1)}) {
                        map[PartCategory.Controls]=ecu;var b=catalog.Resolve(id,map,v.CreateStockTune());
                        check(b.Drivable && b.spec.Validate()==null && b.spec.engineBehavior.fueling==FuelSystem.Injection,id+"/"+kit+"/"+ecu+": compatible kit and controller resolve as a valid injected engine.");
                        var before=new EngineSoundProfile(stock.spec,stock.tune);var after=new EngineSoundProfile(b.spec,b.tune);
                        check(after.fueling==FuelSystem.Injection && after.cylinders==before.cylinders && after.layout==before.layout && after.camOverlap==before.camOverlap && after.litres==before.litres && after.bassGain==before.bassGain && after.mechanicalClatter==before.mechanicalClatter,id+"/"+kit+": sound uses injection metering while retaining engine, cam and exhaust voice.");
                        check(b.spec.engineBehavior.mixtureVariation<v.spec.engineBehavior.mixtureVariation && b.spec.engineBehavior.hesitationSeconds<v.spec.engineBehavior.hesitationSeconds && b.spec.engineBehavior.camOverlap==v.spec.engineBehavior.camOverlap,id+"/"+kit+": metering and hesitation improve without replacing the cam.");
                        var baseline=new InductionProfile(stock.spec,stock.tune);var converted=new InductionProfile(b.spec,b.tune);
                        check(Math.Abs(InductionDynamics.SteadyOutput(baseline,4000).engineTorqueNm-InductionDynamics.SteadyOutput(converted,4000).engineTorqueNm)<1e-9,id+"/"+kit+": conversion grants no arbitrary steady horsepower multiplier.");
                        var car=new CarSimulation(b.spec,b.tune,new DriverSettings{wearMode=WearMode.None});
                        for(int i=0;i<600;i++)car.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=.45},true,false);
                        check(car.state.engineSupplyAvailable && car.state.rpm>b.spec.idleRpm && !car.state.engineFailed && car.state.fuelUsedLitres>0,id+"/"+kit+": installed kit runs, revs and consumes fuel in shared simulation.");
                        var controller=car.mechanics.parts.Find(p=>p.slot=="Controls");controller.condition.failed=true;controller.condition.failureReason="Test injector controller failure";
                        car.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=1},true,false);
                        check(car.state.stalled && !car.state.engineSupplyAvailable && !car.state.engineFailed,id+"/"+kit+": controller failure stalls injection; engine internals remain intact.");
                        var session=new PhysicalDrivingSession(new RunSnapshot{vehicle=b.spec,tune=b.tune,driving=new DriverSettings{wearMode=WearMode.None}});
                        for(int i=0;i<600;i++)session.Step(new TickInput{tick=i,input=new DriverInput{throttle=i<420?.65:0},neutral=i<120});
                        var capture=JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.snapshot));
                        check(PhysicalDrivingSession.Replay(capture).Csv()==session.Csv(),id+"/"+kit+": injection hardware and controller replay deterministically after JSON serialization.");
                        check(catalog.Fits(b.spec,catalog.Find(catalog.Id(id,PartCategory.FuelSystem))),id+"/"+kit+": converted physical bench retains original carb slot fitment for downgrades.");
                    }
                    map[PartCategory.Controls]=id+"/Controls/EfiStreet";
                    var fixedTune=v.CreateStockTune();fixedTune.limiterCut=CutMethod.Fuel;
                    var fixedBuild=catalog.Resolve(id,map,fixedTune);
                    check(new EngineBehaviorProfile(fixedBuild.spec,fixedBuild.tune).cut==v.spec.engineBehavior.cut && fixedBuild.tune.controlLevel==0,id+"/"+kit+": fixed EFI controller does not silently grant programmable limiter/anti-lag hardware.");
                    map[PartCategory.Controls]=catalog.Id(id,PartCategory.Controls,1);map[PartCategory.RevLimiter]=catalog.Id(id,PartCategory.RevLimiter,1);map[PartCategory.TwoStep]=catalog.Id(id,PartCategory.TwoStep,1);
                    var adjustable=v.CreateStockTune();adjustable.limiterCut=adjustable.twoStepCut=CutMethod.Fuel;var fuelBuild=catalog.Resolve(id,map,adjustable);
                    var fuelProfile=new EngineBehaviorProfile(fuelBuild.spec,fuelBuild.tune);
                    check(fuelProfile.cut==CutMethod.Fuel && fuelProfile.launchCut==CutMethod.Fuel,id+"/"+kit+": programmable limiter/two-step can use injector fuel cut after conversion.");
                    var flowCar=new CarSimulation(fuelBuild.spec,fuelBuild.tune,new DriverSettings());flowCar.state.rpm=3500;flowCar.state.gear=0;flowCar.state.throttle=.8;flowCar.state.combustionFraction=.3;
                    flowCar.state.activeCutMethod=CutMethod.Ignition;double ignitionFlow=FuelDynamics.Flow(flowCar);flowCar.state.activeCutMethod=CutMethod.Fuel;double fuelFlow=FuelDynamics.Flow(flowCar);
                    check(ignitionFlow>0 && Math.Abs(fuelFlow/ignitionFlow-.3)<1e-10,id+"/"+kit+": fuel cut stops injector delivery in cut events; ignition cut continues requested fuel.");
                    flowCar.driver.fuelUsage=FuelUsageMode.Reduced;check(Math.Abs(FuelDynamics.Flow(flowCar)/fuelFlow-.25)<1e-10,id+"/"+kit+": Reduced fuel usage remains exactly 25% with injector cut.");
                    flowCar.driver.fuelUsage=FuelUsageMode.None;check(FuelDynamics.Flow(flowCar)==0,id+"/"+kit+": None fuel usage remains zero after conversion.");
                    adjustable.limiterCut=adjustable.twoStepCut=CutMethod.Ignition;var sparkBuild=catalog.Resolve(id,map,adjustable);var sparkProfile=new EngineBehaviorProfile(sparkBuild.spec,sparkBuild.tune);
                    check(sparkProfile.cut==CutMethod.Ignition && sparkProfile.launchCut==CutMethod.Ignition,id+"/"+kit+": programmable injection can still select ignition cut independently.");
                    map[PartCategory.FuelSystem]=catalog.Id(id,PartCategory.FuelSystem);map.Remove(PartCategory.Controls);var reverted=catalog.Resolve(id,map,adjustable);
                    check(reverted.Drivable && reverted.spec.engineBehavior.fueling==FuelSystem.Carburetor && new EngineBehaviorProfile(reverted.spec,reverted.tune).cut==CutMethod.Ignition && reverted.spec.engineBehavior.mixtureVariation==v.spec.engineBehavior.mixtureVariation,id+"/"+kit+": carb downgrade restores metering, sound behavior and ignition-only cut without requiring EFI controller.");
                }
                var player=PlayerProfile.New(DateTime.UtcNow.Ticks);player.cars.Clear();player.cars.Add(new OwnedCar{vehicleId=id,tune=v.CreateStockTune()});player.selectedVehicleId=id;player.cash=100000;MechanicsOwnership.Ensure(player,catalog);
                var owned=player.SelectedCar;string carb=owned.installed.slots.Find(s=>s.slot=="FuelSystem").partInstanceId;player.parts.Find(p=>p.instanceId==carb).condition.health=.62f;
                string conversion=PartOperations.Buy(player,catalog,owned.instanceId,id+"/FuelSystem/EfiPortInjection",true);PartOperations.Buy(player,catalog,owned.instanceId,id+"/Controls/EfiStreet",true);
                var saved=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(player));MechanicsOwnership.Ensure(saved,catalog);
                check(MechanicsOwnership.Resolve(saved,saved.SelectedCar,catalog).Drivable && saved.parts.Find(p=>p.instanceId==conversion)!=null && saved.parts.Find(p=>p.instanceId==carb).condition.health==.62f && saved.parts.Find(p=>p.instanceId==carb).location==PartLocation.Inventory,id+": bought conversion, original carb identity and existing wear survive save/reload.");
                PartOperations.Install(saved,catalog,saved.SelectedCar.instanceId,carb);
                check(MechanicsOwnership.Resolve(saved,saved.SelectedCar,catalog).spec.engineBehavior.fueling==FuelSystem.Carburetor && saved.parts.Find(p=>p.instanceId==carb).condition.health==.62f && saved.parts.Find(p=>p.instanceId==conversion).location==PartLocation.Inventory,id+": reusable carb restores original fueling without healing wear or discarding kit.");
            }
            string folder=Path.Combine(root,"reports/fueling-conversions");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"verification.txt"),log+"Checks: "+count+"\nCatalog definitions: "+catalog.parts.Count+"\n");return count;
        }
    }
}
