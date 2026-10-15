# Per-tire authority branch audit — 2026-10-15

Current follow-up: [release audit and final validation](RELEASE-AUDIT.md). The failures below are retained as the original baseline.

Reviewed `refactor/per-tire-authority` at `81e5699` against `main` at `9bcc706` (68 changed files). This is a focused runtime/source audit of tire ownership, checkpointing, reset recovery and commerce, not certification of every branch feature.

**Result: not green. Two introduced regressions reproduced, plus one inherited checkpoint defect. Production behavior was left unchanged for review.** The intentional v12 incompatible-save reset policy is not itself reported as a defect.

Follow-up: the user confirmed liquids/shop failures. The quote-validation regression is addressed in [the commerce correction](COMMERCE-FIX.md). The results below retain the original audit baseline.

The later [pricing-assertion audit](PRICING-ASSERTIONS.md) corrects the stock/delivery expectations and confirms the full axle suite plus individual-engine wrapper. Historical failed outcomes below are retained rather than overwritten.

## Introduced regressions

### P1 — Incompatible-save reset leaves a recovery floor that rejects its new database

`Game/Assets/DragLab/Scripts/Infrastructure/ProfileStore.cs:26`

The archive suffix list omits `.reset-generation` and `.reset-intent`. An ordinary progression reset establishes a positive generation floor. The incompatible-save reset subsequently archives the databases and writes a fresh generation-zero database while retaining that floor. It returns success, but the next load rejects both the new primary and backup as predating the committed reset, leaving the player in the reset-required flow again.

Reproduced using a disposable SQLite save: create; perform normal reset; make primary/recovery unreadable; load; explicitly reset; load again. The explicit reset returned true, then `ResetRequired` was true on reload. `SqliteStateStore.Read` logged `Recovery database predates the committed reset.`

Repair should treat the reset markers as part of the archived/rollback-managed save family or preserve a compatible generation transactionally. Retain the existing protection against resurrecting pre-reset progression.

### P2 — Read-only dealer normalization causes unrelated refill quotes to fail

`Game/Assets/DragLab/Scripts/Domain/MechanicsOwnership.cs:27–30`

`ValidateForQuote` serializes a copy when the dealer discriminator needs normalization, but applies only `SharedInventory.Ensure`. Unity also materializes the null inline `recoveryWork` as an empty object. `PaidEconomy.ValidateState` then rejects the copy with `Invalid recovery work.` Normal transaction normalization already handles that artifact through `PaidEconomy.Ensure`.

The branch's own `FluidPurchaseVerification.Run` reproduces this at line 159: a valid used tire listing with a stale discriminator prevents an oil-refill quote. It failed in the commerce batch and again as the first action of a fresh Editor process, excluding preceding-test contamination. Earlier generated-market/refill and inactive-payload cases in the same test reached this point successfully.

Repair should normalize the relevant serialization artifacts on the copy without changing the live profile or weakening validation of real malformed recovery jobs/duplicate assets.

## Inherited defect still present

### P1 — Worn-tire rounding can block a race checkpoint

`Game/Assets/DragLab/Scripts/Domain/TireDynamics.cs:51,94`

A tire seeded at `0.1f` has its complement calculated in float precision and stored as a double. A zero-work idle tick recalculates health as `0.100000024`. The strict health-monotonicity check correctly rejects that apparent healing, so `RunPersistence.ConditionAdvances` returns false. This can retain the earlier post-race exit/checkpoint symptom even though the refactor removes the old aggregate-history problem.

The same complement/recalculation existed on `main`; this is **not attributed as a newly introduced regression**. The added focused test reproduces it on this branch. Fix the wear arithmetic/monotonic update rather than permitting real healing through checkpoint validation.

## Runtime matrix

Unity Editor `6000.6.4f1`, Windows batch mode. All save reproductions used isolated fixture paths. No player save was reset or modified.

| Existing suite | Result | Evidence / limitation |
| --- | --- | --- |
| TireVerify | PASS | 494 checks, 171.3 s |
| TireFailureVerify | PASS | 5 checks |
| AxleWheelVerify | FAIL | Mixed-compound checkout combined assertion at line 271; see diagnostic below |
| VehicleMarketVerify | PASS | Stock/pricing checks |
| CareerVarietyVerify | PASS | Rival behavior scenarios |
| CheckpointVerify | PASS | 672 checks; normal checkpoint scenarios do not cover the reproduced worn-tire rounding case |
| ResetVerify | PASS | 17 checks; legacy fixture path does not exercise the new incompatible SQLite reset flow |
| SceneCheck | FAIL — stale fixture | Starts a race from a new game with no purchased car, then dereferences the null race at line 36. Also retains obsolete guaranteed-win and old-save expectations |
| SqliteStateVerify | FAIL — stale expectation | Current-state round trips/checkpoints progressed, then line 56 expected pre-mechanics JSON migration despite the new explicit incompatible-save reset policy; later assertions were not executed |
| FluidPurchaseVerify | FAIL — production regression | `Invalid recovery work` in stale-dealer oil-refill quote; independently repeated |
| MaintenanceReplaceVerify | PASS | 234 checks |
| CartVerify | PASS | 102 checks |
| InventoryCommerceVerify | PASS | Per-category purchases, replacements, storage and resale scenarios |

Totals: **13 existing suites executed: 9 passed, 4 failed.** No full Functional/Extended run, packaged build, real mouse/UI, rendering or audio certification is claimed.

Additional focused audit: 3 failed assertions reproduce the two save/checkpoint problems above, repeated in a fresh process. Normal and empty-stock tire-cart diagnostics passed, including unchanged front tires, actual rear compound, retained worn originals and exact quoted payment.

The empty-stock diagnostic paid **946**, with base price **900** and quote **946**. `AxleWheelVerification.cs:271` assumes payment always equals base price and uses time-seeded stock, so its combined assertion can fail on valid express fees. The original combined failure did not log each operand; this audit does not claim to have excluded every later failure in that suite. Split the assertions and account for the quoted delivery cost, then rerun the remaining axle/setup scenarios.

The new curated Smoke group still includes `SceneCheck` and `SqliteStateVerify` with obsolete assumptions. Update those tests to current contracts rather than changing game behavior to satisfy them or treating the suite as a clean release gate.

## Reproduction and artifacts

Added `Game/Assets/DragLab/Editor/BranchAuditVerification.cs` with public static methods:

- `Run`: worn-tire idle/checkpoint and reset-after-reset regressions; throws on failed safety assertions.
- `TireCartDiagnostic`: mixed rear replacement with generated stock.
- `TireCartExpressDiagnostic`: the same transaction with deliberately empty stock.

These can be invoked through the existing `ValidationBatch` manifest mechanism. Use a UTF-8 manifest with first line `#continue=1`, followed by tab-separated label and fully qualified method, for example `FocusedAudit` and `DragLab.Editor.BranchAuditVerification.Run`. Run `tools/Invoke-Unity.ps1 -Action ValidationBatch -BatchManifest <manifest> -LogPath <log>`.

Raw local evidence remains in `reports/branch-audit/` (`focused-verification.txt`, tire-cart diagnostics, `audit.tsv.results.tsv`, `confirm.tsv.results.tsv`, Unity logs) and `reports/validation-batches/`. These generated artifacts are not committed. Pre-existing user report edits and `Tests.ps1` were preserved; historical reports overwritten by this audit's test runs were restored, with current findings recorded here instead.

No fixes, new interface phase, build replacement or push are included in this audit checkpoint.
