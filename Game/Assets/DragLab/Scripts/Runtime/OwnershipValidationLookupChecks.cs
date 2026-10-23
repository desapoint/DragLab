using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DragLab.Runtime
{
#if UNITY_EDITOR
    /// <summary>Independent parity checks and bounded scaling probe for MechanicsOwnership.Validate.</summary>
    public static class OwnershipValidationLookupChecks
    {
        const int WarmupPairs = 3;
        const int Repeats = 20;
        static readonly StringBuilder Checks = new StringBuilder();
        static readonly StringBuilder Timings = new StringBuilder();
        static int passed;
        static VehicleDefinition[] authored;
        static MechanicalCatalog catalog;

        sealed class Outcome
        {
            public string Error, ExceptionType, ExceptionMessage;
        }

        sealed class GraphSnapshot
        {
            public string Values;
            public object[] References;
        }

        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("S02c ownership validation check failed: " + name);
            passed++; Checks.AppendLine("PASS " + name);
        }

        static Outcome Invoke(Func<string> validator)
        {
            try { return new Outcome { Error = validator() }; }
            catch (Exception ex) { return new Outcome { ExceptionType = ex.GetType().FullName, ExceptionMessage = ex.Message }; }
        }

        static GraphSnapshot SnapshotSource(PlayerProfile profile)
        {
            var b = new StringBuilder();
            var references = new List<object>();
            var seen = new Dictionary<object, int>(new ReferenceComparer());
            AppendPersisted(profile, b, references, seen);
            b.Append("<nonserialized-storageCommit>").Append(profile.storageCommit.ToString(CultureInfo.InvariantCulture));
            return new GraphSnapshot { Values = b.ToString(), References = references.ToArray() };
        }

        static void AppendPersisted(object value, StringBuilder b, List<object> references, Dictionary<object, int> seen)
        {
            if (value == null) { b.Append("N;"); return; }
            Type type = value.GetType();
            if (value is string str) { b.Append("S").Append(str.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(str).Append(';'); return; }
            if (type.IsEnum) { b.Append("E").Append(type.FullName).Append(':').Append(Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)).Append(';'); return; }
            if (type == typeof(bool)) { b.Append((bool)value ? "B1;" : "B0;"); return; }
            if (type == typeof(float)) { b.Append("F").Append(BitConverter.ToInt32(BitConverter.GetBytes((float)value), 0).ToString("X8", CultureInfo.InvariantCulture)).Append(';'); return; }
            if (type == typeof(double)) { b.Append("D").Append(BitConverter.ToInt64(BitConverter.GetBytes((double)value), 0).ToString("X16", CultureInfo.InvariantCulture)).Append(';'); return; }
            if (type == typeof(decimal)) { b.Append("M").Append(((decimal)value).ToString("G29", CultureInfo.InvariantCulture)).Append(';'); return; }
            if (type == typeof(char)) { b.Append("C").Append(((int)(char)value).ToString(CultureInfo.InvariantCulture)).Append(';'); return; }
            if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong))
            { b.Append(type.FullName).Append(':').Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            if (type == typeof(DateTime)) { b.Append("T").Append(((DateTime)value).Ticks.ToString(CultureInfo.InvariantCulture)).Append(':').Append((int)((DateTime)value).Kind).Append(';'); return; }
            if (type == typeof(TimeSpan)) { b.Append("TS").Append(((TimeSpan)value).Ticks.ToString(CultureInfo.InvariantCulture)).Append(';'); return; }
            if (type == typeof(Guid)) { b.Append("G").Append(((Guid)value).ToString("N")).Append(';'); return; }
            if (value is Type reflectedType) { b.Append("TYPE").Append(reflectedType.AssemblyQualifiedName).Append(';'); return; }

            bool isReference = !type.IsValueType;
            if (isReference)
            {
                if (seen.TryGetValue(value, out int existing)) { b.Append("R").Append(existing.ToString(CultureInfo.InvariantCulture)).Append(';'); return; }
                seen.Add(value, references.Count); references.Add(value);
            }
            b.Append("O").Append(type.AssemblyQualifiedName).Append('{');
            if (value is System.Collections.IDictionary dictionary)
            {
                b.Append("COUNT:").Append(dictionary.Count.ToString(CultureInfo.InvariantCulture)).Append(';');
                foreach (System.Collections.DictionaryEntry entry in dictionary) { AppendPersisted(entry.Key, b, references, seen); AppendPersisted(entry.Value, b, references, seen); }
            }
            else if (value is System.Collections.IList list)
            {
                b.Append("COUNT:").Append(list.Count.ToString(CultureInfo.InvariantCulture)).Append(';');
                for (int i = 0; i < list.Count; i++) AppendPersisted(list[i], b, references, seen);
            }
            else
            {
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(field => !field.IsStatic && !field.IsNotSerialized)
                    .OrderBy(field => field.DeclaringType?.FullName, StringComparer.Ordinal)
                    .ThenBy(field => field.MetadataToken);
                foreach (FieldInfo field in fields)
                {
                    b.Append("FIELD:").Append(field.DeclaringType?.FullName).Append(':').Append(field.Name).Append('=');
                    AppendPersisted(field.GetValue(value), b, references, seen);
                }
            }
            b.Append("};");
        }

        static bool SameReferences<T>(IList<T> list, T[] before) where T : class
        {
            if (list == null || list.Count != before.Length) return list == null && before == null;
            for (int i = 0; i < before.Length; i++) if (!ReferenceEquals(list[i], before[i])) return false;
            return true;
        }

        static void Compare(PlayerProfile profile, string label, string expectedContains = null, Type expectedException = null)
        {
            GraphSnapshot before = SnapshotSource(profile);
            var cars = profile.cars == null ? null : profile.cars.ToArray();
            var engines = profile.engines == null ? null : profile.engines.ToArray();
            var parts = profile.parts == null ? null : profile.parts.ToArray();
            var legacy = Invoke(() => OwnershipValidationLegacyReference.Validate(profile, catalog));
            var current = Invoke(() => MechanicsOwnership.Validate(profile, catalog));
            Check(string.Equals(legacy.ExceptionType, current.ExceptionType, StringComparison.Ordinal), label + " exact exception type parity");
            if (legacy.ExceptionType != null)
            {
                Check(expectedException != null, label + " expected legacy exception outcome");
                Check(string.Equals(legacy.ExceptionMessage, current.ExceptionMessage, StringComparison.Ordinal), label + " exception message parity");
                if (expectedException != null) Check(current.ExceptionType == expectedException.FullName, label + " expected malformed-source exception type");
            }
            else
            {
                Check(expectedException == null, label + " did not throw the expected malformed-source exception");
                Check(string.Equals(legacy.Error, current.Error, StringComparison.Ordinal), label + " exact rejection/error text parity");
                if (expectedContains != null) Check(current.Error != null && current.Error.IndexOf(expectedContains, StringComparison.Ordinal) >= 0, label + " reaches expected validation invariant");
                else Check(current.Error == null, label + " both validators accept authored fixture");
            }
            Check(SameReferences(profile.cars, cars) && SameReferences(profile.engines, engines) && SameReferences(profile.parts, parts), label + " source collection/object references unchanged");
            GraphSnapshot after = SnapshotSource(profile);
            Check(string.Equals(before.Values, after.Values, StringComparison.Ordinal), label + " all persisted instance fields unchanged");
            Check(before.References.Length == after.References.Length && Enumerable.Range(0, before.References.Length).All(i => ReferenceEquals(before.References[i], after.References[i])), label + " nested source reference graph unchanged");
        }

        static VehicleDefinition SelectVehicle(int index) => authored[index % authored.Length];

        static PlayerProfile Fixture(int cars, int spares)
        {
            var profile = PlayerProfile.New(638000000000000000L);
            profile.SelectedCar.vehicleId = profile.selectedVehicleId = SelectVehicle(0).spec.id;
            for (int i = 1; i < cars; i++) profile.cars.Add(new OwnedCar { vehicleId = SelectVehicle(i).spec.id });
            MechanicsOwnership.Ensure(profile, catalog);
            foreach (var car in profile.cars)
            {
                var tirePack = PartFor(profile, car, PartCategory.Tires);
                if (tirePack?.condition.tires != null) foreach (var tire in tirePack.condition.tires) tire.definitionId = tirePack.definitionId;
                var wheelPack = PartFor(profile, car, PartCategory.Wheels);
                if (wheelPack?.condition.elements != null) foreach (var wheel in wheelPack.condition.elements) wheel.definitionId = wheelPack.definitionId;
            }
            var body = profile.parts.FirstOrDefault(p => catalog.Find(p.definitionId).category == PartCategory.Body)
                ?? profile.parts.First(p => catalog.Find(p.definitionId).category == PartCategory.Accessories);
            for (int i = 0; i < spares; i++)
            {
                var spare = body.Copy(); spare.instanceId = "s02c/spare/" + i.ToString("D4", CultureInfo.InvariantCulture);
                spare.location = PartLocation.Inventory; spare.ownerId = ""; spare.slot = ""; spare.storageId = SharedInventory.Garage;
                foreach (var member in spare.condition.elements ?? Array.Empty<EngineElement>()) if (member != null) member.id = spare.instanceId + "/member/" + member.cylinder.ToString(CultureInfo.InvariantCulture) + "/" + member.valve.ToString(CultureInfo.InvariantCulture);
                profile.parts.Add(spare);
            }
            string error = MechanicsOwnership.Validate(profile, catalog);
            if (error != null) throw new InvalidOperationException("Invalid S02c fixture: cars=" + cars + " spares=" + spares + " / " + error);
            return profile;
        }

        static PartInstance PartFor(PlayerProfile p, OwnedCar car, PartCategory category)
        {
            var binding = car.installed.slots.FirstOrDefault(s => s.slot == category.ToString());
            return binding == null ? null : p.parts.FirstOrDefault(part => part.instanceId == binding.partInstanceId);
        }

        static void CheckValidTypedMembers()
        {
            var p = Fixture(1, 0); var car = p.cars[0];
            var piston = PartFor(p, car, PartCategory.Pistons);
            var tires = PartFor(p, car, PartCategory.Tires);
            var wheels = PartFor(p, car, PartCategory.Wheels);
            Check(piston != null && piston.condition.elements != null && piston.condition.elements.Length > 0, "authored validation fixture contains typed piston members");
            Check(tires != null && tires.condition.tires != null && tires.condition.tires.Length == 4, "authored validation fixture contains four typed tire members");
            Check(wheels != null && wheels.condition.elements != null && wheels.condition.elements.Length == 4, "authored validation fixture contains four typed wheel members");
            if (piston?.condition.elements?.Length > 0 && EngineElements.Faults(PartCategory.Pistons).Contains(ElementFault.CompressionLeak))
                piston.condition.elements[0].fault = ElementFault.CompressionLeak;
            if (tires?.condition.tires?.Length == 4)
            {
                tires.condition.tires[0].health = .81f; tires.condition.tires[0].wearRemainder = .13;
                tires.condition.tires[3].health = .93f; tires.condition.tires[3].wearRemainder = .07;
                Check(tires.condition.tires.All(t => EngineElements.FaultAllowed(PartCategory.Tires, t)), "authored tire fixture uses only category-allowed member faults");
            }
            if (wheels?.condition.elements?.Length == 4)
            {
                wheels.condition.elements[1].health = .89f; wheels.condition.elements[2].wearRemainder = .02;
                Check(wheels.condition.elements.All(e => EngineElements.FaultAllowed(PartCategory.Wheels, e)), "authored wheel fixture uses only category-allowed member faults");
            }
            if (piston?.condition.elements?.Length > 0)
                Check(EngineElements.FaultAllowed(PartCategory.Pistons, piston.condition.elements[0]), "piston member fault fixture is validated against actual category rules");
            Compare(p, "valid typed piston/tire/wheel member wear and fault state");
        }

        static void CheckNullIdentityLookupSemantics()
        {
            var firstCar = new OwnedCar { instanceId = null };
            var secondCar = new OwnedCar { instanceId = null };
            var blankCar = new OwnedCar { instanceId = "" };
            var cars = new List<OwnedCar> { firstCar, secondCar, blankCar, new OwnedCar { instanceId = "Case" } };
            var firstEngine = new EngineAssemblyInstance { instanceId = null };
            var secondEngine = new EngineAssemblyInstance { instanceId = null };
            var blankEngine = new EngineAssemblyInstance { instanceId = "" };
            var engines = new List<EngineAssemblyInstance> { firstEngine, secondEngine, blankEngine };
            var firstPart = new PartInstance { instanceId = null };
            var secondPart = new PartInstance { instanceId = null };
            var blankPart = new PartInstance { instanceId = "" };
            var parts = new List<PartInstance> { firstPart, secondPart, blankPart, new PartInstance { instanceId = "Case" } };
            using (var lookup = OwnershipValidationLookup.Capture(cars, engines, parts))
            {
                Check(ReferenceEquals(lookup.FindCar(null), firstCar) && lookup.HasCarID(null), "null car ID maps to first null-ID record");
                Check(ReferenceEquals(lookup.FindCar(""), blankCar) && lookup.FindCar("case") == null, "empty and ordinal car IDs remain distinct");
                Check(ReferenceEquals(lookup.FindEngine(null), firstEngine) && ReferenceEquals(lookup.FindEngine(""), blankEngine), "null and empty engine IDs preserve first-match semantics");
                Check(ReferenceEquals(lookup.FindPart(null), firstPart) && ReferenceEquals(lookup.FindPart(""), blankPart), "null and empty part IDs preserve first-match semantics");
            }
            var ownerA = new OwnedCar { instanceId = "owner-a", installed = new InstalledBuild { slots = new List<SlotBinding> { new SlotBinding { slot = "Body", partInstanceId = null } } } };
            var ownerB = new OwnedCar { instanceId = "owner-b", installed = new InstalledBuild { slots = new List<SlotBinding> { new SlotBinding { slot = "Body", partInstanceId = null } } } };
            using (var lookup = OwnershipValidationLookup.Capture(new List<OwnedCar> { ownerA, ownerB }, new List<EngineAssemblyInstance>(), new List<PartInstance>()))
                Check(ReferenceEquals(lookup.CarContainingPart(null), ownerA), "null part-owner relation returns first binding owner");

            var profile = Fixture(1, 0); var car = profile.cars[0]; var body = PartFor(profile, car, PartCategory.Body);
            car.installed.slots.First(binding => binding.slot == PartCategory.Body.ToString()).partInstanceId = null;
            profile.parts.Remove(body);
            var nullIdPart = body.Copy(); nullIdPart.instanceId = null; profile.parts.Add(nullIdPart);
            Compare(profile, "null part ID lookup preserves old binding resolution before later uniqueness rejection", "Duplicate/invalid/orphan part instance.");
        }

        static void CheckAdversarialMutations()
        {
            var earlyGate = Fixture(1, 1); earlyGate.cash = -1;
            earlyGate.parts[earlyGate.parts.Count - 1].instanceId = earlyGate.parts[0].instanceId;
            Compare(earlyGate, "invalid cash gate precedes later duplicate identity", "Invalid profile version/revision/cash.");

            var duplicate = Fixture(1, 1); duplicate.parts[duplicate.parts.Count - 1].instanceId = duplicate.parts[0].instanceId;
            Compare(duplicate, "duplicate part identity", "Duplicate/invalid/orphan part instance.");

            var orphan = Fixture(1, 1); var loose = orphan.parts[orphan.parts.Count - 1];
            loose.location = PartLocation.Vehicle; loose.ownerId = orphan.cars[0].instanceId; loose.slot = PartCategory.Body.ToString();
            Compare(orphan, "unbound vehicle-located orphan", "Duplicate/invalid/orphan part instance.");

            var crossOwner = Fixture(2, 0); var second = crossOwner.cars[1]; var secondBody = PartFor(crossOwner, second, PartCategory.Body);
            secondBody.ownerId = crossOwner.cars[0].instanceId;
            Compare(crossOwner, "cross-car installed owner mismatch", "Part parent/fitment does not match its slot.");

            var duplicateMember = Fixture(1, 0); var p0 = duplicateMember.parts.First(p => p.condition.elements != null && p.condition.elements.Length > 0); var p1 = duplicateMember.parts.First(p => !ReferenceEquals(p, p0) && p.condition.elements != null && p.condition.elements.Length > 0);
            p1.condition.elements[0].id = p0.condition.elements[0].id;
            Compare(duplicateMember, "cross-component member identity duplicate", "Invalid/duplicate typed element identity, category or fault.");

            var wrongFault = Fixture(1, 0); var tireSet = PartFor(wrongFault, wrongFault.cars[0], PartCategory.Tires);
            if (tireSet?.condition.tires?.Length == 4)
            {
                tireSet.condition.tires[0].fault = ElementFault.Disabled;
                Compare(wrongFault, "disallowed tire member fault", "Duplicate/invalid/orphan part instance.");
            }

            var wrongSlot = Fixture(1, 0); var pistonBinding = wrongSlot.cars[0].installed.slots.First(s => s.slot == PartCategory.Pistons.ToString());
            pistonBinding.partInstanceId = PartFor(wrongSlot, wrongSlot.cars[0], PartCategory.Rods).instanceId;
            Compare(wrongSlot, "binding points across slot categories", "Part parent/fitment does not match its slot.");

            var wrongParent = Fixture(1, 0); wrongParent.engines[0].parentVehicleId = "missing-parent";
            Compare(wrongParent, "attached engine parent mismatch", "Invalid engine parent.");

            var detached = Fixture(1, 0); AssemblyOperations.Detach(detached, detached.cars[0].instanceId);
            Compare(detached, "legitimate detached engine and descendants");

            var nullPart = Fixture(1, 0); nullPart.parts.Add(null);
            Compare(nullPart, "null part row legacy exception timing", expectedException: typeof(NullReferenceException));

            var nullEngine = Fixture(1, 0); nullEngine.engines.Add(null);
            Compare(nullEngine, "null engine row legacy exception timing", expectedException: typeof(NullReferenceException));

            var nullCar = Fixture(1, 0); nullCar.cars.Add(null);
            Compare(nullCar, "null car row legacy exception timing", expectedException: typeof(NullReferenceException));

            var nullBinding = Fixture(1, 0); nullBinding.cars[0].installed.slots.Add(null);
            Compare(nullBinding, "null binding row legacy exception timing", expectedException: typeof(NullReferenceException));

            var selected = Fixture(1, 0); selected.selectedOwnedVehicleId = "missing-owned-car";
            Compare(selected, "selected car reference invariant", "Invalid selected owned vehicle reference.");
        }

        struct Sample
        {
            public double Milliseconds;
            public int Gen0, Gen1, Gen2;
        }

        static Sample Measure(Func<string> validator)
        {
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            var watch = Stopwatch.StartNew(); var error = validator(); watch.Stop();
            if (error != null) throw new InvalidOperationException("Timed validation rejected valid workload: " + error);
            return new Sample { Milliseconds = watch.Elapsed.TotalMilliseconds, Gen0 = GC.CollectionCount(0) - g0, Gen1 = GC.CollectionCount(1) - g1, Gen2 = GC.CollectionCount(2) - g2 };
        }

        static double Median(Sample[] values)
        {
            var ordered = values.Select(v => v.Milliseconds).OrderBy(v => v).ToArray();
            int middle = ordered.Length / 2;
            return (ordered.Length & 1) == 0 ? (ordered[middle - 1] + ordered[middle]) / 2 : ordered[middle];
        }

        static void CheckRelationLifetime()
        {
            var a = new OwnedCar { installed = new InstalledBuild { slots = new List<SlotBinding> {
                new SlotBinding { partInstanceId = "other" }, new SlotBinding { partInstanceId = "shared" } } } };
            var b = new OwnedCar { installed = new InstalledBuild { slots = new List<SlotBinding> {
                new SlotBinding { partInstanceId = "shared" } } } };
            var cars = new List<OwnedCar> { a, b };
            var lookup = OwnershipValidationLookup.Capture(cars, new List<EngineAssemblyInstance>(), new List<PartInstance>());
            Check(ReferenceEquals(lookup.CarContainingPart("shared"), a), "non-null duplicate relation keeps first car and binding order");
            lookup.Dispose(); lookup.Dispose();
            Check(Invoke(() => { lookup.FindCar("a"); return null; }).ExceptionType == typeof(ObjectDisposedException).FullName, "disposed car lookup rejects access");
            Check(Invoke(() => { lookup.FindEngine("a"); return null; }).ExceptionType == typeof(ObjectDisposedException).FullName, "disposed engine lookup rejects access");
            Check(Invoke(() => { lookup.FindPart("a"); return null; }).ExceptionType == typeof(ObjectDisposedException).FullName, "disposed part lookup rejects access");
            Check(Invoke(() => { lookup.HasCarID("a"); return null; }).ExceptionType == typeof(ObjectDisposedException).FullName, "disposed car existence lookup rejects access");
            Check(Invoke(() => { lookup.CarContainingPart("shared"); return null; }).ExceptionType == typeof(ObjectDisposedException).FullName, "disposed built relation rejects access");
            a.installed.slots.Add(null);
            using (var malformed = OwnershipValidationLookup.Capture(cars, new List<EngineAssemblyInstance>(), new List<PartInstance>()))
            {
                Check(ReferenceEquals(malformed.CarContainingPart("shared"), a), "malformed later binding preserves earlier matching owner");
                Check(Invoke(() => { malformed.CarContainingPart("missing"); return null; }).ExceptionType == typeof(NullReferenceException).FullName, "malformed binding before missing match preserves scan exception");
            }
            Check(Median(new[] { new Sample { Milliseconds = 4 }, new Sample { Milliseconds = 2 } }) == 3, "even sample median averages middle values");
            Check(Median(new[] { new Sample { Milliseconds = 4 }, new Sample { Milliseconds = 2 }, new Sample { Milliseconds = 3 } }) == 3, "odd sample median uses middle value");
        }

        public static void RunLifetimeStandalone()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying) throw new InvalidOperationException("S02c checks require Editor batch mode outside Play Mode.");
            Checks.Length = 0; passed = 0;
            CheckRelationLifetime();
            Checks.AppendLine("RESULT PASS count=" + passed);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S02C"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "ownership-validation-lifetime-checks.txt"), Checks.ToString(), new UTF8Encoding(false));
            UnityEngine.Debug.Log("S02c lookup lifetime checks passed: " + passed);
        }

        static void Benchmark()
        {
            Timings.AppendLine("cars,spares,total_parts,installed_bindings,warmup_pairs,repeats,pair,order,legacy_ms,legacy_gc0,legacy_gc1,legacy_gc2,indexed_ms,indexed_gc0,indexed_gc1,indexed_gc2");
            foreach (int carCount in new[] { 1, 5, 20 })
            foreach (int spareCount in new[] { 0, 24, 96 })
            {
                var profile = Fixture(carCount, spareCount);
                var baseline = OwnershipValidationLegacyReference.Validate(profile, catalog);
                var current = MechanicsOwnership.Validate(profile, catalog);
                Check(baseline == null && current == null, "valid benchmark fixture car/spare count " + carCount + "/" + spareCount);
                var oldSamples = new Sample[Repeats]; var newSamples = new Sample[Repeats];
                for (int i = 0; i < WarmupPairs; i++)
                {
                    if ((i & 1) == 0) { Measure(() => OwnershipValidationLegacyReference.Validate(profile, catalog)); Measure(() => MechanicsOwnership.Validate(profile, catalog)); }
                    else { Measure(() => MechanicsOwnership.Validate(profile, catalog)); Measure(() => OwnershipValidationLegacyReference.Validate(profile, catalog)); }
                }
                for (int i = 0; i < Repeats; i++)
                {
                    if ((i & 1) == 0)
                    {
                        oldSamples[i] = Measure(() => OwnershipValidationLegacyReference.Validate(profile, catalog));
                        newSamples[i] = Measure(() => MechanicsOwnership.Validate(profile, catalog));
                    }
                    else
                    {
                        newSamples[i] = Measure(() => MechanicsOwnership.Validate(profile, catalog));
                        oldSamples[i] = Measure(() => OwnershipValidationLegacyReference.Validate(profile, catalog));
                    }
                    Timings.AppendLine(string.Join(",", new[] {
                        carCount.ToString(CultureInfo.InvariantCulture), spareCount.ToString(CultureInfo.InvariantCulture), profile.parts.Count.ToString(CultureInfo.InvariantCulture), profile.cars.Sum(c => c.installed.slots.Count).ToString(CultureInfo.InvariantCulture), WarmupPairs.ToString(CultureInfo.InvariantCulture), Repeats.ToString(CultureInfo.InvariantCulture), i.ToString(CultureInfo.InvariantCulture), (i & 1) == 0 ? "legacy_then_indexed" : "indexed_then_legacy",
                        oldSamples[i].Milliseconds.ToString("R", CultureInfo.InvariantCulture), oldSamples[i].Gen0.ToString(CultureInfo.InvariantCulture), oldSamples[i].Gen1.ToString(CultureInfo.InvariantCulture), oldSamples[i].Gen2.ToString(CultureInfo.InvariantCulture),
                        newSamples[i].Milliseconds.ToString("R", CultureInfo.InvariantCulture), newSamples[i].Gen0.ToString(CultureInfo.InvariantCulture), newSamples[i].Gen1.ToString(CultureInfo.InvariantCulture), newSamples[i].Gen2.ToString(CultureInfo.InvariantCulture)
                    }));
                }
                Checks.AppendLine("BENCH_SUMMARY cars=" + carCount + " spares=" + spareCount + " legacy_median_ms=" + Median(oldSamples).ToString("R", CultureInfo.InvariantCulture) + " legacy_sample_max_ms=" + oldSamples.Max(s => s.Milliseconds).ToString("R", CultureInfo.InvariantCulture) + " indexed_median_ms=" + Median(newSamples).ToString("R", CultureInfo.InvariantCulture) + " indexed_sample_max_ms=" + newSamples.Max(s => s.Milliseconds).ToString("R", CultureInfo.InvariantCulture));
            }
        }

        public static void RunStandalone()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying) throw new InvalidOperationException("S02c checks require Editor batch mode outside Play Mode.");
            Checks.Length = 0; Timings.Length = 0; passed = 0;
            Checks.AppendLine("S02c validation parity; test-only pre-change ordered-scan reference; authored typed in-memory profiles; no JSON, ProfileStore, or whole-profile clone.");
            try
            {
                authored = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
                if (authored == null || authored.Length == 0) throw new InvalidOperationException("S02c requires authored vehicle definitions.");
                catalog = new MechanicalCatalog(authored.Select(v => v.spec));
                Checks.AppendLine("CONTENT vehicles=" + authored.Length.ToString(CultureInfo.InvariantCulture) + " fixture_id_policy=logical-counts-and-typed-invariants runtime_entropy_ids_not_byte-pinned");
                CheckNullIdentityLookupSemantics();
                CheckValidTypedMembers();
                CheckAdversarialMutations();
                CheckRelationLifetime();
                Benchmark();
                Checks.AppendLine("BENCH rows=9 repeats=" + Repeats + " warmup_pairs=" + WarmupPairs + " order=paired_alternating_ab_ba raw_samples=true gc_collection_deltas_per_call=true workloads=1,5,20_cars_x_0,24,96_spares statistics=median_and_sample_max_only both_validators_full_validate=true");
                Checks.AppendLine("RESULT PASS count=" + passed.ToString(CultureInfo.InvariantCulture)); WriteReports();
                UnityEngine.Debug.Log("S02c ownership validation checks passed: " + passed);
            }
            catch (Exception ex)
            {
                Checks.AppendLine("RESULT FAIL count=" + passed.ToString(CultureInfo.InvariantCulture)); Checks.AppendLine(ex.ToString());
                WriteReports(); UnityEngine.Debug.LogException(ex); throw;
            }
        }

        static void WriteReports()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S02C"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "ownership-validation-lookup-checks.txt"), Checks.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "ownership-validation-lookup-timings.csv"), Timings.ToString(), new UTF8Encoding(false));
        }
    }
#endif
}
