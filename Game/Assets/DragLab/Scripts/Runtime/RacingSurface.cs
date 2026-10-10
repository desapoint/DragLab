using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    // Tessellated cel shading; decorative geometry never owns clicks or drive input.
    internal sealed class RacingSurface : VisualElement
    {
        internal enum Kind { Header, Footer, Metal, Navigation, Button, Primary }
        const float SweepSeconds = .65f;
        readonly VisualElement owner;
        readonly Kind kind;
        readonly List<Vertex> vertices = new List<Vertex>(1024);
        readonly List<ushort> indices = new List<ushort>(4096);
        bool hovered, focused, pressed, selected, wasAnimating;
        float sweepStarted = float.NegativeInfinity;
        internal bool ShineActive => (hovered || focused) && Time.realtimeSinceStartup - sweepStarted < SweepSeconds;
        internal float ShineProgress => Mathf.Clamp01((Time.realtimeSinceStartup - sweepStarted) / SweepSeconds);
        internal int PaintCount { get; private set; }

        internal RacingSurface(VisualElement owner, Kind kind)
        {
            this.owner = owner; this.kind = kind;
            pickingMode = PickingMode.Ignore;
            AddToClassList("racing-surface");
            generateVisualContent += Draw;
            if (kind == Kind.Header || kind == Kind.Footer || kind == Kind.Metal) return;
            owner.RegisterCallback<PointerEnterEvent>(evt => { if (evt.target != owner || hovered) return; hovered = true; StartShine(); });
            owner.RegisterCallback<PointerLeaveEvent>(evt => { if (evt.target != owner) return; hovered = false; pressed = false; MarkDirtyRepaint(); });
            owner.RegisterCallback<FocusInEvent>(evt => { if (evt.target != owner || focused) return; focused = true; StartShine(); });
            owner.RegisterCallback<FocusOutEvent>(evt => { if (evt.target != owner) return; focused = false; MarkDirtyRepaint(); });
            owner.RegisterCallback<PointerDownEvent>(_ => { pressed = true; MarkDirtyRepaint(); });
            owner.RegisterCallback<PointerUpEvent>(_ => { pressed = false; MarkDirtyRepaint(); });
        }
        void StartShine() { sweepStarted = Time.realtimeSinceStartup; MarkDirtyRepaint(); }
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
            if (kind == Kind.Metal)
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
                if (kind == Kind.Primary) SettingsPlate(w,h);
                else if (kind != Kind.Navigation)
                {
                    Color top = kind == Kind.Primary ? C(.68f,.18f,.22f) : C(.23f,.25f,.28f);
                    Color bottom = kind == Kind.Primary ? C(.34f,.06f,.09f) : C(.075f,.085f,.10f);
                    Face(0,0,w,h,top,bottom);
                    Face(1,0,w-2,1,C(1,1,1,kind == Kind.Primary ? .20f : .16f));
                    Face(1,h-2,w-2,1,C(0,0,0,.3f));
                }
                if ((selected && kind != Kind.Primary) || (lit && kind == Kind.Navigation))
                {
                    // Vertex alpha gives a circular, soft light from below the active menu.
                    Color light = selected ? C(.86f,.055f,.12f,.72f) : C(.7f,.75f,.82f,.16f);
                    Radial(w,h,light);
                    if (selected && kind == Kind.Navigation)
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
                Grain(w,h,65,0x23df917u,.045f);
                if (lit && kind != Kind.Navigation) Face(0,0,w,h,C(1,1,1,.035f));
                if (ShineActive)
                {
                    float x = Mathf.Lerp(-h-w*.25f,w+h,ShineProgress), band = Mathf.Max(18,w*.22f);
                    Slant(x,0,band,h,C(1,1,1,.075f),h*.5f);
                    Slant(x+band*.43f,0,band*.14f,h,C(1,1,1,.07f),h*.5f);
                }
                if (pressed) Face(0,0,w,h,C(0,0,0,.22f));
            }
            var mesh = context.Allocate(vertices.Count,indices.Count);
            foreach (var vertex in vertices) mesh.SetNextVertex(vertex);
            foreach (var index in indices) mesh.SetNextIndex(index);
        }
        void SettingsPlate(float w,float h)
        {
            Face(0,5,w,h-10,C(.70f,.13f,.18f),C(.28f,.035f,.06f));
            // Inset glass face and asymmetric light on the upper/left bevel.
            Face(6,9,w-12,(h-18)*.5f,C(1,.35f,.30f,.13f),C(1,.35f,.30f,0));
            Stroke(new Vector2(20,5),new Vector2(w-2,5),1.3f,C(1,.48f,.38f,.8f));
            Stroke(new Vector2(20,5),new Vector2(2,h-5),1.2f,C(.85f,.18f,.23f));
            Stroke(new Vector2(2,h-5),new Vector2(w-20,h-5),1.4f,C(.98f,.24f,.29f));
            Stroke(new Vector2(w-2,5),new Vector2(w-20,h-5),1.1f,C(.20f,.018f,.04f));
            Slant(25,9,4,7,C(1,.75f,.57f,.4f),2.2f);
        }
        Vector2 ClipPlate(Vector2 point)
        {
            float w = contentRect.width, h = contentRect.height;
            float y = Mathf.Clamp(point.y,5,h-5);
            float t = (y-5)/(h-10);
            return new Vector2(Mathf.Clamp(point.x,20-18*t,w-2-18*t),y);
        }
        void HeaderTrim(float w,float h)
        {
            float end = Mathf.Min(310,w*.24f);
            Face(0,h-1,w,1,C(.38f,.40f,.43f,.35f));
            Face(0,h-2,end,1.5f,C(.75f,.07f,.12f,.85f));
            Face(w-end,h-2,end,1.5f,C(.75f,.07f,.12f,.85f));
            // Short terminal chevrons; center bottom remains metal, not a full red border.
            for (int i = 0; i < 3; i++)
                Slant(w-110+i*13,1,9,5,C(.68f,.07f,.12f,.65f),-5);
            Stroke(new Vector2(w-124,1),new Vector2(w-115,9),1,C(.44f,.13f,.16f));
        }
        void FooterTrim(float w,float h)
        {
            Face(0,0,w,8,C(.065f,.08f,.095f));
            Face(26,7,w-52,1,C(.43f,.47f,.52f,.5f));
            Face(28,7,w*.38f-28,1.5f,C(.9f,.1f,.15f,.88f));
            Face(w*.62f,7,w*.38f-28,1.5f,C(.9f,.1f,.15f,.88f));
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 0 : w-32;
                Face(x,0,17,1,C(.48f,.5f,.54f,.6f));
                Stroke(new Vector2(x+17,0),new Vector2(x+28,8),1.5f,C(.8f,.095f,.14f));
                Stroke(new Vector2(x+4,3),new Vector2(x+11,8),1.4f,C(.45f,.1f,.14f));
                Stroke(new Vector2(x+12,3),new Vector2(x+19,8),1.4f,C(.45f,.1f,.14f));
            }
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
            const int cols = 20, rows = 12;
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
            int start = vertices.Count;
            if (kind == Kind.Primary)
            { a = ClipPlate(a); b = ClipPlate(b); c = ClipPlate(c); d = ClipPlate(d); }
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
