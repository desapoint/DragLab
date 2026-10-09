using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class PhysicalBenchVerification
    {
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            string folder = Path.Combine(root, "reports/phases/2e"); Directory.CreateDirectory(folder);
            foreach (var definition in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
            {
                var catalog = MechanicsTransactions.Catalog(); var profile = PlayerProfile.New(DateTime.UtcNow.Ticks);
                profile.cars.Clear(); profile.cars.Add(new OwnedCar { vehicleId = definition.spec.id, tune = definition.CreateStockTune() }); profile.selectedVehicleId = definition.spec.id;
                profile.driving = new DriverSettings { launch = LaunchMode.Assisted, shifting = ShiftMode.Sequential, throttleAssist = false };
                MechanicsOwnership.Ensure(profile, catalog);
                string before = JsonUtility.ToJson(profile); var capture = PhysicalBenchTransfer.Capture(profile, profile.SelectedCar, 173);
                var session = new PhysicalDrivingSession(capture); foreach (var input in PhysicalScenarios.Timeline(PhysicalScenario.FullPullShiftCoastBrake, capture.seed)) session.Step(input);
                var replay = PhysicalDrivingSession.Replay(session.snapshot);
                File.WriteAllText(Path.Combine(folder, definition.spec.id + "-trace.csv"), session.Csv());
                check(session.Csv() == replay.Csv(), "Every 120 Hz sample repeats exactly in memory: " + definition.spec.id);
                check(session.samples.Count == PhysicalScenarios.Ticks + 1 && session.samples.Select((s, i) => s.tick == i && Math.Abs(s.time - i / 120.0) < 1e-12).All(ok => ok), "Trace retains every chronological tick from idle through braking: " + definition.spec.id);
                check(session.samples.Any(s => s.segment == RunSegment.Staging) && session.samples.Any(s => s.segment == RunSegment.Neutral && s.speedKmh > 1) && session.samples.Any(s => s.segment == RunSegment.Shutdown) && session.samples.Any(s => s.gear >= 2), "Full timeline includes staged revs, shifts, release and neutral coasting: " + definition.spec.id);
                check(session.samples.All(s => s.rpm >= definition.spec.idleRpm * .85 && !s.failed && !s.stalled), "Healthy RPM retains its authored rough-idle band and never resets to zero during shifts: " + definition.spec.id + " / min " + session.samples.Min(s => s.rpm).ToString("R"));
                check(session.samples.Any(s => s.shifting && s.throttle == 1 && s.hp > 0) || session.car.transmission.type == TransmissionType.CVT, "Shift preserves gas and combustion output: " + definition.spec.id);
                check(session.samples[1321].distance > session.samples[1320].distance && session.samples[1321].speedKmh > 1 && session.samples.Last().brake == 1 && session.samples.Last().speedKmh < session.samples[1560].speedKmh, "Selecting neutral preserves momentum; later pedal braking decelerates: " + definition.spec.id);
                check(before == JsonUtility.ToJson(profile), "Bench ticks do not alter owned parts, fuel, cash or records: " + definition.spec.id);
                var decoded = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.snapshot)); var diskReplay = PhysicalDrivingSession.Replay(decoded);
                File.WriteAllText(Path.Combine(folder, definition.spec.id + "-roundtrip-original.json"), JsonUtility.ToJson(session.snapshot, true)); File.WriteAllText(Path.Combine(folder, definition.spec.id + "-roundtrip-parsed.json"), JsonUtility.ToJson(decoded, true));
                File.WriteAllText(Path.Combine(folder, definition.spec.id + "-roundtrip-trace.csv"), diskReplay.Csv());
                check(Math.Abs(diskReplay.car.state.distance - session.car.state.distance) < 1e-8 && Math.Abs(diskReplay.car.state.rpm - session.car.state.rpm) < 1e-8, "Exported JSON replays within serialization precision: " + definition.spec.id + " / distance " + session.car.state.distance.ToString("R") + " -> " + diskReplay.car.state.distance.ToString("R") + " / RPM " + session.car.state.rpm.ToString("R") + " -> " + diskReplay.car.state.rpm.ToString("R"));
                File.WriteAllText(Path.Combine(folder, definition.spec.id + "-trace.csv"), session.Csv()); File.WriteAllText(Path.Combine(folder, definition.spec.id + "-replay.json"), JsonUtility.ToJson(session.snapshot, true));
                capture.components[0].condition.health = .12f; capture.vehicle.gears[0] = 5;
                check(session.snapshot.components[0].condition.health == 1 && session.snapshot.vehicle.gears[0] != 5, "Captured build/spec/conditions are deeply isolated: " + definition.spec.id);
            }
            var sample = new RunSnapshot { vehicle = new VehicleSpec(), tune = new Tune(), driving = new DriverSettings { launch = LaunchMode.Assisted, shifting = ShiftMode.Sequential, throttleAssist = false } };
            var neutral = new PhysicalDrivingSession(sample); foreach (var input in PhysicalScenarios.Timeline(PhysicalScenario.NeutralFlywheel, 1)) neutral.Step(input);
            check(neutral.samples[360].rpm > neutral.samples[480].rpm && neutral.car.state.speed == 0 && Math.Abs(neutral.car.state.rpm - neutral.car.spec.idleRpm) < neutral.car.spec.idleRpm * .05, "Neutral release drops RPM through calculated flywheel inertia and settles in the idle band.");
            var seedsA = PhysicalScenarios.Timeline(PhysicalScenario.SeededDriver, 123); var seedsB = PhysicalScenarios.Timeline(PhysicalScenario.SeededDriver, 123); var seedsC = PhysicalScenarios.Timeline(PhysicalScenario.SeededDriver, 124);
            check(seedsA.Select((s, i) => s.input.throttle == seedsB[i].input.throttle && s.input.shiftUp == seedsB[i].input.shiftUp).All(x => x) && seedsA[300].input.throttle != seedsC[300].input.throttle, "Seeded provider repeats controls and changes them only with seed.");
            sample.inputs.Add(new TickInput { tick = 3 }); check(PhysicalDrivingSession.Validate(sample) != null, "Nonchronological imported input rejected.");
            sample.inputs[0].tick = 0; sample.inputs[0].input.throttle = double.NaN; check(PhysicalDrivingSession.Validate(sample) != null, "Nonfinite imported pedal rejected.");
            sample.model = "unknown-physics"; check(PhysicalDrivingSession.Validate(sample) != null, "Wrong model version rejected instead of silently replaying another physics model.");
            sample.model = MechanicsSchema.Model; sample.inputs.Clear(); sample.tune.partsResolved = true; sample.tune.partMass = 0;
            check(PhysicalDrivingSession.Validate(sample) != null, "Invalid installed mass multiplier rejected before starting imported physics.");
            sample.tune = new Tune(); var endless = new PhysicalDrivingSession(sample);
            for (int tick = 0; tick < 3600; tick++) endless.Step(new TickInput { tick = tick, input = new DriverInput { throttle = 1, shiftUp = tick == 600 || tick == 960 || tick == 1320 || tick == 1680 } });
            check(endless.car.state.distance > 402.336 && !endless.car.state.finished && endless.samples.Count == 3601, "Physical driving continues beyond quarter mile without a race finish or truncated trace.");
            File.WriteAllText(Path.Combine(folder, "physical-verification.txt"), log + "Checks: " + count + "\n"); return count;
        }
    }
}
