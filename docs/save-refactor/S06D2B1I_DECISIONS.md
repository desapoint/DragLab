# S06d2b1i — shared whole-part transition prerequisite

Baseline `17c03a9` on main. This continues the approved complete S00–S14 refactor. Runtime manipulation must use typed state rather than JSON encode/decode or whole-profile copies. Earlier Step1/S06/native performance reds remain open; this is a dormant production seam, not cart/UI activation or a worker-safe detached planner.

## Ownership and execution

| Owner | Exclusive paths |
| --- | --- |
| Luna medium / s06_identity_fix_medium | New Domain/InstalledPartTransition.cs +meta and WholePartMoveProjection.cs +meta; existing SharedInventory.cs, MechanicsOwnership.cs, AssemblyOperations.cs, EngineElementService.cs, EngineElements.cs, ConcreteInstalledBuildInput.cs; I_CORE |
| Luna medium / s06_projection_contract_medium | New Runtime/WholePartMoveProjectionChecks.cs +meta; I_TESTS |
| Luna high / s06_identity_high | I_REVIEW only |
| Coordinator | Frozen Editor-only reference +meta, Editor runner, this contract/results/status, all processes/source freezes/builds/Git |

Code paths are relative to Game/Assets/DragLab/Scripts, notes to docs/save-refactor/S06D2B1I_*. No unlisted production edits without reassignment. Parent alone runs Unity/builds/players/Git after both authors release. No Game/tools changes during runs; verify all frozen hashes and actual process absence before thaw. Preserve unrelated files and metadata. Commit only after actual checks and independent high review.

## Shared rules and lifetime

Cover physical whole PartInstance moves across Storage, Car and detached Engine, including source removal, target displacement and target-only rank projection. Individual-unit and whole-engine-assembly moves remain separate families. Share the run/storage preamble and ordered classifier: loose unit first, engine second, part third, using existing list-first semantics. Keep legacy unit/engine branches unchanged; the new projection explicitly refuses those families instead of reinterpreting colliding IDs. Part destination validation stays after asset lookup and before removal.

One shared transition owns guard order, removal, install, displacement, rank projection and late member handling. Legacy adapters borrow live objects; a private synchronous lazy copy-on-read adapter copies only requested/touched facts. It must not prevalidate or eagerly interpret later owner state, construct a partial PlayerProfile, clone a catalogue/profile, or duplicate domain mutation rules. Preserve first car-by-binding lookup and ordered duplicates/null/dangling behavior. Copy identity must use source reference identity, avoiding collapse of distinct records with duplicate IDs. Shared car definitions retain InstalledPartLookup semantics; shared stored-engine definitions retain original ToDictionary semantics. All lookups see the current private overlay after staging changes.

The scope may borrow the profile and catalogue only during one synchronous owning-thread Evaluate call. Finally release all borrowed references. Success exports only owned touched part/owner/build snapshots; caller mutation cannot change the source or later evaluations. Failure exports only exact exception type/message, with no candidate graph. Legacy failure retains its historic partial mutations; the new scope discards private partial changes. This deliberately is not yet a detached CartPlanInput, worker dispatch or freshness certificate.

Preserve incoming/displaced storageId, exact identities/condition/history/member state, binding order, locations/owner/slot and tune behavior. Source removal does not reproject source car tune; target car install reprojects target only. No extra cylinder-block driveability policy is added to this domain Move seam. Catalogue remains unchanged and borrowed synchronously.

Success reports the actual written physical records through ChangedParts, including a different record removed by an inconsistent source binding. Preserve source-first/destination-second owner order across both owner kinds. Optional affected-build materialization has its own Materialized/BuildErrorType/BuildErrorMessage status; it must never rewrite a successful Move into failure because of an unrelated binding or missing display resource. Unavailable optional part/maps/inspection payloads remain null with an explicit error, never synthesized. A real ProjectRanks or late member failure inside the transition remains the Move's failure. Source malformed diagnostics are not normalized afterward.

At the original late engine-install Ensure stage, legacy still generates missing members. New projection refuses exactly when Count(spec,definition)>0 and members are null/empty, with InvalidOperationException("Typed part move projection cannot initialize missing engine members."). Keep count-zero empty state valid and nonempty correctly typed short/long arrays unchanged. Share wrong-type validation and preserve its exact original error. Earlier guard errors must win; no eager member check or synthesis, healing, IDs, JSON, clocks or RNG in the new seam.

## Frozen proof and finite checks

Coordinator freezes the 17c03a9 Move wrapper plus changed lower mutation/availability/Ensure bodies into Editor-only LegacyWholePartMoveReference before extraction. Only names/call targets change. Catalogue, InstalledPartLookup, and unchanged member topology/type/factory helpers remain shared; unit/whole-engine branches are explicitly outside this reference's proven family. Corrected frozen reference SHA256: `30A75892AA306E8A061454019DEC4AEA9A211569DF0F99B63F9CB197DC76F2BF`. Initial source-a hash `1ad3682c5a5c5e9f3c9f63f89a745469e4a2c04a486df1f248846567e03985b0` did not compile: the extraction accidentally renamed List.Remove as well as the unqualified domain method. Restore that single collection call to the literal original before any test execution; no domain rule or assertion changed. Keep the earlier frozen v1 cart trace untouched.

Use real normalized authored fixtures and disposable literal test clones only for test isolation. Exercise storage moves, occupied car/engine destinations and installed source moves; compare exact success graphs, affected car H/frozen resolution and stored-engine catalogue resolution. Pin unavailable destination after source removal with exact legacy partial graph and no new output/source mutation. Pin active-run/storage/missingasset/destination precedence and cross-family collision dispatch. Cover existing/empty/null/wrong-type/zero-required/nonempty short-long member cases and earlier guard precedence. Do not normalize deliberately invalid variants after corruption.

Assert source/RNG/entropy/JSON stability, owned output alias isolation and no retained profile/catalogue graph. Results must distinguish legacy generation from the new refusal policy, actual executed vectors from source review and Editor timings from native/device acceptance. No expanded commerce audit or passing placeholders for unavailable fixtures. Focused checks, relevant existing Parts/resolver regressions and Windows build follow the actual shared-core diff. Full planner, tickets/freshness, callers, owned refresh, remaining S06 gates and S07–S14 remain required.
