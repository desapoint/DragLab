using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;

namespace DragLab.Editor
{
    public static class EngineMusicVerification
    {
        public static int Run(string root)
        {
            int count = 0; var report = new StringBuilder("Engine-only classical concert / four-car score ranges / deterministic auditions.\n");
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException("CONCERT CHECK FAILED: " + label); count++; report.AppendLine("PASS: " + label); };
            var cars = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            foreach (var car in cars)
                for (int song = 0; song < 3; song++)
                {
                    var config = new EngineBenchConfig { vehicle = car.spec.Copy(), tune = car.CreateStockTune() };
                    string before = JsonUtility.ToJson(config);
                    var music = new EngineBenchMusic(song, config.vehicle); var fixture = EngineBenchMusic.PerformanceConfig(config);
                    var bench = new EngineTestBench(fixture.vehicle, fixture.tune); bool valid = true;
                    for (int tick = 0; tick < music.DurationTicks; tick++)
                    {
                        var control = music.At(tick, bench.spec); bench.Step(control);
                        valid &= bench.state.rpm >= bench.spec.idleRpm && bench.state.rpm <= bench.spec.redlineRpm * .91 &&
                            !bench.state.engineFailed && !bench.state.stalled && bench.state.gear == 0 &&
                            Math.Abs(bench.state.rpm - control.targetRpm) < .01 && music.GainAt(tick) >= 0 && music.GainAt(tick) <= 1;
                    }
                    check(valid && before == JsonUtility.ToJson(config), "Entire score stays in the engine's playable range without modifying the source: " + car.spec.id + " / " + music.Title);
                }
            string directory = Path.Combine(root, "reports/dyno-concert"); Directory.CreateDirectory(directory);
            var hatch = Array.Find(cars, c => c.spec.id == "starter-hatch");
            var source = new EngineBenchConfig { vehicle = hatch.spec.Copy(), tune = hatch.CreateStockTune() };
            string[] names = { "ode-to-joy", "fur-elise", "mountain-king" };
            for (int song = 0; song < 3; song++)
            {
                var music = new EngineBenchMusic(song, source.vehicle); var pcm = music.Render(source); var again = music.Render(source);
                bool bounded = true, equal = pcm.Length == again.Length; double energy = 0;
                for (int i = 0; i < pcm.Length; i++)
                { bounded &= !float.IsNaN(pcm[i]) && !float.IsInfinity(pcm[i]) && Math.Abs(pcm[i]) < 1; equal &= pcm[i] == again[i]; energy += pcm[i] * pcm[i]; }
                check(bounded && equal && energy > 1, "Complete engine-DSP audition is audible, bounded and sample-exact on repeat: " + music.Title);
                EngineSoundAudition.WriteWav(Path.Combine(directory, names[song] + ".wav"), pcm);
                report.AppendLine(music.Title + " / " + (music.DurationTicks / 120.0).ToString("F2") + " s / RMS " + Math.Sqrt(energy / pcm.Length).ToString("F4"));
            }
            File.WriteAllText(Path.Combine(directory, "verification.txt"), report.AppendLine("PASS: " + count + " concert checks.").ToString());
            return count;
        }
    }
}
