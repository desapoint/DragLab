# Per-tire authority refactor — branch checkpoint (2026-10-14)

Branch: `refactor/per-tire-authority`. **Not validated by Unity / not ready to merge.**

## Implemented
- `TireCondition` remains the single authoritative source for each tire's condition, distance, slip work, and temperature.
- `TireDynamics.Ensure` migrates legacy empty tire arrays once. Existing individual tires no longer inherit stale pack health/failure. A derived compatibility summary replaces aggregate wear authority, and obsolete pack slip/distance/running histories are cleared.
- Installed tire IDs are assigned during owned-asset normalization, not during read-only preview construction.
- Queued race checkpoint history checks compare exact tire identities and monotonic individual histories, not pack slip/distance totals. Tire exchanges during an active race are rejected; garage replacements occur between races. Non-tire component history validation is unchanged.
- Individual tire replacement refreshes the compatibility summary without affecting other tire histories.
- Maintenance candidates and the owned-car maintenance display list worn tires by corner rather than treating a single bad tire as an independently broken tire pack.
- Traditional inventory part-sale pricing and detailed buyer valuation use per-tire condition, individual compound, per-tire distance, and failures.
- Car/build warnings identify failed corners; native Components stats only show tire lifetime usage under individual tire entries.
- Added targeted regression assertions for a stale former pack, migration from legacy history, per-tire checkpoint growth/rewind and tire identity swaps. Updated valuation fixture to mark an individual tire failed.

## Still required for a full *no tire pack anywhere* architecture
The ownership/economy schema **still represents installed tires as four `TireCondition` objects held by a `Tires` `PartInstance`**. That container participates in `InstalledBuild`, `MechanicalCatalog` recipes, `TireFitments`, `PartsMarket`, `PartCart`, `SavedSetups`, `PaidEconomy`, `InventoryUnits` and UI adapters. The generic `ComponentCondition` still carries a **derived** summary for compatibility with generic consumers. These are NOT yet removed. A complete elimination requires a versioned four-part ownership/fitment schema and updates to all listed transactions, saves and UIs. This branch does **not** implement that breaking schema migration.

## Required validation before merging
Run from the repository on a configured Unity Windows machine:
```powershell
tools/Invoke-Unity.ps1 -Action AxleWheelVerify
tools/Invoke-Unity.ps1 -Action TireVerify
tools/Invoke-Unity.ps1 -Action TireFailureVerify
tools/Invoke-Unity.ps1 -Action MaintenanceReplaceVerify
tools/Invoke-Unity.ps1 -Action ValuationVerify
tools/Invoke-Unity.ps1 -Action CheckpointVerify
tools/Invoke-Unity.ps1 -Action RecoveryVerify
tools/Invoke-Unity.ps1 -Action BuildWindows
```
Also exercise a real existing save and separately verify one-corner replacement, tire inventory transfers, a mixed-compound build, a partially failed set, and race checkpoints before and after a garage tire replacement. Only syntax delimiter balance and GitHub diff metadata were inspected here; Unity compilation, native builds and these regression suites **have not been run**.
