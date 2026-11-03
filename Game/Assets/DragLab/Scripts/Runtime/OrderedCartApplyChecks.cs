#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Editor-only differential checks for the typed ordered Apply projection.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        static long c1CandidateJsonDelta;
        sealed class C1ExpectedWriteSet
        {
            internal readonly HashSet<string> Parts=new HashSet<string>(StringComparer.Ordinal),Engines=new HashSet<string>(StringComparer.Ordinal),Units=new HashSet<string>(StringComparer.Ordinal),Dealer=new HashSet<string>(StringComparer.Ordinal),Cars=new HashSet<string>(StringComparer.Ordinal),Owners=new HashSet<string>(StringComparer.Ordinal);
            internal readonly Dictionary<string,string> ReplayToWhole=new Dictionary<string,string>(StringComparer.Ordinal);
        }
        sealed class OrderedRowsSpy : IReadOnlyList<PartCartLine>
        {
            readonly PartCartLine[] rows;
            internal int Enumerators, MoveNexts, Currents;
            internal OrderedRowsSpy(PartCartLine[] rows) { this.rows = rows; }
            public int Count => throw new InvalidOperationException("Count must not be read.");
            public PartCartLine this[int index] => throw new InvalidOperationException("Indexer must not be read.");
            public IEnumerator<PartCartLine> GetEnumerator() { Enumerators++; return ReadRows(); }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            IEnumerator<PartCartLine> ReadRows()
            {
                foreach (var row in rows) { MoveNexts++; Currents++; yield return row; }
            }
        }

        sealed class ThrowingRows : IReadOnlyList<PartCartLine>
        {
            readonly PartCartLine[] rows;
            readonly int yielded;
            internal int Enumerators, MoveNexts, Currents;
            internal ThrowingRows(PartCartLine[] rows, int yielded) { this.rows = rows; this.yielded = yielded; }
            public int Count => throw new InvalidOperationException("Count must not be read.");
            public PartCartLine this[int index] => throw new InvalidOperationException("Indexer must not be read.");
            public IEnumerator<PartCartLine> GetEnumerator() { Enumerators++; return ReadRows(); }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            IEnumerator<PartCartLine> ReadRows()
            {
                for (int i = 0; i < yielded; i++) { MoveNexts++; Currents++; yield return rows[i]; }
                MoveNexts++;
                throw new InvalidOperationException("enumerator-after-row-zero");
            }
        }

        public static void RunOrderedCartApply(Action<bool, string> check, string folder)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("An isolated Editor report folder is required.", nameof(folder));
            Directory.CreateDirectory(folder);
            var trace = new Trace(check, folder, true);
            trace.Add("S06D2B1L1C1_ORDERED_CART_APPLY_V1");
            trace.Add("candidate=OrderedCartApplyProjection.Evaluate;oracle=LegacyC1CartApplyReference.Apply;fixtures=independent actual authored catalogs");
            try
            {
                C1NullAndLazy(trace);
                C1Acquisition(trace);
                C1MoveAndSubtype(trace);
                C1ErrorsAndCatalog(trace);
                C1ReservationPreviewAndCollision(trace);
                C1MalformedTireSources(trace);
            }
            catch (Exception error)
            {
                trace.Assert(false, "C1 matrix completed without an unhandled exception / " + C0Error(error));
                trace.Add("RUNNER_EXCEPTION|" + C0Error(error));
            }
            finally
            {
                C1CurrentOrdinals=null;
                trace.Add("GATE|C1|typed Apply matrix; regression/build/high review gates are reported separately");
                File.WriteAllText(Path.Combine(folder, "S06D2B1L1C1_ORDERED_CART_APPLY.txt"), trace.Text.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
            }
        }

        public static void RunOrderedCartApplyMalformed(Action<bool, string> check, string folder)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("An isolated Editor report folder is required.", nameof(folder));
            Directory.CreateDirectory(folder);
            var trace = new Trace(check, folder, true);
            trace.Add("S06D2B1L1C1_MALFORMED_TIRE_FOCUSED_V1");
            trace.Add("scope=seven existing-source malformed tire cases only; this diagnostic is not full C1 acceptance");
            try
            {
                C1MalformedTireSources(trace);
            }
            catch (Exception error)
            {
                trace.Assert(false, "focused malformed tire cases complete without an unhandled exception / " + C0Error(error));
                trace.Add("RUNNER_EXCEPTION|" + C0Error(error));
            }
            finally
            {
                C1CurrentOrdinals = null;
                trace.Add("GATE|focused malformed tire fixture diagnostic only; full C1 matrix and regression/build/high review gates remain separate");
                File.WriteAllText(Path.Combine(folder, "S06D2B1L1C1_MALFORMED_TIRE_FOCUSED.txt"), trace.Text.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
            }
        }

        static void C1NullAndLazy(Trace trace)
        {
            C1NullCase(trace, "null-owner-empty", Array.Empty<PartCartLine>());
            C1NullCase(trace, "null-owner-sales", new[] { new PartCartLine { action = CartAction.SellPart }, new PartCartLine { action = CartAction.SellUnit, assetId = "missing" } });
            C1NullCase(trace, "null-owner-zero-and-negative", new[] { new PartCartLine { action = CartAction.NewSet, quantity = 0 }, new PartCartLine { action = CartAction.NewUnits, quantity = -1 }, new PartCartLine { action = (CartAction)999, quantity = 0 } });
            C1NullCase(trace, "null-owner-move-once", new[] { new PartCartLine { action = CartAction.Move, quantity = 0, assetId = "missing", destination = InventoryKind.Storage, destinationId = SharedInventory.Storage } });
            C1NullCase(trace, "null-owner-null-row", new PartCartLine[] { null });
            Exception legacyNullRows = C1Frozen(null, null, null, false, ValuationPolicy.Defaults(), out _);
            var nullRows = C1Evaluate(null, null, null, false, ValuationPolicy.Defaults(), null, out var nullDisposed, out var nullChildren, out var nullTokens, out var nullEntropy);
            var typedNullRows = new ArgumentNullException("rows");
            trace.Assert(legacyNullRows is NullReferenceException && !nullRows.Succeeded && nullRows.ErrorIndex == -1 && nullRows.ErrorType == typedNullRows.GetType().FullName &&
                nullRows.ErrorMessage == typedNullRows.Message && nullRows.ErrorParameterName == "rows" &&
                nullRows.Effects == null && nullRows.Parts == null && nullRows.Identities == null && !nullRows.HasCashDelta &&
                nullDisposed && nullChildren && nullTokens == 0 && nullEntropy.Count == 0 && nullEntropy.Closed,
                "typed Evaluate rejects null rows with ArgumentNullException(rows) at -1; raw Apply retains its natural NullReferenceException and candidate has no outputs or owner reads");

            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, 161020321u);
            var car = source.cars.Find(item => item.instanceId == carId);
            var set = FindOffer(source, car, PartCategory.HeadIntake, false);
            if (set == null) { trace.Assert(false, "lazy enumerable case has an actual stocked HeadIntake definition"); return; }
            var rows = new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 } };
            var fixture = C0BuildFixture(source, carId, rows, C0CatalogShape.Warm);
            var spy = new OrderedRowsSpy(fixture.LiveRows);
            var result = C1Evaluate(fixture.Live, carId, spy, false, ValuationPolicy.Defaults(), null, out var disposed, out var children, out var tokens, out var entropy);
            Exception frozen = C1Frozen(fixture.Frozen, carId, fixture.FrozenRows, false, ValuationPolicy.Defaults(), out var frozenEntropy);
            trace.Assert(result.Succeeded && frozen == null && spy.Enumerators == 1 && spy.MoveNexts == 1 && spy.Currents == 1,
                "IReadOnlyList spy is consumed through exactly one enumerator without Count/indexer access");
            trace.Assert(disposed && children && tokens == 0 && entropy.Count == 0 && entropy.Closed && frozenEntropy.Closed,
                "single-pass candidate disposes scopes and uses no gameplay identity entropy");
            trace.Assert(c1CandidateJsonDelta == 0, "single-pass candidate performs no runtime JSON encoding");
            trace.Assert(fixture.RowsBefore == RowsSnapshot(fixture.LiveRows) && C0SameRowReferences(fixture.LiveRowReferences, fixture.LiveRows),
                "enumerable backing row values and references remain unchanged");
            C1Success(trace, "lazy-single-pass", fixture, result, fixture.LiveRows);
            var lazyWrites=C1QuoteEffectParity(trace,"lazy-single-pass",source,carId,fixture.LiveRows,fixture.Live.inventory.usingCartReservation,result,C0CatalogShape.Warm,false,frozenEntropy,fixture.Frozen);
            C1SnapshotParity(trace, "lazy-single-pass", fixture, result, fixture.LiveRows, frozenEntropy,lazyWrites);

            var throwRows = new[] { new PartCartLine { action = CartAction.SellUnit, assetId = "ignored" }, new PartCartLine { action = CartAction.Move } };
            var throwFixture = C0BuildFixture(source, carId, throwRows, C0CatalogShape.Warm);
            var candidateThrow = new ThrowingRows(throwFixture.LiveRows, 1);
            var legacyThrow = new ThrowingRows(throwFixture.FrozenRows, 1);
            Exception expected = C1Frozen(throwFixture.Frozen, carId, legacyThrow, false, ValuationPolicy.Defaults(), out _);
            var failed = C1Evaluate(throwFixture.Live, carId, candidateThrow, false, ValuationPolicy.Defaults(), null, out disposed, out children, out tokens, out entropy);
            trace.Assert(expected is InvalidOperationException && !failed.Succeeded && failed.ErrorType == expected.GetType().FullName && failed.ErrorMessage == expected.Message && failed.ErrorIndex == 1,
                "MoveNext failure after row zero retains frozen error and next traversal index");
            trace.Assert(candidateThrow.Enumerators == 1 && candidateThrow.MoveNexts == 2 && disposed && children && tokens == 0 &&
                failed.Effects == null && failed.Parts == null && failed.Identities == null && !failed.HasCashDelta,
                "enumerator failure consumes once, discards prefix outputs and disposes all state");
            trace.Assert(c1CandidateJsonDelta == 0, "enumerator-failure candidate performs no runtime JSON encoding");
        }

        static void C1Acquisition(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, 161020322u);
            var car = source.cars.Find(item => item.instanceId == carId);
            var set = FindOffer(source, car, PartCategory.HeadIntake, false);
            var target = CoreRowsInstalledUnitTarget(source, car);
            if (set == null || target == null) { trace.Assert(false, "acquisition matrix uses actual stocked set and installed generic unit target"); return; }
            C1Case(trace, "NewSet-q0", source, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 0, install = true } }, true, preview: true);
            C1Case(trace, "NewSet-q1-loose", source, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 } }, true);
            C1Case(trace, "NewSet-q2-first-install-only", source, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 2, install = true } }, true);
            C1Case(trace, "NewSet-repeated-same-product-rows", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 },
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 }
            }, true);
            C1Case(trace, "NewSet-negative", source, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = -2 } }, true);
            C1Case(trace, "NewUnits-q0", source, carId, new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = target.Definition.id, role = target.Role, quantity = 0 } }, true);
            C1Case(trace, "NewUnits-q1-loose", source, carId, new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = target.Definition.id, role = target.Role, quantity = 1 } }, true);
            C1Case(trace, "NewUnits-q2-one-position", source, carId, new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = target.Definition.id, role = target.Role, quantity = 2, install = true, positions = target.Positions.Take(1).ToList() } }, true);
            C1Case(trace, "NewUnits-q2-two-ordered-positions", source, carId, new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = target.Definition.id, role = target.Role, quantity = 2, install = true, positions = target.Positions.Take(2).ToList() } }, true);

            uint seed = 391031u;
            var usedPart = CartPreviewCharacterizationFixtures.AddUsedPart(source, "l1c1/actual-used-part", ref seed);
            C1Case(trace, "BuyUsed-part-loose", source, carId, new[] { new PartCartLine { action = CartAction.BuyUsed, assetId = usedPart.Id, quantity = 1 } }, true);
            C1Case(trace, "BuyUsed-part-installed", source, carId, new[] { new PartCartLine { action = CartAction.BuyUsed, assetId = usedPart.Id, definitionId = usedPart.DefinitionId, quantity = 1, install = true } }, true);
            C1Case(trace, "BuyUsed-part-q2-prefix-discard", source, carId, new[] { new PartCartLine { action = CartAction.BuyUsed, assetId = usedPart.Id, quantity = 2 } }, false);

            var fallbackDefinition = C0FindOtherModelDefinition(source, car, out int fallbackCandidates);
            trace.Add("CROSS_MODEL_FIXTURE|authoredCandidates=" + fallbackCandidates.ToString(CultureInfo.InvariantCulture) + ";definition=" + (fallbackDefinition?.id ?? "<unavailable>"));
            if (fallbackDefinition == null) trace.Assert(false, "C1 NewUnits model fallback uses an actual authored cross-model definition");
            else
            {
                C1Case(trace, "NewSet-cross-model-reference-count-fallback", source, carId, new[] {
                    new PartCartLine { action = CartAction.NewSet, definitionId = fallbackDefinition.id, quantity = 1 }
                }, true);
                var fallbackRoles = InventoryUnits.Roles(source, car, fallbackDefinition);
                if (fallbackRoles == null || fallbackRoles.Length == 0) trace.Assert(false, "actual cross-model definition has an authored unit role");
                else C1Case(trace, "NewUnits-cross-model-UnitSpec-reference-fallback", source, carId, new[] {
                    new PartCartLine { action = CartAction.NewUnits, definitionId = fallbackDefinition.id, role = fallbackRoles[0], quantity = 1 }
                }, true);
            }

            DealerStock usedUnit = null; uint unitSeed = 391031u; int attempts = 0;
            for (int i = 0; i < 96 && usedUnit == null; i++)
            {
                attempts = i + 1; uint random = unchecked(unitSeed + (uint)i); if (random == 0) random = 1;
                int first = source.inventory.dealer.Count;
                PartsMarket.AddUsedSupply(source, InventoryUnits.Spec(source, car, target.Definition), target.Definition,
                    "l1c1/actual-used-unit/" + i.ToString(CultureInfo.InvariantCulture), ref random);
                usedUnit = source.inventory.dealer.Skip(first).FirstOrDefault(row => row != null && row.IsUnit && row.DefinitionId == target.Definition.id &&
                    row.unit?.element != null && row.unit.element.role == target.Role && row.unit.element.Validate() == null && !string.IsNullOrWhiteSpace(row.unit.element.id));
                if (usedUnit == null) source.inventory.dealer.RemoveRange(first, source.inventory.dealer.Count - first);
            }
            var positions = InventoryUnits.Positions(source, car, target.Definition.id, target.Role);
            trace.Add("USED_UNIT_FIXTURE|attempts=" + attempts.ToString(CultureInfo.InvariantCulture) + ";definition=" + target.Definition.id + ";role=" + target.Role);
            if (usedUnit == null || positions == null || positions.Length == 0) { trace.Assert(false, "bounded real used unit fits actual installed definition/role/position"); return; }
            C1Case(trace, "BuyUsed-unit-exact-installed-definition-role", source, carId, new[] { new PartCartLine { action = CartAction.BuyUsed, assetId = usedUnit.Id, definitionId = target.Definition.id, role = target.Role, quantity = 1, install = true, positions = new List<string> { positions[0] } } }, true);
            C1Case(trace, "BuyUsed-unit-loose", source, carId, new[] { new PartCartLine { action = CartAction.BuyUsed, assetId = usedUnit.Id, quantity = 1 } }, true);
            C1Case(trace, "BuyUsed-unit-q2-prefix-discard", source, carId, new[] { new PartCartLine { action = CartAction.BuyUsed, assetId = usedUnit.Id, quantity = 2 } }, false);
            C1Case(trace, "undefined-action-used-stock-fallthrough", source, carId, new[] { new PartCartLine { action = (CartAction)999, assetId = usedPart.Id, quantity = 1 } }, true);
            C1Case(trace, "sales-around-purchase", source, carId, new[] {
                new PartCartLine { action = CartAction.SellPart, assetId = "missing-sale-part", quantity = 4 },
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 },
                new PartCartLine { action = CartAction.SellUnit, assetId = "missing-sale-unit", quantity = 4 }
            }, true);
            C1FirstWriteOrderVectors(trace);
        }

        static void C1FirstWriteOrderVectors(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var partSource,out var partCarId,161020450u);
            var partCar=partSource.cars.Find(value=>value?.instanceId==partCarId);
            var partOffer=FindOffer(partSource,partCar,PartCategory.HeadIntake,false);
            var binding=partCar?.installed?.slots?.FirstOrDefault(value=>value?.slot==PartCategory.HeadIntake.ToString());
            var displaced=binding==null?null:partSource.parts.Find(value=>value?.instanceId==binding.partInstanceId);
            if(partOffer==null||displaced==null){trace.Assert(false,"double-install first-write vector has an actual set and displaced root");return;}
            C1Case(trace,"first-write-NewSet-roots-before-displaced-source",partSource,partCarId,new[]{
                new PartCartLine{action=CartAction.NewSet,definitionId=partOffer.id,quantity=1,install=true},
                new PartCartLine{action=CartAction.NewSet,definitionId=partOffer.id,quantity=1,install=true}
            },true,inspect:result=>
            {
                var rows=result.Identities??Array.Empty<CartApplyIdentity>();
                var first=rows.SingleOrDefault(value=>value.RowIndex==0&&value.QuantityOrdinal==0&&value.Kind=="part"&&value.MemberPath=="part");
                var second=rows.SingleOrDefault(value=>value.RowIndex==1&&value.QuantityOrdinal==0&&value.Kind=="part"&&value.MemberPath=="part");
                var expected=new[]{first==null?default:CartApplyIdentityRef.Virtual(first.TokenOrdinal),CartApplyIdentityRef.Existing(displaced.instanceId),second==null?default:CartApplyIdentityRef.Virtual(second.TokenOrdinal)};
                var actual=(result.Parts??Array.Empty<CartApplyPartSnapshot>()).Select(value=>value.Identity).ToArray();
                trace.Assert(first!=null&&second!=null&&actual.SequenceEqual(expected),
                    "frozen NewSet chronology expects row-0 appended root, then displaced source root, then row-1 appended root");
            });

            var installedParts=partCar.installed.slots.Select(value=>value==null?null:partSource.parts.FirstOrDefault(item=>item?.instanceId==value.partInstanceId))
                .Where(value=>value!=null).Select(value=>new{Part=value,Ordinal=partSource.parts.FindIndex(item=>ReferenceEquals(item,value))})
                .Where(value=>value.Ordinal>=0).OrderBy(value=>value.Ordinal).Take(2).ToArray();
            if(installedParts.Length<2){trace.Assert(false,"reverse-source part Move vector has two actual installed source parts");return;}
            var high=installedParts[1];var low=installedParts[0];
            C1Case(trace,"first-write-Move-parts-reverse-source-order",partSource,partCarId,new[]{
                new PartCartLine{action=CartAction.Move,assetId=high.Part.instanceId,destination=InventoryKind.Storage,destinationId=SharedInventory.Garage},
                new PartCartLine{action=CartAction.Move,assetId=low.Part.instanceId,destination=InventoryKind.Storage,destinationId=SharedInventory.Garage}
            },true,inspect:result=>
            {
                var parts=result.Parts??Array.Empty<CartApplyPartSnapshot>();
                trace.Assert(parts.Length==2&&parts[0].Identity==CartApplyIdentityRef.Existing(high.Part.instanceId)&&parts[1].Identity==CartApplyIdentityRef.Existing(low.Part.instanceId)&&
                    parts[0].SourceOrdinal==high.Ordinal&&parts[1].SourceOrdinal==low.Ordinal&&parts.All(value=>value.InsertionOrdinal==-1),
                    "frozen raw Move chronology retains reverse source first-write order and each original SourceOrdinal");
            });

            CartPreviewCharacterizationFixtures.CreateNormalized(out var dealerSource,out var dealerCarId,161020451u);
            uint dealerSeed=551001u;
            var sourceFirst=CartPreviewCharacterizationFixtures.AddUsedPart(dealerSource,"l1c1/reverse-order-first",ref dealerSeed);
            var sourceSecond=CartPreviewCharacterizationFixtures.AddUsedPart(dealerSource,"l1c1/reverse-order-second",ref dealerSeed);
            int firstOrdinal=dealerSource.inventory.dealer.FindIndex(value=>value?.Id==sourceFirst.Id);
            int secondOrdinal=dealerSource.inventory.dealer.FindIndex(value=>value?.Id==sourceSecond.Id);
            if(firstOrdinal<0||secondOrdinal<=firstOrdinal){trace.Assert(false,"reverse-source dealer order fixture appends two distinct actual used rows in known source order");return;}
            C1Case(trace,"first-write-BuyUsed-tombstones-reverse-source-order",dealerSource,dealerCarId,new[]{
                new PartCartLine{action=CartAction.BuyUsed,assetId=sourceSecond.Id,quantity=1},
                new PartCartLine{action=CartAction.BuyUsed,assetId=sourceFirst.Id,quantity=1}
            },true,inspect:result=>
            {
                var actual=(result.Dealer??Array.Empty<CartApplyDealerSnapshot>()).Select(value=>value.Identity).ToArray();
                var expected=new[]{CartApplyIdentityRef.Existing(sourceSecond.Id),CartApplyIdentityRef.Existing(sourceFirst.Id)};
                trace.Assert(actual.SequenceEqual(expected)&&result.Dealer[0].SourceOrdinal==secondOrdinal&&result.Dealer[1].SourceOrdinal==firstOrdinal&&
                    result.Dealer.All(value=>value.Deleted&&value.InsertionOrdinal==-1),
                    "frozen dealer removals follow raw reverse row order while SourceOrdinal retains original dealer positions");
            });

            CartPreviewCharacterizationFixtures.CreateNormalized(out var unitSource,out var unitCarId,161020452u);
            uint unitSeed=552001u;
            var firstUnit=CartPreviewCharacterizationFixtures.AddUsedUnit(unitSource,"l1c1/reverse-order-unit-first",ref unitSeed);
            var secondUnit=CartPreviewCharacterizationFixtures.AddUsedUnit(unitSource,"l1c1/reverse-order-unit-second",ref unitSeed);
            int firstUnitOrdinal=unitSource.inventory.dealer.FindIndex(value=>value?.Id==firstUnit.Id);
            int secondUnitOrdinal=unitSource.inventory.dealer.FindIndex(value=>value?.Id==secondUnit.Id);
            if(firstUnitOrdinal<0||secondUnitOrdinal<=firstUnitOrdinal){trace.Assert(false,"reverse-source unit fixture appends two actual used units in known source order");return;}
            C1Case(trace,"first-write-BuyUsed-unit-and-dealer-reverse-source-order",unitSource,unitCarId,new[]{
                new PartCartLine{action=CartAction.BuyUsed,assetId=secondUnit.Id,quantity=1},
                new PartCartLine{action=CartAction.Move,assetId=secondUnit.unit.element.id,destination=InventoryKind.Storage,destinationId=SharedInventory.Garage},
                new PartCartLine{action=CartAction.BuyUsed,assetId=firstUnit.Id,quantity=1},
                new PartCartLine{action=CartAction.Move,assetId=firstUnit.unit.element.id,destination=InventoryKind.Storage,destinationId=SharedInventory.Garage}
            },true,inspect:result=>
            {
                var expectedUnits=new[]{CartApplyIdentityRef.Existing(secondUnit.unit.element.id),CartApplyIdentityRef.Existing(firstUnit.unit.element.id)};
                var expectedDealer=new[]{CartApplyIdentityRef.Existing(secondUnit.Id),CartApplyIdentityRef.Existing(firstUnit.Id)};
                trace.Assert((result.Units??Array.Empty<CartApplyUnitSnapshot>()).Select(value=>value.Identity).SequenceEqual(expectedUnits)&&
                    result.Units[0].InsertionOrdinal==0&&result.Units[1].InsertionOrdinal==1&&result.Units.All(value=>value.SourceOrdinal==-1),
                    "frozen BuyUsed/Move chronology retains reverse-source first-touch order for unit snapshots");
                trace.Assert((result.Dealer??Array.Empty<CartApplyDealerSnapshot>()).Select(value=>value.Identity).SequenceEqual(expectedDealer)&&
                    result.Dealer[0].SourceOrdinal==secondUnitOrdinal&&result.Dealer[1].SourceOrdinal==firstUnitOrdinal,
                    "same raw sequence retains reverse-source dealer tombstone order and original source ordinals");
            });
        }

        static void C1MoveAndSubtype(Trace trace)
        {
            foreach (var category in new[] { PartCategory.Wheels, PartCategory.Tires })
            {
                CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, category == PartCategory.Wheels ? 16102036u : 16102037u);
                var car = source.cars.Find(item => item.instanceId == carId);
                var target = IdentityFindCornerUnitTarget(source, car, category);
                if (target == null || target.Positions.Length == 0) { trace.Assert(false, "actual installed " + category + " pack has real corner positions"); continue; }
                C1Case(trace, "NewUnits-" + category + "-actual-subtype", source, carId, new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = target.Definition.id, role = target.Role, quantity = 1, install = true, positions = new List<string> { target.Positions[0] } } }, true,
                    inspect: result => C1CheckInstalledSubtype(trace, result, target.Definition.id, target.Role, category, true, null));
                uint seed = category == PartCategory.Wheels ? 55731u : 55737u;
                var dealer = C0AddUsedCornerUnit(source, car, target.Definition, category, ref seed, out int tries);
                if (dealer == null) { trace.Assert(false, "bounded actual used " + category + " subtype generator succeeds; attempts=" + tries); continue; }
                var positions = InventoryUnits.Positions(source, car, dealer.DefinitionId, dealer.unit.element.role);
                if (positions == null || positions.Length == 0) { trace.Assert(false, "used " + category + " subtype has an authored installed corner"); continue; }
                C1Case(trace, "BuyUsed-" + category + "-actual-subtype", source, carId, new[] { new PartCartLine { action = CartAction.BuyUsed, assetId = dealer.Id, definitionId = dealer.DefinitionId, role = dealer.unit.element.role, quantity = 1, install = true, positions = new List<string> { positions[0] } } }, true,
                    inspect: result => C1CheckInstalledSubtype(trace, result, dealer.DefinitionId, dealer.unit.element.role, category, false, dealer.unit.element.id));
            }

            CartPreviewCharacterizationFixtures.CreateNormalized(out var sourceMove, out var carIdMove, 16102034u);
            var carMove = sourceMove.cars.Find(item => item.instanceId == carIdMove);
            var binding = carMove?.installed?.slots?.FirstOrDefault(item => item.slot == PartCategory.HeadIntake.ToString());
            var displaced = binding == null ? null : sourceMove.parts.Find(item => item.instanceId == binding.partInstanceId);
            var set = FindOffer(sourceMove, carMove, PartCategory.HeadIntake, false);
            var engine = sourceMove.engines.Find(item => item.instanceId == carMove.engineAssemblyId);
            if (displaced == null || set == null || engine == null) { trace.Assert(false, "Move cases have real installed root, stocked set, and engine"); return; }
            C1Case(trace, "purchase-Move-purchase", sourceMove, carIdMove, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1, install = true },
                new PartCartLine { action = CartAction.Move, assetId = displaced.instanceId, quantity = 0, destination = InventoryKind.Storage, destinationId = SharedInventory.Garage },
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 }
            }, true);
            C1Case(trace, "purchase-then-return-displaced-root", sourceMove, carIdMove, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1, install = true },
                new PartCartLine { action = CartAction.Move, assetId = displaced.instanceId, quantity = 0, destination = InventoryKind.Car, destinationId = carIdMove }
            }, true);
            C1Case(trace, "return-displaced-root-then-purchase", sourceMove, carIdMove, new[] {
                new PartCartLine { action = CartAction.Move, assetId = displaced.instanceId, quantity = 0, destination = InventoryKind.Car, destinationId = carIdMove },
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1, install = true }
            }, true);
            C1Case(trace, "whole-engine-detach-then-attach", sourceMove, carIdMove, new[] {
                new PartCartLine { action = CartAction.Move, assetId = engine.instanceId, quantity = 3, destination = InventoryKind.Storage, destinationId = SharedInventory.Storage },
                new PartCartLine { action = CartAction.Move, assetId = engine.instanceId, quantity = 0, destination = InventoryKind.Car, destinationId = carIdMove }
            }, true);
            C1Case(trace, "actual-asset-missing-destination-after-prefix", sourceMove, carIdMove, new[] {
                new PartCartLine { action = CartAction.Move, assetId = engine.instanceId, destination = InventoryKind.Storage, destinationId = SharedInventory.Storage },
                new PartCartLine { action = CartAction.Move, assetId = engine.instanceId, destination = InventoryKind.Car, destinationId = "missing-car" }
            }, false, errorIndex: 1);

            if (!KCreatePair(20261101u, true, out var mixed, out string mixedReason) || mixed.PistonA == null || mixed.PistonB == null || mixed.TireUnit?.element == null)
                trace.Assert(false, "K mixed overlay fixture provides real two-car engines, engine parts and TireCondition unit / " + mixedReason);
            else
            {
                var p = mixed.Profile;
                C1Case(trace, "K-C1-mixed-engine-part-unit-overlay", p, mixed.A.instanceId, new[] {
                    new PartCartLine { action = CartAction.Move, assetId = mixed.EngineA.instanceId, destination = InventoryKind.Car, destinationId = mixed.B.instanceId },
                    new PartCartLine { action = CartAction.Move, assetId = mixed.PistonA.instanceId, destination = InventoryKind.Storage, destinationId = SharedInventory.Garage },
                    new PartCartLine { action = CartAction.Move, assetId = mixed.TireUnit.element.id, destination = InventoryKind.Storage, destinationId = SharedInventory.Storage },
                    new PartCartLine { action = CartAction.Move, assetId = mixed.PistonA.instanceId, destination = InventoryKind.Engine, destinationId = mixed.EngineB.instanceId },
                    new PartCartLine { action = CartAction.Move, assetId = mixed.PistonB.instanceId, destination = InventoryKind.Storage, destinationId = SharedInventory.Garage }
                }, true, inspect:result=>
                {
                    var expectedCars=new[]{CartApplyIdentityRef.Existing(mixed.A.instanceId),CartApplyIdentityRef.Existing(mixed.B.instanceId)};
                    var expectedOwners=new[]{CartApplyIdentityRef.Existing(mixed.A.instanceId),CartApplyIdentityRef.Existing(mixed.EngineA.instanceId),
                        CartApplyIdentityRef.Existing(mixed.B.instanceId),CartApplyIdentityRef.Existing(mixed.EngineB.instanceId)};
                    var expectedEngines=new[]{CartApplyIdentityRef.Existing(mixed.EngineA.instanceId),CartApplyIdentityRef.Existing(mixed.EngineB.instanceId)};
                    trace.Assert((result.Cars??Array.Empty<CartApplyCarSnapshot>()).Select(value=>value.Identity).SequenceEqual(expectedCars),
                        "frozen K chronology first detaches source car A then affects target car B");
                    trace.Assert((result.Owners??Array.Empty<CartApplyOwnerSnapshot>()).Select(value=>value.Identity).SequenceEqual(expectedOwners),
                        "frozen K chronology records source car, its engine, target car, then stored destination engine on first touch");
                    trace.Assert((result.Engines??Array.Empty<CartApplyEngineSnapshot>()).Select(value=>value.Identity).SequenceEqual(expectedEngines),
                        "frozen K chronology first changes moved engine A then destination engine B");
                });
                C1Case(trace, "K-C1-forward-stored-engine-attach", p, mixed.A.instanceId, new[] {
                    new PartCartLine { action = CartAction.Move, assetId = mixed.PistonA.instanceId, destination = InventoryKind.Engine, destinationId = mixed.EngineB.instanceId },
                    new PartCartLine { action = CartAction.Move, assetId = mixed.EngineB.instanceId, destination = InventoryKind.Car, destinationId = mixed.B.instanceId }
                }, true);
                C1Case(trace, "K-C1-reverse-order-fails-at-second-row", p, mixed.A.instanceId, new[] {
                    new PartCartLine { action = CartAction.Move, assetId = mixed.EngineB.instanceId, destination = InventoryKind.Car, destinationId = mixed.B.instanceId },
                    new PartCartLine { action = CartAction.Move, assetId = mixed.PistonA.instanceId, destination = InventoryKind.Engine, destinationId = mixed.EngineB.instanceId }
                }, false, errorIndex: 1);
            }

            if (!KCreatePair(20261103u, false, out var late, out string lateReason))
                trace.Assert(false, "K late failure fixture has actual engines/parts / " + lateReason);
            else
            {
                var target = late.A;
                var part = KCarPart(late.Profile, target, PartCategory.Pistons);
                if (part == null) trace.Assert(false, "K late failure uses an actual installed piston part");
                else
                {
                    LegacyWholeEngineMoveReference.Move(late.Profile, late.EngineA.instanceId, InventoryKind.Storage, SharedInventory.Storage);
                    target.garageJobId = "k-target-job";
                    C1Case(trace, "K-C1-engine-prefix-then-real-garage-job-guard", late.Profile, target.instanceId, new[] {
                        new PartCartLine { action = CartAction.Move, assetId = late.EngineA.instanceId, destination = InventoryKind.Car, destinationId = late.B.instanceId },
                        new PartCartLine { action = CartAction.Move, assetId = part.instanceId, destination = InventoryKind.Car, destinationId = target.instanceId }
                    }, false, errorIndex: 1);
                }
            }

            CartPreviewCharacterizationFixtures.CreateNormalized(out var usedSource, out var usedCarId, 16102035u);
            uint usedSeed = 99173u;
            var used = CartPreviewCharacterizationFixtures.AddUsedUnit(usedSource, "l1c1/mixed-move-unit", ref usedSeed);
            C1Case(trace, "BuyUsed-unit-then-Move-same-identity", usedSource, usedCarId, new[] {
                new PartCartLine { action = CartAction.BuyUsed, assetId = used.Id, quantity = 1 },
                new PartCartLine { action = CartAction.Move, assetId = used.unit.element.id, quantity = 0, destination = InventoryKind.Storage, destinationId = SharedInventory.Storage }
            }, true);

            CartPreviewCharacterizationFixtures.CreateNormalized(out var tireSource, out var tireCarId, 16102044u);
            var tireCar = tireSource.cars.Find(item => item.instanceId == tireCarId);
            var tireSet = FindOffer(tireSource, tireCar, PartCategory.Tires, false);
            if (tireSet == null) { trace.Assert(false, "raw tire vectors use an authored stocked tire set"); return; }
            C1Case(trace, "NewSet-tire-loose-raw-members", tireSource, tireCarId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = tireSet.id, quantity = 1 } }, true,
                inspect: result => C1CheckRawLooseTire(trace, result, tireSet.id));
            C1Case(trace, "NewSet-tire-install-then-NewUnits-corner", tireSource, tireCarId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = tireSet.id, quantity = 1, install = true },
                new PartCartLine { action = CartAction.NewUnits, definitionId = tireSet.id, role = "tire", quantity = 1, install = true, positions = new List<string> { "tire:0" } }
            }, true, inspect: result => C1CheckRawInstalledTire(trace, result, tireSet.id));
            C1Case(trace, "NewSet-tire-install-then-NewUnits-q2-distinct-corners", tireSource, tireCarId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = tireSet.id, quantity = 1, install = true },
                new PartCartLine { action = CartAction.NewUnits, definitionId = tireSet.id, role = "tire", quantity = 2, install = true,
                    positions = new List<string> { "tire:0", "tire:1" } }
            }, true, inspect: result => C1CheckTireQuantityTwo(trace, result, tireSet.id, false));
            C1Case(trace, "NewSet-tire-install-then-NewUnits-q2-repeated-corner", tireSource, tireCarId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = tireSet.id, quantity = 1, install = true },
                new PartCartLine { action = CartAction.NewUnits, definitionId = tireSet.id, role = "tire", quantity = 2, install = true,
                    positions = new List<string> { "tire:0", "tire:0" } }
            }, true, inspect: result => C1CheckTireQuantityTwo(trace, result, tireSet.id, true));
        }

        static void C1ErrorsAndCatalog(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, 16102038u);
            var car = source.cars.Find(item => item.instanceId == carId);
            var set = FindOffer(source, car, PartCategory.HeadIntake, false);
            var target = CoreRowsInstalledUnitTarget(source, car);
            if (set == null || target == null) { trace.Assert(false, "error matrix uses actual set and unit target"); return; }
            C1Case(trace, "missing-definition", source, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = "l1c1-missing", quantity = 1 } }, false);
            C1Case(trace, "valid-prefix-then-null-row", source, carId, new PartCartLine[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 }, null }, false, errorIndex: 1);
            C1Case(trace, "valid-prefix-then-missing-used", source, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 }, new PartCartLine { action = CartAction.BuyUsed, assetId = "missing", quantity = 1 } }, false, errorIndex: 1);
            C1Case(trace, "invalid-role", source, carId, new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = target.Definition.id, role = "invalid", quantity = 1 } }, false);
            C1Case(trace, "null-positions-after-buy", source, carId, new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = target.Definition.id, role = target.Role, quantity = 1, install = true, positions = null } }, false);
            C1Case(trace, "invalid-position-after-buy", source, carId, new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = target.Definition.id, role = target.Role, quantity = 1, install = true, positions = new List<string> { "invalid" } } }, false);

            CartPreviewCharacterizationFixtures.CreateNormalized(out var cold, out var coldId, 16102039u);
            var coldCar = cold.cars.Find(item => item.instanceId == coldId); var coldSet = FindOffer(cold, coldCar, PartCategory.HeadIntake, false);
            C1Case(trace, "catalog-cold-index", cold, coldId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = coldSet.id, quantity = 1 } }, true, C0CatalogShape.ColdIndex);
            CartPreviewCharacterizationFixtures.CreateNormalized(out var malformed, out var malformedId, 16102040u);
            var malformedCar = malformed.cars.Find(item => item.instanceId == malformedId); var malformedSet = FindOffer(malformed, malformedCar, PartCategory.HeadIntake, false);
            C1Case(trace, "catalog-null-row-rebuild-order", malformed, malformedId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = malformedSet.id, quantity = 1 } }, false, C0CatalogShape.NullPartRow);
            CartPreviewCharacterizationFixtures.CreateNormalized(out var cash, out var cashId, 16102041u);
            var cashCar = cash.cars.Find(item => item.instanceId == cashId); var cashSet = FindOffer(cash, cashCar, PartCategory.HeadIntake, false); cash.cash = 0;
            C1Case(trace, "insufficient-cash", cash, cashId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = cashSet.id, quantity = 1 } }, false);
            CartPreviewCharacterizationFixtures.CreateNormalized(out var nullDealer, out var dealerCarId, 16102043u);
            nullDealer.inventory.dealer.Insert(0, null);
            C1Case(trace, "null-dealer-first-error", nullDealer, dealerCarId, new[] { new PartCartLine { action = CartAction.BuyUsed, assetId = "missing", quantity = 1 } }, false);
            CartPreviewCharacterizationFixtures.CreateNormalized(out var malformedDealer, out var malformedDealerCarId, 161020432u);
            uint malformedSeed = 1610432u;
            var malformedPart = CartPreviewCharacterizationFixtures.AddUsedPart(malformedDealer, "l1c1/malformed-dealer-part", ref malformedSeed);
            var malformedUnit = CartPreviewCharacterizationFixtures.AddUsedUnit(malformedDealer, "l1c1/malformed-dealer-unit", ref malformedSeed);
            malformedDealer.inventory.dealer.Add(new DealerStock { part = malformedPart.part, unit = malformedUnit.unit });
            C1Case(trace, "malformed-dealer-two-payload-discriminator", malformedDealer, malformedDealerCarId, new[] { new PartCartLine { action = CartAction.BuyUsed, assetId = null, quantity = 1 } }, false);

            CartPreviewCharacterizationFixtures.CreateNormalized(out var active, out var activeCarId, 161020431u);
            var activeCar = active.cars.Find(item => item.instanceId == activeCarId);
            var activeSet = FindOffer(active, activeCar, PartCategory.HeadIntake, false);
            var activeTarget = CoreRowsInstalledUnitTarget(active, activeCar);
            if (activeSet == null || activeTarget == null) { trace.Assert(false, "active-run cases have actual set and unit fixtures"); return; }
            active.runCheckpoints.Add(new RunCheckpoint { runId = "l1c1-active-run-guard", ownedVehicleId = activeCarId, settled = false });
            C1Case(trace, "active-run-NewSet-install-false", active, activeCarId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = activeSet.id, quantity = 1, install = false } }, true);
            C1Case(trace, "active-run-NewSet-install-true", active, activeCarId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = activeSet.id, quantity = 1, install = true } }, true);
            C1Case(trace, "active-run-NewUnits-guard", active, activeCarId, new[] { new PartCartLine { action = CartAction.NewUnits, definitionId = activeTarget.Definition.id, role = activeTarget.Role, quantity = 1 } }, false);
            uint activeSeed = 161041u;
            var activeUsed = CartPreviewCharacterizationFixtures.AddUsedPart(active, "l1c1/active-used-part", ref activeSeed);
            C1Case(trace, "active-run-BuyUsed-install-guard-prefix", active, activeCarId, new[] { new PartCartLine { action = CartAction.BuyUsed, assetId = activeUsed.Id, quantity = 1, install = true } }, false);
        }

        static void C1ReservationPreviewAndCollision(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, 161020399u);
            var car = source.cars.Find(item => item.instanceId == carId); var set = FindOffer(source, car, PartCategory.HeadIntake, false); var target = CoreRowsInstalledUnitTarget(source, car);
            if (set == null || target == null) { trace.Assert(false, "reservation vectors have actual stocked and installed targets"); return; }
            var reservationRows = new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 }, new PartCartLine { action = CartAction.NewUnits, definitionId = target.Definition.id, role = target.Role, quantity = 1 } };
            CartReservations.Capture(source, carId, reservationRows);
            bool capturedFalse = source.inventory.usingCartReservation = false;
            trace.Assert(!capturedFalse && source.inventory.cart != null && source.inventory.cart.carId == carId,
                "reservation false vector independently seeds usingCartReservation=false and a captured authored cart hold");
            C1Case(trace, "reservation-false-independent-source", source, carId, reservationRows, true, reservation: false);
            bool capturedTrue = source.inventory.usingCartReservation = true;
            trace.Assert(capturedTrue && source.inventory.cart != null && source.inventory.cart.carId == carId,
                "reservation true vector independently seeds usingCartReservation=true and preserves the same captured hold");
            C1Case(trace, "reservation-true-independent-source", source, carId, reservationRows, true, reservation: true);
            var a = C1ResultOnly(trace,source, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 } }, false);
            var b = C1ResultOnly(trace,source, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 } }, true);
            trace.Assert(a != null && b != null && TypedCanonical(a, new Dictionary<string, string>()) == TypedCanonical(b, new Dictionary<string, string>()), "preview true and false yield identical typed result");

            string sourceId = source.parts.FirstOrDefault(item => item != null)?.instanceId;
            C1Case(trace, "private-symbol-retries-real-source-id", source, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 } }, true,
                symbol: ordinal => ordinal == 0 ? sourceId : "__c1_retry_" + ordinal.ToString(CultureInfo.InvariantCulture));
            C1Case(trace, "private-symbol-never-appears-in-result", source, carId, new[] { new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 } }, true,
                symbol: ordinal => "__c1_private_hidden_" + ordinal.ToString(CultureInfo.InvariantCulture));
            string collision = "__c1_raw_existing_symbol_" + carId;
            C1Case(trace, "raw-symbol-string-is-always-existing", source, carId, new[] {
                new PartCartLine { action = CartAction.NewSet, definitionId = set.id, quantity = 1 },
                new PartCartLine { action = CartAction.Move, assetId = collision, quantity = 1, destination = InventoryKind.Storage, destinationId = SharedInventory.Storage }
            }, false, symbol: ordinal => ordinal == 0 ? collision : collision + ordinal.ToString(CultureInfo.InvariantCulture), errorIndex: 1);
            C1LiveCatalogOverride(trace);
        }

        static void C1MalformedTireSources(Trace trace)
        {
            uint fixtureSeed=161021000u;
            foreach(string shape in new[]{"null-array","length-one","null-member","wrong-type-member","empty","blank-id","unselected-blank-id"})
            {
                fixtureSeed=unchecked(fixtureSeed+1);
                CartPreviewCharacterizationFixtures.CreateNormalized(out var source,out var carId,fixtureSeed);
                var car=source.cars.Find(value=>value?.instanceId==carId);
                var target=IdentityFindCornerUnitTarget(source,car,PartCategory.Tires);
                var binding=car?.installed?.slots?.FirstOrDefault(value=>value?.slot==PartCategory.Tires.ToString());
                var originalPart=binding==null?null:source.parts.Find(value=>value?.instanceId==binding.partInstanceId);
                if(target==null||target.Positions.Length==0||originalPart?.condition?.elements==null||originalPart.condition.elements.Length!=4)
                {trace.Assert(false,"malformed tire guard vectors start from an actual authored four-tire installed pack / "+shape);continue;}
                string selectedPosition=shape=="unselected-blank-id"&&target.Positions.Length>1?target.Positions[1]:target.Positions[0];
                var row=new PartCartLine{action=CartAction.NewUnits,definitionId=target.Definition.id,role=target.Role,quantity=1,install=true,positions=new List<string>{selectedPosition}};
                if(shape=="unselected-blank-id"&&target.Positions.Length<2){trace.Assert(false,"unselected blank source tire vector has a second actual authored corner");continue;}
                // Build all three isolated graphs while the authored tire view is
                // valid, then corrupt corresponding records independently. The
                // strict compatibility getter intentionally throws on malformed
                // arrays, so fixture cloning must precede corruption.
                var fixture=C0BuildFixture(source,carId,new[]{row},C0CatalogShape.Warm);
                var baselinePart=fixture.Baseline.parts.Find(value=>value?.instanceId==originalPart.instanceId);
                var livePart=fixture.Live.parts.Find(value=>value?.instanceId==originalPart.instanceId);
                var frozenPart=fixture.Frozen.parts.Find(value=>value?.instanceId==originalPart.instanceId);
                C1CorruptTireSource(fixture.Baseline,baselinePart,shape);
                C1CorruptTireSource(fixture.Live,livePart,shape);
                C1CorruptTireSource(fixture.Frozen,frozenPart,shape);
                string before=SourceSnapshot(fixture.Live);var references=C1ProfileReferencesWithoutTireGetter(fixture.Live);
                var memberArray=livePart.condition.elements;var memberReferences=memberArray?.Cast<object>().ToArray()??Array.Empty<object>();
                string frozenBefore=SourceSnapshot(fixture.Frozen);
                Exception frozen=C1Frozen(fixture.Frozen,carId,fixture.FrozenRows,false,ValuationPolicy.Defaults(),out var frozenObserver);
                trace.Add("MALFORMED_FROZEN|"+shape+"|error="+C0Error(frozen)+"|mutated="+(frozenBefore!=SourceSnapshot(fixture.Frozen)));
                var candidate=C1Evaluate(fixture.Live,carId,fixture.LiveRows,false,ValuationPolicy.Defaults(),null,out bool disposed,out bool children,out int tokens,out var observer);
                string expectedPath=shape=="blank-id"?".condition.tires[0].id":".condition.tires";
                bool unsupported=candidate.ErrorType==typeof(CartApplyMalformedSourceException).FullName&&candidate.ErrorIndex==0&&
                    candidate.ErrorMessage=="Unsupported malformed source: parts["+originalPart.instanceId+"]"+expectedPath&&candidate.ErrorParameterName==null;
                if(shape=="unselected-blank-id")
                    trace.Assert(frozen==null&&candidate.Succeeded,"unselected blank existing tire member remains permitted and untouched by replacing a different actual corner / "+shape+";frozen="+C0Error(frozen));
                else
                {
                    trace.Assert(unsupported,"existing malformed tire source fails at the guarded branch with exact typed UnsupportedMalformed path / "+shape+";candidate="+candidate.ErrorType+":"+candidate.ErrorMessage);
                    if(shape=="blank-id")trace.Assert(frozen==null,"frozen legacy raw Apply confirms this branch would repair/allocate the selected blank source tire ID / "+shape);
                }
                bool unchanged=before==SourceSnapshot(fixture.Live)&&C0SameReferences(references,C1ProfileReferencesWithoutTireGetter(fixture.Live))&&
                    ReferenceEquals(memberArray,livePart.condition.elements)&&(memberArray?.Length??-1)==(livePart.condition.elements?.Length??-1);
                for(int i=0;i<memberReferences.Length&&unchanged;i++)unchanged&=ReferenceEquals(memberReferences[i],livePart.condition.elements[i]);
                trace.Assert(unchanged&&disposed&&children&&tokens==0&&observer.Count==0&&observer.Closed&&frozenObserver.Closed,
                    "malformed or blank-ID candidate preserves source values/references, performs no identity entropy and disposes its scope / "+shape);
                if(shape=="unselected-blank-id"&&candidate.Succeeded)
                {var writes=C1QuoteEffectParity(trace,"unselected-blank-existing-tire-member",fixture.Baseline,carId,fixture.LiveRows,fixture.Live.inventory.usingCartReservation,candidate,C0CatalogShape.Warm,false,frozenObserver,fixture.Frozen);C1SnapshotParity(trace,"unselected-blank-existing-tire-member",fixture,candidate,fixture.LiveRows,frozenObserver,writes);}
                else trace.Assert(candidate.Effects==null&&candidate.Parts==null&&candidate.Engines==null&&candidate.Units==null&&candidate.Dealer==null&&candidate.Cars==null&&candidate.Owners==null&&candidate.SupplyDeltas==null&&candidate.Identities==null&&!candidate.HasCashDelta,
                    "UnsupportedMalformed failure suppresses every partial DTO, supply, identity and cash output / "+shape);
            }
        }

        static void C1CorruptTireSource(PlayerProfile profile,PartInstance part,string shape)
        {
            if(shape=="length-one")part.condition.elements=new[]{part.condition.elements[0]};
            else if(shape=="null-member")part.condition.elements[1]=null;
            else if(shape=="wrong-type-member")
            {
                var other=profile.parts.FirstOrDefault(value=>value!=null&&!ReferenceEquals(value,part)&&value.condition?.elements?.Any(element=>element!=null&&!(element is TireCondition))==true);
                var wrong=other?.condition?.elements?.FirstOrDefault(element=>element!=null&&!(element is TireCondition));
                if(wrong==null)throw new InvalidOperationException("Wrong-type malformed fixture lacks an authored non-tire EngineElement in its independent graph.");
                var widened=part.condition.elements.Cast<EngineElement>().ToArray();
                widened[1]=wrong;
                part.condition.elements=widened;
            }
            else if(shape=="empty")part.condition.elements=Array.Empty<EngineElement>();
            else if(shape=="null-array")part.condition.elements=null;
            else if(shape=="blank-id"||shape=="unselected-blank-id")((TireCondition)part.condition.elements[0]).id="";
        }

        static object[] C1ProfileReferencesWithoutTireGetter(PlayerProfile profile)
        {
            var refs=new List<object>{profile,profile?.parts,profile?.cars,profile?.engines,profile?.looseEngineElements,profile?.runCheckpoints,
                profile?.inventory,profile?.inventory?.dealer,profile?.inventory?.market,profile?.inventory?.market?.parts,profile?.inventory?.market?.index,
                profile?.inventory?.cart,profile?.inventory?.cart?.lines,profile?.inventory?.cart?.supply,profile?.catalog,profile?.catalog?.parts};
            var catalogIndex=typeof(MechanicalCatalog).GetField("partIndex",BindingFlags.Instance|BindingFlags.NonPublic);
            if(profile?.catalog!=null&&catalogIndex!=null)refs.Add(catalogIndex.GetValue(profile.catalog));
            var tireView=typeof(ComponentCondition).GetField("tireView",BindingFlags.Instance|BindingFlags.NonPublic);
            if(profile?.parts!=null)foreach(var part in profile.parts)
            {
                refs.Add(part);refs.Add(part?.condition);refs.Add(part?.condition?.elements);
                if(tireView!=null)refs.Add(part?.condition==null?null:tireView.GetValue(part.condition));
            }
            if(profile?.cars!=null)foreach(var car in profile.cars){refs.Add(car);refs.Add(car?.installed);refs.Add(car?.installed?.slots);}
            if(profile?.engines!=null)foreach(var engine in profile.engines){refs.Add(engine);refs.Add(engine?.slots);}
            if(profile?.looseEngineElements!=null)foreach(var unit in profile.looseEngineElements){refs.Add(unit);refs.Add(unit?.element);refs.Add(unit?.tire);}
            if(profile?.inventory?.dealer!=null)foreach(var stock in profile.inventory.dealer){refs.Add(stock);refs.Add(stock?.part);refs.Add(stock?.unit);refs.Add(stock?.unit?.element);refs.Add(stock?.unit?.tire);}
            if(profile?.inventory?.market?.parts!=null)foreach(var row in profile.inventory.market.parts){refs.Add(row);refs.Add(row?.definitions);}
            if(profile?.inventory?.cart?.lines!=null)foreach(var row in profile.inventory.cart.lines){refs.Add(row);refs.Add(row?.positions);}
            if(profile?.inventory?.cart?.supply!=null)foreach(var hold in profile.inventory.cart.supply)refs.Add(hold);
            return refs.ToArray();
        }

        static void C1LiveCatalogOverride(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source, out var carId, 161020400u);
            var sourceCar = source.cars.Find(item => item.instanceId == carId);
            var offer = FindOffer(source, sourceCar, PartCategory.HeadIntake, false);
            if (offer == null) { trace.Assert(false, "live catalog override probe has an actual stocked compatible definition"); return; }
            var live = CartPreviewLiteralTestClone.Copy(source);
            var liveCatalog = C0NewIndependentCatalog(source.catalog, C0CatalogShape.Warm);
            var alternate = C0NewIndependentCatalog(source.catalog, C0CatalogShape.Warm);
            var alternatePart = alternate.Find(offer.id);
            if (alternatePart == null || alternatePart.price < 4) { trace.Assert(false, "alternate authored catalog contains the same real product with adjustable positive price"); return; }
            int originalPrice = alternatePart.price;
            alternatePart.price = Math.Max(1, originalPrice - Math.Max(1, originalPrice / 4));
            alternate.RebuildIndexes();
            live.catalog = liveCatalog;
            int liveCashBefore = live.cash;
            string liveId = null; Exception liveError = null;
            try { liveId = PartOperations.Buy(live, alternate, carId, offer.id, false); }
            catch (Exception error) { liveError = error; }

            var frozen = CartPreviewLiteralTestClone.Copy(source);
            frozen.catalog = C0NewIndependentCatalog(source.catalog, C0CatalogShape.Warm);
            var frozenAlternate = C0NewIndependentCatalog(source.catalog, C0CatalogShape.Warm);
            var frozenPart = frozenAlternate.Find(offer.id);
            frozenPart.price = alternatePart.price; frozenAlternate.RebuildIndexes();
            string frozenId = null;
            Exception frozenError = C1InvokeFrozenBuyPart(frozen, frozenAlternate, carId, offer.id, false, out frozenId);

            var control = CartPreviewLiteralTestClone.Copy(source);
            control.catalog = C0NewIndependentCatalog(source.catalog, C0CatalogShape.Warm);
            int controlCashBefore = control.cash;
            Exception controlError = null;
            try { PartOperations.Buy(control, control.catalog, carId, offer.id, false); }
            catch (Exception error) { controlError = error; }
            int actualDelta = liveCashBefore - live.cash;
            int frozenDelta = frozenError == null ? source.cash - frozen.cash : int.MinValue;
            int controlDelta = controlError == null ? controlCashBefore - control.cash : int.MinValue;
            trace.Assert(liveError == null && frozenError == null && controlError == null && !string.IsNullOrEmpty(liveId) && !string.IsNullOrEmpty(frozenId),
                "explicit live catalog buy and frozen C1 closure succeed with actual authored stock / live=" + C0Error(liveError) + ";frozen=" + C0Error(frozenError));
            trace.Assert(actualDelta == frozenDelta && actualDelta != controlDelta && live.catalog.Find(offer.id).price == originalPrice,
                "PartOperations.Buy honors the separately supplied authored catalog price without replacing owner.catalog / explicit=" + actualDelta + ";owner=" + controlDelta);

            var nullCatalog = CartPreviewLiteralTestClone.Copy(source);
            Exception nullLive = null;
            try { PartOperations.Buy(nullCatalog, null, carId, offer.id, false); }
            catch (Exception error) { nullLive = error; }
            var nullFrozen = CartPreviewLiteralTestClone.Copy(source);
            Exception nullReference = C1InvokeFrozenBuyPart(nullFrozen, null, carId, offer.id, false, out _);
            trace.Assert(nullLive != null && nullReference != null && nullLive.GetType() == nullReference.GetType() && nullLive.Message == nullReference.Message &&
                nullLive is NullReferenceException, "explicit null catalog keeps natural lookup null-receiver failure after the car guard");
            var missingLive = CartPreviewLiteralTestClone.Copy(source);
            Exception missingLiveError = null;
            try { PartOperations.Buy(missingLive, null, "missing-car", offer.id, false); }
            catch (Exception error) { missingLiveError = error; }
            var missingFrozen = CartPreviewLiteralTestClone.Copy(source);
            Exception missingFrozenError = C1InvokeFrozenBuyPart(missingFrozen, null, "missing-car", offer.id, false, out _);
            trace.Assert(missingLiveError != null && missingFrozenError != null && missingLiveError.GetType() == missingFrozenError.GetType() && missingLiveError.Message == missingFrozenError.Message,
                "car guard runs before explicit null catalog lookup, matching frozen order");
        }

        static Exception C1InvokeFrozenBuyPart(PlayerProfile profile, MechanicalCatalog catalog, string carId, string definitionId, bool install, out string result)
        {
            result = null;
            var method = typeof(LegacyC1CartApplyReference).GetMethod("BuyPart", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException("LegacyC1CartApplyReference.BuyPart");
            try { result = method.Invoke(null, new object[] { profile, catalog, carId, definitionId, install }) as string; return null; }
            catch (TargetInvocationException error) { return error.InnerException ?? error; }
        }

        static OrderedCartApplyResult C1ResultOnly(Trace trace,PlayerProfile source, string carId, PartCartLine[] rows, bool preview)
        {
            var fixture = C0BuildFixture(source, carId, rows, C0CatalogShape.Warm);
            Exception frozen=C1Frozen(fixture.Frozen,carId,fixture.FrozenRows,preview,ValuationPolicy.Defaults(),out var frozenObserver);
            var result=C1Evaluate(fixture.Live, carId, fixture.LiveRows, preview, ValuationPolicy.Defaults(), null, out _, out _, out _, out _);
            trace.Assert(frozen==null&&result.Succeeded&&c1CandidateJsonDelta==0,"preview candidate and frozen raw Apply both succeed without JSON / preview="+preview+";frozen="+C0Error(frozen));
            if(frozen==null){var writes=C1QuoteEffectParity(trace,"preview-result-"+preview,source,carId,fixture.LiveRows,fixture.Live.inventory.usingCartReservation,result,C0CatalogShape.Warm,preview,frozenObserver,fixture.Frozen);C1SnapshotParity(trace,"preview-result-"+preview,fixture,result,fixture.LiveRows,frozenObserver,writes);}
            return result;
        }

        static void C1NullCase(Trace trace, string label, PartCartLine[] rows)
        {
            Exception frozen = C1Frozen(null, null, rows, false, ValuationPolicy.Defaults(), out var legacyObserver);
            var candidate = C1Evaluate(null, null, rows, false, ValuationPolicy.Defaults(), null, out bool disposed, out bool children, out int tokens, out var observer);
            bool match = (frozen == null) == candidate.Succeeded && (frozen == null || candidate.ErrorType == frozen.GetType().FullName && candidate.ErrorMessage == frozen.Message && candidate.ErrorParameterName == (frozen as ArgumentException)?.ParamName);
            trace.Assert(match && disposed && children && tokens == 0 && observer.Count == 0 && observer.Closed && legacyObserver.Closed, "null owner raw dispatch exact and lazy / " + label + " / " + C0Error(frozen));
            trace.Assert(c1CandidateJsonDelta == 0, "null-owner candidate dispatch performs no runtime JSON encoding / " + label);
            trace.Assert(!candidate.HasCashDelta && candidate.CashBefore == null && candidate.CashAfter == null && candidate.CashDelta == null, "null/noop/sale dispatch leaves cash absent / " + label);
            if (frozen == null)
            {
                int sales = rows.Count(row => row != null && (row.action == CartAction.SellPart || row.action == CartAction.SellUnit));
                trace.Assert(candidate.Effects?.Length == sales && candidate.Effects.All(effect => effect.Branch == CartApplyBranch.SkippedSale && effect.QuantityOrdinal == -1 && !effect.Identity.HasValue && !effect.PaidAmount.HasValue && effect.Quote == null), "sale is metadata-only skip effect / " + label);
                trace.Assert(candidate.Parts?.Length==0&&candidate.Engines?.Length==0&&candidate.Units?.Length==0&&candidate.Dealer?.Length==0&&candidate.Cars?.Length==0&&candidate.Owners?.Length==0&&candidate.SupplyDeltas?.Length==0&&candidate.Identities?.Length==0,
                    "null-owner sale/no-op success has owned empty typed snapshot arrays and no identities / "+label);
            }
            else C1Failure(trace, label, candidate, frozen, 0);
        }

        static void C1Case(Trace trace, string label, PlayerProfile baseline, string carId, PartCartLine[] rows, bool success,
            C0CatalogShape shape = C0CatalogShape.Warm, bool preview = false, bool? reservation = null, Func<int, string> symbol = null, int errorIndex = 0,
            Action<OrderedCartApplyResult> inspect = null)
        {
            C0Fixture fixture = C0BuildFixture(baseline, carId, rows, shape);
            if (reservation.HasValue) { fixture.Live.inventory.usingCartReservation = reservation.Value; fixture.Frozen.inventory.usingCartReservation = reservation.Value; }
            bool reservationBefore = fixture.Live.inventory.usingCartReservation;
            string before = SourceSnapshot(fixture.Live), index = C0CatalogIndexSnapshot(fixture.Live.catalog), market = C0MarketIndexSnapshot(fixture.Live);
            var refs = C0ProfileReferences(fixture.Live); var rowRefs = fixture.LiveRows.Cast<object>().ToArray(); string rowBefore = RowsSnapshot(fixture.LiveRows);
            var random = UnityEngine.Random.state;
            Exception frozen = C1Frozen(fixture.Frozen, carId, fixture.FrozenRows, preview, ValuationPolicy.Defaults(), out var frozenObserver);
            UnityEngine.Random.state = random;
            var result = C1Evaluate(fixture.Live, carId, fixture.LiveRows, preview, ValuationPolicy.Defaults(), symbol, out bool disposed, out bool children, out int tokens, out var observer);
            bool rng = random.Equals(UnityEngine.Random.state); UnityEngine.Random.state = random;
            bool matches = frozen == null ? result.Succeeded && result.ErrorType == null : !result.Succeeded && result.ErrorType == frozen.GetType().FullName && result.ErrorMessage == frozen.Message && result.ErrorParameterName == (frozen as ArgumentException)?.ParamName;
            trace.Assert(matches, "candidate exact frozen result/error type/message/parameter / " + label + " / frozen=" + C0Error(frozen) + ";candidate=" + result.ErrorType + ":" + result.ErrorMessage);
            trace.Assert((frozen == null) == success, "fixture reaches required legacy boundary / " + label + " / " + C0Error(frozen));
            trace.Assert(disposed && children && tokens == 0, "candidate scope, children and token map dispose / " + label);
            trace.Assert(before == SourceSnapshot(fixture.Live) && index == C0CatalogIndexSnapshot(fixture.Live.catalog) && market == C0MarketIndexSnapshot(fixture.Live) && C0SameReferences(refs, C0ProfileReferences(fixture.Live)), "candidate source values, refs, catalog and market indexes stay unchanged / " + label);
            trace.Assert(rowBefore == RowsSnapshot(fixture.LiveRows) && C0SameRowReferences(rowRefs, fixture.LiveRows), "candidate input rows, positions and references stay unchanged / " + label);
            trace.Assert(rng && observer.Count == 0 && observer.Closed && frozenObserver.Closed, "candidate preserves RNG and uses no gameplay identity/entropy / " + label);
            trace.Assert(c1CandidateJsonDelta == 0, "candidate projection performs no runtime JSON encoding / " + label + ";calls=" + c1CandidateJsonDelta);
            if (frozen == null) C1Success(trace, label, fixture, result, fixture.LiveRows);
            else C1Failure(trace, label, result, frozen, errorIndex);
            C1ExpectedWriteSet expectedWrites=null;
            if (frozen == null) expectedWrites=C1QuoteEffectParity(trace,label,baseline,carId,fixture.LiveRows,reservationBefore,result,shape,preview,frozenObserver,fixture.Frozen);
            if (frozen == null) C1SnapshotParity(trace, label, fixture, result, fixture.LiveRows, frozenObserver,expectedWrites);
            if (frozen == null && inspect != null) inspect(result);
            if (reservation.HasValue) trace.Assert(fixture.Live.inventory.usingCartReservation == reservationBefore, "captured usingCartReservation remains unchanged / " + label + "=" + reservationBefore);
            if (symbol != null)
            {
                var ordinaryFixture = C0BuildFixture(baseline, carId, rows, shape);
                var ordinary = C1Evaluate(ordinaryFixture.Live, carId, ordinaryFixture.LiveRows, preview, ValuationPolicy.Defaults(), null, out _, out _, out _, out _);
                bool same = TypedCanonical(ordinary, new Dictionary<string, string>()) == TypedCanonical(result, new Dictionary<string, string>());
                trace.Assert(same, "forced symbol collision changes only private spelling; exported result matches default namespace / " + label);
                if (result.Succeeded && !PhysicalIds(baseline).Contains(symbol(0)))
                    trace.Assert(C1NoPrivateExistingIdentity(result, symbol(0)), "created identity fields use mapped Virtual refs; private symbol never appears as ExistingId / " + label);
            }
            trace.Add("C1_CASE|" + label + "|success=" + result.Succeeded + "|frozen=" + C0Error(frozen) + "|effects=" + (result.Effects?.Length ?? -1) + "|cash=" + result.HasCashDelta);
        }

        static C1ExpectedWriteSet C1QuoteEffectParity(Trace trace,string label,PlayerProfile baseline,string carId,PartCartLine[] rows,bool reservation,OrderedCartApplyResult result,C0CatalogShape shape,bool preview,RuntimeEntropy.IdentityObservation monolithicObservation,PlayerProfile monolithicProfile)
        {
            // Replay the frozen driver one quantity at a time. Quote is observed
            // before each frozen purchase and the preceding purchase has already
            // changed the disposable stock/hold ledger.
            var replay=C0BuildFixture(baseline,carId,rows,shape);
            replay.Frozen.inventory.usingCartReservation=reservation;
            var expected=new List<(int row,int quantity,SupplyQuote quote)>();
            var replayEmissions=new List<RuntimeEntropy.IdentityEmission>();
            var writes=new C1ExpectedWriteSet();
            var marketCount=typeof(LegacyC1CartApplyReference).GetMethod("MarketCount",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(PlayerProfile),typeof(OwnedCar),typeof(PartDefinition)},null);
            var unitCount=typeof(LegacyC1CartApplyReference).GetMethod("UnitCount",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(PlayerProfile),typeof(OwnedCar),typeof(PartDefinition)},null);
            bool valid=marketCount!=null&&unitCount!=null;
            for(int rowIndex=0;rowIndex<rows.Length&&valid;rowIndex++)
            {
                var row=rows[rowIndex];if(row==null){valid=false;break;}
                int count=row.action==CartAction.Move||row.action==CartAction.SellPart||row.action==CartAction.SellUnit?1:Math.Max(0,row.quantity);
                for(int ordinal=0;ordinal<count;ordinal++)
                {
                    var beforeSignatures=C1CaptureWriteSignatures(replay.Frozen);
                    var step=new PartCartLine{action=row.action,assetId=row.assetId,definitionId=row.definitionId,role=row.role,quantity=1,
                        install=row.install&&(row.action!=CartAction.NewSet||ordinal==0),destination=row.destination,destinationId=row.destinationId,
                        positions=row.positions==null?null:new List<string>()};
                    if(row.positions!=null&&ordinal<row.positions.Count)step.positions.Add(row.positions[ordinal]);
                    if(row.action==CartAction.NewSet||row.action==CartAction.NewUnits)
                    {
                        var definition=LegacyMechanicalCatalogReference.Find(replay.Frozen.catalog,row.definitionId);
                        var car=replay.Frozen.cars?.FirstOrDefault(value=>value?.instanceId==carId);
                        if(definition==null||car==null){valid=false;break;}
                        int units;bool isUnit=row.action==CartAction.NewUnits;
                        try{units=(int)(isUnit?unitCount.Invoke(null,new object[]{replay.Frozen,car,definition}):marketCount.Invoke(null,new object[]{replay.Frozen,car,definition}));}
                        catch(TargetInvocationException){valid=false;break;}
                        expected.Add((rowIndex,ordinal,LegacyScopedSupplyReference.Quote(replay.Frozen,definition,units,isUnit)));
                    }
                    var error=C1Frozen(replay.Frozen,carId,new[]{step},preview,ValuationPolicy.Defaults(),out var stepObservation);
                    if(stepObservation!=null)for(int emission=0;emission<stepObservation.Count;emission++)replayEmissions.Add(stepObservation.EventAt(emission));
                    if(error!=null){valid=false;break;}
                    C1UnionObservedStepWrites(writes,beforeSignatures,C1CaptureWriteSignatures(replay.Frozen));
                    C1AddExplicitStepWrites(writes,replay.Frozen,step,carId,beforeSignatures);
                }
            }
            var actual=result.Effects??Array.Empty<CartApplyRowEffect>();
            bool same=valid&&expected.Count==actual.Count(effect=>effect.Branch==CartApplyBranch.NewSet||effect.Branch==CartApplyBranch.NewUnits);
            foreach(var item in expected)
            {
                var effect=actual.FirstOrDefault(value=>value.RowIndex==item.row&&value.QuantityOrdinal==item.quantity);
                var quote=effect?.Quote;
                same&=quote!=null&&quote.Cost==item.quote.cost&&quote.Fee==item.quote.fee&&quote.LocalUnits==item.quote.localUnits&&quote.ExpressUnits==item.quote.expressUnits&&effect.PaidAmount==item.quote.cost;
            }
            trace.Assert(same,"each NewSet/NewUnits effect quote fields match sequential frozen LegacyScopedSupplyReference observations / "+label);
            bool trajectory=valid&&monolithicObservation!=null&&!monolithicObservation.Overflowed&&replayEmissions.Count==monolithicObservation.Count;
            var aliases=new Dictionary<string,string>(StringComparer.Ordinal);
            if(trajectory)for(int i=0;i<replayEmissions.Count;i++)
            {
                var replayEvent=replayEmissions[i];var wholeEvent=monolithicObservation.EventAt(i);
                if(replayEvent.Scope!=wholeEvent.Scope){trajectory=false;break;}
                string replayId=replayEvent.Scope+"-"+replayEvent.Value.ToString("N"),wholeId=wholeEvent.Scope+"-"+wholeEvent.Value.ToString("N"),canonical="<generated:"+i.ToString(CultureInfo.InvariantCulture)+">";
                aliases[replayId]=canonical;aliases[wholeId]=canonical;
                writes.ReplayToWhole[replayId]=wholeId;
                if(replayEvent.Scope=="part")
                {
                    var replayPart=replay.Frozen.parts?.FirstOrDefault(value=>value?.instanceId==replayId);
                    var wholePart=monolithicProfile.parts?.FirstOrDefault(value=>value?.instanceId==wholeId);
                    int replayCount=replayPart?.condition?.elements?.Length??0,wholeCount=wholePart?.condition?.elements?.Length??0;
                    if(replayCount!=wholeCount){trajectory=false;break;}
                    for(int member=0;member<replayCount;member++)
                    {
                        string replayMember=replayId+"/element/"+member.ToString(CultureInfo.InvariantCulture),wholeMember=wholeId+"/element/"+member.ToString(CultureInfo.InvariantCulture),memberAlias=canonical+"/element/"+member.ToString(CultureInfo.InvariantCulture);
                        aliases[replayMember]=memberAlias;aliases[wholeMember]=memberAlias;
                    }
                }
            }
            trajectory&=C1CanonicalFrozenProfile(replay.Frozen,aliases)==C1CanonicalFrozenProfile(monolithicProfile,aliases);
            trace.Assert(trajectory,"sequential frozen quote replay reaches the monolithic frozen profile/catalog/market/cash state after generated-ID event mapping / "+label);
            C1PruneTransientExpectedWrites(writes,baseline,replay.Frozen);
            return writes;
        }

        static string C1CanonicalFrozenProfile(PlayerProfile profile,Dictionary<string,string> generatedAliases)
            =>TypedCanonical(profile,generatedAliases)+"\nCATALOG\n"+CatalogSnapshot(profile)+"\nRAW_SELECTION_IDS\n"+(profile.selectedOwnedVehicleId??"<null>")+"/"+(profile.selectedVehicleId??"<null>");

        sealed class C1WriteSignatures
        {
            internal readonly Dictionary<string,string> Parts=new Dictionary<string,string>(StringComparer.Ordinal),Engines=new Dictionary<string,string>(StringComparer.Ordinal),Units=new Dictionary<string,string>(StringComparer.Ordinal),Dealer=new Dictionary<string,string>(StringComparer.Ordinal),Cars=new Dictionary<string,string>(StringComparer.Ordinal),Owners=new Dictionary<string,string>(StringComparer.Ordinal);
            internal readonly Dictionary<string,string> EngineParents=new Dictionary<string,string>(StringComparer.Ordinal);
            internal readonly Dictionary<string,PartLocation> PartLocations=new Dictionary<string,PartLocation>(StringComparer.Ordinal);
            internal readonly Dictionary<string,string> PartStorage=new Dictionary<string,string>(StringComparer.Ordinal);
        }
        static C1WriteSignatures C1CaptureWriteSignatures(PlayerProfile p)
        {
            var result=new C1WriteSignatures();
            foreach(var value in p.parts??new List<PartInstance>())if(value?.instanceId!=null){result.Parts[value.instanceId]=TypedCanonical(value,new Dictionary<string,string>());result.PartLocations[value.instanceId]=value.location;result.PartStorage[value.instanceId]=value.storageId;}
            foreach(var value in p.engines??new List<EngineAssemblyInstance>())if(value?.instanceId!=null)result.Engines[value.instanceId]=TypedCanonical(value,new Dictionary<string,string>());
            foreach(var value in p.looseEngineElements??new List<LooseEngineElement>())if(value?.element?.id!=null)result.Units[value.element.id]=TypedCanonical(value,new Dictionary<string,string>());
            foreach(var value in p.inventory?.dealer??new List<DealerStock>())if(value?.Id!=null)result.Dealer[value.Id]=TypedCanonical(value,new Dictionary<string,string>());
            foreach(var value in p.cars??new List<OwnedCar>())if(value?.instanceId!=null)
            {result.Cars[value.instanceId]=TypedCanonical(value,new Dictionary<string,string>());result.Owners["car:"+value.instanceId]=TypedCanonical(value.installed?.slots,new Dictionary<string,string>());}
            foreach(var value in p.engines??new List<EngineAssemblyInstance>())if(value?.instanceId!=null){result.Owners["engine:"+value.instanceId]=TypedCanonical(value.slots,new Dictionary<string,string>());result.EngineParents[value.instanceId]=value.parentVehicleId;}
            return result;
        }
        static void C1UnionObservedStepWrites(C1ExpectedWriteSet writes,C1WriteSignatures before,C1WriteSignatures after)
        {
            C1UnionChanged(writes.Parts,before.Parts,after.Parts);C1UnionChanged(writes.Engines,before.Engines,after.Engines);C1UnionChanged(writes.Units,before.Units,after.Units);C1UnionChanged(writes.Dealer,before.Dealer,after.Dealer);C1UnionChanged(writes.Cars,before.Cars,after.Cars);C1UnionChanged(writes.Owners,before.Owners,after.Owners);
        }
        static void C1UnionChanged(HashSet<string> output,Dictionary<string,string> before,Dictionary<string,string> after)
        {
            foreach(var item in before)if(!after.TryGetValue(item.Key,out string value)||value!=item.Value)output.Add(item.Key);
            foreach(var item in after)if(!before.ContainsKey(item.Key))output.Add(item.Key);
        }
        static void C1AddExplicitStepWrites(C1ExpectedWriteSet writes,PlayerProfile p,PartCartLine row,string carId,C1WriteSignatures before)
        {
            if(row.action==CartAction.BuyUsed)
            {
                writes.Dealer.Add(row.assetId);
                var stock=p.inventory?.dealer?.FirstOrDefault(value=>value?.Id==row.assetId);
                if(stock?.IsPart==true)writes.Parts.Add(row.assetId);
                if(stock?.IsUnit==true)writes.Units.Add(row.assetId);
                else if(stock==null)
                {
                    if(p.parts?.Any(value=>value?.instanceId==row.assetId)==true)writes.Parts.Add(row.assetId);
                    if(p.looseEngineElements?.Any(value=>value?.element?.id==row.assetId)==true)writes.Units.Add(row.assetId);
                }
            }
            bool installsTarget=row.install&&(row.action==CartAction.NewSet||row.action==CartAction.BuyUsed&&p.parts?.Any(value=>value?.instanceId==row.assetId)==true||row.positions!=null&&row.positions.Count>0);
            if(row.action!=CartAction.Move&&installsTarget)
            {
                var target=p.cars?.FirstOrDefault(value=>value?.instanceId==carId);
                if(target!=null)
                {
                    writes.Cars.Add(carId);writes.Owners.Add("car:"+carId);
                }
            }
            if(row.action!=CartAction.Move)return;
            var sourceEngine=p.engines?.FirstOrDefault(value=>value?.instanceId==row.assetId);
            if(sourceEngine!=null&&row.destination==InventoryKind.Car&&before.EngineParents.TryGetValue(row.assetId,out string originalParent)&&originalParent==row.destinationId)return;
            bool unchangedLooseStorageMove=row.destination==InventoryKind.Storage&&before.PartLocations.TryGetValue(row.assetId,out PartLocation priorLocation)&&priorLocation==PartLocation.Inventory&&before.PartStorage.TryGetValue(row.assetId,out string priorStorage)&&priorStorage==row.destinationId;
            if(!unchangedLooseStorageMove&&p.parts?.Any(value=>value?.instanceId==row.assetId)==true)writes.Parts.Add(row.assetId);
            if(sourceEngine!=null)writes.Engines.Add(row.assetId);
            if(p.looseEngineElements?.Any(value=>value?.element?.id==row.assetId)==true)writes.Units.Add(row.assetId);
            if(row.destination==InventoryKind.Car)
            {
                writes.Cars.Add(row.destinationId);writes.Owners.Add("car:"+row.destinationId);
                var car=p.cars?.FirstOrDefault(value=>value?.instanceId==row.destinationId);
                if(sourceEngine!=null&&car!=null)writes.Owners.Add("engine:"+sourceEngine.instanceId);
            }
            else if(row.destination==InventoryKind.Engine)writes.Owners.Add("engine:"+row.destinationId);
        }

        static void C1PruneTransientExpectedWrites(C1ExpectedWriteSet writes,PlayerProfile baseline,PlayerProfile final)
        {
            void Prune(HashSet<string> values,Func<PlayerProfile,IEnumerable<string>> ids,Func<PlayerProfile,IEnumerable<string>> oldIds)
            {
                var finalIds=new HashSet<string>(ids(final)??Enumerable.Empty<string>(),StringComparer.Ordinal);
                var originalIds=new HashSet<string>(oldIds(baseline)??Enumerable.Empty<string>(),StringComparer.Ordinal);
                values.RemoveWhere(id=>!finalIds.Contains(id)&&!originalIds.Contains(id));
            }
            Prune(writes.Parts,p=>p.parts?.Where(v=>v?.instanceId!=null).Select(v=>v.instanceId),p=>p.parts?.Where(v=>v?.instanceId!=null).Select(v=>v.instanceId));
            Prune(writes.Engines,p=>p.engines?.Where(v=>v?.instanceId!=null).Select(v=>v.instanceId),p=>p.engines?.Where(v=>v?.instanceId!=null).Select(v=>v.instanceId));
            Prune(writes.Units,p=>p.looseEngineElements?.Where(v=>v?.element?.id!=null).Select(v=>v.element.id),p=>p.looseEngineElements?.Where(v=>v?.element?.id!=null).Select(v=>v.element.id));
            Prune(writes.Dealer,p=>p.inventory?.dealer?.Where(v=>v?.Id!=null).Select(v=>v.Id),p=>p.inventory?.dealer?.Where(v=>v?.Id!=null).Select(v=>v.Id));
        }

        static OrderedCartApplyResult C1Evaluate(PlayerProfile owner, string carId, IEnumerable<PartCartLine> source, bool preview,
            ValuationPolicy policy, Func<int, string> symbol, out bool disposed, out bool children, out int tokens,
            out RuntimeEntropy.IdentityObservation observation)
        {
            bool wasDisposed = false, wereChildrenDisposed = false; int remainingTokens = -1;
            OrderedCartApplyResult result;
            using (observation = RuntimeEntropy.BeginIdentityObservation(256))
            {
                IReadOnlyList<PartCartLine> rows = source == null ? null : source as IReadOnlyList<PartCartLine> ?? new OrderedRowsEnumerableAdapter(source);
                long jsonBefore = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
                using (RuntimeDataEncodingProbe.Measure("ordered-cart-apply-candidate"))
                    result = OrderedCartApplyProjection.EvaluateForTests(owner, carId, rows, policy, preview, symbol,
                        scope => { wasDisposed = scope.IsDisposed; wereChildrenDisposed = scope.ChildrenDisposed; remainingTokens = scope.ActiveTokenCount; });
                c1CandidateJsonDelta = RuntimeDataEncodingProbe.CurrentThreadJsonCallCount - jsonBefore;
            }
            disposed = wasDisposed; children = wereChildrenDisposed; tokens = remainingTokens;
            return result;
        }

        sealed class OrderedRowsEnumerableAdapter : IReadOnlyList<PartCartLine>
        {
            readonly IEnumerable<PartCartLine> source;
            internal OrderedRowsEnumerableAdapter(IEnumerable<PartCartLine> source) { this.source = source; }
            public int Count => throw new InvalidOperationException("Candidate must not read Count.");
            public PartCartLine this[int index] => throw new InvalidOperationException("Candidate must not index rows.");
            public IEnumerator<PartCartLine> GetEnumerator() => source.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        static Exception C1Frozen(PlayerProfile profile, string carId, IEnumerable<PartCartLine> rows, bool preview,
            ValuationPolicy policy, out RuntimeEntropy.IdentityObservation observation)
        {
            Exception error = null;
            using (observation = RuntimeEntropy.BeginIdentityObservation(256))
                try { LegacyC1CartApplyReference.Apply(profile, carId, rows, preview, policy); }
                catch (Exception caught) { error = caught; }
            return error;
        }

        static void C1Failure(Trace trace, string label, OrderedCartApplyResult result, Exception frozen, int expectedIndex)
        {
            bool suppressed = result.Effects == null && result.Parts == null && result.Engines == null && result.Units == null && result.Dealer == null && result.Cars == null && result.Owners == null && result.SupplyDeltas == null && result.Identities == null && !result.HasCashDelta && result.CashBefore == null && result.CashAfter == null && result.CashDelta == null;
            trace.Assert(!result.Succeeded && result.ErrorType == frozen.GetType().FullName && result.ErrorMessage == frozen.Message && result.ErrorParameterName == (frozen as ArgumentException)?.ParamName && result.ErrorIndex == expectedIndex, "failure captures exact type/message/param/index / " + label);
            trace.Assert(suppressed, "failure exposes no partial effects, snapshots, virtual map, supply or cash / " + label);
        }

        static void C1CheckRawLooseTire(Trace trace, OrderedCartApplyResult result, string definitionId)
        {
            var roots = result.Parts.Where(part => part != null && !part.Deleted && part.DefinitionId == definitionId && part.Identity.IsVirtual).ToArray();
            bool shape = roots.Length == 1 && roots[0].Condition != null && roots[0].Condition.Elements.Length == 0 && roots[0].Condition.Tires.Length == 0;
            trace.Assert(shape, "raw loose NewSet tire root preserves empty Elements and derived Tires arrays; no eager member healing");
        }

        static void C1CheckRawInstalledTire(Trace trace, OrderedCartApplyResult result, string definitionId)
        {
            var roots = result.Parts.Where(part => part != null && !part.Deleted && part.DefinitionId == definitionId && part.Identity.IsVirtual && part.Condition?.Elements?.Length == 4).ToArray();
            bool shape = roots.Length == 1;
            if (shape)
            {
                var elements = roots[0].Condition.Elements;
                var tires = roots[0].Condition.Tires;
                shape = tires.Length == 4 && elements.Select(item => TypedCanonical(item, new Dictionary<string, string>()))
                    .SequenceEqual(tires.Select(item => TypedCanonical(item, new Dictionary<string, string>())), StringComparer.Ordinal) &&
                    elements.All(item => item != null && item.TypeName == typeof(TireCondition).FullName) &&
                    elements.Count(item => item != null && !item.Id.IsVirtual && string.IsNullOrEmpty(item.Id.ExistingId)) == 3 &&
                    elements.Count(item => item != null && item.Id.IsVirtual) == 1;
            }
            trace.Assert(shape, "raw installed NewSet/NewUnits tire preserves concrete subtype, one installed ID and three untouched blank IDs");
        }

        static void C1CheckTireQuantityTwo(Trace trace, OrderedCartApplyResult result, string definitionId, bool repeatedCorner)
        {
            string[] expected = repeatedCorner
                ? new[] { "0:0:part:part", "1:0:element:unit", "1:0:tire:displaced/tire/0", "1:1:element:unit" }
                : new[] { "0:0:part:part", "1:0:element:unit", "1:0:tire:displaced/tire/0", "1:1:element:unit", "1:1:tire:displaced/tire/1" };
            string[] actual = (result.Identities ?? Array.Empty<CartApplyIdentity>())
                .Select(identity => identity.RowIndex.ToString(CultureInfo.InvariantCulture) + ":" +
                    identity.QuantityOrdinal.ToString(CultureInfo.InvariantCulture) + ":" + identity.Kind + ":" + identity.MemberPath)
                .ToArray();
            bool ordered = expected.SequenceEqual(actual, StringComparer.Ordinal);
            var installed = result.Parts?.FirstOrDefault(part => part != null && !part.Deleted && part.DefinitionId == definitionId && part.Identity.IsVirtual);
            var tires = installed?.Condition?.Tires ?? Array.Empty<CartApplyElementSnapshot>();
            int populated = tires.Count(item => item != null);
            int virtualTires = tires.Count(item => item != null && item.Id.IsVirtual);
            int blankExisting = tires.Count(item => item != null && !item.Id.IsVirtual && string.IsNullOrEmpty(item.Id.ExistingId));
            int regularExisting = tires.Count(item => item != null && !item.Id.IsVirtual && !string.IsNullOrEmpty(item.Id.ExistingId));
            trace.Assert(ordered && populated == 4 && virtualTires == (repeatedCorner ? 1 : 2) &&
                blankExisting == (repeatedCorner ? 3 : 2) && regularExisting == 0,
                "q2 fresh tire replacement identities follow actual element/tire emission chronology and retain four installed members; repeated=" + repeatedCorner + ";actual=" + string.Join("|", actual));
        }

        static void C1CheckInstalledSubtype(Trace trace, OrderedCartApplyResult result, string definitionId, string role, PartCategory category,bool expectVirtual,string existingId)
        {
            string expectedType = category == PartCategory.Wheels ? typeof(WheelElement).FullName : typeof(TireCondition).FullName;
            var members = result.Parts.Where(part => part != null && !part.Deleted && part.DefinitionId == definitionId)
                .SelectMany(part => part.Condition?.Elements ?? Array.Empty<CartApplyElementSnapshot>())
                .Where(element => element != null && element.Role == role).ToArray();
            trace.Assert(members.Any(element => element.TypeName == expectedType && element.Id.IsVirtual==expectVirtual&&
                (expectVirtual?element.Id.ExistingId==null:element.Id.ExistingId==existingId)),
                "installed " + category + " member keeps its actual authored subtype, role and " + (expectVirtual?"virtual token":"existing dealer identity"));
        }

        static void C1Success(Trace trace, string label, C0Fixture fixture, OrderedCartApplyResult result, PartCartLine[] rows)
        {
            bool shape = result.Succeeded && result.ErrorType == null && result.ErrorMessage == null && result.ErrorParameterName == null && result.ErrorIndex == -1 &&
                result.Effects != null && result.Parts != null && result.Engines != null && result.Units != null && result.Dealer != null && result.Cars != null && result.Owners != null && result.SupplyDeltas != null && result.Identities != null;
            trace.Assert(shape, "successful result owns every documented collection and omits errors / " + label);
            if (!shape) return;
            int expectedEffects = rows.Sum(row => row == null ? 0 : row.action == CartAction.Move || row.action == CartAction.SellPart || row.action == CartAction.SellUnit ? 1 : Math.Max(0, row.quantity));
            bool ordered = expectedEffects == result.Effects.Length; int effectIndex = 0;
            foreach (var pair in rows.Select((row, index) => new { row, index }))
            {
                var row = pair.row; if (row == null) { ordered = false; break; }
                int count = row.action == CartAction.Move || row.action == CartAction.SellPart || row.action == CartAction.SellUnit ? 1 : Math.Max(0, row.quantity);
                for (int ordinal = 0; ordinal < count && effectIndex < result.Effects.Length; ordinal++)
                {
                    var effect = result.Effects[effectIndex++];
                    var branch = row.action == CartAction.Move ? CartApplyBranch.Move : row.action == CartAction.SellPart || row.action == CartAction.SellUnit ? CartApplyBranch.SkippedSale : row.action == CartAction.NewSet ? CartApplyBranch.NewSet : row.action == CartAction.NewUnits ? CartApplyBranch.NewUnits : CartApplyBranch.BuyUsed;
                    int q = branch == CartApplyBranch.Move || branch == CartApplyBranch.SkippedSale ? -1 : ordinal;
                    ordered &= effect.RowIndex == pair.index && effect.QuantityOrdinal == q && effect.RequestedAction == row.action && effect.Branch == branch;
                    if (branch == CartApplyBranch.Move || branch == CartApplyBranch.SkippedSale) ordered &= (!effect.Identity.HasValue || !effect.Identity.Value.IsVirtual) && !effect.PaidAmount.HasValue && effect.Quote == null && effect.SupplyDeltas.Length == 0;
                    if (branch == CartApplyBranch.Move || branch == CartApplyBranch.BuyUsed) ordered &= effect.AssetId == row.assetId;
                    if (branch == CartApplyBranch.SkippedSale) ordered &= !effect.Identity.HasValue && effect.AssetId == null && effect.DefinitionId == null && effect.InstallTargetId == null;
                    if (branch == CartApplyBranch.NewSet || branch == CartApplyBranch.NewUnits)
                    {
                        bool shouldInstall = row.install && (branch == CartApplyBranch.NewSet ? ordinal == 0 : row.positions != null && ordinal < row.positions.Count);
                        ordered &= effect.DefinitionId == row.definitionId && effect.AssetId == null && effect.Identity.HasValue && effect.Identity.Value.IsVirtual &&
                            effect.PaidAmount.HasValue && effect.Quote != null && effect.PaidAmount.Value == effect.Quote.Cost &&
                            effect.InstallTargetId == (shouldInstall ? fixture.CarId : null);
                    }
                    if (branch == CartApplyBranch.BuyUsed)
                    {
                        var usedStock = fixture.Baseline.inventory.dealer.FirstOrDefault(stock => stock != null && stock.Id == row.assetId);
                        bool shouldInstall = row.install && (usedStock?.IsPart == true || row.positions != null && ordinal < row.positions.Count);
                        var legacyPrice=typeof(LegacyC1CartApplyReference).GetMethod("LegacyBuyUsed",BindingFlags.NonPublic|BindingFlags.Static);
                        int expectedPaid = usedStock == null || legacyPrice==null ? int.MinValue : (int)legacyPrice.Invoke(null,new object[]{usedStock,ValuationPolicy.Defaults()});
                        ordered &= effect.Identity.HasValue && !effect.Identity.Value.IsVirtual && effect.Identity.Value.ExistingId == row.assetId &&
                            effect.DefinitionId == usedStock?.DefinitionId && effect.PaidAmount == expectedPaid && effect.Quote == null &&
                            effect.InstallTargetId == (shouldInstall ? fixture.CarId : null);
                    }
                    if (branch == CartApplyBranch.Move)
                        ordered &= effect.InstallTargetId == row.destinationId && !effect.Identity.HasValue && effect.DefinitionId == null &&
                            !effect.PaidAmount.HasValue && effect.Quote == null && effect.SupplyDeltas.Length == 0;
                }
            }
            trace.Assert(ordered, "effects preserve raw row/quantity order and Move/sale metadata branches / " + label);
            bool positiveAcquisition = rows.Any(row => row != null && row.quantity > 0 && row.action != CartAction.Move && row.action != CartAction.SellPart && row.action != CartAction.SellUnit);
            trace.Assert(result.HasCashDelta == positiveAcquisition && (positiveAcquisition ? result.CashBefore.HasValue && result.CashAfter.HasValue && result.CashDelta == result.CashAfter - result.CashBefore : result.CashBefore == null && result.CashAfter == null && result.CashDelta == null), "cash-delta optionality and arithmetic are branch-derived / " + label);
            if (positiveAcquisition)
                trace.Assert(result.CashBefore == fixture.Baseline.cash && result.CashAfter == fixture.Frozen.cash && result.CashDelta == fixture.Frozen.cash - fixture.Baseline.cash,
                    "cash before/after and signed After-minus-Before delta match the frozen final disposable profile / " + label);
            foreach(var pair in rows.Select((row,index)=>new{row,index}))
            {
                if(pair.row?.action!=CartAction.NewUnits||!pair.row.install||pair.row.positions==null)continue;
                for(int ordinal=0;ordinal<Math.Min(Math.Max(0,pair.row.quantity),pair.row.positions.Count);ordinal++)
                {
                    var root=result.Identities.FirstOrDefault(item=>item.RowIndex==pair.index&&item.QuantityOrdinal==ordinal&&item.Kind=="unit");
                    if(root==null)continue;
                    bool transientAbsent=!(result.Units??Array.Empty<CartApplyUnitSnapshot>()).Any(unit=>unit!=null&&unit.Identity.IsVirtual&&unit.Identity.TokenOrdinal==root.TokenOrdinal);
                    bool memberRetained=(result.Parts??Array.Empty<CartApplyPartSnapshot>()).Any(part=>!part.Deleted&&part.Condition?.Elements?.Any(element=>element?.Id.IsVirtual==true&&element.Id.TokenOrdinal==root.TokenOrdinal)==true)||
                        (result.Engines??Array.Empty<CartApplyEngineSnapshot>()).Any(engine=>!engine.Deleted&&engine.Slots?.Any(slot=>slot!=null&&slot.PartId.IsVirtual&&slot.PartId.TokenOrdinal==root.TokenOrdinal)==true);
                    trace.Assert(transientAbsent&&memberRetained,"installed NewUnits collapses its transient loose-unit record while retaining the installed member token / "+label+"/row="+pair.index+"/q="+ordinal);
                }
            }
            bool tokens = result.Identities.Select(item => item.TokenOrdinal).Distinct().Count() == result.Identities.Length && result.Identities.All(item => item.TokenOrdinal >= 0 && item.RowIndex >= 0 && item.QuantityOrdinal >= 0 && !string.IsNullOrEmpty(item.MemberPath) && !string.IsNullOrEmpty(item.Kind));
            foreach (var effect in result.Effects.Where(item => item.Identity.HasValue && item.Identity.Value.IsVirtual))
                tokens &= result.Identities.Any(item => item.TokenOrdinal == effect.Identity.Value.TokenOrdinal && item.RowIndex == effect.RowIndex && item.QuantityOrdinal == effect.QuantityOrdinal);
            tokens &= C1AllVirtualRefsMapped(result, result.Identities);
            foreach (var part in result.Parts.Where(item => item != null && !item.Deleted && item.Identity.IsVirtual))
                tokens &= part.InstanceId.IsVirtual && part.InstanceId.TokenOrdinal == part.Identity.TokenOrdinal &&
                    (part.Condition?.Elements ?? Array.Empty<CartApplyElementSnapshot>()).All(element => element == null || element.Id.IsVirtual ||
                        string.IsNullOrEmpty(element.Id.ExistingId) || PhysicalIds(fixture.Baseline).Contains(element.Id.ExistingId));
            foreach (var unit in result.Units.Where(item => item != null && !item.Deleted && item.Identity.IsVirtual))
                tokens &= unit.Element != null && unit.Element.Id.IsVirtual && unit.Element.Id.TokenOrdinal == unit.Identity.TokenOrdinal;
            trace.Assert(tokens, "all typed virtual references in effects/snapshots map to token metadata / " + label);
            trace.Assert(C1DtoOwned(result) && C1ArraysDefensive(result), "result DTOs retain no domain objects and array getters are defensive / " + label);
            trace.Assert(fixture.BaselineBefore == SourceSnapshot(fixture.Baseline) && C0SameReferences(fixture.BaselineReferences, C0ProfileReferences(fixture.Baseline)), "independent fixture baseline remains untouched / " + label);
            C1SupplyParity(trace,label,fixture,result);
        }

        static void C1SupplyParity(Trace trace,string label,C0Fixture fixture,OrderedCartApplyResult result)
        {
            var perEffect=(result.Effects??Array.Empty<CartApplyRowEffect>()).SelectMany(effect=>effect.SupplyDeltas??Array.Empty<CartApplySupplyDelta>()).ToArray();
            var total=result.SupplyDeltas??Array.Empty<CartApplySupplyDelta>();
            bool valid=TypedCanonical(perEffect,new Dictionary<string,string>())==TypedCanonical(total,new Dictionary<string,string>());
            var current=new Dictionary<string,int>(StringComparer.Ordinal);var positionsByGroup=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var delta in total)
            {
                if(delta==null){valid=false;continue;}
                var positions=delta.Positions??Array.Empty<int>();if(positions.Length==0){valid=false;continue;}
                string key=(delta.IsHold?"hold":"stock")+":"+string.Join(",",positions.Select(value=>value.ToString(CultureInfo.InvariantCulture)));
                if(positionsByGroup.TryGetValue((delta.IsHold?"hold":"stock")+":"+delta.IdentityGroup.ToString(CultureInfo.InvariantCulture),out string priorPositions))valid&=priorPositions==key;
                else positionsByGroup[(delta.IsHold?"hold":"stock")+":"+delta.IdentityGroup.ToString(CultureInfo.InvariantCulture)]=key;
                if(!current.TryGetValue(key,out int expectedBefore))
                {
                    int? initial=delta.IsHold?
                        (fixture.Baseline.inventory?.cart?.supply!=null&&positions[0]>=0&&positions[0]<fixture.Baseline.inventory.cart.supply.Count?fixture.Baseline.inventory.cart.supply[positions[0]]?.units:null):
                        (fixture.Baseline.inventory?.market?.parts!=null&&positions[0]>=0&&positions[0]<fixture.Baseline.inventory.market.parts.Count?fixture.Baseline.inventory.market.parts[positions[0]]?.units:null);
                    if(!initial.HasValue){valid=false;continue;}expectedBefore=initial.Value;
                }
                valid&=delta.Before==expectedBefore;
                current[key]=delta.After;
            }
            foreach(var entry in current)
            {
                var split=entry.Key.Split(':');bool hold=split[0]=="hold";
                var positions=split.Length<2?Array.Empty<int>():split[1].Split(',').Select(value=>int.Parse(value,CultureInfo.InvariantCulture)).ToArray();
                foreach(int position in positions)
                {
                    int? final=hold?
                        (fixture.Frozen.inventory?.cart?.supply!=null&&position>=0&&position<fixture.Frozen.inventory.cart.supply.Count?fixture.Frozen.inventory.cart.supply[position]?.units:null):
                        (fixture.Frozen.inventory?.market?.parts!=null&&position>=0&&position<fixture.Frozen.inventory.market.parts.Count?fixture.Frozen.inventory.market.parts[position]?.units:null);
                    valid&=final.HasValue&&final.Value==entry.Value;
                }
            }
            trace.Assert(valid,"typed per-effect and aggregate stock/hold deltas match independently observed frozen initial-to-final rows / "+label+";deltas="+total.Length);
        }

        static List<IdentityExpectedEmission> C1RawApplyIdentityExpectedEmissions(PlayerProfile source,string carId,PartCartLine[] rows)
        {
            var expected=new List<IdentityExpectedEmission>();
            var car=source?.cars?.FirstOrDefault(value=>value?.instanceId==carId);
            var tirePackId=car?.installed?.slots?.FirstOrDefault(value=>value?.slot==PartCategory.Tires.ToString())?.partInstanceId;
            var tirePack=source?.parts?.FirstOrDefault(value=>value?.instanceId==tirePackId);
            var blankCorners=new HashSet<int>();
            var currentTires=tirePack?.condition?.elements;
            if(currentTires==null||currentTires.Length==0)
                for(int corner=0;corner<4;corner++)blankCorners.Add(corner);
            else for(int corner=0;corner<currentTires.Length;corner++)
                if(currentTires[corner] is TireCondition tire&&string.IsNullOrEmpty(tire.id))blankCorners.Add(corner);

            for(int rowIndex=0;rowIndex<rows.Length;rowIndex++)
            {
                var row=rows[rowIndex];if(row==null)continue;
                int quantityCount=row.action==CartAction.Move||row.action==CartAction.SellPart||row.action==CartAction.SellUnit?1:Math.Max(0,row.quantity);
                var definition=source?.catalog?.Find(row.definitionId);
                for(int ordinal=0;ordinal<quantityCount;ordinal++)
                {
                    if(row.action==CartAction.NewSet)
                    {
                        expected.Add(new IdentityExpectedEmission{Key=IdentityKey(rowIndex,row.action,ordinal,"part-root",null,-1,null),Scope="part"});
                        if(row.install&&ordinal==0&&definition?.category==PartCategory.Tires)
                        {blankCorners.Clear();for(int corner=0;corner<4;corner++)blankCorners.Add(corner);}
                    }
                    else if(row.action==CartAction.NewUnits)
                    {
                        var positions=IdentityEffectivePositions(row);string position=ordinal<positions.Count?positions[ordinal]:null;
                        string kind=position==null?"loose-unit":"engine-member";
                        expected.Add(new IdentityExpectedEmission{Key=IdentityKey(rowIndex,row.action,ordinal,kind,row.role,ordinal,position),Scope="element"});
                        if(row.install&&position!=null&&definition?.category==PartCategory.Tires&&position.StartsWith("tire:",StringComparison.Ordinal)&&
                            int.TryParse(position.Substring(5),NumberStyles.None,CultureInfo.InvariantCulture,out int corner)&&blankCorners.Remove(corner))
                        {
                            expected.Add(new IdentityExpectedEmission{Key=IdentityKey(rowIndex,row.action,ordinal,"displaced-tire","tire",corner,position),Scope="tire"});
                        }
                    }
                }
            }
            return expected;
        }

        static void C1SnapshotParity(Trace trace,string label,C0Fixture fixture,OrderedCartApplyResult result,PartCartLine[] rows,RuntimeEntropy.IdentityObservation observation,C1ExpectedWriteSet expectedWrites=null)
        {
            var legacyIds=new Dictionary<string,string>(StringComparer.Ordinal);
            var emissions=C1RawApplyIdentityExpectedEmissions(fixture.Baseline,fixture.CarId,rows);
            bool mapped=observation!=null&&observation.Closed&&!observation.Overflowed&&observation.Count==emissions.Count;
            if(observation!=null)for(int i=0;i<Math.Min(observation.Count,emissions.Count);i++)
            {var item=observation.EventAt(i);mapped&=item.Scope==emissions[i].Scope;legacyIds[emissions[i].Key]=item.Scope+"-"+item.Value.ToString("N");}
            foreach(var pair in rows.Select((line,index)=>new{line,index}))
            {
                var line=pair.line;if(line?.action!=CartAction.NewSet)continue;
                string rootKey=IdentityKey(pair.index,line.action,0,"part-root",null,-1,null);
                for(int quantity=0;quantity<line.quantity;quantity++)
                {
                    rootKey=IdentityKey(pair.index,line.action,quantity,"part-root",null,-1,null);
                    if(!legacyIds.TryGetValue(rootKey,out string rootId)){mapped=false;continue;}
                    var definition=fixture.Baseline.catalog.Find(line.definitionId);
                    if(definition==null||definition.category==PartCategory.Tires)continue;
                    var car=fixture.Baseline.cars.Find(value=>value?.instanceId==fixture.CarId);
                    var template=InventoryUnits.Template(fixture.Baseline,car,definition);
                    var elements=template?.condition?.elements??Array.Empty<EngineElement>();
                    for(int ordinal=0;ordinal<elements.Length;ordinal++)
                    {
                        var key=IdentityMemberKey(pair.index,line,quantity,definition.category,elements[ordinal]?.role??"<null>",ordinal);
                        if(!legacyIds.ContainsKey(key))legacyIds.Add(key,rootId+"/element/"+ordinal.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            var tokenToLegacyId=new Dictionary<int,string>();
            foreach(var identity in result.Identities??Array.Empty<CartApplyIdentity>())
            {
                string key=C1IdentitySemanticKey(fixture.Baseline,fixture.CarId,rows,identity);
                if(key==null||!legacyIds.TryGetValue(key,out string id)){mapped=false;continue;}
                if(tokenToLegacyId.ContainsKey(identity.TokenOrdinal)){mapped=false;continue;}
                tokenToLegacyId.Add(identity.TokenOrdinal,id);
            }
            var legacyIdToToken=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var item in tokenToLegacyId)if(!legacyIdToToken.TryAdd(item.Value,item.Key))mapped=false;
            var canonicalAliases=legacyIdToToken.ToDictionary(item=>item.Key,item=>"<candidate-token:"+item.Value.ToString(CultureInfo.InvariantCulture)+">",StringComparer.Ordinal);

            var objectOrdinals=new Dictionary<object,int>(new C1ReferenceComparer());int nextOrdinal=0;
            C1CurrentOrdinals=objectOrdinals;
            bool parity=mapped;
            foreach(var candidate in result.Parts??Array.Empty<CartApplyPartSnapshot>())
            {
                string id=C1ResolvedId(candidate.Identity,tokenToLegacyId);
                var source=candidate.Deleted?null:fixture.Frozen.parts?.FirstOrDefault(value=>value?.instanceId==id);
                if(!candidate.Deleted&&source==null){parity=false;continue;}
                if(candidate.Deleted)
                {var original=fixture.Baseline.parts?.FirstOrDefault(value=>value?.instanceId==id);parity&=original!=null&&C1DtoValueEquals(candidate.Identity,original.instanceId,legacyIdToToken,objectOrdinals,out _)&&C1DeletedBodyEmpty(candidate);}
                C1RegisterSnapshotOrdinals(source?.condition,objectOrdinals,ref nextOrdinal);
                parity&=C1DtoMatchesSource(candidate,source,legacyIdToToken,objectOrdinals,out _);
                int sourceOrdinal=fixture.Baseline.parts?.FindIndex(value=>value?.instanceId==id)??-1;
                parity&=candidate.SourceOrdinal==sourceOrdinal&&candidate.InsertionOrdinal==(sourceOrdinal<0&&!candidate.Deleted?C1InsertionOrdinal(result.Parts,candidate.Identity,tokenToLegacyId):-1);
            }
            foreach(var candidate in result.Engines??Array.Empty<CartApplyEngineSnapshot>())
            {
                string id=C1ResolvedId(candidate.Identity,tokenToLegacyId);
                var source=candidate.Deleted?null:fixture.Frozen.engines?.FirstOrDefault(value=>value?.instanceId==id);
                if(!candidate.Deleted&&source==null){parity=false;continue;}
                if(candidate.Deleted)
                {var original=fixture.Baseline.engines?.FirstOrDefault(value=>value?.instanceId==id);parity&=original!=null&&C1DtoValueEquals(candidate.Identity,original.instanceId,legacyIdToToken,objectOrdinals,out _)&&C1DeletedBodyEmpty(candidate);}
                parity&=C1DtoMatchesSource(candidate,source,legacyIdToToken,objectOrdinals,out _);
                int sourceOrdinal=fixture.Baseline.engines?.FindIndex(value=>value?.instanceId==id)??-1;
                parity&=candidate.SourceOrdinal==sourceOrdinal&&candidate.InsertionOrdinal==-1;
            }
            foreach(var candidate in result.Units??Array.Empty<CartApplyUnitSnapshot>())
            {
                string id=C1ResolvedId(candidate.Identity,tokenToLegacyId);
                var source=candidate.Deleted?null:fixture.Frozen.looseEngineElements?.FirstOrDefault(value=>value?.element?.id==id);
                if(!candidate.Deleted&&source==null){parity=false;continue;}
                if(candidate.Deleted)
                {
                    var original=fixture.Baseline.looseEngineElements?.FirstOrDefault(value=>value?.element?.id==id);
                    var dealerUnit=fixture.Baseline.inventory?.dealer?.FirstOrDefault(value=>value?.unit?.element?.id==id)?.unit;
                    bool installedMember=fixture.Frozen.parts?.Any(part=>part?.condition?.elements?.Any(element=>element?.id==id)==true)==true;
                    parity&=C1DtoValueEquals(candidate.Identity,original?.element?.id??dealerUnit?.element?.id??(installedMember?id:null),legacyIdToToken,objectOrdinals,out _)&&
                        (original!=null||dealerUnit!=null||installedMember)&&C1DeletedBodyEmpty(candidate);
                }
                C1RegisterElementOrdinal(source?.element,objectOrdinals,ref nextOrdinal);
                parity&=C1DtoMatchesSource(candidate,source,legacyIdToToken,objectOrdinals,out _);
                int sourceOrdinal=fixture.Baseline.looseEngineElements?.FindIndex(value=>value?.element?.id==id)??-1;
                parity&=candidate.SourceOrdinal==sourceOrdinal&&candidate.InsertionOrdinal==(sourceOrdinal<0&&!candidate.Deleted?C1InsertionOrdinal(result.Units,candidate.Identity,tokenToLegacyId):-1);
            }
            foreach(var candidate in result.Dealer??Array.Empty<CartApplyDealerSnapshot>())
            {
                string id=C1ResolvedId(candidate.Identity,tokenToLegacyId);
                var source=fixture.Baseline.inventory?.dealer?.FirstOrDefault(value=>value?.Id==id);
                parity&=candidate.Deleted&&source!=null&&C1DtoValueEquals(candidate.Identity,source.Id,legacyIdToToken,objectOrdinals,out _)&&C1DeletedBodyEmpty(candidate);
                int ordinal=fixture.Baseline.inventory?.dealer?.FindIndex(value=>value?.Id==id)??-1;
                parity&=candidate.SourceOrdinal==ordinal&&candidate.InsertionOrdinal==-1;
            }
            foreach(var candidate in result.Cars??Array.Empty<CartApplyCarSnapshot>())
            {
                string id=C1ResolvedId(candidate.Identity,tokenToLegacyId);
                var source=fixture.Frozen.cars?.FirstOrDefault(value=>value?.instanceId==id);
                int sourceOrdinal=fixture.Baseline.cars?.FindIndex(value=>value?.instanceId==id)??-1;
                parity&=source!=null&&C1DtoMatchesSource(candidate,source,legacyIdToToken,objectOrdinals,out _)&&candidate.SourceOrdinal==sourceOrdinal&&candidate.InsertionOrdinal==-1;
            }
            foreach(var candidate in result.Owners??Array.Empty<CartApplyOwnerSnapshot>())
            {
                string id=C1ResolvedId(candidate.Identity,tokenToLegacyId);
                object source=candidate.Kind=="car"?(object)fixture.Frozen.cars?.FirstOrDefault(value=>value?.instanceId==id):fixture.Frozen.engines?.FirstOrDefault(value=>value?.instanceId==id);
                int sourceOrdinal=candidate.Kind=="car"?(fixture.Baseline.cars?.FindIndex(value=>value?.instanceId==id)??-1):(fixture.Baseline.engines?.FindIndex(value=>value?.instanceId==id)??-1);
                parity&=source!=null&&C1DtoMatchesSource(candidate,source,legacyIdToToken,objectOrdinals,out _)&&candidate.SourceOrdinal==sourceOrdinal&&candidate.InsertionOrdinal==-1;
            }
            parity&=C1ChangedRecordsPresent(fixture.Baseline.parts,fixture.Frozen.parts,result.Parts?.Cast<CartApplyRecordSnapshot>(),value=>value?.instanceId,canonicalAliases,tokenToLegacyId);
            parity&=C1ChangedRecordsPresent(fixture.Baseline.engines,fixture.Frozen.engines,result.Engines?.Cast<CartApplyRecordSnapshot>(),value=>value?.instanceId,canonicalAliases,tokenToLegacyId);
            parity&=C1ChangedRecordsPresent(fixture.Baseline.looseEngineElements,fixture.Frozen.looseEngineElements,result.Units?.Cast<CartApplyRecordSnapshot>(),value=>value?.element?.id,canonicalAliases,tokenToLegacyId);
            parity&=C1ChangedRecordsPresent(fixture.Baseline.inventory?.dealer,fixture.Frozen.inventory?.dealer,result.Dealer?.Cast<CartApplyRecordSnapshot>(),value=>value?.Id,canonicalAliases,tokenToLegacyId);
            parity&=C1ChangedRecordsPresent(fixture.Baseline.cars,fixture.Frozen.cars,result.Cars?.Cast<CartApplyRecordSnapshot>(),value=>value?.instanceId,canonicalAliases,tokenToLegacyId);
            parity&=C1ChangedOwnersPresent(fixture.Baseline,fixture.Frozen,result.Owners,canonicalAliases);
            if(expectedWrites!=null)
            {
                var actualParts=(result.Parts??Array.Empty<CartApplyPartSnapshot>()).Select(value=>C1ResolvedId(value.Identity,tokenToLegacyId));
                var actualEngines=(result.Engines??Array.Empty<CartApplyEngineSnapshot>()).Select(value=>C1ResolvedId(value.Identity,tokenToLegacyId));
                var actualUnits=(result.Units??Array.Empty<CartApplyUnitSnapshot>()).Select(value=>C1ResolvedId(value.Identity,tokenToLegacyId));
                var actualDealer=(result.Dealer??Array.Empty<CartApplyDealerSnapshot>()).Select(value=>C1ResolvedId(value.Identity,tokenToLegacyId));
                var actualCars=(result.Cars??Array.Empty<CartApplyCarSnapshot>()).Select(value=>C1ResolvedId(value.Identity,tokenToLegacyId));
                var actualOwners=(result.Owners??Array.Empty<CartApplyOwnerSnapshot>()).Select(value=>value.Kind+":"+C1ResolvedId(value.Identity,tokenToLegacyId));
                bool exact=C1WriteSetMatches(expectedWrites.Parts,actualParts,expectedWrites.ReplayToWhole)&&C1WriteSetMatches(expectedWrites.Engines,actualEngines,expectedWrites.ReplayToWhole)&&
                    C1WriteSetMatches(expectedWrites.Units,actualUnits,expectedWrites.ReplayToWhole)&&C1WriteSetMatches(expectedWrites.Dealer,actualDealer,expectedWrites.ReplayToWhole)&&
                    C1WriteSetMatches(expectedWrites.Cars,actualCars,expectedWrites.ReplayToWhole)&&C1WriteSetMatches(expectedWrites.Owners,actualOwners,expectedWrites.ReplayToWhole);
                string unrelated=fixture.Baseline.parts?.Select(value=>value?.instanceId).FirstOrDefault(id=>id!=null&&!expectedWrites.Parts.Contains(id));
                var unrelatedSource=unrelated==null?null:fixture.Baseline.parts?.FirstOrDefault(value=>value?.instanceId==unrelated);
                var unrelatedSnapshot=unrelatedSource==null?null:new CartApplyPartSnapshot(CartApplyIdentityRef.Existing(unrelated),false,
                    fixture.Baseline.parts.FindIndex(value=>value?.instanceId==unrelated),-1,unrelatedSource,id=>CartApplyIdentityRef.Existing(id),new CartApplyCaptureContext());
                bool negative=unrelatedSnapshot!=null&&!C1WriteSetMatches(expectedWrites.Parts,actualParts.Concat(new[]{unrelatedSnapshot.Identity.ExistingId}),expectedWrites.ReplayToWhole);
                trace.Assert(exact,"typed snapshot identity sets exactly equal independent per-row frozen write chronology / "+label+";parts="+expectedWrites.Parts.Count+"/"+(result.Parts?.Length??-1)+";engines="+expectedWrites.Engines.Count+"/"+(result.Engines?.Length??-1)+";units="+expectedWrites.Units.Count+"/"+(result.Units?.Length??-1)+";dealer="+expectedWrites.Dealer.Count+"/"+(result.Dealer?.Length??-1)+";owners="+expectedWrites.Owners.Count+"/"+(result.Owners?.Length??-1));
                trace.Assert(negative,"write-set comparator rejects a valid but unrelated unchanged snapshot identity / "+label);
            }
            trace.Assert(mapped,"typed snapshot comparator maps every candidate identity path to a distinct frozen legacy physical ID / "+label+";tokens="+tokenToLegacyId.Count+";frozenEvents="+(observation?.Count??-1));
            trace.Assert(parity,"all changed/deleted typed snapshots match frozen final records field-for-field, including nested values, ID provenance, aliases, source ordinals and completeness / "+label);
            C1CurrentOrdinals=null;
        }

        static bool C1WriteSetMatches(IEnumerable<string> expected,IEnumerable<string> actual,Dictionary<string,string> replayToWhole)
        {
            var normalized=(expected??Enumerable.Empty<string>()).Select(id=>replayToWhole!=null&&replayToWhole.TryGetValue(id,out string whole)?whole:id).ToHashSet(StringComparer.Ordinal);
            var observed=(actual??Enumerable.Empty<string>()).ToArray();
            return observed.All(id=>id!=null)&&observed.Length==observed.Distinct(StringComparer.Ordinal).Count()&&normalized.SetEquals(observed);
        }

        static bool C1ChangedRecordsPresent<T>(IEnumerable<T> before,IEnumerable<T> after,IEnumerable<CartApplyRecordSnapshot> snapshots,
            Func<T,string> identity,Dictionary<string,string> aliases,Dictionary<int,string> tokenToLegacyId) where T:class
        {
            var prior=(before??Enumerable.Empty<T>()).Where(value=>value!=null&&identity(value)!=null).ToDictionary(identity,StringComparer.Ordinal);
            var final=(after??Enumerable.Empty<T>()).Where(value=>value!=null&&identity(value)!=null).ToDictionary(identity,StringComparer.Ordinal);
            var expected=new HashSet<string>(StringComparer.Ordinal);
            foreach(var old in prior)
                if(!final.TryGetValue(old.Key,out var current)||TypedCanonical(old.Value,aliases)!=TypedCanonical(current,aliases))expected.Add(old.Key);
            foreach(var added in final)if(!prior.ContainsKey(added.Key))expected.Add(added.Key);
            var captured=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in snapshots??Enumerable.Empty<CartApplyRecordSnapshot>())
            {string id=row==null?null:row.Identity.IsVirtual?(tokenToLegacyId.TryGetValue(row.Identity.TokenOrdinal,out string mapped)?mapped:null):row.Identity.ExistingId;if(id==null)return false;if(!captured.Add(id))return false;}
            return expected.All(captured.Contains);
        }

        static bool C1ChangedOwnersPresent(PlayerProfile before,PlayerProfile after,CartApplyOwnerSnapshot[] snapshots,Dictionary<string,string> aliases)
        {
            var expected=new HashSet<string>(StringComparer.Ordinal);
            foreach(var old in before?.cars??new List<OwnedCar>())
            {
                var current=after?.cars?.FirstOrDefault(value=>value?.instanceId==old?.instanceId);
                if(old!=null&&current!=null&&TypedCanonical(old.installed?.slots,aliases)!=TypedCanonical(current.installed?.slots,aliases))expected.Add("car:"+old.instanceId);
            }
            foreach(var old in before?.engines??new List<EngineAssemblyInstance>())
            {
                var current=after?.engines?.FirstOrDefault(value=>value?.instanceId==old?.instanceId);
                if(old!=null&&current!=null&&TypedCanonical(old.slots,aliases)!=TypedCanonical(current.slots,aliases))expected.Add("engine:"+old.instanceId);
            }
            var captured=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in snapshots??Array.Empty<CartApplyOwnerSnapshot>())
            {if(row==null||row.Identity.IsVirtual||row.Identity.ExistingId==null||!captured.Add(row.Kind+":"+row.Identity.ExistingId))return false;}
            return expected.All(captured.Contains);
        }

        static string C1IdentitySemanticKey(PlayerProfile source,string carId,PartCartLine[] rows,CartApplyIdentity identity)
        {
            if(identity==null||identity.RowIndex<0||identity.RowIndex>=rows.Length)return null;
            var row=rows[identity.RowIndex];if(row==null)return null;
            if(identity.Kind=="part"&&identity.MemberPath=="part")
                return IdentityKey(identity.RowIndex,row.action,identity.QuantityOrdinal,"part-root",null,-1,null);
            if(identity.Kind=="element"&&identity.MemberPath=="unit"&&row.action==CartAction.NewUnits)
            {
                var positions=IdentityEffectivePositions(row);string position=identity.QuantityOrdinal>=0&&identity.QuantityOrdinal<positions.Count?positions[identity.QuantityOrdinal]:null;
                string kind=position==null?"loose-unit":"engine-member";
                return IdentityKey(identity.RowIndex,row.action,identity.QuantityOrdinal,kind,row.role,identity.QuantityOrdinal,position);
            }
            const string prefix="part/elements/";
            if(identity.Kind=="element"&&identity.MemberPath!=null&&identity.MemberPath.StartsWith(prefix,StringComparison.Ordinal)&&row.action==CartAction.NewSet&&
                int.TryParse(identity.MemberPath.Substring(prefix.Length),NumberStyles.None,CultureInfo.InvariantCulture,out int ordinal))
            {
                var definition=source.catalog.Find(row.definitionId);var car=source.cars.Find(value=>value?.instanceId==carId);
                var role=InventoryUnits.Template(source,car,definition)?.condition?.elements?.ElementAtOrDefault(ordinal)?.role??"<null>";
                return IdentityMemberKey(identity.RowIndex,row,identity.QuantityOrdinal,definition?.category??PartCategory.Body,role,ordinal);
            }
            const string displaced="displaced/tire/";
            if(identity.Kind=="tire"&&identity.MemberPath!=null&&identity.MemberPath.StartsWith(displaced,StringComparison.Ordinal)&&row.action==CartAction.NewUnits&&
                int.TryParse(identity.MemberPath.Substring(displaced.Length),NumberStyles.None,CultureInfo.InvariantCulture,out int displacedIndex))
            {
                var positions=IdentityEffectivePositions(row);string position=identity.QuantityOrdinal>=0&&identity.QuantityOrdinal<positions.Count?positions[identity.QuantityOrdinal]:null;
                return IdentityKey(identity.RowIndex,row.action,identity.QuantityOrdinal,"displaced-tire",row.role,displacedIndex,position);
            }
            return null;
        }

        static string C1ResolvedId(CartApplyIdentityRef identity,Dictionary<int,string> tokenToLegacyId)
            =>identity.IsVirtual&&tokenToLegacyId.TryGetValue(identity.TokenOrdinal,out string id)?id:identity.ExistingId;

        static int C1InsertionOrdinal<T>(T[] values,CartApplyIdentityRef identity,Dictionary<int,string> tokenToLegacyId) where T:class
        {
            string id=C1ResolvedId(identity,tokenToLegacyId);int inserted=0;
            foreach(var value in values)
            {
                var record=value as CartApplyRecordSnapshot;
                var recordIdentity=record?.Identity??default;
                string other=C1ResolvedId(recordIdentity,tokenToLegacyId);
                if(other==id)return inserted;
                if(record!=null&&record.SourceOrdinal==-1&&!record.Deleted)inserted++;
            }
            return -1;
        }

        static bool C1DtoMatchesSource(object dto,object source,Dictionary<string,int> tokenById,Dictionary<object,int> ordinals,out string mismatch)
        {
            mismatch=null;if(dto==null)return source==null;if(source==null)
            {
                if(dto is CartApplyRecordSnapshot deleted&&deleted.Deleted)return true;
                return false;
            }
            var type=dto.GetType();
            foreach(var field in C1DtoFields(type))
            {
                object actual=field.GetValue(dto);object expected=C1SourceMember(type,field.Name,source);
                if(!C1DtoValueEquals(actual,expected,tokenById,ordinals,out mismatch)){mismatch=type.Name+"."+field.Name+(mismatch==null?string.Empty:"/"+mismatch);return false;}
            }
            foreach(var property in C1DtoArrayProperties(type))
            {
                var actual=(IEnumerable)property.GetValue(dto);var expected=C1SourceMember(type,property.Name,source) as IEnumerable;
                if(!C1DtoValueEquals(actual,expected,tokenById,ordinals,out mismatch)){mismatch=type.Name+"."+property.Name+(mismatch==null?string.Empty:"/"+mismatch);return false;}
            }
            return true;
        }

        static bool C1DeletedBodyEmpty(CartApplyRecordSnapshot record)
        {
            if(record==null||!record.Deleted)return false;
            for(var type=record.GetType();type!=null&&type!=typeof(CartApplyRecordSnapshot)&&type!=typeof(object);type=type.BaseType)
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                {
                    if(field.IsStatic)continue;
                    object value=field.GetValue(record);object empty=field.FieldType.IsValueType?Activator.CreateInstance(field.FieldType):null;
                    if(!Equals(value,empty))return false;
                }
            return true;
        }

        static IEnumerable<FieldInfo> C1DtoFields(Type type)
        {
            for(var current=type;current!=null&&current!=typeof(object);current=current.BaseType)
                foreach(var field in current.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                    if(!field.IsStatic&&!field.IsPrivate&&!field.FieldType.IsArray&&field.Name!="Deleted"&&field.Name!="SourceOrdinal"&&field.Name!="InsertionOrdinal")yield return field;
        }

        static IEnumerable<PropertyInfo> C1DtoArrayProperties(Type type)
            =>type.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(property=>property.PropertyType.IsArray&&property.GetIndexParameters().Length==0&&property.GetMethod!=null);

        static object C1SourceMember(Type dtoType,string name,object source)
        {
            if(name=="Identity")return C1PrimaryIdentity(source);
            if(name=="TypeName")return source?.GetType().FullName;
            if(name=="ObjectOrdinal")return source!=null&&C1CurrentOrdinals!=null&&C1CurrentOrdinals.TryGetValue(source,out int ordinal)?ordinal:-1;
            if(dtoType==typeof(CartApplyElementSnapshot))
            {
                var tire=source as TireCondition;
                if(name=="TireFailed")return tire!=null&&tire.failed;
                if(name=="FailureReason")return tire?.failureReason;
                if(name=="TemperatureC")return tire?.temperatureC??0d;
                if(name=="SlipEnergyJ")return tire?.slipEnergyJ??0d;
                if(name=="DistanceM")return tire?.distanceM??0d;
                if(name=="LastContactWorkJ")return tire?.lastContactWorkJ??0d;
                if(name=="ConsumedLife")return tire?.consumedLife??0d;
                if(name=="RunningSeconds")return tire?.runningSeconds??0d;
            }
            if(dtoType==typeof(CartApplyPartSnapshot)&&name=="InstanceId")return ((PartInstance)source).instanceId;
            if(dtoType==typeof(CartApplyBindingSnapshot)&&name=="PartId")return ((SlotBinding)source)?.partInstanceId;
            if(dtoType==typeof(CartApplyEngineRebuildTargetSnapshot)&&name=="PartId")return ((EngineRebuildTarget)source)?.partId;
            if(dtoType==typeof(CartApplyUnitSnapshot)&&name=="Element")return ((LooseEngineElement)source)?.element;
            if(dtoType==typeof(CartApplyDealerSnapshot))
            {if(name=="Part")return ((DealerStock)source)?.part;if(name=="Unit")return ((DealerStock)source)?.unit;}
            if(dtoType==typeof(CartApplyCarSnapshot))
            {
                var car=(OwnedCar)source;
                if(name=="HasInstalledBuild")return car.installed!=null;
                if(name=="InstalledVersion")return car.installed?.version??0;
                if(name=="InstalledIdentity")return car.installed?.instanceId;
                if(name=="InstalledVehicleDefinitionId")return car.installed?.vehicleDefinitionId;
                if(name=="Bindings")return car.installed?.slots??new List<SlotBinding>();
                if(name=="UsedHistory")return car.usedHistory??new List<string>();
                if(name=="Presets")return car.presets??new List<Tune>();
            }
            if(dtoType==typeof(CartApplyEngineSnapshot)&&name=="Slots")return ((EngineAssemblyInstance)source)?.slots??new List<SlotBinding>();
            if(dtoType==typeof(CartApplyEngineRebuildSnapshot)&&name=="Targets")return ((EngineRebuildJob)source)?.targets??new List<EngineRebuildTarget>();
            if(dtoType==typeof(CartApplyOwnerSnapshot)&&name=="PartIds")
            {
                if(source is OwnedCar car)return car.installed?.slots?.Select(value=>value==null?null:value.partInstanceId).ToArray();
                if(source is EngineAssemblyInstance engine)return engine.slots?.Select(value=>value==null?null:value.partInstanceId).ToArray();
            }
            if(dtoType==typeof(CartApplyOwnerSnapshot)&&name=="Kind")return source is OwnedCar?"car":"engine";
            string sourceName=char.ToLowerInvariant(name[0])+name.Substring(1);
            var sourceType=source.GetType();
            var field=sourceType.GetField(sourceName,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.IgnoreCase);
            if(field!=null)return field.GetValue(source);
            var property=sourceType.GetProperty(sourceName,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.IgnoreCase);
            if(property!=null&&property.GetIndexParameters().Length==0)return property.GetValue(source);
            return null;
        }

        static string C1PrimaryIdentity(object source)
        {
            if(source is PartInstance part)return part.instanceId;
            if(source is EngineAssemblyInstance engine)return engine.instanceId;
            if(source is LooseEngineElement unit)return unit.element?.id;
            if(source is DealerStock dealer)return dealer.Id;
            if(source is OwnedCar car)return car.instanceId;
            if(source is EngineElement element)return element.id;
            return null;
        }

        static bool C1DtoValueEquals(object actual,object expected,Dictionary<string,int> tokenById,Dictionary<object,int> ordinals,out string mismatch)
        {
            mismatch=null;
            if(actual is CartApplyIdentityRef identity)
            {
                if(expected==null)return !identity.IsVirtual&&identity.ExistingId==null;
                string id=expected as string;
                if(id==null)return false;
                return tokenById.TryGetValue(id,out int token)?identity.IsVirtual&&identity.TokenOrdinal==token&&identity.ExistingId==null:!identity.IsVirtual&&identity.ExistingId==id;
            }
            if(actual==null)return expected==null;
            if(actual is IEnumerable actualValues&&!(actual is string))
            {
                var expectedValues=expected as IEnumerable;if(expectedValues==null||expected is string)return false;
                var a=actualValues.Cast<object>().ToArray();var b=expectedValues.Cast<object>().ToArray();
                if(a.Length!=b.Length)return false;
                for(int i=0;i<a.Length;i++)if(!C1DtoValueEquals(a[i],b[i],tokenById,ordinals,out mismatch))return false;
                return true;
            }
            Type type=actual.GetType();
            if(type.Namespace!=null&&type.Namespace.StartsWith("DragLab.Domain.Mechanics",StringComparison.Ordinal)&&type.Name.StartsWith("CartApply",StringComparison.Ordinal))
                return C1DtoMatchesSource(actual,expected,tokenById,ordinals,out mismatch);
            return Equals(actual,expected);
        }

        static Dictionary<object,int> C1CurrentOrdinals;

        static void C1RegisterSnapshotOrdinals(ComponentCondition condition,Dictionary<object,int> ordinals,ref int next)
        {
            if(condition==null)return;
            foreach(var element in condition.elements??Array.Empty<EngineElement>())C1RegisterElementOrdinal(element,ordinals,ref next);
            foreach(var element in condition.tires??Array.Empty<TireCondition>())C1RegisterElementOrdinal(element,ordinals,ref next);
        }

        static void C1RegisterElementOrdinal(EngineElement element,Dictionary<object,int> ordinals,ref int next)
        {if(element!=null&&!ordinals.ContainsKey(element))ordinals.Add(element,next++);}

        static bool C1ArraysDefensive(OrderedCartApplyResult result)
            => C1ArraysDefensive(result, new HashSet<object>(new C1ReferenceComparer()));

        static bool C1ArraysDefensive(object value, HashSet<object> seen)
        {
            if (value == null) return true;
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime)) return true;
            if (!type.IsValueType && !seen.Add(value)) return true;
            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable) if (!C1ArraysDefensive(item, seen)) return false;
                return true;
            }
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (property.GetIndexParameters().Length != 0 || property.GetMethod == null) continue;
                if (property.PropertyType.IsArray)
                {
                    var first = property.GetValue(value) as Array; var second = property.GetValue(value) as Array;
                    if (first == null || second == null || ReferenceEquals(first, second)) return false;
                    foreach (object item in first) if (!C1ArraysDefensive(item, seen)) return false;
                }
            }
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (!C1ArraysDefensive(field.GetValue(value), seen)) return false;
            return true;
        }

        static bool C1AllVirtualRefsMapped(object value, CartApplyIdentity[] identities)
        {
            if (value == null) return true;
            if (value is CartApplyIdentityRef reference) return !reference.IsVirtual || identities.Any(item => item.TokenOrdinal == reference.TokenOrdinal);
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime)) return true;
            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable) if (!C1AllVirtualRefsMapped(item, identities)) return false;
                return true;
            }
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).All(field => C1AllVirtualRefsMapped(field.GetValue(value), identities));
        }

        static bool C1NoPrivateExistingIdentity(OrderedCartApplyResult result, string privateSymbol)
            => C1NoPrivateExistingIdentity(result, privateSymbol, new HashSet<object>(new C1ReferenceComparer()));

        static bool C1NoPrivateExistingIdentity(object value, string privateSymbol, HashSet<object> seen)
        {
            if (value == null) return true;
            if (value is CartApplyIdentityRef identity) return identity.IsVirtual || !string.Equals(identity.ExistingId, privateSymbol, StringComparison.Ordinal);
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime)) return true;
            if (!type.IsValueType && !seen.Add(value)) return true;
            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable) if (!C1NoPrivateExistingIdentity(item, privateSymbol, seen)) return false;
                return true;
            }
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).All(field => C1NoPrivateExistingIdentity(field.GetValue(value), privateSymbol, seen));
        }

        static bool C1DtoOwned(object value) => C1DtoOwned(value, new HashSet<object>(new C1ReferenceComparer()));
        static bool C1DtoOwned(object value, HashSet<object> seen)
        {
            if (value == null) return true;
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid) || type == typeof(DateTime)) return true;
            if (type.Namespace != null && type.Namespace.StartsWith("DragLab.Domain", StringComparison.Ordinal) &&
                !type.Name.StartsWith("CartApply", StringComparison.Ordinal) && type.Name != "OrderedCartApplyResult") return false;
            if (type.IsValueType) return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).All(field => C1DtoOwned(field.GetValue(value), seen));
            if (!seen.Add(value)) return true;
            if (value is IEnumerable enumerable) { foreach (object item in enumerable) if (!C1DtoOwned(item, seen)) return false; return true; }
            if (type.Namespace == "System") return false;
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).All(field => C1DtoOwned(field.GetValue(value), seen));
        }
        sealed class C1ReferenceComparer : IEqualityComparer<object>
        { public new bool Equals(object a, object b) => ReferenceEquals(a, b); public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value); }
    }
}
#endif
