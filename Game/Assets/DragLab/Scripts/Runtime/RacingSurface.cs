using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    // Tessellated cel shading; decorative geometry never owns clicks or drive input.
    internal sealed class RacingSurface : VisualElement
    {
        internal enum Kind { Bar, Navigation, Button, Primary }
        const float SweepSeconds = .65f;
        readonly VisualElement owner;
        readonly Kind kind;
        readonly List<Vertex> vertices = new List<Vertex>(1024);
        readonly List<ushort> indices = new List<ushort>(4096);
        bool hovered, focused, pressed, selected, wasAnimating;
        float sweepStarted = float.NegativeInfinity;
        internal bool ShineActive => (hovered || focused) && Time.unscaledTime - sweepStarted < SweepSeconds;
        internal float ShineProgress => Mathf.Clamp01((Time.unscaledTime - sweepStarted) / SweepSeconds);
        internal int PaintCount { get; private set; }

        internal RacingSurface(VisualElement owner, Kind kind)
        {
            this.owner = owner; this.kind = kind;
            pickingMode = PickingMode.Ignore;
            AddToClassList("racing-surface");
            generateVisualContent += Draw;
            if (kind == Kind.Bar) return;
            owner.RegisterCallback<PointerEnterEvent>(_ => { hovered = true; StartShine(); });
            owner.RegisterCallback<PointerLeaveEvent>(_ => { hovered = false; pressed = false; MarkDirtyRepaint(); });
            owner.RegisterCallback<FocusInEvent>(_ => { focused = true; StartShine(); });
            owner.RegisterCallback<FocusOutEvent>(_ => { focused = false; MarkDirtyRepaint(); });
            owner.RegisterCallback<PointerDownEvent>(_ => { pressed = true; MarkDirtyRepaint(); });
            owner.RegisterCallback<PointerUpEvent>(_ => { pressed = false; MarkDirtyRepaint(); });
        }
        void StartShine() { sweepStarted = Time.unscaledTime; MarkDirtyRepaint(); }
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
            if (kind == Kind.Bar)
            {
                Face(0,0,w,h,C(.145f,.155f,.17f),C(.055f,.065f,.078f));
                for (float x = -h; x < w; x += 6)
                    Slant(x,0,.7f,h,C(1,1,1,.025f),-h);
                Face(0,0,w,1,C(1,1,1,.10f));
            }
            else
            {
                if (kind != Kind.Navigation)
                {
                    Color top = kind == Kind.Primary ? C(.68f,.18f,.22f) : C(.23f,.25f,.28f);
                    Color bottom = kind == Kind.Primary ? C(.34f,.06f,.09f) : C(.075f,.085f,.10f);
                    Face(0,0,w,h,top,bottom);
                    Face(1,0,w-2,1,C(1,1,1,kind == Kind.Primary ? .20f : .16f));
                    Face(1,h-2,w-2,1,C(0,0,0,.3f));
                }
                if (selected || (lit && kind == Kind.Navigation))
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
