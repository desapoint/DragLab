using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed class RaceShiftFlick : VisualElement
    {
        readonly RaceInputAdapter input;
        readonly Func<bool> active, reversed, canDownshift;
        int pointer=-1;
        Vector2 start;
        bool fired,capturedPointer;
        internal bool Held=>pointer>=0;
        internal RaceShiftFlick(RaceInputAdapter input,Func<bool> active,Func<bool> reversed,Func<bool> canDownshift)
        {
            this.input=input;this.active=active;this.reversed=reversed;this.canDownshift=canDownshift;
            name="raceShiftFlick";AddToClassList("race-shift-flick");focusable=false;tabIndex=-1;
            var hint=new FilledMenuIcon("Flick");hint.AddToClassList("race-flick-hint");Add(hint);
            RegisterCallback<PointerDownEvent>(e=>{
                if(NativeMobile.OwnsRacePointer(e)||e.button!=0)return;
                if(StartContact(e.pointerId,e.position,true))e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e=>{if(NativeMobile.OwnsRacePointer(e)||e.pointerId!=pointer)return;MoveContact(e.pointerId,e.position);e.StopPropagation();});
            RegisterCallback<PointerUpEvent>(e=>{if(NativeMobile.OwnsRacePointer(e)||e.pointerId!=pointer)return;EndContact(e.pointerId,e.position);e.StopPropagation();});
            RegisterCallback<PointerCancelEvent>(e=>{if(!NativeMobile.PhysicalRaceTouches||e.pointerId==PointerId.mousePointerId)CancelContact(e.pointerId);});
            RegisterCallback<PointerCaptureOutEvent>(e=>{if(!NativeMobile.PhysicalRaceTouches||e.pointerId==PointerId.mousePointerId)CancelContact(e.pointerId);});
            RegisterCallback<DetachFromPanelEvent>(_=>Release());
        }
        internal bool StartContact(int id,Vector2 point,bool capture)
        {
            if(!active()||Held)return false;pointer=id;start=point;fired=false;
            capturedPointer=capture;if(capture)this.CapturePointer(pointer);return true;
        }
        internal void MoveContact(int id,Vector2 point){if(id==pointer)TryShift(point);}
        internal void EndContact(int id,Vector2 point){if(id!=pointer)return;TryShift(point);Release();}
        internal void CancelContact(int id){if(id==pointer)Release();}
        void TryShift(Vector2 point)
        {
            if(!active()){Release();return;}
            if(fired)return;var delta=point-start;
            if(Mathf.Abs(delta.y)<28||Mathf.Abs(delta.y)<Mathf.Abs(delta.x)*1.25f)return;
            fired=true;int direction=delta.y<0?1:-1;if(reversed())direction=-direction;
            if(direction>0||canDownshift())input.NativeShift(direction);
        }
        internal void Release()
        {
            int captured=pointer;bool release=capturedPointer;pointer=-1;fired=capturedPointer=false;
            if(release&&captured>=0&&this.HasPointerCapture(captured))this.ReleasePointer(captured);
        }
    }
}
