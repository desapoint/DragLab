using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Editor {
 public static class FuelVerification {
  public static void RunStandalone(){Debug.Log("FUEL PASS: "+Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../.."))));}
  public static int Run(string root){int count=0;var log=new StringBuilder();string folder=Path.Combine(root,"reports/phases/2i");Directory.CreateDirectory(folder);Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+label);File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString());if(!ok)throw new InvalidOperationException(label);count++;};
   foreach(var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")){var d=new DriverSettings {wearMode=WearMode.None,throttleAssist=false};var idle=new CarSimulation(v.spec.Copy(),v.CreateStockTune(),d);double initial=idle.state.fuelLitres,mass=idle.mass.TranslatingKg;
    for(int tick=0;tick<7200;tick++)idle.IndependentStep(DragMath.FixedStep,new DriverInput(),true,false);
    check(idle.state.fuelLitres<initial&&idle.state.fuelLitres>initial-.2&&idle.state.fuelUsedLitres>0,"Running idle burns finite fuel: "+v.spec.id+" / "+idle.state.fuelUsedLitres+" L");
    check(Math.Abs(mass-idle.mass.TranslatingKg-idle.state.fuelUsedLitres*idle.spec.fuel.densityKgPerLitre-(idle.state.fluids.oilConsumedLitres+idle.state.fluids.oilLeakedLitres)*.86-idle.state.fluids.coolantLostLitres*1.05)<1e-7,"Fuel and service-fluid losses change actual mass exactly once: "+v.spec.id);
    var rev=new CarSimulation(v.spec.Copy(),v.CreateStockTune(),d);for(int tick=0;tick<1200;tick++)rev.IndependentStep(DragMath.FixedStep,new DriverInput {throttle=.4},true,false);
    check(rev.state.fuelUsedLitres>idle.state.fuelUsedLitres/6,"Neutral revving consumes more than the equivalent idle duration: "+v.spec.id);
    idle.SetMassInputs(75,0,1.5,.45,.0001);for(int tick=0;tick<1200;tick++)idle.IndependentStep(DragMath.FixedStep,new DriverInput(),true,false);
    check(idle.state.fuelLitres==0&&idle.state.stalled&&idle.state.rpm==0&&!idle.state.engineFailed,"Empty tank stops combustion without destroying engine: "+v.spec.id);
    double used=idle.state.fuelUsedLitres;FuelDynamics.Refuel(idle,1);check(idle.state.fuelLitres==1&&idle.state.stalled&&idle.state.fuelUsedLitres==used,"Refuel preserves usage and requires restart: "+v.spec.id);
    idle.IndependentStep(DragMath.FixedStep,new DriverInput {launch=true},true,false);check(!idle.state.stalled&&idle.state.rpm>=v.spec.idleRpm*.95&&idle.state.fuelFlowLitresPerSecond>0,"Refuel plus explicit restart restores healthy idle: "+v.spec.id);
    double before=idle.state.fuelLitres;double added=FuelDynamics.Refuel(idle,10000);check(added==idle.spec.fuel.capacityLitres-before&&idle.state.fuelLitres==idle.spec.fuel.capacityLitres,"Refuel is capped to tank free space: "+v.spec.id);
    bool rejected=false;try{FuelDynamics.Refuel(idle,double.NaN);}catch(ArgumentException){rejected=true;}check(rejected,"Nonfinite refuel rejected: "+v.spec.id);
    var snap=new RunSnapshot {vehicle=v.spec.Copy(),tune=v.CreateStockTune(),driving=d,startingFuel=new FuelState {litres=3}};var session=new PhysicalDrivingSession(snap);foreach(var c in PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake,7))session.Step(c);File.WriteAllText(Path.Combine(folder,v.spec.id+"-fuel.csv"),session.Csv());
    check(session.Csv()==PhysicalDrivingSession.Replay(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.snapshot))).Csv(),"Full fuel/mass/gear trace repeats exactly: "+v.spec.id);
    double speed=session.car.state.speed,rpm=session.car.state.rpm;FuelDynamics.Refuel(session.car,2);var next=session.ContinueRecording();check(next.car.state.speed==speed&&next.car.state.rpm==rpm&&next.car.state.fuelLitres==session.car.state.fuelLitres,"Live refuel checkpoint retains wheel/engine momentum: "+v.spec.id);
   }
   var spec=new VehicleSpec();var injected=new CarSimulation(spec,new Tune(),new DriverSettings {throttleAssist=false});injected.state.gear=2;injected.state.rpm=4000;injected.state.throttle=0;check(FuelDynamics.Flow(injected)==0,"Injection overrun fuel cut consumes no fuel");spec.engineBehavior.fueling=FuelSystem.Carburetor;var carb=new CarSimulation(spec,new Tune(),new DriverSettings {throttleAssist=false});carb.state.gear=2;carb.state.rpm=4000;check(FuelDynamics.Flow(carb)>0,"Carburetor continues fueling on overrun");injected.state.throttle=1;injected.state.activeCutMethod=CutMethod.Fuel;injected.state.combustionFraction=0;check(FuelDynamics.Flow(injected)==0,"Fuel-cut limiter consumes no injected fuel");injected.state.activeCutMethod=CutMethod.Ignition;check(FuelDynamics.Flow(injected)>0,"Ignition cut can continue consuming unburned fuel");double ordinary=FuelDynamics.Flow(injected);injected.state.antiLagActive=true;check(FuelDynamics.Flow(injected)>ordinary,"Anti-lag increases fuel demand independently of wheel speed");
   var catalog=new MechanicalCatalog(Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Select(v=>v.spec));var profile=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(profile,catalog);var owned=profile.SelectedCar;owned.fuel.litres=5;int cash=profile.cash;FuelDynamics.Refuel(owned.fuel,catalog.Spec(owned.vehicleId).fuel,2);var loaded=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));MechanicsOwnership.Ensure(loaded,catalog);check(loaded.SelectedCar.fuel.litres==7&&loaded.cash==cash,"Owned fuel roundtrips and free refuel does not spend cash");
   File.AppendAllText(Path.Combine(folder,"verification.txt"),"Checks: "+count);return count+FuelUsageVerification.Run(root);
  }
 }
}
