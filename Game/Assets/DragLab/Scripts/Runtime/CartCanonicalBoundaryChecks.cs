#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DragLab.Domain;
using DragLab.Infrastructure;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Finite Editor observations of legacy cart JSON/key and stock lookup boundaries.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint CanonicalBoundarySeed = 20261031u;
        static readonly string[] CartLineSerializedFields = { "definitionId", "quantity", "install", "preparedBlock",
            "preparationOwner", "action", "assetId", "role", "destinationId", "destination", "positions" };

        sealed class AliasFixture
        {
            internal PlayerProfile Profile;
            internal OwnedCar Car;
            internal PartDefinition Source, AliasA, AliasB, UnknownAlias;
            internal NewPartStock SourceRow, FirstRow, SecondRow;
        }

        sealed class BoundaryOutcome
        {
            internal object Value;
            internal Exception Error;
            internal string Text => Error == null ? (Value == null ? "<null>" : Value.ToString()) :
                Error.GetType().FullName + " | " + Error.Message;
        }

        sealed class SignatureVector
        {
            internal string Name;
            internal IEnumerable<PartCartLine> Stored;
            internal IEnumerable<PartCartLine> Query;
        }

        sealed class RowImage
        {
            internal PartCartLine[] Rows;
            internal PartCartLine[] References;
            internal List<string>[] PositionReferences;
            internal string[] Facts;

            internal static RowImage Capture(IEnumerable<PartCartLine> rows)
            {
                if (rows == null) return new RowImage { Rows = null };
                var array = rows.ToArray();
                return new RowImage
                {
                    Rows = array,
                    References = array.ToArray(),
                    PositionReferences = array.Select(row => row?.positions).ToArray(),
                    Facts = array.Select(RowFacts).ToArray()
                };
            }

            internal bool Unchanged(IEnumerable<PartCartLine> rows)
            {
                if (Rows == null) return rows == null;
                if (rows == null) return false;
                var current = rows.ToArray();
                if (current.Length != Rows.Length) return false;
                for (int i = 0; i < current.Length; i++)
                    if (!ReferenceEquals(current[i], References[i]) ||
                        !ReferenceEquals(current[i]?.positions, PositionReferences[i]) ||
                        RowFacts(current[i]) != Facts[i]) return false;
                return true;
            }
        }

        sealed class MarketImage
        {
            internal List<NewPartStock> RowsRef;
            internal NewPartStock[] RowRefs;
            internal List<string>[] DefinitionRefs;
            internal string[] RowFacts;
            internal Dictionary<string, NewPartStock> IndexRef;
            internal string IndexFacts;
            internal int Generation;

            internal static MarketImage Capture(PlayerProfile profile)
            {
                var market = profile.inventory.market;
                var rows = market.parts;
                return new MarketImage
                {
                    RowsRef = rows,
                    RowRefs = rows.ToArray(),
                    DefinitionRefs = rows.Select(row => row?.definitions).ToArray(),
                    RowFacts = rows.Select(StockFacts).ToArray(),
                    IndexRef = market.index,
                    IndexFacts = IndexFactsFor(market.index, rows),
                    Generation = market.generation
                };
            }

            internal bool SameRows(PlayerProfile profile)
            {
                var rows = profile.inventory.market.parts;
                if (!ReferenceEquals(rows, RowsRef) || rows.Count != RowRefs.Length) return false;
                for (int i = 0; i < rows.Count; i++)
                    if (!ReferenceEquals(rows[i], RowRefs[i]) ||
                        !ReferenceEquals(rows[i]?.definitions, DefinitionRefs[i]) ||
                        StockFacts(rows[i]) != RowFacts[i]) return false;
                return true;
            }

            internal bool SameIndex(PlayerProfile profile)
                => ReferenceEquals(profile.inventory.market.index, IndexRef) &&
                   IndexFactsFor(profile.inventory.market.index, profile.inventory.market.parts) == IndexFacts &&
                   profile.inventory.market.generation == Generation;
        }

        sealed class DefinitionImage
        {
            internal List<PartDefinition> PartsRef;
            internal PartDefinition[] OrderedReferences;
            internal PartDefinition[] References;
            internal string[] Ids;
            internal string[] KeyBodies;
            internal AwdHardwareSpec[] AwdReferences;
            internal AwdSpec[] CalibrationReferences;

            internal static DefinitionImage Capture(PlayerProfile profile, string extraId = null)
            {
                var catalog = profile.catalog;
                var ids = catalog.parts.Where(definition => definition != null &&
                        (profile.inventory.market.parts.SelectMany(row => row?.definitions ?? new List<string>())
                            .Contains(definition.id) || definition.id == extraId))
                    .Select(definition => definition.id).Distinct(StringComparer.Ordinal).ToArray();
                var references = ids.Select(id => catalog.parts.FirstOrDefault(definition => definition != null && definition.id == id)).ToArray();
                return new DefinitionImage
                {
                    PartsRef = catalog.parts,
                    OrderedReferences = catalog.parts.ToArray(),
                    References = references,
                    Ids = ids,
                    KeyBodies = references.Select(definition => LegacyCartCanonicalBoundaryReference.ProductCacheMiss(definition)).ToArray(),
                    AwdReferences = references.Select(definition => definition?.awdHardware).ToArray(),
                    CalibrationReferences = references.Select(definition => definition?.awdHardware?.calibration).ToArray()
                };
            }

            internal bool Unchanged(PlayerProfile profile)
            {
                if (!ReferenceEquals(profile.catalog.parts, PartsRef) || profile.catalog.parts.Count != OrderedReferences.Length) return false;
                for (int index = 0; index < OrderedReferences.Length; index++)
                    if (!ReferenceEquals(profile.catalog.parts[index], OrderedReferences[index])) return false;
                for (int i = 0; i < References.Length; i++)
                {
                    var current = profile.catalog.parts.FirstOrDefault(definition => definition != null && definition.id == Ids[i]);
                    if (!ReferenceEquals(current, References[i]) ||
                        LegacyCartCanonicalBoundaryReference.ProductCacheMiss(current) != KeyBodies[i] ||
                        !ReferenceEquals(current?.awdHardware, AwdReferences[i]) ||
                        !ReferenceEquals(current?.awdHardware?.calibration, CalibrationReferences[i])) return false;
                }
                return true;
            }
        }

        public static void RunCanonicalBoundaries(Action<bool, string> check, string outputFolder)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("An isolated report folder is required.", nameof(outputFolder));
            Directory.CreateDirectory(outputFolder);
            var log = new StringBuilder(); int assertions = 0, failures = 0;
            Action<bool, string> record = (ok, label) =>
            {
                assertions++; if (!ok) failures++;
                check(ok, "cart-canonical-boundary/" + L0Safe(label));
                log.AppendLine("ASSERT|" + (ok ? "PASS" : "FAIL") + "|" + L0Safe(label));
            };
            log.AppendLine("S06D2B1L0_CART_CANONICAL_BOUNDARIES");
            log.AppendLine("mode=unmodified-legacy-observation;unity=" + Application.unityVersion);
            log.AppendLine("CART_LINE_SCHEMA|serializedPublicFields=" + CartLineSerializedFields.Length +
                "|names=" + string.Join(",", CartLineSerializedFields));
            try
            {
                record(CartLineSerializedFields.Length == 11, "literal cart-line schema has exactly the current 11 serialized public fields");
                ObserveProductKeys(record, log);
                ObserveResolverRoutes(record, log);
                ObserveCartSignatures(record, log);
            }
            catch (Exception error)
            {
                record(false, "runner exception=" + error.GetType().FullName + " | " + error.Message);
                log.AppendLine("RUNNER_EXCEPTION|" + L0Safe(error.GetType().FullName) + "|" + L0Safe(error.Message));
            }
            finally
            {
                log.AppendLine("SUMMARY|assertions=" + assertions + ";failures=" + failures);
                File.WriteAllText(Path.Combine(outputFolder, "S06D2B1L0_CART_CANONICAL_BOUNDARIES.txt"),
                    log.ToString(), new UTF8Encoding(false));
            }
        }

        static void ObserveProductKeys(Action<bool, string> check, StringBuilder log)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile, out string carId, CanonicalBoundarySeed);
            var car = profile.cars.Find(item => item != null && item.instanceId == carId);
            var source = profile.catalog.parts.FirstOrDefault(definition => definition != null && definition.price > 0 &&
                profile.inventory.market.parts.Any(row => row != null && row.definitions != null && row.definitions.Contains(definition.id)));
            if (source == null || car == null)
            { Open(check, log, "product-key-authored-stocked-source", "normalized actual profile lacks a stocked authored definition or selected car"); return; }

            string sourceKeyBefore = LegacyCartCanonicalBoundaryReference.ProductCacheMiss(source);
            var sourceAwd = source.awdHardware;
            var sourceCalibration = sourceAwd?.calibration;
            CheckKeyParity(check, log, "actual-authored-source", source);
            var idOnly = source.Copy(); idOnly.id = "l0-id-only-alias";
            CheckKeyParity(check, log, "id-only-variation", idOnly);
            ObserveKeyRelation(log, "id-only-variation", source, idOnly);
            check(LegacyCartCanonicalBoundaryReference.ProductCacheMiss(source) ==
                LegacyCartCanonicalBoundaryReference.ProductCacheMiss(idOnly),
                "cache-miss oracle blanks only the authored definition ID");
            check(string.Equals(PartsMarket.ProductKey(source.Copy()), PartsMarket.ProductKey(idOnly.Copy()), StringComparison.Ordinal),
                "fresh public ProductKey references omit only the definition ID");

            var price = source.Copy(); price.price = checked(source.price + 1);
            ObserveKeyVariant(check, log, "scalar-price", source, price);
            var name = source.Copy(); name.name = (source.name ?? "") + " l0";
            ObserveKeyVariant(check, log, "string-name", source, name);
            var category = source.Copy();
            var categories = Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>().ToArray();
            category.category = categories.FirstOrDefault(value => value != source.category);
            ObserveKeyVariant(check, log, "enum-category", source, category);
            var nullDefault = source.Copy(); nullDefault.description = source.description == null ? "" : null;
            ObserveKeyVariant(check, log, "null-default-description", source, nullDefault);
            var descriptionNull = source.Copy(); descriptionNull.description = null;
            var descriptionEmpty = source.Copy(); descriptionEmpty.description = "";
            ObserveKeyVariant(check, log, "description-null", source, descriptionNull);
            ObserveKeyVariant(check, log, "description-empty", source, descriptionEmpty);
            ObserveRelationOnly(check, log, "description-null-empty-pair", descriptionNull, descriptionEmpty);

            var awdSource = profile.catalog.parts.FirstOrDefault(definition => definition?.awdHardware?.calibration != null);
            if (awdSource == null)
                Open(check, log, "nested-awd-product-key", "authored catalogue has no nested AWD hardware/calibration source");
            else
            {
                var nested = awdSource.Copy();
                nested.awdHardware.calibration.responseSeconds += .125f;
                ObserveKeyVariant(check, log, "nested-awd-calibration", awdSource, nested);
                var missingNested = awdSource.Copy(); missingNested.awdHardware = null;
                ObserveKeyVariant(check, log, "nested-awd-null", awdSource, missingNested);
                var defaultNested = awdSource.Copy(); defaultNested.awdHardware = new AwdHardwareSpec();
                ObserveKeyVariant(check, log, "nested-awd-default", awdSource, defaultNested);
                ObserveRelationOnly(check, log, "nested-awd-null-default-pair", missingNested, defaultNested);
            }
            log.AppendLine("OBSERVATION|nullable-array-shape|PartDefinition and its nested AWD/calibration records expose no nullable array field in the current source declarations");

            var positiveZero = source.Copy(); positiveZero.boostScale = 0.0f;
            var negativeZero = source.Copy(); negativeZero.boostScale = -0.0f;
            ObserveKeyVariant(check, log, "signed-positive-zero", source, positiveZero);
            ObserveKeyVariant(check, log, "signed-negative-zero", source, negativeZero);
            ObserveRelationOnly(check, log, "signed-zero-pair", positiveZero, negativeZero);

            foreach (var pair in new[] { new KeyValuePair<string, float>("nan", float.NaN),
                         new KeyValuePair<string, float>("positive-infinity", float.PositiveInfinity),
                         new KeyValuePair<string, float>("negative-infinity", float.NegativeInfinity) })
            {
                var nonfinite = source.Copy(); nonfinite.boostScale = pair.Value;
                ObserveKeyVariant(check, log, "nonfinite-" + pair.Key, source, nonfinite);
            }

            check(LegacyCartCanonicalBoundaryReference.ProductCacheMiss(source) == sourceKeyBefore &&
                ReferenceEquals(source.awdHardware, sourceAwd) && ReferenceEquals(sourceAwd?.calibration, sourceCalibration) &&
                source.id != idOnly.id && source.price != price.price && source.name != name.name,
                "product-key probes leave authored source values and nested references unchanged");
        }

        static void CheckKeyParity(Action<bool, string> check, StringBuilder log, string label, PartDefinition definition)
        {
            // A new definition graph forces the production key through its cache-miss serializer path.
            var probe = definition.Copy();
            long requestBefore = MarketCatalogLayout.CanonicalKeyRequestCount;
            long jsonBefore = MarketCatalogLayout.CanonicalJsonEncodeCount;
            var current = Capture(() => PartsMarket.ProductKey(probe));
            long requestAfter = MarketCatalogLayout.CanonicalKeyRequestCount;
            long jsonAfter = MarketCatalogLayout.CanonicalJsonEncodeCount;
            var legacy = Capture(() => LegacyCartCanonicalBoundaryReference.ProductCacheMiss(probe));
            bool same = OutcomeSame(legacy, current);
            check(same, "literal ProductCacheMiss and public ProductKey outcomes match / " + label + " / " + KeySummary(current));
            bool cold = requestAfter == requestBefore + 1 && jsonAfter == jsonBefore + 1;
            check(cold, "fresh definition reference executes one observable public key cache miss / " + label +
                " / keyRequests=" + (requestAfter - requestBefore) + ";jsonEncodes=" + (jsonAfter - jsonBefore));
            log.AppendLine("PRODUCT_KEY|" + L0Safe(label) + "|" + KeySummary(current) + "|literalParity=" + same +
                "|freshReferenceCacheMiss=" + cold + "|keyRequests=" + (requestAfter - requestBefore) +
                "|jsonEncodes=" + (jsonAfter - jsonBefore));
        }

        static void ObserveKeyVariant(Action<bool, string> check, StringBuilder log, string label,
            PartDefinition source, PartDefinition variant)
        {
            CheckKeyParity(check, log, label, variant);
            ObserveKeyRelation(log, label, source, variant);
        }

        static void ObserveKeyRelation(StringBuilder log, string label, PartDefinition source, PartDefinition variant)
        {
            var sourceProbe = source.Copy(); var variantProbe = variant.Copy();
            var sourceKey = Capture(() => PartsMarket.ProductKey(sourceProbe));
            var variantKey = Capture(() => PartsMarket.ProductKey(variantProbe));
            bool keyEqual = sourceKey.Error == null && variantKey.Error == null &&
                string.Equals((string)sourceKey.Value, (string)variantKey.Value, StringComparison.Ordinal);
            bool freshnessEqual = false; string freshnessOutcome;
            try
            {
                freshnessEqual = MarketCatalogLayout.SameAuthoredDefinition(source, variant);
                freshnessOutcome = freshnessEqual.ToString();
            }
            catch (Exception error) { freshnessOutcome = error.GetType().FullName + " | " + error.Message; }
            log.AppendLine("OBSERVATION|product-relation|" + L0Safe(label) + "|productKeyEqual=" + keyEqual +
                "|freshnessSameAuthoredDefinition=" + L0Safe(freshnessOutcome) + "|source=" + KeySummary(sourceKey) +
                "|variant=" + KeySummary(variantKey));
        }

        static void ObserveRelationOnly(Action<bool, string> check, StringBuilder log, string label,
            PartDefinition left, PartDefinition right)
        {
            CheckKeyParity(check, log, label + "-left", left);
            CheckKeyParity(check, log, label + "-right", right);
            ObserveKeyRelation(log, label, left, right);
        }

        static void ObserveResolverRoutes(Action<bool, string> check, StringBuilder log)
        {
            if (!CreateAliasFixture(CanonicalBoundarySeed + 1, out var direct, out string reason))
            { Open(check, log, "resolver-actual-alias-fixture", reason); return; }

            ObserveColdStock(check, log, "direct-definition", direct, direct.Source);
            ObserveColdAvailable(check, log, "direct-definition", direct, direct.Source);
            if (!CreateAliasFixture(CanonicalBoundarySeed + 7, out var directLedger, out reason))
            { Open(check, log, "resolver-direct-ledger-fixture", reason); return; }
            ObserveLedger(check, log, "direct-definition", directLedger, directLedger.Source);
            if (!CreateAliasFixture(CanonicalBoundarySeed + 2, out var coldAlias, out reason))
            { Open(check, log, "resolver-cold-alias-fixture", reason); return; }
            ObserveColdStock(check, log, "cold-equivalent-alias-miss", coldAlias, coldAlias.UnknownAlias);
            ObserveColdAvailable(check, log, "cold-equivalent-alias-miss", coldAlias, coldAlias.UnknownAlias);

            if (!CreateAliasFixture(CanonicalBoundarySeed + 3, out var ledgerAlias, out reason))
            { Open(check, log, "resolver-ledger-alias-fixture", reason); return; }
            ObserveLedger(check, log, "cold-equivalent-alias-miss", ledgerAlias, ledgerAlias.UnknownAlias);

            if (!CreateAliasFixture(CanonicalBoundarySeed + 4, out var warm, out reason))
            { Open(check, log, "resolver-warm-index-fixture", reason); return; }
            warm.Profile.inventory.market.index = new Dictionary<string, NewPartStock>(StringComparer.Ordinal)
            { [warm.UnknownAlias.id] = warm.SecondRow };
            ObserveWarmStockAndLedger(check, log, warm);

            if (!CreateAliasFixture(CanonicalBoundarySeed + 5, out var held, out reason))
            { Open(check, log, "resolver-held-fixture", reason); return; }
            ObserveHeldAlias(check, log, held);

            if (!CreateAliasFixture(CanonicalBoundarySeed + 6, out var duplicate, out reason))
            { Open(check, log, "resolver-duplicate-fixture", reason); return; }
            duplicate.SecondRow.definitions.Add(duplicate.AliasA.id);
            duplicate.Profile.inventory.market.index = null;
            ObserveDuplicateDefinitionId(check, log, duplicate);
        }

        static void ObserveColdStock(Action<bool, string> check, StringBuilder log, string label,
            AliasFixture fixture, PartDefinition requested)
        {
            var profile = fixture.Profile; profile.inventory.market.index = null;
            var before = MarketImage.Capture(profile);
            var definitions = DefinitionImage.Capture(profile, requested.id);
            var outcome = Capture(() => PartsMarket.Stock(profile, requested));
            var after = MarketImage.Capture(profile);
            int selected = outcome.Error == null ? RowIndex(profile, outcome.Value as NewPartStock) : -1;
            var expectedStock = ReferenceEquals(requested, fixture.Source) ? fixture.SourceRow : fixture.FirstRow;
            int expectedRow = RowIndex(profile, expectedStock);
            bool onlyIndexWrite = before.SameRows(profile) && before.IndexRef == null && after.IndexRef != null && after.SameIndex(profile);
            check(onlyIndexWrite, "cold Stock preserves market row/value/reference state and publishes its actual index / " + label);
            bool expectedIndex = profile.inventory.market.index != null &&
                profile.inventory.market.index.TryGetValue(requested.id, out var mapped) && ReferenceEquals(mapped, expectedStock);
            check(outcome.Error == null && selected == expectedRow && expectedIndex,
                "cold Stock resolves direct or equivalent alias to the first ordered row / " + label + ";expected=" + expectedRow + ";actual=" + selected);
            check(definitions.Unchanged(profile), "cold Stock preserves every market-referenced part-definition value/reference / " + label);
            log.AppendLine("OBSERVATION|stock-cold|" + L0Safe(label) + "|" + OutcomeLabel(outcome) +
                "|selectedRow=" + selected + "|indexBefore=null|indexAfter=" + IndexSummary(profile.inventory.market.index, profile.inventory.market.parts) +
                "|cacheWriteReference=" + !ReferenceEquals(before.IndexRef, profile.inventory.market.index));
        }

        static void ObserveColdAvailable(Action<bool, string> check, StringBuilder log, string label,
            AliasFixture fixture, PartDefinition requested)
        {
            var profile = fixture.Profile; profile.inventory.market.index = null;
            var before = MarketImage.Capture(profile);
            var definitions = DefinitionImage.Capture(profile, requested.id);
            var outcome = Capture(() => PartsMarket.Available(profile, requested));
            int selected = profile.inventory.market.index != null && profile.inventory.market.index.TryGetValue(requested.id, out var row)
                ? RowIndex(profile, row) : -1;
            var expectedStock = ReferenceEquals(requested, fixture.Source) ? fixture.SourceRow : fixture.FirstRow;
            int expected = Math.Max(0, expectedStock.units);
            bool sourceRows = before.SameRows(profile);
            check(sourceRows, "cold Available preserves all market row/value/reference facts / " + label);
            check(definitions.Unchanged(profile), "cold Available preserves market-referenced part definitions / " + label);
            check(outcome.Error == null && Convert.ToInt32(outcome.Value, CultureInfo.InvariantCulture) == expected &&
                selected == RowIndex(profile, expectedStock),
                "cold Available selects the expected direct/first-alias row / " + label + ";available=" + OutcomeLabel(outcome) +
                ";selected=" + selected + ";expectedRow=" + RowIndex(profile, expectedStock));
            log.AppendLine("OBSERVATION|available-cold|" + L0Safe(label) + "|" + OutcomeLabel(outcome) +
                "|selectedRow=" + selected + "|index=" + IndexSummary(profile.inventory.market.index, profile.inventory.market.parts) +
                "|cacheWriteReference=" + !ReferenceEquals(before.IndexRef, profile.inventory.market.index));
        }

        static void ObserveLedger(Action<bool, string> check, StringBuilder log, string label,
            AliasFixture fixture, PartDefinition requested)
        {
            var profile = fixture.Profile; profile.inventory.market.index = null;
            var before = MarketImage.Capture(profile);
            var definitions = DefinitionImage.Capture(profile, requested.id);
            var ledger = new PartsMarket.Ledger(profile);
            BoundaryOutcome available = Capture(() => ledger.Available(requested));
            BoundaryOutcome quote = Capture(() => ledger.Quote(requested, 8, 100, 1));
            bool stable = before.SameRows(profile) && before.SameIndex(profile);
            check(stable, "Ledger Available/Quote preserve cold source rows and market index / " + label);
            check(definitions.Unchanged(profile), "Ledger Available/Quote preserve market-referenced part definitions / " + label);
            var expectedStock = ReferenceEquals(requested, fixture.Source) ? fixture.SourceRow : fixture.FirstRow;
            var expected = ExpectedQuote(expectedStock.units, expectedStock.deliveryPercent, 8, 100);
            bool firstRowSelected = available.Error == null && Convert.ToInt32(available.Value, CultureInfo.InvariantCulture) == expectedStock.units &&
                quote.Error == null && QuoteEquals(quote.Value as SupplyQuote, expected);
            check(firstRowSelected, "cold Ledger miss uses first product row and exact quote / " + label +
                ";expectedAvailable=" + expectedStock.units + ";actual=" + OutcomeLabel(available) + ";quote=" + QuoteOutcome(quote));
            log.AppendLine("OBSERVATION|ledger-cold|" + L0Safe(label) + "|available=" + OutcomeLabel(available) +
                "|expectedFirstRow=" + RowIndex(profile, expectedStock) + "|quote=" + QuoteOutcome(quote) +
                "|index=" + IndexSummary(profile.inventory.market.index, profile.inventory.market.parts) + "|cacheWriteReference=false");
        }

        static void ObserveWarmStockAndLedger(Action<bool, string> check, StringBuilder log, AliasFixture fixture)
        {
            var profile = fixture.Profile; var before = MarketImage.Capture(profile);
            var definitions = DefinitionImage.Capture(profile, fixture.UnknownAlias.id);
            BoundaryOutcome stock = Capture(() => PartsMarket.Stock(profile, fixture.UnknownAlias));
            int stockRow = stock.Error == null ? RowIndex(profile, stock.Value as NewPartStock) : -1;
            BoundaryOutcome available = Capture(() => new PartsMarket.Ledger(profile).Available(fixture.UnknownAlias));
            check(before.SameRows(profile) && before.SameIndex(profile),
                "warm-index resolution leaves market row values/references in place");
            check(definitions.Unchanged(profile), "warm Stock/Ledger preserve market-referenced definition values/references");
            check(stock.Error == null && stockRow == RowIndex(profile, fixture.SecondRow),
                "warm direct Stock index mapping takes precedence over alias first-row scan / actual=" + stockRow);
            check(available.Error == null && Convert.ToInt32(available.Value, CultureInfo.InvariantCulture) == fixture.FirstRow.units,
                "Ledger ignores warmed market.index and keeps its cold first-product-row result / actual=" + OutcomeLabel(available));
            log.AppendLine("OBSERVATION|warm-index-direct-map|stock=" + OutcomeLabel(stock) + "|stockRow=" + stockRow +
                "|ledgerAvailable=" + OutcomeLabel(available) + "|heldRoute=covered-by-active-held-fixture" +
                "|index=" + IndexSummary(profile.inventory.market.index, profile.inventory.market.parts) + "|cacheWriteReference=false");
        }

        static void ObserveHeldAlias(Action<bool, string> check, StringBuilder log, AliasFixture fixture)
        {
            var profile = fixture.Profile; var line = new PartCartLine
            { action = CartAction.NewSet, definitionId = fixture.AliasA.id, quantity = 1 };
            var beforeCapture = MarketImage.Capture(profile);
            var definitions = DefinitionImage.Capture(profile, fixture.AliasA.id);
            BoundaryOutcome capture = Capture(() => { CartReservations.Capture(profile, fixture.Car.instanceId, new[] { line }); return null; });
            if (capture.Error != null || profile.inventory.cart?.supply == null)
            {
                check(false, "actual reservation fixture captures a supply list / " + OutcomeLabel(capture));
                log.AppendLine("OBSERVATION|held-current-generation|capture=" + OutcomeLabel(capture) + "|supply=<unavailable>");
                return;
            }
            bool active = CartReservations.Active(profile) && profile.inventory.cart?.generation == profile.inventory.market.generation;
            string indexAfterCapture = IndexSummary(profile.inventory.market.index, profile.inventory.market.parts);
            bool captureIndexRefWrite = !ReferenceEquals(beforeCapture.IndexRef, profile.inventory.market.index);
            int heldDirect = CartReservations.Held(profile, fixture.AliasA, true);
            string indexAfterDirectHeld = IndexSummary(profile.inventory.market.index, profile.inventory.market.parts);
            int expectedHeld = profile.inventory.cart.supply.Where(row => row.generation == profile.inventory.market.generation &&
                (row.definitionId == fixture.AliasA.id || fixture.FirstRow.definitions.Contains(row.definitionId))).Sum(row => row.units);
            profile.inventory.market.index = null;
            string coldIndexBeforeHeld = IndexSummary(profile.inventory.market.index, profile.inventory.market.parts);
            var coldIndexBefore = MarketImage.Capture(profile);
            int heldCold = CartReservations.Held(profile, fixture.AliasA, true);
            string coldIndexAfterHeld = IndexSummary(profile.inventory.market.index, profile.inventory.market.parts);
            var coldIndexAfter = MarketImage.Capture(profile);
            profile.inventory.market.index = new Dictionary<string, NewPartStock>(StringComparer.Ordinal)
            { [fixture.UnknownAlias.id] = fixture.SecondRow };
            string warmAliasIndexBeforeHeld = IndexSummary(profile.inventory.market.index, profile.inventory.market.parts);
            var warmIndexBefore = MarketImage.Capture(profile);
            int heldAcrossAlias = CartReservations.Held(profile, fixture.UnknownAlias, true);
            string warmAliasIndexAfterHeld = IndexSummary(profile.inventory.market.index, profile.inventory.market.parts);
            var warmIndexAfter = MarketImage.Capture(profile);
            check(capture.Error == null && active && profile.inventory.cart.supply.Count > 0,
                "actual reservation fixture captures live same-generation supply / error=" + OutcomeLabel(capture));
            check(beforeCapture.SameRows(profile), "reservation capture does not rewrite market stock rows");
            check(definitions.Unchanged(profile), "reservation capture and Held preserve market-referenced definitions");
            check(heldDirect == expectedHeld && heldCold == expectedHeld && heldAcrossAlias == 0,
                "Held cold/direct and warm-alias routes follow Stock row-definition membership / direct=" + heldDirect +
                ";cold=" + heldCold + ";expected=" + expectedHeld + ";alias=" + heldAcrossAlias);
            check(coldIndexBefore.IndexRef == null && coldIndexAfter.IndexRef != null &&
                !ReferenceEquals(coldIndexBefore.IndexRef, coldIndexAfter.IndexRef) &&
                ReferenceEquals(warmIndexBefore.IndexRef, warmIndexAfter.IndexRef) && warmIndexBefore.SameIndex(profile),
                "Held cold call builds index while warm mapped call preserves the supplied index reference/content");
            log.AppendLine("OBSERVATION|held-current-generation|capture=" + OutcomeLabel(capture) + "|active=" + active +
                "|heldDirect=" + heldDirect + "|heldCold=" + heldCold + "|heldAcrossWarmAliasMap=" + heldAcrossAlias +
                "|generation=" + profile.inventory.market.generation + "|supply=" + SupplyFacts(profile.inventory.cart.supply) +
                "|indexAfterCapture=" + indexAfterCapture + ";referenceWrite=" + captureIndexRefWrite +
                "|indexAfterDirectHeld=" + indexAfterDirectHeld + "|coldIndex=" + coldIndexBeforeHeld + "->" + coldIndexAfterHeld +
                ";referenceWrite=" + !ReferenceEquals(coldIndexBefore.IndexRef,coldIndexAfter.IndexRef) +
                "|warmAliasIndex=" + warmAliasIndexBeforeHeld + "->" + warmAliasIndexAfterHeld +
                ";referenceWrite=" + !ReferenceEquals(warmIndexBefore.IndexRef,warmIndexAfter.IndexRef));
        }

        static void ObserveDuplicateDefinitionId(Action<bool, string> check, StringBuilder log, AliasFixture fixture)
        {
            var profile = fixture.Profile; profile.inventory.market.index = null;
            var before = MarketImage.Capture(profile);
            var definitions = DefinitionImage.Capture(profile, fixture.AliasA.id);
            var stock = Capture(() => PartsMarket.Stock(profile, fixture.AliasA));
            var afterStock = MarketImage.Capture(profile);
            var ledger = Capture(() => new PartsMarket.Ledger(profile).Available(fixture.AliasA));
            check(before.SameRows(profile), "malformed duplicate-ID lookup does not alter stock-row values/references");
            check(definitions.Unchanged(profile), "malformed duplicate-ID routes preserve part-definition values/references");
            check(ledger.Error == null && Convert.ToInt32(ledger.Value, CultureInfo.InvariantCulture) == fixture.SecondRow.units,
                "Ledger's direct definition map observes its later row assignment for duplicate IDs / " + OutcomeLabel(ledger));
            log.AppendLine("OBSERVATION|duplicate-definition-id|stock=" + OutcomeLabel(stock) +
                "|stockIndexAfter=" + IndexSummary(profile.inventory.market.index, profile.inventory.market.parts) +
                "|ledgerAvailable=" + OutcomeLabel(ledger) + "|row1=" + RowIndex(profile, fixture.FirstRow) +
                "|row2=" + RowIndex(profile, fixture.SecondRow) + "|partialIndexChanged=" +
                (!ReferenceEquals(before.IndexRef, afterStock.IndexRef)));
        }

        static void ObserveCartSignatures(Action<bool, string> check, StringBuilder log)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile, out string carId, CanonicalBoundarySeed + 10);
            var reservation = new CartReservationState { carId = carId, lines = new List<PartCartLine>() };
            profile.inventory.cart = reservation;
            var baseline = new PartCartLine();
            var ordered = new[] { new PartCartLine { definitionId="l0-order-a" },
                new PartCartLine { definitionId="l0-order-b" } };
            var vectors = new List<SignatureVector>
            {
                new SignatureVector { Name="default-row", Stored=new[]{baseline.Copy()}, Query=new[]{baseline.Copy()} },
                new SignatureVector { Name="zero-value-row", Stored=new[]{new PartCartLine{quantity=0,positions=null}}, Query=new[]{new PartCartLine{quantity=0,positions=null}} },
                new SignatureVector { Name="null-row", Stored=new PartCartLine[]{null}, Query=new PartCartLine[]{null} },
                new SignatureVector { Name="null-row-to-default-row", Stored=new PartCartLine[]{null}, Query=new[]{new PartCartLine()} },
                new SignatureVector { Name="default-row-to-null-row", Stored=new[]{new PartCartLine()}, Query=new PartCartLine[]{null} },
                new SignatureVector { Name="null-row-to-zero-row", Stored=new PartCartLine[]{null}, Query=new[]{new PartCartLine{quantity=0,positions=null}} },
                new SignatureVector { Name="null-positions-vs-empty", Stored=new[]{RowCopyWith(baseline,row=>row.positions=null)}, Query=new[]{RowCopyWith(baseline,row=>row.positions=new List<string>())} },
                new SignatureVector { Name="empty-positions", Stored=new[]{RowCopyWith(baseline,row=>row.positions=new List<string>())}, Query=new[]{RowCopyWith(baseline,row=>row.positions=new List<string>())} },
                new SignatureVector { Name="ordered-positions", Stored=new[]{RowCopyWith(baseline,row=>row.positions=new List<string>{"front","rear"})}, Query=new[]{RowCopyWith(baseline,row=>row.positions=new List<string>{"front","rear"})} },
                new SignatureVector { Name="reversed-positions", Stored=new[]{RowCopyWith(baseline,row=>row.positions=new List<string>{"front","rear"})}, Query=new[]{RowCopyWith(baseline,row=>row.positions=new List<string>{"rear","front"})} },
                new SignatureVector { Name="null-position-vs-empty-string", Stored=new[]{RowCopyWith(baseline,row=>row.positions=new List<string>{null})}, Query=new[]{RowCopyWith(baseline,row=>row.positions=new List<string>{""})} },
                new SignatureVector { Name="repeated-row", Stored=new[]{baseline.Copy(),baseline.Copy()}, Query=new[]{baseline.Copy(),baseline.Copy()} },
                new SignatureVector { Name="null-enumerable", Stored=new[]{baseline.Copy()}, Query=null }
            };
            AddSingleFieldRows(vectors, baseline);
            var second = new PartCartLine { definitionId="l0-second", quantity=3, action=CartAction.NewUnits, role="l0-role" };
            vectors.Add(new SignatureVector { Name="row-order-ab", Stored=new[]{baseline.Copy(),second.Copy()}, Query=new[]{baseline.Copy(),second.Copy()} });
            vectors.Add(new SignatureVector { Name="row-order-ba", Stored=new[]{baseline.Copy(),second.Copy()}, Query=new[]{second.Copy(),baseline.Copy()} });

            foreach (var vector in vectors)
                ObserveSignatureVector(check, log, profile, carId, vector);
        }

        static void AddSingleFieldRows(List<SignatureVector> vectors, PartCartLine baseline)
        {
            Action<string, Action<PartCartLine>> add = (name, mutate) =>
                vectors.Add(new SignatureVector { Name="field-"+name, Stored=new[]{baseline.Copy()}, Query=new[]{RowCopyWith(baseline,mutate)} });
            add("definitionId", row => row.definitionId = "l0-definition");
            add("quantity", row => row.quantity = 2);
            add("install", row => row.install = true);
            add("preparedBlock", row => row.preparedBlock = true);
            add("preparationOwner", row => row.preparationOwner = "l0-owner");
            add("action", row => row.action = CartAction.NewUnits);
            add("assetId", row => row.assetId = "l0-asset");
            add("role", row => row.role = "l0-role");
            add("destinationId", row => row.destinationId = "l0-target");
            add("destination", row => row.destination = InventoryKind.Car);
            add("positions", row => row.positions = new List<string> { "l0-position" });
            foreach (var field in new[] { "definitionId", "preparationOwner", "assetId", "role", "destinationId" })
            {
                vectors.Add(new SignatureVector { Name="field-"+field+"-null-empty",
                    Stored=new[]{RowCopyWith(baseline,row=>SetStringField(row,field,null))},
                    Query=new[]{RowCopyWith(baseline,row=>SetStringField(row,field,""))} });
                vectors.Add(new SignatureVector { Name="field-"+field+"-empty-null",
                    Stored=new[]{RowCopyWith(baseline,row=>SetStringField(row,field,""))},
                    Query=new[]{RowCopyWith(baseline,row=>SetStringField(row,field,null))} });
            }
        }

        static void ObserveSignatureVector(Action<bool, string> check, StringBuilder log, PlayerProfile profile,
            string carId, SignatureVector vector)
        {
            string label=vector.Name;
            var state = profile.inventory.cart;
            var oldRowsList = state.lines;
            var oldCarId = state.carId;
            var oldSupplyRef = state.supply;
            var oldSupplyFacts = SupplyFacts(state.supply);
            int oldGeneration = state.generation, oldExpiresRun = state.expiresRun;
            var reservationImage = RowImage.Capture(oldRowsList);
            var inputImage = RowImage.Capture(vector.Query);
            var storedInputImage = RowImage.Capture(vector.Stored);
            try
            {
                state.lines = vector.Stored == null ? null : vector.Stored.Select(row => row?.Copy()).ToList();
                var actualStateRows = state.lines;
                var actualStateImage = RowImage.Capture(actualStateRows);
                long jsonBefore = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
                var frozenSignature = Capture(() => LegacyCartCanonicalBoundaryReference.Signature(vector.Query));
                var actualSignature = Capture(() => CartReservations.SameCart(profile, carId, vector.Query));
                var frozenSame = Capture(() => LegacyCartCanonicalBoundaryReference.SameCart(profile, carId, vector.Query));
                long jsonAfter = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
                bool sameOutcome = OutcomeSame(frozenSame, actualSignature);
                check(sameOutcome, "literal frozen SameCart and live SameCart outcomes agree / " + label +
                    " / " + OutcomeLabel(actualSignature) + ";frozen=" + OutcomeLabel(frozenSame));
                bool expectedSource = inputImage.Unchanged(vector.Query) && storedInputImage.Unchanged(vector.Stored) &&
                    actualStateImage.Unchanged(state.lines) && ReferenceEquals(profile.inventory.cart, state) &&
                    ReferenceEquals(state.lines, actualStateRows) && reservationImage.Unchanged(oldRowsList) &&
                    state.carId == oldCarId && ReferenceEquals(state.supply, oldSupplyRef) &&
                    SupplyFacts(state.supply) == oldSupplyFacts && state.generation == oldGeneration && state.expiresRun == oldExpiresRun;
                check(expectedSource, "signature probes preserve supplied and reservation row/list values/references / " + label);

                var leftSignature = Capture(() => LegacyCartCanonicalBoundaryReference.Signature(vector.Stored));
                bool typedHypothesis = TypedRowsEqual(state.lines, vector.Query);
                bool comparable = leftSignature.Error == null && frozenSignature.Error == null;
                bool jsonEquality = comparable && string.Equals(leftSignature.Value as string,
                    frozenSignature.Value as string, StringComparison.Ordinal);
                string hypothesisStatus = !comparable ? "NOT-COMPARABLE" :
                    typedHypothesis == jsonEquality ? "OBSERVED-AGREES" : "UNRESOLVED-MISMATCH";
                log.AppendLine("OBSERVATION|cart-signature|" + L0Safe(label) + "|storedSignature=" + KeySummary(leftSignature) +
                    "|querySignature=" + KeySummary(frozenSignature) + "|liveSameCart=" + OutcomeLabel(actualSignature) +
                    "|frozenSameCart=" + OutcomeLabel(frozenSame) + "|typedOrdinalHypothesis=" + typedHypothesis +
                    "|literalSignatureEqual=" + jsonEquality + "|signatureComparable=" + comparable + "|hypothesisStatus=" + hypothesisStatus +
                    "|jsonCounter=" + jsonBefore + "->" + jsonAfter + "(known-route-uninstrumented)");
            }
            finally { state.lines = oldRowsList; }
        }

        static PartCartLine RowCopyWith(PartCartLine source, Action<PartCartLine> mutate)
        { var copy = source.Copy(); mutate(copy); return copy; }

        static void SetStringField(PartCartLine row, string field, string value)
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

        static bool TypedRowsEqual(IEnumerable<PartCartLine> left, IEnumerable<PartCartLine> right)
        {
            if (left == null || right == null) return left == null && right == null;
            var a = left.ToArray(); var b = right.ToArray();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                var x = a[i]; var y = b[i];
                if (x == null || y == null) { if (!ReferenceEquals(x, y)) return false; else continue; }
                if (x.definitionId != y.definitionId || x.quantity != y.quantity || x.install != y.install ||
                    x.preparedBlock != y.preparedBlock || x.preparationOwner != y.preparationOwner || x.action != y.action ||
                    x.assetId != y.assetId || x.role != y.role || x.destinationId != y.destinationId || x.destination != y.destination)
                    return false;
                var xp = x.positions ?? new List<string>(); var yp = y.positions ?? new List<string>();
                if (!xp.SequenceEqual(yp, StringComparer.Ordinal)) return false;
            }
            return true;
        }

        static bool CreateAliasFixture(uint seed, out AliasFixture fixture, out string reason)
        {
            fixture = null; reason = null;
            try
            {
                CartPreviewCharacterizationFixtures.CreateNormalized(out var profile, out string carId, seed);
                var car = profile.cars.Find(item => item != null && item.instanceId == carId);
                var source = profile.catalog.parts.FirstOrDefault(definition => definition != null && definition.price > 0 &&
                    profile.inventory.market.parts.Any(row => row != null && row.definitions != null && row.definitions.Contains(definition.id)));
                var sourceRow = source == null ? null : profile.inventory.market.parts.FirstOrDefault(row => row != null && row.definitions.Contains(source.id));
                string sourceKey = source == null ? null : LegacyCartCanonicalBoundaryReference.ProductCacheMiss(source);
                var first = source == null ? null : profile.inventory.market.parts.FirstOrDefault(row => row != null && row.definitions != null &&
                    row.definitions.Any(id => { var existing = profile.catalog.Find(id); return existing != null &&
                        LegacyCartCanonicalBoundaryReference.ProductCacheMiss(existing) == sourceKey; }));
                if (source == null || sourceRow == null || first == null || car == null)
                { reason = "actual selected car lacks a stocked authored product/key row"; return false; }
                var aliasA = source.Copy(); aliasA.id = "l0-alias-a-" + seed.ToString(CultureInfo.InvariantCulture);
                var aliasB = source.Copy(); aliasB.id = "l0-alias-b-" + seed.ToString(CultureInfo.InvariantCulture);
                var unknown = source.Copy(); unknown.id = "l0-alias-unknown-" + seed.ToString(CultureInfo.InvariantCulture);
                profile.catalog.parts.Add(aliasA); profile.catalog.parts.Add(aliasB); profile.catalog.parts.Add(unknown); profile.catalog.RebuildIndexes();
                first.definitions.Add(aliasA.id); first.units = 7; first.initialUnits = Math.Max(first.initialUnits, 7);
                var second = new NewPartStock { definitions = new List<string> { aliasB.id }, units = 13, initialUnits = 13,
                    deliveryPercent = first.deliveryPercent, discovered = first.discovered };
                profile.inventory.market.parts.Add(second);
                profile.inventory.market.index = null;
                fixture = new AliasFixture { Profile = profile, Car = car, Source = source, AliasA = aliasA,
                    AliasB = aliasB, UnknownAlias = unknown, SourceRow = sourceRow, FirstRow = first, SecondRow = second };
                return true;
            }
            catch (Exception error) { reason = error.GetType().FullName + " | " + error.Message; return false; }
        }

        static void Open(Action<bool, string> check, StringBuilder log, string label, string reason)
        {
            log.AppendLine("OPEN|" + L0Safe(label) + "|" + L0Safe(reason));
            check(false, "required actual fixture/probe available / " + label + " / " + reason);
        }

        static string RowFacts(PartCartLine row)
        {
            if (row == null) return "<null-row>";
            return "definitionId=" + StringField(row.definitionId) + ";quantity=" + row.quantity.ToString(CultureInfo.InvariantCulture) +
                ";install=" + row.install + ";preparedBlock=" + row.preparedBlock + ";preparationOwner=" + StringField(row.preparationOwner) +
                ";action=" + ((int)row.action).ToString(CultureInfo.InvariantCulture) + ";assetId=" + StringField(row.assetId) +
                ";role=" + StringField(row.role) + ";destinationId=" + StringField(row.destinationId) +
                ";destination=" + ((int)row.destination).ToString(CultureInfo.InvariantCulture) +
                ";positions=" + StringList(row.positions);
        }

        static string StockFacts(NewPartStock row)
            => row == null ? "<null-row>" : "definitions=" + StringList(row.definitions) +
               ";units=" + row.units + ";initial=" + row.initialUnits + ";feePercent=" + row.deliveryPercent + ";discovered=" + row.discovered;

        static string StringList(IEnumerable<string> values)
            => values == null ? "<null>" : "count=" + values.Count().ToString(CultureInfo.InvariantCulture) +
                "[" + string.Join("|", values.Select(StringField)) + "]";

        static string IndexFactsFor(Dictionary<string, NewPartStock> index, List<NewPartStock> rows)
        {
            if (index == null) return "<null-index>";
            return string.Join(";", index.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => StringField(pair.Key) + "=" + RowIndex(rows, pair.Value)));
        }

        static string IndexSummary(Dictionary<string, NewPartStock> index, List<NewPartStock> rows)
            => index == null ? "<null>" : "count=" + index.Count + ";sha=" + L0Hash(IndexFactsFor(index, rows));

        static string SupplyFacts(IEnumerable<ReservedPartSupply> supply)
            => supply == null ? "<null>" : string.Join(";", supply.Select(row => row == null ? "<null>" :
                StringField(row.definitionId) + ":" + row.units + "@" + row.generation));

        static SupplyQuote ExpectedQuote(int available, int percent, int units, int basePrice)
        {
            int local = Math.Min(units, available); int express = units - local;
            int fee = express == 0 ? 0 : checked((int)Math.Ceiling(basePrice * (express / (double)units) * percent / 100.0));
            return new SupplyQuote { localUnits = local, expressUnits = express, fee = fee, cost = checked(basePrice + fee) };
        }

        static bool QuoteEquals(SupplyQuote actual, SupplyQuote expected)
            => actual != null && expected != null && actual.localUnits == expected.localUnits &&
               actual.expressUnits == expected.expressUnits && actual.fee == expected.fee && actual.cost == expected.cost;

        static int RowIndex(PlayerProfile profile, NewPartStock row) => RowIndex(profile.inventory.market.parts, row);
        static int RowIndex(List<NewPartStock> rows, NewPartStock row)
            => row == null ? -1 : rows.FindIndex(candidate => ReferenceEquals(candidate, row));

        static BoundaryOutcome Capture(Func<object> operation)
        {
            try { return new BoundaryOutcome { Value = operation() }; }
            catch (Exception error) { return new BoundaryOutcome { Error = error }; }
        }

        static bool OutcomeSame(BoundaryOutcome a, BoundaryOutcome b)
            => a.Error == null && b.Error == null ? Equals(a.Value, b.Value) :
                a.Error != null && b.Error != null && a.Error.GetType() == b.Error.GetType() && a.Error.Message == b.Error.Message;

        static string OutcomeLabel(BoundaryOutcome outcome)
            => outcome.Error == null ? "value=" + L0Safe(outcome.Value?.ToString()) :
               "error=" + L0Safe(outcome.Error.GetType().FullName) + "|" + L0Safe(outcome.Error.Message);

        static string QuoteOutcome(BoundaryOutcome outcome)
        {
            if (outcome.Error != null) return OutcomeLabel(outcome);
            if (outcome.Value is SupplyQuote quote)
                return "cost=" + quote.cost + ";fee=" + quote.fee + ";local=" + quote.localUnits + ";express=" + quote.expressUnits;
            return OutcomeLabel(outcome);
        }

        static string KeySummary(BoundaryOutcome outcome)
        {
            if (outcome.Error != null) return "error=" + L0Safe(outcome.Error.GetType().FullName) + ";message=" + L0Safe(outcome.Error.Message);
            var value = outcome.Value as string;
            return "length=" + (value?.Length ?? -1) + ";sha256=" + L0Hash(value ?? "<null>");
        }

        static string L0Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? "<null>"))).Replace("-", "").ToLowerInvariant();
        }

        static string StringField(string value) => value == null ? "<null>" : value.Length.ToString(CultureInfo.InvariantCulture) + ":" + L0Safe(value);
        static string L0Safe(string value) => string.IsNullOrEmpty(value) ? "<empty>" : value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("|", "/");
    }
}
#endif
