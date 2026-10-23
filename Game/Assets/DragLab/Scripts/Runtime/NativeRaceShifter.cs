using System;
using DragLab.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed class RaceGearIndicator : GaugeInk
    {
        readonly Label gearLabel,typeLabel;
        readonly VisualElement readout;
        readonly FilledMenuIcon flickHint;
        int current=-1,from,gears=6;
        TransmissionType type;
        ManualGearboxLayout layout;
        Vector2 knob;
        double handoverSeconds=1;
        internal string Readout=>gearLabel.text;
        internal Vector2 Knob=>knob;
        // The visible exit slides within the boot; the true lever pivot is below its surface.
        internal Vector2 ShaftExit
        {
            get { var pivot=new Vector2(90,178);float t=12/Mathf.Max(1,pivot.y-knob.y);return new Vector2(Mathf.LerpUnclamped(pivot.x,knob.x,t),166); }
        }
        internal bool HPattern=>type==TransmissionType.Manual&&layout==ManualGearboxLayout.HPattern;
        internal RaceGearIndicator()
        {
            name="raceShifter";AddToClassList("race-shifter");
            readout=new VisualElement{name="raceGearReadout",pickingMode=PickingMode.Ignore};readout.AddToClassList("shifter-readout");Add(readout);
            gearLabel=new Label{name="raceGear",pickingMode=PickingMode.Ignore};gearLabel.AddToClassList("shifter-gear");readout.Add(gearLabel);
            flickHint=new FilledMenuIcon("Flick"){name="raceFlickHint",pickingMode=PickingMode.Ignore};flickHint.AddToClassList("race-flick-hint");flickHint.AddToClassList("hidden");readout.Add(flickHint);
            typeLabel=new Label{name="raceTransmission",pickingMode=PickingMode.Ignore};typeLabel.AddToClassList("shifter-type");Add(typeLabel);
            generateVisualContent+=Draw;
        }
        internal void SetState(int gear,TransmissionType kind)=>SetState(gear,new TransmissionSpec{type=kind},6,0,1);
        internal void SetState(int gear,TransmissionSpec transmission,int count,double remaining,double duration)
        {
            bool changed=current<0||type!=transmission.type||layout!=transmission.manualLayout||gears!=count;
            if(changed)
            {
                type=transmission.type;layout=transmission.manualLayout;gears=Math.Max(1,count);current=gear;from=gear;handoverSeconds=Math.Max(.001,duration);
                EnableInClassList("h-pattern",HPattern);EnableInClassList("sequential-selector",type==TransmissionType.Manual&&!HPattern);
                typeLabel.text=HPattern?"H-PATTERN":type==TransmissionType.Manual?"SEQUENTIAL":type==TransmissionType.DCT?"DCT":type==TransmissionType.CVT?"CVT":"AUTOMATIC";
                MarkDirtyRepaint();
            }
            if(gear!=current){from=current;current=gear;handoverSeconds=Math.Max(.001,duration);MarkDirtyRepaint();}
            string text=gear==0?"N":type==TransmissionType.CVT?"D":gear.ToString();if(gearLabel.text!=text)gearLabel.text=text;
            var next=Gate(gear);
            if(HPattern&&remaining>0)
            {
                float t=Mathf.Clamp01(1-(float)(remaining/handoverSeconds));var origin=Gate(from);
                next=t<1f/3?Vector2.Lerp(origin,new Vector2(origin.x,92),t*3):t<2f/3?Vector2.Lerp(new Vector2(origin.x,92),new Vector2(next.x,92),(t-1f/3)*3):Vector2.Lerp(new Vector2(next.x,92),next,(t-2f/3)*3);
            }
            else if(type==TransmissionType.Manual&&!HPattern)
            {
                float t=remaining>0?Mathf.Clamp01(1-(float)(remaining/handoverSeconds)):1;
                next=new Vector2(90,92+Mathf.Sin(t*Mathf.PI)*(gear>from?28:-28));
            }
            if(knob!=next||changed)
            {
                knob=next;MarkDirtyRepaint();
                if(type==TransmissionType.Manual){float half=HPattern?46:34;readout.style.left=knob.x-half;readout.style.top=knob.y-half;}
                else{readout.style.left=StyleKeyword.Null;readout.style.top=StyleKeyword.Null;}
            }
        }
        internal Vector2 Gate(int gear)
        {
            int columns=Math.Max(2,(gears+1)/2);
            return gear==0?new Vector2(90,92):new Vector2(50+(gear-1)/2*80f/(columns-1),gear%2==1?80:133);
        }
        internal void SetTouchHint(bool touch,bool reversed)
        {
            flickHint.EnableInClassList("hidden",!touch);
            tooltip=touch?(reversed?"Flick down to shift up; flick up to shift down":"Flick up to shift up; flick down to shift down"):null;
        }
        void CelKnob(Painter2D p,Vector2 center,float radius)
        {
            Circle(p,center,radius+3,Color.Lerp(face,Color.black,.85f));
            Circle(p,center,radius+1,Color.Lerp(rim,face,.3f));
            Circle(p,center,radius-1,Color.Lerp(face,rim,.48f));
            p.fillColor=Color.Lerp(face,Color.black,.35f);p.BeginPath();
            p.Arc(center,radius-1,Angle.Degrees(5),Angle.Degrees(165));
            p.BezierCurveTo(center+new Vector2(-radius*.4f,radius*.12f),center+new Vector2(radius*.5f,radius*.5f),center+new Vector2(radius*.966f,radius*.085f));p.ClosePath();p.Fill();
            p.fillColor=Color.Lerp(rim,tick,.25f);p.BeginPath();
            p.Arc(center,radius-1,Angle.Degrees(205),Angle.Degrees(325));
            p.BezierCurveTo(center+new Vector2(radius*.4f,-radius*.12f),center+new Vector2(-radius*.35f,-radius*.1f),center+new Vector2(-radius*.87f,-radius*.405f));p.ClosePath();p.Fill();
            p.lineWidth=3;p.strokeColor=Color.Lerp(rim,tick,.7f);p.BeginPath();p.Arc(center,radius*.79f,Angle.Degrees(220),Angle.Degrees(256));p.Stroke();
        }
        static void Plate(Painter2D p,float x,float y,float w,float h,Color color)
        {
            p.fillColor=color;p.BeginPath();p.MoveTo(new Vector2(x+8,y));p.LineTo(new Vector2(x+w-8,y));p.ArcTo(new Vector2(x+w,y),new Vector2(x+w,y+8),8);p.LineTo(new Vector2(x+w,y+h-8));p.ArcTo(new Vector2(x+w,y+h),new Vector2(x+w-8,y+h),8);p.LineTo(new Vector2(x+8,y+h));p.ArcTo(new Vector2(x,y+h),new Vector2(x,y+h-8),8);p.LineTo(new Vector2(x,y+8));p.ArcTo(new Vector2(x,y),new Vector2(x+8,y),8);p.ClosePath();p.Fill();
        }
        void Draw(MeshGenerationContext context)
        {
            if(contentRect.width<2)return;var p=context.painter2D;
            if(!HPattern) { Plate(p,4,4,172,194,new Color(.015f,.02f,.025f));Plate(p,6,6,168,190,rim);Plate(p,8,8,164,186,face);
            Line(p,new Vector2(18,10),new Vector2(162,10),1,new Color(.7f,.72f,.75f,.5f)); }
            if(type==TransmissionType.Manual)
            {
                if(HPattern)
                {
                    var basePoint=new Vector2(90,166);
                    Circle(p,basePoint,29,new Color(.015f,.02f,.025f));Circle(p,basePoint,24,Color.Lerp(face,rim,.2f));Circle(p,basePoint,17,Color.Lerp(face,rim,.55f));
                    Line(p,basePoint+new Vector2(-22,8),basePoint+new Vector2(-10,-13),3,face);Line(p,basePoint+new Vector2(21,9),basePoint+new Vector2(10,-13),3,face);
                    p.lineCap=LineCap.Round;
                    Line(p,ShaftExit,knob,13,new Color(.02f,.025f,.03f));Line(p,ShaftExit,knob,8,Color.Lerp(face,rim,.6f));Line(p,ShaftExit-new Vector2(2,0),knob-new Vector2(2,0),3,tick);
                    p.lineCap=LineCap.Butt;
                    CelKnob(p,knob,46);
                    p.lineWidth=2;p.strokeColor=accent;p.BeginPath();p.Arc(knob,46,Angle.Degrees(205),Angle.Degrees(335));p.Stroke();
                    return;
                }
                Line(p,new Vector2(90,49),new Vector2(90,136),12,Color.black);Line(p,new Vector2(92,49),new Vector2(92,136),1,rim);
                // Foreshortened lever and shaded knob. The knob follows the gate, never the driving assist.
                Line(p,new Vector2(90,155),knob,10,new Color(.02f,.025f,.03f));Line(p,new Vector2(90,155),knob,6,Color.Lerp(face,rim,.6f));Line(p,new Vector2(88,155),knob-new Vector2(2,0),2,tick);
                CelKnob(p,knob,34);
            }
            else
            {
                Plate(p,27,22,126,129,new Color(.018f,.025f,.03f));
                Line(p,new Vector2(24,40),new Vector2(24,126),3,accent);Line(p,new Vector2(156,40),new Vector2(156,126),3,accent);
                if(type==TransmissionType.DCT){Line(p,new Vector2(11,57),new Vector2(11,109),4,rim);Line(p,new Vector2(169,57),new Vector2(169,109),4,rim);}
                else Line(p,new Vector2(39,156),new Vector2(141,156),2,rim);
            }
        }
    }
}
