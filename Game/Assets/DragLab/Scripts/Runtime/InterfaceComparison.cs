using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragLab.Runtime
{
    // Presentation only. Session preferences never enter a player save or test config.
    internal sealed class InterfaceComparison
    {
        static readonly HashSet<string> replacementRoutes = new HashSet<string>();
        bool shortcutHeld;
        GUIStyle badgeStyle, emptyStyle;
        internal static bool IsReplacement(string route) => replacementRoutes.Contains(route);
        internal void ReleaseShortcut() { shortcutHeld = false; }

        internal bool HandleKey(string route, EventType type, KeyCode key, bool modified, bool blocked)
        {
            if (key != KeyCode.F8) return false;
            if (type == EventType.KeyUp) { shortcutHeld = false; return true; }
            if (type != EventType.KeyDown || modified) return false;
            if (!shortcutHeld && !blocked)
            {
                if (!replacementRoutes.Remove(route)) replacementRoutes.Add(route);
            }
            shortcutHeld = true; // OS repeat cannot toggle repeatedly or bypass a focus guard.
            return true;
        }

        static bool TextFocused()
        {
            if (GUIUtility.keyboardControl == 0) return false;
            if (!string.IsNullOrEmpty(GUI.GetNameOfFocusedControl())) return true;
            // Existing unnamed text fields also need protection. Do not create state for buttons.
            try { return GUIUtility.QueryStateObject(typeof(TextEditor), GUIUtility.keyboardControl) is TextEditor; }
            catch (ArgumentException) { return false; }
            catch (InvalidCastException) { return false; }
        }

        internal bool BeginGUI(string route, bool blocked = false)
        {
            var evt = Event.current;
            bool before = IsReplacement(route);
            if (HandleKey(route, evt.type, evt.keyCode, evt.shift || evt.control || evt.alt || evt.command,
                blocked || GUIUtility.hotControl != 0 || GUIUtility.hasModalWindow || TextFocused()))
                evt.Use();
            if (before != IsReplacement(route))
            {
                GUI.FocusControl(null);
                GUIUtility.ExitGUI(); // Rebuild the IMGUI layout on its next Layout event.
            }
            return IsReplacement(route);
        }

        internal void DrawStatus(string route)
        {
            if (Event.current.type != EventType.Repaint) return;
            var matrix = GUI.matrix; var color = GUI.color; var enabled = GUI.enabled;
            GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; GUI.enabled = true;
            if (badgeStyle == null)
            {
                badgeStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                emptyStyle = new GUIStyle(GUI.skin.box) { fontSize = 19, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }
            bool replacement = IsReplacement(route);
            float width = Mathf.Min(390, Screen.width - 16);
            GUI.Box(new Rect(Screen.width - width - 8, 4, width, 34),
                (replacement ? "NEW / EMPTY" : "OLD") + " · " + route + " · F8 compare", badgeStyle);
            if (replacement)
            {
                float panel = Mathf.Min(540, Screen.width - 32);
                GUI.Box(new Rect((Screen.width - panel) / 2, Mathf.Max(48, Screen.height / 2 - 60), panel, 120),
                    "Replacement interface is empty\nF8 returns to the existing interface.\nGame and bench state continue unchanged.", emptyStyle);
            }
            GUI.matrix = matrix; GUI.color = color; GUI.enabled = enabled;
        }
    }
}
