using System;
using DragLab.Domain;
using UnityEngine;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    // Configuration controls do not own the pedals. Garage edits stay in the tune draft.
    public static class AwdSettingsView
    {
        public static bool Draw(VehicleSpec vehicle, Tune tune)
        {
            GUILayout.Label("DRIVE LAYOUT / " + vehicle.drive);
            if (vehicle.drive != DriveType.AWD) return false;
            var data = AxleDriveDynamics.Configuration(vehicle, tune); var defaults = vehicle.awd ?? new AwdSpec(); bool changed = false;
            var center = Array.Find(vehicle.fittedParts, p => p.category == DragLab.Domain.Mechanics.PartCategory.Differential && p.stage > 0);
            bool hardwareForced = center != null;
            GUILayout.Label(hardwareForced ? "Center mode fixed by fitted " + center.name + ". Replace hardware in Parts to change it." : "Factory center hardware supports prototype mode configuration.");
            bool enabled = GUI.enabled; GUI.enabled = enabled && !hardwareForced;
            var mode = (AwdMode)GUILayout.SelectionGrid((int)data.mode, new[] { "Fixed split", "Open center", "Locked center", "On demand", "Active bias", "Limited-slip center" }, 3);
            if (GUI.enabled && mode != data.mode) { data.mode = mode; changed = true; }
            GUI.enabled = enabled;
            if (data.mode == AwdMode.FixedSplit || data.mode == AwdMode.OpenCenter || data.mode == AwdMode.ActiveBias)
                changed |= Slider("Front torque bias (rear receives the remainder)", ref data.frontBias, data.mode == AwdMode.OpenCenter ? .05f : 0, data.mode == AwdMode.OpenCenter ? .95f : 1, "P0", defaults.frontBias);
            if (data.mode == AwdMode.LimitedSlipCenter) GUILayout.Label("Limited-slip center: torque redistribution bounded by installed bias ratio " + data.torqueBiasRatio.ToString("F1") + ":1 / preload " + SharedUnitSettings.Torque(data.centerPreloadNm, "F0") + ".");
            if (data.mode == AwdMode.ActiveBias)
            {
                changed |= Slider("Minimum front share", ref data.minimumFront, 0, data.maximumFront, "P0", defaults.minimumFront);
                changed |= Slider("Maximum front share", ref data.maximumFront, data.minimumFront, 1, "P0", defaults.maximumFront);
            }
            if (data.mode == AwdMode.OnDemand)
            {
                bool primary = GUILayout.Toggle(data.primaryFront, "Front-primary drive (unchecked = rear-primary)");
                if (primary != data.primaryFront) { data.primaryFront = primary; changed = true; }
                changed |= Slider("Maximum secondary axle share", ref data.maximumSecondary, 0, 1, "P0", defaults.maximumSecondary);
                changed |= Slider("Throttle preload of secondary coupling", ref data.preload, 0, 1, "P0", defaults.preload);
                changed |= Slider("Primary slip threshold", ref data.slipThreshold, .001f, .5f, "P1", defaults.slipThreshold);
            }
            if (data.mode == AwdMode.OnDemand || data.mode == AwdMode.ActiveBias)
                changed |= Slider("Coupling response (seconds)", ref data.responseSeconds, .01f, 2, "F2", defaults.responseSeconds);
            if (data.mode != AwdMode.LockedCenter)
            {
                bool isolate = GUILayout.Toggle(data.isolateFailedAxle, "Hardware can isolate a disconnected axle (enables surviving-axle fallback)");
                if (isolate != data.isolateFailedAxle) { data.isolateFailedAxle = isolate; changed = true; }
            }
            if (data.mode == AwdMode.OpenCenter)
            {
                changed |= Slider("Remaining disconnected shaft inertia (kg m²)", ref data.freeShaftInertiaKgM2, .005f, .5f, "F3", defaults.freeShaftInertiaKgM2);
                changed |= TorqueSlider("Free-shaft friction", ref data.freeShaftDragNm, 0, 10, "F2", defaults.freeShaftDragNm);
                changed |= TorqueSlider("Free-shaft viscous drag", ref data.freeShaftViscousDrag, 0, .05f, "F3", defaults.freeShaftViscousDrag);
            }
            GUILayout.Label(data.mode == AwdMode.OpenCenter ? "Open center: a free/broken branch limits both outputs unless isolated." : data.mode == AwdMode.FixedSplit ? "Controlled fixed split: each axle receives its configured share; loss of one does not increase the other unless isolated." : data.mode == AwdMode.LockedCenter ? "Locked center: shared axle speed; available torque follows grip, including surviving-axle drive." : data.mode == AwdMode.OnDemand ? "Primary axle drives first; slip/preload progressively engages the secondary axle." : "Active controller adjusts bias within its front-share limits in response to axle slip.");
            GUILayout.Label("These represent hardware/controller configurations for this prototype, not universal settings every real car offers.");
            if (changed) { tune.awd = data; tune.awdConfigured = true; }
            return changed;
        }
        static bool TorqueSlider(string name, ref float value, float min, float max, string format, float defaultValue)
        {
            float factor = (float)DisplayUnits.Torque(1, SharedUnitSettings.Current.torque), displayed = value * factor;
            string suffix = name.Contains("viscous") ? " per rad/s" : "";
            bool changed = Slider(name + " (" + SharedUnitSettings.TorqueLabel + suffix + ")", ref displayed, min * factor, max * factor, format, defaultValue * factor);
            if (changed) value = displayed / factor;
            return changed;
        }
        static bool Slider(string name, ref float value, float min, float max, string format, float defaultValue)
        {
            GUILayout.Label(name + ": " + value.ToString(format));
            return TuningSlider.Draw(ref value, min, max, defaultValue);
        }
    }
}
