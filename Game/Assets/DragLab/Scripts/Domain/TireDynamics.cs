using System;
using System.Linq;
using DragLab.Domain.Mechanics;
namespace DragLab.Domain {
 [Serializable] public sealed class TireCondition : EngineElement {
  public bool failed; public double temperatureC=20,slipEnergyJ,distanceM,lastContactWorkJ,consumedLife,runningSeconds; public string failureReason="";
  public TireCondition(){role="tire";cylinder=-1;valve=-1;}
  public new TireCondition Copy(){return (TireCondition)MemberwiseClone();}
  public override bool Failed {get{return failed||health<=0||consumedLife>=1;}}
  public override string Validate(){if(base.Validate()!=null)return "Invalid individual tire identity/health.";if(!MechanicsSchema.Finite(health)||health<0||health>1||!MechanicsSchema.Finite(temperatureC)||temperatureC< -50||temperatureC>2000||!MechanicsSchema.Finite(slipEnergyJ)||slipEnergyJ<0||!MechanicsSchema.Finite(distanceM)||distanceM<0||!MechanicsSchema.Finite(lastContactWorkJ)||lastContactWorkJ<0||!MechanicsSchema.Finite(consumedLife)||consumedLife<0||!MechanicsSchema.Finite(runningSeconds)||runningSeconds<0)return "Invalid per-wheel tire condition.";return null;}
 }
 public static class TireDynamics {
  // TireCondition is the sole owner of tire wear and lifetime usage. The containing
  // PartInstance exists for fitment/ownership only; its generic condition is a
  // neutral serialization shell, never a source of wear or failure.
  public static void Ensure(ComponentCondition c) {
   if(c.tires.Length==0) {
    // One-time migration of saves that predate per-corner tire conditions.
    var migrated=new TireCondition[4];
    for(int i=0;i<4;i++) migrated[i]=new TireCondition {
     health=c.health, failed=c.failed,
     temperatureC=c.temperatureC, slipEnergyJ=c.slipEnergyJ*.25,
     distanceM=c.distanceM, runningSeconds=c.runningSeconds, consumedLife=Math.Max(0,1-c.health),
     failureReason=c.failed?c.failureReason:""
    };
    c.tires=migrated;
   }
   if(c.tires.Length!=4||c.tires.Any(t=>t==null))throw new ArgumentException("Four individual tire conditions are required.");
   SyncSummary(c);
  }
  // The former generic ComponentCondition is retained only as a serialized
  // compatibility shell around the four separately owned tires. Do not put
  // derived health, heat, service history, or wear back into this shell.
  public static void SyncSummary(ComponentCondition c) {
   if(c.tires==null||c.tires.Length!=4||c.tires.Any(t=>t==null))throw new ArgumentException("Four individual tire conditions are required.");
   c.health=1;
   c.failed=false;
   c.failureReason="";
   c.temperatureC=20;
   c.slipEnergyJ=c.distanceM=c.runningSeconds=c.stressExposure=0;
   c.deposits=c.leakSeverity=c.adjustmentError=c.serviceWearRemainder=0;
   c.serviceCount=c.reconditionCount=0;
   c.lastService="";
   c.ClearClutch();

   // Keep the four typed TireCondition members in ComponentCondition.elements.
  }
  public static float LowestHealth(ComponentCondition c) =>
   c==null||c.tires==null||c.tires.Length!=4?1:c.tires.Min(t=>Math.Min(t.health,(float)Math.Max(0,1-t.consumedLife)));
  public static bool AnyFailed(ComponentCondition c) =>
   c!=null&&c.tires!=null&&c.tires.Any(t=>t.failed||t.health<=0||t.consumedLife>=1);
  public static double HottestTemperatureC(ComponentCondition c) =>
   c==null||c.tires==null||c.tires.Length==0?20:c.tires.Max(t=>t.temperatureC);
  public static string FailedCorners(ComponentCondition c) {
   if(c==null||c.tires==null)return "";
   var corners=new[]{"Front left","Front right","Rear left","Rear right"};
   return string.Join(", ",c.tires.Select((t,i)=>new{t,i}).Where(x=>x.t.failed||x.t.health<=0||x.t.consumedLife>=1).Select(x=>x.i<4?corners[x.i]:"Tire "+(x.i+1)).ToArray());
  }
  public static void ResetContactReference(CarState state,System.Collections.Generic.IEnumerable<PartInstance> parts){foreach(var p in parts)if(p.slot==PartCategory.Tires.ToString()){Ensure(p.condition);for(int i=0;i<4;i++)p.condition.tires[i].lastContactWorkJ=state.contact.slipWorkJ[i];}}
  public static double Grip(PartDefinition d,TireCondition t){if(t.failed||t.health<=0)return .025;double temperature=t.temperatureC<20?DragMath.Clamp(1+(t.temperatureC-20)*.003,.65,1):t.temperatureC<=d.tireOptimalC?1+.05*(t.temperatureC-20)/Math.Max(1,d.tireOptimalC-20):DragMath.Clamp(1.05-(t.temperatureC-d.tireOptimalC)*.005,.35,1.05);return temperature*(.35+.65*Math.Sqrt(t.health));}
  public static void Tick(CarSimulation car,double dt,double distanceBefore){
   var p=car.mechanics.parts.Find(v=>v.slot==PartCategory.Tires.ToString());if(p==null)return;var c=p.condition;Ensure(c);
   double distance=Math.Max(0,car.state.distance-distanceBefore);
   for(int i=0;i<4;i++) {var d=car.mechanics.TireDefinition(i);double pressure=1+Math.Pow((car.tune.AxlePressure(i<2)-28)/12,2)*.5;var t=c.tires[i];double work=car.state.contact.slipWorkJ[i],delta=Math.Max(0,work-t.lastContactWorkJ);t.lastContactWorkJ=work;t.slipEnergyJ+=delta;t.distanceM+=distance;t.runningSeconds+=dt;
    double cooling=d.coolingWattsPerC*(1+car.state.speed*.08)*(t.temperatureC-20);if(car.state.optimalThermals)t.temperatureC=d.tireOptimalC;else t.temperatureC=DragMath.Clamp(t.temperatureC+(delta*d.tireHeatFraction-cooling*dt)/(d.heatCapacityJPerC*Math.Max(.1,car.wheels.tireMasses[i]/Math.Max(.1,car.wheels.referenceTireMassKg))),-50,2000);
    double normal=(i<2?car.state.frontLoadN:car.state.rearLoadN)*.5,load=Math.Pow(Math.Max(.1,normal/4000),.3),thermal=Math.Max(0,t.temperatureC/d.thermalLimitC-1);
    double size=car.wheels.tireMasses[i]/Math.Max(.1,car.wheels.referenceTireMassKg);
    double loss=(distance/d.tireLifeDistanceM+delta/(d.wearEnergyJ*size))*pressure*load*(1+thermal*3)+dt*thermal*thermal/d.overloadSeconds;
    if(!t.failed){if(Math.Abs(t.health-(1-t.consumedLife))>2e-7)t.consumedLife=Math.Max(t.consumedLife,1-t.health);t.consumedLife+=loss*car.driver.WearRate;t.health=(float)Math.Max(0,1-t.consumedLife);if(t.health<=0){t.failed=true;t.failureReason="Tire wear depleted";}}
   }
   SyncSummary(c);
  }
 }
}
