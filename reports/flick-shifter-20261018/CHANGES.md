# Configurable mobile flick shifting — 2026-10-18

Requested checkpoint: replace mobile up/down buttons with a flickable shifter, allow reversed directions, and make desktop pedals visibly follow physical controls. No later interface phase is advanced. Debug benches and vehicle physics remain intact.

## Behavior and decisions

- The native mobile shifter uses its existing full hit area. Flick up to engage/upshift and down to downshift by default. Clear vertical travel emits one command per contact; holding or continuing a swipe never repeats it. Taps, tiny movements, horizontal swipes and additional fingers on the same shifter do not shift. Independent clutch/brake/gas fingers remain active.
- Settings → Controls → Touch shifting uses the shared joined choice for Up → upshift / Down → upshift. Edits stay drafts until Apply and confirmation. Cancel discards them. Pause → Controls explains the currently applied direction; pause's restricted settings scope remains unchanged.
- Direction is an optional presentation field in the existing separate driving-preferences file. Old preferences default to up-to-upshift. Existing preference writes preserve it, and the existing recoverable settings transaction commits it together with sound/units. The applied value is cached and published only after success. It is outside progression and recorded simulation driver fields, so no physics/replay/schema version change is needed. Progression reset retains the preferences file.
- Flicks use the existing authoritative race input adapter. Manual clutch requirements, neutral engagement, gearbox timing/protection, hardware-specific selector motion and CVT Drive/restart behavior stay authoritative. Interruption, F8, new race and teardown release gesture/pedal captures. Mobile shift buttons are hidden; desktop controls remain available.
- Desktop keyboard input already reached the pedal renderer. The requested refinement increases its smooth face travel/foreshortening so pressing and releasing gas, brake and clutch is more visible. Actual keyboard presses and held throttle through a shift are verified. Pedal requirement/visibility and distinct metal/rubber faces remain unchanged.

## Verification

The dedicated Windows-player flick suite covers default/reversed gestures, simultaneous pedal fingers, one shift per contact, cancellation/extra fingers, interruption/F8 cleanup, real keyboard press/release, real clutch-gated shifts and CVT behavior. Preference checks cover draft/Cancel, locked-file failure without publication, recovery, confirmed Apply, reload, ordinary preference writes and unchanged progression/recorded driver fields. The wider mobile suite now shifts through the actual flick surface rather than clicking a hidden mobile button.

Packaged Windows checks pass: **70 flick**, **107 mobile**, **65 pedal/input**, **148 settings/persistence**, all with zero runtime errors. These suites overlap. Flick/mobile checks were rerun after the final hint placement; pedal/input and settings checks were retained from the preceding build with identical runtime code and only that hint's USS placement changed afterward. The earlier 220-check dashboard and 111-check navigation suites remain the previous checkpoint's historical baseline, not new reruns.

Final build/test results and artifact hashes are recorded alongside this report. Raw logs, isolated saves and screenshots remain local/ignored. The normal-resolution review captures are directly in `reports/interface-screenshots/`, with other resolutions in its `resolution-tests/` subfolder; each suite refreshes its own images through the existing test harness. Screenshot review centered the small flick hint beside the shifter, clear of the build label, before the final rebuild.

Windows and Android builds succeed. The Android development/debug-signed ARM64/IL2CPP APK targets Android 8.0/API 26 or newer. APK v2 signature, 16 KiB ZIP alignment, ZIP CRC, native architecture and all ELF load-segment alignment checks pass; packaged SQLite matches the tracked plugin after normal stripping. The tested Windows runtime is unchanged by Android packaging. There is no attached device/emulator. See [artifact hashes](build-manifest.txt), [packaging evidence](packaging-verification.txt), [suite summary](verification-summary.txt) and [flick assertions](flick-verification.txt). Build-only Unity asset serialization was verified, archived locally and restored; unrelated starting files retain their original hashes.

## Review and limits

Test on the phone: flick both ways while holding gas/clutch, check one shift per swipe, reverse direction in Settings and confirm Apply, then check Cancel leaves the old direction. Review pedal reach, notch/gesture-bar clearance and press/release motion. On desktop, W/Space, S and Left Shift should visibly depress their respective pedals, with clutch shown only when required.

The touch automation runs native UI Toolkit touch events in a Windows development player with mobile layout enabled. It is not a physical Android test. Device feel, touch-driver behavior and player acceptance remain pending. Updated builds are local under `Game/Builds/Windows/DragLab.exe` and `Game/Builds/Android/DragLab.apk`; the APK is for direct phone testing, not store publication. Stop at this requested checkpoint.

## Repository scope

Pre-existing changes in `reports/maintenance-batch/verification.txt`, `reports/shop-inventory/cart-verification.txt`, `.codex-remote-attachments/`, `Game/Assets/Resources.meta`, `Tests.ps1` and `reports/race-exit-regression/` are preserved and excluded. Build-only Unity serialization is checked and restored separately. No push or phone installation is performed.
