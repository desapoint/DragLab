using System;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    [Serializable] public sealed class VehicleOffer
    {
        public string model, paint;
        public int price, reputation;
        public Tune stock;
    }
    public static class VehicleCollection
    {
        public static void Select(PlayerProfile profile, string instanceId)
        {
            var car = profile.cars.Find(c => c.instanceId == instanceId) ?? throw new InvalidOperationException("Owned car no longer exists.");
            if (profile.runCheckpoints.Any(r => !r.settled)) throw new InvalidOperationException("Close the active run before selecting another car.");
            profile.selectedVehicleId = car.vehicleId; profile.selectedOwnedVehicleId = car.instanceId;
        }
        public static string Buy(PlayerProfile profile, MechanicalCatalog catalog, VehicleOffer offer)
        {
            if (offer == null || offer.stock == null || offer.price <= 0 || offer.reputation < 0) throw new InvalidOperationException("Invalid dealership offer.");
            if (profile.cash < offer.price) throw new InvalidOperationException("Insufficient cash for this car.");
            if (profile.reputation < offer.reputation) throw new InvalidOperationException("Earn the required reputation first.");
            if (profile.runCheckpoints.Any(r => !r.settled)) throw new InvalidOperationException("Close the active run before buying a car.");
            var spec = catalog.Spec(offer.model);
            var car = new OwnedCar { vehicleId = offer.model, tune = offer.stock.Copy(), paintHex = offer.paint ?? "#58C7BA" };
            car.tune.ClampTo(spec); profile.cash -= offer.price; profile.cars.Add(car);
            MechanicsOwnership.Ensure(profile, catalog); Select(profile, car.instanceId);
            return car.instanceId;
        }
        public static string Label(MechanicalCatalog catalog, OwnedCar car)
        { return catalog.Spec(car.vehicleId).displayName + " / " + car.instanceId.Substring(Math.Max(0, car.instanceId.Length - 6)); }
    }
}
