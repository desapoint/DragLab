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
        static readonly Gradient steel=Material(new Color(.66f,.71f,.74f),new Color(.38f,.43f,.46f),new Color(.47f,.52f,.55f),new Color(.19f,.23f,.27f));
        static readonly Gradient rubber=Material(new Color(.075f,.09f,.10f),new Color(.035f,.05f,.06f),new Color(.05f,.065f,.075f),new Color(.02f,.03f,.04f));
        static readonly Gradient grip=Material(new Color(.28f,.32f,.34f),new Color(.21f,.25f,.27f),new Color(.24f,.28f,.30f),new Color(.12f,.15f,.17f));
        static readonly Vector2[] padShape={new Vector2(.14f,0),new Vector2(.86f,0),new Vector2(.96f,.09f),new Vector2(1,.89f),new Vector2(.88f,1),new Vector2(.12f,1),new Vector2(0,.89f),new Vector2(.04f,.09f)};
        static readonly Vector2[] gasShape={new Vector2(.16f,0),new Vector2(.84f,0),new Vector2(.94f,.06f),new Vector2(1,.93f),new Vector2(.84f,1),new Vector2(.16f,1),new Vector2(0,.93f),new Vector2(.06f,.06f)};
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
            internal const float Floor=142;
            readonly float center,sine,cosine;
            internal PedalProjection(float center,float depression){this.center=center;float angle=Mathf.Lerp(8,48,Mathf.SmoothStep(0,1,depression))*Mathf.Deg2Rad;sine=Mathf.Sin(angle);cosine=Mathf.Cos(angle);}
            internal Vector2 Point(float x,float y,float depth=0){
                float down=y-Floor,z=down*sine+depth*cosine;
                float perspective=360/(360-z);
                return new Vector2(center+(x+z*.08f)*perspective,Floor+(down*cosine-depth*sine)*perspective);
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
            bool gas=control==RaceInputAdapter.GasControl;float w=gas?38:56,h=gas?106:72;
            var pose=new PedalProjection(cx,Depression);var pad=new Rect(-w/2,PedalProjection.Floor-h,w,h);
            var shape=gas?gasShape:padShape;
            var fixedBase=new PedalProjection(cx,0);var foot=new Rect(-w*.47f,PedalProjection.Floor-2,w*.94f,10);
            p.lineCap=LineCap.Round;
            Plate(p,fixedBase,foot,ribShape,-2,new Color(.008f,.012f,.016f,.8f));
            Plate(p,fixedBase,new Rect(foot.x+2,foot.y+1,foot.width-4,foot.height-3),ribShape,0,face,rubber);
            p.fillColor=new Color(0,0,0,.30f);Outline(p,pose,pad,shape,-5,new Vector2(3,3-Depression*2));p.Fill();
            Plate(p,pose,pad,shape,-6,new Color(.04f,.05f,.065f));
            Plate(p,pose,pad,shape,0,Color.white,steel,Depression*24);
            var inset=new Rect(pad.x+3.5f,pad.y+3.5f,pad.width-7,pad.height-7);
            Plate(p,pose,inset,shape,1,face,rubber,Depression*12);
            for(int i=0;i<(gas?3:4);i++){
                var bar=gas?new Rect(inset.x+6+i*(inset.width-15)/2,inset.y+8,3,inset.height-16)
                    :new Rect(inset.x+6,inset.y+9+i*(inset.height-21)/3,inset.width-12,3);
                Plate(p,pose,bar,ribShape,1,new Color(.015f,.02f,.025f));
                Plate(p,pose,bar,ribShape,2.2f,Color.white,grip,Depression*8);
            }
            p.lineCap=LineCap.Butt;
        }
    }
}
