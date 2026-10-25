# S06d lifecycle verification contract

Date: 2026-10-24. This is a read-only implementation contract for the native Windows S06d checkpoint; no game code or Git state was changed while preparing it.

## Baseline and purpose

S06c already drives the real native maintenance dialog, actual SQLite typed refill writer, official isolated profile, exact selected car, oil/coolant quantities, pending UI, separate Settings persistence/drafts, navigation, and F8 legacy controls. See [S06c results](../../reports/save-refactor/S06C_RESULTS.md), [review](S06C_REVIEW.md), and [execution ledger](EXECUTION.md). S06d adds the missing lifecycle outcomes to that same native path: a lost acknowledgement followed by idempotent retry, a definite pre-commit failure and subsequent new attempt, failed recovery while the owner lease stays fenced, and suspend/resume while a real SQLite worker is held. Synthetic coordinator/pilot checks remain useful unit evidence but do not substitute for these player workflows.

Existing seams make this feasible without faking durable state:

- `TypedFluidRefillPilot` accepts an `ITypedFluidRefillExecutor`; `SqliteTypedFluidRefillExecutor` delegates directly to `SqliteStateStore.CommitFluidRefill` and `ReconcileFluidRefill`.
- `PrototypeGame.TypedFluidRefill.cs` lazily constructs the pilot in private `manualRefillPilot`; native verification can install a development-only pilot using a delegating executor around the real SQLite executor.
- The wrapper must call the real executor first, then replace only its returned acknowledgement with `OutcomeUnknown` for the lost-ack case. It must never fabricate `Committed`, alter a DB image, or bypass the real reconcile query.
- `SqliteStateStore.FailureInjection` and its `:before-commit` callback already hold the real writer for pending-state coverage. For definite failure, throw from a wrapper before entering the real executor (or use an established pre-commit injected failure); do not turn a post-commit exception into a claimed rollback.
- UI action captions are child `Label`s on `Button` surfaces, not `Button.text`. `TypedRefillActionCaptionVisible` already checks this for `Recover` and `Complete`; use the same helper for dialog retry label (`Retry status`); the maintenance entry itself uses `Check save` and preserve the surface.
- `SuspendForMobile()` deliberately returns without a full-profile save while a refill owns the mutation fence. The test must exercise this real lifecycle boundary and then advance frames through normal `UpdateManualRefill`/polling. Do not add any main-thread wait or assume suspension itself drains the worker.

## Native test contract

Use the existing disposable native Windows development player workflow and its isolated save directory. Begin each branch from a persisted/reloaded owned-car fixture with known capacities and enough cash. Snapshot the operation ID, selected slot/reset generation, owner epoch, revision/commit, cash, exact oil/coolant bit values, receipt set, and relevant preference values. The test owns this generated isolated profile: it may persist a fresh capacity fixture between branches, and it must retain each acknowledged refill and its receipt as evidence. Never restore pre-command database bytes or undo a real acknowledged purchase. Production user save files must never be used or modified.

### A. Commit succeeds, acknowledgement is lost, retry reconciles

1. Open the actual oil refill dialog, choose a nontrivial quantity, and record the displayed total and all captured identity/version values.
2. Install a development-only delegating executor. On `Commit`, invoke the real SQLite executor exactly once, then return `OutcomeUnknown` without its successful result. `Reconcile` delegates unchanged to the real SQLite executor.
3. Confirm using the native input probe and advance frames. Assert the UI exposes the enabled retry action through its dialog child caption (`Retry status`); the separate maintenance entry uses `Check save`, the same operation ID remains, and owner cash/fluid/revision/commit remain at the pre-command in-memory values until reconciliation is handled. The durable DB is expected to contain one real committed refill and its receipt.
4. Activate the visible retry action. Assert the original retained identity is reconciled, not a new refill: durable operation receipt count increases once, cash decreases by exactly the quoted cost once, oil addition matches the accepted amount bit-for-bit, coolant is unchanged, and publication advances the exact expected ledger/storage fences. Verify returned owner receipt and selected slot/generation agree with the persisted commit.
5. Reopen/cancel another dialog or navigate and return; assert no second charge/application. This detects accidental replay-as-new-command.

### B. Definite pre-commit failure and fresh attempt

1. On a freshly persisted fixture, have the wrapper fail before calling the real commit executor and return a definite non-unknown failed outcome. Alternatively inject a guaranteed pre-commit failure whose phase is demonstrably before SQLite `COMMIT`.
2. Assert the UI shows the failure and retry/recovery action appropriate to the surfaced state, while in-memory resources and durable SQLite owner image both match the exact pre-command snapshot: no receipt, commit token, cash, fluid, or revision movement.
3. Resolve/acknowledge only through the supported UI action. Then create a fresh quote and confirm it. Assert a new operation ID is used and exactly one successful charge/refill is durable and published.
4. A thrown exception after the real commit is not a definite failure case; classify it as outcome unknown and prove by reconciliation.

### C. Failed recovery retains owner fence

1. Cause a genuine stale admission/recovery requirement by changing one captured dependency before confirmation, or create a committed-awaiting-publication scenario through the existing development fault seam without falsifying the DB result.
2. Make one explicit recovery attempt fail using an isolated development-only load/recovery fault at a point that does not corrupt, delete, or rewrite the fixture. Assert the owner lease/context and recovery-required state remain retained; retry/recovery UI stays usable, unrelated commands and another refill remain fenced, and cash/fluid/revision/commit are not changed in memory by the failed recovery.
3. Remove the injected fault and use the real validated selected-owner reload/recovery route. Assert matching slot, reset generation, path/epoch, commit receipt and operation receipt; only then may the lease/fence clear. A failed load must never clear the operation or authorize a new command.

### D. Real interruption/resume while SQLite worker is held

1. Persist the fixture; open the actual native oil/coolant dialog; hold the actual SQLite worker at `:before-commit` with `ManualResetEventSlim` or an equivalent bounded gate. Capture the operation identity and pre-command owner state.
2. While the worker is held, advance Unity frames, assert the busy state remains, and call the actual `game.SuspendForMobile()` lifecycle method (or its real native pause callback in the controlled player). Assert it returns promptly, retains the exact operation and mutation fence, does not synchronously wait for the worker, and does not replace/reload the profile. Existing separate preference save behavior may persist preferences, but no refill resource publication occurs.
3. Release the worker from a `finally`-protected gate, invoke the actual `OnApplicationPause(false)` callback, and yield Unity frames until normal owner-thread polling handles completion. This is a controlled lifecycle callback exercise, not a claim that the OS suspended or killed the process. Bound each wait and fail with diagnostics on timeout. Assert the same operation ID, selected owner identity, cash, exact requested fluid delta, one receipt, and expected commit/revision fences survive interruption/resume; no duplicate dispatch occurs.
4. Verify the final isolated SQLite owner independently. If simulating process interruption is needed, use the established isolated player restart/load workflow; do not block the owner thread or claim that `SuspendForMobile` represents OS process death.

## Wait, cleanup, and evidence rules

- Every worker coordination wait has a short explicit timeout, and the Unity coroutine yields frames while waiting. No `.Wait()`, `.Result`, `Join`, or owner-thread blocking wait is allowed.
- Always release held callbacks and restore `SqliteStateStore.FailureInjection` and `ProfileStore.ControlledRecoveryFailureInjection` in `finally`, including assertion/timeout failure paths. Restore a replaced pilot only after it has no retained operation; leave any unresolved lease and its pilot intact for truthful recovery. Do not dispose synchronization objects until the worker-exit signal proves the callback has finished. Do not roll back the isolated profile's committed refills or restore pre-command database/checkpoint bytes. Preferences must remain untouched unless a test explicitly dirties them, in which case restore only through their supported save path.
- Keep F8 parity coverage and existing S06c checks intact. Do not change the visual hierarchy or assert the action using `Button.text`; inspect the rendered caption label above `RacingSurface`.
- Each branch should record before/after owner IDs and database/WAL evidence hashes where relevant, operation ID, command outcome, exact SQL operation/refill receipt facts, cash/fluid bits, revision/commit/generation, preference/draft state, bounded wait outcomes, and captured lifecycle frame evidence. For a definite rollback, compare authoritative SQL owner rows and fences; WAL byte differences alone are not proof of a changed committed state. Report separately the gates rerun and retained S06c baselines.
- A passing automation result is technical evidence only; visual/user acceptance remains separate.

## Suspected production-sensitive edges to audit during implementation

1. `SuspendForMobile` returns early while refill blocks mutation; verify the subsequent resume path continues polling the same pilot and does not accidentally recreate it or run an old legacy full save.
2. Retry caption and command dispatch are built by `QuantityAction`/typed dialog helpers. A stale visible button/caption after outcome transition could call the ordinary quote path or become disabled; assert the exact child label and callback identity.
3. A lost acknowledgement after a real commit must remain unknown until SQLite receipt reconciliation. Never release the coordinator lease, clear the operation ID, restore the original owner snapshot, or issue a second operation before reconcile.
4. Recovery errors currently have several paths (ordinary validated load after lost publication and controlled recovery with an active lease). Ensure failed recovery does not consume or abandon the retained lease/context and does not overwrite the isolated primary/backup.
5. Do not use `FailureInjection` as if an exception were proof of no commit unless its exact injected phase is before `COMMIT`; the actual `fluid-refill:before-commit` hook is the definite-failure fixture and must be checked against durable SQL rows/fences.

## Authorized implementation boundary

S06d may edit only the native verification/development fault seam needed for these cases, its Unity `.meta` if needed, and concise S06d test/results ledger documentation. Production changes are permitted only for a reproduced defect in this authorized lifecycle path, with a focused implementation and regression assertion. No schema, player data format, unrelated gameplay/UI, broad save-path refactor, or later S07 work is included. Build/native gates and a local commit are the parent coordinator's responsibility after this contract review.

## Implementation status

The dedicated native lifecycle coroutine is drafted in `Game/Assets/DragLab/Scripts/Runtime/NativeTypedRefillLifecycleVerification.cs`, with a stable Unity metadata file. It is routed through the parent coordinator's `--draglab-typed-refill-lifecycle-only-verify` option and uses only the generated isolated profile. The profile intentionally retains real acknowledged test refills; each following branch prepares a fresh persisted capacity fixture. Source/build/native checks have not yet been run, so this is implementation in progress rather than a completed S06d checkpoint.

### Owner-check factory tick correction

The native lifecycle rerun exposed a clock race in the S06b `SelectedProfileOwnerChecks.MakeCommand` fixture: the coordinator supplied one admission/accepted tick, but the factory independently sampled `DateTime.UtcNow.Ticks` and could create a command whose accepted tick was later than the strict writer's execution tick. The fixture now captures one outer admission tick (`max(saved clock + 10 ticks, current UTC ticks)`), passes the factory-supplied tick into `MakeCommand`, and uses that same tick for planning, intent creation, and writer admission across all three owner-check commands. The production strict ordering fence is unchanged. If the real SQLite case still fails, its assertion now includes outcome status/error and admission, factory, and accepted ticks. A read-only scan found `TypedFluidRefillPilot` already threads its single captured `acceptedTicks` through the factory; no analogous command-factory wall-clock reread was found in the other `TryBeginTypedOperation` call sites. The rerun is pending.
