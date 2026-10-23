# S04 scoped fluid-refill planner shadow

Baseline `3dfcfa1` on main. One dormant action: ordinary oil/coolant top-up, excluding fuel, oil change/filter, flush, parts and rebuilds. No gameplay caller or persistence writer is switched.

`FluidRefillPlanner.Capture` resolves the current target capacities on the owner, reads guard state and copies only immutable request/fence/resource/cash/clock/receipt-window values. `Plan` uses those detached values; it neither owns nor mutates a PlayerProfile. Only the two quantities change in a successful after-image; all fourteen fluid fields, including service history/condition/pressure/temperature, are otherwise preserved. Rejected after-images retain original fluids, cash and saved clock and have no receipt. The proposed receipt uses actual confirmation time and the next ledger revision, not quote issue time.

Freshness uses exact typed read values, current supplied slot/reset identity and real ledger/storage/catalogue fences. It catches same-revision resource, rate, cash, capacity, engine-identity and receipt-window changes. A changed confirmation tick requires a fresh plan. S06 must choose/freeze the actual owner confirmation event and replan once from current input, rather than create an asynchronous revalidation loop in which the timestamp changes on every retry. No per-record version or persistence/reset provider is fabricated.

## Checks actually run

The **final-source** Unity 6000.6.4f1 Windows batch `StateCommandCheckpointVerification.RunFluidRefillPlanner` exited 0: **164 focused checks and 72 isolated legacy storage/recovery checks passed**. Earlier runs are retained locally: prior full 157+72 (`*.prior`) and the separate six-probe capacity/engine check. The final complete run includes all six probes plus an explicit valid-receipt-fixture assertion, so those historical counts are not added to 164.

A late test-fixture correction set the saved economy clock before capturing a receipt-window plan. PlayerProfile.New initializes the idle/business clock, not lastEconomyUtcTicks; an inserted older receipt otherwise violated the valid-graph precondition. The corrected case now proves full validation passes before testing staleness. Production planning code and timed algorithms did not change for this correction. The earlier Luna-high PASS predates this final-source evidence; a fresh independent Luna-high review has now approved the actual final-source run in [S04_FINAL_REVIEW.md](../../docs/save-refactor/S04_FINAL_REVIEW.md).

The independent oracle uses `PaidEconomy.Preview` (the Fluids branch returns PaymentAmounts without JSON or PaymentQuote construction), then `ServiceOperations.TopUp` twice, cash subtraction and `PaidEconomy.Record` on isolated typed fixtures. It does not call JSON-backed Quote/Pay or a full-profile copy. Independent fingerprints compare all fourteen fluid bit patterns against the legacy after-state. Three full successful command shadows include receipt IDs/target/amounts/revision/time and full-validator acceptance. Thirty-two typed fluid/quantity cases use seed **42004**. Six money boundaries include bills around 0.5e-9, 1e-9 and 2e-9, and around an integer ceiling.

Guards exercise clock rollback/issue/expiry, duplicate operation, active run, target availability/reservations, missing engine, malformed engine parent/vehicle/separate slots, malformed fluids, stale global fences and nonfinite/negative quantities. Rejection output semantics, exact finite-double hash collisions, later confirmation replanning, same-revision read changes and the actual full-validator behavior on affected malformed fixtures are checked. The supplemental capacity and engine changes keep the graph valid, change no ledger revision, then restore exactly with finally blocks and no normalization.

The planner requires an already-valid ownership graph; it does not replace whole-profile load validation or reproduce ValidateForQuote's global dealer-recovery normalization. It intentionally surfaces targeted malformed state without healing it. No full legacy quote fingerprint/error policy over unrelated corrupted records is claimed. These are dormant planner tests, not SQL atomicity, durable idempotency or UI confirmation tests.

## Bounded measured cost

One authored car, 39 owned parts, oil/coolant top-up; three warmups and twenty Stopwatch samples in fixed capture/Plan/legacy order. [Versioned raw samples](S04-matrix.csv) are independently checked for count/IDs, finite nonnegative values, arithmetic median and maximum. Copies used to restore the benchmark are only the affected FluidState; fixtures are never whole-profile cloned. Results are descriptive, without random-order/GC-byte/device/frame-tail claims.

| Timed work | Median ms | Sample maximum ms |
| --- | --- | --- |
| Owner capture | 0.25930 | 0.33910 |
| Private Plan | 0.00175 | 0.03390 |
| Legacy Preview + two TopUp + Record + revision | 1.19680 | 1.30790 |

Capture includes current-capacity resolution and targeted guards; Plan consumes private values. The legacy timing includes Preview, two simulation-backed TopUp calls, cash, Record and revision, but excludes Quote/fingerprinting, whole-profile transaction cloning and durable I/O. These figures are not end-to-end game/save or multi-car scaling improvements. No packaged build, phone, UI/visual acceptance, retained-memory or allocation-byte result is claimed.

## Next gate / storage prerequisites

Final independent Luna-high review is **PASS**; local commit is pending. [Storage audit](../../docs/save-refactor/S04_STORAGE_IMPACT.md) shows that schema v2 still uses JSON car/receipt payloads, a full-row diff/relation sync and transient per-row digests recomputed globally. A separate minimal typed-resource/receipt and incremental integrity **proposal/review checkpoint must precede S05 implementation**. Do not reuse StateRows capture or hide a whole-profile binary snapshot behind the pilot. Global S11 schema work remains separate.

Existing production transaction/quote/save paths and other cache keys still clone/encode JSON. The final no-full-profile-save-clone and no-runtime-JSON gates remain open. No S05 writer/migration or later gameplay increment has started.
