# Sequential manual gearbox hardware — 2026-10-08

Manual gearboxes now have an explicit H-pattern / Sequential selector layout, separate from `ShiftMode` driving assists. TransmissionType/TransmissionUpgrade enum ordinals and save IDs are unchanged; existing layouts default to H-pattern. A configurable `sequentialShiftScale` defaults to 0.45 (legacy zero falls back to 0.45) and must lie in 0.10–0.95. It multiplies the same H-pattern duration and installed gearbox's shift modifier only for manual transmissions. The stock example is 0.2624 s for the stage-1 H-pattern swap versus 0.11808 s for its sequential counterpart, with identical ratios, power and gearbox capacity.

A named compatible `Gearbox/SequentialManual` part is available for all four cars through the existing buy/install, inventory/reuse, preview and durability systems. The selector and factor follow the installed identity into races and snapshots. Reinstalling the original gearbox restores the original layout. Factory-rank gearbox parts retain their authored vehicle selector. Core/summary stats, component descriptions, race HUD and both benches now distinguish H-pattern and sequential manual hardware. The former “Sequential” driving control label is “Shift myself” to prevent confusing button input/assistance with hardware.

Both remain single-clutch manual gearboxes. They disconnect shaft torque during selection while engine/flywheel rotation persists. Full manual requires the clutch for this variant; no clutchless flatshift controller or DCT preselection/torque overlap is implied. Downshift interlock remains explicit hardware, so an unprotected money shift can damage the engine. Wear, clutch capacity, neutral decay and per-wheel contact continue through the existing shared simulation. The audio fixture shares the same transmission configuration/duration calculation. The bench selector and speed-factor fields remain editable during keyboard driving.

The mechanical distinction is informed by [Quaife's motorsport gearkits](https://www.quaife.co.uk/quaife-products/motorsport-gearkits/), which distinguishes sequential selection and the optional ECU-assisted flatshift capability. **0.45 is a configurable game-authored timing factor, not a manufacturer measurement.** This implementation models the ordinary clutch-assisted variant; clutchless shift-cut hardware would need its own implementation.

New captures use `draglab-manual-gearbox-v11`. Previous player progression/inventory/unit/audio preferences remain compatible and are not reset. Physics snapshots from previous model versions remain on disk; the existing strict replay-model gate requests a fresh recording rather than silently replaying them under a changed model.

Verification in `verification.txt`: 139 checks cover all four cars, both manual layouts, all three driving modes, measured fixed-tick handovers, persistent flywheel rotation/held player gas, clutch requirements, abuse/interlock behavior, identical JSON replay, installed identity/reuse and unchanged DCT/automatic/CVT timing. `ManualGearboxVerification.Run` is now part of the normal build verification gate.

Final verification:

- Full `BuildWindows` succeeded: **2,477 foundation checks**, including the 139 gearbox-specific checks. UTC gate completion 2026-10-08T17:06:45.7233147Z.
- `reports/player-fixtures/sequential-manual-shop`: **14 checks**, actual Windows mouse buy/install, preview, saved race hardware, disk reload and OEM reuse; zero runtime/GUI errors.
- `reports/player-fixtures/sequential-manual-bench`: **28 checks**, actual manual-type/selector mouse selection while W remains held, shared faster duration, and independent live configuration edits; zero runtime/DSP/GUI errors.
- Shop comparison and bench selector screenshots visually reviewed. The shop summary displays saved H-pattern → Sequential manual and 0.32 s → 0.12 s for the installed-build preview.

Launch the updated `Game/Builds/Windows/DragLab.exe` with its complete adjacent build folder. Historical Slice 2F–2J archives and physics audit reports are retained.
