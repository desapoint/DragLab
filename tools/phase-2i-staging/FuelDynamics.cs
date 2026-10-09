using System;
using DragLab.Domain.Mechanics;
namespace DragLab.Domain {
 public static class FuelDynamics {
  public static double Flow(CarSimulation car) {
   var s=car.state;var fuel=car.spec.fuel;if(s.engineFailed||s.stalled||!s.engineSupplyAvailable||s.fuelLitres<=0||s.rpm<car.spec.idleRpm*.4)return 0;
   bool injection=car.engine.data.fueling==FuelSystem.Injection;
   if(injection&&s.throttle<.01&&s.rpm>car.spec.idleRpm*1.6&&!s.antiLagActive)return 0;
   double fraction=s.activeCutMethod==CutMethod.Fuel?s.combustionFraction:1;
   double request=InductionDynamics.Torque(car.induction,s,s.rpm,s.throttle),kw=Math.Max(0,request*s.rpm/FlywheelDynamics.RpmPerRadian)/1000;
   double idle=fuel.idleLitresPerHour*Math.Max(.5,car.spec.DisplacementLitres/2)/3600;
   double load=kw*fuel.bsfcKgPerKwh/fuel.densityKgPerLitre/3600;
   double neutralPumping=s.gear==0?s.throttle*s.rpm/car.spec.redlineRpm*idle*4:0;
   return Math.Max(0,(idle+load+neutralPumping)*(injection?1:1.15)*(s.antiLagActive?1.35:1)*fraction);
  }
  public static double Refuel(CarSimulation car,double requested) {if(!MechanicsSchema.Finite(requested)||requested<0)throw new ArgumentException("Refuel litres must be finite and nonnegative.");double added=Math.Min(requested,Math.Max(0,car.spec.fuel.capacityLitres-car.state.fuelLitres));car.SetMassInputs(car.tune.driverMassKg,car.tune.payloadMassKg,car.tune.payloadPositionM,car.tune.payloadHeightM,car.state.fuelLitres+added);return added;}
  public static double Refuel(FuelState state,FuelSpec spec,double requested){if(state==null||state.Validate(spec)!=null||!MechanicsSchema.Finite(requested)||requested<0)throw new ArgumentException("Invalid refuel state or quantity.");double added=Math.Min(requested,spec.capacityLitres-state.litres);state.litres+=added;state.flowLitresPerSecond=0;return added;}
 }
 public sealed partial class CarSimulation {
  public FuelState FuelSnapshot(){return new FuelState {litres=state.fuelLitres,cumulativeUsedLitres=state.fuelUsedLitres,flowLitresPerSecond=state.fuelFlowLitresPerSecond};}
  void ConsumeFuel(double dt){double used=Math.Min(state.fuelLitres,FuelDynamics.Flow(this)*dt);state.fuelLitres=Math.Max(0,state.fuelLitres-used);state.fuelUsedLitres+=used;state.fuelFlowLitresPerSecond=used/dt;UpdateMass();if(state.fuelLitres<=0){state.fuelStarved=true;state.engineSupplyAvailable=false;state.stalled=true;state.feedback="OUT OF FUEL / COAST, REFUEL + RESTART";state.feedbackRemaining=2;}}
 }
}
