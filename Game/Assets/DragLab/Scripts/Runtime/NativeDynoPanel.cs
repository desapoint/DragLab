using System;
using System.Linq;
using System.Collections.Generic;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal enum NativeDynoStyle { Primary, Alternative, Reference }

    // Read-only inspection inputs. Capturing player/draft state belongs to the caller.
    internal sealed class NativeDynoSeries
    {
        internal string Id { get; }
        internal string Title { get; }
        internal CarInspection Inspection { get; }
        internal NativeDynoStyle Style { get; }
        internal NativeDynoSeries(string id,string title,CarInspection inspection,NativeDynoStyle style)
        {
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(title))throw new ArgumentException("Dyno series needs an ID and title.");
            Id=id;Title=title;Inspection=inspection??throw new ArgumentNullException(nameof(inspection));Style=style;
        }
    }

    internal sealed class NativeDynoPanel : VisualElement
    {
        internal NativeDynoPlot Plot { get; }
        internal NativeDynoPanel(IEnumerable<NativeDynoSeries> series,TorqueUnit units,string plotName="dynoPlot")
        {
            var inputs=series?.ToArray()??throw new ArgumentNullException(nameof(series));
            if(inputs.Length==0||inputs.Any(item=>item==null)||inputs.Select(item=>item.Id).Distinct().Count()!=inputs.Length)
                throw new ArgumentException("Dyno needs non-null series with unique IDs.",nameof(series));
            pickingMode=PickingMode.Ignore;AddToClassList("dyno-panel");
            var legend=new VisualElement {pickingMode=PickingMode.Ignore};legend.AddToClassList("car-dyno-legend");Add(legend);
            for(int i=0;i<inputs.Length;i++)DynoLegendGroup(legend,inputs[i],units,i==inputs.Length-1);
            Plot=new NativeDynoPlot(Array.AsReadOnly(inputs),units,plotName);Add(Plot);
        }
        static void DynoLegendGroup(VisualElement parent,NativeDynoSeries series,TorqueUnit units,bool last)
        {
            var stats=series.Inspection;
            var group = new VisualElement { name = "dyno"+series.Id+"Legend", pickingMode = PickingMode.Ignore };
            group.AddToClassList("dyno-legend-group"); parent.Add(group);
            if(!last)group.AddToClassList("dyno-legend-current");
            var heading = new Label(series.Title) { enableRichText = false, pickingMode = PickingMode.Ignore }; heading.AddToClassList("dyno-legend-title"); group.Add(heading);
            DynoLegend(group,"HP "+stats.peakHp.ToString("F0"),true,series.Style,"Maximum at "+stats.hpRpm.ToString("F0")+" RPM");
            DynoLegend(group,"Torque "+DisplayUnits.Torque(stats.peakTorque,units).ToString("F0")+" "+DisplayUnits.TorqueLabel(units),false,series.Style,"Maximum at "+stats.torqueRpm.ToString("F0")+" RPM");
        }
        static void DynoLegend(VisualElement parent,string text,bool hp,NativeDynoStyle style,string tooltip)
        {
            var entry = new VisualElement { tooltip = tooltip, pickingMode = PickingMode.Ignore };
            entry.AddToClassList("dyno-legend-entry");
            entry.Add(new DynoLineSample(hp,style)); entry.Add(new Label(text) { pickingMode = PickingMode.Ignore }); parent.Add(entry);
        }
    }

    internal sealed class DynoLineSample : VisualElement
    {
        internal DynoLineSample(bool hp,NativeDynoStyle style)
        {
            AddToClassList("dyno-line-sample"); pickingMode = PickingMode.Ignore;
            RegisterCallback<CustomStyleResolvedEvent>(_=>MarkDirtyRepaint());
            generateVisualContent += context => {
                var painter = context.painter2D; painter.strokeColor = NativeDynoPlot.CurveColor(this,hp,style); painter.lineWidth = hp?2.5f:1.8f;
                float width=contentRect.width,y=contentRect.height*.5f;
                float cycle=style==NativeDynoStyle.Alternative?5:9,on=style==NativeDynoStyle.Alternative?3:5;
                for(float x=0;x<width;x+=style==NativeDynoStyle.Primary?width:cycle) {
                    painter.BeginPath(); painter.MoveTo(new Vector2(x,y)); painter.LineTo(new Vector2(style==NativeDynoStyle.Primary?width:Math.Min(x+on,width),y)); painter.Stroke();
                }
            };
        }
    }

    internal sealed class NativeDynoPlot : VisualElement
    {
        internal IReadOnlyList<NativeDynoSeries> Series { get; }
        internal IReadOnlyList<InspectionCurvePoint?> HoverSamples { get; }
        internal double RpmLimit => maxRpm;
        internal double HpScale => maxHp;
        internal double TorqueScale => maxTorque;
        internal TorqueUnit Units { get; }
        internal Rect PlotRect => new Rect(Left,Top,contentRect.width-Left-Right,contentRect.height-Top-Bottom);
        internal double? HoverRpm { get; private set; }

        readonly VisualElement hoverCard;
        readonly Label hoverRpm;
        readonly Label[] hoverLabels;
        readonly InspectionCurvePoint?[] hoverSamples;
        readonly double maxRpm, maxHp, maxTorque;
        Vector2 hoverPoint;
        const float Left = 38, Right = 46, Top = 20, Bottom = 26;
        static readonly CustomStyleProperty<Color> TorqueProperty = new CustomStyleProperty<Color>("--dyno-torque");
        static readonly CustomStyleProperty<Color> HealthyHpProperty = new CustomStyleProperty<Color>("--dyno-hp-reference");
        static readonly CustomStyleProperty<Color> HealthyTorqueProperty = new CustomStyleProperty<Color>("--dyno-torque-reference");
        static readonly CustomStyleProperty<Color> AlternativeHpProperty = new CustomStyleProperty<Color>("--dyno-hp-preview");
        static readonly CustomStyleProperty<Color> AlternativeTorqueProperty = new CustomStyleProperty<Color>("--dyno-torque-preview");
        internal static Color CurveColor(VisualElement element,bool hp,NativeDynoStyle style)
        {
            if(style==NativeDynoStyle.Reference)return RacingPalette.Read(element,hp?HealthyHpProperty:HealthyTorqueProperty,hp?new Color32(116,191,255,255):new Color32(136,212,181,255));
            if(style==NativeDynoStyle.Alternative)return RacingPalette.Read(element,hp?AlternativeHpProperty:AlternativeTorqueProperty,hp?new Color32(211,158,255,255):new Color32(244,177,112,255));
            return hp?RacingPalette.Accent(element):RacingPalette.Read(element,TorqueProperty,new Color32(230,191,128,255));
        }

        internal NativeDynoPlot(IReadOnlyList<NativeDynoSeries> series,TorqueUnit units,string plotName)
        {
            name = plotName; focusable = false; tabIndex = -1; AddToClassList("car-dyno-plot");
            Series = Array.AsReadOnly(series.ToArray()); Units = units;
            maxRpm = Series.Max(item=>Math.Max(item.Inspection.source.build.spec.redlineRpm,item.Inspection.curve.Count==0?0:item.Inspection.curve[item.Inspection.curve.Count-1].rpm));
            maxHp = Math.Max(1,Series.Max(item=>item.Inspection.peakHp))*1.08;
            maxTorque = Math.Max(1,Series.Max(item=>item.Inspection.peakTorque))*1.08;
            Axis("dynoHpScale",maxHp.ToString("F0"),true,true);
            Axis("dynoTorqueScale",DisplayUnits.Torque(maxTorque,units).ToString("F0"),false,true);
            Axis("dynoHpZero","0",true,false);
            Axis("dynoTorqueZero","0",false,false);
            var ticks = new VisualElement { pickingMode = PickingMode.Ignore }; ticks.AddToClassList("dyno-rpm-ticks"); Add(ticks);
            ticks.Add(new Label("0") { pickingMode = PickingMode.Ignore }); ticks.Add(new Label((maxRpm/2).ToString("F0")+" RPM") { pickingMode = PickingMode.Ignore }); ticks.Add(new Label(maxRpm.ToString("F0")) { pickingMode = PickingMode.Ignore });
            hoverCard = new VisualElement { name = "dynoHoverValues", pickingMode = PickingMode.Ignore }; hoverCard.AddToClassList("dyno-hover-card"); hoverCard.AddToClassList("hidden"); Add(hoverCard);
            hoverRpm = new Label { name = "dynoHoverRpm", pickingMode = PickingMode.Ignore };
            hoverCard.Add(hoverRpm);
            hoverLabels=new Label[Series.Count]; hoverSamples=new InspectionCurvePoint?[Series.Count]; HoverSamples=Array.AsReadOnly(hoverSamples);
            for(int i=0;i<Series.Count;i++) {
                hoverLabels[i]=new Label {name="dynoHover"+Series[i].Id,enableRichText=false,pickingMode=PickingMode.Ignore}; hoverCard.Add(hoverLabels[i]);
            }
            hoverCard.RegisterCallback<GeometryChangedEvent>(_=>{if(HoverRpm.HasValue)PositionHoverCard(hoverPoint);});
            RegisterCallback<PointerDownEvent>(evt=>{if(evt.pointerType==UnityEngine.UIElements.PointerType.touch){this.CapturePointer(evt.pointerId);UpdateHover(evt.localPosition);evt.StopPropagation();}});
            RegisterCallback<PointerUpEvent>(evt=>{if(evt.pointerType==UnityEngine.UIElements.PointerType.touch&&this.HasPointerCapture(evt.pointerId)){this.ReleasePointer(evt.pointerId);evt.StopPropagation();}});
            RegisterCallback<PointerCancelEvent>(evt=>{if(this.HasPointerCapture(evt.pointerId))this.ReleasePointer(evt.pointerId);ClearHover();});
            RegisterCallback<PointerMoveEvent>(evt=>{if(evt.pointerType!=UnityEngine.UIElements.PointerType.touch||this.HasPointerCapture(evt.pointerId))UpdateHover(evt.localPosition);});
            RegisterCallback<PointerLeaveEvent>(evt=>{if(evt.pointerType!=UnityEngine.UIElements.PointerType.touch)ClearHover();});
            RegisterCallback<GeometryChangedEvent>(_=>{ClearHover();MarkDirtyRepaint();});
            RegisterCallback<CustomStyleResolvedEvent>(_=>MarkDirtyRepaint());
            generateVisualContent += Draw;
        }
        void UpdateHover(Vector2 point)
        {
            var plot=PlotRect;
            if(plot.width<=0 || plot.height<=0 || !plot.Contains(point)){ClearHover();return;}
            double rpm=(point.x-plot.xMin)/plot.width*maxRpm;
            HoverRpm=rpm; hoverRpm.text=rpm.ToString("F0")+" RPM";
            for(int i=0;i<Series.Count;i++) {
                hoverSamples[i]=Sample(Series[i].Inspection,rpm);
                hoverLabels[i].text=Series[i].Title+"  "+Values(hoverSamples[i]);
            }
            hoverCard.RemoveFromClassList("hidden");
            hoverPoint=point;PositionHoverCard(point);
            MarkDirtyRepaint();
        }
        void PositionHoverCard(Vector2 point)
        {
            float width=Math.Min(220,Math.Max(0,contentRect.width*.5f-16));
            float height=hoverCard.layout.height>0?hoverCard.layout.height:28+20*Series.Count;
            hoverCard.style.width=width;
            float x=point.x+12;
            if(x+width>contentRect.width)x=point.x-width-12;
            hoverCard.style.left=Mathf.Clamp(x,0,Math.Max(0,contentRect.width-width));
            hoverCard.style.top=Mathf.Clamp(point.y+12,0,Math.Max(0,contentRect.height-height));
        }
        string Values(InspectionCurvePoint? sample)=>sample.HasValue?sample.Value.hp.ToString("F1")+" HP · "+DisplayUnits.Torque(sample.Value.torque,Units).ToString("F1")+" "+DisplayUnits.TorqueLabel(Units):"— HP · — "+DisplayUnits.TorqueLabel(Units);
        void ClearHover()
        {
            if(!HoverRpm.HasValue)return;
            HoverRpm=null;Array.Clear(hoverSamples,0,hoverSamples.Length);hoverCard.AddToClassList("hidden");MarkDirtyRepaint();
        }
        static InspectionCurvePoint? Sample(CarInspection stats,double rpm)
        {
            if(stats==null || stats.curve.Count==0 || rpm<stats.curve[0].rpm || rpm>stats.curve[stats.curve.Count-1].rpm)return null;
            for(int i=1;i<stats.curve.Count;i++) {
                var a=stats.curve[i-1];var b=stats.curve[i];if(rpm>b.rpm)continue;
                double f=(rpm-a.rpm)/(b.rpm-a.rpm);
                return new InspectionCurvePoint {rpm=rpm,hp=a.hp+(b.hp-a.hp)*f,torque=a.torque+(b.torque-a.torque)*f};
            }
            return stats.curve[0];
        }
        void Axis(string id,string text,bool left,bool top)
        {
            var label = new Label(text) { name = id, pickingMode = PickingMode.Ignore };
            label.AddToClassList("dyno-axis"); label.AddToClassList(left?"dyno-axis-left":"dyno-axis-right");
            label.AddToClassList(top?"dyno-axis-top":"dyno-axis-bottom"); Add(label);
        }
        void Draw(MeshGenerationContext context)
        {
            var plot = PlotRect;
            if (plot.width<=0 || plot.height<=0) return;
            var painter = context.painter2D;
            painter.fillColor = new Color32(15,20,25,150); painter.BeginPath();
            painter.MoveTo(plot.min); painter.LineTo(new Vector2(plot.xMax,plot.yMin)); painter.LineTo(plot.max); painter.LineTo(new Vector2(plot.xMin,plot.yMax)); painter.ClosePath(); painter.Fill();
            painter.lineWidth = 1; painter.strokeColor = new Color32(106,120,131,65);
            for (int i=0;i<=4;i++) {
                float f=i/4f; painter.BeginPath(); painter.MoveTo(new Vector2(plot.xMin,plot.yMin+plot.height*f)); painter.LineTo(new Vector2(plot.xMax,plot.yMin+plot.height*f)); painter.Stroke();
                painter.BeginPath(); painter.MoveTo(new Vector2(plot.xMin+plot.width*f,plot.yMin)); painter.LineTo(new Vector2(plot.xMin+plot.width*f,plot.yMax)); painter.Stroke();
            }
            // References are drawn behind the applied build, independent of legend order.
            foreach(var item in Series.OrderBy(item=>item.Style==NativeDynoStyle.Primary?1:0)) {
                Curve(painter,item.Inspection,plot,true,item.Style); Curve(painter,item.Inspection,plot,false,item.Style);
            }
            if(HoverRpm.HasValue) {
                float x=plot.xMin+(float)(HoverRpm.Value/maxRpm)*plot.width;
                painter.lineWidth=1.2f;painter.strokeColor=new Color32(236,238,241,220);painter.BeginPath();painter.MoveTo(new Vector2(x,plot.yMin));painter.LineTo(new Vector2(x,plot.yMax));painter.Stroke();
                for(int i=Series.Count-1;i>=0;i--)HoverDots(painter,hoverSamples[i],plot,x,Series[i].Style);
            }
        }
        void HoverDots(Painter2D painter,InspectionCurvePoint? sample,Rect plot,float x,NativeDynoStyle style)
        {
            if(!sample.HasValue)return;
            var point=sample.Value;
            foreach(bool hp in new[] {true,false}) {
                float y=plot.yMax-(float)(hp?point.hp/maxHp:point.torque/maxTorque)*plot.height;
                painter.fillColor=CurveColor(this,hp,style);painter.strokeColor=new Color32(15,20,25,255);painter.lineWidth=1.2f;
                painter.BeginPath();painter.Arc(new Vector2(x,y),3.5f,Angle.Degrees(0),Angle.Degrees(360));painter.ClosePath();painter.Fill();painter.Stroke();
            }
        }
        void Curve(Painter2D painter,CarInspection stats,Rect plot,bool hp,NativeDynoStyle style)
        {
            painter.lineWidth = hp?2.5f:1.8f; painter.strokeColor = CurveColor(this,hp,style);
            float distance=0;
            for (int i=1;i<stats.curve.Count;i++) {
                var a=stats.curve[i-1]; var b=stats.curve[i];
                Vector2 From(InspectionCurvePoint point)=>new Vector2(plot.xMin+(float)(point.rpm/maxRpm)*plot.width,plot.yMax-(float)((hp?point.hp/maxHp:point.torque/maxTorque))*plot.height);
                var start=From(a); var end=From(b);
                if (style==NativeDynoStyle.Primary) { painter.BeginPath(); painter.MoveTo(start); painter.LineTo(end); painter.Stroke(); }
                else {
                    float length=Vector2.Distance(start,end);
                    for(float d=0;d<length;) {
                        float cycle=style==NativeDynoStyle.Alternative?5:9,on=style==NativeDynoStyle.Alternative?3:5;
                        float phase=(distance+d)%cycle, step=Math.Min(length-d,(phase<on?on:cycle)-phase);
                        if(step<.001f)step=Math.Min(length-d,.001f);
                        if(phase<on) { painter.BeginPath(); painter.MoveTo(Vector2.Lerp(start,end,d/length)); painter.LineTo(Vector2.Lerp(start,end,(d+step)/length)); painter.Stroke(); }
                        d+=step;
                    }
                    distance+=length;
                }
            }
        }
    }
}
