using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragLab.Runtime
{
    internal sealed partial class RacingSurface
    {
        struct Sample { internal Vector2 point; internal Color color; internal Sample(Vector2 point,Color color) { this.point=point;this.color=color; } }
        readonly List<Vector2> contour=new List<Vector2>(40);
        readonly List<Sample> clipA=new List<Sample>(48),clipB=new List<Sample>(48);
        bool clipContour;
        bool UsesContour => kind!=Kind.Header && kind!=Kind.Footer && kind!=Kind.Metal && kind!=Kind.Navigation;
        internal float AntiAliasWidth => 1 / Mathf.Max(1,scaledPixelsPerPoint);
        internal bool UsesClippedContour => UsesContour;
        void MakeContour(float w,float h)
        {
            contour.Clear();
            float top=kind==Kind.Primary?5:1,bottom=kind==Kind.Primary?h-5:h-1;
            float lean=kind==Kind.Primary?18:kind==Kind.Tab||kind==Kind.FirstTab?10:kind==Kind.Action||kind==Kind.SecondaryAction?6:0;
            float left=kind==Kind.ChoiceMiddle||kind==Kind.ChoiceLast?0:1;
            float right=kind==Kind.ChoiceFirst||kind==Kind.ChoiceMiddle?w:w-1;
            var corners=new[] { new Vector2(left+(kind==Kind.FirstTab?0:lean),top),new Vector2(right,top),new Vector2(right-lean,bottom),new Vector2(left,bottom) };
            for(int i=0;i<4;i++)
            {
                float radius=kind==Kind.Panel?11:kind==Kind.Primary?1.8f:4;
                if(kind==Kind.ChoiceMiddle || kind==Kind.ChoiceFirst&&(i==1||i==2) || kind==Kind.ChoiceLast&&(i==0||i==3)) radius=0;
                Vector2 corner=corners[i],incoming=(corners[(i+3)%4]-corner).normalized,outgoing=(corners[(i+1)%4]-corner).normalized;
                if(radius==0) { contour.Add(corner);continue; }
                Vector2 a=corner+incoming*radius,b=corner+outgoing*radius;
                for(int step=0;step<=8;step++) { float t=step/8f; contour.Add((1-t)*(1-t)*a+2*(1-t)*t*corner+t*t*b); }
            }
        }
        void DrawContouredControl(float w,float h)
        {
            MakeContour(w,h);
            bool red=selected||kind==Kind.Primary||kind==Kind.Action;
            bool panel=kind==Kind.Panel;
            Color top=panel?C(.095f,.108f,.12f):red?C(.64f,.055f,.085f):C(.19f,.21f,.235f);
            Color bottom=panel?C(.05f,.06f,.072f):red?C(.27f,.018f,.034f):C(.075f,.085f,.10f);
            // Native alpha fringes cover both slanted and rounded edges in physical
            // pixels. All face/texture/shine layers use this same convex outline.
            Fan(contour,p => Color.Lerp(top,bottom,Mathf.Clamp01(p.y/h)));
            clipContour=true;
            uint seed=0x42c8391u;
            float spacing=panel?3.8f:2.1f;
            for(float x=-h;x<w;x+=spacing+Next(ref seed)*1.8f)
                Stroke(new Vector2(x,h),new Vector2(x+h*.75f,0),.4f,C(.82f,.86f,.9f,(panel?.014f:.026f)*(.5f+Next(ref seed))));
            if(!panel)
            {
                // A narrow bevel, quiet lower reflection and a single soft sweep.
                Face(0,1,w,Mathf.Min(5,h*.14f),C(1,1,1,.065f),C(1,1,1,0));
                Face(0,h-7,w,6,C(1,1,1,0),C(1,1,1,.022f));
                if(hovered||focused) Face(0,0,w,h,C(1,1,1,.045f));
                if(ShineActive)
                    for(int i=0;i<48;i++)
                    {
                        float u=i/48f,v=(i+1)/48f,center=Mathf.Lerp(-.3f,1.3f,ShineProgress);
                        float a=Mathf.Exp(-Mathf.Pow((u-center)/.10f,2))*.13f,b=Mathf.Exp(-Mathf.Pow((v-center)/.10f,2))*.13f;
                        float lean=kind==Kind.Primary?18:kind==Kind.Tab||kind==Kind.FirstTab?10:6;
                        Quad(new Vector2(u*w+lean,0),new Vector2(v*w+lean,0),new Vector2(v*w,h),new Vector2(u*w,h),C(1,1,1,a),C(1,1,1,b),C(1,1,1,b),C(1,1,1,a));
                    }
                if(pressed) Face(0,0,w,h,C(0,0,0,.16f));
            }
            clipContour=false;
            Border(w,h,red,panel);
        }
        Color BorderColor(Vector2 p,float w,float h,bool red,bool panel)
        {
            Color neutral=Color.Lerp(C(.48f,.51f,.55f,.9f),C(.19f,.22f,.26f,.9f),p.y/h);
            if(panel)
            {
                float corner=Mathf.Min(p.x+p.y,w-p.x+h-p.y);
                float accent=Mathf.Pow(Mathf.Clamp01(1-corner/150),.65f);
                return Color.Lerp(neutral,C(1,.08f,.13f),accent);
            }
            if(focused) return C(.98f,.65f,.68f);
            return red?Color.Lerp(C(1,.25f,.26f),C(.72f,.06f,.09f),p.y/h):neutral;
        }
        void Border(float w,float h,bool red,bool panel)
        {
            float edge=panel?1.6f:1;
            for(int i=0;i<contour.Count;i++)
            {
                int next=(i+1)%contour.Count; Vector2 a=contour[i],b=contour[next];
                Vector2 na=Normal(i),nb=Normal(next);
                Color ca=BorderColor(a,w,h,red,panel),cb=BorderColor(b,w,h,red,panel);
                RawQuad(a-na*edge,b-nb*edge,b,a,ca,cb,cb,ca);
                Color clearA=ca,clearB=cb;clearA.a=clearB.a=0;
                RawQuad(a,b,b+nb*AntiAliasWidth,a+na*AntiAliasWidth,ca,cb,clearB,clearA);
            }
        }
        Vector2 Normal(int i)
        {
            Vector2 a=contour[i]-contour[(i+contour.Count-1)%contour.Count],b=contour[(i+1)%contour.Count]-contour[i];
            Vector2 na=new Vector2(a.y,-a.x).normalized,nb=new Vector2(b.y,-b.x).normalized;
            Vector2 normal=(na+nb).normalized;return normal/Mathf.Max(.25f,Vector2.Dot(normal,nb));
        }
        void Fan(List<Vector2> polygon,Func<Vector2,Color> color)
        {
            Vector2 center=Vector2.zero;foreach(var point in polygon)center+=point;center/=polygon.Count;
            int first=vertices.Count;AddVertex(center,color(center));foreach(var point in polygon)AddVertex(point,color(point));
            for(int i=0;i<polygon.Count;i++) { indices.Add((ushort)first);indices.Add((ushort)(first+1+i));indices.Add((ushort)(first+1+(i+1)%polygon.Count)); }
        }
        static float Cross(Vector2 a,Vector2 b) => a.x*b.y-a.y*b.x;
        void ClipQuad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color ca,Color cb,Color cc,Color cd)
        {
            clipA.Clear();clipA.Add(new Sample(a,ca));clipA.Add(new Sample(b,cb));clipA.Add(new Sample(c,cc));clipA.Add(new Sample(d,cd));
            var input=clipA;var output=clipB;
            for(int edge=0;edge<contour.Count&&input.Count>0;edge++)
            {
                Vector2 start=contour[edge],line=contour[(edge+1)%contour.Count]-start;output.Clear();
                Sample previous=input[input.Count-1];float pd=Cross(line,previous.point-start);
                foreach(var current in input)
                {
                    float cdistance=Cross(line,current.point-start);
                    if((pd>=0)!=(cdistance>=0))
                    { float t=pd/(pd-cdistance);output.Add(new Sample(Vector2.Lerp(previous.point,current.point,t),Color.Lerp(previous.color,current.color,t))); }
                    if(cdistance>=0)output.Add(current);previous=current;pd=cdistance;
                }
                var swap=input;input=output;output=swap;
            }
            if(input.Count<3)return;
            // Stroke quads can arrive in either winding; UI meshes cull reversed faces.
            float area=0;for(int i=0;i<input.Count;i++)area+=Cross(input[i].point,input[(i+1)%input.Count].point);
            if(area<0)input.Reverse();
            int first=vertices.Count;foreach(var sample in input)AddVertex(sample.point,sample.color);
            for(int i=1;i<input.Count-1;i++) {indices.Add((ushort)first);indices.Add((ushort)(first+i));indices.Add((ushort)(first+i+1));}
        }
        void RawQuad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color ca,Color cb,Color cc,Color cd)
        { int first=vertices.Count;AddVertex(a,ca);AddVertex(b,cb);AddVertex(c,cc);AddVertex(d,cd);AddIndices(first,first+3,first+2,first+1); }
    }
}
