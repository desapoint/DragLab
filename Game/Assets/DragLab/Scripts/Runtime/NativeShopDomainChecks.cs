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
        public static int CardWindows()
        {
            int checks=0;
            foreach(int count in new[]{0,1,2,3,7,2226,10000})foreach(float height in new[]{0f,160f,340f,720f,1080f})foreach(float offset in new[]{0f,1f,246f,247f,12000f,10000000f}){
                var window=NativeCardWindow.Calculate(count,offset,height,246);
                if(window.First<0||window.Last<window.First||window.Last>count||window.First%2!=0||window.Before<0||window.After<0||window.Last-window.First>2*(Mathf.CeilToInt(Mathf.Max(246,height)/246)+4))throw new Exception("Unbounded or invalid card window");
                float total=(count+1)/2*246;
                if(Mathf.Abs(window.Before+((window.Last-window.First+1)/2)*246+window.After-total)>.01f)throw new Exception("Card window loses scroll extent");
                checks++;
            }
            return checks;
        }
        public static void UnitPreviews(VehicleDefinition[] vehicles,Action<bool,string> check)
        {
            var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            foreach(var vehicle in vehicles){
                var p=PlayerProfile.New(1234);MechanicsOwnership.Ensure(p,catalog);p.cash=1000000;p.reputation=1000;
                VehicleCollection.Buy(p,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
                foreach(var category in new[]{PartCategory.Pistons,PartCategory.Rods,PartCategory.PistonRings,PartCategory.Bearings,PartCategory.Valves,PartCategory.ValveSprings,PartCategory.ValveSeals,PartCategory.SparkPlugs,PartCategory.GlowPlugs}){
                    var fitted=p.componentsPart(category);if(fitted==null)continue;
                    var definition=catalog.Find(catalog.Id(vehicle.spec.id,category,1));if(definition==null||definition.id==fitted.definitionId)continue;
                    var template=InventoryUnits.Template(p,p.SelectedCar,definition);if(template.condition.elements.Length==0)continue;
                    var unit=new LooseEngineElement{definitionId=definition.id,unitPrice=20,element=template.condition.elements[0].Copy()};
                    unit.element.id="unit-preview/"+category;unit.element.definitionId=definition.id;unit.element.health=.37f;
                    if(EngineElements.Faults(category).Contains(ElementFault.CompressionLeak)){unit.element.fault=ElementFault.CompressionLeak;unit.element.severity=.2f;}
                    var stock=new DealerStock{unit=unit,isUnit=true,paidPrice=20};p.inventory.dealer.Add(stock);
                    var cache=new NativePartPreviewCache();
                    NativePartBrowser Model(string page="Shop",PartCartLine[] lines=null)=>new NativePartBrowser(p,lines??Array.Empty<PartCartLine>(),lines!=null,page,s=>InventoryPricing.BuyUsed(s),(_,u)=>20,cache);
                    string snapshot=JsonUtility.ToJson(p);var browser=Model();var entry=browser.Entries.Single(e=>e.AssetId==unit.element.id);browser.Evaluate(entry);
                    string label=vehicle.spec.id+" / "+category;
                    check(entry.Proposed!=null&&entry.RequiresMatchingSet&&entry.Position==null&&entry.Compatibility=="Compatible","Unmatched used unit has an explicit set estimate and no installation target: "+label+" / "+entry.Reason);
                    if(entry.Proposed==null)continue;
                    var proposed=entry.Proposed.source.components.Single(i=>i.slot==category.ToString());var member=proposed.condition.elements.Single(e=>e.id==unit.element.id);
                    check(member.health==unit.element.health&&member.fault==unit.element.fault&&member.severity==unit.element.severity&&proposed.condition.elements.Where(e=>e.id!=unit.element.id).All(e=>e.health==1&&e.fault==ElementFault.None),"Estimate retains offered unit condition/fault and explicitly assumes new matching companions: "+label);
                    check(entry.Price==InventoryPricing.BuyUsed(stock)&&JsonUtility.ToJson(p)==snapshot,"Comparison preserves single-unit price and all real assets/resources/stock: "+label);
                    var again=Model();var restored=again.Entries.Single(e=>e.AssetId==unit.element.id);again.Evaluate(restored);
                    check(restored.RequiresMatchingSet&&ReferenceEquals(restored.Proposed,entry.Proposed)&&again.EvaluationCount==0,"Cached estimate preserves its set requirement: "+label);
                    var plan=Model(lines:new[]{new PartCartLine{definitionId=definition.id,quantity=1,install=true}});var planned=plan.Entries.Single(e=>e.AssetId==unit.element.id);plan.Evaluate(planned);
                    check(plan.PlanError==null&&planned.Proposed!=null&&!planned.RequiresMatchingSet&&planned.Position!=null,"Pending matching set enables a real single-position comparison: "+label+" / "+plan.PlanError+" / "+planned.Reason);
                    p.inventory.dealer.Remove(stock);p.looseEngineElements.Add(unit);string ownedSnapshot=JsonUtility.ToJson(p);
                    var owned=Model("Inventory");var ownedEntry=owned.Entries.Single(e=>e.AssetId==unit.element.id);owned.Evaluate(ownedEntry);
                    check(ownedEntry.RequiresMatchingSet&&ownedEntry.Proposed!=null&&ownedEntry.Position==null,"Stored unmatched unit retains the same set estimate without enabling install: "+label);
                    bool rejected=false;try{InventoryUnits.Install(p,p.SelectedCar.instanceId,unit.element.id,fitted.condition.elements[0].id);}catch(InvalidOperationException){rejected=true;}
                    check(rejected&&JsonUtility.ToJson(p)==ownedSnapshot,"Real installation rejects mismatched individual specifications without mutation: "+label);
                    p.looseEngineElements.Remove(unit);
                }
            }
        }
        public static void Run(VehicleDefinition[] vehicles, Action<bool,string> check)
        {
            foreach(var vehicle in vehicles){var p=PlayerProfile.New(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);MechanicsOwnership.Ensure(p,new MechanicalCatalog(vehicles.Select(v=>v.spec)));p.cash=1000000;p.reputation=1000;VehicleCollection.Buy(p,p.catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});var car=p.SelectedCar;var cache=new NativePartPreviewCache();
                var countSupply=PartsMarket.Counter(p,car);foreach(var sample in p.catalog.parts.Where(d=>p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)).GroupBy(d=>d.category+"/"+d.fuelDelivery).Select(g=>g.First()))check(countSupply(sample)==InventoryUnits.Count(p,car,sample),"Fast shelf quantity agrees with authoritative incoming topology: "+vehicle.spec.id+" / "+sample.category+" / "+sample.fuelDelivery);
                NativePartBrowser Model()=>new NativePartBrowser(p,Array.Empty<PartCartLine>(),false,"Shop",_=>100,(_,u)=>100,cache);
                var a=Model();
                var groups=p.catalog.parts.GroupBy(NativePartBrowser.NewProductKey,StringComparer.Ordinal).ToArray();
                check(a.Entries.Count(e=>e.Source=="New")==groups.Length&&groups.Length<p.catalog.parts.Count,"Equivalent new products appear once, retaining different specifications: "+vehicle.spec.id+" / "+p.catalog.parts.Count+" definitions -> "+groups.Length+" products");
                check(groups.All(g=>g.All(d=>p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)==p.catalog.Fits(p.catalog.Spec(car.vehicleId),g.First()))),"Merged aliases have identical physical fitment: "+vehicle.spec.id);
                var head=a.Entries.First(e=>e.Definition.id==p.catalog.Id(car.vehicleId,PartCategory.HeadIntake,1));a.Evaluate(head);var b=Model();var next=b.Entries.First(e=>e.Id==head.Id);b.Evaluate(next);check(ReferenceEquals(a.Current,b.Current)&&ReferenceEquals(head.Proposed,next.Proposed)&&b.EvaluationCount==0,"Exact current and candidate snapshots are reused: "+vehicle.spec.id);
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
                var tireDefinition=p.catalog.Find(p.catalog.Id(car.vehicleId,PartCategory.Tires,1));var specs=NativePartSpecifications.Summary(tireDefinition);check(specs.Select(s=>s.key).SequenceEqual(new[]{"grip","rollinglife","slidinglife","tireoptimal"})&&Math.Abs(specs[1].value-tireDefinition.tireLifeDistanceM/1000)<1e-6,"Tire summaries prioritize actual authored grip and lifespan rather than torque strength: "+vehicle.spec.id);
                var pack=p.componentsPart(PartCategory.Tires);TireDynamics.Ensure(pack.condition);foreach(var tire in pack.condition.tires)if(string.IsNullOrEmpty(tire.id))tire.id=MechanicsOwnership.NewId("tire");pack.condition.tires[0].health=.4f;var retained=pack.condition.tires.Skip(1).Select(t=>JsonUtility.ToJson(t)).ToArray();int cash=p.cash;var part=p.catalog.Find(pack.definitionId);int unitCost=PartsMarket.Quote(p,part,4,true).cost;var unit=InventoryUnits.Buy(p,car.instanceId,part.id,"tire");InventoryUnits.Install(p,car.instanceId,unit.element.id,"tire:0");check(p.cash==cash-unitCost&&pack.condition.tires.Skip(1).Select(t=>JsonUtility.ToJson(t)).SequenceEqual(retained)&&p.looseEngineElements.Any(u=>u.tireUnit&&u.tire.health==.4f),"One tire can be bought and replaced while retaining other corners and the worn original: "+vehicle.spec.id);
                ProductIdentityChecks(p,check);
            }
        }
        static void ProductIdentityChecks(PlayerProfile p,Action<bool,string> check)
        {
            var car=p.SelectedCar;var d=p.catalog.Find(p.catalog.Id(car.vehicleId,PartCategory.HeadIntake,1));var alias=d.Copy();alias.id=d.id+"-equivalent-offer";p.catalog.parts.Add(alias);
            NativePartBrowser Model(bool planned=false,PartCartLine[] cart=null)=>new NativePartBrowser(p,cart??Array.Empty<PartCartLine>(),planned,"Shop",s=>InventoryPricing.BuyUsed(s),(_,u)=>100);
            var browser=Model();var key=NativePartBrowser.NewProductKey(d);
            check(browser.Entries.Count(e=>e.Source=="New"&&NativePartBrowser.NewProductKey(e.Definition)==key)==1&&p.catalog.Find(alias.id)==alias,"A repeated new offer has one card while every saved/catalog ID remains valid: "+car.vehicleId);
            var fitted=p.componentsPart(PartCategory.Valves);var unitRole=InventoryUnits.Roles(p,car,p.catalog.Find(fitted.definitionId)).First();
            var fittedOffer=browser.Entries.Single(e=>e.Source=="New"&&e.Definition.id==fitted.definitionId);
            check(InventoryUnits.Positions(p,car,fittedOffer.Definition.id,unitRole).Length>0,"Grouping preserves the exact fitted ID for individual-member replacement: "+car.vehicleId);
            foreach(string field in new[]{"fitment","price","reputationRequired","tuning","power","wearEnergyJ","thermalLimitC","massKg","positionFromFrontAxleM","boreMm","fuelKind","turboSize","rpmLimit","torqueLimitNm"}){
                var variant=d.Copy();variant.id=d.id+"-different-"+field;var info=typeof(PartDefinition).GetField(field);
                if(info.FieldType==typeof(string))info.SetValue(variant,(string)info.GetValue(d)+"-different");
                else if(info.FieldType==typeof(float))info.SetValue(variant,(float)info.GetValue(d)+Math.Max(1,Math.Abs((float)info.GetValue(d))*.1f));
                else if(info.FieldType==typeof(int))info.SetValue(variant,(int)info.GetValue(d)+1);
                else info.SetValue(variant,Enum.ToObject(info.FieldType,Convert.ToInt32(info.GetValue(d))+1));
                p.catalog.parts.Add(variant);var distinct=Model();
                check(distinct.Entries.Any(e=>e.Definition.id==variant.id),"Same-name new variants retain a separate offer when "+field+" differs: "+car.vehicleId);p.catalog.parts.Remove(variant);
            }
            var stock=p.componentsPart(PartCategory.HeadIntake);var used=stock.Copy();used.instanceId="product-check-used-a";used.location=PartLocation.Inventory;used.ownerId=used.slot="";used.condition.health=.61f;var second=used.Copy();second.instanceId="product-check-used-b";
            p.inventory.dealer.Add(new DealerStock{part=used,paidPrice=45});p.inventory.dealer.Add(new DealerStock{part=second,paidPrice=45});string snapshot=JsonUtility.ToJson(p);
            var usedBrowser=Model();check(usedBrowser.Entries.Count(e=>e.Source=="Used"&&(e.AssetId==used.instanceId||e.AssetId==second.instanceId))==2&&JsonUtility.ToJson(p)==snapshot,"Separate used identities remain separate even at equal wear/price; browsing never saves or changes ownership: "+car.vehicleId);
            var lines=new[]{new PartCartLine{definitionId=alias.id,quantity=1,install=true}};var planned=Model(true,lines);
            check(planned.PlanError==null&&planned.Entries.Any(e=>e.Source=="New"&&e.Definition.id==alias.id),"Planned equivalent hardware keeps its real ID as the grouped purchase representative: "+car.vehicleId);
            var preview=PartCart.PreviewProfile(p,car,lines);check(preview.SelectedCar.installed.slots.Any(slot=>preview.parts.Any(part=>part.instanceId==slot.partInstanceId&&part.definitionId==alias.id))&&p.catalog.Find(d.id)!=null&&p.catalog.Find(alias.id)!=null,"Grouped offers still use authoritative exact-ID checkout/installation without rewriting aliases: "+car.vehicleId);
        }
    }
#endif
}
