using UnityEngine;
using DragLab.Domain;

namespace DragLab.Presentation
{
    // Fixed sprite-particle pool: no particle module, external texture, instantiation or LINQ per frame.
    public sealed class TireSmokeView : MonoBehaviour
    {
        const int Capacity=96;
        struct Puff { public SpriteRenderer renderer;public Vector3 position,velocity;public float age,life,size,alpha; }
        readonly Puff[] puffs=new Puff[Capacity];
        readonly double[] credits=new double[4];
        static Sprite cloud;
        Vector3 front,rear;
        int cursor;
        uint random=0x8548a3u;
        public int ActiveCount{get;private set;}
        public int FrontEmitted{get;private set;}
        public int RearEmitted{get;private set;}
        static Sprite Cloud()
        {
            if(cloud!=null)return cloud;
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);texture.name="Procedural soft tire smoke";texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;
            var pixels=new Color[32*32];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++) {
                float dx=(x-15.5f)/15.5f,dy=(y-15.5f)/15.5f;
                float falloff=Mathf.Clamp01(1-dx*dx-dy*dy);
                pixels[y*32+x]=new Color(1,1,1,falloff*falloff*.8f);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);
            cloud=Sprite.Create(texture,new Rect(0,0,32,32),new Vector2(.5f,.5f),32);cloud.name="Soft tire smoke puff";return cloud;
        }
        public void Initialize(int order)
        {
            for(int i=0;i<Capacity;i++) {
                var obj=new GameObject("Smoke puff "+i);obj.transform.SetParent(transform,false);
                var renderer=obj.AddComponent<SpriteRenderer>();renderer.sprite=Cloud();renderer.sortingOrder=order;renderer.enabled=false;
                puffs[i].renderer=renderer;
            }
        }
        public void SetContacts(Vector3 front,Vector3 rear){this.front=front;this.rear=rear;}
        float Random(){random^=random<<13;random^=random>>17;random^=random<<5;return random/(float)uint.MaxValue;}
        public void Clear(){for(int i=0;i<Capacity;i++){puffs[i].life=0;if(puffs[i].renderer!=null)puffs[i].renderer.enabled=false;}System.Array.Clear(credits,0,4);ActiveCount=FrontEmitted=RearEmitted=0;}
        public void Step(CarState state,float dt,bool acoustic=false,DriveType drive=DriveType.FWD)
        {
            dt=Mathf.Clamp(dt,0,.1f);ActiveCount=0;
            for(int i=0;i<Capacity;i++) {
                var puff=puffs[i];if(puff.life<=0)continue;puff.age+=dt;
                if(puff.age>=puff.life){puff.renderer.enabled=false;puff.life=0;puffs[i]=puff;continue;}
                puff.position+=puff.velocity*dt;puff.renderer.transform.localPosition=puff.position;
                puff.renderer.transform.localScale=Vector3.one*puff.size*(1+puff.age*2.2f);
                float fade=Mathf.Clamp01(puff.age/.07f)*Mathf.Pow(1-puff.age/puff.life,1.4f);
                puff.renderer.color=new Color(.76f,.79f,.81f,puff.alpha*fade);puffs[i]=puff;ActiveCount++;
            }
            for(int wheel=0;wheel<4;wheel++) {
                var feedback=acoustic&&!state.wheelContactInitialized?TireFeedback.Authored(state,wheel,drive):TireFeedback.Read(state,wheel);
                if(feedback.intensity<.08){credits[wheel]=0;continue;}
                credits[wheel]+=feedback.intensity*22*dt;
                int count=System.Math.Min(3,(int)credits[wheel]);credits[wheel]-=count;
                for(int n=0;n<count;n++) {
                    var puff=puffs[cursor];cursor=(cursor+1)%Capacity;
                    bool wasActive=puff.life>0;
                    puff.age=0;puff.life=.85f+Random()*.45f;puff.size=.20f+Random()*.12f;puff.alpha=(float)feedback.intensity*(.40f+Random()*.25f);
                    puff.position=(wheel<2?front:rear)+new Vector3(-.08f+Random()*.10f,.03f,0);
                    puff.velocity=new Vector3(-.15f-Random()*.24f-(float)state.speed*.035f,.15f+Random()*.25f,0);
                    puff.renderer.transform.localPosition=puff.position;puff.renderer.transform.localScale=Vector3.one*puff.size;puff.renderer.color=Color.clear;puff.renderer.enabled=true;puffs[(cursor+Capacity-1)%Capacity]=puff;
                    if(!wasActive)ActiveCount++;if(wheel<2)FrontEmitted++;else RearEmitted++;
                }
            }
        }
    }
}
