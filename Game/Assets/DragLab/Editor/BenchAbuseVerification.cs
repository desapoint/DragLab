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
    public static class BenchAbuseVerification
    {
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            string folder = Path.Combine(root, "reports/bench-abuse"); Directory.CreateDirectory(folder);
            foreach (var definition in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
            {
                var start = new RunSnapshot { vehicle = definition.spec.Copy(), tune = definition.CreateStockTune(), driving = new DriverSettings() };
                start.tune.partsResolved = true; start.tune.transmissionUpgrade = TransmissionUpgrade.Manual; start.driving.ApplyPreset(DrivingPreset.Manual); start.driving.clutchReleaseSeconds = .08f;
                var commands = PhysicalScenarios.MoneyShiftTimeline(start); var run = new PhysicalDrivingSession(start);
                foreach (var command in commands) run.Step(command);
                File.WriteAllText(Path.Combine(folder, start.vehicle.id + "-failure.csv"), run.Csv());
                File.WriteAllText(Path.Combine(folder, start.vehicle.id + "-failure.json"), JsonUtility.ToJson(run.snapshot, true));
                check(commands.Any(c => c.input.shiftDown) && run.car.state.engineFailed, "Ordinary pedals and manual downshift cause engine failure: " + start.vehicle.id + " / peak " + run.samples.Max(s => s.rpm));
                int down = commands.FindIndex(c => c.input.shiftDown);
                check(!run.samples[down + 1].failed && run.samples.Skip(down + 1).Any(s => s.rpm > start.vehicle.redlineRpm * run.car.engine.data.safeRpmMultiplier && s.throttle == 0), "Selecting gear does not fake failure; clutch back-drive overrevs with gas released: " + start.vehicle.id);
                check(run.Csv() == PhysicalDrivingSession.Replay(run.snapshot).Csv(), "Destructive trace repeats every sample exactly: " + start.vehicle.id);
                var disk = PhysicalDrivingSession.Replay(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(run.snapshot)));
                if (!disk.car.state.engineFailed || Math.Abs(disk.car.state.distance - run.car.state.distance) >= 1e-7) { File.WriteAllText(Path.Combine(folder, "replay-original.json"), JsonUtility.ToJson(run.car.CaptureDynamics(),true)); File.WriteAllText(Path.Combine(folder, "replay-decoded.json"), JsonUtility.ToJson(disk.car.CaptureDynamics(),true)); File.WriteAllText(Path.Combine(folder, "replay-decoded-start.json"), JsonUtility.ToJson(disk.snapshot,true)); }
                check(disk.car.state.engineFailed && Math.Abs(disk.car.state.distance - run.car.state.distance) < 1e-7, "JSON replays actual failure: " + start.vehicle.id);
                start.vehicle.transmission.manualDownshiftInterlock = true; var safe = new PhysicalDrivingSession(start); foreach (var command in commands) safe.Step(command);
                check(!safe.car.state.engineFailed, "Installed manual interlock rejects same destructive inputs: " + start.vehicle.id);
                var failed = run.ContinueRecording(); double damage = failed.car.state.engineDamage, speed = failed.car.state.speed;
                for (int i = 0; i < 120; i++) failed.Step(new TickInput { tick = i, neutral = true, input = new DriverInput { brake = 1, launch = true } });
                check(failed.car.state.engineFailed && failed.car.state.engineDamage >= damage && failed.car.state.speed < speed, "Failed engine cannot be healed by restart; neutral coasting/braking continues: " + start.vehicle.id);
                check(!new PhysicalDrivingSession(start).car.state.engineFailed, "Fresh isolated reset creates healthy engine: " + start.vehicle.id);
                // Split during a shift, while loaded, and while coasting. The next samples
                // must match unsplit physics, including all private induction/clutch flags.
                start.vehicle.transmission.manualDownshiftInterlock = false;
                foreach (int split in new[] { 250, down + 2, commands.Count - 1 })
                {
                    var prefix = new PhysicalDrivingSession(start); foreach (var c in commands.Take(split)) prefix.Step(c);
                    var segment = prefix.ContinueRecording();
                    foreach (var c in commands.Skip(split)) segment.Step(new TickInput { tick = segment.Tick, input = c.input, neutral = c.neutral, staging = c.staging });
                    check(JsonUtility.ToJson(segment.car.CaptureDynamics()) == JsonUtility.ToJson(run.car.CaptureDynamics()) && segment.TotalTick == run.TotalTick, "Checkpoint continuation preserves complete dynamics at tick " + split + ": " + start.vehicle.id);
                    check(segment.Csv() == PhysicalDrivingSession.Replay(segment.snapshot).Csv(), "Continuation snapshot replays independently: " + split + " / " + start.vehicle.id);
                    var decoded = PhysicalDrivingSession.Replay(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(segment.snapshot)));
                    check(Math.Abs(decoded.car.state.rpm - segment.car.state.rpm) < 1e-6 && Math.Abs(decoded.car.state.distance - segment.car.state.distance) < 1e-6, "Serialized continuation retains physical state: " + split + " / " + start.vehicle.id);
                }
            }
            var fixture = new RunSnapshot { vehicle = new VehicleSpec(), tune = new Tune(), driving = new DriverSettings() };
            var endless = new PhysicalDrivingSession(fixture);
            for (int i = 0; i < PhysicalDrivingSession.MaximumTicks; i++) endless.Step(new TickInput { tick = i, neutral = true, input = new DriverInput { throttle = .2 } });
            var next = endless.ContinueRecording(); double rpm = next.car.state.rpm;
            next.Step(new TickInput { tick = 0, neutral = true, input = new DriverInput { throttle = .2 } });
            check(next.TotalTick == 72001 && next.samples[0].tick == 72000 && Math.Abs(next.car.state.rpm - rpm) < 5, "Driving continues beyond ten-minute recording boundary without resetting physics/time.");
            check(next.snapshot.startingDynamics.state != endless.car.state && next.Csv() == PhysicalDrivingSession.Replay(next.snapshot).Csv(), "Continuation is deeply isolated and independently replayable.");
            var invalid = PhysicalDrivingSession.Copy(next.snapshot); invalid.startingDynamics.state.rpm = double.NaN;
            check(PhysicalDrivingSession.Validate(invalid) != null, "Nonfinite checkpoint rejected.");
            invalid = PhysicalDrivingSession.Copy(next.snapshot); invalid.startingDynamics = null;
            check(PhysicalDrivingSession.Validate(invalid) != null, "Missing continuation state rejected.");
            check(PhysicalDrivingSession.Validate(fixture) == null, "Original snapshots with no checkpoint remain compatible.");
            foreach (var type in new[] { TransmissionUpgrade.DCT, TransmissionUpgrade.Automatic })
            {
                var car = new CarSimulation(fixture.vehicle, new Tune { partsResolved = true, transmissionUpgrade = type }, new DriverSettings { shifting = ShiftMode.ManualClutch });
                car.state.launched = true; car.state.gear = 2; car.state.rpm = car.spec.redlineRpm; RoadFixture.Seed(car, car.state.rpm / FlywheelDynamics.RpmPerRadian * car.wheels.radius / (car.spec.gears[1] * car.tune.finalDrive));
                check(!car.Shift(-1, new DriverInput { clutch = 1 }), "Shared hardware retains " + type + " transmission protection.");
            }
            File.WriteAllText(Path.Combine(folder, "verification.txt"), log + "Checks: " + count + "\n"); return count;
        }
    }
}
