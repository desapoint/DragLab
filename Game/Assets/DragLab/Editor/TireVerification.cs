using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Editor {
 public static class TireVerification {
  public static void RunStandalone(){Debug.Log("TIRE PASS: "+Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../.."))));}
  static PartInstance Pack(CarSimulation car){return car.mechanics.parts.Find(p=>p.slot=="Tires");}
  static CarSimulation Car(VehicleDefinition v,int compound,WearMode wear=WearMode.Full){var t=v.CreateStockTune();t.tireLevel=compound;return new CarSimulation(v.spec.Copy(),t,new DriverSettings {wearMode=wear,throttleAssist=false,tractionAssist=false});}
  static void Work(CarSimulation car,double joules,double distance=0){for(int i=0;i<4;i++)car.state.contact.slipWorkJ[i]+=joules;car.state.distance+=distance;TireDynamics.Tick(car,DragMath.FixedStep,car.state.distance-distance);}
  public static int Run(string root){int count=0;var log=new StringBuilder();string folder=Path.Combine(root,"reports/phases/2j");Directory.CreateDirectory(folder);Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+label);File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString());if(!ok)throw new InvalidOperationException(label);count++;};
   foreach(var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")){
    double previousGrip=0,previousLoss=0;
    for(int compound=0;compound<4;compound++) {var car=Car(v,compound);var p=Pack(car);var d=car.mechanics.Definition(p);Work(car,100000,1000);double loss=1-p.condition.tires[0].health;
     check(d.grip>previousGrip&&loss>previousLoss,"Higher compound grip trades sliding/rolling lifetime: "+v.spec.id+" / "+compound+" / loss "+loss);previousGrip=d.grip;previousLoss=loss;
     check(p.condition.tires.All(t=>t.temperatureC>20&&t.slipEnergyJ==100000&&t.distanceM==1000),"Actual contact work heats and persists four tire histories: "+v.spec.id+" / "+compound);
      check(p.condition.health==1&&!p.condition.failed&&p.condition.temperatureC==20&&p.condition.distanceM==0&&p.condition.slipEnergyJ==0&&p.condition.runningSeconds==0,"Tire container retains no simulated wear/heat/history: "+v.spec.id+" / "+compound);
     check(TireDynamics.Grip(d,new TireCondition {temperatureC=200})<TireDynamics.Grip(d,new TireCondition {temperatureC=d.tireOptimalC}),"Overheated tire loses grip: "+v.spec.id+" / "+compound);
     var roadTune=v.CreateStockTune();roadTune.tireLevel=compound;var road=new PhysicalDrivingSession(new RunSnapshot{vehicle=v.spec.Copy(),tune=roadTune,driving=new DriverSettings {wearMode=WearMode.Full,throttleAssist=false,tractionAssist=false}});foreach(var command in PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake,41))road.Step(command);File.WriteAllText(Path.Combine(folder,v.spec.id+"-compound-"+compound+"-road.csv"),road.Csv());check(Pack(road.car).condition.tires.Any(t=>t.slipEnergyJ>0&&t.health<1)&&road.car.state.contact.Validate()==null,"Full-road compound trace contains measured contact heat/wear: "+v.spec.id+" / "+compound);
     double hot=p.condition.tires[0].temperatureC;for(int tick=0;tick<1200;tick++)TireDynamics.Tick(car,DragMath.FixedStep,car.state.distance);check(p.condition.tires[0].temperatureC<hot,"Unpowered tire cools without fictitious sliding heat: "+v.spec.id+" / "+compound);
    }
    var none=Car(v,0,WearMode.None);var partial=Car(v,0,WearMode.Partial);var full=Car(v,0);Work(none,500000);Work(partial,500000);Work(full,500000);
    double partialLoss=Pack(partial).condition.tires[0].consumedLife,fullLoss=Pack(full).condition.tires[0].consumedLife;
    check(TireDynamics.LowestHealth(Pack(none).condition)==1&&Math.Abs(partialLoss*4-fullLoss)<1e-12,"None/Partial 25%/Full use the same work and proportional life: "+v.spec.id);
    check(TireDynamics.HottestTemperatureC(Pack(none).condition)==TireDynamics.HottestTemperatureC(Pack(full).condition),"Wear difficulty does not turn off tire heat: "+v.spec.id);
    var rolling=Car(v,0);for(int i=0;i<12000;i++)Work(rolling,0,.1);check(TireDynamics.LowestHealth(Pack(rolling).condition)<.999999f,"Double precision rolling wear accumulates across tiny ticks: "+v.spec.id);
    var pressure=Car(v,0);pressure.tune.tirePressurePsi=42;Work(pressure,100000);var normal=Car(v,0);normal.tune.tirePressurePsi=28;Work(normal,100000);check(TireDynamics.LowestHealth(Pack(pressure).condition)<TireDynamics.LowestHealth(Pack(normal).condition),"Pressure abuse shortens equal-work life: "+v.spec.id);
    var large=Car(v,0);var largeTune=v.CreateStockTune();largeTune.wheelDiameter=1.5f;large=new CarSimulation(v.spec.Copy(),largeTune,new DriverSettings{wearMode=WearMode.Full});Work(large,100000);check(TireDynamics.HottestTemperatureC(Pack(large).condition)<TireDynamics.HottestTemperatureC(Pack(normal).condition)&&TireDynamics.LowestHealth(Pack(large).condition)>TireDynamics.LowestHealth(Pack(normal).condition),"Larger rubber has greater heat/sliding reserve, alongside mass/inertia cost: "+v.spec.id);
    var lightTune=v.CreateStockTune();lightTune.wheelMass=.8f;var light=new WheelProfile(v.spec,lightTune);var stock=new WheelProfile(v.spec,v.CreateStockTune());check(light.tireMassKg==stock.tireMassKg&&light.rimMassKg<stock.rimMassKg&&light.totalInertia<stock.totalInertia,"Light rims reduce mass/inertia without removing rubber: "+v.spec.id);
    var damaged=Car(v,0);var tire=Pack(damaged).condition.tires[0];tire.health=0;tire.failed=true;tire.consumedLife=1;Work(damaged,0);damaged.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=.2},true,false);
    check(!damaged.state.engineFailed&&!damaged.state.drivetrainFailed&&TireDynamics.Grip(damaged.mechanics.Definition(Pack(damaged)),tire)==.025&&Pack(damaged).condition.tires[1].health>0,"Single failed tire loses grip without destroying engine or other tires: "+v.spec.id);
    var copy=Pack(damaged).Copy();copy.condition.tires[1].health=.3f;check(Pack(damaged).condition.tires[1].health!=.3f,"Owned condition copies isolate per-wheel state: "+v.spec.id);
    damaged.mechanics.ResetWear(damaged.state);Work(damaged,0);check(Pack(damaged).condition.tires.All(t=>t.health==1&&t.slipEnergyJ==0),"Repair ignores old cumulative contact work: "+v.spec.id);
    var reused=Pack(normal).Copy();var usedCar=new CarSimulation(v.spec.Copy(),v.CreateStockTune(),new DriverSettings(),normal.mechanics.parts);Work(usedCar,0);check(Math.Abs(TireDynamics.LowestHealth(Pack(usedCar).condition)-TireDynamics.LowestHealth(reused.condition))<1e-7&&Pack(usedCar).condition.tires.Zip(reused.condition.tires,(a,b)=>Math.Abs(a.slipEnergyJ-b.slipEnergyJ)<1e-7).All(ok=>ok),"Reused pack retains wear, resets only contact-counter reference: "+v.spec.id);
    var session=new PhysicalDrivingSession(new RunSnapshot{vehicle=v.spec.Copy(),tune=v.CreateStockTune(),driving=new DriverSettings {wearMode=WearMode.Full,throttleAssist=false,tractionAssist=false}});
    foreach(var command in PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake,41))session.Step(command);
    check(Pack(session.car).condition.tires.Any(t=>t.slipEnergyJ>0&&t.health<1),"Full physical pull/shift/coast/brake produces tire wear: "+v.spec.id);
    var continuation=session.ContinueRecording();for(int tick=0;tick<120;tick++)continuation.Step(new TickInput{tick=continuation.Tick,neutral=true,input=new DriverInput {brake=.5}});
    check(continuation.Csv()==PhysicalDrivingSession.Replay(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(continuation.snapshot))).Csv(),"Serialized checkpoint retains exact per-wheel thermal/wear trace: "+v.spec.id);
    File.WriteAllText(Path.Combine(folder,v.spec.id+"-tires.csv"),session.Csv());
   }
   var legacy=new ComponentCondition {health=.4f,temperatureC=70,slipEnergyJ=400};
   bool rejectedLegacy=false;try{TireDynamics.Ensure(legacy);}catch(InvalidOperationException){rejectedLegacy=true;}
   check(rejectedLegacy&&legacy.elements.Length==0&&legacy.health==.4f&&legacy.slipEnergyJ==400,"Legacy aggregate tire condition must not regenerate physical tire histories");
   var fresh=new ComponentCondition();TireDynamics.Ensure(fresh);TireDynamics.Ensure(fresh);
   check(fresh.tires.Length==4&&fresh.tires.All(t=>t.health==1&&!t.failed&&t.temperatureC==20),"Fresh tire members initialize idempotently without inventing wear");
   var retainedFailure=Car(Resources.LoadAll<VehicleDefinition>("Data/Vehicles")[0],0);Pack(retainedFailure).condition.tires[0].failed=true;Pack(retainedFailure).condition.tires[0].failureReason="Original tire failure cause";retainedFailure.mechanics.Prepare(retainedFailure.state);Work(retainedFailure,10000);check(Pack(retainedFailure).condition.tires[0].failed&&Pack(retainedFailure).condition.tires[0].failureReason=="Original tire failure cause"&&Pack(retainedFailure).condition.failed==false,"Existing tire failure flags and diagnostic cause survive continued simulation");
   var broken=new ComponentCondition{elements=new EngineElement[]{new TireCondition(),new TireCondition()}};
   bool rejectedMalformed=false;try{TireDynamics.Ensure(broken);}catch(InvalidOperationException){rejectedMalformed=true;}catch(ArgumentException){rejectedMalformed=true;}
   check(rejectedMalformed,"Malformed per-wheel conditions are rejected");
   var catalog=new MechanicalCatalog(Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Select(v=>v.spec));var profile=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(profile,catalog);var owned=profile.parts.Find(p=>p.slot=="Tires");TireDynamics.Ensure(owned.condition);owned.condition.tires[2].health=.35f;owned.condition.tires[2].consumedLife=.65;owned.condition.tires[2].temperatureC=110;var loaded=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));MechanicsOwnership.Ensure(loaded,catalog);var retained=loaded.parts.Find(p=>p.instanceId==owned.instanceId);check(retained.condition.tires[2].health==.35f&&retained.condition.tires[2].temperatureC==110,"Owned save reload retains individual tire wear/heat and identity");
   PartOperations.Remove(loaded,loaded.SelectedCar.instanceId,PartCategory.Tires);check(retained.location==PartLocation.Inventory&&retained.condition.tires[2].health==.35f,"Removing tire pack retains identity and individual wear in inventory");
   PartOperations.Install(loaded,catalog,loaded.SelectedCar.instanceId,retained.instanceId);check(retained.location==PartLocation.Vehicle&&retained.condition.tires[2].health==.35f,"Reinstalling retained tires does not heal them");
   string freshId=PartOperations.Buy(loaded,catalog,loaded.SelectedCar.instanceId,catalog.Id(loaded.SelectedCar.vehicleId,PartCategory.Tires),true);check(TireDynamics.LowestHealth(loaded.parts.Find(p=>p.instanceId==freshId).condition)==1&&retained.location==PartLocation.Inventory&&retained.condition.tires[2].health==.35f,"Compatible fresh replacement retains the worn original unchanged");
   var v8=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").First(v=>v.spec.id=="v8-muscle");var bt=v8.CreateStockTune();bt.controlLevel=1;bt.limiter=LimiterStyle.Hard;bt.revLimitRpm=4000;bt.frontLeftGrip=bt.frontRightGrip=bt.rearLeftGrip=bt.rearRightGrip=.15f;
   var burnout=new CarSimulation(v8.spec.Copy(),bt,new DriverSettings {wearMode=WearMode.Full,throttleAssist=false,tractionAssist=false});burnout.Launch(0);for(int tick=0;tick<600;tick++)burnout.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=1,brake=1},false,false);
   File.WriteAllText(Path.Combine(folder,"burnout.json"),JsonUtility.ToJson(burnout.CaptureDynamics(),true));check(Pack(burnout).condition.tires[2].slipEnergyJ>1000&&Pack(burnout).condition.tires[2].temperatureC>20&&Pack(burnout).condition.tires[2].health<1&&!burnout.state.engineFailed,"Ordinary gas/brake burnout produces rear sliding work, heat and wear without invented damage");
   var accelerated=Car(v8,0);accelerated.driver.wearMultiplier=5;var ordinary=Car(v8,0);Work(accelerated,100000);Work(ordinary,100000);check(Math.Abs(Pack(accelerated).condition.tires[0].consumedLife-5*Pack(ordinary).condition.tires[0].consumedLife)<1e-12,"Wear multiplier scales tire life from the same physical work");
   var optimal=Car(v8,3);optimal.state.optimalThermals=true;Work(optimal,100000);check(Pack(optimal).condition.tires.All(t=>t.temperatureC==90),"Optimal thermals keeps each tire at its compound optimum");
   var open=new CarSimulation(v8.spec.Copy(),bt,new DriverSettings {wearMode=WearMode.None,throttleAssist=false,tractionAssist=false});var solidSpec=v8.spec.Copy();solidSpec.rearDifferential=new DifferentialSpec {kind=DifferentialKind.Spool};var solid=new CarSimulation(solidSpec,bt,new DriverSettings {wearMode=WearMode.None,throttleAssist=false,tractionAssist=false});
   foreach(var car in new[]{open,solid}){var t=Pack(car).condition.tires[2];t.health=0;t.failed=true;t.consumedLife=1;Work(car,0);car.Launch(0);for(int tick=0;tick<480;tick++)car.IndependentStep(DragMath.FixedStep,new DriverInput {throttle=1},false,false);}
   check(solid.state.speed>open.state.speed&&!solid.state.engineFailed&&!open.state.engineFailed,"One failed rear tire limits an open differential more than a solid axle while engine continues running");
   File.AppendAllText(Path.Combine(folder,"verification.txt"),"Checks: "+count);return count;
  }
 }
}


