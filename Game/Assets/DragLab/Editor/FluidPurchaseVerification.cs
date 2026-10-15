using System;
using System.IO;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    // Lightweight commerce regression: used-parts stock must not block buying liquids.
    public static class FluidPurchaseVerification
    {
        public static void Run()
        {
            var catalog = MechanicsTransactions.Catalog();
            var policy = new OperatingPolicy();
            var prices = ValuationPolicy.Defaults();
            long now = DateTime.UtcNow.Ticks;
            // Exercise authentic generated market listings, not only synthetic
            // inventory rows. A paid service must be able to quote after reload.
            foreach(int seed in new[]{1,77,912}){
                var generated=PlayerProfile.New(now);
                generated.cash=100000;
                VehicleListings.Initialize(generated,catalog,seed);
                MechanicsOwnership.Ensure(generated,catalog);
                string before=JsonUtility.ToJson(generated.inventory);
                var loaded=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(generated));
                MechanicsOwnership.Ensure(loaded,catalog);
                string generatedInvalid=MechanicsOwnership.Validate(loaded,catalog);
                if(generatedInvalid!=null)throw new InvalidOperationException("Generated dealer stock seed "+seed
                    +" failed to reload: "+generatedInvalid);
                if(JsonUtility.ToJson(loaded.inventory)!=before)
                    throw new InvalidOperationException("Generated market stock seed "+seed
                        +" changed when loaded without a purchase.");
            }
            var profile = PlayerProfile.New(now);
            MechanicsOwnership.Ensure(profile, catalog);
            profile.cash = 100000;
            var car = profile.SelectedCar;
            var model = catalog.Spec(car.vehicleId);
            var installedTires = profile.parts.First(p => p.instanceId ==
                car.installed.slots.First(s => s.slot == "Tires").partInstanceId);
            var tire = installedTires.condition.tires[0].Copy();
            tire.id = MechanicsOwnership.NewId("used-tire");
            tire.definitionId = installedTires.definitionId;
            tire.health = .61f;
            tire.consumedLife = .39;
            var definition = catalog.Find(tire.definitionId);
            var usedUnit = new LooseEngineElement
            {
                definitionId = tire.definitionId,
                unitPrice = InventoryPricing.NewUnit(definition, 4),
                element = tire,
                tireUnit = true
            };

            // Simulate mismatched saved discriminator and a serializer-created empty part.
            profile.inventory.dealer.Add(new DealerStock
            {
                isUnit = false,
                part = new PartInstance(),
                unit = usedUnit,
                paidPrice = 10
            });
            profile = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));
            MechanicsOwnership.Ensure(profile, catalog);
            var seller = profile.inventory.dealer.Single(s => s.unit != null &&
                s.unit.element != null && s.unit.element.id == tire.id);
            if (!seller.isUnit || seller.part != null || !(seller.unit.element is TireCondition) ||
                Math.Abs(seller.unit.tire.health - .61f) > .000001 ||
                Math.Abs(seller.unit.tire.consumedLife - .39) > .000001)
                throw new InvalidOperationException("Seller normalization changed a real typed tire or its wear history.");
            string sellerJson = JsonUtility.ToJson(profile.inventory);

            car = profile.SelectedCar;
            car.fuel.litres = model.fuel.capacityLitres - 3;
            car.fluids.oilLitres = model.fluids.oilCapacityLitres - 1;
            car.fluids.coolantLitres = model.fluids.coolantCapacityLitres - 2;
            car.fluids.oilCondition = .4;
            int cash = profile.cash;
            int checks = 0;
            void Purchase(PaymentKind kind, double oil, double coolant)
            {
                var request = new PaymentRequest
                {
                    kind = kind,
                    carId = car.instanceId,
                    litres = oil,
                    coolantLitres = coolant
                };
                var quote = PaidEconomy.Quote(profile, catalog, policy, prices, request, now);
                PlayerProfile next;
                string error;
                bool ok = MechanicsTransactions.Try(profile, catalog, profile.revision,
                    p => PaidEconomy.Pay(p, catalog, policy, prices, quote, now),
                    p => true, out next, out error);
                if (!ok) throw new InvalidOperationException(kind + " liquid purchase failed: " + error);
                profile = next;
                car = profile.SelectedCar;
                cash -= quote.Cost;
                if (profile.cash != cash) throw new InvalidOperationException(kind + " charged the wrong amount.");
                if (JsonUtility.ToJson(profile.inventory) != sellerJson)
                    throw new InvalidOperationException(kind + " changed unrelated dealer stock.");
                checks++;
            }

            Purchase(PaymentKind.Fuel, 3, 0);
            if (Math.Abs(car.fuel.litres - model.fuel.capacityLitres) > .000001 ||
                Math.Abs(profile.economyReceipts.Last().fuelAdded - 3) > .000001)
                throw new InvalidOperationException("Paid refuel failed to fill tank or record receipt.");
            Purchase(PaymentKind.Fluids, 1, 0);
            if (Math.Abs(car.fluids.oilLitres - model.fluids.oilCapacityLitres) > .000001 ||
                car.fluids.oilCondition != .4 ||
                Math.Abs(profile.economyReceipts.Last().oilAdded - 1) > .000001)
                throw new InvalidOperationException("Paid engine oil top-up changed aging or receipt.");
            Purchase(PaymentKind.Fluids, 0, 2);
            if (Math.Abs(car.fluids.coolantLitres - model.fluids.coolantCapacityLitres) > .000001 ||
                Math.Abs(profile.economyReceipts.Last().coolantAdded - 2) > .000001)
                throw new InvalidOperationException("Paid coolant top-up failed.");
            if (MechanicsOwnership.Validate(profile, catalog) != null)
                throw new InvalidOperationException("Fluid purchases left an invalid profile: " +
                    MechanicsOwnership.Validate(profile, catalog));

            // Simulate the Unity JSON inactive-member artifact on a LIVE profile,
            // without a pre-quote normalization pass. The transaction clone must
            // normalize it without losing dealer merchandise or charging twice.
            var phantom = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));
            MechanicsOwnership.Ensure(phantom, catalog);
            var phantomSeller = phantom.inventory.dealer.Single(s => s.IsUnit && s.Id == tire.id);
            phantomSeller.part = new PartInstance();
            if (MechanicsOwnership.Validate(phantom, catalog) != null)
                throw new InvalidOperationException("Empty inactive dealer part prevents unrelated payments.");
            phantom.SelectedCar.fuel.litres = model.fuel.capacityLitres - 1;
            int phantomCash = phantom.cash;
            var phantomQuote = PaidEconomy.Quote(phantom, catalog, policy, prices,
                new PaymentRequest { kind = PaymentKind.Fuel, carId = phantom.SelectedCar.instanceId, litres = 1 }, now);
            PlayerProfile phantomPaid;
            string phantomError;
            if (!MechanicsTransactions.Try(phantom, catalog, phantom.revision,
                state => PaidEconomy.Pay(state, catalog, policy, prices, phantomQuote, now),
                state => true, out phantomPaid, out phantomError))
                throw new InvalidOperationException("Fuel purchase rejected inactive seller payload: " + phantomError);
            var retainedSeller = phantomPaid.inventory.dealer.Single(s => s.Id == tire.id);
            if (phantomPaid.cash != phantomCash - phantomQuote.Cost ||
                retainedSeller.part != null || !(retainedSeller.unit.element is TireCondition) ||
                MechanicsOwnership.Validate(phantomPaid, catalog) != null)
                throw new InvalidOperationException("Fuel payment failed to retain normalized used tire stock.");

            // A stale discriminator on a valid individual dealer unit must not
            // prevent a liquid quote: normalization is performed on a copy.
            var stale = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));
            MechanicsOwnership.Ensure(stale, catalog);
            stale.SelectedCar.fluids.oilLitres = model.fluids.oilCapacityLitres - 1;
            var staleSeller = stale.inventory.dealer.Single(s => s.IsUnit && s.Id == tire.id);
            staleSeller.isUnit = false;
            staleSeller.part = new PartInstance();
            string staleBefore = JsonUtility.ToJson(stale);
            var staleQuote = PaidEconomy.Quote(stale, catalog, policy, prices,
                new PaymentRequest { kind = PaymentKind.Fluids, carId = stale.SelectedCar.instanceId, litres = 1 }, now);
            if (JsonUtility.ToJson(stale) != staleBefore)
                throw new InvalidOperationException("Quote silently modified the live seller state.");
            PlayerProfile stalePaid;
            string staleError;
            if (!MechanicsTransactions.Try(stale, catalog, stale.revision,
                state => PaidEconomy.Pay(state, catalog, policy, prices, staleQuote, now),
                state => true, out stalePaid, out staleError))
                throw new InvalidOperationException("Stale seller discriminator blocked oil refill: " + staleError);
            if (stalePaid.cash != stale.cash - staleQuote.Cost ||
                !stalePaid.inventory.dealer.Any(s => s.IsUnit && s.Id == tire.id) ||
                MechanicsOwnership.Validate(stalePaid, catalog) != null)
                throw new InvalidOperationException("Oil refill did not reconcile seller metadata safely.");

            // Selling an owned spare must not depend on a stale unrelated seller flag.
            var resale = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));
            MechanicsOwnership.Ensure(resale, catalog);
            var resaleRow = resale.inventory.dealer.Single(s => s.IsUnit && s.Id == tire.id);
            resaleRow.isUnit = false;
            var spareId = MechanicsOwnership.NewId("spare-filter");
            resale.parts.Add(new PartInstance { instanceId = spareId,
                definitionId = catalog.Id(resale.SelectedCar.vehicleId, PartCategory.OilFilter) });
            int resaleCash = resale.cash;
            var saleQuote = Valuations.Capture(resale, catalog, prices, ValuationKind.LoosePart,
                spareId, "recycler", now);
            PlayerProfile sold;
            string saleError;
            if (!MechanicsTransactions.Try(resale, catalog, resale.revision,
                state => PaidEconomy.Sell(state, catalog, prices, saleQuote, now),
                state => true, out sold, out saleError))
                throw new InvalidOperationException("Stale seller discriminator blocked unrelated asset sale: " + saleError);
            if (sold.cash != resaleCash + saleQuote.Total ||
                sold.parts.Any(i => i.instanceId == spareId) ||
                !sold.inventory.dealer.Any(s => s.IsPart && s.Id == spareId))
                throw new InvalidOperationException("Asset sale lost the payment or actual sold part.");

            // The same saved artifact must also be harmless when buying the USED tire.
            var usedProfile = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));
            MechanicsOwnership.Ensure(usedProfile, catalog);
            usedProfile.inventory.dealer.Single(s => s.IsUnit && s.Id == tire.id).part = new PartInstance();
            var usedQuote = PartCart.Quote(usedProfile, usedProfile.SelectedCar.instanceId,
                new[] { new PartCartLine { action = CartAction.BuyUsed, definitionId = tire.definitionId,
                    assetId = tire.id, role = tire.role } }, now, 300);
            int usedCash = usedProfile.cash;
            PlayerProfile usedPaid;
            string usedError;
            if (!MechanicsTransactions.Try(usedProfile, catalog, usedProfile.revision,
                state => PartCart.Checkout(state, usedQuote, now),
                state => true, out usedPaid, out usedError))
                throw new InvalidOperationException("Used tire purchase confused seller payloads: " + usedError);
            if (usedPaid.cash != usedCash - usedQuote.total ||
                !usedPaid.looseEngineElements.Any(u => u.element.id == tire.id && u.element is TireCondition) ||
                usedPaid.inventory.dealer.Any(s => s.Id == tire.id))
                throw new InvalidOperationException("Used tire checkout lost the actual unit or charged incorrectly.");

            // Used-item browse and checkout must also work when only the saved
            // discriminator is stale; the cloned transaction repairs it on commit.
            var staleUsed = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));
            MechanicsOwnership.Ensure(staleUsed, catalog);
            staleUsed.inventory.dealer.Single(s => s.IsUnit && s.Id == tire.id).isUnit = false;
            var staleUsedQuote = PartCart.Quote(staleUsed, staleUsed.SelectedCar.instanceId,
                new[] { new PartCartLine { action = CartAction.BuyUsed,
                    definitionId = tire.definitionId, assetId = tire.id, role = tire.role } }, now, 300);
            PlayerProfile staleUsedPaid;
            string staleUsedError;
            if (!MechanicsTransactions.Try(staleUsed, catalog, staleUsed.revision,
                state => PartCart.Checkout(state, staleUsedQuote, now),
                state => true, out staleUsedPaid, out staleUsedError))
                throw new InvalidOperationException("Stale dealer flag blocked used tire checkout: " + staleUsedError);
            if (!staleUsedPaid.looseEngineElements.Any(u => u.element.id == tire.id && u.element is TireCondition) ||
                staleUsedPaid.inventory.dealer.Any(s => s.Id == tire.id))
                throw new InvalidOperationException("Stale dealer flag prevented actual used-tire ownership transfer.");

            // Reverse case: an empty inactive unit cannot turn a complete used part into an individual unit.
            var fullProfile = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));
            MechanicsOwnership.Ensure(fullProfile, catalog);
            var filterId = catalog.Id(fullProfile.SelectedCar.vehicleId, PartCategory.OilFilter);
            var fullId = MechanicsOwnership.NewId("used-filter");
            fullProfile.inventory.dealer.Add(new DealerStock {
                part = new PartInstance { instanceId = fullId, definitionId = filterId },
                unit = new LooseEngineElement(), paidPrice = 10
            });
            var fullQuote = PartCart.Quote(fullProfile, fullProfile.SelectedCar.instanceId,
                new[] { new PartCartLine { action = CartAction.BuyUsed,
                    definitionId = filterId, assetId = fullId } }, now, 300);
            PlayerProfile fullPaid;
            string fullError;
            if (!MechanicsTransactions.Try(fullProfile, catalog, fullProfile.revision,
                state => PartCart.Checkout(state, fullQuote, now),
                state => true, out fullPaid, out fullError))
                throw new InvalidOperationException("Used full-part purchase confused seller payloads: " + fullError);
            if (!fullPaid.parts.Any(i => i.instanceId == fullId && i.location == PartLocation.Inventory) ||
                fullPaid.inventory.dealer.Any(s => s.Id == fullId))
                throw new InvalidOperationException("Used full-part checkout lost its original identity.");

            // Conflicting genuine payloads must never be automatically deleted to enable commerce.
            var conflict = profile.inventory.dealer.Single(s => s.isUnit && s.unit?.element?.id == tire.id);
            conflict.part = new PartInstance { instanceId = MechanicsOwnership.NewId("used-part") };
            SharedInventory.Ensure(profile);
            string invalid = MechanicsOwnership.Validate(profile, catalog);
            if (invalid == null || !invalid.Contains("conflicting part and unit payloads"))
                throw new InvalidOperationException("Ambiguous dealer stock was silently accepted or discarded.");
            Debug.Log("FLUID PURCHASE PASS: " + checks +
                " paid fluid purchases, typed dealer unit retained, ambiguous stock rejected.");
        }
    }
}
