using System;
using System.Threading;
using System.Text;
using System.Globalization;

namespace DragLab.Domain
{
    public enum AfterfireCause { ResidualMixture, IgnitionLimiter, FuelCutResidual, TwoStep, AntiLag, PoorCombustion }
    // Reduced-order causal exhaust transport; observes physical delivery without changing torque/resources.
    public class ExhaustMixtureModel
    {
        [Serializable] public sealed class Input
        {
            public double dt, fuelKg, airKg, chamberBurnFraction, intakeK = 298, wallTargetK = 650;
            public double inletK, flameFraction;
            public double turboBoost, rpm;
            public bool limiter, twoStep;
            public AfterfireCause cause;
        }
        [Serializable] public struct Event
        {
            public int index, zone;
            public int frontId;
            public double pressurePa, riseSeconds, burnSeconds, reactingVolumeM3, flowMps;
            public double time, position, fuelKg, oxygenKg, releasedJ, acousticJ, gasK, wallK, intakeK, probability;
            public ulong seed;
            public double turboBoost, rpm;
            public bool limiter, twoStep;
            public AfterfireCause cause;
        }
        [Serializable] public sealed class Zone
        {
            public double fuelKg, oxygenKg, gasK, wallK, flame, pressurePa;
            public int frontId;
        }
        public readonly Zone[] zones;
        public const int HistoryCapacity = 4096;
        sealed class Slot { public Event value; public int sequence; }
        readonly Slot[] history = new Slot[HistoryCapacity];
        int sequence;
        double publishedTime;
        public int Sequence => Volatile.Read(ref sequence);
        public double Time => Volatile.Read(ref publishedTime);
        public bool Read(int index, out Event value)
        {
            var slot = history[index & (HistoryCapacity - 1)];
            int before = Volatile.Read(ref slot.sequence); value = slot.value;
            return before == index && before == Volatile.Read(ref slot.sequence);
        }
        void Record(Event value)
        {
            int next = sequence + 1; var slot = history[next & (HistoryCapacity - 1)];
            Volatile.Write(ref slot.sequence, -next); slot.value = value;
            Volatile.Write(ref slot.sequence, next); Volatile.Write(ref sequence, next);
        }
        readonly ulong seedPrefix;
        static ulong Add(ulong hash, char c) { unchecked { return (hash ^ (byte)c) * 1099511628211UL; } }
        static ulong Digits(ulong hash, int number)
        {
            int divisor = 1; while (number / divisor >= 10 && divisor <= 100000000) divisor *= 10;
            do { hash = Add(hash, (char)('0' + number / divisor % 10)); divisor /= 10; } while (divisor > 0);
            return hash;
        }
        ulong EventSeed(int tickIndex, int tag) => Digits(Add(Digits(seedPrefix, tickIndex), ':'), tag);
        public double FuelIn { get; private set; }
        public double FuelOut { get; private set; }
        public double FuelBurned { get; private set; }
        public double OxygenIn { get; private set; }
        public double OxygenOut { get; private set; }
        public double OxygenBurned { get; private set; }
        public double UnburnedIn { get; private set; }
        public double ChamberBurned { get; private set; }
        public double ExhaustHeatJ { get; private set; }
        public double AcousticEnergyJ { get; private set; }
        public const double OxygenPerFuel = 3.43, HeatJPerKg = 44000000;
        public readonly double stoichOxygenPerFuel, heatingJPerKg;
        readonly long master;
        readonly string id;
        readonly double massPerZone, lengthM, volumePerZone, area;
        int tick, frontSequence;
        double time;
        const double Cp = 1100, AmbientK = 298;
        static double Clamp(double x, double a, double b) => Math.Max(a, Math.Min(b, x));
        static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        public double RetainedFuel { get { double x = 0; foreach (var z in zones) x += z.fuelKg; return x; } }
        public double RetainedOxygen { get { double x = 0; foreach (var z in zones) x += z.oxygenKg; return x; } }

        public ExhaustMixtureModel(long master, string id, double lengthM = 2.5, double radiusM = .032, int count = 8, double initialK = 650,
            double stoichOxygenPerFuel = OxygenPerFuel, double heatingJPerKg = HeatJPerKg)
        {
            if (count < 2 || count > 32 || !Finite(lengthM) || lengthM <= 0 || !Finite(radiusM) || radiusM <= 0 || !Finite(initialK) || initialK < 250 || initialK > 2000)
                throw new ArgumentException("Invalid preview exhaust geometry/temperature.");
            if (!Finite(stoichOxygenPerFuel) || stoichOxygenPerFuel <= 0 || !Finite(heatingJPerKg) || heatingJPerKg <= 0) throw new ArgumentException("Invalid fuel chemistry.");
            this.master = master; this.id = id; this.lengthM = lengthM;
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes("draglab-v8:" + master.ToString(CultureInfo.InvariantCulture) + ":" + id + ":")) { unchecked { hash = (hash ^ b) * 1099511628211UL; } }
            seedPrefix = hash;
            for (int i = 0; i < history.Length; i++) history[i] = new Slot();
            this.stoichOxygenPerFuel = stoichOxygenPerFuel; this.heatingJPerKg = heatingJPerKg;
            // Fixed reference gas holdup; temperature/pressure waves remain reduced-order approximations.
            area = Math.PI * radiusM * radiusM;
            volumePerZone = area * lengthM / count;
            massPerZone = volumePerZone * 101325 / (287 * 650);
            if (!Finite(massPerZone) || massPerZone <= 0) throw new ArgumentException("Invalid gas holdup.");
            zones = new Zone[count];
            for (int i = 0; i < count; i++) zones[i] = new Zone { gasK = initialK, wallK = initialK };
        }
        static double Uniform(ulong seed)
        {
            unchecked { seed += 0x9e3779b97f4a7c15UL; seed = (seed ^ (seed >> 30)) * 0xbf58476d1ce4e5b9UL; seed = (seed ^ (seed >> 27)) * 0x94d049bb133111ebUL; seed ^= seed >> 31; }
            return ((seed >> 11) + .5) / 9007199254740992.0;
        }
        public void Step(Input input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!Finite(input.dt) || input.dt <= 0 || input.dt > .1 || !Finite(input.fuelKg) || input.fuelKg < 0 ||
                !Finite(input.airKg) || input.airKg < 0 || !Finite(input.chamberBurnFraction) || input.chamberBurnFraction < 0 || input.chamberBurnFraction > 1 ||
                !Finite(input.intakeK) || input.intakeK < 250 || input.intakeK > 1500 || !Finite(input.wallTargetK) || input.wallTargetK < 250 || input.wallTargetK > 1800 ||
                !Finite(input.inletK) || input.inletK != 0 && (input.inletK < 250 || input.inletK > 1800) ||
                !Finite(input.flameFraction) || input.flameFraction < 0 || input.flameFraction > 1)
                throw new ArgumentException("Invalid mixture input.");
            double flowKg = input.airKg + input.fuelKg;
            if (!Finite(flowKg) || flowKg / massPerZone > 4096 * .2) throw new ArgumentException("Preview flow exceeds prepared substep budget.");
            int steps = Math.Max(1, (int)Math.Ceiling(flowKg / massPerZone / .2));
            if (steps > 4096) throw new ArgumentException("Preview flow exceeds prepared substep budget.");
            double oxygen = input.airKg * .232;
            // Chamber burning cannot consume more oxygen than the incoming charge contains.
            double chamberBurn = Math.Min(input.fuelKg * input.chamberBurnFraction, oxygen / stoichOxygenPerFuel);
            double unburned = input.fuelKg - chamberBurn, remainingOxygen = oxygen - chamberBurn * stoichOxygenPerFuel;
            FuelIn += input.fuelKg; ChamberBurned += chamberBurn; UnburnedIn += unburned; OxygenIn += remainingOxygen;
            double dt = input.dt / steps, fraction = 1 - Math.Exp(-flowKg / steps / massPerZone);
            // Calibrated chamber heat partition; no fabricated absolute exhaust-temperature measurement.
            double inletK = input.intakeK + (flowKg > 0 ? .28 * chamberBurn * heatingJPerKg / (flowKg * Cp) : 0);
            inletK = Clamp(inletK, input.intakeK, 1800);
            if (input.inletK > 0) inletK = input.inletK;
            for (int sub = 0; sub < steps; sub++)
            {
                // Reverse iteration uses each upstream zone's pre-advection state: no instantaneous full-path travel.
                for (int i = zones.Length - 1; i >= 0; i--)
                {
                    var z = zones[i]; double exitFuel = z.fuelKg * fraction, exitOxygen = z.oxygenKg * fraction;
                    double departingFlame = z.flame * fraction;
                    z.flame -= departingFlame;
                    z.fuelKg -= exitFuel; z.oxygenKg -= exitOxygen;
                    if (i == zones.Length - 1) { FuelOut += exitFuel; OxygenOut += exitOxygen; }
                    else
                    {
                        var downstream = zones[i + 1];
                        downstream.fuelKg += exitFuel; downstream.oxygenKg += exitOxygen;
                        if (departingFlame > downstream.flame && z.frontId != 0) downstream.frontId = z.frontId;
                        downstream.flame = Math.Min(1, downstream.flame + departingFlame);
                    }
                    double upstreamK = i == 0 ? inletK : zones[i - 1].gasK;
                    z.gasK += fraction * (upstreamK - z.gasK);
                }
                zones[0].fuelKg += unburned / steps; zones[0].oxygenKg += remainingOxygen / steps;
                // Flame activity is a transported reaction-progress tracer, not extra
                // fuel or an external energy source. Cold walls/flow extinguish it.
                zones[0].flame = Math.Min(1, zones[0].flame + fraction * input.flameFraction);
                for (int i = 0; i < zones.Length; i++)
                {
                    var z = zones[i];
                    z.flame *= Math.Exp(-dt / (.006 + .024 * Clamp((z.gasK - 400) / 600, 0, 1)));
                    if (z.flame < .015) z.frontId = 0;
                    double exchange = (z.wallK - z.gasK) * (1 - Math.Exp(-dt / .18));
                    z.gasK += exchange; z.wallK -= exchange * massPerZone * Cp / 180;
                    z.wallK += (input.wallTargetK - z.wallK) * (1 - Math.Exp(-dt / 3));
                    z.wallK += (AmbientK - z.wallK) * (1 - Math.Exp(-dt / 25));
                    double soundSpeed = Math.Sqrt(1.34 * 287 * Clamp(z.gasK, 250, 2500));
                    double density = 101325 / (287 * Math.Max(250, z.gasK));
                    double velocity = flowKg / input.dt / (density * area);
                    double ventSeconds = (lengthM * (1 - (i + .5) / zones.Length) + Math.Sqrt(area)) / (soundSpeed + velocity);
                    double ventDecay = Math.Exp(-dt / ventSeconds);
                    z.pressurePa *= ventDecay;
                    // Heat and mixture permit ignition; seeded stochastic induction delay chooses whether/when it catches.
                    double burnable = Math.Min(z.fuelKg, z.oxygenKg / stoichOxygenPerFuel);
                    if (burnable <= 1e-10) continue;
                    double equivalence = z.fuelKg * stoichOxygenPerFuel / Math.Max(1e-15, z.oxygenKg);
                    double flammable = Math.Exp(-Math.Pow(Math.Log(Math.Max(.0001, equivalence)) / 1.4, 2));
                    double temperature = .7 * z.gasK + .3 * z.wallK;
                    double thermal = Clamp((temperature - 620) / 350, 0, 1);
                    // An oxygen/fuel ratio alone is insufficient: mostly inert exhaust
                    // containing a trace of each cannot propagate a pressure-producing flame.
                    // Gasoline-vapor surrogate; temperature widens this authored flammability window.
                    double vaporFraction = z.fuelKg / .114 / (massPerZone / .029);
                    double lower = .014 * (1 - .35 * Clamp((temperature - 298) / 700, 0, 1));
                    double upper = .076 * (1 + .25 * thermal);
                    double concentration = Clamp((vaporFraction - lower) / (lower * .25), 0, 1) * Clamp((upper - vaporFraction) / (upper * .2), 0, 1);
                    double ignition = Math.Max(thermal, z.flame * Clamp((temperature - 450) / 250, 0, 1));
                    // A surviving tracer cannot sustain flame outside the local
                    // flammability/thermal window (including cold-wall quenching).
                    if (concentration <= 0 || flammable < .01 || ignition <= 0) { z.frontId = 0; continue; }
                    double probability = 1 - Math.Exp(-dt * 120 * ignition * flammable * concentration * Clamp(burnable / 1e-6, 0, 1));
                    ulong seed = EventSeed(tick, i * 8192 + sub);
                    if (z.frontId == 0 && Uniform(seed) >= probability) continue;
                    double severity = Clamp(flammable * concentration, 0, 1);
                    // Heat release competes with venting: a slowly burning, freely
                    // vented charge does not create the same pressure front as a rapid one.
                    double flameSpeed = (3 + 24 * severity) * Math.Sqrt(temperature / 650) * (1 + .03 * Math.Sqrt(Math.Max(0, velocity)));
                    double pocketLength = lengthM / zones.Length * Clamp(burnable / (massPerZone * .06), .08, 1);
                    double burnSeconds = Clamp(pocketLength / Math.Max(1, flameSpeed), .001, .16);
                    // Once ignited, burn at the finite local flame-travel rate. Do not
                    // re-roll ignition and consume an arbitrary fuel fraction every tick.
                    double burned = burnable * (1 - Math.Exp(-dt / burnSeconds));
                    double energy = burned * heatingJPerKg;
                    double confinement = ventSeconds / (ventSeconds + burnSeconds);
                    // Integrate finite heat release against venting; pressure is not
                    // reset for every numerical reaction packet.
                    z.pressurePa += .34 * energy / volumePerZone * ventSeconds / dt * (1 - ventDecay);
                    double rise = Clamp(burnSeconds * (.12 + .25 * (1 - severity)), .00025, .025);
                    double acoustic = energy * (.00015 + .004 * confinement * confinement);
                    if (z.frontId == 0) z.frontId = ++frontSequence;
                    z.flame = Math.Min(1, z.flame + burned / Math.Max(1e-15, massPerZone * .02));
                    z.fuelKg -= burned; z.oxygenKg -= burned * stoichOxygenPerFuel;
                    FuelBurned += burned; OxygenBurned += burned * stoichOxygenPerFuel;
                    ExhaustHeatJ += energy - acoustic; AcousticEnergyJ += acoustic;
                    Record(new Event { index = sequence, zone = i, time = time + (sub + 1) * dt,
                        position = (i + .5) / zones.Length, fuelKg = burned, oxygenKg = burned * stoichOxygenPerFuel,
                        releasedJ = energy, acousticJ = acoustic, gasK = z.gasK, wallK = z.wallK, intakeK = input.intakeK,
                        probability = probability, seed = seed, turboBoost = input.turboBoost, rpm = input.rpm, limiter = input.limiter, twoStep = input.twoStep, cause = input.cause,
                        frontId = z.frontId, pressurePa = z.pressurePa, riseSeconds = rise, burnSeconds = burnSeconds,
                        reactingVolumeM3 = area * pocketLength, flowMps = velocity });
                    z.gasK += (energy - acoustic) / (massPerZone * Cp);
                }
            }
            time += input.dt; tick++; Volatile.Write(ref publishedTime, time);
        }
    }
}
