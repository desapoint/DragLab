# S06D2A live Parts UI integration

This checkpoint activates the approved empty-cart Parts builder from `S06D2A_LIVE_CONTRACT.md`. It keeps the existing UI controls, card values, sort/filter rules, warnings, and modal drafts. The nonempty-cart compatibility route and its `PreviewProfile` behavior remain explicitly legacy pending the required typed `PreviewReadOnly` increment; this checkpoint does not claim the full S06 no-JSON gate.

## Implementation

- Empty-cart data builds use the typed `NativePartBrowserBuild` with the stable preview cache and the existing incremental catalog warmup. Builder, query, and card-window tickets have independent lifetimes. A presentation change advances only the presentation token; query, sort, and the current virtual card window are completed against the latest values.
- Query filtering and exact sorting run in bounded steps. Cards are created into a detached `partsGrid` with unregistered surfaces. After a full same-turn freshness check, publication replaces the displayed grid and registers the staged surfaces. Scroll, resize, or viewport replacement invalidates the window token and disables old card actions before new work proceeds.
- Builder and query steps now allow up to 512 inexpensive items while retaining the 2 ms soft-time check; card creation remains one card per step. Query preparation counts the active section in the same filter pass, and window publication restores the accepted `matches / section total` count and empty-state label without rescanning the browser. A requested development hold survives the source-change restart it was meant to observe; route cancellation still clears it.
- Search/category/filter/sort/scroll/resize state stays in its existing controls and objects. Warnings and modal/draft trees are not cleared during refresh. Existing cards can remain visible while updating but are disabled until a newly validated window is accepted.
- During refresh, the detail footer actions and the replacement-position mutation are disabled while the back button and read-only detail navigation remain available. The production freshness guard still rejects stale callbacks.
- Parts-origin card/detail actions retain entry ID, source, and data/presentation/window generations. Card open, purchase confirmation, unit-position selection/application, move preview/confirmation, block-preparation updates/confirmation, and tire-swap selection/confirmation validate fresh typed facts before reading live values or staging a change. `NativeCart` purchase flows pass no Parts token and preserve their own existing validation.
- The converted detail path uses `NativePartBrowser.HealthyCurrent` for its no-wear series. The existing typed part-spec summary cache is retained. The move and block planners still use their existing exact legacy preview machinery after the Parts-origin fence and are tracked for later typed preview conversion.
- Editor/Development hooks expose preparation pending/state, data/presentation/window/detail generations, published card count, action availability, and a hold after the next real builder/query/card step. Sort timing accumulates only the work added by each query step. Scalar diagnostics separate price-fact creation, current validation, query, sort, card creation, attachment, and detail construction.

## Validation

No Unity or player execution was run by this implementation owner. Parent-owned Editor/native runs and independent high review are the acceptance gate. In particular, the indivisible final grid attach/layout tail, current validation and external price-view creation must be measured in those runs. Query, card creation, and detail work are not claimed to meet a frame budget until measured.

## Remaining required scope

Nonempty cart previews, multi-position purchase preview, move/block legacy preview internals, and remaining native frame evidence are still required within S06. This UI checkpoint makes no S06 completion or S07 claim.
