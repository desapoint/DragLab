using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator MaintenanceClick(WindowsKeyboardProbe input,string id)
        {
            if(!shell.ModalOpen) { shell.Root.Q<ScrollView>("ownedDetailScroll").ScrollTo(shell.Root.Q(id)); yield return new WaitForSecondsRealtime(.15f); }
            yield return Click(input,id);
        }
        IEnumerator MaintenanceChecks(WindowsKeyboardProbe input,string id,string brokenId,string reservedId)
        {
            var p=game.Profile;var car=p.cars.Find(c=>c.instanceId==id);var catalog=p.catalog;
            var wet=catalog.parts.First(d=>d.category==PartCategory.Clutch && d.clutchEnvironment==ClutchEnvironment.Wet && catalog.Fits(catalog.Spec(car.vehicleId),d));
            string clutchId=PartOperations.Buy(p,catalog,id,wet.id,true);
            var clutch=p.parts.Find(i=>i.instanceId==clutchId);clutch.condition.health=.7f;clutch.condition.clutchOilLitres=.1;clutch.condition.clutchOilCondition=.3;
            PartInstance Installed(PartCategory category)=>p.parts.Find(i=>i.instanceId==car.installed.slots.First(s=>s.slot==category.ToString()).partInstanceId);
            var radiator=Installed(PartCategory.Cooling);radiator.condition.health=.7f;radiator.condition.leakSeverity=.2;
            var fuelPart=Installed(PartCategory.FuelSystem);fuelPart.condition.deposits=.2;
            var timing=Installed(PartCategory.TimingDrive);timing.condition.adjustmentError=.2;
            var rings=Installed(PartCategory.PistonRings);rings.condition.health=.45f;foreach(var e in rings.condition.elements)e.health=.45f;
            car.fuel.litres=2;car.fluids.oilLitres=1;car.fluids.coolantLitres=1;car.fluids.oilCondition=.4;
            string selected=p.selectedOwnedVehicleId,baseJson=JsonUtility.ToJson(p),disk=File.ReadAllText(ProfileStore.FilePath);
            shell.InvalidateOwnedGrid();shell.Refresh();yield return new WaitForSecondsRealtime(.2f);yield return Click(input,"ownedCar:"+id);
            Check(shell.Root.Q("carMaintenance")!=null && shell.Root.Query(className:"maintenance-fluid").ToList().Count==4,"Details show fuel, engine oil, coolant and installed wet-clutch oil");
            Check(shell.Root.Q("maintenanceRepair:Reseal:"+radiator.instanceId)!=null && shell.Root.Q("maintenanceRepair:Clean:"+fuelPart.instanceId)!=null && shell.Root.Q("maintenanceRepair:Adjust:"+timing.instanceId)!=null && shell.Root.Q("maintenanceRepair:Recondition:"+rings.instanceId)!=null,"Quick maintenance exposes only applicable cleaning, adjustment, resealing and reconditioning actions");
            yield return MaintenanceClick(input,"maintenanceFuel");var quantity=shell.Root.Q<FloatField>("maintenanceQuantity");
            quantity.value=3;Check(shell.Root.Q<Slider>("maintenanceQuantitySlider").value==3,"Refill quantity synchronizes the shared slider/numeric input");
            yield return Capture("car-maintenance-refill");yield return Key(input,0x77);Check(shell.ModalOpen&&shell.Visible,"Maintenance confirmation blocks F8");
            yield return Click(input,"dialogCancel");Check(JsonUtility.ToJson(game.Profile)==baseJson&&File.ReadAllText(ProfileStore.FilePath)==disk,"Maintenance draft/cancel does not spend, refill, repair or save");
            yield return MaintenanceClick(input,"maintenanceFuel");quantity=shell.Root.Q<FloatField>("maintenanceQuantity");quantity.value=1000;
            Check(!shell.Root.Q("dialogConfirm").enabledInHierarchy,"Refill input cannot exceed free tank space");quantity.value=0;
            Check(!shell.Root.Q("dialogConfirm").enabledInHierarchy,"Zero-quantity refill cannot create a purchase/save");quantity.value=3;
            int cost=PaidEconomy.Preview(game.Profile,catalog,game.Costs,game.PricingPolicy,new PaymentRequest {kind=PaymentKind.Fuel,carId=id,litres=3},DateTime.UtcNow.Ticks).cost;
            int cash=game.Profile.cash;yield return Click(input,"dialogConfirm");
            Check(!shell.ModalOpen&&game.Profile.cars.Find(c=>c.instanceId==id).fuel.litres==5&&game.Profile.cash==cash-cost&&game.Profile.selectedOwnedVehicleId==selected,"Confirmed refuel charges the quoted amount on the inspected non-current car without selecting it");
            Check(game.Profile.economyReceipts.Last().target==id&&game.Profile.economyReceipts.Last().fuelAdded==3,"Refill uses the existing exact-car operating receipt");
            yield return MaintenanceClick(input,"maintenanceOil");shell.Root.Q<FloatField>("maintenanceQuantity").value=.5f;yield return Click(input,"dialogConfirm");
            car=game.Profile.cars.Find(c=>c.instanceId==id);
            Check(Math.Abs(car.fluids.oilLitres-1.5)<.000001&&car.fluids.oilCondition==.4&&game.Profile.parts.Find(i=>i.instanceId==radiator.instanceId).condition.leakSeverity==.2,"Oil top-up preserves aging and leaks");
            string oldFilter=car.installed.slots.First(s=>s.slot==PartCategory.OilFilter.ToString()).partInstanceId;
            var tune=JsonUtility.ToJson(car.tune);yield return MaintenanceClick(input,"maintenanceOilChange");yield return Click(input,"dialogConfirm");car=game.Profile.cars.Find(c=>c.instanceId==id);
            var oilSpec=MechanicsOwnership.Resolve(game.Profile,car,catalog).spec.fluids;
            Check(car.fluids.oilCondition==1&&Math.Abs(car.fluids.oilLitres-oilSpec.oilCapacityLitres)<.000001&&game.Profile.parts.Find(i=>i.instanceId==oldFilter).location==PartLocation.Inventory&&car.installed.slots.First(s=>s.slot==PartCategory.OilFilter.ToString()).partInstanceId!=oldFilter,"Paid oil change renews oil/filter and retains the removed filter in inventory");
            Check(JsonUtility.ToJson(car.tune)==tune&&game.Profile.parts.Find(i=>i.instanceId==radiator.instanceId).condition.leakSeverity==.2,"Oil change preserves applied tune and unrelated damage");
            yield return MaintenanceClick(input,"maintenanceCoolant");shell.Root.Q<FloatField>("maintenanceQuantity").value=1;yield return Click(input,"dialogConfirm");
            Check(game.Profile.cars.Find(c=>c.instanceId==id).fluids.coolantLitres==2,"Coolant refill uses the inspected engine's capacity and requested quantity");
            yield return MaintenanceClick(input,"maintenanceFlush");yield return Click(input,"dialogConfirm");
            Check(Math.Abs(game.Profile.cars.Find(c=>c.instanceId==id).fluids.coolantLitres-oilSpec.coolantCapacityLitres)<.000001&&game.Profile.parts.Find(i=>i.instanceId==radiator.instanceId).condition.leakSeverity==.2,"Coolant flush fills installed capacity without repairing a leak");
            yield return MaintenanceClick(input,"maintenanceClutchOil");yield return Click(input,"dialogConfirm");
            clutch=game.Profile.parts.Find(i=>i.instanceId==clutchId);
            Check(clutch.condition.clutchOilCondition==1&&Math.Abs(clutch.condition.clutchOilLitres-wet.clutchOilCapacityLitres)<.000001&&Math.Abs(clutch.condition.health-.7)<.000001,"Wet-clutch oil service restores its bath without repairing clutch wear");
            car=game.Profile.cars.Find(c=>c.instanceId==id);car.fluids.oilLitres=oilSpec.oilCapacityLitres+1;shell.InvalidateOwnedGrid();shell.Refresh();yield return new WaitForSecondsRealtime(.2f);
            cash=game.Profile.cash;yield return MaintenanceClick(input,"maintenanceDrainOil");yield return Click(input,"dialogConfirm");
            Check(Math.Abs(game.Profile.cars.Find(c=>c.instanceId==id).fluids.oilLitres-oilSpec.oilCapacityLitres)<.000001&&game.Profile.cash==cash,"Excess-oil drain uses the existing free service without renewing oil or repairing faults");
            yield return MaintenanceClick(input,"maintenanceRepair:Reseal:"+radiator.instanceId);disk=File.ReadAllText(ProfileStore.FilePath);baseJson=JsonUtility.ToJson(game.Profile);
            using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                yield return Click(input,"dialogConfirm");Check(shell.ModalOpen&&shell.Root.Q<Label>("maintenanceError").text.Length>0&&JsonUtility.ToJson(game.Profile)==baseJson&&File.ReadAllText(ProfileStore.FilePath)==disk,"Failed maintenance save retains quote/dialog and rolls back cash and repair");
            }
            yield return Click(input,"dialogConfirm");
            Check(!shell.ModalOpen&&game.Profile.parts.Find(i=>i.instanceId==radiator.instanceId).condition.leakSeverity==0&&Math.Abs(game.Profile.parts.Find(i=>i.instanceId==radiator.instanceId).condition.health-.7)<.000001,"Retry reseals the radiator while retaining its structural wear");
            yield return MaintenanceClick(input,"maintenanceRepair:Clean:"+fuelPart.instanceId);yield return Click(input,"dialogConfirm");
            Check(game.Profile.parts.Find(i=>i.instanceId==fuelPart.instanceId).condition.deposits==0,"Paid cleaning removes deposits");
            yield return MaintenanceClick(input,"maintenanceRepair:Adjust:"+timing.instanceId);yield return Click(input,"dialogConfirm");
            Check(game.Profile.parts.Find(i=>i.instanceId==timing.instanceId).condition.adjustmentError==0,"Paid adjustment removes timing/clearance error");
            yield return MaintenanceClick(input,"maintenanceRepair:Recondition:"+rings.instanceId);yield return Click(input,"dialogConfirm");
            Check(game.Profile.parts.Find(i=>i.instanceId==rings.instanceId).condition.health>.45f&&game.Profile.parts.Find(i=>i.instanceId==rings.instanceId).condition.reconditionCount==1,"Paid reconditioning uses the existing partial recovery/service limits");
            car=game.Profile.cars.Find(c=>c.instanceId==id);string pistonId=car.installed.slots.First(s=>s.slot==PartCategory.Pistons.ToString()).partInstanceId;
            string definition=game.Profile.parts.Find(i=>i.instanceId==pistonId).definitionId;var oldCondition=JsonUtility.ToJson(game.Profile.parts.Find(i=>i.instanceId==pistonId).condition);
            yield return MaintenanceClick(input,"maintenanceReplace:"+pistonId);yield return Capture("car-maintenance-replace-set");yield return Click(input,"dialogConfirm");
            car=game.Profile.cars.Find(c=>c.instanceId==id);string newId=car.installed.slots.First(s=>s.slot==PartCategory.Pistons.ToString()).partInstanceId;
            var newPistons=game.Profile.parts.Find(i=>i.instanceId==newId);
            Check(newId!=pistonId&&newPistons.definitionId==definition&&newPistons.condition.elements.Length==catalog.Spec(car.vehicleId).sound.cylinders&&newPistons.condition.health==1,"Buy same replaces the exact piston specification as a complete matched cylinder set");
            Check(game.Profile.parts.Find(i=>i.instanceId==pistonId).location==PartLocation.Inventory&&JsonUtility.ToJson(game.Profile.parts.Find(i=>i.instanceId==pistonId).condition)==oldCondition&&JsonUtility.ToJson(car.tune)==tune&&game.Profile.selectedOwnedVehicleId==selected,"Replacement retains old set identity/condition, applied tune and current-car selection");
            yield return Capture("car-maintenance-after");
            yield return Click(input,"ownedBack");yield return Click(input,"ownedCar:"+brokenId);
            var broken=game.Profile.cars.Find(c=>c.instanceId==brokenId);string brakeId=broken.installed.slots.First(s=>s.slot==PartCategory.Brakes.ToString()).partInstanceId;
            Check(!shell.Root.Query<Button>().ToList().Any(b=>b.name.StartsWith("maintenanceRepair:")&&b.name.EndsWith(brakeId)),"Fully failed brakes cannot be repaired by an inapplicable generic service");
            yield return MaintenanceClick(input,"maintenanceReplace:"+brakeId);yield return Click(input,"dialogConfirm");
            Check(game.Profile.parts.Find(i=>i.instanceId==brakeId).location==PartLocation.Inventory&&game.Profile.parts.Find(i=>i.instanceId==brakeId).condition.failed&&shell.Root.Q<Label>("maintenanceCondition").text.Contains("0 failed"),"Buy same replaces failed brakes while retaining their fault in inventory and refreshing condition");
            yield return Click(input,"ownedBack");yield return Click(input,"ownedCar:"+reservedId);
            Check(!shell.Root.Q("maintenanceFuel").enabledInHierarchy,"Reserved garage-job cars remain inspectable but cannot be serviced");yield return Click(input,"ownedBack");
            yield return Click(input,"ownedCar:"+selected);
            Check(shell.Root.Q("maintenanceOil")==null&&shell.Root.Q("maintenanceOilChange")==null&&shell.Root.Q<NativeDynoPlot>("ownedDynoPlot")==null,"Detached engine does not expose oil/coolant actions or fabricate a dyno curve on its chassis");yield return Click(input,"ownedBack");
            var request=new PaymentRequest {kind=PaymentKind.Fuel,carId=id,litres=1};disk=File.ReadAllText(ProfileStore.FilePath);
            Check(!game.CommitNativeMaintenance(request,game.Profile.revision-1,JsonUtility.ToJson(game.Profile),2)&&File.ReadAllText(ProfileStore.FilePath)==disk,"Stale maintenance confirmation cannot charge or change the saved car");
            yield return Click(input,"ownedCar:"+id);yield return MaintenanceClick(input,"maintenanceRebuild");yield return Click(input,"dialogConfirm");
            var rebuilding=game.Profile.cars.Find(c=>c.instanceId==id);
            Check(rebuilding.engineRebuild!=null&&rebuilding.engineRebuild.active&&!shell.Root.Q("maintenanceFuel").enabledInHierarchy&&!shell.Root.Q("maintenanceRebuildCollect").enabledInHierarchy,"Paid shop rebuild persists a timed reservation and disables service/early collection");
            disk=File.ReadAllText(ProfileStore.FilePath);baseJson=JsonUtility.ToJson(game.Profile);
            Check(!game.CommitNativeService(id,game.Profile.revision,baseJson,rebuilding.engineRebuild.id)&&JsonUtility.ToJson(game.Profile)==baseJson&&File.ReadAllText(ProfileStore.FilePath)==disk,"Premature rebuild collection rejects without changes or another save");
            yield return MaintenanceClick(input,"maintenanceRebuildCollect");
            rebuilding.engineRebuild.startedUtcTicks=DateTime.UtcNow.AddMinutes(-30).Ticks;rebuilding.engineRebuild.readyUtcTicks=DateTime.UtcNow.AddSeconds(-1).Ticks;
            shell.InvalidateOwnedGrid();shell.Refresh();yield return new WaitForSecondsRealtime(.2f);yield return MaintenanceClick(input,"maintenanceRebuildCollect");yield return Capture("car-maintenance-rebuild-ready");yield return Click(input,"dialogConfirm");
            Check(!shell.ModalOpen&&game.Profile.cars.Find(c=>c.instanceId==id).engineRebuild==null&&game.Profile.selectedOwnedVehicleId==selected,"Collect applies the completed existing rebuild once without changing selected car");
            cash=game.Profile.cash;game.Profile.cash=0;shell.InvalidateOwnedGrid();shell.Refresh();yield return new WaitForSecondsRealtime(.2f);
            yield return MaintenanceClick(input,"maintenanceOilChange");
            Check(!shell.Root.Q("dialogConfirm").enabledInHierarchy&&shell.Root.Q<Label>("maintenanceError").text.StartsWith("Need $"),"Unaffordable maintenance shows the shortfall and disables publication");yield return Click(input,"dialogCancel");game.Profile.cash=cash;shell.InvalidateOwnedGrid();shell.Refresh();
            yield return Click(input,"ownedBack");
            Screen.SetResolution(1024,768,false);yield return new WaitForSecondsRealtime(.4f);yield return Click(input,"ownedCar:"+id);yield return MaintenanceClick(input,"maintenanceOilChange");
            Check(shell.Root.Q("dialogConfirm").worldBound.xMax<=shell.Root.worldBound.xMax&&shell.Root.Q("modalClose").worldBound.yMin>=0,"Maintenance confirmation remains reachable at compact resolution");yield return Capture("car-maintenance-1024");yield return Click(input,"dialogCancel");yield return Click(input,"ownedBack");
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
        }
#endif
    }
}
