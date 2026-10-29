#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    // Driver only: every domain branch is the unchanged independent I/J reference.
    // InventoryMoveRequest is shared immutable representation, never rule authority.
    internal static class LegacyOrderedInventoryMoveReference
    {
        internal static Exception Apply(PlayerProfile candidate,IReadOnlyList<InventoryMoveRequest> requests,out int failedIndex)
        {
            failedIndex=-1;
            try
            {
                if(requests==null)throw new ArgumentNullException(nameof(requests));
                var owned=new InventoryMoveRequest[requests.Count];
                for(int i=0;i<owned.Length;i++)owned[i]=requests[i];
                for(int i=0;i<owned.Length;i++)
                {
                    failedIndex=i;
                    var row=owned[i];
                    LegacyWholeEngineMoveReference.Move(candidate,row.AssetId,row.Destination,row.TargetId);
                }
                failedIndex=-1;
                return null;
            }
            catch(Exception error){return error;}
        }
    }
}
#endif
