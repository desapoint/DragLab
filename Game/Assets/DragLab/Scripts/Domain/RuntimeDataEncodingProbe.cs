using System;
using System.Threading;

namespace DragLab.Domain.Mechanics
{
    // Thread-local attribution of encoder calls made inside a synchronous UI operation.
    // Calls from the background catalog warm producer remain in global counters only.
    internal static class RuntimeDataEncodingProbe
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [ThreadStatic] static int depth;
        [ThreadStatic] static long localJsonCalls,localLegacyKeyCalls,startJsonCalls,startLegacyKeyCalls;
        [ThreadStatic] static string activeOperation,lastOperation;
        static long jsonCalls,legacyKeyCalls;
        internal static long PartConsumerJsonCallCount=>Interlocked.Read(ref jsonCalls);
        internal static long CurrentThreadJsonCallCount=>localJsonCalls;
        internal static long PartConsumerLegacyKeyCallCount=>Interlocked.Read(ref legacyKeyCalls);
        internal static string LastOperation=>lastOperation;
#else
        internal static long PartConsumerJsonCallCount=>0;
        internal static long CurrentThreadJsonCallCount=>0;
        internal static long PartConsumerLegacyKeyCallCount=>0;
        internal static string LastOperation=>null;
#endif

        [System.Diagnostics.Conditional("UNITY_EDITOR"),System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal static void RecordJsonCall()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(depth>0)localJsonCalls++;
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"),System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal static void RecordLegacyKeyCall()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(depth>0)localLegacyKeyCalls++;
#endif
        }

        internal struct Scope : IDisposable
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            readonly bool active,outer;
            internal Scope(string operation)
            {
                active=true;outer=depth++==0;
                if(outer)
                {
                    activeOperation=operation??"unspecified";
                    startJsonCalls=localJsonCalls;startLegacyKeyCalls=localLegacyKeyCalls;
                }
            }
#else
            internal Scope(string operation){}
#endif
            public void Dispose()
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(!active)return;
                if(depth>0)depth--;
                if(outer&&depth==0)
                {
                    Interlocked.Add(ref jsonCalls,localJsonCalls-startJsonCalls);
                    Interlocked.Add(ref legacyKeyCalls,localLegacyKeyCalls-startLegacyKeyCalls);
                    lastOperation=activeOperation;activeOperation=null;
                }
#endif
            }
        }

        internal static Scope Measure(string operation)=>new Scope(operation);
    }
}
