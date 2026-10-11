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
 public static class VarietyMechanicalVerification {
 public static void RunStandalone(){Run();}
 public static void Run(){
 int count=0;var log=new StringBuilder();Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);count++;log.AppendLine("PASS "+label);};
 var catalog=MechanicsTransactions.Catalog();
 foreach(var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")){
 var map=catalog.Recipe(v.spec.id);var b=catalog.Resolve(v.spec.id,map,v.CreateStockTune());
 check(b.Drivable&&b.spec.Validate()==null,v.spec.id+" valid stock assembly");
 var car=new CarSimulation(b.spec,b.tune,new DriverSettings{wearMode=WearMode.Full});car.Launch(0);
 for(int i=0;i<1500&&!car.state.finished;i++)car.Step(DragMath.FixedStep,new DriverInput{throttle=1});
 check(!car.state.engineFailed&&car.state.knockSeverity==0&&car.state.valveBind==0&&car.state.valveContact==0,v.spec.id+" healthy stock pass avoids combustion/clearance damage");
 if(CombustionHardware.Diesel(v.spec))continue;
 map[PartCategory.Pistons]=v.spec.id+"/Pistons/Dished";var dish=catalog.Resolve(v.spec.id,map,v.CreateStockTune());map[PartCategory.Pistons]=v.spec.id+"/Pistons/Domed";var dome=catalog.Resolve(v.spec.id,map,v.CreateStockTune());
 check(dish.spec.combustion.compressionRatio<b.spec.combustion.compressionRatio&&dome.spec.combustion.compressionRatio>b.spec.combustion.compressionRatio,v.spec.id+" crown volume changes compression in both directions");
 check(InductionDynamics.SteadyTorque(dome.spec,dome.tune,3500)>InductionDynamics.SteadyTorque(dish.spec,dish.tune,3500),v.spec.id+" compression changes dyno torque");
 var abuse=new CarSimulation(dome.spec,dome.tune,new DriverSettings{wearMode=WearMode.Full});abuse.state.rpm=4500;abuse.state.throttle=1;abuse.state.boostBar=1.5;abuse.mechanics.Prepare(abuse.state);ServiceDynamics.Prepare(abuse);var piston=abuse.mechanics.Part(PartCategory.Pistons);double health=piston.condition.health;CombustionHardware.Tick(abuse,1);
 check(abuse.state.knockSeverity>0&&piston.condition.health<health,v.spec.id+" loaded high-compression knock causes retained wear");
 abuse.driver.wearMode=WearMode.None;health=piston.condition.health;CombustionHardware.Tick(abuse,1);check(piston.condition.health==health,v.spec.id+" wear difficulty remains authoritative");
 map=catalog.Recipe(v.spec.id);map[PartCategory.Camshaft]=catalog.Id(v.spec.id,PartCategory.Camshaft,2);var wrong=catalog.Resolve(v.spec.id,map,v.CreateStockTune());check(wrong.warnings.Any(w=>w.Contains("coil binding")),v.spec.id+" cam travel mismatch explained");
 map[PartCategory.ValveSprings]=catalog.Id(v.spec.id,PartCategory.ValveSprings,1);var matched=catalog.Resolve(v.spec.id,map,v.CreateStockTune());check(!matched.warnings.Any(w=>w.Contains("coil binding")),v.spec.id+" matched spring travel clears binding warning");
 if(v.spec.engineBehavior.fueling==FuelSystem.Carburetor){map[PartCategory.Camshaft]=v.spec.id+"/Camshaft/RollerStreet";check(!catalog.Resolve(v.spec.id,map).Drivable,v.spec.id+" mismatched roller actuation rejected");map[PartCategory.Followers]=v.spec.id+"/Followers/RollerConversion";check(catalog.Resolve(v.spec.id,map).Drivable,v.spec.id+" matched roller conversion runs");}
 }
 var profile=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(profile,catalog);var owner=profile.SelectedCar;var spark=profile.parts.Single(p=>p.ownerId==owner.engineAssemblyId&&p.slot=="SparkPlugs");var coil=profile.parts.Single(p=>p.ownerId==owner.engineAssemblyId&&p.slot=="Ignition");
 string id=spark.condition.elements[0].id;spark.condition.elements[0].health=.2f;spark.condition.elements[0].fault=ElementFault.Disabled;var plugs=spark.condition.elements;
 coil.condition.elements=plugs.Concat(coil.condition.elements).ToArray();profile.parts.Remove(spark);owner.installed.slots.RemoveAll(s=>s.slot=="SparkPlugs");profile.ignitionHardwareVersion=0;
 MechanicsOwnership.Ensure(profile,catalog);spark=profile.parts.Single(p=>p.ownerId==owner.engineAssemblyId&&p.slot=="SparkPlugs");
 check(spark.condition.elements[0].id==id&&spark.condition.elements[0].health==.2f&&spark.condition.elements[0].fault==ElementFault.Disabled,"legacy plugs retain exact identity, wear and fault");
 check(coil.condition.elements.All(e=>e.role=="ignition coil"),"legacy split retains separate coils");
 var serialized=JsonUtility.ToJson(profile);MechanicsOwnership.Ensure(profile,catalog);check(serialized==JsonUtility.ToJson(profile),"migration idempotent");
 int price=EngineElementService.Price(catalog.Find(spark.definitionId),spark.condition);check(price<catalog.Find(spark.definitionId).price,"single spark plug cheaper than a set");
 MechanicsOwnership.Remove(profile,owner,"SparkPlugs");MechanicsOwnership.Ensure(profile,catalog);check(!owner.installed.slots.Any(s=>s.slot=="SparkPlugs")&&!MechanicsOwnership.Resolve(profile,owner,catalog).Drivable,"normalization never recreates removed plugs");
 check(catalog.parts.All(p=>!string.IsNullOrEmpty(p.brand)&&!p.name.Contains(catalog.Spec(p.id.Split('/')[0]).displayName)),"fictional model-independent brands preserve catalog identities");
 var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));var folder=Path.Combine(root,"reports/variety");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"mechanical-verification.txt"),log+"TOTAL "+count,new UTF8Encoding(false));Debug.Log("Variety mechanical checks: "+count);
 }
 }
}
