using System;
using System.Linq;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain.Mechanics
{
    [Serializable] public sealed class SetupTireLine {public string definitionId,itemId;public int corner,cost;public bool purchase,wheel;}
    public static class SavedTireFitments
    {
        public static void Plan(PlayerProfile p,OwnedCar car,SetupPart desired,SetupPlanLine packLine,SetupPlan plan)
        {
            bool wheel=desired.slot=="Wheels";var desiredIds=wheel?desired.wheelDefinitions:desired.tireDefinitions;
            if(desiredIds==null||desiredIds.Length==0)return;
            var pack=packLine.purchase?null:p.parts.Find(i=>i.instanceId==packLine.itemId);
            for(int corner=0;corner<4;corner++){
                string actual=pack==null?packLine.definitionId:wheel?pack.condition.elements.Length==4?WheelFitments.DefinitionId(pack,corner):pack.definitionId:pack.condition.tires.Length==0?pack.definitionId:TireFitments.DefinitionId(pack,corner);
                string target=desiredIds[corner];if(actual==target)continue;
                var owned=p.looseEngineElements.Where(u=>u.definitionId==target&&(wheel?!u.tireUnit&&u.element.role=="wheel"&&!u.element.Failed:u.tireUnit&&!u.tire.failed&&u.tire.health>0)&&!plan.tires.Any(t=>t.itemId==u.element.id)).OrderByDescending(u=>wheel?u.element.health:u.tire.health).ThenBy(u=>u.element.id,StringComparer.Ordinal).FirstOrDefault();
                var d=p.catalog.Find(target);var line=new SetupTireLine{wheel=wheel,corner=corner,definitionId=target,itemId=owned?.element.id,purchase=owned==null,cost=owned==null?InventoryPricing.NewUnit(d,4):0};
                if(line.purchase&&p.reputation<d.reputationRequired)plan.errors.Add(d.name+": requires REP "+d.reputationRequired);
                plan.tires.Add(line);plan.cost=checked(plan.cost+line.cost);
            }
        }
        public static void Apply(PlayerProfile p,OwnedCar car,SetupPlan plan,bool preview=false)
        {
            foreach(var line in plan.tires){
                string id=line.itemId;
                if(line.purchase){
                    if(preview){var unit=new LooseEngineElement{definitionId=line.definitionId,tireUnit=!line.wheel,tire=line.wheel?null:new TireCondition(),element=new EngineElement{id="preview/setup/"+(line.wheel?"wheel/":"tire/")+line.corner,role=line.wheel?"wheel":"tire",cylinder=-1,valve=-1}};p.looseEngineElements.Add(unit);id=unit.element.id;}
                    else id=InventoryUnits.Buy(p,car.instanceId,line.definitionId,line.wheel?"wheel":"tire").element.id;
                }
                InventoryUnits.Install(p,car.instanceId,id,(line.wheel?"wheel:":"tire:")+line.corner);
            }
        }
    }
}
