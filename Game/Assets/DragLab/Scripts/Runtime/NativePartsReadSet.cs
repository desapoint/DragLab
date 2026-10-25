using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    // Owner-thread values only. These types are deliberately not serializable and
    // must not be passed to workers.
    internal sealed class NativePartsOwnerContext
    {
        internal readonly string Slot,CanonicalPath;
        internal readonly long Epoch;
        internal NativePartsOwnerContext(string slot,string canonicalPath,long epoch)
        {Slot=slot;CanonicalPath=canonicalPath;Epoch=epoch;}
        internal static NativePartsOwnerContext Capture()
        {ProfileStore.CaptureSelectedContext(out var slot,out var path,out var epoch);return new NativePartsOwnerContext(slot,path,epoch);}
        internal static void AssertMainThread(){ProfileStore.CaptureSelectedContext(out _,out _,out _);}
        internal bool Same(NativePartsOwnerContext other)=>other!=null&&Slot==other.Slot&&
            string.Equals(CanonicalPath,other.CanonicalPath,Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)&&Epoch==other.Epoch;
        internal bool IsCurrent(){var current=Capture();return Same(current);}
    }
    internal sealed class NativePartsPriceFact
    {
        internal readonly string Source,AssetKey;
        internal readonly int? Price;
        internal NativePartsPriceFact(string source,string assetKey,int? price)
        {Source=source;AssetKey=assetKey;Price=price;}
    }
    internal sealed class NativePartsBaseReadInputs
    {
        internal readonly PlayerProfile Profile;
        internal readonly string Page;
        internal readonly bool Planned;
        internal readonly PartCartLine[] Lines;
        internal readonly UnitPreferences Units;
        internal readonly NativePartsOwnerContext Owner;
        internal readonly NativePartsPriceFact[] Prices;
        internal double CaptureMilliseconds{get;}
        internal NativePartsBaseReadInputs(PlayerProfile profile,string page,bool planned,
            IEnumerable<PartCartLine> typedLines,UnitPreferences units,NativePartsOwnerContext ownerContext,
            IEnumerable<NativePartsPriceFact> prices)
        {
            long captureStart=Stopwatch.GetTimestamp();
            NativePartsOwnerContext.AssertMainThread();
            ValidatePage(page);
            Profile=profile;Page=page;Planned=planned;
            Lines=(typedLines??Enumerable.Empty<PartCartLine>()).Select(CopyLineExact).ToArray();
            Units=units==null?null:new UnitPreferences{version=units.version,speed=units.speed,pressure=units.pressure,torque=units.torque};
            Owner=ownerContext;Prices=(prices??Enumerable.Empty<NativePartsPriceFact>()).Select(x=>x==null?null:new NativePartsPriceFact(x.Source,x.AssetKey,x.Price)).ToArray();
            CaptureMilliseconds=(Stopwatch.GetTimestamp()-captureStart)*1000.0/Stopwatch.Frequency;
        }
        internal static void ValidatePage(string page)
        {
            if(page!="Shop"&&page!="Inventory"&&page!="Installed")throw new ArgumentException("Native parts read facts support only Shop, Inventory, or Installed pages.",nameof(page));
        }
        internal static PartCartLine CopyLineExact(PartCartLine l)=>l==null?null:new PartCartLine{definitionId=l.definitionId,quantity=l.quantity,install=l.install,preparedBlock=l.preparedBlock,preparationOwner=l.preparationOwner,action=l.action,assetId=l.assetId,role=l.role,destinationId=l.destinationId,destination=l.destination,positions=l.positions==null?null:new List<string>(l.positions)};
    }
    internal sealed class NativePartsBaseReadSet
    {
        sealed class PartFact {internal PartInstance source,snapshot;}
        sealed class UnitFact {internal LooseEngineElement source;internal EngineElement elementSource;internal string definition,storage;internal int price;internal bool tireUnit;internal EngineElement element;}
        sealed class DealerFact {internal DealerStock source;internal PartInstance partSource;internal bool isUnit;internal PartInstance part;internal UnitFact unit;internal int paid;}
        sealed class RebuildTargetFact {internal EngineRebuildTarget source;internal string partId,definitionId;internal bool replace;}
        readonly PlayerProfile profile;readonly MechanicalCatalog catalog;readonly OwnedCar selectedCar;
        readonly List<OwnedCar> carsCollection;
        readonly string page,selectedId,selectedModel,engineAssemblyId,garageJobId,selectedOwnedVehicleId,selectedVehicleId;readonly bool planned,fuelInitialized;readonly long revision,storageCommit;
        readonly NativePartsOwnerContext owner;readonly UnitPreferences units;
        readonly PartCartLine[] lines;readonly NativePartsPriceFact[] prices;
        readonly Tune tune;readonly FuelState fuel;readonly FluidState fluids;readonly InstalledBuild installed;
        readonly Tune tuneSource;readonly FuelState fuelSource;readonly FluidState fluidsSource;readonly InstalledBuild installedSource;
        readonly EngineRebuildJob rebuild;readonly RebuildTargetFact[] rebuildTargets;
        readonly EngineRebuildJob rebuildSource;readonly List<EngineRebuildTarget> rebuildTargetSources;
        readonly List<PartFact> relevantParts=new List<PartFact>();
        readonly List<string> ownedIds=new List<string>(); readonly List<UnitFact> relevantUnits=new List<UnitFact>();
        readonly List<DealerFact> dealer=new List<DealerFact>();
        readonly List<PartFact> selectedInstalledParts=new List<PartFact>();
        readonly List<PartInstance> ownedPartSources=new List<PartInstance>();readonly List<LooseEngineElement> ownedUnitSources=new List<LooseEngineElement>();
        readonly int cash,reputation;readonly int stockVersion,seed,generation,guarantee,marketCount;
        readonly InventoryState inventoryIdentity;readonly PartsStockState marketIdentity;readonly NewPartStock[] stockSources;
        readonly List<PartInstance> partsCollection;readonly List<LooseEngineElement> looseCollection;
        readonly List<DealerStock> dealerCollection;readonly List<NewPartStock> stockCollection;
        readonly MarketCatalogLayout.ReadyContentToken catalogToken;
        readonly VehicleStockState vehicleStockIdentity;readonly int vehicleStockVersion,vehicleSeed,vehicleGeneration,vehiclePurchases,vehicleRuns,vehicleKnowledge;
        readonly bool usingCartReservation;
        readonly NewPartStock[] stocks;readonly CartReservationState reservation;
        readonly CartReservationState reservationIdentity;readonly List<PartCartLine> reservationLinesIdentity;readonly List<ReservedPartSupply> reservationSupplyIdentity;
        int partIndex,unitIndex,dealerIndex,ownedPartIndex,ownedUnitIndex,stockIndex;
        readonly int totalItems;
        readonly int partCountAtStart,looseCountAtStart,dealerCountAtStart,marketCountAtStart;
        bool sourceShapeChanged;
        internal bool RequiresTypedPreview=>lines.Length>0;
        internal int CapturedItemCount=>partIndex+unitIndex+dealerIndex+ownedPartIndex+ownedUnitIndex+stockIndex;
        internal double CaptureMilliseconds{get;private set;}
        internal double LargestCaptureStepMilliseconds{get;private set;}
        internal double FinalCompareMilliseconds{get;private set;}
        bool Complete=>CapturedItemCount>=totalItems;
        static void AssertMainThread()=>NativePartsOwnerContext.AssertMainThread();
        static double ElapsedMs(long start)=>(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;

        NativePartsBaseReadSet(NativePartsBaseReadInputs input)
        {
            profile=input.Profile;catalog=profile?.catalog;page=input.Page??"";planned=input.Planned;owner=input.Owner;
            revision=profile?.revision??0;storageCommit=profile?.storageCommit??0;cash=page=="Shop"?(profile?.cash??0):0;reputation=page=="Shop"?(profile?.reputation??0):0;selectedOwnedVehicleId=profile?.selectedOwnedVehicleId;selectedVehicleId=profile?.selectedVehicleId;
            carsCollection=profile?.cars;selectedCar=profile==null||carsCollection==null?null:profile.SelectedCar;selectedId=selectedCar?.instanceId;selectedModel=selectedCar?.vehicleId;
            engineAssemblyId=selectedCar?.engineAssemblyId;garageJobId=selectedCar?.garageJobId;fuelInitialized=selectedCar!=null&&selectedCar.fuelInitialized;
            rebuildSource=selectedCar?.engineRebuild;rebuildTargetSources=rebuildSource?.targets;
            rebuild=selectedCar?.engineRebuild==null?null:new EngineRebuildJob{active=selectedCar.engineRebuild.active,id=selectedCar.engineRebuild.id,engineId=selectedCar.engineRebuild.engineId,startedUtcTicks=selectedCar.engineRebuild.startedUtcTicks,readyUtcTicks=selectedCar.engineRebuild.readyUtcTicks,paidPrice=selectedCar.engineRebuild.paidPrice};
            rebuildTargets=selectedCar?.engineRebuild?.targets==null?null:selectedCar.engineRebuild.targets.Select(t=>t==null?null:new RebuildTargetFact{source=t,partId=t.partId,definitionId=t.definitionId,replace=t.replace}).ToArray();
            inventoryIdentity=profile?.inventory;marketIdentity=profile?.inventory?.market;partsCollection=profile?.parts;looseCollection=profile?.looseEngineElements;dealerCollection=profile?.inventory?.dealer;stockCollection=profile?.inventory?.market?.parts;
            MarketCatalogLayout.TryCaptureReadyContentToken(catalog,out catalogToken);
            vehicleStockIdentity=profile?.vehicleStock;if(vehicleStockIdentity!=null){vehicleStockVersion=vehicleStockIdentity.version;vehicleSeed=vehicleStockIdentity.seed;vehicleGeneration=vehicleStockIdentity.generation;vehiclePurchases=vehicleStockIdentity.purchases;vehicleRuns=vehicleStockIdentity.completedRuns;vehicleKnowledge=vehicleStockIdentity.knowledgeRevision;}
            usingCartReservation=profile?.inventory?.usingCartReservation??false;
            tuneSource=selectedCar?.tune;fuelSource=selectedCar?.fuel;fluidsSource=selectedCar?.fluids;installedSource=selectedCar?.installed;
            tune=selectedCar?.tune?.Copy();fuel=selectedCar?.fuel?.Copy();fluids=selectedCar?.fluids?.Copy();installed=CopyInstalled(selectedCar?.installed);
            units=input.Units==null?null:new UnitPreferences{version=input.Units.version,speed=input.Units.speed,pressure=input.Units.pressure,torque=input.Units.torque};
            lines=input.Lines.Select(NativePartsBaseReadInputs.CopyLineExact).ToArray();prices=input.Prices.Select(x=>x==null?null:new NativePartsPriceFact(x.Source,x.AssetKey,x.Price)).ToArray();
            var partRows=profile?.parts;var looseRows=profile?.looseEngineElements;var inventory=profile?.inventory;
            bool shop=page=="Shop", inventoryPage=page=="Inventory", installedPage=page=="Installed";
            int pcount=partRows?.Count??0,ucount=looseRows?.Count??0,dcount=shop?(inventory?.dealer?.Count??0):0;
            partCountAtStart=pcount;looseCountAtStart=ucount;dealerCountAtStart=dcount;marketCountAtStart=shop?(profile?.inventory?.market?.parts?.Count??0):0;
            totalItems=(shop||inventoryPage?pcount+ucount:installedPage?pcount:0)+dcount+(shop?(profile?.inventory?.market?.parts?.Count??0):0);
            if(profile?.inventory?.market!=null){var m=profile.inventory.market;stockVersion=m.version;seed=m.seed;generation=m.generation;guarantee=m.guaranteeAtRefresh;marketCount=shop?(m.parts?.Count??0):0;stocks=new NewPartStock[marketCount];stockSources=shop?new NewPartStock[marketCount]:null;}
            reservationIdentity=profile?.inventory?.cart;reservationLinesIdentity=reservationIdentity?.lines;reservationSupplyIdentity=reservationIdentity?.supply;if(reservationIdentity!=null)reservation=CopyReservation(reservationIdentity);
        }
        internal static CaptureSession BeginCapture(NativePartsBaseReadInputs inputs)
        {
            AssertMainThread();if(inputs==null)throw new ArgumentNullException(nameof(inputs));NativePartsBaseReadInputs.ValidatePage(inputs.Page);long start=Stopwatch.GetTimestamp();var facts=new NativePartsBaseReadSet(inputs);var session=new CaptureSession(facts,inputs.Owner);
            double setup=ElapsedMs(start)+(inputs?.CaptureMilliseconds??0);facts.CaptureMilliseconds+=setup;facts.LargestCaptureStepMilliseconds=Math.Max(facts.LargestCaptureStepMilliseconds,setup);return session;
        }
        internal sealed class CaptureSession
        {
            readonly NativePartsBaseReadSet facts;
            internal bool Complete=>facts.Complete;
            internal bool Failed=>staleOwner||facts.sourceShapeChanged;
            internal string FailureReason=>staleOwner?"Selected owner changed during bounded capture.":facts.sourceShapeChanged?"Profile asset collections changed during bounded capture.":null;
            bool staleOwner;
            internal int TotalItems=>facts.totalItems;
            internal int ItemsCaptured=>facts.CapturedItemCount;
            internal double CaptureMilliseconds=>facts.CaptureMilliseconds;
            internal double LargestCaptureStepMilliseconds=>facts.LargestCaptureStepMilliseconds;
            internal double FinalCompareMilliseconds{get;private set;}
            internal CaptureSession(NativePartsBaseReadSet facts,NativePartsOwnerContext inputOwner){this.facts=facts;staleOwner=inputOwner==null||!inputOwner.IsCurrent();}
            internal bool Advance(int itemCap,double softMilliseconds)
            {
                AssertMainThread();if(Failed)return false;if(facts.owner==null||!facts.owner.IsCurrent()){staleOwner=true;return false;}if(!facts.CaptureSourcesCurrent()){facts.sourceShapeChanged=true;return false;}if(itemCap<1)itemCap=1;long start=Stopwatch.GetTimestamp();int done=0;
                while(!Complete&&done<itemCap)
                {
                    if(ElapsedMs(start)>=softMilliseconds&&done>0)break;
                    if(!facts.CaptureSourcesCurrent()){facts.sourceShapeChanged=true;break;}
                    facts.CaptureOne();done++;
                }
                double ms=ElapsedMs(start);facts.CaptureMilliseconds+=ms;
                facts.LargestCaptureStepMilliseconds=Math.Max(facts.LargestCaptureStepMilliseconds,ms);return Complete;
            }
            internal bool TryFinish(NativePartsBaseReadInputs currentInputs,out NativePartsBaseReadSet readSet,out string reason)
            {
                AssertMainThread();long start=Stopwatch.GetTimestamp();readSet=null;reason=null;
                if(staleOwner){reason="Selected owner changed during bounded capture.";FinalCompareMilliseconds=ElapsedMs(start);return false;}
                if(facts.sourceShapeChanged){reason="Profile asset collections changed during bounded capture.";FinalCompareMilliseconds=ElapsedMs(start);return false;}
                if(!Complete){reason="Base facts are not fully captured.";FinalCompareMilliseconds=ElapsedMs(start);facts.FinalCompareMilliseconds=FinalCompareMilliseconds;return false;}
                bool matches=facts.MatchesCurrent(currentInputs,out reason);FinalCompareMilliseconds=ElapsedMs(start);facts.FinalCompareMilliseconds=FinalCompareMilliseconds;
                if(!matches)return false;readSet=facts;return true;
            }
        }
        void CaptureOne()
        {
            int partsCount=profile?.parts?.Count??0,looseCount=profile?.looseEngineElements?.Count??0;
            bool shop=page=="Shop", inv=page=="Inventory", installedPage=page=="Installed";
            if(shop&&ownedPartIndex<partsCount){var p=profile.parts[ownedPartIndex++];ownedPartSources.Add(p);ownedIds.Add(p?.instanceId);ownedIds.Add(p?.definitionId);if(p?.condition?.elements!=null)foreach(var e in p.condition.elements){ownedIds.Add(e?.id);ownedIds.Add(e?.definitionId);}if(IsSelectedPart(p))selectedInstalledParts.Add(new PartFact{source=p,snapshot=p.Copy()});return;}
            if(shop&&ownedUnitIndex<looseCount){var u=profile.looseEngineElements[ownedUnitIndex++];ownedUnitSources.Add(u);ownedIds.Add(u?.element?.id);ownedIds.Add(u?.definitionId);return;}
            if((inv||installedPage)&&partIndex<partsCount){var p=profile.parts[partIndex++];PartFact fact=null;if(IsSelectedPart(p)){fact=new PartFact{source=p,snapshot=p.Copy()};selectedInstalledParts.Add(fact);}if(inv){if(fact==null)fact=new PartFact{source=p,snapshot=p?.Copy()};relevantParts.Add(fact);}else if(fact!=null)relevantParts.Add(fact);return;}
            if(inv&&unitIndex<looseCount){var u=profile.looseEngineElements[unitIndex++];relevantUnits.Add(CopyUnit(u));return;}
            if(page=="Shop"&&dealerIndex<(profile.inventory?.dealer?.Count??0)){var d=profile.inventory.dealer[dealerIndex++];dealer.Add(d==null?null:new DealerFact{source=d,partSource=d.part,isUnit=d.isUnit,part=d.part?.Copy(),unit=CopyUnit(d.unit),paid=d.paidPrice});return;}
            if(page=="Shop"&&stockIndex<marketCount){var i=stockIndex++;var m=profile.inventory.market.parts[i];stockSources[i]=m;stocks[i]=m==null?null:new NewPartStock{definitions=m.definitions==null?null:new List<string>(m.definitions),units=m.units,initialUnits=m.initialUnits,deliveryPercent=m.deliveryPercent,discovered=m.discovered};return;}
        }
        bool CaptureSourcesCurrent()=>ReferenceEquals(profile?.parts,partsCollection)&&ReferenceEquals(profile?.looseEngineElements,looseCollection)&&
            (profile?.parts?.Count??0)==partCountAtStart&&(profile?.looseEngineElements?.Count??0)==looseCountAtStart&&
            (page!="Shop"||ReferenceEquals(profile?.inventory?.dealer,dealerCollection)&&ReferenceEquals(profile?.inventory?.market?.parts,stockCollection)&&
                (profile?.inventory?.dealer?.Count??0)==dealerCountAtStart&&(profile?.inventory?.market?.parts?.Count??0)==marketCountAtStart);
        bool IsSelectedPart(PartInstance part){if(part==null||string.IsNullOrEmpty(part.instanceId))return false;var slots=selectedCar?.installed?.slots;if(slots==null)return false;for(int i=0;i<slots.Count;i++)if(slots[i]!=null&&slots[i].partInstanceId==part.instanceId)return true;return false;}
        internal bool MatchesCurrent(NativePartsBaseReadInputs currentInputs,out string reason)
        {
            AssertMainThread();long start=Stopwatch.GetTimestamp();try{return MatchesCurrentCore(currentInputs,out reason);}finally{FinalCompareMilliseconds=ElapsedMs(start);}
        }
        bool MatchesCurrentCore(NativePartsBaseReadInputs currentInputs,out string reason)
        {
            reason=null;if(currentInputs==null||profile==null||!ReferenceEquals(currentInputs.Profile,profile)||!ReferenceEquals(profile.catalog,catalog)||profile.revision!=revision||profile.storageCommit!=storageCommit||page=="Shop"&&(profile.cash!=cash||profile.reputation!=reputation)||profile.selectedOwnedVehicleId!=selectedOwnedVehicleId||profile.selectedVehicleId!=selectedVehicleId||!ReferenceEquals(profile.cars,carsCollection)){reason="Profile, catalog, revision, storage, selection or economy changed.";return false;}
            var liveOwner=NativePartsOwnerContext.Capture();if(owner==null||!owner.IsCurrent()||!owner.Same(liveOwner)||!owner.Same(currentInputs.Owner)||page!=(currentInputs.Page??"")||planned!=currentInputs.Planned||!SameUnits(units,currentInputs.Units)||!SameLines(lines,currentInputs.Lines)||!SamePrices(prices,currentInputs.Prices)){reason="Owner, page, units, cart or price facts changed.";return false;}
            if(catalogToken==null){reason="Authored catalog key snapshot is not ready.";return false;}
            if(catalogToken!=null&&!catalogToken.MatchesCurrent(profile.catalog)){reason="Authored catalog content changed.";return false;}
            var currentSelected=profile.cars==null?null:profile.SelectedCar;
            if(!ReferenceEquals(currentSelected,selectedCar)||selectedCar?.instanceId!=selectedId||selectedCar?.vehicleId!=selectedModel||selectedCar?.engineAssemblyId!=engineAssemblyId||selectedCar?.garageJobId!=garageJobId||selectedCar!=null&&selectedCar.fuelInitialized!=fuelInitialized||!ReferenceEquals(selectedCar?.tune,tuneSource)||!ReferenceEquals(selectedCar?.fuel,fuelSource)||!ReferenceEquals(selectedCar?.fluids,fluidsSource)||!ReferenceEquals(selectedCar?.installed,installedSource)||!ReferenceEquals(selectedCar?.engineRebuild,rebuildSource)||!ReferenceEquals(selectedCar?.engineRebuild?.targets,rebuildTargetSources)||!SameRebuild(rebuild,rebuildTargets,selectedCar?.engineRebuild)||!SameTune(tune,selectedCar?.tune)||!SameFuel(fuel,selectedCar?.fuel)||!SameFluids(fluids,selectedCar?.fluids)||!SameInstalled(installed,selectedCar?.installed)){reason="Selected-car inspection or lock facts changed.";return false;}
            if(!SameCapturedRows()){reason="Page assets, dealer, stock, reservation or owned IDs changed.";return false;}
            return true;
        }
        bool SameCapturedRows()
        {
            if(!ReferenceEquals(profile.parts,partsCollection)||!ReferenceEquals(profile.looseEngineElements,looseCollection)||profile.parts!=null&&(profile.parts.Count!=partCountAtStart)||profile.looseEngineElements!=null&&(profile.looseEngineElements.Count!=looseCountAtStart))return false;
            // Compare relevant assets by ordered source identity, retaining exact null positions.
            if(page=="Shop")
            {
                int partIndex=0,unitIndex=0,idIndex=0,installedIndex=0;
                foreach(var p in profile.parts??new List<PartInstance>())
                {
                    if(partIndex>=ownedPartSources.Count||!ReferenceEquals(p,ownedPartSources[partIndex++])||idIndex+2>ownedIds.Count||ownedIds[idIndex++]!=p?.instanceId||ownedIds[idIndex++]!=p?.definitionId)return false;
                    if(p?.condition?.elements!=null)foreach(var e in p.condition.elements)if(idIndex+2>ownedIds.Count||ownedIds[idIndex++]!=e?.id||ownedIds[idIndex++]!=e?.definitionId)return false;
                    if(IsSelectedPart(p)){if(installedIndex>=selectedInstalledParts.Count||!ReferenceEquals(selectedInstalledParts[installedIndex].source,p)||!SamePart(selectedInstalledParts[installedIndex].snapshot,p))return false;installedIndex++;}
                }
                foreach(var u in profile.looseEngineElements??new List<LooseEngineElement>())if(unitIndex>=ownedUnitSources.Count||!ReferenceEquals(u,ownedUnitSources[unitIndex++])||idIndex+2>ownedIds.Count||ownedIds[idIndex++]!=u?.element?.id||ownedIds[idIndex++]!=u?.definitionId)return false;
                if(partIndex!=ownedPartSources.Count||unitIndex!=ownedUnitSources.Count||idIndex!=ownedIds.Count||installedIndex!=selectedInstalledParts.Count)return false;
                if(!ReferenceEquals(profile.inventory?.dealer,dealerCollection)||dealer.Count!=(profile.inventory?.dealer?.Count??0))return false;for(int i=0;i<dealer.Count;i++){var x=dealer[i];var y=profile.inventory.dealer[i];if(x==null||y==null){if(x!=null||y!=null)return false;continue;}if(!ReferenceEquals(x.source,y)||!ReferenceEquals(x.partSource,y.part)||x.isUnit!=y.isUnit||x.paid!=y.paidPrice||!SamePart(x.part,y.part)||!SameUnitFact(x.unit,y.unit))return false;}
            }
            if(page=="Inventory"||page=="Installed")
            {
                int factIndex=0,installedIndex=0;foreach(var p in profile.parts??new List<PartInstance>()){bool selected=IsSelectedPart(p);if(page=="Inventory"||selected){if(factIndex>=relevantParts.Count||!ReferenceEquals(p,relevantParts[factIndex].source)||!SamePart(relevantParts[factIndex].snapshot,p))return false;factIndex++;}if(selected){if(installedIndex>=selectedInstalledParts.Count||!ReferenceEquals(p,selectedInstalledParts[installedIndex].source)||!SamePart(selectedInstalledParts[installedIndex].snapshot,p))return false;installedIndex++;}}if(factIndex!=relevantParts.Count||installedIndex!=selectedInstalledParts.Count)return false;
                if(page=="Inventory"){var us=profile.looseEngineElements;if(us==null){if(relevantUnits.Count!=0)return false;}else{if(us.Count!=relevantUnits.Count)return false;for(int i=0;i<us.Count;i++)if(!SameUnit(relevantUnits[i],us[i]))return false;}}
            }
            if(!ReferenceEquals(profile.inventory,inventoryIdentity)||!ReferenceEquals(profile.parts,partsCollection)||!ReferenceEquals(profile.looseEngineElements,looseCollection))return false;
            var market=profile.inventory?.market;
            if(market==null){if(stockVersion!=0||stocks!=null)return false;}
            else
            {
                if(!ReferenceEquals(market,marketIdentity)||market.version!=stockVersion||market.seed!=seed||market.generation!=generation||market.guaranteeAtRefresh!=guarantee)return false;
                if(page=="Shop")
                {
                    if(!ReferenceEquals(market.parts,stockCollection)||(market.parts?.Count??0)!=marketCount)return false;
                    for(int i=0;i<marketCount;i++){var a=stocks[i];var b=market.parts[i];if(!ReferenceEquals(stockSources[i],b))return false;if(a==null||b==null){if(a!=b)return false;continue;}if(a.units!=b.units||a.initialUnits!=b.initialUnits||a.deliveryPercent!=b.deliveryPercent||a.discovered!=b.discovered||!SameStrings(a.definitions,b.definitions))return false;}
                }
            }
            var vs=profile.vehicleStock;if(!ReferenceEquals(vs,vehicleStockIdentity)||vs!=null&&(vs.version!=vehicleStockVersion||vs.seed!=vehicleSeed||vs.generation!=vehicleGeneration||vs.purchases!=vehiclePurchases||vs.completedRuns!=vehicleRuns||vs.knowledgeRevision!=vehicleKnowledge)||(profile.inventory?.usingCartReservation??false)!=usingCartReservation)return false;
            var currentReservation=profile.inventory?.cart;if(!ReferenceEquals(currentReservation,reservationIdentity)||!ReferenceEquals(currentReservation?.lines,reservationLinesIdentity)||!ReferenceEquals(currentReservation?.supply,reservationSupplyIdentity))return false;
            return SameReservation(reservation,currentReservation);
        }
        internal static IReadOnlyDictionary<string,string[]> ComparedSerializedFields()=>new Dictionary<string,string[]>
        {
            ["Tune"]=new[]{"camTimingDegrees","driverMassKg","payloadMassKg","payloadPositionM","payloadHeightM","launchRpm","finalDrive","tirePressurePsi","shiftRpm","engineLevel","tireLevel","weightLevel","gearboxLevel","exhaustLevel","inductionUpgrade","blowOffValve","turboSize","boostSetting","superchargerPulley","controlLevel","twoStep","launchPreload","antiLag","limiter","twoStepStyle","limiterCut","twoStepCut","revLimitRpm","wheelDiameter","wheelWidth","wheelMass","frontWheelDiameter","rearWheelDiameter","frontWheelWidth","rearWheelWidth","frontTirePressurePsi","rearTirePressurePsi","coldStart","transmissionUpgrade","flywheelMass","awd","awdConfigured","frontLeftGrip","frontRightGrip","rearLeftGrip","rearRightGrip","partsResolved","partLimiterAdjustable","partTwoStepAdjustable","partShiftLight","partShiftLightAdjustable","partTuningConfigured","partBoostAdjustable","partPulleyAdjustable","partValveAdjustable","partFinalDriveAdjustable","partTurboSize","partFixedBoost","partBoostMin","partBoostMax","partFixedPulley","partPulleyMin","partPulleyMax","partFixedFinalDrive","partFinalDriveMin","partFinalDriveMax","partFixedValve","shiftLightRpm","partGearCapacity","partPower","partGrip","partMass","partShift","partCapacity","partBoost"},
            ["FuelState"]=new[]{"litres","cumulativeUsedLitres","flowLitresPerSecond"},
            ["FluidState"]=new[]{"oilLitres","coolantLitres","oilTemperatureC","oilPressureBar","coolantPressureBar","oilCondition","oilConsumedLitres","oilLeakedLitres","coolantLostLitres","serviceElapsedSeconds","oilBurnLps","oilLeakLps","coolantLeakLps","coolantVentLps"},
            ["ComponentCondition"]=new[]{"health","failed","failureReason","runningSeconds","distanceM","stressExposure","temperatureC","slipEnergyJ","deposits","leakSeverity","adjustmentError","serviceWearRemainder","serviceCount","reconditionCount","lastService","elements","clutch"},
            ["PartInstance"]=new[]{"version","instanceId","definitionId","storageId","location","ownerId","slot","condition"},
            ["ClutchConditionState"]=new[]{"version","oilLitres","oilCondition"},
            ["AwdSpec"]=new[]{"mode","centerSpeedRatio","torqueBiasRatio","centerPreloadNm","frontBias","minimumFront","maximumFront","primaryFront","isolateFailedAxle","maximumSecondary","responseSeconds","slipThreshold","preload","freeShaftInertiaKgM2","freeShaftDragNm","freeShaftViscousDrag"},
            ["InstalledBuild"]=new[]{"version","instanceId","vehicleDefinitionId","slots"},
            ["SlotBinding"]=new[]{"slot","partInstanceId"},
            ["EngineRebuildJob"]=new[]{"active","id","engineId","startedUtcTicks","readyUtcTicks","paidPrice","targets"},
            ["EngineRebuildTarget"]=new[]{"partId","definitionId","replace"},
            ["EngineElement"]=new[]{"id","definitionId","health","role","cylinder","valve","severity","wearRemainder","fault"},
            ["TireCondition"]=new[]{"failed","temperatureC","slipEnergyJ","distanceM","lastContactWorkJ","consumedLife","runningSeconds","failureReason"},
            ["PartCartLine"]=new[]{"definitionId","quantity","install","preparedBlock","preparationOwner","action","assetId","role","destinationId","destination","positions"},
            ["LooseEngineElement"]=new[]{"definitionId","storageId","unitPrice","element","tireUnit"},
            ["DealerStock"]=new[]{"isUnit","part","unit","paidPrice"},
            ["NewPartStock"]=new[]{"definitions","units","initialUnits","deliveryPercent","discovered"},
            ["CartReservationState"]=new[]{"version","createdRun","expiresRun","generation","carId","lines","supply"},
            ["ReservedPartSupply"]=new[]{"definitionId","units","generation"},
            ["UnitPreferences"]=new[]{"version","speed","pressure","torque"},
            ["VehicleStockState scoped"]=new[]{"version","seed","generation","purchases","completedRuns","knowledgeRevision"}
        };

        static InstalledBuild CopyInstalled(InstalledBuild i)=>i==null?null:new InstalledBuild{version=i.version,instanceId=i.instanceId,vehicleDefinitionId=i.vehicleDefinitionId,slots=i.slots==null?null:i.slots.Select(s=>s==null?null:new SlotBinding{slot=s.slot,partInstanceId=s.partInstanceId}).ToList()};
        static UnitFact CopyUnit(LooseEngineElement u)=>u==null?null:new UnitFact{source=u,elementSource=u.element,definition=u.definitionId,storage=u.storageId,price=u.unitPrice,tireUnit=u.tireUnit,element=u.element?.Copy()};
        static bool SameUnit(UnitFact a,LooseEngineElement b)=>a==null?b==null:b!=null&&ReferenceEquals(a.source,b)&&ReferenceEquals(a.elementSource,b.element)&&a.definition==b.definitionId&&a.storage==b.storageId&&a.price==b.unitPrice&&a.tireUnit==b.tireUnit&&SameElement(a.element,b.element);
        static bool SameUnitFact(UnitFact a,LooseEngineElement b)=>SameUnit(a,b);
        static bool SamePart(PartInstance a,PartInstance b)=>a==null?b==null:b!=null&&a.version==b.version&&a.instanceId==b.instanceId&&a.definitionId==b.definitionId&&a.storageId==b.storageId&&a.location==b.location&&a.ownerId==b.ownerId&&a.slot==b.slot&&SameCondition(a.condition,b.condition);
        static bool SameCondition(ComponentCondition a,ComponentCondition b)
        {
            if(a==null||b==null)return a==b;
            if(!F32(a.health,b.health)||a.failed!=b.failed||a.failureReason!=b.failureReason||!F64(a.runningSeconds,b.runningSeconds)||!F64(a.distanceM,b.distanceM)||!F64(a.stressExposure,b.stressExposure)||!F64(a.temperatureC,b.temperatureC)||!F64(a.slipEnergyJ,b.slipEnergyJ)||!F64(a.deposits,b.deposits)||!F64(a.leakSeverity,b.leakSeverity)||!F64(a.adjustmentError,b.adjustmentError)||!F64(a.serviceWearRemainder,b.serviceWearRemainder)||a.serviceCount!=b.serviceCount||a.reconditionCount!=b.reconditionCount||a.lastService!=b.lastService)return false;
            if(a.clutch==null||b.clutch==null){if(a.clutch!=b.clutch)return false;}else if(a.clutch.version!=b.clutch.version||!F64(a.clutch.oilLitres,b.clutch.oilLitres)||!F64(a.clutch.oilCondition,b.clutch.oilCondition))return false;
            if(a.elements==null||b.elements==null)return a.elements==b.elements;if(a.elements.Length!=b.elements.Length)return false;for(int i=0;i<a.elements.Length;i++)if(!SameElement(a.elements[i],b.elements[i]))return false;return true;
        }
        static bool SameElement(EngineElement a,EngineElement b)
        {
            if(a==null||b==null)return a==b;if(a.GetType()!=b.GetType()||a.id!=b.id||a.definitionId!=b.definitionId||!F32(a.health,b.health)||a.role!=b.role||a.cylinder!=b.cylinder||a.valve!=b.valve||!F32(a.severity,b.severity)||!F64(a.wearRemainder,b.wearRemainder)||a.fault!=b.fault)return false;
            if(a is TireCondition x&&b is TireCondition y)return x.failed==y.failed&&F64(x.temperatureC,y.temperatureC)&&F64(x.slipEnergyJ,y.slipEnergyJ)&&F64(x.distanceM,y.distanceM)&&F64(x.lastContactWorkJ,y.lastContactWorkJ)&&F64(x.consumedLife,y.consumedLife)&&F64(x.runningSeconds,y.runningSeconds)&&x.failureReason==y.failureReason;return true;
        }
        static bool SameFuel(FuelState a,FuelState b)=>a==null?b==null:b!=null&&F64(a.litres,b.litres)&&F64(a.cumulativeUsedLitres,b.cumulativeUsedLitres)&&F64(a.flowLitresPerSecond,b.flowLitresPerSecond);
        static bool SameFluids(FluidState a,FluidState b)=>a==null?b==null:b!=null&&F64(a.oilLitres,b.oilLitres)&&F64(a.coolantLitres,b.coolantLitres)&&F64(a.oilTemperatureC,b.oilTemperatureC)&&F64(a.oilPressureBar,b.oilPressureBar)&&F64(a.coolantPressureBar,b.coolantPressureBar)&&F64(a.oilCondition,b.oilCondition)&&F64(a.oilConsumedLitres,b.oilConsumedLitres)&&F64(a.oilLeakedLitres,b.oilLeakedLitres)&&F64(a.coolantLostLitres,b.coolantLostLitres)&&F64(a.serviceElapsedSeconds,b.serviceElapsedSeconds)&&F64(a.oilBurnLps,b.oilBurnLps)&&F64(a.oilLeakLps,b.oilLeakLps)&&F64(a.coolantLeakLps,b.coolantLeakLps)&&F64(a.coolantVentLps,b.coolantVentLps);
        static bool SameInstalled(InstalledBuild a,InstalledBuild b){if(a==null||b==null)return a==b;if(a.version!=b.version||a.instanceId!=b.instanceId||a.vehicleDefinitionId!=b.vehicleDefinitionId||a.slots==null||b.slots==null)return a.slots==b.slots;if(a.slots.Count!=b.slots.Count)return false;for(int i=0;i<a.slots.Count;i++){var x=a.slots[i];var y=b.slots[i];if(x==null||y==null){if(x!=y)return false;}else if(x.slot!=y.slot||x.partInstanceId!=y.partInstanceId)return false;}return true;}
        static bool SameRebuild(EngineRebuildJob a,RebuildTargetFact[] targets,EngineRebuildJob b){if(a==null||b==null)return a==b;if(a.active!=b.active||a.id!=b.id||a.engineId!=b.engineId||a.startedUtcTicks!=b.startedUtcTicks||a.readyUtcTicks!=b.readyUtcTicks||a.paidPrice!=b.paidPrice)return false;if(targets==null||b.targets==null)return targets==null&&b.targets==null;if(targets.Length!=b.targets.Count)return false;for(int i=0;i<targets.Length;i++){var x=targets[i];var y=b.targets[i];if(x==null||y==null){if(x!=null||y!=null)return false;}else if(!ReferenceEquals(x.source,y)||x.partId!=y.partId||x.definitionId!=y.definitionId||x.replace!=y.replace)return false;}return true;}
        static bool SameUnits(UnitPreferences a,UnitPreferences b)=>a==null?b==null:b!=null&&a.version==b.version&&a.speed==b.speed&&a.pressure==b.pressure&&a.torque==b.torque;
        static bool SameLines(PartCartLine[] a,PartCartLine[] b){if(a==null||b==null||a.Length!=b.Length)return a==b;for(int i=0;i<a.Length;i++){var x=a[i];var y=b[i];if(x==null||y==null){if(x!=y)return false;continue;}if(x.definitionId!=y.definitionId||x.quantity!=y.quantity||x.install!=y.install||x.preparedBlock!=y.preparedBlock||x.preparationOwner!=y.preparationOwner||x.action!=y.action||x.assetId!=y.assetId||x.role!=y.role||x.destinationId!=y.destinationId||x.destination!=y.destination||!SameStrings(x.positions,y.positions))return false;}return true;}
        static bool SamePrices(NativePartsPriceFact[] a,NativePartsPriceFact[] b){if(a==null||b==null||a.Length!=b.Length)return a==b;for(int i=0;i<a.Length;i++){var x=a[i];var y=b[i];if(x==null||y==null){if(x!=y)return false;}else if(x.Source!=y.Source||x.AssetKey!=y.AssetKey||x.Price!=y.Price)return false;}return true;}
        static bool SameStrings(IList<string> a,IList<string> b){if(a==null||b==null)return a==b;if(a.Count!=b.Count)return false;for(int i=0;i<a.Count;i++)if(a[i]!=b[i])return false;return true;}
        static bool SameReservation(CartReservationState a,CartReservationState b){if(a==null||b==null)return a==b;if(a.version!=b.version||a.createdRun!=b.createdRun||a.expiresRun!=b.expiresRun||a.generation!=b.generation||a.carId!=b.carId||!SameLines(a.lines?.ToArray(),b.lines?.ToArray()))return false;if(a.supply==null||b.supply==null)return a.supply==b.supply;if(a.supply.Count!=b.supply.Count)return false;for(int i=0;i<a.supply.Count;i++)if(a.supply[i]==null||b.supply[i]==null?a.supply[i]!=b.supply[i]:a.supply[i].definitionId!=b.supply[i].definitionId||a.supply[i].units!=b.supply[i].units||a.supply[i].generation!=b.supply[i].generation)return false;return true;}
        static CartReservationState CopyReservation(CartReservationState x)=>x==null?null:new CartReservationState{version=x.version,createdRun=x.createdRun,expiresRun=x.expiresRun,generation=x.generation,carId=x.carId,lines=x.lines==null?null:x.lines.Select(NativePartsBaseReadInputs.CopyLineExact).ToList(),supply=x.supply==null?null:x.supply.Select(s=>s==null?null:new ReservedPartSupply{definitionId=s.definitionId,units=s.units,generation=s.generation}).ToList()};
        static bool SameTune(Tune a,Tune b)
        {
            if(a==null||b==null)return a==b;
            return F32(a.camTimingDegrees,b.camTimingDegrees)&&F32(a.driverMassKg,b.driverMassKg)&&F32(a.payloadMassKg,b.payloadMassKg)&&F32(a.payloadPositionM,b.payloadPositionM)&&F32(a.payloadHeightM,b.payloadHeightM)&&F32(a.launchRpm,b.launchRpm)&&F32(a.finalDrive,b.finalDrive)&&F32(a.tirePressurePsi,b.tirePressurePsi)&&F32(a.shiftRpm,b.shiftRpm)&&a.engineLevel==b.engineLevel&&a.tireLevel==b.tireLevel&&a.weightLevel==b.weightLevel&&a.gearboxLevel==b.gearboxLevel&&a.exhaustLevel==b.exhaustLevel&&a.inductionUpgrade==b.inductionUpgrade&&a.blowOffValve==b.blowOffValve&&F32(a.turboSize,b.turboSize)&&F32(a.boostSetting,b.boostSetting)&&F32(a.superchargerPulley,b.superchargerPulley)&&a.controlLevel==b.controlLevel&&a.twoStep==b.twoStep&&a.launchPreload==b.launchPreload&&a.antiLag==b.antiLag&&a.limiter==b.limiter&&a.twoStepStyle==b.twoStepStyle&&a.limiterCut==b.limiterCut&&a.twoStepCut==b.twoStepCut&&F32(a.revLimitRpm,b.revLimitRpm)&&F32(a.wheelDiameter,b.wheelDiameter)&&F32(a.wheelWidth,b.wheelWidth)&&F32(a.wheelMass,b.wheelMass)&&F32(a.frontWheelDiameter,b.frontWheelDiameter)&&F32(a.rearWheelDiameter,b.rearWheelDiameter)&&F32(a.frontWheelWidth,b.frontWheelWidth)&&F32(a.rearWheelWidth,b.rearWheelWidth)&&F32(a.frontTirePressurePsi,b.frontTirePressurePsi)&&F32(a.rearTirePressurePsi,b.rearTirePressurePsi)&&a.coldStart==b.coldStart&&a.transmissionUpgrade==b.transmissionUpgrade&&F32(a.flywheelMass,b.flywheelMass)&&SameAwd(a.awd,b.awd)&&a.awdConfigured==b.awdConfigured&&F32(a.frontLeftGrip,b.frontLeftGrip)&&F32(a.frontRightGrip,b.frontRightGrip)&&F32(a.rearLeftGrip,b.rearLeftGrip)&&F32(a.rearRightGrip,b.rearRightGrip)&&a.partsResolved==b.partsResolved&&a.partLimiterAdjustable==b.partLimiterAdjustable&&a.partTwoStepAdjustable==b.partTwoStepAdjustable&&a.partShiftLight==b.partShiftLight&&a.partShiftLightAdjustable==b.partShiftLightAdjustable&&a.partTuningConfigured==b.partTuningConfigured&&a.partBoostAdjustable==b.partBoostAdjustable&&a.partPulleyAdjustable==b.partPulleyAdjustable&&a.partValveAdjustable==b.partValveAdjustable&&a.partFinalDriveAdjustable==b.partFinalDriveAdjustable&&F32(a.partTurboSize,b.partTurboSize)&&F32(a.partFixedBoost,b.partFixedBoost)&&F32(a.partBoostMin,b.partBoostMin)&&F32(a.partBoostMax,b.partBoostMax)&&F32(a.partFixedPulley,b.partFixedPulley)&&F32(a.partPulleyMin,b.partPulleyMin)&&F32(a.partPulleyMax,b.partPulleyMax)&&F32(a.partFixedFinalDrive,b.partFixedFinalDrive)&&F32(a.partFinalDriveMin,b.partFinalDriveMin)&&F32(a.partFinalDriveMax,b.partFinalDriveMax)&&a.partFixedValve==b.partFixedValve&&F32(a.shiftLightRpm,b.shiftLightRpm)&&F32(a.partGearCapacity,b.partGearCapacity)&&F32(a.partPower,b.partPower)&&F32(a.partGrip,b.partGrip)&&F32(a.partMass,b.partMass)&&F32(a.partShift,b.partShift)&&F32(a.partCapacity,b.partCapacity)&&F32(a.partBoost,b.partBoost);
        }
        static bool SameAwd(AwdSpec a,AwdSpec b)=>a==null?b==null:b!=null&&a.mode==b.mode&&F32(a.centerSpeedRatio,b.centerSpeedRatio)&&F32(a.torqueBiasRatio,b.torqueBiasRatio)&&F32(a.centerPreloadNm,b.centerPreloadNm)&&F32(a.frontBias,b.frontBias)&&F32(a.minimumFront,b.minimumFront)&&F32(a.maximumFront,b.maximumFront)&&a.primaryFront==b.primaryFront&&a.isolateFailedAxle==b.isolateFailedAxle&&F32(a.maximumSecondary,b.maximumSecondary)&&F32(a.responseSeconds,b.responseSeconds)&&F32(a.slipThreshold,b.slipThreshold)&&F32(a.preload,b.preload)&&F32(a.freeShaftInertiaKgM2,b.freeShaftInertiaKgM2)&&F32(a.freeShaftDragNm,b.freeShaftDragNm)&&F32(a.freeShaftViscousDrag,b.freeShaftViscousDrag);
        [StructLayout(LayoutKind.Explicit)] struct FloatBits { [FieldOffset(0)] internal float value;[FieldOffset(0)] internal int bits; }
        static int F32Bits(float value){var bits=new FloatBits{value=value};return bits.bits;}
        static bool F32(float a,float b)=>F32Bits(a)==F32Bits(b);
        static bool F64(double a,double b)=>BitConverter.DoubleToInt64Bits(a)==BitConverter.DoubleToInt64Bits(b);
    }
}
