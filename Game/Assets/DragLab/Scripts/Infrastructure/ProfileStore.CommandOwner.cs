using System;
using System.IO;
using System.Threading;
using DragLab.Domain;

namespace DragLab.Infrastructure
{
    public static partial class ProfileStore
    {
        sealed class SelectedOwnerProof
        {
            internal PlayerProfile Profile;
            internal string Slot, Path;
            internal long Epoch, ProfileRevision, StorageCommit;
            internal SqliteStateOwnerReceipt Receipt;
        }

        internal sealed class TypedOwnerLease
        {
            internal readonly object OwnerToken;
            internal readonly PlayerProfile Profile;
            internal readonly TypedFluidRefillCoordinator Coordinator;
            internal readonly string Slot, Path;
            internal readonly long Epoch;
            internal bool BeginAttempted;
            internal TypedOwnerLease(object token, PlayerProfile profile, TypedFluidRefillCoordinator coordinator,
                string slot, string path, long epoch)
            { OwnerToken=token; Profile=profile; Coordinator=coordinator; Slot=slot; Path=path; Epoch=epoch; }
        }

        static readonly int commandOwnerThreadId = Thread.CurrentThread.ManagedThreadId;
        static SelectedOwnerProof selectedOwnerProof;
        static TypedOwnerLease typedOwnerLease;
        static long selectionEpoch;
        static bool selectedCheckpointDirty;

        internal static long OwnerEpoch { get { CheckCommandOwnerThread(); return selectionEpoch; } }
        internal static string SelectedSlot { get { CheckCommandOwnerThread(); return slot; } }

        static void CheckCommandOwnerThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != commandOwnerThreadId)
                throw new InvalidOperationException("Selected save ownership is main-thread-only.");
        }

        static string CanonicalOwnerPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A selected save path is required.", nameof(path));
            return Path.GetFullPath(path);
        }

        static bool ContextMatches(string expectedSlot, string expectedPath, long expectedEpoch)
        {
            return expectedEpoch == selectionEpoch && string.Equals(expectedSlot, slot, StringComparison.Ordinal) &&
                string.Equals(CanonicalOwnerPath(expectedPath), CanonicalOwnerPath(FilePath),
                    Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        internal static void InvalidateOwnerProof()
        {
            CheckCommandOwnerThread();
            selectedOwnerProof = null;
        }

        internal static bool RegisterValidatedOwner(PlayerProfile profile, string ownerSlot, string ownerPath,
            long ownerEpoch, SqliteStateOwnerReceipt receipt)
        {
            CheckCommandOwnerThread();
            if (profile == null || receipt == null || receipt.Generation < 0 || receipt.CommitId < 0 ||
                receipt.LedgerRevision < 0 || receipt.ContentRevision < 0 || string.IsNullOrEmpty(receipt.PhysicsIdentity) ||
                !ContextMatches(ownerSlot, ownerPath, ownerEpoch) || selectedCheckpointDirty || writeBlocked ||
                typedOwnerLease != null || profile.revision != receipt.LedgerRevision || profile.storageCommit != receipt.CommitId)
            {
                selectedOwnerProof = null;
                return false;
            }
            selectedOwnerProof = new SelectedOwnerProof
            {
                Profile=profile, Slot=ownerSlot, Path=CanonicalOwnerPath(ownerPath), Epoch=ownerEpoch,
                ProfileRevision=profile.revision, StorageCommit=profile.storageCommit, Receipt=receipt
            };
            return true;
        }

        // Only this owner registry can create a production binding. It performs no storage I/O.
        internal static bool TryGetCommandOwner(PlayerProfile profile, out TypedSaveSlotBinding binding)
            => TryGetCommandOwner(profile,out binding,out _);

        internal static bool TryGetCommandOwner(PlayerProfile profile, out TypedSaveSlotBinding binding,
            out SqliteStateOwnerReceipt receipt)
        {
            CheckCommandOwnerThread();
            binding = null;
            receipt = null;
            var proof = selectedOwnerProof;
            if (proof == null || profile == null || IsValidatedSavePending() || !ReferenceEquals(profile, proof.Profile) || writeBlocked ||
                selectedCheckpointDirty || !ContextMatches(proof.Slot, proof.Path, proof.Epoch) ||
                !string.Equals(CanonicalOwnerPath(FilePath), proof.Path,
                    Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                profile.revision != proof.ProfileRevision || profile.storageCommit != proof.StorageCommit ||
                proof.Receipt.Generation < 0 || proof.Receipt.CommitId != profile.storageCommit ||
                proof.Receipt.LedgerRevision != profile.revision || typedOwnerLease != null)
            {
                if (proof != null && (writeBlocked || selectedCheckpointDirty ||
                    !ContextMatches(proof.Slot, proof.Path, proof.Epoch) || profile == proof.Profile &&
                    (profile.revision != proof.ProfileRevision || profile.storageCommit != proof.StorageCommit)))
                    selectedOwnerProof = null;
                return false;
            }
            binding = new TypedSaveSlotBinding(proof.Path, proof.Slot, proof.Receipt.Generation);
            receipt = proof.Receipt;
            return true;
        }

        internal static bool TryAcquireTypedLease(PlayerProfile profile, TypedFluidRefillCoordinator coordinator,
            out TypedOwnerLease lease)
        {
            CheckCommandOwnerThread();
            lease = null;
            if (coordinator == null || coordinator.HasPending || !TryGetCommandOwner(profile, out _)) return false;
            var proof = selectedOwnerProof;
            if (proof == null || typedOwnerLease != null) return false;
            lease = new TypedOwnerLease(proof, profile, coordinator, proof.Slot, proof.Path, proof.Epoch);
            typedOwnerLease = lease;
            return true;
        }

        internal static bool ReleaseTypedLease(TypedOwnerLease lease)
        {
            CheckCommandOwnerThread();
            if (lease == null || !ReferenceEquals(typedOwnerLease, lease) || !lease.BeginAttempted || lease.Coordinator.HasPending) return false;
            typedOwnerLease = null;
            // Acknowledgement ends the operation; the profile must be rebound after its
            // affected-row patch is published with the new durable receipt.
            selectedOwnerProof = null;
            return true;
        }

        // Acquires the owner lease before TryBegin invokes the caller's factory. The
        // wrapper records the factory/admission attempt so an unstarted lease cannot
        // be dropped and cannot leave a selected owner temporarily unfenced.
        internal static bool TryBeginTypedOperation(PlayerProfile profile, TypedFluidRefillCoordinator coordinator,
            Func<long,TypedFluidRefillCommand> factory, long executionNowTicks, out TypedOwnerLease lease, out string error)
        {
            CheckCommandOwnerThread();lease=null;error=null;
            if(factory==null){error="A fluid command factory is required.";return false;}
            if(!TryGetCommandOwner(profile,out var binding,out _)){error="The selected profile has no current typed owner proof.";return false;}
            if(!TryAcquireTypedLease(profile,coordinator,out lease)){error="The selected owner or coordinator is already leased.";return false;}
            bool began;
            try { began=coordinator.TryBegin(binding,factory,executionNowTicks,out error); }
            catch(Exception ex){error=ex.Message;began=false;}
            lease.BeginAttempted=true;
            if(!began)
            {
                // Scheduling/factory rejection is definite only when no operation was retained.
                if(!coordinator.HasPending){ReleaseTypedLease(lease);lease=null;}
                return false;
            }
            return true;
        }

        internal static bool CanBeginLegacyWrite() => typedOwnerLease == null && !IsValidatedSavePending();
        internal static bool CanChangeSelection() => typedOwnerLease == null && !IsValidatedSavePending();
        internal static bool CanUseCheckpoint() => typedOwnerLease == null && !IsValidatedSavePending();
        internal static void MarkSelectedCheckpointDirty() { CheckCommandOwnerThread(); selectedCheckpointDirty=true; selectedOwnerProof=null; }
        internal static void MarkSelectedCheckpointVerifiedClean() { CheckCommandOwnerThread(); selectedCheckpointDirty=false; }

        internal static void CaptureSelectedContext(out string capturedSlot, out string capturedPath, out long capturedEpoch)
        {
            CheckCommandOwnerThread(); capturedSlot=slot; capturedPath=CanonicalOwnerPath(FilePath); capturedEpoch=selectionEpoch;
        }

        internal static void InvalidateForFailedLoad()
        {
            CheckCommandOwnerThread(); selectedOwnerProof=null; selectedCheckpointDirty=false;
        }
    }
}
