using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        PartCategory partCategory;
        GUIStyle partProse;
        internal readonly Dictionary<string, Rect> partRects = new Dictionary<string, Rect>();
        internal void ShowParts(PartCategory category) { DiscardPreviews(); repairsOnly = false; partCategory = category; screen = "Parts"; scroll = Vector2.zero; partRects.Clear(); }
        bool PartButton(string id, string label)
        {
            bool clicked = GUILayout.Button(label);
            if (Event.current.type == EventType.Repaint)
            { var rect = GUILayoutUtility.GetLastRect(); partRects[id] = RuntimeGuiRect.ToClient(rect); }
            return clicked;
        }
        internal bool CommitParts(long revision, Action<PlayerProfile> operation)
        {
            if (screen == "Race" && race != null && race.phase != RacePhase.Finished) { notice = "Parts are locked during a run."; return false; }
            PlayerProfile candidate; string error;
            bool ok = MechanicsTransactions.Try(profile, profile.catalog, revision, operation, ProfileStore.TrySave, out candidate, out error);
            if (!ok) { notice = error; return false; }
            profile = candidate; settings = profile.driving; lastBenchmarkKey = null; saveDelay = 0;
            DiscardPreviews();
            if (screen == "Collection" || screen == "Assemblies" || screen == "Setups" || screen == "Valuation" || Build == null) ShowGarageCar();
            setupPlan = null;
            notice = "Parts saved. Removed components retain their identity and condition.";
            return true;
        }
        void QueuePartAction(Action<PlayerProfile> operation) { long revision = profile.revision; pendingAction = () => CommitParts(revision, operation); }
        void PartsGUI()
        {
            var catalog = profile.catalog;
            if (partProse == null) partProse = new GUIStyle(GUI.skin.label) { wordWrap = true };
            if (repairsOnly) { RepairsGUI(); return; }
            GUILayout.Label("PARTS / provisional prices / inventory persists", stat);
            if(PartButton("parts-setups", "Save / load configurations / buy missing setup parts")) pendingAction = OpenSetups;
            if(PartButton("parts-valuation", "Compare used-market quotes / preview only")) pendingAction = () => OpenValuation(ValuationKind.Car, Build.instanceId);
            MaintenanceSummary();
            var categories = MechanicalCatalog.Slots(Selected.spec).ToArray();
            var service = ServiceParts();
            int chosen = GUILayout.SelectionGrid(Math.Max(0, Array.IndexOf(categories, partCategory)), categories.Select(c => (service.Any(p => catalog.Find(p.definitionId).category == c && PartFailed(p)) ? "FAILED / " : service.Any(p => catalog.Find(p.definitionId).category == c) ? "SERVICE / " : "") + PartSemantics.Friendly(c.ToString())).ToArray(), 3);
            if (categories[chosen] != partCategory) { partCategory = categories[chosen]; partRects.Clear(); PreviewPart(); }
            var car = Build; string carId = car.instanceId;
            var resolved = MechanicsOwnership.Resolve(profile, car, catalog);
            GUILayout.Label(resolved.Drivable ? "Ready / internal strength headroom: " + SharedUnitSettings.Torque(resolved.safeTorqueNm, "F0") + ", " + resolved.safeRpm.ToString("F0") + " RPM" : "Cannot drive: " + string.Join("; ", resolved.errors));
            GUILayout.BeginHorizontal();
            if (PartButton("safe", "Preview conservative tune defaults")) pendingAction = () =>
            { var copy = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile)); MechanicsOwnership.Ensure(copy, catalog); PartOperations.SafeDefaults(copy, catalog, carId); OpenTuning(); draft.values = copy.cars.Find(c => c.instanceId == carId).tune.Copy(); };
            if (PartButton("garage", "Garage")) pendingAction = ReturnToGarage;
            GUILayout.EndHorizontal();
            var binding = car.installed.slots.Find(s => s.slot == partCategory.ToString());
            var installed = binding == null ? null : profile.parts.Find(p => p.instanceId == binding.partInstanceId);
            GUILayout.Label("INSTALLED / " + partCategory);
            GUILayout.Label((installed == null ? "Empty slot: " : "If removed: ") + PartSemantics.RemovalEffect(partCategory, catalog.Spec(car.vehicleId), MechanicsOwnership.Definitions(profile, car), catalog), partProse);
            if (installed == null) GUILayout.Label("Empty slot — incomplete builds remain owned.");
            else
            {
                GUILayout.Label(catalog.Find(installed.definitionId).name + " / " + Condition(installed));
                InstalledService(installed);
                var category = partCategory;
                GUILayout.BeginHorizontal();
                if (PartButton("preview-remove", "Preview removal / effect on complete build")) PreviewPart(remove: category);
                if (PartButton("remove", "Remove to inventory")) QueuePartAction(p => PartOperations.Remove(p, carId, category));
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("COMPATIBLE CATALOG / no preceding purchases required");
            foreach (var definition in catalog.parts.Where(p => p.category == partCategory && catalog.Fits(Selected.spec, p)))
            {
                string id = definition.id;
                GUILayout.Label(definition.name + " / $" + definition.price + " / " + definition.quality);
                GUILayout.Label(SharedUnitSettings.TorqueText(definition.description), partProse);
                GUILayout.Label(PartTuning.Summary(definition), partProse);
                foreach (var value in PartSemantics.Stats(definition)) GUILayout.Label(value.label + ": " + (value.unit == "Nm" ? SharedUnitSettings.Torque(value.value, "0.##") : value.unit == "bar" ? SharedUnitSettings.Pressure(value.value) : value.value.ToString("0.##") + " " + value.unit));
                GUILayout.BeginHorizontal();
                if (PartButton("preview:" + id, "Preview stats / dyno")) PreviewPart(id);
                GUI.enabled = profile.cash >= definition.price && profile.reputation >= definition.reputationRequired;
                if(definition.reputationRequired>0)GUILayout.Label("Requires REP "+definition.reputationRequired+" / independent offer");
                if (PartButton("buy:" + id, "Buy loose")) QueuePayment(new PaymentRequest { kind = PaymentKind.Part, carId = carId, targetId = id });
                if (PartButton("fit:" + id, "Buy + install")) QueuePayment(new PaymentRequest { kind = PaymentKind.Part, carId = carId, targetId = id, install = true });
                GUI.enabled = true; GUILayout.EndHorizontal();
            }
            GUILayout.Label("LOOSE INVENTORY / compatible " + partCategory);
            foreach (var item in profile.parts.Where(p => p.location == PartLocation.Inventory && catalog.Find(p.definitionId).category == partCategory && catalog.Fits(Selected.spec, catalog.Find(p.definitionId))))
            {
                string id = item.instanceId; var definition = catalog.Find(item.definitionId);
                GUILayout.Label(definition.name + " / " + Condition(item)); GUILayout.BeginHorizontal();
                if (PartButton("preview-item:" + id, "Preview worn part")) PreviewPart(instanceId: id);
                if (PartButton("install:" + id, "Install / reuse")) QueuePartAction(p => PartOperations.Install(p, catalog, carId, id));
                if (PartButton("sell:" + id, "Sell loose / preview buyer quote")) pendingAction = () => OpenValuation(ValuationKind.LoosePart, id);
                if (PartButton("part-quote:" + id, "Buyer quote")) pendingAction = () => OpenValuation(ValuationKind.LoosePart, id);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("DONORS / transfer leaves the donor slot empty");
            foreach (var donor in profile.cars.Where(c => c.instanceId != carId))
            {
                var donorBinding = donor.installed.slots.Find(s => s.slot == partCategory.ToString());
                var item = donorBinding == null ? null : profile.parts.Find(p => p.instanceId == donorBinding.partInstanceId);
                if (item == null || !catalog.Fits(Selected.spec, catalog.Find(item.definitionId))) continue;
                string donorId = donor.instanceId; var category = partCategory;
                if (PartButton("preview-donor:" + donorId, "Preview donor component")) PreviewPart(instanceId: item.instanceId);
                if (PartButton("donor:" + donorId, "Transfer from " + VehicleCollection.Label(catalog, donor) + " / " + Condition(item))) QueuePartAction(p => PartOperations.Transfer(p, catalog, donorId, carId, category));
            }
            int incompatible = profile.parts.Count(p => p.location == PartLocation.Inventory && !catalog.Fits(Selected.spec, catalog.Find(p.definitionId)));
            GUILayout.Label(incompatible + " loose parts fit another vehicle. Select its model in Garage to inspect them.");
        }
        static string Condition(PartInstance item) { return item.condition.health.ToString("P0") + (item.condition.failed ? " / FAILED: " + item.condition.failureReason : "") + " / " + item.instanceId.Substring(0, 13); }
        internal void ScrollToPart(string key)
        {
            Rect rect; if (!partRects.TryGetValue(key, out rect)) return;
            if (rect.center.y > Screen.height - 110) scroll.y += rect.center.y - (Screen.height - 160);
            else if (rect.center.y < 135) scroll.y = Mathf.Max(0, scroll.y - (135 - rect.center.y));
        }
    }
}
