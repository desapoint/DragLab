#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Finite Editor-only owner/block graph characterization. This is not a typed candidate.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        static void RunGraphClosureContracts(Trace trace,string subset)
        {
            if(trace==null)throw new ArgumentNullException(nameof(trace));
            bool all=subset=="graph";
            bool roundTripOnly=subset=="graph-roundtrip";
            bool owners=subset=="graph-owners"||all;
            bool blocks=subset=="graph-blocks"||all;
            if(!owners&&!blocks&&!roundTripOnly)throw new ArgumentException("Expected graph, graph-owners, graph-blocks, or graph-roundtrip.",nameof(subset));
            trace.Add("GRAPH_CLOSURE|subset="+subset+";fixture=literal-profile-copy+current-authored-domain;candidate=legacy-only;typed-policy=unapproved");
            if(roundTripOnly)RunGraphStoredEngineDescendantRoundTrip(trace);
            else if(owners)RunGraphOwnerCases(trace);
            if(blocks)RunGraphBlockCases(trace);
            trace.Add("GRAPH_CLOSURE_DISPOSITION|observations-only;legacy-source-differences-stay-red;typed-owner-closure-and-no-heal-policy-open");
        }

        static void RunGraphOwnerCases(Trace trace)
        {
            RunGraphEngineAttachToEmptyTarget(trace);
            RunGraphEngineAttachToOccupiedTarget(trace);
            RunGraphEngineTransferLocks(trace);
            RunGraphStoredEngineDescendantRoundTrip(trace);
            RunGraphInstalledPartTransfer(trace);
            RunGraphOrderedPackPosition(trace);
        }

        static bool GraphCreateSameModelPair(PlayerProfile profile,out OwnedCar source,out OwnedCar target,out string reason)
        {
            source=profile?.SelectedCar;target=null;reason=null;
            if(profile==null||source==null){reason="normalized fixture has no selected source car";return false;}
            string sourceVehicleId=source.vehicleId;
            var offer=profile.vehicleStock?.listings?.Where(item=>item!=null&&!item.used&&item.model==sourceVehicleId&&item.price>0&&item.stock!=null)
                .OrderBy(item=>item.listingId,StringComparer.Ordinal).FirstOrDefault();
            if(offer==null){reason="no current real new-car offer matches source vehicleDefinitionId="+source.vehicleId;return false;}
            try
            {
                string id=VehicleCollection.Buy(profile,profile.catalog,offer);
                target=profile.cars.Find(item=>item.instanceId==id);
                if(target==null||target.instanceId==source.instanceId||target.vehicleId!=source.vehicleId||string.IsNullOrEmpty(target.engineAssemblyId))
                {reason="actual current matching-model purchase did not create a distinct owned car with engine";return false;}
                VehicleCollection.Select(profile,source.instanceId);
                return true;
            }
            catch(Exception error){reason="actual matching-model offer could not be purchased: "+error.GetType().FullName+" | "+error.Message;return false;}
        }

        static void GraphOpen(Trace trace,string name,string reason)
        {
            trace.Add("OPEN|"+name+"|required actual authored graph precondition unavailable: "+GraphSafe(reason));
            trace.Assert(false,"Required real authored graph fixture exists / "+name+" / "+GraphSafe(reason));
        }

        static PartCartLine GraphEngineMove(EngineAssemblyInstance engine,InventoryKind destination,string destinationId)
            =>new PartCartLine{action=CartAction.Move,assetId=engine.instanceId,definitionId=engine.vehicleDefinitionId,
                destination=destination,destinationId=destinationId};

        static List<Tuple<PartCategory,PartInstance>> GraphRequiredFittedCandidates(PlayerProfile profile,OwnedCar car,string replacementBlockId)
        {
            var result=new List<Tuple<PartCategory,PartInstance>>();
            if(profile?.catalog==null||car==null||string.IsNullOrEmpty(replacementBlockId))return result;
            var definitions=MechanicsOwnership.Definitions(profile,car);definitions[PartCategory.CylinderBlock]=replacementBlockId;
            var spec=profile.catalog.Resolve(car.vehicleId,definitions,car.tune).spec;
            var recipe=profile.catalog.Recipe(car.vehicleId);
            foreach(var category in recipe.Keys.Where(category=>(MechanicalCatalog.EngineSlot(category)||category==PartCategory.Controls)&&
                category!=PartCategory.CylinderBlock&&!PartSemantics.Optional(category,spec,definitions,profile.catalog)).OrderBy(category=>category))
            {
                var binding=car.installed?.slots?.FirstOrDefault(item=>item!=null&&item.slot==category.ToString());
                var part=binding==null?null:profile.parts.Find(item=>item!=null&&item.instanceId==binding.partInstanceId);
                var definition=part==null?null:profile.catalog.Find(part.definitionId);
                if(part!=null&&definition!=null&&profile.catalog.Fits(spec,definition))result.Add(Tuple.Create(category,part));
            }
            return result;
        }

        static bool GraphMoveActualCompanionToGarage(PlayerProfile profile,OwnedCar car,Tuple<PartCategory,PartInstance> candidate,out string partId)
        {
            partId=candidate?.Item2?.instanceId;
            if(candidate==null||string.IsNullOrWhiteSpace(partId))return false;
            string movedPartId=partId;
            SharedInventory.Move(profile,movedPartId,InventoryKind.Storage,SharedInventory.Garage);
            var moved=profile.parts.Find(item=>item!=null&&item.instanceId==movedPartId);
            return moved!=null&&moved.location==PartLocation.Inventory&&string.IsNullOrEmpty(moved.ownerId)&&
                !car.installed.slots.Any(binding=>binding!=null&&binding.partInstanceId==movedPartId);
        }

        static void RunGraphEngineAttachToEmptyTarget(Trace trace)
        {
            const string name="graph-engine-attach-compatible-empty-target";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out _,FixtureSeed+301);
            if(!GraphCreateSameModelPair(profile,out var donor,out var target,out string reason)){GraphOpen(trace,name,reason);return;}
            try
            {
                string donorId=donor.instanceId,targetId=target.instanceId;
                var donorEngine=CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(profile,donorId);
                var targetEngine=CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(profile,targetId);
                string selected=profile.SelectedCar?.instanceId;
                var beforeDonorTune=TypedCanonical(donor.tune,new Dictionary<string,string>());
                var beforeDonorFluids=TypedCanonical(donor.fluids,new Dictionary<string,string>());
                var beforeTargetTune=TypedCanonical(target.tune,new Dictionary<string,string>());
                var beforeTargetFluids=TypedCanonical(target.fluids,new Dictionary<string,string>());
                string donorGraphBefore=TypedCanonical(donor,new Dictionary<string,string>());
                string storedEngineBefore=TypedCanonical(targetEngine,new Dictionary<string,string>());
                var expectedTargetTune=target.tune.Copy();AssemblyOperations.EngineAdjustments(expectedTargetTune,donorEngine.tune);
                string expectedTargetTuneFacts=TypedCanonical(expectedTargetTune,new Dictionary<string,string>());
                string expectedTargetFluidFacts=TypedCanonical(donorEngine.fluids,new Dictionary<string,string>());
                var row=GraphEngineMove(donorEngine,InventoryKind.Car,targetId);
                CompareTotal(trace,name,profile,targetId,new[]{row},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                ComparePreview(trace,name,profile,target,new[]{row});
                var output=GraphAuditedPreview(trace,name,profile,target,new[]{row});
                if(output!=null)
                {
                    var outDonor=output.cars.Find(item=>item.instanceId==donorId);
                    var outTarget=output.cars.Find(item=>item.instanceId==targetId);
                    var outEngine=output.engines.Find(item=>item.instanceId==donorEngine.instanceId);
                    var outStored=output.engines.Find(item=>item.instanceId==targetEngine.instanceId);
                    trace.Assert(outDonor!=null&&string.IsNullOrEmpty(outDonor.engineAssemblyId)&&outTarget!=null&&
                        outTarget.engineAssemblyId==donorEngine.instanceId&&outEngine!=null&&outEngine.parentVehicleId==targetId&&
                        outStored!=null&&string.IsNullOrEmpty(outStored.parentVehicleId)&&outStored.storageId==SharedInventory.Storage&&
                        output.SelectedCar?.instanceId==selected&&
                        donorGraphBefore==TypedCanonical(outDonor,new Dictionary<string,string>())&&
                        storedEngineBefore==TypedCanonical(outStored,new Dictionary<string,string>())&&
                        expectedTargetTuneFacts==TypedCanonical(outTarget.tune,new Dictionary<string,string>())&&
                        expectedTargetFluidFacts==TypedCanonical(outTarget.fluids,new Dictionary<string,string>()),
                        "Real compatible engine moves from donor to empty target; donor graph, displaced engine and exact tune/fluid transfer are recorded / "+name);
                    trace.Add("GRAPH_ENGINE_ATTACH|donor="+donorId+";target="+targetId+";incoming="+donorEngine.instanceId+
                        ";storedTargetEngine="+targetEngine.instanceId+";selection="+(output.SelectedCar?.instanceId??"<null>")+";donorTune="+
                        GraphShortHash(beforeDonorTune)+";donorFluids="+GraphShortHash(beforeDonorFluids)+";targetTuneBefore="+
                        GraphShortHash(beforeTargetTune)+";targetFluidsBefore="+GraphShortHash(beforeTargetFluids)+";targetTuneAfter="+
                        (outTarget==null?"<missing>":GraphShortHash(TypedCanonical(outTarget.tune,new Dictionary<string,string>())))+";targetFluidsAfter="+
                        (outTarget==null?"<missing>":GraphShortHash(TypedCanonical(outTarget.fluids,new Dictionary<string,string>()))));
                }
            }
            catch(Exception error){GraphOpen(trace,name,"actual same-model detach/attach path: "+error.GetType().FullName+" | "+error.Message);}
        }

        static void RunGraphEngineAttachToOccupiedTarget(Trace trace)
        {
            const string name="graph-engine-attach-compatible-occupied-target";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out _,FixtureSeed+302);
            if(!GraphCreateSameModelPair(profile,out var donor,out var target,out string reason)){GraphOpen(trace,name,reason);return;}
            try
            {
                var engine=CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(profile,donor.instanceId);
                var row=GraphEngineMove(engine,InventoryKind.Car,target.instanceId);
                string message="Detach the current engine first.";
                CompareTotal(trace,name,profile,target.instanceId,new[]{row},ValuationPolicy.Defaults(),
                    CartPreviewCharacterizationFixtures.FixedNowTicks,typeof(InvalidOperationException).FullName,message);
                ComparePreview(trace,name,profile,target,new[]{row},typeof(InvalidOperationException).FullName,message);
                trace.Add("GRAPH_ENGINE_OCCUPIED|donor="+donor.instanceId+";target="+target.instanceId+
                    ";incoming="+engine.instanceId+";targetEngine="+target.engineAssemblyId+";expectedFirstError="+GraphSafe(message));
            }
            catch(Exception error){GraphOpen(trace,name,"actual occupied-target fixture: "+error.GetType().FullName+" | "+error.Message);}
        }

        static void RunGraphEngineTransferLocks(Trace trace)
        {
            const string donorName="graph-engine-donor-garage-lock";
            const string targetName="graph-engine-target-garage-lock";
            GraphRunEngineLock(trace,donorName,true,FixtureSeed+303);
            GraphRunEngineLock(trace,targetName,false,FixtureSeed+304);
        }

        static void GraphRunEngineLock(Trace trace,string name,bool lockDonor,uint seed)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out _,seed);
            if(!GraphCreateSameModelPair(profile,out var donor,out var target,out string reason)){GraphOpen(trace,name,reason);return;}
            try
            {
                EngineAssemblyInstance engine;
                OwnedCar caller;
                if(lockDonor)
                {
                    CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(profile,target.instanceId);
                    engine=profile.engines.Find(item=>item.instanceId==donor.engineAssemblyId);
                    caller=target;
                }
                else
                {
                    engine=CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(profile,donor.instanceId);
                    caller=donor;
                }
                var locked=lockDonor?donor:target;
                GarageBusiness.Ensure(profile);
                long now=Math.Max(CartPreviewCharacterizationFixtures.FixedNowTicks,profile.business.accruedThroughUtcTicks);
                var offer=GarageBusiness.DefaultPolicy().offers.FirstOrDefault(item=>item.id=="photo-session");
                if(offer==null)throw new InvalidOperationException("actual garage policy has no photo-session offer");
                GarageBusiness.Assign(profile,GarageBusiness.DefaultPolicy(),locked.instanceId,offer.id,now);
                var row=GraphEngineMove(engine,InventoryKind.Car,target.instanceId);
                const string message="Car and installed components reserved by garage job; claim or cancel it first.";
                CompareTotal(trace,name,profile,caller.instanceId,new[]{row},ValuationPolicy.Defaults(),
                    CartPreviewCharacterizationFixtures.FixedNowTicks,typeof(InvalidOperationException).FullName,message);
                ComparePreview(trace,name,profile,caller,new[]{row},typeof(InvalidOperationException).FullName,message);
                var preview=GraphAuditedPreview(trace,name,profile,caller,new[]{row},out string previewError);
                trace.Assert(preview==null&&previewError==typeof(InvalidOperationException).FullName+" | "+message,
                    "Direct Preview reaches the actual locked owner after the unlocked caller passes its own guard / "+name+
                    " caller="+caller.instanceId+" locked="+locked.instanceId+" error="+GraphSafe(previewError));
                trace.Add("GRAPH_ENGINE_LOCK|case="+name+";locked="+locked.instanceId+";side="+(lockDonor?"donor-detach":"target-attach")+
                    ";caller="+caller.instanceId+";callerUnlocked="+string.IsNullOrEmpty(caller.garageJobId)+";engine="+
                    engine.instanceId+";reservation=actual-garage-assign;directPreviewError="+GraphSafe(previewError));
            }
            catch(Exception error){GraphOpen(trace,name,"actual garage lock or engine move precondition: "+error.GetType().FullName+" | "+error.Message);}
        }

        static void RunGraphStoredEngineDescendantRoundTrip(Trace trace)
        {
            const string name="graph-stored-engine-descendant-egress-roundtrip";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,FixtureSeed+305);
            var car=profile.cars.Find(item=>item.instanceId==carId);
            var part=TopologyInstalledParts(profile,car).FirstOrDefault(item=>MechanicalCatalog.EngineSlot(profile.catalog.Find(item.definitionId).category)&&
                profile.catalog.Find(item.definitionId).category!=PartCategory.CylinderBlock);
            if(part==null){GraphOpen(trace,name,"normalized authored car has no actual installed non-block engine descendant");return;}
            try
            {
                var engine=CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(profile,carId);
                string partId=part.instanceId,definition=part.definitionId,engineId=engine.instanceId,slot=part.slot;
                var originalPartFields=FlattenNormalization(part);
                var originalPartTopology=NormalizationReferenceTopology(part);
                var originalConditionFields=FlattenNormalization(part.condition);
                var originalConditionTopology=NormalizationReferenceTopology(part.condition);
                var egress=new PartCartLine{action=CartAction.Move,assetId=partId,definitionId=definition,
                    destination=InventoryKind.Storage,destinationId=SharedInventory.Garage};
                CompareTotal(trace,name+"-egress",profile,carId,new[]{egress},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                ComparePreview(trace,name+"-egress",profile,car,new[]{egress});
                var afterEgress=GraphAuditedPreview(trace,name+"-egress",profile,car,new[]{egress});
                if(afterEgress==null)return;
                var loose=afterEgress.parts.Find(item=>item.instanceId==partId);
                var egressEngine=afterEgress.engines.Find(item=>item.instanceId==engineId);
                var egressConditionFields=loose==null?new SortedDictionary<string,string>(StringComparer.Ordinal):FlattenNormalization(loose.condition);
                var egressConditionTopology=loose==null?new SortedDictionary<string,string>(StringComparer.Ordinal):NormalizationReferenceTopology(loose.condition);
                var egressConditionDiff=NormalizationDiff(originalConditionFields,egressConditionFields);
                var egressConditionTopologyDiff=NormalizationDiff(originalConditionTopology,egressConditionTopology);
                WriteNormalizationDiff(trace,name+"-egress-condition",egressConditionDiff,egressConditionTopologyDiff);
                bool garageIngressState=loose!=null&&loose.instanceId==partId&&loose.location==PartLocation.Inventory&&
                    string.IsNullOrEmpty(loose.ownerId)&&string.IsNullOrEmpty(loose.slot)&&loose.definitionId==definition&&
                    loose.storageId==SharedInventory.Garage;
                trace.Assert(garageIngressState&&egressEngine!=null&&!egressEngine.slots.Any(binding=>binding.partInstanceId==partId),
                    "Stored engine egress retains physical ID/definition in Garage with no owner/slot and removes only its actual descendant binding / "+partId);
                trace.Assert(egressConditionDiff.Count==0&&egressConditionTopologyDiff.Count==0,
                    "Stored engine egress preserves exact condition fields and reference topology / "+partId);
                trace.Add("GRAPH_STORED_DESCENDANT_EGRESS|engine="+engineId+";part="+partId+";definition="+definition+
                    ";location="+(loose==null?"<missing>":loose.location.ToString())+";owner="+(loose==null?"<missing>":GraphSafe(loose.ownerId))+
                    ";slot="+(loose==null?"<missing>":GraphSafe(loose.slot))+";storage="+(loose==null?"<missing>":loose.storageId.ToString())+
                    ";conditionDiffPaths="+egressConditionDiff.Count+";conditionTopologyDiffPaths="+egressConditionTopologyDiff.Count);
                var ingress=new PartCartLine{action=CartAction.Move,assetId=partId,definitionId=definition,
                    destination=InventoryKind.Engine,destinationId=engineId};
                CompareTotal(trace,name+"-ingress",afterEgress,carId,new[]{ingress},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                var egressCar=afterEgress.cars.Find(item=>item.instanceId==carId);
                ComparePreview(trace,name+"-ingress",afterEgress,egressCar,new[]{ingress});
                var roundTrip=GraphAuditedPreview(trace,name+"-ingress",afterEgress,egressCar,new[]{ingress});
                if(roundTrip!=null)
                {
                    var returned=roundTrip.parts.Find(item=>item.instanceId==partId);
                    var returnedEngine=roundTrip.engines.Find(item=>item.instanceId==engineId);
                    var expectedReturnedFields=new SortedDictionary<string,string>(originalPartFields,StringComparer.Ordinal);
                    expectedReturnedFields["$.storageId"]=TypedCanonical(SharedInventory.Garage,new Dictionary<string,string>(),new List<object>(),"storageId");
                    var returnedFields=returned==null?new SortedDictionary<string,string>(StringComparer.Ordinal):FlattenNormalization(returned);
                    var returnedDiff=NormalizationDiff(expectedReturnedFields,returnedFields);
                    var expectedReturnedTopology=new SortedDictionary<string,string>(originalPartTopology,StringComparer.Ordinal);
                    // Non-null primitive strings have no reference-topology entry.
                    expectedReturnedTopology.Remove("$.storageId");
                    var returnedTopologyDiff=NormalizationDiff(expectedReturnedTopology,returned==null?
                        new SortedDictionary<string,string>(StringComparer.Ordinal):NormalizationReferenceTopology(returned));
                    WriteNormalizationDiff(trace,name+"-end-to-end-return",returnedDiff,returnedTopologyDiff);
                    trace.Assert(returned!=null&&returned.instanceId==partId&&returned.location==PartLocation.Assembly&&
                        returned.ownerId==engineId&&returned.slot==slot&&returned.storageId==SharedInventory.Garage&&
                        returnedEngine!=null&&returnedEngine.slots.Any(binding=>binding.partInstanceId==partId)&&
                        returned.definitionId==definition,
                        "Stored engine ingress restores physical ID/definition/owner/slot and retains the explicit Garage storage tag / "+partId);
                    trace.Assert(returnedDiff.Count==0&&returnedTopologyDiff.Count==0,
                        "Stored engine descendant round-trip preserves original typed part fields except storageId=Garage and restores exact physical ID/condition/slot binding / "+partId);
                    trace.Add("GRAPH_STORED_DESCENDANT|engine="+engineId+";part="+partId+";definition="+definition+";slot="+slot+
                        ";egressOwner=Garage;ingressOwner=engine;expectedOnlyStoragePath=$.storageId;expectedStorage="+GraphSafe(expectedReturnedFields["$.storageId"])+
                        ";returnedFieldDiffPaths="+returnedDiff.Count+";returnedTopologyDiffPaths="+returnedTopologyDiff.Count);
                }
            }
            catch(Exception error){GraphOpen(trace,name,"actual stored descendant move path: "+error.GetType().FullName+" | "+error.Message);}
        }

        static void RunGraphInstalledPartTransfer(Trace trace)
        {
            const string name="graph-installed-engine-slot-part-to-other-occupied-car";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out _,FixtureSeed+306);
            if(!GraphCreateSameModelPair(profile,out var donor,out var target,out string reason)){GraphOpen(trace,name,reason);return;}
            var candidate=TopologyInstalledParts(profile,donor).Where(item=>MechanicalCatalog.EngineSlot(profile.catalog.Find(item.definitionId).category)&&
                profile.catalog.Find(item.definitionId).category!=PartCategory.CylinderBlock)
                .Select(item=>new{Part=item,Target=target.installed.slots.FirstOrDefault(binding=>binding.slot==item.slot)})
                .FirstOrDefault(item=>item.Target!=null);
            if(candidate==null){GraphOpen(trace,name,"same-model authored cars have no corresponding occupied non-block engine-slot category");return;}
            try
            {
                var part=candidate.Part;var old=profile.parts.Find(item=>item.instanceId==candidate.Target.partInstanceId);
                if(old==null){GraphOpen(trace,name,"target binding does not resolve to a real physical part");return;}
                string partId=part.instanceId,oldId=old.instanceId,selected=profile.SelectedCar?.instanceId;
                string donorBefore=TypedCanonical(donor,new Dictionary<string,string>());
                var row=new PartCartLine{action=CartAction.Move,assetId=partId,definitionId=part.definitionId,
                    destination=InventoryKind.Car,destinationId=target.instanceId};
                CompareTotal(trace,name,profile,donor.instanceId,new[]{row},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                ComparePreview(trace,name,profile,donor,new[]{row});
                var output=GraphAuditedPreview(trace,name,profile,donor,new[]{row});
                if(output!=null)
                {
                    var outDonor=output.cars.Find(item=>item.instanceId==donor.instanceId);
                    var outTarget=output.cars.Find(item=>item.instanceId==target.instanceId);
                    var incoming=output.parts.Find(item=>item.instanceId==partId);
                    var displaced=output.parts.Find(item=>item.instanceId==oldId);
                    trace.Assert(outDonor!=null&&!outDonor.installed.slots.Any(binding=>binding.partInstanceId==partId)&&
                        outTarget!=null&&outTarget.installed.slots.Any(binding=>binding.partInstanceId==partId)&&
                        incoming!=null&&incoming.location==PartLocation.Assembly&&incoming.ownerId==target.engineAssemblyId&&
                        displaced!=null&&displaced.location==PartLocation.Inventory&&output.SelectedCar?.instanceId==selected,
                        "Installed donor part moves to occupied compatible slot; displaced physical part remains owned and selected car stays unchanged / "+partId);
                    trace.Add("GRAPH_INSTALLED_PART|donor="+donor.instanceId+";target="+target.instanceId+";category="+part.slot+
                        ";incoming="+partId+";displaced="+oldId+";donorGraphBeforeChars="+donorBefore.Length);
                }
            }
            catch(Exception error){GraphOpen(trace,name,"actual installed donor part transfer: "+error.GetType().FullName+" | "+error.Message);}
        }

        static void RunGraphOrderedPackPosition(Trace trace)
        {
            const string name="graph-ordered-pack-change-before-unit-position";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,FixtureSeed+307);
            var car=profile.cars.Find(item=>item.instanceId==carId);
            var current=TopologyInstalledParts(profile,car).FirstOrDefault(item=>item.condition?.elements?.Length>0&&
                InventoryUnits.Roles(profile,car,profile.catalog.Find(item.definitionId)).Length>0);
            if(current==null){GraphOpen(trace,name,"normalized car has no actual installed multi-member element pack");return;}
            var definition=profile.catalog.Find(current.definitionId);
            if(definition==null||definition.category==PartCategory.Tires||definition.category==PartCategory.Wheels||definition.category==PartCategory.Brakes)
            {GraphOpen(trace,name,"selected actual pack is not a position-addressed engine-member family");return;}
            string role=InventoryUnits.Roles(profile,car,definition).FirstOrDefault();
            if(string.IsNullOrEmpty(role)){GraphOpen(trace,name,"actual installed multi-member pack exposes no authored member role");return;}
            var alternate=profile.catalog.parts.Where(item=>item!=null&&item.category==definition.category&&item.id!=definition.id&&
                item.price>=0&&profile.catalog.Fits(profile.catalog.Spec(car.vehicleId),item)&&InventoryUnits.Count(profile,car,item)>1&&
                InventoryUnits.Roles(profile,car,item).Contains(role))
                .OrderBy(item=>item.id,StringComparer.Ordinal).FirstOrDefault();
            if(alternate==null){GraphOpen(trace,name,"catalog has no distinct compatible authored definition for the selected installed pack category "+definition.category);return;}
            if(profile.inventory.market?.parts==null||!profile.inventory.market.parts.Any(stock=>stock!=null&&stock.definitions!=null&&stock.definitions.Contains(definition.id)))
            {GraphOpen(trace,name,"no current actual market row exists for one NewUnits purchase of the fitted definition "+definition.id);return;}
            try
            {
                string spareId=PartOperations.Buy(profile,profile.catalog,carId,alternate.id,false);
                var spare=profile.parts.Find(item=>item.instanceId==spareId);
                if(spare==null||spare.condition?.elements==null||spare.condition.elements.Length==0)
                {GraphOpen(trace,name,"actual spare purchase did not create a multi-member physical pack");return;}
                string oldPosition=InventoryUnits.Positions(profile,car,definition.id,role).FirstOrDefault();
                string newPosition=spare.condition.elements.FirstOrDefault(item=>item.role==role)?.id;
                if(string.IsNullOrEmpty(role)||string.IsNullOrEmpty(oldPosition)||string.IsNullOrEmpty(newPosition))
                {GraphOpen(trace,name,"actual old/new pack has no same-role position for a unit row");return;}
                string oldPackId=current.instanceId;
                var moveOld=new PartCartLine{action=CartAction.Move,assetId=oldPackId,definitionId=definition.id,
                    destination=InventoryKind.Storage,destinationId=SharedInventory.Garage};
                var moveNew=new PartCartLine{action=CartAction.Move,assetId=spareId,definitionId=alternate.id,
                    destination=InventoryKind.Car,destinationId=carId};
                // Total validates selected positions against the currently fitted
                // definition before Apply executes the preceding pack moves.
                string unitDefinition=definition.id;
                if(unitDefinition==null){GraphOpen(trace,name,"no real compatible unit definition has the observed actual member role "+role);return;}
                var unit=new PartCartLine{action=CartAction.NewUnits,definitionId=unitDefinition,quantity=1,install=true,role=role,
                    positions=new List<string>{oldPosition}};
                var afterMoveRows=new[]{moveOld.Copy(),moveNew.Copy(),unit.Copy()};
                var beforeMoveRows=new[]{unit.Copy(),moveOld.Copy(),moveNew.Copy()};
                const string quoteConflict="Choose a complete set or individual replacements for a slot, not both.";
                var oldOutcome=CompareTotal(trace,name+"-move-pack-then-unit",profile,carId,afterMoveRows,
                    ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks,
                    typeof(InvalidOperationException).FullName,quoteConflict);
                const string movedPackError="Individual replacement needs the same installed specification; use a complete kit to change specification.";
                ComparePreview(trace,name+"-move-pack-then-unit",profile,car,afterMoveRows,
                    typeof(InvalidOperationException).FullName,movedPackError);
                var movedFirstOutput=GraphAuditedPreview(trace,name+"-move-pack-then-unit",profile,car,afterMoveRows,out string movedFirstPreviewError);
                trace.Assert(oldOutcome.Threw&&oldOutcome.ExceptionType==typeof(InvalidOperationException).FullName&&oldOutcome.ExceptionMessage==quoteConflict,
                    "Total rejects this unit row with the exact complete-set/unit conflict before Preview / "+name+" / "+GraphSafe(oldOutcome.Text));
                trace.Assert(movedFirstOutput==null&&movedFirstPreviewError==typeof(InvalidOperationException).FullName+" | "+movedPackError,
                    "Move-first direct Preview reports the exact actual Apply failure / "+name+" / "+GraphSafe(movedFirstPreviewError));
                var newOutcome=CompareTotal(trace,name+"-unit-then-move-pack",profile,carId,beforeMoveRows,
                    ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks,
                    typeof(InvalidOperationException).FullName,quoteConflict);
                trace.Assert(newOutcome.Threw&&newOutcome.ExceptionType==typeof(InvalidOperationException).FullName&&newOutcome.ExceptionMessage==quoteConflict,
                    "Total rejects reversed unit row with the exact complete-set/unit conflict / "+name+" / "+GraphSafe(newOutcome.Text));
                ComparePreview(trace,name+"-unit-then-move-pack",profile,car,beforeMoveRows);
                var unitFirstOutput=GraphAuditedPreview(trace,name+"-unit-then-move-pack",profile,car,beforeMoveRows,out string unitFirstPreviewError);
                trace.Assert(unitFirstPreviewError==null&&unitFirstOutput!=null,
                    "Unit-first direct Preview completes before later pack moves / "+name+" / "+GraphSafe(unitFirstPreviewError??"output captured"));
                if(unitFirstOutput!=null)
                {
                    var storedOld=unitFirstOutput.parts.Find(item=>item.instanceId==oldPackId);
                    var fittedNew=unitFirstOutput.parts.Find(item=>item.instanceId==spareId);
                    bool priorPositionReleased=unitFirstOutput.looseEngineElements.Any(item=>item?.element?.id==oldPosition);
                    bool oldPackWasReplaced=storedOld?.condition?.elements!=null&&storedOld.condition.elements.Any(item=>item!=null&&
                        item.role==role&&item.id!=oldPosition);
                    bool newPackFitted=fittedNew!=null&&fittedNew.location==PartLocation.Assembly&&fittedNew.ownerId==car.engineAssemblyId&&
                        unitFirstOutput.cars.Find(item=>item.instanceId==carId)?.installed.slots.Any(binding=>binding.partInstanceId==spareId)==true;
                    bool alternatePositionExact=fittedNew?.condition?.elements?.Any(item=>item!=null&&item.id==newPosition)==true;
                    trace.Assert(storedOld!=null&&storedOld.location==PartLocation.Inventory&&priorPositionReleased&&oldPackWasReplaced&&
                        newPackFitted&&alternatePositionExact,
                        "Unit first replaces the old fitted pack, then later moves retain the repaired old pack in storage and fit the alternate pack / "+name);
                    trace.Add("GRAPH_PACK_UNIT_FIRST_OUTPUT|oldPack="+oldPackId+";oldPackLocation="+storedOld?.location+
                        ";oldPositionReleased="+priorPositionReleased+";newPack="+spareId+";newPackFitted="+newPackFitted+
                        ";alternatePositionExact="+alternatePositionExact+";alternatePosition="+newPosition+
                        ";replacementMember="+(oldPackWasReplaced?storedOld.condition.elements.First(item=>item!=null&&item.role==role&&item.id!=oldPosition).id:"<missing>"));
                }
                trace.Add("GRAPH_ORDERED_PACK|car="+carId+";category="+definition.category+";oldPack="+oldPackId+
                    ";newPack="+spareId+";oldDefinition="+definition.id+";newDefinition="+alternate.id+";role="+role+
                    ";oldPosition="+oldPosition+";newPackPosition="+newPosition+";moveFirstTotal="+GraphSafe(oldOutcome.Text)+
                    ";unitFirstTotal="+GraphSafe(newOutcome.Text)+";quote=both-rejected;directPreviewBypassesQuote=yes;moveFirstOutput="+
                    (movedFirstOutput==null?"throws-as-pinned":"captured")+";unitFirstOutput="+(unitFirstOutput==null?"unavailable":"captured"));
            }
            catch(Exception error){GraphOpen(trace,name,"actual pack purchase/position sequence: "+error.GetType().FullName+" | "+error.Message);}
        }

        static void RunGraphBlockCases(Trace trace)
        {
            RunGraphWrongPreparationOwner(trace);
            RunGraphMultiTargetBlockFirstError(trace);
        }

        static void RunGraphWrongPreparationOwner(Trace trace)
        {
            const string name="graph-block-wrong-preparation-owner";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,FixtureSeed+308);
            var car=profile.cars.Find(item=>item.instanceId==carId);
            var current=MechanicsOwnership.Definitions(profile,car).TryGetValue(PartCategory.CylinderBlock,out string currentId)?currentId:null;
            var replacement=profile.catalog.parts.Where(item=>item!=null&&item.category==PartCategory.CylinderBlock&&item.id!=currentId&&
                item.price>=0&&profile.catalog.Fits(profile.catalog.Spec(car.vehicleId),item)&&
                profile.inventory.market.parts.Any(stock=>stock!=null&&stock.definitions.Contains(item.id)))
                .OrderBy(item=>item.id,StringComparer.Ordinal).FirstOrDefault();
            if(replacement==null){GraphOpen(trace,name,"no actual distinct compatible cylinder block with current market supply");return;}
            var required=GraphRequiredFittedCandidates(profile,car,replacement.id).FirstOrDefault();
            if(required==null){GraphOpen(trace,name,"replacement resolved spec has no required category with a real compatible fitted part to stage into Garage");return;}
            if(!GraphMoveActualCompanionToGarage(profile,car,required,out string companionId))
            {GraphOpen(trace,name,"actual required fitted companion did not move to Garage with its physical ID preserved: "+required.Item1);return;}
            var core=new PartCartLine{action=CartAction.NewSet,definitionId=replacement.id,quantity=1,install=true};
            try
            {
                bool correctStable;string correctException;
                var correct=GraphAuditedBlockBuild(trace,name+"-correct-control",profile,carId,core,
                    Array.Empty<PartCartLine>(),out correctStable,out _,out correctException);
                if(correct==null||!correct.Ready||correct.Lines==null||correct.Lines.Count<2)
                {GraphOpen(trace,name,"actual authored Reuse plan is not ready or has no real supporting row; exception="+correctException+"; result="+correct?.Error);return;}
                var marker=correct.Lines.FirstOrDefault(item=>item!=null&&item.preparationOwner==correct.Owner&&
                    item.action==CartAction.Move&&item.assetId==companionId&&item.destination==InventoryKind.Car&&item.destinationId==carId);
                if(marker==null){GraphOpen(trace,name,"ready real plan did not reuse the actual Garage companion as a marked Move row");return;}
                var wrongRows=correct.Lines.Select(item=>item?.Copy()).ToArray();
                var wrong=wrongRows.First(item=>item!=null&&item.action==marker.action&&item.assetId==marker.assetId&&item.definitionId==marker.definitionId&&
                    item.preparationOwner==correct.Owner);
                wrong.preparationOwner="block/wrong-owner/audit";
                bool wrongStable;string wrongException;
                var observed=GraphAuditedBlockBuild(trace,name+"-wrong-owner",profile,carId,core,wrongRows,
                    out wrongStable,out var identity,out wrongException);
                if(observed==null){GraphOpen(trace,name,"wrong-owner Build threw: "+wrongException);return;}
                bool retained=observed.Lines.Any(item=>item!=null&&item.preparationOwner=="block/wrong-owner/audit");
                var output=GraphAuditedPreview(trace,name+"-direct-resolve",profile,car,observed.Lines);
                if(output==null){GraphOpen(trace,name,"mismatched-owner real plan could not produce an isolated preview for independent Resolve");return;}
                var resolved=MechanicsOwnership.Resolve(output,output.cars.Find(item=>item.instanceId==carId),output.catalog);
                string directError=resolved.Drivable?null:"Required components / faults: "+string.Join("; ",resolved.errors);
                var total=CompareTotal(trace,name,profile,carId,observed.Lines,ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                ComparePreview(trace,name,profile,car,observed.Lines);
                trace.Assert(identity.Closed&&!identity.Overflowed,"Wrong-owner Build identity observer closes / "+name+" count="+identity.Count);
                trace.Assert(retained&&observed.Ready==resolved.Drivable&&observed.Error==directError,
                    "Mismatched actual companion marker is retained and Build Ready/Error exactly agrees with independent Resolve / "+name+
                    " retained="+retained+" ready="+observed.Ready+" error="+GraphSafe(observed.Error)+" resolve="+GraphSafe(directError));
                trace.Add("GRAPH_BLOCK_WRONG_OWNER|planOwner="+observed.Owner+";injectedRowOwner="+wrong.preparationOwner+
                    ";retained="+retained+";ready="+observed.Ready+";buildError="+GraphSafe(observed.Error)+
                    ";resolveError="+GraphSafe(directError)+";requiredCategory="+required.Item1+";physicalPart="+companionId+
                    ";total="+GraphSafe(total.Text)+";identityCount="+identity.Count+
                    ";identityOverflow="+identity.Overflowed+";identity="+GraphSafe(IdentityTrace(identity))+";auditStable="+wrongStable+
                    ";exception="+GraphSafe(wrongException));
            }
            catch(Exception error){GraphOpen(trace,name,"actual block recipe/build: "+error.GetType().FullName+" | "+error.Message);}
        }

        static void RunGraphMultiTargetBlockFirstError(Trace trace)
        {
            const string name="graph-block-multitarget-first-error-order";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out _,FixtureSeed+309);
            if(!GraphCreateSameModelPair(profile,out var firstTarget,out var secondTarget,out string reason)){GraphOpen(trace,name,reason);return;}
            try
            {
                var sourceCars=new[]{firstTarget,secondTarget};
                var oldBlocks=sourceCars.Select(target=>MechanicsOwnership.Definitions(profile,target)
                    .TryGetValue(PartCategory.CylinderBlock,out string value)?value:null).ToArray();
                var block=profile.catalog.parts.Where(item=>item!=null&&item.category==PartCategory.CylinderBlock&&
                    item.id!=oldBlocks[0]&&item.id!=oldBlocks[1]&&item.price>=0&&
                    sourceCars.All(target=>profile.catalog.Fits(profile.catalog.Spec(target.vehicleId),item))&&
                    profile.inventory.market.parts.Any(stock=>stock!=null&&stock.definitions.Contains(item.id)))
                    .OrderBy(item=>item.id,StringComparer.Ordinal).FirstOrDefault();
                if(block==null){GraphOpen(trace,name,"no one actual market block definition is compatible as a replacement on both real target cars");return;}
                var firstCandidates=GraphRequiredFittedCandidates(profile,firstTarget,block.id);
                var secondCandidates=GraphRequiredFittedCandidates(profile,secondTarget,block.id);
                var firstCompanion=firstCandidates.FirstOrDefault(first=>secondCandidates.Any(second=>second.Item1!=first.Item1));
                if(firstCompanion==null){GraphOpen(trace,name,"replacement resolved spec has no pair of distinct required categories backed by real compatible fitted parts on both target cars");return;}
                var secondCompanion=secondCandidates.FirstOrDefault(second=>second.Item1!=firstCompanion.Item1);
                if(secondCompanion==null){GraphOpen(trace,name,"second target has no distinct required real fitted category after choosing "+firstCompanion.Item1);return;}
                string firstCompanionId,secondCompanionId;
                if(!GraphMoveActualCompanionToGarage(profile,firstTarget,firstCompanion,out firstCompanionId))
                {GraphOpen(trace,name,"actual first-target companion could not be staged in Garage: "+firstCompanion.Item1);return;}
                if(!GraphMoveActualCompanionToGarage(profile,secondTarget,secondCompanion,out secondCompanionId))
                {GraphOpen(trace,name,"actual second-target companion could not be staged in Garage: "+secondCompanion.Item1);return;}
                string[] physicalBlocks;
                try{physicalBlocks=sourceCars.Select(target=>PartOperations.Buy(profile,profile.catalog,target.instanceId,block.id,false)).ToArray();}
                catch(Exception error){GraphOpen(trace,name,"actual market could not supply two physical block instances: "+error.GetType().FullName+" | "+error.Message);return;}
                if(physicalBlocks.Length!=2||physicalBlocks[0]==physicalBlocks[1])
                {GraphOpen(trace,name,"two actual distinct block physical IDs were not returned");return;}
                trace.Add("GRAPH_BLOCK_STAGED_COMPANIONS|block="+block.id+";target1="+firstTarget.instanceId+";category1="+
                    firstCompanion.Item1+";part1="+firstCompanionId+";target2="+secondTarget.instanceId+";category2="+
                    secondCompanion.Item1+";part2="+secondCompanionId+";blockParts="+string.Join(",",physicalBlocks));
                var plans=new List<Tuple<string,List<PartCartLine>,string>>();
                for(int targetIndex=0;targetIndex<sourceCars.Length;targetIndex++)
                {
                    var target=sourceCars[targetIndex];
                    var expectedCompanion=targetIndex==0?firstCompanionId:secondCompanionId;
                    var expectedCategory=targetIndex==0?firstCompanion.Item1:secondCompanion.Item1;
                    var core=new PartCartLine{action=CartAction.Move,assetId=physicalBlocks[targetIndex],definitionId=block.id,quantity=1,
                        destination=InventoryKind.Car,destinationId=target.instanceId};
                    bool planStable;string planException;
                    var plan=GraphAuditedBlockBuild(trace,name+"-control-"+target.instanceId,profile,target.instanceId,core,
                        Array.Empty<PartCartLine>(),out planStable,out _,out planException);
                    if(plan==null||!plan.Ready||plan.Lines==null||plan.Lines.Count<2)
                    {GraphOpen(trace,name,"actual Reuse plan unavailable for target "+target.instanceId+"; exception="+planException+"; error="+plan?.Error);return;}
                    if(!plan.Lines.Any(item=>item!=null&&item.preparationOwner==plan.Owner&&item.action==CartAction.Move&&
                        item.assetId==expectedCompanion&&profile.catalog.Find(item.definitionId)?.category==expectedCategory&&
                        item.destination==InventoryKind.Car&&item.destinationId==target.instanceId))
                    {GraphOpen(trace,name,"actual Reuse plan did not create the requested target-scoped Move companion "+expectedCategory+"/"+expectedCompanion);return;}
                    if(plan.Lines.Any(item=>item!=null&&item.preparationOwner==plan.Owner&&item.action==CartAction.NewSet&&item.install))
                    {GraphOpen(trace,name,"target Reuse plan requires a NewSet companion without a per-row target scope");return;}
                    plans.Add(Tuple.Create(target.instanceId,plan.Lines.Select(item=>item?.Copy()).ToList(),plan.Owner));
                }
                // Remove different real prepared companion categories from the two valid plans.
                // This creates distinguishable Resolve errors without inventing assets or IDs.
                var incomplete=new List<Tuple<string,List<PartCartLine>,string,string>>();
                var missingCategories=new HashSet<PartCategory>();
                foreach(var plan in plans)
                {
                    var rows=plan.Item2;
                    int companion=-1;
                    var candidateIndexes=rows.Select((item,index)=>new{Item=item,Index=index})
                        .Where(pair=>pair.Item!=null&&pair.Item.preparationOwner==plan.Item3&&!pair.Item.preparedBlock)
                        .Select(pair=>new{pair.Index,Category=profile.catalog.Find(pair.Item.definitionId)?.category??PartCategory.CylinderBlock})
                        .Where(pair=>!missingCategories.Contains(pair.Category)).ToArray();
                    if(candidateIndexes.Length>0)companion=candidateIndexes[0].Index;
                    if(companion<0){GraphOpen(trace,name,"actual plan has no removable companion row for target "+plan.Item1);return;}
                    PartCategory removedCategory=profile.catalog.Find(rows[companion].definitionId)?.category??PartCategory.CylinderBlock;
                    if(!missingCategories.Add(removedCategory)){GraphOpen(trace,name,"actual plans lack distinct removable component categories for distinguishable target errors");return;}
                    rows.RemoveAt(companion);
                    var car=profile.cars.Find(item=>item.instanceId==plan.Item1);
                    var direct=GraphAuditedPreview(trace,name+"-resolve-"+plan.Item1,profile,car,rows);
                    if(direct==null){GraphOpen(trace,name,"single-target actual incomplete plan failed to produce independent Resolve graph: "+plan.Item1);return;}
                    var resolved=MechanicsOwnership.Resolve(direct,direct.cars.Find(item=>item.instanceId==plan.Item1),direct.catalog);
                    string error=!resolved.Drivable?"Block preparation is incomplete: "+string.Join("; ",resolved.errors):null;
                    if(string.IsNullOrEmpty(error)){GraphOpen(trace,name,"removing actual "+removedCategory+" companion did not make target incomplete: "+plan.Item1);return;}
                    incomplete.Add(Tuple.Create(plan.Item1,rows,plan.Item3,error));
                    trace.Add("GRAPH_BLOCK_TARGET_INVALID|target="+plan.Item1+";planOwner="+plan.Item3+";missingActualRowIndex="+companion+
                        ";missingCategory="+removedCategory+";resolveErrors="+GraphSafe(string.Join(";",resolved.errors)));
                }
                var allRows=incomplete[0].Item2.Concat(incomplete[1].Item2).ToArray();
                var duplicateOffer=allRows.Where(item=>item!=null&&(item.action==CartAction.NewSet||item.action==CartAction.NewUnits))
                    .GroupBy(item=>item.action+"/"+item.definitionId+"/"+(item.role??""),StringComparer.Ordinal).FirstOrDefault(group=>group.Count()>1);
                var duplicateAsset=allRows.Where(item=>item?.action==CartAction.Move||item?.action==CartAction.BuyUsed||item?.action==CartAction.SellPart||item?.action==CartAction.SellUnit)
                    .GroupBy(item=>item.assetId,StringComparer.Ordinal).FirstOrDefault(group=>group.Count()>1);
                if(duplicateOffer!=null||duplicateAsset!=null)
                {GraphOpen(trace,name,"actual plans collide before target Resolve (duplicate offer="+(duplicateOffer?.Key??"none")+"; duplicate asset="+(duplicateAsset?.Key??"none")+")");return;}
                var ordered=incomplete[0].Item2.Concat(incomplete[1].Item2).ToArray();
                var reversed=incomplete[1].Item2.Concat(incomplete[0].Item2).ToArray();
                string firstCar=incomplete[0].Item1,secondCar=incomplete[1].Item1;
                var first=CompareTotal(trace,name+"-first-target",profile,firstCar,ordered,ValuationPolicy.Defaults(),
                    CartPreviewCharacterizationFixtures.FixedNowTicks,typeof(InvalidOperationException).FullName,incomplete[0].Item4);
                var reverse=CompareTotal(trace,name+"-reverse-target",profile,firstCar,reversed,ValuationPolicy.Defaults(),
                    CartPreviewCharacterizationFixtures.FixedNowTicks,typeof(InvalidOperationException).FullName,incomplete[1].Item4);
                ComparePreview(trace,name+"-first-target",profile,profile.cars.Find(item=>item.instanceId==firstCar),ordered);
                ComparePreview(trace,name+"-reverse-target",profile,profile.cars.Find(item=>item.instanceId==firstCar),reversed);
                trace.Assert(first.Threw&&reverse.Threw&&first.ExceptionType==typeof(InvalidOperationException).FullName&&
                    reverse.ExceptionType==typeof(InvalidOperationException).FullName&&first.ExceptionMessage==incomplete[0].Item4&&
                    reverse.ExceptionMessage==incomplete[1].Item4&&incomplete[0].Item4!=incomplete[1].Item4,
                    "Multi-target Total reports exact independently resolved first target error and reverses precedence with rows / "+name+
                    " firstTarget="+firstCar+" expected="+GraphSafe(incomplete[0].Item4)+" actual="+GraphSafe(first.Text)+
                    " reversedFirst="+secondCar+" expected="+GraphSafe(incomplete[1].Item4)+" actual="+GraphSafe(reverse.Text));
                trace.Add("GRAPH_BLOCK_MULTITARGET|targets="+firstCar+","+secondCar+";first="+GraphSafe(first.Text)+
                    ";reverse="+GraphSafe(reverse.Text)+";resolutionOrderFromRows=yes;typedPolicy=open");
            }
            catch(Exception error){GraphOpen(trace,name,"actual two-target block plan construction: "+error.GetType().FullName+" | "+error.Message);}
        }

        static PlayerProfile GraphAuditedPreview(Trace trace,string name,PlayerProfile source,OwnedCar car,IEnumerable<PartCartLine> rows)
            =>GraphAuditedPreview(trace,name,source,car,rows,out _);

        static BlockPreparationPlan GraphAuditedBlockBuild(Trace trace,string name,PlayerProfile source,string carId,
            PartCartLine core,IEnumerable<PartCartLine> rows,out bool stable,out RuntimeEntropy.IdentityObservation identity,out string exceptionText)
        {
            var sourceRows=rows?.ToArray()??Array.Empty<PartCartLine>();var buildRows=sourceRows.Select(item=>item?.Copy()).ToArray();
            var buildCore=core?.Copy();var sourceBefore=SourceSnapshot(source);var sourceFields=FlattenNormalization(source);
            var sourceRefs=NormalizationReferences(source);var sourceCatalog=CatalogSnapshot(source);
            var sourceRowFields=FlattenNormalization(sourceRows);var sourceRowRefs=NormalizationReferences(sourceRows);
            var input=CartPreviewLiteralTestClone.Copy(source);var inputBefore=SourceSnapshot(input);var inputFields=FlattenNormalization(input);
            var inputRefs=NormalizationReferences(input);var inputCatalog=CatalogSnapshot(input);
            var inputRowFields=FlattenNormalization(buildRows);var inputRowRefs=NormalizationReferences(buildRows);
            var inputCoreFields=FlattenNormalization(buildCore);var inputCoreRefs=NormalizationReferences(buildCore);
            var random=UnityEngine.Random.state;BlockPreparationPlan plan=null;Exception error=null;
            identity=RuntimeEntropy.BeginIdentityObservation();
            try{plan=BlockPreparation.Build(input,carId,buildCore,buildRows,BlockPreparationMode.Reuse,null,ValuationPolicy.Defaults());}
            catch(Exception exception){error=exception;}
            finally{identity.Dispose();}
            exceptionText=error==null?null:error.GetType().FullName+" | "+error.Message;
            var inputFieldsAfter=FlattenNormalization(input);var inputRefsAfter=NormalizationReferences(input);
            var sourceRefsAfter=NormalizationReferences(source);var sourceFieldsAfter=FlattenNormalization(source);
            var inputRowDiff=NormalizationDiff(inputRowFields,FlattenNormalization(buildRows));
            var inputRowRefDiff=NormalizationReferenceDiff(inputRowRefs,NormalizationReferences(buildRows));
            var sourceRowDiff=NormalizationDiff(sourceRowFields,FlattenNormalization(sourceRows));
            var sourceRowRefDiff=NormalizationReferenceDiff(sourceRowRefs,NormalizationReferences(sourceRows));
            var coreDiff=NormalizationDiff(inputCoreFields,FlattenNormalization(buildCore));
            var coreRefDiff=NormalizationReferenceDiff(inputCoreRefs,NormalizationReferences(buildCore));
            var inputDiff=NormalizationDiff(inputFields,inputFieldsAfter);
            var inputRefDiff=NormalizationReferenceDiff(inputRefs,inputRefsAfter);
            var sourceDiff=NormalizationDiff(sourceFields,sourceFieldsAfter);
            var sourceRefDiff=NormalizationReferenceDiff(sourceRefs,sourceRefsAfter);
            bool catalogUnchanged=sourceCatalog==CatalogSnapshot(source)&&inputCatalog==CatalogSnapshot(input)&&
                ReferenceEquals(input.catalog,source.catalog);
            bool randomChanged=!random.Equals(UnityEngine.Random.state);
            bool inputUnchanged=inputDiff.Count==0&&inputRefDiff.Count==0&&inputBefore==SourceSnapshot(input)&&catalogUnchanged;
            bool sourceUnchanged=sourceDiff.Count==0&&sourceRefDiff.Count==0&&sourceBefore==SourceSnapshot(source);
            bool rowsUnchanged=inputRowDiff.Count==0&&inputRowRefDiff.Count==0&&sourceRowDiff.Count==0&&sourceRowRefDiff.Count==0&&
                coreDiff.Count==0&&coreRefDiff.Count==0;
            stable=inputUnchanged&&sourceUnchanged&&rowsUnchanged&&!randomChanged&&identity.Closed&&!identity.Overflowed;
            if(!inputUnchanged)WriteNormalizationDiff(trace,"graph-block-"+name+"-input",inputDiff,inputRefDiff);
            if(!sourceUnchanged)WriteNormalizationDiff(trace,"graph-block-"+name+"-source",sourceDiff,sourceRefDiff);
            if(!rowsUnchanged)WriteNormalizationDiff(trace,"graph-block-"+name+"-rows",inputRowDiff.Concat(sourceRowDiff).Concat(coreDiff).ToList(),
                inputRowRefDiff.Concat(sourceRowRefDiff).Concat(coreRefDiff).ToList());
            UnityEngine.Random.state=random;
            trace.Assert(inputUnchanged,"Block Build isolated input value/reference/catalog graph is exact / "+name+" fields="+inputDiff.Count+" refs="+inputRefDiff.Count);
            trace.Assert(sourceUnchanged,"Block Build caller value/reference/catalog graph is exact / "+name+" fields="+sourceDiff.Count+" refs="+sourceRefDiff.Count);
            trace.Assert(rowsUnchanged,"Block Build preserves ordered row/core values and references / "+name+
                " rowPaths="+(inputRowDiff.Count+sourceRowDiff.Count)+" corePaths="+coreDiff.Count);
            trace.Assert(!randomChanged,"Block Build preserves Unity random state / "+name);
            trace.Assert(identity.Closed&&!identity.Overflowed,"Block Build identity observer closes / "+name+" count="+identity.Count);
            trace.Add("GRAPH_BLOCK_BUILD_AUDIT|"+name+"|inputPaths="+inputDiff.Count+";inputRefs="+inputRefDiff.Count+
                ";sourcePaths="+sourceDiff.Count+";sourceRefs="+sourceRefDiff.Count+";rowPaths="+inputRowDiff.Count+
                ";rowRefs="+inputRowRefDiff.Count+";corePaths="+coreDiff.Count+";catalogUnchanged="+catalogUnchanged+
                ";rngChanged="+randomChanged+";identityCount="+identity.Count+";identity="+GraphSafe(IdentityTrace(identity))+
                ";ready="+(plan?.Ready.ToString()??"<none>")+";error="+GraphSafe(plan?.Error)+";exception="+GraphSafe(exceptionText));
            return plan;
        }

        static PlayerProfile GraphAuditedPreview(Trace trace,string name,PlayerProfile source,OwnedCar car,IEnumerable<PartCartLine> rows,out string exceptionText)
        {
            var sourceRows=rows.ToArray();var previewRows=sourceRows.Select(item=>item?.Copy()).ToArray();
            var sourceBefore=SourceSnapshot(source);var sourceFields=FlattenNormalization(source);var sourceRefs=NormalizationReferences(source);
            var sourceCatalog=CatalogSnapshot(source);var sourceRowFields=FlattenNormalization(sourceRows);var rowRefs=NormalizationReferences(sourceRows);
            var previewRowsBefore=FlattenNormalization(previewRows);var previewRowRefs=NormalizationReferences(previewRows);
            var random=UnityEngine.Random.state;var input=CartPreviewLiteralTestClone.Copy(source);
            var inputBefore=SourceSnapshot(input);var inputFields=FlattenNormalization(input);var inputRefs=NormalizationReferences(input);var inputCatalog=CatalogSnapshot(input);
            var sharedCatalog=source.catalog;
            var inputCar=input.cars.Find(item=>item.instanceId==car.instanceId);
            trace.Assert(inputBefore==sourceBefore&&inputCatalog==sourceCatalog&&ReferenceEquals(input.catalog,sharedCatalog),
                "Graph preview begins from exact literal profile and explicitly shared authored catalogue / "+name);
            PlayerProfile output=null;Exception error=null;var identity=RuntimeEntropy.BeginIdentityObservation();
            try{output=PartCart.PreviewProfile(input,inputCar,previewRows);}catch(Exception exception){error=exception;}
            finally{identity.Dispose();}
            exceptionText=error==null?null:error.GetType().FullName+" | "+error.Message;
            var inputAfter=SourceSnapshot(input);var inputRefsAfter=NormalizationReferences(input);var inputCatalogAfter=CatalogSnapshot(input);
            bool sharedCatalogUnchanged=ReferenceEquals(input.catalog,sharedCatalog)&&ReferenceEquals(source.catalog,sharedCatalog);
            var sourceAfter=SourceSnapshot(source);var sourceRefsAfter=NormalizationReferences(source);
            var rowDiff=NormalizationDiff(previewRowsBefore,FlattenNormalization(previewRows));
            var rowRefDiff=NormalizationReferenceDiff(previewRowRefs,NormalizationReferences(previewRows));
            var sourceRowDiff=NormalizationDiff(sourceRowFields,FlattenNormalization(sourceRows));
            var sourceRowRefDiff=NormalizationReferenceDiff(rowRefs,NormalizationReferences(sourceRows));
            var inputPaths=NormalizationDiff(inputFields,FlattenNormalization(input));
            var inputReferenceDiff=NormalizationReferenceDiff(inputRefs,inputRefsAfter);
            var sourceReferenceDiff=NormalizationReferenceDiff(sourceRefs,sourceRefsAfter);
            bool inputUnchanged=inputBefore==inputAfter&&inputCatalog==inputCatalogAfter&&sharedCatalogUnchanged&&inputPaths.Count==0&&inputReferenceDiff.Count==0;
            bool sourceUnchanged=sourceBefore==sourceAfter&&sourceReferenceDiff.Count==0;
            bool rowsUnchanged=rowDiff.Count==0&&rowRefDiff.Count==0&&sourceRowDiff.Count==0&&sourceRowRefDiff.Count==0;
            bool randomChanged=!random.Equals(UnityEngine.Random.state);
            if(!inputUnchanged)WriteNormalizationDiff(trace,"graph-"+name+"-input",inputPaths,inputReferenceDiff);
            if(!sourceUnchanged)WriteNormalizationDiff(trace,"graph-"+name+"-source",NormalizationDiff(sourceFields,FlattenNormalization(source)),sourceReferenceDiff);
            if(!rowsUnchanged)WriteNormalizationDiff(trace,"graph-"+name+"-rows",rowDiff,rowRefDiff);
            trace.Assert(inputUnchanged,"Graph preview input values/references/catalog remain exact / "+name+" fields="+inputPaths.Count+" refs="+inputReferenceDiff.Count);
            trace.Assert(sourceUnchanged,"Graph preview caller values/references/catalog remain exact / "+name+" refs="+sourceReferenceDiff.Count);
            trace.Assert(rowsUnchanged,"Graph preview preserves ordered row values/references / "+name+" fields="+rowDiff.Count+" refs="+rowRefDiff.Count);
            trace.Assert(!randomChanged,"Graph preview preserves Unity random state / "+name);
            trace.Assert(identity.Closed&&!identity.Overflowed,"Graph preview identity observer closes / "+name+" count="+identity.Count);
            UnityEngine.Random.state=random;
            trace.Add("GRAPH_PREVIEW|"+name+"|exception="+GraphSafe(error==null?"none":error.GetType().FullName+" | "+error.Message)+
                ";inputPaths="+inputPaths.Count+";inputRefs="+inputReferenceDiff.Count+";sourceRefs="+sourceReferenceDiff.Count+
                ";sharedCatalogRefUnchanged="+sharedCatalogUnchanged+
                ";rowPaths="+rowDiff.Count+";rowRefs="+rowRefDiff.Count+";rngChanged="+randomChanged+
                ";identityCount="+identity.Count+";identityOverflow="+identity.Overflowed+";identity="+GraphSafe(IdentityTrace(identity))+
                ";output="+(output==null?"none":"captured"));
            if(output!=null)
            {
                var fields=NormalizationDiff(FlattenNormalization(source),FlattenNormalization(output));
                var references=NormalizationReferenceDiff(NormalizationReferences(source),NormalizationReferences(output));
                WriteNormalizationDiff(trace,"graph-"+name+"-output",fields,references);
                trace.Add("GRAPH_OUTPUT|"+name+"|fieldPaths="+fields.Count+";referencePathChanges="+references.Count+
                    ";selected="+(output.SelectedCar?.instanceId??"<null>")+";cars="+(output.cars?.Count??-1)+";engines="+(output.engines?.Count??-1)+";parts="+(output.parts?.Count??-1));
            }
            return output;
        }

        static string GraphShortHash(string value)
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value??""))).Replace("-","").Substring(0,16).ToLowerInvariant();
        }

        static string GraphSafe(string value)
        {
            if(value==null)return "<null>";
            value=value.Replace("\r","\\r").Replace("\n","\\n");
            return value.Length<=520?value:value.Substring(0,517)+"...";
        }
    }
}
#endif
