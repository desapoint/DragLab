using System;
using System.IO;
using System.Text;
using DragLab.Domain;

namespace DragLab.Editor
{
    public static class EngineDynoVerification
    {
        public static int Run(string root)
        {
            int count = 0;
            var report = new StringBuilder("Persistent bench control sources / chronological live dyno.\n");
            Action<bool, string> check = (ok, text) => { if (!ok) throw new InvalidOperationException("DYNO CHECK FAILED: " + text); count++; report.AppendLine("PASS: " + text); };
            var assigned = new EngineBenchControls { throttle = .65f, clutch = .3f, brake = .2f, fixedRpm = true, targetRpm = 4200, load = .7f, gear = 2, warmthOverride = .4f };
            var input = new EngineBenchInput(); input.Sliders(assigned);
            check(input.Resolve(assigned, false, false, false).throttle == .65f, "Sliders hold partial throttle with no keys.");
            check(input.Resolve(assigned, true, true, true).clutch == .3f, "Slider source ignores unselected pedal overrides.");
            var keyboard = input.Keyboard();
            check(!keyboard.fixedRpm && keyboard.warmthOverride == -1 && keyboard.gear == 2 && keyboard.load == .7f, "Keyboard takeover restores dynamic RPM and temperature while preserving current gear/load.");
            check(keyboard.throttle == 0 && keyboard.clutch == 0 && keyboard.brake == 0, "Keyboard takeover clears latched slider pedals.");
            var held = input.Resolve(assigned, true, false, true);
            check(held.throttle == 1 && held.clutch == 0 && held.brake == 1, "Keyboard pedals operate independently.");
            input.Publish(held);
            check(input.Effective.throttle == 1 && input.Effective.brake == 1, "Displayed feedback receives effective pedal inputs.");
            check(input.Resolve(assigned, false, false, false).throttle == 0 && input.Source == EngineBenchControlSource.Keyboard, "Release returns to zero and stays in keyboard mode instead of falling back to old slider gas.");
            input.Sliders(assigned);
            for (int i = 0; i < 120; i++) input.Publish(input.Resolve(assigned, false, false, false));
            check(input.Effective.throttle == .65f && input.Effective.fixedRpm && input.Source == EngineBenchControlSource.Sliders, "Slider takeover remains latched across ticks.");
            input.Scenario(new EngineBenchControls { throttle = 1, load = .8f, gear = 3, targetRpm = 5100, fixedRpm = true });
            check(input.Source == EngineBenchControlSource.Scenario && input.Effective.targetRpm == 5100 && input.Effective.gear == 3, "Preset timeline publishes gear, RPM target, load and gas feedback.");
            var feedback = input.Effective.Copy(); feedback.throttle = .4f;
            check(input.Effective.throttle == 1, "Editing a UI feedback copy cannot mutate the applied input snapshot.");

            var config = new EngineBenchConfig(); config.vehicle.transmission.type = TransmissionType.Manual;
            var dyno = new EngineBenchDyno(); var bench = new EngineTestBench(config.vehicle, config.tune);
            dyno.Begin("Loaded pull", config, bench.state, 0);
            for (int tick = 0; tick < 1440; tick++)
            {
                var controls = EngineBenchScenarios.At(EngineBenchScenario.LoadedPullAndShifts, tick, bench.spec);
                bench.Step(controls); dyno.Sample(bench.state, controls.load);
            }
            dyno.Finish("scenario completed"); var first = dyno.History[0];
            check(first.Samples.Count == 1441 && first.Samples[0].seconds == 0 && first.Samples[1440].seconds == 12, "Capture includes start, every 120 Hz tick and the complete endpoint.");
            check(first.Samples[1440].throttle == 0 && first.Samples[1440].horsepower == 0, "Throttle-off tail remains in the run.");
            bool cut = false, recovered = false;
            foreach (var s in first.Samples) { if (s.shiftCut && s.shaftTorqueNm == 0 && s.horsepower > 0 && s.throttle == 1) cut = true; if (cut && s.shaftTorqueNm > 0) recovered = true; }
            check(cut && recovered, "Gearbox handover and shaft recovery remain connected while the engine keeps producing power.");
            check(first.Complete && dyno.Active == null, "Completion archives the whole trace without a live continuation.");
            config.vehicle.sound.cylinders = 8;
            check(first.config.vehicle.sound.cylinders == 4, "Run configuration is independent of subsequent edits.");
            config = first.config.Copy(); bench = new EngineTestBench(config.vehicle, config.tune); dyno.Begin("Repeat", config, bench.state, 0);
            for (int tick = 0; tick < 1440; tick++)
            { var c = EngineBenchScenarios.At(EngineBenchScenario.LoadedPullAndShifts, tick, bench.spec); bench.Step(c); dyno.Sample(bench.state, c.load); }
            dyno.Finish("scenario completed");
            check(first.Csv() == dyno.History[1].Csv(), "Fresh replay produces identical full telemetry, including shifts and release.");
            dyno.Begin("RPM fall", config, new CarState { rpm = 5000, engineTorqueNm = 200 }, 1);
            dyno.Sample(new CarState { rpm = 3000, engineTorqueNm = 0, transmissionTorqueCut = true, gear = 2 }, 1);
            dyno.Sample(new CarState { rpm = 3000, engineTorqueNm = 140, gear = 2 }, 1);
            dyno.Sample(new CarState { rpm = 1200, engineTorqueNm = 0, throttle = 0 }, 0);
            dyno.Finish("manual lift"); var falling = dyno.History[2];
            check(falling.Samples[0].rpm == 5000 && falling.Samples[1].rpm == 3000 && falling.Samples[3].rpm == 1200, "Falling RPM is retained in chronological order, never sorted or binned.");
            check(falling.Samples[1].rpm == falling.Samples[2].rpm && falling.Samples[1].torqueNm == 0 && falling.Samples[2].torqueNm == 140, "Repeated RPM with different output remains separate samples.");
            check(falling.Csv().Split('\n').Length == 6, "CSV includes all samples and no WOT-only filtering.");
            for (int i = 0; i < 8; i++) { dyno.Begin("manual " + i, config, bench.state, 0); dyno.Sample(bench.state, 0); dyno.Finish("reset"); }
            check(dyno.History.Count == EngineBenchDyno.HistoryLimit && dyno.History[5].label == "manual 7", "History retains the six latest whole runs.");
            dyno.Begin("live", config, bench.state, 0); var live = dyno.Active; dyno.ClearHistory();
            check(dyno.History.Count == 0 && dyno.Active == live, "Clearing overlays leaves the live recording intact.");
            for (int i = 0; i < EngineBenchDyno.SegmentTicks; i++) dyno.Sample(bench.state, 0);
            check(dyno.History.Count == 1 && dyno.History[0].Samples.Count == EngineBenchDyno.SegmentTicks + 1 && dyno.Active.Samples.Count == 1,
                "Long sessions split into complete 10-minute segments without dropping their beginning.");
            File.WriteAllText(Path.Combine(root, "reports/engine-dyno-verification.txt"), report.AppendLine("PASS: " + count + " input/dyno checks.").ToString());
            return count;
        }
    }
}
