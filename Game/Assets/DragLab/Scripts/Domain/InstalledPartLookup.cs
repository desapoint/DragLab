using System;
using System.Collections.Generic;

namespace DragLab.Domain.Mechanics
{
    // One synchronous installed-binding capture. Part handles are borrowed from the source
    // candidate and must not escape the consumer's scope or cross a mutation/callback boundary.
    internal sealed class InstalledPartLookup : IDisposable
    {
        internal struct Entry
        {
            internal readonly string Slot;
            internal readonly PartInstance Part;

            internal Entry(string slot, PartInstance part)
            {
                Slot = slot;
                Part = part;
            }
        }

        Entry[] entries;
        Dictionary<PartCategory, string> definitions;

        InstalledPartLookup(Entry[] entries, Dictionary<PartCategory, string> definitions)
        {
            this.entries = entries;
            this.definitions = definitions;
        }

        internal static InstalledPartLookup Capture(IList<PartInstance> parts, IList<SlotBinding> slots)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (slots == null) throw new ArgumentNullException(nameof(slots));

            var requestedIds = new List<string>(slots.Count);
            var requestedSet = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < slots.Count; i++)
            {
                var binding = slots[i];
                if (binding == null)
                    throw new ArgumentException("Installed bindings cannot contain null rows.", nameof(slots));
                // Preserve the existing missing-part behavior for blank IDs: no definition is
                // emitted and the corresponding ordered entry has a null part.
                string id = binding.partInstanceId;
                if (!string.IsNullOrWhiteSpace(id) && requestedSet.Add(id)) requestedIds.Add(id);
            }

            OwnedPartIdentityIndex index = requestedIds.Count > parts.Count
                ? OwnedPartIdentityIndex.Build(parts)
                : OwnedPartIdentityIndex.BuildRequested(parts, requestedIds);
            try
            {
                var entries = new Entry[slots.Count];
                var definitions = new Dictionary<PartCategory, string>();
                for (int i = 0; i < slots.Count; i++)
                {
                    var binding = slots[i];
                    PartInstance part = null;
                    if (!string.IsNullOrWhiteSpace(binding.partInstanceId) &&
                        index.TryGetPosition(binding.partInstanceId, out int position))
                        part = parts[position];

                    entries[i] = new Entry(binding.slot, part);
                    if (part != null)
                    {
                        var category = (PartCategory)Enum.Parse(typeof(PartCategory), binding.slot);
                        definitions.Add(category, part.definitionId);
                    }
                }
                return new InstalledPartLookup(entries, definitions);
            }
            finally { index.Dispose(); }
        }

        internal int Count
        {
            get { ThrowIfDisposed(); return entries.Length; }
        }

        internal Entry EntryAt(int index)
        {
            ThrowIfDisposed();
            return entries[index];
        }

        internal Dictionary<PartCategory, string> CopyDefinitions()
        {
            ThrowIfDisposed();
            return new Dictionary<PartCategory, string>(definitions);
        }

        public void Dispose()
        {
            if (entries == null) return;
            Array.Clear(entries, 0, entries.Length);
            entries = null;
            definitions.Clear();
            definitions = null;
        }

        void ThrowIfDisposed()
        {
            if (entries == null) throw new ObjectDisposedException(nameof(InstalledPartLookup));
        }
    }
}
