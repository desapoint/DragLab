# 4L.8 — larger race scene and smaller gauges (2026-10-15)

Started on `main` at merged PR commit `96fa9b5`; no former feature branch was checked out. Keep the user's two report edits, untracked test script and race-exit diagnostics unchanged.

The race camera now magnifies the complete world by 15% and shifts its center down 0.75 world units, moving the road/cars/lights upward on screen. Road, markings, both cars, particles and the complete light/pole assembly scale together. Physics distances/timing, lane projection and the 82% distant-car relationship remain unchanged. Returning to Garage restores its original camera size/position, including an empty garage.

The dashboard, shifter, steering rim, text and controls retain their logical sizes. Only the speed/RPM dial diameters change: 226/254 → 204/228 points; compact 190/214 → 172/192. Number positions adapt through the existing gauge layout. No new themes or setting controls are introduced.

Visual review found a thin background gap after the upward reframing, so asphalt and start/finish ribbons now overscan under the dashboard. Existing lane-area asphalt UVs remain anchored; the visible track keeps its compressed ground perspective. The first compact test also prompted a small upward adjustment before the later review.

## Checks

Final `BuildPlayerOnly` succeeded (`reports/phases/4l/08-build-final.log`, local). Final packaged `RaceHudOnly` passed **141 checks / 0 runtime errors**; [named evidence](08-native.txt). Coverage includes camera framing and Garage restoration, asphalt under-dash coverage, retained narrow perspective and equal-distance finish alignment, checker proportions, light/pole anchoring, purchased shift lights, label/tick clearance, 1024×768 and 1920×1080 layouts, true 3840×2160 UI target, held input, contained pause and actual completed-race exit/settlement reload.

Reviewed final normal, compact and 1080p captures. Earlier compact failure was corrected by the vertical calibration; the intermediate 141/0 pass preceded the final asphalt-overlap correction and is retained locally rather than being substituted for final evidence. Full mechanics/economy matrices were not rerun for this visual-only change. Verified UTF-8/whitespace and restored only whitespace-equivalent Unity-generated font YAML changes. Pre-existing user report/test files were hash-checked unchanged.

## Review

Review larger cars and readable lights, raised road joining the dash without a gap, smaller readable gauges and normal Garage framing after leaving a race. Captures are local in `reports/player-fixtures/race-framing-4l08-overscan/` and the normal interface screenshot gallery; alternate sizes remain in `resolution-tests`. Technical checks do not imply user visual acceptance. Stop before the next control or phase.
