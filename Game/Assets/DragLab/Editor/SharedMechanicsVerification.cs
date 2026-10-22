using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Editor
{
    public static class SharedMechanicsVerification
    {
        public static void RunStandalone()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            Debug.Log("Shared mechanics checks: " + (Run(root) + BenchAbuseVerification.Run(root)));
        }
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            var vehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles"); var catalog = new MechanicalCatalog(vehicles.Select(v => v.spec));
            foreach (var v in vehicles)
            {
                var resolved = catalog.Resolve(v.spec.id, catalog.Recipe(v.spec.id), v.CreateStockTune());
                resolved.tune.transmissionUpgrade = TransmissionUpgrade.Manual;
                var driver = new DriverSettings(); driver.ApplyPreset(DrivingPreset.Manual);
                var start = new RunSnapshot { vehicle = resolved.spec, tune = resolved.tune, driving = driver };
                var bench = new PhysicalDrivingSession(start, false);
                var race = RaceSessionFactory.Create(start.vehicle, start.tune, driver, start.vehicle, start.tune, driver, 402.336);
                // Both modes consume the very same CarSimulation; countdown/finish belong
                // only to race orchestration. Compare from identical physical state.
                race.player.RestoreDynamics(bench.car.CaptureDynamics());
                foreach (var c in PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake, 1).Take(1000))
                { bench.Step(c); race.player.IndependentStep(DragMath.FixedStep, c.input, c.neutral, c.staging); }
                check(JsonUtility.ToJson(bench.car.CaptureDynamics()) == JsonUtility.ToJson(race.player.CaptureDynamics()), "Race construction and unrecorded bench share identical physical/condition behavior: " + v.spec.id);
                var hot = bench.car.mechanics.parts.Find(p => p.slot == "Clutch"); hot.condition.health = .4f; hot.condition.temperatureC = 350; bench.car.mechanics.Prepare(bench.car.state);
                check(bench.car.state.clutchCapacityFactor < .4, "Assigned clutch condition and heat reduce actual capacity: " + v.spec.id);
                double speed = bench.car.state.speed; bench.car.mechanics.ResetWear(bench.car.state);
                check(bench.car.state.speed == speed && Math.Abs(bench.car.state.clutchCapacityFactor-ClutchHardware.Capacity(bench.car.mechanics.Definition(hot),bench.car.mechanics.Part(PartCategory.Clutch).condition))<1e-9 && bench.car.mechanics.parts.All(p => p.condition.health == 1 && !p.condition.failed), "Isolated repair clears wear/heat while retaining clutch fluid condition and road momentum: " + v.spec.id);
                // A failure fixture needs sufficiently little remaining life for
                // every stock powertrain to deplete within this short timeline.
                var weak = new PhysicalDrivingSession(start, false); weak.car.mechanics.parts.Find(p => p.slot == "Driveshaft").condition.health = .000001f;
                foreach (var c in PhysicalScenarios.MoneyShiftTimeline(start).Take(600)) { weak.Step(c); if (weak.car.state.drivetrainFailed) break; }
                var shaft = weak.car.mechanics.parts.Find(p => p.slot == "Driveshaft");
                check(shaft.condition.failed && shaft.condition.failureReason == "Torque overload" && weak.car.state.drivetrainFailed && !weak.car.state.engineFailed && weak.car.state.transmittedTorqueNm == 0, "Weak driveshaft breaks from real transmitted torque; engine continues but wheel torque stops: " + v.spec.id + " / health " + shaft.condition.health + " / cause " + shaft.condition.failureReason + " / engine failed " + weak.car.state.engineFailed);
                check(!bench.car.state.drivetrainFailed, "Healthy stock shaft survives the same controls: " + v.spec.id);
            }
            var spec = vehicles.First(v => v.spec.id == "starter-hatch").spec;
            var tune = new Tune { partsResolved = true, transmissionUpgrade = TransmissionUpgrade.Manual, finalDrive = spec.finalDrive };
            var manual = new DriverSettings(); manual.ApplyPreset(DrivingPreset.Manual);
            var slip = new PhysicalDrivingSession(new RunSnapshot { vehicle = spec.Copy(), tune = tune, driving = manual }, false);
            for (int i = 0; i < 7200; i++) slip.Step(new TickInput { tick = i, neutral = i < 240, staging = i < 240, input = new DriverInput { throttle = 1, clutch = i <= 240 ? 1 : .6, brake = 1 } });
            var clutch = slip.car.mechanics.parts.Find(p => p.slot == "Clutch");
            check(clutch.condition.slipEnergyJ > 100000 && clutch.condition.temperatureC > 50 && clutch.condition.health < .99f, "Actual sustained clutch slip dissipates energy, heats and wears the clutch: " + clutch.condition.slipEnergyJ + " J / " + clutch.condition.temperatureC + " C / " + clutch.condition.health);
            var converter = new PhysicalDrivingSession(new RunSnapshot { vehicle = spec.Copy(), tune = new Tune { partsResolved = true, transmissionUpgrade = TransmissionUpgrade.Automatic }, driving = new DriverSettings { throttleAssist = false } }, false);
            foreach (var c in PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake, 1)) converter.Step(c);
            check(converter.car.mechanics.parts.Find(p => p.slot == "Clutch").condition.slipEnergyJ == 0 && converter.car.mechanics.parts.Find(p => p.slot == "Gearbox").condition.temperatureC > 20, "Torque converter generates gearbox heat rather than fictitious friction-clutch wear.");
            var endless = new PhysicalDrivingSession(new RunSnapshot { vehicle = spec.Copy(), tune = new Tune(), driving = new DriverSettings() }, false);
            for (int i = 0; i < 73000; i++) endless.Step(new TickInput { tick = i, neutral = true, input = new DriverInput { throttle = .2 } });
            check(endless.Tick == 73000 && endless.snapshot.inputs.Count == 0 && endless.samples.Count == 1 && endless.LastInput.throttle == .2, "Recording off runs past ten minutes with bounded telemetry and no recorded inputs.");
            var record = endless.ContinueRecording(); for (int i = 0; i < 60; i++) record.Step(new TickInput { tick = i, neutral = true, input = new DriverInput { throttle = .2 } });
            check(record.TotalTick == 73060 && record.Csv() == PhysicalDrivingSession.Replay(record.snapshot).Csv(), "Enabling recording captures current dynamics and wear rather than restarting simulation.");
            var profile = PlayerProfile.New(DateTime.UtcNow.Ticks); MechanicsOwnership.Ensure(profile, catalog);
            var originalIds = profile.parts.Where(p => p.slot != "Driveshaft").Select(p => p.instanceId).ToArray();
            var oldShaft = profile.parts.Find(p => p.slot == "Driveshaft"); profile.parts.Remove(oldShaft); profile.SelectedCar.installed.slots.RemoveAll(s => s.slot == "Driveshaft"); profile.saveVersion = 2; profile.drivelineVersion = 0;
            MechanicsOwnership.Ensure(profile, catalog);
            check(profile.parts.Count(p => p.slot == "Driveshaft") == 1 && originalIds.All(id => profile.parts.Any(p => p.instanceId == id)), "Additive v2 migration adds one shaft while preserving existing ownership IDs.");
            MechanicsOwnership.Remove(profile, profile.SelectedCar, "Driveshaft"); MechanicsOwnership.Ensure(profile, catalog);
            var missingShaft = MechanicsOwnership.Resolve(profile, profile.SelectedCar, catalog);
            check(!profile.SelectedCar.installed.slots.Any(s => s.slot == "Driveshaft") && new CarSimulation(missingShaft.spec, missingShaft.tune, new DriverSettings(), profile.parts.Where(p => p.ownerId == profile.SelectedCar.instanceId || p.ownerId == profile.SelectedCar.engineAssemblyId)).state.drivetrainFailed, "Migration does not respawn a removed shaft; its absence disconnects drive without rejecting a running engine.");
            string folder = Path.Combine(root, "reports/shared-mechanics"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "verification.txt"), log + "Checks: " + count + "\n");
            File.WriteAllText(Path.Combine(folder, "clutch-slip-end-state.json"), JsonUtility.ToJson(slip.car.CaptureDynamics(), true)); return count + SimulationRulesVerification.Run(root);
        }
    }
}
