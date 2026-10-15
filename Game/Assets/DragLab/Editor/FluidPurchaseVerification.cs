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
                string invalid=MechanicsOwnership.Validate(loaded,catalog);
                if(invalid!=null)throw new InvalidOperationException("Generated dealer stock seed "+seed
                    +" failed to reload: "+invalid);
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
