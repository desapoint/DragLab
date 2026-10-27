# S06d2a live typed Parts cache

Implementation status: source changes are ready for the coordinator's frozen-source Editor/native verification and independent Luna-high review. This is not a passed live S06 checkpoint.

## Contract implemented

- `NativePartPreviewCache.BindTyped` requires a full successful read-set comparison and returns an opaque cache-owned binding with a monotonic data-context generation. The builder invokes it only after `CaptureSession.TryFinish`; typed restore/store reject foreign or stale bindings.
- Wide context reuse compares the previous read set against newly generated inputs, including owner, profile/revision/storage, page/cart/units, price facts, selected context, market/reservation/vehicle stock and authored-content token. A change advances generation and clears comparison entries.
- Physical inspection retention uses a separate exact typed comparison: catalog and ready-content snapshot identity, base vehicle-spec identity, selected car/model/engine IDs, ordered installed slots, exact tune/fuel/fluid bits, and complete captured fitted-part states. Wide context changes can retain current/healthy inspections only when this comparison succeeds.
- Typed entry lookup keys contain generation, source, entry/asset/definition IDs, position and role. Stored values retain typed definition, part and unit snapshots; a same-key input with changed authored fields or component conditions misses. Legacy and typed comparisons share the 256-entry LRU capacity.
- The empty-cart builder passes its typed cache binding into the model after full capture validation, fences each preparation step and final publication, and exposes `CompletedReadSet`/`CacheBinding` only after success. Typed evaluation never selects legacy `Bind`, JSON `CarSignature`, JSON entry `Key`, or plan serialization.
- `HealthyCurrent` is lazy and uses the same healthy `InspectionBuild.Capture` and `Inspect` calculation as the legacy detail path. The UI caller must first perform its complete current-input/detail fence.

## Checks and limits

`NativePartTypedCacheChecks.Run` covers exact reuse, condition and authored-value misses, bounded eviction, legacy encoder counters, exact physical-input equality, wide-context invalidation with retained physical inputs, and tune mutation rejection. The coordinator must run it on the final source together with existing read-set/browser suites and native capture checks.

No Unity, process, performance, native, visual, or human acceptance checks were run by this author. Owned refresh, nonempty/multi-position typed preview, complete action fencing and the full S06 frame gate remain open.

Coordinator correction before execution: `MechanicalCatalog.Spec(model)` returns a detached copy, so comparing those copies by reference would always evict current/healthy inspections. The physical comparer now identifies the baseline through exact catalogue identity and selected model. The catalogue's `specs` dictionary is private, populated only during construction/detached capture; public `Spec` returns copies, and `Recipe`/`Resolve` read those baselines (`PartTuning.Project` only writes the supplied tune). Authored part changes still require the exact ready-content token and fitted-definition snapshots. No baseline vehicle clone or mutable baseline reference is added to cache keys. Independent capture and cash/stock/other-car retention checks exercise this distinction.
