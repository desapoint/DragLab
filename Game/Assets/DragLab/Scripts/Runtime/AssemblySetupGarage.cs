using System;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        internal string setupName = "My setup";
        internal SetupScope setupScope;
        internal SetupPlan setupPlan;
        string inspectedEngine;
        bool retentionPreview;
        bool showKeptSetupParts;
        internal void OpenSetups() { DiscardPreviews(); setupPlan = null; screen = "Setups"; scroll = Vector2.zero; partRects.Clear(); }
        internal void OpenAssemblies() { DiscardPreviews(); screen = "Assemblies"; scroll = Vector2.zero; partRects.Clear(); }
        void SetupGUI()
        {
            GUILayout.Label("SAVED CONFIGURATIONS / hardware blueprints + tunes", stat);
            GUILayout.Label("Save the applied build. Blueprints retain desired parts and adjustments, never fuel, wear or sold item identities.");
            setupName = GUILayout.TextField(setupName, 64);
            setupScope = (SetupScope)GUILayout.SelectionGrid((int)setupScope, new[] { "Engine + controls", "Whole car", "Tune only" }, 3);
            string carId = Build.instanceId;
            if (PartButton("setup-save", "Save applied " + setupScope + " / " + setupName)) {
                string name = setupName; var scope = setupScope;
                QueuePartAction(p => SavedSetups.Save(p, carId, name, scope));
            }
            if (PartButton("setup-tuning", "Edit tuning / Apply before saving a setup")) pendingAction = OpenTuning;
            foreach (var saved in profile.savedSetups.ToArray()) {
                string id = saved.id;
                GUILayout.Space(5); GUILayout.Label(saved.name + " / " + saved.scope + " / " + profile.catalog.Spec(saved.vehicleDefinitionId).displayName + " / " + saved.parts.Count + " hardware slots");
                GUILayout.BeginHorizontal();
                if (PartButton("setup-preview:" + id, "Load preview / no changes yet")) { setupPlan = SavedSetups.Preview(profile, carId, id); }
                if (PartButton("setup-delete:" + id, "Delete blueprint")) QueuePartAction(p => SavedSetups.Delete(p, id));
                GUILayout.EndHorizontal();
            }
            if (setupPlan != null) {
                var plan = setupPlan;
                if (plan.revision != profile.revision || plan.carId != carId) { setupPlan = null; GUILayout.Label("Build changed: load a fresh preview."); }
                else {
                    GUILayout.Space(10); GUILayout.Label("LOAD PREVIEW / " + DisplayUnits.Money(plan.cost) + " / " + plan.Missing + " missing purchases", stat);
                    GUILayout.Label(plan.lines.Count(l => l.installed) + " fitted parts kept / " + plan.lines.Count(l => !l.installed && !l.purchase) + " loose parts reused / " + plan.Missing + " new parts needed");
                    if (plan.resolution != null) {
                        var current = MechanicsOwnership.Resolve(profile, Build, profile.catalog);
                        GUILayout.Label("Peak power " + DragMath.PeakHorsepower(current.spec, current.tune).ToString("F0") + " → " + DragMath.PeakHorsepower(plan.resolution.spec, plan.tune).ToString("F0") + " hp / launch " + Build.tune.launchRpm.ToString("F0") + " → " + plan.tune.launchRpm.ToString("F0") + " RPM");
                    }
                    foreach (var message in plan.errors) GUILayout.Label("BLOCKED: " + message);
                    GUI.enabled = plan.errors.Count == 0 && plan.Missing == 0;
                    if (PartButton("setup-apply-owned", "Apply / reuse owned parts / no purchases")) QueuePartAction(p => SavedSetups.Apply(p, carId, plan.setupId, plan.revision, false, plan.cost));
                    GUI.enabled = plan.errors.Count == 0 && plan.Missing > 0;
                    if (PartButton("setup-apply-buy", "Buy missing + apply / " + DisplayUnits.Money(plan.cost) + " total")) QueuePartAction(p => SavedSetups.Apply(p, carId, plan.setupId, plan.revision, true, plan.cost));
                    GUI.enabled = true;
                    showKeptSetupParts = GUILayout.Toggle(showKeptSetupParts, "Show unchanged fitted components");
                    foreach (var line in plan.lines.Where(l => showKeptSetupParts || !l.installed).OrderByDescending(l => l.purchase)) {
                        var d = profile.catalog.Find(line.definitionId);
                        var item = profile.parts.Find(p => p.instanceId == line.itemId);
                        GUILayout.Label(d.name + " / " + (line.purchase ? "BUY MISSING " + DisplayUnits.Money(line.cost) : line.installed ? "Keep installed / " + Condition(item) : "Reuse inventory / " + Condition(item)));
                    }
                    GUILayout.Label("Other fitted components in this setup's scope move to inventory. Parts on other cars or stored engines are never taken automatically.");
                    foreach (var message in plan.warnings) GUILayout.Label("NOTE: " + message);
                }
            }
            if (PartButton("setups-garage", "Garage")) pendingAction = ReturnToGarage;
        }
        void AssembliesGUI()
        {
            string carId = Build.instanceId;
            GUILayout.Label("ENGINES / owned assemblies + incomplete chassis", stat);
            if (PartButton("assemblies-valuation", "Value selected car / chassis / engine")) pendingAction = () => OpenValuation(ValuationKind.Car, Build.instanceId);
            GUILayout.Label(VehicleCollection.Label(profile.catalog, Build) + " / chassis " + Build.chassisId.Substring(Build.chassisId.Length - 6));
            GUILayout.Label("Stored engines keep actual parts, individual faults, liquids and engine adjustments. Chassis controls, gearbox, clutch, fuel and road hardware stay with the car.");
            bool attached = !string.IsNullOrEmpty(Build.engineAssemblyId);
            GUILayout.Label(attached ? "Installed engine: " + Build.engineAssemblyId : "No engine attached / chassis retained / race unavailable");
            GUI.enabled = attached && Build.engineRebuild == null;
            if (PartButton("engine-detach", "Detach engine / keep all engine descendants")) QueuePartAction(p => AssemblyOperations.Detach(p, carId));
            GUI.enabled = true;
            if (PartButton("engine-retention", "Preview keep engine / dispose chassis / remaining assets")) retentionPreview = !retentionPreview;
            if (retentionPreview) {
                var preview = AssemblyOperations.PreviewKeepEngine(profile, carId);
                GUILayout.Label("KEEP: " + (preview.retainedEngineId ?? "No attached engine") + " / " + preview.retainedParts.Length + " actual descendants. Their condition and fluids are retained.");
                GUILayout.Label("REMAINING / proposed disposal: chassis + " + preview.remainingParts.Length + " road/control parts + chassis fuel. Loose inventory and stored engines remain owned.");
                GUILayout.Label("Open Valuation, choose Chassis / keep engine, then confirm the quoted salvage. Attached engine descendants and fluids remain stored.");
            }
            foreach (var engine in profile.engines.Where(e => string.IsNullOrEmpty(e.parentVehicleId)).ToArray()) {
                string id = engine.instanceId;
                GUILayout.Space(8); GUILayout.Label(AssemblyOperations.DisplayName(profile,engine) + " / " + id.Substring(id.Length - 6) + " / " + engine.slots.Count + " fitted parts");
                var missing = AssemblyOperations.Missing(profile, engine);
                GUILayout.Label(missing.Length == 0 ? "Complete OEM-required engine slots" : "Incomplete: " + string.Join(", ", missing));
                GUILayout.Label("Oil " + engine.fluids.oilLitres.ToString("F2") + " L / coolant " + engine.fluids.coolantLitres.ToString("F2") + " L / worn or failed items remain unchanged");
                GUILayout.BeginHorizontal();
                GUI.enabled = !attached && AssemblyOperations.Fits(engine, Build);
                if (PartButton("engine-attach:" + id, "Attach to selected chassis")) QueuePartAction(p => AssemblyOperations.Attach(p, id, carId));
                GUI.enabled = true;
                if (PartButton("engine-inspect:" + id, "Inspect / reassemble")) inspectedEngine = id;
                if (PartButton("engine-quote:" + id, "Buyer quote")) pendingAction = () => OpenValuation(ValuationKind.Engine, id);
                GUILayout.EndHorizontal();
            }
            var selected = profile.engines.Find(e => e.instanceId == inspectedEngine && string.IsNullOrEmpty(e.parentVehicleId));
            if (selected != null) {
                string engineId = selected.instanceId;
                GUILayout.Space(8); GUILayout.Label("STORED ENGINE HARDWARE / remove or reuse exact parts", stat);
                foreach (var binding in selected.slots.ToArray()) {
                    var item = profile.parts.Find(i => i.instanceId == binding.partInstanceId); var category = AssemblyOperations.Parse(binding.slot);
                    GUILayout.Label(profile.catalog.Find(item.definitionId).name + " / " + Condition(item));
                    if (PartButton("engine-remove:" + item.instanceId, "Remove " + PartSemantics.Friendly(binding.slot) + " to loose inventory")) QueuePartAction(p => AssemblyOperations.RemovePart(p, engineId, category));
                }
                foreach (var item in profile.parts.Where(i => i.location == PartLocation.Inventory && MechanicalCatalog.EngineSlot(profile.catalog.Find(i.definitionId).category) && profile.catalog.Fits(profile.catalog.Spec(selected.vehicleDefinitionId), profile.catalog.Find(i.definitionId))).ToArray()) {
                    string partId = item.instanceId;
                    if (PartButton("engine-install:" + partId, "Fit loose " + profile.catalog.Find(item.definitionId).name + " / " + Condition(item))) QueuePartAction(p => AssemblyOperations.InstallPart(p, engineId, partId));
                }
            }
            if (PartButton("assemblies-collection", "Select another owned chassis / collection")) pendingAction = OpenCollection;
            if (PartButton("assemblies-garage", "Garage")) pendingAction = ReturnToGarage;
        }
    }
}
