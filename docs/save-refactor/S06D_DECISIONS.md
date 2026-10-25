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
