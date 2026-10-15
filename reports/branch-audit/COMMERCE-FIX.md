# Liquid and shop quote correction — 2026-10-15

The user confirmed that liquids and some shop operations were blocked on the per-tire branch. This checkpoint addresses the shared quote-validation regression found in the branch audit; it does not advance an interface phase.

## Change

Dealer metadata reconciliation validates a temporary Unity JSON copy. Unity materializes absent inline `recoveryWork` and cart reservation records in that copy, causing unrelated valid purchases to fail with `Invalid recovery work` or `Invalid cart reservation clock or owner`.

`MechanicsOwnership.ValidateForQuote` now retains those two null states from the source profile before reconciling the copied dealer rows. It does not clear any real job/reservation, alter the live profile, regenerate assets, discard conflicting stock or relax validation. The same path supports paid liquids and asset valuations.

`FluidPurchaseVerification` now additionally checks a real recovery job and reserved cart remain intact during read-only validation and genuinely malformed records are rejected. Its existing cases cover generated stock, liquids, stale dealer discriminators, full-part and individual used-tire purchases, retained wear/identity, resale and conflicting stock rejection.

The valuation fixture was still applying an engine-only `Disabled` fault to newly typed tire elements. It now uses the actual individual tire failure flag and keeps its original condition snapshot separate from the mutated test case. This changes the fixture, not tire behavior or pricing.

## Validation

Unity `6000.6.4f1` runtime checks:

- `FluidPurchaseVerify`: PASS, including the previously failing refill and the added job/cart preservation/rejection cases.
- `CartVerify`: PASS, 102 checks.
- `InventoryCommerceVerify`: PASS, 705 checks.
- `ValuationVerify`: PASS, 4,026 checks after correcting the tire fixture.
- Windows `BuildPlayerOnly`: PASS; updated `Game/Builds/Windows/DragLab.exe` and its companion player data. This direct build entry point avoids the previously audited stale scene/legacy composite gates; those are not claimed green.

The first narrow recovery-job correction exposed the second empty-cart artifact; final validation preserves both absent records from the source. The corrected fixture then passed the complete valuation suite. Real mouse/UI interaction was not rerun because this checkpoint changes domain validation rather than presentation.

Local logs: `unity-commerce-fix.log`, `unity-commerce-valuation.log`, `unity-commerce-build.log` and `commerce-fix.tsv.results.tsv` in this folder. Historical generated reports are restored after retaining current local copies; the concise results above are versioned with the fix.

## Scope and review

Review fuel/oil/coolant top-ups and used-part purchases in the updated Windows player. Quotes should open and confirmed purchases should charge once while preserving actual stock and condition.

The incompatible-save reset-floor defect, inherited worn-tire rounding defect and stale audit test expectations remain separate outstanding findings in [the audit](AUDIT.md). No player save was reset. Existing user report edits and untracked files remain untouched.
