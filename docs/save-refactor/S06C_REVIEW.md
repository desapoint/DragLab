# S06c independent review — manual refill functional activation

Review status: **PASS for the scoped functional activation only.** The broader S06 gate remains **OPEN**. Complete S06d before starting S07. This review does not approve the full save-state refactor or claim human visual acceptance.

## Source review

The final core, UI and runtime routes have no remaining functional blocker found in this review:

- Native owned-car oil/coolant maintenance enters the typed flow before the legacy JSON request clone and profile snapshot. Desktop F8 `PaymentCost` and `QueuePayment` route manual fluid top-ups into the same typed facade before the legacy payment serialization path.
- The pilot uses immutable quote and command values. Cache hits validate the scoped live read set, content version, selected-owner receipt metadata and the oil-pan/cooling capacity facts; admission runs the authoritative planner capture and plan while holding the selected-owner lease.
- Normal command persistence, owner polling and affected-field publication do not serialize, clone, reload or project the full player profile. Publication installs the durable owner proof with the targeted fluid/economy fields under the retained lease. Explicit validated reload remains an exceptional recovery boundary.
- Pending, unknown and durable-but-unhandled operations retain their identity and fence competing mutations. Entry guards cover the audited purchase, cart staging, tuning/setup, reset and race routes. A successful refill-only revision preserves an unrelated tune draft. Separate settings saves preserve the same live progression-profile reference.
- The native dialog submits the displayed quote without silently repricing. The final source updates the actual visible child caption inserted by `AddSurface`; final player verification checks rendered caption text/geometry for `Recover` and `Complete`, rather than checking only `Button.text`.

This is a per-path review, not an end-to-end “no JSON in the whole frame” claim. Existing broader menu refresh/fingerprint work and other legacy callers remain outside this slice and are tracked by later S06/S13 work.

## Evidence reviewed

| Gate | Result | Evidence |
| --- | --- | --- |
| Managed pilot and actual SQLite | 78 assertions pass; five timing observations are reported separately | `reports/save-refactor/S06C/pilot-checks.txt` |
| Selected-owner regression | 83 pass | `reports/save-refactor/S06C/owner-regression-checks.txt` |
| Typed migration | 66 pass | `reports/save-refactor/S06C/typed-migration-checks.txt` |
| Isolated storage regression | 72 pass | `reports/save-refactor/S06C/legacy-storage-checks.txt` |
| Managed run57046 | Unity 6000.6.4f1, exit 0 | `reports/save-refactor/S06C/editor.log` |
| Final Windows player build30013 | exit 0 | `reports/save-refactor/S06C/build-windows-caption.log` |
| Final native run13795 | 70 checks, 0 runtime errors, exit 0 | `reports/player-fixtures/s06c-native-20261024-d/verification.txt` |
| Final F8 run24755 | 52 checks, 0 runtime errors, exit 0 | `reports/player-fixtures/s06c-f8-20261024-c/verification.txt` |

The native suite includes 40 shared onboarding/starter checks and 30 refill checks; F8 includes the same 40 shared checks and 12 refill checks. The refill checks cover persisted/reloaded owned-car fixtures, insufficient funds, stale quote rejection and explicit recovery, actual SQLite dispatch, pending and duplicate confirmation behavior, close/navigation, race-entry exclusion, settings application/draft preservation, fresh operation after success, coolant isolation, and visible `Recover`/`Complete` captions. F8 clicks the actual visible oil, coolant and combined-fill controls and verifies one-effect publication. Native retry/unknown-outcome controls were not exercised in the player run.

I inspected the final quote, stale-recovery, pending and completion captures under `reports/player-fixtures/s06c-native-20261024-d/`. The stale recovery action visibly reads **Recover**, and the completed action visibly reads **Complete** and is disabled. These captures are implementation evidence, not user acceptance.

## Open S06d gate — required before S07

The functional routes passed, but the S06 responsiveness and lifecycle gate did not. Final native frame captures show transaction-adjacent consumer frames above the 50 ms target: oil pending/Build navigation peaks at 177.117 ms, coolant confirmation/completion at 55.373 ms, and the explicit stale-recovery interval at 990.030 ms because it includes the validated-load recovery boundary. The managed first quote capture was 6.011 ms against the provisional 2–3 ms target; validated cache hit was 1.334 ms, held-worker poll 0.625 ms, completion/publication poll 2.056 ms, and writer-release-to-publication 36.905 ms. These are single authored desktop observations, not distributions or device guarantees. Fixture setup is reported separately.

S06d must, before S07:

- Reduce or isolate the broad consumer/UI refresh work responsible for refill-adjacent frame stalls and remeasure quote capture, publication and complete consumer frames with adequate samples.
- Add actual advancing-player coverage for native unknown outcome/retry, definite failure, failed recovery and interruption/resume while proving the operation identity and mutation fence remain intact.
- Re-run the required managed/build/UI gates after any source changes and update the evidence report with the final captures and measurements.

Do not treat the low owner-publication time as evidence that the full frame meets the responsiveness target. Generic forced-termination and platform soak remain later S14 work. No Android/device validation is claimed.

S07–S14, automatic race-entry fluids and the remaining runtime JSON/full-profile save-clone retirement are still required by the overall plan. This functional checkpoint does not close those goals.
