using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using UnityEngine;
using M = DragLab.Editor.V8BackfirePreviewModel;

namespace DragLab.Editor
{
    public static class BackfireMixtureAudition
    {
        const int Rate = 48000;
        const long Seed = 20261020;
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        static string Capture => Path.Combine(Root, "reports/v8-backfire-matrix-20261020");
        static string Folder => Path.Combine(Root, "reports/mixture-backfire-20261020");
        [Serializable] sealed class EventLog { public List<BackfireMixturePreview.Event> events; }
        [Serializable] sealed class Row
        {
            public string id, file;
            public int chemicalEvents, audibleEvents;
            public double fuelInKg, chamberBurnedKg, unburnedKg, exhaustBurnedKg, exhaustOutKg, retainedKg, acousticJ, maxTemperatureK, peak;
        }
        [Serializable] sealed class Manifest { public List<Row> outputs = new List<Row>(); }
        static readonly List<string> checks = new List<string>();
        static void Check(bool yes, string label)
        { checks.Add((yes ? "PASS " : "FAIL ") + label); if (!yes) { File.WriteAllText(Path.Combine(Folder, "verification.txt"), string.Join("\n", checks) + "\n", new UTF8Encoding(false)); throw new InvalidOperationException("MIXTURE PREVIEW: " + label); } }
        static BackfireMixturePreview Sim(long seed, double fuel, double air, double burn, double intake = 350, double wall = 1000, int steps = 240)
        {
            var model = new BackfireMixturePreview(seed, "verification", initialK: wall);
            for (int i = 0; i < steps; i++) model.Step(new BackfireMixturePreview.Input { dt = 1.0 / 120, fuelKg = fuel, airKg = air,
                chamberBurnFraction = burn, intakeK = intake, wallTargetK = wall });
            return model;
        }
        static void Conservation(BackfireMixturePreview m, string name)
        {
            Check(Math.Abs(m.FuelIn - m.ChamberBurned - m.FuelOut - m.FuelBurned - m.RetainedFuel) < 1e-10, name + " fuel mass conserved");
            Check(Math.Abs(m.OxygenIn - m.OxygenOut - m.OxygenBurned - m.RetainedOxygen) < 1e-10, name + " exhaust oxygen conserved");
            Check(Math.Abs(m.FuelBurned * m.heatingJPerKg - m.ExhaustHeatJ - m.AcousticEnergyJ) < 1e-7,
                name + " chemical release split exactly into heat and acoustic energy");
            Check(m.events.All(e => e.fuelKg > 0 && e.oxygenKg > 0 && e.releasedJ > 0 && e.acousticJ < e.releasedJ), name + " every ignition consumes residual fuel and oxygen");
            Check(m.zones.All(z => z.fuelKg >= -1e-14 && z.oxygenKg >= -1e-14 && !double.IsNaN(z.gasK) && !double.IsInfinity(z.gasK)), name + " finite nonnegative zones");
        }
        static bool Rejected(Action action)
        { try { action(); return false; } catch (ArgumentException) { return true; } }
        public static void Verify()
        {
            checks.Clear(); Directory.CreateDirectory(Folder);
            Check(Rejected(() => new BackfireMixturePreview(Seed, "bad", initialK: double.NaN)), "reject non-finite initial thermals");
            Check(Rejected(() => new BackfireMixturePreview(Seed, "bad", radiusM: double.MaxValue)), "reject overflowing geometry");
            Check(Rejected(() => Sim(Seed, double.MaxValue, double.MaxValue, .5)), "reject overflowing flow before substep conversion");
            var invalidInput = new BackfireMixturePreview(Seed, "invalid");
            Check(Rejected(() => invalidInput.Step(new BackfireMixturePreview.Input { dt = .01, fuelKg = double.MaxValue, airKg = double.MaxValue })) &&
                invalidInput.FuelIn == 0 && invalidInput.events.Count == 0 && invalidInput.RetainedFuel == 0, "rejected mixture does not corrupt ledgers or event state");
            Check(Rejected(() => new BackfireMixturePreview(Seed, "bad").Step(null)), "reject absent mixture input");
            var hot = Sim(Seed, 3e-6, 5e-5, .1);
            Check(hot.events.Count > 0, "hot partially burned mixture produces afterfires");
            Check(Sim(Seed, 0, 5e-5, 0).events.Count == 0, "zero fuel cannot produce afterfire");
            Check(Sim(Seed, 2e-6, 0, 0).events.Count == 0, "zero oxygen cannot produce afterfire");
            Check(Sim(Seed, 2e-6, 5e-5, 1).events.Count == 0, "fully chamber-burned fuel cannot produce afterfire");
            Check(Sim(Seed, 2e-6, 5e-5, 0, 298, 298).events.Count == 0, "cold charge and exhaust without ignition cannot light residual mixture");
            Check(Sim(Seed, 3e-6, 5e-5, 0, 1000, 298).events.Count > 0, "charge/gas temperature can enable ignition with cold walls");
            Check(Sim(Seed, 1e-8, 5e-5, 0).events.Count == 0, "trace fuel below the flammability limit cannot explode in inert exhaust");
            string first = JsonUtility.ToJson(new EventLog { events = hot.events });
            Check(first == JsonUtility.ToJson(new EventLog { events = Sim(Seed, 3e-6, 5e-5, .1).events }), "same seed/input gives exact event times, sites and energies");
            Check(first != JsonUtility.ToJson(new EventLog { events = Sim(Seed + 1, 3e-6, 5e-5, .1).events }), "changed seed varies conditional ignition, not fuel availability");
            foreach (var m in new[] { hot, Sim(Seed, 0, 5e-5, 0), Sim(Seed, 2e-6, 0, 0), Sim(Seed, 2e-6, 5e-5, 1) }) Conservation(m, "control");
            var residual = new BackfireMixturePreview(Seed, "residual", initialK: 298);
            for (int i = 0; i < 12; i++) residual.Step(new BackfireMixturePreview.Input { dt = 1.0 / 120, fuelKg = 2e-6, airKg = 0,
                chamberBurnFraction = 0, intakeK = 298, wallTargetK = 298 });
            double priorFuel = residual.FuelIn;
            for (int i = 0; i < 100; i++) residual.Step(new BackfireMixturePreview.Input { dt = 1.0 / 120, fuelKg = 0, airKg = 1e-5,
                chamberBurnFraction = 0, intakeK = 1100, wallTargetK = 1100 });
            Check(residual.FuelIn == priorFuel && residual.events.Count == 0, "cold walls can quench a retained mixture despite hot incoming air");
            Conservation(residual, "cold residual");
            var hotResidual = new BackfireMixturePreview(Seed, "hot-residual", initialK: 1000);
            for (int i = 0; i < 12; i++) hotResidual.Step(new BackfireMixturePreview.Input { dt = 1.0 / 120, fuelKg = 5e-6, airKg = 0,
                chamberBurnFraction = 0, intakeK = 350, wallTargetK = 1000 });
            priorFuel = hotResidual.FuelIn;
            for (int i = 0; i < 100; i++) hotResidual.Step(new BackfireMixturePreview.Input { dt = 1.0 / 120, fuelKg = 0, airKg = 1e-5,
                chamberBurnFraction = 0, intakeK = 350, wallTargetK = 1000 });
            Check(hotResidual.FuelIn == priorFuel && hotResidual.events.Count > 0, "hot fuel-cut residual mixture may ignite later without fresh fuel");
            Conservation(hotResidual, "hot residual");
            ulong timbre = M.Seed(Seed, "07-i6-carb", 2, 11);
            var reference = M.Burst(timbre, "crackle", M.Cleanup.LessTicking);
            Check(reference.SequenceEqual(M.Burst(timbre, "crackle", M.Cleanup.LessTicking, M.Texture.Reference)), "default source preserves prior generator exactly");
            var damped = M.Burst(timbre, "crackle", M.Cleanup.LessTicking, M.Texture.Damped);
            var noShell = M.Burst(timbre, "crackle", M.Cleanup.LessTicking, M.Texture.NoShell);
            Check(!reference.SequenceEqual(damped) && !reference.SequenceEqual(noShell), "shell diagnostic and damped texture differ from prior source");
            Check(damped.SequenceEqual(M.Burst(timbre, "crackle", M.Cleanup.LessTicking, M.Texture.Damped)), "new shell texture remains seeded and exact");
            File.WriteAllText(Path.Combine(Folder, "core-verification.txt"), string.Join("\n", checks) + "\nChecks: " + checks.Count + "\n", new UTF8Encoding(false));
            Debug.Log("MIXTURE BACKFIRE VERIFICATION PASS " + checks.Count);
        }

        static float[] Bed(V8BackfireMatrixExport.Job job)
        { byte[] raw = File.ReadAllBytes(Path.Combine(Capture, job.bed)); var result = new float[job.frames * 2]; Buffer.BlockCopy(raw, 0, result, 0, raw.Length); return result; }
        static double PhysicalFuelFlow(CarSimulation car)
        {
            // These isolated auditions disable depletion. Query the unchanged shared fuel law
            // with realistic accounting, then restore before simulation advances. Never touch live/career objects.
            var accounting = car.driver.fuelUsage;
            try { car.driver.fuelUsage = FuelUsageMode.Realistic; return FuelDynamics.Flow(car); }
            finally { car.driver.fuelUsage = accounting; }
        }
        static void Sample(BinaryWriter writer, double value)
        { if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) >= 1) throw new InvalidOperationException("Invalid WAV value: " + value); writer.Write((short)Math.Round(value * 32767)); }
        static void Wav(string path, double[] left, double[] right, double gain = 1)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + left.Length * 4); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(Rate); w.Write(Rate * 4); w.Write((short)4); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(left.Length * 4);
                for (int i = 0; i < left.Length; i++) { Sample(w, left[i] * gain); Sample(w, right[i] * gain); }
            }
        }
        static double[] Slice(double[] data, double start, double end)
        { return data.Skip((int)Math.Round(start * Rate)).Take((int)Math.Round((end - start) * Rate)).ToArray(); }
        public static void DiagnoseTimber()
        {
            Directory.CreateDirectory(Folder);
            var jobs = JsonUtility.FromJson<V8BackfireMatrixExport.Manifest>(File.ReadAllText(Path.Combine(Capture, "jobs.json"), Encoding.UTF8));
            var job = jobs.jobs.Single(j => j.id == "07-i6-carb"); var bed = Bed(job);
            var old = M.Render(job, Seed, M.Cleanup.Light); var damped = M.Render(job, Seed, M.Cleanup.Light, M.Texture.Damped);
            var noShell = M.Render(job, Seed, M.Cleanup.Light, M.Texture.NoShell);
            var l = new double[job.frames]; var r = new double[job.frames];
            for (int i = 0; i < job.frames; i++) { l[i] = bed[i * 2]; r[i] = bed[i * 2 + 1]; }
            double max = new[] { l, r, old.left, old.right, damped.left, damped.right, noShell.left, noShell.right }.Max(x => x.Max(v => Math.Abs(v)));
            double diagnosticGain = Math.Min(1, .95 / max); // One shared scale; never normalize alternatives separately.
            double localA = 40 - (job.frames / (double)Rate + 1), localB = 88 - (job.frames * 2 / (double)Rate + 2);
            foreach (var target in new[] { Tuple.Create("at-40s", localA), Tuple.Create("at-88s", localB) })
            {
                double from = Math.Max(0, target.Item2 - 1.2), to = Math.Min(job.frames / (double)Rate, target.Item2 + 1.8);
                Wav(Path.Combine(Folder, "timber-" + target.Item1 + "-engine-only.wav"), Slice(l, from, to), Slice(r, from, to), diagnosticGain);
                foreach (var pair in new[] { Tuple.Create("previous-backfire-only", old), Tuple.Create("damped-backfire-only", damped), Tuple.Create("no-shell-backfire-only", noShell) })
                    Wav(Path.Combine(Folder, "timber-" + target.Item1 + "-" + pair.Item1 + ".wav"), Slice(pair.Item2.left, from, to), Slice(pair.Item2.right, from, to), diagnosticGain);
            }
            File.WriteAllText(Path.Combine(Folder, "timber-diagnostic.txt"), "Actual section duration " + job.frames / (double)Rate + " s; 40 s maps to local " + localA +
                " s in A; 88 s maps to local " + localB + " s in B. These are different driving moments. One common diagnostic gain " + diagnosticGain + " for all clips; no separate normalization or EQ.\n" +
                "Seven explicit damped sine shell modes remain in the prior source even after light cleanup/less ticking. NoShell ablates only these modes; Damped shortens/broadens/modulates their response.\n" +
                "Presence of these modes is verified in code; user listening is still needed to identify the perceived ding against the separate engine bed.\n", new UTF8Encoding(false));
            Debug.Log("TIMBER DING DIAGNOSTIC READY");
        }

        internal static EngineSoundFrame WithoutGulp(EngineSoundFrame f)
        {
            return new EngineSoundFrame(f.rpm, f.throttle, f.load, f.boost, f.volume, f.slip, f.speed, f.active, f.cut,
                f.releaseId, f.releaseStrength, f.turboSpool, f.superchargerBoost, f.chargerLoad, f.turboBoost,
                f.combustionFraction, f.idleRoughness, 0, f.fuelHesitation, f.popId, f.popStrength, f.overrun,
                f.twoStep, f.cutMethod, f.cylinderContribution, f.airflowKgS);
        }
        public static void DiagnoseTimberEngine()
        {
            Directory.CreateDirectory(Folder);
            var jobs = JsonUtility.FromJson<V8BackfireMatrixExport.Manifest>(File.ReadAllText(Path.Combine(Capture, "jobs.json"), Encoding.UTF8));
            var job = jobs.jobs.Single(j => j.id == "07-i6-carb");
            var fixture = JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(Path.Combine(Capture, job.id + ".race.json"), Encoding.UTF8));
            var race = fixture.CreateRace();
            string[] names = { "engine-reference", "engine-no-gulp", "gulp-only", "engine-no-clatter", "clatter-only" };
            var signals = names.Select(_ => new M.Stem { left = new double[job.frames], right = new double[job.frames] }).ToArray();
            try
            {
                var car = race.player; var controller = new EngineSoundController(car.spec, car.tune);
                var synths = new EngineSoundSynth[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    var spec = car.spec.Copy(); spec.sound.exhaustPopGain = 0;
                    if (i == 2 || i == 4) spec.sound.engineGain = 0;
                    if (i == 2 || i == 3) spec.engineBehavior.mechanicalClatter = 0;
                    synths[i] = new EngineSoundSynth(new EngineSoundProfile(spec, car.tune), sampleRate: Rate);
                }
                var block = new float[800];
                for (int tick = 0; tick < fixture.ticks.Count; tick++)
                {
                    var command = fixture.ticks[tick]; car.state.warmupLineLock = command.lineLock;
                    if (command.beginCountdown && !race.BeginCountdown()) throw new InvalidOperationException("Timber countdown");
                    if (race.phase == RacePhase.Finished) race.Shutdown(command.input); else race.Step(command.input);
                    var frame = controller.Update(car.state, true, .85, DragMath.FixedStep); var noGulp = WithoutGulp(frame);
                    for (int s = 0; s < synths.Length; s++)
                    {
                        synths[s].Fill(block, 2, Rate, s == 1 || s == 4 ? noGulp : frame);
                        for (int i = 0; i < 400; i++) { signals[s].left[tick * 400 + i] = block[i * 2]; signals[s].right[tick * 400 + i] = block[i * 2 + 1]; }
                    }
                }
                var bed = Bed(job);
                Check(Enumerable.Range(0, job.frames).All(i => signals[0].left[i] == bed[i * 2] && signals[0].right[i] == bed[i * 2 + 1]),
                    "Timber engine diagnostic reference exactly matches the unchanged archived pop-free bed");
                var notes = new StringBuilder("User identifies ding in engine-only layer. These are private copied-spec/frame ablations, not runtime sound changes.\n");
                foreach (var target in new[] { Tuple.Create("at-40s", 3.1), Tuple.Create("at-88s", 14.2) })
                {
                    int from = (int)((target.Item2 - 1.2) * Rate), length = Rate * 3;
                    for (int s = 0; s < signals.Length; s++)
                    {
                        var l = signals[s].left.Skip(from).Take(length).ToArray(); var r = signals[s].right.Skip(from).Take(length).ToArray();
                        Wav(Path.Combine(Folder, "timber-" + target.Item1 + "-" + names[s] + ".wav"), l, r);
                        notes.AppendLine(target.Item1 + " / " + names[s] + " / RMS " + Math.Sqrt(l.Concat(r).Average(v => v * v)).ToString("R"));
                    }
                }
                notes.AppendLine("Gulp-only mutes engine gain and mechanical clatter on private spec; the surviving 90 + throttle*75 Hz intake sine (plus second harmonic) is outside the exhaust/backfire generator. No-gulp removes only its frame excitation. Clatter-only removes that excitation and engine gain while retaining authored clatter.");
                File.WriteAllText(Path.Combine(Folder, "engine-layer-diagnostic.txt"), notes.ToString(), new UTF8Encoding(false));
                Debug.Log("TIMBER ENGINE LAYER DIAGNOSTIC READY");
            }
            finally { race.CloseOpponentWorker(); }
        }

        public static void Run()
        {
            Verify(); DiagnoseTimber(); DiagnoseTimberEngine();
            var jobs = JsonUtility.FromJson<V8BackfireMatrixExport.Manifest>(File.ReadAllText(Path.Combine(Capture, "jobs.json"), Encoding.UTF8));
            var manifest = new Manifest(); string rng = JsonUtility.ToJson(UnityEngine.Random.state);
            foreach (var job in jobs.jobs)
            {
                var fixture = JsonUtility.FromJson<RaceAudioTestFixture>(File.ReadAllText(Path.Combine(Capture, job.id + ".race.json"), Encoding.UTF8));
                var race = fixture.CreateRace(); bool diesel = race.player.engine.data.fueling == FuelSystem.Diesel;
                var model = new BackfireMixturePreview(Seed, job.id, job.lengthM,
                    .027 * Math.Pow(job.litres / 2, .18), initialK: 610, stoichOxygenPerFuel: diesel ? 3.36 : 3.43, heatingJPerKg: diesel ? 43000000 : 44000000);
                var stem = new M.Stem { left = new double[job.frames], right = new double[job.frames] };
                var row = new Row { id = job.id, file = job.id + "-original-mixture.wav" };
                try
                {
                    var car = race.player; int consumedEvents = 0, inventedCutFuelTicks = 0;
                    for (int tick = 0; tick < fixture.ticks.Count; tick++)
                    {
                        var command = fixture.ticks[tick]; car.state.warmupLineLock = command.lineLock;
                        if (command.beginCountdown) Check(race.BeginCountdown(), job.id + " countdown");
                        if (race.phase == RacePhase.Finished) race.Shutdown(command.input); else race.Step(command.input);
                        var s = car.state; double dt = DragMath.FixedStep;
                        // Reported gameplay fuel usage can be scaled by assists. Remove that accounting multiplier.
                        double rate = PhysicalFuelFlow(car);
                        if (s.activeCutMethod == CutMethod.Fuel && s.combustionFraction <= 0 && rate > 1e-12) inventedCutFuelTicks++;
                        // Fuel-cut duty already reduced fuel delivery. Do not also call that delivered fuel unburned.
                        double burn = s.activeCutMethod == CutMethod.Fuel ? s.fuelingTorqueFactor * (1 - .35 * s.engineDamage) : s.combustionFraction;
                        burn *= s.cylinderCombustionFactor;
                        if (s.transmissionTorqueCut || s.stalled || s.engineFailed) rate = 0;
                        if (s.antiLagActive) burn *= 1 - .35 * s.exhaustEnergy;
                        // Closed-throttle pumping is absent from the existing published airflow. Explicit 12% vacuum proxy.
                        double air = Math.Max(s.airflowKgS, car.induction.BaseAirflow(s.rpm) * .12);
                        if (s.rpm <= 0) air = 0;
                        model.Step(new BackfireMixturePreview.Input { dt = dt, fuelKg = rate * car.spec.fuel.densityKgPerLitre * dt,
                            airKg = air * dt, chamberBurnFraction = Math.Max(0, Math.Min(1, burn)),
                            intakeK = Math.Max(250, s.chargeTemperatureK > 0 ? s.chargeTemperatureK : car.induction.data.ambientTemperatureK),
                            wallTargetK = 330 + 280 * s.engineTemperature + 320 * s.exhaustHeat, ignitionSource = s.antiLagActive && s.exhaustEnergy > 0 });
                        while (consumedEvents < model.events.Count)
                        {
                            var chemical = model.events[consumedEvents++]; row.maxTemperatureK = Math.Max(row.maxTemperatureK, chemical.gasK);
                            if (chemical.acousticJ < .002) continue; // Explicit audibility/calculation cutoff; mass/energy remain recorded.
                            row.audibleEvents++;
                            var ev = new V8BackfireMatrixExport.Event { index = chemical.index, time = chemical.time,
                                strength = .28 * Math.Sqrt(chemical.acousticJ), rpm = s.rpm, temperatureK = chemical.gasK,
                                throttle = s.throttle, load = 0, turboBoost = s.turboBoostBar, limiter = s.limiter, twoStep = s.launchControlActive };
                            string kind; var source = M.Source(job, ev, Seed, M.Cleanup.LessTicking, out kind);
                            double position = chemical.position;
                            if (car.induction.turboCount > 0 && position * job.lengthM < .45) position = -.08;
                            double travel; var emitted = M.Propagate(source, job, ev, Seed, position, out travel);
                            int start = (int)Math.Round(ev.time * Rate);
                            for (int i = 0; i < emitted.Length && start + i < job.frames; i++)
                            {
                                double value = emitted[i] * ev.strength * job.popGain * .45;
                                stem.left[start + i] += value; stem.right[start + i] += value;
                            }
                        }
                    }
                    Conservation(model, job.id);
                    Check(inventedCutFuelTicks == 0, job.id + " full fuel cut injects no fresh afterfire fuel");
                    Check(model.FuelIn > 0, job.id + " physical fuel delivery remains present when gameplay depletion is disabled");
                    Check(Math.Abs(car.state.elapsed - fixture.elapsed) < 1e-9 && car.state.exhaustPopCount == fixture.pops,
                        job.id + " authoritative race and original event counters unchanged / ET " + car.state.elapsed.ToString("R") + " vs " + fixture.elapsed.ToString("R") + " / pops " + car.state.exhaustPopCount + " vs " + fixture.pops);
                    row.chemicalEvents = model.events.Count; row.fuelInKg = model.FuelIn; row.chamberBurnedKg = model.ChamberBurned;
                    row.unburnedKg = model.UnburnedIn; row.exhaustBurnedKg = model.FuelBurned; row.exhaustOutKg = model.FuelOut;
                    row.retainedKg = model.RetainedFuel; row.acousticJ = model.AcousticEnergyJ;
                    var bed = Bed(job); byte[] original = File.ReadAllBytes(Path.Combine(Capture, job.reference));
                    int total = job.frames * 2 + Rate;
                    var l = new double[total]; var r = new double[total];
                    for (int i = 0; i < job.frames; i++)
                    {
                        l[i] = BitConverter.ToInt16(original, 44 + i * 4) / 32767.0; r[i] = BitConverter.ToInt16(original, 46 + i * 4) / 32767.0;
                        for (int c = 0; c < 2; c++)
                        {
                            double engine = bed[i * 2 + c], pop = c == 0 ? stem.left[i] : stem.right[i], mixed = engine;
                            if (pop != 0) { double sum = engine / (1 - Math.Abs(engine)) + pop; mixed = sum / (1 + Math.Abs(sum)); }
                            double fade = i < 960 ? i / 959.0 : i >= job.frames - 3840 ? (job.frames - 1 - i) / 3839.0 : 1;
                            mixed *= fade; row.peak = Math.Max(row.peak, Math.Abs(mixed));
                            if (c == 0) l[job.frames + Rate + i] = mixed; else r[job.frames + Rate + i] = mixed;
                        }
                    }
                    Wav(Path.Combine(Folder, row.file), l, r);
                    File.WriteAllText(Path.Combine(Folder, job.id + "-events.json"), JsonUtility.ToJson(new EventLog { events = model.events }, true), new UTF8Encoding(false));
                    manifest.outputs.Add(row); File.WriteAllText(Path.Combine(Folder, "manifest.json"), JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
                    Debug.Log("MIXTURE CASE " + job.id + " chemical=" + row.chemicalEvents + " audible=" + row.audibleEvents);
                }
                finally { race.CloseOpponentWorker(); }
            }
            Check(manifest.outputs.Single(x => x.id == "26-launch-hard-ignition").audibleEvents > 0, "ignition two-step/anti-lag configuration has audible mixture-driven afterfire coverage");
            Check(rng == JsonUtility.ToJson(UnityEngine.Random.state), "mixture/audio preview does not consume Unity gameplay RNG");
            File.WriteAllText(Path.Combine(Folder, "verification.txt"), string.Join("\n", checks) + "\nChecks: " + checks.Count + "\n", new UTF8Encoding(false));
            Debug.Log("MIXTURE BACKFIRE MATRIX PASS");
        }
    }
}
