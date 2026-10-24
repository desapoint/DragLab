# S06a typed pending-refill coordinator

Baseline: `c49f103` (reviewed S05b direct-row writer).

This checkpoint adds a dormant one-operation coordinator for the typed fluid refill. It is not bound to the live ProfileStore or gameplay/UI route. S06b still needs to source the selected owner binding and fence every competing mutation/save before activation.

## Frozen API

- `ITypedFluidRefillExecutor.Commit(binding, command, executionNowTicks)` and `Reconcile(binding, intent)` return `TypedFluidRefillOutcome`. `SqliteTypedFluidRefillExecutor` delegates to the S05b writer.
- Construct `TypedFluidRefillCoordinator(executor, scheduler = null)` on its owner thread. The default `ThreadPoolTypedFluidRefillScheduler` queues one work callback; deterministic checks can supply `ITypedFluidRefillScheduler`. `TryBegin(binding, factory, executionNowTicks, out error)` reserves capacity before synchronously invoking `factory(executionNowTicks)`. The returned command must target the same binding. One worker dispatch follows.
- `Poll()` checks the worker mailbox without waiting. `PendingState`, `PendingOutcome`, `PendingIntent`, and `PendingCommand` are owner-thread-only. A committed/replayed result must match operation, slot, reset generation, accepted time, requested car and next ledger/storage versions. Fresh committed output also matches the retained original plan bit-for-bit. Replayed output is checked against immutable intent and durable bounds without re-pricing against the plan; optional receipt facts are validated by the typed result constructor.
- `TryReconcile(out error)` is permitted only after a polled unknown result. It uses the same binding and original intent; it never creates a new ID or plan.
- `Acknowledge(ownerHandled, out error)` releases terminal non-success outcomes directly. A committed/replayed result is released only with `ownerHandled: true`, which the owner calls after publishing/reconciling it. Passing false keeps the exact result for retry.

## Ownership and failure behavior

One owner thread accesses the coordinator. Workers receive only typed immutable command/intent data, binding, execution time, executor and a completion mailbox. They do not call back into the coordinator, Unity, profile state, UI or synchronization context. The worker publishes the mailbox outcome before setting its volatile ready flag; `Poll` consumes it once.

Capacity includes factory reservation, in-flight work, unread completion, unknown outcomes, and committed results awaiting owner handling. Factory/null/binding errors and scheduler failures release only when an interlocked dispatch gate proves the callback cannot enter the executor. Worker exceptions and null outcomes become unknown. If scheduling reports an error after the worker crossed the gate, the operation remains in flight until that executor call completes; reconciliation and release stay unavailable. Unknown blocks new admissions and can only be resolved by explicit reconciliation. A reconciliation scheduling failure preserves unknown state. Navigation and caller publication exceptions have no coordinator callback path and cannot drop the retained outcome.

No cancellation, automatic retry, queue beyond one item, full profile, or durable history is held here. Reset/slot lifecycle fencing, actual selected-owner binding, live-state patch publication, interruption behavior, broader writer serialization, and frame-budget/device validation are later S06 work. This infrastructure alone does not make gameplay writes responsive or safe to activate.

## Verification

Implementation inspection confirms capacity reservation precedes factory execution, one queued callback per operation, owner-thread guards on public operations/getters, mailbox publication with a claim/ready handshake, dispatch-gate serialization, result identity/version/request-bound checks, stable-intent reconciliation and explicit success acknowledgement. Parent-run Unity6000.6.4f1 verification passes149 focused +72 isolated legacy storage checks, exit0. The scheduler seam exercises deterministic dispatch failure races and delayed reconciliation resolves to both replayed and proven non-commit outcomes. The specific interim mailbox publication window is source-reviewed rather than deterministically paused in the harness. See [S06A_RESULTS](../../reports/save-refactor/S06A_RESULTS.md) for exact execution evidence, corrections and limits; independent review remains a separate gate.
