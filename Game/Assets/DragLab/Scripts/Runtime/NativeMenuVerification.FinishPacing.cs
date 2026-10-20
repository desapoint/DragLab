using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
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
                var priorSave=ProfileStore.ValidatedSaveMeasurement;
                var saveStages=new SortedDictionary<string,double>(StringComparer.Ordinal);
                float deadline=Time.realtimeSinceStartup+75;
                long readyAt=0,resultsShownAt=0,doneAt=0;
                try
                {
                    // Measuring after StartRace avoids counting setup and initial save as settlement.
                    var authoredBefore=game.Profile.catalog;
                    int catalogRevisionBefore=authoredBefore.AuthoringRevision;
                    game.StartRace(null,scenario=="practice");
                    Check(ReferenceEquals(authoredBefore,game.Profile.catalog),
                        "Race entry reuses authored catalog across wear and checkpoint transactions: "+scenario);
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
                    ProfileStore.ValidatedSaveMeasurement=(name,value)=>saveStages[name]=value;
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
                    Check(ReferenceEquals(authoredBefore,game.Profile.catalog)&&
                        game.Profile.catalog.AuthoringRevision==catalogRevisionBefore,
                        "Wear and settlement preserve the same authored catalog and key revision: "+scenario);
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
                    foreach(var entry in saveStages)
                        report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "MEASURE: market baseline {0} / SQLite save {1} = {2:F3}{3}",
                            scenario,entry.Key,entry.Value,
                            entry.Key.EndsWith("records",StringComparison.Ordinal)||
                            entry.Key.EndsWith("bytes",StringComparison.Ordinal)||
                            entry.Key.EndsWith("characters",StringComparison.Ordinal)?"":" ms"));
                    Check(saveStages.ContainsKey("profile serialization")&&
                        saveStages.ContainsKey("record projection")&&
                        saveStages.ContainsKey("state row delta SQL")&&
                        saveStages.ContainsKey("ownership delta SQL")&&
                        saveStages.ContainsKey("integrity digest")&&
                        saveStages.ContainsKey("durable commit")&&
                        saveStages.ContainsKey("entire save worker")&&
                        saveStages.ContainsKey("changed state records")&&
                        saveStages.ContainsKey("market chunk records")&&
                        saveStages.ContainsKey("changed market chunk records")&&
                        saveStages.ContainsKey("previous state records")&&
                        saveStages.ContainsKey("projected state records")&&
                        saveStages.ContainsKey("inserted state records")&&
                        saveStages.ContainsKey("updated state records")&&
                        saveStages.ContainsKey("removed state records")&&
                        saveStages.ContainsKey("unchanged state records"),
                        "Market baseline "+scenario+" captures detailed SQLite save stages and row counts");
                    Check(saveStages["projected state records"]==
                            saveStages["inserted state records"]+saveStages["updated state records"]+saveStages["unchanged state records"]&&
                        saveStages["previous state records"]==
                            saveStages["removed state records"]+saveStages["updated state records"]+saveStages["unchanged state records"]&&
                        saveStages["changed state records"]==
                            saveStages["removed state records"]+saveStages["inserted state records"]+saveStages["updated state records"],
                        "Market baseline "+scenario+" SQLite delta counts exactly reconcile without hidden writes");
                    if(scenario=="paid third")
                        Check(saveStages["market chunk records"]>0&&
                            saveStages["projected state records"]<15348&&
                            saveStages["changed state records"]<17735,
                            "Third-race market chunks reduce SQLite record projection and changed-row churn below F.3 baseline");
                    Check(wall.ContainsKey("finish-to-commit total")&&wall.ContainsKey("save task and completion"),
                        "Market baseline "+scenario+" captured the full finish and save timings");
                }
                finally
                {
                    game.verificationInput=null;
                    PrototypeGame.FinishMeasurement=priorStep;
                    PrototypeGame.FinishWallMeasurement=priorWall;
                    DragLab.Domain.Mechanics.PartsMarket.Measurement=priorMarket;
                    ProfileStore.ValidatedSaveMeasurement=priorSave;
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
                // Same authoritative normalizer: iterator must produce byte-for-byte
                // identical candidate state before any race or durable write.
                string originalJson=JsonUtility.ToJson(game.Profile);
                var synchronous=JsonUtility.FromJson<PlayerProfile>(originalJson);
                var staged=JsonUtility.FromJson<PlayerProfile>(originalJson);
                var fixtureCatalog=game.Profile.catalog;
                synchronous.storageCommit=staged.storageCommit=game.Profile.storageCommit;
                MechanicsOwnership.Ensure(synchronous,fixtureCatalog);
                int normalizationBoundaries=0;bool namedStages=true;
                foreach(var stage in MechanicsOwnership.EnsureSteps(staged,fixtureCatalog))
                {
                    namedStages&=stage!=null&&stage.StartsWith("settlement/normalize",StringComparison.Ordinal);
                    normalizationBoundaries++;
                }
                Check(namedStages&&normalizationBoundaries>2&&
                    JsonUtility.ToJson(synchronous)==JsonUtility.ToJson(staged)&&
                    originalJson==JsonUtility.ToJson(game.Profile),
                    "Staged normalization preserves exact synchronous profile bytes without mutating source");
                // Exercise validation on its actual detached, worker-owned
                // catalog and compare both accepted and rejected profiles.
                var detachedCatalog=fixtureCatalog.DetachedNewSupplySnapshot();
                // Unity can materialize empty inline objects (such as an absent
                // cart) on every JSON round trip. A real finish candidate is
                // fully normalized before the worker sees it. Model exactly that
                // boundary instead of comparing against an unnormalized re-clone.
                PlayerProfile NormalizedValidationFixture()
                {
                    var copy=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(synchronous));
                    copy.storageCommit=synchronous.storageCommit;
                    MechanicsOwnership.Ensure(copy,fixtureCatalog);
                    return copy;
                }
                var workerProfile=NormalizedValidationFixture();
                var invalidCases=new List<PlayerProfile>();
                var invalidLabels=new List<string>();
                var invalidCash=NormalizedValidationFixture();
                invalidCash.cash=-1;invalidCases.Add(invalidCash);invalidLabels.Add("negative cash");
                var invalidParts=NormalizedValidationFixture();
                if(invalidParts.parts.Count>1)
                {
                    invalidParts.parts[1].instanceId=invalidParts.parts[0].instanceId;
                    invalidCases.Add(invalidParts);invalidLabels.Add("duplicate part identity");
                }
                var invalidMarket=NormalizedValidationFixture();
                invalidMarket.inventory.market.version=-1;
                invalidCases.Add(invalidMarket);invalidLabels.Add("invalid market epoch");
                string expectedValid=MechanicsOwnership.Validate(workerProfile,fixtureCatalog);
                var expectedErrors=new List<string>();
                var initialBytes=new List<string>();
                foreach(var malformed in invalidCases)
                {
                    initialBytes.Add(JsonUtility.ToJson(malformed));
                    expectedErrors.Add(MechanicsOwnership.Validate(malformed,fixtureCatalog));
                    malformed.catalog=detachedCatalog;
                }
                string validBytes=JsonUtility.ToJson(workerProfile);
                workerProfile.catalog=detachedCatalog;
                int verificationMainThread=Thread.CurrentThread.ManagedThreadId,validationFixtureThread=verificationMainThread;
                var validationTask=Task.Run(()=>{
                    validationFixtureThread=Thread.CurrentThread.ManagedThreadId;
                    var results=new List<string>{MechanicsOwnership.Validate(workerProfile,detachedCatalog)};
                    foreach(var malformed in invalidCases)
                        results.Add(MechanicsOwnership.Validate(malformed,detachedCatalog));
                    return results;
                });
                while(!validationTask.IsCompleted)yield return null;
                var validationErrors=validationTask.GetAwaiter().GetResult();
                Check(validationFixtureThread!=verificationMainThread,
                    "Detached validation uses worker thread "+validationFixtureThread+
                    " rather than Unity thread "+verificationMainThread);
                Check(!ReferenceEquals(detachedCatalog,fixtureCatalog),
                    "Detached validation uses an independent authored catalog snapshot");
                Check(validationErrors[0]==expectedValid,
                    "Worker validation matches main-thread result: main="+
                    (expectedValid??"<valid>")+" / worker="+(validationErrors[0]??"<valid>"));
                Check(validBytes==JsonUtility.ToJson(workerProfile),
                    "Worker validation leaves normalized candidate bytes unchanged");
                for(int caseIndex=0;caseIndex<invalidCases.Count;caseIndex++)
                    Check(expectedErrors[caseIndex]!=null&&
                        expectedErrors[caseIndex]==validationErrors[caseIndex+1]&&
                        initialBytes[caseIndex]==JsonUtility.ToJson(invalidCases[caseIndex]),
                        "Worker validator rejects "+invalidLabels[caseIndex]+" with exact error and no mutations");
                int cash=game.Profile.cash,generation=game.Profile.vehicleStock.generation;double maxStep=0,maxCamera=0,maxFrame=0;string maxStage="";
                int normalizationFrames=0;double maxNormalizeStep=0;
                var validationWorkers=new List<int>();
                var priorValidationObserver=PrototypeGame.FinishValidationWorkerThread;
                PrototypeGame.FinishValidationWorkerThread=id=>validationWorkers.Add(id);
                var normalizationStages=new Dictionary<string,double>(StringComparer.Ordinal);
                var authoredBefore=game.Profile.catalog;
                game.StartRace(null,!paid);var race=game.Race;
                Check(ReferenceEquals(authoredBefore,game.Profile.catalog),
                    "Finish fixture retains catalog identity through race entry");
PrototypeGame.FinishMeasurement=(name,value)=>{
                    if(name=="camera step units"){maxCamera=Math.Max(maxCamera,value);return;}
                    if(name.StartsWith("settlement/normalize",StringComparison.Ordinal))
                    {
                        normalizationFrames++;
                        maxNormalizeStep=Math.Max(maxNormalizeStep,value);
                        normalizationStages.TryGetValue(name,out var previousMax);
                        normalizationStages[name]=Math.Max(previousMax,value);
                    }
                    if(value>maxStep){maxStep=value;maxStage=name;}
                };
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
                        report.AppendLine("MEASURE: finish "+(paid?"paid refresh":"practice")+
                            " / max main step ms "+maxStep.ToString("F3")+
                            " / max stage "+maxStage+
                            " / max frame ms "+maxFrame.ToString("F3")+
                            " / max camera step "+maxCamera.ToString("F3")+
                            " / normalize frames "+normalizationFrames+
                            " / max normalize step ms "+maxNormalizeStep.ToString("F3")+
                            " / held-write frames "+(Time.frameCount-frames));
                        foreach(var slow in normalizationStages.OrderByDescending(x=>x.Value).Take(5))
                            report.AppendLine("MEASURE: finish "+(paid?"paid refresh":"practice")+
                                " / normalization stage "+slow.Key+
                                " / max ms "+slow.Value.ToString("F3"));
                        Check(normalizationFrames>0&&maxNormalizeStep>0,
                            "Full settlement normalizer progresses on observable paced frames");
                        Check(validationWorkers.Count==2&&validationWorkers.All(id=>id!=verificationMainThread),
                            "Both settlement integrity passes finish on background threads");
                        Check(maxStep<50&&maxCamera<.35,"Finish preparation is split into bounded main steps and camera motion remains continuous");
                    }finally{release.Set();SqliteStateStore.FailureInjection=null;game.verificationInput=null;
                        PrototypeGame.FinishMeasurement=null;
                        PrototypeGame.FinishValidationWorkerThread=priorValidationObserver;}
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
                Check(reloaded.cash==game.Profile.cash&&reloaded.runCheckpoints.All(r=>r.settled),"Background settlement and final shutdown resources survive durable exit/reload");
                if(paid)
                    Check(JsonUtility.ToJson(reloaded.inventory.market)==JsonUtility.ToJson(game.Profile.inventory.market)&&
                        JsonUtility.ToJson(reloaded.vehicleStock)==JsonUtility.ToJson(game.Profile.vehicleStock)&&
                        reloaded.inventory.dealer.Select(x=>JsonUtility.ToJson(x)).SequenceEqual(
                            game.Profile.inventory.dealer.Select(x=>JsonUtility.ToJson(x))),
                        "Committed third-race market offers and dealer hardware survive exact SQLite reload");
                entered.Dispose();release.Dispose();
            }
        }
#endif
    }
}
