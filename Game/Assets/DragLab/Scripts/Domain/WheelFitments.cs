using System;
using System.Collections.Generic;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    // Wheel members reuse the inventory's unit identity/condition records, not engine combustion behavior.
    public static class WheelFitments
    {
        public static string DefinitionId(PartInstance pack,int corner)=>string.IsNullOrEmpty(pack.condition.elements[corner].definitionId)?pack.definitionId:pack.condition.elements[corner].definitionId;
        public static bool HasOverrides(PartInstance pack)=>pack!=null&&pack.condition.elements.Length==4&&pack.condition.elements.All(e=>e.role=="wheel")&&Enumerable.Range(0,4).Any(i=>DefinitionId(pack,i)!=pack.definitionId);
        public static void Ensure(PartInstance pack)
        {
            if(pack.condition.elements!=null&&pack.condition.elements.Length>0)return;
            pack.condition.elements=Enumerable.Range(0,4).Select(i=>new EngineElement{id=pack.instanceId+"/wheel/"+i,role="wheel",cylinder=i,valve=-1,health=pack.condition.failed?0:pack.condition.health}).ToArray();
        }
        public static void Capture(VehicleSpec spec,IEnumerable<PartInstance> parts,MechanicalCatalog catalog)
        {
            spec.cornerWheelDefinitions=new PartDefinition[0];var pack=parts.FirstOrDefault(p=>p.slot=="Wheels");if(pack==null)return;
            spec.cornerWheelDefinitions=Enumerable.Range(0,4).Select(i=>{
                var d=catalog.Find(pack.condition.elements.Length==4?DefinitionId(pack,i):pack.definitionId);if(d==null||d.category!=PartCategory.Wheels||!catalog.Fits(spec,d))throw new InvalidOperationException("Invalid wheel specification at corner "+(i+1));return d.Copy();
            }).ToArray();
        }
        public static void Install(PlayerProfile p,OwnedCar car,LooseEngineElement unit,string position)
        {
            if(!InventoryUnits.Positions(p,car,unit.definitionId,"wheel").Contains(position))throw new InvalidOperationException("Choose a valid wheel position.");
            var slot=car.installed.slots.Find(s=>s.slot=="Wheels");var pack=p.parts.Find(i=>i.instanceId==slot.partInstanceId);Ensure(pack);int corner=int.Parse(position.Substring(6));
            var old=pack.condition.elements[corner];string oldDefinition=DefinitionId(pack,corner);
            p.looseEngineElements.Add(new LooseEngineElement{definitionId=oldDefinition,unitPrice=InventoryPricing.NewUnit(p.catalog.Find(oldDefinition),4),element=old.Copy()});
            var next=unit.element.Copy();next.role="wheel";next.definitionId=unit.definitionId;next.cylinder=corner;next.valve=-1;pack.condition.elements[corner]=next;p.looseEngineElements.Remove(unit);
            pack.condition.health=pack.condition.elements.Min(e=>e.health);pack.condition.failed=pack.condition.elements.All(e=>e.Failed);pack.condition.failureReason=pack.condition.failed?"Wheels unavailable":"";pack.condition.serviceCount++;pack.condition.lastService="Individual wheel replaced / "+InventoryUnits.PositionLabel(p,car,unit.definitionId,position);
        }
    }
}
