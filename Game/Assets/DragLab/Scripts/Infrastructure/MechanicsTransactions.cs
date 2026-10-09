using System;
using System.Linq;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    public static class MechanicsTransactions
    {
        public static MechanicalCatalog Catalog() { return new MechanicalCatalog(Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Select(v => v.spec)); }
        public static bool Try(PlayerProfile current, MechanicalCatalog catalog, long expectedRevision, Action<PlayerProfile> operation,
            Func<PlayerProfile, bool> persist, out PlayerProfile committed, out string error)
        {
            committed = current;
            try
            {
                if (current.revision != expectedRevision) throw new InvalidOperationException("Stale action; refresh the garage.");
                var candidate = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(current));
                MechanicsOwnership.Ensure(candidate, catalog);
                operation(candidate); candidate.revision = current.revision + 1;
                // New purchased packs/vehicles must be initialized before either a real or a test
                // persister sees the candidate. A later normalization must not change a fresh quote.
                MechanicsOwnership.Ensure(candidate, catalog);
                string invalid = MechanicsOwnership.Validate(candidate, catalog); if (invalid != null) throw new InvalidOperationException(invalid);
                if (!persist(candidate)) throw new InvalidOperationException("Save failed; operation rolled back.");
                committed = candidate; error = null; return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        public static void ApplyCheckpoint(PlayerProfile profile, RunCheckpoint checkpoint)
        {
            var car = profile.cars.Find(c => c.instanceId == checkpoint.ownedVehicleId);
            if(car!=null)EngineRebuilds.RequireAvailable(car);
            if (car == null || checkpoint.version != MechanicsSchema.Version || checkpoint.model != MechanicsSchema.Model || checkpoint.tick < 0 || checkpoint.components == null || string.IsNullOrWhiteSpace(checkpoint.runId)) throw new InvalidOperationException("Invalid checkpoint owner/model/tick.");
            var old = profile.runCheckpoints.Find(r => r.runId == checkpoint.runId);
            if (old != null && (old.ownedVehicleId != checkpoint.ownedVehicleId || old.settled || checkpoint.tick < old.tick || checkpoint.tick == old.tick && !checkpoint.settled)) throw new InvalidOperationException("Checkpoint already settled or stale.");
            if(checkpoint.hasFuel){
                var f=checkpoint.fuel;var prior=car.fuel;
                if(f==null || f.Validate(profile.catalog.Spec(car.vehicleId).fuel)!=null || f.litres>prior.litres+1e-9 || RunPersistence.HistoryLess(f.cumulativeUsedLitres,prior.cumulativeUsedLitres) || checkpoint.components.Count!=car.installed.slots.Count || checkpoint.fluids==null)throw new InvalidOperationException("Run checkpoint must capture complete installed resources and cannot refill or rewind fuel.");
            }
            if(checkpoint.fluids!=null) {
                var f=checkpoint.fluids;var prior=car.fluids;
                if(f.Validate(profile.catalog.Spec(car.vehicleId).fluids)!=null || f.oilLitres>prior.oilLitres+1e-9 || f.coolantLitres>prior.coolantLitres+1e-9 || RunPersistence.HistoryLess(f.oilConsumedLitres,prior.oilConsumedLitres) || RunPersistence.HistoryLess(f.oilLeakedLitres,prior.oilLeakedLitres) || RunPersistence.HistoryLess(f.coolantLostLitres,prior.coolantLostLitres) || RunPersistence.HistoryLess(f.serviceElapsedSeconds,prior.serviceElapsedSeconds) || f.oilCondition>prior.oilCondition+1e-9) throw new InvalidOperationException("Checkpoint cannot refill, renew or rewind fluid history.");
            }
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var snapshot in checkpoint.components)
            {
                var item = profile.parts.Find(p => p.instanceId == snapshot.instanceId);
                if (!seen.Add(snapshot.instanceId) || item == null || !car.installed.slots.Any(b=>b.partInstanceId==snapshot.instanceId) || item.ownerId != car.instanceId && item.ownerId != car.engineAssemblyId || snapshot.definitionId != item.definitionId || snapshot.slot!=item.slot || snapshot.ownerId!=item.ownerId || snapshot.condition.Validate() != null) throw new InvalidOperationException("Checkpoint has invalid identity/condition for "+snapshot.slot+".");
                if(!EngineElements.CanAdvance(item.condition,snapshot.condition))throw new InvalidOperationException("Checkpoint rewinds individual elements: "+snapshot.slot);
                if(!RunPersistence.ConditionAdvances(item.condition,snapshot.condition))throw new InvalidOperationException("Checkpoint rewinds mechanical history/condition: "+snapshot.slot);
                item.condition = snapshot.condition.Copy();
            }
            if(checkpoint.fluids!=null)car.fluids=checkpoint.fluids.Copy();
            if(checkpoint.hasFuel)car.fuel=new FuelState{litres=checkpoint.fuel.litres,cumulativeUsedLitres=checkpoint.fuel.cumulativeUsedLitres,flowLitresPerSecond=checkpoint.fuel.flowLitresPerSecond};
            checkpoint.revision = profile.revision + 1;
            if (old != null) profile.runCheckpoints.Remove(old);
            var receipt=JsonUtility.FromJson<RunCheckpoint>(JsonUtility.ToJson(checkpoint));
            // Current resources already live in the owned ledger; receipts need no duplicate element trees.
            receipt.components.Clear();profile.runCheckpoints.Add(receipt);
        }
    }
}
