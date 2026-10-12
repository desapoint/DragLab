using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed class NativeTuningRange : VisualElement
    {
        internal Slider Slider {get;}
        internal FloatField Number {get;}
        internal float Step {get;}
        internal float FineStep {get;}
        internal bool Dragging {get;private set;}
        readonly Func<float> get,min,max,reset;
        readonly Action<float> set;
        readonly Action changed;
        readonly Action<string> invalid;
        readonly SliderFill fill;
        int pointer=-1;
        bool fine,updating;
        float startX,startValue,lastDisplayed=float.NaN;
        internal NativeTuningRange(string id,Func<float> get,Action<float> set,Func<float> min,Func<float> max,Func<float> reset,float step,float fineStep,string unit,Action changed,Action<string> invalid)
        {
            this.get=get;this.set=set;this.min=min;this.max=max;this.reset=reset;this.changed=changed;this.invalid=invalid;Step=step;FineStep=fineStep;
            name="tuneRange:"+id;AddToClassList("tuning-input-row");
            Slider=new Slider(){name="tuneSlider:"+id,focusable=false,tabIndex=-1};Slider.AddToClassList("setting-slider");Add(Slider);
            fill=new SliderFill(Slider);Slider.Q(className:"unity-base-slider__tracker").Add(fill);
            int digits=Mathf.Clamp((int)Math.Ceiling(-Math.Log10(fineStep)),0,6);
            Number=new FloatField(){name="tuneNumber:"+id,isDelayed=true,formatString=digits==0?"0":"0."+new string('#',digits)};Number.AddToClassList("setting-number");Add(Number);
            var units=new Label(unit);units.AddToClassList("tuning-unit");Add(units);
            Slider.tooltip="Left-drag: "+step+" "+unit+" steps. Right-drag: 10× finer ("+fineStep+"). Middle-click: default.";
            Number.tooltip="Type an exact value; Enter to confirm. Fine increment: "+fineStep+" "+unit+".";
            Slider.RegisterValueChangedCallback(e=>{if(!updating)Commit(e.newValue,Step);});
            Number.RegisterValueChangedCallback(e=>{if(!updating)Commit(e.newValue,FineStep);});
            Slider.RegisterCallback<PointerDownEvent>(e=>{
                if(!Slider.enabledInHierarchy)return;
                if(e.button==2){Commit(reset(),0);e.StopImmediatePropagation();e.PreventDefault();return;}
                if(e.button==0){Dragging=true;return;}
                if(e.button!=1)return;fine=true;Dragging=true;pointer=e.pointerId;startX=e.position.x;startValue=get();Slider.CapturePointer(pointer);e.StopImmediatePropagation();e.PreventDefault();
            },TrickleDown.TrickleDown);
            Slider.RegisterCallback<PointerMoveEvent>(e=>{
                if(!fine||e.pointerId!=pointer)return;
                if(Math.Abs(e.position.x-startX)<.001f){e.StopImmediatePropagation();e.PreventDefault();return;}
                float width=Mathf.Max(1,Slider.Q(className:"unity-base-slider__tracker").worldBound.width-Slider.Q(className:"unity-base-slider__dragger").worldBound.width);
                Commit(startValue+(e.position.x-startX)*(max()-min())/width*.1f,FineStep);e.StopImmediatePropagation();e.PreventDefault();
            },TrickleDown.TrickleDown);
            Slider.RegisterCallback<PointerUpEvent>(e=>{bool special=fine;if(special&&Slider.HasPointerCapture(pointer))Slider.ReleasePointer(pointer);fine=false;Dragging=false;pointer=-1;if(special){e.StopImmediatePropagation();e.PreventDefault();}},TrickleDown.TrickleDown);
            Slider.RegisterCallback<PointerCaptureOutEvent>(_=>{fine=false;Dragging=false;pointer=-1;});
            RegisterCallback<DetachFromPanelEvent>(_=>{if(pointer>=0&&Slider.HasPointerCapture(pointer))Slider.ReleasePointer(pointer);fine=false;Dragging=false;pointer=-1;});
            Refresh();
        }
        void Commit(float value,float increment)
        {
            if(!Slider.enabledInHierarchy)return;
            if(float.IsNaN(value)||float.IsInfinity(value)){invalid("Enter a finite number.");lastDisplayed=float.NaN;Refresh();return;}
            value=Mathf.Clamp(increment>0?(float)(Math.Round(value/increment,MidpointRounding.AwayFromZero)*increment):value,min(),max());
            if(Math.Abs(value-get())>1e-6){set(value);changed();}lastDisplayed=float.NaN;Refresh();
        }
        internal void Refresh()
        {
            updating=true;
            try{
                float value=get();Slider.lowValue=min();Slider.highValue=Mathf.Max(min(),max());Slider.SetValueWithoutNotify(value);
                // Do not overwrite uncommitted delayed text every frame. External draft changes
                // and committed slider/number gestures still refresh the displayed value.
                if(float.IsNaN(lastDisplayed)||Math.Abs(value-lastDisplayed)>1e-6){Number.SetValueWithoutNotify(value);lastDisplayed=value;}
                fill.MarkDirtyRepaint();
            }finally{updating=false;}
        }
        internal void Allow(bool allowed){Slider.SetEnabled(allowed);Number.SetEnabled(allowed);}
    }
}
