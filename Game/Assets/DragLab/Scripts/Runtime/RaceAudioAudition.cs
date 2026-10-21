using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        internal static bool AudioAuditionRequested => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-audio-race-test")>=0;
        string[] auditionFiles;
        RaceAudioTestFixture audition;
        RaceAudioCutCounts auditionCuts;
        int auditionStopCheck;float auditionStopWait;
        int auditionIndex=-1,auditionTick,auditionLimiter,auditionShifts,auditionGear,auditionMode;
        bool auditionStopped,auditionDone,auditionVerify;
        string auditionStage="",auditionFolder;
        float auditionGap;
        double auditionEnginePeak,auditionTirePeak;
        readonly StringBuilder auditionReport=new StringBuilder("Visible game / live production audio / input replay verification\n");
        internal bool AudioAuditionActive=>auditionFiles!=null;
        internal string AudioAuditionTitle=>audition==null?"":(auditionIndex+1)+" / "+auditionFiles.Length+" · "+audition.title+" · "+audition.configuration;
        internal string AudioAuditionStage=>auditionDone?"Complete · Restart or close the game":auditionStopped?"Stopped · Resume or Next":auditionStage;
        internal int AudioAuditionMode=>auditionMode;
        internal bool AudioAuditionStopped=>auditionStopped||auditionDone;
        internal bool AuditionEngineActive=>!AudioAuditionActive||(!auditionStopped&&auditionMode!=2);
        internal bool AuditionTiresActive=>!AudioAuditionActive||(!auditionStopped&&auditionMode!=1);
        void BeginAudioAudition()
        {
            if(!AudioAuditionRequested)return;
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--draglab-audio-race-test"),isolated=Array.IndexOf(args,"--draglab-verify");
            if(!RuntimeVerification.Active||isolated+1>=args.Length||at+1>=args.Length)throw new ArgumentException("Audio race audition requires explicit fixtures and an isolated --draglab-verify directory.");
            auditionFolder=Path.GetFullPath(args[isolated+1]);Directory.CreateDirectory(auditionFolder);
            auditionFiles=Directory.GetFiles(Path.GetFullPath(args[at+1]),"*.race.json");Array.Sort(auditionFiles,StringComparer.Ordinal);
            if(auditionFiles.Length==0)throw new InvalidOperationException("Generate the race audio input fixtures first.");
            auditionVerify=Array.IndexOf(args,"--draglab-audio-test-check")>=0;
            Application.runInBackground=true;NextAudioAudition();
        }
        internal void SelectAudioAuditionMode(int mode){auditionMode=Math.Max(0,Math.Min(2,mode));}
        internal void StopAudioAudition()
        {
            if(auditionDone){auditionIndex=-1;NextAudioAudition();return;}
            auditionStopped=!auditionStopped;accumulator=0;
            if(auditionStopped){engineAudio.Silence();opponentAudio.Silence();}
        }
        internal void NextAudioAudition()
        {
            if(!AudioAuditionActive)return;
            race?.CloseOpponentWorker();engineAudio.Silence();opponentAudio.Silence();
            if(++auditionIndex>=auditionFiles.Length){auditionIndex=auditionFiles.Length-1;auditionDone=true;auditionStopped=true;return;}
            audition=JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(auditionFiles[auditionIndex],Encoding.UTF8));
            if(audition==null||audition.ticks==null||audition.ticks.Count==0)throw new InvalidDataException("Empty race audio fixture.");
            race=audition.CreateRace();
            // Test cars never become owned assets or enter settlement/checkpoint paths.
            screen="Race";liveRunClosed=true;awarded=true;practiceRun=true;paused=audioSettingsOpen=false;controls.Clear();
            auditionTick=auditionLimiter=auditionShifts=auditionGear=auditionStopCheck=0;auditionCuts=new RaceAudioCutCounts();auditionEnginePeak=auditionTirePeak=0;
            auditionStopped=auditionDone=false;auditionStage="Idle";accumulator=0;auditionGap=0;
            auditionMode=audition.tireTest?2:1;
            playerView.gameObject.SetActive(true);aiView.gameObject.SetActive(true);
            playerView.Build(race.player.spec,new Color(.25f,.7f,.7f),1000);aiView.Build(race.opponent.spec,new Color(.7f,.3f,.25f),0);
            playerView.ApplySetup(race.player.spec,race.player.tune);aiView.ApplySetup(race.opponent.spec,race.opponent.tune);
            raceCamera.orthographicSize=MenuCameraSize/RaceSceneZoom;raceCamera.transform.position=new Vector3(2,RaceCameraY,-10);
            engineAudio.Configure(race.player.spec,race.player.tune);opponentAudio.Configure(race.opponent.spec,race.opponent.tune);
            InterfaceComparison.SetReplacement("Race",true);PlaceRaceCars();RenderRace();
            WriteAudioAuditionProgress();Debug.Log("LIVE AUDIO: "+AudioAuditionTitle);
        }
        void WriteAudioAuditionProgress()
        {
            File.WriteAllText(Path.Combine(auditionFolder,"listening-status.txt"),AudioAuditionTitle+"\n"+AudioAuditionStage+"\nTick "+auditionTick+" / "+audition.ticks.Count+"\nEngine RMS "+engineAudio.OutputRms+" / tires "+engineAudio.TireOutputRms+" / rival "+opponentAudio.OutputRms+"\n",new UTF8Encoding(false));
        }
        void AdvanceAudioAudition()
        {
            try{
                if(Input.GetKeyDown(KeyCode.Escape)){if(NativeRacePause)nativeMenu.EscapeRacePause();else SetPaused(!paused);}
                if(paused)return;
                if(auditionVerify&&auditionTick==120&&auditionStopCheck==0){if(!RaceHud.AudioAuditionControlsFit())throw new InvalidOperationException("Audition controls overlap or are clipped.");ScreenCapture.CaptureScreenshot(Path.Combine(auditionFolder,audition.id+".png"));StopAudioAudition();auditionStopCheck=1;auditionStopWait=0;}
                if(auditionStopCheck==1){
                    auditionStopWait+=Time.unscaledDeltaTime;if(auditionStopWait<.2f)return;
                    if(auditionTick!=120||engineAudio.OutputRms!=0||engineAudio.TireOutputRms!=0)throw new InvalidOperationException("Stop failed before finish.");
                    auditionReport.AppendLine("PASS stop/resume before finish / "+audition.id);StopAudioAudition();auditionStopCheck=2;
                }
                if(auditionDone||auditionStopped){engineAudio.Silence();opponentAudio.Silence();return;}
                if(auditionVerify&&auditionTick>120&&((auditionMode==1&&engineAudio.TireOutputRms!=0)||(auditionMode==2&&engineAudio.OutputRms!=0)))throw new InvalidOperationException("Category solo leaked a voice.");
                auditionEnginePeak=Math.Max(auditionEnginePeak,engineAudio.OutputRms);auditionTirePeak=Math.Max(auditionTirePeak,engineAudio.TireOutputRms);
                if(opponentAudio.OutputRms>1e-8||opponentAudio.TireOutputRms>1e-8)throw new InvalidOperationException("Rival voice leaked into audition.");
                if(auditionTick==audition.ticks.Count){
                    auditionGap+=Time.unscaledDeltaTime;
                    RenderRace();
                    if(auditionGap<.4f)return;
                    var state=race.player.state;
                    if(JsonUtility.ToJson(auditionCuts)!=JsonUtility.ToJson(audition.cuts)||state.exhaustPopCount!=audition.pops)throw new InvalidOperationException("Live limiter cut/backfire telemetry diverged: "+audition.id);
                    if(!state.finished||Math.Abs(state.elapsed-audition.elapsed)>.00001||auditionLimiter!=audition.limiterTicks||auditionShifts!=audition.shifts||state.boostReleaseCount!=audition.releases)
                        throw new InvalidOperationException("Live race diverged from validated input fixture: "+audition.id+" / ET "+state.elapsed+" vs "+audition.elapsed+" / limiter "+auditionLimiter+" / shifts "+auditionShifts+" / releases "+state.boostReleaseCount);
                    if(auditionVerify&&(audition.tireTest?auditionTirePeak:auditionEnginePeak)<1e-6)throw new InvalidOperationException("Live AudioSource produced no audible category output: "+audition.id);
                    if(auditionVerify&&audition.tireTest&&engineAudio.TireOutputChannels!=2)throw new InvalidOperationException("Tire AudioSource lost stereo wheel placement.");
                    if(engineAudio.AfterfireOverflow!=0||engineAudio.AfterfireTailSteals!=0)throw new InvalidOperationException("Audio event/tail budget exceeded: "+audition.id+" / overflow "+engineAudio.AfterfireOverflow+" / steals "+engineAudio.AfterfireTailSteals);
                    if(auditionVerify&&engineAudio.AfterfireDelivered!=engineAudio.ChemicalEvents)throw new InvalidOperationException("Live afterfire history not fully rendered: "+audition.id+" / "+engineAudio.AfterfireDelivered+" of "+engineAudio.ChemicalEvents);
                    auditionReport.AppendLine("PASS "+audition.id+" / exact ET, shifts, limiter, releases / engine RMS "+auditionEnginePeak+" / tire RMS "+auditionTirePeak+" / fuel/ignition cuts "+auditionCuts.fuel+"/"+auditionCuts.ignition+" / launch cuts "+auditionCuts.launchFuel+"/"+auditionCuts.launchIgnition+" / legacy requests "+state.exhaustPopCount+" / chemical "+engineAudio.ChemicalEvents+" / delivered so far "+engineAudio.AfterfireDelivered+" / tire channels "+engineAudio.TireOutputChannels+" / rival silent");
                    File.WriteAllText(Path.Combine(auditionFolder,"live-verification.txt"),auditionReport.ToString(),new UTF8Encoding(false));
                    if(auditionVerify&&auditionIndex==auditionFiles.Length-1){auditionReport.AppendLine("PASS all "+auditionFiles.Length+" game races");File.WriteAllText(Path.Combine(auditionFolder,"live-verification.txt"),auditionReport.ToString(),new UTF8Encoding(false));Application.Quit(0);auditionStopped=true;return;}
                    NextAudioAudition();return;
                }
                // Live AudioSources consume PCM at wall-clock speed. Accelerating
                // only physics makes timestamped events inaudible and overflows history.
                // Offline production-DSP replay supplies the accelerated coverage.
                accumulator+=Math.Min(Time.unscaledDeltaTime,.1f);
                while(accumulator>=DragMath.FixedStep&&auditionTick<audition.ticks.Count){
                    var tick=audition.ticks[auditionTick++];if(!string.IsNullOrEmpty(tick.stage)){auditionStage=tick.stage;WriteAudioAuditionProgress();}
                    race.player.state.warmupLineLock=tick.lineLock;
                    if(tick.beginCountdown&&!race.BeginCountdown())throw new InvalidOperationException("Cannot stage live fixture "+audition.id);
                    if(race.phase==RacePhase.Finished)race.Shutdown(tick.input);else race.Step(tick.input);
                    controls.BeginFrame(tick.input.throttle,tick.input.clutch,tick.input.brake);
                    auditionCuts.Observe(race.player);
                    if(race.player.state.limiter)auditionLimiter++;
                    if(race.player.state.launched&&race.player.state.gear!=auditionGear){if(auditionGear>0)auditionShifts++;auditionGear=race.player.state.gear;}
                    accumulator-=DragMath.FixedStep;
                    if(auditionVerify&&auditionTick==120&&auditionStopCheck==0)break;
                }
                RenderRace();
            }catch(Exception error){auditionStopped=true;engineAudio.Silence();opponentAudio.Silence();File.WriteAllText(Path.Combine(auditionFolder,"live-verification.txt"),auditionReport+"FAIL "+error,new UTF8Encoding(false));Debug.LogException(error);if(auditionVerify)Application.Quit(1);}
        }
    }
}
