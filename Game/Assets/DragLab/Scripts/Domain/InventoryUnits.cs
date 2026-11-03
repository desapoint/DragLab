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
        internal static VehicleSpec Spec(ICartApplyState state,PartMoveCarState car,PartDefinition d)
        {
            if(car==null||!state.Catalog.Fits(state.Catalog.Spec(car.VehicleId),d))return PartsMarket.Reference(state,d);
            var map=state.CarDefinitions(car);map[d.category]=d.id;return state.Catalog.Resolve(car.VehicleId,map,car.Tune).spec;
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
        internal static PartInstance Template(ICartApplyState state,PartMoveCarState car,PartDefinition d)
        {
            if((car==null||!state.Catalog.Fits(state.Catalog.Spec(car.VehicleId),d))&&!PartsMarket.FitsAny(state,d))
                return new PartInstance{instanceId="unavailable-template",definitionId=d.id};
            var spec=Spec(state,car,d);
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
        internal static int Count(ICartApplyState state,PartMoveCarState car,PartDefinition d)
        {
            var t=Template(state,car,d);
            return t.condition.elements.Length>0?t.condition.elements.Length:d.category==PartCategory.Tires||d.category==PartCategory.Wheels||d.category==PartCategory.Brakes?4:1;
        }
        public static string SetLabel(PlayerProfile p,OwnedCar car,PartDefinition d) => "Complete set / "+Count(p,car,d)+" units";
        public static string[] Roles(PlayerProfile p,OwnedCar car,PartDefinition d)
        {
            if(d.category==PartCategory.Tires)return new[]{"tire"};if(d.category==PartCategory.Wheels)return new[]{"wheel"};
            return Template(p,car,d).condition.elements.Select(e=>e.role).Distinct().ToArray();
        }
        internal static string[] Roles(ICartApplyState state,PartMoveCarState car,PartDefinition d)
        {
            if(d.category==PartCategory.Tires)return new[]{"tire"};
            if(d.category==PartCategory.Wheels)return new[]{"wheel"};
            return Template(state,car,d).condition.elements.Select(e=>e.role).Distinct().ToArray();
        }
        public static LooseEngineElement Buy(PlayerProfile p,string carId,string definition,string role)
        {
            using(var state=new LiveCartApplyState(p))return BuyUnit(state,carId,definition,role,-1,0,out _);
        }
        internal static LooseEngineElement BuyUnit(ICartApplyState state,string carId,string definition,string role,int rowIndex,int quantityOrdinal)
            =>BuyUnit(state,carId,definition,role,rowIndex,quantityOrdinal,out _);
        internal static LooseEngineElement BuyUnit(ICartApplyState state,string carId,string definition,string role,int rowIndex,int quantityOrdinal,out CartApplyAcquisitionReceipt<LooseEngineElement> receipt)
            =>BuyUnit(state,carId,definition,role,rowIndex,quantityOrdinal,out _,out receipt);
        internal static LooseEngineElement BuyUnit(ICartApplyState state,string carId,string definition,string role,int rowIndex,int quantityOrdinal,out CartApplyIdentityRef allocatedIdentity,out CartApplyAcquisitionReceipt<LooseEngineElement> receipt)
        {
            receipt=null;
            allocatedIdentity=default(CartApplyIdentityRef);
            var car=state.FindCarState(carId);
            if(car==null)throw new InvalidOperationException("Owned car no longer exists.");
            EngineRebuilds.RequireAvailable(true,car.GarageJobId,car.RebuildActive);
            if(state.HasUnsettledRun)throw new InvalidOperationException("Close the active run before changing hardware.");
            var d=state.Catalog.FindDefinition(definition);
            if(d==null||!PartsMarket.FitsAny(state,d)||state.Reputation<d.reputationRequired||!Roles(state,car,d).Contains(role))
                throw new InvalidOperationException("Individual unit incompatible or unavailable.");
            int count=Count(state,car,d);
            var quote=state.Supply.Quote(d,count,true);
            int price=quote.Cost;
            if(state.Cash<price)throw new InvalidOperationException("Insufficient cash for unit.");
            var element=EngineElements.Create(d.category);
            var allocated=state.Identities.Allocate("element",rowIndex,quantityOrdinal,"unit");
            allocatedIdentity=allocated.Identity;
            element.id=allocated.DomainFieldValue;element.definitionId=d.id;element.role=role;element.cylinder=-1;element.valve=-1;
            var item=new LooseEngineElement{definitionId=d.id,unitPrice=InventoryPricing.NewUnit(d,count),element=element,tireUnit=d.category==PartCategory.Tires};
            var consumed=state.Supply.Consume(d,count,true);
            state.Cash=state.Cash-price;
            state.AppendUnit(item);
            if(state.CaptureEffects)receipt=new CartApplyAcquisitionReceipt<LooseEngineElement>(item,allocated.Identity,d.id,quote,price,consumed.StockDeltas,consumed.HoldDeltas);
            return item;
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
        internal static string[] Positions(ICartApplyState state,PartMoveCarState car,string definition,string role)
        {
            var d=state.Catalog.FindDefinition(definition);
            if(d==null||!state.Catalog.Fits(state.Catalog.Spec(car.VehicleId),d))return new string[0];
            var pack=car.Bindings.Select(binding=>binding==null?null:state.FindPart(state.IdentityFromOwnedField(binding.partInstanceId)))
                .FirstOrDefault(i=>d.category==PartCategory.Tires||d.category==PartCategory.Wheels?i!=null&&i.slot==d.category.ToString():i!=null&&i.definitionId==definition);
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
        internal static string PositionLabel(ICartApplyState state,PartMoveCarState car,string definition,string position)
        {
            if(position.StartsWith("wheel:",StringComparison.Ordinal))return new[]{"Front left","Front right","Rear left","Rear right"}[int.Parse(position.Substring(6))];
            if(position.StartsWith("tire:",StringComparison.Ordinal))return new[]{"Front left","Front right","Rear left","Rear right"}[int.Parse(position.Substring(5))];
            var e=state.PartsInOrder.SelectMany(i=>i.condition.elements).FirstOrDefault(i=>i.id==position);
            return e==null?"Missing position":e.cylinder<0?e.role:"Cylinder "+(e.cylinder+1)+" / "+e.role+(e.valve<0?"":" "+(e.valve+1));
        }
        public static void Install(PlayerProfile p,string carId,string unitId,string position)
        {
            using(var state=new LiveCartApplyState(p))
                InstallUnit(state,carId,null,CartApplyIdentityRef.Existing(unitId),position,-1,0,state.MemberPolicy);
        }
        internal static void InstallUnit(ICartApplyState state,string carId,LooseEngineElement unit,CartApplyIdentityRef unitIdentity,string position,int rowIndex,int quantityOrdinal,PartMoveMemberPolicy memberPolicy)
        {
            var car=state.FindCarState(carId);
            if(car==null)throw new InvalidOperationException("Owned car no longer exists.");
            EngineRebuilds.RequireAvailable(true,car.GarageJobId,car.RebuildActive);
            if(state.HasUnsettledRun)throw new InvalidOperationException("Close the active run before changing hardware.");
            if(memberPolicy==PartMoveMemberPolicy.LegacyEnsure)
            {
                string externalId=unitIdentity.IsVirtual?state.OwnedFieldValue(unitIdentity):unitIdentity.ExistingId;
                unit=null;
                foreach(var candidate in state.UnitsInOrder)if(candidate.element.id==externalId){unit=candidate;break;}
            }
            if(unit==null)throw new InvalidOperationException("Unit no longer owned.");
            if(state.Catalog.FindDefinition(unit.definitionId)?.category==PartCategory.Wheels)
            {
                WheelFitments.Install(state,car,unit,unitIdentity,position,rowIndex,quantityOrdinal,memberPolicy);
                if(state.CaptureEffects)state.MarkAffectedCar(car);
                return;
            }
            var pack=car.Bindings.Select(binding=>binding==null?null:state.FindPart(state.IdentityFromOwnedField(binding.partInstanceId)))
                .FirstOrDefault(item=>unit.tireUnit?item!=null&&item.slot==PartCategory.Tires.ToString():item!=null&&item.definitionId==unit.definitionId)
                ??throw new InvalidOperationException("Individual replacement needs the same installed specification; use a complete kit to change specification.");
            var incoming=state.Catalog.FindDefinition(unit.definitionId);
            if(incoming==null||!state.Catalog.Fits(state.Catalog.Spec(car.VehicleId),incoming))throw new InvalidOperationException("Individual part does not fit this car.");
            if(unit.tire==null)
            {
                EngineElementService.Replace(state,carId,pack,position,unit,unitIdentity,rowIndex,quantityOrdinal,memberPolicy);
                if(state.CaptureEffects)state.MarkAffectedCar(car);
                return;
            }
            if(!Positions(state,car,unit.definitionId,"tire").Contains(position))throw new InvalidOperationException("Choose a valid tire position.");
            bool projectionExisting=memberPolicy==PartMoveMemberPolicy.ProjectionRequireExisting&&!state.IsCandidateCreated(pack);
            int index;
            if(projectionExisting)
            {
                var elements=pack.condition?.elements;
                if(elements==null||elements.Length!=4||elements.Any(element=>!(element is TireCondition)))
                    throw new CartApplyMalformedSourceException("parts["+pack.instanceId+"].condition.tires");
                index=int.Parse(position.Substring(5));
                var selected=(TireCondition)elements[index];
                if(string.IsNullOrEmpty(selected.id))
                    throw new CartApplyMalformedSourceException("parts["+pack.instanceId+"].condition.tires["+index+"].id");
                pack=state.EditPart(pack);
            }
            else
            {
                pack=state.EditPart(pack);
                TireDynamics.Ensure(pack.condition);
                index=int.Parse(position.Substring(5));
            }
            var old=pack.condition.tires[index];
            string oldDefinition=TireFitments.DefinitionId(pack,index);
            string oldId=string.IsNullOrEmpty(old.id)?state.Identities.Allocate("tire",rowIndex,quantityOrdinal,"displaced/tire/"+index).DomainFieldValue:old.id;
            state.AppendUnit(new LooseEngineElement{definitionId=oldDefinition,unitPrice=InventoryPricing.NewUnit(state.Catalog.FindDefinition(oldDefinition),4),element=new EngineElement{id=oldId,role="tire",cylinder=-1,valve=-1,health=old.failed?0:Math.Min(old.health,(float)Math.Max(0,1-old.consumedLife))},tireUnit=true,tire=old.Copy()});
            var next=unit.tire.Copy();next.id=state.OwnedFieldValue(unitIdentity);next.definitionId=unit.definitionId;next.lastContactWorkJ=0;pack.condition.elements[index]=next;
            state.RemoveUnit(unit);
            TireDynamics.SyncSummary(pack.condition);
            if(state.CaptureEffects)state.MarkAffectedCar(car);
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
