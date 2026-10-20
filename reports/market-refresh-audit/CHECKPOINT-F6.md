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
