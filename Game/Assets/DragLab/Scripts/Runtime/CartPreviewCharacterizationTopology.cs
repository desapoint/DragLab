#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    public static partial class CartPreviewCharacterizationChecks
    {
        static void RunTopologyMatrix(Trace trace,string subset=null)
        {
            bool runAll=subset==null;
            string selected=runAll?"all":subset;
            if(!runAll&&selected!="members-corners"&&selected!="owners-parts"&&selected!="blocks-locks")
                throw new ArgumentException("Unknown topology subset: "+selected,nameof(subset));
            trace.Add("TOPOLOGY_MATRIX|start|subset="+selected+";fixtures=authored-current-catalog;healing=never-after-mutation");
            if(runAll||selected=="members-corners")
            {
                RunTopologyMemberCases(trace);
                RunTopologyCornerCases(trace);
            }
            if(runAll||selected=="owners-parts")
            {
                RunTopologyOwnerCases(trace);
                RunTopologyPartMoveCases(trace);
            }
            if(runAll||selected=="blocks-locks")RunTopologyBlockAndLockCases(trace);
            trace.Add("TOPOLOGY_MATRIX|end|subset="+selected+";fullStep1=OPEN;native=separate");
        }

        static void RunTopologyMemberCases(Trace trace)
        {
            const string name="topology-members";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,FixtureSeed+101);
            var car=profile.cars.Find(item=>item.instanceId==carId);
            string validation=MechanicsOwnership.Validate(profile,profile.catalog);
            trace.Assert(car!=null&&validation==null,"Member topology begins with actual normalized valid profile / "+validation);
            var spec=profile.catalog.Spec(car.vehicleId);
            var packs=TopologyInstalledParts(profile,car).ToArray();
            var memberPacks=packs.Where(item=>item.condition?.elements!=null&&item.condition.elements.Length>1&&
                item.condition.elements.All(element=>element!=null)).ToArray();
            trace.Add("TOPOLOGY_DISCOVERY|"+name+"|vehicle="+car.vehicleId+";cylinders="+spec.sound.cylinders+
                ";multiMemberPacks="+string.Join(",",memberPacks.Select(item=>item.definitionId+"/"+item.condition.elements.Length+"/"+
                    string.Join("+",item.condition.elements.Select(element=>element.role).Distinct()))));
            if(spec.sound.cylinders<=1||!memberPacks.Any(item=>profile.catalog.Find(item.definitionId)?.category==PartCategory.Pistons))
                trace.Add("OPEN|"+name+"|current authored starter has no multi-cylinder installed piston topology.");
            else
                trace.Assert(true,"Authored multi-cylinder piston topology is present / "+spec.sound.cylinders+" cylinders");

            foreach(var category in new[]{PartCategory.Pistons,PartCategory.Valves,PartCategory.Rods})
            {
                var installed=memberPacks.FirstOrDefault(item=>profile.catalog.Find(item.definitionId)?.category==category);
                if(installed==null)
                {
                    trace.Add("OPEN|"+name+"|no actual installed multi-member pack for "+category+".");
                    continue;
                }
                var definition=profile.catalog.Find(installed.definitionId);
                var positions=InventoryUnits.Roles(profile,car,definition)
                    .SelectMany(role=>InventoryUnits.Positions(profile,car,definition.id,role).Select(position=>new{role,position})).ToArray();
                trace.Add("MEMBER_MAP|"+category+"|definition="+definition.id+";expectedCount="+
                    EngineElements.Count(spec,definition)+";actual="+installed.condition.elements.Length+
                    ";roles="+string.Join(";",InventoryUnits.Roles(profile,car,definition).Select(role=>role+"="+
                        string.Join(",",InventoryUnits.Positions(profile,car,definition.id,role)))));
                trace.Assert(EngineElements.Count(spec,definition)==installed.condition.elements.Length&&positions.Length==installed.condition.elements.Length,
                    "Actual member count/role/position topology agrees with authored spec / "+category);
                var roleCandidate=positions.FirstOrDefault(entry=>entry.position!=null);
                if(roleCandidate==null)
                {
                    trace.Add("OPEN|"+name+"|no actual individual position for "+category+".");
                    continue;
                }
                var row=new PartCartLine{action=CartAction.NewUnits,definitionId=definition.id,quantity=1,install=true,
                    role=roleCandidate.role,positions=new List<string>{roleCandidate.position}};
                CompareTotal(trace,"topology-unit-install-"+category,profile,carId,new[]{row},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                ComparePreview(trace,"topology-unit-install-"+category,profile,car,new[]{row});
            }

            // A second actual stock vehicle is optional; its real cylinder count extends the shape only if distinct.
            try
            {
                var second=CartPreviewCharacterizationFixtures.AddSecondOwnedCar(profile);
                var secondSpec=MechanicsOwnership.Resolve(profile,second,profile.catalog).spec;
                var secondPacks=TopologyInstalledParts(profile,second).Where(item=>item.condition?.elements!=null&&item.condition.elements.Length>0)
                    .Where(item=>EngineElements.Supported(profile.catalog.Find(item.definitionId).category)).ToArray();
                foreach(var pack in secondPacks)
                {
                    var definition=profile.catalog.Find(pack.definitionId);
                    trace.Add("MEMBER_MAP|second/"+definition.category+"|vehicle="+second.vehicleId+";definition="+definition.id+
                        ";expectedCount="+EngineElements.Count(secondSpec,definition)+";actual="+pack.condition.elements.Length+
                        ";roles="+string.Join("+",pack.condition.elements.Select(element=>element.role).Distinct()));
                    trace.Assert(EngineElements.Count(secondSpec,definition)==pack.condition.elements.Length,
                        "Actual second-car member count agrees with authored spec / "+second.vehicleId+"/"+definition.category);
                }
                trace.Add("TOPOLOGY_SECOND_CAR|vehicle="+second.vehicleId+";cylinders="+secondSpec.sound.cylinders+
                    ";id="+second.instanceId+";selected="+profile.SelectedCar?.instanceId);
                if(secondSpec.sound.cylinders!=spec.sound.cylinders)
                    trace.Assert(true,"Actual second stock car adds a distinct cylinder configuration / "+spec.sound.cylinders+"→"+secondSpec.sound.cylinders);
                else trace.Add("OPEN|topology-members|second seeded stock car shares cylinder count; no alternate count observed.");
            }
            catch(Exception error){trace.Add("OPEN|topology-members|second real stock car unavailable / "+error.GetType().FullName+" | "+error.Message);}
        }

        static void RunTopologyCornerCases(Trace trace)
        {
            foreach(var category in new[]{PartCategory.Tires,PartCategory.Wheels,PartCategory.Brakes})
            {
                string name="topology-corners-"+category;
                bool shapeFound=false;
                foreach(int corner in new[]{0,2})
                {
                    CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,FixtureSeed+102+(uint)category+(uint)corner*29u);
                    var car=profile.cars.Find(item=>item.instanceId==carId);
                    var pack=TopologyInstalledParts(profile,car).FirstOrDefault(item=>profile.catalog.Find(item.definitionId)?.category==category);
                    if(pack==null){trace.Add("OPEN|"+name+"|no actual fitted "+category+" pack.");continue;}
                    var definition=profile.catalog.Find(pack.definitionId);
                    var roles=InventoryUnits.Roles(profile,car,definition);
                    var positions=roles.SelectMany(role=>InventoryUnits.Positions(profile,car,definition.id,role).Select(position=>new{role,position})).ToArray();
                    if(positions.Length==0)
                    {
                        trace.Add("OPEN|"+name+"|actual fitted pack has no supported individual-unit positions/roles; no role was fabricated.");
                        continue;
                    }
                    if(category==PartCategory.Brakes)
                    {
                        trace.Add("OPEN|"+name+"|brakes have no modeled individual install path in current EngineElements; actual pack retained, no four-corner assumption.");
                        continue;
                    }
                    int corners=category==PartCategory.Tires?pack.condition?.tires?.Length??0:pack.condition?.elements?.Length??0;
                    if(corners!=4)
                    {
                        trace.Add("OPEN|"+name+"|actual corner condition count="+corners+"; four-corner install case unavailable.");
                        continue;
                    }
                    shapeFound=true;
                    var untouchedCornerFacts=new Dictionary<int,TopologyObjectFacts>();
                    for(int index=0;index<4;index++)if(index!=corner)
                    {
                        object beforeCorner=category==PartCategory.Tires?pack.condition.tires[index]:pack.condition.elements[index];
                        untouchedCornerFacts.Add(index,TopologyCaptureObjectFacts(beforeCorner));
                    }
                    trace.Add("CORNER_MAP|"+category+"|pack="+pack.instanceId+";base="+pack.definitionId+
                        ";corner="+corner+";overrides="+(category==PartCategory.Tires?TireFitments.HasOverrides(pack):WheelFitments.HasOverrides(pack))+
                        ";corners="+string.Join(";",Enumerable.Range(0,4).Select(index=>index+":"+
                            (category==PartCategory.Tires?TireFitments.DefinitionId(pack,index):WheelFitments.DefinitionId(pack,index))+"/"+
                            (category==PartCategory.Tires?pack.condition.tires[index].id:pack.condition.elements[index].id))));
                    var position=positions.FirstOrDefault(item=>item.position==(category==PartCategory.Tires?"tire:":"wheel:")+corner);
                    if(position==null)
                    {
                        trace.Add("OPEN|"+name+"|actual individual position missing for corner="+corner+".");
                        continue;
                    }
                    var line=new PartCartLine{action=CartAction.NewUnits,definitionId=definition.id,quantity=1,install=true,
                        role=position.role,positions=new List<string>{position.position}};
                    CompareTotal(trace,"corner-unit-install-"+category+"-"+corner,profile,carId,new[]{line},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                    ComparePreview(trace,"corner-unit-install-"+category+"-"+corner,profile,car,new[]{line});
                    var preview=TopologyAuditedPreview(trace,"corner-unit-install-"+category+"-"+corner,profile,car,new[]{line},out _);
                    var previewPack=preview.parts.Find(item=>item.instanceId==pack.instanceId);
                    bool retained=previewPack!=null;
                    if(retained)for(int index=0;index<4;index++)if(index!=corner)
                    {
                        object after=category==PartCategory.Tires?previewPack.condition.tires[index]:previewPack.condition.elements[index];
                        TopologyLogObjectComparison(trace,"corner-unit-install-"+category+"-"+corner,
                            "untouched-corner["+index+"]",untouchedCornerFacts[index],after);
                        if(untouchedCornerFacts[index].Canonical!=TypedCanonical(after,new Dictionary<string,string>()))retained=false;
                    }
                    trace.Assert(retained,"Individual corner replacement preserves all untouched physical corner records / "+category+"/"+corner);
                }
                if(!shapeFound)trace.Add("OPEN|"+name+"|no supported actual four-corner candidate was found for front and rear positions.");
            }
        }

        static void RunTopologyOwnerCases(Trace trace)
        {
            const string name="topology-owner-engine-return";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string starterId,FixtureSeed+103);
            var starter=profile.cars.Find(item=>item.instanceId==starterId);
            var starterTune=TypedCanonical(starter.tune,new Dictionary<string,string>());
            var starterFluids=TypedCanonical(starter.fluids,new Dictionary<string,string>());
            OwnedCar second;EngineAssemblyInstance engine;
            try
            {
                second=CartPreviewCharacterizationFixtures.AddSecondOwnedCar(profile);
                VehicleCollection.Select(profile,starterId);
                engine=CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(profile,second.instanceId);
            }
            catch(Exception error)
            {
                trace.Add("OPEN|topology-owner-engine-return|actual second-car engine storage fixture unavailable / "+error.GetType().FullName+" | "+error.Message);
                return;
            }
            var selectedId=profile.SelectedCar?.instanceId;
            var fluids=TypedCanonical(engine.fluids,new Dictionary<string,string>());
            var tune=TypedCanonical(engine.tune,new Dictionary<string,string>());
            trace.Assert(string.IsNullOrEmpty(engine.parentVehicleId)&&engine.storageId==SharedInventory.Storage,
                "Actual second-car engine is detached into Storage through domain move / "+engine.instanceId);
            var returnLine=new PartCartLine{action=CartAction.Move,assetId=engine.instanceId,
                definitionId=engine.vehicleDefinitionId,destination=InventoryKind.Car,destinationId=second.instanceId};
            var targetFluidsBefore=TopologyCaptureObjectFacts(engine.fluids);
            var targetTuneBefore=TopologyCaptureObjectFacts(engine.tune);
            var nonTargetFluidsBefore=TopologyCaptureObjectFacts(starter.fluids);
            var nonTargetTuneBefore=TopologyCaptureObjectFacts(starter.tune);
            var selectedGraphBeforeFacts=TopologyCaptureObjectFacts(starter);
            string selectedGraphBefore=selectedGraphBeforeFacts.Canonical;
            CompareTotal(trace,name,profile,second.instanceId,new[]{returnLine},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            ComparePreview(trace,name,profile,second,new[]{returnLine});
            var result=TopologyAuditedPreview(trace,name,profile,second,new[]{returnLine},out _);
            var resultEngine=result.engines.Find(item=>item.instanceId==engine.instanceId);
            var resultOwner=result.cars.Find(item=>item.instanceId==second.instanceId);
            var resultStarter=result.cars.Find(item=>item.instanceId==starterId);
            string resultFluidFacts=TypedCanonical(resultOwner?.fluids,new Dictionary<string,string>());
            string resultTuneFacts=TypedCanonical(resultOwner?.tune,new Dictionary<string,string>());
            string resultStarterFluidFacts=TypedCanonical(resultStarter?.fluids,new Dictionary<string,string>());
            string resultStarterTuneFacts=TypedCanonical(resultStarter?.tune,new Dictionary<string,string>());
            string selectedGraphAfter=TypedCanonical(resultStarter,new Dictionary<string,string>());
            TopologyLogObjectComparison(trace,name,"target.fluids",targetFluidsBefore,resultOwner?.fluids);
            TopologyLogObjectComparison(trace,name,"target.tune",targetTuneBefore,resultOwner?.tune);
            TopologyLogObjectComparison(trace,name,"non-target-starter.fluids",nonTargetFluidsBefore,resultStarter?.fluids);
            TopologyLogObjectComparison(trace,name,"non-target-starter.tune",nonTargetTuneBefore,resultStarter?.tune);
            TopologyLogObjectComparison(trace,name,"selected-car.graph",selectedGraphBeforeFacts,resultStarter);
            trace.Assert(resultEngine!=null&&resultOwner!=null&&resultEngine.parentVehicleId==second.instanceId&&resultOwner.engineAssemblyId==engine.instanceId&&
                resultStarter!=null&&resultStarter.engineAssemblyId==starter.engineAssemblyId,
                "Detached real engine returns to same compatible car without changing non-target owner / "+engine.instanceId);
            trace.Assert(resultOwner!=null&&fluids==resultFluidFacts&&tune==resultTuneFacts&&result.SelectedCar?.instanceId==selectedId&&
                resultStarter!=null&&starterTune==resultStarterTuneFacts&&starterFluids==resultStarterFluidFacts,
                "Same-engine return conserves exact fluid/tune state for target and non-target owner / "+engine.instanceId);
            trace.Assert(resultStarter!=null&&selectedGraphBefore==selectedGraphAfter,
                "Selected non-target car graph remains exactly unchanged during other-owner engine return / "+starterId);
            trace.Add("OWNER_GRAPH|selected="+selectedId+";starter="+starterId+";target="+second.instanceId+
                ";engine="+engine.instanceId+";mount="+engine.vehicleDefinitionId+";return=actual-domain-Move");

            const string incompatible="topology-owner-incompatible-engine";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var otherProfile,out string otherStarterId,FixtureSeed+104);
            try
            {
                var otherCar=CartPreviewCharacterizationFixtures.AddSecondOwnedCar(otherProfile);
                var detached=CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(otherProfile,otherCar.instanceId);
                var otherStarter=otherProfile.cars.Find(item=>item.instanceId==otherStarterId);
                if(detached.vehicleDefinitionId==otherStarter.vehicleId)
                    trace.Add("OPEN|"+incompatible+"|seeded cars unexpectedly share same mount identity; no incompatible engine case.");
                else
                {
                    var bad=new PartCartLine{action=CartAction.Move,assetId=detached.instanceId,
                        definitionId=detached.vehicleDefinitionId,destination=InventoryKind.Car,destinationId=otherStarterId};
                    CompareTotal(trace,incompatible,otherProfile,otherStarterId,new[]{bad},ValuationPolicy.Defaults(),
                        CartPreviewCharacterizationFixtures.FixedNowTicks,typeof(InvalidOperationException).FullName,
                        "Engine mounts are incompatible with this chassis.");
                }
            }
            catch(Exception error){trace.Add("OPEN|"+incompatible+"|actual distinct-mount transfer unavailable / "+error.GetType().FullName+" | "+error.Message);}
        }

        static void RunTopologyPartMoveCases(Trace trace)
        {
            const string storageCase="topology-part-to-storage";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,FixtureSeed+105);
            var car=profile.cars.Find(item=>item.instanceId==carId);
            var installed=TopologyInstalledParts(profile,car).FirstOrDefault(item=>MechanicalCatalog.EngineSlot(profile.catalog.Find(item.definitionId).category));
            if(installed==null){trace.Add("OPEN|"+storageCase+"|no actual installed engine part.");}
            else
            {
                var row=new PartCartLine{action=CartAction.Move,assetId=installed.instanceId,definitionId=installed.definitionId,
                    destination=InventoryKind.Storage,destinationId=SharedInventory.Garage};
                CompareTotal(trace,storageCase,profile,carId,new[]{row},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                ComparePreview(trace,storageCase,profile,car,new[]{row});
                trace.Add("PHYSICAL_SOURCE|"+storageCase+"|part="+installed.instanceId+";definition="+installed.definitionId+
                    ";slot="+installed.slot+";aggregateHealth="+installed.condition.health+";fault="+installed.condition.failed);
            }

            const string engineInstall="topology-loose-part-to-detached-engine";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var engineProfile,out string engineCarId,FixtureSeed+111);
            try
            {
                var engineCar=engineProfile.cars.Find(item=>item.instanceId==engineCarId);
                var enginePart=TopologyInstalledParts(engineProfile,engineCar)
                    .FirstOrDefault(item=>MechanicalCatalog.EngineSlot(engineProfile.catalog.Find(item.definitionId).category)&&
                        engineProfile.catalog.Find(item.definitionId).category!=PartCategory.CylinderBlock);
                if(enginePart==null)trace.Add("OPEN|"+engineInstall+"|no actual non-block engine part to select a compatible authored definition.");
                else
                {
                    string looseId=PartOperations.Buy(engineProfile,engineProfile.catalog,engineCarId,enginePart.definitionId,false);
                    var storedEngine=CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(engineProfile,engineCarId);
                    var move=new PartCartLine{action=CartAction.Move,assetId=looseId,definitionId=enginePart.definitionId,
                        destination=InventoryKind.Engine,destinationId=storedEngine.instanceId};
                    CompareTotal(trace,engineInstall,engineProfile,engineCarId,new[]{move},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                    ComparePreview(trace,engineInstall,engineProfile,engineCar,new[]{move});
                    var preview=TopologyAuditedPreview(trace,engineInstall,engineProfile,engineCar,new[]{move},out _);
                    var moved=preview.parts.Find(item=>item.instanceId==looseId);
                    var resultEngine=preview.engines.Find(item=>item.instanceId==storedEngine.instanceId);
                    trace.Assert(moved!=null&&moved.location==PartLocation.Assembly&&moved.ownerId==storedEngine.instanceId&&
                        resultEngine!=null&&resultEngine.slots.Any(binding=>binding.partInstanceId==looseId),
                        "Loose actual part retains identity while moving into compatible detached engine / "+looseId);
                    trace.Add("ENGINE_PART_MOVE|engine="+storedEngine.instanceId+";part="+looseId+
                        ";definition="+enginePart.definitionId+";destination=stored-engine");
                }
            }
            catch(Exception error){trace.Add("OPEN|"+engineInstall+"|actual purchase/detach/install fixture unavailable / "+error.GetType().FullName+" | "+error.Message);}

            const string crossOwner="topology-part-to-nonselected-owner";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var secondProfile,out string sourceId,FixtureSeed+106);
            try
            {
                var source=secondProfile.cars.Find(item=>item.instanceId==sourceId);
                var target=CartPreviewCharacterizationFixtures.AddSecondOwnedCar(secondProfile);
                VehicleCollection.Select(secondProfile,sourceId);
                string selectedId=secondProfile.SelectedCar?.instanceId;
                var sourceSpec=secondProfile.catalog.Spec(source.vehicleId);var targetSpec=secondProfile.catalog.Spec(target.vehicleId);
                var common=secondProfile.catalog.parts.Where(definition=>definition!=null&&MechanicalCatalog.EngineSlot(definition.category)&&
                    definition.category!=PartCategory.CylinderBlock&&
                    secondProfile.catalog.Fits(sourceSpec,definition)&&secondProfile.catalog.Fits(targetSpec,definition))
                    .OrderBy(definition=>definition.price).ThenBy(definition=>definition.id,StringComparer.Ordinal).FirstOrDefault();
                if(common==null||selectedId==target.instanceId)
                    trace.Add("OPEN|"+crossOwner+"|no compatible real authored part and distinct selected/target cars.");
                else
                {
                    string newPartId=PartOperations.Buy(secondProfile,secondProfile.catalog,source.instanceId,common.id,false);
                    var loose=secondProfile.parts.Find(item=>item.instanceId==newPartId);
                    var oldTarget=target.installed.slots.FirstOrDefault(binding=>binding.slot==common.category.ToString());
                    string oldTargetId=oldTarget?.partInstanceId;
                    var beforeSelectedFacts=TopologyCaptureObjectFacts(source);
                    string beforeSelected=beforeSelectedFacts.Canonical;
                    var row=new PartCartLine{action=CartAction.Move,assetId=newPartId,definitionId=common.id,
                        destination=InventoryKind.Car,destinationId=target.instanceId};
                    CompareTotal(trace,crossOwner,secondProfile,source.instanceId,new[]{row},ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                    ComparePreview(trace,crossOwner,secondProfile,source,new[]{row});
                    var preview=TopologyAuditedPreview(trace,crossOwner,secondProfile,source,new[]{row},out _);
                    var resultSource=preview.cars.Find(item=>item.instanceId==source.instanceId);
                    var resultTarget=preview.cars.Find(item=>item.instanceId==target.instanceId);
                    var moved=preview.parts.Find(item=>item.instanceId==newPartId);
                    var displaced=string.IsNullOrEmpty(oldTargetId)?null:preview.parts.Find(item=>item.instanceId==oldTargetId);
                    string afterSelected=TypedCanonical(resultSource,new Dictionary<string,string>());
                    TopologyLogObjectComparison(trace,crossOwner,"selected-car.graph",beforeSelectedFacts,resultSource);
                    trace.Assert(resultTarget!=null&&resultSource!=null&&preview.SelectedCar?.instanceId==selectedId&&resultTarget.installed.slots.Any(binding=>binding.partInstanceId==newPartId)&&
                        moved!=null&&moved.location==PartLocation.Assembly&&moved.ownerId==target.engineAssemblyId&&
                        (string.IsNullOrEmpty(oldTargetId)||displaced!=null&&displaced.location==PartLocation.Inventory),
                        "Cross-owner move installs actual compatible part and retains displaced target identity / "+common.id);
                    trace.Assert(beforeSelected==afterSelected,
                        "Moving a loose part to nonselected owner leaves selected car graph exact / "+selectedId);
                    trace.Add("OWNER_PART_MOVE|source="+source.instanceId+";target="+target.instanceId+";selected="+selectedId+
                        ";part="+loose.instanceId+";definition="+common.id+";displaced="+(oldTargetId??"none"));
                }
            }
            catch(Exception error){trace.Add("OPEN|"+crossOwner+"|real cross-fit move unavailable / "+error.GetType().FullName+" | "+error.Message);}

            const string collision="topology-destination-slot-collision";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var collisionProfile,out string collisionCarId,FixtureSeed+110);
            try
            {
                var collisionCar=collisionProfile.cars.Find(item=>item.instanceId==collisionCarId);
                var collisionDefinition=TopologyInstalledParts(collisionProfile,collisionCar)
                    .Select(item=>collisionProfile.catalog.Find(item.definitionId))
                    .FirstOrDefault(definition=>definition!=null&&MechanicalCatalog.EngineSlot(definition.category)&&definition.category!=PartCategory.CylinderBlock);
                if(collisionDefinition==null)trace.Add("OPEN|"+collision+"|no actual non-block engine part definition for collision rows.");
                else
                {
                    string firstId=PartOperations.Buy(collisionProfile,collisionProfile.catalog,collisionCarId,collisionDefinition.id,false);
                    string secondId=PartOperations.Buy(collisionProfile,collisionProfile.catalog,collisionCarId,collisionDefinition.id,false);
                    var first=new PartCartLine{action=CartAction.Move,assetId=firstId,definitionId=collisionDefinition.id,
                        destination=InventoryKind.Car,destinationId=collisionCarId};
                    var second=new PartCartLine{action=CartAction.Move,assetId=secondId,definitionId=collisionDefinition.id,
                        destination=InventoryKind.Car,destinationId=collisionCarId};
                    foreach(var ordered in new[]{new[]{first,second},new[]{second,first}})
                        CompareTotal(trace,collision+"-"+ordered[0].assetId,collisionProfile,collisionCarId,ordered,
                            ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks,
                            typeof(InvalidOperationException).FullName,"Conflicting slot installations.");
                    trace.Add("MOVE_COLLISION|category="+collisionDefinition.category+";first="+firstId+";second="+secondId+
                        ";order=both-permutations;expected=Conflicting slot installations.");
                }
            }
            catch(Exception error){trace.Add("OPEN|"+collision+"|actual conflicting move fixture unavailable / "+error.GetType().FullName+" | "+error.Message);}
        }

        static void RunTopologyBlockAndLockCases(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,FixtureSeed+107);
            var car=profile.cars.Find(item=>item.instanceId==carId);
            var spec=profile.catalog.Spec(car.vehicleId);
            string currentBlock=MechanicsOwnership.Definitions(profile,car).TryGetValue(PartCategory.CylinderBlock,out string blockId)?blockId:null;
            var replacement=profile.catalog.parts.Where(definition=>definition!=null&&definition.category==PartCategory.CylinderBlock&&
                    profile.catalog.Fits(spec,definition)&&definition.id!=currentBlock)
                .OrderBy(definition=>definition.price).ThenBy(definition=>definition.id,StringComparer.Ordinal).FirstOrDefault();
            if(replacement==null)
                trace.Add("OPEN|topology-block-preparation|authored car has no distinct compatible replacement block.");
            else
            {
                var core=new PartCartLine{action=CartAction.NewSet,definitionId=replacement.id,quantity=1,install=true};
                CompareTotal(trace,"topology-unprepared-block-first-error",profile,carId,new[]{core},ValuationPolicy.Defaults(),
                    CartPreviewCharacterizationFixtures.FixedNowTicks,typeof(InvalidOperationException).FullName,
                    "Prepare the block and its required components before installation.");
                var plan=TopologyAuditedBlockBuild(trace,"topology-block-reuse",profile,carId,core,Array.Empty<PartCartLine>(),out bool buildStable);
                trace.Add("BLOCK_PLAN|reuse|block="+replacement.id+";owner="+plan.Owner+";ready="+plan.Ready+
                    ";error="+(plan.Error??"<none>")+";total="+plan.Total+";choices="+
                    string.Join(",",plan.Choices.OrderBy(item=>item.Key).Select(item=>item.Key+"="+item.Value.Source+":"+item.Value.Id)));
                trace.Assert(plan.Lines!=null&&plan.Lines.Any(line=>line.preparedBlock&&line.definitionId==replacement.id),
                    "BlockPreparation.Build returns identified prepared core through actual API / "+replacement.id);
                if(plan.Ready&&buildStable)
                {
                    string followOnSource=SourceSnapshot(profile),followOnCatalog=CatalogSnapshot(profile);
                    var followOn=CartPreviewLiteralTestClone.Copy(profile);
                    bool followOnExact=followOnSource==SourceSnapshot(followOn)&&followOnCatalog==CatalogSnapshot(followOn);
                    trace.Assert(followOnExact,"Prepared block follow-on uses a fresh exact literal source/catalog graph / "+replacement.id);
                    if(followOnExact)
                    {
                        var followOnCar=followOn.cars.Find(item=>item.instanceId==carId);
                        CompareTotal(trace,"topology-prepared-block-total",followOn,carId,plan.Lines,ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                        ComparePreview(trace,"topology-prepared-block-preview",followOn,followOnCar,plan.Lines);
                    }
                    else trace.Add("OPEN|topology-prepared-block|fresh equivalent source clone failed; follow-on oracle comparison skipped.");
                }
                else trace.Add("OPEN|topology-block-preparation|actual authored Reuse plan not compared further; ready="+plan.Ready+
                    ";sourceStable="+buildStable+";error="+(plan.Error??"<none>"));

                var requiredCategories=profile.catalog.Recipe(car.vehicleId).Keys.Where(category=>MechanicalCatalog.EngineSlot(category)&&category!=PartCategory.CylinderBlock)
                    .Where(category=>!PartSemantics.Optional(category,spec,MechanicsOwnership.Definitions(profile,car),profile.catalog)).ToArray();
                if(requiredCategories.Length==0)trace.Add("OPEN|topology-block-missing|no required engine component in current authored recipe.");
                else
                {
                    var required=requiredCategories[0];
                    var broken=CartPreviewLiteralTestClone.Copy(profile);
                    var brokenCar=broken.cars.Find(item=>item.instanceId==carId);
                    var binding=brokenCar.installed.slots.FirstOrDefault(item=>item.slot==required.ToString());
                    var removed=binding==null?null:broken.parts.Find(item=>item.instanceId==binding.partInstanceId);
                    if(binding==null||removed==null)trace.Add("OPEN|topology-block-missing|required slot unavailable for controlled fixture: "+required);
                    else
                    {
                        PartOperations.Remove(broken,carId,required);
                        broken.parts.Remove(removed);
                        var missing=TopologyAuditedBlockBuild(trace,"topology-block-missing-"+required,broken,carId,core,
                            Array.Empty<PartCartLine>(),out _);
                        trace.Add("BLOCK_PLAN|missing-required|category="+required+";removedActualId="+removed.instanceId+
                            ";ready="+missing.Ready+";error="+(missing.Error??"<none>"));
                        trace.Assert(!missing.Ready&&!string.IsNullOrEmpty(missing.Error),
                            "Actual missing required component does not produce an accepted complete block plan / "+required);
                    }
                }
            }

            // Both reservation locks are created by their actual domain operations on disposable valid fixtures.
            RunTopologyLockCase(trace,"garage-job-lock",true,FixtureSeed+108);
            RunTopologyLockCase(trace,"engine-rebuild-lock",false,FixtureSeed+109);
        }

        static void RunTopologyLockCase(Trace trace,string name,bool garage,uint seed)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,seed);
            var car=profile.cars.Find(item=>item.instanceId==carId);
            try
            {
                long now=Math.Max(CartPreviewCharacterizationFixtures.FixedNowTicks,profile.lastEconomyUtcTicks);
                if(garage)
                {
                    GarageBusiness.Ensure(profile);
                    now=Math.Max(now,profile.business.accruedThroughUtcTicks);
                    var offer=GarageBusiness.DefaultPolicy().offers.First(item=>item.id=="photo-session");
                    GarageBusiness.Assign(profile,GarageBusiness.DefaultPolicy(),carId,offer.id,now);
                }
                else EngineRebuilds.Start(profile,carId,now,new OperatingPolicy());
                var part=TopologyInstalledParts(profile,car).FirstOrDefault();
                if(part==null){trace.Add("OPEN|"+name+"|no actual cart row target definition.");return;}
                var line=new PartCartLine{action=CartAction.Move,assetId=part.instanceId,definitionId=part.definitionId,
                    destination=InventoryKind.Storage,destinationId=SharedInventory.Garage};
                string message=garage?"Car and installed components reserved by garage job; claim or cancel it first.":
                    "Engine reserved at rebuild shop; collect it before modifying/racing this car.";
                CompareTotal(trace,"topology-"+name,profile,carId,new[]{line},ValuationPolicy.Defaults(),
                    CartPreviewCharacterizationFixtures.FixedNowTicks,typeof(InvalidOperationException).FullName,message);
            }
            catch(Exception error){trace.Add("OPEN|"+name+"|actual reservation operation unavailable / "+error.GetType().FullName+" | "+error.Message);}
        }

        sealed class TopologyObjectFacts
        {
            public readonly string RuntimeType,Canonical;
            public TopologyObjectFacts(string runtimeType,string canonical)
            {RuntimeType=runtimeType;Canonical=canonical;}
        }

        static TopologyObjectFacts TopologyCaptureObjectFacts(object value)
        {
            return new TopologyObjectFacts(value==null?"<null>":value.GetType().FullName,
                TypedCanonical(value,new Dictionary<string,string>()));
        }

        static void TopologyLogObjectComparison(Trace trace,string caseName,string fieldName,TopologyObjectFacts before,object after)
        {
            string beforeCanonical=before.Canonical;
            string afterCanonical=TypedCanonical(after,new Dictionary<string,string>());
            string beforeType=before.RuntimeType;
            string afterType=after==null?"<null>":after.GetType().FullName;
            trace.Add("OUTPUT_BEFORE|"+caseName+"|"+fieldName+"|type="+beforeType+";canonical="+beforeCanonical);
            trace.Add("OUTPUT_AFTER|"+caseName+"|"+fieldName+"|type="+afterType+";canonical="+afterCanonical);
            if(beforeCanonical==afterCanonical)
            {
                trace.Add("OUTPUT_FIRST_DIFF|"+caseName+"|"+fieldName+"|equal");
                return;
            }
            int difference=0,common=Math.Min(beforeCanonical.Length,afterCanonical.Length);
            while(difference<common&&beforeCanonical[difference]==afterCanonical[difference])difference++;
            string beforeContext=TopologyDifferenceContext(beforeCanonical,difference);
            string afterContext=TopologyDifferenceContext(afterCanonical,difference);
            trace.Add("OUTPUT_FIRST_DIFF|"+caseName+"|"+fieldName+"|index="+difference+
                ";beforeFieldContext="+beforeContext+";afterFieldContext="+afterContext);
        }

        static string TopologyDifferenceContext(string canonical,int index)
        {
            if(canonical==null)return "<null-canonical>";
            int offset=Math.Max(0,Math.Min(index,canonical.Length));
            int start=Math.Max(0,offset-90),end=Math.Min(canonical.Length,offset+150);
            int fieldStart=canonical.LastIndexOf(';',Math.Max(0,offset-1));
            if(fieldStart>=0&&offset-fieldStart<260)start=Math.Max(fieldStart+1,start);
            return canonical.Substring(start,Math.Max(0,end-start));
        }

        static PlayerProfile TopologyAuditedPreview(Trace trace,string name,PlayerProfile source,OwnedCar car,
            IEnumerable<PartCartLine> rows,out bool stable)
        {
            var sourceRows=rows.ToArray();var previewRows=sourceRows.Select(line=>line?.Copy()).ToArray();
            string sourceBefore=SourceSnapshot(source),catalogBefore=CatalogSnapshot(source),rowsBefore=RowsSnapshot(sourceRows);
            var randomBefore=UnityEngine.Random.state;
            var input=CartPreviewLiteralTestClone.Copy(source);
            string inputBefore=SourceSnapshot(input),inputCatalogBefore=CatalogSnapshot(input);
            var inputCar=input.cars.Find(item=>item.instanceId==car.instanceId);
            trace.Assert(sourceBefore==inputBefore&&catalogBefore==inputCatalogBefore&&inputCar!=null,
                "Direct preview probe starts from a separate exact literal profile/catalog graph / "+name);
            PlayerProfile result=null;Exception callError=null;
            try{result=PartCart.PreviewProfile(input,inputCar,previewRows);}
            catch(Exception error){callError=error;}
            string inputAfter=SourceSnapshot(input),inputCatalogAfter=CatalogSnapshot(input),sourceAfter=SourceSnapshot(source);
            bool randomChanged=!randomBefore.Equals(UnityEngine.Random.state);
            bool rowsChanged=rowsBefore!=RowsSnapshot(sourceRows)||rowsBefore!=RowsSnapshot(previewRows);
            bool inputUnchanged=inputBefore==inputAfter&&inputCatalogBefore==inputCatalogAfter;
            bool sourceUnchanged=sourceBefore==sourceAfter&&catalogBefore==CatalogSnapshot(source);
            stable=inputUnchanged&&sourceUnchanged&&!randomChanged&&!rowsChanged;
            UnityEngine.Random.state=randomBefore;
            if(!inputUnchanged)trace.SourceDifference("topology-preview-"+name,inputBefore,inputAfter,inputAfter);
            if(!sourceUnchanged)trace.SourceDifference("topology-preview-source-"+name,sourceBefore,sourceAfter,sourceAfter);
            trace.Add("TOPOLOGY_DIRECT_PREVIEW|"+name+"|sourceUnchanged="+sourceUnchanged+
                ";isolatedInputUnchanged="+inputUnchanged+";catalogUnchanged="+(catalogBefore==CatalogSnapshot(source))+
                ";unityRandomUnchanged="+!randomChanged+";rowsUnchanged="+!rowsChanged+
                ";exception="+(callError==null?"<none>":callError.GetType().FullName+" | "+callError.Message));
            trace.Assert(inputUnchanged,"Direct preview candidate input and shared catalog remain exactly unchanged / "+name);
            trace.Assert(sourceUnchanged,"Direct preview caller source and catalog remain exactly unchanged / "+name);
            trace.Assert(!randomChanged,"Direct preview preserves Unity random state / "+name);
            trace.Assert(!rowsChanged,"Direct preview preserves both original and isolated ordered rows / "+name);
            if(callError!=null)throw new InvalidOperationException("Audited direct preview threw / "+name+" / "+callError.GetType().FullName+" | "+callError.Message,callError);
            return result;
        }

        static BlockPreparationPlan TopologyAuditedBlockBuild(Trace trace,string name,PlayerProfile source,string carId,
            PartCartLine core,IEnumerable<PartCartLine> rows,out bool stable)
        {
            var sourceRows=rows.ToArray();var buildRows=sourceRows.Select(line=>line?.Copy()).ToArray();
            var buildCore=core.Copy();
            string sourceBefore=SourceSnapshot(source),catalogBefore=CatalogSnapshot(source);
            string sourceRowsBefore=RowsSnapshot(sourceRows);
            var randomBefore=UnityEngine.Random.state;
            var input=CartPreviewLiteralTestClone.Copy(source);
            string inputBefore=SourceSnapshot(input),inputCatalogBefore=CatalogSnapshot(input);
            string buildRowsBefore=RowsSnapshot(buildRows),buildCoreBefore=RowsSnapshot(new[]{buildCore});
            trace.Assert(sourceBefore==inputBefore&&catalogBefore==inputCatalogBefore,
                "BlockBuild probe starts from an exact literal source/catalog graph / "+name);
            BlockPreparationPlan plan=null;Exception callError=null;
            try{plan=BlockPreparation.Build(input,carId,buildCore,buildRows,BlockPreparationMode.Reuse,null,ValuationPolicy.Defaults());}
            catch(Exception error){callError=error;}
            string inputAfter=SourceSnapshot(input),inputCatalogAfter=CatalogSnapshot(input),sourceAfter=SourceSnapshot(source);
            bool randomChanged=!randomBefore.Equals(UnityEngine.Random.state);
            bool rowsChanged=sourceRowsBefore!=RowsSnapshot(sourceRows)||buildRowsBefore!=RowsSnapshot(buildRows)||
                buildCoreBefore!=RowsSnapshot(new[]{buildCore});
            bool inputUnchanged=inputBefore==inputAfter&&inputCatalogBefore==inputCatalogAfter;
            bool sourceUnchanged=sourceBefore==sourceAfter&&catalogBefore==CatalogSnapshot(source);
            // Disposable-input mutation still fails the assertion below. Fresh
            // follow-on rows remain auditable if the original graph, shared
            // catalog, RNG and supplied rows were not changed by the build.
            stable=sourceUnchanged&&!randomChanged&&!rowsChanged;
            UnityEngine.Random.state=randomBefore;
            if(!inputUnchanged)trace.SourceDifference("topology-block-build-"+name,inputBefore,inputAfter,inputAfter);
            if(!sourceUnchanged)trace.SourceDifference("topology-block-build-source-"+name,sourceBefore,sourceAfter,sourceAfter);
            trace.Add("TOPOLOGY_BLOCK_BUILD_AUDIT|"+name+"|sourceUnchanged="+sourceUnchanged+
                ";isolatedInputUnchanged="+inputUnchanged+";catalogUnchanged="+(catalogBefore==CatalogSnapshot(source))+
                ";unityRandomUnchanged="+!randomChanged+";rowsUnchanged="+!rowsChanged+
                ";exception="+(callError==null?"<none>":callError.GetType().FullName+" | "+callError.Message));
            trace.Assert(inputUnchanged,"BlockPreparation.Build preserves isolated input and shared catalog / "+name);
            trace.Assert(sourceUnchanged,"BlockPreparation.Build preserves caller source and catalog / "+name);
            trace.Assert(!randomChanged,"BlockPreparation.Build preserves Unity random state / "+name);
            trace.Assert(!rowsChanged,"BlockPreparation.Build preserves source rows and core line / "+name);
            if(callError!=null)throw new InvalidOperationException("Audited BlockPreparation.Build threw / "+name+" / "+callError.GetType().FullName+" | "+callError.Message,callError);
            return plan;
        }

        static IEnumerable<PartInstance> TopologyInstalledParts(PlayerProfile profile,OwnedCar car)
        {
            if(profile==null||car?.installed?.slots==null)return Enumerable.Empty<PartInstance>();
            return car.installed.slots.Select(binding=>profile.parts.Find(item=>item.instanceId==binding.partInstanceId)).Where(item=>item!=null);
        }
    }
}
#endif
