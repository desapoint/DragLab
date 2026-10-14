using System;
using System.Linq;
using System.Collections.Generic;

namespace DragLab.Domain.Mechanics
{
    [Serializable]
    public sealed class TireSwapAssignment
    {
        public string unitId;
        public int corner; // Front left, front right, rear left, rear right.
        public TireSwapAssignment(string id,int index){unitId=id;corner=index;}
    }
    public static class InventoryUnits
    {
        public static VehicleSpec Spec(PlayerProfile p,OwnedCar car,PartDefinition d)
        {
            if(car==null||!p.catalog.Fits(p.catalog.Spec(car.vehicleId),d))return PartsMarket.Reference(p,d);
            var map=MechanicsOwnership.Definitions(p,car);map[d.category]=d.id;return p.catalog.Resolve(car.vehicleId,map,car.tune).spec;
        }
        public static PartInstance Template(PlayerProfile p,OwnedCar car,PartDefinition d)
        {
            if((car==null||!p.catalog.Fits(p.catalog.Spec(car.vehicleId),d))&&!PartsMarket.FitsAny(p,d))return new PartInstance{instanceId="unavailable-template",definitionId=d.id};
            var spec=Spec(p,car,d);
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
            if(d==null||!PartsMarket.FitsAny(p,d)||p.reputation<d.reputationRequired||!Roles(p,car,d).Contains(role))throw new InvalidOperationException("Individual unit incompatible or unavailable.");
            int count=Count(p,car,d);int price=PartsMarket.Quote(p,d,count,true).cost;if(p.cash<price)throw new InvalidOperationException("Insufficient cash for unit.");
            var element=EngineElements.Create(d.category);
            element.id=MechanicsOwnership.NewId("element");element.definitionId=d.id;element.role=role;element.cylinder=-1;element.valve=-1;
            var item=new LooseEngineElement{definitionId=d.id,unitPrice=InventoryPricing.NewUnit(d,count),element=element,tireUnit=d.category==PartCategory.Tires};
            PartsMarket.Consume(p,d,count,true);p.cash-=price;p.looseEngineElements.Add(item);return item;
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
            var next=unit.tire.Copy();next.id=unit.element.id;next.definitionId=unit.definitionId;next.lastContactWorkJ=0;pack.condition.elements[index]=next;p.looseEngineElements.Remove(unit);
            TireDynamics.SyncSummary(pack.condition);
            // Tire swap is a transaction; the container owns no maintenance history.
        }
        // Call from MechanicsTransactions.Try (or a validated cart preview).
        // The complete selection is validated against the original inventory before
        // moving anything; callers commit the candidate profile as one transaction.
        public static void SwapTires(PlayerProfile p,string carId,IEnumerable<TireSwapAssignment> source)
        {
            var selections=source==null?new TireSwapAssignment[0]:source.ToArray();
            if(selections.Length<1||selections.Length>4)throw new InvalidOperationException("Select between one and four stored tires.");
            if(selections.Any(x=>x==null||x.corner<0||x.corner>3||string.IsNullOrEmpty(x.unitId))
                ||selections.Select(x=>x.corner).Distinct().Count()!=selections.Length
                ||selections.Select(x=>x.unitId).Distinct(StringComparer.Ordinal).Count()!=selections.Length)
                throw new InvalidOperationException("Each physical tire and corner may appear only once in the swap.");
            if(p.runCheckpoints.Any(r=>!r.settled))throw new InvalidOperationException("Finish or close the active race before swapping tires.");
            var car=AssemblyOperations.MutableCar(p,carId);
            var binding=car.installed.slots.Find(x=>x.slot==PartCategory.Tires.ToString());
            var pack=binding==null?null:p.parts.Find(x=>x.instanceId==binding.partInstanceId);
            if(pack==null)throw new InvalidOperationException("Install a complete tire set before swapping individual tires.");
            TireDynamics.Ensure(pack.condition);
            foreach(var request in selections)
            {
                var unit=p.looseEngineElements.Find(x=>x.element!=null&&x.element.id==request.unitId);
                var spec=unit==null?null:p.catalog.Find(unit.definitionId);
                if(unit==null||!unit.tireUnit||unit.tire==null||unit.tire.Validate()!=null
                    ||unit.element==null||(!string.IsNullOrEmpty(unit.tire.id)&&unit.tire.id!=unit.element.id)
                    ||spec==null||spec.category!=PartCategory.Tires
                    ||!p.catalog.Fits(p.catalog.Spec(car.vehicleId),spec))
                    throw new InvalidOperationException("Stored tire is missing, damaged in the inventory data, or incompatible with this car.");
            }
            // Install() returns each displaced tire to inventory with its original
            // identity, specification, condition and full wear history.
            foreach(var request in selections.OrderBy(x=>x.corner))
                Install(p,carId,request.unitId,"tire:"+request.corner);
        }

    }
}
