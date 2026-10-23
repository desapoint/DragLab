using System;
using System.Collections.Generic;

namespace DragLab.Domain.Mechanics
{
    // Invocation-local lookup metadata for one exact candidate list. This is not a canonical
    // ownership index and does not follow mutations to its source list or instance IDs.
    internal sealed class OwnedPartIdentityIndex : IDisposable
    {
        readonly Dictionary<string, int> positions;
        readonly int sourceCount;
        IList<PartInstance> source;
        bool disposed;

        OwnedPartIdentityIndex(IList<PartInstance> source, Dictionary<string, int> positions)
        {
            this.source = source;
            sourceCount = source.Count;
            this.positions = positions;
        }

        internal static OwnedPartIdentityIndex Build(IList<PartInstance> parts)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));

            var positions = new Dictionary<string, int>(parts.Count, StringComparer.Ordinal);
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part == null)
                    throw new ArgumentException("Cannot index owned parts with a null record.", nameof(parts));
                if (string.IsNullOrWhiteSpace(part.instanceId))
                    throw new ArgumentException("Cannot index an owned part with a blank instance ID.", nameof(parts));
                if (positions.ContainsKey(part.instanceId))
                    throw new ArgumentException("Cannot index duplicate owned part ID: " + part.instanceId, nameof(parts));
                positions.Add(part.instanceId, i);
            }

            return new OwnedPartIdentityIndex(parts, positions);
        }

        // For a sparse synchronous operation, materialize only requested IDs and stop at the
        // last required match. Unscanned source entries are outside this builder's validation.
        internal static OwnedPartIdentityIndex BuildRequested(IList<PartInstance> parts,
            IEnumerable<string> requestedIds)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (requestedIds == null) throw new ArgumentNullException(nameof(requestedIds));

            var remaining = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in requestedIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException("Requested owned-part IDs must be nonblank.", nameof(requestedIds));
                if (!remaining.Add(id))
                    throw new ArgumentException("Requested owned-part IDs must be unique: " + id, nameof(requestedIds));
                if (remaining.Count > parts.Count)
                    throw new ArgumentOutOfRangeException(nameof(requestedIds), "Requested IDs cannot exceed the source record count.");
            }

            var positions = new Dictionary<string, int>(remaining.Count, StringComparer.Ordinal);
            if (remaining.Count > 0)
            {
                for (int i = 0; i < parts.Count && remaining.Count > 0; i++)
                {
                    var part = parts[i];
                    if (part == null)
                        throw new ArgumentException("Cannot scan owned parts with a null record.", nameof(parts));
                    if (string.IsNullOrWhiteSpace(part.instanceId))
                        throw new ArgumentException("Cannot scan an owned part with a blank instance ID.", nameof(parts));
                    if (positions.ContainsKey(part.instanceId))
                        throw new ArgumentException("Requested owned-part ID is ambiguous in the scanned source: " + part.instanceId, nameof(parts));
                    if (!remaining.Contains(part.instanceId)) continue;
                    positions.Add(part.instanceId, i);
                    remaining.Remove(part.instanceId);
                }
            }

            return new OwnedPartIdentityIndex(parts, positions);
        }

        internal int Count
        {
            get { ThrowIfDisposed(); return positions.Count; }
        }

        // The caller must use this only during the synchronous operation that built the index.
        // Structural or identity mutation requires disposal and a fresh build, even when the list
        // count and profile revision remain unchanged. Condition-only edits do not affect positions.
        internal bool TryGetPosition(string instanceId, out int position)
        {
            ThrowIfDisposed();
            position = -1;
            if (source.Count != sourceCount)
                throw new InvalidOperationException("Owned-part list structure changed; discard the index and build a new invocation-local index.");
            if (string.IsNullOrWhiteSpace(instanceId)) return false;
            if (!positions.TryGetValue(instanceId, out int found)) return false;
            if (found >= source.Count || source[found] == null ||
                !string.Equals(source[found].instanceId, instanceId, StringComparison.Ordinal))
                throw new InvalidOperationException("Owned-part index is stale; discard it and build a new invocation-local index.");
            position = found;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            source = null;
            positions.Clear();
        }

        void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OwnedPartIdentityIndex));
        }
    }
}
