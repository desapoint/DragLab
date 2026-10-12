using System;
using System.Collections.Generic;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        TuneDraft draft;
        string tuningResolutionKey; BuildResolution tuningResolution;
        internal readonly Dictionary<string, Rect> tuneRects = new Dictionary<string, Rect>();
        internal bool DraftDirty { get { return draft != null && (JsonUtility.ToJson(draft.values) != JsonUtility.ToJson(draft.saved) || draft.paint != draft.savedPaint); } }
        internal Tune DraftValues { get { return draft == null ? null : draft.values; } }
        internal void ScrollTuningTo(string key) { if (tuneRects.TryGetValue(key, out var rect)) scroll.y = Mathf.Max(0, scroll.y + (rect.center.y - 350) / RaceInputAdapter.Scale(Screen.width, Screen.height)); }
        internal void BeginTuneDraft() { tuneRects.Clear(); draft = new TuneDraft(Build, profile.revision); notice = "Tuning preview: Apply changes to use them in races. Leaving discards edits."; }
        internal void EnsureTuneDraft() { if(Build!=null&&(draft==null||draft.vehicleInstanceId!=Build.instanceId||draft.revision!=profile.revision))BeginTuneDraft(); }
        internal void OpenTuning() { DiscardPreviews(); screen = "Tuning"; scroll = Vector2.zero; BeginTuneDraft(); }
        internal void DiscardPreviews() { draft = null; previewDefinitionId = previewInstanceId = null; previewRemoval = null; statsKey = null;
            if (playerView != null && profile != null && Build != null) { var visible = MechanicsOwnership.Resolve(profile, Build, profile.catalog); playerView.ApplySetup(visible.spec, visible.tune); Color paint; if (ColorUtility.TryParseHtmlString(Build.paintHex, out paint)) playerView.SetPaint(paint); } }
        internal bool ApplyTuneDraftNow()
        {
            if (screen != "Tuning" || draft == null) return false;
            var pending = draft; PlayerProfile committed; string error;
            if (!MechanicsTransactions.Try(profile, profile.catalog, pending.revision, pending.ApplyTo, ProfileStore.TrySave, out committed, out error)) { notice = error; return false; }
            profile = committed; settings = profile.driving; saveDelay = 0; lastBenchmarkKey = statsKey = null; ShowGarageCar(); BeginTuneDraft(); notice = "Tune applied and saved. Races use this build."; return true;
        }
        void ApplyTuneDraft() { ApplyTuneDraftNow(); }
        bool TuneButton(string key, string label)
        {
            bool clicked = GUILayout.Button(label, GUILayout.Height(30));
            if (Event.current.type == EventType.Repaint) tuneRects[key] = RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());
            return clicked;
        }
        bool TuneSlider(string key, string label, ref float value, float min, float max, string format, float defaultValue)
        {
            GUILayout.Label(label + ": " + value.ToString(format));
            bool changed = TuningSlider.Draw(ref value, min, max, defaultValue);
            if (Event.current.type == EventType.Repaint) tuneRects[key] = RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());
            return changed;
        }
        void TuningGUI()
        {
            if(PartButton("tuning-setups", "Saved configurations / applied tune library")) pendingAction = OpenSetups;
            EnsureTuneDraft();
            var tune = draft.values; var defaults = Selected.CreateStockTune(); defaults.ClampTo(Selected.spec); string resolvedKey=profile.revision+"/"+Build.instanceId+"/"+JsonUtility.ToJson(tune);
            if(tuningResolutionKey!=resolvedKey){tuningResolutionKey=resolvedKey;tuningResolution=profile.catalog.Resolve(Build.vehicleId,MechanicsOwnership.Definitions(profile,Build),tune);}
            var resolved=tuningResolution;
            GUILayout.Label("TUNING / " + (DraftDirty ? "UNAPPLIED DRAFT" : "saved values"), stat);
            GUILayout.BeginHorizontal();
            if (TuneButton("apply-top", "Apply changes")) pendingAction = ApplyTuneDraft;
            if (TuneButton("discard-top", "Revert draft")) pendingAction = BeginTuneDraft;
            GUILayout.EndHorizontal();
            GUILayout.Label("Leaving this page discards unapplied changes. Parts purchases/installations use the saved tune.");
            GUILayout.Label(TuningSlider.Help);
            var soundHardware = Selected.spec.sound ?? new EngineSoundSpec();
            GUILayout.Label("ENGINE HARDWARE / " + soundHardware.cylinders + " cylinders / " + Selected.spec.DisplacementLitres.ToString("F1") + " litres", stat);
            var inductionHardware = new InductionProfile(resolved.spec, resolved.tune);
            if (inductionHardware.turboCount > 0 && resolved.tune.partValveAdjustable)
            {
                var valve = (BlowOffType)GUILayout.SelectionGrid((int)tune.blowOffValve, new[] { "Factory valve", "Recirculated", "Hiss", "Chirp", "Flutter" }, 3);
                if (valve != tune.blowOffValve) { tune.blowOffValve = valve; }
            }
            GUILayout.Space(8); GUILayout.Label("TUNE", stat);
            bool dirty = false;
            dirty |= AwdSettingsView.Draw(resolved.spec, tune);
            dirty |= EquipmentTuningView.Draw(resolved.spec, resolved.tune, tune);
            bool cold = GUILayout.Toggle(tune.coldStart, "Cold-start engine / warmup affects carb idle and hesitation");
            if (cold != tune.coldStart) { tune.coldStart = cold; dirty = true; }
            float diameter = (float)tune.WheelDiameter, width = (float)tune.WheelWidth, wheelMass = (float)tune.WheelMass;
            float radiusIn = (float)(Selected.spec.wheelRadiusM * tune.WheelDiameter / DisplayUnits.MetresPerInch);
            if (TuneSlider("radius", "Outer tire radius (inches, including rubber)", ref radiusIn, (float)(Selected.spec.wheelRadiusM * .8 / DisplayUnits.MetresPerInch), (float)(Selected.spec.wheelRadiusM * 1.6 / DisplayUnits.MetresPerInch), "F2", (float)(Selected.spec.wheelRadiusM / DisplayUnits.MetresPerInch))) { tune.wheelDiameter = (float)(radiusIn * DisplayUnits.MetresPerInch / Selected.spec.wheelRadiusM); dirty = true; }
            if (TuneSlider("width", "Wheel width multiplier / grip + mass", ref width, .8f, 1.6f, "F2", 1)) { tune.wheelWidth = width; dirty = true; }
            GUILayout.Label("Wheel and flywheel mass come from installed parts; replace them in Parts.");
            GUILayout.Label("Flywheel " + ((resolved.spec.flywheel ?? new FlywheelSpec()).massKg * resolved.tune.FlywheelMass).ToString("F1") + " kg / inertia " + FlywheelDynamics.Inertia(resolved.spec, resolved.tune).ToString("F3") + " kg m² / engine + flywheel " + resolved.spec.RotationalInertia(resolved.tune).ToString("F3"));
            var rolling = new WheelProfile(resolved.spec, tune);
            GUILayout.Label("Wheel " + rolling.assemblyMass.ToString("F1") + " kg each / radius " + rolling.radius.ToString("F3") + " m / outer radius " + (rolling.radius / DisplayUnits.MetresPerInch).ToString("F2") + " in / effective rolling mass " + rolling.effectiveMass.ToString("F0") + " kg");
            dirty |= TuneSlider("driver-mass", "Driver mass (kg)", ref tune.driverMassKg, 0, 200, "F0", 75);
            dirty |= TuneSlider("payload-mass", "Payload / ballast mass (kg)", ref tune.payloadMassKg, 0, 1000, "F0", 0);
            dirty |= TuneSlider("payload-position", "Payload position from front axle (m)", ref tune.payloadPositionM, -.5f, Selected.spec.wheelbaseM + .5f, "F2", 1.5f);
            dirty |= TuneSlider("payload-height", "Payload height (m)", ref tune.payloadHeightM, 0, 2, "F2", .45f);
            dirty |= TuneSlider("launch", "Launch RPM", ref tune.launchRpm, Selected.spec.idleRpm, Selected.spec.redlineRpm - 200, "F0", defaults.launchRpm);
            if (resolved.tune.partFinalDriveAdjustable)
                dirty |= TuneSlider("final", "Final-drive ratio (higher = stronger launch / lower speed per RPM)", ref tune.finalDrive, resolved.tune.partFinalDriveMin, resolved.tune.partFinalDriveMax, "F2", defaults.finalDrive);
            else GUILayout.Label("Fixed final-drive gearset: " + resolved.tune.finalDrive.ToString("F2") + ":1. Install a gearbox with interchangeable gearsets in Parts to adjust it.");
            dirty |= TuneSlider("shift", "Auto shift RPM", ref tune.shiftRpm, Selected.spec.idleRpm + 1500, Selected.spec.redlineRpm - 100, "F0", defaults.shiftRpm);
            var units = SharedUnitSettings.Current; float pressure = (float)DisplayUnits.Pressure(tune.tirePressurePsi / DisplayUnits.PsiPerBar, units.pressure);
            if (TuneSlider("pressure", "Tire pressure " + DisplayUnits.PressureLabel(units.pressure), ref pressure, (float)DisplayUnits.Pressure(18 / DisplayUnits.PsiPerBar, units.pressure), (float)DisplayUnits.Pressure(42 / DisplayUnits.PsiPerBar, units.pressure), "F2", (float)DisplayUnits.Pressure(defaults.tirePressurePsi / DisplayUnits.PsiPerBar, units.pressure))) { tune.tirePressurePsi = (float)(DisplayUnits.ToBar(pressure, units.pressure) * DisplayUnits.PsiPerBar); dirty = true; }
            if (inductionHardware.turboCount > 0)
            {
                GUILayout.Label("Installed turbo size: " + resolved.tune.TurboSize.ToString("F2") + "× reference per unit. Physical size is changed by replacing the kit in Parts.");
                if (!resolved.tune.partValveAdjustable) GUILayout.Label("Fixed pressure-release valve: " + inductionHardware.valve + ". Choose an adjustable kit to change its setup.");
            }
            if (inductionHardware.turboCount + inductionHardware.superchargerCount > 0)
            {
                float boost = (float)resolved.tune.BoostSetting;
                if (resolved.tune.partBoostAdjustable) {
                    if (TuneSlider("boost", "Boost target multiplier / installed spring is the minimum", ref boost, resolved.tune.partBoostMin, resolved.tune.partBoostMax, "F2", 1)) { tune.boostSetting = boost; dirty = true; }
                } else GUILayout.Label("Fixed boost calibration: " + boost.ToString("F2") + "×. An adjustable wastegate/controller kit unlocks this setting.");
            }
            if (inductionHardware.superchargerCount > 0)
            {
                float pulley = (float)resolved.tune.SuperchargerPulley;
                if (resolved.tune.partPulleyAdjustable) {
                    if (TuneSlider("pulley", "Supercharger pulley / higher = boost + drive load", ref pulley, resolved.tune.partPulleyMin, resolved.tune.partPulleyMax, "F2", 1)) { tune.superchargerPulley = pulley; dirty = true; }
                } else GUILayout.Label("Fixed supercharger drive: " + pulley.ToString("F2") + "×. Install an interchangeable-pulley kit to adjust it.");
            }
            if (dirty) notice = "Preview only. Apply to save this tune; leaving Tuning discards the draft.";
            if (TuneButton("apply", "Apply tune to car")) pendingAction = ApplyTuneDraft;
            if (TuneButton("discard", "Discard draft / restore saved values")) pendingAction = BeginTuneDraft;
            for (int slot = 0; slot < 3; slot++)
            {
                int index = slot;
                GUILayout.BeginHorizontal();
                GUI.enabled = !DraftDirty;
                if (GUILayout.Button("Store saved tune " + (slot + 1))) { Build.SavePreset(index); ProfileStore.Save(profile); }
                GUI.enabled = Build.presets.Count > slot && Build.presets[slot] != null;
                if (GUILayout.Button("Preview tune " + (slot + 1))) pendingAction = () => { draft.values.ApplyAdjustments(Build.presets[index]); draft.values.ClampTo(Selected.spec); notice = "Preset loaded into preview. Apply to use it in races."; };
                GUI.enabled = true; GUILayout.EndHorizontal();
            }
            if (Build.completedRuns > 0)
                GUILayout.Label("RECORDS / " + Build.completedRuns + " passes / quarter " + Build.bestQuarterSeconds.ToString("F3") + " s / 60 ft " + Build.bestSixtyFootSeconds.ToString("F3") + " s / trap " + SharedUnitSettings.Speed(Build.bestTrapKmh / 3.6));
            GUILayout.BeginHorizontal();
            foreach (string paint in new[] { "#58C7BA", "#6CAAF8", "#EFA04F", "#E4E7ED", "#DD608B" })
            {
                Color col; ColorUtility.TryParseHtmlString(paint, out col);
                var old = GUI.backgroundColor; GUI.backgroundColor = col;
                if (GUILayout.Button("Preview paint", GUILayout.Height(28))) draft.paint = paint;
                GUI.backgroundColor = old;
            }
            GUILayout.EndHorizontal();
        }
    }
}
