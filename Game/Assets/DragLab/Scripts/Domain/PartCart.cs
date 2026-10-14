using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DragLab.Domain.Mechanics
{
    public enum CartAction { NewSet, NewUnits, BuyUsed, SellPart, SellUnit, Move }
    [Serializable] public sealed class PartCartLine
    {
        public string definitionId;
        public int quantity=1;
        public bool install;
        public bool preparedBlock;
        public string preparationOwner;
        public CartAction action;
        public string assetId, role, destinationId;
        public InventoryKind destination = InventoryKind.Storage;
        public List<string> positions = new List<string>();
        public PartCartLine Copy() {var copy=(PartCartLine)MemberwiseClone();copy.positions=new List<string>(positions??new List<string>());return copy;}
    }
    public sealed class PartCartQuote
    {
        internal readonly string profileJson, catalogJson, linesJson;
        internal readonly string policyJson;
        internal readonly ValuationQuote[] sales;
        public readonly string id, carId;
        public readonly long revision, issued, expires;
        public readonly int total;
        internal PartCartQuote(PlayerProfile p,string car,IEnumerable<PartCartLine> lines,int amount,long now,int seconds,ValuationPolicy policy,ValuationQuote[] capturedSales)
        {
            id=MechanicsOwnership.NewId("cart");carId=car;revision=p.revision;issued=now;expires=checked(now+TimeSpan.FromSeconds(seconds).Ticks);total=amount;
            profileJson=JsonUtility.ToJson(p);catalogJson=PartCart.CatalogSnapshot(p.catalog,car,p);
            linesJson=JsonUtility.ToJson(new PartCart.Lines { entries=lines.Select(l=>l.Copy()).ToList() });
            policyJson=JsonUtility.ToJson(policy);sales=capturedSales;
        }
    }
    public static class PartCart
    {
        [Serializable] internal sealed class Lines { public List<PartCartLine> entries=new List<PartCartLine>(); }
        internal static string CatalogSnapshot(MechanicalCatalog catalog,string id,PlayerProfile p) => JsonUtility.ToJson(catalog.Spec(p.cars.Find(c=>c.instanceId==id).vehicleId))+string.Join("/",catalog.parts.Select(MarketCatalogLayout.Json));
        public static int Total(PlayerProfile p,string carId,IEnumerable<PartCartLine> source,ValuationPolicy policy=null,long now=0,bool validateBlocks=true)
        {
            var car=p.cars.Find(c=>c.instanceId==carId);EngineRebuilds.RequireAvailable(car);
            var lines=source.ToArray();if(lines.Length==0||lines.Length>100)throw new InvalidOperationException("Cart needs 1–100 distinct offers.");
            policy=policy??ValuationPolicy.Defaults();if(now==0)now=DateTime.UtcNow.Ticks;
            var ids=new HashSet<string>();var assets=new HashSet<string>();var positions=new HashSet<string>();var slots=new HashSet<PartCategory>();int total=0;PlayerProfile movePreview=null;var supply=new PartsMarket.Ledger(p,CartReservations.Matches(p,carId,lines));
            var moveSlots=new HashSet<string>();
            foreach(var line in lines) {
                var d=line==null?null:p.catalog.Find(line.definitionId);
                if(line==null||!Enum.IsDefined(typeof(CartAction),line.action)||line.quantity<1||line.quantity>99)throw new InvalidOperationException("Invalid cart action or quantity.");
                if(line.action==CartAction.SellPart||line.action==CartAction.SellUnit) {
                    if(line.quantity!=1||!assets.Add(line.assetId))throw new InvalidOperationException("Owned item may be used only once in a cart.");
                    total=checked(total-InventoryPricing.Sell(p,line.assetId,line.action==CartAction.SellUnit,policy,"wholesale",now).Total);continue;
                }
                if(line.action==CartAction.Move) {
                    if(line.quantity!=1||!assets.Add(line.assetId))throw new InvalidOperationException("Duplicate move or invalid quantity.");
                    if(movePreview==null){movePreview=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));MechanicsOwnership.Ensure(movePreview,p.catalog);}SharedInventory.Move(movePreview,line.assetId,line.destination,line.destinationId);
                    var moved=p.parts.Find(i=>i.instanceId==line.assetId);if(moved!=null&&line.destination==InventoryKind.Car&&line.destinationId==carId&&!slots.Add(p.catalog.Find(moved.definitionId).category))throw new InvalidOperationException("Conflicting slot installations.");
                    if(moved!=null&&line.destination!=InventoryKind.Storage&&!moveSlots.Add(line.destination+"/"+line.destinationId+"/"+p.catalog.Find(moved.definitionId).category))throw new InvalidOperationException("Conflicting destination slot installations.");
                    continue;
                }
                if(d==null||d.price<0||line.install&&!p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)||p.reputation<d.reputationRequired)throw new InvalidOperationException("Unavailable/incompatible cart offer or reputation.");
                int price=InventoryPricing.NewSet(d);bool unit=line.action==CartAction.NewUnits;
                if(line.action==CartAction.BuyUsed) {
                    var stock=p.inventory.dealer.Find(s=>s.Id==line.assetId);
                    if(stock==null||stock.DefinitionId!=d.id||line.quantity!=1||!assets.Add(line.assetId))throw new InvalidOperationException("Used stock unavailable or used twice.");
                    unit=stock.unit!=null;if(unit&&line.role!=stock.unit.element.role)throw new InvalidOperationException("Used unit role changed.");price=InventoryPricing.BuyUsed(stock,policy);
                } else if(!ids.Add(line.action+"/"+d.id+"/"+line.role))throw new InvalidOperationException("Duplicate new offer.");
                if(unit) {
                    if(!InventoryUnits.Roles(p,car,d).Contains(line.role))throw new InvalidOperationException("This offer has no individually modeled units of that type.");
                    if(line.action==CartAction.NewUnits)price=InventoryPricing.NewUnit(d,InventoryUnits.Count(p,car,d));
                    if(line.install){if(line.positions==null||line.positions.Count==0||line.positions.Count>line.quantity||line.positions.Any(s=>!InventoryUnits.Positions(p,car,d.id,line.role).Contains(s)||!positions.Add(s)))throw new InvalidOperationException("Select unique fitted positions for installed units, matching the fitted specification. Unselected extras remain loose.");}
                } else if(line.install&&!slots.Add(d.category))throw new InvalidOperationException("Only one cart offer can install in each slot. Other offers may be bought as spares.");
                total=checked(total+(line.action==CartAction.BuyUsed?checked(price*line.quantity):supply.Take(d,unit?1:InventoryUnits.Count(p,car,d),price,line.quantity).cost));
            }
            foreach(var category in slots)if(lines.Any(l=>l.install&&(l.action==CartAction.NewUnits||l.action==CartAction.BuyUsed&&p.inventory.dealer.Any(s=>s.Id==l.assetId&&s.unit!=null))&&p.catalog.Find(l.definitionId).category==category))throw new InvalidOperationException("Choose a complete set or individual replacements for a slot, not both.");
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
        public static PartCartQuote Quote(PlayerProfile p,string carId,IEnumerable<PartCartLine> lines,long now,int quoteSeconds,ValuationPolicy policy=null)
        {
            if(now<p.lastEconomyUtcTicks||p.runCheckpoints.Any(r=>!r.settled)||quoteSeconds<1)throw new InvalidOperationException("Close the run and use a valid saved clock before checkout.");
            policy=policy??ValuationPolicy.Defaults();var captured=lines.Select(l=>l.Copy()).ToArray();
            return new PartCartQuote(p,carId,captured,Total(p,carId,captured,policy,now),now,quoteSeconds,policy,captured.Where(l=>l.action==CartAction.SellPart||l.action==CartAction.SellUnit).Select(l=>InventoryPricing.Sell(p,l.assetId,l.action==CartAction.SellUnit,policy,"wholesale",now)).ToArray());
        }
        // Call through MechanicsTransactions: the whole cart publishes with one save or nothing does.
        public static void Checkout(PlayerProfile p,PartCartQuote quote,long now,ValuationPolicy policy=null)
        {
            if(quote==null||now<quote.issued||now>=quote.expires||now<p.lastEconomyUtcTicks||p.revision!=quote.revision||p.runCheckpoints.Any(r=>!r.settled)||p.economyReceipts.Any(r=>r.id==quote.id))throw new InvalidOperationException("Cart quote stale, expired or already purchased.");
            if(!SnapshotEquality.Profile(quote.profileJson,p,p.catalog)||quote.catalogJson!=CatalogSnapshot(p.catalog,quote.carId,p))throw new InvalidOperationException("Assets or offers changed; review the cart again.");
            var lines=JsonUtility.FromJson<Lines>(quote.linesJson).entries;
            policy=policy??ValuationPolicy.Defaults();if(quote.policyJson!=JsonUtility.ToJson(policy))throw new InvalidOperationException("Pricing policy changed; review the cart again.");
            int total=Total(p,quote.carId,lines,policy,now);if(total!=quote.total||p.cash<total)throw new InvalidOperationException("Insufficient cash or changed prices; nothing purchased.");
            foreach(var sale in quote.sales){var error=Valuations.Validate(sale,p,p.catalog,policy,now);if(error!=null)throw new InvalidOperationException(error);}
            int before=p.cash;
            // Sales fund the complete basket; all validation precedes mutation on the transaction candidate.
            foreach(var sale in quote.sales){if(sale.Kind==ValuationKind.LoosePart){var item=p.parts.Find(i=>i.instanceId==sale.AssetId);p.parts.Remove(item);p.inventory.dealer.Add(new DealerStock{part=item,paidPrice=sale.Total});}else{var item=p.looseEngineElements.Find(i=>i.element.id==sale.AssetId);p.looseEngineElements.Remove(item);p.inventory.dealer.Add(new DealerStock{isUnit=true,unit=item,paidPrice=sale.Total});}p.cash=checked(p.cash+sale.Total);}
            bool ownsReservation=CartReservations.SameCart(p,quote.carId,lines);p.inventory.usingCartReservation=CartReservations.Matches(p,quote.carId,lines);try{Apply(p,quote.carId,lines,policy:policy);}finally{p.inventory.usingCartReservation=false;}if(ownsReservation)CartReservations.Clear(p);
            if(p.cash!=checked(before-total))throw new InvalidOperationException("Cart price and actual operations differ.");
            PaidEconomy.Record(p,quote.id,"Parts cart",quote.carId,before,now,disposed:quote.sales.SelectMany(s=>s.IncludedIds).ToArray(),retained:lines.Where(l=>l.action==CartAction.Move).Select(l=>l.assetId).ToArray());
        }
        internal static void Apply(PlayerProfile p,string carId,IEnumerable<PartCartLine> lines,bool preview=false,ValuationPolicy policy=null)
        {
            foreach(var line in lines) {
                if(line.action==CartAction.SellPart||line.action==CartAction.SellUnit)continue;
                if(line.action==CartAction.Move){SharedInventory.Move(p,line.assetId,line.destination,line.destinationId);continue;}
                for(int i=0;i<line.quantity;i++) {
                    if(line.action==CartAction.NewSet){PartOperations.Buy(p,p.catalog,carId,line.definitionId,line.install&&i==0);continue;}
                    LooseEngineElement unit;
                    if(line.action==CartAction.NewUnits)unit=InventoryUnits.Buy(p,carId,line.definitionId,line.role);
                    else {var stock=p.inventory.dealer.Find(s=>s.Id==line.assetId);p.cash-=InventoryPricing.BuyUsed(stock,policy);p.inventory.dealer.Remove(stock);if(stock.part!=null){stock.part.storageId=SharedInventory.Garage;p.parts.Add(stock.part);if(line.install)SharedInventory.Move(p,stock.part.instanceId,InventoryKind.Car,carId);continue;}unit=stock.unit;unit.storageId=SharedInventory.Garage;p.looseEngineElements.Add(unit);}
                    if(line.install&&i<line.positions.Count)InventoryUnits.Install(p,carId,unit.element.id,line.positions[i]);
                }
            }
        }
        public static InspectionBuild Preview(PlayerProfile p,OwnedCar car,IEnumerable<PartCartLine> lines)
        {
            var copy=PreviewProfile(p,car,lines);
            return InspectionBuild.Capture(copy,copy.cars.Find(c=>c.instanceId==car.instanceId));
        }
        public static PlayerProfile PreviewProfile(PlayerProfile p,OwnedCar car,IEnumerable<PartCartLine> lines)
        {
            var copy=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));MechanicsOwnership.Ensure(copy,p.catalog);copy.cash=int.MaxValue/2;
            var captured=lines.ToArray();bool ownsReservation=CartReservations.SameCart(copy,car.instanceId,captured);copy.inventory.usingCartReservation=CartReservations.Matches(copy,car.instanceId,captured);try{Apply(copy,car.instanceId,captured,true);}finally{copy.inventory.usingCartReservation=false;}if(ownsReservation)CartReservations.Clear(copy);MechanicsOwnership.Ensure(copy,p.catalog);return copy;
        }
    }
}
