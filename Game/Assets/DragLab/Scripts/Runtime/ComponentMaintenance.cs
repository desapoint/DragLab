using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        bool repairsOnly, includeWornRepairs = true;
        internal static bool PartFailed(PartInstance part) { return part.condition.failed || part.condition.health <= 0; }
        internal List<PartInstance> ServiceParts()
        {
            var ids = new HashSet<string>(Build.installed.slots.Select(s => s.partInstanceId));
            return profile.parts.Where(p => ids.Contains(p.instanceId) && (PartFailed(p) || p.condition.health <= .5f || p.condition.deposits >= .05 || p.condition.leakSeverity >= .01 || p.condition.adjustmentError >= .05 || EngineElements.NeedsService(p.condition)))
                .OrderByDescending(PartFailed).ThenBy(p => p.condition.health).ThenBy(p => p.slot).ToList();
        }
        internal void ShowRepairs() { DiscardPreviews(); repairsOnly = true; screen = "Parts"; scroll = Vector2.zero; partRects.Clear(); }
        void MaintenanceSummary()
        {
            var service = ServiceParts(); int failed = service.Count(PartFailed), worn = service.Count - failed;
            var color = GUI.contentColor; GUI.contentColor = failed > 0 ? new Color(1, .4f, .35f) : worn > 0 ? new Color(1, .78f, .35f) : new Color(.45f, .85f, .65f);
            GUILayout.Label("COMPONENT CONDITION / " + failed + " FAILED / " + worn + " worn / service faults", stat);
            GUI.contentColor = color;
            if (service.Count > 0 && PartButton("repairs-open", "Review failures / replace components")) pendingAction = ShowRepairs;
        }
        PartInstance CompatibleSpare(PartInstance old)
        {
            var definition = profile.catalog.Find(old.definitionId);
            return profile.parts.Where(p => p.location == PartLocation.Inventory && !PartFailed(p) &&
                profile.catalog.Find(p.definitionId).category == definition.category && profile.catalog.Fits(Selected.spec, profile.catalog.Find(p.definitionId)))
                .OrderByDescending(p => p.condition.health).ThenByDescending(p => p.definitionId == old.definitionId).ThenBy(p => p.instanceId).FirstOrDefault();
        }
        static PartInstance RequireServiceItem(PlayerProfile p, string carId, string itemId)
        {
            var car = p.cars.Find(c => c.instanceId == carId);
            var item = p.parts.Find(i => i.instanceId == itemId);
            if (car == null || item == null || !car.installed.slots.Any(s => s.partInstanceId == itemId) || (!PartFailed(item) && item.condition.health > .5f && item.condition.deposits < .05 && item.condition.leakSeverity < .01 && item.condition.adjustmentError < .05 && !EngineElements.NeedsService(item.condition)))
                throw new InvalidOperationException("This component no longer needs the displayed replacement. Refresh Repairs.");
            return item;
        }
        void RepairsGUI()
        {
            GUILayout.Label("REPAIRS / " + Selected.spec.displayName, stat);
            GUILayout.Label("Red = failed. Amber = worn or a serviceable fault. Replacement does not change your saved tune; a stronger part may be needed if the tune caused the failure.", partProse);
            GUILayout.BeginHorizontal();
            if (PartButton("repairs-browse", "Browse all parts / inventory")) pendingAction = () => ShowParts(partCategory);
            if (PartButton("repairs-garage", "Garage")) pendingAction = ReturnToGarage;
            GUILayout.EndHorizontal();
            FluidGarage();
            includeWornRepairs = GUILayout.Toggle(includeWornRepairs, "Also show worn components / service faults");
            var service = ServiceParts();
            int failed = service.Count(PartFailed);
            GUILayout.Label(failed + " failed / " + (service.Count - failed) + " worn / service-fault installed components. Removed parts remain in inventory.", partProse);
            if (service.Count == 0) GUILayout.Label("No failed or heavily worn / service-fault installed components.");
            else if (!includeWornRepairs && failed == 0) GUILayout.Label("No failed installed components. Enable worn components to inspect service candidates.");
            foreach (var item in service.Where(p => includeWornRepairs || PartFailed(p)))
            {
                var definition = profile.catalog.Find(item.definitionId); var spare = CompatibleSpare(item);
                string oldId = item.instanceId, carId = Build.instanceId, definitionId = definition.id;
                bool failedPart = PartFailed(item);
                GUILayout.BeginVertical(GUI.skin.box);
                var color = GUI.contentColor; GUI.contentColor = failedPart ? new Color(1, .4f, .35f) : new Color(1, .78f, .35f);
                GUILayout.Label((failedPart ? "FAILED / " : item.condition.health<=.5?"WORN / ":"SERVICE FAULT / ") + definition.category + " / " + item.condition.health.ToString("P0"), stat);
                GUI.contentColor = color;
                GUILayout.Label(definition.name, partProse);
                InstalledService(item);
                if (failedPart) GUILayout.Label("Cause: " + (string.IsNullOrWhiteSpace(item.condition.failureReason) ? "Condition depleted" : item.condition.failureReason), partProse);
                Rect bar = GUILayoutUtility.GetRect(0, 9, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint) {
                    var previous = GUI.color; GUI.color = new Color(.22f, .26f, .3f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
                    GUI.color = failedPart ? new Color(.9f, .2f, .18f) : new Color(.9f, .63f, .2f);
                    GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(item.condition.health), bar.height), Texture2D.whiteTexture); GUI.color = previous;
                }
                GUILayout.BeginHorizontal();
                if (PartButton("repair-preview:" + oldId, "Preview new / stats")) PreviewPart(definitionId);
                int replacementPrice=definition.category==PartCategory.Tires?TireFitments.ReplacementCost(profile,Build,definitionId):PartsMarket.Quote(profile,definition,InventoryUnits.Count(profile,Build,definition)).cost;
                bool enabled = GUI.enabled; GUI.enabled = enabled && profile.cash >= replacementPrice;
                if (PartButton("repair-buy:" + oldId, definition.category==PartCategory.Tires?"Buy same + replace":"Buy same + replace / "+DisplayUnits.Money(replacementPrice))) QueuePayment(new PaymentRequest {
                    kind = PaymentKind.Part, carId = carId, targetId = definitionId, install = true, preserveTireFitments=definition.category==PartCategory.Tires
                });
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();
                if (profile.cash < replacementPrice) GUILayout.Label("Need " + DisplayUnits.Money((replacementPrice - profile.cash)) + " more for this new part. Fit a spare or browse cheaper compatible options.", partProse);
                if (spare != null) {
                    string spareId = spare.instanceId;
                    GUILayout.Label("Available spare: " + profile.catalog.Find(spare.definitionId).name + " / " + spare.condition.health.ToString("P0"), partProse);
                    if (PartButton("repair-spare:" + oldId, "Fit this inventory spare / no purchase")) QueuePartAction(p => {
                        RequireServiceItem(p, carId, oldId);
                        var candidate = p.parts.Find(i => i.instanceId == spareId);
                        if (candidate == null || PartFailed(candidate)) throw new InvalidOperationException("The displayed spare is no longer usable.");
                        PartOperations.Install(p, p.catalog, carId, spareId);
                    });
                } else GUILayout.Label("No usable compatible loose spare.", partProse);
                var category = definition.category;
                if (PartButton("repair-options:" + oldId, "Choose alternatives / inventory / donors")) pendingAction = () => ShowParts(category);
                GUILayout.EndVertical();
            }
        }
    }
}
