#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    // Frozen 17c03a9 whole-part Move wrapper and changed transitive mutators.
    // Renames only; catalogue/lookup/member topology factories remain shared unchanged.
    // Unit and whole-engine branches are retained but outside this reference's tested scope.
    internal static class LegacyWholePartMoveReference
    {
        // Frozen SharedInventory.cs: Move
        public static void Move(PlayerProfile p,string assetId,InventoryKind target,string targetId)
        {
            if(p.runCheckpoints.Any(r=>!r.settled))throw new InvalidOperationException("Close the active run before moving inventory.");
            if(target==InventoryKind.Storage&&!p.inventory.storage.Any(s=>s.id==targetId))throw new InvalidOperationException("Unknown storage container.");
            var unit=p.looseEngineElements.Find(u=>u.element.id==assetId);
            if(unit!=null){if(target!=InventoryKind.Storage)throw new InvalidOperationException("Choose a fitted position to install an individual unit.");unit.storageId=targetId;return;}
            var engine=p.engines.Find(e=>e.instanceId==assetId);
            if(engine!=null){if(target==InventoryKind.Car){if(engine.parentVehicleId==targetId)return;if(!string.IsNullOrEmpty(engine.parentVehicleId))AssemblyOperations.Detach(p,engine.parentVehicleId);AssemblyOperations.Attach(p,assetId,targetId);return;}if(target!=InventoryKind.Storage)throw new InvalidOperationException("Engine destination must be storage or a car.");if(!string.IsNullOrEmpty(engine.parentVehicleId))AssemblyOperations.Detach(p,engine.parentVehicleId);engine.storageId=targetId;return;}
            var item=p.parts.Find(i=>i.instanceId==assetId)??throw new InvalidOperationException("Owned item no longer exists.");
            if(target!=InventoryKind.Storage&&target!=InventoryKind.Car&&target!=InventoryKind.Engine)throw new InvalidOperationException("Use a quote/cart to buy or sell.");
            if(item.location!=PartLocation.Inventory){var owner=p.cars.Find(c=>c.installed.slots.Any(s=>s.partInstanceId==assetId));var category=p.catalog.Find(item.definitionId).category;if(owner!=null){MutableCar(p,owner.instanceId);RemoveCar(p,owner.instanceId,category);}else RemoveStored(p,item.ownerId,category);}
            if(target==InventoryKind.Car)InstallCar(p,p.catalog,targetId,assetId);
            else if(target==InventoryKind.Engine)InstallStored(p,targetId,assetId);
            else item.storageId=targetId;
        }
        // Frozen PartOperations.cs: Car
        static OwnedCar PartCar(PlayerProfile profile, string id) { var car=profile.cars.Find(c => c.instanceId == id) ?? throw new InvalidOperationException("Vehicle no longer exists.");RequireAvailable(car);return car; }
        // Frozen PartOperations.cs: Item
        static PartInstance PartItem(PlayerProfile profile, string id) { return profile.parts.Find(p => p.instanceId == id) ?? throw new InvalidOperationException("Part no longer exists."); }
        // Frozen PartOperations.cs: Install
        public static void InstallCar(PlayerProfile profile, MechanicalCatalog catalog, string vehicleId, string itemId) { ReplaceCar(profile, PartCar(profile, vehicleId), PartItem(profile, itemId), catalog); }
        // Frozen PartOperations.cs: Remove
        public static void RemoveCar(PlayerProfile profile, string vehicleId, PartCategory category) { RemoveCarSlot(profile, PartCar(profile, vehicleId), category.ToString()); }
        // Frozen MechanicsOwnership.cs: Definitions
        public static Dictionary<PartCategory, string> CarDefinitions(PlayerProfile profile, OwnedCar car)
        {
            using (var installed = InstalledPartLookup.Capture(profile.parts, car.installed.slots))
                return installed.CopyDefinitions();
        }
        // Frozen MechanicsOwnership.cs: Replace
        public static void ReplaceCar(PlayerProfile profile, OwnedCar car, PartInstance item, MechanicalCatalog catalog)
        {
            RequireAvailable(car);
            var definition = catalog.Find(item.definitionId);
            if (item.location != PartLocation.Inventory || !catalog.Fits(catalog.Spec(car.vehicleId), definition)) throw new InvalidOperationException("Part must be loose and compatible.");
            if(MechanicalCatalog.EngineSlot(definition.category) && string.IsNullOrEmpty(car.engineAssemblyId)) throw new InvalidOperationException("Attach an engine before installing its parts.");
            string slot = definition.category.ToString(); var old = car.installed.slots.Find(s => s.slot == slot);
            if (old != null) RemoveCarSlot(profile, car, slot);
            item.location = MechanicalCatalog.EngineSlot(definition.category) ? PartLocation.Assembly : PartLocation.Vehicle;
            item.ownerId = item.location == PartLocation.Assembly ? car.engineAssemblyId : car.instanceId; item.slot = slot;
            car.installed.slots.Add(new SlotBinding { slot = slot, partInstanceId = item.instanceId });
            ProjectRanks(profile, car, catalog);
        }
        // Frozen MechanicsOwnership.cs: Remove
        public static void RemoveCarSlot(PlayerProfile profile, OwnedCar car, string slot)
        {
            RequireAvailable(car);
            var binding = car.installed.slots.Find(s => s.slot == slot);
            if (binding == null) throw new InvalidOperationException("Slot is empty.");
            var item = profile.parts.Find(p => p.instanceId == binding.partInstanceId);
            item.location = PartLocation.Inventory; item.ownerId = item.slot = ""; car.installed.slots.Remove(binding);
        }
        // Frozen MechanicsOwnership.cs: ProjectRanks
        public static void ProjectRanks(PlayerProfile profile, OwnedCar car, MechanicalCatalog catalog)
        {
            var resolved = catalog.Resolve(car.vehicleId, CarDefinitions(profile, car), car.tune).tune;
            car.tune.engineLevel = resolved.engineLevel; car.tune.tireLevel = resolved.tireLevel; car.tune.weightLevel = resolved.weightLevel;
            car.tune.gearboxLevel = resolved.gearboxLevel; car.tune.exhaustLevel = resolved.exhaustLevel; car.tune.controlLevel = resolved.controlLevel;
            car.tune.inductionUpgrade = resolved.inductionUpgrade; car.tune.transmissionUpgrade = resolved.transmissionUpgrade;
            car.tune.ApplyAdjustments(resolved);
        }
        // Frozen AssemblyOperations.cs: MutableCar
        public static OwnedCar MutableCar(PlayerProfile p, string id)
        {
            var car = p.cars.Find(c => c.instanceId == id) ?? throw new InvalidOperationException("Owned car no longer exists.");
            RequireAvailable(car);
            if (p.runCheckpoints.Any(r => !r.settled)) throw new InvalidOperationException("Close the active run before changing hardware.");
            return car;
        }
        // Frozen AssemblyOperations.cs: Stored
        public static EngineAssemblyInstance Stored(PlayerProfile p, string id)
        {
            if (p.runCheckpoints.Any(r => !r.settled)) throw new InvalidOperationException("Close the active run first.");
            var engine = p.engines.Find(e => e.instanceId == id) ?? throw new InvalidOperationException("Engine no longer exists.");
            if (!string.IsNullOrEmpty(engine.parentVehicleId)) throw new InvalidOperationException("Detach the engine before editing its stored assembly.");
            return engine;
        }
        // Frozen AssemblyOperations.cs: RemovePart
        public static void RemoveStored(PlayerProfile p, string engineId, PartCategory category)
        {
            var engine = Stored(p, engineId); var binding = engine.slots.Find(s => s.slot == category.ToString()) ?? throw new InvalidOperationException("Stored slot is empty.");
            var item = p.parts.Find(i => i.instanceId == binding.partInstanceId);
            item.location = PartLocation.Inventory; item.ownerId = item.slot = ""; engine.slots.Remove(binding);
        }
        // Frozen AssemblyOperations.cs: InstallPart
        public static void InstallStored(PlayerProfile p, string engineId, string partId)
        {
            var engine = Stored(p, engineId); var item = p.parts.Find(i => i.instanceId == partId) ?? throw new InvalidOperationException("Part no longer exists.");
            var d = p.catalog.Find(item.definitionId);
            if (item.location != PartLocation.Inventory || !MechanicalCatalog.EngineSlot(d.category) || !p.catalog.Fits(p.catalog.Spec(engine.vehicleDefinitionId), d)) throw new InvalidOperationException("Select a compatible loose engine part.");
            if (engine.slots.Any(s => s.slot == d.category.ToString())) RemoveStored(p, engineId, d.category);
            item.location = PartLocation.Assembly; item.ownerId = engineId; item.slot = d.category.ToString();
            engine.slots.Add(new SlotBinding { slot = item.slot, partInstanceId = item.instanceId });
            EnsureMembers(p.catalog.Resolve(engine.vehicleDefinitionId,EngineDefinitions(p,engine),engine.tune).spec, d, item);
        }
        // Frozen AssemblyOperations.cs: Parse
        internal static PartCategory Parse(string slot) { return (PartCategory)Enum.Parse(typeof(PartCategory), slot); }
        // Frozen AssemblyOperations.cs: Definitions
        public static Dictionary<PartCategory, string> EngineDefinitions(PlayerProfile p, EngineAssemblyInstance engine)
        { return engine.slots.ToDictionary(s => Parse(s.slot), s => p.parts.Find(i => i.instanceId == s.partInstanceId).definitionId); }
        // Frozen EngineElementService.cs: RequireAvailable
        public static void RequireAvailable(OwnedCar c){if(c==null)throw new InvalidOperationException("Service vehicle no longer exists.");if(!string.IsNullOrEmpty(c.garageJobId))throw new InvalidOperationException("Car and installed components reserved by garage job; claim or cancel it first.");if(c.engineRebuild!=null&&c.engineRebuild.active)throw new InvalidOperationException("Engine reserved at rebuild shop; collect it before modifying/racing this car.");}
        // Frozen EngineElements.cs: Ensure
        public static void EnsureMembers(VehicleSpec v,PartDefinition d,PartInstance p)
        {
            int count=EngineElements.Count(v,d);if(count==0)return;
            if(p.condition.elements!=null&&p.condition.elements.Length>0)
            {
                if(p.condition.elements.Any(e=>!EngineElements.MatchesCategory(d.category,e)))
                    throw new InvalidOperationException("Installed member type does not match "+d.category+". Reset incompatible save.");
                return; // Never recreate existing individual damage.
            }
            p.condition.elements=new EngineElement[count];int valves=PerValve(d.category)?EngineElements.ValveCount(v):1;
            for(int i=0;i<count;i++){var element=EngineElements.Create(d.category);element.id=p.instanceId+"/element/"+i;element.cylinder=i/valves;element.valve=PerValve(d.category)?i%valves:-1;element.role=Role(d.category,i%valves,valves);element.health=p.condition.failed?0:p.condition.health;p.condition.elements[i]=element;}
            if(d.category==PartCategory.Ignition)for(int i=v.combustion.separateSparkPlugs?0:v.sound.cylinders;i<count;i++){var e=p.condition.elements[i];e.role="ignition coil";e.cylinder=EngineElements.Ignition(v)==IgnitionTopology.Distributor?-1:i-(v.combustion.separateSparkPlugs?0:v.sound.cylinders);}
        }
        // Frozen EngineElements.cs: PerValve
        static bool PerValve(PartCategory c){return c==PartCategory.Valves||c==PartCategory.ValveSprings||c==PartCategory.ValveSeals||c==PartCategory.Followers;}
        // Frozen EngineElements.cs: Role
        static string Role(PartCategory c,int valve,int valves){return c==PartCategory.Wheels?"wheel":c==PartCategory.GlowPlugs?"glow plug":c==PartCategory.SparkPlugs||c==PartCategory.Ignition?"spark plug / lead":c==PartCategory.FuelSystem?"injector":c==PartCategory.Bearings?"rod bearing pair":c==PartCategory.PistonRings?"piston ring pack":PerValve(c)?(valve<valves/2?"intake ":"exhaust ")+(c==PartCategory.Valves?"valve / guide":c==PartCategory.ValveSprings?"spring / retainer":c==PartCategory.ValveSeals?"valve seal":"follower / rocker"):PartSemantics.Friendly(c.ToString());}
    }
}
#endif
