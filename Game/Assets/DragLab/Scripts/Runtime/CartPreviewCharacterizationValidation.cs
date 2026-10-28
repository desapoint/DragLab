#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    /// <summary>Finite malformed-state and first-error supplement for S06d2b step 1.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint ValidationSeed = 20261029u;

        static void RunValidationSupplement(Trace trace, string subset)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            if (subset != "validation" && subset != "validation-sales" && subset != "validation-integrity" &&
                subset != "validation-stock" && subset != "validation-positions")
                throw new ArgumentException("Validation subset must be validation, validation-sales, validation-integrity, validation-stock, or validation-positions.", nameof(subset));
            if (subset == "validation" || subset == "validation-sales") ValidationGroup(trace, "sales", ValidationSales);
            if (subset == "validation" || subset == "validation-integrity") ValidationGroup(trace, "integrity", ValidationIntegrityAndDealer);
            if (subset == "validation" || subset == "validation-stock") ValidationGroup(trace, "stock", ValidationStockAndReservations);
            if (subset == "validation" || subset == "validation-positions") ValidationGroup(trace, "positions", ValidationPositionsAndMoves);
            trace.Add("VALIDATION_SUPPLEMENT|scope=" + subset + ";status=step1-subset;typedCandidate=NONE");
        }

        static void ValidationGroup(Trace trace, string name, Action<Trace> body)
        {
            try { body(trace); }
            catch (Exception error)
            {
                trace.Assert(false, "Validation fixture completed / " + name + " / " + error.GetType().FullName + " | " + error.Message);
                trace.Add("VALIDATION_OPEN|" + name + "|fixture exception retained as failure");
            }
        }

        static void ValidationSales(Trace trace)
        {
            var p = ValidationSaleFixture(trace, out var carId, out var partId);
            if (p == null) { trace.Assert(false, "sale validation fixture has an actual loose owned part"); return; }
            var rows = new[] { new PartCartLine { action = CartAction.SellPart, assetId = partId, quantity = 1 } };
            var badPolicy = ValuationPolicy.Defaults(); badPolicy.dealerHandlingFee = 0;
            ValidationTotal(trace, "sale-invalid-policy", p, carId, rows, badPolicy, CartPreviewCharacterizationFixtures.FixedNowTicks,
                "first sale policy.Validate", typeof(InvalidOperationException).FullName, "Invalid dealer spread/handling fee.");
            ValidationTotal(trace, "sale-invalid-time", p, carId, rows, ValuationPolicy.Defaults(), -1,
                "first sale DisplayLooseTotal", typeof(InvalidOperationException).FullName, "Invalid valuation request.");
            var noWholesale = ValuationPolicy.Defaults(); noWholesale.buyers.RemoveAll(buyer => buyer != null && buyer.id == "wholesale");
            ValidationTotal(trace, "sale-unknown-wholesale-buyer", p, carId, rows, noWholesale, CartPreviewCharacterizationFixtures.FixedNowTicks,
                "first sale DisplayLooseTotal", typeof(InvalidOperationException).FullName, "Unknown buyer.");
        }

        static void ValidationIntegrityAndDealer(Trace trace)
        {
            var source = ValidationSaleFixture(trace, out var carId, out var partId);
            if (source == null) { trace.Assert(false, "integrity/dealer fixture has an actual saleable loose part"); return; }
            var sale = new[] { new PartCartLine { action = CartAction.SellPart, assetId = partId, quantity = 1 } };

            var duplicate = CartPreviewLiteralTestClone.Copy(source);
            var first = duplicate.parts.First(part => part != null && part.location == PartLocation.Inventory);
            var second = duplicate.parts.First(part => part != null && part.location == PartLocation.Inventory && !ReferenceEquals(part, first));
            if (second == null) { trace.Assert(false, "owned integrity fixture has two actual parts to duplicate an identity"); }
            else { second.instanceId = first.instanceId; ValidationTotal(trace, "sale-owned-duplicate-id", duplicate, carId, sale,
                ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks, "first sale ValidateForQuote / owned identity", typeof(InvalidOperationException).FullName,
                "Duplicate/invalid/orphan part instance."); }

            var missingCondition = CartPreviewLiteralTestClone.Copy(source);
            var malformedPart = missingCondition.parts.First(part => part != null && part.instanceId == partId);
            malformedPart.condition = null;
            ValidationTotal(trace, "sale-owned-null-condition", missingCondition, carId, sale,
                ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks, "first sale ValidateForQuote / owned condition", typeof(InvalidOperationException).FullName,
                "Duplicate/invalid/orphan part instance.");

            var missingMember = CartPreviewLiteralTestClone.Copy(source);
            var memberPart = missingMember.parts.First(part => part != null && part.instanceId == partId);
            if (memberPart.condition == null || memberPart.condition.elements == null || memberPart.condition.elements.Length == 0)
            {
                trace.Add("VALIDATION_OPEN|sale-owned-null-member|actual saleable authored part has no member array slot");
                trace.Assert(false, "owned member-integrity fixture has an actual member slot");
            }
            else
            {
                memberPart.condition.elements[0] = null;
                ValidationTotal(trace, "sale-owned-null-member", missingMember, carId, sale,
                    ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks, "first sale ValidateForQuote / owned member", typeof(InvalidOperationException).FullName,
                    "Duplicate/invalid/orphan part instance.");
            }

            uint partSeed = ValidationSeed ^ 0x18a2f35du;
            var seededPart = CartPreviewCharacterizationFixtures.AddUsedPart(source, "validation/dealer-part", ref partSeed);
            uint unitSeed = ValidationSeed ^ 0x2b7c4e91u;
            var seededUnit = CartPreviewCharacterizationFixtures.AddUsedUnit(source, "validation/dealer-unit", ref unitSeed);
            string setupError = MechanicsOwnership.Validate(source, source.catalog);
            trace.Assert(setupError == null && seededPart != null && seededPart.IsPart && seededUnit != null && seededUnit.IsUnit,
                "dealer shapes are copied from actual valid authored used supply / validation=" + setupError);
            if (setupError != null || seededPart == null || seededUnit == null) return;
            int malformedIndex = source.inventory.dealer.FindIndex(row => row != null && row.Id == seededPart.Id);
            int unitIndex = source.inventory.dealer.FindIndex(row => row != null && row.Id == seededUnit.Id);
            if (malformedIndex < 0 || unitIndex < 0) { trace.Assert(false, "dealer setup retains the selected actual part and unit rows"); return; }

            var both = CartPreviewLiteralTestClone.Copy(source);
            var bothRow = both.inventory.dealer[malformedIndex];
            bothRow.unit = both.inventory.dealer[unitIndex].unit;
            ValidationTotal(trace, "dealer-both-payloads", both, carId, sale, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                "first sale ValidateForQuote / dealer row " + malformedIndex.ToString(CultureInfo.InvariantCulture), typeof(InvalidOperationException).FullName,
                "Invalid dealer stock at row " + malformedIndex.ToString(CultureInfo.InvariantCulture) + ": conflicting part and unit payloads.");

            var neither = CartPreviewLiteralTestClone.Copy(source); neither.inventory.dealer[malformedIndex].part = null; neither.inventory.dealer[malformedIndex].unit = null;
            ValidationTotal(trace, "dealer-no-payload", neither, carId, sale, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                "first sale ValidateForQuote / dealer row " + malformedIndex.ToString(CultureInfo.InvariantCulture), typeof(InvalidOperationException).FullName,
                "Invalid dealer stock at row " + malformedIndex.ToString(CultureInfo.InvariantCulture) + ": missing part/unit identity.");

            var negativePrice = CartPreviewLiteralTestClone.Copy(source); negativePrice.inventory.dealer[malformedIndex].paidPrice = -1;
            ValidationTotal(trace, "dealer-negative-paid-price", negativePrice, carId, sale, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                "first sale ValidateForQuote / dealer row " + malformedIndex.ToString(CultureInfo.InvariantCulture), typeof(InvalidOperationException).FullName,
                "Invalid dealer stock at row " + malformedIndex.ToString(CultureInfo.InvariantCulture) + ": negative purchase price.");

            var staleDiscriminator = CartPreviewLiteralTestClone.Copy(source);
            staleDiscriminator.inventory.dealer[malformedIndex].isUnit = !staleDiscriminator.inventory.dealer[malformedIndex].IsUnit;
            string strictError = MechanicsOwnership.Validate(staleDiscriminator, staleDiscriminator.catalog);
            string quoteError = MechanicsOwnership.ValidateForQuote(staleDiscriminator, staleDiscriminator.catalog);
            var staleQuote = CompareTotal(trace, "dealer-stale-discriminator-tolerated-for-quote", staleDiscriminator, carId, sale,
                ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(strictError == "Invalid dealer stock at row " + malformedIndex.ToString(CultureInfo.InvariantCulture) + ": wrong part/unit discriminator." && quoteError == null && !staleQuote.Threw,
                "quote validation tolerates only the stale dealer discriminator after strict validation / strict=" + strictError + ";quote=" + quoteError + ";total=" + staleQuote.Text);

            var duplicateDealer = CartPreviewLiteralTestClone.Copy(source);
            // Take the matching row from a separate literal graph copy; no Ensure or JSON round-trip follows corruption.
            var dealerCopy = CartPreviewLiteralTestClone.Copy(source);
            string firstDealerId = duplicateDealer.inventory.dealer[malformedIndex].Id;
            var duplicateStock = dealerCopy.inventory.dealer.First(row => row != null && row.Id == firstDealerId);
            duplicateDealer.inventory.dealer.Add(duplicateStock);
            int duplicateIndex = duplicateDealer.inventory.dealer.Count - 1;
            string expectedDuplicate = "Duplicate dealer part identity at row " + duplicateIndex.ToString(CultureInfo.InvariantCulture) + ": " + duplicateDealer.inventory.dealer[malformedIndex].Id + ".";
            ValidationTotal(trace, "dealer-duplicate-physical-id", duplicateDealer, carId, sale, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                "first sale ValidateForQuote / dealer identity", typeof(InvalidOperationException).FullName, expectedDuplicate);
        }

        static void ValidationStockAndReservations(Trace trace)
        {
            var source = ValidationSaleFixture(trace, out var carId, out var partId);
            if (source == null) { trace.Assert(false, "stock fixture has an actual saleable loose part"); return; }
            var sale = new[] { new PartCartLine { action = CartAction.SellPart, assetId = partId, quantity = 1 } };

            var nullStock = CartPreviewLiteralTestClone.Copy(source);
            nullStock.inventory.market.parts[0] = null;
            ValidationTotal(trace, "null-stock-row-before-sale-row-validation", nullStock, carId, sale,
                ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks, "ledger construction before row validation",
                typeof(NullReferenceException).FullName, "Object reference not set to an instance of an object");

            var badUnits = CartPreviewLiteralTestClone.Copy(source);
            var stock = badUnits.inventory.market.parts.First(row => row != null); stock.units = stock.initialUnits + 1;
            ValidationTotal(trace, "sale-invalid-stock-units", badUnits, carId, sale, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                "first sale ValidateForQuote / PartsMarket.Validate", typeof(InvalidOperationException).FullName, "Invalid new-parts stock.");
            var badPercent = CartPreviewLiteralTestClone.Copy(source);
            badPercent.inventory.market.parts.First(row => row != null).deliveryPercent = 11;
            ValidationTotal(trace, "sale-invalid-stock-delivery-percent", badPercent, carId, sale, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                "first sale ValidateForQuote / PartsMarket.Validate", typeof(InvalidOperationException).FullName, "Invalid new-parts stock.");

            var absent = CartPreviewLiteralTestClone.Copy(source);
            var car = absent.cars.Find(value => value.instanceId == carId);
            var candidateRow = absent.inventory.market.parts.FirstOrDefault(row => row != null && row.definitions != null && row.definitions.Any(id =>
                { var definition = absent.catalog.Find(id); return definition != null && definition.price >= 0 && absent.catalog.Fits(absent.catalog.Spec(car.vehicleId), definition); }));
            var absentOffer = candidateRow == null ? null : candidateRow.definitions.Select(absent.catalog.Find)
                .FirstOrDefault(definition => definition != null && definition.price >= 0 && absent.catalog.Fits(absent.catalog.Spec(car.vehicleId), definition));
            if (absentOffer == null || candidateRow == null)
            {
                trace.Add("VALIDATION_OPEN|absent-product|no current market row contains a selected-spec compatible authored definition");
                trace.Assert(false, "absent-product control has an actual compatible authored row to remove");
            }
            else
            {
                int removedRows = candidateRow.definitions.Count;
                absent.inventory.market.parts.Remove(candidateRow);
                absent.inventory.market.index = null;
                string afterRemovalError = MechanicsOwnership.Validate(absent, absent.catalog);
                trace.Assert(afterRemovalError == null, "removing one authored market product row leaves a valid no-stock state / removedDefinitions=" + removedRows + ";validation=" + afterRemovalError);
                var loose = new[] { new PartCartLine { action = CartAction.NewSet, definitionId = absentOffer.id, quantity = 1 } };
                var quote = CompareTotal(trace, "absent-product-express-default", absent, carId, loose, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks);
                int count = PartsMarket.Count(absent, car, absentOffer), basePrice = InventoryPricing.NewSet(absentOffer);
                int fee = checked((int)Math.Ceiling(basePrice * .10)); int expectedCost = checked(basePrice + fee);
                trace.Assert(afterRemovalError == null && !quote.Threw && Convert.ToInt32(quote.Value, CultureInfo.InvariantCulture) == expectedCost,
                    "absent authored product remains a valid all-express 10-percent quote / offer=" + absentOffer.id + ";count=" + count + ";actual=" + quote.Text + ";manualCost=" + expectedCost + ";fee=" + fee);
            }

            var held = CartPreviewLiteralTestClone.Copy(source);
            var heldCar = held.cars.Find(value => value.instanceId == carId);
            var offer = held.catalog.parts.FirstOrDefault(definition => definition != null && definition.category == PartCategory.HeadIntake && definition.price >= 0 &&
                held.catalog.Fits(held.catalog.Spec(heldCar.vehicleId), definition) && PartsMarket.Stock(held, definition)?.units > 0);
            if (offer == null) { trace.Assert(false, "reservation fixture has an actual authored offer"); return; }
            CartReservations.Capture(held, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = offer.id, quantity = 1 } });
            if (held.inventory.cart.supply.Count == 0)
            { trace.Add("VALIDATION_OPEN|reservation-hold|eligible authored fixture produced no local shelf hold"); trace.Assert(false, "reservation boundary fixture has actual held supply"); return; }

            var badGeneration = CartPreviewLiteralTestClone.Copy(held); badGeneration.inventory.cart.supply[0].generation = badGeneration.inventory.market.generation + 1;
            ValidationTotal(trace, "reservation-future-generation", badGeneration, carId, sale, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                "first sale ValidateForQuote / CartReservations.Validate", typeof(InvalidOperationException).FullName, "Invalid reserved stock or cart lines.");
            var overHeld = CartPreviewLiteralTestClone.Copy(held);
            var matchingStock = PartsMarket.Stock(overHeld, overHeld.catalog.Find(offer.id));
            overHeld.inventory.cart.supply[0].units = matchingStock.units + 1;
            ValidationTotal(trace, "reservation-exceeds-current-stock", overHeld, carId, sale, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                "first sale ValidateForQuote / reservation ceiling", typeof(InvalidOperationException).FullName, "Reserved stock exceeds its original supply.");

            var duplicateRows = CartPreviewLiteralTestClone.Copy(held);
            var originalSupply = duplicateRows.inventory.cart.supply[0];
            duplicateRows.inventory.cart.supply.Add(new ReservedPartSupply { definitionId = originalSupply.definitionId, units = 0, generation = originalSupply.generation });
            string duplicateError = MechanicsOwnership.Validate(duplicateRows, duplicateRows.catalog);
            var duplicateQuote = CompareTotal(trace, "reservation-duplicate-zero-row-control", duplicateRows, carId, sale,
                ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(duplicateError == null && !duplicateQuote.Threw,
                "reservation validator accepts a duplicate zero-unit supply row when bounds remain valid / validate=" + duplicateError + ";total=" + duplicateQuote.Text);
            var missingRows = CartPreviewLiteralTestClone.Copy(held);
            missingRows.inventory.cart.supply.Clear();
            string missingError = MechanicsOwnership.Validate(missingRows, missingRows.catalog);
            var missingQuote = CompareTotal(trace, "reservation-missing-supply-row-control", missingRows, carId, sale,
                ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(missingError == null && !missingQuote.Threw,
                "reservation validator accepts a missing supply row when no ceiling is exceeded / validate=" + missingError + ";total=" + missingQuote.Text);

            var stale = CartPreviewLiteralTestClone.Copy(held);
            stale.vehicleStock.generation = 1; stale.inventory.market.generation = 1;
            stale.inventory.cart.generation = 1; stale.inventory.cart.supply[0].generation = 0;
            string staleError = MechanicsOwnership.Validate(stale, stale.catalog);
            var staleQuote = CompareTotal(trace, "reservation-older-generation-allowed", stale, carId, sale,
                ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(staleError == null && !staleQuote.Threw && CartReservations.Held(stale, stale.catalog.Find(offer.id), true) == 0,
                "older-generation hold is valid but does not count as current stock / validate=" + staleError + ";total=" + staleQuote.Text);

            ValidationAuthoredBasketOverflowCheck(trace, source, carId);
        }

        static void ValidationAuthoredBasketOverflowCheck(Trace trace, PlayerProfile profile, string carId)
        {
            if (profile.inventory.cart != null)
            { trace.Assert(false, "overflow bound starts without an active reservation"); return; }
            var car = profile.cars.Find(value => value.instanceId == carId);
            var eligible = profile.catalog.parts.Where(definition => definition != null && definition.price >= 0 &&
                    profile.reputation >= definition.reputationRequired && PartsMarket.FitsAny(profile, definition))
                .GroupBy(definition => definition.id, StringComparer.Ordinal).Select(group => group.First())
                .OrderByDescending(definition => InventoryPricing.NewSet(definition)).ThenBy(definition => definition.id, StringComparer.Ordinal).Take(100).ToArray();
            if (eligible.Length != 100)
            {
                trace.Add("VALIDATION_OPEN|overflow-100xq99|required=100;found=" + eligible.Length);
                trace.Assert(false, "checked-total boundary fixture has 100 distinct actual authored eligible definitions"); return;
            }
            var remaining = new System.Collections.Generic.Dictionary<NewPartStock, int>();
            long expected = 0; int maxPrice = 0, maxUnits = 0;
            var rows = new PartCartLine[eligible.Length];
            foreach (var pair in eligible.Select((definition, index) => new { definition, index }))
            {
                var definition = pair.definition;
                int units = PartsMarket.Count(profile, car, definition), basePrice = InventoryPricing.NewSet(definition);
                maxPrice = Math.Max(maxPrice, basePrice); maxUnits = Math.Max(maxUnits, units);
                var stock = PartsMarket.Stock(profile, definition);
                int available = 0, percent = stock?.deliveryPercent ?? 10;
                if (stock != null)
                {
                    if (!remaining.TryGetValue(stock, out available))
                        available = Math.Max(0, stock.units - CartReservations.Held(profile, definition, true));
                }
                for (int quantity = 0; quantity < 99; quantity++)
                {
                    int local = Math.Min(units, available), express = units - local;
                    int fee = express == 0 ? 0 : checked((int)Math.Ceiling(basePrice * (express / (double)units) * percent / 100.0));
                    expected += (long)basePrice + fee;
                    if (stock != null) { available -= local; remaining[stock] = available; }
                }
                rows[pair.index] = new PartCartLine { action = CartAction.NewSet, definitionId = definition.id, quantity = 99, install = false };
            }
            long maxAuthoredSetPrice = Math.Max(1, profile.catalog.parts.Where(definition => definition != null && definition.price >= 0)
                .Select(definition => (long)definition.price).DefaultIfEmpty(0).Max());
            long maxUnitOrSetBase = maxAuthoredSetPrice; // NewUnit is max(1, ceil(price/count)), so it cannot exceed this bound.
            long maximumPerPurchaseWithFee = maxUnitOrSetBase + (long)Math.Ceiling(maxUnitOrSetBase * .10);
            long conservativeNewPurchaseUpperBound = 100L * 99L * maximumPerPurchaseWithFee;
            bool upperBoundExcludesInt32Overflow = conservativeNewPurchaseUpperBound <= int.MaxValue;
            var outcome = CompareTotal(trace, "validation-authored-100xq99-basket", profile, carId, rows,
                ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                expected > int.MaxValue ? typeof(OverflowException).FullName : null,
                expected > int.MaxValue ? new OverflowException().Message : null);
            bool actualOverflow = outcome.ExceptionType == typeof(OverflowException).FullName;
            bool expectedOverflow = expected > int.MaxValue;
            bool exact = expectedOverflow ? outcome.Threw && actualOverflow : !outcome.Threw && Convert.ToInt64(outcome.Value, CultureInfo.InvariantCulture) == expected;
            trace.Assert(exact, "checked 100-distinct-row x quantity-99 result matches manually accumulated actual authored supply / expectedTotal=" + expected + ";actual=" + outcome.Text);
            trace.Add("VALIDATION_OVERFLOW_BOUND|basket=100-distinct-highest-set-price-offers;quantityPerOffer=99;maxBasketSetPrice=" + maxPrice +
                ";maxBasketSetUnits=" + maxUnits + ";basketExpectedLongTotal=" + expected + ";conservativeAllNewPurchaseUpperBound=" + conservativeNewPurchaseUpperBound +
                ";maxAuthoredPrice=" + maxAuthoredSetPrice + ";upperBoundExcludesInt32Overflow=" + upperBoundExcludesInt32Overflow +
                ";intMax=" + int.MaxValue + ";basketOverflow=" + expectedOverflow + ";actual=" + outcome.Text +
                ";first=" + eligible[0].id + ";last=" + eligible[eligible.Length - 1].id);
        }

        static void ValidationPositionsAndMoves(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p, out var carId, ValidationSeed + 1);
            var car = p.cars.Find(value => value.instanceId == carId);
            if (!GraphCreateSameModelPair(p, out var target, out var donor, out var pairReason) || target.instanceId != carId)
            {
                trace.Add("VALIDATION_OPEN|wrong-spec-position|same-model pair unavailable: " + pairReason);
                trace.Assert(false, "wrong-spec fixture has an actual same-model owned pair");
                return;
            }
            var targetPiston = car.installed.slots.Select(binding => p.parts.FirstOrDefault(part => part != null && part.instanceId == binding.partInstanceId))
                .FirstOrDefault(part => part != null && part.slot == PartCategory.Pistons.ToString());
            var targetDefinition = targetPiston == null ? null : p.catalog.Find(targetPiston.definitionId);
            var donorDefinition = targetDefinition == null ? null : p.catalog.parts.Where(definition => definition != null && definition.category == PartCategory.Pistons &&
                    definition.id != targetDefinition.id && definition.price >= 0 && p.reputation >= definition.reputationRequired &&
                    p.catalog.Fits(p.catalog.Spec(target.vehicleId), definition) &&
                    PartsMarket.Quote(p, definition, InventoryUnits.Count(p, donor, definition), false, 1).cost <= p.cash)
                .OrderBy(definition => definition.price).ThenBy(definition => definition.id, StringComparer.Ordinal).FirstOrDefault();
            string donorPackId = null;
            SupplyQuote donorQuote = null;
            if (donorDefinition != null)
            {
                donorQuote = PartsMarket.Quote(p, donorDefinition, InventoryUnits.Count(p, donor, donorDefinition), false, 1);
                donorPackId = PartOperations.Buy(p, p.catalog, donor.instanceId, donorDefinition.id, true);
            }
            var donorPiston = string.IsNullOrEmpty(donorPackId) ? null : p.parts.FirstOrDefault(part => part != null && part.instanceId == donorPackId);
            string donorRole = donorPiston?.condition?.elements?.FirstOrDefault(element => element != null)?.role;
            string donorPosition = donorPiston?.condition?.elements?.FirstOrDefault(element => element != null && element.role == donorRole)?.id;
            var wrongSpec = donorPiston == null || string.IsNullOrEmpty(donorRole) || string.IsNullOrEmpty(donorPosition) ? null : new
            {
                Definition = p.catalog.Find(donorPiston.definitionId),
                TargetDefinition = targetDefinition,
                Role = donorRole,
                Position = donorPosition
            };
            if (wrongSpec == null)
            {
                trace.Add("VALIDATION_OPEN|wrong-spec-position|same-model donor has no affordable distinct authored piston offer fitting the target");
                trace.Assert(false, "wrong-spec fixture has an actual installed target D1 and affordable installed donor D2 fitting the target");
            }
            else
            {
                string setupError = MechanicsOwnership.Validate(p, p.catalog);
                trace.Assert(setupError == null, "same-model D1/D2 fixture remains valid before wrong-position injection / " + setupError);
                if (setupError != null) return;
                var row = new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = wrongSpec.Definition.id,
                    role = wrongSpec.Role, install = true, quantity = 1, positions = new System.Collections.Generic.List<string> { wrongSpec.Position } } };
                ValidationTotal(trace, "wrong-spec-real-position", p, carId, row, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                    "unit-position validation", typeof(InvalidOperationException).FullName,
                    "Select unique fitted positions for installed units, matching the fitted specification. Unselected extras remain loose.");
                trace.Add("VALIDATION_POSITION|targetD1=" + wrongSpec.TargetDefinition.id + ";donorD2=" + wrongSpec.Definition.id +
                    ";donorCar=" + donor.instanceId + ";position=" + wrongSpec.Position + ";quote=" + donorQuote?.cost +
                    ";cash=" + p.cash + ";D2FitsTarget=" + p.catalog.Fits(p.catalog.Spec(car.vehicleId), wrongSpec.Definition));
            }

            var saleFixture = ValidationSaleFixture(trace, out var saleCarId, out _);
            LooseEngineElement looseUnit = null;
            if (saleFixture != null)
            {
                var unitCar = saleFixture.cars.Find(value => value.instanceId == saleCarId);
                var unitOffer = saleFixture.catalog.parts.FirstOrDefault(definition => definition != null && definition.price >= 0 &&
                    saleFixture.catalog.Fits(saleFixture.catalog.Spec(unitCar.vehicleId), definition) &&
                    InventoryUnits.Count(saleFixture, unitCar, definition) > 1 && InventoryUnits.Roles(saleFixture, unitCar, definition).Length > 0 &&
                    PartsMarket.Quote(saleFixture, definition, InventoryUnits.Count(saleFixture, unitCar, definition), true, 1).cost <= saleFixture.cash);
                if (unitOffer != null)
                {
                    string role = InventoryUnits.Roles(saleFixture, unitCar, unitOffer).First();
                    var quote = PartsMarket.Quote(saleFixture, unitOffer, InventoryUnits.Count(saleFixture, unitCar, unitOffer), true, 1);
                    var boughtUnit = InventoryUnits.Buy(saleFixture, saleCarId, unitOffer.id, role);
                    looseUnit = saleFixture.looseEngineElements.First(unit => unit.element.id == boughtUnit.element.id);
                    trace.Add("VALIDATION_UNIT_SETUP|definition=" + unitOffer.id + ";role=" + role + ";quote=" + quote.cost +
                        ";cashAfterTwoPartBuys=" + saleFixture.cash + ";unit=" + looseUnit.element.id);
                }
            }
            if (saleFixture == null || looseUnit == null || looseUnit.element == null)
            {
                trace.Add("VALIDATION_OPEN|wrong-unit-destination|no compatible authored multi-member offer could be bought into real owned unit inventory");
                trace.Assert(false, "unit destination fixture has an actual owned physical unit");
            }
            else
            {
                var line = new[] { new PartCartLine { action = CartAction.Move, assetId = looseUnit.element.id,
                    destination = InventoryKind.Engine, destinationId = saleFixture.engines.First(engine => !string.IsNullOrEmpty(engine.parentVehicleId)).instanceId } };
                ValidationTotal(trace, "loose-unit-wrong-destination", saleFixture, saleCarId, line, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                    "move destination validation", typeof(InvalidOperationException).FullName,
                    "Choose a fitted position to install an individual unit.");
            }

            CartPreviewCharacterizationFixtures.CreateNormalized(out var collisions, out var collisionCarId, ValidationSeed + 2);
            var collisionCar = collisions.cars.Find(value => value.instanceId == collisionCarId);
            var collisionOffer = collisions.catalog.parts.FirstOrDefault(definition => definition != null && definition.category == PartCategory.HeadIntake &&
                definition.price >= 0 && collisions.catalog.Fits(collisions.catalog.Spec(collisionCar.vehicleId), definition));
            if (collisionOffer == null) { trace.Assert(false, "slot collision fixture has a real authored part definition"); return; }
            string firstId = PartOperations.Buy(collisions, collisions.catalog, collisionCarId, collisionOffer.id, false);
            string secondId = PartOperations.Buy(collisions, collisions.catalog, collisionCarId, collisionOffer.id, false);
            if (firstId == secondId || string.IsNullOrEmpty(firstId) || string.IsNullOrEmpty(secondId))
            { trace.Assert(false, "slot collision fixture creates two actual distinct loose physical parts"); return; }
            var collisionsRows = new[] {
                new PartCartLine { action = CartAction.Move, assetId = firstId, destination = InventoryKind.Car, destinationId = collisionCarId },
                new PartCartLine { action = CartAction.Move, assetId = secondId, destination = InventoryKind.Car, destinationId = collisionCarId }
            };
            ValidationTotal(trace, "distinct-assets-same-destination-slot", collisions, collisionCarId, collisionsRows, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks,
                "ordered move slot validation", typeof(InvalidOperationException).FullName, "Conflicting slot installations.");
        }

        static PlayerProfile ValidationSaleFixture(Trace trace, out string carId, out string partId)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile, out carId, ValidationSeed);
            string selectedCarId = carId;
            var car = profile.cars.Find(value => value.instanceId == selectedCarId);
            var offer = car == null ? null : profile.catalog.parts.Where(definition => definition != null && definition.price >= 0 &&
                    profile.reputation >= definition.reputationRequired && profile.catalog.Fits(profile.catalog.Spec(car.vehicleId), definition) &&
                    InventoryUnits.Count(profile, car, definition) > 1)
                .OrderBy(definition => definition.price).ThenBy(definition => definition.id, StringComparer.Ordinal)
                .FirstOrDefault(definition => PartsMarket.Quote(profile, definition, InventoryUnits.Count(profile, car, definition), false, 2).cost <= profile.cash);
            if (offer == null)
            {
                trace?.Add("VALIDATION_OPEN|saleable-member-fixture|no authored multi-member offer has a two-purchase quote within actual fixture cash");
                partId = null; return null;
            }
            var setupQuote = PartsMarket.Quote(profile, offer, InventoryUnits.Count(profile, car, offer), false, 2);
            trace?.Add("VALIDATION_SALE_SETUP|car=" + carId + ";definition=" + offer.id + ";count=" + InventoryUnits.Count(profile, car, offer) +
                ";twoPurchaseCost=" + setupQuote.cost + ";cash=" + profile.cash + ";local=" + setupQuote.localUnits + ";express=" + setupQuote.expressUnits);
            PartOperations.Buy(profile, profile.catalog, carId, offer.id, false);
            PartOperations.Buy(profile, profile.catalog, carId, offer.id, false);
            var part = profile.parts.LastOrDefault(item => item != null && item.location == PartLocation.Inventory && item.definitionId == offer.id);
            partId = part?.instanceId;
            if (part == null) return null;
            string invalid = MechanicsOwnership.Validate(profile, profile.catalog);
            if (invalid != null) throw new InvalidOperationException("Validation fixture setup is invalid: " + invalid);
            return profile;
        }

        static void ValidationTotal(Trace trace, string name, PlayerProfile profile, string carId,
            PartCartLine[] rows, ValuationPolicy policy, long now, string stage, string expectedType, string expectedMessage)
        {
            var result = CompareTotal(trace, "validation-" + name, profile, carId, rows, policy, now, expectedType, expectedMessage);
            bool exact = result.Threw && result.ExceptionType == expectedType && result.ExceptionMessage == expectedMessage;
            trace.Assert(exact, "validation first error is exact / " + name + " / " + stage + " / " + result.Text);
            trace.Add("VALIDATION_CASE|" + name + "|stage=" + stage + "|type=" + result.ExceptionType + "|message=" + result.ExceptionMessage);
        }

        static void ValidationTotal(Trace trace, string name, PlayerProfile profile, string carId,
            PartCartLine[] rows, ValuationPolicy policy, long now)
        {
            var result = CompareTotal(trace, "validation-" + name, profile, carId, rows, policy, now);
            trace.Assert(!result.Threw, "validation control succeeds / " + name + " / " + result.Text);
            trace.Add("VALIDATION_CONTROL|" + name + "|actual=" + result.Text);
        }
    }
}
#endif
