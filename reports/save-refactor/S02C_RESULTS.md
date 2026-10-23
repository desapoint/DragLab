# S02c ownership-validation lookup

Baseline: `7e6ea10` on main. One validation-only ID/relation lookup family; Luna medium implementation with Luna high independent review before advancing.

`MechanicsOwnership.Validate` keeps selected-car, vehicle-stock, economy, career and business gates before constructing a disposable lookup. Its ordered uniqueness, ownership/slot, member/fault, inventory, market, rebuild and run checks retain authority and error strings. Lookup substitutions address engines/parts/cars by ID and the first containing car for individual tire/wheel fitment. No normalization, repair, mutation, schema/writer or new state authority is added.

The helper captures first-wins ordinal ID maps, preserving nullable IDs and falling back to the original ordered scans if null records/lists require the old lazy failure behavior. Part-to-containing-car mapping is lazy, only reached after prior ownership gates; malformed relation rows discard partial maps and use the original nested scan. It is a borrowed synchronous scope, not an immutable worker snapshot or a mutation observer. Main owns live state; existing private detached candidates may use pure managed validation on their owner worker. Every exit disposes maps/references.

A test-only original Validate core was frozen before integration. Tests must compare exact rejection text/exception type and source preservation, not merely both-valid/both-invalid. Whole old/new validation timings include build cost; no lookup-only speedup is accepted. The legacy reference is Editor-only and must not enter Android/Windows player code.

## Verification

Unity 6000.6.4f1 Windows batch Editor `StateCommandCheckpointVerification.RunOwnershipValidation` exited 0: **132 parity/source-preservation checks and 72 isolated legacy storage checks passed**. This count is the complete corrected paired-benchmark run; the earlier five-sample run also passed 132+72 but is not the accepted performance method. Its raw files remain locally as `*.initial`. No active player save or pre-existing sqlite-state report was changed.

The original validator body is independently verified byte-equivalent after newline/access/SaveVersion qualification differences to commit `7e6ea10`. New metadata GUIDs are unique, and both verification/reference implementations are Editor-only. The production integration is limited to lookup substitutions; ordered validation remains authoritative.

The corrected benchmark uses three paired warm-ups and twenty measured pairs per workload, alternating legacy/indexed and indexed/legacy order equally. [Versioned raw matrix](S02C-matrix.csv) retains all 180 pairs, order, actual counts and per-call GC collection deltas. An independent Python read checked all nine groups, twenty distinct pair IDs and ten of each order, non-negative finite timings and summary arithmetic. Ordinary GC is included, with no forced or selectively excluded collection. Arithmetic medians below average the middle two samples; the run's textual BENCH_SUMMARY used the upper middle sample, which the final harness now corrects. Paired-delta median is separately calculated, not the difference of the two medians.

| Cars / spares | Actual parts | Legacy median ms | Indexed median ms | Median paired indexed-minus-legacy ms | Legacy / indexed sample max ms | Legacy / indexed Gen0 collections |
| --- | --- | --- | --- | --- | --- | --- |
| 1 / 0 | 39 | 26.0583 | 24.7446 | -0.3567 | 35.8113 / 33.8838 | 7 / 7 |
| 1 / 24 | 63 | 28.3956 | 26.9764 | -2.7128 | 50.5081 / 37.6831 | 9 / 8 |
| 1 / 96 | 135 | 26.5054 | 25.2369 | -0.1174 | 36.3268 / 43.9686 | 6 / 7 |
| 5 / 0 | 216 | 31.6820 | 34.4288 | -0.1103 | 42.7446 / 54.5013 | 6 / 10 |
| 5 / 24 | 240 | 31.7396 | 29.5937 | -2.8129 | 43.0713 / 40.0986 | 9 / 6 |
| 5 / 96 | 312 | 31.6542 | 29.9208 | -1.7060 | 43.3041 / 36.6533 | 8 / 7 |
| 20 / 0 | 807 | 53.4937 | 41.7805 | -10.9169 | 72.6127 / 45.9342 | 10 / 12 |
| 20 / 24 | 831 | 53.5517 | 41.1161 | -12.6199 | 70.5752 / 49.9062 | 11 / 11 |
| 20 / 96 | 903 | 54.5851 | 38.0640 | -16.5248 | 67.6679 / 60.2137 | 11 / 9 |

The 20-car distributions improve by about 22–30%. Small fixtures remain noisy: **5 cars / 0 spares has a slower separate indexed median (34.4288 vs 31.6820 ms, +8.7%)**, while its paired-difference median is -0.1103 ms and 9/20 indexed calls are slower. Both paths resolve the full dealership before reaching the lookup; those common temporary allocations and collections remain in every sample. These observations support improved large-graph scaling, not a statistically definitive absence of small-case regression or a claim that all validation/save costs are solved. The five-sample initial run's apparent 24–27% regressions were not hidden or accepted as the performance baseline.

Supplemental final-source `OwnershipValidationLookupChecks.RunLifetimeStandalone` exited 0: **10 checks passed** for double disposal, all post-disposal query methods, non-null duplicate relation order, malformed relation fallback and conventional median arithmetic. This is a separate focused run, not a rerun of the 132+72 full harness. No production or benchmark algorithm changed after the corrected full run; only these test probes and median summary arithmetic were added.

No frame-tail, allocation-byte, retained-heap, phone, UI/visual or packaged-player result is claimed. Scope is validation-only, and this adds no worker access to the live graph.

## Remaining hard gates

Existing `ValidateForQuote` dealer recovery still JSON-copies a full profile and normalizes a copy; it is unchanged and remains a hard completion blocker for the later typed command/quote conversion. This step does not satisfy the final no-full-profile-clone or no-runtime-JSON requirements. No persistent save path has been converted yet. Independent Luna-high review is **PASS** in [S02C_REVIEW.md](../../docs/save-refactor/S02C_REVIEW.md); local commit is required before advancing.
