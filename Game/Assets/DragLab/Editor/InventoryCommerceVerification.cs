using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class InventoryCommerceVerification
    {
        public static void Run()
        {
            var cat=MechanicsTransactions.Catalog();var policy=ValuationPolicy.Defaults();long now=DateTime.UtcNow.Ticks;
            var log=new StringBuilder();int checks=0;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks++;log.AppendLine("PASS: "+label);};
            Func<PlayerProfile,List<PartCartLine>,PlayerProfile> commit=(p,lines)=>{var quote=PartCart.Quote(p,p.SelectedCar.instanceId,lines,now,300,policy);PlayerProfile next;string error;int saves=0;bool ok=MechanicsTransactions.Try(p,cat,p.revision,q=>PartCart.Checkout(q,quote,now,policy),q=>{saves++;return true;},out next,out error);check(ok&&saves==1,"Atomic checkout / "+(error??"one save"));return next;};
            foreach(var vehicle in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) {
                var p=PlayerProfile.New(now);MechanicsOwnership.Ensure(p,cat);p.cash=1000000;p.reputation=100;
                if(p.SelectedCar.vehicleId!=vehicle.spec.id)VehicleCollection.Buy(p,cat,new VehicleOffer{model=vehicle.spec.id,price=1,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
                var car=p.SelectedCar;string label=vehicle.spec.id+" / ";var d=cat.Find(cat.Id(car.vehicleId,PartCategory.Pistons));
                check(InventoryUnits.Count(p,car,d)==vehicle.spec.sound.cylinders,label+"Complete piston set counts the actual engine cylinders");
                var valves=cat.Find(cat.Id(car.vehicleId,PartCategory.Valves));check(InventoryUnits.Count(p,car,valves)==vehicle.spec.sound.cylinders*EngineElements.ValveCount(vehicle.spec),label+"Valve set counts cylinder and valve topology");
                var pack=p.parts.Find(i=>i.instanceId==car.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId);var old=pack.condition.elements[0];old.health=.25f;old.fault=ElementFault.CompressionLeak;old.severity=.4f;EngineElementService.RefreshAggregate(pack.condition);
                string oldId=old.id,other=JsonUtility.ToJson(pack.condition.elements[1]),bytes=JsonUtility.ToJson(p);
                var line=new PartCartLine{action=CartAction.NewUnits,definitionId=d.id,role=old.role,quantity=2,install=true,positions=new List<string>{old.id}};
                var lines=new List<PartCartLine>{line};var preview=InspectionBuild.Capture(p,car,cart:lines);
                check(JsonUtility.ToJson(p)==bytes,label+"Individual preview never changes condition, identities or resources");
                p=commit(p,lines);car=p.SelectedCar;pack=p.parts.Find(i=>i.instanceId==car.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId);
                check(pack.condition.elements[0].health==1&&pack.condition.elements[0].fault==ElementFault.None&&JsonUtility.ToJson(pack.condition.elements[1])==other,label+"Single replacement restores only the chosen piston");
                check(p.looseEngineElements.Any(u=>u.element.id==oldId&&u.element.health==.25f)&&p.looseEngineElements.Count(u=>u.definitionId==d.id)==2,label+"Old individual identity and unused purchased unit remain in storage");
                var actual=InspectionBuild.Capture(p,car);var a=CarInspection.CalculatePrepared(actual,CarInspection.PrepareCar(actual),false);var b=CarInspection.CalculatePrepared(preview,CarInspection.PrepareCar(preview),false);
                check(a.peakHp==b.peakHp&&a.peakTorque==b.peakTorque,label+"Individual cumulative dyno matches actual installation");
                var kit=cat.Id(car.vehicleId,PartCategory.Pistons,1);string removed=pack.instanceId;
                p=commit(p,new List<PartCartLine>{new PartCartLine{definitionId=kit,install=true}});car=p.SelectedCar;
                check(p.parts.Find(i=>i.instanceId==removed).location==PartLocation.Inventory&&p.parts.Find(i=>i.instanceId==car.installed.slots.Find(s=>s.slot=="Pistons").partInstanceId).condition.elements.Length==vehicle.spec.sound.cylinders,label+"Default quantity one purchases and installs a complete matched set");
                string partBytes=JsonUtility.ToJson(p.parts.Find(i=>i.instanceId==removed).condition);
                p=commit(p,new List<PartCartLine>{new PartCartLine{action=CartAction.Move,assetId=removed,destination=InventoryKind.Storage,destinationId=SharedInventory.Storage}});
                check(SharedInventory.Entries(p,InventoryKind.Storage,SharedInventory.Storage).Any(e=>e.id==removed)&&!SharedInventory.Entries(p,InventoryKind.Storage,SharedInventory.Garage).Any(e=>e.id==removed)&&JsonUtility.ToJson(p.parts.Find(i=>i.instanceId==removed).condition)==partBytes,label+"Moving containers preserves condition and exclusive item ownership");
                var quote=PartCart.Quote(p,p.SelectedCar.instanceId,new[]{new PartCartLine{action=CartAction.SellPart,assetId=removed}},now,300,policy);int credited=-quote.total;
                p=commit(p,new List<PartCartLine>{new PartCartLine{action=CartAction.SellPart,assetId=removed}});
                check(!p.parts.Any(i=>i.instanceId==removed)&&p.inventory.dealer.Any(s=>s.Id==removed&&JsonUtility.ToJson(s.part.condition)==partBytes)&&p.economyReceipts.Last().disposed.Contains(removed),label+"Sale moves the original item to dealer stock and records disposal from player ownership");
                int buyback=InventoryPricing.BuyUsed(p.inventory.dealer.Find(s=>s.Id==removed));check(buyback>credited,label+"Dealer spread prevents immediate same-buyer resale profit");
                p=commit(p,new List<PartCartLine>{new PartCartLine{action=CartAction.BuyUsed,assetId=removed,definitionId=d.id}});
                check(p.parts.Any(i=>i.instanceId==removed&&JsonUtility.ToJson(i.condition)==partBytes)&&!p.inventory.dealer.Any(s=>s.Id==removed),label+"Used purchase returns the same identity and wear without duplication");
                var tires=p.parts.Find(i=>i.instanceId==p.SelectedCar.installed.slots.Find(s=>s.slot=="Tires").partInstanceId);TireDynamics.Ensure(tires.condition);tires.condition.tires[0].health=.2f;tires.condition.tires[0].consumedLife=.8;tires.condition.tires[1].temperatureC=95;string untouched=JsonUtility.ToJson(tires.condition.tires[1]);
                p=commit(p,new List<PartCartLine>{new PartCartLine{action=CartAction.NewUnits,definitionId=tires.definitionId,role="tire",install=true,positions=new List<string>{"tire:0"}}});
                tires=p.parts.Find(i=>i.instanceId==p.SelectedCar.installed.slots.Find(s=>s.slot=="Tires").partInstanceId);
                check(tires.condition.tires[0].health==1&&JsonUtility.ToJson(tires.condition.tires[1])==untouched&&p.looseEngineElements.Any(u=>u.tire!=null&&u.tire.consumedLife==.8),label+"Individual tire replacement retains the other tires' life/heat and old tire condition");
                check(InventoryPricing.Sell(p,ValuationKind.Car,p.SelectedCar.instanceId,policy,"wholesale",now).IncludedIds.Contains(tires.condition.tires[0].id),label+"Whole-car sale quote includes fitted individual tire identities");
                foreach(var fitted in p.SelectedCar.installed.slots.Select(s=>p.parts.Find(i=>i.instanceId==s.partInstanceId)).Where(i=>i.condition.elements.Length>0).ToArray()) {
                    var offer=cat.Find(fitted.definitionId);
                    foreach(var role in InventoryUnits.Roles(p,p.SelectedCar,offer)) {
                        int oldCount=p.looseEngineElements.Count;
                        p=commit(p,new List<PartCartLine>{new PartCartLine{action=CartAction.NewUnits,definitionId=offer.id,role=role}});
                        check(p.looseEngineElements.Count==oldCount+1&&p.looseEngineElements.Last().element.role==role,label+offer.category+" / "+role+" buys independently without a fitted target");
                    }
                }
                string state=JsonUtility.ToJson(p);var loaded=JsonUtility.FromJson<PlayerProfile>(state);MechanicsOwnership.Ensure(loaded,cat);check(SnapshotEquality.Profile(state,loaded,cat),label+"Storage/dealer/unit state reloads without healing or rewriting identities");Debug.Log("INVENTORY MODEL PASS "+vehicle.spec.id);
            }
            var owner=PlayerProfile.New(now);MechanicsOwnership.Ensure(owner,cat);owner.cash=100000;owner.reputation=100;var target=owner.SelectedCar;
            string expensive=PartOperations.Buy(owner,cat,target.instanceId,cat.Id(target.vehicleId,PartCategory.Pistons,1),false);owner.cash=0;
            var basket=new List<PartCartLine>{new PartCartLine{action=CartAction.SellPart,assetId=expensive},new PartCartLine{definitionId=cat.Id(target.vehicleId,PartCategory.OilFilter),install=true}};
            var captured=PartCart.Quote(owner,target.instanceId,basket,now,300,policy);check(captured.total<0,"Trade basket credits a sale before charging purchases; no initial cash required");
            string original=JsonUtility.ToJson(owner);int writes=0;PlayerProfile result;string failure;
            check(!MechanicsTransactions.Try(owner,cat,owner.revision,p=>PartCart.Checkout(p,captured,now,policy),p=>{writes++;return false;},out result,out failure)&&writes==1&&JsonUtility.ToJson(owner)==original,"Failed mixed buy/sell save publishes nothing");
            var done=commit(owner,basket);check(done.cash==-captured.total,"Successful trade settles the captured net price exactly");
            var changed=JsonUtility.FromJson<ValuationPolicy>(JsonUtility.ToJson(policy));changed.usedPartFraction=.1;
            check(!MechanicsTransactions.Try(owner,cat,owner.revision,p=>PartCart.Checkout(p,captured,now,changed),p=>true,out result,out failure),"Pricing policy change invalidates the entire quote");
            check(!MechanicsTransactions.Try(done,cat,done.revision,p=>PartCart.Checkout(p,captured,now,policy),p=>true,out result,out failure),"Replayed mixed cart cannot resell or duplicate stock");
            foreach(var invalid in new[]{new PartCartLine{action=CartAction.Move,assetId=expensive,destination=InventoryKind.Storage,destinationId="missing"},new PartCartLine{action=CartAction.NewUnits,definitionId=cat.Id(target.vehicleId,PartCategory.Pistons),role="Pistons",install=true}}){bool rejected=false;try{PartCart.Quote(owner,target.instanceId,new[]{invalid},now,300,policy);}catch{rejected=true;}check(rejected,"Invalid destination/individual position rejected before payment");}
            var donor=owner.SelectedCar;var source=donor.installed.slots.Find(s=>s.slot=="Cooling").partInstanceId;string condition=JsonUtility.ToJson(owner.parts.Find(i=>i.instanceId==source).condition);
            owner.cash=100000;VehicleCollection.Buy(owner,cat,new VehicleOffer{model=donor.vehicleId,price=1,stock=donor.tune.Copy(),paint="#FFFFFF"});
            SharedInventory.Move(owner,source,InventoryKind.Car,owner.SelectedCar.instanceId);check(!donor.installed.slots.Any(s=>s.partInstanceId==source)&&owner.SelectedCar.installed.slots.Any(s=>s.partInstanceId==source)&&JsonUtility.ToJson(owner.parts.Find(i=>i.instanceId==source).condition)==condition,"Shared move between cars retains original IDs and source slot becomes empty");
            var engine=owner.SelectedCar.engineAssemblyId;SharedInventory.Move(owner,engine,InventoryKind.Storage,SharedInventory.Storage);check(SharedInventory.Entries(owner,InventoryKind.Storage,SharedInventory.Storage).Any(e=>e.id==engine)&&owner.SelectedCar.engineAssemblyId=="","Moving an engine carries its descendant tree and retains an incomplete car");
            SharedInventory.Move(owner,engine,InventoryKind.Car,owner.SelectedCar.instanceId);MechanicsOwnership.Ensure(owner,cat);check(owner.SelectedCar.engineAssemblyId==engine&&MechanicsOwnership.Validate(owner,cat)==null,"Shared engine move installs the original complete tree");
            log.AppendLine("Checks: "+checks);var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/inventory-commerce"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"domain-verification.txt"),log.ToString(),new UTF8Encoding(false));Debug.Log("Inventory commerce checks: "+checks);
        }
    }
}
