using System;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    [Serializable]
    public sealed class InductionSpec
    {
        public int version;
        public InductionType factory;
        public BlowOffType factoryValve = BlowOffType.Recirculated;
        public bool torqueCurveIncludesFactoryInduction = true;
        public float ambientPressureBar = 1.013f, ambientTemperatureK = 298;
        public float turboBoostBar = .7f, turboThresholdRpm = 1800, turboFullBoostRpm = 3600;
        public float turboSpoolSeconds = .65f, turboCoastSeconds = .55f;
        public float turboUnitFlowKgS = .22f, turboEfficiency = .72f, turboBackpressureLoss = .025f;
        public float twinTurboSpoolFactor = .72f;
        public float superchargerBoostBar = .5f, superchargerFullBoostRpm = 1600;
        public float superchargerUnitFlowKgS = .22f, superchargerEfficiency = .68f;
        public float superchargerFrictionNm = 5, superchargerResponseSeconds = .025f;
        public float manifoldFillSeconds = .075f, valveReleaseSeconds = .045f;
        public float intercoolerEffectiveness = .72f, volumetricEfficiency = .9f, maxBoostBar = 2;
        public InductionSpec Copy() { return (InductionSpec)MemberwiseClone(); }
        public string Validate()
        {
            if (!Enum.IsDefined(typeof(InductionType), factory)) return "Invalid factory induction.";
            if (!Enum.IsDefined(typeof(BlowOffType), factoryValve) || factoryValve == BlowOffType.Factory) return "Invalid factory pressure-release valve.";
            if (!Range(ambientPressureBar, .6, 1.2) || !Range(ambientTemperatureK, 250, 330)) return "Invalid induction atmosphere.";
            if (!Range(turboBoostBar, 0, 2.5) || !Range(superchargerBoostBar, 0, 2) || !Range(maxBoostBar, .1, 3)) return "Invalid boost limit.";
            if (!Range(turboThresholdRpm, 500, 9000) || !Range(turboFullBoostRpm, turboThresholdRpm + 200, 13000)) return "Turbo full-boost RPM must exceed threshold.";
            if (!Range(turboSpoolSeconds, .05, 4) || !Range(turboCoastSeconds, .05, 4) || !Range(twinTurboSpoolFactor, .3, 2)) return "Invalid turbo response.";
            if (!Range(turboUnitFlowKgS, .01, 2) || !Range(superchargerUnitFlowKgS, .01, 2)) return "Invalid per-unit compressor flow.";
            if (!Range(turboEfficiency, .4, .9) || !Range(superchargerEfficiency, .4, .9) || !Range(intercoolerEffectiveness, 0, .95)) return "Invalid charge efficiency.";
            if (!Range(superchargerFullBoostRpm, 500, 10000) || !Range(superchargerResponseSeconds, .005, .3) || !Range(superchargerFrictionNm, 0, 80)) return "Invalid supercharger drive.";
            if (!Range(manifoldFillSeconds, .005, .5) || !Range(valveReleaseSeconds, .005, .4) || !Range(volumetricEfficiency, .5, 1.2) || !Range(turboBackpressureLoss, 0, .15)) return "Invalid manifold/engine settings.";
            return null;
        }
        static bool Range(float x, double lo, double hi) { return !float.IsNaN(x) && !float.IsInfinity(x) && x >= lo && x <= hi; }
    }

    public sealed class InductionProfile
    {
        public readonly VehicleSpec vehicle;
        public readonly Tune tune;
        public readonly InductionSpec data;
        public readonly InductionType type;
        public readonly BlowOffType valve;
        public readonly int turboCount, superchargerCount;
        public readonly double size, boostSetting, pulley, litres;
        public readonly InductionProfile factoryReference;
        public InductionProfile(VehicleSpec vehicle, Tune source, bool factoryReference = false)
        {
            this.vehicle = vehicle; tune = source.Copy(); data = (vehicle.induction ?? new InductionSpec()).Copy();
            type = factoryReference || !tune.partsResolved && tune.engineLevel == 0 || tune.inductionUpgrade == EngineUpgradeInduction.Factory ? data.factory :
                tune.inductionUpgrade == EngineUpgradeInduction.Turbo ? InductionType.Turbo : tune.inductionUpgrade == EngineUpgradeInduction.Supercharger ? InductionType.Supercharger :
                tune.inductionUpgrade == EngineUpgradeInduction.TwinTurbo ? InductionType.TwinTurbo : tune.inductionUpgrade == EngineUpgradeInduction.TwinSupercharger ? InductionType.TwinSupercharger : InductionType.Twincharged;
            valve = tune.blowOffValve == BlowOffType.Factory ? data.factoryValve : tune.blowOffValve;
            turboCount = type == InductionType.TwinTurbo ? 2 : type == InductionType.Turbo || type == InductionType.Twincharged ? 1 : 0;
            superchargerCount = type == InductionType.TwinSupercharger ? 2 : type == InductionType.Supercharger || type == InductionType.Twincharged ? 1 : 0;
            size = factoryReference ? 1 : tune.TurboSize; pulley = factoryReference ? 1 : tune.SuperchargerPulley;
            boostSetting = factoryReference ? 1 : tune.BoostSetting * (tune.partsResolved ? tune.partBoost : 1 + Math.Max(0, tune.engineLevel) * .06);
            litres = vehicle.DisplacementLitres;
            if (!factoryReference && data.torqueCurveIncludesFactoryInduction && data.factory != InductionType.NaturallyAspirated)
                this.factoryReference = new InductionProfile(vehicle, new Tune(), true);
        }

        // Capture-only constructor: preserves the already-resolved profile values instead
        // of recomputing them from a synthetic tune or vehicle definition.
        internal InductionProfile(VehicleSpec vehicle, Tune tune, InductionSpec data,
            InductionType type, BlowOffType valve, int turboCount, int superchargerCount,
            double size, double boostSetting, double pulley, double litres,
            InductionProfile factoryReference)
        {
            this.vehicle = vehicle; this.tune = tune; this.data = data; this.type = type;
            this.valve = valve; this.turboCount = turboCount; this.superchargerCount = superchargerCount;
            this.size = size; this.boostSetting = boostSetting; this.pulley = pulley;
            this.litres = litres; this.factoryReference = factoryReference;
        }
        public double TurboEnvelope(double rpm)
        {
            double shift = (size - 1) * 1800;
            double x = DragMath.Clamp((rpm - data.turboThresholdRpm - shift) / (data.turboFullBoostRpm - data.turboThresholdRpm), 0, 1);
            return turboCount == 0 ? 0 : Math.Min(data.maxBoostBar, data.turboBoostBar * boostSetting * x * x * (3 - 2 * x));
        }
        public double SuperchargerEnvelope(double rpm)
        {
            return superchargerCount == 0 ? 0 : Math.Min(data.maxBoostBar, data.superchargerBoostBar * boostSetting * pulley * pulley * DragMath.Clamp(rpm / data.superchargerFullBoostRpm, 0, 1));
        }
        public double BaseAirflow(double rpm)
        {
            return litres * .001 * Math.Max(400, rpm) / 120 * data.volumetricEfficiency * (data.ambientPressureBar * 100000 / (287.05 * data.ambientTemperatureK));
        }
    }

    public static class InductionDynamics
    {
        internal static SteadyOutputInputs CaptureSteadyOutputInputs(InductionProfile profile)
        {
            return SteadyOutputInputs.Capture(profile);
        }

        static double Bypass(InductionProfile profile, double rpm, double developedTurbo)
        {
            return profile.type == InductionType.Twincharged ? DragMath.Clamp((developedTurbo / Math.Max(.01, profile.SuperchargerEnvelope(rpm)) - .35) / .5, 0, 1) : 0;
        }
        static double Approach(double current, double target, double dt, double seconds) { return current + (target - current) * (1 - Math.Exp(-dt / seconds)); }
        public static void Step(InductionProfile profile, CarState state, double gas, double load, bool shifting, bool fuelCut, double dt)
        {
            gas = DragMath.Clamp(gas, 0, 1); load = DragMath.Clamp(load, 0, 1);
            var d = profile.data;
            double priorBoost = state.turboBoostBar;
            bool released = profile.vehicle.engineBehavior.fueling!=FuelSystem.Diesel && (state.inductionLastThrottle - gas > .25 || shifting && !state.inductionWasShifting) && priorBoost > .1 && profile.turboCount > 0;
            if (released)
            {
                state.boostReleaseCount++; state.boostReleaseStrength = DragMath.Clamp(priorBoost / Math.Max(.1, d.turboBoostBar * profile.boostSetting), 0, 1);
                state.audioExhaust?.RecordRelease(state.boostReleaseStrength,state.engineTime);
                // Recirculation retains more rotor energy; a pressure release never adds energy.
                state.turboSpool *= profile.valve == BlowOffType.Recirculated ? .92 : .8;
            }
            state.inductionLastThrottle = gas; state.inductionWasShifting = shifting;
            double combustion = shifting || fuelCut ? 0 : gas * state.combustionFraction;
            double envelope = profile.TurboEnvelope(state.rpm);
            double energy = Math.Max(combustion * state.cylinderCombustionFactor * (.06 + .94 * load), state.exhaustEnergy);
            double rotorTarget = profile.turboCount > 0 && envelope > 0 ? Math.Sqrt(energy) : 0;
            double spoolTime = d.turboSpoolSeconds * Math.Pow(profile.size, 1.7) * (profile.turboCount == 2 ? d.twinTurboSpoolFactor : 1);
            state.turboSpool = Approach(state.turboSpool, rotorTarget, dt, rotorTarget > state.turboSpool ? spoolTime : d.turboCoastSeconds);
            double turboTarget = envelope * state.turboSpool * state.turboSpool * Math.Max(combustion, state.exhaustEnergy);
            state.turboBoostBar = profile.turboCount == 0 ? 0 : Approach(state.turboBoostBar, turboTarget, dt,
                turboTarget > state.turboBoostBar ? d.manifoldFillSeconds : d.valveReleaseSeconds);
            // Twincharging is a series pressure arrangement with an automatic blower bypass as turbo pressure arrives.
            double bypass = Bypass(profile, state.rpm, envelope * state.turboSpool * state.turboSpool);
            state.superchargerBypass = bypass;
            double scTarget = profile.SuperchargerEnvelope(state.rpm) * combustion * (.08 + .92 * load) * (1 - bypass);
            state.superchargerBoostBar = profile.superchargerCount == 0 ? 0 : Approach(state.superchargerBoostBar, scTarget, dt, d.superchargerResponseSeconds);
            double allowedTurbo = Math.Max(0, (d.ambientPressureBar + d.maxBoostBar) / (1 + state.superchargerBoostBar / d.ambientPressureBar) - d.ambientPressureBar);
            state.turboBoostBar = Math.Min(state.turboBoostBar, allowedTurbo);
            UpdateOutput(profile, state, gas, fuelCut || shifting);
        }
        public static void UpdateOutput(InductionProfile profile, CarState state, double gas, bool cut)
        {
            double density = Charge(profile, state.rpm, state.turboBoostBar, state.superchargerBoostBar, out double boost, out double temperature, out double airflow, out double drive, state.optimalThermals);
            state.boostBar = boost; state.chargeTemperatureK = temperature; state.airflowKgS = airflow * gas;
            state.superchargerDriveTorqueNm = drive * gas;
            state.engineTorqueNm = cut || state.engineFailed || state.stalled ? 0 : Math.Max(0, BaseTorque(profile, state.rpm) * density * gas - drive * gas) * state.combustionFraction * state.mechanicalPowerFactor;
            state.engineTorqueNm=Math.Max(0,state.engineTorqueNm-state.extraEngineFrictionNm*gas);
            state.engineTorqueNm = Capacity(profile.vehicle, state.engineTorqueNm, state.rpm, state.deliveryPowerCapacityHp);
            state.engineHorsepower = state.engineTorqueNm * state.rpm / 7127;
        }
        public static double Torque(InductionProfile profile, CarState state, double rpm, double gas)
        {
            double density = Charge(profile, rpm, state.turboBoostBar, state.superchargerBoostBar, out _, out _, out _, out double drive, state.optimalThermals);
            return state.engineFailed || state.stalled ? 0 : Capacity(profile.vehicle, Math.Max(0, BaseTorque(profile, rpm) * density - drive) * gas * state.mechanicalPowerFactor*Mechanics.EngineElements.Ripple(state)-state.extraEngineFrictionNm*gas, rpm, state.deliveryPowerCapacityHp);
        }
        static double Capacity(VehicleSpec spec, double torque, double rpm, double conditionedCapacity = 0)
        {
            double cap = conditionedCapacity > 0 ? conditionedCapacity : double.MaxValue;
            if (spec.fuelPowerCapacityHp > 0) cap = Math.Min(cap, spec.fuelPowerCapacityHp);
            if (spec.ignitionPowerCapacityHp > 0) cap = Math.Min(cap, spec.ignitionPowerCapacityHp);
            return Math.Min(torque, cap * 7127 / Math.Max(400, rpm));
        }
        static double BaseTorque(InductionProfile profile, double rpm)
        {
            double torque = DragMath.AuthoredTorque(profile.vehicle, rpm)*ServiceDynamics.CamMultiplier(profile.vehicle,rpm)*CombustionHardware.CompressionMultiplier(profile.vehicle);
            if (profile.data.torqueCurveIncludesFactoryInduction && profile.data.factory != InductionType.NaturallyAspirated)
            {
                var stock = profile.factoryReference;
                // A reference profile is only evaluated as hardware, never recursively as an engine curve.
                if (stock == null) return torque * profile.tune.PowerMultiplier;
                double turbo = stock.TurboEnvelope(rpm), sc = stock.SuperchargerEnvelope(rpm);
                sc *= 1 - Bypass(stock, rpm, turbo);
                turbo = Math.Min(turbo, Math.Max(0, (stock.data.ambientPressureBar + stock.data.maxBoostBar) / (1 + sc / stock.data.ambientPressureBar) - stock.data.ambientPressureBar));
                double density = Charge(stock, rpm, turbo, sc, out _, out _, out _, out double drive);
                torque = (torque + drive) / Math.Max(.1, density);
            }
            return torque * profile.tune.PowerMultiplier;
        }
        public static double SteadyTorque(VehicleSpec spec, Tune tune, double rpm)
        {
            if ((spec.induction == null || spec.induction.factory == InductionType.NaturallyAspirated) &&
                (!tune.partsResolved && tune.engineLevel == 0 || tune.inductionUpgrade == EngineUpgradeInduction.Factory)) return Capacity(spec, DragMath.AuthoredTorque(spec, rpm) * tune.PowerMultiplier*ServiceDynamics.CamMultiplier(spec,rpm)*CombustionHardware.CompressionMultiplier(spec), rpm);
            var profile = new InductionProfile(spec, tune);
            return SteadyOutput(profile, rpm).engineTorqueNm;
        }
        public static CarState SteadyOutput(InductionProfile profile, double rpm)
        {
            double sc = profile.SuperchargerEnvelope(rpm);
            double turbo = profile.TurboEnvelope(rpm);
            sc *= 1 - Bypass(profile, rpm, turbo);
            turbo = Math.Min(turbo, Math.Max(0, (profile.data.ambientPressureBar + profile.data.maxBoostBar) / (1 + sc / profile.data.ambientPressureBar) - profile.data.ambientPressureBar));
            var state = new CarState { rpm = rpm, turboBoostBar = turbo, superchargerBoostBar = sc };
            UpdateOutput(profile, state, 1, false); return state;
        }
        public static double UnboostedTorque(VehicleSpec spec, Tune tune, double rpm) { return BaseTorque(new InductionProfile(spec, tune), rpm); }
        static double Charge(InductionProfile profile, double rpm, double turbo, double sc, out double boost, out double temperature, out double airflow, out double drive, bool optimalThermals = false)
        {
            var d = profile.data; double ambient = d.ambientPressureBar;
            double turboPr = 1 + Math.Max(0, turbo) / ambient, scPr = 1 + Math.Max(0, sc) / ambient;
            double ratio = Math.Min(1 + d.maxBoostBar / ambient, turboPr * scPr);
            double scHeat = d.ambientTemperatureK * (Math.Pow(scPr, .286) - 1) / d.superchargerEfficiency;
            double turboHeat = (d.ambientTemperatureK + scHeat) * (Math.Pow(turboPr, .286) - 1) / d.turboEfficiency;
            temperature = d.ambientTemperatureK + (optimalThermals ? 0 : (scHeat + turboHeat) * (1 - d.intercoolerEffectiveness));
            double density = ratio * d.ambientTemperatureK / temperature;
            double baseFlow = profile.BaseAirflow(rpm); airflow = baseFlow * density;
            double flowCap = double.MaxValue;
            if (profile.vehicle.engineBehavior.fueling!=FuelSystem.Diesel && profile.turboCount > 0 && turbo > .001) flowCap = d.turboUnitFlowKgS * profile.size * profile.turboCount;
            if (profile.superchargerCount > 0 && sc > .001) flowCap = Math.Min(flowCap, d.superchargerUnitFlowKgS * profile.pulley * profile.superchargerCount);
            // An undersized compressor cannot increase charge mass beyond its capacity; it never suppresses the base engine below NA here.
            density = Math.Min(density, Math.Max(1, flowCap / Math.Max(.0001, baseFlow))); airflow = baseFlow * density;
            boost = Math.Max(0, (density * temperature / d.ambientTemperatureK - 1) * ambient);
            drive = sc > .001 ? airflow * 1005 * scHeat / .95 / (Math.Max(400, rpm) * Math.PI / 30) + d.superchargerFrictionNm * profile.superchargerCount * profile.pulley : 0;
            density *= 1 - d.turboBackpressureLoss * Math.Max(0, turbo) / ambient;
            return Math.Max(.1, density);
        }
    }
}
