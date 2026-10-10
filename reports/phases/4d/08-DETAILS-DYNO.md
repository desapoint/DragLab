# 4D.5 — Owned-car details dyno (2026-10-10)

- Add the dyno to the right of the first details content row, beside layered preview and HP/torque/weight/condition facts. Preserve the existing native theme, concise labels and maintenance below.
- Display cached HP and torque versus RPM, separate labelled scales, colored peak figures with peak-RPM tooltips, and a dashed no-wear reference only when condition changes output. Respect the saved Nm/lb-ft preference.
- Resolve the inspected actual instance through InspectionBuild/CarInspection, using applied hardware/tune/part condition and stored fluids. Curves estimate full-load steady boost; they are not live wheel measurements or a race recording. The tooltip explains the estimate and comparison.
- Calculate curves once per rebuilt details view using CalculatePrepared with runProbe=false. Do not drive a simulation, consume resources, heal condition, fabricate hardware, save or switch selected car. The no-wear reference uses copied components; fluid state/missing hardware remain.
- Render chart strokes as native vector geometry and labels with the existing font for crisp resolution scaling. Missing engines show a concise unavailable state rather than a fabricated curve. Existing Back/F8/naming/services/confirmation remain.

## Verification

- Windows development build passed (Unity 6000.6.4f1, `Invoke-Unity.ps1 -Action BuildPlayerOnly`). The initial compile exposed a missing namespace import for shared units; it was corrected before the final build.
- Native player probe passed **345 checks / 0 runtime errors**, including source-accurate applied curves, a no-wear reference without healing, right-side placement, cached inspection, compact/1080p layout, existing naming/maintenance/payment rollback and F8 guards. Detached-engine details expose no dyno curve. Evidence: `08-native-verification.txt`.
- Reviewed actual 1024×768, 1280×720 and 1920×1080 detail captures. Curve strokes and tick/peak labels fit on the right; maintenance stays below with existing scrolling. The existing 4K settings/grid render-target gates passed; no dedicated 4K dyno capture or physical display review was performed.
- UTF-8, new script GUID uniqueness and whitespace checks passed. Build-only font serialization whitespace normalized; raw builds/logs/captures remain ignored. Historical full mechanics/economy matrices and the separate comparison probe were retained rather than rerun. This is an inspection presentation change; no domain equations changed.
- Technical verification is complete; user visual acceptance remains pending.

## Review

Open a worn car and compare solid versus dashed output. Review first-row layout at current window size and 1080p; inspect a different owned build and a detached-engine chassis. Confirm maintenance still fits below and scrolls; use F8/Back. Stop here for user validation; full inspection/drawer and other Garage actions remain separate controls.
