# Checkpoint D.1 — cold catalog fitting frame-pacing repair

The local `FinishPacingOnly` packaged-player test passed the writer isolation and recovery gates but failed on the **second (paid-refresh) scenario**:

```
MEASURE: finish paid refresh / max main step ms 54,624 / max stage parts/layout group / max frame ms 69,188
FAIL: Finish preparation is split into bounded main steps and camera motion remains continuous
```

This is a genuine frame-pacing regression, not a license/build error and not grounds for increasing the existing `maxStep < 50 ms` limit.

`MarketCatalogLayout.GetSteps` previously yielded only **after an entire group had been fitted against every vehicle specification**. A single grouped product could therefore exceed the intended 2 ms cooperative budget. This repair inserts an explicit yield after the initial grouping operation and checks the 2 ms soft budget **after each individual vehicle fit/count operation**, retaining group-local `fitting` and `maximum` state between steps.

No RNG call, product ordering, fitment predicate, numeric maximum, stock publication, worker mutation, or SQLite transaction was changed. Layout candidates remain private until all groups are complete. The budget is still soft: one individual `Fits` or `Count` call cannot be preempted; the next local run will show whether a deeper refactor is needed.

**Local validation stop gate:**

```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName market-heldwrite-d1-20261019-a
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-baseline-d1-20261019-a
```

Confirm both paid/practice held-write cases pass, that max third-race main step remains under 50 ms, the seeded epoch and exactly-once rewards survive reload, and total settlement and frame-pacing regressions improve or are investigated. No subsequent checkpoint until local gate passes.
