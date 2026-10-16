using System.Collections.Generic;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Presentation
{
    public sealed class DragStripView : MonoBehaviour
    {
        // One projection for road travel, separation and the finish marker. No speed-dependent
        // integration: pausing, replays and resets all retain the same physical distances.
        public const float MetersToView = .60f;
        public const float PlayerLane=-2.6f, OpponentLane=-.8f, RoadDepthCompression=.35f, FarCarScale=.82f, StartLightScale=.85f, StartPoleHeight=1.1f;
        const float Span=76.8f, DepthSlope=(1-FarCarScale)/(OpponentLane-PlayerLane), DepthSkew=1.1f, GroundNearLane=-8f;
        struct Tile { public Transform transform; public Vector3 origin; public float parallax; }
        readonly List<Tile> tiles = new List<Tile>();
        readonly List<SpriteRenderer> lamps = new List<SpriteRenderer>(), halos = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> streaks = new List<SpriteRenderer>();
        Transform tree, finish, start;
        Mesh roadMesh, finishMesh, startMesh; Material roadMaterial, finishMaterial, startMaterial; bool linesInitialized;
        readonly Vector3[] finishVertices=new Vector3[50],startVertices=new Vector3[50];
        readonly Vector2[] roadUv=new Vector2[50],finishUv=new Vector2[50];readonly float[] roadU=new float[50];
        static Sprite asphalt, housing, lens, halo, line, checker, pole;
        static readonly Color Amber = new Color(1,.56f,.045f), Green = new Color(.22f,1,.36f), Red = new Color(1,.08f,.12f);
        public static float DepthScale(float lane) => 1-(lane-PlayerLane)*DepthSlope;
        public static float ScreenLane(float lane) => PlayerLane+(lane-PlayerLane)*RoadDepthCompression;
        public static Vector3 Project(float x,float lane) => new Vector3(2+(x-2)*DepthScale(lane)+(lane-PlayerLane)*DepthSkew,ScreenLane(lane),0);
        public static Vector3 CarPosition(float x,float lane,float contactHeight) => Project(x,lane)+Vector3.up*contactHeight*DepthScale(lane);
        public float Travel { get; private set; }
        public int LitLampCount { get; private set; }
        public float SpeedCue { get; private set; }
        public FinishBillboard Billboard { get; private set; }
        public void Build()
        {
            PrepareTextures();
            BuildRoad();
            Billboard=new GameObject("Finish timing billboard").AddComponent<FinishBillboard>();Billboard.transform.SetParent(transform,false);Billboard.Initialize();
            for(int i=0;i<60;i++) AddTile(Load("barrier-tile"),new Vector3(i*1.28f,.8f),-40,Vector3.one,1);
            for(int i=0;i<24;i++)
            {
                AddTile(line,new Vector3(i*3.2f,-1.3f),-30,new Vector3(1.25f,.36f,1),1);
                // A near edge gives the eye an immediate speed reference against the far barrier.
                AddTile(line,new Vector3(i*3.2f,-3.65f),-28,new Vector3(1.2f,.68f,1),1);
                var streak=AddTile(line,new Vector3(i*3.2f,-3.1f-(i%3)*.15f),-29,new Vector3(.2f,.096f,1),1.12f);
                streak.color=Color.clear;streaks.Add(streak);
            }
            tree=Make(housing,Vector3.zero,-10).transform;
            tree.localScale=Vector3.one*StartLightScale;
            var support=Make(pole,Vector3.zero,-11).transform;support.SetParent(tree,false);
            support.localScale=new Vector3(.14f/(StartLightScale*pole.bounds.size.x),(StartPoleHeight+.08f)/(StartLightScale*pole.bounds.size.y),1);
            support.localPosition=new Vector3(0,-1.6f-StartPoleHeight/(2*StartLightScale)+.04f/StartLightScale);
            for(int row=0;row<5;row++)for(int column=0;column<2;column++)
            {
                var pos=new Vector3(column==0?-.185f:.185f,.66f-row*.39f);
                var glow=Make(halo,Vector3.zero,-9);glow.transform.SetParent(tree,false);glow.transform.localPosition=pos;glow.transform.localScale=Vector3.one*.62f;halos.Add(glow);
                var lamp=Make(lens,Vector3.zero,-8);lamp.transform.SetParent(tree,false);lamp.transform.localPosition=pos;lamp.transform.localScale=Vector3.one*.31f;lamps.Add(lamp);
            }
            // Pre-stage / stage pairs are distinct from the three amber countdown lamps.
            for(int row=0;row<2;row++)for(int col=0;col<2;col++)
            {
                var stage=Make(lens,Vector3.zero,-8);stage.transform.SetParent(tree,false);
                stage.transform.localPosition=new Vector3(col==0?-.14f:.14f,1.2f-row*.22f);
                stage.transform.localScale=Vector3.one*.11f;stage.color=new Color(.9f,.88f,.65f);
            }
            finish=MakeMesh("Finish line",checker.texture,-20,out finishMesh,out finishMaterial);
            start=MakeMesh("Start line",line.texture,-21,out startMesh,out startMaterial);
            SetLines(70,2);
            ShowGarage();
        }
        SpriteRenderer AddTile(Sprite sprite,Vector3 origin,int order,Vector3 scale,float parallax)
        {
            var renderer=Make(sprite,origin,order);var t=renderer.transform;var projectedScale=scale*DepthScale(origin.y);if(sprite==line)projectedScale.y*=RoadDepthCompression;t.localScale=projectedScale;
            tiles.Add(new Tile{transform=t,origin=origin,parallax=parallax});return renderer;
        }
        static Sprite Load(string name) => Resources.Load<Sprite>("Sprites/Track/"+name);
        SpriteRenderer Make(Sprite sprite,Vector3 position,int order)
        {
            var obj=new GameObject(sprite.name);obj.transform.SetParent(transform,false);obj.transform.localPosition=position;
            var r=obj.AddComponent<SpriteRenderer>();r.sharedMaterial=RenderMaterials.Sprite;r.sprite=sprite;r.sortingOrder=order;return r;
        }
        public void ShowGarage()
        {
            Billboard.gameObject.SetActive(false);
            tree.gameObject.SetActive(false);finish.gameObject.SetActive(false);start.gameObject.SetActive(false);Travel=SpeedCue=0;
            PositionTiles(0);for(int i=0;i<roadUv.Length;i++)roadUv[i].x=roadU[i];roadMesh.uv=roadUv;foreach(var streak in streaks)streak.color=Color.clear;
        }
        void PositionTiles(float travel)
        {
            foreach(var tile in tiles)
            {
                float x=Mathf.Repeat(tile.origin.x-travel*tile.parallax+38,Span)-38;
                tile.transform.localPosition=Project(x,tile.origin.y);
            }
        }
        public void Render(RaceSimulation race)
        {
            Billboard.gameObject.SetActive(true);Billboard.Refresh(race);
            tree.gameObject.SetActive(true);finish.gameObject.SetActive(true);start.gameObject.SetActive(true);
            Travel=(float)race.player.state.distance*MetersToView;PositionTiles(Travel);
            tree.localPosition=Project(-3-Travel,.8f)+Vector3.up*(1.6f*StartLightScale+StartPoleHeight);
            SetLines(2+(float)race.distance*MetersToView-Travel,2-Travel);
            float offset=Mathf.Repeat(Travel,6.4f)/6.4f;
            for(int i=0;i<roadUv.Length;i++)roadUv[i].x=roadU[i]+offset;
            roadMesh.uv=roadUv;
            SpeedCue=Mathf.SmoothStep(0,1,Mathf.InverseLerp(12,85,(float)race.player.state.speed));
            for(int i=0;i<streaks.Count;i++)
            {
                streaks[i].color=new Color(.62f,.65f,.67f,SpeedCue*.16f);
                streaks[i].transform.localScale=new Vector3((.15f+SpeedCue*(1.1f+(i%4)*.18f))*DepthScale(-3.1f-(i%3)*.15f),.096f*RoadDepthCompression,1);
            }
            LitLampCount=0;
            for(int i=0;i<lamps.Count;i++)
            {
                int row=i/2;
                bool lit=!race.warmup&&(row<3?race.time>=1.5+row*.5&&race.time<3:row==3?race.time>=3&&!race.player.state.disqualified:race.player.state.disqualified);
                Color color=row<3?Amber:row==3?Green:Red;
                lamps[i].color=lit?color:new Color(color.r*.10f+.035f,color.g*.10f+.035f,color.b*.10f+.035f);
                halos[i].color=new Color(color.r,color.g,color.b,lit?.55f:0);
                if(lit)LitLampCount++;
            }
        }
        Transform MakeMesh(string caption,Texture texture,int order,out Mesh mesh,out Material material)
        {
            var obj=new GameObject(caption);obj.transform.SetParent(transform,false);
            mesh=new Mesh{name=caption};obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            material=RenderMaterials.CreateVertex(caption,texture);
            var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
            return obj.transform;
        }
        void BuildRoad()
        {
            MakeMesh("Perspective asphalt",asphalt.texture,-50,out roadMesh,out roadMaterial);
            var vertices=new Vector3[50];var colors=new Color32[50];
            for(int row=0;row<=24;row++)for(int side=0;side<2;side++)
            {
                float y=Mathf.Lerp(GroundNearLane,.82f,row/24f),x=side==0?-48:55;
                int at=row*2+side;vertices[at]=Project(x,y);
                roadU[at]=x/6.4f;
                roadUv[at]=new Vector2(roadU[at],(y+4.8f)/5.62f);
                colors[at]=Color.Lerp(Color.white,new Color(.77f,.79f,.81f),row/24f);
            }
            roadMesh.vertices=vertices;roadMesh.uv=roadUv;roadMesh.colors32=colors;roadMesh.triangles=StripTriangles();roadMesh.MarkDynamic();
        }
        static int[] StripTriangles()
        {
            var indices=new int[24*6];
            for(int i=0;i<24;i++){int a=i*2,b=i*6;indices[b]=a;indices[b+1]=a+2;indices[b+2]=a+1;indices[b+3]=a+1;indices[b+4]=a+2;indices[b+5]=a+3;}
            return indices;
        }
        void SetLines(float finishX,float startX)
        {
            // Map repeating checks in the ribbon's projected tangent/normal basis. Including
            // the tangent offset at each edge removes the long, skewed texture diamonds.
            var tangent=(Vector2)(Project(finishX,.75f)-Project(finishX,GroundNearLane));tangent.Normalize();
            var normal=new Vector2(tangent.y,-tangent.x);
            float v=0,previousWidth=0;Vector2 previousCenter=Vector2.zero;
            for(int row=0;row<=24;row++)
            {
                float y=Mathf.Lerp(GroundNearLane,.75f,row/24f);Vector2 center=Project(finishX,y);
                float width=.44f*DepthScale(y)*Mathf.Abs(normal.x);
                if(row>0)v+=Vector2.Distance(center,previousCenter)*2/(width+previousWidth);
                for(int side=0;side<2;side++)
                {
                    int index=row*2+side;
                    finishVertices[index]=Project(finishX+(side==0?-.22f:.22f),y);
                    var delta=(Vector2)finishVertices[index]-center;
                    finishUv[index]=new Vector2(.5f+Vector2.Dot(delta,normal)/width,v+Vector2.Dot(delta,tangent)/width);
                    startVertices[index]=Project(startX+(side==0?-.035f:.035f),y);
                }
                previousWidth=width;previousCenter=center;
            }
            finishMesh.vertices=finishVertices;finishMesh.uv=finishUv;startMesh.vertices=startVertices;
            if(!linesInitialized)
            {
                var uv=new Vector2[50];for(int i=0;i<50;i++)uv[i]=new Vector2(i%2,i/2f/24);
                finishMesh.triangles=StripTriangles();finishMesh.MarkDynamic();
                startMesh.uv=uv;startMesh.triangles=StripTriangles();startMesh.MarkDynamic();linesInitialized=true;
            }
            finishMesh.RecalculateBounds();startMesh.RecalculateBounds();
        }
        void OnDestroy()
        { if(startMesh!=null)Destroy(startMesh);if(startMaterial!=null)Destroy(startMaterial);if(roadMesh!=null)Destroy(roadMesh);if(finishMesh!=null)Destroy(finishMesh);if(roadMaterial!=null)Destroy(roadMaterial);if(finishMaterial!=null)Destroy(finishMaterial); }
        // Cached original procedural textures. Local hash noise never consumes the simulation RNG.
        static uint Hash(uint n){n^=n>>16;n*=0x7feb352du;n^=n>>15;n*=0x846ca68bu;return n^(n>>16);}
        static Sprite Texture(string name,int width,int height,float ppu,System.Func<int,int,Color> pixel,bool mipmaps=false)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,mipmaps){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color32[width*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++)pixels[y*width+x]=pixel(x,y);
            texture.SetPixels32(pixels);texture.Apply(mipmaps,true);
            var sprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),ppu,0,SpriteMeshType.FullRect);sprite.name=name;return sprite;
        }
        static void PrepareTextures()
        {
            if(asphalt!=null)return;
            asphalt=Texture("Grained asphalt",512,512,80,(x,y)=>{
                uint n=Hash((uint)(x+y*512)+0x8c735u);float fine=(n&255)/255f;
                // Coarse aggregate plus fine grit, with broad, understated rubber-darkened lanes.
                float aggregate=(Hash((uint)(x/3+(y/3)*171))&255)/255f;
                float shade=.18f+fine*.038f+aggregate*.025f;
                float lane=1-.16f*Mathf.Exp(-Mathf.Pow((y-270)/54f,2))-.12f*Mathf.Exp(-Mathf.Pow((y-407)/48f,2));
                return new Color(shade*.95f*lane,shade*lane,shade*1.04f*lane);
            },true);
            asphalt.texture.wrapMode=TextureWrapMode.Repeat;
            line=Texture("Road paint",64,8,64,(x,y)=>new Color(.78f,.79f,.75f,Mathf.Min(1,Mathf.Min(y+1,8-y)*.5f)));
            checker=Texture("Repeating finish checks",64,64,64,(x,y)=>(x/32+y/32)%2==0?new Color(.88f,.90f,.91f):new Color(.025f,.03f,.035f),true);
            checker.texture.wrapMode=TextureWrapMode.Repeat;
            checker.texture.filterMode=FilterMode.Trilinear;
            pole=Texture("Start tree support pole",32,256,256,(x,y)=>{
                float across=x/31f;
                float shade=.17f+.25f*Mathf.Exp(-Mathf.Pow((across-.36f)/.26f,2));
                float grain=(Hash((uint)(x+y*32))&15)/1800f;
                return new Color(shade*.92f+grain,shade+grain,shade*1.07f+grain);
            });
            housing=Texture("Start tree housing",160,640,200,(x,y)=>{
                float cornerX=Mathf.Max(Mathf.Abs(x-79.5f)-69.5f,0),cornerY=Mathf.Max(Mathf.Abs(y-319.5f)-309.5f,0);
                float d=Mathf.Sqrt(cornerX*cornerX+cornerY*cornerY),alpha=Mathf.Clamp01(10-d);
                bool edge=x<4||x>155||y<4||y>635||d>6;float top=y/640f;
                float brushed=(Hash((uint)(y*5))&15)/2000f;
                return edge?new Color(.27f,.30f,.32f,alpha):new Color(.035f+top*.035f+brushed,.044f+top*.035f+brushed,.052f+top*.035f+brushed,alpha);
            });
            lens=Texture("Start lamp lens",96,96,96,(x,y)=>{
                float dx=(x-47.5f)/47.5f,dy=(y-47.5f)/47.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
                float alpha=Mathf.Clamp01((1-r)*48);float shade=r>.86f?.26f:.62f+(1-r)*.35f;
                float glint=Mathf.Exp(-((dx+.28f)*(dx+.28f)+(dy-.32f)*(dy-.32f))*30)*.35f;
                shade+=glint;return new Color(shade,shade,shade,alpha);
            },true);
            halo=Texture("Start lamp glow",96,96,96,(x,y)=>{
                float dx=(x-47.5f)/47.5f,dy=(y-47.5f)/47.5f;
                return new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-dx*dx-dy*dy),3)*.65f);
            },true);
        }
    }
}
