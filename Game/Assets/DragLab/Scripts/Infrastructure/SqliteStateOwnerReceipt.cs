using System;

namespace DragLab.Infrastructure
{
    /// <summary>
    /// Immutable proof of the selected state's persisted owner metadata.
    /// This value contains no profile, Unity, or owner-context references.
    /// </summary>
    internal sealed class SqliteStateOwnerReceipt
    {
        internal readonly long CommitId;
        internal readonly long Generation;
        internal readonly long LedgerRevision;
        internal readonly long ContentRevision;
        internal readonly string PhysicsIdentity;

        internal SqliteStateOwnerReceipt(long commitId, long generation, long ledgerRevision,
            long contentRevision, string physicsIdentity)
        {
            if (commitId < 0) throw new ArgumentOutOfRangeException(nameof(commitId));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if (ledgerRevision < 0) throw new ArgumentOutOfRangeException(nameof(ledgerRevision));
            if (contentRevision < 0) throw new ArgumentOutOfRangeException(nameof(contentRevision));
            if (string.IsNullOrEmpty(physicsIdentity)) throw new ArgumentException("Physics identity is required.", nameof(physicsIdentity));
            CommitId = commitId;
            Generation = generation;
            LedgerRevision = ledgerRevision;
            ContentRevision = contentRevision;
            PhysicsIdentity = physicsIdentity;
        }
    }
}
