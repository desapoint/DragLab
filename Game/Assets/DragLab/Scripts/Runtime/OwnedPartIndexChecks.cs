using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DragLab.Runtime
{
#if UNITY_EDITOR
    /// <summary>Adversarial managed checks and bounded scaling probe for the one-shot owned-part identity index.</summary>
    public static class OwnedPartIndexChecks
    {
        const int Repeats = 5;
        static readonly StringBuilder Checks = new StringBuilder();
        static readonly StringBuilder Timings = new StringBuilder();
        static int passed;

        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("S02a index check failed: " + name);
            passed++;
            Checks.AppendLine("PASS " + name);
        }

        static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T) { passed++; Checks.AppendLine("PASS " + name); return; }
            throw new InvalidOperationException("S02a index check failed (expected " + typeof(T).Name + "): " + name);
        }

        static PartInstance Part(string id, float health = .9f)
        {
            return new PartInstance
            {
                instanceId = id,
                definitionId = "fixture-definition",
                location = PartLocation.Inventory,
                condition = new ComponentCondition { health = health, runningSeconds = 10, distanceM = 250, stressExposure = .25f }
            };
        }

        static List<PartInstance> Fixture(int count, string namespaceId)
        {
            var parts = new List<PartInstance>(count);
            for (int i = 0; i < count; i++)
                parts.Add(Part(namespaceId + "/part-" + i.ToString("D5", CultureInfo.InvariantCulture)));
            return parts;
        }

        static IEnumerable<string> ExcessRequestedIds(Action onYield)
        {
            for (int i = 0; ; i++)
            {
                onYield();
                yield return "requested-" + i.ToString(CultureInfo.InvariantCulture);
            }
        }

        static PartInstance Scan(List<PartInstance> parts, string id)
        {
            return parts.Find(part => part != null && string.Equals(part.instanceId, id, StringComparison.Ordinal));
        }

        static void CheckDifferentialAndInvalidInputs()
        {
            var parts = Fixture(128, "oracle");
            using (var index = OwnedPartIdentityIndex.Build(parts))
            {
                Check(index.Count == parts.Count, "index count matches source list");
                for (int i = 0; i < parts.Count; i++)
                {
                    Check(index.TryGetPosition(parts[i].instanceId, out int position) && position == i, "index position matches ordered scan at " + i);
                    Check(ReferenceEquals(parts[position], Scan(parts, parts[i].instanceId)), "indexed part reference matches List.Find at " + i);
                }
                Check(!index.TryGetPosition("oracle/missing", out int missingPosition) && missingPosition == -1, "missing key returns false and position -1");
                Check(!index.TryGetPosition(null, out missingPosition) && missingPosition == -1, "null query is absent");
                Check(!index.TryGetPosition("", out missingPosition) && missingPosition == -1, "empty query is absent");
                Check(!index.TryGetPosition("ORACLE/part-00001", out missingPosition), "identity lookup is ordinal and case-sensitive");
            }

            Throws<ArgumentNullException>(() => OwnedPartIdentityIndex.Build(null), "null source list rejected");
            Throws<ArgumentException>(() => OwnedPartIdentityIndex.Build(new List<PartInstance> { Part("dup"), Part("dup") }), "duplicate IDs rejected");
            Throws<ArgumentException>(() => OwnedPartIdentityIndex.Build(new List<PartInstance> { Part("ok"), null }), "null part entries rejected");
            Throws<ArgumentException>(() => OwnedPartIdentityIndex.Build(new List<PartInstance> { Part(null) }), "null instance IDs rejected");
            Throws<ArgumentException>(() => OwnedPartIdentityIndex.Build(new List<PartInstance> { Part("  ") }), "blank instance IDs rejected");
            var disposed = OwnedPartIdentityIndex.Build(Fixture(1, "disposed"));
            disposed.Dispose();
            Throws<ObjectDisposedException>(() => disposed.TryGetPosition("disposed/part-00000", out _), "disposed index cannot be queried");
            disposed.Dispose();
            Check(true, "repeated dispose is safe");

            var requested = new[] { parts[0].instanceId, parts[31].instanceId, parts[127].instanceId, "oracle/not-present" };
            var requestedPositions = new[] { 0, 31, 127, -1 };
            using (var sparse = OwnedPartIdentityIndex.BuildRequested(parts, requested))
            {
                Check(sparse.Count == 3, "requested builder stores only found requested identities");
                BenchmarkIndexQueries(sparse, requested, requestedPositions, "sparse fixture");
                Check(!sparse.TryGetPosition(parts[1].instanceId, out int unrequested) && unrequested == -1, "requested builder excludes present but unrequested IDs");
            }
            Throws<ArgumentNullException>(() => OwnedPartIdentityIndex.BuildRequested(null, requested), "requested builder rejects null source list");
            Throws<ArgumentNullException>(() => OwnedPartIdentityIndex.BuildRequested(parts, null), "requested builder rejects null request set");
            Throws<ArgumentException>(() => OwnedPartIdentityIndex.BuildRequested(parts, new[] { (string)null }), "requested builder rejects null IDs");
            Throws<ArgumentException>(() => OwnedPartIdentityIndex.BuildRequested(parts, new[] { " " }), "requested builder rejects blank IDs");
            Throws<ArgumentException>(() => OwnedPartIdentityIndex.BuildRequested(parts, new[] { requested[0], requested[0] }), "requested builder rejects duplicate requested IDs");
            Throws<ArgumentOutOfRangeException>(() => OwnedPartIdentityIndex.BuildRequested(new List<PartInstance> { Part("only") }, new[] { "one", "two" }), "requested count cannot exceed source count");
            int requestItemsEnumerated = 0;
            Throws<ArgumentOutOfRangeException>(() => OwnedPartIdentityIndex.BuildRequested(new List<PartInstance> { Part("one"), Part("two") }, ExcessRequestedIds(() => requestItemsEnumerated++)), "unbounded requested ID source stops at source count plus one");
            Check(requestItemsEnumerated == 3, "requested ID bound consumes no more than source count plus one sentinel");
            using (var earlyExit = OwnedPartIdentityIndex.BuildRequested(new List<PartInstance> { Part("found-before-tail"), null }, new[] { "found-before-tail" }))
                Check(earlyExit.Count == 1 && earlyExit.TryGetPosition("found-before-tail", out int earlyPosition) && earlyPosition == 0, "requested builder may stop after final match without validating unscanned tail");
            using (var duplicateTail = OwnedPartIdentityIndex.BuildRequested(new List<PartInstance> { Part("first-match"), Part("first-match") }, new[] { "first-match" }))
                Check(duplicateTail.Count == 1, "requested builder cannot certify duplicate-free unscanned tail");
            Throws<ArgumentException>(() => OwnedPartIdentityIndex.BuildRequested(new List<PartInstance> { Part("prefix"), null, Part("target") }, new[] { "target" }), "requested builder rejects malformed rows encountered before requested match");
            Throws<ArgumentException>(() => OwnedPartIdentityIndex.BuildRequested(new List<PartInstance> { Part("prefix"), Part("") }, new[] { "absent" }), "missing requested ID scan rejects blank IDs at end of source");
            Throws<ArgumentException>(() => OwnedPartIdentityIndex.BuildRequested(new List<PartInstance> { Part("prefix"), null }, new[] { "absent" }), "missing requested ID forces scan through and rejects malformed tail");
            Throws<ArgumentException>(() => OwnedPartIdentityIndex.BuildRequested(new List<PartInstance> { Part("wanted"), Part("wanted"), Part("later") }, new[] { "wanted", "later" }), "requested builder rejects duplicate requested identity encountered before remaining match");
            using (var missingOnly = OwnedPartIdentityIndex.BuildRequested(new List<PartInstance> { Part("unrequested-duplicate"), Part("unrequested-duplicate") }, new[] { "absent" }))
                Check(missingOnly.Count == 0 && !missingOnly.TryGetPosition("absent", out _), "unrequested duplicate IDs do not claim requested-key validation");
        }

        static void CheckCandidateLocalStructuralChanges()
        {
            var source = Fixture(4, "source");
            var sourceIds = source.Select(part => part.instanceId).ToArray();
            var sourceIndex = OwnedPartIdentityIndex.Build(source);
            try
            {
                var candidate = new List<PartInstance>(source);
                candidate[1] = Part("source/replacement-at-same-count");
                candidate.Add(Part("source/added"));
                candidate.RemoveAt(0);
                using (var candidateIndex = OwnedPartIdentityIndex.Build(candidate))
                {
                    Check(candidate.Count == source.Count, "replacement plus add/remove exercises same-count candidate");
                    for (int i = 0; i < candidate.Count; i++)
                    {
                        Check(candidateIndex.TryGetPosition(candidate[i].instanceId, out int position) && position == i, "rebuilt candidate index matches scan after structural edits " + i);
                        Check(ReferenceEquals(candidate[i], Scan(candidate, candidate[i].instanceId)), "candidate index resolves same object as scan " + i);
                    }
                    Check(!candidateIndex.TryGetPosition(sourceIds[0], out _), "removed identity is absent from candidate index");
                    Check(candidateIndex.TryGetPosition("source/replacement-at-same-count", out _), "replacement identity is present in candidate index");
                    Check(candidateIndex.TryGetPosition("source/added", out _), "added identity is present in candidate index");
                }
                Check(source.Count == 4 && source.Select(part => part.instanceId).SequenceEqual(sourceIds), "candidate structural changes do not mutate source list");
                for (int i = 0; i < sourceIds.Length; i++)
                    Check(sourceIndex.TryGetPosition(sourceIds[i], out int oldPosition) && oldPosition == i, "source index remains valid until its source changes " + i);

                sourceIndex.Dispose();
                source[2] = Part("source/same-count-new-identity");
                using (var replacementIndex = OwnedPartIdentityIndex.Build(source))
                {
                    Check(replacementIndex.TryGetPosition("source/same-count-new-identity", out int replacedAt) && replacedAt == 2, "new index sees same-count identity replacement");
                    Check(!replacementIndex.TryGetPosition(sourceIds[2], out _), "new index drops replaced identity");
                }
                var swap = source[0]; source[0] = source[3]; source[3] = swap;
                using (var reorderedIndex = OwnedPartIdentityIndex.Build(source))
                {
                    for (int i = 0; i < source.Count; i++)
                        Check(reorderedIndex.TryGetPosition(source[i].instanceId, out int position) && position == i, "fresh index follows same-count reorder after prior disposal " + i);
                }
            }
            finally { sourceIndex.Dispose(); }
        }

        static void CheckLifecycleIsolationAndConditionChanges()
        {
            var first = Fixture(3, "profile-a");
            var sameRevisionOtherProfile = Fixture(3, "profile-b");
            using (var firstIndex = OwnedPartIdentityIndex.Build(first))
            using (var secondIndex = OwnedPartIdentityIndex.Build(sameRevisionOtherProfile))
            {
                Check(firstIndex.Count == secondIndex.Count, "separate same-sized sources can have equal counts");
                Check(firstIndex.TryGetPosition("profile-a/part-00001", out int aPosition) && aPosition == 1, "first source resolves its ID");
                Check(!firstIndex.TryGetPosition("profile-b/part-00001", out _), "first source cannot resolve another source ID");
                Check(secondIndex.TryGetPosition("profile-b/part-00001", out int bPosition) && bPosition == 1, "second source resolves its own ID");
                Check(!secondIndex.TryGetPosition("profile-a/part-00001", out _), "second source cannot resolve another source ID");

                PartInstance reference = first[1];
                string id = reference.instanceId;
                float health = reference.condition.health;
                reference.condition.health = .42f;
                reference.condition.runningSeconds += 25;
                reference.condition.distanceM += 1000;
                reference.condition.stressExposure += .5f;
                Check(firstIndex.TryGetPosition(id, out int afterWearPosition) && afterWearPosition == 1, "condition/history edits preserve identity position");
                Check(ReferenceEquals(first[afterWearPosition], reference) && ReferenceEquals(Scan(first, id), reference), "condition/history edits preserve exact part reference");
                Check(reference.condition.health != health && reference.condition.runningSeconds == 35 && reference.condition.distanceM == 1250, "condition/history fields remain on the same live record");
                Check(!ReferenceEquals(firstIndex, secondIndex), "each operation scope owns a distinct index instance");
            }
        }

        static bool FieldwiseEqual(object left, object right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.GetType() != right.GetType()) return false;
            Type type = left.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)) return left.Equals(right);
            if (type.IsArray)
            {
                var a = (Array)left; var b = (Array)right;
                if (a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++) if (!FieldwiseEqual(a.GetValue(i), b.GetValue(i))) return false;
                return true;
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.IsStatic || field.IsNotSerialized) continue;
                if (!FieldwiseEqual(field.GetValue(left), field.GetValue(right))) return false;
            }
            return true;
        }

        sealed class PartSnapshot
        {
            public PartInstance Reference;
            public string InstanceId, DefinitionId, OwnerId, Slot;
            public PartLocation Location;
            public ComponentCondition ConditionReference, Condition;
        }

        sealed class ReceiptSnapshot
        {
            public RunCheckpoint Reference;
            public string RunId, OwnerId, ClosedReason;
            public long Revision, Tick, ThermalTicks;
            public bool Settled, HasFuel;
            public FuelState Fuel;
            public FluidState Fluids;
        }

        static PartSnapshot[] SnapshotParts(PlayerProfile profile)
        {
            return profile.parts.Select(part => new PartSnapshot
            {
                Reference = part, InstanceId = part.instanceId, DefinitionId = part.definitionId,
                OwnerId = part.ownerId, Slot = part.slot, Location = part.location,
                ConditionReference = part.condition, Condition = part.condition.Copy()
            }).ToArray();
        }

        static ReceiptSnapshot[] SnapshotReceipts(PlayerProfile profile)
        {
            return profile.runCheckpoints.Select(receipt => new ReceiptSnapshot
            {
                Reference = receipt, RunId = receipt.runId, OwnerId = receipt.ownedVehicleId,
                ClosedReason = receipt.closedReason, Revision = receipt.revision, Tick = receipt.tick,
                ThermalTicks = receipt.thermalUtcTicks, Settled = receipt.settled, HasFuel = receipt.hasFuel,
                Fuel = receipt.fuel == null ? null : receipt.fuel.Copy(),
                Fluids = receipt.fluids == null ? null : receipt.fluids.Copy()
            }).ToArray();
        }

        static void AssertCheckpointSourceUnchanged(PlayerProfile profile, List<PartInstance> partList,
            PartSnapshot[] parts, OwnedCar car, string carId, string vehicleId, long thermalTicks, FuelState fuelReference,
            FuelState fuel, FluidState fluidsReference, FluidState fluids, List<RunCheckpoint> receiptList,
            ReceiptSnapshot[] receipts, string context)
        {
            Check(ReferenceEquals(profile.parts, partList) && profile.parts.Count == parts.Length, context + ": owned part collection and count unchanged");
            for (int i = 0; i < parts.Length; i++)
            {
                var expected = parts[i]; var actual = profile.parts[i];
                Check(ReferenceEquals(actual, expected.Reference), context + ": part object reference unchanged " + i);
                Check(actual.instanceId == expected.InstanceId && actual.definitionId == expected.DefinitionId && actual.ownerId == expected.OwnerId && actual.slot == expected.Slot && actual.location == expected.Location, context + ": part identity/location unchanged " + i);
                Check(ReferenceEquals(actual.condition, expected.ConditionReference) && FieldwiseEqual(actual.condition, expected.Condition), context + ": part condition and member history unchanged " + i);
            }
            Check(ReferenceEquals(profile.SelectedCar, car) && car.instanceId == carId && car.vehicleId == vehicleId, context + ": selected car identity unchanged");
            Check(car.thermalUtcTicks == thermalTicks, context + ": car thermal clock unchanged");
            Check(ReferenceEquals(car.fuel, fuelReference) && FieldwiseEqual(car.fuel, fuel), context + ": fuel state and history unchanged");
            Check(ReferenceEquals(car.fluids, fluidsReference) && FieldwiseEqual(car.fluids, fluids), context + ": fluids and history unchanged");
            Check(ReferenceEquals(profile.runCheckpoints, receiptList) && profile.runCheckpoints.Count == receipts.Length, context + ": checkpoint receipt collection/count unchanged");
            for (int i = 0; i < receipts.Length; i++)
            {
                var expected = receipts[i]; var actual = profile.runCheckpoints[i];
                Check(ReferenceEquals(actual, expected.Reference) && actual.runId == expected.RunId && actual.ownedVehicleId == expected.OwnerId && actual.revision == expected.Revision && actual.tick == expected.Tick && actual.thermalUtcTicks == expected.ThermalTicks && actual.settled == expected.Settled && actual.closedReason == expected.ClosedReason && actual.hasFuel == expected.HasFuel, context + ": checkpoint receipt identity/version unchanged " + i);
                Check(FieldwiseEqual(actual.fuel, expected.Fuel) && FieldwiseEqual(actual.fluids, expected.Fluids), context + ": checkpoint receipt resources unchanged " + i);
            }
        }

        static void CheckRealCheckpointFlow()
        {
            var authoredVehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            if (authoredVehicles == null || authoredVehicles.Length == 0) throw new InvalidOperationException("S02a requires authored vehicle definitions for the isolated in-memory checkpoint fixture.");
            var catalog = new MechanicalCatalog(authoredVehicles.Select(vehicle => vehicle.spec));
            var profile = PlayerProfile.New(RuntimeEntropy.UtcNow.Ticks);
            var vehicle = authoredVehicles[0];
            profile.SelectedCar.vehicleId = vehicle.spec.id;
            profile.selectedVehicleId = vehicle.spec.id;
            MechanicsOwnership.Ensure(profile, catalog);
            var ownedCar = profile.SelectedCar;
            var resolved = MechanicsOwnership.Resolve(profile, ownedCar, catalog);
            var simulation = new CarSimulation(resolved.spec, resolved.tune, profile.driving, profile.parts);
            simulation.SetFluids(ownedCar.fluids);
            simulation.SetMassInputs(resolved.tune.driverMassKg, resolved.tune.payloadMassKg,
                resolved.tune.payloadPositionM, resolved.tune.payloadHeightM, ownedCar.fuel.litres);
            // Add one isolated unrelated inventory record after simulation capture so it is not
            // part of the installed run snapshot, while still exercising untouched source state.
            var spare = profile.parts[0].Copy();
            spare.instanceId = "s02a-unrelated-inventory-part";
            spare.location = PartLocation.Inventory; spare.ownerId = ""; spare.slot = "";
            spare.storageId = SharedInventory.Garage;
            if (spare.condition.elements != null)
                for (int i = 0; i < spare.condition.elements.Length; i++)
                    if (spare.condition.elements[i] != null) spare.condition.elements[i].id = "s02a-unrelated-member-" + i.ToString(CultureInfo.InvariantCulture);
            profile.parts.Add(spare);
            string fixtureError = MechanicsOwnership.Validate(profile, catalog);
            if (fixtureError != null) throw new InvalidOperationException("Authored in-memory checkpoint fixture is invalid: " + fixtureError);
            const string runId = "s02a-checkpoint-run";
            MechanicsTransactions.ApplyCheckpoint(profile, RunPersistence.Capture(runId, ownedCar.instanceId, 0, simulation));

            var checkpoint = RunPersistence.Capture(runId, ownedCar.instanceId, 120, simulation);
            var partList = profile.parts;
            var parts = SnapshotParts(profile);
            var fuelRef = ownedCar.fuel; var fuel = ownedCar.fuel.Copy();
            var fluidsRef = ownedCar.fluids; var fluids = ownedCar.fluids.Copy();
            var receiptList = profile.runCheckpoints;
            var receipts = SnapshotReceipts(profile);
            string carId = ownedCar.instanceId, vehicleId = ownedCar.vehicleId;
            long thermalTicks = ownedCar.thermalUtcTicks;

            MechanicsTransactions.ValidateCheckpoint(profile, checkpoint);
            AssertCheckpointSourceUnchanged(profile, partList, parts, ownedCar, carId, vehicleId, thermalTicks, fuelRef, fuel, fluidsRef, fluids, receiptList, receipts, "valid checkpoint validation");

            var lateThermalFailure = RunPersistence.Capture(runId, ownedCar.instanceId, 120, simulation);
            lateThermalFailure.thermalUtcTicks = DateTime.MaxValue.Ticks + 1;
            Throws<InvalidOperationException>(() => MechanicsTransactions.ApplyCheckpoint(profile, lateThermalFailure), "invalid late thermal field rejected before application");
            AssertCheckpointSourceUnchanged(profile, partList, parts, ownedCar, carId, vehicleId, thermalTicks, fuelRef, fuel, fluidsRef, fluids, receiptList, receipts, "late thermal rejection");

            var lateConditionFailure = RunPersistence.Capture(runId, ownedCar.instanceId, 120, simulation);
            lateConditionFailure.components[lateConditionFailure.components.Count - 1].condition.health = float.NaN;
            Throws<InvalidOperationException>(() => MechanicsTransactions.ApplyCheckpoint(profile, lateConditionFailure), "invalid late component condition rejected before application");
            AssertCheckpointSourceUnchanged(profile, partList, parts, ownedCar, carId, vehicleId, thermalTicks, fuelRef, fuel, fluidsRef, fluids, receiptList, receipts, "late component rejection");

            var duplicateFailure = RunPersistence.Capture(runId, ownedCar.instanceId, 120, simulation);
            duplicateFailure.components[duplicateFailure.components.Count - 1].instanceId = duplicateFailure.components[0].instanceId;
            Throws<InvalidOperationException>(() => MechanicsTransactions.ApplyCheckpoint(profile, duplicateFailure), "duplicate checkpoint identity rejected before application");
            AssertCheckpointSourceUnchanged(profile, partList, parts, ownedCar, carId, vehicleId, thermalTicks, fuelRef, fuel, fluidsRef, fluids, receiptList, receipts, "duplicate identity rejection");

            var noResources = RunPersistence.Capture(runId, ownedCar.instanceId, 120, simulation);
            noResources.hasFuel = false;
            noResources.fluids = null;
            FluidState validFluidReference = ownedCar.fluids;
            ownedCar.fluids = null;
            var nullFluidParts = SnapshotParts(profile);
            var nullFluidReceipts = SnapshotReceipts(profile);
            Throws<InvalidOperationException>(() => MechanicsTransactions.ApplyCheckpoint(profile, noResources), "missing prior fluids rejected before component application");
            AssertCheckpointSourceUnchanged(profile, partList, nullFluidParts, ownedCar, carId, vehicleId, thermalTicks, fuelRef, fuel, null, null, receiptList, nullFluidReceipts, "missing prior fluids rejection");
            ownedCar.fluids = validFluidReference;

            FuelState validFuelReference = ownedCar.fuel;
            ownedCar.fuel = null;
            var nullFuelParts = SnapshotParts(profile);
            var nullFuelReceipts = SnapshotReceipts(profile);
            Throws<InvalidOperationException>(() => MechanicsTransactions.ApplyCheckpoint(profile, noResources), "missing prior fuel rejected before component application");
            AssertCheckpointSourceUnchanged(profile, partList, nullFuelParts, ownedCar, carId, vehicleId, thermalTicks, null, null, fluidsRef, fluids, receiptList, nullFuelReceipts, "missing prior fuel rejection");
            ownedCar.fuel = validFuelReference;

            PartInstance wornSnapshot = checkpoint.components.First(part => part.condition.tires.Length != 4);
            wornSnapshot.condition.health = Math.Max(0, wornSnapshot.condition.health - .01f);
            wornSnapshot.condition.runningSeconds += 1;
            wornSnapshot.condition.distanceM += 5;
            wornSnapshot.condition.stressExposure += .05;
            if (wornSnapshot.condition.elements != null && wornSnapshot.condition.elements.Length > 0)
            {
                wornSnapshot.condition.elements[0].health = Math.Max(0, wornSnapshot.condition.elements[0].health - .01f);
                wornSnapshot.condition.elements[0].wearRemainder += .01;
            }
            Check(thermalTicks > 0, "thermal fixture supports a strictly earlier valid incoming clock");
            checkpoint.thermalUtcTicks = thermalTicks - 1;
            checkpoint.fuel.litres = Math.Max(0, checkpoint.fuel.litres - .01);
            checkpoint.fuel.cumulativeUsedLitres += .01;
            checkpoint.fluids.oilLitres = Math.Max(0, checkpoint.fluids.oilLitres - .01);
            checkpoint.fluids.oilConsumedLitres += .01;
            MechanicsTransactions.ValidateCheckpoint(profile, checkpoint);
            AssertCheckpointSourceUnchanged(profile, partList, parts, ownedCar, carId, vehicleId, thermalTicks, fuelRef, fuel, fluidsRef, fluids, receiptList, receipts, "valid worn checkpoint validation");

            var componentIds = new HashSet<string>(checkpoint.components.Select(part => part.instanceId), StringComparer.Ordinal);
            MechanicsTransactions.ApplyCheckpoint(profile, checkpoint);
            Check(profile.parts.Count == parts.Length, "valid checkpoint preserves owned part list length");
            for (int i = 0; i < parts.Length; i++)
            {
                var before = parts[i]; var actual = profile.parts[i];
                Check(ReferenceEquals(actual, before.Reference), "valid checkpoint preserves PartInstance object identity " + i);
                Check(actual.instanceId == before.InstanceId && actual.definitionId == before.DefinitionId && actual.ownerId == before.OwnerId && actual.slot == before.Slot && actual.location == before.Location, "valid checkpoint preserves ownership identity " + i);
                if (componentIds.Contains(before.InstanceId))
                {
                    var expected = checkpoint.components.First(snapshot => snapshot.instanceId == before.InstanceId);
                    Check(!ReferenceEquals(actual.condition, expected.condition), "checkpoint applies a detached typed condition copy " + i);
                    Check(FieldwiseEqual(actual.condition, expected.condition), "checkpoint preserves exact member wear/fault/history " + i);
                }
                else
                    Check(ReferenceEquals(actual.condition, before.ConditionReference) && FieldwiseEqual(actual.condition, before.Condition), "unrelated inventory condition remains untouched " + i);
            }
            Check(ownedCar.thermalUtcTicks == Math.Max(thermalTicks, checkpoint.thermalUtcTicks), "valid checkpoint preserves max prior/incoming car thermal clock");
            Check(FieldwiseEqual(ownedCar.fuel, checkpoint.fuel), "valid checkpoint applies exact fuel and cumulative usage");
            Check(FieldwiseEqual(ownedCar.fluids, checkpoint.fluids), "valid checkpoint applies exact fluid quantities and history");
            Check(profile.runCheckpoints.Count == 1 && profile.runCheckpoints[0].runId == runId && profile.runCheckpoints[0].revision == profile.revision + 1 && profile.runCheckpoints[0].tick == checkpoint.tick && profile.runCheckpoints[0].thermalUtcTicks == checkpoint.thermalUtcTicks && !profile.runCheckpoints[0].settled && FieldwiseEqual(profile.runCheckpoints[0].fuel, ownedCar.fuel) && FieldwiseEqual(profile.runCheckpoints[0].fluids, ownedCar.fluids), "valid checkpoint updates the existing run receipt exactly once with current resource snapshot");
            Check(!ReferenceEquals(ownedCar.fuel, checkpoint.fuel) && !ReferenceEquals(ownedCar.fluids, checkpoint.fluids), "checkpoint resource payloads are copied, not aliased");
        }

        static int QueryPosition(int count, int queryNumber, string group)
        {
            switch (group)
            {
                case "first64": return queryNumber;
                case "last64": return count - 64 + queryNumber;
                case "shuffled64": return (queryNumber * 7919 + 37) % count;
                default: return -1;
            }
        }

        static string[] QueryIds(List<PartInstance> parts, string group)
        {
            int count = parts.Count;
            var ids = new string[64];
            for (int q = 0; q < ids.Length; q++)
            {
                int position = QueryPosition(count, q, group);
                ids[q] = group == "missing64"
                    ? "bench-missing-" + count.ToString(CultureInfo.InvariantCulture) + "-" + q.ToString(CultureInfo.InvariantCulture)
                    : parts[position].instanceId;
            }
            return ids;
        }

        static int[] QueryPositions(int count, string group)
        {
            var positions = new int[64];
            for (int q = 0; q < positions.Length; q++) positions[q] = QueryPosition(count, q, group);
            return positions;
        }

        static void BenchmarkIndexQueries(OwnedPartIdentityIndex index,
            string[] ids, int[] expectedPositions, string label)
        {
            for (int q = 0; q < ids.Length; q++)
            {
                bool found = index.TryGetPosition(ids[q], out int position);
                if (expectedPositions[q] < 0 ? found || position != -1 : !found || position != expectedPositions[q])
                    throw new InvalidOperationException("Index query diverged from fixed position in " + label + ".");
            }
        }

        static void BenchmarkListQueries(List<PartInstance> parts, string[] ids, int[] expectedPositions, string label)
        {
            for (int q = 0; q < ids.Length; q++)
            {
                PartInstance found = Scan(parts, ids[q]);
                if (expectedPositions[q] < 0 ? found != null : !ReferenceEquals(found, parts[expectedPositions[q]]))
                    throw new InvalidOperationException("List.Find query diverged from fixed position in " + label + ".");
            }
        }

        static int FoundQueryCount(int[] expectedPositions) => expectedPositions.Count(position => position >= 0);

        static double Measure(Action action)
        {
            var watch = Stopwatch.StartNew(); action(); watch.Stop(); return watch.Elapsed.TotalMilliseconds;
        }

        static double Median(double[] samples)
        {
            var ordered = samples.OrderBy(value => value).ToArray();
            return ordered[ordered.Length / 2];
        }

        static void BenchmarkScale(int count)
        {
            var parts = Fixture(count, "bench" + count.ToString(CultureInfo.InvariantCulture));
            var fullBuildSamples = new double[Repeats];
            var groups = new[] { "first64", "last64", "shuffled64", "missing64" };
            var queries = groups.ToDictionary(group => group, group => QueryIds(parts, group));
            var positions = groups.ToDictionary(group => group, group => QueryPositions(count, group));
            var fullLookup = groups.ToDictionary(group => group, _ => new double[Repeats]);
            var fullTotal = groups.ToDictionary(group => group, _ => new double[Repeats]);
            var sparseBuild = groups.ToDictionary(group => group, _ => new double[Repeats]);
            var sparseLookup = groups.ToDictionary(group => group, _ => new double[Repeats]);
            var sparseTotal = groups.ToDictionary(group => group, _ => new double[Repeats]);
            var listFind = groups.ToDictionary(group => group, _ => new double[Repeats]);

            // Warm both builders and query paths once; these samples are not isolated-process/JIT cold.
            using (var fullWarm = OwnedPartIdentityIndex.Build(parts))
            {
                foreach (string group in groups)
                    BenchmarkIndexQueries(fullWarm, queries[group], positions[group], "full warm " + group);
            }
            foreach (string group in groups)
            {
                using (var sparseWarm = OwnedPartIdentityIndex.BuildRequested(parts, queries[group]))
                {
                    if (sparseWarm.Count != FoundQueryCount(positions[group])) throw new InvalidOperationException("Sparse warm index count differs from found request count.");
                    BenchmarkIndexQueries(sparseWarm, queries[group], positions[group], "sparse warm " + group);
                }
                BenchmarkListQueries(parts, queries[group], positions[group], "List.Find warm " + group);
            }

            for (int repeat = 0; repeat < Repeats; repeat++)
            {
                fullBuildSamples[repeat] = Measure(() =>
                {
                    using (var full = OwnedPartIdentityIndex.Build(parts)) { }
                });
                foreach (string group in groups)
                {
                    using (var full = OwnedPartIdentityIndex.Build(parts))
                    {
                        fullLookup[group][repeat] = Measure(() => BenchmarkIndexQueries(full, queries[group], positions[group], "full " + group));
                    }
                    fullTotal[group][repeat] = Measure(() =>
                    {
                        using (var full = OwnedPartIdentityIndex.Build(parts))
                            BenchmarkIndexQueries(full, queries[group], positions[group], "full total " + group);
                    });
                    sparseBuild[group][repeat] = Measure(() =>
                    {
                        using (var sparse = OwnedPartIdentityIndex.BuildRequested(parts, queries[group])) { }
                    });
                    using (var sparse = OwnedPartIdentityIndex.BuildRequested(parts, queries[group]))
                    {
                        sparseLookup[group][repeat] = Measure(() => BenchmarkIndexQueries(sparse, queries[group], positions[group], "sparse " + group));
                    }
                    sparseTotal[group][repeat] = Measure(() =>
                    {
                        using (var sparse = OwnedPartIdentityIndex.BuildRequested(parts, queries[group]))
                        {
                            BenchmarkIndexQueries(sparse, queries[group], positions[group], "sparse total " + group);
                        }
                    });
                    listFind[group][repeat] = Measure(() => BenchmarkListQueries(parts, queries[group], positions[group], "List.Find " + group));
                }
            }

            long keyChars = parts.Sum(part => (long)part.instanceId.Length);
            long keyPayloadBytes = keyChars * sizeof(char);
            // Conservative payload lower bound: at least 20 bytes per dictionary entry plus a
            // 4-byte bucket slot. Excludes alignment, prime-capacity slack, headers and key strings.
            long structureEstimateBytes = (long)count * 24;
            foreach (string group in groups)
            {
                int sparseEntries = FoundQueryCount(positions[group]);
                long sparseKeyPayloadBytes = queries[group].Where((id, i) => positions[group][i] >= 0).Sum(id => (long)id.Length * sizeof(char));
                Timings.AppendLine(string.Join(",", new[]
                {
                    count.ToString(CultureInfo.InvariantCulture), group, "64", Repeats.ToString(CultureInfo.InvariantCulture),
                    Median(fullBuildSamples).ToString("F4", CultureInfo.InvariantCulture), fullBuildSamples.Max().ToString("F4", CultureInfo.InvariantCulture),
                    Median(fullLookup[group]).ToString("F4", CultureInfo.InvariantCulture), fullLookup[group].Max().ToString("F4", CultureInfo.InvariantCulture),
                    Median(fullTotal[group]).ToString("F4", CultureInfo.InvariantCulture), fullTotal[group].Max().ToString("F4", CultureInfo.InvariantCulture),
                    Median(sparseBuild[group]).ToString("F4", CultureInfo.InvariantCulture), sparseBuild[group].Max().ToString("F4", CultureInfo.InvariantCulture),
                    Median(sparseLookup[group]).ToString("F4", CultureInfo.InvariantCulture), sparseLookup[group].Max().ToString("F4", CultureInfo.InvariantCulture),
                    Median(sparseTotal[group]).ToString("F4", CultureInfo.InvariantCulture), sparseTotal[group].Max().ToString("F4", CultureInfo.InvariantCulture),
                    Median(listFind[group]).ToString("F4", CultureInfo.InvariantCulture), listFind[group].Max().ToString("F4", CultureInfo.InvariantCulture),
                    count.ToString(CultureInfo.InvariantCulture), sparseEntries.ToString(CultureInfo.InvariantCulture), keyPayloadBytes.ToString(CultureInfo.InvariantCulture), sparseKeyPayloadBytes.ToString(CultureInfo.InvariantCulture),
                    structureEstimateBytes.ToString(CultureInfo.InvariantCulture), ((long)sparseEntries * 24).ToString(CultureInfo.InvariantCulture),
                    "entry-plus-bucket-lower-bound-excludes-alignment-capacity-and-object-headers"
                }));
            }
        }

        // Focused reviewer-requested preservation assertions; preserve the full-run timings.
        public static void RunPreservationStandalone()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            Checks.Length = 0; passed = 0;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S02"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "checkpoint-preservation-checks.txt");
            try
            {
                CheckRealCheckpointFlow();
                Checks.AppendLine("RESULT PASS count=" + passed);
                UnityEngine.Debug.Log("S02a checkpoint preservation checks passed: " + passed);
            }
            catch (Exception error)
            {
                Checks.AppendLine("RESULT FAIL count=" + passed);
                Checks.AppendLine(error.ToString());
                throw;
            }
            finally { File.WriteAllText(path, Checks.ToString(), new UTF8Encoding(false)); }
        }

        public static void RunStandalone()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("S02a checks require a standalone Editor batch invocation with no Play Mode session.");
            Checks.Length = 0; Timings.Length = 0; passed = 0;
            Checks.AppendLine("S02a owned-part identity index; synthetic managed IDs; no profile save, full-profile clone, or JSON manipulation.");
            Timings.AppendLine("count,query_group,requested_ids,repeats,strict_build_median_ms,strict_build_sample_max_ms,strict_lookup_median_ms,strict_lookup_sample_max_ms,strict_build_plus_lookup_median_ms,strict_build_plus_lookup_sample_max_ms,sparse_build_median_ms,sparse_build_sample_max_ms,sparse_lookup_median_ms,sparse_lookup_sample_max_ms,sparse_build_plus_lookup_median_ms,sparse_build_plus_lookup_sample_max_ms,list_find_median_ms,list_find_sample_max_ms,strict_index_entries,sparse_index_entries,all_source_id_utf16_payload_bytes,sparse_retained_id_utf16_payload_bytes,strict_structural_lower_bound_bytes,sparse_structural_lower_bound_bytes,estimate_limit");
            try
            {
                CheckDifferentialAndInvalidInputs();
                CheckCandidateLocalStructuralChanges();
                CheckLifecycleIsolationAndConditionChanges();
                CheckRealCheckpointFlow();
                foreach (int count in new[] { 100, 1000, 10000, 30000 }) BenchmarkScale(count);
                Checks.AppendLine("BENCH rows=16 repeats=" + Repeats + " requested_ids=64 groups=first64,last64,shuffled64,missing64 full_vs_sparse_vs_listfind=true timing_warmup=one-pass sample_max_is_not_percentile=true sparse_validates_only_scanned_prefix=true");
                Checks.AppendLine("RESULT PASS count=" + passed);
                WriteReports();
                UnityEngine.Debug.Log("S02a owned-part index checks passed: " + passed);
            }
            catch (Exception ex)
            {
                Checks.AppendLine("RESULT FAIL count=" + passed);
                Checks.AppendLine(ex.ToString());
                WriteReports();
                UnityEngine.Debug.LogException(ex);
                throw;
            }
        }

        static void WriteReports()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S02"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "owned-part-index-checks.txt"), Checks.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "owned-part-index-timings.csv"), Timings.ToString(), new UTF8Encoding(false));
        }
    }
#endif
}
