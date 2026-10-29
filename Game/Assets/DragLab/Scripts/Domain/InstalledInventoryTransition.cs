using System;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    internal static class InstalledInventoryTransition
    {
        internal static void MoveAfterPreamble(InstalledPartTransitionState state,MechanicalCatalog catalog,
            PartMovePreamble preamble,InventoryKind target,string targetId,PartMoveMemberPolicy memberPolicy)
        {
            switch(preamble.Kind)
            {
                case PartMoveAssetKind.Unit:
                {
                    var unit=state.FindLooseUnit(preamble.AssetId);
                    if(target!=InventoryKind.Storage)throw new InvalidOperationException("Choose a fitted position to install an individual unit.");
                    state.MarkUnitChanged(unit);unit.StorageId=targetId;
                    return;
                }
                case PartMoveAssetKind.Engine:
                    InstalledEngineTransition.MoveAfterPreamble(state,catalog,preamble,target,targetId);
                    return;
                case PartMoveAssetKind.Part:
                    InstalledPartTransition.MovePartAfterPreamble(state,catalog,preamble,target,targetId,memberPolicy);
                    return;
                default: throw new InvalidOperationException("Unknown inventory asset kind.");
            }
        }
    }
}
