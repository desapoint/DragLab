# S06d2b1k — ordered inventory move working set

Baseline main `9879b92`. Previous goal turn is verified progress: J is committed with 473 focused checks, 214,713 existing regression assertions and a Windows build after independent Luna-high approval. Continue the complete S00–S14 goal. Runtime JSON manipulation and whole-profile save/preview copies or mirrors remain prohibited final-state dependencies.

## Deliverable and ownership

Complete the dormant ordered raw-domain Move family: whole parts, whole engine assemblies and loose units moving to Storage. One private working set must observe every earlier row. Existing live SharedInventory.Move and the sequence invoke one shared dispatcher and existing authoritative part/engine operations. No PartCart.Total, UI, checkout, persistence, worker or ticket activation; this is not a new cart validation/valuation authority.

| Owner | Exclusive paths |
| --- | --- |
| Luna medium / s06_identity_fix_medium | Existing Domain/InstalledPartTransition.cs and SharedInventory.cs; new Domain/InstalledInventoryTransition.cs +meta and OrderedInventoryMoveProjection.cs +meta; I/J snapshot helper visibility only as needed in WholePartMoveProjection.cs and WholeEngineMoveProjection.cs; K_CORE |
| Luna medium / j_tests_medium | New Runtime/OrderedInventoryMoveProjectionChecks.cs +meta; K_TESTS |
| Luna high / j_review_high | K_REVIEW only |
| Coordinator | Editor-only frozen loop/reference +meta, Editor runner, this contract/results/status, all source freezes/processes/checks/builds/Git |

Paths are relative to Game/Assets/DragLab/Scripts; notes are docs/save-refactor/S06D2B1K_*. No unlisted changes without reassignment. Root alone operates Unity/builds/players/Git after authors release. Complete manifest capture before launch; all Game/tools edits stay frozen until terminal process/source-hash verification. Preserve unrelated work, metadata, bench, simulation/audio, settings and UI. A tested/reviewed local commit precedes the next increment.

## Input, order and domain boundary

Add immutable InventoryMoveRequest with readonly AssetId, Destination and TargetId. OrderedInventoryMoveProjection.Evaluate(profile, IReadOnlyList<InventoryMoveRequest>, catalog) captures the container into its own array, retaining exact request order and null rows. Items contain only immutable strings/enum values and may be shared. A null container is a new API ArgumentNullException("requests") at FailedIndex=-1, before any domain operation. Empty input is an identity sequence with empty owned output arrays and no source/target reads. A null row produces its natural null-reference error while accessing request fields at that index; do not filter or heal it.

No request count cap, truncation, row deduplication, asset uniqueness, quantity, selected-car, pricing, block-preparation or cart eligibility rules are added here. Those remain the outer cart authority and later full planner. Exercise at least 100 requests. Repeated same-asset domain moves are valid for this API even though an eventual cart may reject duplicate assets. This distinction does not permit a UI fallback or partial activation.

Create exactly one private ProfileInstalledPartTransitionState for the complete invocation. Each row calls the existing preamble against that same state, then common InstalledInventoryTransition.MoveAfterPreamble. Keep run guard, Storage existence, unit-before-engine-before-part classification and exact first-match/null behavior. Unit branch remains Storage-only with the exact existing error; engine and part branches reuse J/I with ProjectionRequireExisting for new private part installs. Existing live SharedInventory.Move calls the same preamble/dispatcher with LegacyEnsure. No independent interpretation in the projection.

On a failure return only Success=false, ErrorType, ErrorMessage and zero-based FailedIndex. All output/write arrays are null; discard private effects from earlier successful rows too. The frozen/live candidate retains its original ordered partial effects. On success export final owned ChangedParts, ChangedEngines, ChangedUnits and first-affected ordered car snapshots, reference-deduplicated by actual source record identity. Track records written even when later moves return their values to the initial state; do not deduplicate or skip requests themselves. Optional materialization is final-only, memoized and separate from sequence success.

## Overlay correctness and memory

FirstCarOwningPart must scan cars in original source order, reading current private bindings only for already-loaded overlays and otherwise borrowing source bindings read-only. Do not wrap or clone unrelated cars/parts to scan. Preserve the original Any/Find null-row, null-installed and null-list exceptions; never turn these into null-safe matching. After a binding match, preserve I's first-car-by-ID relookup semantics and availability ordering. Earlier engine detach/attach and part displacement must change what later lookups see.

Use source-reference-keyed loose-unit wrappers, preserving definition/storage/price/tire flag and every concrete EngineElement/TireCondition field, subtype and ID. Lazy element copies use the existing polymorphism-preserving Copy; unit moves write only storageId. No unit installation, normalization, aggregate tire sync, defaults or ID creation. Existing part/engine/tune/fluid reference maps retain J behavior.

Stored-engine part removal/install must mark the actual engine record changed when its binding list is written, so final engine deltas include I part edits as well as whole-engine transfers. An attached car's binding edit does not invent a write to its engine's empty stored-slot list. Aggregate affected/changed records in first-write order; optional final views must reflect all rows, not cached intermediate snapshots.

Borrow profile/catalogue only during one synchronous owning-thread evaluation and release all sources/maps in finally. No mutable profile/catalogue or scope/delegate/source-record reference escapes; outputs own mutable values. No JSON, whole-profile/catalogue mirror, Ensure, clocks, RNG, new physical IDs, SQL or Unity APIs in the new projection. This remains distinct from detached immutable fact capture, freshness tickets and worker safety.

## Independent reference and finite gates

Before edits, root freezes the Editor-only driver LegacyOrderedInventoryMoveReference (SHA256 B8E959B3AB0CD5B13893DAAC22A1C4DCB84D919D5B36E944D3645C84D4F3535A) over the same immutable request representation, invoking the existing independent LegacyWholeEngineMoveReference.Move for every request. That frozen Move already contains all three original branches: raw loose-unit storage, whole parts via frozen I guards/mutators, and engines via frozen J operations. It never calls the new dispatcher/state lookup. Pin J reference SHA256 B4704709F463E06453872AEAE0E1B3B1BC00034D7AAE846579708D85A268F012 and I reference 30A75892AA306E8A061454019DEC4AEA9A211569DF0F99B63F9CB197DC76F2BF; retain the frozen v1 trace. Unchanged catalogue/lookup/copy primitives stay documented. No separate pre-extraction Unity run is required; actual frozen/live/private differential follows extraction.

Finite actual fixtures: a mixed engine A→bare B then its piston→Storage then stored engine C replacement/displacement then displaced part→Storage, interleaving a real loose unit; reverse order changing the outcome; late target failure after a prior successful move; full 100 repeated moves on one actual unit; full 100 distinct owned physical-part moves using enough real purchased cars; scalar/null request and preamble controls; empty input and one bounded family-collision control. Use an actual acquired tire unit where available to prove concrete subtype/wear/history preservation. Never fabricate healthy members or replace 100 distinct assets with repeated-item capacity evidence. Fixture absence is OPEN/failing precondition with observed counts, not a passing skip.

Setup and expected mutations use frozen operations and actual authored acquisition; disposable literal profile copies are test-only. Compare current live sequence and private final outputs to frozen full typed state, including exact order, identities, condition, fluids/tunes, unit fields, changed/affected record order and exact first errors/FailedIndex. Preserve source values/reference topology, catalogue and RNG/entropy/identity/JSON counters; test nested output mutation and fresh evaluation. Observe readonly fields and dictionary contents explicitly; bound logs to first typed difference.

Require focused differential, existing I/J/H/Parts regressions, Windows build, unchanged frozen source manifests and independent high approval before commit. Record a bounded capture/core/output-inclusive 100-distinct timing sample if feasible, excluding fixture/snapshot diagnostic work; allocations remain NA unless calibrated. Editor correctness/timing is not native frame/device acceptance. Complete cart composition/purchases/sales/holds, immutable inputs/freshness/callers, owned refresh, full Step1/S06 and S07–S14 remain open.
