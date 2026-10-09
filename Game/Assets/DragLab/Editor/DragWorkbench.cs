using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;

namespace DragLab.Editor
{
    public sealed partial class DragWorkbench : EditorWindow
    {
        VehicleDefinition vehicle;
        Tune tune = new Tune();
        BenchmarkResult result;
        string message;
        Vector2 scroll;
        bool sweepTraction = true;
        List<TuneTrial> trials;
        string auditionPath;

        [MenuItem("DragLab/Vehicle Workbench")]
        static void Open() { GetWindow<DragWorkbench>("DragLab Workbench"); }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Vehicle development workbench", EditorStyles.boldLabel);
            if (GUILayout.Button("Open standalone engine / sound test scene")) ProjectSetup.OpenEngineTest();
            DrawContactLab();
            var selected = (VehicleDefinition)EditorGUILayout.ObjectField("Vehicle", vehicle, typeof(VehicleDefinition), false);
            if (selected != vehicle) { vehicle = selected; tune = vehicle != null ? vehicle.CreateStockTune() : new Tune(); result = null; trials = null; }
            if (vehicle == null) { EditorGUILayout.HelpBox("Select a Vehicle Definition from Resources/Data/Vehicles. Use its Inspector to change engine, gearing or mass.", MessageType.Info); return; }
            string invalid = vehicle.spec.Validate();
            if (invalid != null) { EditorGUILayout.HelpBox(invalid, MessageType.Error); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawIntegratedTools();
            if (GUILayout.Button("Select definition in Inspector")) { Selection.activeObject = vehicle; EditorGUIUtility.PingObject(vehicle); }
            EditorGUILayout.HelpBox(vehicle.spec.dataProvenance, MessageType.Info);
            tune.engineLevel = EditorGUILayout.IntSlider("Engine stage", tune.engineLevel, 0, 3);
            tune.tireLevel = EditorGUILayout.IntSlider("Tire stage", tune.tireLevel, 0, 3);
            tune.weightLevel = EditorGUILayout.IntSlider("Weight stage", tune.weightLevel, 0, 3);
            tune.gearboxLevel = EditorGUILayout.IntSlider("Gearbox stage", tune.gearboxLevel, 0, 3);
            tune.exhaustLevel = EditorGUILayout.IntSlider("Exhaust stage (tone)", tune.exhaustLevel, 0, 3);
            tune.controlLevel = EditorGUILayout.IntSlider("Control package", tune.controlLevel, 0, 1);
            using (new EditorGUI.DisabledScope(tune.gearboxLevel == 0)) tune.transmissionUpgrade = (TransmissionUpgrade)EditorGUILayout.EnumPopup("Transmission swap", tune.transmissionUpgrade);
            using (new EditorGUI.DisabledScope(tune.controlLevel == 0))
            {
                tune.twoStep = (EquipmentSetting)EditorGUILayout.EnumPopup("Two-step", tune.twoStep);
                tune.launchPreload = (EquipmentSetting)EditorGUILayout.EnumPopup("Launch boost preload", tune.launchPreload);
                tune.antiLag = (EquipmentSetting)EditorGUILayout.EnumPopup("Shift/overrun anti-lag", tune.antiLag);
                tune.limiter = (LimiterStyle)EditorGUILayout.EnumPopup("Main limiter", tune.limiter);
                tune.limiterCut = (CutMethod)EditorGUILayout.EnumPopup("Main cut method", tune.limiterCut);
                tune.twoStepStyle = (LimiterStyle)EditorGUILayout.EnumPopup("Launch limiter style", tune.twoStepStyle);
                tune.twoStepCut = (CutMethod)EditorGUILayout.EnumPopup("Launch cut method", tune.twoStepCut);
                tune.revLimitRpm = EditorGUILayout.Slider("Main limit RPM", tune.revLimitRpm > 0 ? tune.revLimitRpm : vehicle.spec.redlineRpm, vehicle.spec.idleRpm + 1500, vehicle.spec.redlineRpm * 1.2f);
            }
            tune.coldStart = EditorGUILayout.Toggle("Cold engine", tune.coldStart);
            tune.wheelDiameter = EditorGUILayout.Slider("Wheel diameter multiplier", (float)tune.WheelDiameter, .8f, 1.6f);
            tune.wheelWidth = EditorGUILayout.Slider("Wheel width multiplier", (float)tune.WheelWidth, .8f, 1.6f);
            tune.wheelMass = EditorGUILayout.Slider("Wheel mass multiplier", (float)tune.WheelMass, .6f, 3);
            var wheels = new WheelProfile(vehicle.spec, tune);
            EditorGUILayout.LabelField("Rolling load", wheels.assemblyMass.ToString("F1") + " kg/wheel / " + wheels.effectiveMass.ToString("F0") + " kg effective mass");
            EditorGUILayout.LabelField("Factory fueling/transmission", (vehicle.spec.engineBehavior ?? new EngineBehaviorSpec()).fueling + " / " + (vehicle.spec.transmission ?? new TransmissionSpec()).type);
            if (GUILayout.Button("Export launch / limiter / wheel / gearbox checks")) { PowertrainVerification.Run(Directory.GetParent(Application.dataPath).Parent.FullName); message = "Powertrain reports and sound previews exported to reports/."; }
            using (new EditorGUI.DisabledScope(tune.engineLevel == 0))
                tune.inductionUpgrade = (EngineUpgradeInduction)EditorGUILayout.EnumPopup("Engine upgrade induction", tune.inductionUpgrade);
            tune.blowOffValve = (BlowOffType)EditorGUILayout.EnumPopup("Blow-off valve", tune.blowOffValve);
            var physical = new InductionProfile(vehicle.spec, tune);
            if (physical.turboCount > 0) tune.turboSize = EditorGUILayout.Slider("Turbo size (0 = stock 1.0)", (float)tune.TurboSize, .7f, 1.6f);
            if (physical.turboCount + physical.superchargerCount > 0) tune.boostSetting = EditorGUILayout.Slider("Boost multiplier", (float)tune.BoostSetting, .35f, 1.2f);
            if (physical.superchargerCount > 0) tune.superchargerPulley = EditorGUILayout.Slider("Supercharger pulley", (float)tune.SuperchargerPulley, .75f, 1.3f);
            var sound = new EngineSoundProfile(vehicle.spec, tune);
            EditorGUILayout.LabelField("Sound hardware", sound.cylinders + " cylinders / " + sound.litres.ToString("F1") + " L / " + sound.induction);
            EditorGUILayout.LabelField("Firing frequency at 3,000 RPM", sound.FiringHz(3000).ToString("F0") + " Hz (four stroke)");
            if (GUILayout.Button("Export 8-second sound audition WAV (idle / pull / shift / lift)"))
            {
                string path = EditorUtility.SaveFilePanel("Export generated sound", "", vehicle.spec.id + "-audition.wav", "wav");
                if (path.Length > 0) { EngineSoundAudition.WriteWav(path, EngineSoundAudition.Render(vehicle.spec, tune)); auditionPath = path; message = "Original procedural audition exported: " + path; }
            }
            if (!string.IsNullOrEmpty(auditionPath) && GUILayout.Button("Play exported audition in default audio app")) EditorUtility.OpenWithDefaultApp(auditionPath);
            tune.launchRpm = EditorGUILayout.Slider("Launch RPM", tune.launchRpm, vehicle.spec.idleRpm, vehicle.spec.redlineRpm - 200);
            tune.shiftRpm = EditorGUILayout.Slider("Auto shift RPM", tune.shiftRpm, vehicle.spec.idleRpm + 1500, vehicle.spec.redlineRpm - 100);
            tune.finalDrive = EditorGUILayout.Slider("Final drive", tune.finalDrive, 2, 6);
            tune.tirePressurePsi = EditorGUILayout.Slider("Tire pressure PSI", tune.tirePressurePsi, 18, 42);
            EditorGUILayout.LabelField("Peak horsepower", DragMath.PeakHorsepower(vehicle.spec, tune).ToString("F1") + " hp");
            EditorGUILayout.LabelField("Stock horsepower", DragMath.PeakHorsepower(vehicle.spec, vehicle.CreateStockTune()).ToString("F1") + " hp");
            EditorGUILayout.LabelField("Engine inertia", vehicle.spec.EngineInertia.ToString("F2") + " kg m2 (0 in Inspector = modeled default)");
            EditorGUILayout.LabelField("Clutch capacity", (vehicle.spec.ClutchCapacity * (1 + tune.gearboxLevel * .15)).ToString("F0") + " Nm (0 in Inspector = derived from stock torque)");
            EditorGUILayout.LabelField("Limiter recovery", vehicle.spec.LimiterDrop.ToString("F0") + " RPM below redline (0 in Inspector = 150)");
            Rect plot = GUILayoutUtility.GetRect(100, 170);
            DrawDyno(plot);
            EditorGUILayout.LabelField("Steady WOT: torque orange (Nm), power cyan (hp); dim = stock; green = unboosted build. Turbo lag affects actual passes.");
            if (GUILayout.Button("Export all induction dyno / response / race comparisons"))
            {
                string path = EditorUtility.SaveFilePanel("Export induction comparisons", "", "induction-comparison.csv", "csv");
                if (path.Length > 0) File.WriteAllText(path, InductionVerification.ComparisonCsv(vehicle.spec));
            }
            for (int gear = 1; gear <= vehicle.spec.gears.Length; gear++)
                EditorGUILayout.LabelField("Gear " + gear, "Redline " + DragMath.GearSpeedKmh(vehicle.spec, tune, gear).ToString("F1") + " km/h / shift suggestion " + DragMath.ShiftPoint(vehicle.spec, tune, gear).ToString("F0") + " RPM");
            if (GUILayout.Button("Run deterministic quarter-mile benchmark"))
            {
                tune.ClampTo(vehicle.spec); result = Benchmark.QuarterMile(vehicle.spec, tune);
                message = "Benchmark uses automatic launch, shifting and traction control. These settings do not modify the saved player car.";
            }
            if (result != null) EditorGUILayout.LabelField(result.performanceClass + " / " + result.points + " PP", result.elapsed.ToString("F3") + " s / " + result.trapKmh.ToString("F1") + " km/h");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export vehicle JSON")) ExportDefinition();
            if (GUILayout.Button("Import validated vehicle JSON")) ImportDefinition();
            if (GUILayout.Button("Export telemetry CSV")) ExportTelemetry();
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Export all-car power / tire / traction balance matrix")) ExportBalance();
            sweepTraction = EditorGUILayout.Toggle("Sweep traction control", sweepTraction);
            if (GUILayout.Button("Compare current build: launch RPM / final drive"))
            {
                trials = TuneExperiments.Sweep(vehicle.spec, tune, sweepTraction);
                message = "13 captured assisted quarter-mile experiments. Negative delta is faster than baseline. Player saves and factory data are unchanged.";
            }
            if (trials != null)
            {
                EditorGUILayout.LabelField("Captured experiments (rerun after edits)", EditorStyles.boldLabel);
                foreach (var trial in trials)
                    EditorGUILayout.LabelField(trial.label + " / " + trial.tune.launchRpm.ToString("F0") + " RPM / FD " + trial.tune.finalDrive.ToString("F2"),
                        trial.result.elapsed.ToString("F3") + " s / delta " + (trial.result.elapsed - trials[0].result.elapsed).ToString("+0.000;-0.000;0.000") + " s");
                if (GUILayout.Button("Export captured tune comparisons CSV"))
                {
                    string path = EditorUtility.SaveFilePanel("Export tune experiments", "", "tune-comparisons.csv", "csv");
                    if (path.Length > 0) { File.WriteAllText(path, TuneExperiments.Csv(trials)); message = "Captured comparisons exported: " + path; }
                }
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        void DrawDyno(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(.1f, .14f, .19f));
            double max = 1;
            for (double rpm = vehicle.spec.idleRpm; rpm <= vehicle.spec.redlineRpm; rpm += 50)
                max = Math.Max(max, Math.Max(DragMath.Torque(vehicle.spec, tune, rpm), DragMath.Horsepower(vehicle.spec, tune, rpm)));
            max *= 1.1;
            Handles.BeginGUI();
            var stock = vehicle.CreateStockTune();
            for (int curve = 0; curve < 6; curve++)
            {
                var points = new Vector3[101];
                for (int i = 0; i < points.Length; i++)
                {
                    double rpm = vehicle.spec.idleRpm + (vehicle.spec.redlineRpm - vehicle.spec.idleRpm) * i / 100.0;
                    Tune plotted = curve < 2 ? stock : tune;
                    double torque = curve >= 4 ? InductionDynamics.UnboostedTorque(vehicle.spec, plotted, rpm) : DragMath.Torque(vehicle.spec, plotted, rpm);
                    double y = curve % 2 == 0 ? torque : torque * rpm / 7127;
                    points[i] = new Vector3(r.x + r.width * i / 100, r.yMax - 10 - (float)(y / max) * (r.height - 20));
                }
                Handles.color = curve % 2 == 0 ? new Color(1, .62f, .25f) : Color.cyan;
                if (curve >= 4) Handles.color = curve % 2 == 0 ? Color.green : new Color(.35f, .6f, .35f);
                if (curve < 2) Handles.color = Handles.color * .4f;
                Handles.DrawAAPolyLine(2, points);
            }
            Handles.EndGUI();
        }
        void ExportDefinition()
        {
            string path = EditorUtility.SaveFilePanel("Export vehicle", "", vehicle.spec.id + ".json", "json");
            if (path.Length == 0) return;
            File.WriteAllText(path, JsonUtility.ToJson(vehicle.spec, true)); message = "Vehicle exported: " + path;
        }
        void ExportBalance()
        {
            string path = EditorUtility.SaveFilePanel("Export balance matrix", "", "draglab-balance.csv", "csv");
            if (path.Length == 0) return;
            File.WriteAllText(path, BalanceCsv()); message = (Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Length * 32) + " modeled combinations exported: " + path;
        }
        public static string BalanceCsv()
        {
            var csv = new StringBuilder("vehicle,engine_stage,tire_stage,traction_control,hp,quarter_s,trap_kmh,class,points\n");
            foreach (var definition in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
                for (int engine = 0; engine <= 3; engine++)
                    for (int tires = 0; tires <= 3; tires++)
                        for (int assist = 0; assist <= 1; assist++)
                        {
                            var tuned = definition.CreateStockTune(); tuned.engineLevel = engine; tuned.tireLevel = tires;
                            var pass = Benchmark.QuarterMile(definition.spec, tuned, new DriverSettings { tractionAssist = assist == 1 });
                            csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4:F2},{5:F3},{6:F2},{7},{8}\n",
                                definition.spec.id, engine, tires, assist, DragMath.PeakHorsepower(definition.spec, tuned), pass.elapsed, pass.trapKmh, pass.performanceClass, pass.points);
                        }
            return csv.ToString();
        }
        void ImportDefinition()
        {
            string path = EditorUtility.OpenFilePanel("Import vehicle", "", "json");
            if (path.Length == 0) return;
            try
            {
                VehicleSpec next = JsonUtility.FromJson<VehicleSpec>(File.ReadAllText(path));
                if (next == null || next.Validate() != null) throw new ArgumentException(next == null ? "Empty vehicle." : next.Validate());
                if (next.id != vehicle.spec.id) throw new ArgumentException("Keep the stable vehicle ID. Create a new definition for another car.");
                if (Resources.Load<TextAsset>("Sprites/Vehicles/" + next.artId + "/car-sprite") == null) throw new ArgumentException("Unknown art ID.");
                Undo.RecordObject(vehicle, "Import vehicle data");
                vehicle.spec = next; EditorUtility.SetDirty(vehicle); AssetDatabase.SaveAssets();
                tune = vehicle.CreateStockTune(); result = null; message = "Validated data imported. Undo is available.";
            }
            catch (Exception e) { message = "Import rejected: " + e.Message; }
        }
        void ExportTelemetry()
        {
            string path = EditorUtility.SaveFilePanel("Export telemetry", "", vehicle.spec.id + "-quarter.csv", "csv");
            if (path.Length == 0) return;
            var car = new CarSimulation(vehicle.spec, JsonUtility.FromJson<Tune>(JsonUtility.ToJson(tune)), new DriverSettings());
            car.Launch(0);
            var telemetry = new RaceTelemetry();
            double time = 0;
            while (car.state.distance < 402.336 && time < 90)
            {
                car.Step(DragMath.FixedStep, new DriverInput { throttle = 1 }); time += DragMath.FixedStep;
                telemetry.Record(car, time);
            }
            File.WriteAllText(path, telemetry.Csv()); message = "10 Hz telemetry including clutch slip and shaft torque exported: " + path;
        }
    }
}
