using System;
using DragLab.Domain;

namespace DragLab.Editor
{
    // Listening prototype only. No runtime hook. Prepared buffers/filters; no sample allocations.
    public sealed class NaturalIntakePreview
    {
        readonly EngineSoundProfile profile;
        readonly int rate, mask;
        readonly double[] kernel, pending;
        readonly double smooth, lowAlpha, highAlpha, b0, a1, a2;
        double rpm, level, flow, throttle, phase, low1, low2, high, cavity1, cavity2, dcIn, dcOut;
        double targetRpm, targetLevel, targetFlow, targetThrottle;
        int cursor;
        uint random;
        EngineSoundFrame previous;
        public NaturalIntakePreview(EngineSoundProfile profile, int sampleRate = 48000, uint seed = 171818)
        {
            if (profile == null || sampleRate < 8000 || sampleRate > 192000) throw new ArgumentException("Invalid intake preparation.");
            this.profile = profile; rate = sampleRate; random = seed == 0 ? 171818u : seed; rpm = profile.idleRpm;
            smooth = 1 - Math.Exp(-1.0 / (rate * .018));
            lowAlpha = 1 - Math.Exp(-2 * Math.PI * Math.Min(2200, rate * .3) / rate);
            highAlpha = 1 - Math.Exp(-2 * Math.PI * 65 / rate);
            // Broad plenum response is an authored intake surrogate, independent of exhaust runner geometry.
            double size = Math.Pow(profile.litres / Math.Max(1, profile.cylinders) / .5, 1.0 / 3);
            double center = Math.Min(rate * .2, Math.Max(140, Math.Min(520, 330 / size)));
            double angle = 2 * Math.PI * center / rate, alpha = Math.Sin(angle) / (2 * .62), normal = 1 + alpha;
            b0 = alpha / normal; a1 = -2 * Math.Cos(angle) / normal; a2 = (1 - alpha) / normal;
            kernel = new double[(int)Math.Ceiling(rate * .009)];
            for (int i = 0; i < kernel.Length; i++)
            {
                double t = i / (double)rate;
                kernel[i] = (1 - Math.Exp(-t / .00035)) * (Math.Exp(-t / .0015) - .35 * Math.Exp(-t / .0035));
            }
            int capacity = 1; while (capacity < kernel.Length * 2) capacity *= 2;
            pending = new double[capacity]; mask = capacity - 1;
        }
        double Noise() { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return random / (double)uint.MaxValue * 2 - 1; }
        static double Clamp(double x, double lo, double hi) => Math.Max(lo, Math.Min(hi, x));
        public double Sample(EngineSoundFrame frame)
        {
            if (!ReferenceEquals(previous, frame))
            {
                previous = frame;
                targetRpm = Math.Max(0, frame.rpm); targetThrottle = Clamp(frame.throttle, 0, 1);
                targetLevel = frame.active ? Clamp(frame.intakeGulp, 0, 1) * Clamp(frame.volume, 0, 1) : 0;
                targetFlow = frame.airflowKgS >= 0 ? Clamp(frame.airflowKgS / Math.Max(.02, profile.litres * .04), 0, 1.5) : targetThrottle;
            }
            rpm += smooth * (targetRpm - rpm); level += smooth * (targetLevel - level);
            flow += smooth * (targetFlow - flow); throttle += smooth * (targetThrottle - throttle);
            phase += rpm * profile.cylinders / (120 * rate);
            while (phase >= 1)
            {
                phase -= 1;
                if (level > 1e-8)
                {
                    // Independent noisy valve breaths, not a continuously ringing oscillator.
                    double gain = .8 + .4 * Noise();
                    for (int i = 0; i < kernel.Length; i++) pending[(cursor + i) & mask] += kernel[i] * gain * (.55 + .45 * Noise());
                }
            }
            double pulse = pending[cursor]; pending[cursor] = 0; cursor = (cursor + 1) & mask;
            double noise = Noise(); low1 += lowAlpha * (noise - low1); low2 += lowAlpha * (low1 - low2);
            high += highAlpha * (low2 - high); double air = low2 - high;
            double excitation = air * (1 + .8 * pulse) + .12 * pulse;
            double cavity = b0 * excitation + cavity1;
            cavity1 = -a1 * cavity + cavity2; cavity2 = -b0 * excitation - a2 * cavity;
            double value = (air * 1.6 + cavity * 1.1 + pulse * .12) * level * .10 * (.45 + .55 * Math.Sqrt(flow)) * (.55 + .45 * throttle);
            // Same final DC-removal response as production; the existing engine bed is untouched.
            double output = value - dcIn + .995 * dcOut; dcIn = value; dcOut = output;
            return output;
        }
    }
}
