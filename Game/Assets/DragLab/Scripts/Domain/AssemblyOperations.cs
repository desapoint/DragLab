using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    // A stored engine owns its real descendants. Blueprints never own physical items.
    public static class AssemblyOperations
    {
        public static string DisplayName(PlayerProfile p,EngineAssemblyInstance engine)=>string.IsNullOrWhiteSpace(engine.customName)?p.catalog.Spec(engine.vehicleDefinitionId).displayName+" engine":engine.customName;
        public static void Rename(PlayerProfile p,string id,string name){if(p.runCheckpoints.Any(r=>!r.settled))throw new InvalidOperationException("Close the active run before renaming an engine.");var engine=p.engines.Find(e=>e.instanceId==id)??throw new InvalidOperationException("Engine no longer exists.");engine.customName=VehicleCollection.NormalizeName(name);}
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
            using(var state=new ProfileInstalledPartTransitionState(p,false))
            using(var catalog=CartApplyCatalogReaders.Live(p.catalog))
                return InstalledEngineTransition.Detach(state,carId,catalog);
        }
        public static bool Fits(EngineAssemblyInstance engine, OwnedCar car)
        { return InstalledEngineTransition.Fits(engine.vehicleDefinitionId,car.vehicleId); } // Declared engine mounts; cross-platform conversions need authored adapters.
        public static void Attach(PlayerProfile p, string engineId, string carId)
        {
            using(var state=new ProfileInstalledPartTransitionState(p,false))
            using(var catalog=CartApplyCatalogReaders.Live(p.catalog))
                InstalledEngineTransition.Attach(state,engineId,carId,catalog);
        }
        public static void RemovePart(PlayerProfile p, string engineId, PartCategory category)
        {
            using(var state=new ProfileInstalledPartTransitionState(p,false))
                InstalledPartTransition.RemoveEnginePart(state,engineId,category);
        }
        public static void InstallPart(PlayerProfile p, string engineId, string partId)
        {
            using(var state=new ProfileInstalledPartTransitionState(p,false))
            {
                var engine=InstalledPartTransition.Stored(state,engineId);
                var item=state.FindPart(partId)??throw new InvalidOperationException("Part no longer exists.");
                using(var catalog=CartApplyCatalogReaders.Live(p.catalog))
                    InstalledPartTransition.InstallEnginePartAfterStored(state,catalog,engine,item,PartMoveMemberPolicy.LegacyEnsure);
            }
        }
        internal static PartCategory Parse(string slot) { return (PartCategory)Enum.Parse(typeof(PartCategory), slot); }
        public static Dictionary<PartCategory, string> Definitions(PlayerProfile p, EngineAssemblyInstance engine)
            =>Definitions(p.parts,engine.slots);
        internal static Dictionary<PartCategory,string> Definitions(IList<PartInstance> parts,IList<SlotBinding> slots)
        { return slots.ToDictionary(s => Parse(s.slot), s => parts.FirstOrDefault(i => i.instanceId == s.partInstanceId).definitionId); }
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
