using System;
using System.Threading;

namespace DragLab.Infrastructure
{
    /// <summary>Typed seam for the single-worker fluid command path.</summary>
    internal interface ITypedFluidRefillExecutor
    {
        TypedFluidRefillOutcome Commit(TypedSaveSlotBinding binding, TypedFluidRefillCommand command, long executionNowTicks);
        TypedFluidRefillOutcome Reconcile(TypedSaveSlotBinding binding, TypedFluidRefillIntent intent);
    }

    /// <summary>Production adapter for the reviewed direct-row SQLite writer.</summary>
    internal sealed class SqliteTypedFluidRefillExecutor : ITypedFluidRefillExecutor
    {
        public TypedFluidRefillOutcome Commit(TypedSaveSlotBinding binding, TypedFluidRefillCommand command, long executionNowTicks)
            => SqliteStateStore.CommitFluidRefill(binding, command, executionNowTicks);

        public TypedFluidRefillOutcome Reconcile(TypedSaveSlotBinding binding, TypedFluidRefillIntent intent)
            => SqliteStateStore.ReconcileFluidRefill(binding, intent);
    }

    /// <summary>Worker scheduling seam for deterministic failure and delay checks.</summary>
    internal interface ITypedFluidRefillScheduler
    {
        bool Queue(WaitCallback callback);
    }

    internal sealed class ThreadPoolTypedFluidRefillScheduler : ITypedFluidRefillScheduler
    {
        public bool Queue(WaitCallback callback) => ThreadPool.QueueUserWorkItem(callback);
    }

    internal enum TypedFluidRefillCoordinatorState
    {
        Idle,
        CommitInFlight,
        ReconcileInFlight,
        OutcomeUnknown,
        Terminal,
        CommittedAwaitingHandling
    }

    /// <summary>
    /// Bounded owner-thread coordinator for one durable fluid refill. This remains dormant
    /// until S06b binds it to the actual save owner and fences competing writers.
    /// </summary>
    internal sealed class TypedFluidRefillCoordinator
    {
        sealed class Completion
        {
            internal TypedFluidRefillOutcome Outcome;
            internal int Ready;
            internal int DispatchGate;

            internal void Publish(TypedFluidRefillOutcome value)
            {
                // Claim publication once, then expose the fully assigned outcome.
                if (Interlocked.CompareExchange(ref Ready, -1, 0) != 0) return;
                Outcome = value;
                Volatile.Write(ref Ready, 1);
            }
        }

        sealed class Pending
        {
            internal TypedSaveSlotBinding Binding;
            internal TypedFluidRefillCommand Command;
            internal TypedFluidRefillIntent Intent;
            internal Completion Completion;
        }

        readonly int ownerThreadId;
        readonly ITypedFluidRefillExecutor executor;
        readonly ITypedFluidRefillScheduler scheduler;
        Pending pending;
        TypedFluidRefillCoordinatorState state;
        TypedFluidRefillOutcome outcome;

        internal TypedFluidRefillCoordinator(ITypedFluidRefillExecutor executor, ITypedFluidRefillScheduler scheduler = null)
        {
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.scheduler = scheduler ?? new ThreadPoolTypedFluidRefillScheduler();
            ownerThreadId = Thread.CurrentThread.ManagedThreadId;
            state = TypedFluidRefillCoordinatorState.Idle;
        }

        internal bool HasPending { get { CheckOwner(); return pending != null; } }
        internal TypedFluidRefillCoordinatorState PendingState { get { CheckOwner(); return state; } }
        internal TypedFluidRefillOutcome PendingOutcome { get { CheckOwner(); return outcome; } }
        internal TypedFluidRefillIntent PendingIntent { get { CheckOwner(); return pending == null ? null : pending.Intent; } }
        internal TypedFluidRefillCommand PendingCommand { get { CheckOwner(); return pending == null ? null : pending.Command; } }

        internal bool TryBegin(TypedSaveSlotBinding binding, Func<long, TypedFluidRefillCommand> factory,
            long executionNowTicks, out string error)
        {
            CheckOwner();
            error = null;
            if (pending != null)
            {
                error = "A fluid refill is already retained by this coordinator.";
                return false;
            }
            if (binding == null) { error = "A selected save binding is required."; return false; }
            if (factory == null) { error = "A fluid command factory is required."; return false; }

            // Reserve capacity before running any caller-supplied capture/planning code.
            var reservation = new Pending { Binding = binding };
            pending = reservation;
            TypedFluidRefillCommand command;
            try
            {
                command = factory(executionNowTicks);
                if (command == null) throw new InvalidOperationException("Fluid command factory returned no command.");
                if (!command.MatchesActualBinding(binding))
                    throw new InvalidOperationException("Fluid command binding does not match the selected save binding.");
            }
            catch (Exception ex)
            {
                pending = null;
                error = ex.Message;
                return false;
            }

            reservation.Command = command;
            reservation.Intent = command.Intent;
            state = TypedFluidRefillCoordinatorState.CommitInFlight;
            if (!Dispatch(reservation, false, executionNowTicks, out error))
            {
                pending = null;
                state = TypedFluidRefillCoordinatorState.Idle;
                return false;
            }
            return true;
        }

        /// <summary>Promotes a completed worker mailbox without waiting or invoking user callbacks.</summary>
        internal void Poll()
        {
            CheckOwner();
            var current = pending;
            if (current == null || current.Completion == null || Volatile.Read(ref current.Completion.Ready) != 1) return;
            var received = current.Completion.Outcome;
            current.Completion = null;
            outcome = ValidateOutcome(current, received);
            switch (outcome.Status)
            {
                case TypedFluidRefillStatus.Committed:
                case TypedFluidRefillStatus.Replayed:
                    state = TypedFluidRefillCoordinatorState.CommittedAwaitingHandling;
                    break;
                case TypedFluidRefillStatus.OutcomeUnknown:
                    state = TypedFluidRefillCoordinatorState.OutcomeUnknown;
                    break;
                default:
                    state = TypedFluidRefillCoordinatorState.Terminal;
                    break;
            }
        }

        internal bool TryReconcile(out string error)
        {
            CheckOwner();
            error = null;
            if (pending == null || state != TypedFluidRefillCoordinatorState.OutcomeUnknown || pending.Completion != null)
            {
                error = "Only a retained unknown outcome can be reconciled.";
                return false;
            }
            state = TypedFluidRefillCoordinatorState.ReconcileInFlight;
            if (!Dispatch(pending, true, 0, out error))
            {
                // No reconciliation work was dispatched. Preserve the original unknown.
                state = TypedFluidRefillCoordinatorState.OutcomeUnknown;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Releases a definite terminal outcome. A durable success requires the owner to
        /// confirm publication/reconciliation handling; false retains it for retry.
        /// </summary>
        internal bool Acknowledge(bool ownerHandled, out string error)
        {
            CheckOwner();
            error = null;
            if (pending == null) { error = "There is no retained fluid operation to acknowledge."; return false; }
            if (state == TypedFluidRefillCoordinatorState.Terminal)
            {
                Release();
                return true;
            }
            if (state != TypedFluidRefillCoordinatorState.CommittedAwaitingHandling)
            {
                error = "An in-flight or unknown fluid operation cannot be acknowledged away.";
                return false;
            }
            if (!ownerHandled)
            {
                error = "The committed result remains retained until its owner handles publication or reconciliation.";
                return false;
            }
            Release();
            return true;
        }

        void Release()
        {
            pending = null;
            outcome = null;
            state = TypedFluidRefillCoordinatorState.Idle;
        }

        bool Dispatch(Pending target, bool reconcile, long executionNowTicks, out string error)
        {
            error = null;
            var completion = new Completion();
            target.Completion = completion;
            var workerExecutor = executor;
            var workerBinding = target.Binding;
            var workerCommand = target.Command;
            var workerIntent = target.Intent;
            try
            {
                bool queued = scheduler.Queue(_ =>
                {
                    // A scheduling exception may race an accepted callback. The gate
                    // guarantees that definite pre-dispatch failure prevents executor entry.
                    if (Interlocked.CompareExchange(ref completion.DispatchGate, 1, 0) != 0) return;
                    TypedFluidRefillOutcome result;
                    try
                    {
                        result = reconcile
                            ? workerExecutor.Reconcile(workerBinding, workerIntent)
                            : workerExecutor.Commit(workerBinding, workerCommand, executionNowTicks);
                        if (result == null)
                            result = Unknown("Fluid executor returned no outcome; durability is unknown.");
                    }
                    catch (Exception ex)
                    {
                        result = Unknown("Fluid executor threw; durability is unknown: " + ex.Message);
                    }
                    completion.Publish(result);
                });
                if (!queued)
                {
                    if (Interlocked.CompareExchange(ref completion.DispatchGate, 2, 0) == 0)
                    {
                        target.Completion = null;
                        error = "The fluid worker could not be scheduled.";
                        return false;
                    }
                    return true;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (Interlocked.CompareExchange(ref completion.DispatchGate, 2, 0) == 0)
                {
                    // The worker cannot enter the executor after this transition.
                    target.Completion = null;
                    error = "The fluid worker could not be scheduled: " + ex.Message;
                    return false;
                }
                // Callback crossed the gate first, so retain the operation until that
                // executor call finishes; never reconcile or release it prematurely.
                return true;
            }
        }

        static TypedFluidRefillOutcome ValidateOutcome(Pending target, TypedFluidRefillOutcome received)
        {
            if (received == null) return Unknown("Fluid worker completion was missing; durability is unknown.");
            if (received.Status == TypedFluidRefillStatus.OutcomeUnknown)
                return received;
            if (received.Status != TypedFluidRefillStatus.Committed && received.Status != TypedFluidRefillStatus.Replayed)
                return received;

            var result = received.Result;
            var request = target.Command.Request;
            try
            {
                if (result == null || result.OperationId != request.Identity.OperationId ||
                    result.SaveSlot != request.Identity.SaveSlot || result.ResetGeneration != request.Identity.ResetGeneration ||
                    result.CarId != request.CarId ||
                    result.AcceptedTick != target.Intent.AcceptedTick ||
                    result.BusinessReceiptUtcTicks != target.Intent.AcceptedTick ||
                    result.BusinessReceiptId != request.Identity.OperationId ||
                    result.Cost < 0 || result.CashAfter < 0 ||
                    result.OilAdded < 0 || result.OilAdded > request.RequestedOilLitres ||
                    result.CoolantAdded < 0 || result.CoolantAdded > request.RequestedCoolantLitres ||
                    result.CommittedLedgerRevision != checked(request.Versions.ExpectedLedgerRevision + 1) ||
                    result.CommittedStorageCommit != checked(request.Versions.ExpectedStorageCommit + 1))
                    return Unknown("Fluid durable result did not match the retained identity, target, or expected versions.");
                if (received.Status == TypedFluidRefillStatus.Committed)
                {
                    var plan = target.Command.Plan;
                    if (result.Cost != plan.Cost || result.CashAfter != plan.CashAfter ||
                        !TypedFluidRefillIntent.BitsEqual(result.OilAdded, plan.OilAdded) ||
                        !TypedFluidRefillIntent.BitsEqual(result.CoolantAdded, plan.CoolantAdded) ||
                        !TypedFluidRefillIntent.BitsEqual(result.OilAfter, plan.AfterFluids.OilLitres) ||
                        !TypedFluidRefillIntent.BitsEqual(result.CoolantAfter, plan.AfterFluids.CoolantLitres) ||
                        result.BusinessReceiptId != plan.Receipt.Id ||
                        (result.BusinessReceipt != null && !SameReceipt(plan.Receipt, result.BusinessReceipt)))
                        return Unknown("Fresh fluid durable result did not match the retained command plan.");
                }
            }
            catch (Exception ex)
            {
                return Unknown("Fluid durable result validation failed: " + ex.Message);
            }
            return received;
        }

        static TypedFluidRefillOutcome Unknown(string message)
            => new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null, message);

        static bool SameReceipt(DragLab.Domain.Mechanics.FluidRefillReceiptDelta expected, SqliteEconomyReceipt actual)
        {
            if (expected == null || actual == null || expected.Id != actual.Id || expected.Operation != actual.Operation ||
                expected.Target != actual.Target || expected.Revision != actual.Revision || expected.UtcTicks != actual.UtcTicks ||
                expected.CashBefore != actual.CashBefore || expected.CashAfter != actual.CashAfter ||
                BitConverter.DoubleToInt64Bits(expected.FuelAdded) != actual.FuelAddedBits ||
                BitConverter.DoubleToInt64Bits(expected.OilAdded) != actual.OilAddedBits ||
                BitConverter.DoubleToInt64Bits(expected.CoolantAdded) != actual.CoolantAddedBits) return false;
            return actual.DisposedIds().Length == 0 && actual.RetainedIds().Length == 0;
        }

        void CheckOwner()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThreadId)
                throw new InvalidOperationException("Typed fluid refill coordinator operations must run on their owner thread.");
        }
    }
}
