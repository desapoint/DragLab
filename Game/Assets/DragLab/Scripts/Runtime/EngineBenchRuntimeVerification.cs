using System;
using System.Collections;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine.SceneManagement;

namespace DragLab.Runtime
{
    // Opt-in probe: bench scene only, workspace evidence, never a player profile.
    public sealed partial class EngineBenchRuntimeVerification : MonoBehaviour
    {
        EngineTestScene scene;
        string output;
        int errors;
        readonly StringBuilder report = new StringBuilder("Windows standalone engine bench / real Update / DSP / OnGUI.\n");
        public static void AttachIfRequested(EngineTestScene scene)
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--draglab-bench-verify");
            if (!Debug.isDebugBuild || index < 0) return;
            bool audioOnly = Array.IndexOf(args, "--draglab-audio-settings") >= 0;
            if (audioOnly && FindFirstObjectByType<EngineBenchRuntimeVerification>() != null) return;
            var probe = (audioOnly ? new GameObject("Shared audio verification") : scene.gameObject).AddComponent<EngineBenchRuntimeVerification>(); probe.scene = scene;
            if (audioOnly) DontDestroyOnLoad(probe.gameObject);
            probe.output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(probe.output);
            Application.runInBackground = true;
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); report.AppendLine("PASS: " + text); }
        IEnumerator Start()
        {
            var routine = Run();
            while (true)
            {
                bool more;
                try { more = routine.MoveNext(); }
                catch (Exception e) { File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), report.ToString() + "FAIL: " + e); Debug.LogError(e); Application.Quit(1); yield break; }
                if (!more) break;
                yield return routine.Current;
            }
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), report.ToString() + "Runtime errors: " + errors + "\n");
            Application.Quit(0);
        }
        IEnumerator AudioSettingsRun()
        {
            Check(SharedAudioSettings.MasterVolume == .75f, "Both scenes start with one shared 75% default.");
            Check(SharedAudioSettings.FilePath == Path.Combine(output, "audio-settings.json"), "Audio preferences are isolated in the workspace fixture.");
            scene.ShowTestTab(8); scene.KeyboardControl(); SharedAudioSettings.SetMasterVolume(.62f);
            scene.config.controls.gear = 0;
            yield return new WaitForSecondsRealtime(.2f);
            Check(scene.Controls.Source == EngineBenchControlSource.Keyboard && SharedAudioSettings.MasterVolume == .62f && SharedAudioSettings.DspMasterVolume == .62f && AudioListener.volume == 1,
                "Audio settings can change while keyboard ownership remains active.");
            yield return new WaitForEndOfFrame(); yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("bench-audio-settings.png"));
            scene.UseSliders(); scene.SetSliderControls(new EngineBenchControls { fixedRpm = true, targetRpm = 4000, throttle = .8f, load = .6f });
            scene.config.volume = .03f;
            yield return new WaitForSecondsRealtime(.3f);
            Check(Math.Abs(scene.Audio.CurrentVolume - .62f) < .001, "Legacy bench audition volume does not add a second live master gain.");
            scene.CaptureSlot(true); Check(scene.SlotA.volume == .62f, "A/B captures the current reference level for deterministic offline exports.");
            SharedAudioSettings.SetMasterVolume(0);
            yield return new WaitForSecondsRealtime(.25f);
            var samples = new float[1024]; AudioListener.GetOutputData(samples, 0);
            double energy = 0; foreach (var sample in samples) energy += sample * sample;
            Check(scene.Audio.CurrentVolume == 0 && energy < .000000001 && scene.Bench.state.rpm == 4000, "Master zero mutes listener output while bench physics continues.");
            SharedAudioSettings.SetMasterVolume(.62f);
            scene.SelectVehicle(1); scene.RunScenario(scene.config, EngineBenchScenario.NeutralBlips);
            yield return new WaitForSecondsRealtime(.1f);
            Check(SharedAudioSettings.MasterVolume == .62f, "Changing cars and starting presets do not overwrite master volume.");
            scene.PlayMusic(0); scene.ShowTestTab(8);
            yield return new WaitForSecondsRealtime(.3f); SharedAudioSettings.SetMasterVolume(.48f);
            Check(scene.PlayingMusic && SharedAudioSettings.MasterVolume == .48f && scene.Audio.CurrentVolume <= .48f,
                "The concert uses the same master, and Settings edits do not interrupt playback.");
            scene.StopMusic(); SharedAudioSettings.SetMasterVolume(.62f);
            SceneManager.LoadScene("FirstSlice");
            yield return new WaitForSecondsRealtime(.4f);
            var game = FindFirstObjectByType<PrototypeGame>();
            Check(game != null && SharedAudioSettings.MasterVolume == .62f, "Garage inherits the bench's saved master across actual scene transitions.");
            Check(ProfileStore.FilePath == Path.Combine(output, "profile-v1.json"), "Cross-scene verification keeps career saves isolated too.");
            game.OpenAudioSettings(); yield return new WaitForEndOfFrame(); yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("garage-audio-settings.png"));
            game.settings.soundVolume = .02f; game.StartPractice();
            yield return new WaitForSecondsRealtime(.4f);
            Check(Math.Abs(game.Audio.CurrentVolume - .62f) < .001, "Race uses the same master without legacy driving-volume attenuation.");
            game.OpenAudioSettings();
            yield return new WaitForEndOfFrame(); yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("pause-audio-settings.png"));
            double raceTime = game.Race.time; SharedAudioSettings.SetMasterVolume(.44f);
            yield return new WaitForSecondsRealtime(.15f);
            Check(game.Race.time == raceTime && game.Audio.CurrentVolume == 0, "Race settings preserve pause and mute while changing the global level.");
            game.PauseRace(false); yield return new WaitForSecondsRealtime(.2f);
            Check(Math.Abs(game.Audio.CurrentVolume - .44f) < .001, "Race resume applies the newly selected shared volume.");
            SceneManager.LoadScene("EngineTest"); yield return new WaitForSecondsRealtime(.4f);
            scene = FindFirstObjectByType<EngineTestScene>();
            Check(scene != null && SharedAudioSettings.MasterVolume == .44f && Math.Abs(scene.Audio.CurrentVolume - .44f) < .001,
                "Returning to the bench keeps the race-selected master and runs only one probe.");
            SharedAudioSettings.Reload();
            Check(SharedAudioSettings.MasterVolume == .44f, "The shared preference reloads independently of the player profile.");
        }
        IEnumerator MusicRun()
        {
            scene.SelectVehicle(0); scene.ShowTestTab(0);
            scene.SetSliderControls(new EngineBenchControls { fixedRpm = true, targetRpm = 2200, throttle = .3f, load = .4f, gear = 2 });
            yield return new WaitForSecondsRealtime(.1f);
            string original = JsonUtility.ToJson(scene.config);
            for (int song = 0; song < 3; song++)
            {
                scene.PlayMusic(song);
                Check(scene.PlayingMusic && scene.Controls.Source == EngineBenchControlSource.Scenario && scene.Bench.engine.limiter == LimiterStyle.None,
                    "Concert owns the neutral fixed-RPM fixture: " + scene.MusicTitle);
                yield return new WaitForSecondsRealtime(.5f);
                var samples = new float[1024]; double energy = 0;
                for (int block = 0; block < 4; block++)
                { yield return new WaitForSecondsRealtime(.06f); AudioListener.GetOutputData(samples, 0); foreach (var value in samples) energy += value * value; }
                Check(energy > .000001 && scene.Bench.state.rpm >= scene.Bench.spec.idleRpm && scene.Bench.state.gear == 0,
                    "Engine DSP emits the musical notes with a running neutral engine: " + scene.MusicTitle);
                int tick = scene.MusicTick; scene.Pause(true);
                yield return new WaitForSecondsRealtime(.15f);
                Check(scene.MusicTick == tick && scene.Audio.CurrentVolume == 0, "Pause freezes the score and silences the concert.");
                scene.Pause(false);
                if (song == 0)
                { yield return new WaitForEndOfFrame(); yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("dyno-concert.png")); }
                float deadline = Time.realtimeSinceStartup + 30;
                while (scene.PlayingMusic && Time.realtimeSinceStartup < deadline) yield return null;
                Check(!scene.PlayingMusic && JsonUtility.ToJson(scene.config) == original && scene.Controls.Source == EngineBenchControlSource.Sliders,
                    "Completed theme restores all pre-concert specs/tune/slider settings.");
                var run = scene.Dyno.History[scene.Dyno.History.Count - 1];
                Check(run.Samples.Count > 1000 && run.label.Contains("Dyno concert"), "Dyno archives the entire musical performance.");
                File.WriteAllText(Path.Combine(output, "concert-" + song + ".csv"), run.Csv());
            }
            scene.PlayRandomMusic(); string first = scene.MusicTitle; scene.StopMusic(); scene.PlayRandomMusic();
            Check(scene.PlayingMusic && scene.MusicTitle != first, "Random button avoids immediately repeating the same theme.");
            scene.KeyboardControl();
            Check(!scene.PlayingMusic && scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.config.vehicle.sound.exhaustPopGain == scene.vehicles[0].spec.sound.exhaustPopGain,
                "Keyboard takeover stops the concert, restores acoustic hardware and owns driving inputs.");
            scene.PlayMusic(0); scene.ResetBench();
            Check(!scene.PlayingMusic && scene.Bench.state.rpm == scene.Bench.spec.idleRpm && scene.config.controls.throttle == 0,
                "Reset cancels music and returns the restored engine to neutral idle.");
        }
        IEnumerator Run()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-tire-feedback-only")>=0) {
                scene.SelectVehicle(0);scene.UseSliders();
                scene.SetSliderControls(new EngineBenchControls{fixedRpm=true,targetRpm=3200,throttle=1,load=.3f,gear=1,tireSlip=.8f,roadSpeedKmh=0,launchControls=false});
                SharedAudioSettings.SetMasterVolume(1);yield return new WaitForSecondsRealtime(.5f);
                Check(scene.CarView.SlipParticleCount>0&&scene.Audio.TireOutputRms>.0001,"Authored acoustic slip produces smoke and audible tires at zero road speed");
                float full=scene.Audio.TireOutputRms;SharedAudioSettings.SetMasterVolume(.25f);yield return new WaitForSecondsRealtime(.3f);
                Check(scene.Audio.TireOutputRms<full*.4f&&scene.Audio.TireOutputRms>full*.18f,"Acoustic tire voice follows the shared master volume");
                SharedAudioSettings.SetMasterVolume(0);yield return new WaitForSecondsRealtime(.25f);
                Check(scene.Audio.TireOutputRms<1e-6,"Shared mute silences the new tire source");SharedAudioSettings.SetMasterVolume(.75f);
                scene.Pause(true);int count=scene.CarView.SlipParticleCount;yield return new WaitForSecondsRealtime(.2f);
                Check(scene.CarView.SlipParticleCount==count&&scene.Audio.TireOutputRms==0,"Acoustic pause freezes smoke and silences tires");
                scene.ResetBench();yield return null;
                Check(scene.CarView.SlipParticleCount==0,"Acoustic reset clears stale particles");
                Check(errors==0,"Acoustic tire preview has zero runtime/GUI/DSP errors");yield break;
            }
            yield return new WaitForSecondsRealtime(.2f);
            Check(scene.Bench != null && scene.Audio.Ready && FindFirstObjectByType<PrototypeGame>() == null, "Bench loads directly without race/profile initialization.");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-pressure-audio-only") >= 0)
            { yield return Guarded(PressureAudioRun()); Check(errors == 0, "Pressure playback has no runtime/DSP/GUI errors."); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-bench-config") >= 0)
            { yield return Guarded(ConfigWhileDrivingRun()); Check(errors == 0, "Live configuration edits have no runtime/DSP/GUI errors."); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-audio-levels") >= 0)
            { yield return Guarded(AudioLevelsRun()); Check(errors == 0, "Audio level regression has no runtime/DSP/GUI errors."); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-bench-shift-hold") >= 0)
            { yield return Guarded(ShiftHoldRun()); Check(errors == 0, "Held-gas shift regression has no runtime/DSP/GUI errors."); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-audio-settings") >= 0)
            { yield return AudioSettingsRun(); Check(errors == 0, "Shared audio settings have no runtime/DSP/GUI errors."); yield break; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-bench-music") >= 0)
            { yield return MusicRun(); Check(errors == 0, "No concert DSP/GUI/runtime errors."); yield break; }
            foreach (var vehicle in scene.vehicles)
            {
                int index = Array.IndexOf(scene.vehicles, vehicle); scene.SelectVehicle(index);
                scene.config.controls = new EngineBenchControls { fixedRpm = true, targetRpm = 4000, throttle = .8f, load = .75f, gear = 1 };
                yield return new WaitForSecondsRealtime(.6f);
                var pcm = new float[1024]; double energy = 0;
                for (int block = 0; block < 4; block++)
                {
                    yield return new WaitForSecondsRealtime(.06f); AudioListener.GetOutputData(pcm, 0);
                    foreach (float value in pcm) energy += value * value;
                }
                Check(scene.Bench.state.rpm == 4000 && scene.Bench.state.engineTorqueNm > 0 && energy > .000001,
                    "Fixed RPM/load produces real listener output: " + vehicle.spec.id + " / RMS " + Math.Sqrt(energy / (pcm.Length * 4)).ToString("F4") +
                    " / RPM " + scene.Bench.state.rpm + " / torque " + scene.Bench.state.engineTorqueNm + " / volume " + scene.Audio.CurrentVolume + " / paused " + scene.Paused);
            }
            scene.SelectVehicle(0);
            foreach (var kit in new[] { EngineUpgradeInduction.Turbo, EngineUpgradeInduction.TwinTurbo, EngineUpgradeInduction.Supercharger, EngineUpgradeInduction.TwinSupercharger, EngineUpgradeInduction.Twincharged })
            {
                scene.config.tune.engineLevel = 1; scene.config.tune.inductionUpgrade = kit;
                scene.config.controls = new EngineBenchControls { fixedRpm = true, targetRpm = 5000, throttle = 1, load = 1, gear = 1 };
                scene.ApplyConfig(true); scene.config.controls.gear = 1;
                yield return new WaitForSecondsRealtime(1);
                Check(scene.Bench.state.boostBar > .03 && scene.Bench.state.engineTorqueNm > 0, "All compressor topologies run independently of a race: " + kit);
            }
            scene.CaptureSlot(true);
            scene.config.vehicle.sound.exhaustSystem = ExhaustSetup.Preset(3); scene.config.vehicle.sound.cylinders = 8;
            scene.CaptureSlot(false);
            scene.RunScenario(scene.SlotA, EngineBenchScenario.NeutralBlips);
            Check(scene.ScenarioTick == 0 && scene.Bench.state.rpm == scene.Bench.spec.idleRpm && scene.Bench.state.turboSpool == 0, "Scenario starts from fresh state and audio hardware.");
            yield return new WaitForSecondsRealtime(3.2f);
            Check(scene.Bench.state.throttle == 1 && scene.Bench.state.gear == 0 && scene.RunningScenario, "Timed neutral-blip inputs run through actual Update.");
            Check(scene.Controls.Source == EngineBenchControlSource.Scenario && scene.Controls.Effective.throttle == 1 && scene.Controls.Effective.gear == 0,
                "Preset controls publish the live values displayed by Drive sliders.");
            scene.Pause(true); int tick = scene.ScenarioTick; double rpm = scene.Bench.state.rpm;
            int recordedTick = scene.Dyno.Tick;
            yield return new WaitForSecondsRealtime(.15f);
            Check(scene.ScenarioTick == tick && scene.Bench.state.rpm == rpm && scene.Audio.CurrentVolume == 0, "Pause freezes scenario ticks and mutes DSP.");
            Check(scene.Dyno.Tick == recordedTick, "Paused wall time does not enter the dyno trace.");
            scene.RepeatScenario();
            Check(scene.ScenarioTick == 0 && scene.Bench.state.turboSpool == 0 && scene.Bench.spec.sound.cylinders == scene.SlotA.vehicle.sound.cylinders, "Repeat restores the captured configuration and fresh physics.");
            scene.RunScenario(scene.SlotB, EngineBenchScenario.NeutralBlips);
            Check(scene.Bench.spec.sound.cylinders == 8 && scene.Bench.spec.sound.exhaustSystem.parts.Length == 0, "A/B scene switching applies independent engine and exhaust snapshots.");
            scene.Manual(); scene.config.controls = new EngineBenchControls { fixedRpm = true, targetRpm = 3500, throttle = 1, load = .6f, roadSpeedKmh = 40, tireSlip = 1, gear = 1 };
            yield return new WaitForSecondsRealtime(.3f);
            yield return new WaitForEndOfFrame(); yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("bench-manual.png"));
            yield return new WaitForSecondsRealtime(.2f);
            scene.config.vehicle.sound.exhaustSystem = ExhaustSetup.Preset(0); scene.ApplyConfig();
            Check(scene.Bench.state.rpm == 3500 && scene.Bench.spec.sound.exhaustSystem.parts.Length == 3, "Live exhaust edits retain current operating RPM.");
            foreach (int tab in new[] { 1, 2, 3, 4, 5, 6, 7 })
            {
                scene.ShowTestTab(tab); yield return null; yield return new WaitForEndOfFrame();
                yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("bench-tab-" + tab + ".png"));
                yield return new WaitForSecondsRealtime(.08f);
            }
            scene.ShowTestTab(0);
            scene.config.controls.engineEnabled = false;
            yield return new WaitForSecondsRealtime(.15f);
            Check(scene.Bench.state.rpm == 0 && scene.Audio.CurrentVolume == 0, "Engine off silences live sound.");
            string json = JsonUtility.ToJson(scene.config, true); File.WriteAllText(Path.Combine(output, "bench-config.json"), json);
            Check(JsonUtility.FromJson<EngineBenchConfig>(json).Validate() == null, "Complete bench configuration serializes separately from saves.");
            scene.SelectVehicle(0); scene.Dyno.Finish("verification history reset"); scene.Dyno.ClearHistory();
            scene.config.vehicle.transmission.type = TransmissionType.Manual; scene.ApplyConfig();
            scene.Dyno.Finish("verification history reset"); scene.Dyno.ClearHistory();
            EngineDynoRun firstRun = null;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) scene.RunScenario(scene.config, EngineBenchScenario.LoadedPullAndShifts); else scene.RepeatScenario();
                float deadline = Time.realtimeSinceStartup + 20;
                while (scene.RunningScenario && Time.realtimeSinceStartup < deadline) yield return null;
                Check(scene.Paused && !scene.RunningScenario && scene.Dyno.Active == null, "Complete scenario archives the dyno and pauses at its endpoint / pass " + pass);
                var run = scene.Dyno.History[scene.Dyno.History.Count - 1];
                Check(run.Samples.Count == 1441 && run.Samples[1440].seconds == 12 && run.Samples[1440].throttle == 0 && run.Samples[1440].horsepower == 0,
                    "Live dyno retains the full 12-second trace and throttle-off tail / pass " + pass);
                bool cut = false;
                foreach (var point in run.Samples) if (point.shiftCut && point.throttle == 1 && point.horsepower > 0 && point.shaftTorqueNm == 0) cut = true;
                Check(cut, "Live dyno records shaft interruptions while throttle and engine power remain active / pass " + pass);
                if (pass == 0) firstRun = run;
                else Check(firstRun.Csv() == run.Csv() && scene.Dyno.History.Count == 2, "Repeat overlays an independent, identical full run through the real Update loop.");
                File.WriteAllText(Path.Combine(output, "dyno-pass-" + pass + ".csv"), run.Csv());
            }
            scene.ShowTestTab(0); yield return null; yield return new WaitForEndOfFrame();
            yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("bench-dyno-overlays.png"));
            yield return new WaitForSecondsRealtime(.15f);
            string dynoFolder = Path.Combine(output, "dyno-exports"); scene.ExportDynoTo(dynoFolder);
            Check(File.ReadAllText(Path.Combine(dynoFolder, "dyno-" + firstRun.id + ".csv")) == firstRun.Csv() &&
                JsonUtility.FromJson<EngineBenchConfig>(File.ReadAllText(Path.Combine(dynoFolder, "dyno-" + firstRun.id + ".json"))).Validate() == null,
                "Dyno export writes complete CSV and an independent valid configuration into the fixture folder.");
            var fixedPreset = scene.config.Copy(); fixedPreset.controls = new EngineBenchControls { fixedRpm = true, targetRpm = 4100, throttle = .45f, load = .5f, gear = 1 };
            scene.RunScenario(fixedPreset, EngineBenchScenario.FixedRpmHold);
            yield return new WaitForSecondsRealtime(.1f);
            Check(scene.Controls.Effective.targetRpm == 4100 && scene.Controls.Effective.throttle == .45f && scene.config.controls.targetRpm == 4100 && scene.config.controls.throttle == .45f,
                "Fixed-RPM preset displays its applied controls and retains them for matching config/WAV exports.");
            scene.Dyno.Finish("verification history reset"); scene.Dyno.ClearHistory();
            var flywheelConfig = scene.config.Copy();
            flywheelConfig.vehicle.transmission.type = TransmissionType.Manual;
            flywheelConfig.tune.transmissionUpgrade = TransmissionUpgrade.Factory;
            EngineDynoRun coastLight = null;
            for (int pass = 0; pass < 2; pass++)
            {
                flywheelConfig.tune.flywheelMass = pass == 0 ? .5f : 2;
                scene.RunScenario(flywheelConfig, EngineBenchScenario.FlywheelCoastDown, pass == 0 ? "Light flywheel coast" : "Heavy flywheel coast");
                float deadline = Time.realtimeSinceStartup + 20;
                while (scene.RunningScenario && Time.realtimeSinceStartup < deadline) yield return null;
                Check(scene.Paused && !scene.RunningScenario, "Live neutral coast reaches its complete endpoint / pass " + pass);
                var run = scene.Dyno.History[scene.Dyno.History.Count - 1];
                Check(run.Samples.Count == 1441 && run.Samples[240].rpm == 5000 && run.Samples[360].rpm < 5000 && run.Samples[360].rpm > scene.Bench.spec.idleRpm,
                    "Neutral starts at the same RPM and physically decays after fixture release / pass " + pass);
                Check(run.Samples[360].horsepower == 0 && run.Samples[360].flywheelEnergyJ > 0,
                    "The live engine keeps rotating with stored energy at zero combustion HP / pass " + pass);
                if (pass == 0) coastLight = run;
                else Check(run.Samples[360].rpm > coastLight.Samples[360].rpm + 300 && run.config.tune.flywheelMass == 2 && coastLight.config.tune.flywheelMass == .5f,
                    "Live heavy-flywheel decay is slower than the independently captured light setup.");
                File.WriteAllText(Path.Combine(output, pass == 0 ? "flywheel-coast-light.csv" : "flywheel-coast-heavy.csv"), run.Csv());
            }
            yield return new WaitForEndOfFrame(); yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("bench-flywheel-coast-overlays.png"));
            yield return new WaitForSecondsRealtime(.15f);
            scene.Dyno.Finish("verification history reset"); scene.Dyno.ClearHistory(); EngineDynoRun dynamicFirst = null;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) scene.RunScenario(flywheelConfig, EngineBenchScenario.DynamicPullAndShifts); else scene.RepeatScenario();
                float deadline = Time.realtimeSinceStartup + 20;
                while (scene.RunningScenario && Time.realtimeSinceStartup < deadline) yield return null;
                Check(scene.Paused && !scene.RunningScenario, "Dynamic pull completes through live Update / pass " + pass);
                var run = scene.Dyno.History[scene.Dyno.History.Count - 1];
                bool disconnectedAndRotating = false, allSamplesRunning = true;
                foreach (var point in run.Samples)
                {
                    allSamplesRunning &= point.rpm >= scene.Bench.spec.idleRpm;
                    if (point.shiftCut && point.shaftTorqueNm == 0 && point.rpm > scene.Bench.spec.idleRpm && point.flywheelEnergyJ > 0) disconnectedAndRotating = true;
                }
                Check(allSamplesRunning, "Every dynamic sample preserves running RPM / pass " + pass);
                Check(disconnectedAndRotating && run.Samples[1440].rpm < run.Samples[1080].rpm, "Dynamic shift keeps the crank turning; throttle-off tail loses RPM.");
                if (pass == 0) dynamicFirst = run;
                else Check(dynamicFirst.Csv() == run.Csv(), "Dynamic shifts, flywheel energy and release replay exactly in the player.");
                File.WriteAllText(Path.Combine(output, "flywheel-dynamic-pass-" + pass + ".csv"), run.Csv());
            }
            yield return new WaitForEndOfFrame(); yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("bench-flywheel-dynamic-overlays.png"));
            yield return new WaitForSecondsRealtime(.15f);
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-bench-keyboard") >= 0)
            {
                scene.SelectVehicle(0); scene.config.tune.transmissionUpgrade = TransmissionUpgrade.Manual; scene.ApplyConfig(); scene.verificationKeyboard = true;
                using (var keyboard = new WindowsKeyboardProbe())
                {
                    scene.SetSliderControls(new EngineBenchControls { fixedRpm = true, targetRpm = 2500, throttle = .6f });
                    yield return new WaitForSecondsRealtime(.15f);
                    Check(scene.Controls.Source == EngineBenchControlSource.Sliders && scene.Bench.state.rpm == 2500 && scene.Controls.Effective.throttle == .6f,
                        "Assigned slider RPM and throttle remain latched without keyboard input.");
                    keyboard.Key(0x57, true); yield return new WaitForSecondsRealtime(.4f);
                    Check(scene.Bench.state.throttle == 1 && scene.Bench.state.gear == 0 && scene.Bench.state.rpm > 1200, "OS W revs neutral without a race.");
                    Check(scene.Controls.Source == EngineBenchControlSource.Keyboard && !scene.Controls.Effective.fixedRpm && scene.Controls.Effective.throttle == 1,
                        "OS pedal takeover disables the slider RPM pin and updates feedback to held gas.");
                    keyboard.Key(0x45, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(0x45, false); yield return new WaitForSecondsRealtime(.15f);
                    Check(scene.Bench.state.gear == 1 && scene.Bench.state.throttle == 1, "OS shift preserves held throttle.");
                    keyboard.Key(0x45, true); yield return new WaitForSecondsRealtime(.04f); keyboard.Key(0x45, false);
                    Check(scene.Bench.state.gear == 2 && scene.Bench.state.throttle == 1 && scene.Bench.state.engineHorsepower > 0 &&
                        scene.Bench.state.transmittedTorqueNm == 0 && scene.Bench.state.rpm >= scene.Bench.spec.idleRpm,
                        "OS upshift preserves running engine power and W while the manual gearbox disconnects its shaft.");
                    Check(scene.Controls.Effective.gear == 2 && scene.Controls.Effective.throttle == 1, "Shift and gas feedback update together.");
                    keyboard.Key(0xA0, true); yield return new WaitForSecondsRealtime(.1f);
                    Check(scene.Bench.state.clutch == 1 && scene.Bench.state.throttle == 1, "OS clutch and gas operate independently.");
                    Check(scene.Controls.Effective.clutch == 1 && scene.Controls.Effective.throttle == 1, "Clutch and gas sliders display concurrent keyboard controls.");
                    keyboard.Key(0xA0, false); keyboard.Key(0x20, true); keyboard.Key(0x57, false); yield return new WaitForSecondsRealtime(.1f);
                    Check(scene.Bench.state.throttle == 1, "Overlapping W/Space release preserves gas.");
                    keyboard.Key(0x20, false); yield return new WaitForSecondsRealtime(.1f);
                    Check(scene.Bench.state.throttle == 0, "Last gas-key release stops throttle.");
                    Check(scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Controls.Effective.throttle == 0,
                        "Released keyboard gas stays zero instead of restoring the old 60% slider setting.");
                    keyboard.Key(0x46, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(0x46, false); yield return new WaitForSecondsRealtime(.15f);
                    Check(scene.Bench.state.rpm == scene.config.controls.targetRpm, "OS F enables a static RPM target.");
                    keyboard.Key(0x4E, true); yield return new WaitForSecondsRealtime(.08f); keyboard.Key(0x4E, false); yield return new WaitForSecondsRealtime(.1f);
                    Check(scene.Bench.state.gear == 0, "OS N returns to neutral.");
                    scene.SetSliderControls(new EngineBenchControls { fixedRpm = true, targetRpm = 3800, throttle = .35f, load = .45f, gear = 1 });
                    yield return new WaitForSecondsRealtime(.2f);
                    Check(scene.Controls.Source == EngineBenchControlSource.Sliders && scene.Controls.Effective.throttle == .35f && scene.Bench.state.rpm == 3800,
                        "Slider edit takes back control and holds the assigned operating point.");
                    yield return new WaitForEndOfFrame(); yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("bench-dyno-sliders.png"));
                    keyboard.Key(0x57, true); yield return new WaitForSecondsRealtime(.1f); keyboard.Key(0x57, false); yield return new WaitForSecondsRealtime(.1f);
                    Check(scene.Controls.Source == EngineBenchControlSource.Keyboard && !scene.Controls.Effective.fixedRpm && scene.Controls.Effective.throttle == 0,
                        "Subsequent keyboard takeover resumes dynamic engine behavior and normal pedal release.");
                    scene.ResetBench(); yield return new WaitForSecondsRealtime(.1f);
                    Check(scene.Controls.Source == EngineBenchControlSource.Sliders && !scene.Controls.Effective.fixedRpm && scene.Controls.Effective.gear == 0 && scene.Controls.Effective.load == 0,
                        "Reset clears control overrides and returns to neutral idle.");
                    scene.SetSliderControls(new EngineBenchControls { gear = 1, load = .375f, targetRpm = 3001.3f });
                    scene.verificationFocusField = "controls.load";
                    yield return new WaitForSecondsRealtime(.15f);
                    Check(scene.IsEditingText, "Regression starts with a numeric field focused, as after manually editing controls.");
                    keyboard.Key(0x57, true); yield return new WaitForSecondsRealtime(.15f);
                    bool steady = true; float until = Time.realtimeSinceStartup + 1.5f;
                    var trace = new StringBuilder("frame,source,editing,rpm,gear,gas\n");
                    while (Time.realtimeSinceStartup < until)
                    {
                        var state = scene.Bench.state;
                        trace.AppendLine(Time.frameCount + "," + scene.Controls.Source + "," + scene.IsEditingText + "," + state.rpm.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + state.gear + "," + state.throttle);
                        steady &= scene.Controls.Source == EngineBenchControlSource.Keyboard && state.throttle == 1;
                        yield return null;
                    }
                    File.WriteAllText(Path.Combine(output, "held-W-focus-regression.csv"), trace.ToString());
                    Check(steady && scene.Bench.state.rpm > 1300 && scene.Bench.state.gear == 1,
                        "Sustained W takes control from numeric focus, stays in first gear with full gas, raises RPM and never flickers back to sliders.");
                    yield return new WaitForEndOfFrame(); yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("bench-keyboard-readonly.png"));
                    keyboard.Key(0x57, false);
                    yield return new WaitForSecondsRealtime(.1f);
                    Check(scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Bench.state.throttle == 0,
                        "Focused-field takeover still releases throttle normally.");
                    scene.UseSliders();
                    Check(scene.Controls.Source == EngineBenchControlSource.Sliders, "Use sliders explicitly hands control back to the UI.");
                    scene.SetSliderControls(new EngineBenchControls { gear = 1, load = .375f }); scene.verificationFocusField = "controls.load";
                    yield return new WaitForSecondsRealtime(.1f);
                    keyboard.Key(0x20, true); yield return new WaitForSecondsRealtime(.3f);
                    Check(scene.Controls.Source == EngineBenchControlSource.Keyboard && scene.Bench.state.throttle == 1 && scene.Bench.state.gear == 1,
                        "Space also claims numeric focus without activating a focused GUI button or resetting the bench.");
                    keyboard.Key(0x20, false); yield return new WaitForSecondsRealtime(.1f);
                    Check(scene.Bench.state.throttle == 0, "Focused-field Space release clears held gas.");
                    scene.UseSliders();
                    scene.SetSliderControls(new EngineBenchControls { fixedRpm = true, targetRpm = 0, throttle = 0, gear = 1 });
                    yield return new WaitForSecondsRealtime(.15f);
                    Check(scene.Bench.state.rpm == scene.Bench.spec.idleRpm && scene.Audio.CurrentVolume > 0,
                        "Enabled engine with zero requested RPM still idles audibly.");
                }
            }
#endif
            Check(errors == 0, "Standalone bench has no runtime/DSP/GUI errors.");
        }
        IEnumerator ShiftHoldRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            scene.verificationKeyboard = true; scene.ShowTestTab(0);
            using (var keyboard = new WindowsKeyboardProbe())
            {
                for (int pass = 0; pass < 8; pass++)
                {
                    scene.SelectVehicle(pass % scene.vehicles.Length);
                    var type = (TransmissionType)(pass % 4);
                    scene.config.vehicle.transmission.type = type;
                    scene.config.tune.transmissionUpgrade = TransmissionUpgrade.Factory;
                    scene.ApplyConfig();
                    Check(scene.Bench.transmission.type == type, "Fixture runs the requested " + type + " hardware / pass " + pass);
                    scene.SetSliderControls(new EngineBenchControls { gear = 1, load = .55f });
                    yield return new WaitForSecondsRealtime(.15f);
                    ushort gasKey = pass < 4 ? (ushort)0x57 : (ushort)0x20;
                    keyboard.Key(gasKey, true); yield return new WaitForSecondsRealtime(.4f);
                    int firstHeldSample = scene.Dyno.Active.Samples.Count;
                    var trace = new StringBuilder("frame,source,editing,rpm,gear,gas,clutch,shiftSeconds,shaftNm\n");
                    bool held = true;
                    for (int action = 0; action < 4; action++)
                    {
                        bool mouse = action >= 2, up = action % 2 == 0;
                        var rect = up ? scene.verificationShiftUpRect : scene.verificationShiftDownRect;
                        ushort shiftKey = up ? (ushort)0x45 : (ushort)0x51;
                        if (mouse) keyboard.MouseButtonAt((int)rect.center.x, (int)rect.center.y, true);
                        else keyboard.Key(shiftKey, true);
                        yield return new WaitForSecondsRealtime(.06f);
                        if (mouse) keyboard.MouseButtonAt((int)rect.center.x, (int)rect.center.y, false);
                        else keyboard.Key(shiftKey, false);
                        float until = Time.realtimeSinceStartup + .65f, repeatAt = Time.realtimeSinceStartup + .15f;
                        while (Time.realtimeSinceStartup < until)
                        {
                            var state = scene.Bench.state;
                            trace.AppendLine(Time.frameCount + "," + scene.Controls.Source + "," + scene.IsEditingText + "," + state.rpm.ToString(CultureInfo.InvariantCulture) + "," + state.gear + "," + state.throttle.ToString(CultureInfo.InvariantCulture) + "," + state.clutch.ToString(CultureInfo.InvariantCulture) + "," + state.shiftRemaining.ToString(CultureInfo.InvariantCulture) + "," + state.transmittedTorqueNm.ToString(CultureInfo.InvariantCulture));
                            held &= scene.Controls.Source == EngineBenchControlSource.Keyboard && state.throttle == 1;
                            // Real keyboards send repeated downs while held; no gas up until the end.
                            if (action == 3 && Time.realtimeSinceStartup >= repeatAt) { keyboard.Key(gasKey, true); repeatAt += .05f; }
                            yield return null;
                        }
                        File.WriteAllText(Path.Combine(output, "held-gas-shifts-" + pass + ".csv"), trace.ToString());
                        Check(scene.Bench.state.gear == (type == TransmissionType.CVT ? (up ? 1 : 0) : up ? 2 : 1),
                            "Actual " + (mouse ? "mouse" : "keyboard") + " " + (up ? "upshift" : "downshift") + " selects expected gear / " + type + " / pass " + pass + " / actual " + scene.Bench.state.gear);
                    }
                    File.WriteAllText(Path.Combine(output, "held-gas-shifts-" + pass + ".csv"), trace.ToString());
                    Check(held, "Continuous " + (gasKey == 0x57 ? "W" : "Space") + " through E/Q, mouse shifts and repeated key-downs / " + scene.Bench.spec.id + " / " + type);
                    var samples = scene.Dyno.Active.Samples; bool everyTickHeld = true;
                    for (int i = firstHeldSample; i < samples.Count; i++) everyTickHeld &= samples[i].throttle == 1;
                    Check(everyTickHeld, "Every 120 Hz sample retains held throttle, including shift key-down/up and mouse-down/up / pass " + pass);
                    keyboard.Key(gasKey, false); yield return new WaitForSecondsRealtime(.12f);
                    Check(scene.Bench.state.throttle == 0 && scene.Controls.Source == EngineBenchControlSource.Keyboard, "Only gas release closes the pedal / pass " + pass);
                }
                scene.SelectVehicle(0); scene.SetSliderControls(new EngineBenchControls { gear = 1, throttle = .65f, load = .55f });
                yield return new WaitForSecondsRealtime(.15f);
                for (int action = 0; action < 2; action++)
                {
                    var rect = action == 0 ? scene.verificationShiftUpRect : scene.verificationShiftDownRect;
                    keyboard.MouseButtonAt((int)rect.center.x, (int)rect.center.y, true); yield return new WaitForSecondsRealtime(.06f);
                    keyboard.MouseButtonAt((int)rect.center.x, (int)rect.center.y, false); yield return new WaitForSecondsRealtime(.15f);
                    Check(scene.Controls.Source == EngineBenchControlSource.Sliders && Math.Abs(scene.Bench.state.throttle - .65f) < .00001 && scene.Bench.state.gear == (action == 0 ? 2 : 1),
                        "Remote-friendly slider throttle stays assigned through actual mouse " + (action == 0 ? "upshift" : "downshift") + ".");
                }
            }
#else
            Check(false, "Held-gas OS test requires a Windows development player.");
#endif
            yield break;
        }
        IEnumerator Guarded(IEnumerator routine)
        {
            while (true)
            {
                bool more;
                try { more = routine.MoveNext(); }
                catch (Exception e)
                { report.AppendLine("FAIL: " + e); Debug.LogError(e); yield break; }
                if (!more) yield break;
                yield return routine.Current;
            }
        }
        IEnumerator ReadListenerRms(Action<double> result)
        {
            var pcm = new float[8192]; double energy = 0;
            for (int block = 0; block < 16; block++)
            {
                yield return new WaitForSecondsRealtime(.04f); AudioListener.GetOutputData(pcm, 0);
                foreach (float sample in pcm) energy += sample * sample;
            }
            result(Math.Sqrt(energy / (pcm.Length * 16)));
        }
        // Audio-path regression independent of the historical bench Settings/Mix layout.
        // Uses the existing isolated --draglab-bench-verify preference/profile fixture.
        IEnumerator PressureAudioRun()
        {
            Check(SharedAudioSettings.FilePath == Path.Combine(output, "audio-settings.json"), "Playback preferences remain isolated from player settings.");
            foreach (string id in new[] { "starter-hatch", "rally-sedan", "v8-muscle" })
            {
                int index = Array.FindIndex(scene.vehicles, car => car.spec.id == id);
                Check(index >= 0, "Playback fixture exists: " + id);
                scene.SelectVehicle(index); scene.UseSliders();
                scene.SetSliderControls(new EngineBenchControls { fixedRpm = true, targetRpm = 4000, throttle = .8f, load = .6f });
                SharedAudioSettings.SetMasterVolume(.75f);
                yield return new WaitForSecondsRealtime(2);
                double loud = 0, quiet = 0, muted = 0;
                yield return ReadListenerRms(rms => loud = rms);
                SharedAudioSettings.SetMasterVolume(.25f); yield return new WaitForSecondsRealtime(.4f);
                yield return ReadListenerRms(rms => quiet = rms);
                Check(loud > .00001 && quiet / loud > .27 && quiet / loud < .40, "Listener PCM responds to master gain: " + id);
                SharedAudioSettings.SetMasterVolume(0); yield return new WaitForSecondsRealtime(.3f);
                yield return ReadListenerRms(rms => muted = rms);
                Check(muted < 1e-7 && scene.Bench.state.rpm == 4000, "Mute silences playback while simulation continues: " + id);
                SharedAudioSettings.SetMasterVolume(.75f); scene.Pause(true);
                yield return new WaitForSecondsRealtime(.3f);
                yield return ReadListenerRms(rms => muted = rms);
                Check(muted < 1e-7 && scene.Audio.CurrentVolume == 0, "Pause silences the live pressure voice: " + id);
                scene.Pause(false); yield return new WaitForSecondsRealtime(.3f);
                yield return ReadListenerRms(rms => loud = rms);
                Check(loud > .00001, "Resume restores live playback: " + id);
                report.AppendLine("MEASURE: " + id + " listener RMS " + loud.ToString("F6", CultureInfo.InvariantCulture));
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Check(scene.Audio.CallbackCount > 100, "Real AudioSource callbacks rendered the pressure core.");
            report.AppendLine("MEASURE: callbacks " + scene.Audio.CallbackCount + "; peak observed callback " + scene.Audio.PeakCallbackMilliseconds.ToString("F4", CultureInfo.InvariantCulture) + " ms (desktop observation, not an Android budget claim).");
#endif
        }
        IEnumerator AudioLevelsRun()
        {
            scene.SelectVehicle(Array.FindIndex(scene.vehicles, car => car.spec.id == "starter-hatch"));
            scene.config.vehicle.engineBehavior.mechanicalClatter = 0;
            scene.ApplyConfig(); scene.SetSliderControls(new EngineBenchControls { fixedRpm = true, targetRpm = 4000, throttle = .8f, load = .6f });
            yield return new WaitForSecondsRealtime(2); // Settle the physical turbo before comparing levels.
            double quiet = 0, loud = 0;
            SharedAudioSettings.SetMasterVolume(.25f); yield return new WaitForSecondsRealtime(.4f);
            yield return ReadListenerRms(rms => quiet = rms);
            SharedAudioSettings.SetMasterVolume(.75f); yield return new WaitForSecondsRealtime(.4f);
            yield return ReadListenerRms(rms => loud = rms);
            report.AppendLine("MEASURE: master 25% RMS " + quiet.ToString("F6", CultureInfo.InvariantCulture) + " / 75% RMS " + loud.ToString("F6", CultureInfo.InvariantCulture));
            Check(quiet > .000001 && quiet / loud > .28 && quiet / loud < .39, "Actual listener PCM scales with intermediate master values, not only mute.");
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            scene.ShowTestTab(8); yield return new WaitForSecondsRealtime(.15f);
            using (var mouse = new WindowsKeyboardProbe())
            {
                var rect = AudioSettingsView.VerificationSliderRect;
                Check(rect.width > 100 && rect.center.x < 540 && rect.center.y < 700, "Master slider has a reachable player-client rectangle.");
                int fromX = (int)(rect.x + 5 + (rect.width - 10) * .75f), toX = (int)(rect.x + 5 + (rect.width - 10) * .25f), y = (int)rect.center.y;
                mouse.MouseButtonAt(fromX, y, true); yield return new WaitForSecondsRealtime(.06f);
                mouse.MoveMouseAt(toX, y); yield return new WaitForSecondsRealtime(.15f);
                mouse.MouseButtonAt(toX, y, false); yield return new WaitForSecondsRealtime(.4f);
                report.AppendLine("MEASURE: dragged master " + SharedAudioSettings.MasterVolume.ToString("F6", CultureInfo.InvariantCulture) + " / listener " + AudioListener.volume.ToString("F6", CultureInfo.InvariantCulture));
                Check(Math.Abs(SharedAudioSettings.MasterVolume - .25f) < .035f, "Real mouse drag changes master preference.");
                yield return ReadListenerRms(rms => quiet = rms);
                Check(quiet / loud > .25 && quiet / loud < .43, "Master slider drag changes actual PCM loudness.");
                float saved = SharedAudioSettings.MasterVolume; SharedAudioSettings.Reload();
                Check(SharedAudioSettings.MasterVolume == saved, "Master drag release saves the selected value.");
                yield return new WaitForEndOfFrame(); yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("audio-levels-settings.png"));
                SharedAudioSettings.SetMasterVolume(.75f); scene.ShowTestTab(5);
                yield return new WaitForSecondsRealtime(.2f);
                rect = scene.VerificationMixSlider("engineGain");
                Check(rect.width > 100 && rect.center.y < 680, "Engine gain slider is reachable in Mix.");
                fromX = (int)(rect.x + 5 + (rect.width - 10) * .4f); toX = (int)(rect.x + 5 + (rect.width - 10) * .1f); y = (int)rect.center.y;
                mouse.MouseButtonAt(fromX, y, true); yield return new WaitForSecondsRealtime(.06f);
                mouse.MoveMouseAt(toX, y); yield return new WaitForSecondsRealtime(.15f);
                mouse.MouseButtonAt(toX, y, false); yield return new WaitForSecondsRealtime(.45f);
                report.AppendLine("MEASURE: dragged engine gain config " + scene.config.vehicle.sound.engineGain.ToString("F6", CultureInfo.InvariantCulture) + " / running " + scene.Bench.spec.sound.engineGain.ToString("F6", CultureInfo.InvariantCulture));
                Check(Math.Abs(scene.Bench.spec.sound.engineGain - .2f) < .07f, "Real Mix slider drag applies engine gain to the running DSP hardware.");
                yield return ReadListenerRms(rms => quiet = rms);
                report.AppendLine("MEASURE: engine gain RMS before " + loud.ToString("F6", CultureInfo.InvariantCulture) + " / after " + quiet.ToString("F6", CultureInfo.InvariantCulture));
                Check(quiet < loud * .45 && quiet > .000001, "Engine gain slider drag changes actual PCM loudness.");
                yield return new WaitForEndOfFrame(); yield return DeterministicCapture.Screenshot(output, System.IO.Path.GetFileNameWithoutExtension("audio-levels-mix.png"));
                scene.SelectVehicle(0); scene.config.vehicle.sound.engineGain = 0; scene.config.vehicle.engineBehavior.mechanicalClatter = 0;
                scene.ApplyConfig(); scene.SetSliderControls(new EngineBenchControls { fixedRpm = true, targetRpm = 4000, throttle = .8f, load = .6f, launchControls = false });
                scene.ShowTestTab(5); yield return new WaitForSecondsRealtime(2);
                yield return ReadListenerRms(rms => loud = rms);
                rect = scene.VerificationMixSlider("turboGain");
                fromX = (int)(rect.x + 5 + (rect.width - 10) * .12f); toX = (int)(rect.x + 5); y = (int)rect.center.y;
                mouse.MouseButtonAt(fromX, y, true); yield return new WaitForSecondsRealtime(.06f);
                mouse.MoveMouseAt(toX, y); yield return new WaitForSecondsRealtime(.15f);
                mouse.MouseButtonAt(toX, y, false); yield return new WaitForSecondsRealtime(.45f);
                yield return ReadListenerRms(rms => quiet = rms);
                report.AppendLine("MEASURE: turbo gain " + scene.Bench.spec.sound.turboGain.ToString("F6", CultureInfo.InvariantCulture) + " / RMS before " + loud.ToString("F6", CultureInfo.InvariantCulture) + " / after " + quiet.ToString("F6", CultureInfo.InvariantCulture));
                Check(scene.Bench.spec.sound.turboGain < .015 && quiet < loud * .08 && loud > .000001, "Real accessory gain drag independently silences turbo whistle.");
                scene.SelectVehicle(Array.FindIndex(scene.vehicles, car => car.spec.id == "v8-muscle"));
                Check(scene.Bench.spec.sound.idleBoostDb == 6, "Stock V8 loads the louder +6 dB idle setting.");
            }
#endif
        }
    }
}
