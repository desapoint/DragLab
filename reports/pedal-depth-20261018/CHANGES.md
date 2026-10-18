# Shaped pedals and hinge depth - 2026-10-18

Requested visual checkpoint: replace rectangular, flat pedal faces with shaped/material shading and pressing that feels three-dimensional.

Clutch/brake now have distinct curved shoulder and heel profiles; the accelerator has a longer tapered profile. Layered steel rims, shaded rubber inserts, raised grip ribs, etched edge detail and hinge hardware replace the old flat rectangular pads. Cached material gradients move with the projected surface. All are antialiased vector drawings, including at 4K.

The artwork rotates about a fixed upper hinge from 18 degrees toward the viewer to 32 degrees away, with depth projection and a cast shadow. The lower face moves along that arc, foreshortens and narrows as it recedes; this replaces downward translation/height scaling. Existing press/release timing, authoritative input values and the fixed generous touch rectangles remain unchanged. Held throttle, multiple fingers, clutch visibility, feathering and desktop keyboard animation retain their existing behavior. No simulation, save, mechanical or further UI changes are included.

## Verification and review

Packaged Windows suites pass **65 pedal input / 157 mobile / 85 keyboard-flick**, zero runtime errors. See [the concise results](verification-summary.txt). Visually reviewed desktop and mobile captures include released and full-press poses; a half-clutch capture was also reviewed. Existing mobile coverage checks five race resolutions through 4K, including safe-area clearance. The full dashboard/finish suite remains the preceding [222-check baseline](../wheel-gap-20261018/CHANGES.md); this drawing checkpoint reruns the focused pedal/mobile/keyboard suites. Raw captures, logs and isolated saves remain local and ignored.

Windows and ARM64 Android builds succeed. APK signature, ZIP/ELF 16 KiB alignment, CRC and packaged SQLite checks pass. The tested Windows runtime is unchanged after Android packaging. See [build hashes](build-manifest.txt) and [packaging evidence](packaging-verification.txt). Updated local artifacts are `Game/Builds/Windows/DragLab.exe` and `Game/Builds/Android/DragLab.apk`. Verified build-only Unity serialization was archived locally and restored.

Phone visual acceptance remains pending without a connected Android device. Review resting profiles, material contrast, press/release depth and partial clutch travel; keep gas held while flicking the shifter. Stop at this requested pedal refinement.

Pre-existing reports/maintenance-batch/verification.txt, reports/shop-inventory/cart-verification.txt, .codex-remote-attachments/, Game/Assets/Resources.meta, Tests.ps1 and reports/race-exit-regression/ remain excluded and preserved.
