#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    /// <summary>Editor-only characterization for the dormant scoped whole-part move projection.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint WholePartMoveSeed=20261030u;

        public static void RunWholePartMove(Action<bool,string> check,string outputFolder)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(outputFolder))throw new ArgumentException("An isolated report folder is required.",nameof(outputFolder));
            Directory.CreateDirectory(outputFolder);
            var text=new StringBuilder();int assertions=0,failures=0;
            Action<bool,string> record=(ok,label)=>{assertions++;if(!ok)failures++;check(ok,"whole-part-move/"+label);text.AppendLine("ASSERT|"+(ok?"PASS":"FAIL")+"|"+WpmSafe(label));};
            text.AppendLine("S06D2B1I_WHOLE_PART_MOVE_PROJECTION");
            text.AppendLine("fixtureSeed="+WholePartMoveSeed+";catalogue=authored;legacy=pre-extraction frozen reference;candidate=dormant typed projection");
            try
            {
                WpmValidVectors(record,text);
                WpmTargetReservationFailure(record,text);
                WpmOwnerMaterializationNonblocking(record,text);
                WpmPrecedence(record,text);
                WpmMemberBoundaries(record,text);
                WpmSourceAudit(record,text);
            }
            catch(Exception error)
            {
                record(false,"runner exception="+error.GetType().FullName+" | "+error.Message);
                text.AppendLine("RUNNER_EXCEPTION|"+WpmSafe(error.GetType().FullName)+"|"+WpmSafe(error.Message));
            }
            finally
            {
                text.AppendLine("SUMMARY|assertions="+assertions+";failures="+failures);
                File.WriteAllText(Path.Combine(outputFolder,"S06D2B1I_WHOLE_PART_MOVE_PROJECTION.txt"),text.ToString(),new UTF8Encoding(false));
            }
        }

        static void WpmValidVectors(Action<bool,string> check,StringBuilder log)
        {
            // Each vector uses real starter-hatch hardware. Multi-owner cases buy an actual
            // matching-model car through the existing fixture helper; no component is fabricated.
            RunWpmPrepared(check,log,"car-to-storage",WholePartMoveSeed,profile=>
            {
                var car=profile.SelectedCar;var part=WpmInstalledPart(profile,car,PartCategory.Pistons);
                return Tuple.Create(part.instanceId,InventoryKind.Storage,SharedInventory.Garage);
            });
            RunWpmPrepared(check,log,"storage-to-car-empty-slot",WholePartMoveSeed+1,profile=>
            {
                var car=profile.SelectedCar;var part=WpmInstalledPart(profile,car,PartCategory.Pistons);
                LegacyWholePartMoveReference.Move(profile,part.instanceId,InventoryKind.Storage,SharedInventory.Garage);
                return Tuple.Create(part.instanceId,InventoryKind.Car,car.instanceId);
            });
            RunWpmPrepared(check,log,"storage-to-storage",WholePartMoveSeed+7,profile=>
            {
                var part=WpmInstalledPart(profile,profile.SelectedCar,PartCategory.Pistons);
                LegacyWholePartMoveReference.Move(profile,part.instanceId,InventoryKind.Storage,SharedInventory.Garage);
                return Tuple.Create(part.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            });
            RunWpmPrepared(check,log,"car-to-car-occupied-slot",WholePartMoveSeed+2,profile=>
            {
                if(!GraphCreateSameModelPair(profile,out var donor,out var target,out string reason))throw new InvalidOperationException("OPEN: "+reason);
                var part=WpmInstalledPart(profile,donor,PartCategory.Pistons);
                if(WpmInstalledPart(profile,target,PartCategory.Pistons)==null)throw new InvalidOperationException("OPEN: target lacks actual fitted Pistons slot");
                return Tuple.Create(part.instanceId,InventoryKind.Car,target.instanceId);
            });
            RunWpmPrepared(check,log,"car-to-detached-engine-occupied-slot",WholePartMoveSeed+3,profile=>
            {
                if(!GraphCreateSameModelPair(profile,out var donor,out var target,out string reason))throw new InvalidOperationException("OPEN: "+reason);
                var engine=WpmDetachOwnedEngineToStorage(profile,target.instanceId);
                var part=WpmInstalledPart(profile,donor,PartCategory.Pistons);
                if(WpmEnginePart(profile,engine,PartCategory.Pistons)==null)throw new InvalidOperationException("OPEN: detached target lacks actual fitted Pistons slot");
                return Tuple.Create(part.instanceId,InventoryKind.Engine,engine.instanceId);
            });
            RunWpmPrepared(check,log,"detached-engine-to-car-occupied-slot",WholePartMoveSeed+4,profile=>
            {
                if(!GraphCreateSameModelPair(profile,out var donor,out var target,out string reason))throw new InvalidOperationException("OPEN: "+reason);
                var engine=WpmDetachOwnedEngineToStorage(profile,donor.instanceId);
                var part=WpmEnginePart(profile,engine,PartCategory.Pistons);
                if(WpmInstalledPart(profile,target,PartCategory.Pistons)==null)throw new InvalidOperationException("OPEN: car target lacks actual fitted Pistons slot");
                return Tuple.Create(part.instanceId,InventoryKind.Car,target.instanceId);
            });
            RunWpmPrepared(check,log,"detached-engine-to-storage",WholePartMoveSeed+5,profile=>
            {
                var car=profile.SelectedCar;var engine=WpmDetachOwnedEngineToStorage(profile,car.instanceId);
                var part=WpmEnginePart(profile,engine,PartCategory.Pistons);
                return Tuple.Create(part.instanceId,InventoryKind.Storage,SharedInventory.Garage);
            });
            RunWpmPrepared(check,log,"storage-to-detached-engine-empty-slot",WholePartMoveSeed+6,profile=>
            {
                var car=profile.SelectedCar;var engine=WpmDetachOwnedEngineToStorage(profile,car.instanceId);
                var part=WpmEnginePart(profile,engine,PartCategory.Pistons);
                LegacyWholePartMoveReference.Move(profile,part.instanceId,InventoryKind.Storage,SharedInventory.Garage);
                return Tuple.Create(part.instanceId,InventoryKind.Engine,engine.instanceId);
            });
            WpmMismatchedSourceBinding(check,log);
            WpmDuplicateOwnerResolution(check,log);
        }

        static PartInstance WpmInstalledPart(PlayerProfile profile,OwnedCar car,PartCategory category)
        {
            var binding=car?.installed?.slots?.FirstOrDefault(value=>value!=null&&value.slot==category.ToString());
            return binding==null?null:profile.parts.Find(value=>value!=null&&value.instanceId==binding.partInstanceId);
        }

        static EngineAssemblyInstance WpmDetachOwnedEngineToStorage(PlayerProfile profile,string carId)
        {
            var car=profile.cars.Find(value=>value!=null&&value.instanceId==carId)??throw new InvalidOperationException("The fixture car to detach does not exist.");
            var engine=profile.engines.Find(value=>value!=null&&value.instanceId==car.engineAssemblyId&&value.parentVehicleId==car.instanceId)
                ??throw new InvalidOperationException("The fixture car has no attached real engine to detach.");
            LegacyWholePartMoveReference.Move(profile,engine.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            if(!string.IsNullOrEmpty(engine.parentVehicleId)||engine.storageId!=SharedInventory.Storage)
                throw new InvalidOperationException("The frozen whole-part reference did not detach the real engine into Storage.");
            return engine;
        }

        static PartInstance WpmEnginePart(PlayerProfile profile,EngineAssemblyInstance engine,PartCategory category)
        {
            var binding=engine?.slots?.FirstOrDefault(value=>value!=null&&value.slot==category.ToString());
            return binding==null?null:profile.parts.Find(value=>value!=null&&value.instanceId==binding.partInstanceId);
        }

        static void RunWpmPrepared(Action<bool,string> check,StringBuilder log,string name,uint seed,
            Func<PlayerProfile,Tuple<string,InventoryKind,string>> prepare)
        {
            PlayerProfile profile;string carId;
            CartPreviewCharacterizationFixtures.CreateNormalized(out profile,out carId,seed);
            Tuple<string,InventoryKind,string> request;
            try{request=prepare(profile);}
            catch(Exception error)
            {
                log.AppendLine("OPEN|"+name+"|"+WpmSafe(error.GetType().FullName+" | "+error.Message));
                check(false,"real authored fixture available / "+name+" / "+error.GetType().FullName+" | "+error.Message);return;
            }
            string setupError=MechanicsOwnership.Validate(profile,profile.catalog);
            check(setupError==null,"real operation fixture remains authoritative-valid before projection / "+name+" / "+(setupError??"valid"));
            if(setupError!=null)return;
            RunWpmReferenceComparison(check,log,name,profile,request.Item1,request.Item2,request.Item3,true);
        }

        static void RunWpmReferenceComparison(Action<bool,string> check,StringBuilder log,string name,PlayerProfile profile,
            string assetId,InventoryKind target,string targetId,bool expectSuccess)
        {
            string sourceBefore=SourceSnapshot(profile),catalogBefore=CatalogSnapshot(profile);
            var sourceRefsBefore=NormalizationReferences(profile);
            long jsonBefore=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            var randomBefore=UnityEngine.Random.state;
            bool captureBefore=RuntimeEntropy.Capture;DateTime clockBefore=RuntimeEntropy.UtcNow;
            var identity=RuntimeEntropy.BeginIdentityObservation(64);
            WholePartMoveResult result=null;Exception thrown=null;
            try{result=WholePartMoveProjection.Evaluate(profile,assetId,target,targetId,profile.catalog);}
            catch(Exception error){thrown=error;}
            finally{identity.Dispose();}
            check(identity.Closed&&!identity.Overflowed&&identity.Count==0,"projection emits no IDs / "+name);
            check(jsonBefore==RuntimeDataEncodingProbe.CurrentThreadJsonCallCount,"projection calls no instrumented JSON route / "+name);
            check(randomBefore.Equals(UnityEngine.Random.state),"projection preserves Unity RNG / "+name);
            bool captureAfter=RuntimeEntropy.Capture;DateTime clockAfter=RuntimeEntropy.UtcNow;
            check(captureBefore==captureAfter,"projection preserves RuntimeEntropy mode / "+name);
            if(captureBefore)check(clockBefore.Ticks==clockAfter.Ticks&&clockBefore.Kind==clockAfter.Kind,"captured RuntimeEntropy clock stays exact / "+name);
            check(sourceBefore==SourceSnapshot(profile)&&NormalizationReferenceDiff(sourceRefsBefore,NormalizationReferences(profile)).Count==0,
                "projection preserves complete source values and reference topology / "+name);
            check(catalogBefore==CatalogSnapshot(profile),"projection preserves borrowed catalogue / "+name);

            var legacy=CartPreviewLiteralTestClone.Copy(profile);Exception legacyError=null;
            try{LegacyWholePartMoveReference.Move(legacy,assetId,target,targetId);}
            catch(Exception error){legacyError=error;}
            WpmLiveMatchesFrozen(check,name,profile,assetId,target,targetId,legacy,legacyError);
            bool typedSuccess=thrown==null&&result!=null&&result.Success;
            bool legacySuccess=legacyError==null;
            check(typedSuccess==expectSuccess,"typed outcome matches expected success state / "+name+" / "+WpmOutcome(result,thrown));
            check(legacySuccess==expectSuccess,"frozen legacy reference matches expected success state / "+name+" / "+WpmException(legacyError));
            if(!expectSuccess)
            {
                check(thrown==null&&result!=null&&!result.Success&&result.ErrorType==legacyError?.GetType().FullName&&result.ErrorMessage==legacyError?.Message,
                    "typed rejection matches frozen legacy first exception / "+name+" / typed="+WpmOutcome(result,thrown)+";legacy="+WpmException(legacyError));
                check(result==null||result.MovedPart==null&&result.DisplacedPart==null&&result.ChangedParts==null&&(result.Owners==null||result.Owners.Length==0),
                    "rejection publishes no partial graph or owner snapshots / "+name);
                log.AppendLine("VECTOR|"+name+"|result="+WpmSafe(WpmOutcome(result,thrown))+";reference="+WpmSafe(WpmException(legacyError))+";success=false");
                return;
            }
            if(!typedSuccess||!legacySuccess)return;
            var expectedMoved=legacy.parts.Find(value=>value!=null&&value.instanceId==assetId)?.Copy();
            check(TypedCanonical(expectedMoved,new Dictionary<string,string>())==TypedCanonical(result.MovedPart,new Dictionary<string,string>()),
                "moved physical part and nested condition equal frozen result / "+name);
            var expectedDisplaced=profile.parts.Where(old=>old!=null&&old.instanceId!=assetId)
                .Select(old=>legacy.parts.Find(value=>value!=null&&value.instanceId==old.instanceId))
                .FirstOrDefault(value=>value!=null&&value.location==PartLocation.Inventory&&
                    profile.parts.Any(old=>old!=null&&old.instanceId==value.instanceId&&old.location!=PartLocation.Inventory));
            check(TypedCanonical(expectedDisplaced?.Copy(),new Dictionary<string,string>())==TypedCanonical(result.DisplacedPart,new Dictionary<string,string>()),
                "displaced physical identity/condition exactly matches frozen result / "+name);
            var expectedChanged=WpmExpectedChangedParts(profile,assetId,target,targetId);
            var netChanged=profile.parts.Where(old=>old!=null)
                .Where(old=>legacy.parts.Any(value=>value!=null&&value.instanceId==old.instanceId&&
                    TypedCanonical(old,new Dictionary<string,string>())!=TypedCanonical(legacy.parts.Find(item=>item.instanceId==old.instanceId),new Dictionary<string,string>())))
                .Select(old=>old.instanceId).ToArray();
            var changedParts=result.ChangedParts??Array.Empty<PartInstance>();
            var actualChanged=changedParts.Where(value=>value!=null).Select(value=>value.instanceId).ToArray();
            check(actualChanged.Length==changedParts.Length&&actualChanged.Distinct(StringComparer.Ordinal).Count()==actualChanged.Length&&
                new HashSet<string>(actualChanged,StringComparer.Ordinal).SetEquals(expectedChanged),
                "ChangedParts contains exactly the transition-written records / "+name+";expected="+string.Join(",",expectedChanged)+";actual="+string.Join(",",actualChanged));
            check(netChanged.All(id=>actualChanged.Contains(id,StringComparer.Ordinal)),"ChangedParts covers every net-changed physical record / "+name+";net="+string.Join(",",netChanged));
            foreach(var changed in changedParts.Where(value=>value!=null))
            {
                var expected=legacy.parts.Find(value=>value!=null&&value.instanceId==changed.instanceId);
                check(TypedCanonical(changed,new Dictionary<string,string>())==TypedCanonical(expected?.Copy(),new Dictionary<string,string>()),
                    "ChangedParts value equals frozen post-move physical record / "+name+" / "+changed.instanceId);
                check(!profile.parts.Any(value=>ReferenceEquals(value,changed)),"ChangedParts owns deep records outside the input graph / "+name+" / "+changed.instanceId);
            }
            check(!ReferenceEquals(result.MovedPart,profile.parts.Find(value=>value.instanceId==assetId)),"MovedPart is an owned record outside the input graph / "+name);
            if(result.DisplacedPart!=null)check(!profile.parts.Any(value=>ReferenceEquals(value,result.DisplacedPart)),"DisplacedPart is an owned record outside the input graph / "+name);
            check(result.Owners!=null,"successful projection returns an owned touched-owner snapshot array / "+name);
            if(result.Owners!=null)
            {
                var expectedOrder=WpmExpectedOwnerOrder(profile,assetId,target,targetId);
                var actualOrder=result.Owners.Select(owner=>owner.Kind.ToString()+"/"+owner.Id).ToArray();
                check(expectedOrder.SequenceEqual(actualOrder,StringComparer.Ordinal),"source then destination touched-owner order is exact / "+name+
                    ";expected="+string.Join(",",expectedOrder)+";actual="+string.Join(",",actualOrder));
                foreach(var owner in result.Owners)
                {
                    string ownerDifference=WpmOwnerFirstDifference(owner,legacy);
                    check(ownerDifference==null,"owner bindings/order/tune/parts/resources/inspection match frozen result / "+name+" / "+owner.Kind+"/"+owner.Id+
                        (ownerDifference==null?"":";firstDiff="+ownerDifference));
                    check(owner.Materialized&&owner.BuildErrorType==null&&owner.BuildErrorMessage==null,
                        "valid owner snapshot materializes without optional-query error / "+name+" / "+owner.Kind+"/"+owner.Id);
                    if(name=="car-to-storage"&&owner.Kind.ToString()=="Car")WpmTireBackingTypeProbe(check,log,name,legacy,owner);
                    if(owner.Kind.ToString()=="Car")
                    {
                        var car=legacy.cars.Find(value=>value.instanceId==owner.Id);
                        var current=car==null?null:LegacyInstalledBuildReference.Resolve(legacy,car,legacy.catalog);
                        check(car!=null&&TypedCanonical(owner.Inspection?.build,new Dictionary<string,string>())==TypedCanonical(current,new Dictionary<string,string>()),
                            "car owner output inspection matches frozen original build resolver / "+name+" / "+owner.Id);
                    }
                    else if(owner.Kind.ToString()=="Engine")
                    {
                        var engine=legacy.engines.Find(value=>value.instanceId==owner.Id);
                        var expectedBuilt=engine==null?null:legacy.catalog.Resolve(engine.vehicleDefinitionId,LegacyWholePartMoveReference.EngineDefinitions(legacy,engine),engine.tune);
                        var projectedBuilt=engine==null?null:legacy.catalog.Resolve(owner.VehicleDefinitionId,owner.Definitions,owner.Tune);
                        check(engine!=null&&owner.Inspection==null&&expectedBuilt!=null&&
                            TypedCanonical(expectedBuilt,new Dictionary<string,string>())==TypedCanonical(projectedBuilt,new Dictionary<string,string>()),
                            "stored-engine build equals authoritative ordered-definition resolution without car inspection / "+name+" / "+owner.Id);
                    }
                }
            }
            log.AppendLine("VECTOR|"+name+"|result=success;owners="+(result.Owners==null?"<null>":string.Join(",",result.Owners.Select(owner=>owner.Kind+"/"+owner.Id)))+
                ";changedParts="+string.Join(",",actualChanged)+";moved="+WpmSafe(result.MovedPart?.instanceId)+";displaced="+WpmSafe(result.DisplacedPart?.instanceId));
            if(name=="car-to-car-occupied-slot")WpmOutputIsolation(check,name,profile,assetId,target,targetId,result,sourceBefore);
        }

        static string WpmResultFacts(WholePartMoveResult result)
            =>TypedCanonical(new object[]{result.MovedPart,result.DisplacedPart,result.ChangedParts,
                result.Owners?.Select(owner=>owner==null?null:WpmOwnerFingerprint(owner)).ToArray()},new Dictionary<string,string>());

        // Snapshot fields are readonly and definition maps are not Unity-serialized.
        // Enumerate these explicitly; the general serialized-field oracle stays unchanged.
        static string WpmOwnerFingerprint(WholePartMoveOwnerSnapshot owner)
            =>TypedCanonical(WpmSnapshotValues(owner).Select(pair=>new object[]{pair.Key,WpmOwnerFieldCanonical(pair.Value)}).ToArray(),new Dictionary<string,string>());

        static string WpmOwnerFieldCanonical(object value)
            =>TypedCanonical(value is IDictionary<PartCategory,string> definitions
                ?definitions.Select(pair=>new object[]{pair.Key,pair.Value}).ToArray():value,new Dictionary<string,string>());

        static void WpmOutputIsolation(Action<bool,string> check,string name,PlayerProfile profile,string assetId,
            InventoryKind target,string targetId,WholePartMoveResult first,string sourceBefore)
        {
            string outputBefore=WpmResultFacts(first);
            if(first.MovedPart?.condition!=null)
            {
                first.MovedPart.condition.health=first.MovedPart.condition.health>.5f?.25f:.75f;
                var member=first.MovedPart.condition.elements?.FirstOrDefault(value=>value!=null);
                if(member!=null)member.health=member.health>.5f?.25f:.75f;
            }
            var changed=first.ChangedParts?.FirstOrDefault(value=>value!=null&&value.condition!=null);
            if(changed!=null)changed.condition.distanceM+=1.25;
            var owner=first.Owners?.FirstOrDefault(value=>value!=null);
            string ownerBefore=owner==null?null:WpmOwnerFingerprint(owner);
            if(owner?.Tune!=null)owner.Tune.finalDrive+=.03125f;
            if(owner?.Bindings!=null&&owner.Bindings.Length>0)
                owner.Bindings[0].partInstanceId=first.DisplacedPart?.instanceId??assetId;
            if(owner?.Parts!=null&&owner.Parts.Length>0&&owner.Parts[0]?.condition!=null)
                owner.Parts[0].condition.health=owner.Parts[0].condition.health>.5f?.25f:.75f;
            if(owner?.Definitions!=null&&owner.Definitions.Count>0)
            {
                string mapBefore=WpmOwnerFieldCanonical(owner.Definitions);
                owner.Definitions[owner.Definitions.First().Key]="output-only-copy-probe";
                check(mapBefore!=WpmOwnerFieldCanonical(owner.Definitions),"owner fingerprint observes definition-map value mutation / "+name);
            }
            if(owner!=null)check(ownerBefore!=WpmOwnerFingerprint(owner),"owner fingerprint observes readonly snapshot payload mutation / "+name);
            check(sourceBefore==SourceSnapshot(profile),"mutating nested moved/displaced/changed/owner outputs cannot change source / "+name);
            var second=WholePartMoveProjection.Evaluate(profile,assetId,target,targetId,profile.catalog);
            check(second!=null&&second.Success&&WpmResultFacts(second)==outputBefore,
                "mutating nested output facts cannot affect later evaluation / "+name);
        }

        static string[] WpmExpectedOwnerOrder(PlayerProfile profile,string assetId,InventoryKind destination,string destinationId)
        {
            var order=new List<string>();
            var part=profile.parts.Find(value=>value!=null&&value.instanceId==assetId);
            if(part!=null&&part.location!=PartLocation.Inventory)
            {
                var car=profile.cars.FirstOrDefault(value=>value!=null&&value.installed.slots.Any(slot=>slot!=null&&slot.partInstanceId==assetId));
                if(car!=null)order.Add("Car/"+car.instanceId);
                else if(!string.IsNullOrEmpty(part.ownerId)&&profile.engines.Any(value=>value!=null&&value.instanceId==part.ownerId))order.Add("Engine/"+part.ownerId);
            }
            if(destination==InventoryKind.Car)order.Add("Car/"+destinationId);
            else if(destination==InventoryKind.Engine)order.Add("Engine/"+destinationId);
            return order.Distinct(StringComparer.Ordinal).ToArray();
        }

        static string[] WpmExpectedChangedParts(PlayerProfile profile,string assetId,InventoryKind destination,string destinationId)
        {
            var result=new List<string>();
            var requested=profile.parts.Find(value=>value!=null&&value.instanceId==assetId);
            if(requested==null)return result.ToArray();
            PartCategory category=profile.catalog.Find(requested.definitionId).category;
            if(requested.location!=PartLocation.Inventory)
            {
                var car=profile.cars.FirstOrDefault(value=>value!=null&&value.installed.slots.Any(slot=>slot!=null&&slot.partInstanceId==assetId));
                SlotBinding removed=car!=null
                    ?car.installed.slots.FirstOrDefault(slot=>slot!=null&&slot.slot==category.ToString())
                    :profile.engines.FirstOrDefault(value=>value!=null&&value.instanceId==requested.ownerId)?.slots.FirstOrDefault(slot=>slot!=null&&slot.slot==category.ToString());
                if(removed!=null)result.Add(removed.partInstanceId);
            }
            if(destination==InventoryKind.Car)
            {
                var target=profile.cars.Find(value=>value!=null&&value.instanceId==destinationId);
                var displaced=target?.installed?.slots?.FirstOrDefault(slot=>slot!=null&&slot.slot==category.ToString())?.partInstanceId;
                if(!string.IsNullOrEmpty(displaced)&&displaced!=assetId)result.Add(displaced);
                if(requested.location==PartLocation.Inventory)result.Add(assetId);
            }
            else if(destination==InventoryKind.Engine)
            {
                var target=profile.engines.Find(value=>value!=null&&value.instanceId==destinationId);
                var displaced=target?.slots?.FirstOrDefault(slot=>slot!=null&&slot.slot==category.ToString())?.partInstanceId;
                if(!string.IsNullOrEmpty(displaced)&&displaced!=assetId)result.Add(displaced);
                if(requested.location==PartLocation.Inventory)result.Add(assetId);
            }
            else if(destination==InventoryKind.Storage&&
                (requested.location!=PartLocation.Inventory||SharedInventory.StorageOf(requested.storageId)!=destinationId))result.Add(assetId);
            return result.Distinct(StringComparer.Ordinal).ToArray();
        }

        static string WpmOwnerFirstDifference(WholePartMoveOwnerSnapshot owner,PlayerProfile profile)
        {
            var actual=WpmSnapshotValues(owner);var expected=WpmProfileOwnerValues(profile,owner.Kind.ToString(),owner.Id);
            foreach(var pair in actual)
            {
                object expectedValue;
                if(!expected.TryGetValue(pair.Key,out expectedValue))return pair.Key+"|expected-field-missing";
                string left=WpmOwnerFieldCanonical(pair.Value);
                string right=WpmOwnerFieldCanonical(expectedValue);
                if(left!=right)
                {
                    var differences=NormalizationDiff(FlattenNormalization(pair.Value),FlattenNormalization(expectedValue));
                    string path=differences.Count==0?"$ (explicit field encoding)":differences[0].Split(new[]{" | before="},StringSplitOptions.None)[0];
                    return pair.Key+"|"+WpmSafe(path)+"|actual="+WpmSafe(left)+"|expected="+WpmSafe(right);
                }
            }
            return null;
        }

        static void WpmTireBackingTypeProbe(Action<bool,string> check,StringBuilder log,string name,
            PlayerProfile profile,WholePartMoveOwnerSnapshot owner)
        {
            int index=Array.FindIndex(owner.Bindings,binding=>binding!=null&&binding.slot==PartCategory.Tires.ToString());
            check(index>=0&&owner.Parts!=null&&index<owner.Parts.Length,"real owner snapshot contains an authored tire pack for backing-array probe / "+name);
            if(index<0||owner.Parts==null||index>=owner.Parts.Length)return;
            var source=profile.parts.Find(part=>part!=null&&part.instanceId==owner.Bindings[index].partInstanceId);
            var copied=source?.Copy();var output=owner.Parts[index];
            string rawType=source?.condition?.elements?.GetType().FullName??"<null>";
            string copiedType=copied?.condition?.elements?.GetType().FullName??"<null>";
            string outputType=output?.condition?.elements?.GetType().FullName??"<null>";
            bool exact=TypedCanonical(copied,new Dictionary<string,string>())==TypedCanonical(output,new Dictionary<string,string>());
            check(exact,"owned tire snapshot exactly equals the frozen physical PartInstance.Copy result / "+name);
            log.AppendLine("OWNER_ARRAY_TYPE|"+name+"|slot=Tires;rawElements="+WpmSafe(rawType)+";copiedElements="+WpmSafe(copiedType)+
                ";snapshotElements="+WpmSafe(outputType)+";exactCopiedValues="+exact);
        }

        static SortedDictionary<string,object> WpmSnapshotValues(WholePartMoveOwnerSnapshot owner)
        {
            var values=new SortedDictionary<string,object>(StringComparer.Ordinal)
            {
                ["Bindings"]=owner.Bindings,["BuildErrorMessage"]=owner.BuildErrorMessage,["BuildErrorType"]=owner.BuildErrorType,
                ["Definitions"]=owner.Definitions,["EngineAssemblyId"]=owner.EngineAssemblyId,["Fuel"]=owner.Fuel,
                ["Fluids"]=owner.Fluids,["GarageJobId"]=owner.GarageJobId,["HasRebuildJob"]=owner.HasRebuildJob,
                ["Id"]=owner.Id,["Inspection"]=owner.Inspection,["Kind"]=owner.Kind.ToString(),["Materialized"]=owner.Materialized,
                ["ParentVehicleId"]=owner.ParentVehicleId,["Parts"]=owner.Parts,["RebuildActive"]=owner.RebuildActive,
                ["Tune"]=owner.Tune,["VehicleDefinitionId"]=owner.VehicleDefinitionId,["VehicleId"]=owner.VehicleId
            };
            return values;
        }

        static SortedDictionary<string,object> WpmProfileOwnerValues(PlayerProfile profile,string kind,string id)
        {
            var values=new SortedDictionary<string,object>(StringComparer.Ordinal);
            if(kind=="Car")
            {
                var car=profile.cars.Find(value=>value.instanceId==id);
                if(car==null)return values;
                var bindings=car.installed.slots.Select(binding=>binding==null?null:new SlotBinding{slot=binding.slot,partInstanceId=binding.partInstanceId}).ToArray();
                var parts=bindings.Select(binding=>string.IsNullOrWhiteSpace(binding?.partInstanceId)?null:profile.parts.Find(part=>part.instanceId==binding.partInstanceId)?.Copy()).ToArray();
                bool materialized=parts.All(part=>part!=null);Dictionary<PartCategory,string> definitions=null;InspectionBuild inspection=null;
                string buildErrorType=null,buildErrorMessage=null;
                if(materialized)
                {
                    try
                    {
                        definitions=LegacyWholePartMoveReference.CarDefinitions(profile,car);
                        inspection=LegacyInstalledBuildReference.Capture(profile,car);
                    }
                    catch(Exception error){materialized=false;parts=null;definitions=null;inspection=null;buildErrorType=error.GetType().FullName;buildErrorMessage=error.Message;}
                }
                values["Bindings"]=bindings;values["BuildErrorMessage"]=buildErrorMessage;values["BuildErrorType"]=buildErrorType;
                values["Definitions"]=definitions;values["EngineAssemblyId"]=car.engineAssemblyId;values["Fuel"]=car.fuel?.Copy();
                values["Fluids"]=car.fluids?.Copy();values["GarageJobId"]=car.garageJobId;values["HasRebuildJob"]=car.engineRebuild!=null;
                values["Id"]=id;values["Inspection"]=inspection;values["Kind"]=kind;values["Materialized"]=materialized;
                values["ParentVehicleId"]=null;values["Parts"]=parts;values["RebuildActive"]=car.engineRebuild!=null&&car.engineRebuild.active;
                values["Tune"]=car.tune?.Copy();values["VehicleDefinitionId"]=null;values["VehicleId"]=car.vehicleId;
                return values;
            }
            var engine=profile.engines.Find(value=>value.instanceId==id);
            if(engine==null)return values;
            var engineBindings=engine.slots.Select(binding=>binding==null?null:new SlotBinding{slot=binding.slot,partInstanceId=binding.partInstanceId}).ToArray();
            PartInstance[] engineParts=null;Dictionary<PartCategory,string> engineDefinitions=null;bool engineMaterialized=false;
            string engineErrorType=null,engineErrorMessage=null;
            try
            {
                engineParts=engineBindings.Select(binding=>binding==null||string.IsNullOrWhiteSpace(binding.partInstanceId)?null:
                    profile.parts.Find(part=>part.instanceId==binding.partInstanceId)?.Copy()).ToArray();
                if(engineParts.Any(part=>part==null))throw new InvalidOperationException("Affected engine contains an unresolved installed-part binding.");
                engineDefinitions=LegacyWholePartMoveReference.EngineDefinitions(profile,engine);engineMaterialized=true;
            }
            catch(Exception error){engineParts=null;engineDefinitions=null;engineErrorType=error.GetType().FullName;engineErrorMessage=error.Message;}
            values["Bindings"]=engineBindings;values["BuildErrorMessage"]=engineErrorMessage;values["BuildErrorType"]=engineErrorType;
            values["Definitions"]=engineDefinitions;values["EngineAssemblyId"]=null;values["Fuel"]=null;values["Fluids"]=null;
            values["GarageJobId"]=null;values["HasRebuildJob"]=false;values["Id"]=id;values["Inspection"]=null;values["Kind"]=kind;
            values["Materialized"]=engineMaterialized;values["ParentVehicleId"]=engine.parentVehicleId;values["Parts"]=engineParts;
            values["RebuildActive"]=false;values["Tune"]=engine.tune?.Copy();values["VehicleDefinitionId"]=engine.vehicleDefinitionId;values["VehicleId"]=null;
            return values;
        }

        static string WpmScalar(object value)=>value==null?"<null>":Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);

        static void WpmMismatchedSourceBinding(Action<bool,string> check,StringBuilder log)
        {
            const string name="definition-category-removes-first-bound-slot-before-storage";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,WholePartMoveSeed+40);
            var car=profile.cars.Find(value=>value.instanceId==carId);
            var requested=WpmInstalledPart(profile,car,PartCategory.Pistons);
            var actualRods=WpmInstalledPart(profile,car,PartCategory.Rods);
            if(requested==null||actualRods==null){log.AppendLine("OPEN|"+name+"|actual fitted Pistons and Rods parts unavailable");check(false,"actual fitted category mismatch fixture exists / "+name);return;}
            string realRodsDefinition=actualRods.definitionId;
            // Intentional malformed source: use an actual authored fitted definition, no fabricated asset.
            profile.parts.Find(value=>value.instanceId==requested.instanceId).definitionId=realRodsDefinition;
            WpmMismatchedBindingComparison(check,log,name,profile,requested.instanceId,SharedInventory.Garage);
        }

        static void WpmMismatchedBindingComparison(Action<bool,string> check,StringBuilder log,string name,PlayerProfile profile,string assetId,string storageId)
        {
            string before=SourceSnapshot(profile),catalog=CatalogSnapshot(profile);var refs=NormalizationReferences(profile);
            var legacy=CartPreviewLiteralTestClone.Copy(profile);Exception error=null;
            try{LegacyWholePartMoveReference.Move(legacy,assetId,InventoryKind.Storage,storageId);}catch(Exception caught){error=caught;}
            WpmLiveMatchesFrozen(check,name,profile,assetId,InventoryKind.Storage,storageId,legacy,error);
            long json=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;var random=UnityEngine.Random.state;
            bool entropyMode=RuntimeEntropy.Capture;DateTime entropyClock=RuntimeEntropy.UtcNow;
            var identity=RuntimeEntropy.BeginIdentityObservation(32);WholePartMoveResult result=null;Exception typedError=null;
            try{result=WholePartMoveProjection.Evaluate(profile,assetId,InventoryKind.Storage,storageId,profile.catalog);}catch(Exception caught){typedError=caught;}finally{identity.Dispose();}
            check(error==null&&typedError==null&&result!=null&&result.Success,"typed mismatch-owner route keeps frozen legacy successful outcome / "+name+
                ";legacy="+WpmException(error)+";typed="+WpmOutcome(result,typedError));
            check(before==SourceSnapshot(profile)&&NormalizationReferenceDiff(refs,NormalizationReferences(profile)).Count==0&&catalog==CatalogSnapshot(profile),"mismatched owner projection preserves its deliberately malformed input / "+name);
            check(json==RuntimeDataEncodingProbe.CurrentThreadJsonCallCount&&random.Equals(UnityEngine.Random.state)&&identity.Count==0&&identity.Closed&&!identity.Overflowed,
                "mismatched owner evaluation emits no JSON/identity and preserves RNG / "+name);
            DateTime entropyAfter=RuntimeEntropy.UtcNow;
            check(RuntimeEntropy.Capture==entropyMode&&(!entropyMode||entropyClock.Ticks==entropyAfter.Ticks&&entropyClock.Kind==entropyAfter.Kind),
                "mismatched owner evaluation preserves RuntimeEntropy mode and captured clock / "+name);
            if(result==null||!result.Success||error!=null)return;
            var expectedChanged=WpmExpectedChangedParts(profile,assetId,InventoryKind.Storage,storageId);
            var actualChanged=result.ChangedParts?.Where(value=>value!=null).Select(value=>value.instanceId).ToArray()??Array.Empty<string>();
            check(new HashSet<string>(actualChanged,StringComparer.Ordinal).SetEquals(expectedChanged)&&actualChanged.Length==expectedChanged.Length,
                "ChangedParts exactly names mismatched requested and actual removed physical records / "+name+
                ";expected="+string.Join(",",expectedChanged)+";actual="+string.Join(",",actualChanged));
            foreach(string id in expectedChanged)
            {
                var actual=result.ChangedParts.First(value=>value!=null&&value.instanceId==id);
                var expected=legacy.parts.Find(value=>value!=null&&value.instanceId==id);
                check(TypedCanonical(actual,new Dictionary<string,string>())==TypedCanonical(expected,new Dictionary<string,string>()),
                    "mismatch ChangedParts record equals exact frozen effect / "+name+" / "+id);
            }
            var owner=result.Owners?.FirstOrDefault(value=>value!=null&&value.Kind.ToString()=="Car");
            var referenceCar=legacy.cars.FirstOrDefault(value=>value!=null&&value.instanceId==profile.cars.First(car=>car.installed.slots.Any(slot=>slot.partInstanceId==assetId)).instanceId);
            check(result.Owners?.Length==1&&owner!=null&&referenceCar!=null&&TypedCanonical(owner.Bindings,new Dictionary<string,string>())==TypedCanonical(referenceCar.installed.slots.ToArray(),new Dictionary<string,string>())&&
                TypedCanonical(owner.Tune,new Dictionary<string,string>())==TypedCanonical(referenceCar.tune,new Dictionary<string,string>()),
                "mismatched owner snapshot preserves the exact surviving ordered bindings and tune / "+name);
            var afterRequest=legacy.parts.Find(value=>value.instanceId==assetId);
            var afterRods=legacy.parts.Find(value=>value.instanceId!=assetId&&value.location==PartLocation.Inventory&&
                profile.parts.Any(old=>old.instanceId==value.instanceId&&old.slot==PartCategory.Rods.ToString()));
            check(afterRequest!=null&&afterRequest.location==PartLocation.Assembly&&afterRequest.slot==PartCategory.Pistons.ToString()&&
                afterRequest.storageId==storageId&&afterRods!=null&&afterRods.ownerId==""&&afterRods.slot=="",
                "legacy exact partial graph records wrong-category remove and requested-ID storage assignment / "+name);
            log.AppendLine("EDGE|"+name+"|changedParts="+string.Join(",",actualChanged)+";requestedStillBound=true;referenceRemovedFirstRods=true");
        }

        static void WpmOwnerMaterializationNonblocking(Action<bool,string> check,StringBuilder log)
        {
            const string name="successful-move-with-unmaterializable-optional-owner-view";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,WholePartMoveSeed+42);
            var car=profile.cars.Find(value=>value.instanceId==carId);
            var requested=WpmInstalledPart(profile,car,PartCategory.Pistons);
            var unrelated=car.installed.slots.FirstOrDefault(value=>value!=null&&value.partInstanceId!=requested?.instanceId);
            if(requested==null||unrelated==null){log.AppendLine("OPEN|"+name+"|actual unrelated installed binding unavailable");check(false,"real unrelated binding exists / "+name);return;}
            car.fluids=null;
            var unrelatedPart=profile.parts.Find(value=>value!=null&&value.instanceId==unrelated.partInstanceId);
            if(unrelatedPart==null){log.AppendLine("OPEN|"+name+"|unrelated bound physical part unavailable");check(false,"unrelated bound physical part exists / "+name);return;}
            unrelatedPart.ownerId="";
            string before=SourceSnapshot(profile),catalog=CatalogSnapshot(profile);var refs=NormalizationReferences(profile);
            var legacy=CartPreviewLiteralTestClone.Copy(profile);Exception legacyError=null;
            try{LegacyWholePartMoveReference.Move(legacy,requested.instanceId,InventoryKind.Storage,SharedInventory.Garage);}catch(Exception error){legacyError=error;}
            WpmLiveMatchesFrozen(check,name,profile,requested.instanceId,InventoryKind.Storage,SharedInventory.Garage,legacy,legacyError);
            long json=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;var random=UnityEngine.Random.state;
            bool entropyMode=RuntimeEntropy.Capture;DateTime entropyClock=RuntimeEntropy.UtcNow;
            var identity=RuntimeEntropy.BeginIdentityObservation(32);WholePartMoveResult result=null;Exception typedError=null;
            try{result=WholePartMoveProjection.Evaluate(profile,requested.instanceId,InventoryKind.Storage,SharedInventory.Garage,profile.catalog);}
            catch(Exception error){typedError=error;}finally{identity.Dispose();}
            check(legacyError==null&&typedError==null&&result!=null&&result.Success,"owner query defect does not turn a valid move into a core failure / "+name+
                ";legacy="+WpmException(legacyError)+";typed="+WpmOutcome(result,typedError));
            check(before==SourceSnapshot(profile)&&NormalizationReferenceDiff(refs,NormalizationReferences(profile)).Count==0&&catalog==CatalogSnapshot(profile),"unmaterializable-owner evaluation preserves malformed source and catalogue / "+name);
            check(json==RuntimeDataEncodingProbe.CurrentThreadJsonCallCount&&random.Equals(UnityEngine.Random.state)&&identity.Count==0&&identity.Closed&&!identity.Overflowed,
                "unmaterializable-owner evaluation emits no JSON/identity and preserves Unity RNG / "+name);
            DateTime entropyAfter=RuntimeEntropy.UtcNow;
            check(RuntimeEntropy.Capture==entropyMode&&(!entropyMode||entropyClock.Ticks==entropyAfter.Ticks&&entropyClock.Kind==entropyAfter.Kind),
                "unmaterializable-owner evaluation preserves RuntimeEntropy mode and captured clock / "+name);
            if(result==null||!result.Success||legacyError!=null)return;
            var owner=result.Owners?.SingleOrDefault(value=>value!=null&&value.Kind.ToString()=="Car"&&value.Id==carId);
            var referenceCar=legacy.cars.Find(value=>value.instanceId==carId);
            var changedIds=result.ChangedParts?.Where(value=>value!=null).Select(value=>value.instanceId).ToArray()??Array.Empty<string>();
            check(changedIds.Length==1&&changedIds[0]==requested.instanceId&&
                TypedCanonical(result.ChangedParts[0],new Dictionary<string,string>())==TypedCanonical(legacy.parts.Find(value=>value.instanceId==requested.instanceId),new Dictionary<string,string>()),
                "successful move reports its exact requested physical record write / "+name+";changed="+string.Join(",",changedIds));
            check(result.Owners?.Length==1&&owner!=null&&!owner.Materialized&&owner.Parts==null&&owner.Definitions==null&&owner.Inspection==null&&
                !string.IsNullOrEmpty(owner.BuildErrorType)&&!string.IsNullOrEmpty(owner.BuildErrorMessage),
                "optional owner failure is explicit and separate from successful transition status / "+name+
                ";materialized="+(owner?.Materialized.ToString()??"<missing>")+";error="+WpmSafe(owner?.BuildErrorType+" | "+owner?.BuildErrorMessage));
            check(owner!=null&&referenceCar!=null&&TypedCanonical(owner.Bindings,new Dictionary<string,string>())==TypedCanonical(referenceCar.installed.slots.ToArray(),new Dictionary<string,string>()),
                "base owner bindings remain available and match legacy despite optional query error / "+name);
            Exception expectedQueryError=null;
            try
            {
                var definitions=LegacyWholePartMoveReference.CarDefinitions(legacy,referenceCar);
                ConcreteInstalledBuildInput.Capture(legacy,referenceCar).Inspection(legacy.catalog);
            }
            catch(Exception error){expectedQueryError=error;}
            check(expectedQueryError!=null&&owner.BuildErrorType==expectedQueryError.GetType().FullName&&owner.BuildErrorMessage==expectedQueryError.Message,
                "optional owner error sidecar matches explicit typed H inspection precondition / "+name+" / "+WpmException(expectedQueryError));
            log.AppendLine("OPTIONAL_OWNER|"+name+"|move=success;materialized="+owner.Materialized+";buildError="+
                WpmSafe(owner.BuildErrorType+" | "+owner.BuildErrorMessage)+";legacyMove="+WpmException(legacyError));
        }

        static void WpmDuplicateOwnerResolution(Action<bool,string> check,StringBuilder log)
        {
            const string name="duplicate-car-id-first-owner-lookup-before-remove";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string firstId,WholePartMoveSeed+41);
            if(!GraphCreateSameModelPair(profile,out var first,out var later,out string reason))
            {log.AppendLine("OPEN|"+name+"|"+WpmSafe(reason));check(false,"actual duplicate-owner pair available / "+name+" / "+reason);return;}
            var firstPiston=WpmInstalledPart(profile,first,PartCategory.Pistons);
            var laterPiston=WpmInstalledPart(profile,later,PartCategory.Pistons);
            if(firstPiston==null||laterPiston==null){log.AppendLine("OPEN|"+name+"|actual fitted Pistons row absent");check(false,"both actual owner shapes have Pistons / "+name);return;}
            LegacyWholePartMoveReference.Move(profile,firstPiston.instanceId,InventoryKind.Storage,SharedInventory.Garage);
            string laterOriginalId=later.instanceId;
            later.instanceId=first.instanceId;
            if(later.installed!=null)later.installed.instanceId=first.instanceId;
            check(laterOriginalId!=firstId&&first.instanceId==later.instanceId,"only actual later car identity collides with first owner key / "+name);
            // Do not normalize or globally validate this deliberately duplicated-owner graph.
            WpmErrorVector(check,log,name,profile,laterPiston.instanceId,InventoryKind.Storage,SharedInventory.Garage,null,
                typeof(InvalidOperationException).FullName,"Slot is empty.");
            log.AppendLine("EDGE|"+name+"|first="+WpmSafe(first.instanceId)+";laterOriginal="+WpmSafe(laterOriginalId)+
                ";requested="+WpmSafe(laterPiston.instanceId)+";expectedFirstError=InvalidOperationException | Slot is empty.");
        }

        static void WpmTargetReservationFailure(Action<bool,string> check,StringBuilder log)
        {
            const string name="installed-source-to-reserved-target-partial-detach";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out _,WholePartMoveSeed+43);
            if(!GraphCreateSameModelPair(profile,out var donor,out var target,out string reason))
            {log.AppendLine("OPEN|"+name+"|"+WpmSafe(reason));check(false,"real target-lock pair available / "+name+" / "+reason);return;}
            var requested=WpmInstalledPart(profile,donor,PartCategory.Pistons);
            var targetOriginal=WpmInstalledPart(profile,target,PartCategory.Pistons);
            if(requested==null||targetOriginal==null){log.AppendLine("OPEN|"+name+"|actual fitted Pistons counterpart unavailable");check(false,"real source/target parts exist / "+name);return;}
            GarageBusiness.Ensure(profile);
            long now=Math.Max(CartPreviewCharacterizationFixtures.FixedNowTicks,profile.business.accruedThroughUtcTicks);
            var offer=GarageBusiness.DefaultPolicy().offers.FirstOrDefault(value=>value!=null&&value.id=="photo-session");
            if(offer==null){log.AppendLine("OPEN|"+name+"|actual photo-session offer unavailable");check(false,"real lock offer available / "+name);return;}
            GarageBusiness.Assign(profile,GarageBusiness.DefaultPolicy(),target.instanceId,offer.id,now);
            check(!string.IsNullOrEmpty(target.garageJobId),"target is locked by actual GarageBusiness assignment / "+name);
            string donorId=donor.instanceId,targetId=target.instanceId,requestedId=requested.instanceId,targetOriginalId=targetOriginal.instanceId;
            string donorTune=TypedCanonical(donor.tune,new Dictionary<string,string>());
            string targetBindings=TypedCanonical(target.installed.slots,new Dictionary<string,string>());
            const string expectedMessage="Car and installed components reserved by garage job; claim or cancel it first.";
            WpmErrorVector(check,log,name,profile,requestedId,InventoryKind.Car,targetId,null,
                typeof(InvalidOperationException).FullName,expectedMessage);
            var legacy=CartPreviewLiteralTestClone.Copy(profile);Exception error=null;
            try{LegacyWholePartMoveReference.Move(legacy,requestedId,InventoryKind.Car,targetId);}catch(Exception caught){error=caught;}
            var legacyRequested=legacy.parts.Find(value=>value.instanceId==requestedId);
            var legacyDonor=legacy.cars.Find(value=>value.instanceId==donorId);
            var legacyTarget=legacy.cars.Find(value=>value.instanceId==targetId);
            check(error!=null&&error.GetType().FullName==typeof(InvalidOperationException).FullName&&error.Message==expectedMessage&&
                legacyRequested!=null&&legacyRequested.location==PartLocation.Inventory&&string.IsNullOrEmpty(legacyRequested.ownerId)&&
                string.IsNullOrEmpty(legacyRequested.slot)&&legacyDonor!=null&&!legacyDonor.installed.slots.Any(binding=>binding.partInstanceId==requestedId)&&
                legacyTarget!=null&&legacyTarget.installed.slots.Any(binding=>binding.partInstanceId==targetOriginalId)&&
                TypedCanonical(legacyTarget.installed.slots,new Dictionary<string,string>())==targetBindings&&
                TypedCanonical(legacyDonor.tune,new Dictionary<string,string>())==donorTune,
                "frozen late target reservation error preserves earlier source detach and exact untouched target graph / "+name+
                ";error="+WpmException(error));
            log.AppendLine("PARTIAL_REJECT|"+name+"|typed=no-published-graph;legacy="+WpmSafe(WpmException(error))+
                ";requested="+WpmSafe(requestedId)+";donorAfter="+legacyRequested?.location+"/"+legacyRequested?.ownerId+"/"+legacyRequested?.slot+
                ";targetOriginal="+WpmSafe(targetOriginalId));
        }

        static void WpmPrecedence(Action<bool,string> check,StringBuilder log)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var source,out _,WholePartMoveSeed+20);
            var car=source.SelectedCar;var part=WpmInstalledPart(source,car,PartCategory.Pistons);
            if(part==null){check(false,"precedence fixture has actual installed Pistons part");return;}
            WpmErrorVector(check,log,"run-guard-before-invalid-destination-and-missing-members",source,part.instanceId,InventoryKind.Seller,"bad-destination",
                profile=>{profile.runCheckpoints.Add(new RunCheckpoint{settled=false});profile.parts.Find(value=>value.instanceId==part.instanceId).condition.elements=null;},
                typeof(InvalidOperationException).FullName,"Close the active run before moving inventory.");
            WpmErrorVector(check,log,"run-guard-before-storage-and-missing-asset",source,"absent-physical-id",InventoryKind.Storage,"missing-container",
                profile=>profile.runCheckpoints.Add(new RunCheckpoint{settled=false}),
                typeof(InvalidOperationException).FullName,"Close the active run before moving inventory.");
            WpmErrorVector(check,log,"unknown-storage-before-missing-asset",source,"absent-physical-id",InventoryKind.Storage,"missing-container",
                null,typeof(InvalidOperationException).FullName,"Unknown storage container.");
            WpmErrorVector(check,log,"missing-asset-before-invalid-destination",source,"absent-physical-id",InventoryKind.Seller,"seller",
                null,typeof(InvalidOperationException).FullName,"Owned item no longer exists.");
            WpmErrorVector(check,log,"invalid-destination-before-source-detach",source,part.instanceId,InventoryKind.Seller,"seller",
                null,typeof(InvalidOperationException).FullName,"Use a quote/cart to buy or sell.");
        }

        static void WpmErrorVector(Action<bool,string> check,StringBuilder log,string name,PlayerProfile template,string assetId,
            InventoryKind target,string targetId,Action<PlayerProfile> mutate,string errorType,string errorMessage)
        {
            var profile=CartPreviewLiteralTestClone.Copy(template);mutate?.Invoke(profile);
            string before=SourceSnapshot(profile),catalog=CatalogSnapshot(profile);var refs=NormalizationReferences(profile);
            long json=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;var random=UnityEngine.Random.state;
            bool entropyMode=RuntimeEntropy.Capture;DateTime entropyClock=RuntimeEntropy.UtcNow;
            var identity=RuntimeEntropy.BeginIdentityObservation(32);WholePartMoveResult result=null;Exception thrown=null;
            try{result=WholePartMoveProjection.Evaluate(profile,assetId,target,targetId,profile.catalog);}catch(Exception error){thrown=error;}finally{identity.Dispose();}
            check(thrown==null&&result!=null&&!result.Success&&result.ErrorType==errorType&&result.ErrorMessage==errorMessage,
                "exact first rejection / "+name+" / "+WpmOutcome(result,thrown));
            check(result==null||result.MovedPart==null&&result.DisplacedPart==null&&result.ChangedParts==null&&(result.Owners==null||result.Owners.Length==0),"rejection has no partial output / "+name);
            check(before==SourceSnapshot(profile)&&NormalizationReferenceDiff(refs,NormalizationReferences(profile)).Count==0&&catalog==CatalogSnapshot(profile),"rejection preserves exact input values/references/catalogue / "+name);
            check(json==RuntimeDataEncodingProbe.CurrentThreadJsonCallCount&&random.Equals(UnityEngine.Random.state)&&identity.Count==0&&identity.Closed&&!identity.Overflowed,
                "rejection has no JSON/RNG/identity effects / "+name);
            DateTime entropyAfter=RuntimeEntropy.UtcNow;
            check(RuntimeEntropy.Capture==entropyMode&&(!entropyMode||entropyClock.Ticks==entropyAfter.Ticks&&entropyClock.Kind==entropyAfter.Kind),
                "rejection preserves RuntimeEntropy mode and captured clock / "+name);
            var legacy=CartPreviewLiteralTestClone.Copy(profile);string legacyBefore=SourceSnapshot(legacy);Exception legacyError=null;
            try{LegacyWholePartMoveReference.Move(legacy,assetId,target,targetId);}catch(Exception error){legacyError=error;}
            WpmLiveMatchesFrozen(check,name,profile,assetId,target,targetId,legacy,legacyError);
            check(legacyError!=null&&legacyError.GetType().FullName==errorType&&legacyError.Message==errorMessage,
                "frozen legacy transition confirms exact first-error precedence / "+name+" / "+WpmException(legacyError));
            bool legacyPartial=legacyBefore!=SourceSnapshot(legacy);
            bool expectedLegacyPartial=name=="wrong-member-type-common-error"||name=="installed-source-to-reserved-target-partial-detach";
            check(legacyPartial==expectedLegacyPartial,"frozen first-error partial effects match characterized boundary / "+name+";changed="+legacyPartial);
            log.AppendLine("REJECT|"+name+"|expected="+errorType+" | "+WpmSafe(errorMessage)+";actual="+WpmSafe(WpmOutcome(result,thrown))+
                ";legacyPartial="+legacyPartial);
        }

        static void WpmMemberBoundaries(Action<bool,string> check,StringBuilder log)
        {
            WpmMissingMembers(check,log,"required-members-null",true);
            WpmMissingMembers(check,log,"required-members-empty",false);
            WpmNonemptyMembers(check,log,"nonempty-short-members",false);
            WpmNonemptyMembers(check,log,"nonempty-long-members",true);
            WpmWrongMemberType(check,log);
            WpmZeroCountEmpty(check,log);
        }

        static void WpmMissingMembers(Action<bool,string> check,StringBuilder log,string name,bool useNull)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,WholePartMoveSeed+(useNull?30u:31u));
            var engine=WpmDetachOwnedEngineToStorage(profile,carId);
            var part=WpmEnginePart(profile,engine,PartCategory.Pistons);
            if(part==null){log.AppendLine("OPEN|"+name+"|actual fitted piston part absent");check(false,"actual required-member part exists / "+name);return;}
            LegacyWholePartMoveReference.Move(profile,part.instanceId,InventoryKind.Storage,SharedInventory.Garage);
            check(MechanicsOwnership.Validate(profile,profile.catalog)==null,"fixture validates before missing-member corruption / "+name);
            profile.parts.Find(value=>value.instanceId==part.instanceId).condition.elements=useNull?null:new EngineElement[0];
            RunMissingMemberProbe(check,log,name,profile,part.instanceId,engine.instanceId);
        }

        static void RunMissingMemberProbe(Action<bool,string> check,StringBuilder log,string name,PlayerProfile profile,string assetId,string engineId)
        {
            string before=SourceSnapshot(profile),catalog=CatalogSnapshot(profile);var refs=NormalizationReferences(profile);
            long json=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;var random=UnityEngine.Random.state;
            bool capture=RuntimeEntropy.Capture;DateTime clock=RuntimeEntropy.UtcNow;var identity=RuntimeEntropy.BeginIdentityObservation(32);
            WholePartMoveResult result=null;Exception thrown=null;
            try{result=WholePartMoveProjection.Evaluate(profile,assetId,InventoryKind.Engine,engineId,profile.catalog);}catch(Exception error){thrown=error;}finally{identity.Dispose();}
            check(thrown==null&&result!=null&&!result.Success&&result.ErrorType==typeof(InvalidOperationException).FullName&&
                result.ErrorMessage=="Typed part move projection cannot initialize missing engine members.","required empty/null members refuse with exact no-heal error / "+name+" / "+WpmOutcome(result,thrown));
            check(before==SourceSnapshot(profile)&&NormalizationReferenceDiff(refs,NormalizationReferences(profile)).Count==0&&catalog==CatalogSnapshot(profile),"missing-member rejection preserves exact graph and catalogue / "+name);
            check(json==RuntimeDataEncodingProbe.CurrentThreadJsonCallCount&&random.Equals(UnityEngine.Random.state)&&identity.Count==0&&identity.Closed&&!identity.Overflowed,
                "missing-member refusal does not serialize, draw IDs, or advance Unity RNG / "+name);
            bool mode=RuntimeEntropy.Capture==capture;DateTime after=RuntimeEntropy.UtcNow;
            check(mode&&(!capture||clock.Ticks==after.Ticks&&clock.Kind==after.Kind),"missing-member refusal preserves entropy mode and captured clock / "+name);
            var legacy=CartPreviewLiteralTestClone.Copy(profile);Exception legacyError=null;
            try{LegacyWholePartMoveReference.Move(legacy,assetId,InventoryKind.Engine,engineId);}catch(Exception error){legacyError=error;}
            WpmLiveMatchesFrozen(check,name,profile,assetId,InventoryKind.Engine,engineId,legacy,legacyError);
            var healed=legacy.parts.Find(value=>value.instanceId==assetId)?.condition?.elements;
            check(legacyError==null&&healed!=null&&healed.Length==EngineElements.Count(legacy.catalog.Resolve(legacy.engines.Find(value=>value.instanceId==engineId).vehicleDefinitionId,
                LegacyWholePartMoveReference.EngineDefinitions(legacy,legacy.engines.Find(value=>value.instanceId==engineId)),legacy.engines.Find(value=>value.instanceId==engineId).tune).spec,
                legacy.catalog.Find(legacy.parts.Find(value=>value.instanceId==assetId).definitionId)),"legacy reference synthesizes required members for this old missing-state shape / "+name);
            check(before==SourceSnapshot(profile),"legacy healing occurs only on its disposable diagnostic copy / "+name);
            log.AppendLine("NO_HEAL|"+name+"|typed="+WpmSafe(WpmOutcome(result,thrown))+";legacy="+WpmSafe(WpmException(legacyError))+";legacyMemberCount="+(healed==null?"<null>":healed.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        static void WpmNonemptyMembers(Action<bool,string> check,StringBuilder log,string name,bool makeLong)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,WholePartMoveSeed+(makeLong?32u:33u));
            var engine=WpmDetachOwnedEngineToStorage(profile,carId);
            var part=WpmEnginePart(profile,engine,PartCategory.Pistons);if(part==null){check(false,"actual piston part exists / "+name);return;}
            LegacyWholePartMoveReference.Move(profile,part.instanceId,InventoryKind.Storage,SharedInventory.Garage);
            var mutable=profile.parts.Find(value=>value.instanceId==part.instanceId);
            if(mutable.condition.elements==null||mutable.condition.elements.Length==0){check(false,"fixture has actual piston members / "+name);return;}
            var original=mutable.condition.elements;
            if(makeLong){var longer=new EngineElement[original.Length+1];Array.Copy(original,longer,original.Length);longer[original.Length]=original[0];mutable.condition.elements=longer;}
            else mutable.condition.elements=new[]{original[0]};
            string memberBefore=TypedCanonical(mutable.condition.elements,new Dictionary<string,string>());
            RunWpmReferenceComparison(check,log,name,profile,part.instanceId,InventoryKind.Engine,engine.instanceId,true);
            // The checker above proves output parity; this assertion documents that the actual source is not healed.
            check(memberBefore==TypedCanonical(mutable.condition.elements,new Dictionary<string,string>()),"nonempty member array remains exact and is not repaired / "+name);
        }

        static void WpmWrongMemberType(Action<bool,string> check,StringBuilder log)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,WholePartMoveSeed+34);
            var engine=WpmDetachOwnedEngineToStorage(profile,carId);
            var piston=WpmEnginePart(profile,engine,PartCategory.Pistons);var other=WpmEnginePart(profile,engine,PartCategory.Valves);
            if(piston==null||other==null||other.condition.elements==null||other.condition.elements.Length==0){check(false,"actual differing member category vectors exist / wrong-member-type");return;}
            LegacyWholePartMoveReference.Move(profile,piston.instanceId,InventoryKind.Storage,SharedInventory.Garage);
            var wrong=profile.parts.Find(value=>value.instanceId==piston.instanceId);wrong.condition.elements=other.condition.elements;
            WpmErrorVector(check,log,"wrong-member-type-common-error",profile,piston.instanceId,InventoryKind.Engine,engine.instanceId,null,
                typeof(InvalidOperationException).FullName,"Installed member type does not match Pistons. Reset incompatible save.");
        }

        static void WpmZeroCountEmpty(Action<bool,string> check,StringBuilder log)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,WholePartMoveSeed+35);
            var car=profile.cars.Find(value=>value.instanceId==carId);var spec=profile.catalog.Spec(car.vehicleId);
            var candidate=car.installed.slots.Select(binding=>profile.parts.Find(part=>part.instanceId==binding.partInstanceId))
                .Where(part=>part!=null).Select(part=>new{Part=part,Definition=profile.catalog.Find(part.definitionId)})
                .FirstOrDefault(value=>value.Definition!=null&&MechanicalCatalog.EngineSlot(value.Definition.category)&&EngineElements.Count(spec,value.Definition)==0);
            if(candidate==null){log.AppendLine("OPEN|zero-count-empty-members|no actual fitted engine-slot offer has zero authored member count");check(false,"actual zero-count empty-member shape available / zero-count-empty-members");return;}
            var engine=WpmDetachOwnedEngineToStorage(profile,carId);
            var part=WpmEnginePart(profile,engine,candidate.Definition.category);
            if(part==null){check(false,"actual zero-count category remains on detached engine");return;}
            LegacyWholePartMoveReference.Move(profile,part.instanceId,InventoryKind.Storage,SharedInventory.Garage);
            profile.parts.Find(value=>value.instanceId==part.instanceId).condition.elements=new EngineElement[0];
            RunWpmReferenceComparison(check,log,"zero-count-empty-members",profile,part.instanceId,InventoryKind.Engine,engine.instanceId,true);
        }

        static void WpmSourceAudit(Action<bool,string> check,StringBuilder log)
        {
            string path=Path.Combine(UnityEngine.Application.dataPath,"DragLab","Scripts","Domain","WholePartMoveProjection.cs");
            if(!File.Exists(path)){check(false,"typed projection source is available for forbidden-side-effect audit");return;}
            string source=File.ReadAllText(path,Encoding.UTF8);
            string[] forbidden={"JsonUtility", "SharedInventory.Move(", "MechanicsOwnership.Ensure(", "EngineElements.Ensure(", "RuntimeEntropy.NewId(", "Guid.NewGuid("};
            foreach(string token in forbidden)check(source.IndexOf(token,StringComparison.Ordinal)<0,"projection source excludes mutating/JSON/identity route / "+token);
            log.AppendLine("SOURCE_AUDIT|forbiddenTokensChecked="+forbidden.Length+";path=WholePartMoveProjection.cs");
        }

        static string WpmOutcome(WholePartMoveResult result,Exception thrown)
            =>thrown!=null?thrown.GetType().FullName+" | "+thrown.Message:result==null?"<null>":result.Success?"success":result.ErrorType+" | "+result.ErrorMessage;
        static string WpmException(Exception error)=>error==null?"success":error.GetType().FullName+" | "+error.Message;
        static void WpmLiveMatchesFrozen(Action<bool,string> check,string name,PlayerProfile source,string assetId,
            InventoryKind target,string targetId,PlayerProfile frozenAfter,Exception frozenError)
        {
            var live=CartPreviewLiteralTestClone.Copy(source);Exception liveError=null;
            try{SharedInventory.Move(live,assetId,target,targetId);}catch(Exception error){liveError=error;}
            bool sameError=(frozenError==null&&liveError==null)||(frozenError!=null&&liveError!=null&&
                frozenError.GetType().FullName==liveError.GetType().FullName&&frozenError.Message==liveError.Message);
            check(sameError,"current live wrapper returns frozen exact error/success / "+name+";frozen="+WpmException(frozenError)+";live="+WpmException(liveError));
            check(SourceSnapshot(frozenAfter)==SourceSnapshot(live),"current live wrapper output graph matches frozen whole-part reference / "+name);
        }
        static string WpmSafe(string value)=>new string((value??"").Select(character=>char.IsControl(character)?' ':character).Take(400).ToArray());
    }
}
#endif
