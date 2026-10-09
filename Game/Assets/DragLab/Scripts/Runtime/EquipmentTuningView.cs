using System;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Runtime
{
    // Installed hardware controls access. This view never claims pedal ownership.
    public static class EquipmentTuningView
    {
        public static bool DrawHardware(VehicleSpec spec, Tune tune)
        {
            bool changed = false; var profile = new InductionProfile(spec, tune);
            Label("INSTALLED HARDWARE TUNING / physical sizes require a part swap");
            if (tune.partFinalDriveAdjustable) changed |= HardwareSlider("Final-drive gearset", ref tune.finalDrive, tune.partFinalDriveMin, tune.partFinalDriveMax, spec.finalDrive);
            else Label("Fixed final drive: " + tune.finalDrive.ToString("F2") + ":1");
            if (profile.turboCount > 0) Label("Turbo size: " + tune.TurboSize.ToString("F2") + "× reference per unit / " + profile.turboCount + " units");
            if (profile.turboCount + profile.superchargerCount > 0) {
                if (tune.partBoostAdjustable) changed |= HardwareSlider("Boost target multiplier / spring floor", ref tune.boostSetting, tune.partBoostMin, tune.partBoostMax, 1);
                else Label("Fixed boost calibration: " + tune.BoostSetting.ToString("F2") + "×");
            }
            if (profile.superchargerCount > 0) {
                if (tune.partPulleyAdjustable) changed |= HardwareSlider("Interchangeable pulley drive ratio", ref tune.superchargerPulley, tune.partPulleyMin, tune.partPulleyMax, 1);
                else Label("Fixed pulley drive: " + tune.SuperchargerPulley.ToString("F2") + "×");
            }
            if (profile.turboCount > 0 && tune.partValveAdjustable) {
                Label("Pressure-release valve setup");
                var next = (BlowOffType)GUILayout.SelectionGrid((int)tune.blowOffValve, new[] { "Kit default", "Recirculated", "Hiss", "Chirp", "Flutter" }, 3);
                if (next != tune.blowOffValve) { tune.blowOffValve = next; changed = true; }
            }
            return changed;
        }
        static bool HardwareSlider(string label, ref float value, float min, float max, float defaultValue)
        {
            Label(label + ": " + value.ToString("F2")); return TuningSlider.Draw(ref value, min, max, Mathf.Clamp(defaultValue, min, max));
        }
        public static bool Draw(VehicleSpec spec, Tune installed, Tune draft)
        {
            bool changed = false;
            if (spec.valvetrain.timingAdjustable) changed |= HardwareSlider("Cam timing (crank degrees; advance + / retard -)", ref draft.camTimingDegrees, -6, 6, 0);
            else Label("Cam timing is fixed. Install an adjustable timing drive to tune it.");
            var controls = new EngineBehaviorProfile(spec, installed);
            Label("FITTED CONTROLS / " + controls.limiter + " / two-step " + controls.twoStep);
            if (installed.partLimiterAdjustable) {
                float rpm = draft.revLimitRpm > 0 ? draft.revLimitRpm : spec.redlineRpm;
                if (Slider("Electronic limiter threshold (RPM)", ref rpm, spec.idleRpm + 1500, spec.redlineRpm * 1.2f, spec.redlineRpm)) { draft.revLimitRpm = rpm; changed = true; }
                changed |= Limiter("Limiter behavior", ref draft.limiter, false);
                changed |= Cut("Limiter cut", ref draft.limiterCut, spec.engineBehavior.fueling);
            } else Label("Limiter threshold/cut tuning requires a programmable rev-limiter module in Parts.");
            if (installed.partTwoStepAdjustable) {
                changed |= Equipment("Two-step launch cut", ref draft.twoStep);
                changed |= Slider("Two-step threshold / launch RPM", ref draft.launchRpm, spec.idleRpm, spec.redlineRpm - 200, Mathf.Clamp(3200, spec.idleRpm, spec.redlineRpm - 200));
                changed |= Limiter("Two-step behavior", ref draft.twoStepStyle, true);
                changed |= Cut("Two-step cut", ref draft.twoStepCut, spec.engineBehavior.fueling);
            } else Label("Adjustable launch cuts require programmable TwoStep hardware. Basic module cuts at 3500 RPM; normal foot-controlled launch RPM remains adjustable.");
            if (installed.controlLevel > 0 && new InductionProfile(spec, installed).turboCount > 0) {
                changed |= Equipment("Turbo launch preload", ref draft.launchPreload);
                changed |= Equipment("Overrun / shift anti-lag", ref draft.antiLag);
            } else Label("Preload/anti-lag tuning requires a programmable controller and turbo hardware.");
            if (installed.partShiftLightAdjustable) {
                float rpm = (float)draft.ShiftLightRpm(spec);
                // Draft's projection flags are intentionally not authoritative.
                if (draft.shiftLightRpm > 0) rpm = draft.shiftLightRpm;
                if (Slider("Shift-light alert threshold (RPM)", ref rpm, spec.idleRpm + 1000, spec.redlineRpm, spec.redlineRpm - 200)) { draft.shiftLightRpm = rpm; changed = true; }
            } else Label(installed.partShiftLight ? "Fixed shift light: " + installed.ShiftLightRpm(spec).ToString("F0") + " RPM." : "No shift light installed. Fit one in Parts for a shift alert.");
            return changed;
        }
        static GUIStyle prose;
        static void Label(string value) { if (prose == null) prose = new GUIStyle(GUI.skin.label) { wordWrap = true }; GUILayout.Label(value, prose); }
        static bool Slider(string name, ref float value, float min, float max, float defaultValue)
        {
            Label(name + ": " + value.ToString("F0"));
            return TuningSlider.Draw(ref value, min, max, defaultValue);
        }
        static bool Equipment(string name, ref EquipmentSetting value)
        {
            Label(name); var next = (EquipmentSetting)GUILayout.SelectionGrid((int)value, new[] { "Factory", "Off", "On" }, 3);
            bool changed = next != value; if (changed) value = next; return changed;
        }
        static bool Limiter(string name, ref LimiterStyle value, bool launch)
        {
            Label(name); var next = (LimiterStyle)GUILayout.SelectionGrid((int)value, launch ? new[] { "Module default", "Hard bounce", "Soft cut" } : new[] { "Module default", "Hard bounce", "Soft cut", "Disabled" }, launch ? 3 : 2);
            bool changed = next != value; if (changed) value = next; return changed;
        }
        static bool Cut(string name, ref CutMethod value, FuelSystem fuel)
        {
            if (fuel == FuelSystem.Carburetor) { Label(name + ": ignition cut (carb cannot electronically stop fuel flow)"); return false; }
            Label(name); var next = (CutMethod)GUILayout.SelectionGrid((int)value, new[] { "Module default", "Fuel cut", "Ignition cut" }, 3);
            bool changed = next != value; if (changed) value = next; return changed;
        }
    }
}
