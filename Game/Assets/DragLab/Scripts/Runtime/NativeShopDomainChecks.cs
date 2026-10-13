using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static class NativeShopDomainChecks
    {
        public static void Run(VehicleDefinition[] vehicles, Action<bool,string> check)
        {
            foreach(var vehicle in vehicles){var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,new MechanicalCatalog(vehicles.Select(v=>v.spec)));p.cash=1000000;p.reputation=1000;VehicleCollection.Buy(p,p.catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});var car=p.SelectedCar;var cache=new NativePartPreviewCache();
                NativePartBrowser Model()=>new NativePartBrowser(p,Array.Empty<PartCartLine>(),false,"Shop",_=>100,(_,u)=>100,cache);
                var a=Model();var head=a.Entries.First(e=>e.Definition.id==p.catalog.Id(car.vehicleId,PartCategory.HeadIntake,1));a.Evaluate(head);var b=Model();var next=b.Entries.First(e=>e.Id==head.Id);b.Evaluate(next);check(ReferenceEquals(a.Current,b.Current)&&ReferenceEquals(head.Proposed,next.Proposed)&&b.EvaluationCount==0,"Exact current and candidate snapshots are reused: "+vehicle.spec.id);
                car.fuel.litres-=1;var c=Model();var fresh=new NativePartBrowser(p,Array.Empty<PartCartLine>(),false,"Shop",_=>100,(_,u)=>100);check(!ReferenceEquals(b.Current,c.Current)&&Math.Abs(c.Current.vehicleMassKg-fresh.Current.vehicleMassKg)<1e-8&&b.Current.vehicleMassKg>c.Current.vehicleMassKg,"Fuel changes invalidate cached build mass without a revision bump: "+vehicle.spec.id);
                car.tune.driverMassKg+=5;var d=Model();check(d.Current.vehicleMassKg>c.Current.vehicleMassKg+4.9,"Tune changes invalidate cached build data: "+vehicle.spec.id);
                var fitted=p.componentsPart(PartCategory.HeadIntake);fitted.condition.health=.4f;var worn=Model();fresh=new NativePartBrowser(p,Array.Empty<PartCartLine>(),false,"Shop",_=>100,(_,u)=>100);check(!ReferenceEquals(worn.Current,d.Current)&&Math.Abs(worn.Current.peakHp-fresh.Current.peakHp)<1e-8,"Wear invalidation agrees with a fresh authoritative inspection: "+vehicle.spec.id);
                var stored=fitted.Copy();stored.instanceId=MechanicsOwnership.NewId("part");stored.location=PartLocation.Inventory;stored.ownerId="";stored.slot="";stored.storageId=SharedInventory.Garage;foreach(var element in stored.condition.elements)element.id=MechanicsOwnership.NewId("element");foreach(var tire in stored.condition.tires)tire.id=MechanicsOwnership.NewId("tire");p.parts.Add(stored);
                NativePartBrowser Owned()=>new NativePartBrowser(p,Array.Empty<PartCartLine>(),false,"Inventory",_=>100,(_,u)=>100,cache);
                var inventory=Owned();var incoming=inventory.Entries.First(e=>e.Id==stored.instanceId);inventory.Evaluate(incoming);stored.condition.health=.7f;var changed=Owned();var changedEntry=changed.Entries.First(e=>e.Id==stored.instanceId);changed.Evaluate(changedEntry);
                check(!ReferenceEquals(incoming.Proposed,changedEntry.Proposed)&&changed.EvaluationCount==1,"Incoming retained-part condition invalidates only its candidate: "+vehicle.spec.id);
                var lines=new[]{new PartCartLine{definitionId=p.catalog.Id(car.vehicleId,PartCategory.Cooling,1),quantity=1,install=true}};
                NativePartBrowser Planned()=>new NativePartBrowser(p,lines,true,"Shop",_=>100,(_,u)=>100,cache);
                var planned=Planned();var plannedAgain=Planned();check(planned.Plan!=null&&ReferenceEquals(planned.Plan,plannedAgain.Plan),"Unchanged cart plans reuse their authoritative snapshot: "+vehicle.spec.id+" / "+planned.PlanError);
                lines[0].install=false;var noInstall=Planned();check(noInstall.Plan!=null&&!ReferenceEquals(planned.Plan,noInstall.Plan),"Cart installation edits invalidate planned snapshots: "+vehicle.spec.id);
                var definition=p.catalog.Find(stored.definitionId);definition.massKg+=2;var definitionChanged=Model();fresh=new NativePartBrowser(p,Array.Empty<PartCartLine>(),false,"Shop",_=>100,(_,u)=>100);
                check(!ReferenceEquals(definitionChanged.Current,worn.Current)&&Math.Abs(definitionChanged.Current.vehicleMassKg-fresh.Current.vehicleMassKg)<1e-8,"Definition edits invalidate calculations and match a fresh inspection: "+vehicle.spec.id);
                var specs=NativePartSpecifications.Summary(p.catalog.Find(p.catalog.Id(car.vehicleId,PartCategory.Tires,1)));check(specs.Select(s=>s.key).SequenceEqual(new[]{"grip","rollinglife","slidinglife","tireoptimal"})&&specs[1].value==25000,"Tire summaries prioritize actual grip and lifespan rather than torque strength: "+vehicle.spec.id);
                var pack=p.componentsPart(PartCategory.Tires);TireDynamics.Ensure(pack.condition);foreach(var tire in pack.condition.tires)if(string.IsNullOrEmpty(tire.id))tire.id=MechanicsOwnership.NewId("tire");pack.condition.tires[0].health=.4f;var retained=pack.condition.tires.Skip(1).Select(t=>JsonUtility.ToJson(t)).ToArray();int cash=p.cash;var part=p.catalog.Find(pack.definitionId);var unit=InventoryUnits.Buy(p,car.instanceId,part.id,"tire");InventoryUnits.Install(p,car.instanceId,unit.element.id,"tire:0");check(p.cash==cash-InventoryPricing.NewUnit(part,4)&&pack.condition.tires.Skip(1).Select(t=>JsonUtility.ToJson(t)).SequenceEqual(retained)&&p.looseEngineElements.Any(u=>u.tireUnit&&u.tire.health==.4f),"One tire can be bought and replaced while retaining other corners and the worn original: "+vehicle.spec.id);
            }
        }
    }
#endif
}
