using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    public static class TireFitments
    {
        public static string DefinitionId(PartInstance pack,int corner)=>string.IsNullOrEmpty(pack.condition.tires[corner].definitionId)?pack.definitionId:pack.condition.tires[corner].definitionId;
        public static bool HasOverrides(PartInstance pack)=>pack!=null&&pack.condition.tires!=null&&pack.condition.tires.Length==4&&Enumerable.Range(0,4).Any(i=>DefinitionId(pack,i)!=pack.definitionId);
        static PartInstance Pack(PlayerProfile p,OwnedCar car,string definition){var b=car.installed.slots.Find(s=>s.slot=="Tires");var pack=b==null?null:p.parts.Find(i=>i.instanceId==b.partInstanceId);if(pack==null||pack.definitionId!=definition)throw new InvalidOperationException("Fitted tire assembly changed.");return pack;}
        public static int ReplacementCost(PlayerProfile p,OwnedCar car,string definition)
        {
            var pack=Pack(p,car,definition);if(!HasOverrides(pack))return p.catalog.Find(definition).price;
            var ids=Enumerable.Range(0,4).Select(i=>DefinitionId(pack,i)).ToArray();
            foreach(var id in ids){var d=p.catalog.Find(id);if(d==null||!p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)||p.reputation<d.reputationRequired)throw new InvalidOperationException("Same tire replacement unavailable; choose compatible alternatives in Shop.");}
            return ids.Distinct().Count()==1?p.catalog.Find(ids[0]).price:ids.Sum(id=>InventoryPricing.NewUnit(p.catalog.Find(id),4));
        }
        public static void ReplaceSame(PlayerProfile p,OwnedCar car,string definition)
        {
            var old=Pack(p,car,definition);int cost=ReplacementCost(p,car,definition);if(p.cash<cost)throw new InvalidOperationException("Insufficient cash for fitted tire specifications.");
            if(!HasOverrides(old)){PartOperations.Buy(p,p.catalog,car.instanceId,definition,true);return;}
            var ids=Enumerable.Range(0,4).Select(i=>DefinitionId(old,i)).ToArray();
            if(ids.Distinct().Count()==1){PartOperations.Buy(p,p.catalog,car.instanceId,ids[0],true);return;}
            var item=new PartInstance{instanceId=MechanicsOwnership.NewId("part"),definitionId=definition,condition=new ComponentCondition{tires=ids.Select(id=>new TireCondition{id=MechanicsOwnership.NewId("tire"),definitionId=id}).ToArray()}};
            p.parts.Add(item);p.cash-=cost;MechanicsOwnership.Replace(p,car,item,p.catalog);
        }
        public static void Capture(VehicleSpec spec,IEnumerable<PartInstance> parts,MechanicalCatalog catalog)
        {
            var pack=parts.FirstOrDefault(p=>p.slot==PartCategory.Tires.ToString());spec.cornerTireDefinitions=new PartDefinition[0];if(pack==null)return;
            // Empty old condition arrays retain the pack's authored definition; never create player assets here.
            if(pack.condition.tires==null||pack.condition.tires.Length==0)return;
            spec.cornerTireDefinitions=Enumerable.Range(0,4).Select(i=>{
                var d=catalog.Find(DefinitionId(pack,i));
                if(d==null||d.category!=PartCategory.Tires||!catalog.Fits(spec,d))throw new InvalidOperationException("Incompatible fitted tire at corner "+(i+1));
                return d.Copy();
            }).ToArray();
        }
    }
}
