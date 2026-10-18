using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal static class NativeMobile
    {
        internal static bool SimulatedTouch, SimulatedPhysicalRaceTouches;
        internal static bool PhysicalRaceTouches=>Application.isMobilePlatform||SimulatedPhysicalRaceTouches;
        internal static bool OwnsRacePointer(IPointerEvent e)=>PhysicalRaceTouches&&(e.pointerType!=UnityEngine.UIElements.PointerType.mouse||Input.touchCount>0);
        internal static Rect? SimulatedSafeArea;
        internal static bool Touch => Application.isMobilePlatform || SimulatedTouch;
        internal static float Scale(int width,int height)
        {
            float desktop=Mathf.Max(1,Mathf.Min(width/1920f,height/1080f));
            return Touch?Mathf.Max(.5f,Mathf.Min(width/960f,height/540f)):desktop;
        }
        internal static Rect SafeArea => SimulatedSafeArea ?? Screen.safeArea;
        internal static void Fit(VisualElement root,PanelSettings panel,int width,int height)
        {
            panel.scale=Scale(width,height);root.EnableInClassList("touch-ui",Touch);
            Rect safe=panel.targetTexture==null&&Touch?SafeArea:new Rect(0,0,width,height);
            root.style.marginLeft=Mathf.Max(0,safe.xMin)/panel.scale;
            root.style.marginRight=Mathf.Max(0,width-safe.xMax)/panel.scale;
            root.style.marginTop=Mathf.Max(0,height-safe.yMax)/panel.scale;
            root.style.marginBottom=Mathf.Max(0,safe.yMin)/panel.scale;
            float left=Mathf.Max(0,safe.xMin)/panel.scale,right=Mathf.Max(0,width-safe.xMax)/panel.scale;
            float top=Mathf.Max(0,height-safe.yMax)/panel.scale,bottom=Mathf.Max(0,safe.yMin)/panel.scale;
            foreach(var child in root.Children()){
                if(child.name=="metalBackground"){
                    child.style.left=-left;child.style.right=-right;
                    child.style.top=Touch?-top:64;child.style.bottom=Touch?-bottom:30;
                }
                else if(child.name=="modalLayer"){
                    child.style.left=-left;child.style.right=-right;child.style.top=-top;child.style.bottom=-bottom;
                    child.style.paddingLeft=left;child.style.paddingRight=right;child.style.paddingTop=top;child.style.paddingBottom=bottom;
                }
            }
        }
    }
}
