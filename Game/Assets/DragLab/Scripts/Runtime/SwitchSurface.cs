using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    // The native Toggle keeps input/focus semantics; this child only paints it.
    internal sealed class SwitchSurface : VisualElement
    {
        const float Duration=.18f;
        float progress,from,target,started;
        bool initialized,wasAnimating;
        readonly Toggle toggle;
        internal float Progress => progress;
        internal bool Animating => initialized && progress!=target;
        internal int PaintCount {get;private set;}
        internal SwitchSurface(Toggle toggle)
        {
            this.toggle=toggle;pickingMode=PickingMode.Ignore;AddToClassList("switch-surface");generateVisualContent+=Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_=>MarkDirtyRepaint());
            toggle.RegisterCallback<FocusInEvent>(_=>MarkDirtyRepaint());toggle.RegisterCallback<FocusOutEvent>(_=>MarkDirtyRepaint());
        }
        internal void SetValue(bool value)
        {
            float next=value?1:0;
            if(!initialized) {initialized=true;progress=target=from=next;MarkDirtyRepaint();return;}
            if(next==target)return;
            Advance();from=progress;target=next;started=Time.realtimeSinceStartup;MarkDirtyRepaint();
        }
        void Advance()
        {float t=Mathf.Clamp01((Time.realtimeSinceStartup-started)/Duration);progress=Mathf.Lerp(from,target,t*t*(3-2*t));if(t>=1)progress=target;}
        internal void Tick(bool visible)
        {
            if(!visible)return;
            bool animate=Animating;if(animate)Advance();if(animate||wasAnimating)MarkDirtyRepaint();wasAnimating=animate;
        }
        void Draw(MeshGenerationContext context)
        {
            float w=contentRect.width,h=contentRect.height;if(w<1||h<1)return;PaintCount++;
            var p=context.painter2D;
            Color dark=new Color(.10f,.13f,.16f),red=RacingPalette.Shade(this,.92f);
            Capsule(p,new Rect(1,1,w-2,h-2),Color.Lerp(new Color(.31f,.35f,.39f),RacingPalette.Shade(this,1,.08f),progress));
            Capsule(p,new Rect(2,2,w-4,h-4),Color.Lerp(dark,red,progress));
            p.strokeColor=Color.Lerp(new Color(.6f,.65f,.7f,.25f),RacingPalette.Shade(this,1,.35f,.4f),progress);p.lineWidth=1;
            p.BeginPath();p.MoveTo(new Vector2(h*.5f,3));p.LineTo(new Vector2(w-h*.5f,3));p.Stroke();
            float radius=h*.5f-4,x=Mathf.Lerp(h*.5f,w-h*.5f,progress),y=h*.5f;
            Circle(p,x,y+1,radius+1,new Color(0,0,0,.45f));
            Circle(p,x,y,radius,new Color(.82f,.85f,.88f));Circle(p,x,y-.65f,radius-1,new Color(.96f,.97f,.985f));
            p.strokeColor=new Color(1,1,1,.85f);p.lineWidth=.8f;p.BeginPath();p.Arc(new Vector2(x,y),radius-1,Angle.Degrees(200),Angle.Degrees(330));p.Stroke();
            if(toggle.focusController?.focusedElement==toggle) { p.strokeColor=RacingPalette.Shade(this,1,.6f);p.lineWidth=1;p.BeginPath();p.MoveTo(new Vector2(h*.5f,h-1));p.LineTo(new Vector2(w-h*.5f,h-1));p.Stroke(); }
        }
        static void Capsule(Painter2D p,Rect r,Color color)
        {
            float radius=r.height*.5f;p.fillColor=color;p.BeginPath();p.MoveTo(new Vector2(r.x+radius,r.y));p.LineTo(new Vector2(r.xMax-radius,r.y));
            p.Arc(new Vector2(r.xMax-radius,r.center.y),radius,Angle.Degrees(270),Angle.Degrees(450));p.LineTo(new Vector2(r.x+radius,r.yMax));
            p.Arc(new Vector2(r.x+radius,r.center.y),radius,Angle.Degrees(90),Angle.Degrees(270));p.ClosePath();p.Fill();
        }
        static void Circle(Painter2D p,float x,float y,float radius,Color color)
        {p.fillColor=color;p.BeginPath();p.Arc(new Vector2(x,y),radius,Angle.Degrees(0),Angle.Degrees(360));p.ClosePath();p.Fill();}
    }
}
