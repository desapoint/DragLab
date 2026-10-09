using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        readonly CarStatsView statsView = new CarStatsView();
        string previewDefinitionId, previewInstanceId, statsKey;
        PartCategory? previewRemoval;
        CarInspection savedStats, proposedStats;
        internal DragLab.Presentation.LayeredCarView CarViewForVerification { get { return playerView; } }
        internal CarInspection SavedInspection { get { return savedStats; } }
        internal CarInspection ProposedInspection { get { return proposedStats; } }
        internal void ScrollInspectionTo(float y) { statsView.SetScrollForInspection(y); }
        internal System.Collections.Generic.List<string> StatsSummaryForVerification { get { return statsView.SummaryForVerification; } }
        internal Rect StatsSummaryRect { get { return statsView.SummaryRect; } }
        internal void PreviewPart(string definitionId = null, string instanceId = null, PartCategory? remove = null)
        { previewDefinitionId = definitionId; previewInstanceId = instanceId; previewRemoval = remove; statsKey = null; }
        Task<CarInspection[]> inspectionTask;
        string inspectionTaskKey, inspectionTaskBaseKey, inspectionBaseKey;
        InspectionBuild inspectionPendingSource;
        float inspectionChangedAt;
        int inspectionFrame=-1;
        internal bool InspectionBusy => inspectionTask != null || savedStats != null && !savedStats.probeComplete || proposedStats != null && !proposedStats.probeComplete;
        internal void RefreshInspection()
        {
            if(inspectionFrame==Time.frameCount)return; inspectionFrame=Time.frameCount;
            string baseKey = profile.revision + "/" + Build.instanceId + "/" + JsonUtility.ToJson(Build.tune) + "/" + JsonUtility.ToJson(Build.installed) + "/" + JsonUtility.ToJson(Build.fuel) + "/" + JsonUtility.ToJson(Build.fluids) + "/" + string.Join("/", profile.parts.Where(p => p.ownerId == Build.instanceId || p.ownerId == Build.engineAssemblyId).Select(p => JsonUtility.ToJson(p.condition)));
            string key=baseKey+"/"+screen+"/"+previewDefinitionId+"/"+previewInstanceId+"/"+previewRemoval+"/"+(draft==null?"":JsonUtility.ToJson(draft.values)+"/"+draft.paint);
            if(inspectionTask!=null && inspectionTask.IsCompleted) {
                try {
                    var result=inspectionTask.GetAwaiter().GetResult();
                    if(inspectionTaskBaseKey==baseKey)savedStats=result[0];
                    if(inspectionTaskKey==key)proposedStats=result[1];
                } catch(Exception e) { notice="Inspection probe failed: "+e.Message; }
                inspectionTask=null;
            }
            if(statsKey!=key) {
                statsKey=key;inspectionChangedAt=Time.realtimeSinceStartup;
                if(inspectionBaseKey!=baseKey || savedStats==null) {
                    inspectionBaseKey=baseKey;
                    var capture=InspectionBuild.Capture(profile,Build);
                    savedStats=CarInspection.CalculatePrepared(capture,CarInspection.PrepareCar(capture),false);
                }
                inspectionPendingSource=null;proposedStats=null;
                if(screen=="Tuning"&&draft!=null&&DraftDirty)inspectionPendingSource=InspectionBuild.Capture(profile,Build,draft.values);
                else if(screen=="Parts"&&(previewDefinitionId!=null||previewInstanceId!=null||previewRemoval.HasValue))inspectionPendingSource=InspectionBuild.Capture(profile,Build,definitionId:previewDefinitionId,instanceId:previewInstanceId,remove:previewRemoval);
                if(inspectionPendingSource!=null)proposedStats=CarInspection.CalculatePrepared(inspectionPendingSource,CarInspection.PrepareCar(inspectionPendingSource),false);
            }
            if(inspectionTask==null&&Time.realtimeSinceStartup-inspectionChangedAt>=.12f&&(!savedStats.probeComplete||proposedStats!=null&&!proposedStats.probeComplete)) {
                var saved=savedStats;var proposed=proposedStats;
                var savedCar=saved.probeComplete?null:CarInspection.PrepareCar(saved.source);
                var proposedCar=proposed==null?null:CarInspection.PrepareCar(proposed.source);
                inspectionTaskKey=key;inspectionTaskBaseKey=baseKey;
                inspectionTask=Task.Run(()=>new[]{saved.probeComplete?saved:CarInspection.CalculatePrepared(saved.source,savedCar),proposed==null?null:CarInspection.CalculatePrepared(proposed.source,proposedCar)});
            }
            var visible = (proposedStats ?? savedStats).source.build;
            playerView.ApplySetup(visible.spec, visible.tune);
            Color paint; string paintHex = screen == "Tuning" && draft != null ? draft.paint : Build.paintHex;
            if (ColorUtility.TryParseHtmlString(paintHex, out paint)) playerView.SetPaint(paint);
        }
        void DrawCarStats(float width, float height)
        {
            RefreshInspection();
            if(InspectionBusy) notice="Dyno updated; copied-car performance probe pending. Preview never saves.";
            float left = panelWidth + 30;
            if (width - left < 260) return;
            string title = screen == "Tuning" ? "CAR STATS / " + (DraftDirty ? "DRAFT TUNE" : "saved tune") : screen == "Parts" && proposedStats != null ? "PART PREVIEW / saved → proposed" : "FULL CAR STATS / " + Selected.spec.displayName;
            string slot = null;
            if (screen == "Parts") {
                var item = previewInstanceId == null ? null : profile.parts.Find(p => p.instanceId == previewInstanceId);
                var definition = profile.catalog.Find(item == null ? previewDefinitionId : item.definitionId);
                slot = previewRemoval.HasValue ? previewRemoval.Value.ToString() : definition != null ? definition.category.ToString() : partCategory.ToString();
            }
            statsView.Draw(new Rect(left, 150, width - left - 16, height - 166), savedStats, proposedStats, title, slot);
        }
        void OnDestroy() { race?.CloseOpponentWorker(); statsView.Dispose(); }
    }
}
