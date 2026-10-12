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
        internal string Search="",Compatibility="Compatible",Availability="All",Condition="All",Tuning="All",Sort="Name",Container="All";
        internal bool Descending;
        internal HashSet<PartCategory> Categories=new HashSet<PartCategory>();
        internal string[] Direction=Enumerable.Repeat("Any",7).ToArray();
        internal double?[] Minimum=new double?[7];
        internal NativePartQuery Copy(){var c=(NativePartQuery)MemberwiseClone();c.Categories=new HashSet<PartCategory>(Categories);c.Direction=(string[])Direction.Clone();c.Minimum=(double?[])Minimum.Clone();return c;}
        internal bool Matches(NativePartEntry e,int cash)
        {
            if(Categories.Count>0&&!Categories.Contains(e.Definition.category))return false;
            if(Container!="All"&&e.Location!=Container)return false;
            if(!string.IsNullOrWhiteSpace(Search)&&!(e.Definition.name+" "+e.Definition.brand+" "+e.Definition.category+" "+e.AssetId+" "+e.Location).ToLowerInvariant().Contains(Search.Trim().ToLowerInvariant()))return false;
            if(Compatibility!="All"&&e.Compatibility!=Compatibility)return false;
            if(Availability=="Owned"&&!e.Owned||Availability=="Affordable"&&(!e.Price.HasValue||e.Price>cash)||Availability=="Unaffordable"&&(!e.Price.HasValue||e.Price<=cash))return false;
            if(Condition=="New"&&e.Source!="New"||Condition=="Used"&&e.Source=="New")return false;
            bool tunable=e.Definition.tuning!=PartTuningOptions.None||e.Definition.camTimingAdjustable;
            if(Tuning=="Adjustable"&&!tunable||Tuning=="Fixed"&&tunable)return false;
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
            if(Sort=="Name")result=string.Compare(a.Definition.name,b.Definition.name,StringComparison.OrdinalIgnoreCase);
            else {if(av.HasValue!=bv.HasValue)return av.HasValue?-1:1;result=av.HasValue?av.Value.CompareTo(bv.Value):0;}
            if(Descending)result=-result;return result!=0?result:string.CompareOrdinal(a.Id,b.Id);
        }
    }
    internal sealed class NativePartEntry
    {
        internal string Id,AssetId,Source,Location,Compatibility="Calculating…",Reason="",Position;
        internal PartDefinition Definition;
        internal PartInstance Part;
        internal LooseEngineElement Unit;
        internal bool Owned,Ready;
        internal int? Price;
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
        readonly PlayerProfile actual,reference;
        readonly OwnedCar actualCar,referenceCar;
        readonly Dictionary<PartCategory,string> referenceMap;
        readonly VehicleSpec referenceSpec;
        internal NativePartBrowser(PlayerProfile profile,IEnumerable<PartCartLine> lines,bool planned,string page,Func<DealerStock,int> usedPrice,Func<string,bool,int> salePrice)
        {
            actual=profile;actualCar=profile.SelectedCar;plannedReference=planned;
            if(actualCar==null)return;
            Current=string.IsNullOrEmpty(actualCar.engineAssemblyId)?null:Inspect(InspectionBuild.Capture(profile,actualCar));reference=profile;
            var cart=lines.Select(l=>l.Copy()).ToArray();
            if(cart.Length>0)try {PartCart.Total(profile,actualCar.instanceId,cart);var copy=PartCart.PreviewProfile(profile,actualCar,cart);Plan=Inspect(InspectionBuild.Capture(copy,copy.SelectedCar));if(planned)reference=copy;}catch(Exception e){PlanError=e.Message;}
            referenceCar=reference.SelectedCar;referenceMap=MechanicsOwnership.Definitions(reference,referenceCar);Reference=planned&&PlanError!=null||string.IsNullOrEmpty(referenceCar.engineAssemblyId)?null:Inspect(InspectionBuild.Capture(reference,referenceCar));
            referenceSpec=actual.catalog.Resolve(referenceCar.vehicleId,referenceMap,referenceCar.tune).spec;
            bool Owns(string id)=>profile.parts.Any(p=>p.definitionId==id)||profile.looseEngineElements.Any(p=>p.definitionId==id);
            if(page=="Installed")foreach(var slot in actualCar.installed.slots){var p=profile.parts.Find(i=>i.instanceId==slot.partInstanceId);if(p==null)continue;Add(p.instanceId,p.definitionId,"Installed",p,null,true,p.ownerId,null);}
            if(page=="Inventory"){
                foreach(var p in profile.parts.Where(p=>!actualCar.installed.slots.Any(s=>s.partInstanceId==p.instanceId)))Add(p.instanceId,p.definitionId,"Owned",p,null,true,p.location==PartLocation.Inventory?SharedInventory.StorageOf(p.storageId):p.ownerId,SafePrice(()=>salePrice(p.instanceId,false),p.location==PartLocation.Inventory));
                foreach(var u in profile.looseEngineElements)Add(u.element.id,u.definitionId,"Owned",null,u,true,SharedInventory.StorageOf(u.storageId),SafePrice(()=>salePrice(u.element.id,true),true));
            }
            if(page=="Shop"){
                foreach(var d in profile.catalog.parts)Add("new:"+d.id,d.id,"New",null,null,Owns(d.id),"New",InventoryPricing.NewSet(d));
                foreach(var d in profile.inventory.dealer)Add("used:"+d.Id,d.DefinitionId,"Used",d.part,d.unit,Owns(d.DefinitionId),"Dealer",usedPrice(d));
            }
            // Foreign interfaces need no engine curve. Classify them before building
            // the view, keeping expensive selected-build previews on a bounded budget.
            if(Reference!=null)foreach(var entry in Entries.Where(e=>!actual.catalog.Fits(referenceSpec,e.Definition)))Evaluate(entry);
        }
        static int? SafePrice(Func<int> get,bool available){if(!available)return null;try{return get();}catch(Exception){return null;}}
        void Add(string id,string definition,string source,PartInstance part,LooseEngineElement unit,bool owned,string location,int? price)
        {var d=actual.catalog.Find(definition);if(d!=null)Entries.Add(new NativePartEntry{Id=id,AssetId=part?.instanceId??unit?.element.id,Definition=d,Source=source,Part=part,Unit=unit,Owned=owned,Location=location,Price=price});}
        internal static NativePartSection Section(PartCategory c)
        {
            if(c==PartCategory.Induction||c==PartCategory.Exhaust)return NativePartSection.Induction;
            if(c==PartCategory.Wheels||c==PartCategory.Tires||c==PartCategory.Brakes)return NativePartSection.WheelsBrakes;
            if(c==PartCategory.Body||c==PartCategory.Interior||c==PartCategory.Accessories)return NativePartSection.BodyChassis;
            if(c==PartCategory.Clutch||c==PartCategory.Flywheel||c==PartCategory.Gearbox||c==PartCategory.Differential||c==PartCategory.Driveshaft||MechanicalCatalog.DriveBranch(c))return NativePartSection.Drivetrain;
            return NativePartSection.Engine;
        }
        internal static CarInspection Inspect(InspectionBuild b)=>CarInspection.CalculatePrepared(b,CarInspection.PrepareCar(b),false);
        internal string[] Positions(NativePartEntry e)=>e.Unit==null?new string[0]:InventoryUnits.Positions(reference,referenceCar,e.Definition.id,e.Unit.element.role);
        internal string PositionLabel(NativePartEntry e,string position)=>InventoryUnits.PositionLabel(reference,referenceCar,e.Definition.id,position);
        internal NativePartEntry PreviewNewUnit(NativePartEntry offer,string role,IEnumerable<string> selection)
        {
            var e=new NativePartEntry{Id=offer.Id,Source="New",Definition=offer.Definition,Owned=offer.Owned,Price=InventoryPricing.NewUnit(offer.Definition,InventoryUnits.Count(actual,actualCar,offer.Definition))};
            e.Unit=new LooseEngineElement{definitionId=e.Definition.id,element=new EngineElement{id="native-offer-unit",role=role,cylinder=-1,valve=-1},tireUnit=e.Definition.category==PartCategory.Tires,tire=e.Definition.category==PartCategory.Tires?new TireCondition():null};
            var available=InventoryUnits.Positions(reference,referenceCar,e.Definition.id,role);var selected=selection.Where(available.Contains).Distinct().ToArray();
            if(selected.Length==0&&available.Length>0)selected=new[]{available[0]};e.Position=selected.FirstOrDefault();Evaluate(e);
            if(selected.Length>1&&e.Proposed!=null){
                var copy=PartCart.PreviewProfile(reference,referenceCar,new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=e.Definition.id,role=role,quantity=selected.Length,install=true,positions=selected.ToList()}});
                e.Proposed=Inspect(InspectionBuild.Capture(copy,copy.SelectedCar));
            }
            return e;
        }
        internal void Evaluate(NativePartEntry e)
        {
            if(e.Ready)return;e.Ready=true;var d=e.Definition;
            if(plannedReference&&PlanError!=null){e.Compatibility="Unavailable";e.Reason=PlanError;return;}
            if(!actual.catalog.Fits(referenceSpec,d)){e.Compatibility="Incompatible";e.Reason="Mounting, bore, fuel type or controller interface does not fit this build.";return;}
            if(Reference==null){e.Compatibility="Compatible";e.Reason="Attach an engine to compare performance.";return;}
            try{
                InspectionBuild candidate;
                if(e.Unit!=null){
                    var positions=InventoryUnits.Positions(reference,referenceCar,d.id,e.Unit.element.role);if(positions.Length==0){e.Compatibility="Compatible";e.Reason="Individual replacement needs the same installed specification. Fit the matching complete set before replacing this unit.";return;}
                    if(!positions.Contains(e.Position))e.Position=positions[0];var copy=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(reference));copy.catalog=actual.catalog;SharedInventory.Ensure(copy);
                    var u=copy.looseEngineElements.Find(i=>i.element.id==e.Unit.element.id);if(u==null){u=new LooseEngineElement{definitionId=d.id,storageId=SharedInventory.Garage,unitPrice=e.Unit.unitPrice,element=e.Unit.element.Copy(),tireUnit=e.Unit.tireUnit,tire=e.Unit.tire?.Copy()};copy.looseEngineElements.Add(u);}
                    InventoryUnits.Install(copy,referenceCar.instanceId,u.element.id,e.Position);candidate=InspectionBuild.Capture(copy,copy.SelectedCar);
                }else candidate=InspectionBuild.Capture(reference,referenceCar,definitionId:d.id,instanceId:e.Part?.instanceId);
                var map=new Dictionary<PartCategory,string>(referenceMap){[d.category]=d.id};var structural=actual.catalog.Resolve(referenceCar.vehicleId,map,referenceCar.tune);
                bool mating=(d.category==PartCategory.Camshaft||d.category==PartCategory.Followers)&&structural.errors.Any(error=>error.Contains("cannot mate"));
                e.Compatibility=mating?"Requires supporting changes":"Compatible";
                e.Reason=string.Join("\n",structural.errors.Concat(structural.warnings));
                if(candidate.build.errors.Any(error=>!error.StartsWith("Failed ",StringComparison.Ordinal))){e.Reason=string.Join("\n",candidate.build.errors.Concat(structural.warnings));return;}
                e.Proposed=Inspect(candidate);var a=Reference;var b=e.Proposed;
                e.Reason=string.Join("\n",structural.errors.Concat(structural.warnings).Concat(b.warnings.Select(w=>w.title+": "+w.explanation+" "+w.suggestion)).Distinct());
                e.Change[0]=b.peakHp-a.peakHp;e.Change[1]=b.peakTorque-a.peakTorque;e.Change[2]=b.vehicleMassKg-a.vehicleMassKg;
                var replaced=reference.componentsPart(d.category);double health=e.Unit!=null?e.Unit.element.health:e.Part!=null?e.Part.condition.health:1;
                double oldHealth=replaced?.condition.health??0;
                if(e.Unit!=null&&replaced!=null){if(e.Unit.tireUnit&&e.Position.StartsWith("tire:",StringComparison.Ordinal))oldHealth=replaced.condition.tires[int.Parse(e.Position.Substring(5))].health;else oldHealth=replaced.condition.elements.FirstOrDefault(element=>element.id==e.Position)?.health??oldHealth;}
                if(PartSemantics.Wears(d.category))e.Change[3]=(health-oldHealth)*100;
                var prior=replaced==null?null:actual.catalog.Find(replaced.definitionId);
                if(PartSemantics.TorqueRated(d.category)&&prior!=null)e.Change[4]=d.torqueLimitNm-prior.torqueLimitNm;
                if(PartSemantics.EngineInternal(d.category)&&prior!=null)e.Change[5]=d.rpmLimit-prior.rpmLimit;
                e.Change[6]=b.radiatorWattsPerC-a.radiatorWattsPerC;
            }catch(Exception error){e.Compatibility="Compatible";e.Reason="Performance estimate unavailable: "+error.Message;}
        }
    }
    internal static class NativePartReference
    {
        internal static PartInstance componentsPart(this PlayerProfile p,PartCategory c){var slot=p.SelectedCar.installed.slots.Find(s=>s.slot==c.ToString());return slot==null?null:p.parts.Find(i=>i.instanceId==slot.partInstanceId);}
    }
}
