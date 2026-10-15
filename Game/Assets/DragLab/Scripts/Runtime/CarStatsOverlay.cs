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
        CarInspection savedStats, proposedStats, healthyStats;
        internal CarInspection HealthyInspection => healthyStats;
        internal DragLab.Presentation.LayeredCarView CarViewForVerification { get { return playerView; } }
        internal CarInspection SavedInspection { get { return savedStats; } }
        internal CarInspection ProposedInspection { get { return proposedStats; } }
        internal void ScrollInspectionTo(float y) { statsView.SetScrollForInspection(y); }
        internal System.Collections.Generic.List<string> StatsSummaryForVerification { get { return statsView.SummaryForVerification; } }
        internal Rect StatsSummaryRect { get { return statsView.SummaryRect; } }
        internal void PreviewPart(string definitionId = null, string instanceId = null, PartCategory? remove = null)
        { previewCart=false; previewDefinitionId = definitionId; previewInstanceId = instanceId; previewRemoval = remove; statsKey = null; }
        [Serializable] sealed class CartInspectionKey {public System.Collections.Generic.List<DragLab.Domain.Mechanics.PartCartLine> lines;}
        Task<CarInspection[]> inspectionTask;
        string inspectionTaskKey, inspectionTaskBaseKey, inspectionBaseKey;
        InspectionBuild inspectionPendingSource;
        float inspectionChangedAt;
        int inspectionFrame=-1;
        internal bool InspectionBusy => inspectionTask != null || savedStats != null && !savedStats.probeComplete || proposedStats != null && !proposedStats.probeComplete || healthyStats != null && !healthyStats.probeComplete;
        internal void RefreshInspection()
        {
            if(inspectionFrame==Time.frameCount)return; inspectionFrame=Time.frameCount;
            string baseKey = profile.revision + "/" + Build.instanceId + "/" + JsonUtility.ToJson(Build.tune) + "/" + JsonUtility.ToJson(Build.installed) + "/" + JsonUtility.ToJson(Build.fuel) + "/" + JsonUtility.ToJson(Build.fluids) + "/" + string.Join("/", profile.parts.Where(p => p.ownerId == Build.instanceId || p.ownerId == Build.engineAssemblyId).Select(p => JsonUtility.ToJson(p.condition)));
            string key=baseKey+"/"+(previewCart?JsonUtility.ToJson(new CartInspectionKey {lines=cart}):"")+"/"+screen+"/"+previewDefinitionId+"/"+previewInstanceId+"/"+previewRemoval+"/"+(draft==null?"":JsonUtility.ToJson(draft.values)+"/"+draft.paint);
            if(inspectionTask!=null && inspectionTask.IsCompleted) {
                try {
                    var result=inspectionTask.GetAwaiter().GetResult();
                    if(inspectionTaskBaseKey==baseKey){savedStats=result[0];healthyStats=result[2];}
                    if(inspectionTaskKey==key)proposedStats=result[1];
                } catch(Exception e) { notice="Inspection probe failed: "+e.Message; }
                inspectionTask=null;
            }
            if(statsKey!=key) {
                if(screen=="Parts"&&previewCart)RefreshCartPrice();
                statsKey=key;inspectionChangedAt=Time.realtimeSinceStartup;
                if(inspectionBaseKey!=baseKey || savedStats==null) {
                    inspectionBaseKey=baseKey;
                    var capture=InspectionBuild.Capture(profile,Build);
                    savedStats=CarInspection.CalculatePrepared(capture,CarInspection.PrepareCar(capture),false);
                    healthyStats=null;
                    if(capture.components.Any(p=>p.condition.health<.9999f||p.condition.failed||p.condition.deposits>.001||p.condition.leakSeverity>.001||p.condition.adjustmentError>.001||p.condition.clutchOilCondition<.9999||p.condition.elements.Any(e=>e.health<.9999f||e.fault!=ElementFault.None)||p.condition.tires.Any(t=>TireDynamics.RemainingHealth(t)<.9999f||t.Failed))) {
                        var healthy=InspectionBuild.Capture(profile,Build,healthy:true);
                        healthyStats=CarInspection.CalculatePrepared(healthy,CarInspection.PrepareCar(healthy),false);
                    }
                }
                inspectionPendingSource=null;proposedStats=null;
                if(screen=="Parts"&&previewCart&&cartError==null&&cart.Any(l=>l.install||l.action==CartAction.Move))inspectionPendingSource=InspectionBuild.Capture(profile,Build,cart:cart);
                else if(screen=="Tuning"&&draft!=null&&DraftDirty)inspectionPendingSource=InspectionBuild.Capture(profile,Build,draft.values);
                else if(screen=="Parts"&&(previewDefinitionId!=null||previewInstanceId!=null||previewRemoval.HasValue))inspectionPendingSource=InspectionBuild.Capture(profile,Build,definitionId:previewDefinitionId,instanceId:previewInstanceId,remove:previewRemoval);
                if(inspectionPendingSource!=null)proposedStats=CarInspection.CalculatePrepared(inspectionPendingSource,CarInspection.PrepareCar(inspectionPendingSource),false);
            }
            if(inspectionTask==null&&Time.realtimeSinceStartup-inspectionChangedAt>=.12f&&(!savedStats.probeComplete||proposedStats!=null&&!proposedStats.probeComplete||healthyStats!=null&&!healthyStats.probeComplete)) {
                var saved=savedStats;var proposed=proposedStats;var healthy=healthyStats;
                var savedCar=saved.probeComplete?null:CarInspection.PrepareCar(saved.source);
                var proposedCar=proposed==null?null:CarInspection.PrepareCar(proposed.source);
                var healthyCar=healthy==null||healthy.probeComplete?null:CarInspection.PrepareCar(healthy.source);
                inspectionTaskKey=key;inspectionTaskBaseKey=baseKey;
                inspectionTask=Task.Run(()=>new[]{saved.probeComplete?saved:CarInspection.CalculatePrepared(saved.source,savedCar),proposed==null?null:CarInspection.CalculatePrepared(proposed.source,proposedCar),healthy==null?null:healthy.probeComplete?healthy:CarInspection.CalculatePrepared(healthy.source,healthyCar)});
            }
            var visible = (proposedStats ?? savedStats).source.build;
            playerView.ApplySetup(visible.spec, visible.tune);
            Color paint; string paintHex = screen == "Tuning" && draft != null ? draft.paint : Build.paintHex;
            if (ColorUtility.TryParseHtmlString(paintHex, out paint)) playerView.SetPaint(paint);
        }
        void DrawCarStats(float width, float height)
        {
            RefreshInspection();

            float left = panelWidth + 30;
            if (width - left < 260) return;
            string title = screen == "Tuning" ? "CAR STATS / " + (DraftDirty ? "DRAFT TUNE" : "saved tune") : screen == "Parts" && proposedStats != null ? (previewCart?"CART PREVIEW / current → combined installs":"PART PREVIEW / saved → proposed") : "FULL CAR STATS / " + Selected.spec.displayName;
            string slot = null;
            if (screen == "Parts" && !previewCart) {
                var item = previewInstanceId == null ? null : profile.parts.Find(p => p.instanceId == previewInstanceId);
                var definition = profile.catalog.Find(item == null ? previewDefinitionId : item.definitionId);
                slot = previewRemoval.HasValue ? previewRemoval.Value.ToString() : definition != null ? definition.category.ToString() : partCategory.ToString();
            }
            statsView.Draw(new Rect(left, 150, width - left - 16, height - 166), savedStats, proposedStats, title, slot, healthyStats);
        }
        void OnDestroy() { race?.CloseOpponentWorker(); statsView.Dispose(); }
    }
}
