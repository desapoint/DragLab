using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    // Mirrors Unity's observed malformed-UTF16 handling without allocating an
    // encoded string. A high surrogate consumes the following code unit; a low
    // surrogate not paired by that rule becomes U+FFFD.
    internal static class ProductStringSemantics
    {
        internal static bool EffectiveEquals(string left, string right)
        {
            left = left ?? string.Empty;
            right = right ?? string.Empty;
            var a = new EffectiveReader(left);
            var b = new EffectiveReader(right);
            while (true)
            {
                bool hasA = a.TryRead(out char charA);
                bool hasB = b.TryRead(out char charB);
                if (hasA != hasB) return false;
                if (!hasA) return true;
                if (charA != charB) return false;
            }
        }

        internal static int AddEffectiveHash(int hash, string value)
        {
            value = value ?? string.Empty;
            unchecked
            {
                var reader = new EffectiveReader(value);
                int count = 0;
                while (reader.TryRead(out char current))
                {
                    hash = hash * 31 + current;
                    count++;
                }
                hash = hash * 31 + count;
                return hash;
            }
        }

        struct EffectiveReader
        {
            readonly string value;
            int index;
            char pending;
            bool hasPending;

            internal EffectiveReader(string source)
            {
                value = source;
                index = 0;
                pending = default(char);
                hasPending = false;
            }

            internal bool TryRead(out char result)
            {
                if (hasPending)
                {
                    result = pending;
                    pending = default(char);
                    hasPending = false;
                    return true;
                }
                if (index >= value.Length)
                {
                    result = default(char);
                    return false;
                }

                char current = value[index++];
                if (char.IsHighSurrogate(current))
                {
                    if (index >= value.Length)
                    {
                        result = '\uFFFD';
                        return true;
                    }

                    char following = value[index++];
                    if (char.IsLowSurrogate(following))
                    {
                        result = current;
                        pending = following;
                        hasPending = true;
                        return true;
                    }

                    // The serializer consumes the next code unit even when it
                    // is not a low surrogate, emitting one replacement unit.
                    result = '\uFFFD';
                    return true;
                }

                result = char.IsLowSurrogate(current) ? '\uFFFD' : current;
                return true;
            }
        }
    }

    // Immutable owned value capture for the serialized ProductKey graph. This is
    // deliberately independent of MarketCatalogLayout.Key and all JSON caches.
    internal sealed class PartProductValue
    {
        readonly string sourceId;
        readonly string[] strings;
        readonly int[] scalars;
        readonly bool[] scalarIsFloat;
        readonly bool hadAwdHardware;
        readonly bool hadCalibration;
        readonly bool supported;

        // Explicit capture schema. `id` is retained for RawEquals, but excluded
        // from ProductEquivalent because the legacy key blanks only that field.
        static readonly string[] fieldPaths =
        {
            "id", "name", "fitment", "brand", "quality", "fuelKind", "pistonCrown",
            "crownVolumeCc", "valveReliefMm", "springMaxLiftMm", "followerInterface", "plugHeatRange",
            "category", "boreMm", "power", "grip", "massScale", "shiftScale", "capacityScale", "boostScale",
            "centerRatioMin", "centerRatioMax", "awdHardware", "awdHardware.version", "awdHardware.modes",
            "awdHardware.integratedCenter", "awdHardware.adjustments", "awdHardware.calibration",
            "awdHardware.calibration.mode",
            "awdHardware.calibration.centerSpeedRatio", "awdHardware.calibration.torqueBiasRatio",
            "awdHardware.calibration.centerPreloadNm", "awdHardware.calibration.frontBias",
            "awdHardware.calibration.minimumFront", "awdHardware.calibration.maximumFront",
            "awdHardware.calibration.primaryFront", "awdHardware.calibration.isolateFailedAxle",
            "awdHardware.calibration.maximumSecondary", "awdHardware.calibration.responseSeconds",
            "awdHardware.calibration.slipThreshold", "awdHardware.calibration.preload",
            "awdHardware.calibration.freeShaftInertiaKgM2", "awdHardware.calibration.freeShaftDragNm",
            "awdHardware.calibration.freeShaftViscousDrag", "awdHardware.responseMin", "awdHardware.responseMax",
            "rpmLimit", "torqueLimitNm", "massKg", "inertiaKgM2", "positionFromFrontAxleM", "heightM",
            "price", "stage", "reputationRequired", "clutchEnvironment", "clutchMaterial", "clutchActuation",
            "clutchPlateCount", "clutchOilCapacityLitres", "clutchEngagementRpm", "tuning", "turboSize",
            "fixedBoost", "boostMin", "boostMax", "fixedPulley", "pulleyMin", "pulleyMax",
            "fixedFinalDrive", "finalDriveMin", "finalDriveMax", "valve", "turboUnits", "superchargerUnits",
            "fuelDelivery", "supportsInjection", "supportsDiesel", "injectionControlMetadataVersion",
            "controllerCylinderCapacity", "injectionIdleControl", "injectionMixtureVariation", "camLiftMm",
            "camDurationDegrees", "camOverlapDegrees", "camLobeSeparation", "camTorqueBias", "valvetrainRpm",
            "camTimingAdjustable", "fluidCapacityLitres", "fluidMinimumLitres", "baffling", "pumpFlowScale",
            "capPressureBar", "induction", "transmission", "manualDownshiftInterlock", "manualLayout",
            "sequentialShiftScale", "thermalLimitC", "heatCapacityJPerC", "coolingWattsPerC", "wearEnergyJ",
            "overloadSeconds", "tireLifeDistanceM", "tireOptimalC", "tireHeatFraction", "description",
            "differential", "torqueBiasRatio", "preloadNm", "engineHeatTransferScale", "maxPowerHp", "provenance"
        };

        internal static string[] FieldPaths => (string[])fieldPaths.Clone();
        internal string SourceId => sourceId;
        internal bool AwdHardwareWasNull => !hadAwdHardware;
        internal bool CalibrationWasNull => hadAwdHardware && !hadCalibration;

        PartProductValue(string id, string[] copiedStrings, int[] copiedScalars, bool[] copiedFloatFlags,
            bool hasAwdHardware, bool hasCalibration, bool isSupported)
        {
            sourceId = id;
            strings = copiedStrings;
            scalars = copiedScalars;
            scalarIsFloat = copiedFloatFlags;
            hadAwdHardware = hasAwdHardware;
            hadCalibration = hasCalibration;
            supported = isSupported;
        }

        internal static PartProductValue Capture(PartDefinition source)
        {
            // Match the legacy null boundary through the same natural dereference
            // class of error (rather than manufacturing a NullReferenceException).
            var sourceId = source.id;

            var copiedStrings = new[]
            {
                source.name, source.fitment, source.brand, source.quality,
                source.followerInterface, source.description, source.provenance
            };
            var values = new ScalarBuilder();

            values.AddInt((int)source.fuelKind);
            values.AddInt((int)source.pistonCrown);
            values.AddFloat(source.crownVolumeCc);
            values.AddFloat(source.valveReliefMm);
            values.AddFloat(source.springMaxLiftMm);
            values.AddInt(source.plugHeatRange);
            values.AddInt((int)source.category);
            values.AddFloat(source.boreMm);
            values.AddFloat(source.power);
            values.AddFloat(source.grip);
            values.AddFloat(source.massScale);
            values.AddFloat(source.shiftScale);
            values.AddFloat(source.capacityScale);
            values.AddFloat(source.boostScale);
            values.AddFloat(source.centerRatioMin);
            values.AddFloat(source.centerRatioMax);

            var hardware = source.awdHardware;
            var hasHardware = hardware != null;
            var calibration = hardware == null ? null : hardware.calibration;
            var hasCalibration = calibration != null;
            AddHardware(values, hardware, calibration);

            values.AddFloat(source.rpmLimit);
            values.AddFloat(source.torqueLimitNm);
            values.AddFloat(source.massKg);
            values.AddFloat(source.inertiaKgM2);
            values.AddFloat(source.positionFromFrontAxleM);
            values.AddFloat(source.heightM);
            values.AddInt(source.price);
            values.AddInt(source.stage);
            values.AddInt(source.reputationRequired);
            values.AddInt((int)source.clutchEnvironment);
            values.AddInt((int)source.clutchMaterial);
            values.AddInt((int)source.clutchActuation);
            values.AddInt(source.clutchPlateCount);
            values.AddFloat(source.clutchOilCapacityLitres);
            values.AddFloat(source.clutchEngagementRpm);
            values.AddInt((int)source.tuning);
            values.AddFloat(source.turboSize);
            values.AddFloat(source.fixedBoost);
            values.AddFloat(source.boostMin);
            values.AddFloat(source.boostMax);
            values.AddFloat(source.fixedPulley);
            values.AddFloat(source.pulleyMin);
            values.AddFloat(source.pulleyMax);
            values.AddFloat(source.fixedFinalDrive);
            values.AddFloat(source.finalDriveMin);
            values.AddFloat(source.finalDriveMax);
            values.AddInt((int)source.valve);
            values.AddInt(source.turboUnits);
            values.AddInt(source.superchargerUnits);
            values.AddInt((int)source.fuelDelivery);
            values.AddBool(source.supportsInjection);
            values.AddBool(source.supportsDiesel);
            values.AddInt(source.injectionControlMetadataVersion);
            values.AddInt(source.controllerCylinderCapacity);
            values.AddFloat(source.injectionIdleControl);
            values.AddFloat(source.injectionMixtureVariation);
            values.AddFloat(source.camLiftMm);
            values.AddFloat(source.camDurationDegrees);
            values.AddFloat(source.camOverlapDegrees);
            values.AddFloat(source.camLobeSeparation);
            values.AddFloat(source.camTorqueBias);
            values.AddFloat(source.valvetrainRpm);
            values.AddBool(source.camTimingAdjustable);
            values.AddFloat(source.fluidCapacityLitres);
            values.AddFloat(source.fluidMinimumLitres);
            values.AddFloat(source.baffling);
            values.AddFloat(source.pumpFlowScale);
            values.AddFloat(source.capPressureBar);
            values.AddInt((int)source.induction);
            values.AddInt((int)source.transmission);
            values.AddBool(source.manualDownshiftInterlock);
            values.AddInt((int)source.manualLayout);
            values.AddFloat(source.sequentialShiftScale);
            values.AddFloat(source.thermalLimitC);
            values.AddFloat(source.heatCapacityJPerC);
            values.AddFloat(source.coolingWattsPerC);
            values.AddFloat(source.wearEnergyJ);
            values.AddFloat(source.overloadSeconds);
            values.AddFloat(source.tireLifeDistanceM);
            values.AddFloat(source.tireOptimalC);
            values.AddFloat(source.tireHeatFraction);
            values.AddInt((int)source.differential);
            values.AddFloat(source.torqueBiasRatio);
            values.AddFloat(source.preloadNm);
            values.AddFloat(source.engineHeatTransferScale);
            values.AddFloat(source.maxPowerHp);

            return new PartProductValue(sourceId, copiedStrings, values.Values(), values.FloatFlags(),
                hasHardware, hasCalibration, true);
        }

        static void AddHardware(ScalarBuilder values, AwdHardwareSpec hardware, AwdSpec calibration)
        {
            // Missing reference values retain raw null-shape flags separately, while
            // their scalar slots carry the Unity-effective default object values.
            values.AddInt(hardware == null ? 1 : hardware.version);
            values.AddInt(hardware == null ? 0 : hardware.modes);
            values.AddBool(hardware != null && hardware.integratedCenter);
            values.AddInt((int)(hardware == null ? AwdAdjustments.None : hardware.adjustments));
            values.AddInt((int)(calibration == null ? AwdMode.FixedSplit : calibration.mode));
            values.AddFloat(calibration == null ? 0 : calibration.centerSpeedRatio);
            values.AddFloat(calibration == null ? 3 : calibration.torqueBiasRatio);
            values.AddFloat(calibration == null ? 35 : calibration.centerPreloadNm);
            values.AddFloat(calibration == null ? .5f : calibration.frontBias);
            values.AddFloat(calibration == null ? .15f : calibration.minimumFront);
            values.AddFloat(calibration == null ? .85f : calibration.maximumFront);
            values.AddBool(calibration == null || calibration.primaryFront);
            values.AddBool(calibration != null && calibration.isolateFailedAxle);
            values.AddFloat(calibration == null ? .5f : calibration.maximumSecondary);
            values.AddFloat(calibration == null ? .2f : calibration.responseSeconds);
            values.AddFloat(calibration == null ? .05f : calibration.slipThreshold);
            values.AddFloat(calibration == null ? 0 : calibration.preload);
            values.AddFloat(calibration == null ? .03f : calibration.freeShaftInertiaKgM2);
            values.AddFloat(calibration == null ? .25f : calibration.freeShaftDragNm);
            values.AddFloat(calibration == null ? .001f : calibration.freeShaftViscousDrag);
            values.AddFloat(hardware == null ? .05f : hardware.responseMin);
            values.AddFloat(hardware == null ? 1 : hardware.responseMax);
        }

        internal bool RawEquals(PartProductValue other)
        {
            if (ReferenceEquals(other, null) || !string.Equals(sourceId, other.sourceId, StringComparison.Ordinal) ||
                hadAwdHardware != other.hadAwdHardware || hadCalibration != other.hadCalibration ||
                !SameStrings(strings, other.strings) || scalars.Length != other.scalars.Length ||
                scalarIsFloat.Length != other.scalarIsFloat.Length) return false;
            for (int i = 0; i < scalars.Length; i++)
                if (scalars[i] != other.scalars[i] || scalarIsFloat[i] != other.scalarIsFloat[i]) return false;
            return true;
        }

        internal bool TryProductEquivalent(PartProductValue other, out bool equivalent)
        {
            if (ReferenceEquals(other, null)) { equivalent = false; return true; }
            equivalent = false;
            if (!supported || !other.supported || strings.Length != other.strings.Length ||
                scalars.Length != other.scalars.Length || scalarIsFloat.Length != other.scalarIsFloat.Length)
                return false;

            // Every product string is serialized as a value string; L0 explicitly
            // pins description null/empty, and the mandatory field probes pin each
            // remaining serialized string before this implementation is released.
            for (int i = 0; i < strings.Length; i++)
                if (!ProductStringSemantics.EffectiveEquals(strings[i], other.strings[i]))
                    return true;

            for (int i = 0; i < scalars.Length; i++)
            {
                int left = scalarIsFloat[i] ? CanonicalFloatBits(scalars[i]) : scalars[i];
                int right = other.scalarIsFloat[i] ? CanonicalFloatBits(other.scalars[i]) : other.scalars[i];
                if (scalarIsFloat[i] != other.scalarIsFloat[i] || left != right) return true;
            }

            // The tested null/default AWD hardware pair is equivalent. The scalar
            // slots above contain default-object values for either source shape.
            equivalent = true;
            return true;
        }

        internal bool TryGetProductHashCode(out int hash)
        {
            hash = 0;
            if (!supported || strings == null || scalars == null || scalarIsFloat == null ||
                scalars.Length != scalarIsFloat.Length) return false;

            unchecked
            {
                int value = 17;
                for (int i = 0; i < strings.Length; i++) value = ProductStringSemantics.AddEffectiveHash(value, strings[i]);
                for (int i = 0; i < scalars.Length; i++)
                    value = AddInt(value, scalarIsFloat[i] ? CanonicalFloatBits(scalars[i]) : scalars[i]);
                hash = value;
                return true;
            }
        }

        static bool SameStrings(string[] a, string[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            return true;
        }

        static int AddInt(int hash, int value) { unchecked { return hash * 31 + value; } }

        static int CanonicalFloatBits(int bits)
        {
            var value = new FloatBits { bits = bits }.value;
            if (value == 0f) return 0; // L0: negative and positive zero share a product key.
            if (float.IsNaN(value)) return unchecked((int)0x7fc00000); // serializer's NaN class, independent of payload.
            return bits;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct FloatBits
        {
            [FieldOffset(0)] internal float value;
            [FieldOffset(0)] internal int bits;
        }

        sealed class ScalarBuilder
        {
            readonly List<int> values = new List<int>(96);
            readonly List<bool> floatFlags = new List<bool>(96);
            internal void AddInt(int value) { values.Add(value); floatFlags.Add(false); }
            internal void AddBool(bool value) => AddInt(value ? 1 : 0);
            internal void AddFloat(float value)
            {
                var bits = new FloatBits { value = value };
                values.Add(bits.bits); floatFlags.Add(true);
            }
            internal int[] Values() => values.ToArray();
            internal bool[] FloatFlags() => floatFlags.ToArray();
        }
    }
}
