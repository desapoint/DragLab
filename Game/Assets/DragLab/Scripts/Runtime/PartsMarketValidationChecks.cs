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
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DragLab.Runtime
{
#if UNITY_EDITOR
    /// <summary>Frozen first-result and exception parity for PartsMarket.Validate.</summary>
    public static class PartsMarketValidationChecks
    {
        sealed class Outcome
        {
            public string Error, ExceptionType, ExceptionMessage;
        }

        static Outcome Invoke(Func<string> action)
        {
            try{return new Outcome{Error=action()};}
            catch(Exception error){return new Outcome{ExceptionType=error.GetType().FullName,ExceptionMessage=error.Message};}
        }

        // Frozen pre-loop-change public body. Keep its whole call graph and LINQ
        // source expression as the independent behavior oracle.
        static string LegacyValidate(PlayerProfile p)
        {
            var m=p.inventory.market;
            if(m==null||m.version!=2||m.seed!=p.vehicleStock.seed||m.generation!=p.vehicleStock.generation||m.guaranteeAtRefresh<0||m.guaranteeAtRefresh>20||m.parts==null||m.engines==null)return "Invalid parts market epoch.";
            var ids=new HashSet<string>();foreach(var s in m.parts)if(s==null||s.definitions==null||s.definitions.Count==0||s.units<0||s.units>s.initialUnits||s.initialUnits<0||s.deliveryPercent<5||s.deliveryPercent>10||s.definitions.Any(id=>p.catalog.Find(id)==null||!ids.Add(id)))return "Invalid new-parts stock.";
            string reservationError=CartReservations.Validate(p);if(reservationError!=null)return reservationError;
            ids=new HashSet<string>(p.parts.SelectMany(i=>i.ItemIds).Concat(p.engines.Select(e=>e.instanceId)).Concat(p.looseEngineElements.Select(u=>u.element.id)).Concat(p.inventory.dealer.SelectMany(s=>s.IsPart?s.part.ItemIds:s.IsUnit?new[]{s.unit.element.id}:Enumerable.Empty<string>())).Concat(p.vehicleStock.listings.SelectMany(o=>o.parts.SelectMany(i=>i.ItemIds))));foreach(var o in m.engines){if(o==null||string.IsNullOrWhiteSpace(o.id)||!ids.Add(o.id)||!ids.Add(o.engine?.instanceId??"")||o.quantity<0||o.quantity>3||o.used&&o.quantity>1||o.price<1||o.engine==null||o.engine.vehicleDefinitionId!=o.model||!string.IsNullOrEmpty(o.engine.parentVehicleId)||o.parts==null||o.parts.Count==0||o.engine.slots==null||o.engine.slots.Count!=o.parts.Count)return "Invalid engine stock.";
                var map=new Dictionary<PartCategory,string>();foreach(var i in o.parts){var d=p.catalog.Find(i.definitionId);if(d==null||!MechanicalCatalog.EngineSlot(d.category)||!p.catalog.Fits(p.catalog.Spec(o.model),d)||i.Validate()!=null||!ids.Add(i.instanceId)||i.ownerId!=o.engine.instanceId||i.location!=PartLocation.Assembly||map.ContainsKey(d.category)||!o.engine.slots.Any(s=>s.partInstanceId==i.instanceId&&s.slot==d.category.ToString()))return "Invalid offered engine component.";map.Add(d.category,d.id);foreach(var e in i.condition.elements)if(!ids.Add(e.id)||!EngineElements.FaultAllowed(d.category,e))return "Invalid offered engine descendant.";}
                if(p.catalog.Recipe(o.model).Any(v=>MechanicalCatalog.EngineSlot(v.Key)&&!map.ContainsKey(v.Key))||o.engine.fluids==null||o.engine.fluids.Validate(p.catalog.Spec(o.model).fluids)!=null||o.engine.tune==null)return "Incomplete offered engine.";
            }return null;
        }

        static void Check(Action<bool,string> check,bool condition,string label)
        {
            check(condition,label);
            if(!condition)throw new InvalidOperationException("Parts market validation parity failed: "+label);
        }

        static Outcome Compare(Action<bool,string> check,PlayerProfile profile,string label)
        {
            var expected=Invoke(()=>LegacyValidate(profile));
            var actual=Invoke(()=>PartsMarket.Validate(profile));
            Check(check,expected.ExceptionType==actual.ExceptionType,label+" exact exception type");
            Check(check,expected.ExceptionMessage==actual.ExceptionMessage,label+" exact exception message");
            Check(check,expected.Error==actual.Error,label+" exact first error / acceptance");
            return expected;
        }

        static void CompareExpected(Action<bool,string> check,PlayerProfile profile,string expected,string label)
        {
            var outcome=Compare(check,profile,label);
            Check(check,outcome.ExceptionType==null,label+" expected a returned validation result");
            Check(check,outcome.Error==expected,label+" expected result");
        }

        static void Mutate(Action<bool,string> check,PlayerProfile profile,string label,Action apply,Action restore,string expected=null)
        {
            try
            {
                apply();
                if(expected==null)Compare(check,profile,label);
                else CompareExpected(check,profile,expected,label);
            }
            finally{restore();}
        }

        static void MutateAccepted(Action<bool,string> check,PlayerProfile profile,string label,Action apply,Action restore)
        {
            try{apply();CompareExpected(check,profile,null,label);}
            finally{restore();}
        }

        static double Median(double[] samples)=>samples.OrderBy(value=>value).ElementAt(samples.Length/2);

        static string MedianMax(Action action,int repeats)
        {
            action();var samples=new double[repeats];
            for(int i=0;i<repeats;i++){var watch=Stopwatch.StartNew();action();watch.Stop();samples[i]=watch.Elapsed.TotalMilliseconds;}
            return "median_ms="+Median(samples).ToString("R",CultureInfo.InvariantCulture)+
                " max_ms="+samples.Max().ToString("R",CultureInfo.InvariantCulture)+" samples="+repeats.ToString(CultureInfo.InvariantCulture);
        }

        static void SourceParity(Action<bool,string> check,PlayerProfile profile)
        {
            var parts=profile.parts;var engines=profile.engines;var loose=profile.looseEngineElements;
            var dealer=profile.inventory.dealer;var listings=profile.vehicleStock.listings;
            CompareExpected(check,profile,null,"full-roster base profile accepted");

            // Initial ID-set semantics: ItemIds adds instanceId even when null,
            // ignores null member objects and null/empty member IDs, and the
            // HashSet constructor silently collapses duplicates across sources.
            var sourcePart=parts.First(part=>part!=null&&part.condition!=null);
            string originalInstance=sourcePart.instanceId;
            MutateAccepted(check,profile,"unconditional null part instance ID",()=>sourcePart.instanceId=null,
                ()=>sourcePart.instanceId=originalInstance);
            var originalElements=sourcePart.condition.elements;
            MutateAccepted(check,profile,"null element array means no descendant IDs",()=>sourcePart.condition.elements=null,
                ()=>sourcePart.condition.elements=originalElements);
            if(originalElements!=null&&originalElements.Length>0&&originalElements[0]!=null)
            {
                var member=originalElements[0];string memberId=member.id;
                MutateAccepted(check,profile,"empty member ID is filtered by ItemIds",()=>member.id=string.Empty,
                    ()=>member.id=memberId);
                MutateAccepted(check,profile,"null member ID is filtered by ItemIds",()=>member.id=null,
                    ()=>member.id=memberId);
            }
            var memberlessCopy=sourcePart.Copy();memberlessCopy.instanceId="parts-market-check/memberless";
            memberlessCopy.condition.elements=new EngineElement[]{null};parts.Add(memberlessCopy);
            try{CompareExpected(check,profile,null,"null member object is filtered by ItemIds");}
            finally{parts.Remove(memberlessCopy);}

            var duplicateEngine=new EngineAssemblyInstance{instanceId=sourcePart.instanceId};engines.Add(duplicateEngine);
            try{CompareExpected(check,profile,null,"duplicate ID in initial HashSet sources is accepted until used against market engines");}
            finally{engines.Remove(duplicateEngine);}

            // LINQ validates every source argument while constructing the chain,
            // before HashSet enumeration reaches earlier malformed rows.
            Mutate(check,profile,"parts null source precedes source enumeration",()=>profile.parts=null,
                ()=>profile.parts=parts,null);
            profile.parts=parts;
            var markerPart=parts.Count;parts.Add(null);
            try
            {
                Mutate(check,profile,"later null engine source wins over earlier null part row",()=>profile.engines=null,
                    ()=>profile.engines=engines,null);
                Mutate(check,profile,"later null loose-unit source wins over earlier malformed rows",()=>profile.looseEngineElements=null,
                    ()=>profile.looseEngineElements=loose,null);
                Mutate(check,profile,"later null dealer source wins over earlier malformed rows",()=>profile.inventory.dealer=null,
                    ()=>profile.inventory.dealer=dealer,null);
                Mutate(check,profile,"later null listing source wins over earlier malformed rows",()=>profile.vehicleStock.listings=null,
                    ()=>profile.vehicleStock.listings=listings,null);
            }
            finally{parts.RemoveAt(markerPart);}

            // Each outer null enumerable is compared directly so exception type,
            // message and parameter name stay identical to LINQ's source checks.
            Mutate(check,profile,"null p.parts source",()=>profile.parts=null,()=>profile.parts=parts,null);
            Mutate(check,profile,"null p.engines source",()=>profile.engines=null,()=>profile.engines=engines,null);
            Mutate(check,profile,"null p.looseEngineElements source",()=>profile.looseEngineElements=null,
                ()=>profile.looseEngineElements=loose,null);
            Mutate(check,profile,"null dealer source",()=>profile.inventory.dealer=null,()=>profile.inventory.dealer=dealer,null);
            Mutate(check,profile,"null vehicle listing source",()=>profile.vehicleStock.listings=null,
                ()=>profile.vehicleStock.listings=listings,null);

            var originalMarket=profile.inventory.market;
            Mutate(check,profile,"null market retains earlier epoch error",()=>profile.inventory.market=null,
                ()=>profile.inventory.market=originalMarket,"Invalid parts market epoch.");
            var newStockRows=profile.inventory.market.parts;
            if(newStockRows.Count>0)
            {
                var stockRow=newStockRows[0];var originalDefinitions=stockRow.definitions;
                Mutate(check,profile,"null new-stock definition list preserves stock error",()=>stockRow.definitions=null,
                    ()=>stockRow.definitions=originalDefinitions,"Invalid new-parts stock.");
                string duplicateDefinition=stockRow.definitions.First();
                var laterRow=newStockRows.Skip(1).FirstOrDefault(row=>row!=null&&row.definitions!=null&&
                    !row.definitions.Contains(duplicateDefinition));
                if(laterRow!=null)
                {
                    laterRow.definitions.Add(duplicateDefinition);
                    try{CompareExpected(check,profile,"Invalid new-parts stock.","duplicate new-stock definition ID preserves first stock error");}
                    finally{laterRow.definitions.RemoveAt(laterRow.definitions.Count-1);}
                }
                var savedFirstStock=newStockRows[0];newStockRows[0]=null;
                try{CompareExpected(check,profile,"Invalid new-parts stock.","null new-stock row preserves market error");}
                finally{newStockRows[0]=savedFirstStock;}
            }
            var originalStock=profile.vehicleStock;
            Mutate(check,profile,"null vehicle stock retains legacy epoch exception order",()=>profile.vehicleStock=null,
                ()=>profile.vehicleStock=originalStock,null);
            var originalInventory=profile.inventory;
            Mutate(check,profile,"null inventory retains original dereference behavior",()=>profile.inventory=null,
                ()=>profile.inventory=originalInventory,null);

            // Malformed rows must fail only when their source sequence is reached.
            parts.Add(null);try{Compare(check,profile,"null owned part row");}finally{parts.RemoveAt(parts.Count-1);}
            engines.Add(null);try{Compare(check,profile,"null stored engine row");}finally{engines.RemoveAt(engines.Count-1);}
            loose.Add(null);try{Compare(check,profile,"null loose-unit row");}finally{loose.RemoveAt(loose.Count-1);}
            dealer.Add(null);try{Compare(check,profile,"null dealer row");}finally{dealer.RemoveAt(dealer.Count-1);}
            var firstPart=parts[0];var oldCondition=firstPart.condition;
            Mutate(check,profile,"null owned part condition during ItemIds",()=>firstPart.condition=null,
                ()=>firstPart.condition=oldCondition,null);
            var oldUnit=loose.FirstOrDefault(unit=>unit!=null);
            if(oldUnit!=null)
            {
                var oldElement=oldUnit.element;
                Mutate(check,profile,"null loose unit element",()=>oldUnit.element=null,()=>oldUnit.element=oldElement,null);
            }
            var firstListing=listings[0];var firstListingPart=firstListing.parts[0];
            var originalListingParts=firstListing.parts;
            Mutate(check,profile,"null vehicle listing part list preserves direct market source exception",()=>firstListing.parts=null,
                ()=>firstListing.parts=originalListingParts,null);
            Mutate(check,profile,"null listing part row preserves direct market ID traversal exception",()=>firstListing.parts[0]=null,
                ()=>firstListing.parts[0]=firstListingPart,null);
            listings.Add(null);
            try{Compare(check,profile,"null vehicle listing row preserves direct market ID traversal exception");}
            finally{listings.RemoveAt(listings.Count-1);}
            var originalPending=profile.firstPurchasePending;
            profile.firstPurchasePending=true;
            try{CompareExpected(check,profile,null,"first-purchase flag alone does not add vehicle-listing validation to this leaf");}
            finally{profile.firstPurchasePending=originalPending;}

            // Empty source collections have no synthetic sentinel. Their behavior
            // is compared against the frozen routine on the same full market.
            var savedParts=parts.ToArray();
            try{parts.Clear();Compare(check,profile,"empty owned parts collection");}
            finally{parts.Clear();parts.AddRange(savedParts);}
            var savedEngines=engines.ToArray();
            try{engines.Clear();Compare(check,profile,"empty stored engine collection");}
            finally{engines.Clear();engines.AddRange(savedEngines);}
            var savedLoose=loose.ToArray();
            try{loose.Clear();Compare(check,profile,"empty loose unit collection");}
            finally{loose.Clear();loose.AddRange(savedLoose);}
            var savedDealer=dealer.ToArray();
            try{dealer.Clear();Compare(check,profile,"empty dealer collection");}
            finally{dealer.Clear();dealer.AddRange(savedDealer);}
            var savedListings=listings.ToArray();
            try{listings.Clear();profile.firstPurchasePending=false;Compare(check,profile,"empty vehicle listing collection");}
            finally{listings.Clear();listings.AddRange(savedListings);}
            var savedNewStock=profile.inventory.market.parts.ToArray();
            try{profile.inventory.market.parts.Clear();Compare(check,profile,"empty new-parts stock collection");}
            finally{profile.inventory.market.parts.Clear();profile.inventory.market.parts.AddRange(savedNewStock);}
            var savedEngineStock=profile.inventory.market.engines.ToArray();
            try{profile.inventory.market.engines.Clear();Compare(check,profile,"empty market engine-offer collection");}
            finally{profile.inventory.market.engines.Clear();profile.inventory.market.engines.AddRange(savedEngineStock);}

            // Cross-store collision probes use a real existing parts-market offer.
            // The initial ID set itself permits duplicates; adding a colliding ID
            // to a market engine offer must preserve its exact first error.
            var market=profile.inventory.market;
            if(market.engines.Count==0)
            {
                uint random=0x3914a7u;string model=listings[0].model;
                market.engines.Add(PartsMarket.CreateEngineOffer(profile,model,false,"parts-market-check/offer",ref random));
            }
            var marketOffer=market.engines[0];string offerId=marketOffer.id;
            string basePartId=parts[0].instanceId;
            Mutate(check,profile,"owned part ID collides with market engine offer ID",()=>marketOffer.id=basePartId,
                ()=>marketOffer.id=offerId,"Invalid engine stock.");
            string baseMemberId=parts.SelectMany(part=>part.ItemIds).First(id=>!string.IsNullOrEmpty(id));
            Mutate(check,profile,"owned descendant ID collides with market engine offer ID",()=>marketOffer.id=baseMemberId,
                ()=>marketOffer.id=offerId,"Invalid engine stock.");
            string assemblyId=marketOffer.engine.instanceId;
            Mutate(check,profile,"owned ID collides with offered engine assembly ID",()=>marketOffer.engine.instanceId=basePartId,
                ()=>marketOffer.engine.instanceId=assemblyId,"Invalid engine stock.");

            var storedEngine=new EngineAssemblyInstance{instanceId="parts-market-check/stored-engine"};
            engines.Add(storedEngine);
            Mutate(check,profile,"stored engine ID collides with market engine offer ID",()=>marketOffer.id=storedEngine.instanceId,
                ()=>marketOffer.id=offerId,"Invalid engine stock.");
            engines.Remove(storedEngine);

            var donor=parts.First(part=>part!=null&&part.condition?.elements?.FirstOrDefault(element=>element!=null)!=null);
            var donorElement=donor.condition.elements.First(element=>element!=null).Copy();
            string donorDefinition=donor.definitionId;
            var looseUnit=new LooseEngineElement{definitionId=donorDefinition,element=donorElement};
            string oldUnitId=looseUnit.element.id;looseUnit.element.id="parts-market-check/loose-unit";
            loose.Add(looseUnit);
            Mutate(check,profile,"loose unit ID collides with market engine offer ID",()=>marketOffer.id=looseUnit.element.id,
                ()=>marketOffer.id=offerId,"Invalid engine stock.");
            Mutate(check,profile,"loose unit descendant ID collides with market engine offer ID",()=>marketOffer.id=looseUnit.element.id,
                ()=>marketOffer.id=offerId,"Invalid engine stock.");
            loose.Remove(looseUnit);looseUnit.element.id=oldUnitId;

            var dealerPart=donor.Copy();dealerPart.instanceId="parts-market-check/dealer-part";
            foreach(var element in dealerPart.condition.elements??Array.Empty<EngineElement>())if(element!=null)element.id="parts-market-check/dealer-part/"+element.cylinder.ToString(CultureInfo.InvariantCulture);
            var dealerStockPart=new DealerStock{part=dealerPart};dealer.Add(dealerStockPart);
            Mutate(check,profile,"dealer part ID collides with market engine offer ID",()=>marketOffer.id=dealerPart.instanceId,
                ()=>marketOffer.id=offerId,"Invalid engine stock.");
            dealer.Remove(dealerStockPart);

            var dealerUnitElement=donor.condition.elements.FirstOrDefault(element=>element!=null).Copy();
            dealerUnitElement.id="parts-market-check/dealer-unit";
            var dealerUnit=new DealerStock{isUnit=true,unit=new LooseEngineElement{definitionId=donorDefinition,element=dealerUnitElement}};
            dealer.Add(dealerUnit);
            Mutate(check,profile,"dealer unit ID collides with market engine offer ID",()=>marketOffer.id=dealerUnitElement.id,
                ()=>marketOffer.id=offerId,"Invalid engine stock.");
            dealer.Remove(dealerUnit);

            var listing= listings.First(offer=>offer!=null&&offer.parts!=null&&offer.parts.Count>0);
            var listingPart=listing.parts[0];string listingPartId=listingPart.instanceId;
            Mutate(check,profile,"vehicle listing part ID collides with market engine offer ID",()=>marketOffer.id=listingPartId,
                ()=>marketOffer.id=offerId,"Invalid engine stock.");
            var listingMember=listingPart.condition.elements?.FirstOrDefault(element=>element!=null&&
                !string.IsNullOrEmpty(element.id));
            if(listingMember!=null)
            {
                string listingMemberId=listingMember.id;
                Mutate(check,profile,"vehicle listing descendant ID collides with market engine offer ID",()=>marketOffer.id=listingMemberId,
                    ()=>marketOffer.id=offerId,"Invalid engine stock.");
            }

            string savedOfferPart=marketOffer.parts[0].instanceId;
            Mutate(check,profile,"offered engine component ID collides with owned part",()=>marketOffer.parts[0].instanceId=basePartId,
                ()=>marketOffer.parts[0].instanceId=savedOfferPart,"Invalid offered engine component.");
            string savedOfferMember=marketOffer.parts[0].condition.elements.First(element=>element!=null).id;
            Mutate(check,profile,"offered engine descendant ID collides with owned part",()=>marketOffer.parts[0].condition.elements.First(element=>element!=null).id=basePartId,
                ()=>marketOffer.parts[0].condition.elements.First(element=>element!=null).id=savedOfferMember,"Invalid offered engine descendant.");

            var emptyParts=marketOffer.parts;marketOffer.parts=new List<PartInstance>();
            try{CompareExpected(check,profile,"Invalid engine stock.","empty offered engine components preserve market error");}
            finally{marketOffer.parts=emptyParts;}
            var originalEngineOffer=market.engines[0];market.engines[0]=null;
            try{CompareExpected(check,profile,"Invalid engine stock.","null market engine row preserves market error");}
            finally{market.engines[0]=originalEngineOffer;}

            CompareExpected(check,profile,null,"all ID source probes restore a valid full roster");
        }

        static string BuildTiming(PlayerProfile profile,int repeats)
        {
            string frozen=MedianMax(()=>{if(LegacyValidate(profile)!=null)throw new InvalidOperationException("Frozen oracle rejected timing profile.");},repeats);
            string current=MedianMax(()=>{if(PartsMarket.Validate(profile)!=null)throw new InvalidOperationException("Current validator rejected timing profile.");},repeats);
            return "full_validate_old_median_max "+frozen+"; full_validate_current_median_max "+current+
                "; allocations=not-measured(no reliable Unity-thread allocation counter used)";
        }

        static PlayerProfile CreateNativeLikeProfile(MechanicalCatalog catalog)
        {
            var profile=PlayerProfile.NewGame(638000000000000000L);profile.cash=100000000;profile.reputation=10000;
            VehicleListings.Initialize(profile,catalog,16102026);MechanicsOwnership.Ensure(profile,catalog);
            var starter=profile.vehicleStock.listings[0];VehicleCollection.Buy(profile,catalog,starter);
            var second=profile.vehicleStock.listings.FirstOrDefault(offer=>!offer.used&&offer.model!=starter.model);
            if(second==null)throw new InvalidOperationException("Post-purchase market has no second distinct new vehicle offer.");
            VehicleCollection.Buy(profile,catalog,second);MechanicsOwnership.Ensure(profile,catalog);
            if(profile.firstPurchasePending||profile.vehicleStock.listings.Count!=59||profile.cars.Count!=2)
                throw new InvalidOperationException("Expected the actual post-purchase 59-listing market and two owned cars.");
            var template=profile.parts.FirstOrDefault(part=>part.location==PartLocation.Inventory)??profile.parts[0];
            while(profile.parts.Count<1081)
            {
                var copy=template.Copy();copy.instanceId="parts-market-check/filler/"+profile.parts.Count.ToString("D4",CultureInfo.InvariantCulture);
                copy.location=PartLocation.Inventory;copy.ownerId="";copy.slot="";copy.storageId=SharedInventory.Garage;
                foreach(var element in copy.condition.elements??Array.Empty<EngineElement>())if(element!=null)
                    element.id=copy.instanceId+"/member/"+element.cylinder.ToString(CultureInfo.InvariantCulture);
                profile.parts.Add(copy);
            }
            if(profile.parts.Count!=1081)throw new InvalidOperationException("Expected exactly 1,081 owned parts for the native-like fixture.");
            return profile;
        }

        static void EnsureActualMarketEngineOffer(PlayerProfile profile)
        {
            if(profile.inventory.market.engines.Count>0)return;
            uint random=0x3914a7u;string model=profile.vehicleStock.listings[0].model;
            profile.inventory.market.engines.Add(PartsMarket.CreateEngineOffer(profile,model,false,"parts-market-check/generated-offer",ref random));
        }

        public static void Run(Action<bool,string> check,string fixtureDirectory)
        {
            var authored=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            if(authored==null||authored.Length==0)throw new InvalidOperationException("PartsMarket validation checks require authored vehicle definitions.");
            var catalog=new MechanicalCatalog(authored.Select(vehicle=>vehicle.spec));
            var profile=CreateNativeLikeProfile(catalog);EnsureActualMarketEngineOffer(profile);
            if(profile.vehicleStock.listings.Count!=59||profile.parts.Count!=1081||profile.cars.Count!=2)
                throw new InvalidOperationException("PartsMarket parity fixture is not the full post-purchase native-like profile.");
            SourceParity(check,profile);
            const int repeats=10;string timing=BuildTiming(profile,repeats);
            timing="native_seed=16102026 cars="+profile.cars.Count.ToString(CultureInfo.InvariantCulture)+
                " profile_parts="+profile.parts.Count.ToString(CultureInfo.InvariantCulture)+
                " listings="+profile.vehicleStock.listings.Count.ToString(CultureInfo.InvariantCulture)+
                " listing_parts="+profile.vehicleStock.listings.Sum(offer=>offer.parts.Count).ToString(CultureInfo.InvariantCulture)+
                " part_market_engine_offers="+profile.inventory.market.engines.Count.ToString(CultureInfo.InvariantCulture)+
                " repeats="+repeats.ToString(CultureInfo.InvariantCulture)+Environment.NewLine+timing;
            if(!string.IsNullOrEmpty(fixtureDirectory))
            {
                Directory.CreateDirectory(fixtureDirectory);
                File.WriteAllText(Path.Combine(fixtureDirectory,"parts-market-validation-timing.txt"),timing+Environment.NewLine,new UTF8Encoding(false));
            }
            check(true,timing);
        }
    }
#endif
}
