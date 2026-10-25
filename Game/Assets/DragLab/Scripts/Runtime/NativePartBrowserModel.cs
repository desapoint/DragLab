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
        internal static readonly string[] SectionNames={"Engine","Induction","Drivetrain","Wheels & brakes","Body & chassis"};
        internal static readonly string[] MetricNames={"HP change","Torque change","Weight change","Condition change","Torque strength","RPM strength","Cooling change"};
        internal readonly List<NativePartEntry> Entries=new List<NativePartEntry>();
        internal CarInspection Current,Reference,Plan;
        internal string PlanError;
        readonly bool plannedReference;
        readonly NativePartPreviewCache cache;readonly string referenceKey;
        internal int EvaluationCount {get;private set;}
        internal double EvaluationMilliseconds {get;private set;}
        internal readonly bool UsedReadyProductGroups;
        internal readonly double ProductGroupLookupMilliseconds;
        internal readonly double ShopPopulationMilliseconds;
        internal readonly double CurrentInspectionMilliseconds;
        internal readonly double ShopLedgerSetupMilliseconds;
        internal readonly double ShopProductEnumerationMilliseconds;
        internal readonly double ShopClassificationMilliseconds;
        internal readonly double ShopRepresentativeMilliseconds;
        internal readonly double ShopAliasMilliseconds;
        internal readonly double ShopSupplyCountMilliseconds;
        internal readonly double ShopQuoteAndHoldMilliseconds;
        internal readonly double ShopEntryCreationMilliseconds;
        internal readonly double ShopUsedOfferMilliseconds;
        internal readonly double ShopStockBindingMilliseconds;
        internal readonly bool UsedReadyStockBinding;
        readonly PlayerProfile actual,reference;
        readonly OwnedCar actualCar,referenceCar;
        readonly Dictionary<PartCategory,string> referenceMap;
        readonly VehicleSpec referenceSpec;
        static double ElapsedMilliseconds(long start)=>(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        internal NativePartBrowser(PlayerProfile profile,IEnumerable<PartCartLine> lines,bool planned,string page,Func<DealerStock,int> usedPrice,Func<string,bool,int> salePrice,NativePartPreviewCache cache=null)
        {
            actual=profile;actualCar=profile.SelectedCar;plannedReference=planned;this.cache=cache;
            if(actualCar==null)return;
            long currentInspectionStart=System.Diagnostics.Stopwatch.GetTimestamp();cache?.Bind(profile,actualCar);
            Current=string.IsNullOrEmpty(actualCar.engineAssemblyId)?null:cache==null?Inspect(InspectionBuild.Capture(profile,actualCar)):cache.Inspection("current",()=>Inspect(InspectionBuild.Capture(profile,actualCar),cache));reference=profile;
            CurrentInspectionMilliseconds=ElapsedMilliseconds(currentInspectionStart);
            var cart=lines.Select(l=>l.Copy()).ToArray();
            string incoming=cart.Length==0?"":cache==null?JsonUtility.ToJson(profile):cache.Context+"/"+string.Join("/",cart.Select(l=>JsonUtility.ToJson(profile.parts.Find(i=>i.instanceId==l.assetId))+JsonUtility.ToJson(profile.looseEngineElements.Find(i=>i.element.id==l.assetId))+JsonUtility.ToJson(profile.inventory.dealer.Find(i=>i.Id==l.assetId))+JsonUtility.ToJson(profile.catalog.Find(l.definitionId))));
            string planKey="plan/"+NativePartPreviewCache.Fingerprint(JsonUtility.ToJson(new PartCart.Lines{entries=cart.ToList()})+incoming);
            if(cart.Length>0)try {PartCart.Total(profile,actualCar.instanceId,cart);var copy=cache==null?PartCart.PreviewProfile(profile,actualCar,cart):cache.Plan(planKey,()=>PartCart.PreviewProfile(profile,actualCar,cart));Plan=cache==null?Inspect(InspectionBuild.Capture(copy,copy.SelectedCar)):cache.Inspection(planKey,()=>Inspect(InspectionBuild.Capture(copy,copy.SelectedCar),cache));if(planned)reference=copy;}catch(Exception e){PlanError=e.Message;}
            referenceKey=planned&&Plan!=null?planKey:"current";
            referenceCar=reference.SelectedCar;referenceMap=MechanicsOwnership.Definitions(reference,referenceCar);Reference=planned&&PlanError!=null||string.IsNullOrEmpty(referenceCar.engineAssemblyId)?null:ReferenceEquals(reference,profile)?Current:Plan;
            referenceSpec=actual.catalog.Resolve(referenceCar.vehicleId,referenceMap,referenceCar.tune).spec;
            var ownedDefinitions=new HashSet<string>(profile.looseEngineElements.Select(u=>u.definitionId),StringComparer.Ordinal);
            foreach(var part in profile.parts){if(part.condition.tires.Length==4)for(int i=0;i<4;i++)ownedDefinitions.Add(TireFitments.DefinitionId(part,i));else if(WheelFitments.HasOverrides(part))for(int i=0;i<4;i++)ownedDefinitions.Add(WheelFitments.DefinitionId(part,i));else ownedDefinitions.Add(part.definitionId);}
            bool Owns(string id)=>ownedDefinitions.Contains(id);
            if(page=="Installed")foreach(var slot in actualCar.installed.slots){var p=profile.parts.Find(i=>i.instanceId==slot.partInstanceId);if(p==null)continue;Add(p.instanceId,p.definitionId,"Installed",p,null,true,p.ownerId,null);}
            if(page=="Inventory"){
                foreach(var p in profile.parts.Where(p=>!actualCar.installed.slots.Any(s=>s.partInstanceId==p.instanceId)))Add(p.instanceId,p.definitionId,"Owned",p,null,true,p.location==PartLocation.Inventory?SharedInventory.StorageOf(p.storageId):p.ownerId,SafePrice(()=>salePrice(p.instanceId,false),p.location==PartLocation.Inventory));
                foreach(var u in profile.looseEngineElements)Add(u.element.id,u.definitionId,"Owned",null,u,true,SharedInventory.StorageOf(u.storageId),SafePrice(()=>salePrice(u.element.id,true),true));
            }
            long shopPopulationStart=0;bool shopPage=page=="Shop";
            if(shopPage){
                shopPopulationStart=System.Diagnostics.Stopwatch.GetTimestamp();
                long groupLookupStart=System.Diagnostics.Stopwatch.GetTimestamp();
                bool usedReadyGroups=MarketCatalogLayout.TryReadyShopProductGroups(profile.catalog,out var readyGroups,out var readyGroupById);
                ProductGroupLookupMilliseconds=ElapsedMilliseconds(groupLookupStart);
                UsedReadyProductGroups=usedReadyGroups;
                long ledgerSetupStart=System.Diagnostics.Stopwatch.GetTimestamp();
                var fitted=new HashSet<string>(referenceMap.Values);
                var supplyCount=usedReadyGroups?PartsMarket.CounterIndexed(profile,actualCar):PartsMarket.Counter(profile,actualCar);
                var shelf=new PartsMarket.Ledger(profile,profile.inventory.usingCartReservation);
                ShopLedgerSetupMilliseconds=ElapsedMilliseconds(ledgerSetupStart);
                long productEnumerationStart=System.Diagnostics.Stopwatch.GetTimestamp();
                string selectedModelPrefix=referenceCar.vehicleId+"/";
                if(usedReadyGroups){
                    double representativeMs=0,aliasMs=0,supplyCountMs=0,quoteAndHoldMs=0,entryCreationMs=0,usedOfferMs=0;
                    long bindingStart=System.Diagnostics.Stopwatch.GetTimestamp();
                    UsedReadyStockBinding=shelf.BindReadyProductGroups(readyGroups,readyGroupById);
                    ShopStockBindingMilliseconds=ElapsedMilliseconds(bindingStart);
                    foreach(var product in readyGroups){
                        long phaseStart=System.Diagnostics.Stopwatch.GetTimestamp();
                        PartDefinition d=null;int best=int.MaxValue;bool owned=false;
                        foreach(var id in product.DefinitionIds){
                            var candidate=profile.catalog.Find(id);if(candidate==null)continue;
                            owned|=Owns(id);
                            int rank=fitted.Contains(id)?0:id.StartsWith(selectedModelPrefix,StringComparison.Ordinal)?1:2;
                            if(rank<best||rank==best&&StringComparer.Ordinal.Compare(id,d?.id)<0){d=candidate;best=rank;}
                        }
                        representativeMs+=ElapsedMilliseconds(phaseStart);
                        if(d==null)continue;
                        phaseStart=System.Diagnostics.Stopwatch.GetTimestamp();int units=supplyCount(d);supplyCountMs+=ElapsedMilliseconds(phaseStart);
                        phaseStart=System.Diagnostics.Stopwatch.GetTimestamp();var supply=shelf.Quote(d,units,d.price);int available=Math.Max(0,shelf.Available(d)-(profile.inventory.usingCartReservation?CartReservations.Held(profile,d):0));quoteAndHoldMs+=ElapsedMilliseconds(phaseStart);
                        phaseStart=System.Diagnostics.Stopwatch.GetTimestamp();
                        Add("new:"+d.id,d.id,"New",null,null,owned,"New",supply.cost);
                        Entries[Entries.Count-1].StockUnits=available;Entries[Entries.Count-1].RequiresExpress=supply.expressUnits>0;
                        entryCreationMs+=ElapsedMilliseconds(phaseStart);
                        phaseStart=System.Diagnostics.Stopwatch.GetTimestamp();Entries[Entries.Count-1].SearchExtra=string.Join(" ",product.DefinitionIds);aliasMs+=ElapsedMilliseconds(phaseStart);
                    }
                    ShopRepresentativeMilliseconds=representativeMs;ShopAliasMilliseconds=aliasMs;
                    ShopSupplyCountMilliseconds=supplyCountMs;ShopQuoteAndHoldMilliseconds=quoteAndHoldMs;
                    ShopEntryCreationMilliseconds=entryCreationMs;
                    long usedOfferStart=System.Diagnostics.Stopwatch.GetTimestamp();
                    foreach(var d in profile.inventory.dealer.Where(d=>d.IsPart||d.IsUnit))Add("used:"+d.Id,d.DefinitionId,"Used",d.IsPart?d.part:null,d.IsUnit?d.unit:null,Owns(d.DefinitionId),"Dealer",usedPrice(d));
                    ShopUsedOfferMilliseconds=ElapsedMilliseconds(usedOfferStart);
                }else{
                    foreach(var product in profile.catalog.parts.GroupBy(NewProductKey,StringComparer.Ordinal)){
                        var d=product.OrderBy(p=>fitted.Contains(p.id)?0:p.id.StartsWith(selectedModelPrefix,StringComparison.Ordinal)?1:2).ThenBy(p=>p.id,StringComparer.Ordinal).First();
                        var supply=shelf.Quote(d,supplyCount(d),d.price);int available=Math.Max(0,shelf.Available(d)-(profile.inventory.usingCartReservation?CartReservations.Held(profile,d):0));
                        Add("new:"+d.id,d.id,"New",null,null,product.Any(p=>Owns(p.id)),"New",supply.cost);
                        Entries[Entries.Count-1].StockUnits=available;Entries[Entries.Count-1].RequiresExpress=supply.expressUnits>0;
                        Entries[Entries.Count-1].SearchExtra=string.Join(" ",product.Select(p=>p.id));
                    }
                    long usedOfferStart=System.Diagnostics.Stopwatch.GetTimestamp();
                    foreach(var d in profile.inventory.dealer.Where(d=>d.IsPart||d.IsUnit))Add("used:"+d.Id,d.DefinitionId,"Used",d.IsPart?d.part:null,d.IsUnit?d.unit:null,Owns(d.DefinitionId),"Dealer",usedPrice(d));
                    ShopUsedOfferMilliseconds=ElapsedMilliseconds(usedOfferStart);
                }
                ShopProductEnumerationMilliseconds=ElapsedMilliseconds(productEnumerationStart);
            }
            long shopClassificationStart=shopPage?System.Diagnostics.Stopwatch.GetTimestamp():0;
            foreach(var entry in Entries)Classify(entry);
            if(shopPage)ShopClassificationMilliseconds=ElapsedMilliseconds(shopClassificationStart);
            if(shopPage)ShopPopulationMilliseconds=ElapsedMilliseconds(shopPopulationStart);
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
            if(e.Ready)return;string key=cache==null?null:NativePartPreviewCache.Key(referenceKey,e);if(cache!=null&&cache.Restore(key,e))return;
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
                    if(!positions.Contains(e.Position))e.Position=positions[0];var copy=UnitPreviewProfile();
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
            finally{EvaluationMilliseconds+=clock.Elapsed.TotalMilliseconds;if(cache!=null)cache.Store(key,e);}
        }
        internal bool Restore(NativePartEntry e)=>e.Ready||cache!=null&&cache.Restore(NativePartPreviewCache.Key(referenceKey,e),e);
        PlayerProfile UnitPreviewProfile()
        {
            var car=JsonUtility.FromJson<OwnedCar>(JsonUtility.ToJson(referenceCar));var installed=new HashSet<string>(car.installed.slots.Select(s=>s.partInstanceId));
            return new PlayerProfile{catalog=actual.catalog,selectedOwnedVehicleId=car.instanceId,selectedVehicleId=car.vehicleId,cars=new List<OwnedCar>{car},parts=reference.parts.Where(p=>installed.Contains(p.instanceId)).Select(p=>p.Copy()).ToList(),runCheckpoints=reference.runCheckpoints.ToList(),cash=reference.cash};
        }
    }
    internal static class NativePartReference
    {
        internal static PartInstance componentsPart(this PlayerProfile p,PartCategory c){var slot=p.SelectedCar.installed.slots.Find(s=>s.slot==c.ToString());return slot==null?null:p.parts.Find(i=>i.instanceId==slot.partInstanceId);}
    }
}
