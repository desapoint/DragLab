# User-directed UX refinements — 2026-10-22

Preserve the current performance/commerce checkpoint first. Deliver one requested control or infrastructure checkpoint at a time; technical completion is separate from user acceptance.

## Current

- Commerce/performance technical checkpoint completed in b413fc3 and 1f54da3; final Windows/Android builds are refreshed with the shifter control. User review remains pending.
- Remaining requested controls are technically complete with documented regression/build receipts; platform builds are refreshed. See [warning repair delivery](../reports/warning-repairs-20261023/CHANGES.md).
- Shifter control technically complete: 623 packaged checks passed, both platform builds refreshed. User explicitly directed continuation through the remaining requested controls; retain separate tested local commits and user acceptance status.

## Requested control sequence (technical completion is separate from acceptance)

1. Shifter (technically complete; user review pending): remove plus/minus controls entirely on desktop too; reduce the necessary interaction width. Add sun-like shading to the knob. On mobile put a shifting/flick-direction hint next to the gear number inside the knob, respecting the configured direction. Retain transmission-specific layouts, shift duration and independent held throttle.
2. False start (technically complete, native 54/0): quick leave action after leaving before the legal start (interpretation of missing the start/trying to leave too early). Retain once-only settlement, wear/resources and safe race exit.
3. Hear engine (technical checks: full sequence 64/0, header controls 49/0, owned fitted/layout 119/0; dealership 271/0): button for owned cars and dealership cars. Play a car-specific sequence covering idle, revving, shifting and pressure release/BOV when supported. Use isolated captured car hardware/condition; do not spend fuel, damage assets or alter saves. Preserve used-car inspection disclosure and stop the preview on exit/context change.
4. Warning repair links (technical warning suite 948/0): shared warning dialog should offer Shop navigation with default Compatible + In stock filters and the relevant part category/categories; preserve the user's current car context and existing draft/confirmation guards. A warning is guidance, not physical incompatibility.

These requests do not authorize unrelated new audio synthesis changes, later workbench phases, new bench navigation or removing desktop F8.

2026-10-22 clarification: continue the complete explicitly requested batch without per-control permission stops. Quick leave, engine listening and warning repair links are authorized in order. Physical/visual acceptance remains separate from technical completion.
