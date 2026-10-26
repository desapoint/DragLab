using System;
using System.Threading;
using System.Threading.Tasks;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Managed tests for synchronous, thread-local Parts encoder attribution.</summary>
    public static class NativePartEncoderProbeChecks
    {
        static void Check(Action<bool,string> check,bool result,string label)
        {check(result,label);if(!result)throw new InvalidOperationException(label);}

        public static void Run(Action<bool,string> check,string fixtureDirectory)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            long json0=RuntimeDataEncodingProbe.PartConsumerJsonCallCount;
            long key0=RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount;
            long canonicalJson0=MarketCatalogLayout.CanonicalJsonEncodeCount;
            long canonicalKey0=MarketCatalogLayout.CanonicalKeyRequestCount;

            RuntimeDataEncodingProbe.RecordJsonCall();
            RuntimeDataEncodingProbe.RecordLegacyKeyCall();
            Check(check,RuntimeDataEncodingProbe.PartConsumerJsonCallCount==json0&&
                RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount==key0,
                "Encoder hooks outside an operation scope are not attributed");

            using(RuntimeDataEncodingProbe.Measure("probe/nested-outer"))
            {
                RuntimeDataEncodingProbe.RecordJsonCall();
                using(RuntimeDataEncodingProbe.Measure("probe/nested-inner"))
                {
                    RuntimeDataEncodingProbe.RecordJsonCall();
                    RuntimeDataEncodingProbe.RecordLegacyKeyCall();
                }
                RuntimeDataEncodingProbe.RecordLegacyKeyCall();
            }
            Check(check,RuntimeDataEncodingProbe.PartConsumerJsonCallCount==json0+2&&
                RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount==key0+2&&
                RuntimeDataEncodingProbe.LastOperation=="probe/nested-outer",
                "Nested scopes attribute each known hook call once to the outer operation");

            bool threw=false;
            try
            {
                using(RuntimeDataEncodingProbe.Measure("probe/exception"))
                {
                    RuntimeDataEncodingProbe.RecordJsonCall();
                    RuntimeDataEncodingProbe.RecordLegacyKeyCall();
                    throw new InvalidOperationException("scope exception sentinel");
                }
            }
            catch(InvalidOperationException error) when(error.Message=="scope exception sentinel"){threw=true;}
            long afterExceptionJson=RuntimeDataEncodingProbe.PartConsumerJsonCallCount;
            long afterExceptionKey=RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount;
            Check(check,threw&&afterExceptionJson==json0+3&&afterExceptionKey==key0+3&&
                RuntimeDataEncodingProbe.LastOperation=="probe/exception",
                "Exception unwinding disposes the scope and records its synchronous hook deltas");
            RuntimeDataEncodingProbe.RecordJsonCall();RuntimeDataEncodingProbe.RecordLegacyKeyCall();
            Check(check,RuntimeDataEncodingProbe.PartConsumerJsonCallCount==afterExceptionJson&&
                RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount==afterExceptionKey,
                "Exception disposal restores the scope depth for later outside-scope calls");

            using(RuntimeDataEncodingProbe.Measure("probe/cumulative-a"))RuntimeDataEncodingProbe.RecordJsonCall();
            using(RuntimeDataEncodingProbe.Measure("probe/cumulative-b"))RuntimeDataEncodingProbe.RecordLegacyKeyCall();
            Check(check,RuntimeDataEncodingProbe.PartConsumerJsonCallCount==json0+4&&
                RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount==key0+4&&
                RuntimeDataEncodingProbe.LastOperation=="probe/cumulative-b",
                "Sequential same-thread scopes add to cumulative process-lifetime totals");

            int mainThread=Thread.CurrentThread.ManagedThreadId;int workerThread=mainThread;
            long beforeWorkerJson=RuntimeDataEncodingProbe.PartConsumerJsonCallCount;
            long beforeWorkerKey=RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount;
            using(RuntimeDataEncodingProbe.Measure("probe/main-with-worker"))
            {
                Task.Run(()=>
                {
                    workerThread=Thread.CurrentThread.ManagedThreadId;
                    RuntimeDataEncodingProbe.RecordJsonCall();
                    RuntimeDataEncodingProbe.RecordLegacyKeyCall();
                }).GetAwaiter().GetResult();
                RuntimeDataEncodingProbe.RecordJsonCall();
            }
            Check(check,workerThread!=mainThread&&
                RuntimeDataEncodingProbe.PartConsumerJsonCallCount==beforeWorkerJson+1&&
                RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount==beforeWorkerKey&&
                RuntimeDataEncodingProbe.LastOperation=="probe/main-with-worker",
                "Worker-thread hooks stay outside an open main-thread scope while the main call is attributed");

            Check(check,MarketCatalogLayout.CanonicalJsonEncodeCount==canonicalJson0&&
                MarketCatalogLayout.CanonicalKeyRequestCount==canonicalKey0,
                "Probe checks call only the scalar hooks and do not invoke encoders or canonical-key workers");
        }
    }
#endif
}
