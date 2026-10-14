# Installed AWD hardware and supported tuning — 2026-10-14

This implements the separately approved correction to AWD permissions. It does not advance another Slice 4 page. Technical completion and user interface acceptance remain separate.

## Behavior

On AWD cars the existing common Differential slot is the center distribution assembly. Front and rear axle differentials remain independent components. The transfer-case slot retains its housing/branch-disconnect role; the existing ratio-equipped transfer case includes a mechanically locked center and takes precedence as an integrated assembly.

Installed hardware declares its supported system and calibration. The catalog includes fixed 35:65 distribution, open center, mechanical center LSD, locked center, on-demand coupling, active center and an actual open/locked switchable assembly. Adjustable LSD, programmable on-demand and programmable active-center variants include their matching mechanism/controller. Installing an engine ECU does not change the center hardware. Existing component identities, condition, faults and quantities are retained; this update does not grant replacements or reroll saved market stock. New offers follow the existing seeded refresh/availability rules.

Fixed assemblies show their specifications without editable AWD sliders. Switchable hardware offers only Open and Locked. Adjustable LSD bias/preload, coupling engagement/response, active distribution limits and special transfer-case speed ratios are bounded by their hardware. Primary axle, disconnect capability, free-shaft inertia/drag and fixed mechanical geometry are specifications rather than generic tuning options. Normal locked hardware remains 1:1; unequal tire geometry can produce binding/slip and wear. The paid ratio-equipped unit retains its explicit ratio adjustment. All six existing axle-force modes still use independent tire geometry, loads, grip, losses, heat and stress.

Native Tuning and the legacy installed-car view use the same policy and shared existing controls. Unsupported modes are constrained again during build resolution and simulation construction, including saved or directly edited tunes. Raw engineering fixtures remain deliberately configurable. Part details/comparisons expose center-system specifications and tuning permissions without confusing them with the independent axle differentials. Changes remain drafts until Apply; fixed values and equipment installation retain their existing transaction boundaries.

The physics identity is now `draglab-variety-v19`. Older input recordings need recording again. A narrowly accepted v18 resource journal can still recover monotonic fuel/wear history when closing an interrupted run; it cannot resume obsolete driving inputs, grant a result, or bypass current checkpoint validation.

## Validation and review

The AWD hardware matrix passes **2,095/0**: authored AWD models, all requested modes, fixed and adjustable hardware, forged settings, bounds, independent axles, engine-ECU isolation, nested authoring/cache invalidation, replacement identity/condition and prior-version resource-journal recovery. The geometry matrix passes **487/0**; category comparison **1,256/0**, cart **102/0**, shop/cache **975/0**, seeded market **408/0** and checkpoint **672/0** are rerun. Their current evidence is copied here; historical reports are preserved.

Focused packaged real-input checks pass **151/0 runtime errors**, including fixed OEM systems, all added assemblies, permitted controls, bounded settings, switchable modes, isolated drafts, F8 and actual Apply/save/reload. Screenshots remain local in `reports/player-fixtures/awd-hardware-final/`. The active-controller screenshot was visually inspected for the existing control styling/layout. The Windows player was rebuilt successfully in `Game/Builds/Windows`.

The broader packaged Tuning regression passes **783/0 runtime errors**, including hardware variants across the roster, precision gestures, units, draft boundaries and missing-engine/empty-garage states. The residual-performance/save regression passes **2,526/0** with the expanded 2,270-definition catalog: exact isolated inspections, cached authored identity, seeded stock, migration, failed publication and corrupt/future-save safeguards. Prepared refresh samples are **65.91–70.58 ms** with per-offer capture maxima **6.50–9.95 ms**; compressed saves measure **134,697 bytes versus 1,446,072 bytes** flat. These local samples retain the earlier performance improvement without claiming frame-free definitive transactions or cold cache misses. No previous full-menu suite is claimed as rerun for this increment.

Review checklist:

- Open an owned AWD car in Tuning → Drivetrain: fixed center specifications should have no free mode picker.
- Preview/install a different center assembly and check that only supported modes/settings appear; engine ECU changes must not convert the system.
- Check front/rear axle differentials remain independent; test unequal tire sizes with ordinary locked and ratio-equipped hardware.
- Check shared slider precision/reset, F8, Revert and Apply; restart to confirm saved supported calibration.

The [performance follow-up](../residual-performance/CHANGES.md) is a separate committed checkpoint. The [database direction](../../docs/SAVE_STORAGE_DIRECTION.md) remains a proposal, not a SQL conversion.
