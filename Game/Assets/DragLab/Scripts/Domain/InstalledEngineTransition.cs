using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    internal static class InstalledEngineTransition
    {
        internal static void MoveAfterPreamble(InstalledPartTransitionState state,ICartCatalogRead catalog,
            PartMovePreamble preamble,InventoryKind target,string targetId)
        {
            var engine=state.FindEngine(preamble.AssetId);
            if(target==InventoryKind.Car)
            {
                if(engine.ParentVehicleId==targetId)return;
                if(!string.IsNullOrEmpty(engine.ParentVehicleId))Detach(state,engine.ParentVehicleId,catalog);
                Attach(state,preamble.AssetId,targetId,catalog);
                return;
            }
            if(target!=InventoryKind.Storage)throw new InvalidOperationException("Engine destination must be storage or a car.");
            if(!string.IsNullOrEmpty(engine.ParentVehicleId))Detach(state,engine.ParentVehicleId,catalog);
            state.MarkAffected(engine);state.MarkEngineChanged(engine);engine.StorageId=targetId;
        }

        internal static string Detach(InstalledPartTransitionState state,string carId,ICartCatalogRead catalog)
        {
            var car=InstalledPartTransition.MutableCar(state,carId);
            var engine=state.FindEngine(car.EngineAssemblyId)??throw new InvalidOperationException("This chassis has no engine.");
            state.MarkAffected(car);state.MarkAffected(engine);state.MarkEngineChanged(engine);
            engine.Bindings=car.Bindings.Where(binding=>MechanicalCatalog.EngineSlot(AssemblyOperations.Parse(binding.slot))).ToList();
            ((List<SlotBinding>)car.Bindings).RemoveAll(binding=>MechanicalCatalog.EngineSlot(AssemblyOperations.Parse(binding.slot)));
            engine.Fluids=car.Fluids;engine.Tune=car.Tune.Copy();
            car.Fluids=new FluidState();car.EngineAssemblyId="";engine.ParentVehicleId="";
            InstalledPartTransition.ProjectRanks(state,catalog,car);
            return engine.Id;
        }

        internal static void Attach(InstalledPartTransitionState state,string engineId,string carId,ICartCatalogRead catalog)
        {
            var car=InstalledPartTransition.MutableCar(state,carId);
            var engine=InstalledPartTransition.Stored(state,engineId);
            if(!Fits(engine.VehicleDefinitionId,car.VehicleId))throw new InvalidOperationException("Engine mounts are incompatible with this chassis.");
            if(!string.IsNullOrEmpty(car.EngineAssemblyId)||car.Bindings.Any(binding=>MechanicalCatalog.EngineSlot(AssemblyOperations.Parse(binding.slot))))
                throw new InvalidOperationException("Detach the current engine first.");
            state.MarkAffected(car);state.MarkAffected(engine);state.MarkEngineChanged(engine);
            car.EngineAssemblyId=engine.Id;engine.ParentVehicleId=car.Id;
            ((List<SlotBinding>)car.Bindings).AddRange(engine.Bindings);engine.Bindings=new List<SlotBinding>();
            car.Fluids=engine.Fluids;engine.Fluids=null;
            AssemblyOperations.EngineAdjustments(car.Tune,engine.Tune);
            InstalledPartTransition.ProjectRanks(state,catalog,car);
        }

        internal static bool Fits(string engineVehicleDefinitionId,string carVehicleId)
            =>engineVehicleDefinitionId==carVehicleId;
    }
}
