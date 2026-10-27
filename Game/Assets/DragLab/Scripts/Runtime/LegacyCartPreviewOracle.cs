#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>
    /// Frozen S06D2B step-1 reference for the d541ad8 PartCart preview path.
    /// This file is editor-only and must never become a player fallback.
    /// Keep the original control flow and helper call order intact.
    /// </summary>
    public static class LegacyCartPreviewOracle
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR"),System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void RecordRuntimeJsonCall()=>PartCart.RecordRuntimeJsonCall();

        public static int Total(PlayerProfile p,string carId,IEnumerable<PartCartLine> source,ValuationPolicy policy=null,long now=0,bool validateBlocks=true,bool includeOwnerReservation=false)
        {
            var car=p.cars.Find(c=>c.instanceId==carId);EngineRebuilds.RequireAvailable(car);
            var lines=source.ToArray();if(lines.Length==0||lines.Length>100)throw new InvalidOperationException("Cart needs 1–100 distinct offers.");
            policy=policy??ValuationPolicy.Defaults();if(now==0)now=DragLab.Domain.RuntimeEntropy.UtcNow.Ticks;
            var ids=new HashSet<string>();var assets=new HashSet<string>();var positions=new HashSet<string>();var slots=new HashSet<PartCategory>();int total=0;PlayerProfile movePreview=null;
            Func<string,bool,int> salePrices=null;
            // An additive draft may reuse its owner's hold before Capture publishes the new rows.
            // Checkout still requires the exact captured cart and never opts into this preview path.
            var supply=new PartsMarket.Ledger(p,CartReservations.Matches(p,carId,lines)||includeOwnerReservation&&p.inventory.cart?.carId==carId);
            var moveSlots=new HashSet<string>();
            foreach(var line in lines) {
                var d=line==null?null:p.catalog.Find(line.definitionId);
                if(line==null||!Enum.IsDefined(typeof(CartAction),line.action)||line.quantity<1||line.quantity>99)throw new InvalidOperationException("Invalid cart action or quantity.");
                if(line.action==CartAction.SellPart||line.action==CartAction.SellUnit) {
                    if(line.quantity!=1||!assets.Add(line.assetId))throw new InvalidOperationException("Owned item may be used only once in a cart.");
                    if(salePrices==null)salePrices=Valuations.LoosePriceView(p,policy,"wholesale",now);
                    total=checked(total-salePrices(line.assetId,line.action==CartAction.SellUnit));continue;
                }
                if(line.action==CartAction.Move) {
                    if(line.quantity!=1||!assets.Add(line.assetId))throw new InvalidOperationException("Duplicate move or invalid quantity.");
                    if(movePreview==null){RecordRuntimeJsonCall();RecordRuntimeJsonCall();movePreview=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));MechanicsOwnership.Ensure(movePreview,p.catalog);}SharedInventory.Move(movePreview,line.assetId,line.destination,line.destinationId);
                    var moved=p.parts.Find(i=>i.instanceId==line.assetId);if(moved!=null&&line.destination==InventoryKind.Car&&line.destinationId==carId&&!slots.Add(p.catalog.Find(moved.definitionId).category))throw new InvalidOperationException("Conflicting slot installations.");
                    if(moved!=null&&line.destination!=InventoryKind.Storage&&!moveSlots.Add(line.destination+"/"+line.destinationId+"/"+p.catalog.Find(moved.definitionId).category))throw new InvalidOperationException("Conflicting destination slot installations.");
                    continue;
                }
                if(d==null||d.price<0||line.install&&!p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)||p.reputation<d.reputationRequired)throw new InvalidOperationException("Unavailable/incompatible cart offer or reputation.");
                int price=InventoryPricing.NewSet(d);bool unit=line.action==CartAction.NewUnits;
                if(line.action==CartAction.BuyUsed) {
                    var stock=p.inventory.dealer.Find(s=>s.Id==line.assetId);
                    if(stock==null||!(stock.IsPart||stock.IsUnit)||stock.DefinitionId!=d.id||line.quantity!=1||!assets.Add(line.assetId))throw new InvalidOperationException("Used stock unavailable or used twice.");
                    unit=stock.IsUnit;if(unit&&line.role!=stock.unit.element.role)throw new InvalidOperationException("Used unit role changed.");price=InventoryPricing.BuyUsed(stock,policy);
                } else if(!ids.Add(line.action+"/"+d.id+"/"+line.role))throw new InvalidOperationException("Duplicate new offer.");
                if(unit) {
                    if(!InventoryUnits.Roles(p,car,d).Contains(line.role))throw new InvalidOperationException("This offer has no individually modeled units of that type.");
                    if(line.action==CartAction.NewUnits)price=InventoryPricing.NewUnit(d,InventoryUnits.Count(p,car,d));
                    if(line.install){if(line.positions==null||line.positions.Count==0||line.positions.Count>line.quantity||line.positions.Any(s=>!InventoryUnits.Positions(p,car,d.id,line.role).Contains(s)||!positions.Add(s)))throw new InvalidOperationException("Select unique fitted positions for installed units, matching the fitted specification. Unselected extras remain loose.");}
                } else if(line.install&&!slots.Add(d.category))throw new InvalidOperationException("Only one cart offer can install in each slot. Other offers may be bought as spares.");
                total=checked(total+(line.action==CartAction.BuyUsed?checked(price*line.quantity):supply.Take(d,unit?1:InventoryUnits.Count(p,car,d),price,line.quantity).cost));
            }
            foreach(var category in slots)if(lines.Any(l=>l.install&&(l.action==CartAction.NewUnits||l.action==CartAction.BuyUsed&&p.inventory.dealer.Any(s=>s.Id==l.assetId&&s.IsUnit))&&p.catalog.Find(l.definitionId).category==category))throw new InvalidOperationException("Choose a complete set or individual replacements for a slot, not both.");
            if(validateBlocks){
                var blocks=lines.Where(l=>p.catalog.Find(l.definitionId)?.category==PartCategory.CylinderBlock&&(l.install||l.action==CartAction.Move&&l.destination==InventoryKind.Car)).Where(l=>{var target=p.cars.Find(c=>c.instanceId==(l.action==CartAction.Move?l.destinationId:carId));return target==null||!MechanicsOwnership.Definitions(p,target).TryGetValue(PartCategory.CylinderBlock,out var old)||old!=l.definitionId;}).ToArray();
                if(blocks.Length>0){if(blocks.Any(l=>!l.preparedBlock))throw new InvalidOperationException("Prepare the block and its required components before installation.");var preview=PreviewProfile(p,car,lines);foreach(var targetId in blocks.Select(l=>l.action==CartAction.Move?l.destinationId:carId).Distinct()){var result=MechanicsOwnership.Resolve(preview,preview.cars.Find(c=>c.instanceId==targetId),preview.catalog);if(!result.Drivable)throw new InvalidOperationException("Block preparation is incomplete: "+string.Join("; ",result.errors));}}
            }
            return total;
        }

        public static int DeliveryFee(PlayerProfile p,string carId,IEnumerable<PartCartLine> lines)
        {
            var car=p.cars.Find(c=>c.instanceId==carId);var ledger=new PartsMarket.Ledger(p,CartReservations.Matches(p,carId,lines));int fee=0;
            foreach(var row in lines.Where(l=>l.action==CartAction.NewSet||l.action==CartAction.NewUnits)){var d=p.catalog.Find(row.definitionId);int count=InventoryUnits.Count(p,car,d);bool unit=row.action==CartAction.NewUnits;fee=checked(fee+ledger.Take(d,unit?1:count,unit?InventoryPricing.NewUnit(d,count):d.price,row.quantity).fee);}return fee;
        }

        public static void Apply(PlayerProfile p,string carId,IEnumerable<PartCartLine> lines,bool preview=false,ValuationPolicy policy=null)
        {
            foreach(var line in lines) {
                if(line.action==CartAction.SellPart||line.action==CartAction.SellUnit)continue;
                if(line.action==CartAction.Move){SharedInventory.Move(p,line.assetId,line.destination,line.destinationId);continue;}
                for(int i=0;i<line.quantity;i++) {
                    if(line.action==CartAction.NewSet){PartOperations.Buy(p,p.catalog,carId,line.definitionId,line.install&&i==0);continue;}
                    LooseEngineElement unit;
                    if(line.action==CartAction.NewUnits)unit=InventoryUnits.Buy(p,carId,line.definitionId,line.role);
                    else {var stock=p.inventory.dealer.Find(s=>s.Id==line.assetId);if(stock==null||!(stock.IsPart||stock.IsUnit))throw new InvalidOperationException("Used stock unavailable or invalid.");p.cash-=InventoryPricing.BuyUsed(stock,policy);p.inventory.dealer.Remove(stock);if(stock.IsPart){stock.part.storageId=SharedInventory.Garage;p.parts.Add(stock.part);if(line.install)SharedInventory.Move(p,stock.part.instanceId,InventoryKind.Car,carId);continue;}unit=stock.unit;unit.storageId=SharedInventory.Garage;p.looseEngineElements.Add(unit);}
                    if(line.install&&i<line.positions.Count)InventoryUnits.Install(p,carId,unit.element.id,line.positions[i]);
                }
            }
        }

        public static PlayerProfile PreviewProfile(PlayerProfile p,OwnedCar car,IEnumerable<PartCartLine> lines)
        {
            RecordRuntimeJsonCall();RecordRuntimeJsonCall();var copy=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));MechanicsOwnership.Ensure(copy,p.catalog);copy.cash=int.MaxValue/2;
            var captured=lines.ToArray();bool ownsReservation=CartReservations.SameCart(copy,car.instanceId,captured);copy.inventory.usingCartReservation=CartReservations.Matches(copy,car.instanceId,captured);try{Apply(copy,car.instanceId,captured,true);}finally{copy.inventory.usingCartReservation=false;}if(ownsReservation)CartReservations.Clear(copy);MechanicsOwnership.Ensure(copy,p.catalog);return copy;
        }
    }
}
#endif
