# 4D.5 — Shared dyno infrastructure (2026-10-10)

- Extract the native chart, peak legends, unit display, cursor line, intersection dots and readout into `NativeDynoPanel`. Inputs are labelled caller-supplied `CarInspection` snapshots with stable series IDs and presentation styles. The shared element has no profile capture, simulation, ownership, transaction or save authority.
- Garage captures its inspected applied build and copied no-wear reference exactly as before, supplying Current/No wear labels to the shared element. Preserve missing-engine handling, existing semantic IDs, red/amber solid applied curves, blue/mint dashed reference curves, peak figures, plot alignment and cached refresh.
- Support a separate proposed series with shared preview palette variables and short dashes. All displayed builds share RPM/HP/torque scales, including a proposed build with a higher redline. Every hover sample uses the same RPM and selected torque units; below-range outputs remain unavailable. Size/clamp the hover card using its measured layout, including wrapped labels.
- Record the approved full grouped-stat/comparison direction in the objective, phase plan and native design contract. This is the first infrastructure checkpoint only. The Stats button, collapsible Engine group, remaining groups and new parts/cart/tuning pages are not implemented here. Existing legacy comparisons remain available through F8.

## Verification

- Final Windows development build passed using Unity 6000.6.4f1; local executable/supporting files updated. The initial sandbox build stalled during Unity licensing initialization; only that task-owned batch process was stopped, then the build passed with normal local process access.
- Initial and final native probes each passed **361 checks / 0 runtime errors**. The final fixture explicitly compares the applied 7,000-RPM build with a separate 7,200-RPM build. Evidence: `11-native-verification.txt`.
- Eight added native assertions exercise independent supplied-series lists, caller-owned labels, scales covering all builds, lb-ft conversion, synchronized three-series pointer readouts, distinct proposed colors, no focus/profile/save side effects, and pointer-leave cleanup. Existing settings, resize, F8, applied inspection, names, maintenance, failed-save rollback and exact ownership checks are retained in the same probe.
- Reviewed Garage hover captures at 1024×768, 1280×720 and 1920×1080, plus the final three-series developer capture at 1280×720. Garage retains its prior layout/readout. Three-series legends may wrap in the narrow Garage-sized fixture; future comparison pages must choose suitable width/reflow during their own UI review. This fixture is developer-only and is not a new player comparison control.
- Existing 4K settings/grid render-target checks run in the native suite. No dedicated physical 4K dyno hover review or full historical mechanics/economy matrix rerun in this UI-only extraction.
- UTF-8 and whitespace checks passed. Build-only font trailing-space serialization noise normalized. Raw builds/logs/captures remain local and ignored. Human acceptance remains separate from technical checks.

## Review checkpoint

Open Garage → an owned car. Compare Current/No wear peaks, curves and alignment; hover across the graph and leave it. Check Back, maintenance scrolling and F8 at current/compact/1080p sizes. Stop here before adding the Stats button and first collapsible Engine group.
