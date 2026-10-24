#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Independent bounded checks for the dormant S06a pending-refill owner.</summary>
    public static class TypedFluidRefillCoordinatorChecks
    {
        const int WaitMs = 5000;
        const long SeedNow = 638000000000000000L;
        static readonly OperatingPolicy Policy = new OperatingPolicy { oilPerLitre = 6.125, coolantPerLitre = 3.875, quoteSeconds = 300 };
        static VehicleDefinition[] authored;
        static MechanicalCatalog catalog;
        static int passed;

        sealed class FakeExecutor : ITypedFluidRefillExecutor
        {
            internal Func<TypedSaveSlotBinding, TypedFluidRefillCommand, long, TypedFluidRefillOutcome> CommitAction;
            internal Func<TypedSaveSlotBinding, TypedFluidRefillIntent, TypedFluidRefillOutcome> ReconcileAction;
            internal int CommitCalls, ReconcileCalls;
            internal TypedSaveSlotBinding LastBinding;
            internal TypedFluidRefillCommand LastCommand;
            internal TypedFluidRefillIntent LastIntent;
            internal long LastExecutionTicks;

            public TypedFluidRefillOutcome Commit(TypedSaveSlotBinding binding, TypedFluidRefillCommand command, long executionNowTicks)
            {
                Interlocked.Increment(ref CommitCalls);
                LastBinding = binding; LastCommand = command; LastExecutionTicks = executionNowTicks;
                return CommitAction == null ? Unknown("fake default") : CommitAction(binding, command, executionNowTicks);
            }

            public TypedFluidRefillOutcome Reconcile(TypedSaveSlotBinding binding, TypedFluidRefillIntent intent)
            {
                Interlocked.Increment(ref ReconcileCalls);
                LastBinding = binding; LastIntent = intent;
                return ReconcileAction == null ? Unknown("fake default") : ReconcileAction(binding, intent);
            }
        }

        sealed class FakeScheduler : ITypedFluidRefillScheduler
        {
            internal Func<WaitCallback, bool> QueueAction;
            public bool Queue(WaitCallback callback) => QueueAction(callback);
        }

        static void Check(Action<bool, string> check, bool condition, string label)
        {
            check(condition, label);
            if (!condition) throw new InvalidOperationException("S06a coordinator check failed: " + label);
            passed++;
        }

        static TypedFluidRefillOutcome Unknown(string error)
            => new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null, error);

        static TypedFluidRefillOutcome Terminal(TypedFluidRefillStatus status, string error = "expected terminal")
            => new TypedFluidRefillOutcome(status, null, error);

        static PlayerProfile NewProfile()
        {
            var profile = PlayerProfile.New(SeedNow - TimeSpan.TicksPerDay);
            profile.SelectedCar.vehicleId = profile.selectedVehicleId = authored[0].spec.id;
            MechanicsOwnership.Ensure(profile, catalog);
            profile.catalog = catalog;
            profile.cash = 6000;
            var fluid = profile.SelectedCar.fluids;
            fluid.oilLitres = Math.Min(2.1, catalog.Spec(profile.SelectedCar.vehicleId).fluids.oilCapacityLitres * .55);
            fluid.coolantLitres = Math.Min(1.4, catalog.Spec(profile.SelectedCar.vehicleId).fluids.coolantCapacityLitres * .5);
            fluid.oilTemperatureC = 91.25; fluid.oilPressureBar = 3.75; fluid.coolantPressureBar = 1.25;
            fluid.oilCondition = .63; fluid.oilConsumedLitres = .12; fluid.oilLeakedLitres = .03;
            fluid.coolantLostLitres = .04; fluid.serviceElapsedSeconds = 18.5;
            string invalid = MechanicsOwnership.Validate(profile, catalog);
            if (invalid != null) throw new InvalidOperationException("Invalid authored coordinator fixture: " + invalid);
            return profile;
        }

        static long Generation(string path)
        {
            using (var db = new SqliteNative(path, true))
                return long.Parse(db.Scalar("SELECT value FROM state_meta WHERE name='reset_generation'"), CultureInfo.InvariantCulture);
        }

        static TypedFluidRefillCommand BuildCommand(TypedSaveSlotBinding binding, string operation, long now)
        {
            var profile = ProfileStore.Load();
            var request = new FluidRefillRequest(new StateCommandIdentity(binding.SaveSlot, binding.ResetGeneration, operation),
                new StateCommandVersionFence(profile.revision, profile.storageCommit, MechanicalCatalog.Version),
                profile.SelectedCar.instanceId, .375, .25, now - 10, checked(now - 10 + TimeSpan.FromSeconds(Policy.quoteSeconds).Ticks));
            var plan = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(profile, Policy, request, now));
            if (!plan.Committable) throw new InvalidOperationException("Coordinator fixture plan is not committable: " + plan.Error);
            return TypedFluidRefillCommand.Create(binding, plan, MechanicsSchema.Model, now);
        }

        static TypedFluidRefillOutcome Success(TypedFluidRefillCommand command, TypedFluidRefillStatus status = TypedFluidRefillStatus.Committed)
        {
            var plan = command.Plan;
            var result = TypedFluidRefillResult.ForCommitted(command, checked(command.Request.Versions.ExpectedLedgerRevision + 1),
                checked(command.Request.Versions.ExpectedStorageCommit + 1), command.Request.CarId, plan.Cost, plan.CashAfter,
                plan.OilAdded, plan.CoolantAdded, plan.AfterFluids.OilLitres, plan.AfterFluids.CoolantLitres,
                command.Request.Identity.OperationId, null);
            return new TypedFluidRefillOutcome(status, result, null);
        }

        static TypedFluidRefillOutcome StoredOutcome(TypedFluidRefillCommand command, long acceptedTick,
            double requestedOil, double oilAdded)
        {
            var source = command.Request;
            var request = new FluidRefillRequest(source.Identity, source.Versions, source.CarId, requestedOil,
                source.RequestedCoolantLitres, source.IssuedUtcTicks, source.ExpiresUtcTicks);
            var intent = TypedFluidRefillIntent.Create(command.ExpectedBinding, request, command.PhysicsIdentity, acceptedTick);
            var result = TypedFluidRefillResult.FromStored(intent,
                checked(source.Versions.ExpectedLedgerRevision + 1), checked(source.Versions.ExpectedStorageCommit + 1),
                source.CarId, 10, Math.Max(0, command.Plan.CashBefore - 10), oilAdded, .1,
                command.Plan.BeforeFluids.OilLitres + oilAdded, command.Plan.BeforeFluids.CoolantLitres + .1,
                source.Identity.OperationId, null);
            return new TypedFluidRefillOutcome(TypedFluidRefillStatus.Replayed, result, null);
        }

        static TypedFluidRefillOutcome StoredFreshOutcome(TypedFluidRefillCommand command, int cost,
            int cashAfter, double oilAfter, double coolantAfter)
        {
            var request = command.Request;
            var result = TypedFluidRefillResult.FromStored(command.Intent,
                checked(request.Versions.ExpectedLedgerRevision + 1), checked(request.Versions.ExpectedStorageCommit + 1),
                request.CarId, cost, cashAfter, command.Plan.OilAdded, command.Plan.CoolantAdded,
                oilAfter, coolantAfter, request.Identity.OperationId, null);
            return new TypedFluidRefillOutcome(TypedFluidRefillStatus.Committed, result, null);
        }

        static TypedFluidRefillCoordinator Begin(FakeExecutor executor, TypedSaveSlotBinding binding,
            TypedFluidRefillCommand command, long now, Action<bool, string> check, string label)
        {
            var coordinator = new TypedFluidRefillCoordinator(executor);
            Check(check, coordinator.TryBegin(binding, _ => command, now, out var error), label + (error == null ? "" : ": " + error));
            return coordinator;
        }

        static void WaitForState(TypedFluidRefillCoordinator coordinator, TypedFluidRefillCoordinatorState expected,
            Action<bool, string> check, string label)
        {
            var until = Environment.TickCount + WaitMs;
            do
            {
                coordinator.Poll();
                if (coordinator.PendingState == expected) { Check(check, true, label); return; }
                Thread.Yield();
            } while (unchecked(Environment.TickCount - until) < 0);
            Check(check, false, label + " (bounded wait expired; state=" + coordinator.PendingState + ")");
        }

        static void CheckDelayedCommit(TypedSaveSlotBinding binding, TypedFluidRefillCommand command, long now, Action<bool, string> check)
        {
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            {
                var executor = new FakeExecutor { CommitAction = (b, c, ticks) => { entered.Set(); release.WaitOne(WaitMs); return Success(c); } };
                var coordinator = Begin(executor, binding, command, now, check, "delayed command reserves and dispatches once");
                try
                {
                    Check(check, entered.WaitOne(WaitMs), "delayed executor entered within bounded harness wait");
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < 100; i++) coordinator.Poll();
                    sw.Stop();
                    Check(check, sw.ElapsedMilliseconds < 500 && coordinator.PendingState == TypedFluidRefillCoordinatorState.CommitInFlight,
                        "owner Poll stays nonblocking while commit is delayed");
                    Check(check, coordinator.PendingIntent != null && coordinator.PendingIntent.Request.Identity.OperationId == command.Intent.Request.Identity.OperationId,
                        "original intent remains retained while view is absent and commit is delayed");
                    bool factoryCalled = false;
                    Check(check, !coordinator.TryBegin(binding, _ => { factoryCalled = true; return command; }, now, out _) && !factoryCalled,
                        "capacity rejects before invoking a second factory");
                }
                finally { release.Set(); }
                WaitForState(coordinator, TypedFluidRefillCoordinatorState.CommittedAwaitingHandling, check,
                    "delayed completion survives originating view closure");
                Check(check, executor.CommitCalls == 1 && executor.LastCommand == command && executor.LastBinding == binding && executor.LastExecutionTicks == now,
                    "one exact worker commit receives only binding, immutable command and execution ticks");
                Check(check, !coordinator.Acknowledge(false, out _) && coordinator.PendingOutcome != null,
                    "publication failure retains durable result for retry");
                Check(check, coordinator.Acknowledge(true, out _) && !coordinator.HasPending,
                    "handled success is explicitly acknowledged and releases capacity");
                Check(check, coordinator.TryBegin(binding, _ => command, now, out _), "coordinator can be reused after terminal handling");
                // Drain the reuse operation before leaving the fixture.
                WaitForState(coordinator, TypedFluidRefillCoordinatorState.CommittedAwaitingHandling, check, "reused operation completes");
                Check(check, coordinator.Acknowledge(true, out _), "reused operation can be acknowledged");
            }
        }

        static void CheckFactoryAndThreadGuards(TypedSaveSlotBinding binding, TypedFluidRefillCommand command,
            TypedSaveSlotBinding foreignBinding, TypedFluidRefillCommand foreignCommand, long now, Action<bool, string> check)
        {
            var failedExecutor = new FakeExecutor();
            var coordinator = new TypedFluidRefillCoordinator(failedExecutor);
            int dispatchCount = 0;
            long factoryTicks = 0;
            Check(check, !coordinator.TryBegin(binding, _ => throw new InvalidOperationException("factory failure"), now, out _) && !coordinator.HasPending,
                "factory exception releases reservation before dispatch");
            Check(check, !coordinator.TryBegin(binding, _ => foreignCommand, now, out _) && !coordinator.HasPending && failedExecutor.CommitCalls == 0,
                "factory command with foreign binding releases reservation before dispatch");
            Check(check, coordinator.TryBegin(binding, ticks => { dispatchCount++; factoryTicks = ticks; return command; }, now, out _) &&
                dispatchCount == 1 && factoryTicks == now,
                "factory runs once on owner thread and receives exact execution ticks");
            WaitForState(coordinator, TypedFluidRefillCoordinatorState.OutcomeUnknown, check, "fake missing executor result becomes unknown");
            Check(check, coordinator.TryReconcile(out _), "unknown result schedules one explicit reconciliation");
            WaitForState(coordinator, TypedFluidRefillCoordinatorState.OutcomeUnknown, check, "unknown reconciliation stays retained");
            Check(check, ReferenceEquals(coordinator.PendingIntent, command.Intent), "unknown reconciliation reuses original intent");
            Check(check, !coordinator.Acknowledge(true, out _) && coordinator.HasPending,
                "unknown result cannot be discarded by acknowledgement");

            Action[] guardedOperations =
            {
                () => coordinator.Poll(),
                () => coordinator.TryBegin(binding, _ => command, now, out _),
                () => coordinator.TryReconcile(out _),
                () => coordinator.Acknowledge(false, out _),
                () => { var ignored = coordinator.HasPending; },
                () => { var ignored = coordinator.PendingState; },
                () => { var ignored = coordinator.PendingOutcome; },
                () => { var ignored = coordinator.PendingIntent; },
                () => { var ignored = coordinator.PendingCommand; }
            };
            for (int i = 0; i < guardedOperations.Length; i++)
            {
                bool rejected = false;
                var thread = new Thread(() =>
                {
                    try { guardedOperations[i](); }
                    catch (InvalidOperationException) { rejected = true; }
                }) { IsBackground = true };
                bool joined = false;
                try
                {
                    thread.Start();
                    joined = thread.Join(WaitMs);
                }
                finally
                {
                    if (thread.IsAlive) thread.Join(WaitMs);
                }
                Check(check, joined && rejected, "foreign-thread coordinator operation/getter is rejected: " + i.ToString(CultureInfo.InvariantCulture));
            }
            Check(check, coordinator.Acknowledge(false, out _) == false, "unresolved unknown operation remains unacknowledgeable");
        }

        static void CheckExecutorOutcomes(TypedSaveSlotBinding binding, TypedFluidRefillCommand command,
            TypedFluidRefillCommand otherCommand, long now, Action<bool, string> check)
        {
            foreach (var mode in new[] { "throw", "null", "inconsistent", "accepted-tick", "over-request", "fresh-cost", "fresh-cash", "fresh-after" })
            {
                var executor = new FakeExecutor();
                executor.CommitAction = (b, c, ticks) => mode == "throw" ? throw new InvalidOperationException("injected executor throw")
                    : mode == "null" ? null : mode == "inconsistent" ? Success(otherCommand)
                    : mode == "accepted-tick" ? StoredOutcome(c, c.AcceptedTick + 1, c.Request.RequestedOilLitres, .1)
                    : mode == "over-request" ? StoredOutcome(c, c.AcceptedTick, c.Request.RequestedOilLitres + .5, c.Request.RequestedOilLitres + .25)
                    : mode == "fresh-cost" ? StoredFreshOutcome(c, c.Plan.Cost + 1, c.Plan.CashAfter - 1,
                        c.Plan.AfterFluids.OilLitres, c.Plan.AfterFluids.CoolantLitres)
                    : mode == "fresh-cash" ? StoredFreshOutcome(c, c.Plan.Cost, c.Plan.CashAfter - 1,
                        c.Plan.AfterFluids.OilLitres, c.Plan.AfterFluids.CoolantLitres)
                    : StoredFreshOutcome(c, c.Plan.Cost, c.Plan.CashAfter,
                        c.Plan.AfterFluids.OilLitres + .001, c.Plan.AfterFluids.CoolantLitres);
                var coordinator = Begin(executor, binding, command, now, check, mode + " dispatch starts");
                WaitForState(coordinator, TypedFluidRefillCoordinatorState.OutcomeUnknown, check,
                    mode + " becomes retained outcome-unknown");
                Check(check, coordinator.PendingCommand == command && ReferenceEquals(coordinator.PendingIntent, command.Intent),
                    mode + " preserves original command identity and intent");
                Check(check, !coordinator.TryBegin(binding, _ => command, now, out _), mode + " blocks capacity reuse until resolution");
                Check(check, coordinator.TryReconcile(out _), mode + " can reconcile retained intent");
                WaitForState(coordinator, TypedFluidRefillCoordinatorState.OutcomeUnknown, check,
                    mode + " repeated unknown reconciliation remains retained");
                Check(check, executor.CommitCalls == 1 && executor.ReconcileCalls == 1,
                    mode + " performs one commit and one explicit reconciliation, with no retry loop");
            }

            var originalReadSet = command.Plan.ReadSet;
            var repricedReadSet = new FluidRefillReadSet(originalReadSet.CarId, originalReadSet.VehicleId,
                originalReadSet.EngineAssemblyId, originalReadSet.GarageJobId, originalReadSet.HasRebuild,
                originalReadSet.RebuildActive, originalReadSet.Cash, originalReadSet.LastEconomyUtcTicks,
                originalReadSet.OilCapacityLitres, originalReadSet.CoolantCapacityLitres,
                originalReadSet.OilPerLitre + 2.25, originalReadSet.CoolantPerLitre + 3.5,
                originalReadSet.Fluids, originalReadSet.HasUnsettledRun, originalReadSet.OperationAlreadyRecorded,
                originalReadSet.EconomyReceiptCount, originalReadSet.OldestEconomyReceiptId);
            var repricedPlan = FluidRefillPlanner.Plan(new FluidRefillInput(command.Request, repricedReadSet, command.Plan.PlanningNowTicks));
            if (!repricedPlan.Committable) throw new InvalidOperationException("Replayed changed-price fixture became uncommittable.");
            var repricedCommand = TypedFluidRefillCommand.Create(binding, repricedPlan, command.PhysicsIdentity, command.AcceptedTick);
            Check(check, command.Intent.IntentHex == repricedCommand.Intent.IntentHex && command.Plan.Cost != repricedCommand.Plan.Cost,
                "changed prices alter current plan while retaining byte-identical stable intent");
            var replayExecutor = new FakeExecutor { CommitAction = (b, c, ticks) => Success(command, TypedFluidRefillStatus.Replayed) };
            var replayCoordinator = Begin(replayExecutor, binding, repricedCommand, now, check,
                "same stable intent admits a changed-readset replay fixture");
            WaitForState(replayCoordinator, TypedFluidRefillCoordinatorState.CommittedAwaitingHandling, check,
                "valid replay after price changes remains accepted without current-plan repricing");
            Check(check, replayCoordinator.PendingOutcome.Status == TypedFluidRefillStatus.Replayed &&
                replayCoordinator.PendingOutcome.Result.Cost == command.Plan.Cost &&
                replayCoordinator.PendingOutcome.Result.Cost != repricedCommand.Plan.Cost,
                "replay preserves actual stored durable result after readset and price changes");
            Check(check, replayCoordinator.Acknowledge(true, out _), "changed-readset replay result can be explicitly handled");

            foreach (var status in new[] { TypedFluidRefillStatus.Rejected, TypedFluidRefillStatus.IntentMismatch, TypedFluidRefillStatus.NotCommitted })
            {
                var executor = new FakeExecutor { CommitAction = (b, c, ticks) => Terminal(status) };
                var coordinator = Begin(executor, binding, command, now, check, status + " command dispatched");
                WaitForState(coordinator, TypedFluidRefillCoordinatorState.Terminal, check, status + " promotes to definite terminal state");
                Check(check, coordinator.Acknowledge(false, out _) && !coordinator.HasPending,
                    status + " permits explicit terminal acknowledgement without publication");
            }
        }

        static void CheckSchedulingBoundaries(TypedSaveSlotBinding binding, TypedFluidRefillCommand command,
            long now, Action<bool, string> check)
        {
            foreach (bool throws in new[] { false, true })
            {
                var executor = new FakeExecutor();
                var scheduler = new FakeScheduler
                {
                    QueueAction = callback => throws ? throw new InvalidOperationException("injected pre-dispatch scheduling failure") : false
                };
                var coordinator = new TypedFluidRefillCoordinator(executor, scheduler);
                bool accepted = coordinator.TryBegin(binding, _ => command, now, out _);
                Check(check, !accepted && !coordinator.HasPending && executor.CommitCalls == 0,
                    (throws ? "scheduler throw" : "scheduler rejection") + " before callback entry releases reservation safely");
            }

            int queueCount = 0;
            var unknownExecutor = new FakeExecutor { CommitAction = (b, c, ticks) => Unknown("injected unknown") };
            var reconcileRejectingScheduler = new FakeScheduler
            {
                QueueAction = callback => Interlocked.Increment(ref queueCount) == 1
                    ? ThreadPool.QueueUserWorkItem(callback)
                    : false
            };
            var unknownCoordinator = new TypedFluidRefillCoordinator(unknownExecutor, reconcileRejectingScheduler);
            Check(check, unknownCoordinator.TryBegin(binding, _ => command, now, out _), "initial operation starts before reconciliation scheduling rejection");
            WaitForState(unknownCoordinator, TypedFluidRefillCoordinatorState.OutcomeUnknown, check, "injected unknown promotes before reconciliation scheduling rejection");
            Check(check, !unknownCoordinator.TryReconcile(out _) && unknownCoordinator.PendingState == TypedFluidRefillCoordinatorState.OutcomeUnknown &&
                ReferenceEquals(unknownCoordinator.PendingIntent, command.Intent),
                "reconciliation scheduling rejection preserves original unknown intent and capacity");

            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            {
                Thread acceptedWorker = null;
                var executor = new FakeExecutor { CommitAction = (b, c, ticks) => { entered.Set(); release.WaitOne(WaitMs); return Success(c); } };
                var scheduler = new FakeScheduler
                {
                    QueueAction = callback =>
                    {
                        acceptedWorker = new Thread(() => callback(null)) { IsBackground = true };
                        acceptedWorker.Start();
                        if (!entered.WaitOne(WaitMs)) throw new TimeoutException("worker did not enter executor");
                        throw new InvalidOperationException("scheduler threw after callback crossed dispatch gate");
                    }
                };
                var coordinator = new TypedFluidRefillCoordinator(executor, scheduler);
                bool accepted = coordinator.TryBegin(binding, _ => command, now, out _);
                try
                {
                    Check(check, accepted && coordinator.HasPending && coordinator.PendingState == TypedFluidRefillCoordinatorState.CommitInFlight,
                        "scheduler throw after accepted callback preserves the operation in flight");
                    for (int i = 0; i < 20; i++) coordinator.Poll();
                    Check(check, coordinator.PendingState == TypedFluidRefillCoordinatorState.CommitInFlight && !coordinator.TryReconcile(out _),
                        "ambiguous scheduling exception cannot expose early unknown or reconciliation before worker result");
                }
                finally
                {
                    release.Set();
                    if (acceptedWorker != null && !acceptedWorker.Join(WaitMs))
                        throw new TimeoutException("callback accepted before scheduler failure did not terminate within the bounded wait");
                }
                WaitForState(coordinator, TypedFluidRefillCoordinatorState.CommittedAwaitingHandling, check,
                    "callback accepted before scheduler throw publishes its single durable result");
                Check(check, executor.CommitCalls == 1 && coordinator.Acknowledge(true, out _),
                    "ambiguous scheduling completion is handled once and releases only after publication");
            }
        }

        static void CheckResolvedReconciliation(TypedSaveSlotBinding binding, TypedFluidRefillCommand command,
            long now, Action<bool, string> check)
        {
            foreach (var resolution in new[] { TypedFluidRefillStatus.Replayed, TypedFluidRefillStatus.NotCommitted })
            {
                using (var entered = new ManualResetEvent(false))
                using (var release = new ManualResetEvent(false))
                using (var executorExited = new ManualResetEvent(false))
                {
                    FakeExecutor executor = null;
                    executor = new FakeExecutor
                    {
                        CommitAction = (b, c, ticks) => Interlocked.CompareExchange(ref executor.CommitCalls, 0, 0) == 1
                            ? Unknown("simulated lost commit acknowledgement") : Terminal(TypedFluidRefillStatus.Rejected),
                        ReconcileAction = (b, intent) =>
                        {
                            entered.Set();
                            try
                            {
                                release.WaitOne(WaitMs);
                                return resolution == TypedFluidRefillStatus.Replayed
                                    ? Success(command, TypedFluidRefillStatus.Replayed)
                                    : Terminal(TypedFluidRefillStatus.NotCommitted);
                            }
                            finally { executorExited.Set(); }
                        }
                    };
                    var coordinator = new TypedFluidRefillCoordinator(executor);
                    Check(check, coordinator.TryBegin(binding, _ => command, now, out _),
                        resolution + " reconciliation case starts with one commit dispatch");
                    WaitForState(coordinator, TypedFluidRefillCoordinatorState.OutcomeUnknown, check,
                        resolution + " reconciliation case retains initial lost acknowledgement");
                    try
                    {
                        Check(check, ReferenceEquals(coordinator.PendingIntent, command.Intent) && coordinator.TryReconcile(out _),
                            resolution + " reconciliation dispatches using the exact original intent");
                        Check(check, entered.WaitOne(WaitMs), resolution + " reconciliation executor reaches deterministic gate");
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        for (int i = 0; i < 100; i++) coordinator.Poll();
                        stopwatch.Stop();
                        bool factoryCalled = false;
                        Check(check, stopwatch.ElapsedMilliseconds < 500 && coordinator.PendingState == TypedFluidRefillCoordinatorState.ReconcileInFlight,
                            resolution + " owner polling remains prompt while reconciliation is delayed");
                        Check(check, !coordinator.TryBegin(binding, _ => { factoryCalled = true; return command; }, now, out _) && !factoryCalled,
                            resolution + " retains capacity before a second command factory");
                        Check(check, !coordinator.Acknowledge(false, out _) && !coordinator.TryReconcile(out _),
                            resolution + " cannot be discarded or reconciled twice while worker is in flight");
                    }
                    finally
                    {
                        release.Set();
                        if (!executorExited.WaitOne(WaitMs))
                            throw new TimeoutException(resolution + " reconciliation executor did not exit within the bounded wait");
                    }

                    var terminalState = resolution == TypedFluidRefillStatus.Replayed
                        ? TypedFluidRefillCoordinatorState.CommittedAwaitingHandling
                        : TypedFluidRefillCoordinatorState.Terminal;
                    WaitForState(coordinator, terminalState, check,
                        resolution + " delayed reconciliation promotes its actual typed resolution");
                    Check(check, executor.CommitCalls == 1 && executor.ReconcileCalls == 1 &&
                        ReferenceEquals(executor.LastBinding, binding) && ReferenceEquals(executor.LastIntent, command.Intent),
                        resolution + " uses one commit and one reconciliation with original binding and intent");
                    Check(check, resolution == TypedFluidRefillStatus.Replayed
                        ? coordinator.PendingOutcome.Status == TypedFluidRefillStatus.Replayed && coordinator.Acknowledge(true, out _)
                        : coordinator.PendingOutcome.Status == TypedFluidRefillStatus.NotCommitted && coordinator.Acknowledge(false, out _),
                        resolution + " requires the matching explicit acknowledgement rule");
                    Check(check, coordinator.TryBegin(binding, _ => command, now, out _),
                        resolution + " acknowledgement releases capacity for subsequent reuse");
                    WaitForState(coordinator, TypedFluidRefillCoordinatorState.Terminal, check,
                        resolution + " reused coordinator completes a new terminal command");
                    Check(check, coordinator.Acknowledge(false, out _) && !coordinator.HasPending,
                        resolution + " reused coordinator can release its separately handled terminal result");
                }
            }
        }

        static long[] FluidBits(FluidState fluid) => new[]
        {
            fluid.oilLitres, fluid.coolantLitres, fluid.oilTemperatureC, fluid.oilPressureBar,
            fluid.coolantPressureBar, fluid.oilCondition, fluid.oilConsumedLitres, fluid.oilLeakedLitres,
            fluid.coolantLostLitres, fluid.serviceElapsedSeconds, fluid.oilBurnLps, fluid.oilLeakLps,
            fluid.coolantLeakLps, fluid.coolantVentLps
        }.Select(BitConverter.DoubleToInt64Bits).ToArray();

        static bool ReceiptMatches(EconomyReceipt actual, FluidRefillReceiptDelta expected)
        {
            return actual != null && actual.id == expected.Id && actual.operation == expected.Operation &&
                actual.target == expected.Target && actual.revision == expected.Revision && actual.utcTicks == expected.UtcTicks &&
                actual.cashBefore == expected.CashBefore && actual.cashAfter == expected.CashAfter &&
                BitConverter.DoubleToInt64Bits(actual.fuelAdded) == BitConverter.DoubleToInt64Bits(expected.FuelAdded) &&
                BitConverter.DoubleToInt64Bits(actual.oilAdded) == BitConverter.DoubleToInt64Bits(expected.OilAdded) &&
                BitConverter.DoubleToInt64Bits(actual.coolantAdded) == BitConverter.DoubleToInt64Bits(expected.CoolantAdded) &&
                actual.disposed != null && actual.disposed.Length == 0 && actual.retained != null && actual.retained.Length == 0;
        }

        static void CheckProductionSqlite(string outputDirectory, long now, Action<bool, string> check)
        {
            string directory = Path.Combine(Path.GetFullPath(outputDirectory), "s06a-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "coordinator.sqlite3");
            string oldPath = ProfileStore.VerificationPath;
            try
            {
                ProfileStore.VerificationPath = path;
                if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not initialize isolated production executor fixture: " + ProfileStore.Status);
                SqliteStateStore.Invalidate();
                long generation = Generation(path);
                var binding = new TypedSaveSlotBinding(path, "slot-1", generation);
                var command = BuildCommand(binding, "s06a-sqlite-" + Guid.NewGuid().ToString("N"), now);
                int cashBefore = ProfileStore.Load().cash;
                var expected = command.Plan;
                var coordinator = new TypedFluidRefillCoordinator(new SqliteTypedFluidRefillExecutor());
                Check(check, coordinator.TryBegin(binding, _ => command, now, out var error), "production SQLite coordinator accepts valid frozen S04 command" + (error == null ? "" : ": " + error));
                WaitForState(coordinator, TypedFluidRefillCoordinatorState.CommittedAwaitingHandling, check,
                    "production SQLite commit completes through coordinator worker");
                var retained = coordinator.PendingOutcome;
                Check(check, retained != null && retained.Status == TypedFluidRefillStatus.Committed && retained.Result != null,
                    "production coordinator retains committed typed result");
                SqliteStateStore.Invalidate();
                var cold = ProfileStore.Load();
                Check(check, cold.cash == expected.CashAfter && cold.revision == command.Request.Versions.ExpectedLedgerRevision + 1 &&
                    cold.storageCommit == command.Request.Versions.ExpectedStorageCommit + 1 && cashBefore - cold.cash == expected.Cost,
                    "independent plan and fresh cold ProfileStore load agree on durable cash and versions");
                Check(check, FluidBits(cold.SelectedCar.fluids).SequenceEqual(FluidBits(expected.AfterFluids.ToDetachedState())),
                    "independent S04 plan matches all fourteen cold-loaded fluid values bit for bit");
                Check(check, cold.economyReceipts.Count == 1 && ReceiptMatches(cold.economyReceipts[0], expected.Receipt),
                    "cold-loaded business history matches independent planned scalar, quantity and empty-item facts");
                using (var db = new SqliteNative(path, true))
                {
                    Check(check, db.Scalar("SELECT COUNT(*) FROM operation_receipts WHERE operation_id=?", command.Intent.Request.Identity.OperationId) == "1",
                        "production writer persists exactly one operation receipt");
                    Check(check, db.Scalar("SELECT COUNT(*) FROM fluid_refill_results WHERE operation_id=?", command.Intent.Request.Identity.OperationId) == "1",
                        "production writer persists exactly one refill result");
                }
                Check(check, coordinator.Acknowledge(true, out _), "production committed result is explicitly handled and acknowledged");

                SqliteStateStore.Invalidate();
                var replay = new TypedFluidRefillCoordinator(new SqliteTypedFluidRefillExecutor());
                Check(check, replay.TryBegin(binding, _ => command, now + TimeSpan.FromDays(1).Ticks, out _), "same immutable command can be submitted for production replay");
                WaitForState(replay, TypedFluidRefillCoordinatorState.CommittedAwaitingHandling, check, "production SQLite replay completes");
                Check(check, replay.PendingOutcome.Status == TypedFluidRefillStatus.Replayed && replay.PendingOutcome.Result.Cost == expected.Cost,
                    "production reconciliation/replay reports prior durable effect without repricing");
                SqliteStateStore.Invalidate();
                var replayCold = ProfileStore.Load();
                Check(check, replayCold.cash == cold.cash && replayCold.revision == cold.revision && replayCold.storageCommit == cold.storageCommit &&
                    FluidBits(replayCold.SelectedCar.fluids).SequenceEqual(FluidBits(cold.SelectedCar.fluids)) &&
                    replayCold.economyReceipts.Count == cold.economyReceipts.Count &&
                    ReceiptMatches(replayCold.economyReceipts.Single(), expected.Receipt),
                    "all cold-loaded fluid bits, cash, versions and business history are unchanged by exact production replay");
                Check(check, replay.Acknowledge(true, out _), "replayed production result can be acknowledged after handling");
            }
            finally
            {
                SqliteStateStore.Invalidate();
                ProfileStore.VerificationPath = oldPath;
            }
        }

        public static void Run(Action<bool, string> check, string outputDirectory)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("An isolated output directory is required.", nameof(outputDirectory));
            passed = 0;
            string oldPath = ProfileStore.VerificationPath;
            string directory = Path.Combine(Path.GetFullPath(outputDirectory), "s06a-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "commands.sqlite3");
            try
            {
                authored = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
                if (authored == null || authored.Length == 0) throw new InvalidOperationException("S06a requires authored vehicle definitions.");
                catalog = new MechanicalCatalog(authored.Select(vehicle => vehicle.spec));
                ProfileStore.VerificationPath = path;
                if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not initialize valid S06a command fixture: " + ProfileStore.Status);
                SqliteStateStore.Invalidate();
                var binding = new TypedSaveSlotBinding(path, "slot-1", Generation(path));
                const long now = SeedNow;
                var command = BuildCommand(binding, "s06a-delay-" + Guid.NewGuid().ToString("N"), now);
                var foreignBinding = new TypedSaveSlotBinding(Path.Combine(directory, "other.sqlite3"), "slot-1", binding.ResetGeneration);
                var foreignCommand = BuildCommand(foreignBinding, "s06a-foreign-" + Guid.NewGuid().ToString("N"), now);

                CheckDelayedCommit(binding, command, now, check);
                CheckFactoryAndThreadGuards(binding, command, foreignBinding, foreignCommand, now, check);
                CheckExecutorOutcomes(binding, command, foreignCommand, now, check);
                CheckSchedulingBoundaries(binding, command, now, check);
                CheckResolvedReconciliation(binding, command, now, check);
                CheckProductionSqlite(outputDirectory, now + 1000, check);
                UnityEngine.Debug.Log("S06a typed refill coordinator checks passed: " + passed.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                SqliteStateStore.Invalidate();
                ProfileStore.VerificationPath = oldPath;
                authored = null; catalog = null;
            }
        }
    }
}
#endif
