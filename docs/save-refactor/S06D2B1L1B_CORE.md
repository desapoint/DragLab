# S06D2B1L1B — dormant scoped stock and reservation core

Status: **source authoring complete; root verification and independent Luna-high review now approve the bounded checkpoint.** See the [final results](../../reports/save-refactor/S06D2B1L1B_RESULTS.md) for the actual checks and build. This authoring note makes no caller activation or performance claim.

## Changes in this checkpoint

- Added [ScopedMarketReadSet.cs](../../Game/Assets/DragLab/Scripts/Domain/ScopedMarketReadSet.cs) and its Unity metadata. The constructor stores only its borrowed owner reference; it does not dereference the graph until an operation runs. The scope is disposable, and the normal Ledger is nested within it so source-row references remain private implementation state.
- The scope models direct Stock, Available, public Held, Quote, Consume and the normal default Ledger. Ready-product-group Ledger bindings are excluded. All live callers remain unchanged.
- Stock and hold quantities use private overlays keyed by source object reference; stock writes assign only `stockOverlay[row] = units` after resolving the existing identity group. Cold Stock index construction uses a private ordinal map and retains `Dictionary.Add` partial-prefix behavior on errors. Warm indexes are read in place; successful alias mappings go to a private comparer-matched overlay. No source index or quantity is written.
- Product comparisons capture `PartProductValue` lazily at the existing product-comparison branches. Ledger product hash buckets retain separate exact-equivalence representatives, so hash collisions do not decide product identity.
- Results contain copied quote scalars, stable identity groups, source ordinals, and defensive position arrays. `Consume` returns one final before/after delta per changed stock or hold object identity group. It preserves legacy unchecked hold/stock arithmetic, including malformed negative hold values, while `Calculate` remains the sole fee implementation.
- Added `MechanicalCatalog.LookupScope` and its factory without editing the existing `Find` or `RebuildIndexes` method bodies. The private shadow follows their branch order and does not publish index/revision changes. Its borrowed catalogue references are cleared on disposal.
- Changed only `PartsMarket.Calculate` accessibility so the dormant scope can call the one production body. The body and `InventoryPricing.NewUnit` are unchanged. The root-owned frozen literal oracle and pre-extraction closure hashes remain the independent evidence boundaries.

## Review and evidence still required

The implementation is not complete until the full assigned L1B checker passes against the frozen oracle. Required cases include cold/warm index semantics, duplicate/error ordering, catalogue lookup branches, normal Ledger eager construction and cached misses, reservation generations and expiry, checked `Sum(int)` behavior, exact quote values, reachable arithmetic overflow, identity aliasing, source nonmutation, disposal, and no JSON/key-cache/ID/RNG/clock effects. In particular, the results must be checked against warm exact hits when `market.parts` is null and against LINQ null-argument boundaries on warm misses.

Root owns Unity execution, source-manifest verification, compilation/build decisions, process checks, and Git staging/commit. No runtime or compiler validation is claimed here. This checkpoint does not complete stock/ready-group conversion, Apply, Total, DeliveryFee, checkout, full S06/Step 1, or S07–S14.
