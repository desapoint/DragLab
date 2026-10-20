# F.6 — Staged settlement normalization (checkpoint 1)

Base: `main` after the validated F.5 merge `eb729ec8`.

## Problem and observed baseline

The F.5 packaged-player fixture still reports about 28–39 ms per
`settlement/normalize` main-thread step. Paid-third max frame: 45.7 ms;
whole settlement: 985.5 ms. F.6 targets frame responsiveness **rather
than** necessarily reducing the total CPU work or SQLite time.

## Changes

- One authority for `MechanicsOwnership` normalization. The synchronous
  `Ensure` API still drains it completely in one call, with **no per-part
  yield overhead**; no save format, migration, validation rule or execution
  ordering changes.
- `EnsureSteps` is an internal, paced iterator that runs only on the
  private, unpublished race-settlement candidate. It yields after
  common metadata, each owned car, spark migration, each part, vehicle
  market, parts market and final validation.
- `PrototypeGame.FinishSettlement` uses those steps at **both**
  normalization sites. `AdvanceFinishSettlement` already enforces its
  2 ms inter-yield frame-work budget and preserves held-writer handling.
- Packaged-player `FinishPacingChecks` compares exact JSON from the
  original synchronous and new staged normalizers; also asserts the source
  was not touched and logs the maximum normalizer step and slice count.

## Limitations and safety

A single indivisible operation such as `MechanicsOwnership.Validate`,
`SparkPlugMigration.Apply` or a large `catalog.Resolve` call can still
exceed the 2 ms budget; iterator yields only occur *between* operations.
No improvement is claimed until the packaged-player fixtures run. A
second F.6 checkpoint may be needed to split any remaining long validation
or other expensive operations after measuring the precise stage.

The candidate is private until the durable SQLite commit; a failure or
interruption disposes the iterator without publishing partial changes.
Normal save loading, migrations and ordinary transactions remain synchronous.

## Local Windows acceptance

```powershell
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action SqliteStateVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly

.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName settlement-f6-20261019-a
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName settlement-held-f6-20261019-a
```

Compare `MEASURE: market baseline paid third / max settlement step` and
`MEASURE: finish paid refresh / max normalize step ms` to the F.5
baseline. The existing durable-write, seeded-market parity, ownership and
save-reload checks must remain green.


## F.6 second checkpoint: distributed full ownership validation

The first packaged-player test verified exact settlement behavior (83 market-baseline
checks, 75 held-writer checks; zero runtime errors) but measured a **44.961 ms**
paid-third main step and **54.823 ms** maximum frame. The old full
`MechanicsOwnership.Validate` call was the slow step.

The second checkpoint now runs **the same ordered validation rules** through
`ValidateCore`, a shared read-only iterator. Normal transactions consume it
synchronously via `Validate`; only an unpublished finish candidate consumes
the paced `ValidateSteps`. Early-exit reasons are preserved word-for-word,
including invalid cash, duplicate part identities, and stale/invalid market
epochs. No validation rules are deleted, skipped, or moved after persistence.

Validation yields between cars/slots, stored engines, owned components,
element identities, inventory, market, tire/wheel specifications and run
checkpoints. `NativeMenuVerification.FinishPacing` now records the five
slowest normalization stages and checks exact synchronous/staged rejection
for several intentionally corrupted profiles.

**Not yet retested on Unity**. It remains possible that an indivisible
subvalidator such as `VehicleListings.Validate`, `SharedInventory.Validate`
or `PartsMarket.Validate` exceeds the 8 ms target. Run the packaged-player
benchmark again, inspect the five slowest labels and only then consider
splitting those specific functions. No improvement is claimed for this
follow-up until those measurements are available.

## F.6 third checkpoint — off-thread complete validation (current)

The second player run completed **83 market** and **81 held-writer** checks
with zero runtime errors on the successful rerun. Paid-third maximum main
settlement step improved from **44.961 ms** to **28.066 ms**, but the
remaining hot spots were `VehicleListings.Validate` (up to **29.393 ms**)
and `PartsMarket.Validate` (up to **14.549 ms**).

Instead of adding nested per-offer yield checkpoints, the current revision
removes staged per-rule validation and restores the original synchronous
`MechanicsOwnership.Validate` method as the single validation authority.
Regular transactions and loading still invoke it synchronously.

Only finish settlement changes:

1. Normalize the unpublished settlement candidate on Unity's main thread,
   retaining the existing incremental metadata/car/part work but skipping
   its inline full validation.
2. Create one `MechanicalCatalog.DetachedNewSupplySnapshot()` on main.
   This contains independent managed vehicle specifications, authored
   definitions and indexes, with no Unity asset loading from the worker.
3. Set the private candidate's nonserialized `catalog` reference to that
   detached snapshot, then run the unmodified full `Validate` using
   `Task.Run`. No other code touches this candidate until completion.
4. Restore its original catalog on main even if the worker fails. Any
   validation error throws **before** checkpoint application, stock refresh,
   save dispatch or profile publication (as appropriate for that pass).
5. Repeat the complete worker validation after market generation and second
   normalization. Existing revision checks, checkpoint writer drain, SQLite
   durable commit and publication order remain intact.

This is the same two validation points used before F.6. The worker does
not perform normalization, market generation, serialization or profile
writes, and must not use Unity's `Resources`, `Application`, `Time`,
`UnityEngine.Object`, or main-thread Unity APIs. The validator call graph
uses managed state, `MechanicalCatalog.Resolve` on the detached catalog,
and read-only ownership/market checks. Do not swap in the authored catalog
on the worker or make its helpers access Unity objects.

The test fixture now runs detached validation on `Task.Run`, verifies
different managed thread IDs, confirms exact error results and unchanged
candidate JSON for valid and malformed profiles, and asserts that the
two **actual settlement** validation passes complete off the main thread.

**Performance caveat:** Snapshot creation is still synchronous on the main
thread. It is explicitly measured as `settlement/catalog snapshot`;
the full worker CPU is reported as `validation worker CPU`. CPU duration
need not decrease; the goal is to remove validation from the main-frame
critical path. Do not claim any worker performance result until Unity tests.

## Current local acceptance

```powershell
git fetch origin
git pull --ff-only origin perf/f6-staged-settlement-normalization
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action SqliteStateVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName settlement-f6-worker-a
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName settlement-held-f6-worker-a
```

Require exact saved profile/market reload, one payout, stable wear/IDs
and no runner UI stalls while the writer is held. Compare paid-third max
step and max frame with F.5 and the prior F.6 runs; separately inspect
`settlement/catalog snapshot` for a newly introduced main-thread spike.

## F.6 worker fixture observation correction

The first worker run passed all detached validation parity checks and
reported the held-writer practice main-step **8.544 ms** and maximum frame
**23.800 ms**. The test then failed because the old `FinishMeasurement`
observer recorded zero `settlement/normalize*` **frame-final stages**.
This was a test accounting defect: the existing 2 ms frame scheduler can
consume every normalization yield before finally reporting a later
`settlement/validation wait` boundary, so stage-at-end-of-frame cannot
reliably count normalizer operations.

The new `FinishNormalizationStep` observer is called on the main thread
*for each actual* `MechanicsOwnership.EnsureSteps` iterator advancement
in both normalization passes. It measures only the corresponding
`MoveNext` work, independently of where the frame ended. In the held
writer fixture, assert multiple stages including metadata and parts-market
normalization, preserve both worker-thread assertions, and report total
`normalize steps` and peak per-step CPU cost.

This instrumentation does not alter normalization ordering, validation
rules, rewards, SQLite persistence, worker catalog ownership, or the frame
scheduler's 2 ms time budget.

Rerun after rebuilding the packaged Windows player using fresh disposable
fixture:
```powershell
git pull --ff-only origin perf/f6-staged-settlement-normalization
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName settlement-held-f6-worker-c
```
