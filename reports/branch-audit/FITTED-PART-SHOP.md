# Fitted-part Shop button — 2026-10-15

Requested control-sized checkpoint only. User visual acceptance remains separate from technical checks; no later Slice 4 phase advances.

- Every disclosed fitted-part row in the dealership dialog now has a compact Shop button beside its type, reusing the current filled icon, action surface, hover/pressed states and no-Tab behavior.
- The button closes the dialog and queues a fresh Build → Shop navigation. The selected section and query are set before rendering: **Compatible + In stock + exactly the clicked category**, Current build reference, no search, default Name order and no retained price/brand/tuning/stat filters.
- The currently selected owned car remains the compatibility reference. Browsing another dealership car's components does not select or buy that car. Uninspected listings retain their existing disclosure boundary. Before a first car is bought, the button is disabled with a concise tooltip because the part browser requires an owned-car reference.
- No purchase/install, inspection, stock refresh, asset/resource change or save is performed. Actual reserved cart rows survive. Clicking main Build again clears the contextual category under the established navigation-default rule. An empty result retains the requested filters instead of silently showing unavailable or incompatible products.
- No full-part dyno calculation is needed for the category filter, and existing preview/specification caches remain available.

## Checks

- Windows BuildPlayerOnly: passed; refreshed `Game/Builds/Windows/DragLab.exe` and companion data.
- Native navigation/fitted-shop suite: **103 checks / 0 runtime errors**. Real mouse/keyboard events and an isolated fresh-save fixture verify disabled first-purchase actions and actual shortcuts for Pistons, Induction, Clutch, Wheels and Body across all five Build sections, matching visible offers, filter reset, car/cart identity and unchanged profile/save bytes. Ordinary browsing records zero full-part comparisons. Fitted-row and filter screenshots were visually inspected at 1280×720.
- Shared fitted-list checks now distinguish system disclosure buttons from per-part actions, preserving the existing collapsed overview assertions.
- Local evidence: `reports/player-fixtures/native-fitted-parts-shop/verification.txt`, player log and `reports/branch-audit/fitted-shop-build.log`. Screenshots are exported into `reports/interface-screenshots/` as `fitted-parts-shop-*.png` and `fitted-shop-filter.png`.
- No full simulation/economy matrix is claimed for this navigation-only control. Existing user maintenance/cart report edits, `Tests.ps1` and race-exit regression files remain excluded.

## Review

Open a disclosed car's fitted parts, expand a group and click Shop on a component. Confirm the matching section/type and Compatible + In stock; opening Filters reveals the selected type under Advanced. Check that current car/cart are retained and that normal Build navigation restores the unfiltered default Engine shop.
