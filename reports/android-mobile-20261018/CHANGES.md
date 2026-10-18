# Android touch compatibility checkpoint — 2026-10-18

Scope: make the currently implemented game workflows usable on a phone, then deliver an Android APK. This is a requested platform checkpoint; it does not advance the deferred interface redesigns or alter vehicle physics/economy. Debug benches remain intact.

## Changes

- Native menus, pause and results use one mobile scale policy (960×540 logical baseline, including smaller phone surfaces), with shared fonts/vector surfaces and safe-area margins. Phone navigation uses filled icons at compact widths. Touch buttons, fields and slider hit areas are taller without changing the desktop window defaults or desktop scale policy.
- Tuning exposes shared − / + fine-increment and ↺ default actions on touch. Exact numeric fields request a mobile keyboard supporting decimal and negative numbers. Existing right-drag/middle-click and explicit Apply/Revert stay available on desktop. Fixed hardware disables the helpers as well as its slider/number field.
- Dyno plots accept touch-down/drag, show the same sampled values/intersection markers, and retain the reading after lift. Mouse hover behavior remains unchanged.
- A touch footer replaces the F8-only comparison hint. Legacy pages have a touch comparison action with existing text/gesture/modal guards. Business/Data default to their working legacy presentation on mobile because their replacement workspaces are unfinished. This makes the existing behavior reachable without implementing those later native pages.
- Android Back is handled within the game: text editing, modal dismissal, detail/library/cart Back, Settings Cancel and return to Garage. Back does not silently exit the application. Pause continues to use the existing contained dialog and settings drafts.
- Warm-up has a touch line-lock action for non-AWD cars and the existing Ready action. Pedals/shifting continue through the shared authoritative adapter. Safe insets cover the gauge cluster, pedals, shifter and upper race controls; the smallest notched layout received a positioning correction after an overlap was detected. New races release stale captured pedal fingers as well as resetting the adapter.
- Backgrounding from menus captures actual cart reservations and persists the profile; it does not publish unapplied tuning, settings, numeric edits or previews. Race suspension retains the established checkpoint/settlement path. Saves remain in Android's application data directory, with settings separate from progression.

## Validation

Final Windows-player checks pass with zero runtime errors: **108 mobile access**, **65 pedal/input**, **220 full dashboard/pause/results/finish**, and **111 navigation/maintenance summary** checks. These suites overlap; they are not 504 unique behaviors. The mobile matrix covers 854×480, 1280×720, 1920×1080 and 2400×1080 menu surfaces, three-pedal HUD clearance at 854/1280/1920 with simulated notch/gesture insets, real touch scrolling/sliders, fresh purchase, naming, fuel, cart persistence, fine/reset tuning, confirmed Settings, warm-up/Ready, held pedals/shifting and pause/quit. The legacy comparison return also uses a real pointer press/release matching the compatibility mouse path. Detailed touch/pedal/navigation results and concise dashboard measurements accompany this report.

The isolated paid-refresh race retained the existing performance bounds: maximum main step **36.635 ms**, maximum frame **42.518 ms**; practice maximum step **14.611 ms**. No timing assertions were relaxed. An earlier desktop probe missed a startup filter click; unchanged final pedal, dashboard and navigation fixtures all pass on their fresh reruns. Test profiles and captures are isolated from the actual player save. Intermediate failures are retained locally: a compiler namespace ambiguity and the legacy comparison button's press/release guard were fixed, and the synthetic scrolling fixture was corrected to use the primary finger expected by Unity rather than a secondary finger. See Unity's [touch event construction](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/UIElements/Core/Events/PointerEvents.cs) and [ScrollView input handling](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/UIElements/Core/Controls/ScrollView.cs). No custom replacement scrolling implementation was needed.

The automated touch suite uses native UI Toolkit touch events in a Windows development player with the mobile layout policy enabled. It covers the actual purchase/maintenance/tuning/preferences transactions and race controls; it is not an Android hardware test. Android IME appearance, finger reach, device-specific audio/performance and suspend/resume must be reviewed on the phone. There is no connected Android device/emulator. Developer CSV exports remain in application storage; this checkpoint does not add an Android share sheet or redesign the debug benches.

## Phone review and installation

Copy `Game/Builds/Android/DragLab.apk` to the phone and open it with its Files app. If requested, permit installation from that source. Final Android packaging, APK v2 signature, 16 KiB ZIP alignment, ZIP CRC and native-library checks pass. The Windows runtime remains byte-identical to the final passing suites. See [artifact hashes](build-manifest.txt). Build-only Unity asset serialization was checked, archived locally and restored.

This is a development/debug-signed ARM64 APK for Android 8.0/API 26 or newer, not a store upload. No phone is installed to or modified automatically.

Review first purchase, shop/cart and maintenance, finger scrolling, numeric/text keyboard entry, touch tuning/reset, warm-up and manual-clutch racing, pause/audio/quit, and returning after backgrounding. Check both notch and gesture-bar clearance. The phone creates its own save; no desktop progression is copied automatically. Technical completion and player/device acceptance remain separate.

## Repository scope

Pre-existing changes in `reports/maintenance-batch/verification.txt`, `reports/shop-inventory/cart-verification.txt`, `.codex-remote-attachments/`, `Game/Assets/Resources.meta`, `Tests.ps1` and `reports/race-exit-regression/` are preserved and excluded. Raw players, test saves, captures and logs stay local/ignored. No remote push or phone installation is performed. User acceptance is pending; stop at this platform checkpoint.
