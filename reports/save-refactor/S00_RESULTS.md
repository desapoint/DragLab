# S00 save/state baseline — 2026-10-23

Production baseline: main `6d19aae`. S00 adds Editor-only diagnostic tooling, static synthetic fixtures and audit documents; no production save/physics/audio/UI path is changed. User authorizes the full plan; Luna-high approval is still required before S01.

## Verified evidence

- Final frozen-input Editor run (`editor-final-b.log`) completed with exit 0: **342 samples**, **57 action/scale groups**, **6 samples each**. Inputs are 1/5/20 cars × 0/24/96 loose parts.
- Independent `python tools/check_save_refactor_baseline.py` passes all 9 gzip/raw hash checks, canonical imported profile hashes, exact identity/health/structure, n/median/max aggregation, direct-read SQL labeling and unavailable allocation labeling.
- Frozen JSON import changes 792 duplicated historical numeric fields across 9 fixtures by at most 4 ULP, limited to runningSeconds/distanceM/consumedLife. This is an explicitly checked legacy test-import boundary. Every other value is exact, and Unity asserts imported→normalized state is byte-identical. No fixture is rewritten or healed. Converted production operations must preserve untouched typed values exactly and avoid JSON round-trips.
- Final-source focused rollback run (`editor-rollback.log`) completed with exit 0: **9 fixtures**, injection reached before COMMIT, unchanged committed source, and unchanged deterministic fingerprint of **all persisted user tables and table schemas**. It includes ownership mirrors, checkpoints, metadata and migrations. The full 342 timing run used the earlier narrower rows/meta rollback fingerprint; this final focused probe strengthens verification outside the timed workload.
- All disposable SQLite files, temporary backup WAL/SHM/journals and report-local settings sidecars are removed after draining; static input fixtures and raw logs remain. No real player save is loaded. Runs require a separate batch Editor outside play mode.

## Representative final medians (ms, n=6)

| Action / 96 loose parts | 1 car | 20 cars |
| --- | ---: | ---: |
| Fuel quote + payment + durable save | 752.05 | 1,111.35 |
| Cart quote + checkout + durable save | 749.72 | 945.45 |
| No-op operation through legacy transaction/save | 346.96 | 499.32 |

For the 20-car refill, separately measured phase medians are clone **161.41 ms**, normalization before/after **58.95 / 62.95 ms**, operation **580.65 ms**, save capture/validation **136.94 ms**, and database/durability **108.38 ms**. These are independently aggregated phase medians, not an additive decomposition of the median total. Operation includes existing quote/payment validation and JSON snapshot/fingerprint work, not only changing a fuel field.

Unity 6000.6.4f1, Windows 11, i9-10900KF / 20 logical cores, 32,636 MB RAM. Schema 2, profile contract 12, mechanics schema 1, physics draglab-variety-v22, content revision 8. Raw manifest records profile/stock/settings hashes, market seed/epoch, fixed request clock, frame-cap/VSync metadata and input counts.

## Boundaries and remaining measurement work

This is an **already-warmed Editor diagnostic**, with each sample using a fresh prepopulated database. It is not cold-process/JIT/OS storage, same-DB steady writes, packaged frame pacing or a phone result. Six samples support descriptive medians/maxima only, not meaningful p95/p99 estimates. The allocation API returned constant zero even for a known 1 MiB allocation; final output correctly marks allocations **unavailable**, not zero. Worker allocations, peak/retained memory, storage hardware model and thermal behavior remain unmeasured. ADB device inventory found no connected phone.

The bounded baseline directly measures fuel payment, stock/express cart quote/checkout, inventory read/move, no-op transaction and injected rollback. Broader worn/rebuild/swap/sale/setup/tuning/business/reset/recovery/settlement and independently scaled history/stock workloads are mapped in the test matrix, not claimed measured. Each command conversion must capture its own missing baseline before switching callers; S14 retains broader soak/device/frame gates. No remaining gate is silently marked complete.

## Corrections retained in local logs

Initial diagnostic compilation exposed a shadowed local and ambiguous Debug reference; both were fixed before timing. The first full run passed but its constant-zero allocation readings are invalid and its fixtures were not yet versioned. Strict raw-JSON replay then stopped on known decimal/double rounding; diagnostic comparison proved normalization unchanged and enabled the independently bounded import check above. Original logs/CSV/manifest remain under `reports/save-refactor/S00/*-initial*` and diagnostic logs, not relabelled as final passes.

Final timing harness SHA-256: `41a0545d54760ed266da7a5929a36612a883821a09cea0d8afba040ebbf38d5e`. Final focused-verification harness SHA-256: `4edf085f00de6df728dcf9506946d738a2e74809aaa469dca220bfc896508d85`. The latter broadens untimed table fingerprint/cleanup and adds a focused entry point; timed operations are unchanged.

See [execution ledger](../../docs/save-refactor/EXECUTION.md), [Luna-high review](../../docs/save-refactor/S00_REVIEW.md), [state/JSON audit](../../docs/save-refactor/S00_STATE_AUDIT.md), [storage audit](../../docs/save-refactor/S00_STORAGE_AUDIT.md), [test matrix](../../docs/save-refactor/S00_TEST_MATRIX.md), and [static fixture provenance](../../tools/fixtures/save-refactor-s00/README.md).
