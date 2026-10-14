using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DragLab.Domain.Mechanics
{
    [Serializable] public sealed class ReservedPartSupply {public string definitionId;public int units,generation;}
    [Serializable] public sealed class CartReservationState
    {
        public int version=1,createdRun,expiresRun,generation;
        public string carId;
        public List<PartCartLine> lines=new List<PartCartLine>();
        public List<ReservedPartSupply> supply=new List<ReservedPartSupply>();
    }
    public static class CartReservations
    {
        public const int LifetimeRaces=9;
        static string Lines(IEnumerable<PartCartLine> rows)=>JsonUtility.ToJson(new PartCart.Lines{entries=rows.Select(l=>l?.Copy()).ToList()});
        // The ninth completion and its third refresh retain the hold; the following completion expires it.
        public static bool Active(PlayerProfile p)=>p.inventory.cart!=null&&p.inventory.cart.version==1&&p.vehicleStock.completedRuns<=p.inventory.cart.expiresRun;
        public static bool SameCart(PlayerProfile p,string carId,IEnumerable<PartCartLine> rows)=>p.inventory.cart!=null&&p.inventory.cart.carId==carId&&Lines(p.inventory.cart.lines)==Lines(rows);
        public static bool Matches(PlayerProfile p,string carId,IEnumerable<PartCartLine> rows)=>Active(p)&&SameCart(p,carId,rows);
        static bool Same(MechanicalCatalog c,string a,string b)=>a==b||PartsMarket.ProductKey(c.Find(a))==PartsMarket.ProductKey(c.Find(b));
        public static int Held(PlayerProfile p,PartDefinition d,bool currentOnly=false)
        {if(!Active(p))return 0;var stock=PartsMarket.Stock(p,d);return p.inventory.cart.supply.Where(s=>(!currentOnly||s.generation==p.inventory.market.generation)&&(s.definitionId==d.id||stock!=null&&stock.definitions.Contains(s.definitionId))).Sum(s=>s.units);}
        public static bool HoldsUsed(PlayerProfile p,string id)=>Active(p)&&p.inventory.cart.lines.Any(l=>l.action==CartAction.BuyUsed&&l.assetId==id);
        public static void Capture(PlayerProfile p,string carId,IEnumerable<PartCartLine> rows)
        {
            var lines=rows.Select(l=>l.Copy()).ToList();var old=p.inventory.cart;
            if(lines.Count==0){Clear(p);return;}
            if(old!=null&&old.carId==carId&&Lines(old.lines)==Lines(lines))return;
            var car=p.cars.Find(c=>c.instanceId==carId);if(car==null){Clear(p);return;}
            bool retain=Active(p)&&old.carId==carId;
            var next=new CartReservationState{carId=carId,createdRun=p.vehicleStock.completedRuns,expiresRun=checked(p.vehicleStock.completedRuns+LifetimeRaces),generation=p.inventory.market.generation,lines=lines};
            var requirements=lines.Where(l=>l.action==CartAction.NewSet||l.action==CartAction.NewUnits).GroupBy(l=>PartsMarket.ProductKey(p.catalog.Find(l.definitionId)),StringComparer.Ordinal);
            foreach(var group in requirements){var d=p.catalog.Find(group.First().definitionId);int needed=group.Sum(l=>checked(l.quantity*(l.action==CartAction.NewUnits?1:InventoryUnits.Count(p,car,p.catalog.Find(l.definitionId)))));int heldCurrent=0;
                if(retain)foreach(var prior in old.supply.Where(s=>Same(p.catalog,s.definitionId,d.id)).OrderBy(s=>s.generation)){int take=Math.Min(needed,prior.units);if(take>0){next.supply.Add(new ReservedPartSupply{definitionId=d.id,units=take,generation=prior.generation});needed-=take;if(prior.generation==p.inventory.market.generation)heldCurrent+=take;}}
                var stock=PartsMarket.Stock(p,d);int available=stock==null?0:Math.Max(0,stock.units-heldCurrent);int fresh=Math.Min(needed,available);if(fresh>0)next.supply.Add(new ReservedPartSupply{definitionId=d.id,units=fresh,generation=p.inventory.market.generation});
            }
            p.inventory.cart=next;ReleaseOldUsed(p,old,new HashSet<string>(lines.Where(l=>l.action==CartAction.BuyUsed).Select(l=>l.assetId)));
        }
        static void ReleaseOldUsed(PlayerProfile p,CartReservationState old,HashSet<string> keep)
        {if(old!=null&&old.generation<p.inventory.market.generation){var ids=new HashSet<string>(old.lines.Where(l=>l.action==CartAction.BuyUsed).Select(l=>l.assetId));p.inventory.dealer.RemoveAll(s=>ids.Contains(s.Id)&&!keep.Contains(s.Id));}}
        public static void Clear(PlayerProfile p){var old=p.inventory.cart;p.inventory.cart=null;ReleaseOldUsed(p,old,new HashSet<string>());}
        public static void Expire(PlayerProfile p)
        {
            var state=p.inventory.cart;if(state==null)return;
            if(string.IsNullOrEmpty(state.carId)&&state.lines!=null&&state.lines.Count==0&&state.supply!=null&&state.supply.Count==0){p.inventory.cart=null;return;}
            if(!p.cars.Any(c=>c.instanceId==state.carId)){Clear(p);return;}
            if(!Active(p))ReleaseOldUsed(p,state,new HashSet<string>());
        }
        public static string Validate(PlayerProfile p)
        {
            var state=p.inventory.cart;if(state==null)return null;
            if(state.version!=1||string.IsNullOrEmpty(state.carId)||state.lines==null||state.supply==null||state.lines.Count<1||state.lines.Count>100||state.createdRun<0||state.createdRun>p.vehicleStock.completedRuns||state.expiresRun!=(long)state.createdRun+LifetimeRaces||state.generation<0||state.generation>p.inventory.market.generation)return "Invalid cart reservation clock or owner.";
            if(state.lines.Any(l=>l==null||!Enum.IsDefined(typeof(CartAction),l.action)||l.quantity<1||l.quantity>99||l.positions==null||l.definitionId!=null&&p.catalog.Find(l.definitionId)==null)||state.supply.Any(s=>s==null||s.units<0||s.units>25344||s.generation<0||s.generation>p.inventory.market.generation||p.catalog.Find(s.definitionId)==null))return "Invalid reserved stock or cart lines.";
            if(Active(p))foreach(var row in p.inventory.market.parts){int held=state.supply.Where(s=>s.generation==p.inventory.market.generation&&row.definitions.Contains(s.definitionId)).Sum(s=>s.units);if(held>row.units)return "Reserved stock exceeds its original supply.";}
            return null;
        }
    }
}
