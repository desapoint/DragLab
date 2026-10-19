using System;

namespace DragLab.Domain
{
    // A short pressure transient with filtered turbulent energy. No repeating
    // pitched oscillators: those made ignition cuts sound like a struck bell.
    public sealed class ExhaustPopSynth
    {
        readonly double litres, gain, sharpness, decayMultiplier;
        double age = 1, strength, width, decay, fast, slow, air, airSecond, airBass;
        int rate;
        double retriggerTail,lastOutput,tailDecay;
        uint random = 0x91732a5u;
        public ExhaustPopSynth(EngineSoundProfile profile)
        { litres = profile.litres; gain = profile.popGain; sharpness = profile.popSharpness; decayMultiplier = profile.popDecay; }
        double Noise() { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return random / (double)uint.MaxValue * 2 - 1; }
        public void Configure(int sampleRate)
        {
            if (rate == sampleRate) return;
            rate = sampleRate;
            tailDecay=Math.Exp(-1.0/(rate*.002));
            double cutoff = DragMath.Clamp(1600 / Math.Sqrt(litres / 2) * sharpness, 500, Math.Min(3800, rate * .25));
            fast = 1 - Math.Exp(-2 * Math.PI * cutoff / rate);
            slow = 1 - Math.Exp(-2 * Math.PI * 90 / rate);
        }
        public void Trigger(double eventStrength, bool twoStep)
        {
            retriggerTail=lastOutput;
            age = 0; strength = DragMath.Clamp(eventStrength, 0, 1);
            width = DragMath.Clamp(.0028 * Math.Sqrt(litres / 2) / sharpness, .0012, .006) * (1 + Noise() * .12);
            decay = (twoStep ? .010 : .016) * decayMultiplier * (1 + Noise() * .15);
            // Keep the filter state continuous when rapid events overlap.
        }
        public void Clear() { age = 1; air = airSecond = airBass = retriggerTail = lastOutput = 0; }
        public double Sample()
        {
            if (age >= Math.Max(.10, decay * 9)) {lastOutput=0;return 0;}
            air += fast * (Noise() - air); airSecond += fast * (air - airSecond); airBass += slow * (airSecond - airBass);
            double u = age / width;
            double pressure = (1 - 2 * u * u) * Math.Exp(-u * u);
            double attack = 1 - Math.Exp(-age / .0006);
            double burst = (airSecond - airBass) * Math.Exp(-age / decay);
            double value = (.65 * pressure + 1.3 * burst) * attack * strength * gain;
            age += 1.0 / rate;
            value+=retriggerTail;retriggerTail*=tailDecay;lastOutput=value;
            return value;
        }
    }
}
