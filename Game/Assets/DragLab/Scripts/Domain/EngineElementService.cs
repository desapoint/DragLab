using System;
using System.Collections.Generic;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    [Serializable] public sealed class EngineRebuildTarget {public string partId, definitionId;public bool replace;}
    [Serializable] public sealed class EngineRebuildJob
    {
        public bool active; public string id, engineId;
        public long startedUtcTicks, readyUtcTicks;
        public int paidPrice;
        public List<EngineRebuildTarget> targets=new List<EngineRebuildTarget>();
    }
    public static class EngineElementService
    {
        public static int Price(PartDefinition d,ComponentCondition c){return InventoryPricing.NewUnit(d,c.elements.Length);}
        static PartInstance Installed(PlayerProfile p,OwnedCar car,string partId)
        {EngineRebuilds.RequireAvailable(car);var item=p.parts.Find(i=>i.instanceId==partId);if(item==null||!car.installed.slots.Any(s=>s.partInstanceId==partId)||item.condition.elements==null||item.condition.elements.Length==0)throw new InvalidOperationException("Individual part is no longer installed.");if(item.condition.failed)foreach(var e in item.condition.elements)e.health=0;return item;}
        public static void RefreshAggregate(ComponentCondition c)
        {if(c.elements==null||c.elements.Length==0)return;c.health=c.elements.Average(e=>e.health);c.failed=c.elements.All(e=>e.Failed);c.failureReason=c.failed?"All individual components unavailable":"";}
        public static void Replace(PlayerProfile p,string carId,string partId,string elementId,string spareId=null)
        {
            var car=p.cars.Find(c=>c.instanceId==carId);var item=Installed(p,car,partId);ReplacePack(p,item,elementId,spareId);
        }
        public static void ReplaceStored(PlayerProfile p,string engineId,string partId,string elementId,string spareId=null)
        {
            var engine=AssemblyOperations.Stored(p,engineId);var item=p.parts.Find(i=>i.instanceId==partId);
            if(item==null||!engine.slots.Any(s=>s.partInstanceId==partId)||item.condition.elements.Length==0)throw new InvalidOperationException("Stored component changed; refresh the quote.");
            if(item.condition.failed)foreach(var e in item.condition.elements)e.health=0;
            ReplacePack(p,item,elementId,spareId);
        }
        static void ReplacePack(PlayerProfile p,PartInstance item,string elementId,string spareId)
        {
            var old=item.condition.elements.FirstOrDefault(e=>e.id==elementId);if(old==null)throw new InvalidOperationException("Individual component changed; refresh the quote.");
            var d=p.catalog.Find(item.definitionId);EngineElement replacement;
            if(spareId==null){int price=Price(d,item.condition);if(p.cash<price)throw new InvalidOperationException("Insufficient cash for this individual component.");p.cash-=price;replacement=new EngineElement{id=MechanicsOwnership.NewId("element"),role=old.role,cylinder=old.cylinder,valve=old.valve};}
            else {var loose=p.looseEngineElements.Find(e=>e.element.id==spareId);var source=loose==null?null:p.catalog.Find(loose.definitionId);if(source==null||source.category!=d.category||source.fitment!=d.fitment||source.id!=d.id||loose.element.role!=old.role||loose.element.Failed)throw new InvalidOperationException("Spare needs the same matched specification and role; mixed piston masses/profiles require a complete matched kit.");replacement=loose.element.Copy();replacement.cylinder=old.cylinder;replacement.valve=old.valve;p.looseEngineElements.Remove(loose);}
            int index=Array.IndexOf(item.condition.elements,old);p.looseEngineElements.Add(new LooseEngineElement{definitionId=item.definitionId,unitPrice=Price(d,item.condition),element=old.Copy()});item.condition.elements[index]=replacement;RefreshAggregate(item.condition);item.condition.serviceCount++;item.condition.lastService="Individual "+old.role+" replaced / cylinder "+(old.cylinder+1);
        }
        public static int SalePrice(PlayerProfile p,LooseEngineElement e){return (int)Math.Floor(e.unitPrice*.45*e.element.health*(e.element.Failed?.1:1));}
        public static void Sell(PlayerProfile p,string id){var loose=p.looseEngineElements.Find(e=>e.element.id==id);if(loose==null)throw new InvalidOperationException("Individual spare already sold/moved.");p.cash=checked(p.cash+SalePrice(p,loose));p.looseEngineElements.Remove(loose);}
    }
    public static class EngineRebuilds
    {
        public static void RequireAvailable(OwnedCar c){if(c==null)throw new InvalidOperationException("Service vehicle no longer exists.");if(!string.IsNullOrEmpty(c.garageJobId))throw new InvalidOperationException("Car and installed components reserved by garage job; claim or cancel it first.");if(c.engineRebuild!=null&&c.engineRebuild.active)throw new InvalidOperationException("Engine reserved at rebuild shop; collect it before modifying/racing this car.");}
        public static bool Scope(PartCategory c){return PartSemantics.EngineInternal(c)||c==PartCategory.HeadIntake||c==PartCategory.Camshaft||c==PartCategory.Valves||c==PartCategory.ValveSprings||c==PartCategory.Followers||c==PartCategory.TimingDrive||c==PartCategory.PistonRings||c==PartCategory.CylinderBlock||c==PartCategory.Bearings||c==PartCategory.ValveSeals||c==PartCategory.OilPump;}
        public static EngineRebuildJob Quote(PlayerProfile p,OwnedCar car,long now,OperatingPolicy costs=null)
        {
            RequireAvailable(car);if(string.IsNullOrEmpty(car.engineAssemblyId))throw new InvalidOperationException("No engine attached to rebuild.");if(now<=0||now>DateTime.MaxValue.Ticks-TimeSpan.TicksPerDay)throw new ArgumentException("Invalid shop time.");
            var required=MechanicalCatalog.Slots(p.catalog.Spec(car.vehicleId)).Where(Scope);
            if(required.Any(c=>!car.installed.slots.Any(s=>s.slot==c.ToString())))throw new InvalidOperationException("Assemble missing engine components before requesting a rebuild.");
            var job=new EngineRebuildJob{active=true,id=MechanicsOwnership.NewId("rebuild"),engineId=car.engineAssemblyId,startedUtcTicks=now};
            costs=costs??new OperatingPolicy();costs.Validate();int work=0;job.paidPrice=checked(costs.rebuildBaseLabour+p.catalog.Spec(car.vehicleId).sound.cylinders*costs.rebuildPerCylinder);
            foreach(var binding in car.installed.slots){var part=p.parts.Find(i=>i.instanceId==binding.partInstanceId);var d=p.catalog.Find(part.definitionId);if(!Scope(d.category))continue;
                bool replace=part.condition.failed||part.condition.health<.35||part.condition.elements.Any(e=>e.Failed||e.fault==ElementFault.DroppedValve||e.fault==ElementFault.Seized||e.fault==ElementFault.WeakSpring);
                job.targets.Add(new EngineRebuildTarget{partId=part.instanceId,definitionId=part.definitionId,replace=replace});job.paidPrice=checked(job.paidPrice+(replace?d.price:(int)Math.Ceiling(d.price*(1-part.condition.health)*.5)));if(replace||part.condition.health<.9||EngineElements.NeedsService(part.condition))work++;
            }
            job.paidPrice+=p.catalog.Find(p.catalog.Id(car.vehicleId,PartCategory.OilFilter)).price;
            job.readyUtcTicks=checked(now+TimeSpan.TicksPerMinute*(5+work));return job;
        }
        public static void Start(PlayerProfile p,string carId,long now,OperatingPolicy costs=null)
        {var car=p.cars.Find(c=>c.instanceId==carId);var job=Quote(p,car,now,costs);if(p.cash<job.paidPrice)throw new InvalidOperationException("Insufficient cash for quoted rebuild.");p.cash-=job.paidPrice;car.engineRebuild=job;}
        public static string Validate(PlayerProfile p,OwnedCar car)
        {var j=car.engineRebuild;if(j==null||!j.active)return null;if(string.IsNullOrEmpty(j.id)||j.engineId!=car.engineAssemblyId||j.paidPrice<0||j.startedUtcTicks<=0||j.readyUtcTicks<=j.startedUtcTicks||j.readyUtcTicks>DateTime.MaxValue.Ticks||j.targets==null||j.targets.Count==0||j.targets.Select(t=>t.partId).Distinct().Count()!=j.targets.Count)return "Invalid rebuild quote.";foreach(var t in j.targets)if(!car.installed.slots.Any(s=>s.partInstanceId==t.partId)||!p.parts.Any(i=>i.instanceId==t.partId&&i.definitionId==t.definitionId))return "Rebuild contents changed.";return null;}
        public static void Collect(PlayerProfile p,string carId,string jobId,long now)
        {
            var car=p.cars.Find(c=>c.instanceId==carId);var job=car==null?null:car.engineRebuild;
            if(job==null||!job.active||job.id!=jobId||Validate(p,car)!=null)throw new InvalidOperationException("Rebuild job changed or was already collected.");if(now<job.readyUtcTicks)throw new InvalidOperationException("Rebuild is still in progress.");
            var savedTune=car.tune.Copy();car.engineRebuild=null;
            foreach(var target in job.targets){var old=p.parts.Find(i=>i.instanceId==target.partId);if(target.replace){var item=new PartInstance{instanceId=MechanicsOwnership.NewId("part"),definitionId=target.definitionId};p.parts.Add(item);MechanicsOwnership.Replace(p,car,item,p.catalog);}else{var c=old.condition;c.health=Math.Max(c.health,.95f);c.failed=false;c.failureReason="";c.deposits=c.leakSeverity=c.adjustmentError=c.serviceWearRemainder=0;foreach(var e in c.elements){e.health=Math.Max(e.health,.95f);e.fault=ElementFault.None;e.severity=0;}c.serviceCount++;c.lastService="Shop engine rebuild / reconditioned";}}
            car.fluids.oilLitres=MechanicsOwnership.Resolve(p,car,p.catalog).spec.fluids.oilCapacityLitres;car.fluids.oilCondition=1;
            var filter=new PartInstance{instanceId=MechanicsOwnership.NewId("part"),definitionId=p.catalog.Id(car.vehicleId,PartCategory.OilFilter)};p.parts.Add(filter);MechanicsOwnership.Replace(p,car,filter,p.catalog);car.tune=savedTune;
        }
    }
}
