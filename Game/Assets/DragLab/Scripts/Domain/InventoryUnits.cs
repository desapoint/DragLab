using System;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    public static class InventoryUnits
    {
        public static PartInstance Template(PlayerProfile p,OwnedCar car,PartDefinition d)
        {
            var definitions=MechanicsOwnership.Definitions(p,car);definitions[d.category]=d.id;
            var spec=p.catalog.Resolve(car.vehicleId,definitions,car.tune).spec;
            var pack=new PartInstance{instanceId="offer-template",definitionId=d.id};
            EngineElements.Ensure(spec,d,pack);
            if(d.category==PartCategory.Tires)TireDynamics.Ensure(pack.condition);
            return pack;
        }
        public static int Count(PlayerProfile p,OwnedCar car,PartDefinition d)
        {
            var t=Template(p,car,d);
            return t.condition.elements.Length>0?t.condition.elements.Length:d.category==PartCategory.Tires||d.category==PartCategory.Wheels||d.category==PartCategory.Brakes?4:1;
        }
        public static string SetLabel(PlayerProfile p,OwnedCar car,PartDefinition d) => "Complete set / "+Count(p,car,d)+" units";
        public static string[] Roles(PlayerProfile p,OwnedCar car,PartDefinition d)
        {
            if(d.category==PartCategory.Tires)return new[]{"tire"};if(d.category==PartCategory.Wheels)return new[]{"wheel"};
            return Template(p,car,d).condition.elements.Select(e=>e.role).Distinct().ToArray();
        }
        public static LooseEngineElement Buy(PlayerProfile p,string carId,string definition,string role)
        {
            var car=AssemblyOperations.MutableCar(p,carId);var d=p.catalog.Find(definition);
            if(d==null||!p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)||p.reputation<d.reputationRequired||!Roles(p,car,d).Contains(role))throw new InvalidOperationException("Individual unit incompatible or unavailable.");
            int price=InventoryPricing.NewUnit(d,Count(p,car,d));if(p.cash<price)throw new InvalidOperationException("Insufficient cash for unit.");
            var item=new LooseEngineElement{definitionId=d.id,unitPrice=price,element=new EngineElement{id=MechanicsOwnership.NewId("element"),role=role,cylinder=-1,valve=-1},tireUnit=d.category==PartCategory.Tires,tire=d.category==PartCategory.Tires?new TireCondition():null};
            p.cash-=price;p.looseEngineElements.Add(item);return item;
        }
        public static string[] Positions(PlayerProfile p,OwnedCar car,string definition,string role)
        {
            var d=p.catalog.Find(definition);if(d==null||!p.catalog.Fits(p.catalog.Spec(car.vehicleId),d))return new string[0];
            var pack=car.installed.slots.Select(s=>p.parts.Find(i=>i.instanceId==s.partInstanceId)).FirstOrDefault(i=>d.category==PartCategory.Tires||d.category==PartCategory.Wheels?i.slot==d.category.ToString():i.definitionId==definition);
            if(pack==null)return new string[0];
            if(d.category==PartCategory.Wheels)return Enumerable.Range(0,4).Select(i=>"wheel:"+i).ToArray();
            if(d.category==PartCategory.Tires)return Enumerable.Range(0,4).Select(i=>"tire:"+i).ToArray();
            return pack.condition.elements.Where(e=>e.role==role).Select(e=>e.id).ToArray();
        }
        public static string PositionLabel(PlayerProfile p,OwnedCar car,string definition,string position)
        {
            if(position.StartsWith("wheel:",StringComparison.Ordinal))return new[]{"Front left","Front right","Rear left","Rear right"}[int.Parse(position.Substring(6))];
            if(position.StartsWith("tire:",StringComparison.Ordinal))return new[]{"Front left","Front right","Rear left","Rear right"}[int.Parse(position.Substring(5))];
            var e=p.parts.SelectMany(i=>i.condition.elements).FirstOrDefault(i=>i.id==position);
            return e==null?"Missing position":e.cylinder<0?e.role:"Cylinder "+(e.cylinder+1)+" / "+e.role+(e.valve<0?"":" "+(e.valve+1));
        }
        public static void Install(PlayerProfile p,string carId,string unitId,string position)
        {
            var car=AssemblyOperations.MutableCar(p,carId);var unit=p.looseEngineElements.Find(u=>u.element.id==unitId)??throw new InvalidOperationException("Unit no longer owned.");
            if(p.catalog.Find(unit.definitionId)?.category==PartCategory.Wheels){WheelFitments.Install(p,car,unit,position);return;}
            var pack=car.installed.slots.Select(s=>p.parts.Find(i=>i.instanceId==s.partInstanceId)).FirstOrDefault(i=>unit.tireUnit?i.slot==PartCategory.Tires.ToString():i.definitionId==unit.definitionId)??throw new InvalidOperationException("Individual replacement needs the same installed specification; use a complete kit to change specification.");
            var incoming=p.catalog.Find(unit.definitionId);if(incoming==null||!p.catalog.Fits(p.catalog.Spec(car.vehicleId),incoming))throw new InvalidOperationException("Individual part does not fit this car.");
            if(unit.tire==null){EngineElementService.Replace(p,carId,pack.instanceId,position,unitId);return;}
            if(!Positions(p,car,unit.definitionId,"tire").Contains(position))throw new InvalidOperationException("Choose a valid tire position.");
            TireDynamics.Ensure(pack.condition);int index=int.Parse(position.Substring(5));var old=pack.condition.tires[index];
            string oldDefinition=TireFitments.DefinitionId(pack,index);
            string oldId=string.IsNullOrEmpty(old.id)?MechanicsOwnership.NewId("tire"):old.id;
            p.looseEngineElements.Add(new LooseEngineElement{definitionId=oldDefinition,unitPrice=InventoryPricing.NewUnit(p.catalog.Find(oldDefinition),4),element=new EngineElement{id=oldId,role="tire",cylinder=-1,valve=-1,health=old.failed?0:Math.Min(old.health,(float)Math.Max(0,1-old.consumedLife))},tireUnit=true,tire=old.Copy()});
            var next=unit.tire.Copy();next.id=unit.element.id;next.definitionId=unit.definitionId;next.lastContactWorkJ=0;pack.condition.tires[index]=next;p.looseEngineElements.Remove(unit);
            pack.condition.health=pack.condition.tires.Min(t=>t.health);pack.condition.failed=pack.condition.tires.Any(t=>t.failed);pack.condition.failureReason=pack.condition.failed?"Tire unavailable":"";
            pack.condition.serviceCount++;pack.condition.lastService="Individual tire replaced / "+PositionLabel(p,car,pack.definitionId,position);
        }
    }
}
