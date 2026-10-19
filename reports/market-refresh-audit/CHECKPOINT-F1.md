# Market Checkpoint F.1 — Isolated new-stock generation

## Scope
The result UI and existing gated actions remain unchanged. On each eligible
third paid-race market refresh, the used-supply budget is still calculated from
the identical seeded initial new-stock plan, and Unity-dependent used/engine
construction remains on the main thread.

After used dealer offers have been generated, the entire remaining **new-parts**
phase runs in one detached task: old-stock and nine-race reservation carry,
owned-platform matching, member/used-part wear coverage, 24–44 new delivery
opportunities, and final fitting guarantee. The worker uses a private snapshot
of every part definition and vehicle spec; the settlement profile itself was
already cloned from the persisted live profile, and is exclusively owned by
this task until join. No Unity Resources, JsonUtility, UI or live profile
reference is accessed by the task. The candidate's authored catalog reference
is restored before engine-offer generation and durable SQLite validation.

Upstream product-key serialization stays on Unity's main thread because
JsonUtility is Unity-bound. Catalog grouping/fitting and initial product
selection/quantity generation already run on workers as of Checkpoint E.
The new measured `new complete worker CPU` covers preparation, wear needs,
delivery and fitting guarantee, and is deliberately **not** a complete stock
epoch or SQLite measurement.

## Correctness
- Keep source seed, iteration order and all RNG streams unchanged.
- Preserve previous-epoch stock, discovery metadata and reserved cart units
  while generating into a private settlement profile.
- Keep all market parts, used dealer IDs, complete engines, reward receipts
  and car/part epoch aligned with synchronous generation.
- Preserve buttons disabled until validated SQLite commit, with no spinner.
- Fault propagation joins/observes the worker before retry and never
  exposes partially updated stock.

## Validation
```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName market-heldwrite-f1-20261019-a
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-baseline-f1-20261019-a
```

Compare `new complete worker CPU` with the earlier ~407 ms isolated
new-stock stage evidence; aim below 500 ms on the same hardware. Also compare
end-to-end third-race time and frame pacing to Checkpoint E's 4301 ms and
62.58 ms, since a faster worker is not proof of a faster validated settlement.
No local Unity runtime is available via this GitHub integration.
