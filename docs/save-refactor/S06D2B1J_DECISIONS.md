# S06d2b1j — whole-engine transition prerequisite

Baseline main `1c76d62`. Previous goal turn made verified progress: I is locally committed with 393 focused checks, 214,320 existing regression assertions, a Windows build and independent Luna-high approval. Continue the complete authorized S00–S14 plan; this prerequisite does not close Step1/S06 or enable S07. Runtime manipulation must ultimately use typed state with no JSON encode/decode and no full-profile save/preview clone or mirror.

## Outcome and ownership

Extract the authoritative whole-engine Move/Detach/Attach family into shared operations used by existing live wrappers and a dormant private typed projection. Cover Storage-to-Storage, stored-engine-to-car, attached-engine-to-Storage, car-to-car and same-parent no-op. Do not add UI callers, persistence/checkout, worker execution or new fitment rules. Whole parts remain on the reviewed I core; loose-unit and full cart composition remain pending.

| Owner | Exclusive paths |
| --- | --- |
| Luna medium / s06_identity_fix_medium | Existing Domain/InstalledPartTransition.cs, AssemblyOperations.cs, SharedInventory.cs; new Domain/InstalledEngineTransition.cs +meta and WholeEngineMoveProjection.cs +meta; WholePartMoveProjection.cs only to expose/reuse the optional car snapshot helper and keep nullable binding capture inside its query-error boundary; J_CORE |
| Luna medium / j_tests_medium | New Runtime/WholeEngineMoveProjectionChecks.cs +meta; J_TESTS |
| Luna high / j_review_high | J_REVIEW only |
| Coordinator | Frozen Editor-only reference +meta; Editor runner; this contract/results/status; processes, source freezes, checks/builds/Git |

Code paths are relative to Game/Assets/DragLab/Scripts; notes are docs/save-refactor/S06D2B1J_*. Request reassignment before any unlisted edit. Root alone runs Unity/builds/players/Git. Authors release before source freeze; no Game/tools writes during runs, and terminal source-hash/process verification precedes thaw. Preserve unrelated files, F8/mobile behavior, bench, simulation/audio/content and stable metadata. One tested/reviewed local commit precedes the next increment.

## Shared operation and error order

Keep common Move preamble and classification: unsettled run, Storage destination existence when applicable, loose unit before engine before part, with exact list-first semantics and existing null/error behavior. The engine projection explicitly refuses other recognized asset families; it does not reinterpret a colliding ID. A missing ID still reaches the original missing-owned-item error. Engine destination must be Storage or Car; its existing error occurs before detaching.

For target Car, same `parentVehicleId == targetId` returns immediately after preamble/classification. Do not look up or validate the target car, run availability checks, detach/reproject or require valid nested engine data. A missing target car and null engine slots/tune/fluids can therefore coexist with this legacy no-op; preserve raw absence, not new defaults. Otherwise detach the source parent first, then attach the requested engine. For Storage, detach first when attached, then assign the requested engine's storageId. Storage assignment occurs only after successful detach.

Detach uses the first car found by source ID through the original MutableCar guard order (car existence, garage job before active rebuild, then unsettled run). It looks up the engine referenced by that car's engineAssemblyId, which can differ from the requested asset on a malformed graph. Replace engine.slots with the ordered engine-category subset of car bindings; remove those bindings from the car; transfer the car's fluid reference into the engine; assign car.tune.Copy() into engine.tune; assign a new empty FluidState to the bare chassis; clear car.engineAssemblyId and engine.parentVehicleId; project source-car ranks. This explicit empty-chassis transition is preserved domain behavior, not permission to normalize missing resources elsewhere.

Attach preserves exact sequence: MutableCar(target), Stored(requested engine), shared physical mount fit (exact model equality), occupied-engine or engine-slot guard, reciprocal IDs, append engine bindings in order, replace engine slots with an empty list, transfer engine fluid reference into the car and null engine.fluids, apply exactly EngineAdjustments, then project target-car ranks. A null source engine tune makes EngineAdjustments a no-op; do not invent a tune. Extract a common scalar fit helper if needed so public Fits and the new core retain one authority.

Do not rewrite descendant PartInstance location/owner/slot/condition/IDs: the original assembly transfer does not do so. Preserve unrelated chassis bindings, storage tags, null values, existing engine slot replacement, tune overlay fields and late ProjectRanks failures. Public Detach/Attach signatures stay compatible. The private adapter supplies facts and owned writes, never separate mutation/fit/availability rules.

## Memory and result contract

Reuse the synchronous lazy scoped adapter. Car engine-ID/fluids and engine parent/storage/bindings/tune/fluids become live-backed or private-backed properties with exact setters. Copy by source reference, preserving distinct equal-ID records and first-match order. Private copy context preserves relevant shared FluidState/Tune references; setters transfer already-owned values without copying them again. Detach's explicit tune.Copy remains a new distinct tune as before. Do not eagerly interpret nested slots/resources ahead of guards. Discard caches and all borrowed profile/catalogue/source references in finally. No JSON, whole-profile/catalogue clone, Ensure, ID/RNG allocation, clocks or Unity APIs in the new projection. Catalogue remains an explicit borrowed owning-thread argument; this is not a detached worker input or freshness ticket.

`WholeEngineMoveProjection.Evaluate(profile, assetId, destination, destinationId, catalog)` returns Success/ErrorType/ErrorMessage, MovedEngine, ChangedEngines and CarOwners. On failure, expose only exact exception type/message; no partial candidate graph. The legacy live route retains its exact partial detach/attach effects. On success, export owned value snapshots of the requested engine and every engine actually written (including a different car-referenced engine on malformed input), deduplicated by source reference in mutation order. Affected car snapshots follow source then destination, deduplicated by source reference. No-op has no changed owners, while its raw moved-engine snapshot preserves nulls. Snapshot copies are value outputs, not a reconstructed PlayerProfile graph or proof of worker readiness.

Engine raw snapshots include instance/model/custom-name/storage/parent IDs, exact ordered nullable binding arrays, tune and fluid values/nulls. Optional descendant/build materialization has its own availability/error status and cannot change MoveSuccess. Attached engines' empty stored-slot list is not a complete attached-car build: use the existing car snapshot/H path only for actually affected cars, without an extra no-op target read. Car query errors likewise remain optional and do not turn a successful transfer into failure. Preserve source identity/values and isolate returned mutable data from source and later evaluations.

## Frozen reference and finite verification

Before extraction, root freezes `LegacyWholeEngineMoveReference` (Editor-only), SHA256 `B4704709F463E06453872AEAE0E1B3B1BC00034D7AAE846579708D85A268F012`. Its raw Move prefix/engine branch and current Detach/Attach/Fits/EngineAdjustments are literal bodies with redirected names only. It calls frozen I MutableCar/Stored/ProjectRanks/definitions and never the new engine core. Catalogue, EngineSlot, Tune.Copy and FluidState are shared unchanged primitives. Part/unit branches are not claimed as a new projection family; preserve the existing frozen v1 cart trace.

Use disposable actual authored fixtures, with frozen setup transitions/expectations and clearly labelled literal test-only clones. No production profile mirror. Keep malformed variants unnormalized. The finite matrix is:

| Group | Actual controls |
| --- | --- |
| Valid transfers | Garage-to-Storage; stored engine to its bare compatible car; attached engine to Storage; attached engine A to actual same-model bare car B |
| No-op | Same-parent car with unavailable context; same-parent missing target car plus null engine slots/tune/fluids, proving no target query/defaults |
| Ordered guards | Unsettled run before unknown Storage/missing asset; unknown Storage before missing asset; missing asset; invalid engine destination before detach; missing parent car; car-referenced engine missing |
| Later target failures | Occupied target; actual incompatible model; target garage job and active rebuild variants after source detach; pin exact live partial state and private error-only/source-preserved result |
| Transfer/null facts | Nondefault real fluid/tune/history and unrelated chassis bindings; pre-existing stored-slot oddities overwritten by detach; null engine fluids/tune into a valid bare target, preserving success and optional query error |
| Malformed ordering | Parent/car-engine disagreement (car-selected different engine); invalid slot parse before detach writes; null car tune after binding/fluid writes; one late source rank failure after field transfers |
| Null source slot list on Attach | A stored engine with slots=null into an otherwise valid bare car: reciprocal IDs are assigned before the original List.AddRange(null) throws; engine slots, fluid/tune transfers and rank projection have not happened. Compare the exact live partial graph/error and private error-only/source preservation; do not normalize null to an empty list |

This is a finite matrix, not every malformed cross-product. Document any unavailable actual fixture as OPEN/failing precondition; do not fabricate OEM parts, heal state or use passing skips. Compare the current public live operation and private projection to independent frozen outcomes: exact type/message and full live partial candidate state; raw requested/changed engines, car bindings/order and rank/tune/fluid values; descendants' exact physical IDs/data unchanged; source values/reference topology and catalogue/RNG/entropy/JSON stability; output mutation/fresh-evaluation isolation. Compare owned Part.Copy representation explicitly, with full member subtype/data checks. Enumerate readonly snapshot fields and dictionary key/value facts explicitly rather than assuming Unity serialization reflection observes them.

Keep raw output bounded (first typed-field difference, not full profiles/catalogue). Final focused verification, existing I whole-part and H/Parts regressions, Windows build, unchanged source manifests and Luna-high review precede commit. No transition timing/native allocation claim without measurements; full native50ms, device, cart/ticket/caller, JSON-retirement and S07–S14 gates remain open.
