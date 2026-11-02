#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Infrastructure;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Editor-only tests for the dormant typed canonical values; no live route is switched.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        static readonly string[] CartValueFields =
        { "definitionId", "quantity", "install", "preparedBlock", "preparationOwner", "action", "assetId", "role", "destinationId", "destination", "positions" };

        static readonly string[] ProductTopLevelFields =
        {
            "name", "fitment", "brand", "quality", "fuelKind", "pistonCrown", "crownVolumeCc", "valveReliefMm", "springMaxLiftMm",
            "followerInterface", "plugHeatRange", "category", "boreMm", "power", "grip", "massScale", "shiftScale", "capacityScale",
            "boostScale", "centerRatioMin", "centerRatioMax", "awdHardware", "rpmLimit", "torqueLimitNm", "massKg", "inertiaKgM2",
            "positionFromFrontAxleM", "heightM", "price", "stage", "reputationRequired", "clutchEnvironment", "clutchMaterial",
            "clutchActuation", "clutchPlateCount", "clutchOilCapacityLitres", "clutchEngagementRpm", "tuning", "turboSize", "fixedBoost",
            "boostMin", "boostMax", "fixedPulley", "pulleyMin", "pulleyMax", "fixedFinalDrive", "finalDriveMin", "finalDriveMax", "valve",
            "turboUnits", "superchargerUnits", "fuelDelivery", "supportsInjection", "supportsDiesel", "injectionControlMetadataVersion",
            "controllerCylinderCapacity", "injectionIdleControl", "injectionMixtureVariation", "camLiftMm", "camDurationDegrees",
            "camOverlapDegrees", "camLobeSeparation", "camTorqueBias", "valvetrainRpm", "camTimingAdjustable", "fluidCapacityLitres",
            "fluidMinimumLitres", "baffling", "pumpFlowScale", "capPressureBar", "induction", "transmission", "manualDownshiftInterlock",
            "manualLayout", "sequentialShiftScale", "thermalLimitC", "heatCapacityJPerC", "coolingWattsPerC", "wearEnergyJ", "overloadSeconds",
            "tireLifeDistanceM", "tireOptimalC", "tireHeatFraction", "description", "differential", "torqueBiasRatio", "preloadNm",
            "engineHeatTransferScale", "maxPowerHp", "provenance"
        };

        static readonly string[] ProductStringFields =
        { "name", "fitment", "brand", "quality", "followerInterface", "description", "provenance" };

        static readonly string[] AwdHardwareFields =
        { "version", "modes", "integratedCenter", "adjustments", "calibration", "responseMin", "responseMax" };

        static readonly string[] AwdCalibrationFields =
        { "mode", "centerSpeedRatio", "torqueBiasRatio", "centerPreloadNm", "frontBias", "minimumFront", "maximumFront", "primaryFront",
            "isolateFailedAxle", "maximumSecondary", "responseSeconds", "slipThreshold", "preload", "freeShaftInertiaKgM2", "freeShaftDragNm", "freeShaftViscousDrag" };

        readonly struct SerializedFieldSpec
        {
            internal readonly string Name;
            internal readonly Type FieldType;
            internal SerializedFieldSpec(string name, Type fieldType) { Name = name; FieldType = fieldType; }
        }

        sealed class CheckLog
        {
            readonly Action<bool, string> callback;
            readonly StringBuilder report = new StringBuilder();
            readonly string folder;
            int assertions, failures;

            internal CheckLog(Action<bool, string> check, string outputFolder) { callback = check; folder = outputFolder; }
            internal void Check(bool passed, string label)
            {
                assertions++; if (!passed) failures++;
                string line = "ASSERT|" + (passed ? "PASS" : "FAIL") + "|" + L1Safe(label);
                report.AppendLine(line); callback(passed, label);
            }
            internal void Note(string line) => report.AppendLine(line);
            internal void Flush()
            {
                report.Insert(0, "S06D2B1L1A canonical value checks\nassertions=" + assertions + ";failures=" + failures + "\n");
                File.WriteAllText(Path.Combine(folder, "S06D2B1L1A_CANONICAL_VALUES.txt"), report.ToString(), new UTF8Encoding(false));
            }
        }

        sealed class Result<T>
        {
            internal T Value;
            internal Exception Error;
        }

        sealed class SinglePassEnumerable<T> : IEnumerable<T>
        {
            readonly T[] values;
            internal int EnumerationCount { get; private set; }
            internal SinglePassEnumerable(T[] items) { values = items; }
            public IEnumerator<T> GetEnumerator()
            {
                EnumerationCount++;
                if (EnumerationCount > 1) throw new InvalidOperationException("Single-pass fixture was enumerated more than once.");
                return ((IEnumerable<T>)values).GetEnumerator();
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        sealed class ProductEntry
        {
            internal PartProductValue Value;
            internal string Oracle;
            internal Exception OracleError;
        }

        sealed class ProductPairFacts
        {
            internal PartProductValue LeftBefore, RightBefore, LeftValue, RightValue;
            internal bool LeftSupported, RightSupported, ComparisonSupported, TypedEqual;
            internal int LeftHash, RightHash;
        }

        sealed class CollisionComparer : IEqualityComparer<PartProductValue>
        {
            internal bool Unsupported;
            public int GetHashCode(PartProductValue value) => 17;
            public bool Equals(PartProductValue left, PartProductValue right)
            {
                if (ReferenceEquals(left, right))
                {
                    if (left == null) return true;
                    if (!left.TryProductEquivalent(right, out bool selfEqual)) { Unsupported = true; return false; }
                    return selfEqual;
                }
                if (left == null || right == null) return left == right;
                if (!left.TryProductEquivalent(right, out bool equal)) { Unsupported = true; return false; }
                return equal;
            }
        }

        /// <summary>Run L1A's dormant typed value diagnostics. Root owns Unity execution and source freezing.</summary>
        public static void RunCanonicalValues(Action<bool, string> check, string folder)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("An isolated Editor report folder is required.", nameof(folder));
            Directory.CreateDirectory(folder);
            var log = new CheckLog(check, folder);
            log.Note("scope=typed canonical snapshots only; no live equality/product key/market/cart route changes");
            log.Note("instrumentation=typed operations are bracketed by observable counters; frozen Unity JSON oracle calls run separately and these counters are not treated as proof that every direct JsonUtility call is intercepted");
            try
            {
                RunCartSnapshotChecks(log);
            RunProductSnapshotChecks(log);
            }
            catch (Exception error)
            {
                log.Check(false, "diagnostic completed without an unhandled exception / " + error.GetType().FullName + ": " + error.Message);
            }
            finally { log.Flush(); }
        }

        static void RunCartSnapshotChecks(CheckLog log)
        {
            var baseline = new PartCartLine();
            var defaultRow = new PartCartLine();
            CompareCartPair(log, "default-self", new[] { baseline.Copy() }, new[] { defaultRow.Copy() }, true, true);
            CompareCartPair(log, "null-default", new PartCartLine[] { null }, new[] { new PartCartLine() }, false, true);
            CompareCartPair(log, "default-null", new[] { new PartCartLine() }, new PartCartLine[] { null }, false, true);
            CompareCartPair(log, "null-zero-row", new PartCartLine[] { null }, new[] { new PartCartLine { quantity = 0 } }, false, false);
            CompareCartPair(log, "positions-null-empty", new[] { Row(baseline, r => r.positions = null) },
                new[] { Row(baseline, r => r.positions = new List<string>()) }, false, true);
            CompareCartPair(log, "position-null-empty", new[] { Row(baseline, r => r.positions = new List<string> { null }) },
                new[] { Row(baseline, r => r.positions = new List<string> { "" }) }, false, true);
            CompareCartPair(log, "position-order", new[] { Row(baseline, r => r.positions = new List<string> { "a", "b" }) },
                new[] { Row(baseline, r => r.positions = new List<string> { "b", "a" }) }, false, false);
            CompareCartPair(log, "outer-order", new[] { new PartCartLine { definitionId = "a" }, new PartCartLine { definitionId = "b" } },
                new[] { new PartCartLine { definitionId = "b" }, new PartCartLine { definitionId = "a" } }, false, false);
            CompareCartPair(log, "repeated-rows", new[] { baseline.Copy(), baseline.Copy() }, new[] { baseline.Copy(), baseline.Copy() }, true, true);

            foreach (string field in new[] { "definitionId", "preparationOwner", "assetId", "role", "destinationId" })
            {
                var nullValue = Row(baseline, row => SetCartString(row, field, null));
                var emptyValue = Row(baseline, row => SetCartString(row, field, ""));
                CompareCartPair(log, "cart-" + field + "-null-empty", new[] { nullValue }, new[] { Row(baseline, row => SetCartString(row, field, "")) }, false, true);
                CompareCartPair(log, "cart-" + field + "-empty-null", new[] { emptyValue }, new[] { Row(baseline, row => SetCartString(row, field, null)) }, false, true);
                CheckCartUnicodePairs(log, baseline, field);
            }
            CheckCartPositionUnicodePairs(log, baseline);

            var singleField = new List<Tuple<string, Action<PartCartLine>>>
            {
                Tuple.Create("definitionId", (Action<PartCartLine>)(r=>r.definitionId="l1a-definition")),
                Tuple.Create("quantity", (Action<PartCartLine>)(r=>r.quantity=2)),
                Tuple.Create("install", (Action<PartCartLine>)(r=>r.install=true)),
                Tuple.Create("preparedBlock", (Action<PartCartLine>)(r=>r.preparedBlock=true)),
                Tuple.Create("preparationOwner", (Action<PartCartLine>)(r=>r.preparationOwner="l1a-owner")),
                Tuple.Create("action", (Action<PartCartLine>)(r=>r.action=(CartAction)unchecked((int)r.action+1))),
                Tuple.Create("assetId", (Action<PartCartLine>)(r=>r.assetId="l1a-asset")),
                Tuple.Create("role", (Action<PartCartLine>)(r=>r.role="l1a-role")),
                Tuple.Create("destinationId", (Action<PartCartLine>)(r=>r.destinationId="l1a-target")),
                Tuple.Create("destination", (Action<PartCartLine>)(r=>r.destination=(InventoryKind)unchecked((int)r.destination+1))),
                Tuple.Create("positions", (Action<PartCartLine>)(r=>r.positions=new List<string>{"l1a-position"}))
            };
            foreach (var field in singleField)
                CompareCartPair(log, "cart-field-" + field.Item1, new[] { baseline.Copy() }, new[] { Row(baseline, field.Item2) }, false, null);
            CompareCartPair(log, "cart-undefined-action", new[] { baseline.Copy() }, new[] { Row(baseline, row => row.action = (CartAction)777777) }, false, null);
            CompareCartPair(log, "cart-undefined-destination", new[] { baseline.Copy() }, new[] { Row(baseline, row => row.destination = (InventoryKind)777777) }, false, null);

            CompareCartNullEnumerable(log);
            CheckCartCaptureOwnership(log, baseline);
            CheckCartAccessorMapping(log);
            var manyRows = Enumerable.Range(0, 137).Select(index => new PartCartLine { definitionId = "l1a-row-" + index }).ToArray();
            var singlePass = new SinglePassEnumerable<PartCartLine>(manyRows);
            int count = PureTyped(log, "capture uncapped single-pass cart input", () => CartRowsSnapshot.Capture(singlePass).Count);
            log.Check(singlePass.EnumerationCount == 1 && count == manyRows.Length,
                "cart snapshot enumerates the source exactly once with no cart row-count cap / rows=" + count);
            log.Note("cart-schema|expected-fields=" + string.Join(",", CartValueFields) + "|count=" + CartValueFields.Length);
        }

        static void CheckCartUnicodePairs(CheckLog log, PartCartLine baseline, string field)
        {
            TraceCartUnicodeObservations(log, field, new[] { "L1A-\uD800\U0001F680", "L1A-\uDC00\U0001F680", "L1A-\uD800A", "L1A-\uD800\uD801" },
                new[] { "L1A-\uFFFD\U0001F680", "L1A-\uFFFD\U0001F680", "L1A-\uFFFDA", "L1A-\uFFFD\uFFFD" });
            CompareCartPair(log, "cart-" + field + "-unpaired-high-replacement", new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uD800")) },
                new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uFFFD")) }, false, null);
            CompareCartPair(log, "cart-" + field + "-replacement-unpaired-high", new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uFFFD")) },
                new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uD800")) }, false, null);
            CompareCartPair(log, "cart-" + field + "-unpaired-low-replacement", new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uDC00")) },
                new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uFFFD")) }, false, null);
            CompareCartPair(log, "cart-" + field + "-replacement-unpaired-low", new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uFFFD")) },
                new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uDC00")) }, false, null);
            CompareCartPair(log, "cart-" + field + "-supplementary-replacement", new[] { Row(baseline, row => SetCartString(row, field, "L1A-\U0001F680")) },
                new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uFFFD")) }, false, null);
            CompareCartPair(log, "cart-" + field + "-replacement-supplementary", new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uFFFD")) },
                new[] { Row(baseline, row => SetCartString(row, field, "L1A-\U0001F680")) }, false, null);
            CompareCartPair(log, "cart-" + field + "-mixed-surrogate-supplementary", new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uD800\U0001F680")) },
                new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uFFFD\U0001F680")) }, false, false);
            CompareCartPair(log, "cart-" + field + "-mixed-replacement-supplementary", new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uFFFD\U0001F680")) },
                new[] { Row(baseline, row => SetCartString(row, field, "L1A-\uD800\U0001F680")) }, false, false);
            CheckCartMalformedControls(log, baseline, field);
        }

        static void CheckCartPositionUnicodePairs(CheckLog log, PartCartLine baseline)
        {
            TraceCartPositionObservations(log, new[] { "L1A-\uD800\U0001F680", "L1A-\uDC00\U0001F680", "L1A-\uD800A", "L1A-\uD800\uD801" },
                new[] { "L1A-\uFFFD\U0001F680", "L1A-\uFFFD\U0001F680", "L1A-\uFFFDA", "L1A-\uFFFD\uFFFD" });
            foreach (var pair in new[]
            {
                Tuple.Create("high", "L1A-\uD800", "L1A-\uFFFD"), Tuple.Create("low", "L1A-\uDC00", "L1A-\uFFFD"),
                Tuple.Create("supplementary", "L1A-\U0001F680", "L1A-\uFFFD"),
                Tuple.Create("mixed", "L1A-\uD800\U0001F680", "L1A-\uFFFD\U0001F680")
            })
            {
                CompareCartPair(log, "cart-position-unicode-" + pair.Item1, new[] { Row(baseline, row => row.positions = new List<string> { pair.Item2 }) },
                    new[] { Row(baseline, row => row.positions = new List<string> { pair.Item3 }) }, false, pair.Item1 == "mixed" ? (bool?)false : null);
                CompareCartPair(log, "cart-position-unicode-reverse-" + pair.Item1, new[] { Row(baseline, row => row.positions = new List<string> { pair.Item3 }) },
                    new[] { Row(baseline, row => row.positions = new List<string> { pair.Item2 }) }, false, pair.Item1 == "mixed" ? (bool?)false : null);
            }
            CheckCartMalformedControls(log, baseline, "positions");
        }

        static void CheckCartMalformedControls(CheckLog log, PartCartLine baseline, string field)
        {
            var controls = new[]
            {
                Tuple.Create("high-high-low-vs-two-replacements", "L1A-\uD800\uD800\uDC00", "L1A-\uFFFD\uFFFD", true),
                Tuple.Create("high-ascii-vs-replacement", "L1A-\uD800A", "L1A-\uFFFD", true),
                Tuple.Create("two-high-vs-replacement", "L1A-\uD800\uD800", "L1A-\uFFFD", true),
                Tuple.Create("high-ascii-negative-control", "L1A-\uD800A", "L1A-\uFFFDA", false),
                Tuple.Create("low-valid-pair", "L1A-\uDC00\U0001F680", "L1A-\uFFFD\U0001F680", true),
                Tuple.Create("valid-pair-high", "L1A-\U0001F680\uD800", "L1A-\U0001F680\uFFFD", true)
            };
            foreach (var control in controls)
            {
                PartCartLine left = field == "positions"
                    ? Row(baseline, row => row.positions = new List<string> { control.Item2 })
                    : Row(baseline, row => SetCartString(row, field, control.Item2));
                PartCartLine right = field == "positions"
                    ? Row(baseline, row => row.positions = new List<string> { control.Item3 })
                    : Row(baseline, row => SetCartString(row, field, control.Item3));
                CompareCartPair(log, "cart-" + field + "-legacy-decoder-" + control.Item1, new[] { left }, new[] { right }, false, control.Item4);
            }
        }

        static PartCartLine Row(PartCartLine source, Action<PartCartLine> mutate)
        { var row = source.Copy(); mutate(row); return row; }

        static void SetCartString(PartCartLine row, string field, string value)
        {
            switch (field)
            {
                case "definitionId": row.definitionId = value; break;
                case "preparationOwner": row.preparationOwner = value; break;
                case "assetId": row.assetId = value; break;
                case "role": row.role = value; break;
                case "destinationId": row.destinationId = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        static void CompareCartPair(CheckLog log, string label, IEnumerable<PartCartLine> left, IEnumerable<PartCartLine> right,
            bool expectedRaw, bool? expectedEffective)
        {
            var leftSources = left?.ToArray(); var rightSources = right?.ToArray();
            var leftPositionRefs = leftSources?.Select(row => row?.positions).ToArray();
            var rightPositionRefs = rightSources?.Select(row => row?.positions).ToArray();
            var typed = PureTyped(log, "cart typed capture/equality / " + label, () =>
            {
                var typedLeft = CartRowsSnapshot.Capture(left);
                var typedRight = CartRowsSnapshot.Capture(right);
                return Tuple.Create(typedLeft, typedRight, typedLeft.RawEquals(typedRight), typedLeft.CartSignatureEquals(typedRight),
                    typedLeft.RawEquals(CartRowsSnapshot.Capture(left)), typedRight.RawEquals(CartRowsSnapshot.Capture(right)));
            });
            bool raw = typed.Item3;
            bool effective = typed.Item4;
            var oracleLeft = CaptureFrozenString(() => LegacyCartCanonicalBoundaryReference.Signature(left));
            var oracleRight = CaptureFrozenString(() => LegacyCartCanonicalBoundaryReference.Signature(right));
            bool oracleComparable = oracleLeft.Error == null && oracleRight.Error == null;
            bool oracleEqual = oracleComparable && string.Equals(oracleLeft.Value, oracleRight.Value, StringComparison.Ordinal);
            log.Check(raw == expectedRaw, "cart raw equality outcome / " + label + " / expected=" + expectedRaw + ";actual=" + raw);
            if (expectedEffective.HasValue)
                log.Check(effective == expectedEffective.Value, "cart effective equality outcome / " + label + " / expected=" + expectedEffective.Value + ";actual=" + effective);
            log.Check(oracleComparable && effective == oracleEqual,
                "cart typed signature equals frozen JSON signature equality / " + label + ";oracle=" + (oracleComparable ? oracleEqual.ToString() : ErrorLabel(oracleLeft.Error ?? oracleRight.Error)));
            log.Check(typed.Item5 && typed.Item6 && SameCartReferences(leftSources, leftPositionRefs, left) &&
                SameCartReferences(rightSources, rightPositionRefs, right), "cart capture/equality preserves caller row/list references, values and null shape / " + label);
        }

        static bool SameCartReferences(PartCartLine[] rows, List<string>[] positions, IEnumerable<PartCartLine> current)
        {
            if (rows == null) return current == null;
            if (current == null) return false;
            var now = current.ToArray();
            if (now.Length != rows.Length) return false;
            for (int i = 0; i < rows.Length; i++)
                if (!ReferenceEquals(rows[i], now[i]) || !ReferenceEquals(positions[i], now[i]?.positions)) return false;
            return true;
        }

        static void CompareCartNullEnumerable(CheckLog log)
        {
            Exception capture = null, oracle = null;
            try { PureTyped(log, "null cart input", () => CartRowsSnapshot.Capture(null)); }
            catch (Exception error) { capture = error; }
            try { LegacyCartCanonicalBoundaryReference.Signature(null); } catch (Exception error) { oracle = error; }
            log.Check(capture != null && oracle != null && capture.GetType() == oracle.GetType() && capture.Message == oracle.Message,
                "null outer enumerable retains exact frozen exception type/message / " + ErrorLabel(capture));
            log.Check(capture is ArgumentNullException argumentNull && argumentNull.ParamName == "source", "null outer enumerable identifies parameter source");
        }

        static void TraceCartUnicodeObservations(CheckLog log, string field, string[] inputs, string[] replacementControls)
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                var row = new PartCartLine(); SetCartString(row, field, inputs[i]);
                var control = new PartCartLine(); SetCartString(control, field, replacementControls[i]);
                TraceJsonField(log, "observation-only cart field=" + field + " probe=" + i, inputs[i],
                    LegacyCartCanonicalBoundaryReference.Signature(new[] { row }), field);
                TraceJsonField(log, "observation-only cart replacement-control field=" + field + " probe=" + i, replacementControls[i],
                    LegacyCartCanonicalBoundaryReference.Signature(new[] { control }), field);
            }
        }

        static void TraceCartPositionObservations(CheckLog log, string[] inputs, string[] replacementControls)
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                string[] values = { inputs[i], replacementControls[i] };
                for (int side = 0; side < values.Length; side++)
                {
                    var row = new PartCartLine { positions = new List<string> { values[side] } };
                    TraceJsonField(log, "observation-only cart position probe=" + i + " side=" + side, values[side],
                        LegacyCartCanonicalBoundaryReference.Signature(new[] { row }), "positions");
                }
            }
        }

        static void TraceJsonField(CheckLog log, string label, string input, string json, string fieldName)
        {
            string token = ExtractJsonFieldToken(json, fieldName);
            log.Note("UTF16-OBS|" + L1Safe(label) + "|input=" + Utf16Units(input, 96) + "|frozen-token=" + Utf16Units(token, 96));
        }

        static string Utf16Units(string value, int maxUnits)
        {
            if (value == null) return "<null>";
            int length = Math.Min(value.Length, maxUnits);
            var units = new StringBuilder(length * 5);
            for (int i = 0; i < length; i++)
            {
                if (i > 0) units.Append(',');
                units.Append(((int)value[i]).ToString("X4", CultureInfo.InvariantCulture));
            }
            if (value.Length > length) units.Append(",...");
            return "length=" + value.Length + ";u16=" + units;
        }

        static string ExtractJsonFieldToken(string json, string fieldName)
        {
            if (json == null || fieldName == null) return null;
            string sought = "\"" + fieldName + "\"";
            for (int index = 0; index < json.Length;)
            {
                if (json[index] != '"') { index++; continue; }
                int end = JsonStringEnd(json, index);
                if (end < 0) return "<unterminated-json-string>";
                bool property = end - index + 1 == sought.Length && string.CompareOrdinal(json, index, sought, 0, sought.Length) == 0;
                int next = end + 1;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (property && next < json.Length && json[next] == ':')
                {
                    next++;
                    while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                    int valueEnd = JsonValueEnd(json, next);
                    return valueEnd < next ? "<missing-json-value>" : json.Substring(next, valueEnd - next);
                }
                index = end + 1;
            }
            return "<field-not-found>";
        }

        static int JsonStringEnd(string json, int start)
        {
            for (int i = start + 1; i < json.Length; i++)
            {
                if (json[i] == '\\') { i++; continue; }
                if (json[i] == '"') return i;
            }
            return -1;
        }

        static int JsonValueEnd(string json, int start)
        {
            if (start >= json.Length) return -1;
            if (json[start] == '"') { int end = JsonStringEnd(json, start); return end < 0 ? -1 : end + 1; }
            if (json[start] != '[' && json[start] != '{')
            {
                int end = start;
                while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']' && !char.IsWhiteSpace(json[end])) end++;
                return end;
            }
            var stack = new Stack<char>();
            stack.Push(json[start] == '[' ? ']' : '}');
            for (int i = start + 1; i < json.Length; i++)
            {
                if (json[i] == '"') { int stringEnd = JsonStringEnd(json, i); if (stringEnd < 0) return -1; i = stringEnd; continue; }
                if (json[i] == '[') stack.Push(']');
                else if (json[i] == '{') stack.Push('}');
                else if ((json[i] == ']' || json[i] == '}') && stack.Count > 0)
                {
                    if (stack.Pop() != json[i]) return -1;
                    if (stack.Count == 0) return i + 1;
                }
            }
            return -1;
        }

        static void CheckCartCaptureOwnership(CheckLog log, PartCartLine source)
        {
            var sourceRow = Row(source, row => row.positions = new List<string> { "front", null });
            var sourcePositions = sourceRow.positions;
            var result = PureTyped(log, "cart owned snapshot isolation", () =>
            {
                var sourceFacts = CartRowsSnapshot.Capture(new[] { sourceRow });
                var captured = CartRowsSnapshot.Capture(new[] { sourceRow });
                return Tuple.Create(sourceFacts, captured);
            });
            var sourceFacts = result.Item1;
            var captured = result.Item2;
            var outputFacts = PureTyped(log, "cart defensive position outputs", () =>
            {
                var value = captured.GetRow(0);
                var copy1 = value.CopyPositions(); var copy2 = value.CopyPositions();
                if (copy1.Length > 0) copy1[0] = "mutated-output";
                return Tuple.Create(value, copy1, copy2, captured.RawEquals(sourceFacts));
            });
            log.Check(!ReferenceEquals(outputFacts.Item2, outputFacts.Item3) && outputFacts.Item4 &&
                ReferenceEquals(sourceRow.positions, sourcePositions) && sourceRow.positions.SequenceEqual(new[] { "front", null }),
                "cart snapshot owns positions and returns independent copies");
            sourceRow.definitionId = "mutated-source"; sourcePositions.Add("after-capture");
            bool stillSame = PureTyped(log, "cart snapshot after source mutation", () => captured.RawEquals(sourceFacts));
            log.Check(stillSame && outputFacts.Item1.DefinitionId != sourceRow.definitionId,
                "cart snapshot remains unchanged after caller mutates source and returned position copy");

            var rowSnapshots = PureTyped(log, "cart null-row and default-row markers", () => Tuple.Create(
                CartRowsSnapshot.Capture(new PartCartLine[] { null }), CartRowsSnapshot.Capture(new[] { new PartCartLine() })));
            var nullMarker = rowSnapshots.Item1;
            var defaultSnapshot = rowSnapshots.Item2;
            log.Check(nullMarker.Count == 1 && nullMarker.GetRow(0).IsNullRow && !defaultSnapshot.GetRow(0).IsNullRow,
                "cart snapshot exposes null-row marker separately from default row value");
        }

        static void CheckCartAccessorMapping(CheckLog log)
        {
            var row = new PartCartLine
            {
                definitionId = "l1a-definition", quantity = 4, install = true, preparedBlock = true,
                preparationOwner = "l1a-owner", action = (CartAction)7, assetId = "l1a-asset", role = "l1a-role",
                destinationId = "l1a-destination", destination = (InventoryKind)9, positions = new List<string> { null, "rear" }
            };
            var value = PureTyped(log, "capture cart row accessor mapping", () => CartRowsSnapshot.Capture(new[] { row }).GetRow(0));
            var positions = PureTyped(log, "copy cart row position sequence", () => value.CopyPositions());
            bool fields = !value.IsNullRow && value.DefinitionId == row.definitionId && value.Quantity == row.quantity &&
                value.Install == row.install && value.PreparedBlock == row.preparedBlock && value.PreparationOwner == row.preparationOwner &&
                value.Action == row.action && value.AssetId == row.assetId && value.Role == row.role && value.DestinationId == row.destinationId &&
                value.Destination == row.destination && !value.PositionsWasNull && value.PositionCount == 2 &&
                value.PositionAt(0) == null && value.PositionAt(1) == "rear" && positions.SequenceEqual(new[] { null, "rear" });
            log.Check(fields, "CartRowValue accessors map all 11 source fields and ordered null position entries");
        }

        static void RunProductSnapshotChecks(CheckLog log)
        {
            var authoredVehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            log.Check(authoredVehicles != null && authoredVehicles.Length > 0 && authoredVehicles.All(v => v != null && v.spec != null),
                "fresh authored VehicleDefinition resources are available without a profile clone");
            if (authoredVehicles == null || authoredVehicles.Length == 0 || authoredVehicles.Any(v => v == null || v.spec == null))
                return;

            var vehicleRefs = authoredVehicles.ToArray();
            var vehicleSpecRefs = authoredVehicles.Select(vehicle => vehicle.spec).ToArray();
            var vehicleAwdRefs = authoredVehicles.Select(vehicle => vehicle.spec.awd).ToArray();
            var vehicleIds = authoredVehicles.Select(vehicle => vehicle.spec.id).ToArray();
            var vehicleNames = authoredVehicles.Select(vehicle => vehicle.spec.displayName).ToArray();
            var vehicleDrives = authoredVehicles.Select(vehicle => vehicle.spec.drive).ToArray();
            var vehicleAwdFacts = authoredVehicles.Select(vehicle => AwdSourceFacts(vehicle.spec.awd)).ToArray();
            var catalog = new MechanicalCatalog(authoredVehicles.Select(vehicle => vehicle.spec));
            var definitions = catalog.parts.ToArray();
            var catalogPartsRef = catalog.parts;
            var sourceRefs = definitions.ToArray();
            var hardwareRefs = definitions.Select(definition => definition?.awdHardware).ToArray();
            var calibrationRefs = definitions.Select(definition => definition?.awdHardware?.calibration).ToArray();
            var sourceSnapshots = PureTyped(log, "capture full catalog raw source guard", () => definitions.Select(PartProductValue.Capture).ToArray());
            int observedAuthoredCount = definitions.Length;
            var aliases = MakeAliases(definitions);
            log.Check(aliases.Length == 3, "three disposable aliases are derived from real authored definitions / actual=" + aliases.Length);
            var all = new List<PartDefinition>(definitions.Length + aliases.Length);
            all.AddRange(definitions); all.AddRange(aliases);
            log.Note("catalog|fresh-resource-vehicle-count=" + authoredVehicles.Length + "|actual-generated-definition-count=" + observedAuthoredCount +
                "|test-alias-count=" + aliases.Length + "|partition-input-count=" + all.Count);

            CheckProductFieldInventory(log);
            CheckProductCoverageAndPairs(log, definitions.FirstOrDefault(value => value != null), definitions.FirstOrDefault(value => value?.awdHardware?.calibration != null));
            CheckProductSpecialValues(log, definitions);
            CheckProductPartition(log, all);
            CheckProductCollisionHandling(log, all);
            CheckProductOwnership(log, definitions.FirstOrDefault(value => value != null));

            bool unchanged = PureTyped(log, "recapture and compare full catalog raw source guard", () =>
            {
                bool same = definitions.Length == catalog.parts.Count && ReferenceEquals(catalog.parts, catalogPartsRef);
                for (int i = 0; same && i < definitions.Length; i++)
                    same = ReferenceEquals(definitions[i], catalog.parts[i]) && ReferenceEquals(definitions[i], sourceRefs[i]) &&
                        ReferenceEquals(definitions[i]?.awdHardware, hardwareRefs[i]) &&
                        ReferenceEquals(definitions[i]?.awdHardware?.calibration, calibrationRefs[i]) &&
                        sourceSnapshots[i].RawEquals(PartProductValue.Capture(definitions[i]));
                for (int i = 0; same && i < authoredVehicles.Length; i++)
                    same = ReferenceEquals(authoredVehicles[i], vehicleRefs[i]) && ReferenceEquals(authoredVehicles[i].spec, vehicleSpecRefs[i]) &&
                        ReferenceEquals(authoredVehicles[i].spec.awd, vehicleAwdRefs[i]) && authoredVehicles[i].spec.id == vehicleIds[i] &&
                        authoredVehicles[i].spec.displayName == vehicleNames[i] && authoredVehicles[i].spec.drive == vehicleDrives[i] &&
                        AwdSourceFacts(authoredVehicles[i].spec.awd) == vehicleAwdFacts[i];
                return same;
            });
            log.Check(unchanged, "complete actual catalogue and loaded vehicle/spec/AWD references plus raw definition values remain unchanged");
        }

        static PartDefinition[] MakeAliases(PartDefinition[] definitions)
        {
            var result = new List<PartDefinition>(3);
            foreach (var source in definitions.Where(value => value != null).Take(3))
            {
                var alias = source.Copy();
                alias.id = "l1a-alias-" + result.Count.ToString(CultureInfo.InvariantCulture);
                result.Add(alias);
            }
            return result.ToArray();
        }

        static string AwdSourceFacts(AwdSpec value)
        {
            if (value == null) return "<null>";
            return string.Join(",", new[]
            {
                ((int)value.mode).ToString(CultureInfo.InvariantCulture), FloatBits(value.centerSpeedRatio), FloatBits(value.torqueBiasRatio),
                FloatBits(value.centerPreloadNm), FloatBits(value.frontBias), FloatBits(value.minimumFront), FloatBits(value.maximumFront),
                value.primaryFront ? "1" : "0", value.isolateFailedAxle ? "1" : "0", FloatBits(value.maximumSecondary),
                FloatBits(value.responseSeconds), FloatBits(value.slipThreshold), FloatBits(value.preload), FloatBits(value.freeShaftInertiaKgM2),
                FloatBits(value.freeShaftDragNm), FloatBits(value.freeShaftViscousDrag)
            });
        }

        static string FloatBits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("X8", CultureInfo.InvariantCulture);

        static void CheckProductFieldInventory(CheckLog log)
        {
            var expected = new List<string> { "id" };
            foreach (string path in ProductTopLevelFields)
            {
                expected.Add(path);
                if (path != "awdHardware") continue;
                expected.AddRange(new[] { "awdHardware.version", "awdHardware.modes", "awdHardware.integratedCenter", "awdHardware.adjustments", "awdHardware.calibration" });
                expected.AddRange(AwdCalibrationFields.Select(field => "awdHardware.calibration." + field));
                expected.AddRange(new[] { "awdHardware.responseMin", "awdHardware.responseMax" });
            }
            var actual = PureTyped(log, "read explicit PartProductValue field inventory", () => PartProductValue.FieldPaths);
            bool exact = actual != null && actual.SequenceEqual(expected, StringComparer.Ordinal);
            log.Check(exact, "PartProductValue explicit source field inventory exactly matches the independently pinned paths / expected=" + expected.Count + ";actual=" + (actual?.Length ?? -1));
            if (actual != null && actual.Length > 0)
            {
                actual[0] = "caller-mutated-field-path";
                var secondCopy = PureTyped(log, "read defensive product field inventory copy", () => PartProductValue.FieldPaths);
                log.Check(secondCopy.Length > 0 && secondCopy[0] == "id", "PartProductValue field metadata returns defensive copies");
            }
            log.Check(CartValueFields.Length == 11 && CartValueFields.Distinct(StringComparer.Ordinal).Count() == 11,
                "PartCartLine test inventory has exactly 11 individually covered serialized fields");
            CheckSerializedFieldMetadata<PartCartLine>(log, "PartCartLine", L1ACartLineSerializedFields());
            CheckSerializedFieldMetadata<PartDefinition>(log, "PartDefinition", PartDefinitionSerializedFields());
            CheckSerializedFieldMetadata<AwdHardwareSpec>(log, "AwdHardwareSpec", new[]
            {
                F("version", typeof(int)), F("modes", typeof(int)), F("integratedCenter", typeof(bool)),
                F("adjustments", typeof(AwdAdjustments)), F("calibration", typeof(AwdSpec)), F("responseMin", typeof(float)), F("responseMax", typeof(float))
            });
            CheckSerializedFieldMetadata<AwdSpec>(log, "AwdSpec", new[]
            {
                F("mode", typeof(AwdMode)), F("centerSpeedRatio", typeof(float)), F("torqueBiasRatio", typeof(float)),
                F("centerPreloadNm", typeof(float)), F("frontBias", typeof(float)), F("minimumFront", typeof(float)),
                F("maximumFront", typeof(float)), F("primaryFront", typeof(bool)), F("isolateFailedAxle", typeof(bool)),
                F("maximumSecondary", typeof(float)), F("responseSeconds", typeof(float)), F("slipThreshold", typeof(float)),
                F("preload", typeof(float)), F("freeShaftInertiaKgM2", typeof(float)), F("freeShaftDragNm", typeof(float)),
                F("freeShaftViscousDrag", typeof(float))
            });
        }

        static SerializedFieldSpec F(string name, Type type) => new SerializedFieldSpec(name, type);

        static SerializedFieldSpec[] L1ACartLineSerializedFields() => new[]
        {
            F("definitionId", typeof(string)), F("quantity", typeof(int)), F("install", typeof(bool)), F("preparedBlock", typeof(bool)),
            F("preparationOwner", typeof(string)), F("action", typeof(CartAction)), F("assetId", typeof(string)), F("role", typeof(string)),
            F("destinationId", typeof(string)), F("destination", typeof(InventoryKind)), F("positions", typeof(List<string>))
        };

        static SerializedFieldSpec[] PartDefinitionSerializedFields()
        {
            var result = new List<SerializedFieldSpec>();
            Add(result, typeof(string), "id name fitment brand quality followerInterface description provenance");
            Add(result, typeof(FuelKind), "fuelKind"); Add(result, typeof(PistonCrown), "pistonCrown");
            Add(result, typeof(float), "crownVolumeCc valveReliefMm springMaxLiftMm boreMm power grip massScale shiftScale capacityScale boostScale centerRatioMin centerRatioMax rpmLimit torqueLimitNm massKg inertiaKgM2 positionFromFrontAxleM heightM clutchOilCapacityLitres clutchEngagementRpm turboSize fixedBoost boostMin boostMax fixedPulley pulleyMin pulleyMax fixedFinalDrive finalDriveMin finalDriveMax injectionIdleControl injectionMixtureVariation camLiftMm camDurationDegrees camOverlapDegrees camLobeSeparation camTorqueBias valvetrainRpm fluidCapacityLitres fluidMinimumLitres baffling pumpFlowScale capPressureBar sequentialShiftScale thermalLimitC heatCapacityJPerC coolingWattsPerC wearEnergyJ overloadSeconds tireLifeDistanceM tireOptimalC tireHeatFraction torqueBiasRatio preloadNm engineHeatTransferScale maxPowerHp");
            Add(result, typeof(int), "plugHeatRange price stage reputationRequired clutchPlateCount turboUnits superchargerUnits injectionControlMetadataVersion controllerCylinderCapacity");
            Add(result, typeof(PartCategory), "category");
            Add(result, typeof(ClutchEnvironment), "clutchEnvironment"); Add(result, typeof(ClutchMaterial), "clutchMaterial");
            Add(result, typeof(ClutchActuation), "clutchActuation"); Add(result, typeof(PartTuningOptions), "tuning");
            Add(result, typeof(BlowOffType), "valve"); Add(result, typeof(FuelDeliveryHardware), "fuelDelivery");
            Add(result, typeof(bool), "supportsInjection supportsDiesel camTimingAdjustable manualDownshiftInterlock");
            Add(result, typeof(EngineUpgradeInduction), "induction"); Add(result, typeof(TransmissionUpgrade), "transmission");
            Add(result, typeof(ManualGearboxLayout), "manualLayout"); Add(result, typeof(DifferentialKind), "differential");
            Add(result, typeof(AwdHardwareSpec), "awdHardware");
            return result.ToArray();
        }

        static void Add(List<SerializedFieldSpec> fields, Type type, string names)
        {
            foreach (string name in names.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) fields.Add(F(name, type));
        }

        static void CheckSerializedFieldMetadata<T>(CheckLog log, string label, SerializedFieldSpec[] expected)
        {
            var expectedMap = expected.ToDictionary(item => item.Name, item => item.FieldType, StringComparer.Ordinal);
            var actual = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => !field.IsStatic && !Attribute.IsDefined(field, typeof(NonSerializedAttribute), false) &&
                    (field.IsPublic || Attribute.IsDefined(field, typeof(SerializeField), false) || Attribute.IsDefined(field, typeof(SerializeReference), false)))
                .ToArray();
            bool exact = actual.Length == expectedMap.Count;
            foreach (var field in actual)
                exact &= expectedMap.TryGetValue(field.Name, out Type type) && type == field.FieldType;
            foreach (var item in expected)
                exact &= actual.Any(field => field.Name == item.Name && field.FieldType == item.FieldType);
            string mismatch = actual.FirstOrDefault(field => !expectedMap.TryGetValue(field.Name, out Type type) || type != field.FieldType)?.Name ??
                expected.FirstOrDefault(item => !actual.Any(field => field.Name == item.Name && field.FieldType == item.FieldType)).Name;
            log.Check(exact, label + " serialized instance-field metadata exactly matches independent name/type inventory" +
                (exact ? " / count=" + actual.Length : " / first-mismatch=" + (mismatch ?? "field-count")));
        }

        static void CheckProductCoverageAndPairs(CheckLog log, PartDefinition baseline, PartDefinition awdBaseline)
        {
            if (baseline == null) { log.Check(false, "authored definition available for field mutation probes"); return; }
            var baselineValue = PureTyped(log, "capture product baseline", () => PartProductValue.Capture(baseline));
            var baselineOracle = LegacyCartCanonicalBoundaryReference.ProductCacheMiss(baseline);
            log.Check(ProductSupported(log, "product-baseline", baselineValue), "authored baseline supports equivalence and hash");

            var idOnly = baseline.Copy(); idOnly.id = (baseline.id ?? "") + "/l1a-id-only";
            CheckProductPair(log, "product-id-only", baseline, idOnly, true, false);

            foreach (string field in ProductStringFields)
            {
                var nullVariant = baseline.Copy(); SetProductString(nullVariant, field, null);
                var emptyVariant = baseline.Copy(); SetProductString(emptyVariant, field, "");
                CheckProductPair(log, "product-" + field + "-null-empty", nullVariant, emptyVariant, null, false);
                CheckProductPair(log, "product-" + field + "-empty-null", emptyVariant, nullVariant, null, false);
                var unicode = baseline.Copy(); SetProductString(unicode, field, (GetProductString(baseline, field) as string ?? "") + " L1A \"escaped\" \\ Ω 雪");
                CheckProductPair(log, "product-" + field + "-unicode", baseline, unicode, null, false);
                CheckProductUnicodePairs(log, baseline, field);
            }

            foreach (string path in ProductTopLevelFields.Where(path => path != "awdHardware"))
            {
                var changed = baseline.Copy();
                if (!MutateProductPath(changed, path))
                {
                    log.Check(false, "required top-level serialized field has an explicit mutation / " + path);
                    continue;
                }
                CheckProductPair(log, "product-field-" + path, baseline, changed, null, false);
            }

            var withAwd = awdBaseline;
            if (withAwd == null)
                log.Check(false, "authored AWD hardware baseline exists for nested field matrix");
            else
            {
                var hwBase = withAwd.Copy();
                var nullHardware = hwBase.Copy(); nullHardware.awdHardware = null;
                var defaultHardware = hwBase.Copy(); defaultHardware.awdHardware = new AwdHardwareSpec();
                CheckProductPair(log, "product-awd-null-default", nullHardware, defaultHardware, true, false);

                var nullCalibration = hwBase.Copy(); nullCalibration.awdHardware.calibration = null;
                var defaultCalibration = hwBase.Copy(); defaultCalibration.awdHardware.calibration = new AwdSpec();
                CheckProductPair(log, "product-awd-calibration-null-default", nullCalibration, defaultCalibration, null, false);

                foreach (string field in AwdHardwareFields.Where(path => path != "calibration"))
                {
                    var changed = hwBase.Copy();
                    if (!MutateAwdHardware(changed.awdHardware, field)) { log.Check(false, "required AWD hardware field has an explicit mutation / " + field); continue; }
                    CheckProductPair(log, "product-awd-field-" + field, hwBase, changed, null, false);
                }
                foreach (string field in AwdCalibrationFields)
                {
                    var changed = hwBase.Copy();
                    if (changed.awdHardware.calibration == null) changed.awdHardware.calibration = new AwdSpec();
                    if (!MutateAwdCalibration(changed.awdHardware.calibration, field)) { log.Check(false, "required AWD calibration field has an explicit mutation / " + field); continue; }
                    CheckProductPair(log, "product-awd-calibration-" + field, hwBase, changed, null, false);
                }
            }

            Exception nullCapture = null;
            try { PureTyped(log, "null product definition input", () => PartProductValue.Capture(null)); }
            catch (Exception error) { nullCapture = error; }
            Exception nullOracle = null;
            try { LegacyCartCanonicalBoundaryReference.ProductCacheMiss(null); } catch (Exception error) { nullOracle = error; }
            log.Check(nullCapture != null && nullOracle != null && nullCapture.GetType() == nullOracle.GetType() && nullCapture.Message == nullOracle.Message,
                "null PartDefinition capture preserves frozen dereference exception / " + ErrorLabel(nullCapture));
            bool baselineRawStable = PureTyped(log, "recapture unchanged product baseline", () => baselineValue.RawEquals(PartProductValue.Capture(baseline)));
            log.Check(baselineRawStable && baselineOracle == LegacyCartCanonicalBoundaryReference.ProductCacheMiss(baseline),
                "product field probes leave authored baseline raw value and literal oracle unchanged");
        }

        static void CheckProductSpecialValues(CheckLog log, PartDefinition[] definitions)
        {
            var baseline = definitions.FirstOrDefault(value => value != null);
            if (baseline == null) return;
            var positiveZero = baseline.Copy(); positiveZero.boostScale = 0f;
            var negativeZero = baseline.Copy(); negativeZero.boostScale = -0f;
            CheckProductPair(log, "product-signed-zero", positiveZero, negativeZero, true, false);

            var adjacent = baseline.Copy(); adjacent.boostScale = AdjacentFloat(1f);
            var one = baseline.Copy(); one.boostScale = 1f;
            CheckProductPair(log, "product-adjacent-finite-float", one, adjacent, null, false);

            var maxFinite = baseline.Copy(); maxFinite.boostScale = float.MaxValue;
            var minFinite = baseline.Copy(); minFinite.boostScale = float.MinValue;
            CheckProductPair(log, "product-float-extremes", maxFinite, minFinite, null, false);
            var smallestPositive = baseline.Copy(); smallestPositive.boostScale = BitConverter.ToSingle(BitConverter.GetBytes(1), 0);
            var smallestNegative = baseline.Copy(); smallestNegative.boostScale = BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x80000001)), 0);
            CheckProductPair(log, "product-subnormal-sign", smallestPositive, smallestNegative, null, false);

            foreach (var edge in new[] { Tuple.Create("nan", float.NaN), Tuple.Create("positive-infinity", float.PositiveInfinity), Tuple.Create("negative-infinity", float.NegativeInfinity) })
            {
                var changed = baseline.Copy(); changed.boostScale = edge.Item2;
                CheckProductPair(log, "product-nonfinite-" + edge.Item1, baseline, changed, null);
            }
            var positiveInfinity = baseline.Copy(); positiveInfinity.boostScale = float.PositiveInfinity;
            var negativeInfinity = baseline.Copy(); negativeInfinity.boostScale = float.NegativeInfinity;
            CheckProductPair(log, "product-infinity-sign", positiveInfinity, negativeInfinity, null, false);
            int nanA = unchecked((int)0x7FC00001), nanB = unchecked((int)0x7FC00002), negativeNan = unchecked((int)0xFFC00001);
            var payloadA = baseline.Copy(); payloadA.boostScale = BitConverter.ToSingle(BitConverter.GetBytes(nanA), 0);
            var payloadB = baseline.Copy(); payloadB.boostScale = BitConverter.ToSingle(BitConverter.GetBytes(nanB), 0);
            var payloadNegative = baseline.Copy(); payloadNegative.boostScale = BitConverter.ToSingle(BitConverter.GetBytes(negativeNan), 0);
            CheckProductPair(log, "product-nan-payloads", payloadA, payloadB, null, false);
            CheckProductPair(log, "product-nan-payload-sign", payloadA, payloadNegative, null, false);

            foreach (var edge in new[] { Tuple.Create("control", "L1A \"quote\" \\ slash\nline\t\r\0"),
                Tuple.Create("supplementary", "L1A-\U0001F680"), Tuple.Create("unpaired-high", "L1A-\uD800"),
                Tuple.Create("unpaired-low", "L1A-\uDC00") })
            {
                var text = baseline.Copy(); text.name = edge.Item2;
                CheckProductPair(log, "product-string-edge-" + edge.Item1, baseline, text, null);
            }
            var highSurrogate = baseline.Copy(); highSurrogate.name = "L1A-\uD800";
            var replacement = baseline.Copy(); replacement.name = "L1A-\uFFFD";
            CheckProductPair(log, "product-string-unpaired-versus-replacement", highSurrogate, replacement, null);
        }

        static void CheckProductUnicodePairs(CheckLog log, PartDefinition baseline, string field)
        {
            TraceProductUnicodeObservations(log, baseline, field, new[] { "L1A-\uD800\U0001F680", "L1A-\uDC00\U0001F680", "L1A-\uD800A", "L1A-\uD800\uD801" },
                new[] { "L1A-\uFFFD\U0001F680", "L1A-\uFFFD\U0001F680", "L1A-\uFFFDA", "L1A-\uFFFD\uFFFD" });
            ProductStringPair(log, baseline, field, "unpaired-high", "L1A-\uD800", "L1A-\uFFFD");
            ProductStringPair(log, baseline, field, "unpaired-low", "L1A-\uDC00", "L1A-\uFFFD");
            ProductStringPair(log, baseline, field, "supplementary-control", "L1A-\U0001F680", "L1A-\uFFFD");
            ProductStringPair(log, baseline, field, "mixed-surrogate-supplementary", "L1A-\uD800\U0001F680", "L1A-\uFFFD\U0001F680", false);
            CheckProductMalformedControls(log, baseline, field);
        }

        static void CheckProductMalformedControls(CheckLog log, PartDefinition baseline, string field)
        {
            var controls = new[]
            {
                Tuple.Create("high-high-low-vs-two-replacements", "L1A-\uD800\uD800\uDC00", "L1A-\uFFFD\uFFFD", true),
                Tuple.Create("high-ascii-vs-replacement", "L1A-\uD800A", "L1A-\uFFFD", true),
                Tuple.Create("two-high-vs-replacement", "L1A-\uD800\uD800", "L1A-\uFFFD", true),
                Tuple.Create("high-ascii-negative-control", "L1A-\uD800A", "L1A-\uFFFDA", false),
                Tuple.Create("low-valid-pair", "L1A-\uDC00\U0001F680", "L1A-\uFFFD\U0001F680", true),
                Tuple.Create("valid-pair-high", "L1A-\U0001F680\uD800", "L1A-\U0001F680\uFFFD", true)
            };
            foreach (var control in controls)
            {
                var left = baseline.Copy(); SetProductString(left, field, control.Item2);
                var right = baseline.Copy(); SetProductString(right, field, control.Item3);
                CheckProductPair(log, "product-" + field + "-legacy-decoder-" + control.Item1, left, right, control.Item4, false);
            }
        }

        static void TraceProductUnicodeObservations(CheckLog log, PartDefinition baseline, string field, string[] inputs, string[] replacementControls)
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                var input = baseline.Copy(); SetProductString(input, field, inputs[i]);
                var control = baseline.Copy(); SetProductString(control, field, replacementControls[i]);
                TraceJsonField(log, "observation-only product field=" + field + " probe=" + i, inputs[i],
                    LegacyCartCanonicalBoundaryReference.ProductCacheMiss(input), field);
                TraceJsonField(log, "observation-only product replacement-control field=" + field + " probe=" + i, replacementControls[i],
                    LegacyCartCanonicalBoundaryReference.ProductCacheMiss(control), field);
            }
        }

        static void ProductStringPair(CheckLog log, PartDefinition baseline, string field, string label, string leftText, string rightText, bool? expectedOracle = null)
        {
            var left = baseline.Copy(); SetProductString(left, field, leftText);
            var right = baseline.Copy(); SetProductString(right, field, rightText);
            CheckProductPair(log, "product-" + field + "-" + label, left, right, expectedOracle, false);
            CheckProductPair(log, "product-" + field + "-" + label + "-reverse", right, left, expectedOracle, false);
        }

        static float AdjacentFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return value;
            if (value == 0f) return BitConverter.ToSingle(BitConverter.GetBytes(1), 0);
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            bits += value > 0 ? 1 : -1;
            return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        }

        static void CheckProductPair(CheckLog log, string label, PartDefinition left, PartDefinition right, bool? expected, bool? expectedRaw = null)
        {
            var leftHardware = left.awdHardware; var leftCalibration = leftHardware?.calibration;
            var rightHardware = right.awdHardware; var rightCalibration = rightHardware?.calibration;
            var typed = PureTyped(log, "product typed capture/equality/hash / " + label, () =>
            {
                var leftBefore = PartProductValue.Capture(left);
                var rightBefore = PartProductValue.Capture(right);
                var leftValue = PartProductValue.Capture(left);
                var rightValue = PartProductValue.Capture(right);
                bool leftSupported = leftValue.TryGetProductHashCode(out int leftHash);
                bool rightSupported = rightValue.TryGetProductHashCode(out int rightHash);
                bool comparisonSupported = leftValue.TryProductEquivalent(rightValue, out bool typedEqual);
                return new ProductPairFacts
                {
                    LeftBefore = leftBefore, RightBefore = rightBefore, LeftValue = leftValue, RightValue = rightValue,
                    LeftSupported = leftSupported, RightSupported = rightSupported, ComparisonSupported = comparisonSupported,
                    TypedEqual = typedEqual, LeftHash = leftHash, RightHash = rightHash
                };
            });
            var leftOracle = CaptureFrozenString(() => LegacyCartCanonicalBoundaryReference.ProductCacheMiss(left));
            var rightOracle = CaptureFrozenString(() => LegacyCartCanonicalBoundaryReference.ProductCacheMiss(right));
            bool oracleComparable = leftOracle.Error == null && rightOracle.Error == null;
            bool oracleEqual = oracleComparable && string.Equals(leftOracle.Value, rightOracle.Value, StringComparison.Ordinal);
            log.Check(typed.LeftSupported && typed.RightSupported && typed.ComparisonSupported,
                "product pair returns supported value/hash for both operands / " + label);
            if (oracleComparable && typed.ComparisonSupported)
                log.Check(typed.TypedEqual == oracleEqual, "typed product equivalence matches exact frozen key result / " + label + ";oracleEqual=" + oracleEqual + ";typed=" + typed.TypedEqual);
            else
                log.Check(false, "required product oracle values are available for exact equivalence comparison / " + label + ";left=" + ErrorLabel(leftOracle.Error) + ";right=" + ErrorLabel(rightOracle.Error));
            if (expected.HasValue)
                log.Check(oracleComparable && oracleEqual == expected.Value, "product frozen oracle establishes expected finite outcome / " + label + ";actual=" + oracleEqual);
            if (expectedRaw.HasValue)
            {
                bool rawEqual = PureTyped(log, "product raw snapshot comparison / " + label, () => typed.LeftBefore.RawEquals(typed.RightBefore));
                log.Check(rawEqual == expectedRaw.Value, "product raw captures preserve expected raw distinction / " + label);
            }
            if (typed.TypedEqual && typed.LeftSupported && typed.RightSupported)
                log.Check(typed.LeftHash == typed.RightHash, "equal supported products have identical supported hashes / " + label);
            bool stable = PureTyped(log, "recapture product pair source / " + label,
                () => typed.LeftBefore.RawEquals(PartProductValue.Capture(left)) && typed.RightBefore.RawEquals(PartProductValue.Capture(right)));
            log.Check(stable && ReferenceEquals(left.awdHardware, leftHardware) && ReferenceEquals(left.awdHardware?.calibration, leftCalibration) &&
                ReferenceEquals(right.awdHardware, rightHardware) && ReferenceEquals(right.awdHardware?.calibration, rightCalibration),
                "product capture and compare preserve source raw values / " + label);
        }

        static bool ProductSupported(CheckLog log, string label, PartProductValue value)
        {
            var result = PureTyped(log, "product self/null comparison / " + label, () =>
            {
                bool equalitySupported = value.TryProductEquivalent(value, out bool selfEqual);
                bool hashSupported = value.TryGetProductHashCode(out _);
                bool nullSupported = value.TryProductEquivalent(null, out bool nullEqual);
                return Tuple.Create(equalitySupported, selfEqual, hashSupported, nullSupported, nullEqual);
            });
            bool equalitySupported = result.Item1;
            bool hashSupported = result.Item3;
            log.Check(equalitySupported && result.Item2 && hashSupported, "product self comparison/hash supported / " + label);
            log.Check(result.Item4 && !result.Item5, "product comparison to null is supported and not equal / " + label);
            return equalitySupported && hashSupported;
        }

        static void CheckProductPartition(CheckLog log, List<PartDefinition> definitions)
        {
            var values = PureTyped(log, "capture every fresh catalogue and alias product value", () => definitions.Select(PartProductValue.Capture).ToArray());
            var oracleResults = definitions.Select(definition => CaptureFrozenString(() => LegacyCartCanonicalBoundaryReference.ProductCacheMiss(definition))).ToArray();
            var entries = definitions.Select((definition, index) => new ProductEntry
            { Value = values[index], Oracle = oracleResults[index].Value, OracleError = oracleResults[index].Error }).ToArray();
            foreach (var entry in entries.Where(value => value.OracleError != null))
                log.Check(false, "required catalogue product has a supported frozen oracle / " + ErrorLabel(entry.OracleError));

            var partition = PureTyped(log, "build exact collision-safe product partitions", () =>
            {
                bool supported = true;
                var hashes = new int[entries.Length];
                var oracleGroups = new Dictionary<string, int>(StringComparer.Ordinal);
                var typedGroupsByHash = new Dictionary<int, List<Tuple<int, int>>>();
                var typedGroupForEntry = Enumerable.Repeat(-1, entries.Length).ToArray();
                int nextOracleGroup = 0, nextTypedGroup = 0;

                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (entry.OracleError != null || entry.Oracle == null) { supported = false; continue; }
                    if (!oracleGroups.ContainsKey(entry.Oracle)) oracleGroups.Add(entry.Oracle, nextOracleGroup++);
                    if (!entry.Value.TryGetProductHashCode(out hashes[i])) { supported = false; continue; }
                    if (!entry.Value.TryProductEquivalent(entry.Value, out bool selfEqual) || !selfEqual) { supported = false; continue; }
                    if (!typedGroupsByHash.TryGetValue(hashes[i], out var bucket))
                    { bucket = new List<Tuple<int, int>>(); typedGroupsByHash.Add(hashes[i], bucket); }
                    int selected = -1;
                    foreach (var representative in bucket)
                    {
                        if (!entry.Value.TryProductEquivalent(entries[representative.Item2].Value, out bool equal))
                        { supported = false; continue; }
                        if (!equal) continue;
                        selected = representative.Item1;
                        supported &= string.Equals(entry.Oracle, entries[representative.Item2].Oracle, StringComparison.Ordinal);
                        break;
                    }
                    if (selected < 0)
                    {
                        selected = nextTypedGroup++;
                        bucket.Add(Tuple.Create(selected, i));
                    }
                    typedGroupForEntry[i] = selected;
                }

                var oracleToTyped = new Dictionary<int, int>();
                var typedToOracle = new Dictionary<int, int>();
                bool bijective = supported && nextTypedGroup == nextOracleGroup;
                for (int i = 0; i < entries.Length; i++)
                {
                    if (typedGroupForEntry[i] < 0 || entries[i].OracleError != null || entries[i].Oracle == null) { bijective = false; continue; }
                    int og = oracleGroups[entries[i].Oracle], tg = typedGroupForEntry[i];
                    if (oracleToTyped.TryGetValue(og, out int oldTyped) && oldTyped != tg) bijective = false;
                    else oracleToTyped[og] = tg;
                    if (typedToOracle.TryGetValue(tg, out int oldOracle) && oldOracle != og) bijective = false;
                    else typedToOracle[tg] = og;
                }
                return Tuple.Create(bijective, nextOracleGroup, nextTypedGroup, hashes);
            });
            log.Check(partition.Item1, "full actual fresh-catalogue plus three-alias equivalence partitions are bijective / values=" + entries.Length +
                ";oracleClasses=" + partition.Item2 + ";typedClasses=" + partition.Item3);
            log.Note("partition|actual-count=" + definitions.Count + "|oracle-class-count=" + partition.Item2 + "|typed-class-count=" + partition.Item3 +
                "|status=" + (partition.Item1 ? "SUPPORTED-PARITY" : "FAIL") + "|raw-key-bodies=omitted");
        }

        static void CheckProductCollisionHandling(CheckLog log, List<PartDefinition> definitions)
        {
            var source = definitions.FirstOrDefault(value => value != null);
            if (source == null) { log.Check(false, "real product value available for collision test"); return; }
            var alias = source.Copy(); alias.id = (alias.id ?? "") + "/forced-collision-alias";
            var different = source.Copy(); different.name = (different.name ?? "") + "-forced-collision-different";
            var result = PureTyped(log, "forced real product hash collision", () =>
            {
                var samples = new[] { PartProductValue.Capture(source), PartProductValue.Capture(alias), PartProductValue.Capture(different) };
                var comparer = new CollisionComparer(); var set = new HashSet<PartProductValue>(comparer);
                foreach (var value in samples) set.Add(value);
                bool hashesSupported = samples.All(value => value.TryGetProductHashCode(out _));
                return Tuple.Create(samples, comparer.Unsupported, set.Count, hashesSupported);
            });
            log.Check(!result.Item2 && result.Item3 == 2 && result.Item4,
                "forced hash collisions resolve ID alias and changed product through exact real ProductEquivalent comparison");
        }

        static void CheckProductOwnership(CheckLog log, PartDefinition baseline)
        {
            if (baseline == null) return;
            var copy = baseline.Copy();
            var captured = PureTyped(log, "product snapshot ownership capture", () => PartProductValue.Capture(copy));
            var frozenBefore = LegacyCartCanonicalBoundaryReference.ProductCacheMiss(copy);
            copy.name = (copy.name ?? "") + "-after-capture";
            if (copy.awdHardware != null)
            {
                if (copy.awdHardware.calibration == null) copy.awdHardware.calibration = new AwdSpec();
                else copy.awdHardware.calibration.frontBias = AdjacentFloat(copy.awdHardware.calibration.frontBias);
            }
            var afterMutation = PureTyped(log, "product snapshot after source mutation", () =>
            {
                var snapshot = PartProductValue.Capture(copy);
                bool supported = captured.TryProductEquivalent(snapshot, out bool equal);
                bool rawEqual = captured.RawEquals(snapshot);
                return Tuple.Create(snapshot, supported, equal, rawEqual);
            });
            log.Check(!afterMutation.Item4 && afterMutation.Item2 && !afterMutation.Item3,
                "product snapshot is detached from source scalar/nested changes");
            log.Check(frozenBefore != LegacyCartCanonicalBoundaryReference.ProductCacheMiss(copy),
                "source mutation after capture changes only the fresh oracle value");
        }

        static void SetProductString(PartDefinition value, string field, string next)
        {
            switch (field)
            {
                case "name": value.name = next; break;
                case "fitment": value.fitment = next; break;
                case "brand": value.brand = next; break;
                case "quality": value.quality = next; break;
                case "followerInterface": value.followerInterface = next; break;
                case "description": value.description = next; break;
                case "provenance": value.provenance = next; break;
                default: throw new ArgumentOutOfRangeException(nameof(field));
            }
        }

        static bool MutateProductPath(PartDefinition part, string path)
        {
            switch (path)
            {
                case "name": case "fitment": case "brand": case "quality": case "followerInterface": case "description": case "provenance":
                    SetProductString(part, path, (string)GetProductString(part, path) + "-l1a"); return true;
                case "fuelKind": part.fuelKind = (FuelKind)unchecked((int)part.fuelKind + 1); return true;
                case "pistonCrown": part.pistonCrown = (PistonCrown)unchecked((int)part.pistonCrown + 1); return true;
                case "category": part.category = (PartCategory)unchecked((int)part.category + 1); return true;
                case "clutchEnvironment": part.clutchEnvironment = (ClutchEnvironment)unchecked((int)part.clutchEnvironment + 1); return true;
                case "clutchMaterial": part.clutchMaterial = (ClutchMaterial)unchecked((int)part.clutchMaterial + 1); return true;
                case "clutchActuation": part.clutchActuation = (ClutchActuation)unchecked((int)part.clutchActuation + 1); return true;
                case "tuning": part.tuning = (PartTuningOptions)unchecked((int)part.tuning ^ 1); return true;
                case "valve": part.valve = (BlowOffType)unchecked((int)part.valve + 1); return true;
                case "fuelDelivery": part.fuelDelivery = (FuelDeliveryHardware)unchecked((int)part.fuelDelivery + 1); return true;
                case "induction": part.induction = (EngineUpgradeInduction)unchecked((int)part.induction + 1); return true;
                case "transmission": part.transmission = (TransmissionUpgrade)unchecked((int)part.transmission + 1); return true;
                case "manualLayout": part.manualLayout = (ManualGearboxLayout)unchecked((int)part.manualLayout + 1); return true;
                case "differential": part.differential = (DifferentialKind)unchecked((int)part.differential + 1); return true;
                case "supportsInjection": part.supportsInjection = !part.supportsInjection; return true;
                case "supportsDiesel": part.supportsDiesel = !part.supportsDiesel; return true;
                case "camTimingAdjustable": part.camTimingAdjustable = !part.camTimingAdjustable; return true;
                case "manualDownshiftInterlock": part.manualDownshiftInterlock = !part.manualDownshiftInterlock; return true;
                case "turboUnits": part.turboUnits++; return true;
                case "superchargerUnits": part.superchargerUnits++; return true;
                case "plugHeatRange": part.plugHeatRange++; return true;
                case "price": part.price++; return true;
                case "stage": part.stage++; return true;
                case "reputationRequired": part.reputationRequired++; return true;
                case "clutchPlateCount": part.clutchPlateCount++; return true;
                case "injectionControlMetadataVersion": part.injectionControlMetadataVersion++; return true;
                case "controllerCylinderCapacity": part.controllerCylinderCapacity++; return true;
            }
            if (IsProductFloat(path))
            {
                float oldValue = GetProductFloat(part, path);
                float changed = AdjacentFloat(oldValue);
                if (changed.Equals(oldValue)) changed = 0f.Equals(oldValue) ? 1f : 0f;
                return SetProductFloat(part, path, changed);
            }
            return false;
        }

        static object GetProductString(PartDefinition part, string path)
        {
            switch (path)
            {
                case "name": return part.name; case "fitment": return part.fitment; case "brand": return part.brand;
                case "quality": return part.quality; case "followerInterface": return part.followerInterface;
                case "description": return part.description; case "provenance": return part.provenance;
                default: throw new ArgumentOutOfRangeException(nameof(path));
            }
        }

        static bool IsProductFloat(string path) => ProductFloatFields.Contains(path, StringComparer.Ordinal);
        static readonly string[] ProductFloatFields =
        {
            "crownVolumeCc", "valveReliefMm", "springMaxLiftMm", "boreMm", "power", "grip", "massScale", "shiftScale", "capacityScale", "boostScale",
            "centerRatioMin", "centerRatioMax", "rpmLimit", "torqueLimitNm", "massKg", "inertiaKgM2", "positionFromFrontAxleM", "heightM",
            "clutchOilCapacityLitres", "clutchEngagementRpm", "turboSize", "fixedBoost", "boostMin", "boostMax", "fixedPulley", "pulleyMin", "pulleyMax",
            "fixedFinalDrive", "finalDriveMin", "finalDriveMax", "injectionIdleControl", "injectionMixtureVariation", "camLiftMm", "camDurationDegrees",
            "camOverlapDegrees", "camLobeSeparation", "camTorqueBias", "valvetrainRpm", "fluidCapacityLitres", "fluidMinimumLitres", "baffling",
            "pumpFlowScale", "capPressureBar", "sequentialShiftScale", "thermalLimitC", "heatCapacityJPerC", "coolingWattsPerC", "wearEnergyJ",
            "overloadSeconds", "tireLifeDistanceM", "tireOptimalC", "tireHeatFraction", "torqueBiasRatio", "preloadNm", "engineHeatTransferScale", "maxPowerHp"
        };

        static float GetProductFloat(PartDefinition p, string path)
        {
            switch (path)
            {
                case "crownVolumeCc": return p.crownVolumeCc; case "valveReliefMm": return p.valveReliefMm; case "springMaxLiftMm": return p.springMaxLiftMm;
                case "boreMm": return p.boreMm; case "power": return p.power; case "grip": return p.grip; case "massScale": return p.massScale;
                case "shiftScale": return p.shiftScale; case "capacityScale": return p.capacityScale; case "boostScale": return p.boostScale;
                case "centerRatioMin": return p.centerRatioMin; case "centerRatioMax": return p.centerRatioMax; case "rpmLimit": return p.rpmLimit;
                case "torqueLimitNm": return p.torqueLimitNm; case "massKg": return p.massKg; case "inertiaKgM2": return p.inertiaKgM2;
                case "positionFromFrontAxleM": return p.positionFromFrontAxleM; case "heightM": return p.heightM; case "clutchOilCapacityLitres": return p.clutchOilCapacityLitres;
                case "clutchEngagementRpm": return p.clutchEngagementRpm; case "turboSize": return p.turboSize; case "fixedBoost": return p.fixedBoost;
                case "boostMin": return p.boostMin; case "boostMax": return p.boostMax; case "fixedPulley": return p.fixedPulley; case "pulleyMin": return p.pulleyMin;
                case "pulleyMax": return p.pulleyMax; case "fixedFinalDrive": return p.fixedFinalDrive; case "finalDriveMin": return p.finalDriveMin;
                case "finalDriveMax": return p.finalDriveMax; case "injectionIdleControl": return p.injectionIdleControl; case "injectionMixtureVariation": return p.injectionMixtureVariation;
                case "camLiftMm": return p.camLiftMm; case "camDurationDegrees": return p.camDurationDegrees; case "camOverlapDegrees": return p.camOverlapDegrees;
                case "camLobeSeparation": return p.camLobeSeparation; case "camTorqueBias": return p.camTorqueBias; case "valvetrainRpm": return p.valvetrainRpm;
                case "fluidCapacityLitres": return p.fluidCapacityLitres; case "fluidMinimumLitres": return p.fluidMinimumLitres; case "baffling": return p.baffling;
                case "pumpFlowScale": return p.pumpFlowScale; case "capPressureBar": return p.capPressureBar; case "sequentialShiftScale": return p.sequentialShiftScale;
                case "thermalLimitC": return p.thermalLimitC; case "heatCapacityJPerC": return p.heatCapacityJPerC; case "coolingWattsPerC": return p.coolingWattsPerC;
                case "wearEnergyJ": return p.wearEnergyJ; case "overloadSeconds": return p.overloadSeconds; case "tireLifeDistanceM": return p.tireLifeDistanceM;
                case "tireOptimalC": return p.tireOptimalC; case "tireHeatFraction": return p.tireHeatFraction; case "torqueBiasRatio": return p.torqueBiasRatio;
                case "preloadNm": return p.preloadNm; case "engineHeatTransferScale": return p.engineHeatTransferScale; case "maxPowerHp": return p.maxPowerHp;
                default: throw new ArgumentOutOfRangeException(nameof(path));
            }
        }

        static bool SetProductFloat(PartDefinition p, string path, float value)
        {
            switch (path)
            {
                case "crownVolumeCc": p.crownVolumeCc = value; break; case "valveReliefMm": p.valveReliefMm = value; break; case "springMaxLiftMm": p.springMaxLiftMm = value; break;
                case "boreMm": p.boreMm = value; break; case "power": p.power = value; break; case "grip": p.grip = value; break; case "massScale": p.massScale = value; break;
                case "shiftScale": p.shiftScale = value; break; case "capacityScale": p.capacityScale = value; break; case "boostScale": p.boostScale = value; break;
                case "centerRatioMin": p.centerRatioMin = value; break; case "centerRatioMax": p.centerRatioMax = value; break; case "rpmLimit": p.rpmLimit = value; break;
                case "torqueLimitNm": p.torqueLimitNm = value; break; case "massKg": p.massKg = value; break; case "inertiaKgM2": p.inertiaKgM2 = value; break;
                case "positionFromFrontAxleM": p.positionFromFrontAxleM = value; break; case "heightM": p.heightM = value; break; case "clutchOilCapacityLitres": p.clutchOilCapacityLitres = value; break;
                case "clutchEngagementRpm": p.clutchEngagementRpm = value; break; case "turboSize": p.turboSize = value; break; case "fixedBoost": p.fixedBoost = value; break;
                case "boostMin": p.boostMin = value; break; case "boostMax": p.boostMax = value; break; case "fixedPulley": p.fixedPulley = value; break; case "pulleyMin": p.pulleyMin = value; break;
                case "pulleyMax": p.pulleyMax = value; break; case "fixedFinalDrive": p.fixedFinalDrive = value; break; case "finalDriveMin": p.finalDriveMin = value; break;
                case "finalDriveMax": p.finalDriveMax = value; break; case "injectionIdleControl": p.injectionIdleControl = value; break; case "injectionMixtureVariation": p.injectionMixtureVariation = value; break;
                case "camLiftMm": p.camLiftMm = value; break; case "camDurationDegrees": p.camDurationDegrees = value; break; case "camOverlapDegrees": p.camOverlapDegrees = value; break;
                case "camLobeSeparation": p.camLobeSeparation = value; break; case "camTorqueBias": p.camTorqueBias = value; break; case "valvetrainRpm": p.valvetrainRpm = value; break;
                case "fluidCapacityLitres": p.fluidCapacityLitres = value; break; case "fluidMinimumLitres": p.fluidMinimumLitres = value; break; case "baffling": p.baffling = value; break;
                case "pumpFlowScale": p.pumpFlowScale = value; break; case "capPressureBar": p.capPressureBar = value; break; case "sequentialShiftScale": p.sequentialShiftScale = value; break;
                case "thermalLimitC": p.thermalLimitC = value; break; case "heatCapacityJPerC": p.heatCapacityJPerC = value; break; case "coolingWattsPerC": p.coolingWattsPerC = value; break;
                case "wearEnergyJ": p.wearEnergyJ = value; break; case "overloadSeconds": p.overloadSeconds = value; break; case "tireLifeDistanceM": p.tireLifeDistanceM = value; break;
                case "tireOptimalC": p.tireOptimalC = value; break; case "tireHeatFraction": p.tireHeatFraction = value; break; case "torqueBiasRatio": p.torqueBiasRatio = value; break;
                case "preloadNm": p.preloadNm = value; break; case "engineHeatTransferScale": p.engineHeatTransferScale = value; break; case "maxPowerHp": p.maxPowerHp = value; break;
                default: return false;
            }
            return true;
        }

        static bool MutateAwdHardware(AwdHardwareSpec h, string path)
        {
            switch (path)
            {
                case "version": h.version++; return true; case "modes": h.modes ^= 1; return true;
                case "integratedCenter": h.integratedCenter = !h.integratedCenter; return true;
                case "adjustments": h.adjustments = (AwdAdjustments)unchecked((int)h.adjustments ^ 1); return true;
                case "responseMin": h.responseMin = AdjacentFloat(h.responseMin); return true;
                case "responseMax": h.responseMax = AdjacentFloat(h.responseMax); return true;
                default: return false;
            }
        }

        static bool MutateAwdCalibration(AwdSpec a, string path)
        {
            switch (path)
            {
                case "mode": a.mode = (AwdMode)unchecked((int)a.mode + 1); return true;
                case "centerSpeedRatio": a.centerSpeedRatio = AdjacentFloat(a.centerSpeedRatio); return true;
                case "torqueBiasRatio": a.torqueBiasRatio = AdjacentFloat(a.torqueBiasRatio); return true;
                case "centerPreloadNm": a.centerPreloadNm = AdjacentFloat(a.centerPreloadNm); return true;
                case "frontBias": a.frontBias = AdjacentFloat(a.frontBias); return true;
                case "minimumFront": a.minimumFront = AdjacentFloat(a.minimumFront); return true;
                case "maximumFront": a.maximumFront = AdjacentFloat(a.maximumFront); return true;
                case "primaryFront": a.primaryFront = !a.primaryFront; return true;
                case "isolateFailedAxle": a.isolateFailedAxle = !a.isolateFailedAxle; return true;
                case "maximumSecondary": a.maximumSecondary = AdjacentFloat(a.maximumSecondary); return true;
                case "responseSeconds": a.responseSeconds = AdjacentFloat(a.responseSeconds); return true;
                case "slipThreshold": a.slipThreshold = AdjacentFloat(a.slipThreshold); return true;
                case "preload": a.preload = AdjacentFloat(a.preload); return true;
                case "freeShaftInertiaKgM2": a.freeShaftInertiaKgM2 = AdjacentFloat(a.freeShaftInertiaKgM2); return true;
                case "freeShaftDragNm": a.freeShaftDragNm = AdjacentFloat(a.freeShaftDragNm); return true;
                case "freeShaftViscousDrag": a.freeShaftViscousDrag = AdjacentFloat(a.freeShaftViscousDrag); return true;
                default: return false;
            }
        }

        static Result<string> CaptureFrozenString(Func<string> action)
        {
            try { return new Result<string> { Value = action() }; }
            catch (Exception error) { return new Result<string> { Error = error }; }
        }

        static T PureTyped<T>(CheckLog log, string label, Func<T> operation)
        {
            long json = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            long canonicalRequests = MarketCatalogLayout.CanonicalKeyRequestCount;
            long canonicalJson = MarketCatalogLayout.CanonicalJsonEncodeCount;
            var random = UnityEngine.Random.state;
            bool entropyCapture = RuntimeEntropy.Capture;
            DateTime entropyClock = entropyCapture ? RuntimeEntropy.UtcNow : default(DateTime);
            var observation = RuntimeEntropy.BeginIdentityObservation(64);
            T value = default(T);
            Exception error = null;
            try { value = operation(); }
            catch (Exception caught) { error = caught; }
            finally { observation.Dispose(); }

            bool pure = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount == json &&
                MarketCatalogLayout.CanonicalKeyRequestCount == canonicalRequests &&
                MarketCatalogLayout.CanonicalJsonEncodeCount == canonicalJson &&
                random.Equals(UnityEngine.Random.state) && observation.Count == 0 && observation.Closed && !observation.Overflowed &&
                RuntimeEntropy.Capture == entropyCapture && (!entropyCapture ||
                    (RuntimeEntropy.UtcNow.Ticks == entropyClock.Ticks && RuntimeEntropy.UtcNow.Kind == entropyClock.Kind));
            log.Check(pure, "typed capture/equality preserves JSON/key counters, ID observation, RNG and entropy state / " + label);
            if (error != null) throw error;
            return value;
        }

        static string ErrorLabel(Exception error) => error == null ? "<none>" : error.GetType().FullName + ":" + L1Safe(error.Message);
        static string L1Safe(string value)
        {
            value = (value ?? "<null>").Replace("\r", "\\r").Replace("\n", "\\n").Replace("|", "/");
            return value.Length <= 180 ? value : value.Substring(0, 180);
        }
    }
}
#endif
