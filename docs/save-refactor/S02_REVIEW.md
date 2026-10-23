# S02a independent review

Reviewer: gpt-6-luna / high
Review status: **PASS — bounded owned-part identity-index checkpoint.**
Baseline: S01 `fd092bc`; review date: 2026-10-23.

## Findings

`OwnedPartIdentityIndex` is internal and invocation-local. Strict `Build` validates the full source list; production `BuildRequested` bounds the unique request set by `parts.Count`, stores positions only for requested matches, and stops once the last request is found. It rejects malformed rows in the scanned prefix. Missing or late IDs force a longer scan, and an early stop deliberately does not validate the tail. The ID map contains no `PartInstance` or condition copies, JSON manipulation, global cache, or second owner. Its reference to the list indirectly retains its records until disposal.

The caller uses the exact `profile.parts` list synchronously inside `ValidateCheckpointTargets`, collects validated handles into a private array, and disposes the map before application. There is no callback or identity/structure mutation during that lifetime. `ApplyCheckpoint` stages copied target conditions, selected resource values, and the replacement receipt before the first assignment. It applies conditions through those resolved handles without another part-ID search. Existing live-profile ownership stays on the main thread; the already detached settlement candidate can use the pure helper privately on its own worker. No new live-profile worker access or persistent authority was added.

The accepted boundary depends on a validated, uniquely identified ownership graph. Sparse mode cannot detect malformed or duplicate identities in an unscanned tail, and the type cannot detect arbitrary same-count mutation outside resolved positions. Rebuild after any identity/list mutation regardless of revision or count. Existing checkpoint validation also does not add binding-slot/location/unique-binding hardening; that remains an explicit precondition and later targeted work. The storage audit documents that `RaceCheckpointStore.TryQueue` can call validation directly, so callers must preserve the validated-profile invariant. S02a does not claim this topology risk is closed.

## Evidence reviewed

The corrected full Unity 6000.6.4f1 Windows batch run completed `RunPartIndex`: **1,375 focused identity/checkpoint checks and 72 isolated storage checks passed, exit 0**. After the reviewer found missing thermal assertions, the final test source ran `RunPreservationStandalone`: **1,067 overlapping checkpoint-preservation checks passed, exit 0**. This supplemental count is not added to the earlier full-run count; it verifies unchanged thermal state on rejection and the existing `max(prior, incoming)` car-clock behavior for a valid earlier incoming clock. The supplemental change was test-only; production and benchmark algorithms did not change.

The independent timing validation covers 16 workload rows and 25 columns, with 64 requested IDs, five warmed repeats, strict full map, sparse map, and `List.Find` for first, last, shuffled, and missing groups at 100, 1,000, 10,000, and 30,000 parts. At 30,000 parts, build-plus-query medians were 0.0191 ms sparse / 0.1261 ms `List.Find` / 2.3960 ms strict map for the first 64; 1.7679 / 109.8497 / 2.3862 ms for the last 64; 1.9457 / 53.6736 / 2.3766 ms shuffled; and 1.8191 / 16.6973 / 2.4608 ms missing. These are warmed Editor microbenchmarks, not device, full checkpoint, frame-tail, allocation, or heap-profile results. Five samples support median/sample-maximum reporting only, not p95/p99. The 24-byte-per-entry figure is a structural lower-bound estimate that excludes alignment, capacity, headers, request-set allocation, and the source-list reference.

Raw receipts are recorded in `reports/save-refactor/S02/` and summarized in `reports/save-refactor/S02_RESULTS.md` and `reports/save-refactor/S02-matrix.csv`. The initial strict-map run passed its tests but regressed the first-64 workload; it was not accepted as the final optimization. Sparse capture resolved that measured regression. The focused test harness does not clone/serialize the profile or touch a player save; the existing storage suite uses isolated fixtures.

## Scope carried forward

This is one bounded identity-index optimization, not a command/save-path migration. `MechanicsTransactions.Try` still uses whole-profile JSON cloning and normalization, and broader runtime JSON manipulation remains. The user’s no-full-profile-clone and no-runtime-JSON-manipulation requirements remain hard gates for every later migrated production path and final plan completion. S02a does not claim those gates are met. No additional index family or S02b work is included in this approval.

No remaining S02a blocker. Proceed only after the coordinator completes the required local commit and verifies the committed checkpoint; keep the topology, clone, and JSON work open for their assigned future steps.
