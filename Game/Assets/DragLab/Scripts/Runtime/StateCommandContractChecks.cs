using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DragLab.Domain.Mechanics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DragLab.Runtime
{
#if UNITY_EDITOR
    /// <summary>Managed-only adversarial checks for the dormant S01 command metadata contract.</summary>
    public static class StateCommandContractChecks
    {
        static readonly StringBuilder Report = new StringBuilder();
        static int passed;

        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("S01 contract check failed: " + name);
            passed++;
            Report.AppendLine("PASS " + name);
        }

        static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T) { passed++; Report.AppendLine("PASS " + name); return; }
            throw new InvalidOperationException("S01 contract check failed (expected " + typeof(T).Name + "): " + name);
        }

        static StateCommandIdentity Identity(string slot = "verification", long generation = 7, string operation = "s01-op-001")
            => new StateCommandIdentity(slot, generation, operation);
        static StateCommandVersionFence Fence(long ledger = 12, long storage = 41, long content = 3)
            => new StateCommandVersionFence(ledger, storage, content);
        static StateRecordKey Key(string family, string key) => new StateRecordKey(family, key);
        static StateReadDependency Existing(StateRecordKey key, long version) => new StateReadDependency(key, true, version);
        static StateReadDependency Absent(StateRecordKey key) => new StateReadDependency(key, false, 0);
        static StateWriteDeclaration Update(StateRecordKey key, long version) => new StateWriteDeclaration(key, StateWriteKind.Update, version);
        static StateWriteDeclaration Create(StateRecordKey key) => new StateWriteDeclaration(key, StateWriteKind.Create, 0);

        static IEnumerable<StateReadDependency> ExcessReads(StateRecordKey key, Action onYield)
        {
            for (int i = 0; ; i++)
            {
                onYield();
                yield return Absent(new StateRecordKey("bounded", key.Key + "-" + i));
            }
        }

        static StateCommandManifest BasicManifest()
        {
            var car = Key("car", "car-1");
            var cash = Key("ledger", "cash");
            return new StateCommandManifest(Identity(), Fence(),
                new[] { Existing(car, 8), Existing(cash, 4) },
                new[] { Update(car, 8), Update(cash, 4) });
        }

        static void CheckIdentityAndVersionFences()
        {
            var identity = Identity();
            Check(identity.Equals(new StateCommandIdentity("verification", 7, "s01-op-001")), "identity equality binds slot, generation and operation");
            Check(!identity.Equals(Identity("other-slot")), "identity rejects other slot");
            Check(!identity.Equals(Identity(generation: 8)), "identity rejects reset generation reuse");
            Check(!identity.Equals(Identity(operation: "s01-op-002")), "identity rejects operation reuse mismatch");
            Check(!identity.Equals(Identity(operation: "S01-op-001")), "identity comparison is ordinal and case-sensitive");
            Throws<ArgumentException>(() => new StateCommandIdentity("../player", 1, "op"), "invalid slot path rejected");
            Throws<ArgumentException>(() => new StateCommandIdentity("verification", -1, "op"), "negative reset generation rejected");
            Throws<ArgumentException>(() => new StateCommandIdentity("verification", 0, "bad\noperation"), "control character in operation rejected");

            Check(Identity(generation: long.MaxValue - 1).ResetGeneration == long.MaxValue - 1, "largest current reset generation accepted; reset exhaustion remains owner responsibility");
            Throws<ArgumentException>(() => Identity(generation: long.MaxValue), "reserved exhausted generation rejected");
            Check(Fence(content: long.MaxValue - 1).ExpectedContentVersion == long.MaxValue - 1, "nonincrementing content boundary accepted");

            var manifest = BasicManifest();
            Check(manifest.ValidateCurrentFence("verification", 7, Fence(), out var error) && error == StateCommandContractError.None, "current slot, generation and all versions accepted");
            Check(!manifest.ValidateCurrentFence("slot-b", 7, Fence(), out error) && error == StateCommandContractError.StaleSaveSlot, "stale save slot rejected");
            Check(!manifest.ValidateCurrentFence("verification", 8, Fence(), out error) && error == StateCommandContractError.StaleResetGeneration, "stale reset generation rejected");
            Check(!manifest.ValidateCurrentFence("verification", 7, Fence(13, 41, 3), out error) && error == StateCommandContractError.StaleLedgerRevision, "stale ledger revision rejected");
            Check(!manifest.ValidateCurrentFence("verification", 7, Fence(12, 42, 3), out error) && error == StateCommandContractError.StaleStorageCommit, "stale storage commit rejected");
            Check(!manifest.ValidateCurrentFence("verification", 7, Fence(12, 41, 4), out error) && error == StateCommandContractError.StaleContentVersion, "stale content version rejected");
            Check(!manifest.ValidateCurrentFence("verification", 7, null, out error) && error == StateCommandContractError.InvalidVersionFence, "missing current version fence rejected");
            Throws<ArgumentOutOfRangeException>(() => Fence(long.MaxValue - 1, 1, 1), "ledger fence reserves next version");
            Throws<ArgumentOutOfRangeException>(() => Fence(1, long.MaxValue - 1, 1), "storage fence reserves next version");
            Throws<ArgumentOutOfRangeException>(() => Fence(1, 1, long.MaxValue), "content fence rejects unusable version");
        }

        static void CheckManifestConflictMatrix()
        {
            var car = Key("car", "car-1"); var cash = Key("ledger", "cash"); var absent = Key("receipt", "r-1");
            Throws<ArgumentException>(() => new StateCommandManifest(Identity(), Fence(), new[] { Existing(car, 1), Existing(car, 1) }, Array.Empty<StateWriteDeclaration>()), "duplicate read dependency rejected");
            Throws<ArgumentException>(() => new StateCommandManifest(Identity(), Fence(), new[] { Existing(car, 1) }, new[] { Update(car, 1), Update(car, 1) }), "duplicate write declaration rejected");
            Throws<ArgumentException>(() => new StateCommandManifest(Identity(), Fence(), Array.Empty<StateReadDependency>(), new[] { Update(car, 1) }), "write without read fence rejected");
            Throws<ArgumentException>(() => new StateCommandManifest(Identity(), Fence(), new[] { Existing(car, 1) }, new[] { Update(car, 2) }), "write/read version mismatch rejected");
            Throws<ArgumentException>(() => new StateCommandManifest(Identity(), Fence(), new[] { Absent(car) }, new[] { Update(car, 0) }), "update of absent record rejected");
            Throws<ArgumentException>(() => new StateCommandManifest(Identity(), Fence(), new[] { Existing(car, 1) }, new[] { Create(car) }), "create over existing record rejected");

            int enumerated = 0;
            Throws<ArgumentOutOfRangeException>(() => new StateCommandManifest(Identity(), Fence(), ExcessReads(car, () => enumerated++), Array.Empty<StateWriteDeclaration>()), "unbounded lazy read source stops at manifest ceiling");
            Check(enumerated == StateCommandManifest.MaxDeclaredRecords + 1, "manifest bound consumes at most ceiling plus one sentinel");
            var boundedReads = new List<StateReadDependency>(); var boundedWrites = new List<StateWriteDeclaration>();
            for (int i = 0; i < StateCommandManifest.MaxDeclaredRecords / 2 + 1; i++)
            {
                var boundedKey = Key("bounded-create", i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                boundedReads.Add(Absent(boundedKey)); boundedWrites.Add(Create(boundedKey));
            }
            Throws<ArgumentOutOfRangeException>(() => new StateCommandManifest(Identity(), Fence(), boundedReads, boundedWrites), "combined read/write declarations obey total record ceiling");

            var creates = new StateCommandManifest(Identity(), Fence(), new[] { Absent(absent) }, new[] { Create(absent) });
            Check(creates.ValidateReadSet(new[] { Absent(absent) }, out var error) && error == StateCommandContractError.None, "explicit absent-record create dependency accepted");
            Check(creates.ValidateObservedWrites(new[] { Create(absent) }, out error) && error == StateCommandContractError.None, "declared absent-record create observation accepted");
            Check(creates.ValidateObservedWrites(Array.Empty<StateWriteDeclaration>(), out error), "declared no-op write is permitted");
            Check(!creates.ValidateReadSet(Array.Empty<StateReadDependency>(), out error) && error == StateCommandContractError.StaleRecordDependency, "missing observed dependency rejected");
            Check(!creates.ValidateReadSet(new[] { Existing(absent, 1) }, out error) && error == StateCommandContractError.StaleRecordDependency, "absent-to-existing dependency change rejected");

            var manifest = BasicManifest();
            var validReads = new[] { Existing(car, 8), Existing(cash, 4) };
            Check(manifest.ValidateReadSet(validReads, out error), "exact observed read-set accepted");
            Check(!manifest.ValidateReadSet(new[] { Existing(car, 9), Existing(cash, 4) }, out error) && error == StateCommandContractError.StaleRecordDependency, "stale record version rejected");
            Check(!manifest.ValidateReadSet(new[] { Existing(car, 8) }, out error) && error == StateCommandContractError.StaleRecordDependency, "incomplete observed read-set rejected");
            Check(!manifest.ValidateReadSet(new[] { Existing(car, 8), Existing(cash, 4), Absent(Key("extra", "x")) }, out error) && error == StateCommandContractError.StaleRecordDependency, "extra observed dependency rejected");
            Check(!manifest.ValidateReadSet(new[] { Existing(car, 8), Existing(car, 8) }, out error) && error == StateCommandContractError.DuplicateReadDependency, "duplicate observed dependency rejected");
            Check(!manifest.ValidateReadSet(null, out error) && error == StateCommandContractError.StaleRecordDependency, "null observed read-set rejected");

            Check(manifest.ValidateObservedWrites(new[] { Update(car, 8) }, out error), "declared subset of writes accepted");
            Check(!manifest.ValidateObservedWrites(new[] { Update(car, 8), Update(car, 8) }, out error) && error == StateCommandContractError.DuplicateObservedWrite, "duplicate observed write rejected");
            Check(!manifest.ValidateObservedWrites(new[] { Update(car, 9) }, out error) && error == StateCommandContractError.UndeclaredWrite, "observed stale write intent rejected");
            Check(!manifest.ValidateObservedWrites(new[] { Update(Key("car", "car-2"), 1) }, out error) && error == StateCommandContractError.UndeclaredWrite, "undeclared record change rejected");
            Check(!manifest.ValidateObservedWrites(new[] { new StateWriteDeclaration(car, StateWriteKind.Delete, 8) }, out error) && error == StateCommandContractError.UndeclaredWrite, "conflicting write kind rejected");
            Check(!manifest.ValidateObservedWrites(new[] { Update(car, 8), Update(cash, 4), Create(Key("extra", "x")) }, out error) && error == StateCommandContractError.UndeclaredWrite, "extra write beyond declared set rejected");
            Check(!manifest.ValidateObservedWrites(null, out error) && error == StateCommandContractError.UndeclaredWrite, "null observed write-set rejected");
        }

        static void CheckDefensiveMetadata()
        {
            var car = Key("car", "car-1"); var reads = new List<StateReadDependency> { Existing(car, 8) };
            var writes = new List<StateWriteDeclaration> { Update(car, 8) };
            var manifest = new StateCommandManifest(Identity(), Fence(), reads, writes);
            reads.Clear(); writes.Clear();
            Check(manifest.Reads.Count == 1 && manifest.Writes.Count == 1, "manifest copies caller-owned input collections");
            Check(manifest.Reads[0].Record.Equals(car) && manifest.Reads[0].Version == 8, "manifest metadata remains stable after caller mutation");
            Throws<NotSupportedException>(() => ((IList<StateReadDependency>)manifest.Reads).Clear(), "read collection is externally read-only");
            Throws<NotSupportedException>(() => ((IList<StateWriteDeclaration>)manifest.Writes).Clear(), "write collection is externally read-only");
            var original = Existing(car, 8);
            Check(original.Exists && original.Version == 8 && original.Record.Equals(car), "source dependency objects remain immutable");
            Check(car.Equals(new StateRecordKey("car", "car-1")) && car.GetHashCode() == new StateRecordKey("car", "car-1").GetHashCode(), "record keys preserve ordinal value identity");
            Check(!car.Equals(Key("Car", "car-1")) && !car.Equals(Key("car", "CAR-1")), "record keys distinguish ordinal case changes");
        }

        static void CheckDurableLifecycle()
        {
            var id = Identity(); var versions = Fence();
            var receipt = new StateCommandReceipt(id, 13, 42);
            Check(StateCommandLifecycleRules.CanTransition(StateCommandLifecycle.Preparing, StateCommandLifecycle.ReadyToCommit), "preparing can become ready");
            Check(StateCommandLifecycleRules.CanTransition(StateCommandLifecycle.ReadyToCommit, StateCommandLifecycle.CanceledBeforeCommit), "ready command can cancel before commit");
            Check(StateCommandLifecycleRules.ValidateTransition(StateCommandLifecycle.ReadyToCommit, StateCommandLifecycle.CanceledBeforeCommit, id, versions, null, out var error), "precommit cancel accepted without receipt");
            Check(!StateCommandLifecycleRules.CanTransition(StateCommandLifecycle.CommitInProgress, StateCommandLifecycle.CanceledBeforeCommit), "cancellation forbidden after commit begins");
            Check(StateCommandLifecycleRules.CanTransition(StateCommandLifecycle.CommitInProgress, StateCommandLifecycle.OutcomeUnknown), "ambiguous commit outcome can be represented");
            Check(StateCommandLifecycleRules.CanTransition(StateCommandLifecycle.OutcomeUnknown, StateCommandLifecycle.Committed) && StateCommandLifecycleRules.CanTransition(StateCommandLifecycle.OutcomeUnknown, StateCommandLifecycle.RolledBack), "unknown outcome requires reconciliation to a terminal durable result");
            Check(StateCommandLifecycleRules.ValidateTransition(StateCommandLifecycle.OutcomeUnknown, StateCommandLifecycle.Committed, id, versions, receipt, out error), "unknown outcome reconciles with matching durable receipt");
            Check(StateCommandLifecycleRules.ValidateTransition(StateCommandLifecycle.CommitInProgress, StateCommandLifecycle.Committed, id, versions, receipt, out error), "commit completes with matching durable receipt");
            Check(StateCommandLifecycleRules.ValidateTransition(StateCommandLifecycle.CommitInProgress, StateCommandLifecycle.RolledBack, id, versions, null, out error), "precommit/storage rollback terminal path carries no receipt");
            Check(!StateCommandLifecycleRules.ValidateTransition(StateCommandLifecycle.OutcomeUnknown, StateCommandLifecycle.Committed, id, versions, null, out error) && error == StateCommandContractError.ReceiptIdentityMismatch, "unknown outcome cannot be declared committed without receipt");
            Check(!StateCommandLifecycleRules.ValidateTransition(StateCommandLifecycle.OutcomeUnknown, StateCommandLifecycle.Committed, Identity(operation: "other-op"), versions, receipt, out error) && error == StateCommandContractError.ReceiptIdentityMismatch, "receipt for another operation rejected");
            Check(!StateCommandLifecycleRules.ValidateTransition(StateCommandLifecycle.OutcomeUnknown, StateCommandLifecycle.Committed, id, Fence(12, 40, 3), receipt, out error) && error == StateCommandContractError.ReceiptVersionMismatch, "receipt with mismatched committed storage version rejected");
            Check(!StateCommandLifecycleRules.ValidateTransition(StateCommandLifecycle.ReadyToCommit, StateCommandLifecycle.CanceledBeforeCommit, id, versions, receipt, out error) && error == StateCommandContractError.ReceiptIdentityMismatch, "precommit cancellation cannot carry a commit receipt");
            Check(!StateCommandLifecycleRules.ValidateTransition(StateCommandLifecycle.Committed, StateCommandLifecycle.RolledBack, id, versions, null, out error) && error == StateCommandContractError.InvalidLifecycleTransition, "committed state cannot transition back to rollback");
            Check(!receipt.Matches(Identity(operation: "different"), versions), "receipt match binds operation identity");
            Check(receipt.Matches(id, versions), "receipt versions advance ledger and storage exactly once");
            var nearLimit = Fence(long.MaxValue - 2, long.MaxValue - 2, long.MaxValue - 1);
            var lastUsableReceipt = new StateCommandReceipt(id, long.MaxValue - 1, long.MaxValue - 1);
            Check(lastUsableReceipt.Matches(id, nearLimit), "last usable next ledger and storage receipt versions accepted");
            Check(!new StateCommandReceipt(id, 14, 42).Matches(id, versions), "receipt advancing ledger by more than one rejected");
            Check(!new StateCommandReceipt(id, 13, 43).Matches(id, versions), "receipt advancing storage by more than one rejected");
            Throws<ArgumentOutOfRangeException>(() => new StateCommandReceipt(id, 0, 42), "receipt rejects noncommitted ledger version");
        }

        public static void RunStandalone()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("S01 contract checks require a standalone Editor batch invocation with no Play Mode session.");
            Report.Length = 0; passed = 0;
            Report.AppendLine("S01 dormant metadata contract checks; pure managed fixtures; no profile store, JSON, or player slot access.");
            try
            {
                CheckIdentityAndVersionFences();
                CheckManifestConflictMatrix();
                CheckDefensiveMetadata();
                CheckDurableLifecycle();
                Report.AppendLine("RESULT PASS count=" + passed);
                WriteReport();
                UnityEngine.Debug.Log("S01 contract checks passed: " + passed);
            }
            catch (Exception ex)
            {
                Report.AppendLine("RESULT FAIL count=" + passed);
                Report.AppendLine(ex.ToString());
                WriteReport();
                throw;
            }
        }

        static void WriteReport()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S01/state-command-contract-checks.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Report.ToString(), new UTF8Encoding(false));
        }
    }
#endif
}
