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
        static readonly Gradient steel=Material(new Color(.72f,.76f,.78f),new Color(.26f,.30f,.33f),new Color(.49f,.53f,.55f),new Color(.17f,.20f,.23f));
        static readonly Gradient rubber=Material(new Color(.16f,.18f,.19f),new Color(.075f,.09f,.10f),new Color(.10f,.12f,.13f),new Color(.025f,.035f,.04f));
        static readonly Gradient grip=Material(new Color(.34f,.38f,.40f),new Color(.19f,.22f,.24f),new Color(.24f,.27f,.29f),new Color(.065f,.08f,.095f));
        static readonly Vector2[] clutchShape={new Vector2(.18f,0),new Vector2(.83f,0),new Vector2(1,.18f),new Vector2(.96f,.78f),new Vector2(.78f,1),new Vector2(.19f,.98f),new Vector2(0,.79f),new Vector2(0,.16f)};
        static readonly Vector2[] brakeShape={new Vector2(.16f,0),new Vector2(.86f,.03f),new Vector2(1,.19f),new Vector2(.94f,.81f),new Vector2(.74f,1),new Vector2(.16f,.97f),new Vector2(0,.78f),new Vector2(.02f,.17f)};
        static readonly Vector2[] gasShape={new Vector2(.26f,0),new Vector2(.84f,.02f),new Vector2(1,.10f),new Vector2(.91f,.82f),new Vector2(.69f,1),new Vector2(.15f,.97f),new Vector2(0,.85f),new Vector2(.06f,.14f)};
        static readonly Vector2[] ribShape={new Vector2(.12f,0),new Vector2(.88f,0),new Vector2(1,.22f),new Vector2(1,.78f),new Vector2(.88f,1),new Vector2(.12f,1),new Vector2(0,.78f),new Vector2(0,.22f)};
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
        static Gradient Material(Color a,Color b,Color c,Color d)=>new Gradient{
            colorKeys=new[]{new GradientColorKey(a,0),new GradientColorKey(b,.35f),new GradientColorKey(c,.68f),new GradientColorKey(d,1)},
            alphaKeys=new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)}};
        readonly struct PedalProjection
        {
            readonly float center,sine,cosine;
            internal PedalProjection(float center,float depression){this.center=center;float angle=Mathf.Lerp(18,-32,depression)*Mathf.Deg2Rad;sine=Mathf.Sin(angle);cosine=Mathf.Cos(angle);}
            internal Vector2 Point(float x,float y,float depth=0){
                float down=y-13,z=down*sine+depth*cosine;
                float perspective=360/(360-z);
                return new Vector2(center+(x+z*.08f)*perspective,13+(down*cosine-depth*sine)*perspective);
            }
            internal Vector2 Point(Rect area,Vector2 uv,float depth)=>Point(area.x+uv.x*area.width,area.y+uv.y*area.height,depth);
        }
        static void Outline(Painter2D p,PedalProjection pose,Rect area,Vector2[] shape,float depth,Vector2 offset)
        {
            var previous=pose.Point(area,shape[shape.Length-1],depth)+offset;
            var first=pose.Point(area,shape[0],depth)+offset;
            p.BeginPath();p.MoveTo(Vector2.Lerp(previous,first,.8f));
            for(int i=0;i<shape.Length;i++){
                var corner=pose.Point(area,shape[i],depth)+offset;
                var next=pose.Point(area,shape[(i+1)%shape.Length],depth)+offset;
                p.QuadraticCurveTo(corner,Vector2.Lerp(corner,next,.2f));p.LineTo(Vector2.Lerp(corner,next,.8f));
            }
            p.ClosePath();
        }
        static void Plate(Painter2D p,PedalProjection pose,Rect area,Vector2[] shape,float depth,Color color,Gradient material=null,float reflection=0)
        {
            p.fillColor=color;
            if(material!=null)p.fillGradient=FillGradient.MakeLinearGradient(material,pose.Point(area.xMin,area.yMin-reflection,depth),pose.Point(area.xMax,area.yMax-reflection,depth),AddressMode.Clamp);
            Outline(p,pose,area,shape,depth,Vector2.zero);p.Fill();
        }
        void Draw(MeshGenerationContext context)
        {
            if(contentRect.width<2)return;var p=context.painter2D;float cx=contentRect.width*.5f;
            bool gas=control==RaceInputAdapter.GasControl;float w=gas?34:49,h=gas?98:62;
            var pose=new PedalProjection(cx,Depression);var pad=new Rect(-w/2,gas?35:70,w,h);
            var shape=gas?gasShape:control==RaceInputAdapter.BrakeControl?brakeShape:clutchShape;
            var mount=new Vector2(cx,13);var armEnd=pose.Point(0,pad.center.y,-6);
            p.lineCap=LineCap.Round;
            Line(p,mount+new Vector2(3,3),armEnd+new Vector2(3,3),10,new Color(.008f,.012f,.016f,.8f));
            Line(p,mount,armEnd,8,new Color(.06f,.075f,.085f));
            Line(p,mount+new Vector2(-2,0),armEnd+new Vector2(-2,0),2.5f,Color.Lerp(rim,face,Depression*.5f));
            p.fillColor=new Color(0,0,0,.35f);Outline(p,pose,pad,shape,-6,new Vector2(4+Depression*2,7-Depression*3));p.Fill();
            Plate(p,pose,pad,shape,-6,new Color(.04f,.05f,.065f));
            Plate(p,pose,pad,shape,0,Color.white,steel,Depression*24);
            var inset=new Rect(pad.x+3.5f,pad.y+3.5f,pad.width-7,pad.height-7);
            Plate(p,pose,inset,shape,1,face,rubber,Depression*12);
            for(int i=0;i<(gas?3:4);i++){
                var bar=gas?new Rect(inset.x+4+i*(inset.width-11)/2,inset.y+11,3,inset.height-22)
                    :new Rect(inset.x+6,inset.y+8+i*(inset.height-20)/3,inset.width-12,4);
                Plate(p,pose,bar,ribShape,1,new Color(.015f,.02f,.025f));
                Plate(p,pose,bar,ribShape,3,Color.white,grip,Depression*8);
            }
            for(int i=0;i<8;i++){
                float y=pad.y+12+i*(pad.height-24)/7;
                Line(p,pose.Point(pad.x+1.8f,y,.2f),pose.Point(pad.x+3,y-1,.2f),.55f,new Color(.85f,.9f,.93f,.28f));
            }
            Line(p,pose.Point(-w*.25f,pad.y+1,.2f),pose.Point(w*.30f,pad.y+1,.2f),1.2f,Color.Lerp(tick,accent,Depression*.5f));
            Circle(p,mount,5,new Color(.025f,.035f,.045f));Circle(p,mount,3.3f,rim);
            Line(p,mount+new Vector2(-1.5f,0),mount+new Vector2(1.5f,0),1,new Color(.06f,.075f,.09f));p.lineCap=LineCap.Butt;
        }
    }
}
