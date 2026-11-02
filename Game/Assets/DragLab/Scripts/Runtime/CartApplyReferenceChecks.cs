#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Independent frozen-versus-live raw Apply liveness checks for L1C0.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        enum C0CatalogShape { Warm, ColdIndex, NullPartRow }

        sealed class C0Outcome
        {
            internal Exception Error;
            internal RuntimeEntropy.IdentityObservation Identities;
        }

        sealed class C0Fixture
        {
            internal PlayerProfile Baseline, Live, Frozen;
            internal MechanicalCatalog LiveCatalog, FrozenCatalog;
            internal string CarId;
            internal PartCartLine[] LiveRows, FrozenRows;
            internal string BaselineBefore, RowsBefore;
            internal string BaselineIndexBefore;
            internal string BaselineMarketIndexBefore;
            internal object[] BaselineReferences, LiveRowReferences, FrozenRowReferences;
            internal string LiveMarketIndexBefore, FrozenMarketIndexBefore;
            internal C0Outcome LiveOutcome, FrozenOutcome;
        }

        /// <summary>Root-owned runner invokes this after the frozen source manifest is pinned.</summary>
        public static void RunCartApplyReference(Action<bool, string> check, string folder)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("An isolated Editor report folder is required.", nameof(folder));
            Directory.CreateDirectory(folder);
            var trace = new Trace(check, folder, true);
            trace.Add("S06D2B1L1C0_CART_APPLY_REFERENCE_V1");
            trace.Add("oracle=LegacyCartApplyReference.Apply;live=PartCart.Apply;fixture=independent profiles and independent MechanicalCatalog instances;candidate=none");
            try
            {
                C0RunRawGuardMatrix(trace);
                C0RunAcquisitionMatrix(trace);
                C0RunMoveAndOrderingMatrix(trace);
                C0RunUnitSubtypeMatrix(trace);
                C0RunFailurePrefixMatrix(trace);
                C0RunCatalogLayoutMatrix(trace);
                C0RunMalformedDealerAndCashMatrix(trace);
            }
            catch (Exception error)
            {
                trace.Assert(false, "L1C0 runner completed without an unhandled exception / " + C0Error(error));
                trace.Add("RUNNER_EXCEPTION|" + C0Error(error));
            }
            finally
            {
                trace.Add("GATE|C0|independent literal Apply closure liveness only;no typed projection implemented or measured");
                File.WriteAllText(Path.Combine(folder, "S06D2B1L1C0_CART_APPLY.txt"), trace.Text.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
            }
        }

        static void C0RunRawGuardMatrix(Trace trace)
        {
            C0CompareNoOwner(trace, "null-owner-empty", Array.Empty<PartCartLine>(), true);
            C0CompareNoOwner(trace, "null-owner-sale-skips", new[] {
                new PartCartLine { action = CartAction.SellPart, assetId = "absent-part" },
                new PartCartLine { action = CartAction.SellUnit, assetId = "absent-unit" }
            }, true);
            C0CompareNoOwner(trace, "null-owner-nonpositive-purchases", new[] {
                new PartCartLine { action = CartAction.NewSet, quantity = 0 },
                new PartCartLine { action = CartAction.NewUnits, quantity = -1 },
                new PartCartLine { action = CartAction.BuyUsed, quantity = 0 }
            }, true);
            C0CompareNoOwner(trace, "null-owner-move-runs-once-even-at-zero-quantity", new[] {
                new PartCartLine { action = CartAction.Move, quantity = 0, assetId = "absent", destination = InventoryKind.Storage, destinationId = SharedInventory.Storage }
            }, false);
            C0CompareNoOwner(trace, "null-row-natural-first-error", new PartCartLine[] { null }, false);
            C0CompareNoOwner(trace, "null-lines-natural-first-error", null, false, passNullRows: true);

            CartPreviewCharacterizationFixtures.CreateNormalized(out var active, out var carId, 16102031u);
            active.runCheckpoints.Add(new RunCheckpoint { runId = "l1c0-guard-only", ownedVehicleId = carId, settled = false });
            var car = active.cars.Find(value => value.instanceId == carId);
            var set = FindOffer(active, car, PartCategory.HeadIntake, false);
            if (set == null) { trace.Assert(false, "active-run guard fixture has an actual compatible stocked NewSet definition"); return; }
            C0CompareCase(trace, "active-run-NewSet-install-false-retains-legacy-path", active, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1, install = false }
            }, expectedSuccess: true);
            C0CompareCase(trace, "active-run-NewSet-install-true-preserves-Replace-route", active, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1, install = true }
            }, expectedSuccess: true);
            var genericTarget = CoreRowsInstalledUnitTarget(active, car);
            if (genericTarget == null) { trace.Assert(false, "active-run guard fixture has an actual installed NewUnits role and position"); return; }
            C0CompareCase(trace, "active-run-NewUnits-fails-at-mutable-car-guard", active, carId, new[] {
                new PartCartLine { action = CartAction.NewUnits, definitionId = genericTarget.Definition.id, role = genericTarget.Role, quantity = 1, install = false }
            }, expectedSuccess: false);
            uint usedSeed = 161041u;
            var usedPart = CartPreviewCharacterizationFixtures.AddUsedPart(active, "l1c0/active-used-part", ref usedSeed);
            C0CompareCase(trace, "active-run-BuyUsed-part-install-fails-after-cash-and-dealer-removal", active, carId, new[] {
                new PartCartLine { action = CartAction.BuyUsed, assetId = usedPart.Id, quantity = 1, install = true }
            }, expectedSuccess: false);
        }

        static void C0RunAcquisitionMatrix(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, 16102032u);
            var car = source.cars.Find(value => value.instanceId == carId);
            var set = FindOffer(source, car, PartCategory.HeadIntake, false);
            if (set == null) { trace.Assert(false, "NewSet matrix has an actual compatible stocked HeadIntake definition"); return; }
            C0CompareCase(trace, "NewSet-quantity-zero-preview-true", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 0, install = true }
            }, preview: true, expectedSuccess: true);
            C0CompareCase(trace, "NewSet-quantity-two-loose", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 2, install = false }
            }, expectedSuccess: true);
            C0CompareCase(trace, "NewSet-quantity-two-first-only-install", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 2, install = true }
            }, expectedSuccess: true);
            C0CompareCase(trace, "NewSet-negative-quantity", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = -2, install = false }
            }, expectedSuccess: true);

            var genericTarget = CoreRowsInstalledUnitTarget(source, car);
            C0LogCategoryFacts(trace, "generic-NewUnits", source, car, genericTarget?.Definition?.category ?? PartCategory.HeadIntake,
                genericTarget?.Role, genericTarget?.Positions?.Length ?? 0);
            if (genericTarget == null) { trace.Assert(false, "NewUnits matrix has an actual installed generic unit role and position"); return; }
            C0CompareCase(trace, "NewUnits-quantity-zero", source, carId, new[] {
                new PartCartLine { action = CartAction.NewUnits, definitionId = genericTarget.Definition.id, role = genericTarget.Role, quantity = 0 }
            }, expectedSuccess: true);
            C0CompareCase(trace, "NewUnits-quantity-two-partial-install", source, carId, new[] {
                new PartCartLine { action = CartAction.NewUnits, definitionId = genericTarget.Definition.id, role = genericTarget.Role, quantity = 2,
                    install = true, positions = genericTarget.Positions.Take(1).ToList() }
            }, expectedSuccess: true);
            C0CompareCase(trace, "NewUnits-unknown-definition-first-error", source, carId, new[] {
                new PartCartLine { action = CartAction.NewUnits, definitionId = "l1c0-missing-definition", role = genericTarget.Role, quantity = 1 }
            }, expectedSuccess: false);
            C0CompareCase(trace, "NewUnits-invalid-role-first-error", source, carId, new[] {
                new PartCartLine { action = CartAction.NewUnits, definitionId = genericTarget.Definition.id, role = "l1c0-invalid-role", quantity = 1 }
            }, expectedSuccess: false);
            C0CompareCase(trace, "NewUnits-invalid-position-after-purchase", source, carId, new[] {
                new PartCartLine { action = CartAction.NewUnits, definitionId = genericTarget.Definition.id, role = genericTarget.Role,
                    quantity = 1, install = true, positions = new List<string> { "l1c0-invalid-position" } }
            }, expectedSuccess: false);

            var fallbackDefinition = C0FindOtherModelDefinition(source, car, out int fallbackCandidates);
            trace.Add("ACTUAL_CROSS_MODEL_COUNTS|authoredCandidates=" + fallbackCandidates.ToString(CultureInfo.InvariantCulture) +
                ";selectedCar=" + car.vehicleId + ";selectedFitRejected=" + (fallbackDefinition == null ? "unavailable" : "true") +
                ";fitsAny=" + (fallbackDefinition != null && PartsMarket.FitsAny(source, fallbackDefinition)));
            if (fallbackDefinition == null)
            { trace.Assert(false, "actual authored supported definition exists which fails selected-car fit but fits another authored model / candidates=" + fallbackCandidates); return; }
            C0CompareCase(trace, "NewSet-cross-model-Reference-and-Count-fallback", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = fallbackDefinition.id, quantity = 1, install = false }
            }, expectedSuccess: true);
            var fallbackRoles = InventoryUnits.Roles(source, car, fallbackDefinition);
            if (fallbackRoles == null || fallbackRoles.Length == 0)
            { trace.Assert(false, "actual cross-model authored definition resolves at least one modeled unit role"); return; }
            C0CompareCase(trace, "NewUnits-cross-model-UnitSpec-Reference-fallback", source, carId, new[] {
                new PartCartLine { action = CartAction.NewUnits, definitionId = fallbackDefinition.id, role = fallbackRoles[0], quantity = 1, install = false }
            }, expectedSuccess: true);

            uint usedSeed = 391031u;
            var usedPart = CartPreviewCharacterizationFixtures.AddUsedPart(source, "l1c0/actual-used-part", ref usedSeed);
            C0CompareCase(trace, "BuyUsed-actual-part-loose", source, carId, new[] {
                new PartCartLine { action = CartAction.BuyUsed, assetId = usedPart.Id, quantity = 1, install = false }
            }, expectedSuccess: true);
            C0CompareCase(trace, "BuyUsed-actual-part-installed", source, carId, new[] {
                new PartCartLine { action = CartAction.BuyUsed, assetId = usedPart.Id, definitionId = usedPart.DefinitionId, quantity = 1, install = true }
            }, expectedSuccess: true);
            C0CompareCase(trace, "BuyUsed-part-second-iteration-fails-after-first-effects", source, carId, new[] {
                new PartCartLine { action = CartAction.BuyUsed, assetId = usedPart.Id, quantity = 2, install = false }
            }, expectedSuccess: false);

            DealerStock usedUnit = null;
            int usedUnitAttempts = 0;
            uint usedUnitSeed = 391031u;
            for (int attempt = 0; attempt < 96 && usedUnit == null; attempt++)
            {
                usedUnitAttempts = attempt + 1;
                uint random = unchecked(usedUnitSeed + (uint)attempt); if (random == 0) random = 1;
                int first = source.inventory.dealer.Count;
                PartsMarket.AddUsedSupply(source, InventoryUnits.Spec(source, car, genericTarget.Definition), genericTarget.Definition,
                    "l1c0/actual-used-unit/" + attempt.ToString(CultureInfo.InvariantCulture), ref random);
                usedUnit = source.inventory.dealer.Skip(first).FirstOrDefault(row => row != null && row.IsUnit &&
                    row.DefinitionId == genericTarget.Definition.id && row.unit?.element != null &&
                    row.unit.element.role == genericTarget.Role && row.unit.element.Validate() == null &&
                    !string.IsNullOrWhiteSpace(row.unit.element.id));
                if (usedUnit == null) source.inventory.dealer.RemoveRange(first, source.inventory.dealer.Count - first);
            }
            trace.Add("ACTUAL_UNIT_GENERATOR|category=" + genericTarget.Definition.category + ";attempts=" +
                usedUnitAttempts.ToString(CultureInfo.InvariantCulture) + ";seed=" + usedUnitSeed.ToString(CultureInfo.InvariantCulture) +
                ";definition=" + genericTarget.Definition.id + ";role=" + genericTarget.Role +
                ";dealerRows=" + source.inventory.dealer.Count.ToString(CultureInfo.InvariantCulture));
            if (usedUnit == null)
            { trace.Assert(false, "actual AddUsedSupply yields a valid unit matching the installed generic definition and role after 96 attempts"); return; }
            var usedUnitPositions = InventoryUnits.Positions(source, car, genericTarget.Definition.id, genericTarget.Role);
            if (usedUnitPositions == null || usedUnitPositions.Length == 0)
            { trace.Assert(false, "actual BuyUsed unit has a compatible authored target position"); return; }
            C0CompareCase(trace, "BuyUsed-actual-unit-installed", source, carId, new[] {
                new PartCartLine { action = CartAction.BuyUsed, assetId = usedUnit.Id, definitionId = genericTarget.Definition.id,
                    role = genericTarget.Role, quantity = 1, install = true, positions = new List<string> { usedUnitPositions[0] } }
            }, expectedSuccess: true);
            C0CompareCase(trace, "BuyUsed-unit-second-iteration-fails-after-first-effects", source, carId, new[] {
                new PartCartLine { action = CartAction.BuyUsed, assetId = usedUnit.Id, quantity = 2, install = false }
            }, expectedSuccess: false);

            C0CompareCase(trace, "undefined-action-used-stock-fallthrough", source, carId, new[] {
                new PartCartLine { action = (CartAction)999, assetId = usedPart.Id, quantity = 1, install = false }
            }, expectedSuccess: true);
            C0CompareCase(trace, "sale-skips-around-purchase", source, carId, new[] {
                new PartCartLine { action = CartAction.SellPart, assetId = "missing-sale-part", quantity = 4 },
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1, install = false },
                new PartCartLine { action = CartAction.SellUnit, assetId = "missing-sale-unit", quantity = 4 }
            }, expectedSuccess: true);

            CartPreviewCharacterizationFixtures.CreateNormalized(out var aliasSource, out var aliasCarId, 16102033u);
            var aliasCar = aliasSource.cars.Find(value => value.instanceId == aliasCarId);
            var aliasBase = FindOffer(aliasSource, aliasCar, PartCategory.HeadIntake, false);
            if (aliasBase == null || !C0AddDefinitionAlias(aliasSource, aliasBase, out var aliasId))
            { trace.Assert(false, "catalog alias fixture adds a copied authored definition and same-row stock alias"); return; }
            C0CompareCase(trace, "NewSet-authored-definition-alias-cold-index", aliasSource, aliasCarId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = aliasId, quantity = 1, install = false }
            }, C0CatalogShape.ColdIndex, expectedSuccess: true);
        }

        static void C0RunMoveAndOrderingMatrix(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, 16102034u);
            var car = source.cars.Find(value => value.instanceId == carId);
            var binding = car.installed.slots.FirstOrDefault(value => value.slot == PartCategory.HeadIntake.ToString());
            var displaced = binding == null ? null : source.parts.Find(value => value.instanceId == binding.partInstanceId);
            var set = FindOffer(source, car, PartCategory.HeadIntake, false);
            if (displaced == null || set == null) { trace.Assert(false, "ordered install/move fixture has actual installed root and stocked replacement"); return; }
            C0CompareCase(trace, "purchase-Move-purchase-sequence-observes-displaced-root", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1, install = true },
                new PartCartLine { action = CartAction.Move, assetId = displaced.instanceId, quantity = 0,
                    destination = InventoryKind.Storage, destinationId = SharedInventory.Garage },
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1, install = false }
            }, expectedSuccess: true);

            var forward = C0CompareCase(trace, "order-pair-forward-purchase-then-return-displaced-root", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1, install = true },
                new PartCartLine { action = CartAction.Move, assetId = displaced.instanceId, quantity = 0,
                    destination = InventoryKind.Car, destinationId = carId }
            }, expectedSuccess: true);
            var reverse = C0CompareCase(trace, "order-pair-reverse-return-root-then-purchase", source, carId, new[] {
                new PartCartLine { action = CartAction.Move, assetId = displaced.instanceId, quantity = 0,
                    destination = InventoryKind.Car, destinationId = carId },
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1, install = true }
            }, expectedSuccess: true);
            if (forward != null && reverse != null && forward.LiveOutcome?.Error == null && reverse.LiveOutcome?.Error == null)
            {
                string forwardInstalled = forward.Live.cars.Find(value => value.instanceId == carId)?.installed?.slots
                    ?.FirstOrDefault(value => value.slot == PartCategory.HeadIntake.ToString())?.partInstanceId;
                string reverseInstalled = reverse.Live.cars.Find(value => value.instanceId == carId)?.installed?.slots
                    ?.FirstOrDefault(value => value.slot == PartCategory.HeadIntake.ToString())?.partInstanceId;
                trace.Assert(forwardInstalled == displaced.instanceId && reverseInstalled != displaced.instanceId &&
                    !string.IsNullOrEmpty(reverseInstalled) && forwardInstalled != reverseInstalled,
                    "reversing actual purchase/Move rows changes the installed root / forward=" + forwardInstalled + ";reverse=" + reverseInstalled);
            }
            else trace.Assert(false, "both sides of the order-sensitive purchase/Move pair succeed before comparing final ownership");

            var engine = source.engines.Find(value => value.instanceId == car.engineAssemblyId);
            if (engine == null) { trace.Assert(false, "whole-engine Move fixture has the actual selected engine"); return; }
            C0CompareCase(trace, "whole-engine-detach-then-attach-each-Move-runs-once", source, carId, new[] {
                new PartCartLine { action = CartAction.Move, assetId = engine.instanceId, quantity = 3,
                    destination = InventoryKind.Storage, destinationId = SharedInventory.Storage },
                new PartCartLine { action = CartAction.Move, assetId = engine.instanceId, quantity = 0,
                    destination = InventoryKind.Car, destinationId = carId }
            }, expectedSuccess: true);

            CartPreviewCharacterizationFixtures.CreateNormalized(out var unitSource, out var unitCarId, 16102035u);
            uint usedSeed = 99173u;
            var dealerUnit = CartPreviewCharacterizationFixtures.AddUsedUnit(unitSource, "l1c0/mixed-move-unit", ref usedSeed);
            C0CompareCase(trace, "BuyUsed-unit-then-move-same-newly-loose-identity", unitSource, unitCarId, new[] {
                new PartCartLine { action = CartAction.BuyUsed, assetId = dealerUnit.Id, quantity = 1 },
                new PartCartLine { action = CartAction.Move, assetId = dealerUnit.unit.element.id, quantity = 0,
                    destination = InventoryKind.Storage, destinationId = SharedInventory.Storage }
            }, expectedSuccess: true);

            CartPreviewCharacterizationFixtures.CreateNormalized(out var tireSource, out var tireCarId, 16102044u);
            var tireCar = tireSource.cars.Find(value => value.instanceId == tireCarId);
            var tireOffer = tireCar == null ? null : FindOffer(tireSource, tireCar, PartCategory.Tires, false);
            if (tireOffer == null)
            { trace.Assert(false, "authored stocked tire kit exists for raw NewSet identity cases"); return; }
            C0CompareCase(trace, "NewSet-tires-loose-raw-empty-member-shape", tireSource, tireCarId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = tireOffer.id, quantity = 1 }
            }, expectedSuccess: true, rawTireIdentityCase: true);
            C0CompareCase(trace, "NewSet-tires-install-then-NewUnits-tire-corner", tireSource, tireCarId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = tireOffer.id, quantity = 1, install = true },
                new PartCartLine { action = CartAction.NewUnits, definitionId = tireOffer.id, role = "tire", quantity = 1,
                    install = true, positions = new List<string> { "tire:0" } }
            }, expectedSuccess: true, rawTireIdentityCase: true);
        }

        static void C0RunUnitSubtypeMatrix(Trace trace)
        {
            foreach (var category in new[] { PartCategory.Wheels, PartCategory.Tires })
            {
                CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId,
                    category == PartCategory.Wheels ? 16102036u : 16102037u);
                var car = source.cars.Find(value => value.instanceId == carId);
                C0LogCategoryFacts(trace, category.ToString(), source, car, category, null, 0);
                var target = IdentityFindCornerUnitTarget(source, car, category);
                if (target == null || target.Positions.Length == 0)
                { trace.Assert(false, "authored installed " + category + " pack and addressable corner positions exist"); continue; }
                C0CompareCase(trace, "NewUnits-actual-" + category + "-subtype-installed", source, carId, new[] {
                    new PartCartLine { action = CartAction.NewUnits, definitionId = target.Definition.id, role = target.Role,
                        quantity = 1, install = true, positions = new List<string> { target.Positions[0] } }
                }, expectedSuccess: true);

                uint seed = category == PartCategory.Wheels ? 55731u : 55737u;
                var dealer = C0AddUsedCornerUnit(source, car, target.Definition, category, ref seed, out int attempts);
                trace.Add("ACTUAL_UNIT_GENERATOR|category=" + category + ";attempts=" + attempts + ";seedAfter=" + seed.ToString(CultureInfo.InvariantCulture) +
                    ";dealerRows=" + source.inventory.dealer.Count.ToString(CultureInfo.InvariantCulture) +
                    ";subtype=" + (dealer?.unit?.element?.GetType().FullName ?? "<missing>"));
                if (dealer == null)
                { trace.Assert(false, "actual AddUsedSupply yields a real " + category + " dealer unit with the required runtime subtype"); continue; }
                var positions = InventoryUnits.Positions(source, car, dealer.DefinitionId, dealer.unit.element.role);
                if (positions == null || positions.Length == 0)
                { trace.Assert(false, "actual used " + category + " unit fits an authored installed corner"); continue; }
                C0CompareCase(trace, "BuyUsed-actual-" + category + "-subtype-installed", source, carId, new[] {
                    new PartCartLine { action = CartAction.BuyUsed, assetId = dealer.Id, definitionId = dealer.DefinitionId,
                        role = dealer.unit.element.role, quantity = 1, install = true, positions = new List<string> { positions[0] } }
                }, expectedSuccess: true);
            }
        }

        static void C0RunFailurePrefixMatrix(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, 16102038u);
            var car = source.cars.Find(value => value.instanceId == carId);
            var set = FindOffer(source, car, PartCategory.HeadIntake, false);
            var target = CoreRowsInstalledUnitTarget(source, car);
            if (set == null || target == null) { trace.Assert(false, "late-error fixtures have actual stocked NewSet and NewUnits definitions"); return; }
            C0CompareCase(trace, "NewSet-unknown-definition-first-error", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = "l1c0-missing-definition", quantity = 1 }
            }, expectedSuccess: false);
            C0CompareCase(trace, "successful-NewSet-prefix-then-null-row", source, carId, new PartCartLine[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 }, null
            }, expectedSuccess: false);
            C0CompareCase(trace, "successful-NewSet-prefix-then-missing-used-row", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 },
                new PartCartLine { action = CartAction.BuyUsed, assetId = "l1c0-missing-used-asset", quantity = 1 }
            }, expectedSuccess: false);
            C0CompareCase(trace, "NewUnits-adds-real-unit-before-null-position-error", source, carId, new[] {
                new PartCartLine { action = CartAction.NewUnits, definitionId = target.Definition.id, role = target.Role,
                    quantity = 1, install = true, positions = null }
            }, expectedSuccess: false);
        }

        static void C0RunCatalogLayoutMatrix(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var coldSource, out var coldCarId, 16102039u);
            var coldCar = coldSource.cars.Find(value => value.instanceId == coldCarId);
            var coldSet = FindOffer(coldSource, coldCar, PartCategory.HeadIntake, false);
            if (coldSet == null) { trace.Assert(false, "cold catalog fixture has a real stocked NewSet definition"); return; }
            C0CompareCase(trace, "catalog-cold-index-rebuild-does-not-cross-fixtures", coldSource, coldCarId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = coldSet.id, quantity = 1 }
            }, C0CatalogShape.ColdIndex, expectedSuccess: true);

            CartPreviewCharacterizationFixtures.CreateNormalized(out var malformedSource, out var malformedCarId, 16102040u);
            var malformedCar = malformedSource.cars.Find(value => value.instanceId == malformedCarId);
            var malformedSet = FindOffer(malformedSource, malformedCar, PartCategory.HeadIntake, false);
            if (malformedSet == null) { trace.Assert(false, "malformed catalog fixture has a real stocked NewSet definition"); return; }
            C0CompareCase(trace, "catalog-null-public-part-row-rebuild-error-order", malformedSource, malformedCarId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = malformedSet.id, quantity = 1 }
            }, C0CatalogShape.NullPartRow, expectedSuccess: false);
        }

        static void C0RunMalformedDealerAndCashMatrix(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, 16102041u);
            var car = source.cars.Find(value => value.instanceId == carId);
            var set = FindOffer(source, car, PartCategory.HeadIntake, false);
            if (set == null) { trace.Assert(false, "cash-boundary fixture has an actual stocked part"); return; }
            source.cash = 0;
            C0CompareCase(trace, "NewSet-insufficient-cash-first-error", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 }
            }, expectedSuccess: false);

            CartPreviewCharacterizationFixtures.CreateNormalized(out var malformed, out var malformedCarId, 16102042u);
            uint seed = 410021u;
            var usedPart = CartPreviewCharacterizationFixtures.AddUsedPart(malformed, "l1c0/malformed-dealer-part", ref seed);
            var usedUnit = CartPreviewCharacterizationFixtures.AddUsedUnit(malformed, "l1c0/malformed-dealer-unit", ref seed);
            malformed.inventory.dealer.Add(new DealerStock { part = usedPart.part, unit = usedUnit.unit });
            C0CompareCase(trace, "BuyUsed-both-payloads-invalid-discriminator", malformed, malformedCarId, new[] {
                new PartCartLine { action = CartAction.BuyUsed, assetId = null, quantity = 1 }
            }, expectedSuccess: false);

            CartPreviewCharacterizationFixtures.CreateNormalized(out var nullRow, out var nullRowCarId, 16102043u);
            nullRow.inventory.dealer.Insert(0, null);
            C0CompareCase(trace, "BuyUsed-null-dealer-entry-natural-error", nullRow, nullRowCarId, new[] {
                new PartCartLine { action = CartAction.BuyUsed, assetId = "missing-after-null-row", quantity = 1 }
            }, expectedSuccess: false);
        }

        static bool C0AddDefinitionAlias(PlayerProfile profile, PartDefinition source, out string aliasId)
        {
            aliasId = null;
            if (profile?.catalog?.parts == null || source == null || profile.inventory?.market?.parts == null) return false;
            aliasId = source.id + "/l1c0-alias";
            string copiedAliasId = aliasId;
            string sourceId = source.id;
            if (profile.catalog.parts.Any(value => value != null && value.id == copiedAliasId)) return false;
            var alias = source.Copy(); alias.id = copiedAliasId;
            profile.catalog.parts.Add(alias);
            profile.catalog.RebuildIndexes();
            var row = profile.inventory.market.parts.FirstOrDefault(value => value != null && value.definitions != null && value.definitions.Contains(sourceId));
            if (row == null) return false;
            row.definitions.Add(copiedAliasId);
            return true;
        }

        static PartDefinition C0FindOtherModelDefinition(PlayerProfile profile, OwnedCar car, out int authoredCandidates)
        {
            authoredCandidates = 0;
            if (profile?.catalog?.parts == null || car == null) return null;
            var selectedSpec = profile.catalog.Spec(car.vehicleId);
            var candidates = profile.catalog.parts.Where(value => value != null && value.price >= 0 && value.reputationRequired <= profile.reputation &&
                EngineElements.Supported(value.category) && value.category != PartCategory.Tires && value.category != PartCategory.Wheels &&
                !profile.catalog.Fits(selectedSpec, value) && PartsMarket.FitsAny(profile, value))
                .OrderBy(value => value.category).ThenBy(value => value.id, StringComparer.Ordinal).ToArray();
            authoredCandidates = candidates.Length;
            return candidates.FirstOrDefault();
        }

        static DealerStock C0AddUsedCornerUnit(PlayerProfile profile, OwnedCar car, PartDefinition definition,
            PartCategory category, ref uint seed, out int attempts)
        {
            attempts = 0;
            for (int attempt = 0; attempt < 128; attempt++)
            {
                attempts = attempt + 1;
                uint random = unchecked(seed + (uint)attempt); if (random == 0) random = 1;
                int first = profile.inventory.dealer.Count;
                PartsMarket.AddUsedSupply(profile, InventoryUnits.Spec(profile, car, definition), definition,
                    "l1c0/actual-" + category.ToString().ToLowerInvariant() + "/" + attempt.ToString(CultureInfo.InvariantCulture), ref random);
                var row = profile.inventory.dealer.Skip(first).FirstOrDefault(value => value != null && value.IsUnit &&
                    value.DefinitionId == definition.id && value.unit?.element != null &&
                    (category == PartCategory.Tires ? value.unit.element is TireCondition && value.unit.tireUnit : value.unit.element is WheelElement));
                if (row != null) { seed = random; return row; }
                profile.inventory.dealer.RemoveRange(first, profile.inventory.dealer.Count - first);
            }
            return null;
        }

        static void C0LogCategoryFacts(Trace trace, string label, PlayerProfile profile, OwnedCar car,
            PartCategory category, string selectedRole, int selectedPositionCount)
        {
            var spec = car == null ? null : profile.catalog.Spec(car.vehicleId);
            var definitions = profile.catalog.parts.Where(value => value != null && value.category == category &&
                (spec == null || profile.catalog.Fits(spec, value))).ToArray();
            var stockedDefinitions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in profile.inventory?.market?.parts ?? new List<NewPartStock>())
                if (row?.definitions != null) foreach (string id in row.definitions)
                    if (definitions.Any(definition => definition.id == id)) stockedDefinitions.Add(id);
            var binding = car?.installed?.slots?.FirstOrDefault(value => value.slot == category.ToString());
            var root = binding == null ? null : profile.parts.FirstOrDefault(value => value?.instanceId == binding.partInstanceId);
            string[] roles = root == null || root.definitionId == null ? Array.Empty<string>() :
                InventoryUnits.Roles(profile, car, profile.catalog.Find(root.definitionId));
            trace.Add("ACTUAL_FIXTURE_COUNTS|" + label + "|catalogParts=" + profile.catalog.parts.Count.ToString(CultureInfo.InvariantCulture) +
                ";compatibleDefinitions=" + definitions.Length.ToString(CultureInfo.InvariantCulture) +
                ";stockedDefinitions=" + stockedDefinitions.Count.ToString(CultureInfo.InvariantCulture) +
                ";marketRows=" + (profile.inventory?.market?.parts?.Count ?? -1).ToString(CultureInfo.InvariantCulture) +
                ";installedRoots=" + (root == null ? 0 : 1).ToString(CultureInfo.InvariantCulture) +
                ";availableRoles=" + roles.Length.ToString(CultureInfo.InvariantCulture) +
                ";selectedRole=" + (selectedRole ?? "<none>") +
                ";selectedPositions=" + selectedPositionCount.ToString(CultureInfo.InvariantCulture) +
                ";dealerRows=" + (profile.inventory?.dealer?.Count ?? -1).ToString(CultureInfo.InvariantCulture));
        }

        static void C0CompareNoOwner(Trace trace, string label, PartCartLine[] rows, bool expectSuccess, bool passNullRows = false)
        {
            IEnumerable<PartCartLine> input = passNullRows ? null : rows;
            string rowsBefore = input == null ? "<null>" : RowsSnapshot(input.ToArray());
            var before = UnityEngine.Random.state;
            var live = C0Invoke(null, null, input, false, false);
            bool liveRandom = before.Equals(UnityEngine.Random.state);
            UnityEngine.Random.state = before;
            var frozen = C0Invoke(null, null, input, true, false);
            bool frozenRandom = before.Equals(UnityEngine.Random.state);
            bool outcomes = C0ErrorsEqual(live.Error, frozen.Error);
            trace.Assert(outcomes, "null-owner raw outcome matches frozen Apply / " + label + " / live=" + C0Error(live.Error) + ";frozen=" + C0Error(frozen.Error));
            trace.Assert(expectSuccess ? live.Error == null && frozen.Error == null : live.Error != null && frozen.Error != null,
                "null-owner guard case reaches its observed raw outcome / " + label);
            trace.Assert(input == null || rowsBefore == RowsSnapshot(input.ToArray()), "null-owner guard preserves raw row values/order / " + label);
            trace.Assert(liveRandom && frozenRandom, "raw guard vector preserves Unity RNG / " + label);
            trace.Assert(live.Identities.Closed && frozen.Identities.Closed && !live.Identities.Overflowed && !frozen.Identities.Overflowed,
                "raw guard identity observations close without overflow / " + label);
            trace.Add("RAW_GUARD|" + label + "|result=" + C0Error(live.Error) + "|idEvents=" + C0IdentitySummary(live.Identities) + ";" + C0IdentitySummary(frozen.Identities));
        }

        static C0Fixture C0CompareCase(Trace trace, string label, PlayerProfile baseline, string carId,
            PartCartLine[] rows, C0CatalogShape shape = C0CatalogShape.Warm, bool preview = false, bool? expectedSuccess = null,
            bool rawTireIdentityCase = false)
        {
            C0Fixture fixture;
            try { fixture = C0BuildFixture(baseline, carId, rows, shape); }
            catch (Exception error)
            { trace.Assert(false, "independent fixture setup succeeds / " + label + " / " + C0Error(error)); return null; }

            var random = UnityEngine.Random.state;
            var live = C0Invoke(fixture.Live, carId, fixture.LiveRows, false, preview);
            fixture.LiveOutcome = live;
            bool liveRandom = random.Equals(UnityEngine.Random.state);
            UnityEngine.Random.state = random;
            var frozen = C0Invoke(fixture.Frozen, carId, fixture.FrozenRows, true, preview);
            fixture.FrozenOutcome = frozen;
            bool frozenRandom = random.Equals(UnityEngine.Random.state);
            UnityEngine.Random.state = random;

            bool errorsEqual = C0ErrorsEqual(live.Error, frozen.Error);
            trace.Assert(errorsEqual, "live raw Apply and independent frozen reference have exact success/error parity / " + label +
                " / live=" + C0Error(live.Error) + ";frozen=" + C0Error(frozen.Error));
            if (expectedSuccess.HasValue)
                trace.Assert((live.Error == null) == expectedSuccess.Value && (frozen.Error == null) == expectedSuccess.Value,
                    "raw outcome matches required success/failure boundary / " + label + " / observed=" + C0Error(live.Error));
            bool graphEqual = CanonicalGraphEquivalent(fixture.Live, fixture.Frozen, fixture.Baseline, trace, label);
            trace.Assert(graphEqual, "full live/frozen partial or success graph and physical-ID path bijection / " + label);
            bool identityLifecycle = live.Identities != null && frozen.Identities != null && live.Identities.Closed && frozen.Identities.Closed &&
                !live.Identities.Overflowed && !frozen.Identities.Overflowed;
            trace.Assert(identityLifecycle, "identity observations close without overflow / " + label + " live=" + C0IdentitySummary(live.Identities) +
                ";frozen=" + C0IdentitySummary(frozen.Identities));
            if (live.Error == null && frozen.Error == null)
            {
                if (rawTireIdentityCase)
                    C0AssertRawTireIdentityParity(trace, label, fixture, live.Identities, frozen.Identities);
                else
                {
                    bool liveMapped = IdentityBuildMap(trace, label + "/live", fixture.Baseline, fixture.Live, fixture.LiveRows,
                        live.Identities, null, null, PhysicalIds(fixture.Baseline), out var leftMap, out _);
                    bool frozenMapped = IdentityBuildMap(trace, label + "/frozen", fixture.Baseline, fixture.Frozen, fixture.FrozenRows,
                        frozen.Identities, null, null, PhysicalIds(fixture.Baseline), out var rightMap, out _);
                    bool mapsEqual = liveMapped && frozenMapped && leftMap.Keys.OrderBy(value => value, StringComparer.Ordinal)
                        .SequenceEqual(rightMap.Keys.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal);
                    bool aliasedGraph = mapsEqual && TypedCanonical(fixture.Live, IdentityAliases(leftMap)) ==
                        TypedCanonical(fixture.Frozen, IdentityAliases(rightMap));
                    trace.Assert(aliasedGraph, "emitted identities map to the same row/quantity/member paths / " + label);
                }
            }
            else
                trace.Add("IDENTITY_PARTIAL|" + label + "|live=" + C0IdentitySummary(live.Identities) + ";frozen=" + C0IdentitySummary(frozen.Identities) +
                    "|ignored-events-may-precede-error;partial physical IDs compared by graph path");

            bool sourceUnchanged = fixture.BaselineBefore == SourceSnapshot(fixture.Baseline);
            bool sourceIndexUnchanged = fixture.BaselineIndexBefore == C0CatalogIndexSnapshot(fixture.Baseline.catalog);
            bool sourceMarketIndexUnchanged = fixture.BaselineMarketIndexBefore == C0MarketIndexSnapshot(fixture.Baseline);
            bool baselineReferencesUnchanged = C0SameReferences(fixture.BaselineReferences, C0ProfileReferences(fixture.Baseline));
            bool rowUnchanged = fixture.RowsBefore == RowsSnapshot(fixture.LiveRows) && fixture.RowsBefore == RowsSnapshot(fixture.FrozenRows) &&
                C0SameRowReferences(fixture.LiveRowReferences, fixture.LiveRows) && C0SameRowReferences(fixture.FrozenRowReferences, fixture.FrozenRows);
            bool separateCatalogs = !ReferenceEquals(fixture.Live.catalog, fixture.Frozen.catalog) &&
                !ReferenceEquals(fixture.Live.catalog, fixture.Baseline.catalog) && !ReferenceEquals(fixture.Frozen.catalog, fixture.Baseline.catalog) &&
                ReferenceEquals(fixture.Live.catalog, fixture.LiveCatalog) && ReferenceEquals(fixture.Frozen.catalog, fixture.FrozenCatalog) &&
                (fixture.Live.inventory.market.index == null && fixture.Frozen.inventory.market.index == null ||
                 !ReferenceEquals(fixture.Live.inventory.market.index, fixture.Frozen.inventory.market.index));
            bool catalogParity = CatalogSnapshot(fixture.Live) == CatalogSnapshot(fixture.Frozen) &&
                C0CatalogIndexSnapshot(fixture.Live.catalog) == C0CatalogIndexSnapshot(fixture.Frozen.catalog) &&
                C0MarketIndexSnapshot(fixture.Live) == C0MarketIndexSnapshot(fixture.Frozen);
            trace.Assert(sourceUnchanged, "baseline fixture values and references remain unchanged / " + label);
            trace.Assert(sourceIndexUnchanged && sourceMarketIndexUnchanged && baselineReferencesUnchanged,
                "baseline catalog/market indexes and profile object/list references remain unchanged / " + label);
            trace.Assert(rowUnchanged, "Apply preserves input row values, order, positions and references / " + label);
            trace.Assert(separateCatalogs, "each execution has its own MechanicalCatalog and unshared non-null market index / " + label);
            trace.Assert(catalogParity, "live and frozen catalog content/index/revision effects match without cross-fixture cache writes / " + label +
                " / liveIndex=" + C0Hash(C0CatalogIndexSnapshot(fixture.Live.catalog)) + ";frozenIndex=" + C0Hash(C0CatalogIndexSnapshot(fixture.Frozen.catalog)));
            trace.Assert(fixture.LiveMarketIndexBefore == fixture.FrozenMarketIndexBefore,
                "independent profiles begin with equivalent market-index state / " + label);
            trace.Assert(liveRandom && frozenRandom, "raw Apply/reference preserve Unity RNG state / " + label);
            trace.Add("APPLY_CASE|" + label + "|shape=" + shape + "|preview=" + preview + "|live=" + C0Error(live.Error) +
                "|frozen=" + C0Error(frozen.Error) + "|identity=" + C0IdentitySummary(live.Identities) + ";" + C0IdentitySummary(frozen.Identities) +
                "|graph=" + graphEqual + "|catalogParity=" + catalogParity);
            return fixture;
        }

        static void C0AssertRawTireIdentityParity(Trace trace, string label, C0Fixture fixture,
            RuntimeEntropy.IdentityObservation live, RuntimeEntropy.IdentityObservation frozen)
        {
            var rows = fixture.LiveRows;
            bool hasUnitInstall = rows != null && rows.Length == 2 && rows[0]?.action == CartAction.NewSet &&
                rows[1]?.action == CartAction.NewUnits && rows[1].install && rows[1].role == "tire";
            string[] expectedScopes = hasUnitInstall ? new[] { "part", "element", "tire" } : new[] { "part" };
            bool EventScopesMatch(RuntimeEntropy.IdentityObservation observation)
            {
                if (observation == null || !observation.Closed || observation.Overflowed || observation.Count != expectedScopes.Length) return false;
                for (int i = 0; i < expectedScopes.Length; i++)
                    if (!string.Equals(observation.EventAt(i).Scope, expectedScopes[i], StringComparison.Ordinal)) return false;
                return true;
            }

            var sourceIds = PhysicalIds(fixture.Baseline);
            bool EmissionsMatchOutput(PlayerProfile output, RuntimeEntropy.IdentityObservation observation,
                out int rawTireMemberCount, out int blankTireIds)
            {
                rawTireMemberCount = 0;
                blankTireIds = 0;
                if (!EventScopesMatch(observation)) return false;
                var generatedPaths = PhysicalIdPaths(output).Where(pair => !string.IsNullOrEmpty(pair.Value) && !sourceIds.Contains(pair.Value)).ToArray();
                var emittedIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < observation.Count; i++)
                {
                    var emission = observation.EventAt(i);
                    string id = (emission.Scope ?? string.Empty) + "-" + emission.Value.ToString("N");
                    if (!emittedIds.Add(id) || generatedPaths.Count(pair => pair.Value == id) != 1) return false;
                }
                if (generatedPaths.Length != emittedIds.Count || generatedPaths.Any(pair => !emittedIds.Contains(pair.Value))) return false;

                var car = output.cars.FirstOrDefault(value => value.instanceId == fixture.CarId);
                var tireBinding = car?.installed?.slots?.FirstOrDefault(value => value.slot == PartCategory.Tires.ToString());
                var installed = tireBinding == null ? null : output.parts.FirstOrDefault(value => value.instanceId == tireBinding.partInstanceId);
                if (!hasUnitInstall)
                {
                    var fresh = output.parts.Where(value => value != null && !sourceIds.Contains(value.instanceId) &&
                        value.definitionId == rows[0].definitionId).ToArray();
                    return fresh.Length == 1 && fresh[0].condition?.elements != null && fresh[0].condition.elements.Length == 0 &&
                        fresh[0].condition.tires.Length == 0 && observation.EventAt(0).Scope == "part" &&
                        fresh[0].instanceId == "part-" + observation.EventAt(0).Value.ToString("N");
                }

                var tireMembers = installed?.condition?.elements?.OfType<TireCondition>().ToArray() ?? Array.Empty<TireCondition>();
                rawTireMemberCount = tireMembers.Length;
                blankTireIds = tireMembers.Count(value => string.IsNullOrEmpty(value.id));
                if (tireMembers.Length != 4 || blankTireIds != 3 || installed.definitionId != rows[0].definitionId ||
                    installed.instanceId != "part-" + observation.EventAt(0).Value.ToString("N")) return false;
                string unitId = "element-" + observation.EventAt(1).Value.ToString("N");
                string displacedId = "tire-" + observation.EventAt(2).Value.ToString("N");
                var installedUnit = tireMembers.SingleOrDefault(value => value.id == unitId);
                var displaced = output.looseEngineElements.SingleOrDefault(value => value?.tireUnit == true && value.element?.id == displacedId);
                var baselineCar = fixture.Baseline.cars.FirstOrDefault(value => value.instanceId == fixture.CarId);
                var baselineBinding = baselineCar?.installed?.slots?.FirstOrDefault(value => value.slot == PartCategory.Tires.ToString());
                var oldRoot = baselineBinding == null ? null : fixture.Baseline.parts.FirstOrDefault(value => value.instanceId == baselineBinding.partInstanceId);
                var retainedOldRoot = oldRoot == null ? null : output.parts.FirstOrDefault(value => value.instanceId == oldRoot.instanceId);
                bool oldConditionPreserved = oldRoot != null && retainedOldRoot != null &&
                    TypedCanonical(oldRoot.condition, new Dictionary<string, string>()) ==
                    TypedCanonical(retainedOldRoot.condition, new Dictionary<string, string>());
                return tireBinding?.partInstanceId == installed?.instanceId && installedUnit != null && installedUnit.lastContactWorkJ == 0 &&
                    displaced?.tire != null && displaced.tire.id == displacedId && ReferenceEquals(displaced.tire, displaced.element) &&
                    displaced.definitionId == rows[0].definitionId &&
                    oldConditionPreserved;
            }

            bool scopesMatch = EventScopesMatch(live) && EventScopesMatch(frozen) &&
                Enumerable.Range(0, expectedScopes.Length).All(i =>
                    string.Equals(live.EventAt(i).Scope, frozen.EventAt(i).Scope, StringComparison.Ordinal));
            bool liveIdentity = EmissionsMatchOutput(fixture.Live, live, out int liveRawTireMembers, out int liveBlankTireIds);
            bool frozenIdentity = EmissionsMatchOutput(fixture.Frozen, frozen, out int frozenRawTireMembers, out int frozenBlankTireIds);
            trace.Assert(scopesMatch, "raw tire ID events match the known Apply-only allocations / " + label +
                " / expectedScopes=" + string.Join(",", expectedScopes));
            trace.Assert(liveIdentity && frozenIdentity, "raw tire nonempty emitted IDs biject to their owned output paths without assigning blank members / " + label);
            trace.Add("RAW_TIRE_IDENTITY|" + label + "|events=" + C0IdentitySummary(live) +
                "|mappingMode=explicitRawApplyOwnedPathMap|row0-part-event→new-kit|row1-element-event→installed-corner|row1-tire-event→displaced-corner" +
                "|liveNonemptyNewIds=" + live.Count.ToString(CultureInfo.InvariantCulture) +
                ";frozenNonemptyNewIds=" + frozen.Count.ToString(CultureInfo.InvariantCulture) +
                ";rawTireMembers=" + liveRawTireMembers.ToString(CultureInfo.InvariantCulture) + "/" + frozenRawTireMembers.ToString(CultureInfo.InvariantCulture) +
                ";blankInstalledTireMembers=" + liveBlankTireIds.ToString(CultureInfo.InvariantCulture) + "/" + frozenBlankTireIds.ToString(CultureInfo.InvariantCulture) +
                ";finalEnsureNotRun=true");
        }

        static C0Fixture C0BuildFixture(PlayerProfile baseline, string carId, PartCartLine[] rows, C0CatalogShape shape)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            if (baseline.catalog == null) throw new InvalidOperationException("A normalized authored catalog is required.");
            string baselineBefore = SourceSnapshot(baseline);
            string baselineIndexBefore = C0CatalogIndexSnapshot(baseline.catalog);
            string baselineMarketIndexBefore = C0MarketIndexSnapshot(baseline);
            object[] baselineReferences = C0ProfileReferences(baseline);
            var live = CartPreviewLiteralTestClone.Copy(baseline);
            var frozen = CartPreviewLiteralTestClone.Copy(baseline);
            live.catalog = C0NewIndependentCatalog(baseline.catalog, shape);
            frozen.catalog = C0NewIndependentCatalog(baseline.catalog, shape);
            if (ReferenceEquals(live.catalog, frozen.catalog) || ReferenceEquals(live.catalog, baseline.catalog) || ReferenceEquals(frozen.catalog, baseline.catalog))
                throw new InvalidOperationException("Literal profile isolation accidentally shared a MechanicalCatalog instance.");
            if (!C0AuthoredCatalogParity(baseline.catalog, live.catalog, shape) || !C0AuthoredCatalogParity(baseline.catalog, frozen.catalog, shape))
                throw new InvalidOperationException("Independent authored catalog definitions/specs/revisions differ from the fixture baseline.");
            if (C0CatalogIndexSnapshot(live.catalog) != C0CatalogIndexSnapshot(frozen.catalog))
                throw new InvalidOperationException("Independent catalogs do not begin with the same private lookup-index state.");
            if (live.inventory?.market?.index != null && ReferenceEquals(live.inventory.market.index, frozen.inventory?.market?.index))
                throw new InvalidOperationException("Independent fixture profiles unexpectedly share a market index instance.");
            string baselineProfileValues = TypedCanonical(baseline, new Dictionary<string, string>());
            if (baselineProfileValues != TypedCanonical(live, new Dictionary<string, string>()) ||
                baselineProfileValues != TypedCanonical(frozen, new Dictionary<string, string>()) ||
                !C0CloneTopologyMatches(baseline, live) || !C0CloneTopologyMatches(baseline, frozen))
                throw new InvalidOperationException("Literal fixture clones differ in profile values or object/list alias topology before catalog separation.");
            var liveRows = CopyRows(rows); var frozenRows = CopyRows(rows);
            string rowsBefore = RowsSnapshot(liveRows);
            if (rowsBefore != RowsSnapshot(frozenRows)) throw new InvalidOperationException("Paired raw input rows differ before Apply.");
            return new C0Fixture { Baseline = baseline, Live = live, Frozen = frozen, CarId = carId,
                LiveCatalog = live.catalog, FrozenCatalog = frozen.catalog,
                LiveRows = liveRows, FrozenRows = frozenRows, BaselineBefore = baselineBefore, RowsBefore = rowsBefore,
                BaselineIndexBefore = baselineIndexBefore, BaselineMarketIndexBefore = baselineMarketIndexBefore,
                BaselineReferences = baselineReferences,
                LiveMarketIndexBefore = C0MarketIndexSnapshot(live), FrozenMarketIndexBefore = C0MarketIndexSnapshot(frozen),
                LiveRowReferences = liveRows == null ? null : liveRows.Cast<object>().ToArray(),
                FrozenRowReferences = frozenRows == null ? null : frozenRows.Cast<object>().ToArray() };
        }

        static bool C0AuthoredCatalogParity(MechanicalCatalog baseline, MechanicalCatalog candidate, C0CatalogShape shape)
        {
            if (baseline == null || candidate == null || baseline.AuthoringRevision != candidate.AuthoringRevision) return false;
            int expectedCount = baseline.parts.Count + (shape == C0CatalogShape.NullPartRow ? 1 : 0);
            if (candidate.parts.Count != expectedCount) return false;
            for (int i = 0; i < baseline.parts.Count; i++)
                if (TypedCanonical(baseline.parts[i], new Dictionary<string, string>()) !=
                    TypedCanonical(candidate.parts[i], new Dictionary<string, string>())) return false;
            var specsField = typeof(MechanicalCatalog).GetField("specs", BindingFlags.Instance | BindingFlags.NonPublic);
            var baseSpecs = specsField?.GetValue(baseline) as IDictionary;
            var candidateSpecs = specsField?.GetValue(candidate) as IDictionary;
            if (specsField == null || baseSpecs == null || candidateSpecs == null || baseSpecs.Count != candidateSpecs.Count) return false;
            foreach (DictionaryEntry entry in baseSpecs)
                if (!candidateSpecs.Contains(entry.Key) || TypedCanonical(entry.Value, new Dictionary<string, string>()) !=
                    TypedCanonical(candidateSpecs[entry.Key], new Dictionary<string, string>())) return false;
            return true;
        }

        static bool C0CloneTopologyMatches(object source, object copy)
            => CloneTopologyMatches(source, copy, new Dictionary<object, object>(new ObjectReferenceComparer()),
                new Dictionary<object, object>(new ObjectReferenceComparer()));

        static MechanicalCatalog C0NewIndependentCatalog(MechanicalCatalog baseline, C0CatalogShape shape)
        {
            var catalog = MechanicsTransactions.Catalog();
            if (catalog == null || ReferenceEquals(catalog, baseline)) throw new InvalidOperationException("Catalog factory did not return a new authored catalog.");
            var aliases = baseline.parts.Where(value => value != null && value.id != null &&
                value.id.EndsWith("/l1c0-alias", StringComparison.Ordinal)).ToArray();
            if (aliases.Length > 0)
            {
                foreach (var alias in aliases) catalog.parts.Add(alias.Copy());
                catalog.RebuildIndexes();
            }
            if (shape == C0CatalogShape.NullPartRow) catalog.parts.Add(null);
            if (shape == C0CatalogShape.ColdIndex) C0SetCatalogIndexNull(catalog);
            return catalog;
        }

        static C0Outcome C0Invoke(PlayerProfile profile, string carId, IEnumerable<PartCartLine> rows, bool frozen, bool preview)
        {
            var result = new C0Outcome();
            using (result.Identities = RuntimeEntropy.BeginIdentityObservation(256))
            {
                try
                {
                    if (frozen) LegacyCartApplyReference.Apply(profile, carId, rows, preview, ValuationPolicy.Defaults());
                    else PartCart.Apply(profile, carId, rows, preview, ValuationPolicy.Defaults());
                }
                catch (Exception error) { result.Error = error; }
            }
            return result;
        }

        static bool C0ErrorsEqual(Exception left, Exception right)
            => left == null && right == null || left != null && right != null && left.GetType() == right.GetType() &&
                left.Message == right.Message && (!(left is ArgumentException leftArgument) || right is ArgumentException rightArgument && leftArgument.ParamName == rightArgument.ParamName);

        static string C0Error(Exception error)
            => error == null ? "none" : error.GetType().FullName + ":" + C0Safe(error.Message) +
                (error is ArgumentException argument ? ":param=" + C0Safe(argument.ParamName) : "");

        static string C0IdentitySummary(RuntimeEntropy.IdentityObservation observation)
        {
            if (observation == null) return "missing";
            var scopes = new List<string>();
            for (int i = 0; i < observation.Count; i++) scopes.Add(observation.EventAt(i).Scope ?? "<null>");
            return "count=" + observation.Count.ToString(CultureInfo.InvariantCulture) + ";closed=" + observation.Closed +
                ";overflow=" + observation.Overflowed + ";scopes=" + string.Join(",", scopes);
        }

        static string C0CatalogIndexSnapshot(MechanicalCatalog catalog)
        {
            if (catalog == null) return "catalog=null";
            var indexField = typeof(MechanicalCatalog).GetField("partIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            var countField = typeof(MechanicalCatalog).GetField("indexedPartCount", BindingFlags.Instance | BindingFlags.NonPublic);
            if (indexField == null || countField == null) throw new InvalidOperationException("Editor index diagnostic fields changed; update this characterization explicitly.");
            var index = indexField.GetValue(catalog) as IDictionary;
            var entries = new List<string>();
            if (index != null)
            {
                var valueType = index.GetType().GetGenericArguments()[1];
                var positionField = valueType.GetField("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var partField = valueType.GetField("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (positionField == null || partField == null) throw new InvalidOperationException("Editor catalog index entry metadata changed.");
                foreach (DictionaryEntry entry in index)
                {
                    int position = (int)positionField.GetValue(entry.Value);
                    var part = partField.GetValue(entry.Value) as PartDefinition;
                    bool localReference = position >= 0 && position < catalog.parts.Count && ReferenceEquals(catalog.parts[position], part);
                    entries.Add(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) + "=" + position.ToString(CultureInfo.InvariantCulture) + ":" +
                        (part?.id ?? "<null>") + ":sameSlot=" + localReference);
                }
            }
            entries.Sort(StringComparer.Ordinal);
            return "revision=" + catalog.AuthoringRevision.ToString(CultureInfo.InvariantCulture) + ";parts=" + catalog.parts.Count.ToString(CultureInfo.InvariantCulture) +
                ";indexedCount=" + Convert.ToString(countField.GetValue(catalog), CultureInfo.InvariantCulture) + ";null=" + (index == null) +
                ";entries=" + string.Join("|", entries);
        }

        static string C0MarketIndexSnapshot(PlayerProfile profile)
        {
            var market = profile?.inventory?.market;
            if (market == null) return "market=null";
            var field = typeof(PartsStockState).GetField("index", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) throw new InvalidOperationException("Editor market index diagnostic field changed; update this characterization explicitly.");
            var index = field.GetValue(market) as IDictionary;
            var entries = new List<string>();
            if (index != null) foreach (DictionaryEntry entry in index)
            {
                int row = -1;
                if (market.parts != null) for (int i = 0; i < market.parts.Count; i++)
                    if (ReferenceEquals(market.parts[i], entry.Value)) { row = i; break; }
                entries.Add(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) + "=" + row.ToString(CultureInfo.InvariantCulture));
            }
            entries.Sort(StringComparer.Ordinal);
            return "rows=" + (market.parts?.Count ?? -1).ToString(CultureInfo.InvariantCulture) + ";null=" + (index == null) + ";entries=" + string.Join("|", entries);
        }

        static void C0SetCatalogIndexNull(MechanicalCatalog catalog)
        {
            var field = typeof(MechanicalCatalog).GetField("partIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Editor catalog index diagnostic field partIndex was not found.");
            field.SetValue(catalog, null);
        }

        static string C0Hash(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""))).Replace("-", "");
        }

        static string C0Safe(string value)
        {
            value = (value ?? "<null>").Replace("\r", "\\r").Replace("\n", "\\n").Replace("|", "/");
            return value.Length <= 200 ? value : value.Substring(0, 200);
        }

        static object[] C0ProfileReferences(PlayerProfile profile)
        {
            var refs = new List<object> { profile, profile?.parts, profile?.cars, profile?.engines, profile?.looseEngineElements,
                profile?.runCheckpoints, profile?.inventory, profile?.inventory?.dealer, profile?.inventory?.market,
                profile?.inventory?.market?.parts, profile?.inventory?.market?.index, profile?.inventory?.cart,
                profile?.inventory?.cart?.lines, profile?.inventory?.cart?.supply, profile?.catalog, profile?.catalog?.parts };
            if (profile?.catalog != null)
            {
                var field = typeof(MechanicalCatalog).GetField("partIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) throw new InvalidOperationException("Editor catalog index diagnostic field partIndex was not found.");
                refs.Add(field.GetValue(profile.catalog));
            }
            if (profile?.parts != null) foreach (var part in profile.parts)
            { refs.Add(part); refs.Add(part?.condition); refs.Add(part?.condition?.elements); refs.Add(part?.condition?.tires); }
            if (profile?.cars != null) foreach (var car in profile.cars)
            { refs.Add(car); refs.Add(car?.installed); refs.Add(car?.installed?.slots); }
            if (profile?.engines != null) foreach (var engine in profile.engines)
            { refs.Add(engine); refs.Add(engine?.slots); }
            if (profile?.looseEngineElements != null) foreach (var unit in profile.looseEngineElements)
            { refs.Add(unit); refs.Add(unit?.element); refs.Add(unit?.tire); }
            if (profile?.inventory?.dealer != null) foreach (var stock in profile.inventory.dealer)
            { refs.Add(stock); refs.Add(stock?.part); refs.Add(stock?.unit); refs.Add(stock?.unit?.element); refs.Add(stock?.unit?.tire); }
            if (profile?.inventory?.market?.parts != null) foreach (var row in profile.inventory.market.parts)
            { refs.Add(row); refs.Add(row?.definitions); }
            if (profile?.inventory?.cart?.lines != null) foreach (var row in profile.inventory.cart.lines)
            { refs.Add(row); refs.Add(row?.positions); }
            if (profile?.inventory?.cart?.supply != null) foreach (var hold in profile.inventory.cart.supply) refs.Add(hold);
            return refs.ToArray();
        }

        static bool C0SameReferences(object[] left, object[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (!ReferenceEquals(left[i], right[i])) return false;
            return true;
        }

        static bool C0SameRowReferences(object[] expected, PartCartLine[] rows)
        {
            if (expected == null) return rows == null;
            if (rows == null || expected.Length != rows.Length) return false;
            for (int i = 0; i < rows.Length; i++) if (!ReferenceEquals(expected[i], rows[i])) return false;
            return true;
        }
    }
}
#endif
