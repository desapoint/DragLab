using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Editor
{
    public static class StockDurabilityVerification
    {
        public static void Audit()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            Run(root, false);
        }
        public static int Run(string root, bool enforce = true)
        {
            int checks = 0; var report = new StringBuilder("OEM durability / full wear, simulated thermals, multiplier 1 / shared race and bench model.\n");
            var csv = new StringBuilder("car,scenario,passes,elapsed_s,distance_m,rpm,peak_rpm,engine_damage,peak_engine_nm,peak_transmitted_nm,part,health,peak_temperature_c,failure\n");
            var failures = new List<string>();
            var vehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            var catalog = new MechanicalCatalog(vehicles.Select(v => v.spec));
            foreach (var v in vehicles)
            {
                var build = catalog.Resolve(v.spec.id, catalog.Recipe(v.spec.id), v.CreateStockTune());
                foreach (string scenario in new[] { "ten-quarter-miles", "two-minute-pull", "five-minute-idle" })
                {
                    var driver = new DriverSettings { wearMode = WearMode.Full, wearMultiplier = 1, optimalThermals = false, throttleAssist = false, tractionAssist = false };
                    List<PartInstance> carried = null; CarSimulation car = null;
                    var peaks = new Dictionary<string, double>(); double elapsed = 0, peakEngine = 0, peakShaft = 0, peakRpm = 0, damage = 0; int passes = 0;
                    Action sample = () => { elapsed += DragMath.FixedStep; peakEngine = Math.Max(peakEngine, Math.Abs(car.state.engineTorqueNm)); peakShaft = Math.Max(peakShaft, Math.Abs(car.state.transmittedTorqueNm)); peakRpm = Math.Max(peakRpm, car.state.rpm); damage = Math.Max(damage, car.state.engineDamage); foreach (var p in car.mechanics.parts) { double peak; peaks.TryGetValue(p.slot, out peak); peaks[p.slot] = Math.Max(peak, p.condition.temperatureC); } };
                    int repeats = scenario == "ten-quarter-miles" ? 10 : 1;
                    for (int run = 0; run < repeats; run++)
                    {
                        car = new CarSimulation(build.spec, build.tune, driver, carried);
                        if (scenario == "five-minute-idle")
                        {
                            for (int tick = 0; tick < 36000 && !Broken(car); tick++) { car.IndependentStep(DragMath.FixedStep, new DriverInput(), true, false); sample(); }
                        }
                        else
                        {
                            for (int tick = 0; tick < 360; tick++) { car.Stage(DragMath.FixedStep, new DriverInput()); sample(); }
                            car.Launch(0);
                            int duration = scenario == "two-minute-pull" ? 14400 : 3600;
                            for (int tick = 0; tick < duration && !Broken(car); tick++) { car.Step(DragMath.FixedStep, new DriverInput { throttle = 1 }); sample(); if (scenario == "ten-quarter-miles" && car.state.distance >= 402.336) { passes++; break; } }
                            if (scenario == "ten-quarter-miles" && !Broken(car))
                            {
                                // Neutral coast/braking then idle; carry actual heat/wear into the next pass.
                                for (int tick = 0; tick < 7200 && !Broken(car); tick++) { car.IndependentStep(DragMath.FixedStep, new DriverInput { brake = car.state.speed > .1 ? 1 : 0 }, true, false); sample(); }
                            }
                        }
                        carried = car.mechanics.parts.Select(p => p.Copy()).ToList();
                        if (Broken(car)) break;
                    }
                    bool okay = !Broken(car) && (scenario != "ten-quarter-miles" || passes == 10) && carried.All(p => p.condition.health >= .9f);
                    string label = v.spec.id + " / " + scenario + " / passes " + passes + " / lowest health " + carried.Min(p => p.condition.health).ToString("P2") + " / " + string.Join("; ", carried.Where(p => p.condition.failed).Select(p => p.slot + ": " + p.condition.failureReason));
                    report.AppendLine((okay ? "PASS: " : "FAIL: ") + label); checks++; if (!okay) failures.Add(label);
                    foreach (var p in carried) csv.AppendLine(string.Join(",", v.spec.id, scenario, passes, F(elapsed), F(car.state.distance), F(car.state.rpm), F(peakRpm), F(damage), F(peakEngine), F(peakShaft), p.slot, F(p.condition.health), F(peaks[p.slot]), p.condition.failureReason));
                }
            }
            string folder = Path.Combine(root, "reports/stock-durability"); Directory.CreateDirectory(folder);
            if (enforce)
            {
                Action<bool, string> check = (ok, label) => { checks++; report.AppendLine((ok ? "PASS: " : "FAIL: ") + label); if (!ok) failures.Add(label); };
                var hatch = vehicles.First(v => v.spec.id == "starter-hatch");
                foreach(var vehicle in vehicles) {
                    var build=catalog.Resolve(vehicle.spec.id,catalog.Recipe(vehicle.spec.id),vehicle.CreateStockTune());
                    var free=new CarSimulation(build.spec,build.tune,new DriverSettings {throttleAssist=false});
                    free.state.converterLossPowerW=80000;free.state.clutchFrictionPowerW=80000;
                    var box=free.mechanics.Part(PartCategory.Gearbox);box.condition.temperatureC=100;
                    free.IndependentStep(DragMath.FixedStep,new DriverInput(),true,false);
                    check(free.state.converterLossPowerW==0&&free.state.clutchFrictionPowerW==0&&box.condition.temperatureC<100,"Disconnected neutral clears prior coupling heat and gearbox cools: "+vehicle.spec.id);
                    free.state.converterLossPowerW=80000;free.state.clutchFrictionPowerW=80000;
                    free.Stage(DragMath.FixedStep,new DriverInput());
                    check(free.state.converterLossPowerW==0&&free.state.clutchFrictionPowerW==0,"Staging cannot retain a previous coupling heat sample: "+vehicle.spec.id);
                    foreach(var part in free.mechanics.parts){part.condition.temperatureC=100;if(part.condition.tires!=null)foreach(var tire in part.condition.tires)tire.temperatureC=100;}
                    box.condition.health=.7f;box.condition.failed=true;box.condition.failureReason="Fixture failure";
                    free.state.fluids.oilTemperatureC=110;double fuel=free.state.fuelLitres,oil=free.state.fluids.oilLitres;
                    free.CoolDown(600);
                    check(box.condition.temperatureC<100&&box.condition.health==.7f&&box.condition.failed&&box.condition.failureReason=="Fixture failure","Race turnaround cools a gearbox without repairing it: "+vehicle.spec.id);
                    check(free.state.fuelLitres==fuel&&free.state.fluids.oilLitres<=oil&&free.state.fluids.oilTemperatureC<110,"Parked turnaround burns no fuel, adds no liquid, and cools oil: "+vehicle.spec.id);
                    check(free.mechanics.Part(PartCategory.Tires).condition.tires.All(t=>t.temperatureC<100),"Turnaround cools each retained tire: "+vehicle.spec.id);
                }
                var oldReplay = new RunSnapshot { model = "draglab-shared-stress-v2", vehicle = hatch.spec.Copy(), tune = hatch.CreateStockTune(), driving = new DriverSettings() };
                check(PhysicalDrivingSession.Validate(oldReplay) != null, "Prior physics recordings are rejected instead of silently changing their repeatable results.");
                oldReplay.model="draglab-integrated-tools-v15";
                check(PhysicalDrivingSession.Validate(oldReplay)!=null,"Pre-thermal-integration v15 recordings require a new capture.");
                var lowGrip = catalog.Resolve(hatch.spec.id, catalog.Recipe(hatch.spec.id), hatch.CreateStockTune()); lowGrip.spec.tireFriction = .4f;
                var stop = new CarSimulation(lowGrip.spec, lowGrip.tune, new DriverSettings()); RoadFixture.Seed(stop, 35);
                double energyBefore = stop.state.contact.KineticEnergy(stop.mass.TranslatingKg, stop.wheels.totalInertia * .25);
                stop.IndependentStep(DragMath.FixedStep, new DriverInput { brake = 1 }, true, false);
                double removed = energyBefore - stop.state.contact.KineticEnergy(stop.mass.TranslatingKg, stop.wheels.totalInertia * .25);
                double work = stop.state.brakeFrictionPowerW * DragMath.FixedStep;
                var brakes = stop.mechanics.parts.Find(p => p.slot == "Brakes"); var brakeDefinition = stop.mechanics.Definition(brakes);
                check(work > 0 && work <= removed + 1e-6 && stop.state.brakeFrictionPowerW < stop.wheels.vehicleMass * stop.spec.BrakeDeceleration * 35, "Brake heat is bounded by actual body plus independent wheel energy loss and applied brake work.");
                check(Math.Abs(brakes.condition.temperatureC - 20 - work / brakeDefinition.heatCapacityJPerC) < 1e-8, "Published road brake work is the same heat consumed by shared component stress.");
                var held = new CarSimulation(lowGrip.spec, lowGrip.tune, new DriverSettings()); held.IndependentStep(DragMath.FixedStep, new DriverInput { brake = 1 }, true, true);
                check(held.state.brakeFrictionPowerW == 0 && held.mechanics.parts.Find(p => p.slot == "Brakes").condition.health == 1, "Holding brakes stationary does not heat or wear them.");
                brakes.condition.temperatureC = 1000; brakes.condition.health = .001f;
                for (int i = 0; i < 120 && !brakes.condition.failed; i++) stop.IndependentStep(DragMath.FixedStep, new DriverInput { brake = 1 }, true, false);
                check(brakes.condition.failed && brakes.condition.failureReason == "Thermal overload", "Truly overheated worn brakes can still fail; OEM calibration does not disable thermal damage.");
                var muscle = vehicles.First(v => v.spec.id == "v8-muscle"); var v8 = catalog.Resolve(muscle.spec.id, catalog.Recipe(muscle.spec.id), muscle.CreateStockTune());
                var pedal = new DriverInput { throttle = 1 };
                foreach (var mode in new[] { ShiftMode.Automatic, ShiftMode.Sequential })
                {
                    var car = new CarSimulation(v8.spec, v8.tune, new DriverSettings { shifting = mode, throttleAssist = false }); car.Launch(0);
                    car.state.gear = 2; RoadFixture.Seed(car, 20); car.state.rpm = 6200; car.Shift(1, pedal); car.Step(DragMath.FixedStep, pedal);
                    check(car.state.rpm > car.spec.idleRpm && pedal.throttle == 1 && (mode == ShiftMode.Automatic ? car.state.throttle == 0 : car.state.throttle == 1 && car.state.engineHorsepower > 0), "Manual-gearbox handover keeps rotating and preserves input; only automatic driver lifts: " + mode);
                    if (mode == ShiftMode.Automatic)
                    {
                        for (int i = 0; i < 150; i++) car.Step(DragMath.FixedStep, pedal);
                        check(car.state.throttle == 1 && !Broken(car), "Automatic handover reapplies held gas without re-pressing the input.");
                    }
                }
                var ungoverned = new CarSimulation(v8.spec, v8.tune, new DriverSettings { launch = LaunchMode.Manual, shifting = ShiftMode.Sequential, throttleAssist = false });
                for (int i = 0; i < 2400 && !ungoverned.state.engineFailed; i++) ungoverned.IndependentStep(DragMath.FixedStep, pedal, true, false);
                check(ungoverned.engine.limiter == LimiterStyle.None && ungoverned.state.engineFailed, "Classic V8 retains no limiter and still fails under sustained manual neutral overrev.");
            }
            File.WriteAllText(Path.Combine(folder, "verification.txt"), report + "Checks: " + checks + "\n");
            File.WriteAllText(Path.Combine(folder, "parts.csv"), csv.ToString());
            if (enforce && failures.Count > 0) throw new InvalidOperationException("Stock durability: " + string.Join("\n", failures));
            Debug.Log(report.ToString()); return checks;
        }
        static bool Broken(CarSimulation car) { return car.state.engineFailed || car.state.drivetrainFailed || car.mechanics.parts.Any(p => p.condition.failed); }
        static string F(double value) { return value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
