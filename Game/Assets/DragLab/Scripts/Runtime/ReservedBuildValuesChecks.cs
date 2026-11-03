#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    /// <summary>Independent Editor-only characterization of the R1 reserved-build value boundary.</summary>
    public static class ReservedBuildValuesChecks
    {
        const uint Seed = 173031u;
        static readonly List<string> checks = new List<string>();
        static readonly List<string> trace = new List<string>();
        static int failures;

        // Unity executeMethod entry point. Root owns launching this batch.
        public static void RunBatch()
        {
            string folder = Argument("-draglabR1ReportFolder") ?? Path.Combine("reports", "save-refactor", "S06D2B1L1DR1",
                "reservation-values-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
            Run((ok, message) => Record(ok, message), folder);
            if (failures != 0) EditorApplication.Exit(1);
        }

        static void Run(Action<bool, string> check, string folder)
        {
            checks.Clear(); trace.Clear(); failures = 0;
            try
            {
                Case("R1-01-created-hash-first", () => CreatedHashFirst(check));
                Case("R1-02-latent-malformed-fallback", () => LatentMalformedFallback(check));
                Case("R1-03-context-and-owned-copies", () => ContextAndCopies(check));
                Case("R1-04-hash-only-and-slot-label", () => HashOnlyAndSlotLabel(check));
                Case("R1-05-fallback-restoration", () => FallbackRestoration(check));
                Case("R1-06-text-stale-absent", () => TextStaleAbsent(check));
                Case("R1-07-field-perturbations", () => FieldPerturbations(check));
                Case("R1-08-null-empty-order-shapes", () => NullEmptyOrderShapes(check));
                Case("R1-09-element-array-runtime-type", () => ElementArrayRuntimeType(check));
                Case("R1-10-inline-json-roundtrip", () => InlineJsonRoundtrip(check));
                Case("R1-11-numeric-controls", () => NumericControls(check));
                Case("R1-12-source-and-global-invariance", () => SourceAndGlobalInvariance(check));
                Case("R1-13-AWD-clutch-value-fields", () => StructuralValueFieldPerturbations(check));
                Case("R1-14-available-element-runtime-types", () => ElementRuntimeRegistry(check));
            }
            catch (Exception error)
            {
                failures++;
                trace.Add("RUNNER_EXCEPTION|" + error.GetType().FullName + "|" + error.Message);
                Write(Path.Combine(folder, "runner-error.txt"), error.ToString());
            }
            try
            {
                Directory.CreateDirectory(folder);
                Write(Path.Combine(folder, "checks.txt"), string.Join("\n", checks) + "\nSUMMARY|checks=" + checks.Count + "|failures=" + failures);
                Write(Path.Combine(folder, "trace.txt"), string.Join("\n", trace));
                if (failures == 0) Write(Path.Combine(folder, "runner-error.txt"), "");
            }
            catch (Exception error)
            {
                failures++;
                try { Directory.CreateDirectory(folder); Write(Path.Combine(folder, "runner-error.txt"), error.ToString()); } catch { }
            }
        }

        static void CreatedHashFirst(Action<bool, string> check)
        {
            Fixture f = Create();
            var job = f.Profile.business.jobs[0];
            bool legacy = LegacyReference.Matches(f.Profile, f.Car, job);
            var imported = LegacyReservedBuildImport.FromLegacy(f.Profile, f.Car, job);
            var current = ReservedBuildValues.CaptureCreated(f.Profile, f.Car);
            var outcome = imported.MatchCurrent(current, Labels(f.Car), job.snapshotJson);
            check(legacy, "R1-01 legacy unchanged assigned job must match");
            check(outcome.Kind == ReservedBuildMatchKind.HashMatched, "R1-01 unchanged import must choose hash baseline");
            check(imported.FallbackKind == ReservedBuildFallbackKind.Values && imported.HashBaseline != null,
                "R1-01 must retain both the exact hash baseline and decoded expectation");
            trace.Add("R1-01|kind=" + outcome.Kind + "|fallback=" + imported.FallbackKind + "|legacy=" + legacy);
        }

        static void LatentMalformedFallback(Action<bool, string> check)
        {
            Fixture f = Create();
            var job = f.Profile.business.jobs[0];
            job.snapshotJson = "{"; // malformed only in this disposable diagnostic fixture
            job.snapshot = LegacyReference.Snapshot(f.Profile, f.Car);
            var imported = LegacyReservedBuildImport.FromLegacy(f.Profile, f.Car, job);
            var current = ReservedBuildValues.CaptureCreated(f.Profile, f.Car);
            check(imported.HashBaseline != null && imported.FallbackKind == ReservedBuildFallbackKind.Error,
                "R1-02 import must preserve the hash authority and retain malformed fallback metadata");
            check(imported.MatchCurrent(current, Labels(f.Car), job.snapshotJson).Kind == ReservedBuildMatchKind.HashMatched,
                "R1-02 matching hash must short-circuit malformed fallback");
            f.Car.paintHex = (f.Car.paintHex ?? "") + "-changed";
            var outcome = imported.MatchCurrent(ReservedBuildValues.CaptureCreated(f.Profile, f.Car), Labels(f.Car), job.snapshotJson);
            check(outcome.Kind == ReservedBuildMatchKind.FallbackError && outcome.ErrorType == imported.ErrorType,
                "R1-02 after hash miss the stored malformed fallback error must surface");
            var decode=LegacyReference.CaptureDecodeError(job.snapshotJson);
            check(outcome.ErrorType==decode.Type&&outcome.ErrorMessage==decode.Message&&outcome.ErrorParameterName==decode.Parameter,
                "R1-02 fallback failure metadata matches independent Unity decode error");
            var legacy=LegacyReference.CaptureMatches(f.Profile,f.Car,job);
            check(!legacy.Matched&&legacy.ErrorType==decode.Type&&legacy.ErrorMessage==decode.Message&&legacy.ErrorParameterName==decode.Parameter,
                "R1-02 frozen old matcher exposes the same exact malformed-payload exception");
            trace.Add("R1-02|error=" + outcome.ErrorType + "|message=" + outcome.ErrorMessage);
        }

        static void ContextAndCopies(Action<bool, string> check)
        {
            Fixture f = Create();
            var job = f.Profile.business.jobs[0];
            var context = LegacyReservedBuildImport.FromLegacyOwner(f.Profile);
            check(context.TryGet(job.id, job.carId, job.snapshot, out var result), "R1-03 exact owner/job/car/hash key must resolve");
            check(!context.TryGet(job.id, job.carId + "-wrong", job.snapshot, out _), "R1-03 wrong car key must not resolve");
            var value = ReservedBuildValues.CaptureCreated(f.Profile, f.Car);
            var copy = value.DefensiveCopy();
            var partCopies = value.PartsCopy();
            if (partCopies != null && partCopies.Length > 0 && partCopies[0] != null)
                partCopies[0].instanceId = "mutated-copy";
            var fuel = value.FuelCopy(); if (fuel != null) fuel.litres += 123;
            check(value.MatchesCurrent(ReservedBuildValues.CaptureCreated(f.Profile, f.Car), ReservedBuildComparisonMode.ExactHashBaseline),
                "R1-03 returned diagnostic copies must not mutate owned value");
            check(copy.MatchesCurrent(value, ReservedBuildComparisonMode.ExactHashBaseline), "R1-03 defensive copy retains exact values");
            check(result.MatchCurrent(value, Labels(f.Car), job.snapshotJson).Kind == ReservedBuildMatchKind.HashMatched,
                "R1-03 context result retains immutable hash baseline");
            var created=ReservedBuildContext.FromCreatedJobs(f.Profile,new[]{job});
            check(created.TryGet(job.id,job.carId,job.snapshot,out var createdResult)
                &&createdResult.Kind==ReservedBuildImportKind.CreatedTyped
                &&createdResult.MatchCurrent(value,Labels(f.Car),job.snapshotJson).Kind==ReservedBuildMatchKind.HashMatched,
                "R1-03 newly created job context stores typed values and exact key");
            string originalPaint=f.Car.paintHex;f.Car.paintHex=originalPaint+"-source-mutation";
            check(!copy.MatchesCurrent(ReservedBuildValues.CaptureCreated(f.Profile,f.Car),ReservedBuildComparisonMode.ExactHashBaseline),
                "R1-03 captured owned value is independent after caller source mutation");
            f.Car.paintHex=originalPaint;
            var slotCopy=result.HashBaseline.OrderedSlotLabelsCopy();
            if(slotCopy!=null&&slotCopy.Length>0)slotCopy[0]="mutated-returned-slot";
            check(result.HashBaseline.MatchesCurrent(value,Labels(f.Car)),"R1-03 hash baseline labels are returned defensively");
        }

        static void HashOnlyAndSlotLabel(Action<bool, string> check)
        {
            Fixture f = Create(); var job = f.Profile.business.jobs[0]; job.snapshotJson = null;
            job.snapshot = LegacyReference.Snapshot(f.Profile, f.Car);
            var imported = LegacyReservedBuildImport.FromLegacy(f.Profile, f.Car, job);
            var slots = Labels(f.Car);
            var old = f.Car.installed.slots[0].slot;
            f.Car.installed.slots[0].slot = old + "-changed";
            var changed = ReservedBuildValues.CaptureCreated(f.Profile, f.Car);
            check(imported.HashBaseline != null && !imported.HashBaseline.MatchesCurrent(changed, Labels(f.Car)),
                "R1-04 separate ordered slot labels participate in the exact hash baseline");
            check(imported.MatchCurrent(changed, Labels(f.Car), null).Kind == ReservedBuildMatchKind.FallbackAbsent,
                "R1-04 hash miss with absent payload has no fallback authority");
            check(!LegacyReference.Matches(f.Profile, f.Car, job), "R1-04 legacy hash-only slot change rejects");
            trace.Add("R1-04|beforeSlots=" + string.Join(",", slots) + "|after=" + string.Join(",", Labels(f.Car)));
        }

        static void FallbackRestoration(Action<bool, string> check)
        {
            Fixture f = Create(); var job = f.Profile.business.jobs[0];
            string originalSnapshot=job.snapshot,originalJson=job.snapshotJson;
            try
            {
                string payload="\n"+job.snapshotJson;
                var decodedCurrent=JsonUtility.FromJson<ReservedBuildSnapshot>(payload);
                job.snapshot="deliberate-hash-miss";
                job.snapshotJson=payload;
                var imported = LegacyReservedBuildImport.FromLegacy(f.Profile, f.Car, job);
                var originalCurrent=ReservedBuildValues.CaptureCreated(f.Profile,f.Car);
                var initial = imported.MatchCurrent(originalCurrent, Labels(f.Car), payload);
                bool oldActualMatch=LegacyReference.Matches(f.Profile,f.Car,job);
                bool oldDecodedSerialized=LegacyReference.SerializedContents(payload,decodedCurrent);
                var decodedValues=ReservedBuildValues.FromSnapshot(decodedCurrent);
                var restored = imported.MatchCurrent(decodedValues, Labels(f.Car), payload);
                check(imported.JsonTextBaseline==null,"R1-05 prefixed payload prevents an exact-text baseline for this decoded-content diagnostic");
                check(initial.Kind == ReservedBuildMatchKind.Mismatch, "R1-05 original owner projection must remain a mismatch against decoded payload");
                check(oldDecodedSerialized,"R1-05 independent old Serialized succeeds for the directly decoded current-content value");
                check(restored.Kind == ReservedBuildMatchKind.JsonExpectedMatched,
                    "R1-05 same import result matches independently decoded current contents without recapture");
                check(!oldActualMatch,"R1-05 preserve observed legacy inline-JSON ghost on the original owner profile");
                trace.Add("R1-05|initial="+initial.Kind+"|decoded="+restored.Kind+"|legacyOriginalOwnerMatches="+oldActualMatch
                    +"|legacySerializedDecoded="+oldDecodedSerialized);
            }
            finally { job.snapshot=originalSnapshot;job.snapshotJson=originalJson; }
        }

        static void TextStaleAbsent(Action<bool, string> check)
        {
            Fixture textFixture=Create();var textJob=textFixture.Profile.business.jobs[0];
            textJob.snapshot="no-hash-match";
            var textResult=LegacyReservedBuildImport.FromLegacy(textFixture.Profile,textFixture.Car,textJob);
            var textOutcome=textResult.MatchCurrent(ReservedBuildValues.CaptureCreated(textFixture.Profile,textFixture.Car),Labels(textFixture.Car),textJob.snapshotJson);
            check(textOutcome.Kind==ReservedBuildMatchKind.JsonTextMatched,"R1-06 exact serialized text authority must report JsonTextMatched");
            Fixture staleFixture=Create();var staleJob=staleFixture.Profile.business.jobs[0];
            staleJob.snapshot="no-hash-match";
            var stale=LegacyReservedBuildImport.FromLegacy(staleFixture.Profile,staleFixture.Car,staleJob);
            var staleOutcome=stale.MatchCurrent(ReservedBuildValues.CaptureCreated(staleFixture.Profile,staleFixture.Car),Labels(staleFixture.Car),staleJob.snapshotJson+" ");
            check(staleOutcome.Kind==ReservedBuildMatchKind.StalePayload,"R1-06 changed payload bytes after import must be stale");
            Fixture absent=Create();var absentJob=absent.Profile.business.jobs[0];absentJob.snapshot="no-hash-match";absentJob.snapshotJson=null;
            var absentResult=LegacyReservedBuildImport.FromLegacy(absent.Profile,absent.Car,absentJob);
            var absentOutcome=absentResult.MatchCurrent(ReservedBuildValues.CaptureCreated(absent.Profile,absent.Car),Labels(absent.Car),null);
            check(absentOutcome.Kind==ReservedBuildMatchKind.FallbackAbsent,"R1-06 missing JSON reports absent fallback");
            check(!LegacyReference.Matches(absent.Profile,absent.Car,absentJob),"R1-06 frozen old matcher rejects hash miss without JSON");
        }

        static void InlineJsonRoundtrip(Action<bool, string> check)
        {
            Fixture f = Create(); var car = f.Car; var job = f.Profile.business.jobs[0];
            string text = JsonUtility.ToJson(new ReservedBuildSnapshot { fuel=car.fuel, fluids=car.fluids, tune=car.tune,
                paint=car.paintHex, parts=car.installed.slots.OrderBy(s=>s.partInstanceId,StringComparer.Ordinal)
                    .Select(s=>f.Profile.parts.Find(p=>p.instanceId==s.partInstanceId)).ToArray() });
            var decoded = JsonUtility.FromJson<ReservedBuildSnapshot>(text);
            bool legacy = LegacyReference.Same(LegacyReference.Contents(f.Profile, car), decoded, "");
            var value = ReservedBuildValues.FromSnapshot(decoded);
            var source = ReservedBuildValues.CaptureCreated(f.Profile, car);
            bool typed = value.MatchesCurrent(source, ReservedBuildComparisonMode.LegacyJsonFallback);
            check(legacy==typed, "R1-10 typed comparison result equals independent legacy Same for ordinary inline roundtrip");
            trace.Add("R1-10-ordinary|serializedLength=" + text.Length + "|legacySame=" + legacy + "|typedSame=" + typed);

            var oldFuel=car.fuel;string oldSnapshot=job.snapshot,oldJson=job.snapshotJson;
            try
            {
                car.fuel=null;
                var nullableContents=LegacyReference.Contents(f.Profile,car);
                string nullableText=JsonUtility.ToJson(nullableContents);
                var nullableDecoded=JsonUtility.FromJson<ReservedBuildSnapshot>(nullableText);
                bool nullableLegacy=LegacyReference.Same(nullableContents,nullableDecoded,"");
                bool nullableTyped=ReservedBuildValues.FromSnapshot(nullableDecoded).MatchesCurrent(
                    ReservedBuildValues.FromSnapshot(nullableContents),ReservedBuildComparisonMode.LegacyJsonFallback);
                check(nullableLegacy==nullableTyped,"R1-10 nullable inline decoded comparison matches independent observed legacy Same outcome");
                check(LegacyReference.SerializedContents(nullableText,nullableContents),
                    "R1-10 legacy exact JSON text branch accepts the actual nullable fixture serialization");
                job.snapshot="forced-hash-miss-for-exact-text";job.snapshotJson=nullableText;
                var imported=LegacyReservedBuildImport.FromLegacy(f.Profile,car,job);
                var outcome=imported.MatchCurrent(ReservedBuildValues.CaptureCreated(f.Profile,car),Labels(car),nullableText);
                check(outcome.Kind==ReservedBuildMatchKind.JsonTextMatched,
                    "R1-10 exact-text import branch succeeds with a nullable inline field");
                check(LegacyReference.Matches(f.Profile,car,job),"R1-10 old matcher succeeds on exact nullable serialized text");
                trace.Add("R1-10-nullable|sourceFuelNull="+(nullableContents.fuel==null)+"|decodedFuelNull="+(nullableDecoded.fuel==null)
                    +"|legacySame="+nullableLegacy+"|typedSame="+nullableTyped+"|exactText="+outcome.Kind+"|textLength="+nullableText.Length);
            }
            finally { car.fuel=oldFuel;job.snapshot=oldSnapshot;job.snapshotJson=oldJson; }
        }

        static void FieldPerturbations(Action<bool,string> check)
        {
            Fixture f=Create();
            var original=LegacyReference.Contents(f.Profile,f.Car);
            var paths=new List<List<PathPart>>();
            var nullComplex=new List<string>();var reachedComplexTypes=new SortedSet<string>(StringComparer.Ordinal);
            CollectPaths(original,new List<PathPart>(),new HashSet<object>(ReferenceEqualityComparer.Instance),paths,nullComplex,reachedComplexTypes,0);
            var baseline=ReservedBuildValues.FromSnapshot(original);
            int exercised=0,cloneBaselines=0,unsupported=0;
            foreach(var path in paths)
            {
                object prior=ReadPath(original,path);
                if(!TryChanged(prior,PathType(original,path),out object changed))
                { unsupported++;check(false,"R1-07 required reachable scalar path has no mutation operator: "+String.Join(".",path.Select(p=>p.Field?.Name??"["+p.Index+"]")));continue; }
                var altered=CloneSnapshot(original);
                bool untouchedClone=LegacyReference.Same(original,altered,"");
                check(untouchedClone,"R1-07 bounded clone independently matches before field mutation");
                if(!untouchedClone)continue;
                cloneBaselines++;
                if(!SetPath(altered,path,changed))
                { check(false,"R1-07 supported field path can be changed: "+String.Join(".",path.Select(p=>p.Field?.Name??"["+p.Index+"]")));continue; }
                object changedValue=ReadPath(altered,path);
                string fieldName=path.Last().Field?.Name??"";
                check(!LegacyReference.Same(prior,changedValue,fieldName),"R1-07 selected source field changes at "+fieldName);
                bool legacySame=LegacyReference.Same(original,altered,"");
                var typed=ReservedBuildValues.FromSnapshot(altered);
                bool typedSame=baseline.MatchesCurrent(typed,ReservedBuildComparisonMode.LegacyJsonFallback);
                string label=String.Join(".",path.Select(p=>p.Field!=null?p.Field.Name:"["+p.Index+"]"));
                check(!legacySame,"R1-07 independent old field comparator detects change at "+label);
                check(!typedSame,"R1-07 owned typed fallback detects change at "+label);
                exercised++;
            }
            check(exercised>0,"R1-07 actual normalized reservation fixture exposes supported scalar fields");
            check(exercised==paths.Count&&unsupported==0,"R1-07 every collected scalar path was baseline-checked and independently mutated");
            trace.Add("R1-07|fieldPaths="+paths.Count+"|cloneBaselines="+cloneBaselines+"|mutations="+exercised+"|unsupported="+unsupported
                +"|nullComplexPaths="+nullComplex.Count+"|nullComplex="+String.Join(",",nullComplex)
                +"|reachedComplexTypes="+String.Join(",",reachedComplexTypes));
        }

        static void StructuralValueFieldPerturbations(Action<bool,string> check)
        {
            Fixture f=Create();var structural=CloneSnapshot(LegacyReference.Contents(f.Profile,f.Car));
            var authoredAwd=f.Profile.catalog.parts.FirstOrDefault(d=>d?.awdHardware?.calibration!=null);
            check(authoredAwd!=null,"R1-13 actual authored AWD calibration exists in the catalog");
            if(authoredAwd==null)return;
            check(structural.tune!=null,"R1-13 normalized contents has a Tune value to anchor AWD fields");
            if(structural.tune==null)return;
            structural.tune.awd=authoredAwd.awdHardware.calibration.Copy();
            var clutchPart=structural.parts.FirstOrDefault(p=>p!=null&&f.Profile.catalog.Find(p.definitionId)?.category==PartCategory.Clutch);
            check(clutchPart!=null&&clutchPart.condition!=null,"R1-13 actual reserved clutch part/condition anchors a value-only clutch state");
            if(clutchPart==null||clutchPart.condition==null)return;
            int clutchIndex=Array.FindIndex(structural.parts,p=>p!=null&&p.instanceId==clutchPart.instanceId);
            check(clutchIndex>=0,"R1-13 actual clutch part is present in ordered reserved contents");
            if(clutchIndex<0)return;
            structural.parts[clutchIndex].condition.clutch=new ClutchConditionState{version=1,oilLitres=.87,oilCondition=.93};

            var cleanClone=CloneSnapshot(structural);
            check(LegacyReference.Same(structural,cleanClone,""),"R1-13 isolated AWD/clutch value graph has a clean independent-clone baseline");
            var baseline=ReservedBuildValues.FromSnapshot(structural);
            var paths=new List<List<PathPart>>();var nullComplex=new List<string>();var reached=new SortedSet<string>(StringComparer.Ordinal);
            CollectPaths(structural,new List<PathPart>(),new HashSet<object>(ReferenceEqualityComparer.Instance),paths,nullComplex,reached,0);
            var awdPaths=paths.Where(path=>path.Any(part=>part.Field!=null&&part.Field.DeclaringType==typeof(AwdSpec))).ToList();
            var clutchPaths=paths.Where(path=>path.Any(part=>part.Field!=null&&part.Field.DeclaringType==typeof(ClutchConditionState))).ToList();
            check(awdPaths.Count==16,"R1-13 all 16 public AwdSpec scalar fields are reachable from actual authored calibration");
            check(clutchPaths.Count==3,"R1-13 all 3 public ClutchConditionState fields are reachable on copied real clutch part");
            int mutations=ExercisePaths(check,structural,baseline,awdPaths,"AWD");
            mutations+=ExercisePaths(check,structural,baseline,clutchPaths,"clutch");
            trace.Add("R1-13|awdDefinition="+authoredAwd.id+"|clutchPart="+clutchPart.instanceId+"|awdFields="+awdPaths.Count
                +"|clutchFields="+clutchPaths.Count+"|mutations="+mutations+"|originOwnerMutated=false|newIdentity=false");
        }

        static int ExercisePaths(Action<bool,string> check,ReservedBuildSnapshot original,ReservedBuildValues baseline,List<List<PathPart>> paths,string label)
        {
            int done=0;
            foreach(var path in paths)
            {
                object prior=ReadPath(original,path);
                if(!TryChanged(prior,PathType(original,path),out object changed))
                { check(false,"R1-13 "+label+" field has no mutation operator: "+PathLabel(path));continue; }
                var mutated=CloneSnapshot(original);
                bool clean=LegacyReference.Same(original,mutated,"");
                check(clean,"R1-13 "+label+" bounded clone compares equal before mutation: "+PathLabel(path));
                if(!clean)continue;
                if(!SetPath(mutated,path,changed)){check(false,"R1-13 "+label+" field setter failed: "+PathLabel(path));continue;}
                check(!LegacyReference.Same(prior,ReadPath(mutated,path),path.Last().Field?.Name??""),"R1-13 "+label+" selected value changed: "+PathLabel(path));
                check(!LegacyReference.Same(original,mutated,""),"R1-13 frozen Same rejects changed "+label+" field: "+PathLabel(path));
                check(!baseline.MatchesCurrent(ReservedBuildValues.FromSnapshot(mutated),ReservedBuildComparisonMode.LegacyJsonFallback),
                    "R1-13 typed fallback rejects changed "+label+" field: "+PathLabel(path));
                done++;
            }
            check(done==paths.Count,"R1-13 all "+label+" value paths were exercised");
            return done;
        }

        static void ElementRuntimeRegistry(Action<bool,string> check)
        {
            Fixture f=Create();var source=LegacyReference.Contents(f.Profile,f.Car);
            var tirePart=source.parts.FirstOrDefault(p=>p?.condition?.elements!=null&&p.condition.elements.Any(e=>e is TireCondition));
            check(tirePart!=null,"R1-14 actual TireCondition member anchors element registry controls");
            if(tirePart==null)return;
            var baseSource=tirePart.condition.elements.OfType<TireCondition>().First();
            var members=ExistingFixtureElements(f.Profile).GroupBy(e=>e.GetType()).ToDictionary(group=>group.Key,group=>group.First());
            Type[] registry={typeof(EngineElement),typeof(PistonElement),typeof(ConnectingRodElement),typeof(PistonRingElement),
                typeof(BearingElement),typeof(ValveElement),typeof(ValveSpringElement),typeof(ValveSealElement),typeof(FollowerElement),
                typeof(WheelElement),typeof(IgnitionElement),typeof(SparkPlugElement),typeof(GlowPlugElement),typeof(FuelSystemElement),typeof(TireCondition)};
            int sameType=0,typeMismatch=0,unavailable=0,baseDiagnostic=0;
            foreach(Type concreteType in registry)
            {
                EngineElement left;
                if(concreteType==typeof(EngineElement))
                {
                    // Base EngineElement is not a persisted physical subtype; this is a data-type-only
                    // compatibility diagnostic retagging an existing real member with the same ID.
                    left=RetagActualElement(baseSource,typeof(EngineElement));baseDiagnostic++;
                }
                else if(!members.TryGetValue(concreteType,out left))
                {
                    unavailable++;
                    check(false,"R1-14 OPEN: no real fixture element of registered runtime type is available; no synthetic member substituted: "+concreteType.Name);
                    continue;
                }
                var right=(EngineElement)CloneOwnedDiagnosticGraph(left,new Dictionary<object,object>(ReferenceEqualityComparer.Instance),0);
                var leftSnapshot=SnapshotWithElementArray(source,tirePart,new EngineElement[]{left});
                var rightSnapshot=SnapshotWithElementArray(source,tirePart,new EngineElement[]{right});
                bool oldSame=LegacyReference.Same(leftSnapshot,rightSnapshot,"");
                bool typedSame=ReservedBuildValues.FromSnapshot(leftSnapshot).MatchesCurrent(ReservedBuildValues.FromSnapshot(rightSnapshot),ReservedBuildComparisonMode.LegacyJsonFallback);
                check(oldSame&&typedSame,"R1-14 exact same runtime element type compares equal: "+concreteType.Name);
                sameType++;
                Type otherType=concreteType==typeof(EngineElement)?typeof(PistonElement):typeof(EngineElement);
                var other=RetagActualElement(left,otherType);
                var otherSnapshot=SnapshotWithElementArray(source,tirePart,new EngineElement[]{other});
                bool oldDifferent=LegacyReference.Same(leftSnapshot,otherSnapshot,"");
                bool typedDifferent=ReservedBuildValues.FromSnapshot(leftSnapshot).MatchesCurrent(ReservedBuildValues.FromSnapshot(otherSnapshot),ReservedBuildComparisonMode.LegacyJsonFallback);
                check(!oldDifferent&&!typedDifferent,"R1-14 exact runtime-type mismatch rejects: "+concreteType.Name+" vs "+otherType.Name);
                typeMismatch++;
            }
            check(registry.Length==15&&sameType+unavailable==15&&typeMismatch==sameType,"R1-14 every base/registered element type either had real-source equality/mismatch controls or an explicit failed prerequisite");
            trace.Add("R1-14|registryCount="+registry.Length+"|equalControls="+sameType+"|typeMismatchControls="+typeMismatch
                +"|unavailable="+unavailable+"|realIdentitySources="+String.Join(",",members.Select(pair=>pair.Key.Name+"="+pair.Value.id))
                +"|baseDiagnosticRetagCount="+baseDiagnostic+"|baseSourceType="+baseSource.GetType().Name+"|baseSourceId="+baseSource.id
                +"|originPart="+tirePart.instanceId+"|addedPhysicalRows=0");
        }

        static IEnumerable<EngineElement> ExistingFixtureElements(PlayerProfile profile)
        {
            foreach(var element in ElementsFromParts(profile.parts))yield return element;
            foreach(var unit in profile.looseEngineElements??new List<LooseEngineElement>())if(unit?.element!=null)yield return unit.element;
            foreach(var row in profile.inventory?.dealer??new List<DealerStock>())
            {
                foreach(var element in ElementsFromParts(row==null?null:new[]{row.part}))yield return element;
                if(row?.unit?.element!=null)yield return row.unit.element;
            }
            foreach(var offer in profile.vehicleStock?.listings??new List<VehicleOffer>())
                foreach(var element in ElementsFromParts(offer?.parts))yield return element;
            foreach(var checkpoint in profile.runCheckpoints??new List<RunCheckpoint>())
                foreach(var element in ElementsFromParts(checkpoint?.components))yield return element;
        }
        static IEnumerable<EngineElement> ElementsFromParts(IEnumerable<PartInstance> parts)
        {
            foreach(var part in parts??Enumerable.Empty<PartInstance>())
                foreach(var element in part?.condition?.elements??Array.Empty<EngineElement>())if(element!=null)yield return element;
        }

        static EngineElement RetagActualElement(EngineElement source,Type targetType)
        {
            if(!typeof(EngineElement).IsAssignableFrom(targetType))throw new ArgumentException("Not an EngineElement subtype.",nameof(targetType));
            var target=(EngineElement)FormatterServices.GetUninitializedObject(targetType);
            foreach(var field in Fields(targetType))
            {
                var sourceField=source.GetType().GetField(field.Name,BindingFlags.Public|BindingFlags.Instance);
                if(sourceField==null||sourceField.FieldType!=field.FieldType)continue;
                field.SetValue(target,CloneOwnedDiagnosticGraph(sourceField.GetValue(source),new Dictionary<object,object>(ReferenceEqualityComparer.Instance),0));
            }
            return target;
        }

        static void NullEmptyOrderShapes(Action<bool,string> check)
        {
            Fixture f=Create();var source=LegacyReference.Contents(f.Profile,f.Car);
            var baseline=ReservedBuildValues.FromSnapshot(source);
            AssertShape(check,source,baseline,s=>s.fuel=null,"null fuel");
            AssertShape(check,source,baseline,s=>s.fluids=null,"null fluids");
            AssertShape(check,source,baseline,s=>s.tune=null,"null tune");
            AssertShape(check,source,baseline,s=>s.parts=null,"null parts");
            AssertShape(check,source,baseline,s=>s.parts=Array.Empty<PartInstance>(),"empty parts");
            if(source.parts!=null&&source.parts.Length>0)
                AssertShape(check,source,baseline,s=>s.parts[0]=null,"null part entry");
            check(source.parts!=null&&source.parts.Length>1,"R1-08 normalized actual fixture supplies multiple ordered installed parts");
            if(source.parts!=null&&source.parts.Length>1)
                AssertShape(check,source,baseline,s=>{var first=s.parts[0];s.parts[0]=s.parts[1];s.parts[1]=first;},"ordered parts");
            trace.Add("R1-08|partsCount="+(source.parts==null?-1:source.parts.Length));
        }

        static void ElementArrayRuntimeType(Action<bool,string> check)
        {
            Fixture f=Create();var source=LegacyReference.Contents(f.Profile,f.Car);
            var part=source.parts.FirstOrDefault(p=>p?.condition?.elements!=null&&p.condition.elements.Any(e=>e is TireCondition));
            check(part!=null,"R1-09 normalized actual installed build includes a TireCondition element");
            if(part==null)return;
            var originArray=part.condition.elements;
            var captured=ReservedBuildValues.FromSnapshot(source);
            var copiedPart=captured.PartsCopy().FirstOrDefault(p=>p!=null&&p.instanceId==part.instanceId);
            check(copiedPart!=null&&copiedPart.condition!=null&&copiedPart.condition.elements!=null
                &&copiedPart.condition.elements.GetType()==originArray.GetType()
                &&!ReferenceEquals(copiedPart.condition.elements,originArray),
                "R1-09 owned copy preserves source element-array runtime type without retaining its array");
            if(copiedPart?.condition?.elements!=null&&originArray!=null)
            {
                check(copiedPart.condition.elements.Length==originArray.Length,"R1-09 owned element copy preserves array length");
                for(int i=0;i<Math.Min(originArray.Length,copiedPart.condition.elements.Length);i++)
                    if(originArray[i]!=null)check(!ReferenceEquals(originArray[i],copiedPart.condition.elements[i]),"R1-09 owned copy does not retain source element reference at index "+i);
            }

            var actualTire=part.condition.elements.OfType<TireCondition>().First();
            var first=SnapshotWithElementArray(source,part,new EngineElement[]{actualTire});
            var second=SnapshotWithElementArray(source,part,new TireCondition[]{actualTire});
            check(!LegacyReference.Same(first,second,""),"R1-09 frozen comparer rejects EngineElement[] versus TireCondition[] despite same member");
            check(!ReservedBuildValues.FromSnapshot(first).MatchesCurrent(ReservedBuildValues.FromSnapshot(second),ReservedBuildComparisonMode.LegacyJsonFallback),
                "R1-09 typed fallback preserves array runtime-type distinction");
            var emptyEngine=SnapshotWithElementArray(source,part,Array.Empty<EngineElement>());
            var emptyTire=SnapshotWithElementArray(source,part,Array.Empty<TireCondition>());
            check(!LegacyReference.Same(emptyEngine,emptyTire,""),"R1-09 frozen comparer rejects empty array runtime-type mismatch");
            check(!ReservedBuildValues.FromSnapshot(emptyEngine).MatchesCurrent(ReservedBuildValues.FromSnapshot(emptyTire),ReservedBuildComparisonMode.LegacyJsonFallback),
                "R1-09 typed fallback rejects empty TireCondition[] versus EngineElement[]");
            check(part.condition.elements.Length==originArray.Length&&ReferenceEquals(part.condition.elements,originArray)
                &&ReferenceEquals(part.condition.elements.FirstOrDefault(e=>e is TireCondition),actualTire),
                "R1-09 diagnostic repacking leaves source array and tire member references unchanged");
        }
        static ReservedBuildSnapshot SnapshotWithElementArray(ReservedBuildSnapshot source,PartInstance target,EngineElement[] elements)
        {
            var parts=(PartInstance[])source.parts.Clone();int index=Array.FindIndex(parts,p=>p!=null&&p.instanceId==target.instanceId);
            var copy=parts[index].Copy();copy.condition.elements=elements;parts[index]=copy;
            return new ReservedBuildSnapshot{fuel=source.fuel,fluids=source.fluids,tune=source.tune,paint=source.paint,parts=parts};
        }
        static ReservedBuildSnapshot SnapshotWithElementArray(ReservedBuildSnapshot source,PartInstance target,TireCondition[] elements)
        {
            var parts=(PartInstance[])source.parts.Clone();int index=Array.FindIndex(parts,p=>p!=null&&p.instanceId==target.instanceId);
            var copy=parts[index].Copy();copy.condition.elements=elements;parts[index]=copy;
            return new ReservedBuildSnapshot{fuel=source.fuel,fluids=source.fluids,tune=source.tune,paint=source.paint,parts=parts};
        }
        static ReservedBuildSnapshot CloneSnapshot(ReservedBuildSnapshot source)
        { return (ReservedBuildSnapshot)CloneOwnedDiagnosticGraph(source,new Dictionary<object,object>(ReferenceEqualityComparer.Instance),0); }
        static object CloneOwnedDiagnosticGraph(object source,Dictionary<object,object> seen,int depth)
        {
            if(source==null)return null;
            Type type=source.GetType();
            if(type==typeof(string)||type.IsPrimitive||type.IsEnum||type.IsValueType||type==typeof(decimal))return source;
            if(depth>32)throw new InvalidOperationException("R1 bounded clone graph exceeded depth 32.");
            if(seen.TryGetValue(source,out object prior))return prior;
            if(type.IsArray)
            {
                var input=(Array)source;
                if(input.Rank!=1)throw new InvalidOperationException("R1 bounded clone supports only one-dimensional arrays.");
                Type elementType=type.GetElementType();var output=Array.CreateInstance(elementType,input.Length);seen.Add(source,output);
                for(int i=0;i<input.Length;i++)output.SetValue(CloneOwnedDiagnosticGraph(input.GetValue(i),seen,depth+1),i);
                return output;
            }
            if(!AllowedSnapshotType(type))throw new InvalidOperationException("R1 bounded clone encountered unsupported graph type: "+type.FullName);
            object clone=FormatterServices.GetUninitializedObject(type);seen.Add(source,clone);
            foreach(var field in Fields(type))field.SetValue(clone,CloneOwnedDiagnosticGraph(field.GetValue(source),seen,depth+1));
            return clone;
        }
        static bool AllowedSnapshotType(Type type)
        {
            return type==typeof(ReservedBuildSnapshot)||type==typeof(FuelState)||type==typeof(FluidState)||type==typeof(Tune)
                ||type==typeof(AwdSpec)||type==typeof(PartInstance)||type==typeof(ComponentCondition)||type==typeof(ClutchConditionState)
                ||typeof(EngineElement).IsAssignableFrom(type);
        }
        static void AssertShape(Action<bool,string> check,ReservedBuildSnapshot original,ReservedBuildValues baseline,Action<ReservedBuildSnapshot> mutate,string label)
        {
            var altered=CloneSnapshot(original);
            bool untouchedClone=LegacyReference.Same(original,altered,"");
            check(untouchedClone,"R1-08 bounded clone independently matches before shape mutation: "+label);
            if(!untouchedClone)return;
            mutate(altered);
            bool legacy=LegacyReference.Same(original,altered,"");
            bool typed=baseline.MatchesCurrent(ReservedBuildValues.FromSnapshot(altered),ReservedBuildComparisonMode.LegacyJsonFallback);
            check(!legacy,"R1-08 frozen field comparer distinguishes "+label);
            check(!typed,"R1-08 typed fallback distinguishes "+label);
        }

        sealed class PathPart { internal FieldInfo Field; internal int Index=-1; }
        sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance=new ReferenceEqualityComparer();
            public new bool Equals(object a,object b)=>ReferenceEquals(a,b);
            public int GetHashCode(object value)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
        static void CollectPaths(object value,List<PathPart> prefix,HashSet<object> visited,List<List<PathPart>> output,
            List<string> nullComplex,SortedSet<string> reachedComplexTypes,int depth)
        {
            if(value==null||depth>20)return;
            if(!(value is string)&&!value.GetType().IsValueType&&!visited.Add(value))return;
            if(!Leaf(value)&&!(value is IList))reachedComplexTypes.Add(value.GetType().FullName);
            if(value is IList list)
            {
                for(int i=0;i<list.Count;i++)
                {
                    var p=new List<PathPart>(prefix){new PathPart{Index=i}};
                    if(list[i]==null)
                    {
                        Type itemType=list.GetType().GetElementType();
                        if(itemType==null&&list.GetType().IsGenericType)itemType=list.GetType().GetGenericArguments()[0];
                        nullComplex.Add(PathLabel(p)+":"+(itemType==null?"unknown":itemType.FullName)+"/null-element");continue;
                    }
                    if(Leaf(list[i]))output.Add(p);else CollectPaths(list[i],p,visited,output,nullComplex,reachedComplexTypes,depth+1);
                }
                return;
            }
            foreach(var field in Fields(value.GetType()))
            {
                object child=field.GetValue(value);var p=new List<PathPart>(prefix){new PathPart{Field=field}};
                if(child==null)
                {
                    if(field.FieldType==typeof(string))output.Add(p);
                    else nullComplex.Add(PathLabel(p)+":"+field.FieldType.FullName+"/null-field");
                    continue;
                }
                if(Leaf(child))output.Add(p);else CollectPaths(child,p,visited,output,nullComplex,reachedComplexTypes,depth+1);
            }
        }
        static string PathLabel(List<PathPart> path)=>String.Join(".",path.Select(p=>p.Field!=null?p.Field.Name:"["+p.Index+"]"));
        static FieldInfo[] Fields(Type type)=>Array.FindAll(type.GetFields(BindingFlags.Public|BindingFlags.Instance),f=>!Attribute.IsDefined(f,typeof(NonSerializedAttribute)));
        static bool Leaf(object value)=>value==null||value is string||value.GetType().IsPrimitive||value.GetType().IsEnum||value.GetType()==typeof(decimal);
        static object ReadPath(object root,List<PathPart> path)
        {
            object at=root;
            for(int i=0;i<path.Count;i++)
            {
                var part=path[i];
                if(part.Field!=null)at=part.Field.GetValue(at);
                else at=((IList)at)[part.Index];
            }
            return at;
        }
        static Type PathType(object root,List<PathPart> path)
        {
            object at=root;
            for(int i=0;i<path.Count;i++)
            {
                var part=path[i];
                if(part.Field!=null)
                {
                    if(i==path.Count-1)return part.Field.FieldType;
                    at=part.Field.GetValue(at);
                }
                else
                {
                    var elementType=at.GetType().GetElementType();
                    if(i==path.Count-1)return elementType;
                    at=((IList)at)[part.Index];
                }
            }
            return typeof(object);
        }
        static bool SetPath(object root,List<PathPart> path,object value)
        {
            try { SetPathValue(root,path,0,value); return true; }
            catch(ArgumentException) { return false; }
            catch(FieldAccessException) { return false; }
        }
        static object SetPathValue(object at,List<PathPart> path,int offset,object value)
        {
            var part=path[offset];bool last=offset==path.Count-1;
            if(part.Field!=null)
            {
                if(last){part.Field.SetValue(at,value);return at;}
                object child=part.Field.GetValue(at);if(child==null)throw new ArgumentException("null path");
                part.Field.SetValue(at,SetPathValue(child,path,offset+1,value));return at;
            }
            IList list=(IList)at;
            if(last){list[part.Index]=value;return at;}
            list[part.Index]=SetPathValue(list[part.Index],path,offset+1,value);return at;
        }
        static bool TryChanged(object value,Type type,out object changed)
        {
            changed=null;Type t=value==null?type:value.GetType();
            if(value==null&&t==typeof(string)){changed="<r1-null-string>";return true;}
            if(value==null)return false;
            if(t==typeof(string)){changed=(string)value+"#";return true;}
            if(t==typeof(bool)){changed=!(bool)value;return true;}
            if(t.IsEnum){changed=Enum.ToObject(t,Convert.ToInt64(value)+1);return true;}
            if(t==typeof(float)){changed=(float)value+17.25f;return true;}
            if(t==typeof(double)){changed=(double)value+17.25d;return true;}
            if(t==typeof(int)){changed=(int)value+1;return true;}
            if(t==typeof(long)){changed=(long)value+1L;return true;}
            if(t==typeof(short)){changed=(short)((short)value+1);return true;}
            if(t==typeof(sbyte)){changed=(sbyte)((sbyte)value+1);return true;}
            if(t==typeof(ushort)){changed=(ushort)((ushort)value+1);return true;}
            if(t==typeof(byte)){changed=(byte)((byte)value+1);return true;}
            if(t==typeof(uint)){changed=unchecked((uint)value+1u);return true;}
            if(t==typeof(ulong)){changed=unchecked((ulong)value+1UL);return true;}
            if(t==typeof(char)){changed=(char)((char)value+1);return true;}
            if(t==typeof(decimal)){changed=(decimal)value+1m;return true;}
            return false;
        }

        static void NumericControls(Action<bool, string> check)
        {
            double one = 1d, four = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(one)+4), five = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(one)+5);
            check(LegacyReference.Same(one, four, "oilLitres"), "R1-11 frozen 4-ULP non-health double is equal");
            check(!LegacyReference.Same(one, five, "oilLitres"), "R1-11 frozen 5-ULP non-health double is unequal");
            check(!LegacyReference.Same(double.NaN, double.NaN, "oilLitres"), "R1-11 legacy double comparison rejects NaN");
            check(LegacyReference.Same(0d, -0d, "health"), "R1-11 legacy equality treats signed zeros as equal before health guard");
            check(!LegacyReference.Same(one, four, "health"), "R1-11 health remains exact outside x==y");
            // Compare typed modes on a real captured structure by modifying a defensive copy's fluid value.
            Fixture f = Create(); var baseline = ReservedBuildValues.CaptureCreated(f.Profile, f.Car);
            var oneFluid = baseline.FluidsCopy(); oneFluid.oilLitres=one;
            var fourFluid = baseline.FluidsCopy(); fourFluid.oilLitres=four;
            var alteredValue = ReservedBuildValues.FromSnapshot(new ReservedBuildSnapshot { fuel=baseline.FuelCopy(), fluids=fourFluid,
                tune=baseline.TuneCopy(), paint=baseline.Paint, parts=baseline.PartsCopy() });
            var oneValue = ReservedBuildValues.FromSnapshot(new ReservedBuildSnapshot { fuel=baseline.FuelCopy(), fluids=oneFluid,
                tune=baseline.TuneCopy(), paint=baseline.Paint, parts=baseline.PartsCopy() });
            check(oneValue.MatchesCurrent(alteredValue, ReservedBuildComparisonMode.LegacyJsonFallback), "R1-11 typed 4-ULP fallback equality");
            check(!oneValue.MatchesCurrent(alteredValue, ReservedBuildComparisonMode.ExactHashBaseline), "R1-11 exact hash baseline compares raw double bits");
            var nanSnapshot=LegacyReference.Contents(f.Profile,f.Car);nanSnapshot.fuel.cumulativeUsedLitres=Double.NaN;
            var nanValue=ReservedBuildValues.FromSnapshot(nanSnapshot);
            check(LegacyReference.Same(nanSnapshot,nanSnapshot,"" )==false,"R1-11 legacy Same traverses identical object graphs and rejects NaN fields");
            check(nanValue.MatchesCurrent(nanValue,ReservedBuildComparisonMode.ExactHashBaseline),"R1-11 exact mode compares identical NaN payload bits");
            check(!nanValue.MatchesCurrent(nanValue,ReservedBuildComparisonMode.LegacyJsonFallback),"R1-11 fallback self-comparison must retain legacy NaN semantics");
        }

        static void SourceAndGlobalInvariance(Action<bool, string> check)
        {
            Fixture f = Create(); var job = f.Profile.business.jobs[0];
            var ownerRef=f.Profile; var carRef=f.Car; var partsRef=f.Profile.parts; var installedRef=f.Car.installed;
            string before=JsonUtility.ToJson(f.Profile); var randomBefore=UnityEngine.Random.state;
            string numericBitsBefore=NumericBits(f.Profile,f.Car);
            long jsonBefore=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            RuntimeEntropy.IdentityObservation identity=RuntimeEntropy.BeginIdentityObservation();
            try { LegacyReservedBuildImport.FromLegacyOwner(f.Profile); }
            finally { identity.Dispose(); }
            long jsonDelta=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount-jsonBefore;
            string after=JsonUtility.ToJson(f.Profile); var randomAfter=UnityEngine.Random.state;
            string numericBitsAfter=NumericBits(f.Profile,f.Car);
            check(before==after, "R1-08 importer must not mutate source serialized profile values");
            check(numericBitsBefore==numericBitsAfter,"R1-12 explicit IEEE bit transcript remains unchanged across import");
            check(ReferenceEquals(ownerRef,f.Profile)&&ReferenceEquals(carRef,f.Car)&&ReferenceEquals(partsRef,f.Profile.parts)&&ReferenceEquals(installedRef,f.Car.installed),
                "R1-08 importer must preserve source object references");
            check(randomBefore.Equals(randomAfter), "R1-08 importer must preserve Unity Random state");
            check(identity.Count==0&&!identity.Overflowed, "R1-08 import must not allocate gameplay identities");
            check(job==f.Profile.business.jobs[0], "R1-08 imported result must not replace source job");
            trace.Add("R1-12|identityEvents=" + identity.Count + "|instrumentedJsonCallDelta="+jsonDelta+
                "|note=legacy importer uses explicit JSON boundary not currently instrumented by RuntimeDataEncodingProbe");
        }
        static string NumericBits(PlayerProfile p,OwnedCar c)
        {
            var bits=new List<string>();
            if(c.fuel!=null){bits.Add(BitConverter.DoubleToInt64Bits(c.fuel.litres).ToString("X16"));bits.Add(BitConverter.DoubleToInt64Bits(c.fuel.cumulativeUsedLitres).ToString("X16"));}
            if(c.fluids!=null){bits.Add(BitConverter.DoubleToInt64Bits(c.fluids.oilLitres).ToString("X16"));bits.Add(BitConverter.DoubleToInt64Bits(c.fluids.oilCondition).ToString("X16"));}
            if(c.tune!=null)bits.Add(BitConverter.ToInt32(BitConverter.GetBytes(c.tune.camTimingDegrees),0).ToString("X8"));
            foreach(var part in p.parts.Where(x=>x?.condition!=null).Take(12))
            {
                bits.Add(BitConverter.ToInt32(BitConverter.GetBytes(part.condition.health),0).ToString("X8"));
                bits.Add(BitConverter.DoubleToInt64Bits(part.condition.runningSeconds).ToString("X16"));
                if(part.condition.elements!=null)foreach(var element in part.condition.elements.Where(x=>x!=null).Take(4))
                    bits.Add(BitConverter.ToInt32(BitConverter.GetBytes(element.health),0).ToString("X8"));
            }
            return String.Join("|",bits);
        }

        static Fixture Create()
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile, out string carId, Seed);
            long now=Math.Max(CartPreviewCharacterizationFixtures.FixedNowTicks,profile.business.accruedThroughUtcTicks);
            GarageBusiness.Assign(profile,GarageBusiness.DefaultPolicy(),carId,"photo-session",now);
            return new Fixture(profile,profile.cars.Find(c=>c.instanceId==carId));
        }
        static string[] Labels(OwnedCar car) => car.installed.slots.OrderBy(s=>s.partInstanceId,StringComparer.Ordinal).Select(s=>s.slot).ToArray();
        sealed class Fixture { internal readonly PlayerProfile Profile; internal readonly OwnedCar Car; internal Fixture(PlayerProfile p,OwnedCar c){Profile=p;Car=c;} }
        static void Case(string id, Action body) { trace.Add("CASE|"+id); body(); }
        static void Record(bool ok,string message) { if(!ok)failures++; checks.Add((ok?"PASS|":"FAIL|")+message); }
        static string Argument(string name) { var args=Environment.GetCommandLineArgs(); for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];return null; }
        static void Write(string path,string text) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,text,new UTF8Encoding(false)); }

        /// <summary>Literal Editor oracle copied from the pre-R1 GarageBusiness/SnapshotEquality bodies.</summary>
        static class LegacyReference
        {
            static readonly System.Collections.Concurrent.ConcurrentDictionary<Type,FieldInfo[]> fields=new System.Collections.Concurrent.ConcurrentDictionary<Type,FieldInfo[]>();
            static FieldInfo[] Fields(Type t)=>fields.GetOrAdd(t,x=>Array.FindAll(x.GetFields(BindingFlags.Public|BindingFlags.Instance),f=>!Attribute.IsDefined(f,typeof(NonSerializedAttribute))));
            internal static string Snapshot(PlayerProfile p,OwnedCar c)
            {
                string text=JsonUtility.ToJson(c.fuel)+JsonUtility.ToJson(c.fluids)+JsonUtility.ToJson(c.tune)+c.paintHex;
                foreach(var s in c.installed.slots.OrderBy(s=>s.partInstanceId,StringComparer.Ordinal))text+=s.slot+JsonUtility.ToJson(p.parts.Find(i=>i.instanceId==s.partInstanceId));
                using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
            }
            internal static ReservedBuildSnapshot Contents(PlayerProfile p,OwnedCar c)=>new ReservedBuildSnapshot{fuel=c.fuel,fluids=c.fluids,tune=c.tune,paint=c.paintHex,parts=c.installed.slots.OrderBy(s=>s.partInstanceId,StringComparer.Ordinal).Select(s=>p.parts.Find(i=>i.instanceId==s.partInstanceId)).ToArray()};
            internal static bool Matches(PlayerProfile p,OwnedCar c,AssignedGarageJob j)=>Snapshot(p,c)==j.snapshot||!String.IsNullOrEmpty(j.snapshotJson)&&Serialized(j.snapshotJson,Contents(p,c));
            internal static bool SerializedContents(string json,ReservedBuildSnapshot contents)=>Serialized(json,contents);
            internal static (bool Matched,string ErrorType,string ErrorMessage,string ErrorParameterName) CaptureMatches(PlayerProfile p,OwnedCar c,AssignedGarageJob j)
            {
                try { return (Matches(p,c,j),null,null,null); }
                catch(Exception error) { return (false,error.GetType().FullName,error.Message,(error as ArgumentException)?.ParamName); }
            }
            internal static (string Type,string Message,string Parameter) CaptureDecodeError(string json)
            {
                try { JsonUtility.FromJson<ReservedBuildSnapshot>(json); return (null,null,null); }
                catch(Exception error) { return (error.GetType().FullName,error.Message,(error as ArgumentException)?.ParamName); }
            }
            static bool Serialized<T>(string json,T current)=>JsonUtility.ToJson(current)==json||Same(JsonUtility.FromJson<T>(json),current,"");
            internal static bool Same(object a,object b,string field="")
            {
                if(a==null||b==null)return a==b;if(a.GetType()!=b.GetType())return false;var type=a.GetType();
                if(type==typeof(double)){double x=(double)a,y=(double)b;if(x==y)return true;if(field=="health"||!Finite(x)||!Finite(y)||Math.Sign(x)!=Math.Sign(y))return false;long xb=BitConverter.DoubleToInt64Bits(x),yb=BitConverter.DoubleToInt64Bits(y);return xb>yb?xb-yb<=4:yb-xb<=4;}
                if(type.IsPrimitive||type.IsEnum||type==typeof(string)||type==typeof(decimal))return a.Equals(b);
                if(a is IList al&&b is IList bl){if(al.Count!=bl.Count)return false;for(int i=0;i<al.Count;i++)if(!Same(al[i],bl[i],field))return false;return true;}
                foreach(var f in Fields(type))if(!Same(f.GetValue(a),f.GetValue(b),f.Name))return false;return true;
            }
            static bool Finite(double v)=>!Double.IsNaN(v)&&!Double.IsInfinity(v);
        }
    }
}
#endif
