# L1D validation reader routes — independent scope review

Status: **read-only source review** of the 22 validation/reservation targets in [the validation seam proposal](S06D2B1L1D_VALIDATION_SEAMS_PROPOSAL.md), compared with the initial file allowlist in [the query design](S06D2B1L1D_CART_QUERY_DESIGN.md). Root has separately checked declaration/file hashes against v13a. This note grants no production edit, test-run, Unity, build, Git, or caller-conversion approval.

## Observed catalogue identity routes

`MechanicsOwnership.ValidateForQuote(profile, catalog)` calls `Validate(profile, catalog)` and conditionally retries `ValidateCore(profile, catalog, true)` only when the first error contains “dealer”. `ValidateCore` has a real explicit-catalog path and a separate profile-catalog path:

| Call site in `MechanicsOwnership.ValidateCore` | Actual identity source | Recommended typed treatment |
|---|---|---|
| `VehicleListings.Validate(profile, catalog)` | Explicit `catalog` | Add a reader-backed, no-proof internal route within the already listed `VehicleListings.cs`; preserve legacy public wrapper. |
| Car and engine descendant checks (`Spec`, `Find`, `Fits`, `Recipe`, `Resolve`) | Explicit `catalog` | Route to the explicit-catalog reader capability; keep short-circuit and lookup order. |
| `SavedSetups.Validate(setup, catalog)` | Explicit `catalog` | Add the exact reader overload in `SavedSetups.cs`; this source file is absent from the initial file allowlist and must be added before edits. |
| `SharedInventory.Validate(profile, identities, tolerateDealerDiscriminator)` and `ValidUnit(profile, unit)` | `profile.catalog` | Add reader overloads in `SharedInventory.cs`, preserving the legacy wrappers and shared identity-set/row order. Do not silently feed the explicit-catalog reader. |
| `PartsMarket.Validate(profile)` | `profile.catalog` | Add a reader overload in `PartsMarket.cs`; preserve all market checks/order. Its nested `CartReservations.Validate(profile)` must use the same profile-catalog reader. |
| `GarageBusiness.Validate(profile)` | No catalog argument; current reservation/snapshot path uses JSON | Keep the old method for legacy callers/oracle; a separate typed reservation-view validation route is required for candidate quote validation. See active-job gap below. |

Normal `Valuations.LoosePriceView(profile, policy, buyer, ticks)` passes `profile.catalog` to `ValidateForQuote`; therefore its ordinary runtime case has equal explicit/profile catalogue objects. The public validation signature nevertheless permits distinct objects, and the descendant calls make that distinction observable. Freeze an explicit identity test using two actual authored catalogue objects with a controlled definition difference: pin the legacy outcome, then require either two independent readers that preserve it or an explicit candidate-boundary rejection documented before evaluation. Never assign the explicit catalog into `profile.catalog` to make the paths agree.

The owner/profile catalogue route is also used below the visible call boundary: `PartsMarket.Validate` calls `CartReservations.Validate`; `CartReservations.Validate` performs `profile.catalog.Find` on cart lines and holds. `PartsMarket.Validate` additionally reads market definitions and complete-engine offer descendants through `profile.catalog` (`Find`, `Spec`, `Fits`, `Recipe`). A market-only overload that redirects its top-level checks but leaves these nested reads live is incomplete. The reader must be passed through the complete method body and the nested reservation validation, with the old one-argument methods remaining behavior-compatible.

## Positive listing proof is not a pure query seam

`VehicleListings.Validate(profile, catalog)` first calls `VehicleListingsValidationProof.TryValidate`. A matching positive certificate returns its cached result. A stale certificate is cleared with `CompareExchange`; a successful core run may publish a new certificate through `StoreValid`, which captures a ready content token and copies listing/part facts into a static weak-table bucket. Those are observable cache effects even though they do not mutate the profile fields.

For the candidate query, avoid both cache reads and writes. Add a narrowly named reader-backed query method inside the already listed `VehicleListings.cs` that calls its validation core directly and accepts the reader capability. Keep `VehicleListingsValidationProof.cs` out of the production edit set. Test with: (a) no existing proof, (b) a valid positive proof, and (c) a positive proof made stale by a same-revision source edit. Candidate results must match the recomputed legacy branch while proof-hit/core-run counters and certificate identity remain unchanged; do not let a stale query clear the positive proof as a side effect. The old frozen oracle may retain its actual legacy proof behavior.

## Exact allowlist additions and treatment

These are finite path deltas to the initial query-design list, not authorization to edit them. Each path/method range and body hash must be added to the production/oracle manifest and reviewed before code work.

| Exact additional path | Reached methods / concrete reason | Recommended treatment |
|---|---|---|
| `Game/Assets/DragLab/Scripts/Domain/SavedSetups.cs` | `SavedSetups.Validate(setup, MechanicalCatalog)` is a listed proposal target but is not in the initial file allowlist; it uses `Spec`, repeated `Find`, and `Fits` for base, wheel, and tire definitions. | Add only a reader overload for `Validate`; keep setup planning/application methods and public validation adapter unchanged. |
| `Game/Assets/DragLab/Scripts/Domain/VehicleListingsValidationProof.cs` | Called by legacy `VehicleListings.Validate`; positive cache hit, stale invalidation, and positive publication are outside query purity. | No candidate edit recommended. Route the query through a new no-proof overload in `VehicleListings.cs`; freeze this path for the legacy reference and test that candidate does not touch it. |
| `Game/Assets/DragLab/Scripts/Domain/OwnershipValidationLookup.cs` | `ValidateCore` captures the car/engine/part lookup before traversing identities. It is reached but not among the 22 rows/initial files. | Reuse unchanged only after pinning the exact capture/find methods and proving it builds an operation-local map without source/index writes. Do not broaden this helper into a profile/catalog mirror. |
| `Game/Assets/DragLab/Scripts/Domain/GarageBusiness.cs` | Proposal rows `Validate`, `Snapshot`, `Contents`, and `Matches` reach JSON hash/fallback for active jobs. The whole file is outside the initial allowlist. | Prefer byte-identical legacy reuse. Put the new typed reservation view/comparer and query-only reservation validator in a separate exact new Domain path, then call it only from the typed quote-validation route. Do not edit `Assign`, `Finish`, rewards, receipts, or income without an explicit creation-boundary lease. |
| `Game/Assets/DragLab/Scripts/Domain/SnapshotEquality.cs` | `Serialized<T>`/`Same` is reached only after legacy hash mismatch and nonempty `snapshotJson`. The file is outside the initial list and contains broader profile equality methods. | Freeze/reuse unchanged in the legacy oracle only. Do not route typed candidate comparison through reflection/JSON or change `Profile`/other legacy callers. |
| `Game/Assets/DragLab/Scripts/Infrastructure/ProfileStore.cs` | Current typed load and legacy-import return points are possible producers for a nonserialized typed reserved view. Infrastructure is outside the initial Domain allowlist. | The dormant API may instead require an explicit prebuilt owner/job context. Whichever is selected, name the actual producer/handoff for current load, legacy import and in-session job creation. If ProfileStore is selected, add only a post-validation materialization call after both typed load and import; keep save serialization unchanged. |
| `Game/Assets/DragLab/Scripts/Infrastructure/LegacyProfileDecoder.cs` | `DecodeAndValidate`/`ValidateLoadedV12` currently validate the original graph and can reach old GarageBusiness matching before returning. | Prefer post-validation materialization at `ProfileStore` return if it can preserve the legacy hash-first and tolerance semantics. If the typed view must exist before validation, this path is an additional exact edit and the old reference must be frozen/routed first. |
| New exact Domain path(s) for `ReservedBuildValues` and `LegacyReservedBuildImport` (names in closure proposal) | A mutable `ReservedBuildSnapshot` containing `PartInstance[]` is not an immutable typed view and retains domain references. The import bridge must implement hash-first legacy conversion separately from query comparison. | Add a small concrete immutable value DTO/comparer and one explicit import-only converter after high approves the lifecycle. Keep `GarageBusiness.cs` and `SnapshotEquality.cs` byte-identical if possible; do not use reflection, query-time `JsonUtility`, whole-profile copies, or synthetic defaults. |

The minimum new reader call surface is finite:

| Reader method to add in the named file | Scope it must cover |
|---|---|
| `MechanicsOwnership.ValidateForQuoteTyped(profile, explicitCatalogReader, profileCatalogReader, reservationContext)` | Preserve existing two-pass dealer discriminator and error order. Use `explicitCatalogReader` for direct owned/listing/setup checks, `profileCatalogReader` for nested shared inventory/market/reservation checks, and the already prepared typed reservation context for GarageBusiness. Existing public methods remain unchanged. |
| `VehicleListings.ValidateForQuery(profile, catalogReader)` | Direct core validation only; never call `VehicleListingsValidationProof.TryValidate` or `StoreValid`. |
| `SavedSetups.Validate(setup, catalogReader)` | The setup validation body only. |
| `SharedInventory.Validate(profile, identities, tolerateDealerDiscriminator, catalogReader)` and `ValidUnit(profile, unit, catalogReader)` | All part/unit/dealer definition reads, including descendant and tire specification checks. Existing adapters keep using `profile.catalog`. |
| `PartsMarket.Validate(profile, catalogReader)` and `CartReservations.Validate(profile, catalogReader)` | All market/engine/reservation reads under the profile-catalog route. The nested call must pass the same reader. |
| Reader-bearing valuation view (`LoosePriceView`/display value helpers) in `Valuation.cs` | All reached definition reads for a sale, including tire/wheel overrides and loose-unit definitions; use the profile-catalog reader and the typed validation entry above. Preserve the existing lazy construction/revision behavior. |

Method names above are proposed internal names, not an API approval. `ICartCatalogRead` already exposes `FindDefinition`, `Recipe`, `Spec`, `Resolve`, and `Fits`; add no broader abstraction. Keep old public methods as adapters. Record each exact overload signature/range/hash in the method lease before writing code. `VehicleInspection.Validate`, `MassDynamics.ValidateInputs`, `MechanicsSchema.Finite`, `FuelState.Validate`, `FluidState.Validate`, `EngineElements.FaultAllowed`, `MechanicalCatalog.Slots/EngineSlot`, and `OwnershipValidationLookup.Capture/Find*` can remain unchanged; pin only reached declarations and prove local-map construction has no source/index writes.

For catalog identity, `LoosePriceView` currently supplies `profile.catalog` to the explicit validation parameter, so both reader arguments can share one owned scope after reference identity is checked. If a typed API exposes a distinct explicit catalogue, it must preserve two separate reader capabilities or reject that request before row processing; it must not retarget `profile.catalog`. This decision belongs at the typed input boundary, not in the readers.

## Active business snapshot dependency

The path is `Valuations.LoosePriceView → MechanicsOwnership.ValidateForQuote → ValidateCore → GarageBusiness.Validate → Matches → Snapshot/SnapshotEquality.Serialized`. The active-job JSON issue is a separate concrete path expansion, not solved by threading catalogue readers. The legacy hash is checked first; if it matches, a malformed or stale `snapshotJson` is not read. If the hash mismatches, nonempty JSON enables structural fallback, whose doubles allow up to four ULPs except field `health`; primitive and float fields are exact. `Ensure`'s `UpgradeLegacySnapshots` writes only when the hash-only current contents match. These behaviors and the named import/creation choices are detailed in [the reservation test design](S06D2B1L1D_RESERVATION_TEST_DESIGN.md).

The frozen query valuation reference currently reaches live `GarageBusiness.Validate`, so changing it before independently freezing/routing the business match closure invalidates oracle independence. Capture the complete active-job legacy closure first, including successful hash branch, serialized exact/fallback branch, tolerance boundaries, malformed payload, and error precedence. The candidate must consume an already-owned typed reserved view and show zero query-time JSON; it must not inherit the old oracle's source/JSON purity failures.

## Finite method-level implementation boundary

Proposed reader-bearing call chain for the validation pass:

```text
MechanicsOwnership.ValidateForQuoteTyped(explicitReader, profileReader)
  -> ValidateTyped -> ValidateCoreTyped
     -> VehicleListings.ValidateForQuery(explicitReader)       // bypass proof cache
     -> GarageBusiness.ValidateForQuery(typedReservedView)     // no JSON
     -> SharedInventory.Validate(profileReader, sharedIds, pass)
          -> ValidUnit(profileReader, unit)
     -> PartsMarket.Validate(profileReader)
          -> CartReservations.Validate(profileReader)
     -> SavedSetups.Validate(setup, explicitReader)
```

The two reader arguments are the exact explicit-catalog and `profile.catalog` contexts. On the normal `LoosePriceView` route, both originate from the same object and may share one owned scope after reference equality is proven. On a distinct-object route, preserve separate identities or reject it at the new typed API boundary before evaluating rows; never retarget the borrowed profile. The query API also needs an explicit owner/job-bound typed reservation context before evaluation. `LegacyReservedBuildImport.FromLegacy` may run only at its named import boundary; if it is not wired by the profile-load/legacy-import route or an equally explicit caller-owned preparation route, the candidate still has no valid way to avoid query-time JSON. Treat that as an unresolved integration dependency, not a hidden fallback.

Unchanged leaves such as `VehicleInspection.Validate`, `MassDynamics.ValidateInputs`, `MechanicsSchema.Finite`, `FuelState.Validate`, `FluidState.Validate`, `EngineElements.FaultAllowed`, `MechanicalCatalog.Slots/EngineSlot`, and the existing local `OwnershipValidationLookup` are not a reason to expand into entire files. Pin their exact declarations as reached dependencies, then reuse them unchanged if they have no catalog/index/cache writes. Any helper that performs a public catalog lookup, publishes/invalidate a cache, touches JSON, normalizes a source, or reads an unapproved global must instead be isolated behind a specific reviewed overload.

Before any implementation, high/root should approve the extra exact paths above, method-level signatures and hashes, oracle routing, and reservation lifecycle. If source review discovers another method/file, report its exact signature/range/hash and stop for a finite allowlist decision. This review does not change the full L1D mode matrix, source-purity, no-runtime-JSON, physical, caller, native/performance, or S06–S14 gates.
