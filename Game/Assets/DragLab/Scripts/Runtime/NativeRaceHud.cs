using System;
using System.Collections.Generic;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    // Instruments and touch presentation; PrototypeGame owns simulation and input consumption.
    internal sealed class NativeRaceHud : MonoBehaviour
    {
        PrototypeGame game;
        UIDocument document;
        PanelSettings panel;
        VisualElement root, dials;
        internal RaceDashboard Dashboard { get; private set; }
        internal RaceSteeringRim SteeringRim { get; private set; }
        Label phase, warning;
        Button pauseButton, readyButton, lineLockButton;
        readonly System.Collections.Generic.List<RacingSurface> hudSurfaces = new System.Collections.Generic.List<RacingSurface>();
        CarSimulation boundCar;
        SpeedUnit speedUnit;
        internal AnalogRaceGauge Speed { get; private set; }
        internal AnalogRaceGauge Rpm { get; private set; }
        internal RaceGearIndicator Gear { get; private set; }
        internal RaceShiftLamp ShiftLamp { get; private set; }
        internal NativeRacePedals Pedals { get; private set; }
        internal RaceShiftFlick ShiftFlick { get; private set; }
        readonly Dictionary<int,VisualElement> physicalContacts=new Dictionary<int,VisualElement>();
        readonly HashSet<int> physicalSeen=new HashSet<int>();
        readonly List<int> physicalReleased=new List<int>();
        internal void ReleaseInput(){physicalContacts.Clear();Pedals?.Release();ShiftFlick?.Release();}
        internal void PollTouches(IReadOnlyList<Touch> samples=null)
        {
            if(samples==null&&!NativeMobile.PhysicalRaceTouches)return;
            if(!Visible||!game.RaceTouchActive){ReleaseInput();return;}
            physicalSeen.Clear();int count=samples?.Count??Input.touchCount;
            for(int i=0;i<count;i++){
                var touch=samples==null?Input.GetTouch(i):samples[i];int id=touch.fingerId+PointerId.touchPointerIdBase;
                var point=new Vector2(touch.position.x,Screen.height-touch.position.y)/panel.scale;physicalSeen.Add(id);
                if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled){EndPhysical(id,point,touch.phase==TouchPhase.Canceled);continue;}
                if(touch.phase==TouchPhase.Began){
                    EndPhysical(id,point,true);
                    foreach(var pedal in new[]{Pedals.Clutch,Pedals.Brake,Pedals.Gas})
                        if(pedal.resolvedStyle.display!=DisplayStyle.None&&pedal.worldBound.Contains(point)&&pedal.StartContact(id,point.y,false)){physicalContacts[id]=pedal;break;}
                    if(!physicalContacts.ContainsKey(id)&&ShiftFlick.enabledInHierarchy&&ShiftFlick.worldBound.Contains(point)&&ShiftFlick.StartContact(id,point,false))physicalContacts[id]=ShiftFlick;
                }
                else if(physicalContacts.TryGetValue(id,out var target)){
                    if(target is RaceTouchPedal pedal)pedal.MoveContact(id,point.y);else ShiftFlick.MoveContact(id,point);
                }
            }
            physicalReleased.Clear();foreach(int id in physicalContacts.Keys)if(!physicalSeen.Contains(id))physicalReleased.Add(id);
            foreach(int id in physicalReleased)EndPhysical(id,Vector2.zero,true);
        }
        void EndPhysical(int id,Vector2 point,bool canceled)
        {
            if(!physicalContacts.TryGetValue(id,out var target))return;physicalContacts.Remove(id);
            if(target is RaceTouchPedal pedal)pedal.End(id);else if(canceled)ShiftFlick.CancelContact(id);else ShiftFlick.EndContact(id,point);
        }
        Button shiftUpButton, shiftDownButton;
        internal VisualElement Root => root;
        internal PanelSettings Panel => panel;
        internal bool Visible => root != null && !root.ClassListContains("hidden");
        internal static bool Supports(string route) => route == "Race";

        internal void Initialize(PrototypeGame owner)
        {
            game = owner;
            panel = Instantiate(Resources.Load<PanelSettings>("UI/NativePanel"));
            var host = new GameObject("Native race instruments"); host.transform.SetParent(transform);
            panel.sortingOrder -= 1;
            document = host.AddComponent<UIDocument>(); document.panelSettings = panel;
            root = new VisualElement { name = "raceHud", pickingMode = PickingMode.Ignore };
            root.AddToClassList("shell"); root.AddToClassList("race-hud"); root.AddToClassList("hidden");
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/NativePalette"));
            // The UXML has the same Resources basename. Enumerate the typed assets
            // instead of allowing an ambiguous single-resource lookup to skip the shared skin.
            var shared = Array.Find(Resources.LoadAll<StyleSheet>("UI"),sheet=>sheet.name=="NativeShell");
            if(shared==null)throw new InvalidOperationException("Missing shared native UI stylesheet.");
            root.styleSheets.Add(shared);
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/NativeRaceHud"));
            Dashboard = new RaceDashboard(); root.Add(Dashboard);
            document.rootVisualElement.Add(root);
            // Only explicit visible controls can receive pointer input.
            document.rootVisualElement.pickingMode = PickingMode.Ignore;
            dials = new VisualElement { name = "raceDials", pickingMode = PickingMode.Ignore };
            dials.AddToClassList("race-dials"); root.Add(dials);
            Speed = new AnalogRaceGauge("speedGauge"); Speed.AddToClassList("speed-gauge"); dials.Add(Speed);
            Rpm = new AnalogRaceGauge("rpmGauge"); Rpm.AddToClassList("rpm-gauge"); dials.Add(Rpm);
            ShiftLamp=new RaceShiftLamp();Rpm.Add(ShiftLamp);
            SteeringRim = new RaceSteeringRim(); root.Add(SteeringRim);
            Gear = new RaceGearIndicator(); root.Add(Gear);
            Pedals=new NativeRacePedals(game.RaceControls,()=>game.RaceTouchActive);root.Add(Pedals);
            shiftDownButton=HudButton("raceShiftDown","−",()=>{if(game.RaceTouchActive)game.RaceControls.NativeShift(-1);});
            shiftUpButton=HudButton("raceShiftUp","+",()=>{if(game.RaceTouchActive)game.RaceControls.NativeShift(1);});
            shiftDownButton.AddToClassList("race-shift-down");shiftUpButton.AddToClassList("race-shift-up");
            Gear.Add(shiftDownButton);Gear.Add(shiftUpButton);
            ShiftFlick=new RaceShiftFlick(game.RaceControls,()=>NativeMobile.Touch&&game.RaceTouchActive,()=>DrivingPreferences.ReverseTouchShifting,()=>game.Race?.player.transmission.type!=TransmissionType.CVT);Gear.Add(ShiftFlick);
            shiftDownButton.tooltip="Downshift";shiftUpButton.tooltip="Engage / upshift / restart";
            phase = new Label { name = "racePhase", pickingMode = PickingMode.Ignore }; phase.AddToClassList("race-phase"); root.Add(phase);
            warning = new Label { name = "raceWarning", pickingMode = PickingMode.Ignore }; warning.AddToClassList("race-warning"); root.Add(warning);
            pauseButton = HudButton("racePause", "", () => game.PauseRace(true)); pauseButton.AddToClassList("race-pause-button");pauseButton.tooltip="Pause";pauseButton.Add(new FilledMenuIcon("Pause"));
            readyButton = HudButton("raceReady", "Ready", () => game.Race?.BeginCountdown());readyButton.AddToClassList("race-ready-button");
            lineLockButton=HudButton("raceLineLock","Line lock",()=>{if(game.RaceTouchActive&&game.Race.warmup)game.Race.player.state.warmupLineLock=!game.Race.player.state.warmupLineLock;});lineLockButton.AddToClassList("race-line-lock");
            InterfaceComparison.SetReplacement("Race", true);
        }

        Button HudButton(string id,string caption,Action clicked)
        {
            var button=new Button(clicked){name=id,focusable=false,tabIndex=-1};button.AddToClassList("action-button");button.Add(new Label(caption){pickingMode=PickingMode.Ignore});
            var surface=new RacingSurface(button,RacingSurface.Kind.SecondaryAction);button.Insert(0,surface);hudSurfaces.Add(surface);root.Add(button);return button;
        }
        void LateUpdate() { Refresh(); foreach(var surface in hudSurfaces)surface.Tick(Visible); }
        internal void Refresh()
        {
            if (root == null) return;
            bool results = game.ComparisonRoute == "Results";
            bool show = game.Race != null && (Supports(game.ComparisonRoute) || game.NativeRacePause || results) && InterfaceComparison.IsReplacement(results ? "Results" : "Race");
            root.EnableInClassList("hidden", !show);
            if (!show) { ReleaseInput(); return; }
            int width = panel.targetTexture == null ? Screen.width : panel.targetTexture.width;
            int height = panel.targetTexture == null ? Screen.height : panel.targetTexture.height;
            panel.scale = NativeMenuShell.DisplayScale(width, height);
            bool compact=width/panel.scale<1150||height/panel.scale<700;
            root.EnableInClassList("compact",compact);
            var safe=NativeMobile.SafeArea;
            root.EnableInClassList("touch-ui",NativeMobile.Touch);
            float insetRight=(Screen.width-safe.xMax)/panel.scale,insetBottom=safe.yMin/panel.scale;
            float insetLeft=safe.xMin/panel.scale,insetTop=(Screen.height-safe.yMax)/panel.scale;
            bool mobile=NativeMobile.Touch;float center=(insetLeft+width/panel.scale-insetRight)*.5f;
            dials.style.width=mobile?new StyleLength(350):new StyleLength(StyleKeyword.Auto);
            dials.style.left=mobile?center-175:(compact?16:22)+insetLeft;dials.style.bottom=22+insetBottom;
            Dashboard.SetClusterCenter(mobile?(float?)center:null);SteeringRim.SetClusterCenter(mobile?(float?)center:null);
            pauseButton.style.top=readyButton.style.top=lineLockButton.style.top=18+insetTop;
            Gear.style.right=24+insetRight;Gear.style.bottom=15+insetBottom;
            Pedals.style.left=mobile?new StyleLength(16+insetLeft):new StyleLength(StyleKeyword.Auto);
            Pedals.style.right=mobile?new StyleLength(StyleKeyword.Auto):new StyleLength(260+insetRight);Pedals.style.bottom=24+insetBottom;
            pauseButton.style.right=24+insetRight;readyButton.style.right=80+insetRight;
            bool wide=NativeMobile.Touch&&width/panel.scale>=1600;root.EnableInClassList("touch-wide",wide);
            phase.style.left=warning.style.left=mobile?new StyleLength(center-200):new StyleLength(new Length(compact?42:44,LengthUnit.Percent));
            phase.style.right=warning.style.right=mobile?width/panel.scale-center-200:(compact?260:wide?660:520)+insetRight;
            phase.style.bottom=(mobile||compact?228:76)+insetBottom;warning.style.bottom=(mobile||compact?194:36)+insetBottom;
            pauseButton.SetEnabled(!game.NativeRacePause);
            pauseButton.EnableInClassList("hidden", game.Race.ResultReady);
            readyButton.EnableInClassList("hidden",!game.Race.warmup||game.NativeRacePause);
            readyButton.SetEnabled(game.Race.WarmupStopped&&!game.Race.player.state.engineFailed&&!game.Race.player.state.drivetrainFailed);
            var car = game.Race.player;
            bool inputActive=game.RaceTouchActive;
            lineLockButton.EnableInClassList("hidden",!NativeMobile.Touch||!game.Race.warmup||car.spec.drive==DriveType.AWD);
            lineLockButton.EnableInClassList("selected",car.state.warmupLineLock);lineLockButton.SetEnabled(inputActive);
            lineLockButton.style.right=190+insetRight;
            Pedals.Refresh(car,inputActive,game.RaceControls.Peek(),Time.unscaledDeltaTime);
            shiftUpButton.EnableInClassList("hidden",NativeMobile.Touch);shiftDownButton.EnableInClassList("hidden",NativeMobile.Touch);
            ShiftFlick.EnableInClassList("hidden",!NativeMobile.Touch);ShiftFlick.SetEnabled(NativeMobile.Touch&&inputActive);
            if(!inputActive||!NativeMobile.Touch)ShiftFlick.Release();
            shiftUpButton.SetEnabled(inputActive);shiftDownButton.SetEnabled(inputActive&&car.transmission.type!=TransmissionType.CVT);
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
            ShiftLamp.SetState(car);
            Gear.SetState(state.gear, car.transmission, car.spec.gears.Length, state.shiftRemaining, TransmissionDynamics.ShiftSeconds(car.transmission,car.spec,car.tune));
            string signal = game.NativeRacePause ? "PAUSED" : game.Race.warmup ? "WARM-UP" : state.disqualified ? "RED LIGHT" : game.Race.time < 1.5 ? "STAGED" : game.Race.time < 2 ? "AMBER 1" : game.Race.time < 2.5 ? "AMBER 2" : game.Race.time < 3 ? "AMBER 3" : "GREEN";
            if(state.finished&&!game.NativeRacePause)signal="FINISHED";
            Set(phase, signal + "  ·  " + state.distance.ToString("F1") + " / " + game.Race.distance.ToString("F1") + " m");
            string faults = state.engineFailed ? "ENGINE FAILED" : state.stalled ? "STALLED · CLUTCH + ENTER" : "";
            if (state.drivetrainFailed) faults += (faults.Length > 0 ? " · " : "") + "NO DRIVE PATH";
            if (state.brakesFailed) faults += (faults.Length > 0 ? " · " : "") + "BRAKE FAILURE";
            Set(warning, faults.Length > 0 ? faults : state.bogging ? "BOGGING" : state.limiter ? "REV LIMITER" : state.slip > .2 ? "WHEELSPIN" : "");
        }
        static void Set(Label label, string text) { if (label.text != text) label.text = text; }
        void OnDestroy() { ReleaseInput(); if (panel != null) Destroy(panel); }
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
                Vector2 point = GaugeFace.Point(size, Scale.Angle(i * Scale.MajorStep), Mathf.Clamp(.80f-42/size,.50f,.66f));
                numbers[i].style.left = point.x - 16; numbers[i].style.top = point.y - 11;
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

    internal sealed class RaceShiftLamp : GaugeInk
    {
        internal bool Installed { get; private set; }
        internal bool Lit { get; private set; }
        internal RaceShiftLamp()
        { name="raceShiftLamp";AddToClassList("gauge-shift-lamp");generateVisualContent+=Draw; }
        internal void SetState(CarSimulation car)
        {
            bool installed=car.tune.partShiftLight&&car.transmission.type!=TransmissionType.CVT;
            var state=car.state;
            bool lit=installed&&state.launched&&!state.engineFailed&&!state.stalled&&state.gear>0&&state.gear<car.spec.gears.Length&&state.shiftRemaining<=0&&state.rpm>=car.tune.ShiftLightRpm(car.spec);
            EnableInClassList("hidden",!installed);
            if(Installed!=installed||Lit!=lit){Installed=installed;Lit=lit;MarkDirtyRepaint();}
        }
        void Draw(MeshGenerationContext context)
        {
            if(!Installed)return;
            var p=context.painter2D;var center=contentRect.center;float r=Mathf.Min(contentRect.width,contentRect.height)*.36f;
            Circle(p,center,r+2,rim);Circle(p,center,r,face);
            Circle(p,center,r*.76f,Lit?new Color(1,.65f,.12f):new Color(.16f,.12f,.06f));
            if(Lit)Circle(p,center+new Vector2(-r*.18f,-r*.22f),r*.22f,new Color(1,.91f,.65f));
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

}
