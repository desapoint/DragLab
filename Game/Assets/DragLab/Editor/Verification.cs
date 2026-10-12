using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Presentation;
using DragLab.Runtime;
using DragLab.Domain.Mechanics;
using System.Linq;

namespace DragLab.Editor
{
    public static class Verification
    {
        [MenuItem("DragLab/Verify Foundation")]
        public static void Run()
        {
            int count = 0;
            var cars = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            count += StockDurabilityVerification.Run(Path.GetFullPath(Path.Combine(Application.dataPath, "../..")));
            Require(cars.Length >= 8, "Expanded vehicle definitions exist.", ref count);
            foreach (var definition in cars)
            {
                Require(definition.spec.Validate() == null, "Vehicle validates: " + definition.spec.id, ref count);
                Require(Resources.Load<Sprite>("Sprites/Vehicles/" + definition.spec.artId + "/wheel") != null, "Sprite imports as Sprite: " + definition.spec.artId, ref count);
                var stock = Benchmark.QuarterMile(definition.spec, definition.CreateStockTune());
                Require(stock.elapsed > 8 && stock.elapsed < 30, "Benchmark has plausible bounds: " + definition.spec.id, ref count);
                var tune = definition.CreateStockTune(); tune.engineLevel = 1;
                var upgraded = Benchmark.QuarterMile(definition.spec, tune);
                if (upgraded.elapsed >= stock.elapsed)
                {
                    var probe = new CarSimulation(definition.spec, tune, new DriverSettings());
                    var diagnosticDuel = new RaceSimulation(probe, new CarSimulation(definition.spec, definition.CreateStockTune(), new DriverSettings()), 402.336);
                    while (diagnosticDuel.phase != RacePhase.Finished) diagnosticDuel.Step(new DriverInput());
                    File.WriteAllText(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "../..")), "reports/flywheel-upgrade-diagnostic.csv"), diagnosticDuel.telemetry.Csv());
                }
                Require(upgraded.elapsed < stock.elapsed, "Power upgrade improves assisted quarter mile: " + definition.spec.id + " / stock " + stock.elapsed + " / upgraded " + upgraded.elapsed, ref count);
                var car = new CarSimulation(definition.spec, definition.CreateStockTune(), new DriverSettings());
                Require(Math.Abs(DragMath.Horsepower(definition.spec, car.tune, 5000) - DragMath.Torque(definition.spec, car.tune, 5000) * 5000 / 7127) < .0001, "HP derives from torque: " + definition.spec.id, ref count);
                Debug.Log("BENCHMARK " + definition.spec.id + " " + stock.elapsed.ToString("F3") + "s " + stock.trapKmh.ToString("F1") + "km/h " + stock.performanceClass);
                var repeat = Benchmark.QuarterMile(definition.spec, definition.CreateStockTune());
                Require(repeat.elapsed == stock.elapsed && repeat.trapKmh == stock.trapKmh, "Benchmark is deterministic: " + definition.spec.id, ref count);
                var view = new GameObject("Sprite validation").AddComponent<LayeredCarView>();
                try
                {
                    view.Build(definition.spec.artId, Color.white);
                    view.Animate(new CarState { frontWheelAngleDegrees = 90, rearWheelAngleDegrees = 45 });
                    Transform fw = view.transform.Find("front-wheel"), fr = view.transform.Find("front-rotor"), fc = view.transform.Find("front-caliper");
                    Require(Quaternion.Angle(fw.localRotation, fr.localRotation) < .001f, "Front rotor shares wheel rotation: " + definition.spec.id, ref count);
                    Require(Quaternion.Angle(fc.localRotation, Quaternion.identity) < .001f, "Caliper stays stationary: " + definition.spec.id, ref count);
                    Require(Quaternion.Angle(fw.localRotation, view.transform.Find("rear-wheel").localRotation) > 40, "Axles animate independently: " + definition.spec.id, ref count);
                    Require(view.transform.Find("body-paint").GetComponent<SpriteRenderer>().sortingOrder > fw.GetComponent<SpriteRenderer>().sortingOrder,
                        "Body/fender draws over wheels: " + definition.spec.id, ref count);
                }
                finally { UnityEngine.Object.DestroyImmediate(view.gameObject); }
            }
            var p = PlayerProfile.New(DateTime.UtcNow.Ticks);
            long now = p.idleClaimUtcTicks;
            Require(p.PendingIdle(now + TimeSpan.TicksPerHour * 20) == 800, "Offline revenue is capped at eight hours.", ref count);
            Require(p.PendingIdle(now - TimeSpan.TicksPerHour) == 0, "Clock rollback never pays negative earnings.", ref count);
            p.ClaimIdle(now + TimeSpan.TicksPerHour * 20);
            Require(p.ClaimIdle(now + TimeSpan.TicksPerHour * 20) == 0, "An idle claim cannot be paid twice.", ref count);
            p.AwardRace(true, "one", 100, 40, 2); p.AwardRace(true, "one", 100, 40, 2);
            Require(p.reputation == 2, "Repeat career wins do not duplicate reputation.", ref count);
            var v = cars[0];
            var race = new RaceSimulation(new CarSimulation(v.spec, v.CreateStockTune(), new DriverSettings { launch = LaunchMode.Manual }),
                new CarSimulation(v.spec, v.CreateStockTune(), new DriverSettings()), 402.336);
            race.Step(new DriverInput { launch = true });
            Require(race.player.state.disqualified, "Early manual launch is a red light.", ref count);
            Require(race.player.state.gear == 0 && race.player.state.stagingBrakesHeld && race.player.state.brake == 1,
                "Red-light rejection leaves the staged car in neutral with brakes held.", ref count);
            var sameSpec = new VehicleSpec();
            var duel = new RaceSimulation(new CarSimulation(sameSpec, new Tune(), new DriverSettings { reactionSeconds = .1 }),
                new CarSimulation(sameSpec, new Tune(), new DriverSettings { reactionSeconds = .3 }), 402.336);
            while (duel.phase != RacePhase.Finished) duel.Step(new DriverInput());
            Require(duel.playerWon, "Reaction advantage wins equal builds.", ref count);
            Require(Math.Abs(duel.player.state.elapsed - duel.opponent.state.elapsed) < .02, "Reaction is separate from elapsed time.", ref count);
            Require(duel.player.state.splits.ContainsKey(18.288) && duel.player.state.splits.ContainsKey(201.168), "Race records 60 ft and eighth-mile splits.", ref count);
            var spin = new CarSimulation(sameSpec, new Tune { engineLevel = 3, launchRpm = 5000 }, new DriverSettings { tractionAssist = false, shifting = ShiftMode.Sequential });
            spin.Launch(0);
            for (int i = 0; i < 120; i++) spin.Step(DragMath.FixedStep, new DriverInput());
            Require(spin.state.frontWheelAngleDegrees > spin.state.rearWheelAngleDegrees, "FWD wheelspin affects driven axle only.", ref count);
            var sourceTune = new Tune { finalDrive = 100 };
            var snapshot = new CarSimulation(sameSpec, sourceTune, new DriverSettings());
            Require(sourceTune.finalDrive == 100 && snapshot.tune.finalDrive == 6, "Simulation snapshots tune without altering the player's source.", ref count);
            sourceTune.finalDrive = 2;
            Require(snapshot.tune.finalDrive == 6, "Race adjustments cannot change through the source tune.", ref count);
            var invalidTune = new Tune { launchRpm = float.NaN, finalDrive = float.PositiveInfinity };
            invalidTune.ClampTo(sameSpec);
            Require(!float.IsNaN(invalidTune.launchRpm) && invalidTune.finalDrive == sameSpec.finalDrive, "Invalid saved tune numbers recover safely.", ref count);
            var manual = new CarSimulation(sameSpec, new Tune(), new DriverSettings { launch = LaunchMode.Manual, shifting = ShiftMode.Automatic });
            for (int i = 0; i < 30; i++) manual.Stage(DragMath.FixedStep, new DriverInput { throttle = 1, clutch = 1 });
            Require(manual.state.rpm > sameSpec.idleRpm + 1000, "Manual staging responds to throttle independently of transmission mode.", ref count);
            manual.Launch(0);
            for (int i = 0; i < 60; i++) manual.Step(DragMath.FixedStep, new DriverInput { throttle = 1, clutch = 1 });
            Require(manual.state.distance == 0, "Manual launch held clutch prevents drive even with automatic gearbox.", ref count);
            for (int i = 0; i < 120; i++) manual.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
            Require(manual.state.distance > 0, "Releasing launch clutch drives the car.", ref count);
            var clutch = new CarSimulation(sameSpec, new Tune(), new DriverSettings { shifting = ShiftMode.ManualClutch });
            clutch.Launch(0); clutch.state.rpm = 6500;
            Require(!clutch.Shift(1, new DriverInput()) && clutch.state.gear == 1, "Manual shift without clutch is rejected.", ref count);
            Require(clutch.Shift(1, new DriverInput { clutch = 1 }) && clutch.state.shiftRemaining > 0, "Manual shift with clutch interrupts torque.", ref count);
            clutch.state.shiftRemaining = 0; RoadFixture.Seed(clutch, 6500 / FlywheelDynamics.RpmPerRadian * clutch.wheels.radius / (sameSpec.gears[1] * clutch.tune.finalDrive)); clutch.state.rpm = 6500;
            clutch.transmission.manualDownshiftInterlock = true;
            Require(!clutch.Shift(-1, new DriverInput { clutch = 1 }), "Installed manual interlock blocks over-rev downshift.", ref count);
            var roundedDistance = new RaceSimulation(new CarSimulation(sameSpec, new Tune(), new DriverSettings()),
                new CarSimulation(sameSpec, new Tune(), new DriverSettings()), (float)402.336);
            while (roundedDistance.phase != RacePhase.Finished) roundedDistance.Step(new DriverInput());
            Require(roundedDistance.player.state.splits.ContainsKey(402.336), "Float-authored finish distance retains quarter-mile split.", ref count);
            Require(roundedDistance.telemetry.samples.Count > 100 && roundedDistance.telemetry.samples.Count < 1000, "Race telemetry is bounded and sampled.", ref count);
            double fixedTime = roundedDistance.time, fixedEt = roundedDistance.player.state.elapsed, fixedTrap = roundedDistance.player.state.trapSpeed;
            double fixedReaction = roundedDistance.player.state.reaction, fixedSplit = roundedDistance.player.state.splits[18.288];
            int sampleCount = roundedDistance.telemetry.samples.Count;
            for (int i = 0; i < 1200; i++) roundedDistance.Shutdown(new DriverInput());
            Require(roundedDistance.player.state.speed == 0 && roundedDistance.opponent.state.speed == 0 && roundedDistance.player.state.distance > roundedDistance.distance,
                "Automatic shutdown brakes stop both cars past the finish without reversing.", ref count);
            Require(roundedDistance.time == fixedTime && roundedDistance.player.state.elapsed == fixedEt && roundedDistance.player.state.trapSpeed == fixedTrap &&
                roundedDistance.player.state.reaction == fixedReaction && roundedDistance.player.state.splits[18.288] == fixedSplit && roundedDistance.telemetry.samples.Count == sampleCount,
                "Shutdown preserves race time, ET, trap, reaction, splits and run telemetry.", ref count);
            var coast = new CarSimulation(sameSpec, new Tune(), new DriverSettings { shutdownBrakeAssist = false });
            var braking = new CarSimulation(sameSpec, new Tune(), new DriverSettings());
            coast.Launch(0); braking.Launch(0); RoadFixture.Seed(coast, 35, 4, true); RoadFixture.Seed(braking, 35, 4, true);
            for (int i = 0; i < 120; i++) { coast.Shutdown(DragMath.FixedStep, 0); braking.Shutdown(DragMath.FixedStep, 1); }
            Require(coast.state.speed > braking.state.speed + 7 && coast.state.distance > braking.state.distance,
                "Pedal braking reduces speed and stopping distance compared with coasting.", ref count);
            var brakeRace = new RaceSimulation(coast, braking, 402.336) { phase = RacePhase.Finished };
            brakeRace.Shutdown(new DriverInput());
            Require(coast.state.brake == 0 && braking.state.brake == 1, "Disabled shutdown assist leaves player braking to the pedal; opponent still brakes.", ref count);
            brakeRace.Shutdown(new DriverInput { brake = .5 });
            Require(coast.state.brake == .5, "Manual shutdown accepts partial brake pressure.", ref count);
            var gripBrake = new CarSimulation(new VehicleSpec { tireFriction = .3f, brakeDecelerationMps2 = 20 }, new Tune(), new DriverSettings());
            gripBrake.Launch(0); RoadFixture.Seed(gripBrake, 20, 4, true); gripBrake.Shutdown(DragMath.FixedStep, 1);
            Require(-gripBrake.state.acceleration < 4, "Brake force is capped by tire grip even with powerful brakes.", ref count);
            var raceBrake = new CarSimulation(sameSpec, new Tune(), new DriverSettings { throttleAssist = false, shifting = ShiftMode.ManualClutch });
            raceBrake.Launch(0); RoadFixture.Seed(raceBrake, 20); raceBrake.state.clutch = 1;
            raceBrake.Step(DragMath.FixedStep, new DriverInput { clutch = 1, brake = 1 });
            Require(raceBrake.state.speed < 20 && raceBrake.state.brake == 1, "Brake input also decelerates a car during the timed run.", ref count);
            var staggered = new RaceSimulation(new CarSimulation(sameSpec, new Tune(), new DriverSettings()),
                new CarSimulation(sameSpec, new Tune(), new DriverSettings { reactionSeconds = 5 }), 402.336);
            while (!staggered.player.state.finished) staggered.Step(new DriverInput());
            double crossingSpeed = staggered.player.state.speed, crossingEt = staggered.player.state.elapsed;
            int crossingSamples = staggered.telemetry.samples.Count;
            for (int i = 0; i < 120; i++) staggered.Step(new DriverInput());
            Require(staggered.phase == RacePhase.Racing && staggered.player.state.speed < crossingSpeed && staggered.player.state.distance > staggered.distance &&
                staggered.player.state.elapsed == crossingEt && staggered.telemetry.samples.Count == crossingSamples,
                "First finisher brakes immediately while opponent timing continues and telemetry freezes.", ref count);
            var owned = new OwnedCar(); owned.SavePreset(0); owned.tune.engineLevel = 2; owned.tune.finalDrive = 5;
            Require(owned.LoadPreset(0) && owned.tune.engineLevel == 2 && owned.tune.finalDrive == 4.1f, "Loading presets preserves purchased upgrades.", ref count);
            owned.Record(roundedDistance);
            Require(owned.completedRuns == 1 && owned.bestQuarterSeconds > 0 && owned.bestSixtyFootSeconds > 0, "Per-car records include finish and splits.", ref count);
            // Compare power/grip combinations without TC: extra torque must carry a traction tradeoff.
            var highPower = new CarSimulation(sameSpec, new Tune { engineLevel = 3, launchRpm = 5000 }, new DriverSettings { tractionAssist = false, shifting = ShiftMode.Sequential });
            var grippy = new CarSimulation(sameSpec, new Tune { engineLevel = 3, tireLevel = 3, launchRpm = 5000 }, new DriverSettings { tractionAssist = false, shifting = ShiftMode.Sequential });
            highPower.Launch(0); grippy.Launch(0);
            for (int i = 0; i < 120; i++) { highPower.Step(DragMath.FixedStep, new DriverInput()); grippy.Step(DragMath.FixedStep, new DriverInput()); }
            Require(grippy.state.distance > highPower.state.distance && grippy.state.slip < highPower.state.slip, "Tires improve an unassisted high-power launch and reduce wheelspin.", ref count);
            var manualDuel = new RaceSimulation(new CarSimulation(sameSpec, new Tune(), new DriverSettings { launch = LaunchMode.Manual, shifting = ShiftMode.Sequential, throttleAssist = false }),
                new CarSimulation(sameSpec, new Tune(), new DriverSettings { reactionSeconds = .3 }), 402.336);
            while (manualDuel.phase != RacePhase.Finished)
            {
                var s = manualDuel.player.state;
                bool staging = manualDuel.time < 3.12;
                // Meter launch clutch around 3200 RPM until first-gear road speed
                // catches it. Dumping it now physically consumes stored flywheel energy.
                double launchCapacity = DragMath.Torque(sameSpec, manualDuel.player.tune, s.rpm) + (s.rpm - 3200) * .2;
                double launchPedal = s.speed < 3200 / FlywheelDynamics.RpmPerRadian * manualDuel.player.wheels.radius / (sameSpec.gears[0] * manualDuel.player.tune.finalDrive)
                    ? DragMath.Clamp(.85 - .55 * launchCapacity / sameSpec.ClutchCapacity, 0, 1) : 0;
                manualDuel.Step(new DriverInput { throttle = !staging || s.rpm < 3200 ? 1 : 0, clutch = staging ? 1 : launchPedal,
                    launch = !staging && !s.launched, shiftUp = s.launched && s.speed > 2 && s.clutch < .2 && s.clutchSlipRpm < 250 && s.rpm >= Math.Min(manualDuel.player.tune.shiftRpm, manualDuel.player.ShiftPoint(s.gear)) });
            }
            File.WriteAllText(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "../..")), "reports/flywheel-manual-diagnostic.csv"), manualDuel.telemetry.Csv());
            Require(manualDuel.playerWon && manualDuel.player.state.gear > 1, "Manual launch/optimal sequential shifting can beat an identical automatic build through reaction advantage. Player " + manualDuel.player.state.elapsed + " / AI " + manualDuel.opponent.state.elapsed, ref count);
            var lightEngine = new CarSimulation(new VehicleSpec { engineInertiaKgM2 = .15f }, new Tune(), new DriverSettings { launch = LaunchMode.Manual });
            var heavyEngine = new CarSimulation(new VehicleSpec { engineInertiaKgM2 = .7f }, new Tune(), new DriverSettings { launch = LaunchMode.Manual });
            for (int i = 0; i < 60; i++) { lightEngine.Stage(DragMath.FixedStep, new DriverInput { throttle = 1, clutch = 1 }); heavyEngine.Stage(DragMath.FixedStep, new DriverInput { throttle = 1, clutch = 1 }); }
            Require(lightEngine.state.rpm > heavyEngine.state.rpm + 1000, "Engine inertia changes free-rev response.", ref count);
            var weakClutch = new CarSimulation(new VehicleSpec { clutchCapacityNm = 50 }, new Tune(), new DriverSettings());
            var strongClutch = new CarSimulation(sameSpec, new Tune(), new DriverSettings());
            weakClutch.Launch(0); strongClutch.Launch(0);
            for (int i = 0; i < 60; i++) { weakClutch.Step(DragMath.FixedStep, new DriverInput()); strongClutch.Step(DragMath.FixedStep, new DriverInput()); }
            Require(weakClutch.state.rpm > strongClutch.state.rpm && weakClutch.state.distance < strongClutch.state.distance, "Insufficient clutch capacity causes RPM flare and poorer acceleration.", ref count);
            Require(weakClutch.state.transmittedTorqueNm <= 50.00001, "Clutch torque never exceeds configured capacity.", ref count);
            var upgradedClutch = new CarSimulation(new VehicleSpec { clutchCapacityNm = 50 }, new Tune { gearboxLevel = 3 }, new DriverSettings());
            upgradedClutch.Launch(0);
            for (int i = 0; i < 60; i++) upgradedClutch.Step(DragMath.FixedStep, new DriverInput());
            Require(upgradedClutch.state.distance > weakClutch.state.distance, "Gearbox upgrades improve acceleration when clutch capacity is the bottleneck.", ref count);
            var partial = new CarSimulation(sameSpec, new Tune(), new DriverSettings { launch = LaunchMode.Manual, shifting = ShiftMode.ManualClutch, throttleAssist = false });
            partial.Stage(DragMath.FixedStep, new DriverInput { throttle = 1, clutch = 1 }); partial.state.rpm = 3200; partial.Launch(0);
            partial.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
            Require(partial.state.clutch > .8 && partial.state.clutch < 1, "Digital clutch release ramps instead of instantly dumping.", ref count);
            for (int i = 0; i < 60; i++) partial.Step(DragMath.FixedStep, new DriverInput { throttle = 1, clutch = .55 });
            Require(partial.state.transmittedTorqueNm > 0 && partial.state.transmittedTorqueNm < sameSpec.ClutchCapacity * .7 && partial.state.clutchSlipRpm > 0,
                "Partial analog pedal transmits bounded torque while allowing clutch slip.", ref count);
            var adapter = new RaceInputAdapter(); adapter.BeginFrame(0, 0);
            Rect gasRect = RaceInputAdapter.ControlRect(RaceInputAdapter.GasControl, 1100, 700), clutchRect = RaceInputAdapter.ControlRect(RaceInputAdapter.ClutchControl, 1100, 700);
            adapter.SubmitPointer(10, new Vector2(gasRect.x + gasRect.width * .25f, gasRect.center.y), true, false, 1100, 700);
            adapter.SubmitPointer(20, clutchRect.center, true, false, 1100, 700);
            var twoPedals = adapter.Consume();
            Require(Math.Abs(twoPedals.throttle - .5) < .001 && twoPedals.clutch == 1, "Independent fingers produce simultaneous analog throttle and clutch.", ref count);
            adapter.SubmitPointer(40, RaceInputAdapter.ControlRect(RaceInputAdapter.BrakeControl, 1100, 700).center, true, false, 1100, 700);
            Require(adapter.Peek().brake == 1 && adapter.Peek().clutch == 1 && Math.Abs(adapter.Peek().throttle - .5) < .001,
                "A third finger operates the brake independently of gas and clutch.", ref count);
            adapter.SubmitPointer(20, Vector2.zero, false, true, 1100, 700);
            Require(Math.Abs(adapter.Consume().throttle - .5) < .001 && adapter.Peek().clutch == 0, "Releasing one finger leaves the other pedal held.", ref count);
            adapter.SubmitPointer(30, RaceInputAdapter.ControlRect(RaceInputAdapter.UpControl, 1100, 700).center, true, false, 1100, 700);
            Require(adapter.Consume().shiftUp && !adapter.Consume().shiftUp, "Touch shift edges are consumed exactly once across fixed ticks.", ref count);
            adapter.Clear();
            Require(adapter.Peek().throttle == 0 && adapter.Peek().clutch == 0 && adapter.Peek().brake == 0 && !adapter.Peek().shiftUp, "Pause/reset clears held controls and pending edges.", ref count);
            foreach (bool useSpace in new[] { false, true })
            {
                adapter.Clear();
                adapter.SubmitKeyboard(!useSpace, useSpace, false, false, false, false, false);
                Require(adapter.Consume().throttle == 1 && !adapter.Peek().launch, "Held gas is read immediately after reset and never triggers launch: Space=" + useSpace, ref count);
                adapter.SubmitKeyboard(!useSpace, useSpace, true, false, false, true, false);
                var shiftedInput = adapter.Consume();
                Require(shiftedInput.throttle == 1 && shiftedInput.clutch == 1 && shiftedInput.shiftUp && !adapter.Consume().shiftUp && adapter.Peek().throttle == 1,
                    "Shift edge/clutch cannot consume held keyboard throttle: Space=" + useSpace, ref count);
            }
            adapter.SubmitKeyboard(true, true, false, false, false, false, false);
            adapter.SubmitKeyboard(false, true, false, false, false, false, false);
            Require(adapter.Peek().throttle == 1, "Releasing W while Space remains held preserves throttle.", ref count);
            adapter.SubmitKeyboard(false, false, false, false, false, false, false);
            Require(adapter.Consume().throttle == 0, "Releasing both throttle keys stops giving gas.", ref count);
            foreach (LaunchMode mode in new[] { LaunchMode.Automatic, LaunchMode.Assisted, LaunchMode.Manual })
            {
                var staged = new CarSimulation(sameSpec, new Tune(), new DriverSettings { launch = mode });
                Require(staged.state.gear == 0 && staged.state.stagingBrakesHeld && staged.state.brake == 1,
                    "Car initializes in neutral with staging brakes held: " + mode, ref count);
                double initialRpm = staged.state.rpm;
                for (int i = 0; i < 120; i++) staged.Stage(DragMath.FixedStep, new DriverInput { throttle = 1 });
                double heldRpm = staged.state.rpm;
                for (int i = 0; i < 60; i++) staged.Stage(DragMath.FixedStep, new DriverInput());
                Require(heldRpm > initialRpm + 1000 && staged.state.rpm < heldRpm && staged.state.distance == 0 && !staged.state.launched,
                    "Held/released staging gas controls RPM without moving the car: " + mode, ref count);
                Require(staged.state.gear == 0 && staged.state.brake == 1 && staged.state.stagingBrakesHeld && staged.state.transmittedTorqueNm == 0 &&
                    staged.state.frontWheelAngleDegrees == 0 && staged.state.rearWheelAngleDegrees == 0 && !staged.Shift(1, new DriverInput { clutch = 1 }),
                    "Neutral staging isolates wheels; race timing handles first-gear engagement: " + mode, ref count);
                staged.Launch(.25);
                bool released = staged.state.gear == 1 && !staged.state.stagingBrakesHeld && staged.state.brake == 0;
                staged.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
                Require(released && staged.state.distance > 0 && staged.state.transmittedTorqueNm > 0,
                    "Launching engages first, releases staging brakes and drives the car: " + mode, ref count);
            }
            var waitForLaunch = new RaceSimulation(new CarSimulation(sameSpec, new Tune(), new DriverSettings { launch = LaunchMode.Assisted }),
                new CarSimulation(sameSpec, new Tune(), new DriverSettings()), 402.336);
            for (int i = 0; i < 480; i++) waitForLaunch.Step(new DriverInput { throttle = 1 });
            Require(waitForLaunch.player.state.gear == 0 && waitForLaunch.player.state.brake == 1 && waitForLaunch.player.state.distance == 0 &&
                waitForLaunch.opponent.state.gear == 1 && !waitForLaunch.opponent.state.stagingBrakesHeld,
                "Waiting after green stays neutral/braked while a launched opponent is in gear.", ref count);
            foreach (LaunchMode mode in new[] { LaunchMode.Assisted, LaunchMode.Manual })
            {
                var earlyUp = new RaceSimulation(new CarSimulation(sameSpec, new Tune(), new DriverSettings { launch = mode }),
                    new CarSimulation(sameSpec, new Tune(), new DriverSettings()), 402.336);
                earlyUp.Step(new DriverInput { throttle = 1, shiftUp = true });
                Require(earlyUp.player.state.disqualified && earlyUp.player.state.gear == 0 && earlyUp.player.state.stagingBrakesHeld,
                    "Early neutral-to-first shift is a red light with staging brakes retained: " + mode, ref count);
                foreach (TransmissionType type in Enum.GetValues(typeof(TransmissionType)))
                {
                    var launchSpec = sameSpec.Copy(); launchSpec.transmission.type = type;
                    var gearLaunch = new RaceSimulation(new CarSimulation(launchSpec, new Tune(), new DriverSettings {
                        launch = mode, shifting = ShiftMode.Sequential, throttleAssist = false }),
                        new CarSimulation(launchSpec, new Tune(), new DriverSettings()), 402.336);
                    for (int i = 0; i < 390; i++) gearLaunch.Step(new DriverInput { throttle = 1, clutch = 1 });
                    gearLaunch.Step(new DriverInput { throttle = 1, clutch = 1, shiftUp = true });
                    Require(gearLaunch.player.state.launched && !gearLaunch.player.state.disqualified && gearLaunch.player.state.gear == 1 &&
                        gearLaunch.player.state.shiftRemaining == 0 && !gearLaunch.player.state.stagingBrakesHeld && gearLaunch.player.state.brake == 0 &&
                        gearLaunch.player.state.throttle == 1 && gearLaunch.player.state.distance == 0,
                        "Shift-up engages first exactly once, preserving gas and launch timestamp: " + mode + "/" + type, ref count);
                    for (int i = 0; i < 15; i++) gearLaunch.Step(new DriverInput { throttle = 1, clutch = 1 });
                    Require(gearLaunch.player.state.gear == 1 && gearLaunch.player.state.throttle == 1 &&
                        (mode != LaunchMode.Manual || type != TransmissionType.Manual || gearLaunch.player.state.distance == 0),
                        "No buffered second shift; manual launch clutch still holds drive disconnected: " + mode + "/" + type, ref count);
                    for (int i = 0; i < 90; i++) gearLaunch.Step(new DriverInput { throttle = 1 });
                    Require(gearLaunch.player.state.distance > 0 && gearLaunch.player.state.throttle == 1,
                        "Held gas drives after neutral-to-first engagement and clutch release: " + mode + "/" + type, ref count);
                }
            }
            var autoGearLaunch = new RaceSimulation(new CarSimulation(sameSpec, new Tune(), new DriverSettings { reactionSeconds = .5 }),
                new CarSimulation(sameSpec, new Tune(), new DriverSettings()), 402.336);
            autoGearLaunch.Step(new DriverInput { shiftUp = true });
            Require(!autoGearLaunch.player.state.disqualified && !autoGearLaunch.player.state.launched,
                "Automatic launch ignores an early shift-up without red-lighting.", ref count);
            for (int i = 0; i < 390; i++) autoGearLaunch.Step(new DriverInput());
            autoGearLaunch.Step(new DriverInput { shiftUp = true });
            Require(!autoGearLaunch.player.state.launched && autoGearLaunch.player.state.gear == 0,
                "Shift-up cannot bypass the selected automatic reaction delay.", ref count);
            for (int i = 0; i < 45; i++) autoGearLaunch.Step(new DriverInput());
            Require(autoGearLaunch.player.state.launched && autoGearLaunch.player.state.reaction >= .5,
                "Automatic launch still engages on its scheduled reaction.", ref count);
            waitForLaunch.Step(new DriverInput { launch = true });
            Require(waitForLaunch.player.state.launched && waitForLaunch.player.state.gear == 1,
                "Enter-compatible launch remains available.", ref count);
            waitForLaunch.player.state.stalled = true; waitForLaunch.player.state.rpm = 0;
            waitForLaunch.Step(new DriverInput { shiftUp = true, clutch = 1 });
            Require(!waitForLaunch.player.state.stalled && waitForLaunch.player.state.rpm > 0 && waitForLaunch.player.state.gear == 1,
                "Combined shift-up/restart control starts a stalled engine without changing gear.", ref count);
            foreach (ShiftMode mode in new[] { ShiftMode.Sequential, ShiftMode.ManualClutch })
            {
                var heldGas = new CarSimulation(sameSpec, new Tune(), new DriverSettings { shifting = mode, throttleAssist = false });
                heldGas.Launch(0); RoadFixture.Seed(heldGas, 10); heldGas.state.rpm = 5000;
                heldGas.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
                heldGas.Step(DragMath.FixedStep, new DriverInput { throttle = 1, clutch = mode == ShiftMode.ManualClutch ? 1 : 0, shiftUp = true });
                Require(heldGas.state.gear == 2 && heldGas.state.throttle == 1 && heldGas.state.transmittedTorqueNm == 0,
                    "Gear change cuts shaft torque temporarily while throttle remains held: " + mode, ref count);
                for (int i = 0; i < 120; i++) heldGas.Step(DragMath.FixedStep, new DriverInput { throttle = 1 });
                Require(heldGas.state.throttle == 1 && heldGas.state.transmittedTorqueNm > 0 && heldGas.state.shiftRemaining == 0,
                    "Held gas resumes driving torque after a shift without another press: " + mode, ref count);
            }
            var limited = new CarSimulation(sameSpec, new Tune(), new DriverSettings { launch = LaunchMode.Manual });
            int cuts = 0, recoveries = 0; bool wasCut = false;
            for (int i = 0; i < 720; i++)
            {
                limited.Stage(DragMath.FixedStep, new DriverInput { throttle = 1 });
                if (limited.state.limiter && !wasCut) cuts++;
                if (!limited.state.limiter && wasCut) recoveries++;
                wasCut = limited.state.limiter;
            }
            Require(cuts > 5 && recoveries > 5 && limited.state.rpm <= sameSpec.redlineRpm + 100 && limited.state.throttle == 1,
                "Rev limiter repeatedly cuts and recovers with gas held without cancelling throttle.", ref count);
            bool fuelCut = true; ClutchDynamics.UpdateLimiter(sameSpec, sameSpec.redlineRpm - 50, ref fuelCut);
            Require(fuelCut, "Limiter cut remains active inside its recovery band.", ref count);
            ClutchDynamics.UpdateLimiter(sameSpec, sameSpec.redlineRpm - 200, ref fuelCut);
            Require(!fuelCut, "Limiter resumes fuel below the configured recovery RPM.", ref count);
            var preference = new DriverSettings { soundVolume = 0, shutdownBrakeAssist = false, clutchReleaseSeconds = .4f };
            preference.ApplyPreset(DrivingPreset.Sequential);
            Require(preference.shifting == ShiftMode.Sequential && preference.launch == LaunchMode.Assisted && !preference.throttleAssist && preference.tractionAssist,
                "Sequential preset supplies pedal-controlled gas with clutch-assisted launch.", ref count);
            preference.ApplyPreset(DrivingPreset.Manual);
            Require(preference.shifting == ShiftMode.ManualClutch && preference.launch == LaunchMode.Manual && !preference.tractionAssist && !preference.revMatchAssist &&
                preference.soundVolume == 0 && !preference.shutdownBrakeAssist && preference.clutchReleaseSeconds == .4f, "Manual preset preserves independent preferences.", ref count);
            preference.ApplyPreset(DrivingPreset.Automatic);
            Require(preference.shifting == ShiftMode.Automatic && preference.launch == LaunchMode.Automatic && preference.throttleAssist && preference.tractionAssist,
                "Automatic preset restores driving assists.", ref count);
            var captured = owned.lastPass;
            double captureEt = captured.elapsedSeconds; float captureDrive = captured.tune.finalDrive;
            roundedDistance.player.tune.finalDrive = 2; roundedDistance.Shutdown(new DriverInput());
            Require(captured.elapsedSeconds == captureEt && captured.tune.finalDrive == captureDrive && captured.driving != roundedDistance.player.driver,
                "Saved pass captures timing, tune and driving settings independently of subsequent race/tune changes.", ref count);
            var otherDistance = new PassRecord { distanceM = 201.168, elapsedSeconds = 10 };
            Require(captured.Compare(otherDistance).Contains("different distance"), "Run comparison refuses incompatible track lengths.", ref count);
            var baselineTune = new Tune { engineLevel = 1, tireLevel = 1, finalDrive = 4.1f };
            var trials = TuneExperiments.Sweep(sameSpec, baselineTune, true);
            Require(trials.Count == 13 && trials[0].label == "baseline" && baselineTune.finalDrive == 4.1f && baselineTune.launchRpm == 3200,
                "Tune sweep creates thirteen isolated experiments without changing its source.", ref count);
            Require(trials[0].result.elapsed == TuneExperiments.Sweep(sameSpec, baselineTune, true)[0].result.elapsed &&
                TuneExperiments.Csv(trials).Split('\n').Length == 15, "Tune experiments are deterministic and export all rows.", ref count);
            var taller = trials.Find(t => t.tune.finalDrive == 2);
            double bestTime = double.MaxValue; foreach (var trial in trials) bestTime = Math.Min(bestTime, trial.result.elapsed);
            Require(taller.result.elapsed > bestTime + .1, "Taller final drive is not automatically the quickest quarter mile.", ref count);
            var referenceDuel = new RaceSimulation(new CarSimulation(sameSpec, new Tune(), new DriverSettings()), new CarSimulation(sameSpec, new Tune(), new DriverSettings()), 402.336);
            while (referenceDuel.phase != RacePhase.Finished) referenceDuel.Step(new DriverInput());
            Require(Math.Abs(Benchmark.QuarterMile(sameSpec, new Tune()).elapsed - referenceDuel.player.state.elapsed) < .000001,
                "Assisted benchmark and actual timed pass share interpolated finish timing.", ref count);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            Directory.CreateDirectory(Path.Combine(root, "reports"));
            count += EngineSoundVerification.Run(root);
            count += PowertrainVerification.Run(root);
            count += ManualGearboxVerification.Run(root);
            count += PartHardwareVerification.Run(root);
            count += RemainingChecks(root);
            File.WriteAllText(Path.Combine(root, "reports/tune-comparisons.csv"), TuneExperiments.Csv(trials));
            File.WriteAllText(Path.Combine(root, "reports/career-path.txt"), VerifyCareer(ref count));
            File.WriteAllText(Path.Combine(root, "reports/balance-matrix.csv"), DragWorkbench.BalanceCsv());
            File.WriteAllText(Path.Combine(root, "reports/unity-verification.txt"), "PASS: " + count + " foundation checks.\nEditor: " + Application.unityVersion + "\nUTC: " + DateTime.UtcNow.ToString("O") + "\n");
            Debug.Log("DRAGLAB VERIFICATION PASS: " + count + " checks.");
        }
        // Resume the shared tail after a successful foundation prefix; report its scope explicitly.
        public static void RunRemaining()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            int count=RemainingChecks(root);
            File.WriteAllText(Path.Combine(root,"reports/variety/foundation-remaining-verification.txt"),"PASS: "+count+" remaining foundation checks. Prefix is recorded separately, not rerun here.\n",new UTF8Encoding(false));
            Debug.Log("DRAGLAB REMAINING VERIFICATION PASS: "+count+" checks.");
        }
        public static void RunRemainingSimulation()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            int count=RemainingSimulationChecks(root);
            File.WriteAllText(Path.Combine(root,"reports/variety/foundation-simulation-verification.txt"),"PASS: "+count+" remaining simulation checks. Completed hardware/service/recovery/tools gates are recorded separately, not rerun here.\n",new UTF8Encoding(false));
            Debug.Log("DRAGLAB REMAINING SIMULATION PASS: "+count+" checks.");
        }
        static int RemainingChecks(string root)
        {
            int count=0;
            count += FuelingConversionVerification.Run(root);
            count += ServiceFluidsVerification.Run(root);
            count += IndividualEngineVerification.Run(root);
            count += MechanicalRecoveryVerification.Run(root);
            count += IntegratedToolsVerification.Run(root);
            count += RemainingSimulationChecks(root);
            return count;
        }
        static int RemainingSimulationChecks(string root)
        {
            int count=0;
            count += SliceTwoAudit.RunVerified();
            count += InductionVerification.Run(root);
            count += EngineBenchVerification.Run(root);
            count += EngineDynoVerification.Run(root);
            count += FlywheelVerification.Run(root);
            count += ExhaustPopVerification.Run(root);
            count += EngineMusicVerification.Run(root);
            count += SharedAudioVerification.Run(root);
            count += MechanicsVerification.Run(root);
            count += PartEffectsVerification.Run(root);
            count += PartsVerification.Run(root);
            count += OwnershipVerification.Run(root);
            count += InventoryVerification.Run(root);
            count += PhysicalBenchVerification.Run(root);
            count += BenchAbuseVerification.Run(root);
            count += SharedMechanicsVerification.Run(root);
            count += AxleFailureVerification.Run(root);
            count += GameResetVerification.Run(root);
            count += CarInspectionVerification.Run(root);
            count += MassLoadVerification.Run(root);
            count += WheelContactVerification.Run(root);
            count += DrivingContactVerification.Run(root);
            count += AwdContactVerification.Run(root);
            count += FuelVerification.Run(root);
            count += TireVerification.Run(root);
            return count;
        }
        static void Require(bool pass, string label, ref int count)
        {
            if (!pass) throw new InvalidOperationException("DRAGLAB CHECK FAILED: " + label);
            count++;
        }
        public static void VerifyCareerStandalone() { int count=0;string result=VerifyCareer(ref count); File.WriteAllText(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath,"../..")),"reports/career-path.txt"),result);Debug.Log("CAREER PARTS PASS: " + count); }
        static string VerifyCareer(ref int count)
        {
            var events = Resources.LoadAll<CareerEventDefinition>("Data/Events").Where(e=>new[]{"first-pass","club-run","coupe-rival","local-final"}.Contains(e.stableId)).ToArray();
            Array.Sort(events, (a, b) => a.reputationRequired.CompareTo(b.reputationRequired));
            var starter = Array.Find(Resources.LoadAll<VehicleDefinition>("Data/Vehicles"), v => v.spec.id == "starter-hatch");
            var ledger = PlayerProfile.New(DateTime.UtcNow.Ticks);
            var catalog = new MechanicalCatalog(Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Select(v=>v.spec)); MechanicsOwnership.Ensure(ledger,catalog);
            var log = new StringBuilder("Starter-car career path; normal AI; no injected funds or reputation.\n");
            CareerEventDefinition lastWon = null;
            foreach (var evt in events)
            {
                Require(ledger.reputation >= evt.reputationRequired, "Prior career wins unlock " + evt.stableId, ref count);
                int guard = 60;
                while (--guard > 0)
                {
                    var duel = CareerDuel(ledger, evt);
                    if (duel.playerWon)
                    {
                        ledger.AwardRace(true, evt.stableId, evt.cashWin, evt.cashLoss, evt.reputationWin); lastWon = evt;
                        var build = ledger.SelectedCar.tune;
                        log.AppendLine("WIN " + evt.stableId + " / ET " + duel.player.state.elapsed.ToString("F3") + " / rival ET " + duel.opponent.state.elapsed.ToString("F3") +
                            " / FD " + build.finalDrive.ToString("F2") + " / launch " + build.launchRpm.ToString("F0") + " RPM / E-T-W-G " +
                            build.engineLevel + "-" + build.tireLevel + "-" + build.weightLevel + "-" + build.gearboxLevel + " / cash " + ledger.cash + " / REP " + ledger.reputation);
                        break;
                    }
                    // Find the best freely adjustable tune before purchasing parts.
                    var resolved=MechanicsOwnership.Resolve(ledger,ledger.SelectedCar,catalog);
                    double baseline = Benchmark.QuarterMile(resolved.spec, resolved.tune).elapsed;
                    Tune best = ledger.SelectedCar.tune;
                    foreach (var trial in TuneExperiments.Sweep(resolved.spec, resolved.tune, true))
                        if (trial.result.elapsed < baseline - .01) { best = trial.tune; baseline = trial.result.elapsed; }
                    ledger.SelectedCar.tune.ApplyAdjustments(best);
                    if (CareerDuel(ledger, evt).playerWon) continue;
                    string purchase = null; double fastest = baseline;
                    foreach (string category in new[] { "engine", "tires", "weight", "gearbox" })
                    {
                        var candidate = ledger.SelectedCar.tune.Copy();
                        int level = category == "engine" ? candidate.engineLevel : category == "tires" ? candidate.tireLevel : category == "weight" ? candidate.weightLevel : candidate.gearboxLevel;
                        if (level >= 3 || ledger.cash < PlayerProfile.UpgradeCost(category, level)) continue;
                        if (category == "engine") candidate.engineLevel++; else if (category == "tires") candidate.tireLevel++; else if (category == "weight") candidate.weightLevel++; else candidate.gearboxLevel++;
                        var map=MechanicsOwnership.Definitions(ledger,ledger.SelectedCar);var slot=category=="engine"?PartCategory.HeadIntake:category=="tires"?PartCategory.Tires:category=="weight"?PartCategory.Body:PartCategory.Gearbox;map[slot]=catalog.Id(starter.spec.id,slot,level+1);var proposed=catalog.Resolve(starter.spec.id,map,candidate);
                        double elapsed = Benchmark.QuarterMile(proposed.spec, proposed.tune).elapsed;
                        if (elapsed < fastest - .005) { purchase = category; fastest = elapsed; }
                    }
                    if (purchase != null)
                    {
                        if (!ledger.Upgrade(purchase)) throw new InvalidOperationException("Career test purchase failed.");
                        log.AppendLine("BUY " + purchase + " / cash " + ledger.cash); continue;
                    }
                    var defs=MechanicsOwnership.Definitions(ledger,ledger.SelectedCar);
                    bool stripped=false;
                    foreach(var slot in new[]{PartCategory.Interior,PartCategory.Accessories}) if(defs.ContainsKey(slot)){PartOperations.Remove(ledger,ledger.SelectedCar.instanceId,slot);log.AppendLine("STRIP " + slot + " / original retained");stripped=true;break;}
                    if(stripped)continue;
                    string extra=null; double extraTime=baseline;
                    foreach(var part in catalog.parts.Where(p=>catalog.Fits(resolved.spec,p)&&p.price<=ledger.cash&&p.stage>0&&new[]{PartCategory.Clutch,PartCategory.FuelSystem,PartCategory.Ignition,PartCategory.Flywheel}.Contains(p.category))) {
                        if(defs.TryGetValue(part.category,out var old)&&catalog.Find(old).stage>=part.stage)continue;
                        var map=new System.Collections.Generic.Dictionary<PartCategory,string>(defs);map[part.category]=part.id;var proposed=catalog.Resolve(starter.spec.id,map,ledger.SelectedCar.tune);double time=Benchmark.QuarterMile(proposed.spec,proposed.tune).elapsed;if(time<extraTime-.005){extra=part.id;extraTime=time;}
                    }
                    if(extra!=null){PartOperations.Buy(ledger,catalog,ledger.SelectedCar.instanceId,extra,true);log.AppendLine("BUY/INSTALL " + extra + " / cash " + ledger.cash);continue;}
                    if (lastWon == null) break;
                    var repeat = CareerDuel(ledger, lastWon);
                    if (!repeat.playerWon) break;
                    int repBefore = ledger.reputation;
                    ledger.AwardRace(true, lastWon.stableId, lastWon.cashWin, lastWon.cashLoss, lastWon.reputationWin);
                    if (ledger.reputation != repBefore) throw new InvalidOperationException("Repeat win duplicated reputation.");
                    log.AppendLine("REPEAT " + lastWon.stableId + " / cash " + ledger.cash);
                }
                File.WriteAllText(Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath,"../..")), "reports/career-path-progress.txt"),log.ToString());
                Require(ledger.careerWins.Contains(evt.stableId) && ledger.cash >= 0, "Starter can progress through " + evt.stableId + " with earned upgrades/tuning.", ref count);
            }
            Require(ledger.careerWins.Count == 4 && ledger.reputation == 12, "All four career events complete without duplicate reputation.", ref count);
            return log.ToString();
        }
        static RaceSimulation CareerDuel(PlayerProfile ledger, CareerEventDefinition evt)
        {
            var b=MechanicsOwnership.Resolve(ledger,ledger.SelectedCar,ledger.catalog);
            var race = new RaceSimulation(new CarSimulation(b.spec, b.tune, new DriverSettings()),
                new CarSimulation(evt.opponent.spec, evt.opponentTune, new DriverSettings { reactionSeconds = .22 }), evt.distanceM);
            while (race.phase != RacePhase.Finished) race.Step(new DriverInput());
            return race;
        }
    }
}
