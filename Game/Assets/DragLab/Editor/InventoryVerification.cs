using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class InventoryVerification
    {
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder(); var catalog = MechanicsTransactions.Catalog();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            var profile = PlayerProfile.New(DateTime.UtcNow.Ticks); profile.cash = 25000; MechanicsOwnership.Ensure(profile, catalog);
            string carId = profile.SelectedCar.instanceId; string oldId = profile.SelectedCar.installed.slots.Find(s => s.slot == "Pistons").partInstanceId;
            profile.parts.Find(p => p.instanceId == oldId).condition.health = .35f;
            PlayerProfile next; string error; string forgedId = "";
            check(MechanicsTransactions.Try(profile, catalog, profile.revision, p => forgedId = PartOperations.Buy(p, catalog, carId, catalog.Id("starter-hatch", PartCategory.Pistons, 1), true), p => true, out next, out error), "Buy/install forged internals directly with no preceding rank."); profile = next;
            check(profile.parts.Find(p => p.instanceId == oldId).location == PartLocation.Inventory && profile.parts.Find(p => p.instanceId == oldId).condition.health == .35f, "Replaced OEM component remains in inventory with its original condition.");
            check(!MechanicsTransactions.Try(profile, catalog, profile.revision, p => PartOperations.Sell(p, catalog, forgedId), p => true, out next, out error), "Installed item sale rejected.");
            check(MechanicsTransactions.Try(profile, catalog, profile.revision, p => PartOperations.Install(p, catalog, carId, oldId), p => true, out next, out error), "Direct downgrade reuses existing OEM identity."); profile = next;
            check(profile.parts.Find(p => p.instanceId == forgedId).location == PartLocation.Inventory && profile.parts.Find(p => p.instanceId == oldId).condition.health == .35f, "Downgrade retains both components and does not heal OEM.");
            int cash = profile.cash;
            check(MechanicsTransactions.Try(profile, catalog, profile.revision, p => PartOperations.Sell(p, catalog, forgedId), p => true, out next, out error) && next.cash > cash, "Loose part sale atomically changes cash and removes identity."); profile = next;
            check(!MechanicsTransactions.Try(profile, catalog, profile.revision, p => PartOperations.Sell(p, catalog, forgedId), p => true, out next, out error), "Repeated sold-item action cannot duplicate cash.");
            check(!MechanicsTransactions.Try(profile, catalog, profile.revision, p => PartOperations.Buy(p, catalog, carId, catalog.Id("sport-coupe", PartCategory.Pistons), true), p => true, out next, out error), "Wrong geometry purchase rejected before deducting cash.");
            check(MechanicsTransactions.Try(profile, catalog, profile.revision, p => PartOperations.Remove(p, carId, PartCategory.Crankshaft), p => true, out next, out error), "Component removal is a valid owned incomplete build."); profile = next;
            check(!MechanicsOwnership.Resolve(profile, profile.SelectedCar, catalog).Drivable, "Incomplete build cannot drive.");
            var crank = profile.parts.Find(p => p.location == PartLocation.Inventory && catalog.Find(p.definitionId).category == PartCategory.Crankshaft); crank.condition.failed = true; crank.condition.failureReason = "injected fracture";
            check(MechanicsTransactions.Try(profile, catalog, profile.revision, p => PartOperations.Install(p, catalog, carId, crank.instanceId), p => true, out next, out error) && !MechanicsOwnership.Resolve(next, next.SelectedCar, catalog).Drivable, "Reinstalled failed component stays failed and blocks driving."); profile = next;
            string failedId = crank.instanceId;
            check(MechanicsTransactions.Try(profile, catalog, profile.revision, p => PartOperations.Buy(p, catalog, carId, catalog.Id("starter-hatch", PartCategory.Crankshaft), true), p => true, out next, out error) && MechanicsOwnership.Resolve(next, next.SelectedCar, catalog).Drivable, "Compatible regular replacement restores operation without renewing retained failed part."); profile = next;
            check(profile.parts.Find(p => p.instanceId == failedId).condition.failed, "Removed failed component retains failure cause.");
            profile.cars.Add(new OwnedCar { vehicleId = "sport-coupe" }); MechanicsOwnership.Ensure(profile, catalog);
            string donorId = profile.cars[1].instanceId; string fuelId = profile.cars[1].installed.slots.Find(s => s.slot == "FuelSystem").partInstanceId;
            profile.parts.Find(p => p.instanceId == fuelId).condition.health = .8f;
            check(MechanicsTransactions.Try(profile, catalog, profile.revision, p => PartOperations.Transfer(p, catalog, donorId, carId, PartCategory.FuelSystem), p => true, out next, out error), "Compatible shared part transfers between different engine models."); profile = next;
            check(!MechanicsOwnership.Resolve(profile, profile.cars[1], catalog).Drivable && profile.parts.Find(p => p.instanceId == fuelId).condition.health == .8f, "Donor remains incomplete and moved part retains condition.");
            check(MechanicsTransactions.Try(profile, catalog, profile.revision, p => PartOperations.SafeDefaults(p, catalog, carId), p => true, out next, out error) && next.parts.Find(p => p.instanceId == failedId).condition.failed, "Conservative tune defaults change adjustments without healing hardware."); profile = next;
            var decoded = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile)); MechanicsOwnership.Ensure(decoded, catalog);
            check(decoded.parts.Count == profile.parts.Count && decoded.parts.Find(p => p.instanceId == oldId).condition.health == .35f && decoded.parts.Find(p => p.instanceId == failedId).condition.failed, "Inventory/worn/failed conditions survive save serialization and reload.");
            string folder = Path.Combine(root, "reports/phases/2d"); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "inventory-verification.txt"), log + "Checks: " + count + "\n");
            return count;
        }
    }
}
