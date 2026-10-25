using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Independent equivalence checks for the Shop supply counter and ready-group ledger projection.</summary>
    public static class ShopSupplyProjectionChecks
    {
        static void Check(Action<bool,string> check,bool condition,string label)
        {
            check(condition,label);
            if(!condition)throw new InvalidOperationException(label);
        }

        static string[] Models(MechanicalCatalog catalog)
            =>catalog.parts.Where(definition=>definition.category==PartCategory.CylinderBlock)
                .Select(definition=>definition.id.Split('/')[0]).Distinct().OrderBy(model=>model,StringComparer.Ordinal).ToArray();

        // Kept local as the independent pre-index oracle: the selected spec wins only when it fits;
        // otherwise the first ordinal model that fits supplies the original Count calculation.
        static Func<PartDefinition,int> OriginalCounter(PlayerProfile profile,OwnedCar car)
        {
            var selected=car==null?null:profile.catalog.Spec(car.vehicleId);
            var specs=Models(profile.catalog).Select(profile.catalog.Spec).ToArray();
            return definition=>{
                if(definition.category==PartCategory.Tires||definition.category==PartCategory.Wheels||definition.category==PartCategory.Brakes)return 4;
                if(!EngineElements.Supported(definition.category))return 1;
                var spec=selected!=null&&profile.catalog.Fits(selected,definition)?selected:
                    specs.FirstOrDefault(candidate=>profile.catalog.Fits(candidate,definition));
                return spec==null?1:PartsMarket.Count(spec,definition);
            };
        }

        static IReadOnlyList<MarketCatalogLayout.ReadyProductGroup> WarmReadyGroups(
            MechanicalCatalog catalog,Action<bool,string> check,string fixtureLabel,
            out IReadOnlyDictionary<string,MarketCatalogLayout.ReadyProductGroup> groupById)
        {
            groupById=null;
            Check(check,!MarketCatalogLayout.TryReadyShopProductGroups(catalog,out var cold,out var coldById)&&cold==null&&coldById==null,
                "cold Shop group lookup returns no partial groups: "+fixtureLabel);
            bool sawReadyMarker=false,sawAfterReadyYield=false;
            using(var steps=MarketCatalogLayout.WarmSteps(catalog).GetEnumerator())
            {
                while(steps.MoveNext())
                {
                    string marker=steps.Current as string;
                    bool ready=MarketCatalogLayout.TryReadyShopProductGroups(catalog,out var groups,out var byId);
                    if(string.Equals(marker,"parts/shop ready",StringComparison.Ordinal))
                    {
                        Check(check,ready&&groups!=null&&byId!=null,"ready groups and immutable alias index publish atomically at the explicit warmup marker");
                        sawReadyMarker=true;
                    }
                    else if(!sawReadyMarker)
                        Check(check,!ready&&groups==null,"ready groups remain unavailable before complete Shop grouping: "+(marker??"worker step"));
                    else
                    {
                        sawAfterReadyYield=true;
                        Check(check,ready&&groups!=null,"ready Shop groups remain valid through later fit/layout warmup yields");
                    }
                }
            }
            Check(check,sawReadyMarker&&sawAfterReadyYield,
                "fresh catalog exposes the Shop-ready boundary before subsequent fit/layout warmup work");
            Check(check,MarketCatalogLayout.TryReadyShopProductGroups(catalog,out var finalGroups,out groupById)&&finalGroups!=null&&groupById!=null,
                "completed warmup retains a complete ready product-group snapshot");
            return finalGroups;
        }

        static void CompareQuote(Action<bool,string> check,SupplyQuote actual,SupplyQuote expected,string label)
        {
            Check(check,actual!=null&&expected!=null&&actual.cost==expected.cost&&actual.fee==expected.fee&&
                actual.localUnits==expected.localUnits&&actual.expressUnits==expected.expressUnits,
                label+" (actual "+QuoteText(actual)+", expected "+QuoteText(expected)+")");
        }

        static string QuoteText(SupplyQuote quote)
            =>quote==null?"null":"cost="+quote.cost+", fee="+quote.fee+", local="+quote.localUnits+", express="+quote.expressUnits;

        static SupplyQuote ExpectedQuote(int available,int percent,int units,int basePrice,int quantity)
        {
            var result=new SupplyQuote();
            for(int item=0;item<quantity;item++)
            {
                int local=Math.Min(units,available),express=units-local;
                int fee=express==0?0:checked((int)Math.Ceiling(basePrice*(express/(double)units)*percent/100.0));
                result.localUnits=checked(result.localUnits+local);result.expressUnits=checked(result.expressUnits+express);
                result.fee=checked(result.fee+fee);result.cost=checked(result.cost+basePrice+fee);available-=local;
            }
            return result;
        }

        static PartsMarket.Ledger ReadyLedger(PlayerProfile profile,bool includeReserved,
            IReadOnlyList<MarketCatalogLayout.ReadyProductGroup> groups,
            IReadOnlyDictionary<string,MarketCatalogLayout.ReadyProductGroup> groupById,
            Action<bool,string> check,string label)
        {
            var ledger=new PartsMarket.Ledger(profile,includeReserved);
            Check(check,ledger.BindReadyProductGroups(groups,groupById),"validated Shop groups bind to an operation-local stock projection: "+label);
            return ledger;
        }

        static void CompareProductQuote(PlayerProfile profile,IReadOnlyList<MarketCatalogLayout.ReadyProductGroup> groups,
            IReadOnlyDictionary<string,MarketCatalogLayout.ReadyProductGroup> groupById,
            PartDefinition definition,int units,int basePrice,int quantity,bool includeReserved,
            int expectedAvailable,SupplyQuote expected,string label,Action<bool,string> check)
        {
            var fallback=new PartsMarket.Ledger(profile,includeReserved);
            var ready=ReadyLedger(profile,includeReserved,groups,groupById,check,label);
            int fallbackAvailable=fallback.Available(definition),readyAvailable=ready.Available(definition);
            Check(check,fallbackAvailable==readyAvailable&&readyAvailable==expectedAvailable,
                label+" preserves direct-ID/first-equivalent stock selection and available quantity (legacy="+fallbackAvailable+", ready="+readyAvailable+")");
            var fallbackQuote=fallback.Quote(definition,units,basePrice,quantity);
            var readyQuote=ready.Quote(definition,units,basePrice,quantity);
            CompareQuote(check,fallbackQuote,readyQuote,label+" exact quote parity");
            CompareQuote(check,readyQuote,expected,label+" exact quantity, local/express and fee oracle");
        }

        public static void Run(Action<bool,string> check,string fixtureDirectory)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(fixtureDirectory))throw new ArgumentException("An isolated fixture directory is required.",nameof(fixtureDirectory));
            string fixtureLabel=Path.GetFileName(Path.GetFullPath(fixtureDirectory));
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            Check(check,vehicles!=null&&vehicles.Length>0,"authored vehicle definitions load for supply projection fixture: "+fixtureLabel);
            var catalog=new MechanicalCatalog(vehicles.Select(vehicle=>vehicle.spec));
            var profile=PlayerProfile.New(25102026);
            MechanicsOwnership.Ensure(profile,catalog);profile.cash=1000000;profile.reputation=1000;
            var selected=vehicles.First(vehicle=>vehicle.spec.id=="starter-hatch");
            VehicleCollection.Buy(profile,catalog,new VehicleOffer{model=selected.spec.id,price=1,reputation=0,
                stock=selected.CreateStockTune(),paint="#FFFFFF"});

            var source=catalog.parts.First(definition=>definition.category!=PartCategory.CylinderBlock&&definition.price>0);
            var aliasA=source.Copy();aliasA.id="s06d-supply-alias-a";
            var aliasB=source.Copy();aliasB.id="s06d-supply-alias-b";
            var aliasC=source.Copy();aliasC.id="s06d-supply-alias-c";
            var unknownAlias=source.Copy();unknownAlias.id="s06d-supply-unindexed-alias";
            var absent=catalog.parts.First(definition=>definition.category!=PartCategory.CylinderBlock&&definition.price>0&&
                !string.Equals(PartsMarket.ProductKey(definition),PartsMarket.ProductKey(source),StringComparison.Ordinal));
            catalog.parts.Add(aliasA);catalog.parts.Add(aliasB);catalog.parts.Add(aliasC);catalog.RebuildIndexes();

            var selectedCounter=PartsMarket.Counter(profile,profile.SelectedCar);
            var selectedOracle=OriginalCounter(profile,profile.SelectedCar);
            var indexedSelected=PartsMarket.CounterIndexed(profile,profile.SelectedCar);
            int selectedCount=0;
            foreach(var definition in catalog.parts)
            {
                int expected=selectedOracle(definition),old=selectedCounter(definition),indexed=indexedSelected(definition);
                Check(check,old==expected,"public legacy Counter matches independent selected-car oracle: "+definition.id);
                Check(check,indexed==expected,"indexed Counter preserves selected-car supply quantity: "+definition.id+" expected="+expected+" actual="+indexed);
                selectedCount++;
            }
            Check(check,selectedCount==catalog.parts.Count,"selected-car Counter oracle covered every authored catalog product");

            var noCarCounter=PartsMarket.Counter(profile,null);var noCarOracle=OriginalCounter(profile,null);
            var indexedNoCar=PartsMarket.CounterIndexed(profile,null);int noCarCount=0;
            foreach(var definition in catalog.parts)
            {
                int expected=noCarOracle(definition),old=noCarCounter(definition),indexed=indexedNoCar(definition);
                Check(check,old==expected,"public legacy Counter matches independent no-selected-car oracle: "+definition.id);
                Check(check,indexed==expected,"indexed Counter preserves first-fitting-model fallback quantity: "+definition.id+" expected="+expected+" actual="+indexed);
                noCarCount++;
            }
            Check(check,noCarCount==catalog.parts.Count,"no-selected-car Counter oracle covered every authored catalog product");

            var missingInterface=catalog.parts.First(definition=>definition.category==PartCategory.Pistons).Copy();
            foreach(var fitment in new string[]{null,"s06d-missing-physical-interface"})
            {
                missingInterface.fitment=fitment;
                int selectedExpected=selectedOracle(missingInterface),noCarExpected=noCarOracle(missingInterface);
                Check(check,selectedCounter(missingInterface)==selectedExpected&&indexedSelected(missingInterface)==selectedExpected,
                    "missing/null interface preserves selected-car exact fallback: "+(fitment??"null"));
                Check(check,noCarCounter(missingInterface)==noCarExpected&&indexedNoCar(missingInterface)==noCarExpected,
                    "missing/null interface preserves no-selected-car exact fallback: "+(fitment??"null"));
            }

            var models=Models(catalog).Select(catalog.Spec).ToArray();var selectedSpec=catalog.Spec(profile.SelectedCar.vehicleId);
            var incompatible=catalog.parts.FirstOrDefault(definition=>EngineElements.Supported(definition.category)&&
                !catalog.Fits(selectedSpec,definition)&&models.Any(spec=>catalog.Fits(spec,definition)));
            Check(check,incompatible!=null,"fixture contains a supported product physically incompatible with the selected car but fitting another model");
            Check(check,incompatible!=null&&selectedOracle(incompatible)==indexedSelected(incompatible),
                "indexed Counter filters by interface only as a prefilter and keeps authoritative exact fit/Count behavior");
            var unsupported=catalog.parts.FirstOrDefault(definition=>!EngineElements.Supported(definition.category)&&
                definition.category!=PartCategory.Tires&&definition.category!=PartCategory.Wheels&&definition.category!=PartCategory.Brakes);
            if(unsupported!=null)Check(check,indexedSelected(unsupported)==1&&indexedNoCar(unsupported)==1,
                "unsupported product categories retain the legacy one-unit supply quantity");

            var groups=WarmReadyGroups(catalog,check,fixtureLabel,out var groupById);
            Check(check,groups.SelectMany(group=>group.DefinitionIds).Count()==catalog.parts.Count,
                "ready group aliases cover every catalog definition exactly once before stock binding");

            // Row 1 is the first equivalent-product fallback. The source itself is a direct ID in row 2,
            // so it must resolve there even though both rows describe one canonical product.
            var firstRow=new NewPartStock{definitions=new List<string>{aliasA.id},units=2,initialUnits=2,deliveryPercent=5};
            var directRow=new NewPartStock{definitions=new List<string>{source.id,aliasB.id},units=4,initialUnits=4,deliveryPercent=10};
            profile.inventory.market=new PartsStockState{version=2,seed=profile.vehicleStock.seed,generation=profile.vehicleStock.generation,
                guaranteeAtRefresh=profile.driving.guaranteedFittingParts,parts=new List<NewPartStock>{firstRow,directRow}};
            profile.inventory.market.index=null;
            var preboundProbe=new PartsMarket.Ledger(profile,false);
            Check(check,preboundProbe.BindReadyProductGroups(groups,groupById),"ready ledger prebind accepts the validated immutable group snapshot");
            Check(check,profile.inventory.market.index==null,"operation-local prebinding leaves the profile market index untouched");

            CompareProductQuote(profile,groups,groupById,source,3,125,2,false,4,ExpectedQuote(4,10,3,125,2),
                "direct source ID wins over an earlier equivalent alias row",check);
            CompareProductQuote(profile,groups,groupById,aliasB,3,125,2,false,4,ExpectedQuote(4,10,3,125,2),
                "direct alias ID keeps its own later stock-row mapping",check);
            CompareProductQuote(profile,groups,groupById,aliasA,3,125,2,false,2,ExpectedQuote(2,5,3,125,2),
                "direct first-row alias preserves its row units and delivery fee",check);
            CompareProductQuote(profile,groups,groupById,aliasC,3,125,2,false,2,ExpectedQuote(2,5,3,125,2),
                "unlisted equivalent alias falls back to the first matching market row",check);
            CompareProductQuote(profile,groups,groupById,unknownAlias,3,125,2,false,2,ExpectedQuote(2,5,3,125,2),
                "definition absent from the ready alias index retains the legacy product-key fallback",check);
            CompareProductQuote(profile,groups,groupById,absent,3,125,2,false,0,ExpectedQuote(0,10,3,125,2),
                "missing product stock remains a null miss with ordinary express pricing",check);

            var fallbackLedger=new PartsMarket.Ledger(profile,false);
            var readyLedger=ReadyLedger(profile,false,groups,groupById,check,"remaining-quantity sequence");
            var fallbackTake=fallbackLedger.Take(source,3,125,1,false);
            var readyTake=readyLedger.Take(source,3,125,1,false);
            CompareQuote(check,readyTake,fallbackTake,"Take parity with an isolated remaining-quantity ledger");
            CompareQuote(check,readyTake,ExpectedQuote(4,10,3,125,1),"Take exact available-versus-express split");
            Check(check,fallbackLedger.Available(source)==readyLedger.Available(source)&&readyLedger.Available(source)==1&&directRow.units==4,
                "non-consuming Take updates each ledger's remaining units without mutating persisted stock");
            CompareQuote(check,readyLedger.Quote(source,3,125),fallbackLedger.Quote(source,3,125),
                "subsequent quote observes the decremented live ledger remainder");
            CompareQuote(check,readyLedger.Quote(source,3,125),ExpectedQuote(1,10,3,125,1),
                "subsequent quote exact fee uses remaining local stock");

            // Existing ledger instances continue to read row price/fee data live; a fresh one snapshots current units.
            var liveFallback=new PartsMarket.Ledger(profile,false);
            var liveReady=ReadyLedger(profile,false,groups,groupById,check,"live row updates");
            directRow.deliveryPercent=7;
            CompareQuote(check,liveReady.Quote(source,3,150,2),liveFallback.Quote(source,3,150,2),
                "existing bound and fallback ledgers both use the live base price and delivery percent");
            CompareQuote(check,liveReady.Quote(source,3,150,2),ExpectedQuote(4,7,3,150,2),
                "live price and delivery percent retain exact quantity and fee arithmetic");
            directRow.units=6;
            var freshFallback=new PartsMarket.Ledger(profile,false);
            var freshReady=ReadyLedger(profile,false,groups,groupById,check,"refreshed stock amount");
            Check(check,freshReady.Available(source)==freshFallback.Available(source)&&freshReady.Available(source)==6,
                "a newly constructed ledger sees the current market row quantity");

            // Active current-epoch cart holds subtract from the non-reserved view and are restored only
            // for the exact cart owner view; the hold state itself is never consumed by Quote.
            directRow.units=4;directRow.deliveryPercent=10;
            int heldUnits=1;int generation=profile.inventory.market.generation;
            profile.inventory.cart=new CartReservationState{version=1,createdRun=profile.vehicleStock.completedRuns,
                expiresRun=profile.vehicleStock.completedRuns+CartReservations.LifetimeRaces,generation=generation,
                carId=profile.SelectedCar.instanceId,
                lines=new List<PartCartLine>{new PartCartLine{action=CartAction.NewSet,definitionId=source.id}},
                supply=new List<ReservedPartSupply>{new ReservedPartSupply{definitionId=source.id,units=heldUnits,generation=generation}}};
            CompareProductQuote(profile,groups,groupById,source,3,125,2,false,3,ExpectedQuote(3,10,3,125,2),
                "active cart hold is excluded from the ordinary ledger view",check);
            CompareProductQuote(profile,groups,groupById,source,3,125,2,true,4,ExpectedQuote(4,10,3,125,2),
                "matching held stock is restored in the reserved cart-owner ledger view",check);
            Check(check,profile.inventory.cart.supply[0].units==heldUnits,
                "read-only quote and prebinding preserve the cart reservation units");
            Check(check,CartReservations.Active(profile)&&CartReservations.SameCart(profile,profile.SelectedCar.instanceId,profile.inventory.cart.lines),
                "hold fixture is an active reservation for the exact selected-car cart");
        }
    }
#endif
}
