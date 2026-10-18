# Mobile display edges and native-only presentation — 2026-10-18

Requested checkpoint: cover the exposed status-bar/safe-area strip and remove mobile access to the old interface. Desktop comparison and debug benches remain available in their existing roles. No later interface page is implemented.

## Fix and decisions

The Android player already starts fullscreen and renders outside the safe area. `NativeMobile.Fit` applied safe-area margins to the whole menu root, including its background. Consequently the 3D scene/clear color was visible around that inset root; the previous mobile captures show this at the sides, top and bottom. The fix extends the existing decorative metal layer back out to all four display edges. Navigation, inputs and other controls retain their safe-area margins.

Dialog dimming also reaches every screen edge. Its content retains the same safe-area padding, so the card, close action and inputs stay clear of cutouts/gesture areas. Pause continues to show the race behind its shared dialog rather than painting an opaque menu background. Insets update through the shared fit path during resizing/safe-area changes. No Android permission or persistent save/settings format change is needed.

Mobile has no Old / New button in the native footer or legacy debug overlay, no F8 hint, and no functioning interface switch through an external keyboard or presentation request. Main menu, race and results routes stay native. The former automatic Business/Data fallback is removed: those entries now stay in their existing unfinished native workspaces. This intentionally does not implement Business/Data redesigns. Desktop F8 and the original interface are retained. The footer returns to its normal height after removing the touch switch. Project instructions/design decisions now record the platform distinction.

## Verification

Expanded mobile checks retain starter purchase, car naming/dyno, fluid purchase, cart reservation persistence, tuning draft/fine/reset, Settings Apply/Cancel, phone-size/safe-area layout, centered instruments, independent pedals, flick shifting and pause/quit. Added tests cover full-viewport background geometry, rendered corner/edge pixels over a temporary magenta underlay, edge dimming under a modal, card containment, absence of the mobile switch/hints, rejected legacy selection and ignored external F8. The sentinel is removed afterward; normal captures contain only production UI.

Final packaged Windows suites pass **145 mobile**, **85 gestures/physical holds**, **111 desktop navigation** and **148 Settings/persistence** checks, all with zero runtime errors. These suites overlap. Edge geometry is checked at 854×480, 1280×720, 1920×1080 and 2400×1080; the mobile dashboard retains its five-resolution matrix through 4K. Desktop navigation checks exercise real F8 switches, and Settings retains its compact/4K layout, confirmation and recovery checks. The prior 65 pedal and 220 full dashboard/race suites remain the preceding checkpoint's baseline rather than new reruns.

An initial test queried the comparison row's base name instead of its generated Row-suffixed name; the fixture was corrected and mobile hiding was applied to the complete row before the final passes. The failing isolated run remains local. The production player logged zero runtime errors in the final suites.

Exact results and build hashes accompany this report. Captures, logs and isolated profiles remain local/ignored under `reports/player-fixtures/` and the current `reports/interface-screenshots/` gallery, whose alternate-resolution images remain in `resolution-tests/`.

Windows and Android builds succeed. The development/debug-signed ARM64/IL2CPP APK supports Android 8.0/API 26 or newer. APK v2 signature, 16 KiB ZIP alignment, CRC, native architecture, all ELF load-segment alignment checks and stripped SQLite plugin equality pass. The tested Windows runtime is unchanged by Android packaging. No phone/emulator is attached. See [build hashes](build-manifest.txt), [packaging evidence](packaging-verification.txt), [suite summary](verification-summary.txt) and [mobile assertions](mobile-verification.txt). Verified build-only Unity serialization was archived locally and restored; pre-existing files retain their original hashes.

## Phone review and scope

Review the former status-bar/notch edge in Garage, Settings, shop and an open dialog; the screen should remain covered while controls clear the cutout. Check gesture-bar clearance, pause over the race, background/resume and both landscape safe-area orientations supported by the phone. Confirm there is no Old / New control. Physical Android review remains pending without an attached phone; Windows simulated-mobile coverage is not a device test.

Updated local artifacts are `Game/Builds/Windows/DragLab.exe` and `Game/Builds/Android/DragLab.apk`. Stop at this checkpoint; new Business/Data content and other UI phases remain deferred. No push, store publication or device installation is performed.

Pre-existing changes in `reports/maintenance-batch/verification.txt`, `reports/shop-inventory/cart-verification.txt`, `.codex-remote-attachments/`, `Game/Assets/Resources.meta`, `Tests.ps1` and `reports/race-exit-regression/` remain preserved and excluded. Verified build-only Unity serialization is restored separately. Raw builds and images remain ignored.
