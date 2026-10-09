using System;
using System.Linq;
using DragLab.Domain.Mechanics;
namespace DragLab.Domain {
 [Serializable] public sealed class TireCondition {
  public float health=1; public bool failed; public double temperatureC=20,slipEnergyJ,distanceM,lastContactWorkJ,consumedLife;
  public TireCondition Copy(){return (TireCondition)MemberwiseClone();}
  public string Validate(){if(!MechanicsSchema.Finite(health)||health<0||health>1||!MechanicsSchema.Finite(temperatureC)||temperatureC< -50||temperatureC>2000||!MechanicsSchema.Finite(slipEnergyJ)||slipEnergyJ<0||!MechanicsSchema.Finite(distanceM)||distanceM<0||!MechanicsSchema.Finite(lastContactWorkJ)||lastContactWorkJ<0||!MechanicsSchema.Finite(consumedLife)||consumedLife<0)return "Invalid per-wheel tire condition.";return null;}
 }
 public static class TireDynamics {
  public static void Ensure(ComponentCondition c){if(c.tires==null||c.tires.Length==0){c.tires=new TireCondition[4];for(int i=0;i<4;i++)c.tires[i]=new TireCondition {health=c.health,failed=c.failed,temperatureC=c.temperatureC,slipEnergyJ=c.slipEnergyJ*.25,distanceM=c.distanceM,consumedLife=1-c.health};}if(c.tires.Length!=4||c.tires.Any(t=>t==null))throw new ArgumentException("Tire pack requires four conditions.");if(c.failed&&c.tires.All(t=>!t.failed))foreach(var t in c.tires)t.failed=true;double minimum=c.tires.Min(t=>t.health);if(c.health<minimum-1e-6)foreach(var t in c.tires){t.health=Math.Min(t.health,c.health);t.consumedLife=Math.Max(t.consumedLife,1-t.health);}}
  public static double Grip(PartDefinition d,TireCondition t){if(t.failed||t.health<=0)return .025;double temperature=t.temperatureC<20?DragMath.Clamp(1+(t.temperatureC-20)*.003,.65,1):t.temperatureC<=d.tireOptimalC?1+.05*(t.temperatureC-20)/Math.Max(1,d.tireOptimalC-20):DragMath.Clamp(1.05-(t.temperatureC-d.tireOptimalC)*.005,.35,1.05);return temperature*(.35+.65*Math.Sqrt(t.health));}
  public static void Tick(CarSimulation car,double dt,double distanceBefore){
   var p=car.mechanics.parts.Find(v=>v.slot==PartCategory.Tires.ToString());if(p==null)return;var d=car.mechanics.Definition(p);var c=p.condition;Ensure(c);
   double distance=Math.Max(0,car.state.distance-distanceBefore),pressure=1+Math.Pow((car.tune.tirePressurePsi-28)/12,2)*.5;
   for(int i=0;i<4;i++) {var t=c.tires[i];double work=car.state.contact.slipWorkJ[i],delta=Math.Max(0,work-t.lastContactWorkJ);t.lastContactWorkJ=work;t.slipEnergyJ+=delta;t.distanceM+=distance;
    double cooling=d.coolingWattsPerC*(1+car.state.speed*.08)*(t.temperatureC-20);if(car.state.optimalThermals)t.temperatureC=d.tireOptimalC;else t.temperatureC=DragMath.Clamp(t.temperatureC+(delta-cooling*dt)/(d.heatCapacityJPerC*Math.Max(.1,car.wheels.tireMassKg/Math.Max(.1,car.wheels.referenceTireMassKg))),-50,2000);
    double normal=(i<2?car.state.frontLoadN:car.state.rearLoadN)*.5,load=Math.Pow(Math.Max(.1,normal/4000),.3),thermal=Math.Max(0,t.temperatureC/d.thermalLimitC-1);
    double size=car.wheels.tireMassKg/Math.Max(.1,car.wheels.referenceTireMassKg);
    double loss=(distance/d.tireLifeDistanceM+delta/(d.wearEnergyJ*size))*pressure*load*(1+thermal*3)+dt*thermal*thermal/d.overloadSeconds;
    if(!t.failed){t.consumedLife=Math.Max(t.consumedLife,1-t.health)+loss*car.driver.WearRate;t.health=(float)Math.Max(0,1-t.consumedLife);if(t.health<=0)t.failed=true;}
   }
   c.health=1;c.failed=false;c.temperatureC=-50;c.slipEnergyJ=0;foreach(var t in c.tires){c.health=Math.Min(c.health,t.health);c.temperatureC=Math.Max(c.temperatureC,t.temperatureC);c.slipEnergyJ+=t.slipEnergyJ;if(t.failed)c.failed=true;}
   if(c.failed)c.failureReason="Tire grip loss / worn or overheated tire";c.runningSeconds+=dt;c.distanceM+=distance;
  }
 }
}
