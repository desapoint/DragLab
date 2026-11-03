using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    internal sealed class WholeEngineMoveSnapshot
    {
        internal readonly string Id,VehicleDefinitionId,CustomName,StorageId,ParentVehicleId;
        internal readonly SlotBinding[] Bindings;
        internal readonly Tune Tune;
        internal readonly FluidState Fluids;
        internal readonly PartInstance[] Parts;
        internal readonly Dictionary<PartCategory,string> Definitions;
        internal readonly bool Materialized;
        internal readonly string BuildErrorType,BuildErrorMessage;

        internal WholeEngineMoveSnapshot(PartMoveEngineState engine,SlotBinding[] bindings,Tune tune,FluidState fluids,
            PartInstance[] parts,Dictionary<PartCategory,string> definitions,bool materialized,string errorType,string errorMessage)
        {
            Id=engine.Id;VehicleDefinitionId=engine.VehicleDefinitionId;CustomName=engine.CustomName;
            StorageId=engine.StorageId;ParentVehicleId=engine.ParentVehicleId;
            Bindings=bindings;Tune=tune;Fluids=fluids;Parts=parts;Definitions=definitions;
            Materialized=materialized;BuildErrorType=errorType;BuildErrorMessage=errorMessage;
        }
    }

    internal sealed class WholeEngineMoveResult
    {
        internal bool Success {get;private set;}
        internal string ErrorType {get;private set;}
        internal string ErrorMessage {get;private set;}
        internal WholeEngineMoveSnapshot MovedEngine {get;private set;}
        internal WholeEngineMoveSnapshot[] ChangedEngines {get;private set;}
        internal WholePartMoveOwnerSnapshot[] CarOwners {get;private set;}

        internal static WholeEngineMoveResult Failure(Exception error)
            =>new WholeEngineMoveResult{ErrorType=error?.GetType().FullName,ErrorMessage=error?.Message};
        internal static WholeEngineMoveResult Completed(WholeEngineMoveSnapshot moved,WholeEngineMoveSnapshot[] changed,
            WholePartMoveOwnerSnapshot[] cars)
            =>new WholeEngineMoveResult{Success=true,MovedEngine=moved,ChangedEngines=changed,CarOwners=cars};
    }

    // Synchronous, dormant typed preview of the existing whole-engine Move family.
    internal static class WholeEngineMoveProjection
    {
        internal static WholeEngineMoveResult Evaluate(PlayerProfile profile,string assetId,
            InventoryKind destination,string destinationId,MechanicalCatalog catalog)
        {
            var state=new ProfileInstalledPartTransitionState(profile,true);
            using(var catalogRead=CartApplyCatalogReaders.Scoped(catalog))
            try
            {
                var preamble=InstalledPartTransition.Preamble(state,assetId,destination,destinationId);
                if(preamble.Kind!=PartMoveAssetKind.Engine)
                    throw new NotSupportedException("Whole-engine projection accepts physical engine assets only.");
                InstalledEngineTransition.MoveAfterPreamble(state,catalogRead,preamble,destination,destinationId);

                var snapshots=new Dictionary<PartMoveEngineState,WholeEngineMoveSnapshot>();
                var movedState=state.FindEngine(assetId);
                var moved=GetSnapshot(state,catalogRead,movedState,snapshots);
                var changed=state.ChangedEngines.Select(engine=>GetSnapshot(state,catalogRead,engine,snapshots)).ToArray();
                var cars=state.AffectedCars.Select(car=>WholePartMoveProjection.SnapshotCar(state,catalogRead,car)).ToArray();
                return WholeEngineMoveResult.Completed(moved,changed,cars);
            }
            catch(Exception error){return WholeEngineMoveResult.Failure(error);}
            finally{state.Dispose();}
        }

        static WholeEngineMoveSnapshot GetSnapshot(ProfileInstalledPartTransitionState state,ICartCatalogRead catalog,
            PartMoveEngineState engine,Dictionary<PartMoveEngineState,WholeEngineMoveSnapshot> snapshots)
        {
            if(!snapshots.TryGetValue(engine,out var snapshot)){snapshot=SnapshotEngine(state,catalog,engine);snapshots.Add(engine,snapshot);}
            return snapshot;
        }

        internal static WholeEngineMoveSnapshot SnapshotEngine(ProfileInstalledPartTransitionState state,
            ICartCatalogRead catalog,PartMoveEngineState engine)
        {
            var bindings=engine.BindingCopies;
            PartInstance[] parts=null;Dictionary<PartCategory,string> definitions=null;
            bool materialized=false;string errorType=null,errorMessage=null;
            try
            {
                if(!string.IsNullOrEmpty(engine.ParentVehicleId))
                    throw new InvalidOperationException("Attached engine descendants are represented by the owning car.");
                if(bindings==null)throw new InvalidOperationException("Engine bindings are unavailable for descendant materialization.");
                var captured=new List<PartInstance>(bindings.Length);
                foreach(var binding in bindings)
                {
                    var part=binding==null||string.IsNullOrWhiteSpace(binding.partInstanceId)?null:state.FindPart(binding.partInstanceId);
                    if(part==null)throw new InvalidOperationException("Engine contains an unresolved installed-part binding.");
                    captured.Add(part.Copy());
                }
                parts=captured.ToArray();definitions=new Dictionary<PartCategory,string>(state.EngineDefinitions(engine));materialized=true;
            }
            catch(Exception error){parts=null;definitions=null;errorType=error.GetType().FullName;errorMessage=error.Message;}
            return new WholeEngineMoveSnapshot(engine,bindings,engine.Tune?.Copy(),engine.Fluids?.Copy(),
                parts,definitions,materialized,errorType,errorMessage);
        }
    }
}
