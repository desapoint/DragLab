# 4B.2 / 4C.0 — Main navigation and Settings shell (2026-10-09)

User-requested infrastructure checkpoint after `2bb104d`. **Ready for user review; 4B and 4C are not complete.** No individual preference controls, Save/confirmation flow, inspection drawer or replacement gameplay pages are implemented yet. Stop here for validation.

## Delivered

- Native Unity UI Toolkit shell with opaque full-window grey surfaces, small rounded bordered/shaded controls, red selection/primary emphasis, hover/pressed/focus feedback and filled procedural icons. The strip is covered in migrated menu presentation.
- Fixed full-width 64-logical-pixel top bar: Garage, Build, Tuning, Race, Business, Data; actual cash, selected-car fuel/tank and REP; Settings gear right-aligned with a full-height hit area. Inspection space is reserved immediately to its left without implementing a button/drawer or bench navigation.
- Working main navigation maps to current Garage/Parts/Tuning/Race setup/Business/Stats routes. It stays in New presentation and calls the existing draft-discard/tuning-entry boundaries; no second domain operation or save authority. Other workspaces are placeholders with F8 access to current functionality. Deeper category grouping is not finalized.
- Full-window Settings shell: Difficulty/Driving, Sound, Units, Controls and General tabs. Selected category persists through F8 and resize. No editing or fake Save button; the new draft/explicit Save/confirmation behavior remains for the next requested control. Current legacy settings keep their previous behavior.
- Session-only F8 comparison remains available. Native pointer gestures block comparison. Race/pause/settings/results and both development benches retain existing presentation and shared input/simulation behavior; no new race HUD or paused-race Settings migration.
- Normal logical sizes remain unchanged from 1280×720 to 1920×1080. Above the 1080p target, one shared scale uses the smaller width/height ratio (2× at 3840×2160). Compact layout hides the brand and reduces spacing without stretching icons. Windowed project defaults are unchanged.

## Framework and assets

UI Toolkit replaces the earlier unimplemented uGUI proposal for this checkpoint: built-in runtime support, flex layouts, rounded USS controls, TextCore text and procedural icon geometry avoid a bitmap-sized interface. Added only `com.unity.modules.uielements@1.0.0` via the Editor's asynchronous Package Manager API; Unity resolved its built-in UI/hierarchy/physics/animation dependencies. No downloaded icon/font pack or Input System migration.

`NativeShell.uxml` / `.uss` and project `NativeTheme.tss` are editable Resources assets. `NativeUiSetup.PrepareShell` creates/retains the stable PanelSettings asset; runtime clones the panel so display scaling and verification render targets never modify the asset. No scene YAML was edited. Unity's generated default-theme files were replaced by the project theme; all new assets have matching metadata. Panel/render API references: [Panel Settings](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Runtime-Panel-Settings.html), [Painter2D](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/UIElements.Painter2D.html).

## Checks actually run

- Unity 6000.6.4f1 import/compile and `DragLab.Editor.PlayerBuild.BuildPlayerOnly` passed; updated player at `Game/Builds/Windows/DragLab.exe`, with the complete build directory required.
- [Native shell verification](05-native-verification.txt): **89 checks, zero runtime errors**, actual OS keys/mouse clicks. Navigation, hover labels/color, pointer guard, category changes, Old/New restoration, unchanged profile/disk save, empty garage, same-route car identity updates, resize and fullscreen/windowed restoration. Launcher: `tools/Test-NativeMenu.ps1 -FixtureName <fresh-name>`; fixtures stay isolated from player progression.
- [Existing comparison regression](05-comparison-verification.txt): **48 checks, zero runtime errors**, including exact tune/cart state, text/reset guards, held W through race/bench comparison, hidden race pointer controls, Shift+F8 and physical recording remaining OFF. Its retained legacy labels say “empty replacement”; those assertions check presentation/route, while the new shell probe checks actual widgets.
- Native renders inspected at 1280×720, 1024×768 and 1920×1080; true 3840×2160 UI render target captured/inspected. Windows clamped the requested 4K window to **3360×2160**; that window layout passed, but an actual 3840-wide desktop window is not verified on this monitor. The offscreen capture verifies 4K rendering/scaling, not 4K OS input/display hardware.
- UTF-8, unique UXML control IDs, all new asset metadata, **327 unique asset GUIDs**, PowerShell parsing, local documentation links and Git staged whitespace checks. `Game/ProjectSettings` unchanged. Existing Unity deprecation/build warnings remain; no native USS/font/runtime errors in the probes.

Initial probe failed because the general runtime verifier ran concurrently; its attach guard now excludes the dedicated native-menu probe. The next probe exposed the bar's bottom border shortening the Settings hit area; fixed before the passing run. Native screenshot review caught footer overlap with Unity's development watermark; fixed. The CLI once returned early while its task-owned batch Editor continued; inspected its exact process/log and waited for successful completion. No incomplete attempt is counted as passing evidence.

Full Slice 2/3 physics/economy matrices, controller/device acceptance and performance budgets were not rerun/completed. This sparse shell is not evidence for final large-list/widget performance. Unusually narrow windows or very long localized/resource strings need later responsive coverage. Individual preference behavior, dialogs, keyboard bindings, drawer content and other screens remain pending.

## Review checklist

1. Launch the updated Windows player, or Play `FirstSlice` in Unity. Press **F8** in Garage to see the new shell. Hover/click the six destinations; inspect icon shape, grey/red emphasis and top-bar spacing.
2. Click the far-right **gear**. Try all five Settings tabs; press F8 twice and confirm the same category returns. Use Old settings when an actual preference needs changing.
3. Resize the window and try fullscreen/windowed mode. Check readability, resources, the full-height right-aligned gear and absence of the strip behind menus. Current launch size/mode remains unchanged.
4. Validate this shell and specify the first Settings control. No further control or phase starts automatically.

Local screenshot evidence: `reports/player-fixtures/native-menu-shell-final/` (ignored raw captures). Human interface acceptance is pending.
