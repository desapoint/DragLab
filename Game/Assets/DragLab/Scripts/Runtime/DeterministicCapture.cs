using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal static class DeterministicCapture
    {
        internal static bool Active => RuntimeEntropy.Capture;
        internal static bool Frame { get; private set; }
        internal static string Name { get; private set; }
        internal static bool Shine => Frame && Name.Contains("shine");
        internal static bool Transition => Frame && Name.Contains("switch-transition");
        static readonly Dictionary<string,int> occurrences=new Dictionary<string,int>(StringComparer.Ordinal);
        internal static IEnumerator Screenshot(string folder,string name)
        {
            if(!Active){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));yield break;}
            string key=folder+"/"+name;occurrences.TryGetValue(key,out int occurrence);occurrences[key]=++occurrence;
            string fileName=occurrence==1?name:name+"-repeat-"+occurrence;
            var game=UnityEngine.Object.FindFirstObjectByType<PrototypeGame>();
            var shell=UnityEngine.Object.FindFirstObjectByType<NativeMenuShell>();
            float deadline=Time.realtimeSinceStartup+45;
            int quiet=0;
            while(quiet<6){
                game?.CompleteCaptureInspection();shell?.Refresh();
                bool pending=VehicleAssessmentCache.Pending||(game!=null&&game.CaptureInspectionPending)||(shell!=null&&shell.CaptureTasksPending);
                quiet=pending?0:quiet+1;
                if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Capture did not reach completed estimates: "+name);
                yield return null;
            }
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            if(!name.Contains("hover")&&!name.Contains("shine"))using(var input=new WindowsKeyboardProbe())input.MoveMouseAt(Screen.width-2,Screen.height-2);
#endif
            Name=name;Frame=true;
            Action restoreRace=null;
            try {
                restoreRace=game?.BeginCaptureRace(name);
                UnityEngine.Object.FindFirstObjectByType<EngineTestScene>()?.PrepareRenderCapture(name);
                foreach(var document in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)){
                    foreach(var surface in document.rootVisualElement.Query<RacingSurface>().ToList())surface.MarkDirtyRepaint();
                    foreach(var surface in document.rootVisualElement.Query<SwitchSurface>().ToList())surface.MarkDirtyRepaint();
                }
                for(int i=0;i<4;i++)yield return null;
                yield return new WaitForEndOfFrame();
                var labels=UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
                    .SelectMany(d=>d.rootVisualElement.Query<Label>().ToList())
                    .Where(label=>label.visible&&label.worldBound.width>0&&label.worldBound.height>0)
                    .Select(label=>label.name+"="+label.text).OrderBy(text=>text,StringComparer.Ordinal).ToArray();
                File.AppendAllText(Path.Combine(folder,"capture-inputs.jsonl"),JsonUtility.ToJson(new Inputs{name=fileName,width=Screen.width,height=Screen.height,labels=labels})+"\n",new System.Text.UTF8Encoding(false));
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,fileName+".png"));
                yield return null;
            }finally {restoreRace?.Invoke();Frame=false;Name=null;}
        }
        [Serializable] sealed class Inputs {public string name;public int width,height;public string[] labels;}
    }

    public sealed partial class PrototypeGame
    {
        internal bool CaptureInspectionPending => inspectionTask!=null&&!inspectionTask.IsCompleted;
        internal void CompleteCaptureInspection(){if(Build!=null&&!string.IsNullOrEmpty(Build.engineAssemblyId)&&savedStats!=null&&(screen=="Parts"||screen=="Tuning"||screen=="Stats"))RefreshInspection();}
        internal Action BeginCaptureRace(string name)
        {
            if(race==null||!new[]{"race-gauges-driving","race-road-72kmh","race-road-126kmh","race-trapezoid-finish-approach","race-gauges-results"}.Contains(name))return null;
            var previous=race;var previousPass=finishedPass;string previousNotice=notice;
            // Fresh copied stock fixture, shared physics and exact 120 Hz input.
            // The real test race, resources and results are restored before input resumes.
            var driver=new DriverSettings{shifting=ShiftMode.Automatic,launch=LaunchMode.Automatic,wearMode=WearMode.None,warmupBeforeRace=false,throttleAssist=false};
            var sampled=RaceSessionFactory.Create(Selected.spec,Selected.CreateStockTune(),driver,Selected.spec,Selected.CreateStockTune(),driver,402.336);
            for(int tick=0;tick<120*90;tick++){
                sampled.Step(new DriverInput{throttle=1});
                if(name=="race-gauges-driving"&&tick>=120*6)break;
                if(name=="race-road-72kmh"&&sampled.player.state.speed>=20)break;
                if(name=="race-road-126kmh"&&sampled.player.state.speed>=35)break;
                if(name=="race-trapezoid-finish-approach"&&sampled.player.state.distance>=399)break;
                if(sampled.phase==RacePhase.Finished)break;
            }
            if(name=="race-gauges-results")for(int tick=0;tick<240;tick++)sampled.Shutdown(new DriverInput());
            playerView.ClearSlipEffects();aiView.ClearSlipEffects();
            race=sampled;finishedPass=null;notice="Fixed-tick verification replay";RenderRace();RaceHud?.Refresh();
            return ()=>{race=previous;finishedPass=previousPass;notice=previousNotice;RenderRace();RaceHud?.Refresh();};
        }
    }
    internal sealed partial class NativeMenuShell
    {
        internal bool CaptureTasksPending => (ownedRatings!=null&&!ownedRatings.IsCompleted)||(dealerRatings!=null&&!dealerRatings.IsCompleted)
            ||carInspections.Values.Any(c=>c?.Driving!=null&&!c.Driving.IsCompleted)
            ||game.ScreenName=="Parts"&&!ModalOpen&&(PartBrowserPreparationPending||partsDetailId==null&&PartComparisonPending);
    }
}
