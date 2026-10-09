using System;
using UnityEngine;
using DragLab.Infrastructure;

using DragLab.Domain;

namespace DragLab.Runtime
{
    // Runtime IMGUI instrumentation; one batched GL pass keeps full tick traces
    // inexpensive compared with a separate rotated GUI texture per segment.
    public sealed class EngineBenchDynoView : IDisposable
    {
        static readonly Color[] colors = { new Color(.2f, .9f, 1), new Color(1, .65f, .2f),
            new Color(.7f, .55f, 1), new Color(.4f, 1, .45f), new Color(1, .4f, .55f), new Color(1, .9f, .4f), new Color(.85f, .85f, 1) };
        readonly BenchPlotLines lines = new BenchPlotLines();
        bool rpmAxis, hp = true, nm = true, shaft = true;
        public void Draw(Rect area, EngineBenchDyno dyno, Action start, Action stop, Action export)
        {
            Color prior = GUI.color; GUI.color = new Color(.04f, .06f, .08f, 1);
            GUI.DrawTexture(area, Texture2D.whiteTexture); GUI.color = prior;
            GUI.Box(area, GUIContent.none);
            float x = area.x + 10, y = area.y + 7;
            GUI.Label(new Rect(x, y, 180, 22), "LIVE DYNO / full run traces");
            if (GUI.Button(new Rect(x + 190, y, 90, 22), rpmAxis ? "Axis: RPM" : "Axis: time")) rpmAxis = !rpmAxis;
            hp = GUI.Toggle(new Rect(x + 290, y, 45, 22), hp, "HP");
            nm = GUI.Toggle(new Rect(x + 335, y, 65, 22), nm, SharedUnitSettings.TorqueLabel);
            shaft = GUI.Toggle(new Rect(x + 405, y, 100, 22), shaft, "Shaft " + SharedUnitSettings.TorqueLabel);
            if (GUI.Button(new Rect(x + 510, y, 66, 22), dyno.Active == null ? "Record" : "End run")) { if (dyno.Active == null) start(); else stop(); }
            if (GUI.Button(new Rect(x + 581, y, 65, 22), "Export")) export();
            if (GUI.Button(new Rect(x + 651, y, 65, 22), "Clear old")) dyno.ClearHistory();
            string current = dyno.Active == null ? "Capture stopped" : "Recording #" + dyno.Active.id + " / " + (dyno.Tick * DragMath.FixedStep).ToString("F2") + " s";
            GUI.Label(new Rect(x, y + 24, 700, 20), current + "   Continuous: thick HP / amber engine " + SharedUnitSettings.TorqueLabel + " / violet shaft " + SharedUnitSettings.TorqueLabel + " / lower RPM");
            var plot = new Rect(area.x + 48, area.y + 58, area.width - 96, area.height - 190);
            var rpmPlot = new Rect(plot.x, plot.yMax + 8, plot.width, 30);
            double maxX = rpmAxis ? 1000 : 1, maxHp = 1, maxNm = 1, minNm = 0;
            foreach (var run in dyno.History) if (run.visible) Bounds(run, ref maxX, ref maxHp, ref maxNm, ref minNm);
            if (dyno.Active != null) Bounds(dyno.Active, ref maxX, ref maxHp, ref maxNm, ref minNm);
            maxX = Nice(maxX); maxHp = Nice(maxHp); maxNm = Nice(maxNm);
            if (minNm < 0) minNm = -Nice(-minNm);
            double maxRpm = 1000;
            foreach (var run in dyno.History) if (run.visible) maxRpm = Math.Max(maxRpm, run.MaxRpm);
            if (dyno.Active != null) maxRpm = Math.Max(maxRpm, dyno.Active.MaxRpm);
            maxRpm = Nice(maxRpm);
            GUI.Box(plot, GUIContent.none);
            GUI.Box(rpmPlot, GUIContent.none);
            GUI.Label(new Rect(rpmPlot.x - 45, rpmPlot.y + 5, 45, 20), "RPM");
            GUI.Label(new Rect(rpmPlot.xMax + 3, rpmPlot.y - 5, 44, 20), maxRpm.ToString("F0"));
            GUI.Label(new Rect(rpmPlot.xMax + 3, rpmPlot.yMax - 12, 40, 20), "0");
            for (int i = 0; i <= 4; i++)
            {
                float fraction = i / 4f;
                GUI.Label(new Rect(plot.x - 45, plot.yMax - fraction * plot.height - 9, 44, 20), (maxHp * fraction).ToString("F0"));
                GUI.Label(new Rect(plot.xMax + 3, plot.yMax - fraction * plot.height - 9, 44, 20), DisplayUnits.Torque(minNm + (maxNm - minNm) * fraction, SharedUnitSettings.Current.torque).ToString("F0"));
                GUI.Label(new Rect(plot.x + fraction * plot.width - 15, rpmPlot.yMax + 1, 65, 20), (maxX * fraction).ToString(rpmAxis ? "F0" : "F1"));
            }
            GUI.Label(new Rect(area.x + 5, plot.y - 19, 40, 20), "HP");
            GUI.Label(new Rect(plot.xMax + 4, plot.y - 19, 42, 20), SharedUnitSettings.TorqueLabel);
            GUI.Label(new Rect(plot.center.x - 45, rpmPlot.yMax + 17, 135, 20), rpmAxis ? "RPM / time-ordered" : "Elapsed seconds");
            if (Event.current.type == EventType.Repaint)
            {
                lines.Begin();
                
                float scale = GUI.matrix.lossyScale.x;
                GL.Color(new Color(.4f, .5f, .6f, .3f));
                for (int i = 0; i <= 4; i++)
                {
                    float f = i / 4f;
                    Line(new Vector2(plot.x, plot.y + plot.height * f), new Vector2(plot.xMax, plot.y + plot.height * f), scale);
                    Line(new Vector2(plot.x + plot.width * f, plot.y), new Vector2(plot.x + plot.width * f, plot.yMax), scale);
                }
                foreach (var run in dyno.History) if (run.visible) { Trace(run, plot, maxX, maxHp, maxNm, minNm, scale, .65f); RpmTrace(run, rpmPlot, maxX, maxRpm, scale, .65f); }
                if (dyno.Active != null) { Trace(dyno.Active, plot, maxX, maxHp, maxNm, minNm, scale, 1); RpmTrace(dyno.Active, rpmPlot, maxX, maxRpm, scale, 1); }
                lines.End();
            }
            float legendY = area.yMax - 32;
            for (int i = 0; i < dyno.History.Count; i++)
            {
                var run = dyno.History[i]; Color previous = GUI.contentColor; GUI.contentColor = ColorFor(run);
                run.visible = GUI.Toggle(new Rect(x + i * 116, legendY, 115, 22), run.visible,
                    new GUIContent("#" + run.id + " / " + run.Samples[run.Samples.Count - 1].seconds.ToString("F1") + "s", run.label + " / " + run.endedBy));
                GUI.contentColor = previous;
            }
            if (dyno.Active != null)
            {
                var s = dyno.Active.Samples[dyno.Active.Samples.Count - 1];
                GUI.Label(new Rect(x, area.yMax - 54, 700, 20), dyno.Active.label + " / " + s.rpm.ToString("F0") + " RPM / " + s.horsepower.ToString("F0") + " HP / " + SharedUnitSettings.Torque(s.torqueNm, "F0") + " / G" + s.gear);
            }
            else if (dyno.History.Count > 0) GUI.Label(new Rect(x, area.yMax - 54, 700, 20), "Latest: #" + dyno.History[dyno.History.Count - 1].id + " " + dyno.History[dyno.History.Count - 1].label);
        }
        static Color ColorFor(EngineDynoRun run) { return colors[(run.id - 1) % colors.Length]; }
        void Bounds(EngineDynoRun run, ref double x, ref double power, ref double torque, ref double minTorque)
        {
            x = Math.Max(x, rpmAxis ? run.MaxRpm : run.Samples[run.Samples.Count - 1].seconds);
            if (hp) power = Math.Max(power, run.MaxHorsepower);
            if (nm) torque = Math.Max(torque, run.MaxEngineNm);
            if (shaft) torque = Math.Max(torque, run.MaxShaftNm);
            if (shaft) minTorque = Math.Min(minTorque, run.MinShaftNm);
        }
        static double Nice(double value) { double step = Math.Pow(10, Math.Floor(Math.Log10(value))) / 2; return Math.Ceiling(value / step) * step; }
        void Trace(EngineDynoRun run, Rect plot, double maxX, double maxHp, double maxNm, double minNm, float scale, float alpha)
        {
            Color color = ColorFor(run); color.a = alpha;
            if (hp) Plot(run, plot, maxX, s => s.horsepower / maxHp, color, 4);
            Color engineColor = Color.Lerp(color, new Color(1, .65f, .18f, alpha), .6f);
            Color shaftColor = Color.Lerp(color, new Color(.9f, .4f, 1, alpha), .6f);
            if (nm) Plot(run, plot, maxX, s => (s.torqueNm - minNm) / (maxNm - minNm), engineColor, 2.5f);
            if (shaft) Plot(run, plot, maxX, s => (s.shaftTorqueNm - minNm) / (maxNm - minNm), shaftColor, 1.5f);
        }
        static float Y(Rect plot, double fraction) { return plot.yMax - (float)fraction * plot.height; }
        void Plot(EngineDynoRun run, Rect plot, double maxX, Func<EngineDynoSample, double> value, Color color, float width)
        {
            lines.Series(run.Samples.Count, plot.width, i => new Vector2(plot.x + (float)((rpmAxis ? run.Samples[i].rpm : run.Samples[i].seconds) / maxX) * plot.width, Y(plot, value(run.Samples[i]))), color, width);
        }
        void RpmTrace(EngineDynoRun run, Rect plot, double maxX, double maxRpm, float scale, float alpha)
        {
            Color color = ColorFor(run); color.a = alpha; Plot(run, plot, maxX, s => s.rpm / maxRpm, color, 2);
        }
        void Line(Vector2 a, Vector2 b, float scale) { lines.Line(a, b, new Color(.4f, .5f, .6f, .3f), 1); }
        public void Dispose() { lines.Dispose(); }
    }
}
