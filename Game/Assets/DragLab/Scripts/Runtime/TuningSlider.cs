using UnityEngine;

namespace DragLab.Runtime
{
    // Share gestures across garage and bench configuration, without taking pedal ownership.
    public static class TuningSlider
    {
        static float dragStartX, dragStartValue;
        public const string Help = "Sliders: left-drag to adjust; hold right-click and drag for 10× finer adjustment; middle-click to restore the field default.";

        public static bool Draw(ref float value, float min, float max, float defaultValue)
        {
            var track = GUI.skin.horizontalSlider; var thumb = GUI.skin.horizontalSliderThumb;
            Rect rect = GUILayoutUtility.GetRect(0, Mathf.Max(18, Mathf.Max(track.fixedHeight, thumb.fixedHeight)), GUILayout.ExpandWidth(true));
            int id = GUIUtility.GetControlID("DragLabTuningSlider".GetHashCode(), FocusType.Passive, rect);
            var e = Event.current; var kind = e.GetTypeForControl(id);
            float next = value;
            if (GUI.enabled && kind == EventType.MouseDown && rect.Contains(e.mousePosition) && GUIUtility.hotControl == 0)
            {
                if (e.button == 2) { next = Mathf.Clamp(defaultValue, min, max); e.Use(); }
                else if (e.button == 1)
                {
                    GUIUtility.hotControl = id; GUIUtility.keyboardControl = 0;
                    dragStartX = e.mousePosition.x; dragStartValue = value; e.Use();
                }
            }
            if (GUIUtility.hotControl == id)
            {
                if (kind == EventType.MouseDrag && e.button == 1)
                {
                    if (GUI.enabled) next = Mathf.Clamp(dragStartValue + (e.mousePosition.x - dragStartX) * (max - min) / Mathf.Max(1, rect.width - thumb.fixedWidth) * .1f, min, max);
                    e.Use();
                }
                else if (kind == EventType.MouseUp && e.button == 1) { GUIUtility.hotControl = 0; e.Use(); }
            }
            // Consume special-button events before Unity's normal slider can page/jump.
            var raw = e.rawType;
            bool priorChange = GUI.changed; GUI.changed = false;
            float ordinary = GUI.HorizontalSlider(rect, next, min, max);
            bool ordinaryEdit = GUI.changed && GUI.enabled && e.button == 0 &&
                (raw == EventType.MouseDown || raw == EventType.MouseDrag || raw == EventType.MouseUp || raw == EventType.KeyDown);
            GUI.changed |= priorChange;
            if (ordinaryEdit) next = ordinary;
            if (next == value) return false;
            value = next; GUI.changed = true; return true;
        }
    }
}
