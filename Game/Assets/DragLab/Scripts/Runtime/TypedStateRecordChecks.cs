#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Independent byte-vector, ownership, fluid-bit and bundled SQLite checks for the dormant S05a1 typed codec.</summary>
    public static class TypedStateRecordChecks
    {
        static readonly StringBuilder Report = new StringBuilder();
        static int passed;

        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("S05a1 typed record check failed: " + name);
            passed++;
            Report.AppendLine("PASS " + name);
        }

        static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T) { passed++; Report.AppendLine("PASS " + name); return; }
            throw new InvalidOperationException("S05a1 typed record check failed (expected " + typeof(T).Name + "): " + name);
        }

        static string Hex(byte[] bytes) => TypedStateEncoding.ToLowerHex(bytes);

        static TypedStateRecord GoldenRecord() => new TypedStateRecord("probe", "key", new[]
        {
            TypedStateField.Null("text"),
            TypedStateField.Text("empty", ""),
            TypedStateField.Int64("count", -1),
            TypedStateField.Text("unicode", "é·🚗"),
            TypedStateField.Int64("zero", long.MinValue)
        });

        static void CheckIndependentGoldenVector()
        {
            const string expectedBytes = "11000000647261676c61622d7265636f72642f76310500000070726f6265030000006b65790500000004000000746578740005000000656d707479010000000005000000636f756e7402ffffffffffffffff07000000756e69636f64650108000000c3a9c2b7f09f9a97040000007a65726f020000000000000080";
            const string expectedHash = "5b66c53abb3159ed49ed2295f6094fc9e47dfc86cbb09434efbf19ede7ff7f93";
            var record = GoldenRecord();
            Check(Hex(record.CanonicalBytes()) == expectedBytes, "manually pinned canonical row vector covers null, empty, Unicode, signed Int64 and exact field order");
            Check(record.HashHex() == expectedHash, "SHA-256 matches independent Python hashlib golden, not production-derived expected data");
            Check(Hex(record.HashBytes()) == expectedHash, "raw SHA-256 bytes match the independent hexadecimal golden");

            var nullRecord = new TypedStateRecord("probe", "key", new[] { TypedStateField.Null("value") });
            var emptyRecord = new TypedStateRecord("probe", "key", new[] { TypedStateField.Text("value", "") });
            Check(!nullRecord.CanonicalBytes().SequenceEqual(emptyRecord.CanonicalBytes()) && nullRecord.HashHex() != emptyRecord.HashHex(), "SQL NULL remains distinct from empty text in canonical bytes and hash");

            var reversed = new TypedStateRecord("probe", "key", new[]
            {
                TypedStateField.Text("unicode", "é·🚗"),
                TypedStateField.Int64("count", -1),
                TypedStateField.Text("empty", ""),
                TypedStateField.Null("text"),
                TypedStateField.Int64("zero", long.MinValue)
            });
            Check(record.HashHex() != reversed.HashHex(), "typed row hash binds field order");
        }

        static void CheckCultureAndCompositeKeys()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                var invariant = GoldenRecord();
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
                var localized = GoldenRecord();
                Check(invariant.HashHex() == localized.HashHex() && invariant.CanonicalBytes().SequenceEqual(localized.CanonicalBytes()), "canonical bytes are independent of current and UI culture");
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }

            string expectedComposite = "0a0000006f776e65725f6b696e640103000000636172080000006f776e65725f69640103000000610a62";
            string composite = TypedStateEncoding.CompositeKey(
                TypedStateField.Text("owner_kind", "car"),
                TypedStateField.Text("owner_id", "a\nb"));
            Check(composite == expectedComposite, "composite key matches independent field-by-field framing vector without row prefix or field count");

            string naiveA = string.Join(":", new[] { "car", "a:b" });
            string naiveB = string.Join(":", new[] { "car:a", "b" });
            string framedA = TypedStateEncoding.CompositeKey(TypedStateField.Text("kind", "car"), TypedStateField.Text("id", "a:b"));
            string framedB = TypedStateEncoding.CompositeKey(TypedStateField.Text("kind", "car:a"), TypedStateField.Text("id", "b"));
            Check(naiveA == naiveB && framedA != framedB, "length-framed composite key cannot alias values that collide under delimiter concatenation");
            Check(TypedStateEncoding.CompositeKey(TypedStateField.Text("a", "x"), TypedStateField.Text("b", "y")) !=
                  TypedStateEncoding.CompositeKey(TypedStateField.Text("b", "y"), TypedStateField.Text("a", "x")), "composite key binds ordered key-field names and values");
        }

        static void CheckImmutabilityAndInvalidInputs()
        {
            var sourceFields = new List<TypedStateField>
            {
                TypedStateField.Text("first", "before"), TypedStateField.Int64("number", 17)
            };
            var record = new TypedStateRecord("mutable-probe", "key", sourceFields);
            string originalHash = record.HashHex();
            byte[] canonical = record.CanonicalBytes();
            byte[] hashBytes = record.HashBytes();
            object[] sqlValues = record.SqlValues();
            TypedStateField[] fields = record.Fields();
            sourceFields[0] = TypedStateField.Text("first", "changed");
            sourceFields.Clear();
            canonical[0] ^= 0xff;
            hashBytes[0] ^= 0xff;
            sqlValues[0] = "changed";
            fields[0] = TypedStateField.Text("first", "changed");
            Check(record.HashHex() == originalHash, "source collection and every exposed array are defensive copies and cannot change a record hash");
            Check(record.ReadText("first") == "before" && (long)record.SqlValues()[1] == 17, "record retains captured typed values after caller mutation");

            var duplicate = new[] { TypedStateField.Text("dup", "a"), TypedStateField.Int64("dup", 1) };
            Throws<ArgumentException>(() => new TypedStateRecord("probe", "key", duplicate), "duplicate field names rejected ordinally");
            Throws<ArgumentException>(() => new TypedStateRecord("", "key", Array.Empty<TypedStateField>()), "empty table name rejected");
            var emptyKey = new TypedStateRecord("probe", "", Array.Empty<TypedStateField>());
            Check(emptyKey.RecordKey == "" && emptyKey.CanonicalBytes().Length > 0, "empty non-null opaque record key is preserved by the generic codec");
            Throws<ArgumentNullException>(() => new TypedStateRecord("probe", null, Array.Empty<TypedStateField>()), "null record key rejected");
            Throws<ArgumentException>(() => new TypedStateRecord("probe", "key", new[] { (TypedStateField)null }), "null field rejected");
            Throws<ArgumentException>(() => TypedStateField.Text("", "value"), "empty field name rejected");
            Throws<ArgumentException>(() => TypedStateField.Text("value", null), "null text requires the explicit Null field kind");
            Throws<ArgumentException>(() => TypedStateField.Text("bad\ud800", "value"), "unpaired UTF-16 surrogate in field name rejected by strict UTF-8");
            Throws<ArgumentException>(() => TypedStateField.Text("value", "bad\ud800"), "unpaired UTF-16 surrogate in text value rejected by strict UTF-8");
            Throws<ArgumentException>(() => TypedStateEncoding.CompositeKey(), "empty composite key rejected");
        }

        static double[] FluidValues(FluidState state) => new[]
        {
            state.oilLitres, state.coolantLitres, state.oilTemperatureC, state.oilPressureBar,
            state.coolantPressureBar, state.oilCondition, state.oilConsumedLitres, state.oilLeakedLitres,
            state.coolantLostLitres, state.serviceElapsedSeconds, state.oilBurnLps, state.oilLeakLps,
            state.coolantLeakLps, state.coolantVentLps
        };

        static void SetFluidValue(FluidState state, int index, double value)
        {
            switch (index)
            {
                case 0: state.oilLitres = value; break;
                case 1: state.coolantLitres = value; break;
                case 2: state.oilTemperatureC = value; break;
                case 3: state.oilPressureBar = value; break;
                case 4: state.coolantPressureBar = value; break;
                case 5: state.oilCondition = value; break;
                case 6: state.oilConsumedLitres = value; break;
                case 7: state.oilLeakedLitres = value; break;
                case 8: state.coolantLostLitres = value; break;
                case 9: state.serviceElapsedSeconds = value; break;
                case 10: state.oilBurnLps = value; break;
                case 11: state.oilLeakLps = value; break;
                case 12: state.coolantLeakLps = value; break;
                case 13: state.coolantVentLps = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        static long[] FluidBits(FluidState state) => FluidValues(state).Select(BitConverter.DoubleToInt64Bits).ToArray();

        static FluidState FluidFixture()
        {
            var state = new FluidState();
            double adjacent = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(1.0) + 1);
            double[] values = { 0.125, BitConverter.Int64BitsToDouble(long.MinValue), adjacent, 2.5, 0.75, 0.95, 1.125, 0.0625, 0.03125, 12.5, 0.000125, 0.00025, 0.0005, 0.00075 };
            for (int i = 0; i < values.Length; i++) SetFluidValue(state, i, values[i]);
            return state;
        }

        static void CheckFluidRecordRoundTrip()
        {
            var source = FluidFixture();
            long[] expected = FluidBits(source);
            Check(expected[1] == long.MinValue, "coolant fixture explicitly contains the negative-zero IEEE-754 bit pattern");
            var captured = TypedFluidStateRecord.FromFluidState("car", "car:codec:001", source);
            source.oilLitres = 29.0;
            Check(captured.Restore().oilLitres == BitConverter.Int64BitsToDouble(expected[0]), "fluid record owns a detached capture unaffected by later source mutation");
            var restored = captured.Restore();
            long[] actual = FluidBits(restored);
            Check(actual.SequenceEqual(expected), "all 14 fluid doubles round-trip by exact IEEE-754 bits, including adjacent value and negative zero");
            restored.coolantLitres = 8.0;
            Check(FluidBits(captured.Restore()).SequenceEqual(expected), "mutating restored FluidState does not change the immutable typed row");

            var stateRecord = captured.AsStateRecord();
            var reparsed = TypedFluidStateRecord.FromStateRecord(stateRecord);
            Check(reparsed.Restore() != null && FluidBits(reparsed.Restore()).SequenceEqual(expected), "fluid row reconstruction verifies composite owner key, schema version, field order and all bits");
            var engineRecord = TypedFluidStateRecord.FromFluidState("engine", "engine:codec:001", FluidFixture()).AsStateRecord();
            var engine = TypedFluidStateRecord.FromStateRecord(engineRecord);
            Check(engine.OwnerKind == "engine" && engine.OwnerId == "engine:codec:001" && FluidBits(engine.Restore()).SequenceEqual(expected),
                "engine-owner typed fluid row round-trips the same exact values under its distinct composite key");
            string[] expectedNames = {
                "owner_kind", "owner_id", "value_version", "oil_litres_bits", "coolant_litres_bits",
                "oil_temperature_c_bits", "oil_pressure_bar_bits", "coolant_pressure_bar_bits", "oil_condition_bits",
                "oil_consumed_litres_bits", "oil_leaked_litres_bits", "coolant_lost_litres_bits", "service_elapsed_seconds_bits",
                "oil_burn_lps_bits", "oil_leak_lps_bits", "coolant_leak_lps_bits", "coolant_vent_lps_bits"
            };
            Check(stateRecord.TableName == "fluid_states" && stateRecord.Fields().Select(field => field.Name).SequenceEqual(expectedNames),
                  "fluid record emits every independently declared column in exact 17-column DDL order");

            string baseHash = captured.AsStateRecord().HashHex();
            for (int i = 0; i < 14; i++)
            {
                var changed = FluidFixture();
                double changedValue = BitConverter.Int64BitsToDouble(expected[i] + 1L);
                SetFluidValue(changed, i, changedValue);
                string changedHash = TypedFluidStateRecord.FromFluidState("car", "car:codec:001", changed).AsStateRecord().HashHex();
                Check(changedHash != baseHash, "typed fluid row hash depends on field " + expectedNames[i + 3]);
            }

            var outOfRange = FluidFixture();
            for (int i = 0; i < 14; i++) SetFluidValue(outOfRange, i, i % 2 == 0 ? -123.5 : 98765.25);
            var invalidButFinite = TypedFluidStateRecord.FromFluidState("engine", "engine:finite-out-of-range", outOfRange);
            Check(FluidBits(invalidButFinite.Restore()).SequenceEqual(FluidBits(outOfRange)), "codec preserves finite out-of-domain values without normalization or repair");

            for (int i = 0; i < 14; i++)
            {
                var nonfinite = FluidFixture();
                SetFluidValue(nonfinite, i, i % 2 == 0 ? double.NaN : double.PositiveInfinity);
                int field = i;
                Throws<ArgumentException>(() => TypedFluidStateRecord.FromFluidState("car", "car:bad:" + field.ToString(CultureInfo.InvariantCulture), nonfinite),
                    "nonfinite value rejected in fluid field " + i.ToString(CultureInfo.InvariantCulture));
            }
            Throws<ArgumentException>(() => TypedFluidStateRecord.FromFluidState("vehicle", "id", FluidFixture()), "unsupported fluid owner kind rejected");
            Throws<ArgumentException>(() => TypedFluidStateRecord.FromFluidState("car", "", FluidFixture()), "empty fluid owner ID rejected");
            Throws<ArgumentNullException>(() => TypedFluidStateRecord.FromFluidState("car", "id", null), "null fluid source rejected");

            var wrongVersionFields = stateRecord.Fields();
            wrongVersionFields[2] = TypedStateField.Int64("value_version", 2);
            var wrongVersion = new TypedStateRecord(stateRecord.TableName, stateRecord.RecordKey, wrongVersionFields);
            Throws<NotSupportedException>(() => TypedFluidStateRecord.FromStateRecord(wrongVersion), "unknown fluid row version rejected");
            var wrongTable = new TypedStateRecord("other_table", stateRecord.RecordKey, stateRecord.Fields());
            Throws<ArgumentException>(() => TypedFluidStateRecord.FromStateRecord(wrongTable), "wrong fluid table name rejected");
            var missingField = new TypedStateRecord(stateRecord.TableName, stateRecord.RecordKey, stateRecord.Fields().Take(16));
            Throws<InvalidDataException>(() => TypedFluidStateRecord.FromStateRecord(missingField), "missing fluid row field rejected");
            var extraFields = stateRecord.Fields().Concat(new[] { TypedStateField.Int64("extra", 0) });
            var extraField = new TypedStateRecord(stateRecord.TableName, stateRecord.RecordKey, extraFields);
            Throws<InvalidDataException>(() => TypedFluidStateRecord.FromStateRecord(extraField), "extra fluid row field rejected");
            var nonfiniteFields = stateRecord.Fields();
            nonfiniteFields[3] = TypedStateField.Int64("oil_litres_bits", BitConverter.DoubleToInt64Bits(double.NaN));
            var nonfiniteRow = new TypedStateRecord(stateRecord.TableName, stateRecord.RecordKey, nonfiniteFields);
            Throws<ArgumentException>(() => TypedFluidStateRecord.FromStateRecord(nonfiniteRow), "nonfinite encoded fluid bits rejected during typed row reconstruction");
            var wrongKey = new TypedStateRecord(stateRecord.TableName, "wrong-key", stateRecord.Fields());
            Throws<InvalidDataException>(() => TypedFluidStateRecord.FromStateRecord(wrongKey), "fluid row with mismatched composite key rejected");
            var reorderedFields = stateRecord.Fields();
            var swap = reorderedFields[3]; reorderedFields[3] = reorderedFields[4]; reorderedFields[4] = swap;
            var reordered = new TypedStateRecord(stateRecord.TableName, stateRecord.RecordKey, reorderedFields);
            Throws<InvalidDataException>(() => TypedFluidStateRecord.FromStateRecord(reordered), "fluid row with a non-DDL field order rejected");
        }

        static long NextBits(ref ulong state)
        {
            unchecked
            {
                state ^= state >> 12;
                state ^= state << 25;
                state ^= state >> 27;
                return (long)(state * 0x2545F4914F6CDD1DUL);
            }
        }

        static void CheckNativeSqliteInt64AndFluidRoundTrip()
        {
            var values = new List<long> { long.MinValue, long.MaxValue, 0, 1, -1, unchecked((long)0x8000000000000000UL), 0x7fefffffffffffffL };
            ulong seed = 0xD1B54A32D192ED03UL;
            for (int i = 0; i < 1000; i++) values.Add(NextBits(ref seed));
            using (var db = new SqliteNative(":memory:"))
            {
                Check(db.Scalar("SELECT sqlite_version()") != null, "managed native verification opens the bundled SQLite in-memory database");
                db.Exec("CREATE TABLE bits(slot INTEGER PRIMARY KEY, value_bits INTEGER NOT NULL)");
                using (var insertBits = db.Prepare("INSERT INTO bits(slot,value_bits) VALUES(?,?)"))
                    for (int i = 0; i < values.Count; i++) insertBits.Execute((long)i, values[i]);
                var rows = db.Query("SELECT value_bits FROM bits ORDER BY slot");
                bool exact = rows.Count == values.Count;
                for (int i = 0; exact && i < values.Count; i++)
                    exact = long.Parse(rows[i][0], NumberStyles.Integer, CultureInfo.InvariantCulture) == values[i];
                Check(exact, "SQLite INTEGER bind to column_text to invariant Int64 parse preserves signed extrema and 1,000 seeded arbitrary bit patterns");

                db.Exec("CREATE TABLE text_values(slot INTEGER PRIMARY KEY, value TEXT NULL)");
                using (var insertText = db.Prepare("INSERT INTO text_values(slot,value) VALUES(?,?)"))
                {
                    insertText.Execute(0L, null);
                    insertText.Execute(1L, "");
                    insertText.Execute(2L, "é\0🚗");
                }
                var textRows = db.Query("SELECT value FROM text_values ORDER BY slot");
                Check(textRows.Count == 3 && textRows[0][0] == null && textRows[1][0] == "" && textRows[2][0] == "é\0🚗",
                    "managed native bound-text roundtrip preserves SQL NULL, empty text, Unicode and embedded NUL distinctly");

                var source = FluidFixture();
                long[] expected = FluidBits(source);
                var typed = TypedFluidStateRecord.FromFluidState("car", "car:sqlite:001", source).AsStateRecord();
                var sqlValues = typed.SqlValues();
                string[] names = typed.Fields().Select(field => field.Name).ToArray();
                string columnDefs = string.Join(",", names.Select((name, i) => "c" + i.ToString(CultureInfo.InvariantCulture) + (i < 2 ? " TEXT" : " INTEGER")));
                string columnList = string.Join(",", names.Select((name, i) => "c" + i.ToString(CultureInfo.InvariantCulture)));
                string placeholders = string.Join(",", names.Select(_ => "?"));
                db.Exec("CREATE TABLE fluid_bits (" + columnDefs + ")");
                using (var insert = db.Prepare("INSERT INTO fluid_bits (" + columnList + ") VALUES (" + placeholders + ")")) insert.Execute(sqlValues);
                var sqlRows = db.Query("SELECT " + columnList + " FROM fluid_bits");
                Check(sqlRows.Count == 1 && sqlRows[0].Length == 17, "all 17 fluid row columns are stored and read through managed SqliteNative");

                var originalFields = typed.Fields();
                var readFields = new TypedStateField[originalFields.Length];
                for (int i = 0; i < originalFields.Length; i++)
                {
                    switch (originalFields[i].Kind)
                    {
                        case TypedStateValueKind.Null:
                            Check(sqlRows[0][i] == null, "SQLite preserves null fluid-column binding " + i.ToString(CultureInfo.InvariantCulture));
                            readFields[i] = TypedStateField.Null(originalFields[i].Name);
                            break;
                        case TypedStateValueKind.Text:
                            Check(sqlRows[0][i] == (string)sqlValues[i], "SQLite preserves text fluid-column binding " + i.ToString(CultureInfo.InvariantCulture));
                            readFields[i] = TypedStateField.Text(originalFields[i].Name, sqlRows[0][i]);
                            break;
                        case TypedStateValueKind.Int64:
                            long parsed = long.Parse(sqlRows[0][i], NumberStyles.Integer, CultureInfo.InvariantCulture);
                            Check(parsed == (long)sqlValues[i], "SQLite preserves signed fluid INTEGER column " + originalFields[i].Name);
                            readFields[i] = TypedStateField.Int64(originalFields[i].Name, parsed);
                            break;
                        default: throw new InvalidOperationException("Unexpected typed fluid SQL value kind.");
                    }
                }
                var rehydrated = TypedFluidStateRecord.FromStateRecord(new TypedStateRecord(typed.TableName, typed.RecordKey, readFields));
                Check(FluidBits(rehydrated.Restore()).SequenceEqual(expected), "queried SQLite fluid row reconstructs all 14 original double bit patterns through the typed row reader");
            }
        }

        public static void RunStandalone()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("S05a1 checks require an Editor batch process outside Play Mode.");
            Report.Length = 0;
            passed = 0;
            Report.AppendLine("S05a1 dormant typed state codecs; no schema, save, migration, writer, profile, JsonUtility, or caller activation.");
            try
            {
                CheckIndependentGoldenVector();
                CheckCultureAndCompositeKeys();
                CheckImmutabilityAndInvalidInputs();
                CheckFluidRecordRoundTrip();
                CheckNativeSqliteInt64AndFluidRoundTrip();
                Report.AppendLine("RESULT PASS count=" + passed.ToString(CultureInfo.InvariantCulture));
                UnityEngine.Debug.Log("S05a1 typed state record checks passed: " + passed.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception error)
            {
                Report.AppendLine("RESULT FAIL count=" + passed.ToString(CultureInfo.InvariantCulture));
                Report.AppendLine(error.ToString());
                UnityEngine.Debug.LogException(error);
                throw;
            }
            finally
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S05A1"));
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "typed-record-checks.txt"), Report.ToString(), new UTF8Encoding(false));
            }
        }
    }
}
#endif
