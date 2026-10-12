using System;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        internal void PrepareNativeCart(){if(Build!=null)EnsureCartOwner();}
        internal void RemoveNativeCartLine(PartCartLine line){cart.Remove(line);CartChanged();}
        internal void ClearNativeCart(){cart.Clear();CartChanged();}
        internal void SetNativeCartInstall(PartCartLine line,bool install){SetCartInstallation(line,install);}
        internal int NativeCartTotal()=>cart.Count==0?0:PartCart.Total(profile,Build.instanceId,cart,PricingPolicy);
        internal int NativeUsedPrice(DealerStock stock)=>InventoryPricing.BuyUsed(stock,PricingPolicy);
        internal int NativeSalePrice(string id,bool unit)=>InventoryPricing.Sell(profile,id,unit,PricingPolicy,"wholesale",DateTime.UtcNow.Ticks).Total;
        internal bool NativeBlockPreparationPending(PartDefinition definition)=>definition?.category==PartCategory.CylinderBlock&&(!MechanicsOwnership.Definitions(profile,Build).TryGetValue(PartCategory.CylinderBlock,out var installed)||installed!=definition.id);
        internal bool InstallNativeUnit(string id,string position,long revision)=>CommitParts(revision,p=>InventoryUnits.Install(p,p.SelectedCar.instanceId,id,position));
        internal PartCartQuote QuoteNativeCart()=>PartCart.Quote(profile,Build.instanceId,cart,DateTime.UtcNow.Ticks,Costs.quoteSeconds,PricingPolicy);
        internal bool CommitNativeCart(PartCartQuote quote)
        {
            if(!CommitParts(quote.revision,p=>PartCart.Checkout(p,quote,DateTime.UtcNow.Ticks,PricingPolicy)))return false;
            cart.Clear();CartChanged();return true;
        }
        internal void StageNativePart(NativePartEntry entry,bool install,string position=null)
        {
            PrepareNativeCart();
            if(entry.Source=="New") {unitOffers.Remove(entry.Definition.id);var existing=cart.Find(l=>l.action==CartAction.NewSet&&l.definitionId==entry.Definition.id);if(install&&existing!=null)SetCartInstallation(existing,true);else AddToCart(entry.Definition.id,install);return;}
            var line=new PartCartLine{definitionId=entry.Definition.id,assetId=entry.AssetId,quantity=1,install=install};
            if(entry.Source=="Used")line.action=CartAction.BuyUsed;
            else if(entry.Unit!=null){line.action=CartAction.Move;throw new InvalidOperationException("Individual units need a fitted position.");}
            else{line.action=CartAction.Move;line.install=false;line.destination=install?InventoryKind.Car:InventoryKind.Storage;line.destinationId=install?Build.instanceId:SharedInventory.Garage;}
            if(entry.Unit!=null){line.role=entry.Unit.element.role;if(install){if(string.IsNullOrEmpty(position))throw new InvalidOperationException("Choose a fitted position.");line.positions.Add(position);}}
            StageInventory(line);
            if(install&&entry.Unit==null&&line.action==CartAction.BuyUsed)SetCartInstallation(line,true);
        }
    }
}
