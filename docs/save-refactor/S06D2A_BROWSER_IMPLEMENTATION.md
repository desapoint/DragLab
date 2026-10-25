# S06d2a bounded browser/card integration

Baseline `7d6ee35`; typed facts are committed and reviewed. This increment activates bounded empty-cart Parts preparation and card construction. Full S06, nonempty typed PreviewReadOnly, owned refresh and S07 remain open until their gates pass. No physics, price/quantity formula, persistence or design/theme change.

## Owner-thread preparation and freshness

Add a private `NativePartBrowserBuild` state that uses the committed capture session, then prepares the existing exact browser entry/model result in item-capped 2–3 ms soft chunks. Never schedule the whole old constructor on the next frame. Preserve the synchronous constructor as an independent reference/legacy nonempty-cart path; share authoritative computations, not a second reduced-fidelity implementation.

**Before every preparation chunk that reads live owner data**, perform the complete typed read-set comparison against freshly supplied inputs on the main thread; it is a separate measured indivisible validation tail, not part of a claimed 3 ms limit. Main-thread chunks cannot yield/reenter mutating callbacks within their read interval. This prevents transient same-revision A→B→A edits from tainting private prepared entries: seeing B at any chunk rejects that session permanently. Final same-turn validation is also required before model/window publication. Cheap epoch/revision guards alone are insufficient. If a future pure worker is used, it receives only detached immutable values and cannot use this live-data protocol.

During capture, optional prefix checks only reject early; they cannot certify freshness. Complete capture must pass the atomic full `TryFinish` comparison before preparation. Never spread a preparation freshness certificate across frames. Price facts must have unique stable source/asset keys, cover every price consumed on that page, and be freshly regenerated with the same authoritative semantics for comparison; measure their setup and comparison cost too.

Capture/PrepareCar remain on main; an optional CalculatePrepared worker receives its own detached inputs/prepared simulation, no profile/UI/catalog or shared mutable cache. Poll only completed results; no waits on ordinary UI. Retain exact curves, warnings, compatibility, quantities, stock/hold/fee/ownership and comparison deltas. Do not compute planned cart facts from the base read set alone.

A cold/mutated catalog requests the existing bounded warm producer and presents a preparing state. Never finish WarmSteps or canonical keys synchronously. Owner/profile/catalog/car/cart/units/price changes reject private work, with bounded retry and explicit failure state. Preserve critical warnings until a valid replacement is ready; mark old content updating and disable stale mutation actions. No failure fallback to a complete blocking constructor may be counted as a bounded pass.

## Typed caches and live presentation

The converted empty-cart construction/evaluation/card path must not call JSON CarSignature/Key or JSON part-spec keys. Bind preview caches to exact typed facts with an opaque local generation. Retain current inspection only when an explicit typed selected-physical-input comparison proves equality; never use hash-only freshness. Per-entry comparison cache keys use the validated context generation, entry identity and position/role inputs. Preserve bounded cache limits. Retain old serialized bridges only for still-unconverted callers, with no no-JSON completion claim.

Replace the part-spec cache key with an exact typed definition snapshot/equality (including ID and nested/null fields), keeping Summary values unchanged. Skip the unused empty-cart legacy plan-key serialization. No new whole-profile clone, serialization roundtrip, reflection equality loop or mutable player graph mirror.

Model entries remain private until complete. Apply the latest query/search/sort/filters/section, scroll/detail and units at acceptance; do not restore an earlier presentation snapshot. Measure full-section filter/sort and bound it if it violates the frame gate. Keep the existing ~3 ms comparison scheduling and seven exact deltas.

Build the visible card window privately or with stable placeholders under a separate item/time budget. Preserve spacers, order, field precision, IDs, surfaces and handlers; avoid a single expensive first-window loop or final attachment spike. Do not register/tick abandoned surfaces or leave partial facts enabled. Query/scroll/resize/model/owner changes cancel old window tickets; only the latest complete, still-current window becomes actionable. Use existing themed loading/count/notice affordances, mouse-down/hover/selected states and no Tab/focus highlights.

Every data-mutating callback from prepared cards/details must re-resolve current IDs and run the current view/owner/price/stock fence immediately before staging or confirmation, including an event queued before invalidation. Navigation and search remain responsive. Preserve modal/settings/tuning drafts and existing warnings on cancellation/failure.

## Explicit next dependency

Keep nonempty-cart legacy preview behaviour correct and measured; it is not optimized by this empty-cart increment. Before full S06 acceptance, introduce typed PreviewReadOnly and integrate it into the same builder, with no whole-profile clone or JSON preview. Atomic checkout still belongs to S07. No remaining case is waived from the final goal or silently labeled fast.

## Exclusive implementation ownership

| Owner | Paths / responsibility |
| --- | --- |
| s06d_views_medium / Luna medium | Runtime/NativePartBrowserModel.cs, NativeParts.cs, NativePartPreviewCache.cs, NativePartSpecifications.cs; NativePartPurchases.cs and NativeInventoryMoves.cs minimal stale-action fences only; new NativePartBrowserBuild.cs + meta if needed; NativePartsReadSet.cs typed physical-compare helpers only; Domain/MarketCatalogLayout.cs typed definition-equality exposure only; PrototypeGame.cs bounded warm-request helper only; S06D2A_BROWSER_CORE.md |
| s06d_lifecycle_medium / Luna medium | New NativePartsBrowserBuildChecks.cs + meta; new NativePartsBrowserBuildVerification.cs + meta; S06D2A_BROWSER_TEST_MATRIX.md. Coordinate exact builder/hooks API before code depends on it. |
| Fresh Luna-high reviewer | S06D2A_BROWSER_REVIEW.md; independent source/evidence review after freeze |
| Coordinator | Editor runner, NativeMenuVerification routing, Test-NativeMenu.ps1 flag, ledger/status/contract/results, all execution and Git |

No overlapping writes or unrelated control changes. Core must request reassignment for other action-handler files before editing; test callbacks live only in Editor/development probes. Preserve the cart's use of purchase dialogs with independently resolved entries. Freeze all Game source before each run.

## Required gates

Compare stepped and synchronous results with typed assertions across empty-cart Shop/Inventory/Installed: IDs/order/aliases/definitions/prices/stock/express/ownership/compatibility/warnings/precision and all comparison results. Preserve explicitly labeled nonempty legacy parity. Test actual held chunks, A→B→A mutation, owner/context/profile/car/cart/unit/market/catalog/member changes, failure/retry, latest view-state and warning/draft/action behaviour, window cancellation and cache lifetimes. Prove the converted path avoids legacy encoders with real counters/guards plus source review. Record capture, each preparation/validation/query/publish/card phase and all frame tails through first complete list; a later spike is still failure. Run matching seeded native input/captures and relevant regressions/builds, then independent review/local commit. Passing correctness or helper timing alone cannot pass S06 or the full S00–S14 objective.
