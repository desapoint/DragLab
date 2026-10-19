# Market Checkpoint E — managed catalog/layout/stock work on a detached worker

## Boundary

The result buttons **stay disabled** until full validated SQLite settlement is committed. No UI status indicator and no queued navigation state are introduced. This checkpoint moves more calculations off Unity's main thread instead:

1. Compute authored product keys and reference vehicle specs on the main thread; this uses Unity JsonUtility and existing shared catalog lookup.
2. Snapshot all authored `PartDefinition` values (including cloned AWD metadata) and each reference `VehicleSpec`. Perform product equivalence grouping, per-vehicle fitment, and maximum delivery set-size calculations on a worker using **only detached managed snapshots**. `MechanicalCatalog.FitsDetached` is the same pure predicate used by the normal main-thread call.
3. On completion, return only group identity strings, counts and fitment flags, then map identities back to authored references and **atomically** publish the finished layout on main.
4. Generate the seeded **initial new-part stock plan** (selection indices, alias rows, delivery percentages and initial quantities) on a second worker using copied string IDs/integers. Publish the complete plan to the isolated candidate only after completion.
5. Existing worker executes wear-weighted new supply delivery. All Unity Resources/JsonUtility, authored vehicle offer/used stock construction, live profile mutation and verified SQLite commit remain on their original main/control paths.

The synchronous generation path is retained for initial onboarding, non-race refresh and editor verification. The incremental post-third-race path uses background grouping, fitment, initial stock and delivery. All preserved RNG streams and formerly ordered selection logic are unchanged. `GetSteps` still checks the main-thread cache before scheduling work, so non-race warmed catalogs are reused.

## Explicit safety gates

- Worker snapshots cannot see future authored catalog changes or write to live profile.
- Pending button actions remain disabled; no `ReturnToGarage`, retry or purchases before authoritative save.
- `MarketWorkerWait` is passed through the iterator, and the existing `FlushFinishSettlement` joins workers for explicit quit/suspend boundaries.
- If a worker faults, no partial result is published; the settlement retry starts from the original saved profile.
- `NewSupplyChecks.LayoutRegression` explicitly forces the cold worker branch, then compares exact identity order, representative references, set maxima and fit flags to the synchronous result.
- `NewSupplyVerification` compares the whole parts/used/engine market epoch with the synchronous path, alongside pre-existing used-stock hashes.
- The original result-button gating and held-writer tests remain unchanged. Measure both time to commit and worst frame after optimization.

## Required local Unity validation

```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName market-heldwrite-e-20261019-a
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-baseline-e-20261019-a
```

Do not proceed to other checkpoints until the local Unity gate passes. Verify same seeded stock and used/engine rows, zero worker-side Unity API exceptions, no early navigation, exactly-once reward/receipt, save/recovery parity, max <50 ms settlement step, and measure full refresh elapsed time against D.3 (6085 ms). No local Unity run is possible through the GitHub connector.
