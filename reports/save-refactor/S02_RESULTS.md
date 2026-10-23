# S02a operation-local owned-part identity lookup

Baseline: S01 `fd092bc` on main. One identity family only; implementation Luna medium, independent checkpoint review Luna high. No worktree.

## Change and ownership

Checkpoint validation resolves actual component identities through a requested-ID-only ordinal map scoped to one synchronous invocation, scanning only until all requested IDs are found (full scan for late/missing IDs). Validated source ownership topology remains a precondition; sparse capture does not inspect an unscanned tail. It returns only validated affected-part handles and disposes the map before application. No list/identity mutation or arbitrary callback occurs while the index is alive. Main retains live-profile ownership; the pre-existing isolated settlement candidate may execute this pure managed helper on its private worker. No live state was newly exposed to workers.

The map is deliberately not a retained profile cache or an automatic observer of public mutable objects. Same-count structural/identity changes require disposal and rebuild. Count/resolved-position checks are defensive guards, not a replacement for this ownership rule. It copies no car, part or condition graph and uses no JSON. It does not introduce canonical state, a save writer or record versions.

`ApplyCheckpoint` prepares affected condition/resource copies and the replacement receipt before assignments. This fixes a pre-existing late receipt-copy failure on missing prior resources that could partly mutate a malformed source. Valid states retain exact identities, member wear/faults, resource histories and receipt behavior. Full topology validation remains an existing source precondition; this step does not claim new binding/location index validation.

## Checks and measurement

Prior full harness revision, corrected sparse algorithm, Unity 6000.6.4f1 Windows-target batch: **1,375 focused identity/checkpoint checks passed**. The combined wrapper passed **72 existing isolated save/load/recovery checks**, then exited **0**. Negative import/future-schema/corrupt-primary log messages are expected asserted recovery cases, not hidden failures. Command: `unity run <repo>/Game --timeout 900 --log-file <repo>/reports/save-refactor/S02/editor.log --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.StateCommandCheckpointVerification.RunPartIndex`.

Five warmed samples per workload, fixed 64 requested IDs; construction/disposal + lookup median at 30,000 source parts:

| Placement | List.Find | Full map | Sparse map |
| --- | ---: | ---: | ---: |
| First 64 | 0.1261 ms | 2.3960 ms | 0.0191 ms |
| Last 64 | 109.8497 ms | 2.3862 ms | 1.7679 ms |
| Shuffled 64 | 53.6736 ms | 2.3766 ms | 1.9457 ms |
| Missing 64 | 16.6973 ms | 2.4608 ms | 1.8191 ms |

Sparse maps retain 64 entries (zero found entries for all-missing queries) instead of 30,000. The 24-byte/key dictionary structural estimate is a lower bound excluding alignment, capacity and object headers; source-ID payload bytes are referenced, not newly allocated copies. Temporary requested-ID sets and source-list reachability are not heap-profiled. The index still retains the source list indirectly until synchronous disposal. No phone/GC/total-heap/frame-tail guarantee follows from these desktop lookup microbenchmarks.

Independent Python CSV validation passed 16 workload/group rows x25 columns, exact request/repeat/retained-entry counts, finite values and median<=sample-max. The concise versioned `S02-matrix.csv` records the actual results. No p95/p99 claim from five samples. This is algorithm evidence, not total checkpoint or whole save latency.

Raw artifacts are under `reports/save-refactor/S02/`; the test-only wrapper routes existing SQLite regression into separate disposable fixtures without overwriting the pre-existing dirty SQLite report. No active player save or audio/physics change is involved.

S00/S01 contracts and legacy JSON/cloning costs remain open until their named conversion steps. Index work alone does not satisfy final no-clone/no-runtime-JSON gates. No packaged build/UI acceptance or physical phone timing is claimed here.

## Preserved initial result and correction

The first full-map integration passed **1,358 focused checks + 72 existing storage checks**, exit 0, but the equal 64-query workload exposed a common-case regression. At 30,000 parts, full build + first-64 lookup took 2.2459 ms median against 0.1274 ms for List.Find; late-64 lookup improved from 111.8846 ms to 2.2587 ms. These are five warmed Editor samples, not phone or frame-tail measurements. Raw files are preserved as `*-full-map-initial` under S02. High review held the checkpoint and required sparse capture rather than accepting the common-case regression.

Sparse capture now enumerates bounded requested IDs, scans the current candidate prefix once, and allocates only found requested mappings. Its early exit assumes prevalidated source IDs/topology; strict full Build remains available for full-index invariant checks. Untimed preflight checks assert exact counts; fair timed comparisons include construction/disposal plus the same queries with no extra sparse-only LINQ assertion. Corrected focused execution/timings above remove the front-of-list regression; storage completion passed; independent final review passed; local commit closes the checkpoint.

Final raw receipts: `S02/owned-part-index-checks.txt` (1,375), `S02/legacy-storage-checks.txt` (72), and `S02/editor.log` (successful batch exit 0). No compiler errors. Static/staged path checks and final Luna-high verdict are required before the local commit. No interface change: visual review is not applicable to this infrastructure step.

## Supplemental final-source preservation check

The reviewer identified an assertion gap: the first focused suite did not freeze/assert the car thermal clock. The released harness now snapshots that clock and car identity values before operations, checks every rejected/read-only operation leaves the clock unchanged, and deliberately applies a strictly earlier valid incoming clock to verify `max(prior, incoming)` while preserving the receipt's existing incoming-clock semantics. No production or benchmark algorithm changed after the complete run.

Fresh final-source `OwnedPartIndexChecks.RunPreservationStandalone` passes **1,067 checks**, exit **0**, Unity 6000.6.4f1 batch Windows target. This focused subset overlaps the earlier complete run; counts are not added as independent coverage. Raw: `S02/checkpoint-preservation-checks.txt` and `S02/editor-preservation.log`. Command matches the earlier Unity invocation with this executeMethod and the preservation log path. The earlier 1,375+72 complete-run artifacts and 16-row timing CSV remain unchanged. Luna-high **PASS** is recorded in `docs/save-refactor/S02_REVIEW.md`; the local commit closes this bounded checkpoint.
