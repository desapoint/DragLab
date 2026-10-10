using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal static class RacingPalette
    {
        internal static readonly CustomStyleProperty<Color> AccentProperty = new CustomStyleProperty<Color>("--racing-accent");
        internal static readonly CustomStyleProperty<Color> MetalTop = new CustomStyleProperty<Color>("--racing-metal-top");
        internal static readonly CustomStyleProperty<Color> MetalBottom = new CustomStyleProperty<Color>("--racing-metal-bottom");
        internal static readonly CustomStyleProperty<Color> PanelTop = new CustomStyleProperty<Color>("--racing-panel-top");
        internal static readonly CustomStyleProperty<Color> PanelBottom = new CustomStyleProperty<Color>("--racing-panel-bottom");
        internal static bool TryRead(VisualElement element, CustomStyleProperty<Color> property, out Color value)
        {
            for(var current=element;current!=null;current=current.parent)
                if(current.customStyle.TryGetValue(property,out value))return true;
            value=default;return false;
        }
        internal static Color Read(VisualElement element, CustomStyleProperty<Color> property, Color fallback)
            => TryRead(element,property,out var value) ? value : fallback;
        internal static Color Accent(VisualElement element) => Read(element, AccentProperty, new Color32(245,34,53,255));
        internal static Color Shade(VisualElement element, float brightness=1, float white=0, float alpha=1)
        {
            Color value=Color.Lerp(Accent(element)*brightness,Color.white,white);value.a=alpha;return value;
        }
    }
}
