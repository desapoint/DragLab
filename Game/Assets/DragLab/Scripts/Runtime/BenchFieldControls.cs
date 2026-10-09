using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    // A small runtime debug inspector, so new public sound parameters remain testable.
    public sealed class BenchFieldControls
    {
        readonly Dictionary<Type, FieldInfo[]> cachedFields = new Dictionary<Type, FieldInfo[]>();
        readonly Dictionary<string, string> edits = new Dictionary<string, string>();
        internal readonly Dictionary<string, Rect> verificationSliders = new Dictionary<string, Rect>();
        internal readonly Dictionary<string, Rect> verificationControls = new Dictionary<string, Rect>();
        void RecordControl(string key)
        {
            if (Event.current.type != EventType.Repaint) return;
            var rect = GUILayoutUtility.GetLastRect();
            verificationControls[key] = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size * GUI.matrix.m00);
        }
        public void Clear() { edits.Clear(); }
        public bool HasTextFocus(string name) { return !string.IsNullOrEmpty(name) && edits.ContainsKey(name); }
        static bool EditingEvent()
        {
            if (!GUI.enabled) return false;
            var type = Event.current.rawType;
            return type == EventType.MouseDown || type == EventType.MouseDrag || type == EventType.MouseUp || type == EventType.KeyDown;
        }
        public bool Draw(object target, string path, params string[] skip)
        {
            if (target == null) return false;
            var type = target.GetType();
            if (!cachedFields.TryGetValue(type, out var fields)) { fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance); cachedFields[type] = fields; }
            bool dirty = false;
            foreach (var field in fields)
            {
                if (field.IsInitOnly || field.Name == "version" || Array.IndexOf(skip, field.Name) >= 0) continue;
                if (target is Tune && field.Name.StartsWith("part", StringComparison.Ordinal)) continue; // Derived installed-hardware projection is not an engineering knob.
                object value = field.GetValue(target); string key = path + "." + field.Name;
                bool canEdit = EditingEvent();
                if (field.FieldType == typeof(bool))
                { bool next = GUILayout.Toggle((bool)value, field.Name); RecordControl(key); if (canEdit && next != (bool)value) { field.SetValue(target, next); dirty = true; } }
                else if (field.FieldType.IsEnum)
                {
                    GUILayout.Label(field.Name == "manualLayout" ? "Manual gearbox selector hardware" : field.Name);
                    string[] choices = field.FieldType == typeof(ManualGearboxLayout) ? new[] { "H-pattern", "Sequential" } : Enum.GetNames(field.FieldType);
                    int next = GUILayout.SelectionGrid(Convert.ToInt32(value), choices, 3);
                    RecordControl(key);
                    if (canEdit && next != Convert.ToInt32(value)) { field.SetValue(target, Enum.ToObject(field.FieldType, next)); dirty = true; }
                }
                else if (field.FieldType == typeof(float) || field.FieldType == typeof(double) || field.FieldType == typeof(int))
                {
                    float current = Convert.ToSingle(value); Bounds(field.Name, current, out float low, out float high);
                    if (target is FlywheelSpec && field.Name == "massKg") { low = .5f; high = 35; }
                    float factor = 1; string label = field.Name; var units = SharedUnitSettings.Current;
                    if (field.Name.EndsWith("Bar", StringComparison.OrdinalIgnoreCase)) { factor = (float)DisplayUnits.Pressure(1, units.pressure); label += " (" + DisplayUnits.PressureLabel(units.pressure) + ")"; }
                    if (field.Name == "tirePressurePsi") { factor = (float)DisplayUnits.Pressure(1 / DisplayUnits.PsiPerBar, units.pressure); label = "Tire pressure (" + DisplayUnits.PressureLabel(units.pressure) + ")"; }
                    if (field.Name == "roadSpeedKmh") { factor = (float)DisplayUnits.Speed(1 / 3.6, units.speed); label = "Roller speed (" + DisplayUnits.SpeedLabel(units.speed) + ")"; }
                    if (field.Name == "converterLockSpeedMps") { factor = (float)DisplayUnits.Speed(1, units.speed); label = "Converter lock speed (" + DisplayUnits.SpeedLabel(units.speed) + ")"; }
                    if (field.Name == "wheelRadiusM") { factor = (float)(1 / DisplayUnits.MetresPerInch); label = "Outer tire radius (in)"; }
                    if (field.Name.EndsWith("Nm", StringComparison.OrdinalIgnoreCase)) { factor = (float)DisplayUnits.Torque(1, units.torque); label = field.Name.Substring(0, field.Name.Length - 2) + " (" + DisplayUnits.TorqueLabel(units.torque) + ")"; }
                    float displayed = current * factor;
                    float next = Number(label, key, displayed, low * factor, high * factor, field.FieldType == typeof(int));
                    if (next != displayed)
                    {
                        next /= factor;
                        object boxed = field.FieldType == typeof(int) ? (object)(int)next : field.FieldType == typeof(double) ? (object)(double)next : next;
                        field.SetValue(target, boxed); dirty = true;
                    }
                }
                else if (value is float[] ratios) dirty |= DrawArray(ratios, key, .2f, 6);
                else if (value is TorquePoint[] points)
                {
                    GUILayout.Label("Torque curve samples: RPM / " + SharedUnitSettings.TorqueLabel);
                    for (int i = 0; i < points.Length; i++)
                    {
                        float rpm = Number("Point " + i + " RPM", key + i + "rpm", points[i].rpm, 0, 14000, false);
                        float factor = (float)DisplayUnits.Torque(1, SharedUnitSettings.Current.torque);
                        float shown = points[i].torqueNm * factor;
                        float torque = Number("Point " + i + " " + SharedUnitSettings.TorqueLabel, key + i + "nm", shown, 0, 3000 * factor, false);
                        torque = torque == shown ? points[i].torqueNm : torque / factor;
                        if (rpm != points[i].rpm || torque != points[i].torqueNm) { points[i] = new TorquePoint(rpm, torque); dirty = true; }
                    }
                }
            }
            return dirty;
        }
        public bool DrawArray(float[] values, string name, float low, float high)
        {
            bool dirty = false;
            for (int i = 0; i < values.Length; i++)
            { float next = Number(name + " " + (i + 1), name + i, values[i], low, high, false); if (next != values[i]) { values[i] = next; dirty = true; } }
            return dirty;
        }
        float Number(string label, string key, float current, float low, float high, bool integer)
        {
            float applied = current; bool canEdit = EditingEvent();
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(285));
            if (!edits.TryGetValue(key, out string text) || GUI.GetNameOfFocusedControl() != key) text = current.ToString("G6", CultureInfo.InvariantCulture);
            GUI.SetNextControlName(key);
            string entered = GUILayout.TextField(text, GUILayout.Width(150));
            RecordControl(key + ":text");
            GUILayout.EndHorizontal();
            bool edited = entered != text, validTextEdit = false;
            if (edited && float.TryParse(entered, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) && !float.IsNaN(number) && !float.IsInfinity(number))
            { current = integer ? Mathf.Round(number) : number; validTextEdit = true; }
            edits[key] = entered;
            // Keep exact typed values available even beyond the convenient slider span;
            // engineering validation decides whether a draft can be applied.
            bool previousChange = GUI.changed; GUI.changed = false;
            float next = GUILayout.HorizontalSlider(current, Mathf.Min(low, current), Mathf.Max(high, current));
            if (Event.current.type == EventType.Repaint)
            {
                var rect = GUILayoutUtility.GetLastRect();
                verificationSliders[key] = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size * GUI.matrix.m00);
            }
            bool sliderEdit = GUI.changed; GUI.changed |= previousChange;
            if (integer) next = Mathf.Round(next);
            if (next != current) edits[key] = next.ToString("G6", CultureInfo.InvariantCulture);
            // Layout/repaint and disabled feedback are observations, never input.
            // Slider round-trips may differ by a float ULP at some view sizes.
            return canEdit && (validTextEdit || sliderEdit) ? next : applied;
        }
        static void Bounds(string name, float value, out float low, out float high)
        {
            low = 0; high = Mathf.Max(1, value * 2);
            if (name.IndexOf("Rpm", StringComparison.OrdinalIgnoreCase) >= 0) { low = 0; high = 14000; }
            if (name.IndexOf("Gain", StringComparison.OrdinalIgnoreCase) >= 0) high = 2;
            if (name.EndsWith("Seconds")) high = Mathf.Max(4, value * 2);
            if (name.EndsWith("Ratio")) { low = .1f; high = Mathf.Max(6, value * 2); }
            switch (name)
            {
                case "sequentialShiftScale": low = .1f; high = .95f; break;
                case "cylinders": low = 1; high = 16; break;
                case "engineLevel": case "tireLevel": case "weightLevel": case "gearboxLevel": case "exhaustLevel": case "exhaust": high = 3; break;
                case "controlLevel": case "amount": case "pulseSoftening": case "throttle": case "clutch": case "brake": case "load": case "volume": high = 1; break;
                case "warmthOverride": case "damageOverride": low = -1; high = 1; break;
                case "gear": high = 8; break;
                case "roadSpeedKmh": high = 500; break;
                case "tireSlip": high = 2; break;
                case "displacementLitres": low = .3f; high = 12; break;
                case "massKg": low = 400; high = 4000; break;
                case "assemblyMassKg": low = 5; high = 100; break;
                case "flywheelMass": low = .25f; high = 2.5f; break;
                case "outerRadiusM": low = .05f; high = .3f; break;
                case "innerRadiusM": low = 0; high = .15f; break;
                case "exhaustPopSharpness": low = .25f; high = 2.5f; break;
                case "exhaustPopDecay": low = .25f; high = 3; break;
                case "idleBoostDb": low = -6; high = 12; break;
                case "attenuationDb": high = 30; break;
                case "lowPassHz": low = 100; high = 18000; break;
                case "resonanceHz": low = 40; high = 5000; break;
                case "resonanceGainDb": low = -24; high = 12; break;
                case "resonanceQ": low = .25f; high = 8; break;
                case "launchCutFrequencyHz": low = 8; high = 80; break;
                case "warmupSeconds": low = 1; high = 300; break;
                case "safeRpmMultiplier": low = 1.01f; high = 1.4f; break;
                case "ambientTemperatureK": low = 250; high = 330; break;
                case "ambientPressureBar": low = .6f; high = 1.2f; break;
            }
        }
    }
}
