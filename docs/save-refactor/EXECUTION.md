# Save/state refactor execution ledger

Authority: user request on 2026-10-23 authorizes the whole [plan](../SAVE_STATE_REFACTOR_PLAN.md), coordinated **gpt-6-luna / medium** implementers and **gpt-6-luna / high** independent checkpoint review before advancing. No worktree, push, unrelated UI/audio/physics changes or blind PR merge. Technical approval is not human visual/phone acceptance.

Baseline: local main `6d19aae`. Unrelated modified files are the pre-existing new-parts-supply, parts-market, race-audio-audition and sqlite-state reports; untracked attachments, Tests.ps1, race-exit-regression and shop-responsive-20261019 remain untouched.

Hard save-path requirement reinforced by the user: **no full-profile cloning to save**. Working sets and persistence use affected records only; no whole-profile JSON/binary/deep copy or full snapshot substitute. S00 measures the existing legacy cost and may copy isolated test fixtures. Every converted command must eliminate that production behavior, and final completion requires all live save callers migrated. Reviewers enforce this before approving each converted path.

Additional hard completion gate: **no JSON encode/decode for runtime data manipulation**. This includes command validation/quotes, domain/spec/part copies, equality/cache fingerprints, previews and markets, not only save calls. Use typed records, explicit copies and typed version/hash keys; normal state projection writes affected typed records. JSON may remain only for named legacy/settings/diagnostic/external file boundaries. S00 audits the remaining callsites; per-command reviews and final callsite allowlist prove removal.

## Next checkpoint

S05b final-source664 focused +66 migration +72 isolated storage checks pass, Unity exit0; independent Luna-high review is PASS in S05B_REVIEW.md. Complete its local commit, then begin S06 ordered owner admission/publication. Earlier committed checkpoints are listed below.

**S05b — final technical review is PASS; local commit precedes S06 activation work.** S05a is committed as `ff9102e` after66 focused +72 isolated storage checks and Luna-high PASS. S05a1 is committed as `ffb1081` after89+72 checks and Luna-high PASS. S05p is committed as `ad4ed4e` after independent Luna-high PASS. S04 is committed as `1fc63d0` after final Luna-high PASS. S03a is committed as `3dfcfa1` after independent Luna-high PASS. S02c is committed as `34abd7f` after Luna-high PASS. S02b is committed as `7e6ea10` after Luna-high PASS. S02a passed Luna-high review and is committed as `62b2bc0`. S01 passed review and is committed as `fd092bc`. S00 is `39d09b7`, whitespace closure `f163454`. No gameplay caller is converted to the dormant refill writer yet; S06 owns ordered admission and publication. Parent coordinates all Unity runs, real mouse tests, staging and commits. Interactive tests run sequentially; no two Unity Editors use this project concurrently.

## File ownership manifest

| S00 owner | Model / reasoning | Exclusive write paths | Task |
| --- | --- | --- | --- |
| state_medium | gpt-6-luna / medium | docs/save-refactor/S00_STATE_AUDIT.md | Live graph, commands, alias/index/copy audit |
| storage_medium | gpt-6-luna / medium | docs/save-refactor/S00_STORAGE_AUDIT.md | SQL/projection, durability, writer/recovery audit |
| baseline_medium | gpt-6-luna / medium | Runtime/SaveRefactorBaselineChecks.cs and .meta; docs/save-refactor/S00_TEST_MATRIX.md | Isolated bounded baseline harness and existing test inventory |
| coordinator | parent | Plan/ledger, S00_BASELINE.md, integration corrections explicitly reassigned, reports/save-refactor/** | Current branch/PR evidence, build/test execution, final integration and commit |
| checkpoint reviewer | gpt-6-luna / high | docs/save-refactor/S00_REVIEW.md when implementation agents release a slot | Independent source/evidence review; no production edits |

Runtime paths above are relative to Game/Assets/DragLab/Scripts/. No overlapping writes; requests to alter shared contracts return to parent. Each subsequent checkpoint gets its own manifest before dispatch. Agent completion is not checkpoint approval.

## Gates

| Checkpoint | Implementation/checks | Luna-high review | Local commit | Limits / next |
| --- | --- | --- | --- | --- |
| S00 | Final frozen 342 samples + independent validator and 9-fixture all-table rollback pass; see S00_RESULTS.md | PASS — gpt-6-luna / high, S00_REVIEW.md | `39d09b7`, whitespace closure `f163454` | Bounded warmed-Editor baseline only; no phone, allocation, tail-frame or whole-command-family claim |
| S01 | 75 contract checks + 72 existing isolated storage checks pass; dormant typed contracts/storage mapping | PASS — S01_REVIEW.md | `fd092bc` | No caller switch or new persistent authority |
| S02a | Prior full harness 1,375 focused + 72 existing storage checks; supplemental final-source preservation 1,067, all exit 0; sparse capture fixes first-car regression | PASS — S02_REVIEW.md | `62b2bc0` | Operation-local requested-ID map only; no persistent command conversion |
| S02b | Prior full harness 250 + 72 storage; final-source focused parity 220, all exit 0; 36-row cost comparison passes | PASS — S02B_REVIEW.md | `7e6ea10` | No persistent cache, no command/save conversion |
| S02c | 132 exact parity + 72 isolated storage checks; final-source 10 lifetime probes; 180 paired timing records | PASS — S02C_REVIEW.md | `34abd7f` | Existing error order/global invariants retained; 5-car/no-spares separate median +8.7%, paired median -0.1103 ms |
| S03a | 95 typed parity/lifetime/dependency/bounds checks + 72 isolated storage pass; 20 capture-inclusive timing pairs | PASS — S03A_REVIEW.md | `3dfcfa1` | One cache only; no whole-profile working set or persistence switch |
| S04 | Final-source 164 focused + 72 isolated storage, exit 0; descriptive 20-sample cost | PASS — S04_FINAL_REVIEW.md | `1fc63d0` | Dormant until typed storage/writer pilot passes; no production caller switch |
| S05p | Concrete typed schema/authority/codec/receipt and bounded-query proposal; two native in-memory probes and static checks pass | PASS — S05P_REVIEW.md after capture/null/placeholder corrections | `ad4ed4e` | Proposal only, no production migration/writer; managed Windows/Android, parity and faults remain implementation gates |
| S05a1 | 89 codec/native binding + 72 isolated storage checks pass, exit 0; source/meta boundaries pass | PASS — S05A1_REVIEW.md | `ffb1081` | Dormant typed scalar/fluid row only; no migration, writer or live save switch |
| S05a | 66 typed migration +72 isolated storage checks pass, exit0; original-version recovery/strict load boundaries covered | PASS — S05A_REVIEW.md | `ff9102e` | Typed economy/fluid authority; transitional legacy projection remains explicit debt |
| S05b | Final-source664 direct refill +66 migration +72 isolated storage checks pass, exit0 | PASS — S05B_REVIEW.md | This checkpoint commit | Dormant scoped writer; S06 owns selected binding, ordered admission and publication |
| S06–S14 | Not implemented | Not reviewed | None | Execute in order after prior gate passes; remaining cache/JSON consumers stay explicit conversion work |

Every accepted row records exact checks, measurements, failures/corrections and remaining costs. All named command suffixes have separate commits/reviews. Do not mark an entire family complete from one converted pilot.

S00 integration ownership transfer: after baseline_medium released its files, parent corrected compiler errors, diagnostics/fixture loading, allocation probe and rollback verification. tools/check_save_refactor_baseline.py and tools/fixtures/save-refactor-s00/** are parent-owned static evidence. No overlapping agent writes.

## S01 file ownership

Baseline `f163454`. Contracts are pure managed metadata for affected records, not a second state ledger. State medium freezes and communicates the API before dependent code is written. No real storage adapter is authorized until the coordinator reviews that mapping; no production caller changes.

| Owner | Exclusive paths | Outcome |
| --- | --- | --- |
| state_medium / Luna medium | Domain/StateCommandContracts.cs + .meta; docs/save-refactor/S01_CONTRACTS.md | Immutable identity/version/read-write/receipt contracts and invariants |
| storage_medium2 / Luna medium | docs/save-refactor/S01_STORAGE_MAPPING.md; proposed Infrastructure/StateCommandStorageBoundary.cs + .meta only after coordinator approval | Map existing fences and explicit missing durability requirements |
| baseline_medium / Luna medium | Runtime/StateCommandContractChecks.cs + .meta; docs/save-refactor/S01_TEST_MATRIX.md | Adversarial contract/failure checks without save writes |
| coordinator | Ledger, status headers, reports/save-refactor/S01_RESULTS.md and raw execution artifacts | Integration, static/Unity checks, commit |
| checkpoint_high / Luna high | docs/save-refactor/S01_REVIEW.md, after a slot is released | Independent review before S02 |

S01 preserves current production persistence. The existing runtime JSON dependencies and whole-profile clones remain migration work, not accepted final behavior. This checkpoint establishes the contracts that later command conversions must satisfy.

S01 integration ownership transfer: the implementation agents released all assigned files; parent corrected test preprocessor scope coordination, boundary tests/docs, and added Editor/StateCommandCheckpointVerification.cs + .meta for the existing isolated storage regression. Combined run exited 0 (75 + 72 checks). No packaged build/UI change at this dormant checkpoint.

## S02a file ownership

Baseline `fd092bc`. First index family only: owned part ID to position. Build within a synchronous validation invocation, return validated part handles, dispose index before mutation/publication. No callbacks or structural identity changes within index lifetime, no global cache or mutation-version assumption. Main owns live graph; existing isolated worker candidates may run the same pure-managed helper privately. No worker receives the live graph.

| Owner | Exclusive paths | Outcome |
| --- | --- | --- |
| state_medium / Luna medium | Domain/OwnedPartIdentityIndex.cs + .meta; docs/save-refactor/S02_IDENTITY_INDEX.md | One-shot ordinal ID map, rejection/lifetime contract |
| baseline_medium / Luna medium | Runtime/OwnedPartIndexChecks.cs + .meta; docs/save-refactor/S02_TEST_MATRIX.md | Independent scans, mutation/lifetime and real checkpoint differential tests, build/lookup measurements |
| storage_medium2 / Luna medium | docs/save-refactor/S02_STORAGE_IMPACT.md | Read-only checkpoint ownership, failure and persistence audit |
| coordinator | Infrastructure/MechanicsTransactions.cs, optional Editor test wrapper; ledger/status/results | Integrate one index into checkpoint validation/apply and run gates |
| checkpoint_high / Luna high | docs/save-refactor/S02_REVIEW.md after a slot is released | Independent correctness/performance review before next family |

S02a integration ownership transfer: implementers released files; parent owns final integration and doc corrections. Initial full-map run (1,358 + 72 pass) is retained locally but its common-case regression was rejected. Corrected sparse run passes 1,375 + 72, exit 0; independent 16-row CSV validation passes. All affected values/receipt stage before assignment. No later family has started.

## S02b file ownership

Baseline `62b2bc0`. Continue one installed-relation lookup family, reusing S02a's sparse ID map. Capture ordered bindings/parts once per synchronous operation, supply definitions and exact typed part copies without repeated scans. This is not a new state owner, mutation observer or worker-safe snapshot. Remaining JSON-based preview fingerprints and profile-copy costs are later conversion work, not silently accepted completion; several spec/tune copies are already typed.

| Owner | Exclusive paths | Outcome |
| --- | --- | --- |
| state_medium / Luna medium | Domain/InstalledPartLookup.cs + .meta; docs/save-refactor/S02B_INSTALLED_LOOKUP.md | Frozen narrow installed binding/part view and candidate-local lifetime |
| baseline_medium / Luna medium | Runtime/InstalledPartLookupChecks.cs + .meta; docs/save-refactor/S02B_TEST_MATRIX.md | Independent old-scan parity, typed resource/member identity preservation, build-inclusive scaling |
| storage_medium2 / Luna medium | docs/save-refactor/S02B_OWNERSHIP_AUDIT.md | Read-only order/ownership/alias/persistence impact review |
| coordinator | Domain/MechanicsOwnership.cs and Domain/CarInspection.cs; optional test wrapper; ledger/status/results | Integrate shared capture, run tests, stage explicit files and commit |
| checkpoint_high / Luna high | docs/save-refactor/S02B_REVIEW.md after a slot is released | Independent checkpoint approval before advancing |

All agents must freeze/share the API before dependent implementation; no overlapping edits. No S03 or later step is active.

S02b integration ownership transfer: all implementers released files; parent integrated Definitions/Resolve/Inspection, corrected CS0136/CS0104 compile errors before runtime, aligned timed legacy baseline with production List.Find and added a final distinct front/rear tire-definition fixture. Prior full250+72 and final-source focused220 pass, exit0; no production/timing changes between runs. Independent CSV validation passes36rows/15columns, all3models/4sizes/3placements; no helper median regression observed.

## S02c file ownership

Baseline `7e6ea10`. One ownership-validation read-index family: cars/engines/parts by actual ID and installed part to containing car. Parent holds Validate edits until the verification agent freezes the exact legacy core. Keep ordered duplicate/orphan/member/fault checks and dealer-labelled error recovery behavior; no index prevalidation may silently change them. This is synchronous pure validation, not a state-owner rewrite or new persistent ledger.

| Owner | Exclusive paths | Outcome |
| --- | --- | --- |
| state_medium / Luna medium | Domain/OwnershipValidationLookup.cs + .meta; docs/save-refactor/S02C_VALIDATION_LOOKUP.md | Frozen disposable ID/relation lookup retaining legacy first-match semantics |
| baseline_medium / Luna medium | Runtime/OwnershipValidationLookupChecks.cs + .meta; Runtime/OwnershipValidationLegacyReference.cs + .meta; docs/save-refactor/S02C_TEST_MATRIX.md | Frozen original core, exact rejection/read-only differential cases, whole-validator measurements |
| storage_medium2 / Luna medium | docs/save-refactor/S02C_VALIDATION_AUDIT.md | Validation helper side effects/error ordering and persistence impact review |
| coordinator | Domain/MechanicsOwnership.cs Validate integration; test wrapper; ledger/status/results | Maintain validation order and authority, run checks, commit explicit paths |
| checkpoint_high / Luna high | docs/save-refactor/S02C_REVIEW.md after a slot is released | Independent review before the next increment |

No S03 or later step is active. Remaining profile clone/JSON manipulation gates stay open.

S02c integration ownership transfer: all implementers released files; parent integrated only Validate lookups after the unchanged early gates, independently checked the frozen oracle/GUIDs, and ran the full and supplemental gates. Initial five-sample timing method was not accepted; corrected alternating 20-pair data retains every ordinary GC event. No production algorithm changed after the corrected full run. Supplemental final-source tests and median summary arithmetic were checked separately (10 pass, exit 0). No S03 or persistent writer conversion has started.

## S03a file ownership

Baseline `34abd7f`. One cache: `NativeSteadyOutputCache`, currently keyed by JSON representations and evaluating a borrowed `InductionProfile` later. Replace only its key/evaluator capture with typed, private, dependency-specific inputs; retain exact authoritative `InductionDynamics.SteadyOutput` equations. Cash/name/history/tires do not invalidate unconditioned engine output. Changes to actual engine/curve/cam/compression/fueling/capacity/induction inputs do. Cache entry/point counts are bounded, stale source edits cannot poison an old key. Existing car-signature/part-entry JSON and profile-plan caches are separate open later work.

| Owner | Exclusive paths | Task |
| --- | --- | --- |
| state_medium / Luna medium | Domain/SteadyOutputInputs.cs + .meta; Domain/InductionDynamics.cs capture-only addition; docs/save-refactor/S03A_STEADY_INPUTS.md | Immutable typed key and private narrow steady snapshot preserving computed profile/factory values |
| baseline_medium / Luna medium | Runtime/SteadyOutputCacheChecks.cs + .meta; docs/save-refactor/S03A_TEST_MATRIX.md | Independent direct physics equivalence, source edits, dependencies, bounded cache, timing; no JSON test oracle |
| coordinator | Runtime/NativePartPreviewCache.cs steady-cache section only; test wrapper, status/ledger/results | Cache integration, execution, commits |
| checkpoint_high / Luna high | docs/save-refactor/S03A_REVIEW.md after implementation slot releases | Independent exact-dependency/lifetime/performance gate |

No command/save-path conversion is claimed by S03a. Main/owner captures inputs; only the private evaluator is used on workers. No Unity objects, live profile, whole-profile clone or runtime reflection serializer enters this cache.

S03a integration transfer: implementation agents released all assigned files; coordinator integrated only NativeSteadyOutputCache and the isolated wrapper, corrected final before-first-point test coverage, verified GUID/UTF-8/no-JSON boundaries and ran the combined Editor checkpoint (95 + 72, exit 0). Cold-fill and retained-delegate limits remain explicit in S03A_RESULTS.md. No S04 or save-path conversion started.

## S04 file ownership

Baseline `3dfcfa1`. One action: ordinary oil/coolant top-up (`PaymentKind.Fluids`), excluding fuel, oil change/filter, flush, parts and rebuilds. Capture only the affected car's read dependencies, capacities, policy, exact fluid history, cash/clock/receipt and command fences. Plan privately without Ensure, PlayerProfile clone, JSON, live mutation or full CarSimulation. Compare the existing quantity/rounding/top-up behavior and resource/history/identity outcomes before switching any caller. No generic per-record version authority is fabricated where none exists; fences/observations and missing writer requirements must be explicit.

| Owner | Exclusive paths | Task |
| --- | --- | --- |
| state_medium / Luna medium | Domain/FluidRefillPlanner.cs + .meta; docs/save-refactor/S04_FLUID_PLANNER.md | Propose/freeze typed request/input/result/read-set API, narrow capture/planning and stale-dependency checks |
| baseline_medium / Luna medium | Runtime/FluidRefillPlannerChecks.cs + .meta; docs/save-refactor/S04_TEST_MATRIX.md | Independent deterministic/golden/randomized legacy shadow, rejection/source preservation and bounded-cost evidence |
| coordinator | Optional Editor wrapper, status/ledger, results and any approved existing entry-point adapter | Coordinate contract decisions, run Unity/checks, commit; no gameplay caller switch in S04 |
| persistence audit owner when assigned / Luna medium | docs/save-refactor/S04_STORAGE_IMPACT.md | Read-only affected-row/receipt/clock/fence map and future S05 integration needs; no writer/schema changes |
| checkpoint_high / Luna high | docs/save-refactor/S04_REVIEW.md after implementation slot release | Independent exact behavior/working-set/no-clone/no-JSON review |

The pilot may retain a synchronous full-validator preflight during its dormant shadow stage if documented; this is not the eventual UI-thread cost target. Prefer targeted read-set validation in the planner itself and explicitly distinguish already-valid ownership prerequisites from corrupted fixture checks. Dealer-recovery normalization and old quote snapshots must never be silently invoked by the new path. Existing callers and persistence stay unchanged until later gates.

S04 integration transfer: all agents released assigned files; parent added the isolated wrapper and final valid-capacity/engine-identity stale-read probes, then ran the full157+72 and supplemental6 separately, exit0. Targeted hash equality, rejection after-images and actual confirmation timing were corrected before the full run. No new gameplay/writer call is present. S05 needs a separate reviewed typed schema/integrity proposal before implementation; its existing JSON/full-row bridge cannot serve this command.

S04 final-source rerun: 164 focused + 72 storage passed, exit0, after a receipt-fixture clock correction and added full-validator assertion. No production/timed algorithm changed. Earlier 157+72 and supplemental6 remain historical, not final count additions. Prior Luna-high PASS predates the correction; final evidence recheck is pending because that reviewer hit a usage limit. No commit/next checkpoint until the requested Luna-high gate is satisfied.

S04 final-review gate resolved: the first reviewer exhausted its usage allowance during recheck. A fresh independent gpt-6-luna/high reviewer inspected the complete final source/raw164+72 results and approved S04_FINAL_REVIEW.md. Same requested model/reasoning; no substitution or bypass. Local commit is now the remaining S04 gate.

## S05p file ownership — explicit prerequisite added from S04 evidence

Baseline `1fc63d0`. The existing schema cannot implement the pilot without JSON car/receipt manipulation and all-row hash/diff work. Review a minimal typed schema, incremental integrity, migration/compatibility, durable intent receipt, owner fence and API contract **before** changing any production migration/writer/loader. This is a prerequisite within S05, not advancement of S11's other hot record families. No authored-content database conversion or profile-format replacement is authorized here.

| Owner | Exclusive paths | Task |
| --- | --- | --- |
| storage_pilot_medium / Luna medium | docs/save-refactor/S05P_TYPED_STORAGE_PROPOSAL.md | Concrete canonical typed tables/columns and constraints, exact affected transaction/query plan, incremental row integrity, immutable v3 migration/backup and v2 bridge retirement plan |
| s05_domain_medium / Luna medium | docs/save-refactor/S05P_DOMAIN_BOUNDARY.md | Map exact pilot input/result/receipt and owner/reset/slot/read-set transport; history/field/null/identity authority and publication/reconciliation contracts |
| s05_tests_medium / Luna medium | docs/save-refactor/S05P_TEST_MATRIX.md | Independent migration/durability/rollback/digest/cache/checkpoint/reset/idempotency/failure matrix with actual fixture paths and exact verification entry points |
| coordinator | docs/save-refactor/S05P_DECISIONS.md if needed; plan/ledger/status/results; isolated static proposal checks only | Resolve proposals against source, validate documented DDL/query assumptions, stage/commit reviewed proposal |
| fresh independent reviewer / Luna high | docs/save-refactor/S05P_REVIEW.md after author slot release | Review schema/authority/migration/compatibility/integrity/failure plan before S05 implementation |

Use individual typed records, not a binary/JSON whole-profile replacement. Preserve existing migration checksums/scripts and recoverable save floors. Current digest is XOR of per-row SHA-256, not a monolithic SHA; persist/verify enough row integrity to update only declared records without rescanning unrelated state. Typed pilot reads/writes and receipt reconciliation must avoid JSON, full row inventory and global relation sync. Every temporary legacy bridge must name its callers, canonical field authority and retirement checkpoint under the final no-runtime-JSON gate.

S05p integration transfer: all medium authors released files. Parent pinned S05P_DECISIONS, corrected the full transitional capture handoff, nullable receipt children and attached-engine legacy placeholder semantics, then reran the native probes. Fresh Luna-high re-review PASS retained its initial blocked findings as resolved history. No production migration, writer or UI caller changed. Existing user reports/attachments remain untouched.

## S05a1 file ownership — typed codec foundation before migration activation

Baseline `ad4ed4e`. Implement the approved exact scalar/key/record hash codec and one immutable fluid row transport, with no production callsite or schema-version change. This is a testable infrastructure step within S05a, not a migration/writer completion. Existing 001/002 migrations, profile save behavior and UI stay unchanged. Parent owns Unity/Git; agents do not run either.

| Owner | Exclusive paths | Task |
| --- | --- | --- |
| storage_pilot_medium / Luna medium | Infrastructure/TypedStateRecord.cs + .meta; Infrastructure/TypedFluidStateRecord.cs + .meta; docs/save-refactor/S05A1_TYPED_CODEC.md | Explicit immutable typed scalar fields, canonical key/hash bytes and exact 14-bit-field fluid row, no JSON/reflection/profile copy |
| s05_tests_medium / Luna medium | Runtime/TypedStateRecordChecks.cs + .meta; docs/save-refactor/S05A1_TEST_MATRIX.md | Independent canonical vectors/alias/culture/invalid input/native managed SQLite integer binding checks, isolated output only |
| coordinator | Editor/StateCommandCheckpointVerification.cs wrapper; status/ledger/results/vector fixture if required | Integrate fixed API and independently derive golden bytes, run focused + existing isolated storage, local commit |
| fresh Luna-high reviewer | docs/save-refactor/S05A1_REVIEW.md after implementer release | Review exact codecs/immutability/native gates and dormant scope |

No generalized serializer, whole-profile transport or generic JSON fallback. Transport owns only one affected row. Native tests use disposable in-memory DBs; managed Windows evidence does not replace Android verification before a migration-containing Android release. Raw doubles, null/empty strings, schema field order and composite-key framing must match the pinned S05p codec. New fields/tables are not activated by this checkpoint.

S05a1 integration transfer: both medium owners released source/docs; parent fixed the isolated wrapper, independently derived the canonical golden vector, checked GUIDs and no-JSON/profile/reflection dependencies, and ran final-source89+72 in Unity6000.6.4f1 (exit0). A fresh Luna-high review passes actual source and dedicated raw evidence. No production save/migration/caller changed. Local commit remains before next implementation step.

## S05a file ownership — activate typed pilot storage safely

Baseline `ffb1081`. Implement approved migration003, typed economy/fluid/receipt authority, persisted record integrity, typed load overlay and strict-CAS transitional legacy write projection. This changes storage, not the gameplay refill caller; direct command writer and UI queue remain S05b/S06. Do not substitute another dormant-only helper checkpoint for this deliverable. Existing001/002 checksums stay immutable. Preserve exact resources/identities/history/seeds/reservations and recoverable reset/backup behavior.

| Owner | Exclusive paths | Task |
| --- | --- | --- |
| s05a_storage / Luna medium | Infrastructure/SqliteStateStore.cs; new Infrastructure/SqliteTypedState.cs + .meta and other typed backend helpers agreed with coordinator; Resources/Persistence/Migrations/003-typed-economy-fluids-integrity.sql.txt + .meta; docs/save-refactor/S05A_STORAGE.md | Canonical typed rows, exact migration/backfill and field removal, full-load integrity, transitional typed projection and fenced transaction/reset/backup behavior; freeze API first |
| s05a_owner / Luna medium | Infrastructure/ProfileStore.cs and ProfileStore.Pending.cs; new narrow legacy decode helper + .meta only after source audit/coordinator decision; docs/save-refactor/S05A_OWNER.md | Owner-side complete capture before worker, typed load envelope overlay before validation, narrow existing Unity placeholder handling without healing, exact unchanged publication/reset ordering |
| s05a_tests / Luna medium | Runtime/TypedSaveMigrationChecks.cs + .meta; Infrastructure/SqliteStateVerification.cs; docs/save-refactor/S05A_TEST_MATRIX.md | Real v1/v2 fixture construction from immutable scripts, migration/fault/source preservation, exact typed bit/history/null parity, mixed legacy save/CAS/reset/recovery/tamper coverage; update old schema expectations meaningfully |
| coordinator | Editor wrapper; StateRows.cs only if required field-removal helper integration; status/ledger/results | Freeze API and decode policy, run native Unity checks sequentially, source/evidence integration, local commit |
| fresh Luna-high reviewer | docs/save-refactor/S05A_REVIEW.md after implementer release | Independent final source/migration/caller/fault/evidence checkpoint gate |

The transitional bridge must capture DTOs directly from the owner-owned candidate and omit moved fields from legacy rows; it is existing debt removed at S13. New direct refill path must never use it. JSON decode remains only this unconverted legacy load/migration boundary; typed overlay is direct assignment. No new complete profile clone. Full source validation and exact-old-digest checks precede migration; failures roll back v2 rather than create defaults. Legacy inline-null placeholder handling must be explicit and source-audited, never a broad Ensure that creates/heals components. Parent owns Unity/Git. No parallel Editor use or writes to existing unrelated reports.

S05a integration transfer: medium owners released storage/owner/tests, parent corrected fixture oracles against real Unity serialization and existing codec contracts, then ran final-source66 focused +72 isolated storage checks (Unity6000.6.4f1, exit0). Fresh Luna-high final review is PASS in S05A_REVIEW.md. Raw failure/correction history and actual limits are in reports/save-refactor/S05A_RESULTS.md. Original-version backup sidecars, zero-byte first-migration retry, strict source rejection, exact typed values and all migration fault gates are covered. Existing unrelated reports/attachments remain unchanged. The transitional owner capture still costs113.24 ms in one diagnostic fixture, and its worker185.06 ms: no responsiveness or final no-JSON/no-clone claim. Complete the local S05a commit before S05b direct affected-row writer; no UI queue is active yet.

## S05b file ownership — direct affected-record refill writer

Baseline `ff9102e`; S05a is locally committed after66+72 checks and Luna-high PASS. Implement S05P's direct refill transaction and durable reconciliation, dormant until the separate S06 admission/UI queue. Binding and result-scope refinements are in S05B_DECISIONS.md. No new migration or per-record fake versions.

| Owner | Exclusive paths | Task |
| --- | --- | --- |
| s05b_domain / Luna medium | new Infrastructure/TypedFluidRefillCommand.cs +meta; docs/save-refactor/S05B_DOMAIN.md | Immutable owner binding, exact stable intent, S04-derived scoped command and actual durable result/outcome types |
| s05b_storage / Luna medium | new Infrastructure/SqliteStateStore.FluidRefill.cs +meta; docs/save-refactor/S05B_STORAGE.md | Existing-gate strict-CAS direct typed transaction, bounded history, affected-row integrity, exact durable replay/reconciliation, injected-failure/query evidence hooks |
| s05b_tests / Luna medium | new Runtime/TypedFluidRefillCommitChecks.cs +meta; docs/save-refactor/S05B_TEST_MATRIX.md | Independent actual SQLite mutation/fault/replay/eviction/reset/slot/checkpoint/legacy interop and scaling cases |
| coordinator | existing SqliteStateStore.cs partial declaration, SqliteTypedState.cs monotonic-history load guard; Editor wrapper; S05B_DECISIONS.md/status/ledger/results | Freeze shared API, resolve owner authority/durability gaps, run Unity/Git after release |
| fresh Luna-high reviewer after author release | docs/save-refactor/S05B_REVIEW.md | Independent exact final source and evidence gate before commit/advance |

Authors must coordinate property/API names after the frozen parent decision. Parent owns all Unity/process/Git operations. One scoped transaction may not hide full-profile JSON, all-row hashing/relationship scans or synchronous whole-state backups; unrelated files and existing reports remain untouched. A committed result must remain reconstructible from actual durable columns after bounded history/car removal, without inventing old untouched state. S06 owns automatic actual-slot/path binding capture and serialized live-state publication.
