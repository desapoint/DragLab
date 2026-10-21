using System;
using System.Threading;
namespace DragLab.Domain
{
    // Owner-thread chemistry, attached only to audible simulations. Never serialized into player state.
    public sealed class RuntimeExhaustState
    {
        public readonly ExhaustMixtureModel mixture;
        public struct Release { public double time,strength; }
        sealed class ReleaseSlot { public Release value; public int sequence; }
        readonly ReleaseSlot[] releases = new ReleaseSlot[256];
        int releaseSequence;
        readonly double engineOrigin;
        public int ReleaseSequence => Volatile.Read(ref releaseSequence);
        public void RecordRelease(double strength, double engineTime = -1)
        {
            int next=releaseSequence+1;var slot=releases[next&255];
            Volatile.Write(ref slot.sequence,-next);slot.value=new Release{time=engineTime<0?mixture.Time:Math.Max(0,engineTime-engineOrigin),strength=strength};
            Volatile.Write(ref slot.sequence,next);Volatile.Write(ref releaseSequence,next);
        }
        public bool ReadRelease(int sequence,out Release value)
        {var slot=releases[sequence&255];int before=Volatile.Read(ref slot.sequence);value=slot.value;return before==sequence&&before==Volatile.Read(ref slot.sequence);}
        readonly ExhaustMixtureModel.Input input = new ExhaustMixtureModel.Input();
        public readonly double lengthM;
        public const double HeaderLengthM = 1.1, TurbineLengthM = .18;
        public static double PartLength(ExhaustPart p) => p.kind == ExhaustPartKind.AbsorptiveSilencer || p.kind == ExhaustPartKind.ChamberedSilencer ? .65 : p.kind == ExhaustPartKind.Resonator ? .32 : .22;
        public static double PathLength(EngineSoundProfile profile)
        {
            double length = HeaderLengthM + (profile.turboCount > 0 ? TurbineLengthM : 0) + .8;
            foreach (var part in profile.exhaustSystem.parts) length += PartLength(part);
            return length;
        }
        public RuntimeExhaustState(EngineSoundProfile profile, CarState state)
        {
            engineOrigin=state.engineTime;
            for(int i=0;i<releases.Length;i++)releases[i]=new ReleaseSlot();
            lengthM = PathLength(profile);
            bool diesel = profile.fueling == FuelSystem.Diesel;
            mixture = new ExhaustMixtureModel(20261020, profile.layout + ":" + profile.cylinders + ":" + profile.litres.ToString("R", System.Globalization.CultureInfo.InvariantCulture), lengthM,
                .027 * Math.Pow(profile.litres / 2, .18), initialK: 330 + 280 * state.engineTemperature,
                stoichOxygenPerFuel: diesel ? 3.36 : 3.43, heatingJPerKg: diesel ? 43000000 : 44000000);
        }
        public void Step(VehicleSpec spec, InductionProfile induction, CarState state, double physicalFuelLitresS, double dt)
        {
            input.dt = dt; input.fuelKg = physicalFuelLitresS * spec.fuel.densityKgPerLitre * dt;
            input.airKg = state.rpm <= 0 || state.stalled || state.engineFailed ? 0 : Math.Max(state.airflowKgS, induction.BaseAirflow(state.rpm) * .12) * dt;
            input.intakeK = DragMath.Clamp(state.chargeTemperatureK > 0 ? state.chargeTemperatureK : induction.data.ambientTemperatureK, 250, 1500);
            var charge = CombustionExhaustBoundary.Evaluate(spec, state, input.fuelKg, input.airKg, input.intakeK);
            input.chamberBurnFraction = charge.burnFraction;
            input.inletK = charge.outletK;
            input.flameFraction = charge.flameFraction;
            input.wallTargetK = DragMath.Clamp(330 + 280 * state.engineTemperature, 250, 1800);
            input.cause = state.antiLagActive ? AfterfireCause.AntiLag : state.launchControlActive ? AfterfireCause.TwoStep : state.limiter && state.activeCutMethod==CutMethod.Ignition ? AfterfireCause.IgnitionLimiter : state.activeCutMethod==CutMethod.Fuel && state.combustionFraction<=0 ? AfterfireCause.FuelCutResidual : state.cylinderCombustionFactor<.95 ? AfterfireCause.PoorCombustion : AfterfireCause.ResidualMixture;
            input.rpm = state.rpm; input.limiter = state.limiter; input.twoStep = state.launchControlActive;
            input.turboBoost = Math.Max(0, state.turboBoostBar);
            mixture.Step(input);
        }
    }
}
