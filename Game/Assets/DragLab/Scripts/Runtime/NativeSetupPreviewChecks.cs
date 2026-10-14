using System;
using System.Linq;
using UnityEngine;
using DragLab.Data;
using DragLab.Infrastructure;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static class NativeSetupPreviewChecks
    {
        public static void Run(VehicleDefinition[] vehicles,Action<bool,string> check)
        {
            var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            foreach(var vehicle in vehicles){
                var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,catalog);p.cash=1000000;p.reputation=1000;
                VehicleCollection.Buy(p,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});var car=p.SelectedCar;
                p.componentsPart(PartCategory.HeadIntake).condition.health=.65f;car.fuel.litres=7;car.fluids.oilLitres=2;car.fluids.coolantLitres=3;car.fluids.oilCondition=.5;
                foreach(SetupScope scope in Enum.GetValues(typeof(SetupScope))){
                    SavedSetups.Save(p,car.instanceId,"Café "+scope,scope);var saved=p.savedSetups.Last();saved.tune.driverMassKg+=20;saved.tune.launchRpm+=200;
                    string before=JsonUtility.ToJson(p);var preview=NativeSetupPreview.Capture(p,car,saved);
                    check(preview.Current!=null&&preview.Proposed!=null&&preview.Plan.Missing==0&&before==JsonUtility.ToJson(p),"Setup preview is complete and read-only: "+vehicle.spec.id+" / "+scope);
                    check(preview.Proposed.source.startingFuel.litres==7&&preview.Proposed.source.startingFluids.oilLitres==2&&preview.Proposed.source.startingFluids.oilCondition==.5&&preview.Proposed.source.components.Any(i=>i.definitionId==p.componentsPart(PartCategory.HeadIntake).definitionId&&i.condition.health==.65f),"Setup preview retains actual wear and liquids: "+vehicle.spec.id+" / "+scope);
                    var applied=JsonUtility.FromJson<PlayerProfile>(before);applied.catalog=catalog;foreach(var c in applied.cars)if(p.cars.Find(i=>i.instanceId==c.instanceId).engineRebuild==null)c.engineRebuild=null;SavedSetups.Apply(applied,car.instanceId,saved.id,p.revision,false,preview.Plan.cost);var expected=NativePartBrowser.Inspect(InspectionBuild.Capture(applied,applied.SelectedCar));
                    check(Math.Abs(expected.peakHp-preview.Proposed.peakHp)<1e-8&&Math.Abs(expected.vehicleMassKg-preview.Proposed.vehicleMassKg)<1e-8&&JsonUtility.ToJson(expected.source.build.tune)==JsonUtility.ToJson(preview.Proposed.source.build.tune),"Preview agrees with actual setup application and hardware limits: "+vehicle.spec.id+" / "+scope);
                    var query=new NativeSetupQuery{Search="café",Scope=NativeSetupQuery.ScopeName(scope)};check(query.Matches(saved,catalog)&&!new NativeSetupQuery{Search="not present"}.Matches(saved,catalog),"Setup search and scope match the saved metadata: "+vehicle.spec.id+" / "+scope);
                }
                var oem=p.componentsPart(PartCategory.HeadIntake);string upgraded=PartOperations.Buy(p,catalog,car.instanceId,catalog.Id(car.vehicleId,PartCategory.HeadIntake,1),true);SavedSetups.Save(p,car.instanceId,"Track build",SetupScope.WholeCar);var track=p.savedSetups.Last();PartOperations.Install(p,catalog,car.instanceId,oem.instanceId);
                var spare=p.parts.Find(i=>i.instanceId==upgraded);spare.condition.health=.61f;string state=JsonUtility.ToJson(p);var retained=NativeSetupPreview.Capture(p,car,track);
                check(retained.Proposed!=null&&retained.Plan.lines.Any(l=>l.itemId==upgraded&&!l.installed&&!l.purchase)&&retained.Proposed.source.components.Any(i=>i.instanceId==upgraded&&i.slot=="HeadIntake"&&i.condition.health==.61f)&&state==JsonUtility.ToJson(p),"Inventory preview projects the exact retained worn part into its slot: "+vehicle.spec.id);
                PartOperations.Sell(p,catalog,upgraded);state=JsonUtility.ToJson(p);var missing=NativeSetupPreview.Capture(p,car,track);
                check(missing.Proposed!=null&&missing.Plan.Missing==1&&missing.Plan.cost==InventoryPricing.NewSet(catalog.Find(track.parts.First(i=>i.slot=="HeadIntake").definitionId))&&state==JsonUtility.ToJson(p),"Missing hardware is a priced preview, never a granted asset: "+vehicle.spec.id);
                var bought=JsonUtility.FromJson<PlayerProfile>(state);bought.catalog=catalog;foreach(var c in bought.cars)if(p.cars.Find(i=>i.instanceId==c.instanceId).engineRebuild==null)c.engineRebuild=null;SavedSetups.Apply(bought,car.instanceId,track.id,p.revision,true,missing.Plan.cost);var real=NativePartBrowser.Inspect(InspectionBuild.Capture(bought,bought.SelectedCar));
                check(Math.Abs(real.peakHp-missing.Proposed.peakHp)<1e-8&&Math.Abs(real.vehicleMassKg-missing.Proposed.vehicleMassKg)<1e-8,"Missing-part curve matches the existing buy/apply transaction: "+vehicle.spec.id);
                p.cash=0;var unaffordable=NativeSetupPreview.Capture(p,car,track);check(unaffordable.Proposed!=null&&unaffordable.Plan.errors.Any(e=>e.Contains("Insufficient cash")),"Unaffordable setups remain inspectable with an explicit error: "+vehicle.spec.id);
            }
        }
        public static void LoadChecks(VehicleDefinition[] vehicles,Action<bool,string> check)
        {
            var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            foreach(var vehicle in vehicles){
                var p=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(p,catalog);p.cash=1000000;p.reputation=1000;VehicleCollection.Buy(p,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});var car=p.SelectedCar;
                p.componentsPart(PartCategory.HeadIntake).condition.health=.61f;car.fuel.litres=7;car.fluids.oilLitres=2;car.fluids.coolantLitres=3;
                foreach(SetupScope scope in Enum.GetValues(typeof(SetupScope))){
                    string id=SavedSetups.Save(p,car.instanceId,"Load "+scope,scope);p.savedSetups.Last().tune.launchRpm+=100;
                    string before=JsonUtility.ToJson(p);var quote=NativeSetupLoadQuote.Capture(p,id);quote.Validate(p);
                    check(before==JsonUtility.ToJson(p)&&!quote.BuyMissing&&quote.Cost==0,"Review captures the real scope without changing assets: "+vehicle.spec.id+" / "+scope);
                    PlayerProfile committed;string error;bool ok=MechanicsTransactions.Try(p,catalog,p.revision,q=>SavedSetups.Apply(q,quote.CarId,quote.SetupId,quote.Revision,quote.BuyMissing,quote.Cost),q=>false,out committed,out error);
                    check(!ok&&before==JsonUtility.ToJson(p),"Failed durable setup publication retains the entire current build: "+vehicle.spec.id+" / "+scope);
                    ok=MechanicsTransactions.Try(p,catalog,p.revision,q=>SavedSetups.Apply(q,quote.CarId,quote.SetupId,quote.Revision,quote.BuyMissing,quote.Cost),q=>true,out committed,out error);
                    check(ok&&committed.revision==p.revision+1&&committed.cash==p.cash&&committed.SelectedCar.fuel.litres==7&&committed.SelectedCar.fluids.oilLitres==2&&committed.parts.Find(i=>i.instanceId==p.componentsPart(PartCategory.HeadIntake).instanceId).condition.health==.61f,"Confirmed owned setup commits once with actual identities, wear and supplies: "+vehicle.spec.id+" / "+scope+" / "+error);
                    bool stale=false;try{quote.Validate(committed);}catch(InvalidOperationException){stale=true;}check(stale,"A completed setup quote cannot be replayed: "+vehicle.spec.id+" / "+scope);
                }
                string owned=p.componentsPart(PartCategory.HeadIntake).instanceId;string upgraded=PartOperations.Buy(p,catalog,car.instanceId,catalog.Id(car.vehicleId,PartCategory.HeadIntake,1),true);string setup=SavedSetups.Save(p,car.instanceId,"Missing kit",SetupScope.WholeCar);PartOperations.Install(p,catalog,car.instanceId,owned);PartOperations.Sell(p,catalog,upgraded);
                var purchase=NativeSetupLoadQuote.Capture(p,setup);int cash=p.cash;PlayerProfile bought;string problem;
                bool paid=MechanicsTransactions.Try(p,catalog,p.revision,q=>SavedSetups.Apply(q,purchase.CarId,purchase.SetupId,purchase.Revision,purchase.BuyMissing,purchase.Cost),q=>true,out bought,out problem);
                check(paid&&purchase.BuyMissing&&bought.cash==cash-purchase.Cost&&bought.parts.Any(i=>i.instanceId==owned&&i.location==PartLocation.Inventory&&i.condition.health==.61f),"Explicit buy/load charges only missing hardware and retains the worn original: "+vehicle.spec.id+" / "+problem);
                foreach(string change in new[]{"fuel","tune","blueprint","condition","price","hardware"}){
                    var copy=EngineWorkshop.Copy(p);var d=catalog.Find(catalog.Id(car.vehicleId,PartCategory.HeadIntake,1));int price=d.price;float power=d.power;
                    try{if(change=="fuel")copy.SelectedCar.fuel.litres+=1;else if(change=="tune")copy.SelectedCar.tune.driverMassKg+=1;else if(change=="blueprint")copy.savedSetups.Last().tune.launchRpm+=1;else if(change=="condition")copy.componentsPart(PartCategory.HeadIntake).condition.health=.4f;else if(change=="price")d.price++;else d.power+=.01f;
                        bool stale=false;try{purchase.Validate(copy);}catch(InvalidOperationException){stale=true;}check(stale,"Review rejects unrevisioned "+change+" changes: "+vehicle.spec.id);
                    }finally{if(change=="price")d.price=price;else if(change=="hardware")d.power=power;}
                }
                p.cash=0;bool refused=false;try{NativeSetupLoadQuote.Capture(p,setup);}catch(InvalidOperationException){refused=true;}check(refused,"An unaffordable plan cannot open an actionable load quote: "+vehicle.spec.id);
            }
        }
    }
#endif
}
