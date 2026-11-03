using System;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    internal static class InstalledInventoryTransition
    {
        internal static void MoveAfterPreamble(InstalledPartTransitionState state,ICartCatalogRead catalog,
            PartMovePreamble preamble,InventoryKind target,string targetId,PartMoveMemberPolicy memberPolicy)
        {
            switch(preamble.Kind)
            {
                case PartMoveAssetKind.Unit:
                {
                    if(target!=InventoryKind.Storage)throw new InvalidOperationException("Choose a fitted position to install an individual unit.");
                    if(state is ICartApplyState applyState)
                    {
                        var source=applyState.FindUnit(preamble.Identity);
                        var unit=applyState.EditUnit(source);
                        applyState.MarkChangedUnit(unit);
                        unit.storageId=targetId;
                    }
                    else
                    {
                        var unit=state.FindLooseUnit(preamble.AssetId);
                        state.MarkUnitChanged(unit);unit.StorageId=targetId;
                    }
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
