# Partial-stock maintenance orders — 2026-10-15

Requested follow-up to [maintenance cart staging](CART-REPLACEMENTS.md): use available shop inventory first, deliver only shortages by express, show the missing quantity on the pill and verify the shop inventory lifecycle.

The existing stock ledger already performs the allocation and consumption correctly. This checkpoint retains that authority and adds explicit pill counts and regression coverage; it does not introduce another stock pool or change pricing rules.

- **Express · 2/4** means two of the four ordered units need express delivery. **In stock · 1** means the requested unit is available without a delivery fee. Tooltips state the stocked quantity, express quantity and included fee.
- The shared quote allocates stocked units first. The seeded 5–10% fee applies only to the shortage, including a partially stocked complete set. Already reserved units remain available to their own order when more replacements are added.
- Previewing does not move inventory. Add reserves the available units, reducing shop shelf availability immediately and preserving the hold in the saved cart. Removing/clearing the order releases its stocked units. Checkout permanently consumes those units once; express units go directly to the purchased components and never increase the shelf quantity.
- The normal combined cart price, exact installation positions, explicit checkout, rollback and nine-race reservation policy remain in force.

## Checks actually run

| Gate | Result |
| --- | --- |
| Maintenance replacement matrix | **677 checks, 0 failures**, all 13 cars |
| Cart reservation matrix | **48 checks, 0 failures** |
| Native maintenance mouse flow | **78 checks, 0 runtime errors** |
| Windows player build | Passed; updated `Game/Builds/Windows/DragLab.exe` and adjacent files |

The expanded matrix independently calculates shortage fees for complete sets and individual units, checks empty/partial/full/surplus supply, preview immutability, saved reservation quantities, shelf release, failed checkout rollback and exact post-checkout stock. Each car exercises a two-of-four stocked brake set and one-of-two stocked piston replacements. The starter additionally exercises zero, four and five stocked brakes for a four-unit order.

The real mouse run verifies the displayed fractions, fully stocked single-piston state, unchanged preview stock, durable shelf reservations, reuse when adding another cylinder, cancellation/retry and actual checkout. It confirms both tested shelf quantities reach zero after checkout without adding express deliveries to stock. Existing slider, switch and scrollbar gates still pass at 720p/1080p and a 4K render target. The normal-resolution dialog screenshot was visually inspected; user visual acceptance remains separate.

Local evidence: `reports/player-fixtures/maintenance-partial-stock-20261015/`, `reports/interface-screenshots/` (resolution captures in its subfolder), and archived domain results in `reports/branch-audit/local-partial-stock-raw/`. The screenshot runner clears its previous run's captures as usual. The earlier 102-check full-cart result is a retained baseline, not a rerun in this checkpoint.

Preserved unrelated starting work: edits to `reports/maintenance-batch/verification.txt` and `reports/shop-inventory/cart-verification.txt`, plus untracked `Tests.ps1` and `reports/race-exit-regression/`. Generated reports were archived before restoring their original bytes; build-only font whitespace was restored after verification. No later interface phase advanced.
