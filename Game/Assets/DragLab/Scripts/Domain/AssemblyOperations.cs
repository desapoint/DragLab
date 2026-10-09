using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    // A stored engine owns its real descendants. Blueprints never own physical items.
    public static class AssemblyOperations
    {
        public static OwnedCar MutableCar(PlayerProfile p, string id)
        {
            var car = p.cars.Find(c => c.instanceId == id) ?? throw new InvalidOperationException("Owned car no longer exists.");
            EngineRebuilds.RequireAvailable(car);
            if (p.runCheckpoints.Any(r => !r.settled)) throw new InvalidOperationException("Close the active run before changing hardware.");
            return car;
        }
        public static EngineAssemblyInstance Stored(PlayerProfile p, string id)
        {
            if (p.runCheckpoints.Any(r => !r.settled)) throw new InvalidOperationException("Close the active run first.");
            var engine = p.engines.Find(e => e.instanceId == id) ?? throw new InvalidOperationException("Engine no longer exists.");
            if (!string.IsNullOrEmpty(engine.parentVehicleId)) throw new InvalidOperationException("Detach the engine before editing its stored assembly.");
            return engine;
        }
        public static string Detach(PlayerProfile p, string carId)
        {
            var car = MutableCar(p, carId);
            var engine = p.engines.Find(e => e.instanceId == car.engineAssemblyId) ?? throw new InvalidOperationException("This chassis has no engine.");
            engine.slots = car.installed.slots.Where(s => MechanicalCatalog.EngineSlot(Parse(s.slot))).ToList();
            car.installed.slots.RemoveAll(s => MechanicalCatalog.EngineSlot(Parse(s.slot)));
            engine.fluids = car.fluids; engine.tune = car.tune.Copy();
            car.fluids = new FluidState(); car.engineAssemblyId = ""; engine.parentVehicleId = "";
            MechanicsOwnership.ProjectRanks(p, car, p.catalog);
            return engine.instanceId;
        }
        public static bool Fits(EngineAssemblyInstance engine, OwnedCar car)
        { return engine.vehicleDefinitionId == car.vehicleId; } // Declared engine mounts; cross-platform conversions need authored adapters.
        public static void Attach(PlayerProfile p, string engineId, string carId)
        {
            var car = MutableCar(p, carId); var engine = Stored(p, engineId);
            if (!Fits(engine, car)) throw new InvalidOperationException("Engine mounts are incompatible with this chassis.");
            if (!string.IsNullOrEmpty(car.engineAssemblyId) || car.installed.slots.Any(s => MechanicalCatalog.EngineSlot(Parse(s.slot)))) throw new InvalidOperationException("Detach the current engine first.");
            car.engineAssemblyId = engine.instanceId; engine.parentVehicleId = car.instanceId;
            car.installed.slots.AddRange(engine.slots); engine.slots = new List<SlotBinding>();
            car.fluids = engine.fluids; engine.fluids = null;
            EngineAdjustments(car.tune, engine.tune);
            MechanicsOwnership.ProjectRanks(p, car, p.catalog);
        }
        public static void RemovePart(PlayerProfile p, string engineId, PartCategory category)
        {
            var engine = Stored(p, engineId); var binding = engine.slots.Find(s => s.slot == category.ToString()) ?? throw new InvalidOperationException("Stored slot is empty.");
            var item = p.parts.Find(i => i.instanceId == binding.partInstanceId);
            item.location = PartLocation.Inventory; item.ownerId = item.slot = ""; engine.slots.Remove(binding);
        }
        public static void InstallPart(PlayerProfile p, string engineId, string partId)
        {
            var engine = Stored(p, engineId); var item = p.parts.Find(i => i.instanceId == partId) ?? throw new InvalidOperationException("Part no longer exists.");
            var d = p.catalog.Find(item.definitionId);
            if (item.location != PartLocation.Inventory || !MechanicalCatalog.EngineSlot(d.category) || !p.catalog.Fits(p.catalog.Spec(engine.vehicleDefinitionId), d)) throw new InvalidOperationException("Select a compatible loose engine part.");
            if (engine.slots.Any(s => s.slot == d.category.ToString())) RemovePart(p, engineId, d.category);
            item.location = PartLocation.Assembly; item.ownerId = engineId; item.slot = d.category.ToString();
            engine.slots.Add(new SlotBinding { slot = item.slot, partInstanceId = item.instanceId });
            EngineElements.Ensure(p.catalog.Spec(engine.vehicleDefinitionId), d, item);
        }
        internal static PartCategory Parse(string slot) { return (PartCategory)Enum.Parse(typeof(PartCategory), slot); }
        public static Dictionary<PartCategory, string> Definitions(PlayerProfile p, EngineAssemblyInstance engine)
        { return engine.slots.ToDictionary(s => Parse(s.slot), s => p.parts.Find(i => i.instanceId == s.partInstanceId).definitionId); }
        public static void EngineAdjustments(Tune target, Tune source)
        {
            if (source == null) return;
            target.camTimingDegrees = source.camTimingDegrees; target.blowOffValve = source.blowOffValve;
            target.boostSetting = source.boostSetting; target.superchargerPulley = source.superchargerPulley;
            target.turboSize = source.turboSize; target.flywheelMass = source.flywheelMass;
            target.launchRpm = source.launchRpm; target.shiftRpm = source.shiftRpm;
            target.twoStep = source.twoStep; target.launchPreload = source.launchPreload; target.antiLag = source.antiLag;
            target.limiter = source.limiter; target.limiterCut = source.limiterCut; target.revLimitRpm = source.revLimitRpm;
            target.twoStepStyle = source.twoStepStyle; target.twoStepCut = source.twoStepCut; target.shiftLightRpm = source.shiftLightRpm; target.coldStart = source.coldStart;
        }
        public static string[] Missing(PlayerProfile p, EngineAssemblyInstance engine)
        {
            var categories = engine.slots.Select(s => Parse(s.slot)).ToArray();
            return p.catalog.Recipe(engine.vehicleDefinitionId, new Tune()).Keys.Where(MechanicalCatalog.EngineSlot).Where(c => !categories.Contains(c)).Select(c => PartSemantics.Friendly(c.ToString())).ToArray();
        }
        public static AssetRetentionPreview PreviewKeepEngine(PlayerProfile p, string carId)
        {
            var car = p.cars.Find(c => c.instanceId == carId) ?? throw new InvalidOperationException("Car no longer exists.");
            return new AssetRetentionPreview {
                retainedEngineId = car.engineAssemblyId,
                retainedParts = car.installed.slots.Where(s => MechanicalCatalog.EngineSlot(Parse(s.slot))).Select(s => s.partInstanceId).ToArray(),
                remainingChassisId = car.chassisId,
                remainingParts = car.installed.slots.Where(s => !MechanicalCatalog.EngineSlot(Parse(s.slot))).Select(s => s.partInstanceId).ToArray()
            };
        }
    }
    public sealed class AssetRetentionPreview
    {
        public string retainedEngineId, remainingChassisId;
        public string[] retainedParts, remainingParts;
    }
}
