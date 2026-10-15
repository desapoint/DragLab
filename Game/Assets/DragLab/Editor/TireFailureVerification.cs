using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Editor {
 public static class TireFailureVerification {
  public static void RunStandalone(){string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));string folder=Path.Combine(root,"reports/phases/2j");Directory.CreateDirectory(folder);var log=new StringBuilder();int count=0;Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+label);File.WriteAllText(Path.Combine(folder,"failure-verification.txt"),log.ToString());if(!ok)throw new InvalidOperationException(label);count++;};
   var v=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").First(c=>c.spec.id=="v8-muscle");var catalog=new MechanicalCatalog(Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Select(vehicle=>vehicle.spec));var tune=v.CreateStockTune();tune.controlLevel=1;tune.limiter=LimiterStyle.Hard;tune.revLimitRpm=4000;tune.rearLeftGrip=tune.rearRightGrip=.2f;
   var car=new CarSimulation(v.spec.Copy(),tune,new DriverSettings {wearMode=WearMode.Full,wearMultiplier=5,throttleAssist=false,tractionAssist=false});var pack=car.mechanics.parts.Find(p=>p.slot=="Tires");var worn=pack.condition.tires[2];worn.health=.0001f;worn.consumedLife=1-worn.health;car.Launch(0);
   for(int tick=0;tick<2400&&!worn.failed;tick++)car.IndependentStep(DragMath.FixedStep,new DriverInput {throttle=1,brake=1},false,false);
   File.WriteAllText(Path.Combine(folder,"physical-tire-failure.json"),JsonUtility.ToJson(car.CaptureDynamics(),true));check(worn.failed&&worn.slipEnergyJ>0,"A nearly exhausted tire actually fails from gas/brake contact work");check(!car.state.engineFailed&&!car.state.drivetrainFailed&&car.state.rpm>0,"Physical tire failure leaves the engine and drivetrain running");
   var profile=PlayerProfile.New(DateTime.UtcNow.Ticks);profile.cars.Clear();profile.cars.Add(new OwnedCar {vehicleId=v.spec.id,tune=v.CreateStockTune()});profile.selectedVehicleId=v.spec.id;MechanicsOwnership.Ensure(profile,catalog);var owned=profile.parts.Find(p=>p.slot=="Tires");owned.condition=pack.condition.Copy();string identity=owned.instanceId;profile=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));MechanicsOwnership.Ensure(profile,catalog);owned=profile.parts.Find(p=>p.instanceId==identity);check(owned.condition.tires[2].failed&&owned.condition.tires[2].health==0,"Physics-driven failure survives owned-instance save/reload");
   int cash=profile.cash;string fresh=PartOperations.Buy(profile,catalog,profile.SelectedCar.instanceId,catalog.Id(v.spec.id,PartCategory.Tires),true);var replacement=profile.parts.Find(p=>p.instanceId==fresh);check(replacement.condition.health==1&&owned.location==PartLocation.Inventory&&owned.condition.tires[2].failed&&profile.cash==cash-catalog.Find(replacement.definitionId).price,"Buying a compatible replacement keeps the failed original and charges only its provisional price");
   var resolved=catalog.Resolve(v.spec.id,MechanicsOwnership.Definitions(profile,profile.SelectedCar),profile.SelectedCar.tune);var repaired=new CarSimulation(resolved.spec,resolved.tune,new DriverSettings(),profile.SelectedCar.installed.slots.Select(s=>profile.parts.Find(p=>p.instanceId==s.partInstanceId)));var newTires=repaired.mechanics.parts.Find(p=>p.slot=="Tires");check(newTires.condition.tires.All(t=>t.health==1&&!t.failed)&&TireDynamics.Grip(repaired.mechanics.Definition(newTires),newTires.condition.tires[2])==1,"Replacement restores fresh contact behavior without healing the old tires");
   File.AppendAllText(Path.Combine(folder,"failure-verification.txt"),"Checks: "+count);Debug.Log("TIRE FAILURE PASS: "+count);
  }
 }
}
