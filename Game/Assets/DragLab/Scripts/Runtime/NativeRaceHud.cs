using System;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    // Read-only instruments. Simulation and input remain owned by PrototypeGame.
    internal sealed class NativeRaceHud : MonoBehaviour
    {
        PrototypeGame game;
        UIDocument document;
        PanelSettings panel;
        VisualElement root, dials;
        float pedalInset = -1;
        Label phase, warning;
        CarSimulation boundCar;
        SpeedUnit speedUnit;
        internal AnalogRaceGauge Speed { get; private set; }
        internal AnalogRaceGauge Rpm { get; private set; }
        internal RaceGearIndicator Gear { get; private set; }
        internal VisualElement Root => root;
        internal PanelSettings Panel => panel;
        internal bool Visible => root != null && !root.ClassListContains("hidden");
        internal static bool Supports(string route) => route == "Race";

        internal void Initialize(PrototypeGame owner)
        {
            game = owner;
            panel = Instantiate(Resources.Load<PanelSettings>("UI/NativePanel"));
            var host = new GameObject("Native race instruments"); host.transform.SetParent(transform);
            document = host.AddComponent<UIDocument>(); document.panelSettings = panel;
            root = new VisualElement { name = "raceHud", pickingMode = PickingMode.Ignore };
            root.AddToClassList("shell"); root.AddToClassList("race-hud"); root.AddToClassList("hidden");
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/NativePalette"));
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/NativeRaceHud"));
            document.rootVisualElement.Add(root);
            // The full-screen transparent tree must never intercept existing pedal hit tests.
            document.rootVisualElement.pickingMode = PickingMode.Ignore;
            dials = new VisualElement { name = "raceDials", pickingMode = PickingMode.Ignore };
            dials.AddToClassList("race-dials"); root.Add(dials);
            Speed = new AnalogRaceGauge("speedGauge"); Speed.AddToClassList("speed-gauge"); dials.Add(Speed);
            Rpm = new AnalogRaceGauge("rpmGauge"); Rpm.AddToClassList("rpm-gauge"); dials.Add(Rpm);
            Gear = new RaceGearIndicator(); root.Add(Gear);
            phase = new Label { name = "racePhase", pickingMode = PickingMode.Ignore }; phase.AddToClassList("race-phase"); root.Add(phase);
            warning = new Label { name = "raceWarning", pickingMode = PickingMode.Ignore }; warning.AddToClassList("race-warning"); root.Add(warning);
            InterfaceComparison.SetReplacement("Race", true);
        }

        void LateUpdate() => Refresh();
        internal void Refresh()
        {
            if (root == null) return;
            bool show = game.Race != null && Supports(game.ComparisonRoute) && InterfaceComparison.IsReplacement("Race");
            root.EnableInClassList("hidden", !show);
            if (!show) return;
            int width = panel.targetTexture == null ? Screen.width : panel.targetTexture.width;
            int height = panel.targetTexture == null ? Screen.height : panel.targetTexture.height;
            panel.scale = NativeMenuShell.DisplayScale(width, height);
            // Legacy pedals use their own scale until their later reviewed migration.
            float inset = 91 * RaceInputAdapter.Scale(width, height) / panel.scale + 16;
            if (Math.Abs(inset - pedalInset) > .01f)
            { pedalInset = inset; dials.style.bottom = inset; Gear.style.bottom = inset + 5; }
            root.EnableInClassList("compact", width / panel.scale < 1150 || height / panel.scale < 700);
            var car = game.Race.player;
            if (boundCar != car || speedUnit != SharedUnitSettings.Current.speed)
            {
                boundCar = car; speedUnit = SharedUnitSettings.Current.speed;
                BuildLimits.Capture(car, out double safeRpm, out _, out _, out _);
                // Safe hardware redline and electronic limiter/shift targets are different things.
                double redline = Math.Min(car.spec.redlineRpm, safeRpm);
                double rpmMax = Math.Ceiling(Math.Max(car.spec.redlineRpm * 1.15, redline + 1000) / 1000) * 1000;
                Rpm.Configure(new RaceGaugeScale(rpmMax, 1000, 1000, "RPM", redline));
                double gearedSpeed = car.transmission.type == TransmissionType.CVT
                    ? car.spec.redlineRpm / 60 * Math.PI * 2 * WheelProfile.DrivenRadius(car.spec,car.tune) / (car.transmission.cvtMinRatio * car.tune.finalDrive)
                    : DragMath.GearSpeedKmh(car.spec, car.tune, car.spec.gears.Length) / 3.6;
                double displayMax = DisplayUnits.Speed(Math.Max(gearedSpeed, 200 / 3.6), speedUnit);
                double step = speedUnit == SpeedUnit.MetresPerSecond ? 10 : speedUnit == SpeedUnit.MilesPerHour ? 20 : 40;
                Speed.Configure(new RaceGaugeScale(Math.Ceiling(displayMax / step) * step, step, 1, DisplayUnits.SpeedLabel(speedUnit)));
            }
            var state = car.state;
            Speed.SetValue(DisplayUnits.Speed(state.speed, speedUnit));
            Rpm.SetValue(state.rpm);
            Gear.SetState(state.gear, car.transmission.type);
            string signal = game.Race.warmup ? "WARM-UP" : state.disqualified ? "RED LIGHT" : game.Race.time < 1.5 ? "STAGED" : game.Race.time < 2 ? "AMBER 1" : game.Race.time < 2.5 ? "AMBER 2" : game.Race.time < 3 ? "AMBER 3" : "GREEN";
            Set(phase, signal + "  ·  " + state.distance.ToString("F1") + " / " + game.Race.distance.ToString("F1") + " m");
            string faults = state.engineFailed ? "ENGINE FAILED" : state.stalled ? "STALLED · CLUTCH + ENTER" : "";
            if (state.drivetrainFailed) faults += (faults.Length > 0 ? " · " : "") + "NO DRIVE PATH";
            if (state.brakesFailed) faults += (faults.Length > 0 ? " · " : "") + "BRAKE FAILURE";
            Set(warning, faults.Length > 0 ? faults : state.bogging ? "BOGGING" : state.limiter ? "REV LIMITER" : state.slip > .2 ? "WHEELSPIN" : "");
        }
        static void Set(Label label, string text) { if (label.text != text) label.text = text; }
        void OnDestroy() { if (panel != null) Destroy(panel); }
    }

    // Presentation configuration, separate from telemetry and mechanical tuning.
    // The same dial supports a partial circle or a semicircle by changing its sweep.
    internal sealed class RaceGaugeScale
    {
        internal readonly double Maximum, MajorStep, LabelDivisor, Redline;
        internal readonly string Unit;
        internal readonly float StartDegrees, SweepDegrees;
        internal RaceGaugeScale(double maximum, double majorStep, double labelDivisor, string unit,
            double redline = double.PositiveInfinity, float startDegrees = 135, float sweepDegrees = 270)
        {
            Maximum = Math.Max(1, maximum); MajorStep = Math.Max(1, majorStep); LabelDivisor = Math.Max(1, labelDivisor);
            Redline = Math.Max(0, redline); Unit = unit; StartDegrees = startDegrees; SweepDegrees = Mathf.Clamp(sweepDegrees, 1, 330);
        }
        internal float Angle(double value) => StartDegrees + SweepDegrees * Mathf.Clamp01((float)(value / Maximum));
    }

    internal sealed class AnalogRaceGauge : VisualElement
    {
        readonly GaugeFace face;
        readonly GaugeNeedle needle;
        readonly VisualElement numbers;
        readonly Label value, unit, factor;
        internal RaceGaugeScale Scale { get; private set; }
        internal double Value { get; private set; }
        internal string Readout => value.text;
        internal int FacePaints => face.PaintCount;
        internal int NeedlePaints => needle.PaintCount;
        internal AnalogRaceGauge(string id)
        {
            name = id; pickingMode = PickingMode.Ignore; AddToClassList("analog-gauge");
            face = new GaugeFace(); Add(face);
            numbers = new VisualElement { pickingMode = PickingMode.Ignore }; numbers.AddToClassList("gauge-layer"); Add(numbers);
            needle = new GaugeNeedle(); Add(needle);
            value = new Label { name = id + "Value", pickingMode = PickingMode.Ignore }; value.AddToClassList("gauge-value"); Add(value);
            unit = new Label { name = id + "Unit", pickingMode = PickingMode.Ignore }; unit.AddToClassList("gauge-unit"); Add(unit);
            factor = new Label { pickingMode = PickingMode.Ignore }; factor.AddToClassList("gauge-factor"); Add(factor);
            RegisterCallback<GeometryChangedEvent>(_ => PositionNumbers());
        }
        internal void Configure(RaceGaugeScale scale)
        {
            Scale = scale; face.Scale = scale; needle.Scale = scale; needle.MarkDirtyRepaint(); unit.text = scale.Unit;
            factor.text = scale.LabelDivisor == 1 ? "" : "×" + scale.LabelDivisor.ToString("0");
            numbers.Clear();
            for (double number = 0; number <= scale.Maximum + .01; number += scale.MajorStep)
            {
                var label = new Label((number / scale.LabelDivisor).ToString("0.#")) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("gauge-number"); if (number >= scale.Redline) label.AddToClassList("gauge-red-number"); numbers.Add(label);
            }
            PositionNumbers(); face.MarkDirtyRepaint(); SetValue(Value);
        }
        void PositionNumbers()
        {
            if (Scale == null) return;
            float size = Mathf.Min(contentRect.width, contentRect.height);
            for (int i = 0; i < numbers.childCount; i++)
            {
                Vector2 point = GaugeFace.Point(size, Scale.Angle(i * Scale.MajorStep), .68f);
                numbers[i].style.left = point.x - 20; numbers[i].style.top = point.y - 12;
            }
        }
        internal void SetValue(double next)
        {
            Value = double.IsNaN(next) || double.IsInfinity(next) ? 0 : Math.Max(0, next);
            string text = Value.ToString("F0"); if (value.text != text) value.text = text;
            needle.SetValue(Value);
        }
    }

    internal class GaugeInk : VisualElement
    {
        static readonly CustomStyleProperty<Color> AccentProperty = new CustomStyleProperty<Color>("--racing-accent");
        static readonly CustomStyleProperty<Color> FaceProperty = new CustomStyleProperty<Color>("--gauge-face");
        static readonly CustomStyleProperty<Color> RimProperty = new CustomStyleProperty<Color>("--gauge-rim");
        static readonly CustomStyleProperty<Color> TickProperty = new CustomStyleProperty<Color>("--gauge-tick");
        protected Color accent = new Color(.93f,.04f,.12f), face = new Color(.045f,.055f,.065f), rim = new Color(.43f,.47f,.5f), tick = new Color(.88f,.9f,.91f);
        protected GaugeInk()
        {
            pickingMode = PickingMode.Ignore; AddToClassList("gauge-layer");
            RegisterCallback<CustomStyleResolvedEvent>(_ => {
                if (customStyle.TryGetValue(AccentProperty, out var a)) accent = a;
                if (customStyle.TryGetValue(FaceProperty, out var f)) face = f;
                if (customStyle.TryGetValue(RimProperty, out var r)) rim = r;
                if (customStyle.TryGetValue(TickProperty, out var t)) tick = t;
                MarkDirtyRepaint();
            });
        }
        protected static void Circle(Painter2D p, Vector2 center, float radius, Color color)
        { p.fillColor = color; p.BeginPath(); p.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath(); p.Fill(); }
        protected static void Line(Painter2D p, Vector2 a, Vector2 b, float width, Color color)
        { p.strokeColor = color; p.lineWidth = width; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }
    }

    internal sealed class GaugeFace : GaugeInk
    {
        internal RaceGaugeScale Scale;
        internal int PaintCount { get; private set; }
        internal GaugeFace() { generateVisualContent += Draw; }
        internal static Vector2 Point(float size, float angle, float radius)
        { float a = angle * Mathf.Deg2Rad; return new Vector2(size * .5f + Mathf.Cos(a) * size * .5f * radius, size * .5f + Mathf.Sin(a) * size * .5f * radius); }
        void Draw(MeshGenerationContext context)
        {
            if (Scale == null) return;
            float size = Mathf.Min(contentRect.width, contentRect.height); if (size < 2) return;
            PaintCount++;
            var p = context.painter2D; var center = new Vector2(size * .5f, size * .5f);
            Circle(p, center, size * .497f, new Color(.015f,.02f,.025f,.94f));
            Circle(p, center, size * .48f, rim);
            Circle(p, center, size * .465f, face);
            // Two narrow reflected arcs give the bezel depth without a raster texture.
            p.lineWidth = 1.5f; p.strokeColor = new Color(1,1,1,.32f); p.BeginPath(); p.Arc(center,size*.48f,Angle.Degrees(198),Angle.Degrees(322));p.Stroke();
            if (Scale.Redline < Scale.Maximum)
            {
                p.lineWidth = size * .034f; p.strokeColor = accent; p.BeginPath();
                p.Arc(center, size * .423f, Angle.Degrees(Scale.Angle(Scale.Redline)), Angle.Degrees(Scale.Angle(Scale.Maximum))); p.Stroke();
            }
            int minorCount = Mathf.RoundToInt((float)(Scale.Maximum / Scale.MajorStep * 5));
            for (int i = 0; i <= minorCount; i++)
            {
                double v = Scale.Maximum * i / minorCount; float a = Scale.Angle(v); bool major = i % 5 == 0;
                Line(p, Point(size,a,major?.80f:.85f),Point(size,a,.90f),major?2:1,v >= Scale.Redline ? accent : tick);
            }
        }
    }

    internal sealed class GaugeNeedle : GaugeInk
    {
        internal RaceGaugeScale Scale;
        internal int PaintCount { get; private set; }
        double value = double.NaN;
        internal GaugeNeedle() { generateVisualContent += Draw; }
        internal void SetValue(double next) { if (value == next) return; value = next; MarkDirtyRepaint(); }
        void Draw(MeshGenerationContext context)
        {
            if (Scale == null) return;
            float size = Mathf.Min(contentRect.width,contentRect.height); if (size < 2) return;
            PaintCount++;
            float angle = Scale.Angle(value); var p = context.painter2D; var center = new Vector2(size*.5f,size*.5f);
            Line(p, GaugeFace.Point(size,angle+180,.14f),GaugeFace.Point(size,angle,.79f),3,accent);
            Circle(p,center,size*.061f,rim); Circle(p,center,size*.045f,face);
        }
    }

    internal sealed class RaceGearIndicator : GaugeInk
    {
        readonly Label gearLabel, typeLabel;
        internal string Readout => gearLabel.text;
        internal RaceGearIndicator()
        {
            name="raceShifter"; AddToClassList("race-shifter");
            gearLabel=new Label {name="raceGear",pickingMode=PickingMode.Ignore};gearLabel.AddToClassList("shifter-gear");Add(gearLabel);
            typeLabel=new Label {name="raceTransmission",pickingMode=PickingMode.Ignore};typeLabel.AddToClassList("shifter-type");Add(typeLabel);
            generateVisualContent+=Draw;
        }
        internal void SetState(int gear, TransmissionType type)
        {
            string text=gear==0?"N":type==TransmissionType.CVT?"D":gear.ToString();
            if(gearLabel.text!=text)gearLabel.text=text;
            string label=type==TransmissionType.CVT?"CVT":type==TransmissionType.Automatic?"AUTO":type==TransmissionType.DCT?"DCT":"MANUAL";
            if(typeLabel.text!=label)typeLabel.text=label;
        }
        void Draw(MeshGenerationContext context)
        {
            float w=contentRect.width;if(w<2)return;var p=context.painter2D;
            Line(p,new Vector2(w*.5f,75),new Vector2(w*.5f,144),14,rim);
            Circle(p,new Vector2(w*.5f,151),29,face);Circle(p,new Vector2(w*.5f,151),17,rim);
            Line(p,new Vector2(w*.5f,79),new Vector2(w*.5f,146),8,face);
            Circle(p,new Vector2(w*.5f,56),49,rim);Circle(p,new Vector2(w*.5f,56),46,face);
            p.lineWidth=2;p.strokeColor=accent;p.BeginPath();p.Arc(new Vector2(w*.5f,56),46,Angle.Degrees(205),Angle.Degrees(335));p.Stroke();
        }
    }
}
