using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    internal enum PartMoveAssetKind { Unit, Engine, Part }
    internal enum PartMoveMemberPolicy { LegacyEnsure, ProjectionRequireExisting }

    internal sealed class PartMovePreamble
    {
        internal readonly PartMoveAssetKind Kind;
        internal readonly string AssetId;
        internal readonly PartInstance Part;
        internal PartMovePreamble(PartMoveAssetKind kind,string assetId,PartInstance part=null)
        {Kind=kind;AssetId=assetId;Part=part;}
    }

    internal sealed class PartMoveEffect
    {
        internal PartInstance MovedPart;
        internal PartInstance DisplacedPart;
        internal readonly List<PartInstance> ChangedParts=new List<PartInstance>();
        internal readonly List<PartMoveCarState> Cars=new List<PartMoveCarState>();
        internal readonly List<PartMoveEngineState> Engines=new List<PartMoveEngineState>();
        internal readonly List<PartMoveAffectedOwner> Owners=new List<PartMoveAffectedOwner>();
    }

    internal sealed class PartMoveAffectedOwner
    {
        internal readonly PartMoveCarState Car;
        internal readonly PartMoveEngineState Engine;
        internal PartMoveAffectedOwner(PartMoveCarState car){Car=car;}
        internal PartMoveAffectedOwner(PartMoveEngineState engine){Engine=engine;}
    }

    // Mutation core works over either borrowed live objects or an invocation-local
    // copy-on-read projection. Implementations are disposed before evaluation returns.
    internal abstract class InstalledPartTransitionState : IDisposable
    {
        internal abstract bool HasUnsettledRun {get;}
        internal abstract bool StorageExists(string id);
        internal abstract bool HasLooseUnit(string id);
        internal abstract PartMoveLooseUnitState FindLooseUnit(string id);
        internal abstract bool HasEngineAsset(string id);
        internal abstract PartInstance FindPart(string id);
        internal abstract PartMoveCarState FindCar(string id);
        internal abstract PartMoveCarState FirstCarOwningPart(string partId);
        internal abstract PartMoveEngineState FindEngine(string id);
        internal abstract IDictionary<PartCategory,string> CarDefinitions(PartMoveCarState car);
        internal abstract IDictionary<PartCategory,string> EngineDefinitions(PartMoveEngineState engine);
        internal abstract void MarkAffected(PartMoveCarState car);
        internal abstract void MarkAffected(PartMoveEngineState engine);
        internal abstract void MarkChanged(PartInstance part);
        internal abstract IReadOnlyList<PartInstance> ChangedParts {get;}
        internal abstract IReadOnlyList<PartMoveCarState> AffectedCars {get;}
        internal abstract IReadOnlyList<PartMoveEngineState> AffectedEngines {get;}
        internal abstract IReadOnlyList<PartMoveAffectedOwner> AffectedOwners {get;}
        internal abstract void MarkEngineChanged(PartMoveEngineState engine);
        internal abstract IReadOnlyList<PartMoveEngineState> ChangedEngines {get;}
        internal abstract void MarkUnitChanged(PartMoveLooseUnitState unit);
        internal abstract IReadOnlyList<PartMoveLooseUnitState> ChangedUnits {get;}
        public abstract void Dispose();
    }

    internal sealed class PartMoveCarState
    {
        OwnedCar live;
        OwnedCar source;
        internal readonly string Id,VehicleId,ChassisId;
        string engineAssemblyId;
        internal string EngineAssemblyId {get{if(live!=null)return live.engineAssemblyId;return engineAssemblyId;}set{if(live!=null)live.engineAssemblyId=value;else engineAssemblyId=value;}}
        internal string GarageJobId;
        internal readonly bool HasRebuildJob,RebuildActive;
        List<SlotBinding> bindings;
        Tune tune;
        FuelState fuel;
        FluidState fluids;
        bool bindingsLoaded,tuneLoaded,fuelLoaded,fluidsLoaded;
        internal IList<SlotBinding> Bindings
        {
            get{if(live!=null)return live.installed.slots;if(!bindingsLoaded){var sourceBindings=source.installed.slots;bindings=sourceBindings==null?null:sourceBindings.Select(binding=>binding==null?null:new SlotBinding{slot=binding.slot,partInstanceId=binding.partInstanceId}).ToList();bindingsLoaded=true;}return bindings;}
            set{if(live!=null)live.installed.slots=(List<SlotBinding>)value;else{bindings=(List<SlotBinding>)value;bindingsLoaded=true;}}
        }
        internal Tune Tune
        {
            get{if(live!=null)return live.tune;if(!tuneLoaded){tune=copyTune(source.tune);tuneLoaded=true;}return tune;}
            set{if(live!=null)live.tune=value;else{tune=value;tuneLoaded=true;}}
        }
        internal FuelState Fuel {get{if(live!=null)return live.fuel;if(!fuelLoaded){fuel=source.fuel?.Copy();fuelLoaded=true;}return fuel;}}
        internal FluidState Fluids
        {
            get{if(live!=null)return live.fluids;if(!fluidsLoaded){fluids=copyFluids(source.fluids);fluidsLoaded=true;}return fluids;}
            set{if(live!=null)live.fluids=value;else{fluids=value;fluidsLoaded=true;}}
        }

        internal PartMoveCarState(OwnedCar car,bool copy,Func<Tune,Tune> copyTune=null,Func<FluidState,FluidState> copyFluids=null)
        {
            Id=car.instanceId;VehicleId=car.vehicleId;ChassisId=car.chassisId;
            engineAssemblyId=car.engineAssemblyId;GarageJobId=car.garageJobId;
            HasRebuildJob=car.engineRebuild!=null;RebuildActive=car.engineRebuild!=null&&car.engineRebuild.active;
            if(copy){source=car;this.copyTune=copyTune??(value=>value?.Copy());this.copyFluids=copyFluids??(value=>value?.Copy());}else live=car;
        }
        readonly Func<Tune,Tune> copyTune;
        readonly Func<FluidState,FluidState> copyFluids;
        internal void Release(){live=null;source=null;bindings=null;tune=null;fuel=null;fluids=null;}
        internal Tune OwnedTuneCopy=>Tune?.Copy();
        internal SlotBinding[] BindingCopies=>Bindings==null?null:Bindings.Select(binding=>binding==null?null:new SlotBinding{slot=binding.slot,partInstanceId=binding.partInstanceId}).ToArray();
        internal IList<SlotBinding> BindingsForLookup=>live!=null?live.installed.slots:bindingsLoaded?bindings:source.installed.slots;
    }

    internal sealed class PartMoveEngineState
    {
        EngineAssemblyInstance live;
        EngineAssemblyInstance source;
        internal readonly string Id,VehicleDefinitionId;
        string parentVehicleId,storageId;
        internal string ParentVehicleId {get{if(live!=null)return live.parentVehicleId;return parentVehicleId;}set{if(live!=null)live.parentVehicleId=value;else parentVehicleId=value;}}
        internal string StorageId {get{if(live!=null)return live.storageId;return storageId;}set{if(live!=null)live.storageId=value;else storageId=value;}}
        internal readonly string CustomName;
        List<SlotBinding> bindings;
        Tune tune;
        FluidState fluids;
        bool bindingsLoaded,tuneLoaded,fluidsLoaded;
        internal IList<SlotBinding> Bindings
        {
            get{if(live!=null)return live.slots;if(!bindingsLoaded){var sourceBindings=source.slots;bindings=sourceBindings==null?null:sourceBindings.Select(binding=>binding==null?null:new SlotBinding{slot=binding.slot,partInstanceId=binding.partInstanceId}).ToList();bindingsLoaded=true;}return bindings;}
            set{if(live!=null)live.slots=(List<SlotBinding>)value;else{bindings=(List<SlotBinding>)value;bindingsLoaded=true;}}
        }
        internal Tune Tune {get{if(live!=null)return live.tune;if(!tuneLoaded){tune=copyTune(source.tune);tuneLoaded=true;}return tune;}set{if(live!=null)live.tune=value;else{tune=value;tuneLoaded=true;}}}
        internal FluidState Fluids {get{if(live!=null)return live.fluids;if(!fluidsLoaded){fluids=copyFluids(source.fluids);fluidsLoaded=true;}return fluids;}set{if(live!=null)live.fluids=value;else{fluids=value;fluidsLoaded=true;}}}
        readonly Func<Tune,Tune> copyTune;
        readonly Func<FluidState,FluidState> copyFluids;
        internal PartMoveEngineState(EngineAssemblyInstance engine,bool copy,Func<Tune,Tune> copyTune=null,Func<FluidState,FluidState> copyFluids=null)
        {
            Id=engine.instanceId;VehicleDefinitionId=engine.vehicleDefinitionId;parentVehicleId=engine.parentVehicleId;storageId=engine.storageId;CustomName=engine.customName;
            if(copy){source=engine;this.copyTune=copyTune??(value=>value?.Copy());this.copyFluids=copyFluids??(value=>value?.Copy());}else live=engine;
        }
        internal void Release(){live=null;source=null;bindings=null;tune=null;fluids=null;}
        internal SlotBinding[] BindingCopies=>Bindings==null?null:Bindings.Select(binding=>binding==null?null:new SlotBinding{slot=binding.slot,partInstanceId=binding.partInstanceId}).ToArray();
    }

    internal sealed class PartMoveLooseUnitState
    {
        LooseEngineElement live,source;
        EngineElement element;
        bool elementLoaded;
        readonly Func<EngineElement,EngineElement> copyElement;
        string storageId;
        internal readonly string Id,DefinitionId;
        internal readonly int UnitPrice;
        internal readonly bool TireUnit;
        internal string StorageId {get{if(live!=null)return live.storageId;return storageId;}set{if(live!=null)live.storageId=value;else storageId=value;}}
        internal EngineElement Element
        {
            get{if(live!=null)return live.element;if(!elementLoaded){element=copyElement(source.element);elementLoaded=true;}return element;}
            set{if(live!=null)live.element=value;else{element=value;elementLoaded=true;}}
        }
        internal PartMoveLooseUnitState(LooseEngineElement unit,bool copy,Func<EngineElement,EngineElement> copyElement)
        {
            Id=unit.element.id;DefinitionId=unit.definitionId;UnitPrice=unit.unitPrice;TireUnit=unit.tireUnit;storageId=unit.storageId;
            if(copy){source=unit;this.copyElement=copyElement;}else live=unit;
        }
        internal void Release(){live=null;source=null;element=null;}
    }

    internal sealed class ProfileInstalledPartTransitionState : InstalledPartTransitionState
    {
        sealed class ReferenceComparer<T> : IEqualityComparer<T> where T:class
        {internal static readonly ReferenceComparer<T> Instance=new ReferenceComparer<T>();public bool Equals(T x,T y)=>ReferenceEquals(x,y);public int GetHashCode(T value)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);}
        PlayerProfile profile;
        readonly bool copyOnRead;
        readonly Dictionary<PartInstance,PartInstance> partCopies=new Dictionary<PartInstance,PartInstance>(ReferenceComparer<PartInstance>.Instance);
        readonly Dictionary<OwnedCar,PartMoveCarState> carStates=new Dictionary<OwnedCar,PartMoveCarState>(ReferenceComparer<OwnedCar>.Instance);
        readonly Dictionary<EngineAssemblyInstance,PartMoveEngineState> engineStates=new Dictionary<EngineAssemblyInstance,PartMoveEngineState>(ReferenceComparer<EngineAssemblyInstance>.Instance);
        readonly Dictionary<LooseEngineElement,PartMoveLooseUnitState> unitStates=new Dictionary<LooseEngineElement,PartMoveLooseUnitState>(ReferenceComparer<LooseEngineElement>.Instance);
        readonly Dictionary<Tune,Tune> tuneCopies=new Dictionary<Tune,Tune>(ReferenceComparer<Tune>.Instance);
        readonly Dictionary<FluidState,FluidState> fluidCopies=new Dictionary<FluidState,FluidState>(ReferenceComparer<FluidState>.Instance);
        readonly Dictionary<EngineElement,EngineElement> elementCopies=new Dictionary<EngineElement,EngineElement>(ReferenceComparer<EngineElement>.Instance);
        readonly List<PartMoveCarState> affectedCars=new List<PartMoveCarState>();
        readonly List<PartMoveEngineState> affectedEngines=new List<PartMoveEngineState>();
        readonly List<PartMoveAffectedOwner> affectedOwners=new List<PartMoveAffectedOwner>();
        readonly List<PartInstance> changedParts=new List<PartInstance>();
        readonly List<PartMoveEngineState> changedEngines=new List<PartMoveEngineState>();
        readonly List<PartMoveLooseUnitState> changedUnits=new List<PartMoveLooseUnitState>();
        internal ProfileInstalledPartTransitionState(PlayerProfile profile,bool copyOnRead){this.profile=profile;this.copyOnRead=copyOnRead;}
        internal override bool HasUnsettledRun=>profile.runCheckpoints.Any(run=>!run.settled);
        internal override bool StorageExists(string id)=>profile.inventory.storage.Any(storage=>storage.id==id);
        internal override bool HasLooseUnit(string id)=>profile.looseEngineElements.Find(unit=>unit.element.id==id)!=null;
        internal override PartMoveLooseUnitState FindLooseUnit(string id)
        {var unit=profile.looseEngineElements.Find(candidate=>candidate.element.id==id);return unit==null?null:Wrap(unit);}
        internal override bool HasEngineAsset(string id)=>profile.engines.Find(engine=>engine.instanceId==id)!=null;
        internal override PartInstance FindPart(string id)
        {
            var part=profile.parts.Find(item=>item.instanceId==id);
            if(part==null||!copyOnRead)return part;
            if(!partCopies.TryGetValue(part,out var copy)){copy=part.Copy();partCopies.Add(part,copy);}return copy;
        }
        PartInstance ReadSourcePart(PartInstance source)
        {
            if(source==null||!copyOnRead)return source;
            return partCopies.TryGetValue(source,out var copy)?copy:source;
        }
        internal override PartMoveCarState FindCar(string id)
        {var car=profile.cars.Find(candidate=>candidate.instanceId==id);return car==null?null:Wrap(car);}
        internal override PartMoveCarState FirstCarOwningPart(string partId)
        {
            foreach(var car in profile.cars)
                if(ReadCarBindingsForLookup(car).Any(binding=>binding.partInstanceId==partId))return Wrap(car);
            return null;
        }
        IList<SlotBinding> ReadCarBindingsForLookup(OwnedCar car)
            =>car!=null&&carStates.TryGetValue(car,out var state)?state.BindingsForLookup:car.installed.slots;
        PartMoveCarState Wrap(OwnedCar car)
        {if(!carStates.TryGetValue(car,out var state)){state=new PartMoveCarState(car,copyOnRead,CopyTune,CopyFluid);carStates.Add(car,state);}return state;}
        internal override PartMoveEngineState FindEngine(string id)
        {var engine=profile.engines.Find(candidate=>candidate.instanceId==id);return engine==null?null:Wrap(engine);}
        PartMoveEngineState Wrap(EngineAssemblyInstance engine)
        {if(!engineStates.TryGetValue(engine,out var state)){state=new PartMoveEngineState(engine,copyOnRead,CopyTune,CopyFluid);engineStates.Add(engine,state);}return state;}
        PartMoveLooseUnitState Wrap(LooseEngineElement unit)
        {if(!unitStates.TryGetValue(unit,out var state)){state=new PartMoveLooseUnitState(unit,copyOnRead,CopyElement);unitStates.Add(unit,state);}return state;}
        Tune CopyTune(Tune source){if(source==null)return null;if(!copyOnRead)return source;if(!tuneCopies.TryGetValue(source,out var copy)){copy=source.Copy();tuneCopies.Add(source,copy);}return copy;}
        FluidState CopyFluid(FluidState source){if(source==null)return null;if(!copyOnRead)return source;if(!fluidCopies.TryGetValue(source,out var copy)){copy=source.Copy();fluidCopies.Add(source,copy);}return copy;}
        EngineElement CopyElement(EngineElement source){if(source==null)return null;if(!copyOnRead)return source;if(!elementCopies.TryGetValue(source,out var copy)){copy=source.Copy();elementCopies.Add(source,copy);}return copy;}
        internal override IDictionary<PartCategory,string> CarDefinitions(PartMoveCarState car)
            =>MechanicsOwnership.Definitions(new TransitionPartList(this),car.Bindings);
        internal override IDictionary<PartCategory,string> EngineDefinitions(PartMoveEngineState engine)
            =>AssemblyOperations.Definitions(new TransitionPartList(this),engine.Bindings);
        internal override void MarkAffected(PartMoveCarState car)
        {if(car!=null&&!affectedCars.Contains(car)){affectedCars.Add(car);affectedOwners.Add(new PartMoveAffectedOwner(car));}}
        internal override void MarkAffected(PartMoveEngineState engine)
        {if(engine!=null&&!affectedEngines.Contains(engine)){affectedEngines.Add(engine);affectedOwners.Add(new PartMoveAffectedOwner(engine));}}
        internal override void MarkChanged(PartInstance part){if(part!=null&&!changedParts.Any(existing=>ReferenceEquals(existing,part)))changedParts.Add(part);}
        internal override IReadOnlyList<PartInstance> ChangedParts=>changedParts;
        internal override IReadOnlyList<PartMoveCarState> AffectedCars=>affectedCars;
        internal override IReadOnlyList<PartMoveEngineState> AffectedEngines=>affectedEngines;
        internal override IReadOnlyList<PartMoveAffectedOwner> AffectedOwners=>affectedOwners;
        internal override void MarkEngineChanged(PartMoveEngineState engine){if(engine!=null&&!changedEngines.Contains(engine))changedEngines.Add(engine);}
        internal override IReadOnlyList<PartMoveEngineState> ChangedEngines=>changedEngines;
        internal override void MarkUnitChanged(PartMoveLooseUnitState unit){if(unit!=null&&!changedUnits.Contains(unit))changedUnits.Add(unit);}
        internal override IReadOnlyList<PartMoveLooseUnitState> ChangedUnits=>changedUnits;
        public override void Dispose()
        {
            foreach(var pair in carStates)pair.Value.Release();foreach(var pair in engineStates)pair.Value.Release();foreach(var pair in unitStates)pair.Value.Release();
            carStates.Clear();engineStates.Clear();unitStates.Clear();tuneCopies.Clear();fluidCopies.Clear();elementCopies.Clear();partCopies.Clear();affectedCars.Clear();affectedEngines.Clear();affectedOwners.Clear();changedParts.Clear();changedEngines.Clear();changedUnits.Clear();profile=null;
        }

        // InstalledPartLookup needs source ordering and duplicate checks. Unrelated scanned
        // records are borrowed read-only; only records already requested for mutation are copied.
        sealed class TransitionPartList : IList<PartInstance>
        {
            readonly ProfileInstalledPartTransitionState owner;
            internal TransitionPartList(ProfileInstalledPartTransitionState owner){this.owner=owner;}
            public int Count=>owner.profile.parts.Count;public bool IsReadOnly=>true;
            public PartInstance this[int index]{get=>owner.ReadSourcePart(owner.profile.parts[index]);set=>throw new NotSupportedException();}
            public int IndexOf(PartInstance item){for(int i=0;i<Count;i++)if(ReferenceEquals(this[i],item))return i;return -1;}
            public bool Contains(PartInstance item)=>IndexOf(item)>=0;
            public void CopyTo(PartInstance[] array,int arrayIndex){for(int i=0;i<Count;i++)array[arrayIndex+i]=this[i];}
            public IEnumerator<PartInstance> GetEnumerator(){for(int i=0;i<Count;i++)yield return this[i];}
            IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
            public void Add(PartInstance item)=>throw new NotSupportedException();public void Clear()=>throw new NotSupportedException();
            public void Insert(int index,PartInstance item)=>throw new NotSupportedException();public bool Remove(PartInstance item)=>throw new NotSupportedException();
            public void RemoveAt(int index)=>throw new NotSupportedException();
        }
    }

    internal static class InstalledPartTransition
    {
        internal static PartMovePreamble Preamble(InstalledPartTransitionState state,string assetId,InventoryKind target,string targetId)
        {
            if(state.HasUnsettledRun)throw new InvalidOperationException("Close the active run before moving inventory.");
            if(target==InventoryKind.Storage&&!state.StorageExists(targetId))throw new InvalidOperationException("Unknown storage container.");
            if(state.HasLooseUnit(assetId))return new PartMovePreamble(PartMoveAssetKind.Unit,assetId);
            if(state.HasEngineAsset(assetId))return new PartMovePreamble(PartMoveAssetKind.Engine,assetId);
            var part=state.FindPart(assetId)??throw new InvalidOperationException("Owned item no longer exists.");
            return new PartMovePreamble(PartMoveAssetKind.Part,assetId,part);
        }

        internal static PartMoveEffect MovePartAfterPreamble(InstalledPartTransitionState state,MechanicalCatalog catalog,
            PartMovePreamble preamble,InventoryKind target,string targetId,PartMoveMemberPolicy memberPolicy)
        {
            var item=preamble.Part;
            if(target!=InventoryKind.Storage&&target!=InventoryKind.Car&&target!=InventoryKind.Engine)
                throw new InvalidOperationException("Use a quote/cart to buy or sell.");
            var effect=new PartMoveEffect{MovedPart=item};
            if(item.location!=PartLocation.Inventory)
            {
                var bindingOwner=state.FirstCarOwningPart(preamble.AssetId);var category=catalog.Find(item.definitionId).category;
                if(bindingOwner!=null)
                {
                    // SharedInventory first selects by a binding, then its public helpers
                    // independently reselect the first record matching that owner's ID.
                    MutableCar(state,state.FindCar(bindingOwner.Id));
                    var operationOwner=FindVehicle(state,bindingOwner.Id);
                    state.MarkAffected(operationOwner);
                    RemoveCarPart(state,operationOwner,category.ToString());
                }
                else
                {
                    var engine=RemoveEnginePart(state,item.ownerId,category);
                    state.MarkAffected(engine);
                }
            }
            if(target==InventoryKind.Car)
            {
                var targetCar=FindVehicle(state,targetId);
                state.MarkAffected(targetCar);
                effect.DisplacedPart=InstallCarPart(state,catalog,targetCar,item);
            }
            else if(target==InventoryKind.Engine)
            {
                var targetEngine=Stored(state,targetId);
                state.MarkAffected(targetEngine);
                var destinationPart=state.FindPart(preamble.AssetId)??throw new InvalidOperationException("Part no longer exists.");
                effect.MovedPart=destinationPart;
                effect.DisplacedPart=InstallEnginePartAfterStored(state,catalog,targetEngine,destinationPart,memberPolicy);
            }
            else if(item.storageId!=targetId){item.storageId=targetId;state.MarkChanged(item);}
            effect.Cars.AddRange(state.AffectedCars);effect.Engines.AddRange(state.AffectedEngines);
            effect.Owners.AddRange(state.AffectedOwners);
            effect.ChangedParts.AddRange(state.ChangedParts);
            return effect;
        }

        internal static void RequireAvailable(PartMoveCarState car)
        {EngineRebuilds.RequireAvailable(car!=null,car?.GarageJobId,car?.RebuildActive??false);}
        internal static PartMoveCarState MutableCar(InstalledPartTransitionState state,string carId)
        {
            var car=state.FindCar(carId);
            MutableCar(state,car);
            return car;
        }
        static void MutableCar(InstalledPartTransitionState state,PartMoveCarState car)
        {
            if(car==null)throw new InvalidOperationException("Owned car no longer exists.");RequireAvailable(car);
            if(state.HasUnsettledRun)throw new InvalidOperationException("Close the active run before changing hardware.");
        }
        static PartMoveCarState FindVehicle(InstalledPartTransitionState state,string id)
        {var car=state.FindCar(id);if(car==null)throw new InvalidOperationException("Vehicle no longer exists.");RequireAvailable(car);return car;}
        internal static PartMoveEngineState Stored(InstalledPartTransitionState state,string id)
        {
            if(state.HasUnsettledRun)throw new InvalidOperationException("Close the active run first.");
            var engine=state.FindEngine(id)??throw new InvalidOperationException("Engine no longer exists.");
            if(!string.IsNullOrEmpty(engine.ParentVehicleId))throw new InvalidOperationException("Detach the engine before editing its stored assembly.");
            return engine;
        }

        internal static PartInstance RemoveCarPart(InstalledPartTransitionState state,PartMoveCarState car,string slot)
        {
            RequireAvailable(car);
            var binding=car.Bindings.FirstOrDefault(row=>row.slot==slot);if(binding==null)throw new InvalidOperationException("Slot is empty.");
            var item=state.FindPart(binding.partInstanceId);item.location=PartLocation.Inventory;item.ownerId=item.slot="";car.Bindings.Remove(binding);state.MarkChanged(item);return item;
        }
        internal static PartInstance InstallCarPart(InstalledPartTransitionState state,MechanicalCatalog catalog,
            PartMoveCarState car,PartInstance item)
        {
            RequireAvailable(car);
            var definition=catalog.Find(item.definitionId);
            if(item.location!=PartLocation.Inventory||!catalog.Fits(catalog.Spec(car.VehicleId),definition))
                throw new InvalidOperationException("Part must be loose and compatible.");
            if(MechanicalCatalog.EngineSlot(definition.category)&&string.IsNullOrEmpty(car.EngineAssemblyId))
                throw new InvalidOperationException("Attach an engine before installing its parts.");
            string slot=definition.category.ToString();var old=car.Bindings.FirstOrDefault(binding=>binding.slot==slot);PartInstance displaced=null;
            if(old!=null){displaced=RemoveCarPart(state,car,slot);}
            item.location=MechanicalCatalog.EngineSlot(definition.category)?PartLocation.Assembly:PartLocation.Vehicle;
            item.ownerId=item.location==PartLocation.Assembly?car.EngineAssemblyId:car.Id;item.slot=slot;
            car.Bindings.Add(new SlotBinding{slot=slot,partInstanceId=item.instanceId});state.MarkChanged(item);ProjectRanks(state,catalog,car);return displaced;
        }
        internal static PartMoveEngineState RemoveEnginePart(InstalledPartTransitionState state,string engineId,PartCategory category)
        {
            var engine=Stored(state,engineId);var binding=engine.Bindings.FirstOrDefault(row=>row.slot==category.ToString())??throw new InvalidOperationException("Stored slot is empty.");
            var item=state.FindPart(binding.partInstanceId);item.location=PartLocation.Inventory;item.ownerId=item.slot="";state.MarkEngineChanged(engine);engine.Bindings.Remove(binding);state.MarkChanged(item);return engine;
        }
        internal static PartInstance InstallEnginePartAfterStored(InstalledPartTransitionState state,MechanicalCatalog catalog,
            PartMoveEngineState current,PartInstance item,PartMoveMemberPolicy memberPolicy)
        {
            var definition=catalog.Find(item.definitionId);
            if(item.location!=PartLocation.Inventory||!MechanicalCatalog.EngineSlot(definition.category)||
                !catalog.Fits(catalog.Spec(current.VehicleDefinitionId),definition))
                throw new InvalidOperationException("Select a compatible loose engine part.");
            PartInstance displaced=null;var old=current.Bindings.FirstOrDefault(binding=>binding.slot==definition.category.ToString());
            if(old!=null){RemoveEnginePart(state,current.Id,definition.category);displaced=state.FindPart(old.partInstanceId);}
            item.location=PartLocation.Assembly;item.ownerId=current.Id;item.slot=definition.category.ToString();
            state.MarkEngineChanged(current);current.Bindings.Add(new SlotBinding{slot=item.slot,partInstanceId=item.instanceId});state.MarkChanged(item);
            var spec=catalog.Resolve(current.VehicleDefinitionId,state.EngineDefinitions(current),current.Tune).spec;
            if(memberPolicy==PartMoveMemberPolicy.ProjectionRequireExisting)EngineElements.EnsureExisting(spec,definition,item);
            else EngineElements.Ensure(spec,definition,item);
            return displaced;
        }
        internal static void ProjectRanks(InstalledPartTransitionState state,MechanicalCatalog catalog,PartMoveCarState car)
        {
            var resolved=catalog.Resolve(car.VehicleId,state.CarDefinitions(car),car.Tune).tune;
            var target=car.Tune;target.engineLevel=resolved.engineLevel;target.tireLevel=resolved.tireLevel;target.weightLevel=resolved.weightLevel;
            target.gearboxLevel=resolved.gearboxLevel;target.exhaustLevel=resolved.exhaustLevel;target.controlLevel=resolved.controlLevel;
            target.inductionUpgrade=resolved.inductionUpgrade;target.transmissionUpgrade=resolved.transmissionUpgrade;target.ApplyAdjustments(resolved);
            car.Tune=target;
        }
    }
}
