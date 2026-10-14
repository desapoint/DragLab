# Broken and threshold maintenance — 2026-10-14

## Behavior

Garage → Owned car details → Maintenance now has **Replace broken** and **Replace under 15%**.

- Broken mode selects failed components and defective individual members. Threshold mode starts at **15% remaining condition**, includes broken components, and otherwise uses a strict below-threshold comparison. The exact existing `NativeTuningRange` control supports 1% left-drag, 0.1% right-drag/exact input and middle-click reset to 15%; it uses the common thin track, centered knob, fill and numeric field.
- The grouped checklist shows actual sets, cylinder members and tire/wheel corners. Shared animated switches remain compact (56 × 28) beside each condition; the initial vertical/stretch layout shown in the user screenshot is corrected, with no new control theme. Individual selections are optional. Selecting every member of the same specification buys a complete set; a partial selection buys exact units. An independent whole-set failure requires a set replacement rather than healing it through one healthy member.
- Fitted specifications are retained, including mixed corner compounds/rims. Stock and 5–10% express fees are aggregated before confirmation; the fixed review shows selected quantity, total and resulting balance. Previews, gestures, resizing, Cancel and X do not save or apply anything.
- Confirmation reuses the existing cart quote and one atomic purchase/save. Stale selections/stock, active runs, reservations, a pending cart, insufficient cash and failed persistence cannot partially publish. Failed-save retry preserves the reviewed selection.
- Removed sets/members keep their actual identity, wear and faults in inventory. Other members, other cars and applied tuning remain. Fuel and engine liquids are not refilled or renewed by component replacement. Missing hardware is not invented.
- This implements the previously deferred threshold-control decision, without a save-schema or simulation change. Existing individual repairs, refills, oil changes and rebuilds remain available.

## Validation

- Domain matrix: **221 / 0**, across all 13 authored cars; failed sets/cylinders, exact 15% boundary, mixed tires, retained members, complete-set grouping, independent selected car, delivery totals, insufficient cash and failed persistence.
- Windows build succeeded, then was rebuilt after the final checks at the user's request in `Game/Builds/Windows` (`DragLab.exe` plus its adjacent data/runtime files). Packaged native checks pass **52 / 0 runtime errors**, including actual mouse/keyboard input, shared switch/slider bounds at 720p/1080p/4K, read-only cancellation, individual selection, failed-save retry and retained identities. The market checkpoint `439df38` remains the prior validated baseline; its full menus **1,543/0**, cart **287/0** and workshop **496/0** are retained historical coverage, not repeated as this new feature's result.

## Verification correction

The first rerun with a reused fixture exposed a test setup omission: Garage already being the active route did not turn on its F8 replacement view. The focused probe now explicitly selects the native Garage before clicking. This is test setup only; the F8 behavior and player defaults are unchanged. Two consecutive runs of the same fixture pass **52/0** (including the 22-check first purchase) and **30/0** (the focused maintenance checks on the reused profile), replacing the prior screenshots each time. Repeat-run evidence is recorded alongside the native report.

## User review

Review both actions on an inspected owned car, adjust the remaining-condition threshold, uncheck a replacement and cancel. Confirm the final stock/fee/total review before buying. Fresh screenshots use the existing `reports/interface-screenshots` gallery, with other resolutions in `resolution-tests`. Technical completion and user visual acceptance remain separate; no later interface phase is advanced.
