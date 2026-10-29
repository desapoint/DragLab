#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    /// <summary>Finite Editor-only differential checks for ordered raw-domain inventory moves.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint OrderedMoveSeed=20261101u;

        sealed class OrderedMoveFixture
        {
            internal PlayerProfile Profile;
            internal OwnedCar A,B;
            internal EngineAssemblyInstance EngineA,EngineB;
            internal PartInstance PistonA,PistonB;
            internal LooseEngineElement TireUnit;
        }

        sealed class OrderedMoveRun
        {
            internal OrderedInventoryMoveResult Projection;
            internal PlayerProfile Frozen,Live;
            internal Exception FrozenError,LiveError;
            internal int FrozenFailedIndex,LiveFailedIndex;
        }

        public static void RunOrderedInventoryMove(Action<bool,string> check,string outputFolder)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(outputFolder))throw new ArgumentException("An isolated report folder is required.",nameof(outputFolder));
            Directory.CreateDirectory(outputFolder);
            var log=new StringBuilder();int assertions=0,failures=0;
            Action<bool,string> record=(ok,label)=>{assertions++;if(!ok)failures++;check(ok,"ordered-inventory-move/"+label);log.AppendLine("ASSERT|"+(ok?"PASS":"FAIL")+"|"+KSafe(label));};
            log.AppendLine("S06D2B1K_ORDERED_INVENTORY_MOVE_PROJECTION");
            log.AppendLine("fixture=authored/current catalogue;oracle=LegacyOrderedInventoryMoveReference;candidate=one private ordered working set");
            try
            {
                KMixedOverlayAndDisplacement(record,log);
                KReverseOrderChangesOutcome(record,log);
                KLateFailureDiscardsPrivatePrefix(record,log);
                KRepeatedActualUnitHundred(record,log);
                KDistinctActualPartsHundred(record,log);
                KRequestEdges(record,log);
                KFamilyCollision(record,log);
                KSourceAudit(record,log);
            }
            catch(Exception error)
            {
                record(false,"runner exception="+error.GetType().FullName+" | "+error.Message);
                log.AppendLine("RUNNER_EXCEPTION|"+KSafe(error.GetType().FullName)+"|"+KSafe(error.Message));
            }
            finally
            {
                log.AppendLine("SUMMARY|assertions="+assertions+";failures="+failures);
                File.WriteAllText(Path.Combine(outputFolder,"S06D2B1K_ORDERED_INVENTORY_MOVE_PROJECTION.txt"),log.ToString(),new UTF8Encoding(false));
            }
        }

        static void KMixedOverlayAndDisplacement(Action<bool,string> check,StringBuilder log)
        {
            if(!KCreatePair(OrderedMoveSeed,true,out var fixture,out string reason)){KOpen(check,log,"mixed-engine-part-unit-overlay",reason);return;}
            var p=fixture.Profile;var a=fixture.A;var b=fixture.B;var engineA=fixture.EngineA;var engineB=fixture.EngineB;
            if(fixture.PistonA==null||fixture.PistonB==null||fixture.TireUnit?.element==null)
            {KOpen(check,log,"mixed-engine-part-unit-overlay","actual piston parts or acquired tire unit are unavailable");return;}
            var requests=new InventoryMoveRequest[]
            {
                new InventoryMoveRequest(engineA.instanceId,InventoryKind.Car,b.instanceId),
                new InventoryMoveRequest(fixture.PistonA.instanceId,InventoryKind.Storage,SharedInventory.Garage),
                new InventoryMoveRequest(fixture.TireUnit.element.id,InventoryKind.Storage,SharedInventory.Storage),
                new InventoryMoveRequest(fixture.PistonA.instanceId,InventoryKind.Engine,engineB.instanceId),
                new InventoryMoveRequest(fixture.PistonB.instanceId,InventoryKind.Storage,SharedInventory.Garage)
            };
            string invalid=MechanicsOwnership.Validate(p,p.catalog);
            check(invalid==null,"mixed fixture starts from actual validated owned assets / "+(invalid??"valid"));if(invalid!=null)return;
            var run=KRun(check,log,"mixed-engine-part-unit-overlay",p,requests,true,verifyLive:true);
            if(run?.Projection==null||!run.Projection.Success||run.FrozenError!=null)return;
            KCompareOwnedOutputs(check,log,"mixed-engine-part-unit-overlay",p,run.Frozen,run.Projection,
                new[]{fixture.PistonA.instanceId,fixture.PistonB.instanceId},
                new[]{engineA.instanceId,engineB.instanceId},new[]{fixture.TireUnit.element.id},new[]{a.instanceId,b.instanceId});
            var finalA=run.Frozen.engines.Find(e=>e!=null&&e.instanceId==engineA.instanceId);
            var finalB=run.Frozen.engines.Find(e=>e!=null&&e.instanceId==engineB.instanceId);
            var finalCarA=run.Frozen.cars.Find(car=>car!=null&&car.instanceId==a.instanceId);
            var finalCarB=run.Frozen.cars.Find(car=>car!=null&&car.instanceId==b.instanceId);
            check(finalA?.parentVehicleId==b.instanceId&&finalCarA?.engineAssemblyId==""&&finalCarB?.engineAssemblyId==engineA.instanceId,
                "later rows observe engine A attached to B and A left bare");
            check(finalB!=null&&finalB.slots.Any(binding=>binding!=null&&binding.partInstanceId==fixture.PistonA.instanceId)&&
                !finalB.slots.Any(binding=>binding!=null&&binding.partInstanceId==fixture.PistonB.instanceId),
                "later install sees engine B's updated private binding overlay");
            var displaced=run.Frozen.parts.Find(part=>part!=null&&part.instanceId==fixture.PistonB.instanceId);
            check(displaced!=null&&displaced.location==PartLocation.Inventory&&displaced.storageId==SharedInventory.Garage,
                "displaced engine part is visible to its later Storage request");
            KOutputMutationIsolation(check,"mixed-engine-part-unit-overlay",p,requests,run.Projection);
        }

        static void KReverseOrderChangesOutcome(Action<bool,string> check,StringBuilder log)
        {
            if(!KCreatePair(OrderedMoveSeed+1,false,out var fixture,out string reason)){KOpen(check,log,"reverse-order-changes-outcome",reason);return;}
            var p=fixture.Profile;var a=fixture.A;var b=fixture.B;
            var forward=new InventoryMoveRequest[]
            {
                new InventoryMoveRequest(fixture.PistonA.instanceId,InventoryKind.Engine,fixture.EngineB.instanceId),
                new InventoryMoveRequest(fixture.EngineB.instanceId,InventoryKind.Car,b.instanceId)
            };
            var reverse=new InventoryMoveRequest[]
            {
                forward[1],forward[0]
            };
            var first=KRun(check,log,"reverse-order-forward",p,forward,true,verifyLive:true,
                expectedParts:new[]{fixture.PistonA.instanceId,fixture.PistonB.instanceId},
                expectedEngines:new[]{fixture.EngineB.instanceId},expectedUnits:Array.Empty<string>(),expectedCars:new[]{a.instanceId,b.instanceId});
            var second=KRun(check,log,"reverse-order-reversed",p,reverse,false,expectedIndex:1,verifyLive:true);
            check(first?.Frozen!=null&&second?.Frozen!=null&&first.Projection?.Success==true&&second.Projection?.Success==false&&second.Projection.FailedIndex==1,
                "request order changes a valid forward attach into a frozen failure at row 1");
        }

        static void KLateFailureDiscardsPrivatePrefix(Action<bool,string> check,StringBuilder log)
        {
            if(!KCreatePair(OrderedMoveSeed+2,false,out var fixture,out string reason)){KOpen(check,log,"late-failure-after-prior-row",reason);return;}
            var p=fixture.Profile;var target=fixture.A;var source=fixture.B;
            var firstPart=KCarPart(p,target,PartCategory.Pistons);
            if(firstPart==null){KOpen(check,log,"late-failure-after-prior-row","actual attached engine A has a fitted piston");return;}
            LegacyWholeEngineMoveReference.Move(p,fixture.EngineA.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            target.garageJobId="k-target-job";
            var requests=new InventoryMoveRequest[]
            {
                new InventoryMoveRequest(fixture.EngineA.instanceId,InventoryKind.Car,source.instanceId),
                new InventoryMoveRequest(firstPart.instanceId,InventoryKind.Car,target.instanceId)
            };
            KRun(check,log,"late-failure-after-prior-row",p,requests,false,expectedIndex:1,verifyLive:true);
        }

        static void KRepeatedActualUnitHundred(Action<bool,string> check,StringBuilder log)
        {
            if(!KCreateNormalizedWithTire(OrderedMoveSeed+3,out var p,out var unit,out string reason)){KOpen(check,log,"100-repeated-actual-tire-unit-moves",reason);return;}
            var requests=new InventoryMoveRequest[100];
            for(int i=0;i<requests.Length;i++)requests[i]=new InventoryMoveRequest(unit.element.id,InventoryKind.Storage,
                i%2==0?SharedInventory.Storage:SharedInventory.Garage);
            var run=KRun(check,log,"100-repeated-actual-tire-unit-moves",p,requests,true,verifyLive:true);
            if(run?.Projection==null||!run.Projection.Success||run.FrozenError!=null)return;
            check(run.Projection.ChangedUnits!=null&&run.Projection.ChangedUnits.Select(item=>item?.Id).SequenceEqual(new[]{unit.element.id},StringComparer.Ordinal),
                "100 raw repeated requests remain ordered and yield one first-written unit record");
            check(run.Projection.ChangedParts!=null&&run.Projection.ChangedParts.Length==0&&run.Projection.ChangedEngines!=null&&run.Projection.ChangedEngines.Length==0&&
                run.Projection.CarOwners!=null&&run.Projection.CarOwners.Length==0,"unit-only sequence returns empty arrays for untouched families");
            KCompareOwnedOutputs(check,log,"100-repeated-actual-tire-unit-moves",p,run.Frozen,run.Projection,
                Array.Empty<string>(),Array.Empty<string>(),new[]{unit.element.id},Array.Empty<string>());
            var snapshot=run.Projection.ChangedUnits[0];var expected=run.Frozen.looseEngineElements.Find(value=>value?.element?.id==unit.element.id);
            check(KUnitSnapshotFacts(snapshot)==KLooseUnitFacts(expected),"actual tire subtype/wear/history and storage match the frozen repeated-move result");
            check(snapshot.Element is TireCondition,"unit snapshot retains concrete TireCondition subtype");
            KOutputMutationIsolation(check,"100-repeated-actual-tire-unit-moves",p,requests,run.Projection);
        }

        static void KDistinctActualPartsHundred(Action<bool,string> check,StringBuilder log)
        {
            if(!KCreateHundredPartsFixture(OrderedMoveSeed+4,out var p,out var partIds,out var ownerIds,out string reason,out int carsSeen,out int partsSeen))
            {
                log.AppendLine("OPEN|100-distinct-real-part-moves|cars="+carsSeen+";installedParts="+partsSeen+";reason="+KSafe(reason));
                check(false,"fixture supplies 100 distinct actual owned installed parts / cars="+carsSeen+";parts="+partsSeen+";"+KSafe(reason));return;
            }
            var requests=partIds.Select(id=>new InventoryMoveRequest(id,InventoryKind.Storage,SharedInventory.Garage)).ToArray();
            var run=KRun(check,log,"100-distinct-actual-part-moves",p,requests,true,verifyLive:true);
            if(run?.Projection==null||!run.Projection.Success||run.FrozenError!=null)return;
            check(run.Projection.ChangedParts!=null&&run.Projection.ChangedParts.Select(part=>part?.instanceId).SequenceEqual(partIds,StringComparer.Ordinal),
                "100 distinct real physical IDs remain separate and ordered through projection output");
            check(run.Projection.ChangedUnits!=null&&run.Projection.ChangedUnits.Length==0&&run.Projection.ChangedEngines!=null&&run.Projection.ChangedEngines.Length==0,
                "100 part-only sequence reports no engine/unit writes");
            KCompareOwnedOutputs(check,log,"100-distinct-actual-part-moves",p,run.Frozen,run.Projection,
                partIds,Array.Empty<string>(),Array.Empty<string>(),ownerIds);
            KMeasureHundredPartCost(check,log,p,requests);
        }

        static void KRequestEdges(Action<bool,string> check,StringBuilder log)
        {
            // Empty is an identity operation; null profile proves no source/target read is needed.
            var empty=OrderedInventoryMoveProjection.Evaluate(null,Array.Empty<InventoryMoveRequest>(),null);
            check(empty!=null&&empty.Success&&empty.FailedIndex==-1&&empty.ChangedParts!=null&&empty.ChangedParts.Length==0&&
                empty.ChangedEngines!=null&&empty.ChangedEngines.Length==0&&empty.ChangedUnits!=null&&empty.ChangedUnits.Length==0&&
                empty.CarOwners!=null&&empty.CarOwners.Length==0,"empty list succeeds without reading a profile/catalogue and returns owned empty arrays");
            log.AppendLine("VECTOR|empty|success=true;output=empty-owned-arrays");

            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out string carId,OrderedMoveSeed+5);
            var before=SourceSnapshot(p);var refs=NormalizationReferences(p);long json=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            var nullContainer=KRun(check,log,"null-container",p,null,false,expectedIndex:-1,verifyLive:false);
            check(before==SourceSnapshot(p)&&NormalizationReferenceDiff(refs,NormalizationReferences(p)).Count==0&&json==RuntimeDataEncodingProbe.CurrentThreadJsonCallCount,
                "null request container reports error without source/reference/JSON changes");
            if(nullContainer?.Projection!=null)
                check(nullContainer.Projection.ErrorType==typeof(ArgumentNullException).FullName&&
                    nullContainer.FrozenError is ArgumentNullException argumentNull&&argumentNull.ParamName=="requests",
                    "null request container keeps ArgumentNullException(requests) contract with frozen runtime message");

            var nullRow=new InventoryMoveRequest[]{null};
            KRun(check,log,"null-row",p,nullRow,false,expectedIndex:0,verifyLive:true);
            var nullAsset=new[]{new InventoryMoveRequest(null,InventoryKind.Storage,SharedInventory.Garage)};
            KRun(check,log,"null-asset-id",p,nullAsset,false,expectedIndex:0,verifyLive:true);
            var nullStorage=new[]{new InventoryMoveRequest("missing-asset",InventoryKind.Storage,null)};
            KRun(check,log,"null-storage-id-first-guard",p,nullStorage,false,expectedIndex:0,verifyLive:true);
        }

        static void KFamilyCollision(Action<bool,string> check,StringBuilder log)
        {
            if(!KCreateNormalizedWithTire(OrderedMoveSeed+6,out var p,out var unit,out string reason)){KOpen(check,log,"unit-before-engine-collision",reason);return;}
            var attached=p.engines.FirstOrDefault(engine=>engine!=null&&!string.IsNullOrEmpty(engine.parentVehicleId));
            if(attached==null){KOpen(check,log,"unit-before-engine-collision","fixture has no actual attached engine ID to collide with");return;}
            string actualEngineId=attached.instanceId;unit.element.id=actualEngineId;
            var request=new[]{new InventoryMoveRequest(actualEngineId,InventoryKind.Storage,SharedInventory.Storage)};
            var run=KRun(check,log,"unit-before-engine-collision",p,request,true,verifyLive:true);
            if(run?.Projection==null||!run.Projection.Success)return;
            check(run.Projection.ChangedUnits!=null&&run.Projection.ChangedUnits.Length==1&&run.Projection.ChangedUnits[0].Id==actualEngineId&&
                run.Projection.ChangedEngines!=null&&run.Projection.ChangedEngines.Length==0&&run.Projection.CarOwners!=null&&run.Projection.CarOwners.Length==0,
                "real loose-unit classification wins a deliberate unit/engine ID collision");
        }

        static OrderedMoveRun KRun(Action<bool,string> check,StringBuilder log,string name,PlayerProfile profile,
            IReadOnlyList<InventoryMoveRequest> requests,bool expectSuccess,int expectedIndex=-1,bool verifyLive=true,
            string[] expectedParts=null,string[] expectedEngines=null,string[] expectedUnits=null,string[] expectedCars=null)
        {
            string before=profile==null?"<null-profile>":SourceSnapshot(profile);
            string catalog=profile==null?"<null-catalogue>":CatalogSnapshot(profile);
            var refs=profile==null?null:NormalizationReferences(profile);
            long json=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;var unity=UnityEngine.Random.state;
            bool entropy=RuntimeEntropy.Capture;DateTime clock=RuntimeEntropy.UtcNow;var identity=RuntimeEntropy.BeginIdentityObservation(64);
            OrderedInventoryMoveResult projection=null;Exception thrown=null;
            try{projection=OrderedInventoryMoveProjection.Evaluate(profile,requests,profile?.catalog);}catch(Exception error){thrown=error;}finally{identity.Dispose();}
            check(identity.Closed&&!identity.Overflowed&&identity.Count==0,"projection creates no IDs / "+name);
            check(json==RuntimeDataEncodingProbe.CurrentThreadJsonCallCount,"projection calls no instrumented JSON route / "+name);
            check(unity.Equals(UnityEngine.Random.state),"projection preserves Unity RNG / "+name);
            check(entropy==RuntimeEntropy.Capture,"projection preserves RuntimeEntropy mode / "+name);
            if(entropy)check(clock.Ticks==RuntimeEntropy.UtcNow.Ticks&&clock.Kind==RuntimeEntropy.UtcNow.Kind,"captured clock stays exact / "+name);
            if(profile!=null)
            {
                check(before==SourceSnapshot(profile)&&NormalizationReferenceDiff(refs,NormalizationReferences(profile)).Count==0,
                    "projection preserves full source values/reference topology / "+name);
                check(catalog==CatalogSnapshot(profile),"projection preserves borrowed catalogue / "+name);
            }

            var frozen=profile==null?null:CartPreviewLiteralTestClone.Copy(profile);Exception frozenError=null;int frozenIndex=-1;
            if(frozen!=null)frozenError=LegacyOrderedInventoryMoveReference.Apply(frozen,requests,out frozenIndex);
            var live=profile==null?null:CartPreviewLiteralTestClone.Copy(profile);Exception liveError=null;int liveIndex=-1;
            if(verifyLive&&live!=null&&requests!=null)
            {
                for(int i=0;i<requests.Count;i++)
                {
                    liveIndex=i;
                    try
                    {
                        var row=requests[i];SharedInventory.Move(live,row.AssetId,row.Destination,row.TargetId);
                    }
                    catch(Exception error){liveError=error;break;}
                }
                if(liveError==null)liveIndex=-1;
                check((liveError?.GetType().FullName)==(frozenError?.GetType().FullName)&&liveError?.Message==frozenError?.Message&&liveIndex==frozenIndex,
                    "current live row loop matches frozen first error and index / "+name);
                string liveDiff=liveError==null&&frozenError==null?KProfileDifference(frozen,live):KProfileDifference(frozen,live);
                check(liveDiff==null,"current live row loop full profile matches frozen / "+name+(liveDiff==null?"":";firstGroup="+liveDiff));
            }

            bool success=thrown==null&&projection!=null&&projection.Success;
            check(success==expectSuccess,"private sequence outcome matches expected / "+name+" / "+KOutcome(projection,thrown));
            if(frozen!=null)check((frozenError==null)==expectSuccess,"frozen sequence outcome matches expected / "+name+" / "+KError(frozenError));
            if(!expectSuccess)
            {
                check(thrown==null&&projection!=null&&!projection.Success&&projection.ErrorType==frozenError?.GetType().FullName&&
                    projection.ErrorMessage==frozenError?.Message&&projection.FailedIndex==frozenIndex&&projection.FailedIndex==expectedIndex,
                    "exact frozen error and zero-based failed row / "+name+" / typed="+KOutcome(projection,thrown)+";frozen="+KError(frozenError)+";index="+projection?.FailedIndex);
                check(projection==null||projection.ChangedParts==null&&projection.ChangedEngines==null&&projection.ChangedUnits==null&&projection.CarOwners==null,
                    "failure exposes no prefix/final output arrays / "+name);
                if(profile!=null)check(before==SourceSnapshot(profile)&&NormalizationReferenceDiff(refs,NormalizationReferences(profile)).Count==0,
                    "failure discards all private prefix writes and preserves source / "+name);
                log.AppendLine("VECTOR|"+name+"|success=false;failedIndex="+(projection?.FailedIndex??-2)+";error="+KSafe(KOutcome(projection,thrown)));
                return new OrderedMoveRun{Projection=projection,Frozen=frozen,Live=live,FrozenError=frozenError,LiveError=liveError,FrozenFailedIndex=frozenIndex,LiveFailedIndex=liveIndex};
            }
            if(!success||frozenError!=null)return new OrderedMoveRun{Projection=projection,Frozen=frozen,Live=live,FrozenError=frozenError,LiveError=liveError,FrozenFailedIndex=frozenIndex,LiveFailedIndex=liveIndex};

            check(projection.FailedIndex==-1,"successful sequence uses FailedIndex=-1 / "+name);
            KCompareOwnedOutputs(check,log,name,profile,frozen,projection,expectedParts,expectedEngines,expectedUnits,expectedCars);
            log.AppendLine("VECTOR|"+name+"|success=true;parts="+string.Join(",",projection.ChangedParts.Select(item=>item?.instanceId))+
                ";engines="+string.Join(",",projection.ChangedEngines.Select(item=>item?.Id))+";units="+
                string.Join(",",projection.ChangedUnits.Select(item=>item?.Id))+";cars="+string.Join(",",projection.CarOwners.Select(item=>item?.Id)));
            return new OrderedMoveRun{Projection=projection,Frozen=frozen,Live=live,FrozenError=frozenError,LiveError=liveError,FrozenFailedIndex=frozenIndex,LiveFailedIndex=liveIndex};
        }

        static void KCompareOwnedOutputs(Action<bool,string> check,StringBuilder log,string name,PlayerProfile input,PlayerProfile frozen,
            OrderedInventoryMoveResult result,string[] expectedParts,string[] expectedEngines,string[] expectedUnits,string[] expectedCars)
        {
            var parts=result.ChangedParts??Array.Empty<PartInstance>();
            var engines=result.ChangedEngines??Array.Empty<WholeEngineMoveSnapshot>();
            var units=result.ChangedUnits??Array.Empty<LooseUnitMoveSnapshot>();
            var cars=result.CarOwners??Array.Empty<WholePartMoveOwnerSnapshot>();
            if(expectedParts!=null)check(parts.Select(item=>item?.instanceId).SequenceEqual(expectedParts,StringComparer.Ordinal),"ChangedParts write order/IDs exact / "+name);
            if(expectedEngines!=null)check(engines.Select(item=>item?.Id).SequenceEqual(expectedEngines,StringComparer.Ordinal),"ChangedEngines first-write order/IDs exact / "+name);
            if(expectedUnits!=null)check(units.Select(item=>item?.Id).SequenceEqual(expectedUnits,StringComparer.Ordinal),"ChangedUnits first-write order/IDs exact / "+name);
            if(expectedCars!=null)check(cars.Select(item=>item?.Id).SequenceEqual(expectedCars,StringComparer.Ordinal),"CarOwners first-affected order/IDs exact / "+name);
            check(parts.All(item=>item!=null)&&parts.Select(item=>item.instanceId).Distinct(StringComparer.Ordinal).Count()==parts.Length,
                "changed physical parts are nonnull and identity-deduplicated / "+name);
            check(engines.All(item=>item!=null)&&engines.Select(item=>item.Id).Distinct(StringComparer.Ordinal).Count()==engines.Length,
                "changed engine snapshots are nonnull and source-reference-deduplicated / "+name);
            check(units.All(item=>item!=null)&&units.Select(item=>item.Id).Distinct(StringComparer.Ordinal).Count()==units.Length,
                "changed unit snapshots are nonnull and identity-deduplicated / "+name);
            foreach(var item in parts)
            {
                var expected=frozen.parts.Find(part=>part!=null&&part.instanceId==item.instanceId);
                check(expected!=null&&TypedCanonical(expected.Copy(),new Dictionary<string,string>())==TypedCanonical(item,new Dictionary<string,string>()),
                    "changed part exact member/subtype/condition facts match frozen output / "+name+" / "+item.instanceId);
                check(!input.parts.Any(part=>ReferenceEquals(part,item)),"changed part snapshot is owned / "+name+" / "+item.instanceId);
            }
            foreach(var item in engines)
            {
                var expected=frozen.engines.Find(engine=>engine!=null&&engine.instanceId==item.Id);
                check(expected!=null&&WemEngineRaw(expected)==WemEngineSnapshot(item),"changed engine raw fields/bindings/tune/fluid match frozen output / "+name+" / "+item.Id);
                string diff=WemEnginePartsDifference(item,expected,frozen);
                check(diff==null,"changed engine descendants/materialization match frozen copy / "+name+" / "+item.Id+(diff==null?"":";firstDiff="+diff));
            }
            foreach(var item in units)
            {
                var expected=frozen.looseEngineElements.Find(unit=>unit?.element?.id==item.Id);
                check(expected!=null&&KUnitSnapshotFacts(item)==KLooseUnitFacts(expected),"changed unit exact scalar/element subtype/history matches frozen / "+name+" / "+item.Id);
                check(!input.looseEngineElements.Any(unit=>ReferenceEquals(unit,item)),"changed unit snapshot is owned / "+name+" / "+item.Id);
                if(item.Element is TireCondition)check(item.Element.GetType()==typeof(TireCondition),"tire unit subtype is preserved / "+name+" / "+item.Id);
            }
            foreach(var owner in cars)
            {
                string diff=WemCarOwnerFirstDifference(owner,frozen);
                check(diff==null,"affected car binding order/tune/fluid/inspection match frozen output / "+name+" / "+owner?.Id+(diff==null?"":";firstDiff="+diff));
            }
        }

        static bool KCreatePair(uint seed,bool withTire,out OrderedMoveFixture fixture,out string reason)
        {
            fixture=null;reason=null;
            try
            {
                CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out string ignored,seed);
                if(!GraphCreateSameModelPair(p,out var a,out var b,out reason))return false;
                var engineA=p.engines.Find(engine=>engine!=null&&engine.instanceId==a.engineAssemblyId);
                var engineB=p.engines.Find(engine=>engine!=null&&engine.instanceId==b.engineAssemblyId);
                if(engineA==null||engineB==null){reason="actual same-model pair has no two attached engines";return false;}
                LegacyWholeEngineMoveReference.Move(p,engineB.instanceId,InventoryKind.Storage,SharedInventory.Storage);
                fixture=new OrderedMoveFixture{Profile=p,A=a,B=b,EngineA=engineA,EngineB=engineB,
                    PistonA=KCarPart(p,a,PartCategory.Pistons),PistonB=KEnginePart(p,engineB,PartCategory.Pistons)};
                if(fixture.PistonA==null||fixture.PistonB==null){reason="actual same-model cars lack physical Piston packs";return false;}
                if(withTire)
                {
                    fixture.TireUnit=KBuyActualTireUnit(p,a);
                    if(fixture.TireUnit==null){reason="InventoryUnits.Buy did not acquire a real typed tire unit";return false;}
                }
                return true;
            }
            catch(Exception error){reason=error.GetType().FullName+" | "+error.Message;fixture=null;return false;}
        }

        static bool KCreateNormalizedWithTire(uint seed,out PlayerProfile profile,out LooseEngineElement tire,out string reason)
        {
            profile=null;tire=null;reason=null;
            try
            {
                CartPreviewCharacterizationFixtures.CreateNormalized(out profile,out string carId,seed);
                var car=profile.cars.Find(candidate=>candidate!=null&&candidate.instanceId==carId);
                tire=KBuyActualTireUnit(profile,car);
                if(tire==null)throw new InvalidOperationException("No actual acquired TireCondition unit.");
                return true;
            }
            catch(Exception error){reason=error.GetType().FullName+" | "+error.Message;return false;}
        }

        static LooseEngineElement KBuyActualTireUnit(PlayerProfile p,OwnedCar car)
        {
            var spec=p.catalog.Spec(car.vehicleId);
            var definition=p.catalog.parts.OrderBy(item=>item.id,StringComparer.Ordinal)
                .FirstOrDefault(item=>item!=null&&item.category==PartCategory.Tires&&p.catalog.Fits(spec,item));
            if(definition==null)return null;
            var unit=InventoryUnits.Buy(p,car.instanceId,definition.id,"tire");
            if(unit?.element is not TireCondition tire)throw new InvalidOperationException("Actual tire purchase did not create a TireCondition.");
            // Non-default fixture values make subtype/history preservation observable.
            tire.health=.73f;tire.consumedLife=.27;tire.distanceM=1842.5;tire.runningSeconds=93.25;
            tire.temperatureC=68.5;tire.slipEnergyJ=281.75;tire.lastContactWorkJ=41.125;
            LegacyWholeEngineMoveReference.Move(p,tire.id,InventoryKind.Storage,SharedInventory.Garage);
            if(tire.Validate()!=null)throw new InvalidOperationException("Acquired tire fixture has invalid actual condition data.");
            return unit;
        }

        static bool KCreateHundredPartsFixture(uint seed,out PlayerProfile p,out string[] partIds,out string[] ownerIds,
            out string reason,out int carCount,out int installedPartCount)
        {
            p=null;partIds=Array.Empty<string>();ownerIds=Array.Empty<string>();reason=null;carCount=0;installedPartCount=0;
            CartPreviewCharacterizationFixtures.CreateNormalized(out p,out string firstId,seed);
            var fixtureProfile=p;
            var ids=new List<string>();var idOwners=new List<string>();var seen=new HashSet<string>(StringComparer.Ordinal);
            Action collect=()=>
            {
                var current=fixtureProfile.SelectedCar;if(current==null)return;
                foreach(var binding in current.installed?.slots??new List<SlotBinding>())
                {
                    var part=binding==null?null:fixtureProfile.parts.Find(value=>value!=null&&value.instanceId==binding.partInstanceId);
                    if(part!=null&&seen.Add(part.instanceId)){ids.Add(part.instanceId);idOwners.Add(current.instanceId);}
                }
            };
            collect();
            int attempts=0;
            while(ids.Count<100&&attempts<12)
            {
                attempts++;
                try
                {
                    var car=CartPreviewCharacterizationFixtures.AddSecondOwnedCar(fixtureProfile);
                    if(car==null)break;
                    VehicleCollection.Select(fixtureProfile,car.instanceId);
                    collect();
                }
                catch(Exception error){reason=error.GetType().FullName+" | "+error.Message;break;}
            }
            carCount=p.cars.Count;installedPartCount=ids.Count;
            if(ids.Count<100){if(string.IsNullOrEmpty(reason))reason="actual purchased car stock exhausted before 100 distinct installed assets";return false;}
            partIds=ids.Take(100).ToArray();
            // Owner records follow the request stream's first-affected order.
            ownerIds=idOwners.Take(100).Distinct(StringComparer.Ordinal).ToArray();
            return true;
        }

        static PartInstance KCarPart(PlayerProfile p,OwnedCar car,PartCategory category)
        {
            var binding=car?.installed?.slots?.FirstOrDefault(row=>row!=null&&row.slot==category.ToString());
            return binding==null?null:p.parts.Find(part=>part!=null&&part.instanceId==binding.partInstanceId);
        }

        static PartInstance KEnginePart(PlayerProfile p,EngineAssemblyInstance engine,PartCategory category)
        {
            var binding=engine?.slots?.FirstOrDefault(row=>row!=null&&row.slot==category.ToString());
            return binding==null?null:p.parts.Find(part=>part!=null&&part.instanceId==binding.partInstanceId);
        }

        static string KUnitSnapshotFacts(LooseUnitMoveSnapshot item)
            =>item==null?"<null>":TypedCanonical(new object[]{item.Id,item.DefinitionId,item.StorageId,item.UnitPrice,item.TireUnit,item.Element},new Dictionary<string,string>());

        static string KLooseUnitFacts(LooseEngineElement item)
            =>item==null?"<null>":TypedCanonical(new object[]{item.element?.id,item.definitionId,item.storageId,item.unitPrice,item.tireUnit,item.element?.Copy()},new Dictionary<string,string>());

        static string KProfileDifference(PlayerProfile a,PlayerProfile b)
        {
            if(SourceSnapshot(a)==SourceSnapshot(b))return null;
            if(TypedCanonical(a.engines,new Dictionary<string,string>())!=TypedCanonical(b.engines,new Dictionary<string,string>()))return "engines";
            if(TypedCanonical(a.cars,new Dictionary<string,string>())!=TypedCanonical(b.cars,new Dictionary<string,string>()))return "cars";
            if(TypedCanonical(a.parts,new Dictionary<string,string>())!=TypedCanonical(b.parts,new Dictionary<string,string>()))return "parts";
            if(TypedCanonical(a.looseEngineElements,new Dictionary<string,string>())!=TypedCanonical(b.looseEngineElements,new Dictionary<string,string>()))return "loose-units";
            return "other-profile-fields";
        }

        static void KOutputMutationIsolation(Action<bool,string> check,string name,PlayerProfile source,
            IReadOnlyList<InventoryMoveRequest> requests,OrderedInventoryMoveResult result)
        {
            string outputBefore=KResultFingerprint(result);string sourceBefore=SourceSnapshot(source);
            if(result.ChangedParts!=null&&result.ChangedParts.Length>0&&result.ChangedParts[0]?.condition?.elements?.Length>0)
                result.ChangedParts[0].condition.elements[0].health-=.01f;
            if(result.ChangedEngines!=null&&result.ChangedEngines.Length>0)
            {
                var engine=result.ChangedEngines[0];if(engine.Tune!=null)engine.Tune.shiftRpm+=1;
                if(engine.Fluids!=null)engine.Fluids.oilLitres+=1;
                if(engine.Bindings!=null&&engine.Bindings.Length>0&&engine.Bindings[0]!=null)engine.Bindings[0].slot+="-mutated";
                if(engine.Definitions!=null&&engine.Definitions.Count>0)engine.Definitions[engine.Definitions.Keys.First()]="mutated-definition";
            }
            if(result.ChangedUnits!=null&&result.ChangedUnits.Length>0&&result.ChangedUnits[0]?.Element!=null)
            {
                result.ChangedUnits[0].Element.health-=.01f;
                if(result.ChangedUnits[0].Element is TireCondition tire)tire.consumedLife+=.01;
            }
            foreach(var car in result.CarOwners??Array.Empty<WholePartMoveOwnerSnapshot>())
            {
                if(car.Tune!=null)car.Tune.launchRpm+=1;
                if(car.Fluids!=null)car.Fluids.oilLitres+=1;
                if(car.Bindings!=null&&car.Bindings.Length>0&&car.Bindings[0]!=null)car.Bindings[0].slot+="-mutated";
                if(car.Definitions!=null&&car.Definitions.Count>0)car.Definitions[car.Definitions.Keys.First()]="mutated-definition";
                if(car.Parts!=null&&car.Parts.Length>0&&car.Parts[0]?.condition?.elements?.Length>0)car.Parts[0].condition.elements[0].health-=.01f;
            }
            check(sourceBefore==SourceSnapshot(source),"nested mutations to all output family snapshots cannot change input / "+name);
            var fresh=OrderedInventoryMoveProjection.Evaluate(source,requests,source.catalog);
            check(fresh!=null&&fresh.Success&&KResultFingerprint(fresh)==outputBefore,"nested output mutation cannot change a fresh sequence result / "+name);
        }

        static string KResultFingerprint(OrderedInventoryMoveResult result)
        {
            if(result==null)return "<null>";
            var parts=(result.ChangedParts??Array.Empty<PartInstance>()).Select(item=>TypedCanonical(item,new Dictionary<string,string>())).ToArray();
            var engines=(result.ChangedEngines??Array.Empty<WholeEngineMoveSnapshot>()).Select(item=>
                TypedCanonical(new object[]{WemEngineSnapshot(item),item.Parts,WemDefinitionsCanonical(item.Definitions),item.Materialized,item.BuildErrorType,item.BuildErrorMessage},new Dictionary<string,string>())).ToArray();
            var units=(result.ChangedUnits??Array.Empty<LooseUnitMoveSnapshot>()).Select(KUnitSnapshotFacts).ToArray();
            var cars=(result.CarOwners??Array.Empty<WholePartMoveOwnerSnapshot>()).Select(owner=>
                TypedCanonical(new object[]{owner.Id,owner.VehicleId,owner.EngineAssemblyId,owner.GarageJobId,owner.HasRebuildJob,owner.RebuildActive,
                    owner.Bindings,owner.Tune,owner.Fuel,owner.Fluids,owner.Materialized,owner.BuildErrorType,owner.BuildErrorMessage,owner.Parts,
                    WemDefinitionsCanonical(owner.Definitions),owner.Inspection},new Dictionary<string,string>())).ToArray();
            return TypedCanonical(new object[]{result.Success,result.ErrorType,result.ErrorMessage,result.FailedIndex,parts,engines,units,cars},new Dictionary<string,string>());
        }

        static void KMeasureHundredPartCost(Action<bool,string> check,StringBuilder log,PlayerProfile p,InventoryMoveRequest[] requests)
        {
            const int warmups=5,samples=15;
            for(int i=0;i<warmups;i++)
            {
                var warmed=OrderedInventoryMoveProjection.Evaluate(p,requests,p.catalog);
                if(warmed==null||!warmed.Success){check(false,"100-distinct cost warmup succeeds / "+i);return;}
            }
            var timings=new double[samples];bool allSuccess=true;long before=Stopwatch.GetTimestamp();
            for(int i=0;i<samples;i++)
            {
                long start=Stopwatch.GetTimestamp();var result=OrderedInventoryMoveProjection.Evaluate(p,requests,p.catalog);
                long end=Stopwatch.GetTimestamp();timings[i]=(end-start)*1000.0/Stopwatch.Frequency;
                if(result==null||!result.Success)allSuccess=false;
            }
            long after=Stopwatch.GetTimestamp();
            check(allSuccess,"100-distinct bounded timing samples all complete successfully");
            Array.Sort(timings);double median=timings[samples/2];int p95Index=(int)Math.Ceiling(.95*samples)-1;double p95=timings[p95Index];
            log.AppendLine("COST|workload=100-distinct-owned-part-moves;warmups="+warmups+";samples="+samples+
                ";medianMs="+median.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+
                ";p95Ms="+p95.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+
                ";elapsedWallMs="+((after-before)*1000.0/Stopwatch.Frequency).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+
                ";allocations=NA;fixture/snapshots/diagnostics=outside samples");
            log.AppendLine("LIMIT|Editor managed timing only; no native frame/device acceptance");
        }

        static void KSourceAudit(Action<bool,string> check,StringBuilder log)
        {
            string path=Path.Combine(UnityEngine.Application.dataPath,"DragLab","Scripts","Domain","OrderedInventoryMoveProjection.cs");
            if(!File.Exists(path)){check(false,"ordered projection source available for static forbidden-token audit");return;}
            string source=File.ReadAllText(path,new UTF8Encoding(false));
            string[] forbidden={"JsonUtility","RuntimeDataEncodingProbe","MechanicsOwnership.NewId","RuntimeEntropy.NewGuid","UnityEngine.Random","CloneProfile","PlayerProfile.Copy"};
            var found=forbidden.Where(token=>source.IndexOf(token,StringComparison.Ordinal)>=0).ToArray();
            check(found.Length==0,"ordered projection source excludes JSON/profile-clone/ID/RNG paths / "+string.Join(",",found));
            log.AppendLine("SOURCE_AUDIT|tokens="+forbidden.Length+";matches="+string.Join(",",found));
        }

        static void KOpen(Action<bool,string> check,StringBuilder log,string name,string reason)
        {
            log.AppendLine("OPEN|"+name+"|"+KSafe(reason));
            check(false,"required actual authored fixture available / "+name+" / "+KSafe(reason));
        }

        static string KOutcome(OrderedInventoryMoveResult result,Exception thrown)
            =>thrown!=null?thrown.GetType().FullName+" | "+thrown.Message:result==null?"<null result>":result.Success?"success":result.ErrorType+" | "+result.ErrorMessage;
        static string KError(Exception error)=>error==null?"success":error.GetType().FullName+" | "+error.Message;
        static string KSafe(string value)=>string.IsNullOrEmpty(value)?"<empty>":value.Replace("\r","\\r").Replace("\n","\\n").Replace("|","/");
    }
}
#endif
