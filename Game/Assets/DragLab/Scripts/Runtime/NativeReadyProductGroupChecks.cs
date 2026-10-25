using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Independent S06d2a parity and invalidation checks for ready Shop product groups.</summary>
    public static class NativeReadyProductGroupChecks
    {
        static void Check(Action<bool,string> check,bool condition,string label)
        {
            check(condition,label);
            if(!condition)throw new InvalidOperationException(label);
        }

        static string[][] LegacySourceGroups(MechanicalCatalog catalog)
            =>catalog.parts.GroupBy(NativePartBrowser.NewProductKey,StringComparer.Ordinal)
                .Select(group=>group.Select(definition=>definition.id).ToArray()).ToArray();

        static string[][] ReadySourceGroups(IReadOnlyList<MarketCatalogLayout.ReadyProductGroup> groups)
            =>groups.Select(group=>group.DefinitionIds.ToArray()).ToArray();

        static string[] LegacyRepresentatives(PlayerProfile profile,string[][] groups)
        {
            var fitted=new HashSet<string>(MechanicsOwnership.Definitions(profile,profile.SelectedCar).Values,StringComparer.Ordinal);
            string model=profile.SelectedCar.vehicleId+"/";
            return groups.Select(ids=>ids.Select(id=>profile.catalog.Find(id))
                .OrderBy(definition=>fitted.Contains(definition.id)?0:
                    definition.id.StartsWith(model,StringComparison.Ordinal)?1:2)
                .ThenBy(definition=>definition.id,StringComparer.Ordinal)
                .First().id).ToArray();
        }

        static bool SameGroups(string[][] left,string[][] right)
            =>left.Length==right.Length&&Enumerable.Range(0,left.Length).All(index=>left[index].SequenceEqual(right[index],StringComparer.Ordinal));

        static int Warm(MechanicalCatalog catalog,Action<bool,string> check,string context,
            out double maximumMoveMilliseconds,out double totalWarmMilliseconds)
        {
            int yields=0;
            bool shopPublished=false;
            maximumMoveMilliseconds=0;
            long warmStarted=Stopwatch.GetTimestamp();
            using(var steps=MarketCatalogLayout.WarmSteps(catalog).GetEnumerator())
            {
                while(true)
                {
                    long started=Stopwatch.GetTimestamp();
                    bool more=steps.MoveNext();
                    double elapsed=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;
                    maximumMoveMilliseconds=Math.Max(maximumMoveMilliseconds,elapsed);
                    if(!more)break;
                    yields++;
                    if(Equals(steps.Current,"parts/shop ready"))shopPublished=true;
                    bool available=MarketCatalogLayout.TryReadyShopProductGroups(catalog,out var early);
                    Check(check,available==shopPublished&&(available?early!=null:early==null),
                        "ready Shop lookup follows complete group publication independently of market fitting: "+context+" / step "+yields);
                }
            }
            totalWarmMilliseconds=(Stopwatch.GetTimestamp()-warmStarted)*1000.0/Stopwatch.Frequency;
            return yields;
        }

        static NativePartBrowser Browser(PlayerProfile profile,IEnumerable<PartCartLine> lines=null)
            =>new NativePartBrowser(profile,lines??Array.Empty<PartCartLine>(),false,"Shop",
                stock=>InventoryPricing.BuyUsed(stock),(_,unit)=>20);

        static NativePartBrowser BrowserAfterDirectInvalidation(PlayerProfile profile,PartDefinition definition)
        {
            int price=definition.price;
            definition.price=checked(price+1);
            var browser=Browser(profile);
            definition.price=price;
            return browser;
        }

        static bool MutateScalar(FieldInfo field,object target)
        {
            var type=field.FieldType;var old=field.GetValue(target);
            if(type==typeof(string)){field.SetValue(target,(string.IsNullOrEmpty((string)old)?"S06d":(string)old+"-S06d"));return true;}
            if(type==typeof(bool)){field.SetValue(target,!(bool)old);return true;}
            if(type==typeof(int)){field.SetValue(target,checked((int)old+1));return true;}
            if(type==typeof(float)){float value=(float)old;float delta=Math.Max(.125f,Math.Abs(value)*.01f);float changed=value+delta;if(changed==value)changed=value-delta;field.SetValue(target,changed);return true;}
            if(type==typeof(double)){double value=(double)old;double delta=Math.Max(.125d,Math.Abs(value)*.01d);double changed=value+delta;if(changed==value)changed=value-delta;field.SetValue(target,changed);return true;}
            if(type.IsEnum){var values=Enum.GetValues(type);int index=Array.IndexOf(values,old);field.SetValue(target,values.GetValue((index+1)%values.Length));return true;}
            return false;
        }

        static int CheckKeyFields(PartDefinition source,Action<bool,string> check)
        {
            int covered=0;
            Action<object,string,int> visit=null;
            visit=(owner,path,depth)=>{
                if(owner==null||depth>2)return;
                foreach(var field in owner.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public)){
                    if(field.IsNotSerialized||field.IsStatic||field.Name=="id"&&owner is PartDefinition)continue;
                    var value=field.GetValue(owner);string name=path+"."+field.Name;
                    if(value!=null&&!(field.FieldType==typeof(string))&&!field.FieldType.IsValueType){
                        if(field.Name=="awdHardware"||owner is AwdHardwareSpec&&field.Name=="calibration")visit(value,name,depth+1);
                        continue;
                    }
                    if(value==null||!field.FieldType.IsPrimitive&&!field.FieldType.IsEnum&&field.FieldType!=typeof(string))continue;
                    var candidate=source.Copy();
                    object target=candidate;string[] parts=name.Split('.');
                    for(int i=1;i<parts.Length-1;i++)target=target.GetType().GetField(parts[i],BindingFlags.Instance|BindingFlags.Public).GetValue(target);
                    var leaf=target.GetType().GetField(parts[parts.Length-1],BindingFlags.Instance|BindingFlags.Public);
                    string before=MarketCatalogLayout.Key(candidate);object originalValue=leaf.GetValue(target);
                    if(!MutateScalar(leaf,target))continue;
                    Check(check,!Equals(originalValue,leaf.GetValue(target)),"field mutation fixture changes the represented value before key comparison: "+name);
                    string after=MarketCatalogLayout.Key(candidate);
                    Check(check,!string.Equals(before,after,StringComparison.Ordinal),"product key notices direct authored field change: "+name);
                    covered++;
                }
            };
            visit(source,"PartDefinition",0);
            return covered;
        }

        static NativePartEntry NewEntry(NativePartBrowser browser,string definitionId)
            =>browser.Entries.Single(entry=>entry.Source=="New"&&entry.Definition.id==definitionId);

        public static void Run(Action<bool,string> check,string fixtureDirectory)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(fixtureDirectory))throw new ArgumentException("An isolated fixture directory is required.",nameof(fixtureDirectory));
            string fixtureLabel=Path.GetFileName(Path.GetFullPath(fixtureDirectory));
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            Check(check,vehicles!=null&&vehicles.Length>0,"authored vehicle definitions load for ready-group fixture: "+fixtureLabel);
            var catalog=new MechanicalCatalog(vehicles.Select(vehicle=>vehicle.spec));
            var profile=PlayerProfile.New(16102026);
            MechanicsOwnership.Ensure(profile,catalog);profile.cash=1000000;profile.reputation=1000;
            var selected=vehicles.First(vehicle=>vehicle.spec.id=="starter-hatch");
            VehicleCollection.Buy(profile,catalog,new VehicleOffer{model=selected.spec.id,price=1,reputation=0,
                stock=selected.CreateStockTune(),paint="#FFFFFF"});
            var installed=MechanicsOwnership.Definitions(profile,profile.SelectedCar);
            var fittedPart=profile.componentsPart(PartCategory.Induction);
            if(fittedPart==null)throw new InvalidOperationException("Ready-group fixture requires a fitted induction part.");
            var fittedDefinition=catalog.Find(fittedPart.definitionId);

            // Three groups distinguish the live browser's exact representative priority.
            var fittedAlias=fittedDefinition.Copy();fittedAlias.id="!s06d-fitting-alias";catalog.parts.Add(fittedAlias);
            var ownedKeys=new HashSet<string>(profile.parts.Select(part=>catalog.Find(part.definitionId)).Where(definition=>definition!=null)
                .Select(NativePartBrowser.NewProductKey),StringComparer.Ordinal);
            var outsideSources=catalog.parts.Where(definition=>!installed.ContainsValue(definition.id)&&
                !definition.id.StartsWith(profile.SelectedCar.vehicleId+"/",StringComparison.Ordinal)&&definition.price>100&&
                !ownedKeys.Contains(NativePartBrowser.NewProductKey(definition))).ToArray();
            if(outsideSources.Length<2)throw new InvalidOperationException("Ready-group fixture needs two non-owned, non-model-prefix authored products.");
            var modelSource=outsideSources[0];var modelAlias=modelSource.Copy();modelAlias.id=profile.SelectedCar.vehicleId+"/0-s06d-model-alias";
            var modelForeign=modelSource.Copy();modelForeign.id="000-s06d-foreign-alias";
            catalog.parts.Add(modelAlias);catalog.parts.Add(modelForeign);
            var ordinalSource=outsideSources.First(source=>!string.Equals(NativePartBrowser.NewProductKey(source),NativePartBrowser.NewProductKey(modelSource),StringComparison.Ordinal));
            var ordinalAliasA=ordinalSource.Copy();ordinalAliasA.id="!s06d-ordinal-alias";
            var ordinalAliasZ=ordinalSource.Copy();ordinalAliasZ.id="~s06d-ordinal-alias";
            catalog.parts.Add(ordinalAliasA);catalog.parts.Add(ordinalAliasZ);
            catalog.RebuildIndexes();

            Check(check,!MarketCatalogLayout.TryReadyShopProductGroups(catalog,out var coldGroups)&&coldGroups==null,
                "cold or unvalidated catalog returns no ready product groups");
            int warmYields=Warm(catalog,check,"authored catalog",out double maxWarmMoveMs,out double totalWarmMs);
            Check(check,warmYields>0,"catalog warmup publishes groups only after bounded yielded work");
            Check(check,MarketCatalogLayout.TryReadyShopProductGroups(catalog,out var readyGroups)&&readyGroups!=null,
                "fully warmed current catalog returns immutable source-ordered product groups");
            var fallback=LegacySourceGroups(catalog);
            var ready=ReadySourceGroups(readyGroups);
            Check(check,SameGroups(ready,fallback),"ready group count, source order, and every alias ID exactly match the former GroupBy(ProductKey) fallback");
            Check(check,ready.All(group=>group.Length>0)&&ready.SelectMany(group=>group).Count()==catalog.parts.Count,
                "ready groups contain every authored definition exactly once without dropping aliases");

            var expectedRepresentatives=LegacyRepresentatives(profile,fallback);
            var browser=Browser(profile);
            var browserRepresentatives=browser.Entries.Where(entry=>entry.Source=="New").Select(entry=>entry.Definition.id).ToArray();
            Check(check,browserRepresentatives.SequenceEqual(expectedRepresentatives,StringComparer.Ordinal),
                "native Shop cards preserve former fallback group order and live fitted/model-prefix/ordinal representative selection");
            Check(check,expectedRepresentatives.Contains(fittedDefinition.id,StringComparer.Ordinal)&&
                expectedRepresentatives.Contains(modelAlias.id,StringComparer.Ordinal)&&expectedRepresentatives.Contains(ordinalAliasA.id,StringComparer.Ordinal),
                "fixtures exercise fitted, same-model-prefix, and ordinal representative priorities");
            var hardwareDefinition=catalog.parts.FirstOrDefault(definition=>definition.awdHardware!=null);
            Check(check,hardwareDefinition!=null,"fixture has authored AWD hardware and calibration fields for stable-key coverage");
            int keyedFields=CheckKeyFields(hardwareDefinition,check);
            Check(check,keyedFields>=50,"typed key equality coverage mutates authored scalar/string/enum fields including AWD hardware and calibration (covered "+keyedFields+")");
            var fittedEntry=NewEntry(browser,fittedDefinition.id);
            var search=new NativePartQuery{Search=fittedAlias.id,InStockOnly=false,Compatibility="All"};
            Check(check,search.Matches(fittedEntry,profile.cash,false),"native Shop alias search remains discoverable through the grouped card");
            Check(check,fittedEntry.Owned&&catalog.Find(fittedAlias.id)==fittedAlias,
                "grouped card retains exact authored alias identity and current owned status");

            // A one-row live stock ledger makes price, stock and affordability changes explicit.
            var stockEntry=NewEntry(browser,modelAlias.id);
            var stockRow=new NewPartStock{definitions=new List<string>{modelAlias.id},units=20,initialUnits=20,deliveryPercent=10};
            profile.inventory.market=new PartsStockState{version=2,seed=profile.vehicleStock.seed,generation=profile.vehicleStock.generation,
                guaranteeAtRefresh=profile.driving.guaranteedFittingParts,parts=new List<NewPartStock>{stockRow}};
            profile.inventory.market.index=null;
            browser=Browser(profile);stockEntry=NewEntry(browser,modelAlias.id);
            Check(check,stockEntry.StockUnits==20&&!stockEntry.RequiresExpress&&stockEntry.Price==modelAlias.price,
                "ready group reads current stocked units and base price from the live market ledger");
            stockRow.units=0;
            var emptyStockBrowser=Browser(profile);var emptyStockEntry=NewEntry(emptyStockBrowser,modelAlias.id);
            Check(check,emptyStockEntry.StockUnits==0&&emptyStockEntry.RequiresExpress&&emptyStockEntry.Price>stockEntry.Price,
                "same ready groups observe depleted live stock and the current express price");
            var defaultStockFilter=new NativePartQuery{Availability="Affordable",Compatibility="All"};
            Check(check,!defaultStockFilter.Matches(emptyStockEntry,profile.cash,false),
                "the native query default continues to hide a zero-stock new offer");
            var affordable=new NativePartQuery{Availability="Affordable",InStockOnly=false,Compatibility="All"};
            profile.cash=Math.Max(0,emptyStockEntry.Price.Value-1);
            Check(check,!affordable.Matches(emptyStockEntry,profile.cash,false),"current cash excludes a live-stock offer just above affordability");
            profile.cash=emptyStockEntry.Price.Value;
            Check(check,affordable.Matches(emptyStockEntry,profile.cash,false),"current cash admits the same grouped offer at its exact live price");
            var inventoryCopy=profile.parts.First().Copy();inventoryCopy.instanceId="s06d-ready-owned-alias";
            inventoryCopy.definitionId=ordinalAliasA.id;inventoryCopy.location=PartLocation.Inventory;inventoryCopy.ownerId=inventoryCopy.slot="";
            profile.parts.Add(inventoryCopy);
            var newlyOwned=NewEntry(Browser(profile),ordinalAliasA.id);
            Check(check,newlyOwned.Owned&&newlyOwned.Id=="new:"+ordinalAliasA.id,
                "live ownership changes affect grouped card state while preserving the selected definition identity");
            profile.parts.Remove(inventoryCopy);
            Check(check,!NewEntry(Browser(profile),ordinalAliasA.id).Owned,
                "removing the owned alias restores the current unowned card state without changing group layout");
            Check(check,MarketCatalogLayout.TryReadyShopProductGroups(catalog,out var stillReady)&&SameGroups(ReadySourceGroups(stillReady),fallback),
                "cash, stock and ownership mutations do not invalidate authored product groups");

            // A live cart hold changes available units only; it must not alter the group snapshot.
            stockRow.units=20;
            profile.inventory.usingCartReservation=true;
            profile.inventory.cart=new CartReservationState{version=1,createdRun=profile.vehicleStock.completedRuns,
                expiresRun=profile.vehicleStock.completedRuns+CartReservations.LifetimeRaces,generation=profile.inventory.market.generation,
                carId=profile.SelectedCar.instanceId,lines=new List<PartCartLine>{new PartCartLine{action=CartAction.NewSet,definitionId=modelAlias.id}},
                supply=new List<ReservedPartSupply>{new ReservedPartSupply{definitionId=modelAlias.id,units=7,generation=profile.inventory.market.generation}}};
            var heldBrowser=Browser(profile,profile.inventory.cart.lines);var heldEntry=NewEntry(heldBrowser,modelAlias.id);
            Check(check,heldEntry.StockUnits==13&&heldEntry.Price==modelAlias.price&&profile.inventory.cart.supply[0].units==7&&
                heldBrowser.UsedReadyProductGroups&&SameGroups(ReadySourceGroups(stillReady),fallback),
                "active matching cart reservation reduces available stock while preserving held units, live price, and ready group identity");
            profile.inventory.usingCartReservation=false;profile.inventory.cart=null;

            // Public authored fields can change without an explicit revision bump or list replacement.
            int originalPrice=modelAlias.price;modelAlias.price=checked(originalPrice+1);
            Check(check,!MarketCatalogLayout.TryReadyShopProductGroups(catalog,out var editedGroups)&&editedGroups==null,
                "direct in-place semantic edit invalidates ready groups even without RebuildIndexes");
            var editedFallback=LegacySourceGroups(catalog);var editedBrowser=Browser(profile);
            Check(check,editedBrowser.Entries.Count(entry=>entry.Source=="New")==editedFallback.Length&&
                editedBrowser.Entries.Where(entry=>entry.Source=="New").Select(entry=>entry.Definition.id).SequenceEqual(LegacyRepresentatives(profile,editedFallback),StringComparer.Ordinal),
                "semantic invalidation returns the exact cold GroupBy/ProductKey fallback rather than stale grouped cards");
            modelAlias.price=originalPrice;catalog.RebuildIndexes();
            Check(check,!MarketCatalogLayout.TryReadyShopProductGroups(catalog,out _),
                "catalog authoring revision change invalidates the prior group snapshot");
            int secondWarmYields=Warm(catalog,check,"revised catalog",out double secondMaxWarmMoveMs,out double secondTotalWarmMs);
            Check(check,secondWarmYields>0&&MarketCatalogLayout.TryReadyShopProductGroups(catalog,out readyGroups),
                "revised catalog requires and completes a new warm publication");
            // Same catalog and source order for the ready lookup versus old grouping cost comparison.
            int iterations=32,readySink=0,legacySink=0;
            var timer=Stopwatch.StartNew();
            for(int i=0;i<iterations;i++)
            {
                if(!MarketCatalogLayout.TryReadyShopProductGroups(catalog,out var groups))break;
                readySink+=groups.Count;
            }
            timer.Stop();double readyMilliseconds=timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            for(int i=0;i<iterations;i++)legacySink+=LegacySourceGroups(catalog).Length;
            timer.Stop();double legacyMilliseconds=timer.Elapsed.TotalMilliseconds;
            Check(check,readySink>0&&legacySink>0,"timing comparison uses matched catalog definitions and original source order");
            long readyBrowserStart=Stopwatch.GetTimestamp();var timedBrowser=Browser(profile);
            double readyConstructionMilliseconds=(Stopwatch.GetTimestamp()-readyBrowserStart)*1000.0/Stopwatch.Frequency;
            check(true,string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "non-gating ready browser phase sums: representative={0:F3} supplyCount={1:F3} quoteHold={2:F3} entry={3:F3} alias={4:F3} used={5:F3} stockBinding={6:F3} ms bound={7} (inclusive enumeration and total reported separately)",
                timedBrowser.ShopRepresentativeMilliseconds,timedBrowser.ShopSupplyCountMilliseconds,
                timedBrowser.ShopQuoteAndHoldMilliseconds,timedBrowser.ShopEntryCreationMilliseconds,
                timedBrowser.ShopAliasMilliseconds,timedBrowser.ShopUsedOfferMilliseconds,
                timedBrowser.ShopStockBindingMilliseconds,timedBrowser.UsedReadyStockBinding));
            Check(check,timedBrowser.UsedReadyProductGroups,"ready browser construction uses the validated grouped snapshot");
            long fallbackStart=Stopwatch.GetTimestamp();
            int fallbackConstructionSink=0;
            for(int i=0;i<iterations;i++)fallbackConstructionSink+=BrowserAfterDirectInvalidation(profile,modelAlias).Entries.Count;
            double fallbackConstructionMilliseconds=(Stopwatch.GetTimestamp()-fallbackStart)*1000.0/Stopwatch.Frequency;
            Check(check,fallbackConstructionSink>0,"fallback browser construction timing exercised cold GroupBy path after direct invalidation");

            int replaceIndex=catalog.parts.FindIndex(definition=>definition.id==modelAlias.id);
            int countBeforeReplacement=catalog.parts.Count;
            var replacement=catalog.parts[replaceIndex].Copy();
            catalog.parts[replaceIndex]=replacement;
            Check(check,catalog.parts.Count==countBeforeReplacement&&!MarketCatalogLayout.TryReadyShopProductGroups(catalog,out var replacementGroups)&&replacementGroups==null,
                "same-count authored object replacement invalidates the ready group snapshot by identity");
            Check(check,MarketCatalogLayout.TryReadyShopProductGroups(catalog,out readyGroups)==false,
                "replacement remains unavailable until a complete rewarm");
            int replacementWarmYields=Warm(catalog,check,"same-count replacement",out double replacementMaxMoveMs,out double replacementTotalWarmMs);
            Check(check,replacementWarmYields>0&&MarketCatalogLayout.TryReadyShopProductGroups(catalog,out readyGroups),
                "same-count replacement publishes only after rewarming the new authored object identity");
            Check(check,true,"non-gating matched timing notes: ready lookup+validation "+readyMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " ms / "+iterations+" reads; previous GroupBy(ProductKey) "+legacyMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " ms / "+iterations+" groups; warm yields="+warmYields+" max MoveNext="+maxWarmMoveMs.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                " ms total="+totalWarmMs.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms; revised warm yields="+secondWarmYields+
                " max MoveNext="+secondMaxWarmMoveMs.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms total="+
                secondTotalWarmMs.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms; replacement warm yields="+replacementWarmYields+
                " max MoveNext="+replacementMaxMoveMs.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms total="+
                replacementTotalWarmMs.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms; browser ready construction="+
                readyConstructionMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms (strict validation="+
                timedBrowser.ProductGroupLookupMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms, Shop population="+
                timedBrowser.ShopPopulationMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms); repeated fallback browser construction="+
                fallbackConstructionMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" ms; fixture="+fixtureLabel+
                ". Timing is descriptive, not a pass threshold.");
        }
    }
#endif
}
