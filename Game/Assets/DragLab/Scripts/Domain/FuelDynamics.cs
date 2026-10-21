using System;
using DragLab.Domain.Mechanics;
namespace DragLab.Domain {
 public static class FuelDynamics {
  public static double Flow(CarSimulation car) {
   return Calculate(car.spec,car.tune,car.induction,car.engine,car.state,car.driver.FuelUsageRate,true);
  }
  public static double PhysicalFlow(VehicleSpec spec,Tune tune,InductionProfile induction,EngineBehaviorProfile engine,CarState s,bool requireTank=true) => Calculate(spec,tune,induction,engine,s,1,requireTank);
  static double Calculate(VehicleSpec spec,Tune tune,InductionProfile induction,EngineBehaviorProfile engine,CarState s,double usageRate,bool requireTank) {
   var fuel=spec.fuel;if(s.engineFailed||s.stalled||!s.engineSupplyAvailable||(requireTank&&s.fuelLitres<=0)||s.rpm<spec.idleRpm*.4)return 0;
   bool injection=engine.data.fueling!=FuelSystem.Carburetor;
   if(injection&&s.throttle<.01&&s.rpm>spec.idleRpm*1.6&&!s.antiLagActive)return 0;
   double fraction=s.activeCutMethod==CutMethod.Fuel?s.combustionFraction:1;
   double request=InductionDynamics.Torque(induction,s,s.rpm,s.throttle),kw=Math.Max(0,request*s.rpm/FlywheelDynamics.RpmPerRadian)/1000;
   kw/=Math.Max(.05,s.cylinderCombustionFactor*EngineElements.Ripple(s));
   double idle=fuel.IdleLitresPerHour*Math.Max(.5,spec.DisplacementLitres/2)/3600;
   double efficiency=Math.Max(.35,s.conditionCombustionEfficiency);
   double load=(kw+s.extraEngineFrictionNm*s.rpm/FlywheelDynamics.RpmPerRadian/1000)*fuel.BsfcKgPerKwh/Math.Max(.8,CombustionHardware.CompressionMultiplier(spec))/efficiency/fuel.densityKgPerLitre/3600;
   double neutralPumping=s.gear==0?s.throttle*s.rpm/spec.redlineRpm*idle*4:0;
   return Math.Max(0,(idle+load+neutralPumping)*(1+s.fuelMeteringWaste)*(1+spec.valvetrain.camTorqueBias*Math.Max(0,1-s.rpm/spec.redlineRpm)*.3)*(injection?1:1.15)*(s.antiLagActive?1.35:1)*fraction)*usageRate*s.cylinderFuelFactor;
  }
  public static double Refuel(CarSimulation car,double requested) {if(!MechanicsSchema.Finite(requested)||requested<0)throw new ArgumentException("Refuel litres must be finite and nonnegative.");double added=Math.Min(requested,Math.Max(0,car.spec.fuel.capacityLitres-car.state.fuelLitres));car.SetMassInputs(car.tune.driverMassKg,car.tune.payloadMassKg,car.tune.payloadPositionM,car.tune.payloadHeightM,car.state.fuelLitres+added);return added;}
  public static double Refuel(FuelState state,FuelSpec spec,double requested){if(state==null||state.Validate(spec)!=null||!MechanicsSchema.Finite(requested)||requested<0)throw new ArgumentException("Invalid refuel state or quantity.");double added=Math.Min(requested,spec.capacityLitres-state.litres);state.litres+=added;state.flowLitresPerSecond=0;return added;}
 }
 public sealed partial class CarSimulation {
  public FuelState FuelSnapshot(){return new FuelState {litres=state.fuelLitres,cumulativeUsedLitres=state.fuelUsedLitres,flowLitresPerSecond=state.fuelFlowLitresPerSecond};}
  void ConsumeFuel(double dt){if(state.audioExhaust!=null){double physical=FuelDynamics.PhysicalFlow(spec,tune,induction,engine,state);if(driver.FuelUsageRate>0)physical=Math.Min(physical,state.fuelLitres/(dt*driver.FuelUsageRate));state.audioExhaust.Step(spec,induction,state,physical,dt);}double used=Math.Min(state.fuelLitres,FuelDynamics.Flow(this)*dt);state.fuelLitres=Math.Max(0,state.fuelLitres-used);state.fuelUsedLitres+=used;state.fuelFlowLitresPerSecond=used/dt;UpdateMass();if(state.fuelLitres<=0){state.fuelStarved=true;state.engineSupplyAvailable=false;state.stalled=true;state.engineHorsepower=state.engineTorqueNm=0;state.feedback="OUT OF FUEL / COAST, REFUEL + RESTART";state.feedbackRemaining=2;}}
 }
}
