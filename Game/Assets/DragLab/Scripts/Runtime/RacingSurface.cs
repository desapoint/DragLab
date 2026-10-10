using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    // Tessellated cel shading; decorative geometry never owns clicks or drive input.
    internal sealed partial class RacingSurface : VisualElement
    {
        internal enum Kind { Header, Footer, Metal, Navigation, Button, Primary, Tab, FirstTab, Action, SecondaryAction, ChoiceFirst, ChoiceMiddle, ChoiceLast, Panel }
        const float SweepSeconds = .65f;
        readonly VisualElement owner;
        readonly Kind kind;
        readonly List<Vertex> vertices = new List<Vertex>(1024);
        readonly List<ushort> indices = new List<ushort>(4096);
        static Texture2D dither;
        bool hovered, focused, pressed, selected, wasAnimating;
        float sweepStarted = float.NegativeInfinity;
        internal bool ShineActive => Time.realtimeSinceStartup - sweepStarted < SweepSeconds;
        internal float ShineProgress => Mathf.Clamp01((Time.realtimeSinceStartup - sweepStarted) / SweepSeconds);
        internal int PaintCount { get; private set; }

        internal RacingSurface(VisualElement owner, Kind kind)
        {
            this.owner = owner; this.kind = kind;
            pickingMode = PickingMode.Ignore;
            AddToClassList("racing-surface");
            generateVisualContent += Draw;
            if (kind == Kind.Header || kind == Kind.Footer || kind == Kind.Metal || kind == Kind.Panel) return;
            owner.RegisterCallback<PointerEnterEvent>(evt => { if (evt.target != owner || hovered) return; hovered = true; StartShine(); });
            owner.RegisterCallback<PointerLeaveEvent>(evt => { if (evt.target != owner) return; hovered = false; pressed = false; MarkDirtyRepaint(); });
            owner.RegisterCallback<FocusInEvent>(evt => { if (evt.target != owner || focused) return; focused = true; if (!hovered) StartShine(); });
            owner.RegisterCallback<FocusOutEvent>(evt => { if (evt.target != owner) return; focused = false; MarkDirtyRepaint(); });
            owner.RegisterCallback<PointerDownEvent>(_ => { pressed = true; MarkDirtyRepaint(); });
            owner.RegisterCallback<PointerUpEvent>(_ => { pressed = false; MarkDirtyRepaint(); });
        }
        void StartShine() { if (ShineActive) return; sweepStarted = Time.realtimeSinceStartup; MarkDirtyRepaint(); }
        internal void Tick(bool visible)
        {
            // Static/hidden surfaces stay cached; there is no perpetual animation timer.
            if (!visible) return;
            bool red = owner.ClassListContains("selected");
            bool animated = ShineActive;
            if (red != selected || animated || wasAnimating) MarkDirtyRepaint();
            selected = red; wasAnimating = animated;
        }
        void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 1 || h < 1) return;
            PaintCount++;
            vertices.Clear(); indices.Clear();
            bool lit = hovered || focused;
            if (UsesContour) DrawContouredControl(w,h);
            else if (kind == Kind.Metal)
            {
                Metal(w,h);
            }
            else if (kind == Kind.Header || kind == Kind.Footer)
            {
                Face(0,0,w,h,C(.145f,.155f,.17f),C(.055f,.065f,.078f));
                for (float x = -h; x < w; x += 6)
                    Slant(x,0,.7f,h,C(1,1,1,.025f),-h);
                Face(0,0,w,1,C(1,1,1,.10f));
                if (kind == Kind.Header) HeaderTrim(w,h); else FooterTrim(w,h);
            }
            else
            {
                if (selected || lit)
                {
                    // Vertex alpha gives a circular, soft light from below the active menu.
                    Color light = selected ? C(.86f,.055f,.12f,.72f) : C(.7f,.75f,.82f,.16f);
                    Radial(w,h,light);
                    if (selected)
                        Radial(w,h,C(1,.22f,.28f,.28f),Mathf.Max(w*.38f,h*.6f));
                    if (selected)
                        for (int i = 0; i < 24; i++)
                        {
                            float x1 = i*w/24, x2 = (i+1)*w/24;
                            Color left = C(.95f,.22f,.27f,Mathf.Sin(i*Mathf.PI/24)*.9f);
                            Color right = C(.95f,.22f,.27f,Mathf.Sin((i+1)*Mathf.PI/24)*.9f);
                            Quad(new Vector2(x1,h-2),new Vector2(x2,h-2),new Vector2(x2,h),new Vector2(x1,h),left,right,right,left);
                        }
                }
                Grain(w,h,Mathf.Min(2400,(int)(w*h/6)),0x23df917u,.032f);
                if (ShineActive)
                {
                    // Existing navigation sweep; contoured controls use their own clipped mesh.
                    for(int i=0;i<64;i++)
                    {
                        float u=i/64f, v=(i+1)/64f;
                        float center=Mathf.Lerp(-.3f,1.3f,ShineProgress);
                        float a=Mathf.Exp(-Mathf.Pow((u-center)/.11f,2))*.14f;
                        float b=Mathf.Exp(-Mathf.Pow((v-center)/.11f,2))*.14f;
                        Quad(new Vector2(u*w,0),new Vector2(v*w,0),new Vector2(v*w,h),new Vector2(u*w,h),C(1,1,1,a),C(1,1,1,b),C(1,1,1,b),C(1,1,1,a));
                    }
                }
                if (pressed) Face(0,0,w,h,C(0,0,0,.22f));
            }
            var mesh = context.Allocate(vertices.Count,indices.Count);
            foreach (var vertex in vertices) mesh.SetNextVertex(vertex);
            foreach (var index in indices) mesh.SetNextIndex(index);
            if (!UsesContour && kind != Kind.Header && kind != Kind.Footer) DrawDither(context,w,h);
        }
        void DrawDither(MeshGenerationContext context,float w,float h)
        {
            // Point-sampled, stable, sub-2% luminance noise breaks up 8-bit colour
            // banding after interpolation. No animated noise or gameplay RNG.
            if (dither == null)
            {
                const int size=512; var pixels=new Color32[size*size]; uint seed=0x83bc94u;
                for(int i=0;i<pixels.Length;i++)
                { byte tone=Next(ref seed)>.5f?(byte)255:(byte)0; pixels[i]=new Color32(tone,tone,tone,(byte)(1+Next(ref seed)*3)); }
                dither=new Texture2D(size,size,TextureFormat.RGBA32,false) { name="Racing surface dither",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave };
                dither.SetPixels32(pixels); dither.Apply(false,true);
            }
            var mesh=context.Allocate(4,6,dither); Rect uv=mesh.uvRegion;
            var a=new Vector2(0,0);var b=new Vector2(w,0);var c=new Vector2(w,h);var d=new Vector2(0,h);
            mesh.SetNextVertex(new Vertex {position=new Vector3(a.x,a.y,Vertex.nearZ),tint=Color.white,uv=new Vector2(uv.xMin,uv.yMax)});
            mesh.SetNextVertex(new Vertex {position=new Vector3(b.x,b.y,Vertex.nearZ),tint=Color.white,uv=new Vector2(uv.xMax,uv.yMax)});
            mesh.SetNextVertex(new Vertex {position=new Vector3(c.x,c.y,Vertex.nearZ),tint=Color.white,uv=new Vector2(uv.xMax,uv.yMin)});
            mesh.SetNextVertex(new Vertex {position=new Vector3(d.x,d.y,Vertex.nearZ),tint=Color.white,uv=new Vector2(uv.xMin,uv.yMin)});
            foreach(ushort index in new ushort[]{0,1,2,2,3,0})mesh.SetNextIndex(index);
        }
        void HeaderTrim(float w,float h)
        {
            float end = Mathf.Min(310,w*.24f);
            Face(0,h-1,w,1,C(.38f,.40f,.43f,.35f));
            FadeLine(0,h-2,end,1.5f,false);
            FadeLine(w-end,h-2,end,1.5f,true);

        }
        void FooterTrim(float w,float h)
        {
            Face(0,0,w,8,C(.065f,.08f,.095f));
            Face(26,7,w-52,1,C(.43f,.47f,.52f,.5f));
            FadeLine(28,7,w*.42f-28,1.5f,false);
            FadeLine(w*.58f,7,w*.42f-28,1.5f,true);
            for (int side = 0; side < 2; side++)
            {
                System.Func<float,float,Vector2> point = (x,y) => new Vector2(side==0?x:w-x,y);
                Stroke(point(0,0),point(17,0),1,C(.48f,.5f,.54f,.6f));
                Stroke(point(17,0),point(28,8),1.5f,C(.8f,.095f,.14f));
                Stroke(point(4,3),point(11,8),1.4f,C(.45f,.1f,.14f));
                Stroke(point(12,3),point(19,8),1.4f,C(.45f,.1f,.14f));
            }
        }
        void FadeLine(float x,float y,float w,float h,bool reverse)
        {
            Color red=C(.92f,.09f,.14f,.88f), clear=C(.92f,.09f,.14f,0);
            Quad(new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h),reverse?clear:red,reverse?red:clear,reverse?red:clear,reverse?clear:red);
        }
        void Metal(float w,float h)
        {
            const int cols = 32, rows = 20;
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= cols; x++)
                {
                    float u = x/(float)cols,v = y/(float)rows;
                    float light = Mathf.Exp(-((u-.43f)*(u-.43f)*3+(v-.23f)*(v-.23f)*2));
                    float diagonal = Mathf.Sin((u*1.4f+v)*12)*.0025f;
                    AddVertex(new Vector2(u*w,v*h),C(.038f+light*.055f+diagonal,.049f+light*.061f+diagonal,.062f+light*.071f+diagonal));
                }
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                { int a=y*(cols+1)+x; AddIndices(a,a+1,a+cols+2,a+cols+1); }
            Grain(w,h,Mathf.Min(4800,(int)(w*h/220)),0x786dfa1u,.042f);
            uint seed = 0x7129c45u;
            for (float offset = -h; offset < w; offset += 2.5f+Next(ref seed)*3)
                Stroke(new Vector2(offset,h),new Vector2(offset+h,0),.45f,C(.55f,.60f,.65f,.008f+Next(ref seed)*.009f));
            // Uneven fine brushing and sparse scratches, fading along their length.
            for (int i = 0; i < 270; i++)
            {
                float x = Next(ref seed)*w, y = Next(ref seed)*h;
                float length = 12+Next(ref seed)*85;
                float alpha = .012f+Next(ref seed)*.026f;
                Stroke(new Vector2(x,y),new Vector2(x+length*.8f,y-length*.6f),.4f,C(.58f,.63f,.69f,alpha));
            }
            for (int i = 0; i < 6; i++)
            {
                float inset = 22+i*6;
                Stroke(new Vector2(w-inset,h),new Vector2(w,h-inset),.6f,C(.40f,.47f,.54f,.055f));
                Stroke(new Vector2(0,h-inset-24),new Vector2(inset+24,h),.6f,C(.40f,.47f,.54f,.038f));
            }
        }
        void Grain(float w,float h,int count,uint seed,float alpha)
        {
            for (int i = 0; i < count; i++)
            {
                float x = Next(ref seed)*w, y = Next(ref seed)*h;
                float length = .5f+Next(ref seed)*1.7f;
                float tone = Next(ref seed) > .5f ? .25f : 0;
                Face(x,y,length,.5f,C(tone,tone,tone,alpha*(.4f+Next(ref seed)*.6f)));
            }
        }
        static float Next(ref uint seed)
        { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed&0xffffff)/16777215f; }
        void Stroke(Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 side = new Vector2(b.y-a.y,a.x-b.x).normalized*width*.5f;
            Quad(a+side,b+side,b-side,a-side,color,color,color,color);
        }
        void Radial(float w,float h,Color light,float radius = 0)
        {
            const int cols = 48, rows = 24;
            if (radius <= 0) radius = Mathf.Max(w*.58f,h*.9f);
            var center = new Vector2(w*.5f,h+5);
            int start = vertices.Count;
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= cols; x++)
                {
                    var point = new Vector2(w*x/cols,h*y/rows);
                    Color color = light;
                    color.a *= Mathf.Pow(Mathf.Clamp01(1-Vector2.Distance(point,center)/radius),1.6f);
                    AddVertex(point,color);
                }
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int a = start+y*(cols+1)+x, b = a+1, c = b+cols+1, d = a+cols+1;
                    AddIndices(a,b,c,d);
                }
        }
        void Face(float x,float y,float w,float h,Color color) => Face(x,y,w,h,color,color);
        void Face(float x,float y,float w,float h,Color top,Color bottom)
        { Quad(new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h),top,top,bottom,bottom); }
        void Slant(float x,float y,float w,float h,Color color,float lean)
        { Quad(new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w-lean,y+h),new Vector2(x-lean,y+h),color,color,color,color); }
        void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color ca,Color cb,Color cc,Color cd)
        {
            if (clipContour) { ClipQuad(a,b,c,d,ca,cb,cc,cd); return; }
            int start = vertices.Count;
            AddVertex(a,ca); AddVertex(b,cb); AddVertex(c,cc); AddVertex(d,cd);
            AddIndices(start,start+1,start+2,start+3);
        }
        void AddVertex(Vector2 point,Color color)
        { vertices.Add(new Vertex { position = new Vector3(point.x,point.y,Vertex.nearZ), tint = color }); }
        void AddIndices(int a,int b,int c,int d)
        {
            indices.Add((ushort)a); indices.Add((ushort)b); indices.Add((ushort)c);
            indices.Add((ushort)c); indices.Add((ushort)d); indices.Add((ushort)a);
        }
        static Color C(float r,float g,float b,float a = 1) => new Color(r,g,b,a);
    }
}
