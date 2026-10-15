using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Editor
{
    public static class PaidOperationsVerification
    {
        [MenuItem("DragLab/Prepare Operating Policy")]
        public static void Prepare()
        {
            ValuationVerification.PreparePolicy(); const string path = "Assets/DragLab/Resources/Data/Economy/OperatingPolicy.asset";
            if (AssetDatabase.LoadAssetAtPath<OperatingPolicyDefinition>(path) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)); AssetDatabase.Refresh(); AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<OperatingPolicyDefinition>(), path); AssetDatabase.SaveAssets();
        }
        [MenuItem("DragLab/Verify Paid Operations and Salvage")]
        public static void RunStandalone()
        {
            if(Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy")==null||Resources.Load<ValuationPolicyDefinition>("Data/Economy/ValuationPolicy")==null)
                throw new InvalidOperationException("Operating/valuation policy assets missing. Use the corresponding explicit DragLab/Prepare menu actions before verification.");
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, "reports/phases/3d"); Directory.CreateDirectory(folder);
            var log = new StringBuilder(); int checks = 0; long now = DateTime.UtcNow.Ticks;
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); checks++; log.AppendLine("PASS: " + label); File.WriteAllText(Path.Combine(folder, "verification-progress.txt"), log.ToString()); };
            var cat = MechanicsTransactions.Catalog(); var costs = Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy").policy; var prices = Resources.Load<ValuationPolicyDefinition>("Data/Economy/ValuationPolicy").policy;
            Func<string, PlayerProfile> fresh = model => { var p = PlayerProfile.New(now); p.cars[0].vehicleId = model; p.selectedVehicleId = model; p.cash = 200000; p.reputation = 99; MechanicsOwnership.Ensure(p, cat); return p; };
            Func<PlayerProfile, Action<PlayerProfile>, PlayerProfile> commit = (p, op) => { PlayerProfile next; string error; if (!MechanicsTransactions.Try(p, cat, p.revision, op, q => true, out next, out error)) throw new InvalidOperationException(error); return next; };
            Action<PlayerProfile, Action<PlayerProfile>, string> rejects = (p, op, label) => { string json = JsonUtility.ToJson(p); PlayerProfile next; string error; bool ok = MechanicsTransactions.Try(p, cat, p.revision, op, q => true, out next, out error); check(!ok && json == JsonUtility.ToJson(p), label); };
            Func<PlayerProfile, PaymentRequest, PaymentQuote> quote = (p, r) => PaidEconomy.Quote(p, cat, costs, prices, r, now);
            foreach (var asset in Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v => v.spec.id)) {
                string model = asset.spec.id; var p = fresh(model); var car = p.SelectedCar; string carId = car.instanceId, engineId = car.engineAssemblyId, chassis = car.chassisId; var fuel = cat.Spec(model).fuel;
                car.fuel.litres = fuel.capacityLitres - 2.5; car.fuel.cumulativeUsedLitres = 7;
                var q = quote(p, new PaymentRequest { kind = PaymentKind.Fuel, carId = carId, litres = 1000 });
                check(q.Fuel == 2.5 && q.Cost == (int)Math.Ceiling(2.5 * costs.fuelPerLitre), "Refuel capped and priced on actual free space: " + model); int cash = p.cash;
                p = commit(p, a => PaidEconomy.Pay(a, cat, costs, prices, q, now)); check(p.cash == cash - q.Cost && p.SelectedCar.fuel.litres == fuel.capacityLitres && p.SelectedCar.fuel.cumulativeUsedLitres == 7 && p.economyReceipts.Count == 1, "Paid fuel retains history and settles once: " + model);
                rejects(p, a => PaidEconomy.Pay(a, cat, costs, prices, q, now), "Duplicate fuel quote rejected atomically: " + model);
                var noSpace = quote(p, new PaymentRequest { kind = PaymentKind.Fuel, carId = carId, litres = 2 }); rejects(p, a => PaidEconomy.Pay(a, cat, costs, prices, noSpace, now), "Full tank purchases reject without cash change: " + model);
                p.SelectedCar.fuel.litres = 0; var affordable = quote(p, new PaymentRequest { kind = PaymentKind.Fuel, carId = carId, litres = .5 }); p.cash = 0;
                rejects(p, a => PaidEconomy.Pay(a, cat, costs, prices, quote(a, new PaymentRequest { kind = PaymentKind.Fuel, carId = carId, litres = .5 }), now), "Low-cash fuel keeps money/quantity unchanged: " + model); p.cash = 200000;
                var fluid = p.SelectedCar.fluids; fluid.oilLitres = 1; fluid.coolantLitres = 1; fluid.oilCondition = .4;
                q = quote(p, new PaymentRequest { kind = PaymentKind.Fluids, carId = carId, litres = 100, coolantLitres = 100 }); cash = p.cash; p = commit(p, a => PaidEconomy.Pay(a, cat, costs, prices, q, now));
                check(p.cash == cash - q.Cost && p.SelectedCar.fluids.oilCondition == .4 && p.SelectedCar.fluids.oilLitres > 1, "Paid fluids retain aging / existing faults: " + model);
                var radiator = p.parts.Find(i => i.ownerId == engineId && cat.Find(i.definitionId).category == PartCategory.Cooling); radiator.condition.leakSeverity = .5; radiator.condition.health = .7f;
                q = quote(p, new PaymentRequest { kind = PaymentKind.Repair, carId = carId, targetId = radiator.instanceId, service = ServiceAction.Reseal }); cash = p.cash; p = commit(p, a => PaidEconomy.Pay(a, cat, costs, prices, q, now));
                check(p.cash == cash - q.Cost && p.parts.Find(i => i.instanceId == radiator.instanceId).condition.leakSeverity == 0 && p.parts.Find(i => i.instanceId == radiator.instanceId).condition.health == .7f, "Paid repair restores leak only, preserves structural wear: " + model);
                radiator = p.parts.Find(i => i.instanceId == radiator.instanceId); radiator.condition.failed = true; radiator.condition.failureReason = "fixture";
                rejects(p, a => PaidEconomy.Pay(a, cat, costs, prices, quote(a, new PaymentRequest { kind = PaymentKind.Repair, carId = carId, targetId = radiator.instanceId, service = ServiceAction.Reseal }), now), "Unrepairable failure cannot be healed by paid reseal: " + model); radiator.condition.failed = false;
                // An oil change must replace the currently fitted grade, not silently downgrade it.
                var upgradedFilter=cat.Find(cat.Id(model,PartCategory.OilFilter,1));
                if(upgradedFilter!=null)PartOperations.Buy(p,cat,carId,upgradedFilter.id,true);
                string filter = p.SelectedCar.installed.slots.Find(s => s.slot == "OilFilter").partInstanceId;
                string fittedFilterId=ServiceOperations.OilFilter(p,p.SelectedCar).id;
                q = quote(p, new PaymentRequest { kind = PaymentKind.OilChange, carId = carId });
                int filterCost=PartsMarket.Quote(p,cat.Find(fittedFilterId),1).cost;
                check(q.Cost==(int)Math.Ceiling(q.Oil*costs.oilPerLitre-1e-9)+costs.oilChangeLabour+filterCost,"Oil change quote uses installed filter grade: "+model);
                cash = p.cash; p = commit(p, a => PaidEconomy.Pay(a, cat, costs, prices, q, now));
                var installedFilter=p.parts.Find(i=>i.instanceId==p.SelectedCar.installed.slots.Find(s=>s.slot=="OilFilter").partInstanceId);
                check(p.cash == cash - q.Cost && p.SelectedCar.fluids.oilCondition == 1 && p.parts.Find(i => i.instanceId == filter).location == PartLocation.Inventory && installedFilter.definitionId==fittedFilterId, "Oil/filter/labour paid once; upgraded filter grade and removed filter retained: " + model);
                q = quote(p, new PaymentRequest { kind = PaymentKind.Part, carId = carId, targetId = cat.Id(model, PartCategory.Pistons, 1), install = true }); cash = p.cash; p = commit(p, a => PaidEconomy.Pay(a, cat, costs, prices, q, now)); check(p.cash == cash - q.Cost && p.economyReceipts.Last().operation == "Part", "Quoted upgrade purchase settles once: " + model);
                var offer = new VehicleOffer { model = model, price = asset.purchasePrice, reputation = asset.reputationRequired, stock = asset.CreateStockTune() }; q = quote(p, new PaymentRequest { kind = PaymentKind.Car, offer = offer }); cash = p.cash; p = commit(p, a => PaidEconomy.Pay(a, cat, costs, prices, q, now, offer));
                check(p.cash == cash - q.Cost && p.cars.Count == 2 && p.SelectedCar.fuel.litres == fuel.capacityLitres, "Dealership payment includes new package resources and separate identities: " + model);
                var bought = p.SelectedCar; double maxPartOut = prices.buyers.Max(b => Valuations.Capture(p, cat, prices, ValuationKind.Engine, bought.engineAssemblyId, b.id, now).Total) + prices.buyers.Max(b => Valuations.Capture(p, cat, prices, ValuationKind.Chassis, bought.chassisId, b.id, now).Total);
                check(q.Cost > maxPartOut && prices.buyers.All(b => Valuations.Capture(p, cat, prices, ValuationKind.Car, bought.instanceId, b.id, now).Total < q.Cost), "Stock immediate disposal cannot profit from dealership package: " + model);
                var sale = Valuations.Capture(p, cat, prices, ValuationKind.Car, bought.instanceId, "enthusiast", now); cash = p.cash; p = commit(p, a => PaidEconomy.Sell(a, cat, prices, sale, now));
                check(p.cash == cash + sale.Total && p.cars.Count == 1 && p.parts.All(i => !sale.IncludedIds.Contains(i.instanceId)) && p.SelectedCar.instanceId == carId, "Whole-car sale removes descendants, reselects actual remaining car: " + model); rejects(p, a => PaidEconomy.Sell(a, cat, prices, sale, now), "Repeated whole-car sale pays nothing: " + model);
                PartOperations.Remove(p, carId, PartCategory.Interior); string interior = p.parts.First(i => i.location == PartLocation.Inventory && cat.Find(i.definitionId).category == PartCategory.Interior).instanceId;
                string piston = p.SelectedCar.installed.slots.Find(s => s.slot == "Pistons").partInstanceId; p.parts.Find(i => i.instanceId == piston).condition.elements[0].fault = ElementFault.Disabled; p.SelectedCar.fluids.oilLitres = 2;
                string exactCondition = JsonUtility.ToJson(p.parts.Find(i => i.instanceId == piston).condition);
                sale = Valuations.Capture(p, cat, prices, ValuationKind.Chassis, chassis, "recycler", now); cash = p.cash; p = commit(p, a => PaidEconomy.Sell(a, cat, prices, sale, now));
                check(p.cars.Count == 0 && p.SelectedCar == null && p.cash == cash + sale.Total && p.engines.Find(e => e.instanceId == engineId).fluids.oilLitres == 2 && p.parts.Any(i => i.instanceId == interior), "Last incomplete/stripped chassis salvaged with retained engine/loose inventory: " + model);
                check(exactCondition == JsonUtility.ToJson(p.parts.Find(i => i.instanceId == piston).condition) && p.economyReceipts.Last().retained.Contains(engineId) && p.economyReceipts.Last().retained.Contains(piston), "Engine identities/faults/resources remain exact in salvage receipt: " + model);
                string json = JsonUtility.ToJson(p); var reload = JsonUtility.FromJson<PlayerProfile>(json); reload.Sanitize(now); MechanicsOwnership.Ensure(reload, cat); check(json == JsonUtility.ToJson(reload) && reload.cars.Count == 0, "Empty garage survives normalization without free OEM car: " + model);
                sale = Valuations.Capture(p, cat, prices, ValuationKind.Engine, engineId, "builder", now, new[] { piston }); cash = p.cash; p = commit(p, a => PaidEconomy.Sell(a, cat, prices, sale, now));
                check(p.cash == cash + sale.Total && p.engines.All(e => e.instanceId != engineId) && p.parts.Find(i => i.instanceId == piston).location == PartLocation.Inventory && p.parts.All(i => !sale.IncludedIds.Contains(i.instanceId)), "Partial engine sale excludes exact kept failed pack and children: " + model);
                sale = Valuations.Capture(p, cat, prices, ValuationKind.LoosePart, piston, "recycler", now); p = commit(p, a => PaidEconomy.Sell(a, cat, prices, sale, now)); check(p.parts.All(i => i.instanceId != piston), "Retained loose pack can be sold once separately: " + model);
                check(MechanicsOwnership.Validate(p, cat) == null, "All multi-asset transactions preserve exclusive ownership: " + model);
                var service = fresh(model); string serviceCar = service.SelectedCar.instanceId;
                service.SelectedCar.fluids.coolantLitres = 1;
                q = quote(service, new PaymentRequest { kind = PaymentKind.CoolantFlush, carId = serviceCar }); cash = service.cash;
                service = commit(service, a => PaidEconomy.Pay(a, cat, costs, prices, q, now));
                check(service.cash == cash - q.Cost && service.SelectedCar.fluids.coolantLitres == MechanicsOwnership.Resolve(service, service.SelectedCar, cat).spec.fluids.coolantCapacityLitres, "Paid flush charges full coolant and labour: " + model);
                var tires=service.parts.Find(i=>i.instanceId==service.SelectedCar.installed.slots.Find(s=>s.slot=="Tires").partInstanceId);
                rejects(service,a=>PaidEconomy.Quote(a,cat,costs,prices,new PaymentRequest{kind=PaymentKind.ElementNew,carId=serviceCar,targetId=tires.instanceId,auxiliaryId=tires.condition.tires[0].id},now),"Generic element purchase cannot bypass typed per-corner tire checkout: "+model);
                var pack = service.parts.Find(i => i.instanceId == service.SelectedCar.installed.slots.Find(s => s.slot == "Pistons").partInstanceId);
                string oldElement = pack.condition.elements[0].id, otherElements = string.Join("/", pack.condition.elements.Skip(1).Select(e => JsonUtility.ToJson(e)));
                pack.condition.elements[0].fault = ElementFault.CompressionLeak; pack.condition.elements[0].severity = .4f;
                q = quote(service, new PaymentRequest { kind = PaymentKind.ElementNew, carId = serviceCar, targetId = pack.instanceId, auxiliaryId = oldElement }); cash = service.cash;
                service = commit(service, a => PaidEconomy.Pay(a, cat, costs, prices, q, now));
                pack = service.parts.Find(i => i.instanceId == pack.instanceId);
                check(service.cash == cash - q.Cost && service.looseEngineElements.Any(e => e.element.id == oldElement && e.element.fault == ElementFault.CompressionLeak) && string.Join("/", pack.condition.elements.Skip(1).Select(e => JsonUtility.ToJson(e))) == otherElements, "Paid singular replacement retains removed fault and all other cylinders: " + model);
                sale = Valuations.Capture(service, cat, prices, ValuationKind.LooseElement, oldElement, "recycler", now); cash = service.cash;
                service = commit(service, a => PaidEconomy.Sell(a, cat, prices, sale, now));
                check(service.cash == cash + sale.Total && service.looseEngineElements.All(e => e.element.id != oldElement), "Quoted individual spare sale removes only its actual identity: " + model);
                rejects(service, a => PaidEconomy.Sell(a, cat, prices, sale, now), "Individual spare cannot be sold twice: " + model);
                string attached = service.SelectedCar.engineAssemblyId; double retainedFuel = service.SelectedCar.fuel.litres;
                sale = Valuations.Capture(service, cat, prices, ValuationKind.Engine, attached, "builder", now, new[] { pack.instanceId });
                service = commit(service, a => PaidEconomy.Sell(a, cat, prices, sale, now));
                check(service.cars.Count == 1 && string.IsNullOrEmpty(service.SelectedCar.engineAssemblyId) && service.SelectedCar.fuel.litres == retainedFuel && service.engines.All(e => e.instanceId != attached) && service.parts.Find(i => i.instanceId == pack.instanceId).location == PartLocation.Inventory && MechanicsOwnership.Validate(service, cat) == null, "Attached engine sale preserves chassis/fuel and selected retained pack: " + model);
            }
            var rebuild = fresh("starter-hatch"); string rebuildCar = rebuild.SelectedCar.instanceId;
            var rebuildFilter=cat.Find(cat.Id("starter-hatch",PartCategory.OilFilter,1));
            if(rebuildFilter!=null)PartOperations.Buy(rebuild,cat,rebuildCar,rebuildFilter.id,true);
            string retainedRebuildFilter=ServiceOperations.OilFilter(rebuild,rebuild.SelectedCar).id;
            var rebuildQuote = quote(rebuild, new PaymentRequest { kind = PaymentKind.Rebuild, carId = rebuildCar }); int rebuildCash = rebuild.cash;
            var originalRebuild = EngineRebuilds.Quote(rebuild, rebuild.SelectedCar, now);
            check(rebuildQuote.Cost == originalRebuild.paidPrice + (int)Math.Ceiling(MechanicsOwnership.Resolve(rebuild, rebuild.SelectedCar, cat).spec.fluids.oilCapacityLitres * costs.oilPerLitre), "Rebuild payment includes renewal oil, existing component/filter cost and labour.");
            rebuild = commit(rebuild, a => PaidEconomy.Pay(a, cat, costs, prices, rebuildQuote, now));
            check(rebuild.cash == rebuildCash - rebuildQuote.Cost && rebuild.SelectedCar.engineRebuild.paidPrice == rebuildQuote.Cost, "Rebuild job captures actual paid total once.");
            rejects(rebuild, a => PaidEconomy.Sell(a, cat, prices, Valuations.Capture(a, cat, prices, ValuationKind.Car, a.SelectedCar.instanceId, "recycler", now), now), "Reserved rebuild blocks whole-car disposal.");
            rejects(rebuild, a => PaidEconomy.Quote(a, cat, costs, prices, new PaymentRequest { kind = PaymentKind.Fuel, carId = rebuildCar, litres = 1 }, now), "Paid modification cannot bypass rebuild reservation.");
            var reserved = rebuild.SelectedCar.engineRebuild;
            rebuild = commit(rebuild, a => EngineRebuilds.Collect(a, rebuildCar, reserved.id, reserved.readyUtcTicks));
            check(rebuild.cash == rebuildCash - rebuildQuote.Cost && rebuild.SelectedCar.engineRebuild == null && ServiceOperations.OilFilter(rebuild,rebuild.SelectedCar).id==retainedRebuildFilter, "Rebuild collection retains upgraded filter specification and charges no second fee.");
            var ringsProfile=fresh("starter-hatch");
            var ringsCar=ringsProfile.SelectedCar;
            var ringPart=ringsProfile.parts.Find(i=>i.instanceId==ringsCar.installed.slots.Find(s=>s.slot=="PistonRings").partInstanceId);
            var blockPart=ringsProfile.parts.Find(i=>i.instanceId==ringsCar.installed.slots.Find(s=>s.slot=="CylinderBlock").partInstanceId);
            ringPart.condition.health=.5f;blockPart.condition.health=.6f;
            check(!ServiceOperations.Available(cat.Find(ringPart.definitionId),ringPart.condition,ServiceOperations.BoreHealth(ringsProfile,ringsCar)).Contains(ServiceAction.Recondition),"Worn bore excludes piston-ring reconditioning from available paid repairs.");
            rejects(ringsProfile,a=>PaidEconomy.Quote(a,cat,costs,prices,new PaymentRequest{kind=PaymentKind.Repair,carId=ringsCar.instanceId,targetId=ringPart.instanceId,service=ServiceAction.Recondition},now),"Worn-bore ring service rejects at quote, before charging.");
            blockPart.condition.health=.9f;
            var ringQuote=quote(ringsProfile,new PaymentRequest{kind=PaymentKind.Repair,carId=ringsCar.instanceId,targetId=ringPart.instanceId,service=ServiceAction.Recondition});int ringCash=ringsProfile.cash;
            ringsProfile=commit(ringsProfile,a=>PaidEconomy.Pay(a,cat,costs,prices,ringQuote,now));
            check(ringsProfile.cash==ringCash-ringQuote.Cost&&ringsProfile.parts.Find(i=>i.instanceId==ringPart.instanceId).condition.health>=.75f,"Viable piston-ring repair charges once after bore prerequisite is met.");
            var wet = fresh("starter-hatch"); var wetDefinition = cat.parts.First(d => d.category == PartCategory.Clutch && d.clutchEnvironment == ClutchEnvironment.Wet && cat.Fits(cat.Spec("starter-hatch"), d));
            PartOperations.Buy(wet, cat, wet.SelectedCar.instanceId, wetDefinition.id, true); MechanicsOwnership.Ensure(wet, cat);
            var wetPart = wet.parts.Find(i => i.instanceId == wet.SelectedCar.installed.slots.Find(s => s.slot == "Clutch").partInstanceId); wetPart.condition.health = .6f; wetPart.condition.clutchOilLitres = .1; wetPart.condition.clutchOilCondition = .2;
            var wetQuote = quote(wet, new PaymentRequest { kind = PaymentKind.ClutchOil, carId = wet.SelectedCar.instanceId, targetId = wetPart.instanceId }); int wetCash = wet.cash;
            wet = commit(wet, a => PaidEconomy.Pay(a, cat, costs, prices, wetQuote, now)); wetPart = wet.parts.Find(i => i.instanceId == wetPart.instanceId);
            check(wet.cash == wetCash - wetQuote.Cost && wetPart.condition.health == .6f && wetPart.condition.clutchOilLitres == wetDefinition.clutchOilCapacityLitres && wetPart.condition.clutchOilCondition == 1, "Paid wet-clutch oil renews bath without restoring friction wear.");
            var test = fresh("starter-hatch"); string owner = test.SelectedCar.instanceId; test.SelectedCar.fuel.litres = 0;
            var payment = quote(test, new PaymentRequest { kind = PaymentKind.Fuel, carId = owner, litres = 3 });
            test.SelectedCar.fuel.litres = 1; rejects(test, a => PaidEconomy.Pay(a, cat, costs, prices, payment, now), "Same-revision tank edit rejects stale payment."); test.SelectedCar.fuel.litres = 0;
            costs.fuelPerLitre += 1; rejects(test, a => PaidEconomy.Pay(a, cat, costs, prices, payment, now), "Unversioned fuel price edit rejects old payment."); costs.fuelPerLitre -= 1;
            rejects(test, a => PaidEconomy.Pay(a, cat, costs, prices, payment, payment.Expires), "Expired payment rejected.");
            rejects(test, a => PaidEconomy.Quote(a, cat, costs, prices, new PaymentRequest { kind = PaymentKind.Fuel, carId = owner, litres = double.NaN }, now), "Nonfinite litres rejected without mutation.");
            PlayerProfile failed; string error; string before = JsonUtility.ToJson(test);
            check(!MechanicsTransactions.Try(test, cat, test.revision, a => PaidEconomy.Pay(a, cat, costs, prices, payment, now), a => false, out failed, out error) && before == JsonUtility.ToJson(test), "Failed persistence rolls back fuel/cash/receipt.");
            var salvage = Valuations.Capture(test, cat, prices, ValuationKind.Chassis, test.SelectedCar.chassisId, "recycler", now);
            check(!MechanicsTransactions.Try(test, cat, test.revision, a => PaidEconomy.Sell(a, cat, prices, salvage, now), a => false, out failed, out error) && before == JsonUtility.ToJson(test), "Failed multi-asset salvage persistence preserves car/engine/cash.");
            test = commit(test, a => PaidEconomy.Sell(a, cat, prices, salvage, now)); test.cash = 0; test = commit(test, a => PaidEconomy.StartRecovery(a, costs, now)); string job = test.recoveryWork.id;
            rejects(test, a => PaidEconomy.ClaimRecovery(a, job, now), "Recovery cannot claim before deadline."); rejects(test, a => PaidEconomy.StartRecovery(a, costs, now), "Cannot stack recovery jobs.");
            long ready = test.recoveryWork.readyUtcTicks; test = commit(test, a => PaidEconomy.ClaimRecovery(a, job, ready)); check(test.cash == costs.recoveryReward && test.cars.Count == 0, "Timed recovery funds empty garage without creating or repairing assets."); rejects(test, a => PaidEconomy.ClaimRecovery(a, job, ready), "Repeated recovery claim rejected."); rejects(test, a => PaidEconomy.StartRecovery(a, costs, now - 1), "Backwards clock cannot start additional jobs.");
            test.cash = costs.recoveryCashLimit; rejects(test, a => PaidEconomy.StartRecovery(a, costs, ready), "Recovery reward bounded by cash eligibility.");
            var cheapest = Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Where(v => v.reputationRequired == 0).Select(v => new VehicleOffer { model = v.spec.id, price = v.purchasePrice, reputation = 0, stock = v.CreateStockTune() }).OrderBy(o => PaidEconomy.DealerPrice(cat, costs, prices, o)).First();
            check(PaidEconomy.DealerPrice(cat, costs, prices, cheapest) < costs.recoveryCashLimit, "Recovery cash ceiling can fund a playable no-reputation replacement package.");
            long clock = ready; test.cash = 0; int jobs = 0;
            while (test.cash < PaidEconomy.DealerPrice(cat, costs, prices, cheapest)) { test = commit(test, a => PaidEconomy.StartRecovery(a, costs, clock)); clock = test.recoveryWork.readyUtcTicks; job = test.recoveryWork.id; test = commit(test, a => PaidEconomy.ClaimRecovery(a, job, clock)); if (++jobs > 100) throw new InvalidOperationException("Unbounded recovery route."); }
            var purchase = PaidEconomy.Quote(test, cat, costs, prices, new PaymentRequest { kind = PaymentKind.Car, offer = cheapest }, clock); test = commit(test, a => PaidEconomy.Pay(a, cat, costs, prices, purchase, clock, cheapest)); check(test.cars.Count == 1 && MechanicsOwnership.Resolve(test, test.SelectedCar, cat).Drivable && test.engines.Any(e => string.IsNullOrEmpty(e.parentVehicleId)), "Zero-cash empty garage recovers through bounded timed work and funded replacement, retaining stored assets.");
            string disk = Path.Combine(folder, "disk/profile-v1.json"); Directory.CreateDirectory(Path.GetDirectoryName(disk));
            try {
                ProfileStore.VerificationPath = disk; var old = fresh("starter-hatch"); old.saveVersion = 4; File.WriteAllText(disk, JsonUtility.ToJson(old)); var migrated = ProfileStore.Load(); check(migrated.saveVersion == MechanicsOwnership.SaveVersion && migrated.SelectedCar.instanceId == old.SelectedCar.instanceId && File.Exists(disk + ".legacy-v4"), "Schema-4 migration retains original identities and backup.");
                var dq = PaidEconomy.Quote(migrated, cat, costs, prices, new PaymentRequest { kind = PaymentKind.Fuel, carId = migrated.SelectedCar.instanceId, litres = 1 }, now); // full tank: capture only
                var ds = Valuations.Capture(migrated, cat, prices, ValuationKind.Car, migrated.SelectedCar.instanceId, "recycler", now);
                PlayerProfile committed; check(MechanicsTransactions.Try(migrated, cat, migrated.revision, a => PaidEconomy.Sell(a, cat, prices, ds, now), ProfileStore.TrySave, out committed, out error), "Actual disk transaction commits last-car sale.");
                var loaded = ProfileStore.Load(); check(loaded.cars.Count == 0 && loaded.economyReceipts.Any(r => r.id == ds.Id) && loaded.cash == committed.cash, "Disk reload keeps disposal receipt / money / empty ownership together."); rejects(loaded, a => PaidEconomy.Sell(a, cat, prices, ds, now), "Reloaded disposal cannot pay twice.");
                File.Copy(disk, disk + ".tmp", true); check(ProfileStore.Load().cash == loaded.cash, "Leftover temporary transaction file cannot duplicate settlement.");
                File.WriteAllText(disk, "truncated"); var recovered = ProfileStore.Load(); check(recovered.SelectedCar != null && recovered.economyReceipts.All(r => r.id != ds.Id) && recovered.cash == migrated.cash, "Backup recovery restores complete pre-sale assets and pre-sale cash, never a mixed tree/payment.");
            } finally { ProfileStore.VerificationPath = null; }
            File.WriteAllText(Path.Combine(folder, "verification.txt"), "3D / schema 5 / " + MechanicsSchema.Model + "\n" + log + "Checks: " + checks + "\n"); Debug.Log("DRAGLAB PAID OPERATIONS PASS " + checks);
        }
    }
}
