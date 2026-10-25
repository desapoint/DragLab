using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    /// <summary>Owner capability binding a command to one selected save identity.</summary>
    internal sealed class TypedSaveSlotBinding : IEquatable<TypedSaveSlotBinding>
    {
        static readonly StringComparison PathComparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        internal readonly string CanonicalPath;
        internal readonly string SaveSlot;
        internal readonly long ResetGeneration;

        internal TypedSaveSlotBinding(string canonicalPath, string saveSlot, long resetGeneration)
        {
            if (string.IsNullOrWhiteSpace(canonicalPath) || !Path.IsPathFullyQualified(canonicalPath))
                throw new ArgumentException("A selected absolute save path is required.", nameof(canonicalPath));
            if (string.IsNullOrWhiteSpace(saveSlot)) throw new ArgumentException("A selected save slot is required.", nameof(saveSlot));
            if (resetGeneration < 0 || resetGeneration == long.MaxValue) throw new ArgumentOutOfRangeException(nameof(resetGeneration));
            CanonicalPath = Path.GetFullPath(canonicalPath);
            SaveSlot = saveSlot;
            ResetGeneration = resetGeneration;
            // Reuse the identity contract's slot syntax/length validation.
            _ = new StateCommandIdentity(SaveSlot, ResetGeneration, "binding-validation");
        }

        internal bool Matches(TypedSaveSlotBinding other)
        {
            return other != null && ResetGeneration == other.ResetGeneration &&
                string.Equals(SaveSlot, other.SaveSlot, StringComparison.Ordinal) &&
                string.Equals(CanonicalPath, other.CanonicalPath, PathComparison);
        }

        public bool Equals(TypedSaveSlotBinding other) => Matches(other);
        public override bool Equals(object obj) => Equals(obj as TypedSaveSlotBinding);
        public override int GetHashCode()
        {
            unchecked
            {
                int pathHash = PathComparison == StringComparison.OrdinalIgnoreCase
                    ? StringComparer.OrdinalIgnoreCase.GetHashCode(CanonicalPath)
                    : StringComparer.Ordinal.GetHashCode(CanonicalPath);
                return ((pathHash * 397) ^ StringComparer.Ordinal.GetHashCode(SaveSlot)) * 397 ^ ResetGeneration.GetHashCode();
            }
        }
    }

    /// <summary>Stable operation identity and canonical intent, independent of planner state.</summary>
    internal sealed class TypedFluidRefillIntent
    {
        const string IntentDomain = "fluid-refill-intent/v1";
        const string CommandKind = "FluidTopUp";
        readonly byte[] intentBytes;
        readonly byte[] intentSha256;

        internal readonly TypedSaveSlotBinding ExpectedBinding;
        internal readonly FluidRefillRequest Request;
        internal readonly string PhysicsIdentity;
        internal readonly long AcceptedTick;
        internal readonly string IntentHex;

        internal byte[] IntentBytes => (byte[])intentBytes.Clone();
        internal byte[] IntentSha256 => (byte[])intentSha256.Clone();

        TypedFluidRefillIntent(TypedSaveSlotBinding expectedBinding, FluidRefillRequest request,
            string physicsIdentity, long acceptedTick, byte[] canonicalIntent)
        {
            ExpectedBinding = expectedBinding;
            Request = request;
            PhysicsIdentity = physicsIdentity;
            AcceptedTick = acceptedTick;
            intentBytes = canonicalIntent;
            intentSha256 = Hash(canonicalIntent);
            IntentHex = ToHex(canonicalIntent);
        }

        internal static TypedFluidRefillIntent Create(TypedSaveSlotBinding expectedBinding,
            FluidRefillRequest request, string physicsIdentity, long acceptedTick)
        {
            if (expectedBinding == null) throw new ArgumentNullException(nameof(expectedBinding));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!string.Equals(request.Identity.SaveSlot, expectedBinding.SaveSlot, StringComparison.Ordinal) ||
                request.Identity.ResetGeneration != expectedBinding.ResetGeneration)
                throw new ArgumentException("Request identity does not match the owner-confirmed save binding.", nameof(request));
            if (!StateCommandIdentity.ValidToken(physicsIdentity, 192)) throw new ArgumentException("A valid owner-supplied physics identity is required.", nameof(physicsIdentity));
            if (acceptedTick <= 0 || acceptedTick > DateTime.MaxValue.Ticks)
                throw new ArgumentOutOfRangeException(nameof(acceptedTick));
            if (request.IssuedUtcTicks <= 0 || request.ExpiresUtcTicks <= request.IssuedUtcTicks ||
                acceptedTick < request.IssuedUtcTicks || acceptedTick >= request.ExpiresUtcTicks ||
                string.IsNullOrEmpty(request.CarId) ||
                !MechanicsSchema.Finite(request.RequestedOilLitres) || request.RequestedOilLitres < 0 ||
                !MechanicsSchema.Finite(request.RequestedCoolantLitres) || request.RequestedCoolantLitres < 0)
                throw new ArgumentException("Immutable request identity, quote timing, or quantities are invalid.", nameof(request));

            var bytes = EncodeIntent(request, physicsIdentity, acceptedTick);
            if (bytes.Length > 2048) throw new ArgumentException("Canonical intent exceeds the 2048-byte limit.", nameof(request));
            return new TypedFluidRefillIntent(expectedBinding, request, physicsIdentity, acceptedTick, bytes);
        }

        internal bool MatchesActualBinding(TypedSaveSlotBinding actualBinding) => ExpectedBinding.Matches(actualBinding);

        static byte[] EncodeIntent(FluidRefillRequest request, string physicsIdentity, long acceptedTick)
        {
            using (var stream = new MemoryStream())
            {
                WriteText(stream, IntentDomain);
                WriteStringField(stream, "save_slot", request.Identity.SaveSlot);
                WriteIntField(stream, "reset_generation", request.Identity.ResetGeneration);
                WriteStringField(stream, "operation_id", request.Identity.OperationId);
                WriteStringField(stream, "command_kind", CommandKind);
                WriteIntField(stream, "expected_ledger_revision", request.Versions.ExpectedLedgerRevision);
                WriteIntField(stream, "expected_storage_commit", request.Versions.ExpectedStorageCommit);
                WriteIntField(stream, "content_revision", request.Versions.ExpectedContentVersion);
                WriteStringField(stream, "physics_identity", physicsIdentity);
                WriteStringField(stream, "car_id", request.CarId);
                WriteIntField(stream, "requested_oil_bits", BitConverter.DoubleToInt64Bits(request.RequestedOilLitres));
                WriteIntField(stream, "requested_coolant_bits", BitConverter.DoubleToInt64Bits(request.RequestedCoolantLitres));
                WriteIntField(stream, "quote_issue_tick", request.IssuedUtcTicks);
                WriteIntField(stream, "quote_expiry_tick", request.ExpiresUtcTicks);
                WriteIntField(stream, "accepted_confirmation_tick", acceptedTick);
                return stream.ToArray();
            }
        }

        static void WriteStringField(Stream stream, string name, string value)
        {
            WriteText(stream, name); stream.WriteByte(0x01); WriteText(stream, value);
        }

        static void WriteIntField(Stream stream, string name, long value)
        {
            WriteText(stream, name); stream.WriteByte(0x02); WriteInt64(stream, value);
        }

        static void WriteText(Stream stream, string value)
        {
            if (value == null) throw new ArgumentException("Canonical intent strings cannot be null.");
            byte[] utf8 = new UTF8Encoding(false, true).GetBytes(value);
            WriteUInt32(stream, checked((uint)utf8.Length));
            stream.Write(utf8, 0, utf8.Length);
        }

        static void WriteUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)value); stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16)); stream.WriteByte((byte)(value >> 24));
        }

        static void WriteInt64(Stream stream, long value)
        {
            ulong bits = unchecked((ulong)value);
            for (int shift = 0; shift < 64; shift += 8) stream.WriteByte((byte)(bits >> shift));
        }

        static byte[] Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return sha.ComputeHash(bytes);
        }

        static string ToHex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++) { chars[i * 2] = alphabet[bytes[i] >> 4]; chars[i * 2 + 1] = alphabet[bytes[i] & 15]; }
            return new string(chars);
        }

        internal static bool BitsEqual(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }

    /// <summary>Detached successful plan joined to the independently recoverable intent.</summary>
    internal sealed class TypedFluidRefillCommand
    {
        internal readonly TypedFluidRefillIntent Intent;
        internal readonly FluidRefillResult Plan;
        internal TypedSaveSlotBinding ExpectedBinding => Intent.ExpectedBinding;
        internal FluidRefillRequest Request => Intent.Request;
        internal FluidRefillReadSet ReadSet => Plan.ReadSet;
        internal string PhysicsIdentity => Intent.PhysicsIdentity;
        internal long AcceptedTick => Intent.AcceptedTick;
        internal string IntentHex => Intent.IntentHex;
        internal byte[] IntentBytes => Intent.IntentBytes;
        internal byte[] IntentSha256 => Intent.IntentSha256;
        internal string EvictedReceiptId => ReadSet.EconomyReceiptCount == 256 ? ReadSet.OldestEconomyReceiptId : null;

        TypedFluidRefillCommand(TypedFluidRefillIntent intent, FluidRefillResult plan)
        { Intent = intent; Plan = plan; }

        internal static TypedFluidRefillCommand Create(TypedSaveSlotBinding expectedBinding,
            FluidRefillResult successfulPlan, string physicsIdentity, long acceptedTick)
        {
            if (successfulPlan == null) throw new ArgumentNullException(nameof(successfulPlan));
            if (!successfulPlan.Committable) throw new ArgumentException("Only a successful fluid plan can become a storage command.", nameof(successfulPlan));
            if (successfulPlan.Request == null || successfulPlan.ReadSet == null || successfulPlan.Receipt == null ||
                successfulPlan.BeforeFluids == null || successfulPlan.AfterFluids == null)
                throw new ArgumentException("Fluid plan is incomplete.", nameof(successfulPlan));
            var intent = TypedFluidRefillIntent.Create(expectedBinding, successfulPlan.Request, physicsIdentity, acceptedTick);
            if (acceptedTick != successfulPlan.PlanningNowTicks || acceptedTick != successfulPlan.Receipt.UtcTicks)
                throw new ArgumentException("Accepted confirmation time must equal the successful plan and receipt time.", nameof(acceptedTick));
            if (!string.Equals(successfulPlan.ReadSet.CarId, intent.Request.CarId, StringComparison.Ordinal))
                throw new ArgumentException("Plan target does not match its immutable request.", nameof(successfulPlan));
            if (successfulPlan.ReadSet.EconomyReceiptCount < 0 || successfulPlan.ReadSet.EconomyReceiptCount > 256 ||
                (successfulPlan.ReadSet.EconomyReceiptCount == 0 && successfulPlan.ReadSet.OldestEconomyReceiptId != null) ||
                (successfulPlan.ReadSet.EconomyReceiptCount > 0 && string.IsNullOrEmpty(successfulPlan.ReadSet.OldestEconomyReceiptId)))
                throw new ArgumentException("Business receipt window observations are invalid.", nameof(successfulPlan));
            ValidatePlan(successfulPlan);
            return new TypedFluidRefillCommand(intent, successfulPlan);
        }

        internal bool MatchesActualBinding(TypedSaveSlotBinding actualBinding) => Intent.MatchesActualBinding(actualBinding);

        static void ValidatePlan(FluidRefillResult plan)
        {
            if (plan.ReadSet.Fluids == null || plan.CashBefore != plan.ReadSet.Cash ||
                !MechanicsSchema.Finite(plan.ReadSet.OilCapacityLitres) || plan.ReadSet.OilCapacityLitres < 0 ||
                !MechanicsSchema.Finite(plan.ReadSet.CoolantCapacityLitres) || plan.ReadSet.CoolantCapacityLitres < 0 ||
                !MechanicsSchema.Finite(plan.ReadSet.OilPerLitre) || plan.ReadSet.OilPerLitre < 0 ||
                !MechanicsSchema.Finite(plan.ReadSet.CoolantPerLitre) || plan.ReadSet.CoolantPerLitre < 0 ||
                !AllFluidValuesFinite(plan.ReadSet.Fluids) || !AllFluidValuesFinite(plan.AfterFluids) ||
                !MechanicsSchema.Finite(plan.RequestedOilLitres) || plan.RequestedOilLitres < 0 ||
                !MechanicsSchema.Finite(plan.RequestedCoolantLitres) || plan.RequestedCoolantLitres < 0 ||
                !MechanicsSchema.Finite(plan.OilAdded) || plan.OilAdded < 0 ||
                !MechanicsSchema.Finite(plan.CoolantAdded) || plan.CoolantAdded < 0 || plan.Cost < 0 ||
                plan.CashBefore < 0 || plan.CashAfter < 0 || plan.LastEconomyUtcTicksAfter != plan.PlanningNowTicks)
                throw new ArgumentException("Successful plan has invalid quantities or economy deltas.", nameof(plan));
            if (!plan.BeforeFluids.Equals(plan.ReadSet.Fluids) ||
                !BitsEqual(plan.AfterFluids.OilLitres, plan.BeforeFluids.OilLitres + plan.OilAdded) ||
                !BitsEqual(plan.AfterFluids.CoolantLitres, plan.BeforeFluids.CoolantLitres + plan.CoolantAdded))
                throw new ArgumentException("Successful plan fluid deltas are inconsistent.", nameof(plan));

            // Re-run the pure planner over its detached inputs. This validates rounded
            // cost, cash, receipt and all untouched fluid bits without a live profile.
            var recomputed = FluidRefillPlanner.Plan(new FluidRefillInput(plan.Request, plan.ReadSet, plan.PlanningNowTicks));
            if (!recomputed.Committable || recomputed.Cost != plan.Cost || recomputed.CashBefore != plan.CashBefore ||
                recomputed.CashAfter != plan.CashAfter || !BitsEqual(recomputed.OilAdded, plan.OilAdded) ||
                !BitsEqual(recomputed.CoolantAdded, plan.CoolantAdded) || !recomputed.AfterFluids.Equals(plan.AfterFluids) ||
                !SameReceipt(recomputed.Receipt, plan.Receipt))
                throw new ArgumentException("Successful plan does not match the deterministic fluid planner result.", nameof(plan));
        }

        static bool AllFluidValuesFinite(FluidRefillFluidValues fluids)
        {
            return fluids != null && MechanicsSchema.Finite(fluids.OilLitres) && MechanicsSchema.Finite(fluids.CoolantLitres) &&
                MechanicsSchema.Finite(fluids.OilTemperatureC) && MechanicsSchema.Finite(fluids.OilPressureBar) &&
                MechanicsSchema.Finite(fluids.CoolantPressureBar) && MechanicsSchema.Finite(fluids.OilCondition) &&
                MechanicsSchema.Finite(fluids.OilConsumedLitres) && MechanicsSchema.Finite(fluids.OilLeakedLitres) &&
                MechanicsSchema.Finite(fluids.CoolantLostLitres) && MechanicsSchema.Finite(fluids.ServiceElapsedSeconds) &&
                MechanicsSchema.Finite(fluids.OilBurnLps) && MechanicsSchema.Finite(fluids.OilLeakLps) &&
                MechanicsSchema.Finite(fluids.CoolantLeakLps) && MechanicsSchema.Finite(fluids.CoolantVentLps);
        }

        static bool BitsEqual(double a, double b) => TypedFluidRefillIntent.BitsEqual(a, b);

        static bool SameReceipt(FluidRefillReceiptDelta a, FluidRefillReceiptDelta b)
        {
            return a != null && b != null && a.Id == b.Id && a.Operation == b.Operation && a.Target == b.Target &&
                a.Revision == b.Revision && a.UtcTicks == b.UtcTicks && a.CashBefore == b.CashBefore &&
                a.CashAfter == b.CashAfter && BitsEqual(a.FuelAdded, b.FuelAdded) &&
                BitsEqual(a.OilAdded, b.OilAdded) && BitsEqual(a.CoolantAdded, b.CoolantAdded);
        }

    }

    internal enum TypedFluidRefillStatus
    {
        Committed,
        Replayed,
        NotCommitted,
        Rejected,
        IntentMismatch,
        OutcomeUnknown
    }

    /// <summary>Durable result facts acknowledged or replayed by typed storage.</summary>
    internal sealed class TypedFluidRefillResult
    {
        internal readonly string OperationId;
        internal readonly string SaveSlot;
        internal readonly long ResetGeneration;
        internal readonly string CarId, BusinessReceiptId;
        internal readonly int Cost, CashAfter;
        internal readonly long CommittedLedgerRevision, CommittedStorageCommit;
        internal readonly long AcceptedTick, BusinessReceiptUtcTicks;
        internal readonly double OilAdded, CoolantAdded;
        internal readonly double OilAfter, CoolantAfter;
        // Optional publication patch only. Durable replay remains valid after this
        // bounded business-history row has been evicted.
        internal readonly SqliteEconomyReceipt BusinessReceipt;

        TypedFluidRefillResult(TypedFluidRefillIntent intent, FluidRefillResult plan, long committedLedgerRevision,
            long committedStorageCommit, string carId, int cost, int cashAfter, double oilAdded, double coolantAdded,
            double oilAfter, double coolantAfter, string businessReceiptId, SqliteEconomyReceipt optionalBusinessReceipt,
            bool validateAgainstPlan)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (validateAgainstPlan && plan == null) throw new ArgumentNullException(nameof(plan));
            var request = intent.Request;
            long expectedRevision = checked(request.Versions.ExpectedLedgerRevision + 1);
            long expectedCommit = checked(request.Versions.ExpectedStorageCommit + 1);
            if (committedLedgerRevision != expectedRevision || committedStorageCommit != expectedCommit ||
                !string.Equals(carId, request.CarId, StringComparison.Ordinal) ||
                !string.Equals(businessReceiptId, request.Identity.OperationId, StringComparison.Ordinal) ||
                cost < 0 || cashAfter < 0 || (long)cost + cashAfter > int.MaxValue ||
                !MechanicsSchema.Finite(oilAdded) || oilAdded < 0 || !MechanicsSchema.Finite(coolantAdded) || coolantAdded < 0 ||
                oilAdded > request.RequestedOilLitres || coolantAdded > request.RequestedCoolantLitres ||
                !MechanicsSchema.Finite(oilAfter) || oilAfter < 0 || !MechanicsSchema.Finite(coolantAfter) || coolantAfter < 0 ||
                oilAfter < oilAdded || coolantAfter < coolantAdded ||
                (optionalBusinessReceipt != null && !MatchesReceiptFacts(optionalBusinessReceipt, businessReceiptId,
                    carId, committedLedgerRevision, intent.AcceptedTick, cost, cashAfter, oilAdded, coolantAdded)))
                throw new ArgumentException("Stored result does not match its command identity, versions, or valid durable facts.");
            if (validateAgainstPlan && (cost != plan.Cost || cashAfter != plan.CashAfter ||
                !BitsEqual(oilAdded, plan.OilAdded) || !BitsEqual(coolantAdded, plan.CoolantAdded) ||
                !BitsEqual(oilAfter, plan.AfterFluids.OilLitres) || !BitsEqual(coolantAfter, plan.AfterFluids.CoolantLitres) ||
                !string.Equals(businessReceiptId, plan.Receipt.Id, StringComparison.Ordinal) ||
                (optionalBusinessReceipt != null && !MatchesReceipt(plan.Receipt, optionalBusinessReceipt))))
                throw new ArgumentException("Fresh committed result does not match the deterministic plan.");
            OperationId = request.Identity.OperationId;
            SaveSlot = request.Identity.SaveSlot;
            ResetGeneration = request.Identity.ResetGeneration;
            CarId = carId; BusinessReceiptId = businessReceiptId;
            Cost = cost; CashAfter = cashAfter;
            CommittedLedgerRevision = committedLedgerRevision; CommittedStorageCommit = committedStorageCommit;
            AcceptedTick = intent.AcceptedTick; BusinessReceiptUtcTicks = intent.AcceptedTick;
            OilAdded = oilAdded; CoolantAdded = coolantAdded; OilAfter = oilAfter; CoolantAfter = coolantAfter;
            BusinessReceipt = optionalBusinessReceipt;
        }

        internal static TypedFluidRefillResult ForCommitted(TypedFluidRefillCommand command, long committedLedgerRevision,
            long committedStorageCommit, string carId, int cost, int cashAfter, double oilAdded, double coolantAdded,
            double oilAfter, double coolantAfter, string businessReceiptId, SqliteEconomyReceipt businessReceipt)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            return new TypedFluidRefillResult(command.Intent, command.Plan, committedLedgerRevision, committedStorageCommit,
                carId, cost, cashAfter, oilAdded, coolantAdded, oilAfter, coolantAfter, businessReceiptId, businessReceipt, true);
        }

        internal static TypedFluidRefillResult FromStored(TypedFluidRefillIntent intent, long committedLedgerRevision,
            long committedStorageCommit, string carId, int cost, int cashAfter, double oilAdded, double coolantAdded,
            double oilAfter, double coolantAfter, string businessReceiptId, SqliteEconomyReceipt optionalBusinessReceipt)
        {
            return new TypedFluidRefillResult(intent, null, committedLedgerRevision, committedStorageCommit,
                carId, cost, cashAfter, oilAdded, coolantAdded, oilAfter, coolantAfter, businessReceiptId, optionalBusinessReceipt, false);
        }

        static bool BitsEqual(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        static bool MatchesReceipt(FluidRefillReceiptDelta expected, SqliteEconomyReceipt actual)
        {
            return expected != null && actual != null &&
                string.Equals(expected.Id, actual.Id, StringComparison.Ordinal) &&
                string.Equals(expected.Operation, actual.Operation, StringComparison.Ordinal) &&
                string.Equals(expected.Target, actual.Target, StringComparison.Ordinal) &&
                expected.Revision == actual.Revision && expected.UtcTicks == actual.UtcTicks &&
                expected.CashBefore == actual.CashBefore && expected.CashAfter == actual.CashAfter &&
                BitConverter.DoubleToInt64Bits(expected.FuelAdded) == actual.FuelAddedBits &&
                BitConverter.DoubleToInt64Bits(expected.OilAdded) == actual.OilAddedBits &&
                BitConverter.DoubleToInt64Bits(expected.CoolantAdded) == actual.CoolantAddedBits;
        }

        static bool MatchesReceiptFacts(SqliteEconomyReceipt receipt, string receiptId, string carId,
            long revision, long utcTicks, int cost, int cashAfter, double oilAdded, double coolantAdded)
        {
            return receipt != null && string.Equals(receipt.Id, receiptId, StringComparison.Ordinal) &&
                string.Equals(receipt.Operation, PaymentKind.Fluids.ToString(), StringComparison.Ordinal) &&
                string.Equals(receipt.Target, carId, StringComparison.Ordinal) && receipt.Revision == revision &&
                receipt.UtcTicks == utcTicks && receipt.CashAfter == cashAfter &&
                (long)receipt.CashBefore - receipt.CashAfter == cost && receipt.FuelAddedBits == BitConverter.DoubleToInt64Bits(0) &&
                receipt.OilAddedBits == BitConverter.DoubleToInt64Bits(oilAdded) &&
                receipt.CoolantAddedBits == BitConverter.DoubleToInt64Bits(coolantAdded) &&
                receipt.DisposedIds().Length == 0 && receipt.RetainedIds().Length == 0;
        }
    }

    internal sealed class TypedFluidRefillOutcome
    {
        internal readonly TypedFluidRefillStatus Status;
        internal readonly TypedFluidRefillResult Result;
        internal readonly string Error;
        // Current commit observed in the same writer/reconciliation transaction.
        // Generic S06a callers may omit it; active S06c publication requires it.
        internal readonly long? ObservedStorageCommit;

        internal TypedFluidRefillOutcome(TypedFluidRefillStatus status, TypedFluidRefillResult result, string error,
            long? observedStorageCommit = null)
        {
            if (!Enum.IsDefined(typeof(TypedFluidRefillStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if ((status == TypedFluidRefillStatus.Committed || status == TypedFluidRefillStatus.Replayed) != (result != null))
                throw new ArgumentException("Committed and replayed outcomes require an exact result; other outcomes cannot carry one.", nameof(result));
            if (observedStorageCommit < 0) throw new ArgumentOutOfRangeException(nameof(observedStorageCommit));
            Status = status; Result = result; Error = error; ObservedStorageCommit=observedStorageCommit;
        }
    }
}
