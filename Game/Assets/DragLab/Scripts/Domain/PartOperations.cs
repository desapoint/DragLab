using System;

namespace DragLab.Domain.Mechanics
{
    public static class PartOperations
    {
        static OwnedCar Car(PlayerProfile profile, string id) { var car=profile.cars.Find(c => c.instanceId == id) ?? throw new InvalidOperationException("Vehicle no longer exists.");EngineRebuilds.RequireAvailable(car);return car; }
        static PartInstance Item(PlayerProfile profile, string id) { return profile.parts.Find(p => p.instanceId == id) ?? throw new InvalidOperationException("Part no longer exists."); }
        public static string Buy(PlayerProfile profile, MechanicalCatalog catalog, string vehicleId, string definitionId, bool install)
        {
            var car = Car(profile, vehicleId); var part = catalog.Find(definitionId);
            if(part==null||profile.reputation<part.reputationRequired)throw new InvalidOperationException("Part offer requires REP "+(part==null?0:part.reputationRequired)+"; no preceding rank purchase is required.");
            if (!catalog.Fits(catalog.Spec(car.vehicleId), part)) throw new InvalidOperationException("Incompatible purchase for selected vehicle.");
            int price=InventoryPricing.NewSet(part);if (price < 0 || profile.cash < price) throw new InvalidOperationException("Insufficient cash.");
            var item = new PartInstance { instanceId = MechanicsOwnership.NewId("part"), definitionId = part.id };
            profile.parts.Add(item); profile.cash -= price;
            if (install) MechanicsOwnership.Replace(profile, car, item, catalog);
            return item.instanceId;
        }
        public static void Install(PlayerProfile profile, MechanicalCatalog catalog, string vehicleId, string itemId) { MechanicsOwnership.Replace(profile, Car(profile, vehicleId), Item(profile, itemId), catalog); }
        public static void Remove(PlayerProfile profile, string vehicleId, PartCategory category) { MechanicsOwnership.Remove(profile, Car(profile, vehicleId), category.ToString()); }
        public static int SaleValue(PartDefinition definition, ComponentCondition condition) { return (int)Math.Floor(definition.price * .45 * condition.health * (condition.failed ? .1 : 1)); }
        public static void Sell(PlayerProfile profile, MechanicalCatalog catalog, string itemId)
        {
            var item = Item(profile, itemId);
            if (item.location != PartLocation.Inventory) throw new InvalidOperationException("Remove the installed part before selling it.");
            profile.cash = checked(profile.cash + SaleValue(catalog.Find(item.definitionId), item.condition)); profile.parts.Remove(item);
        }
        public static void Transfer(PlayerProfile profile, MechanicalCatalog catalog, string donorVehicleId, string targetVehicleId, PartCategory category)
        {
            if (donorVehicleId == targetVehicleId) throw new InvalidOperationException("Select another donor.");
            var donor = Car(profile, donorVehicleId); var binding = donor.installed.slots.Find(s => s.slot == category.ToString());
            if (binding == null) throw new InvalidOperationException("Donor slot is empty.");
            var item = Item(profile, binding.partInstanceId);
            if (!catalog.Fits(catalog.Spec(Car(profile, targetVehicleId).vehicleId), catalog.Find(item.definitionId))) throw new InvalidOperationException("Donor part is incompatible.");
            MechanicsOwnership.Remove(profile, donor, category.ToString()); MechanicsOwnership.Replace(profile, Car(profile, targetVehicleId), item, catalog);
        }
        public static void SafeDefaults(PlayerProfile profile, MechanicalCatalog catalog, string vehicleId)
        {
            var car = Car(profile, vehicleId); var spec = catalog.Spec(car.vehicleId);
            car.tune = new Tune { launchRpm = Math.Min(3200, spec.redlineRpm - 600), shiftRpm = spec.redlineRpm - 500, finalDrive = spec.finalDrive, tirePressurePsi = 30, boostSetting = .65f, superchargerPulley = .9f };
            MechanicsOwnership.ProjectRanks(profile, car, catalog);
            var build = MechanicsOwnership.Resolve(profile, car, catalog);
            // Tune delivery to the weakest authored component; never silently replace/heal it.
            while (car.tune.boostSetting > .35f && PeakTorque(build.spec, build.tune) > build.safeTorqueNm)
            { car.tune.boostSetting = Math.Max(.35f, car.tune.boostSetting - .05f); build = MechanicsOwnership.Resolve(profile, car, catalog); }
        }
        static double PeakTorque(VehicleSpec spec, Tune tune) { double peak = 0; for (double rpm = spec.idleRpm; rpm <= spec.redlineRpm; rpm += 100) peak = Math.Max(peak, DragMath.Torque(spec, tune, rpm)); return peak; }
    }
}
