using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    // Dormant command metadata. This file deliberately contains no profile references,
    // record payloads, persistence code, Unity APIs, or worker/coordinator implementation.
    public enum StateCommandContractError
    {
        None,
        InvalidIdentity,
        InvalidVersionFence,
        InvalidRecordKey,
        InvalidDependencyVersion,
        DuplicateReadDependency,
        DuplicateWriteDeclaration,
        MissingWriteReadDependency,
        WriteVersionMismatch,
        StaleSaveSlot,
        StaleResetGeneration,
        StaleLedgerRevision,
        StaleStorageCommit,
        StaleContentVersion,
        StaleRecordDependency,
        UndeclaredWrite,
        DuplicateObservedWrite,
        InvalidLifecycleTransition,
        ReceiptIdentityMismatch,
        ReceiptVersionMismatch
    }

    public sealed class StateCommandIdentity : IEquatable<StateCommandIdentity>
    {
        public readonly string SaveSlot;
        public readonly long ResetGeneration;
        public readonly string OperationId;

        public StateCommandIdentity(string saveSlot, long resetGeneration, string operationId)
        {
            if (!ValidSlot(saveSlot) || resetGeneration < 0 || resetGeneration == long.MaxValue ||
                !ValidToken(operationId, 128))
                throw new ArgumentException("Invalid save slot, reset generation, or operation ID.");
            SaveSlot = saveSlot;
            ResetGeneration = resetGeneration;
            OperationId = operationId;
        }

        static bool ValidSlot(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 48) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                    !(c >= '0' && c <= '9') && c != '_' && c != '-') return false;
            return true;
        }

        internal static bool ValidToken(string value, int maxLength)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength &&
                !value.Any(char.IsControl);
        }

        public bool Equals(StateCommandIdentity other)
        {
            return !ReferenceEquals(other, null) && ResetGeneration == other.ResetGeneration &&
                string.Equals(SaveSlot, other.SaveSlot, StringComparison.Ordinal) &&
                string.Equals(OperationId, other.OperationId, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StateCommandIdentity); }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(SaveSlot);
                hash = hash * 397 ^ ResetGeneration.GetHashCode();
                return hash * 397 ^ StringComparer.Ordinal.GetHashCode(OperationId);
            }
        }
    }

    public sealed class StateCommandVersionFence : IEquatable<StateCommandVersionFence>
    {
        public readonly long ExpectedLedgerRevision;
        public readonly long ExpectedStorageCommit;
        public readonly long ExpectedContentVersion;

        public StateCommandVersionFence(long expectedLedgerRevision, long expectedStorageCommit,
            long expectedContentVersion)
        {
            if (expectedLedgerRevision < 0 || expectedLedgerRevision >= long.MaxValue - 1 ||
                expectedStorageCommit < 0 || expectedStorageCommit >= long.MaxValue - 1 ||
                expectedContentVersion < 0 || expectedContentVersion == long.MaxValue)
                throw new ArgumentOutOfRangeException("Command versions must be non-negative and leave room for a next version.");
            ExpectedLedgerRevision = expectedLedgerRevision;
            ExpectedStorageCommit = expectedStorageCommit;
            ExpectedContentVersion = expectedContentVersion;
        }

        public bool Equals(StateCommandVersionFence other)
        {
            return !ReferenceEquals(other, null) && ExpectedLedgerRevision == other.ExpectedLedgerRevision &&
                ExpectedStorageCommit == other.ExpectedStorageCommit &&
                ExpectedContentVersion == other.ExpectedContentVersion;
        }
        public override bool Equals(object obj) { return Equals(obj as StateCommandVersionFence); }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ExpectedLedgerRevision.GetHashCode();
                hash = hash * 397 ^ ExpectedStorageCommit.GetHashCode();
                return hash * 397 ^ ExpectedContentVersion.GetHashCode();
            }
        }
    }

    public sealed class StateRecordKey : IEquatable<StateRecordKey>
    {
        public readonly string Family;
        public readonly string Key;

        public StateRecordKey(string family, string key)
        {
            if (!StateCommandIdentity.ValidToken(family, 64) ||
                !StateCommandIdentity.ValidToken(key, 192))
                throw new ArgumentException("Record family/key is empty, too long, or contains control characters.");
            Family = family;
            Key = key;
        }

        public bool Equals(StateRecordKey other)
        {
            return !ReferenceEquals(other, null) &&
                string.Equals(Family, other.Family, StringComparison.Ordinal) &&
                string.Equals(Key, other.Key, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StateRecordKey); }
        public override int GetHashCode()
        {
            unchecked { return StringComparer.Ordinal.GetHashCode(Family) * 397 ^ StringComparer.Ordinal.GetHashCode(Key); }
        }
    }

    public sealed class StateReadDependency
    {
        public readonly StateRecordKey Record;
        public readonly bool Exists;
        // Version zero means absent only. Existing-record versions start at one.
        public readonly long Version;

        public StateReadDependency(StateRecordKey record, bool exists, long version)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (exists ? version <= 0 || version == long.MaxValue : version != 0)
                throw new ArgumentOutOfRangeException(nameof(version), "Existing dependencies need a usable positive version; absent dependencies use zero.");
            Record = record;
            Exists = exists;
            Version = version;
        }
    }

    public enum StateWriteKind { Create, Update, Delete }

    public sealed class StateWriteDeclaration
    {
        public readonly StateRecordKey Record;
        public readonly StateWriteKind Kind;
        // Create expects absence/version zero; update and delete expect an existing version.
        public readonly long ExpectedVersion;

        public StateWriteDeclaration(StateRecordKey record, StateWriteKind kind, long expectedVersion)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!Enum.IsDefined(typeof(StateWriteKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == StateWriteKind.Create ? expectedVersion != 0 : expectedVersion <= 0 || expectedVersion == long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(expectedVersion));
            Record = record;
            Kind = kind;
            ExpectedVersion = expectedVersion;
        }
    }

    public sealed class StateCommandManifest
    {
        // Hard memory ceiling; manifests enumerate only the affected key set, never a profile snapshot.
        public const int MaxDeclaredRecords = 16384;
        readonly ReadOnlyCollection<StateReadDependency> reads;
        readonly ReadOnlyCollection<StateWriteDeclaration> writes;
        readonly Dictionary<StateRecordKey, StateReadDependency> readByKey;
        readonly Dictionary<StateRecordKey, StateWriteDeclaration> writeByKey;

        public readonly StateCommandIdentity Identity;
        public readonly StateCommandVersionFence Versions;
        public ReadOnlyCollection<StateReadDependency> Reads { get { return reads; } }
        public ReadOnlyCollection<StateWriteDeclaration> Writes { get { return writes; } }

        public StateCommandManifest(StateCommandIdentity identity, StateCommandVersionFence versions,
            IEnumerable<StateReadDependency> readDependencies, IEnumerable<StateWriteDeclaration> writeDeclarations)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (versions == null) throw new ArgumentNullException(nameof(versions));
            if (readDependencies == null) throw new ArgumentNullException(nameof(readDependencies));
            if (writeDeclarations == null) throw new ArgumentNullException(nameof(writeDeclarations));
            var readArray = ReadBounded(readDependencies, MaxDeclaredRecords, nameof(readDependencies));
            var writeArray = ReadBounded(writeDeclarations, MaxDeclaredRecords, nameof(writeDeclarations));
            if (readArray.Length + writeArray.Length > MaxDeclaredRecords)
                throw new ArgumentOutOfRangeException("Manifest exceeds its total affected-record bound.");
            readByKey = new Dictionary<StateRecordKey, StateReadDependency>();
            foreach (var dependency in readArray)
                if (dependency == null || readByKey.ContainsKey(dependency.Record))
                    throw new ArgumentException("Read dependencies must be non-null and unique by record key.");
                else readByKey.Add(dependency.Record, dependency);
            writeByKey = new Dictionary<StateRecordKey, StateWriteDeclaration>();
            foreach (var declaration in writeArray)
            {
                if (declaration == null || writeByKey.ContainsKey(declaration.Record))
                    throw new ArgumentException("Write declarations must be non-null and unique by record key.");
                if (!readByKey.TryGetValue(declaration.Record, out var dependency))
                    throw new ArgumentException("Every write requires an explicit read dependency, including an absent-create fence.");
                bool expectsExisting = declaration.Kind != StateWriteKind.Create;
                if (dependency.Exists != expectsExisting)
                    throw new ArgumentException("Write kind conflicts with its expected record existence.");
                if (dependency.Version != declaration.ExpectedVersion)
                    throw new ArgumentException("Write version differs from its read dependency.");
                writeByKey.Add(declaration.Record, declaration);
            }
            Identity = identity;
            Versions = versions;
            reads = Array.AsReadOnly(readArray);
            writes = Array.AsReadOnly(writeArray);
        }

        static T[] ReadBounded<T>(IEnumerable<T> source, int max, string parameterName)
        {
            var items = new List<T>();
            foreach (var item in source)
            {
                if (items.Count == max)
                    throw new ArgumentOutOfRangeException(parameterName, "Manifest exceeds its affected-record bound.");
                items.Add(item);
            }
            return items.ToArray();
        }

        // Call with versions captured by the state owner. This never reads live state itself.
        public bool ValidateCurrentFence(string currentSaveSlot, long currentResetGeneration,
            StateCommandVersionFence currentVersions, out StateCommandContractError error)
        {
            if (!string.Equals(Identity.SaveSlot, currentSaveSlot, StringComparison.Ordinal))
                error = StateCommandContractError.StaleSaveSlot;
            else if (Identity.ResetGeneration != currentResetGeneration)
                error = StateCommandContractError.StaleResetGeneration;
            else if (currentVersions == null)
                error = StateCommandContractError.InvalidVersionFence;
            else if (Versions.ExpectedLedgerRevision != currentVersions.ExpectedLedgerRevision)
                error = StateCommandContractError.StaleLedgerRevision;
            else if (Versions.ExpectedStorageCommit != currentVersions.ExpectedStorageCommit)
                error = StateCommandContractError.StaleStorageCommit;
            else if (Versions.ExpectedContentVersion != currentVersions.ExpectedContentVersion)
                error = StateCommandContractError.StaleContentVersion;
            else { error = StateCommandContractError.None; return true; }
            return false;
        }

        // The owner supplies the observed read-set after capture/revalidation.
        // Missing and extra observations are rejected to keep the manifest bounded and exact.
        public bool ValidateReadSet(IEnumerable<StateReadDependency> ownerObserved,
            out StateCommandContractError error)
        {
            if (ownerObserved == null) { error = StateCommandContractError.StaleRecordDependency; return false; }
            var observed = new Dictionary<StateRecordKey, StateReadDependency>();
            foreach (var item in ownerObserved)
            {
                if (observed.Count >= readByKey.Count)
                { error = StateCommandContractError.StaleRecordDependency; return false; }
                if (item == null || observed.ContainsKey(item.Record))
                { error = StateCommandContractError.DuplicateReadDependency; return false; }
                observed.Add(item.Record, item);
            }
            if (observed.Count != readByKey.Count)
            { error = StateCommandContractError.StaleRecordDependency; return false; }
            foreach (var expected in readByKey)
            {
                if (!observed.TryGetValue(expected.Key, out var actual) ||
                    actual.Exists != expected.Value.Exists || actual.Version != expected.Value.Version)
                { error = StateCommandContractError.StaleRecordDependency; return false; }
            }
            error = StateCommandContractError.None;
            return true;
        }

        // Observed writes are metadata from the candidate writer. Declared no-op writes are allowed;
        // any write outside the captured manifest or with different intent/version is rejected.
        public bool ValidateObservedWrites(IEnumerable<StateWriteDeclaration> observedWrites,
            out StateCommandContractError error)
        {
            if (observedWrites == null) { error = StateCommandContractError.UndeclaredWrite; return false; }
            var observed = new HashSet<StateRecordKey>();
            foreach (var actual in observedWrites)
            {
                if (observed.Count >= writeByKey.Count)
                { error = StateCommandContractError.UndeclaredWrite; return false; }
                if (actual == null || !observed.Add(actual.Record))
                { error = StateCommandContractError.DuplicateObservedWrite; return false; }
                if (!writeByKey.TryGetValue(actual.Record, out var declared) ||
                    declared.Kind != actual.Kind || declared.ExpectedVersion != actual.ExpectedVersion)
                { error = StateCommandContractError.UndeclaredWrite; return false; }
            }
            error = StateCommandContractError.None;
            return true;
        }
    }

    public enum StateCommandLifecycle
    {
        Preparing,
        ReadyToCommit,
        CanceledBeforeCommit,
        RejectedBeforeCommit,
        CommitInProgress,
        OutcomeUnknown,
        Committed,
        RolledBack
    }

    public sealed class StateCommandReceipt
    {
        public readonly StateCommandIdentity Identity;
        public readonly long CommittedLedgerRevision;
        public readonly long CommittedStorageCommit;

        public StateCommandReceipt(StateCommandIdentity identity, long committedLedgerRevision,
            long committedStorageCommit)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (committedLedgerRevision <= 0 || committedLedgerRevision == long.MaxValue ||
                committedStorageCommit <= 0 || committedStorageCommit == long.MaxValue)
                throw new ArgumentOutOfRangeException("A committed receipt requires usable positive committed versions.");
            Identity = identity;
            CommittedLedgerRevision = committedLedgerRevision;
            CommittedStorageCommit = committedStorageCommit;
        }

        public bool Matches(StateCommandIdentity identity, StateCommandVersionFence expected)
        {
            return Identity.Equals(identity) && expected != null &&
                CommittedLedgerRevision == expected.ExpectedLedgerRevision + 1 &&
                CommittedStorageCommit == expected.ExpectedStorageCommit + 1;
        }
    }

    public static class StateCommandLifecycleRules
    {
        public static bool CanTransition(StateCommandLifecycle from, StateCommandLifecycle to)
        {
            switch (from)
            {
                case StateCommandLifecycle.Preparing:
                    return to == StateCommandLifecycle.ReadyToCommit || to == StateCommandLifecycle.CanceledBeforeCommit || to == StateCommandLifecycle.RejectedBeforeCommit;
                case StateCommandLifecycle.ReadyToCommit:
                    return to == StateCommandLifecycle.CommitInProgress || to == StateCommandLifecycle.CanceledBeforeCommit || to == StateCommandLifecycle.RejectedBeforeCommit;
                case StateCommandLifecycle.CommitInProgress:
                    return to == StateCommandLifecycle.Committed || to == StateCommandLifecycle.OutcomeUnknown || to == StateCommandLifecycle.RolledBack;
                case StateCommandLifecycle.OutcomeUnknown:
                    return to == StateCommandLifecycle.Committed || to == StateCommandLifecycle.RolledBack;
                default:
                    return false;
            }
        }

        public static bool ValidateTransition(StateCommandLifecycle from, StateCommandLifecycle to,
            StateCommandIdentity identity, StateCommandVersionFence expected,
            StateCommandReceipt receipt, out StateCommandContractError error)
        {
            if (!CanTransition(from, to))
            { error = StateCommandContractError.InvalidLifecycleTransition; return false; }
            if (to == StateCommandLifecycle.Committed)
            {
                if (identity == null || receipt == null || !receipt.Identity.Equals(identity))
                { error = StateCommandContractError.ReceiptIdentityMismatch; return false; }
                if (expected == null || !receipt.Matches(identity, expected))
                { error = StateCommandContractError.ReceiptVersionMismatch; return false; }
            }
            else if (receipt != null)
            { error = StateCommandContractError.ReceiptIdentityMismatch; return false; }
            error = StateCommandContractError.None;
            return true;
        }
    }
}
