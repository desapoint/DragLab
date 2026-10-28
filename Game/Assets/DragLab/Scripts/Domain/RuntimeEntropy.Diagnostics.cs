using System;
using System.Collections.Generic;
using System.Threading;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace DragLab.Domain
{
    /// <summary>Opt-in read-only observation of IDs that RuntimeEntropy already generated.</summary>
    public static partial class RuntimeEntropy
    {
        const int MaximumIdentityObservationEvents=65536;
        [ThreadStatic] static IdentityObservation activeIdentityObservation;

        internal readonly struct IdentityEmission
        {
            public readonly string Scope;
            public readonly Guid Value;
            internal IdentityEmission(string scope,Guid value){Scope=scope;Value=value;}
        }

        internal sealed class IdentityObservation : IDisposable
        {
            readonly int ownerThreadId,maxEvents;
            readonly List<IdentityEmission> events;
            bool overflowed,closed;

            internal IdentityObservation(int capacity)
            {
                ownerThreadId=Thread.CurrentThread.ManagedThreadId;maxEvents=capacity;
                events=new List<IdentityEmission>(Math.Min(capacity,32));
            }

            internal int Count=>events.Count;
            internal bool Overflowed=>overflowed;
            internal bool Closed=>closed;
            internal IdentityEmission EventAt(int index)=>events[index];

            internal void Record(string scope,Guid value)
            {
                if(closed)return;
                if(events.Count>=maxEvents){overflowed=true;return;}
                try{events.Add(new IdentityEmission(scope,value));}
                catch{overflowed=true;}
            }

            internal void MarkIncomplete(){if(!closed)overflowed=true;}

            public void Dispose()
            {
                if(Thread.CurrentThread.ManagedThreadId!=ownerThreadId)
                    throw new InvalidOperationException("Identity observation must be disposed on its originating thread.");
                if(closed)return;
                if(!ReferenceEquals(activeIdentityObservation,this))
                    throw new InvalidOperationException("Identity observation is not the active thread lease.");
                activeIdentityObservation=null;
                closed=true;
            }
        }

        internal static IdentityObservation BeginIdentityObservation(int maxEvents=MaximumIdentityObservationEvents)
        {
            if(maxEvents<1||maxEvents>MaximumIdentityObservationEvents)
                throw new ArgumentOutOfRangeException(nameof(maxEvents),"Identity observation capacity must be from 1 through 65,536 events.");
            if(activeIdentityObservation!=null&&!activeIdentityObservation.Closed)
                throw new InvalidOperationException("Identity observations cannot be nested on one thread.");
            var result=new IdentityObservation(maxEvents);
            activeIdentityObservation=result;
            return result;
        }

        static partial void ObserveGeneratedIdentity(string scope,Guid value)
        {
            var observation=activeIdentityObservation;
            if(observation==null)return;
            try{observation.Record(scope,value);}
            catch{observation.MarkIncomplete();}
        }
    }
}
#endif
