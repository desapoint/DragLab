using System;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Infrastructure
{
    public static class SharedUnitSettings
    {
        static UnitPreferences preferences;
#if UNITY_EDITOR
        public static string VerificationPath;
#endif
        public static string FilePath
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(VerificationPath)) return VerificationPath;
#endif
                return Path.Combine(Path.GetDirectoryName(SharedAudioSettings.FilePath), "unit-settings.json");
            }
        }
        public static UnitPreferences Current
        {
            get
            {
                if (preferences != null) return preferences;
                preferences = new UnitPreferences();
                try { if (File.Exists(FilePath)) { var data = JsonUtility.FromJson<UnitPreferences>(File.ReadAllText(FilePath)); if (data != null && data.version == 1) preferences = data; } } catch (Exception) { }
                preferences.Sanitize(); return preferences;
            }
        }
        public static void Set(SpeedUnit speed, PressureUnit pressure) { Set(speed, pressure, Current.torque); }
        public static void Set(SpeedUnit speed, PressureUnit pressure, TorqueUnit torque)
        {
            var next = new UnitPreferences { speed = speed, pressure = pressure, torque = torque }; next.Sanitize();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)); string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(next, true));
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath);
                preferences = next;
            }
            catch (Exception e) { Debug.LogWarning("Unit settings could not be saved: " + e.Message); }
        }
        public static string Torque(double nm, string format = "F0") { return DisplayUnits.FormatTorque(nm, Current, format); }
        public static string TorqueLabel { get { return DisplayUnits.TorqueLabel(Current.torque); } }
        // Format embedded SI measurements in authored part descriptions and simulation warnings.
        // The source text and exported records remain untouched.
        public static string TorqueText(string text)
        {
            if (string.IsNullOrEmpty(text) || Current.torque == TorqueUnit.NewtonMetres) return text;
            return Regex.Replace(text, @"(?<![\w.,])(-?\d+(?:[.,]\d+)?)\s+Nm\b", match => {
                double value;
                if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
                    !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return match.Value;
                return Torque(value, "0.##");
            });
        }
        public static string Speed(double mps, string format = "F1") { return DisplayUnits.FormatSpeed(mps, Current, format); }
        public static string Pressure(double bar) { return DisplayUnits.FormatPressure(bar, Current); }
        public static string Boost(double bar) { return DisplayUnits.Boost(bar, Current); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] public static void Reload() { preferences = null; }
    }
}
