using System;

namespace DragLab.Domain
{
    [Serializable]
    public sealed class EngineBenchControls
    {
        public bool engineEnabled = true, fixedRpm, launchControls = true, allowFailure;
        public float throttle, clutch, brake, load, roadSpeedKmh, tireSlip;
        public float targetRpm = 3000;
        public float warmthOverride = -1, damageOverride = -1;
        public int gear;
        public EngineBenchControls Copy() { return (EngineBenchControls)MemberwiseClone(); }
    }

    // An endless dynamometer fixture. It shares engine/induction/limiter/audio physics,
    // but has no race, countdown, finish, rewards, saves or vehicle translation.
    public sealed class EngineTestBench
    {
        public readonly VehicleSpec spec;
        public readonly Tune tune;
        public readonly EngineBehaviorProfile engine;
        public readonly InductionProfile induction;
        public readonly TransmissionSpec transmission;
        public readonly WheelProfile wheels;
        public CarState state { get; private set; }
        public double audioLoad { get; private set; }
        public int ticks { get; private set; }
        double shiftTargetRpm, synchronizationRemaining;
        public EngineTestBench(VehicleSpec source, Tune sourceTune, CarState previous = null)
        {
            spec = source.Copy(); tune = sourceTune.Copy(); tune.ClampTo(spec);
            string invalid = spec.Validate(); if (invalid != null) throw new ArgumentException(invalid);
            engine = new EngineBehaviorProfile(spec, tune); induction = new InductionProfile(spec, tune);
            transmission = TransmissionDynamics.Configuration(spec, tune);
            wheels = new WheelProfile(spec, tune);
            state = previous ?? new CarState { rpm = spec.idleRpm, engineTemperature = tune.coldStart ? 0 : 1 };
            state.cvtRatio = transmission.cvtMaxRatio; state.effectiveRollingMassKg = wheels.effectiveMass; state.wheelRadiusM = wheels.radius;
            state.finished = state.disqualified = false; state.distance = 0;
            FlywheelDynamics.Publish(spec, tune, state);
        }
        public void SetGear(int gear)
        {
            gear = Math.Max(0, Math.Min(spec.gears.Length, gear));
            if (transmission.type == TransmissionType.CVT && gear > 0) gear = 1;
            int old = state.gear; if (old == gear) return;
            if (old > 0 && gear > 0)
            {
                shiftTargetRpm = Math.Max(spec.idleRpm, state.rpm * spec.gears[gear - 1] / spec.gears[old - 1]);
                synchronizationRemaining = .6;
                state.shiftRemaining = TransmissionDynamics.ShiftSeconds(transmission, spec, tune);
            }
            state.gear = gear; state.launched = gear > 0;
            if (gear == 0) { state.shiftRemaining = 0; synchronizationRemaining = 0; }
        }
        public void Step(EngineBenchControls control)
        {
            const double dt = DragMath.FixedStep; ticks++;
            SetGear(control.gear);
            state.throttle = control.engineEnabled ? DragMath.Clamp(control.throttle, 0, 1) : 0;
            state.clutch = DragMath.Clamp(control.clutch, 0, 1); state.brake = DragMath.Clamp(control.brake, 0, 1);
            state.speed = DragMath.Clamp(control.roadSpeedKmh, 0, 500) / 3.6;
            state.slip = DragMath.Clamp(control.tireSlip, 0, 2);
            state.stagingBrakesHeld = false;
            if (control.warmthOverride >= 0) state.engineTemperature = DragMath.Clamp(control.warmthOverride, 0, 1);
            if (control.damageOverride >= 0) { state.engineDamage = DragMath.Clamp(control.damageOverride, 0, 1); state.engineFailed = state.engineDamage >= 1; }
            if (control.fixedRpm) { state.rpm = DragMath.Clamp(control.targetRpm, spec.idleRpm, spec.redlineRpm * 1.5); synchronizationRemaining = 0; }
            audioLoad = DragMath.Clamp(control.load, 0, 1);
            if (!control.engineEnabled)
            {
                state.rpm = state.engineTorqueNm = state.engineHorsepower = state.transmittedTorqueNm = 0;
                state.launchControlActive = state.antiLagActive = false; state.exhaustEnergy = state.combustionFraction = 0;
                InductionDynamics.Step(induction, state, 0, 0, false, true, dt); FlywheelDynamics.Publish(spec, tune, state); return;
            }
            if (!control.allowFailure) { state.engineFailed = state.stalled = false; state.engineDamage = Math.Min(.95, state.engineDamage); }
            if (!state.engineFailed && !state.stalled) state.rpm = Math.Max(spec.idleRpm, state.rpm);
            EngineBehaviorDynamics.BeginTick(spec, tune, engine, state, state.throttle, control.launchControls && state.gear == 0, dt);
            bool shifting = state.shiftRemaining > 0;
            bool shaftCut = shifting && transmission.type == TransmissionType.Manual;
            // A manual handover disconnects the shaft; it does not release W or
            // shut down combustion. Explicit engine limiter cuts remain physical.
            state.transmissionTorqueCut = false;
            InductionDynamics.Step(induction, state, state.throttle, audioLoad, state.transmissionTorqueCut, state.combustionFraction <= 0, dt);
            double torque = InductionDynamics.Torque(induction, state, state.rpm, state.throttle) * state.combustionFraction;
            double engagement = state.gear == 0 ? 1 : 1 - state.clutch;
            double retained = shifting ? TransmissionDynamics.Retention(transmission) : 1;
            state.transmittedTorqueNm = shaftCut ? 0 : torque * audioLoad * engagement * retained;
            audioLoad *= engagement * retained;
            if (state.gear > 0) TransmissionDynamics.Ratio(transmission, spec, tune, wheels, state, dt);
            if (!control.fixedRpm)
            {
                // The fixture holds load rather than advancing a road vehicle. Its
                // virtual clutch synchronizes to the ratio-derived handover speed.
                if (!shifting && synchronizationRemaining > 0 && state.gear > 0)
                {
                    double capacity = spec.ClutchCapacity * tune.CapacityMultiplier * engagement;
                    double correction = FlywheelDynamics.SynchronizingTorque(state.rpm, shiftTargetRpm, spec.RotationalInertia(tune), dt, capacity);
                    state.transmittedTorqueNm = DragMath.Clamp(state.transmittedTorqueNm + correction, -capacity, capacity);
                    synchronizationRemaining = Math.Max(0, synchronizationRemaining - dt);
                    if (Math.Abs(state.rpm - shiftTargetRpm) < 80) synchronizationRemaining = 0;
                }
                double resistance = state.transmittedTorqueNm + state.brake * (18 + state.rpm * .01) * engagement;
                state.rpm = ClutchDynamics.FreeRev(spec, tune, state.rpm, state.throttle, dt, ref state.limiter,
                    torque - resistance, engine, state);
            }
            EngineBehaviorDynamics.FinishTick(spec, engine, state, false, dt);
            if (!control.allowFailure) { state.engineFailed = state.stalled = false; state.engineDamage = Math.Min(.95, state.engineDamage); }
            if (!state.engineFailed && !state.stalled) state.rpm = Math.Max(spec.idleRpm, state.rpm);
            InductionDynamics.UpdateOutput(induction, state, state.throttle, state.combustionFraction <= 0 || state.transmissionTorqueCut);
            state.shiftRemaining = Math.Max(0, state.shiftRemaining - dt);
            double angle = state.speed / wheels.radius * 180 / Math.PI * dt;
            state.frontWheelAngleDegrees += angle * (spec.drive == DriveType.RWD ? 1 : 1 + state.slip);
            state.rearWheelAngleDegrees += angle * (spec.drive == DriveType.FWD ? 1 : 1 + state.slip);
            state.wheelAngleDegrees += angle;
            FlywheelDynamics.Publish(spec, tune, state);
        }
    }

    public enum EngineBenchScenario { WarmIdle, NeutralBlips, LoadedPullAndShifts, TwoStep, TurboLift, Burnout, ColdWarmup, RpmSweep, FixedRpmHold, FlywheelCoastDown, DynamicPullAndShifts }
    public static class EngineBenchScenarios
    {
        public static int DurationTicks(EngineBenchScenario scenario) { return scenario == EngineBenchScenario.ColdWarmup ? 7200 : 1440; }
        public static string Description(EngineBenchScenario scenario)
        {
            switch (scenario)
            {
                case EngineBenchScenario.WarmIdle: return "12 s warm idle; no throttle or load.";
                case EngineBenchScenario.NeutralBlips: return "12 s neutral: idle 0–3 s, gas 3–3.5 / 6–6.5 / 9–10 s, release between blips.";
                case EngineBenchScenario.LoadedPullAndShifts: return "12 s: idle 0–2 s, loaded RPM sweep 2–9 s, shift at 5/7 s, lift at 9 s.";
                case EngineBenchScenario.TwoStep: return "12 s: idle 0–2 s, hold gas on configured two-step 2–9 s, release.";
                case EngineBenchScenario.TurboLift: return "12 s: idle 0–2 s, 5000 RPM / full gas / full load 2–7 s, lift 7–9 s, gas again.";
                case EngineBenchScenario.Burnout: return "12 s: idle 0–2 s, 4500 RPM / 40 km/h rollers / 90% slip 2–9 s, release.";
                case EngineBenchScenario.ColdWarmup: return "60 s cold-start idle using configured warmup/cam/fueling.";
                case EngineBenchScenario.FixedRpmHold: return "12 s at the captured target RPM, throttle, load, gear, rollers and slip settings.";
                case EngineBenchScenario.FlywheelCoastDown: return "12 s neutral: fixture holds 5000 RPM / no gas for 0–2 s, releases at 2 s; physical coast to idle. Compare flywheel A/B.";
                case EngineBenchScenario.DynamicPullAndShifts: return "12 s dynamic pull: gas 2–9 s, shifts at 5/7 s, release at 9 s; RPM follows inertia, clutch and load.";
                default: return "12 s RPM sweep from idle to redline at 65% gas / 50% load; no shift edges.";
            }
        }
        public static EngineBenchControls At(EngineBenchScenario scenario, int tick, VehicleSpec spec, EngineBenchControls configured = null)
        {
            double time = tick * DragMath.FixedStep;
            var c = new EngineBenchControls();
            if (scenario == EngineBenchScenario.FixedRpmHold) { c = configured == null ? c : configured.Copy(); c.fixedRpm = true; return c; }
            if (scenario == EngineBenchScenario.WarmIdle) return c;
            if (scenario == EngineBenchScenario.NeutralBlips)
            { c.throttle = time >= 3 && time < 3.5 || time >= 6 && time < 6.5 || time >= 9 && time < 10 ? 1 : 0; return c; }
            if (scenario == EngineBenchScenario.TwoStep) { c.throttle = time >= 2 && time < 9 ? 1 : 0; return c; }
            if (scenario == EngineBenchScenario.ColdWarmup) return c;
            if (scenario == EngineBenchScenario.FlywheelCoastDown)
            { c.fixedRpm = time < 2; c.targetRpm = 5000; c.launchControls = false; return c; }
            if (scenario == EngineBenchScenario.DynamicPullAndShifts)
            { c.gear = time < 2 ? 0 : time < 5 ? 1 : time < 7 ? 2 : 3; c.throttle = time >= 2 && time < 9 ? .8f : 0; c.load = .65f; c.launchControls = false; c.roadSpeedKmh = time < 2 ? 0 : 60; return c; }
            if (scenario == EngineBenchScenario.RpmSweep)
            { c.fixedRpm = true; c.targetRpm = (float)(spec.idleRpm + (spec.redlineRpm - spec.idleRpm) * tick / DurationTicks(scenario)); c.throttle = .65f; c.load = .5f; c.gear = 1; return c; }
            if (time < 2) return c;
            c.gear = scenario == EngineBenchScenario.LoadedPullAndShifts ? (time < 5 ? 1 : time < 7 ? 2 : 3) : 1;
            c.fixedRpm = true;
            c.targetRpm = scenario == EngineBenchScenario.LoadedPullAndShifts ? (float)(2000 + Math.Min(1, (time - 2) / 7) * (spec.redlineRpm - 2200)) : scenario == EngineBenchScenario.Burnout ? 4500 : 5000;
            c.load = scenario == EngineBenchScenario.TurboLift ? 1 : .7f;
            c.throttle = time < (scenario == EngineBenchScenario.TurboLift ? 7 : 9) || scenario == EngineBenchScenario.TurboLift && time >= 9 ? 1 : 0;
            c.launchControls = false;
            c.roadSpeedKmh = scenario == EngineBenchScenario.Burnout ? 40 : 80;
            c.tireSlip = scenario == EngineBenchScenario.Burnout && time < 9 ? .9f : 0;
            return c;
        }
    }
}
