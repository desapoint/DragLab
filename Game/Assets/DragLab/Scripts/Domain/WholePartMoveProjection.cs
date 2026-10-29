using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    internal enum PartMoveOwnerKind { Car, Engine }

    internal sealed class WholePartMoveOwnerSnapshot
    {
        internal readonly PartMoveOwnerKind Kind;
        internal readonly string Id,VehicleId,VehicleDefinitionId,EngineAssemblyId,ParentVehicleId,GarageJobId;
        internal readonly bool HasRebuildJob,RebuildActive;
        internal readonly SlotBinding[] Bindings;
        internal readonly PartInstance[] Parts;
        internal readonly Dictionary<PartCategory,string> Definitions;
        internal readonly Tune Tune;
        internal readonly FuelState Fuel;
        internal readonly FluidState Fluids;
        internal readonly InspectionBuild Inspection;
        internal readonly bool Materialized;
        internal readonly string BuildErrorType,BuildErrorMessage;

        internal WholePartMoveOwnerSnapshot(PartMoveCarState car,SlotBinding[] bindings,PartInstance[] parts,
            Dictionary<PartCategory,string> definitions,InspectionBuild inspection,bool materialized,string errorType,string errorMessage)
        {
            Kind=PartMoveOwnerKind.Car;Id=car.Id;VehicleId=car.VehicleId;EngineAssemblyId=car.EngineAssemblyId;
            GarageJobId=car.GarageJobId;HasRebuildJob=car.HasRebuildJob;RebuildActive=car.RebuildActive;
            Bindings=bindings;Parts=parts;Definitions=definitions;Tune=car.OwnedTuneCopy;
            Fuel=car.Fuel?.Copy();Fluids=car.Fluids?.Copy();Inspection=inspection;Materialized=materialized;
            BuildErrorType=errorType;BuildErrorMessage=errorMessage;
        }
        internal WholePartMoveOwnerSnapshot(PartMoveEngineState engine,SlotBinding[] bindings,PartInstance[] parts,
            Dictionary<PartCategory,string> definitions,bool materialized,string errorType,string errorMessage)
        {
            Kind=PartMoveOwnerKind.Engine;Id=engine.Id;VehicleDefinitionId=engine.VehicleDefinitionId;
            ParentVehicleId=engine.ParentVehicleId;Bindings=bindings;Parts=parts;
            Definitions=definitions;Tune=engine.Tune?.Copy();Materialized=materialized;
            BuildErrorType=errorType;BuildErrorMessage=errorMessage;
        }
    }

    internal sealed class WholePartMoveResult
    {
        internal bool Success {get;private set;}
        internal string ErrorType {get;private set;}
        internal string ErrorMessage {get;private set;}
        internal PartInstance MovedPart {get;private set;}
        internal PartInstance DisplacedPart {get;private set;}
        internal PartInstance[] ChangedParts {get;private set;}
        internal WholePartMoveOwnerSnapshot[] Owners {get;private set;}

        internal static WholePartMoveResult Failure(Exception error)
            =>new WholePartMoveResult{Success=false,ErrorType=error?.GetType().FullName,ErrorMessage=error?.Message};
        internal static WholePartMoveResult Completed(PartInstance moved,PartInstance displaced,
            PartInstance[] changed,WholePartMoveOwnerSnapshot[] owners)
            =>new WholePartMoveResult{Success=true,MovedPart=moved,DisplacedPart=displaced,
                ChangedParts=changed,Owners=owners};
    }

    // Explicit test/next-slice seam. It owns no persistent state and never publishes
    // the partially-mutated private graph on failure.
    internal static class WholePartMoveProjection
    {
        internal static WholePartMoveResult Evaluate(PlayerProfile profile,string assetId,
            InventoryKind destination,string destinationId,MechanicalCatalog catalog)
        {
            var state=new ProfileInstalledPartTransitionState(profile,true);
            try
            {
                var preamble=InstalledPartTransition.Preamble(state,assetId,destination,destinationId);
                if(preamble.Kind!=PartMoveAssetKind.Part)
                    throw new NotSupportedException("Whole-part projection accepts physical part assets only.");
                var effect=InstalledPartTransition.MovePartAfterPreamble(state,catalog,preamble,destination,destinationId,
                    PartMoveMemberPolicy.ProjectionRequireExisting);
                var owners=new List<WholePartMoveOwnerSnapshot>();
                foreach(var owner in effect.Owners)
                {
                    if(owner.Car!=null)owners.Add(SnapshotCar(state,catalog,owner.Car));
                    else owners.Add(SnapshotEngine(state,owner.Engine));
                }
                return WholePartMoveResult.Completed(effect.MovedPart?.Copy(),effect.DisplacedPart?.Copy(),
                    effect.ChangedParts.Select(part=>part?.Copy()).ToArray(),owners.ToArray());
            }
            catch(Exception error){return WholePartMoveResult.Failure(error);}
            finally{state.Dispose();}
        }

        internal static WholePartMoveOwnerSnapshot SnapshotCar(ProfileInstalledPartTransitionState state,
            MechanicalCatalog catalog,PartMoveCarState car)
        {
            SlotBinding[] bindings=null;
            PartInstance[] parts=null;Dictionary<PartCategory,string> definitions=null;InspectionBuild inspection=null;
            bool materialized=false;string buildErrorType=null,buildErrorMessage=null;
            try
            {
                bindings=car.BindingCopies;
                if(bindings==null)throw new InvalidOperationException("Affected car bindings are unavailable for inspection materialization.");
                var components=new List<InstalledBuildComponent>(bindings.Length);
                bool complete=true;
                foreach(var binding in bindings)
                {
                    string id=binding?.partInstanceId;
                    var part=string.IsNullOrWhiteSpace(id)?null:state.FindPart(id);
                    if(part==null)complete=false;
                    components.Add(new InstalledBuildComponent(binding?.slot,part,id));
                }
                if(!complete)throw new InvalidOperationException("Affected car contains an unresolved installed-part binding.");
                parts=components.Select(component=>component.Part?.Copy()).ToArray();
                definitions=new Dictionary<PartCategory,string>(state.CarDefinitions(car));
                var input=ConcreteInstalledBuildInput.FromTransitionFacts(car.Id,car.VehicleId,car.EngineAssemblyId,
                    car.GarageJobId,car.HasRebuildJob,car.OwnedTuneCopy,definitions,components,car.Fuel,car.Fluids);
                inspection=input.Inspection(catalog);materialized=true;
            }
            catch(Exception error){parts=null;definitions=null;inspection=null;buildErrorType=error.GetType().FullName;buildErrorMessage=error.Message;}
            return new WholePartMoveOwnerSnapshot(car,bindings,parts,definitions,inspection,materialized,buildErrorType,buildErrorMessage);
        }

        static WholePartMoveOwnerSnapshot SnapshotEngine(ProfileInstalledPartTransitionState state,PartMoveEngineState engine)
        {
            var bindings=engine.BindingCopies;PartInstance[] parts=null;Dictionary<PartCategory,string> definitions=null;
            bool materialized=false;string buildErrorType=null,buildErrorMessage=null;
            try
            {
                var captured=new List<PartInstance>(bindings.Length);
                foreach(var binding in bindings)
                {
                    var part=binding==null||string.IsNullOrWhiteSpace(binding.partInstanceId)?null:state.FindPart(binding.partInstanceId);
                    if(part==null)throw new InvalidOperationException("Affected engine contains an unresolved installed-part binding.");
                    captured.Add(part.Copy());
                }
                parts=captured.ToArray();definitions=new Dictionary<PartCategory,string>(state.EngineDefinitions(engine));materialized=true;
            }
            catch(Exception error){parts=null;definitions=null;buildErrorType=error.GetType().FullName;buildErrorMessage=error.Message;}
            return new WholePartMoveOwnerSnapshot(engine,bindings,parts,definitions,materialized,buildErrorType,buildErrorMessage);
        }
    }
}
