# S06d — consumer latency and lifecycle gate

Baseline `2b6687c`: S06c manual refill functional activation is committed and independently reviewed. The entire S00–S14 goal, no-runtime-JSON and no-full-profile-save-clone requirements remain active. S07 is blocked by the remaining S06 gate; no worktree or push.

## Ordered work

1. Attribute the existing native frame cost and complete actual native lifecycle coverage without changing physics, transaction semantics or UI design. The existing opt-in frame CSV keeps its original columns and appends five consumer costs: owned cars, parts, cart, other menus and surface ticking. Use fixed row fields and bounded buffers; no new allocation or file I/O per measured frame. Record timer overhead and all setup, capture, recovery, navigation and completion samples. Parent has captured a deterministic pre-change native baseline using seed16102026 and the S06c Windows player.
2. Use measured consumer costs and the source audit to implement the smallest exact dependency/target-specific recalculation or immutable managed preparation needed to meet the gate. Do not move a whole expensive call into the following frame, lower fidelity, omit warnings, retain a stale result as current, alter global revision/commit fences, or relabel a miss as a pass. Freeze the view-signal and exclusive file contract after attribution, before those production edits.
3. Re-run matched seeded workloads, managed/storage regressions and actual native/F8 flows. Include final owner capture/publication and complete consumer-frame tails, plus lifecycle outcomes. Independently review and locally commit each coherent infrastructure/implementation step before advancing. All S06 acceptance requirements must pass before S07; Android/forced-termination whole-system soak remains additionally required in S14.

The explicit validated-load recovery boundary may still have its separately measured legacy cost; it cannot enter ordinary quote/admission/poll/publication. Its failure must retain the operation and fence. Broader unconverted consumer fingerprints/copies remain required S13 retirement work, not an allowance in the final game.

## Native lifecycle contract

Add one development-only coroutine with four isolated cases over the actual SQLite writer and selected-owner path: real commit/lost acknowledgement/reconcile with the same intent; definite pre-COMMIT rollback followed by a fresh operation; failed controlled recovery retaining its durable result/lease then successful explicit reload; actual `SuspendForMobile()` while a writer is held followed by normal owner polling on resume. Do not claim OS process death from a method call.

The delegating lost-ack executor may hide only the acknowledgement after a real successful commit; it must never fabricate committed state. Use the established pre-COMMIT fault phase for definite failure. For non-destructive failed recovery, the parent adds `ProfileStore.ControlledRecoveryFailureInjection` as an internal `Action` under Editor/development compilation only, invoked inside the existing controlled-load try/catch before `Load(false)`. Throw once to exercise the real retained-lease error path; no user or fixture database corruption is needed.

Workers receive only immutable typed commands, executor values and synchronization primitives. No live profile, UI or Unity object crosses that boundary. Native coroutine waits yield frames, have explicit deadlines and release held workers in `finally`; never wait synchronously on main or dispose a signal still used by a worker. Do not swap the test pilot away while an accepted operation remains retained. Test actual rendered child captions, resource bits, operation identity, receipt counts, durable versions and settings.

## File ownership for the current step

| Owner | Exclusive paths | Boundary |
| --- | --- | --- |
| s06d_views_medium / Luna medium | Runtime/FramePacingProbe.cs, NativeMenuShell.cs; S06D1_ATTRIBUTION.md | Instrumentation only; unchanged route/consumer behavior |
| s06d_lifecycle_medium / Luna medium | New Runtime/NativeTypedRefillLifecycleVerification.cs + meta; S06D_LIFECYCLE_AUDIT.md | Actual native verification; no production edits outside assigned file |
| Coordinator | ProfileStore.CommandOwner.cs development-only hook; NativeMenuVerification.cs flag; tools/Test-NativeMenu.ps1; Editor/StateCommandCheckpointVerification.cs explicit report routing; decisions/status/results; actual processes/Git | Parent alone runs Unity/player checks and commits |
| Fresh Luna-high reviewer after author release | S06D1_REVIEW.md | Exact source/evidence review before the instrumentation/lifecycle checkpoint advances |

Runtime paths are under `Game/Assets/DragLab/Scripts/`. Other Game edits need explicit reassignment; no overlapping writers. The view audit is read-only until the next concrete consumer contract is frozen. Native and Editor processes run sequentially after all authors release/freeze Game sources. Existing unrelated reports, attachments, tests and generated artifacts remain untouched.

## S06d2a — ready product-group reuse contract (2026-10-24)

Baseline `0d3c7ae`. This is the first bounded S06d2 implementation checkpoint; owned-car refresh is the next separately committed step. Reuse completed authored product groups in the native Shop browser without repeating canonical key/group construction. The lookup is read-only, succeeds only for a current validated catalog/layout, and never finishes a warmup synchronously. Preserve exact fallback, source order, representative priority (fitted, same-model prefix, ordinal ID), aliases/search, live stock, cart holds, ownership, price and affordability. Do not add JSON or full-profile copying. Existing unconverted JSON is explicit S13 debt, never a completed final state. Do not weaken authoring mutation invalidation or verification oracles to manufacture a hit.

| Owner | Exclusive write paths | Work |
| --- | --- | --- |
| s06d_views_medium / Luna medium | Domain/MarketCatalogLayout.cs; Runtime/NativePartBrowserModel.cs; docs/save-refactor/S06D2A_CORE.md | Validated ready-group API and exact browser consumption; document source/mutability assumptions and fallback |
| s06d_lifecycle_medium / Luna medium | New Runtime/NativeReadyProductGroupChecks.cs + meta; docs/save-refactor/S06D2A_TEST_MATRIX.md | Independent parity, representative, alias, state mutation and invalidation checks; coordinate API first |
| Coordinator | Editor/StateCommandCheckpointVerification.cs runner; ledger/status/results, process/Git | Final integration, matched native/managed checks and commit |
| Fresh Luna-high reviewer | S06D2A_REVIEW.md | Independent correctness/performance evidence review before next increment |

Paths relative to Game/Assets/DragLab/Scripts except docs. Authors must release/freeze Game sources before any Unity process. Parent alone runs Unity/player/Git. No ownership-signal/owned UI changes in this checkpoint, and no S07 until the full S06 gate passes. Independent whole-frame measurements decide whether this removes the measured Parts spike; source expectations are not proof.

S06d2a telemetry extension before source freeze: views author also owns four bounded per-browser scalar measurements (ready-path flag, ready lookup, current inspection and Shop population elapsed milliseconds) in NativePartBrowserModel.cs. Coordinator additionally owns the test-only NativeTypedFluidRefillVerification.cs note immediately after pending Build navigation. This records the actual constructor path/substages in the real player without changing checks, physics or UI; no new per-frame log or general profiling framework. Warmup budgets remain soft, and the actual whole-frame gate remains authoritative.

S06d2a verifier handoff: lifecycle author released NativeReadyProductGroupChecks.cs to coordinator after Editor fixture failures. Parent isolates search/affordability from compatibility while preserving the default stock-filter assertion and all production query defaults. The other-model fixture deliberately need not fit the selected car. No test or source edits overlap. Failed attempts remain under S06D2/attempt*/; only successful final-source evidence may close the checkpoint.

### S06d2a measured correction — independent Shop readiness (2026-10-25)

Editor67866 passes2337 group checks plus existing pilot/owner/migration/storage; Windows29666 and deterministic native80595 pass70/0. Performance does not pass: actual navigation used the cold fallback (`readyGroups=False`), nativeParts129.062 ms / frame154.106 ms. Full market-fit layout completion was an unnecessary dependency and delayed Shop group availability after catalog replacement/recovery. Do not make the test wait longer or mark this passed.

Views author retains its production-file ownership: prepare and publish complete source-ordered groups immediately after canonical keys, independently of full market fit/layout preparation. Publish only against the exact captured key-snapshot object, revision and definition references after semantic validation. Keep group preparation bounded/yielding; preserve existing market fitment/delivery order and seeds. Lookup needs complete groups and current semantic keys, never partial groups or a synchronous warmup. An explicit `parts/shop ready` iterator marker identifies complete Shop publication; the parent-owned verifier asserts unavailability before it and exact ready groups during the remaining market-fit warmup, rather than incorrectly demanding full market completion. The unchanged real native navigation and all whole-frame tails remain the acceptance gate. This is correction inside the uncompleted S06d2a checkpoint, not authorization to advance to owned-car work or S07.

S06d2a remaining-population attribution: views author owns six additional ready-loop scalar phase sums in its existing file, and parent owns corresponding one-shot managed/native verification notes. The focused `RunNativeReadyProductGroupsOnlyCheckpoint` entry runs only catalogue checks under an explicit safe report root; it does not imply fresh persistence regression execution. Prior full52908 evidence remains archived. Current phase sums are nested, diagnostic timestamps add overhead, and the unchanged real player frame remains the final gate.

### S06d2a remaining exact supply projection contract (2026-10-25)

Focused Editor54177 passes1567 wrapper entries; ready-loop attribution is representative8.736 ms, count19.256 ms, quote/hold17.647 ms, entry6.369 ms, aliases1.851 ms and used0.109 ms. These observations locate supply counting and repeated product lookup as the next costs. Current ready native navigation still fails the frame gate; no commit or phase advance.

Extend s06d_views_medium exclusive production ownership to Domain/PartsMarket.cs alongside its prior MarketCatalogLayout.cs / NativePartBrowserModel.cs / CORE doc. Optimize Counter using an operation-local physical-interface candidate index that preserves selected-spec priority, model order, and calls the original exact Fits/Count on candidates; do not change quantity semantics or use mutable long-lived profile caches. An index is a prefilter only: all retained candidates still pass the authoritative Fits predicate, with a safe exact fallback for exceptional/missing keys. No formula duplication or omitted fitment rule.

For ready groups only, allow an internal Ledger prebinding of a representative to the same stock row its existing direct-ID / first-equivalent-product fallback would choose, using the validated group aliases. Preserve direct-ID precedence and original row ordering, null misses, live remaining quantity, holds, fees and quote arithmetic. Never identify products only by category/name, never cache player stock in authored groups, and never change global inventory indexes or checkout semantics to make browsing faster. Existing non-ready callers retain their exact path. The ready group object is immutable and validated before binding. Any contract ambiguity returns to coordinator before implementation.

s06d_lifecycle_medium owns new Runtime/ShopSupplyProjectionChecks.cs + meta and S06D2A_SUPPLY_TEST_MATRIX.md: independent original Counter oracle over catalogue products and selected/no-selected models, physically incompatible products, exact inventory identity/stock fallback/direct precedence, partial/absent stock, active holds, prices/fees and live changes. Parent owns Editor runner / native notes and all execution/Git. No writes overlap; freeze all Game source before runs. No broader mutation, persistence or UI optimization is authorized by this extension; real full-frame and existing parity gates remain mandatory.

S06d2a final test handoff: after authors froze/released integration and Windows6426/native43138 completed, coordinator owns the small missing/null-interface oracle addition in ShopSupplyProjectionChecks.cs. It uses a detached piston definition and unchanged public/local/indexed counters, selected and no-car, without mutating catalog/player state. Production files remain frozen; rebuilding a player is not required solely for this non-player-invoked fixture addition. Root still records which exact production variant each build ran.

### Working-commit boundary, not a passed responsiveness gate

The exact product-group/supply component is runnable and functionally verified; native Parts still89.475 ms and frame124.561 ms. Independent review may approve a safe local working commit for this coherent source component only, with full S06d2 latency acceptance explicitly failing/open. Do not report a completed S06d2/S06 performance checkpoint or advance S07. The next necessary bounded browser/card-publication work remains inside S06d2, followed by owned-car refresh and a full final gate. This distinction preserves additive, reviewable history before larger UI edits and does not reduce the user's whole-plan/no-JSON/no-full-profile-clone objective.
