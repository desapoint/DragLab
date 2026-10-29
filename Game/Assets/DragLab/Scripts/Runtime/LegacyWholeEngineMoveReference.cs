#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    // Whole-engine Move and Assembly bodies frozen before J from 1c76d62.
    // Prefix/part branches use the earlier independent I reference; no new core calls.
    // Catalogue, Tune.Copy, FluidState and EngineSlot are unchanged shared primitives.
    internal static class LegacyWholeEngineMoveReference
    {
        public static void Move(PlayerProfile p,string assetId,InventoryKind target,string targetId)
        {
            if(p.runCheckpoints.Any(r=>!r.settled))throw new InvalidOperationException("Close the active run before moving inventory.");
            if(target==InventoryKind.Storage&&!p.inventory.storage.Any(s=>s.id==targetId))throw new InvalidOperationException("Unknown storage container.");
            var unit=p.looseEngineElements.Find(u=>u.element.id==assetId);
            if(unit!=null){if(target!=InventoryKind.Storage)throw new InvalidOperationException("Choose a fitted position to install an individual unit.");unit.storageId=targetId;return;}
            var engine=p.engines.Find(e=>e.instanceId==assetId);
            if(engine!=null){if(target==InventoryKind.Car){if(engine.parentVehicleId==targetId)return;if(!string.IsNullOrEmpty(engine.parentVehicleId))Detach(p,engine.parentVehicleId);Attach(p,assetId,targetId);return;}if(target!=InventoryKind.Storage)throw new InvalidOperationException("Engine destination must be storage or a car.");if(!string.IsNullOrEmpty(engine.parentVehicleId))Detach(p,engine.parentVehicleId);engine.storageId=targetId;return;}
            var item=p.parts.Find(i=>i.instanceId==assetId)??throw new InvalidOperationException("Owned item no longer exists.");
            if(target!=InventoryKind.Storage&&target!=InventoryKind.Car&&target!=InventoryKind.Engine)throw new InvalidOperationException("Use a quote/cart to buy or sell.");
            if(item.location!=PartLocation.Inventory){var owner=p.cars.Find(c=>c.installed.slots.Any(s=>s.partInstanceId==assetId));var category=p.catalog.Find(item.definitionId).category;if(owner!=null){LegacyWholePartMoveReference.MutableCar(p,owner.instanceId);LegacyWholePartMoveReference.RemoveCar(p,owner.instanceId,category);}else LegacyWholePartMoveReference.RemoveStored(p,item.ownerId,category);}
            if(target==InventoryKind.Car)LegacyWholePartMoveReference.InstallCar(p,p.catalog,targetId,assetId);
            else if(target==InventoryKind.Engine)LegacyWholePartMoveReference.InstallStored(p,targetId,assetId);
            else item.storageId=targetId;
        }
        public static string Detach(PlayerProfile p, string carId)
        {
            var car = LegacyWholePartMoveReference.MutableCar(p, carId);
            var engine = p.engines.Find(e => e.instanceId == car.engineAssemblyId) ?? throw new InvalidOperationException("This chassis has no engine.");
            engine.slots = car.installed.slots.Where(s => MechanicalCatalog.EngineSlot(LegacyWholePartMoveReference.Parse(s.slot))).ToList();
            car.installed.slots.RemoveAll(s => MechanicalCatalog.EngineSlot(LegacyWholePartMoveReference.Parse(s.slot)));
            engine.fluids = car.fluids; engine.tune = car.tune.Copy();
            car.fluids = new FluidState(); car.engineAssemblyId = ""; engine.parentVehicleId = "";
            LegacyWholePartMoveReference.ProjectRanks(p, car, p.catalog);
            return engine.instanceId;
        }
        public static bool Fits(EngineAssemblyInstance engine, OwnedCar car)
        { return engine.vehicleDefinitionId == car.vehicleId; }
        public static void Attach(PlayerProfile p, string engineId, string carId)
        {
            var car = LegacyWholePartMoveReference.MutableCar(p, carId); var engine = LegacyWholePartMoveReference.Stored(p, engineId);
            if (!Fits(engine, car)) throw new InvalidOperationException("Engine mounts are incompatible with this chassis.");
            if (!string.IsNullOrEmpty(car.engineAssemblyId) || car.installed.slots.Any(s => MechanicalCatalog.EngineSlot(LegacyWholePartMoveReference.Parse(s.slot)))) throw new InvalidOperationException("Detach the current engine first.");
            car.engineAssemblyId = engine.instanceId; engine.parentVehicleId = car.instanceId;
            car.installed.slots.AddRange(engine.slots); engine.slots = new List<SlotBinding>();
            car.fluids = engine.fluids; engine.fluids = null;
            EngineAdjustments(car.tune, engine.tune);
            LegacyWholePartMoveReference.ProjectRanks(p, car, p.catalog);
        }
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
    }
}
#endif
