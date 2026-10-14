using System;
using DragLab.Domain;
using System.Linq;
using UnityEngine;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        internal bool RenameNativeCar(string id, long revision, string name)
        {
            if (screen != "Garage" || pendingAction != null) { notice = "Garage is no longer available."; return false; }
            if (profile.revision != revision) { notice = "Car details changed. Close and reopen Rename."; return false; }
            var car = profile.cars.Find(item => item.instanceId == id);
            if (car == null) { notice = "Owned car no longer exists."; return false; }
            string normalized;
            try { normalized = VehicleCollection.NormalizeName(name); }
            catch (InvalidOperationException error) { notice = error.Message; return false; }
            if ((car.customName ?? "") == normalized) return true;
            if (!CommitParts(revision,p => VehicleCollection.Rename(p,id,normalized))) return false;
            notice = "Car name saved."; return true;
        }
        internal bool SelectNativeCar(string id,long revision)
        {
            if ((screen != "Garage" && screen != "Race setup") || pendingAction != null) { notice = "Garage is no longer available."; return false; }
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
            foreach (var offer in profile.vehicleStock.listings.ToArray())
            {
                var vehicle=vehicles.First(v=>v.spec.id==offer.model);
                int price = PaidEconomy.DealerPrice(profile.catalog, Costs, PricingPolicy, offer);
                GUILayout.Label(vehicle.spec.displayName + " / " + vehicle.spec.sound.cylinders + " cylinders / " + vehicle.spec.DisplacementLitres.ToString("F1") + " L / " + vehicle.spec.drive + " / owned " + profile.cars.Count(c => c.vehicleId == offer.model));
                GUILayout.Label((offer.used?"Used · "+offer.mileageKm.ToString("N0")+" km · fitted parts and wear retained":"New · factory fitted package"));
                if(offer.used){GUILayout.Label((offer.reportedCondition*100).ToString("F0")+"% condition / "+(offer.engineRuns?"Engine runs":"Engine does not run")+" / "+(offer.drives?"Drives":"Does not drive"));
                    if(!VehicleInspection.Known(offer))PaidButton("car-inspect:"+offer.listingId,"Inspect this listing",new PaymentRequest{kind=PaymentKind.VehicleInspection,offer=offer});
                    else {GUILayout.Label("Inspected / "+string.Join("; ",offer.historyNotes));GUILayout.Label("Fuel "+offer.fuel.litres.ToString("F1")+" L / oil "+offer.fluids.oilLitres.ToString("F1")+" L / coolant "+offer.fluids.coolantLitres.ToString("F1")+" L");foreach(var part in offer.parts.Where(p=>p.condition.failed||p.condition.leakSeverity>0||p.condition.elements.Any(e=>e.fault!=ElementFault.None)))GUILayout.Label(profile.catalog.Find(part.definitionId).name+" / "+(part.condition.failed?part.condition.failureReason:"Service faults"));}
                }
                GUI.enabled = profile.cash >= price && profile.reputation >= offer.reputation;
                if (PartButton("car-buy:" + offer.listingId, "Buy another / " + DisplayUnits.Money(price) + " / REP " + offer.reputation)) QueuePayment(new PaymentRequest { kind = PaymentKind.Car, offer = offer });
                GUI.enabled = true;
            }
            if (PartButton("collection-garage", "Garage / selected owned car")) pendingAction = ReturnToGarage;
        }
    }
}
