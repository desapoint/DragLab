using System;
using System.Runtime.CompilerServices;

namespace DragLab.Domain.Mechanics
{
    // Reusable, non-persisted, source-scoped index lifetime. Callers own the
    // revision contract and must publish only complete immutable snapshots.
    // Only the main thread accesses the registry. Workers receive completed
    // detached snapshots, not mutable authoritative source objects.
    internal sealed class RuntimeIndexSlot<TSource,TIndex>
        where TSource:class where TIndex:class
    {
        sealed class Entry
        {
            internal long version;
            internal TIndex value;
        }
        readonly ConditionalWeakTable<TSource,Entry> entries=
            new ConditionalWeakTable<TSource,Entry>();

        internal bool TryGet(TSource source,long version,out TIndex index)
        {
            index=null;
            if(source==null||!entries.TryGetValue(source,out var entry)||
                entry.version!=version||entry.value==null)return false;
            index=entry.value;
            return true;
        }
        internal void Publish(TSource source,long version,TIndex index)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            if(index==null)throw new ArgumentNullException(nameof(index));
            var entry=entries.GetValue(source,_=>new Entry());
            entry.value=null;
            entry.version=version;
            entry.value=index;
        }
        internal void Invalidate(TSource source)
        {
            if(source!=null)entries.Remove(source);
        }
    }
}
