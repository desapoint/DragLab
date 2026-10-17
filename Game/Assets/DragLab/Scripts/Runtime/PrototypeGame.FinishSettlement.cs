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
        void AdvanceFinishSettlement()
        {
            if(!ResultSettlementPending)return;
            if(finishBound!=race){finishBound=race;finishRetryAt=0;notice="";}
            if(Time.realtimeSinceStartup<finishRetryAt)return;
            long stamp=FinishMeasurement==null?0:System.Diagnostics.Stopwatch.GetTimestamp();
            try{
                if(finishWork==null)finishWork=PrepareFinishSettlement().GetEnumerator();
                if(!finishWork.MoveNext()){finishWork.Dispose();finishWork=null;finishTask=null;}
            }catch(Exception error){
                finishWork?.Dispose();finishWork=null;finishTask=null;finishRetryAt=Time.realtimeSinceStartup+.5f;
                notice="Result save failed; retry retained. "+error.GetBaseException().Message;
                string ignored;ProfileStore.TryCheckpoint(profile,RunPersistence.Capture(liveRunId,liveRunOwner,liveRunTick,race.player),out ignored);
            }finally{if(stamp!=0)FinishMeasurement?.Invoke(finishWork?.Current as string??"settlement/publish",(System.Diagnostics.Stopwatch.GetTimestamp()-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency);}
        }
        IEnumerable<object> PrepareFinishSettlement()
        {
            var session=race;var source=profile;long tick=liveRunTick,revision=source.revision,commit=source.storageCommit;int failures=RunPersistence.FailureCount(session.player);
            var checkpoint=RunPersistence.Capture(liveRunId,liveRunOwner,tick,session.player,false,"Result settled / shutdown still running");
            // No ledger mutation is allowed while this read-only snapshot is cloned.
            var frozen=source.CopyWithPreferences(source.driving,source.difficulty);
            var clone=Task.Run(()=>{var copy=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(frozen));copy.storageCommit=commit;return copy;});
            finishTask=clone;while(!clone.IsCompleted)yield return "settlement/clone wait";
            var candidate=clone.GetAwaiter().GetResult();candidate.catalog=source.catalog;
            MechanicsOwnership.Ensure(candidate,candidate.catalog);yield return "settlement/normalize";
            var old=candidate.runCheckpoints.Find(r=>r.runId==liveRunId);
            if(old==null||old.tick<tick)MechanicsTransactions.ApplyCheckpoint(candidate,checkpoint);
            CareerProgression.Settle(candidate,liveRunId,session,false);candidate.revision=checked(revision+1);yield return "settlement/outcome";
            if(session.player.state.finished&&!session.player.state.disqualified&&!practiceRun)
                foreach(var unused in VehicleListings.AfterRaceSteps(candidate,candidate.catalog))yield return unused;
            MechanicsOwnership.Ensure(candidate,candidate.catalog);yield return "settlement/normalize";
            if(profile!=source||profile.revision!=revision||profile.storageCommit!=commit||race!=session)throw new InvalidOperationException("Settlement became stale; retry required.");
            while(RaceCheckpointStore.Busy)yield return "settlement/checkpoint wait";
            var save=ProfileStore.BeginValidatedSave(candidate);finishTask=save;
            while(!save.IsCompleted)yield return "settlement/save dispatch or wait";
            ProfileStore.CompleteValidatedSave(candidate,save);
            profile=candidate;settings=profile.driving;awarded=true;savedRunTick=tick;savedRunFailures=failures;finishedPass=Build.lastPass;notice="";
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
