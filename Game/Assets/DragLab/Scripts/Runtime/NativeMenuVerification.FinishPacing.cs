using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

        // A natural, unblocked packaged-player baseline, separate from the held-writer pacing test.
        // Uses the existing disposable verification profile, never the real player save.
        IEnumerator MarketBaselineChecks(WindowsKeyboardProbe input)
        {
            foreach(string scenario in new[]{"practice","paid ordinary","paid third"})
            {
                game.ReturnToGarage();
                game.Profile.driving.launch=LaunchMode.Automatic;
                game.Profile.driving.shifting=ShiftMode.Automatic;
                game.Profile.driving.wearMode=WearMode.None;
                game.Profile.driving.optimalThermals=true;
                if(scenario=="paid third")
                    game.Profile.vehicleStock.completedRuns+=((2-game.Profile.vehicleStock.completedRuns%3)+3)%3;
                else if(scenario=="paid ordinary"&&game.Profile.vehicleStock.completedRuns%3==2)
                    game.Profile.vehicleStock.completedRuns++;
                Check(ProfileStore.TrySave(game.Profile),"Market baseline "+scenario+" has a durable isolated starting profile");
                int generation=game.Profile.vehicleStock.generation;
                int cash=game.Profile.cash;
                var costs=new SortedDictionary<string,double[]>(StringComparer.Ordinal);
                var wall=new SortedDictionary<string,double>(StringComparer.Ordinal);
                var frames=new List<double>();
                double maxStep=0,maxCamera=0;string maxStage="";
                var priorStep=PrototypeGame.FinishMeasurement;
                var priorWall=PrototypeGame.FinishWallMeasurement;
                var priorMarket=DragLab.Domain.Mechanics.PartsMarket.Measurement;
                float deadline=Time.realtimeSinceStartup+75;
                long readyAt=0,resultsShownAt=0,doneAt=0;
                try
                {
                    // Measuring after StartRace avoids counting setup and initial save as settlement.
                    game.StartRace(null,scenario=="practice");
                    var race=game.Race;
                    Check(race!=null,"Market baseline "+scenario+" stages a real race");
                    game.verificationInput=()=>new DriverInput{throttle=1};
                    PrototypeGame.FinishMeasurement=(name,value)=>{
                        if(name=="camera step units"){maxCamera=Math.Max(maxCamera,value);return;}
                        if(value>maxStep){maxStep=value;maxStage=name;}
                    };
                    PrototypeGame.FinishWallMeasurement=(name,value)=>wall[name]=value;
                    DragLab.Domain.Mechanics.PartsMarket.Measurement=(name,value)=>{
                        if(!costs.TryGetValue(name,out var values))costs[name]=values=new double[3];
                        values[0]+=value;values[1]++;values[2]=Math.Max(values[2],value);
                    };
                    while(!race.ResultReady&&Time.realtimeSinceStartup<deadline)yield return null;
                    Check(race.ResultReady,"Market baseline "+scenario+" reaches the actual finish line");
                    readyAt=System.Diagnostics.Stopwatch.GetTimestamp();
                    long previous=readyAt;
                    int framesPending=0;
                    while(game.ResultSettlementPending&&Time.realtimeSinceStartup<deadline)
                    {
                        yield return null;
                        long now=System.Diagnostics.Stopwatch.GetTimestamp();
                        frames.Add((now-previous)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                        previous=now;
                        framesPending++;
                        if(resultsShownAt==0&&game.Results!=null&&game.Results.Root!=null&&!game.Results.Root.ClassListContains("hidden"))
                            resultsShownAt=now;
                    }
                    doneAt=System.Diagnostics.Stopwatch.GetTimestamp();
                    Check(!game.ResultSettlementPending&&game.Profile.careerRuns.Last().complete,
                        "Market baseline "+scenario+" settles without an injected writer delay");
                    Check(resultsShownAt!=0,"Market baseline "+scenario+" presents the results screen");
                    Check(game.Profile.vehicleStock.generation==generation+(scenario=="paid third"?1:0),
                        "Market baseline "+scenario+" only refreshes the intended epoch");
                    int expected=scenario=="practice"?0:(race.playerWon?120:40);
                    Check(game.Profile.cash==cash+expected,"Market baseline "+scenario+" pays once");
                    frames.Sort();
                    double maxFrame=frames.Count==0?0:frames[frames.Count-1];
                    double p95=frames.Count==0?0:frames[(int)Math.Ceiling(.95*frames.Count)-1];
                    double resultToCommit=(doneAt-readyAt)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                    double resultsVisible=(resultsShownAt-readyAt)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "MEASURE: market baseline {0} / result-to-commit {1:F3} ms / results-visible {2:F3} ms / pending frames {3} / max frame {4:F3} ms / p95 frame {5:F3} ms / max settlement step {6:F3} ms ({7}) / max camera step {8:F3}",
                        scenario,resultToCommit,resultsVisible,framesPending,maxFrame,p95,maxStep,maxStage,maxCamera));
                    foreach(var entry in wall)
                        report.AppendLine("MEASURE: market baseline "+scenario+" / wall "+entry.Key+" = "+entry.Value.ToString("F3",CultureInfo.InvariantCulture)+" ms");
                    foreach(var entry in costs)
                        report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "MEASURE: market baseline {0} / stage {1} / {2:F3} ms sampled / {3} calls / {4:F3} ms max",
                            scenario,entry.Key,entry.Value[0],(int)entry.Value[1],entry.Value[2]));
                    Check(wall.ContainsKey("finish-to-commit total")&&wall.ContainsKey("save task and completion"),
                        "Market baseline "+scenario+" captured the full finish and save timings");
                }
                finally
                {
                    game.verificationInput=null;
                    PrototypeGame.FinishMeasurement=priorStep;
                    PrototypeGame.FinishWallMeasurement=priorWall;
                    DragLab.Domain.Mechanics.PartsMarket.Measurement=priorMarket;
                }
                // Distinguish responsive shop UI from completion of the expensive preview evaluation.
                game.ReturnToGarage();
                long shopStart=System.Diagnostics.Stopwatch.GetTimestamp();
                game.QueueNativeNavigation("Parts",true);
                float shopDeadline=Time.realtimeSinceStartup+12;
                while((game.ScreenName!="Parts"||shell.Root.Q("partsPage").ClassListContains("hidden"))&&Time.realtimeSinceStartup<shopDeadline)
                    yield return null;
                long shopVisibleAt=System.Diagnostics.Stopwatch.GetTimestamp();
                Check(game.ScreenName=="Parts"&&!shell.Root.Q("partsPage").ClassListContains("hidden"),
                    "Market baseline "+scenario+" shows the shop page");
                while(shell.PartComparisonPending&&Time.realtimeSinceStartup<shopDeadline)yield return null;
                Check(!shell.PartComparisonPending,"Market baseline "+scenario+" completes first shop preview batch");
                long shopReadyAt=System.Diagnostics.Stopwatch.GetTimestamp();
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "MEASURE: market baseline {0} / shop visible {1:F3} ms / comparison ready {2:F3} ms",
                    scenario,(shopVisibleAt-shopStart)*1000.0/System.Diagnostics.Stopwatch.Frequency,
                    (shopReadyAt-shopStart)*1000.0/System.Diagnostics.Stopwatch.Frequency));
                game.ReturnToGarage();
            }
        }
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
                    SqliteStateStore.FailureInjection=stage=>{if(stage=="before-commit"&&race.ResultReady){writerThread=Thread.CurrentThread.ManagedThreadId;entered.Set();if(!release.Wait(8000))throw new IOException("Slow-write fixture timed out");}};
                    try{
                        float deadline=Time.realtimeSinceStartup+75;long previous=System.Diagnostics.Stopwatch.GetTimestamp();
                        while(!entered.IsSet&&Time.realtimeSinceStartup<deadline){
                            if(race.ResultReady){long now=System.Diagnostics.Stopwatch.GetTimestamp();maxFrame=Math.Max(maxFrame,(now-previous)*1000.0/System.Diagnostics.Stopwatch.Frequency);}
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
