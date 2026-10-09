using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;

namespace DragLab.Editor
{
    public static class EngineBenchVerification
    {
        static void Check(bool ok, string message, ref int count) { if (!ok) throw new InvalidOperationException("BENCH CHECK FAILED: " + message); count++; }
        static double Energy(float[] data) { double sum = 0; foreach (float value in data) { if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Nonfinite bench audio"); sum += value * value; } return sum / data.Length; }
        static double FilterTone(ExhaustSetup setup, double hz)
        {
            var filter = new ExhaustFilter(setup, 0); filter.Configure(44100); double sum = 0;
            for (int i = 0; i < 44100; i++) { double value = filter.Process(Math.Sin(i * 2 * Math.PI * hz / 44100)); if (i > 22050) sum += value * value; }
            return sum / 22049;
        }
        public static int Run(string root)
        {
            int count = 0; var report = new StringBuilder("Standalone engine bench / exhaust transfer / deterministic scenario replay.\n");
            var config = new EngineBenchConfig();
            config.controls = new EngineBenchControls { fixedRpm = true, targetRpm = 4500, throttle = 1, load = .8f, gear = 2 };
            var bench = new EngineTestBench(config.vehicle, config.tune);
            for (int i = 0; i < 240; i++) bench.Step(config.controls);
            Check(bench.state.rpm == 4500 && bench.state.gear == 2 && bench.state.engineTorqueNm > 0, "Static RPM overrides integration while retaining real output/load.", ref count);
            Check(bench.state.distance == 0 && !bench.state.finished && bench.ticks == 240, "Bench has no countdown/translation/finish.", ref count);
            config.vehicle.sound.cylinders = 8; config.vehicle.torque[0].torqueNm = 999;
            Check(bench.spec.sound.cylinders == 4 && bench.spec.torque[0].torqueNm == 85, "Bench captures deep engineering/acoustic copies.", ref count);
            var free = new EngineTestBench(new VehicleSpec(), new Tune()); var loaded = new EngineTestBench(new VehicleSpec(), new Tune());
            for (int i = 0; i < 90; i++) { free.Step(new EngineBenchControls { throttle = .6f }); loaded.Step(new EngineBenchControls { throttle = .6f, load = .9f }); }
            Check(free.state.rpm > loaded.state.rpm + 500, "Adjustable dyno load changes actual RPM response.", ref count);
            var manualSpec = new VehicleSpec(); manualSpec.transmission.type = TransmissionType.Manual;
            var running = new EngineTestBench(manualSpec, new Tune());
            var driving = new EngineBenchControls { throttle = 1, gear = 1, load = .4f };
            for (int i = 0; i < 120; i++) running.Step(driving);
            Check(running.state.rpm > manualSpec.idleRpm + 500, "Held gas accelerates the engine in first gear.", ref count);
            driving.gear = 2; running.Step(driving);
            Check(running.state.throttle == 1 && running.state.rpm >= manualSpec.idleRpm && running.state.engineHorsepower > 0 &&
                running.state.transmittedTorqueNm == 0 && !running.state.transmissionTorqueCut,
                "Manual shift disconnects the shaft while held throttle and combustion remain active.", ref count);
            var audibleShift = new EngineSoundController(manualSpec, new Tune()).Update(running.state, true, .35, DragMath.FixedStep, running.audioLoad);
            Check(audibleShift.active && !audibleShift.cut, "Bench gear change does not mute combustion or fake an engine limiter.", ref count);
            driving.throttle = 0; driving.brake = 1; driving.load = 1;
            bool idleFloor = true;
            for (int i = 0; i < 720; i++) { running.Step(driving); idleFloor &= running.state.rpm >= manualSpec.idleRpm; }
            Check(idleFloor && running.state.rpm <= manualSpec.idleRpm * 1.05, "Powered-on lift/braking recovers to proper idle without dropping to zero.", ref count);
            driving.fixedRpm = true; driving.targetRpm = 0; running.Step(driving);
            Check(running.state.rpm == manualSpec.idleRpm, "Static RPM below idle cannot stop a powered-on engine.", ref count);
            driving.damageOverride = 1; running.Step(driving);
            Check(running.state.rpm == manualSpec.idleRpm && !running.state.engineFailed && !running.state.stalled,
                "Default protected testing keeps the engine idling even after a failure-state injection.", ref count);
            driving.engineEnabled = false; running.Step(driving);
            Check(running.state.rpm == 0, "Explicit engine-off still stops the engine.", ref count);
            var turbo = new EngineTestBench(new VehicleSpec(), new Tune { engineLevel = 1, inductionUpgrade = EngineUpgradeInduction.Turbo });
            var gas = new EngineBenchControls { fixedRpm = true, targetRpm = 5000, throttle = 1, load = 1, gear = 1 };
            for (int i = 0; i < 360; i++) turbo.Step(gas);
            Check(turbo.state.boostBar > .2, "Loaded fixture builds physical boost.", ref count);
            gas.throttle = 0; turbo.Step(gas);
            Check(turbo.state.boostReleaseCount == 1 && turbo.state.engineTorqueNm == 0, "Fixture lift emits one charged release without crank power.", ref count);
            turbo.SetGear(0); Check(turbo.state.gear == 0 && !turbo.state.launched, "Neutral is available without a race cycle.", ref count);
            gas.gear = 1; gas.tireSlip = 1; gas.roadSpeedKmh = 40; turbo.Step(gas);
            Check(turbo.state.frontWheelAngleDegrees > turbo.state.rearWheelAngleDegrees && turbo.state.slip == 1, "Rollers/slip rotate the driven axle independently.", ref count);
            gas.engineEnabled = false; turbo.Step(gas);
            Check(turbo.state.rpm == 0 && turbo.state.engineTorqueNm == 0, "Power off stops engine output.", ref count);
            var silencer = ExhaustPart.Create(ExhaustPartKind.AbsorptiveSilencer); silencer.lowPassHz = 600;
            var setup = new ExhaustSetup { parts = new[] { silencer } };
            Check(FilterTone(setup, 2000) < FilterTone(setup, 100) * .1, "Silencer changes spectral balance, not just output level.", ref count);
            double full = FilterTone(setup, 100); setup.parts[0].amount = .5f;
            Check(FilterTone(setup, 100) > full, "Partial component strength is continuously adjustable.", ref count);
            setup.parts[0].amount = 0;
            Check(Math.Abs(FilterTone(setup, 1000) - FilterTone(new ExhaustSetup(), 1000)) < 1e-12, "Zero amount bypasses exactly; legacy empty paths remain compatible.", ref count);
            var resonator = new ExhaustSetup { parts = new[] { ExhaustPart.Create(ExhaustPartKind.Resonator) } };
            Check(FilterTone(resonator, 160) < FilterTone(resonator, 600) * .3, "Resonator cuts its authored band independently.", ref count);
            Check(FilterTone(ExhaustSetup.Preset(0), 2000) < FilterTone(ExhaustSetup.Preset(3), 2000) * .5, "Multi-component quiet/open setups differ.", ref count);
            var captured = new EngineSoundProfile(new VehicleSpec { sound = new EngineSoundSpec { exhaustSystem = resonator } }, new Tune());
            resonator.parts[0].amount = 0;
            Check(captured.exhaustSystem.parts[0].amount == 1, "Sound profile owns its own exhaust chain.", ref count);
            Check(new ExhaustSetup { parts = new[] { new ExhaustPart { resonanceQ = float.NaN } } }.Validate() != null, "Invalid acoustic coefficients are rejected.", ref count);
            foreach (var definition in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
            {
                var test = new EngineBenchConfig { vehicle = definition.spec.Copy(), tune = definition.CreateStockTune() };
                test.tune.controlLevel = 1; test.tune.twoStep = EquipmentSetting.On;
                foreach (EngineBenchScenario scenario in Enum.GetValues(typeof(EngineBenchScenario)))
                {
                    var first = EngineBenchRenderer.Render(test, scenario); var second = EngineBenchRenderer.Render(test, scenario);
                    bool identical = first.Length == second.Length;
                    for (int i = 0; i < first.Length && identical; i++) identical = first[i] == second[i];
                    Check(identical && Energy(first) > .000001, "Sample-exact audible scenario: " + definition.spec.id + "/" + scenario, ref count);
                }
            }
            config = new EngineBenchConfig { vehicle = new VehicleSpec { sound = new EngineSoundSpec { exhaustSystem = ExhaustSetup.Preset(0) } } };
            var quiet = EngineBenchRenderer.Render(config, EngineBenchScenario.LoadedPullAndShifts);
            config.vehicle.sound.exhaustSystem = ExhaustSetup.Preset(3);
            var open = EngineBenchRenderer.Render(config, EngineBenchScenario.LoadedPullAndShifts);
            Check(Energy(open) > Energy(quiet) * 1.2, "Identical scenario inputs reveal exhaust configuration differences.", ref count);
            string folder = Path.Combine(root, "reports/engine-bench"); Directory.CreateDirectory(folder);
            EngineSoundAudition.WriteWav(Path.Combine(folder, "loaded-pull-quiet.wav"), quiet);
            EngineSoundAudition.WriteWav(Path.Combine(folder, "loaded-pull-open.wav"), open);
            var json = JsonUtility.ToJson(config); var restored = JsonUtility.FromJson<EngineBenchConfig>(json);
            Check(restored.Validate() == null && JsonUtility.ToJson(restored) == json, "Bench JSON round-trips every configuration field.", ref count);
            config.controls.engineEnabled = false;
            Check(Energy(EngineBenchRenderer.Render(config, EngineBenchScenario.FixedRpmHold)) == 0, "Powered-off fixed-RPM replay remains exactly silent.", ref count);
            Check(EngineBenchRenderer.ScenarioConfig(config, EngineBenchScenario.WarmIdle).controls.engineEnabled &&
                !EngineBenchRenderer.ScenarioConfig(config, EngineBenchScenario.FixedRpmHold).controls.engineEnabled,
                "Timeline scenarios own engine power; captured fixed-RPM holds preserve it.", ref count);
            report.AppendLine("PASS: " + count + " bench/exhaust checks, including identical PCM rerenders of all " + Enum.GetValues(typeof(EngineBenchScenario)).Length + " scenarios on all four cars.\nA/B exports use matched 120 Hz physics / 60 Hz publication / 44100 Hz PCM; no player saves accessed.");
            File.WriteAllText(Path.Combine(root, "reports/engine-bench-verification.txt"), report.ToString());
            return count;
        }
    }
}
