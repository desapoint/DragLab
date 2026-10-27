# S06d2a live warmup and encoder evidence

Status: source implementation is ready for parent-owned Unity/native verification. This note records the bounded warmup request and synchronous encoder attribution. It does not claim the full S06, frame, visual-acceptance, Android, or S07 gates.

## 1. Recover the bounded catalog producer after authored edits

### Finding

`PrototypeGame.RequestNativePartsLayoutWarmup` currently treats any `TryCaptureReadyContentToken` result as ready. A token can still be returned after an in-place same-revision authored edit; the builder later calls `MatchesCurrent`, rejects the token, and fails. Since the warm iterator may already have completed, the request path can leave the page unable to obtain a replacement token. The helper also restarts the iterator whenever its remembered revision differs, even when a producer is already active.

### Implemented behavior

Keep `RequestNativePartsLayoutWarmup(MechanicalCatalog catalog)` as a cheap request/status method:

1. Return false for a null catalog.
2. Retire the current producer only if its catalog reference or authoring revision differs from the request. Return false immediately when the matching producer is active; this avoids a repeated full-catalog validation scan while warming.
3. With no active producer, call `TryCaptureReadyContentToken`. Return true only when a token exists **and** `token.MatchesCurrent(catalog)` succeeds. The typed snapshot check is the readiness proof; token existence alone is insufficient.
4. If that readiness check fails, start one existing `MarketCatalogLayout.WarmSteps(catalog)` iterator and remember the catalog/revision. Return false. The existing owner-thread `UpdateMeasured` advancement remains bounded and is still the only place that advances the iterator.

The helper must not call `MoveNext`, drain `WarmSteps`, request canonical keys directly, call `Key`, or encode JSON. `WarmSteps` remains the explicitly legacy warm producer through S06; its canonical key/JSON work remains visible and is retired under the later S13 work. It must not be presented as part of the converted Parts operation's zero-encoder proof. The request retires a producer only when its catalog reference or authoring revision no longer matches, returns false immediately while that producer is active, and validates a ready token with `MatchesCurrent` before returning true.

When a same-revision edit invalidates the cached token, the next request fails closed and starts a producer only if none is active. An active iterator is allowed to finish or reject its captured content; once it becomes null, a later request starts another bounded producer. This avoids repeatedly resetting progress while making a permanent authored change recoverable. A new catalog reference follows the existing lifecycle that replaces the producer for the newly selected catalog; do not add a second unbounded producer.

### Deterministic native regression

Use one existing isolated native fixture and a known semantic authored field, such as a definition's price. Save the original value, then run this order:

1. Confirm the original catalog token is ready and a bounded Parts model can be accepted.
2. Change only the chosen authored field in place, without changing authoring revision, IDs, seeds, stock, cash, or player assets. Request warmup; it must report preparing, and the bounded existing producer must eventually publish a token whose `MatchesCurrent` succeeds. Build and accept a model using the edited value (state B).
3. Restore only the authored field to its original value (state A). Request warmup again; it must recover and publish a token matching A. Build and accept a fresh A model.
4. Assert the B model used B's typed page facts and the final A model uses A's facts. Require distinct accepted build/data generations. Assert no intervening stock, ID, seed, or purchase changes.

This is a held B then restored A test, not a hash/signature or token-presence test. Keep the existing real mouse activation and route tests; the helper result only indicates whether the typed token currently matches.

## 2. Attribute encoder evidence to synchronous operations

### Finding

`NativeEncoderMark` in the native verification harness is sampled around a yielded click coroutine. `PrototypeGame.UpdateMeasured` advances the existing background catalog warm iterator between those samples. Its allowed legacy key/JSON work can therefore be attributed to the click and produce a false no-JSON failure, even when the converted callback itself invoked no encoder. Removing or pausing the warm worker would hide real behavior and is not acceptable.

### Implemented measurement seam

`Domain.RuntimeDataEncodingProbe` provides a development-only, thread-local scope around synchronous converted operations: bounded builder capture/prepare/publish, fresh read-set fence, query/window publication, card open, guarded Parts action callbacks, and live filter population. Existing counter sites record increments only while a same-thread scope is active; the scope aggregates its local delta on `Dispose`, and nested scopes are counted once by their outer scope. It does not sample process-wide counters, so worker encoder calls cannot be attributed to a UI scope. It must not span a yield, frame, scheduled callback, or pending modal lifetime. Serializer sites in the legacy purchase, move, block, and `PartCart.PreviewProfile` paths now report their real calls into an active scope. Those callback paths can therefore report nonzero until their required typed-preview conversion; the instrumentation does not suppress or relabel them as converted.

The sample includes every currently relevant counter:

- `MarketCatalogLayout.CanonicalKeyRequestCount` and `CanonicalJsonEncodeCount`;
- `NativePartBrowser.LegacyPlanJsonCount`, `LegacyOwnedCarJsonCount`, and `LegacyCartPreviewCount`;
- `NativePartPreviewCache.LegacyCarSignatureJsonCount` and `LegacyEntryKeyJsonCount`.

`Runtime.NativePartEncoderProbeChecks.Run(Action<bool,string>, string)` exercises the scope machinery without invoking an encoder or warm iterator: outside-scope hooks, nested JSON/key hooks, exception disposal and restored depth, sequential same-thread cumulative scopes, and a joined pure-managed worker thread while a main-thread scope is open. It also asserts the canonical encoder/key counters remain unchanged. The test does not reset shared counters and is test-harness-only; the coordinator should wire it beside `NativePartTypedCacheChecks` and run it on frozen source.

The native harness reads operation-local cumulative totals through `NativeMenuShell.PartConsumerJsonCallCount` and `PartConsumerLegacyKeyCallCount` (also exposed by a compatibility readout). They are monotonic process-lifetime sums of completed scopes and have no reset API; `PartConsumerLastOperation` reports the latest completed outer scope label. Tests must sample these around a synchronous operation or production callback, never around a coroutine yield. Core converted empty-cart browse/query/window operations should remain zero; known legacy planner callbacks may be nonzero and remain explicit S06 work. Global counters remain independent evidence that background warm work was not stopped or hidden. `RecordJsonCall` and `RecordLegacyKeyCall` are compiled into the known counter increment sites only for Editor/Development builds; non-development builds have no encoder-counter recording.

Do not reset process-wide counters, suppress increments, disable the warm producer, or subtract background counts from global totals. Do not use a before/after mark across the click coroutine's yields as the converted-path proof. Keep ordinary full-run counters for attribution and the explicit S13 warm-producer debt; operation-local samples only narrow which synchronous call caused an increment.

## Ownership and review boundary

The source implementation is in `PrototypeGame.cs`, the Parts UI wrappers, `MarketCatalogLayout.cs`, the browser/cache encoder counter sites, and `RuntimeDataEncodingProbe.cs`. Parent-owned managed/native runs and high review remain required. Warmup recovery and operation attribution do not pass the full S06 frame, parity, action, human visual-acceptance, Android, or S07 gates.
