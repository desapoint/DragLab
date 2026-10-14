using System;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    [Serializable] public sealed class VehicleOffer
    {
        public string model, paint, listingId;
        public bool used;public double mileageKm;
        public int disclosureVersion,estimateSeed;public bool inspected,firstStarter,engineRuns,drives;public double reportedCondition;
        public System.Collections.Generic.List<string> historyNotes=new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<PartInstance> parts;public FuelState fuel;public FluidState fluids;
        public int price, reputation, cataloguePrice;
        public Tune stock;
    }
    public static class VehicleCollection
    {
        public const int NameLimit = 48;
        public static string NormalizeName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > NameLimit || name.Any(char.IsControl)) throw new InvalidOperationException("Use up to 48 characters on one line.");
            return name;
        }
        public static void Rename(PlayerProfile profile, string instanceId, string name)
        {
            var car = profile.cars.Find(c => c.instanceId == instanceId) ?? throw new InvalidOperationException("Owned car no longer exists.");
            if (profile.runCheckpoints.Any(r => !r.settled)) throw new InvalidOperationException("Close the active run before renaming a car.");
            car.customName = NormalizeName(name);
        }
        public static string DisplayName(MechanicalCatalog catalog, OwnedCar car)
        { return string.IsNullOrWhiteSpace(car.customName) ? catalog.Spec(car.vehicleId).displayName : car.customName; }
        public static void Select(PlayerProfile profile, string instanceId)
        {
            var car = profile.cars.Find(c => c.instanceId == instanceId) ?? throw new InvalidOperationException("Owned car no longer exists.");
            if (profile.runCheckpoints.Any(r => !r.settled)) throw new InvalidOperationException("Close the active run before selecting another car.");
            profile.selectedVehicleId = car.vehicleId; profile.selectedOwnedVehicleId = car.instanceId;
        }
        public static string Buy(PlayerProfile profile, MechanicalCatalog catalog, VehicleOffer offer)
        { return Buy(profile,catalog,offer,offer?.price??0); }
        internal static string Buy(PlayerProfile profile, MechanicalCatalog catalog, VehicleOffer offer,int chargedPrice)
        {
            if (offer == null || offer.stock == null || offer.price <= 0 || chargedPrice<offer.price || offer.reputation < 0) throw new InvalidOperationException("Invalid dealership offer.");
            if (profile.cash < chargedPrice) throw new InvalidOperationException("Insufficient cash for this car.");
            if (profile.reputation < offer.reputation) throw new InvalidOperationException("Earn the required reputation first.");
            if (profile.runCheckpoints.Any(r => !r.settled)) throw new InvalidOperationException("Close the active run before buying a car.");
            VehicleListings.RequireCurrent(profile,offer);
            var spec = catalog.Spec(offer.model);
            if(offer.parts!=null&&offer.parts.Count>0){var bought=InstallExact(profile,catalog,offer);profile.cash-=chargedPrice;VehicleListings.Consume(profile,catalog,offer);MechanicsOwnership.Ensure(profile,catalog);Select(profile,bought.instanceId);return bought.instanceId;}
            var car = new OwnedCar { vehicleId = offer.model, tune = offer.stock.Copy(), paintHex = offer.paint ?? "#58C7BA" };
            car.tune.ClampTo(spec); profile.cash -= chargedPrice; profile.cars.Add(car);
            MechanicsOwnership.Ensure(profile, catalog); Select(profile, car.instanceId);
            return car.instanceId;
        }
        public static PlayerProfile Preview(MechanicalCatalog catalog,VehicleOffer offer){var p=new PlayerProfile{saveVersion=MechanicsOwnership.SaveVersion,catalog=catalog,cash=int.MaxValue,reputation=int.MaxValue,inventory=new InventoryState{market=new PartsStockState{version=1,seed=1}},vehicleStock=new VehicleStockState{version=1,seed=1},drivelineVersion=2,equipmentVersion=1,ignitionHardwareVersion=1};var car=InstallExact(p,catalog,offer);MechanicsOwnership.Ensure(p,catalog);Select(p,car.instanceId);return p;}
        static OwnedCar InstallExact(PlayerProfile p,MechanicalCatalog c,VehicleOffer offer){
            var car=new OwnedCar{instanceId=MechanicsOwnership.NewId("vehicle"),chassisId=MechanicsOwnership.NewId("chassis"),engineAssemblyId=MechanicsOwnership.NewId("engine"),vehicleId=offer.model,tune=offer.stock.Copy(),paintHex=offer.paint,mileageKm=offer.mileageKm,usedHistory=new System.Collections.Generic.List<string>(offer.historyNotes??new System.Collections.Generic.List<string>()),fuel=offer.fuel.Copy(),fluids=offer.fluids.Copy(),fuelInitialized=true,massVersion=1,serviceHardwareVersion=1};
            car.installed=new InstalledBuild{instanceId=car.instanceId,vehicleDefinitionId=car.vehicleId};p.engines.Add(new EngineAssemblyInstance{instanceId=car.engineAssemblyId,parentVehicleId=car.instanceId,vehicleDefinitionId=car.vehicleId});
            foreach(var source in offer.parts){var part=source.Copy();var d=c.Find(part.definitionId);part.location=MechanicalCatalog.EngineSlot(d.category)?PartLocation.Assembly:PartLocation.Vehicle;part.ownerId=MechanicalCatalog.EngineSlot(d.category)?car.engineAssemblyId:car.instanceId;part.slot=d.category.ToString();part.storageId="";p.parts.Add(part);car.installed.slots.Add(new SlotBinding{slot=part.slot,partInstanceId=part.instanceId});}
            p.cars.Add(car);return car;
        }
        public static string Label(MechanicalCatalog catalog, OwnedCar car)
        { return DisplayName(catalog,car) + " / " + car.instanceId.Substring(Math.Max(0, car.instanceId.Length - 6)); }
    }
}
