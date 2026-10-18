# Mobile header/footer edges - 2026-10-18

Requested checkpoint: extend the non-interactive header and footer artwork into the phone status-bar/cutout/gesture margins.

The shared safe-area fit now extends only their existing RacingSurface decorations left/right and outward to the top/bottom display edges. Mobile header overflow permits that drawing. Navigation buttons, resource labels, content and other controls retain their safe-area layout. The surfaces ignore picking. Desktop fitting restores the original zero horizontal outsets, header top and footer -8-point trim. Existing full-screen metal background and modal dimming remain intact.

## Checks and review

Packaged mobile suite: 157 checks, zero runtime errors. Four menu resolutions (854, 1280, 1920 and 2400 wide) check full header/footer bounds, ignored decoration picking and reachable navigation; existing five-resolution race layout, touch inputs, pause, Apply/Cancel and edge-dimming checks also pass. The 1280 capture was visually reviewed. Raw screenshots, logs and isolated saves remain local/ignored.

Desktop navigation: 111 checks, zero runtime errors. Windows and ARM64 Android builds succeed. APK signing, ZIP/ELF alignment, CRC and SQLite checks pass; the tested Windows runtime is unchanged by Android packaging. See [suite summary](verification-summary.txt), [build hashes](build-manifest.txt) and [packaging evidence](packaging-verification.txt). Updated local artifacts are `Game/Builds/Windows/DragLab.exe` and `Game/Builds/Android/DragLab.apk`. Verified build-only Unity serialization was archived locally and restored. Phone visual acceptance remains pending without a connected device. Review continuous header/footer textures beside the notch and gesture area in landscape; Settings/navigation must remain reachable. No later UI phase is advanced.

Pre-existing changes in reports/maintenance-batch/verification.txt, reports/shop-inventory/cart-verification.txt, .codex-remote-attachments/, Game/Assets/Resources.meta, Tests.ps1 and reports/race-exit-regression/ remain excluded and preserved.
