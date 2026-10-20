using System;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    // Causality gates and explicit diagnostic gaps, not an acoustic-realism certification.
    public static class AudioCausalityVerification
    {
        static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/audio-causality-20261020"));

        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var report = new StringBuilder("Audio causality audit: production behavior gates; open model limitations are separate.\n");
            int checks = 0, failures = 0;
            Action<bool, string> check = (ok, label) => {
                report.AppendLine((ok ? "PASS " : "FAIL ") + label); checks++; if (!ok) failures++;
            };
            var spec = new VehicleSpec();
            spec.engineBehavior.fueling = FuelSystem.Carburetor;
            spec.engineBehavior.mixtureVariation = .4f;
            spec.engineBehavior.camOverlap = .3f;
            var tune = new Tune();
            var profile = new EngineBehaviorProfile(spec, tune);
            foreach (string unavailable in new[] { "failed", "stalled", "no supply" })
            {
                foreach (bool opening in new[] { false, true })
                {
                    var state = new CarState { rpm = spec.redlineRpm + 100, previousGas = opening ? 0 : 1,
                        engineFailed = unavailable == "failed", stalled = unavailable == "stalled",
                        engineSupplyAvailable = unavailable != "no supply" };
                    EngineBehaviorDynamics.BeginTick(spec, tune, profile, state, opening ? 1 : 0, true, DragMath.FixedStep);
                    check(state.exhaustPopCount == 0, unavailable + " engine cannot create fresh transition/limiter pops / opening=" + opening);
                }
            }
            var healthy = new CarState { rpm = spec.redlineRpm + 100, throttle = 1 };
            EngineBehaviorDynamics.BeginTick(spec, tune, profile, healthy, 1, false, DragMath.FixedStep);
            check(healthy.exhaustPopCount > 0, "running carburetor retains existing ignition-cut event behavior");
            int validPops = healthy.exhaustPopCount;
            foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -.5, 0 })
                EngineBehaviorDynamics.Pop(healthy, bad);
            check(healthy.exhaustPopCount == validPops, "invalid or nonpositive requests cannot create backfires");
            foreach (FuelSystem fuel in new[] { FuelSystem.Injection, FuelSystem.Diesel })
            {
                var quiet = spec.Copy(); quiet.engineBehavior.fueling = fuel; quiet.engineBehavior.cut = CutMethod.Fuel;
                quiet.engineBehavior.launchCut = CutMethod.Fuel;
                var state = new CarState { rpm = quiet.redlineRpm + 100, previousGas = 1 };
                EngineBehaviorDynamics.BeginTick(quiet, tune, new EngineBehaviorProfile(quiet, tune), state, 1, false, DragMath.FixedStep);
                check(state.exhaustPopCount == 0, fuel + " fuel-cut entry does not invent an ignition afterfire");
            }

            // Diagnose the current transport without enshrining loss as an expected PASS.
            var controller = new EngineSoundController(spec, tune);
            var transport = new CarState { rpm = 4000, throttle = 1 };
            var initial = controller.Update(transport, true, 1, DragMath.FixedStep);
            for (int i = 0; i < 3; i++) EngineBehaviorDynamics.Pop(transport, .5 + i * .1);
            var delivered = controller.Update(transport, true, 1, DragMath.FixedStep);
            report.AppendLine("OBSERVATION event transport: " + transport.exhaustPopCount + " physical pops; " +
                (delivered.popId - initial.popId) + " published trigger IDs. Current frame stores only the last strength; event queue remains an integration blocker.");
            string before = JsonUtility.ToJson(transport), random = JsonUtility.ToJson(UnityEngine.Random.state);
            var synth = new EngineSoundSynth(new EngineSoundProfile(spec, tune), sampleRate: 48000);
            var pcm = new float[2048]; synth.Fill(pcm, 2, 48000, delivered);
            check(before == JsonUtility.ToJson(transport), "DSP does not mutate authoritative vehicle state");
            check(random == JsonUtility.ToJson(UnityEngine.Random.state), "DSP does not advance the gameplay random stream");
            check(pcm.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) < 1), "event playback is finite with output headroom");
            check(controller.Update(transport, true, 1, DragMath.FixedStep).popId == delivered.popId,
                "republishing unchanged state does not invent another event");

            // Low-rate render publication is a separate loss point from fixed-tick generation.
            var skipping = new EngineSoundController(spec, tune);
            var start = skipping.Update(new CarState { rpm = 4000 }, true, 1, .016);
            var skipState = new CarState { rpm = 4000, exhaustPopCount = 5, exhaustPopStrength = .6 };
            var end = skipping.Update(skipState, true, 1, .016);
            report.AppendLine("OBSERVATION skipped simulation ticks: 5 physical increments; " + (end.popId - start.popId) + " published IDs. Sub-tick time/cause/energy is absent.");
            report.AppendLine("OPEN no fuel/oxygen exhaust inventory, transport, ignition-site or energy-conservation state. Timer/edge events are heuristics, not a chemistry solver.");
            report.AppendLine("OPEN waveform/detail and platform limits are enumerated in CHANGES.md; passing these gates does not close them.");
            report.AppendLine("Focused checks: " + checks + "; failures: " + failures + ".");
            File.WriteAllText(Path.Combine(Folder, "verification.txt"), report.ToString(), new UTF8Encoding(false));
            if (failures > 0) throw new InvalidOperationException("Audio causality gates failed: " + failures);
            Debug.Log("AUDIO CAUSALITY FOCUSED PASS " + checks);
        }

        public static void FullAudit()
        {
            Run();
            // Isolate rerun artifacts; retain historical reports and player preferences.
            Directory.CreateDirectory(Path.Combine(Folder, "reports"));
            int engine = EngineSoundVerification.Run(Folder);
            int pop = ExhaustPopVerification.Run(Folder);
            int induction = InductionVerification.Run(Folder);
            int powertrain = PowertrainVerification.Run(Folder);
            TireFeedbackVerification.VerifyEffectsAt(Folder);
            PressureAudioVerification.Refinements();
            TireSoundVerification.Run();
            File.WriteAllText(Path.Combine(Folder, "suite-summary.txt"),
                "PASS production regression gates: engine " + engine + "; existing pop " + pop + "; induction " + induction +
                "; powertrain " + powertrain + ". Pressure/refinement/transition and per-wheel tire suites also completed.\n" +
                "This is behavioral regression evidence, not a claim of measured acoustic accuracy or Android device profiling.\n", new UTF8Encoding(false));
            Debug.Log("AUDIO CAUSALITY FULL AUDIT PASS");
        }
    }
}
