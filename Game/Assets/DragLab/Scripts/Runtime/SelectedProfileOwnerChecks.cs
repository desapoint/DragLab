#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Independent selected-save owner and typed-write fence checks for S06b.</summary>
    public static class SelectedProfileOwnerChecks
    {
        sealed class HeldScheduler : ITypedFluidRefillScheduler
        {
            internal WaitCallback Callback;
            public bool Queue(WaitCallback callback) { Callback = callback; return true; }
            internal void Run() { var callback = Callback; Callback = null; callback?.Invoke(null); }
        }

        sealed class UnknownExecutor : ITypedFluidRefillExecutor
        {
            public TypedFluidRefillOutcome Commit(TypedSaveSlotBinding binding, TypedFluidRefillCommand command, long now)
                => new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null, "held fixture outcome");
            public TypedFluidRefillOutcome Reconcile(TypedSaveSlotBinding binding, TypedFluidRefillIntent intent)
                => new TypedFluidRefillOutcome(TypedFluidRefillStatus.NotCommitted, null, "fixture proves absence");
        }

        static int passed;
        static Action<bool, string> report;
        static void Check(bool condition, string label)
        {
            report(condition, label);
            if (!condition) throw new InvalidOperationException("S06b owner check failed: " + label);
            passed++;
        }

        static string DbMeta(string path, string name)
        {
            using (var db = new SqliteNative(path, true))
                return db.Scalar("SELECT value FROM state_meta WHERE name=?", name);
        }

        static string Slot() => ProfileStore.SelectedSlot;

        static void SetWriteBlocked(bool value)
        {
            typeof(ProfileStore).GetField("writeBlocked", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
        }

        static bool GetStaticBool(Type type, string name)
            => (bool)type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        static void SetStaticField(Type type, string name, object value)
            => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);

        static void DrainPendingSaveForCleanup()
        {
            var field = typeof(ProfileStore).GetField("pendingValidatedSave", BindingFlags.NonPublic | BindingFlags.Static);
            var ticket = field.GetValue(null);
            if (ticket == null) return;
            var task = (Task)ticket.GetType().GetField("Task", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ticket);
            if (task != null) try { task.Wait(TimeSpan.FromSeconds(6)); } catch { }
            field.SetValue(null, null);
        }

        static void CompareReceipt(string path, SqliteStateOwnerReceipt receipt, string label)
        {
            Check(receipt != null && receipt.CommitId.ToString(CultureInfo.InvariantCulture) == DbMeta(path, "commit_id") &&
                receipt.Generation.ToString(CultureInfo.InvariantCulture) == DbMeta(path, "reset_generation") &&
                receipt.LedgerRevision.ToString(CultureInfo.InvariantCulture) == DbMeta(path, "ledger_revision") &&
                receipt.ContentRevision.ToString(CultureInfo.InvariantCulture) == DbMeta(path, "content_revision") &&
                receipt.PhysicsIdentity == DbMeta(path, "physics_identity"), label + " matches independent committed state_meta facts");
        }

        static SqliteStateOwnerReceipt ReadReceipt(string path) => SqliteStateStore.ReadState(path).OwnerReceipt;

        static PlayerProfile SeedOwnedCarFixture(PlayerProfile current)
        {
            var authored = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            if (authored == null || authored.Length == 0) throw new InvalidOperationException("S06b requires authored vehicle definitions.");
            var catalog = new MechanicalCatalog(authored.Select(vehicle => vehicle.spec));
            var profile = PlayerProfile.New(DateTime.UtcNow.Ticks - TimeSpan.TicksPerDay);
            if (profile.SelectedCar == null) throw new InvalidOperationException("The disposable authored fixture must begin with one owned car.");
            profile.SelectedCar.vehicleId = profile.selectedVehicleId = authored[0].spec.id;
            MechanicsOwnership.Ensure(profile, catalog);
            profile.catalog = catalog;
            profile.cash = 6000;
            var fluids = profile.SelectedCar.fluids;
            var capacity = catalog.Spec(profile.SelectedCar.vehicleId).fluids;
            fluids.oilLitres = Math.Min(2.1, capacity.oilCapacityLitres * .55);
            fluids.coolantLitres = Math.Min(1.4, capacity.coolantCapacityLitres * .5);
            fluids.oilTemperatureC = 91.25; fluids.oilPressureBar = 3.75; fluids.coolantPressureBar = 1.25;
            fluids.oilCondition = .63; fluids.oilConsumedLitres = .12; fluids.oilLeakedLitres = .03;
            fluids.coolantLostLitres = .04; fluids.serviceElapsedSeconds = 18.5;
            fluids.oilBurnLps = .0001; fluids.oilLeakLps = .00002; fluids.coolantLeakLps = .00003; fluids.coolantVentLps = .00004;
            profile.revision = current.revision;
            profile.storageCommit = current.storageCommit;
            string invalid = MechanicsOwnership.Validate(profile, catalog);
            if (invalid != null) throw new InvalidOperationException("Invalid disposable owned-car fixture: " + invalid);
            if (!ProfileStore.TrySave(profile)) throw new IOException("Could not persist disposable owned-car fixture: " + ProfileStore.Status);
            var loaded = ProfileStore.Load();
            if (!ProfileStore.TryGetCommandOwner(loaded, out _) || loaded.SelectedCar == null)
                throw new InvalidOperationException("The persisted owned-car fixture did not receive a validated selected owner proof.");
            return loaded;
        }

        static TypedFluidRefillCommand MakeCommand(PlayerProfile profile, TypedSaveSlotBinding binding,
            string physicsIdentity, long acceptedTick, string operationId = "s06b-owner-check")
        {
            // Use the coordinator's single admission tick throughout planning and storage.
            // A second wall-clock read here could cross acceptedTick and make a valid command stale.
            long now = acceptedTick;
            var policy = new OperatingPolicy { oilPerLitre = 1, coolantPerLitre = 1, quoteSeconds = 300 };
            long issued = now - TimeSpan.TicksPerMinute;
            var request = new FluidRefillRequest(new StateCommandIdentity(binding.SaveSlot, binding.ResetGeneration, operationId),
                new StateCommandVersionFence(profile.revision, profile.storageCommit, MechanicalCatalog.Version),
                profile.SelectedCar.instanceId, .001, .001, issued, checked(issued + TimeSpan.FromSeconds(policy.quoteSeconds).Ticks));
            var input = FluidRefillPlanner.Capture(profile, policy, request, now);
            var plan = FluidRefillPlanner.Plan(input);
            if (!plan.Committable) throw new InvalidOperationException("The disposable fluid command fixture was not committable: " + plan.Error);
            return TypedFluidRefillCommand.Create(binding, plan, physicsIdentity, now);
        }

        static string DurableFingerprint(string path)
        {
            using (var db = new SqliteNative(path, true))
            {
                var result = new List<string>();
                foreach (string table in db.Query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name").Select(row => row[0]))
                {
                    result.Add("[" + table + "]");
                    foreach (var row in db.Query("SELECT * FROM \"" + table + "\"")) result.Add(string.Join("\u001f", row));
                }
                return string.Join("\n", result);
            }
        }

        static void VerifyLoadAndReset(string primary, Action<bool, string> check)
        {
            var profile = ProfileStore.Load();
            Check(ProfileStore.TryGetCommandOwner(profile, out var binding, out var newReceipt), "new-game save registers exact selected owner");
            Check(binding.CanonicalPath == Path.GetFullPath(primary) && binding.SaveSlot == Slot(), "binding uses canonical selected path and actual slot");
            CompareReceipt(primary, newReceipt, "new-game owner receipt");
            CompareReceipt(primary, ReadReceipt(primary), "new-game validated load receipt");
            string beforeFailedReset = DurableFingerprint(primary);
            SqliteStateStore.FailureInjection = label => { if (label == "before-commit") throw new IOException("Injected S06b pre-COMMIT reset failure."); };
            try { Check(!ProfileStore.TryReset(profile, out _) && DurableFingerprint(primary) == beforeFailedReset,
                "pre-COMMIT reset failure preserves all durable metadata and rows"); }
            finally { SqliteStateStore.FailureInjection = null; }
            Check(!ProfileStore.TryGetCommandOwner(profile, out _), "failed reset invalidates unverified owner proof");
            profile = ProfileStore.Load();
            string beforeGeneration = DbMeta(primary, "reset_generation");
            if (!ProfileStore.TryReset(profile, out var reset)) throw new IOException("Isolated owner reset failed: " + ProfileStore.Status);
            Check(long.Parse(DbMeta(primary, "reset_generation"), CultureInfo.InvariantCulture) ==
                checked(long.Parse(beforeGeneration, CultureInfo.InvariantCulture) + 1), "reset advances durable generation exactly once");
            Check(ProfileStore.TryGetCommandOwner(reset, out var resetBinding, out var resetReceipt) && resetBinding.ResetGeneration.ToString(CultureInfo.InvariantCulture) == DbMeta(primary, "reset_generation"),
                "successful reset binds only its actual committed generation");
            CompareReceipt(primary, resetReceipt, "reset owner receipt");
            CompareReceipt(primary, ReadReceipt(primary), "reset validated load receipt");
            SqliteStateStore.DrainBackups();
            byte[] backup = File.ReadAllBytes(primary + ".bak");
            File.WriteAllText(primary, "incompatible primary fixture", new System.Text.UTF8Encoding(false));
            var recovered = ProfileStore.Load();
            bool recoveredOwner = ProfileStore.TryGetCommandOwner(recovered, out var recoveredBinding, out var recoveredReceipt);
            Check(File.Exists(primary) && SqliteStateStore.IsDatabase(primary) && recoveredOwner,
                "validated backup promotion registers against the recovered primary path");
            Check(recoveredBinding.CanonicalPath == Path.GetFullPath(primary), "backup proof is rebound to primary path after promotion");
            Check(backup.Length > 0, "disposable backup fixture existed before promotion");
            CompareReceipt(primary, recoveredReceipt, "promoted backup owner receipt");
            CompareReceipt(primary, ReadReceipt(primary), "promoted backup validated load receipt");
        }

        static void VerifyAdmissionFences(string path, PlayerProfile profile)
        {
            string badCheckpoint = UnityEngine.JsonUtility.ToJson(new RunCheckpoint { version = -1, runId = "s06b-invalid-checkpoint" });
            SqliteStateStore.WriteCheckpoint(path, "s06b-invalid-checkpoint", badCheckpoint);
            string beforeInvalidMerge = DurableFingerprint(path);
            int expectedCash = profile.cash;
            long expectedRevision = profile.revision;
            double expectedOil = profile.SelectedCar.fluids.oilLitres;
            var invalidCheckpointLoad = ProfileStore.Load();
            Check(!ProfileStore.TryGetCommandOwner(invalidCheckpointLoad, out _),
                "invalid pending checkpoint warning prevents owner proof registration");
            Check(SqliteStateStore.Checkpoints(path).SequenceEqual(new[] { badCheckpoint }) &&
                DurableFingerprint(path) == beforeInvalidMerge && invalidCheckpointLoad.cash == expectedCash &&
                invalidCheckpointLoad.revision == expectedRevision &&
                BitConverter.DoubleToInt64Bits(invalidCheckpointLoad.SelectedCar.fluids.oilLitres) == BitConverter.DoubleToInt64Bits(expectedOil),
                "invalid checkpoint bytes and profile resources remain unchanged without healing or consumption");
            SqliteStateStore.ClearCheckpoints(path);
            profile = ProfileStore.Load();
            Check(ProfileStore.TryGetCommandOwner(profile, out _), "clearing invalid checkpoint allows a fresh validated owner registration");
            var catalog = MechanicsTransactions.Catalog();
            var capacity = catalog.Spec(profile.SelectedCar.vehicleId).fluids;
            profile.SelectedCar.fluids.oilLitres = Math.Min(profile.SelectedCar.fluids.oilLitres, capacity.oilCapacityLitres * .5);
            profile.SelectedCar.fluids.coolantLitres = Math.Min(profile.SelectedCar.fluids.coolantLitres, capacity.coolantCapacityLitres * .5);
            Check(ProfileStore.TrySave(profile), "isolated owner fixture persists spare fluid capacity before command admission");
            Check(ProfileStore.TryGetCommandOwner(profile, out var binding, out var ownerReceipt), "validated load retains cached owner proof");
            var scheduler = new HeldScheduler();
            var coordinator = new TypedFluidRefillCoordinator(new UnknownExecutor(), scheduler);
            bool factoryRan = false;
            long admissionTick = Math.Max(profile.lastEconomyUtcTicks + 10, DateTime.UtcNow.Ticks);
            Check(ProfileStore.TryBeginTypedOperation(profile, coordinator, now =>
            {
                factoryRan = true;
                return MakeCommand(profile, binding, ownerReceipt.PhysicsIdentity, now);
            }, admissionTick, out var lease, out var error),
                "selected owner acquires capacity before factory and retains a typed operation: " + error);
            Check(factoryRan && coordinator.HasPending, "typed command factory runs only after capacity and owner lease are acquired");
            string before = DurableFingerprint(path);
            int cash = profile.cash;
            Check(!ProfileStore.TrySave(profile) && profile.cash == cash && DurableFingerprint(path) == before,
                "legacy save refuses before profile mutation and SQL while typed lease is retained");
            bool beginSaveRefused = false;
            try { ProfileStore.BeginValidatedSave(profile); } catch (InvalidOperationException) { beginSaveRefused = true; }
            Check(beginSaveRefused && DurableFingerprint(path) == before,
                "asynchronous legacy BeginSave refuses before checkpoint merge or capture while typed lease is retained");
            bool loadRefused = false;
            try { ProfileStore.Load(); } catch (InvalidOperationException) { loadRefused = true; }
            Check(loadRefused && DurableFingerprint(path) == before,
                "selected load refuses before lifecycle work while typed lease is retained");
            Check(!ProfileStore.TryReset(profile, out _) && DurableFingerprint(path) == before,
                "reset refuses before transaction while typed lease is retained");
            SetWriteBlocked(true);
            bool archiveRefused = !ProfileStore.TryResetIncompatibleSave(out _);
            SetWriteBlocked(false);
            Check(archiveRefused && DurableFingerprint(path) == before, "incompatible archive/reset refuses before moving selected save while typed lease is retained");
            bool slotRefused = false;
            try { ProfileStore.UseSlot("s06b-other"); } catch (InvalidOperationException) { slotRefused = true; }
            Check(slotRefused && DurableFingerprint(path) == before, "slot change refuses before changing selected database");
            Check(!ProfileStore.TryCheckpoint(profile, new RunCheckpoint(), out _) && DurableFingerprint(path) == before,
                "checkpoint submission refuses before database work while typed lease is retained");
            Check(!ProfileStore.TryGetCommandOwner(profile, out _), "a leased owner cannot admit a second typed operation");
            Check(ProfileStore.TrySavePreferences(profile, out _), "separate preferences save remains available in the isolated fixture root");
            Check(!ProfileStore.ReleaseTypedLease(lease), "early release is refused while coordinator retains an outcome");
            Check(scheduler.Callback != null && coordinator.PendingState == TypedFluidRefillCoordinatorState.CommitInFlight,
                "delayed worker remains retained before callback dispatch");
            scheduler.Run();
            coordinator.Poll();
            Check(coordinator.PendingState == TypedFluidRefillCoordinatorState.OutcomeUnknown && coordinator.HasPending,
                "unknown worker outcome remains retained after nonblocking poll");
            Check(!ProfileStore.ReleaseTypedLease(lease), "unknown durable outcome cannot release the owner lease");
            Check(coordinator.TryReconcile(out _) && scheduler.Callback != null, "unknown outcome dispatches bounded reconciliation");
            scheduler.Run();
            coordinator.Poll();
            Check(coordinator.PendingState == TypedFluidRefillCoordinatorState.Terminal && coordinator.HasPending,
                "reconciled terminal absence remains retained until coordinator acknowledgement");
            Check(coordinator.Acknowledge(false, out _) && !coordinator.HasPending, "definite no-commit result can be acknowledged");
            Check(ProfileStore.ReleaseTypedLease(lease), "same lease releases only after coordinator acknowledgement");
            var reloaded = ProfileStore.Load();
            Check(reloaded != null && ProfileStore.TryGetCommandOwner(reloaded, out _), "owner binding is reusable after acknowledged capacity release");
            var successorScheduler = new HeldScheduler();
            var successorCoordinator = new TypedFluidRefillCoordinator(new UnknownExecutor(), successorScheduler);
            Check(ProfileStore.TryGetCommandOwner(reloaded, out var successorBinding, out var successorReceipt), "successor owner can be inspected after release");
            long successorAdmissionTick = Math.Max(reloaded.lastEconomyUtcTicks + 10, DateTime.UtcNow.Ticks);
            Check(ProfileStore.TryBeginTypedOperation(reloaded, successorCoordinator,
                now => MakeCommand(reloaded, successorBinding, successorReceipt.PhysicsIdentity, now, "s06b-successor"),
                successorAdmissionTick, out var successorLease, out _),
                "a later lease can be acquired after completed acknowledgement");
            Check(!ProfileStore.ReleaseTypedLease(lease), "stale lease cannot release a later owner's capacity reservation");
            successorScheduler.Run(); successorCoordinator.Poll();
            Check(successorCoordinator.TryReconcile(out _), "successor unknown operation remains explicitly reconcilable");
            successorScheduler.Run(); successorCoordinator.Poll();
            Check(successorCoordinator.Acknowledge(false, out _) && ProfileStore.ReleaseTypedLease(successorLease),
                "successor lease releases only after its coordinator acknowledgement");
            var durableProfile = ProfileStore.Load();
            Check(ProfileStore.TryGetCommandOwner(durableProfile, out var durableBinding, out var durableReceipt), "fresh load registers before committed-but-unhandled lease scenario");
            var durableScheduler = new HeldScheduler();
            var durableCoordinator = new TypedFluidRefillCoordinator(new SqliteTypedFluidRefillExecutor(), durableScheduler);
            long durableAdmissionTick = Math.Max(durableProfile.lastEconomyUtcTicks + 10, DateTime.UtcNow.Ticks);
            long durableFactoryTick = 0;
            bool durableBegan = ProfileStore.TryBeginTypedOperation(durableProfile, durableCoordinator, now =>
            {
                durableFactoryTick = now;
                return MakeCommand(durableProfile, durableBinding, durableReceipt.PhysicsIdentity, now, "s06b-owner-committed");
            }, durableAdmissionTick, out var durableLease, out var durableBeginError);
            Check(durableBegan, "actual typed SQLite commit begins under the selected-owner lease: " + durableBeginError +
                " / admission tick " + durableAdmissionTick.ToString(CultureInfo.InvariantCulture) +
                " / factory tick " + durableFactoryTick.ToString(CultureInfo.InvariantCulture));
            durableScheduler.Run(); durableCoordinator.Poll();
            var durableOutcome = durableCoordinator.PendingOutcome;
            string durableOutcomeDiagnostic = "writer outcome=" + (durableOutcome?.Status.ToString() ?? "missing") +
                ", error=" + (durableOutcome?.Error ?? "none") +
                ", admission tick=" + durableAdmissionTick.ToString(CultureInfo.InvariantCulture) +
                ", factory tick=" + durableFactoryTick.ToString(CultureInfo.InvariantCulture) +
                ", accepted tick=" + (durableOutcome?.Result?.AcceptedTick.ToString(CultureInfo.InvariantCulture) ?? "missing");
            Check(durableCoordinator.PendingState == TypedFluidRefillCoordinatorState.CommittedAwaitingHandling,
                "committed typed result remains retained until owner handling (" + durableOutcomeDiagnostic + ")");
            Check(!durableCoordinator.Acknowledge(false, out _) && !ProfileStore.ReleaseTypedLease(durableLease),
                "committed-but-unhandled durable result cannot be acknowledged away or release its lease");
            string beforeRejectedSave = DurableFingerprint(path);
            Check(!ProfileStore.TrySave(durableProfile) && DurableFingerprint(path) == beforeRejectedSave,
                "legacy save remains fenced while a committed typed result awaits owner handling");
            Check(durableCoordinator.Acknowledge(true, out _) && ProfileStore.ReleaseTypedLease(durableLease),
                "durable result lease releases after explicit owner acknowledgement");
            var durableReload = ProfileStore.Load();
            Check(ProfileStore.TryGetCommandOwner(durableReload, out _) && durableOutcome?.Result != null &&
                durableReload.cash == durableOutcome.Result.CashAfter,
                "cold validated reload observes the independently committed typed effect after lease release");
            ProfileStore.MarkSelectedCheckpointDirty();
            Check(!ProfileStore.TryGetCommandOwner(durableReload, out _), "unverified dirty checkpoint invalidates cached owner proof");
            ProfileStore.MarkSelectedCheckpointVerifiedClean();
            var clean = ProfileStore.Load();
            Check(ProfileStore.TryGetCommandOwner(clean, out _), "verified clean checkpoint permits a fresh load registration");
        }

        static void VerifyAsyncLegacySave(string path, PlayerProfile candidate)
        {
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            {
                SqliteStateStore.FailureInjection = label =>
                {
                    if (label != "before-commit") return;
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("S06b bounded test writer release expired.");
                };
                Task<ProfileStore.ValidatedSaveResult> save = null;
                bool assertionsFinished = false;
                try
                {
                    save = ProfileStore.BeginValidatedSave(candidate);
                    Check(entered.Wait(TimeSpan.FromSeconds(5)), "validated legacy save reaches its held pre-COMMIT test boundary");
                    Check(!ProfileStore.TryGetCommandOwner(candidate, out _), "typed admission is refused while asynchronous legacy save ticket is retained");
                    bool selectionRefused = false;
                    try { ProfileStore.UseSlot("s06b-pending-slot"); } catch (InvalidOperationException) { selectionRefused = true; }
                    Check(selectionRefused, "slot selection is refused from Begin through Complete of legacy save");
                    Check(!ProfileStore.TrySave(candidate), "parallel legacy save is refused while a validated ticket is retained");
                    assertionsFinished = true;
                }
                finally
                {
                    release.Set();
                    SqliteStateStore.FailureInjection = null;
                    if (save != null && save.Wait(TimeSpan.FromSeconds(5)))
                    {
                        try { ProfileStore.CompleteValidatedSave(candidate, save); }
                        catch when (!assertionsFinished) { }
                    }
                }
                if (save == null || !save.IsCompleted) throw new TimeoutException("S06b legacy writer did not finish within the bounded wait.");
                Check(candidate.storageCommit.ToString(CultureInfo.InvariantCulture) == DbMeta(path, "commit_id") &&
                    ProfileStore.TryGetCommandOwner(candidate, out _), "completion publishes the captured candidate and actual durable receipt");
            }
        }

        static void VerifyAsyncLegacyFailure(string path, PlayerProfile candidate)
        {
            string before = DurableFingerprint(path);
            SqliteStateStore.FailureInjection = label =>
            {
                if (label == "before-commit") throw new IOException("Injected S06b legacy pre-COMMIT failure.");
            };
            Task<ProfileStore.ValidatedSaveResult> save = null;
            try
            {
                save = ProfileStore.BeginValidatedSave(candidate);
                bool finished;
                try { finished = save.Wait(TimeSpan.FromSeconds(5)); }
                catch (AggregateException) { finished = true; }
                if (!finished) throw new TimeoutException("S06b failed legacy writer did not finish within its bounded wait.");
                bool completionFailed = false;
                try { ProfileStore.CompleteValidatedSave(candidate, save); }
                catch { completionFailed = true; }
                Check(completionFailed && DurableFingerprint(path) == before,
                    "pre-COMMIT async failure preserves durable state and clears the completion ticket");
                Check(!ProfileStore.TryGetCommandOwner(candidate, out _), "failed async persistence invalidates selected owner proof");
            }
            finally
            {
                SqliteStateStore.FailureInjection = null;
                if (save != null && save.IsCompleted)
                {
                    try { ProfileStore.CompleteValidatedSave(candidate, save); }
                    catch { }
                }
            }
            var reloaded = ProfileStore.Load();
            Check(ProfileStore.TryGetCommandOwner(reloaded, out _), "validated reload re-establishes owner after definite pre-COMMIT failure");
        }

        static void VerifyAsyncDirtyCandidate(string path, PlayerProfile candidate)
        {
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            {
                SqliteStateStore.FailureInjection = label =>
                {
                    if (label != "before-commit") return;
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("S06b dirty-candidate writer release expired.");
                };
                Task<ProfileStore.ValidatedSaveResult> save = null;
                bool completed = false;
                int savedCash = candidate.cash;
                try
                {
                    save = ProfileStore.BeginValidatedSave(candidate);
                    Check(entered.Wait(TimeSpan.FromSeconds(5)), "dirty-candidate save reaches bounded held writer boundary");
                    candidate.cash = checked(savedCash + 1);
                    release.Set();
                    SqliteStateStore.FailureInjection = null;
                    if (!save.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("S06b dirty-candidate writer did not finish within its bounded wait.");
                    ProfileStore.CompleteValidatedSave(candidate, save);
                    completed = true;
                    Check(!ProfileStore.TryGetCommandOwner(candidate, out _), "candidate changed after capture cannot receive clean typed owner proof");
                    var durable = ProfileStore.Load();
                    Check(durable.cash == savedCash && candidate.cash == savedCash + 1 &&
                        durable.storageCommit.ToString(CultureInfo.InvariantCulture) == DbMeta(path, "commit_id"),
                        "completion preserves the captured durable profile while leaving later candidate edits unbound");
                }
                finally
                {
                    release.Set();
                    SqliteStateStore.FailureInjection = null;
                    if (!completed && save != null && save.IsCompleted)
                    {
                        try { ProfileStore.CompleteValidatedSave(candidate, save); }
                        catch { }
                    }
                }
            }
        }

        static void VerifyPostCommitDiagnostics(string path, PlayerProfile profile)
        {
            string before = DbMeta(path, "commit_id");
            SqliteStateStore.Measurement = (phase, _) =>
            {
                if (phase == "durable commit") throw new IOException("Injected S06b post-COMMIT diagnostic failure.");
            };
            try
            {
                Check(ProfileStore.TrySave(profile), "post-COMMIT diagnostic observer failure does not report a durable write as rolled back");
            }
            finally { SqliteStateStore.Measurement = null; }
            Check(long.Parse(DbMeta(path, "commit_id"), CultureInfo.InvariantCulture) == long.Parse(before, CultureInfo.InvariantCulture) + 1,
                "post-COMMIT observer failure leaves exactly one durable commit");
            Check(ProfileStore.TryGetCommandOwner(profile, out _), "post-COMMIT owner registration retains truthful committed metadata");
            CompareReceipt(path, ReadReceipt(path), "post-COMMIT observer receipt");
        }

        static void VerifyCapturedPathCompletion(string path, string alternatePath, PlayerProfile candidate)
        {
            using (var entered = new ManualResetEventSlim(false))
            using (var release = new ManualResetEventSlim(false))
            {
                SqliteStateStore.FailureInjection = label =>
                {
                    if (label != "before-commit") return;
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("S06b captured-path writer release expired.");
                };
                Task<ProfileStore.ValidatedSaveResult> save = null;
                string originalVerificationPath = ProfileStore.VerificationPath;
                bool completed = false;
                try
                {
                    save = ProfileStore.BeginValidatedSave(candidate);
                    Check(entered.Wait(TimeSpan.FromSeconds(5)), "captured-path save reaches bounded held writer boundary");
                    bool pathChangeRefused = false;
                    try { ProfileStore.VerificationPath = alternatePath; } catch (InvalidOperationException) { pathChangeRefused = true; }
                    Check(pathChangeRefused && ProfileStore.VerificationPath == originalVerificationPath,
                        "selected verification path cannot change from Begin through Complete");
                    release.Set();
                    SqliteStateStore.FailureInjection = null;
                    if (!save.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("S06b captured-path save did not finish within its bounded wait.");
                    ProfileStore.CompleteValidatedSave(candidate, save);
                    completed = true;
                    Check(candidate.storageCommit.ToString(CultureInfo.InvariantCulture) == DbMeta(path, "commit_id") &&
                        ProfileStore.TryGetCommandOwner(candidate, out _), "completion publishes only the captured path receipt");
                    Check(!File.Exists(alternatePath), "refused path retarget does not create a second selected database");
                }
                finally
                {
                    release.Set();
                    SqliteStateStore.FailureInjection = null;
                    if (ProfileStore.VerificationPath != originalVerificationPath)
                    {
                        try { ProfileStore.VerificationPath = originalVerificationPath; } catch { }
                    }
                    if (!completed && save != null && save.IsCompleted)
                    {
                        try { ProfileStore.CompleteValidatedSave(candidate, save); }
                        catch { }
                    }
                }
            }
        }

        public static void Run(Action<bool, string> check, string outputDirectory)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("An isolated S06b output directory is required.", nameof(outputDirectory));
            passed = 0; report = check;
            string priorPath = ProfileStore.VerificationPath;
            string priorSlot = Slot();
            bool priorWriteBlocked = ProfileStore.ResetRequired;
            bool priorCheckpointDirty = GetStaticBool(typeof(ProfileStore), "selectedCheckpointDirty");
            object priorStatus = typeof(ProfileStore).GetField("status", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var priorFailureInjection = SqliteStateStore.FailureInjection;
            var priorMeasurement = SqliteStateStore.Measurement;
            string root = Path.Combine(Path.GetFullPath(outputDirectory), "s06b-owner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string primary = Path.Combine(root, "selected.sqlite3");
            try
            {
                ProfileStore.VerificationPath = primary;
                ProfileStore.UseSlot("s06b-owner");
                VerifyLoadAndReset(primary, check);
                var loaded = ProfileStore.Load();
                Check(ProfileStore.TryGetCommandOwner(loaded, out _), "ordinary validated load re-registers durable owner");
                var copy = new PlayerProfile();
                Check(!ProfileStore.TryGetCommandOwner(copy, out _), "equal but distinct profile object cannot reuse selected owner proof");
                Check(ProfileStore.TryGetCommandOwner(loaded, out _), "failed reference comparison does not evict the actual selected profile proof");
                long revision = loaded.revision;
                loaded.revision = checked(revision + 1);
                Check(!ProfileStore.TryGetCommandOwner(loaded, out _), "changed profile revision invalidates cached owner without SQL");
                loaded.revision = revision;
                var current = ProfileStore.Load();
                var ownedFixture = SeedOwnedCarFixture(current);
                VerifyAdmissionFences(primary, ownedFixture);
                VerifyAsyncLegacySave(primary, ProfileStore.Load());
                VerifyAsyncLegacyFailure(primary, ProfileStore.Load());
                VerifyAsyncDirtyCandidate(primary, ProfileStore.Load());
                var afterLegacy = ProfileStore.Load();
                VerifyPostCommitDiagnostics(primary, afterLegacy);
                VerifyCapturedPathCompletion(primary, Path.Combine(root, "retargeted.sqlite3"), ProfileStore.Load());
                var afterRetarget = ProfileStore.Load();
                Check(ProfileStore.TryGetCommandOwner(afterRetarget, out _), "fresh validated load recovers owner after stale-path completion");
                string selectedPath = ProfileStore.VerificationPath;
                ProfileStore.VerificationPath = Path.Combine(root, "legacy-profile.json");
                Check(!ProfileStore.TryGetCommandOwner(afterLegacy, out _), "legacy JSON fixture path cannot establish a typed owner binding");
                ProfileStore.VerificationPath = selectedPath;
                ProfileStore.UseSlot("s06b-another-slot");
                Check(!ProfileStore.TryGetCommandOwner(afterLegacy, out _), "slot and selection-epoch changes invalidate an old owner binding");
                ProfileStore.UseSlot("s06b-owner");
                afterLegacy = ProfileStore.Load();
                Check(ProfileStore.TryGetCommandOwner(afterLegacy, out _), "current slot can acquire a newly validated owner proof");
                string corrupt = Path.Combine(root, "incompatible.sqlite3");
                ProfileStore.VerificationPath = corrupt;
                File.WriteAllText(corrupt, "not a supported SQLite save", new System.Text.UTF8Encoding(false));
                var unsupported = ProfileStore.Load();
                Check(!ProfileStore.TryGetCommandOwner(unsupported, out _) && File.ReadAllText(corrupt, System.Text.Encoding.UTF8) == "not a supported SQLite save",
                    "incompatible durable input remains untouched and does not register the in-memory fallback");
                string invalidMetadata = Path.Combine(root, "invalid-content-revision.sqlite3");
                File.Copy(primary, invalidMetadata);
                using (var db = new SqliteNative(invalidMetadata))
                    db.Exec("UPDATE state_meta SET value='not-a-number' WHERE name='content_revision'");
                ProfileStore.VerificationPath = invalidMetadata;
                var invalidRevision = ProfileStore.Load();
                Check(!ProfileStore.TryGetCommandOwner(invalidRevision, out _), "invalid numeric content metadata is rejected without owner proof");
                ProfileStore.VerificationPath = selectedPath;
                UnityEngine.Debug.Log("S06b selected-profile owner checks passed: " + passed.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                DrainPendingSaveForCleanup();
                SetStaticField(typeof(ProfileStore), "typedOwnerLease", null);
                ProfileStore.InvalidateOwnerProof();
                if (ProfileStore.VerificationPath != priorPath) ProfileStore.VerificationPath = priorPath;
                if (Slot() != priorSlot) ProfileStore.UseSlot(priorSlot);
                SetWriteBlocked(priorWriteBlocked);
                SetStaticField(typeof(ProfileStore), "selectedCheckpointDirty", priorCheckpointDirty);
                SetStaticField(typeof(ProfileStore), "status", priorStatus);
                SqliteStateStore.FailureInjection = priorFailureInjection;
                SqliteStateStore.Measurement = priorMeasurement;
                SqliteStateStore.Invalidate();
                report = null;
            }
        }
    }
}
#endif
