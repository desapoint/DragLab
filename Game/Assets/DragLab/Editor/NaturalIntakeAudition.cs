using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    public static class NaturalIntakeAudition
    {
        const int Rate = 48000, Block = 400;
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Capture => Path.Combine(Root, "reports/v8-backfire-matrix-20261020");
        static NaturalIntakePreview.Voice voice = NaturalIntakePreview.Voice.Airflow;
        static int levelDb;
        static string Folder => levelDb != 0 ? Path.Combine(Root, "reports/intake-levels-20261020/minus" + -levelDb + "db") : Path.Combine(Root, voice == NaturalIntakePreview.Voice.Airflow ? "reports/natural-intake-20261020" : voice == NaturalIntakePreview.Voice.ValveBody ? "reports/intake-body-20261020" : "reports/intake-rush-20261020");
        static readonly List<string> checks = new List<string>();
        [Serializable] sealed class Row { public string id, file; public double seconds, newPeak; public int referenceMismatches; }
        [Serializable] sealed class Manifest { public string voice; public int intakeLevelDb; public double commonIntakeGain; public List<Row> outputs = new List<Row>(); }
        static void Check(bool yes, string why)
        { checks.Add((yes ? "PASS " : "FAIL ") + why); if (!yes) { SaveChecks(); throw new InvalidOperationException("INTAKE PREVIEW: " + why); } }
        static void SaveChecks() { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, "verification.txt"), string.Join("\n", checks) + "\nChecks: " + checks.Count + "\n", new UTF8Encoding(false)); }
        static double Soft(double x) => x / (1 + Math.Abs(x));
        static short Pcm(double x, int sample, int total)
        {
            short value = (short)Math.Round(x * 32767);
            if (sample < 960) value = (short)Math.Round(value * sample / 959.0);
            if (sample >= total - 3840) value = (short)Math.Round(value * (total - 1 - sample) / 3839.0);
            return value;
        }
        static void Wav(string path, short[] pcm)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + pcm.Length * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(Rate); w.Write(Rate * 4); w.Write((short)4); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(pcm.Length * 2); foreach (short value in pcm) w.Write(value);
            }
        }
        static double PairFraction(double[] signal, double hz, int rate)
        {
            int start = voice != NaturalIntakePreview.Voice.Airflow ? 0 : signal.Length / 2;
            int count = voice != NaturalIntakePreview.Voice.Airflow ? Math.Min(signal.Length, rate / 4) : signal.Length - start;
            double total = 0, pair = 0;
            for (int i = start; i < start + count; i++) total += signal[i] * signal[i];
            foreach (double frequency in new[] { hz, hz * 2 })
            {
                double a = 0, b = 0;
                for (int i = start; i < start + count; i++) { double phase = 2 * Math.PI * frequency * i / rate; a += signal[i] * Math.Sin(phase); b += signal[i] * Math.Cos(phase); }
                pair += a * a + b * b;
            }
            return 2 * pair / Math.Max(1e-20, count * total);
        }
        static EngineSoundProfile Isolated(VehicleSpec spec, Tune tune)
        { var copy = spec.Copy(); copy.sound.engineGain = copy.sound.exhaustPopGain = copy.sound.turboGain = copy.sound.superchargerGain = copy.sound.blowOffGain = 0; copy.engineBehavior.mechanicalClatter = 0; return new EngineSoundProfile(copy, tune); }
        static void Verify()
        {
            var spec = new VehicleSpec(); spec.sound.cylinders = 6; spec.displacementLitres = 3;
            var tune = new Tune(); var profile = new EngineSoundProfile(spec, tune);
            foreach (int rate in new[] { 44100, 48000 })
            {
                var frame = new EngineSoundFrame(4200, .8, .7, 0, .85, intakeGulp: .8, airflowKgS: .12);
                var a = new NaturalIntakePreview(profile, rate, 171818, voice); var b = new NaturalIntakePreview(profile, rate, 171818, voice); var c = new NaturalIntakePreview(profile, rate, 9, voice);
                var first = new double[rate]; bool same = true, changed = false;
                for (int i = 0; i < rate; i++) { first[i] = a.Sample(frame); same &= first[i] == b.Sample(frame); changed |= first[i] != c.Sample(frame); }
                Check(same && changed, "same seed exact / different seed changes breath texture / " + rate);
                Check(first.All(v => !double.IsNaN(v) && !double.IsInfinity(v) && Math.Abs(v) < 1) && first.Sum(v => v * v) > 1e-5, "finite audible intake with headroom / " + rate);
                var original = new float[rate]; new EngineSoundSynth(Isolated(spec, tune), sampleRate: rate).Fill(original, 1, rate, frame);
                double oldPair = PairFraction(original.Select(v => (double)v).ToArray(), 150, rate), newPair = PairFraction(first, 150, rate);
                Check(newPair < oldPair * .15, "old intake fundamental/harmonic concentration reduced at least 85% / " + rate + " / " + oldPair + " to " + newPair);
                if (voice == NaturalIntakePreview.Voice.IntakeRush)
                {
                    var body = new NaturalIntakePreview(profile, rate, 171818, NaturalIntakePreview.Voice.ValveBody);
                    var rejected = new double[rate]; for (int i = 0; i < rate; i++) rejected[i] = body.Sample(frame);
                    double beforePair = PairFraction(rejected, 210, rate), afterPair = PairFraction(first, 210, rate);
                    Check(afterPair < beforePair * .2, "valve-cadence tone concentration reduced at least 80% against body candidate / " + rate + " / " + beforePair + " to " + afterPair);
                }
                var quiet = new NaturalIntakePreview(profile, rate, 171818, voice); var zero = new EngineSoundFrame(4200, .8, .7, 0, .85, intakeGulp: 0, airflowKgS: .12);
                bool silent = true; for (int i = 0; i < 2048; i++) silent &= quiet.Sample(zero) == 0;
                Check(silent, "no gulp excitation produces exact silence, no continuous hiss / " + rate);
                var ramp = new double[rate / 2]; for (int i = 0; i < ramp.Length; i++) ramp[i] = a.Sample(zero);
                Check(ramp.Skip(ramp.Length - 2048).Max(v => Math.Abs(v)) < 1e-8, "intake decays after throttle-opening excitation ends / " + rate);
                long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 8192; i++) a.Sample(frame);
                Check(GC.GetAllocatedBytesForCurrentThread() == before, "prepared sample generation allocates zero managed bytes / " + rate);
            }
        }
        static double Rms(float[] x, int firstFrame, int count)
        { double sum = 0; for (int i = firstFrame * 2; i < (firstFrame + count) * 2; i++) sum += x[i] * x[i]; return Math.Sqrt(sum / (count * 2)); }
        static double MatchOutput(float[] old, float[] source, int start, int count)
        {
            double goal = Rms(old, start, count), low = 0, high = 100;
            for (int pass = 0; pass < 40; pass++)
            {
                double gain = (low + high) * .5, sum = 0;
                for (int i = start * 2; i < (start + count) * 2; i++) { double value = Soft(source[i] * gain); sum += value * value; }
                if (Math.Sqrt(sum / (count * 2)) < goal) low = gain; else high = gain;
            }
            return (low + high) * .5;
        }
        public static void Run() { levelDb = 0; voice = NaturalIntakePreview.Voice.Airflow; Render(); }
        public static void RunBody() { levelDb = 0; voice = NaturalIntakePreview.Voice.ValveBody; Render(); CompareRejected(); }
        public static void RunRush() { levelDb = 0; voice = NaturalIntakePreview.Voice.IntakeRush; Render(); CompareRejected(); }
        public static void RunRushLevels()
        {
            voice = NaturalIntakePreview.Voice.IntakeRush;
            try
            {
                foreach (int db in new[] { -9, -15 }) { levelDb = db; Render(); CompareRushLevel(); }
            }
            finally { levelDb = 0; }
        }
        static void CompareRushLevel()
        {
            foreach (string time in new[] { "40s", "88s" }) foreach (string layer in new[] { "engine", "intake" })
            {
                string name = "timber-at-" + time + "-" + layer + "-before-after.wav";
                byte[] before = File.ReadAllBytes(Path.Combine(Root, "reports/intake-rush-20261020", name)), after = File.ReadAllBytes(Path.Combine(Folder, name));
                int frames = Rate * 3, second = (Rate * 3 + Rate / 2) * 4;
                var pcm = new short[(Rate * 6 + Rate / 2) * 2];
                Buffer.BlockCopy(before, 44 + second, pcm, 0, frames * 4);
                Buffer.BlockCopy(after, 44 + second, pcm, second, frames * 4);
                Wav(Path.Combine(Folder, "timber-at-" + time + "-" + layer + "-quieter.wav"), pcm);
            }
        }
        static void CompareRejected()
        {
            foreach (string time in new[] { "40s", "88s" }) foreach (string layer in new[] { "engine", "intake" })
            {
                string name = "timber-at-" + time + "-" + layer + "-before-after.wav";
                string previousFolder = voice == NaturalIntakePreview.Voice.IntakeRush ? "reports/intake-body-20261020" : "reports/natural-intake-20261020";
                byte[] before = File.ReadAllBytes(Path.Combine(Root, previousFolder, name)), after = File.ReadAllBytes(Path.Combine(Folder, name));
                int frames = Rate * 3, second = (Rate * 3 + Rate / 2) * 4;
                var pcm = new short[(Rate * 6 + Rate / 2) * 2];
                Buffer.BlockCopy(before, 44 + second, pcm, 0, frames * 4);
                Buffer.BlockCopy(after, 44 + second, pcm, second, frames * 4);
                Wav(Path.Combine(Folder, "timber-at-" + time + "-" + layer + (voice == NaturalIntakePreview.Voice.IntakeRush ? "-body-to-rush.wav" : "-hiss-to-body.wav")), pcm);
            }
        }
        static void Render()
        {
            Directory.CreateDirectory(Folder); checks.Clear(); Verify();
            string globalRandom = JsonUtility.ToJson(UnityEngine.Random.state);
            var captures = JsonUtility.FromJson<V8BackfireMatrixExport.Manifest>(File.ReadAllText(Path.Combine(Capture, "jobs.json"), Encoding.UTF8));
            var manifest = new Manifest { voice = voice.ToString(), intakeLevelDb = levelDb };
            foreach (string id in new[] { "07-i6-carb", "30-exhaust-2", "19-injection-soft-fuel" })
            {
                var job = captures.jobs.Single(j => j.id == id); byte[] reference = File.ReadAllBytes(Path.Combine(Capture, job.reference));
                var fixture = JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(Path.Combine(Capture, id + ".race.json"), Encoding.UTF8));
                var race = fixture.CreateRace();
                var original = new float[job.frames * 2]; var noGulp = new float[original.Length]; var oldIntake = new float[original.Length]; var natural = new float[original.Length];
                var row = new Row { id = id, file = id + "-original-natural-intake.wav", seconds = job.frames / (double)Rate };
                try
                {
                    var car = race.player; var controller = new EngineSoundController(car.spec, car.tune);
                    var profile = new EngineSoundProfile(car.spec, car.tune);
                    var originalSynth = new EngineSoundSynth(profile, sampleRate: Rate); var dry = new EngineSoundSynth(profile, sampleRate: Rate);
                    var old = new EngineSoundSynth(Isolated(car.spec, car.tune), sampleRate: Rate); var fresh = new NaturalIntakePreview(profile, Rate, 171818, voice);
                    var block = new float[Block * 2];
                    for (int tick = 0; tick < fixture.ticks.Count; tick++)
                    {
                        var command = fixture.ticks[tick]; car.state.warmupLineLock = command.lineLock;
                        if (command.beginCountdown) Check(race.BeginCountdown(), id + " countdown");
                        if (race.phase == RacePhase.Finished) race.Shutdown(command.input); else race.Step(command.input);
                        var f = controller.Update(car.state, true, .85, DragMath.FixedStep);
                        originalSynth.Fill(block, 2, Rate, f); Array.Copy(block, 0, original, tick * Block * 2, block.Length);
                        dry.Fill(block, 2, Rate, BackfireMixtureAudition.WithoutGulp(f)); Array.Copy(block, 0, noGulp, tick * Block * 2, block.Length);
                        old.Fill(block, 2, Rate, f); Array.Copy(block, 0, oldIntake, tick * Block * 2, block.Length);
                        for (int i = 0; i < Block; i++) { float value = (float)fresh.Sample(f); natural[(tick * Block + i) * 2] = natural[(tick * Block + i) * 2 + 1] = value; }
                    }
                    Check(Math.Abs(car.state.elapsed - fixture.elapsed) < 1e-9 && car.state.exhaustPopCount == fixture.pops, id + " unchanged race result and backfire counters");
                    if (id == "07-i6-carb")
                    {
                        manifest.commonIntakeGain = voice != NaturalIntakePreview.Voice.Airflow ? MatchOutput(oldIntake, natural, (int)(1.9 * Rate), Rate * 3) :
                            Rms(oldIntake, (int)(1.9 * Rate), Rate * 3) / Rms(natural, (int)(1.9 * Rate), Rate * 3);
                        Check(manifest.commonIntakeGain > 0 && manifest.commonIntakeGain < 100, "one finite global level match from Timber target clip");
                        // Calibration is retained for reproducibility, then only the intake
                        // gain is attenuated before summing with the unchanged engine bed.
                        if (levelDb != 0) manifest.commonIntakeGain *= Math.Pow(10, levelDb / 20.0);
                    }
                    var updated = new float[original.Length];
                    for (int i = 0; i < original.Length; i++)
                    {
                        double baseline = noGulp[i], extra = natural[i] * manifest.commonIntakeGain;
                        updated[i] = extra == 0 ? noGulp[i] : (float)Soft(baseline / (1 - Math.Abs(baseline)) + extra);
                        row.newPeak = Math.Max(row.newPeak, Math.Abs(updated[i]));
                        if (Pcm(original[i], i / 2, job.frames) != BitConverter.ToInt16(reference, 44 + i * 2)) row.referenceMismatches++;
                    }
                    Check(row.referenceMismatches == 0, id + " original production PCM reproduces exactly");
                    Check(updated.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) < .99), id + " new complete sequence is finite without clipping");
                    var combined = new short[(job.frames * 2 + Rate) * 2];
                    Buffer.BlockCopy(reference, 44, combined, 0, job.frames * 4);
                    for (int i = 0; i < updated.Length; i++) combined[(job.frames + Rate) * 2 + i] = Pcm(updated[i], i / 2, job.frames);
                    Wav(Path.Combine(Folder, row.file), combined);
                    if (id == "07-i6-carb")
                    {
                        foreach (var point in new[] { Tuple.Create("at-40s", 1.9), Tuple.Create("at-88s", 13.0) })
                        {
                            int from = (int)Math.Round(point.Item2 * Rate) * 2, count = Rate * 3 * 2;
                            foreach (bool isolated in new[] { true, false })
                            {
                                var first = isolated ? oldIntake : original; var second = isolated ? natural : updated;
                                var clip = new short[(Rate * 6 + Rate / 2) * 2];
                                for (int i = 0; i < count; i++)
                                {
                                    int frame = i / 2;
                                    double edge = Math.Min(1, Math.Min(frame, Rate * 3 - 1 - frame) / (Rate * .005 - 1));
                                    clip[i] = (short)Math.Round(first[from + i] * edge * 32767);
                                    double value = isolated ? Soft(second[from + i] * manifest.commonIntakeGain) : second[from + i];
                                    clip[count + Rate + i] = (short)Math.Round(value * edge * 32767);
                                }
                                Wav(Path.Combine(Folder, "timber-" + point.Item1 + (isolated ? "-intake-before-after.wav" : "-engine-before-after.wav")), clip);
                            }
                        }
                    }
                    manifest.outputs.Add(row); File.WriteAllText(Path.Combine(Folder, "manifest.json"), JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
                    Debug.Log("NATURAL INTAKE PREVIEW " + id + " gain " + manifest.commonIntakeGain + " peak " + row.newPeak);
                }
                finally { race.CloseOpponentWorker(); }
            }
            Check(globalRandom == JsonUtility.ToJson(UnityEngine.Random.state), "preview does not consume gameplay RNG");
            SaveChecks(); Debug.Log("NATURAL INTAKE AUDITION PASS");
        }
    }
}
