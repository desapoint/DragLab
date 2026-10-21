using System;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    public static class AfterfireBangVerification
    {
        public static string OutputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/afterfire-bangs-20261021"));
        static string Root => OutputDirectory;
        static readonly StringBuilder Log = new StringBuilder();
        static int checks;
        static void Check(bool yes, string text)
        {
            Log.AppendLine((yes ? "PASS " : "FAIL ") + text); checks++;
            File.WriteAllText(Path.Combine(Root, "source-verification.txt"), Log + "Checks: " + checks + "\n", new UTF8Encoding(false));
            if (!yes) throw new InvalidOperationException(text);
        }
        static void Wav(string name, float[] pcm)
        {
            using (var file = new BinaryWriter(File.Create(Path.Combine(Root, name))))
            {
                file.Write(Encoding.ASCII.GetBytes("RIFF")); file.Write(36 + pcm.Length * 4);
                file.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); file.Write(16); file.Write((short)3); file.Write((short)2);
                file.Write(48000); file.Write(48000 * 8); file.Write((short)8); file.Write((short)32);
                file.Write(Encoding.ASCII.GetBytes("data")); file.Write(pcm.Length * 4);
                foreach (float value in pcm) file.Write(value);
            }
        }
        static void Capture(string label)
        {
            Directory.CreateDirectory(Root);
            var receipt = new StringBuilder("Same input replay, afterfire-only installed path and trackside listener. No per-file normalization.\n");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/race-audio-audition"));
            foreach (string prefix in new[] { "22-", "26-" })
            {
                string path = Directory.GetFiles(folder, prefix + "*.race.json").Single();
                var fixture = JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(path, Encoding.UTF8));
                var race = fixture.CreateRace();
                try
                {
                    var car = race.player; var profile = new EngineSoundProfile(car.spec, car.tune);
                    var controller = new EngineSoundController(car.spec, car.tune);
                    controller.Update(car.state, true, .85, DragMath.FixedStep);
                    var synth = new AfterfireAudioSynth(profile, 48000); synth.Connect(car.state.audioExhaust.mixture);
                    var outdoor = new TracksidePropagation(48000);
                    var geometry = new TracksidePropagation.Scene(car.spec.induction.ambientTemperatureK, .5, car.spec.induction.ambientPressureBar * 100000).At();
                    var pcm = new float[(fixture.ticks.Count + 120) * 800]; var block = new float[800]; int offset = 0;
                    foreach (var tick in fixture.ticks)
                    {
                        car.state.warmupLineLock = tick.lineLock;
                        if (tick.beginCountdown && !race.BeginCountdown()) throw new InvalidOperationException("Countdown failed");
                        if (race.phase == RacePhase.Finished) race.Shutdown(tick.input); else race.Step(tick.input);
                        controller.Update(car.state, true, .85, DragMath.FixedStep);
                        for (int i = 0; i < 400; i++) block[i * 2] = block[i * 2 + 1] = (float)synth.Sample();
                        outdoor.Process(block, 2, geometry); Array.Copy(block, 0, pcm, offset, 800); offset += 800;
                    }
                    for (int tail = 0; tail < 120; tail++)
                    {
                        for (int i = 0; i < 400; i++) block[i * 2] = block[i * 2 + 1] = (float)synth.Sample();
                        outdoor.Process(block, 2, geometry); Array.Copy(block, 0, pcm, offset, 800); offset += 800;
                    }
                    Wav(label + "-" + fixture.id + "-afterfire.wav", pcm);
                    receipt.AppendLine(fixture.id + " / reactions " + synth.Delivered + " / chemical " + car.state.audioExhaust.mixture.Sequence + " / ET " + car.state.elapsed.ToString("R") + " / peak " + pcm.Max(v => Math.Abs(v)).ToString("R"));
                    if (label == "after") Check(synth.Delivered == car.state.audioExhaust.mixture.Sequence && synth.HistoryOverflows == 0 && synth.TailSteals == 0, "isolated actual race retains every reaction and voice / " + fixture.id);
                }
                finally { race.CloseOpponentWorker(); }
            }
            File.WriteAllText(Path.Combine(Root, label + "-capture.txt"), receipt.ToString(), new UTF8Encoding(false));
        }
        public static void CaptureBefore() { Capture("before"); }
        static ExhaustMixtureModel.Event Reaction(double energy = .2, double pressure = 60000, double rise = .0007, double burn = .012)
        {
            return new ExhaustMixtureModel.Event { acousticJ = energy, pressurePa = pressure, riseSeconds = rise, burnSeconds = burn, flowMps = 40, seed = 12345 };
        }
        static double Energy(ReactivePressureBurst source, int frames)
        {
            double sum = 0; for (int i = 0; i < frames; i++) { double x = source.Sample(); sum += x * x; } return sum;
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root); Log.Clear(); checks = 0;
            foreach (int rate in new[] { 24000, 44100, 48000 })
            {
                var first = new ReactivePressureBurst(rate); var repeat = new ReactivePressureBurst(rate);
                var eventA = Reaction(); first.Begin(eventA); repeat.Begin(eventA);
                double pressureEnergy = 0, bodyEnergy = 0, noiseEnergy = 0, peak = 0, audibleEnergy = 0; bool same = true, finite = true;
                for (int i = 0; i < rate / 2; i++)
                {
                    double x = first.Sample(); same &= x == repeat.Sample(); finite &= !double.IsNaN(x) && !double.IsInfinity(x);
                    peak = Math.Max(peak, Math.Abs(x)); pressureEnergy += first.PressureSample * first.PressureSample;
                    bodyEnergy += first.CombustionBodySample * first.CombustionBodySample;
                    noiseEnergy += first.TurbulenceSample * first.TurbulenceSample;
                    // The production engine mix removes DC before outdoor propagation.
                    if (i < rate / 100) audibleEnergy += x * x;
                }
                Check(same && finite && peak > 0 && audibleEnergy > 0, "seeded finite transient with audible attack / " + rate);
                Check(noiseEnergy < (pressureEnergy + bodyEnergy) * .01, "compression and combustion body dominate airflow/high grit by at least 20 dB / " + rate);
                Check(bodyEnergy > pressureEnergy * .05, "blast has an audible low/mid combustion body rather than an isolated high edge / " + rate);
                Log.AppendLine("MEASURE " + rate + " pressure / body / high-grit energy " + pressureEnergy.ToString("R") + " / " + bodyEnergy.ToString("R") + " / " + noiseEnergy.ToString("R") + " / peak " + peak.ToString("R"));
                var a = new ReactivePressureBurst(rate); var b = new ReactivePressureBurst(rate);
                a.Begin(eventA); var half = eventA; half.acousticJ *= .5; b.Begin(half); b.Excite(half);
                double error = 0;
                for (int i = 0; i < rate / 5; i++) error = Math.Max(error, Math.Abs(a.Sample() - b.Sample()));
                Check(error < 1e-12 && Math.Abs(b.FundedEnergy - .2) < 1e-12, "splitting a simultaneous reaction packet cannot change its bang or energy / " + rate);
                var quieter = eventA; quieter.acousticJ *= .25; a.Begin(eventA); b.Begin(quieter);
                bool proportional = true;
                for (int i = 0; i < rate / 5; i++) proportional &= Math.Abs(a.Sample() * .5 - b.Sample()) < 1e-12;
                Check(proportional, "quarter reaction energy gives half the pressure amplitude, including texture / " + rate);
                a.Begin(eventA); var differentSeed = eventA; differentSeed.seed++; b.Begin(differentSeed);
                bool backbone = true, texture = false;
                for (int i = 0; i < rate / 5; i++) { a.Sample(); b.Sample(); backbone &= a.PressureSample == b.PressureSample; texture |= a.TurbulenceSample != b.TurbulenceSample; }
                Check(backbone && texture, "seed changes irregular texture while preserving physical pressure response / " + rate);
                a.Begin(eventA); b.Begin(Reaction(rise: .006));
                double earlyFast = Energy(a, rate / 1000); double earlySlow = Energy(b, rate / 1000);
                Check(earlyFast > earlySlow * 2, "fast simulated pressure rise has a sharper compression edge / " + rate);
                a.Begin(Reaction(pressure: 1000)); b.Begin(eventA);
                Check(Energy(b, rate / 1000) > Energy(a, rate / 1000), "larger physical overpressure steepens the compression front / " + rate);
                var zero = new ReactivePressureBurst(rate); zero.Begin(Reaction(energy: 0));
                Check(Energy(zero, rate / 5) == 0, "no acoustic reaction energy produces no bang or hiss / " + rate);
                a.Begin(eventA); b.Begin(Reaction(burn: .04));
                Energy(a, rate / 20); Energy(b, rate / 20);
                Check(Energy(b, rate / 10) > Energy(a, rate / 10) * 2, "longer physical burn retains a longer pressure-release decay / " + rate);
                // One continuously fed flame settles instead of generating a new canned
                // bang at each numerical tick, while residual flow remains much quieter.
                var continuous = new ReactivePressureBurst(rate); var feed = Reaction(energy: .002);
                continuous.Begin(feed); double attack = 0, settled = 0, flow = 0, lateBody = 0;
                for (int i = 0; i < rate; i++)
                {
                    if (i > 0 && i % (rate / 120) == 0) { feed.time = i / (double)rate; continuous.Excite(feed); }
                    double value = continuous.Sample();
                    if (i < rate / 20) attack += continuous.PressureSample * continuous.PressureSample;
                    if (i >= rate * .95) { settled += continuous.PressureSample * continuous.PressureSample; flow += continuous.TurbulenceSample * continuous.TurbulenceSample; lateBody += continuous.CombustionBodySample * continuous.CombustionBodySample; }
                }
                Check(settled < attack * .1 && flow < lateBody * .01 && lateBody > 0, "steady burning retains rough body without repeated compression bangs or an air jet / " + rate);
                for (int i = 0; i < rate * 2; i++) continuous.Sample();
                Check(continuous.Quiet, "pressure and residual flow stop when finite fuel funding stops / " + rate);
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < rate; i++) { if (i % 200 == 0) { feed.time = 3 + i / (double)rate; continuous.Excite(feed); } continuous.Sample(); }
                Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "zero warmed event and sample allocations / " + rate);
                continuous.Clear(); Check(continuous.Sample() == 0, "clear discards the old reaction and tail / " + rate);
            }
            CheckWalls();
        }
        static void CheckWalls()
        {
            var spec = new VehicleSpec(); spec.sound.exhaustSystem = new ExhaustSetup { version = 1, parts = new[] { ExhaustPart.Create(ExhaustPartKind.ChamberedSilencer) } };
            spec.sound.exhaustSystem.parts[0].attenuationDb = 0;
            var muffled = spec.Copy(); muffled.sound.exhaustSystem.parts[0].attenuationDb = 30;
            var tune = new Tune(); var open = new AfterfireAudioSynth(new EngineSoundProfile(spec, tune), 48000);
            var damped = new AfterfireAudioSynth(new EngineSoundProfile(muffled, tune), 48000);
            var pocket = new ExhaustMixtureModel(1718, "local-wall", initialK: 1000);
            pocket.zones[0].fuelKg = 5e-5; pocket.zones[0].oxygenKg = 5e-5 * pocket.stoichOxygenPerFuel;
            pocket.zones[0].flame = 1; pocket.zones[0].frontId = 1;
            open.Connect(pocket); damped.Connect(pocket);
            pocket.Step(new ExhaustMixtureModel.Input { dt = .0005, intakeK = 350, wallTargetK = 1000 });
            Check(pocket.Sequence > 0, "wall fixture has a genuine fuel-consuming local reaction");
            double wall = 0, outlet = 0, attenuated = 0; int firstWall = -1, firstOutlet = -1; bool sameWall = true, summed = true;
            for (int i = 0; i < 48000; i++)
            {
                double value = open.Sample(); damped.Sample();
                sameWall &= open.LastWallSample == damped.LastWallSample;
                summed &= Math.Abs(value - open.LastWallSample - open.LastOutletSample) < 1e-12;
                wall += open.LastWallSample * open.LastWallSample; outlet += open.LastOutletSample * open.LastOutletSample; attenuated += damped.LastOutletSample * damped.LastOutletSample;
                if (firstWall < 0 && Math.Abs(open.LastWallSample) > 1e-12) firstWall = i;
                if (firstOutlet < 0 && Math.Abs(open.LastOutletSample) > 1e-12) firstOutlet = i;
            }
            Check(wall > 0 && sameWall && attenuated < outlet * .1, "local wall sound bypasses downstream silencing while the outlet still obeys it");
            Check(firstWall >= 0 && firstOutlet > firstWall, "local shell response arrives before the longer in-pipe outlet path");
            Check(summed && wall < outlet && open.LastWallSample == 0 && open.LastOutletSample == 0, "bounded wall/outlet sum decays without a sustained bell");
        }
        public static void Checkpoint()
        {
            Run(); Capture("after");
            string outdoorReceipt = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/trackside-audio-20261021/outdoor-verification.txt"));
            byte[] historical = File.Exists(outdoorReceipt) ? File.ReadAllBytes(outdoorReceipt) : null;
            try
            {
                CrowdAudioVerification.Run();
                File.Copy(outdoorReceipt, Path.Combine(Root, "outdoor-verification.txt"), true);
            }
            finally
            {
                if (historical != null) File.WriteAllBytes(outdoorReceipt, historical);
            }
            ConditionDrivenAfterfireVerification.Run();
            File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/condition-afterfire-20261021/causality.txt")), Path.Combine(Root, "causality.txt"), true);
            RuntimeAudioIntegrationVerification.OutputDirectory = Root; RuntimeAudioIntegrationVerification.TracksideListening = true;
            RuntimeAudioIntegrationVerification.Run(); RuntimeAudioIntegrationVerification.CheckRaces();
        }
        public static void RebalanceAndBuild() { Checkpoint(); PlayerBuild.BuildPlayerOnly(); }
    }
}
