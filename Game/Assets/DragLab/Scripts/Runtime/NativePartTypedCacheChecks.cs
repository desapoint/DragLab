using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Exact-value checks for the live typed Parts preview cache.</summary>
    public static class NativePartTypedCacheChecks
    {
        static void Check(Action<bool,string> check,bool result,string label)
        {check(result,label);if(!result)throw new InvalidOperationException(label);}

        static NativePartsBaseReadSet Capture(PlayerProfile profile,NativePartsOwnerContext owner,out NativePartsBaseReadInputs inputs)
        {
            inputs=new NativePartsBaseReadInputs(profile,"Shop",false,null,new UnitPreferences(),owner,null);
            var session=NativePartsBaseReadSet.BeginCapture(inputs);
            while(!session.Complete)session.Advance(128,10);
            if(!session.TryFinish(inputs,out var facts,out var reason))throw new InvalidOperationException(reason??"Cache fixture capture failed.");
            return facts;
        }

        static NativePartEntry Entry(PartDefinition definition,int index)
            =>new NativePartEntry{Id="typed-entry-"+index,AssetId="typed-asset-"+index,Source="Owned",Definition=definition,Part=new PartInstance{instanceId="typed-asset-"+index,definitionId=definition.id,condition=new ComponentCondition{health=1,elements=Array.Empty<EngineElement>()}},Position="slot:"+index,Ready=true,Change=new double?[]{1,2,3,4,5,6,7}};

        public static void Run(Action<bool,string> check,string fixtureDirectory)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            Check(check,vehicles!=null&&vehicles.Length>0,"typed cache vehicle fixture loads");
            var vehicle=vehicles.First(v=>v.spec.id=="starter-hatch");
            var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            var profile=PlayerProfile.New(25102026);profile.cars.Clear();profile.selectedOwnedVehicleId="";profile.selectedVehicleId="";
            profile.vehicleStock=new VehicleStockState{version=1,seed=25102026,listings=new List<VehicleOffer>()};
            MechanicsOwnership.Ensure(profile,catalog);profile.cash=1000000;profile.reputation=1000;
            VehicleCollection.Buy(profile,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
            foreach(var _ in MarketCatalogLayout.WarmSteps(catalog)){}
            var owner=NativePartsOwnerContext.Capture();var facts=Capture(profile,owner,out var inputs);var cache=new NativePartPreviewCache();
            long legacyCar=NativePartPreviewCache.LegacyCarSignatureJsonCount,legacyEntry=NativePartPreviewCache.LegacyEntryKeyJsonCount;
            long canonicalJson=MarketCatalogLayout.CanonicalJsonEncodeCount,canonicalKeys=MarketCatalogLayout.CanonicalKeyRequestCount;
            var binding=cache.BindTyped(facts,inputs);var authored=catalog.parts[0];var first=Entry(authored,0);
            var physicalInspection=new CarInspection();cache.TypedCurrent(binding,false,()=>physicalInspection);
            var healthyInspection=new CarInspection();cache.TypedCurrent(binding,true,()=>healthyInspection);
            cache.StoreTyped(binding,first);var exact=Entry(authored,0);
            Check(check,cache.RestoreTyped(binding,exact)&&exact.Change.SequenceEqual(first.Change),"Exact typed entry facts reuse their seven comparison deltas");
            var changed=Entry(authored,0);changed.Part.condition.health=.75f;
            Check(check,!cache.RestoreTyped(binding,changed),"Same identity with changed condition bits misses the typed cache");
            cache.StoreTyped(binding,first);
            var changedAuthored=Entry(authored,0);int price=authored.price;authored.price=price+1;
            Check(check,!cache.RestoreTyped(binding,changedAuthored),"Same definition ID with changed authored field misses the typed cache");authored.price=price;
            cache.StoreTyped(binding,first);
            for(int i=1;i<=NativePartPreviewCache.ComparisonLimit;i++)cache.StoreTyped(binding,Entry(authored,i));
            Check(check,!cache.RestoreTyped(binding,Entry(authored,0)),"Typed comparison LRU evicts the oldest entry at its shared 256-entry bound");
            var pistonDefinition=catalog.parts.First(d=>d.category==PartCategory.Pistons);
            NativePartEntry UnitEntry()=>new NativePartEntry{Id="typed-loose-unit",AssetId="typed-loose-unit",Source="Owned",Definition=pistonDefinition,
                Unit=new LooseEngineElement{definitionId=pistonDefinition.id,storageId=SharedInventory.Garage,unitPrice=37,
                    element=new PistonElement{id="typed-loose-unit",definitionId=pistonDefinition.id,role="piston",cylinder=-1,valve=-1,health=.64f}},
                Position="position-a",Ready=true,Change=new double?[]{1,2,3,4,5,6,7}};
            cache.StoreTyped(binding,UnitEntry());
            Check(check,cache.RestoreTyped(binding,UnitEntry()),"Exact loose-member facts reuse their typed comparison");
            var changedUnit=UnitEntry();changedUnit.Unit.element.health=.63f;
            Check(check,!cache.RestoreTyped(binding,changedUnit),"Same loose-member identity with changed condition misses");
            cache.StoreTyped(binding,UnitEntry());var changedPosition=UnitEntry();changedPosition.Position="position-b";
            Check(check,!cache.RestoreTyped(binding,changedPosition),"A different selected position cannot reuse another member comparison");
            var changedRole=UnitEntry();changedRole.Unit.element.role="another-role";
            Check(check,!cache.RestoreTyped(binding,changedRole),"A different replacement role cannot reuse another role comparison");
            var foreignCache=new NativePartPreviewCache();var foreignBinding=foreignCache.BindTyped(facts,inputs);bool foreignRejected=false;
            try{cache.RestoreTyped(foreignBinding,Entry(authored,1));}catch(InvalidOperationException){foreignRejected=true;}
            Check(check,foreignRejected,"A typed binding from another cache instance cannot restore results");
            Check(check,NativePartPreviewCache.LegacyCarSignatureJsonCount==legacyCar&&NativePartPreviewCache.LegacyEntryKeyJsonCount==legacyEntry,
                "Typed bind/store/restore/eviction do not call legacy JSON signature or entry-key encoders");
            Check(check,MarketCatalogLayout.CanonicalJsonEncodeCount==canonicalJson&&MarketCatalogLayout.CanonicalKeyRequestCount==canonicalKeys,
                "Typed cache operations do not request canonical JSON keys");
            var again=Capture(profile,owner,out var sameInputs);
            Check(check,facts.SamePhysicalInspectionInputs(again),"Exact selected inspection inputs compare equal across independent captures");
            int cash=profile.cash;profile.cash++;
            var cashChanged=Capture(profile,owner,out var cashInputs);
            Check(check,!facts.SameWideCacheContext(cashChanged)&&facts.SamePhysicalInspectionInputs(cashChanged),
                "Cash invalidates wide entry context while retaining unchanged physical inspection inputs");
            var changedBinding=cache.BindTyped(cashChanged,cashInputs);
            Check(check,changedBinding.Generation>binding.Generation,"A changed wide context advances the opaque cache generation");
            Check(check,ReferenceEquals(cache.TypedCurrent(changedBinding,false,()=>new CarInspection()),physicalInspection),
                "An unchanged physical inspection survives a cash-only context change");
            Check(check,ReferenceEquals(cache.TypedCurrent(changedBinding,true,()=>new CarInspection()),healthyInspection),
                "The unchanged healthy inspection also survives a cash-only context change");
            bool staleRejected=false;try{cache.StoreTyped(binding,Entry(authored,900));}catch(InvalidOperationException){staleRejected=true;}
            Check(check,staleRejected,"A stale prior generation cannot store into the current cache");profile.cash=cash;
            var marketRow=profile.inventory.market.parts.First();int stock=marketRow.units;marketRow.units++;
            var stockChanged=Capture(profile,owner,out var stockInputs);var stockBinding=cache.BindTyped(stockChanged,stockInputs);
            Check(check,facts.SamePhysicalInspectionInputs(stockChanged)&&
                ReferenceEquals(cache.TypedCurrent(stockBinding,false,()=>new CarInspection()),physicalInspection)&&
                ReferenceEquals(cache.TypedCurrent(stockBinding,true,()=>new CarInspection()),healthyInspection),
                "Stock-only changes retain both exact physical inspections");marketRow.units=stock;
            string selectedId=profile.selectedOwnedVehicleId,selectedModel=profile.selectedVehicleId;
            VehicleCollection.Buy(profile,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
            var other=profile.SelectedCar;profile.selectedOwnedVehicleId=selectedId;profile.selectedVehicleId=selectedModel;
            var addedCar=Capture(profile,owner,out var addedInputs);var addedBinding=cache.BindTyped(addedCar,addedInputs);
            Check(check,ReferenceEquals(cache.TypedCurrent(addedBinding,false,()=>new CarInspection()),physicalInspection),
                "Buying another actual complete car does not evict the unchanged selected inspection");
            var otherPart=profile.parts.First(p=>p.ownerId==other.instanceId||p.ownerId==other.engineAssemblyId);
            float otherHealth=otherPart.condition.health;otherPart.condition.health=.73f;
            var unrelatedWear=Capture(profile,owner,out var wearInputs);var wearBinding=cache.BindTyped(unrelatedWear,wearInputs);
            Check(check,addedCar.SamePhysicalInspectionInputs(unrelatedWear)&&
                ReferenceEquals(cache.TypedCurrent(wearBinding,false,()=>new CarInspection()),physicalInspection)&&
                ReferenceEquals(cache.TypedCurrent(wearBinding,true,()=>new CarInspection()),healthyInspection),
                "Unrelated-car wear retains both selected-car inspections");otherPart.condition.health=otherHealth;
            var originalTune=profile.SelectedCar.tune.camTimingDegrees;profile.SelectedCar.tune.camTimingDegrees=originalTune+1;
            var tuneChanged=Capture(profile,owner,out var tuneInputs);
            Check(check,!facts.SamePhysicalInspectionInputs(tuneChanged),"A tune bit change invalidates physical inspection reuse");
            var tuneBinding=cache.BindTyped(tuneChanged,tuneInputs);
            Check(check,!ReferenceEquals(cache.TypedCurrent(tuneBinding,false,()=>new CarInspection()),physicalInspection),
                "A changed tune recomputes the current physical inspection");profile.SelectedCar.tune.camTimingDegrees=originalTune;
            var installed=profile.SelectedCar.installed;var savedSlots=installed.slots;int savedVersion=installed.version;
            try
            {
                installed.slots=null;var nullSlots=Capture(profile,owner,out _);
                installed.version=savedVersion+1;var changedNullSlots=Capture(profile,owner,out var changedNullInputs);
                Check(check,!nullSlots.SamePhysicalInspectionInputs(changedNullSlots)&&!nullSlots.MatchesCurrent(changedNullInputs,out _),
                    "Null slot lists do not hide a changed installed-build version from typed freshness or reuse");
            }
            finally{installed.slots=savedSlots;installed.version=savedVersion;}
            Capture(profile,owner,out var validInputs);var terminal=NativePartBrowserBuild.Begin(validInputs);
            var duplicatePrice=new NativePartsPriceFact("Used","invalid-duplicate",1);
            var invalidInputs=new NativePartsBaseReadInputs(profile,"Shop",false,null,new UnitPreferences(),owner,new[]{duplicatePrice,duplicatePrice});
            terminal.Advance(invalidInputs,1,2);
            Check(check,terminal.Failed&&!terminal.InputsChanged&&terminal.Model==null,
                "Invalid duplicate price facts are terminal errors, not input-change automatic retries");
            var pricePolicy=ValuationPolicy.Defaults();
            var queryPrices=profile.inventory.dealer.Where(stock=>stock.IsPart||stock.IsUnit)
                .Select(stock=>new NativePartsPriceFact("Used",stock.Id,InventoryPricing.BuyUsed(stock,pricePolicy))).ToArray();
            var queryInputs=new NativePartsBaseReadInputs(profile,"Shop",false,null,new UnitPreferences(),owner,queryPrices);
            var queryModel=NativePartBrowserBuild.Begin(queryInputs,new NativePartPreviewCache());int steps=0;
            while(!queryModel.Complete&&!queryModel.Failed&&steps++<100000)queryModel.Advance(queryInputs,512,2);
            Check(check,queryModel.Complete,"Typed query oracle has a fully prepared private model / "+queryModel.State+" / "+queryModel.FailureReason);
            foreach(NativePartSection section in Enum.GetValues(typeof(NativePartSection)))
            {
                var query=new NativePartQuery{Compatibility="All",InStockOnly=false,Sort="Price",Descending=(int)section%2!=0};
                var expectedSection=queryModel.Model.Entries.Where(e=>NativePartBrowser.Section(e.Definition.category)==section).ToList();
                var expected=expectedSection.Where(e=>query.Matches(e,profile.cash)).ToList();expected.Sort(query.Compare);
                var work=new NativePartQueryBuild(queryModel.Model,query,section,profile.cash,1);steps=0;
                while(!work.Complete&&steps++<100000)work.Advance(512,2);
                Check(check,work.Complete&&work.SectionCount==expectedSection.Count,
                    "Bounded query preserves the original section denominator / "+section);
                Check(check,work.Result.Select(e=>e.Id).SequenceEqual(expected.Select(e=>e.Id)),
                    "Bounded merge sort matches the synchronous filter/order oracle / "+section);
            }
            int evaluations=queryModel.Model.EvaluationCount;
            var emptyQuery=new NativePartQuery{Search="no-such-product-typed-query",Sort=NativePartBrowser.MetricNames[0]};
            var emptyWork=new NativePartQueryBuild(queryModel.Model,emptyQuery,NativePartSection.Engine,profile.cash,2);steps=0;
            while(!emptyWork.Complete&&steps++<100000)emptyWork.Advance(512,2);
            Check(check,emptyWork.Complete&&emptyWork.Result.Count==0&&emptyWork.SectionCount>0&&
                queryModel.Model.EvaluationCount==evaluations,
                "An empty change-dependent query completes with its section denominator and no candidate evaluation");
        }
    }
#endif
}
