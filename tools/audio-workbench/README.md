# DragLab Audio Workbench — Drivetrain and BOV fixes

## Unity reference extension

The supplied archive is preserved in local commit `40d475f`. This working copy
now adds **Unity reference replay** with exact game snapshots and event timing,
editable browser acoustics, a separate authoritative Unity WAV player and
reference-based A/B/WAV export. Game audio and physics are unchanged.

Run `./tools/Export-UnityAudioReference.ps1` from the repository root, then open
`reports/unity-audio-reference-20261021/index.html`. That generated page preloads
the first real game configuration; this source HTML also supports manual trace
and WAV import. See [the trace contract and verification workflow](../../docs/UNITY_AUDIO_REFERENCE.md).

The earlier documentation below describes the supplied **free simulation**
baseline. In Unity-reference mode manual input/probability triggers are disabled.
Browser sound still differs from production DSP; exact timing is the first gate.

**Open:** `DragLab_Audio_Workbench_DrivetrainFix.html`, then click **Start audio**. The HTML is fully standalone; companion DSP JS and test files are for inspection. This changes only the browser workbench, **not the DragLab Unity repository**.

## Changes

- **Throttle and RPM:** the simplified driving model no longer applies positive wheel torque at zero pedal; off-throttle engine braking and free-rev return toward idle reduce RPM. Manual pedal input cancels the running scripted sequence. **Release pedal** resets the latched slider to 0%. The W keyboard key is momentary, and loss of keyboard focus releases W/L/B.
- **Feedback:** live telemetry shows the actual pedal command, the effective throttle plate, whether the script is active, turbo spool percentage, manifold boost pressure, and current / maximum gear.
- **Transmission:** all starter profiles default to six forward gears; `forwardGears` is editable from 3 to 8 under **Engine geometry**. Pressing Q in top gear, or during an active shift, does not trigger another shift or its associated audio. Gear ratios remain generic approximations.
- **Turbo and BOV:** browser physics now distinguishes compressor spool inertia from intake-manifold boost. Pressure drops quickly when the throttle plate closes; turbo spool decays more slowly. BOV triggers on a meaningful rapid throttle-plate closure *with available boost*, not simply every time a gear is selected. Manual BOV remains an independent sound-test trigger.
- **Shift control:** **TorqueCut** (default) keeps the throttle plate open, often retains boost and does not force a BOV; **ThrottleLift** closes the plate briefly, drops manifold pressure and can produce a BOV sound if boosted. Combustion remains audible throughout both types of shift. Probabilistic shift afterfire now requires high-load ignition-cut conditions.
- Earlier limiter, shift-audio, independent tire-spin, and physical-geometry preset fixes remain present.

## Test procedure

1. Start audio, select **Race drive**, hold W, then release W. Observe pedal command return to 0 and vehicle speed/RPM fall. The throttle slider is a *latched* manual control; click **Release pedal** if it was set above zero.
2. Upshift to 6 / 6 using Q. Another Q should not trigger gear-shift/BOV sounds.
3. With Tempest and boost present, compare **Race & controls → Shift control → TorqueCut** and **ThrottleLift**. The former should generally retain boost without a BOV; the latter should vent manifold boost while turbo spool remains and may produce a BOV.
4. Run the full scripted sequence, then manually change throttle; the script should relinquish control immediately.

## Validation commands

Run `node verify_drivetrain_lab.cjs`, `node verify_drivetrain_limiter.cjs`, `node verify_drivetrain_shift.cjs`, `node verify_drivetrain_tires.cjs`. Optional `python verify_drivetrain_browser.py` requires Playwright and a local Chromium binary.

The browser's RPM, wheel torque, clutch coupling, manifold pressure and afterfire scheduling remain *approximations*; the updated game-mapping export includes preview gear and shift-strategy settings but does not apply them in Unity. This generator is based on repository `desapoint/DragLab` `main` commit `9fd56eb` (2026-10-21). Older saved bench presets import with default `forwardGears=6` and `shiftStrategy=TorqueCut`.
