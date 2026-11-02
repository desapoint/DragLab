#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    // Frozen at main 88a9b66, before the L1B production seam.
    // Literal default-ledger/stock/held/quote/consume closure. Intentionally
    // mutating and JSON-backed, for disposable Editor fixtures only.
    // ProductCacheMiss is the independent, already characterized L0 boundary.
    // MechanicalCatalog.Find and PartDefinition.Copy remain unchanged dependencies.
    internal static class LegacyScopedSupplyReference
    {
        static string ProductKey(PartDefinition definition)
            => LegacyCartCanonicalBoundaryReference.ProductCacheMiss(definition);

        internal static bool Active(PlayerProfile p)
            => p.inventory.cart != null && p.inventory.cart.version == 1 && p.vehicleStock.completedRuns <= p.inventory.cart.expiresRun;

        internal static NewPartStock Stock(PlayerProfile p, PartDefinition d)
        {
            var m=p.inventory.market;if(m.index==null){m.index=new Dictionary<string,NewPartStock>(StringComparer.Ordinal);foreach(var s in m.parts)foreach(string id in s.definitions)m.index.Add(id,s);}if(m.index.TryGetValue(d.id,out var stock))return stock;string key=ProductKey(d);stock=m.parts.FirstOrDefault(row=>row.definitions.Any(id=>{var existing=p.catalog.Find(id);return existing!=null&&ProductKey(existing)==key;}));if(stock!=null)m.index[d.id]=stock;return stock;
        }

        internal static int Held(PlayerProfile p, PartDefinition d, bool currentOnly=false)
        {if(!Active(p))return 0;var stock=Stock(p,d);return p.inventory.cart.supply.Where(s=>(!currentOnly||s.generation==p.inventory.market.generation)&&(s.definitionId==d.id||stock!=null&&stock.definitions.Contains(s.definitionId))).Sum(s=>s.units);}

        internal static int Available(PlayerProfile p, PartDefinition d)
            => Math.Max(0,(Stock(p,d)?.units??0)-Held(p,d,true));

        internal static int NewUnit(PartDefinition d, int count)
            => Math.Max(1,checked((int)Math.Ceiling(d.price/(double)Math.Max(1,count))));

        internal static SupplyQuote Calculate(int available, int percent, int units, int basePrice, int quantity)
        {
            if(available<0||percent<5||percent>10||units<1||basePrice<0||quantity<1||quantity>99)throw new InvalidOperationException("Invalid supply request.");var q=new SupplyQuote();
            for(int i=0;i<quantity;i++){int local=Math.Min(units,available),express=units-local;available-=local;int fee=express==0?0:checked((int)Math.Ceiling(basePrice*(express/(double)units)*percent/100.0));q.localUnits+=local;q.expressUnits+=express;q.fee=checked(q.fee+fee);q.cost=checked(q.cost+basePrice+fee);}return q;
        }

        internal static SupplyQuote Quote(PlayerProfile p, PartDefinition d, int count, bool unit=false, int quantity=1)
        {var stock=Stock(p,d);int available=Available(p,d)+(p.inventory.usingCartReservation?Held(p,d):0);return Calculate(available,stock?.deliveryPercent??10,unit?1:count,unit?NewUnit(d,count):d.price,quantity);}

        internal static SupplyQuote Consume(PlayerProfile p, PartDefinition d, int count, bool unit=false)
        {
            var q=Quote(p,d,count,unit);var stock=Stock(p,d);int left=q.localUnits;
            if(p.inventory.usingCartReservation&&Active(p))foreach(var hold in p.inventory.cart.supply.Where(s=>s.definitionId==d.id||stock!=null&&stock.definitions.Contains(s.definitionId)).OrderBy(s=>s.generation)){int take=Math.Min(left,hold.units);hold.units-=take;left-=take;if(stock!=null&&hold.generation==p.inventory.market.generation)stock.units-=take;}
            if(stock!=null)stock.units-=left;return q;
        }

        internal sealed class Ledger
        {
            readonly PlayerProfile owner;
            readonly Dictionary<string,NewPartStock> stocks=new Dictionary<string,NewPartStock>(StringComparer.Ordinal);
            readonly Dictionary<NewPartStock,int> remaining=new Dictionary<NewPartStock,int>();
            Dictionary<string,NewPartStock> products;

            internal Ledger(PlayerProfile p, bool includeReserved=false)
            {
                owner=p;bool reserved=Active(p);
                foreach(var s in p.inventory.market.parts){remaining[s]=s.units;if(reserved){var d=p.catalog.Find(s.definitions[0]);int held=LegacyScopedSupplyReference.Held(p,d,true);remaining[s]=Math.Max(0,s.units-held)+(includeReserved?LegacyScopedSupplyReference.Held(p,d):0);}foreach(string id in s.definitions)stocks[id]=s;}
            }

            NewPartStock Find(PartDefinition d)
            {
                if(stocks.TryGetValue(d.id,out var stock))return stock;
                if(products==null){products=new Dictionary<string,NewPartStock>(StringComparer.Ordinal);foreach(var row in owner.inventory.market.parts)foreach(string id in row.definitions){var definition=owner.catalog.Find(id);if(definition!=null){string key=ProductKey(definition);if(!products.ContainsKey(key))products[key]=row;}}}
                products.TryGetValue(ProductKey(d),out stock);stocks[d.id]=stock;return stock;
            }

            internal int Held(PartDefinition d, bool currentOnly=false) => LegacyScopedSupplyReference.Held(owner,d,currentOnly);
            internal int Available(PartDefinition d){var stock=Find(d);return stock==null?0:remaining[stock];}
            internal SupplyQuote Quote(PartDefinition d,int units,int basePrice,int quantity=1){var stock=Find(d);return Calculate(stock==null?0:remaining[stock],stock?.deliveryPercent??10,units,basePrice,quantity);}
            internal SupplyQuote Take(PartDefinition d,int units,int basePrice,int quantity=1,bool consume=false)
            {
                var stock=Find(d);int available=stock==null?0:remaining[stock];var q=Calculate(available,stock?.deliveryPercent??10,units,basePrice,quantity);
                if(stock!=null){remaining[stock]=available-q.localUnits;if(consume)stock.units=remaining[stock];}return q;
            }
        }
    }
}
#endif
