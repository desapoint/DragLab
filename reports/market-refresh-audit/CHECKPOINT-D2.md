# Checkpoint D.2 — fix unbounded catalog grouping work

## Local failure

The post-D.1 packaged Windows held-writer check still failed in the **third paid race**:

```
MEASURE: finish paid refresh / max main step ms 55,270 / max stage parts/layout grouping / max frame ms 69,612 / max camera step 0,190
Checks: 63 / runtime errors: 0
FAIL: Finish preparation is split into bounded main steps and camera motion remains continuous
```

D.1 split within a group's per-vehicle fitting operations but left the **entire `GroupBy(...).OrderBy(...).ToArray()` construction within one MoveNext**, now measured at 55.3 ms. That was the remaining problem; increasing the 50 ms limit would conceal an actual hitch.

## Change

The same authored JSON equivalence keys are grouped into private `GroupDraft` buckets **one definition at a time**, yielding when a 2 ms soft CPU budget expires. A `SortedDictionary` maintains groups ordered by their minimum ordinal definition ID as the original LINQ implementation did. Each group's IDs are sorted ordinally before publication. The minimum-ID definition is selected identically to the old code. Fitment, maximum set counts, RNG streams, stock allocation, save and UI are unchanged.

As before, no candidate is visible before `GetSteps` completes. A separate yield also separates the final grouping batch from first fitment. `NewSupplyChecks.LayoutRegression` now checks that grouping actually yields multiple times and that the final grouping, representatives, IDs, fitment/maximums and direct cached path match the original implementation.

**Soft budget caveat:** a single dictionary lookup on a long JSON product key may exceed 2 ms and cannot be preempted, but the known 55 ms single `GroupBy` call has been removed. Verify frame pacing, not just the presence of yields.

## Local validation (stop before Checkpoint E)

```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName market-heldwrite-d2-20261019-a
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-baseline-d2-20261019-a
```

Both held-write cases must pass with the original 50 ms threshold, zero runtime errors, exactly-once reward/receipt and intact persisted market epochs. Compare max frame, max settlement step, pending frames, and third-race settlement time with Checkpoint C. Further modifications require local validation results.
