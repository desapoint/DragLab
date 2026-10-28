#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    public static partial class CartPreviewCharacterizationChecks
    {
        [Serializable] class NormalizationWalkerNode { public int value; }
        [Serializable] sealed class NormalizationWalkerDerivedNode:NormalizationWalkerNode { }
        [Serializable] sealed class NormalizationWalkerHolder
        {
            public object node;
            public object left,right;
            public float scalar;
            public double precise;
            public List<int> order;
            public EngineElement[] engineElements;
            public TireCondition[] tireElements;
        }
        sealed class NormalizationCase
        {
            public string Name,Disposition;
            public Action<PlayerProfile> Corrupt;
            public bool ValidOptionalNull;
        }

        /// <summary>Strict Editor-only Ensure / source-diff characterization. Parent owns collector wiring.</summary>
        static void RunNormalizationContracts(Trace trace,string subset)
        {
            if(trace==null)throw new ArgumentNullException(nameof(trace));
            bool all=subset=="all";
            bool sourceDiff=subset=="source-diffs"||subset=="normalization"||all;
            bool ensure=subset=="ensure"||subset=="normalization"||all;
            if(!sourceDiff&&!ensure)throw new ArgumentException("Expected all, normalization, source-diffs, or ensure.",nameof(subset));
            trace.Add("NORMALIZATION|subset="+subset+";source=literal-test-clone;comparator=typed-fields+float/double-bits+ordered-collections+reference-paths;json-warming=none");
            RunNormalizationWalkerSelfTests(trace);
            if(ensure)RunValidEnsureBoundary(trace);
            if(ensure)foreach(var item in BuildNormalizationCases())RunNormalizationCase(trace,item);
            if(sourceDiff)RunPreviewAttribution(trace);
            trace.Add("NORMALIZATION_DISPOSITION|PROPOSED_ONLY|Optional current-schema nulls: preserve source null and derive private defaults only if needed; do not reject solely for JSON materialization. Required malformed state: no heal, return the exact authoritative typed validation/missing-data error. Neither disposition is reviewer-approved by this diagnostic.");
        }

        static void RunValidEnsureBoundary(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var seed,out _,FixtureSeed+220);
            var p=CartPreviewLiteralTestClone.Copy(seed);
            string validation=MechanicsOwnership.Validate(p,p.catalog);
            var before=FlattenNormalization(p);var references=NormalizationReferences(p);
            var random=UnityEngine.Random.state;long clockBefore=RuntimeEntropy.UtcNow.Ticks;var identity=RuntimeEntropy.BeginIdentityObservation();
            string failure=null;try{MechanicsOwnership.Ensure(p,p.catalog);}catch(Exception e){failure=e.GetType().FullName+" | "+e.Message;}finally{identity.Dispose();}
            var after=FlattenNormalization(p);var afterReferences=NormalizationReferences(p);
            var differences=NormalizationDiff(before,after);var refDifferences=NormalizationReferenceDiff(references,afterReferences);
            WriteNormalizationDiff(trace,"valid-ensure",differences,refDifferences);
            trace.Assert(validation==null,"Current normalized fixture validates before second Ensure / "+(validation??"valid"));
            trace.Assert(failure==null,"Second Ensure on current normalized fixture completes / "+(failure??"ok"));
            trace.Assert(identity.Closed&&!identity.Overflowed&&identity.Count==0,
                "Valid current-schema Ensure emits no physical identity and observer closed without overflow / "+IdentityTrace(identity));
            trace.Assert(differences.Count==0&&refDifferences.Count==0&&UnityEngine.Random.state.Equals(random),
                "Current valid Ensure is exact typed field/reference/random no-op / fields="+differences.Count+" refs="+refDifferences.Count);
            trace.Add("NORMALIZATION_VALID_ENSURE|validator="+(validation??"valid")+";error="+(failure??"none")+
                ";changedPaths="+differences.Count+";referencePathChanges="+refDifferences.Count+";randomChanged="+(!UnityEngine.Random.state.Equals(random))+
                ";capture="+RuntimeEntropy.Capture+";clockTicks="+clockBefore+"->"+RuntimeEntropy.UtcNow.Ticks+";identityEmissions="+IdentityTrace(identity));
        }

        static List<NormalizationCase> BuildNormalizationCases()
        {
            return new List<NormalizationCase>
            {
                new NormalizationCase{Name="old-save-version",Disposition="migration-only;typed preview must not migrate",Corrupt=p=>p.saveVersion=1},
                new NormalizationCase{Name="null-required-profile-collection",Disposition="required structure; reject exact typed validator error without heal",Corrupt=p=>p.parts=null},
                new NormalizationCase{Name="inactive-dealer-discriminator",Disposition="metadata mismatch; record validator and exact Ensure normalization",Corrupt=p=>{var s=p.inventory.dealer.FirstOrDefault(x=>x!=null&&x.IsPart);if(s==null)throw new InvalidOperationException("fixture has no actual used part dealer row");s.isUnit=true;}},
                new NormalizationCase{Name="null-car-fluids",Disposition="classify by pre-mutation validator; optional null preserves source null, required invalid state rejects",Corrupt=p=>p.SelectedCar.fluids=null,ValidOptionalNull=true},
                new NormalizationCase{Name="null-car-fuel",Disposition="classify by pre-mutation validator; optional null preserves source null, required invalid state rejects",Corrupt=p=>p.SelectedCar.fuel=null,ValidOptionalNull=true},
                new NormalizationCase{Name="null-awd-tune",Disposition="optional current-schema AWD tune value; preserve null if accepted and derive privately only when needed",Corrupt=p=>p.SelectedCar.tune.awd=null,ValidOptionalNull=true},
                new NormalizationCase{Name="null-last-pass",Disposition="optional current-schema prior pass; preserve null if accepted",Corrupt=p=>p.SelectedCar.lastPass=null,ValidOptionalNull=true},
                new NormalizationCase{Name="null-custom-name",Disposition="optional current-schema display name; preserve null if accepted",Corrupt=p=>p.SelectedCar.customName=null,ValidOptionalNull=true},
                new NormalizationCase{Name="null-garage-job-id",Disposition="optional current-schema job link; preserve null if accepted",Corrupt=p=>p.SelectedCar.garageJobId=null,ValidOptionalNull=true},
                new NormalizationCase{Name="missing-oem-service-part",Disposition="actual legacy service-migration marker plus removed real installed record; record exact insertion, never seed a replacement fixture item",Corrupt=p=>{var car=p.SelectedCar;var slot=car.installed.slots.FirstOrDefault(b=>Enum.TryParse(b.slot,out PartCategory c)&&ServiceHardware.IsCategory(c));if(slot==null)throw new InvalidOperationException("fixture has no actual installed service-hardware slot");var part=p.parts.Find(x=>x!=null&&x.instanceId==slot.partInstanceId);if(part==null)throw new InvalidOperationException("fixture service binding has no physical part");p.parts.Remove(part);car.installed.slots.Remove(slot);car.serviceHardwareVersion=0;}},
                new NormalizationCase{Name="missing-required-member-array",Disposition="record actual Ensure heal/exception; future typed behavior follows actual pre-validator result",Corrupt=p=>{var part=InstalledPistonPack(p);if(part==null)throw new InvalidOperationException("fixture has no owner-resolved installed piston pack");part.condition.elements=null;}},
                new NormalizationCase{Name="missing-required-member-id",Disposition="record actual Ensure behavior (EngineElements.Ensure returns for nonempty member arrays); future typed behavior follows actual pre-validator result",Corrupt=p=>{var part=InstalledPistonPack(p);if(part==null)throw new InvalidOperationException("fixture has no owner-resolved installed piston pack");part.condition.elements[0].id=null;}},
                new NormalizationCase{Name="missing-tire-corners",Disposition="record actual TireDynamics.Ensure behavior for an empty typed tire array; future typed behavior follows actual pre-validator result",Corrupt=p=>{var part=FittedPack(p,PartCategory.Tires);if(part==null)throw new InvalidOperationException("fixture has no actual fitted tire pack");part.condition.tires=Array.Empty<TireCondition>();}},
                new NormalizationCase{Name="missing-tire-id",Disposition="record actual TireDynamics.Ensure ID repair and emitted identity; future typed behavior follows actual pre-validator result",Corrupt=p=>{var part=FittedPack(p,PartCategory.Tires);if(part==null||part.condition.tires.Length!=4)throw new InvalidOperationException("fixture has no actual four-corner fitted tire pack");part.condition.tires[0].id=null;}},
                new NormalizationCase{Name="missing-wheel-corners",Disposition="record actual WheelFitments.Ensure behavior for null members; future typed behavior follows actual pre-validator result",Corrupt=p=>{var part=FittedPack(p,PartCategory.Wheels);if(part==null)throw new InvalidOperationException("fixture has no actual fitted wheel pack");part.condition.elements=null;}},
                new NormalizationCase{Name="missing-market",Disposition="required market lifecycle metadata; no preview regeneration",Corrupt=p=>p.inventory.market=null},
                new NormalizationCase{Name="missing-reservation-supply",Disposition="active hold with missing exact supply rows; record validator and legacy expiration/heal behavior",Corrupt=p=>{var car=p.SelectedCar;var offer=FindOffer(p,car,PartCategory.HeadIntake,false);if(offer==null)throw new InvalidOperationException("fixture has no actual reservable offer");CartReservations.Capture(p,car.instanceId,new[]{new PartCartLine{action=CartAction.NewSet,definitionId=offer.id,quantity=1}});p.inventory.cart.supply=null;}}
            };
        }

        static PartInstance InstalledPistonPack(PlayerProfile profile)
        {
            var car=profile?.SelectedCar;if(car==null||string.IsNullOrEmpty(car.engineAssemblyId))return null;
            return profile.parts?.FirstOrDefault(part=>part!=null&&part.location==PartLocation.Assembly&&part.ownerId==car.engineAssemblyId&&
                part.condition?.elements?.Length>0&&profile.catalog.Find(part.definitionId)?.category==PartCategory.Pistons);
        }

        static PartInstance FittedPack(PlayerProfile profile,PartCategory category)
        {
            var car=profile?.SelectedCar;if(car==null)return null;
            string owner=category==PartCategory.Tires||category==PartCategory.Wheels?car.instanceId:car.engineAssemblyId;
            return profile.parts?.FirstOrDefault(part=>part!=null&&part.ownerId==owner&&part.location==PartLocation.Vehicle&&
                profile.catalog.Find(part.definitionId)?.category==category);
        }

        static void RunNormalizationCase(Trace trace,NormalizationCase test)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var seed,out _,FixtureSeed+221);
            var seedBefore=FlattenNormalization(seed);var seedReferences=NormalizationReferences(seed);var setupRandom=UnityEngine.Random.state;
            PlayerProfile p;
            try{p=CartPreviewLiteralTestClone.Copy(seed);test.Corrupt(p);}
            catch(Exception e){trace.Assert(false,"Normalization fixture constructed without post-corruption healing / "+test.Name+" / "+e.GetType().FullName+" | "+e.Message);trace.Add("NORMALIZATION_OPEN|"+test.Name+"|fixture="+e.GetType().FullName+" | "+e.Message);return;}
            string validation;try{validation=MechanicsOwnership.Validate(p,p.catalog);}catch(Exception e){validation="THREW "+e.GetType().FullName+" | "+e.Message;}
            var before=FlattenNormalization(p);var refsBefore=NormalizationReferences(p);var random=UnityEngine.Random.state;long clockBefore=RuntimeEntropy.UtcNow.Ticks;var identity=RuntimeEntropy.BeginIdentityObservation();
            string ensureError=null;try{MechanicsOwnership.Ensure(p,p.catalog);}catch(Exception e){ensureError=e.GetType().FullName+" | "+e.Message;}finally{identity.Dispose();}
            var after=FlattenNormalization(p);var refsAfter=NormalizationReferences(p);var differences=NormalizationDiff(before,after);var refDifferences=NormalizationReferenceDiff(refsBefore,refsAfter);
            WriteNormalizationDiff(trace,test.Name,differences,refDifferences);
            var seedDiff=NormalizationDiff(seedBefore,FlattenNormalization(seed));var seedRefDiff=NormalizationReferenceDiff(seedReferences,NormalizationReferences(seed));
            WriteNormalizationDiff(trace,test.Name+"-original-seed",seedDiff,seedRefDiff);
            bool untouched=random.Equals(UnityEngine.Random.state);
            trace.Add("NORMALIZATION_CASE|"+test.Name+"|preValidator="+Safe(validation)+"|ensure="+Safe(ensureError??"success")+
                "|changedFields="+differences.Count+"|changedReferences="+refDifferences.Count+"|rngChanged="+(!untouched)+
                "|capture="+RuntimeEntropy.Capture+"|clockTicks="+clockBefore+"->"+RuntimeEntropy.UtcNow.Ticks+"|identityEmissions="+IdentityTrace(identity)+"|disposition="+test.Disposition);
            trace.Assert(untouched,"Ensure diagnostic observation leaves Unity random state unchanged / "+test.Name);
            trace.Assert(seedDiff.Count==0&&seedRefDiff.Count==0&&setupRandom.Equals(UnityEngine.Random.state),
                "Isolated corruption/Ensure leaves original normalized fixture and Unity RNG unchanged / "+test.Name);
            if(test.ValidOptionalNull&&validation==null)
                trace.Add("NORMALIZATION_VALID_OPTIONAL_NULL|"+test.Name+"|inputValid=yes|EnsureChangeCount="+differences.Count+"|typedProposal=preserve-null-and-derive-privately;not-review-approved");
            else if(validation==null)
                trace.Add("NORMALIZATION_VALID_SHAPE|"+test.Name+"|inputValid=yes|changes="+differences.Count+"|typedProposal=preserve-current-records;rule-review-open;do-not-label-JSON-materialization-as-invalid");
            else
                trace.Add("NORMALIZATION_MALFORMED|"+test.Name+"|inputValid=no|authoritativePreError="+Safe(validation)+"|typedProposal=no-heal-and-return-this-error;not-review-approved");
            if(ensureError!=null)trace.Add("NORMALIZATION_ENSURE_EXCEPTION|"+test.Name+"|"+Safe(ensureError));
        }

        static void RunPreviewAttribution(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var seed,out var carId,FixtureSeed+222);
            var baseProfile=CartPreviewLiteralTestClone.Copy(seed);var selected=baseProfile.cars.Find(x=>x.instanceId==carId);
            var offer=FindOffer(baseProfile,selected,PartCategory.HeadIntake,false);
            if(offer==null){trace.Assert(false,"Preview attribution has an authored compatible HeadIntake offer");return;}
            var rows=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=offer.id}};
            RunEncodingAttribution(trace,"ToJson-only",seed,carId,rows);
            RunEncodingAttribution(trace,"FromJson-only",seed,carId,rows);
            RunEncodingAttribution(trace,"roundtrip-output",seed,carId,rows);
            RunCartOperationAttribution(trace,"Total.production",seed,carId,rows,false);
            RunCartOperationAttribution(trace,"Total.frozen",seed,carId,rows,true);
            RunCartOperationAttribution(trace,"PreviewProfile.production",seed,carId,rows,false);
            RunCartOperationAttribution(trace,"PreviewProfile.frozen",seed,carId,rows,true);
            var moved=seed.parts.FirstOrDefault(part=>part!=null&&part.location==PartLocation.Vehicle&&part.ownerId==carId);
            if(moved==null)trace.Add("PREVIEW_ATTRIBUTION_OPEN|move-route|normalized fixture has no actual installed physical part");
            else
            {
                var moveRows=new[]{new PartCartLine{action=CartAction.Move,assetId=moved.instanceId,definitionId=moved.definitionId,destination=InventoryKind.Storage,destinationId=SharedInventory.Storage}};
                RunCartOperationAttribution(trace,"Total.Move.production",seed,carId,moveRows,false);
                RunCartOperationAttribution(trace,"Total.Move.frozen",seed,carId,moveRows,true);
                RunCartOperationAttribution(trace,"PreviewProfile.Move.production",seed,carId,moveRows,false);
                RunCartOperationAttribution(trace,"PreviewProfile.Move.frozen",seed,carId,moveRows,true);
                RunCartOperationAttribution(trace,"Total+PreviewProfile.Move.production",seed,carId,moveRows,false);
                RunCartOperationAttribution(trace,"Total+PreviewProfile.Move.frozen",seed,carId,moveRows,true);
            }
        }

        static void RunEncodingAttribution(Trace trace,string operation,PlayerProfile seed,string carId,PartCartLine[] rows)
        {
            string encoded=operation=="FromJson-only"?JsonUtility.ToJson(CartPreviewLiteralTestClone.Copy(seed)):null;
            var rowsBefore=FlattenNormalization(rows);var rowRefs=NormalizationReferences(rows);
            var input=CartPreviewLiteralTestClone.Copy(seed);var before=FlattenNormalization(input);var refs=NormalizationReferences(input);var random=UnityEngine.Random.state;long clockBefore=RuntimeEntropy.UtcNow.Ticks;var identity=RuntimeEntropy.BeginIdentityObservation();
            PlayerProfile output=null;string error=null;
            try
            {
                if(operation=="ToJson-only")JsonUtility.ToJson(input);
                else if(operation=="FromJson-only")output=JsonUtility.FromJson<PlayerProfile>(encoded);
                else output=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(input));
            }
            catch(Exception e){error=e.GetType().FullName+" | "+e.Message;}finally{identity.Dispose();}
            var after=FlattenNormalization(input);var refsAfter=NormalizationReferences(input);
            var inputDiff=NormalizationDiff(before,after);var refDiff=NormalizationReferenceDiff(refs,refsAfter);
            var rowDiff=NormalizationDiff(rowsBefore,FlattenNormalization(rows));var rowRefDiff=NormalizationReferenceDiff(rowRefs,NormalizationReferences(rows));
            WriteNormalizationDiff(trace,"attribution-"+operation+"-input",inputDiff,refDiff);
            WriteNormalizationDiff(trace,"attribution-"+operation+"-rows",rowDiff,rowRefDiff);
            if(output!=null)WriteNormalizationOutputDiff(trace,"attribution-"+operation+"-output",input,output);
            trace.Assert(inputDiff.Count==0&&refDiff.Count==0&&rowDiff.Count==0&&rowRefDiff.Count==0&&random.Equals(UnityEngine.Random.state),
                "Encoding attribution leaves its observed source profile/catalog/rows/references and Unity RNG unchanged / "+operation);
            trace.Add("PREVIEW_ATTRIBUTION|"+operation+"|error="+Safe(error??"none")+"|inputFieldPaths="+inputDiff.Count+
                "|inputRefPaths="+refDiff.Count+"|rowFieldPaths="+rowDiff.Count+"|rowRefPaths="+rowRefDiff.Count+"|output="+(output==null?"none":"captured")+"|rngChanged="+(!random.Equals(UnityEngine.Random.state))+
                "|capture="+RuntimeEntropy.Capture+"|clockTicks="+clockBefore+"->"+RuntimeEntropy.UtcNow.Ticks+"|identityEmissions="+IdentityTrace(identity));
        }

        static void RunCartOperationAttribution(Trace trace,string operation,PlayerProfile seed,string carId,PartCartLine[] rows,bool frozen)
        {
            var seedBefore=FlattenNormalization(seed);var seedReferences=NormalizationReferences(seed);var setupRandom=UnityEngine.Random.state;
            var rowsBefore=FlattenNormalization(rows);var rowRefs=NormalizationReferences(rows);
            var input=CartPreviewLiteralTestClone.Copy(seed);var car=input.cars.Find(x=>x.instanceId==carId);
            var before=FlattenNormalization(input);var refs=NormalizationReferences(input);var random=UnityEngine.Random.state;long clockBefore=RuntimeEntropy.UtcNow.Ticks;var identity=RuntimeEntropy.BeginIdentityObservation();
            PlayerProfile output=null;string value=null,error=null;SortedDictionary<string,string> afterTotal=null;SortedDictionary<string,object> refsAfterTotal=null;
            try
            {
                if(operation.StartsWith("Total+PreviewProfile",StringComparison.Ordinal))
                {
                    value=(frozen?LegacyCartPreviewOracle.Total(input,carId,rows,now:CartPreviewCharacterizationFixtures.FixedNowTicks):PartCart.Total(input,carId,rows,now:CartPreviewCharacterizationFixtures.FixedNowTicks)).ToString(CultureInfo.InvariantCulture);
                    afterTotal=FlattenNormalization(input);refsAfterTotal=NormalizationReferences(input);car=input.cars.Find(x=>x.instanceId==carId);
                    output=frozen?LegacyCartPreviewOracle.PreviewProfile(input,car,rows):PartCart.PreviewProfile(input,car,rows);
                }
                else if(operation.StartsWith("Total",StringComparison.Ordinal))
                    value=(frozen?LegacyCartPreviewOracle.Total(input,carId,rows,now:CartPreviewCharacterizationFixtures.FixedNowTicks):PartCart.Total(input,carId,rows,now:CartPreviewCharacterizationFixtures.FixedNowTicks)).ToString(CultureInfo.InvariantCulture);
                else output=frozen?LegacyCartPreviewOracle.PreviewProfile(input,car,rows):PartCart.PreviewProfile(input,car,rows);
            }
            catch(Exception e){error=e.GetType().FullName+" | "+e.Message;}finally{identity.Dispose();}
            var after=FlattenNormalization(input);var refsAfter=NormalizationReferences(input);
            var inputDiff=NormalizationDiff(before,after);var refDiff=NormalizationReferenceDiff(refs,refsAfter);
            var rowDiff=NormalizationDiff(rowsBefore,FlattenNormalization(rows));var rowRefDiff=NormalizationReferenceDiff(rowRefs,NormalizationReferences(rows));
            WriteNormalizationDiff(trace,"attribution-"+operation+"-input",inputDiff,refDiff);
            WriteNormalizationDiff(trace,"attribution-"+operation+"-rows",rowDiff,rowRefDiff);
            if(afterTotal!=null)WriteNormalizationDiff(trace,"attribution-"+operation+"-after-total",NormalizationDiff(before,afterTotal),NormalizationReferenceDiff(refs,refsAfterTotal));
            if(output!=null)WriteNormalizationOutputDiff(trace,"attribution-"+operation+"-output",seed,output);
            trace.Assert(inputDiff.Count==0&&refDiff.Count==0&&rowDiff.Count==0&&rowRefDiff.Count==0&&random.Equals(UnityEngine.Random.state),
                "Cart operation leaves its observed input/catalog/rows/reference graph and Unity RNG unchanged / "+operation);
            var seedDiff=NormalizationDiff(seedBefore,FlattenNormalization(seed));var seedRefDiff=NormalizationReferenceDiff(seedReferences,NormalizationReferences(seed));
            trace.Assert(seedDiff.Count==0&&seedRefDiff.Count==0&&setupRandom.Equals(UnityEngine.Random.state),
                "Attribution leaves original fixture value/reference graph and Unity RNG unchanged / "+operation);
            trace.Add("PREVIEW_ATTRIBUTION|"+operation+"|error="+Safe(error??"none")+"|value="+Safe(value??"<profile>")+"|inputFieldPaths="+inputDiff.Count+
                "|inputRefPaths="+refDiff.Count+"|rowFieldPaths="+rowDiff.Count+"|rowRefPaths="+rowRefDiff.Count+"|output="+(output==null?"none":"captured")+"|rngChanged="+(!random.Equals(UnityEngine.Random.state))+
                "|capture="+RuntimeEntropy.Capture+"|clockTicks="+clockBefore+"->"+RuntimeEntropy.UtcNow.Ticks+"|identityEmissions="+IdentityTrace(identity));
            trace.Assert(error!=null||value!=null||output!=null,"Attribution records exact result/exception / "+operation);
        }

        static void WriteNormalizationOutputDiff(Trace trace,string name,PlayerProfile source,PlayerProfile output)
        {
            var before=FlattenNormalization(source);var after=FlattenNormalization(output);
            var fields=NormalizationDiff(before,after);
            var sourceRefs=NormalizationReferences(source);var outputRefs=NormalizationReferences(output);
            var refs=NormalizationReferenceDiff(sourceRefs,outputRefs);
            WriteNormalizationDiff(trace,name,fields,refs);
        }

        static string Safe(string value)=>value==null?"<null>":value.Replace("\r","\\r").Replace("\n","\\n");
        static string IdentityTrace(RuntimeEntropy.IdentityObservation observation)
        {
            if(observation==null)return "<unavailable>";
            int shown=Math.Min(observation.Count,32);
            return "count="+observation.Count+";overflow="+observation.Overflowed+";closed="+observation.Closed+
                ";events="+string.Join(",",Enumerable.Range(0,shown).Select(i=>observation.EventAt(i).Scope+":"+observation.EventAt(i).Value.ToString("N")))+
                ";omitted="+Math.Max(0,observation.Count-shown);
        }

        static SortedDictionary<string,string> FlattenNormalization(object root)
        {
            var result=new SortedDictionary<string,string>(StringComparer.Ordinal);
            FlattenNormalizationNode(root,"$",result,new List<object>());
            if(root is PlayerProfile profile)result["$.__catalogContent"]=CatalogSnapshot(profile);
            return result;
        }
        static void FlattenNormalizationNode(object value,string path,SortedDictionary<string,string> result,List<object> ancestors)
        {
            if(value==null){result[path]="<null>";return;}
            Type type=value.GetType();
            if(type==typeof(string)||type.IsPrimitive||type.IsEnum||type==typeof(decimal)||type==typeof(Guid)||type==typeof(TimeSpan))
            {result[path]=TypedCanonical(value,new Dictionary<string,string>(),new List<object>(),Path.GetFileName(path));return;}
            result[path+".$runtimeType"]=type.FullName??type.Name;
            if(value is UnityEngine.Object){result[path+".$unityObject"]="reference";return;}
            if(ancestors.Any(x=>ReferenceEquals(x,value))){result[path+".$cycle"]="cycle";return;}
            ancestors.Add(value);
            if(value is IList list)
            {
                result[path+".$count"]=list.Count.ToString(CultureInfo.InvariantCulture);
                for(int i=0;i<list.Count;i++)FlattenNormalizationNode(list[i],path+"["+i.ToString(CultureInfo.InvariantCulture)+"]",result,ancestors);
            }
            else foreach(var field in SerializedFields(type))FlattenNormalizationNode(field.GetValue(value),path+"."+field.Name,result,ancestors);
            ancestors.RemoveAt(ancestors.Count-1);
        }
        static SortedDictionary<string,object> NormalizationReferences(object root)
        {
            var result=new SortedDictionary<string,object>(StringComparer.Ordinal);CollectNormalizationReferences(root,"$",result,new List<object>());
            if(root is PlayerProfile profile)result["$.catalog"]=profile.catalog;
            return result;
        }
        static SortedDictionary<string,string> NormalizationReferenceTopology(object root)
        {
            var result=new SortedDictionary<string,string>(StringComparer.Ordinal);
            var seen=new Dictionary<object,string>(new ObjectReferenceComparer());
            AppendNormalizationReferenceTopology(root,"$",result,seen);
            return result;
        }
        static void AppendNormalizationReferenceTopology(object value,string path,SortedDictionary<string,string> result,Dictionary<object,string> seen)
        {
            if(value==null){result[path]="null";return;}
            Type type=value.GetType();if(type.IsValueType||type==typeof(string))return;
            if(seen.TryGetValue(value,out string first)){result[path]="alias:"+first;return;}
            seen.Add(value,path);result[path]="node:"+(type.FullName??type.Name);
            if(value is UnityEngine.Object)return;
            if(value is IList list)
            {
                result[path+".$count"]=list.Count.ToString(CultureInfo.InvariantCulture);
                for(int i=0;i<list.Count;i++)AppendNormalizationReferenceTopology(list[i],path+"["+i.ToString(CultureInfo.InvariantCulture)+"]",result,seen);
            }
            else foreach(var field in SerializedFields(type))AppendNormalizationReferenceTopology(field.GetValue(value),path+"."+field.Name,result,seen);
        }
        static void RunNormalizationWalkerSelfTests(Trace trace)
        {
            var absent=new NormalizationWalkerHolder{node=null};
            var empty=new NormalizationWalkerHolder{node=new NormalizationWalkerNode()};
            var baseNode=new NormalizationWalkerHolder{node=new NormalizationWalkerNode{value=1}};
            var derivedNode=new NormalizationWalkerHolder{node=new NormalizationWalkerDerivedNode{value=1}};
            var emptyEngineArray=new NormalizationWalkerHolder{node=Array.Empty<EngineElement>()};
            var emptyTireArray=new NormalizationWalkerHolder{node=Array.Empty<TireCondition>()};
            var shared=new List<int>{1,2};
            var aliasGraph=new NormalizationWalkerHolder{left=shared,right=shared};
            var distinctGraph=new NormalizationWalkerHolder{left=new List<int>{1,2},right=new List<int>{1,2}};
            var positiveZero=new NormalizationWalkerHolder{scalar=0f,precise=0d,order=new List<int>{1,2}};
            var negativeZero=new NormalizationWalkerHolder{scalar=BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x80000000)),0),precise=BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL)),order=new List<int>{1,2}};
            var exactOne=new NormalizationWalkerHolder{scalar=0f,precise=1d,order=new List<int>{1,2}};
            var nextDouble=new NormalizationWalkerHolder{scalar=0f,precise=BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(1d)+1),order=new List<int>{1,2}};
            var reversed=new NormalizationWalkerHolder{scalar=0f,precise=0d,order=new List<int>{2,1}};
            bool nullType=NormalizationDiff(FlattenNormalization(absent),FlattenNormalization(empty)).Any(path=>path.Contains("node.$runtimeType"));
            bool derivedType=NormalizationDiff(FlattenNormalization(baseNode),FlattenNormalization(derivedNode)).Any(path=>path.Contains("node.$runtimeType"));
            bool arrayType=NormalizationDiff(FlattenNormalization(emptyEngineArray),FlattenNormalization(emptyTireArray)).Any(path=>path.Contains("$runtimeType"));
            bool alias=NormalizationReferenceTopology(aliasGraph)["$.right"]=="alias:$.left"&&NormalizationReferenceTopology(distinctGraph)["$.right"].StartsWith("node:",StringComparison.Ordinal);
            bool signedZero=FlattenNormalization(positiveZero)["$.scalar"]!=FlattenNormalization(negativeZero)["$.scalar"];
            bool oneUlp=FlattenNormalization(exactOne)["$.precise"]!=FlattenNormalization(nextDouble)["$.precise"];
            bool order=NormalizationDiff(FlattenNormalization(positiveZero),FlattenNormalization(reversed)).Any(path=>path.Contains("order[0]"));
            trace.Assert(nullType,"Typed walker records null→object presence/runtime type");
            trace.Assert(derivedType,"Typed walker distinguishes derived DTO runtime types with identical serialized fields");
            trace.Assert(arrayType,"Typed walker distinguishes empty array runtime element types");
            trace.Assert(alias,"Typed reference-topology walker distinguishes shared aliases from distinct equal-valued nodes");
            trace.Assert(signedZero&&oneUlp&&order,"Typed walker preserves signed zero, one-ULP double precision, and collection order");
            trace.Add("NORMALIZATION_WALKER_SELFTEST|nullObject="+nullType+";derived="+derivedType+";arrayType="+arrayType+";alias="+alias+";signedZero="+signedZero+";oneUlp="+oneUlp+";order="+order);
        }
        static void CollectNormalizationReferences(object value,string path,SortedDictionary<string,object> result,List<object> ancestors)
        {
            if(value==null)return;Type type=value.GetType();if(type.IsValueType)return;
            result[path]=value;if(value is UnityEngine.Object)return;if(ancestors.Any(x=>ReferenceEquals(x,value)))return;ancestors.Add(value);
            if(value is IList list)for(int i=0;i<list.Count;i++)CollectNormalizationReferences(list[i],path+"["+i.ToString(CultureInfo.InvariantCulture)+"]",result,ancestors);
            else foreach(var field in SerializedFields(type))CollectNormalizationReferences(field.GetValue(value),path+"."+field.Name,result,ancestors);
            ancestors.RemoveAt(ancestors.Count-1);
        }
        static List<string> NormalizationDiff(SortedDictionary<string,string> before,SortedDictionary<string,string> after)
        {
            return before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal)
                .Where(key=>!before.TryGetValue(key,out var a)||!after.TryGetValue(key,out var b)||a!=b)
                .Select(key=>key+" | before="+(before.TryGetValue(key,out var a)?a:"<missing>")+" | after="+(after.TryGetValue(key,out var b)?b:"<missing>")).ToList();
        }
        static List<string> NormalizationReferenceDiff(SortedDictionary<string,object> before,SortedDictionary<string,object> after)
        {
            return before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal)
                .Where(key=>!before.TryGetValue(key,out var a)||!after.TryGetValue(key,out var b)||!ReferenceEquals(a,b))
                .Select(key=>key+" | before="+ReferenceDescription(before,key)+" | after="+ReferenceDescription(after,key)).ToList();
        }
        static string ReferenceDescription(SortedDictionary<string,object> references,string path)
        {
            if(!references.TryGetValue(path,out var value))return "<missing>";
            return value==null?"null":value.GetType().FullName+"#"+RuntimeHelpers.GetHashCode(value).ToString("X8",CultureInfo.InvariantCulture);
        }
        static void WriteNormalizationDiff(Trace trace,string name,List<string> fields,List<string> references)
        {
            string safe=new string(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray());
            string prefix=Path.Combine(trace.ReportFolder,"normalization-"+safe);
            File.WriteAllLines(prefix+"-field-diff.txt",fields,new UTF8Encoding(false));
            File.WriteAllLines(prefix+"-reference-diff.txt",references,new UTF8Encoding(false));
            trace.Add("NORMALIZATION_DIFF|"+name+"|fieldPaths="+fields.Count+";referencePaths="+references.Count+
                ";sample="+string.Join(",",fields.Take(8).Select(x=>x.Split('|')[0])));
        }
    }
}
#endif
