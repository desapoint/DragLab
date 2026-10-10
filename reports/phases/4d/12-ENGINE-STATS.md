# 4D.6 — Stats button and collapsible Engine group (2026-10-10)

- Add a filled-chart Stats button beside the applied car summary. It reveals a read-only Engine group inline, before quick maintenance; no new page/dialog is used for the group. The group header collapses rows with a filled disclosure arrow. Visibility is remembered per owned instance, with group collapse state kept in the UI session.
- Render 20 shared definitions: power/torque and their peak RPM; cylinders, layout, displacement and effective fueling; idle/redline; installed effective limiter style/target/cut; authored bore/valves per cylinder; cam lift, duration, overlap, lobe separation and applied timing. No engine means a disabled Stats button, with no fabricated engine data. Unspecified dimensions/valve count, absent limiter targets and zero-output peak locations remain unavailable.
- Reuse the exact cached applied dyno inspection. Build values once per existing detail refresh, not every frame or gesture. Numeric rows use neutral display-range bars; the shared info control opens the existing themed modal for explanations and explicitly distinguishes display ranges from operating safety limits.
- Create a native catalogue/snapshot/row/group renderer accepting optional proposed inspections. Comparisons use the same definition IDs, formats, units and ranges; numeric changes get favorable/drawback/tradeoff labels and delta segments. More timing/displacement/RPM is not automatically better. Only renderer reuse is exercised in developer fixtures; no later parts/cart/tuning page is migrated.
- Preserve F8, Settings return, names, current selection, dyno layout, paid maintenance, exact part identity/condition/resources, no focus decoration/Tab traversal and shared hover/mouse-down surfaces. Detached surfaces are removed from the shell's tracking list on detail rebuild.

## Verification

- Final Windows development build passed with Unity 6000.6.4f1; executable/supporting files updated.
- Initial native probe: 381 checks / 0 runtime errors. Final probe: **385 checks / 0 runtime errors**. Evidence: `12-native-verification.txt`. The 24 additional assertions cover cached applied inspection, read-only rows, condition-aware power/units/configuration, effective limiter, shared explanation modal and Escape/F8 guards, collapse/Settings retention, comparison units/favorable/tradeoff bars, missing metadata/limiter, no profile/save side effects, compact/1080p bounds and detached-engine handling. Existing native Settings, dyno, naming, selection, maintenance and failed-save checks remain passing.
- Reviewed actual expanded/stat-scrolled captures at 1280×720, 1024×768 and 1920×1080: readable two-column rows, matching units, shared info controls, neutral range bars and reachable actions. The existing 4K settings/grid render-target gates passed; dedicated 4K Engine capture/physical display review and device acceptance remain open.
- UTF-8 and staged whitespace checks passed. Build-only font serialization whitespace normalized. Raw captures/builds/logs stay ignored. Full historical physics/economy gates are retained; this read-only UI checkpoint changes no simulation equations, prices or saves. Technical completion does not imply human visual acceptance.

## Review

Garage → owned car → Stats: review summary placement, Engine labels/units/bars, collapse/expand, per-row explanations and scrolling at compact/current/1080p sizes. Compare the output figures with the existing graph, then test F8 and Settings Cancel. Stop before the next stat group.
