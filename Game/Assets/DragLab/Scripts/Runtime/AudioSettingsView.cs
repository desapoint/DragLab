using UnityEngine;
using DragLab.Infrastructure;
using DragLab.Presentation;

namespace DragLab.Runtime
{
    public static class AudioSettingsView
    {
        static GUIStyle wrapped;
        internal static Rect VerificationSliderRect;
        public static void Draw(EngineAudio audio = null)
        {
            // A slider consumes MouseUp. Remember it before drawing so the final
            // drag value is saved, rather than only living until a reload.
            var inputType = Event.current.rawType;
            if (wrapped == null) wrapped = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label("AUDIO SETTINGS");
            GUILayout.Label("Master volume / " + SharedAudioSettings.MasterVolume.ToString("P0"));
            float current = SharedAudioSettings.MasterVolume;
            bool previous = GUI.changed; GUI.changed = false;
            float value = GUILayout.HorizontalSlider(current, 0, 1);
            if (Event.current.type == EventType.Repaint)
            {
                var rect = GUILayoutUtility.GetLastRect();
                VerificationSliderRect = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size * GUI.matrix.m00);
            }
            bool changed = GUI.changed; GUI.changed |= previous;
            if (GUI.enabled && value != current && (changed || inputType == EventType.MouseDown || inputType == EventType.MouseDrag || inputType == EventType.KeyDown)) SharedAudioSettings.SetMasterVolume(value, false);
            if (inputType == EventType.MouseUp || inputType == EventType.KeyUp) SharedAudioSettings.Save();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("25%")) SharedAudioSettings.SetMasterVolume(.25f);
            if (GUILayout.Button("50%")) SharedAudioSettings.SetMasterVolume(.5f);
            if (GUILayout.Button("75%")) SharedAudioSettings.SetMasterVolume(.75f);
            if (GUILayout.Button("100%")) SharedAudioSettings.SetMasterVolume(1);
            GUILayout.EndHorizontal();
            GUILayout.Label("One saved setting for racing, engine tests and the dyno concert. 0% mutes all sound.", wrapped);
            GUILayout.Label("Engine, exhaust and accessory gains remain available in the bench Mix tab for sound design.", wrapped);
            if (audio != null) GUILayout.Label("Generated output: " + (audio.OutputRms > .000001f ? (20 * Mathf.Log10(audio.OutputRms)).ToString("F1") + " dBFS RMS" : "silent"), wrapped);
            GUILayout.Label(SharedAudioSettings.Status, wrapped);
        }
    }
}
