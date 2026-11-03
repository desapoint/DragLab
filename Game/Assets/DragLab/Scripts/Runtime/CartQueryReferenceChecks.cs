#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Editor-only liveness checks for the pre-L1D frozen cart-query closure.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint CartQueryReferenceSeed = 20261103u;
        const int CartQueryIdentityCapacity = 8192;

        sealed class CartQueryReferenceCall
        {
            internal object Value;
            internal Exception Error;
            internal RuntimeEntropy.IdentityObservation Identities;
            internal long JsonCalls;
            internal bool Threw => Error != null;
            internal string Text => Error == null
                ? Convert.ToString(Value, CultureInfo.InvariantCulture)
                : C0Error(Error);
        }

        sealed class CartQueryCountingRows : IEnumerable<PartCartLine>
        {
            readonly PartCartLine[] rows;
            readonly int throwAfter;
            readonly string throwMessage;
            readonly int throwOnEnumerator;
            internal int Enumerators, MoveNextCalls, Yielded;
            internal int ThrowAfter => throwAfter;
            internal string ThrowMessage => throwMessage;
            internal int RowCount => rows == null ? -1 : rows.Length;
            internal CartQueryCountingRows(PartCartLine[] values, int throwAfter = -1, string throwMessage = null, int throwOnEnumerator = 1)
            { rows = values; this.throwAfter = throwAfter; this.throwMessage = throwMessage ?? "cart-query-enumerator-failure"; this.throwOnEnumerator = throwOnEnumerator; }
            public IEnumerator<PartCartLine> GetEnumerator()
            {
                Enumerators++;
                return Enumerate().GetEnumerator();
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            IEnumerable<PartCartLine> Enumerate()
            {
                if (rows == null) throw new InvalidOperationException("Counting rows require a backing array.");
                for (int i = 0; i < rows.Length; i++)
                {
                    MoveNextCalls++;
                    if (throwAfter == i && Enumerators == throwOnEnumerator) throw new InvalidOperationException(throwMessage);
                    Yielded++;
                    yield return rows[i];
                }
                MoveNextCalls++;
                if (throwAfter == rows.Length && Enumerators == throwOnEnumerator) throw new InvalidOperationException(throwMessage);
            }
        }

        sealed class CartQueryReferenceCase
        {
            internal readonly string Name;
            internal readonly PlayerProfile Baseline;
            internal readonly C0Fixture Fixture;
            internal CartQueryReferenceCase(string name, PlayerProfile baseline, C0Fixture fixture)
            { Name = name; Baseline = baseline; Fixture = fixture; }
        }

        /// <summary>
        /// Runs live-versus-frozen legacy query liveness. This intentionally does
        /// not call or certify the forthcoming typed query implementation.
        /// </summary>
        public static void RunCartQueryReferenceLiveness(Action<bool, string> check, string folder)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("An isolated Editor report folder is required.", nameof(folder));
            Directory.CreateDirectory(folder);
            var trace = new Trace(check, folder, true);
            trace.Add("S06D2B1L1D_CART_QUERY_REFERENCE_LIVENESS_V1");
            trace.Add("baseline=production-e261080;checker=CartPreviewCharacterizationChecks.RunCartQueryReferenceLiveness;live=pre-L1D-PartCart-and-Valuations;frozen=LegacyCartQueryReference;typedCandidate=NONE;fixtures=independent-authored-clones;global-clock-rng-overrides=NONE");
            try
            {
                CartQueryReferenceSourceHashes(trace);
                var shared = new List<int> { 1 };
                var aliasGraph = new NormalizationWalkerHolder { left = shared, right = shared };
                var splitGraph = new NormalizationWalkerHolder { left = new List<int> { 1 }, right = new List<int> { 1 } };
                var otherShared = new List<int> { 2 };
                var mappedGraph = new NormalizationWalkerHolder { left = otherShared, right = otherShared };
                trace.Assert(!CartQueryTopologyMatches(aliasGraph, splitGraph), "query topology comparator rejects lost aliases despite equal values");
                trace.Assert(CartQueryTopologyMatches(aliasGraph, mappedGraph) &&
                    TypedCanonical(aliasGraph, new Dictionary<string, string>()) != TypedCanonical(mappedGraph, new Dictionary<string, string>()),
                    "query topology comparator separates alias structure from mandatory scalar/identity graph comparison");
                CartQueryReferenceTotalEntryAndBounds(trace);
                CartQueryReferenceTotalActionsAndErrors(trace);
                CartQueryReferenceNullOwnerGuards(trace);
                CartQueryReferenceReservationOptions(trace);
                CartQueryReferenceFeeTraversal(trace);
                CartQueryReferenceApplyPreview(trace);
                CartQueryReferenceValuation(trace);
                int openPrerequisites = trace.Text.ToString().Split(new[] { "OPEN|" }, StringSplitOptions.None).Length - 1;
                trace.Assert(openPrerequisites == 0,
                    "every required authored-topology fixture/vector resolved without an OPEN skip / remaining=" + openPrerequisites);
                trace.Add("GATES|typed-query-parity=NOT-RUN;typed-source-purity=NOT-CLAIMED;production-edit=NOT-AUTHORIZED;independent-closure-provenance=must-pass-high-before-domain-edit");
            }
            catch (Exception error)
            {
                trace.Assert(false, "cart-query reference liveness runner completed / " + C0Error(error));
                trace.Add("RUNNER_EXCEPTION|" + C0Error(error));
            }
            finally
            {
                File.WriteAllText(Path.Combine(folder, "S06D2B1L1D_REFERENCE_LIVENESS.txt"),
                    trace.Text.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
            }
        }

        static void CartQueryReferenceSourceHashes(Trace trace)
        {
            string root = Path.Combine(Application.dataPath, "DragLab", "Scripts");
            foreach (string relative in new[]
            {
                "Domain/PartCart.cs", "Domain/CartReservations.cs", "Domain/PartsMarket.cs", "Domain/Valuation.cs",
                "Domain/MechanicsOwnership.cs", "Domain/InventoryUnits.cs", "Domain/SharedInventory.cs",
                "Domain/RuntimeEntropy.cs", "Domain/RuntimeEntropy.Diagnostics.cs",
                "Runtime/LegacyCartPreviewOracle.cs", "Runtime/LegacyC1CartApplyReference.cs",
                "Runtime/LegacyMechanicalCatalogReference.cs", "Runtime/LegacyScopedSupplyReference.cs",
                "Runtime/LegacyCartQueryReference.cs", "Runtime/LegacyCartQueryValuationReference.cs",
                "Runtime/CartPreviewCharacterizationFixtures.cs", "Runtime/CartPreviewLiteralTestClone.cs",
                "Runtime/CartApplyReferenceChecks.cs", "Runtime/OrderedCartApplyChecks.cs",
                "Runtime/CartQueryReferenceChecks.cs", "../Editor/CartPreviewCharacterizationVerification.cs"
            })
            {
                string path = Path.Combine(root, relative);
                bool exists = File.Exists(path);
                trace.Assert(exists, "liveness provenance path exists / " + relative);
                trace.Add("QUERY_REFERENCE_SOURCE_SHA256|" + relative + "|" + (exists ? HashFile(path) : "MISSING"));
            }
            trace.Add("QUERY_REFERENCE_BASELINE_MANIFEST|GamePaths=2163;DomainFiles=266;ExistingLegacyReferences=24;manifest=reports/save-refactor/S06D2B1L1D/pre-query-start-source-sha256.txt;production=27BD75E0FDA4266B76D9FA8CD958CFA803A71FE2F9FB8DA6F9BF5DD7897E9996");
        }

        static void CartQueryReferenceTotalEntryAndBounds(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, CartQueryReferenceSeed + 1);
            var car = source.cars.Find(item => item.instanceId == carId);
            var set = car == null ? null : FindOffer(source, car, PartCategory.HeadIntake, false);
            if (set == null)
            { trace.Assert(false, "Total entry/bounds fixture has an actual authored stocked set"); return; }

            CartQueryTotalCase(trace, "total-new-set-one", source, carId,
                new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 } });
            CartQueryTotalCase(trace, "total-cold-catalog-index-publication", source, carId,
                new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 } },
                expectedSuccess: true, shape: C0CatalogShape.ColdIndex);
            CartQueryTotalCase(trace, "total-explicit-time-no-sale", source, carId,
                new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 } },
                now: CartPreviewCharacterizationFixtures.FixedNowTicks, expectedSuccess: true, expectedClockReads: 0);
            CartQueryTotalCase(trace, "total-empty", source, carId, Array.Empty<PartCartLine>(), expectedSuccess: false, expectedClockReads: 0,
                expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Cart needs 1–100 distinct offers.");
            CartQueryTotalCase(trace, "total-null-rows", source, carId, null, expectedSuccess: false, expectedClockReads: 0,
                expectedErrorType: typeof(ArgumentNullException), expectedErrorMessage: new ArgumentNullException("source").Message, expectedErrorParameter: "source");
            CartQueryTotalCase(trace, "total-null-row", source, carId, new PartCartLine[] { null }, expectedSuccess: false, expectedClockReads: 1,
                expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Invalid cart action or quantity.");
            CartQueryTotalCase(trace, "total-invalid-action", source, carId,
                new[] { new PartCartLine { action = (CartAction)999, definitionId = set.id, quantity = 1 } }, expectedSuccess: false, expectedClockReads: 1,
                expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Invalid cart action or quantity.");
            CartQueryTotalCase(trace, "total-quantity-zero", source, carId,
                new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 0 } }, expectedSuccess: false, expectedClockReads: 1,
                expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Invalid cart action or quantity.");
            CartQueryTotalCase(trace, "total-quantity-100", source, carId,
                new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 100 } }, expectedSuccess: false, expectedClockReads: 1,
                expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Invalid cart action or quantity.");
            CartQueryTotalCase(trace, "total-101-before-row-validation", source, carId,
                Enumerable.Range(0, 101).Select(_ => (PartCartLine)null).ToArray(), expectedSuccess: false, expectedClockReads: 0,
                expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Cart needs 1–100 distinct offers.");
            CartQueryTotalCase(trace, "total-100-reaches-first-row-validation", source, carId,
                Enumerable.Range(0, 100).Select(_ => (PartCartLine)null).ToArray(), expectedSuccess: false, expectedClockReads: 1,
                expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Invalid cart action or quantity.");
            CartQueryTotalCase(trace, "total-target-before-null-source", source, "missing-query-reference-car", null,
                expectedSuccess: false, expectedClockReads: 0,
                expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Service vehicle no longer exists.");

            var hundred = source.catalog.parts.Where(definition => definition != null && definition.price >= 0 &&
                    definition.reputationRequired <= source.reputation && source.catalog.Fits(source.catalog.Spec(car.vehicleId), definition))
                .GroupBy(definition => definition.id, StringComparer.Ordinal).Select(group => group.First())
                .OrderBy(definition => definition.price).ThenBy(definition => definition.id, StringComparer.Ordinal)
                .Take(100).ToArray();
            trace.Assert(hundred.Length == 100, "actual authored catalogue supplies 100 distinct definitions fitting the owned car / count=" + hundred.Length);
            if (hundred.Length == 100)
                CartQueryTotalCase(trace, "total-100-distinct-authored-valid-rows", source, carId,
                    hundred.Select(definition => new PartCartLine { action = CartAction.NewSet, definitionId = definition.id, quantity = 1 }).ToArray(), expectedSuccess: true);
            else trace.Add("OPEN|total-100-distinct-authored-valid-rows|count=" + hundred.Length + ";no synthetic definitions or omitted failure");

            var q99Definition = source.catalog.parts
                .Where(definition => definition != null && definition.price >= 0 && definition.reputationRequired <= source.reputation &&
                    source.catalog.Fits(source.catalog.Spec(car.vehicleId), definition))
                .OrderBy(definition => definition.price).ThenBy(definition => definition.id, StringComparer.Ordinal)
                .FirstOrDefault(definition => source.inventory.market.parts.Any(stock => stock != null && stock.units > 0 && stock.units < 99 &&
                    stock.definitions != null && stock.definitions.Contains(definition.id)));
            if (q99Definition != null)
            {
                var q99Rows = new[] { new PartCartLine { action = CartAction.NewSet, definitionId = q99Definition.id, quantity = 99 } };
                var quoteLedger = new LegacyScopedSupplyReference.Ledger(source);
                var independentQuote = quoteLedger.Quote(q99Definition, InventoryUnits.Count(source, car, q99Definition),
                    q99Definition.price, 99);
                var q99Result = CartQueryTotalCase(trace, "total-q99-authored-mixed-local-express", source, carId, q99Rows,
                    expectedSuccess: true);
                trace.Assert(independentQuote.localUnits > 0 && independentQuote.expressUnits > 0 &&
                    q99Result?.Value is int total && total == independentQuote.cost,
                    "q99 Total equals independent legacy ledger cost with both local and express supply / " + q99Definition.id +
                    " / local=" + independentQuote.localUnits + ";express=" + independentQuote.expressUnits +
                    ";cost=" + independentQuote.cost + ";total=" + q99Result?.Text);
            }
            else trace.Add("OPEN|total-q99-authored-mixed-local-express|no actual fitting market row with 1-98 units; no quantity reduction or synthetic stock");

            var throwing = new CartQueryCountingRows(new[]
            {
                new PartCartLine { action = CartAction.SellPart, definitionId = set.id, assetId = "ignored-before-materialization", quantity = 1 },
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 }
            }, 1, "total-enumerator-after-one-row");
            CartQueryTotalEnumerableCase(trace, "total-enumerator-fails-during-materialization", source, carId, throwing, expectedClockReads: 0);
            var throwsFirstMove = new CartQueryCountingRows(new[]
            {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 }
            }, 0, "total-enumerator-before-first-row");
            CartQueryTotalEnumerableCase(trace, "total-enumerator-fails-before-first-row", source, carId, throwsFirstMove, expectedClockReads: 0);

            var rebuildBlocked = CartPreviewLiteralTestClone.Copy(source);
            var blockedCar = rebuildBlocked.cars.Find(item => item.instanceId == carId);
            if (blockedCar != null)
            {
                blockedCar.engineRebuild = new EngineRebuildJob { active = true };
                CartQueryTotalCase(trace, "total-rebuild-guard-before-source", rebuildBlocked, carId, null,
                    expectedClockReads: 0, expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException),
                    expectedErrorMessage: "Engine reserved at rebuild shop; collect it before modifying/racing this car.");
            }
            else trace.Assert(false, "rebuild guard fixture retains selected owned car");
        }

        static void CartQueryReferenceTotalActionsAndErrors(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, CartQueryReferenceSeed + 2);
            var car = source.cars.Find(item => item.instanceId == carId);
            var set = car == null ? null : FindOffer(source, car, PartCategory.HeadIntake, false);
            if (set == null) { trace.Assert(false, "Total action fixture has an authored HeadIntake offer"); return; }

            var unitDefinition = source.catalog.parts.Where(definition => definition != null && definition.price >= 0 &&
                    definition.reputationRequired <= source.reputation && source.catalog.Fits(source.catalog.Spec(car.vehicleId), definition) &&
                    InventoryUnits.Roles(source, car, definition).Any(candidateRole =>
                        InventoryUnits.Positions(source, car, definition.id, candidateRole).Length > 0))
                .OrderBy(definition => definition.price).ThenBy(definition => definition.id, StringComparer.Ordinal).FirstOrDefault();
            if (unitDefinition != null)
            {
                string role = InventoryUnits.Roles(source, car, unitDefinition).First(candidateRole =>
                    InventoryUnits.Positions(source, car, unitDefinition.id, candidateRole).Length > 0);
                CartQueryTotalCase(trace, "total-new-units-loose", source, carId,
                    new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = unitDefinition.id, role = role, quantity = 1 } }, expectedSuccess: true);
                var positions = InventoryUnits.Positions(source, car, unitDefinition.id, role);
                if (positions.Length > 0)
                {
                    CartQueryTotalCase(trace, "total-new-units-install-real-position", source, carId,
                        new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = unitDefinition.id, role = role,
                            quantity = 1, install = true, positions = new List<string> { positions[0] } } }, expectedSuccess: true);
                    if (positions.Length > 0)
                        CartQueryTotalCase(trace, "total-new-units-repeated-install-position", source, carId,
                            new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = unitDefinition.id, role = role,
                                quantity = 2, install = true, positions = new List<string> { positions[0], positions[0] } } }, expectedSuccess: false,
                            expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Select unique fitted positions for installed units, matching the fitted specification. Unselected extras remain loose.");
                }
                else trace.Add("OPEN|total-new-units-install-real-position|no authored fitted position for " + unitDefinition.id + "/" + role);
                CartQueryTotalCase(trace, "total-new-units-invalid-role", source, carId,
                    new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = unitDefinition.id, role = "l1d-query/unknown-role", quantity = 1 } },
                    expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException),
                    expectedErrorMessage: "This offer has no individually modeled units of that type.");
            }
            else trace.Add("OPEN|total-new-units|no compatible authored modeled unit offer");

            uint usedPartSeed = CartQueryReferenceSeed ^ 0x165667b1u;
            DealerStock usedPart = CartQueryAddUsedPartForCategory(source, "l1d-query/total-used-part", PartCategory.HeadIntake, ref usedPartSeed);
            if (usedPart != null)
                CartQueryTotalCase(trace, "total-buy-used-actual-part", source, carId,
                    new[] { new PartCartLine { action = CartAction.BuyUsed, definitionId = usedPart.DefinitionId,
                        assetId = usedPart.Id, quantity = 1 } }, expectedSuccess: true);
            else trace.Add("OPEN|total-buy-used-actual-part|bounded actual authored used-part fixture construction failed");

            uint usedUnitSeed = CartQueryReferenceSeed ^ 0x6c8e9cf5u;
            string usedRole = unitDefinition == null ? null : InventoryUnits.Roles(source, car, unitDefinition).FirstOrDefault();
            DealerStock usedUnit = unitDefinition == null || string.IsNullOrEmpty(usedRole) ? null :
                CartQueryAddUsedUnitForCategory(source, "l1d-query/used-unit", unitDefinition.category, usedRole, ref usedUnitSeed);
            if (usedUnit?.unit?.element != null)
            {
                CartQueryTotalCase(trace, "total-buy-used-actual-unit", source, carId,
                    new[] { new PartCartLine { action = CartAction.BuyUsed, definitionId = usedUnit.DefinitionId,
                        assetId = usedUnit.Id, role = usedUnit.unit.element.role, quantity = 1 } }, expectedSuccess: true);
                CartQueryTotalCase(trace, "total-buy-used-unit-wrong-role", source, carId,
                    new[] { new PartCartLine { action = CartAction.BuyUsed, definitionId = usedUnit.DefinitionId,
                        assetId = usedUnit.Id, role = "l1d-query/changed-role", quantity = 1 } }, expectedSuccess: false,
                    expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Used unit role changed.");
            }
            else trace.Add("OPEN|total-buy-used-actual-unit|bounded fixture setup yielded no actual dealer unit");

            string loosePartId = PartOperations.Buy(source, source.catalog, carId, set.id, false);
            CartQueryTotalCase(trace, "total-sale-actual-loose-part", source, carId,
                new[] { new PartCartLine { action = CartAction.SellPart, definitionId = set.id, assetId = loosePartId, quantity = 1 } }, expectedSuccess: true);
            CartQueryTotalCase(trace, "total-explicit-time-sale", source, carId,
                new[] { new PartCartLine { action = CartAction.SellPart, definitionId = set.id, assetId = loosePartId, quantity = 1 } },
                now: CartPreviewCharacterizationFixtures.FixedNowTicks, expectedSuccess: true, expectedClockReads: 0);
            CartQueryTotalCase(trace, "total-sale-missing-owned-asset", source, carId,
                new[] { new PartCartLine { action = CartAction.SellPart, definitionId = set.id, assetId = "l1d-query/missing-sale-asset", quantity = 1 } },
                expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException),
                expectedErrorMessage: "Quote requires an owned loose part without retained descendants.");
            CartQueryTotalCase(trace, "total-unknown-new-definition", source, carId,
                new[] { new PartCartLine { action = CartAction.NewSet, definitionId = "l1d-query/unknown-definition", quantity = 1 } },
                expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException),
                expectedErrorMessage: "Unavailable/incompatible cart offer or reputation.");
            CartQueryTotalCase(trace, "total-buy-used-missing-asset", source, carId,
                new[] { new PartCartLine { action = CartAction.BuyUsed, definitionId = set.id, assetId = "l1d-query/missing-dealer-asset", quantity = 1 } },
                expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Used stock unavailable or used twice.");
            var reputationDefinition = source.catalog.parts.Where(definition => definition != null && definition.price >= 0 &&
                    definition.reputationRequired > 0)
                .OrderBy(definition => definition.reputationRequired).ThenBy(definition => definition.id, StringComparer.Ordinal).FirstOrDefault();
            if (reputationDefinition != null)
            {
                var lowReputationSource = CartPreviewLiteralTestClone.Copy(source);
                lowReputationSource.reputation = 0;
                CartQueryTotalCase(trace, "total-insufficient-reputation", lowReputationSource, carId,
                    new[] { new PartCartLine { action = CartAction.NewSet, definitionId = reputationDefinition.id, quantity = 1 } }, expectedSuccess: false,
                    expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Unavailable/incompatible cart offer or reputation.");
            }
            else trace.Add("OPEN|total-insufficient-reputation|authored catalogue has no positive reputation threshold; no definition modification");
            if (unitDefinition != null)
            {
                string role = InventoryUnits.Roles(source, car, unitDefinition).First(candidateRole =>
                    InventoryUnits.Positions(source, car, unitDefinition.id, candidateRole).Length > 0);
                var looseUnit = InventoryUnits.Buy(source, carId, unitDefinition.id, role);
                CartQueryTotalCase(trace, "total-sale-actual-loose-unit", source, carId,
                    new[] { new PartCartLine { action = CartAction.SellUnit, definitionId = unitDefinition.id,
                        assetId = looseUnit.element.id, quantity = 1 } }, expectedSuccess: true);
                var saleThenBuy = new[]
                {
                    new PartCartLine { action = CartAction.SellUnit, definitionId = unitDefinition.id, assetId = looseUnit.element.id, quantity = 1 },
                    new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 }
                };
                CartQueryTotalCase(trace, "total-sale-first-then-buy-lazy-quote", source, carId, saleThenBuy, expectedSuccess: true);
                var buyThenSale = new[]
                {
                    new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 },
                    new PartCartLine { action = CartAction.SellUnit, definitionId = unitDefinition.id, assetId = looseUnit.element.id, quantity = 1 }
                };
                CartQueryTotalCase(trace, "total-buy-first-then-sale-lazy-quote", source, carId, buyThenSale, expectedSuccess: true);
            }

            var duplicate = new[]
            {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 },
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 }
            };
            CartQueryTotalCase(trace, "total-duplicate-offer-after-first-row", source, carId, duplicate, expectedSuccess: false,
                expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Duplicate new offer.");
            var duplicateMove = new[]
            {
                new PartCartLine { action = CartAction.Move, assetId = loosePartId, destination = InventoryKind.Storage,
                    destinationId = SharedInventory.Storage, quantity = 1 },
                new PartCartLine { action = CartAction.Move, assetId = loosePartId, destination = InventoryKind.Storage,
                    destinationId = SharedInventory.Storage, quantity = 1 }
            };
            CartQueryTotalCase(trace, "total-duplicate-move-after-first-overlay", source, carId, duplicateMove, expectedSuccess: false,
                expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Duplicate move or invalid quantity.");
            CartQueryTotalCase(trace, "total-failing-move-missing-asset", source, carId,
                new[] { new PartCartLine { action = CartAction.Move, assetId = "l1d-query/missing-move-asset",
                    destination = InventoryKind.Storage, destinationId = SharedInventory.Storage, quantity = 1 } }, expectedSuccess: false,
                expectedErrorType: typeof(InvalidOperationException), expectedErrorMessage: "Owned item no longer exists.");

            var secondHead = source.catalog.parts.Where(definition => definition != null && definition.category == PartCategory.HeadIntake &&
                    definition.id != set.id && definition.price >= 0 && definition.reputationRequired <= source.reputation &&
                    source.catalog.Fits(source.catalog.Spec(car.vehicleId), definition))
                .OrderBy(definition => definition.id, StringComparer.Ordinal).FirstOrDefault();
            if (secondHead != null)
                CartQueryTotalCase(trace, "total-conflicting-installed-slot", source, carId, new[]
                {
                    new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1, install = true },
                    new PartCartLine { action = CartAction.NewSet, definitionId = secondHead.id, quantity = 1, install = true }
                }, expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException),
                    expectedErrorMessage: "Only one cart offer can install in each slot. Other offers may be bought as spares.");
            else trace.Add("OPEN|total-conflicting-installed-slot|no second actual compatible authored HeadIntake definition");

            var incompatibleInstall = source.catalog.parts.Where(definition => definition != null && definition.price >= 0 &&
                    definition.reputationRequired <= source.reputation && !source.catalog.Fits(source.catalog.Spec(car.vehicleId), definition))
                .OrderBy(definition => definition.id, StringComparer.Ordinal).FirstOrDefault();
            if (incompatibleInstall != null)
                CartQueryTotalCase(trace, "total-incompatible-authored-install", source, carId,
                    new[] { new PartCartLine { action = CartAction.NewSet, definitionId = incompatibleInstall.id,
                        quantity = 1, install = true } }, expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException),
                    expectedErrorMessage: "Unavailable/incompatible cart offer or reputation.");
            else trace.Add("OPEN|total-incompatible-authored-install|catalog has no authored priced definition incompatible with selected vehicle");

            MechanicsOwnership.Definitions(source, car).TryGetValue(PartCategory.CylinderBlock, out string installedBlockDefinitionId);
            var unpreparedBlock = source.catalog.parts.Where(definition => definition != null && definition.category == PartCategory.CylinderBlock &&
                    definition.id != installedBlockDefinitionId && definition.reputationRequired <= source.reputation &&
                    source.catalog.Fits(source.catalog.Spec(car.vehicleId), definition) && definition.price >= 0)
                .OrderBy(definition => definition.id, StringComparer.Ordinal).FirstOrDefault();
            if (unpreparedBlock != null)
            {
                var unprepared = CartQueryTotalCase(trace, "total-unprepared-distinct-block", source, carId,
                    new[] { new PartCartLine { action = CartAction.NewSet, definitionId = unpreparedBlock.id,
                    quantity = 1, install = true, preparedBlock = false } }, expectedSuccess: false);
                const string unpreparedBlockError = "Prepare the block and its required components before installation.";
                trace.Assert(CartQueryErrorIs(unprepared, typeof(InvalidOperationException), unpreparedBlockError, null),
                    "unprepared different compatible block reaches the exact preparation guard / " + unprepared?.Text);
                CartQueryTotalCase(trace, "total-unprepared-block-skips-readiness", source, carId,
                    new[] { new PartCartLine { action = CartAction.NewSet, definitionId = unpreparedBlock.id,
                    quantity = 1, install = true, preparedBlock = false } }, validateBlocks: false, expectedSuccess: true);
                var prepared = CartQueryTotalCase(trace, "total-prepared-block-readiness-check", source, carId,
                    new[] { new PartCartLine { action = CartAction.NewSet, definitionId = unpreparedBlock.id,
                    quantity = 1, install = true, preparedBlock = true } }, expectedSuccess: null);
                trace.Assert(prepared != null && (prepared.Error == null && prepared.Value is int ||
                    prepared.Error is InvalidOperationException && prepared.Error.Message.StartsWith("Block preparation is incomplete: ", StringComparison.Ordinal)),
                    "prepared different compatible block reaches block Resolve readiness, not availability or unprepared guard / " + prepared?.Text);
            }
            else trace.Add("OPEN|total-unprepared-distinct-block|no authored distinct compatible CylinderBlock");
        }

        static void CartQueryReferenceNullOwnerGuards(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, CartQueryReferenceSeed + 6);
            var car = source.cars.Find(item => item.instanceId == carId);
            var offer = car == null ? null : FindOffer(source, car, PartCategory.HeadIntake, false);
            if (offer == null) { trace.Assert(false, "null-owner guard uses actual authored offer fixture"); return; }
            var rows = new[] { new PartCartLine { action = CartAction.NewSet, definitionId = offer.id, quantity = 1 } };
            var test = CartQueryReferenceBuildCase(trace, "null-owner-total", source, carId, rows);
            if (test == null) return;
            int clockReads = 0;
            Func<long> clock = () => { clockReads++; return RuntimeEntropy.UtcNow.Ticks; };
            var live = CartQueryReferenceInvoke(() => PartCart.Total(null, carId, test.Fixture.LiveRows, ValuationPolicy.Defaults()));
            var frozen = CartQueryReferenceInvoke(() => LegacyCartQueryReference.TotalWithClock(null, carId,
                test.Fixture.FrozenRows, ValuationPolicy.Defaults(), 0, true, false, clock));
            trace.Assert(CartQueryReferenceResultEqual(live, frozen) &&
                CartQueryErrorIs(live, typeof(NullReferenceException), CartQueryNaturalNullReferenceMessage(), null) &&
                CartQueryErrorIs(frozen, typeof(NullReferenceException), CartQueryNaturalNullReferenceMessage(), null) && clockReads == 0,
                "Total null-owner first error is frozen-equivalent and precedes default time / " + live.Text + ";" + frozen.Text + ";clock=" + clockReads);
            trace.Assert(test.Fixture.BaselineBefore == SourceSnapshot(test.Fixture.Baseline) &&
                RowsSnapshot(test.Fixture.LiveRows) == test.Fixture.RowsBefore && RowsSnapshot(test.Fixture.FrozenRows) == test.Fixture.RowsBefore,
                "null-owner Total preserves independent fixture baseline and rows");

            var feeTest = CartQueryReferenceBuildCase(trace, "null-owner-fee", source, carId, rows);
            if (feeTest == null) return;
            var liveFee = CartQueryReferenceInvoke(() => PartCart.DeliveryFee(null, carId, feeTest.Fixture.LiveRows));
            var frozenFee = CartQueryReferenceInvoke(() => LegacyCartQueryReference.DeliveryFee(null, carId, feeTest.Fixture.FrozenRows));
            trace.Assert(CartQueryReferenceResultEqual(liveFee, frozenFee) &&
                CartQueryErrorIs(liveFee, typeof(NullReferenceException), CartQueryNaturalNullReferenceMessage(), null) &&
                CartQueryErrorIs(frozenFee, typeof(NullReferenceException), CartQueryNaturalNullReferenceMessage(), null),
                "DeliveryFee null-owner natural first error matches frozen reference / " + liveFee.Text + ";" + frozenFee.Text);
        }

        static void CartQueryReferenceReservationOptions(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, CartQueryReferenceSeed + 7);
            var car = source.cars.Find(item => item.instanceId == carId);
            var definition = car == null ? null : source.catalog.parts.Where(candidate => candidate != null &&
                    candidate.price > 0 && candidate.reputationRequired <= source.reputation &&
                    source.catalog.Fits(source.catalog.Spec(car.vehicleId), candidate) &&
                    PartsMarket.Available(source, candidate) > 0)
                .OrderBy(candidate => candidate.price).ThenBy(candidate => candidate.id, StringComparer.Ordinal).FirstOrDefault();
            if (definition == null) { trace.Assert(false, "reservation Total vectors use an actual authored stocked offer"); return; }
            CartReservations.Capture(source, carId, new[]
            {
                new PartCartLine { action = CartAction.NewSet, definitionId = definition.id, quantity = 99 }
            });
            var reservation = source.inventory.cart;
            var stock = source.inventory.market.parts.FirstOrDefault(row => row != null && row.definitions.Contains(definition.id));
            if (reservation?.supply == null || stock == null || stock.units <= 0)
            { trace.Add("OPEN|total-owner-reservation-options|actual hold/stock not available for " + definition.id); return; }
            var queryRows = new[] { new PartCartLine { action = CartAction.NewSet, definitionId = definition.id, quantity = 1 } };
            var exactBaseline = CartPreviewLiteralTestClone.Copy(source);
            CartReservations.Capture(exactBaseline, carId, queryRows);
            exactBaseline.inventory.usingCartReservation = true;
            CartQueryTotalCase(trace, "total-exact-active-cart-matches-without-owner-opt-in", exactBaseline, carId,
                queryRows, includeOwnerReservation: false, expectedSuccess: true);
            CartQueryTotalCase(trace, "total-exact-active-cart-matches-with-owner-opt-in", exactBaseline, carId,
                queryRows, includeOwnerReservation: true, expectedSuccess: true);
            int?[,] totals = new int?[2, 2];
            foreach (bool usingReservation in new[] { false, true })
            foreach (bool includeOwner in new[] { false, true })
            {
                string label = "total-owner-reservation-using-" + usingReservation.ToString(CultureInfo.InvariantCulture) +
                    "-include-" + includeOwner.ToString(CultureInfo.InvariantCulture);
                var test = CartQueryReferenceBuildCase(trace, label, source, carId, queryRows);
                if (test == null) continue;
                test.Fixture.Live.inventory.usingCartReservation = usingReservation;
                test.Fixture.Frozen.inventory.usingCartReservation = usingReservation;
                var actualRows = new CartQueryCountingRows(test.Fixture.LiveRows);
                var frozenRows = new CartQueryCountingRows(test.Fixture.FrozenRows);
                var result = CartQueryTotalInvoke(trace, test, actualRows, frozenRows, 0, true, includeOwner, true, 1, label);
                if (result.Value is int total) totals[usingReservation ? 1 : 0, includeOwner ? 1 : 0] = total;
                trace.Assert(test.Fixture.Live.inventory.usingCartReservation == usingReservation &&
                    test.Fixture.Frozen.inventory.usingCartReservation == usingReservation,
                    "Total preserves the independently seeded usingCartReservation source value / " + label);
            }
            bool flagIndependent = totals[0, 0].HasValue && totals[1, 0].HasValue && totals[0, 1].HasValue && totals[1, 1].HasValue &&
                totals[0, 0].Value == totals[1, 0].Value && totals[0, 1].Value == totals[1, 1].Value;
            bool includeChangesQuote = totals[0, 0].HasValue && totals[0, 1].HasValue && totals[0, 0].Value != totals[0, 1].Value;
            trace.Assert(flagIndependent, "Total uses explicit Matches/owner-reservation rules independently of the raw usingCartReservation source flag");
            trace.Assert(includeChangesQuote, "includeOwnerReservation changes the actual same-owner hold quote / " +
                (totals[0, 0].HasValue ? totals[0, 0].Value.ToString(CultureInfo.InvariantCulture) : "missing") + "/" +
                (totals[0, 1].HasValue ? totals[0, 1].Value.ToString(CultureInfo.InvariantCulture) : "missing"));
        }

        static void CartQueryReferenceFeeTraversal(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, CartQueryReferenceSeed + 3);
            var car = source.cars.Find(item => item.instanceId == carId);
            var set = car == null ? null : FindOffer(source, car, PartCategory.HeadIntake, false);
            if (set == null) { trace.Assert(false, "DeliveryFee fixture has an actual authored stocked offer"); return; }
            var rows = new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 } };

            CartQueryFeeCase(trace, "fee-no-cart-one-filter-pass", source, carId, rows, reservation: "none", expectedEnumerators: 1);
            CartQueryFeeCase(trace, "fee-exact-active-cart-two-passes", source, carId, rows, reservation: "exact", expectedEnumerators: 2);
            CartQueryFeeCase(trace, "fee-active-different-lines-two-passes", source, carId, rows, reservation: "changed", expectedEnumerators: 2);
            CartQueryFeeCase(trace, "fee-active-other-car-one-filter-pass", source, carId, rows, reservation: "other-car", expectedEnumerators: 1);
            CartQueryFeeCase(trace, "fee-expired-reservation-one-filter-pass", source, carId, rows, reservation: "expired", expectedEnumerators: 1);
            CartQueryFeeCase(trace, "fee-null-source-no-cart", source, carId, null, reservation: "none", expectedEnumerators: 0,
                expectedSuccess: false, expectedErrorType: typeof(ArgumentNullException), expectedErrorMessage: new ArgumentNullException("source").Message,
                expectedErrorParameter: "source");
            CartQueryFeeCase(trace, "fee-null-source-active-exact", source, carId, null, reservation: "exact", expectedEnumerators: 0,
                expectedSuccess: false, expectedErrorType: typeof(ArgumentNullException), expectedErrorMessage: new ArgumentNullException("source").Message,
                expectedErrorParameter: "source");
            CartQueryFeeCase(trace, "fee-selected-null-row", source, carId, new PartCartLine[] { null }, reservation: "none", expectedEnumerators: 1,
                expectedSuccess: false, expectedErrorType: typeof(NullReferenceException), expectedErrorMessage: CartQueryNaturalNullReferenceMessage());
            CartQueryFeeCase(trace, "fee-undefined-action-filtered-out", source, carId,
                new[] { new PartCartLine { action = (CartAction)999, definitionId = set.id, quantity = 1 } }, reservation: "none", expectedEnumerators: 1,
                expectedFee: 0);

            CartQueryFeeThrowCase(trace, "fee-iterator-throws-filter-pass", source, carId, rows, reservation: "none", throwAfter: 0,
                expectedEnumerators: 1);
            CartQueryFeeThrowCase(trace, "fee-iterator-throws-match-pass", source, carId, rows, reservation: "exact", throwAfter: 0,
                expectedEnumerators: 1);
            CartQueryFeeThrowCase(trace, "fee-iterator-throws-after-match-before-filter", source, carId, rows, reservation: "exact", throwAfter: rows.Length,
                expectedEnumerators: 1);
            CartQueryFeeThrowCase(trace, "fee-filter-iterator-throws-after-complete-match-pass", source, carId, rows,
                reservation: "exact", throwAfter: 0, expectedEnumerators: 2, throwOnEnumerator: 2);
        }

        static void CartQueryReferenceApplyPreview(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, CartQueryReferenceSeed + 4);
            var car = source.cars.Find(item => item.instanceId == carId);
            var set = car == null ? null : FindOffer(source, car, PartCategory.HeadIntake, false);
            if (set == null) { trace.Assert(false, "Apply/Preview fixture has actual authored supply"); return; }

            var newSetRows = new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 2, install = false } };
            CartQueryApplyCase(trace, "apply-new-set-two", source, carId, newSetRows);
            CartQueryApplyCase(trace, "apply-source-usingCartReservation-false", source, carId, newSetRows, sourceUsingCartReservation: false);
            CartQueryApplyCase(trace, "apply-source-usingCartReservation-true", source, carId, newSetRows, sourceUsingCartReservation: true);
            CartQueryApplyCase(trace, "apply-preview-flag-new-set-two", source, carId, newSetRows, preview: true);
            CartQueryApplyCase(trace, "apply-null-enumerable", source, carId, null, expectedSuccess: false,
                expectedErrorType: typeof(NullReferenceException), expectedErrorMessage: CartQueryNaturalNullReferenceMessage());
            CartQueryPreviewProfileCase(trace, "preview-profile-new-set-two", source, carId, newSetRows);
            CartQueryPreviewProfileFlagCase(trace, source, carId, newSetRows, false);
            CartQueryPreviewProfileFlagCase(trace, source, carId, newSetRows, true);
            CartQueryApplyCase(trace, "apply-empty", source, carId, Array.Empty<PartCartLine>());
            CartQueryPreviewProfileCase(trace, "preview-profile-empty", source, carId, Array.Empty<PartCartLine>());
            CartQueryPreviewProfileCase(trace, "preview-profile-exact-active-reservation", source, carId, newSetRows, seedExactReservation: true);
            CartQueryPreviewProfileCase(trace, "preview-profile-null-enumerable", source, carId, null, expectedSuccess: false,
                expectedErrorType: typeof(ArgumentNullException), expectedErrorMessage: new ArgumentNullException("source").Message,
                expectedErrorParameter: "source");
            CartQueryPreviewCase(trace, "preview-inspection-new-set-two", source, carId, newSetRows);
            CartQueryPreviewCase(trace, "preview-inspection-exact-active-reservation", source, carId, newSetRows, seedExactReservation: true);
            CartQueryPreviewCase(trace, "preview-inspection-empty", source, carId, Array.Empty<PartCartLine>());

            var q99Definition = source.catalog.parts.Where(definition => definition != null && definition.price >= 0 &&
                    source.catalog.Fits(source.catalog.Spec(car.vehicleId), definition))
                .OrderBy(definition => definition.price).ThenBy(definition => definition.id, StringComparer.Ordinal)
                .FirstOrDefault(definition => source.inventory.market.parts.Any(stock => stock != null && stock.units > 0 && stock.units < 99 &&
                    stock.definitions != null && stock.definitions.Contains(definition.id)));
            if (q99Definition != null)
            {
                var q99Rows = new[] { new PartCartLine { action = CartAction.NewSet, definitionId = q99Definition.id, quantity = 99 } };
                var q99Quote = new LegacyScopedSupplyReference.Ledger(source).Quote(q99Definition,
                    InventoryUnits.Count(source, car, q99Definition), q99Definition.price, 99);
                source.cash = Math.Max(source.cash, q99Quote.cost);
                trace.Assert(q99Quote.localUnits > 0 && q99Quote.expressUnits > 0 && source.cash >= q99Quote.cost,
                    "q99 Apply/preview fixture has mixed actual stock and adequate explicit fixture cash before clones / local=" +
                    q99Quote.localUnits + ";express=" + q99Quote.expressUnits + ";cash=" + source.cash + ";cost=" + q99Quote.cost);
                CartQueryApplyCase(trace, "apply-q99-express-capable", source, carId, q99Rows, expectedSuccess: true);
                CartQueryPreviewProfileCase(trace, "preview-profile-q99-express-capable", source, carId, q99Rows, expectedSuccess: true);
            }
            else trace.Add("OPEN|apply-q99-express-capable|no actual compatible row with mixed local and express units; no stock fabrication");
            CartQueryPreviewCase(trace, "preview-inspection-null-enumerable", source, carId, null, expectedSuccess: false,
                expectedErrorType: typeof(ArgumentNullException), expectedErrorMessage: new ArgumentNullException("source").Message,
                expectedErrorParameter: "source");
            CartQuerySequenceFailureCase(trace, "apply-enumerator-fails-after-one-row", source, carId, newSetRows, "Apply", 1);
            CartQuerySequenceFailureCase(trace, "preview-profile-enumerator-fails-after-one-row", source, carId, newSetRows, "PreviewProfile", 1);
            CartQuerySequenceFailureCase(trace, "preview-enumerator-fails-after-one-row", source, carId, newSetRows, "Preview", 1);

            uint usedPartSeed = CartQueryReferenceSeed ^ 0x9e3779b9u;
            DealerStock usedPart = CartQueryAddUsedPartForCategory(source, "l1d-query/apply-used-part", PartCategory.HeadIntake, ref usedPartSeed);
            if (usedPart != null)
            {
                var usedRows = new[] { new PartCartLine { action = CartAction.BuyUsed, definitionId = usedPart.DefinitionId,
                    assetId = usedPart.Id, quantity = 1 } };
                CartQueryApplyCase(trace, "apply-used-real-part", source, carId, usedRows);
                CartQueryPreviewProfileCase(trace, "preview-profile-used-real-part", source, carId, usedRows);
            }
            else trace.Add("OPEN|apply-used-real-part|bounded actual authored used-part fixture construction failed");

            uint unitSeed = CartQueryReferenceSeed ^ 0x85ebca6bu;
            var applyUnitDefinition = source.catalog.parts.Where(definition => definition != null && definition.price >= 0 &&
                    definition.reputationRequired <= source.reputation && source.catalog.Fits(source.catalog.Spec(car.vehicleId), definition) &&
                    InventoryUnits.Roles(source, car, definition).Length > 0)
                .OrderBy(definition => definition.price).ThenBy(definition => definition.id, StringComparer.Ordinal).FirstOrDefault();
            string applyUnitRole = applyUnitDefinition == null ? null : InventoryUnits.Roles(source, car, applyUnitDefinition)[0];
            DealerStock actualUsedUnit = applyUnitDefinition == null ? null :
                CartQueryAddUsedUnitForCategory(source, "l1d-query/apply-used-unit", applyUnitDefinition.category, applyUnitRole, ref unitSeed);
            if (actualUsedUnit?.unit?.element != null)
            {
                var usedUnitRows = new[] { new PartCartLine { action = CartAction.BuyUsed,
                    definitionId = actualUsedUnit.DefinitionId, assetId = actualUsedUnit.Id,
                    role = actualUsedUnit.unit.element.role, quantity = 1 } };
                CartQueryApplyCase(trace, "apply-used-real-unit", source, carId, usedUnitRows);
                CartQueryPreviewProfileCase(trace, "preview-profile-used-real-unit", source, carId, usedUnitRows);
            }
            else trace.Add("OPEN|apply-used-real-unit|actual used dealer unit unavailable");

            string moveId = PartOperations.Buy(source, source.catalog, carId, set.id, false);
            var moveRows = new[] { new PartCartLine { action = CartAction.Move, definitionId = set.id, assetId = moveId,
                quantity = 1, destination = InventoryKind.Storage, destinationId = SharedInventory.Storage } };
            CartQueryApplyCase(trace, "apply-real-part-move-to-storage", source, carId, moveRows);
            CartQueryTotalCase(trace, "total-real-part-move-to-storage", source, carId, moveRows);
            CartQueryPreviewProfileCase(trace, "preview-profile-real-part-move-to-storage", source, carId, moveRows);
            CartQueryPreviewCase(trace, "preview-inspection-real-part-move-to-storage", source, carId, moveRows);

            var failureRows = new[]
            {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 },
                new PartCartLine { action = (CartAction)999, definitionId = set.id, quantity = 1 }
            };
            CartQueryApplyCase(trace, "apply-partial-first-row-before-invalid-action", source, carId, failureRows,
                expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException),
                expectedErrorMessage: "Used stock unavailable or invalid.");
            CartQueryApplyCase(trace, "apply-preview-flag-partial-first-row-before-invalid-action", source, carId, failureRows,
                preview: true, expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException),
                expectedErrorMessage: "Used stock unavailable or invalid.");
            CartQueryPreviewProfileCase(trace, "preview-profile-partial-first-row-before-invalid-action", source, carId,
                failureRows, expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException),
                expectedErrorMessage: "Used stock unavailable or invalid.");
            CartQueryPreviewCase(trace, "preview-partial-first-row-before-invalid-action", source, carId, failureRows,
                expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException),
                expectedErrorMessage: "Used stock unavailable or invalid.");
            CartQueryTotalCase(trace, "total-first-error-after-materialized-rows", source, carId, failureRows,
                expectedClockReads: 1, expectedSuccess: false, expectedErrorType: typeof(InvalidOperationException),
                expectedErrorMessage: "Invalid cart action or quantity.");

            var saleSkippedRows = new[] { new PartCartLine { action = CartAction.SellPart, definitionId = set.id,
                assetId = moveId, quantity = 1 } };
            CartQueryApplyCase(trace, "apply-skips-sale-with-actual-asset", source, carId, saleSkippedRows);
            CartQueryPreviewProfileCase(trace, "preview-profile-skips-sale-with-actual-asset", source, carId, saleSkippedRows);
            var mixedNewAndMove = new[]
            {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 },
                new PartCartLine { action = CartAction.Move, assetId = moveId, destination = InventoryKind.Storage,
                    destinationId = SharedInventory.Storage, quantity = 1 }
            };
            CartQueryApplyCase(trace, "apply-mixed-new-set-then-real-move", source, carId, mixedNewAndMove);
            CartQueryPreviewProfileCase(trace, "preview-profile-mixed-new-set-then-real-move", source, carId, mixedNewAndMove);

            foreach (var category in new[] { PartCategory.Tires, PartCategory.Wheels })
            {
                string role = category == PartCategory.Tires ? "tire" : "wheel";
                var cornerDefinition = FindOffer(source, car, category, false);
                var positions = cornerDefinition == null ? Array.Empty<string>() :
                    InventoryUnits.Positions(source, car, cornerDefinition.id, role);
                if (cornerDefinition != null && InventoryUnits.Roles(source, car, cornerDefinition).Contains(role) && positions.Length > 0)
                {
                    var rowsForInstall = new[]
                    {
                        new PartCartLine { action = CartAction.NewSet, definitionId = cornerDefinition.id, quantity = 1 },
                        new PartCartLine { action = CartAction.NewUnits, definitionId = cornerDefinition.id, role = role,
                            quantity = 1, install = true, positions = new List<string> { positions[0] } }
                    };
                    CartQueryApplyCase(trace, "apply-real-" + role + "-new-set-and-unit-install", source, carId, rowsForInstall);
                    CartQueryPreviewProfileCase(trace, "preview-profile-real-" + role + "-new-set-and-unit-install", source, carId, rowsForInstall);
                    CartQueryPreviewCase(trace, "preview-inspection-real-" + role + "-new-set-and-unit-install", source, carId, rowsForInstall);
                }
                else trace.Add("OPEN|apply-real-" + role + "-install|actual authored stocked definition/unit/position unavailable");

                uint categorySeed = CartQueryReferenceSeed ^ (category == PartCategory.Tires ? 0x51ed270bu : 0x165667c5u);
                DealerStock usedCorner = CartQueryAddUsedUnitForCategory(source, "l1d-query/" + role + "-used", category, role, ref categorySeed);
                if (usedCorner?.unit?.element != null)
                {
                    var usedCornerRows = new[] { new PartCartLine { action = CartAction.BuyUsed,
                        definitionId = usedCorner.DefinitionId, assetId = usedCorner.Id, role = role, quantity = 1 } };
                    CartQueryApplyCase(trace, "apply-real-used-" + role + "-unit", source, carId, usedCornerRows);
                    CartQueryPreviewProfileCase(trace, "preview-profile-real-used-" + role + "-unit", source, carId, usedCornerRows);
                }
                else trace.Add("OPEN|used-" + role + "-unit|bounded actual authored dealer supply fixture unavailable");

                uint usedPackSeed = CartQueryReferenceSeed ^ (category == PartCategory.Tires ? 0x2c1b3c6du : 0x297a2d39u);
                DealerStock usedPack = CartQueryAddUsedPartForCategory(source, "l1d-query/" + role + "-used-pack", category, ref usedPackSeed);
                if (usedPack?.part != null)
                {
                    var usedPackRows = new[] { new PartCartLine { action = CartAction.BuyUsed,
                        definitionId = usedPack.DefinitionId, assetId = usedPack.Id, quantity = 1 } };
                    CartQueryApplyCase(trace, "apply-real-used-" + role + "-part", source, carId, usedPackRows);
                    CartQueryPreviewProfileCase(trace, "preview-profile-real-used-" + role + "-part", source, carId, usedPackRows);
                }
                else trace.Add("OPEN|used-" + role + "-part|bounded actual authored dealer pack fixture unavailable");
            }
        }

        static DealerStock CartQueryAddUsedUnitForCategory(PlayerProfile profile, string prefix,
            PartCategory category, string role, ref uint seed)
        {
            var car = profile?.SelectedCar;
            if (profile?.catalog == null || car == null || seed == 0) return null;
            var spec = profile.catalog.Spec(car.vehicleId);
            var candidates = profile.catalog.parts.Where(definition => definition != null && definition.category == category &&
                    definition.reputationRequired <= profile.reputation && profile.catalog.Fits(spec, definition) &&
                    InventoryUnits.Roles(profile, car, definition).Contains(role))
                .OrderBy(definition => definition.id, StringComparer.Ordinal).ToArray();
            const int attempts = 96;
            int attemptIndex = 0;
            foreach (var definition in candidates)
            for (int attempt = 0; attempt < attempts; attempt++, attemptIndex++)
            {
                uint random = unchecked(seed + (uint)attemptIndex);
                if (random == 0) random = 1;
                string id = prefix + "/" + definition.id + "/" + attempt.ToString(CultureInfo.InvariantCulture);
                int dealerStart = profile.inventory.dealer.Count;
                try { PartsMarket.AddUsedSupply(profile, spec, definition, id, ref random); }
                catch { profile.inventory.dealer.RemoveRange(dealerStart, profile.inventory.dealer.Count - dealerStart); continue; }
                var match = profile.inventory.dealer.Skip(dealerStart).FirstOrDefault(stock => stock != null && stock.IsUnit &&
                    stock.DefinitionId == definition.id && stock.unit?.element?.role == role);
                if (match != null) { seed = random; return match; }
                profile.inventory.dealer.RemoveRange(dealerStart, profile.inventory.dealer.Count - dealerStart);
            }
            return null;
        }

        static DealerStock CartQueryAddUsedPartForCategory(PlayerProfile profile, string prefix,
            PartCategory category, ref uint seed)
        {
            var car = profile?.SelectedCar;
            if (profile?.catalog == null || car == null || seed == 0) return null;
            var spec = profile.catalog.Spec(car.vehicleId);
            var candidates = profile.catalog.parts.Where(definition => definition != null && definition.category == category &&
                    definition.reputationRequired <= profile.reputation && profile.catalog.Fits(spec, definition))
                .OrderBy(definition => definition.id, StringComparer.Ordinal).ToArray();
            const int attempts = 96;
            int attemptIndex = 0;
            foreach (var definition in candidates)
            for (int attempt = 0; attempt < attempts; attempt++, attemptIndex++)
            {
                uint random = unchecked(seed + (uint)attemptIndex);
                if (random == 0) random = 1;
                string id = prefix + "/" + definition.id + "/" + attempt.ToString(CultureInfo.InvariantCulture);
                int dealerStart = profile.inventory.dealer.Count;
                try { PartsMarket.AddUsedSupply(profile, spec, definition, id, ref random); }
                catch { profile.inventory.dealer.RemoveRange(dealerStart, profile.inventory.dealer.Count - dealerStart); continue; }
                var match = profile.inventory.dealer.Skip(dealerStart).FirstOrDefault(stock => stock != null && stock.IsPart &&
                    stock.DefinitionId == definition.id);
                if (match != null) { seed = random; return match; }
                profile.inventory.dealer.RemoveRange(dealerStart, profile.inventory.dealer.Count - dealerStart);
            }
            return null;
        }

        static void CartQueryReferenceValuation(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, CartQueryReferenceSeed + 5);
            var car = source.cars.Find(item => item.instanceId == carId);
            if (car == null) { trace.Assert(false, "valuation fixture has selected authored car"); return; }
            var policy = ValuationPolicy.Defaults();
            var assets = new List<Tuple<string, ValuationKind, string>>();
            var loosePartDefinition = FindOffer(source, car, PartCategory.HeadIntake, false);
            if (loosePartDefinition != null)
            {
                string id = PartOperations.Buy(source, source.catalog, carId, loosePartDefinition.id, false);
                assets.Add(Tuple.Create(id, ValuationKind.LoosePart, "loose-authored-part"));
            }

            var looseUnitDefinition = source.catalog.parts.Where(definition => definition != null && definition.price >= 0 &&
                    definition.reputationRequired <= source.reputation && source.catalog.Fits(source.catalog.Spec(car.vehicleId), definition) &&
                    InventoryUnits.Roles(source, car, definition).Length > 0)
                .OrderBy(definition => definition.price).ThenBy(definition => definition.id, StringComparer.Ordinal).FirstOrDefault();
            if (looseUnitDefinition != null)
            {
                string role = InventoryUnits.Roles(source, car, looseUnitDefinition)[0];
                var looseUnit = InventoryUnits.Buy(source, carId, looseUnitDefinition.id, role);
                assets.Add(Tuple.Create(looseUnit.element.id, ValuationKind.LooseElement, "loose-authored-unit"));
            }
            else trace.Add("OPEN|valuation-loose-unit|no actual compatible modeled unit offer");

            foreach (var category in new[] { PartCategory.Tires, PartCategory.Wheels })
            {
                var definition = source.catalog.parts.Where(item => item != null && item.category == category && item.price >= 0 &&
                        item.reputationRequired <= source.reputation &&
                        source.catalog.Fits(source.catalog.Spec(car.vehicleId), item))
                    .OrderBy(item => item.price).ThenBy(item => item.id, StringComparer.Ordinal)
                    .FirstOrDefault(item => InventoryUnits.Roles(source, car, item).Contains(category == PartCategory.Tires ? "tire" : "wheel"));
                string role = category == PartCategory.Tires ? "tire" : "wheel";
                if (definition == null)
                { trace.Add("OPEN|valuation-" + role + "-unit|no authored compatible corner-unit offer"); continue; }
                var unit = InventoryUnits.Buy(source, carId, definition.id, role);
                assets.Add(Tuple.Create(unit.element.id, ValuationKind.LooseElement, "loose-" + role + "-unit"));
                string setId = PartOperations.Buy(source, source.catalog, carId, definition.id, false);
                if (category == PartCategory.Tires)
                {
                    // Initialize only this just-created untouched pack, never existing members.
                    var freshTires = source.parts.Single(item => item.instanceId == setId);
                    trace.Assert(TireDynamics.CanSeed(freshTires.condition), "valuation fixture tire pack is newly created and untouched before explicit initialization");
                    TireDynamics.Ensure(freshTires);
                }
                assets.Add(Tuple.Create(setId, ValuationKind.LoosePart, "loose-" + role + "-part"));
            }

            CartQueryValuationPrices(trace, source, carId, assets, policy, CartPreviewCharacterizationFixtures.FixedNowTicks);
            CartQueryValuationValidationCases(trace, source, carId, loosePartDefinition, assets.FirstOrDefault());
            if (assets.Count > 0)
            {
                var first = assets[0];
                CartQueryValuationPriceCase(trace, source, carId, first.Item1, first.Item2, policy,
                    CartPreviewCharacterizationFixtures.FixedNowTicks, "unknown-buyer", "l1d-query/unknown-buyer",
                    expectedErrorMessage: "Unknown buyer.");
                CartQueryValuationPriceCase(trace, source, carId, first.Item1, first.Item2, policy, 0, "invalid-zero-time",
                    expectedErrorMessage: "Invalid valuation request.");
                CartQueryValuationPriceCase(trace, source, carId, "l1d-query/missing-asset", first.Item2, policy,
                    CartPreviewCharacterizationFixtures.FixedNowTicks, "missing-loose-asset", expectedErrorMessage:
                        first.Item2 == ValuationKind.LoosePart
                            ? "Quote requires an owned loose part without retained descendants."
                            : "Quote requires an owned loose engine element without retained descendants.");
                CartQueryValuationPriceCase(trace, source, carId, first.Item1, first.Item2, policy,
                    DateTime.MaxValue.Ticks, "invalid-out-of-range-time", expectedErrorMessage: "Invalid valuation request.");
                CartQueryValuationViewRevision(trace, source, carId, first.Item1, first.Item2, policy);
                CartQueryValuationInvalidPolicyCase(trace, source, carId, first.Item1, first.Item2);
            }
            else trace.Add("OPEN|valuation-prices|no authored loose part/unit fixture could be constructed");

        }

        static void CartQueryValuationValidationCases(Trace trace, PlayerProfile source, string carId,
            PartDefinition loosePartDefinition, Tuple<string, ValuationKind, string> quoteAsset)
        {
            if (loosePartDefinition == null || quoteAsset == null)
            { trace.Add("OPEN|valuation-validation-cases|requires an actual loose authored part and quote asset"); return; }
            string looseId = quoteAsset.Item1;
            if (quoteAsset.Item2 != ValuationKind.LoosePart)
            {
                looseId = PartOperations.Buy(source, source.catalog, carId, loosePartDefinition.id, false);
                if (string.IsNullOrEmpty(looseId))
                { trace.Assert(false, "validation fixture creates actual authored loose part"); return; }
            }
            uint seed = CartQueryReferenceSeed ^ 0x27d4eb2fu;
            DealerStock dealerPart = null;
            try { dealerPart = CartPreviewCharacterizationFixtures.AddUsedPart(source, "l1d-query/validation-dealer", ref seed); }
            catch (Exception error) { trace.Add("OPEN|valuation-dealer-validation|bounded actual authored fixture unavailable / " + C0Error(error)); }
            if (dealerPart?.part == null)
            { trace.Add("OPEN|valuation-dealer-validation|bounded actual dealer-part fixture construction failed"); return; }

            CartQueryValidationPriceCase(trace, "validation-ordinary-valid", source, carId, looseId, "valid");
            CartQueryValidationPriceCase(trace, "validation-ordinary-invalid-cash", source, carId, looseId, "negative-cash");
            CartQueryValidationPriceCase(trace, "validation-dealer-discriminator-tolerated", source, carId, looseId, "dealer-discriminator");
            CartQueryValidationPriceCase(trace, "validation-real-conflicting-duplicate-id", source, carId, looseId, "duplicate-dealer-id");
        }

        static void CartQueryValidationPriceCase(Trace trace, string label, PlayerProfile baseline,
            string carId, string looseId, string mutation)
        {
            var test = CartQueryReferenceBuildCase(trace, label, baseline, carId, null);
            if (test == null) return;
            var fixture = test.Fixture;
            string duplicateDealerId = null;
            int duplicateDealerIndex = -1;
            if (mutation == "negative-cash")
            {
                fixture.Live.cash = -1; fixture.Frozen.cash = -1;
            }
            else if (mutation == "dealer-discriminator")
            {
                var liveStock = fixture.Live.inventory.dealer.FirstOrDefault(stock => stock != null && stock.IsPart);
                var frozenStock = fixture.Frozen.inventory.dealer.FirstOrDefault(stock => stock != null && stock.IsPart);
                if (liveStock == null || frozenStock == null)
                { trace.Add("OPEN|" + label + "|actual dealer part absent after independent clone"); return; }
                liveStock.isUnit = !liveStock.IsUnit;
                frozenStock.isUnit = !frozenStock.IsUnit;
            }
            else if (mutation == "duplicate-dealer-id")
            {
                var liveStock = fixture.Live.inventory.dealer.FirstOrDefault(stock => stock != null && stock.IsPart);
                var frozenStock = fixture.Frozen.inventory.dealer.FirstOrDefault(stock => stock != null && stock.IsPart);
                var liveOther = fixture.Live.parts.FirstOrDefault(part => part != null && part.instanceId != looseId);
                var frozenOther = fixture.Frozen.parts.FirstOrDefault(part => part != null && part.instanceId != looseId);
                if (liveStock?.part == null || frozenStock?.part == null || liveOther == null || frozenOther == null)
                { trace.Add("OPEN|" + label + "|requires real dealer and separate authored part identities"); return; }
                liveStock.part.instanceId = liveOther.instanceId;
                frozenStock.part.instanceId = frozenOther.instanceId;
                duplicateDealerId = liveOther.instanceId;
                duplicateDealerIndex = fixture.Live.inventory.dealer.IndexOf(liveStock);
            }

            string liveBefore = SourceSnapshot(fixture.Live), frozenBefore = SourceSnapshot(fixture.Frozen);
            string liveCatalogBefore = CatalogSnapshot(fixture.Live), frozenCatalogBefore = CatalogSnapshot(fixture.Frozen);
            string liveIndexBefore = C0CatalogIndexSnapshot(fixture.Live.catalog), frozenIndexBefore = C0CatalogIndexSnapshot(fixture.Frozen.catalog);
            string liveMarketBefore = C0MarketIndexSnapshot(fixture.Live), frozenMarketBefore = C0MarketIndexSnapshot(fixture.Frozen);
            object[] liveReferencesBefore = C0ProfileReferences(fixture.Live), frozenReferencesBefore = C0ProfileReferences(fixture.Frozen);
            Type expectedErrorType = null;
            string expectedErrorMessage = null;
            string expectedErrorParameter = null;
            if (mutation == "negative-cash")
            {
                expectedErrorType = typeof(InvalidOperationException);
                expectedErrorMessage = "Invalid profile version/revision/cash.";
            }
            else if (mutation == "duplicate-dealer-id")
            {
                expectedErrorType = typeof(InvalidOperationException);
                expectedErrorMessage = "Duplicate dealer part identity at row " + duplicateDealerIndex.ToString(CultureInfo.InvariantCulture) +
                    ": " + duplicateDealerId + ".";
            }
            var live = CartQueryReferenceInvoke(() =>
            {
                var view = Valuations.LoosePriceView(fixture.Live, ValuationPolicy.Defaults(), "wholesale", CartPreviewCharacterizationFixtures.FixedNowTicks);
                return view(looseId, false);
            });
            var frozen = CartQueryReferenceInvoke(() =>
            {
                var view = LegacyCartQueryValuationReference.LoosePriceView(fixture.Frozen, ValuationPolicy.Defaults(), "wholesale", CartPreviewCharacterizationFixtures.FixedNowTicks);
                return view(looseId, false);
            });
            bool isTolerance = mutation == "dealer-discriminator";
            bool isInvalid = expectedErrorType != null;
            bool intendedResult = isInvalid
                ? CartQueryErrorIs(live, expectedErrorType, expectedErrorMessage, expectedErrorParameter) &&
                    CartQueryErrorIs(frozen, expectedErrorType, expectedErrorMessage, expectedErrorParameter)
                : live.Error == null && frozen.Error == null && live.Value is int liveValue && liveValue >= 0 &&
                    frozen.Value is int frozenValue && frozenValue >= 0;
            bool outcome = CartQueryReferenceResultEqual(live, frozen) &&
                intendedResult && (!isTolerance || live.Value is int && frozen.Value is int);
            bool values = SourceSnapshot(fixture.Live) == liveBefore && SourceSnapshot(fixture.Frozen) == frozenBefore;
            bool indexes = C0CatalogIndexSnapshot(fixture.Live.catalog) == liveIndexBefore && C0CatalogIndexSnapshot(fixture.Frozen.catalog) == frozenIndexBefore &&
                CatalogSnapshot(fixture.Live) == liveCatalogBefore && CatalogSnapshot(fixture.Frozen) == frozenCatalogBefore &&
                C0MarketIndexSnapshot(fixture.Live) == liveMarketBefore && C0MarketIndexSnapshot(fixture.Frozen) == frozenMarketBefore;
            bool referenceEffectsMatch = C0SameReferences(liveReferencesBefore, C0ProfileReferences(fixture.Live)) ==
                C0SameReferences(frozenReferencesBefore, C0ProfileReferences(fixture.Frozen));
            bool independent = CanonicalGraphEquivalent(fixture.Live, fixture.Frozen, fixture.Baseline, trace, label + ".validation-graph") &&
                CartQueryTopologyMatches(fixture.Live, fixture.Frozen);
            trace.Assert(outcome, "ordinary/dealer/identity validation reaches its source-pinned intended outcome / " + label +
                " / expected=" + (expectedErrorType == null ? "nonnegative price" : expectedErrorType.Name + ": " + expectedErrorMessage) +
                ";live=" + live.Text + ";frozen=" + frozen.Text);
            trace.Assert(values && indexes && referenceEffectsMatch && independent, "quote validation source values, reference topology and catalog/market indexes are measured / " + label +
                " / liveChanged=" + (liveBefore != SourceSnapshot(fixture.Live)) + ";frozenChanged=" + (frozenBefore != SourceSnapshot(fixture.Frozen)));
            trace.Add("QUERY_VALIDATE_PRICE|" + label + "|result=" + live.Text + ";" + frozen.Text + "|sourcePreserved=" + values +
                "|indexPreserved=" + indexes + "|sameReferenceEffect=" + referenceEffectsMatch);
        }

        static CartQueryReferenceCase CartQueryReferenceBuildCase(Trace trace, string label, PlayerProfile baseline,
            string carId, PartCartLine[] rows, C0CatalogShape shape = C0CatalogShape.Warm)
        {
            C0Fixture fixture;
            try { fixture = C0BuildFixture(baseline, carId, rows, shape); }
            catch (Exception error)
            { trace.Assert(false, "independent authored live/frozen fixture builds / " + label + " / " + C0Error(error)); return null; }
            return new CartQueryReferenceCase(label, baseline, fixture);
        }

        static CartQueryReferenceCall CartQueryReferenceInvoke(Func<object> action)
        {
            var result = new CartQueryReferenceCall();
            long jsonBefore = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            result.Identities = RuntimeEntropy.BeginIdentityObservation(CartQueryIdentityCapacity);
            try { result.Value = action(); }
            catch (Exception error) { result.Error = error; }
            finally
            {
                result.Identities.Dispose();
                result.JsonCalls = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount - jsonBefore;
            }
            return result;
        }

        static CartQueryReferenceCall CartQueryTotalCase(Trace trace, string label, PlayerProfile baseline, string carId,
            PartCartLine[] rows, long now = 0, bool validateBlocks = true, bool includeOwnerReservation = false,
            bool? expectedSuccess = true, int? expectedClockReads = null, C0CatalogShape shape = C0CatalogShape.Warm,
            Type expectedErrorType = null, string expectedErrorMessage = null, string expectedErrorParameter = null)
        {
            var test = CartQueryReferenceBuildCase(trace, label, baseline, carId, rows, shape);
            if (test == null) return null;
            var actualRows = test.Fixture.LiveRows == null ? null : new CartQueryCountingRows(test.Fixture.LiveRows);
            var frozenRows = test.Fixture.FrozenRows == null ? null : new CartQueryCountingRows(test.Fixture.FrozenRows);
            return CartQueryTotalInvoke(trace, test, actualRows, frozenRows, now, validateBlocks, includeOwnerReservation,
                expectedSuccess, expectedClockReads ?? (now == 0 ? 1 : 0), label,
                expectedErrorType, expectedErrorMessage, expectedErrorParameter);
        }

        static void CartQueryTotalEnumerableCase(Trace trace, string label, PlayerProfile baseline, string carId,
            CartQueryCountingRows sourceRows, int expectedClockReads)
        {
            var test = CartQueryReferenceBuildCase(trace, label, baseline, carId, new[]
            {
                new PartCartLine { action = CartAction.SellPart, assetId = "enumerator-template", quantity = 1 }
            });
            if (test == null) return;
            var actualRows = new CartQueryCountingRows(test.Fixture.LiveRows, sourceRows.ThrowAfter, sourceRows.ThrowMessage);
            var frozenRows = new CartQueryCountingRows(test.Fixture.FrozenRows, sourceRows.ThrowAfter, sourceRows.ThrowMessage);
            CartQueryTotalInvoke(trace, test, actualRows, frozenRows, 0, true, false, false, expectedClockReads, label,
                typeof(InvalidOperationException), sourceRows.ThrowMessage, null);
        }

        static CartQueryReferenceCall CartQueryTotalInvoke(Trace trace, CartQueryReferenceCase test, CartQueryCountingRows actualRows,
            CartQueryCountingRows frozenRows, long now, bool validateBlocks, bool includeOwnerReservation,
            bool? expectedSuccess, int? expectedClockReads, string label, Type expectedErrorType = null,
            string expectedErrorMessage = null, string expectedErrorParameter = null)
        {
            var fixture = test.Fixture;
            string liveBefore = SourceSnapshot(fixture.Live), frozenBefore = SourceSnapshot(fixture.Frozen);
            string liveCatalogBefore = CatalogSnapshot(fixture.Live), frozenCatalogBefore = CatalogSnapshot(fixture.Frozen);
            object[] liveRefs = C0ProfileReferences(fixture.Live), frozenRefs = C0ProfileReferences(fixture.Frozen);
            string liveRowsBefore = RowsSnapshot(fixture.LiveRows), frozenRowsBefore = RowsSnapshot(fixture.FrozenRows);
            var liveRandomBefore = UnityEngine.Random.state;
            long liveJsonBefore = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            var live = CartQueryReferenceInvoke(() => PartCart.Total(fixture.Live, test.Fixture.CarId, actualRows,
                ValuationPolicy.Defaults(), now, validateBlocks, includeOwnerReservation));
            long liveJsonDelta = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount - liveJsonBefore;
            var liveRandomAfter = UnityEngine.Random.state;
            var frozenRandomBefore = liveRandomAfter;
            long frozenJsonBefore = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            int frozenClockCalls = 0, frozenClockMoves = -1;
            Func<long> clock = () => { frozenClockCalls++; frozenClockMoves = frozenRows?.MoveNextCalls ?? 0; return RuntimeEntropy.UtcNow.Ticks; };
            var frozen = CartQueryReferenceInvoke(() => LegacyCartQueryReference.TotalWithClock(fixture.Frozen,
                test.Fixture.CarId, frozenRows, ValuationPolicy.Defaults(), now, validateBlocks, includeOwnerReservation, clock));
            long frozenJsonDelta = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount - frozenJsonBefore;
            var frozenRandomAfter = UnityEngine.Random.state;

            bool output = CartQueryReferenceResultEqual(live, frozen);
            bool graph = CanonicalGraphEquivalent(fixture.Live, fixture.Frozen, fixture.Baseline, trace, label + ".partial-profile");
            bool aliasTopology = CartQueryTopologyMatches(fixture.Live, fixture.Frozen);
            bool catalog = CatalogSnapshot(fixture.Live) == CatalogSnapshot(fixture.Frozen) &&
                C0CatalogIndexSnapshot(fixture.Live.catalog) == C0CatalogIndexSnapshot(fixture.Frozen.catalog) &&
                C0MarketIndexSnapshot(fixture.Live) == C0MarketIndexSnapshot(fixture.Frozen);
            bool rows = liveRowsBefore == RowsSnapshot(fixture.LiveRows) && frozenRowsBefore == RowsSnapshot(fixture.FrozenRows) &&
                (actualRows == null && frozenRows == null || actualRows != null && frozenRows != null &&
                 actualRows.Enumerators == frozenRows.Enumerators && actualRows.MoveNextCalls == frozenRows.MoveNextCalls && actualRows.Yielded == frozenRows.Yielded);
            bool enumerationOnce = actualRows == null && frozenRows == null || actualRows != null && frozenRows != null &&
                actualRows.Enumerators == 1 && frozenRows.Enumerators == 1;
            bool scopes = CartQueryReferenceIdentityParity(live.Identities, frozen.Identities);
            bool baseline = fixture.BaselineBefore == SourceSnapshot(fixture.Baseline) &&
                C0SameReferences(fixture.BaselineReferences, C0ProfileReferences(fixture.Baseline));
            bool independentGraphAndCatalog = liveBefore == frozenBefore && liveCatalogBefore == frozenCatalogBefore &&
                C0SameReferences(liveRefs, C0ProfileReferences(fixture.Live)) == C0SameReferences(frozenRefs, C0ProfileReferences(fixture.Frozen));
            bool rng = liveRandomBefore.Equals(liveRandomAfter) && frozenRandomBefore.Equals(frozenRandomAfter);
            int expectedClockMoves = expectedClockReads == 1 && frozenRows != null && frozenRows.RowCount >= 0
                ? frozenRows.RowCount + 1 : -1;
            bool clockMatches = !expectedClockReads.HasValue || frozenClockCalls == expectedClockReads.Value &&
                (expectedClockReads.Value != 1 || frozenClockMoves == expectedClockMoves);
            if (expectedClockReads.HasValue)
                trace.Assert(clockMatches, "frozen Total default-time call count matches guard / " + label + " / reads=" + frozenClockCalls + ";rowMoveNextAtRead=" + frozenClockMoves);
            if (expectedSuccess.HasValue)
                trace.Assert((live.Error == null) == expectedSuccess.Value && (frozen.Error == null) == expectedSuccess.Value,
                    "legacy Total reaches requested outcome / " + label + " / live=" + live.Text + ";frozen=" + frozen.Text);
            if (expectedErrorType != null)
                trace.Assert(CartQueryErrorIs(live, expectedErrorType, expectedErrorMessage, expectedErrorParameter) &&
                    CartQueryErrorIs(frozen, expectedErrorType, expectedErrorMessage, expectedErrorParameter),
                    "legacy Total reaches exact source-pinned first error / " + label + " / expected=" + expectedErrorType.Name + ": " + expectedErrorMessage +
                    ";live=" + live.Text + ";frozen=" + frozen.Text);
            trace.Assert(output, "live Total and frozen closure return/error parity / " + label + " / live=" + live.Text + ";frozen=" + frozen.Text);
            trace.Assert(graph && aliasTopology && catalog, "live/frozen Total typed partial graph, aliases, source/catalog/index effects match / " + label +
                " / liveChanged=" + (liveBefore != SourceSnapshot(fixture.Live)) + ";frozenChanged=" + (frozenBefore != SourceSnapshot(fixture.Frozen)));
            trace.Assert(rows && enumerationOnce && baseline && independentGraphAndCatalog,
                "Total materializes nonnull source exactly once and preserves row/baseline invariants / " + label +
                " / enumerators=" + (actualRows?.Enumerators ?? 0) + "/" + (frozenRows?.Enumerators ?? 0));
            trace.Assert(scopes && !live.Identities.Overflowed && !frozen.Identities.Overflowed,
                "Total observed identity event count/scope order matches without comparing GUID values / " + label +
                " / live=" + C0IdentitySummary(live.Identities) + ";frozen=" + C0IdentitySummary(frozen.Identities));
            trace.Assert(rng, "Total leaves Unity RNG state unchanged on both old paths / " + label);
            trace.Assert(liveJsonDelta == frozenJsonDelta,
                "Total instrumented runtime JSON call effects match old live/frozen paths / " + label + " / " + liveJsonDelta + "/" + frozenJsonDelta);
            trace.Add("QUERY_TOTAL|" + label + "|now=" + now.ToString(CultureInfo.InvariantCulture) +
                "|actual=" + live.Text + "|frozen=" + frozen.Text + "|enumerators=" + (actualRows?.Enumerators ?? 0) + "/" + (frozenRows?.Enumerators ?? 0) +
                "|clockFrozen=" + frozenClockCalls + "@MoveNext" + frozenClockMoves + "|liveUtc=real-unmodified-property" +
                "|partialLive=" + (liveBefore != SourceSnapshot(fixture.Live)) + ";partialFrozen=" + (frozenBefore != SourceSnapshot(fixture.Frozen)) +
                "|graph=" + graph + ";catalogIndex=" + catalog + ";identityScopes=" + scopes + ";jsonCalls=" + liveJsonDelta + "/" + frozenJsonDelta);
            return live;
        }

        static bool CartQueryReferenceResultEqual(CartQueryReferenceCall live, CartQueryReferenceCall frozen)
        {
            if (live.JsonCalls != frozen.JsonCalls) return false;
            if (!C0ErrorsEqual(live.Error, frozen.Error)) return false;
            if (live.Error != null) return true;
            if (live.Value is int liveInt && frozen.Value is int frozenInt) return liveInt == frozenInt;
            if (live.Value == null || frozen.Value == null) return live.Value == null && frozen.Value == null;
            return live.Value.GetType() == frozen.Value.GetType() &&
                TypedCanonical(live.Value, new Dictionary<string, string>()) == TypedCanonical(frozen.Value, new Dictionary<string, string>());
        }

        static bool CartQueryErrorIs(CartQueryReferenceCall call, Type exceptionType, string message, string parameterName)
        {
            if (call?.Error == null || call.Error.GetType() != exceptionType || call.Error.Message != message) return false;
            var argumentError = call.Error as ArgumentException;
            return string.Equals(argumentError?.ParamName, parameterName, StringComparison.Ordinal);
        }

        static bool CartQueryReferenceIdentityParity(RuntimeEntropy.IdentityObservation live,
            RuntimeEntropy.IdentityObservation frozen)
        {
            if (live == null || frozen == null || !live.Closed || !frozen.Closed || live.Count != frozen.Count) return false;
            for (int i = 0; i < live.Count; i++)
                if (!string.Equals(live.EventAt(i).Scope, frozen.EventAt(i).Scope, StringComparison.Ordinal)) return false;
            return true;
        }

        static Dictionary<string, string> CQRIdentityAliases(RuntimeEntropy.IdentityObservation observation)
        {
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            if (observation == null || !observation.Closed || observation.Overflowed) return aliases;
            for (int i = 0; i < observation.Count; i++)
            {
                var emission = observation.EventAt(i);
                string id = (emission.Scope ?? string.Empty) + "-" + emission.Value.ToString("N");
                if (!aliases.ContainsKey(id)) aliases.Add(id, "<new:" + emission.Scope + ":" + i.ToString(CultureInfo.InvariantCulture) + ">");
            }
            return aliases;
        }

        static void CartQueryFeeCase(Trace trace, string label, PlayerProfile baseline, string carId,
            PartCartLine[] rows, string reservation, int expectedEnumerators, bool expectedSuccess = true,
            Type expectedErrorType = null, string expectedErrorMessage = null, string expectedErrorParameter = null, int? expectedFee = null)
        {
            var test = CartQueryReferenceBuildCase(trace, label, baseline, carId, rows);
            if (test == null) return;
            CartQueryPrepareReservation(test.Fixture.Live, carId, test.Fixture.LiveRows, reservation);
            CartQueryPrepareReservation(test.Fixture.Frozen, carId, test.Fixture.FrozenRows, reservation);
            var actualRows = test.Fixture.LiveRows == null ? null : new CartQueryCountingRows(test.Fixture.LiveRows);
            var frozenRows = test.Fixture.FrozenRows == null ? null : new CartQueryCountingRows(test.Fixture.FrozenRows);
            CartQueryFeeInvoke(trace, test, actualRows, frozenRows, expectedEnumerators, label,
                expectedSuccess, expectedErrorType, expectedErrorMessage, expectedErrorParameter, expectedFee);
        }

        static void CartQueryFeeThrowCase(Trace trace, string label, PlayerProfile baseline, string carId,
            PartCartLine[] rows, string reservation, int throwAfter, int expectedEnumerators, int throwOnEnumerator = 1)
        {
            var test = CartQueryReferenceBuildCase(trace, label, baseline, carId, rows);
            if (test == null) return;
            CartQueryPrepareReservation(test.Fixture.Live, carId, test.Fixture.LiveRows, reservation);
            CartQueryPrepareReservation(test.Fixture.Frozen, carId, test.Fixture.FrozenRows, reservation);
            var actualRows = new CartQueryCountingRows(test.Fixture.LiveRows, throwAfter,
                "fee-enumerator-after-" + throwAfter.ToString(CultureInfo.InvariantCulture), throwOnEnumerator);
            var frozenRows = new CartQueryCountingRows(test.Fixture.FrozenRows, throwAfter,
                "fee-enumerator-after-" + throwAfter.ToString(CultureInfo.InvariantCulture), throwOnEnumerator);
            CartQueryFeeInvoke(trace, test, actualRows, frozenRows, expectedEnumerators, label, false,
                typeof(InvalidOperationException), "fee-enumerator-after-" + throwAfter.ToString(CultureInfo.InvariantCulture), null);
        }

        static void CartQueryPrepareReservation(PlayerProfile profile, string carId, PartCartLine[] rows, string mode)
        {
            if (mode == "none") { profile.inventory.cart = null; return; }
            var cartRows = (rows ?? Array.Empty<PartCartLine>()).Select(line => line?.Copy()).ToList();
            if (mode == "changed" && cartRows.Count > 0) cartRows[0].quantity = cartRows[0].quantity == 1 ? 2 : 1;
            profile.inventory.cart = new CartReservationState
            {
                version = 1,
                createdRun = profile.vehicleStock.completedRuns,
                expiresRun = mode == "expired" ? profile.vehicleStock.completedRuns - 1 : profile.vehicleStock.completedRuns + 9,
                generation = profile.inventory.market.generation,
                carId = mode == "other-car" ? carId + "/unrelated" : carId,
                lines = cartRows,
                supply = new List<ReservedPartSupply>()
            };
        }

        static void CartQueryFeeInvoke(Trace trace, CartQueryReferenceCase test, CartQueryCountingRows actualRows,
            CartQueryCountingRows frozenRows, int expectedEnumerators, string label, bool expectedSuccess,
            Type expectedErrorType, string expectedErrorMessage, string expectedErrorParameter, int? expectedFee = null)
        {
            var fixture = test.Fixture;
            string liveBefore = SourceSnapshot(fixture.Live), frozenBefore = SourceSnapshot(fixture.Frozen);
            string liveCatalogBefore = CatalogSnapshot(fixture.Live), frozenCatalogBefore = CatalogSnapshot(fixture.Frozen);
            var random = UnityEngine.Random.state;
            long liveJsonBefore = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            var live = CartQueryReferenceInvoke(() => PartCart.DeliveryFee(fixture.Live, test.Fixture.CarId, actualRows));
            long liveJsonDelta = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount - liveJsonBefore;
            var liveRandom = UnityEngine.Random.state;
            long frozenJsonBefore = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            var frozen = CartQueryReferenceInvoke(() => LegacyCartQueryReference.DeliveryFee(fixture.Frozen, test.Fixture.CarId, frozenRows));
            long frozenJsonDelta = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount - frozenJsonBefore;
            var frozenRandom = UnityEngine.Random.state;
            int actualPasses = actualRows?.Enumerators ?? 0, frozenPasses = frozenRows?.Enumerators ?? 0;
            bool output = CartQueryReferenceResultEqual(live, frozen);
            bool intended = expectedSuccess
                ? live.Error == null && frozen.Error == null && live.Value is int liveFeeValue && liveFeeValue >= 0 &&
                    frozen.Value is int frozenFeeValue && frozenFeeValue >= 0 &&
                    (!expectedFee.HasValue || liveFeeValue == expectedFee.Value && frozenFeeValue == expectedFee.Value)
                : CartQueryErrorIs(live, expectedErrorType, expectedErrorMessage, expectedErrorParameter) &&
                    CartQueryErrorIs(frozen, expectedErrorType, expectedErrorMessage, expectedErrorParameter);
            bool passes = actualPasses == frozenPasses && actualPasses == expectedEnumerators;
            bool traversal = actualRows == null && frozenRows == null || actualRows != null && frozenRows != null &&
                actualRows.MoveNextCalls == frozenRows.MoveNextCalls && actualRows.Yielded == frozenRows.Yielded;
            bool graph = CanonicalGraphEquivalent(fixture.Live, fixture.Frozen, fixture.Baseline, trace, label + ".legacy-side-effects");
            bool catalog = CatalogSnapshot(fixture.Live) == CatalogSnapshot(fixture.Frozen) &&
                C0CatalogIndexSnapshot(fixture.Live.catalog) == C0CatalogIndexSnapshot(fixture.Frozen.catalog) &&
                C0MarketIndexSnapshot(fixture.Live) == C0MarketIndexSnapshot(fixture.Frozen);
            bool baseline = fixture.BaselineBefore == SourceSnapshot(fixture.Baseline) &&
                C0SameReferences(fixture.BaselineReferences, C0ProfileReferences(fixture.Baseline));
            bool rng = random.Equals(liveRandom) && liveRandom.Equals(frozenRandom);
            trace.Assert(output && intended, "live DeliveryFee and frozen closure reach source-pinned intended result / " + label +
                " / expected=" + (expectedSuccess ? "integer fee" : expectedErrorType?.Name + ": " + expectedErrorMessage) +
                ";live=" + live.Text + ";frozen=" + frozen.Text);
            trace.Assert(passes && traversal, "DeliveryFee reservation-dependent source enumeration passes / " + label +
                " / expected=" + expectedEnumerators + ";live=" + actualPasses + ";frozen=" + frozenPasses +
                ";moveNext=" + (actualRows?.MoveNextCalls ?? 0) + "/" + (frozenRows?.MoveNextCalls ?? 0));
            trace.Assert(graph && catalog && baseline, "DeliveryFee legacy value/index side effects match (not assumed pure) / " + label +
                " / liveChanged=" + (liveBefore != SourceSnapshot(fixture.Live)) + ";frozenChanged=" + (frozenBefore != SourceSnapshot(fixture.Frozen)) +
                ";catalogBeforeParity=" + (liveCatalogBefore == frozenCatalogBefore));
            trace.Assert(rng, "DeliveryFee leaves Unity RNG state unchanged / " + label);
            trace.Assert(liveJsonDelta == frozenJsonDelta,
                "DeliveryFee instrumented runtime JSON call effects match / " + label + " / " + liveJsonDelta + "/" + frozenJsonDelta);
            trace.Add("QUERY_FEE|" + label + "|actual=" + live.Text + "|frozen=" + frozen.Text +
                "|enumerators=" + actualPasses + "/" + frozenPasses + "|moves=" + (actualRows?.MoveNextCalls ?? 0) + "/" + (frozenRows?.MoveNextCalls ?? 0) +
                "|partialLive=" + (liveBefore != SourceSnapshot(fixture.Live)) + ";partialFrozen=" + (frozenBefore != SourceSnapshot(fixture.Frozen)) +
                "|catalogIndex=" + catalog + ";jsonCalls=" + liveJsonDelta + "/" + frozenJsonDelta);
        }

        static void CartQueryApplyCase(Trace trace, string label, PlayerProfile baseline, string carId,
            PartCartLine[] rows, bool preview = false, bool? expectedSuccess = true, bool? sourceUsingCartReservation = null,
            Type expectedErrorType = null, string expectedErrorMessage = null, string expectedErrorParameter = null)
        {
            var test = CartQueryReferenceBuildCase(trace, label, baseline, carId, rows);
            if (test == null) return;
            var fixture = test.Fixture;
            if (sourceUsingCartReservation.HasValue)
            {
                fixture.Live.inventory.usingCartReservation = sourceUsingCartReservation.Value;
                fixture.Frozen.inventory.usingCartReservation = sourceUsingCartReservation.Value;
            }
            string liveBefore = SourceSnapshot(fixture.Live), frozenBefore = SourceSnapshot(fixture.Frozen);
            var random = UnityEngine.Random.state;
            var live = CartQueryReferenceInvoke(() => { PartCart.Apply(fixture.Live, carId, fixture.LiveRows, preview, ValuationPolicy.Defaults()); return null; });
            var liveRandom = UnityEngine.Random.state;
            var frozen = CartQueryReferenceInvoke(() => { LegacyCartQueryReference.Apply(fixture.Frozen, carId, fixture.FrozenRows, preview, ValuationPolicy.Defaults()); return null; });
            var frozenRandom = UnityEngine.Random.state;
            CartQueryCompareMutableCase(trace, label, test, live, frozen, liveBefore, frozenBefore, expectedSuccess,
                random.Equals(liveRandom) && liveRandom.Equals(frozenRandom));
            if (expectedErrorType != null)
                trace.Assert(CartQueryErrorIs(live, expectedErrorType, expectedErrorMessage, expectedErrorParameter) &&
                    CartQueryErrorIs(frozen, expectedErrorType, expectedErrorMessage, expectedErrorParameter),
                    "raw Apply reaches exact source-pinned first error / " + label + " / expected=" + expectedErrorType.Name + ": " + expectedErrorMessage +
                    ";live=" + live.Text + ";frozen=" + frozen.Text);
            if (sourceUsingCartReservation.HasValue)
                trace.Assert(fixture.Live.inventory.usingCartReservation == sourceUsingCartReservation.Value &&
                    fixture.Frozen.inventory.usingCartReservation == sourceUsingCartReservation.Value,
                    "raw Apply preserves the explicitly seeded usingCartReservation source flag / " + label);
            trace.Add("QUERY_APPLY_MODE|" + label + "|preview=" + preview.ToString(CultureInfo.InvariantCulture));
        }

        static void CartQuerySequenceFailureCase(Trace trace, string label, PlayerProfile baseline, string carId,
            PartCartLine[] rows, string operation, int throwAfter)
        {
            var test = CartQueryReferenceBuildCase(trace, label, baseline, carId, rows);
            if (test == null) return;
            var fixture = test.Fixture;
            var liveRows = new CartQueryCountingRows(fixture.LiveRows, throwAfter, label + "-enumerator");
            var frozenRows = new CartQueryCountingRows(fixture.FrozenRows, throwAfter, label + "-enumerator");
            string liveBefore = SourceSnapshot(fixture.Live), frozenBefore = SourceSnapshot(fixture.Frozen);
            var liveInput = new CartQueryInputObservation(fixture.Live);
            var frozenInput = new CartQueryInputObservation(fixture.Frozen);
            var random = UnityEngine.Random.state;
            CartQueryReferenceCall live = CartQueryReferenceInvoke(() =>
            {
                if (operation == "Apply") { PartCart.Apply(fixture.Live, carId, liveRows, false, ValuationPolicy.Defaults()); return null; }
                var car = fixture.Live.cars.Find(value => value.instanceId == carId);
                if (operation == "PreviewProfile") return PartCart.PreviewProfile(fixture.Live, car, liveRows);
                return PartCart.Preview(fixture.Live, car, liveRows);
            });
            var liveRandom = UnityEngine.Random.state;
            var frozen = CartQueryReferenceInvoke(() =>
            {
                if (operation == "Apply") { LegacyCartQueryReference.Apply(fixture.Frozen, carId, frozenRows, false, ValuationPolicy.Defaults()); return null; }
                var car = fixture.Frozen.cars.Find(value => value.instanceId == carId);
                if (operation == "PreviewProfile") return LegacyCartQueryReference.PreviewProfile(fixture.Frozen, car, frozenRows);
                return LegacyCartQueryReference.Preview(fixture.Frozen, car, frozenRows);
            });
            var frozenRandom = UnityEngine.Random.state;
            CartQueryCompareMutableCase(trace, label, test, live, frozen, liveBefore, frozenBefore, false,
                random.Equals(liveRandom) && liveRandom.Equals(frozenRandom));
            trace.Assert(CartQueryErrorIs(live, typeof(InvalidOperationException), label + "-enumerator", null) &&
                CartQueryErrorIs(frozen, typeof(InvalidOperationException), label + "-enumerator", null),
                operation + " observes exact first enumerable exception after the expected yielded prefix / " + label +
                " / live=" + live.Text + ";frozen=" + frozen.Text);
            trace.Assert(liveRows.Enumerators == 1 && frozenRows.Enumerators == 1 &&
                liveRows.Enumerators == frozenRows.Enumerators && liveRows.MoveNextCalls == frozenRows.MoveNextCalls &&
                liveRows.Yielded == frozenRows.Yielded,
                operation + " consumes the throwing enumerable once and preserves the yielded prefix / " + label +
                " / live=" + liveRows.Enumerators + ":" + liveRows.MoveNextCalls + ":" + liveRows.Yielded +
                ";frozen=" + frozenRows.Enumerators + ":" + frozenRows.MoveNextCalls + ":" + frozenRows.Yielded);
            if (operation != "Apply")
                trace.Assert(CartQueryLegacyInputEffects(trace, label, fixture, liveInput, frozenInput),
                    operation + " matches frozen source field/reference mutation deltas on enumerable failure / " + label);
        }

        static void CartQueryPreviewProfileCase(Trace trace, string label, PlayerProfile baseline, string carId,
            PartCartLine[] rows, bool? expectedSuccess = true, bool seedExactReservation = false,
            Type expectedErrorType = null, string expectedErrorMessage = null, string expectedErrorParameter = null)
        {
            var test = CartQueryReferenceBuildCase(trace, label, baseline, carId, rows);
            if (test == null) return;
            var fixture = test.Fixture;
            if (seedExactReservation)
            {
                CartQueryPrepareReservation(fixture.Live, carId, fixture.LiveRows, "exact");
                CartQueryPrepareReservation(fixture.Frozen, carId, fixture.FrozenRows, "exact");
            }
            string liveBefore = SourceSnapshot(fixture.Live), frozenBefore = SourceSnapshot(fixture.Frozen);
            var liveInput = new CartQueryInputObservation(fixture.Live);
            var frozenInput = new CartQueryInputObservation(fixture.Frozen);
            var random = UnityEngine.Random.state;
            var live = CartQueryReferenceInvoke(() => PartCart.PreviewProfile(fixture.Live,
                fixture.Live.cars.Find(car => car.instanceId == carId), fixture.LiveRows));
            var liveRandom = UnityEngine.Random.state;
            var frozen = CartQueryReferenceInvoke(() => LegacyCartQueryReference.PreviewProfile(fixture.Frozen,
                fixture.Frozen.cars.Find(car => car.instanceId == carId), fixture.FrozenRows));
            var frozenRandom = UnityEngine.Random.state;
            bool outputGraph = live.Error != null || frozen.Error != null
                ? live.Error != null && frozen.Error != null
                : CanonicalGraphEquivalent((PlayerProfile)live.Value, (PlayerProfile)frozen.Value, fixture.Baseline, trace, label + ".returned-profile");
            bool inputEffectsMatch = CartQueryLegacyInputEffects(trace, label, fixture, liveInput, frozenInput);
            CartQueryCompareMutableCase(trace, label, test, live, frozen, liveBefore, frozenBefore, expectedSuccess,
                random.Equals(liveRandom) && liveRandom.Equals(frozenRandom), outputGraph);
            trace.Assert(inputEffectsMatch, "PreviewProfile source field/reference mutation deltas match the frozen legacy behavior / " + label);
            if (expectedErrorType != null)
                trace.Assert(CartQueryErrorIs(live, expectedErrorType, expectedErrorMessage, expectedErrorParameter) &&
                    CartQueryErrorIs(frozen, expectedErrorType, expectedErrorMessage, expectedErrorParameter),
                    "PreviewProfile reaches exact source-pinned first error / " + label + " / expected=" + expectedErrorType.Name + ": " + expectedErrorMessage +
                    ";live=" + live.Text + ";frozen=" + frozen.Text);
        }

        static void CartQueryPreviewProfileFlagCase(Trace trace, PlayerProfile baseline, string carId,
            PartCartLine[] rows, bool sourceUsingCartReservation)
        {
            string label = "preview-profile-source-usingCartReservation-" + sourceUsingCartReservation.ToString(CultureInfo.InvariantCulture);
            var test = CartQueryReferenceBuildCase(trace, label, baseline, carId, rows);
            if (test == null) return;
            var fixture = test.Fixture;
            fixture.Live.inventory.usingCartReservation = sourceUsingCartReservation;
            fixture.Frozen.inventory.usingCartReservation = sourceUsingCartReservation;
            string liveBefore = SourceSnapshot(fixture.Live), frozenBefore = SourceSnapshot(fixture.Frozen);
            var liveInput = new CartQueryInputObservation(fixture.Live);
            var frozenInput = new CartQueryInputObservation(fixture.Frozen);
            var random = UnityEngine.Random.state;
            var live = CartQueryReferenceInvoke(() => PartCart.PreviewProfile(fixture.Live,
                fixture.Live.cars.Find(car => car.instanceId == carId), fixture.LiveRows));
            var liveRandom = UnityEngine.Random.state;
            var frozen = CartQueryReferenceInvoke(() => LegacyCartQueryReference.PreviewProfile(fixture.Frozen,
                fixture.Frozen.cars.Find(car => car.instanceId == carId), fixture.FrozenRows));
            var frozenRandom = UnityEngine.Random.state;
            bool outputGraph = live.Error != null || frozen.Error != null
                ? live.Error != null && frozen.Error != null && C0ErrorsEqual(live.Error, frozen.Error)
                : CanonicalGraphEquivalent((PlayerProfile)live.Value, (PlayerProfile)frozen.Value, fixture.Baseline, trace, label + ".returned-profile");
            bool inputEffectsMatch = CartQueryLegacyInputEffects(trace, label, fixture, liveInput, frozenInput);
            CartQueryCompareMutableCase(trace, label, test, live, frozen, liveBefore, frozenBefore, true,
                random.Equals(liveRandom) && liveRandom.Equals(frozenRandom), outputGraph);
            trace.Assert(inputEffectsMatch, "PreviewProfile source field/reference mutation deltas match for the independently seeded reservation flag / " + label);
            trace.Assert(fixture.Live.inventory.usingCartReservation == sourceUsingCartReservation &&
                fixture.Frozen.inventory.usingCartReservation == sourceUsingCartReservation,
                "PreviewProfile leaves the independently seeded input reservation flag unchanged / " + label);
        }

        static void CartQueryPreviewCase(Trace trace, string label, PlayerProfile baseline, string carId,
            PartCartLine[] rows, bool seedExactReservation = false, bool expectedSuccess = true,
            Type expectedErrorType = null, string expectedErrorMessage = null, string expectedErrorParameter = null)
        {
            var test = CartQueryReferenceBuildCase(trace, label, baseline, carId, rows);
            if (test == null) return;
            var fixture = test.Fixture;
            if (seedExactReservation)
            {
                CartQueryPrepareReservation(fixture.Live, carId, fixture.LiveRows, "exact");
                CartQueryPrepareReservation(fixture.Frozen, carId, fixture.FrozenRows, "exact");
            }
            string liveBefore = SourceSnapshot(fixture.Live), frozenBefore = SourceSnapshot(fixture.Frozen);
            var liveInput = new CartQueryInputObservation(fixture.Live);
            var frozenInput = new CartQueryInputObservation(fixture.Frozen);
            var random = UnityEngine.Random.state;
            var live = CartQueryReferenceInvoke(() => PartCart.Preview(fixture.Live,
                fixture.Live.cars.Find(car => car.instanceId == carId), fixture.LiveRows));
            var liveRandom = UnityEngine.Random.state;
            var frozen = CartQueryReferenceInvoke(() => LegacyCartQueryReference.Preview(fixture.Frozen,
                fixture.Frozen.cars.Find(car => car.instanceId == carId), fixture.FrozenRows));
            var frozenRandom = UnityEngine.Random.state;
            bool parity = C0ErrorsEqual(live.Error, frozen.Error) &&
                live.JsonCalls == frozen.JsonCalls &&
                (live.Error != null || live.Value != null && frozen.Value != null && live.Value.GetType() == frozen.Value.GetType());
            bool source = fixture.BaselineBefore == SourceSnapshot(fixture.Baseline) &&
                C0SameReferences(fixture.BaselineReferences, C0ProfileReferences(fixture.Baseline));
            bool inputEffectsMatch = CartQueryLegacyInputEffects(trace, label, fixture, liveInput, frozenInput);
            bool fieldGraph = live.Error != null && C0ErrorsEqual(live.Error, frozen.Error);
            if (live.Error == null && frozen.Error == null)
                fieldGraph = TypedCanonical(live.Value, CQRIdentityAliases(live.Identities)) ==
                    TypedCanonical(frozen.Value, CQRIdentityAliases(frozen.Identities));
            bool graph = fixture.Live.catalog != null && fixture.Frozen.catalog != null &&
                CatalogSnapshot(fixture.Live) == CatalogSnapshot(fixture.Frozen) &&
                C0CatalogIndexSnapshot(fixture.Live.catalog) == C0CatalogIndexSnapshot(fixture.Frozen.catalog) &&
                C0MarketIndexSnapshot(fixture.Live) == C0MarketIndexSnapshot(fixture.Frozen);
            bool rng = random.Equals(liveRandom) && liveRandom.Equals(frozenRandom);
            bool intended = expectedSuccess
                ? live.Error == null && frozen.Error == null && live.Value is InspectionBuild && frozen.Value is InspectionBuild
                : live.Error != null && frozen.Error != null;
            if (expectedErrorType != null)
                intended &= CartQueryErrorIs(live, expectedErrorType, expectedErrorMessage, expectedErrorParameter) &&
                    CartQueryErrorIs(frozen, expectedErrorType, expectedErrorMessage, expectedErrorParameter);
            trace.Assert(parity && intended && fieldGraph && CartQueryReferenceIdentityParity(live.Identities, frozen.Identities), "live PartCart.Preview and frozen Preview reach intended fieldwise outcome / " + label +
                " / live=" + live.Text + ";frozen=" + frozen.Text);
            trace.Assert(source && inputEffectsMatch && graph && rng, "Preview input/baseline, catalog/index effects and Unity RNG are measured / " + label);
            trace.Add("QUERY_PREVIEW|" + label + "|live=" + live.Text + "|frozen=" + frozen.Text + "|fieldParity=" + parity +
                "|inputEffectsMatch=" + inputEffectsMatch + "|baselineUnchanged=" + source + "|catalogIndexParity=" + graph);
        }

        static void CartQueryCompareMutableCase(Trace trace, string label, CartQueryReferenceCase test,
            CartQueryReferenceCall live, CartQueryReferenceCall frozen, string liveBefore, string frozenBefore,
            bool? expectedSuccess, bool unityRandomUnchanged, bool? suppliedGraphParity = null)
        {
            var fixture = test.Fixture;
            bool output = suppliedGraphParity.HasValue
                ? C0ErrorsEqual(live.Error, frozen.Error) && live.JsonCalls == frozen.JsonCalls &&
                    (live.Error != null || suppliedGraphParity.Value)
                : CartQueryReferenceResultEqual(live, frozen);
            bool graph = suppliedGraphParity ?? CanonicalGraphEquivalent(fixture.Live, fixture.Frozen, fixture.Baseline, trace, label + ".partial-profile");
            bool topology = CartQueryTopologyMatches(fixture.Live, fixture.Frozen);
            bool catalog = CatalogSnapshot(fixture.Live) == CatalogSnapshot(fixture.Frozen) &&
                C0CatalogIndexSnapshot(fixture.Live.catalog) == C0CatalogIndexSnapshot(fixture.Frozen.catalog) &&
                C0MarketIndexSnapshot(fixture.Live) == C0MarketIndexSnapshot(fixture.Frozen);
            bool rows = fixture.RowsBefore == RowsSnapshot(fixture.LiveRows) && fixture.RowsBefore == RowsSnapshot(fixture.FrozenRows) &&
                C0SameRowReferences(fixture.LiveRowReferences, fixture.LiveRows) && C0SameRowReferences(fixture.FrozenRowReferences, fixture.FrozenRows);
            bool baseline = fixture.BaselineBefore == SourceSnapshot(fixture.Baseline) &&
                C0SameReferences(fixture.BaselineReferences, C0ProfileReferences(fixture.Baseline));
            bool identities = CartQueryReferenceIdentityParity(live.Identities, frozen.Identities);
            if (expectedSuccess.HasValue)
                trace.Assert((live.Error == null) == expectedSuccess.Value && (frozen.Error == null) == expectedSuccess.Value,
                    "legacy action reaches expected success/error boundary / " + label + " / " + live.Text + ";" + frozen.Text);
            trace.Assert(output && graph && topology && catalog, "live/frozen graph, identity-path mapping and side effects match / " + label +
                " / livePartial=" + (liveBefore != SourceSnapshot(fixture.Live)) + ";frozenPartial=" + (frozenBefore != SourceSnapshot(fixture.Frozen)));
            trace.Assert(rows && baseline && identities && unityRandomUnchanged,
                "legacy action retains row/baseline isolation and matching ID scope trace / " + label +
                " / scopes=" + C0IdentitySummary(live.Identities) + ";" + C0IdentitySummary(frozen.Identities));
            trace.Add("QUERY_APPLY_OR_PREVIEWPROFILE|" + label + "|live=" + live.Text + "|frozen=" + frozen.Text +
                "|livePartial=" + (liveBefore != SourceSnapshot(fixture.Live)) + ";frozenPartial=" + (frozenBefore != SourceSnapshot(fixture.Frozen)) +
                "|graph=" + graph + ";catalogIndex=" + catalog + ";rowInvariant=" + rows + ";identityScopeParity=" + identities);
        }

        static void CartQueryValuationPrices(Trace trace, PlayerProfile baseline, string carId,
            List<Tuple<string, ValuationKind, string>> assets, ValuationPolicy policy, long now)
        {
            foreach (var asset in assets)
                CartQueryValuationPriceCase(trace, baseline, carId, asset.Item1, asset.Item2, policy, now, asset.Item3);
        }

        static void CartQueryValuationPriceCase(Trace trace, PlayerProfile baseline, string carId,
            string assetId, ValuationKind kind, ValuationPolicy policy, long now, string label, string buyerId = "wholesale",
            string expectedErrorMessage = null)
        {
            var row = new PartCartLine { action = kind == ValuationKind.LoosePart ? CartAction.SellPart : CartAction.SellUnit,
                assetId = assetId, quantity = 1 };
            var test = CartQueryReferenceBuildCase(trace, "valuation-" + label, baseline, carId, new[] { row });
            if (test == null) return;
            var fixture = test.Fixture;
            bool unit = kind == ValuationKind.LooseElement;
            var live = CartQueryReferenceInvoke(() =>
            {
                var view = Valuations.LoosePriceView(fixture.Live, ValuationPolicy.Defaults(), buyerId, now);
                return view(assetId, unit);
            });
            var frozen = CartQueryReferenceInvoke(() =>
            {
                var view = LegacyCartQueryValuationReference.LoosePriceView(fixture.Frozen, ValuationPolicy.Defaults(), buyerId, now);
                return view(assetId, unit);
            });
            bool parity = CartQueryReferenceResultEqual(live, frozen);
            bool graph = CanonicalGraphEquivalent(fixture.Live, fixture.Frozen, fixture.Baseline, trace, "valuation-" + label + ".validation-effects");
            bool catalog = CatalogSnapshot(fixture.Live) == CatalogSnapshot(fixture.Frozen) &&
                C0CatalogIndexSnapshot(fixture.Live.catalog) == C0CatalogIndexSnapshot(fixture.Frozen.catalog) &&
                C0MarketIndexSnapshot(fixture.Live) == C0MarketIndexSnapshot(fixture.Frozen);
            bool rows = fixture.RowsBefore == RowsSnapshot(fixture.LiveRows) && fixture.RowsBefore == RowsSnapshot(fixture.FrozenRows);
            bool intended = expectedErrorMessage == null
                ? live.Error == null && frozen.Error == null && live.Value is int liveValue && liveValue >= 0 && frozen.Value is int frozenValue && frozenValue >= 0
                : CartQueryErrorIs(live, typeof(InvalidOperationException), expectedErrorMessage, null) &&
                    CartQueryErrorIs(frozen, typeof(InvalidOperationException), expectedErrorMessage, null);
            trace.Assert(parity && intended, "live LoosePriceView and frozen valuation closure reach source-pinned success/error / " + label +
                " / expected=" + (expectedErrorMessage ?? "nonnegative quote value") + ";live=" + live.Text + ";frozen=" + frozen.Text);
            trace.Assert(graph && catalog && rows && fixture.BaselineBefore == SourceSnapshot(fixture.Baseline),
                "valuation validation/index effects match on independent profiles / " + label +
                " / sourceMutationLive=" + (fixture.BaselineBefore != SourceSnapshot(fixture.Live)) +
                ";sourceMutationFrozen=" + (fixture.BaselineBefore != SourceSnapshot(fixture.Frozen)));
            trace.Add("QUERY_PRICE|" + label + "|kind=" + kind + "|now=" + now.ToString(CultureInfo.InvariantCulture) +
                "|live=" + live.Text + "|frozen=" + frozen.Text + "|validationGraph=" + graph + ";catalogIndex=" + catalog);
        }

        static void CartQueryValuationViewRevision(Trace trace, PlayerProfile baseline, string carId,
            string assetId, ValuationKind kind, ValuationPolicy policy)
        {
            var test = CartQueryReferenceBuildCase(trace, "valuation-revision-fence", baseline, carId,
                new[] { new PartCartLine { action = kind == ValuationKind.LoosePart ? CartAction.SellPart : CartAction.SellUnit,
                    assetId = assetId, quantity = 1 } });
            if (test == null) return;
            var fixture = test.Fixture; bool unit = kind == ValuationKind.LooseElement;
            var live = CartQueryReferenceInvoke(() =>
            {
                var view = Valuations.LoosePriceView(fixture.Live, ValuationPolicy.Defaults(), "wholesale", CartPreviewCharacterizationFixtures.FixedNowTicks);
                fixture.Live.revision++;
                return view(assetId, unit);
            });
            var frozen = CartQueryReferenceInvoke(() =>
            {
                var view = LegacyCartQueryValuationReference.LoosePriceView(fixture.Frozen, ValuationPolicy.Defaults(), "wholesale", CartPreviewCharacterizationFixtures.FixedNowTicks);
                fixture.Frozen.revision++;
                return view(assetId, unit);
            });
            trace.Assert(CartQueryReferenceResultEqual(live, frozen) && live.Error is InvalidOperationException &&
                live.Error.Message == "Price view changed; refresh inventory.",
                "both loose price views reject a revision change with the same exact stale-view error / live=" + live.Text + ";frozen=" + frozen.Text);
            trace.Assert(CanonicalGraphEquivalent(fixture.Live, fixture.Frozen, fixture.Baseline, trace, "valuation-revision-fence") &&
                fixture.Live.revision == fixture.Frozen.revision,
                "stale valuation calls leave equivalent independently mutated revisions / " + fixture.Live.revision + "/" + fixture.Frozen.revision);
        }

        static void CartQueryValuationInvalidPolicyCase(Trace trace, PlayerProfile baseline, string carId,
            string assetId, ValuationKind kind)
        {
            var test = CartQueryReferenceBuildCase(trace, "valuation-invalid-policy", baseline, carId,
                new[] { new PartCartLine { action = kind == ValuationKind.LoosePart ? CartAction.SellPart : CartAction.SellUnit,
                    assetId = assetId, quantity = 1 } });
            if (test == null) return;
            var fixture = test.Fixture;
            var live = CartQueryReferenceInvoke(() =>
            {
                var policy = ValuationPolicy.Defaults(); policy.quoteSeconds = 0;
                return Valuations.LoosePriceView(fixture.Live, policy, "wholesale", CartPreviewCharacterizationFixtures.FixedNowTicks);
            });
            var frozen = CartQueryReferenceInvoke(() =>
            {
                var policy = ValuationPolicy.Defaults(); policy.quoteSeconds = 0;
                return LegacyCartQueryValuationReference.LoosePriceView(fixture.Frozen, policy, "wholesale", CartPreviewCharacterizationFixtures.FixedNowTicks);
            });
            trace.Assert(CartQueryReferenceResultEqual(live, frozen) &&
                CartQueryErrorIs(live, typeof(InvalidOperationException), "Invalid valuation policy identity/lifetime/core value.", null) &&
                CartQueryErrorIs(frozen, typeof(InvalidOperationException), "Invalid valuation policy identity/lifetime/core value.", null),
                "invalid valuation policy reaches the exact source-pinned validation error / " + live.Text + ";" + frozen.Text);
            trace.Assert(CanonicalGraphEquivalent(fixture.Live, fixture.Frozen, fixture.Baseline, trace, "valuation-invalid-policy") &&
                C0CatalogIndexSnapshot(fixture.Live.catalog) == C0CatalogIndexSnapshot(fixture.Frozen.catalog),
                "invalid-policy quote validation side effects match on isolated profiles");
        }

        sealed class CartQueryInputObservation
        {
            internal readonly SortedDictionary<string, string> Fields;
            internal readonly SortedDictionary<string, object> References;
            internal CartQueryInputObservation(PlayerProfile source)
            { Fields = FlattenNormalization(source); References = NormalizationReferences(source); }
        }

        // Scalar values and generated-ID bijections remain separately checked by
        // CanonicalGraphEquivalent. This comparator checks only reference topology.
        static bool CartQueryTopologyMatches(object left, object right)
            => NormalizationReferenceTopology(left).SequenceEqual(NormalizationReferenceTopology(right));

        static string CartQueryNaturalNullReferenceMessage()
        {
            // Mono runtime-thrown and constructed NRE messages differ.
            object absent = null;
            try { absent.ToString(); }
            catch (NullReferenceException error) { return error.Message; }
            throw new InvalidOperationException("Natural null-reference probe did not throw.");
        }

        static string[] CartQueryChangedReferencePaths(SortedDictionary<string, object> before, SortedDictionary<string, object> after)
            => before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal)
                .Where(path => !before.TryGetValue(path, out var oldValue) || !after.TryGetValue(path, out var newValue) ||
                    !ReferenceEquals(oldValue, newValue)).ToArray();

        static bool CartQueryLegacyInputEffects(Trace trace, string label, C0Fixture fixture,
            CartQueryInputObservation liveBefore, CartQueryInputObservation frozenBefore)
        {
            var liveAfter = FlattenNormalization(fixture.Live);
            var frozenAfter = FlattenNormalization(fixture.Frozen);
            var liveChanges = NormalizationDiff(liveBefore.Fields, liveAfter);
            var frozenChanges = NormalizationDiff(frozenBefore.Fields, frozenAfter);
            var liveReferenceChanges = CartQueryChangedReferencePaths(liveBefore.References, NormalizationReferences(fixture.Live));
            var frozenReferenceChanges = CartQueryChangedReferencePaths(frozenBefore.References, NormalizationReferences(fixture.Frozen));
            bool matches = liveBefore.Fields.SequenceEqual(frozenBefore.Fields) && liveAfter.SequenceEqual(frozenAfter) &&
                liveChanges.SequenceEqual(frozenChanges, StringComparer.Ordinal) &&
                liveReferenceChanges.SequenceEqual(frozenReferenceChanges, StringComparer.Ordinal) &&
                CartQueryTopologyMatches(fixture.Live, fixture.Frozen);
            bool pure = liveChanges.Count == 0 && frozenChanges.Count == 0 &&
                liveReferenceChanges.Length == 0 && frozenReferenceChanges.Length == 0;
            trace.Add("LEGACY_INPUT_EFFECTS|" + label + "|exactDeltaParity=" + matches + "|sourcePure=" + pure +
                "|fields=" + liveChanges.Count + "/" + frozenChanges.Count +
                "|referencePaths=" + liveReferenceChanges.Length + "/" + frozenReferenceChanges.Length +
                "|candidateRequirement=zero-input-mutations;legacy-effects-are-not-waived");
            foreach (var change in liveChanges) trace.Add("LEGACY_SOURCE_FIELD|" + label + "|" + change);
            foreach (var path in liveReferenceChanges) trace.Add("LEGACY_SOURCE_REFERENCE|" + label + "|" + path);
            if (!pure) trace.Add("LEGACY_PURITY|FAIL|" + label + "|strict source preservation remains a required typed-candidate divergence");
            return matches;
        }

        static string CQRReportSafe(string value)
            => new string((value ?? string.Empty).Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
    }
}
#endif
