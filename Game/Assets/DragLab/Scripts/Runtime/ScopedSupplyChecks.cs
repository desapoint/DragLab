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
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Editor-only parity and source-isolation checks for the dormant L1B stock scope.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        sealed class L1BFixture
        {
            internal PlayerProfile Profile;
            internal PartDefinition Definition;
            internal PartDefinition Alias;
        }

        sealed class L1BOutcome<T>
        {
            internal T Value;
            internal Exception Error;
        }

        sealed class L1BCheckLog
        {
            readonly Action<bool, string> callback;
            readonly StringBuilder text = new StringBuilder();
            readonly string folder;
            int assertions, failures;
            internal L1BCheckLog(Action<bool, string> check, string path) { callback = check; folder = path; }
            internal void Check(bool ok, string label)
            {
                assertions++; if (!ok) failures++;
                callback(ok, label);
                text.AppendLine("ASSERT|" + (ok ? "PASS" : "FAIL") + "|" + L1BSafe(label));
            }
            internal void Note(string value) => text.AppendLine(L1BSafe(value));
            internal void Flush()
            {
                text.Insert(0, "S06d2b1l1b scoped supply checks\nassertions=" + assertions + ";failures=" + failures + "\n");
                File.WriteAllText(Path.Combine(folder, "S06D2B1L1B_SCOPED_SUPPLY.txt"), text.ToString(), new UTF8Encoding(false));
            }
        }

        sealed class L1BSourceFacts
        {
            internal string Values;
            internal object[] References;
            internal PartProductValue[] Products;
            internal bool Same(L1BSourceFacts other)
            {
                if (other == null || Values != other.Values || References.Length != other.References.Length || Products.Length != other.Products.Length) return false;
                for (int i = 0; i < References.Length; i++) if (!ReferenceEquals(References[i], other.References[i])) return false;
                for (int i = 0; i < Products.Length; i++) if (!Products[i].RawEquals(other.Products[i])) return false;
                return true;
            }
        }

        /// <summary>Root-owned runner invokes this; the checker never starts Unity or a process.</summary>
        public static void RunScopedSupply(Action<bool, string> check, string folder)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("An isolated Editor report folder is required.", nameof(folder));
            Directory.CreateDirectory(folder);
            var log = new L1BCheckLog(check, folder);
            log.Note("scope=dormant borrowed scoped stock/reservation values; no caller activation or publication");
            try
            {
                L1BOracleLiveness(log);
                L1BStockMatrix(log);
                L1BCatalogLookupMatrix(log);
                L1BLedgerMatrix(log);
                L1BReservationMatrix(log);
                L1BQuoteAndFeeMatrix(log);
                L1BLifetimeAndPurity(log);
            }
            catch (Exception error)
            {
                log.Check(false, "L1B diagnostic completed without an unhandled exception / " + L1BError(error));
            }
            finally { log.Flush(); }
        }

        static L1BFixture L1BCreateFixture()
        {
            var vehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            if (vehicles == null || vehicles.Length == 0) throw new InvalidOperationException("L1B requires actual authored vehicle definitions.");
            var catalog = new MechanicalCatalog(vehicles.Select(vehicle => vehicle.spec));
            var definition = catalog.parts.FirstOrDefault(value => value != null && value.category == PartCategory.Pistons);
            if (definition == null) throw new InvalidOperationException("L1B requires an actual authored piston definition.");
            var profile = new PlayerProfile
            {
                catalog = catalog,
                vehicleStock = new VehicleStockState { version = 1, seed = 73019, generation = 4, completedRuns = 3 },
                inventory = new InventoryState()
            };
            profile.inventory.market = new PartsStockState { version = 2, seed = 4421, generation = 4, guaranteeAtRefresh = 0, parts = new List<NewPartStock>() };
            return new L1BFixture { Profile = profile, Definition = definition };
        }

        static PartDefinition L1BAddIdAlias(L1BFixture fixture, string suffix = "/l1b-alias")
        {
            var alias = fixture.Definition.Copy();
            alias.id = fixture.Definition.id + suffix;
            if (fixture.Profile.catalog.parts.Any(value => value != null && value.id == alias.id)) throw new InvalidOperationException("L1B alias ID unexpectedly collides with authored content.");
            fixture.Profile.catalog.parts.Add(alias);
            fixture.Alias = alias;
            return alias;
        }

        static NewPartStock L1BStock(params string[] ids)
            => new NewPartStock { definitions = ids == null ? null : new List<string>(ids), units = 12, initialUnits = 12, deliveryPercent = 7, discovered = true };

        static void L1BSetReservation(L1BFixture fixture, int expiresRun, params ReservedPartSupply[] supplies)
        {
            fixture.Profile.inventory.cart = new CartReservationState
            {
                version = 1, createdRun = Math.Max(0, fixture.Profile.vehicleStock.completedRuns - 1), expiresRun = expiresRun,
                generation = fixture.Profile.inventory.market.generation, carId = "diagnostic-owner",
                lines = new List<PartCartLine>(), supply = supplies == null ? null : new List<ReservedPartSupply>(supplies)
            };
        }

        static string L1BSafe(string value)
        {
            value = (value ?? "<null>").Replace("\r", "\\r").Replace("\n", "\\n").Replace("|", "/");
            return value.Length <= 200 ? value : value.Substring(0, 200);
        }

        static string L1BError(Exception error)
            => error == null ? "<none>" : error.GetType().FullName + ":" + L1BSafe(error.Message) +
                (error is ArgumentException argument ? ":param=" + L1BSafe(argument.ParamName) : "");

        static L1BOutcome<T> L1BCapture<T>(Func<T> action)
        {
            try { return new L1BOutcome<T> { Value = action() }; }
            catch (Exception error) { return new L1BOutcome<T> { Error = error }; }
        }

        static L1BOutcome<T> L1BMeasure<T>(L1BCheckLog log, string label, Func<T> action)
        {
            long json = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            long requests = MarketCatalogLayout.CanonicalKeyRequestCount;
            long encodes = MarketCatalogLayout.CanonicalJsonEncodeCount;
            var random = UnityEngine.Random.state;
            bool capture = RuntimeEntropy.Capture;
            DateTime clock = capture ? RuntimeEntropy.UtcNow : default(DateTime);
            var identity = RuntimeEntropy.BeginIdentityObservation(64);
            var outcome = L1BCapture(action);
            identity.Dispose();
            bool unchanged = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount == json &&
                MarketCatalogLayout.CanonicalKeyRequestCount == requests && MarketCatalogLayout.CanonicalJsonEncodeCount == encodes &&
                random.Equals(UnityEngine.Random.state) && identity.Count == 0 && identity.Closed && !identity.Overflowed &&
                RuntimeEntropy.Capture == capture && (!capture || (RuntimeEntropy.UtcNow.Ticks == clock.Ticks && RuntimeEntropy.UtcNow.Kind == clock.Kind));
            log.Check(unchanged, "candidate operation has no JSON/key-cache/ID/RNG/entropy writes / " + label);
            return outcome;
        }

        static bool L1BSameError(Exception candidate, Exception oracle)
            => candidate != null && oracle != null && candidate.GetType() == oracle.GetType() && candidate.Message == oracle.Message &&
                (!(candidate is ArgumentException left) || oracle is ArgumentException right && left.ParamName == right.ParamName);

        static void L1BCompareErrors(L1BCheckLog log, string label, Exception candidate, Exception oracle)
        {
            log.Check(candidate == null && oracle == null || L1BSameError(candidate, oracle),
                label + " matches exact frozen exception class/message/parameter / candidate=" + L1BError(candidate) + ";oracle=" + L1BError(oracle));
        }

        static L1BSourceFacts L1BSourceState(PlayerProfile profile, params PartDefinition[] watchedDefinitions)
        {
            var values = new StringBuilder();
            var refs = new List<object>();
            var productFacts = new List<PartProductValue>();
            if (profile == null) return new L1BSourceFacts { Values = "profile=null", References = new object[0], Products = new PartProductValue[0] };
            var inventory = profile.inventory;
            refs.Add(inventory); values.Append("inventory=").Append(inventory == null ? "null" : "set");
            if (inventory == null) return new L1BSourceFacts { Values = values.ToString(), References = refs.ToArray(), Products = new PartProductValue[0] };
            var market = inventory.market; refs.Add(market);
            values.Append("|market=").Append(market == null ? "null" : "set");
            if (market != null)
            {
                values.Append("|epoch=").Append(market.generation).Append('|').Append(market.seed).Append("|parts=").Append(market.parts == null ? -1 : market.parts.Count);
                refs.Add(market.parts);
                L1BAppendRows(values, refs, market.parts);
                refs.Add(market.index);
                L1BAppendIndex(values, refs, market.index);
            }
            var cart = inventory.cart; refs.Add(cart);
            values.Append("|using=").Append(inventory.usingCartReservation ? 1 : 0).Append("|cart=").Append(cart == null ? "null" : "set");
            if (cart != null)
            {
                values.Append('|').Append(cart.version).Append('|').Append(cart.createdRun).Append('|').Append(cart.expiresRun).Append('|').Append(cart.generation).Append('|').Append(cart.carId);
                refs.Add(cart.supply); values.Append("|holds=").Append(cart.supply == null ? -1 : cart.supply.Count);
                if (cart.supply != null) for (int i = 0; i < cart.supply.Count; i++)
                {
                    var hold = cart.supply[i]; refs.Add(hold);
                    values.Append("|h").Append(i).Append('=');
                    if (hold == null) values.Append("null");
                    else values.Append(hold.definitionId).Append(',').Append(hold.units).Append(',').Append(hold.generation);
                }
            }
            values.Append("|runs=").Append(profile.vehicleStock == null ? -1 : profile.vehicleStock.completedRuns);
            refs.Add(profile.vehicleStock);
            var catalog = profile.catalog; refs.Add(catalog);
            values.Append("|catalog=").Append(catalog == null ? "null" : catalog.parts.Count.ToString(CultureInfo.InvariantCulture));
            if (catalog != null)
            {
                refs.Add(catalog.parts);
                values.Append("|revision=").Append(catalog.AuthoringRevision);
                foreach (var definition in watchedDefinitions ?? Array.Empty<PartDefinition>())
                {
                    refs.Add(definition);
                    values.Append("|d=").Append(definition == null ? "null" : definition.id);
                    if (definition != null)
                    {
                        refs.Add(definition.awdHardware); refs.Add(definition.awdHardware?.calibration);
                        productFacts.Add(PartProductValue.Capture(definition));
                    }
                }
                L1BAppendCatalogIndex(values, refs, catalog);
            }
            return new L1BSourceFacts { Values = values.ToString(), References = refs.ToArray(), Products = productFacts.ToArray() };
        }

        static void L1BAppendRows(StringBuilder values, List<object> refs, List<NewPartStock> rows)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i]; refs.Add(row); values.Append("|r").Append(i).Append('=');
                if (row == null) { values.Append("null"); continue; }
                refs.Add(row.definitions);
                values.Append(row.units).Append(',').Append(row.initialUnits).Append(',').Append(row.deliveryPercent).Append(',').Append(row.discovered ? 1 : 0).Append(':');
                if (row.definitions == null) values.Append("null");
                else values.Append(string.Join("\u001f", row.definitions.Select(id => id ?? "<null>")));
            }
        }

        static void L1BAppendIndex(StringBuilder values, List<object> refs, IDictionary<string, NewPartStock> index)
        {
            if (index == null) { values.Append("|index=null"); return; }
            values.Append("|index=").Append(index.GetType().FullName).Append('|').Append(index.Count);
            refs.Add(index);
            var entries = index.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
            foreach (var entry in entries) { refs.Add(entry.Value); values.Append('|').Append(entry.Key).Append('=').Append(L1BReferencePosition(entry.Value)); }
        }

        static int L1BReferencePosition(object value)
        {
            // The index stores NewPartStock directly; callers add row values to the same reference vector.
            return value == null ? -1 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }

        static void L1BAppendCatalogIndex(StringBuilder values, List<object> refs, MechanicalCatalog catalog)
        {
            Type type = typeof(MechanicalCatalog);
            var mapField = type.GetField("partIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            var countField = type.GetField("indexedPartCount", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mapField == null || countField == null) { values.Append("|catalog-index-fields=missing"); return; }
            object map = mapField.GetValue(catalog); refs.Add(map);
            values.Append("|catalog-index=").Append(map == null ? "null" : "set").Append('|').Append(countField.GetValue(catalog));
            var revision = type.GetField("authoringRevision", BindingFlags.Instance | BindingFlags.NonPublic);
            var authoring = type.GetField("authoring", BindingFlags.Instance | BindingFlags.NonPublic);
            if (revision != null) values.Append('|').Append(revision.GetValue(catalog));
            if (authoring != null) values.Append('|').Append((bool)authoring.GetValue(catalog) ? 1 : 0);
            if (map is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in L1BDictionaryEntries(dictionary).OrderBy(item => item.Key as string, StringComparer.Ordinal))
                {
                    object indexed = entry.Value;
                    var fields = indexed.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var valueField = fields.FirstOrDefault(field => field.Name == "value");
                    var positionField = fields.FirstOrDefault(field => field.Name == "position");
                    object part = valueField?.GetValue(indexed); refs.Add(part);
                    values.Append('|').Append(entry.Key).Append('=').Append(positionField == null ? -1 : (int)positionField.GetValue(indexed)).Append(':')
                        .Append(part == null ? "null" : ((PartDefinition)part).id);
                }
            }
        }

        static IEnumerable<DictionaryEntry> L1BDictionaryEntries(IDictionary dictionary)
        {
            IDictionaryEnumerator iterator = dictionary.GetEnumerator();
            while (iterator.MoveNext()) yield return iterator.Entry;
        }

        static string L1BQuoteText(SupplyQuote quote)
            => quote == null ? "null" : quote.localUnits + "," + quote.expressUnits + "," + quote.fee + "," + quote.cost;

        static string L1BQuoteText(ScopedSupplyQuote quote)
            => quote == null ? "null" : quote.LocalUnits + "," + quote.ExpressUnits + "," + quote.Fee + "," + quote.Cost;

        static bool L1BSameQuote(ScopedSupplyQuote candidate, SupplyQuote oracle)
            => candidate != null && oracle != null && candidate.LocalUnits == oracle.localUnits && candidate.ExpressUnits == oracle.expressUnits &&
                candidate.Fee == oracle.fee && candidate.Cost == oracle.cost;

        static bool L1BSameQuote(ScopedSupplyQuote left, ScopedSupplyQuote right)
            => left != null && right != null && left.LocalUnits == right.LocalUnits && left.ExpressUnits == right.ExpressUnits && left.Fee == right.Fee && left.Cost == right.Cost;

        static void L1BOracleLiveness(L1BCheckLog log)
        {
            var live = L1BCreateFixture(); var frozen = L1BCreateFixture();
            live.Profile.inventory.market.parts.Add(L1BStock(live.Definition.id));
            frozen.Profile.inventory.market.parts.Add(L1BStock(frozen.Definition.id));
            var liveStock = L1BCapture(() => PartsMarket.Stock(live.Profile, live.Definition));
            var frozenStock = L1BCapture(() => LegacyScopedSupplyReference.Stock(frozen.Profile, frozen.Definition));
            log.Check(liveStock.Error == null && frozenStock.Error == null &&
                L1BRowOrdinal(live.Profile, liveStock.Value) == L1BRowOrdinal(frozen.Profile, frozenStock.Value) &&
                liveStock.Value.units == frozenStock.Value.units && liveStock.Value.deliveryPercent == frozenStock.Value.deliveryPercent,
                "frozen Stock oracle matches unchanged live cold exact lookup");

            var lQuote = L1BCapture(() => PartsMarket.Quote(live.Profile, live.Definition, 9, false, 2));
            var fQuote = L1BCapture(() => LegacyScopedSupplyReference.Quote(frozen.Profile, frozen.Definition, 9, false, 2));
            log.Check(lQuote.Error == null && fQuote.Error == null && L1BQuoteText(lQuote.Value) == L1BQuoteText(fQuote.Value),
                "frozen Quote oracle matches unchanged live repeated-quantity quote");

            var lAvailable = L1BCapture(() => PartsMarket.Available(live.Profile, live.Definition));
            var fAvailable = L1BCapture(() => LegacyScopedSupplyReference.Available(frozen.Profile, frozen.Definition));
            log.Check(lAvailable.Error == null && fAvailable.Error == null && lAvailable.Value == fAvailable.Value,
                "frozen Available oracle matches unchanged live availability");

            var lLedgerFixture = L1BCreateFixture(); var fLedgerFixture = L1BCreateFixture();
            lLedgerFixture.Profile.inventory.market.parts.Add(L1BStock(lLedgerFixture.Definition.id));
            fLedgerFixture.Profile.inventory.market.parts.Add(L1BStock(fLedgerFixture.Definition.id));
            var liveLedger = new PartsMarket.Ledger(lLedgerFixture.Profile);
            var frozenLedger = new LegacyScopedSupplyReference.Ledger(fLedgerFixture.Profile);
            int ll = liveLedger.Available(lLedgerFixture.Definition), fl = frozenLedger.Available(fLedgerFixture.Definition);
            var lt = liveLedger.Take(lLedgerFixture.Definition, 7, 125, 3, false);
            var ft = frozenLedger.Take(fLedgerFixture.Definition, 7, 125, 3, false);
            log.Check(ll == fl && L1BQuoteText(lt) == L1BQuoteText(ft) && liveLedger.Available(lLedgerFixture.Definition) == frozenLedger.Available(fLedgerFixture.Definition),
                "frozen normal Ledger constructor/Find/Available/Take closure matches unchanged live behavior");

            var lConsume = L1BCreateFixture(); var fConsume = L1BCreateFixture();
            lConsume.Profile.inventory.market.parts.Add(L1BStock(lConsume.Definition.id));
            fConsume.Profile.inventory.market.parts.Add(L1BStock(fConsume.Definition.id));
            L1BSetReservation(lConsume, 8, new ReservedPartSupply { definitionId = lConsume.Definition.id, units = 4, generation = 4 });
            L1BSetReservation(fConsume, 8, new ReservedPartSupply { definitionId = fConsume.Definition.id, units = 4, generation = 4 });
            lConsume.Profile.inventory.usingCartReservation = fConsume.Profile.inventory.usingCartReservation = true;
            var lConsumed = L1BCapture(() => PartsMarket.Consume(lConsume.Profile, lConsume.Definition, 5));
            var fConsumed = L1BCapture(() => LegacyScopedSupplyReference.Consume(fConsume.Profile, fConsume.Definition, 5));
            log.Check(lConsumed.Error == null && fConsumed.Error == null && L1BQuoteText(lConsumed.Value) == L1BQuoteText(fConsumed.Value) &&
                lConsume.Profile.inventory.market.parts[0].units == fConsume.Profile.inventory.market.parts[0].units &&
                lConsume.Profile.inventory.cart.supply[0].units == fConsume.Profile.inventory.cart.supply[0].units,
                "frozen Consume oracle matches unchanged live quote and stock/hold mutations on independent fixtures");

            foreach (var vector in new[] { Tuple.Create(0, 10, 4, 25, 3), Tuple.Create(12, 5, 9, 101, 2), Tuple.Create(2, 10, 5, 1, 4) })
            {
                var actual = L1BCapture(() => PartsMarket.Calculate(vector.Item1, vector.Item2, vector.Item3, vector.Item4, vector.Item5));
                var oracle = L1BCapture(() => LegacyScopedSupplyReference.Calculate(vector.Item1, vector.Item2, vector.Item3, vector.Item4, vector.Item5));
                log.Check(actual.Error == null && oracle.Error == null && L1BQuoteText(actual.Value) == L1BQuoteText(oracle.Value),
                    "frozen Calculate oracle matches unchanged live arithmetic for finite liveness vector");
            }
            foreach (int count in new[] { int.MinValue, -1, 0, 1, 7, int.MaxValue })
            {
                int livePrice = InventoryPricing.NewUnit(live.Definition, count);
                int frozenPrice = LegacyScopedSupplyReference.NewUnit(frozen.Definition, count);
                log.Check(livePrice == frozenPrice, "frozen NewUnit oracle matches unchanged live pricing for count class=" + L1BCountClass(count));
            }
            log.Note("oracle-liveness=actual and frozen operations ran on independently created minimal fixtures; no candidate method was used to build expected results");
        }

        static string L1BCountClass(int count)
            => count == int.MinValue ? "min" : count < 0 ? "negative" : count == 0 ? "zero" : count == 1 ? "one" : count == int.MaxValue ? "max" : "positive";

        static int L1BRowOrdinal(PlayerProfile profile, NewPartStock stock)
        {
            if (profile?.inventory?.market?.parts == null) return -1;
            for (int i = 0; i < profile.inventory.market.parts.Count; i++) if (ReferenceEquals(profile.inventory.market.parts[i], stock)) return i;
            return -1;
        }

        static void L1BStockMatrix(L1BCheckLog log)
        {
            var cold = L1BCreateFixture(); var coldRef = L1BCreateFixture();
            cold.Profile.inventory.market.parts.Add(L1BStock(cold.Definition.id));
            coldRef.Profile.inventory.market.parts.Add(L1BStock(coldRef.Definition.id));
            L1BCompareStock(log, "cold exact ID", cold, coldRef, cold.Definition.id);

            var alias = L1BCreateFixture(); var aliasRef = L1BCreateFixture();
            var requestAlias = L1BAddIdAlias(alias); var requestAliasRef = L1BAddIdAlias(aliasRef);
            alias.Profile.inventory.market.parts.Add(L1BStock(alias.Definition.id));
            aliasRef.Profile.inventory.market.parts.Add(L1BStock(aliasRef.Definition.id));
            L1BCompareStock(log, "cold product alias", alias, aliasRef, requestAlias.id, requestAliasRef.id);

            var ordered = L1BCreateFixture(); var orderedRef = L1BCreateFixture();
            var orderedRequest = L1BAddIdAlias(ordered); var orderedRequestRef = L1BAddIdAlias(orderedRef);
            var secondAlias = ordered.Definition.Copy(); secondAlias.id += "/second"; ordered.Profile.catalog.parts.Add(secondAlias);
            var secondAliasRef = orderedRef.Definition.Copy(); secondAliasRef.id += "/second"; orderedRef.Profile.catalog.parts.Add(secondAliasRef);
            ordered.Profile.inventory.market.parts.Add(L1BStock(ordered.Definition.id));
            ordered.Profile.inventory.market.parts.Add(L1BStock(secondAlias.id));
            orderedRef.Profile.inventory.market.parts.Add(L1BStock(orderedRef.Definition.id));
            orderedRef.Profile.inventory.market.parts.Add(L1BStock(secondAliasRef.id));
            L1BCompareStock(log, "cold alias uses first stock row", ordered, orderedRef, orderedRequest.id, orderedRequestRef.id);

            var reversed = L1BCreateFixture(); var reversedRef = L1BCreateFixture();
            var reversedRequest = L1BAddIdAlias(reversed); var reversedRequestRef = L1BAddIdAlias(reversedRef);
            var reversedSecond = reversed.Definition.Copy(); reversedSecond.id += "/second"; reversed.Profile.catalog.parts.Add(reversedSecond);
            var reversedSecondRef = reversedRef.Definition.Copy(); reversedSecondRef.id += "/second"; reversedRef.Profile.catalog.parts.Add(reversedSecondRef);
            reversed.Profile.inventory.market.parts.Add(L1BStock(reversedSecond.id));
            reversed.Profile.inventory.market.parts.Add(L1BStock(reversed.Definition.id));
            reversedRef.Profile.inventory.market.parts.Add(L1BStock(reversedSecondRef.id));
            reversedRef.Profile.inventory.market.parts.Add(L1BStock(reversedRef.Definition.id));
            L1BCompareStock(log, "reversed rows change first alias result", reversed, reversedRef, reversedRequest.id, reversedRequestRef.id);

            var exactWins = L1BCreateFixture(); var exactWinsRef = L1BCreateFixture();
            var earlierAlias = L1BAddIdAlias(exactWins, "/earlier"); var earlierAliasRef = L1BAddIdAlias(exactWinsRef, "/earlier");
            exactWins.Profile.inventory.market.parts.Add(L1BStock(earlierAlias.id));
            exactWins.Profile.inventory.market.parts.Add(L1BStock(exactWins.Definition.id));
            exactWinsRef.Profile.inventory.market.parts.Add(L1BStock(earlierAliasRef.id));
            exactWinsRef.Profile.inventory.market.parts.Add(L1BStock(exactWinsRef.Definition.id));
            L1BCompareStock(log, "exact ID wins over earlier equivalent row", exactWins, exactWinsRef, exactWins.Definition.id);

            L1BStockWarmComparerAndForeignRow(log);
            L1BStockColdDuplicatePrefix(log);
            L1BStockMissRemainsUncached(log);
            L1BStockMalformedWarmShapes(log);
            L1BStockErrorOrdering(log);
        }

        static void L1BCompareStock(L1BCheckLog log, string label, L1BFixture candidate, L1BFixture reference, string candidateId, string referenceId = null)
        {
            referenceId = referenceId ?? candidateId;
            var requested = L1BFindFixtureDefinition(candidate, candidateId);
            var legacyRequest = L1BFindFixtureDefinition(reference, referenceId);
            var expected = L1BCapture(() => LegacyScopedSupplyReference.Stock(reference.Profile, legacyRequest));
            var before = L1BSourceState(candidate.Profile, requested);
            L1BOutcome<StockResolution> actual;
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                actual = L1BMeasure(log, "Stock / " + label, () => scope.Stock(requested));
                var after = L1BSourceState(candidate.Profile, requested);
                log.Check(before.Same(after), "Stock preserves all watched source refs/values/index/catalog state / " + label);
            }
            L1BCompareErrors(log, "Stock exact result/error / " + label, actual.Error, expected.Error);
            if (actual.Error != null || expected.Error != null) return;
            var result = actual.Value;
            int ordinal = L1BRowOrdinal(reference.Profile, expected.Value);
            int units = expected.Value == null ? 0 : expected.Value.units;
            int initial = expected.Value == null ? 0 : expected.Value.initialUnits;
            int delivery = expected.Value == null ? 10 : expected.Value.deliveryPercent;
            log.Check(result.Found == (expected.Value != null) && result.RowOrdinal == ordinal && result.Units == units &&
                result.InitialUnits == initial && result.DeliveryPercent == delivery,
                "Stock selected same row position and stock scalars / " + label + ";candidate=" + result.RowOrdinal + ";oracle=" + ordinal);
            var positions = result.Positions;
            var expectedPositions = L1BStockPositionsAtOrdinal(candidate.Profile, result.RowOrdinal);
            bool positionsMatch = positions.SequenceEqual(expectedPositions);
            log.Check(positionsMatch, "Stock result positions are exact and defensive / " + label);
            if (positions.Length > 0) positions[0] = int.MinValue;
            log.Check(result.Positions.SequenceEqual(expectedPositions),
                "Stock result position array cannot mutate later output / " + label);
            var hasBorrowedField = typeof(StockResolution).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => typeof(PlayerProfile).IsAssignableFrom(field.FieldType) || typeof(MechanicalCatalog).IsAssignableFrom(field.FieldType) ||
                    typeof(PartDefinition).IsAssignableFrom(field.FieldType) || typeof(NewPartStock).IsAssignableFrom(field.FieldType) ||
                    typeof(ReservedPartSupply).IsAssignableFrom(field.FieldType));
            log.Check(!hasBorrowedField, "StockResolution exposes no borrowed profile/catalog/definition/stock/hold reference / " + label);
        }

        static int[] L1BStockPositions(PlayerProfile profile, int rowOrdinal, NewPartStock reference)
        {
            if (profile.inventory.market.parts == null) return Array.Empty<int>();
            if (reference == null) return Array.Empty<int>();
            var positions = new List<int>();
            for (int i = 0; i < profile.inventory.market.parts.Count; i++) if (ReferenceEquals(profile.inventory.market.parts[i], reference)) positions.Add(i);
            return positions.ToArray();
        }

        static int[] L1BStockPositionsAtOrdinal(PlayerProfile profile, int rowOrdinal)
        {
            if (rowOrdinal < 0 || profile?.inventory?.market?.parts == null || rowOrdinal >= profile.inventory.market.parts.Count) return Array.Empty<int>();
            return L1BStockPositions(profile, rowOrdinal, profile.inventory.market.parts[rowOrdinal]);
        }

        static PartDefinition L1BFindFixtureDefinition(L1BFixture fixture, string id)
        {
            if (fixture.Definition != null && fixture.Definition.id == id) return fixture.Definition;
            if (fixture.Alias != null && fixture.Alias.id == id) return fixture.Alias;
            return fixture.Profile.catalog.parts.FirstOrDefault(value => value != null && value.id == id);
        }

        static void L1BStockWarmComparerAndForeignRow(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            var alias = L1BAddIdAlias(candidate, "/case-alias"); var aliasRef = L1BAddIdAlias(reference, "/case-alias");
            alias.id = alias.id.ToUpperInvariant(); aliasRef.id = aliasRef.id.ToUpperInvariant();
            var row = L1BStock(candidate.Definition.id); var rowRef = L1BStock(reference.Definition.id);
            candidate.Profile.inventory.market.parts.Add(row); reference.Profile.inventory.market.parts.Add(rowRef);
            candidate.Profile.inventory.market.index = new Dictionary<string, NewPartStock>(StringComparer.OrdinalIgnoreCase) { [candidate.Definition.id] = row };
            reference.Profile.inventory.market.index = new Dictionary<string, NewPartStock>(StringComparer.OrdinalIgnoreCase) { [reference.Definition.id] = rowRef };
            L1BCompareStock(log, "warm index retains its actual case-insensitive comparer", candidate, reference, alias.id, aliasRef.id);

            var foreign = L1BCreateFixture(); var foreignRef = L1BCreateFixture();
            var detached = L1BStock(foreign.Definition.id); var detachedRef = L1BStock(foreignRef.Definition.id);
            foreign.Profile.inventory.market.index = new Dictionary<string, NewPartStock>(StringComparer.Ordinal) { [foreign.Definition.id] = detached };
            foreignRef.Profile.inventory.market.index = new Dictionary<string, NewPartStock>(StringComparer.Ordinal) { [foreignRef.Definition.id] = detachedRef };
            L1BCompareStock(log, "warm index may resolve a foreign row absent from market list", foreign, foreignRef, foreign.Definition.id);

            var repeated = L1BCreateFixture(); var repeatedRef = L1BCreateFixture();
            var repeatedRow = L1BStock(repeated.Definition.id); var repeatedRowRef = L1BStock(repeatedRef.Definition.id);
            repeated.Profile.inventory.market.parts.Add(repeatedRow); repeated.Profile.inventory.market.parts.Add(repeatedRow);
            repeatedRef.Profile.inventory.market.parts.Add(repeatedRowRef); repeatedRef.Profile.inventory.market.parts.Add(repeatedRowRef);
            repeated.Profile.inventory.market.index = new Dictionary<string, NewPartStock> { [repeated.Definition.id] = repeatedRow };
            repeatedRef.Profile.inventory.market.index = new Dictionary<string, NewPartStock> { [repeatedRef.Definition.id] = repeatedRowRef };
            L1BCompareStock(log, "warm row identity appears at repeated source positions", repeated, repeatedRef, repeated.Definition.id);
            using (var scope = ScopedMarketReadSet.OpenBorrowed(repeated.Profile))
            {
                var first = L1BMeasure(log, "stock repeated reference identity first", () => scope.Stock(repeated.Definition));
                var second = L1BMeasure(log, "stock repeated reference identity second", () => scope.Stock(repeated.Definition));
                log.Check(first.Error == null && second.Error == null && first.Value.IdentityGroup == second.Value.IdentityGroup &&
                    first.Value.Positions.SequenceEqual(new[] { 0, 1 }), "same stock reference maps to one private identity group and both positions");
            }
        }

        static void L1BStockColdDuplicatePrefix(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            candidate.Profile.inventory.market.parts.Add(L1BStock(candidate.Definition.id));
            candidate.Profile.inventory.market.parts.Add(L1BStock(candidate.Definition.id));
            reference.Profile.inventory.market.parts.Add(L1BStock(reference.Definition.id));
            reference.Profile.inventory.market.parts.Add(L1BStock(reference.Definition.id));
            var expected = L1BCapture(() => LegacyScopedSupplyReference.Stock(reference.Profile, reference.Definition));
            var before = L1BSourceState(candidate.Profile, candidate.Definition);
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var first = L1BMeasure(log, "cold duplicate index exact first exception", () => scope.Stock(candidate.Definition));
                L1BCompareErrors(log, "cold duplicate ID insertion exception", first.Error, expected.Error);
                var prefix = L1BMeasure(log, "partial private index prefix remains reusable", () => scope.Stock(candidate.Definition));
                var expectedPrefix = LegacyScopedSupplyReference.Stock(reference.Profile, reference.Definition);
                log.Check(prefix.Error == null && prefix.Value.Found && prefix.Value.RowOrdinal == 0,
                    "captured partial private Stock index serves its inserted prefix after caught failure");
                log.Check(expectedPrefix == reference.Profile.inventory.market.parts[0],
                    "frozen Stock reference reuses assigned partial source index after caught failure");
                var after = L1BSourceState(candidate.Profile, candidate.Definition);
                log.Check(before.Same(after) && candidate.Profile.inventory.market.index == null,
                    "cold duplicate failure leaves only the private partial prefix and keeps source index null");
            }
        }

        static void L1BStockMissRemainsUncached(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            var candidateAlias = L1BAddIdAlias(candidate, "/late-stock-alias");
            var referenceAlias = L1BAddIdAlias(reference, "/late-stock-alias");
            var other = candidate.Profile.catalog.parts.First(value => value != null && value.category == PartCategory.Rods);
            var otherRef = reference.Profile.catalog.parts.First(value => value != null && value.category == PartCategory.Rods);
            candidate.Profile.inventory.market.parts.Add(L1BStock(other.id));
            reference.Profile.inventory.market.parts.Add(L1BStock(otherRef.id));
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var first = L1BMeasure(log, "cold product miss is uncached / first query", () => scope.Stock(candidateAlias));
                var firstReference = LegacyScopedSupplyReference.Stock(reference.Profile, referenceAlias);
                log.Check(first.Error == null && !first.Value.Found && firstReference == null,
                    "first missing product lookup returns no stock and does not synthesize a row");
                candidate.Profile.inventory.market.parts.Add(L1BStock(candidate.Definition.id));
                reference.Profile.inventory.market.parts.Add(L1BStock(reference.Definition.id));
                var before = L1BSourceState(candidate.Profile, candidateAlias, candidate.Definition);
                var second = L1BMeasure(log, "cold product miss rescans for new matching stock", () => scope.Stock(candidateAlias));
                var secondReference = LegacyScopedSupplyReference.Stock(reference.Profile, referenceAlias);
                log.Check(second.Error == null && second.Value.Found && second.Error == null &&
                    second.Value.RowOrdinal == L1BRowOrdinal(reference.Profile, secondReference),
                    "uncached alias miss observes subsequently-added matching market row just like frozen Stock");
                log.Check(before.Same(L1BSourceState(candidate.Profile, candidateAlias, candidate.Definition)),
                    "uncached candidate rescan does not publish alias into source market index or mutate source rows");
            }
        }

        static void L1BStockMalformedWarmShapes(L1BCheckLog log)
        {
            Action<L1BFixture, L1BFixture, string> compare = (candidate, reference, name) =>
            {
                var cRow = L1BStock(candidate.Definition.id); var rRow = L1BStock(reference.Definition.id);
                candidate.Profile.inventory.market.index = new Dictionary<string, NewPartStock> { [candidate.Definition.id] = cRow };
                reference.Profile.inventory.market.index = new Dictionary<string, NewPartStock> { [reference.Definition.id] = rRow };
                L1BCompareStock(log, name, candidate, reference, candidate.Definition.id);
            };
            var nullParts = L1BCreateFixture(); var nullPartsRef = L1BCreateFixture();
            nullParts.Profile.inventory.market.parts = null; nullPartsRef.Profile.inventory.market.parts = null; compare(nullParts, nullPartsRef, "warm exact hit with null market.parts");
            var nullRow = L1BCreateFixture(); var nullRowRef = L1BCreateFixture();
            nullRow.Profile.inventory.market.parts.Add(null); nullRowRef.Profile.inventory.market.parts.Add(null); compare(nullRow, nullRowRef, "warm exact hit with null market row");
            var nullDefinitions = L1BCreateFixture(); var nullDefinitionsRef = L1BCreateFixture();
            var cStock = L1BStock(nullDefinitions.Definition.id); var rStock = L1BStock(nullDefinitionsRef.Definition.id);
            cStock.definitions = null; rStock.definitions = null;
            nullDefinitions.Profile.inventory.market.parts.Add(cStock); nullDefinitionsRef.Profile.inventory.market.parts.Add(rStock);
            compare(nullDefinitions, nullDefinitionsRef, "warm exact hit with null row definitions");
        }

        static void L1BStockErrorOrdering(L1BCheckLog log)
        {
            var coldNullParts = L1BCreateFixture(); var coldNullPartsRef = L1BCreateFixture();
            coldNullParts.Profile.inventory.market.parts = null; coldNullPartsRef.Profile.inventory.market.parts = null;
            L1BCompareStock(log, "cold null parts foreach ordering", coldNullParts, coldNullPartsRef, coldNullParts.Definition.id);
            var coldNullDefinitions = L1BCreateFixture(); var coldNullDefinitionsRef = L1BCreateFixture();
            coldNullDefinitions.Profile.inventory.market.parts.Add(L1BStock()); coldNullDefinitionsRef.Profile.inventory.market.parts.Add(L1BStock());
            coldNullDefinitions.Profile.inventory.market.parts[0].definitions = null; coldNullDefinitionsRef.Profile.inventory.market.parts[0].definitions = null;
            L1BCompareStock(log, "cold null definitions foreach ordering", coldNullDefinitions, coldNullDefinitionsRef, coldNullDefinitions.Definition.id);
            var warmNullParts = L1BCreateFixture(); var warmNullPartsRef = L1BCreateFixture();
            warmNullParts.Profile.inventory.market.parts = null; warmNullPartsRef.Profile.inventory.market.parts = null;
            warmNullParts.Profile.inventory.market.index = new Dictionary<string, NewPartStock>();
            warmNullPartsRef.Profile.inventory.market.index = new Dictionary<string, NewPartStock>();
            L1BCompareStock(log, "warm miss null parts LINQ source error", warmNullParts, warmNullPartsRef, warmNullParts.Definition.id);
            var warmNullDefinitions = L1BCreateFixture(); var warmNullDefinitionsRef = L1BCreateFixture();
            warmNullDefinitions.Profile.inventory.market.index = new Dictionary<string, NewPartStock>();
            warmNullDefinitionsRef.Profile.inventory.market.index = new Dictionary<string, NewPartStock>();
            warmNullDefinitions.Profile.inventory.market.parts.Add(L1BStock()); warmNullDefinitionsRef.Profile.inventory.market.parts.Add(L1BStock());
            warmNullDefinitions.Profile.inventory.market.parts[0].definitions = null; warmNullDefinitionsRef.Profile.inventory.market.parts[0].definitions = null;
            L1BCompareStock(log, "warm miss null definitions LINQ source error", warmNullDefinitions, warmNullDefinitionsRef, warmNullDefinitions.Definition.id);
        }

        static void L1BCatalogLookupMatrix(L1BCheckLog log)
        {
            var cold = L1BCreateFixture(); var coldRef = L1BCreateFixture();
            L1BCatalogLookupParity(log, "cold exact lookup", cold, coldRef, cold.Definition.id);

            var warm = L1BCreateFixture(); var warmRef = L1BCreateFixture();
            warm.Profile.catalog.RebuildIndexes(); warmRef.Profile.catalog.RebuildIndexes();
            L1BCatalogLookupParity(log, "warm valid index hit", warm, warmRef, warm.Definition.id);

            var stale = L1BCreateFixture(); var staleRef = L1BCreateFixture();
            stale.Profile.catalog.RebuildIndexes(); staleRef.Profile.catalog.RebuildIndexes();
            var staleAlias = L1BAddIdAlias(stale, "/catalog-stale"); var staleAliasRef = L1BAddIdAlias(staleRef, "/catalog-stale");
            L1BCatalogLookupParity(log, "stale indexed count privately rebuilds", stale, staleRef, staleAlias.id, staleAliasRef.id);

            var moved = L1BCreateFixture(); var movedRef = L1BCreateFixture();
            moved.Profile.catalog.RebuildIndexes(); movedRef.Profile.catalog.RebuildIndexes();
            var movedOriginal = moved.Definition; var movedOriginalRef = movedRef.Definition;
            moved.Profile.catalog.parts.Remove(movedOriginal); moved.Profile.catalog.parts.Add(movedOriginal);
            movedRef.Profile.catalog.parts.Remove(movedOriginalRef); movedRef.Profile.catalog.parts.Add(movedOriginalRef);
            L1BCatalogLookupParity(log, "moved indexed definition falls back to current list position", moved, movedRef, movedOriginal.id);

            var replaced = L1BCreateFixture(); var replacedRef = L1BCreateFixture();
            string replacedId = replaced.Definition.id; string replacedIdRef = replacedRef.Definition.id;
            replaced.Profile.catalog.RebuildIndexes(); replacedRef.Profile.catalog.RebuildIndexes();
            var substitute = replaced.Definition.Copy(); substitute.id += "/replacement";
            var substituteRef = replacedRef.Definition.Copy(); substituteRef.id += "/replacement";
            int replacedPosition = L1BCatalogPosition(replaced.Profile.catalog, replaced.Definition);
            int replacedPositionRef = L1BCatalogPosition(replacedRef.Profile.catalog, replacedRef.Definition);
            replaced.Profile.catalog.parts[replacedPosition] = substitute;
            replacedRef.Profile.catalog.parts[replacedPositionRef] = substituteRef;
            L1BCatalogLookupParity(log, "replaced indexed value misses after stale fallback", replaced, replacedRef, replacedId, replacedIdRef);

            var mismatched = L1BCreateFixture(); var mismatchedRef = L1BCreateFixture();
            string oldId = mismatched.Definition.id; string oldIdRef = mismatchedRef.Definition.id;
            mismatched.Profile.catalog.RebuildIndexes(); mismatchedRef.Profile.catalog.RebuildIndexes();
            mismatched.Definition.id += "/mutated"; mismatchedRef.Definition.id += "/mutated";
            L1BCatalogLookupParity(log, "warm value ID mismatch privately repairs index", mismatched, mismatchedRef, oldId, oldIdRef);

            var duplicate = L1BCreateFixture(); var duplicateRef = L1BCreateFixture();
            duplicate.Profile.catalog.parts.Add(duplicate.Definition.Copy()); duplicateRef.Profile.catalog.parts.Add(duplicateRef.Definition.Copy());
            L1BCatalogLookupParity(log, "duplicate IDs map first occurrence", duplicate, duplicateRef, duplicate.Definition.id);

            var nullRow = L1BCreateFixture(); var nullRowRef = L1BCreateFixture();
            nullRow.Profile.catalog.parts.Insert(0, null); nullRowRef.Profile.catalog.parts.Insert(0, null);
            L1BCatalogLookupParity(log, "cold null row rebuild error", nullRow, nullRowRef, nullRow.Definition.id);

            var nullId = L1BCreateFixture(); var nullIdRef = L1BCreateFixture();
            nullId.Profile.catalog.parts[0].id = null; nullIdRef.Profile.catalog.parts[0].id = null;
            L1BCatalogLookupParity(log, "cold null ID rebuild error", nullId, nullIdRef, nullId.Definition.id);

            var authoring = L1BCreateFixture(); var authoringRef = L1BCreateFixture();
            L1BSetCatalogAuthoring(authoring.Profile.catalog, true); L1BSetCatalogAuthoring(authoringRef.Profile.catalog, true);
            L1BCatalogLookupParity(log, "authoring mode uses list scan without publication", authoring, authoringRef, authoring.Definition.id);
        }

        static void L1BSetCatalogAuthoring(MechanicalCatalog catalog, bool value)
        {
            var field = typeof(MechanicalCatalog).GetField("authoring", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(typeof(MechanicalCatalog).FullName, "authoring");
            field.SetValue(catalog, value);
        }

        static void L1BCatalogLookupParity(L1BCheckLog log, string label, L1BFixture candidate, L1BFixture reference,
            string candidateId, string referenceId = null)
        {
            referenceId = referenceId ?? candidateId;
            var before = L1BSourceState(candidate.Profile, candidate.Definition);
            var expected = L1BCapture(() => reference.Profile.catalog.Find(referenceId));
            var actual = L1BCapture(() =>
            {
                using (var lookup = candidate.Profile.catalog.OpenLookupScope())
                {
                    var value = lookup.FindDefinition(candidateId, out bool found, out int position, out int identityGroup);
                    lookup.AssertSourceUnchanged();
                    return Tuple.Create(value, found, position, identityGroup);
                }
            });
            if (actual.Error != null || expected.Error != null)
            {
                L1BCompareErrors(log, "private catalog lookup exact error / " + label, actual.Error, expected.Error);
                log.Check(actual.Error != null && expected.Error != null && actual.Value == null && expected.Value == null,
                    "malformed catalog lookup returns no result when unchanged Find errors / " + label);
            }
            else
            {
                log.Check(actual.Value.Item2 == (expected.Value != null) &&
                    actual.Value.Item3 == L1BCatalogPosition(candidate.Profile.catalog, actual.Value.Item1) &&
                    actual.Value.Item3 == L1BCatalogPosition(reference.Profile.catalog, expected.Value) &&
                    actual.Value.Item4 == (expected.Value == null ? -1 : 0),
                    "private catalog lookup matches unchanged Find presence/position and identity-group shape / " + label);
            }
            log.Check(before.Same(L1BSourceState(candidate.Profile, candidate.Definition)),
                "catalog lookup leaves source list, index, revision, and authored values unchanged / " + label);
        }

        static int L1BCatalogPosition(MechanicalCatalog catalog, PartDefinition definition)
        {
            if (catalog?.parts == null || definition == null) return -1;
            for (int i = 0; i < catalog.parts.Count; i++) if (ReferenceEquals(catalog.parts[i], definition)) return i;
            return -1;
        }

        static void L1BLedgerMatrix(L1BCheckLog log)
        {
            L1BLedgerLastExactIDWins(log);
            L1BLedgerFirstProductRowAndSharedRemainder(log);
            L1BLedgerCachedNullMiss(log);
            L1BLedgerConstructorErrors(log);
            L1BLedgerConsumeOverlayAndTakePublication(log);
        }

        static void L1BLedgerLastExactIDWins(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            candidate.Profile.inventory.market.parts.Add(new NewPartStock { definitions = new List<string> { candidate.Definition.id }, units = 3, initialUnits = 3, deliveryPercent = 5 });
            candidate.Profile.inventory.market.parts.Add(new NewPartStock { definitions = new List<string> { candidate.Definition.id }, units = 8, initialUnits = 8, deliveryPercent = 9 });
            reference.Profile.inventory.market.parts.Add(new NewPartStock { definitions = new List<string> { reference.Definition.id }, units = 3, initialUnits = 3, deliveryPercent = 5 });
            reference.Profile.inventory.market.parts.Add(new NewPartStock { definitions = new List<string> { reference.Definition.id }, units = 8, initialUnits = 8, deliveryPercent = 9 });
            var expectedLedger = new LegacyScopedSupplyReference.Ledger(reference.Profile);
            var before = L1BSourceState(candidate.Profile, candidate.Definition);
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var ledger = scope.OpenDefaultLedger();
                var available = L1BMeasure(log, "normal Ledger later duplicate ID wins", () => ledger.Available(candidate.Definition));
                int expectedAvailable = expectedLedger.Available(reference.Definition);
                var quote = L1BMeasure(log, "normal Ledger exact quote", () => ledger.Quote(candidate.Definition, 6, 125, 2));
                var expectedQuote = expectedLedger.Quote(reference.Definition, 6, 125, 2);
                var take = L1BMeasure(log, "normal Ledger private take", () => ledger.Take(candidate.Definition, 6, 125, 2, false));
                var expectedTake = expectedLedger.Take(reference.Definition, 6, 125, 2, false);
                var availableAgain = L1BMeasure(log, "normal Ledger remainder after Take", () => ledger.Available(candidate.Definition));
                log.Check(available.Error == null && available.Value == expectedAvailable && available.Value == 8,
                    "Ledger exact-ID assignment selects later duplicate row");
                log.Check(quote.Error == null && L1BSameQuote(quote.Value, expectedQuote), "Ledger Quote matches frozen result");
                log.Check(take.Error == null && L1BSameQuote(take.Value.Quote, expectedTake), "Ledger Take matches frozen result");
                log.Check(availableAgain.Error == null && availableAgain.Value == expectedLedger.Available(reference.Definition),
                    "Ledger Take updates only operation-local remainder");
                log.Check(take.Value.StockDelta == null, "non-consuming Ledger Take reports no source stock delta");
                log.Check(before.Same(L1BSourceState(candidate.Profile, candidate.Definition)), "Ledger construction and Take leave source profile/catalog/index unchanged");
            }
        }

        static void L1BLedgerFirstProductRowAndSharedRemainder(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            var candidateRequest = L1BAddIdAlias(candidate); var referenceRequest = L1BAddIdAlias(reference);
            var candidateSecond = candidate.Definition.Copy(); candidateSecond.id += "/second"; candidate.Profile.catalog.parts.Add(candidateSecond);
            var referenceSecond = reference.Definition.Copy(); referenceSecond.id += "/second"; reference.Profile.catalog.parts.Add(referenceSecond);
            candidate.Profile.inventory.market.parts.Add(new NewPartStock { definitions = new List<string> { candidate.Definition.id }, units = 6, initialUnits = 6, deliveryPercent = 6 });
            candidate.Profile.inventory.market.parts.Add(new NewPartStock { definitions = new List<string> { candidateSecond.id }, units = 11, initialUnits = 11, deliveryPercent = 10 });
            reference.Profile.inventory.market.parts.Add(new NewPartStock { definitions = new List<string> { reference.Definition.id }, units = 6, initialUnits = 6, deliveryPercent = 6 });
            reference.Profile.inventory.market.parts.Add(new NewPartStock { definitions = new List<string> { referenceSecond.id }, units = 11, initialUnits = 11, deliveryPercent = 10 });
            var expectedLedger = new LegacyScopedSupplyReference.Ledger(reference.Profile);
            var before = L1BSourceState(candidate.Profile, candidate.Definition, candidateRequest, candidateSecond);
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var ledger = scope.OpenDefaultLedger();
                var aliasAvailable = L1BMeasure(log, "Ledger first exact product class row", () => ledger.Available(candidateRequest));
                int expectedAlias = expectedLedger.Available(referenceRequest);
                var baseTake = L1BMeasure(log, "Ledger first alias row private remainder", () => ledger.Take(candidate.Definition, 4, 31, 1, false));
                var aliasAvailableAfter = L1BMeasure(log, "Ledger alias shares first row remainder", () => ledger.Available(candidateRequest));
                var aliasTake = L1BMeasure(log, "Ledger alias consumes shared remainder", () => ledger.Take(candidateRequest, 4, 31, 1, false));
                var expectedBaseTake = expectedLedger.Take(reference.Definition, 4, 31, 1, false);
                var expectedAliasAfter = expectedLedger.Available(referenceRequest);
                var expectedAliasTake = expectedLedger.Take(referenceRequest, 4, 31, 1, false);
                log.Check(aliasAvailable.Error == null && aliasAvailable.Value == expectedAlias && aliasAvailable.Value == 6,
                    "Ledger product map selects first row for exact supported product class");
                log.Check(baseTake.Error == null && L1BSameQuote(baseTake.Value.Quote, expectedBaseTake), "base-ID Take result matches frozen Ledger");
                log.Check(aliasAvailableAfter.Error == null && aliasAvailableAfter.Value == expectedAliasAfter && aliasAvailableAfter.Value == 2,
                    "base and alias queries share a single operation-local remainder");
                log.Check(aliasTake.Error == null && L1BSameQuote(aliasTake.Value.Quote, expectedAliasTake) && aliasTake.Value.StockDelta == null,
                    "non-consuming alias Take updates only the first product row remainder");
                log.Check(before.Same(L1BSourceState(candidate.Profile, candidate.Definition, candidateRequest, candidateSecond)),
                    "Ledger product resolution and alias deltas never write source catalog or stock");
            }
        }

        static void L1BLedgerCachedNullMiss(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            var missing = candidate.Profile.catalog.parts.First(value => value != null && value.category == PartCategory.Rods);
            var missingRef = reference.Profile.catalog.parts.First(value => value != null && value.category == PartCategory.Rods);
            candidate.Profile.inventory.market.parts.Add(L1BStock(candidate.Definition.id));
            reference.Profile.inventory.market.parts.Add(L1BStock(reference.Definition.id));
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var ledger = scope.OpenDefaultLedger();
                var first = L1BMeasure(log, "Ledger caches null product miss", () => ledger.Available(missing));
                var expectedLedger = new LegacyScopedSupplyReference.Ledger(reference.Profile);
                int expectedFirst = expectedLedger.Available(missingRef);
                log.Check(first.Error == null && first.Value == expectedFirst && first.Value == 0, "first cold Ledger product miss returns zero");
                candidate.Profile.inventory.market.parts.Add(L1BStock(missing.id));
                reference.Profile.inventory.market.parts.Add(L1BStock(missingRef.id));
                var repeated = L1BMeasure(log, "same Ledger retains cached null miss", () => ledger.Available(missing));
                var expectedRepeated = expectedLedger.Available(missingRef);
                using (var freshScope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
                {
                    var freshLedger = freshScope.OpenDefaultLedger();
                    var fresh = L1BMeasure(log, "fresh Ledger observes added matching product", () => freshLedger.Available(missing));
                    var freshExpectedLedger = new LegacyScopedSupplyReference.Ledger(reference.Profile);
                    int freshExpected = freshExpectedLedger.Available(missingRef);
                    log.Check(fresh.Error == null && fresh.Value == freshExpected && fresh.Value > 0,
                        "fresh Ledger observes newly-added stock that old Ledger cached as a miss");
                }
                log.Check(repeated.Error == null && repeated.Value == expectedRepeated && repeated.Value == 0,
                    "existing Ledger preserves its cached null miss after source change");
            }
        }

        static void L1BLedgerConstructorErrors(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            L1BSetReservation(candidate, 9, new ReservedPartSupply { definitionId = candidate.Definition.id, units = 1, generation = 4 });
            L1BSetReservation(reference, 9, new ReservedPartSupply { definitionId = reference.Definition.id, units = 1, generation = 4 });
            candidate.Profile.inventory.market.parts.Add(L1BStock()); reference.Profile.inventory.market.parts.Add(L1BStock());
            candidate.Profile.inventory.market.parts[0].definitions.Clear(); reference.Profile.inventory.market.parts[0].definitions.Clear();
            var expected = L1BCapture(() => new LegacyScopedSupplyReference.Ledger(reference.Profile));
            var before = L1BSourceState(candidate.Profile, candidate.Definition);
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var actual = L1BMeasure(log, "default Ledger eager constructor preamble error", () => scope.OpenDefaultLedger());
                L1BCompareErrors(log, "active default Ledger empty-definition first error", actual.Error, expected.Error);
                log.Check(before.Same(L1BSourceState(candidate.Profile, candidate.Definition)), "failed default Ledger construction leaves source graph unchanged");
            }
        }

        static void L1BLedgerConsumeOverlayAndTakePublication(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            var cStock = L1BStock(candidate.Definition.id); var rStock = L1BStock(reference.Definition.id);
            cStock.units = cStock.initialUnits = rStock.units = rStock.initialUnits = 10;
            candidate.Profile.inventory.market.parts.Add(cStock); reference.Profile.inventory.market.parts.Add(rStock);
            L1BSetReservation(candidate, 10,
                new ReservedPartSupply { definitionId = candidate.Definition.id, units = 3, generation = 2 },
                new ReservedPartSupply { definitionId = candidate.Definition.id, units = 4, generation = 4 });
            L1BSetReservation(reference, 10,
                new ReservedPartSupply { definitionId = reference.Definition.id, units = 3, generation = 2 },
                new ReservedPartSupply { definitionId = reference.Definition.id, units = 4, generation = 4 });
            candidate.Profile.inventory.usingCartReservation = reference.Profile.inventory.usingCartReservation = true;
            var sourceBefore = L1BSourceState(candidate.Profile, candidate.Definition);
            var expectedConsume = LegacyScopedSupplyReference.Consume(reference.Profile, reference.Definition, 5);
            var expectedLedger = new LegacyScopedSupplyReference.Ledger(reference.Profile);
            int expectedInitialLedger = expectedLedger.Available(reference.Definition);
            var expectedLocalTake = expectedLedger.Take(reference.Definition, 2, 0, 1, false);
            var expectedAfterLocalTake = expectedLedger.Available(reference.Definition);
            var expectedSecondLedger = new LegacyScopedSupplyReference.Ledger(reference.Profile);
            int expectedSecondInitial = expectedSecondLedger.Available(reference.Definition);
            var expectedConsumingTake = expectedSecondLedger.Take(reference.Definition, 1, 0, 1, true);

            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var consume = L1BMeasure(log, "Consume overlay precedes default Ledger construction", () => scope.Consume(candidate.Definition, 5));
                log.Check(consume.Error == null && L1BSameQuote(consume.Value.Quote, expectedConsume),
                    "ordered private Consume matches frozen mutation before creating Ledger");
                var ledger = scope.OpenDefaultLedger();
                var initial = L1BMeasure(log, "new Ledger reads current scoped stock overlay", () => ledger.Available(candidate.Definition));
                log.Check(initial.Error == null && initial.Value == expectedInitialLedger && initial.Value == 6,
                    "new Ledger constructor subtracts current holds from the preceding Consume stock overlay");
                var takeLocal = L1BMeasure(log, "Ledger local Take without publication", () => ledger.Take(candidate.Definition, 2, 0, 1, false));
                log.Check(takeLocal.Error == null && L1BSameQuote(takeLocal.Value.Quote, expectedLocalTake) && takeLocal.Value.StockDelta == null,
                    "Take consume=false updates only Ledger remainder and returns no stock delta");
                var afterLocal = L1BMeasure(log, "Ledger remainder after non-consuming Take", () => ledger.Available(candidate.Definition));
                log.Check(afterLocal.Error == null && afterLocal.Value == expectedAfterLocalTake,
                    "private Ledger remainder reflects the non-consuming Take");
                var secondLedger = scope.OpenDefaultLedger();
                var secondInitial = L1BMeasure(log, "second Ledger has independent remainder over shared source overlay", () => secondLedger.Available(candidate.Definition));
                log.Check(secondInitial.Error == null && secondInitial.Value == expectedSecondInitial && secondInitial.Value == 6,
                    "second Ledger initializes from parent overlay and active current holds, independent of first Ledger remainder");
                var takeConsume = L1BMeasure(log, "second Ledger consuming Take reports exact overlay delta", () => secondLedger.Take(candidate.Definition, 1, 0, 1, true));
                var delta = takeConsume.Value == null ? null : takeConsume.Value.StockDelta;
                log.Check(takeConsume.Error == null && L1BSameQuote(takeConsume.Value.Quote, expectedConsumingTake) && delta != null &&
                    delta.Before == 8 && delta.After == 5 && delta.Positions.SequenceEqual(new[] { 0 }),
                    "Take consume=true starts from current scoped stock, then reports second Ledger remainder");
                log.Check(sourceBefore.Same(L1BSourceState(candidate.Profile, candidate.Definition)),
                    "Consume and both Ledger Take modes leave the borrowed market and holds unchanged");
            }
        }

        static void L1BReservationMatrix(L1BCheckLog log)
        {
            L1BHeldGenerationAndFlags(log);
            L1BActiveNullAndErrorOrder(log);
            L1BConsumeGenerationAndIdentityDeltas(log);
        }

        static void L1BHeldGenerationAndFlags(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            var alias = L1BAddIdAlias(candidate); var aliasRef = L1BAddIdAlias(reference);
            candidate.Profile.inventory.market.parts.Add(L1BStock(candidate.Definition.id, alias.id));
            reference.Profile.inventory.market.parts.Add(L1BStock(reference.Definition.id, aliasRef.id));
            candidate.Profile.inventory.market.generation = reference.Profile.inventory.market.generation = 4;
            L1BSetReservation(candidate, 12,
                new ReservedPartSupply { definitionId = alias.id, units = 2, generation = 3 },
                new ReservedPartSupply { definitionId = candidate.Definition.id, units = 3, generation = 4 });
            L1BSetReservation(reference, 12,
                new ReservedPartSupply { definitionId = aliasRef.id, units = 2, generation = 3 },
                new ReservedPartSupply { definitionId = reference.Definition.id, units = 3, generation = 4 });
            var expectedAll = LegacyScopedSupplyReference.Held(reference.Profile, reference.Definition, false);
            var expectedCurrent = LegacyScopedSupplyReference.Held(reference.Profile, reference.Definition, true);
            var beforeHeld = L1BSourceState(candidate.Profile, candidate.Definition, alias);
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var all = L1BMeasure(log, "Held all generations direct and stock alias IDs", () => scope.Held(candidate.Definition, false));
                var current = L1BMeasure(log, "Held current generation only", () => scope.Held(candidate.Definition, true));
                log.Check(all.Error == null && all.Value == expectedAll && all.Value == 5, "Held includes exact and resolved-row alias supply across generations");
                log.Check(current.Error == null && current.Value == expectedCurrent && current.Value == 3, "Held currentOnly filters by market generation");
                log.Check(beforeHeld.Same(L1BSourceState(candidate.Profile, candidate.Definition, alias)), "Held leaves initial source state unchanged");
                candidate.Profile.inventory.usingCartReservation = false;
                reference.Profile.inventory.usingCartReservation = false;
                var beforeWithout = L1BSourceState(candidate.Profile, candidate.Definition, alias);
                var without = L1BMeasure(log, "Quote without reservation flag", () => scope.Quote(candidate.Definition, 5, false, 1));
                var withoutExpected = LegacyScopedSupplyReference.Quote(reference.Profile, reference.Definition, 5, false, 1);
                log.Check(without.Error == null && L1BSameQuote(without.Value, withoutExpected), "Quote does not add holds when usingCartReservation is false");
                log.Check(beforeWithout.Same(L1BSourceState(candidate.Profile, candidate.Definition, alias)),
                    "Quote without reservation flag leaves its post-toggle source state unchanged");
                candidate.Profile.inventory.usingCartReservation = true;
                reference.Profile.inventory.usingCartReservation = true;
                var beforeWith = L1BSourceState(candidate.Profile, candidate.Definition, alias);
                var with = L1BMeasure(log, "Quote with reservation flag", () => scope.Quote(candidate.Definition, 5, false, 1));
                var withExpected = LegacyScopedSupplyReference.Quote(reference.Profile, reference.Definition, 5, false, 1);
                log.Check(with.Error == null && L1BSameQuote(with.Value, withExpected) && with.Value.LocalUnits == 5,
                    "Quote adds all active matching generations back after current-only availability subtraction");
                log.Check(beforeWith.Same(L1BSourceState(candidate.Profile, candidate.Definition, alias)),
                    "Quote with reservation flag leaves its post-toggle source state unchanged");
            }

            var inactive = L1BCreateFixture(); var inactiveRef = L1BCreateFixture();
            inactive.Profile.inventory.market.parts.Add(L1BStock(inactive.Definition.id));
            inactiveRef.Profile.inventory.market.parts.Add(L1BStock(inactiveRef.Definition.id));
            L1BSetReservation(inactive, 2, new ReservedPartSupply { definitionId = inactive.Definition.id, units = 7, generation = 4 });
            L1BSetReservation(inactiveRef, 2, new ReservedPartSupply { definitionId = inactiveRef.Definition.id, units = 7, generation = 4 });
            inactive.Profile.vehicleStock.completedRuns = inactiveRef.Profile.vehicleStock.completedRuns = 2;
            var inactiveExpected = LegacyScopedSupplyReference.Held(inactiveRef.Profile, inactiveRef.Definition, false);
            using (var scope = ScopedMarketReadSet.OpenBorrowed(inactive.Profile))
            {
                var held = L1BMeasure(log, "Held at exact expiresRun remains active", () => scope.Held(inactive.Definition));
                log.Check(held.Error == null && held.Value == inactiveExpected && held.Value == 7, "reservation is active at expiresRun boundary");
                inactive.Profile.vehicleStock.completedRuns = 3; inactiveRef.Profile.vehicleStock.completedRuns = 3;
                var expiredExpected = LegacyScopedSupplyReference.Held(inactiveRef.Profile, inactiveRef.Definition, false);
                var expired = L1BMeasure(log, "Held following run is inactive", () => scope.Held(inactive.Definition));
                log.Check(expired.Error == null && expired.Value == expiredExpected && expired.Value == 0, "reservation expires only after expiresRun");
            }
        }

        static void L1BActiveNullAndErrorOrder(L1BCheckLog log)
        {
            var inactive = L1BCreateFixture();
            inactive.Profile.inventory.cart = new CartReservationState { version = 2, expiresRun = 100, supply = null };
            using (var scope = ScopedMarketReadSet.OpenBorrowed(inactive.Profile))
            {
                var noRequest = L1BMeasure(log, "inactive Held with null request", () => scope.Held(null));
                log.Check(noRequest.Error == null && noRequest.Value == 0, "inactive Held returns zero before resolving a null request or null supply list");
            }
            var activeNullSupply = L1BCreateFixture(); var activeNullSupplyRef = L1BCreateFixture();
            var cRow = L1BStock(activeNullSupply.Definition.id); var rRow = L1BStock(activeNullSupplyRef.Definition.id);
            activeNullSupply.Profile.inventory.market.parts.Add(cRow); activeNullSupplyRef.Profile.inventory.market.parts.Add(rRow);
            activeNullSupply.Profile.inventory.market.index = new Dictionary<string, NewPartStock> { [activeNullSupply.Definition.id] = cRow };
            activeNullSupplyRef.Profile.inventory.market.index = new Dictionary<string, NewPartStock> { [activeNullSupplyRef.Definition.id] = rRow };
            L1BSetReservation(activeNullSupply, 20, new ReservedPartSupply[0]);
            L1BSetReservation(activeNullSupplyRef, 20, new ReservedPartSupply[0]);
            activeNullSupply.Profile.inventory.cart.supply = null; activeNullSupplyRef.Profile.inventory.cart.supply = null;
            var expected = L1BCapture(() => LegacyScopedSupplyReference.Held(activeNullSupplyRef.Profile, activeNullSupplyRef.Definition));
            using (var scope = ScopedMarketReadSet.OpenBorrowed(activeNullSupply.Profile))
            {
                var actual = L1BMeasure(log, "active Held null supply uses LINQ null behavior", () => scope.Held(activeNullSupply.Definition));
                L1BCompareErrors(log, "active Held null supply exception order", actual.Error, expected.Error);
            }
            var nullOwner = L1BMeasure(log, "OpenBorrowed stores a null owner without touching it", () => ScopedMarketReadSet.OpenBorrowed(null));
            log.Check(nullOwner.Error == null && nullOwner.Value != null, "OpenBorrowed(null) defers owner dereference until operation");
            if (nullOwner.Value != null)
            {
                var query = L1BMeasure(log, "null owner Stock dereference", () => nullOwner.Value.Stock(null));
                var legacy = L1BCapture(() => LegacyScopedSupplyReference.Stock(null, null));
                L1BCompareErrors(log, "null owner Stock error matches legacy first dereference", query.Error, legacy.Error);
                nullOwner.Value.Dispose();
            }
            var activeNullRequest = L1BCreateFixture(); var activeNullRequestRef = L1BCreateFixture();
            activeNullRequest.Profile.inventory.market.index = new Dictionary<string, NewPartStock>();
            activeNullRequestRef.Profile.inventory.market.index = new Dictionary<string, NewPartStock>();
            L1BSetReservation(activeNullRequest, 20, new ReservedPartSupply[0]);
            L1BSetReservation(activeNullRequestRef, 20, new ReservedPartSupply[0]);
            var nullExpected = L1BCapture(() => LegacyScopedSupplyReference.Held(activeNullRequestRef.Profile, null));
            using (var scope = ScopedMarketReadSet.OpenBorrowed(activeNullRequest.Profile))
            {
                var nullActual = L1BMeasure(log, "active Held null request Stock-first error", () => scope.Held(null));
                L1BCompareErrors(log, "active Held null request error order", nullActual.Error, nullExpected.Error);
            }
        }

        static void L1BConsumeGenerationAndIdentityDeltas(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            var alias = L1BAddIdAlias(candidate); var aliasRef = L1BAddIdAlias(reference);
            var cStock = L1BStock(candidate.Definition.id, alias.id); var rStock = L1BStock(reference.Definition.id, aliasRef.id);
            cStock.units = rStock.units = 3;
            candidate.Profile.inventory.market.parts.Add(cStock); reference.Profile.inventory.market.parts.Add(rStock);
            candidate.Profile.inventory.market.generation = reference.Profile.inventory.market.generation = 4;
            L1BSetReservation(candidate, 10,
                new ReservedPartSupply { definitionId = alias.id, units = 2, generation = 2 },
                new ReservedPartSupply { definitionId = candidate.Definition.id, units = 3, generation = 4 },
                new ReservedPartSupply { definitionId = alias.id, units = 5, generation = 3 });
            L1BSetReservation(reference, 10,
                new ReservedPartSupply { definitionId = aliasRef.id, units = 2, generation = 2 },
                new ReservedPartSupply { definitionId = reference.Definition.id, units = 3, generation = 4 },
                new ReservedPartSupply { definitionId = aliasRef.id, units = 5, generation = 3 });
            candidate.Profile.inventory.usingCartReservation = reference.Profile.inventory.usingCartReservation = true;
            var before = L1BSourceState(candidate.Profile, candidate.Definition, alias);
            var expected = LegacyScopedSupplyReference.Consume(reference.Profile, reference.Definition, 5);
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var actual = L1BMeasure(log, "Consume uses generation-ordered holds and current-stock delta", () => scope.Consume(candidate.Definition, 5));
                log.Check(actual.Error == null && L1BSameQuote(actual.Value.Quote, expected), "Consume quote exactly matches frozen result");
                log.Check(reference.Profile.inventory.market.parts[0].units == 3 && actual.Value.StockDeltas.Length == 0,
                    "old-generation holds consume the request before current-generation stock and produce no stock delta");
                log.Check(reference.Profile.inventory.cart.supply.Select(row => row.units).SequenceEqual(new[] { 0, 3, 2 }),
                    "frozen old-generation Consume leaves exact ordered hold quantities");
                var holds = actual.Value.HoldDeltas;
                log.Check(holds.Length == 2 && holds.SelectMany(delta => delta.Positions).SequenceEqual(new[] { 0, 2 }) &&
                    holds.Select(delta => delta.After).SequenceEqual(new[] { 0, 2 }) && holds.Select(delta => delta.Before).SequenceEqual(new[] { 2, 5 }),
                    "Consume reports only changed generation-ordered hold identity groups with final quantity deltas");
                var after = L1BSourceState(candidate.Profile, candidate.Definition, alias);
                log.Check(before.Same(after), "Consume returns a private delta without changing market or reservation source rows");
            }

            var currentCandidate = L1BCreateFixture(); var currentReference = L1BCreateFixture();
            var currentAlias = L1BAddIdAlias(currentCandidate); var currentAliasRef = L1BAddIdAlias(currentReference);
            var currentStock = L1BStock(currentCandidate.Definition.id, currentAlias.id);
            var currentStockRef = L1BStock(currentReference.Definition.id, currentAliasRef.id);
            currentStock.units = currentStock.initialUnits = currentStockRef.units = currentStockRef.initialUnits = 8;
            currentCandidate.Profile.inventory.market.parts.Add(currentStock); currentReference.Profile.inventory.market.parts.Add(currentStockRef);
            currentCandidate.Profile.inventory.market.generation = currentReference.Profile.inventory.market.generation = 4;
            L1BSetReservation(currentCandidate, 10,
                new ReservedPartSupply { definitionId = currentAlias.id, units = 2, generation = 2 },
                new ReservedPartSupply { definitionId = currentAlias.id, units = 5, generation = 3 },
                new ReservedPartSupply { definitionId = currentCandidate.Definition.id, units = 3, generation = 4 });
            L1BSetReservation(currentReference, 10,
                new ReservedPartSupply { definitionId = currentAliasRef.id, units = 2, generation = 2 },
                new ReservedPartSupply { definitionId = currentAliasRef.id, units = 5, generation = 3 },
                new ReservedPartSupply { definitionId = currentReference.Definition.id, units = 3, generation = 4 });
            currentCandidate.Profile.inventory.usingCartReservation = currentReference.Profile.inventory.usingCartReservation = true;
            var expectedCurrentConsume = LegacyScopedSupplyReference.Consume(currentReference.Profile, currentReference.Definition, 12);
            var currentBefore = L1BSourceState(currentCandidate.Profile, currentCandidate.Definition, currentAlias);
            using (var scope = ScopedMarketReadSet.OpenBorrowed(currentCandidate.Profile))
            {
                var actual = L1BMeasure(log, "Consume current holds and unreserved stock remainder", () => scope.Consume(currentCandidate.Definition, 12));
                log.Check(actual.Error == null && L1BSameQuote(actual.Value.Quote, expectedCurrentConsume),
                    "current-generation Consume quote matches frozen result");
                log.Check(actual.Value.StockDeltas.Length == 1 && actual.Value.StockDeltas[0].Before == 8 && actual.Value.StockDeltas[0].After == 3 &&
                    actual.Value.StockDeltas[0].Positions.SequenceEqual(new[] { 0 }),
                    "current hold consumption and remaining local units combine into exact stock delta 8 to 3");
                var holds = actual.Value.HoldDeltas;
                log.Check(holds.Length == 3 && holds.Select(delta => delta.Before).SequenceEqual(new[] { 2, 5, 3 }) &&
                    holds.All(delta => delta.After == 0) && holds.SelectMany(delta => delta.Positions).SequenceEqual(new[] { 0, 1, 2 }),
                    "Consume reports exact old/current generation hold deltas in ascending generation order");
                log.Check(currentReference.Profile.inventory.market.parts[0].units == 3 &&
                    currentReference.Profile.inventory.cart.supply.Select(row => row.units).SequenceEqual(new[] { 0, 0, 0 }),
                    "frozen current-generation Consume leaves exact stock and ordered hold quantities");
                log.Check(currentBefore.Same(L1BSourceState(currentCandidate.Profile, currentCandidate.Definition, currentAlias)),
                    "current-generation Consume returns deltas without changing candidate source objects");
            }

            var repeated = L1BCreateFixture(); var repeatedRef = L1BCreateFixture();
            var sameHold = new ReservedPartSupply { definitionId = repeated.Definition.id, units = 2, generation = 4 };
            var sameHoldRef = new ReservedPartSupply { definitionId = repeatedRef.Definition.id, units = 2, generation = 4 };
            var repeatedStock = L1BStock(repeated.Definition.id); var repeatedStockRef = L1BStock(repeatedRef.Definition.id);
            repeatedStock.units = repeatedStockRef.units = 2;
            repeated.Profile.inventory.market.parts.Add(repeatedStock); repeated.Profile.inventory.market.parts.Add(repeatedStock);
            repeatedRef.Profile.inventory.market.parts.Add(repeatedStockRef); repeatedRef.Profile.inventory.market.parts.Add(repeatedStockRef);
            repeated.Profile.inventory.market.index = new Dictionary<string, NewPartStock> { [repeated.Definition.id] = repeatedStock };
            repeatedRef.Profile.inventory.market.index = new Dictionary<string, NewPartStock> { [repeatedRef.Definition.id] = repeatedStockRef };
            L1BSetReservation(repeated, 10, sameHold, sameHold); L1BSetReservation(repeatedRef, 10, sameHoldRef, sameHoldRef);
            repeated.Profile.inventory.usingCartReservation = repeatedRef.Profile.inventory.usingCartReservation = true;
            var repeatedBefore = L1BSourceState(repeated.Profile, repeated.Definition);
            var repeatedExpected = LegacyScopedSupplyReference.Consume(repeatedRef.Profile, repeatedRef.Definition, 2);
            using (var scope = ScopedMarketReadSet.OpenBorrowed(repeated.Profile))
            {
                var actual = L1BMeasure(log, "Consume repeated stock/hold object references", () => scope.Consume(repeated.Definition, 2));
                var holdDeltas = actual.Value?.HoldDeltas ?? Array.Empty<ScopedQuantityDelta>();
                log.Check(actual.Error == null && L1BSameQuote(actual.Value.Quote, repeatedExpected) && repeatedRef.Profile.inventory.cart.supply[0].units == 0,
                    "Consume repeated supply reference matches legacy mutation");
                log.Check(holdDeltas.Length == 1 && holdDeltas[0].Positions.SequenceEqual(new[] { 0, 1 }) && holdDeltas[0].Before == 2 && holdDeltas[0].After == 0,
                    "repeated hold references produce one final identity-group delta with both positions");
                log.Check(actual.Value.StockDeltas.Length == 1 && actual.Value.StockDeltas[0].Before == 2 && actual.Value.StockDeltas[0].After == 0 &&
                    actual.Value.StockDeltas[0].Positions.SequenceEqual(new[] { 0, 1 }),
                    "repeated current-generation stock reference produces one identity-group delta with both positions");
                log.Check(repeatedBefore.Same(L1BSourceState(repeated.Profile, repeated.Definition)), "repeated-reference Consume keeps source graph unchanged");
            }
        }

        static void L1BQuoteAndFeeMatrix(L1BCheckLog log)
        {
            L1BQuoteParity(log, "partial local/express with repeated quantity", 5, 5, 7, false, 3);
            L1BQuoteParity(log, "delivery lower endpoint", 3, 5, 4, false, 2);
            L1BQuoteParity(log, "delivery upper endpoint", 0, 10, 4, false, 2);
            L1BQuoteParity(log, "NewUnit pricing branch", 4, 8, 7, true, 3);
            L1BQuoteParity(log, "no market row uses ten-percent default", null, 10, 5, false, 2);
            L1BQuoteErrorParity(log, "invalid quantity is rejected by shared Calculate", 4, 7, 2, false, 0, null, null);
            L1BQuoteErrorParity(log, "invalid delivery percent precedes quantity error", 4, 4, 2, false, 0, 4, null);
            L1BQuoteErrorParity(log, "negative price reaches base-price validation", 4, 7, 2, false, 1, 7, -1);
            L1BQuoteErrorParity(log, "invalid units precede negative price", 4, 7, 2, false, 0, 7, -1);
            L1BQuoteCostOverflow(log);
            L1BQuoteUncheckedExpressWrap(log);
            L1BNewUnitBounds(log);
        }

        static void L1BQuoteParity(L1BCheckLog log, string label, int? available, int percent, int count, bool unit, int quantity)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            if (available.HasValue)
            {
                var cStock = L1BStock(candidate.Definition.id); cStock.units = cStock.initialUnits = available.Value; cStock.deliveryPercent = percent;
                var rStock = L1BStock(reference.Definition.id); rStock.units = rStock.initialUnits = available.Value; rStock.deliveryPercent = percent;
                candidate.Profile.inventory.market.parts.Add(cStock); reference.Profile.inventory.market.parts.Add(rStock);
            }
            var expected = L1BCapture(() => LegacyScopedSupplyReference.Quote(reference.Profile, reference.Definition, count, unit, quantity));
            var before = L1BSourceState(candidate.Profile, candidate.Definition);
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var actual = L1BMeasure(log, "Quote / " + label, () => scope.Quote(candidate.Definition, count, unit, quantity));
                L1BCompareErrors(log, "Quote exception / " + label, actual.Error, expected.Error);
                if (actual.Error == null && expected.Error == null)
                    log.Check(L1BSameQuote(actual.Value, expected.Value), "Quote all four fields / " + label + ";candidate=" + L1BQuoteText(actual.Value) + ";oracle=" + L1BQuoteText(expected.Value));
                log.Check(before.Same(L1BSourceState(candidate.Profile, candidate.Definition)), "Quote preserves source market and catalog / " + label);
            }
        }

        static void L1BQuoteErrorParity(L1BCheckLog log, string label, int stockUnits, int percent, int count, bool unit,
            int quantity, int? overridePercent, int? overridePrice)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            var cStock = L1BStock(candidate.Definition.id); var rStock = L1BStock(reference.Definition.id);
            cStock.units = cStock.initialUnits = stockUnits; rStock.units = rStock.initialUnits = stockUnits;
            cStock.deliveryPercent = overridePercent ?? percent; rStock.deliveryPercent = overridePercent ?? percent;
            candidate.Profile.inventory.market.parts.Add(cStock); reference.Profile.inventory.market.parts.Add(rStock);
            if (overridePrice.HasValue) { candidate.Definition.price = overridePrice.Value; reference.Definition.price = overridePrice.Value; }
            var expected = L1BCapture(() => LegacyScopedSupplyReference.Quote(reference.Profile, reference.Definition, count, unit, quantity));
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var actual = L1BMeasure(log, "Quote validation/error precedence / " + label,
                    () => scope.Quote(candidate.Definition, count, unit, quantity));
                L1BCompareErrors(log, "Quote exact first validation error / " + label, actual.Error, expected.Error);
            }
        }

        static void L1BQuoteCostOverflow(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            candidate.Definition.price = reference.Definition.price = int.MaxValue;
            var cStock = L1BStock(candidate.Definition.id); var rStock = L1BStock(reference.Definition.id);
            cStock.units = cStock.initialUnits = rStock.units = rStock.initialUnits = 0;
            cStock.deliveryPercent = rStock.deliveryPercent = 10;
            candidate.Profile.inventory.market.parts.Add(cStock); reference.Profile.inventory.market.parts.Add(rStock);
            var expected = L1BCapture(() => LegacyScopedSupplyReference.Quote(reference.Profile, reference.Definition, 1));
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var actual = L1BMeasure(log, "reachable checked cost overflow", () => scope.Quote(candidate.Definition, 1));
                L1BCompareErrors(log, "checked cost overflow matches frozen Quote", actual.Error, expected.Error);
            }
        }

        static void L1BQuoteUncheckedExpressWrap(L1BCheckLog log)
        {
            var candidate = L1BCreateFixture(); var reference = L1BCreateFixture();
            candidate.Definition.price = reference.Definition.price = 0;
            var cStock = L1BStock(candidate.Definition.id); var rStock = L1BStock(reference.Definition.id);
            cStock.units = cStock.initialUnits = rStock.units = rStock.initialUnits = int.MaxValue;
            cStock.deliveryPercent = rStock.deliveryPercent = 10;
            candidate.Profile.inventory.market.parts.Add(cStock); reference.Profile.inventory.market.parts.Add(rStock);
            var expected = L1BCapture(() => LegacyScopedSupplyReference.Quote(reference.Profile, reference.Definition, int.MaxValue, false, 99));
            using (var scope = ScopedMarketReadSet.OpenBorrowed(candidate.Profile))
            {
                var actual = L1BMeasure(log, "very-large repeated quantity unchecked units", () => scope.Quote(candidate.Definition, int.MaxValue, false, 99));
                log.Check(actual.Error == null && expected.Error == null && L1BSameQuote(actual.Value, expected.Value) &&
                    actual.Value.LocalUnits == int.MaxValue,
                    "unchecked express accumulation matches frozen loop; local-unit total remains bounded at int.MaxValue");
            }
        }

        static void L1BNewUnitBounds(L1BCheckLog log)
        {
            foreach (int count in new[] { int.MinValue, -1, 0, 1, 2, 99, int.MaxValue })
            {
                var fixture = L1BCreateFixture();
                int expected = LegacyScopedSupplyReference.NewUnit(fixture.Definition, count);
                var actual = L1BMeasure(log, "NewUnit count-class=" + L1BCountClass(count), () => InventoryPricing.NewUnit(fixture.Definition, count));
                log.Check(actual.Error == null && actual.Value == expected, "InventoryPricing.NewUnit matches frozen price expression for count class=" + L1BCountClass(count));
            }
            var nullResult = L1BCapture(() => InventoryPricing.NewUnit(null, 1));
            var nullOracle = L1BCapture(() => LegacyScopedSupplyReference.NewUnit(null, 1));
            L1BCompareErrors(log, "NewUnit null definition dereference", nullResult.Error, nullOracle.Error);
        }

        static void L1BLifetimeAndPurity(L1BCheckLog log)
        {
            var fixture = L1BCreateFixture();
            var alias = L1BAddIdAlias(fixture, "/lifetime-alias");
            fixture.Profile.inventory.market.parts.Add(L1BStock(fixture.Definition.id));
            var scope = ScopedMarketReadSet.OpenBorrowed(fixture.Profile);
            var ledger = scope.OpenDefaultLedger();
            var stock = scope.Stock(alias);
            var quote = scope.Quote(fixture.Definition, 3);
            var quoteFieldsOnly = typeof(ScopedSupplyQuote).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .All(field => field.FieldType == typeof(int));
            var deltaFieldsOnly = typeof(ScopedQuantityDelta).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .All(field => field.FieldType == typeof(int) || field.FieldType == typeof(int[]));
            log.Check(stock != null && quote != null && quoteFieldsOnly && deltaFieldsOnly,
                "result data types expose only quote scalars and identity/position/value deltas");
            var copyA = stock.Positions; var copyB = stock.Positions;
            if (copyA.Length > 0) copyA[0] = -123;
            log.Check(!ReferenceEquals(copyA, copyB) && stock.Positions.SequenceEqual(new[] { 0 }), "Stock positions are independent owned arrays");
            var ownerField = typeof(ScopedMarketReadSet).GetField("owner", BindingFlags.Instance | BindingFlags.NonPublic);
            var stockIndexField = typeof(ScopedMarketReadSet).GetField("stockIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            var aliasIndexField = typeof(ScopedMarketReadSet).GetField("stockAliases", BindingFlags.Instance | BindingFlags.NonPublic);
            var catalogField = typeof(ScopedMarketReadSet).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic);
            var ledgerSetField = typeof(ScopedMarketReadSet).GetField("ledgers", BindingFlags.Instance | BindingFlags.NonPublic);
            object catalogLookup = catalogField?.GetValue(scope);
            scope.Dispose();
            scope.Dispose();
            log.Check(scope.IsDisposed, "scope disposal is idempotent and observable");
            var children = ledgerSetField?.GetValue(scope);
            var childCountProperty = children?.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
            int childCount = childCountProperty == null ? -1 : (int)childCountProperty.GetValue(children, null);
            bool ownerCleared = ownerField != null && ownerField.GetValue(scope) == null && stockIndexField != null && stockIndexField.GetValue(scope) == null &&
                aliasIndexField != null && aliasIndexField.GetValue(scope) == null && catalogField != null && catalogField.GetValue(scope) == null &&
                children != null && childCount == 0;
            var lookupOwner = catalogLookup?.GetType().GetField("owner", BindingFlags.Instance | BindingFlags.NonPublic);
            var lookupIndex = catalogLookup?.GetType().GetField("startingIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            var lookupParts = catalogLookup?.GetType().GetField("startingParts", BindingFlags.Instance | BindingFlags.NonPublic);
            var lookupShadow = catalogLookup?.GetType().GetField("shadow", BindingFlags.Instance | BindingFlags.NonPublic);
            bool lookupCleared = catalogLookup != null && lookupOwner?.GetValue(catalogLookup) == null &&
                lookupIndex?.GetValue(catalogLookup) == null && lookupParts?.GetValue(catalogLookup) == null && lookupShadow?.GetValue(catalogLookup) == null;
            var ledgerOwner = ledger.GetType().GetField("owner", BindingFlags.Instance | BindingFlags.NonPublic);
            var ledgerParent = ledger.GetType().GetField("parent", BindingFlags.Instance | BindingFlags.NonPublic);
            log.Check(ownerCleared && lookupCleared && ledgerOwner != null && ledgerOwner.GetValue(ledger) == null && ledgerParent != null && ledgerParent.GetValue(ledger) == null,
                "Dispose clears borrowed owner/catalog/index/ledger references and nested lookup state");
            var afterDispose = new[]
            {
                L1BCapture(() => scope.Stock(fixture.Definition)).Error,
                L1BCapture(() => scope.Available(fixture.Definition)).Error,
                L1BCapture(() => scope.Held(fixture.Definition)).Error,
                L1BCapture(() => scope.Quote(fixture.Definition, 1)).Error,
                L1BCapture(() => scope.Consume(fixture.Definition, 1)).Error,
                L1BCapture(() => scope.OpenDefaultLedger()).Error,
                L1BCapture(() => ledger.Available(fixture.Definition)).Error,
                L1BCapture(() => ledger.Quote(fixture.Definition, 1, 1)).Error,
                L1BCapture(() => ledger.Take(fixture.Definition, 1, 1)).Error
            };
            log.Check(afterDispose.All(error => error is ObjectDisposedException),
                "all scoped read-set and nested Ledger methods fail with ObjectDisposedException before owner access");
        }
    }
}
#endif
