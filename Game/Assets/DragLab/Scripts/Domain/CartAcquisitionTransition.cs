using System;
using System.Collections.Generic;

namespace DragLab.Domain.Mechanics
{
    // Acquisition receipts are invocation-local handoffs between shared rules and
    // the ordered row dispatcher. They contain no profile or catalogue objects.
    internal sealed class CartApplyAcquisitionReceipt<T>
    {
        readonly CartApplySupplyDelta[] supplyDeltas;
        internal readonly T Value;
        internal readonly CartApplyIdentityRef Identity;
        internal readonly string DefinitionId;
        internal readonly CartApplyQuote Quote;
        internal readonly int PaidAmount;
        internal CartApplySupplyDelta[] SupplyDeltas => (CartApplySupplyDelta[])supplyDeltas.Clone();

        internal CartApplyAcquisitionReceipt(T value,CartApplyIdentityRef identity,string definitionId,
            CartApplyQuote quote,int paidAmount,IEnumerable<CartApplySupplyDelta> stockDeltas,
            IEnumerable<CartApplySupplyDelta> holdDeltas)
        {
            Value=value;Identity=identity;DefinitionId=definitionId;Quote=quote;PaidAmount=paidAmount;
            var combined=new List<CartApplySupplyDelta>();
            if(stockDeltas!=null)combined.AddRange(stockDeltas);
            if(holdDeltas!=null)combined.AddRange(holdDeltas);
            supplyDeltas=combined.ToArray();
        }
    }

    internal sealed class CartApplyUsedAcquisition
    {
        internal readonly PartInstance Part;
        internal readonly LooseEngineElement Unit;
        internal readonly CartApplyIdentityRef Identity;
        internal readonly string DefinitionId;
        internal readonly int PaidAmount;
        internal bool IsUnit => Unit!=null;
        internal CartApplyUsedAcquisition(PartInstance part,LooseEngineElement unit,CartApplyIdentityRef identity,string definitionId,int paidAmount)
        {Part=part;Unit=unit;Identity=identity;DefinitionId=definitionId;PaidAmount=paidAmount;}
    }

    internal static class CartAcquisitionTransition
    {
        internal static CartApplyUsedAcquisition BuyUsed(ICartApplyState state,PartCartLine line,ValuationPolicy policy)
        {
            var stock=state.FindDealerStockReadOnly(line.assetId);
            if(stock==null||!(stock.IsPart||stock.IsUnit))throw new InvalidOperationException("Used stock unavailable or invalid.");
            int price=checked((int)Math.Ceiling(stock.PaidPrice*(1+(policy?.dealerResaleMarkup??.35)))+(policy?.dealerHandlingFee??1));
            state.Cash=state.Cash-price;
            state.RemoveDealerStock(stock);
            if(stock.IsPart)
            {
                var part=state.TransferDealerPart(stock);
                part.storageId=SharedInventory.Garage;
                state.AppendPart(part);
                string definitionId=part.definitionId;
                return new CartApplyUsedAcquisition(part,null,state.IdentityFromOwnedField(part.instanceId),definitionId,price);
            }
            var unit=state.TransferDealerUnit(stock);
            unit.storageId=SharedInventory.Garage;
            state.AppendUnit(unit);
            string unitDefinitionId=unit.definitionId;
            return new CartApplyUsedAcquisition(null,unit,state.IdentityFromOwnedField(unit.element.id),unitDefinitionId,price);
        }
    }
}
