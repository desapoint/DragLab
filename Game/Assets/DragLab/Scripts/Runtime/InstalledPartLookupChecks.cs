using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DriveType = DragLab.Domain.DriveType;
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
    /// <summary>Independent scan parity and bounded cost checks for installed part capture.</summary>
    public static class InstalledPartLookupChecks
    {
        const int Repeats = 5;
        static readonly StringBuilder Checks = new StringBuilder();
        static readonly StringBuilder Timings = new StringBuilder();
        static int passed;

        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("S02b installed-part lookup check failed: " + name);
            passed++; Checks.AppendLine("PASS " + name);
        }

        static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T) { passed++; Checks.AppendLine("PASS " + name); return; }
            throw new InvalidOperationException("S02b installed-part lookup check failed (expected " + typeof(T).Name + "): " + name);
        }

        static PartInstance Scan(IList<PartInstance> parts, string id)
        {
            for (int i = 0; i < parts.Count; i++)
                if (parts[i] != null && string.Equals(parts[i].instanceId, id, StringComparison.Ordinal)) return parts[i];
            return null;
        }

        static List<PartInstance> ScanBindingOrder(IList<PartInstance> parts, IList<SlotBinding> slots)
        {
            var result = new List<PartInstance>(slots.Count);
            foreach (var slot in slots)
            {
                var found = Scan(parts, slot.partInstanceId);
                if (found != null) result.Add(found);
            }
            return result;
        }

        static Dictionary<PartCategory, string> ScanDefinitions(IList<PartInstance> parts, IList<SlotBinding> slots)
        {
            var result = new Dictionary<PartCategory, string>();
            foreach (var binding in slots)
            {
                var part = Scan(parts, binding.partInstanceId);
                if (part != null) result.Add((PartCategory)Enum.Parse(typeof(PartCategory), binding.slot), part.definitionId);
            }
            return result;
        }

        static void CheckLookupSurface()
        {
            var parts = new List<PartInstance> { new PartInstance { instanceId = "lookup/a", definitionId = "a" }, new PartInstance { instanceId = "lookup/b", definitionId = "b" } };
            var slots = new List<SlotBinding> { new SlotBinding { slot = "Pistons", partInstanceId = "lookup/b" }, new SlotBinding { slot = "Rods", partInstanceId = "missing" }, new SlotBinding { slot = "HeadIntake", partInstanceId = "lookup/a" } };
            using (var lookup = InstalledPartLookup.Capture(parts, slots))
            {
                Check(lookup.Count == slots.Count, "one capture entry per binding row");
                Check(lookup.EntryAt(0).Slot == slots[0].slot && ReferenceEquals(lookup.EntryAt(0).Part, parts[1]), "capture keeps binding order and exact part reference");
                Check(lookup.EntryAt(1).Slot == slots[1].slot && lookup.EntryAt(1).Part == null, "dangling binding remains null without fabricating a part");
                Check(ReferenceEquals(lookup.EntryAt(2).Part, parts[0]), "later binding remains in original position");
                var actual = lookup.CopyDefinitions(); var expected = ScanDefinitions(parts, slots);
                Check(actual.Count == expected.Count && actual[PartCategory.Pistons] == expected[PartCategory.Pistons] && actual[PartCategory.HeadIntake] == expected[PartCategory.HeadIntake], "definition map matches ordered legacy Find scans and skips missing part");
            }
            Throws<ObjectDisposedException>(() => { var ignored = lookupAfterDispose(parts, slots); }, "disposed lookup rejects later access");
            Throws<ArgumentException>(() => {
                using (var duplicate = InstalledPartLookup.Capture(parts, new List<SlotBinding> {
                    new SlotBinding { slot = "Pistons", partInstanceId = "lookup/a" },
                    new SlotBinding { slot = "Pistons", partInstanceId = "lookup/b" }
                })) duplicate.CopyDefinitions();
            }, "duplicate category preserves legacy Dictionary.Add rejection");
            using (var empty = InstalledPartLookup.Capture(new List<PartInstance>(), new List<SlotBinding> { new SlotBinding { slot = "Pistons", partInstanceId = "not-there" } }))
                Check(empty.Count == 1 && empty.EntryAt(0).Part == null && empty.CopyDefinitions().Count == 0, "empty source and dangling binding do not fabricate installed records");
            using (var mixedMissing = InstalledPartLookup.Capture(new List<PartInstance> { parts[1] }, new List<SlotBinding> {
                new SlotBinding { slot = "Pistons", partInstanceId = "lookup/b" },
                new SlotBinding { slot = "Rods", partInstanceId = "lookup/absent" }
            }))
                Check(ReferenceEquals(mixedMissing.EntryAt(0).Part, parts[1]) && mixedMissing.EntryAt(1).Part == null && mixedMissing.CopyDefinitions().Count == 1, "more distinct binding IDs than source parts uses bounded full-scan fallback and keeps missing row absent");
            using (var blankId = InstalledPartLookup.Capture(parts, new List<SlotBinding> { new SlotBinding { slot = "Pistons", partInstanceId = " " } }))
                Check(blankId.EntryAt(0).Part == null && blankId.CopyDefinitions().Count == 0, "blank binding identity follows legacy missing-part behavior");
            Throws<ArgumentException>(() => InstalledPartLookup.Capture(parts, new List<SlotBinding> { null }), "null binding row is rejected before capture");

            var oldId = parts[0].instanceId; parts[0] = new PartInstance { instanceId = "lookup/replaced", definitionId = "replacement" };
            using (var rebuilt = InstalledPartLookup.Capture(parts, new List<SlotBinding> { new SlotBinding { slot = "Pistons", partInstanceId = "lookup/replaced" }, new SlotBinding { slot = "Rods", partInstanceId = oldId } }))
                Check(ReferenceEquals(rebuilt.EntryAt(0).Part, parts[0]) && rebuilt.EntryAt(1).Part == null, "fresh capture after same-count source replacement sees current candidate identity");
        }

        static InstalledPartLookup lookupAfterDispose(IList<PartInstance> parts, IList<SlotBinding> slots)
        {
            var lookup = InstalledPartLookup.Capture(parts, slots); lookup.Dispose(); lookup.EntryAt(0); return lookup;
        }

        static bool Equal(object left, object right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.GetType() != right.GetType()) return false;
            var type = left.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)) return left.Equals(right);
            if (type.IsArray)
            {
                var a = (Array)left; var b = (Array)right;
                if (a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++) if (!Equal(a.GetValue(i), b.GetValue(i))) return false;
                return true;
            }
            if (left is System.Collections.IDictionary ld && right is System.Collections.IDictionary rd)
            {
                if (ld.Count != rd.Count) return false;
                foreach (System.Collections.DictionaryEntry e in ld) if (!rd.Contains(e.Key) || !Equal(e.Value, rd[e.Key])) return false;
                return true;
            }
            if (left is System.Collections.IList ll && right is System.Collections.IList rl)
            {
                if (ll.Count != rl.Count) return false;
                for (int i = 0; i < ll.Count; i++) if (!Equal(ll[i], rl[i])) return false;
                return true;
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (!field.IsStatic && !field.IsNotSerialized && !Equal(field.GetValue(left), field.GetValue(right))) return false;
            return true;
        }

        static BuildResolution ScanResolve(PlayerProfile profile, OwnedCar car, MechanicalCatalog catalog)
        {
            var result = catalog.Resolve(car.vehicleId, ScanDefinitions(profile.parts, car.installed.slots), car.tune);
            TireFitments.Capture(result.spec, ScanBindingOrder(profile.parts, car.installed.slots), catalog);
            if (string.IsNullOrEmpty(car.engineAssemblyId)) result.errors.Add("No engine attached. Attach a stored engine in Assemblies.");
            if (car.engineRebuild != null) result.errors.Add("Engine is reserved at the rebuild shop. Collect it after the quoted time.");
            if (!string.IsNullOrEmpty(car.garageJobId)) result.errors.Add("Car and installed components are reserved by a garage job. Claim or cancel to release.");
            foreach (var binding in car.installed.slots)
            {
                var item = Scan(profile.parts, binding.partInstanceId);
                if (item != null && catalog.Find(item.definitionId).category == PartCategory.Tires && item.condition.tires != null && item.condition.tires.Length == 4)
                {
                    for (int i = 0; i < 4; i++) if (item.condition.tires[i].Failed) result.warnings.Add(new[] { "Front left", "Front right", "Rear left", "Rear right" }[i] + " tire failed");
                    continue;
                }
                if (item != null && (PartSemantics.Wears(catalog.Find(item.definitionId).category) || catalog.Find(item.definitionId).category == PartCategory.Controls && result.spec.engineBehavior.fueling == FuelSystem.Injection) && (item.condition.failed || item.condition.health <= 0))
                    (MechanicalCatalog.DisablingEngineFailure(catalog.Find(item.definitionId).category) || catalog.Find(item.definitionId).category == PartCategory.Controls && result.spec.engineBehavior.fueling == FuelSystem.Injection ? result.errors : result.warnings).Add(binding.slot + " failed: " + item.condition.failureReason);
            }
            return result;
        }

        static void CheckResolutionEqual(BuildResolution actual, BuildResolution expected, string context)
        {
            Check(Equal(actual.spec, expected.spec), context + " spec and tire/wheel fitment parity");
            Check(Equal(actual.tune, expected.tune), context + " resolved tune parity");
            Check(actual.errors.SequenceEqual(expected.errors), context + " ordered errors parity");
            Check(actual.warnings.SequenceEqual(expected.warnings), context + " ordered warnings parity");
            Check(actual.safeRpm.Equals(expected.safeRpm) && actual.safeTorqueNm.Equals(expected.safeTorqueNm), context + " safety limit parity");
        }

        static List<VehicleDefinition> RepresentativeVehicles(VehicleDefinition[] all)
        {
            var selected = new List<VehicleDefinition>();
            var awd = all.FirstOrDefault(v => v.spec.drive == DriveType.AWD);
            var diesel = all.FirstOrDefault(v => v.spec.engineBehavior.fueling == FuelSystem.Diesel);
            var petrol = all.FirstOrDefault(v => v.spec.drive != DriveType.AWD && v.spec.engineBehavior.fueling != FuelSystem.Diesel);
            foreach (var item in new[] { petrol, diesel, awd }) if (item != null && !selected.Contains(item)) selected.Add(item);
            foreach (var item in all) if (selected.Count < 3 && !selected.Contains(item)) selected.Add(item);
            return selected;
        }

        static PlayerProfile AuthoredProfile(VehicleDefinition vehicle, MechanicalCatalog catalog)
        {
            var profile = PlayerProfile.New(638000000000000000L);
            profile.SelectedCar.vehicleId = profile.selectedVehicleId = vehicle.spec.id;
            MechanicsOwnership.Ensure(profile, catalog);
            return profile;
        }

        static void CheckAuthoredParity()
        {
            var authored = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            if (authored == null || authored.Length == 0) throw new InvalidOperationException("S02b requires authored vehicle definitions.");
            var catalog = new MechanicalCatalog(authored.Select(v => v.spec));
            var vehicles = RepresentativeVehicles(authored);
            foreach (var vehicle in vehicles)
            {
                var profile = AuthoredProfile(vehicle, catalog); var car = profile.SelectedCar;
                var tireSet = car.installed.slots.Select(s => Scan(profile.parts, s.partInstanceId)).FirstOrDefault(p => p != null && catalog.Find(p.definitionId).category == PartCategory.Tires);
                if (tireSet != null && tireSet.condition.tires != null && tireSet.condition.tires.Length == 4)
                {
                    tireSet.condition.tires[0].health = .73f; tireSet.condition.tires[0].wearRemainder = .17;
                    tireSet.condition.tires[2].health = .88f; tireSet.condition.tires[2].wearRemainder = .09;
                    var alternative = catalog.parts.FirstOrDefault(part => part.category == PartCategory.Tires && part.id != tireSet.definitionId && catalog.Fits(vehicle.spec, part));
                    Check(alternative != null, vehicle.spec.id + " mixed-specification fixture has another physically fitting tire");
                    tireSet.condition.tires[0].definitionId = alternative.id;
                    tireSet.condition.tires[1].definitionId = alternative.id;
                    tireSet.condition.tires[2].definitionId = tireSet.definitionId;
                    tireSet.condition.tires[3].definitionId = tireSet.definitionId;
                }
                var memberSet = car.installed.slots.Select(s => Scan(profile.parts, s.partInstanceId)).FirstOrDefault(p => p != null && p.condition.elements != null && p.condition.elements.Length >= 2);
                if (memberSet != null)
                {
                    memberSet.condition.elements[0].health = .81f; memberSet.condition.elements[0].wearRemainder = .11;
                    memberSet.condition.elements[memberSet.condition.elements.Length - 1].health = .92f;
                }
                var sourceRefs = profile.parts.ToArray(); var conditionRefs = profile.parts.Select(p => p.condition).ToArray();
                var expectedDefinitions = ScanDefinitions(profile.parts, car.installed.slots);
                var definitions = MechanicsOwnership.Definitions(profile, car);
                Check(Equal(definitions, expectedDefinitions), vehicle.spec.id + " public Definitions matches independent ordered scans");
                var expectedResolve = ScanResolve(profile, car, catalog);
                var actualResolve = MechanicsOwnership.Resolve(profile, car, catalog);
                CheckResolutionEqual(actualResolve, expectedResolve, vehicle.spec.id + " public Resolve");

                var inspection = InspectionBuild.Capture(profile, car);
                var expectedParts = ScanBindingOrder(profile.parts, car.installed.slots).Select(p => p.Copy()).ToList();
                Check(inspection.components.Count == expectedParts.Count && inspection.components.Select(p => p.instanceId).SequenceEqual(expectedParts.Select(p => p.instanceId)), vehicle.spec.id + " inspection component order/IDs match scan oracle");
                for (int i = 0; i < expectedParts.Count; i++) Check(Equal(inspection.components[i], expectedParts[i]), vehicle.spec.id + " inspection full typed PartInstance matches scan copy including identity/location/member history " + i);
                Check(inspection.startingFuel.litres.Equals(car.fuel.litres) && Equal(inspection.startingFluids, car.fluids), vehicle.spec.id + " inspection starts from fuel quantity and exact fluid values");
                var expectedInspectionBuild = catalog.Resolve(car.vehicleId, expectedDefinitions, car.tune);
                TireFitments.Capture(expectedInspectionBuild.spec, expectedParts, catalog);
                if (string.IsNullOrEmpty(car.engineAssemblyId)) expectedInspectionBuild.errors.Add("No engine attached: stored engines retain their own hardware and liquids.");
                CheckResolutionEqual(inspection.build, expectedInspectionBuild, vehicle.spec.id + " InspectionBuild.Capture");
                if (tireSet != null)
                    Check(inspection.build.spec.cornerTireDefinitions.Length == 4 && inspection.build.spec.cornerTireDefinitions[0].id != inspection.build.spec.cornerTireDefinitions[2].id,
                        vehicle.spec.id + " inspection retains distinct front/rear tire specifications");
                Check(profile.parts.Count == sourceRefs.Length && Enumerable.Range(0, sourceRefs.Length).All(i => ReferenceEquals(profile.parts[i], sourceRefs[i]) && ReferenceEquals(profile.parts[i].condition, conditionRefs[i])), vehicle.spec.id + " all three read-only public calls preserve source record and condition references");
                if (inspection.components.Count > 0)
                {
                    var source = Scan(profile.parts, inspection.components[0].instanceId); var savedHealth = source.condition.health;
                    float copiedHealth = inspection.components[0].condition.health;
                    Check(!ReferenceEquals(source, inspection.components[0]) && !ReferenceEquals(source.condition, inspection.components[0].condition), vehicle.spec.id + " inspection exposes detached typed component copies");
                    source.condition.health = savedHealth == .52f ? .53f : .52f;
                    Check(inspection.components[0].condition.health == copiedHealth, vehicle.spec.id + " post-capture source condition mutation cannot alter captured inspection component");
                    source.condition.health = savedHealth;
                }
                if (car.installed.slots.Count >= 2)
                {
                    var firstBinding = car.installed.slots[0]; var secondBinding = car.installed.slots[1];
                    car.installed.slots.Add(new SlotBinding { slot = firstBinding.slot, partInstanceId = secondBinding.partInstanceId });
                    Throws<ArgumentException>(() => MechanicsOwnership.Definitions(profile, car), vehicle.spec.id + " public Definitions rejects duplicate category binding");
                    Throws<ArgumentException>(() => ScanDefinitions(profile.parts, car.installed.slots), vehicle.spec.id + " independent scan oracle rejects duplicate category binding");
                    car.installed.slots.RemoveAt(car.installed.slots.Count - 1);
                }

                if (car.installed.slots.Count > 0)
                {
                    var first = car.installed.slots[0]; var removed = Scan(profile.parts, first.partInstanceId);
                    profile.parts.Remove(removed);
                    var missingDefinitions = MechanicsOwnership.Definitions(profile, car);
                    var missingExpected = ScanDefinitions(profile.parts, car.installed.slots);
                    Check(Equal(missingDefinitions, missingExpected), vehicle.spec.id + " detached/missing binding is skipped without fabrication");
                    Check(!missingDefinitions.ContainsKey((PartCategory)Enum.Parse(typeof(PartCategory), first.slot)), vehicle.spec.id + " missing binding does not create a definition");
                    var missingInspection = InspectionBuild.Capture(profile, car);
                    Check(missingInspection.components.Count == ScanBindingOrder(profile.parts, car.installed.slots).Count, vehicle.spec.id + " missing binding omitted from inspection component copies");
                }
                var detachedEngineProfile = AuthoredProfile(vehicle, catalog); var detachedCar = detachedEngineProfile.SelectedCar;
                detachedCar.engineAssemblyId = "";
                var detachedExpected = ScanResolve(detachedEngineProfile, detachedCar, catalog);
                CheckResolutionEqual(MechanicsOwnership.Resolve(detachedEngineProfile, detachedCar, catalog), detachedExpected, vehicle.spec.id + " detached engine error/warning parity");
            }
            Check(vehicles.Count == 3, "three authored representative cars selected (petrol, diesel, AWD when authored)");
        }

        static PartInstance PadPart(int i)
        {
            return new PartInstance { instanceId = "s02b/pad/" + i.ToString("D6", CultureInfo.InvariantCulture), definitionId = "unused", location = PartLocation.Inventory, ownerId = "", slot = "", condition = new ComponentCondition() };
        }

        static ulong StableOrderKey(string value)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in value) { hash ^= c; hash *= 1099511628211UL; }
            return hash;
        }

        static void Arrange(List<PartInstance> parts, List<SlotBinding> slots, int total, string mode)
        {
            var installed = new List<PartInstance>();
            foreach (var slot in slots) { var part = Scan(parts, slot.partInstanceId); if (part != null) installed.Add(part); }
            var rest = parts.Where(p => !installed.Contains(p)).ToList();
            if (mode == "shuffled") installed = installed.OrderBy(p => StableOrderKey(p.instanceId)).ToList();
            var combined = new List<PartInstance>(total);
            if (mode == "shuffled")
            {
                for (int i = 0; i < total; i++) combined.Add(null);
                foreach (var part in installed)
                {
                    int position = (int)(StableOrderKey(part.instanceId) % (ulong)total);
                    while (combined[position] != null) position = (position + 1) % total;
                    combined[position] = part;
                }
                int restIndex = 0, padIndex = 0;
                for (int i = 0; i < total; i++) if (combined[i] == null) combined[i] = restIndex < rest.Count ? rest[restIndex++] : PadPart(padIndex++);
            }
            else
            {
                if (mode == "first") { combined.AddRange(installed); combined.AddRange(rest); }
                else { combined.AddRange(rest); combined.AddRange(installed); }
                int pad = 0; while (combined.Count < total) combined.Add(PadPart(pad++));
                if (combined.Count > total) combined.RemoveRange(total, combined.Count - total);
            }
            parts.Clear(); parts.AddRange(combined);
        }

        static (List<PartInstance> refs, Dictionary<PartCategory, string> definitions, List<PartInstance> copies) LegacyCapture(IList<PartInstance> parts, IList<SlotBinding> slots)
        {
            // Timed baseline uses the production List.Find path, rather than substituting
            // a differently optimized interface scan. The independent correctness oracle
            // above deliberately remains separate from this benchmark implementation.
            var source = parts as List<PartInstance> ?? throw new InvalidOperationException("Benchmark requires its fixed List fixture.");
            var defs = new Dictionary<PartCategory, string>();
            foreach (var binding in slots)
            {
                var part = source.Find(p => p.instanceId == binding.partInstanceId);
                if (part != null) defs.Add((PartCategory)Enum.Parse(typeof(PartCategory), binding.slot), part.definitionId);
            }
            var refs = slots.Select(binding => source.Find(p => p.instanceId == binding.partInstanceId)).Where(part => part != null).ToList();
            var copies = refs.Select(p => p.Copy()).ToList();
            return (refs, defs, copies);
        }

        static (List<PartInstance> refs, Dictionary<PartCategory, string> definitions, List<PartInstance> copies) IndexedCapture(IList<PartInstance> parts, IList<SlotBinding> slots)
        {
            using (var lookup = InstalledPartLookup.Capture(parts, slots))
            {
                var refs = new List<PartInstance>(lookup.Count);
                var copies = new List<PartInstance>(lookup.Count);
                for (int i = 0; i < lookup.Count; i++)
                {
                    var part = lookup.EntryAt(i).Part;
                    if (part == null) continue;
                    refs.Add(part); copies.Add(part.Copy());
                }
                return (refs, lookup.CopyDefinitions(), copies);
            }
        }

        static double Measure(Action action)
        {
            var watch = Stopwatch.StartNew(); action(); watch.Stop(); return watch.Elapsed.TotalMilliseconds;
        }

        static double Median(double[] values) => values.OrderBy(v => v).ElementAt(values.Length / 2);

        static void Benchmark()
        {
            Timings.AppendLine("vehicle_id,parts_count,installed_count,position_group,repeats,legacy_capture_defs_copies_median_ms,legacy_capture_defs_copies_sample_max_ms,indexed_capture_defs_copies_median_ms,indexed_capture_defs_copies_sample_max_ms,public_definitions_median_ms,public_definitions_sample_max_ms,public_resolve_median_ms,public_resolve_sample_max_ms,inspection_capture_median_ms,inspection_capture_sample_max_ms");
            var authored = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            var catalog = new MechanicalCatalog(authored.Select(v => v.spec));
            foreach (var vehicle in RepresentativeVehicles(authored))
            foreach (int size in new[] { 100, 1000, 10000, 30000 })
            foreach (string group in new[] { "first", "last", "shuffled" })
            {
                var profile = AuthoredProfile(vehicle, catalog); var car = profile.SelectedCar;
                while (profile.parts.Count < size) profile.parts.Add(PadPart(profile.parts.Count));
                Arrange(profile.parts, car.installed.slots, size, group);
                // Warm and validate every variant before timing; comparisons perform no reflection inside timed regions.
                var legacy = LegacyCapture(profile.parts, car.installed.slots); var indexed = IndexedCapture(profile.parts, car.installed.slots);
                Check(legacy.refs.SequenceEqual(indexed.refs) && Equal(legacy.definitions, indexed.definitions) && legacy.copies.Select(p => p.instanceId).SequenceEqual(indexed.copies.Select(p => p.instanceId)), vehicle.spec.id + " " + size + " " + group + " timed helper preflight parity");
                var legacyTimes = new double[Repeats]; var indexTimes = new double[Repeats];
                var defsTimes = new double[Repeats]; var resolveTimes = new double[Repeats]; var inspectionTimes = new double[Repeats];
                for (int r = 0; r < Repeats; r++)
                {
                    legacyTimes[r] = Measure(() => { var ignored = LegacyCapture(profile.parts, car.installed.slots); });
                    indexTimes[r] = Measure(() => { var ignored = IndexedCapture(profile.parts, car.installed.slots); });
                    defsTimes[r] = Measure(() => MechanicsOwnership.Definitions(profile, car));
                    resolveTimes[r] = Measure(() => MechanicsOwnership.Resolve(profile, car, catalog));
                    inspectionTimes[r] = Measure(() => InspectionBuild.Capture(profile, car));
                }
                Timings.AppendLine(string.Join(",", new[] {
                    vehicle.spec.id, size.ToString(CultureInfo.InvariantCulture), car.installed.slots.Count.ToString(CultureInfo.InvariantCulture), group, Repeats.ToString(CultureInfo.InvariantCulture),
                    Median(legacyTimes).ToString("R", CultureInfo.InvariantCulture), legacyTimes.Max().ToString("R", CultureInfo.InvariantCulture),
                    Median(indexTimes).ToString("R", CultureInfo.InvariantCulture), indexTimes.Max().ToString("R", CultureInfo.InvariantCulture),
                    Median(defsTimes).ToString("R", CultureInfo.InvariantCulture), defsTimes.Max().ToString("R", CultureInfo.InvariantCulture),
                    Median(resolveTimes).ToString("R", CultureInfo.InvariantCulture), resolveTimes.Max().ToString("R", CultureInfo.InvariantCulture),
                    Median(inspectionTimes).ToString("R", CultureInfo.InvariantCulture), inspectionTimes.Max().ToString("R", CultureInfo.InvariantCulture)
                }));
            }
        }

        public static void RunParityStandalone()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying) throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            Checks.Length = 0; passed = 0;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S02B"));
            Directory.CreateDirectory(root);
            try
            {
                CheckLookupSurface();
                CheckAuthoredParity();
                Checks.AppendLine("RESULT PASS count=" + passed);
                UnityEngine.Debug.Log("S02b final-source installed lookup parity passed: " + passed);
            }
            catch (Exception error)
            {
                Checks.AppendLine("RESULT FAIL count=" + passed);
                Checks.AppendLine(error.ToString());
                throw;
            }
            finally { File.WriteAllText(Path.Combine(root, "installed-part-final-parity-checks.txt"), Checks.ToString(), new UTF8Encoding(false)); }
        }

        public static void RunStandalone()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying) throw new InvalidOperationException("S02b checks require Editor batch mode outside Play Mode.");
            Checks.Length = 0; Timings.Length = 0; passed = 0;
            Checks.AppendLine("S02b installed-part lookup; authored in-memory fixtures; no ProfileStore, JSON manipulation, full-profile clone, or player save access.");
            try
            {
                CheckLookupSurface();
                CheckAuthoredParity();
                Benchmark();
                Checks.AppendLine("BENCH rows=36 repeats=" + Repeats + " public_endpoints=Definitions,Resolve,InspectionBuild.Capture sample_max_is_not_percentile=true synthetic_padding_is_inventory_only=true");
                Checks.AppendLine("RESULT PASS count=" + passed.ToString(CultureInfo.InvariantCulture));
                WriteReports();
                UnityEngine.Debug.Log("S02b installed-part lookup checks passed: " + passed);
            }
            catch (Exception ex)
            {
                Checks.AppendLine("RESULT FAIL count=" + passed.ToString(CultureInfo.InvariantCulture)); Checks.AppendLine(ex.ToString());
                WriteReports(); UnityEngine.Debug.LogException(ex); throw;
            }
        }

        static void WriteReports()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S02B"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "installed-part-lookup-checks.txt"), Checks.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "installed-part-lookup-timings.csv"), Timings.ToString(), new UTF8Encoding(false));
        }
    }
#endif
}
