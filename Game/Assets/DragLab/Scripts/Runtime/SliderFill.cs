using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed class SliderFill : VisualElement
    {
        internal SliderFill(Slider slider)
        {
            AddToClassList("slider-fill"); pickingMode = PickingMode.Ignore;
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
            generateVisualContent += context => {
                float fraction=Mathf.InverseLerp(slider.lowValue,slider.highValue,slider.value);float w=contentRect.width*(slider.inverted?1-fraction:fraction), h=contentRect.height;
                if (w <= 0 || h <= 0) return;
                var mesh = context.Allocate(4, 6);
                var top = RacingPalette.Shade(this,1,.04f); var bottom = RacingPalette.Shade(this,.72f);
                mesh.SetNextVertex(new Vertex { position = new Vector3(0, 0, Vertex.nearZ), tint = top });
                mesh.SetNextVertex(new Vertex { position = new Vector3(w, 0, Vertex.nearZ), tint = top });
                mesh.SetNextVertex(new Vertex { position = new Vector3(w, h, Vertex.nearZ), tint = bottom });
                mesh.SetNextVertex(new Vertex { position = new Vector3(0, h, Vertex.nearZ), tint = bottom });
                foreach (ushort index in new ushort[] { 0, 1, 2, 2, 3, 0 }) mesh.SetNextIndex(index);
            };
        }
    }
}
