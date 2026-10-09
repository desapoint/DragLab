# Torque units and visible tire setup — 2026-10-08

Settings now offers Nm / lb-ft alongside speed and pressure. The preference is shared across garage/shop stats, component details, warnings, race telemetry, both benches and dyno graphs. Editable torque fields and torque-curve points display the chosen unit and convert edits back to SI. Existing unit files default to Nm; changing speed/pressure through older callers retains torque selection. Progression reset keeps the preference. Physics, asset authoring and CSV/replay data retain Nm.

Layered car views use the resolved tire outer radius to resize wheels and raise the body/axle centers relative to the ground shadow. This applies to garage and part/tune previews, saved racing builds, opponents and bench configurations. Brake discs and calipers retain their own scale; rotors keep rotating with wheels and calipers stay fixed. Empty brake/wheel/body slots hide their matching layers. Draft changes revert on leaving; Apply commits the saved appearance. Paint-only changes now invalidate the preview cache.

The existing size setting remains **outer tire radius in inches, including rubber**. 14→18 produces 28.57% larger wheel sprites. The placeholder wheel texture combines rim and tire, so rim diameter/sidewall are not independently adjustable artwork yet. Width acts along the depth axis and is not distorted into an ellipse in this side view. Internal engine, cooling, electronics and stripped interior changes continue to affect simulation/stats; these placeholders do not have separate artwork for them. Sprite world coordinates remain an illustration scaled relative to each car's stock artwork, rather than surveyed vehicle dimensions. Physics behavior/model versions are unchanged.

Validation:

- `tools/Invoke-Unity.ps1 -Action UnitsVisualVerify`: **68 checks passed** (44 targeted plus 24 existing inspection/parts/tune/save checks), including four-car 14/18 scaling, independent brakes/rotation, idempotent rendering, removed parts, legacy preferences, conversion and persistence/reset.
- `reports/player-fixtures/torque-wheel-ui-final`: **19 checks passed**, using actual Windows mouse controls. Preview-only size changes, leaving/revert, Apply, saved race geometry, shop install and torque settings/persistence verified; zero runtime errors.
- `reports/player-fixtures/torque-wheel-physical-final`: **33 checks passed** with mph/psi/lb-ft selected. Held gas/shift/input ownership, mass/fuel/tire controls, exact replay and snapshot import/export; zero runtime errors.
- `reports/player-fixtures/torque-wheel-sound`: **24 checks passed** with mph/psi/lb-ft selected. Live engine/turbo/gearbox/audio configuration remains editable during keyboard driving; zero runtime errors.
- `reports/player-fixtures/torque-wheel-reference`: exact 14-inch and 18-inch screenshots visually reviewed. Capture-only fixture, no mouse-control claim.
- Rebuilt Windows player successfully; final managed assembly timestamp: 2026-10-08 16:42:54 UTC. Launch `Game/Builds/Windows/DragLab.exe` with its complete adjacent build folder.

The physical bench import regression initially selected `unit-settings.json` from its fixture export directory. Replay selection now excludes preference files in both the actual button and test harness. The rerun passed. An early player-only test compilation and a summary check that observed the same frame before repaint were also corrected before the passing builds/runs.

Historical Slice 2F–2J checkpoints and their audit evidence are preserved. This work does not advance the pending Slice 2K–2L phases.
