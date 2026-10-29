using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    internal sealed class InventoryMoveRequest
    {
        internal readonly string AssetId;
        internal readonly InventoryKind Destination;
        internal readonly string TargetId;
        internal InventoryMoveRequest(string assetId,InventoryKind destination,string targetId)
        {AssetId=assetId;Destination=destination;TargetId=targetId;}
    }

    internal sealed class LooseUnitMoveSnapshot
    {
        internal readonly string Id,DefinitionId,StorageId;
        internal readonly int UnitPrice;
        internal readonly bool TireUnit;
        internal readonly EngineElement Element;
        internal LooseUnitMoveSnapshot(PartMoveLooseUnitState unit)
        {
            Id=unit.Id;DefinitionId=unit.DefinitionId;StorageId=unit.StorageId;UnitPrice=unit.UnitPrice;
            TireUnit=unit.TireUnit;Element=unit.Element?.Copy();
        }
    }

    internal sealed class OrderedInventoryMoveResult
    {
        internal bool Success {get;private set;}
        internal string ErrorType {get;private set;}
        internal string ErrorMessage {get;private set;}
        internal int FailedIndex {get;private set;}
        internal PartInstance[] ChangedParts {get;private set;}
        internal WholeEngineMoveSnapshot[] ChangedEngines {get;private set;}
        internal LooseUnitMoveSnapshot[] ChangedUnits {get;private set;}
        internal WholePartMoveOwnerSnapshot[] CarOwners {get;private set;}

        internal static OrderedInventoryMoveResult Failure(Exception error,int index)
            =>new OrderedInventoryMoveResult{Success=false,ErrorType=error?.GetType().FullName,ErrorMessage=error?.Message,FailedIndex=index};
        internal static OrderedInventoryMoveResult Completed(PartInstance[] parts,WholeEngineMoveSnapshot[] engines,
            LooseUnitMoveSnapshot[] units,WholePartMoveOwnerSnapshot[] cars)
            =>new OrderedInventoryMoveResult{Success=true,FailedIndex=-1,ChangedParts=parts,ChangedEngines=engines,
                ChangedUnits=units,CarOwners=cars};
    }

    // Ordered, synchronous raw-domain move composition. This is intentionally not
    // cart valuation, validation, persistence or caller activation.
    internal static class OrderedInventoryMoveProjection
    {
        internal static OrderedInventoryMoveResult Evaluate(PlayerProfile profile,
            IReadOnlyList<InventoryMoveRequest> requests,MechanicalCatalog catalog)
        {
            if(requests==null)return OrderedInventoryMoveResult.Failure(new ArgumentNullException("requests"),-1);
            InventoryMoveRequest[] ordered;
            try
            {
                ordered=new InventoryMoveRequest[requests.Count];
                for(int i=0;i<ordered.Length;i++)ordered[i]=requests[i];
            }
            catch(Exception error){return OrderedInventoryMoveResult.Failure(error,-1);}

            var state=new ProfileInstalledPartTransitionState(profile,true);
            int failedIndex=-1;
            try
            {
                for(int i=0;i<ordered.Length;i++)
                {
                    failedIndex=i;
                    var request=ordered[i];
                    var preamble=InstalledPartTransition.Preamble(state,request.AssetId,request.Destination,request.TargetId);
                    InstalledInventoryTransition.MoveAfterPreamble(state,catalog,preamble,
                        request.Destination,request.TargetId,PartMoveMemberPolicy.ProjectionRequireExisting);
                }

                var snapshotCache=new Dictionary<PartMoveEngineState,WholeEngineMoveSnapshot>();
                var engines=state.ChangedEngines.Select(engine=>
                {
                    if(!snapshotCache.TryGetValue(engine,out var snapshot))
                    {snapshot=WholeEngineMoveProjection.SnapshotEngine(state,catalog,engine);snapshotCache.Add(engine,snapshot);}
                    return snapshot;
                }).ToArray();
                var parts=state.ChangedParts.Select(part=>part.Copy()).ToArray();
                var units=state.ChangedUnits.Select(unit=>new LooseUnitMoveSnapshot(unit)).ToArray();
                var cars=state.AffectedCars.Select(car=>WholePartMoveProjection.SnapshotCar(state,catalog,car)).ToArray();
                return OrderedInventoryMoveResult.Completed(parts,engines,units,cars);
            }
            catch(Exception error){return OrderedInventoryMoveResult.Failure(error,failedIndex);}
            finally{state.Dispose();}
        }
    }
}
