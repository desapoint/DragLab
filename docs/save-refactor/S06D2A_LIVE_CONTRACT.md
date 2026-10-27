# S06d2a live empty-cart Parts integration

Baseline `0f2fa8a`. The dormant builder has passed its checkpoint; this step activates it without changing the UI design, simulation, prices or persistence. The umbrella contract is `S06D2A_BROWSER_IMPLEMENTATION.md`. Full S06 still requires typed nonempty/multi-position preview, owned refresh and complete native frame gates before S07. No JSON or whole-profile-copy exception is granted for final completion.

## Ownership and execution

| Owner | Exclusive paths |
| --- | --- |
| s06d_cache_medium / Luna medium | Runtime/NativePartBrowserBuild.cs, NativePartBrowserModel.cs, NativePartPreviewCache.cs, NativePartsReadSet.cs; Domain/MarketCatalogLayout.cs only minimal typed token comparison/exposure; new Runtime/NativePartTypedCacheChecks.cs + meta; S06D2A_LIVE_CACHE.md |
| s06d_views_medium / Luna medium | Runtime/NativeParts.cs, NativePartPurchases.cs, NativeInventoryMoves.cs; PrototypeGame.cs bounded warm-request only; NativeMenuShell.cs staged surface lifecycle/cancellation only; new NativePartWindowBuild.cs + meta if needed; S06D2A_LIVE_UI.md |
| Fresh s06d_live_high / Luna high | S06D2A_LIVE_REVIEW.md; independent design/source/evidence review, no Game edits |
| Coordinator | Runner/tool/native verification integration, shared WaitParts adaptation, ledger/status/contract/results, all Unity/process/Git work |

Concrete APIs and the idle-view boundary below have independent design clearance in S06D2A_LIVE_REVIEW.md. Request reassignment for any other source file. Freeze all Game source for each run. Commit the complete reviewed working checkpoint before the next increment; no worktree or push.

## Two independent lifetimes

Data lifetime includes owner slot/path/epoch, profile/catalog identity and typed values, selected car, page, planned/current mode, cart, units, stock/holds and external price facts. Invalid data cancels private model and query/window work, disables stale data actions and requests a fresh builder. A failed session is terminal; restoring A after observing B cannot revive it.

Presentation lifetime includes query, section, sort/filter, scroll, details and viewport geometry. These changes cancel/restart only query/window work; the base builder continues because it does not consume the query. Publish against the latest presentation generation. Never restore an earlier query, scroll, dialog or Settings/tuning draft snapshot.

Use one owner-thread private model ticket and separately cancellable query/window tickets. Keep current warnings visible until their validated replacement is ready. Existing content may remain visibly updating, with stale mutations disabled. No partial model, metric order or actionable card list is published as current. Cold or directly mutated authored data requests the existing bounded warm producer; requesting never drains WarmSteps/Key synchronously or repeatedly resets a producer already making progress.

## Typed cache API

`NativePartPreviewCache.BindTyped(NativePartsBaseReadSet facts, NativePartsBaseReadInputs freshInputs)` returns an opaque `TypedBinding` with a monotonically increasing local generation. Binding belongs to its exact cache instance; stale/foreign bindings cannot restore/store results. The complete same-turn typed freshness fence precedes binding and every live-reading consumer chunk. This cache is not payment authority.

Compare the prior wide facts to current inputs with typed equality. A changed owner/data context clears entry comparisons and legacy planned-profile cache state and advances generation. No JSON context string or hash-only certificate. Keep the total comparison cache bounded by the existing256 limit across mode changes; do not silently retain two unbounded legacy/typed stores.

Independently retain Current and healthy-current inspections only when exact captured selected inspection inputs match: catalogue identity/content token, selected model/car/engine identities, tune, fuel/fluids, ordered bindings, fitted definitions/instances and complete member conditions, with exact numeric bits and nulls. Cash, another car's unrelated wear, prices or stock cannot evict an unchanged physical inspection merely through a global revision bump. A physical-input comparer is only a cache reuse test, never a current-owner proof. Do not copy a whole profile or catalogue per key.

Typed entry keys contain generation, source/entry/asset/definition identity and position/role. Cached values retain exact incoming typed facts and must reject a same-key incoming condition/specification change. Use the existing bounded LRU and exact seven-delta/inspection results; no serialized definition/part/unit keys.

After full capture finish, the builder uses the supplied cache and typed binding. Expose `CompletedReadSet` and `CacheBinding` only after successful final validation; never expose a partial model as a fallback. Converted `Evaluate`/`Restore` must select typed cache methods and never accidentally enter `CarSignature`, legacy `Key` or unused plan serialization. Existing legacy callers remain correct and separately identified.

Expose a typed healthy-current model/cache path matching the existing `InspectionBuild.Capture(..., healthy:true)` plus `Inspect` calculation. Parts details use it instead of `OwnedHealthy`/`InspectOwned` and their legacy JSON signature. Preserve the same no-wear series and source values. No live wear/fluids are healed.

## Fresh values and active work

Every model/query/card/detail preparation chunk that reads live values runs one complete atomic `MatchesCurrent` with fresh owner inputs immediately before its synchronous read interval; no yield or mutating callback inside the interval. Initial capture keeps the committed dormant protocol: prefix checks can only reject early, then one complete atomic `TryFinish` is required before preparation. Final model/window/action publication has a full same-turn fence too. Future incremental probes can only reject early, never certify freshness. Workers, if justified, receive detached immutable scalar inputs only; no live profile/catalog/UI, owner callback or ordinary main-thread wait.

Regenerate exact consumed external facts for each full fence: Shop `Used`; Inventory `OwnedPart` for nonselected inventory parts and `OwnedUnit` for each loose unit; Installed none. One fresh shared `NativeSalePrices()` view per Inventory wrapper preserves global validation and exact valuation arithmetic. Null unavailable owned prices remain null; missing/duplicate/unused keys fail. New Shop costs use the existing ready ledger, not duplicate quote generation. Measure factory/wrapper cost separately. If it breaks the frame budget, propose a narrow typed dependency-proven display-price optimization; never reuse stale prices based only on revision or timing.

Reviewed integration clarification after native4987: an Inventory wrapper with no price-backed rows needs no sale-price factory; initialize it lazily at the first consumed row, preserving full fresh validation for nonempty inventory. Within one synchronous owner-thread query/window stack, generate fresh external facts before the chunk and perform the complete typed comparison again before publication using those same facts. The intervening preparation must not yield, invoke application callbacks or mutate live ownership/pricing dependencies. `PublishPartWindow` has one caller, `AdvancePartWindow`, immediately after its fresh fence; queries likewise advance without reentry. This avoids duplicate price-factory validation inside that single interval. It grants no reuse across frames, callbacks, action confirmations or separate chunks; each still generates fresh consumed prices and passes the full current-input fence.

While the published view is idle, cheap owner/profile/revision/storage/car/page/cart/units/cash/reputation/market invalidators run each frame; no periodic full price regeneration or validation is required. Rendered strings remain the last accepted presentation, never a fresh-data certificate. A same-revision direct public-field edit with no notification is detected before the next actual model/query/card/detail consumption or action, including a queued event before the next LateUpdate. That full freshly priced fence fails closed, disables stale actions and triggers rebuilding. Cheap direct header/notification reads are not cache or mutation authority. Do not use an incremental probe, old prices or a rendered label to authorize work.

## Queries and cards

Static Price/Name browsing uses intrinsic four-fact summaries and does not evaluate every candidate's full car performance. Preserve the existing `RequiresChanges` behavior: basic filters first, then evaluate only relevant uncached change-dependent candidates under a budget, and publish the complete exact sorted result together. Query and sort must be measured and bounded if needed; do not move a stall to final sorting.

Build only the current virtual card window, keeping existing row geometry, IDs, values, disabled/selected/hover/pressed states and spacers. Create cards/surfaces privately under separate item/time budgets. Unpublished surfaces are not registered or ticked; abandoned cards release every registration/handler/reference. Recheck current scroll/viewport/presentation generation before accepting. Final attachment/registration/layout is measured as an indivisible tail; CPU creation alone cannot prove a frame pass. Do not disable navigation, search or numeric editing.

## Action origin and revalidation

Parts-origin actions capture an explicit optional origin token identifying the accepted data/model/window generation and entry ID/source. At click/confirmation, resolve that ID in the current accepted model and actual owner graph, regenerate facts and pass the complete owner/price/stock/car/cart/unit fence before staging or mutation. A queued event after invalidation fails closed and triggers an update; it cannot act on a retained entry object. Keep existing transaction validation and manual-refill exclusion too.

Cover card/detail open, position Apply, direct unit-install confirmation, purchase confirmation, remove/sale/move staging and relevant tire/move dialogs. Preserve purchase drafts and current critical warning text on failure. NativeCart calls to purchase/move dialogs pass no Parts-origin token and retain their independent legacy validation; do not infer exemption from `ScreenName` because Cart shares the Parts route. General legacy transaction/checkout internals are not converted by this display checkpoint and remain later mandatory work.

## Gates and diagnostics

Editor/Development hooks pause only after a real completed model/query/window step, never manufacture a result. `PartBrowserPreparationPending` includes model/query/window work, so shared waits cannot dereference an unpublished model. Expose scalar generation/state/entry/card/action and factory/capture/validation/preparation/query/sort/card/attach diagnostics. Keep test-only controls out of normal gameplay.

Test exact cache hits/misses/eviction and physical reuse after cash/stock/other-car changes; changed physical/member/authored values must miss. Verify actual held A→B→A rejection, latest query/scroll/resize, route/F8/profile/owner changes, stale card and modal confirmation, warning/draft retention, cancellation/failure/retry, intrinsic browse without mass comparisons, all seven deltas and complete stat/dyno parity. Native frame evidence runs through first fully actionable list and comparison/detail readiness; a late spike is failure. Fresh Windows/native captures and independent high review precede the local commit.

Prove zero legacy encoders/key calls inside the converted consumer operations. Existing background authored warmup can still invoke its explicitly unconverted canonical encoder until final retirement; report that boundary separately rather than masking it or misattributing its calls to the UI. Legacy nonempty/multi-position previews remain functional, measured and explicitly outside this empty-cart conversion until typed PreviewReadOnly is integrated inside S06. No full S06, Android, human acceptance or S07 claim follows from this intermediate checkpoint.
