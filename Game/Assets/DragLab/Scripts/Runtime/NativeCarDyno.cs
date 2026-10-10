using System;
using System.Linq;
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
                bool differs = stats.curve.Count == healthy.curve.Count && stats.curve.Zip(healthy.curve,(a,b)=>Math.Abs(a.hp-b.hp)>.1 || Math.Abs(a.torque-b.torque)>.1).Any(value=>value);
                TorqueUnit units = SharedUnitSettings.Current.torque;
                var legend = new VisualElement(); legend.AddToClassList("car-dyno-legend"); panel.Add(legend);
                var hp = new Label(stats.peakHp.ToString("F0")+" HP") { tooltip = "Peak at "+stats.hpRpm.ToString("F0")+" RPM" }; hp.AddToClassList("dyno-hp"); legend.Add(hp);
                var torque = new Label(DisplayUnits.Torque(stats.peakTorque,units).ToString("F0")+" "+DisplayUnits.TorqueLabel(units)) { tooltip = "Peak at "+stats.torqueRpm.ToString("F0")+" RPM" }; torque.AddToClassList("dyno-torque"); legend.Add(torque);
                if (differs) { var reference = new Label("-- No wear"); reference.AddToClassList("dyno-reference"); legend.Add(reference); }
                panel.Add(new NativeDynoPlot(stats,differs?healthy:null,units));
                panel.tooltip = "Estimated engine output at full load and steady boost. Separate HP and torque scales; applied hardware, tune and condition. Dashed curves remove part wear only for comparison; stored fluids and missing hardware stay. No race or recording.";
            } catch (Exception) { var unavailable = new Label("Dyno unavailable"); unavailable.AddToClassList("car-warning"); panel.Add(unavailable); }
            return panel;
        }
    }

    internal sealed class NativeDynoPlot : VisualElement
    {
        internal CarInspection Inspection { get; }
        internal CarInspection HealthyReference { get; }
        internal TorqueUnit Units { get; }
        readonly double maxRpm, maxHp, maxTorque;
        const float Left = 38, Right = 46, Top = 20, Bottom = 26;
        static readonly Color TorqueColor = new Color32(230,191,128,255), ReferenceColor = new Color32(138,165,187,255);

        internal NativeDynoPlot(CarInspection stats,CarInspection healthy,TorqueUnit units)
        {
            name = "ownedDynoPlot"; pickingMode = PickingMode.Ignore; AddToClassList("car-dyno-plot");
            Inspection = stats; HealthyReference = healthy; Units = units;
            maxRpm = stats.source.build.spec.redlineRpm;
            maxHp = Math.Max(1,Math.Max(stats.peakHp,healthy?.peakHp??0))*1.08;
            maxTorque = Math.Max(1,Math.Max(stats.peakTorque,healthy?.peakTorque??0))*1.08;
            Axis("dynoHpScale",maxHp.ToString("F0"),true,true);
            Axis("dynoTorqueScale",DisplayUnits.Torque(maxTorque,units).ToString("F0"),false,true);
            Axis("dynoHpZero","0",true,false);
            Axis("dynoTorqueZero","0",false,false);
            var ticks = new VisualElement(); ticks.AddToClassList("dyno-rpm-ticks"); Add(ticks);
            ticks.Add(new Label("0")); ticks.Add(new Label((maxRpm/2).ToString("F0")+" RPM")); ticks.Add(new Label(maxRpm.ToString("F0")));
            RegisterCallback<GeometryChangedEvent>(_=>MarkDirtyRepaint());
            RegisterCallback<CustomStyleResolvedEvent>(_=>MarkDirtyRepaint());
            generateVisualContent += Draw;
        }
        void Axis(string id,string text,bool left,bool top)
        {
            var label = new Label(text) { name = id, pickingMode = PickingMode.Ignore };
            label.AddToClassList("dyno-axis"); label.AddToClassList(left?"dyno-axis-left":"dyno-axis-right");
            label.AddToClassList(top?"dyno-axis-top":"dyno-axis-bottom"); Add(label);
        }
        void Draw(MeshGenerationContext context)
        {
            var plot = new Rect(Left,Top,contentRect.width-Left-Right,contentRect.height-Top-Bottom);
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
        }
        void Curve(Painter2D painter,CarInspection stats,Rect plot,bool hp,bool dashed)
        {
            painter.lineWidth = hp?2.5f:1.8f; painter.strokeColor = dashed?ReferenceColor:hp?RacingPalette.Accent(this):TorqueColor;
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
