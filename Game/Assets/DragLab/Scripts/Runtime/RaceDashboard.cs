using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    // Static, deterministic grained vinyl. No gameplay RNG or per-frame texture work.
    internal sealed class RaceDashboard : GaugeInk
    {
        static Texture2D grain;
        internal int PaintCount { get; private set; }
        internal RaceDashboard()
        { name = "raceDashboard"; AddToClassList("race-dashboard"); generateVisualContent += Draw; }
        internal static float Top(float x, float width)
        {
            float center = Mathf.Min(268, width * .26f);
            return 48 - 30 * Mathf.Exp(-Mathf.Pow((x-center)/230,2)) - 9 * Mathf.Exp(-Mathf.Pow((x-width+130)/140,2));
        }
        static Texture2D Grain()
        {
            if (grain != null) return grain;
            const int width=1024,height=256;var pixels=new Color32[width*height];uint seed=0x7814b52u;
            for(int i=0;i<pixels.Length;i++)
            { seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;byte shade=(byte)(220+seed%36);pixels[i]=new Color32(shade,shade,shade,255); }
            grain=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Dashboard vinyl grain",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            grain.SetPixels32(pixels);grain.Apply(false,true);return grain;
        }
        void Draw(MeshGenerationContext context)
        {
            float w=contentRect.width,h=contentRect.height;if(w<2||h<2)return;PaintCount++;
            const int columns=128;var mesh=context.Allocate((columns+1)*2,columns*6,Grain());var uv=mesh.uvRegion;
            for(int i=0;i<=columns;i++)
            {
                float x=w*i/columns,y=Top(x,w);
                mesh.SetNextVertex(new Vertex{position=new Vector3(x,y,Vertex.nearZ),tint=new Color(.19f,.205f,.22f),uv=new Vector2(Mathf.Lerp(uv.xMin,uv.xMax,i/(float)columns),Mathf.Lerp(uv.yMax,uv.yMin,y/h))});
                mesh.SetNextVertex(new Vertex{position=new Vector3(x,h,Vertex.nearZ),tint=new Color(.045f,.05f,.06f),uv=new Vector2(Mathf.Lerp(uv.xMin,uv.xMax,i/(float)columns),uv.yMin)});
            }
            for(int i=0;i<columns;i++)foreach(int n in new[]{i*2,i*2+2,i*2+1,i*2+1,i*2+2,i*2+3})mesh.SetNextIndex((ushort)n);
            var p=context.painter2D;p.lineWidth=2;p.strokeColor=rim;p.BeginPath();p.MoveTo(new Vector2(0,Top(0,w)));
            for(int i=1;i<=columns;i++){float x=w*i/columns;p.LineTo(new Vector2(x,Top(x,w)));}p.Stroke();
            p.strokeColor=new Color(0,0,0,.75f);p.lineWidth=3;p.BeginPath();p.MoveTo(new Vector2(0,Top(0,w)+5));
            for(int i=1;i<=columns;i++){float x=w*i/columns;p.LineTo(new Vector2(x,Top(x,w)+5));}p.Stroke();
            // Small stitched seam, fixed geometry and a subdued red center-console accent.
            for(float x=550;x<w-270;x+=12)Line(p,new Vector2(x,76),new Vector2(x+5,76),1,new Color(.35f,.36f,.37f,.45f));
            Line(p,new Vector2(w-245,h-10),new Vector2(w-28,h-10),2,accent);
        }
    }
    internal sealed class RaceSteeringRim : GaugeInk
    {
        internal const float Radius = 270, Thickness = 48, LowerOffset = 260;
        internal RaceSteeringRim()
        { name = "raceSteeringRim"; AddToClassList("race-dashboard"); generateVisualContent += Draw; }
        void Draw(MeshGenerationContext context)
        {
            float w=contentRect.width,h=contentRect.height;if(w<2||h<2)return;
            var hub=new Vector2(Mathf.Min(270,w*.26f),h+LowerOffset);
            var p=context.painter2D;
            p.lineWidth=Thickness;p.strokeColor=new Color(.012f,.016f,.02f);p.BeginPath();
            p.Arc(hub,Radius,Angle.Degrees(180),Angle.Degrees(360));p.Stroke();
            p.lineWidth=2;p.strokeColor=new Color(.27f,.29f,.31f);p.BeginPath();
            p.Arc(hub,Radius+Thickness*.5f-2,Angle.Degrees(198),Angle.Degrees(342));p.Stroke();
        }
    }

}
