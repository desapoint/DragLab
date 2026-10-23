using System;
using System.Collections.Generic;
using System.IO;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    /// <summary>An immutable v1 fluid row; each double is stored as exact IEEE-754 bits.</summary>
    internal sealed class TypedFluidStateRecord
    {
        internal const int CurrentValueVersion = 1;
        internal const string Table = "fluid_states";
        internal readonly string OwnerKind;
        internal readonly string OwnerId;
        internal readonly int ValueVersion;
        internal readonly long OilLitresBits;
        internal readonly long CoolantLitresBits;
        internal readonly long OilTemperatureCBits;
        internal readonly long OilPressureBarBits;
        internal readonly long CoolantPressureBarBits;
        internal readonly long OilConditionBits;
        internal readonly long OilConsumedLitresBits;
        internal readonly long OilLeakedLitresBits;
        internal readonly long CoolantLostLitresBits;
        internal readonly long ServiceElapsedSecondsBits;
        internal readonly long OilBurnLpsBits;
        internal readonly long OilLeakLpsBits;
        internal readonly long CoolantLeakLpsBits;
        internal readonly long CoolantVentLpsBits;

        TypedFluidStateRecord(string ownerKind, string ownerId, long[] bits)
        {
            if (ownerKind != "car" && ownerKind != "engine") throw new ArgumentException("Fluid owner kind must be 'car' or 'engine'.", nameof(ownerKind));
            TypedStateEncoding.ValidateName(ownerId, nameof(ownerId));
            if (bits == null) throw new ArgumentNullException(nameof(bits));
            if (bits.Length != 14) throw new ArgumentException("A fluid row must contain exactly fourteen values.", nameof(bits));
            for (int i = 0; i < bits.Length; i++)
            {
                double value = BitConverter.Int64BitsToDouble(bits[i]);
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentException("Fluid state must contain finite values.", nameof(bits));
            }
            OwnerKind = ownerKind;
            OwnerId = ownerId;
            ValueVersion = CurrentValueVersion;
            OilLitresBits = bits[0];
            CoolantLitresBits = bits[1];
            OilTemperatureCBits = bits[2];
            OilPressureBarBits = bits[3];
            CoolantPressureBarBits = bits[4];
            OilConditionBits = bits[5];
            OilConsumedLitresBits = bits[6];
            OilLeakedLitresBits = bits[7];
            CoolantLostLitresBits = bits[8];
            ServiceElapsedSecondsBits = bits[9];
            OilBurnLpsBits = bits[10];
            OilLeakLpsBits = bits[11];
            CoolantLeakLpsBits = bits[12];
            CoolantVentLpsBits = bits[13];
        }

        internal static TypedFluidStateRecord FromFluidState(string ownerKind, string ownerId, FluidState source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var values = new[]
            {
                source.oilLitres, source.coolantLitres, source.oilTemperatureC, source.oilPressureBar,
                source.coolantPressureBar, source.oilCondition, source.oilConsumedLitres, source.oilLeakedLitres,
                source.coolantLostLitres, source.serviceElapsedSeconds, source.oilBurnLps, source.oilLeakLps,
                source.coolantLeakLps, source.coolantVentLps
            };
            var bits = new long[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (double.IsNaN(values[i]) || double.IsInfinity(values[i]))
                    throw new ArgumentException("Fluid state must contain finite values.", nameof(source));
                bits[i] = BitConverter.DoubleToInt64Bits(values[i]);
            }
            return new TypedFluidStateRecord(ownerKind, ownerId, bits);
        }

        internal static TypedFluidStateRecord FromStateRecord(TypedStateRecord row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (row.TableName != Table) throw new ArgumentException("Typed row is not a fluid_states record.", nameof(row));
            string ownerKind = row.ReadText("owner_kind");
            string ownerId = row.ReadText("owner_id");
            if (row.ReadInt64("value_version") != CurrentValueVersion)
                throw new NotSupportedException("Unsupported typed fluid row version.");
            string expectedKey = TypedStateEncoding.CompositeKey(
                TypedStateField.Text("owner_kind", ownerKind),
                TypedStateField.Text("owner_id", ownerId));
            if (!string.Equals(row.RecordKey, expectedKey, StringComparison.Ordinal))
                throw new InvalidDataException("Typed fluid row key does not match its owner fields.");
            var fields = row.Fields();
            string[] expectedNames =
            {
                "owner_kind", "owner_id", "value_version", "oil_litres_bits", "coolant_litres_bits",
                "oil_temperature_c_bits", "oil_pressure_bar_bits", "coolant_pressure_bar_bits",
                "oil_condition_bits", "oil_consumed_litres_bits", "oil_leaked_litres_bits",
                "coolant_lost_litres_bits", "service_elapsed_seconds_bits", "oil_burn_lps_bits",
                "oil_leak_lps_bits", "coolant_leak_lps_bits", "coolant_vent_lps_bits"
            };
            if (fields.Length != expectedNames.Length)
                throw new InvalidDataException("Typed fluid row has an unexpected field count.");
            for (int i = 0; i < expectedNames.Length; i++)
                if (!string.Equals(fields[i].Name, expectedNames[i], StringComparison.Ordinal))
                    throw new InvalidDataException("Typed fluid row field order does not match the v1 schema.");
            return new TypedFluidStateRecord(ownerKind, ownerId, new[]
            {
                row.ReadInt64("oil_litres_bits"), row.ReadInt64("coolant_litres_bits"),
                row.ReadInt64("oil_temperature_c_bits"), row.ReadInt64("oil_pressure_bar_bits"),
                row.ReadInt64("coolant_pressure_bar_bits"), row.ReadInt64("oil_condition_bits"),
                row.ReadInt64("oil_consumed_litres_bits"), row.ReadInt64("oil_leaked_litres_bits"),
                row.ReadInt64("coolant_lost_litres_bits"), row.ReadInt64("service_elapsed_seconds_bits"),
                row.ReadInt64("oil_burn_lps_bits"), row.ReadInt64("oil_leak_lps_bits"),
                row.ReadInt64("coolant_leak_lps_bits"), row.ReadInt64("coolant_vent_lps_bits")
            });
        }

        internal TypedStateRecord AsStateRecord()
        {
            var key = TypedStateEncoding.CompositeKey(
                TypedStateField.Text("owner_kind", OwnerKind),
                TypedStateField.Text("owner_id", OwnerId));
            return new TypedStateRecord(Table, key, new[]
            {
                TypedStateField.Text("owner_kind", OwnerKind),
                TypedStateField.Text("owner_id", OwnerId),
                TypedStateField.Int64("value_version", ValueVersion),
                TypedStateField.Int64("oil_litres_bits", OilLitresBits),
                TypedStateField.Int64("coolant_litres_bits", CoolantLitresBits),
                TypedStateField.Int64("oil_temperature_c_bits", OilTemperatureCBits),
                TypedStateField.Int64("oil_pressure_bar_bits", OilPressureBarBits),
                TypedStateField.Int64("coolant_pressure_bar_bits", CoolantPressureBarBits),
                TypedStateField.Int64("oil_condition_bits", OilConditionBits),
                TypedStateField.Int64("oil_consumed_litres_bits", OilConsumedLitresBits),
                TypedStateField.Int64("oil_leaked_litres_bits", OilLeakedLitresBits),
                TypedStateField.Int64("coolant_lost_litres_bits", CoolantLostLitresBits),
                TypedStateField.Int64("service_elapsed_seconds_bits", ServiceElapsedSecondsBits),
                TypedStateField.Int64("oil_burn_lps_bits", OilBurnLpsBits),
                TypedStateField.Int64("oil_leak_lps_bits", OilLeakLpsBits),
                TypedStateField.Int64("coolant_leak_lps_bits", CoolantLeakLpsBits),
                TypedStateField.Int64("coolant_vent_lps_bits", CoolantVentLpsBits)
            });
        }

        internal FluidState Restore()
        {
            return new FluidState
            {
                oilLitres = BitConverter.Int64BitsToDouble(OilLitresBits),
                coolantLitres = BitConverter.Int64BitsToDouble(CoolantLitresBits),
                oilTemperatureC = BitConverter.Int64BitsToDouble(OilTemperatureCBits),
                oilPressureBar = BitConverter.Int64BitsToDouble(OilPressureBarBits),
                coolantPressureBar = BitConverter.Int64BitsToDouble(CoolantPressureBarBits),
                oilCondition = BitConverter.Int64BitsToDouble(OilConditionBits),
                oilConsumedLitres = BitConverter.Int64BitsToDouble(OilConsumedLitresBits),
                oilLeakedLitres = BitConverter.Int64BitsToDouble(OilLeakedLitresBits),
                coolantLostLitres = BitConverter.Int64BitsToDouble(CoolantLostLitresBits),
                serviceElapsedSeconds = BitConverter.Int64BitsToDouble(ServiceElapsedSecondsBits),
                oilBurnLps = BitConverter.Int64BitsToDouble(OilBurnLpsBits),
                oilLeakLps = BitConverter.Int64BitsToDouble(OilLeakLpsBits),
                coolantLeakLps = BitConverter.Int64BitsToDouble(CoolantLeakLpsBits),
                coolantVentLps = BitConverter.Int64BitsToDouble(CoolantVentLpsBits)
            };
        }
    }
}
