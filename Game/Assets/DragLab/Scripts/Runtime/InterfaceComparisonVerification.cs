using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    // Opt-in native input/rendering probe. ProfileStore redirects --draglab-verify to a fixture.
    internal sealed class InterfaceComparisonVerification : MonoBehaviour
    {
        internal static bool Active => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-ui-verify") >= 0;
        readonly StringBuilder report = new StringBuilder("Native interface comparison verification\n");
        string folder;
        int errors, checks;
        float started;
        internal static void AttachIfRequested()
        {
            if (!Active || FindFirstObjectByType<InterfaceComparisonVerification>() != null) return;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--draglab-verify");
            if (index < 0 || index + 1 == args.Length) throw new ArgumentException("UI verification requires an isolated --draglab-verify folder.");
            var probe = new GameObject("Interface comparison verification").AddComponent<InterfaceComparisonVerification>();
            probe.folder = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(probe.folder);
            DontDestroyOnLoad(probe.gameObject); Application.runInBackground = true;
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Log(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; report.AppendLine(message); } }
        void Check(bool ok, string label) { checks++; if (!ok) throw new InvalidOperationException(label); report.AppendLine("PASS: " + label); }
        static void Field(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        void Finish(Exception error = null)
        {
            report.AppendLine("Checks: " + checks + " / runtime errors: " + errors);
            if (error != null) report.AppendLine("FAIL: " + error);
            File.WriteAllText(Path.Combine(folder, "verification.txt"), report.ToString(), new UTF8Encoding(false));
            Application.Quit(error == null && errors == 0 ? 0 : 1);
        }
        bool CaptureAll => Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-capture-all-interfaces") >= 0;
        void Update() { if (started > 0 && Time.realtimeSinceStartup - started > (CaptureAll ? 180 : 90)) Finish(new TimeoutException("UI probe timeout")); }
        IEnumerator Start()
        {
            started = Time.realtimeSinceStartup;
            var stack = new System.Collections.Generic.Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                var routine = stack.Peek(); bool more;
                try { more = routine.MoveNext(); }
                catch (Exception error) { Finish(error); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (routine.Current is IEnumerator nested) stack.Push(nested); else yield return routine.Current;
            }
            Finish();
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator Run()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            yield return new WaitForSecondsRealtime(.4f);
            using (var input = new WindowsKeyboardProbe())
            {
                var game = FindFirstObjectByType<PrototypeGame>();
                Check(game != null && ProfileStore.FilePath == Path.Combine(folder, "save-slot-1.sqlite3"), "Real player uses an isolated database fixture");
                // Fresh-dealership behavior is verified by NativeMenuVerification.Market.
                // This historical whole-interface probe needs an owned car for tuning/race routes.
                if(game.Profile.firstPurchasePending){VehicleCollection.Buy(game.Profile,game.Profile.catalog,game.Profile.vehicleStock.listings.First(o=>o.model=="starter-hatch"));ProfileStore.Save(game.Profile);game.ReturnToGarage();}
                if (CaptureAll) { yield return CaptureInterfaceSurvey(game); yield break; }
                InterfaceComparison.SetReplacement("Garage",false);
                var profile = game.Profile; var saved = JsonUtility.ToJson(profile);
                string disk = File.Exists(ProfileStore.FilePath) ? File.ReadAllText(ProfileStore.FilePath) : null;
                foreach (string route in new[] { "Garage", "Settings", "Driving", "Parts", "Tuning", "Stats", "Race setup", "Career", "Business", "Collection", "Setups", "Assemblies", "Recovery" })
                {
                    Field(game, "screen", route); yield return new WaitForSecondsRealtime(.12f);
                    yield return Press(input, 0x77);
                    Check(InterfaceComparison.IsReplacement(route) && game.ScreenName == route, route + " enters an empty replacement without routing elsewhere");
                    if (route == "Garage") yield return Capture("garage-new");
                    yield return Press(input, 0x77);
                    Check(!InterfaceComparison.IsReplacement(route), route + " restores the existing interface");
                }
                Check(ReferenceEquals(game.Profile, profile) && saved == JsonUtility.ToJson(profile), "All menu toggles retain exact player state without publication");
                Field(game, "screen", "Garage"); yield return Press(input, 0x77);
                Field(game, "screen", "Settings"); yield return new WaitForSecondsRealtime(.1f);
                Check(!InterfaceComparison.IsReplacement("Settings"), "Presentation choice is independent for each main interface");
                Field(game, "screen", "Garage"); yield return new WaitForSecondsRealtime(.1f);
                Check(InterfaceComparison.IsReplacement("Garage"), "Returning to a main interface remembers its session choice");
                yield return Press(input, 0x77);
                game.OpenTuning(); game.DraftValues.launchRpm += 100; var draft = game.DraftValues;
                yield return Press(input, 0x77); yield return Press(input, 0x77);
                Check(ReferenceEquals(game.DraftValues, draft) && game.DraftDirty, "Unapplied tuning draft survives comparison");
                game.ShowParts(PartCategory.Pistons);
                var vehicle = Resources.LoadAll<DragLab.Data.VehicleDefinition>("Data/Vehicles").First(v => v.spec.id == game.Profile.selectedVehicleId);
                game.AddToCart(game.Profile.catalog.parts.First(p => p.category == PartCategory.Pistons && game.Profile.catalog.Fits(vehicle.spec, p)).id);
                var line = game.CartLines[0]; yield return Press(input, 0x77); yield return Press(input, 0x77);
                Check(game.CartCount > 0 && ReferenceEquals(game.CartLines[0], line), "Unconfirmed cart retains original lines and quantities");
                Check(disk == (File.Exists(ProfileStore.FilePath) ? File.ReadAllText(ProfileStore.FilePath) : null), "Comparison, tune draft and cart preview do not write the save");
                game.OpenValuation(ValuationKind.Car, game.Profile.SelectedCar.instanceId);
                yield return Press(input, 0x77); Check(InterfaceComparison.IsReplacement("Valuation"), "Valuation switches as a whole main interface");
                yield return Press(input, 0x77);
                game.ShowDrivingSettings(); game.OpenAudioSettings(); Field(game, "resetGameConfirm", true);
                yield return Press(input, 0x77); Check(!InterfaceComparison.IsReplacement("Settings"), "Reset confirmation blocks switching");
                Field(game, "resetGameConfirm", false); game.StartPractice();
                input.Key(0x57, true); yield return new WaitForSecondsRealtime(.3f);
                var race = game.Race; input.Key(0x77, true); yield return new WaitForSecondsRealtime(.8f);
                Check(InterfaceComparison.IsReplacement(game.ComparisonRoute), "Held F8 toggles only once");
                Check(ReferenceEquals(game.Race, race) && race.player.state.throttle > .99, "Held W and the same live race survive comparison");
                float scale = RaceInputAdapter.Scale(Screen.width, Screen.height);
                Rect oldShift = RaceInputAdapter.ControlRect(RaceInputAdapter.UpControl, Screen.width / scale, Screen.height / scale);
                int gear = race.player.state.gear;
                input.MouseButtonAt((int)(oldShift.center.x * scale), (int)(oldShift.center.y * scale), true);
                yield return new WaitForSecondsRealtime(.1f);
                input.MouseButtonAt((int)(oldShift.center.x * scale), (int)(oldShift.center.y * scale), false);
                Check(race.player.state.gear == gear, "Hidden legacy race shift hitbox cannot launch or change gear");
                yield return Capture("race-new"); input.Key(0x77, false); yield return new WaitForSecondsRealtime(.1f);
                yield return Press(input, 0x77); input.Key(0x57, false);
                yield return Capture("race-old");
                game.ReturnToGarage(); SceneManager.LoadScene("EngineTest"); yield return new WaitForSecondsRealtime(.5f);
                var sound = FindFirstObjectByType<EngineTestScene>(); sound.KeyboardControl();
                input.Key(0x57, true); yield return new WaitForSecondsRealtime(.2f); var bench = sound.Bench;
                yield return Press(input, 0x77);
                Check(InterfaceComparison.IsReplacement(EngineTestScene.ComparisonRoute) && !sound.RunningScenario && ReferenceEquals(sound.Bench, bench), "Sound F8 hides the entire interface without starting scenario B or replacing the engine");
                Check(sound.Controls.Source == DragLab.Domain.EngineBenchControlSource.Keyboard && sound.Bench.state.throttle > .99, "Sound bench keeps held W and keyboard ownership");
                yield return Capture("sound-new"); yield return Press(input, 0x77); input.Key(0x57, false);
                sound.ShowTestTab(5); yield return new WaitForSecondsRealtime(.2f);
                Rect textField = sound.VerificationFieldControl("sound.engineGain:text");
                input.MouseButtonAt((int)textField.center.x, (int)textField.center.y, true); yield return new WaitForSecondsRealtime(.1f);
                input.MouseButtonAt((int)textField.center.x, (int)textField.center.y, false); yield return new WaitForSecondsRealtime(.2f);
                Check(sound.IsEditingText, "Numeric field has actual IMGUI text focus");
                yield return Press(input, 0x77); Check(!InterfaceComparison.IsReplacement(EngineTestScene.ComparisonRoute), "Text focus blocks comparison");
                yield return Press(input, 0x57); Check(!sound.IsEditingText, "Intentional W takeover leaves numeric editing before scenario shortcut"); sound.CaptureSlot(false);
                input.Key(0x10, true); yield return Press(input, 0x77); input.Key(0x10, false);
                Check(sound.RunningScenario && !InterfaceComparison.IsReplacement(EngineTestScene.ComparisonRoute), "Shift+F8 retains scenario B without switching presentation");
                sound.Pause(true); SceneManager.LoadScene("DrivingTest"); yield return new WaitForSecondsRealtime(.5f);
                var physical = FindFirstObjectByType<PhysicalDrivingBench>();
                input.Key(0x57, true); yield return new WaitForSecondsRealtime(.2f); var session = physical.Session; long tick = session.TotalTick;
                yield return Press(input, 0x77);
                Check(InterfaceComparison.IsReplacement(PhysicalDrivingBench.ComparisonRoute) && ReferenceEquals(physical.Session, session), "Physical F8 retains the same shared simulation session");
                Check(physical.KeyboardOwned && physical.AppliedInput.throttle > .99 && session.TotalTick > tick, "Physical simulation advances with held gas while hidden");
                yield return Capture("physical-new"); yield return Press(input, 0x77); input.Key(0x57, false);
                Check(!physical.Recording, "Comparison does not enable recording"); yield return Capture("physical-old");
                Check(errors == 0, "No native rendering or runtime errors");
            }
#else
            throw new InvalidOperationException("Run this probe in the Windows development player.");
#endif
        }
        // Rendering survey is deliberately separate from historical input assertions.
        // Select each presentation directly; focused probes still validate real input.
        IEnumerator CaptureInterfaceSurvey(PrototypeGame game)
        {
            game.OpenTuning();
            foreach (string route in new[] { "Garage", "Settings", "Driving", "Parts", "Tuning", "Stats", "Race setup", "Career", "Business", "Collection", "Setups", "Assemblies", "Recovery" })
            {
                Field(game, "screen", route);
                foreach (bool replacement in new[] { false, true })
                {
                    InterfaceComparison.SetReplacement(route, replacement);
                    yield return new WaitForSecondsRealtime(.3f);
                    yield return Capture("route-" + route.Replace(" ", "-").ToLowerInvariant() + (replacement ? "-new" : "-old"));
                }
                InterfaceComparison.SetReplacement(route, false);
            }
            game.OpenValuation(ValuationKind.Car, game.Profile.SelectedCar.instanceId);
            foreach (bool replacement in new[] { false, true })
            {
                InterfaceComparison.SetReplacement("Valuation", replacement);
                yield return new WaitForSecondsRealtime(.3f); yield return Capture("valuation-" + (replacement ? "new" : "old"));
            }
            game.ReturnToGarage(); game.StartPractice(); game.PauseRace(true);
            foreach (bool replacement in new[] { false, true })
            {
                InterfaceComparison.SetReplacement("Pause", replacement);
                yield return new WaitForSecondsRealtime(.3f); yield return Capture("pause-" + (replacement ? "new" : "old"));
            }
            game.ReturnToGarage(); SceneManager.LoadScene("EngineTest"); yield return new WaitForSecondsRealtime(.5f);
            var sound=FindFirstObjectByType<EngineTestScene>(); sound.Pause(true);
            for (int tab=0; tab<9; tab++)
            {
                sound.ShowTestTab(tab); yield return new WaitForSecondsRealtime(.2f); yield return Capture("sound-tab-" + tab);
            }
            InterfaceComparison.SetReplacement(EngineTestScene.ComparisonRoute,true);
            yield return new WaitForSecondsRealtime(.2f); yield return Capture("sound-new");
            SceneManager.LoadScene("DrivingTest"); yield return new WaitForSecondsRealtime(.5f);
            var physical=FindFirstObjectByType<PhysicalDrivingBench>();
            var physicalCars=Resources.LoadAll<DragLab.Data.VehicleDefinition>("Data/Vehicles").OrderBy(v=>v.spec.id).ToArray();
            physical.SelectVehicle(Array.FindIndex(physicalCars,v=>v.spec.id=="starter-hatch"));physical.SetPaused(true);
            yield return Capture("physical-old");
            physical.StartScenario(PhysicalScenario.FullPullShiftCoastBrake);
            float deadline=Time.realtimeSinceStartup+45;
            while(!physical.Paused&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(physical.Paused&&physical.Session.samples.Count>1000,"Physical render survey has an actual recorded shared-simulation pull");
            foreach(string group in DrivingTraceChannels.Groups)
            {
                Field(physical,"showDiagnostics",false);
                Field(physical,"graph",Array.FindIndex(DrivingTraceChannels.All,c=>c.group==group));
                yield return new WaitForSecondsRealtime(.15f);
                yield return Capture("physical-graph-"+System.Text.RegularExpressions.Regex.Replace(group.ToLowerInvariant(),"[^a-z0-9]+","-"));
            }
            foreach(int offset in new[]{0,140,280,420,700,1000})
            {physical.MassPanelForVerification(offset);yield return new WaitForSecondsRealtime(.15f);yield return Capture("physical-diagnostics-"+offset);}
            Field(physical,"showDiagnostics",false);
            Check(physical.ReplayLast(),"Physical visual survey replays its real recorded input");
            deadline=Time.realtimeSinceStartup+45;while(!physical.Paused&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(physical.Paused,"Physical replay finishes before its overlay capture");
            Field(physical,"graph",0);yield return Capture("physical-replay-overlays");
            physical.SetStartingTire(2,.35f,110);physical.SetPaused(true);
            foreach(string slot in new[]{"Tires","Ignition","Clutch","Cooling"})
            {
                physical.ElementPanelForVerification(slot,1100);
                yield return new WaitForSecondsRealtime(.15f);yield return Capture("physical-component-"+slot.ToLowerInvariant());
            }
            Field(physical,"audioSettings",true);physical.TirePanelForVerification(0);
            yield return new WaitForSecondsRealtime(.15f);yield return Capture("physical-audio-settings");
            InterfaceComparison.SetReplacement(PhysicalDrivingBench.ComparisonRoute,true);
            yield return new WaitForSecondsRealtime(.2f); yield return Capture("physical-new");
            Check(errors==0,"Every interface survey capture renders without runtime errors");
        }
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        static IEnumerator Press(WindowsKeyboardProbe input, ushort key)
        {
            input.Key(key, true); yield return new WaitForSecondsRealtime(.09f);
            input.Key(key, false); yield return new WaitForSecondsRealtime(.14f);
        }
#endif
    }
}
