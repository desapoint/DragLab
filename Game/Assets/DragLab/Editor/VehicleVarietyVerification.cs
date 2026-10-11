using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Editor {
 public static class VehicleVarietyVerification {
 public static void RunStandalone(){
 VehicleVarietySetup.Prepare();
 int checks=0;var log=new StringBuilder();Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);checks++;log.AppendLine("PASS "+label);};
 var cat=MechanicsTransactions.Catalog();var cars=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
 check(cars.Length>=13&&cars.Select(v=>v.spec.id).Distinct().Count()==cars.Length,"13 unique authored vehicles/trims");
 foreach(var v in cars){var b=cat.Resolve(v.spec.id,cat.Recipe(v.spec.id),v.CreateStockTune());check(b.Drivable&&b.spec.Validate()==null,v.spec.id+" stock hardware resolves");var c=new CarSimulation(b.spec,b.tune,new DriverSettings{wearMode=WearMode.Full});c.Launch(0);for(int i=0;i<3600&&c.state.distance<402.336;i++)c.Step(DragMath.FixedStep,new DriverInput{throttle=1});check(c.state.distance>=402.336&&!c.state.engineFailed&&c.mechanics.parts.All(p=>!p.condition.failed),v.spec.id+" full-wear factory quarter mile survives");
 if(!CombustionHardware.Diesel(v.spec))continue;
 check(c.mechanics.Part(PartCategory.Ignition)==null&&c.mechanics.Part(PartCategory.SparkPlugs)==null&&c.mechanics.Part(PartCategory.GlowPlugs)!=null,v.spec.id+" diesel heaters and injectors replace spark equipment");
 check(c.engine.cut==CutMethod.Fuel&&c.engine.launchCut==CutMethod.Fuel&&!c.engine.antiLag&&!c.engine.preload,v.spec.id+" compression ignition prevents gasoline exhaust-retard controls");
 var hostile=b.tune.Copy();hostile.limiterCut=hostile.twoStepCut=CutMethod.Ignition;hostile.antiLag=hostile.launchPreload=EquipmentSetting.On;hostile.controlLevel=1;hostile.partLimiterAdjustable=hostile.partTwoStepAdjustable=true;var controls=new EngineBehaviorProfile(b.spec,hostile);check(controls.cut==CutMethod.Fuel&&!controls.antiLag&&!controls.preload,v.spec.id+" tune cannot fabricate spark hardware");
 var cold=new CarSimulation(b.spec,b.tune,new DriverSettings());cold.state.engineTemperature=0;ServiceDynamics.Prepare(cold);double healthy=cold.state.dieselColdFactor;var glow=cold.mechanics.Part(PartCategory.GlowPlugs);glow.condition.health=0;glow.condition.failed=true;glow.condition.failureReason="Assigned heater failure";ServiceDynamics.Prepare(cold);check(cold.state.dieselColdFactor<healthy&&!cold.state.engineFailed,v.spec.id+" failed glow heaters worsen cold combustion only");cold.state.engineTemperature=1;ServiceDynamics.Prepare(cold);check(cold.state.dieselColdFactor==1&&!cold.state.stalled,v.spec.id+" warm diesel does not need spark/heater firing");
 cold.state.rpm=3000;cold.state.throttle=1;check(FuelDynamics.Flow(cold)>0&&cold.spec.fuel.densityKgPerLitre>.8,v.spec.id+" diesel fuel mass and metering");
 cold.state.turboBoostBar=.8;InductionDynamics.Step(cold.induction,cold.state,1,1,false,false,DragMath.FixedStep);InductionDynamics.Step(cold.induction,cold.state,0,0,false,false,DragMath.FixedStep);check(cold.state.boostReleaseCount==0,v.spec.id+" diesel lift has no throttle-plate blow-off event");
 var audio=new float[48000];new EngineSoundSynth(new EngineSoundProfile(b.spec,b.tune)).Fill(audio,1,48000,new EngineSoundFrame(2200,.8,.8,0,1));var petrol=b.spec.Copy();petrol.engineBehavior.fueling=FuelSystem.Injection;var other=new float[48000];new EngineSoundSynth(new EngineSoundProfile(petrol,b.tune)).Fill(other,1,48000,new EngineSoundFrame(2200,.8,.8,0,1));check(audio.All(x=>!float.IsNaN(x)&&!float.IsInfinity(x))&&audio.Zip(other,(x,y)=>Math.Abs(x-y)).Sum()>10,v.spec.id+" finite distinct diesel sound");
 check(!cat.Fits(v.spec,cat.Find(cat.Id("starter-hatch",PartCategory.SparkPlugs))),v.spec.id+" petrol hardware not interchangeable with diesel");
 }
 check(cat.Fits(cat.Spec("juniper-club"),cat.Find(cat.Id("starter-hatch",PartCategory.Pistons))),"same-engine hatch trims share actual piston family");check(!cat.Fits(cat.Spec("juniper-economy"),cat.Find(cat.Id("starter-hatch",PartCategory.Pistons))),"different bore/family economy internals excluded");check(cat.Recipe("kestrel-track").All(k=>cat.Find(k.Value)!=null),"Track trim retains complete factory recipe");
 var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/variety"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"vehicle-verification.txt"),log+"TOTAL "+checks,new UTF8Encoding(false));Debug.Log("Vehicle variety checks: "+checks);
 }
 }
}
