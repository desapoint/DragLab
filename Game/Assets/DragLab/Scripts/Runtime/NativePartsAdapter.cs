using System;
using System.Linq;
using System.Collections.Generic;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        internal void PrepareNativeCart(){if(Build!=null)EnsureCartOwner();}
        internal void RemoveNativeCartLine(PartCartLine line){if(line.preparedBlock&&line.preparationOwner!=null)cart.RemoveAll(l=>l.preparationOwner==line.preparationOwner);else cart.Remove(line);CartChanged();}
        internal void ClearNativeCart(){cart.Clear();CartChanged();}
        internal void SetNativeCartInstall(PartCartLine line,bool install){SetCartInstallation(line,install);}
        internal int NativeCartTotal()=>cart.Count==0?0:PartCart.Total(profile,Build.instanceId,cart,PricingPolicy);
        internal int NativeUsedPrice(DealerStock stock)=>InventoryPricing.BuyUsed(stock,PricingPolicy);
        internal int NativeSalePrice(string id,bool unit)=>InventoryPricing.Sell(profile,id,unit,PricingPolicy,"wholesale",DragLab.Domain.RuntimeEntropy.UtcNow.Ticks).Total;
        internal bool NativeBlockPreparationPending(PartDefinition definition)=>definition?.category==PartCategory.CylinderBlock&&(!MechanicsOwnership.Definitions(profile,Build).TryGetValue(PartCategory.CylinderBlock,out var installed)||installed!=definition.id);
        internal bool InstallNativeUnit(string id,string position,long revision)=>CommitParts(revision,p=>InventoryUnits.Install(p,p.SelectedCar.instanceId,id,position));
        internal PartCartQuote QuoteNativeCart()=>PartCart.Quote(profile,Build.instanceId,cart,DragLab.Domain.RuntimeEntropy.UtcNow.Ticks,Costs.quoteSeconds,PricingPolicy);
        internal bool CommitNativeCart(PartCartQuote quote)
        {
            if(!CommitParts(quote.revision,p=>PartCart.Checkout(p,quote,DragLab.Domain.RuntimeEntropy.UtcNow.Ticks,PricingPolicy)))return false;
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
            if(d==null||install&&!profile.catalog.Fits(Selected.spec,d)||profile.reputation<d.reputationRequired||quantity<1||quantity>99){notice="Offer or quantity unavailable.";return false;}
            bool unit=role!=null;
            if(unit&&!InventoryUnits.Roles(profile,Build,d).Contains(role)){notice="Individual role unavailable.";return false;}
            if(install&&NativeBlockPreparationPending(d)){notice="Prepare a complete engine installation before installing this block.";return false;}
            var action=unit?CartAction.NewUnits:CartAction.NewSet;var existing=cart.Find(l=>l.action==action&&l.definitionId==definition&&l.role==role);
            if(existing!=null&&existing.quantity+quantity>99){notice="A cart line holds at most 99 sets or units.";return false;}
            if(existing==null&&cart.Count>=100){notice="Cart holds at most 100 distinct lines.";return false;}
            if(unit&&install&&!ValidateNativeUnitPlan(existing,d,role,quantity,positions))return false;
            var line=existing??new PartCartLine{definitionId=definition,action=action,role=role,quantity=0};
            if(d.category!=PartCategory.CylinderBlock)line.preparationOwner=null;
            line.quantity+=quantity;if(existing==null)cart.Add(line);
            if(install){if(unit){line.positions=line.positions.Union(positions).ToList();line.install=true;}else SetCartInstallation(line,true);}
            if(unit)unitOffers[definition]=role;else unitOffers.Remove(definition);
            notice="";CartChanged();return true;
        }
        internal int NativeCartLinePrice(PartCartLine line)
        {
            var d=profile.catalog.Find(line.definitionId);
            if(line.action==CartAction.SellPart||line.action==CartAction.SellUnit)return -NativeSalePrice(line.assetId,line.action==CartAction.SellUnit);
            if(line.action==CartAction.Move)return 0;
            if(line.action==CartAction.BuyUsed){var stock=profile.inventory.dealer.Find(s=>s.Id==line.assetId);if(stock==null)throw new InvalidOperationException("Used stock unavailable.");return NativeUsedPrice(stock);}
            if(d==null)throw new InvalidOperationException("Offer unavailable.");
            var ledger=new PartsMarket.Ledger(profile,CartReservations.Matches(profile,Build.instanceId,cart));foreach(var row in cart){if(row.action!=CartAction.NewSet&&row.action!=CartAction.NewUnits)continue;var definition=profile.catalog.Find(row.definitionId);int count=InventoryUnits.Count(profile,Build,definition);bool unit=row.action==CartAction.NewUnits;var q=ledger.Take(definition,unit?1:count,unit?InventoryPricing.NewUnit(definition,count):definition.price,row.quantity);if(row==line)return q.cost;}throw new InvalidOperationException("Cart line changed.");
        }
        internal bool EditNativeCartPurchase(PartCartLine current,string expected,string carId,bool individual,string role,int quantity,bool install,IEnumerable<string> targets)
        {
            if(Build==null||Build.instanceId!=carId){notice="Selected car changed; review the cart again.";return false;}
            PrepareNativeCart();
            if(!cart.Contains(current)||UnityEngine.JsonUtility.ToJson(current)!=expected){notice="Cart changed; review the row again.";return false;}
            var d=profile.catalog.Find(current.definitionId);bool fresh=current.action==CartAction.NewSet||current.action==CartAction.NewUnits;
            if(d==null||install&&!profile.catalog.Fits(Selected.spec,d)||profile.reputation<d.reputationRequired||quantity<1||quantity>99||!fresh&&current.action!=CartAction.BuyUsed){notice="Offer or quantity unavailable.";return false;}
            var stock=fresh?null:profile.inventory.dealer.Find(s=>s.Id==current.assetId);
            if(!fresh&&(stock==null||stock.DefinitionId!=d.id||quantity!=1||individual!=stock.IsUnit||individual&&role!=stock.unit.element.role)){notice="Used stock changed; review it again.";return false;}
            var candidate=current.Copy();candidate.action=fresh?individual?CartAction.NewUnits:CartAction.NewSet:CartAction.BuyUsed;candidate.role=individual?role:null;candidate.quantity=quantity;candidate.install=install;candidate.positions=individual&&install?targets.Distinct().ToList():new List<string>();
            if(individual&&!InventoryUnits.Roles(profile,Build,d).Contains(role)){notice="Individual role unavailable.";return false;}
            if(cart.Any(l=>l!=current&&l.action==candidate.action&&l.definitionId==d.id&&l.role==candidate.role&&fresh)){notice="Another row already buys this offer and role. Edit that row or remove it first.";return false;}
            if(install&&NativeBlockPreparationPending(d)&&!current.preparedBlock){notice="Prepare a complete engine installation before installing this block.";return false;}
            if(current.preparedBlock){var review=cart.Where(l=>l!=current&&(install||l.preparationOwner!=current.preparationOwner)).Select(l=>l.Copy()).ToList();review.Add(candidate);try{PartCart.Total(profile,carId,review,PricingPolicy);}catch(Exception e){notice=e.Message;return false;}if(!install){cart.RemoveAll(l=>l!=current&&l.preparationOwner==current.preparationOwner);current.preparedBlock=false;current.preparationOwner=null;}}
            if(individual&&install&&!ValidateNativeUnitPlan(current,d,role,quantity,candidate.positions))return false;
            if(install&&!individual&&cart.Any(l=>l!=current&&(l.install||l.action==CartAction.Move&&l.destination==InventoryKind.Car&&l.destinationId==carId)&&profile.catalog.Find(l.definitionId)?.category==d.category)){notice="Another row installs in this slot. Store or remove it first.";return false;}
            current.action=candidate.action;current.role=candidate.role;current.quantity=candidate.quantity;current.install=candidate.install;current.positions=candidate.positions;
            if(d.category!=PartCategory.CylinderBlock)current.preparationOwner=null;
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
