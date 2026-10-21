using System;

namespace DragLab.Domain
{
    // Reduced-order cylinder-to-exhaust boundary, evaluated from delivered charge and
    // actuator outputs. Operating-mode labels have no place in the chemistry inputs.
    public static class CombustionExhaustBoundary
    {
        public struct Charge
        {
            public double burnFraction, outletK, flameFraction, burnSeconds, exhaustOpenDegrees;
        }

        public static Charge Evaluate(VehicleSpec spec, CarState state, double fuelKg, double airKg, double intakeK)
        {
            double rpm = Math.Max(1, state.rpm);
            double oxygenPerFuel = spec.engineBehavior.fueling == FuelSystem.Diesel ? 3.36 : 3.43;
            double heat = spec.engineBehavior.fueling == FuelSystem.Diesel ? 43000000 : 44000000;
            double phi = fuelKg * oxygenPerFuel / Math.Max(1e-15, airKg * .232);
            double mixture = Math.Exp(-Math.Pow(Math.Log(Math.Max(.01, phi)) / 1.6, 2));
            double bore = spec.engineBoreMm > 0 ? spec.engineBoreMm / 1000 : Math.Pow(spec.DisplacementLitres / Math.Max(1, spec.sound.cylinders) / 1000, 1.0 / 3);
            double advance = state.combustionAdvanceDegrees;
            // Turbulent-flame travel surrogate: bore, RPM, mixture, charge temperature,
            // and expansion after TDC all matter. Constants require measured calibration.
            double expansion = 1 + Math.Max(0, -advance) / 35;
            double flameSpeed = (9 + 12 * Math.Sqrt(rpm / 3000)) * Math.Sqrt(intakeK / 330) * (.45 + .55 * mixture) / expansion;
            double duration = DragMath.Clamp(.8 * bore / flameSpeed, .0006, .018);
            double valveOpen = 135 - (spec.valvetrain.durationAt050Degrees - 200) * .15 - spec.valvetrain.camTimingDegrees;
            valveOpen = DragMath.Clamp(valveOpen, 95, 165);
            double available = Math.Max(0, (valveOpen + advance) / (6 * rpm) - .0002);
            double progress = available / duration;
            double completion = 1 - Math.Exp(-5 * progress * progress * progress);
            double firing = DragMath.Clamp(state.ignitionFiringFraction, 0, 1);
            double efficiency = DragMath.Clamp(state.chamberCombustionEfficiency * state.cylinderCombustionFactor, 0, 1);
            if (state.stalled || state.engineFailed || !state.engineSupplyAvailable || fuelKg <= 0 || airKg <= 0) firing = 0;
            double burn = firing * efficiency * completion;
            double burned = Math.Min(fuelKg * burn, airKg * .232 / oxygenPerFuel);
            // Late heat has less expansion work extracted before the valve opens.
            double late = DragMath.Clamp((30 - advance) / 90, 0, 1);
            double outlet = intakeK + (.28 + .32 * late) * burned * heat / Math.Max(1e-12, (fuelKg + airKg) * 1100);
            double flame = firing * efficiency * 15 * progress * progress * Math.Exp(-5 * progress * progress * progress);
            return new Charge { burnFraction = burn, outletK = DragMath.Clamp(outlet, intakeK, 1800),
                flameFraction = DragMath.Clamp(flame, 0, 1), burnSeconds = duration, exhaustOpenDegrees = valveOpen };
        }
    }
}
