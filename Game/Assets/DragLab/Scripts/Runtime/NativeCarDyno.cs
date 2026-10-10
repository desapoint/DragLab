using System;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        VisualElement OwnedCarDyno(OwnedCar car)
        {
            var panel = new VisualElement { name = "ownedCarDyno", pickingMode = PickingMode.Ignore };
            panel.AddToClassList("car-dyno");
            if (string.IsNullOrEmpty(car.engineAssemblyId)) {
                var missing = new Label("No engine attached"); missing.AddToClassList("car-warning"); panel.Add(missing); return panel;
            }
            try {
                var source = InspectionBuild.Capture(game.Profile,car);
                var stats = CarInspection.CalculatePrepared(source,CarInspection.PrepareCar(source),false);
                var healthySource = InspectionBuild.Capture(game.Profile,car,healthy:true);
                var healthy = CarInspection.CalculatePrepared(healthySource,CarInspection.PrepareCar(healthySource),false);
                TorqueUnit units = SharedUnitSettings.Current.torque;
                var legend = new VisualElement(); legend.AddToClassList("car-dyno-legend"); panel.Add(legend);
                DynoLegendGroup(legend,"Current",stats,units,false);
                DynoLegendGroup(legend,"No wear",healthy,units,true);
                panel.Add(new NativeDynoPlot(stats,healthy,units));
                panel.tooltip = "Estimated engine output at full load and steady boost. Separate HP and torque scales; applied hardware, tune and condition. Dashed curves remove part wear only for comparison; stored fluids and missing hardware stay. No race or recording.";
            } catch (Exception) { var unavailable = new Label("Dyno unavailable"); unavailable.AddToClassList("car-warning"); panel.Add(unavailable); }
            return panel;
        }
        static void DynoLegendGroup(VisualElement parent,string title,CarInspection stats,TorqueUnit units,bool healthy)
        {
            var group = new VisualElement { name = healthy?"dynoNoWearLegend":"dynoCurrentLegend", pickingMode = PickingMode.Ignore };
            group.AddToClassList("dyno-legend-group"); parent.Add(group);
            if(!healthy)group.AddToClassList("dyno-legend-current");
            var heading = new Label(title) { pickingMode = PickingMode.Ignore }; heading.AddToClassList("dyno-legend-title"); group.Add(heading);
            DynoLegend(group,"HP "+stats.peakHp.ToString("F0"),true,healthy,"Maximum at "+stats.hpRpm.ToString("F0")+" RPM");
            DynoLegend(group,"Torque "+DisplayUnits.Torque(stats.peakTorque,units).ToString("F0")+" "+DisplayUnits.TorqueLabel(units),false,healthy,"Maximum at "+stats.torqueRpm.ToString("F0")+" RPM");
        }
        static void DynoLegend(VisualElement parent,string text,bool hp,bool healthy,string tooltip)
        {
            var entry = new VisualElement { tooltip = tooltip, pickingMode = PickingMode.Ignore };
            entry.AddToClassList("dyno-legend-entry");
            entry.Add(new DynoLineSample(hp,healthy)); entry.Add(new Label(text) { pickingMode = PickingMode.Ignore }); parent.Add(entry);
        }
    }

    internal sealed class DynoLineSample : VisualElement
    {
        internal DynoLineSample(bool hp,bool healthy)
        {
            AddToClassList("dyno-line-sample"); pickingMode = PickingMode.Ignore;
            RegisterCallback<CustomStyleResolvedEvent>(_=>MarkDirtyRepaint());
            generateVisualContent += context => {
                var painter = context.painter2D; painter.strokeColor = NativeDynoPlot.CurveColor(this,hp,healthy); painter.lineWidth = hp?2.5f:1.8f;
                float width=contentRect.width,y=contentRect.height*.5f;
                for(float x=0;x<width;x+=healthy?9:width) {
                    painter.BeginPath(); painter.MoveTo(new Vector2(x,y)); painter.LineTo(new Vector2(healthy?Math.Min(x+5,width):width,y)); painter.Stroke();
                }
            };
        }
    }

    internal sealed class NativeDynoPlot : VisualElement
    {
        internal CarInspection Inspection { get; }
        internal CarInspection HealthyReference { get; }
        internal TorqueUnit Units { get; }
        internal Rect PlotRect => new Rect(Left,Top,contentRect.width-Left-Right,contentRect.height-Top-Bottom);
        internal double? HoverRpm { get; private set; }
        internal InspectionCurvePoint? HoverCurrent { get; private set; }
        internal InspectionCurvePoint? HoverHealthy { get; private set; }
        readonly VisualElement hoverCard;
        readonly Label hoverRpm, hoverCurrent, hoverHealthy;
        readonly double maxRpm, maxHp, maxTorque;
        const float Left = 38, Right = 46, Top = 20, Bottom = 26;
        static readonly CustomStyleProperty<Color> TorqueProperty = new CustomStyleProperty<Color>("--dyno-torque");
        static readonly CustomStyleProperty<Color> HealthyHpProperty = new CustomStyleProperty<Color>("--dyno-hp-reference");
        static readonly CustomStyleProperty<Color> HealthyTorqueProperty = new CustomStyleProperty<Color>("--dyno-torque-reference");
        internal static Color CurveColor(VisualElement element,bool hp,bool healthy)
            => healthy ? RacingPalette.Read(element,hp?HealthyHpProperty:HealthyTorqueProperty,hp?new Color32(116,191,255,255):new Color32(136,212,181,255))
                : hp ? RacingPalette.Accent(element) : RacingPalette.Read(element,TorqueProperty,new Color32(230,191,128,255));

        internal NativeDynoPlot(CarInspection stats,CarInspection healthy,TorqueUnit units)
        {
            name = "ownedDynoPlot"; focusable = false; tabIndex = -1; AddToClassList("car-dyno-plot");
            Inspection = stats; HealthyReference = healthy; Units = units;
            maxRpm = stats.source.build.spec.redlineRpm;
            maxHp = Math.Max(1,Math.Max(stats.peakHp,healthy?.peakHp??0))*1.08;
            maxTorque = Math.Max(1,Math.Max(stats.peakTorque,healthy?.peakTorque??0))*1.08;
            Axis("dynoHpScale",maxHp.ToString("F0"),true,true);
            Axis("dynoTorqueScale",DisplayUnits.Torque(maxTorque,units).ToString("F0"),false,true);
            Axis("dynoHpZero","0",true,false);
            Axis("dynoTorqueZero","0",false,false);
            var ticks = new VisualElement { pickingMode = PickingMode.Ignore }; ticks.AddToClassList("dyno-rpm-ticks"); Add(ticks);
            ticks.Add(new Label("0") { pickingMode = PickingMode.Ignore }); ticks.Add(new Label((maxRpm/2).ToString("F0")+" RPM") { pickingMode = PickingMode.Ignore }); ticks.Add(new Label(maxRpm.ToString("F0")) { pickingMode = PickingMode.Ignore });
            hoverCard = new VisualElement { name = "dynoHoverValues", pickingMode = PickingMode.Ignore }; hoverCard.AddToClassList("dyno-hover-card"); hoverCard.AddToClassList("hidden"); Add(hoverCard);
            hoverRpm = new Label { name = "dynoHoverRpm", pickingMode = PickingMode.Ignore };
            hoverCurrent = new Label { name = "dynoHoverCurrent", pickingMode = PickingMode.Ignore };
            hoverHealthy = new Label { name = "dynoHoverNoWear", pickingMode = PickingMode.Ignore };
            hoverCard.Add(hoverRpm); hoverCard.Add(hoverCurrent); if(healthy!=null)hoverCard.Add(hoverHealthy);
            RegisterCallback<PointerMoveEvent>(evt=>UpdateHover(evt.localPosition));
            RegisterCallback<PointerLeaveEvent>(_=>ClearHover());
            RegisterCallback<GeometryChangedEvent>(_=>{ClearHover();MarkDirtyRepaint();});
            RegisterCallback<CustomStyleResolvedEvent>(_=>MarkDirtyRepaint());
            generateVisualContent += Draw;
        }
        void UpdateHover(Vector2 point)
        {
            var plot=PlotRect;
            if(plot.width<=0 || plot.height<=0 || !plot.Contains(point)){ClearHover();return;}
            double rpm=(point.x-plot.xMin)/plot.width*maxRpm;
            HoverRpm=rpm; HoverCurrent=Sample(Inspection,rpm); HoverHealthy=Sample(HealthyReference,rpm);
            hoverRpm.text=rpm.ToString("F0")+" RPM";
            hoverCurrent.text="Current  "+Values(HoverCurrent);
            hoverHealthy.text="No wear  "+Values(HoverHealthy);
            hoverCard.RemoveFromClassList("hidden");
            float width=Math.Min(220,Math.Max(0,contentRect.width*.5f-16)),height=HealthyReference==null?48:68;
            hoverCard.style.width=width;
            float x=point.x+12;
            if(x+width>contentRect.width)x=point.x-width-12;
            hoverCard.style.left=Mathf.Clamp(x,0,Math.Max(0,contentRect.width-width));
            hoverCard.style.top=Mathf.Clamp(point.y+12,0,Math.Max(0,contentRect.height-height));
            MarkDirtyRepaint();
        }
        string Values(InspectionCurvePoint? sample)=>sample.HasValue?sample.Value.hp.ToString("F1")+" HP · "+DisplayUnits.Torque(sample.Value.torque,Units).ToString("F1")+" "+DisplayUnits.TorqueLabel(Units):"— HP · — "+DisplayUnits.TorqueLabel(Units);
        void ClearHover()
        {
            if(!HoverRpm.HasValue)return;
            HoverRpm=null;HoverCurrent=null;HoverHealthy=null;hoverCard.AddToClassList("hidden");MarkDirtyRepaint();
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
            if (HealthyReference!=null) { Curve(painter,HealthyReference,plot,true,true); Curve(painter,HealthyReference,plot,false,true); }
            Curve(painter,Inspection,plot,true,false); Curve(painter,Inspection,plot,false,false);
            if(HoverRpm.HasValue) {
                float x=plot.xMin+(float)(HoverRpm.Value/maxRpm)*plot.width;
                painter.lineWidth=1.2f;painter.strokeColor=new Color32(236,238,241,220);painter.BeginPath();painter.MoveTo(new Vector2(x,plot.yMin));painter.LineTo(new Vector2(x,plot.yMax));painter.Stroke();
                HoverDots(painter,HoverHealthy,plot,x,true);HoverDots(painter,HoverCurrent,plot,x,false);
            }
        }
        void HoverDots(Painter2D painter,InspectionCurvePoint? sample,Rect plot,float x,bool healthy)
        {
            if(!sample.HasValue)return;
            var point=sample.Value;
            foreach(bool hp in new[] {true,false}) {
                float y=plot.yMax-(float)(hp?point.hp/maxHp:point.torque/maxTorque)*plot.height;
                painter.fillColor=CurveColor(this,hp,healthy);painter.strokeColor=new Color32(15,20,25,255);painter.lineWidth=1.2f;
                painter.BeginPath();painter.Arc(new Vector2(x,y),3.5f,Angle.Degrees(0),Angle.Degrees(360));painter.ClosePath();painter.Fill();painter.Stroke();
            }
        }
        void Curve(Painter2D painter,CarInspection stats,Rect plot,bool hp,bool dashed)
        {
            painter.lineWidth = hp?2.5f:1.8f; painter.strokeColor = CurveColor(this,hp,dashed);
            float distance=0;
            for (int i=1;i<stats.curve.Count;i++) {
                var a=stats.curve[i-1]; var b=stats.curve[i];
                Vector2 From(InspectionCurvePoint point)=>new Vector2(plot.xMin+(float)(point.rpm/maxRpm)*plot.width,plot.yMax-(float)((hp?point.hp/maxHp:point.torque/maxTorque))*plot.height);
                var start=From(a); var end=From(b);
                if (!dashed) { painter.BeginPath(); painter.MoveTo(start); painter.LineTo(end); painter.Stroke(); }
                else {
                    float length=Vector2.Distance(start,end);
                    for(float d=0;d<length;) {
                        float phase=(distance+d)%9, step=Math.Min(length-d,(phase<5?5:9)-phase);
                        if(step<.001f)step=Math.Min(length-d,.001f);
                        if(phase<5) { painter.BeginPath(); painter.MoveTo(Vector2.Lerp(start,end,d/length)); painter.LineTo(Vector2.Lerp(start,end,(d+step)/length)); painter.Stroke(); }
                        d+=step;
                    }
                    distance+=length;
                }
            }
        }
    }
}
