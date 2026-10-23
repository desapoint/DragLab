# Save/state refactor execution ledger

Authority: user request on 2026-10-23 authorizes the whole [plan](../SAVE_STATE_REFACTOR_PLAN.md), coordinated **gpt-6-luna / medium** implementers and **gpt-6-luna / high** independent checkpoint review before advancing. No worktree, push, unrelated UI/audio/physics changes or blind PR merge. Technical approval is not human visual/phone acceptance.

Baseline: local main `6d19aae`. Unrelated modified files are the pre-existing new-parts-supply, parts-market, race-audio-audition and sqlite-state reports; untracked attachments, Tests.ps1, race-exit-regression and shop-responsive-20261019 remain untouched.

Hard save-path requirement reinforced by the user: **no full-profile cloning to save**. Working sets and persistence use affected records only; no whole-profile JSON/binary/deep copy or full snapshot substitute. S00 measures the existing legacy cost and may copy isolated test fixtures. Every converted command must eliminate that production behavior, and final completion requires all live save callers migrated. Reviewers enforce this before approving each converted path.

Additional hard completion gate: **no JSON encode/decode for runtime data manipulation**. This includes command validation/quotes, domain/spec/part copies, equality/cache fingerprints, previews and markets, not only save calls. Use typed records, explicit copies and typed version/hash keys; normal state projection writes affected typed records. JSON may remain only for named legacy/settings/diagnostic/external file boundaries. S00 audits the remaining callsites; per-command reviews and final callsite allowlist prove removal.

## Next checkpoint

**S02a — Luna-high PASS; recording the local checkpoint commit before the next increment.** S01 passed review and is committed as `fd092bc`. S00 is `39d09b7`, whitespace closure `f163454`. S02a changes checkpoint lookup only; no persistent command conversion yet. Parent coordinates all Unity runs, real mouse tests, staging and commits. Interactive tests run sequentially; no two Unity Editors use this project concurrently.

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
| S02a | Prior full harness 1,375 focused + 72 existing storage checks; supplemental final-source preservation 1,067, all exit 0; sparse capture fixes first-car regression | PASS — S02_REVIEW.md | This checkpoint | Operation-local requested-ID map only; no persistent command conversion |
| S02b–S14 | Not started | Not reviewed | None | Execute in plan order after prior gate passes |

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
