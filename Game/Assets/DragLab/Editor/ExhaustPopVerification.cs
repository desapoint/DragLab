using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;

namespace DragLab.Editor
{
    public static class ExhaustPopVerification
    {
        const int Rate = 44100;
        static double Energy(float[] data) { double value = 0; foreach (var sample in data) value += sample * sample; return value; }
        static float[] Burst(VehicleSpec spec, int rate = Rate)
        {
            var pop = new ExhaustPopSynth(new EngineSoundProfile(spec, new Tune())); pop.Configure(rate); pop.Trigger(.8, true);
            var data = new float[rate / 4]; for (int i = 0; i < data.Length; i++) data[i] = (float)pop.Sample(); return data;
        }
        static double NarrowFraction(float[] data, double first, double second)
        {
            double power = 0;
            foreach (double hz in new[] { first, second })
            {
                double a = 0, b = 0;
                for (int i = 0; i < data.Length; i++) { double phase = 2 * Math.PI * hz * i / Rate; a += data[i] * Math.Sin(phase); b += data[i] * Math.Cos(phase); }
                power += a * a + b * b;
            }
            return 2 * power / Math.Max(.000000001, data.Length * Energy(data));
        }
        public static int Run(string root)
        {
            int count = 0; var report = new StringBuilder("Exhaust pop pressure bursts / legacy pitched-pair regression.\n");
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException("EXHAUST POP CHECK FAILED: " + label); count++; report.AppendLine("PASS: " + label); };
            string directory = Path.Combine(root, "reports/audio-pop-comparison"); Directory.CreateDirectory(directory);
            foreach (int cylinders in new[] { 4, 8 })
            {
                var spec = new VehicleSpec { sound = new EngineSoundSpec { cylinders = cylinders, displacementLitres = cylinders == 8 ? 5 : 2 } };
                var burst = Burst(spec); var repeat = Burst(spec); bool identical = true, finite = true;
                for (int i = 0; i < burst.Length; i++) { identical &= burst[i] == repeat[i]; finite &= !float.IsNaN(burst[i]) && !float.IsInfinity(burst[i]) && Math.Abs(burst[i]) < 1; }
                check(identical && finite && Energy(burst) > .1, "Burst is audible, bounded and reproducible: " + cylinders + " cylinders.");
                var reference = new float[burst.Length]; double frequency = cylinders == 8 ? 340 : 520;
                for (int i = 0; i < reference.Length; i++)
                {
                    double t = i / (double)Rate;
                    reference[i] = (float)((Math.Sin(t * 2 * Math.PI * frequency) + .4 * Math.Sin(t * 2 * Math.PI * frequency * 2.62)) * Math.Min(1, t / .0015) * Math.Exp(-t * 65) * .8);
                }
                double oldFraction = NarrowFraction(reference, frequency, frequency * 2.62), newFraction = NarrowFraction(burst, frequency, frequency * 2.62);
                check(newFraction < oldFraction * .3, "Former bell partials lose at least 70% of their normalized concentration: " + cylinders + " cylinders.");
                report.AppendLine(cylinders + " cyl old-pair fraction " + oldFraction.ToString("F5") + " / pressure-burst fraction " + newFraction.ToString("F5"));
                double tail = 0; for (int i = Rate / 8; i < burst.Length; i++) tail += burst[i] * burst[i];
                check(tail == 0, "Pop ends without a sustained metallic tail: " + cylinders + " cylinders.");
                spec.sound.exhaustPopGain = 0; check(Energy(Burst(spec)) == 0, "Pop gain isolates the layer without changing mechanics: " + cylinders + " cylinders.");
            }
            var plain = new VehicleSpec(); var ordinary = Burst(plain);
            plain.sound.exhaustPopSharpness = 2; check(Energy(Burst(plain)) != Energy(ordinary), "Sharpness changes the pressure shape/filter.");
            plain.sound.exhaustPopDecay = 2; check(Energy(Burst(plain)) > Energy(ordinary), "Decay controls brief turbulent energy, not a fixed ringing oscillator.");
            check(Energy(Burst(plain, 48000)) > .1, "Burst works at the live player's 48 kHz rate.");
            plain.sound.exhaustPopSharpness = float.NaN; check(plain.Validate() != null, "Invalid pop controls are rejected.");
            foreach (var car in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
            {
                var config = new EngineBenchConfig { vehicle = car.spec.Copy(), tune = car.CreateStockTune() };
                config.tune.controlLevel = 1; config.tune.twoStep = EquipmentSetting.On;
                config.tune.twoStepStyle = LimiterStyle.Hard; config.tune.twoStepCut = CutMethod.Ignition; config.tune.launchRpm = 3500;
                var pcm = EngineBenchRenderer.Render(config, EngineBenchScenario.TwoStep);
                EngineSoundAudition.WriteWav(Path.Combine(directory, "after-two-step-" + car.spec.id + ".wav"), pcm);
                File.WriteAllText(Path.Combine(directory, "after-two-step-" + car.spec.id + ".json"), JsonUtility.ToJson(config, true));
                check(Energy(pcm) > 1, "Full 3500 RPM ignition two-step renders with its actual exhaust: " + car.spec.id);
            }
            File.WriteAllText(Path.Combine(root, "reports/exhaust-pop-verification.txt"), report.AppendLine("PASS: " + count + " exhaust-pop checks.").ToString()); return count;
        }
    }
}
