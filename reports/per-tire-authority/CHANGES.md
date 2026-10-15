# Tire authority and polymorphic component elements (2026-10-14)

## Follow-up: remaining individual-component types (save v12)

- Every category modeled by `EngineElements.Supported` has a concrete `EngineElement` subtype: pistons, connecting rods, piston rings, bearings, valves, valve springs, valve seals, followers, ignition assemblies, spark plugs, glow plugs, injectors/fuel-system units and wheels. Fitted tires remain their own `TireCondition` class.
- `EngineElements.Create` is the central factory. Newly owned assemblies, retail loose units, individual replacements, wheel bundles, used-market dealer stock, native part previews and saved-setup preview members now preserve the same concrete type. Cross-category members are rejected by `EngineElements.MatchesCategory`, `FaultAllowed` and inventory boundaries.
- Dedicated subtype validation now checks nonvalve positions, valvetrain topology and physical wheel corner/role. Existing shared fault and wear fields remain on `EngineElement` because the physics and UI still use that contract.
- One-time ignition-to-spark-plug splitting copies member identity, fault and wear into a `SparkPlugElement`, rebinds its destination specification and rejects mismatched subtype use during checkpoints.
- Added `AxleWheelVerification` coverage for all 13 non-tire subtype JSON round-trips and deep copies, cross-category rejection, topology violations, and atomic stored/installed replacement for rods, valve springs and spark plugs.
- The previously generic clutch-fluid fields have been moved into a separate `ClutchConditionState` serialized only for clutch parts, with neutralized tire packs and defensive inventory validation; the traditional accessor names still work.
- Save version raised to **12**. Older test profiles deliberately use the existing **blocking UI Toolkit reset-required dialog** with timestamped backup/archival; do not silently migrate older profile JSON.

**Still outstanding:** Runtime Unity compilation, fresh-profile test execution, shop/checkout and checkpoint integration verification. Generic `ComponentCondition` still owns some other cross-category summary/service fields, and the `EngineElement` base still carries cylinder/fault/wear fields for all its subclasses. Moving *every* shared simulation field into category-specific payloads would be a broader, separately testable data-contract change, not completed by simply adding concrete subclasses. Do not merge before Unity tests.


Branch: `refactor/per-tire-authority`. **Unity compilation and play-mode regression suites have not been run. Do not merge until verified.**

## Implemented

- Tires, pistons, piston rings, valves and wheels can now use specialized concrete classes via `EngineElement : PartElement`, with `[SerializeReference] ComponentCondition.elements` preserving the runtime type. Other supported engine components continue to use `EngineElement`.
- Each fitted tire is a `TireCondition` subtype in **the same serialized `condition.elements` array**. `condition.tires` is now a nonserialized, cached typed accessor for existing physics, inspection, inventory and UI code. There is no separate serialized `condition.tires` field.
- Loose inventory/dealer tire units now persist one typed `element`; `LooseEngineElement.tire` is a nonserialized accessor, not a second tire condition. The individual tire stores its own ID, specification, wear, mileage, slip energy, temperature, running time and failure.
- Fixed duplicate tire IDs in dealer validation, double tire-fault counting in maintenance and duplicate generic tire/cylinder rows in native stats.
- The existing per-corner `TireDynamics`, `TireFitments`, race checkpoint protections, compound differences, pricing and stored worn-tire history remain wired to the individual tire type.
- Inventory supports a single transaction swapping **individual, Front, Rear or All** stored tires. The native selection dialog can preselect a pair or set and allows changes per corner; displaced tires return to inventory. Tests cover worn-asset preservation and rejecting repeated unit IDs.

## Breaking saves and native UI reset dialog

- **Save version 11** intentionally rejects older part formats instead of attempting automatic migration.
- When `ProfileStore` cannot load an incompatible save, the game opens DragLab's existing **UI Toolkit `NativeMenuShell` modal** on startup. There is no IMGUI fallback for the reset flow.
- The modal blocks navigation and cannot be dismissed with Escape/close. **Reset and start new game** is an explicit confirmation.
- The reset path archives the existing database, backups and journal under unique `.before-reset-<timestamp>` suffixes, then creates a fresh save and reloads the scene. If reset fails, it reports the error and attempts to restore the archived originals.
- No automatic destructive reset occurs on load.

## Unity regression coverage added (not yet executed)

- `AxleWheelVerification`: typed tire `JsonUtility` serialization/deserialization, polymorphic piston/valve round-trip, typed loose tire JSON with no duplicate condition payload, independent copies, rear-axle worn tire swapping, displaced tire identities and invalid duplicate swap rollback.
- Existing `TireVerification`, `TireFailureVerification`, checkpoint, valuation, inventory and native menu checks should be rerun.

## Remaining architectural scope

The parent `ComponentCondition` still serializes generic mechanic-level fields (health, deposits, clutch data, etc.), neutralized for tire sets. These are **not** category-specialized yet. Also, tire subtype currently derives from the existing `EngineElement` class to preserve the large installed-element API; this retains generic engine-element fields on tire members. A later broader condition/element API revision would be needed to completely remove those irrelevant fields, and should be undertaken only after Unity verifies the current breaking serialization change.

## Recommended validation

Run on the Unity-configured Windows environment:

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

Test both a **fresh profile** and an **incompatible prior profile**: verify the native dialog, refusal to continue until confirmation, archival of old data, successful reset, then one-corner and axle/set swaps with mixed/worn tires, races and checkpoint persistence.

**Verification status:** GitHub source changes committed; static inspection only. No claim of successful Unity compilation, save round-trip or runtime tests.
