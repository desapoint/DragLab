# Market-refresh wait audit — 2026-10-18

Requested scope: identify the delay and recommend a generation change. This report does not change supply probabilities, quantities, seeds, reservations or refresh timing.

## Evidence

The real Race HUD player run `raster-roster-race-20261018-a` measured a 293.590 ms main-thread settlement step at `parts/layout and selection`, and a 300.206 ms maximum frame. The existing <50 ms pacing gate correctly failed; there were zero runtime exceptions. The practice settlement's maximum step was 37.202 ms. These are local observations, not Android timings.

`PartsMarket.GenerateSteps` stages product-key batches, but its next step synchronously obtains the catalogue layout, tests product fit across model specifications, builds alias groups/maximal set sizes and performs the legacy selection/quantity plan retained to preserve used-market streams. A cold `MarketCatalogLayout.Get` constructs every group in one call. The measured label includes this whole block; finer profiling is required before assigning the entire 293.590 ms to one expression.

`PrototypeGame.AdvanceFinishSettlement` advances exactly one iterator item per Update. This avoids combining work but imposes one display frame of waiting for each tiny item, even if many could fit in a short CPU budget. Parts, wear coverage and 52 new-car offers add many yields. Thus merely adding more yields can eliminate a hitch while extending the wait.

Used-car inspection already has an isolated physical worker/cache. `WarmNext` is pumped only outside Race. Cached layouts/warm markers are keyed by catalogue object identity; transactions can replace that catalogue. Warming outside a race therefore does not guarantee a warm catalogue at the finish. The warm marker also omits the purchase count even though generated offer identities include it. Preparation invalidation should include every input to the corresponding cached output. Do not move Unity asset lookup or mutable player state onto arbitrary workers to compensate.

## Recommended implementation checkpoint

1. Retain an immutable authored-catalogue snapshot and derived fit/topology/category layout by content revision across transactions. Invalidate explicitly when authored definitions change. Keep per-save ownership, condition and inventory out of that cache.
2. Divide cold layout/fit preparation into bounded groups. Pump ready preparation steps together within a measured 2–3 ms frame budget instead of one step per frame. Stop immediately at an asynchronous wait; do not spin on a pending writer/inspection. Record longest individual step and total preparation wall time.
3. Prepare immutable next-epoch inputs while racing and in menus. Use the existing private seeded streams. Workers may consume copied plain data only; asset loading and Unity-object access stay on the main thread.
4. Finalize wear priority against the **actual finish checkpoint**, current owned cars, available used inventory, new stock and cart reservations. Those inputs can change while preparing. Validate a revision key before using prepared work; recompute only stale dependent pieces. Do not pre-roll a final market against old wear or replay a different random stream.
5. Atomically publish/persist the complete candidate epoch with the result, retaining the existing single reward receipt, save-failure recovery, three-race refresh and nine-race reservations. Until publication, expose the current committed stock. The player should never see half a market or receive the same reward twice.

Acceptance: seeded product/quantity/used-stock fingerprints unchanged for identical inputs; correct invalidation after purchases/cart edits/wear/ownership/settings changes; save-failure/reload and duplicate-reward tests; cold and warm player-frame traces including the third race; Android-device timing when connected. Target a few milliseconds of preparation per frame and no finish hitch. Measure end-to-end wait as well as frame duration.

A new database is unnecessary for this cost: the current durable save already uses SQLite and its writer is isolated. The observed stall precedes the durable write.
