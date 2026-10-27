using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal enum NativePartSection { Engine, Induction, Drivetrain, WheelsBrakes, BodyChassis }
    internal sealed class NativePartQuery
    {
        internal string Search="",Compatibility="Compatible",Availability="All",Condition="All",Tuning="All",Sort="Price",Container="All",BrandFocus="All";
        internal bool Descending,InStockOnly=true;
        internal HashSet<string> Brands=new HashSet<string>(StringComparer.Ordinal);
        internal HashSet<PartCategory> Categories=new HashSet<PartCategory>();
        internal string[] Direction=Enumerable.Repeat("Any",7).ToArray();
        internal double?[] Minimum=new double?[7];
        internal NativePartQuery Copy(){var c=(NativePartQuery)MemberwiseClone();c.Categories=new HashSet<PartCategory>(Categories);c.Brands=new HashSet<string>(Brands,StringComparer.Ordinal);c.Direction=(string[])Direction.Clone();c.Minimum=(double?[])Minimum.Clone();return c;}
        internal bool RequiresChanges=>Array.IndexOf(NativePartBrowser.MetricNames,Sort)>=0||Direction.Any(v=>v!="Any")||Minimum.Any(v=>v.HasValue);
        internal bool Matches(NativePartEntry e,int cash,bool changes=true)
        {
            if(Categories.Count>0&&!Categories.Contains(e.Definition.category))return false;
            if(!e.BrandNames.Any(brand=>(Brands.Count==0||Brands.Contains(brand))&&(BrandFocus=="All"||PartBrands.Focus(brand)==BrandFocus)))return false;
            if(Container!="All"&&e.Location!=Container)return false;
            if(!string.IsNullOrWhiteSpace(Search)&&!((e.DisplayName??e.Definition.name)+" "+e.SearchExtra+" "+string.Join(" ",e.BrandNames.Select(b=>b+" "+PartBrands.Focus(b)))+" "+e.Definition.category+" "+e.AssetId+" "+e.Location).ToLowerInvariant().Contains(Search.Trim().ToLowerInvariant()))return false;
            if(Compatibility!="All"&&e.Compatibility!=Compatibility)return false;
            if(Availability=="Owned"&&!e.Owned||Availability=="Affordable"&&(!e.Price.HasValue||e.Price>cash)||Availability=="Unaffordable"&&(!e.Price.HasValue||e.Price<=cash))return false;
            if(InStockOnly&&e.Source=="New"&&e.StockUnits<=0)return false;
            if(Condition=="New"&&e.Source!="New"||Condition=="Used"&&e.Source=="New")return false;
            bool tunable=e.Definition.tuning!=PartTuningOptions.None||e.Definition.camTimingAdjustable;
            if(Tuning=="Adjustable"&&!tunable||Tuning=="Fixed"&&tunable)return false;
            if(!changes)return true;
            for(int i=0;i<Direction.Length;i++){
                if(Direction[i]=="Any"&&!Minimum[i].HasValue)continue;
                var v=e.Change[i];if(!v.HasValue)return false;double tolerance=i==3?.01:.001;
                if(Direction[i]=="Increase"&&v<=tolerance||Direction[i]=="Decrease"&&v>=-tolerance||Direction[i]=="Unchanged"&&Math.Abs(v.Value)>tolerance)return false;
                if(Minimum[i].HasValue&&Math.Abs(v.Value)+tolerance<Minimum[i].Value)return false;
            }
            return true;
        }
        internal int Compare(NativePartEntry a,NativePartEntry b)
        {
            int metric=Array.IndexOf(NativePartBrowser.MetricNames,Sort);double? av=Sort=="Price"?a.Price:metric>=0?a.Change[metric]:null,bv=Sort=="Price"?b.Price:metric>=0?b.Change[metric]:null;
            int result;
            if(Sort=="Name")result=string.Compare(a.DisplayName??a.Definition.name,b.DisplayName??b.Definition.name,StringComparison.OrdinalIgnoreCase);
            else {if(av.HasValue!=bv.HasValue)return av.HasValue?-1:1;result=av.HasValue?av.Value.CompareTo(bv.Value):0;}
            if(Descending)result=-result;return result!=0?result:string.CompareOrdinal(a.Id,b.Id);
        }
    }
    internal sealed class NativePartEntry
    {
        internal string Id,AssetId,Source,Location,Compatibility="Calculating…",Reason="",Position;
        internal PartDefinition Definition;
        internal string DisplayName,SearchExtra="";
        internal string[] FittedBrands;
        internal IEnumerable<string> BrandNames=>FittedBrands??new[]{Definition.brand};
        internal PartInstance Part;
        internal LooseEngineElement Unit;
        internal bool Owned,Ready,RequiresMatchingSet;
        internal int? Price;
        internal int StockUnits;internal bool RequiresExpress;
        internal double?[] Change=new double?[7];
        internal CarInspection Proposed;
    }
    internal sealed class NativePartBrowser
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static int legacyPlanJsonCount,legacyOwnedCarJsonCount,legacyCartPreviewCount;
        internal static int LegacyPlanJsonCount=>legacyPlanJsonCount;
        internal static int LegacyOwnedCarJsonCount=>legacyOwnedCarJsonCount;
        internal static int LegacyCartPreviewCount=>legacyCartPreviewCount;
#else
        internal static int LegacyPlanJsonCount=>0;
        internal static int LegacyOwnedCarJsonCount=>0;
        internal static int LegacyCartPreviewCount=>0;
#endif
        internal static readonly string[] SectionNames={"Engine","Induction","Drivetrain","Wheels & brakes","Body & chassis"};
        internal static readonly string[] MetricNames={"HP change","Torque change","Weight change","Condition change","Torque strength","RPM strength","Cooling change"};
        internal readonly List<NativePartEntry> Entries=new List<NativePartEntry>();
        internal CarInspection Current,Reference,Plan;
        CarInspection healthyCurrent;
        bool healthyCurrentReady;
        internal CarInspection HealthyCurrent=>GetHealthyCurrent();
        internal CarInspection GetHealthyCurrent()
        {
            if(!healthyCurrentReady){healthyCurrentReady=true;healthyCurrent=typedBinding==null||actualCar==null||string.IsNullOrEmpty(actualCar.engineAssemblyId)?null:cache.TypedCurrent(typedBinding,true,()=>Inspect(InspectionBuild.Capture(actual,actualCar,healthy:true),cache));}
            return healthyCurrent;
        }
        internal string PlanError;
        readonly bool plannedReference;
        readonly NativePartPreviewCache cache;readonly string referenceKey;
        readonly NativePartPreviewCache.TypedBinding typedBinding;
        internal int EvaluationCount {get;private set;}
        internal double EvaluationMilliseconds {get;private set;}
        internal bool UsedReadyProductGroups{get;private set;}
        internal double ProductGroupLookupMilliseconds{get;private set;}
        internal double ShopPopulationMilliseconds{get;private set;}
        internal double CurrentInspectionMilliseconds{get;private set;}
        internal double ShopLedgerSetupMilliseconds{get;private set;}
        internal double ShopProductEnumerationMilliseconds{get;private set;}
        internal double ShopClassificationMilliseconds{get;private set;}
        internal double ShopRepresentativeMilliseconds{get;private set;}
        internal double ShopAliasMilliseconds{get;private set;}
        internal double ShopSupplyCountMilliseconds{get;private set;}
        internal double ShopQuoteAndHoldMilliseconds{get;private set;}
        internal double ShopEntryCreationMilliseconds{get;private set;}
        internal double ShopUsedOfferMilliseconds{get;private set;}
        internal double ShopStockBindingMilliseconds{get;private set;}
        internal bool UsedReadyStockBinding{get;private set;}
        internal double ConstructorSetupMilliseconds{get;private set;}
        readonly PlayerProfile actual,reference;
        readonly OwnedCar actualCar,referenceCar;
        readonly Dictionary<PartCategory,string> referenceMap;
        readonly VehicleSpec referenceSpec;
        IEnumerator<int> stagedSteps;
        internal bool PreparationUnavailable{get;private set;}
        internal bool BuildFinished{get;private set;}
        internal int LastAdvanceItems{get;private set;}
        int buildPhase;
        static double ElapsedMilliseconds(long start)=>(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        internal NativePartBrowser(PlayerProfile profile,IEnumerable<PartCartLine> lines,bool planned,string page,Func<DealerStock,int> usedPrice,Func<string,bool,int> salePrice,NativePartPreviewCache cache=null)
            :this(profile,lines,planned,page,usedPrice==null?(Func<DealerStock,int?>)null:stock=>(int?)usedPrice(stock),salePrice,cache,false,false,null,null){}
        internal NativePartBrowser(PlayerProfile profile,IEnumerable<PartCartLine> lines,bool planned,string page,Func<DealerStock,int?> usedPrice,Func<string,bool,int> salePrice,NativePartPreviewCache cache,bool deferBuild,bool requireReadyGroups,IReadOnlyList<MarketCatalogLayout.ReadyProductGroup> suppliedGroups,IReadOnlyDictionary<string,MarketCatalogLayout.ReadyProductGroup> suppliedGroupById,NativePartPreviewCache.TypedBinding typedBinding=null)
        {
            long setupStart=System.Diagnostics.Stopwatch.GetTimestamp();
            actual=profile;actualCar=profile.SelectedCar;plannedReference=planned;this.cache=cache;this.typedBinding=typedBinding;
            if(actualCar==null){BuildFinished=true;ConstructorSetupMilliseconds=ElapsedMilliseconds(setupStart);return;}
            long currentInspectionStart=System.Diagnostics.Stopwatch.GetTimestamp();if(!requireReadyGroups)cache?.Bind(profile,actualCar);
            Current=string.IsNullOrEmpty(actualCar.engineAssemblyId)?null:typedBinding!=null?cache.TypedCurrent(typedBinding,false,()=>Inspect(InspectionBuild.Capture(profile,actualCar),cache)):requireReadyGroups||cache==null?Inspect(InspectionBuild.Capture(profile,actualCar)):cache.Inspection("current",()=>Inspect(InspectionBuild.Capture(profile,actualCar),cache));
            reference=profile;
            CurrentInspectionMilliseconds=ElapsedMilliseconds(currentInspectionStart);
            var cart=lines.Select(l=>l.Copy()).ToArray();
            string planKey="current";
            if(cart.Length>0){
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                legacyPlanJsonCount++;
                RuntimeDataEncodingProbe.RecordJsonCall();
#endif
                string incoming=cache==null?JsonUtility.ToJson(profile):cache.Context+"/"+string.Join("/",cart.Select(l=>JsonUtility.ToJson(profile.parts.Find(i=>i.instanceId==l.assetId))+JsonUtility.ToJson(profile.looseEngineElements.Find(i=>i.element.id==l.assetId))+JsonUtility.ToJson(profile.inventory.dealer.Find(i=>i.Id==l.assetId))+JsonUtility.ToJson(profile.catalog.Find(l.definitionId))));planKey="plan/"+NativePartPreviewCache.Fingerprint(JsonUtility.ToJson(new PartCart.Lines{entries=cart.ToList()})+incoming);}
            if(cart.Length>0)try {PartCart.Total(profile,actualCar.instanceId,cart);var copy=cache==null?PartCart.PreviewProfile(profile,actualCar,cart):cache.Plan(planKey,()=>PartCart.PreviewProfile(profile,actualCar,cart));Plan=cache==null?Inspect(InspectionBuild.Capture(copy,copy.SelectedCar)):cache.Inspection(planKey,()=>Inspect(InspectionBuild.Capture(copy,copy.SelectedCar),cache));if(planned)reference=copy;}catch(Exception e){PlanError=e.Message;}
            referenceKey=planned&&Plan!=null?planKey:"current";
            referenceCar=reference.SelectedCar;referenceMap=MechanicsOwnership.Definitions(reference,referenceCar);Reference=planned&&PlanError!=null||string.IsNullOrEmpty(referenceCar.engineAssemblyId)?null:ReferenceEquals(reference,profile)?Current:Plan;
            referenceSpec=actual.catalog.Resolve(referenceCar.vehicleId,referenceMap,referenceCar.tune).spec;
            ConstructorSetupMilliseconds=ElapsedMilliseconds(setupStart);
            stagedSteps=PopulateEntries(profile,page,usedPrice,salePrice,requireReadyGroups,suppliedGroups,suppliedGroupById).GetEnumerator();
            if(!deferBuild)while(!AdvanceBuild(int.MaxValue,double.MaxValue)){}
        }
        internal bool AdvanceBuild(int itemCap,double softMilliseconds)
        {
            if(BuildFinished||PreparationUnavailable)return true;
            if(itemCap<1)itemCap=1;long start=System.Diagnostics.Stopwatch.GetTimestamp();int done=0;LastAdvanceItems=0;
            while(done<itemCap&&(done==0||ElapsedMilliseconds(start)<softMilliseconds))
            {long itemStart=System.Diagnostics.Stopwatch.GetTimestamp();if(!stagedSteps.MoveNext()){stagedSteps.Dispose();stagedSteps=null;BuildFinished=true;break;}double itemMs=ElapsedMilliseconds(itemStart);if(buildPhase==1){ShopPopulationMilliseconds+=itemMs;ShopProductEnumerationMilliseconds+=itemMs;}else if(buildPhase==2){ShopPopulationMilliseconds+=itemMs;ShopClassificationMilliseconds+=itemMs;}done++;LastAdvanceItems++;}
            return BuildFinished||PreparationUnavailable;
        }
        internal void AbandonBuild(){stagedSteps?.Dispose();stagedSteps=null;BuildFinished=true;}
        IEnumerable<int> PopulateEntries(PlayerProfile profile,string page,Func<DealerStock,int?> usedPrice,Func<string,bool,int> salePrice,bool requireReadyGroups,IReadOnlyList<MarketCatalogLayout.ReadyProductGroup> suppliedGroups,IReadOnlyDictionary<string,MarketCatalogLayout.ReadyProductGroup> suppliedGroupById)
        {
            var ownedDefinitions=new HashSet<string>(StringComparer.Ordinal);
            foreach(var unit in profile.looseEngineElements){ownedDefinitions.Add(unit.definitionId);yield return 0;}
            foreach(var part in profile.parts){if(part.condition.tires.Length==4)for(int i=0;i<4;i++)ownedDefinitions.Add(TireFitments.DefinitionId(part,i));else if(WheelFitments.HasOverrides(part))for(int i=0;i<4;i++)ownedDefinitions.Add(WheelFitments.DefinitionId(part,i));else ownedDefinitions.Add(part.definitionId);yield return 0;}
            bool Owns(string id)=>ownedDefinitions.Contains(id);
            if(page=="Installed")foreach(var slot in actualCar.installed.slots){var p=profile.parts.Find(i=>i.instanceId==slot.partInstanceId);if(p!=null)Add(p.instanceId,p.definitionId,"Installed",p,null,true,p.ownerId,null);yield return 0;}
            if(page=="Inventory"){
                foreach(var p in profile.parts){if(!actualCar.installed.slots.Any(s=>s.partInstanceId==p.instanceId))Add(p.instanceId,p.definitionId,"Owned",p,null,true,p.location==PartLocation.Inventory?SharedInventory.StorageOf(p.storageId):p.ownerId,SafePrice(()=>salePrice(p.instanceId,false),p.location==PartLocation.Inventory));yield return 0;}
                foreach(var u in profile.looseEngineElements){Add(u.element.id,u.definitionId,"Owned",null,u,true,SharedInventory.StorageOf(u.storageId),SafePrice(()=>salePrice(u.element.id,true),true));yield return 0;}
            }
            bool shopPage=page=="Shop";
            if(shopPage){
                long lookupStart=System.Diagnostics.Stopwatch.GetTimestamp();
                IReadOnlyList<MarketCatalogLayout.ReadyProductGroup> readyGroups=suppliedGroups;IReadOnlyDictionary<string,MarketCatalogLayout.ReadyProductGroup> readyGroupById=suppliedGroupById;
                bool ready=readyGroups!=null&&readyGroupById!=null;
                if(!ready&&!requireReadyGroups)ready=MarketCatalogLayout.TryReadyShopProductGroups(profile.catalog,out readyGroups,out readyGroupById);
                ProductGroupLookupMilliseconds=ElapsedMilliseconds(lookupStart);UsedReadyProductGroups=ready;
                if(!ready&&requireReadyGroups){PreparationUnavailable=true;yield break;}
                long ledgerStart=System.Diagnostics.Stopwatch.GetTimestamp();var fitted=new HashSet<string>(referenceMap.Values);
                var supplyCount=ready?PartsMarket.CounterIndexed(profile,actualCar):PartsMarket.Counter(profile,actualCar);
                var shelf=new PartsMarket.Ledger(profile,profile.inventory.usingCartReservation,ready?readyGroups:null,ready?readyGroupById:null);ShopLedgerSetupMilliseconds=ElapsedMilliseconds(ledgerStart);
                string prefix=referenceCar.vehicleId+"/";buildPhase=1;
                if(ready){UsedReadyStockBinding=true;
                    foreach(var product in readyGroups){long phase=System.Diagnostics.Stopwatch.GetTimestamp();PartDefinition d=null;int best=int.MaxValue;bool owned=false;
                        foreach(var id in product.DefinitionIds){var candidate=profile.catalog.Find(id);if(candidate==null)continue;owned|=Owns(id);int rank=fitted.Contains(id)?0:id.StartsWith(prefix,StringComparison.Ordinal)?1:2;if(rank<best||rank==best&&StringComparer.Ordinal.Compare(id,d?.id)<0){d=candidate;best=rank;}}
                        ShopRepresentativeMilliseconds+=ElapsedMilliseconds(phase);if(d!=null){phase=System.Diagnostics.Stopwatch.GetTimestamp();int units=supplyCount(d);ShopSupplyCountMilliseconds+=ElapsedMilliseconds(phase);phase=System.Diagnostics.Stopwatch.GetTimestamp();var quote=shelf.Quote(d,units,d.price);int available=Math.Max(0,shelf.Available(d)-(profile.inventory.usingCartReservation?shelf.Held(d):0));ShopQuoteAndHoldMilliseconds+=ElapsedMilliseconds(phase);phase=System.Diagnostics.Stopwatch.GetTimestamp();Add("new:"+d.id,d.id,"New",null,null,owned,"New",quote.cost);if(Entries.Count>0){Entries[Entries.Count-1].StockUnits=available;Entries[Entries.Count-1].RequiresExpress=quote.expressUnits>0;Entries[Entries.Count-1].SearchExtra=string.Join(" ",product.DefinitionIds);}ShopEntryCreationMilliseconds+=ElapsedMilliseconds(phase);}
                        yield return 0;
                    }
                }else foreach(var product in profile.catalog.parts.GroupBy(NewProductKey,StringComparer.Ordinal)){var d=product.OrderBy(p=>fitted.Contains(p.id)?0:p.id.StartsWith(prefix,StringComparison.Ordinal)?1:2).ThenBy(p=>p.id,StringComparer.Ordinal).First();var quote=shelf.Quote(d,supplyCount(d),d.price);int available=Math.Max(0,shelf.Available(d)-(profile.inventory.usingCartReservation?shelf.Held(d):0));Add("new:"+d.id,d.id,"New",null,null,product.Any(p=>Owns(p.id)),"New",quote.cost);Entries[Entries.Count-1].StockUnits=available;Entries[Entries.Count-1].RequiresExpress=quote.expressUnits>0;Entries[Entries.Count-1].SearchExtra=string.Join(" ",product.Select(p=>p.id));yield return 0;}
                foreach(var d in profile.inventory.dealer){if(d.IsPart||d.IsUnit){long usedStart=System.Diagnostics.Stopwatch.GetTimestamp();Add("used:"+d.Id,d.DefinitionId,"Used",d.IsPart?d.part:null,d.IsUnit?d.unit:null,Owns(d.DefinitionId),"Dealer",usedPrice(d));ShopUsedOfferMilliseconds+=ElapsedMilliseconds(usedStart);}yield return 0;}
            }
            buildPhase=shopPage?2:0;foreach(var entry in Entries){Classify(entry);yield return 0;}
        }
        internal static string NewProductKey(PartDefinition definition)
        {
            return PartsMarket.ProductKey(definition);
        }
        static int? SafePrice(Func<int> get,bool available){if(!available)return null;try{return get();}catch(Exception){return null;}}
        void Add(string id,string definition,string source,PartInstance part,LooseEngineElement unit,bool owned,string location,int? price)
        {var d=actual.catalog.Find(definition);if(d!=null){var tireParts=TireFitments.HasOverrides(part)?Enumerable.Range(0,4).Select(i=>actual.catalog.Find(TireFitments.DefinitionId(part,i))).ToArray():WheelFitments.HasOverrides(part)?Enumerable.Range(0,4).Select(i=>actual.catalog.Find(WheelFitments.DefinitionId(part,i))).ToArray():new PartDefinition[0];Entries.Add(new NativePartEntry{Id=id,AssetId=part?.instanceId??unit?.element.id,Definition=d,FittedBrands=tireParts.Length>0?tireParts.Select(t=>t.brand).Distinct().ToArray():new[]{d.brand},DisplayName=tireParts.Length>0?tireParts.Select(t=>t.id).Distinct().Count()>1?d.category==PartCategory.Wheels?"Mixed wheels":"Mixed tires":tireParts[0].name:d.name,SearchExtra=string.Join(" ",tireParts.Select(t=>t.name+" "+t.brand)),Source=source,Part=part,Unit=unit,Owned=owned,Location=location,Price=price});}}
        internal static NativePartSection Section(PartCategory c)
        {
            if(c==PartCategory.Induction||c==PartCategory.Exhaust)return NativePartSection.Induction;
            if(c==PartCategory.Wheels||c==PartCategory.Tires||c==PartCategory.Brakes)return NativePartSection.WheelsBrakes;
            if(c==PartCategory.Body||c==PartCategory.Interior||c==PartCategory.Accessories)return NativePartSection.BodyChassis;
            if(c==PartCategory.Clutch||c==PartCategory.Flywheel||c==PartCategory.Gearbox||c==PartCategory.Differential||c==PartCategory.Driveshaft||MechanicalCatalog.DriveBranch(c))return NativePartSection.Drivetrain;
            return NativePartSection.Engine;
        }
        internal static CarInspection Inspect(InspectionBuild b,NativePartPreviewCache cache=null)
        {
            var prepared=CarInspection.PrepareCar(b);
            return CarInspection.CalculatePrepared(b,prepared,false,cache==null?null:cache.SteadyOutput(prepared.induction));
        }
        internal string[] Positions(NativePartEntry e)=>e.Unit==null?new string[0]:InventoryUnits.Positions(reference,referenceCar,e.Definition.id,e.Unit.element.role);
        internal string PositionLabel(NativePartEntry e,string position)=>InventoryUnits.PositionLabel(reference,referenceCar,e.Definition.id,position);
        internal NativePartEntry PreviewNewUnit(NativePartEntry offer,string role,IEnumerable<string> selection)
        {
            var e=new NativePartEntry{Id=offer.Id,Source="New",Definition=offer.Definition,Owned=offer.Owned,Price=PartsMarket.Quote(actual,offer.Definition,InventoryUnits.Count(actual,actualCar,offer.Definition),true).cost};
            var member=EngineElements.Create(e.Definition.category);
            member.id="native-offer-unit";member.definitionId=e.Definition.id;member.role=role;member.cylinder=-1;member.valve=-1;
            e.Unit=new LooseEngineElement{definitionId=e.Definition.id,element=member,tireUnit=e.Definition.category==PartCategory.Tires};
            var available=InventoryUnits.Positions(reference,referenceCar,e.Definition.id,role);var selected=selection.Where(available.Contains).Distinct().ToArray();
            if(selected.Length==0&&available.Length>0)selected=new[]{available[0]};e.Position=selected.FirstOrDefault();Evaluate(e);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(selected.Length>1&&e.Proposed!=null){legacyCartPreviewCount++;RuntimeDataEncodingProbe.RecordJsonCall();}
#endif
            if(selected.Length>1&&e.Proposed!=null){
                var copy=PartCart.PreviewProfile(reference,referenceCar,new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=e.Definition.id,role=role,quantity=selected.Length,install=true,positions=selected.ToList()}});
                e.Proposed=Inspect(InspectionBuild.Capture(copy,copy.SelectedCar),cache);
            }
            return e;
        }
        void Classify(NativePartEntry e)
        {
            if(plannedReference&&PlanError!=null){e.Ready=true;e.Compatibility="Unavailable";e.Reason=PlanError;return;}
            if(!actual.catalog.Fits(referenceSpec,e.Definition)){e.Ready=true;e.Compatibility="Incompatible";e.Reason="Mounting, bore, fuel type or controller interface does not fit this build.";return;}
            e.Compatibility="Compatible";
            if(e.Definition.category==PartCategory.Camshaft||e.Definition.category==PartCategory.Followers){var map=new Dictionary<PartCategory,string>(referenceMap){[e.Definition.category]=e.Definition.id};var result=actual.catalog.Resolve(referenceCar.vehicleId,map,referenceCar.tune);if(result.errors.Any(error=>error.Contains("cannot mate")))e.Compatibility="Requires supporting changes";}
            if(Reference==null){e.Ready=true;e.Reason="Attach an engine to compare performance.";}
        }
        internal void Evaluate(NativePartEntry e)
        {
            if(e.Ready)return;string key=typedBinding==null&&cache!=null?NativePartPreviewCache.Key(referenceKey,e):null;if(cache!=null&&(typedBinding!=null?cache.RestoreTyped(typedBinding,e):cache.Restore(key,e)))return;
            var clock=System.Diagnostics.Stopwatch.StartNew();EvaluationCount++;e.Ready=true;var d=e.Definition;
            if(plannedReference&&PlanError!=null){e.Compatibility="Unavailable";e.Reason=PlanError;return;}
            if(!actual.catalog.Fits(referenceSpec,d)){e.Compatibility="Incompatible";e.Reason="Mounting, bore, fuel type or controller interface does not fit this build.";return;}
            if(Reference==null){e.Compatibility="Compatible";e.Reason="Attach an engine to compare performance.";return;}
            try{
                InspectionBuild candidate;
                if(e.Unit!=null){
                    var positions=InventoryUnits.Positions(reference,referenceCar,d.id,e.Unit.element.role);
                    e.RequiresMatchingSet=positions.Length==0;
                    if(e.RequiresMatchingSet){
                        candidate=InspectionBuild.Capture(reference,referenceCar,definitionId:d.id);
                        var pack=candidate.components.Single(p=>p.slot==d.category.ToString());
                        EngineElements.Ensure(candidate.build.spec,d,pack);
                        int index=Array.FindIndex(pack.condition.elements,member=>member.role==e.Unit.element.role);
                        if(index<0)throw new InvalidOperationException("This unit has no matching position in the proposed set.");
                        var target=pack.condition.elements[index];var incoming=e.Unit.element.Copy();
                        incoming.cylinder=target.cylinder;incoming.valve=target.valve;pack.condition.elements[index]=incoming;
                        EngineElementService.RefreshAggregate(pack.condition);
                        e.Position=null;
                    }else{
                    if(!positions.Contains(e.Position))e.Position=positions[0];var copy=UnitPreviewProfile(e.Unit);
                    var u=copy.looseEngineElements.Find(i=>i.element.id==e.Unit.element.id);if(u==null){u=new LooseEngineElement{definitionId=d.id,storageId=SharedInventory.Garage,unitPrice=e.Unit.unitPrice,element=e.Unit.element.Copy(),tireUnit=e.Unit.tireUnit,tire=e.Unit.tire?.Copy()};copy.looseEngineElements.Add(u);}
                    InventoryUnits.Install(copy,referenceCar.instanceId,u.element.id,e.Position);candidate=InspectionBuild.Capture(copy,copy.SelectedCar);
                    }
                }else candidate=InspectionBuild.Capture(reference,referenceCar,definitionId:d.id,instanceId:e.Part?.instanceId);
                var map=new Dictionary<PartCategory,string>(referenceMap){[d.category]=d.id};var structural=actual.catalog.Resolve(referenceCar.vehicleId,map,referenceCar.tune);
                bool mating=(d.category==PartCategory.Camshaft||d.category==PartCategory.Followers)&&structural.errors.Any(error=>error.Contains("cannot mate"));
                e.Compatibility=mating?"Requires supporting changes":"Compatible";
                e.Reason=string.Join("\n",structural.errors.Concat(structural.warnings));
                if(candidate.build.errors.Any(error=>!error.StartsWith("Failed ",StringComparison.Ordinal))){e.Reason=string.Join("\n",candidate.build.errors.Concat(structural.warnings));return;}
                e.Proposed=Inspect(candidate,cache);var a=Reference;var b=e.Proposed;
                e.Reason=string.Join("\n",structural.errors.Concat(structural.warnings).Concat(b.warnings.Select(w=>w.title+": "+w.explanation+" "+w.suggestion)).Distinct());
                e.Change[0]=b.peakHp-a.peakHp;e.Change[1]=b.peakTorque-a.peakTorque;e.Change[2]=b.vehicleMassKg-a.vehicleMassKg;
                var replaced=reference.componentsPart(d.category);double health=e.Unit!=null?e.Unit.element.health:e.Part!=null?(d.category==PartCategory.Tires?TireDynamics.LowestHealth(e.Part.condition):e.Part.condition.health):1;
                double oldHealth=replaced==null?0:d.category==PartCategory.Tires?TireDynamics.LowestHealth(replaced.condition):replaced.condition.health;
                if(e.RequiresMatchingSet)health=candidate.components.Single(p=>p.slot==d.category.ToString()).condition.health;
                if(e.Unit!=null&&replaced!=null&&!e.RequiresMatchingSet){if(e.Unit.tireUnit&&e.Position.StartsWith("tire:",StringComparison.Ordinal))oldHealth=TireDynamics.RemainingHealth(replaced.condition.tires[int.Parse(e.Position.Substring(5))]);else oldHealth=replaced.condition.elements.FirstOrDefault(element=>element.id==e.Position)?.health??oldHealth;}
                if(PartSemantics.Wears(d.category))e.Change[3]=(health-oldHealth)*100;
                var prior=replaced==null?null:actual.catalog.Find(replaced.definitionId);
                if(PartSemantics.TorqueRated(d.category)&&prior!=null)e.Change[4]=d.torqueLimitNm-prior.torqueLimitNm;
                if(PartSemantics.EngineStrengthRated(d.category)&&prior!=null)e.Change[5]=d.rpmLimit-prior.rpmLimit;
                e.Change[6]=b.radiatorWattsPerC-a.radiatorWattsPerC;
            }catch(Exception error){e.Reason="Performance estimate unavailable: "+error.Message;}
            finally{EvaluationMilliseconds+=clock.Elapsed.TotalMilliseconds;if(cache!=null){if(typedBinding!=null)cache.StoreTyped(typedBinding,e);else cache.Store(key,e);}}
        }
        internal bool Restore(NativePartEntry e)=>e.Ready||cache!=null&&(typedBinding!=null?cache.RestoreTyped(typedBinding,e):cache.Restore(NativePartPreviewCache.Key(referenceKey,e),e));
        PlayerProfile UnitPreviewProfile(LooseEngineElement selectedUnit)
        {
            var source=referenceCar;var car=new OwnedCar{instanceId=source.instanceId,chassisId=source.chassisId,engineAssemblyId=source.engineAssemblyId,garageJobId=source.garageJobId,installed=source.installed==null?null:new InstalledBuild{version=source.installed.version,instanceId=source.installed.instanceId,vehicleDefinitionId=source.installed.vehicleDefinitionId,slots=source.installed.slots==null?null:source.installed.slots.Select(s=>s==null?null:new SlotBinding{slot=s.slot,partInstanceId=s.partInstanceId}).ToList()},fuel=source.fuel?.Copy(),fuelInitialized=source.fuelInitialized,fluids=source.fluids?.Copy(),serviceHardwareVersion=source.serviceHardwareVersion,thermalUtcTicks=source.thermalUtcTicks,engineRebuild=source.engineRebuild==null?null:new EngineRebuildJob{active=source.engineRebuild.active},massVersion=source.massVersion,vehicleId=source.vehicleId,tune=source.tune?.Copy()};
            var installed=new HashSet<string>((car.installed?.slots??new List<SlotBinding>()).Where(s=>s!=null).Select(s=>s.partInstanceId),StringComparer.Ordinal);
            var parts=reference.parts.Where(p=>p!=null&&installed.Contains(p.instanceId)).Select(p=>p.Copy()).ToList();
            var unit=selectedUnit==null?null:new LooseEngineElement{definitionId=selectedUnit.definitionId,storageId=selectedUnit.storageId,unitPrice=selectedUnit.unitPrice,element=selectedUnit.element?.Copy(),tireUnit=selectedUnit.tireUnit};
            return new PlayerProfile{catalog=actual.catalog,selectedOwnedVehicleId=car.instanceId,selectedVehicleId=car.vehicleId,cars=new List<OwnedCar>{car},parts=parts,looseEngineElements=unit==null?new List<LooseEngineElement>():new List<LooseEngineElement>{unit},inventory=new InventoryState(),runCheckpoints=reference.runCheckpoints==null?null:new List<RunCheckpoint>(reference.runCheckpoints)};
        }
    }
    internal static class NativePartReference
    {
        internal static PartInstance componentsPart(this PlayerProfile p,PartCategory c){var slot=p.SelectedCar.installed.slots.Find(s=>s.slot==c.ToString());return slot==null?null:p.parts.Find(i=>i.instanceId==slot.partInstanceId);}
    }
}
