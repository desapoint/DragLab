#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    /// <summary>Finite Editor-only commerce and reservation characterization for S06d2b.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint CommerceSeed = 20261028u;

        static void RunCommerceContracts(Trace trace, string subset)
        {
            if (subset != "commerce" && subset != "commerce-mixed" && subset != "commerce-owner" && subset != "commerce-lifetime")
                throw new ArgumentException("Unknown commerce subset: " + subset, nameof(subset));
            if (subset == "commerce" || subset == "commerce-mixed") CommerceGroup(trace, "mixed", CommerceMixed);
            if (subset == "commerce" || subset == "commerce-owner") CommerceGroup(trace, "owner", CommerceOwner);
            if (subset == "commerce" || subset == "commerce-lifetime") CommerceGroup(trace, "lifetime", CommerceLifetime);
            trace.Add("COMMERCE|scope=" + subset + ";status=step1-subset");
        }

        static void CommerceGroup(Trace trace, string name, Action<Trace> body)
        {
            try { body(trace); }
            catch (Exception error)
            {
                trace.Assert(false, "Commerce fixture completed / " + name + " / " + error.GetType().FullName + " | " + error.Message);
                trace.Add("COMMERCE_OPEN|" + name + "|group exception retained as failure");
            }
        }

        static void CommerceMixed(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p, out var carId, CommerceSeed);
            var car = p.cars.Find(value => value.instanceId == carId);
            var multi = car == null ? null : p.catalog.parts.Where(d => d != null && d.price >= 0 && p.catalog.Fits(p.catalog.Spec(car.vehicleId), d) &&
                    InventoryUnits.Count(p, car, d) > 1 && InventoryUnits.Roles(p, car, d).Length > 0 && PartsMarket.Stock(p, d) != null)
                .OrderBy(d => d.id, StringComparer.Ordinal).FirstOrDefault();
            var multiStock = multi == null ? null : PartsMarket.Stock(p, multi);
            if (multi == null || multiStock == null)
            { trace.Assert(false, "mixed commerce fixture has an authored compatible multi-member stocked offer"); return; }

            string soldId = PartOperations.Buy(p, p.catalog, carId, multi.id, false);
            var sold = p.parts.FirstOrDefault(part => part.instanceId == soldId && part.location == PartLocation.Inventory);
            var usedPartSeed = CommerceSeed ^ 0x6c8e9cf5u;
            var usedPart = CartPreviewCharacterizationFixtures.AddUsedPart(p, "commerce/used-part", ref usedPartSeed);
            var usedUnitSeed = CommerceSeed ^ 0x85ebca6bu;
            var usedUnit = CartPreviewCharacterizationFixtures.AddUsedUnit(p, "commerce/used-unit", ref usedUnitSeed);
            if (sold == null || usedPart?.IsPart != true || usedUnit?.IsUnit != true)
            { trace.Assert(false, "mixed basket uses actual loose sale, used-part and used-unit assets"); return; }

            var single = car == null ? null : p.catalog.parts.Where(d => d != null && d.price >= 0 && p.catalog.Fits(p.catalog.Spec(car.vehicleId), d) &&
                    InventoryUnits.Count(p, car, d) == 1 && PartsMarket.Stock(p, d) != null &&
                    PartsMarket.ProductKey(d) != PartsMarket.ProductKey(multi))
                .OrderBy(d => d.id, StringComparer.Ordinal).FirstOrDefault();
            var singleStock = single == null ? null : PartsMarket.Stock(p, single);
            if (single == null || singleStock == null)
            { trace.Assert(false, "mixed basket has a second authored product with a distinct stock row"); return; }

            int setCount = InventoryUnits.Count(p, car, multi);
            string role = InventoryUnits.Roles(p, car, multi).FirstOrDefault();
            if (string.IsNullOrEmpty(role)) { trace.Assert(false, "multi-member offer has an actual modeled unit role"); return; }

            // Controlled quantities alter only two existing authored stock rows and retain valid schema bounds.
            multiStock.units = setCount + 1; multiStock.initialUnits = setCount + 1; multiStock.deliveryPercent = 5;
            singleStock.units = 1; singleStock.initialUnits = 1; singleStock.deliveryPercent = 10;
            string valid = MechanicsOwnership.Validate(p, p.catalog);
            trace.Assert(valid == null, "controlled two-product stock remains globally valid / " + (valid ?? "valid"));
            if (valid != null) return;

            var rows = new[]
            {
                new PartCartLine { action=CartAction.NewSet, definitionId=multi.id, quantity=1 },
                new PartCartLine { action=CartAction.NewUnits, definitionId=multi.id, role=role, quantity=2 },
                new PartCartLine { action=CartAction.NewSet, definitionId=single.id, quantity=2 },
                new PartCartLine { action=CartAction.BuyUsed, definitionId=usedPart.DefinitionId, assetId=usedPart.Id, quantity=1 },
                new PartCartLine { action=CartAction.BuyUsed, definitionId=usedUnit.DefinitionId, assetId=usedUnit.Id, role=usedUnit.unit.element.role, quantity=1 },
                new PartCartLine { action=CartAction.SellPart, definitionId=sold.definitionId, assetId=soldId, quantity=1 }
            };
            var policy = ValuationPolicy.Defaults(); long now = CartPreviewCharacterizationFixtures.FixedNowTicks;
            CartReservations.Capture(p, carId, rows);
            var reservation = p.inventory.cart;
            int heldMulti = reservation.supply.Where(s => s.definitionId == multi.id && s.generation == p.inventory.market.generation).Sum(s => s.units);
            int heldSingle = reservation.supply.Where(s => s.definitionId == single.id && s.generation == p.inventory.market.generation).Sum(s => s.units);
            trace.Assert(CartReservations.Matches(p, carId, rows) && reservation.lines.Count == rows.Length,
                "mixed basket captures exact six action rows for its owner");
            trace.Assert(reservation.supply.Count == 2 && heldMulti == setCount + 1 && heldSingle == 1 &&
                reservation.supply.All(s => s.generation == p.inventory.market.generation) &&
                reservation.supply.Count(s => s.definitionId == multi.id && s.units == setCount + 1) == 1 &&
                reservation.supply.Count(s => s.definitionId == single.id && s.units == 1) == 1,
                "mixed reservation holds current-generation local shelf only / multi=" + heldMulti + ";single=" + heldSingle);

            var setQuote = CommerceExpectedPrice(setCount + 1, setCount, InventoryPricing.NewSet(multi), 1, 5);
            var unitQuote = CommerceExpectedPrice(1, 1, InventoryPricing.NewUnit(multi, setCount), 2, 5);
            var secondQuote = CommerceExpectedPrice(1, 1, InventoryPricing.NewSet(single), 2, 10);
            int expectedFees = checked(setQuote.fee + unitQuote.fee + secondQuote.fee);
            int expectedTotal = checked(setQuote.cost + unitQuote.cost + secondQuote.cost +
                InventoryPricing.BuyUsed(usedPart, policy) + InventoryPricing.BuyUsed(usedUnit, policy) -
                Valuations.LoosePriceOracleForChecks(CartPreviewLiteralTestClone.Copy(p), policy, "wholesale", soldId, false, now));
            var total = CompareTotal(trace, "commerce-mixed-six-rows", p, carId, rows, policy, now);
            var fee = CompareFee(trace, "commerce-mixed-six-rows", p, carId, rows);
            trace.Assert(!total.Threw && Convert.ToInt32(total.Value, CultureInfo.InvariantCulture) == expectedTotal,
                "mixed fixed-time Total matches independently accumulated supply, used and sale amounts / actual=" + total.Text + ";expected=" + expectedTotal);
            trace.Assert(!fee.Threw && Convert.ToInt32(fee.Value, CultureInfo.InvariantCulture) == expectedFees,
                "mixed DeliveryFee equals sequential per-row express ceilings / actual=" + fee.Text + ";expected=" + expectedFees);
            trace.Assert(setQuote.expressUnits == 0 && unitQuote.localUnits == 1 && unitQuote.expressUnits == 1 &&
                secondQuote.localUnits == 1 && secondQuote.expressUnits == 1 && setQuote.fee == 0 && unitQuote.fee > 0 && secondQuote.fee > 0,
                "mixed products exercise local-to-express exhaustion at 5% and 10% / five=" + unitQuote.fee + ";ten=" + secondQuote.fee);
            trace.Add("COMMERCE_MIXED|seed=" + CommerceSeed + ";car=" + carId + ";multi=" + multi.id + ";single=" + single.id +
                ";rows=6;localHeld=" + (heldMulti + heldSingle) + ";generation=" + p.inventory.market.generation +
                ";fee=" + expectedFees + ";total=" + expectedTotal);
        }

        static void CommerceOwner(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p, out var carId, CommerceSeed + 1);
            var car = p.cars.Find(value => value.instanceId == carId);
            var unrelated = CartPreviewCharacterizationFixtures.AddSecondOwnedCar(p);
            VehicleCollection.Select(p, carId);
            var offer = car == null ? null : p.catalog.parts.Where(d => d != null && d.price >= 0 && PartsMarket.Stock(p, d) != null &&
                    p.catalog.Fits(p.catalog.Spec(car.vehicleId), d) && p.catalog.Fits(p.catalog.Spec(unrelated.vehicleId), d))
                .OrderBy(d => d.id, StringComparer.Ordinal).FirstOrDefault();
            if (offer == null) { trace.Assert(false, "two actual owned cars share an authored stocked compatible offer"); return; }
            var stock = PartsMarket.Stock(p, offer); stock.units = Math.Max(1, InventoryUnits.Count(p, car, offer)); stock.initialUnits = stock.units;
            var ownerRows = new[] { new PartCartLine { action=CartAction.NewSet, definitionId=offer.id, quantity=1 } };
            CartReservations.Capture(p, carId, ownerRows);
            int held = CartReservations.Held(p, offer, true);
            var own = CompareTotal(trace, "commerce-owner-cart", p, carId, ownerRows, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks);
            var ownFee = CompareFee(trace, "commerce-owner-cart", p, carId, ownerRows);
            var otherRows = new[] { new PartCartLine { action=CartAction.NewSet, definitionId=offer.id, quantity=1 } };
            trace.Assert(CartReservations.Matches(p, carId, ownerRows) && held > 0 && !CartReservations.Matches(p, unrelated.instanceId, otherRows),
                "reservation belongs only to exact car and same offer / held=" + held + ";other=" + unrelated.instanceId);
            var other = CompareTotal(trace, "commerce-unrelated-car-cart", p, unrelated.instanceId, otherRows, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks);
            var otherFeeOutcome = CompareFee(trace, "commerce-unrelated-car-cart", p, unrelated.instanceId, otherRows);
            var otherStock = PartsMarket.Stock(p, offer);
            int otherUnits = InventoryUnits.Count(p, unrelated, offer);
            int otherAvailable = Math.Max(0, (otherStock?.units ?? 0) - CartReservations.Held(p, offer, true));
            int otherLocal = Math.Min(otherUnits, otherAvailable), otherExpress = otherUnits - otherLocal;
            int otherFee = otherExpress == 0 ? 0 : checked((int)Math.Ceiling(InventoryPricing.NewSet(offer) * (otherExpress / (double)otherUnits) * (otherStock?.deliveryPercent ?? 10) / 100.0));
            int expectedOther = checked(InventoryPricing.NewSet(offer) + otherFee);
            trace.Assert(!own.Threw && Convert.ToInt32(own.Value, CultureInfo.InvariantCulture) == InventoryPricing.NewSet(offer),
                "same-cart owner quote reclaims its exact hold / held=" + held + ";actual=" + own.Text);
            trace.Assert(!ownFee.Threw && Convert.ToInt32(ownFee.Value, CultureInfo.InvariantCulture) == 0,
                "same-cart DeliveryFee sees its fully held local set / actual=" + ownFee.Text);
            trace.Assert(!other.Threw && Convert.ToInt32(other.Value, CultureInfo.InvariantCulture) == expectedOther,
                "same-offer unrelated-car quote uses only unheld shelf and express remainder / available=" + otherAvailable + ";local=" + otherLocal + ";express=" + otherExpress + ";actual=" + other.Text + ";expected=" + expectedOther);
            trace.Assert(!otherFeeOutcome.Threw && Convert.ToInt32(otherFeeOutcome.Value, CultureInfo.InvariantCulture) == otherFee,
                "unrelated-cart DeliveryFee matches its unheld-shelf express fee / actual=" + otherFeeOutcome.Text + ";expected=" + otherFee);
            CartReservations.Capture(p, unrelated.instanceId, otherRows);
            trace.Assert(CartReservations.Matches(p, unrelated.instanceId, otherRows) && !CartReservations.Matches(p, carId, ownerRows),
                "capturing another car replaces ownership without borrowing the first car hold");

            VehicleCollection.Select(p, carId);
            uint changedSeed = CommerceSeed ^ 0x31415928u;
            var changedUsed = CartPreviewCharacterizationFixtures.AddUsedPart(p, "commerce/changed-used", ref changedSeed);
            var usedRows = new[] { new PartCartLine { action=CartAction.BuyUsed, definitionId=changedUsed.DefinitionId, assetId=changedUsed.Id, quantity=1 } };
            CartReservations.Capture(p, carId, usedRows);
            VehicleListings.AfterRace(p, p.catalog); VehicleListings.AfterRace(p, p.catalog); VehicleListings.AfterRace(p, p.catalog);
            int generationBeforeChange = p.inventory.market.generation;
            var changedRows = new[] { new PartCartLine { action=CartAction.NewSet, definitionId=offer.id, quantity=1 } };
            CartReservations.Capture(p, carId, changedRows);
            trace.Assert(CartReservations.Matches(p, carId, changedRows) && p.inventory.cart.generation == generationBeforeChange,
                "changed same-car cart captures current generation after refresh");
            trace.Assert(!p.inventory.dealer.Any(item => item.Id == changedUsed.Id),
                "changed capture removes old used hold only after its market generation becomes stale");
            int cashBeforeClear = p.cash;
            string marketBeforeClear = TypedCanonical(p.inventory.market.parts, new System.Collections.Generic.Dictionary<string, string>());
            CartReservations.Clear(p);
            trace.Assert(p.inventory.cart == null && CartReservations.Held(p, offer, true) == 0,
                "explicit Clear releases active reservation and its held stock");
            trace.Assert(p.cash == cashBeforeClear && marketBeforeClear == TypedCanonical(p.inventory.market.parts, new System.Collections.Generic.Dictionary<string, string>()),
                "Clear releases holds without changing cash or market stock");
            trace.Add("COMMERCE_OWNER|owner=" + carId + ";other=" + unrelated.instanceId + ";definition=" + offer.id +
                ";held=" + held + ";generation=" + generationBeforeChange + ";clear=true");
        }

        static void CommerceLifetime(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p, out var carId, CommerceSeed + 2);
            uint usedSeed = CommerceSeed ^ 0x27182818u;
            var used = CartPreviewCharacterizationFixtures.AddUsedPart(p, "commerce/lifetime-used", ref usedSeed);
            var rows = new[] { new PartCartLine { action=CartAction.BuyUsed, definitionId=used.DefinitionId, assetId=used.Id, quantity=1 } };
            CartReservations.Capture(p, carId, rows);
            bool stayed = true; bool refreshedThree = false;
            for (int completed = 1; completed <= CartReservations.LifetimeRaces + 1; completed++)
            {
                var before = CommerceCaptureState(p);
                var total = CompareTotal(trace, "commerce-lifetime-total-before-" + completed, p, carId, rows, ValuationPolicy.Defaults(), CartPreviewCharacterizationFixtures.FixedNowTicks);
                var fee = CompareFee(trace, "commerce-lifetime-fee-before-" + completed, p, carId, rows);
                var afterPreview = CommerceCaptureState(p);
                bool readOnly = CommerceStateEqual(before, afterPreview, out var readFields, out var readReferences);
                CommerceWriteSnapshot(trace, "lifetime-" + completed.ToString("D2", CultureInfo.InvariantCulture) + "-read", before, afterPreview, readFields, readReferences);
                trace.Assert(readOnly && !total.Threw && !fee.Threw,
                    "Total and DeliveryFee remain read-only between race mutations / next=" + completed);
                string marketBefore = TypedCanonical(p.inventory.market.parts, new System.Collections.Generic.Dictionary<string, string>());
                string cartBefore = TypedCanonical(p.inventory.cart, new System.Collections.Generic.Dictionary<string, string>());
                int cashBefore = p.cash, runsBefore = p.vehicleStock.completedRuns;
                string[] dealerBefore = p.inventory.dealer.Select(item => item.Id + "\n" + TypedCanonical(item, new System.Collections.Generic.Dictionary<string, string>())).ToArray();
                var heldUsedBefore = p.inventory.dealer.FirstOrDefault(item => item.Id == used.Id);
                string heldUsedFacts = heldUsedBefore == null ? null : TypedCanonical(heldUsedBefore, new System.Collections.Generic.Dictionary<string, string>());
                VehicleListings.AfterRace(p, p.catalog);
                FixtureBaselines.Clear();
                var afterRace = CommerceCaptureState(p);
                bool active = CartReservations.Active(p);
                int expectedGeneration = completed / 3;
                bool usedPresent = p.inventory.dealer.Any(item => item.Id == used.Id);
                bool expectedActive = completed <= CartReservations.LifetimeRaces;
                bool oldDealerFactsPreserved = completed % 3 == 0 || dealerBefore.All(fact =>
                {
                    int separator = fact.IndexOf('\n'); string id = fact.Substring(0, separator), canonical = fact.Substring(separator + 1);
                    var current = p.inventory.dealer.FirstOrDefault(item => item.Id == id);
                    if (id == used.Id && !expectedActive) return current == null;
                    return current != null && TypedCanonical(current, new System.Collections.Generic.Dictionary<string, string>()) == canonical;
                });
                bool heldUsedFactsPreserved = expectedActive
                    ? p.inventory.dealer.Any(item => item.Id == used.Id && TypedCanonical(item, new System.Collections.Generic.Dictionary<string, string>()) == heldUsedFacts)
                    : !usedPresent;
                bool marketStableOutsideRefresh = completed % 3 == 0 || marketBefore == TypedCanonical(p.inventory.market.parts, new System.Collections.Generic.Dictionary<string, string>());
                stayed &= active == expectedActive && p.inventory.market.generation == expectedGeneration &&
                    usedPresent == expectedActive && p.vehicleStock.completedRuns == completed &&
                    completed == runsBefore + 1 && p.cash == cashBefore &&
                    TypedCanonical(p.inventory.cart, new System.Collections.Generic.Dictionary<string, string>()) == cartBefore &&
                    oldDealerFactsPreserved && heldUsedFactsPreserved && marketStableOutsideRefresh && CartReservations.SameCart(p, carId, rows);
                refreshedThree |= completed == 9 && p.inventory.market.generation == 3 && active;
                CommerceStateEqual(afterPreview, afterRace, out var raceFields, out var raceReferences);
                CommerceWriteSnapshot(trace, "lifetime-" + completed.ToString("D2", CultureInfo.InvariantCulture) + "-race", afterPreview, afterRace, raceFields, raceReferences);
                trace.Add("COMMERCE_RACE|completed=" + completed + ";generation=" + p.inventory.market.generation +
                    ";active=" + active + ";expires=" + p.inventory.cart?.expiresRun + ";usedPresent=" + usedPresent +
                    ";dealerRows=" + p.inventory.dealer.Count + ";sourceChars=" + afterRace.SourceLength + ";sourceSha=" + afterRace.SourceHash.Substring(0, 16) +
                    ";sidecar=lifetime-" + completed.ToString("D2", CultureInfo.InvariantCulture));
            }
            trace.Assert(stayed && refreshedThree && p.vehicleStock.completedRuns == 10 && !CartReservations.Active(p),
                "nine-race reservation survives third refresh and expires after completion ten / runs=" + p.vehicleStock.completedRuns);
            trace.Assert(!p.inventory.dealer.Any(item => item.Id == used.Id), "expiry removes the held stale used asset; unrelated refresh changes are retained as exact sidecars");
            trace.Add("COMMERCE_LIFETIME|car=" + carId + ";asset=" + used.Id + ";races=9;refreshes=3;expiredAt=10");
        }

        sealed class CommerceState
        {
            public string SourceHash, CatalogHash;
            public int SourceLength, CatalogLength;
            public System.Collections.Generic.SortedDictionary<string, string> Fields;
            public System.Collections.Generic.SortedDictionary<string, object> References;
        }

        static CommerceState CommerceCaptureState(PlayerProfile p)
        {
            string source = SourceSnapshot(p);
            var state = new CommerceState { SourceLength = source.Length, SourceHash = CommerceHash(source) };
            source = null;
            string catalog = CatalogSnapshot(p);
            state.CatalogLength = catalog.Length; state.CatalogHash = CommerceHash(catalog);
            catalog = null;
            state.Fields = FlattenNormalization(p);
            state.Fields.Remove("$.__catalogContent");
            state.References = NormalizationReferences(p);
            return state;
        }

        static bool CommerceStateEqual(CommerceState before, CommerceState after,
            out System.Collections.Generic.List<string> fields, out System.Collections.Generic.List<string> references)
        {
            fields = NormalizationDiff(before.Fields, after.Fields);
            references = NormalizationReferenceDiff(before.References, after.References);
            return before.SourceLength == after.SourceLength && before.SourceHash == after.SourceHash &&
                before.CatalogLength == after.CatalogLength && before.CatalogHash == after.CatalogHash &&
                fields.Count == 0 && references.Count == 0;
        }

        static void CommerceWriteSnapshot(Trace trace, string label, CommerceState before, CommerceState after,
            System.Collections.Generic.List<string> fields, System.Collections.Generic.List<string> references)
        {
            string safe = new string(label.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
            string prefix = Path.Combine(trace.ReportFolder, "commerce-" + safe);
            var summary = new StringBuilder();
            summary.Append("BEFORE sourceChars=").Append(before.SourceLength).Append(" sourceSha256=").Append(before.SourceHash)
                .Append(" catalogChars=").Append(before.CatalogLength).Append(" catalogSha256=").Append(before.CatalogHash)
                .Append("\nAFTER sourceChars=").Append(after.SourceLength).Append(" sourceSha256=").Append(after.SourceHash)
                .Append(" catalogChars=").Append(after.CatalogLength).Append(" catalogSha256=").Append(after.CatalogHash).Append('\n');
            File.WriteAllText(prefix + "-hashes.txt", summary.ToString(), new UTF8Encoding(false));
            File.WriteAllLines(prefix + "-field-diff.txt", fields, new UTF8Encoding(false));
            File.WriteAllLines(prefix + "-reference-diff.txt", references, new UTF8Encoding(false));
        }

        static string CommerceHash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
        }

        sealed class CommercePrice
        {
            public int cost, fee, localUnits, expressUnits;
        }

        static CommercePrice CommerceExpectedPrice(int available, int units, int basePrice, int quantity, int percent)
        {
            var expected = new CommercePrice();
            for (int i = 0; i < quantity; i++)
            {
                int local = Math.Min(units, available), express = units - local;
                int fee = express == 0 ? 0 : checked((int)Math.Ceiling(basePrice * (express / (double)units) * percent / 100.0));
                expected.localUnits = checked(expected.localUnits + local);
                expected.expressUnits = checked(expected.expressUnits + express);
                expected.fee = checked(expected.fee + fee);
                expected.cost = checked(expected.cost + basePrice + fee);
                available -= local;
            }
            return expected;
        }
    }
}
#endif
