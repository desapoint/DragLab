# S06d2b step-1 topology fixture blueprint

**Status:** Read-only fixture plan. This supplements, and does not replace or close, the full [characterization matrix](S06D2B_CHARACTERIZATION_MATRIX.md). No Editor/Unity check or preview parity result is claimed here. All topology is discovered from the current authored catalogue and physical fixture state; missing authored shapes remain open cases.

## Fixture boundary and common capture

Use `CartPreviewCharacterizationFixtures.CreateNormalized(out PlayerProfile profile, out string starterCarId, uint seed)` as the only root. It creates a disposable profile, loads `MechanicsTransactions.Catalog()`, seeds the actual vehicle stock, runs initial `MechanicsOwnership.Ensure`, and requires current global validation to pass. It also adds one real used part. It does not write a profile store and must not be followed by `Ensure` after a deliberate malformed mutation. For paired legacy/candidate execution, make independent literal test copies before either call; keep their shared `profile.catalog` reference unchanged.

Capture, in ordered form, before and after each operation: every car ID, vehicle ID, selected-car ID, engine-assembly link, installed slot binding; every engine ID, parent/storage owner, slot binding, tune, fuel and fluids; every part ID, definition, location, owner, slot, aggregate condition and each typed member's runtime type, ID, definition, role, cylinder, valve, health, severity, fault and wear remainder; tires' per-corner definitions, IDs, condition/history; every loose unit ID/type/definition/role/position/storage/condition; cash, inventory containers, dealer/vehicle stock, reservations, run checkpoints and revision. Existing IDs are exact. Only newly generated IDs use the operation/row/member bijection already required by the matrix. Record operation result, first error/exception type and exact message, plus `PartCart.Total` and preview output.

Do not normalize a malformed case. Start from a validated fixture, copy it independently, apply one deliberate mutation to the isolated input, and invoke both paths directly. Restore modified values/references in `finally` when a multi-probe case reuses a fixture. Do not alter the catalog or manufacture part definitions.

## Authored topology discovery

For every car under test, use its actual `VehicleSpec` from `profile.catalog.Spec(car.vehicleId)`, actual fitted slots/part records, and the methods that define the supported topology:

- `EngineElements.Count(spec, definition)` and `EngineElements.Ensure(spec, definition, part)` for engine-member cardinality and typed members.
- `InventoryUnits.Count(profile, car, definition)`, `Roles(profile, car, definition)`, and `Positions(profile, car, definition.id, role)` for unit purchase/position probes. Record returned arrays exactly; never assume a role string or position count.
- `profile.catalog.Find`, `Fits`, `Resolve`, and `Recipe` for authored compatible definitions and block requirements.
- `TireFitments.DefinitionId(pack, corner)`, `WheelFitments.DefinitionId(pack, corner)`, `WheelFitments.HasOverrides`, and the actual pack condition arrays for corner maps.

Discover candidate cars by enumerating authored catalogue vehicles and their actual specs, then choose a real available listing/owned car only when the fixture's actual `VehicleCollection.Buy` path can create it. For a six- or eight-cylinder case, assert `spec.sound.cylinders` is exactly 6 or 8 and that the real installed member arrays from the selected fitted definitions have those cylinder indices. Require a piston definition and a multi-valve definition with members, then retain a second role-bearing category (for example rods or valve springs) only if it exists on that same actual engine. Assert the discovered required shape before running the case. If a current actual listing cannot produce that shape, report the missing car/offer/part shape; do not synthesize a spec or alter catalogue fields.

The committed matrix requires an authored multi-cylinder/member case; it does not specifically require both six and eight cylinders. Those are optional additional strata if authored and useful to test nonconstant counts, not new acceptance gates. This blueprint does not assert either shape is currently stocked. Catalogue discovery and exact found car/definition IDs belong in the run report.

## Individual members, wheel/tire corners and brakes

For piston and multi-valve coverage, take the currently installed real `PartInstance`; enumerate its `condition.elements` in stored order and retain the exact `EngineElement` subtype and all identity/condition fields. `InventoryUnits.Positions` supplies valid member IDs. For each available role, probe one member, a valid subset, and all positions in a separate isolated fixture. Compare the chosen position order, new/displaced unit identities, and every untouched element. No test should update all elements just to simplify comparison.

For tires and wheels, use the actual four-corner maps only when the current pack has those entries. Tire corners are indexed by the existing `tire:0`…`tire:3` operation convention; wheel corners by `wheel:0`…`wheel:3`. Resolve each current corner's definition through `TireFitments.DefinitionId` or `WheelFitments.DefinitionId`, then verify each ID with `catalog.Find` and `catalog.Fits`. Record labels from `InventoryUnits.PositionLabel`; the current mapping is front left, front right, rear left, rear right. A front/rear subset uses the exact selected corner indices. Preserve each incoming loose unit's actual ID and condition and the displaced corner's complete previous ID/history; the unused corners must remain bit-for-bit unchanged. If authored overrides exist, include the mixed-corner case and record `WheelFitments.HasOverrides`; otherwise keep that row open instead of inventing overrides.

Use `InventoryUnits.SwapTires(profile, carId, assignments)` for a valid stored-tire swap only when the fixture already contains suitable real loose tires; the assignments must reference existing `unitId`s and unique corners. For install-by-cart, derive the row positions from `InventoryUnits.Positions`, not from UI labels.

Brakes are conditional. Inspect the actual installed brake pack and its member array. If there are no modeled brake members or no compatible individual-unit path, the brake-topology case is unavailable for that fixture and must be reported open. Do not project four corners just because the category name is plural.

## Block preparation and incomplete builds

Construct a real core row from an authored cylinder-block definition that fits the target's actual spec:

```csharp
var core = new PartCartLine {
    action = CartAction.NewSet,
    definitionId = actualBlock.id,
    quantity = 1,
    install = true
};
var plan = BlockPreparation.Build(
    profile, target.instanceId, core, existingRows,
    BlockPreparationMode.Reuse, null, policy);
```

The supported operation is `BlockPreparation.Build(PlayerProfile,string,PartCartLine,IEnumerable<PartCartLine>,BlockPreparationMode,IDictionary<PartCategory,BlockPartChoice>,ValuationPolicy)`. Record `Owner`, ordered `Lines`, each `Choices` entry, `Current`, `Proposed`, `Healthy`, `Total`, `Ready`, and exact `Error`. Compare source immutability and preview row order. Run Reuse on a valid real engine first. Then isolate a valid recipe shape with a physically available compatible loose part, if one exists, to cover `Owned`; use an actual fitting authored offer for `OEM`; cover `Cart` by adding the real supporting purchase/move row. Do not force a fabricated `Manual` choice to mimic missing stock.

For partial/missing requirements, use the actual target recipe and `PartSemantics.Optional(category,spec,definitions,catalog)` result. The test must establish the required set from the real spec, then create a case by applying an actual supported domain move or a single controlled removal on a disposable literal clone. The baseline remains the valid normalized profile. Record which real component is absent and why; run `Build` and the frozen preview without `Ensure` between removal and observation. Keep both outcomes: exact block-plan error/order and exact whole-preview accept/reject. A malformed or incomplete state must not be healed to make the plan ready. Existing `NativeWorkshopVerification.BlockDomainChecks` is a useful behavioral predecessor for Reuse/OEM/Owned choices and source immutability, but its JSON clone and direct `.72` condition fabrication are not the fixture method for this characterization.

## Second owner, detached engine, and transfer

Use the actual helpers, on isolated normalized profiles:

```csharp
OwnedCar second = CartPreviewCharacterizationFixtures.AddSecondOwnedCar(profile);
EngineAssemblyInstance stored =
    CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(profile, second.instanceId);
```

`AddSecondOwnedCar` requires the normalized selected starter car and a real distinct new offer in seeded `vehicleStock`; it buys through `VehicleCollection.Buy` and returns the created actual car. `DetachOwnedEngineToStorage` requires an existing car with its real attached engine and calls `SharedInventory.Move` to `SharedInventory.Storage`; it verifies the engine's parent is empty and storage ID is set. These helpers mutate their disposable profile by design; never reuse one mutated input for the oracle and candidate.

Before any cart row, capture both cars' IDs, selected ID, engine links, installed bindings, tune fields, fuel/fluids, all engine part/member identities and condition, plus the detached engine's full graph. Probe separately: move a real compatible part into the second car; move an owned part into the detached engine where actual fitment permits; move the detached engine back to either eligible car using the `SharedInventory.Move` operation via a `CartAction.Move` row; and replace/swap an actual complete engine only where a valid real graph is available. Assert that only the intended target owner changes, the source car's remaining tune/fluid state is conserved according to the authoritative detach/attach behavior, and non-target car state remains unchanged. Compare the entire selected-car and target-car inspections separately; selected identity is not a substitute for the row's destination.

Use the production domain flow in the test; do not directly assign `parentVehicleId`, `engineAssemblyId`, `storageId`, or slot bindings. Capture intermediate state after each logical row in diagnostic traces, but compare the overall preview as the all-or-nothing operation under test. `NativeWorkshopVerification` around its donor/receiver setup provides precedent for actual engine detachment, rename, storage, and subsequent swap, but the characterization must not save or touch `ProfileStore`.

## Targeted malformed probes after valid baselines

Only after the relevant valid topology case has passed, make one malformed mutation at a time on independent copies. Candidate probes are limited to real serialized/typed fields encountered in the case: null row or positions collection; empty/duplicate/nonexistent member ID; null condition or member; member runtime subtype replaced with another real subtype; unsupported fault for that concrete `EngineElement` category; invalid health/severity/wear remainder; missing engine owner link; stale/mismatched part owner; missing recipe component; null/missing wheel or tire corner; and duplicate corner/asset. Preserve the exact unmodified input copy and record the changed path/value. Expected behavior is the independent frozen oracle's acceptance or exact first error/exception, not a presumed rejection. Never substitute a valid fault enum from another component category or invent a plausible physical unit.

## Native-only boundary and run handoff

The domain fixture can prove typed result parity and state preservation, but it cannot establish real mouse callback ordering, modal payload, viewport/scroll behavior, or Unity layout timing. Keep the existing Native UI action matrix and its raw-frame evidence as separate gates. Do not label this blueprint as native validation. At the Editor run, record actual discovered car/definition IDs, exact member/corner maps, any unavailable shapes, method result/error traces, and whether every required row ran. An unavailable authored topology stays open; it is not a pass. S06d2b step 1 remains open until the complete matrix, independent legacy trace, review, and required Editor/native gates are completed.
