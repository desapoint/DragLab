using System;

namespace DragLab.Domain
{
    // Uniform annular-disc approximation. Fictional calibration, not OEM data.
    [Serializable]
    public sealed class FlywheelSpec
    {
        public int version;
        public float massKg = 8, outerRadiusM = .15f, innerRadiusM = .025f;
        public FlywheelSpec Copy() { return (FlywheelSpec)MemberwiseClone(); }
        public double Inertia { get { return .5 * massKg * (outerRadiusM * outerRadiusM + innerRadiusM * innerRadiusM); } }
        public string Validate()
        {
            if (!Finite(massKg) || massKg < .5 || massKg > 35) return "Flywheel mass must be 0.5–35 kg.";
            if (!Finite(outerRadiusM) || outerRadiusM < .05 || outerRadiusM > .3) return "Flywheel outer radius must be 0.05–0.3 m.";
            if (!Finite(innerRadiusM) || innerRadiusM < 0 || innerRadiusM >= outerRadiusM) return "Flywheel inner radius must be smaller than its outer radius.";
            return null;
        }
        static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
    }

    public static class FlywheelDynamics
    {
        public const double RpmPerRadian = 30 / Math.PI;
        public static double Mass(VehicleSpec spec, Tune tune)
        {
            var part = spec.hasInstalledParts ? spec.FittedFlywheel : null;
            if (spec.hasInstalledParts && part == null) return 0;
            return part != null && part.massKg > 0 ? part.massKg : (spec.flywheel ?? new FlywheelSpec()).massKg * tune.FlywheelMass;
        }
        public static double Inertia(VehicleSpec spec, Tune tune)
        {
            var part = spec.hasInstalledParts ? spec.FittedFlywheel : null;
            if (spec.hasInstalledParts && part == null) return 0;
            // Zero absolute ratings retain the earlier scale-only snapshot contract.
            return part != null && part.inertiaKgM2 > 0 ? part.inertiaKgM2 : (spec.flywheel ?? new FlywheelSpec()).Inertia * tune.FlywheelMass;
        }
        public static void Publish(VehicleSpec spec, Tune tune, CarState state)
        {
            double omega = Math.Max(0, state.rpm) / RpmPerRadian;
            state.flywheelInertiaKgM2 = Inertia(spec, tune);
            state.engineRotationalInertiaKgM2 = spec.RotationalInertia(tune);
            state.flywheelEnergyJ = .5 * state.flywheelInertiaKgM2 * omega * omega;
            state.flywheelAngularMomentum = state.flywheelInertiaKgM2 * omega;
        }
        // Finite clutch torque changes angular momentum; changing gear alone does not.
        public static double SynchronizingTorque(double rpm, double targetRpm, double inertia, double dt, double capacity)
        { return DragMath.Clamp((rpm - targetRpm) * inertia / (RpmPerRadian * dt), -capacity, capacity); }
        // Engineering fixture sizing, not automatic hardware installation. Leave
        // the stock/explicit rating intact so under-rated clutches can still slip.
        public static double MatchedClutchCapacity(VehicleSpec spec, Tune tune, double synchronizationSeconds = .20)
        {
            double peak = 0;
            for (double rpm = spec.idleRpm; rpm <= spec.redlineRpm; rpm += 25) peak = Math.Max(peak, DragMath.Torque(spec, tune, rpm));
            double drop = spec.redlineRpm * (1 - spec.gears[1] / spec.gears[0]);
            double headroom = spec.RotationalInertia(tune) * drop / (RpmPerRadian * synchronizationSeconds);
            return (peak + headroom) / tune.CapacityMultiplier;
        }
    }
}
