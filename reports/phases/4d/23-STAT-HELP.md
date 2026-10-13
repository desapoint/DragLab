# Shared stat help — More, Less and comparison drawbacks

2026-10-13. Requested shared help refinement, ready for user review after checks below. No subsequent Slice 4 control is authorized by this delivery.

## Changes

- Every current numeric stat and component rating has authored More/Less descriptions. Shared help covers engine output and geometry, cams, compression, forced induction, gearing, converter/clutch capacity, AWD, tires, load distribution, flywheel inertia, cooling, fluids, maintenance condition, individual elements, driving estimates and exhaust sound shaping.
- Wording connects values to driving effects and maintenance in plain language. For example, heavier wheels take effort both to move and to spin; shorter gearing pulls harder but reaches its rev limit sooner.
- Comparisons append a final drawback paragraph matching the actual higher/lower direction. Risks remain conditional; this help does not claim a simulated failure from one number. Unchanged/missing values and favorable changes are explicit. No duplicate comparison figures.
- Hardware categories retain their existing descriptions; warnings retain actual explanations/suggestions. All existing native stat consumers and tradeoff hints use the same helper. Unknown future stat extensions preserve authored help; catalogue coverage verification catches missing help for existing stats.
- No simulation, compatibility, tuning permission, unit conversion, profile/save or transaction behavior changes. Shared native dialog and hover styling remain intact.

## Checks

- Windows player build: passed (`tools/Invoke-Unity.ps1 BuildPlayerOnly`).
- Final `StatHelpVerify`: **157 checks, 13 cars, 0 failures**. Covers all catalogue ratings, every numeric nested stat/summary, direction-specific gearing/cooling, missing vs zero, categorical handling, and cam requirements versus spring strength. [Recorded checks](../../stat-help/verification.txt).
- Final full packaged native menu suite: **1,496 checks / 0 runtime errors** (`native-stat-help-final`). Existing shop/cart/engine/setup/maintenance/units/resizing/readonly gates also run in this suite; these counts overlap and must not be summed as independent acceptance. [Selected evidence](23-stat-help-native.txt).
- Normal-window power/weight/displacement dialogs and the orange comparison hint inspected visually: text wraps, separate paragraphs are visible and hover help stays inside the viewport. Existing compact/1080p/4K layout checks passed; new help screenshots are normal-resolution captures, not a claim of dedicated per-dialog 4K captures.
- **129** fresh screenshots exported by the full suite. Normal images stay at `reports/interface-screenshots`; alternate dimensions stay in `resolution-tests`. Six new `stat-help-*.png` captures cover single-car power, tuning weight and owned/dealership power/displacement comparisons.
- The earlier `native-stat-help` attempt stopped at **319 checks / 0 runtime errors** when an AWD click did not change the mode; it is retained as local diagnostic history, not acceptance. The final full rerun passed that check without a gameplay or input-harness change.
- UTF-8/mojibake and diff checks pass. Unity-imported font changes were verified to be trailing-whitespace-only and restored to the committed bytes.

Comparison drawbacks are explanations selected from the real delta direction, not new predictions of combined failure probability. Actual combined-build warnings remain separately authoritative. No physics, economy or audio calibration suites were rerun for this help-text-only change.

## Review checklist

- Open a numeric stat's info on car details: More/Less are understandable without car knowledge.
- Compare cars, shop parts, a tuning draft, cart, stored engines or a saved setup: the last paragraph describes that change's direction and potential drawbacks.
- Unchanged and unavailable stats do not invent a change or downside. Categorical rows do not pretend that one type is more/less.
- Check the orange tradeoff hint and info dialog share the explanation, wrap properly and remain readable at the normal window size.
- Confirm Close/X/Escape and draft state behave as before.

Visual acceptance remains the user's decision.
