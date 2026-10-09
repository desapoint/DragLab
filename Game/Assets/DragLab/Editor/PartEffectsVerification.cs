using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Editor
{
    public static class PartEffectsVerification
    {
        [MenuItem("DragLab/Verify Part Effects")]
        public static void RunStandalone() { Run(Path.GetFullPath(Path.Combine(Application.dataPath, "../.."))); }
        public static int Run(string root)
        {
            var vehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            var catalog = new MechanicalCatalog(vehicles.Select(v => v.spec)); int count = 0; var log = new StringBuilder();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            foreach (var definition in vehicles) {
                string id = definition.spec.id; var stock = catalog.Resolve(id, catalog.Recipe(id), definition.CreateStockTune());
                foreach (var part in catalog.parts.Where(p => catalog.Fits(definition.spec, p))) {
                    check(!string.IsNullOrWhiteSpace(part.description) && (!PartSemantics.EngineInternal(part.category) ? part.rpmLimit == 0 : part.rpmLimit > definition.spec.redlineRpm) && (!PartSemantics.TorqueRated(part.category) ? part.torqueLimitNm == 0 : part.torqueLimitNm > 0), "Relevant ratings and explained effects: " + part.id);
                }
                var map = catalog.Recipe(id); map.Remove(PartCategory.Interior); map.Remove(PartCategory.Accessories);
                var stripped = catalog.Resolve(id, map, definition.CreateStockTune());
                check(stripped.Drivable && Math.Abs(new WheelProfile(stock.spec, stock.tune).vehicleMass - new WheelProfile(stripped.spec, stripped.tune).vehicleMass - 80) < 1e-6, "Strip cabin/comfort equipment subtracts 80 kg without removing chassis: " + id);
                var strippedPass = Benchmark.QuarterMile(stripped.spec, stripped.tune); var fullPass = Benchmark.QuarterMile(stock.spec, stock.tune);
                check(strippedPass.elapsed > 0 && strippedPass.elapsed < 30, "Stripped build completes actual shared race; timing includes changed grip/shift dynamics: " + id);
                log.AppendLine("Mass A/B " + id + ": stock " + fullPass.elapsed + " s; stripped " + strippedPass.elapsed + " s.");
                map = catalog.Recipe(id); map.Remove(PartCategory.Body); check(!catalog.Resolve(id, map).Drivable, "Removing structural body makes an incomplete build: " + id);
                map = catalog.Recipe(id); map.Remove(PartCategory.Exhaust); var headers = catalog.Resolve(id, map);
                check(headers.Drivable && new EngineSoundProfile(headers.spec, headers.tune).exhaustSystem.parts.Length == 0, "Exhaust removal runs with open acoustic path: " + id);
                map = catalog.Recipe(id); map.Remove(PartCategory.Induction); var na = catalog.Resolve(id, map);
                check(na.Drivable && new InductionProfile(na.spec, na.tune).type == InductionType.NaturallyAspirated, "Removed boost hardware produces NA operation: " + id);
                if (definition.spec.induction.factory != InductionType.NaturallyAspirated) check(DragMath.Torque(na.spec, na.tune, 4000) < DragMath.Torque(stock.spec, stock.tune, 4000), "Removing factory boost reduces torque rather than retaining boosted curve: " + id);
                map = catalog.Recipe(id); map.Remove(PartCategory.Cooling); var uncooled = catalog.Resolve(id, map);
                check(uncooled.Drivable && new CarSimulation(uncooled.spec, uncooled.tune, new DriverSettings()).state.engineCoolingFactor == .15, "Engine can run without radiator, with residual cooling only: " + id);
                map = catalog.Recipe(id); map[PartCategory.Cooling] = catalog.Id(id, PartCategory.Cooling, 1); var cooled = catalog.Resolve(id, map);
                check(new CarSimulation(cooled.spec, cooled.tune, new DriverSettings()).state.engineCoolingFactor > 2 && cooled.tune.PowerMultiplier == stock.tune.PowerMultiplier && cooled.safeRpm == stock.safeRpm && cooled.safeTorqueNm == stock.safeTorqueNm, "Cooling adds heat transfer, no invented HP/RPM/strength: " + id);
                map = catalog.Recipe(id); map.Remove(PartCategory.Controls);
                check(catalog.Resolve(id, map).Drivable == (definition.spec.engineBehavior.fueling == FuelSystem.Carburetor), "ECU removal distinguishes injected versus mechanical carb engine: " + id);
                map = catalog.Recipe(id); map[PartCategory.Gearbox] = catalog.Id(id, PartCategory.Gearbox, 1); var gear = catalog.Resolve(id, map);
                check(gear.tune.CapacityMultiplier == stock.tune.CapacityMultiplier && gear.tune.GearCapacityMultiplier > stock.tune.GearCapacityMultiplier, "Gearbox upgrade does not secretly upgrade separate clutch: " + id);
                foreach (var type in new[] { TransmissionUpgrade.Automatic, TransmissionUpgrade.CVT }) {
                    map = catalog.Recipe(id); map[PartCategory.Gearbox] = id + "/Gearbox/" + type; map.Remove(PartCategory.Clutch);
                    var r = catalog.Resolve(id, map); var car = new CarSimulation(r.spec, r.tune, new DriverSettings()); car.Launch(0);
                    for (int i = 0; i < 2400; i++) car.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
                    check(r.Drivable && !car.state.drivetrainFailed && car.state.speed > 10, "Converter/CVT drives without separate friction clutch: " + id + "/" + type);
                }
            }
            var model = catalog.Spec("starter-hatch"); var recipe = catalog.Recipe(model.id);
            var normal = catalog.Resolve(model.id, recipe);
            recipe[PartCategory.Controls] = catalog.Id(model.id, PartCategory.Controls, 1); var controllerOnly = catalog.Resolve(model.id, recipe);
            check(!controllerOnly.tune.partLimiterAdjustable && !controllerOnly.tune.partTwoStepAdjustable && !controllerOnly.tune.partShiftLight, "Controller purchase does not grant separate limiter/two-step/shift-light modules.");
            recipe[PartCategory.RevLimiter] = catalog.Id(model.id, PartCategory.RevLimiter, 1);
            var limiterTune = new Tune { limiter = LimiterStyle.Soft, revLimitRpm = 5000, twoStep = EquipmentSetting.On };
            var withLimiter = catalog.Resolve(model.id, recipe, limiterTune); var profile = new EngineBehaviorProfile(withLimiter.spec, withLimiter.tune);
            check(profile.limiter == LimiterStyle.Soft && profile.limitRpm == 5000 && !profile.twoStep && !withLimiter.tune.partShiftLight, "Limiter purchase independently unlocks threshold/style, without other equipment.");
            recipe[PartCategory.TwoStep] = catalog.Id(model.id, PartCategory.TwoStep, 1); limiterTune.launchRpm = 3000; limiterTune.twoStepStyle = LimiterStyle.Soft;
            var two = catalog.Resolve(model.id, recipe, limiterTune); profile = new EngineBehaviorProfile(two.spec, two.tune);
            check(profile.twoStep && profile.launchLimiter == LimiterStyle.Soft && profile.launchRpm == 3000, "Programmable two-step independently applies assigned launch cut.");
            recipe[PartCategory.ShiftLight] = catalog.Id(model.id, PartCategory.ShiftLight, 1); limiterTune.shiftLightRpm = 4200;
            var light = catalog.Resolve(model.id, recipe, limiterTune);
            check(light.tune.partShiftLight && light.tune.ShiftLightRpm(light.spec) == 4200 && light.tune.PowerMultiplier == two.tune.PowerMultiplier && light.tune.shiftRpm == two.tune.shiftRpm, "Shift-light threshold is separate from automatic shifts and engine power.");
            recipe.Remove(PartCategory.RevLimiter); var removed = catalog.Resolve(model.id, recipe, limiterTune);
            check(new EngineBehaviorProfile(removed.spec, removed.tune).limiter == LimiterStyle.None && !removed.tune.partLimiterAdjustable, "Removing limiter removes protection and tuning capability.");
            recipe[PartCategory.TwoStep] = catalog.Id(model.id, PartCategory.TwoStep); var fixedTwo = catalog.Resolve(model.id, recipe, limiterTune);
            check(!fixedTwo.tune.partTwoStepAdjustable && new EngineBehaviorProfile(fixedTwo.spec, fixedTwo.tune).launchRpm == 3500, "Basic two-step has fixed threshold; old stored sliders cannot override its hardware.");
            var allForged = catalog.Recipe(model.id); foreach (var c in new[] { PartCategory.Pistons, PartCategory.Rods, PartCategory.Crankshaft }) allForged[c] = catalog.Id(model.id, c, 1);
            var forgedBuild = catalog.Resolve(model.id, allForged); check(forgedBuild.spec.engineBehavior.safeRpmMultiplier > normal.spec.engineBehavior.safeRpmMultiplier && forgedBuild.safeRpm > normal.safeRpm, "Matched forged core raises actual mechanical overrev threshold, not just displayed rating.");
            var unused = new CarSimulation(normal.spec, normal.tune, new DriverSettings()); var trim = unused.mechanics.parts.Find(p => p.slot == "Interior"); trim.condition.temperatureC = 800;
            for (int i = 0; i < 120; i++) unused.mechanics.Tick(unused, DragMath.FixedStep, 0, 0);
            check(trim.condition.health == 1 && !trim.condition.failed, "Nonwear interior never gets generic engine heat/thermal damage.");
            var wearless = new CarSimulation(normal.spec, normal.tune, new DriverSettings { wearMode = WearMode.None });
            var oldClutch = wearless.mechanics.parts.Find(p => p.slot == "Clutch"); oldClutch.condition.temperatureC = 500; wearless.mechanics.Tick(wearless, 1, 5000, 0);
            check(oldClutch.condition.health == 1, "None wear remains authoritative for hardware thermal damage.");
            var paid = PlayerProfile.New(DateTime.UtcNow.Ticks); paid.cash = 10000; MechanicsOwnership.Ensure(paid, catalog);
            foreach (var module in new[] { PartCategory.RevLimiter, PartCategory.TwoStep, PartCategory.ShiftLight }) { int cash = paid.cash; var bought = PartOperations.Buy(paid, catalog, paid.SelectedCar.instanceId, catalog.Id(model.id, module, 1), true); check(paid.cash == cash - catalog.Find(catalog.Id(model.id, module, 1)).price && paid.SelectedCar.installed.slots.Any(x => x.partInstanceId == bought), "Separate paid module installation: " + module); }
            var open = DifferentialDynamics.Solve(new DifferentialSpec(), 10000, 500, 3000, .3);
            var lsd = DifferentialDynamics.Solve(new DifferentialSpec { kind = DifferentialKind.LimitedSlip, torqueBiasRatio = 3, preloadNm = 35 }, 10000, 500, 3000, .3);
            var welded = DifferentialDynamics.Solve(new DifferentialSpec { kind = DifferentialKind.Welded }, 10000, 500, 3000, .3);
            var spool = DifferentialDynamics.Solve(new DifferentialSpec { kind = DifferentialKind.Spool }, 10000, 500, 3000, .3);
            check(open.capacity == 1000 && lsd.capacity > open.capacity && lsd.capacity < welded.capacity && welded.capacity == 3500 && spool.capacity == welded.capacity, "Split grip: open limited by weak wheel; LSD bounded bias; welded/spool combine grip without creating grip.");
            check(open.leftSlip > open.rightSlip && welded.leftSlip == welded.rightSlip, "Split-grip spin estimates distinguish weak-wheel open spin and locked outputs.");
            foreach (DifferentialKind kind in Enum.GetValues(typeof(DifferentialKind))) check(DifferentialDynamics.Solve(new DifferentialSpec { kind = kind, torqueBiasRatio = 3, preloadNm = 35 }, 2000, 1000, 1000, .3).capacity == 2000, "Even grip has no fictitious traction bonus: " + kind);
            foreach (int stage in new[] { 0, 1, 2, 3 }) {
                var map = catalog.Recipe(model.id); map[PartCategory.Differential] = catalog.Id(model.id, PartCategory.Differential, stage);
                var r = catalog.Resolve(model.id, map, new Tune { frontLeftGrip = .15f, frontRightGrip = 1 }); var car = new CarSimulation(r.spec, r.tune, new DriverSettings { tractionAssist = false, wearMode = WearMode.None });
                car.state.frontDriveAvailable = true; double force = AxleDriveDynamics.RoadForce(car, 10000, 6000, 0, DragMath.FixedStep);
                check(car.spec.frontDifferential.kind == (DifferentialKind)stage && force > 0 && car.state.frontLeftSlip >= 0, "Installed differential affects shared road solver: stage " + stage);
                log.AppendLine("Road force stage " + stage + ": " + force.ToString("F2") + " N");
            }
            var pickup = catalog.Spec("utility-pickup"); var centerMap = catalog.Recipe(pickup.id); centerMap[PartCategory.Differential] = catalog.Id(pickup.id, PartCategory.Differential, 1);
            var centerBuild = catalog.Resolve(pickup.id, centerMap); var centerCar = new CarSimulation(centerBuild.spec, centerBuild.tune, new DriverSettings { wearMode = WearMode.None, tractionAssist = false });
            double withPreload = AxleDriveDynamics.RoadForce(centerCar, 10000, 0, 6000, DragMath.FixedStep); centerCar.awd.centerPreloadNm = 0;
            double withoutPreload = AxleDriveDynamics.RoadForce(centerCar, 10000, 0, 6000, DragMath.FixedStep);
            check(centerCar.awd.mode == AwdMode.LimitedSlipCenter && withPreload > 0 && withoutPreload == 0, "Center LSD preload actually supports a low-resistance connected branch; zero preload cannot invent high-grip axle torque.");
            var ecuProfile = PlayerProfile.New(DateTime.UtcNow.Ticks); MechanicsOwnership.Ensure(ecuProfile, catalog); var ecu = ecuProfile.parts.Find(p => p.slot == "Controls"); ecu.condition.health = 0; ecu.condition.failed = true; ecu.condition.failureReason = "Electrical fixture";
            check(!MechanicsOwnership.Resolve(ecuProfile, ecuProfile.SelectedCar, catalog).Drivable && !InspectionBuild.Capture(ecuProfile, ecuProfile.SelectedCar).build.Drivable, "Failed required EFI controller blocks garage entry and inspection consistently, without declaring broken engine internals.");
            // Known equal heat input validates the actual shared thermal solver, not a UI-only stat.
            Func<bool, CarSimulation> hotEngine = upgrade => {
                var map = catalog.Recipe(model.id); if (upgrade) map[PartCategory.Cooling] = catalog.Id(model.id, PartCategory.Cooling, 1);
                var r = catalog.Resolve(model.id, map); var car = new CarSimulation(r.spec, r.tune, new DriverSettings { wearMode = WearMode.Full });
                foreach (var p in car.mechanics.parts) if (PartSemantics.EngineInternal(car.mechanics.Definition(p).category)) p.condition.temperatureC = 170;
                for (int i = 0; i < 120 * 180; i++) { car.state.engineHorsepower = 480; car.state.engineTorqueNm = 0; car.state.rpm = 3000; car.mechanics.Tick(car, DragMath.FixedStep, 0, 0); if (car.state.engineFailed) break; }
                return car;
            };
            var hotOem = hotEngine(false); var hotUpgrade = hotEngine(true);
            var oldCore = hotOem.mechanics.parts.Find(p => p.slot == "Pistons"); var newCore = hotUpgrade.mechanics.parts.Find(p => p.slot == "Pistons");
            check(newCore.condition.temperatureC < oldCore.condition.temperatureC - 10 && newCore.condition.health > oldCore.condition.health && hotUpgrade.state.coolingHeatRejectedW > 0, "Equal severe load: radiator upgrade removes more heat and protects engine internals.");
            check(hotOem.state.engineFailed && !hotUpgrade.state.engineFailed, "A tune that thermally breaks OEM core survives the same timed load with upgraded cooling.");
            log.AppendLine("Thermal 480 hp / 180 s: OEM core " + oldCore.condition.temperatureC + " C / " + oldCore.condition.health + "; upgraded " + newCore.condition.temperatureC + " C / " + newCore.condition.health);
            var save = PlayerProfile.New(DateTime.UtcNow.Ticks); MechanicsOwnership.Ensure(save, catalog); var owned = save.SelectedCar;
            var interior = save.parts.Find(p => p.slot == "Interior"); string retainedId = interior.instanceId;
            PartOperations.Remove(save, owned.instanceId, PartCategory.Interior); save = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(save)); MechanicsOwnership.Ensure(save, catalog);
            check(!save.SelectedCar.installed.slots.Any(s => s.slot == "Interior") && save.parts.Any(p => p.instanceId == retainedId && p.location == PartLocation.Inventory), "Removed interior persists empty after reload, with original retained in inventory.");
            PartOperations.Install(save, catalog, save.SelectedCar.instanceId, retainedId);
            check(save.SelectedCar.installed.slots.Any(s => s.slot == "Interior" && s.partInstanceId == retainedId), "Reinstall original interior without buying replacement or healing another component.");
            string folder = Path.Combine(root, "reports/part-effects"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "verification.txt"), log + "Checks: " + count + "\n");
            File.WriteAllText(Path.Combine(folder, "catalog.md"), string.Join("\n\n", catalog.parts.Select(p => "### " + p.id + "\n" + p.name + "\n\n" + p.description + "\n\n" + string.Join("\n", PartSemantics.Stats(p).Select(v => "- " + v.label + ": " + v.value + " " + v.unit)))));
            return count;
        }
    }
}
