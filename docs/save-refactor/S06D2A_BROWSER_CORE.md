# S06d2a bounded browser core checkpoint

This is a dormant backend checkpoint. It does not change `NativeParts.RefreshParts` or claim that live navigation/card publication is bounded. The synchronous `NativePartBrowser` route remains the UI path until a separately reviewed activation step.

`NativePartBrowserBuild.Begin(inputs, cache)` accepts only an empty cart and unique nonempty external price keys. The `cache` argument is retained for call-site/test continuity but is not used by this checkpoint; no JSON preview cache is consulted. Nonempty cart previews remain on the synchronous legacy path until typed `PreviewReadOnly` is integrated within S06.

The builder advances the existing typed read-set capture in bounded item/time steps. Once capture completes, `TryFinish` performs the full same-turn typed fence. It then privately initializes the existing authoritative model and consumes its unchanged entry/population/classification operations from an iterator. Every `Advance(freshInputs, itemCap, softMilliseconds)` performs a full `MatchesCurrent` immediately before the synchronous preparation chunk; there is no yield or callback dispatch inside that chunk. The model remains unavailable until another full comparison succeeds after the final chunk. Any failed comparison is terminal; callers retry with a new builder and newly captured inputs.

The Shop builder borrows ready groups and alias metadata from the read set’s opaque authored-content token. It never calls the cold `TryReadyShopProductGroups` validator or the canonical-key encoder. `PartsMarket.Ledger` has a ready-group overload that binds aliases before held-stock calculations. Its reservation lookup keeps the original exact definition-ID and first equivalent stock-row semantics without mutating the shared market index or encoding product keys. The public legacy `Ledger` and `CartReservations.Held` paths remain unchanged.

Page valuation inputs contain only externally computed facts consumed by that page: `OwnedPart` keyed by asset ID, `OwnedUnit` keyed by element ID, and `Used` keyed by dealer ID. Missing/duplicate/unconsumed facts reject this builder; a null owned-asset valuation remains the existing unavailable-price case. New Shop costs continue to use the exact `Ledger.Quote` calculation from captured catalog, stock, reservation, and supply-count facts, so the builder does not precompute a duplicate market-wide quote table.

`NativePartSpecificationCache` now keeps a source definition reference, deep typed snapshot, and computed `PartStat[]`; same-revision edits fail the typed equality and recompute the summary. The bounded empty-cart model does not bind or query `NativePartPreviewCache`, avoiding its legacy JSON `CarSignature` and per-entry key paths. Typed cache reuse and retaining an unchanged physical inspection remain follow-up work before live activation.

Fitted-position loose-unit evaluation now uses an explicit private typed projection of the selected car, installed bindings, fitted part copies, selected loose unit, shared catalog, and the run-checkpoint references read by `AssemblyOperations.MutableCar`. It calls unchanged `InventoryUnits.Install` and `InspectionBuild.Capture`. The read-set therefore fences the exact unsettled-run guard. No unrelated profile assets or marketplace state are cloned. Multi-position `PreviewNewUnit` still uses `PartCart.PreviewProfile`; its legacy-call counter makes that boundary visible. It remains outside the converted empty-cart model’s no-encoder claim until typed preview is integrated.

Editor/Development diagnostics available on `NativePartBrowserBuild` include capture progress and largest capture step, preparation operation count, model setup time, total/largest preparation step, full validation total/largest tail, and final publish validation. The encoder/key-request counters are compiled only for Editor or Development builds; non-Development release builds have no counter storage or increment cost. These are diagnostic measurements, not a performance pass. The first live UI checkpoint must separately measure query/sort, comparison, complete visible-window build/attachment, and action publication through the first complete list.

## Source scope for this checkpoint

- `Runtime/NativePartBrowserBuild.cs` + `.meta`: dormant builder state, terminal failure/cancellation, fresh-input fences, and scalar phase diagnostics.
- `Runtime/NativePartBrowserModel.cs`: resumable exact entry/classification iterator and typed scoped spare-unit preview projection; existing synchronous constructor remains the oracle.
- `Runtime/NativePartsReadSet.cs`: external price uniqueness/coverage, catalog-token group borrow, and run-checkpoint freshness facts.
- `Runtime/NativePartPreviewCache.cs`: Editor/Development counters for legacy cache encoders; the dormant builder does not call them.
- `Runtime/NativePartSpecifications.cs`: exact typed definition-snapshot specification cache.
- `Domain/MarketCatalogLayout.cs`: opaque token exposes completed immutable group references and typed definition equality/copy helpers.
- `Domain/PartsMarket.cs`, `Domain/CartReservations.cs`: ready-bound ledger held-stock resolution using existing exact stock semantics.
- `docs/save-refactor/S06D2A_FACTS_CORE.md`, this report: facts contract and checkpoint boundary.

No Unity or build checks were run by this author. Parent-owned Editor parity, mutation, no-encoder, and performance runs are required before this checkpoint can be reviewed or committed.
