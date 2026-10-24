using System;
using System.Threading.Tasks;
using DragLab.Domain;
using UnityEngine;
namespace DragLab.Infrastructure
{
    public static partial class ProfileStore
    {
        // Observer for the verified player-baseline fixture only. Publishing on
        // the main thread prevents writer callbacks from racing the report.
        internal static Action<string,double> ValidatedSaveMeasurement;
        internal sealed class ValidatedSaveResult
        {
            internal long commit;
            internal SqliteStateStore.SaveTrace trace;
        }
        internal static Task<ValidatedSaveResult> BeginValidatedSave(PlayerProfile candidate)
        {
            if(writeBlocked)throw new InvalidOperationException("Save writes blocked; existing database preserved.");
            RaceCheckpointStore.Drain();string warning;
            RaceCheckpointStore.Merge(FilePath,candidate,false,out warning);
            DrivingPreferences.Ensure(candidate);SqliteStateStore.Prepare();
            string path=FilePath,build=Application.version;
            bool profileSave=(ValidatedSaveMeasurement!=null);
            var trace=profileSave?new SqliteStateStore.SaveTrace():null;
            long captureStart=profileSave?System.Diagnostics.Stopwatch.GetTimestamp():0;
            var snapshot=SqliteStateStore.LegacyCaptureManaged(candidate,build,trace);
            if(trace!=null)trace.Elapsed("owner complete capture",captureStart);
            long dispatched=profileSave?System.Diagnostics.Stopwatch.GetTimestamp():0;
            return Task.Run(()=>{
                long workStart=profileSave?System.Diagnostics.Stopwatch.GetTimestamp():0;
                if(trace!=null)trace.Elapsed("worker dispatch wait",dispatched);
                SqliteStateStore.WriteLegacy(path,snapshot);
                if(trace!=null)trace.Elapsed("entire save worker",workStart);
                return new ValidatedSaveResult{commit=snapshot.committed,trace=trace};
            });
        }
        internal static void CompleteValidatedSave(PlayerProfile candidate,Task<ValidatedSaveResult> save)
        {
            var completed=save.GetAwaiter().GetResult();
            candidate.storageCommit=completed.commit;
            RaceCheckpointStore.Retire(FilePath,false);
            Status="Saved.";
            // Diagnostic reporting must never affect a successful durable commit.
            var observer=ValidatedSaveMeasurement;
            if(observer!=null&&completed.trace!=null)
                foreach(var sample in completed.trace.stages)
                    try{observer(sample.Key,sample.Value);}catch(Exception){}
        }
    }
}
