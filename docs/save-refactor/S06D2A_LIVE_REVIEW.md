# S06d2a live Parts integration — design review

Date: 2026-10-25
Review type: independent Luna-high design approval before live-source activation.
Scope: proposed integration of the committed dormant typed browser builder, typed cache reuse, bounded visible cards, query handling, and mutation fences. No source edits, Unity runs, process interaction, or Git operations.

## Verdict

**Approved to begin source activation after the parent freezes the live integration contract.** The existing `NativePartBrowserBuild` API is a suitable bounded model producer for the empty-cart route. This approval covers the proposed sequence and fences below; it does not approve implementation, evidence, responsiveness, visual acceptance, full S06, or S07. Re-review frozen source and fresh evidence before any local checkpoint commit.

The whole S00–S14 plan remains authorized. Full S06 remains open until typed nonempty-cart `PreviewReadOnly`, owned-car completion refresh, live action fencing, and the complete native responsiveness gate pass. Nonempty-cart and multi-unit purchase paths may remain explicitly legacy for this live empty-cart step only; they must be converted within S06 before its final gate. They cannot be treated as a passed no-JSON result or deferred beyond S06.

## Minimal integration contract

### 1. Per-view owner-thread session

- Create one private build session for the current Parts owner/page/planned context. Capture typed price facts once per input wrapper with stable unique `(source, assetKey)` keys (`OwnedPart`, `OwnedUnit`, `Used`) and exact consumed-row coverage. Do not recreate the price map for every preparation chunk; each freshness comparison receives newly supplied current facts with the same authoritative semantics.
- Ask `PrototypeGame` only to request/advance the existing bounded catalogue warm producer. The helper must not synchronously finish `WarmSteps`, generate canonical keys, or duplicate Shop pricing. A cold catalogue leaves the page in a preparing state while the existing producer advances.
- Keep the `NativePartBrowserBuild` and its model private. Use `Begin` for the empty cart and `Advance(freshInputs, itemCap, softMilliseconds)` on owner-thread updates. No live profile/UI/catalog/market object goes to a worker. No main-thread wait or whole-constructor fallback is allowed.
- Capture is not certified until the atomic full `TryFinish` succeeds. Before every later chunk that reads live owner data, do one complete `MatchesCurrent` comparison immediately before the chunk; do not yield, dispatch callbacks, or spread this comparison across frames. Any mismatch, including an observed A→B→A intermediate state, terminally rejects and discards that builder. Final model acceptance also requires a full same-turn match.
- Make cache binding depend on an opaque local generation tied to this validated session. Retain an inspection only after exact typed physical-input equality against the new capture. Do not use hash-only equality, JSON keys, or a cache entry from another generation. Per-entry results use generation plus exact entry ID/position/role. Keep cache capacity bounded and release abandoned-generation entries.

### 2. Latest-query, bounded-window publication

- Preserve query state independently of builder state: search, filters, sort, source/category section, scroll, selected detail, unit preferences, purchase drafts, settings/tuning drafts, and critical warnings. Do not snapshot/restore old UI state when a build completes.
- After the typed model is complete and freshly accepted, apply the latest query to it. Query/filter/sort work must be measured as its own phase and divided or otherwise bounded if it can exceed the frame gate. Continue to preserve exact ordering, precision, IDs, compatibility, stock/holds/fees, warnings, and all seven existing comparison deltas.
- Build the visible card window as a separate cancellable ticket with stable placeholders/spacers. Bound card creation and attachment by item/time per update; measure query, each window step, attach/publish, and all frame tails through the first complete actionable list. Avoid one large first-window loop or an attachment spike after bounded model preparation.
- Every ticket carries the current view/build generation and relevant window range. Search/filter/sort/section/scroll/resize, selected owner/page, units, cart, catalogue/market/price changes, or any new model acceptance invalidate obsolete work. A cancelled/partial set of cards must not be registered, ticked, or actionable. Only a complete window from the latest still-current ticket can publish. Release its private cards/surfaces on cancellation and teardown.
- Surface registration/ticking begins only at successful publication; teardown or cancellation unregisters/releases exactly the surfaces created by that ticket. Preserve existing layout, focus policy, mouse-down/hover/selected states, and dialog behavior; this step does not authorize a visual redesign.

### 3. Mutation and presentation fences

- Every button or queued callback that changes state must resolve the current entry/asset/unit IDs at invocation time and validate the current typed view/owner plus relevant price, stock, reservation, and cart facts immediately before staging or confirmation. Do not trust a captured `NativePartEntry`, display object, position, purchase draft, or formerly valid token when the callback was queued before invalidation.
- Apply these checks to all Installed, Inventory, Shop purchase, sale, move, cart-edit, and detail actions owned by the integration; retain the `NativeCart.OpenPartPurchase` path, which may create a cart line without a Parts token. Preserve its independent resolution and existing transaction contract.
- On invalidation or failure, disable stale mutations, retain warnings and user drafts, show an updating/preparing/error state, and retry only with a fresh builder and fresh inputs. Search and navigation stay responsive. Do not silently fall back to blocking legacy computation.
- A typed builder may publish only the empty-cart model. A nonempty cart or multi-unit preview that still uses `PartCart.PreviewProfile` is explicitly identified, measured legacy behavior until typed `PreviewReadOnly` is integrated inside S06. Its preview must not be mislabeled typed or fast.

## Review gates for the frozen source

Before source activation, freeze and share the concrete hook/API contract, including ownership of the `NativeParts` lifecycle helper and every callback path. At final review, inspect the complete diff and verify:

1. No JSON encoder/key/equality calls, `JsonUtility`, whole-profile copy, reflection comparison, or player-graph mirror is reachable from the converted empty-cart builder, cache binding, query/card generation, or action-fence path. Keep editor/development counters or guards proving this rather than relying on inspection of one call site.
2. Every capture, preparation, query, comparison, window-build, attach/publish, retry, cancel, and action boundary retains the exact fences above. Full `MatchesCurrent` occurs before each live-reading chunk and final publication; no partial certificate or epoch-only shortcut is used. A→B→A is rejected when B is observed.
3. Exact typed synchronous parity remains for the empty-cart Shop, Inventory, and Installed pages, including IDs/order/aliases/definitions/prices/stock/express/ownership/fit/compatibility/warnings/precision and all seven deltas. Existing nonempty legacy parity remains explicitly labeled.
4. Typed cache reuse passes exact-equality controls: exact unchanged source can reuse; same-revision physical mutation recomputes; changed generation/identity/position misses; no hash-only retention. Card windows preserve the last accepted query/scroll/details and warning/draft state through resize and cancellation.
5. Actions fired after invalidation or queued before invalidation reject safely; purchases, moves, sale and cart callbacks resolve current exact IDs and preserve cart/transaction semantics.
6. Fresh native evidence records every frame from entry through first complete actionable list and any later spikes, with separate phase tails and the 50 ms gate applied. Run matching seeded captures and regressions/builds. Legacy-route screenshots do not stand in for activation evidence. Automated screenshot parity is not human visual acceptance.

## Approval boundary

The current evidence reviewed is for the dormant backend only: final Editor84128 and Windows74791 pass; native64679 covers the unchanged legacy route with 70 checks, zero runtime errors, and 14 byte-identical screenshots. The recorded legacy route still has a 118.455 ms maximum frame and 90.100 ms Parts frame, so responsiveness remains FAIL/OPEN. None of those results demonstrate live builder, cache, query, card, action, Android, or S06 acceptance.

The approved next work is source activation of the **empty-cart** bounded model/cache/query/card/action path under this contract. Then freeze source and return for independent implementation/evidence review. Typed nonempty-cart preview and owned refresh still belong in S06 before S07; final S00–S14 requirements remain unchanged.

## Frozen-contract clarifications

**Published idle view:** cheap owner/profile/revision/storage/car/page/cart/unit/cash/reputation/market-generation invalidators may disable stale actions and cancel pending work every frame. Do not rebuild external price facts or run the complete read-set comparison as a free-running idle poll. A stable rendered label is only the last accepted presentation; it is not proof that mutable public fields remain current. Before any later operation reads live model/query/card/detail data or publishes a result/action, regenerate the required fresh prices and perform the full atomic `MatchesCurrent` in the same turn immediately before that read/action. If a cheap invalidator fires, invalidate promptly. A direct same-revision edit without a notification is detected by the next full read/action fence; do not claim idle detection. An action queued before invalidation must still fail its own immediate full fence, even if the UI has not yet run its next update. This matches the stated transition requirement without imposing repeated Inventory price generation, which has previously measured 46 ms for a shared-price view. No incremental comparison or price snapshot may certify freshness.

**Cache/model API coupling:** final source review must confirm `Begin` actually binds the typed cache only after successful full capture/freshness validation, and that typed `Evaluate`/`Restore`/`Store` paths never reach legacy `Bind`, `CarSignature`, `Key`, or plan serialization. `NativePartBrowserBuild` currently constructs its model with a null cache and the model stores references to the live profile/part/unit/catalog objects; activation must close both seams, not just replace the key function. Expose the completed read set and opaque cache binding only after final validation. For unchanged physical inspection reuse, require exact selected-input comparison and a cached healthy-current inspection matching `InspectionBuild.Capture(..., healthy:true)`/`Inspect`; cash, stock, unrelated-car wear, or a broad revision bump must not evict exact physical reuse. Use this healthy typed path in Parts detail in place of `OwnedHealthy`/`InspectOwned` and their JSON signature. Physical equality is cache reuse only, never an owner-currentness proof.

**Query read boundary:** presentation/query/window generations remain independent of the base builder. Static price/name browsing should use intrinsic summary values and avoid mass performance evaluation. When `RequiresChanges` is enabled, apply cheap filters first, evaluate only needed uncached candidates under a budget, then publish the complete exact order. If query/card/detail work reads live object references retained by the model, it still needs the complete fresh fence immediately before that synchronous chunk. Prefer immutable accepted presentation values for idle rendered strings; never infer their freshness from an old fence. Current query/card/action publication has its own final same-turn fence.
