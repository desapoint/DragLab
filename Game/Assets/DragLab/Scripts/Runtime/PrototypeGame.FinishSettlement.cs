using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        IEnumerator<object> finishWork;
        Task finishTask;
        float finishRetryAt;
        RaceSimulation finishBound;
        internal bool ResultSettlementPending=>race!=null&&race.ResultReady&&!awarded;
        internal static Action<string,double> FinishMeasurement;
        // Opt-in wall-clock spans for player-build baselines. Never changes settlement pacing.
        internal static Action<string,double> FinishWallMeasurement;
        static long WallStamp()=>FinishWallMeasurement==null?0:System.Diagnostics.Stopwatch.GetTimestamp();
        // Observed on the main thread after Task completion by packaged-player tests.
        internal static Action<int> FinishValidationWorkerThread;
        sealed class WorkerValidationResult
        {
            internal string error;
            internal int threadId;
            internal double cpuMilliseconds;
        }
        // Validation is read-only, but PartsMarket.Validate uses profile.catalog.
        // The profile is unpublished and cannot be touched by gameplay while this
        // job runs. Restore the original catalog on main before the next step.
        IEnumerable<object> ValidateFinishCandidate(PlayerProfile candidate,
            MechanicalCatalog originalCatalog,MechanicalCatalog detachedCatalog)
        {
            candidate.catalog=detachedCatalog;
            var work=Task.Run(()=>{
                long started=System.Diagnostics.Stopwatch.GetTimestamp();
                string error=MechanicsOwnership.Validate(candidate,detachedCatalog);
                return new WorkerValidationResult{
                    error=error,
                    threadId=System.Threading.Thread.CurrentThread.ManagedThreadId,
                    cpuMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*
                        1000.0/System.Diagnostics.Stopwatch.Frequency};
            });
            finishTask=work;
            while(!work.IsCompleted)yield return "settlement/validation wait";
            WorkerValidationResult result;
            try {result=work.GetAwaiter().GetResult();}
            finally {candidate.catalog=originalCatalog;}
            FinishWallMeasurement?.Invoke("validation worker CPU",result.cpuMilliseconds);
            FinishValidationWorkerThread?.Invoke(result.threadId);
            if(result.error!=null)throw new InvalidOperationException(result.error);
            yield return "settlement/validation complete";
        }
        static void EmitWall(string name,long start)
        {
            if(start!=0)FinishWallMeasurement?.Invoke(name,(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);
        }
        void AdvanceFinishSettlement()
        {
            if(!ResultSettlementPending)return;
            if(finishBound!=race){finishBound=race;finishRetryAt=0;notice="";}
            if(Time.realtimeSinceStartup<finishRetryAt)return;
            long stamp=FinishMeasurement==null?0:System.Diagnostics.Stopwatch.GetTimestamp();
            try{
                // Drain cheap iterator boundaries within this frame instead of forcing
                // hundreds of additional frames for product keys, wear and offer rows.
                // Never busy-spin on background work or an external writer.
                long frameStart=System.Diagnostics.Stopwatch.GetTimestamp();
                while(true){
                    if(finishWork==null)finishWork=PrepareFinishSettlement().GetEnumerator();
                    if(!finishWork.MoveNext()){finishWork.Dispose();finishWork=null;finishTask=null;break;}
                    if(finishWork.Current is MarketWorkerWait waiting){
                        finishTask=waiting.task;
                        if(!waiting.task.IsCompleted)break;
                    }else if(finishWork.Current is string status&&status.EndsWith("wait",StringComparison.Ordinal))
                        break;
                    if((System.Diagnostics.Stopwatch.GetTimestamp()-frameStart)*1000.0/
                        System.Diagnostics.Stopwatch.Frequency>=2.0)break;
                }
            }catch(Exception error){
                finishWork?.Dispose();finishWork=null;finishTask=null;finishRetryAt=Time.realtimeSinceStartup+.5f;
                notice="Result save failed; retry retained. "+error.GetBaseException().Message;
                string ignored;ProfileStore.TryCheckpoint(profile,RunPersistence.Capture(liveRunId,liveRunOwner,liveRunTick,race.player),out ignored);
            }finally{if(stamp!=0)FinishMeasurement?.Invoke(finishWork?.Current as string??"settlement/publish",(System.Diagnostics.Stopwatch.GetTimestamp()-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency);}
        }
        IEnumerable<object> PrepareFinishSettlement()
        {
            long entire=WallStamp();
            var session=race;var source=profile;long tick=liveRunTick,revision=source.revision,commit=source.storageCommit;int failures=RunPersistence.FailureCount(session.player);
            var checkpoint=RunPersistence.Capture(liveRunId,liveRunOwner,tick,session.player,false,"Result settled / shutdown still running");
            // No ledger mutation is allowed while this read-only snapshot is cloned.
            var frozen=source.CopyWithPreferences(source.driving,source.difficulty);
            long cloning=WallStamp();
            var clone=Task.Run(()=>{var copy=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(frozen));copy.storageCommit=commit;return copy;});
            finishTask=clone;while(!clone.IsCompleted)yield return "settlement/clone wait";
            var candidate=clone.GetAwaiter().GetResult();candidate.catalog=source.catalog;
            EmitWall("clone task",cloning);
            // Normalize Unity-dependent state on main. No full validator call
            // may block this frame; it will run against detached managed data.
            foreach(var stage in MechanicsOwnership.EnsureSteps(candidate,candidate.catalog,false))
                yield return stage;
            long catalogStamp=System.Diagnostics.Stopwatch.GetTimestamp();
            var detachedCatalog=source.catalog.DetachedNewSupplySnapshot();
            double catalogMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-catalogStamp)*
                1000.0/System.Diagnostics.Stopwatch.Frequency;
            FinishWallMeasurement?.Invoke("validation catalog snapshot",catalogMilliseconds);
            FinishMeasurement?.Invoke("settlement/catalog snapshot",catalogMilliseconds);
            foreach(var step in ValidateFinishCandidate(candidate,source.catalog,detachedCatalog))
                yield return step;
            var old=candidate.runCheckpoints.Find(r=>r.runId==liveRunId);
            if(old==null||old.tick<tick)MechanicsTransactions.ApplyCheckpoint(candidate,checkpoint);
            CareerProgression.Settle(candidate,liveRunId,session,false);candidate.revision=checked(revision+1);yield return "settlement/outcome";
            long market=WallStamp();
            if(session.player.state.finished&&!session.player.state.disqualified&&!practiceRun)
                foreach(var unused in VehicleListings.AfterRaceSteps(candidate,candidate.catalog))yield return unused;
            EmitWall("after-race stock generation including yields",market);
            foreach(var stage in MechanicsOwnership.EnsureSteps(candidate,candidate.catalog,false))
                yield return stage;
            foreach(var step in ValidateFinishCandidate(candidate,source.catalog,detachedCatalog))
                yield return step;
            if(profile!=source||profile.revision!=revision||profile.storageCommit!=commit||race!=session)throw new InvalidOperationException("Settlement became stale; retry required.");
            long checkpointWait=WallStamp();
            while(RaceCheckpointStore.Busy)yield return "settlement/checkpoint wait";
            EmitWall("checkpoint writer wait",checkpointWait);
            long dispatch=WallStamp();
            var save=ProfileStore.BeginValidatedSave(candidate);finishTask=save;
            EmitWall("save dispatch main thread",dispatch);
            long writer=WallStamp();
            while(!save.IsCompleted)yield return "settlement/save dispatch or wait";
            ProfileStore.CompleteValidatedSave(candidate,save);
            EmitWall("save task and completion",writer);
            profile=candidate;settings=profile.driving;awarded=true;savedRunTick=tick;savedRunFailures=failures;finishedPass=Build.lastPass;notice="";
            EmitWall("finish-to-commit total",entire);
        }
        bool FlushFinishSettlement()
        {
            if(!ResultSettlementPending)return true;finishRetryAt=0;
            do{
                if(finishTask!=null&&!finishTask.IsCompleted){try{finishTask.GetAwaiter().GetResult();}catch{} }
                AdvanceFinishSettlement();
                if(finishWork!=null&&(VehicleAssessmentCache.Pending||RaceCheckpointStore.Busy))System.Threading.Thread.Sleep(1);
            }while(finishWork!=null);
            return awarded;
        }
    }
}
