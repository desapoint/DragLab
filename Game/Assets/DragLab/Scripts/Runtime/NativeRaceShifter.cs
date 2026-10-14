using System;
using DragLab.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed class RaceGearIndicator : GaugeInk
    {
        readonly Label gearLabel,typeLabel;
        readonly VisualElement gateLabels;
        int current=-1,from,gears=6;
        TransmissionType type;
        ManualGearboxLayout layout;
        Vector2 knob;
        internal string Readout=>gearLabel.text;
        internal Vector2 Knob=>knob;
        internal bool HPattern=>type==TransmissionType.Manual&&layout==ManualGearboxLayout.HPattern;
        internal RaceGearIndicator()
        {
            name="raceShifter";AddToClassList("race-shifter");
            gateLabels=new VisualElement{pickingMode=PickingMode.Ignore};gateLabels.AddToClassList("gauge-layer");Add(gateLabels);
            gearLabel=new Label{name="raceGear",pickingMode=PickingMode.Ignore};gearLabel.AddToClassList("shifter-gear");Add(gearLabel);
            typeLabel=new Label{name="raceTransmission",pickingMode=PickingMode.Ignore};typeLabel.AddToClassList("shifter-type");Add(typeLabel);
            generateVisualContent+=Draw;
        }
        internal void SetState(int gear,TransmissionType kind)=>SetState(gear,new TransmissionSpec{type=kind},6,0,1);
        internal void SetState(int gear,TransmissionSpec transmission,int count,double remaining,double duration)
        {
            bool changed=current<0||type!=transmission.type||layout!=transmission.manualLayout||gears!=count;
            if(changed)
            {
                type=transmission.type;layout=transmission.manualLayout;gears=Math.Max(1,count);current=gear;from=gear;
                EnableInClassList("h-pattern",HPattern);EnableInClassList("sequential-selector",type==TransmissionType.Manual&&!HPattern);
                typeLabel.text=HPattern?"H-PATTERN":type==TransmissionType.Manual?"SEQUENTIAL":type==TransmissionType.DCT?"DCT":type==TransmissionType.CVT?"CVT":"AUTOMATIC";
                RebuildGateLabels();MarkDirtyRepaint();
            }
            if(gear!=current){from=current;current=gear;MarkDirtyRepaint();}
            string text=gear==0?"N":type==TransmissionType.CVT?"D":gear.ToString();if(gearLabel.text!=text)gearLabel.text=text;
            var next=Gate(gear);
            if(HPattern&&remaining>0)
            {
                float t=Mathf.Clamp01(1-(float)(remaining/Math.Max(.001,duration)));var origin=Gate(from);
                next=t<1f/3?Vector2.Lerp(origin,new Vector2(origin.x,92),t*3):t<2f/3?Vector2.Lerp(new Vector2(origin.x,92),new Vector2(next.x,92),(t-1f/3)*3):Vector2.Lerp(new Vector2(next.x,92),next,(t-2f/3)*3);
            }
            else if(type==TransmissionType.Manual&&!HPattern)
            {
                float t=remaining>0?Mathf.Clamp01(1-(float)(remaining/Math.Max(.001,duration))):1;
                next=new Vector2(110,92+Mathf.Sin(t*Mathf.PI)*(gear>from?28:-28));
            }
            if(knob!=next||changed)
            {
                knob=next;MarkDirtyRepaint();
                if(type==TransmissionType.Manual){gearLabel.style.left=knob.x-20;gearLabel.style.top=knob.y-20;}
                else{gearLabel.style.left=StyleKeyword.Null;gearLabel.style.top=StyleKeyword.Null;}
            }
        }
        internal Vector2 Gate(int gear)
        {
            int columns=Math.Max(2,(gears+1)/2);
            return gear==0?new Vector2(110,92):new Vector2(44+(gear-1)/2*132f/(columns-1),gear%2==1?51:133);
        }
        void RebuildGateLabels()
        {
            gateLabels.Clear();
            if(HPattern)for(int i=1;i<=gears;i++)
            {
                var point=Gate(i);var label=new Label(i.ToString()){pickingMode=PickingMode.Ignore};label.AddToClassList("gate-number");label.style.left=point.x-15;label.style.top=point.y+(i%2==1?-31:12);gateLabels.Add(label);
            }
            else if(type==TransmissionType.Manual)foreach(var sign in new[]{"−","+"})
            {var label=new Label(sign){pickingMode=PickingMode.Ignore};label.AddToClassList("gate-number");label.style.left=95;label.style.top=sign=="−"?16:144;gateLabels.Add(label);}
        }
        static void Plate(Painter2D p,float x,float y,float w,float h,Color color)
        {
            p.fillColor=color;p.BeginPath();p.MoveTo(new Vector2(x+8,y));p.LineTo(new Vector2(x+w-8,y));p.ArcTo(new Vector2(x+w,y),new Vector2(x+w,y+8),8);p.LineTo(new Vector2(x+w,y+h-8));p.ArcTo(new Vector2(x+w,y+h),new Vector2(x+w-8,y+h),8);p.LineTo(new Vector2(x+8,y+h));p.ArcTo(new Vector2(x,y+h),new Vector2(x,y+h-8),8);p.LineTo(new Vector2(x,y+8));p.ArcTo(new Vector2(x,y),new Vector2(x+8,y),8);p.ClosePath();p.Fill();
        }
        void Draw(MeshGenerationContext context)
        {
            if(contentRect.width<2)return;var p=context.painter2D;
            Plate(p,4,4,212,194,new Color(.015f,.02f,.025f));Plate(p,6,6,208,190,rim);Plate(p,8,8,204,186,face);
            Line(p,new Vector2(18,10),new Vector2(202,10),1,new Color(.7f,.72f,.75f,.5f));
            if(type==TransmissionType.Manual)
            {
                if(HPattern)
                {
                    int columns=Math.Max(2,(gears+1)/2);
                    for(int i=0;i<columns;i++){float x=44+i*132f/(columns-1);Line(p,new Vector2(x,51),new Vector2(x,133),9,Color.black);Line(p,new Vector2(x+1,51),new Vector2(x+1,133),2,rim);}
                    Line(p,new Vector2(44,92),new Vector2(176,92),9,Color.black);
                }
                else {Line(p,new Vector2(110,49),new Vector2(110,136),12,Color.black);Line(p,new Vector2(112,49),new Vector2(112,136),1,rim);}
                // Foreshortened lever and shaded knob. The knob follows the gate, never the driving assist.
                Line(p,new Vector2(110,155),knob,10,new Color(.02f,.025f,.03f));Line(p,new Vector2(112,155),knob+new Vector2(2,0),3,rim);
                Circle(p,knob,23,Color.black);Circle(p,knob+new Vector2(0,-2),21,rim);Circle(p,knob+new Vector2(0,-2),18,new Color(.075f,.085f,.10f));
                p.strokeColor=new Color(1,1,1,.35f);p.lineWidth=1;p.BeginPath();p.Arc(knob+new Vector2(0,-2),18,Angle.Degrees(205),Angle.Degrees(325));p.Stroke();
            }
            else
            {
                Plate(p,27,22,166,129,new Color(.018f,.025f,.03f));
                Line(p,new Vector2(24,40),new Vector2(24,126),3,accent);Line(p,new Vector2(196,40),new Vector2(196,126),3,accent);
                if(type==TransmissionType.DCT){Line(p,new Vector2(11,57),new Vector2(11,109),4,rim);Line(p,new Vector2(209,57),new Vector2(209,109),4,rim);}
                else Line(p,new Vector2(49,156),new Vector2(171,156),2,rim);
            }
        }
    }
}
