using System;
using System.Collections.Generic;

namespace DragLab.Domain.Mechanics
{
    internal static class OrderedCartApplyProjection
    {
        internal static OrderedCartApplyResult Evaluate(PlayerProfile owner,string carId,IReadOnlyList<PartCartLine> rows,
            ValuationPolicy policy=null,bool preview=false)
            =>EvaluateCore(owner,carId,rows,policy,preview,null,null);

        static OrderedCartApplyResult EvaluateCore(PlayerProfile owner,string carId,IReadOnlyList<PartCartLine> rows,
            ValuationPolicy policy,bool preview,Func<int,string> symbolProposer,Action<CartApplyWorkingSet> afterDispose)
        {
            var state=new CartApplyWorkingSet(owner,symbolProposer);OrderedCartApplyResult result=null;
            try
            {
                try
                {
                    if(rows==null)throw new ArgumentNullException(nameof(rows));
                    OrderedCartApplyTransition.ApplyRows(state,carId,(IEnumerable<PartCartLine>)rows,preview,policy);
                }
                catch(Exception error)
                {
                    result=OrderedCartApplyResult.Failure(error,rows==null?-1:state.ErrorIndex);
                }
                if(result==null)
                    result=new OrderedCartApplyResult(true,state.HasCashRead,state.CashBeforeValue,state.CashAfterValue,state.CashDeltaValue,
                        state.Effects,state.ChangedPartSnapshots(),state.ChangedEngineSnapshots(),state.ChangedUnitSnapshots(),
                        state.ChangedDealerSnapshots(),state.ChangedCarSnapshots(),state.ChangedOwnerSnapshots(),state.SupplyDeltas(),state.IdentityMap());
            }
            finally
            {
                state.Dispose();
                afterDispose?.Invoke(state);
            }
            return result;
        }

#if UNITY_EDITOR
        internal static OrderedCartApplyResult EvaluateForTests(PlayerProfile owner,string carId,IReadOnlyList<PartCartLine> rows,
            ValuationPolicy policy,bool preview,Func<int,string> proposePrivateSymbol,Action<CartApplyWorkingSet> inspectAfterDispose)
            =>EvaluateCore(owner,carId,rows,policy,preview,proposePrivateSymbol,inspectAfterDispose);
#endif
    }
}
