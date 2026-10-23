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
        internal static Action<string,double> Measurement;
        static long Stamp()=>Measurement==null?0:System.Diagnostics.Stopwatch.GetTimestamp();
        static void Measured(string phase,long stamp){if(stamp!=0)Measurement?.Invoke(phase,(System.Diagnostics.Stopwatch.GetTimestamp()-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency);}
        public static MechanicalCatalog Catalog() { return new MechanicalCatalog(Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Select(v => v.spec)); }
        public static bool Try(PlayerProfile current, MechanicalCatalog catalog, long expectedRevision, Action<PlayerProfile> operation,
            Func<PlayerProfile, bool> persist, out PlayerProfile committed, out string error)
        {
            committed = current;
            try
            {
                if (current.revision != expectedRevision) throw new InvalidOperationException("Stale action; refresh the garage.");
                long stamp=Stamp();var candidate = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(current));candidate.storageCommit=current.storageCommit;Measured("clone",stamp);stamp=Stamp();
                MechanicsOwnership.Ensure(candidate, catalog);Measured("normalize before operation",stamp);stamp=Stamp();
                operation(candidate); candidate.revision = current.revision + 1;Measured("operation",stamp);stamp=Stamp();
                // New purchased packs/vehicles must be initialized before either a real or a test
                // persister sees the candidate. A later normalization must not change a fresh quote.
                MechanicsOwnership.Ensure(candidate, catalog);Measured("normalize after operation",stamp);stamp=Stamp();
                if (!persist(candidate)) throw new InvalidOperationException("Save failed; operation rolled back.");Measured("persistence",stamp);
                committed = candidate; error = null; return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        public static void ApplyCheckpoint(PlayerProfile profile, RunCheckpoint checkpoint)
        {
            var targets = ValidateCheckpointTargets(profile, checkpoint, out var car);
            // Validation returns invocation-owned handles, with its lookup already disposed.
            // No identity/list mutation or external callback occurs between capture and use.
            // Prepare all affected values before assignments; malformed prior resources must
            // not fail receipt construction after some conditions have already been applied.
            var conditions = checkpoint.components.Select(p => p.condition.Copy()).ToArray();
            var fluids = checkpoint.fluids == null ? car.fluids : checkpoint.fluids.Copy();
            var fuel = checkpoint.hasFuel ? checkpoint.fuel.Copy() : car.fuel;
            if (fluids == null || fuel == null)
                throw new InvalidOperationException("Checkpoint requires existing fuel and fluid state.");
            var receipt = new RunCheckpoint { runId=checkpoint.runId, ownedVehicleId=checkpoint.ownedVehicleId,
                revision=profile.revision+1, tick=checkpoint.tick, thermalUtcTicks=checkpoint.thermalUtcTicks,
                hasFuel=checkpoint.hasFuel, fuel=fuel.Copy(), fluids=fluids.Copy(),
                settled=checkpoint.settled, closedReason=checkpoint.closedReason };
            var old = profile.runCheckpoints.Find(r => r.runId == checkpoint.runId);
            for (int i = 0; i < targets.Length; i++)
                targets[i].condition = conditions[i];
            if(checkpoint.fluids!=null)car.fluids=fluids;
            car.thermalUtcTicks=Math.Max(car.thermalUtcTicks,checkpoint.thermalUtcTicks);
            if(checkpoint.hasFuel)car.fuel=fuel;
            if (old != null) profile.runCheckpoints.Remove(old);
            // Receipts contain no duplicate component trees; conditions live in the owned ledger.
            profile.runCheckpoints.Add(receipt);
        }
        // Validate every field before touching the ledger, also used by lightweight race snapshots.
        public static void ValidateCheckpoint(PlayerProfile profile, RunCheckpoint checkpoint)
            => ValidateCheckpointTargets(profile, checkpoint, out _);

        static PartInstance[] ValidateCheckpointTargets(PlayerProfile profile, RunCheckpoint checkpoint,
            out OwnedCar car)
        {
            car = profile.cars.Find(c => c.instanceId == checkpoint.ownedVehicleId);
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
            var targets = new PartInstance[checkpoint.components.Count];
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var requestedIds = new string[checkpoint.components.Count];
            for (int i = 0; i < checkpoint.components.Count; i++)
            {
                var snapshot = checkpoint.components[i];
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.instanceId) || !seen.Add(snapshot.instanceId))
                    throw new InvalidOperationException("Checkpoint has a missing or duplicate component identity.");
                requestedIds[i] = snapshot.instanceId;
            }
            // This index belongs only to this synchronous invocation. Existing detached
            // settlement candidates may call here on their owner worker; live profiles
            // remain main-owned. Never retain it across callbacks or candidate mutations.
            // The source already has validated ownership topology. Capture only requested
            // identities and stop once found, rather than index unrelated loose inventory.
            using (var index = OwnedPartIdentityIndex.BuildRequested(profile.parts, requestedIds))
            {
                for (int i = 0; i < checkpoint.components.Count; i++)
                {
                    var snapshot = checkpoint.components[i];
                    var item = index.TryGetPosition(snapshot.instanceId, out int position) ? profile.parts[position] : null;
                    if (item == null || !car.installed.slots.Any(b=>b.partInstanceId==snapshot.instanceId) || item.ownerId != car.instanceId && item.ownerId != car.engineAssemblyId || snapshot.definitionId != item.definitionId || snapshot.slot!=item.slot || snapshot.ownerId!=item.ownerId || snapshot.condition == null || snapshot.condition.Validate() != null) throw new InvalidOperationException("Checkpoint has invalid identity/condition for "+snapshot.slot+".");
                    if(!EngineElements.CanAdvance(item.condition,snapshot.condition))throw new InvalidOperationException("Checkpoint rewinds individual elements: "+snapshot.slot);
                    if(!RunPersistence.ConditionAdvances(item.condition,snapshot.condition))throw new InvalidOperationException("Checkpoint rewinds mechanical history/condition: "+snapshot.slot);
                    targets[i] = item;
                }
            }
            if(checkpoint.thermalUtcTicks<0||checkpoint.thermalUtcTicks>DateTime.MaxValue.Ticks)throw new InvalidOperationException("Invalid thermal clock.");
            return targets;
        }
    }
}
