using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    // Opt-in integration probe: real rendered layout and native mouse/keyboard events.
    internal sealed class NativeMenuVerification : MonoBehaviour
    {
        internal static bool Active => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-native-menu-verify") >= 0;
        PrototypeGame game;
        NativeMenuShell shell;
        string folder;
        int checks, errors;
        float started;
        readonly StringBuilder report = new StringBuilder("Native navigation / Settings shell verification\n");
        internal static void AttachIfRequested(PrototypeGame game, NativeMenuShell shell)
        {
            var args = Environment.GetCommandLineArgs();
            if (!Debug.isDebugBuild || Array.IndexOf(args, "--draglab-native-menu-verify") < 0) return;
            int at = Array.IndexOf(args, "--draglab-verify");
            if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("Native menu verification needs an isolated profile folder.");
            var probe = game.gameObject.AddComponent<NativeMenuVerification>();
            probe.game = game; probe.shell = shell; probe.folder = Path.GetFullPath(args[at + 1]);
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; report.AppendLine(message); } }
        void Check(bool ok, string label) { checks++; if (!ok) throw new InvalidOperationException(label); report.AppendLine("PASS: " + label); }
        void Update() { if (started > 0 && Time.realtimeSinceStartup - started > 110) Finish(new TimeoutException("Native shell probe timeout")); }
        void Finish(Exception exception = null)
        {
            report.AppendLine("Checks: " + checks + " / runtime errors: " + errors);
            if (exception != null) report.AppendLine("FAIL: " + exception);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "verification.txt"), report.ToString(), new UTF8Encoding(false));
            Application.Quit(exception == null && errors == 0 ? 0 : 1);
        }
        IEnumerator Start()
        {
            started = Time.realtimeSinceStartup;
            var routines = new System.Collections.Generic.Stack<IEnumerator>(); routines.Push(Run());
            while (routines.Count > 0)
            {
                var routine = routines.Peek(); bool more;
                try { more = routine.MoveNext(); } catch (Exception exception) { Finish(exception); yield break; }
                if (!more) { routines.Pop(); continue; }
                if (routine.Current is IEnumerator nested) routines.Push(nested); else yield return routine.Current;
            }
            Finish();
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return new WaitForSecondsRealtime(.2f);
        }
        void LayoutCheck()
        {
            var root = shell.Root; var nav = root.Q("navBar").worldBound; var settings = root.Q("settingsButton").worldBound;
            float scale = NativeMenuShell.DisplayScale(Screen.width, Screen.height);
            Check(nav.yMin < 1 && Mathf.Abs(nav.width * scale - Screen.width) < 2, "Navigation anchored top/full width at " + Screen.width + "x" + Screen.height);
            Check(Mathf.Abs(settings.xMax - nav.xMax) < 1 && Mathf.Abs(settings.height - nav.height) < 1, "Settings anchored right/full bar height: nav " + nav + ", settings " + settings);
            foreach (string id in new[] { "navGarage", "navBuild", "navTuning", "navRace", "navBusiness", "navData", "cashGroup", "fuelGroup", "repGroup" })
            {
                var bounds = root.Q(id).worldBound;
                Check(bounds.width > 0 && bounds.xMin >= 0 && bounds.xMax <= settings.xMin, id + " visible without overlapping Settings");
            }
            var icon = root.Q("settingsButton").Q<FilledMenuIcon>().worldBound;
            Check(Mathf.Abs(icon.width - icon.height) < .1 && icon.height < settings.height, "Settings filled icon keeps aspect ratio and padding");
        }
        IEnumerator Run()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            yield return new WaitForSecondsRealtime(.5f);
            using (var input = new WindowsKeyboardProbe())
            {
                Check(ProfileStore.FilePath == Path.Combine(folder, "profile-v1.json"), "Profile isolated from player assets");
                var baseline = JsonUtility.ToJson(game.Profile);
                string disk = File.Exists(ProfileStore.FilePath) ? File.ReadAllText(ProfileStore.FilePath) : null;
                yield return Key(input, 0x77);
                Check(shell.Visible && game.ScreenName == "Garage", "F8 displays native Garage shell on unchanged route");
                yield return Capture("garage-1280"); LayoutCheck();
                var hover = shell.Root.Q<Button>("navBuild"); var normalColor = hover.resolvedStyle.backgroundColor;
                input.MoveMouseAt((int)hover.worldBound.center.x, (int)hover.worldBound.center.y);
                yield return new WaitForSecondsRealtime(.2f);
                Check(shell.Root.Q<Label>("interactionHint").text == "Build" && hover.resolvedStyle.backgroundColor != normalColor, "Hover supplies label and visual feedback");
                foreach (var destination in new[] { new[] {"navBuild", "Parts"}, new[] {"navTuning", "Tuning"}, new[] {"navRace", "Race setup"}, new[] {"navBusiness", "Business"}, new[] {"navData", "Stats"}, new[] {"navGarage", "Garage"} })
                {
                    yield return Click(input, destination[0]);
                    Check(shell.Visible && game.ScreenName == destination[1], "Native click selects " + destination[0] + " and stays New");
                }
                yield return Click(input, "settingsButton");
                Check(game.ScreenName == "Settings" && shell.Visible, "Gear opens fullscreen native Settings");
                var hit = shell.Root.Q("settingsButton").worldBound;
                input.MouseButtonAt((int)hit.center.x, (int)hit.center.y, true);
                yield return Key(input, 0x77);
                Check(shell.Visible, "Active native pointer gesture blocks F8");
                input.MouseButtonAt((int)hit.center.x, (int)hit.center.y, false);
                yield return new WaitForSecondsRealtime(.15f);
                foreach (string id in new[] { "Difficulty", "Sound", "Units", "Controls", "General" })
                {
                    yield return Click(input, "tab" + id);
                    Check(shell.Root.Q<Button>("tab" + id).ClassListContains("selected"), "Settings category " + id + " selected");
                }
                int category = shell.Category;
                yield return Key(input, 0x77);
                Check(!shell.Visible && game.ScreenName == "Settings", "F8 restores original Settings on same route");
                yield return Key(input, 0x77);
                Check(shell.Visible && shell.Category == category, "F8 retains native Settings category");
                Check(baseline == JsonUtility.ToJson(game.Profile), "Navigation/categories/F8 do not publish player preferences or resources");
                Check(disk == (File.Exists(ProfileStore.FilePath) ? File.ReadAllText(ProfileStore.FilePath) : null), "Shell actions do not save preferences");
                yield return Capture("settings-1280");
                foreach (int width in new[] { 1024, 1920, 3840 })
                {
                    int height = width == 1024 ? 768 : width * 9 / 16;
                    Screen.SetResolution(width, height, FullScreenMode.Windowed);
                    yield return new WaitForSecondsRealtime(.8f);
                    Check(shell.Visible && shell.Category == category, "Resize retains Settings presentation/category");
                    if (Screen.width != width || Screen.height != height)
                        report.AppendLine("LIMIT: requested " + width + "x" + height + "; platform rendered " + Screen.width + "x" + Screen.height);
                    LayoutCheck(); yield return Capture("settings-" + Screen.width + "x" + Screen.height);
                }
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.7f);
                var texture = new RenderTexture(3840, 2160, 24); texture.Create();
                shell.Panel.targetTexture = texture;
                yield return new WaitForSecondsRealtime(.4f);
                Check(Mathf.Abs(shell.Root.Q("navBar").worldBound.width - 1920) < 1 && shell.Panel.scale == 2, "True 3840x2160 render target uses shared 2x scale");
                yield return new WaitForEndOfFrame();
                var previousTarget = RenderTexture.active; RenderTexture.active = texture;
                var image = new Texture2D(3840, 2160, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 3840, 2160), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(folder, "settings-4k-render-target.png"), image.EncodeToPNG());
                RenderTexture.active = previousTarget; shell.Panel.targetTexture = null;
                texture.Release(); Destroy(texture); Destroy(image);
                Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
                yield return new WaitForSecondsRealtime(.8f);
                Check(Screen.fullScreenMode == FullScreenMode.FullScreenWindow && shell.Visible && shell.Category == category, "Fullscreen transition preserves Settings category");
                LayoutCheck(); yield return Capture("settings-fullscreen");
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.7f);
                Check(Screen.fullScreenMode == FullScreenMode.Windowed && Screen.width == 1280 && Screen.height == 720, "Windowed mode/default fixture size restored");
                Check(baseline == JsonUtility.ToJson(game.Profile), "Resolution changes preserve actual player state");
                // Empty garage feedback is checked with a temporary fixture selection, never saved.
                var cars = game.Profile.cars; game.Profile.cars = new System.Collections.Generic.List<DragLab.Domain.OwnedCar>();
                shell.Refresh();
                Check(shell.Root.Q<Label>("fuel").text == "— L", "Empty garage does not fabricate fuel/tank");
                Check(shell.Root.Q<Label>("selectedCar").text == "No car selected", "Car identity refreshes without route/category changes");
                game.Profile.cars = cars;
                shell.Refresh();
                yield return Capture("settings-restored");
            }
#else
            throw new InvalidOperationException("Native probe requires a Windows development player.");
#endif
        }
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator Key(WindowsKeyboardProbe input, ushort key)
        { input.Key(key, true); yield return new WaitForSecondsRealtime(.08f); input.Key(key, false); yield return new WaitForSecondsRealtime(.2f); }
        IEnumerator Click(WindowsKeyboardProbe input, string id)
        {
            var rect = shell.Root.Q(id).worldBound;
            float scale = NativeMenuShell.DisplayScale(Screen.width, Screen.height);
            input.MouseButtonAt((int)(rect.center.x * scale), (int)(rect.center.y * scale), true);
            yield return new WaitForSecondsRealtime(.08f);
            input.MouseButtonAt((int)(rect.center.x * scale), (int)(rect.center.y * scale), false);
            yield return new WaitForSecondsRealtime(.2f);
        }
#endif
    }
}
