# Market responsiveness Checkpoint D — isolated delivery batch + frame-budget settlement

**Scope.** Based on Checkpoint C's packaged Windows result: 70 checks / 0 errors; worst third-race settlement step improved from 295.8 ms to 70.1 ms, but total time rose from 9.50 s to 10.85 s across 642 pending frames. This checkpoint removes wasted frame boundaries while isolating a pure-managed part of new stock delivery on one worker. It deliberately does **not** move vehicle offer generation, market fitting, Unity serialization, live profile mutation, SQLite save, or results UI onto a worker.

## Safe worker boundary

- `VehicleListings.GenerateSteps` requests offloaded delivery **only** for incremental after-race refresh. Initial shop initialization, synchronous reset and editor market generation preserve their synchronous path and original RNG. 
- The private settlement candidate is produced by `PrepareFinishSettlement` through `CopyWithPreferences` and JSON cloning before market generation; the live UI/race continues reading the original profile until durable commit.
- `SupplyWear.Steps` builds the candidate need graph on main before worker launch. The worker receives prepared candidate stock, new-part definitions and needs, and calls only managed branch filters, integer stock/reservation availability, weighted RNG and quantity mutation. Unlike the normal `Need.Probability`, its direct mode does not call `PartsMarket.Stock` / `ProductKey` / Unity JSON serialization; the private stock rows were mapped on main by `NewPartSupply.Prepare`.
- The worker's telemetry goes into a private list; the main iterator replays it only after the task completes. Unity API, gameplay UI and save writes remain entirely on the main/control paths.
- The iterator yields a `MarketWorkerWait` carrying the task. The main settlement coordinator retains it in `finishTask`, so the existing synchronous exceptional flush waits rather than racing the worker. A failed worker faults the private candidate; retry starts from the original profile without partial publication.
- No further stage mutates the candidate until `job.GetAwaiter().GetResult()` succeeds. Final epoch/revision check and single validated SQLite commit remain authoritative.

## Bounded main-thread pacing

`AdvanceFinishSettlement` now processes multiple inexpensive MoveNext calls within a **2 ms soft per-frame budget**. It exits early for `*wait` stages or incomplete worker jobs. Any individual synchronous step can still exceed the budget; the next profile should tell us which steps need further splitting. Normal race physics and settlement results continue to render every frame. UI action gating is unchanged pending Checkpoint E.

## Validation

```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-baseline-d-20261019-a
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName market-heldwrite-d-20261019-a
```

Check new/used/engine baselines remain identical, no double rewards, worker exceptions = 0, no Unity off-thread warnings, and compare third paid `result-to-commit`, `pending frames`, `max frame` and `max settlement step` with Checkpoint C. The normal paid/practice settlement should not regress. Additional manual tests: immediately open Build after startup, rapid Garage requests on results (still gated until E), reset during non-race cache warmup.

**Gate:** stop for local Unity validation before Checkpoint E. No Unity code execution is available to the GitHub connector.
