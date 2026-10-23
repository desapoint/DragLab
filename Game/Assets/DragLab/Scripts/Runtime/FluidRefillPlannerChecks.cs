using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DragLab.Runtime
{
#if UNITY_EDITOR
    /// <summary>Independent typed checks for the dormant S04 oil/coolant refill planner.</summary>
    public static class FluidRefillPlannerChecks
    {
        const long Now = 638000000000000000L;
        const long ResetGeneration = 4;
        const string SaveSlot = "s04-verification";
        const string OperationId = "s04-fluid-refill-001";
        static readonly StringBuilder Report = new StringBuilder();
        static int passed;
        static VehicleDefinition[] authored;
        static MechanicalCatalog catalog;

        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("S04 fluid planner check failed: " + name);
            passed++; Report.AppendLine("PASS " + name);
        }

        static T Throws<T>(Action action, string name, string messagePart = null) where T : Exception
        {
            try { action(); }
            catch (T ex)
            {
                if (messagePart != null && (ex.Message == null || ex.Message.IndexOf(messagePart, StringComparison.Ordinal) < 0))
                    throw new InvalidOperationException("S04 expected message containing '" + messagePart + "' for " + name + "; got: " + ex.Message, ex);
                passed++; Report.AppendLine("PASS " + name); return ex;
            }
            throw new InvalidOperationException("S04 expected " + typeof(T).Name + ": " + name);
        }

        static PlayerProfile Fixture()
        {
            var p = PlayerProfile.New(Now - TimeSpan.TicksPerDay);
            p.SelectedCar.vehicleId = p.selectedVehicleId = authored[0].spec.id;
            MechanicsOwnership.Ensure(p, catalog);
            p.catalog = catalog;
            p.cash = 6000;
            var car = p.SelectedCar;
            car.fluids.oilLitres = Math.Min(2.1, catalog.Spec(car.vehicleId).fluids.oilCapacityLitres * .55);
            car.fluids.coolantLitres = Math.Min(1.4, catalog.Spec(car.vehicleId).fluids.coolantCapacityLitres * .5);
            car.fluids.oilTemperatureC = 91.25;
            car.fluids.oilPressureBar = 3.75;
            car.fluids.coolantPressureBar = 1.25;
            car.fluids.oilCondition = .63;
            car.fluids.oilConsumedLitres = .12;
            car.fluids.oilLeakedLitres = .03;
            car.fluids.coolantLostLitres = .04;
            car.fluids.serviceElapsedSeconds = 18.5;
            car.fluids.oilBurnLps = .0001;
            car.fluids.oilLeakLps = .00002;
            car.fluids.coolantLeakLps = .00003;
            car.fluids.coolantVentLps = .00004;
            string invalid = MechanicsOwnership.Validate(p, catalog);
            if (invalid != null) throw new InvalidOperationException("S04 authored fixture is invalid: " + invalid);
            return p;
        }

        static OperatingPolicy Policy() => new OperatingPolicy { oilPerLitre = 6.125, coolantPerLitre = 3.875, quoteSeconds = 300 };

        static FluidRefillRequest Request(PlayerProfile p, double oil, double coolant, string operation = OperationId,
            long issued = Now, long? expires = null)
        {
            return new FluidRefillRequest(new StateCommandIdentity(SaveSlot, ResetGeneration, operation),
                new StateCommandVersionFence(p.revision, p.storageCommit, MechanicalCatalog.Version), p.SelectedCar.instanceId,
                oil, coolant, issued, expires ?? checked(issued + TimeSpan.FromSeconds(Policy().quoteSeconds).Ticks));
        }

        static string FluidFingerprint(FluidState f)
        {
            return string.Join("|", new[] { f.oilLitres, f.coolantLitres, f.oilTemperatureC, f.oilPressureBar, f.coolantPressureBar,
                f.oilCondition, f.oilConsumedLitres, f.oilLeakedLitres, f.coolantLostLitres, f.serviceElapsedSeconds,
                f.oilBurnLps, f.oilLeakLps, f.coolantLeakLps, f.coolantVentLps }.Select(v => BitConverter.DoubleToInt64Bits(v).ToString("X16", CultureInfo.InvariantCulture)));
        }

        static void RestoreFluidWorkload(PlayerProfile profile, FluidState fluids, int cash, long revision, long lastEconomy)
        {
            profile.SelectedCar.fluids = fluids.Copy();
            profile.cash = cash;
            profile.revision = revision;
            profile.lastEconomyUtcTicks = lastEconomy;
            profile.economyReceipts.Clear();
        }

        static void CheckIndependentShadow()
        {
            foreach (var amounts in new[] { (oil: .375, coolant: .625), (oil: 100.0, coolant: .125), (oil: .00000002, coolant: .00000001) })
            {
                var p = Fixture(); var car = p.SelectedCar; var policy = Policy();
                var before = FluidFingerprint(car.fluids); int cashBefore = p.cash; long revisionBefore = p.revision;
                var carRef = car; var fluidRef = car.fluids; var partsRef = p.parts; var enginesRef = p.engines;
                var request = Request(p, amounts.oil, amounts.coolant);
                var input = FluidRefillPlanner.Capture(p, policy, request, Now + 10);
                var result = FluidRefillPlanner.Plan(input);

                // Independent legacy shadow: Preview computes the accepted quantities and money;
                // the two public TopUp calls mutate only the typed fluid record; Record is called
                // directly, matching Pay's post-action cash/receipt behavior without Quote/Pay.
                var legacyRequest = new PaymentRequest { kind = PaymentKind.Fluids, carId = car.instanceId,
                    litres = amounts.oil, coolantLitres = amounts.coolant };
                var quote = PaidEconomy.Preview(p, catalog, policy, ValuationPolicy.Defaults(), legacyRequest, Now + 10);
                Check(result.Committable, "positive refill plan is committable");
                Check(Bitwise(result.OilAdded, quote.oil) && Bitwise(result.CoolantAdded, quote.coolant), "planner accepted quantities match independent Preview clamp");
                Check(result.Cost == quote.cost, "planner money ceiling matches independent Preview");
                Check(ReferenceEquals(p.cars[0], carRef) && ReferenceEquals(car.fluids, fluidRef) && ReferenceEquals(p.parts, partsRef) && ReferenceEquals(p.engines, enginesRef), "capture and Plan preserve source references");
                Check(FluidFingerprint(car.fluids) == before && p.cash == cashBefore && p.revision == revisionBefore && p.economyReceipts.Count == 0, "capture and Plan do not mutate profile or fluid history");

                double oilAdded = ServiceOperations.TopUp(p, car.instanceId, true, quote.oil);
                double coolantAdded = ServiceOperations.TopUp(p, car.instanceId, false, quote.coolant);
                p.cash = checked(cashBefore - quote.cost);
                PaidEconomy.Record(p, request.Identity.OperationId, PaymentKind.Fluids.ToString(), car.instanceId,
                    cashBefore, Now + 10, oil: oilAdded, coolant: coolantAdded);
                p.revision = checked(revisionBefore + 1);

                Check(Bitwise(result.OilAdded, oilAdded) && Bitwise(result.CoolantAdded, coolantAdded), "planner deltas match independent TopUp results");
                Check(Bitwise(result.AfterFluids.OilLitres, car.fluids.oilLitres) && Bitwise(result.AfterFluids.CoolantLitres, car.fluids.coolantLitres), "planner after-fluid quantities match legacy shadow");
                Check(FluidFingerprint(result.AfterFluids.ToDetachedState()) == FluidFingerprint(car.fluids), "all 14 fluid values match independent shadow field fingerprint");
                Check(p.cash == result.CashAfter && result.CashBefore == cashBefore, "planner cash delta matches legacy shadow");
                var receipt = p.economyReceipts.Single(); var delta = result.Receipt;
                Check(delta != null && receipt.id == delta.Id && receipt.operation == delta.Operation && receipt.target == delta.Target, "receipt identity, operation and target match");
                Check(receipt.revision == delta.Revision && receipt.utcTicks == delta.UtcTicks && receipt.utcTicks == Now + 10, "receipt revision and confirmation timestamp match");
                Check(receipt.cashBefore == delta.CashBefore && receipt.cashAfter == delta.CashAfter && Bitwise(receipt.oilAdded, delta.OilAdded) && Bitwise(receipt.coolantAdded, delta.CoolantAdded), "receipt cash and quantities match");
                Check(MechanicsOwnership.Validate(p, catalog) == null, "valid post-refill fixture passes full ownership validation");
            }
        }

        static void CheckRoundingBoundaries()
        {
            var policy = Policy();
            foreach (double bill in new[] { .5e-9, 1e-9, 2e-9, .999999999, 1.0, 1.000000001 })
            {
                var p = Fixture();
                double oil = bill / policy.oilPerLitre;
                var request = Request(p, oil, 0, "s04-rounding-" + passed.ToString(CultureInfo.InvariantCulture));
                var result = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(p, policy, request, Now + 3));
                var quote = PaidEconomy.Preview(p, catalog, policy, ValuationPolicy.Defaults(),
                    new PaymentRequest { kind = PaymentKind.Fluids, carId = p.SelectedCar.instanceId, litres = oil }, Now + 3);
                Check(Bitwise(result.OilAdded, quote.oil) && result.Cost == quote.cost,
                    "legacy Preview parity at money boundary " + bill.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        static void CheckSeededRandomShadow()
        {
            const int seed = 42004;
            const int cases = 32;
            var random = new System.Random(seed);
            var p = Fixture(); var policy = Policy(); var car = p.SelectedCar;
            var baseFluids = car.fluids.Copy(); int cash = p.cash; long revision = p.revision, lastEconomy = p.lastEconomyUtcTicks;
            for (int i = 0; i < cases; i++)
            {
                RestoreFluidWorkload(p, baseFluids, cash, revision, lastEconomy);
                double oilFraction = random.NextDouble() * .97;
                double coolantFraction = random.NextDouble() * .97;
                double oil = random.NextDouble() * catalog.Spec(car.vehicleId).fluids.oilCapacityLitres;
                double coolant = random.NextDouble() * catalog.Spec(car.vehicleId).fluids.coolantCapacityLitres;
                car.fluids.oilLitres = catalog.Spec(car.vehicleId).fluids.oilCapacityLitres * oilFraction;
                car.fluids.coolantLitres = catalog.Spec(car.vehicleId).fluids.coolantCapacityLitres * coolantFraction;
                var request = Request(p, oil, coolant, "s04-random-" + i.ToString("D2", CultureInfo.InvariantCulture));
                var result = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(p, policy, request, Now + 4));
                var quote = PaidEconomy.Preview(p, catalog, policy, ValuationPolicy.Defaults(),
                    new PaymentRequest { kind = PaymentKind.Fluids, carId = car.instanceId, litres = oil, coolantLitres = coolant }, Now + 4);
                Check(result.OilAdded == quote.oil && result.CoolantAdded == quote.coolant && result.Cost == quote.cost,
                    "seeded random accepted amounts and money match independent legacy preview case " + i.ToString(CultureInfo.InvariantCulture));
                if (result.Committable)
                {
                    double oilAdded = ServiceOperations.TopUp(p, car.instanceId, true, quote.oil);
                    double coolantAdded = ServiceOperations.TopUp(p, car.instanceId, false, quote.coolant);
                    p.cash = checked(cash - quote.cost);
                    PaidEconomy.Record(p, request.Identity.OperationId, PaymentKind.Fluids.ToString(), car.instanceId,
                        cash, Now + 4, oil: oilAdded, coolant: coolantAdded);
                    p.revision = checked(revision + 1);
                    Check(FluidFingerprint(result.AfterFluids.ToDetachedState()) == FluidFingerprint(car.fluids),
                        "seeded random all-fluid shadow parity case " + i.ToString(CultureInfo.InvariantCulture));
                }
            }
            RestoreFluidWorkload(p, baseFluids, cash, revision, lastEconomy);
            Report.AppendLine("randomShadow seed=" + seed.ToString(CultureInfo.InvariantCulture) + " cases=" + cases.ToString(CultureInfo.InvariantCulture) + " workload=single-authored-car-typed-fluid-reset");
        }

        static double ElapsedMilliseconds(Action action)
        {
            long start = Stopwatch.GetTimestamp(); action();
            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        static double Median(double[] values)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            return (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2.0;
        }

        static void CheckDescriptiveTiming()
        {
            const int warmups = 3;
            const int samples = 20;
            var p = Fixture(); var car = p.SelectedCar; var policy = Policy();
            var initialFluids = car.fluids.Copy(); int initialCash = p.cash; long initialRevision = p.revision, initialClock = p.lastEconomyUtcTicks;
            double oil = .35, coolant = .2;
            var request = Request(p, oil, coolant, "s04-timing");
            var captureMs = new double[samples]; var planMs = new double[samples]; var legacyMs = new double[samples];
            Action captureAndPlan = () =>
            {
                var input = FluidRefillPlanner.Capture(p, policy, request, Now + 5);
                FluidRefillPlanner.Plan(input);
            };
            Action legacyShadow = () =>
            {
                var quote = PaidEconomy.Preview(p, catalog, policy, ValuationPolicy.Defaults(),
                    new PaymentRequest { kind = PaymentKind.Fluids, carId = car.instanceId, litres = oil, coolantLitres = coolant }, Now + 5);
                double oilAdded = ServiceOperations.TopUp(p, car.instanceId, true, quote.oil);
                double coolantAdded = ServiceOperations.TopUp(p, car.instanceId, false, quote.coolant);
                p.cash = checked(initialCash - quote.cost);
                PaidEconomy.Record(p, request.Identity.OperationId, PaymentKind.Fluids.ToString(), car.instanceId,
                    initialCash, Now + 5, oil: oilAdded, coolant: coolantAdded);
                p.revision = checked(initialRevision + 1);
            };
            for (int i = 0; i < warmups; i++)
            {
                RestoreFluidWorkload(p, initialFluids, initialCash, initialRevision, initialClock);
                captureAndPlan(); legacyShadow();
            }
            for (int i = 0; i < samples; i++)
            {
                RestoreFluidWorkload(p, initialFluids, initialCash, initialRevision, initialClock);
                captureMs[i] = ElapsedMilliseconds(() => FluidRefillPlanner.Capture(p, policy, request, Now + 5));
                var input = FluidRefillPlanner.Capture(p, policy, request, Now + 5);
                planMs[i] = ElapsedMilliseconds(() => FluidRefillPlanner.Plan(input));
                RestoreFluidWorkload(p, initialFluids, initialCash, initialRevision, initialClock);
                legacyMs[i] = ElapsedMilliseconds(legacyShadow);
            }
            RestoreFluidWorkload(p, initialFluids, initialCash, initialRevision, initialClock);
            Report.AppendLine("timing workload=one-authored-car targetFluid=oil+coolant ownedCars=" + p.cars.Count.ToString(CultureInfo.InvariantCulture) +
                " ownedParts=" + p.parts.Count.ToString(CultureInfo.InvariantCulture) + " warmups=" + warmups.ToString(CultureInfo.InvariantCulture) +
                " samples=" + samples.ToString(CultureInfo.InvariantCulture) + " clock=Stopwatch; descriptive-only; no-allocation-or-tail-percentile-claim");
            Report.AppendLine("timingMedianMs capture=" + Median(captureMs).ToString("R", CultureInfo.InvariantCulture) +
                " plan=" + Median(planMs).ToString("R", CultureInfo.InvariantCulture) +
                " legacyPreviewTopUpRecord=" + Median(legacyMs).ToString("R", CultureInfo.InvariantCulture));
            for (int i = 0; i < samples; i++)
                Report.AppendLine("timingSample=" + i.ToString("D2", CultureInfo.InvariantCulture) +
                    " captureMs=" + captureMs[i].ToString("R", CultureInfo.InvariantCulture) +
                    " planMs=" + planMs[i].ToString("R", CultureInfo.InvariantCulture) +
                    " legacyPreviewTopUpRecordMs=" + legacyMs[i].ToString("R", CultureInfo.InvariantCulture));
        }

        static bool Bitwise(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        static void CheckOutcomesAndFreshness()
        {
            var p = Fixture(); var policy = Policy();
            var zero = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(p, policy, Request(p, 0, 0), Now + 1));
            Check(!zero.Committable && zero.Error == "No free capacity / quantity to purchase." && zero.Receipt == null, "zero accepted quantity retains legacy noncommittable message");
            Check(FluidFingerprint(zero.AfterFluids.ToDetachedState()) == FluidFingerprint(zero.BeforeFluids.ToDetachedState()) && zero.LastEconomyUtcTicksAfter == p.lastEconomyUtcTicks, "zero refill result preserves independent fluid before-image and saved economy clock");

            p.cash = 1;
            var expensive = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(p, policy, Request(p, .25, .25, "s04-low-cash"), Now + 1));
            Check(!expensive.Committable && expensive.Error == "Insufficient cash or changed price; nothing changed." && expensive.CashAfter == p.cash && expensive.Receipt == null, "insufficient funds remains noncommittable without a receipt");
            Check(FluidFingerprint(expensive.AfterFluids.ToDetachedState()) == FluidFingerprint(expensive.BeforeFluids.ToDetachedState()) && expensive.LastEconomyUtcTicksAfter == p.lastEconomyUtcTicks, "insufficient-funds result preserves independent fluid before-image and saved economy clock");

            p.cash = 6000;
            var currentRequest = Request(p, .2, .3, "s04-freshness");
            var planned = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(p, policy, currentRequest, Now + 1));
            var sameTick = FluidRefillPlanner.Capture(p, policy, currentRequest, Now + 1);
            Check(FluidRefillPlanner.IsCurrent(planned, sameTick, SaveSlot, ResetGeneration, Now + 1, out var error), "equivalent fresh typed readset remains current at planning tick: " + (error ?? "ok"));
            var later = FluidRefillPlanner.Capture(p, policy, currentRequest, Now + 2);
            Check(!FluidRefillPlanner.IsCurrent(planned, later, SaveSlot, ResetGeneration, Now + 2, out error), "changed confirmation tick requires a fresh Plan");
            var replanned = FluidRefillPlanner.Plan(later);
            Check(FluidRefillPlanner.IsCurrent(replanned, later, SaveSlot, ResetGeneration, Now + 2, out error) && replanned.Receipt.UtcTicks == Now + 2, "fresh confirmation plan records actual confirmation tick");
            Check(!FluidRefillPlanner.IsCurrent(replanned, later, "other-slot", ResetGeneration, Now + 2, out error) && error == "State command save slot is stale.", "current owner save-slot mismatch rejected");
            Check(!FluidRefillPlanner.IsCurrent(replanned, later, SaveSlot, ResetGeneration + 1, Now + 2, out error) && error == "State command reset generation is stale.", "current owner reset-generation mismatch rejected");

            p.SelectedCar.fluids.oilTemperatureC += 1;
            var changedFluids = FluidRefillPlanner.Capture(p, policy, currentRequest, Now + 2);
            Check(!FluidRefillPlanner.IsCurrent(replanned, changedFluids, SaveSlot, ResetGeneration, Now + 2, out error) && error == "Fluid refill read dependencies changed.", "same-revision fluid-history edit invalidates plan");
            p.SelectedCar.fluids.oilTemperatureC -= 1;
            p.cash--;
            var changedCash = FluidRefillPlanner.Capture(p, policy, currentRequest, Now + 2);
            Check(!FluidRefillPlanner.IsCurrent(replanned, changedCash, SaveSlot, ResetGeneration, Now + 2, out error), "same-revision cash edit invalidates plan");

            var receiptFixture = Fixture(); receiptFixture.lastEconomyUtcTicks = Now - TimeSpan.TicksPerDay;
            var receiptRequest = Request(receiptFixture, .1, .1, "s04-receipt-list");
            var receiptPlan = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(receiptFixture, policy, receiptRequest, Now + 1));
            receiptFixture.economyReceipts.Add(new EconomyReceipt { id = "s04-unrelated-receipt", operation = "prior", target = "other", revision = 0,
                utcTicks = Now - 2 * TimeSpan.TicksPerDay, cashBefore = receiptFixture.cash, cashAfter = receiptFixture.cash });
            Check(MechanicsOwnership.Validate(receiptFixture, catalog) == null, "receipt-window freshness fixture remains globally valid");
            var changedReceiptList = FluidRefillPlanner.Capture(receiptFixture, policy, receiptRequest, Now + 1);
            Check(!FluidRefillPlanner.IsCurrent(receiptPlan, changedReceiptList, SaveSlot, ResetGeneration, Now + 1, out error), "same-revision receipt-list mutation invalidates read dependencies");

            var negative = Fixture();
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(negative, policy,
                Request(negative, -.1, .1, "s04-negative"), Now), "negative requested amount rejected", "Quantity must be finite and nonnegative.");
        }

        static void CheckExactDoubleEquality()
        {
            long one = BitConverter.DoubleToInt64Bits(1.0);
            double collision = BitConverter.Int64BitsToDouble(one ^ (1L << 32) ^ 1L);
            Check(MechanicsSchema.Finite(collision) && collision > 0 && !Bitwise(collision, 1.0), "finite positive exact-bit collision fixture is valid");
            var identity = new StateCommandIdentity(SaveSlot, ResetGeneration, "s04-bit-equality");
            var fence = new StateCommandVersionFence(12, 5, MechanicalCatalog.Version);
            var requestA = new FluidRefillRequest(identity, fence, "car", 1.0, .25, Now, Now + TimeSpan.FromSeconds(300).Ticks);
            var requestB = new FluidRefillRequest(identity, fence, "car", collision, .25, Now, Now + TimeSpan.FromSeconds(300).Ticks);
            Check(!requestA.Equals(requestB), "request equality distinguishes finite values with colliding folded hash bits");

            var p = Fixture(); var request = Request(p, .2, .1, "s04-readset-bit-equality");
            var policyA = Policy(); policyA.oilPerLitre = 1.0;
            var policyB = Policy(); policyB.oilPerLitre = collision;
            var planned = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(p, policyA, request, Now + 1));
            var fresh = FluidRefillPlanner.Capture(p, policyB, request, Now + 1);
            Check(!FluidRefillPlanner.IsCurrent(planned, fresh, SaveSlot, ResetGeneration, Now + 1, out var error) &&
                error == "Fluid refill read dependencies changed.", "readset freshness compares exact rate bits despite hash-fold collision");
        }

        static void CheckCaptureGuards()
        {
            var p = Fixture(); var policy = Policy(); var request = Request(p, .1, .1, "s04-guards");
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(p, policy, request, request.IssuedUtcTicks - 1), "before-issued time rejected", "Payment quote stale/expired.");
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(p, policy, request, request.ExpiresUtcTicks), "expired request rejected", "Payment quote stale/expired.");
            var rollback = Fixture(); rollback.lastEconomyUtcTicks = Now + 100;
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(rollback, policy, Request(rollback, .1, .1, "s04-clock-rollback"), Now), "clock earlier than saved economy time rejected", "Clock moved backwards");
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(p, policy,
                new FluidRefillRequest(request.Identity, request.Versions, request.CarId, .1, .1, request.IssuedUtcTicks, request.ExpiresUtcTicks - 1), Now), "wrong policy expiry rejected", "Payment quote stale/expired.");
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(p, policy,
                new FluidRefillRequest(request.Identity, new StateCommandVersionFence(1, request.Versions.ExpectedStorageCommit, MechanicalCatalog.Version), request.CarId, .1, .1, request.IssuedUtcTicks, request.ExpiresUtcTicks), Now), "stale ledger fence rejected", "State command version fence is stale.");
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(p, policy,
                new FluidRefillRequest(request.Identity, new StateCommandVersionFence(request.Versions.ExpectedLedgerRevision, 99, MechanicalCatalog.Version), request.CarId, .1, .1, request.IssuedUtcTicks, request.ExpiresUtcTicks), Now), "stale storage fence rejected", "State command version fence is stale.");
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(p, policy,
                new FluidRefillRequest(request.Identity, new StateCommandVersionFence(request.Versions.ExpectedLedgerRevision, request.Versions.ExpectedStorageCommit, MechanicalCatalog.Version + 1), request.CarId, .1, .1, request.IssuedUtcTicks, request.ExpiresUtcTicks), Now), "stale content fence rejected", "State command version fence is stale.");
            p.economyReceipts.Add(new EconomyReceipt { id = request.Identity.OperationId, operation = "Fluids", utcTicks = Now, revision = 0 });
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(p, policy, request, Now), "duplicate operation rejected", "Operation already settled.");
            p.economyReceipts.Clear();
            p.runCheckpoints.Add(new RunCheckpoint { settled = false });
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(p, policy, request, Now), "active run blocks refill planning", "Close the active run");
            p.runCheckpoints.Clear();

            var invalidFluid = Fixture(); invalidFluid.SelectedCar.fluids.oilLitres = double.NaN;
            Check(MechanicsOwnership.Validate(invalidFluid, catalog) != null, "malformed fluid fixture is rejected by full validator");
            Throws<ArgumentException>(() => FluidRefillPlanner.Capture(invalidFluid, policy, Request(invalidFluid, .1, .1, "s04-invalid-fluid"), Now), "malformed target fluids rejected without normalization", "Invalid fluid state.");

            var malformedEngine = Fixture(); var targetCar = malformedEngine.SelectedCar;
            var attachedEngine = malformedEngine.engines.Single(e => e.instanceId == targetCar.engineAssemblyId);
            var originalParent = attachedEngine.parentVehicleId; var originalVehicle = attachedEngine.vehicleDefinitionId;
            string originalFluids = FluidFingerprint(targetCar.fluids);
            Action[] corruptions =
            {
                () => attachedEngine.parentVehicleId = "s04-wrong-parent",
                () => attachedEngine.vehicleDefinitionId = "s04-wrong-vehicle",
                () => attachedEngine.slots.Add(new SlotBinding { slot = "s04-invalid", partInstanceId = "s04-missing-part" })
            };
            string[] corruptionNames = { "engine parent", "engine vehicle", "attached engine slots" };
            for (int i = 0; i < corruptions.Length; i++)
            {
                corruptions[i]();
                Check(MechanicsOwnership.Validate(malformedEngine, catalog) == "Invalid engine parent.", "full validator rejects malformed " + corruptionNames[i]);
                Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(malformedEngine, policy,
                    Request(malformedEngine, .1, .1, "s04-invalid-engine-" + i.ToString(CultureInfo.InvariantCulture)), Now),
                    "planner rejects malformed " + corruptionNames[i], "Invalid engine parent.");
                Check(FluidFingerprint(targetCar.fluids) == originalFluids, "malformed " + corruptionNames[i] + " is not healed and fluid source stays exact");
                attachedEngine.parentVehicleId = originalParent;
                attachedEngine.vehicleDefinitionId = originalVehicle;
                attachedEngine.slots.Clear();
            }
            Check(MechanicsOwnership.Validate(malformedEngine, catalog) == null, "typed engine corruption cases restore original valid fixture");

            var missingEngine = Fixture(); missingEngine.SelectedCar.engineAssemblyId = "";
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(missingEngine, policy, Request(missingEngine, .1, .1, "s04-missing-engine"), Now), "missing attached engine rejected", "No attached engine to service.");
            var reserved = Fixture(); reserved.SelectedCar.garageJobId = "s04-active-job";
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(reserved, policy, Request(reserved, .1, .1, "s04-garage-job"), Now), "reserved garage car rejected", "garage job");
            var rebuilding = Fixture(); rebuilding.SelectedCar.engineRebuild = new EngineRebuildJob { active = true };
            Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(rebuilding, policy, Request(rebuilding, .1, .1, "s04-rebuild-job"), Now), "active rebuild reservation rejects refill", "rebuild shop");
        }

        static void CheckRequestQuantities()
        {
            var p = Fixture(); var policy = Policy();
            foreach (double invalid in new[] { -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var request = Request(p, invalid, 0, "s04-invalid-" + passed.ToString(CultureInfo.InvariantCulture));
                Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(p, policy, request, Now), "invalid oil quantity rejected");
            }
            foreach (double invalid in new[] { -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                var request = Request(p, 0, invalid, "s04-invalid-c-" + passed.ToString(CultureInfo.InvariantCulture));
                Throws<InvalidOperationException>(() => FluidRefillPlanner.Capture(p, policy, request, Now), "invalid coolant quantity rejected");
            }
        }

        static void CheckTargetDependencies()
        {
            var p = Fixture(); var car = p.SelectedCar; var policy = Policy();
            var request = Request(p, .2, .3, "s04-target-dependencies");
            var plan = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(p, policy, request, Now + 1));
            long revision = p.revision;
            var panSlot = car.installed.slots.Single(s => s.slot == PartCategory.OilPan.ToString());
            var panPart = p.parts.Single(part => part.instanceId == panSlot.partInstanceId);
            var definition = catalog.Find(panPart.definitionId);
            float capacity = definition.fluidCapacityLitres;
            try
            {
                definition.fluidCapacityLitres += .5f; catalog.RebuildIndexes();
                Check(MechanicsOwnership.Validate(p, catalog) == null, "capacity edit retains a valid ownership graph");
                var changed = FluidRefillPlanner.Capture(p, policy, request, Now + 1);
                Check(changed.ReadSet.OilCapacityLitres != plan.ReadSet.OilCapacityLitres, "oil-pan edit changes actual resolved refill capacity");
                Check(p.revision == revision && !FluidRefillPlanner.IsCurrent(plan, changed, SaveSlot, ResetGeneration, Now + 1, out var error)
                    && error == "Fluid refill read dependencies changed.", "same-revision capacity edit invalidates the refill plan");
            }
            finally { definition.fluidCapacityLitres = capacity; catalog.RebuildIndexes(); }

            string engineId = car.engineAssemblyId;
            var engine = p.engines.Single(e => e.instanceId == engineId);
            var descendants = p.parts.Where(part => part.ownerId == engineId).ToArray();
            string replacementId = engineId + "-s04";
            try
            {
                engine.instanceId = replacementId; car.engineAssemblyId = replacementId;
                foreach (var part in descendants) part.ownerId = replacementId;
                Check(MechanicsOwnership.Validate(p, catalog) == null, "engine identity edit retains valid actual descendant ownership");
                var changed = FluidRefillPlanner.Capture(p, policy, request, Now + 1);
                Check(p.revision == revision && changed.ReadSet.EngineAssemblyId == replacementId &&
                    !FluidRefillPlanner.IsCurrent(plan, changed, SaveSlot, ResetGeneration, Now + 1, out var error)
                    && error == "Fluid refill read dependencies changed.", "same-revision valid engine identity edit invalidates the refill plan");
            }
            finally
            {
                engine.instanceId = engineId; car.engineAssemblyId = engineId;
                foreach (var part in descendants) part.ownerId = engineId;
            }
            Check(MechanicsOwnership.Validate(p, catalog) == null && p.revision == revision, "dependency fixture restores original valid ownership without normalization");
        }

        public static void RunTargetDependenciesStandalone()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("S04 checks require a dedicated Editor batch process outside Play Mode.");
            Report.Length = 0; passed = 0;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S04"));
            Directory.CreateDirectory(root);
            try
            {
                authored = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
                catalog = new MechanicalCatalog(authored.Select(v => v.spec));
                CheckTargetDependencies();
                Report.AppendLine("RESULT PASS count=" + passed);
                UnityEngine.Debug.Log("S04 target dependency checks passed: " + passed);
            }
            catch (Exception ex) { Report.AppendLine("RESULT FAIL count=" + passed); Report.AppendLine(ex.ToString()); throw; }
            finally { File.WriteAllText(Path.Combine(root, "fluid-refill-target-dependency-checks.txt"), Report.ToString(), new UTF8Encoding(false)); }
        }

        public static void RunStandalone()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("S04 checks require a dedicated Editor batch process outside Play Mode.");
            Report.Length = 0; passed = 0;
            Report.AppendLine("S04 dormant typed fluid refill planner; no caller switch, ProfileStore, SQLite, JSON oracle, or whole-profile clone.");
            try
            {
                authored = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
                if (authored == null || authored.Length == 0) throw new InvalidOperationException("S04 requires authored vehicle definitions.");
                catalog = new MechanicalCatalog(authored.Select(v => v.spec));
                Report.AppendLine("fixture=one authored vehicle; initialized once per case with MechanicsOwnership.Ensure; runtime IDs remain generated; no save writes");
                Report.AppendLine("policy=oil 6.125 / coolant 3.875 / quoteSeconds 300; clockTicks=" + Now.ToString(CultureInfo.InvariantCulture));
                CheckIndependentShadow();
                CheckRoundingBoundaries();
                CheckSeededRandomShadow();
                CheckOutcomesAndFreshness();
                CheckExactDoubleEquality();
                CheckCaptureGuards();
                CheckRequestQuantities();
                CheckTargetDependencies();
                CheckDescriptiveTiming();
                Report.AppendLine("RESULT PASS count=" + passed.ToString(CultureInfo.InvariantCulture));
                WriteReport();
                UnityEngine.Debug.Log("S04 fluid refill planner checks passed: " + passed);
            }
            catch (Exception ex)
            {
                Report.AppendLine("RESULT FAIL count=" + passed.ToString(CultureInfo.InvariantCulture)); Report.AppendLine(ex.ToString());
                WriteReport(); UnityEngine.Debug.LogException(ex); throw;
            }
        }

        static void WriteReport()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S04"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fluid-refill-planner-checks.txt"), Report.ToString(), new UTF8Encoding(false));
        }
    }
#endif
}
