using System;
using System.Collections.Generic;
using DragLab.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed class NativeRacePedals : VisualElement
    {
        internal readonly RaceTouchPedal Clutch, Brake, Gas;
        internal NativeRacePedals(RaceInputAdapter input, Func<bool> active)
        {
            name="racePedals";pickingMode=PickingMode.Ignore;AddToClassList("race-pedals");
            Clutch=new RaceTouchPedal("clutchPedal","Clutch",RaceInputAdapter.ClutchControl,input,active);
            Brake=new RaceTouchPedal("brakePedal","Brake",RaceInputAdapter.BrakeControl,input,active);
            Gas=new RaceTouchPedal("gasPedal","Accelerator",RaceInputAdapter.GasControl,input,active);
            Add(Clutch);Add(Brake);Add(Gas);
        }
        internal static bool NeedsClutch(CarSimulation car) => car.transmission.type==TransmissionType.Manual &&
            (car.driver.launch==LaunchMode.Manual || car.driver.shifting==ShiftMode.ManualClutch&&!car.driver.clutchAssist);
        internal void Refresh(CarSimulation car,bool active,DriverInput input,float dt)
        {
            bool clutch=NeedsClutch(car);Clutch.EnableInClassList("hidden",!clutch);
            if(!active)Release();else if(!clutch)Clutch.Release();
            Clutch.Animate(active&&clutch?(float)input.clutch:0,dt);
            Brake.Animate(active?(float)input.brake:0,dt);Gas.Animate(active?(float)input.throttle:0,dt);
        }
        internal void Release(){Clutch.Release();Brake.Release();Gas.Release();}
    }

    internal sealed class RaceTouchPedal : GaugeInk
    {
        readonly RaceInputAdapter input;
        readonly Func<bool> active;
        readonly int control;
        readonly Gradient steel=new Gradient{colorKeys=new[]{new GradientColorKey(new Color(.7f,.73f,.75f),0),new GradientColorKey(new Color(.2f,.23f,.26f),1)},alphaKeys=new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)}};
        readonly Dictionary<int,float> fingers=new Dictionary<int,float>();
        readonly HashSet<int> capturedPointers=new HashSet<int>();
        internal float Depression { get; private set; }
        internal RaceTouchPedal(string id,string caption,int control,RaceInputAdapter input,Func<bool> active)
        {
            name=id;tooltip=caption;this.control=control;this.input=input;this.active=active;
            RemoveFromClassList("gauge-layer");AddToClassList("race-pedal");pickingMode=PickingMode.Position;focusable=false;tabIndex=-1;
            RegisterCallback<PointerDownEvent>(e=>{
                if(NativeMobile.OwnsRacePointer(e)||e.button!=0)return;
                if(StartContact(e.pointerId,e.position.y,true))e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e=>{
                if(NativeMobile.OwnsRacePointer(e)||!fingers.ContainsKey(e.pointerId))return;
                MoveContact(e.pointerId,e.position.y);e.StopPropagation();
            });
            RegisterCallback<PointerUpEvent>(e=>{if(NativeMobile.OwnsRacePointer(e))return;End(e.pointerId);e.StopPropagation();});
            RegisterCallback<PointerCancelEvent>(e=>{if(!NativeMobile.PhysicalRaceTouches||e.pointerId==PointerId.mousePointerId)End(e.pointerId);});
            RegisterCallback<PointerCaptureOutEvent>(e=>{if(!NativeMobile.PhysicalRaceTouches||e.pointerId==PointerId.mousePointerId)End(e.pointerId);});
            RegisterCallback<DetachFromPanelEvent>(_=>Release());
            generateVisualContent+=Draw;
        }
        internal bool StartContact(int pointer,float y,bool capture)
        {
            if(!active())return false;fingers[pointer]=y;input.NativePedal(pointer,control,true);
            if(capture){capturedPointers.Add(pointer);this.CapturePointer(pointer);}return true;
        }
        internal void MoveContact(int pointer,float y)
        {
            if(!fingers.TryGetValue(pointer,out float start))return;if(!active()){Release();return;}
            input.NativePedal(pointer,control,true,Mathf.Clamp01(1+(y-start)/100));
        }
        internal void End(int pointer)
        {
            if(!fingers.Remove(pointer))return;input.NativePedal(pointer,control,false);
            if(capturedPointers.Remove(pointer)&&this.HasPointerCapture(pointer))this.ReleasePointer(pointer);
        }
        internal void Release(){if(fingers.Count==0)return;foreach(int pointer in new List<int>(fingers.Keys))End(pointer);}
        internal void Animate(float value,float dt)
        {
            float next=Mathf.MoveTowards(Depression,Mathf.Clamp01(value),Mathf.Max(0,dt)*9);
            if(next==Depression)return;Depression=next;MarkDirtyRepaint();
        }
        static void Plate(Painter2D p,Rect r,float radius,Color color,FillGradient gradient=default)
        {
            p.fillColor=color;if(gradient.gradient!=null)p.fillGradient=gradient;p.BeginPath();p.MoveTo(new Vector2(r.xMin+radius,r.yMin));p.LineTo(new Vector2(r.xMax-radius,r.yMin));
            p.ArcTo(new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMin+radius),radius);p.LineTo(new Vector2(r.xMax,r.yMax-radius));
            p.ArcTo(new Vector2(r.xMax,r.yMax),new Vector2(r.xMax-radius,r.yMax),radius);p.LineTo(new Vector2(r.xMin+radius,r.yMax));
            p.ArcTo(new Vector2(r.xMin,r.yMax),new Vector2(r.xMin,r.yMax-radius),radius);p.LineTo(new Vector2(r.xMin,r.yMin+radius));
            p.ArcTo(new Vector2(r.xMin,r.yMin),new Vector2(r.xMin+radius,r.yMin),radius);p.ClosePath();p.Fill();
        }
        void Draw(MeshGenerationContext context)
        {
            if(contentRect.width<2)return;var p=context.painter2D;float cx=contentRect.width*.5f;
            bool gas=control==RaceInputAdapter.GasControl;float w=gas?40:58,h=gas?100:64;
            float travel=Depression*20,y=gas?35:69;
            p.lineCap=LineCap.Round;
            Line(p,new Vector2(cx-5,15),new Vector2(cx+2,y+35+travel),11,new Color(.025f,.03f,.035f));
            Line(p,new Vector2(cx-4,16),new Vector2(cx+3,y+35+travel),4,rim);p.lineCap=LineCap.Butt;
            var pad=new Rect(cx-w/2,y+travel,w,h*(1-Depression*.2f));
            Plate(p,new Rect(pad.x-3,pad.y+4,pad.width+6,pad.height+4),8,new Color(.008f,.012f,.015f));
            Plate(p,pad,6,Color.white,FillGradient.MakeLinearGradient(steel,new Vector2(0,pad.y),new Vector2(0,pad.yMax),AddressMode.Clamp));
            var rubber=new Rect(pad.x+4,pad.y+4,pad.width-8,pad.height-8);Plate(p,rubber,4,face);
            p.lineCap=LineCap.Round;
            if(gas)for(int i=0;i<3;i++){float x=rubber.x+7+i*(rubber.width-14)/2;Line(p,new Vector2(x,rubber.y+8),new Vector2(x,rubber.yMax-8),3,new Color(.31f,.34f,.36f));}
            else for(int i=0;i<4;i++){float yy=rubber.y+9+i*(rubber.height-18)/3;Line(p,new Vector2(rubber.x+7,yy),new Vector2(rubber.xMax-7,yy),3,new Color(.31f,.34f,.36f));}
            Line(p,new Vector2(pad.x+7,pad.y+2),new Vector2(pad.xMax-7,pad.y+2),2,Color.Lerp(tick,accent,Depression));
            Circle(p,new Vector2(cx-5,15),4,new Color(.15f,.18f,.2f));p.lineCap=LineCap.Butt;
        }
    }
}
