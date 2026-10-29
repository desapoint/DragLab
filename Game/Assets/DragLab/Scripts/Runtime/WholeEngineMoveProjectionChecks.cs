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
    /// <summary>Finite frozen-reference checks for the dormant scoped whole-engine move projection.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint WholeEngineMoveSeed=20261031u;

        public static void RunWholeEngineMove(Action<bool,string> check,string outputFolder)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(outputFolder))throw new ArgumentException("An isolated report folder is required.",nameof(outputFolder));
            Directory.CreateDirectory(outputFolder);
            var log=new StringBuilder();int assertions=0,failures=0;
            Action<bool,string> record=(ok,label)=>{assertions++;if(!ok)failures++;check(ok,"whole-engine-move/"+label);log.AppendLine("ASSERT|"+(ok?"PASS":"FAIL")+"|"+WemSafe(label));};
            log.AppendLine("S06D2B1J_WHOLE_ENGINE_MOVE_PROJECTION");
            log.AppendLine("seed="+WholeEngineMoveSeed+";fixture=authored;oracle=LegacyWholeEngineMoveReference;candidate=dormant typed projection");
            try
            {
                WemValidMoves(record,log);
                WemNoOps(record,log);
                WemOrderedGuards(record,log);
                WemLateFailures(record,log);
                WemTransferFactsAndMalformed(record,log);
                WemSourceAudit(record,log);
            }
            catch(Exception error)
            {
                record(false,"runner exception="+error.GetType().FullName+" | "+error.Message);
                log.AppendLine("RUNNER_EXCEPTION|"+WemSafe(error.GetType().FullName)+"|"+WemSafe(error.Message));
            }
            finally
            {
                log.AppendLine("SUMMARY|assertions="+assertions+";failures="+failures);
                File.WriteAllText(Path.Combine(outputFolder,"S06D2B1J_WHOLE_ENGINE_MOVE_PROJECTION.txt"),log.ToString(),new UTF8Encoding(false));
            }
        }

        static void WemValidMoves(Action<bool,string> check,StringBuilder log)
        {
            RunWemPrepared(check,log,"storage-to-storage",WholeEngineMoveSeed,p=>
            {
                var e=WemStoredEngine(p,p.SelectedCar.instanceId);return Tuple.Create(e.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            });
            RunWemPrepared(check,log,"stored-engine-to-bare-compatible-car",WholeEngineMoveSeed+1,p=>
            {
                var car=p.SelectedCar;var e=WemStoredEngine(p,car.instanceId);return Tuple.Create(e.instanceId,InventoryKind.Car,car.instanceId);
            });
            RunWemPrepared(check,log,"attached-engine-to-storage",WholeEngineMoveSeed+2,p=>
            {
                var c=p.SelectedCar;var e=WemAttachedEngine(p,c);return Tuple.Create(e.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            });
            RunWemPrepared(check,log,"attached-engine-a-to-compatible-bare-b",WholeEngineMoveSeed+3,p=>
            {
                if(!GraphCreateSameModelPair(p,out var source,out var target,out string reason))throw new InvalidOperationException("OPEN: "+reason);
                var e=WemAttachedEngine(p,source);WemMakeBare(p,target);
                return Tuple.Create(e.instanceId,InventoryKind.Car,target.instanceId);
            });
        }

        static void WemNoOps(Action<bool,string> check,StringBuilder log)
        {
            RunWemPrepared(check,log,"same-parent-no-op",WholeEngineMoveSeed+4,p=>
            {
                var e=WemAttachedEngine(p,p.SelectedCar);return Tuple.Create(e.instanceId,InventoryKind.Car,p.SelectedCar.instanceId);
            });
            RunWemPrepared(check,log,"same-parent-no-op-target-garage-job",WholeEngineMoveSeed+5,p=>
            {
                var car=p.SelectedCar;var e=WemAttachedEngine(p,car);car.garageJobId="j-noop-job";
                return Tuple.Create(e.instanceId,InventoryKind.Car,car.instanceId);
            });
            RunWemPrepared(check,log,"same-parent-no-op-target-active-rebuild",WholeEngineMoveSeed+25,p=>
            {
                var car=p.SelectedCar;var e=WemAttachedEngine(p,car);car.engineRebuild=new EngineRebuildJob{active=true};
                return Tuple.Create(e.instanceId,InventoryKind.Car,car.instanceId);
            });
            RunWemPrepared(check,log,"same-parent-no-op-missing-target-and-null-engine-payload",WholeEngineMoveSeed+26,p=>
            {
                var car=p.SelectedCar;var e=WemAttachedEngine(p,car);string parent=e.parentVehicleId;
                p.cars.Remove(car);e.slots=null;e.tune=null;e.fluids=null;
                return Tuple.Create(e.instanceId,InventoryKind.Car,parent);
            });
        }

        static void WemOrderedGuards(Action<bool,string> check,StringBuilder log)
        {
            RunWemExpectedFailure(check,log,"unsettled-run-before-unknown-storage",WholeEngineMoveSeed+6,
                (p,asset)=>{p.runCheckpoints.Add(new RunCheckpoint{runId="j-run",settled=false});return Tuple.Create(asset,InventoryKind.Storage,"missing-storage");});
            RunWemExpectedFailure(check,log,"unsettled-run-before-missing-asset",WholeEngineMoveSeed+24,
                (p,asset)=>{p.runCheckpoints.Add(new RunCheckpoint{runId="j-run",settled=false});return Tuple.Create("missing-engine",InventoryKind.Car,p.SelectedCar.instanceId);});
            RunWemExpectedFailure(check,log,"unknown-storage-before-missing-asset",WholeEngineMoveSeed+7,
                (p,asset)=>Tuple.Create("missing-engine",InventoryKind.Storage,"missing-storage"));
            RunWemExpectedFailure(check,log,"missing-asset",WholeEngineMoveSeed+8,
                (p,asset)=>Tuple.Create("missing-engine",InventoryKind.Car,p.SelectedCar.instanceId));
            RunWemExpectedFailure(check,log,"invalid-engine-destination-before-detach",WholeEngineMoveSeed+9,
                (p,asset)=>{var e=WemAttachedEngine(p,p.SelectedCar);return Tuple.Create(e.instanceId,InventoryKind.Seller,"seller");});
            RunWemMalformedParentCar(check,log);
            RunWemMissingReferencedEngine(check,log);
        }

        static void WemLateFailures(Action<bool,string> check,StringBuilder log)
        {
            RunWemPrepared(check,log,"occupied-target-after-source-detach",WholeEngineMoveSeed+10,p=>
            {
                if(!GraphCreateSameModelPair(p,out var source,out var target,out string reason))throw new InvalidOperationException("OPEN: "+reason);
                var e=WemAttachedEngine(p,source);return Tuple.Create(e.instanceId,InventoryKind.Car,target.instanceId);
            },false);
            RunWemPrepared(check,log,"incompatible-target-after-source-detach",WholeEngineMoveSeed+11,p=>
            {
                var source=p.SelectedCar;var e=WemAttachedEngine(p,source);var target=WemCreateActualOtherModel(p,source);
                return Tuple.Create(e.instanceId,InventoryKind.Car,target.instanceId);
            },false);
            RunWemPrepared(check,log,"target-garage-job-after-source-detach",WholeEngineMoveSeed+12,p=>
            {
                if(!GraphCreateSameModelPair(p,out var source,out var target,out string reason))throw new InvalidOperationException("OPEN: "+reason);
                var e=WemAttachedEngine(p,source);target.garageJobId="j-active-job";return Tuple.Create(e.instanceId,InventoryKind.Car,target.instanceId);
            },false);
            RunWemPrepared(check,log,"target-active-rebuild-after-source-detach",WholeEngineMoveSeed+13,p=>
            {
                if(!GraphCreateSameModelPair(p,out var source,out var target,out string reason))throw new InvalidOperationException("OPEN: "+reason);
                var e=WemAttachedEngine(p,source);target.engineRebuild=new EngineRebuildJob{active=true};return Tuple.Create(e.instanceId,InventoryKind.Car,target.instanceId);
            },false);
            RunWemNullSlotsAttach(check,log);
        }

        static void WemTransferFactsAndMalformed(Action<bool,string> check,StringBuilder log)
        {
            RunWemPrepared(check,log,"transfer-nondefault-fluid-tune-and-unrelated-bindings",WholeEngineMoveSeed+14,p=>
            {
                if(!GraphCreateSameModelPair(p,out var source,out var target,out string reason))throw new InvalidOperationException("OPEN: "+reason);
                var e=WemAttachedEngine(p,source);
                WemMakeBare(p,target);
                source.fluids.oilLitres=3.125;source.fluids.coolantLitres=4.75;source.fluids.oilTemperatureC=117.25;source.fluids.oilConsumedLitres=.375;
                source.tune.launchRpm=4175;source.tune.shiftRpm=7025;source.tune.camTimingDegrees=2.5f;source.tune.engineLevel=4;
                target.tune.launchRpm=2890;target.tune.shiftRpm=6100;target.tune.camTimingDegrees=-1.25f;target.tune.engineLevel=2;
                var unrelated=source.installed.slots.FirstOrDefault(b=>b!=null&&!MechanicalCatalog.EngineSlot(AssemblyOperations.Parse(b.slot)));
                if(unrelated==null)throw new InvalidOperationException("OPEN: source has no unrelated chassis binding");
                return Tuple.Create(e.instanceId,InventoryKind.Car,target.instanceId);
            });
            RunWemPrepared(check,log,"null-engine-fluid-and-tune-into-bare-target",WholeEngineMoveSeed+15,p=>
            {
                var c=p.SelectedCar;var e=WemStoredEngine(p,c.instanceId);e.fluids=null;e.tune=null;return Tuple.Create(e.instanceId,InventoryKind.Car,c.instanceId);
            });
            RunWemPrepared(check,log,"car-query-missing-fuel-after-successful-attach",WholeEngineMoveSeed+27,p=>
            {
                var c=p.SelectedCar;var e=WemStoredEngine(p,c.instanceId);c.fuel=null;
                return Tuple.Create(e.instanceId,InventoryKind.Car,c.instanceId);
            });
            RunWemMalformedParentBindingDisagreement(check,log);
            RunWemMalformedInvalidSlot(check,log);
            RunWemMalformedNullCarTune(check,log);
            RunWemMalformedLateRankFailure(check,log);
            RunWemMalformedOverwrittenEngineSlots(check,log);
        }

        static EngineAssemblyInstance WemStoredEngine(PlayerProfile p,string carId)
        {
            var attached=WemAttachedEngine(p,p.cars.Find(c=>c.instanceId==carId));
            LegacyWholeEngineMoveReference.Detach(p,carId);
            attached.storageId=SharedInventory.Garage;
            if(!string.IsNullOrEmpty(attached.parentVehicleId))throw new InvalidOperationException("Frozen detach setup did not store engine.");
            return attached;
        }

        static EngineAssemblyInstance WemAttachedEngine(PlayerProfile p,OwnedCar car)
            =>p.engines.Find(e=>e!=null&&e.instanceId==car?.engineAssemblyId&&e.parentVehicleId==car.instanceId)
                ??throw new InvalidOperationException("The fixture has no actual attached engine.");

        static void WemMakeBare(PlayerProfile p,OwnedCar car)
        {
            if(!string.IsNullOrEmpty(car.engineAssemblyId))LegacyWholeEngineMoveReference.Detach(p,car.instanceId);
        }

        static OwnedCar WemCreateActualOtherModel(PlayerProfile p,OwnedCar source)
        {
            var candidate=p.cars.FirstOrDefault(c=>c!=null&&c.instanceId!=source.instanceId&&c.vehicleId!=source.vehicleId&&string.IsNullOrEmpty(c.engineAssemblyId));
            if(candidate!=null)return candidate;
            candidate=p.cars.FirstOrDefault(c=>c!=null&&c.vehicleId!=source.vehicleId);
            if(candidate==null)
            {
                candidate=CartPreviewCharacterizationFixtures.AddSecondOwnedCar(p);
                if(candidate==null||candidate.vehicleId==source.vehicleId)
                    throw new InvalidOperationException("OPEN: actual new-car purchase did not produce a different-model owned car");
                VehicleCollection.Select(p,source.instanceId);
            }
            WemMakeBare(p,candidate);return candidate;
        }

        static void RunWemPrepared(Action<bool,string> check,StringBuilder log,string name,uint seed,
            Func<PlayerProfile,Tuple<string,InventoryKind,string>> prepare,bool expectSuccess=true)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,seed);
            Tuple<string,InventoryKind,string> request;
            try{request=prepare(profile);}
            catch(Exception error){WemOpen(check,log,name,error);return;}
            bool validFixture=expectSuccess&&!name.Contains("preexisting-stored-slots")&&
                !name.Contains("null-engine")&&!name.Contains("missing-target-and-null")&&
                !name.Contains("missing-fuel")&&
                !name.Contains("target-garage-job")&&!name.Contains("target-active-rebuild")&&
                !name.Contains("parent-car-references-different-engine");
            if(validFixture)
            {
                string invalid=MechanicsOwnership.Validate(profile,profile.catalog);
                check(invalid==null,"authored fixture validates before operation / "+name+" / "+(invalid??"valid"));
                if(invalid!=null)return;
            }
            RunWemComparison(check,log,name,profile,request.Item1,request.Item2,request.Item3,expectSuccess);
        }

        static void RunWemExpectedFailure(Action<bool,string> check,StringBuilder log,string name,uint seed,
            Func<PlayerProfile,string,Tuple<string,InventoryKind,string>> prepare)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,seed);
            string asset=profile.SelectedCar?.engineAssemblyId;
            Tuple<string,InventoryKind,string> request;
            try{request=prepare(profile,asset);}
            catch(Exception error){WemOpen(check,log,name,error);return;}
            RunWemComparison(check,log,name,profile,request.Item1,request.Item2,request.Item3,false);
        }

        static void RunWemComparison(Action<bool,string> check,StringBuilder log,string name,PlayerProfile profile,
            string assetId,InventoryKind target,string targetId,bool expectSuccess)
        {
            string before=SourceSnapshot(profile),catalog=CatalogSnapshot(profile);var refs=NormalizationReferences(profile);
            long json=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;var unity=UnityEngine.Random.state;
            bool entropy=RuntimeEntropy.Capture;DateTime clock=RuntimeEntropy.UtcNow;var identity=RuntimeEntropy.BeginIdentityObservation(64);
            WholeEngineMoveResult actual=null;Exception thrown=null;
            try{actual=WholeEngineMoveProjection.Evaluate(profile,assetId,target,targetId,profile.catalog);}
            catch(Exception error){thrown=error;}finally{identity.Dispose();}
            check(identity.Closed&&!identity.Overflowed&&identity.Count==0,"no identities allocated / "+name);
            check(json==RuntimeDataEncodingProbe.CurrentThreadJsonCallCount,"no runtime JSON route / "+name);
            check(unity.Equals(UnityEngine.Random.state),"Unity RNG unchanged / "+name);
            check(entropy==RuntimeEntropy.Capture,"entropy capture mode unchanged / "+name);
            if(entropy)check(clock.Ticks==RuntimeEntropy.UtcNow.Ticks&&clock.Kind==RuntimeEntropy.UtcNow.Kind,"captured clock unchanged / "+name);
            check(before==SourceSnapshot(profile)&&NormalizationReferenceDiff(refs,NormalizationReferences(profile)).Count==0,
                "complete source values and reference topology unchanged / "+name);
            check(catalog==CatalogSnapshot(profile),"borrowed catalogue unchanged / "+name);

            var legacy=CartPreviewLiteralTestClone.Copy(profile);Exception legacyError=null;
            try{LegacyWholeEngineMoveReference.Move(legacy,assetId,target,targetId);}catch(Exception error){legacyError=error;}
            bool success=thrown==null&&actual!=null&&actual.Success;
            check(success==expectSuccess,"private outcome matches expected / "+name+" / "+WemOutcome(actual,thrown));
            check((legacyError==null)==expectSuccess,"frozen outcome matches expected / "+name+" / "+WemError(legacyError));
            WemLiveWrapperMatchesFrozen(check,name,profile,assetId,target,targetId,legacy,legacyError);
            if(!expectSuccess)
            {
                check(thrown==null&&actual!=null&&!actual.Success&&actual.ErrorType==legacyError?.GetType().FullName&&actual.ErrorMessage==legacyError?.Message,
                    "exact frozen first error / "+name+" / typed="+WemOutcome(actual,thrown)+";legacy="+WemError(legacyError));
                if(name.Contains("null-engine-slots-attach"))
                {
                    var targetCar=legacy.cars.Find(car=>car!=null&&car.instanceId==targetId);
                    var partialEngine=legacy.engines.Find(engine=>engine!=null&&engine.instanceId==assetId);
                    var inputTarget=profile.cars.Find(car=>car!=null&&car.instanceId==targetId);
                    var inputEngine=profile.engines.Find(engine=>engine!=null&&engine.instanceId==assetId);
                    check(legacyError is ArgumentNullException,"null engine binding AddRange keeps its exact argument-null error / "+name);
                    check(targetCar!=null&&inputTarget!=null&&targetCar.engineAssemblyId==assetId&&partialEngine?.parentVehicleId==targetId&&
                        TypedCanonical(targetCar.fluids,new Dictionary<string,string>())==TypedCanonical(inputTarget.fluids,new Dictionary<string,string>())&&
                        TypedCanonical(targetCar.installed.slots,new Dictionary<string,string>())==TypedCanonical(inputTarget.installed.slots,new Dictionary<string,string>())&&
                        TypedCanonical(targetCar.tune,new Dictionary<string,string>())==TypedCanonical(inputTarget.tune,new Dictionary<string,string>())&&
                        partialEngine?.slots==null&&TypedCanonical(partialEngine.fluids,new Dictionary<string,string>())==TypedCanonical(inputEngine.fluids,new Dictionary<string,string>())&&
                        TypedCanonical(partialEngine.tune,new Dictionary<string,string>())==TypedCanonical(inputEngine.tune,new Dictionary<string,string>()),
                        "legacy AddRange failure retains only preceding reciprocal-ID writes and preserves later fields / "+name);
                }
                check(actual==null||actual.MovedEngine==null&&actual.ChangedEngines==null&&actual.CarOwners==null,
                    "failure exports no partial engine or owner graph / "+name);
                check(before==SourceSnapshot(profile)&&NormalizationReferenceDiff(refs,NormalizationReferences(profile)).Count==0,
                    "private failure preserves all source values and references / "+name);
                log.AppendLine("VECTOR|"+name+"|success=false|error="+WemSafe(WemOutcome(actual,thrown)));
                return;
            }
            if(!success||legacyError!=null)return;
            WemCompareSuccess(check,log,name,profile,assetId,target,targetId,legacy,actual);
            WemOutputIsolation(check,name,profile,assetId,target,targetId,actual,before);
        }

        static void WemCompareSuccess(Action<bool,string> check,StringBuilder log,string name,PlayerProfile input,
            string assetId,InventoryKind target,string targetId,PlayerProfile legacy,WholeEngineMoveResult actual)
        {
            var expected=legacy.engines.Find(e=>e!=null&&e.instanceId==assetId);
            check(WemEngineRaw(expected)==WemEngineSnapshot(actual.MovedEngine),"requested moved engine raw facts/nulls match frozen result / "+name);
            string movedPartsDifference=WemEnginePartsDifference(actual.MovedEngine,expected,legacy);
            check(movedPartsDifference==null,"requested engine descendants/materialization match frozen owned-copy facts / "+name+
                (movedPartsDifference==null?"":";firstDiff="+movedPartsDifference));
            check(actual.MovedEngine!=null&&!input.engines.Any(e=>ReferenceEquals(e,actual.MovedEngine)),"moved engine snapshot owned / "+name);
            var changedExpected=WemExpectedChangedEngines(input,assetId,target,targetId,legacy);
            var changed=actual.ChangedEngines??Array.Empty<WholeEngineMoveSnapshot>();
            check(changed.Select(e=>e?.Id).SequenceEqual(changedExpected,StringComparer.Ordinal),"changed engine order/identity exact / "+name+";expected="+string.Join(",",changedExpected)+";actual="+string.Join(",",changed.Select(e=>e?.Id)));
            foreach(var snapshot in changed)
            {
                var frozen=legacy.engines.Find(e=>e!=null&&e.instanceId==snapshot?.Id);
                check(snapshot!=null&&WemEngineRaw(frozen)==WemEngineSnapshot(snapshot),"changed engine ordered fields/tune/fluid/nulls match frozen graph / "+name+" / "+snapshot?.Id);
                string partsDifference=WemEnginePartsDifference(snapshot,frozen,legacy);
                check(partsDifference==null,"changed engine descendant IDs/conditions match owned-copy representation / "+name+" / "+snapshot?.Id+
                    (partsDifference==null?"":";firstDiff="+partsDifference));
                check(snapshot!=null&&!input.engines.Any(e=>ReferenceEquals(e,snapshot)),"changed engine snapshot does not alias source / "+name+" / "+snapshot?.Id);
            }
            var expectedCars=WemExpectedCarOwnerOrder(input,assetId,target,targetId,legacy);
            var owners=actual.CarOwners??Array.Empty<WholePartMoveOwnerSnapshot>();
            check(owners.Select(o=>o?.Id).SequenceEqual(expectedCars,StringComparer.Ordinal),"affected cars source/target order exact / "+name+";expected="+string.Join(",",expectedCars)+";actual="+string.Join(",",owners.Select(o=>o?.Id)));
            foreach(var owner in owners)
            {
                string diff=WemCarOwnerFirstDifference(owner,legacy);
                check(diff==null,"affected car explicit fields/binding order/tune/fluid/materialization match frozen / "+name+" / "+owner?.Id+(diff==null?"":";firstDiff="+diff));
            }
            if(name.Contains("storage-to-storage"))check(changed.Length==1&&changed[0].StorageId==targetId,"storage assignment appears in changed engine snapshot / "+name);
            if(name.Contains("same-parent-no-op"))check(changed.Length==0&&owners.Length==0,"same-parent no-op has no changed records/owners / "+name);
            if(name.Contains("parent-car-references-different-engine"))
            {
                var source=input.cars.Find(car=>car!=null&&car.instanceId==input.engines.Find(engine=>engine!=null&&engine.instanceId==assetId)?.parentVehicleId);
                var selectedId=source?.engineAssemblyId;
                var other=input.cars.FirstOrDefault(car=>car!=null&&car.instanceId!=source?.instanceId&&car.engineAssemblyId==selectedId);
                var frozenSource=legacy.cars.FirstOrDefault(car=>car!=null&&car.instanceId==source?.instanceId);
                var frozenOther=legacy.cars.FirstOrDefault(car=>car!=null&&car.instanceId==other?.instanceId);
                var moved=changed.FirstOrDefault(engine=>engine?.Id==assetId);var detached=changed.FirstOrDefault(engine=>engine?.Id==selectedId);
                var frozenRequested=legacy.engines.Find(engine=>engine!=null&&engine.instanceId==assetId);
                var frozenSelected=legacy.engines.Find(engine=>engine!=null&&engine.instanceId==selectedId);
                check(changed.Length==2&&changed[0]?.Id==selectedId&&changed[1]?.Id==assetId,
                    "malformed Storage move reports selected car engine then requested engine / "+name);
                check(moved?.ParentVehicleId==source?.instanceId&&moved?.StorageId==targetId&&
                    detached!=null&&string.IsNullOrEmpty(detached.ParentVehicleId)&&
                    WemEngineRaw(frozenRequested)==WemEngineSnapshot(moved)&&WemEngineRaw(frozenSelected)==WemEngineSnapshot(detached),
                    "malformed Storage move preserves requested engine parent while detaching the car-selected engine / "+name);
                check(frozenSource!=null&&string.IsNullOrEmpty(frozenSource.engineAssemblyId)&&frozenOther!=null&&frozenOther.engineAssemblyId==selectedId&&
                    owners.Length==1&&owners[0]?.Id==source?.instanceId,
                    "malformed Storage move clears source, leaves other car's stale engine reference, and touches only source car / "+name);
            }
            log.AppendLine("VECTOR|"+name+"|success=true;changedEngines="+string.Join(",",changed.Select(e=>e?.Id))+";cars="+string.Join(",",owners.Select(o=>o?.Id)));
        }

        static string[] WemExpectedChangedEngines(PlayerProfile input,string assetId,InventoryKind target,string targetId,PlayerProfile legacy)
        {
            var order=new List<string>();var requested=input.engines.Find(e=>e!=null&&e.instanceId==assetId);
            if(target==InventoryKind.Car&&requested!=null&&requested.parentVehicleId==targetId)return Array.Empty<string>();
            if(requested!=null&&!string.IsNullOrEmpty(requested.parentVehicleId))
            {
                var source=input.cars.Find(c=>c!=null&&c.instanceId==requested.parentVehicleId);
                var selected=source==null?null:input.engines.Find(e=>e!=null&&e.instanceId==source.engineAssemblyId);
                if(selected!=null)order.Add(selected.instanceId);
            }
            if(requested!=null&&target==InventoryKind.Storage)order.Add(assetId);
            if(target==InventoryKind.Car&&requested!=null&&requested.parentVehicleId!=targetId)order.Add(assetId);
            return order.Distinct(StringComparer.Ordinal).ToArray();
        }

        static string[] WemExpectedCarOwnerOrder(PlayerProfile input,string assetId,InventoryKind target,string targetId,PlayerProfile legacy)
        {
            var order=new List<string>();var requested=input.engines.Find(e=>e!=null&&e.instanceId==assetId);
            if(target==InventoryKind.Car&&requested!=null&&requested.parentVehicleId==targetId)return Array.Empty<string>();
            if(requested!=null&&!string.IsNullOrEmpty(requested.parentVehicleId)&&input.cars.Any(c=>c!=null&&c.instanceId==requested.parentVehicleId))order.Add(requested.parentVehicleId);
            if(target==InventoryKind.Car&&requested?.parentVehicleId!=targetId&&legacy.cars.Any(c=>c!=null&&c.instanceId==targetId))order.Add(targetId);
            return order.Distinct(StringComparer.Ordinal).ToArray();
        }

        static string WemEngineRaw(EngineAssemblyInstance e)
            =>e==null?"<null>":TypedCanonical(new object[]{e.instanceId,e.vehicleDefinitionId,e.customName,e.storageId,e.parentVehicleId,
                e.slots?.Select(x=>x==null?null:new SlotBinding{slot=x.slot,partInstanceId=x.partInstanceId}).ToArray(),e.tune?.Copy(),e.fluids?.Copy()},new Dictionary<string,string>());

        static string WemEngineSnapshot(WholeEngineMoveSnapshot e)
            =>e==null?"<null>":TypedCanonical(new object[]{e.Id,e.VehicleDefinitionId,e.CustomName,e.StorageId,e.ParentVehicleId,e.Bindings,e.Tune,e.Fluids},new Dictionary<string,string>());

        static string WemEnginePartsDifference(WholeEngineMoveSnapshot snapshot,EngineAssemblyInstance engine,PlayerProfile frozen)
        {
            if(snapshot==null||engine==null)return "missing snapshot or frozen engine";
            PartInstance[] expected=null;bool materialized=false;string errorType=null,errorMessage=null;
            try
            {
                if(!string.IsNullOrEmpty(engine.parentVehicleId))
                    throw new InvalidOperationException("Attached engine descendants are represented by the owning car.");
                if(engine.slots==null)throw new InvalidOperationException("Engine bindings are unavailable for descendant materialization.");
                var bindings=engine.slots;
                expected=bindings.Select(binding=>binding==null||string.IsNullOrWhiteSpace(binding.partInstanceId)?null:
                    frozen.parts.Find(part=>part!=null&&part.instanceId==binding.partInstanceId)?.Copy()).ToArray();
                if(expected.Any(part=>part==null))throw new InvalidOperationException("Engine contains an unresolved installed-part binding.");
                LegacyWholePartMoveReference.EngineDefinitions(frozen,engine);materialized=true;
            }
            catch(Exception error){expected=null;errorType=error.GetType().FullName;errorMessage=error.Message;}
            if(snapshot.Materialized!=materialized||snapshot.BuildErrorType!=errorType||snapshot.BuildErrorMessage!=errorMessage)return "materialization status/error";
            string expectedParts=WemNamedCanonical(expected),actualParts=WemNamedCanonical(snapshot.Parts);
            if(expectedParts!=actualParts)return "part values/backing member representation";
            Dictionary<PartCategory,string> expectedDefinitions=materialized?LegacyWholePartMoveReference.EngineDefinitions(frozen,engine):null;
            return WemDefinitionsCanonical(expectedDefinitions)==WemDefinitionsCanonical(snapshot.Definitions)?null:"ordered definition map";
        }

        static string WemDefinitionsCanonical(IDictionary<PartCategory,string> values)
            =>values==null?"<null>":string.Join(";",values.OrderBy(pair=>(int)pair.Key).Select(pair=>((int)pair.Key).ToString(System.Globalization.CultureInfo.InvariantCulture)+"="+WemSafe(pair.Value)));

        static string WemCarOwnerFirstDifference(WholePartMoveOwnerSnapshot owner,PlayerProfile p)
        {
            if(owner==null)return "null owner";
            var car=p.cars.Find(c=>c!=null&&c.instanceId==owner.Id);if(car==null)return "missing frozen car";
            var expected=new SortedDictionary<string,object>(StringComparer.Ordinal)
            {
                ["Id"]=car.instanceId,["VehicleId"]=car.vehicleId,["EngineAssemblyId"]=car.engineAssemblyId,["GarageJobId"]=car.garageJobId,
                ["HasRebuildJob"]=car.engineRebuild!=null,["RebuildActive"]=car.engineRebuild!=null&&car.engineRebuild.active,
                ["Bindings"]=car.installed?.slots?.Select(b=>b==null?null:new SlotBinding{slot=b.slot,partInstanceId=b.partInstanceId}).ToArray(),
                ["Tune"]=car.tune?.Copy(),["Fuel"]=car.fuel?.Copy(),["Fluids"]=car.fluids?.Copy()
            };
            var actual=new SortedDictionary<string,object>(StringComparer.Ordinal)
            {
                ["Id"]=owner.Id,["VehicleId"]=owner.VehicleId,["EngineAssemblyId"]=owner.EngineAssemblyId,["GarageJobId"]=owner.GarageJobId,
                ["HasRebuildJob"]=owner.HasRebuildJob,["RebuildActive"]=owner.RebuildActive,["Bindings"]=owner.Bindings,
                ["Tune"]=owner.Tune,["Fuel"]=owner.Fuel,["Fluids"]=owner.Fluids
            };
            foreach(var pair in expected)
            {
                string a=WemNamedCanonical(actual[pair.Key]),b=WemNamedCanonical(pair.Value);
                if(a!=b)return pair.Key+"|actual="+WemSafe(a)+"|expected="+WemSafe(b);
            }
            var expectedBuild=WemCarBuildFacts(p,car);var actualBuild=WemOwnerBuildFacts(owner);
            return expectedBuild==actualBuild?null:"optional materialization/parts/definitions/inspection first difference";
        }

        static string WemCarBuildFacts(PlayerProfile p,OwnedCar car)
        {
            Dictionary<PartCategory,string> definitions=null;
            var bindings=car.installed?.slots?.Select(b=>b==null?null:new SlotBinding{slot=b.slot,partInstanceId=b.partInstanceId}).ToArray();
            try
            {
                if(bindings==null)throw new InvalidOperationException("Affected car bindings are unavailable for inspection materialization.");
                var parts=bindings.Select(b=>string.IsNullOrWhiteSpace(b?.partInstanceId)?null:p.parts.Find(x=>x!=null&&x.instanceId==b.partInstanceId)?.Copy()).ToArray();
                if(parts.Any(x=>x==null))throw new InvalidOperationException("Affected car contains an unresolved installed-part binding.");
                definitions=LegacyWholePartMoveReference.CarDefinitions(p,car);
                if(car.fluids==null)throw new InvalidOperationException("Inspection requires captured fluid state.");
                if(car.fuel==null)throw new InvalidOperationException("Inspection requires captured fuel state.");
                var inspection=LegacyInstalledBuildReference.Capture(p,car);
                return WemNamedCanonical(new object[]{true,null,null,bindings,parts,inspection})+"|definitions="+WemDefinitionsCanonical(definitions);
            }
            catch(Exception error){return WemNamedCanonical(new object[]{false,error.GetType().FullName,error.Message,bindings,null,null})+"|definitions="+WemDefinitionsCanonical(null);}
        }

        static string WemOwnerBuildFacts(WholePartMoveOwnerSnapshot owner)
            =>WemNamedCanonical(new object[]{owner.Materialized,owner.BuildErrorType,owner.BuildErrorMessage,owner.Bindings,owner.Parts,owner.Inspection})+
                "|definitions="+WemDefinitionsCanonical(owner.Definitions);

        static void WemLiveWrapperMatchesFrozen(Action<bool,string> check,string name,PlayerProfile source,string assetId,
            InventoryKind target,string targetId,PlayerProfile frozen,Exception frozenError)
        {
            var live=CartPreviewLiteralTestClone.Copy(source);Exception liveError=null;
            try{SharedInventory.Move(live,assetId,target,targetId);}catch(Exception error){liveError=error;}
            check((liveError?.GetType().FullName)==(frozenError?.GetType().FullName)&&liveError?.Message==frozenError?.Message,
                "public live wrapper exact error parity with frozen oracle / "+name);
            string diff=WemChangedStateDifference(frozen,live);
            check(diff==null,"public live wrapper graph matches frozen oracle / "+name+(diff==null?"":";firstDiff="+diff));
        }

        static string WemChangedStateDifference(PlayerProfile a,PlayerProfile b)
        {
            if(SourceSnapshot(a)==SourceSnapshot(b))return null;
            string x=TypedCanonical(a.engines,new Dictionary<string,string>()),y=TypedCanonical(b.engines,new Dictionary<string,string>());
            if(x!=y)return "engines";
            x=TypedCanonical(a.cars,new Dictionary<string,string>());y=TypedCanonical(b.cars,new Dictionary<string,string>());if(x!=y)return "cars";
            x=TypedCanonical(a.parts,new Dictionary<string,string>());y=TypedCanonical(b.parts,new Dictionary<string,string>());if(x!=y)return "parts";
            x=TypedCanonical(a.looseEngineElements,new Dictionary<string,string>());y=TypedCanonical(b.looseEngineElements,new Dictionary<string,string>());if(x!=y)return "loose-engine-elements";
            return "other-profile-mutable-fields";
        }

        static void WemOutputIsolation(Action<bool,string> check,string name,PlayerProfile input,string assetId,InventoryKind target,string targetId,
            WholeEngineMoveResult result,string sourceBefore)
        {
            string outputBefore=WemResultFingerprint(result);
            if(result.MovedEngine?.Tune!=null)result.MovedEngine.Tune.launchRpm+=1;
            if(result.MovedEngine?.Fluids!=null)result.MovedEngine.Fluids.oilLitres+=1;
            if(result.MovedEngine?.Bindings!=null&&result.MovedEngine.Bindings.Length>0&&result.MovedEngine.Bindings[0]!=null)result.MovedEngine.Bindings[0].slot+="-mutated";
            if(result.MovedEngine?.Parts!=null&&result.MovedEngine.Parts.Length>0&&result.MovedEngine.Parts[0]?.condition?.elements!=null&&result.MovedEngine.Parts[0].condition.elements.Length>0)result.MovedEngine.Parts[0].condition.elements[0].health-=.01f;
            if(result.MovedEngine?.Definitions!=null&&result.MovedEngine.Definitions.Count>0)result.MovedEngine.Definitions[result.MovedEngine.Definitions.Keys.First()]= "mutated-definition";
            foreach(var owner in result.CarOwners??Array.Empty<WholePartMoveOwnerSnapshot>())
            {
                if(owner.Tune!=null)owner.Tune.launchRpm+=1;
                if(owner.Fluids!=null)owner.Fluids.oilLitres+=1;
                if(owner.Bindings!=null&&owner.Bindings.Length>0&&owner.Bindings[0]!=null)owner.Bindings[0].slot+="-mutated";
                if(owner.Definitions!=null&&owner.Definitions.Count>0)owner.Definitions[owner.Definitions.Keys.First()]= "mutated-definition";
                if(owner.Parts!=null&&owner.Parts.Length>0&&owner.Parts[0]?.condition?.elements!=null&&owner.Parts[0].condition.elements.Length>0)owner.Parts[0].condition.elements[0].health-=.01f;
            }
            foreach(var changed in result.ChangedEngines??Array.Empty<WholeEngineMoveSnapshot>())
            {
                if(changed?.Tune!=null)changed.Tune.shiftRpm+=1;
                if(changed?.Fluids!=null)changed.Fluids.coolantLitres+=1;
                if(changed?.Bindings!=null&&changed.Bindings.Length>0&&changed.Bindings[0]!=null)changed.Bindings[0].slot+="-mutated";
            }
            check(sourceBefore==SourceSnapshot(input),"mutating nested engine/car snapshot outputs cannot alter source / "+name);
            var second=WholeEngineMoveProjection.Evaluate(input,assetId,target,targetId,input.catalog);
            check(second!=null&&second.Success&&WemResultFingerprint(second)==outputBefore,"output mutation does not change fresh evaluation / "+name);
        }

        static string WemResultFingerprint(WholeEngineMoveResult r)
        {
            if(r==null)return "<null>";
            var changed=(r.ChangedEngines??Array.Empty<WholeEngineMoveSnapshot>()).Select(WemFullEngineSnapshot).ToArray();
            var cars=(r.CarOwners??Array.Empty<WholePartMoveOwnerSnapshot>()).Select(o=>WemNamedCanonical(new object[]{o.Id,o.VehicleId,o.EngineAssemblyId,o.GarageJobId,o.HasRebuildJob,o.RebuildActive,o.Bindings,o.Tune,o.Fuel,o.Fluids,
                o.Materialized,o.BuildErrorType,o.BuildErrorMessage,o.Parts,WemDefinitionsCanonical(o.Definitions),o.Inspection})).ToArray();
            return WemNamedCanonical(new object[]{r.Success,r.ErrorType,r.ErrorMessage,WemFullEngineSnapshot(r.MovedEngine),changed,cars});
        }

        static string WemFullEngineSnapshot(WholeEngineMoveSnapshot e)
            =>WemNamedCanonical(new object[]{WemEngineSnapshot(e),e?.Parts,e==null?null:WemDefinitionsCanonical(e.Definitions),e?.Materialized,e?.BuildErrorType,e?.BuildErrorMessage});

        static void RunWemNullSlotsAttach(Action<bool,string> check,StringBuilder log)
        {
            RunWemPrepared(check,log,"null-engine-slots-attach-late-argument-null",WholeEngineMoveSeed+16,p=>
            {
                if(!GraphCreateSameModelPair(p,out var source,out var target,out string reason))throw new InvalidOperationException("OPEN: "+reason);
                var e=WemAttachedEngine(p,source);WemMakeBare(p,target);LegacyWholeEngineMoveReference.Detach(p,source.instanceId);e.slots=null;
                return Tuple.Create(e.instanceId,InventoryKind.Car,target.instanceId);
            },false);
        }

        static void RunWemMalformedParentCar(Action<bool,string> check,StringBuilder log)
            =>RunWemPrepared(check,log,"missing-parent-car",WholeEngineMoveSeed+17,p=>
            {
                var e=WemAttachedEngine(p,p.SelectedCar);e.parentVehicleId="missing-parent";return Tuple.Create(e.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            },false);

        static void RunWemMissingReferencedEngine(Action<bool,string> check,StringBuilder log)
            =>RunWemPrepared(check,log,"car-referenced-engine-missing",WholeEngineMoveSeed+18,p=>
            {
                var car=p.SelectedCar;var e=WemAttachedEngine(p,car);e.parentVehicleId=car.instanceId;car.engineAssemblyId="missing-engine";
                return Tuple.Create(e.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            },false);

        static void RunWemMalformedParentBindingDisagreement(Action<bool,string> check,StringBuilder log)
            =>RunWemPrepared(check,log,"parent-car-references-different-engine",WholeEngineMoveSeed+19,p=>
            {
                if(!GraphCreateSameModelPair(p,out var source,out var other,out string reason))throw new InvalidOperationException("OPEN: "+reason);
                var requested=WemAttachedEngine(p,source);var selected=WemAttachedEngine(p,other);requested.parentVehicleId=source.instanceId;source.engineAssemblyId=selected.instanceId;
                return Tuple.Create(requested.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            });

        static void RunWemMalformedInvalidSlot(Action<bool,string> check,StringBuilder log)
            =>RunWemPrepared(check,log,"invalid-source-slot-parse",WholeEngineMoveSeed+20,p=>
            {
                var e=WemAttachedEngine(p,p.SelectedCar);p.SelectedCar.installed.slots.Add(new SlotBinding{slot="not-a-category",partInstanceId="bad"});
                return Tuple.Create(e.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            },false);

        static void RunWemMalformedNullCarTune(Action<bool,string> check,StringBuilder log)
            =>RunWemPrepared(check,log,"null-source-car-tune-after-detach-writes",WholeEngineMoveSeed+21,p=>
            {
                var e=WemAttachedEngine(p,p.SelectedCar);p.SelectedCar.tune=null;return Tuple.Create(e.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            },false);

        static void RunWemMalformedLateRankFailure(Action<bool,string> check,StringBuilder log)
            =>RunWemPrepared(check,log,"late-source-rank-failure",WholeEngineMoveSeed+22,p=>
            {
                var e=WemAttachedEngine(p,p.SelectedCar);p.SelectedCar.vehicleId="missing-authored-vehicle";
                return Tuple.Create(e.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            },false);

        static void RunWemMalformedOverwrittenEngineSlots(Action<bool,string> check,StringBuilder log)
            =>RunWemPrepared(check,log,"preexisting-stored-slots-overwritten-on-detach",WholeEngineMoveSeed+23,p=>
            {
                var e=WemAttachedEngine(p,p.SelectedCar);e.slots.Add(new SlotBinding{slot="unrelated-retained-value",partInstanceId="retained-id"});
                return Tuple.Create(e.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            });

        static void WemOpen(Action<bool,string> check,StringBuilder log,string name,Exception error)
        {
            log.AppendLine("OPEN|"+name+"|"+WemSafe(error.GetType().FullName+" | "+error.Message));
            check(false,"real authored fixture available / "+name+" / "+error.GetType().FullName+" | "+error.Message);
        }

        static void WemSourceAudit(Action<bool,string> check,StringBuilder log)
        {
            string path=Path.Combine(UnityEngine.Application.dataPath,"DragLab","Scripts","Domain","WholeEngineMoveProjection.cs");
            if(!File.Exists(path)){check(false,"projection source available for bounded forbidden-token audit");return;}
            string source=File.ReadAllText(path,new UTF8Encoding(false));
            string[] forbidden={"JsonUtility","RuntimeDataEncodingProbe","MechanicsOwnership.NewId","RuntimeEntropy.NewGuid","UnityEngine.Random","PlayerProfile.Copy","CloneProfile"};
            var found=forbidden.Where(token=>source.IndexOf(token,StringComparison.Ordinal)>=0).ToArray();
            check(found.Length==0,"projection source excludes runtime JSON/full-profile copy/identity/RNG APIs / "+string.Join(",",found));
            log.AppendLine("SOURCE_AUDIT|forbiddenTokens="+forbidden.Length+";matches="+string.Join(",",found));
        }

        static string WemNamedCanonical(object value)=>TypedCanonical(value,new Dictionary<string,string>());
        static string WemOutcome(WholeEngineMoveResult result,Exception thrown)
            =>thrown!=null?thrown.GetType().FullName+" | "+thrown.Message:result==null?"<null result>":result.Success?"success":result.ErrorType+" | "+result.ErrorMessage;
        static string WemError(Exception error)=>error==null?"success":error.GetType().FullName+" | "+error.Message;
        static string WemSafe(string value)=>string.IsNullOrEmpty(value)?"<empty>":value.Replace("\r","\\r").Replace("\n","\\n").Replace("|","/");
    }
}
#endif
