# Floor-mounted pedals / original visual reference - 2026-10-18

Requested checkpoint: fix each pedal to the floor so its top recedes on press, remove the white/red strip, and use the original cleaner pedal appearance as the visual reference.

The shared renderer now pivots around a fixed heel at the bottom. Increasing press tilts the top down/away with perspective and eased visual travel. A static dark mounting foot replaces the overhead stalk and hinge. Broad brake/clutch pads and a narrow accelerator restore the clean rounded metal-frame/dark-rubber look, with a subtle taper, quieter shading and thinner grooves. The etched edge marks and white/red pressed strip are removed. Touch rectangles, feathering direction, full-press contact behavior and independent fingers/keyboard input remain unchanged; this checkpoint does not change driving semantics or simulation.

## Checks and phone review

Packaged Windows suites pass **65 pedal input / 157 mobile / 85 keyboard-flick**, zero runtime errors. See [the concise results](verification-summary.txt). Mobile race layout coverage extends through 4K. Released, pressed and half-clutch captures are retained locally; desktop released/full and mobile full-press poses were reviewed. Pixel comparison of the frozen desktop captures finds zero channel difference in the fixed clutch/accelerator base strips, and zero bright red indicator pixels in the pedal region. Raw captures, logs and isolated saves remain local/ignored. The full dashboard/finish suite remains the preceding 222-check baseline.

Windows and ARM64 Android builds succeed. APK signature, ZIP/ELF 16 KiB alignment, CRC and SQLite plugin checks pass; the tested Windows runtime is unchanged by Android packaging. See [build hashes](build-manifest.txt) and [packaging evidence](packaging-verification.txt). Updated local artifacts: `Game/Builds/Windows/DragLab.exe` and `Game/Builds/Android/DragLab.apk`. Verified build-only Unity serialization was archived locally and restored.

On a phone, review the original-style material contrast and floor-fixed heel through full and partial travel; the top should move down and away as input increases. Keep gas held while flicking the shifter. Physical phone acceptance remains pending without an attached device. Stop at this requested revision; no later UI phase is advanced.

Pre-existing reports/maintenance-batch/verification.txt, reports/shop-inventory/cart-verification.txt, .codex-remote-attachments/, Game/Assets/Resources.meta, Tests.ps1 and reports/race-exit-regression/ remain preserved and excluded.
