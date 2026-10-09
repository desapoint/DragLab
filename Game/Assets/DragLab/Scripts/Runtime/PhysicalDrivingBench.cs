using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using DragLab.Presentation;
using DriveType = DragLab.Domain.DriveType;

namespace DragLab.Runtime
{
    public sealed partial class PhysicalDrivingBench : MonoBehaviour
    {
        public string GraphLabel { get { return DrivingTraceChannels.All[graph].label; } }
        public PhysicalDrivingSession Session { get; private set; }
        public EngineAudio Audio { get; private set; }
        public bool Paused { get; private set; }
        public bool KeyboardOwned { get; private set; }
        public bool Automated { get { return timeline != null; } }
        public DriverInput AppliedInput { get { return Session == null ? new DriverInput() : Session.LastInput; } }
        public bool Recording { get; private set; }
        public string Source { get { return timeline != null ? replay ? "REPLAY" : "SCENARIO" : KeyboardOwned ? "KEYBOARD / PEDALS" : "SLIDERS"; } }
        public readonly List<PhysicalDrivingSession> history = new List<PhysicalDrivingSession>();
        internal readonly Dictionary<string, Rect> buttonRects = new Dictionary<string, Rect>();
        internal readonly Dictionary<string, Rect> pedalRects = new Dictionary<string, Rect>();
        RunSnapshot config, lastRun;
        List<TickInput> timeline;
        bool replay, neutral = true, staging = true, upPending, downPending, mouseGas, mouseClutch, mouseBrake;
        bool clearGuiFocus;
        bool audioSettings;
        bool showDiagnostics;
        string continuationFolder;
        DriverInput sliders;
        double accumulator;
        VehicleDefinition[] vehicles;
        MechanicalCatalog catalog;
        LayeredCarView view;
        GUIStyle partProse;
        Vector2 configScroll, diagnosticsScroll;
        int selected, graph;
        int wearPart;
        internal readonly Dictionary<string,Rect> tireRects=new Dictionary<string,Rect>();
        internal void TirePanelForVerification(float scroll){wearPart=config.components.FindIndex(p=>p.slot=="Tires");configScroll=new Vector2(0,scroll);}
        internal void ElementPanelForVerification(string slot,float scroll){wearPart=config.components.FindIndex(p=>p.slot==slot);configScroll=new Vector2(0,scroll);testElement=0;showDiagnostics=false;}
        PartCategory category;
        string notice = "Isolated bench: no payouts or player inventory/condition changes.";
        string exportFolder;
        readonly DrivingTraceView traceView = new DrivingTraceView();
        void Start()
        {
            vehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v => v.spec.id).ToArray();
            catalog = new MechanicalCatalog(vehicles.Select(v => v.spec));
            var cameraObject = new GameObject("Physical bench camera"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 4.5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .105f, .14f); camera.transform.position = new Vector3(0, 0, -10); cameraObject.AddComponent<AudioListener>();
            view = new GameObject("Physical bench car").AddComponent<LayeredCarView>(); view.transform.position = new Vector3(3, .1f, 0);
            Audio = gameObject.AddComponent<EngineAudio>(); Audio.Initialize();
            exportFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../DrivingBenchExports"));
            var transferred = PhysicalBenchTransfer.Take();
            if (transferred == null) SelectVehicle(0);
            else { config = PhysicalDrivingSession.Copy(transferred); config.driving.throttleAssist = false; selected = Array.FindIndex(vehicles, v => v.spec.id == config.vehicle.id); ResetBench(); }
            DrivingBenchRuntimeVerification.AttachIfRequested(this);
        }
        public void SelectVehicle(int index)
        {
            selected = Mathf.Clamp(index, 0, vehicles.Length - 1); var definition = vehicles[selected];
            var isolated = PlayerProfile.New(DateTime.UtcNow.Ticks); isolated.cars.Clear(); isolated.selectedVehicleId = definition.spec.id;
            isolated.cars.Add(new OwnedCar { vehicleId = definition.spec.id, tune = definition.CreateStockTune() });
            isolated.driving = config == null ? new DriverSettings { launch = LaunchMode.Assisted, shifting = ShiftMode.Sequential, throttleAssist = false } : config.driving.Copy();
            MechanicsOwnership.Ensure(isolated, catalog); config = PhysicalDrivingSession.Copy(PhysicalBenchTransfer.Capture(isolated, isolated.SelectedCar));
            ResetBench(true);
        }
        public void ResetBench(bool preserveInputMode = false, bool resumeCapturedState = false)
        {
            bool wasKeyboard = KeyboardOwned; var assigned = sliders;
            if (!resumeCapturedState) { config.startingTick = 0; config.hasStartingDynamics = false; config.startingDynamics = null; config.inputs.Clear(); }
            Archive(); timeline = null; replay = false; Paused = KeyboardOwned = false; neutral = staging = true; sliders = new DriverInput(); upPending = downPending = false; accumulator = 0;
            if (preserveInputMode) { KeyboardOwned = wasKeyboard; sliders = assigned; }
            Session = new PhysicalDrivingSession(config, Recording); Audio.Configure(Session.car.spec, Session.car.tune);
            neutral = Session.car.state.gear == 0; staging = Session.car.state.stagingBrakesHeld;
            continuationFolder = null;
            view.Build(config.vehicle.artId, vehicles[Math.Max(0, selected)].defaultPaint);
            view.ApplySetup(Session.car.spec, Session.car.tune);
        }
        void Archive()
        {
            if (Session == null || !Session.Recording || Session.Tick == 0) return;
            lastRun = PhysicalDrivingSession.Copy(Session.snapshot);
            if (!history.Contains(Session)) history.Add(Session);
            while (history.Count > 6) history.RemoveAt(0);
        }
        public void StartScenario(PhysicalScenario scenario)
        {
            Recording = true;
            ResetBench(); timeline = PhysicalScenarios.Timeline(scenario, config.seed); notice = "Scenario " + scenario + " / seed " + config.seed;
        }
        public bool ReplayLast()
        {
            if (lastRun == null) Archive(); if (lastRun == null || lastRun.inputs.Count == 0) return false;
            var captured = PhysicalDrivingSession.Copy(lastRun); config = PhysicalDrivingSession.Copy(captured); config.inputs.Clear();
            Recording = true;
            selected = Array.FindIndex(vehicles, v => v.spec.id == config.vehicle.id);
            ResetBench(resumeCapturedState: true); timeline = captured.inputs; replay = true; notice = "Replaying captured build, condition, seed and exact inputs."; return true;
        }
        public void SetPaused(bool value) { Paused = value; accumulator = 0; if (value) Audio.Silence(); }
        public void SetRecording(bool value)
        {
            if (Recording == value) return;
            if (timeline != null) EndRun();
            Archive(); Session = Session.ContinueRecording(value); Recording = value;
            notice = value ? "Recording from current physical state; completed ten-minute segments export automatically." : "Recording off: physics continues, no timeline or disk capture.";
        }
        public void ResetWear()
        {
            Archive(); Session.car.mechanics.ResetWear(Session.car.state);
            if (Session.car.state.fuelLitres > 0 && Session.car.state.rpm < Session.car.spec.idleRpm) Session.car.state.rpm = Session.car.spec.idleRpm;
            if (Session.car.state.fuelLitres <= 0) { Session.car.state.stalled = true; Session.car.state.engineSupplyAvailable = false; }
            config.components = Session.car.mechanics.parts.Select(p => p.Copy()).ToList();
            timeline = null; Session = Session.ContinueRecording(Recording);
            notice = "Isolated wear/heat repaired; road momentum and pedal ownership retained.";
        }
        public bool SetStartingCondition(int index, float health, double temperature)
        {
            if ((!Paused && Session.car.state.launched) || index < 0 || index >= config.components.Count || !MechanicsSchema.Finite(health) || health < 0 || health > 1 || !MechanicsSchema.Finite(temperature) || temperature < -50 || temperature > 1500) return false;
            var c = config.components[index].condition; c.health = health; foreach(var element in c.elements){element.health=health;element.fault=ElementFault.None;element.severity=0;element.wearRemainder=0;} c.temperatureC = temperature; c.failed = health == 0; c.failureReason = c.failed ? "Assigned failed test part" : ""; if(config.components[index].slot=="Tires")c.tires=new TireCondition[0];
            ResetBench(true); return true;
        }
        public bool SetStartingTire(int wheel, float health, double temperature)
        {
            int index=config.components.FindIndex(p=>p.slot=="Tires");
            if(index<0||wheel<0||wheel>3||(!Paused&&Session.car.state.launched)||!MechanicsSchema.Finite(health)||health<0||health>1||!MechanicsSchema.Finite(temperature)||temperature< -50||temperature>1500)return false;
            var c=config.components[index].condition;TireDynamics.Ensure(c);var tire=c.tires[wheel];tire.health=health;tire.consumedLife=1-health;tire.temperatureC=temperature;tire.failed=health==0;
            c.health=c.tires.Min(t=>t.health);c.failed=c.tires.Any(t=>t.failed);c.temperatureC=c.tires.Max(t=>t.temperatureC);c.failureReason=c.failed?"Assigned failed test tire":"";
            ResetBench(true);notice="Assigned "+new[]{"FL","FR","RL","RR"}[wheel]+" tire life/temperature. Isolated test reset; pedal source retained.";return true;
        }
        internal void ContinueLiveRecording()
        {
            if (continuationFolder == null) continuationFolder = Path.Combine(exportFolder, "continuous-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            try
            {
                Directory.CreateDirectory(continuationFolder); string name = Path.Combine(continuationFolder, Session.snapshot.startingTick.ToString("D12"));
                File.WriteAllText(name + ".csv", Session.Csv()); File.WriteAllText(name + ".json", JsonUtility.ToJson(Session.snapshot, true));
                notice = "Driving continues; full segment and replay exported: " + continuationFolder;
            }
            catch (Exception e) { notice = "Driving continues; segment export failed: " + e.Message; }
            Archive(); Session = Session.ContinueRecording();
        }
        public void StartMoneyShiftTest()
        {
            ApplyTestPart(config.vehicle.id + "/Gearbox/Manual", false);
            config.driving.ApplyPreset(DrivingPreset.Manual); config.driving.clutchReleaseSeconds = .08f; Recording = true;
            ResetBench(); timeline = PhysicalScenarios.MoneyShiftTimeline(config); showDiagnostics = true;
            notice = "Repeatable manual money shift: no injected RPM/damage. Clutch re-engagement can overrev the engine.";
        }
        public void UseSliders() { timeline = null; KeyboardOwned = false; sliders = AppliedInput; sliders.shiftUp = sliders.shiftDown = sliders.launch = false; neutral = Session.car.state.gear == 0; staging = Session.car.state.stagingBrakesHeld; }
        void ManualCommand() { if (timeline != null) UseSliders(); }
        public void EndRun() { Archive(); SetPaused(true); if (timeline != null) { sliders = AppliedInput; sliders.shiftUp = sliders.shiftDown = sliders.launch = false; neutral = Session.car.state.gear == 0; staging = Session.car.state.stagingBrakesHeld; } timeline = null; }
        void Update()
        {
            if (Session == null) return;
            if (Input.GetKeyDown(KeyCode.R)) ResetBench();
            if (Input.GetKeyDown(KeyCode.Escape)) SetPaused(!Paused);
            if (Paused) return;
            bool keyboard = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.S) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.N);
            if (keyboard)
            {
                if (timeline != null) { timeline = null; neutral = Session.car.state.gear == 0; staging = Session.car.state.stagingBrakesHeld; }
                if (!KeyboardOwned) clearGuiFocus = true;
                KeyboardOwned = true;
            }
            upPending |= Input.GetKeyDown(KeyCode.E); downPending |= Input.GetKeyDown(KeyCode.Q);
            if (Input.GetKeyDown(KeyCode.Return)) sliders.launch = true;
            if (Input.GetKeyDown(KeyCode.N)) { neutral = true; staging = false; }
            if (mouseGas || mouseClutch || mouseBrake) { timeline = null; KeyboardOwned = true; }
            accumulator += Math.Min(Time.unscaledDeltaTime, .1f);
            while (accumulator >= DragMath.FixedStep && !Paused)
            {
                if (timeline != null)
                {
                    if (Session.Tick >= timeline.Count) { EndRun(); break; }
                    Session.Step(timeline[(int)Session.Tick]);
                    if (Session.Tick >= timeline.Count) EndRun();
                }
                else
                {
                    if (Recording && Session.Tick >= PhysicalDrivingSession.MaximumTicks) ContinueLiveRecording();
                    var input = KeyboardOwned ? new DriverInput { throttle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Space) || mouseGas ? 1 : 0, clutch = Input.GetKey(KeyCode.LeftShift) || mouseClutch ? 1 : 0, brake = Input.GetKey(KeyCode.S) || mouseBrake ? 1 : 0 } : sliders;
                    input.shiftUp = upPending; input.shiftDown = downPending;
                    if (upPending && Session.car.state.gear == 0) { neutral = false; staging = false; }
                    if (downPending && Session.car.state.gear == 1) { neutral = true; staging = false; input.shiftDown = false; }
                    input.launch |= sliders.launch; sliders.launch = false; upPending = downPending = false;
                    Session.Step(new TickInput { tick = Session.Tick, input = input, neutral = neutral, staging = staging });
                }
                accumulator = Math.Max(0, accumulator - DragMath.FixedStep);
            }
            view.Animate(Session.car.state); Audio.Render(Session.car.state, !Paused, 1);
        }
        public void Export(string folder = null)
        {
            if (!Recording) { notice = "Enable recording to export an input timeline. Recording is off."; return; }
            folder = folder ?? exportFolder; Directory.CreateDirectory(folder); string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            File.WriteAllText(Path.Combine(folder, id + ".csv"), Session.Csv());
            File.WriteAllText(Path.Combine(folder, id + ".json"), JsonUtility.ToJson(Session.snapshot, true)); notice = "CSV + replay JSON exported to " + folder;
        }
        public bool Import(string path)
        {
            try
            {
                var captured = JsonUtility.FromJson<RunSnapshot>(File.ReadAllText(path)); string invalid = PhysicalDrivingSession.Validate(captured);
                if (invalid != null) throw new ArgumentException(invalid);
                int importedVehicle = Array.FindIndex(vehicles, v => v.spec.id == captured.vehicle.id);
                if (importedVehicle < 0 || captured.installed == null || captured.components.Count == 0 || captured.components.Any(p => catalog.Find(p.definitionId) == null)) throw new ArgumentException("Replay needs a known vehicle and captured component definitions.");
                selected = importedVehicle; Recording = true;
                Archive(); config = PhysicalDrivingSession.Copy(captured); config.inputs.Clear(); ResetBench(resumeCapturedState: true); timeline = captured.inputs; replay = true; return true;
            }
            catch (Exception exception) { notice = exception.Message; return false; }
        }
        bool Button(string key, string label)
        {
            bool clicked = GUILayout.Button(label);
            if (Event.current.type == EventType.Repaint) { var rect = GUILayoutUtility.GetLastRect(); buttonRects[key] = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size * GUI.matrix.m00); }
            return clicked;
        }
        void OnGUI()
        {
            if (Session == null) return;
            RuntimeGuiRect.BeginFrame();
            if (partProse == null) partProse = new GUIStyle(GUI.skin.label) { wordWrap = true };
            if (clearGuiFocus) { GUI.FocusControl(null); clearGuiFocus = false; }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space) Event.current.Use();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f); GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(12, 12, 480, 696), GUI.skin.box);
            GUILayout.Label("PHYSICAL DRIVING BENCH / " + Source + (Paused ? " / PAUSED" : ""));
            GUILayout.Label("Shared car physics / " + MechanicsSchema.Model + " / 120 Hz");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Garage")) SceneManager.LoadScene("FirstSlice");
            if (GUILayout.Button(audioSettings ? "Close audio settings" : "Audio settings")) audioSettings = !audioSettings;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Fixed RPM / load / acoustic overrides → sound bench")) SceneManager.LoadScene("EngineTest");
            int vehicle = GUILayout.SelectionGrid(Math.Max(0, selected), vehicles.Select(v => v.spec.displayName).ToArray(), 2);
            if (vehicle != selected) SelectVehicle(vehicle);
            GUILayout.Label("Test setup changes reset this isolated run. Captures retain component IDs and condition.");
            int preset = config.driving.shifting == ShiftMode.Automatic ? 0 : config.driving.shifting == ShiftMode.Sequential ? 1 : 2;
            int next = GUILayout.SelectionGrid(preset, new[] { "Automatic shifts", "Shift myself", "Manual clutch" }, 3);
            if (next != preset) { config.driving.ApplyPreset((DrivingPreset)next); config.driving.throttleAssist = false; ResetBench(true); }
            bool record = GUILayout.Toggle(Recording, "Record input/trace (optional; OFF on entry)");
            if (record != Recording) SetRecording(record);
            GUILayout.Label("Downshift protection: " + (Session.car.transmission.ProtectsDownshift ? "installed gearbox/controller" : "none on this manual gearbox"));
            showDiagnostics = GUILayout.Toggle(showDiagnostics, "Show detailed engine / clutch / slip diagnostics");
            GUILayout.Label("Repeatable input scenarios (enable recording)");
            if (Button("scenario", "Full pull / shifts / lift / neutral coast / brake")) StartScenario(PhysicalScenario.FullPullShiftCoastBrake);
            if (Button("money", "Manual money-shift / engine failure test")) StartMoneyShiftTest();
            if (GUILayout.Button("Neutral flywheel rev + decay")) StartScenario(PhysicalScenario.NeutralFlywheel);
            if (GUILayout.Button("Seeded driver / seed " + config.seed)) StartScenario(PhysicalScenario.SeededDriver);
            if (Button("seed", "Next seed (reset)")) { config.seed++; ResetBench(true); }
            GUILayout.BeginHorizontal();
            if (Button("replay", "Replay last run")) ReplayLast();
            if (Button("export", "Export CSV + replay JSON")) Export();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Import latest exported replay")) { if (Directory.Exists(exportFolder)) { string path = Directory.GetFiles(exportFolder, "*.json").Where(p => !p.EndsWith("unit-settings.json") && !p.EndsWith("audio-settings.json") && !p.EndsWith("profile-v1.json")).OrderByDescending(p => p).FirstOrDefault(); if (path != null) Import(path); } }
            configScroll = GUILayout.BeginScrollView(configScroll);
            if (audioSettings) { AudioSettingsView.Draw(Audio); UnitSettingsView.Draw(); }
            if (AwdSettingsView.Draw(config.vehicle, config.tune)) ResetBench(true);
            if (EquipmentTuningView.Draw(config.vehicle, config.tune, config.tune)) ReResolve();
            if (EquipmentTuningView.DrawHardware(config.vehicle, config.tune)) ReResolve();
            GUILayout.Label("SPLIT-GRIP TEST / axle differential response (1 = normal surface)");
            bool surface = GripSlider("Front left", ref config.tune.frontLeftGrip) | GripSlider("Front right", ref config.tune.frontRightGrip) | GripSlider("Rear left", ref config.tune.rearLeftGrip) | GripSlider("Rear right", ref config.tune.rearRightGrip);
            if (surface) ReResolve();
            WearSetup();
            if (SimulationSettingsView.Draw(config.driving, true)) ResetBench(true);
            GUILayout.Label("ISOLATED TEST PARTS / player inventory never changes");
            category = (PartCategory)GUILayout.SelectionGrid((int)category, Enum.GetNames(typeof(PartCategory)), 3);
            foreach (var definition in catalog.parts.Where(p => p.category == category && catalog.Fits(config.vehicle, p)))
            {
                GUILayout.Label(SharedUnitSettings.TorqueText(definition.description), partProse);
                GUILayout.Label(PartTuning.Summary(definition), partProse);
                foreach (var value in PartSemantics.Stats(definition)) GUILayout.Label(value.label + ": " + (value.unit == "Nm" ? SharedUnitSettings.Torque(value.value, "0.##") : value.unit == "bar" ? SharedUnitSettings.Pressure(value.value) : value.value.ToString("0.##") + " " + value.unit));
                if (GUILayout.Button("Test " + definition.name))
                {
                    ApplyTestPart(definition.id, true);
                }
            }
            if (GUILayout.Button("Remove selected test component")) RemoveTestPart(category);
            GUILayout.Label("Shift light: " + (config.tune.partShiftLight ? (Session.car.state.rpm >= config.tune.ShiftLightRpm(config.vehicle) ? "SHIFT NOW" : "armed at " + config.tune.ShiftLightRpm(config.vehicle).ToString("F0") + " RPM") : "not installed"));
            GUILayout.EndScrollView(); GUILayout.Label(notice); GUILayout.EndArea();
            var s = Session.car.state;
            GUILayout.BeginArea(new Rect(508, 12, 760, 180), GUI.skin.box);
            GUILayout.Label(config.vehicle.displayName + " / " + TransmissionDynamics.DisplayName(Session.car.transmission) + " / tick " + Session.TotalTick + " / " + (Session.TotalTick / 120.0).ToString("F2") + " s / " + Source);
            GUILayout.Label("RPM " + s.rpm.ToString("F0") + " / gear " + (s.gear == 0 ? "N" : s.gear.ToString()) + " / " + SharedUnitSettings.Speed(s.speed) + " / " + s.distance.ToString("F1") + " m");
            GUILayout.Label("Engine " + SharedUnitSettings.Torque(s.engineTorqueNm, "F1") + " / " + s.engineHorsepower.ToString("F1") + " hp / shaft " + SharedUnitSettings.Torque(s.transmittedTorqueNm, "F1") + " / boost " + SharedUnitSettings.Boost(s.boostBar));
            GUILayout.Label("Measured slip " + s.slip.ToString("P0") + " / clutch slip " + s.clutchSlipRpm.ToString("F0") + " RPM / flywheel " + s.flywheelEnergyJ.ToString("F0") + " J");
            GUILayout.Label("Coupling open " + s.clutch.ToString("P0") + " / applied brake " + s.brake.ToString("P0") + " / CVT ratio " + s.cvtRatio.ToString("F2") + " / converter slip " + s.converterSlipRpm.ToString("F0") + " RPM");
            GUILayout.Label("No distance finish or race cycle. Shared component stress, heat and fuel; no race-cycle restriction.");
            GUILayout.Label(s.feedback + (s.stalled ? " / STALLED: N then Enter to restart" : "") + (s.engineFailed ? " / ISOLATED TEST ENGINE FAILURE" : "")); GUILayout.EndArea();
            string[] groups=DrivingTraceChannels.Groups;
            for(int i=0;i<groups.Length;i++){var rect=new Rect(508+(i%8)*760f/8,198+(i/8)*21,760f/8,21);buttonRects["graph-"+groups[i]]=new Rect(GUIUtility.GUIToScreenPoint(rect.position),rect.size*GUI.matrix.m00);}
            int currentGroup=Array.IndexOf(groups,DrivingTraceChannels.All[graph].group);
            int selectedGroup=GUI.SelectionGrid(new Rect(508,198,760,63),currentGroup,groups,8);
            int[] channels=Enumerable.Range(0,DrivingTraceChannels.All.Length).Where(i=>DrivingTraceChannels.All[i].group==groups[selectedGroup]).ToArray();
            int selectedChannel=Math.Max(0,Array.IndexOf(channels,graph));
            selectedChannel=GUI.SelectionGrid(new Rect(508,264,760,40),selectedChannel,channels.Select(i=>DrivingTraceView.Label(i)).ToArray(),6);
            graph=channels[selectedChannel];

            if (showDiagnostics) Diagnostics(new Rect(508, 307, 760, 161));
            else traceView.Draw(new Rect(508, 307, 760, 161), Session, history, graph);
            GUILayout.BeginArea(new Rect(508, 482, 760, 226), GUI.skin.box); GUILayout.BeginHorizontal();
            if (Button("reset", "Reset / R")) ResetBench();
            if (Button("neutral", "Neutral / N")) { ManualCommand(); neutral = true; staging = false; }
            if (Button("down", "Shift down / Q")) { ManualCommand(); downPending = true; }
            if (Button("up", "Engage / shift up / E")) { ManualCommand(); upPending = true; }
            if (Button("pause", Paused ? "Resume / Esc" : "Pause / Esc")) SetPaused(!Paused);
            if (Button("end", "End run")) EndRun(); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); mouseGas = GUILayout.RepeatButton("Hold gas / W or SPACE"); mouseClutch = GUILayout.RepeatButton("Hold clutch / LEFT SHIFT"); mouseBrake = GUILayout.RepeatButton("Hold brake / S"); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Button("sliders", "Use sliders (explicit control takeover)")) UseSliders();
            if (Button("wear-reset", "Repair wear / heat (isolated)")) ResetWear();
            GUILayout.EndHorizontal();
            var feedback = KeyboardOwned || timeline != null ? AppliedInput : sliders;
            GUI.enabled = !KeyboardOwned && timeline == null;
            feedback.throttle = PedalSlider("Throttle", feedback.throttle); feedback.clutch = PedalSlider("Clutch pedal", feedback.clutch); feedback.brake = PedalSlider("Brake", feedback.brake);
            if (GUI.enabled) sliders = feedback; GUI.enabled = true; GUILayout.EndArea();
        }
        double PedalSlider(string label, double value)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label + " " + value.ToString("P0"), GUILayout.Width(145)); float result = GUILayout.HorizontalSlider((float)value, 0, 1);
            if (Event.current.type == EventType.Repaint) { var rect = GUILayoutUtility.GetLastRect(); pedalRects[label] = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size * GUI.matrix.m00); }
            GUILayout.EndHorizontal(); return result;
        }
        bool GripSlider(string label, ref float value) { GUILayout.Label(label + " grip " + value.ToString("P0")); var kind = Event.current.rawType; float next = GUILayout.HorizontalSlider(value, 0, 1.5f); bool changed = (kind == EventType.MouseDown || kind == EventType.MouseDrag || kind == EventType.MouseUp || kind == EventType.KeyDown) && Math.Abs(next - value) > .0001; if (changed) value = next; return changed; }
        void ReResolve() { var map = config.components.ToDictionary(p => catalog.Find(p.definitionId).category, p => p.definitionId); var resolved = catalog.Resolve(config.vehicle.id, map, config.tune); config.vehicle = resolved.spec; config.tune = resolved.tune; ResetBench(true); }
        void RemoveTestPart(PartCategory slot) {
            if (slot == PartCategory.Differential && config.vehicle.drive == DriveType.AWD) { config.tune.awdConfigured = false; config.tune.awd = null; }
 var old = config.components.Find(p => catalog.Find(p.definitionId).category == slot); if (old == null) return; config.components.Remove(old); config.installed.slots.RemoveAll(s => s.partInstanceId == old.instanceId); ReResolve(); }
        void ApplyTestPart(string id, bool reset)
        {
            var definition = catalog.Find(id); var slot = definition.category;
            var map = new Dictionary<PartCategory, string>();
            foreach (var part in config.components) map[catalog.Find(part.definitionId).category] = part.definitionId;
            map[slot] = id; var resolved = catalog.Resolve(config.vehicle.id, map, config.tune);
            var old = config.components.Find(p => catalog.Find(p.definitionId).category == slot);
            if (old != null) { config.components.Remove(old); config.installed.slots.RemoveAll(s => s.partInstanceId == old.instanceId); }
            var item = new PartInstance { instanceId = MechanicsOwnership.NewId("bench-part"), definitionId = id, location = old == null ? PartLocation.Vehicle : old.location, ownerId = old == null ? config.installed.instanceId : old.ownerId, slot = slot.ToString() };
            config.components.Add(item); config.installed.slots.Add(new SlotBinding { slot = item.slot, partInstanceId = item.instanceId });
            config.vehicle = resolved.spec; config.tune = resolved.tune; if (reset) ResetBench(true);
        }
        void Diagnostics(Rect rect)
        {
            var car = Session.car; var s = car.state; double coupled = car.GearboxInputRpm;
            var panelColor = GUI.color; GUI.color = new Color(.035f, .05f, .065f, 1); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = panelColor;
            GUILayout.BeginArea(rect, GUI.skin.box);
            diagnosticsScroll = GUILayout.BeginScrollView(diagnosticsScroll);
            GUILayout.Label("LIVE PHYSICAL DIAGNOSTICS / isolated damage / independent wheel rotation/contact");
            var failed = Session.car.mechanics.parts.Where(PrototypeGame.PartFailed).ToArray();
            var oldColor = GUI.contentColor;
            if (failed.Length > 0) GUI.contentColor = new Color(1, .4f, .35f);
            GUILayout.Label("FAILED COMPONENTS / " + failed.Length);
            foreach (var part in failed) GUILayout.Label(part.slot + " / " + part.condition.health.ToString("P0") + " / " + part.condition.failureReason, partProse);
            GUI.contentColor = oldColor;
            GUILayout.Label("Crank " + s.rpm.ToString("F0") + " RPM / gearbox input " + coupled.ToString("F0") + " RPM / signed mismatch " + (s.gear == 0 ? 0 : s.rpm - coupled).ToString("F0") + " RPM");
            GUILayout.Label("Clutch capacity " + SharedUnitSettings.Torque(car.spec.ClutchCapacity * car.tune.CapacityMultiplier) + " / coupling open " + s.clutch.ToString("P0") + " / shaft " + SharedUnitSettings.Torque(s.transmittedTorqueNm, "F0"));
            GUILayout.Label("Converter slip " + s.converterSlipRpm.ToString("F0") + " RPM / lock " + s.converterLocked + " / CVT ratio " + s.cvtRatio.ToString("F2") + " / measured wheel slip " + s.slip.ToString("P0"));
            GUILayout.Label("Drive " + car.spec.drive + (car.spec.drive == DriveType.AWD ? " / " + car.awd.mode : "") + " / front:rear torque share " + s.frontTorqueShare.ToString("P0") + ":" + s.rearTorqueShare.ToString("P0"));
            GUILayout.Label("Front / rear force " + s.frontDriveForceN.ToString("F0") + " / " + s.rearDriveForceN.ToString("F0") + " N / measured slip " + s.frontSlip.ToString("P1") + " / " + s.rearSlip.ToString("P1"));
            GUILayout.Label("Differentials front " + car.spec.frontDifferential.kind + " / rear " + car.spec.rearDifferential.kind + " / left:right measured slipS front " + s.frontLeftSlip.ToString("P1") + ":" + s.frontRightSlip.ToString("P1") + " rear " + s.rearLeftSlip.ToString("P1") + ":" + s.rearRightSlip.ToString("P1"));
            MassControls(); TireDiagnostics();
            GUILayout.Label("Wheel RPM FL/FR/RL/RR " + string.Join(" / ", Array.ConvertAll(s.contact.omega, w => (w * FlywheelDynamics.RpmPerRadian).ToString("F0"))));
            GUILayout.Label("Brake work " + s.contact.brakeWorkJ.ToString("F0") + " J / tire contact loss " + s.contact.contactWorkJ.ToString("F0") + " J / clutch heat " + (s.clutchFrictionPowerW / 1000).ToString("F2") + " kW");
            GUILayout.Label("Radiator rejecting " + (s.coolingHeatRejectedW / 1000).ToString("F2") + " kW / engine heat transfer " + s.engineCoolingFactor.ToString("F2") + "x");
            GUILayout.Label("Front path " + s.frontDriveAvailable + " / rear path " + s.rearDriveAvailable + " / brake capacity " + s.brakeCapacityFactor.ToString("P0") + (s.brakesFailed ? " / BRAKES FAILED" : ""));
            GUILayout.Label("Front / rear shaft RPM " + s.frontShaftRpm.ToString("F0") + " / " + s.rearShaftRpm.ToString("F0") + " / shaft power " + (s.frontShaftPowerW / 1000).ToString("F2") + " / " + (s.rearShaftPowerW / 1000).ToString("F2") + " kW" + (s.openCenterFreeBranch ? " / FREE BRANCH SPINNING" : ""));
            GUILayout.Label("Limiter " + car.engine.limiter + " / active " + s.limiter + " / limit " + s.activeLimitRpm.ToString("F0") + " RPM / safe RPM " + (car.spec.redlineRpm * car.engine.data.safeRpmMultiplier).ToString("F0") + " / combustion " + s.combustionFraction.ToString("P0"));
            GUILayout.Label("Damage " + s.engineDamage.ToString("P1") + " / warmup " + s.engineTemperature.ToString("P0") + " / exhaust heat " + s.exhaustHeat.ToString("P0") + " / turbo spool " + s.turboSpool.ToString("P0"));
            GUILayout.Label(s.engineFailed ? "ENGINE FAILED: neutral/coast/brake; isolated repair or reset." : s.drivetrainFailed ? "DRIVELINE FAILED: engine can rev; wheel torque disconnected." : s.stalled ? "STALLED: neutral + Enter to restart." : "Engine running / " + s.feedback);
            var monitored = Session.car.mechanics.parts.ToArray();
            for (int i = 0; i < monitored.Length; i += 2) GUILayout.Label(string.Join(" / ", monitored.Skip(i).Take(2).Select(p => p.slot + " " + p.condition.health.ToString("P1") + " " + p.condition.temperatureC.ToString("F1") + " °C")));
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        void TireDiagnostics()
        {
            var part=Session.car.mechanics.parts.Find(p=>p.slot=="Tires");if(part==null)return;var d=Session.car.mechanics.Definition(part);
            GUILayout.Label("TIRES / each wheel: life, temperature, accumulated sliding work, condition grip");
            for(int i=0;i<4;i++){var t=part.condition.tires[i];GUILayout.Label(new[]{"FL","FR","RL","RR"}[i]+" / "+t.health.ToString("P2")+" / "+t.temperatureC.ToString("F1")+" °C / "+(t.slipEnergyJ/1000).ToString("F1")+" kJ / grip "+TireDynamics.Grip(d,t).ToString("F2")+"x"+(t.failed?" / FAILED":""));}
        }
        void WearSetup()
        {
            GUILayout.Label("ISOLATED STARTING CONDITION / all installed parts");
            GUILayout.Label("Repair wear / heat below the pedals resets all copied components.");
            foreach (var failed in Session.car.mechanics.parts.Where(PrototypeGame.PartFailed).ToArray())
            {
                var oldColor = GUI.contentColor; GUI.contentColor = new Color(1, .4f, .35f);
                GUILayout.Label("FAILED / " + failed.slot + " / " + failed.condition.failureReason, partProse); GUI.contentColor = oldColor;
                int index = config.components.FindIndex(p => p.instanceId == failed.instanceId);
                if (index >= 0 && Button("inspect-failure:" + failed.instanceId, "Inspect failed " + failed.slot + " / condition controls")) wearPart = index;
            }
            if (config.components.Count == 0) return;
            wearPart = Mathf.Clamp(wearPart, 0, config.components.Count - 1);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous part")) wearPart = (wearPart + config.components.Count - 1) % config.components.Count;
            if (GUILayout.Button("Next part")) wearPart = (wearPart + 1) % config.components.Count;
            GUILayout.EndHorizontal();
            var part = config.components[wearPart]; var d = catalog.Find(part.definitionId); var live = Session.car.mechanics.parts.Find(p => p.instanceId == part.instanceId);
            GUILayout.Label(d.name);
            if (live != null) GUILayout.Label("Live: " + live.condition.health.ToString("P2") + " / " + live.condition.temperatureC.ToString("F1") + " °C / " + live.condition.failureReason);
            if(PartSemantics.TorqueRated(d.category))GUILayout.Label("Torque strength "+SharedUnitSettings.Torque(d.torqueLimitNm));
            if(PartSemantics.EngineInternal(d.category))GUILayout.Label("Mechanical RPM strength "+d.rpmLimit.ToString("F0")+" RPM");
            if(PartSemantics.Wears(d.category))GUILayout.Label("Heat limit "+d.thermalLimitC.ToString("F0")+" °C");
            if(d.category==PartCategory.Tires)GUILayout.Label("Rolling life "+(d.tireLifeDistanceM/1000).ToString("F0")+" km / slip-work life "+(d.wearEnergyJ/1e6).ToString("F1")+" MJ per tire / optimum "+d.tireOptimalC+" °C");
            GUI.enabled = Paused || !Session.car.state.launched;
            GUILayout.Label("Starting health " + part.condition.health.ToString("P0"));
            float health = GUILayout.HorizontalSlider(part.condition.health, 0, 1);
            GUILayout.Label("Starting temperature " + part.condition.temperatureC.ToString("F0") + " °C");
            double temperature = GUILayout.HorizontalSlider((float)part.condition.temperatureC, 20, 500);
            if (GUI.enabled && (Math.Abs(health - part.condition.health) > .00001 || Math.Abs(temperature - part.condition.temperatureC) > .01)) SetStartingCondition(wearPart, health, temperature);
            if(d.category==PartCategory.Tires){TireDynamics.Ensure(part.condition);for(int i=0;i<4;i++){
                var tire=part.condition.tires[i];GUILayout.Label(new[]{"FL","FR","RL","RR"}[i]+" starting life "+tire.health.ToString("P1")+" / "+tire.temperatureC.ToString("F1")+" °C");
                float life=GUILayout.HorizontalSlider(tire.health,0,1);if(Event.current.type==EventType.Repaint)tireRects["life:"+i]=RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());
                GUILayout.Label("Temperature °C");float heat=GUILayout.HorizontalSlider((float)tire.temperatureC,20,200);if(Event.current.type==EventType.Repaint)tireRects["heat:"+i]=RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());
                if(GUI.enabled&&(Math.Abs(life-tire.health)>.00001||Math.Abs(heat-tire.temperatureC)>.01))SetStartingTire(i,life,heat);
            }}
            GUI.enabled = true;
            ServiceFaultControls(part,d,live);
            GUILayout.Label("Assign before drive engagement or pause first. Assigning resets this test; pedal source is kept.");
        }
        void OnApplicationFocus(bool focused) { if (!focused && !DrivingBenchRuntimeVerification.Active && Session != null) SetPaused(true); }
        void OnDestroy() { traceView.Dispose(); }
    }
}

