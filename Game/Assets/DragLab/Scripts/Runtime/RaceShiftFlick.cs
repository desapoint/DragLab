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
        bool fired;
        internal bool Held=>pointer>=0;
        internal RaceShiftFlick(RaceInputAdapter input,Func<bool> active,Func<bool> reversed,Func<bool> canDownshift)
        {
            this.input=input;this.active=active;this.reversed=reversed;this.canDownshift=canDownshift;
            name="raceShiftFlick";AddToClassList("race-shift-flick");focusable=false;tabIndex=-1;
            var hint=new FilledMenuIcon("Flick");hint.AddToClassList("race-flick-hint");Add(hint);
            RegisterCallback<PointerDownEvent>(e=>{
                if(!active()||e.button!=0||Held)return;
                pointer=e.pointerId;start=e.position;fired=false;this.CapturePointer(pointer);e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e=>{if(e.pointerId!=pointer)return;TryShift(e.position);e.StopPropagation();});
            RegisterCallback<PointerUpEvent>(e=>{if(e.pointerId!=pointer)return;TryShift(e.position);Release();e.StopPropagation();});
            RegisterCallback<PointerCancelEvent>(e=>{if(e.pointerId==pointer)Release();});
            RegisterCallback<PointerCaptureOutEvent>(e=>{if(e.pointerId==pointer)Release();});
            RegisterCallback<DetachFromPanelEvent>(_=>Release());
        }
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
            int captured=pointer;pointer=-1;fired=false;
            if(captured>=0&&this.HasPointerCapture(captured))this.ReleasePointer(captured);
        }
    }
}
