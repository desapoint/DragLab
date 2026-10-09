using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public static class UnitSettingsView
    {
        internal static Rect SpeedRect, PressureRect, TorqueRect;
        static Rect LastScreenRect() { return RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect()); }
        public static void Draw()
        {
            var units = SharedUnitSettings.Current;
            GUILayout.Label("DISPLAY UNITS / shared by garage, races and test benches");
            var speed = (SpeedUnit)GUILayout.SelectionGrid((int)units.speed, new[] { "km/h", "mph", "m/s" }, 3);
            if (Event.current.type == EventType.Repaint) SpeedRect = LastScreenRect();
            var pressure = (PressureUnit)GUILayout.SelectionGrid((int)units.pressure, new[] { "bar", "psi", "kPa" }, 3);
            if (Event.current.type == EventType.Repaint) PressureRect = LastScreenRect();
            var torque = (TorqueUnit)GUILayout.SelectionGrid((int)units.torque, new[] { "Nm", "lb-ft" }, 2);
            if (Event.current.type == EventType.Repaint) TorqueRect = LastScreenRect();
            if (speed != units.speed || pressure != units.pressure || torque != units.torque) { SharedUnitSettings.Set(speed, pressure, torque); GUI.FocusControl(null); }
            GUILayout.Label("Boost always includes bar and psi. Tire outer radius is in inches, including the rubber.");
        }
    }
}
