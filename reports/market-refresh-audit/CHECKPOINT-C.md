# Market responsiveness Checkpoint C — incremental catalog layout

## Confirmed pre-change measurements
The packaged Windows player on Checkpoint B reported 70/70 pass, no runtime errors, and a third paid-race settlement of 9,502 ms, including an 8,336 ms new/used/vehicle refresh. The largest single step was **295.839 ms**, at `parts/layout and selection`. The `product keys` phase of 4,834 ms includes frame gaps across iterator yields, not 4.8 seconds of CPU time. Smaller CPU-only samples include eligible-pool preparation 66 ms and worst product selection 60.6 ms.

## Change scope
- Split the old one-shot `MarketCatalogLayout.Get` fitting/group calculation into `GetSteps`, yielding roughly once per 2 ms of measured execution time between groups. The grouping is built privately and published only when complete; consumers never access half-built groups.
- Warm the entire authored-key/layout cache after startup on **non-race frames**, via `MarketCatalogLayout.WarmSteps`. The warmup is independent of the shop and UI; it does not reroll stock or draw RNG. A normal progression reset reuses the same authored catalog.
- Retain the synchronous `Get` API for existing tooling and non-incremental callers; it now drains the identical generator.
- During paid-race refresh, use the stepwise path even if cold (e.g. player rapidly races before warmup completes), removing the previous one-shot layout/build stall. An already warmed layout completes immediately after validating its keyed product revision.
- No worker, no navigation/UI, no persistence, no category weights, no save contract and no RNG changes in this checkpoint. Stock remains visible only after the existing complete settlement.

## Validation contract
`NewSupplyVerify` now tests atomic staged layout publication, exact authored grouping/ID order, max-set/fitment equality with the old algorithm, and synchronous cached reuse. The persisted baseline matrix still checks deterministic used parts, engines and identities. `PartsMarketVerify` validates full market regression. Both must pass before another checkpoint.

```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-baseline-c-20261019-a
Select-String -Path .\reports\player-fixtures\market-baseline-c-20261019-a\verification.txt -Pattern 'MEASURE: market baseline','FAIL:','Checks:'
```

Manual cold-start: after startup/first purchase, enter Build immediately; ensure no missing stock or exception. Manual repeat: make a third paid race soon after launch, before warmup is ready, and ensure no visual stutter or absent parts. The cache warm is main-thread-only and scheduled outside racing, so no concurrent access to authored parts. As with prior checkpoints, no Unity test was run remotely; stop and review the local results before Checkpoint D.

## Expected outcomes and limits
Primary measurable gate: the third-race worst `parts/layout and selection` step should no longer contain a 300 ms fitting build. The 9.5 s refresh latency may remain due to `parts/product keys` and hundreds of one-step-per-frame yields. Other spikes (`delivery/product choice`, `delivery/pool preparation`, persistence normalization, shop entry) remain measurable; target them only after local comparison, before/within background-worker and settlement UX checkpoints as needed. The 2 ms budget is **soft**: one expensive group or initial grouping operation may exceed it, so measure max frame rather than claiming a guaranteed cap.
