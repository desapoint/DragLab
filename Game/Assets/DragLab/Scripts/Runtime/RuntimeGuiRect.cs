using UnityEngine;
namespace DragLab.Runtime
{
    internal static class RuntimeGuiRect
    {
        static Vector2 rootMouse;
        public static void BeginFrame() { rootMouse = Event.current.mousePosition; }
        // GUIToScreenPoint alone has an offset in scaled, nested scroll groups.
        // Relate the rectangle to the same event's root pixel coordinates.
        public static Rect ToClient(Rect rect)
        {
            var position = GUIUtility.GUIToScreenPoint(rect.position) - GUIUtility.GUIToScreenPoint(Event.current.mousePosition) + rootMouse;
            return new Rect(position, rect.size * GUI.matrix.m00);
        }
    }
}
