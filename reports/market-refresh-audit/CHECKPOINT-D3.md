# Checkpoint D.3 — restore exact LINQ market ordering

## Local validation failures

After D.2 the user's Unity editor checks reached runtime but failed:

- `NewSupplyVerification`: `Staged layout exactly matches the former fitted grouping/max-set algorithm` in `NewSupplyChecks.LayoutRegression`.
- `PartsMarketVerification`: `987654 identical seed/epoch produces exact stock including identities`.

The D.2 incremental replacement used the minimum **ordinal** ID both to select the representative definition and to order groups. The original implementation had two comparers:

```csharp
catalog.parts.GroupBy(d => keys[d.id], StringComparer.Ordinal)
    .OrderBy(g => g.Min(d => d.id), StringComparer.Ordinal)
```

`g.Min(d => d.id)` chooses the group's minimum using `Comparer<string>.Default` (locale-aware string ordering). The representative, independently, is `g.OrderBy(v => v.id, StringComparer.Ordinal).First()`. These rules differ on some product IDs and their replacement changed market group order, and thus the deterministic selection stream.

## Correction

A private group draft tracks **two** IDs:

- `orderingId`: minimum under `Comparer<string>.Default` for its position in the `SortedDictionary<...,StringComparer.Ordinal>`.
- `definition`: minimum under `StringComparer.Ordinal`, as before, for fitment, quantity and the selected representative.

The group and definition keys are still built in <=32-definition or 2 ms soft batches, then fitted stepwise. The full layout is published only after completion. RNG and stock algorithms have not been modified. Tests and assertion criteria have **not** been weakened. The direct regression tests compare exact grouping and deterministic market output.

## Local gate

```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName market-heldwrite-d3-20261019-a
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-baseline-d3-20261019-a
```

Require exact seeded stock/identity parity, both editor suites, paid held-writer maximum step <50 ms, no runtime errors, exactly-once rewards and durable recovery before proceeding to Checkpoint E. Unity has not been run remotely.
