using System;
using System.Collections.Generic;
using System.Globalization;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Presentation
{
    public readonly struct FinishReading
    {
        public readonly string Elapsed, Reaction;
        public readonly bool Finished;
        public FinishReading(CarState state)
        {
            Finished=state.finished;
            Elapsed=Finished?state.elapsed.ToString("00.000",CultureInfo.InvariantCulture):"";
            Reaction=Finished&&state.reaction>=0?state.reaction.ToString("0.000",CultureInfo.InvariantCulture).PadLeft(6):"";
        }
    }

    public sealed class FinishBillboard : MonoBehaviour
    {
        public const float BeyondFinishMetres=RaceSimulation.ShutdownStopMetres-8;
        public FinishReading Player { get; private set; }
        public FinishReading Opponent { get; private set; }
        Mesh mesh;
        Material material;
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> triangles=new List<int>();
        RaceSimulation boundRace;
        static readonly Color Metal=new Color(.13f,.16f,.18f), Face=new Color(.025f,.035f,.04f),
            Off=new Color(.13f,.105f,.065f), On=new Color(1,.46f,.055f), Ink=new Color(.72f,.76f,.75f);
        static readonly int[] digits={0x3f,0x06,0x5b,0x4f,0x66,0x6d,0x7d,0x07,0x7f,0x6f};
        static readonly Dictionary<char,string> letters=new Dictionary<char,string>{
            {'Y',"10001100010101000100001000010000100"},{'O',"01110100011000110001100011000101110"},
            {'U',"10001100011000110001100011000101110"},{'R',"11110100011000111110101001001010001"},
            {'I',"11111001000010000100001000010011111"},{'V',"10001100011000110001100010101000100"},
            {'A',"01110100011000111111100011000110001"},{'L',"10000100001000010000100001000011111"},
            {'E',"11111100001000011110100001000011111"},{'T',"11111001000010000100001000010000100"}};

        public void Initialize()
        {
            mesh=new Mesh{name="Track timing board"};gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            material=RenderMaterials.CreateVertex("Track timing board",Texture2D.whiteTexture);
            var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=-44;
            transform.localPosition=new Vector3(-1.5f,1.65f,0);
        }
        public void Refresh(RaceSimulation race)
        {
            float scale=DragStripView.DepthScale(.8f)*1.25f;
            transform.localScale=Vector3.one*scale;
            transform.localPosition=DragStripView.Project(2+(float)(race.distance+BeyondFinishMetres-race.player.state.distance)*DragStripView.MetersToView,.8f)+Vector3.up*(3.1f*scale);
            bool dirty=boundRace!=race;
            if(dirty||Player.Finished!=race.player.state.finished){Player=new FinishReading(race.player.state);dirty=true;}
            if(boundRace!=race||Opponent.Finished!=race.opponent.state.finished){Opponent=new FinishReading(race.opponent.state);dirty=true;}
            boundRace=race;if(dirty)Draw();
        }
        void Draw()
        {
            vertices.Clear();colors.Clear();triangles.Clear();
            Rect(-2.5f,-3.1f,.14f,2.1f,Metal);Rect(2.35f,-3.1f,.14f,2.1f,Metal);
            Rect(-3.4f,-1.12f,6.8f,2.28f,new Color(.32f,.36f,.38f));
            Rect(-3.36f,-1.08f,6.72f,2.2f,Metal);Rect(-3.27f,-.97f,6.54f,1.98f,Face);
            Rect(-3.26f,.94f,6.52f,.035f,new Color(.8f,.025f,.055f));
            Rect(-.015f,-.91f,.03f,1.79f,Metal);
            Lane(-3.12f,"YOU",Player);Lane(.18f,"RIVAL",Opponent);
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        void Lane(float x,string caption,FinishReading reading)
        {
            Text(caption,x+.08f,.68f,.023f,Ink);
            Text("ET",x+.08f,.10f,.025f,Ink);Number(reading.Elapsed,x+.7f,-.16f,.34f,.63f,6);
            Text("RT",x+.08f,-.59f,.021f,Ink);Number(reading.Reaction,x+.7f,-.78f,.29f,.46f,6);
        }
        void Number(string text,float x,float y,float width,float height,int count)
        {
            bool lit=text.Length>0;if(!lit)text="  .   ";
            for(int i=0;i<count;i++)
            {
                char c=i<text.Length?text[i]:' ';
                if(c=='.'){Rect(x+.015f,y,width*.13f,width*.13f,lit?On:Off);x+=width*.25f;continue;}
                int mask=c>='0'&&c<='9'?digits[c-'0']:0;
                float t=width*.16f;
                for(int s=0;s<7;s++)
                {
                    bool horizontal=s==0||s==3||s==6;
                    float sx=x+(s==1||s==2?width-t:0);
                    float sy=y+(s==0?height-t:s==1||s==5?height*.5f:s==6?height*.5f-t*.5f:0);
                    float sw=horizontal?width:t,sh=horizontal?t:height*.5f-t*.5f;
                    Bevel(sx,sy,sw,sh,(mask&(1<<s))!=0?On:Off,horizontal);
                }
                x+=width*1.17f;
            }
        }
        void Bevel(float x,float y,float w,float h,Color color,bool horizontal)
        {
            float b=Mathf.Min(w,h)*.45f;
            if(horizontal)Polygon(color,new Vector2(x,y+h/2),new Vector2(x+b,y),new Vector2(x+w-b,y),new Vector2(x+w,y+h/2),new Vector2(x+w-b,y+h),new Vector2(x+b,y+h));
            else Polygon(color,new Vector2(x+w/2,y),new Vector2(x+w,y+b),new Vector2(x+w,y+h-b),new Vector2(x+w/2,y+h),new Vector2(x,y+h-b),new Vector2(x,y+b));
        }
        void Text(string text,float x,float y,float pixel,Color color)
        {
            foreach(char c in text){if(letters.TryGetValue(c,out string bits))for(int i=0;i<35;i++)if(bits[i]=='1')Rect(x+(i%5)*pixel,y+(6-i/5)*pixel,pixel*.83f,pixel*.83f,color);x+=pixel*6;}
        }
        void Rect(float x,float y,float w,float h,Color c)=>Polygon(c,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h));
        void Polygon(Color color,params Vector2[] points)
        {
            int at=vertices.Count;foreach(var p in points){vertices.Add(p);colors.Add(color);}
            for(int i=1;i<points.Length-1;i++){triangles.Add(at);triangles.Add(at+i);triangles.Add(at+i+1);}
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
