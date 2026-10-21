using System;
using DragLab.Domain;

namespace DragLab.Editor
{
    // Listening prototype only. No runtime hook. Prepared buffers/filters; no sample allocations.
    public sealed class NaturalIntakePreview
    {
        public enum Voice { Airflow, ValveBody, IntakeRush }
        readonly EngineSoundProfile profile;
        readonly Voice voice;
        readonly int rate, mask;
        readonly double[] kernel, pending;
        readonly double smooth, lowAlpha, highAlpha, b0, a1, a2;
        double rpm, level, flow, throttle, phase, low1, low2, high, cavity1, cavity2, dcIn, dcOut;
        double targetRpm, targetLevel, targetFlow, targetThrottle;
        int cursor;
        uint random;
        EngineSoundFrame previous;
        double previousGulp, bodyBurst;
        readonly double burstDecay;
        public NaturalIntakePreview(EngineSoundProfile profile, int sampleRate = 48000, uint seed = 171818, Voice voice = Voice.Airflow)
        {
            if (profile == null || sampleRate < 8000 || sampleRate > 192000) throw new ArgumentException("Invalid intake preparation.");
            this.profile = profile; this.voice = voice; rate = sampleRate; random = seed == 0 ? 171818u : seed; rpm = profile.idleRpm;
            bool body = voice == Voice.ValveBody, rush = voice == Voice.IntakeRush;
            burstDecay = Math.Exp(-1.0 / (rate * .075));
            smooth = 1 - Math.Exp(-1.0 / (rate * .018));
            lowAlpha = 1 - Math.Exp(-2 * Math.PI * Math.Min(body ? 850 : 2200, rate * .3) / rate);
            highAlpha = 1 - Math.Exp(-2 * Math.PI * (rush ? 260 : body ? 40 : 65) / rate);
            // Broad plenum response is an authored intake surrogate, independent of exhaust runner geometry.
            double size = Math.Pow(profile.litres / Math.Max(1, profile.cylinders) / .5, 1.0 / 3);
            double center = Math.Min(rate * .2, Math.Max(rush ? 420 : body ? 110 : 140, Math.Min(rush ? 1100 : body ? 300 : 520, (rush ? 720 : body ? 195 : 330) / size)));
            double angle = 2 * Math.PI * center / rate, alpha = Math.Sin(angle) / (2 * .62), normal = 1 + alpha;
            b0 = alpha / normal; a1 = -2 * Math.Cos(angle) / normal; a2 = (1 - alpha) / normal;
            kernel = new double[(int)Math.Ceiling(rate * (body ? .016 : .009))];
            for (int i = 0; i < kernel.Length; i++)
            {
                double t = i / (double)rate;
                kernel[i] = body ? (1 - Math.Exp(-t / .0008)) * (Math.Exp(-t / .0024) - .45 * Math.Exp(-t / .006)) :
                    (1 - Math.Exp(-t / .00035)) * (Math.Exp(-t / .0015) - .35 * Math.Exp(-t / .0035));
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
                if (voice != Voice.Airflow)
                {
                    double gulp = frame.active ? Clamp(frame.intakeGulp, 0, 1) : 0;
                    bodyBurst = frame.active ? Math.Max(bodyBurst, Math.Max(0, gulp - previousGulp) * Clamp(frame.volume, 0, 1)) : 0;
                    previousGulp = gulp;
                }
                targetFlow = frame.airflowKgS >= 0 ? Clamp(frame.airflowKgS / Math.Max(.02, profile.litres * .04), 0, 1.5) : targetThrottle;
            }
            if (voice != Voice.Airflow) { bodyBurst *= burstDecay; targetLevel = bodyBurst; }
            rpm += smooth * (targetRpm - rpm); level += smooth * (targetLevel - level);
            flow += smooth * (targetFlow - flow); throttle += smooth * (targetThrottle - throttle);
            phase += rpm * profile.cylinders / (120 * rate);
            while (phase >= 1)
            {
                phase -= 1;
                if (level > 1e-8)
                {
                    // The rush uses zero-mean turbulence. Positive-only kernels in the
                    // body candidate retain a coherent pulse train and can sound like buzzing.
                    double gain = .8 + .4 * Noise();
                    for (int i = 0; i < kernel.Length; i++) pending[(cursor + i) & mask] += kernel[i] * gain * (voice == Voice.IntakeRush ? Noise() : .55 + .45 * Noise());
                }
            }
            double pulse = pending[cursor]; pending[cursor] = 0; cursor = (cursor + 1) & mask;
            double noise = Noise(); low1 += lowAlpha * (noise - low1); low2 += lowAlpha * (low1 - low2);
            high += highAlpha * (low2 - high); double air = low2 - high;
            if (voice == Voice.IntakeRush)
            {
                // No additive periodic pressure carrier: valve cadence is only a faint
                // stochastic texture beneath an opening-driven, band-limited intake rush.
                double input = air * (.90 + .10 * Clamp(pulse, -1, 1));
                double response = b0 * input + cavity1;
                cavity1 = -a1 * response + cavity2; cavity2 = -b0 * input - a2 * response;
                double rushValue = (input + .35 * response) * level * .10 * (.45 + .55 * Math.Sqrt(flow)) * (.55 + .45 * throttle);
                double result = rushValue - dcIn + .995 * dcOut; dcIn = rushValue; dcOut = result;
                return result;
            }
            double excitation = voice == Voice.ValveBody ? pulse * (.65 + .35 * air) : air * (1 + .8 * pulse) + .12 * pulse;
            double cavity = b0 * excitation + cavity1;
            cavity1 = -a1 * cavity + cavity2; cavity2 = -b0 * excitation - a2 * cavity;
            double texture = voice == Voice.ValveBody ? pulse * .55 + cavity * .8 + air * Math.Min(1, Math.Abs(pulse)) * .12 : air * 1.6 + cavity * 1.1 + pulse * .12;
            double value = texture * level * .10 * (.45 + .55 * Math.Sqrt(flow)) * (.55 + .45 * throttle);
            // Same final DC-removal response as production; the existing engine bed is untouched.
            double output = value - dcIn + .995 * dcOut; dcIn = value; dcOut = output;
            return output;
        }
    }
}
