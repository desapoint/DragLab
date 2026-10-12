using System;
using System.Linq;
using System.Collections.Generic;
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
        bool ValidateNativeUnitPlan(PartCartLine current,PartDefinition definition,string role,int quantity,List<string> positions)
        {
            var available=InventoryUnits.Positions(profile,Build,definition.id,role);
            if(positions.Count==0||positions.Count>quantity||positions.Any(p=>!available.Contains(p))){notice="Select fitted positions matching this specification and quantity.";return false;}
            if(cart.Any(l=>l!=current&&l.install&&((l.action==CartAction.NewSet||l.action==CartAction.BuyUsed&&!IsUnit(l))&&profile.catalog.Find(l.definitionId)?.category==definition.category||l.positions.Any(positions.Contains)))){notice="Another cart installation uses this slot or position.";return false;}
            return true;
        }
        internal bool SetNativeCartUnitPositions(PartCartLine line,IEnumerable<string> targets)
        {
            if(!cart.Contains(line)||line.action!=CartAction.NewUnits){notice="Cart line changed; review it again.";return false;}
            var selected=targets.Distinct().ToList();if(!ValidateNativeUnitPlan(line,profile.catalog.Find(line.definitionId),line.role,line.quantity,selected))return false;
            line.positions=selected;line.install=true;notice="";CartChanged();return true;
        }
        internal bool StageNativePurchase(string definition,string role,int quantity,bool install,IEnumerable<string> targets=null)
        {
            PrepareNativeCart();var d=profile.catalog.Find(definition);var positions=(targets??Enumerable.Empty<string>()).Distinct().ToList();
            if(d==null||!profile.catalog.Fits(Selected.spec,d)||profile.reputation<d.reputationRequired||quantity<1||quantity>99){notice="Offer or quantity unavailable.";return false;}
            bool unit=role!=null;
            if(unit&&!InventoryUnits.Roles(profile,Build,d).Contains(role)){notice="Individual role unavailable.";return false;}
            if(install&&NativeBlockPreparationPending(d)){notice="Prepare a complete engine installation before installing this block.";return false;}
            var action=unit?CartAction.NewUnits:CartAction.NewSet;var existing=cart.Find(l=>l.action==action&&l.definitionId==definition&&l.role==role);
            if(existing!=null&&existing.quantity+quantity>99){notice="A cart line holds at most 99 sets or units.";return false;}
            if(existing==null&&cart.Count>=100){notice="Cart holds at most 100 distinct lines.";return false;}
            if(unit&&install&&!ValidateNativeUnitPlan(existing,d,role,quantity,positions))return false;
            var line=existing??new PartCartLine{definitionId=definition,action=action,role=role,quantity=0};
            line.quantity+=quantity;if(existing==null)cart.Add(line);
            if(install){if(unit){line.positions=line.positions.Union(positions).ToList();line.install=true;}else SetCartInstallation(line,true);}
            if(unit)unitOffers[definition]=role;else unitOffers.Remove(definition);
            notice="";CartChanged();return true;
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
