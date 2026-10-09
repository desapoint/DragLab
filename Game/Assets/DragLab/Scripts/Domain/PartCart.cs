using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DragLab.Domain.Mechanics
{
    [Serializable] public sealed class PartCartLine
    {
        public string definitionId;
        public int quantity=1;
        public bool install;
        public PartCartLine Copy() => (PartCartLine)MemberwiseClone();
    }
    public sealed class PartCartQuote
    {
        internal readonly string profileJson, catalogJson, linesJson;
        public readonly string id, carId;
        public readonly long revision, issued, expires;
        public readonly int total;
        internal PartCartQuote(PlayerProfile p,string car,IEnumerable<PartCartLine> lines,int amount,long now,int seconds)
        {
            id=MechanicsOwnership.NewId("cart");carId=car;revision=p.revision;issued=now;expires=checked(now+TimeSpan.FromSeconds(seconds).Ticks);total=amount;
            profileJson=JsonUtility.ToJson(p);catalogJson=PartCart.CatalogSnapshot(p.catalog,car,p);
            linesJson=JsonUtility.ToJson(new PartCart.Lines { entries=lines.Select(l=>l.Copy()).ToList() });
        }
    }
    public static class PartCart
    {
        [Serializable] internal sealed class Lines { public List<PartCartLine> entries=new List<PartCartLine>(); }
        internal static string CatalogSnapshot(MechanicalCatalog catalog,string id,PlayerProfile p) => JsonUtility.ToJson(catalog.Spec(p.cars.Find(c=>c.instanceId==id).vehicleId))+string.Join("/",catalog.parts.Select(d=>JsonUtility.ToJson(d)));
        public static int Total(PlayerProfile p,string carId,IEnumerable<PartCartLine> source)
        {
            var car=p.cars.Find(c=>c.instanceId==carId);EngineRebuilds.RequireAvailable(car);
            var lines=source.ToArray();if(lines.Length==0||lines.Length>100)throw new InvalidOperationException("Cart needs 1–100 distinct offers.");
            var ids=new HashSet<string>();var slots=new HashSet<PartCategory>();int total=0;
            foreach(var line in lines) {
                var d=line==null?null:p.catalog.Find(line.definitionId);
                if(d==null||!ids.Add(d.id)||line.quantity<1||line.quantity>99||d.price<0||!p.catalog.Fits(p.catalog.Spec(car.vehicleId),d)||p.reputation<d.reputationRequired)throw new InvalidOperationException("Unavailable/incompatible cart offer, quantity or reputation.");
                if(line.install&&!slots.Add(d.category))throw new InvalidOperationException("Only one cart offer can install in each slot. Other offers may be bought as spares.");
                total=checked(total+checked(d.price*line.quantity));
            }
            return total;
        }
        public static PartCartQuote Quote(PlayerProfile p,string carId,IEnumerable<PartCartLine> lines,long now,int quoteSeconds)
        {
            if(now<p.lastEconomyUtcTicks||p.runCheckpoints.Any(r=>!r.settled)||quoteSeconds<1)throw new InvalidOperationException("Close the run and use a valid saved clock before checkout.");
            return new PartCartQuote(p,carId,lines,Total(p,carId,lines),now,quoteSeconds);
        }
        // Call through MechanicsTransactions: the whole cart publishes with one save or nothing does.
        public static void Checkout(PlayerProfile p,PartCartQuote quote,long now)
        {
            if(quote==null||now<quote.issued||now>=quote.expires||now<p.lastEconomyUtcTicks||p.revision!=quote.revision||p.runCheckpoints.Any(r=>!r.settled)||p.economyReceipts.Any(r=>r.id==quote.id))throw new InvalidOperationException("Cart quote stale, expired or already purchased.");
            if(!SnapshotEquality.Profile(quote.profileJson,p,p.catalog)||quote.catalogJson!=CatalogSnapshot(p.catalog,quote.carId,p))throw new InvalidOperationException("Assets or offers changed; review the cart again.");
            var lines=JsonUtility.FromJson<Lines>(quote.linesJson).entries;
            int total=Total(p,quote.carId,lines);if(total!=quote.total||p.cash<total)throw new InvalidOperationException("Insufficient cash or changed prices; nothing purchased.");
            int before=p.cash;
            foreach(var line in lines)for(int i=0;i<line.quantity;i++)PartOperations.Buy(p,p.catalog,quote.carId,line.definitionId,line.install&&i==0);
            PaidEconomy.Record(p,quote.id,"Parts cart",quote.carId,before,now);
        }
    }
}
