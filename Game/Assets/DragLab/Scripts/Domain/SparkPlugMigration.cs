using System;
using System.Collections.Generic;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    // Explicit one-time split of the old composite ignition kit. No missing kit is invented.
    public static class SparkPlugMigration
    {
        public static void Apply(PlayerProfile profile, MechanicalCatalog catalog)
        {
            if(profile.ignitionHardwareVersion>=1)return;
            foreach(var old in profile.parts.Concat(profile.inventory.dealer.Where(s=>!s.isUnit&&s.part!=null).Select(s=>s.part)).ToArray()) {
                var d=catalog.Find(old.definitionId);if(d==null||d.category!=PartCategory.Ignition)continue;
                var car=profile.cars.FirstOrDefault(c=>c.engineAssemblyId==old.ownerId);
                var engine=profile.engines.FirstOrDefault(e=>e.instanceId==old.ownerId&&string.IsNullOrEmpty(e.parentVehicleId));
                var slots=car!=null?car.installed.slots:engine?.slots;
                if(slots!=null&&slots.Any(b=>b.slot==PartCategory.SparkPlugs.ToString()))continue;
                var spec=catalog.Spec(d.id.Split('/')[0]);spec.combustion.separateSparkPlugs=false;
                EngineElements.Ensure(spec,d,old);
                var plugs=old.condition.elements.Where(e=>e.role=="spark plug / lead").Select(e=>EngineElements.CopyAsCategory(PartCategory.SparkPlugs,e)).ToArray();
                if(plugs.Length==0)continue;
                var split=new PartInstance { instanceId=MechanicsOwnership.NewId("part"), definitionId=catalog.Id(spec.id,PartCategory.SparkPlugs,Math.Min(1,d.stage)),location=old.location,ownerId=old.ownerId,slot=old.location==PartLocation.Inventory?"":PartCategory.SparkPlugs.ToString(),storageId=old.storageId,condition=old.condition.Copy() };
                foreach(var plug in plugs)plug.definitionId=split.definitionId;
                split.condition.elements=plugs;old.condition.elements=old.condition.elements.Where(e=>e.role!="spark plug / lead").ToArray();
                var seller=profile.inventory.dealer.FirstOrDefault(s=>s.part==old);
                if(seller==null)profile.parts.Add(split);else {int paid=seller.paidPrice/6;seller.paidPrice-=paid;profile.inventory.dealer.Add(new DealerStock{part=split,paidPrice=paid});}
                slots?.Add(new SlotBinding{slot=split.slot,partInstanceId=split.instanceId});
            }
            foreach(var unit in profile.looseEngineElements) {
                if(unit.tireUnit||unit.element.role!="spark plug / lead")continue;
                var d=catalog.Find(unit.definitionId);if(d?.category==PartCategory.Ignition){unit.definitionId=catalog.Id(d.id.Split('/')[0],PartCategory.SparkPlugs,Math.Min(1,d.stage));unit.element=EngineElements.CopyAsCategory(PartCategory.SparkPlugs,unit.element);unit.element.definitionId=unit.definitionId;}
            }
            // Seller buyback records also retain the same individual identities and faults.
            foreach(var stock in profile.inventory.dealer.Where(s=>s.isUnit&&s.unit!=null&&!s.unit.tireUnit)) {
                var d=catalog.Find(stock.unit.definitionId);if(d?.category==PartCategory.Ignition&&stock.unit.element.role=="spark plug / lead"){stock.unit.definitionId=catalog.Id(d.id.Split('/')[0],PartCategory.SparkPlugs,Math.Min(1,d.stage));stock.unit.element=EngineElements.CopyAsCategory(PartCategory.SparkPlugs,stock.unit.element);stock.unit.element.definitionId=stock.unit.definitionId;}
            }
            foreach(var setup in profile.savedSetups.Where(s=>s.scope!=SetupScope.TuneOnly&&!s.parts.Any(p=>p.slot=="SparkPlugs"))) {
                var old=setup.parts.FirstOrDefault(p=>p.slot=="Ignition");var d=old==null?null:catalog.Find(old.definitionId);
                if(d!=null)setup.parts.Add(new SetupPart{slot="SparkPlugs",definitionId=catalog.Id(d.id.Split('/')[0],PartCategory.SparkPlugs,Math.Min(1,d.stage))});
            }
            profile.ignitionHardwareVersion=1;
        }
    }
}
