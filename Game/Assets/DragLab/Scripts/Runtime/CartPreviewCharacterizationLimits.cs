#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    /// <summary>Bounded S06d2b1d limits, malformed-input, and legacy-normalization evidence.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint LimitsFixtureSeed = 20261028u;

        static void RunLimitContracts(Trace trace, string subset)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            if (subset == "limits" || subset == "all") RunLimitCases(trace);
            if (subset == "malformed" || subset == "all") RunMalformedCases(trace);
            if (subset != "limits" && subset != "malformed" && subset != "all")
                throw new ArgumentException("Limit-contract subset must be limits, malformed, or all.", nameof(subset));
        }

        static void RunLimitCases(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile, out var carId, LimitsFixtureSeed);
            var car = profile.cars.Find(item => item.instanceId == carId);
            if (car == null) { trace.Assert(false, "limit fixture has its selected target car"); return; }

            var empty = CompareTotal(trace, "limits-empty-row-source", profile, carId, Array.Empty<PartCartLine>(),
                ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                typeof(InvalidOperationException).FullName, "Cart needs 1–100 distinct offers.");
            trace.Assert(empty.ExceptionType == typeof(InvalidOperationException).FullName && empty.ExceptionMessage == "Cart needs 1–100 distinct offers.",
                "zero rows reject at row-count validation with the established exact error / " + empty.Text);
            var repeatedOffer = FindOffer(profile, car, PartCategory.HeadIntake, false);
            if (repeatedOffer != null)
            {
                var overLimitRows = Enumerable.Range(0, 101).Select(_ => new PartCartLine
                    { action = CartAction.NewSet, definitionId = repeatedOffer.id, quantity = 1, install = false }).ToArray();
                var overLimit = CompareTotal(trace, "limits-101-rows-precedes-duplicate-offer", profile, carId, overLimitRows,
                    ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                    typeof(InvalidOperationException).FullName, "Cart needs 1–100 distinct offers.");
                trace.Assert(overLimit.ExceptionType == typeof(InvalidOperationException).FullName &&
                    overLimit.ExceptionMessage == "Cart needs 1–100 distinct offers.",
                    "101 rows reject at row-count validation before duplicate-offer checks / " + overLimit.Text);
                trace.Add("LIMIT_CASE|101-rows|rows=101;repeatedDefinition=" + repeatedOffer.id + ";firstStage=row-count;actual=" + overLimit.Text);
            }
            else
            {
                trace.Add("LIMITS_OPEN|101-row-precedence|no real authored no-install row available");
                trace.Assert(false, "requested 101-row validation fixture precondition has an actual offer");
            }

            var eligibleOffers = profile.catalog.parts
                .Where(definition => definition != null && definition.price >= 0 &&
                    profile.reputation >= definition.reputationRequired && PartsMarket.FitsAny(profile, definition))
                .GroupBy(definition => definition.id, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(definition => definition.price).ThenBy(definition => definition.id, StringComparer.Ordinal).ToArray();
            var offers = eligibleOffers.Take(100).ToArray();
            if (offers.Length < 100)
            {
                trace.Add("LIMITS_OPEN|100-distinct-offers|required=100;found=" + eligibleOffers.Length + ";eligibleRule=FitsAny+REP+nonnegativePrice");
                trace.Assert(false, "requested 100-row boundary fixture has all 100 distinct eligible authored offers / found=" + eligibleOffers.Length);
            }
            if (offers.Length == 100)
            {
                var rows = offers.Select(definition => new PartCartLine
                    { action = CartAction.NewSet, definitionId = definition.id, quantity = 1, install = false }).ToArray();
                var total = CompareTotal(trace, "limits-100-distinct-eligible-offers", profile, carId, rows,
                    ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks);
                var fee = CompareFee(trace, "limits-100-distinct-eligible-offers", profile, carId, rows);
                trace.Assert(!total.Threw, "100 distinct eligible no-install rows are accepted / " + total.Text);
                trace.Add("LIMIT_CASE|100-rows|rows=" + rows.Length + ";distinct=" + offers.Select(item => item.id).Distinct(StringComparer.Ordinal).Count() +
                    ";eligibleAvailable=" + eligibleOffers.Length + ";first=" + offers[0].id + ";last=" + offers[offers.Length - 1].id +
                    ";total=" + total.Text + ";fee=" + fee.Text);
                if (!total.Threw && Convert.ToInt64(total.Value, CultureInfo.InvariantCulture) < int.MaxValue / 2)
                    IdentityPreviewCase(trace, "limits-100-row-identity-output", profile, carId, rows);
                else if (!total.Threw)
                {
                    trace.Add("LIMITS_OPEN|100-row-identity-output|total=" + total.Text + ";previewCashSentinel=" + (int.MaxValue / 2).ToString(CultureInfo.InvariantCulture));
                    trace.Assert(false, "requested 100-row full output fixture fits the legacy preview cash sentinel / total=" + total.Text);
                }
            }

            var q99Candidates = profile.catalog.parts
                .Where(definition => definition != null && definition.price >= 0 &&
                    profile.reputation >= definition.reputationRequired && PartsMarket.FitsAny(profile, definition))
                .GroupBy(definition => definition.id, StringComparer.Ordinal).Select(group => group.First())
                .OrderBy(definition => definition.price).ThenBy(definition => definition.id, StringComparer.Ordinal).ToArray();
            PartDefinition quantityOffer = null;
            SupplyQuote q99Quote = null;
            var q99QuoteErrors = new List<string>();
            int q99OverSentinel = 0;
            foreach (var definition in q99Candidates)
            {
                try
                {
                    int count = InventoryUnits.Count(profile, car, definition);
                    var quote = PartsMarket.Quote(profile, definition, count, false, 99);
                    if (quote != null && quote.cost >= 0 && quote.cost < int.MaxValue / 2)
                    { quantityOffer = definition; q99Quote = quote; break; }
                    q99OverSentinel++;
                }
                catch (Exception error)
                { q99QuoteErrors.Add(definition.id + ":" + error.GetType().FullName + " | " + error.Message); }
            }
            if (quantityOffer == null)
            {
                string firstQuoteErrors = string.Join(" || ", q99QuoteErrors.Take(8));
                trace.Add("LIMITS_OPEN|quantity-99|eligible=" + q99Candidates.Length + ";withinPreviewCash=0;overSentinel=" + q99OverSentinel +
                    ";quoteExceptionCount=" + q99QuoteErrors.Count + ";firstQuoteExceptions=" + firstQuoteErrors);
                trace.Assert(false, "requested quantity-99 fixture has an eligible offer within the legacy preview cash sentinel / eligible=" + q99Candidates.Length +
                    ";overSentinel=" + q99OverSentinel + ";quoteExceptionCount=" + q99QuoteErrors.Count);
            }
            if (quantityOffer != null)
            {
                var row = new[] { new PartCartLine { action = CartAction.NewSet, definitionId = quantityOffer.id, quantity = 99, install = false } };
                var total = CompareTotal(trace, "limits-quantity-99", profile, carId, row,
                    ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks);
                var fee = CompareFee(trace, "limits-quantity-99", profile, carId, row);
                trace.Assert(!total.Threw, "quantity 99 is accepted / " + total.Text);
                trace.Add("LIMIT_CASE|quantity-99|offer=" + quantityOffer.id + ";quantity=99;quotedCost=" + q99Quote.cost+
                    ";quotedFee=" + q99Quote.fee + ";total=" + total.Text + ";fee=" + fee.Text);
                if (!total.Threw && Convert.ToInt64(total.Value, CultureInfo.InvariantCulture) < int.MaxValue / 2)
                    IdentityPreviewCase(trace, "limits-quantity-99-identity-output", profile, carId, row);
                else if (!total.Threw)
                {
                    trace.Add("LIMITS_OPEN|quantity-99-identity-output|total=" + total.Text + ";previewCashSentinel=" + (int.MaxValue / 2).ToString(CultureInfo.InvariantCulture));
                    trace.Assert(false, "requested quantity-99 full output fixture fits the legacy preview cash sentinel / total=" + total.Text);
                }

                var row100 = new[] { new PartCartLine { action = CartAction.NewSet, definitionId = quantityOffer.id, quantity = 100, install = false } };
                var invalid = CompareTotal(trace, "limits-quantity-100", profile, carId, row100,
                    ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                    typeof(InvalidOperationException).FullName, "Invalid cart action or quantity.");
                trace.Assert(invalid.ExceptionType == typeof(InvalidOperationException).FullName &&
                    invalid.ExceptionMessage == "Invalid cart action or quantity.",
                    "quantity 100 rejects at row validation with the established exact error / " + invalid.Text);
                trace.Add("LIMIT_CASE|quantity-100|offer=" + quantityOffer.id + ";firstStage=row-validation;actual=" + invalid.Text);
            }
            trace.Add("LIMITS_OPEN|typed-preview=not-implemented;limits-only-does-not-close-full-S06-or-step-1-matrix");
        }

        static void RunMalformedCases(Trace trace)
        {

            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile, out var carId, LimitsFixtureSeed + 1);
            var car = profile.cars.Find(item => item.instanceId == carId);
            var set = car == null ? null : FindOffer(profile, car, PartCategory.HeadIntake, false);
            if (car == null || set == null) { trace.Assert(false, "malformed fixture has real car and eligible set offer"); return; }
            var looseOffer = FindOffer(profile, car, PartCategory.HeadIntake, false);
            if (looseOffer == null) { trace.Assert(false, "malformed move/sale fixture has actual market stock for a loose part"); return; }
            PartOperations.Buy(profile, profile.catalog, carId, looseOffer.id, false);
            if (!profile.parts.Any(item => item != null && item.location == PartLocation.Inventory && item.definitionId == looseOffer.id))
            { trace.Assert(false, "actual PartOperations.Buy setup created the loose fixture part"); return; }
            long now = CartPreviewCharacterizationFixtures.FixedNowTicks;
            var policy = ValuationPolicy.Defaults();

            var target = LimitMalformedFixture(LimitsFixtureSeed + 2, out var targetCarId, out _, out _);
            LimitTotalCase(trace, "missing-target-precedes-null-source", target, "missing-target", null, policy, now,
                "target availability", typeof(InvalidOperationException).FullName, "Service vehicle no longer exists.");
            var job = LimitMalformedFixture(LimitsFixtureSeed + 9, out targetCarId, out var jobCar, out var jobOffer);
            jobCar.garageJobId = "fixture-reservation";
            LimitTotalCase(trace, "garage-job-reservation", job, targetCarId,
                new[] { new PartCartLine { action = CartAction.NewSet, definitionId = jobOffer.id } }, policy, now,
                "target availability", typeof(InvalidOperationException).FullName,
                "Car and installed components reserved by garage job; claim or cancel it first.");
            var rebuild = LimitMalformedFixture(LimitsFixtureSeed + 10, out targetCarId, out var rebuildCar, out var rebuildOffer);
            rebuildCar.engineRebuild = new EngineRebuildJob { active = true };
            LimitTotalCase(trace, "active-engine-rebuild-reservation", rebuild, targetCarId,
                new[] { new PartCartLine { action = CartAction.NewSet, definitionId = rebuildOffer.id } }, policy, now,
                "target availability", typeof(InvalidOperationException).FullName,
                "Engine reserved at rebuild shop; collect it before modifying/racing this car.");

            LimitTotalCase(trace, "null-source", profile, carId, null, policy, now,
                "row materialization", typeof(ArgumentNullException).FullName, new ArgumentNullException("source").Message);
            LimitTotalCase(trace, "null-row", profile, carId, new PartCartLine[] { null }, policy, now,
                "row validation", typeof(InvalidOperationException).FullName, "Invalid cart action or quantity.");
            LimitTotalCase(trace, "undefined-action", profile, carId,
                new[] { new PartCartLine { action = (CartAction)777, definitionId = set.id } }, policy, now,
                "row validation", typeof(InvalidOperationException).FullName, "Invalid cart action or quantity.");
            LimitTotalCase(trace, "quantity-zero", profile, carId,
                new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 0 } }, policy, now,
                "row validation", typeof(InvalidOperationException).FullName, "Invalid cart action or quantity.");
            LimitTotalCase(trace, "quantity-100", profile, carId,
                new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 100 } }, policy, now,
                "row validation", typeof(InvalidOperationException).FullName, "Invalid cart action or quantity.");
            LimitTotalCase(trace, "duplicate-new-offer", profile, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id },
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id } }, policy, now,
                "ordered row validation", typeof(InvalidOperationException).FullName, "Duplicate new offer.");

            var unitTarget = IdentityFindInstalledUnitTarget(profile, car, PartCategory.Pistons) ??
                IdentityFindInstalledUnitTarget(profile, car, PartCategory.Wheels) ??
                IdentityFindInstalledUnitTarget(profile, car, PartCategory.Tires);
            if (unitTarget == null || unitTarget.Positions.Length == 0)
            {
                trace.Assert(false, "malformed positions fixture has an actual installed multi-member unit target");
            }
            else
            {
                var baseRow = new PartCartLine { action = CartAction.NewUnits, definitionId = unitTarget.Definition.id,
                    role = unitTarget.Role, install = true, quantity = 2, positions = unitTarget.Positions.Take(2).ToList() };
                LimitUnitPositionCase(trace, "null-unit-positions", profile, carId, baseRow, null, policy, now);
                LimitUnitPositionCase(trace, "empty-unit-positions", profile, carId, baseRow, new List<string>(), policy, now);
                if (unitTarget.Positions.Length >= 2)
                    LimitUnitPositionCase(trace, "too-many-unit-positions", profile, carId,
                        new PartCartLine { action = baseRow.action, definitionId = baseRow.definitionId, role = baseRow.role,
                            install = true, quantity = 1, positions = unitTarget.Positions.Take(2).ToList() }, policy, now);
                else trace.Assert(false, "authored unit fixture has two positions for the too-many boundary");
                if (unitTarget.Positions.Length > 0)
                {
                    string position = unitTarget.Positions[0];
                    LimitUnitPositionCase(trace, "duplicate-unit-position", profile, carId,
                        new PartCartLine { action = baseRow.action, definitionId = baseRow.definitionId, role = baseRow.role,
                            install = true, quantity = 2, positions = new List<string> { position, position } }, policy, now);
                    LimitUnitPositionCase(trace, "wrong-unit-position", profile, carId,
                        new PartCartLine { action = baseRow.action, definitionId = baseRow.definitionId, role = baseRow.role,
                            install = true, quantity = 1, positions = new List<string> { "s06d2b/nonexistent-position" } }, policy, now);
                }
                LimitTotalCase(trace, "wrong-unit-role", profile, carId,
                    new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = unitTarget.Definition.id,
                        role = "s06d2b/nonexistent-role", quantity = 1 } }, policy, now,
                    "unit role", typeof(InvalidOperationException).FullName, "This offer has no individually modeled units of that type.");
            }

            var ownedPart = profile.parts.FirstOrDefault(item => item != null && item.location == PartLocation.Inventory && item.definitionId == looseOffer.id);
            if (ownedPart != null)
            {
                var badDestination = new[] { new PartCartLine { action = CartAction.Move,
                    assetId = ownedPart.instanceId, destination = (InventoryKind)777, destinationId = "unused" } };
                LimitTotalCase(trace, "undefined-move-destination", profile, carId, badDestination, policy, now,
                    "move destination validation", typeof(InvalidOperationException).FullName, "Use a quote/cart to buy or sell.");
                LimitTotalCase(trace, "duplicate-move-asset", profile, carId, new[] {
                    new PartCartLine { action = CartAction.Move, assetId = ownedPart.instanceId, destination = InventoryKind.Storage, destinationId = SharedInventory.Storage },
                    new PartCartLine { action = CartAction.Move, assetId = ownedPart.instanceId, destination = InventoryKind.Storage, destinationId = SharedInventory.Storage } }, policy, now,
                    "ordered move validation", typeof(InvalidOperationException).FullName, "Duplicate move or invalid quantity.");
            }
            else trace.Assert(false, "malformed move fixture has an actual loose owned part");

            LimitLedgerFailureCase(trace, profile, carId, set, now);
            LimitNullDealerWrapperCase(trace, profile, carId, set, now, policy);
            LimitReservationFailureCase(trace, profile, carId, set, now, policy);
            trace.Add("MALFORMED_OPEN|remaining=complete duplicate sell/slot order; dealer conflicting/missing payload and stale forms; every stock shape; all block/multitarget errors; typed no-heal behavior belongs to typed planner");
        }

        static PlayerProfile LimitMalformedFixture(uint seed, out string carId, out OwnedCar car, out PartDefinition offer)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile, out carId, seed);
            string capturedCarId = carId;
            car = profile.cars.Find(item => item.instanceId == capturedCarId);
            offer = car == null ? null : FindOffer(profile, car, PartCategory.HeadIntake, false);
            return profile;
        }

        static void LimitTotalCase(Trace trace, string name, PlayerProfile profile, string carId,
            IEnumerable<PartCartLine> rows, ValuationPolicy policy, long now, string stage,
            string expectedType, string expectedMessage)
        {
            var result = CompareTotal(trace, "malformed-" + name, profile, carId, rows, policy, now, expectedType, expectedMessage);
            bool exact = result.Threw && (expectedType == null || result.ExceptionType == expectedType) &&
                (expectedMessage == null || result.ExceptionMessage == expectedMessage);
            trace.Assert(exact, "malformed first error is exact at declared stage / " + name + " / " + stage + " / " + result.Text);
            trace.Add("MALFORMED_CASE|" + name + "|firstStage=" + stage + "|actual=" + result.Text +
                "|expectedType=" + (expectedType ?? "<none>") + "|expectedMessage=" + (expectedMessage ?? "<oracle-only>"));
        }

        static void LimitUnitPositionCase(Trace trace, string name, PlayerProfile profile, string carId,
            PartCartLine source, ValuationPolicy policy, long now)
            => LimitUnitPositionCase(trace, name, profile, carId, source, source.positions, policy, now);

        static void LimitUnitPositionCase(Trace trace, string name, PlayerProfile profile, string carId,
            PartCartLine source, List<string> positions, ValuationPolicy policy, long now)
        {
            var row = new PartCartLine { action = source.action, definitionId = source.definitionId,
                role = source.role, install = source.install, quantity = source.quantity,
                positions = positions == null ? null : new List<string>(positions) };
            LimitTotalCase(trace, name, profile, carId, new[] { row }, policy, now,
                "unit-position validation", typeof(InvalidOperationException).FullName,
                "Select unique fitted positions for installed units, matching the fitted specification. Unselected extras remain loose.");
        }

        static void LimitLedgerFailureCase(Trace trace, PlayerProfile baseline, string carId, PartDefinition offer, long now)
        {
            var profile = CartPreviewLiteralTestClone.Copy(baseline);
            profile.inventory.market = null;
            LimitTotalCase(trace, "null-market-before-row-validation", profile, carId,
                new[] { new PartCartLine { action = CartAction.NewSet, definitionId = offer.id } },
                ValuationPolicy.Defaults(), now, "ledger construction", typeof(NullReferenceException).FullName,
                "Object reference not set to an instance of an object");
        }

        static void LimitNullDealerWrapperCase(Trace trace, PlayerProfile baseline, string carId,
            PartDefinition offer, long now, ValuationPolicy policy)
        {
            var profile = CartPreviewLiteralTestClone.Copy(baseline);
            var loose = profile.parts.FirstOrDefault(item => item != null && item.location == PartLocation.Inventory);
            if (loose == null)
            {
                var car = profile.cars.Find(item => item.instanceId == carId);
                var actualOffer = FindOffer(profile, car, PartCategory.HeadIntake, false);
                if (actualOffer == null) { trace.Assert(false, "dealer-wrapper case has an actual offer for a saleable loose part"); return; }
                PartOperations.Buy(profile, profile.catalog, carId, actualOffer.id, false);
                loose = profile.parts.LastOrDefault(item => item.location == PartLocation.Inventory && item.definitionId == actualOffer.id);
            }
            if (loose == null) { trace.Assert(false, "dealer-wrapper case created a real loose part"); return; }
            int nullIndex = profile.inventory.dealer.Count;
            profile.inventory.dealer.Add(null);
            LimitTotalCase(trace, "null-dealer-wrapper-at-sale-quote", profile, carId,
                new[] { new PartCartLine { action = CartAction.SellPart, assetId = loose.instanceId } }, policy, now,
                "first sale ValidateForQuote", typeof(InvalidOperationException).FullName,
                "Invalid dealer stock at row " + nullIndex.ToString(CultureInfo.InvariantCulture) + ": missing listing.");
        }

        static void LimitReservationFailureCase(Trace trace, PlayerProfile baseline, string carId,
            PartDefinition offer, long now, ValuationPolicy policy)
        {
            var profile = CartPreviewLiteralTestClone.Copy(baseline);
            var car = profile.cars.Find(item => item.instanceId == carId);
            var loose = profile.parts.FirstOrDefault(item => item != null && item.location == PartLocation.Inventory);
            if (loose == null)
            {
                var actualOffer = FindOffer(profile, car, PartCategory.HeadIntake, false);
                if (actualOffer == null) { trace.Assert(false, "reservation error fixture has an actual offer for a saleable loose part"); return; }
                PartOperations.Buy(profile, profile.catalog, carId, actualOffer.id, false);
                loose = profile.parts.LastOrDefault(item => item.location == PartLocation.Inventory && item.definitionId == actualOffer.id);
            }
            if (loose == null) { trace.Assert(false, "reservation error fixture created a real loose part"); return; }
            CartReservations.Capture(profile, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = offer.id } });
            profile.inventory.cart.version = 2;
            LimitTotalCase(trace, "invalid-reservation-at-sale-quote", profile, carId,
                new[] { new PartCartLine { action = CartAction.SellPart, assetId = loose.instanceId } }, policy, now,
                "first sale ValidateForQuote", typeof(InvalidOperationException).FullName, "Invalid cart reservation clock or owner.");
        }

    }
}
#endif
