using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using DragLab.Presentation;

namespace DragLab.Runtime
{
    // Temporary systems UI. Replace with production UI after the driving model is accepted.
    public sealed partial class PrototypeGame : MonoBehaviour
    {
        static bool benchBootHandled;
        public VehicleDefinition[] vehicles;
        public CareerEventDefinition[] career;
        public DriverSettings settings = new DriverSettings();
        PlayerProfile profile;
        RaceSimulation race;
        CareerEventDefinition activeEvent;
        LayeredCarView playerView, aiView;
        Camera raceCamera;
        DragStripView strip;
        EngineAudio engineAudio;
        readonly RaceInputAdapter controls = new RaceInputAdapter();
        Action pendingAction;
        float saveDelay;
        string screen = "Garage";
        string notice;
        double accumulator;
        float difficulty = 1;
        float freeDistance = 402.336f;
        int freeOpponent;
        bool awarded;
        string liveRunId, liveRunOwner;
        long liveRunTick, savedRunTick;
        int savedRunFailures;
        bool liveRunClosed;
        bool paused;
        bool audioSettingsOpen;
        bool practiceRun;
        PassRecord previousPass;
        PassRecord finishedPass;
        Vector2 scroll;
        Vector2 resultScroll;
        BenchmarkResult benchmark;
        string lastBenchmarkKey;
        GUIStyle heading, stat;
        float panelWidth;
        internal Func<DriverInput> verificationInput;
        internal PlayerProfile Profile { get { return profile; } }
        internal RaceSimulation Race { get { return race; } }
        internal EngineAudio Audio { get { return engineAudio; } }
        internal string ScreenName { get { return screen; } }
        internal bool IsPractice { get { return practiceRun; } }
        internal void PauseRace(bool value) { SetPaused(value); }
        internal void OpenAudioSettings() { if (screen == "Race") { SetPaused(true); audioSettingsOpen = true; } else { DiscardPreviews(); screen = "Settings"; scroll = Vector2.zero; } }
        internal void PreviewVehicle(string id) { DiscardPreviews(); profile.selectedVehicleId = id; MechanicsOwnership.Ensure(profile,profile.catalog); lastBenchmarkKey = null; screen = "Garage"; ShowGarageCar(); }
        public void ReturnToGarage() { DiscardPreviews(); if(!SyncRaceWear(true,"Returned to garage"))return; race = null; screen = "Garage"; ShowGarageCar(); }
        public void StartPractice() { StartRace(null, true); }

        VehicleDefinition Selected { get { return Array.Find(vehicles, v => v.spec.id == profile.selectedVehicleId) ?? vehicles[0]; } }
        OwnedCar Build { get { return profile.SelectedCar; } }

        void Start()
        {
            if (!benchBootHandled && Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-engine-bench") >= 0)
            { benchBootHandled = true; SceneManager.LoadScene("EngineTest"); return; }
            if (!benchBootHandled && Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-driving-bench") >= 0)
            { benchBootHandled = true; SceneManager.LoadScene("DrivingTest"); return; }
            var authoredVehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            if (authoredVehicles.Length > 0) vehicles = authoredVehicles;
            career = Resources.LoadAll<CareerEventDefinition>("Data/Events");
            if (vehicles.Length == 0) { Debug.LogError("Run DragLab > Prepare First Slice in the editor."); enabled = false; return; }
            Array.Sort(career, (a, b) => a.reputationRequired.CompareTo(b.reputationRequired));
            profile = ProfileStore.Load(true);
            settings = profile.driving;
            difficulty = profile.difficulty;
            if (Build != null && Array.Find(vehicles, v => v.spec.id == profile.selectedVehicleId) == null)
            {
                profile.selectedVehicleId = vehicles[0].spec.id;
                if (profile.SelectedCar == null) profile.cars.Add(new OwnedCar { vehicleId = profile.selectedVehicleId, tune = vehicles[0].CreateStockTune() });
            }
            if (Build != null) Build.tune.ClampTo(Selected.spec); else screen = "Collection";
            raceCamera = Camera.main;
            if (raceCamera == null)
            {
                var c = new GameObject("Race Camera"); c.tag = "MainCamera";
                raceCamera = c.AddComponent<Camera>();
            }
            raceCamera.orthographic = true;
            raceCamera.clearFlags = CameraClearFlags.SolidColor;
            raceCamera.orthographicSize = 5.2f;
            raceCamera.backgroundColor = new Color(.10f, .16f, .23f);
            raceCamera.transform.position = new Vector3(2, 0, -10);
            if (raceCamera.GetComponent<AudioListener>() == null) raceCamera.gameObject.AddComponent<AudioListener>();
            engineAudio = new GameObject("Driving Audio").AddComponent<EngineAudio>(); engineAudio.Initialize();
            strip = new GameObject("Drag Strip").AddComponent<DragStripView>(); strip.Build();
            playerView = new GameObject("Player Car").AddComponent<LayeredCarView>();
            aiView = new GameObject("Opponent Car").AddComponent<LayeredCarView>();
            ShowGarageCar();
            notice = ProfileStore.Status;
            RuntimeVerification.AttachIfRequested(this);
        }
        void ShowGarageCar()
        {
            if (Build == null) { playerView.gameObject.SetActive(false); aiView.gameObject.SetActive(false); strip.ShowGarage(); engineAudio.Silence(); controls.Clear(); return; }
            playerView.gameObject.SetActive(true);
            Color paint;
            if (!ColorUtility.TryParseHtmlString(Build.paintHex, out paint)) paint = Selected.defaultPaint;
            playerView.Build(Selected.spec.artId, paint, 1000);
            var visibleBuild = MechanicsOwnership.Resolve(profile, Build, profile.catalog);
            playerView.ApplySetup(visibleBuild.spec, visibleBuild.tune);
            playerView.transform.position = new Vector3(6.5f, 3.85f, 0);
            aiView.gameObject.SetActive(false);
            raceCamera.transform.position = new Vector3(2, 0, -10);
            strip.ShowGarage();
            engineAudio.Silence(); controls.Clear();
        }

        public void StartRace(CareerEventDefinition evt = null, bool practice = false)
        {
            if (Build == null) { notice = "No owned car. Open Collection / recovery work to buy a replacement."; return; }
            DiscardPreviews();
            if(!SyncRaceWear(true,"Retry / new race"))return;
            string entryError=CareerProgression.EntryError(profile,Build,evt); if(entryError!=null){notice=entryError;return;}
            MechanicsOwnership.Ensure(profile, MechanicsTransactions.Catalog());
            var resolved = MechanicsOwnership.Resolve(profile, Build, profile.catalog);
            if (!resolved.Drivable) { notice = "Build cannot drive: " + string.Join("; ", resolved.errors); return; }
            if (!AutoTopUp()) return;
            ProfileStore.Save(profile);
            activeEvent = evt;
            practiceRun = practice && evt == null; previousPass = Build.lastPass; finishedPass = null;
            var opponent = evt != null ? evt.opponent : practiceRun ? Selected : vehicles[freeOpponent];
            Tune enemyTune = evt != null ? JsonUtility.FromJson<Tune>(JsonUtility.ToJson(evt.opponentTune)) : opponent.CreateStockTune();
            var enemyDriver = new DriverSettings { reactionSeconds = difficulty < .5 ? .45 : difficulty < 1.5 ? .22 : .08 };
            var playerDriver = JsonUtility.FromJson<DriverSettings>(JsonUtility.ToJson(settings));
            race = RaceSessionFactory.Create(resolved.spec, resolved.tune, playerDriver,
                opponent.spec, enemyTune, enemyDriver, evt != null ? evt.distanceM : practiceRun ? 402.336 : freeDistance,
                profile.parts.FindAll(p => p.ownerId == Build.instanceId || p.ownerId == Build.engineAssemblyId));
            race.player.SetMassInputs(resolved.tune.driverMassKg, resolved.tune.payloadMassKg, resolved.tune.payloadPositionM, resolved.tune.payloadHeightM, Build.fuel.litres);
            race.player.state.fuelUsedLitres = Build.fuel.cumulativeUsedLitres;
            race.player.SetFluids(Build.fluids);
            race.player.ApplyRaceTemperatures(playerDriver.raceTemperatures,Build.thermalUtcTicks,DateTime.UtcNow.Ticks);
            if(playerDriver.warmupBeforeRace)race.BeginWarmup();
            liveRunId=MechanicsOwnership.NewId("run");liveRunOwner=Build.instanceId;liveRunTick=0;savedRunTick=-1;savedRunFailures=RunPersistence.FailureCount(race.player);liveRunClosed=false;
            PlayerProfile entered;string entrySaveError;
            var initial=RunPersistence.Capture(liveRunId,liveRunOwner,0,race.player);
            if(!MechanicsTransactions.Try(profile,profile.catalog,profile.revision,p=>{CareerProgression.Enter(p,liveRunId,liveRunOwner,evt,practiceRun);MechanicsTransactions.ApplyCheckpoint(p,initial);},ProfileStore.TrySave,out entered,out entrySaveError)){notice=entrySaveError;race=null;return;}
            profile=entered;settings=profile.driving;savedRunTick=0;
            aiView.gameObject.SetActive(true);
            aiView.Build(opponent.spec.artId, opponent.defaultPaint, 0);
            playerView.ApplySetup(race.player.spec, race.player.tune);
            aiView.ApplySetup(race.opponent.spec, race.opponent.tune);
            playerView.transform.position = new Vector3(2, -2, 0);
            aiView.transform.position = new Vector3(2, -.2f, 0);
            screen = "Race";
            accumulator = 0; awarded = false; paused = false; resultScroll = Vector2.zero;
            controls.Clear(); engineAudio.Silence();
            engineAudio.Configure(race.player.spec, race.player.tune);
            strip.Render(race);
        }

        void Update()
        {
            if (profile == null) return;
            if (pendingAction != null) { var action = pendingAction; pendingAction = null; action(); }
            if (saveDelay > 0) { saveDelay -= Time.unscaledDeltaTime; if (saveDelay <= 0) ProfileStore.Save(profile); }
            if (screen == "Garage" && Input.GetKeyDown(KeyCode.P)) StartPractice();
            if (screen != "Race" || race == null) return;
            if ((race.phase == RacePhase.Finished || paused) && Input.GetKeyDown(KeyCode.G)) { ReturnToGarage(); return; }
            if (race.phase == RacePhase.Finished && Input.GetKeyDown(KeyCode.R)) { StartRace(activeEvent, practiceRun); return; }
            if (Input.GetKeyDown(KeyCode.Escape)) SetPaused(!paused);
            if (paused) return;
            if (race.phase == RacePhase.Finished)
            {
                if(!awarded)FinishRace();
                controls.Poll(); controls.ConsumePause();
                accumulator += Math.Min(Time.unscaledDeltaTime, .1f);
                while (accumulator >= DragMath.FixedStep)
                {
                    var shutdownInput = controls.Consume();
                    if (verificationInput != null) shutdownInput = verificationInput();
                    race.Shutdown(shutdownInput);liveRunTick++; accumulator -= DragMath.FixedStep;
                }
                SaveLiveRunIfDue();RenderRace(); return;
            }
            controls.Poll();
            if (controls.ConsumePause()) { SetPaused(true); return; }
            accumulator += Math.Min(Time.unscaledDeltaTime, .1f);
            while (accumulator >= DragMath.FixedStep)
            {
                var input = controls.Consume();
                if (verificationInput != null) input = verificationInput();
                race.Step(input);
                liveRunTick++;
                accumulator -= DragMath.FixedStep;
            }
            RenderRace();
            SaveLiveRunIfDue();
            if (race.phase == RacePhase.Finished && !awarded) FinishRace();
        }

        void RenderRace()
        {
            playerView.transform.position = new Vector3(2, -2, 0);
            aiView.transform.position = new Vector3(2 + (float)(race.opponent.state.distance - race.player.state.distance) * .18f, -.2f, 0);
            strip.Render(race);
            playerView.Animate(race.player.state);
            aiView.Animate(race.opponent.state);
            engineAudio.Render(race.player.state, !paused && race.phase != RacePhase.Finished, 1);
        }

        void FinishRace()
        {
            if(awarded||race==null||race.phase!=RacePhase.Finished)return;
            var checkpoint=RunPersistence.Capture(liveRunId,liveRunOwner,liveRunTick,race.player,false,"Result settled / shutdown still running");
            PlayerProfile next;string error;
            if(!MechanicsTransactions.Try(profile,profile.catalog,profile.revision,p=>{var old=p.runCheckpoints.Find(r=>r.runId==liveRunId);if(old==null||old.tick<liveRunTick)MechanicsTransactions.ApplyCheckpoint(p,checkpoint);CareerProgression.Settle(p,liveRunId,race);},ProfileStore.TrySave,out next,out error)){notice="Result save failed; retry retained. "+error;return;}
            profile=next;settings=profile.driving;awarded=true;savedRunTick=liveRunTick;savedRunFailures=RunPersistence.FailureCount(race.player);finishedPass=Build.lastPass;
            var result=profile.careerRuns.Find(r=>r.runId==liveRunId);notice=result.outcome+(practiceRun?" / Practice / no payout":" / reward and resources saved once");
        }
        void SetPaused(bool value)
        {
            paused = value; accumulator = 0; controls.Clear();
            if (!value) audioSettingsOpen = false;
            // Windows can send a pause callback before Awake creates the audio source.
            if (engineAudio != null) engineAudio.Silence();
            if(value&&profile!=null&&race!=null)SyncRaceWear();
        }
        void OnApplicationFocus(bool focused) { if (!focused && screen == "Race" && !RuntimeVerification.Active) SetPaused(true); }
        void SaveLiveRunIfDue()
        {
            if(liveRunTick-savedRunTick>=RunPersistence.PeriodTicks || RunPersistence.FailureCount(race.player)!=savedRunFailures)SyncRaceWear();
        }
        bool SyncRaceWear(bool close=false,string reason="")
        {
            if(race==null||profile==null||liveRunClosed)return true;
            if(!close&&liveRunTick==savedRunTick)return true;
            var checkpoint=RunPersistence.Capture(liveRunId,liveRunOwner,liveRunTick,race.player,close,reason);
            PlayerProfile next;string error;
            if(!MechanicsTransactions.Try(profile,profile.catalog,profile.revision,p=>{MechanicsTransactions.ApplyCheckpoint(p,checkpoint);if(close)CareerProgression.Interrupted(p,liveRunId);},ProfileStore.TrySave,out next,out error)){notice="Run save failed; live state retained. "+error;return false;}
            profile=next;settings=profile.driving;savedRunTick=liveRunTick;savedRunFailures=RunPersistence.FailureCount(race.player);liveRunClosed=close;return true;
        }
        void OnApplicationPause(bool pause) { if (pause)SetPaused(true); }
        void OnApplicationQuit() { if (profile != null) { SyncRaceWear(true,"Quit");ProfileStore.Save(profile); } }

        internal string AuditMouseEvents = "";
        internal Rect warmupLineLockRect;
        void OnGUI()
        {
            if (RuntimeVerification.Active && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp)) AuditMouseEvents += Event.current.type + "@" + Event.current.mousePosition + ";";
            if (profile == null) return;
            RuntimeGuiRect.BeginFrame();
            float scale = RaceInputAdapter.Scale(Screen.width, Screen.height);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            panelWidth = Mathf.Min(630, width - 32);
            if (heading == null) { heading = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold }; stat = new GUIStyle(GUI.skin.label) { fontSize = 17 }; }
            if (screen == "Race") { RaceGUI(width, height); return; }
            GUILayout.BeginArea(new Rect(16, 16, panelWidth, height - 32), GUI.skin.box);
            GUILayout.Label("DRAGLAB / first systems slice", heading);
            GUILayout.Label("$" + profile.cash + "   |   REP " + profile.reputation + "   |   " + (Build == null ? "EMPTY GARAGE / retained assets preserved" : Selected.spec.displayName), stat);
            GUILayout.BeginHorizontal();
            foreach (string tab in new[] { "Garage", "Parts", "Tuning", "Stats", "Race setup", "Career", "Business", "Driving", "Settings" })
            {
                GUI.enabled = Build != null || tab == "Garage" || tab == "Driving" || tab == "Settings";
                if (GUILayout.Button(tab, GUILayout.Height(32))) pendingAction = () => { resetGameConfirm = false; DiscardPreviews(); screen = tab; scroll = Vector2.zero; if (tab == "Parts") repairsOnly = false; if (tab == "Tuning") BeginTuneDraft(); };
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            if (screen == "Garage") { if (Build == null) RecoveryGUI(); else GarageGUI(); }
            if (screen == "Parts") PartsGUI();
            if (screen == "Tuning") TuningGUI();
            if (screen == "Stats") GUILayout.Label("Full saved car statistics are shown alongside. Choose Tuning or preview a part to compare changes.");
            if (screen == "Race setup") FreeRaceGUI();
            if (screen == "Career") CareerGUI();
            if (screen == "Business") BusinessGUI();
            if (screen == "Driving") DrivingGUI();
            if (screen == "Settings") SettingsGUI();
            if (screen == "Collection") CollectionGUI();
            if (screen == "Setups") SetupGUI();
            if (screen == "Assemblies") AssembliesGUI();
            if (screen == "Valuation") ValuationGUI();
            if (screen == "Recovery") RecoveryGUI();
            GUILayout.EndScrollView();
            GUILayout.Label(notice ?? "");
            GUILayout.Label("W/Space: throttle  |  S: brake  |  E: launch / shift up  |  Q: shift down  |  Left Shift: clutch  |  Esc: pause");
            GUILayout.EndArea();
            if (Build != null && (screen == "Garage" || screen == "Parts" || screen == "Tuning" || screen == "Stats")) DrawCarStats(width, height);
        }

        void GarageGUI()
        {
            if (PartButton("recovery-open", "Operating receipts / recovery work / replacement routes")) pendingAction = OpenRecovery;
            if(PartButton("setups-open", "Save / load engine configurations and tunes")) pendingAction = OpenSetups;
            if(PartButton("assemblies-open", "Engine assemblies / detach / store / transfer")) pendingAction = OpenAssemblies;
            if(PartButton("valuation-open", "Value car / engine / chassis / loose parts")) pendingAction = () => OpenValuation(ValuationKind.Car, Build.instanceId);
            MaintenanceSummary(); FuelGarage(); FluidGarage();
            if (GUILayout.Button("Engine / sound test bench")) { ProfileStore.Save(profile); SceneManager.LoadScene("EngineTest"); return; }
            var tune = Build.tune;
            MechanicsOwnership.Ensure(profile, profile.catalog ?? MechanicsTransactions.Catalog());
            var resolved = MechanicsOwnership.Resolve(profile, Build, profile.catalog);
            string key = JsonUtility.ToJson(resolved.spec) + JsonUtility.ToJson(resolved.tune);
            if (resolved.Drivable) {
                if (key != lastBenchmarkKey) { benchmark = Benchmark.QuarterMile(resolved.spec, resolved.tune); lastBenchmarkKey = key; }
                GUILayout.Label(Selected.spec.drive + " / " + (resolved.spec.massKg * resolved.tune.MassMultiplier).ToString("F0") + " kg / " + DragMath.PeakHorsepower(resolved.spec, resolved.tune).ToString("F0") + " hp / " + benchmark.performanceClass + " " + benchmark.points + " PP", stat);
                GUILayout.Label("Full-tank auto benchmark: " + benchmark.elapsed.ToString("F2") + " s at " + SharedUnitSettings.Speed(benchmark.trapKmh / 3.6, "F0") + " (modeled)");
            } else GUILayout.Label("Incomplete / unavailable build: no driving benchmark until repaired or assembled.", stat);
            GUI.enabled = resolved.Drivable;
            if (GUILayout.Button("Physical driving bench / this installed build"))
                pendingAction = () => { PhysicalBenchTransfer.Put(PhysicalBenchTransfer.Capture(profile, Build)); ProfileStore.Save(profile); SceneManager.LoadScene("DrivingTest"); };
            if (GUILayout.Button("Practice this build / P (quarter mile, no payout)", GUILayout.Height(32))) pendingAction = StartPractice;
            GUI.enabled = true;
            if (Build.lastPass != null)
                GUILayout.Label("LAST PASS / " + Build.lastPass.distanceM.ToString("F1") + " m / " + Build.lastPass.elapsedSeconds.ToString("F3") + " s / " + SharedUnitSettings.Speed(Build.lastPass.trapKmh / 3.6));
            GUILayout.Label(resolved.Drivable ? (resolved.warnings.Count == 0 ? "BUILD READY" : "ENGINE CAN RUN / COMPONENT WARNINGS") : "ENGINE / BUILD UNAVAILABLE: " + string.Join("; ", resolved.errors), stat);
            if (resolved.warnings.Count > 0) GUILayout.Label(string.Join("; ", resolved.warnings));
            if (GUILayout.Button("Inspect / buy / swap individual parts")) pendingAction = () => ShowParts(PartCategory.Pistons);
            GUILayout.Label("Installed " + resolved.tune.inductionUpgrade + " induction / " + TransmissionDynamics.DisplayName(TransmissionDynamics.Configuration(resolved.spec, resolved.tune)) + " gearbox. Hardware changes are in Parts.");
            if (GUILayout.Button("Tune this car / preview changes")) pendingAction = OpenTuning;
            if (GUILayout.Button("Full car statistics")) pendingAction = () => { DiscardPreviews(); screen = "Stats"; scroll = Vector2.zero; };
            GUILayout.Space(8);
            if (GUILayout.Button("Owned car collection / dealership / buy another copy")) pendingAction = OpenCollection;
            GUILayout.Label("GARAGE BUSINESS / $"+BusinessPolicy.hourlyRates[profile.garageLevel-1]+" per hour / "+BusinessPolicy.capsHours[profile.garageLevel-1]+" hour cap",stat);
            if(PartButton("garage-business","Jobs / expansion / claim income"))pendingAction=OpenBusiness;
            int idle=GarageBusiness.Pending(profile,BusinessPolicy,DateTime.UtcNow.Ticks);
            GUI.enabled=idle>0;if(PartButton("garage-idle","Claim $"+idle))QueuePartAction(p=>GarageBusiness.Claim(p,BusinessPolicy,DateTime.UtcNow.Ticks));GUI.enabled=true;

        }

        bool EquipmentButtons(string label, ref EquipmentSetting setting)
        {
            GUILayout.Label(label);
            var chosen = (EquipmentSetting)GUILayout.SelectionGrid((int)setting, new[] { "Factory", "Off", "On" }, 3);
            if (chosen == setting) return false;
            setting = chosen; return true;
        }
        bool Slider(string name, ref float value, float min, float max, string format)
        {
            GUILayout.Label(name + ": " + value.ToString(format));
            var kind = Event.current.rawType;
            float next = GUILayout.HorizontalSlider(value, min, max);
            bool changed = GUI.enabled && (kind == EventType.MouseDown || kind == EventType.MouseDrag || kind == EventType.MouseUp || kind == EventType.KeyDown) && Math.Abs(next - value) > .0001f;
            if (changed) value = next; return changed;
        }
        void UpgradeButton(string type, int level, string effect)
        {
            int cost = PlayerProfile.UpgradeCost(type, level);
            int max = type == "controls" ? 1 : 3;
            GUI.enabled = level < max && profile.cash >= cost;
            if (GUILayout.Button(type.ToUpperInvariant() + " " + level + "/" + max + "  |  " + effect + (level < max ? "  |  $" + cost : "  |  MAX")))
            { profile.Upgrade(type); ProfileStore.Save(profile); notice = "Installed " + type + "."; }
            GUI.enabled = true;
        }
        void FreeRaceGUI()
        {
            GUILayout.Label("FREE RACE", heading);
            GUILayout.Label("No entry fee. $120 win / $40 completed loss. No reputation farming.");
            freeOpponent = GUILayout.SelectionGrid(freeOpponent, Array.ConvertAll(vehicles, v => v.spec.displayName), 1);
            int selectedDistance = freeDistance < 300 ? 0 : freeDistance < 600 ? 1 : 2;
            selectedDistance = GUILayout.SelectionGrid(selectedDistance, new[] { "1/8 mile", "1/4 mile", "1/2 mile" }, 3);
            freeDistance = new[] { 201.168f, 402.336f, 804.672f }[selectedDistance];
            DifficultyGUI();
            if (GUILayout.Button("Stage free race", GUILayout.Height(44))) pendingAction = () => StartRace();
        }
        void CareerGUI()
        {
            GUILayout.Label("CAREER / F to S",heading);
            GUILayout.Label("Qualifications unlock rivals. First wins add REP and a bonus; repeats pay cash only. Entry and rewards settle with the real build and its fuel/wear.");
            DifficultyGUI();
            foreach(var evt in career){
                GUILayout.Label(evt.tier+" / "+evt.eventKind+" / "+evt.displayName+" / "+evt.rivalName);
                string error=CareerProgression.EntryError(profile,Build,evt);
                GUILayout.Label("REP "+evt.reputationRequired+" / entry $"+evt.entryFee+" / win $"+evt.cashWin+" / completed loss $"+evt.cashLoss+" / first bonus $"+evt.firstWinBonus+" / "+evt.reputationWin+" REP");
                if(error!=null)GUILayout.Label(error);
                GUI.enabled=error==null;
                if(PartButton("career-"+evt.stableId,(profile.careerWins.Contains(evt.stableId)?"[WON] ":"")+"Stage "+evt.displayName))pendingAction=()=>StartRace(evt);GUI.enabled=true;
            }
            GUILayout.Label("Recent race outcomes");foreach(var r in profile.careerRuns.FindAll(r=>r.complete).GetRange(Math.Max(0,profile.careerRuns.FindAll(r=>r.complete).Count-10),Math.Min(10,profile.careerRuns.FindAll(r=>r.complete).Count)))GUILayout.Label((r.eventId??"Free / practice")+" / "+r.outcome);
        }
        void DifficultyGUI()
        {
            float old = difficulty;
            difficulty = GUILayout.SelectionGrid((int)difficulty, new[] { "Easy AI", "Normal AI", "Hard AI" }, 3);
            if (old != difficulty) { profile.difficulty = (int)difficulty; saveDelay = .5f; }
            GUILayout.Label("Difficulty changes AI reaction time. Both cars use identical physics.");
        }
        void DrivingGUI()
        {
            string before = JsonUtility.ToJson(settings);
            GUILayout.Label("DRIVING ASSISTS", heading);
            GUILayout.BeginHorizontal();
            foreach (DrivingPreset preset in Enum.GetValues(typeof(DrivingPreset)))
                if (GUILayout.Button(preset == DrivingPreset.Automatic ? "Drive automatically" : preset == DrivingPreset.Sequential ? "Shift myself" : "Full manual")) settings.ApplyPreset(preset);
            GUILayout.EndHorizontal();
            GUILayout.Label("Presets change launch, shifting, throttle, traction and rev matching. Other preferences remain yours.");
            settings.shifting = (ShiftMode)GUILayout.SelectionGrid((int)settings.shifting, new[] { "Automatic shifts", "Shift myself", "Manual clutch" }, 3);
            GUILayout.Label("Shift myself controls E/Q with clutch assistance. Install sequential manual hardware in Parts for faster selection.");
            settings.launch = (LaunchMode)GUILayout.SelectionGrid((int)settings.launch, new[] { "Auto launch", "Assisted launch", "Manual launch" }, 3);
            settings.throttleAssist = GUILayout.Toggle(settings.throttleAssist, "Automatic throttle after launch");
            SimulationSettingsView.Draw(settings);
            settings.shutdownBrakeAssist = GUILayout.Toggle(settings.shutdownBrakeAssist, "Automatic braking in shutdown lane after finish");
            Slider("Digital clutch release time (seconds)", ref settings.clutchReleaseSeconds, .02f, .6f, "F2");
            GUILayout.Label("Sound level: " + SharedAudioSettings.MasterVolume.ToString("P0") + " / change it on Settings.");
            GUILayout.Label("Staging: neutral, brakes held. Hold W/Space to rev freely; no clutch needed.");
            GUILayout.Label("Auto launch reacts in 0.18 s. E engages first for assisted/manual after green (Enter also works).");
            GUILayout.Label("Launch engages first gear and releases the staging brakes.");
            GUILayout.Label("Manual clutch launch: hold Shift, E at green, then release Shift.");
            GUILayout.Label("Manual clutch shifting: keep W/Space held, hold Left Shift, E/Q to change gear, then release Shift. Disable rev match assist and blip gas for downshifts.");
            GUILayout.Label("Touch/mouse pedals: hold middle/right for full input; slide left for partial pressure. Different fingers can hold each pedal. S applies brakes during a run and after finish.");
            GUILayout.Label("Hardware determines clutch / converter / DCT / CVT behavior and downshift protection. Manual clutch can stall; clutch in + Enter restarts. Shared mechanical wear/failure persists on owned parts.");
            if (before != JsonUtility.ToJson(settings)) saveDelay = .5f;
        }

        void RaceGUI(float width, float height)
        {
            var state = race.player.state;
            float hudHeight = race.player.spec.drive == DriveType.AWD || state.brakesFailed || state.drivetrainFailed ? 218 : 192;
            GUILayout.BeginArea(new Rect(16, 16, width - 32, hudHeight), GUI.skin.box);
            GUILayout.Label((activeEvent != null ? activeEvent.displayName : practiceRun ? "PRACTICE / NO PAYOUT" : "FREE RACE") + " / " + race.distance.ToString("F1") + " m", heading);
            bool cvt = race.player.transmission.type == TransmissionType.CVT;
            string driveLabel = cvt ? (state.gear == 0 ? "CVT N" : "CVT D / RATIO " + state.cvtRatio.ToString("F2")) : "GEAR " + (state.gear == 0 ? "N" : state.gear.ToString());
            GUILayout.Label(driveLabel + "    " + SharedUnitSettings.Speed(state.speed, "F0") + "    " + state.rpm.ToString("F0") + " RPM    SLIP " + (state.slip * 100).ToString("F0") + "%", stat);
            double target = state.launched ? (cvt ? Math.Min(race.player.tune.shiftRpm, race.player.transmission.cvtTargetRpm) : race.player.ShiftPoint(state.gear)) : race.player.tune.launchRpm;
            GUILayout.Label((state.launched ? (cvt ? "CVT operating target " : "Shift target ") : "Launch target ") + target.ToString("F0") + " RPM" + (race.player.tune.partShiftLight && !cvt && state.launched && state.rpm >= race.player.tune.ShiftLightRpm(race.player.spec) && state.gear < race.player.spec.gears.Length ? "  /  SHIFT NOW" : "") +
                "   Throttle " + (state.throttle * 100).ToString("F0") + "%   Clutch " + (state.clutch * 100).ToString("F0") + "%   Brake " + (state.brake * 100).ToString("F0") + "%   " + (state.acceleration / DragMath.Gravity).ToString("F2") + " g");
            Rect rpmBar = GUILayoutUtility.GetRect(100, 10, GUILayout.ExpandWidth(true));
            Color oldColor = GUI.color;
            GUI.color = new Color(.2f, .23f, .28f); GUI.DrawTexture(rpmBar, Texture2D.whiteTexture);
            GUI.color = state.limiter ? Color.red : new Color(.3f, .8f, .65f);
            GUI.DrawTexture(new Rect(rpmBar.x, rpmBar.y, rpmBar.width * Mathf.Clamp01((float)state.rpm / race.player.spec.redlineRpm), rpmBar.height), Texture2D.whiteTexture);
            GUI.color = Color.yellow; GUI.DrawTexture(new Rect(rpmBar.x + rpmBar.width * (float)target / race.player.spec.redlineRpm - 1, rpmBar.y, 2, rpmBar.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            string message = race.warmup ? "WARM-UP / LIGHTS WAITING" : race.time < 1.5 ? "STAGED" : race.time < 2 ? "AMBER 1" : race.time < 2.5 ? "AMBER 2" : race.time < 3 ? "AMBER 3" : "GREEN";
            if (state.disqualified) message = "RED LIGHT";
            else if (state.stagingBrakesHeld) message += " / NEUTRAL / BRAKES HELD";
            else if (state.finished) message = "FINISH RECORDED / SHUTDOWN LANE    " + Math.Max(0, state.distance - state.finishDistance).ToString("F1") + " m beyond finish";
            else if (race.phase == RacePhase.Racing) message += "    " + (state.feedbackRemaining > 0 ? state.feedback : "") + "    " + state.distance.ToString("F1") + " m";
            GUILayout.Label(message, stat);
            GUILayout.Label((state.engineFailed ? "ENGINE FAILED" : state.stalled ? "STALLED / CLUTCH IN + ENTER" : state.bogging ? "BOGGING / stage more RPM or release clutch gradually" : state.limiter ? "REV LIMITER" : state.slip > .2 ? "HEAVY WHEELSPIN / reduce torque or improve grip" : "") +
                "   Clutch slip " + state.clutchSlipRpm.ToString("F0") + " RPM / shaft " + SharedUnitSettings.Torque(state.transmittedTorqueNm, "F0") +
                " / boost " + SharedUnitSettings.Boost(state.boostBar) + " / engine " + SharedUnitSettings.Torque(state.engineTorqueNm, "F0") + " / " + state.engineHorsepower.ToString("F0") + " hp");
            GUILayout.Label(TransmissionDynamics.DisplayName(race.player.transmission) + (race.player.transmission.type == TransmissionType.CVT ? " ratio " + state.cvtRatio.ToString("F2") : "") +
                " / " + race.player.engine.data.fueling + " / " + (state.launchControlActive ? "TWO-STEP" : "") + (state.antiLagActive ? " ANTI-LAG" : "") +
                " / heat " + (state.exhaustHeat * 100).ToString("F0") + "% / damage " + (state.engineDamage * 100).ToString("F0") + "% / warm " + (state.engineTemperature * 100).ToString("F0") + "%");
            if (race.player.spec.drive == DriveType.AWD || state.brakesFailed || state.drivetrainFailed)
                GUILayout.Label("Drive " + race.player.spec.drive + " / front:rear " + state.frontTorqueShare.ToString("P0") + ":" + state.rearTorqueShare.ToString("P0") + " / brakes " + state.brakeCapacityFactor.ToString("P0") + (state.drivetrainFailed ? " / NO DRIVE PATH" : state.openCenterFreeBranch ? " / FREE SHAFT " + Math.Max(state.frontShaftRpm, state.rearShaftRpm).ToString("F0") + " RPM" : ""));
            GUILayout.EndArea();
            if (paused)
            {
                if (audioSettingsOpen)
                {
                    GUILayout.BeginArea(new Rect(width * .5f - 230, height * .5f - 160, 460, 320), GUI.skin.box);
                    AudioSettingsView.Draw(Audio);
                    if (GUILayout.Button("Back to pause menu", GUILayout.Height(32))) audioSettingsOpen = false;
                    GUILayout.EndArea(); return;
                }
                GUILayout.BeginArea(new Rect(width * .5f - 180, height * .5f - 80, 360, 180), GUI.skin.box);
                GUILayout.Label("PAUSED", heading);
                if (GUILayout.Button("Resume", GUILayout.Height(32))) pendingAction = () => SetPaused(false);
                if (GUILayout.Button("Audio settings", GUILayout.Height(32))) audioSettingsOpen = true;
                if (GUILayout.Button("Garage", GUILayout.Height(32))) pendingAction = ReturnToGarage;
                GUILayout.EndArea();
                return;
            }
            if (race.phase == RacePhase.Finished)
            {
                GUILayout.BeginArea(new Rect(width * .5f - 280, 16 + hudHeight, 560, height - hudHeight - 133), GUI.skin.box);
                GUILayout.Label(state.disqualified ? "RED LIGHT / DISQUALIFIED" : race.timedOut ? "TIMEOUT" : race.playerWon ? "WIN" : "LOSS", heading);
                GUILayout.Label("Reaction: " + (state.reaction >= 0 ? state.reaction.ToString("F3") + " s" : "—") + "   ET: " + (state.finished ? state.elapsed.ToString("F3") + " s" : "DNF"), stat);
                GUILayout.Label("Trap: " + SharedUnitSettings.Speed(state.trapSpeed / 3.6) + "   Opponent ET: " + race.opponent.state.elapsed.ToString("F3") + " s");
                resultScroll = GUILayout.BeginScrollView(resultScroll);
                foreach (var split in state.splits) GUILayout.Label(split.Key.ToString("F1") + " m: " + split.Value.ToString("F3") + " s");
                GUILayout.Label(notice);
                int failedParts = race.player.mechanics.parts.FindAll(PartFailed).Count;
                if (failedParts > 0) {
                    GUILayout.Label("COMPONENT FAILURES / " + failedParts + " — " + string.Join(", ", race.player.mechanics.parts.FindAll(PartFailed).ConvertAll(p => p.slot)));
                    if (PartButton("race-repairs", "Review failed parts / replacements")) pendingAction = () => { ReturnToGarage(); ShowRepairs(); };
                }
                if (finishedPass != null) GUILayout.Label(finishedPass.Compare(previousPass, SharedUnitSettings.Current));
                GUILayout.Label("Shutdown lane: " + (state.speed < .01 ? "STOPPED" : SharedUnitSettings.Speed(state.speed, "F0")) +
                    " / " + (race.player.driver.shutdownBrakeAssist ? "automatic brakes" : "hold S or the brake pedal") + " / recorded ET and trap stay fixed.");
                GUILayout.Label(race.telemetry.Analysis(race.player));
                if (GUILayout.Button("Export this run's telemetry CSV"))
                {
                    try { string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ProfileStore.FilePath), "last-run.csv"); System.IO.File.WriteAllText(path, race.telemetry.Csv()); notice = "Exported: " + path; }
                    catch (Exception e) { notice = "Export failed: " + e.Message; }
                }
                GUILayout.EndScrollView();
                if (GUILayout.Button("Garage / G", GUILayout.Height(32))) pendingAction = ReturnToGarage;
                if (GUILayout.Button("Run again / R", GUILayout.Height(32))) pendingAction = () => StartRace(activeEvent, practiceRun);
                GUILayout.EndArea();
                GUI.Box(RaceInputAdapter.ControlRect(RaceInputAdapter.BrakeControl, width, height), "Brake / S\n" + (controls.Peek().brake * 100).ToString("F0") + "%", GUI.skin.button);
                return;
            }
            var held = controls.Peek();
            if(race.warmup) {
                GUILayout.BeginArea(new Rect(16,16+hudHeight,width-32,175),GUI.skin.box);
                GUILayout.Label("WARM-UP: W/Space gas; E engage/upshift; Q downshift to neutral; Shift clutch; S brake. Stop before staging.");
                var tires=race.player.mechanics.Part(DragLab.Domain.Mechanics.PartCategory.Tires);
                GUILayout.Label("Tires C: "+string.Join(" / ",System.Array.ConvertAll(tires.condition.tires,t=>t.temperatureC.ToString("F1")))+" / gearbox "+race.player.mechanics.Part(DragLab.Domain.Mechanics.PartCategory.Gearbox).condition.temperatureC.ToString("F1")+" C / oil "+state.fluids.oilTemperatureC.ToString("F1")+" C");
                if(race.player.spec.drive!=DriveType.AWD){state.warmupLineLock=GUILayout.Toggle(state.warmupLineLock,"Burnout line lock: brake non-driven axle only (hold S)");if(Event.current.type==EventType.Repaint)warmupLineLockRect=RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());}
                GUI.enabled=race.WarmupStopped&&!state.engineFailed&&!state.drivetrainFailed;
                if(PartButton("warmup-ready","Ready / start lights (Enter)"))pendingAction=()=>race.BeginCountdown();
                GUI.enabled=true;
                if(PartButton("warmup-abort","Return to garage / keep fuel and wear"))pendingAction=ReturnToGarage;
                GUILayout.EndArea();
            }
            string[] labels = { "Gas / W or Space\n" + (held.throttle * 100).ToString("F0") + "%", "Clutch / Shift\n" + (held.clutch * 100).ToString("F0") + "%", "Shift down / Q", state.launched ? "Shift up / E" : "N → 1 / E\nLaunch at green", "Pause", "Brake / S\n" + (held.brake * 100).ToString("F0") + "%" };
            if(race.warmup){labels[RaceInputAdapter.UpControl]="Engage / shift up / E";labels[RaceInputAdapter.DownControl]="Downshift / neutral / Q";}
            if (cvt) { labels[RaceInputAdapter.DownControl] = "CVT / continuous ratio"; labels[RaceInputAdapter.UpControl] = state.launched ? "Drive / CVT" : "N → D / E\nLaunch at green"; }
            if (state.stalled) labels[RaceInputAdapter.UpControl] = "Restart / E\nClutch in if manual";
            for (int i = 0; i < labels.Length; i++) GUI.Box(RaceInputAdapter.ControlRect(i, width, height), labels[i], GUI.skin.button);
            GUI.Label(new Rect(16, height - 24, width - 32, 22), race.warmup?"Warm-up: E engages drive, Q returns first gear to neutral. Stop, then Ready / Enter begins the lights.":cvt ? "Hold W or Space for gas. E selects Drive at green; CVT then adjusts its ratio continuously. Enter also launches." : "Hold W or Space for gas. E engages first at green, then shifts up; keep holding gas. Enter also launches.");
        }
    }
}


