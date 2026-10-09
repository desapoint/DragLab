using System;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    // Engine-side inertia and bounded clutch capacity. Wheel inertia/contact is independently integrated by CarSimulation.
    public static class ClutchDynamics
    {
        const double RpmPerRadian = 60 / (2 * Math.PI);
        public static void UpdateLimiter(VehicleSpec spec, double rpm, ref bool cut)
        {
            if (rpm >= spec.redlineRpm) cut = true;
            else if (rpm <= spec.redlineRpm - spec.LimiterDrop) cut = false;
        }
        public static double FreeRev(VehicleSpec spec, Tune tune, double rpm, double gas, double dt, ref bool cut, double suppliedTorque = double.NaN, EngineBehaviorProfile engine = null, CarState state = null)
        {
            if (engine == null) UpdateLimiter(spec, rpm, ref cut);
            double torque = engine == null && cut ? 0 : double.IsNaN(suppliedTorque) ? DragMath.Torque(spec, tune, rpm) * gas : suppliedTorque;
            double fraction = state == null ? (cut ? 0 : 1) : state.combustionFraction;
            double drag = (18 + rpm * .003) * (1 - gas * fraction);
            if(state!=null)drag+=state.clutchViscousDragNm;
            double idle = spec.idleRpm;
            if (state != null)
            {
                idle *= 1 + state.idleRoughness * (.1 * Math.Sin(state.engineTime * 11) + .06 * Math.Sin(state.engineTime * 23));
                if (state.engineFailed || state.stalled) idle = 0;
            }
            double next = DragMath.Clamp(rpm + (torque - drag) / spec.RotationalInertia(tune) * RpmPerRadian * dt, idle, engine == null ? spec.redlineRpm + 100 : spec.redlineRpm * 1.5);
            if (engine == null) UpdateLimiter(spec, next, ref cut);
            return next;
        }
        public static double Transmit(VehicleSpec spec, Tune tune, CarState state, double gas, double wheelRpm,
            bool manualPedal, bool launchControl, bool shifting, double dt, InductionProfile induction = null,
            EngineBehaviorProfile engine = null, WheelProfile wheels = null, double ratio = 1, double torqueRetention = 1, double reflectedLoadInertia = double.NaN, PartDefinition clutchHardware = null)
        {
            if (engine == null) UpdateLimiter(spec, state.rpm, ref state.limiter);
            double torque = engine == null && state.limiter ? 0 : induction == null ? DragMath.Torque(spec, tune, state.rpm) * gas : InductionDynamics.Torque(induction, state, state.rpm, gas);
            if (engine != null) torque *= state.combustionFraction;
            var hardware=clutchHardware??Array.Find(spec.fittedParts,p=>p.category==PartCategory.Clutch);
            if (shifting || manualPedal && state.clutch >= .98)
            {
                if (!manualPedal) state.clutch = 1;
                double drag=hardware!=null&&hardware.clutchEnvironment==ClutchEnvironment.Wet?Math.Min(5,Math.Abs(state.rpm-wheelRpm)*.0008)*state.clutchOilFactor:0;
                state.clutchViscousDragNm=Math.Sign(state.rpm-wheelRpm)*drag;
                state.rpm = FreeRev(spec, tune, state.rpm, gas, dt, ref state.limiter, torque, engine, state);
                state.clutchSlipRpm = Math.Max(0, state.rpm - wheelRpm); state.transmittedTorqueNm = state.clutchViscousDragNm; return state.transmittedTorqueNm;
            }
            double maxCapacity = spec.ClutchCapacity * tune.CapacityMultiplier * state.clutchCapacityFactor;
            double engagement = hardware==null?(manualPedal ? DragMath.Clamp((.85 - state.clutch) / .55, 0, 1) : 1):ClutchHardware.Engagement(hardware,manualPedal?state.clutch:0,state.rpm);
            state.clutchViscousDragNm=hardware!=null&&hardware.clutchEnvironment==ClutchEnvironment.Wet?Math.Min(5,Math.Abs(state.rpm-wheelRpm)*.0008)*state.clutchOilFactor:0;
            double capacity = maxCapacity * engagement * torqueRetention;
            double inertia = spec.RotationalInertia(tune);
            if (launchControl)
            {
                // Assisted clutch supplies only the load compatible with holding launch RPM.
                double correction = (state.rpm - tune.launchRpm) * inertia / (RpmPerRadian * Math.Max(.04, dt));
                capacity = DragMath.Clamp(torque + correction, 0, maxCapacity*engagement);
                state.clutch = maxCapacity > 0 ? 1 - capacity / maxCapacity : 1;
            }
            else if (!manualPedal) state.clutch = 0;
            double targetRpm = Math.Max(spec.idleRpm, wheelRpm);
            // Rotational energy above the coupled/idle speed can briefly supply extra torque.
            double inertiaTorque = Math.Max(0, state.rpm - targetRpm) * inertia / (RpmPerRadian * dt);
            double transmitted = Math.Min(capacity, torque + inertiaTorque);
            if (engine != null && wheels != null)
            {
                // Synchronization conserves engine/vehicle momentum through reflected rolling inertia.
                // A dumped clutch can pull a low-rev engine below idle; assist deliberately avoids this.
                double vehicleInertia = wheels.effectiveMass * wheels.radius * wheels.radius / (ratio * ratio * spec.drivetrainEfficiency);
                if (state.openCenterFreeBranch) vehicleInertia = AxleDriveDynamics.FreeReflectedInertia(AxleDriveDynamics.Configuration(spec, tune), state, ratio / tune.finalDrive);
                if (!double.IsNaN(reflectedLoadInertia)) vehicleInertia = reflectedLoadInertia;
                double reducedInertia = inertia * vehicleInertia / (inertia + vehicleInertia);
                double sync = (state.rpm - (manualPedal || launchControl ? wheelRpm : targetRpm)) * reducedInertia / (RpmPerRadian * dt);
                double synchronousTorque = torque * vehicleInertia / (inertia + vehicleInertia) + sync;
                transmitted = launchControl ? DragMath.Clamp(Math.Min(transmitted, synchronousTorque), -capacity, capacity) : DragMath.Clamp(synchronousTorque, -capacity, capacity);
            }
            double engineDrag = engine == null ? 0 : (18 + state.rpm * .003) * (1 - gas * state.combustionFraction);
            engineDrag+=state.clutchViscousDragNm*.15;
            state.rpm = DragMath.Clamp(state.rpm + (torque - transmitted - engineDrag) / inertia * RpmPerRadian * dt,
                state.engineFailed || state.stalled || manualPedal && !state.stallPrevention ? 0 : spec.idleRpm, engine == null ? spec.redlineRpm + 100 : spec.redlineRpm * 1.5);
            state.clutchSlipRpm = Math.Max(0, state.rpm - wheelRpm);
            if (engine == null) UpdateLimiter(spec, state.rpm, ref state.limiter);
            state.transmittedTorqueNm = transmitted;
            return transmitted;
        }
    }
}
