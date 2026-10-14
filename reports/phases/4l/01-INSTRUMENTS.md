# 4L.1 — speed, RPM and shifter display (2026-10-14)

The user authorized the first race instrument checkpoint and documentation of the complete future gauge direction. Two vector analog dials sit on the left with 270° sweeps; road speed follows the applied speed unit and RPM has a distinct safe redline band. The shifter-shaped display sits on the right and reads the actual engaged gear, N in neutral and D for CVT. The RPM dial contains no recommended shift marker. Its exact RPM readout is separate from the ×1000 tick multiplier.

Gauge scale/sweep, stationary face, moving needle, labels and telemetry are separated. Shared NativePalette tokens provide the accent/text, with gauge face/rim/tick tokens in NativeRaceHud.uss. The same renderer accepts a 180° sweep; no style-selector menu or digital renderer is delivered. Only changed numeric text/needles update on each frame; scale and safe redline are captured when binding a different race car or applied speed unit. No car inspection, benchmark, database query or save is added to display updates. Over-range needles clamp while exact readouts remain truthful.

The native overlay is transparent to pointer controls and defaults on for driving. F8 restores the original whole interface on the same live race. Existing pedals/shift buttons, warm-up, pause and results remain their established UI until separately requested. Instrument anchors account for the existing controls' different scaling. A concise phase/distance line preserves staging information; concurrent essential failures remain visible together. Shared physics, input/shift timing, hardware permissions, fluids/wear, persistence and payout authorities are unchanged.

[Full approved decisions](../../../docs/RACE_INSTRUMENTS.md) record later analog/digital/eight-segment themes, hardware-based single/multiple shift lights, fuel, optional thermal/oil detail and oil-pressure gauges. These are not silently implemented with this checkpoint.

## Checks

Windows BuildPlayerOnly passed. Final packaged gauge suite: **59 checks / 0 runtime errors** ([evidence](01-native.txt)); Race selection regression: **49 / 0** ([evidence](01-race-selection.txt)). These overlap starter/shared checks and are not a summed unique total. The gauge suite covers exact authoritative readings; safe redline/no shift target; neutral/manual/DCT/automatic/CVT presentation; all speed units; the same renderer with a semicircle; separate fixed-face/needle repainting; concurrent faults; compact/1080p/true-4K bounds; read-only player/database state; F8; real held W, E launch and keyboard/mouse shifts with throttle assist disabled; pause/resume; leaving; and a full physical practice pass through results. Asset metadata has no missing entries or duplicated GUIDs; edited UTF-8 and Git whitespace checks pass. Earlier Slice 2/3 domain matrices remain historical baselines, not rerun by this presentation checkpoint. No controller/Android/audio-listening validation is claimed. Compact/windowed and 1080p use actual Windows frames; 4K uses a true 3840×2160 UI render target, not a 4K physical monitor test.

A diagnostic test attempted a mouse upshift before the previous mechanical shift interval had ended. The fixture now waits for the real gearbox state and disables throttle assist during held-key checks; the simulation timing was not relaxed. The initial sandboxed build could not reach Unity licensing; subsequent builds use the established licensed host execution.

## Review checklist

- Start a race: check dial size/left placement, right shifter, neutral idle and live needle/readouts.
- Check the RPM red band and the absence of a suggested shift tick; changing speed units should match both dial and numeric units.
- Hold W or Space while using E/Q or the original mouse shift controls; compare with F8, pause/resume and finish a pass.
- Review compact and 1080p screenshots plus the true 4K UI capture. The semicircle screenshot demonstrates the configurable sweep, not a new selectable player option.

Review captures: reports/player-fixtures/race-gauges-verified/ and reports/interface-screenshots/ (alternate resolutions in resolution-tests). Rendered normal/compact/1080p/sweep frames were inspected.

Build: Game/Builds/Windows/DragLab.exe and its adjacent data/plugins. Technical completion does not imply user visual acceptance. Stop here before another race control.
