# Maintenance replacements through the cart — 2026-10-15

Requested checkpoint: make Replace broken / Replace under a configurable remaining-condition percentage add matching parts to the shared cart with installation checked. Show delivery shortages before adding. This does not advance the next Slice 4 interface.

## Behavior

- The previous native dialog bought immediately and rejected every nonempty cart. The new Add to cart action saves only the order/reservation. Payment, ownership changes and installation happen through ordinary cart checkout, with its combined dyno, stats, warnings and confirmation.
- The threshold still defaults to **below 15% remaining condition**, with whole-percent dragging, 0.1% right-drag precision and middle-click reset. Broken/faulty parts remain eligible at any threshold. Deselection keeps excluded components untouched. Rows and totals remain stable while dragging, then refresh on release.
- Matching installed specifications and exact cylinder/corner targets are retained. Compatible existing orders merge without duplicate positions or quantities. A matching complete set covers individual replacements; promoting an individual order to a set retains separately ordered spares. Different-specification installation conflicts and another car's cart reject without overwriting that cart.
- Adding from a non-current car's details preserves the current car. The resulting **View cart** action explicitly selects the inspected car and opens its basket. Add remains available with insufficient funds; checkout remains blocked until affordable.
- Stock/Express pills sit beside part names. Their tooltips give missing quantities and included fees; the fixed review shows the entire basket's delivered total, balance, stock/express counts and cumulative fee. The existing seeded **5–10% fee applies to missing units**, including partially stocked sets. Previously reserved supply is reused rather than charged as missing a second time.
- Cancellation, stale state/cart and failed saves publish no partial order or resource changes. Re-adding the identical selection does not extend its reservation. The existing nine-race reservation policy remains in force.

## Verification actually run

| Gate | Result |
| --- | --- |
| MaintenanceReplacementVerification.Run | **416 checks, 0 failures**, all 13 cars |
| CartVerification.Run | **102 checks passed** |
| CartReservationVerification.Run | **48 checks, 0 failures** |
| BuildPlayerOnly | Windows player built successfully |
| Native menu MaintenanceOnly, fresh isolated save | **74 checks, 0 runtime errors** |

The maintenance matrix covers actual per-cylinder faults/wear, strict threshold boundaries, mixed tire compounds/corners, complete-set promotion, retained spares, conflicting baskets, no-money staging, reserved supply, express charges, failed staging/checkout saves, normal atomic checkout, retained removed identities and subsequent liquid purchases. The cart/reservation gates passed after the shared quote change; the final maintenance matrix was rerun after the set/individual merge refinement.

The real mouse run covers threshold gestures, stable rows during both drag modes, deselection, cancel/X, save failure and retry, durable reservation reload, duplicate adds, insufficient funds, wrong-car protection, stale confirmation, View cart, checkout cancellation and actual express-inclusive payment/installation. Its reviewed two-cylinder/brake basket cost **$294 including $24 delivery**; the exact same total was paid once. Excluded radiator wear and its leak remained unchanged.

During implementation, player tests caught and corrected the renamed action's text-dependent UI identifier and a queued Garage-to-cart navigation handoff. Dialog actions now optionally accept a stable identifier independently of their caption; existing callers retain their prior IDs. The maintenance test resets only its isolated fixture cart so an aborted prior run cannot contaminate its starting state. Failed attempts were not counted as passing gates.

## Build and visual review

- Updated runnable package: `Game/Builds/Windows/DragLab.exe` and its adjacent data/dependencies. These generated files remain local.
- Screenshots: `reports/interface-screenshots/`; resolution captures stay in its resolution subfolder. The existing screenshot runner clears its previous captures before each run.
- Final isolated evidence: `reports/player-fixtures/maintenance-cart-final-20261015/verification.txt` and `player.log`. Domain outputs are archived locally under `reports/branch-audit/local-maintenance-cart-raw/`; historical/user-edited report files are preserved separately.
- Executed layout checks: 1280×720, 1920×1080, and 3840×2160 render-target scaling. Shared thin sliders, 56×28 switches, dialog scrollbar insets, fixed totals/actions and X clearance pass. Visually inspected the normal-resolution replacement dialog and combined cart capture.

User review remains separate from technical completion: inspect the part-name pills, threshold spacing and View cart workflow, then review the combined basket before checkout. No later interface step is started.

Pre-existing work left untouched and excluded from this commit: edits to `reports/maintenance-batch/verification.txt` and `reports/shop-inventory/cart-verification.txt`, plus untracked `Tests.ps1` and `reports/race-exit-regression/`. Generated checks were archived before restoring those original report bytes; font build changes were verified to contain only whitespace and restored.
