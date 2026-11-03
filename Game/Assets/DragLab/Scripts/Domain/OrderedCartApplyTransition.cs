using System;
using System.Collections.Generic;

namespace DragLab.Domain.Mechanics
{
    internal static class OrderedCartApplyTransition
    {
        internal static void ApplyLive(PlayerProfile owner,string carId,IEnumerable<PartCartLine> rows,bool preview=false,ValuationPolicy policy=null)
        {
            using(var state=new LiveCartApplyState(owner))ApplyRows(state,carId,rows,preview,policy);
        }

        internal static void ApplyRows(ICartApplyState state,string carId,IEnumerable<PartCartLine> source,bool preview,ValuationPolicy policy)
        {
            int rowIndex=0;
            using(IEnumerator<PartCartLine> rows=source.GetEnumerator())
            {
                while(true)
                {
                    state.BeginRow(rowIndex);
                    if(!rows.MoveNext())break;
                    var line=rows.Current;
                    CartAction action=line.action;
                    if(action==CartAction.SellPart||action==CartAction.SellUnit)
                    {
                        if(state.CaptureEffects)state.RecordEffect(new CartApplyRowEffect(rowIndex,-1,action,CartApplyBranch.SkippedSale));
                        rowIndex++;
                        state.CompleteRow(rowIndex);
                        continue;
                    }
                    if(action==CartAction.Move)
                    {
                        SharedInventory.Move(state,line.assetId,line.destination,line.destinationId);
                        if(state.CaptureEffects)state.RecordEffect(new CartApplyRowEffect(rowIndex,-1,action,CartApplyBranch.Move,
                            assetId:line.assetId,installTargetId:line.destinationId));
                        rowIndex++;
                        state.CompleteRow(rowIndex);
                        continue;
                    }
                    for(int quantityOrdinal=0;quantityOrdinal<line.quantity;quantityOrdinal++)
                    {
                        if(action==CartAction.NewSet)
                        {
                            PartOperations.BuyPart(state,carId,line.definitionId,line.install&&quantityOrdinal==0,rowIndex,quantityOrdinal,out var receipt);
                            if(state.CaptureEffects)state.RecordEffect(new CartApplyRowEffect(rowIndex,quantityOrdinal,action,CartApplyBranch.NewSet,
                                receipt.DefinitionId,null,receipt.Identity,line.install&&quantityOrdinal==0?carId:null,
                                receipt.PaidAmount,receipt.Quote,receipt.SupplyDeltas));
                            continue;
                        }
                        if(action==CartAction.NewUnits)
                        {
                            var unit=InventoryUnits.BuyUnit(state,carId,line.definitionId,line.role,rowIndex,quantityOrdinal,out var unitIdentity,out var receipt);
                            bool installed=false;
                            if(line.install&&quantityOrdinal<line.positions.Count)
                            {
                                string position=line.positions[quantityOrdinal];
                                InventoryUnits.InstallUnit(state,carId,unit,unitIdentity,position,rowIndex,quantityOrdinal,state.MemberPolicy);
                                installed=true;
                            }
                            if(state.CaptureEffects)state.RecordEffect(new CartApplyRowEffect(rowIndex,quantityOrdinal,action,CartApplyBranch.NewUnits,
                                receipt.DefinitionId,null,receipt.Identity,installed?carId:null,
                                receipt.PaidAmount,receipt.Quote,receipt.SupplyDeltas));
                            continue;
                        }
                        var used=CartAcquisitionTransition.BuyUsed(state,line,policy);
                        string installTarget=null;
                        if(used.Part!=null)
                        {
                            if(line.install)
                            {
                                SharedInventory.Move(state,used.Identity,InventoryKind.Car,carId);
                                installTarget=carId;
                            }
                        }
                        else if(line.install&&quantityOrdinal<line.positions.Count)
                        {
                            string position=line.positions[quantityOrdinal];
                            InventoryUnits.InstallUnit(state,carId,used.Unit,used.Identity,position,rowIndex,quantityOrdinal,state.MemberPolicy);
                            installTarget=carId;
                        }
                        if(state.CaptureEffects)state.RecordEffect(new CartApplyRowEffect(rowIndex,quantityOrdinal,action,CartApplyBranch.BuyUsed,
                            used.DefinitionId,line.assetId,used.Identity,installTarget,used.PaidAmount));
                    }
                    rowIndex++;
                    state.CompleteRow(rowIndex);
                }
            }
        }
    }
}
