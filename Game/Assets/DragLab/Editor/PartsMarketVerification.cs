using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using DragLab.Data;

namespace DragLab.Editor
{
    public static class PartsMarketVerification
    {
        public static void Regressions()
        {
            var failures=new System.Collections.Generic.List<string>();
            foreach(var gate in new Action[]{Run,VehicleMarketVerification.RunStandalone,CartVerification.Run,InventoryCommerceVerification.Run,PaidOperationsVerification.RunStandalone,AssemblySetupVerification.RunStandalone,NativeShopCacheVerification.Run,NativeSetupPreviewVerification.Run})try{var timer=System.Diagnostics.Stopwatch.StartNew();gate();Debug.Log("MARKET GATE PASS "+gate.Method.DeclaringType.Name+" · "+timer.Elapsed.TotalSeconds.ToString("0.0")+" s");}catch(Exception e){failures.Add(gate.Method.DeclaringType.Name+": "+e.Message);Debug.LogError("MARKET GATE FAIL "+gate.Method.DeclaringType.Name+": "+e);}
            if(failures.Count>0)throw new InvalidOperationException(string.Join("\n",failures));
        }
        public static void Run()
        {
            var log=new StringBuilder();int checks=0;
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);checks++;log.AppendLine("PASS "+label);};
            var catalog=MechanicsTransactions.Catalog();long now=DateTime.UtcNow.Ticks;
            Func<int,PlayerProfile> create=seed=>{var p=PlayerProfile.New(now);p.cash=100000000;p.reputation=1000;VehicleListings.Initialize(p,catalog,seed);MechanicsOwnership.Ensure(p,catalog);return p;};
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v=>v.spec.id,StringComparer.Ordinal).ToArray();
            foreach(int seed in new[]{12345,-7,987654}){
                var p=create(seed);var other=create(seed);
                check(JsonUtility.ToJson(p.inventory)==JsonUtility.ToJson(other.inventory),seed+" identical seed/epoch produces exact stock including identities");
                string before=JsonUtility.ToJson(p.inventory);MechanicsOwnership.Ensure(p,catalog);check(before==JsonUtility.ToJson(p.inventory),seed+" normalization cannot replenish/reroll stock");
                var loaded=EngineWorkshop.Copy(p);check(SnapshotEquality.Profile(JsonUtility.ToJson(p),loaded,catalog),seed+" serialization round trip retains supply");
                check(p.inventory.market.parts.Any(s=>s.units==0)&&p.inventory.market.parts.Any(s=>s.units>0),seed+" seeded new availability includes local stock and delivery-only offers");
                check(p.inventory.dealer.Count<=176&&p.inventory.market.engines.Count<=6,seed+" global used/engine pool sizes have no per-model quotas");
                check(p.inventory.market.parts.Count(row=>row.units>0&&row.definitions.Any(id=>{var d=catalog.Find(id);return d.reputationRequired==0&&p.cars.Any(car=>catalog.Fits(catalog.Spec(car.vehicleId),d));}))>=5,seed+" at least five local new choices fit something in the owned garage");
                foreach(var v in vehicles){
                    check(catalog.parts.Where(d=>catalog.Fits(v.spec,d)).All(d=>{var stock=p.inventory.market.parts.Single(s=>s.definitions.Contains(d.id));return stock.initialUnits==0||stock.initialUnits>=PartsMarket.Count(v.spec,d);}),v.spec.id+" all initially stocked products support a whole required set");
                    foreach(bool used in new[]{false,true}){
                        var copy=EngineWorkshop.Copy(p);uint fixtureRandom=(uint)seed^91u;var offer=PartsMarket.CreateEngineOffer(copy,v.spec.id,used,"engine-fixture/"+v.spec.id+"/"+used,ref fixtureRandom);copy.inventory.market.engines.Add(offer);int price=PartsMarket.EnginePrice(offer),cash=copy.cash;string id=PartsMarket.BuyEngine(copy,offer.id,price);MechanicsOwnership.Ensure(copy,catalog);
                        var engine=copy.engines.Single(e=>e.instanceId==id);check(copy.cash==cash-price&&EngineWorkshop.Missing(copy,engine).Length==0&&string.IsNullOrEmpty(engine.parentVehicleId),seed+" "+v.spec.id+" "+used+" complete engine bought into storage");
                        check(MechanicsOwnership.Validate(copy,catalog)==null,seed+" "+v.spec.id+" engine ownership validates");
                        check(EngineWorkshop.Parts(copy,engine).All(i=>Math.Abs(i.condition.health-offer.parts.Single(original=>original.definitionId==i.definitionId).condition.health)<1e-6||!PartSemantics.Wears(catalog.Find(i.definitionId).category)),seed+" "+v.spec.id+" engine condition retained");
                        if(!used&&catalog.Recipe(v.spec.id).ContainsKey(PartCategory.Ignition)){var ignition=catalog.Find(catalog.Id(v.spec.id,PartCategory.Ignition,1));int members=EngineWorkshop.Count(copy,engine,ignition);EngineWorkshop.BuyPart(copy,id,ignition.id,PartsMarket.Quote(copy,ignition,members).cost);check(EngineWorkshop.Parts(copy,engine).Single(i=>i.definitionId==ignition.id).condition.elements.Length==members,"Stored engine purchases use the current ignition topology without duplicate spark plugs");}
                        if(used)check(offer.parts.Select(i=>i.instanceId).SequenceEqual(EngineWorkshop.Parts(copy,engine).Select(i=>i.instanceId))&&!copy.inventory.market.engines.Any(o=>o.id==offer.id),"Used engine is an exact nonrepeatable asset");
                    }
                }
                int epoch=p.vehicleStock.generation;string firstUsed=p.inventory.dealer.FirstOrDefault()?.Id;
                var loose=new PartInstance{instanceId="sold-probe",definitionId=p.parts.First().definitionId};p.parts.Add(loose);PartOperations.Sell(p,catalog,loose.instanceId);
                VehicleListings.AfterRace(p,catalog);VehicleListings.AfterRace(p,catalog);
                check(p.vehicleStock.generation==epoch&&p.inventory.dealer.Any(s=>s.Id==loose.instanceId)&&(firstUsed==null||p.inventory.dealer.Any(s=>s.Id==firstUsed)),"Trade-in and generated supply remain through two races");
                VehicleListings.AfterRace(p,catalog);check(p.vehicleStock.generation==epoch+1&&p.inventory.market.generation==epoch+1&&!p.inventory.dealer.Any(s=>s.Id==loose.instanceId||s.Id==firstUsed),"Third completed race refreshes both markets and expires trade-ins");
                other.vehicleStock.generation++;PartsMarket.Refresh(other);check(JsonUtility.ToJson(p.inventory)==JsonUtility.ToJson(other.inventory),"Refresh ignores player sales and generates the same next epoch");
                // Existing saves gain supply once, preserving old trade-ins.
                p.inventory.market=null;p.inventory.dealer.Add(new DealerStock{part=loose,paidPrice=1});PartsMarket.Ensure(p,true);check(p.inventory.dealer.Any(s=>s.Id==loose.instanceId),"Migration preserves existing trade-ins until refresh");
            }
            bool allUsedLarger=true,noEngines=false;var counts=new System.Collections.Generic.HashSet<string>();
            for(int seed=1;seed<=32;seed++){var sample=create(seed);sample.driving.guaranteedFittingParts=0;VehicleListings.Initialize(sample,catalog,seed);allUsedLarger&=sample.inventory.dealer.Select(row=>PartsMarket.ProductKey(catalog.Find(row.DefinitionId))).Distinct().Count()>sample.inventory.market.parts.Count(row=>row.initialUnits>0);noEngines|=sample.inventory.market.engines.Count==0;counts.Add(sample.inventory.dealer.Count+"/"+sample.inventory.market.engines.Count);}
            check(allUsedLarger&&noEngines&&counts.Count>4,"Every seeded used pool has more distinct products than stocked new products; engine pool may still be empty");
            var garage=create(414);VehicleCollection.Buy(garage,catalog,new VehicleOffer{model="v8-muscle",price=1,reputation=0,stock=new Tune(),paint="#FFFFFF"});
            foreach(int minimum in new[]{0,5,10,20}){garage.driving.guaranteedFittingParts=minimum;VehicleListings.Initialize(garage,catalog,414);var stock=garage.inventory.market;int fitting=garage.inventory.dealer.Where(row=>{var d=catalog.Find(row.DefinitionId);return d.reputationRequired<=garage.reputation&&garage.cars.Any(car=>catalog.Fits(catalog.Spec(car.vehicleId),d));}).Select(row=>PartsMarket.ProductKey(catalog.Find(row.DefinitionId))).Distinct().Count();check(stock.guaranteeAtRefresh==minimum&&fitting>=minimum,"Collective owned-garage used guarantee: "+minimum+" across two different platforms");string before=JsonUtility.ToJson(garage.inventory);garage.driving.guaranteedFittingParts=20-minimum;MechanicsOwnership.Ensure(garage,catalog);check(before==JsonUtility.ToJson(garage.inventory),"Difficulty changes cannot reroll current stock; next refresh only");}
            var rules=new DriverSettings();rules.ApplySimulationPreset(SimulationDifficulty.Simulation);check(rules.guaranteedFittingParts==0,"Hard simulation preset disables market guarantees");rules.ApplySimulationPreset(SimulationDifficulty.Arcade);check(rules.guaranteedFittingParts==10,"Easy simulation preset offers a larger market guarantee");
            var ordered=create(981);string orderedInventory=JsonUtility.ToJson(ordered.inventory);catalog.parts.Reverse();try{check(orderedInventory==JsonUtility.ToJson(create(981).inventory),"Catalogue iteration order cannot change saved-seed selections, quantities or aliases");}finally{catalog.parts.Reverse();}
            var scarce=create(981);scarce.driving.guaranteedFittingParts=0;VehicleListings.Initialize(scarce,catalog,981);var generous=EngineWorkshop.Copy(scarce);generous.driving.guaranteedFittingParts=20;VehicleListings.Initialize(generous,catalog,981);check(JsonUtility.ToJson(scarce.inventory.market.engines)==JsonUtility.ToJson(generous.inventory.market.engines),"Guarantee top-ups cannot change the independent engine selection/quantity rolls");
            var batch=create(77);var samplePart=catalog.Find(catalog.Id("starter-hatch",PartCategory.Pistons));var batchSpec=catalog.Resolve("starter-hatch",catalog.Recipe("starter-hatch"),new Tune()).spec;int complete=0,single=0,partialBatch=0;uint batchRandom=229;
            batch.inventory.dealer.Clear();for(int n=0;n<200;n++){int previous=batch.inventory.dealer.Count;PartsMarket.AddUsedSupply(batch,batchSpec,samplePart,"quantity-probe/"+n,ref batchRandom);var added=batch.inventory.dealer.Skip(previous).ToArray();if(added.Any(row=>row.part!=null))complete++;else if(added.Length==1)single++;else partialBatch++;}
            check(complete>single+partialBatch&&single>0&&partialBatch>0,"Repeated components favour complete matching sets while preserving single/partial used stock");check(MechanicsOwnership.Validate(batch,catalog)==null,"Generated loose used members preserve unique valid condition and identities");
            foreach(var category in new[]{PartCategory.Tires,PartCategory.Wheels,PartCategory.Ignition}){
                var d=catalog.Find(catalog.Id("starter-hatch",category));for(int n=0;n<30;n++)PartsMarket.AddUsedSupply(batch,batchSpec,d,"quantity-probe/"+category+"/"+n,ref batchRandom);
                check(batch.inventory.dealer.Any(row=>row.unit!=null&&row.unit.definitionId==d.id),category+" seeded supply includes valid individual replacements");
            }
            check(MechanicsOwnership.Validate(batch,catalog)==null,"Generated tire, wheel and ignition singles retain valid per-member metadata");
            var supplied=batch.inventory.dealer.First(row=>row.unit!=null);string suppliedId=supplied.Id;var member=supplied.unit;int usedPrice=InventoryPricing.BuyUsed(supplied),usedCash=batch.cash;var usedQuote=PartCart.Quote(batch,batch.SelectedCar.instanceId,new[]{new PartCartLine{action=CartAction.BuyUsed,assetId=suppliedId,definitionId=supplied.DefinitionId,role=member.element.role}},now,300);PartCart.Checkout(batch,usedQuote,now);
            check(batch.cash==usedCash-usedPrice&&batch.looseEngineElements.Any(u=>u.element.id==suppliedId&&u.element.health==member.element.health)&&!batch.inventory.dealer.Any(row=>row.Id==suppliedId),"Generated individual stock buys once with its exact identity, wear and price");
            var missingStock=create(912);string ownedBefore=JsonUtility.ToJson(missingStock.parts),carsBefore=JsonUtility.ToJson(missingStock.vehicleStock);
            missingStock.inventory.market=null;bool refused=false;try{MechanicsOwnership.Ensure(missingStock,catalog);}catch(InvalidOperationException){refused=true;}
            check(refused&&ownedBefore==JsonUtility.ToJson(missingStock.parts)&&carsBefore==JsonUtility.ToJson(missingStock.vehicleStock),
                "Current save with missing supply is rejected without inventing stock or mutating owned hardware");
            var test=create(41);foreach(var stock in test.inventory.market.parts)stock.units=0;
            var car=test.SelectedCar;var piston=catalog.Find(MechanicsOwnership.Definitions(test,car)[PartCategory.Pistons]);int count=InventoryUnits.Count(test,car,piston);
            var quote=PartsMarket.Quote(test,piston,count);check(quote.expressUnits==count&&quote.fee>=Math.Ceiling(piston.price*.05)&&quote.fee<=Math.Ceiling(piston.price*.1),"Express rate is 5–10% of absent goods");
            var pool=test.inventory.market.parts.Single(s=>s.definitions.Contains(piston.id));pool.units=Math.Max(1,count/2);pool.initialUnits=Math.Max(pool.initialUnits,pool.units);int local=pool.units;
            var partial=PartsMarket.Quote(test,piston,count);check(partial.localUnits==local&&partial.expressUnits==count-local&&partial.fee==(int)Math.Ceiling(piston.price*(count-local)/(double)count*pool.deliveryPercent/100.0)&&pool.units==local,"Partial local stock charges delivery only on the missing fraction, without mutating a preview");
            int partialCash=test.cash;PartOperations.Buy(test,catalog,car.instanceId,piston.id,false);check(pool.units==0&&test.cash==partialCash-partial.cost&&EngineWorkshop.Copy(test).inventory.market.parts.Single(s=>s.definitions.Contains(piston.id)).units==0,"Buying depletes only local supply and reload cannot restock it");
            var staleLines=new[]{new PartCartLine{definitionId=piston.id}};var staleQuote=PartCart.Quote(test,car.instanceId,staleLines,now,300);var refreshed=EngineWorkshop.Copy(test);for(int n=0;n<3;n++)VehicleListings.AfterRace(refreshed,catalog);bool denied=false;string refreshBefore=JsonUtility.ToJson(refreshed);try{PartCart.Checkout(refreshed,staleQuote,now);}catch(InvalidOperationException){denied=true;}check(denied&&refreshBefore==JsonUtility.ToJson(refreshed),"Market refresh invalidates stale quotes before any charge or transfer");
            var row=new PartCartLine{definitionId=piston.id,quantity=2};var lines=new[]{row,new PartCartLine{definitionId=piston.id,action=CartAction.NewUnits,quantity=2,role=InventoryUnits.Roles(test,car,piston).First()}};
            int expected=PartCart.Total(test,car.instanceId,lines),money=test.cash;var cart=PartCart.Quote(test,car.instanceId,lines,now,300);PartCart.Checkout(test,cart,now);test.revision++;
            check(test.cash==money-expected&&test.inventory.market.parts.All(s=>s.units==0),"Mixed set/unit cart charges quoted express total once without negative stock");
            var alias=test.inventory.market.parts.First(s=>s.definitions.Count>1);alias.units=alias.initialUnits=Math.Max(8,alias.initialUnits);var d1=catalog.Find(alias.definitions[0]);var d2=catalog.Find(alias.definitions[1]);PartsMarket.Consume(test,d1,8,true);check(PartsMarket.Available(test,d2)==alias.initialUnits-1,"Equivalent definition aliases share one quantity pool");
            string setup=SavedSetups.Save(test,car.instanceId,"market probe",SetupScope.WholeCar);foreach(var i in test.parts){if(catalog.Find(i.definitionId).category==PartCategory.Tires){foreach(var t in i.condition.tires){t.health=0;t.consumedLife=1;t.failed=true;t.failureReason="Replacement test failure";}TireDynamics.SyncSummary(i.condition);}else{i.condition.health=.2f;i.condition.failed=true;i.condition.failureReason="Replacement test failure";foreach(var e in i.condition.elements)e.health=0;}}
            foreach(var i in test.parts.Where(i=>i.location==PartLocation.Inventory).ToArray())test.parts.Remove(i);
            foreach(var stock in test.inventory.market.parts)stock.units=0;test.looseEngineElements.Clear();var plan=SavedSetups.Preview(test,car.instanceId,setup);money=test.cash;
            check(plan.errors.Count==0&&plan.deliveryFee>0,"Missing saved build quotes express goods");
            PlayerProfile loadedSetup;string setupError;
            bool appliedSetup=MechanicsTransactions.Try(test,catalog,test.revision,p=>SavedSetups.Apply(p,car.instanceId,setup,p.revision,true,plan.cost),p=>true,out loadedSetup,out setupError);
            check(appliedSetup,"Saved build uses the game's normalization/commit boundary: "+setupError);test=loadedSetup;car=test.SelectedCar;
            check(test.cash==money-plan.cost,"Saved build checkout equals combined supply quote");
            var request=new PaymentRequest{kind=PaymentKind.Part,carId=car.instanceId,targetId=piston.id,install=true};var payment=PaidEconomy.Quote(test,catalog,new OperatingPolicy(),ValuationPolicy.Defaults(),request,now);money=test.cash;PaidEconomy.Pay(test,catalog,new OperatingPolicy(),ValuationPolicy.Defaults(),payment,now);test.revision++;check(test.cash==money-payment.Cost&&payment.Cost>piston.price,"Same-part maintenance includes delivery and charges once");
            var timing=System.Diagnostics.Stopwatch.StartNew();for(int n=0;n<3;n++){var t=PartCart.Quote(test,car.instanceId,new[]{new PartCartLine{definitionId=piston.id}},now,300);PlayerProfile next;string failure;if(!MechanicsTransactions.Try(test,catalog,test.revision,p=>PartCart.Checkout(p,t,now),p=>true,out next,out failure))throw new InvalidOperationException(failure);test=next;}log.AppendLine("Measured quote + atomic transaction mean: "+(timing.Elapsed.TotalMilliseconds/3).ToString("0.0")+" ms; serialized profile: "+JsonUtility.ToJson(test).Length+" chars");
            string snapshot=JsonUtility.ToJson(test);PlayerProfile discarded;string error;bool committed=MechanicsTransactions.Try(test,catalog,test.revision,p=>PartOperations.Buy(p,catalog,p.SelectedCar.instanceId,piston.id,false),p=>false,out discarded,out error);check(!committed&&snapshot==JsonUtility.ToJson(test),"Failed persistence retains stock, cash and asset ownership");
            check(MechanicsOwnership.Validate(test,catalog)==null,"Final market/profile validates");
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/parts-market"));Directory.CreateDirectory(folder);log.AppendLine("Checks: "+checks+"; failures: 0");File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString(),new UTF8Encoding(false));Debug.Log("Parts market verification passed: "+checks+" checks.");
        }
    }
}
