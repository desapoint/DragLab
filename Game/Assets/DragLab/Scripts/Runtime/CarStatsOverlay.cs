using System.Linq;
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
        internal void RefreshInspection()
        {
            string key = profile.revision + "/" + Build.instanceId + "/" + JsonUtility.ToJson(Build.tune) + "/" + JsonUtility.ToJson(Build.installed) + "/" + string.Join("/", profile.parts.Where(p => p.ownerId == Build.instanceId || p.ownerId == Build.engineAssemblyId).Select(p => JsonUtility.ToJson(p.condition))) + "/" + previewDefinitionId + "/" + previewInstanceId + "/" + previewRemoval + "/" + (draft == null ? "" : JsonUtility.ToJson(draft.values) + "/" + draft.paint);
            if (statsKey == key) return; statsKey = key;
            savedStats = CarInspection.Calculate(InspectionBuild.Capture(profile, Build)); proposedStats = null;
            if (screen == "Tuning" && draft != null && DraftDirty) proposedStats = CarInspection.Calculate(InspectionBuild.Capture(profile, Build, draft.values));
            else if (screen == "Parts" && (previewDefinitionId != null || previewInstanceId != null || previewRemoval.HasValue)) proposedStats = CarInspection.Calculate(InspectionBuild.Capture(profile, Build, definitionId: previewDefinitionId, instanceId: previewInstanceId, remove: previewRemoval));
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
            string title = screen == "Tuning" ? "CAR STATS / " + (DraftDirty ? "DRAFT TUNE" : "saved tune") : screen == "Parts" && proposedStats != null ? "PART PREVIEW / saved → proposed" : "FULL CAR STATS / " + Selected.spec.displayName;
            string slot = null;
            if (screen == "Parts") {
                var item = previewInstanceId == null ? null : profile.parts.Find(p => p.instanceId == previewInstanceId);
                var definition = profile.catalog.Find(item == null ? previewDefinitionId : item.definitionId);
                slot = previewRemoval.HasValue ? previewRemoval.Value.ToString() : definition != null ? definition.category.ToString() : partCategory.ToString();
            }
            statsView.Draw(new Rect(left, 150, width - left - 16, height - 166), savedStats, proposedStats, title, slot);
        }
        void OnDestroy() { statsView.Dispose(); }
    }
}
