using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;
namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator FinishPacingChecks(WindowsKeyboardProbe input)
        {
            foreach(bool paid in new[]{false,true})
            {
                game.ReturnToGarage();game.Profile.driving.launch=LaunchMode.Automatic;game.Profile.driving.shifting=ShiftMode.Automatic;
                game.Profile.driving.wearMode=WearMode.None;game.Profile.driving.optimalThermals=true;
                if(paid)game.Profile.vehicleStock.completedRuns+=((2-game.Profile.vehicleStock.completedRuns%3)+3)%3;
                Check(ProfileStore.TrySave(game.Profile),"Finish pacing fixture has a durable baseline");
                int cash=game.Profile.cash,generation=game.Profile.vehicleStock.generation;double maxStep=0,maxCamera=0,maxFrame=0;string maxStage="";
                game.StartRace(null,!paid);var race=game.Race;
PrototypeGame.FinishMeasurement=(name,value)=>{if(name=="camera step units")maxCamera=Math.Max(maxCamera,value);else if(value>maxStep){maxStep=value;maxStage=name;}};
                Check(race!=null,"Finish pacing stages a real "+(paid?"paid refresh":"practice")+" run");
                game.verificationInput=()=>new DriverInput{throttle=1};int mainThread=Thread.CurrentThread.ManagedThreadId,writerThread=mainThread;
                var entered=new ManualResetEventSlim();var release=new ManualResetEventSlim();
                {
                    SqliteStateStore.FailureInjection=stage=>{if(stage=="before-commit"&&race.phase==RacePhase.Finished){writerThread=Thread.CurrentThread.ManagedThreadId;entered.Set();if(!release.Wait(8000))throw new IOException("Slow-write fixture timed out");}};
                    try{
                        float deadline=Time.realtimeSinceStartup+75;long previous=System.Diagnostics.Stopwatch.GetTimestamp();
                        while(!entered.IsSet&&Time.realtimeSinceStartup<deadline){
                            if(race.phase==RacePhase.Finished){long now=System.Diagnostics.Stopwatch.GetTimestamp();maxFrame=Math.Max(maxFrame,(now-previous)*1000.0/System.Diagnostics.Stopwatch.Frequency);}
                            previous=System.Diagnostics.Stopwatch.GetTimestamp();yield return null;
                        }
                        Check(entered.IsSet&&game.ResultSettlementPending,"Real settlement reaches a held durable write without blocking the UI");
                        game.HandleApplicationInterruption();Check(game.ResultSettlementPending&&!game.NativeRacePause,"Finished focus loss neither pauses nor waits on the held writer");
                        int frames=Time.frameCount;double shutdown=race.player.state.shutdownTime;yield return new WaitForSecondsRealtime(.5f);
                        Check(Time.frameCount-frames>=10&&race.player.state.shutdownTime>shutdown+.2,"Frames and physical shutdown continue while durable write is held");
                        Check(writerThread!=mainThread,"Durable write executes on the isolated writer, not Unity's main thread");
                        Check(game.Profile.cash==cash&&!game.Profile.careerRuns.Last().complete,"Cash and complete receipt remain unpublished before durable commit");
                        Check(!game.Results.Root.Q("resultAgain").enabledInHierarchy&&!game.Results.Root.Q("resultGarage").enabledInHierarchy,"Results cannot start a conflicting action while settlement is pending");
                        report.AppendLine("MEASURE: finish "+(paid?"paid refresh":"practice")+" / max main step ms "+maxStep.ToString("F3")+" / max stage "+maxStage+" / max frame ms "+maxFrame.ToString("F3")+" / max camera step "+maxCamera.ToString("F3")+" / held-write frames "+(Time.frameCount-frames));
                        Check(maxStep<50&&maxCamera<.35,"Finish preparation is split into bounded main steps and camera motion remains continuous");
                    }finally{release.Set();SqliteStateStore.FailureInjection=null;game.verificationInput=null;PrototypeGame.FinishMeasurement=null;}
                }
                float settledDeadline=Time.realtimeSinceStartup+12;
                if(paid){game.ReturnToGarage();Check(game.Race==null,"Explicit Garage boundary drains pending settlement before exit");}
                else{game.SendMessage("OnApplicationPause",true);Check(!game.ResultSettlementPending&&!game.NativeRacePause,"Actual suspend boundary drains pending work without auto-pausing finished results");}
                while(game.ResultSettlementPending&&Time.realtimeSinceStartup<settledDeadline)yield return null;
                Check(!game.ResultSettlementPending&&game.Profile.careerRuns.Last().complete,"Successful background commit publishes exactly one completed receipt");
                int expected=paid?(race.playerWon?120:40):0;
                Check(game.Profile.cash==cash+expected,"Background settlement applies the captured reward exactly once");
                Check(game.Profile.vehicleStock.generation==generation+(paid?1:0)&&game.Profile.inventory.market.generation==game.Profile.vehicleStock.generation,"Paid third-race stock refresh stays seeded and shares the car/part epoch");
                game.ReturnToGarage();var reloaded=ProfileStore.Load();
                Check(reloaded.cash==game.Profile.cash&&reloaded.runCheckpoints.All(r=>r.settled),"Background settlement and final shutdown resources survive durable exit/reload");entered.Dispose();release.Dispose();
            }
        }
#endif
    }
}
