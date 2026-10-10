using System;
using System.Linq;
using UnityEngine;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        internal bool SelectNativeCar(string id,long revision)
        {
            if (screen != "Garage" || pendingAction != null) { notice = "Garage is no longer available."; return false; }
            if (profile.revision != revision) { notice = "Car details changed. Go back and reopen this car."; return false; }
            if (!profile.cars.Exists(car => car.instanceId == id)) { notice = "Owned car no longer exists."; return false; }
            if (profile.selectedOwnedVehicleId == id) return true;
            if (!CommitParts(revision,p => VehicleCollection.Select(p,id))) return false;
            ShowGarageCar(); notice = "Current car saved.";
            return true;
        }
        internal void OpenCollection() { DiscardPreviews(); screen = "Collection"; scroll = Vector2.zero; partRects.Clear(); }
        void CollectionGUI()
        {
            GUILayout.Label("OWNED CARS / independent parts, fuel, tunes and records", stat);
            if (PartButton("collection-recovery", "Recovery work / idle claim / operating receipts")) pendingAction = OpenRecovery;
            foreach (var car in profile.cars.ToArray())
            {
                string id = car.instanceId;
                GUILayout.Label(VehicleCollection.Label(profile.catalog, car) + (id == profile.selectedOwnedVehicleId ? " / SELECTED" : ""));
                var resolved = MechanicsOwnership.Resolve(profile, car, profile.catalog);
                GUILayout.Label(car.fuel.litres.ToString("F1") + " L fuel / " + car.completedRuns + " passes / " + (resolved.Drivable ? "Ready" : string.Join("; ", resolved.errors)));
                if (PartButton("car-select:" + id, "Select this owned car")) QueuePartAction(p => VehicleCollection.Select(p, id));
                if (PartButton("car-quote:" + id, "Value this exact car / buyer comparison")) pendingAction = () => OpenValuation(ValuationKind.Car, id);
            }
            GUILayout.Space(10); GUILayout.Label("DEALERSHIP / buy another copy / provisional prices", stat);
            foreach (var vehicle in vehicles)
            {
                var offer = new VehicleOffer { model = vehicle.spec.id, stock = vehicle.CreateStockTune(), price = vehicle.purchasePrice, reputation = vehicle.reputationRequired, paint = "#" + ColorUtility.ToHtmlStringRGB(vehicle.defaultPaint) };
                int price = PaidEconomy.DealerPrice(profile.catalog, Costs, PricingPolicy, offer);
                GUILayout.Label(vehicle.spec.displayName + " / " + vehicle.spec.sound.cylinders + " cylinders / " + vehicle.spec.DisplacementLitres.ToString("F1") + " L / " + vehicle.spec.drive + " / owned " + profile.cars.Count(c => c.vehicleId == offer.model));
                GUILayout.Label("OEM package includes initial fuel/oil/coolant. Component-value floor prevents an immediate stock part-out profit.");
                GUI.enabled = profile.cash >= price && profile.reputation >= offer.reputation;
                if (PartButton("car-buy:" + offer.model, "Buy another / $" + price + " / REP " + offer.reputation)) QueuePayment(new PaymentRequest { kind = PaymentKind.Car, offer = offer });
                GUI.enabled = true;
            }
            if (PartButton("collection-garage", "Garage / selected owned car")) pendingAction = ReturnToGarage;
        }
    }
}
