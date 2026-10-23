using System;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    // Immutable command request. Slot/reset identity and global fences come from
    // the owner; this value never invents a storage identity or row version.
    internal sealed class FluidRefillRequest : IEquatable<FluidRefillRequest>
    {
        internal readonly StateCommandIdentity Identity;
        internal readonly StateCommandVersionFence Versions;
        internal readonly string CarId;
        internal readonly double RequestedOilLitres, RequestedCoolantLitres;
        internal readonly long IssuedUtcTicks, ExpiresUtcTicks;

        internal FluidRefillRequest(StateCommandIdentity identity, StateCommandVersionFence versions,
            string carId, double requestedOilLitres, double requestedCoolantLitres,
            long issuedUtcTicks, long expiresUtcTicks)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Versions = versions ?? throw new ArgumentNullException(nameof(versions));
            CarId = carId; RequestedOilLitres = requestedOilLitres; RequestedCoolantLitres = requestedCoolantLitres;
            IssuedUtcTicks = issuedUtcTicks; ExpiresUtcTicks = expiresUtcTicks;
        }

        public bool Equals(FluidRefillRequest other)
        {
            return !ReferenceEquals(other, null) && Identity.Equals(other.Identity) && Versions.Equals(other.Versions) &&
                string.Equals(CarId, other.CarId, StringComparison.Ordinal) && Same(RequestedOilLitres, other.RequestedOilLitres) &&
                Same(RequestedCoolantLitres, other.RequestedCoolantLitres) && IssuedUtcTicks == other.IssuedUtcTicks && ExpiresUtcTicks == other.ExpiresUtcTicks;
        }
        public override bool Equals(object obj) => Equals(obj as FluidRefillRequest);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Identity.GetHashCode(); hash = hash * 397 ^ Versions.GetHashCode();
                hash = hash * 397 ^ (CarId == null ? 0 : StringComparer.Ordinal.GetHashCode(CarId));
                hash = hash * 397 ^ HashBits(RequestedOilLitres); hash = hash * 397 ^ HashBits(RequestedCoolantLitres);
                hash = hash * 397 ^ IssuedUtcTicks.GetHashCode(); return hash * 397 ^ ExpiresUtcTicks.GetHashCode();
            }
        }
        static bool Same(double a, double b) => Bits(a) == Bits(b);
        static long Bits(double value) => new DoubleBits { Value = value }.Bits;
        static int HashBits(double value) { long bits = Bits(value); return (int)bits ^ (int)(bits >> 32); }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        struct DoubleBits
        {
            [System.Runtime.InteropServices.FieldOffset(0)] public double Value;
            [System.Runtime.InteropServices.FieldOffset(0)] public long Bits;
        }
    }

    // Immutable value copy of every persisted fluid field. No FluidState reference
    // crosses the planning boundary.
    internal sealed class FluidRefillFluidValues : IEquatable<FluidRefillFluidValues>
    {
        internal readonly double OilLitres, CoolantLitres, OilTemperatureC, OilPressureBar, CoolantPressureBar;
        internal readonly double OilCondition, OilConsumedLitres, OilLeakedLitres, CoolantLostLitres, ServiceElapsedSeconds;
        internal readonly double OilBurnLps, OilLeakLps, CoolantLeakLps, CoolantVentLps;

        internal FluidRefillFluidValues(FluidState source)
        {
            OilLitres = source.oilLitres; CoolantLitres = source.coolantLitres;
            OilTemperatureC = source.oilTemperatureC; OilPressureBar = source.oilPressureBar; CoolantPressureBar = source.coolantPressureBar;
            OilCondition = source.oilCondition; OilConsumedLitres = source.oilConsumedLitres; OilLeakedLitres = source.oilLeakedLitres;
            CoolantLostLitres = source.coolantLostLitres; ServiceElapsedSeconds = source.serviceElapsedSeconds;
            OilBurnLps = source.oilBurnLps; OilLeakLps = source.oilLeakLps; CoolantLeakLps = source.coolantLeakLps; CoolantVentLps = source.coolantVentLps;
        }

        internal FluidRefillFluidValues(FluidRefillFluidValues source, double oilLitres, double coolantLitres)
        {
            OilLitres = oilLitres; CoolantLitres = coolantLitres;
            OilTemperatureC = source.OilTemperatureC; OilPressureBar = source.OilPressureBar; CoolantPressureBar = source.CoolantPressureBar;
            OilCondition = source.OilCondition; OilConsumedLitres = source.OilConsumedLitres; OilLeakedLitres = source.OilLeakedLitres;
            CoolantLostLitres = source.CoolantLostLitres; ServiceElapsedSeconds = source.ServiceElapsedSeconds;
            OilBurnLps = source.OilBurnLps; OilLeakLps = source.OilLeakLps; CoolantLeakLps = source.CoolantLeakLps; CoolantVentLps = source.CoolantVentLps;
        }

        internal FluidState ToDetachedState()
        {
            return new FluidState
            {
                oilLitres = OilLitres, coolantLitres = CoolantLitres,
                oilTemperatureC = OilTemperatureC, oilPressureBar = OilPressureBar, coolantPressureBar = CoolantPressureBar,
                oilCondition = OilCondition, oilConsumedLitres = OilConsumedLitres, oilLeakedLitres = OilLeakedLitres,
                coolantLostLitres = CoolantLostLitres, serviceElapsedSeconds = ServiceElapsedSeconds,
                oilBurnLps = OilBurnLps, oilLeakLps = OilLeakLps, coolantLeakLps = CoolantLeakLps, coolantVentLps = CoolantVentLps
            };
        }

        public bool Equals(FluidRefillFluidValues other)
        {
            return !ReferenceEquals(other, null) && Same(OilLitres, other.OilLitres) && Same(CoolantLitres, other.CoolantLitres) &&
                Same(OilTemperatureC, other.OilTemperatureC) && Same(OilPressureBar, other.OilPressureBar) && Same(CoolantPressureBar, other.CoolantPressureBar) &&
                Same(OilCondition, other.OilCondition) && Same(OilConsumedLitres, other.OilConsumedLitres) && Same(OilLeakedLitres, other.OilLeakedLitres) &&
                Same(CoolantLostLitres, other.CoolantLostLitres) && Same(ServiceElapsedSeconds, other.ServiceElapsedSeconds) &&
                Same(OilBurnLps, other.OilBurnLps) && Same(OilLeakLps, other.OilLeakLps) && Same(CoolantLeakLps, other.CoolantLeakLps) && Same(CoolantVentLps, other.CoolantVentLps);
        }
        public override bool Equals(object obj) => Equals(obj as FluidRefillFluidValues);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                Add(ref hash, OilLitres); Add(ref hash, CoolantLitres); Add(ref hash, OilTemperatureC); Add(ref hash, OilPressureBar); Add(ref hash, CoolantPressureBar);
                Add(ref hash, OilCondition); Add(ref hash, OilConsumedLitres); Add(ref hash, OilLeakedLitres); Add(ref hash, CoolantLostLitres); Add(ref hash, ServiceElapsedSeconds);
                Add(ref hash, OilBurnLps); Add(ref hash, OilLeakLps); Add(ref hash, CoolantLeakLps); Add(ref hash, CoolantVentLps); return hash;
            }
        }
        static bool Same(double a, double b) => Bits(a) == Bits(b);
        static long Bits(double value) => new DoubleBits { Value = value }.Bits;
        static void Add(ref int hash, double value) { long bits = Bits(value); unchecked { hash = hash * 31 + (int)bits; hash = hash * 31 + (int)(bits >> 32); } }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        struct DoubleBits
        {
            [System.Runtime.InteropServices.FieldOffset(0)] public double Value;
            [System.Runtime.InteropServices.FieldOffset(0)] public long Bits;
        }
    }

    // Exact relevant read values, not synthetic row versions. A new Capture is
    // required before apply; direct same-revision edits are caught by value equality.
    internal sealed class FluidRefillReadSet : IEquatable<FluidRefillReadSet>
    {
        internal readonly string CarId, VehicleId, EngineAssemblyId, GarageJobId, OldestEconomyReceiptId;
        internal readonly bool HasRebuild, RebuildActive, HasUnsettledRun, OperationAlreadyRecorded;
        internal readonly int Cash, EconomyReceiptCount;
        internal readonly long LastEconomyUtcTicks;
        internal readonly double OilCapacityLitres, CoolantCapacityLitres, OilPerLitre, CoolantPerLitre;
        internal readonly FluidRefillFluidValues Fluids;

        internal FluidRefillReadSet(string carId, string vehicleId, string engineAssemblyId, string garageJobId,
            bool hasRebuild, bool rebuildActive, int cash, long lastEconomyUtcTicks,
            double oilCapacityLitres, double coolantCapacityLitres, double oilPerLitre, double coolantPerLitre,
            FluidRefillFluidValues fluids, bool hasUnsettledRun, bool operationAlreadyRecorded,
            int economyReceiptCount, string oldestEconomyReceiptId)
        {
            CarId = carId; VehicleId = vehicleId; EngineAssemblyId = engineAssemblyId; GarageJobId = garageJobId;
            HasRebuild = hasRebuild; RebuildActive = rebuildActive; Cash = cash; LastEconomyUtcTicks = lastEconomyUtcTicks;
            OilCapacityLitres = oilCapacityLitres; CoolantCapacityLitres = coolantCapacityLitres;
            OilPerLitre = oilPerLitre; CoolantPerLitre = coolantPerLitre; Fluids = fluids;
            HasUnsettledRun = hasUnsettledRun; OperationAlreadyRecorded = operationAlreadyRecorded;
            EconomyReceiptCount = economyReceiptCount; OldestEconomyReceiptId = oldestEconomyReceiptId;
        }

        public bool Equals(FluidRefillReadSet other)
        {
            return !ReferenceEquals(other, null) && string.Equals(CarId, other.CarId, StringComparison.Ordinal) &&
                string.Equals(VehicleId, other.VehicleId, StringComparison.Ordinal) && string.Equals(EngineAssemblyId, other.EngineAssemblyId, StringComparison.Ordinal) &&
                string.Equals(GarageJobId, other.GarageJobId, StringComparison.Ordinal) && HasRebuild == other.HasRebuild && RebuildActive == other.RebuildActive &&
                Cash == other.Cash && LastEconomyUtcTicks == other.LastEconomyUtcTicks && EconomyReceiptCount == other.EconomyReceiptCount &&
                string.Equals(OldestEconomyReceiptId, other.OldestEconomyReceiptId, StringComparison.Ordinal) && Same(OilCapacityLitres, other.OilCapacityLitres) &&
                Same(CoolantCapacityLitres, other.CoolantCapacityLitres) && Same(OilPerLitre, other.OilPerLitre) && Same(CoolantPerLitre, other.CoolantPerLitre) &&
                Fluids.Equals(other.Fluids) && HasUnsettledRun == other.HasUnsettledRun && OperationAlreadyRecorded == other.OperationAlreadyRecorded;
        }
        public override bool Equals(object obj) => Equals(obj as FluidRefillReadSet);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(CarId ?? "");
                hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(VehicleId ?? ""); hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(EngineAssemblyId ?? "");
                hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(GarageJobId ?? ""); hash = hash * 397 ^ Cash; hash = hash * 397 ^ LastEconomyUtcTicks.GetHashCode();
                hash = hash * 397 ^ EconomyReceiptCount; hash = hash * 397 ^ StringComparer.Ordinal.GetHashCode(OldestEconomyReceiptId ?? "");
                hash = hash * 397 ^ HashBits(OilCapacityLitres); hash = hash * 397 ^ HashBits(CoolantCapacityLitres); hash = hash * 397 ^ HashBits(OilPerLitre); hash = hash * 397 ^ HashBits(CoolantPerLitre);
                hash = hash * 397 ^ (Fluids == null ? 0 : Fluids.GetHashCode());
                hash = hash * 397 ^ (HasRebuild ? 1 : 0); hash = hash * 397 ^ (RebuildActive ? 1 : 0);
                hash = hash * 397 ^ (HasUnsettledRun ? 1 : 0); return hash * 397 ^ (OperationAlreadyRecorded ? 1 : 0);
            }
        }
        static bool Same(double a, double b) => Bits(a) == Bits(b);
        static long Bits(double value) => new DoubleBits { Value = value }.Bits;
        static int HashBits(double value) { long bits = Bits(value); return (int)bits ^ (int)(bits >> 32); }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        struct DoubleBits
        {
            [System.Runtime.InteropServices.FieldOffset(0)] public double Value;
            [System.Runtime.InteropServices.FieldOffset(0)] public long Bits;
        }
    }

    internal sealed class FluidRefillInput
    {
        internal readonly FluidRefillRequest Request;
        internal readonly FluidRefillReadSet ReadSet;
        internal readonly long CapturedNowTicks;
        internal FluidRefillInput(FluidRefillRequest request, FluidRefillReadSet readSet, long capturedNowTicks)
        { Request = request; ReadSet = readSet; CapturedNowTicks = capturedNowTicks; }
    }

    internal sealed class FluidRefillReceiptDelta
    {
        internal readonly string Id, Operation, Target;
        internal readonly long Revision, UtcTicks;
        internal readonly int CashBefore, CashAfter;
        internal readonly double FuelAdded, OilAdded, CoolantAdded;
        internal FluidRefillReceiptDelta(string id, string operation, string target, long revision, long utcTicks,
            int cashBefore, int cashAfter, double oilAdded, double coolantAdded)
        {
            Id = id; Operation = operation; Target = target; Revision = revision; UtcTicks = utcTicks;
            CashBefore = cashBefore; CashAfter = cashAfter; FuelAdded = 0; OilAdded = oilAdded; CoolantAdded = coolantAdded;
        }
    }

    internal sealed class FluidRefillResult
    {
        internal readonly FluidRefillRequest Request;
        internal readonly FluidRefillReadSet ReadSet;
        internal readonly long PlanningNowTicks;
        internal readonly long LastEconomyUtcTicksAfter;
        internal readonly FluidRefillFluidValues BeforeFluids, AfterFluids;
        internal readonly double RequestedOilLitres, RequestedCoolantLitres, OilAdded, CoolantAdded;
        internal readonly int Cost, CashBefore, CashAfter;
        internal readonly bool Committable;
        internal readonly string Error;
        internal readonly FluidRefillReceiptDelta Receipt;

        internal FluidRefillResult(FluidRefillInput input, double oilAdded, double coolantAdded, int cost, bool committable, string error)
        {
            Request = input.Request; ReadSet = input.ReadSet; PlanningNowTicks = input.CapturedNowTicks;
            LastEconomyUtcTicksAfter = committable ? input.CapturedNowTicks : input.ReadSet.LastEconomyUtcTicks;
            BeforeFluids = input.ReadSet.Fluids;
            AfterFluids = committable
                ? new FluidRefillFluidValues(BeforeFluids, BeforeFluids.OilLitres + oilAdded, BeforeFluids.CoolantLitres + coolantAdded)
                : BeforeFluids;
            RequestedOilLitres = input.Request.RequestedOilLitres; RequestedCoolantLitres = input.Request.RequestedCoolantLitres;
            OilAdded = oilAdded; CoolantAdded = coolantAdded; Cost = cost; CashBefore = input.ReadSet.Cash;
            Committable = committable; Error = error; CashAfter = committable ? checked(CashBefore - cost) : CashBefore;
            Receipt = committable ? new FluidRefillReceiptDelta(input.Request.Identity.OperationId, PaymentKind.Fluids.ToString(),
                input.Request.CarId, checked(input.Request.Versions.ExpectedLedgerRevision + 1), input.CapturedNowTicks,
                CashBefore, CashAfter, oilAdded, coolantAdded) : null;
        }
    }

    // S04 shadow planner. Capture is owner-thread-only; Plan consumes detached values
    // and may be run without a profile reference. No caller is switched in this slice.
    internal static class FluidRefillPlanner
    {
        internal static FluidRefillInput Capture(PlayerProfile profile, OperatingPolicy policy,
            FluidRefillRequest request, long nowTicks)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (nowTicks <= 0 || nowTicks > DateTime.MaxValue.Ticks - TimeSpan.TicksPerDay || nowTicks < profile.lastEconomyUtcTicks)
                throw new InvalidOperationException("Clock moved backwards or outside supported range. Wait for the saved clock before paying/claiming.");
            if (nowTicks < request.IssuedUtcTicks || nowTicks >= request.ExpiresUtcTicks)
                throw new InvalidOperationException("Payment quote stale/expired.");
            if (profile.runCheckpoints.Any(r => !r.settled))
                throw new InvalidOperationException("Close the active run before changing assets or resources.");
            if (profile.economyReceipts.Any(r => r.id == request.Identity.OperationId))
                throw new InvalidOperationException("Operation already settled.");

            policy.Validate();
            long expectedExpiry = checked(request.IssuedUtcTicks + TimeSpan.FromSeconds(policy.quoteSeconds).Ticks);
            if (request.ExpiresUtcTicks != expectedExpiry) throw new InvalidOperationException("Payment quote stale/expired.");
            if (profile.revision != request.Versions.ExpectedLedgerRevision || profile.storageCommit != request.Versions.ExpectedStorageCommit ||
                request.Versions.ExpectedContentVersion != MechanicalCatalog.Version)
                throw new InvalidOperationException("State command version fence is stale.");

            var car = profile.cars.Find(c => c.instanceId == request.CarId);
            EngineRebuilds.RequireAvailable(car);
            var build = MechanicsOwnership.Resolve(profile, car, profile.catalog);
            if (string.IsNullOrEmpty(car.engineAssemblyId)) throw new InvalidOperationException("No attached engine to service.");
            var engine = profile.engines.Find(e => e.instanceId == car.engineAssemblyId);
            if (engine == null || engine.parentVehicleId != car.instanceId || engine.vehicleDefinitionId != car.vehicleId || engine.slots == null || engine.slots.Count != 0)
                throw new InvalidOperationException("Invalid engine parent.");
            if (car.fluids == null || car.fluids.Validate(build.spec.fluids) != null) throw new ArgumentException("Invalid fluid state.");
            if (!MechanicsSchema.Finite(request.RequestedOilLitres) || request.RequestedOilLitres < 0 ||
                !MechanicsSchema.Finite(request.RequestedCoolantLitres) || request.RequestedCoolantLitres < 0)
                throw new InvalidOperationException("Quantity must be finite and nonnegative.");

            // A caller must first establish a valid ownership graph. This planner does
            // not invoke ValidateForQuote, whose dealer recovery path uses JSON copies.
            // No build-error list check is added: legacy fluid payment only requires a
            // current build and attached, available engine.
            var fluids = new FluidRefillFluidValues(car.fluids);
            var readSet = new FluidRefillReadSet(car.instanceId, car.vehicleId, car.engineAssemblyId, car.garageJobId,
                car.engineRebuild != null, car.engineRebuild != null && car.engineRebuild.active,
                profile.cash, profile.lastEconomyUtcTicks, build.spec.fluids.oilCapacityLitres,
                build.spec.fluids.coolantCapacityLitres, policy.oilPerLitre, policy.coolantPerLitre,
                fluids, false, false, profile.economyReceipts.Count,
                profile.economyReceipts.Count == 0 ? null : profile.economyReceipts[0].id);
            return new FluidRefillInput(request, readSet, nowTicks);
        }

        internal static FluidRefillResult Plan(FluidRefillInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            double oil = Quantity(input.Request.RequestedOilLitres, input.ReadSet.OilCapacityLitres - input.ReadSet.Fluids.OilLitres);
            double coolant = Quantity(input.Request.RequestedCoolantLitres, input.ReadSet.CoolantCapacityLitres - input.ReadSet.Fluids.CoolantLitres);
            int cost = Money(oil * input.ReadSet.OilPerLitre + coolant * input.ReadSet.CoolantPerLitre);
            string error = input.ReadSet.Cash < cost ? "Insufficient cash or changed price; nothing changed." :
                oil + coolant <= 0 ? "No free capacity / quantity to purchase." : null;
            return new FluidRefillResult(input, oil, coolant, cost, error == null, error);
        }

        internal static bool IsCurrent(FluidRefillResult planned, FluidRefillInput current,
            string currentSaveSlot, long currentResetGeneration, long nowTicks, out string error)
        {
            if (planned == null || current == null) { error = "Fluid refill input is unavailable."; return false; }
            if (!planned.Request.Identity.Equals(current.Request.Identity) ||
                !string.Equals(planned.Request.Identity.SaveSlot, currentSaveSlot, StringComparison.Ordinal))
            { error = "State command save slot is stale."; return false; }
            if (planned.Request.Identity.ResetGeneration != currentResetGeneration)
            { error = "State command reset generation is stale."; return false; }
            if (!planned.Request.Equals(current.Request) || !planned.Request.Versions.Equals(current.Request.Versions) ||
                !planned.ReadSet.Equals(current.ReadSet))
            { error = "Fluid refill read dependencies changed."; return false; }
            if (planned.PlanningNowTicks != current.CapturedNowTicks || nowTicks != planned.PlanningNowTicks || nowTicks != current.CapturedNowTicks ||
                nowTicks < current.Request.IssuedUtcTicks || nowTicks >= current.Request.ExpiresUtcTicks ||
                nowTicks < current.ReadSet.LastEconomyUtcTicks)
            { error = "Payment quote stale/expired."; return false; }
            error = null; return true;
        }

        static double Quantity(double requested, double free)
        {
            if (!MechanicsSchema.Finite(requested) || requested < 0) throw new InvalidOperationException("Quantity must be finite and nonnegative.");
            return Math.Min(requested, Math.Max(0, free));
        }

        static int Money(double value)
        {
            if (!MechanicsSchema.Finite(value) || value < 0 || value > int.MaxValue)
                throw new InvalidOperationException("Price outside supported currency range.");
            return checked((int)Math.Ceiling(value - 1e-9));
        }
    }
}
