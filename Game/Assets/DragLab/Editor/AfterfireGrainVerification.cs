using System;
using System.IO;
using System.Text;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    public static class AfterfireGrainVerification
    {
        public static string OutputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/afterfire-grain-20261021"));
        static string Root => OutputDirectory;
        static readonly StringBuilder Log = new StringBuilder();
        static int checks;
        static void Check(bool yes, string label)
        {
            Log.AppendLine((yes ? "PASS " : "FAIL ") + label); checks++;
            File.WriteAllText(Path.Combine(Root, "grain-verification.txt"), Log + "Checks: " + checks + "\n", new UTF8Encoding(false));
            if (!yes) throw new InvalidOperationException(label);
        }
        sealed class Bands
        {
            readonly double highAlpha, midAlpha, lowAlpha;
            double highLow, midLow, lowLow;
            public double High, Mid;
            public Bands(int rate)
            {
                highAlpha = 1 - Math.Exp(-2 * Math.PI * 2200 / rate);
                midAlpha = 1 - Math.Exp(-2 * Math.PI * 650 / rate);
                lowAlpha = 1 - Math.Exp(-2 * Math.PI * 80 / rate);
            }
            public void Add(double x)
            {
                highLow += highAlpha * (x - highLow); midLow += midAlpha * (x - midLow); lowLow += lowAlpha * (x - lowLow);
                High += (x - highLow) * (x - highLow); Mid += (midLow - lowLow) * (midLow - lowLow);
            }
        }
        static ExhaustMixtureModel.Event Reaction(double time, double energy, double pressure, double rise, double burn)
        {
            return new ExhaustMixtureModel.Event { time = time, acousticJ = energy, pressurePa = pressure, riseSeconds = rise, burnSeconds = burn, flowMps = 40, seed = 12345 };
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root); Log.Clear(); checks = 0;
            foreach (int rate in new[] { 24000, 44100, 48000 })
            {
                foreach (bool longer in new[] { false, true })
                {
                    string label = rate + (longer ? " / longer burn" : " / short burn");
                    var old = new ReactivePressureBurst(rate, smoothTexture: false);
                    var current = new ReactivePressureBurst(rate); var repeat = new ReactivePressureBurst(rate);
                    var first = Reaction(0, .2, 60000, longer ? .003 : .0007, longer ? .05 : .012);
                    old.Begin(first); current.Begin(first); repeat.Begin(first);
                    var before = new Bands(rate); var after = new Bands(rate);
                    bool pressureExact = true, reproducible = true, finite = true;
                    for (int i = 0; i < rate; i++)
                    {
                        if (i == (int)(rate * .055) || i == (int)(rate * .14))
                        {
                            bool renewed = i == (int)(rate * .14);
                            var e = Reaction(i / (double)rate, renewed ? .3 : .035, renewed ? 90000 : 15000, first.riseSeconds, first.burnSeconds);
                            old.Excite(e); current.Excite(e); repeat.Excite(e);
                        }
                        old.Sample(); double x = current.Sample();
                        pressureExact &= old.PressureSample == current.PressureSample;
                        reproducible &= x == repeat.Sample(); finite &= !double.IsNaN(x) && !double.IsInfinity(x);
                        before.Add(old.CombustionBodySample + old.TurbulenceSample);
                        after.Add(current.CombustionBodySample + current.TurbulenceSample);
                    }
                    Check(pressureExact && current.FundedEnergy == old.FundedEnergy, "pressure waveform and reaction funding remain bit-identical / " + label);
                    Check(after.High < before.High * .75, "grain/high-frequency texture falls without changing compression / " + label);
                    Check(after.Mid > before.Mid * .6 && after.Mid < before.Mid * 1.1, "audible low/mid texture remains within 2.3 dB of reference / " + label);
                    Check(reproducible && finite, "smoothed texture is finite and seeded reproducibly / " + label);
                    Log.AppendLine("MEASURE " + label + " / grain energy ratio " + (after.High / before.High).ToString("R") + " / low-mid ratio " + (after.Mid / before.Mid).ToString("R"));
                    current.Clear(); Check(current.Sample() == 0, "clearing the source also clears the smoothing tail / " + label);
                }
                var core = new ReactivePressureBurst(rate); var feed = Reaction(0, .002, 60000, .0007, .012); core.Begin(feed);
                for (int i = 0; i < rate; i++) core.Sample();
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < rate; i++) { if (i % 200 == 0) { feed.time = 1 + i / (double)rate; core.Excite(feed); } core.Sample(); }
                Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "zero warmed texture event/sample allocations / " + rate);
            }
        }
        public static void VerifyAndBuild()
        {
            Run();
            AfterfireBangVerification.OutputDirectory = Root; AfterfireBangVerification.Run();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/trackside-audio-20261021/outdoor-verification.txt"));
            byte[] historical = File.ReadAllBytes(path);
            try { CrowdAudioVerification.Run(); File.Copy(path, Path.Combine(Root, "outdoor-verification.txt"), true); }
            finally { File.WriteAllBytes(path, historical); }
            RuntimeAudioIntegrationVerification.OutputDirectory = Root; RuntimeAudioIntegrationVerification.TracksideListening = true;
            RuntimeAudioIntegrationVerification.Run(); RuntimeAudioIntegrationVerification.CheckRaces();
            PlayerBuild.BuildPlayerOnly();
        }
    }
}
