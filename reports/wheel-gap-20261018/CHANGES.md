# Wheel peak alignment — 2026-10-18

Requested visual checkpoint: move the wheel left so its highest point sits directly between the speed and RPM gauges.

The gauges have unequal diameters, so the cluster center is not the gap midpoint. The rim now follows the midpoint between the actual facing dial bounds, converted into rim-local coordinates. Geometry callbacks update it when layout changes; normal HUD refresh retains alignment. This applies to desktop and mobile. Gauge positions, dashboard bump, wheel height, radius and thickness remain unchanged. No later UI phase is advanced.

## Verification

Packaged Windows suites pass **145 mobile / 222 desktop HUD**, with zero runtime errors. See [the concise assertions](verification-summary.txt). Mobile captures at 854, 1280, 1920, 2400 and 3840 pixels wide verify the gap alignment with simulated safe-area insets. Desktop checks cover compact and 1080p layouts. Actual 1280-pixel mobile and desktop captures are visually reviewed. Raw captures, isolated saves and build logs remain local and ignored.

Windows and ARM64 Android builds succeed. APK v2 signing, ZIP/ELF 16 KiB alignment, CRC and packaged SQLite checks pass; the tested Windows runtime remains unchanged after Android packaging. See [build hashes](build-manifest.txt) and [packaging checks](packaging-verification.txt). Updated local artifacts are `Game/Builds/Windows/DragLab.exe` and `Game/Builds/Android/DragLab.apk`. Verified build-only Unity serialization was archived locally and restored.

Phone visual acceptance remains pending; simulated mobile checks do not replace an Android device test. Review that the wheel peak is centered in the gap in landscape, including with a cutout. Stop at this requested adjustment.

Pre-existing changes in reports/maintenance-batch/verification.txt, reports/shop-inventory/cart-verification.txt, .codex-remote-attachments/, Game/Assets/Resources.meta, Tests.ps1 and reports/race-exit-regression/ are excluded and preserved.
