# Checkpoint F.2 — Retain authored catalogs and completed product keys

## Problem
`PrototypeGame.StartRace` previously passed `MechanicsTransactions.Catalog()`
to `MechanicsOwnership.Ensure`. That constructor loads authored resources
and produces all-new `PartDefinition` instances on **every race entry**.
Both market caches are weak-keyed by object identity, so completing a menu
warmup before racing could not help the new catalog. The measured 1.85-second
product-key stage included iterator/frame waits, not solely CPU.

## Behavior
- Race entry keeps the profile's existing authored catalog. Only a profile
  with no catalog falls back to `MechanicsTransactions.Catalog()`.
- `MechanicalCatalog.RebuildIndexes` advances an authoring revision.
  Official author overrides already call this method. Wear, owned part
  condition, cart changes, seed/epoch and used-stock creation do not.
- `MarketCatalogLayout.WarmSteps` publishes a complete canonical ID->key
  snapshot once per catalog revision, after verifying the exact ordered part
  references. No partial dictionary becomes visible.
- On after-race incremental stock refresh, `GenerateSteps` reuses those
  exact strings on a cache hit, rather than calling `JsonUtility` and
  comparing every authored part again. If warmup was interrupted, the cold
  path preserves its bounded Unity-main-thread serialization and publishes
  a completed snapshot for following refreshes.
- Synchronous/editor generation still uses the original validating
  `Key` path, so direct test fixture part edits remain observable.
- Explicit authored edits **must call** `MechanicalCatalog.RebuildIndexes`
  (as `PartCatalogOverrides.Apply` already does). Direct field mutation
  without an invalidation call is not a supported way to change authored
  data while a running player's prepared catalog is in use.

All wear-sensitive supply selection remains against the actual settled
profile and detached worker, unchanged. The result actions remain disabled
until durable commit. No UI indicators have been added.

## Required Unity gates
```powershell
git switch main
git pull --ff-only origin main
.\tools\Invoke-Unity.ps1 -Action NewSupplyVerify
.\tools\Invoke-Unity.ps1 -Action PartsMarketVerify
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName market-heldwrite-f2-20261019-a
.\tools\Test-NativeMenu.ps1 -MarketBaselineOnly -FrameProbe -FixtureName market-baseline-f2-20261019-a
```

Compare "product keys reused" / "product keys" and full after-race
stock wall time with the F.1 baseline (product keys: 1853.982 ms,
third-race settlement: 4301.703 ms). Verify the new-probe catalog
reference stays stable at race entry, full seeded stock and used/engine
identity parity, the held-writer test, and frame pacing (<50ms main
settlement step) before closing the checkpoint.

The repository integration does not execute Unity locally.
