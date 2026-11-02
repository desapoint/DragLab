# S06D2B1L1C — implementable API and ownership design

Status: **C1 design for independent review; no implementation GO.** This document refines the proposed surface in [the L1C contract](S06D2B1L1C_CONTRACT.md) into one implementation checkpoint. The matching candidate matrix is in [C1 test design](S06D2B1L1C1_TEST_DESIGN.md). No source, Unity, build, or Git work is authorized by this design note.

## Publicly internal candidate surface

Keep one dormant API, with no caller conversion and no second planner entry point:

```csharp
internal static OrderedCartApplyResult Evaluate(
    PlayerProfile owner, string carId,
    IReadOnlyList<PartCartLine> rows,
    ValuationPolicy policy = null,
    bool preview = false);
```

`OrderedCartApplyResult` is immutable after construction. Its concrete proposed fields are `bool Succeeded`, `bool HasCashDelta`, `int? CashBefore`, `int? CashAfter`, `int? CashDelta`, `string ErrorType`, `string ErrorMessage`, `int ErrorIndex`, and success-only arrays `CartApplyRowEffect[] Effects`, `CartApplyPartSnapshot[] Parts`, `CartApplyEngineSnapshot[] Engines`, `CartApplyUnitSnapshot[] Units`, `CartApplyDealerSnapshot[] Dealer`, `CartApplyCarSnapshot[] Cars`, `CartApplyOwnerSnapshot[] Owners`, `CartApplySupplyDelta[] SupplyDeltas`, and `CartApplyIdentity[] Identities`. Keep the backing arrays private and expose defensive array copies. `HasCashDelta == false` means no raw branch read cash; the three nullable cash fields are null, never zero-filled. Each effect records `int RowIndex`, `int QuantityOrdinal` (`-1` for non-quantity Move/skip), `CartAction RequestedAction`, `CartApplyBranch Branch`, `string DefinitionId`, `string AssetId`, nullable `CartApplyIdentityRef Identity`, `string InstallTargetId`, `int? PaidAmount`, nullable immutable `CartApplyQuote` (`cost`, `fee`, `localUnits`, `expressUnits`), and owned `CartApplySupplyDelta[]` for that iteration where the branch consumed supply. Branch enum values are `SkippedSale`, `Move`, `NewSet`, `NewUnits`, and `BuyUsed`; undefined requested actions retain their raw enum value while `Branch` is `BuyUsed`. `CartApplySupplyDelta` contains integer identity-group, before/after and cloned position array plus source/hold kind; it must not retain `ScopedQuantityDelta` or market rows. Snapshots contain only owned primitive/string/enum/nested DTO values. Skipped sales and Move effects have no cash read; zero-iteration acquisition rows have no quantity effect. Each sale row emits only metadata (`SkippedSale`, ordinal `-1`, no identity/quote/cash or mutation snapshot); it does not claim a physical/economic effect.

Failure contains only `Succeeded == false`, the exact exception full type name, exact message, and row index (`-1` for the documented outer `rows == null` boundary). All success-only arrays, effects, snapshots, token maps, and cash deltas are absent. Construction uses values captured at the branch that failed; it must not re-read the source graph. Dispose the operation scope on success and failure before returning. No result field or nested collection may retain a source object, profile/catalogue, definition, list, or scope.

Snapshot DTOs are field-complete projections of changed values, with a separate `bool Deleted` tombstone and `CartApplyIdentityRef Identity` on every record. Emit records in first-write order; list additions also carry `int InsertionOrdinal`, and removals carry the captured `int SourceOrdinal`. `CartApplyPartSnapshot` contains version, identity, definition/storage IDs, location, owner ID, slot, and `CartApplyConditionSnapshot`. The condition snapshot contains every serialized `ComponentCondition` field: health, failed, failure reason, running seconds, distance, stress exposure, temperature, slip energy, deposits, leak severity, adjustment error, service wear remainder, service/recondition counts, last service, nullable clutch snapshot (`version`, oil litres, oil condition), and ordered element snapshots. Element snapshots carry concrete runtime type name plus every serialized scalar field of that concrete `EngineElement`/`TireCondition` subtype in deterministic declaration-name order; they contain no element reference. `CartApplyUnitSnapshot` contains definition/storage IDs, unit price, `tireUnit`, and one element snapshot. `CartApplyDealerSnapshot` contains identity, `Deleted`, discriminator, paid price, and exactly one nested part or unit snapshot where present. `CartApplyEngineSnapshot` contains identity, parent/vehicle-definition/storage/custom-name IDs, ordered slot bindings, fluids, and tune values. `CartApplyCarSnapshot` contains every owned serialized field of an affected `OwnedCar`: instance/chassis/engine/job IDs; installed build version/identity/vehicle definition/ordered bindings; fuel and its initialized flag; fluids; service hardware version; thermal ticks; owned `EngineRebuildSnapshot` (`active`, ID, engine ID, start/ready ticks, paid price, ordered `EngineRebuildTargetSnapshot[]` entries with part ID, definition ID and replace flag); mass version; vehicle and custom name; used-history IDs; tune; paint hex; ordered presets; completed runs, mileage, all best-pass scalars; and owned last-pass values when present. `CartApplyOwnerSnapshot` records affected owner identity/kind and ordered source part IDs, with no owner object. Deleted records carry identity, deletion flag and source ordinal only, with no stale body. `CartApplySupplyDelta` carries identity group, copied positions, before/after units and source/hold kind; `CartApplyQuote` carries cost, fee, local units and express units. Arrays are private-backed and cloned on getter; nested values are DTOs. These describe changed/deleted records and effect-relevant owning structures only, not a whole-profile snapshot.

## One ordered working state

Create one `CartApplyWorkingSet` per `Evaluate` call and use it for every row and iteration. It is a private overlay, not a profile copy. It owns:

* a lazily acquired owner reference, optional cash scalar, and reference-keyed COW wrappers for cars, engines, parts and loose units;
* source-order list views with identity tombstones and ordered additions for `parts`, `looseEngineElements`, and `dealer`; lookups merge source rows and prior overlay writes without copying a complete list;
* one `ScopedMarketReadSet` stock/hold overlay and the single nonpublishing catalog lookup capability described below;
* an operation-local virtual identity allocator and token-to-created-object map;
* the existing K/I/J state needed by whole-engine, part and unit Move/install transitions.

First-match reads preserve legacy source order. Each write captures only the record or member reached by that branch, keyed by object identity; a removed source row is a tombstone and a newly created row is appended at the exact legacy insertion point. New candidate rows become visible to later rows through the same view. Existing borrowed rows stay unchanged. Reads and changes to `usingCartReservation` retain its input value; the operation never infers it with `SameCart` or `Matches`.

The scope may retain a nullable owner before work starts, but opening it performs no owner field reads. It must not open a catalog or market lookup, read cash, or inspect a car until the ordered branch reaches that operation. Thus null-owner empty input, sales-only input, and nonpositive-quantity NewSet/NewUnits/undefined-action rows can complete without owner access. Move still runs once for any quantity and follows its ordinary guard/error order. Dispose clears owner, lookup/market children, source references, COW wrappers, overlays, caches and tokens.

`ProfileInstalledPartTransitionState` is currently a source-reference COW state with `copyOnRead` and read-only `TransitionPartList`; candidate work must extend or factor that state rather than inventing a second Move rule set. The candidate state must expose its merged part/unit reads and append/remove effects to K/I/J. Its implementation must keep existing live `copyOnRead: false` behavior while the candidate scope uses owned COW writes. Do not put `MechanicalCatalog` on that state: current transition methods receive catalog as a separate argument.

## Narrow operation capabilities and catalogue boundary

Shared transition rules receive narrow capabilities, never a mutable `PlayerProfile` or `MechanicalCatalog`. The exact proposed signatures are:

```csharp
internal interface ICartCatalogRead : IDisposable
{
    string[] Models();
    CartApplyCatalogProduct FindProduct(string id);
    PartDefinition FindDefinition(string id);
    Dictionary<PartCategory, string> Recipe(string model);
    VehicleSpec Spec(string vehicleId);
    BuildResolution Resolve(string vehicleId, IDictionary<PartCategory, string> installed, Tune adjustments = null);
    bool Fits(VehicleSpec spec, PartDefinition definition);
}

internal sealed class CartApplyCatalogProduct
{
    internal readonly PartDefinition Definition;
    internal readonly bool Found;
    internal readonly int Position, IdentityGroup;
}

internal interface ICartApplyState : IDisposable
{
    bool HasUnsettledRun { get; }
    int Reputation { get; }
    bool HasCashRead { get; }
    int Cash { get; set; } // live setter writes owner.cash here; candidate setter updates only its private scalar
    PartMoveCarState FindCarState(string id);
    PartInstance FindPart(CartApplyIdentityRef identity);
    PartInstance FindPartReadOnly(string externalId); // always Existing, never resolves virtual-symbol text
    PartInstance EditPart(PartInstance source); // capture source.Copy() before returning a mutable value
    LooseEngineElement FindUnitReadOnly(string externalId); // always Existing, never resolves virtual-symbol text
    LooseEngineElement EditUnit(LooseEngineElement source); // capture wrapper + exact typed element Copy() first
    CartApplyDealerRead FindDealerStockReadOnly(string id);
    CartApplyIdentityRef IdentityFromOwnedField(string id); // only for a candidate-owned relation/binding field
    string OwnedFieldValue(CartApplyIdentityRef identity); // source ID or private candidate symbol for internal serialization only
    bool IsCandidateCreated(PartInstance part); // true only for allocator-created root objects, not COW copies
    bool IsCandidateCreated(LooseEngineElement unit);
    void RemovePart(PartInstance source);
    void AppendPart(PartInstance value);
    void RemoveUnit(LooseEngineElement source);
    void AppendUnit(LooseEngineElement value);
    void RemoveDealerStock(CartApplyDealerRead source);
    void AppendDealerStock(DealerStock value);
    PartInstance TransferDealerPart(CartApplyDealerRead source); // detached payload copy preserving all fields/condition
    LooseEngineElement TransferDealerUnit(CartApplyDealerRead source); // detached wrapper + subtype-preserving element copy
    ICartApplySupply Supply { get; }
    ICartCatalogRead Catalog { get; }
    ICartIdentityAllocator Identities { get; }
}

internal interface ICartApplySupply
{
    CartApplyQuote Quote(PartDefinition definition, int count, bool unit, int quantity = 1);
    CartApplyConsumeReceipt Consume(PartDefinition definition, int count, bool unit);
}

internal sealed class CartApplyQuote
{
    internal readonly int Cost, Fee, LocalUnits, ExpressUnits;
}

internal sealed class CartApplyConsumeReceipt
{
    internal readonly CartApplyQuote Quote;
    internal readonly CartApplySupplyDelta[] StockDeltas, HoldDeltas;
}

internal interface ICartIdentityAllocator
{
    CartApplyAllocatedIdentity Allocate(string kind, int rowIndex, int quantityOrdinal, string memberPath);
}

internal sealed class CartApplyAllocatedIdentity
{
    internal readonly CartApplyIdentityRef Identity;
    internal readonly string DomainFieldValue;
}

internal readonly struct CartApplyIdentityRef
{
    internal readonly bool IsVirtual;
    internal readonly string ExistingId;
    internal readonly int TokenOrdinal;
}
```

`CartApplyDealerRead` is an immutable value handle containing `CartApplyIdentityRef Identity`, `bool IsPart`, `bool IsUnit`, `string DefinitionId`, `int PaidPrice`, `int SourceOrdinal`, and `bool IsOverlayAddition`. It contains no `DealerStock` or payload reference; the working set privately resolves the handle to its row for the explicit transfer-copy operation. It exposes no mutator. `FindPartReadOnly`/`FindUnitReadOnly` and dealer lookup may return borrowed data only to read scalar fields. Any code that can mutate a source record must first request `EditPart`/`EditUnit` (which returns a scope-owned `Copy()` wrapper); dealer payloads are transferred only by the explicit copy methods. Appends accept only newly created values or scope-owned copies. Never pass the read-only handles to a helper that mutates payloads. For car/engine/unit Move wrappers, retain the existing `PartMove*State` COW boundary: a read wrapper becomes writable only through its existing state mutation path.

These interfaces are internal, and the interfaces themselves must not expose profile/list/catalog objects. `ICartApplyState` is implemented by `CartApplyWorkingSet : InstalledPartTransitionState, ICartApplyState`: it implements the existing abstract K state methods over the same COW wrappers, and owns the per-operation part/unit/dealer overlays. Factor the common state-wrapper construction from `ProfileInstalledPartTransitionState`; the live subclass retains `copyOnRead: false`, while the candidate working set enables COW and adds source-ordered overlay lookup. `FindCarState` returns the existing `PartMoveCarState` wrapper (not `OwnedCar`); part/unit/dealer accessors return state-owned read/write handles whose source values are never mutated before COW capture. The shown methods are the minimum shared surface; any extra method must be a scalar/value operation or transition-state wrapper, never a mutable source collection.

Definition reads are borrowed read-only values; transition code may not mutate a definition. `Models()` lazily returns a fresh `string[]` using the exact legacy algorithm `catalog.parts.Where(d => d.category == PartCategory.CylinderBlock).Select(d => d.id.Split('/')[0]).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray()` at the point the caller requests Models; it does not clone definitions or the catalog. Preserve the legacy source enumeration, null/error behavior, ordinal sort, and call timing. This is required by `PartsMarket.FitsAny`, `Reference`, and cross-model `Count` fallback. `Recipe(model)` delegates to the unchanged `MechanicalCatalog.Recipe(model)` at that same call point and returns its newly created `Dictionary<PartCategory,string>`; retain its exact `Slots(specs[model])`, category omissions, and optional legacy-tune branches, with no guessed factory recipe. `PartsMarket.Reference` must call `Models`, then `Spec`/`Fits` in legacy order, then `Recipe`, then `Resolve`. `FindProduct` returns `CartApplyCatalogProduct`, copying the `Found`, `Position`, and `IdentityGroup` metadata returned by `LookupScope.FindDefinition`; `FindDefinition` is the convenience projection of that same result. `Spec` returns `VehicleSpec.Copy()`; `Resolve` returns the existing `BuildResolution` with its existing typed spec/tune copies. Existing typed `VehicleSpec.Copy`, `Tune.Copy`, and `PartDefinition.Copy` semantics remain in force; no JSON path is introduced. The lazy `Cash` getter dereferences `owner.cash` exactly when the raw branch reads it, so null-owner access raises the same exception at that point. `LiveCartApplyState.Cash.set` writes `owner.cash` at the exact legacy statement point; `CartApplyWorkingSet.Cash.set` updates only its private scalar. `HasCashRead` is separate result metadata and does not alter either setter or arithmetic. Preserve the exact legacy compound assignment/checked expression at each debit rather than routing all operations through one potentially different arithmetic helper. A method that needs a car does so by an ordered state lookup at its existing rule location.

`ICartApplySupply` is the only supply dependency used by shared Buy rules. `LiveCartApplySupply` wraps the existing public `PartsMarket.Quote` and `PartsMarket.Consume` calls, preserving their cache and publication behavior. `ScopedCartApplySupply` wraps the operation's shared `ScopedMarketReadSet` and its L1B overlays. The shared transition uses only these two methods and copies the returned scalar quote/consume receipt into owned effect data. The live receipt has quote values and no private overlay deltas; the candidate receipt has the `ConsumeResult` quote plus copied stock/hold deltas. Neither shared rule body reads `ScopedMarketReadSet` directly.

`ICartIdentityAllocator` is injected into shared creation rules. Its live and candidate implementations, private symbol bridge, typed external-ID ingress, and collision behavior are specified in [Virtual identities and no-heal boundary](#virtual-identities-and-no-heal-boundary); no future rows are read ahead.

Result effects are route metadata rather than a claim that every row mutated state. Emit one metadata-only `CartApplyRowEffect` with `QuantityOrdinal == -1`, `Branch == SkippedSale` for each skipped SellPart/SellUnit row, and one Move effect at ordinal `-1`; skip effects have no token, quote, cash or physical/economic mutation. Move has no token/quote/cash, but may have changed-record snapshots. Acquisition rows with zero iterations have no effect. This matches the C1 matrix's distinction between row traversal and physical/economic effects.

`CartApplyWorkingSet.OpenBorrowed(PlayerProfile owner, string carId, ValuationPolicy policy)` stores those inputs without field access. On the first lookup that requires authored content, it captures `owner.catalog` at the same legacy point, calls `MechanicalCatalog.OpenLookupScope()` once, constructs a private `LookupCatalogRead`, then constructs `ScopedMarketReadSet.OpenBorrowed(owner, catalogRead)` with that exact reader. Change `ScopedMarketReadSet`'s current `MechanicalCatalog.LookupScope catalog` field to a non-owning `ICartCatalogRead catalogRead` field and its constructor/factory to accept the shared reader; `ResolveStock` calls `catalogRead.FindProduct(id)` to retain the exact definition/found/position/identity-group tuple. `ScopedMarketReadSet.Dispose` clears the reader reference but does not dispose it; the working set disposes the one reader/scope exactly once. `LookupCatalogRead.FindDefinition` delegates only to `FindProduct(...).Definition`; its `Spec` and `Fits` methods call the corresponding typed/detached catalog methods; `Resolve` uses the reader-backed core below. The catalog and reader stay private to the working set. Thread that reader explicitly through every current separate-catalog parameter in `InstalledPartTransition`, `InstalledInventoryTransition`, `InstalledEngineTransition`, and nested installation/replacement helpers. The K state has no catalog property. No scoped candidate call may fall back to public `MechanicalCatalog.Find`, publish indexes/revisions, or construct `DetachedNewSupplySnapshot` (which copies the whole catalog).

`MechanicalCatalog.Resolve` needs one internal `ResolveCore(string, IDictionary<PartCategory,string>, Tune, Func<string,PartDefinition>)`. The current public `Resolve` calls it with `Find`; `LookupCatalogRead.Resolve` calls it with `LookupScope.FindDefinition(id, out _, out _, out _)`. The delegate is passed to each nested helper on the actual Resolve path that can call `Find` (including `PartSemantics` removal/optional/apply and combustion validation), with overloads retaining old public signatures and routing them through public `Find`. Keep the entire old branch/order body in the core and freeze/hash the pre-change `Resolve` before extraction. `Spec` remains the current typed-copy call; no speculative `SpecCore` is required unless code inspection shows it calls `Find`. `Fits` remains detached fit logic. This is lookup substitution only, not a broader physics or catalog redesign.

## Shared rule path and raw row dispatch

`Evaluate` must execute the existing ordered loop directly against the one working state. It must not prevalidate/copy rows or use `PartCartLine.Copy` before the legacy read point:

```text
if rows is null: return the typed outer-boundary failure (index -1)
open the lazy operation scope
for rowIndex in source order:
    read the row's action at its legacy use point
    if SellPart or SellUnit: record skip; continue
    if Move: call shared K/I/J Move once; record effect; continue
    for ordinal from 0 while ordinal < row.quantity:
        if NewSet: shared BuyPart(..., install && ordinal == 0)
        else if NewUnits:
            unit = shared BuyUnit(...)
            if install && ordinal < row.positions.Count:
                shared unit-install dispatcher(unit, row.positions[ordinal])
        else: shared BuyUsed(...), including undefined enum values
on first exception: return failure with captured type/message/row; discard overlay
otherwise export owned snapshots and effects
```

The raw line behavior remains literal: Move is once even at zero or negative quantity; acquisitions iterate only while `ordinal < quantity`; `NewSet` installs only ordinal zero; `NewUnits` installs selected positions after each individual buy; used parts install for each reached iteration; and sale rows skip without inspecting payloads. Null row and null positions fail only at their actual ordered dereference. No eager cash access, aggregate quantity quote, inferred reservation state, sale realization, `Total`, fee addition, or caller activation is introduced.

The live adapters and candidate call the same rules:

* `PartOperations.Buy` and candidate NewSet/BuyUsed-part installation use one shared `BuyPart` transition for the existing validation order, quote/consume, authored initialization, insertion/debit and optional `MechanicsOwnership.Replace`.
* `InventoryUnits.Buy` and candidate NewUnits use one shared `BuyUnit` transition for `Spec`/`Template`/`Count`/`Roles`, `InventoryPricing.NewUnit`, quote/consume, category factory, debit and loose insertion.
* `InventoryUnits.Install`, NewUnits/BuyUsed-unit install, and unit Move use one dispatcher preserving the exact wheel-first, fitted-pack, tire-payload and engine-element branches. Preserve subtype, ID/history, displaced-member ordering, removal, aggregate/service updates and validation order.
* `SharedInventory.Move`, NewSet/BuyUsed part installation and explicit Move use the existing K/I/J dispatcher with the same state. Do not invoke a public adapter from candidate evaluation if that would open another scope or mutate a profile.

The shared unit-install signature explicitly takes `PartMoveMemberPolicy memberPolicy` and the state provenance query `IsCandidateCreated(root)`. `LiveCartApplyState` passes `LegacyEnsure`; `CartApplyWorkingSet` passes `ProjectionRequireExisting`. Thread the same parameter through `WheelFitments.Install`, `TireDynamics.Ensure/SyncSummary`, `EngineElementService.Replace/ReplacePack`, and each nested install helper that can normalize a member or allocate an ID. Under `ProjectionRequireExisting`, inspect a source-owned displaced pack/member at the exact legacy branch point before any normalization or identity allocation: if its existing members/IDs are incomplete and the legacy helper would call Ensure or NewId to repair it, throw the typed unsupported-malformed-source exception before mutating the copy. `IsCandidateCreated` is based on the allocator's root-object reference map, not `copyOnRead` or the fact that a COW wrapper is operation-owned. Thus a newly acquired NewSet tire root with its authored blank member shape can initialize four fresh tire members when NewUnits later installs into it; an existing source tire pack copied into COW cannot. Newly purchased candidate-owned part/unit roots may use normal creation helpers and virtual identities for their own required members. Under `LegacyEnsure`, retain current live helper behavior and existing allocator call order. K's Move policy continues through the same explicit parameter.

For used stock, preserve exact sequence: first dealer match by ID; discriminator/payload checks; price and cash debit; dealer removal; payload transfer; optional shared install. Do not reconstruct or normalize a used payload. `PartCart.PreviewProfile` remains only a provenance boundary and is not called. `InventoryPricing.NewSet`, `LookupScope` outside the scoped reader, and `SameCart` are not raw Apply dispatch routes; pin and label them as non-live where the implementation inventory requires it.

## Virtual identities and no-heal boundary

An operation-local allocator uses typed external identity `CartApplyIdentityRef { bool IsVirtual; string ExistingId; int TokenOrdinal; }`: every input string is tagged `Existing` at ingress and is never reinterpreted as a virtual token. A live allocation calls `MechanicsOwnership.NewId` at the legacy creation point and returns the actual ID with `Existing`; candidate allocation derives a token from `(rowIndex, quantityOrdinal, semanticMemberPath)` and maps it to the newly created object by reference. Existing cart lines cannot name a virtual token. Export `CartApplyIdentity` as `{ int TokenOrdinal; int RowIndex; int QuantityOrdinal; string MemberPath; string Kind; }`; snapshots carry typed refs and preserve source IDs separately.

Current K/I/J objects use string ID fields, so candidate-owned records may temporarily hold deterministic private symbol strings as a compatibility representation. At the first actual allocation only (never at scope construction/no-op), choose a symbol absent from a null-safe scan of the owner's identity-valued fields; do not clone the profile or inspect future input rows. The symbol is private implementation data, never a gameplay identity: it is written only into candidate-owned records and never copied into source state, saved, exposed in effects/results, or passed to an external-ID lookup. Every raw cart ID is tagged `Existing` on ingress. Internal binding/reference fields read from candidate-owned COW/new records go through `IdentityFromOwnedField`; internal serialization uses `OwnedFieldValue`. K/I/J state lookups take `CartApplyIdentityRef`, and transitions that receive purchased objects carry the object/typed identity handle directly rather than looking them up by a fabricated ID. Thus an input asset/destination string equal to a private symbol still performs an `Existing` lookup and cannot resolve to the candidate object; no future-row scan or re-key is needed. Test this collision explicitly. Internal symbols are never returned or treated as persistent IDs. The candidate never calls `MechanicsOwnership.NewId`, RNG, clock, entropy or persistence APIs. Existing IDs, including BuyUsed member IDs and raw blank tire IDs, remain literal.

`IReadOnlyList<PartCartLine>` is consumed once through its `IEnumerable` enumerator in source order, with no pre-copy, index/count pass, or second enumeration. The outer null list gets the typed index `-1` result. A null row is dereferenced where the raw body first reads its action and is reported at that row. Track `ErrorIndex` as the traversal index: it starts at 0, advances immediately after a yielded row is fully handled, and is the next index whose `MoveNext`/enumerator access throws (so an enumerator failure after row 0 and before row 1 is index 1; an enumerator-creation/first-advance failure is index 0). Only `rows == null` uses -1. Null `positions` is dereferenced only after the corresponding NewUnits buy succeeds and the raw conditional reaches `positions.Count`; earlier private work is discarded in the failure result. Empty lists and all skipped/no-iteration rows do not open a catalog/market scope or read owner fields. The scope itself may be a lightweight deferred holder; its acquisition must occur only at the first actual legacy owner/catalog access. Failure results never contain token arrays, effects, snapshots, supply arrays, or cash fields, even if virtual tokens were allocated before failure.

The shared loop method is `OrderedCartApplyTransition.ApplyRows(ICartApplyState state, string carId, IEnumerable<PartCartLine> source, bool preview, ValuationPolicy policy)`. It consumes the legacy `IEnumerable` once through its enumerator, with no `ToArray`, `Count`, `Select`, `Copy`, or second enumeration. `PartCart.Apply` keeps its exact existing `IEnumerable<PartCartLine>` signature and calls the live adapter; `Evaluate` performs only its explicit null-list boundary, then passes the same `IReadOnlyList<PartCartLine>` as `IEnumerable` into the candidate adapter. The `preview` value remains unused. This keeps lazy enumeration, row access and exception ordering identical for the live route while giving the candidate a typed boundary.

Use existing creation helpers to initialize only members of a newly acquired product. Never call `MechanicsOwnership.Ensure` or equivalent normalization on existing source records. If an existing malformed source would require creation/repair of a member to continue, fail at that ordered point with the bounded typed unsupported-malformed-source result (or an earlier existing validation error), dispose the whole candidate scope, and expose no partial outputs. Do not heal missing OEM members, wear, conditions, faults, or identities. Preserve actual operation deterioration such as failed-part service effects inside COW state.

## Single-checkpoint source ownership map

The later implementation is one full C1 production checkpoint, followed by the entire required differential/regression gate from the contract and test design. These are exact implementation seams, not a sequence of partial checkpoints:

| Source | C1 ownership/seam |
|---|---|
| `Game/Assets/DragLab/Scripts/Domain/OrderedCartApplyProjection.cs` (+ `.meta`) | New `Evaluate`, immutable results/effects/snapshots, operation scope, overlay views and token allocator. Keep ownership local; no profile clone. |
| `Domain/PartCart.cs` | `Apply` becomes a thin live adapter over the same shared ordered rules with the real allocator. Preserve the frozen oracle's independence. |
| `Domain/PartOperations.cs` | Factor `BuyPart` rule body; live `Buy` and candidate acquisition pass distinct state/identity capabilities. |
| `Domain/InventoryUnits.cs` | Factor `BuyUnit` and exact install dispatcher; thread state/catalog/identity capabilities through `Spec`, `Template`, `Count`, `Roles`, `Positions` and install branches. |
| `Domain/PartsMarket.cs` | Route `FitsAny`, `Reference`, and `Count` through `ICartCatalogRead.Models/Spec/Fits/Recipe/Resolve` in the shared acquisition branch; keep public live adapters on the existing catalog and preserve the exact lazy `Models()` enumeration. |
| `Domain/SharedInventory.cs`, `InstalledPartTransition.cs`, `InstalledInventoryTransition.cs`, `InstalledEngineTransition.cs` | Adapt live Move and K/I/J transition signatures to explicit state plus `ICartCatalogRead`; implement candidate state as the same transition authority, preserving live state semantics. |
| `Domain/MechanicsOwnership.cs`, `EngineElementService.cs`, `WheelFitments.cs`, `TireDynamics.cs`, `TireFitments.cs`, `EngineElements.cs` | Thread narrow reader/state/identity parameters only through reached mutation and nested lookup helpers; preserve order and authored factories. |
| `Domain/MechanicalCatalog.cs` | Add reader-backed `Resolve` core and private LookupScope adapter; keep public `Find`/`Resolve` behavior intact and avoid index publication in scoped route. Pin unchanged `Recipe(string, Tune = null)` in the C1 method manifest and delegate the reader's `Recipe(model)` to it without recipe reconstruction. Audit all nested `Find` calls. |
| `Domain/ScopedMarketReadSet.cs` | Share the operation's one lookup scope with market definition resolution; retain L1B quote/consume/stock/hold overlay semantics and dispose behavior. |

Exact call routing in that checkpoint is: `PartCart.Apply` → `OrderedCartApplyTransition.ApplyLive` (live state, public PartsMarket adapter, real allocator); `Evaluate` → `OrderedCartApplyTransition.ApplyCandidate` (one `CartApplyWorkingSet`, scoped supply adapter, virtual allocator). Both call the same lazy `ApplyRows` over `IEnumerable<PartCartLine>` and the same `BuyPart`/`BuyUnit`/`BuyUsed`/`InstallUnit` rules. Add an `InstalledPartTransition.Preamble` overload whose asset parameter is `CartApplyIdentityRef`; the public/live string adapter tags its input `Existing`, while any candidate internal binding conversion uses `IdentityFromOwnedField`. `PartMovePreamble` stores this typed identity. Thread it through `InstalledInventoryTransition.MoveAfterPreamble(state, catalogRead, ...)`, with K/I/J internal lookups receiving typed refs rather than interpreting raw strings as virtual. No adapter re-enters `PartCart.Apply`. `MechanicsOwnership.Replace` routes to shared `BuyPart` replacement logic only where the legacy Buy branch invokes it; the live external method remains a wrapper. Public catalog callers retain their existing methods, while only the cart scope uses `LookupCatalogRead` and `ResolveCore`. `LiveCartApplySupply` invokes public `PartsMarket.Quote`/`Consume`; `ScopedCartApplySupply` invokes `ScopedMarketReadSet.Quote`/`Consume` bound to the same single lookup reader. The candidate copies its scalar fields immediately into the effect/snapshot DTOs.

## Frozen catalog-call boundary before production edits

The C0 oracle currently invokes unchanged production catalog helpers through the frozen acquisition/mutation reference. Redirecting `MechanicalCatalog.Resolve`, `Find`, or a nested helper into the new shared reader before pinning an independent old route would silently make the oracle call the candidate path. Therefore, at the start of the single C1 implementation checkpoint and **before editing any Domain method**, the test author/root must add independent frozen methods to the test-only `LegacyCartApplyReference` closure for the exact pre-C1 `MechanicalCatalog.Find`, `Spec`, `Recipe`, `Resolve`, and every nested Find-calling helper reached by Resolve (including current `PartSemantics`/combustion lookup calls). Copy the old implementation and lookup order literally into a private legacy catalog adapter that reads the disposable fixture catalogue without calling the production methods slated for redirection. It may borrow authored immutable definitions; it may not publish candidate indexes or call candidate `ICartCatalogRead`. Preserve the current typed `Spec` copy and `Recipe` category/legacy-tune behavior, and include their source method ranges/body hashes in the frozen provenance.

Before the first production edit, create the complete C1 pre-edit source/method manifest for every source file in the ownership table and every reached dependency, including normalized full-file hashes, exact changed-method ranges/body hashes, C0 commit/path evidence, and the independent frozen copies. Record exact pre-change source ranges and hashes for `Find`, `Spec`, `Recipe`, `Resolve`, and nested Resolve helpers, then obtain independent high semantic/hash review that confirms the copies match the C0-pinned methods and preserve the observed C0 vectors. This is a review gate within C1 preparation, not a separately committed characterization slice; no production method changes until this review is complete. Keep `Recipe` unchanged and route it through the reader only as a typed delegation; keep the frozen catalog adapter private to the oracle/test path and unchanged for the remainder of C1. Then add production `ResolveCore` and the narrow LookupScope-backed reader, route only the candidate through it, and verify the oracle remains on the frozen implementation. Root/test author owns Unity and all runtime/editor evidence. If the independent freeze review cannot be completed, stop before any production source edit; do not claim a candidate differential.

All other pinned C0/K/I/J dependencies remain frozen unless high review finds a specific live transition signature that must be threaded; any such change belongs inside this same C1 checkpoint and its source manifest. The candidate must remain independent of the frozen oracle. No C1 result is complete until every matrix row is run against the frozen raw Apply reference, all existing regressions pass, source immutability and forbidden-dependency checks pass, and high review approves the implementation evidence.
