using System;
using System.Collections.Generic;
using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed class DrivingTraceView : IDisposable
    {
        readonly BenchPlotLines lines = new BenchPlotLines();
        static double Value(DrivingTraceSample s,int field) { double value=DrivingTraceChannels.All[field].Value(s);return field==2?DisplayUnits.Speed(s.speedKmh/3.6,SharedUnitSettings.Current.speed):field==3?DisplayUnits.Torque(s.shaftNm,SharedUnitSettings.Current.torque):field==7?DisplayUnits.Pressure(s.boost,SharedUnitSettings.Current.pressure):field==11?DisplayUnits.Pressure(s.oilPressureBar,SharedUnitSettings.Current.pressure):value; }

        public static string Label(int field){if(DrivingTraceChannels.All[field].group.StartsWith("Part "))return DrivingTraceChannels.All[field].label.Replace("Part ","").Replace(" health %","").Replace(" °C","");return field==2?DisplayUnits.SpeedLabel(SharedUnitSettings.Current.speed):field==3?"Shaft "+SharedUnitSettings.TorqueLabel:field==7?"Boost "+DisplayUnits.PressureLabel(SharedUnitSettings.Current.pressure):field==11?"Oil "+DisplayUnits.PressureLabel(SharedUnitSettings.Current.pressure):DrivingTraceChannels.All[field].label;}
        public void Draw(Rect rect, PhysicalDrivingSession current, List<PhysicalDrivingSession> history, int field)
        {
            Color prior = GUI.color; GUI.color = new Color(.025f, .035f, .05f, 1); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = prior;
            string fullLabel = DrivingTraceChannels.All[field].group.StartsWith("Part ") ? DrivingTraceChannels.All[field].label : Label(field);
            string liveValue = current.samples.Count == 0 ? "—" : Value(current.samples[current.samples.Count - 1], field).ToString("G5");
            GUI.Box(rect, fullLabel+" = "+liveValue+" / "+(current.Recording ? "120 Hz / current green / previous grey" : "Recording OFF / live diagnostics"));
            if (Event.current.type != EventType.Repaint) return;
            double duration = Math.Max(1, current.TotalTick / 120.0), maximum = 1, minimum = 0;
            foreach (var s in current.samples) { maximum = Math.Max(maximum, Value(s, field)); minimum = Math.Min(minimum, Value(s, field)); }
            foreach (var run in history) { duration = Math.Max(duration, run.TotalTick / 120.0); foreach (var s in run.samples) { maximum = Math.Max(maximum, Value(s, field)); minimum = Math.Min(minimum, Value(s, field)); } }
            var plot = new Rect(rect.x + 45, rect.y + 32, rect.width - 60, rect.height - 60);
            GUI.Label(new Rect(rect.x + 3, rect.y + 25, 42, 20), maximum.ToString("F0")); GUI.Label(new Rect(rect.x + 3, plot.yMax - 8, 42, 20), minimum.ToString("F0"));
            for (int i = 0; i <= 4; i++) GUI.Label(new Rect(plot.x + i * plot.width / 4 - 8, plot.yMax + 4, 64, 20), (duration * i / 4).ToString("F1") + " s");
            lines.Begin();
            for (int i = 0; i <= 4; i++)
            {
                float f = i / 4f; Color grid = new Color(.25f, .3f, .4f, .55f);
                lines.Line(new Vector2(plot.x, plot.y + plot.height * f), new Vector2(plot.xMax, plot.y + plot.height * f), grid, 1);
                lines.Line(new Vector2(plot.x + plot.width * f, plot.y), new Vector2(plot.x + plot.width * f, plot.yMax), grid, 1);
            }
            if (minimum < 0) { float zero = plot.yMax - (float)(-minimum / (maximum - minimum)) * plot.height; lines.Line(new Vector2(plot.x, zero), new Vector2(plot.xMax, zero), new Color(.6f, .6f, .6f, .8f), 1); }
            foreach (var previous in history) if (!ReferenceEquals(previous, current)) DrawRun(previous, plot, duration, minimum, maximum, field, new Color(.5f, .57f, .7f, .7f), 2);
            DrawRun(current, plot, duration, minimum, maximum, field, new Color(.25f, 1, .45f), 2.5f); lines.End();
        }
        void DrawRun(PhysicalDrivingSession run, Rect plot, double duration, double minimum, double maximum, int field, Color color, float width)
        {
            lines.Series(run.samples.Count, plot.width, i => new Vector2(plot.x + (float)(run.samples[i].time / duration) * plot.width,
                plot.yMax - (float)((Value(run.samples[i], field) - minimum) / (maximum - minimum)) * plot.height), color, width);
        }
        public void Dispose() { lines.Dispose(); }
    }
}

