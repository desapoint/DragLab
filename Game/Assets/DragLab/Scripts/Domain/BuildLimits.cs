using System;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    // Read the same ratings/condition thresholds as the running model; do not change its controls.
    public static class BuildLimits
    {
        public static void Capture(CarSimulation car,out double rpm,out string rpmPart,out double torque,out string torquePart)
        {
            rpm=double.MaxValue;torque=double.MaxValue;rpmPart=torquePart="—";
            foreach(var slot in new[]{PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft,PartCategory.CylinderBlock}){
                var p=car.mechanics.Part(slot);if(p==null){rpm=torque=0;rpmPart=torquePart="Missing "+PartSemantics.Friendly(slot.ToString());return;}
                var d=car.mechanics.Definition(p);double speed=p.condition.failed||p.condition.health<=0?0:d.rpmLimit;
                double strength=p.condition.failed||p.condition.health<=0?0:MechanicalStress.TorqueStrength(d,p.condition);
                bool broken=p.condition.failed||p.condition.health<=0;
                if((slot!=PartCategory.CylinderBlock||speed>0||broken)&&speed<rpm){rpm=speed;rpmPart=d.name;}
                if((slot!=PartCategory.CylinderBlock||strength>0||broken)&&strength<torque){torque=strength;torquePart=d.name;}
            }
            var springs=car.mechanics.Part(PartCategory.ValveSprings);if(springs==null){rpm=0;rpmPart="Missing valve springs";return;}
            var v=car.spec.valvetrain;double demand=Math.Max(1,v.profileDemandRpm/car.spec.redlineRpm),severity=car.driver.ConditionSeverity;
            double safe=v.springSafeRpm*(1-(1-springs.condition.health)*.4*severity)/demand;
            foreach(var e in springs.condition.elements??new EngineElement[0]){
                double limit=e.Failed?0:v.springSafeRpm*(1-(1-e.health)*.4*severity)*(e.fault==ElementFault.WeakSpring?1-e.severity*.65:1)/demand;safe=Math.Min(safe,limit);
            }
            if(springs.condition.failed)safe=0;
            if(safe<rpm){rpm=safe;rpmPart=car.mechanics.Definition(springs).name;}
        }
    }
}
