# S06D2B1L1B — borrowed scoped stock and reservation working set

Status: **design proposal revised by independent review; test reconciliation and root GO still required.** This is a synchronous, dormant shadow of the existing stock/reservation operations. It does not change production callers or provide a transaction, worker payload, saved snapshot, profile/catalogue mirror, or publication mechanism.

## Bounded scope

Implement one owner-thread `ScopedMarketReadSet` that borrows one live `PlayerProfile` only for its disposable synchronous lifetime. It models the direct `PartsMarket.Stock`, `Available`, `Quote`, `Consume`, public `CartReservations.Active/Held`, and the normal non-ready `PartsMarket.Ledger` constructor plus its `Find`, `Held`, `Available`, `Quote`, and `Take` behavior. Include the exact `InventoryPricing.NewUnit` and `PartsMarket.Calculate` fee seams. The internal ready-product-group Ledger overload is explicitly excluded; it remains unchanged for a later typed-conversion checkpoint.

Keep the component dormant. Do not redirect `PartsMarket`, `CartReservations`, `Ledger`, `PartCart.Total`, `DeliveryFee`, Apply, checkout, UI, save, or browser callers. Do not implement the later mutating non-sale Apply, `Total`, quote/checkout, or any command publication. Do not claim generation-fenced merges, stale-command safety, atomicity, worker safety, native/device performance, or completion of full S06/Step 1.

## API and lifetime

Use an operation-scoped borrowed owner, not an eager immutable graph capture. Names may follow repository conventions, but expose the equivalent of:

```csharp
internal sealed class ScopedMarketReadSet : IDisposable
{
    internal static ScopedMarketReadSet OpenBorrowed(PlayerProfile owner);
    internal StockResolution Stock(PartDefinition request);
    internal int Available(PartDefinition request);
    internal int Held(PartDefinition request, bool currentOnly = false);
    internal ScopedSupplyQuote Quote(PartDefinition request, int count, bool unit = false, int quantity = 1);
    internal ConsumeResult Consume(PartDefinition request, int count, bool unit = false);
    internal ScopedSupplyLedger OpenDefaultLedger(bool includeReserved = false);
}

internal sealed class ScopedSupplyLedger
{
    internal int Held(PartDefinition request, bool currentOnly = false);
    internal int Available(PartDefinition request);
    internal ScopedSupplyQuote Quote(PartDefinition request, int units, int basePrice, int quantity = 1);
    internal SupplyTakeResult Take(PartDefinition request, int units, int basePrice, int quantity = 1, bool consume = false);
}
```

Use immutable owned result shapes: `StockResolution` contains `Found`, row ordinal (or `-1` for a warm-index row absent from `market.parts`), in-scope stock identity group and positions, units, initial units, and delivery percent. `ScopedSupplyQuote` copies the four `SupplyQuote` integers. `SupplyTakeResult` contains that quote and optional owned stock deltas. `ConsumeResult` contains the quote and owned stock/hold deltas. Each delta records the identity group, source positions, before-units, and after-units; positions are returned defensively. No result includes borrowed source objects.

The read-set constructor stores the owner reference only; it performs no owner/profile/market/catalogue dereference. Each operation first checks disposal, then follows its exact legacy access/error order. Passing the original `PartDefinition` preserves null and dereference order; do not replace it with an ID-only request or eagerly call `PartProductValue.Capture`. Capture a `PartProductValue` only when the matching legacy branch reaches a product-key comparison. The scope and ledger are synchronous, owner-thread-only, and must not cross `await`, worker, frame, or thread boundaries. This is not a snapshot of a generation: no automatic revision/epoch fence is claimed.

All source graph references are private borrowed implementation details and are cleared on `Dispose`; use after disposal throws `ObjectDisposedException` before touching the owner. Scope and ledger return only copied quote scalars, stock/reservation row ordinals, stable in-scope identity-group numbers, and immutable owned deltas. They never return `PlayerProfile`, `MechanicalCatalog`, `PartDefinition`, `NewPartStock`, `ReservedPartSupply`, source list/dictionary, or mutable array. The internal lookup caches and overlays may hold borrowed object references while open to preserve alias behavior; those references never appear in results and must be severed on disposal.

Do not copy all stock rows, holds, products, or definitions up front. Read unaffected values from the borrowed owner graph in legacy order. Copy a stock or hold quantity into the private overlay only when that quantity is first changed. Product values are lazy and branch-specific. The default Ledger still performs its exact eager constructor walk and first-error order; it does not eagerly deep-copy the graph.

## Private overlays and identity

No operation writes to `market.index`, `NewPartStock.units`, `ReservedPartSupply.units`, catalogue index fields, catalogue revision, or any other owner field. Cold index construction and subsequent successful alias bindings happen in a private lookup overlay. `Consume` and `Take(..., consume:true)` return owned deltas instead of publishing source changes.

Use explicit reference-equality maps for `NewPartStock` and `ReservedPartSupply`. Two positions containing the same object reference must read and update one shared private quantity, just as the old loops mutate one object. Distinct objects with equal fields remain distinct. A row reachable through a warm market index but absent from `market.parts` remains usable and receives an in-scope identity group with no market-list ordinal. Results list affected positions and identity groups but do not expose references. Repeated IDs, repeated list positions, and repeated object references are distinct cases.

Failures propagate with the same first exception type/message and order. The owner graph remains unchanged even when the old operation would have partially mutated a disposable fixture. Do not emit a partial `ConsumeResult` when the operation throws. Any internal overlay mutation before a failure is confined to the private scope and cannot be observed outside it; the failed operation's scope is disposed by the caller.

## Stock algorithm

Keep the exact direct `PartsMarket.Stock` behavior separate from Ledger behavior:

1. If `market.index` is warm, use its actual mapping and comparer as read-only input. An exact ID hit wins immediately, including a later duplicate-row mapping or a row reference absent from `market.parts`.
2. If `market.index` is null, the legacy method first assigns a new ordinal dictionary to `market.index`, then iterates market rows and each definitions list in order using `Add`. A duplicate ID throws at the same insertion and leaves the legacy source with an assigned partial dictionary. The scoped candidate instead assigns its private dictionary before populating it, leaves the borrowed source index null, and retains the same private partial prefix if the exception is caught for characterization. Do not prevalidate, replace `Add` with assignment, or deduplicate. This differs from catalogue `RebuildIndexes`, which publishes its temporary map only after a complete scan.
3. On an ID miss, compare the request's typed product to definitions by market-row order then definition-list order; resolve IDs through the scoped catalogue lookup, skip unknown definitions, and choose the first matching row. Cache only a successful alias in the private market-index overlay; misses remain uncached. Reverse order must change results only where the actual traversal does.

A warm market index is borrowed read-only until a private alias/rebuild overlay is needed. Preserve its actual comparer. Private alias additions never write through. Cold construction uses `StringComparer.Ordinal`, matching the current legacy initialization. Do not implement Stock as Ledger with flags.

## Default Ledger behavior

Only `OpenDefaultLedger` is in scope. The ready-product-groups overload and its `BindReadyProductGroups` behavior are excluded, not approximated.

The normal `Ledger` constructor performs `Active(owner)` before traversing the stock rows. For each row, it stores the initial remaining quantity, and if active reads `definitions[0]`, resolves that definition, calls public Held(current-only), optionally calls Held(all generations), and computes the remaining amount. It then walks definition IDs and assigns each exact ID to the row. Preserve this row/field/exception order even if the quantity overlay remains lazy. The constructor must not be replaced with a filtered or validated read-set prepass.

The normal constructor's exact ID map uses dictionary assignment, so later repeated IDs overwrite earlier rows. On a product miss, the product map is built once by market-row/definition order; known IDs resolve through the catalog scope; first row for each exact supported product class wins. Cache the request ID's row or null result in the ledger scope. Repeated Ledger queries share its remaining-quantity overlay and lookup caches; a fresh ledger initializes from the then-current source values. Preserve the difference from Stock's cold `Dictionary.Add` behavior.

`Quote` does not mutate remaining quantities. `Take` computes through the one shared Calculate method, then subtracts `localUnits` from that row's private remainder; `consume:true` records the resulting source-stock delta but does not publish it. Alias requests resolving to the same stock object share one private remainder. Product misses cached by a Ledger stay cached in that Ledger even if the borrowed graph later changes; no cross-scope persistence is claimed.

## Reservations, availability, and consumption

`Active` is true only when a reservation exists, `version == 1`, and completed runs are `<= expiresRun`. Preserve this check order. It does not check `carId` or generation. Public `Held` returns zero before resolving Stock when inactive; when active it resolves Stock first, then enumerates supply rows and includes exact requested IDs or IDs contained in the resolved stock row. `currentOnly` filters by market generation. Sum uses checked integer semantics and preserves enumeration/overflow order. If active and the request is null, public Held retains Stock's dereference failure order.

`Available` resolves Stock, calls public Held with `currentOnly=true`, subtracts from stock units and clamps at zero. In `Quote`, if `usingCartReservation` is true, add all-generation Held back to that Available result. Keep this asymmetric behavior; do not collapse the formulas.

`Consume` first performs the exact Quote call. It then resolves Stock again, selects matching supply rows, sorts by generation ascending, and consumes quote-local units in enumeration order. Decrement matching hold quantities; decrement source-stock delta for consumed holds only when their generation is current; decrement remaining local units from stock afterward. Preserve exhausted rows, IDs, generations, list order, and unrelated rows. Apply identical arithmetic to private identity-group overlays; return a delta containing every changed hold position/group and the final stock group/position units. No source mutation or partial result escapes.

## Catalogue lookup without publishing

The candidate must not call public `MechanicalCatalog.Find` during its shadow lookup: a cold/stale call may publish `partIndex`, `indexedPartCount`, and `authoringRevision`. Add a narrowly scoped nested `MechanicalCatalog.LookupScope` and an internal factory to that class so the nested scope can read the private index/count/authoring fields. Keep the existing `Find`, `RebuildIndexes`, and index-publication bodies unchanged. A normal completed catalogue uses the non-authoring path, and the test-only fixture may set/restore private `authoring` state by Editor reflection to cover the list-scan branch without adding a production authoring API.

The scoped lookup must mirror `Find`'s branch order. For a null ID, return null before touching the index. If the captured index is null or captured count differs from `parts.Count`, build a private replacement map with the exact `RebuildIndexes` first-ID rule and null-row/null-key exception point, assigning it to the private scope only after the full build succeeds. For a warm index, use the borrowed actual map and comparer. Apply the same bounds/reference/current-ID validity checks on a hit. On an invalid entry, perform the same ordered list fallback; rebuild the private map only when `found != null || indexedPart.value != null`. A miss with no indexed entry does not invent a rebuild. In the authoring branch use the same `List.Find` predicate/order. Never advance source revision or replace source index.

The nested lookup scope and market overlay have no public/source escape. Its product lookup result returns only `found`, source ordinal/reference-group metadata, and a typed `PartProductValue`; if the parent working set needs a borrowed definition for the next exact legacy step, that reference remains private inside the open working set and is never returned to its caller. Tests inspect private index state through an Editor-only diagnostic and compare actual `Find` behavior on independent fixtures. Cover cold, warm-valid, stale-count, displaced/replaced value, mismatched ID, duplicate, null row, null ID, and authoring cases. Capture only the catalog values reached by successful lookup/product comparison; do not mirror the full catalogue. Explicitly fingerprint and assert source list/object/index identities and values before/after scope use.

## Fees and pricing

`InventoryPricing.NewUnit(d,count)` remains the existing method and exact expression: `Math.Max(1, checked((int)Math.Ceiling(d.price / (double)Math.Max(1,count))))`. The scoped quote receives the original `PartDefinition`, count, unit flag, and quantity so it evaluates the same branch and dereference order. Do not duplicate NewUnit logic. Because its divisor is at least one and price/result are `int`, do not invent an unreachable finite-input overflow case.

There is exactly one production `PartsMarket.Calculate` body. After the independent reference below is frozen, its only production change may be accessibility (for example private-to-internal); preserve its body byte-for-byte. The candidate calls this shared body for the exact fee calculation. Freeze an independent literal Calculate implementation before changing accessibility; it must not call the production body or a later-shared helper. Test local/express units, per-unit fee results, accumulated fee/cost values, validation order, exact exception type/message, and each checked-overflow point that is reachable from valid `Calculate` inputs. Do not invent a per-unit-fee or accumulated-fee overflow if the source bounds make it unreachable; prove reachability from the exact loop order before requiring such a vector. The cost overflow case remains required. `Quote` must preserve original call order: Stock; Available (including its own Stock/Held calls); read reservation flag and optionally Held; choose unit/count and `NewUnit(d,count)` or `d.price`; then Calculate.

## Frozen oracle and source boundaries

The root-owned `LegacyScopedSupplyReference` is frozen before candidate production changes and was source-compared to the unchanged live method bodies on the pinned baseline. It is a literal independent reference for Stock, Active/Held, Available, default Ledger constructor/Find/Available/Quote/Take, Quote, Consume, Calculate, NewUnit, and product cache-miss equivalence. It may use the frozen L0 `ProductCacheMiss` as the product oracle, but must not call candidate scoped types, L1A typed equality, or the production Calculate body. The reference is intentionally mutating and runs only on disposable fixtures. After the candidate exists, test oracle liveness against unchanged live methods on independently built fixtures, then run full candidate parity before L1B completion; a separate pre-implementation Unity run is not required.

The pre-extraction manifest freezes `PartsMarket`, `CartReservations`, `MechanicalCatalog`, `SharedInventory`, `MarketCatalogLayout`, the L1A typed-value boundary and helpers, and the frozen references. The only allowed existing-source additions are `MechanicalCatalog.LookupScope` plus its internal factory and changing `PartsMarket.Calculate` accessibility; the existing `Find`, `RebuildIndexes`, and `Calculate` bodies must remain byte-identical. Do not edit existing `Stock`, `Held`, `Available`, `Quote`, `Consume`, default Ledger behavior, pricing arithmetic, or ready-Ledger code. Keep Editor diagnostics separate from production types and keep all source paths frozen during Unity/build runs.

## Required evidence before checkpoint completion

Reconcile test design against this corrected scope before implementation GO. The complete suite must cover:

- Stock cold exact, miss, alias ordering, warm hit/miss, cached successful alias, duplicate-ID partial private prefix, unknown/null/malformed rows and definition lists, reverse row/definition order, and actual warm dictionary comparer/mapping.
- MechanicalCatalog.Find shadow branches, including private-index/revision non-publication and authoring-mode fixture.
- Default Ledger constructor first-error/eager traversal order; later-ID assignment, first product group row, cached misses, fresh ledger, and shared remaining values across aliases.
- Active false/current expiry/following run; public Held direct/alias matching, current/all generations, duplicates/repeated row references, checked overflow and null-request order; Available clamp and using-reservation Quote asymmetry.
- Quote and Consume exact results/deltas, repeated quantity iteration, ordering and stock/hold identity groups, malformed errors, failure without escaped partial result, and unchanged source objects.
- NewUnit count/price bounds and exact null behavior; Calculate every result field, validation precedence and checked overflows.
- Disposable lifecycle, no borrowed reference escape, no source writes, cold/warm index state preservation, complete L1A product parity, no JSON/key-cache/RNG/ID/clock changes in candidate operations, bounded reports, and exact source manifests.

Use actual authored definitions for product equivalence and only explicit ID-only aliases. Test fixtures may arrange market rows and reservations but must not invent physical component identities or repair malformed data. The full required matrix must pass with no skipped unsupported case. This checkpoint does not activate callers or complete full Apply/Total/DeliveryFee/checkout, stock/ready-group conversion, full S06/Step 1, or S07–S14.
