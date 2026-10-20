# Checkpoint F.5 — Reusable runtime indexes for market generation

Branch: `perf/f5-runtime-market-indexes`. Base: `d1f18f7` (validated F.4).

## Scope

- New `RuntimeIndexSlot<TSource,TIndex>`: weak-keyed, transient lifetime/version container for completed immutable derived views, designed to be reused by future ownership/inventory/inspection indexes.
- `NewSupplyCatalogIndex.ForDetached`: exports immutable category membership by definition ID after validating the warmed classification against current authored definitions. The detached snapshot carries **no live `PartDefinition` references** and is never serialized.
- `PartsMarket`: obtains the completed snapshot on main and explicitly passes it to the isolated new-supply worker. If unavailable or stale, the prior worker-side lazy category matching remains the fallback.
- `NewPartSupply.DeliveryPool`: stores one stable owned/unowned partition of eligible new products per refresh, preserving original product order and exact 76/24 selection/empty-branch RNG behavior.
- Editor regressions cover lifecycle and invalidation, cold/partial publication, stale authored mutations, exact category/RNG parity, all 44 deterministic delivery opportunities, and full synchronous-vs-detached new/used/engine market JSON parity.

## Invariants

The F.4 SQLite schema, chunking, market epoch, stock IDs, seed streams, used dealer rows, quantities, player-owned parts, wear, cart holds and transaction behavior are unchanged. No index is serialized into `PlayerProfile` or SQLite. Only immutable definition classifications are shared across candidate scopes; delivery stock remains private per candidate.

## Local acceptance tests (not run in this environment)

```powershell
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action SqliteStateVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly

.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-index-f5-20261019-a
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName market-index-held-f5-20261019-a
```

Compare to F.4 paid-third fixture: **209.859 ms** new-stock worker CPU, **72.346 ms** delivery pool preparation, **91.570 ms** product choice, **336.104 ms** SQLite save worker and **1,048.794 ms** result-to-commit. Evaluate both frame times and CPU, not only overall wall time. No measured speedup is claimed until those benchmarks run.
