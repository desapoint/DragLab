# Commerce and class performance — 2026-10-22

Status: technical regression checkpoint passed; refreshed platform builds in progress with the following requested shifter control. User acceptance pending.

This is a measured prerequisite to the requested commerce refinements, not a rewrite of the approved architecture plan or a new worker migration.

## Reproduced costs and changes

- Class/PP estimation runs the same full quarter-mile simulation as before. Constant enum-to-string conversions inside component search predicates and repeated string-keyed component lookups were occurring every physics tick. MechanicalStress now maintains a typed category index, refreshed with checkpoint bindings. Constant slot names are compiled constants. No physics formulas, timestep, wear rates, RNG, fuel/heat behavior or probe duration changed.
- The dealership and owned-car rating workers held results until the entire batch finished. Completed immutable results now publish individually through per-batch concurrent queues on the main thread. The existing workers still receive prepared managed captures; UI, Unity asset access and authoritative game state stay on main. Old-generation queues are detached; physical-signature checks and per-car caches remain.
- Inventory browser sale labels were creating binding valuation quotes for every spare. Those quotes validated and snapshotted the whole profile repeatedly. A synchronous price view validates the common context once and runs the same valuation arithmetic per asset. It returns amounts only; confirmation still captures full immutable quotes and validates ownership, resources, policy, clock and staleness. Full quotes now reuse one profile serialization for their fingerprint and snapshot instead of serializing twice. Cart totals reuse the same read-only pricing context for their sale rows.

## Measured desktop diagnostic

Unity 6000.6.4f1, same machine, isolated profiles; timings are diagnostic examples, not Android guarantees. The final manifest retains exact receipts.

| Workload | Before | Initial optimized result |
| --- | ---: | ---: |
| Full class probe, current car | 777 ms | 382 ms |
| Static inspection | 10.76 ms | 1.98 ms |
| 1,000 simulation steps | 334 ms | 166 ms |
| 100 fitting part comparisons | 549 ms | 313 ms |
| 24 binding sale quotes vs 24 display values | 3,782 ms | 46 ms |
| Inventory browser with 24 spares, optimized | — | 31 ms |

The 24-price comparison uses identical valuation formulas and checks every displayed value against its binding quote. Part conditions vary across 25%, 50% and 75%. The unoptimized inventory path would create those binding quotes merely to draw labels.

Remaining measured costs are explicit: catalogue creation is around 0.7 s on this machine, whole-profile transaction preparation/validation and durable saving still have a cost, and full cold block previews are around 0.4 s. A real runtime purchase in the responsiveness suite took 760 ms including its durable save. This checkpoint does not claim incremental command working sets or asynchronous command publication are implemented; those remain in ARCHITECTURE_PERFORMANCE_PLAN.md. No state-safety checks were removed to reduce that number.

## Exact simulation regression fixture

`tools/fixtures/menu-physics-v22.json.gz` contains eight pre-optimization prepared hardware/condition inputs (not player saves). `menu-physics-v22.csv` holds SHA-256 hashes of their complete final CarState, physical components and inspection output. Cases cover 3/4/6/8 cylinders, FWD/RWD/AWD and petrol/diesel. The optimized run must match every byte represented by those hashes, including wear, fluids, cuts, pressure and event state. The diagnostic still runs the complete probe and does not use a simplified physics model. Raw waveforms/screenshots/logs and diagnostic snapshots remain local.

Run with Unity CLI: execute DragLab.Runtime.MenuResponsivenessChecks.Optimized. Baseline is a before-edit diagnostic capture; it does not overwrite the versioned golden hashes.

## Verification

- Pinned eight-case exact physics parity: passed, including the final compressed-fixture run (Unity exit 0).
- Varied-condition display price parity and read-only profile check: passed.
- Native responsiveness: 76 checks, zero runtime errors; includes tab/cache reuse, HP/torque sort, draft rollback and a durable purchase/reload.
- Native dealership: 271 checks, zero runtime errors; includes progressive class publication, disclosure, filters, comparisons and compact/4K layouts.
- Final native cart: 914 checks; purchase: 893 checks; full workshop/block roster: 1,726 checks. All passed with zero runtime errors.
- Focused valuation: **915 checks passed** for city-three (petrol I3), fjord (diesel I4) and super-v8 (petrol V8), plus all global quote-safety assertions. Includes condition/member failures, ownership/resource/tune invalidation, expiry, policy/catalogue changes, detach/retained assets, recovery and unsettled-run guards. Output is isolated from historical phase archives.
- Refreshed platform build receipts follow the requested shifter control.
- The optional expanded-roster valuation sweep was deliberately bounded before completion; its 4,850 recorded passing assertions are preserved locally and are not a full-suite pass. A selected petrol/diesel fixture run retains all per-model and global quote-safety assertions; the default test still covers every model.

An earlier workshop run completed 1,707 assertions without runtime errors but hit its old six-minute overall budget after synchronous roster-wide block fixtures. It was not marked passing. Workshop/block-only probes now have a bounded 20-minute suite budget, retaining all model coverage; interactive latency remains measured separately. ValuationVerification accepts an isolated output directory so reruns preserve historical phase archives.

## Review and scope

Review the commerce screens through the companion commerce-flow report. Physical Android performance/interaction and user visual acceptance remain separate gates. No later architecture/workbench phase is advanced and the debug bench is unchanged.

The additional shifter, false-start exit, engine-listening and warning/shop requests are recorded in docs/UX_REFINEMENT_QUEUE.md for individual reviewable checkpoints.

Focused reproduction: set DRAGLAB_VALUATION_REPORT to an isolated output directory and DRAGLAB_VALUATION_MODELS=city-three,fjord,super-v8 before executing DragLab.Editor.ValuationVerification.RunStandalone. Omit the model variable for the unchanged full-roster default.
