using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    // Development build probe: runs the real Update/OnGUI loop with injected driver inputs.
    // All saves and screenshots go into an explicitly supplied fixture directory.
    public sealed partial class RuntimeVerification : MonoBehaviour
    {
        PrototypeGame game;
        string output;
        int errors;
        float startedAt;
        public static bool Active { get { return Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-verify") >= 0; } }
        public static void AttachIfRequested(PrototypeGame target)
        {
            if (!Active) return;
            var probe = target.gameObject.AddComponent<RuntimeVerification>(); probe.game = target;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--draglab-verify");
            probe.output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(probe.output);
            Application.runInBackground = true;
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        IEnumerator Start()
        {
            startedAt = Time.realtimeSinceStartup;
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                var routine = stack.Peek();
                bool more;
                try { more = routine.MoveNext(); }
                catch (Exception e)
                {
                    File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), "FAIL: " + e + "\n");
                    Debug.LogError(e); Application.Quit(1); yield break;
                }
                if (!more) { stack.Pop(); continue; }
                if (routine.Current is IEnumerator nested) stack.Push(nested);
                else yield return routine.Current;
            }
            Application.Quit(0);
        }
        void Update()
        {
            if (startedAt > 0 && Time.realtimeSinceStartup - startedAt > 180)
            {
                File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), "FAIL: Probe exceeded 180 seconds.\n");
                Application.Quit(2);
            }
        }
        IEnumerator Run()
        {
            yield return null;
            yield return Capture("garage");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-paid-only") >= 0) { yield return PaidOperationsRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-valuation-only") >= 0) { yield return ValuationRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-assembly-setups-only") >= 0) { yield return AssemblySetupRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-collection-only") >= 0) { yield return CollectionRun(); yield break; }
            Check(game.Audio.Ready, "Procedural engine/tire sources initialized.");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-recovery-stage")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-recovery-resume")>=0){yield return InterruptedRun();yield break;}
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-stats-summary-only") >= 0) { yield return StatsSummaryRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-maintenance-only") >= 0) { yield return MaintenanceRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-tuning-gestures-only") >= 0) { yield return TuningGesturesRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-part-effects-only") >= 0)
            { yield return PartEffectsRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-axle-failures-only") >= 0)
            { yield return AxleFailureRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-durability-only") >= 0)
            { yield return StockDurabilityRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-inspection-only") >= 0)
            { yield return InspectionRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-reset-only") >= 0)
            { yield return ResetGameRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-parts-only") >= 0)
            { yield return PartsRun(); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-powertrain-only") >= 0)
            {
                yield return PowertrainRun(); yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-induction-only") >= 0)
            {
                yield return InductionRun(); yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-keyboard-only") >= 0)
            {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
                yield return KeyboardRun();
                yield break;
#else
                throw new InvalidOperationException("Keyboard probe requires the Windows development player.");
#endif
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-capture-only") >= 0)
            {
                Check(errors == 0, "Screenshot capture reports no runtime errors.");
                File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), "PASS: capture-only probe. Full gameplay suite not run.\n");
                yield break;
            }
            Check(game.Profile.cash == 1200, "Fresh isolated profile.");
            int cash = game.Profile.cash;
            game.StartRace(game.career[0]);
            while (game.Race.time < 2.55) yield return null;
            Check(game.Race.player.state.gear == 0 && game.Race.player.state.stagingBrakesHeld && game.Race.player.state.brake == 1 &&
                game.Race.opponent.state.gear == 0 && game.Race.opponent.state.brake == 1, "Both cars stage in neutral with brakes held.");
            yield return Capture("starting-lights");
            while (game.Race.time < 6.7) yield return null;
            // Unity starts recording this buffer on first request; sample again after DSP has filled it.
            float[] audio = new float[1024]; AudioListener.GetOutputData(audio, 0);
            yield return new WaitForSecondsRealtime(.3f);
            Check(game.Audio.CurrentPitch > .2 && game.Audio.CurrentVolume > 0, "Engine audio follows the running car.");
            AudioListener.GetOutputData(audio, 0);
            double sum = 0; foreach (float sample in audio) sum += sample * sample;
            File.WriteAllText(Path.Combine(output, "audio-probe.txt"), "Engine sources ready. Pitch at race capture: " + game.Audio.CurrentPitch + "\nUnity listener output RMS: " + Math.Sqrt(sum / audio.Length) + "\nThis checks generated playback signals, not speaker sound quality.\n");
            Check(sum > .000001, "Warmed listener buffer contains nonzero engine audio.");
            yield return Capture("automatic-race");
            yield return AwaitFinish();
            yield return Capture("automatic-result");
            Check(game.Race.playerWon && game.Profile.cash == cash + game.career[0].cashWin, "Career win settles once.");
            Check(game.Profile.SelectedCar.completedRuns == 1, "Per-car records stored.");
            int settled = game.Profile.cash;
            double settledEt = game.Race.player.state.elapsed, settledTrap = game.Race.player.state.trapSpeed;
            double shutdownDistance = game.Race.player.state.distance;
            game.PauseRace(true);
            yield return new WaitForSecondsRealtime(.25f);
            Check(game.Race.player.state.distance == shutdownDistance, "Pause freezes shutdown motion as well as racing.");
            game.PauseRace(false);
            yield return AwaitStop();
            Check(game.Profile.cash == settled && game.Profile.SelectedCar.completedRuns == 1 && game.Race.player.state.elapsed == settledEt &&
                game.Race.player.state.trapSpeed == settledTrap && game.Race.player.state.distance > game.Race.distance,
                "Automatic shutdown stops past the finish without changing times, cash or records.");
            yield return Capture("automatic-stopped");
            game.ReturnToGarage();
            Check(game.Profile.Upgrade("engine"), "Earned funds can purchase an engine stage.");
            Check(game.Profile.SelectedCar.tune.engineLevel == 1, "Purchased part is installed.");
            game.Profile.SelectedCar.SavePreset(0);
            game.Profile.SelectedCar.tune.finalDrive = 5;
            Check(game.Profile.SelectedCar.LoadPreset(0) && game.Profile.SelectedCar.tune.engineLevel == 1, "Tune preset retains installed parts.");
            Check(game.Profile.Upgrade("exhaust"), "Earned funds purchase an exhaust tone stage.");
            game.Profile.SelectedCar.tune.inductionUpgrade = EngineUpgradeInduction.Turbo;
            game.Profile.SelectedCar.tune.blowOffValve = BlowOffType.Chirp;
            InstallFixtureHardware();
            game.settings.launch = LaunchMode.Manual;
            game.settings.shifting = ShiftMode.Sequential;
            game.settings.throttleAssist = false;
            game.StartRace();
            game.verificationInput = () => new DriverInput {
                throttle = game.Race.player.state.shiftRemaining > 0 ? 0 : 1, clutch = game.Race.time < 3.12 ? 1 : 0,
                launch = !game.Race.player.state.launched && game.Race.time >= 3.12,
                shiftUp = game.Race.player.state.launched && game.Race.player.state.speed > 2 && game.Race.player.state.clutch < .2 && game.Race.player.state.clutchSlipRpm < 250 && game.Race.player.state.rpm >= DragMath.ShiftPoint(game.Race.player.spec, game.Race.player.tune, game.Race.player.state.gear)
            };
            yield return AwaitFinish();
            Check(game.Race.player.state.finished && game.Race.player.state.gear > 1, "Manual launch with sequential shifting completes.");
            Check(game.Audio.BlowOffCount > 0, "Charged turbo releases through actual throttle-lift audio during manual shifts.");
            Check(game.Race.telemetry.samples.Count > 30, "Real run telemetry recorded.");
            File.WriteAllText(Path.Combine(output, "manual-run.csv"), game.Race.telemetry.Csv());
            yield return Capture("manual-result");
            ProfileStore.Save(game.Profile);
            var loaded = ProfileStore.Load();
            Check(loaded.SelectedCar.completedRuns == 2 && loaded.driving.launch == LaunchMode.Manual && loaded.SelectedCar.presets.Count == 1,
                "Records, driving settings and preset survive reload.");
            Check(loaded.SelectedCar.tune.exhaustLevel == 1 && loaded.SelectedCar.tune.inductionUpgrade == EngineUpgradeInduction.Turbo &&
                loaded.SelectedCar.tune.blowOffValve == BlowOffType.Chirp, "Installed exhaust/induction and valve survive player save reload.");
            game.Profile.SelectedCar.tune.inductionUpgrade = EngineUpgradeInduction.Supercharger;
            InstallFixtureHardware();
            game.settings.launch = LaunchMode.Automatic;
            game.settings.shifting = ShiftMode.ManualClutch;
            game.settings.throttleAssist = true;
            game.settings.shutdownBrakeAssist = false;
            game.StartRace();
            bool applyBrake = false;
            game.verificationInput = () => {
                var state = game.Race.player.state;
                bool shift = state.launched && state.speed > 3 && state.shiftRemaining <= 0 && state.clutchSlipRpm < 250 && state.rpm >= DragMath.ShiftPoint(game.Race.player.spec, game.Race.player.tune, state.gear) - 100;
                return new DriverInput { throttle = 1, clutch = shift || state.shiftRemaining > 0 ? 1 : 0, shiftUp = shift, brake = applyBrake ? 1 : 0 };
            };
            while (game.Race.time < 5) yield return null;
            Check(game.Audio.EstimatedBoost > .1 && game.Audio.BlowOffCount == 0 && game.Audio.CurrentVolume > 0,
                "Supercharger whine runs under load without turbo blow-off events.");
            double pausedTime = game.Race.time;
            game.PauseRace(true);
            yield return new WaitForSecondsRealtime(.25f);
            Check(game.Race.time == pausedTime && game.Audio.CurrentVolume == 0, "Pause freezes race time and silences driving audio.");
            game.PauseRace(false);
            yield return AwaitFinish();
            Check(game.Race.player.state.finished && game.Race.player.state.gear > 1, "Automatic launch with manual-clutch shifting completes.");
            Check(game.Profile.SelectedCar.completedRuns == 3 && game.Audio.CurrentVolume == 0, "Manual-clutch pass records once and finished audio is silent.");
            File.WriteAllText(Path.Combine(output, "manual-clutch-run.csv"), game.Race.telemetry.Csv());
            yield return Capture("manual-clutch-result");
            double coastDistance = game.Race.player.state.distance;
            yield return new WaitForSecondsRealtime(.5f);
            Check(game.Race.player.state.brake == 0 && game.Race.player.state.speed > 10 && game.Race.player.state.distance > coastDistance,
                "Manual shutdown coasts while brake is released.");
            applyBrake = true;
            yield return AwaitStop();
            Check(game.Race.player.state.brake == 1 && game.Profile.SelectedCar.completedRuns == 3, "Manual shutdown pedal stops the car without duplicating records.");
            ProfileStore.Save(game.Profile);
            Check(!ProfileStore.Load().driving.shutdownBrakeAssist, "Manual brake preference persists.");
            yield return Capture("manual-stopped");
            settled = game.Profile.cash;
            game.settings.launch = LaunchMode.Manual;
            game.StartRace();
            game.verificationInput = () => new DriverInput { launch = game.Race.time < 1 };
            yield return AwaitFinish();
            Check(game.Race.player.state.disqualified && game.Profile.cash == settled, "Red light earns no reward.");
            yield return Capture("red-light-result");
            game.verificationInput = null;
            game.settings.ApplyPreset(DrivingPreset.Automatic);
            int practiceCash = game.Profile.cash, practiceRep = game.Profile.reputation, priorRuns = game.Profile.SelectedCar.completedRuns;
            game.ReturnToGarage(); game.StartPractice();
            yield return AwaitFinish();
            Check(game.Profile.cash == practiceCash && game.Profile.reputation == practiceRep && game.Profile.SelectedCar.completedRuns == priorRuns + 1 &&
                game.Profile.SelectedCar.lastPass != null, "Practice writes records without any payout.");
            yield return Capture("practice-result");
            var stored = ProfileStore.Load().SelectedCar.lastPass;
            Check(stored != null && Math.Abs(stored.elapsedSeconds - game.Race.player.state.elapsed) < 1e-10 && stored.driving.shifting == ShiftMode.Automatic,
                "Practice last-pass snapshot survives player save reload within JSON double precision: saved " + (stored == null ? "null" : stored.elapsedSeconds.ToString("R")) + " / runtime " + game.Race.player.state.elapsed.ToString("R"));
            game.ReturnToGarage(); yield return null;
            Check(game.ScreenName == "Garage", "Practice returns to garage without restarting.");
            foreach (var vehicle in game.vehicles)
            {
                if (game.Profile.cars.Find(c => c.vehicleId == vehicle.spec.id) == null)
                    game.Profile.cars.Add(new OwnedCar { vehicleId = vehicle.spec.id, tune = vehicle.CreateStockTune() });
                game.PreviewVehicle(vehicle.spec.id);
                yield return null;
                yield return Capture("garage-" + vehicle.spec.artId);
            }
            Check(errors == 0, "No runtime errors or GUI exceptions.");
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), "PASS: Windows player Update/OnGUI, four-car rendering, start tree, automatic career win, one-time settlement, upgrade/preset, manual launch/sequential shifts, automatic launch/manual-clutch shifts, paused time/audio/shutdown motion, nonzero warmed listener audio, automatic and manual shutdown braking, stable finish times/rewards, records/settings/save reload, clutch/brake telemetry and red-light denial.\nUnity: " + Application.unityVersion + "\nScreenshots: 1280x720 requested window.\nRuntime errors: " + errors + "\nInputs injected at the driver boundary; physical keyboard and Android device touch are not covered.\n");
        }
        IEnumerator AwaitStop()
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (game.Race.player.state.speed > 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.Race.player.state.speed == 0, "Braking stops the car within the shutdown deadline / speed "+game.Race.player.state.speed+" / shutdown "+game.Race.player.state.shutdownTime);
        }
        IEnumerator PowertrainRun()
        {
            var report = new System.Text.StringBuilder("Windows powertrain passes through real Update/DSP/OnGUI; injected hardware and isolated saves.\n");
            foreach (var hardware in new[] { TransmissionUpgrade.Manual, TransmissionUpgrade.DCT, TransmissionUpgrade.Automatic, TransmissionUpgrade.CVT })
            {
                game.ReturnToGarage();
                game.Profile.SelectedCar.tune = new Tune { engineLevel = 1, tireLevel = 2, gearboxLevel = 1, controlLevel = 1,
                    inductionUpgrade = EngineUpgradeInduction.Turbo, transmissionUpgrade = hardware,
                    twoStep = EquipmentSetting.On, launchPreload = EquipmentSetting.On, antiLag = EquipmentSetting.On };
                InstallFixtureHardware();
                game.settings.ApplyPreset(DrivingPreset.Automatic);
                int cash = game.Profile.cash, rep = game.Profile.reputation;
                // This standard gearbox fixture uses a clutch matched to its boosted
                // engine and flywheel. Stock capacity/slip is tested separately.
                var definition = Array.Find(game.vehicles, v => v.spec.id == game.Profile.selectedVehicleId);
                var factorySpec = definition.spec;
                var fixtureSpec = factorySpec.Copy();
                fixtureSpec.clutchCapacityNm = (float)FlywheelDynamics.MatchedClutchCapacity(fixtureSpec, game.Profile.SelectedCar.tune);
                try { definition.spec = fixtureSpec; game.StartPractice(); }
                finally { definition.spec = factorySpec; }
                report.AppendLine(hardware + " fixture clutch / " + (fixtureSpec.ClutchCapacity * 1.15).ToString("F1") + " Nm; factory asset restored immediately.");
                while (game.Race.time < 1.5) yield return null;
                var car = game.Race.player;
                Check(car.state.gear == 0 && car.state.brake == 1 && car.state.launchControlActive && car.state.boostBar > .03,
                    "Two-step builds boost while staged with zero wheel torque: " + hardware);
                double rpm = car.state.rpm, spool = car.state.turboSpool, heat = car.state.exhaustHeat, timer = car.state.antiLagTime;
                game.PauseRace(true); yield return new WaitForSecondsRealtime(.15f);
                Check(car.state.rpm == rpm && car.state.turboSpool == spool && car.state.exhaustHeat == heat && car.state.antiLagTime == timer && game.Audio.CurrentVolume == 0,
                    "Pause freezes all launch/thermal controls and silences audio: " + hardware);
                game.PauseRace(false); yield return Capture("powertrain-" + hardware.ToString().ToLowerInvariant() + "-staging");
                while (game.Race.time < 7) yield return null;
                var samples = new float[1024]; double energy = 0;
                // A cut can silence a single listener block; sample a bounded warmed interval.
                for (int sampleBlock = 0; sampleBlock < 4; sampleBlock++)
                {
                    yield return new WaitForSecondsRealtime(.06f); AudioListener.GetOutputData(samples, 0);
                    foreach (float value in samples) energy += value * value;
                }
                yield return Capture("powertrain-" + hardware.ToString().ToLowerInvariant() + "-running");
                Check(energy > .000001 && !car.state.launchControlActive && car.state.speed > 0, "Driving releases launch limiter and emits DSP: " + hardware +
                    " / energy " + energy + " / RPM " + car.state.rpm + " / speed " + car.state.speed + " / failed " + car.state.engineFailed + " / time " + game.Race.time);
                yield return AwaitFinish();
                Check(car.state.finished && !car.state.engineFailed && !car.state.stalled && game.Profile.cash == cash && game.Profile.reputation == rep,
                    "Hardware completes practice without engine failure or payout: " + hardware);
                Check(ProfileStore.Load().SelectedCar.lastPass.tune.transmissionUpgrade == hardware && game.Race.telemetry.Csv().Contains("engine_damage"), "New parts and diagnostics persist: " + hardware);
                if (hardware == TransmissionUpgrade.CVT) Check(car.state.gear == 1 && car.state.cvtRatio < car.transmission.cvtMaxRatio, "CVT varies ratio without gear edges.");
                File.WriteAllText(Path.Combine(output, "powertrain-" + hardware.ToString().ToLowerInvariant() + ".csv"), game.Race.telemetry.Csv());
                report.AppendLine(hardware + " / ET " + car.state.elapsed.ToString("F3") + " / staged boost checked / damage " + car.state.engineDamage.ToString("F4") + " / listener RMS " + Math.Sqrt(energy / (samples.Length * 4)).ToString("F4"));
            }
            game.ReturnToGarage();
            game.Profile.SelectedCar.tune = new Tune { controlLevel = 1, limiter = LimiterStyle.None };
            InstallFixtureHardware();
            game.settings.launch = LaunchMode.Manual; game.settings.shifting = ShiftMode.Sequential; game.settings.throttleAssist = false;
            game.StartPractice(); game.verificationInput = () => new DriverInput { throttle = 1 };
            float deadline = Time.realtimeSinceStartup + 12;
            while (!game.Race.player.state.engineFailed && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.Race.player.state.engineFailed && game.Race.player.state.engineTorqueNm == 0 && game.Audio.CurrentVolume == 0, "Unrestricted neutral overrev breaks the engine and stops combustion audio.");
            yield return Capture("powertrain-overrev-failure");
            game.ReturnToGarage();
            game.Profile.SelectedCar.tune = new Tune { wheelDiameter = 1.6f, wheelMass = 3, wheelWidth = 1.6f };
            game.settings.ApplyPreset(DrivingPreset.Manual); game.settings.clutchReleaseSeconds = .008f;
            game.StartPractice(); game.verificationInput = () => new DriverInput { launch = game.Race.time >= 3.12 && !game.Race.player.state.launched };
            deadline = Time.realtimeSinceStartup + 7;
            while (!game.Race.player.state.stalled && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.Race.player.state.stalled && game.Race.player.state.transmittedTorqueNm == 0, "Heavy wheels and a low-rev clutch dump stall the actual player.");
            yield return Capture("powertrain-heavy-wheel-stall");
            game.verificationInput = () => new DriverInput { clutch = 1, launch = true };
            yield return new WaitForSecondsRealtime(.15f);
            Check(!game.Race.player.state.stalled && game.Race.player.state.rpm > 700, "Clutch-in Enter restarts through the player input boundary.");
            game.ReturnToGarage();
            foreach (string id in new[] { "v8-muscle", "utility-pickup" })
            {
                var definition = Array.Find(game.vehicles, v => v.spec.id == id);
                if (game.Profile.cars.Find(v => v.vehicleId == id) == null) game.Profile.cars.Add(new OwnedCar { vehicleId = id, tune = definition.CreateStockTune() });
                game.Profile.selectedVehicleId = id; game.Profile.SelectedCar.tune.coldStart = true;
                game.settings.launch = LaunchMode.Manual; game.settings.shifting = ShiftMode.Sequential; game.settings.throttleAssist = false;
                game.StartPractice(); yield return new WaitForSecondsRealtime(.4f);
                Check(game.Race.player.engine.data.fueling == FuelSystem.Carburetor && game.Race.player.state.idleRoughness > .1 && game.Race.player.state.engineTemperature < .1,
                    "Cold older-car idle reaches simulation and audio: " + id);
                yield return Capture("powertrain-cold-" + id);
                game.ReturnToGarage();
            }
            game.Profile.selectedVehicleId = "v8-muscle";
            game.Profile.SelectedCar.tune = new Tune { controlLevel = 1, twoStep = EquipmentSetting.On,
                twoStepStyle = LimiterStyle.Hard, twoStepCut = CutMethod.Ignition, launchRpm = 3500 };
            InstallFixtureHardware();
            game.StartPractice(); game.verificationInput = () => new DriverInput();
            yield return new WaitForSecondsRealtime(.5f);
            var v8 = game.Race.player;
            var idleSamples = new float[1024]; AudioListener.GetOutputData(idleSamples, 0);
            double idleEnergy = 0; foreach (float value in idleSamples) idleEnergy += value * value;
            Check(!v8.state.launchControlActive && v8.state.rpm < 1200 && idleEnergy > .000001, "Warm V8 idle produces combustion audio before throttle.");
            game.verificationInput = () => new DriverInput { throttle = 1 };
            yield return new WaitForSecondsRealtime(3);
            double twoStepEnergy = 0;
            for (int block = 0; block < 4; block++)
            {
                yield return new WaitForSecondsRealtime(.06f); AudioListener.GetOutputData(idleSamples, 0);
                foreach (float value in idleSamples) twoStepEnergy += value * value;
            }
            Check(v8.state.gear == 0 && v8.state.brake == 1 && v8.state.launchControlActive && v8.state.rpm > 2800 && v8.state.rpm < 3650 && v8.state.exhaustPopCount > 10 && twoStepEnergy > .000001,
                "Held V8 neutral throttle activates the 3500 RPM ignition two-step with repeated pops and audible DSP.");
            yield return Capture("powertrain-v8-two-step-3500");
            report.AppendLine("V8 warm idle listener RMS " + Math.Sqrt(idleEnergy / idleSamples.Length).ToString("F4") +
                " / 3500 RPM two-step RMS " + Math.Sqrt(twoStepEnergy / (idleSamples.Length * 4)).ToString("F4") + " / physical pops " + v8.state.exhaustPopCount);
            game.ReturnToGarage();
            Check(errors == 0, "Powertrain player has no runtime/DSP/GUI errors.");
            report.AppendLine("PASS: four gearbox passes, two-step preload/pause, DSP, save/telemetry/no payout, unrestricted engine failure, heavy-wheel stall/restart, cold carb idle and warm V8 idle/3500 RPM two-step. Runtime errors: " + errors);
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), report.ToString());
        }
        IEnumerator InductionRun()
        {
            var report = new System.Text.StringBuilder("Windows player induction passes using real Update/DSP/OnGUI and isolated saves.\n");
            foreach (var kit in new[] { EngineUpgradeInduction.Turbo, EngineUpgradeInduction.TwinTurbo, EngineUpgradeInduction.Supercharger, EngineUpgradeInduction.TwinSupercharger, EngineUpgradeInduction.Twincharged })
            {
                game.ReturnToGarage();
                game.Profile.SelectedCar.tune = new Tune { engineLevel = 1, tireLevel = 2, gearboxLevel = 2, inductionUpgrade = kit, blowOffValve = BlowOffType.Chirp };
                InstallFixtureHardware();
                game.settings.ApplyPreset(DrivingPreset.Automatic);
                game.settings.throttleAssist = false;
                int cash = game.Profile.cash, rep = game.Profile.reputation;
                game.StartPractice();
                game.verificationInput = () => new DriverInput { throttle = game.Race.player.state.shiftRemaining > 0 ? 0 : 1 };
                while (game.Race.time < 6) yield return null;
                double pressure = game.Race.player.state.boostBar;
                game.PauseRace(true); yield return new WaitForSecondsRealtime(.15f);
                Check(game.Race.player.state.boostBar == pressure && game.Audio.CurrentVolume == 0, "Pause freezes compressor state and mutes DSP: " + kit);
                game.PauseRace(false);
                float[] samples = new float[1024]; AudioListener.GetOutputData(samples, 0);
                yield return new WaitForSecondsRealtime(.25f); AudioListener.GetOutputData(samples, 0);
                double energy = 0; foreach (float value in samples) energy += value * value;
                Check(energy > .000001 && game.Audio.CurrentVolume > 0, "Live DSP emits audible induction voice: " + kit);
                yield return Capture("induction-" + kit.ToString().ToLowerInvariant() + "-running");
                yield return AwaitFinish();
                var car = game.Race.player; var telemetry = game.Race.telemetry.samples;
                double peakBoost = 0, drive = 0;
                foreach (var sample in telemetry)
                {
                    peakBoost = Math.Max(peakBoost, sample.boostBar); drive = Math.Max(drive, sample.superchargerDriveTorqueNm);
                    Check(Math.Abs(sample.engineHorsepower - sample.engineTorqueNm * sample.rpm / 7127) < .000001,
                        "Telemetry engine torque/HP agree: " + kit);
                }
                Check(car.state.finished && peakBoost > .2 && car.state.boostReleaseCount == game.Audio.BlowOffCount,
                    "Actual physical boost/events reach the renderer and completed pass: " + kit);
                if (car.induction.turboCount > 0) Check(car.state.boostReleaseCount > 0, "Throttle lifts during real shifts vent a charged turbo: " + kit);
                else Check(car.state.boostReleaseCount == 0, "Blower-only pass has no turbo release: " + kit);
                if (car.induction.superchargerCount > 0) Check(drive > 5, "Actual drivetrain deducts blower work: " + kit);
                Check(game.Profile.cash == cash && game.Profile.reputation == rep && ProfileStore.Load().SelectedCar.lastPass.tune.inductionUpgrade == kit,
                    "Topology/save/practice settlement remain consistent: " + kit);
                File.WriteAllText(Path.Combine(output, "induction-" + kit.ToString().ToLowerInvariant() + ".csv"), game.Race.telemetry.Csv());
                report.AppendLine(kit + " / ET " + car.state.elapsed.ToString("F3") + " / peak boost " + peakBoost.ToString("F3") + " bar / peak drive " + drive.ToString("F1") + " Nm / releases " + car.state.boostReleaseCount + " / listener RMS " + Math.Sqrt(energy / samples.Length).ToString("F4"));
            }
            Check(errors == 0, "No DSP/gameplay/GUI errors during induction passes.");
            report.AppendLine("PASS: five topology passes, physical boost/torque/HP/drive/release telemetry, listener playback, frozen pause state, settings/save and no-payout settlement. Runtime errors: " + errors);
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), report.ToString());
        }
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator KeyboardRun()
        {
            using (var keyboard = new WindowsKeyboardProbe())
            {
                const ushort W = 0x57, Space = 0x20, E = 0x45, Q = 0x51, Shift = 0xA0;
                yield return new WaitForSecondsRealtime(.2f);
                game.settings.launch = LaunchMode.Assisted; game.settings.shifting = ShiftMode.Sequential; game.settings.throttleAssist = false;
                keyboard.Key(W, true);
                yield return new WaitForSecondsRealtime(.1f);
                game.StartRace(); // W was already down when the adapter cleared.
                yield return new WaitForSecondsRealtime(.6f);
                Check(game.Race.player.state.throttle == 1 && game.Race.player.state.rpm > game.Race.player.tune.launchRpm + 500 &&
                    !game.Race.player.state.launched && !game.Race.player.state.disqualified && game.Race.player.state.gear == 0 && game.Race.player.state.brake == 1,
                    "Held W revs immediately in neutral with staging brakes held.");
                yield return Capture("held-w-staging");
                keyboard.Key(Space, true); keyboard.Key(W, false);
                yield return new WaitForSecondsRealtime(.2f);
                Check(game.Race.player.state.throttle == 1 && !game.Race.player.state.disqualified, "Held Space revs without triggering launch/red light.");
                game.PauseRace(true); yield return new WaitForSecondsRealtime(.1f); game.PauseRace(false);
                yield return new WaitForSecondsRealtime(.2f);
                Check(game.Race.player.state.throttle == 1, "Gas held through resume is read without a new press.");
                while (game.Race.time < 3.1) yield return null;
                Check(!game.Race.player.state.launched && game.Race.player.state.gear == 0 && game.Race.player.state.stagingBrakesHeld,
                    "Space remains exclusively gas after green with neutral and brake hold maintained.");
                keyboard.Key(E, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(E, false);
                yield return new WaitForSecondsRealtime(.1f);
                Check(game.Race.player.state.launched && !game.Race.player.state.disqualified && game.Race.player.state.gear == 1 &&
                    !game.Race.player.state.stagingBrakesHeld && game.Race.player.state.brake == 0 && game.Race.player.state.throttle == 1,
                    "E engages first exactly once and releases staging brakes while Space stays held.");
                yield return AwaitSpeed(6);
                keyboard.Key(E, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(E, false);
                yield return new WaitForSecondsRealtime(.6f);
                Check(game.Race.player.state.gear == 2 && game.Race.player.state.throttle == 1 && game.Race.player.state.transmittedTorqueNm > 0,
                    "Space stays full throttle through a sequential shift and torque resumes.");
                yield return Capture("held-space-after-upshift");
                keyboard.Key(W, true); keyboard.Key(Space, false);
                keyboard.Key(Q, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(Q, false);
                yield return new WaitForSecondsRealtime(.6f);
                Check(game.Race.player.state.throttle == 1 && (game.Race.player.state.gear == 1 ||
                    game.Race.player.state.gear == 2 && game.Race.player.state.feedback == "UNSAFE DOWNSHIFT BLOCKED"),
                    "W stays held through a downshift or its safe overrev rejection.");
                game.StartRace();
                yield return new WaitForSecondsRealtime(.2f);
                keyboard.Key(E, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(E, false);
                Check(game.Race.player.state.disqualified && game.Race.player.state.gear == 0 && game.Race.player.state.stagingBrakesHeld,
                    "E before green red-lights while retaining neutral and staging brake hold.");
                game.StartRace();
                while (game.Race.time < 3.1) yield return null;
                float scale = RaceInputAdapter.Scale(Screen.width, Screen.height);
                Rect upRect = RaceInputAdapter.ControlRect(RaceInputAdapter.UpControl, Screen.width / scale, Screen.height / scale);
                keyboard.MouseButtonAt((int)(upRect.center.x * scale), (int)(upRect.center.y * scale), true);
                yield return new WaitForSecondsRealtime(.08f);
                keyboard.MouseButtonAt((int)(upRect.center.x * scale), (int)(upRect.center.y * scale), false);
                yield return new WaitForSecondsRealtime(.1f);
                Check(game.Race.player.state.launched && game.Race.player.state.gear == 1 && game.Race.player.state.throttle == 1 &&
                    !game.Race.player.state.stagingBrakesHeld, "Combined mouse shift-up launches first without cancelling held W.");
                yield return Capture("combined-shift-up-launch");
                game.settings.launch = LaunchMode.Manual; game.settings.shifting = ShiftMode.ManualClutch;
                keyboard.Key(Shift, true); game.StartRace();
                while (game.Race.time < 3.1) yield return null;
                keyboard.Key(E, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(E, false);
                yield return new WaitForSecondsRealtime(.1f);
                Check(game.Race.player.state.launched && game.Race.player.state.gear == 1 && game.Race.player.state.distance == 0 &&
                    game.Race.player.state.clutch > .98 && game.Race.player.state.throttle == 1,
                    "Full manual E engages first; held clutch retains drive disconnect and W stays active.");
                keyboard.Key(Shift, false); yield return new WaitForSecondsRealtime(.6f);
                Check(game.Race.player.state.distance > 0 && game.Race.player.state.throttle == 1,
                    "Releasing clutch after E launch drives using the original held gas.");
                game.settings.launch = LaunchMode.Automatic; game.settings.shifting = ShiftMode.ManualClutch;
                game.StartRace();
                yield return AwaitSpeed(8);
                keyboard.Key(Shift, true); yield return new WaitForSecondsRealtime(.08f);
                keyboard.Key(E, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(E, false);
                yield return new WaitForSecondsRealtime(.4f);
                Check(game.Race.player.state.gear == 2 && game.Race.player.state.throttle == 1 && game.Race.player.state.clutch > .98,
                    "W stays held while clutch and gear keys are used together.");
                keyboard.Key(Shift, false); yield return new WaitForSecondsRealtime(.4f);
                Check(game.Race.player.state.throttle == 1 && game.Race.player.state.transmittedTorqueNm > 0,
                    "Clutch release restores drive with the original gas hold.");
                yield return Capture("held-w-after-clutch-shift");
                keyboard.Key(W, false); yield return new WaitForSecondsRealtime(.2f);
                Check(game.Race.player.state.throttle == 0, "Releasing the last gas key stops throttle when automatic throttle is off.");
                game.ReturnToGarage(); game.settings.ApplyPreset(DrivingPreset.Automatic);
                int cashBeforePractice = game.Profile.cash;
                keyboard.Key(0x50, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(0x50, false);
                yield return new WaitForSecondsRealtime(.15f);
                Check(game.ScreenName == "Race" && game.IsPractice && game.Race.player.state.gear == 0, "Garage P shortcut enters neutral practice staging.");
                yield return AwaitFinish();
                Check(game.Profile.cash == cashBeforePractice && game.Profile.reputation == 0 && game.Profile.SelectedCar.lastPass != null,
                    "Keyboard-started practice persists a pass without paying cash/REP.");
                var wornClutch = game.Profile.parts.Find(p => p.ownerId == game.Profile.SelectedCar.instanceId && p.slot == "Clutch");
                var savedClutch = ProfileStore.Load().parts.Find(p => p.instanceId == wornClutch.instanceId);
                Check(wornClutch.condition.slipEnergyJ > 0 && wornClutch.condition.health < 1 && savedClutch.condition.health == wornClutch.condition.health && savedClutch.condition.slipEnergyJ == wornClutch.condition.slipEnergyJ,
                    "Actual completed practice commits calculated clutch wear/heat to owned parts and disk.");
                yield return Capture("keyboard-practice-result");
                keyboard.Key(0x52, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(0x52, false);
                yield return new WaitForSecondsRealtime(.15f);
                Check(game.IsPractice && game.Race.phase == RacePhase.Countdown, "Result R shortcut restages the same practice mode.");
                Check(game.Race.player.mechanics.parts.Find(p => p.instanceId == wornClutch.instanceId).condition.health == wornClutch.condition.health,
                    "Retry preserves actual worn clutch condition instead of renewing it.");
                keyboard.Key(0x1B, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(0x1B, false);
                yield return new WaitForSecondsRealtime(.1f);
                keyboard.Key(0x47, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(0x47, false);
                yield return new WaitForSecondsRealtime(.15f);
                Check(game.ScreenName == "Garage" && game.Profile.cash == cashBeforePractice, "Paused G shortcut abandons practice and returns to garage without payout.");
                Check(errors == 0, "Keyboard test has no runtime errors.");
                File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), "PASS: Windows OS keyboard/mouse events through Unity Input.GetKey/Poll/Update: held W at race entry, neutral/brake-held staging, Space staging without red light, held gas across pause/resume, E engaging first exactly once/releasing staging brakes, Space plus sequential upshift, W plus downshift attempt/overrev protection, early E red light, combined mouse shift-up launch with W held, full manual E launch/clutch hold/release, automatic launch plus W/clutch/upshift/release, throttle resumes after torque interruption, last gas key release.\nRuntime errors: " + errors + "\nNo driver-input injection used. Physical hardware rollover and human driving feel remain untested.\n");
            }
        }
        IEnumerator AwaitSpeed(double speed)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (game.Race.player.state.speed < speed && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.Race.player.state.speed >= speed, "Keyboard-driven car accelerates before deadline.");
        }
#endif
        IEnumerator AwaitFinish()
        {
            float deadline = Time.realtimeSinceStartup + 105;
            while (game.Race.phase != RacePhase.Finished && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.Race.phase == RacePhase.Finished, "Run terminates before timeout.");
            yield return null;
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            if (texture == null) throw new InvalidOperationException("Unity screenshot capture returned no frame.");
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
        void InstallFixtureHardware()
        {
            // Explicit isolated development fixtures now install real definitions instead of treating tune ranks as authority.
            if (!Active) throw new InvalidOperationException("Hardware fixture is development-only.");
            if(game.Race!=null)game.ReturnToGarage();
            Check(game.Race==null,"Fixture hardware changes require a closed race and committed resources.");
            var profile = game.Profile; var catalog = profile.catalog; var car = profile.SelectedCar;
            foreach (var slot in catalog.Recipe(car.vehicleId, car.tune))
            {
                var binding = car.installed.slots.Find(s => s.slot == slot.Key.ToString());
                if (binding != null && profile.parts.Find(p => p.instanceId == binding.partInstanceId).definitionId == slot.Value) continue;
                var item = new PartInstance { instanceId = MechanicsOwnership.NewId("fixture-part"), definitionId = slot.Value };
                profile.parts.Add(item); MechanicsOwnership.Replace(profile, car, item, catalog);
            }
        }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
