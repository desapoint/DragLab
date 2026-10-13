using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Runtime;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class SimulationRulesVerification
    {
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            var spec = Resources.LoadAll<VehicleDefinition>("Data/Vehicles").First(v => v.spec.id == "starter-hatch").spec.Copy();
            spec.transmission.type = TransmissionType.Manual;
            var drivers = new[] { new DriverSettings { wearMode = WearMode.None }, new DriverSettings { wearMode = WearMode.Partial }, new DriverSettings(), new DriverSettings { wearMultiplier = 10 } };
            var cars = drivers.Select(d => new CarSimulation(spec.Copy(), new Tune(), d)).ToArray();
            foreach (var car in cars)
            {
                car.state.gear = 1; car.state.rpm = 4500; car.state.engineTorqueNm = 300; car.state.engineHorsepower = 150; car.state.transmittedTorqueNm = 200; car.state.clutchFrictionPowerW = 200 * 2000 / FlywheelDynamics.RpmPerRadian;
                car.mechanics.Tick(car, .1, 2000, 0);
            }
            double[] loss = cars.Select(c => 1.0 - c.mechanics.parts.First(p => p.slot == "Clutch").condition.health).ToArray();
            check(loss[0] == 0 && loss[2] > 0 && Math.Abs(loss[1] - loss[2] * .25) < 1e-7 && Math.Abs(loss[3] - loss[2] * 10) < 1e-6, "Same clutch slip work produces None=0, Partial=25%, Full=100%, multiplier=10x health loss within float condition precision.");
            check(cars.Select(c => c.mechanics.parts.First(p => p.slot == "Clutch").condition.temperatureC).Distinct().Count() == 1, "Wear rate does not independently change physical friction heat.");
            foreach (var car in cars) { car.state.rpm = spec.redlineRpm * 1.4; EngineBehaviorDynamics.FinishTick(spec, car.engine, car.state, false, .1); }
            double[] damage = cars.Select(c => c.state.engineDamage).ToArray();
            check(damage[0] == 0 && damage[2] > 0 && Math.Abs(damage[1] / damage[2] - .25) < 1e-9 && Math.Abs(damage[3] / damage[2] - 10) < 1e-9, "Overrev abuse uses the same None/Partial/Full and multiplier rules.");
            var partial = new CarSimulation(spec.Copy(), new Tune(), new DriverSettings { wearMode = WearMode.Partial });
            var weak = partial.mechanics.parts.First(p => p.slot == "Driveshaft"); weak.condition.health = .001f;
            partial.state.gear = 1; partial.state.transmittedTorqueNm = 2000;
            partial.mechanics.Tick(partial, .1, 0, 0);
            check(weak.condition.failed && partial.state.drivetrainFailed, "Partial wear still permits a worn shaft to break under torque abuse.");
            cars[0].mechanics.parts.First(p => p.slot == "Driveshaft").condition.failed = true; cars[0].mechanics.Prepare(cars[0].state);
            check(cars[0].state.drivetrainFailed, "None wear does not heal an existing failed component.");
            var optimal = new CarSimulation(spec.Copy(), new Tune { coldStart = true }, new DriverSettings { optimalThermals = true });
            var clutch = optimal.mechanics.parts.First(p => p.slot == "Clutch"); clutch.condition.temperatureC = 500;
            optimal.IndependentStep(DragMath.FixedStep, new DriverInput { throttle = .5 }, true, false);
            check(optimal.state.engineTemperature == 1 && optimal.state.exhaustHeat == 0 && clutch.condition.temperatureC == 60 && optimal.state.clutchCapacityFactor == 1, "Optimal thermals initializes warm and removes clutch heat fade through the shared model.");
            optimal.state.rpm = 4500; optimal.state.turboBoostBar = .7;
            InductionDynamics.UpdateOutput(optimal.induction, optimal.state, 1, false);
            var normal = new CarSimulation(spec.Copy(), new Tune(), new DriverSettings()); normal.state.rpm = 4500; normal.state.turboBoostBar = .7;
            InductionDynamics.UpdateOutput(normal.induction, normal.state, 1, false);
            check(optimal.state.chargeTemperatureK == optimal.induction.data.ambientTemperatureK && normal.state.chargeTemperatureK > optimal.state.chargeTemperatureK && optimal.state.engineTorqueNm > normal.state.engineTorqueNm, "Optimal charge temperatures affect actual torque as well as telemetry.");
            var manual = new DriverSettings { shifting = ShiftMode.ManualClutch, launch = LaunchMode.Manual, throttleAssist = false };
            var assist = manual.Copy(); assist.clutchAssist = true;
            var gearboxSpec = spec.Copy(); gearboxSpec.transmission.type = TransmissionType.Manual;
            var plainCar = new CarSimulation(gearboxSpec, new Tune(), manual); var assistedCar = new CarSimulation(gearboxSpec, new Tune(), assist);
            foreach (var car in new[] { plainCar, assistedCar }) { car.state.launched = true; car.state.gear = 1; car.state.rpm = 3000; }
            check(!plainCar.Shift(1, new DriverInput { throttle = 1 }) && assistedCar.Shift(1, new DriverInput { throttle = 1 }), "Clutch assist allows manual shift requests without a clutch pedal; hardware checks still apply.");
            var stall = new CarState { rpm = 0, clutch = 0 }; var prevent = new CarState { rpm = 0, clutch = 0, stallPrevention = true };
            EngineBehaviorDynamics.FinishTick(spec, normal.engine, stall, true, DragMath.FixedStep); EngineBehaviorDynamics.FinishTick(spec, normal.engine, prevent, true, DragMath.FixedStep);
            check(stall.stalled && !prevent.stalled, "Stall prevention can be selected independently of wear and thermal settings.");
            foreach (var preset in new[] { SimulationDifficulty.Arcade, SimulationDifficulty.Balanced, SimulationDifficulty.Simulation })
            {
                var d = manual.Copy(); d.soundVolume = .63f; d.clutchReleaseSeconds = .27f; d.ApplySimulationPreset(preset);
                check(d.shifting == manual.shifting && d.launch == manual.launch && !d.throttleAssist && d.soundVolume == .63f && d.clutchReleaseSeconds == .27f, "Simulation preset preserves chosen driving modes and other preferences: " + preset);
                check(d.wearMode == (preset == SimulationDifficulty.Arcade ? WearMode.None : preset == SimulationDifficulty.Balanced ? WearMode.Partial : WearMode.Full) && d.optimalThermals == (preset == SimulationDifficulty.Arcade) && d.clutchAssist == (preset != SimulationDifficulty.Simulation), "Preset sets coherent wear/thermal/clutch rules: " + preset);
                var data = new RunSnapshot { vehicle = gearboxSpec.Copy(), tune = new Tune(), driving = d };
                var session = new PhysicalDrivingSession(data); foreach (var input in PhysicalScenarios.Timeline(PhysicalScenario.NeutralFlywheel, 1).Take(480)) session.Step(input);
                var json = JsonUtility.ToJson(session.snapshot); var parsed = JsonUtility.FromJson<RunSnapshot>(json);
                check(PhysicalDrivingSession.Replay(parsed).Csv() == session.Csv(), "Captured simulation preset replays exactly after JSON round trip: " + preset);
            }
            var profile = PlayerProfile.New(DateTime.UtcNow.Ticks); profile.driving.ApplySimulationPreset(SimulationDifficulty.Balanced); profile.driving.optimalThermals = true; profile.difficulty = 2;
            var fresh = profile.RestartKeepingPreferences(DateTime.UtcNow.Ticks);
            check(JsonUtility.ToJson(fresh.driving) == JsonUtility.ToJson(profile.driving) && fresh.difficulty == 2, "Game reset retains new simulation preferences and independent AI difficulty.");
            profile.driving.wearMultiplier = float.NaN; profile.driving.wearMode = (WearMode)99; profile.Sanitize(DateTime.UtcNow.Ticks);
            check(profile.driving.wearMode == WearMode.Full && profile.driving.wearMultiplier == 1, "Invalid save wear rules sanitize to finite full-rate defaults.");
            var invalidCapture = new RunSnapshot { vehicle = spec.Copy(), tune = new Tune(), driving = new DriverSettings { wearMultiplier = float.NaN } };
            check(PhysicalDrivingSession.Validate(invalidCapture) != null, "Replay rejects nonfinite simulation rules rather than corrupting dynamics.");
            string previousPath = ProfileStore.VerificationPath;
            try
            {
                string fixture = Path.Combine(root, "reports/simulation-rules/legacy-settings"); Directory.CreateDirectory(fixture);
                ProfileStore.VerificationPath = Path.Combine(fixture, "profile-v1.json");
                foreach (string suffix in new[] { "", ".bak" }) if (File.Exists(ProfileStore.FilePath + suffix)) File.Delete(ProfileStore.FilePath + suffix);
                var legacy = PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(legacy,new MechanicalCatalog(Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Select(v=>v.spec))); string oldId = legacy.SelectedCar.instanceId;
                string oldJson = JsonUtility.ToJson(legacy);
                foreach (string field in new[] { "wearMode", "wearMultiplier", "simulationDifficulty", "optimalThermals", "clutchAssist", "stallPrevention" })
                    oldJson = System.Text.RegularExpressions.Regex.Replace(oldJson, ",?\"" + field + "\":[^,}]+", "");
                File.WriteAllText(ProfileStore.FilePath, oldJson); var migrated = ProfileStore.Load();
                check(migrated.SelectedCar.instanceId == oldId && migrated.driving.wearMode == WearMode.Full && migrated.driving.wearMultiplier == 1 && !migrated.driving.optimalThermals && !migrated.driving.clutchAssist && !migrated.driving.stallPrevention, "Old saves without new settings retain ownership and the pre-change full-wear thermal behavior.");
            }
            finally { ProfileStore.VerificationPath = previousPath; }
            var points = Enumerable.Range(0, 1000).Select(i => new Vector2(i, 0)).ToArray();
            points[301] = new Vector2(301, 500); points[302] = new Vector2(302, -100); points[303] = new Vector2(-20, 0); points[999] = new Vector2(999, 0);
            var indices = BenchPlotLines.ReducedIndices(points.Length, 10, i => points[i]);
            check(indices.Contains(301) && indices.Contains(302) && indices.Contains(303) && indices[0] == 0 && indices.Last() == 999 && indices.SequenceEqual(indices.OrderBy(i => i).Distinct()), "Plot reduction preserves brief spikes, negative torque, RPM reversals, chronology and final zero tail.");
            string folder = Path.Combine(root, "reports/simulation-rules"); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "verification.txt"), log + "Checks: " + count + "\n");
            return count;
        }
    }
}
