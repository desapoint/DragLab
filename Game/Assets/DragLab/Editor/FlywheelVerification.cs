using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Editor
{
    public static class FlywheelVerification
    {
        public static int Run(string root)
        {
            int count = 0;
            var report = new StringBuilder("Calculated flywheel / neutral coast / bounded gear synchronization.\n");
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException("FLYWHEEL CHECK FAILED: " + label); count++; report.AppendLine("PASS: " + label); };
            var spec = new VehicleSpec();
            spec.flywheel = new FlywheelSpec { massKg = 10, innerRadiusM = 0, outerRadiusM = .2f };
            check(Math.Abs(spec.flywheel.Inertia - .2) < .000001, "10 kg / 0.2 m solid disc has inertia 0.2 kg m².");
            spec.flywheel.innerRadiusM = .1f;
            check(Math.Abs(spec.flywheel.Inertia - .25) < .000001, "Annular disc uses both inner and outer radii.");
            var lightTune = new Tune { flywheelMass = .5f }; var heavyTune = new Tune { flywheelMass = 2 };
            check(Math.Abs(FlywheelDynamics.Inertia(spec, heavyTune) / FlywheelDynamics.Inertia(spec, lightTune) - 4) < .000001, "Flywheel mass changes inertia linearly.");
            var copy = spec.Copy(); copy.flywheel.outerRadiusM *= 2; copy.flywheel.innerRadiusM *= 2;
            check(Math.Abs(copy.flywheel.Inertia / spec.flywheel.Inertia - 4) < .000001, "Doubling both radii quadruples inertia without changing mass.");
            check(spec.flywheel.outerRadiusM == .2f, "Vehicle copies own their flywheel data.");
            check(new Tune().FlywheelMass == 1 && new Tune { flywheelMass = 0 }.FlywheelMass == 1, "Legacy tune zero selects stock flywheel mass.");
            var old = JsonUtility.FromJson<VehicleSpec>("{\"id\":\"legacy\",\"engineInertiaKgM2\":0.3}");
            check(old.Copy().RotationalInertia(new Tune()) > .3, "Legacy specs without flywheel data resolve a modeled flywheel plus explicit core inertia.");
            copy.flywheel.innerRadiusM = copy.flywheel.outerRadiusM;
            check(copy.Validate() != null, "Invalid annular geometry is rejected.");
            copy = spec.Copy(); copy.flywheel.massKg = float.NaN;
            check(copy.Validate() != null, "Non-finite flywheel mass is rejected.");
            var invalidTune = new Tune { flywheelMass = float.NaN }; invalidTune.ClampTo(spec);
            check(invalidTune.FlywheelMass == .25, "Non-finite tune mass resolves to a bounded value.");
            var s = new CarState { rpm = 3000 }; FlywheelDynamics.Publish(spec, lightTune, s);
            double energy = s.flywheelEnergyJ, momentum = s.flywheelAngularMomentum;
            s.rpm = 6000; FlywheelDynamics.Publish(spec, lightTune, s);
            check(Math.Abs(s.flywheelEnergyJ / energy - 4) < .000001 && Math.Abs(s.flywheelAngularMomentum / momentum - 2) < .000001, "Stored energy scales with RPM squared; angular momentum with RPM.");
            var light = new EngineTestBench(spec, lightTune); var heavy = new EngineTestBench(spec, heavyTune);
            var hold = new EngineBenchControls { fixedRpm = true, targetRpm = 5000, launchControls = false };
            light.Step(hold); heavy.Step(hold); double heavyInitialEnergy = heavy.state.flywheelEnergyJ;
            hold.fixedRpm = false;
            for (int i = 0; i < 120; i++) { light.Step(hold); heavy.Step(hold); }
            check(heavy.state.rpm > light.state.rpm + 500, "After the same neutral lift, the heavier flywheel retains substantially more RPM.");
            check(heavy.state.rpm < 5000 && heavy.state.flywheelEnergyJ < heavyInitialEnergy, "Neutral drag consumes stored energy rather than holding RPM indefinitely.");
            check(light.state.engineHorsepower == 0 && light.state.rpm > spec.idleRpm && light.state.flywheelEnergyJ > 0, "Zero combustion horsepower does not mean a stopped engine.");
            for (int i = 0; i < 3600; i++) { light.Step(hold); heavy.Step(hold); }
            check(light.state.rpm >= spec.idleRpm && heavy.state.rpm >= spec.idleRpm, "Both coast-downs settle at idle without an unintended stall.");
            light = new EngineTestBench(spec, lightTune); heavy = new EngineTestBench(spec, heavyTune);
            var gas = new EngineBenchControls { throttle = 1, launchControls = false };
            for (int i = 0; i < 36; i++) { light.Step(gas); heavy.Step(gas); }
            check(light.state.rpm > heavy.state.rpm + 500, "Lighter flywheel also revs up faster under identical combustion torque.");
            light.state.rpm = 4500; light.SetGear(1); light.SetGear(2);
            check(light.state.rpm == 4500 && light.state.shiftRemaining > 0, "Bench gear selection preserves RPM and starts a timed handover.");
            gas.gear = 2; light.Step(gas);
            check(light.state.rpm > 4000 && light.state.transmittedTorqueNm == 0 && light.state.engineHorsepower > 0, "Manual handover disconnects the shaft while engine rotation and combustion continue.");
            double disconnectedRpm = light.state.rpm;
            light.SetGear(0); light.Step(new EngineBenchControls { launchControls = false });
            check(light.state.rpm > disconnectedRpm - 100 && light.state.shiftRemaining == 0, "Selecting neutral releases handover load without a ratio-induced RPM reset.");
            foreach (var type in new[] { TransmissionType.Manual, TransmissionType.DCT, TransmissionType.Automatic })
            {
                var vehicle = spec.Copy(); vehicle.transmission.type = type;
                var car = new CarSimulation(vehicle, heavyTune, new DriverSettings { shifting = ShiftMode.Sequential, throttleAssist = false });
                car.Launch(0); car.state.rpm = 4500; car.state.speed = 4500 / FlywheelDynamics.RpmPerRadian * car.wheels.radius / (vehicle.gears[0] * heavyTune.finalDrive); RoadFixture.Seed(car, car.state.speed);
                double speed = car.state.speed;
                check(car.Shift(1, new DriverInput { throttle = 1 }) && car.state.rpm == 4500 && car.state.speed == speed, "Gear selection preserves crank and vehicle momentum: " + type);
                car.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
                check(car.state.rpm > 4000 && car.state.speed > speed * .98 && car.state.flywheelEnergyJ > 0, "First shift tick is finite with rolling momentum: " + type);
            }
            var config = new EngineBenchConfig { vehicle = spec, tune = lightTune };
            string first = Capture(config, EngineBenchScenario.FlywheelCoastDown);
            check(first == Capture(config, EngineBenchScenario.FlywheelCoastDown), "Neutral coast scenario reproduces every CSV sample exactly.");
            File.WriteAllText(Path.Combine(root, "reports/flywheel-coast-light.csv"), first);
            config.tune = heavyTune;
            File.WriteAllText(Path.Combine(root, "reports/flywheel-coast-heavy.csv"), Capture(config, EngineBenchScenario.FlywheelCoastDown));
            check(first != Capture(config, EngineBenchScenario.FlywheelCoastDown), "A/B flywheel change produces a different full coast trace.");
            first = Capture(config, EngineBenchScenario.DynamicPullAndShifts);
            check(first == Capture(config, EngineBenchScenario.DynamicPullAndShifts), "Dynamic pull, handovers and release repeat deterministically.");
            File.WriteAllText(Path.Combine(root, "reports/flywheel-dynamic-shifts.csv"), first);
            check(first.Contains("flywheel_energy_j") && first.Contains("total_engine_inertia_kg_m2"), "Dyno export includes inertia, energy and angular momentum.");
            var hatch = Array.Find(Resources.LoadAll<DragLab.Data.VehicleDefinition>("Data/Vehicles"), v => v.spec.id == "starter-hatch").spec;
            var boosted = new Tune { engineLevel = 1, gearboxLevel = 1, tireLevel = 2, inductionUpgrade = EngineUpgradeInduction.Turbo };
            var matched = hatch.Copy(); matched.clutchCapacityNm = (float)FlywheelDynamics.MatchedClutchCapacity(matched, boosted);
            double weakMin, strongMin, weakSlip, strongSlip;
            File.WriteAllText(Path.Combine(root, "reports/shift-stock-clutch.csv"), ShiftTrace(hatch, boosted, out weakMin, out weakSlip));
            File.WriteAllText(Path.Combine(root, "reports/shift-matched-clutch.csv"), ShiftTrace(matched, boosted, out strongMin, out strongSlip));
            report.AppendLine("Full gas 1→2 / ratio " + hatch.gears[0] + "→" + hatch.gears[1] + "; stock clutch " + (hatch.ClutchCapacity * 1.15).ToString("F1") + " Nm; matched " + (matched.ClutchCapacity * 1.15).ToString("F1") + " Nm.");
            report.AppendLine("Within 0.75 s: stock minimum " + weakMin.ToString("F0") + " RPM / final slip " + weakSlip.ToString("F0") + "; matched minimum " + strongMin.ToString("F0") + " RPM / final slip " + strongSlip.ToString("F0") + ".");
            check(strongMin < hatch.redlineRpm * .8 && strongSlip < 100, "Matched clutch produces a clear finite RPM drop and couples under uninterrupted full gas.");
            check(weakMin > strongMin + 500 && weakSlip > strongSlip + 500, "Under-rated clutch retains physical slip instead of disguising it as close gear ratios.");
            check(hatch.clutchCapacityNm == 0, "Sizing the fixture leaves the authored stock clutch unchanged.");
            File.WriteAllText(Path.Combine(root, "reports/flywheel-verification.txt"), report.AppendLine("PASS: " + count + " flywheel checks.").ToString());
            return count;
        }
        static string ShiftTrace(VehicleSpec spec, Tune tune, out double minRpm, out double slip)
        {
            var car = new CarSimulation(spec, tune, new DriverSettings { launch = LaunchMode.Manual, shifting = ShiftMode.Sequential, throttleAssist = false });
            var input = new DriverInput { throttle = 1 };
            // Build the turbo charge through the real staging dynamics, then start
            // both fixtures at identical crank/road speeds near first-gear redline.
            for (int i = 0; i < 360; i++) car.Stage(DragMath.FixedStep, input);
            car.Launch(0); car.state.rpm = spec.redlineRpm - 100;
            car.state.speed = car.state.rpm / FlywheelDynamics.RpmPerRadian * car.wheels.radius / (spec.gears[0] * car.tune.finalDrive); RoadFixture.Seed(car, car.state.speed);
            car.Shift(1, input); minRpm = car.state.rpm;
            var csv = new StringBuilder("time,gear,rpm,road_rpm,throttle,shaft_nm,slip_rpm\n");
            for (int i = 0; i < 90; i++)
            {
                car.Step(DragMath.FixedStep, input); minRpm = Math.Min(minRpm, car.state.rpm);
                double roadRpm = car.state.speed / car.wheels.radius * spec.gears[1] * car.tune.finalDrive * FlywheelDynamics.RpmPerRadian;
                csv.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:F4},{1},{2:F3},{3:F3},{4:F2},{5:F3},{6:F3}\n", (i + 1) * DragMath.FixedStep, car.state.gear, car.state.rpm, roadRpm, car.state.throttle, car.state.transmittedTorqueNm, car.state.clutchSlipRpm);
            }
            slip = car.state.clutchSlipRpm; return csv.ToString();
        }
        static string Capture(EngineBenchConfig config, EngineBenchScenario scenario)
        {
            var bench = new EngineTestBench(config.vehicle, config.tune); var dyno = new EngineBenchDyno(); dyno.Begin("flywheel", config, bench.state, 0);
            for (int i = 0; i < EngineBenchScenarios.DurationTicks(scenario); i++)
            { var c = EngineBenchScenarios.At(scenario, i, bench.spec); bench.Step(c); dyno.Sample(bench.state, c.load); }
            return dyno.Active.Csv();
        }
    }
}
