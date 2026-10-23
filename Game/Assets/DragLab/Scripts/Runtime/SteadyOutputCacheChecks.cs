using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    /// <summary>Independent equation parity, capture lifetime, dependency, and bound checks for S03a.</summary>
    public static class SteadyOutputCacheChecks
    {
        const int Repeats = 20;
        static readonly StringBuilder Report = new StringBuilder();
        static readonly StringBuilder Timings = new StringBuilder();
        static VehicleDefinition[] authored;
        static int passed;

        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("S03a steady-output check failed: " + name);
            passed++; Report.AppendLine("PASS " + name);
        }

        static VehicleSpec Spec(int index) => authored[index % authored.Length].spec.Copy();
        static VehicleSpec BoostedSpec()
        {
            var source = authored.FirstOrDefault(v => v.spec != null && v.spec.induction != null && v.spec.induction.factory != InductionType.NaturallyAspirated) ?? authored[0];
            return source.spec.Copy();
        }
        static Tune TuneFor(VehicleSpec spec, InductionType type)
        {
            var tune = new Tune { finalDrive = spec.finalDrive, launchRpm = 3200, shiftRpm = spec.redlineRpm - 200, partsResolved = true };
            tune.inductionUpgrade = type == InductionType.NaturallyAspirated ? EngineUpgradeInduction.Factory : (EngineUpgradeInduction)((int)type);
            tune.boostSetting = 1.08f; tune.turboSize = 1.12f; tune.superchargerPulley = 1.07f;
            return tune;
        }

        static InductionProfile Profile(VehicleSpec spec, InductionType type)
        {
            if (type == InductionType.NaturallyAspirated) spec.induction.factory = InductionType.NaturallyAspirated;
            spec.induction.torqueCurveIncludesFactoryInduction = true;
            return new InductionProfile(spec, TuneFor(spec, type));
        }

        static InductionProfile Profile(VehicleSpec spec, Tune tune)
        {
            spec.induction.torqueCurveIncludesFactoryInduction = true;
            return new InductionProfile(spec, tune);
        }

        static double[] RpmSamples(InductionProfile profile)
        {
            var curve = profile.vehicle.torque;
            var samples = new SortedSet<double> { profile.vehicle.idleRpm, (profile.vehicle.idleRpm + profile.vehicle.redlineRpm) * .5, profile.vehicle.redlineRpm };
            if (curve != null) foreach (var point in curve) samples.Add(point.rpm);
            return samples.ToArray();
        }

        static double[] InspectionRpmSamples(InductionProfile profile)
        {
            var samples = new List<double>();
            for (double rpm = profile.vehicle.idleRpm; rpm <= profile.vehicle.redlineRpm; rpm += 100) samples.Add(rpm);
            if (samples.Count == 0 || samples[samples.Count - 1] != profile.vehicle.redlineRpm) samples.Add(profile.vehicle.redlineRpm);
            return samples.ToArray();
        }

        static InspectionSteadyPoint Direct(InductionProfile profile, double rpm)
        {
            var state = InductionDynamics.SteadyOutput(profile, rpm);
            return new InspectionSteadyPoint { engineTorqueNm = state.engineTorqueNm, boostBar = state.boostBar };
        }

        static InspectionSteadyPoint Cached(Func<double, InspectionSteadyPoint> evaluator, double rpm) => evaluator(rpm);
        static bool Same(InspectionSteadyPoint a, InspectionSteadyPoint b) => a.engineTorqueNm == b.engineTorqueNm && a.boostBar == b.boostBar;
        static string F(double x) => x.ToString("R", CultureInfo.InvariantCulture);

        static void CheckProfilesAndParity()
        {
            var types = new[] { InductionType.NaturallyAspirated, InductionType.Turbo, InductionType.TwinTurbo, InductionType.Supercharger, InductionType.TwinSupercharger, InductionType.Twincharged };
            foreach (var type in types)
            {
                var spec = BoostedSpec(); var profile = Profile(spec, type);
                var cache = new NativeSteadyOutputCache(); var evaluate = cache.For(profile);
                foreach (double rpm in RpmSamples(profile))
                    Check(Same(Direct(profile, rpm), Cached(evaluate, rpm)), type + " exact torque/boost parity at RPM " + F(rpm));
                Check(cache.CachedEntries == 1 && cache.CachedPoints == RpmSamples(profile).Length, type + " bounded cache counts match evaluated points");
            }

            var dieselSource = authored.FirstOrDefault(v => v.spec.engineBehavior != null && v.spec.engineBehavior.fueling == FuelSystem.Diesel);
            var diesel = dieselSource == null ? Spec(0) : dieselSource.spec.Copy();
            if (dieselSource == null) { diesel.engineBehavior.fueling = FuelSystem.Diesel; diesel.combustion.fuelKind = FuelKind.Diesel; Report.AppendLine("CONTENT diesel=typed_variant_of_authored " + diesel.id); }
            else Report.AppendLine("CONTENT diesel=authored " + diesel.id);
            var dieselProfile = Profile(diesel, InductionType.Turbo); var dieselEval = new NativeSteadyOutputCache().For(dieselProfile);
            foreach (double rpm in RpmSamples(dieselProfile)) Check(Same(Direct(dieselProfile, rpm), dieselEval(rpm)), "typed diesel turbo exact torque/boost parity at RPM " + F(rpm));
        }

        static void CheckFrozenCaptureAndRelevantFreshInputs()
        {
            var spec = BoostedSpec(); var profile = Profile(spec, InductionType.Turbo);
            var rpms = RpmSamples(profile); double rpm = rpms[Math.Min(2, rpms.Length - 1)];
            var expected = Direct(profile, rpm);
            var cache = new NativeSteadyOutputCache(); var old = cache.For(profile);
            Check(Same(expected, old(rpm)), "initial delegate equals direct equation");
            int hits = cache.Hits;
            var equivalent = Profile(spec.Copy(), InductionType.Turbo);
            var second = cache.For(equivalent);
            Check(cache.CachedEntries == 1, "equivalent typed profile reuses same entry");
            Check(Same(expected, second(rpm)) && cache.Hits > hits, "same-key delegate records cache hit");

            double untouchedRpm = rpm + .125;
            var beforeFirstExpected = Direct(profile, untouchedRpm);
            var beforeFirst = new NativeSteadyOutputCache().For(profile);

            profile.vehicle.torque[1].torqueNm += 73;
            profile.data.turboBoostBar += .17f;
            profile.vehicle.induction.turboBoostBar += .17f;
            profile.tune.partPower += .12f;
            var mutatedExpected = Direct(profile, rpm);
            Check(Same(expected, old(rpm)), "old delegate remains frozen after torque/data/tune mutation following a cache hit");
            Check(Same(beforeFirstExpected, beforeFirst(untouchedRpm)), "cold curve/data/power delegate remains frozen after source mutation before its first evaluation");
            var fresh = cache.For(profile);
            Check(!Same(expected, mutatedExpected) && Same(mutatedExpected, fresh(rpm)), "fresh delegate reflects relevant torque/data/power edits");

            var captureSpec = BoostedSpec(); captureSpec.induction.factory = InductionType.Turbo;
            var inputProfile = Profile(captureSpec, InductionType.Twincharged);
            var captured = InductionDynamics.CaptureSteadyOutputInputs(inputProfile);
            var same = InductionDynamics.CaptureSteadyOutputInputs(Profile(inputProfile.vehicle.Copy(), InductionType.Twincharged));
            Check(captured.Equals(same) && same.Equals(captured) && captured.GetHashCode() == same.GetHashCode(), "typed captures compare equal and hash equally for independent equivalent inputs");
            var originalEval = captured.Evaluate(rpm);
            inputProfile.vehicle.torque[2].torqueNm += 41;
            inputProfile.vehicle.valvetrain.camTorqueBias += .12f;
            Check(captured.Evaluate(rpm).engineTorqueNm == originalEval.engineTorqueNm, "captured evaluator is independent of later source graph edits");
            var changed = InductionDynamics.CaptureSteadyOutputInputs(Profile(inputProfile.vehicle.Copy(), InductionType.Twincharged));
            Check(!captured.Equals(changed), "fresh typed capture key changes after relevant curve/cam edits");

            if (inputProfile.factoryReference != null)
            {
                var beforeFactory = InductionDynamics.CaptureSteadyOutputInputs(inputProfile);
                var factoryExpected = Direct(inputProfile, rpm);
                var factoryCache = new NativeSteadyOutputCache(); var factoryOld = factoryCache.For(inputProfile);
                inputProfile.factoryReference.data.turboBoostBar += .2f;
                var factoryChangedDirect = Direct(inputProfile, rpm);
                Check(Same(factoryExpected, factoryOld(rpm)), "old delegate freezes factory-reference data before its first evaluation");
                var afterFactory = InductionDynamics.CaptureSteadyOutputInputs(inputProfile);
                Check(!beforeFactory.Equals(afterFactory), "factory-reference induction graph participates in typed key");
                var factoryFresh = factoryCache.For(inputProfile);
                Check(!Same(factoryExpected, factoryChangedDirect) && Same(factoryChangedDirect, factoryFresh(rpm)), "fresh delegate reflects factory-reference induction edit");
            }

            var divergence = Profile(captureSpec.Copy(), InductionType.Twincharged);
            var divergenceInput = InductionDynamics.CaptureSteadyOutputInputs(divergence);
            var divergenceOutput = Direct(divergence, rpm);
            divergence.vehicle.induction.turboBoostBar += .3f;
            Check(divergenceInput.Equals(InductionDynamics.CaptureSteadyOutputInputs(divergence)) && Same(divergenceOutput, Direct(divergence, rpm)), "vehicle induction source edits do not replace captured profile.data inputs");
        }

        static void CheckDependencyAndExclusionInputs()
        {
            Func<InductionProfile, Action>[] changes =
            {
                p => () => p.vehicle.torque[1].torqueNm += 11,
                p => () => p.vehicle.fuelPowerCapacityHp += 17,
                p => () => p.vehicle.ignitionPowerCapacityHp += 13,
                p => () => p.vehicle.engineBehavior.fueling = p.vehicle.engineBehavior.fueling == FuelSystem.Diesel ? FuelSystem.Injection : FuelSystem.Diesel,
                p => () => p.vehicle.valvetrain.camTorqueBias += .1f,
                p => () => p.vehicle.valvetrain.camTimingDegrees += .5f,
                p => () => p.vehicle.combustion.compressionRatio += .25f,
                p => () => p.data.maxBoostBar -= .1f,
                p => () => p.tune.partPower += .1f,
                p => () => p.data.factory = p.data.factory == InductionType.Turbo ? InductionType.NaturallyAspirated : InductionType.Turbo,
                p => () => p.data.turboBoostBar += .13f
            };
            int i = 0;
            foreach (var changeFactory in changes)
            {
                var candidateSpec = BoostedSpec(); candidateSpec.induction.factory = InductionType.Turbo;
                var candidate = Profile(candidateSpec, InductionType.Twincharged);
                var before = InductionDynamics.CaptureSteadyOutputInputs(candidate);
                changeFactory(candidate)();
                var after = InductionDynamics.CaptureSteadyOutputInputs(candidate);
                Check(!before.Equals(after), "typed key changes for relevant input " + i++);
            }

            var profileSpec = BoostedSpec(); profileSpec.induction.factory = InductionType.Turbo;
            var baseline = Profile(profileSpec.Copy(), TuneFor(profileSpec, InductionType.Twincharged));
            var baselineKey = InductionDynamics.CaptureSteadyOutputInputs(baseline);
            foreach (var change in new Action<Tune>[] {
                t => t.inductionUpgrade = EngineUpgradeInduction.Turbo,
                t => t.turboSize += .1f,
                t => t.boostSetting += .1f,
                t => t.superchargerPulley += .1f,
                t => t.partPower += .1f
            })
            {
                var tune = TuneFor(profileSpec, InductionType.Twincharged); change(tune);
                var variant = Profile(profileSpec.Copy(), tune);
                Check(!baselineKey.Equals(InductionDynamics.CaptureSteadyOutputInputs(variant)), "computed induction profile/tune output input changes key");
            }

            var unrelatedSpec = BoostedSpec(); unrelatedSpec.induction.factory = InductionType.Turbo;
            var unrelated = Profile(unrelatedSpec, InductionType.Twincharged);
            var beforeUnrelated = InductionDynamics.CaptureSteadyOutputInputs(unrelated);
            unrelated.vehicle.displayName += " renamed"; unrelated.vehicle.massKg += 50; unrelated.vehicle.tireFriction += .2f;
            unrelated.tune.tirePressurePsi += 4; unrelated.tune.finalDrive += .2f;
            var afterUnrelated = InductionDynamics.CaptureSteadyOutputInputs(unrelated);
            Check(beforeUnrelated.Equals(afterUnrelated), "chassis/name/tire/final-drive edits are excluded from steady-output key");
            var unrelatedCache = new NativeSteadyOutputCache(); var f1 = unrelatedCache.For(unrelated);
            unrelated.vehicle.displayName = "different profile name"; unrelated.vehicle.massKg += 10;
            var f2 = unrelatedCache.For(unrelated);
            Check(unrelatedCache.CachedEntries == 1 && Same(f1(3200), f2(3200)), "unrelated profile context retains same cache entry and output");
        }

        static void CheckBoundsAndDelegateLifetime()
        {
            var profile = Profile(BoostedSpec(), InductionType.Turbo);
            var cache = new NativeSteadyOutputCache(); var first = cache.For(profile);
            for (int i = 0; i < NativeSteadyOutputCache.PointLimit + 17; i++) first(1000 + i * .25);
            Check(cache.CachedPoints <= NativeSteadyOutputCache.PointLimit, "per-entry point FIFO remains within configured point limit");
            Check(cache.CachedEntries == 1, "point overflow does not add entries");
            int beforeRetainedHit = cache.Hits, beforeEvictedMiss = cache.Misses;
            first(1000 + (NativeSteadyOutputCache.PointLimit + 16) * .25);
            Check(cache.Hits == beforeRetainedHit + 1, "newest point remains a FIFO cache hit");
            first(1000);
            Check(cache.Misses == beforeEvictedMiss + 1, "oldest evicted point is recomputed on demand");
            var direct = Direct(profile, 6000); Check(Same(direct, first(6000)), "delegate remains usable after point eviction");

            Func<double, InspectionSteadyPoint> evicted = first;
            for (int i = 0; i < NativeSteadyOutputCache.Limit + 3; i++)
            {
                var spec = Spec(i % authored.Length); spec.torque[1].torqueNm += 3 + i;
                var unique = Profile(spec, (InductionType)(i % 6)); cache.For(unique);
            }
            Check(cache.CachedEntries == NativeSteadyOutputCache.Limit, "outer LRU stays within configured entry limit");
            Check(cache.CachedPoints <= NativeSteadyOutputCache.Limit * NativeSteadyOutputCache.PointLimit, "aggregate retained points remain bounded by entry and point limits");
            Check(Same(Direct(profile, 4200), evicted(4200)), "evicted delegate retains only its private bounded input and remains callable");

            var parallel = cache.For(Profile(Spec(0), InductionType.TwinTurbo));
            var expected = Direct(Profile(Spec(0), InductionType.TwinTurbo), 4300);
            var jobs = Enumerable.Range(0, 8).Select(_ => Task.Run(() => { for (int n = 0; n < 200; n++) if (!Same(expected, parallel(4300))) throw new InvalidOperationException("parallel result mismatch"); })).ToArray();
            Task.WaitAll(jobs);
            Check(true, "private evaluator concurrent reads return stable exact outputs");
        }

        static double Median(double[] values)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
        }
        static double Time(Action action) { var watch = Stopwatch.StartNew(); action(); watch.Stop(); return watch.Elapsed.TotalMilliseconds; }
        static void Benchmark()
        {
            var profile = Profile(BoostedSpec(), InductionType.Twincharged); var samples = InspectionRpmSamples(profile);
            var capture = new double[Repeats]; var direct = new double[Repeats]; var cold = new double[Repeats]; var hit = new double[Repeats]; var held = new double[Repeats];
            Timings.AppendLine("profile,rpm_points,repeats,pair,order,capture_only_ms,direct_full_curve_ms,for_plus_cold_full_curve_ms,for_plus_hit_full_curve_ms,held_delegate_hit_full_curve_ms");
            for (int r = 0; r < Repeats; r++)
            {
                var warmCache = new NativeSteadyOutputCache(); var warmEvaluator = warmCache.For(profile);
                foreach (var rpm in samples) warmEvaluator(rpm);
                capture[r] = Time(() => InductionDynamics.CaptureSteadyOutputInputs(profile));
                if ((r & 1) == 0)
                {
                    direct[r] = Time(() => { foreach (var rpm in samples) Direct(profile, rpm); });
                    var coldCache = new NativeSteadyOutputCache();
                    cold[r] = Time(() => { var evaluator = coldCache.For(profile); foreach (var rpm in samples) evaluator(rpm); });
                    hit[r] = Time(() => { var evaluator = warmCache.For(profile); foreach (var rpm in samples) evaluator(rpm); });
                    held[r] = Time(() => { foreach (var rpm in samples) warmEvaluator(rpm); });
                }
                else
                {
                    held[r] = Time(() => { foreach (var rpm in samples) warmEvaluator(rpm); });
                    hit[r] = Time(() => { var evaluator = warmCache.For(profile); foreach (var rpm in samples) evaluator(rpm); });
                    var coldCache = new NativeSteadyOutputCache();
                    cold[r] = Time(() => { var evaluator = coldCache.For(profile); foreach (var rpm in samples) evaluator(rpm); });
                    direct[r] = Time(() => { foreach (var rpm in samples) Direct(profile, rpm); });
                }
                Timings.AppendLine(string.Join(",", new[] { "twincharged", samples.Length.ToString(CultureInfo.InvariantCulture), Repeats.ToString(CultureInfo.InvariantCulture), r.ToString(CultureInfo.InvariantCulture), (r & 1) == 0 ? "direct_then_cache" : "cache_then_direct", capture[r].ToString("R", CultureInfo.InvariantCulture), direct[r].ToString("R", CultureInfo.InvariantCulture), cold[r].ToString("R", CultureInfo.InvariantCulture), hit[r].ToString("R", CultureInfo.InvariantCulture), held[r].ToString("R", CultureInfo.InvariantCulture) }));
            }
            Report.AppendLine("TIMING profile=twincharged rpm_points=" + samples.Length + " repeats=" + Repeats + " capture_median_ms=" + F(Median(capture)) + " capture_sample_max_ms=" + F(capture.Max()) + " direct_curve_median_ms=" + F(Median(direct)) + " direct_curve_sample_max_ms=" + F(direct.Max()) + " For_plus_cold_curve_median_ms=" + F(Median(cold)) + " For_plus_cold_curve_sample_max_ms=" + F(cold.Max()) + " For_plus_hit_curve_median_ms=" + F(Median(hit)) + " For_plus_hit_curve_sample_max_ms=" + F(hit.Max()) + " held_delegate_median_ms=" + F(Median(held)) + " held_delegate_sample_max_ms=" + F(held.Max()) + " alternating_orders=true descriptive_only=true no_allocation_claim=true");
        }

        public static void RunStandalone()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying) throw new InvalidOperationException("S03a checks require Editor batch mode outside Play Mode.");
            Report.Length = 0; Timings.Length = 0; passed = 0;
            Report.AppendLine("S03a typed steady-output capture/cache; direct InductionDynamics.SteadyOutput oracle; no JSON/profile/save operations.");
            try
            {
                authored = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
                if (authored == null || authored.Length == 0) throw new InvalidOperationException("S03a requires authored vehicle definitions.");
                var basis = BoostedSpec();
                Report.AppendLine("CONTENT authored_vehicles=" + authored.Length + " base_id=" + basis.id + " base_factory=" + basis.induction.factory + " base_curve_points=" + (basis.torque == null ? 0 : basis.torque.Length) + " types_tested=NA,turbo,twin_turbo,supercharger,twin_supercharger,twincharged");
                CheckProfilesAndParity(); CheckFrozenCaptureAndRelevantFreshInputs(); CheckDependencyAndExclusionInputs(); CheckBoundsAndDelegateLifetime(); Benchmark();
                Report.AppendLine("RESULT PASS checks=" + passed); WriteReports(); UnityEngine.Debug.Log("S03a steady-output checks passed: " + passed);
            }
            catch (Exception ex)
            {
                Report.AppendLine("RESULT FAIL checks=" + passed); Report.AppendLine(ex.ToString()); WriteReports(); UnityEngine.Debug.LogException(ex); throw;
            }
        }

        static void WriteReports()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/save-refactor/S03A"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "steady-output-cache-checks.txt"), Report.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "steady-output-cache-timings.csv"), Timings.ToString(), new UTF8Encoding(false));
        }
    }
#endif
}
