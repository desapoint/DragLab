using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Parity and descriptive workload checks for exact shared sale prices.</summary>
    public static class NativePartDisplayPriceChecks
    {
        static void Check(Action<bool,string> check,bool result,string label)
        {check(result,label);if(!result)throw new InvalidOperationException(label);}

        static PartInstance CopyFor(PartInstance source,string id)
        {
            var copy=source.Copy();copy.instanceId=id;
            int index=0;foreach(var member in copy.condition.elements??Array.Empty<EngineElement>())member.id=id+"/member/"+index++;
            return copy;
        }

        static void Parity(Action<bool,string> check,PlayerProfile profile,ValuationPolicy policy,string id,bool unit,long now,string label)
        {
            int expected=Valuations.LoosePriceOracleForChecks(profile,policy,"wholesale",id,unit,now);
            int actual=Valuations.LoosePriceView(profile,policy,"wholesale",now)(id,unit);
            Check(check,actual==expected,label+" exact integer parity ("+actual+")");
        }

        static string ValidationCosts(PlayerProfile profile,MechanicalCatalog catalog)
        {
            var result=new StringBuilder();
            Action<string,Action> measure=(name,action)=>{var timer=Stopwatch.StartNew();action();timer.Stop();result.AppendLine("  "+name+": "+timer.Elapsed.TotalMilliseconds.ToString("F3",CultureInfo.InvariantCulture)+" ms");};
            string direct=null,quoted=null;
            measure("VehicleListings.Validate",()=>direct=VehicleListings.Validate(profile,catalog));
            measure("PaidEconomy.ValidateState",()=>PaidEconomy.ValidateState(profile));
            measure("GarageBusiness.Validate",()=>GarageBusiness.Validate(profile));
            measure("MechanicsOwnership.Validate",()=>direct=MechanicsOwnership.Validate(profile,catalog));
            measure("MechanicsOwnership.ValidateForQuote",()=>quoted=MechanicsOwnership.ValidateForQuote(profile,catalog));
            result.AppendLine("  direct result: "+(direct??"valid"));
            result.AppendLine("  quote result: "+(quoted??"valid"));
            result.AppendLine("  dealer recovery fallback: "+(direct!=null&&direct.IndexOf("dealer",StringComparison.OrdinalIgnoreCase)>=0&&quoted==null));
            var template=profile.parts.FirstOrDefault(part=>part.location==PartLocation.Inventory);
            if(template!=null){var extras=new List<PartInstance>();for(int i=0;i<36;i++)extras.Add(CopyFor(template,"validation-extra-"+i));profile.parts.AddRange(extras);
                int baseCount=profile.parts.Count-extras.Count;
                measure("MechanicsOwnership.ValidateForQuote with 36 additional inventory parts",()=>quoted=MechanicsOwnership.ValidateForQuote(profile,catalog));
                result.AppendLine("  expanded parts="+profile.parts.Count+" (base="+baseCount+"); quote result: "+(quoted??"valid"));
                profile.parts.RemoveRange(profile.parts.Count-extras.Count,extras.Count);
            }
            return result.ToString();
        }

        static LooseEngineElement DealerUnit(LooseEngineElement source,string id)
        {
            var element=source.element.Copy();element.id=id;
            if(element is TireCondition tire)tire.definitionId=source.definitionId;
            else element.definitionId=source.definitionId;
            return new LooseEngineElement{definitionId=source.definitionId,storageId=source.storageId,unitPrice=source.unitPrice,tireUnit=source.tireUnit,element=element};
        }

        static void DealerValidationChecks(Action<bool,string> check,PlayerProfile profile,MechanicalCatalog catalog,PartInstance part,LooseEngineElement unit,ValuationPolicy policy,long now)
        {
            var dealer=profile.inventory.dealer;
            Action<DealerStock,string,string> verify=(stock,label,expected)=>{
                profile.inventory.dealer=new List<DealerStock>{stock};
                string strict=MechanicsOwnership.Validate(profile,catalog);
                long json=RuntimeDataEncodingProbe.PartConsumerJsonCallCount,key=RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount;
                bool unchangedFlag=stock.isUnit;string quote;
                int price=-1;using(RuntimeDataEncodingProbe.Measure("display-price/dealer-recovery")){
                    quote=MechanicsOwnership.ValidateForQuote(profile,catalog);
                    price=Valuations.LoosePriceView(profile,policy,"wholesale",now)(part.instanceId,false);
                }
                Check(check,strict==expected&&quote==null&&stock.isUnit==unchangedFlag&&
                    price==Valuations.LoosePriceOracleForChecks(profile,policy,"wholesale",part.instanceId,false,now)&&
                    RuntimeDataEncodingProbe.PartConsumerJsonCallCount==json&&RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount==key,
                    label+" strict error order, fresh factory/price parity, read-only state and zero encoders");
            };
            var dealerPart=part.Copy();dealerPart.instanceId="dealer-recovery-part";
            dealerPart.location=PartLocation.Inventory;dealerPart.ownerId="";dealerPart.slot="";
            int index=0;foreach(var e in dealerPart.condition.elements??Array.Empty<EngineElement>())e.id="dealer-recovery-part/member/"+index++;
            try{
                verify(new DealerStock{isUnit=true,part=dealerPart,paidPrice=5},"dealer part stale unit flag", "Invalid dealer stock at row 0: wrong part/unit discriminator.");
                verify(new DealerStock{isUnit=false,unit=DealerUnit(unit,"dealer-recovery-unit"),paidPrice=5},"dealer unit stale part flag", "Invalid dealer stock at row 0: wrong part/unit discriminator.");
                var emptyUnit=DealerUnit(unit,"empty-inactive-unit");emptyUnit.element.id="";
                profile.inventory.dealer=new List<DealerStock>{new DealerStock{isUnit=true,part=dealerPart,unit=emptyUnit,paidPrice=5}};
                Check(check,MechanicsOwnership.ValidateForQuote(profile,catalog)==null,"dealer recovery ignores only an empty inactive unit wrapper");
                profile.inventory.dealer=new List<DealerStock>{new DealerStock{isUnit=false,part=dealerPart,unit=DealerUnit(unit,"conflicting-unit"),paidPrice=5}};
                Check(check,MechanicsOwnership.ValidateForQuote(profile,catalog)=="Invalid dealer stock at row 0: conflicting part and unit payloads.","dealer recovery preserves conflicting populated payload rejection");
                profile.inventory.dealer=new List<DealerStock>{new DealerStock{isUnit=false,part=new PartInstance(),paidPrice=5}};
                Check(check,MechanicsOwnership.ValidateForQuote(profile,catalog)=="Invalid dealer stock at row 0: missing part/unit identity.","dealer recovery preserves missing merchandise identity rejection");
                profile.inventory.dealer=new List<DealerStock>{new DealerStock{isUnit=false,part=part.Copy(),paidPrice=5}};
                Check(check,MechanicsOwnership.ValidateForQuote(profile,catalog)=="Duplicate dealer part identity at row 0: "+part.instanceId+".","dealer recovery preserves duplicate dealer identity rejection");
                var descendantSource=profile.parts.FirstOrDefault(candidate=>candidate.condition!=null&&candidate.condition.elements!=null&&candidate.condition.elements.Length>0&&catalog.Find(candidate.definitionId)!=null&&EngineElements.Supported(catalog.Find(candidate.definitionId).category));
                if(descendantSource==null)throw new InvalidOperationException("Dealer recovery fixture requires an owned component with a typed descendant.");
                var malformed=descendantSource.Copy();malformed.instanceId="dealer-recovery-descendant-part";malformed.location=PartLocation.Inventory;malformed.ownerId="";malformed.slot="";
                for(int i=0;i<malformed.condition.elements.Length;i++)malformed.condition.elements[i].id="dealer-recovery-descendant/member/"+i;
                malformed.condition.elements[0].id=part.instanceId;
                profile.inventory.dealer=new List<DealerStock>{new DealerStock{isUnit=true,part=malformed,paidPrice=5}};
                Check(check,MechanicsOwnership.ValidateForQuote(profile,catalog)=="Duplicate dealer descendant at row 0 ("+malformed.instanceId+"): "+part.instanceId+".","dealer recovery preserves duplicate descendant rejection after discriminator mismatch");
            }finally{profile.inventory.dealer=dealer;}
        }

        public static void Run(Action<bool,string> check,string fixtureDirectory)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            Check(check,vehicles!=null&&vehicles.Length>0,"display-price vehicle fixtures load");
            var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            var profile=PlayerProfile.New(16102026);MechanicsOwnership.Ensure(profile,catalog);profile.cash=100000000;profile.reputation=10000;
            VehicleListings.Initialize(profile,catalog,25102526);
            foreach(var _ in MarketCatalogLayout.WarmSteps(catalog)){}
            var vehicle=vehicles.First(v=>v.spec.id=="starter-hatch");
            VehicleCollection.Buy(profile,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});

            var ordinary=catalog.parts.First(d=>d.category==PartCategory.HeadIntake);
            var ordinaryId=PartOperations.Buy(profile,catalog,profile.SelectedCar.instanceId,ordinary.id,false);
            var ordinaryPart=profile.parts.Find(p=>p.instanceId==ordinaryId);
            var tireDefinition=catalog.parts.First(d=>d.category==PartCategory.Tires);
            var tireId=PartOperations.Buy(profile,catalog,profile.SelectedCar.instanceId,tireDefinition.id,false);
            var tirePart=profile.parts.Find(p=>p.instanceId==tireId);
            // Match the existing transaction's post-operation initialization.
            // Direct Buy intentionally leaves fresh tire members to Ensure.
            MechanicsOwnership.Ensure(profile,catalog);
            var policy=ValuationPolicy.Defaults();long now=DragLab.Domain.RuntimeEntropy.UtcNow.Ticks;

            Parity(check,profile,policy,ordinaryId,false,now,"ordinary loose part");
            var saved=ordinaryPart.condition.Copy();ordinaryPart.condition.health=.31f;ordinaryPart.condition.distanceM=124567;ordinaryPart.condition.runningSeconds=78901;ordinaryPart.condition.stressExposure=13.75;ordinaryPart.condition.reconditionCount=2;
            Parity(check,profile,policy,ordinaryId,false,now,"worn part with history and reconditioning");
            ordinaryPart.condition.failed=true;ordinaryPart.condition.failureReason="Display price fault fixture";ordinaryPart.condition.health=0;
            Parity(check,profile,policy,ordinaryId,false,now,"failed part scrap recovery");ordinaryPart.condition=saved.Copy();

            var tires=tirePart.condition.tires;
            var originalTireCondition=tirePart.condition.Copy();
            for(int i=0;i<4;i++){tires[i].health=new[]{.91f,.68f,.42f,.17f}[i];tires[i].consumedLife=new[]{.08,.21,.49,.76}[i];tires[i].distanceM=(i+1)*28765;tires[i].runningSeconds=(i+2)*1200;}
            Parity(check,profile,policy,tireId,false,now,"four independent tire corners, wear and history");
            tires[2].failed=true;tires[2].health=0;tires[2].failureReason="Display tire fault fixture";
            Parity(check,profile,policy,tireId,false,now,"one failed tire corner");
            tires[2].failed=false;tires[2].health=.42f;tires[2].failureReason="";
            var tireUnitSource=tires[0].Copy();tirePart.condition=originalTireCondition;

            var spark=catalog.parts.First(d=>d.category==PartCategory.SparkPlugs);
            var sparkId=PartOperations.Buy(profile,catalog,profile.SelectedCar.instanceId,spark.id,false);
            MechanicsOwnership.Ensure(profile,catalog);
            var sparkPart=profile.parts.Find(p=>p.instanceId==sparkId);
            string looseUnitId=null;int looseUnitPrice=InventoryPricing.NewUnit(spark,EngineElements.Count(vehicle.spec,spark));
            if(sparkPart.condition.elements.Length>0){
                var unitElement=sparkPart.condition.elements[0].Copy();unitElement.id="display-unit-fixture";unitElement.health=.43f;unitElement.fault=ElementFault.Disabled;unitElement.severity=.6f;
                looseUnitId=unitElement.id;
                profile.looseEngineElements.Add(new LooseEngineElement{definitionId=spark.id,storageId=SharedInventory.Garage,unitPrice=looseUnitPrice,element=unitElement});
                Parity(check,profile,policy,unitElement.id,true,now,"loose faulty individual component");
            }

            var tireUnit=tireUnitSource;tireUnit.id="display-tire-unit-fixture";tireUnit.definitionId=tireDefinition.id;
            int tireUnitPrice=InventoryPricing.NewUnit(tireDefinition,4);
            profile.looseEngineElements.Add(new LooseEngineElement{definitionId=tireDefinition.id,storageId=SharedInventory.Garage,unitPrice=tireUnitPrice,tireUnit=true,element=tireUnit});
            Parity(check,profile,policy,tireUnit.id,true,now,"loose individual tire with own wear/history");

            var dealerUnitSource=profile.looseEngineElements.Find(item=>item.element.id==looseUnitId);
            if(sparkPart.condition.elements==null||sparkPart.condition.elements.Length==0||dealerUnitSource==null)throw new InvalidOperationException("Dealer recovery fixtures need a grouped spark unit and loose engine unit.");
            DealerValidationChecks(check,profile,profile.catalog,sparkPart,dealerUnitSource,policy,now);

            string profileBeforePrices=JsonUtility.ToJson(profile),policyBeforePrices=JsonUtility.ToJson(policy);
            var readOnlyView=Valuations.LoosePriceView(profile,policy,"wholesale",now);
            readOnlyView(ordinaryId,false);
            Check(check,JsonUtility.ToJson(profile)==profileBeforePrices&&JsonUtility.ToJson(policy)==policyBeforePrices,"display pricing leaves profile and policy read-only");

            // Public same-revision edits are deliberately fresh: a new display view must see them.
            int prior=Valuations.LoosePriceView(profile,policy,"wholesale",now)(ordinaryId,false);ordinaryPart.condition.health=.22f;
            int edited=Valuations.LoosePriceView(profile,policy,"wholesale",now)(ordinaryId,false);
            Check(check,prior!=edited,"same-revision condition edit is reflected by a newly generated price view");ordinaryPart.condition=saved.Copy();
            var heldView=Valuations.LoosePriceView(profile,policy,"wholesale",now);
            var wholesaler=policy.buyers.Find(b=>b.id=="wholesale");double buyerFactor=wholesaler.parts;wholesaler.parts=buyerFactor+.03;
            Check(check,heldView(ordinaryId,false)==Valuations.LoosePriceOracleForChecks(profile,policy,"wholesale",ordinaryId,false,now),"held view sees same-revision buyer-policy mutation like the old API");wholesaler.parts=buyerFactor;
            var quality=policy.qualities.Find(q=>q.quality==ordinary.quality);double qualityFactor=quality.factor;quality.factor=qualityFactor+.12;
            Check(check,heldView(ordinaryId,false)==Valuations.LoosePriceOracleForChecks(profile,policy,"wholesale",ordinaryId,false,now),"held view sees same-revision quality mutation like the old API");quality.factor=qualityFactor;
            int catalogPrice=ordinary.price;ordinary.price++;
            Check(check,heldView(ordinaryId,false)==Valuations.LoosePriceOracleForChecks(profile,policy,"wholesale",ordinaryId,false,now),"held view resolves current catalogue values like the old API");ordinary.price=catalogPrice;
            var originalCatalog=profile.catalog;var replacementCatalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));replacementCatalog.Find(ordinary.id).price=catalogPrice+1;profile.catalog=replacementCatalog;
            Check(check,heldView(ordinaryId,false)==Valuations.LoosePriceOracleForChecks(profile,policy,"wholesale",ordinaryId,false,now),"held view resolves a replacement catalogue reference like the old API");profile.catalog=originalCatalog;
            int replacedPartIndex=profile.parts.IndexOf(ordinaryPart);var replacement=ordinaryPart.Copy();replacement.condition.health=.27f;profile.parts[replacedPartIndex]=replacement;
            Check(check,heldView(ordinaryId,false)==Valuations.LoosePriceOracleForChecks(profile,policy,"wholesale",ordinaryId,false,now),"held view resolves a same-ID replacement asset like the old API");profile.parts[replacedPartIndex]=ordinaryPart;
            var looseTire=profile.looseEngineElements.Find(u=>u.element.id==tireUnit.id);
            string originalTireDefinition=looseTire.definitionId;looseTire.definitionId="missing-tire-definition";
            bool oldTireFailure=false,newTireFailure=false;
            try{Valuations.LoosePriceOracleForChecks(profile,policy,"wholesale",tireUnit.id,true,now);}catch(NullReferenceException){oldTireFailure=true;}
            try{heldView(tireUnit.id,true);}catch(NullReferenceException){newTireFailure=true;}
            looseTire.definitionId=originalTireDefinition;
            Check(check,oldTireFailure&&newTireFailure,"held tire-unit view preserves missing-definition failure behavior");
            long revision=profile.revision;var revisionView=Valuations.LoosePriceView(profile,policy,"wholesale",now);profile.revision++;
            bool revisionRejected=false;try{revisionView(ordinaryId,false);}catch(InvalidOperationException e){revisionRejected=e.Message=="Price view changed; refresh inventory.";}
            Check(check,revisionRejected,"revision mutation keeps the established stale-price error");profile.revision=revision;
            bool missingRejected=false;try{Valuations.LoosePriceView(profile,policy,"wholesale",now)("missing-display-price-id",false);}catch(InvalidOperationException e){missingRejected=e.Message=="Quote requires an owned loose part without retained descendants.";}
            Check(check,missingRejected,"missing loose asset keeps the established quote error");
            ordinaryPart.condition=saved;tirePart.condition=originalTireCondition;
            Check(check,JsonUtility.ToJson(profile)==profileBeforePrices&&JsonUtility.ToJson(policy)==policyBeforePrices,"parity and restored mutation probes leave profile and policy unchanged");

            // Scale fixtures reuse typed part data and only change unique identities; all
            // 1000 measurements include a fresh factory validation, with no JSON oracle loop.
            var baselineParts=profile.parts.ToList();var template=CopyFor(ordinaryPart,"scale-template");var all=new List<PartInstance>{template};
            for(int i=1;i<1000;i++)all.Add(CopyFor(template,"scale-"+i));
            profile.parts.Clear();profile.parts.AddRange(baselineParts);profile.parts.AddRange(all);
            var warm=Valuations.LoosePriceView(profile,policy,"wholesale",now);warm(all[0].instanceId,false);
            var report=new StringBuilder("Fresh shared display sale price measurements / milliseconds / calling-thread allocations\n");
            report.AppendLine("Validation stage diagnostics / current fixture parts="+profile.parts.Count+", cars="+profile.cars.Count+", catalog definitions="+catalog.parts.Count);
            report.Append(ValidationCosts(profile,catalog));
            check(true,"PRICE-COST validation stages on exact authored starter fixture; see report");
            foreach(int count in new[]{24,100,1000}){
                profile.parts.Clear();profile.parts.AddRange(baselineParts);profile.parts.AddRange(all.Take(count));
                long beforeBytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();var prices=Valuations.LoosePriceView(profile,policy,"wholesale",now);int checksum=0;
                for(int i=0;i<count;i++)checksum=unchecked(checksum+prices(all[i].instanceId,false));
                timer.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-beforeBytes;
                string line=count+" records incl. fresh wrapper/validation: "+timer.Elapsed.TotalMilliseconds.ToString("F3",CultureInfo.InvariantCulture)+" ms; "+bytes.ToString(CultureInfo.InvariantCulture)+" B; checksum="+checksum;
                report.AppendLine(line);check(true,"PRICE-PERF "+line);
                if(count<=24)for(int i=0;i<count;i++)Check(check,prices(all[i].instanceId,false)==Valuations.LoosePriceOracleForChecks(profile,policy,"wholesale",all[i].instanceId,false,now),"scale sample quote oracle parity "+i);
            }
            if(looseUnitId!=null){
                var baselineUnits=profile.looseEngineElements.ToList();var unitTemplate=profile.looseEngineElements.Find(e=>e.element.id==looseUnitId);
                var units=new List<LooseEngineElement>();for(int i=0;i<1000;i++){var element=unitTemplate.element.Copy();element.id="unit-scale-"+i;element.health=(i%5+1)*.18f;units.Add(new LooseEngineElement{definitionId=unitTemplate.definitionId,storageId=unitTemplate.storageId,unitPrice=looseUnitPrice,element=element});}
                profile.looseEngineElements.Clear();profile.looseEngineElements.AddRange(baselineUnits);profile.looseEngineElements.AddRange(units);
                var unitWarm=Valuations.LoosePriceView(profile,policy,"wholesale",now);unitWarm(units[0].element.id,true);
                foreach(int count in new[]{24,100,1000}){
                    profile.looseEngineElements.Clear();profile.looseEngineElements.AddRange(baselineUnits);profile.looseEngineElements.AddRange(units.Take(count));
                    long beforeBytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();var prices=Valuations.LoosePriceView(profile,policy,"wholesale",now);int checksum=0;
                    for(int i=0;i<count;i++)checksum=unchecked(checksum+prices(units[i].element.id,true));
                    timer.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-beforeBytes;
                    string line=count+" individual units incl. fresh wrapper/validation: "+timer.Elapsed.TotalMilliseconds.ToString("F3",CultureInfo.InvariantCulture)+" ms; "+bytes.ToString(CultureInfo.InvariantCulture)+" B; checksum="+checksum;
                    report.AppendLine(line);check(true,"PRICE-PERF "+line);
                    if(count<=24)for(int i=0;i<count;i++)Check(check,prices(units[i].element.id,true)==Valuations.LoosePriceOracleForChecks(profile,policy,"wholesale",units[i].element.id,true,now),"unit scale sample quote oracle parity "+i);
                }
            }
            if(!string.IsNullOrEmpty(fixtureDirectory)){Directory.CreateDirectory(fixtureDirectory);File.WriteAllText(Path.Combine(fixtureDirectory,"S06D2A_LIVE_PRICES.txt"),report.ToString(),new UTF8Encoding(false));}
        }
    }
#endif
}
