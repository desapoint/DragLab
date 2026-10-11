using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Presentation;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    // Development-only controls: isolated configuration copies, no profile/economy access.
    public sealed class EngineTestScene : MonoBehaviour
    {
        public VehicleDefinition[] vehicles;
        public EngineBenchConfig config = new EngineBenchConfig();
        public EngineBenchScenario scenario = EngineBenchScenario.NeutralBlips;
        public EngineTestBench Bench { get; private set; }
        public EngineAudio Audio { get; private set; }
        public bool Paused { get; private set; }
        public bool RunningScenario { get; private set; }
        public int ScenarioTick { get; private set; }
        public bool PlayingMusic { get; private set; }
        public int MusicTick { get; private set; }
        public string MusicTitle => music == null ? "" : music.Title;
        EngineBenchMusic music;
        EngineBenchConfig musicRestore;
        int lastSong = -1;
        public EngineBenchConfig SlotA { get; private set; }
        public EngineBenchConfig SlotB { get; private set; }
        public EngineBenchInput Controls { get; } = new EngineBenchInput();
        public EngineBenchDyno Dyno { get; } = new EngineBenchDyno();
        readonly EngineBenchDynoView dynoView = new EngineBenchDynoView();
        EngineBenchConfig capturedRun;
        EngineBenchScenario capturedScenario;
        string capturedLabel;
        LayeredCarView view;
        internal LayeredCarView CarView=>view;
        double accumulator;
        int selected, tab;
        Vector2 scroll;
        GUIStyle title;
        string notice = "Copies only: the bench never edits vehicle assets or your player save.";
        string filePath;
        bool pendingConfig, mouseGas, mouseClutch, mouseBrake;
        float lastEdit;
        bool automated, editingText, showDyno = true, clearGuiFocus, guiGasW, guiGasSpace;
        readonly InterfaceComparison interfaceComparison = new InterfaceComparison();
        internal const string ComparisonRoute = "Sound bench";
        internal bool verificationKeyboard;
        internal string verificationFocusField;
        internal bool IsEditingText => editingText;
        internal Rect VerificationMixSlider(string name) => fields.verificationSliders["sound." + name];
        internal Rect VerificationFieldControl(string name) => fields.verificationControls[name];
        internal Rect VerificationDriveSlider(string name) => fields.verificationSliders["controls." + name];
        internal void ScrollTestToBottom() { scroll = new Vector2(0, 10000); }
        internal void ScrollTestToTop() { scroll = Vector2.zero; }
        internal Rect verificationVehicleRect;
        internal Rect verificationShiftUpRect, verificationShiftDownRect;
        internal void ShowTestTab(int value) { tab = value; scroll = Vector2.zero; }
        readonly BenchFieldControls fields = new BenchFieldControls();

        void Start()
        {
            automated = Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-bench-verify") >= 0;
            var authoredVehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            if (authoredVehicles.Length > 0) vehicles = authoredVehicles;
            Array.Sort(vehicles, (a, b) => string.CompareOrdinal(a.spec.id, b.spec.id));
            var cameraObject = new GameObject("Engine Bench Camera"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 4.5f;
            camera.backgroundColor = new Color(.075f, .105f, .14f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0, 0, -10); cameraObject.AddComponent<AudioListener>();
            view = new GameObject("Bench car / independent wheel parts").AddComponent<LayeredCarView>();
            view.transform.position = new Vector3(3, .1f, 0); view.transform.localScale = Vector3.one * 1.1f;
            Audio = gameObject.AddComponent<EngineAudio>(); Audio.Initialize();
            filePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../EngineBenchConfigs/config.json"));
            SelectVehicle(0);
            EngineBenchRuntimeVerification.AttachIfRequested(this);
            InterfaceComparisonVerification.AttachIfRequested();
        }
        public void SelectVehicle(int index)
        {
            bool keepKeyboard = Controls.Source == EngineBenchControlSource.Keyboard;
            RestoreMusicConfig();
            selected = Mathf.Clamp(index, 0, vehicles.Length - 1);
            config = new EngineBenchConfig { vehicle = vehicles[selected].spec.Copy(), tune = vehicles[selected].CreateStockTune() };
            view.Build(config.vehicle.artId, vehicles[selected].defaultPaint);
            ResetBench();
            if (keepKeyboard) KeyboardControl();
        }
        public void ApplyConfig(bool reset = false)
        {
            if(reset)view.ClearSlipEffects();
            string invalid = config.Validate(); if (invalid != null) { notice = invalid + " Previous valid sound remains active."; return; }
            if (!reset) { Dyno.Finish("configuration changed"); RunningScenario = false; }
            Bench = new EngineTestBench(config.vehicle, config.tune, reset || Bench == null ? null : Bench.state);
            Audio.Configure(Bench.spec, Bench.tune); view.ApplySetup(Bench.spec, Bench.tune); pendingConfig = false;
            if (reset) { config.controls.gear = 0; accumulator = 0; }
            if (reset || Controls.Source != EngineBenchControlSource.Keyboard) Controls.Sliders(config.controls);
            if (!reset) BeginDyno();
        }
        public void ResetBench(bool clearPedals = true)
        {
            RestoreMusicConfig();
            Dyno.Finish("reset / car changed");
            var restoredControls = config.controls.Copy();
            RunningScenario = false; ScenarioTick = 0; Paused = false;
            if (clearPedals) config.controls = new EngineBenchControls();
            else config.controls.throttle = config.controls.clutch = config.controls.brake = 0;
            mouseGas = mouseClutch = mouseBrake = false;
            guiGasW = guiGasSpace = false;
            ApplyConfig(true);
            if (!clearPedals) config.controls = restoredControls;
            Controls.Sliders(config.controls); BeginDyno();
        }
        public void Pause(bool pause) { Paused = pause; accumulator = 0; if (pause) Audio.Silence(); }
        public void CaptureSlot(bool a)
        {
            if (config.Validate() != null) return;
            var snapshot = config.Copy(); snapshot.controls = Controls.Effective.Copy(); snapshot.volume = SharedAudioSettings.MasterVolume;
            if (a) SlotA = snapshot; else SlotB = snapshot;
            notice = "Captured " + (a ? "A" : "B") + " configuration, including applied controls and volume.";
        }
        public void RunScenario(EngineBenchConfig source, EngineBenchScenario chosen, string label = null)
        {
            if (PlayingMusic) { if (ReferenceEquals(source, config)) source = musicRestore.Copy(); StopMusic(); }
            if (source == null || source.Validate() != null) { notice = "Capture a valid configuration first."; return; }
            capturedRun = source.Copy(); capturedScenario = chosen;
            capturedLabel = label ?? chosen + (ReferenceEquals(source, SlotA) ? " / A" : ReferenceEquals(source, SlotB) ? " / B" : "");
            config = EngineBenchRenderer.ScenarioConfig(source, chosen); scenario = chosen;
            ResetBench(false); RunningScenario = true;
            Controls.Scenario(EngineBenchScenarios.At(chosen, 0, Bench.spec, capturedRun.controls));
            BeginDyno(capturedLabel);
            notice = "Scenario: fresh engine, temperature, compressor state and audio generator.";
        }
        public void RepeatScenario() { RunScenario(capturedRun, capturedScenario, capturedLabel); }
        public void Manual()
        {
            if (PlayingMusic) StopMusic();
            bool wasScenario = Controls.Source == EngineBenchControlSource.Scenario;
            if (wasScenario)
            {
                Dyno.Finish(RunningScenario ? "scenario interrupted" : "scenario finished");
                config.controls = Controls.Effective.Copy(); Controls.Sliders(config.controls);
            }
            RunningScenario = false; Pause(false);
            if (wasScenario) BeginDyno();
        }
        public void SetSliderControls(EngineBenchControls assigned)
        {
            Manual(); config.controls = assigned.Copy(); Controls.Sliders(config.controls);
        }
        public void UseSliders() { SetSliderControls(Controls.Effective); clearGuiFocus = true; }
        public void KeyboardControl()
        {
            // Finish an outstanding hardware edit before giving keys ownership;
            // its debounce must not take ownership back halfway through held W.
            if (pendingConfig) { ApplyConfig(); pendingConfig = false; }
            Manual();
            if (Controls.Source != EngineBenchControlSource.Keyboard) config.controls = Controls.Keyboard();
            editingText = false; clearGuiFocus = true;
        }
        public void BeginDyno(string label = "Manual")
        {
            if (Bench == null || config.Validate() != null) return;
            var snapshot = config.Copy(); snapshot.controls = Controls.Effective.Copy(); snapshot.volume = SharedAudioSettings.MasterVolume;
            snapshot.vehicle = Bench.spec.Copy(); snapshot.tune = Bench.tune.Copy();
            Dyno.Begin(Bench.spec.displayName + " / " + label, snapshot, Bench.state, Bench.audioLoad);
        }
        public void EndDyno() { Dyno.Finish("recording stopped"); }
        public void PlayRandomMusic()
        {
            // Random choice without immediately repeating the last theme.
            int chosen = lastSong < 0 ? UnityEngine.Random.Range(0, 3) : (lastSong + 1 + UnityEngine.Random.Range(0, 2)) % 3;
            PlayMusic(chosen);
        }
        public void PlayMusic(int song)
        {
            if (PlayingMusic) StopMusic();
            if (pendingConfig) ApplyConfig();
            if (config.Validate() != null) { notice = "Restore valid settings before booking the concert."; return; }
            EngineBenchMusic performance;
            try { performance = new EngineBenchMusic(song, config.vehicle); }
            catch (ArgumentException e) { notice = e.Message; return; }
            musicRestore = config.Copy(); musicRestore.controls = Controls.Effective.Copy();
            config = EngineBenchMusic.PerformanceConfig(config);
            // Musical fixed-RPM fixture: no protective cuts or accessory noise to
            // obscure notes. The original engine/exhaust character remains intact.
            ResetBench(false); // Not playing yet, so the saved configuration remains intact.
            music = performance; PlayingMusic = true; MusicTick = 0; lastSong = song;
            Controls.Scenario(music.At(0, Bench.spec)); BeginDyno("Dyno concert / " + music.Title);
            notice = "Now performing " + music.Title + ". W/Space or Stop restores your settings; Esc pauses.";
            clearGuiFocus = true;
        }
        void RestoreMusicConfig()
        {
            if (!PlayingMusic) return;
            PlayingMusic = false; config = musicRestore; musicRestore = null; fields.Clear();
        }
        public void StopMusic()
        {
            if (!PlayingMusic) return;
            Dyno.Finish("concert ended"); RestoreMusicConfig(); ResetBench(false);
            notice = "Concert finished. Your pre-concert bench settings are restored.";
        }
        void Update()
        {
            if (Bench == null) return;
            if (pendingConfig && Time.realtimeSinceStartup - lastEdit > .18f) ApplyConfig();
            // W/Space are not numeric-entry keys. They explicitly claim driving
            // control even when a numeric field kept focus after a slider edit.
            if ((!automated || verificationKeyboard) && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space))) KeyboardControl();
            if ((!automated || verificationKeyboard) && !editingText)
            {
                bool pedalEdge = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.LeftShift);
                bool driveEdge = pedalEdge || Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Q) ||
                    Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.UpArrow) ||
                    Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.RightBracket);
                if (driveEdge) KeyboardControl();
                if (Input.GetKeyDown(KeyCode.R)) ResetBench();
                if (Input.GetKeyDown(KeyCode.Escape)) Pause(!Paused);
                if (Input.GetKeyDown(KeyCode.F5)) RunScenario(config, scenario);
                if (Input.GetKeyDown(KeyCode.F6)) RepeatScenario();
                if (Input.GetKeyDown(KeyCode.F7)) RunScenario(SlotA, scenario);
                if (Input.GetKeyDown(KeyCode.F8) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) RunScenario(SlotB, scenario);
                if (Input.GetKeyDown(KeyCode.N)) config.controls.gear = 0;
                if (Input.GetKeyDown(KeyCode.E)) config.controls.gear = Math.Min(Bench.spec.gears.Length, Bench.state.gear + 1);
                if (Input.GetKeyDown(KeyCode.Q)) config.controls.gear = Math.Max(0, Bench.state.gear - 1);
                if (Input.GetKeyDown(KeyCode.F)) config.controls.fixedRpm = !config.controls.fixedRpm;
                if (Input.GetKeyDown(KeyCode.B)) { config.controls.tireSlip = config.controls.tireSlip > 0 ? 0 : 1; config.controls.roadSpeedKmh = 40; }
                if (Input.GetKey(KeyCode.UpArrow)) config.controls.load = Mathf.Clamp01(config.controls.load + Time.unscaledDeltaTime * .4f);
                if (Input.GetKey(KeyCode.DownArrow)) config.controls.load = Mathf.Clamp01(config.controls.load - Time.unscaledDeltaTime * .4f);
                if (Input.GetKeyDown(KeyCode.LeftBracket)) config.controls.targetRpm = Mathf.Max(400, config.controls.targetRpm - 100);
                if (Input.GetKeyDown(KeyCode.RightBracket)) config.controls.targetRpm = Mathf.Min(Bench.spec.redlineRpm * 1.5f, config.controls.targetRpm + 100);
            }
            if (!Paused)
            {
                accumulator += Math.Min(.1, Time.unscaledDeltaTime);
                while (accumulator >= DragMath.FixedStep)
                {
                    EngineBenchControls control;
                    if (PlayingMusic)
                    {
                        control = music.At(MusicTick++, Bench.spec); Controls.Scenario(control);
                    }
                    else if (RunningScenario)
                    {
                        control = EngineBenchScenarios.At(capturedScenario, ScenarioTick++, Bench.spec, capturedRun.controls);
                        Controls.Scenario(control);
                        if (ScenarioTick >= EngineBenchScenarios.DurationTicks(capturedScenario)) { RunningScenario = false; Paused = true; accumulator = 0; }
                    }
                    else
                    {
                        bool keys = (!automated || verificationKeyboard) && (!editingText || Controls.Source == EngineBenchControlSource.Keyboard);
                        control = Controls.Resolve(config.controls,
                            (keys && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Space))) || guiGasW || guiGasSpace || mouseGas,
                            (keys && Input.GetKey(KeyCode.LeftShift)) || mouseClutch, (keys && Input.GetKey(KeyCode.S)) || mouseBrake);
                    }
                    Bench.Step(control); Controls.Publish(control, Bench.state); Dyno.Sample(Bench.state, control.load);
                    if (PlayingMusic && MusicTick >= music.DurationTicks) { StopMusic(); break; }
                    if (Paused) Dyno.Finish("scenario completed");
                    accumulator = Math.Max(0, accumulator - DragMath.FixedStep);
                    if (Paused) break;
                }
            }
            view.Animate(Bench.state,!Paused,true);
            Audio.Render(Bench.state, !Paused && Bench.state.rpm > 0, PlayingMusic ? music.GainAt(Math.Max(0, MusicTick - 1)) : 1, Bench.audioLoad);
        }
        void OnApplicationFocus(bool focus) { if (!focus) { interfaceComparison.ReleaseShortcut(); guiGasW = guiGasSpace = false; if (!automated && Audio != null) Pause(true); } }
        void OnDisable() { guiGasW = guiGasSpace = false; if (Audio != null) Audio.Silence(); }
        void OnDestroy() { dynoView.Dispose(); }
        void OnGUI()
        {
            if (Bench == null) return;
            bool replacement = interfaceComparison.BeginGUI(ComparisonRoute, editingText);
            // Keep the existing gas event bridge active even with all fields hidden.
            if (replacement) { mouseGas = mouseClutch = mouseBrake = false; GasGUIEvents(); }
            else DrawLegacyGUI();
            interfaceComparison.DrawStatus(ComparisonRoute);
        }
        void GasGUIEvents()
        {
            if ((!automated || verificationKeyboard) && (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp))
            {
                bool down = Event.current.type == EventType.KeyDown;
                bool gas = Event.current.keyCode == KeyCode.W || Event.current.keyCode == KeyCode.Space;
                if (Event.current.keyCode == KeyCode.W) guiGasW = down;
                if (Event.current.keyCode == KeyCode.Space) guiGasSpace = down;
                if (gas && down && (Controls.Source != EngineBenchControlSource.Keyboard || Paused)) KeyboardControl();
                if (gas) { GUI.FocusControl(null); editingText = false; Event.current.Use(); }
            }
        }
        void DrawLegacyGUI()
        {
            if (Bench == null) return;
            RuntimeGuiRect.BeginFrame();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            // Keep gas event ownership independent of presentation.
            GasGUIEvents();
            if (clearGuiFocus) { GUI.FocusControl(null); clearGuiFocus = false; }
            if (Controls.Source == EngineBenchControlSource.Keyboard && !fields.HasTextFocus(GUI.GetNameOfFocusedControl()) && GUI.GetNameOfFocusedControl() != "bench-file-path" && (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp)) Event.current.Use();
            if (title == null) title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            GUILayout.BeginArea(new Rect(12, 12, 508, 696), GUI.skin.box);
            GUILayout.Label("ENGINE / SOUND TEST BENCH", title);
            var names = Array.ConvertAll(vehicles, v => v.spec.displayName);
            int next = GUILayout.SelectionGrid(selected, names, 2);
            if (Event.current.type == EventType.Repaint) { var rect = GUILayoutUtility.GetLastRect(); verificationVehicleRect = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size * GUI.matrix.m00); }
            if (next != selected) SelectVehicle(next);
            tab = GUILayout.SelectionGrid(tab, new[] { "Drive", "Engine", "Induction", "Exhaust", "Gearbox / wheels", "Mix", "Scenarios", "Files", "Settings" }, 4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Use keyboard")) KeyboardControl();
            if (GUILayout.Button("Use sliders")) UseSliders();
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            bool keyboardOwned = Controls.Source == EngineBenchControlSource.Keyboard;
            bool locked = PlayingMusic;
            GUI.enabled = !locked;
            bool dirty = false;
            if (tab == 0)
            {
                GUILayout.Label("Control source: " + Controls.Source + " / sliders show applied inputs.");
                GUILayout.Label(keyboardOwned ? "Keyboard owns driving inputs. Hardware and sound settings stay editable." : "Assigned slider values latch; W/Space takes over for driving.");
                GUILayout.Label(RunningScenario ? "Scenario owns inputs. Manual controls interrupt it." : "Manual engine test / no race cycle.");
                GUILayout.Label("Actual engine RPM: " + Bench.state.rpm.ToString("F0") + " (targetRpm below is a test setting)");
                GUI.enabled = false; GUILayout.HorizontalSlider((float)Bench.state.rpm, 0, Mathf.Max(14000, (float)Bench.state.rpm)); GUI.enabled = !locked;
                var displayed = Controls.Effective.Copy();
                GUI.enabled = !locked && !keyboardOwned;
                dirty = fields.Draw(displayed, "controls", "engineEnabled", "launchControls", "allowFailure", "warmthOverride", "damageOverride");
                if (dirty && !keyboardOwned) SetSliderControls(displayed);
                GUI.enabled = !locked;
                GUILayout.Label("Engine operation / test overrides (independent of driving inputs)");
                bool options = fields.Draw(config.controls, "controls", "fixedRpm", "throttle", "clutch", "brake", "load", "roadSpeedKmh", "tireSlip", "targetRpm", "gear");
                if (options && !keyboardOwned) { Manual(); Controls.Sliders(config.controls); }
                GUILayout.Label("Master volume " + SharedAudioSettings.MasterVolume.ToString("P0") + " / change on Settings.");
            }
            if (tab == 1)
            {
                GUILayout.Label("Engine size / torque / inertia / RPM limits");
                dirty |= fields.Draw(config.vehicle, "vehicle", "sound", "induction", "engineBehavior", "transmission", "wheels", "flywheel", "gears", "id", "displayName", "artId", "dataProvenance");
                GUILayout.Label("Flywheel: annular disc mass / radii; engineInertiaKgM2 is crank/core only (0 = 0.12).");
                dirty |= fields.Draw(config.vehicle.flywheel, "flywheel");
                GUILayout.Label("Calculated flywheel inertia: " + FlywheelDynamics.Inertia(config.vehicle, config.tune).ToString("F4") + " kg m² / total: " + config.vehicle.RotationalInertia(config.tune).ToString("F4"));
                GUILayout.Label("Fueling / cam / warmup / limiter / launch control");
                dirty |= fields.Draw(config.vehicle.engineBehavior, "behavior");
                dirty |= fields.Draw(config.vehicle.combustion,"combustion","fuelKind","separateSparkPlugs");
                config.vehicle.combustion.fuelKind=config.vehicle.engineBehavior.fueling==FuelSystem.Diesel?DragLab.Domain.Mechanics.FuelKind.Diesel:DragLab.Domain.Mechanics.FuelKind.Petrol;
                GUILayout.Label("Upgrades / adjustable launch and limiter settings");
                dirty |= fields.Draw(config.tune, "tune");
            }
            if (tab == 2) dirty |= fields.Draw(config.vehicle.induction, "induction");
            if (tab == 3) dirty |= ExhaustGUI();
            if (tab == 4)
            {
                GUILayout.Label("Manual selector hardware changes shift time; driving assists only change who operates it. Sequential here uses the existing single clutch, with no automatic flatshift cut.");
                dirty |= fields.Draw(config.vehicle.transmission, "transmission");
                dirty |= fields.Draw(config.vehicle.wheels, "wheels");
                dirty |= fields.DrawArray(config.vehicle.gears, "gear ratios", .2f, 6);
            }
            if (tab == 5)
            {
                GUILayout.Label("All acoustic layers; physical displacement is on Engine tab.");
                GUILayout.Label("Engine gain: combustion; accessory gains: separate layers.\nMaster on Settings changes the complete mix.");
                if (locked) GUILayout.Label("Stop the concert to edit its temporary sound setup. Master on Settings stays editable.");
                dirty |= fields.Draw(config.vehicle.sound, "sound", "exhaustSystem", "induction", "blowOff", "boostBar", "spoolSeconds", "displacementLitres");
                GUILayout.Label("Master volume " + SharedAudioSettings.MasterVolume.ToString("P0") + " / change on Settings.");
            }
            if (tab == 6) ScenariosGUI();
            if (tab == 7) FilesGUI();
            if (tab == 8) { GUI.enabled = true; AudioSettingsView.Draw(Audio); UnitSettingsView.Draw(); }
            if (dirty && tab != 0)
            {
                if (Controls.Source != EngineBenchControlSource.Keyboard) { Manual(); Controls.Sliders(config.controls); }
                pendingConfig = true; lastEdit = Time.realtimeSinceStartup;
            }
            GUI.enabled = !locked;
            GUILayout.EndScrollView();
            string invalid = config.Validate(); if (invalid != null) GUILayout.Label("DRAFT INVALID: " + invalid);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply now")) ApplyConfig();
            if (GUILayout.Button("Restore valid")) { config.vehicle = Bench.spec.Copy(); config.tune = Bench.tune.Copy(); fields.Clear(); }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Label(notice);
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(540, 12, 728, 190), GUI.skin.box);
            var s = Bench.state;
            GUILayout.Label((PlayingMusic ? "DYNO CONCERT / " + MusicTitle : Controls.Source == EngineBenchControlSource.Scenario ? "SCENARIO " + capturedScenario + " / " + (ScenarioTick / 120f).ToString("F2") + " s" : "MANUAL / " + Controls.Source) + (Paused ? " / PAUSED" : " / LIVE"), PlayingMusic ? GUI.skin.label : title);
            GUILayout.Label("RPM " + s.rpm.ToString("F0") + " / gear " + (s.gear == 0 ? "N" : s.gear.ToString()) + " / gas " + s.throttle.ToString("P0") + " / dyno load " + Bench.audioLoad.ToString("P0"));
            GUILayout.Label(Bench.spec.sound.cylinders + " cyl / " + Bench.spec.DisplacementLitres.ToString("F1") + " L / " + Bench.engine.data.fueling + " / " + Bench.induction.type + " / " + TransmissionDynamics.DisplayName(Bench.transmission));
            GUILayout.Label("Torque " + SharedUnitSettings.Torque(s.engineTorqueNm, "F0") + " / " + s.engineHorsepower.ToString("F0") + " hp / boost " + SharedUnitSettings.Boost(s.boostBar) + " / spool " + s.turboSpool.ToString("P0"));
            GUILayout.Label("Warm " + s.engineTemperature.ToString("P0") + " / heat " + s.exhaustHeat.ToString("P0") + " / wear " + s.engineDamage.ToString("P0") + " / cuts " + s.limiter + " / two-step " + s.launchControlActive + " / ALS " + s.antiLagActive);
            GUILayout.Label("Rollers " + SharedUnitSettings.Speed(s.speed, "F0") + " / slip " + s.slip.ToString("P0") + " / valve events " + s.boostReleaseCount + " / exhaust pops " + s.exhaustPopCount + (s.engineFailed ? " / ENGINE FAILED" : ""));
            GUILayout.Label("Flywheel I " + s.flywheelInertiaKgM2.ToString("F3") + " kg m² / stored " + (s.flywheelEnergyJ / 1000).ToString("F2") + " kJ / momentum " + s.flywheelAngularMomentum.ToString("F1") + " kg m²/s");
            GUILayout.EndArea();
            if (showDyno) dynoView.Draw(new Rect(540, 210, 728, 282), Dyno, () => BeginDyno(), EndDyno, ExportDyno);
            GUILayout.BeginArea(new Rect(540, 500, 728, 208), GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset / R")) ResetBench();
            if (GUILayout.Button("Neutral / N")) { Manual(); config.controls.gear = 0; }
            if (GUILayout.Button("Shift down / Q")) { Manual(); config.controls.gear = Math.Max(0, Bench.state.gear - 1); }
            if (Event.current.type == EventType.Repaint) verificationShiftDownRect = ScaleProbeRect(GUILayoutUtility.GetLastRect(), scale);
            if (GUILayout.Button("Shift up / E")) { Manual(); config.controls.gear = Math.Min(Bench.spec.gears.Length, Bench.state.gear + 1); }
            if (Event.current.type == EventType.Repaint) verificationShiftUpRect = ScaleProbeRect(GUILayoutUtility.GetLastRect(), scale);
            if (GUILayout.Button(Paused ? "Resume / Esc" : "Pause / Esc")) Pause(!Paused);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            mouseGas = GUILayout.RepeatButton("Hold gas / W or SPACE", GUILayout.Height(45));
            mouseClutch = GUILayout.RepeatButton("Hold clutch / LEFT SHIFT", GUILayout.Height(45));
            mouseBrake = GUILayout.RepeatButton("Hold brake / S", GUILayout.Height(45));
            if ((mouseGas || mouseClutch || mouseBrake) && Controls.Source != EngineBenchControlSource.Keyboard) KeyboardControl();
            GUILayout.EndHorizontal();
            GUILayout.Label("F: fixed RPM   [ / ]: target ±100 RPM   Up/Down: load   B: tire spin\nF5: run current   F6: repeat captured   F7: run A / Shift+F8: run B / F8: compare UI\nManual tests run endlessly; fixed RPM is a dynamometer override.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(showDyno ? "Show car / wheels" : "Show live dyno")) showDyno = !showDyno;
            if (GUILayout.Button(PlayingMusic ? "Stop concert / restore" : "Dyno concert / random classic")) { if (PlayingMusic) StopMusic(); else PlayRandomMusic(); }
            if (GUILayout.Button("Return to garage")) SceneManager.LoadScene("FirstSlice");
            if (GUILayout.Button("Physical driving bench")) SceneManager.LoadScene("DrivingTest");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            if (verificationFocusField != null) { GUI.FocusControl(verificationFocusField); verificationFocusField = null; }
            editingText = fields.HasTextFocus(GUI.GetNameOfFocusedControl()) || GUI.GetNameOfFocusedControl() == "bench-file-path";
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return) { GUI.FocusControl(null); editingText = false; }
        }
        static float LabeledSlider(string label, float value, float low, float high)
        {
            var type = Event.current.rawType; bool edit = GUI.enabled && (type == EventType.MouseDown || type == EventType.MouseDrag || type == EventType.MouseUp || type == EventType.KeyDown);
            GUILayout.Label(label + " " + value.ToString("F2")); bool previous = GUI.changed; GUI.changed = false;
            float next = GUILayout.HorizontalSlider(value, low, high); bool changed = GUI.changed; GUI.changed |= previous;
            return edit && changed ? next : value;
        }
        static Rect ScaleProbeRect(Rect local, float scale)
        { return new Rect((local.x + 540) * scale, (local.y + 500) * scale, local.width * scale, local.height * scale); }
        bool ExhaustGUI()
        {
            bool dirty = false;
            GUILayout.Label("Ordered acoustic chain. Amount 0 bypasses a part; no presence booleans.");
            GUILayout.BeginHorizontal();
            string[] presets = { "Quiet", "Chambered", "Absorptive", "Open" };
            for (int i = 0; i < presets.Length; i++) if (GUILayout.Button(presets[i])) { config.vehicle.sound.exhaustSystem = ExhaustSetup.Preset(i); fields.Clear(); dirty = true; }
            GUILayout.EndHorizontal();
            var setup = config.vehicle.sound.exhaustSystem;
            for (int i = 0; i < setup.parts.Length; i++)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Part " + (i + 1) + " / " + setup.parts[i].kind);
                var priorKind = setup.parts[i].kind;
                dirty |= fields.Draw(setup.parts[i], "part" + i);
                if (setup.parts[i].kind != priorKind) { setup.parts[i] = ExhaustPart.Create(setup.parts[i].kind); fields.Clear(); }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Remove")) { var list = new System.Collections.Generic.List<ExhaustPart>(setup.parts); list.RemoveAt(i); setup.parts = list.ToArray(); dirty = true; fields.Clear(); }
                if (i > 0 && GUILayout.Button("Move earlier")) { var part = setup.parts[i]; setup.parts[i] = setup.parts[i - 1]; setup.parts[i - 1] = part; dirty = true; fields.Clear(); }
                GUILayout.EndHorizontal(); GUILayout.EndVertical();
            }
            if (setup.parts.Length < 8 && GUILayout.Button("Add exhaust component"))
            { var list = new System.Collections.Generic.List<ExhaustPart>(setup.parts); list.Add(ExhaustPart.Create(ExhaustPartKind.AbsorptiveSilencer)); setup.parts = list.ToArray(); dirty = true; }
            return dirty;
        }
        void ScenariosGUI()
        {
            scenario = (EngineBenchScenario)GUILayout.SelectionGrid((int)scenario, Enum.GetNames(typeof(EngineBenchScenario)), 2);
            GUILayout.Label(EngineBenchScenarios.Description(scenario));
            if (GUILayout.Button("Run current settings / F5")) RunScenario(config, scenario);
            if (GUILayout.Button("Repeat captured run / F6")) RepeatScenario();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture A")) CaptureSlot(true);
            if (GUILayout.Button("Capture B")) CaptureSlot(false);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Run A / F7")) RunScenario(SlotA, scenario);
            if (GUILayout.Button("Run B / Shift+F8")) RunScenario(SlotB, scenario);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Return to manual controls")) Manual();
            if (GUILayout.Button("Enable controls + 3500 RPM two-step"))
            {
                config.tune.controlLevel = 1; config.tune.twoStep = EquipmentSetting.On; config.tune.launchRpm = 3500;
                config.tune.twoStepStyle = LimiterStyle.Hard; config.tune.twoStepCut = CutMethod.Ignition; ApplyConfig();
            }
            if (GUILayout.Button("Size test clutch for current torque + flywheel / 0.20 s sync"))
            { config.vehicle.clutchCapacityNm = (float)FlywheelDynamics.MatchedClutchCapacity(config.vehicle, config.tune); ApplyConfig(); }
            GUILayout.Label("Scenario inputs and initial warm/cold state are fixed. Repeat uses the captured configuration. A/B runs use the same selected scenario. Export WAV for sample-exact comparison.");
        }
        void FilesGUI()
        {
            GUILayout.Label("Bench-only JSON + deterministic scenario WAV. Includes all specs/tune/controls/volume.");
            GUI.SetNextControlName("bench-file-path"); filePath = GUILayout.TextField(filePath);
            if (GUILayout.Button("Export current JSON")) FileAction(() => { EnsureParent(filePath); File.WriteAllText(filePath, JsonUtility.ToJson(config, true)); });
            if (GUILayout.Button("Import JSON")) FileAction(() =>
            {
                var loaded = JsonUtility.FromJson<EngineBenchConfig>(File.ReadAllText(filePath));
                if (loaded == null || loaded.Validate() != null) throw new InvalidOperationException(loaded == null ? "Invalid JSON" : loaded.Validate());
                config = loaded; ResetBench(false); fields.Clear();
                view.Build(config.vehicle.artId, Color.white); view.ApplySetup(Bench.spec, Bench.tune);
            });
            if (GUILayout.Button("Export scenario WAV / current")) ExportWav(config, "current");
            if (GUILayout.Button("Export matching A/B WAVs + configs")) { ExportWav(SlotA, "A"); ExportWav(SlotB, "B"); }
        }
        void ExportWav(EngineBenchConfig source, string label)
        {
            FileAction(() =>
            {
                if (source == null || source.Validate() != null) throw new InvalidOperationException("Capture a valid " + label + " configuration first.");
                string destination = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(filePath)), scenario + "-" + label);
                EnsureParent(destination + ".wav");
                var audition = source.Copy();
                if (label == "current") audition.volume = SharedAudioSettings.MasterVolume;
                EngineSoundAudition.WriteWav(destination + ".wav", EngineBenchRenderer.Render(audition, scenario));
                File.WriteAllText(destination + ".json", JsonUtility.ToJson(audition, true));
                File.WriteAllText(destination + "-scenario.txt", scenario + "\n" + EngineBenchScenarios.Description(scenario) + "\n120 Hz simulation / 60 Hz publication / 44100 Hz mono PCM\n");
            });
        }
        public void ExportDyno()
        { ExportDynoTo(Path.GetDirectoryName(Path.GetFullPath(filePath))); }
        public void ExportDynoTo(string folder)
        {
            FileAction(() =>
            {
                Directory.CreateDirectory(folder);
                foreach (var run in Dyno.History) WriteDyno(folder, run);
                if (Dyno.Active != null) WriteDyno(folder, Dyno.Active);
            });
        }
        static void WriteDyno(string folder, EngineDynoRun run)
        {
            string destination = Path.Combine(folder, "dyno-" + run.id);
            File.WriteAllText(destination + ".csv", run.Csv());
            File.WriteAllText(destination + ".json", JsonUtility.ToJson(run.config, true));
            File.WriteAllText(destination + "-run.txt", run.label + "\n" + (run.endedBy ?? "live export") + "\nFull chronological 120 Hz samples / engine HP and Nm / shaft Nm before gearing\n");
        }
        void FileAction(Action action) { try { action(); notice = "Bench file operation completed: " + Path.GetDirectoryName(Path.GetFullPath(filePath)); } catch (Exception e) { notice = e.Message; } }
        static void EnsureParent(string path) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))); }
    }
}

