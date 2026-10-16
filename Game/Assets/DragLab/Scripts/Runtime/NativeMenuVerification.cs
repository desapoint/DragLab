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
    internal sealed partial class NativeMenuVerification : MonoBehaviour
    {
        internal static bool Active => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-native-menu-verify") >= 0;
        PrototypeGame game;
        NativeMenuShell shell;
        string folder;
        int checks, errors;
        float started;
        float timeoutSeconds=360;
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
        void Update() { if (started > 0 && Time.realtimeSinceStartup - started > timeoutSeconds) Finish(new TimeoutException("Native shell probe timeout")); }
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
            Application.runInBackground=true;
            timeoutSeconds=Array.Exists(Environment.GetCommandLineArgs(),arg=>arg.EndsWith("-only-verify",StringComparison.Ordinal))?360:720;
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
            yield return DeterministicCapture.Screenshot(folder,name);
            yield return new WaitForSecondsRealtime(.2f);
        }
        void LayoutCheck()
        {
            var root = shell.Root; var nav = root.Q("navBar").worldBound; var settings = root.Q("settingsButton").worldBound;
            float scale = NativeMenuShell.DisplayScale(Screen.width, Screen.height);
            Check(nav.yMin < 1 && Mathf.Abs(nav.width * scale - Screen.width) < 2, "Navigation anchored top/full width at " + Screen.width + "x" + Screen.height);
            Check(Mathf.Abs(nav.xMax - settings.xMax - 12) < 1 && Mathf.Abs(settings.height - nav.height) < 1, "Settings has twelve-point right inset/full bar height: nav " + nav + ", settings " + settings);
            foreach (string id in new[] { "navGarage", "navBuild", "navTuning", "navRace", "navBusiness", "navData", "cashGroup", "fuelGroup", "repGroup" })
            {
                var bounds = root.Q(id).worldBound;
                Check(bounds.width > 0 && bounds.xMin >= 0 && bounds.xMax <= settings.xMin, id + " visible without overlapping Settings");
            }
            var icon = root.Q("settingsButton").Q<FilledMenuIcon>().worldBound;
            Check(Mathf.Abs(icon.width - icon.height) < .1 && icon.height < settings.height, "Settings filled icon keeps aspect ratio and padding");
        }
        void DialogScrollBounds(string context)
        {
            var view=shell.Root.Q<ScrollView>("modalContent");var close=shell.Root.Q("modalClose").worldBound;
            Check(view.worldBound.yMin>=close.yMax+12&&view.worldBound.yMax<=shell.Root.Q("modalActions").worldBound.yMin,"Dialog content clears fixed X/title and actions: "+context);
            var bar=view.verticalScroller;
            if(bar.resolvedStyle.display!=DisplayStyle.None)Check(Mathf.Abs(shell.Root.Q("modalCard").worldBound.xMax-bar.worldBound.xMax-10)<1&&Mathf.Abs(bar.worldBound.width-8)<.1f&&bar.lowButton.resolvedStyle.display==DisplayStyle.None&&bar.highButton.resolvedStyle.display==DisplayStyle.None,"Dialog scrollbar uses the shared thin rail at a ten-point right inset: "+context);
        }
        IEnumerator Run()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            yield return new WaitForSecondsRealtime(.5f);
            using (var input = new WindowsKeyboardProbe())
            {
                Check(ProfileStore.FilePath == Path.Combine(folder, "save-slot-1.sqlite3"), "Profile isolated from player assets");
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-reset-required-only-verify")>=0){yield return IncompatibleSaveChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-shine-only-verify")>=0){yield return AngledShineChecks(input);yield break;}
                if(game.Profile.firstPurchasePending)yield return StarterPurchaseChecks(input);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-owned-fitted-only-verify")>=0){yield return OwnedFittedPartsChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-navigation-only-verify")>=0){yield return NavigationDefaultsChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-race-pause-only-verify")>=0){yield return RacePauseChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-finish-pacing-only-verify")>=0){yield return FinishPacingChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-race-results-only-verify")>=0){yield return ResultActionChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-race-hud-only-verify")>=0){yield return RaceHudChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-setup-management-only-verify")>=0){yield return SetupManagementChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-race-selection-only-verify")>=0){yield return RaceSelectionChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-disposal-only-verify")>=0){yield return DisposalChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-sqlite-state-only-verify")>=0){if(!shell.Visible)yield return Key(input,0x77);yield return Click(input,"settingsButton");yield return SettingsChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-part-brands-only-verify")>=0){yield return PartBrandInterfaceChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-awd-hardware-only-verify")>=0){yield return AwdHardwareInterfaceChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-responsiveness-only-verify")>=0){yield return ResponsivenessChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-pricing-only-verify")>=0){yield return SupplyPricingChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-rebuild-only-verify")>=0){yield return RebuildFlowChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-maintenance-only-verify")>=0){yield return MaintenanceBatchChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-parts-only-verify")>=0){yield return NativePartsChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-tuning-only-verify")>=0){yield return TuningLayoutChecks(input);yield break;}
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-dealership-only-verify")>=0) {
                    Check(File.Exists(ProfileStore.FilePath)||ProfileStore.TrySave(game.Profile),"Focused dealership probe has a durable isolated baseline");
                    if(!shell.Visible)yield return Key(input,0x77);yield return DealershipChecks(input);yield break;
                }
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--draglab-inspection-only-verify")>=0){if(!shell.Visible)yield return Key(input,0x77);yield return UsedInspectionChecks(input);yield break;}
                yield return TuningLayoutChecks(input);
                MoneyFormattingChecks();
                var baseline = JsonUtility.ToJson(game.Profile);
                string disk = File.Exists(ProfileStore.FilePath) ? File.ReadAllText(ProfileStore.FilePath) : null;
                if(!shell.Visible)yield return Key(input, 0x77);
                Check(shell.Visible && game.ScreenName == "Garage", "F8 displays native Garage shell on unchanged route");
                yield return Capture("garage-1280"); LayoutCheck();
                yield return new WaitForEndOfFrame();
                var glowImage = ScreenCapture.CaptureScreenshotAsTexture();
                var activeBounds = shell.Root.Q("navGarage").worldBound;
                Color bottomGlow = glowImage.GetPixel((int)activeBounds.center.x,Screen.height-(int)(activeBounds.yMax-10)-1);
                Color topGlow = glowImage.GetPixel((int)activeBounds.center.x,Screen.height-(int)(activeBounds.yMin+6)-1);
                Color edgeGlow = glowImage.GetPixel((int)(activeBounds.xMin+3),Screen.height-(int)(activeBounds.yMax-10)-1);
                Check(bottomGlow.r-bottomGlow.g > topGlow.r-topGlow.g+.12f && bottomGlow.r-bottomGlow.g > edgeGlow.r-edgeGlow.g+.08f,
                    "Rendered selected-tab red glow fades upward and sideways from its bottom center");
                var settingsBounds = shell.Root.Q("settingsButton").worldBound;
                Color plateTopOutside = Pixel(glowImage,settingsBounds.xMin+5,settingsBounds.yMin+10);
                Color plateBottomInside = Pixel(glowImage,settingsBounds.xMin+8,settingsBounds.yMax-12);
                Color plateTopInside = Pixel(glowImage,settingsBounds.xMax-8,settingsBounds.yMin+10);
                Color plateBottomOutside = Pixel(glowImage,settingsBounds.xMax-6,settingsBounds.yMax-12);
                Check(plateBottomInside.r-plateBottomInside.g > plateTopOutside.r-plateTopOutside.g+.08f &&
                    plateTopInside.r-plateTopInside.g > plateBottomOutside.r-plateBottomOutside.g+.08f,
                    "Rendered Settings red plate has two slanted sides rather than a rectangular fill");
                Color leftTrim = Pixel(glowImage,8,62),rightTrim = Pixel(glowImage,settingsBounds.xMin-5,62),centerTrim = Pixel(glowImage,Screen.width*.63f,62);
                Check(leftTrim.r-leftTrim.g > .15f && rightTrim.r-rightTrim.g > .15f && centerTrim.r-centerTrim.g < .08f,
                    "Header has red bottom accents on both ends with a neutral center");
                Color footerTrim = Pixel(glowImage,Screen.width*.22f,Screen.height-31),footerCenter = Pixel(glowImage,Screen.width*.5f,Screen.height-31);
                Check(footerTrim.r-footerTrim.g > .15f && footerCenter.r-footerCenter.g < .08f,
                    "Footer red trim keeps its metal center break and raised corner details");
                Destroy(glowImage);
                Check(shell.Root.Q("pageTitle") == null && shell.Root.Q("pageDescription") == null && shell.Root.Q("contentTitle") == null && shell.Root.Q("contentDescription") == null,
                    "Selected navigation/tabs replace redundant titles, subtitles and placeholder descriptions");
                Check(shell.Root.Q<Button>("navBuild").Q<Label>().resolvedStyle.unityFontDefinition.fontAsset.name == "BarlowCondensed-BoldItalic",
                    "Navigation uses bundled condensed bold italic racing font");
                Check(shell.Root.Q<Button>("tabSound").Q<Label>().resolvedStyle.unityFontDefinition.fontAsset.name == "BarlowCondensed-Medium",
                    "Settings uses bundled readable condensed medium font");
                var hover = shell.Root.Q<Button>("navBuild"); var normalColor = hover.resolvedStyle.backgroundColor;
                var surface = hover.Q<RacingSurface>();
                Check(surface != null && surface.pickingMode == PickingMode.Ignore, "Layered button surface cannot intercept clicks");
                input.MoveMouseAt((int)hover.worldBound.center.x, (int)hover.worldBound.center.y);
                yield return new WaitForSecondsRealtime(.2f);
                Check(shell.Root.Q<Label>("interactionHint").text == "F8 · Old / New" && hover.resolvedStyle.backgroundColor != normalColor, "Hover provides visual feedback without duplicating visible button labels in the footer");
                Check(surface.ShineActive && surface.ShineProgress > 0 && surface.ShineProgress < 1, "Hover starts timed shine sweep");
                float firstShine = surface.ShineProgress;
                yield return Capture("button-shine-a");
                Check(surface.ShineProgress > firstShine, "Shine progresses across successive rendered frames");
                yield return Capture("button-shine-b");
                yield return new WaitForSecondsRealtime(.8f);
                Check(!surface.ShineActive, "Shine finishes instead of running a perpetual hover animation");
                int idlePaint = surface.PaintCount;
                yield return new WaitForSecondsRealtime(.3f);
                Check(surface.PaintCount == idlePaint, "Stationary hovered button keeps cached geometry after shine");
                var metal = shell.Root.Q("metalBackground").Q<RacingSurface>();
                int metalPaint = metal.PaintCount;
                yield return new WaitForSecondsRealtime(.3f);
                Check(metal.pickingMode == PickingMode.Ignore && metal.PaintCount == metalPaint,
                    "Static metal/noise backdrop ignores input and keeps cached geometry without ongoing repaint");
                input.MoveMouseAt(1100, 500);
                yield return new WaitForSecondsRealtime(.2f);
                yield return new WaitForSecondsRealtime(.7f);
                var originalFocus = shell.Root.focusController.focusedElement;
                yield return Key(input, 0x09);
                Check(shell.Root.focusController.focusedElement == originalFocus && !surface.ShineActive,
                    "Real Tab does not navigate or start focus shine");
                input.Key(0x10,true); yield return Key(input,0x09); input.Key(0x10,false);
                Check(shell.Root.focusController.focusedElement == originalFocus && game.ScreenName == "Garage",
                    "Real Shift+Tab does not navigate or change destination");
                int unfocusedPaint=surface.PaintCount;
                shell.Root.Q<Button>("navBuild").Focus(); yield return new WaitForSecondsRealtime(.15f);
                Check(!surface.ShineActive && surface.PaintCount==unfocusedPaint,
                    "Button focus does not add highlight, start shine or repaint decoration");
                var garageButton=shell.Root.Q<Button>("navGarage"); var garageRect=garageButton.worldBound;
                input.MoveMouseAt((int)garageRect.center.x,(int)garageRect.center.y);
                yield return new WaitForSecondsRealtime(.8f);
                var released=ScreenCapture.CaptureScreenshotAsTexture();
                float releasedLight=Pixel(released,garageRect.center.x,garageRect.yMax-8).grayscale;Destroy(released);
                input.MouseButtonAt((int)garageRect.center.x,(int)garageRect.center.y,true);
                yield return new WaitForSecondsRealtime(.15f);
                var down=ScreenCapture.CaptureScreenshotAsTexture();
                float downLight=Pixel(down,garageRect.center.x,garageRect.yMax-8).grayscale;Destroy(down);
                Check(downLight<releasedLight-.005f,"Mouse down retains visible pressed shading on the selected navigation button: "+releasedLight+" → "+downLight);
                Check(!garageButton.Q<RacingSurface>().ShineActive,"Mouse down does not restart finished hover shine");
                input.MouseButtonAt((int)garageRect.center.x,(int)garageRect.center.y,false);
                yield return new WaitForSecondsRealtime(.2f);
                var up=ScreenCapture.CaptureScreenshotAsTexture();
                Check(Mathf.Abs(Pixel(up,garageRect.center.x,garageRect.yMax-8).grayscale-releasedLight)<.015f,"Mouse up restores normal selected/hover shading without persistent focus highlight");Destroy(up);
                foreach (var destination in new[] { new[] {"navBuild", "Parts"}, new[] {"navTuning", "Tuning"}, new[] {"navRace", "Race setup"}, new[] {"navBusiness", "Business"}, new[] {"navData", "Stats"}, new[] {"navGarage", "Garage"} })
                {
                    yield return Click(input, destination[0]);
                    Check(shell.Visible && game.ScreenName == destination[1], "Native click selects " + destination[0] + " and stays New");
                }
                var settingsHit = shell.Root.Q("settingsButton").worldBound;
                input.MouseButtonAt((int)settingsHit.xMin+3,(int)settingsHit.yMin+8,true);
                yield return new WaitForSecondsRealtime(.08f);
                input.MouseButtonAt((int)settingsHit.xMin+3,(int)settingsHit.yMin+8,false);
                yield return new WaitForSecondsRealtime(.2f);
                Check(game.ScreenName == "Settings" && shell.Visible,"Settings retains its full-height hit area including the clear corner outside the slanted face");
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
                    if(DeterministicCapture.Active&&width==3840){report.AppendLine("LIMIT: oversized physical window is host-dependent; deterministic 4K coverage uses the fixed 3840x2160 render target below.");continue;}
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
                Check(shell.Root.Q<Label>("selectedCar") == null, "Sparse shell does not add unnecessary car identity text");
                game.Profile.cars = cars;
                shell.Refresh();
                yield return Capture("settings-restored");
                yield return SettingsChecks(input);
                if(game.Profile.firstPurchasePending){game.QueueNativeNavigation("Garage");yield return new WaitForSecondsRealtime(.4f);yield return StarterPurchaseChecks(input);}
                yield return OwnedCarChecks(input);
                yield return DealershipChecks(input);
                yield return UsedInspectionChecks(input);
            }
#else
            throw new InvalidOperationException("Native probe requires a Windows development player.");
#endif
        }
        static Color Pixel(Texture2D image,float x,float y) => image.GetPixel((int)x,image.height-(int)y-1);
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator Key(WindowsKeyboardProbe input, ushort key)
        { input.Key(key, true); yield return null; yield return new WaitForSecondsRealtime(.08f); input.Key(key, false); yield return null; yield return new WaitForSecondsRealtime(.2f); }
        IEnumerator Click(WindowsKeyboardProbe input, string id)
        { return Click(input,shell.Root.Q(id)); }
        IEnumerator Click(WindowsKeyboardProbe input, VisualElement target)
        {
            if(target==null)throw new InvalidOperationException("Native click target is missing.");
            // Let layout settle and deliver pointer movement separately from the press.
            yield return null;
            var rect = target.worldBound;
            float scale = NativeMenuShell.DisplayScale(Screen.width, Screen.height);
            input.MoveMouseAt((int)(rect.center.x * scale), (int)(rect.center.y * scale));
            yield return null;yield return new WaitForSecondsRealtime(.08f);
            rect=target.worldBound;
            input.MouseButtonAt((int)(rect.center.x * scale), (int)(rect.center.y * scale), true);
            // Keep down/up in separate input updates even during a slow preview frame.
            yield return null;
            yield return new WaitForSecondsRealtime(.08f);
            input.MouseButtonAt((int)(rect.center.x * scale), (int)(rect.center.y * scale), false);
            yield return null;
            yield return new WaitForSecondsRealtime(.2f);
        }
#endif
    }
}
