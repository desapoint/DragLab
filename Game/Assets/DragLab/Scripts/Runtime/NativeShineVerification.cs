using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        Texture2D ShinePixels()
        {
            if(shell.Panel.targetTexture==null)return ScreenCapture.CaptureScreenshotAsTexture();
            var target=shell.Panel.targetTexture;var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=previous;return image;
        }
        float ShineRidge(Texture2D lit,Texture2D idle,Rect bounds,float fraction,float scale)
        {
            int y=lit.height-1-(int)((bounds.yMin+bounds.height*fraction)*scale);
            float peak=0;int left=(int)((bounds.xMin+2)*scale),right=(int)((bounds.xMax-2)*scale);
            for(int x=left;x<=right;x++)peak=Mathf.Max(peak,lit.GetPixel(x,y).grayscale-idle.GetPixel(x,y).grayscale);
            Check(peak>.025f,"Rendered shine has measurable contrast at row "+fraction);
            double sum=0,weight=0;
            for(int x=left;x<=right;x++){
                float light=lit.GetPixel(x,y).grayscale-idle.GetPixel(x,y).grayscale;
                if(light<peak*.5f)continue;sum+=x*light;weight+=light;
            }
            return (float)(sum/weight);
        }
        IEnumerator ShineSample(WindowsKeyboardProbe input,VisualElement board,RacingSurface.Kind kind,float width,float height,string label,bool synthetic=false)
        {
            input.MoveMouseAt(30,650);yield return new WaitForSecondsRealtime(.1f);
            var button=new Button{name="shineProbe",focusable=false,tabIndex=-1};button.style.width=width;button.style.height=height;button.style.marginLeft=0;button.style.marginTop=0;button.style.paddingLeft=0;button.style.paddingRight=0;button.style.borderLeftWidth=0;button.style.borderRightWidth=0;button.style.borderTopWidth=0;button.style.borderBottomWidth=0;button.style.backgroundColor=Color.clear;
            if(kind==RacingSurface.Kind.Panel)button.AddToClassList("clickable-panel");board.Add(button);
            var surface=new RacingSurface(button,kind);button.Insert(0,surface);var tick=surface.schedule.Execute(()=>surface.Tick(true)).Every(10);
            yield return new WaitForSecondsRealtime(.1f);var bounds=button.worldBound;
            if(synthetic){using(var enter=PointerEnterEvent.GetPooled()){enter.target=button;button.SendEvent(enter);}}
            else input.MoveMouseAt((int)(bounds.center.x*shell.Panel.scale),(int)(bounds.center.y*shell.Panel.scale));
            yield return new WaitForSecondsRealtime(.28f);yield return new WaitForEndOfFrame();
            Check(surface.ShineActive,"Hover starts shared angled sweep: "+label);
            var lit=ShinePixels();File.WriteAllBytes(Path.Combine(folder,"shine-"+label+".png"),lit.EncodeToPNG());
            yield return new WaitForSecondsRealtime(.8f);yield return new WaitForEndOfFrame();var idle=ShinePixels();
            float scale=shell.Panel.scale,top=ShineRidge(lit,idle,bounds,.28f,scale),bottom=ShineRidge(lit,idle,bounds,.72f,scale);
            float angle=Mathf.Atan((top-bottom)/(bounds.height*.44f*scale))*Mathf.Rad2Deg;
            Check(Mathf.Abs(angle-25)<4,"Rendered angle stays 25 degrees from vertical: "+label+" / "+angle.ToString("F2"));
            if(surface.UsesClippedContour&&kind==RacingSurface.Kind.Primary){
                int x=(int)((bounds.xMin+3)*scale),y=lit.height-1-(int)((bounds.yMin+8)*scale);var a=lit.GetPixel(x,y);var b=idle.GetPixel(x,y);
                Check(Vector4.Distance(a,b)<.015f,"Angled Settings shine stays outside the clear slanted corner");
            }
            Check(!surface.ShineActive,"Sweep completes once: "+label);Destroy(lit);Destroy(idle);tick.Pause();board.Clear();
        }
        IEnumerator AngledShineChecks(WindowsKeyboardProbe input)
        {
            if(!shell.Visible)yield return Key(input,0x77);
            var board=new VisualElement{name="shineProbeBoard"};board.style.position=Position.Absolute;board.style.left=130;board.style.top=210;board.style.width=600;board.style.height=200;board.style.backgroundColor=new Color(.06f,.07f,.08f);shell.Root.Add(board);
            string before=JsonUtility.ToJson(game.Profile);byte[] disk=File.Exists(Infrastructure.ProfileStore.FilePath)?File.ReadAllBytes(Infrastructure.ProfileStore.FilePath):null;
            try{
                foreach(var kind in new[]{RacingSurface.Kind.Navigation,RacingSurface.Kind.Button,RacingSurface.Kind.Primary,RacingSurface.Kind.Tab,RacingSurface.Kind.FirstTab,RacingSurface.Kind.Action,RacingSurface.Kind.SecondaryAction,RacingSurface.Kind.DangerAction,RacingSurface.Kind.ChoiceFirst,RacingSurface.Kind.ChoiceMiddle,RacingSurface.Kind.ChoiceLast,RacingSurface.Kind.Panel}){
                    float width=kind==RacingSurface.Kind.Primary?80:kind==RacingSurface.Kind.Panel?340:160,height=kind==RacingSurface.Kind.Panel?110:64;
                    yield return ShineSample(input,board,kind,width,height,kind.ToString());
                }
                yield return ShineSample(input,board,RacingSurface.Kind.Action,240,32,"wide-short-action");
                Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.5f);yield return ShineSample(input,board,RacingSurface.Kind.Primary,80,64,"1080p-settings");
                var target=new RenderTexture(3840,2160,24);target.Create();shell.Panel.targetTexture=target;shell.Refresh();yield return new WaitForSecondsRealtime(.2f);
                try{Check(shell.Panel.scale==2,"4K target retains uniform two-times native scaling");yield return ShineSample(input,board,RacingSurface.Kind.Primary,80,64,"4k-settings",true);yield return ShineSample(input,board,RacingSurface.Kind.Action,240,32,"4k-short-action",true);}
                finally{shell.Panel.targetTexture=null;target.Release();Destroy(target);}
                Check(before==JsonUtility.ToJson(game.Profile)&&(disk==null?!File.Exists(Infrastructure.ProfileStore.FilePath):System.Linq.Enumerable.SequenceEqual(disk,File.ReadAllBytes(Infrastructure.ProfileStore.FilePath))),"Hover rendering changes no player assets or saved bytes");
                Check(errors==0,"Angled shine probe has no runtime errors");
            }finally{board.RemoveFromHierarchy();Screen.SetResolution(1280,720,false);}
        }
#endif
    }
}
