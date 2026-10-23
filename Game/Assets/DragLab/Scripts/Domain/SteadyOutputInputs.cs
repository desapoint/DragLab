using System;
using System.Runtime.InteropServices;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    // Immutable exact dependency key and private evaluator input for steady induction
    // output. The only mutable graph retained here is a private, narrow capture that
    // callers cannot access; every Evaluate call returns a fresh CarState.
    internal sealed class SteadyOutputInputs : IEquatable<SteadyOutputInputs>
    {
        readonly InductionProfile snapshot;
        readonly int hash;

        SteadyOutputInputs(InductionProfile snapshot) { this.snapshot = snapshot; hash = ProfileHash(snapshot, true); }

        internal static SteadyOutputInputs Capture(InductionProfile source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new SteadyOutputInputs(CopyProfile(source, true));
        }

        internal CarState Evaluate(double rpm) => InductionDynamics.SteadyOutput(snapshot, rpm);

        public bool Equals(SteadyOutputInputs other)
        {
            return !ReferenceEquals(other, null) && ProfilesEqual(snapshot, other.snapshot, true);
        }

        public override bool Equals(object obj) => Equals(obj as SteadyOutputInputs);
        public override int GetHashCode() => hash;

        static InductionProfile CopyProfile(InductionProfile source, bool includeFactoryReference)
        {
            var vehicle = CopyVehicleInputs(source.vehicle);
            var data = CopyInductionInputs(source.data);
            var tune = source.tune == null ? null : new Tune { partsResolved = true, partPower = source.tune.PowerMultiplier };
            // BaseTorque evaluates the factory reference only as an induction/charge
            // profile, so its own factoryReference is outside the evaluator's read set.
            var factory = includeFactoryReference && source.factoryReference != null
                ? CopyProfile(source.factoryReference, false) : null;
            return new InductionProfile(vehicle, tune, data, source.type, source.valve,
                source.turboCount, source.superchargerCount, source.size, source.boostSetting,
                source.pulley, source.litres, factory);
        }

        static VehicleSpec CopyVehicleInputs(VehicleSpec source)
        {
            if (source == null) return null;
            return new VehicleSpec
            {
                torque = source.torque == null ? null : (TorquePoint[])source.torque.Clone(),
                redlineRpm = source.redlineRpm,
                fuelPowerCapacityHp = source.fuelPowerCapacityHp,
                ignitionPowerCapacityHp = source.ignitionPowerCapacityHp,
                engineBehavior = source.engineBehavior == null ? null : new EngineBehaviorSpec { fueling = source.engineBehavior.fueling },
                valvetrain = source.valvetrain == null ? null : new ValvetrainSpec
                {
                    camTorqueBias = source.valvetrain.camTorqueBias,
                    camTimingDegrees = source.valvetrain.camTimingDegrees
                },
                combustion = source.combustion == null ? null : new CombustionSpec
                {
                    compressionRatio = source.combustion.compressionRatio,
                    referenceCompression = source.combustion.referenceCompression
                }
            };
        }

        static InductionSpec CopyInductionInputs(InductionSpec source)
        {
            if (source == null) return null;
            return new InductionSpec
            {
                factory = source.factory,
                torqueCurveIncludesFactoryInduction = source.torqueCurveIncludesFactoryInduction,
                ambientPressureBar = source.ambientPressureBar,
                ambientTemperatureK = source.ambientTemperatureK,
                turboBoostBar = source.turboBoostBar,
                turboThresholdRpm = source.turboThresholdRpm,
                turboFullBoostRpm = source.turboFullBoostRpm,
                turboUnitFlowKgS = source.turboUnitFlowKgS,
                turboEfficiency = source.turboEfficiency,
                turboBackpressureLoss = source.turboBackpressureLoss,
                superchargerBoostBar = source.superchargerBoostBar,
                superchargerFullBoostRpm = source.superchargerFullBoostRpm,
                superchargerUnitFlowKgS = source.superchargerUnitFlowKgS,
                superchargerEfficiency = source.superchargerEfficiency,
                superchargerFrictionNm = source.superchargerFrictionNm,
                intercoolerEffectiveness = source.intercoolerEffectiveness,
                volumetricEfficiency = source.volumetricEfficiency,
                maxBoostBar = source.maxBoostBar
            };
        }

        static bool ProfilesEqual(InductionProfile a, InductionProfile b, bool compareFactory)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.vehicle == null || b.vehicle == null || a.tune == null || b.tune == null || a.data == null || b.data == null) return false;
            if (a.type != b.type || a.turboCount != b.turboCount || a.superchargerCount != b.superchargerCount ||
                !Same(a.size, b.size) || !Same(a.boostSetting, b.boostSetting) || !Same(a.pulley, b.pulley) ||
                !Same(a.litres, b.litres) || !Same(a.tune.PowerMultiplier, b.tune.PowerMultiplier) ||
                !VehicleInputsEqual(a.vehicle, b.vehicle) || !InductionInputsEqual(a.data, b.data)) return false;
            return !compareFactory || ProfilesEqual(a.factoryReference, b.factoryReference, false);
        }

        static bool VehicleInputsEqual(VehicleSpec a, VehicleSpec b)
        {
            if (!Same(a.redlineRpm, b.redlineRpm) || !Same(a.fuelPowerCapacityHp, b.fuelPowerCapacityHp) ||
                !Same(a.ignitionPowerCapacityHp, b.ignitionPowerCapacityHp) ||
                !Same(a.engineBehavior == null, b.engineBehavior == null) ||
                !Same(a.valvetrain == null, b.valvetrain == null) || !Same(a.combustion == null, b.combustion == null)) return false;
            if (a.engineBehavior != null && a.engineBehavior.fueling != b.engineBehavior.fueling) return false;
            if (a.valvetrain != null && (!Same(a.valvetrain.camTorqueBias, b.valvetrain.camTorqueBias) || !Same(a.valvetrain.camTimingDegrees, b.valvetrain.camTimingDegrees))) return false;
            if (a.combustion != null && (!Same(a.combustion.compressionRatio, b.combustion.compressionRatio) || !Same(a.combustion.referenceCompression, b.combustion.referenceCompression))) return false;
            if (a.torque == null || b.torque == null) return a.torque == b.torque;
            if (a.torque.Length != b.torque.Length) return false;
            for (int i = 0; i < a.torque.Length; i++)
                if (!Same(a.torque[i].rpm, b.torque[i].rpm) || !Same(a.torque[i].torqueNm, b.torque[i].torqueNm)) return false;
            return true;
        }

        static bool InductionInputsEqual(InductionSpec a, InductionSpec b)
        {
            return a.factory == b.factory && a.torqueCurveIncludesFactoryInduction == b.torqueCurveIncludesFactoryInduction &&
                Same(a.ambientPressureBar, b.ambientPressureBar) && Same(a.ambientTemperatureK, b.ambientTemperatureK) &&
                Same(a.turboBoostBar, b.turboBoostBar) && Same(a.turboThresholdRpm, b.turboThresholdRpm) && Same(a.turboFullBoostRpm, b.turboFullBoostRpm) &&
                Same(a.turboUnitFlowKgS, b.turboUnitFlowKgS) && Same(a.turboEfficiency, b.turboEfficiency) && Same(a.turboBackpressureLoss, b.turboBackpressureLoss) &&
                Same(a.superchargerBoostBar, b.superchargerBoostBar) && Same(a.superchargerFullBoostRpm, b.superchargerFullBoostRpm) &&
                Same(a.superchargerUnitFlowKgS, b.superchargerUnitFlowKgS) && Same(a.superchargerEfficiency, b.superchargerEfficiency) &&
                Same(a.superchargerFrictionNm, b.superchargerFrictionNm) && Same(a.intercoolerEffectiveness, b.intercoolerEffectiveness) &&
                Same(a.volumetricEfficiency, b.volumetricEfficiency) && Same(a.maxBoostBar, b.maxBoostBar);
        }

        static int ProfileHash(InductionProfile profile, bool includeFactory)
        {
            if (profile == null) return 0;
            unchecked
            {
                int hash = 17;
                Add(ref hash, (int)profile.type); Add(ref hash, profile.turboCount); Add(ref hash, profile.superchargerCount);
                Add(ref hash, profile.size); Add(ref hash, profile.boostSetting); Add(ref hash, profile.pulley); Add(ref hash, profile.litres);
                Add(ref hash, profile.tune == null ? 0 : Bits(profile.tune.PowerMultiplier));
                AddVehicleHash(ref hash, profile.vehicle); AddInductionHash(ref hash, profile.data);
                if (includeFactory) Add(ref hash, ProfileHash(profile.factoryReference, false));
                return hash;
            }
        }

        static void AddVehicleHash(ref int hash, VehicleSpec v)
        {
            if (v == null) { Add(ref hash, 0); return; }
            Add(ref hash, v.redlineRpm); Add(ref hash, v.fuelPowerCapacityHp); Add(ref hash, v.ignitionPowerCapacityHp);
            Add(ref hash, v.engineBehavior == null ? -1 : (int)v.engineBehavior.fueling);
            Add(ref hash, v.valvetrain == null ? 0 : 1);
            if (v.valvetrain != null) { Add(ref hash, v.valvetrain.camTorqueBias); Add(ref hash, v.valvetrain.camTimingDegrees); }
            Add(ref hash, v.combustion == null ? 0 : 1);
            if (v.combustion != null) { Add(ref hash, v.combustion.compressionRatio); Add(ref hash, v.combustion.referenceCompression); }
            if (v.torque == null) { Add(ref hash, -1); return; }
            Add(ref hash, v.torque.Length);
            foreach (var point in v.torque) { Add(ref hash, point.rpm); Add(ref hash, point.torqueNm); }
        }

        static void AddInductionHash(ref int hash, InductionSpec d)
        {
            if (d == null) { Add(ref hash, 0); return; }
            Add(ref hash, (int)d.factory); Add(ref hash, d.torqueCurveIncludesFactoryInduction ? 1 : 0);
            Add(ref hash, d.ambientPressureBar); Add(ref hash, d.ambientTemperatureK); Add(ref hash, d.turboBoostBar);
            Add(ref hash, d.turboThresholdRpm); Add(ref hash, d.turboFullBoostRpm); Add(ref hash, d.turboUnitFlowKgS);
            Add(ref hash, d.turboEfficiency); Add(ref hash, d.turboBackpressureLoss); Add(ref hash, d.superchargerBoostBar);
            Add(ref hash, d.superchargerFullBoostRpm); Add(ref hash, d.superchargerUnitFlowKgS); Add(ref hash, d.superchargerEfficiency);
            Add(ref hash, d.superchargerFrictionNm); Add(ref hash, d.intercoolerEffectiveness); Add(ref hash, d.volumetricEfficiency); Add(ref hash, d.maxBoostBar);
        }

        static bool Same(float a, float b) => Bits(a) == Bits(b);
        static bool Same(double a, double b) => Bits(a) == Bits(b);
        static bool Same(bool a, bool b) => a == b;
        [StructLayout(LayoutKind.Explicit)] struct FloatBits
        {
            [FieldOffset(0)] public float value;
            [FieldOffset(0)] public int bits;
        }
        [StructLayout(LayoutKind.Explicit)] struct DoubleBits
        {
            [FieldOffset(0)] public double value;
            [FieldOffset(0)] public long bits;
        }
        static int Bits(float value) => new FloatBits { value = value }.bits;
        static long Bits(double value) => new DoubleBits { value = value }.bits;
        static void Add(ref int hash, int value) { unchecked { hash = hash * 31 + value; } }
        static void Add(ref int hash, float value) => Add(ref hash, Bits(value));
        static void Add(ref int hash, double value)
        {
            long bits = Bits(value); Add(ref hash, (int)bits); Add(ref hash, (int)(bits >> 32));
        }
    }
}
