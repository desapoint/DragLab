using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    // Dormant owner-thread projection of the current stock/reservation algorithms.
    // It borrows the source graph only while open and never publishes mutations.
    internal sealed class ScopedMarketReadSet : IDisposable
    {
        PlayerProfile owner;
        Dictionary<string, NewPartStock> stockIndex;
        Dictionary<string, NewPartStock> stockAliases;
        readonly Dictionary<NewPartStock, int> stockOverlay = new Dictionary<NewPartStock, int>(ReferenceComparer<NewPartStock>.Instance);
        readonly Dictionary<ReservedPartSupply, int> holdOverlay = new Dictionary<ReservedPartSupply, int>(ReferenceComparer<ReservedPartSupply>.Instance);
        readonly Dictionary<NewPartStock, int> stockGroups = new Dictionary<NewPartStock, int>(ReferenceComparer<NewPartStock>.Instance);
        readonly Dictionary<ReservedPartSupply, int> holdGroups = new Dictionary<ReservedPartSupply, int>(ReferenceComparer<ReservedPartSupply>.Instance);
        readonly HashSet<ScopedSupplyLedger> ledgers = new HashSet<ScopedSupplyLedger>();
        MechanicalCatalog.LookupScope catalog;
        ICartCatalogRead sharedCatalog;
        int nextStockGroup, nextHoldGroup;
        bool disposed;

        ScopedMarketReadSet(PlayerProfile value,ICartCatalogRead reader=null)
        {
            owner = value;
            sharedCatalog=reader;
        }

        internal static ScopedMarketReadSet OpenBorrowed(PlayerProfile owner) => new ScopedMarketReadSet(owner);
        internal static ScopedMarketReadSet OpenBorrowed(PlayerProfile owner,ICartCatalogRead reader) => new ScopedMarketReadSet(owner,reader);
        internal bool IsDisposed => disposed;

        internal StockResolution Stock(PartDefinition request)
        {
            return ResolveStock(request).Snapshot;
        }

        internal int Available(PartDefinition request)
        {
            Check();
            var stock = ResolveStock(request).Snapshot;
            int held = Held(request, true);
            return Math.Max(0, (stock.Found ? CurrentStock(stock.IdentityGroup) : 0) - held);
        }

        internal int Held(PartDefinition request, bool currentOnly = false)
        {
            Check();
            var p = owner;
            if (!CartReservations.Active(p)) return 0;
            var stock = ResolveStock(request);
            var supply = p.inventory.cart.supply;
            int sum = 0;
            return supply.Where(hold => (!currentOnly || hold.generation == p.inventory.market.generation) &&
                    (hold.definitionId == request.id || stock.Snapshot.Found && StockContains(stock.Snapshot.IdentityGroup, hold.definitionId)))
                .Sum(hold => CurrentHold(hold));
        }

        internal ScopedSupplyQuote Quote(PartDefinition request, int count, bool unit = false, int quantity = 1)
        {
            Check();
            var stock = Stock(request);
            int available = Available(request) + (owner.inventory.usingCartReservation ? Held(request) : 0);
            int units = unit ? 1 : count;
            int price = unit ? InventoryPricing.NewUnit(request, count) : request.price;
            return ScopedSupplyQuote.From(PartsMarket.Calculate(available, stock.Found ? stock.DeliveryPercent : 10, units, price, quantity));
        }

        internal ConsumeResult Consume(PartDefinition request, int count, bool unit = false)
        {
            Check();
            var quote = Quote(request, count, unit);
            var stock = ResolveStock(request);
            int left = quote.LocalUnits;
            var holdBefore = new Dictionary<ReservedPartSupply,int>(ReferenceComparer<ReservedPartSupply>.Instance);
            var stockBefore = new Dictionary<NewPartStock,int>(ReferenceComparer<NewPartStock>.Instance);
            if (owner.inventory.usingCartReservation && CartReservations.Active(owner))
            {
                var matching = owner.inventory.cart.supply.Where(hold => hold.definitionId == request.id || stock.Snapshot.Found && StockContains(stock.Snapshot.IdentityGroup, hold.definitionId)).OrderBy(value => value.generation);
                foreach (var hold in matching)
                {
                    int before = CurrentHold(hold);
                    int take = Math.Min(left, before);
                    if(!holdBefore.ContainsKey(hold))holdBefore.Add(hold,before);
                    SetHold(hold, before - take);
                    left -= take;
                    if (stock.Snapshot.Found && hold.generation == owner.inventory.market.generation)
                    {
                        if(!stockBefore.ContainsKey(stock.Source))stockBefore.Add(stock.Source,CurrentStock(stock.Snapshot.IdentityGroup));
                        SetStockByGroup(stock.Snapshot.IdentityGroup, CurrentStock(stock.Snapshot.IdentityGroup) - take);
                    }
                }
            }
            if (stock.Snapshot.Found)
            {
                if(!stockBefore.ContainsKey(stock.Source))stockBefore.Add(stock.Source,CurrentStock(stock.Snapshot.IdentityGroup));
                SetStockByGroup(stock.Snapshot.IdentityGroup, CurrentStock(stock.Snapshot.IdentityGroup) - left);
            }
            var holdDeltas=new List<ScopedQuantityDelta>();foreach(var pair in holdBefore)if(pair.Value!=CurrentHold(pair.Key))holdDeltas.Add(Delta(pair.Key,pair.Value,CurrentHold(pair.Key)));
            var stockDeltas=new List<ScopedQuantityDelta>();foreach(var pair in stockBefore)if(pair.Value!=CurrentStock(stockGroups[pair.Key]))stockDeltas.Add(Delta(pair.Key,pair.Value,CurrentStock(stockGroups[pair.Key])));
            return new ConsumeResult(quote, stockDeltas, holdDeltas);
        }

        internal ScopedSupplyLedger OpenDefaultLedger(bool includeReserved = false)
        {
            Check();
            var ledger = new ScopedSupplyLedger(this, owner, includeReserved);
            ledgers.Add(ledger);
            return ledger;
        }

        CatalogValue CatalogProduct(string id, out bool found, out int position, out int identityGroup)
        {
            Check();
            var definition = FindDefinition(id, out found, out position, out identityGroup);
            return new CatalogValue(found, position, identityGroup, found ? PartProductValue.Capture(definition) : null);
        }

        PartDefinition FindDefinition(string id,out bool found,out int position,out int identityGroup)
        {
            Check();
            if(sharedCatalog!=null){var product=sharedCatalog.FindProduct(id);found=product.Found;position=product.Position;identityGroup=product.IdentityGroup;return product.Definition;}
            if(catalog==null)catalog=owner.catalog.OpenLookupScope();
            return catalog.FindDefinition(id,out found,out position,out identityGroup);
        }

        bool ProductEquivalent(PartProductValue left, PartProductValue right) => Equivalent(left, right);

        void Check()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ScopedMarketReadSet));
        }

        NewPartStock StockReferenceForGroup(int group)
        {
            Check();
            foreach (var pair in stockGroups) if (pair.Value == group) return pair.Key;
            return null;
        }

        int CurrentStock(int group)
        {
            Check();
            var row = StockReferenceForGroup(group);
            if (row == null) return 0;
            return stockOverlay.TryGetValue(row, out var value) ? value : row.units;
        }
        int ReadSourceStockUnits(NewPartStock stock)
        {
            Check();int ignored=stock.units;
            if(!stockGroups.TryGetValue(stock,out var group))stockGroups[stock]=group=nextStockGroup++;
            return CurrentStock(group);
        }

        int CurrentHold(ReservedPartSupply hold)
        {
            Check();
            return holdOverlay.TryGetValue(hold, out var value) ? value : hold.units;
        }

        void SetHold(ReservedPartSupply hold, int units)
        {
            Check(); holdOverlay[hold] = units;
        }

        StockResolution StockByReference(NewPartStock stock)=>SnapshotStock(stock);
        void SetStockGroup(int group,int units)=>SetStockByGroup(group,units);
        static int ProductHash(PartProductValue value){if(!value.TryGetProductHashCode(out var hash))throw new InvalidOperationException("Unsupported product value in scoped market lookup.");return hash;}
        static bool Equivalent(PartProductValue left, PartProductValue right)
        {
            if (!left.TryProductEquivalent(right, out var same)) throw new InvalidOperationException("Unsupported product value in scoped market lookup.");
            return same;
        }

        void EnsureColdStockIndex(PartsStockState market)
        {
            if(stockIndex!=null||market.index!=null)return;
            stockIndex=new Dictionary<string,NewPartStock>(StringComparer.Ordinal);
            foreach(var row in market.parts)foreach(string definitionId in row.definitions)stockIndex.Add(definitionId,row);
        }
        bool TryIndexedStock(PartsStockState market,string id,out NewPartStock stock)
        {
            if(stockIndex!=null)return stockIndex.TryGetValue(id,out stock);
            if(stockAliases!=null&&stockAliases.TryGetValue(id,out stock))return true;
            return market.index.TryGetValue(id,out stock);
        }
        void CacheStockAlias(PartsStockState market,string id,NewPartStock row)
        {
            if(stockIndex!=null)stockIndex[id]=row;
            else {if(stockAliases==null)stockAliases=new Dictionary<string,NewPartStock>(market.index.Comparer);stockAliases[id]=row;}
        }

        StockResolution SnapshotStock(NewPartStock row)
        {
            if(row==null)return StockResolution.Missing;
            int ordinal = -1;
            var rows = owner.inventory.market.parts;
            var positions = new List<int>();
            for (int i = 0; rows!=null && i < rows.Count; i++)
                if (ReferenceEquals(rows[i], row)) { positions.Add(i); if (ordinal < 0) ordinal = i; }
            if (!stockGroups.TryGetValue(row, out var group)) stockGroups[row] = group = nextStockGroup++;
            return new StockResolution(true, ordinal, group, CurrentStock(group), row.initialUnits, row.deliveryPercent, positions);
        }

        ResolvedStock ResolveStock(PartDefinition request)
        {
            Check();var market=owner.inventory.market;EnsureColdStockIndex(market);
            if(TryIndexedStock(market,request.id,out var stock))return new ResolvedStock(stock,SnapshotStock(stock));
            var product=PartProductValue.Capture(request);
            if(market.parts==null)throw new ArgumentNullException("source");
            for(int rowIndex=0;rowIndex<market.parts.Count;rowIndex++)
            {
                var row=market.parts[rowIndex];
                if(row.definitions==null)throw new ArgumentNullException("source");
                foreach(string id in row.definitions)
                {
                    var existing=CatalogProduct(id,out var found,out _,out _);
                    if(found&&Equivalent(product,existing.Value)){CacheStockAlias(market,request.id,row);return new ResolvedStock(row,SnapshotStock(row));}
                }
            }
            return new ResolvedStock(null,StockResolution.Missing);
        }
        readonly struct ResolvedStock{internal readonly NewPartStock Source;internal readonly StockResolution Snapshot;internal ResolvedStock(NewPartStock source,StockResolution snapshot){Source=source;Snapshot=snapshot;}}

        int GroupForHold(ReservedPartSupply hold)
        {
            if (!holdGroups.TryGetValue(hold, out var group)) holdGroups[hold] = group = nextHoldGroup++;
            return group;
        }

        ScopedQuantityDelta Delta(NewPartStock row, int before, int after)
        {
            int group = stockGroups[row];
            var positions = new List<int>(); var rows = owner.inventory.market.parts;
            for (int i = 0; rows!=null && i < rows.Count; i++) if (ReferenceEquals(rows[i], row)) positions.Add(i);
            return new ScopedQuantityDelta(group, before, after, positions);
        }

        ScopedQuantityDelta Delta(ReservedPartSupply hold, int before, int after)
        {
            int group = GroupForHold(hold);
            var positions = new List<int>(); var rows = owner.inventory.cart.supply;
            for (int i = 0; rows!=null && i < rows.Count; i++) if (ReferenceEquals(rows[i], hold)) positions.Add(i);
            return new ScopedQuantityDelta(group, before, after, positions);
        }

        bool StockContains(int group, string id)
        {
            var stock = StockReferenceForGroup(group);
            return stock != null && stock.definitions.Contains(id);
        }

        void SetStockByGroup(int group, int value)
        {
            var stock = StockReferenceForGroup(group);
            if (stock != null) stockOverlay[stock] = value;
        }

        public void Dispose()
        {
            if (disposed) return;
            foreach (var ledger in ledgers.ToArray()) ledger.CloseFromOwner();
            ledgers.Clear();
            stockIndex = null;
            stockAliases = null;
            stockOverlay.Clear(); holdOverlay.Clear(); stockGroups.Clear(); holdGroups.Clear();
            catalog?.Dispose(); catalog=null; sharedCatalog=null; owner = null; disposed = true;
        }

        internal readonly struct CatalogValue
        {
            internal readonly bool Found;
            internal readonly int Position, IdentityGroup;
            internal readonly PartProductValue Value;
            internal CatalogValue(bool found, int position, int group, PartProductValue value)
            { Found = found; Position = position; IdentityGroup = group; Value = value; }
        }
    internal sealed class ScopedSupplyLedger
    {
        ScopedMarketReadSet parent;
        PlayerProfile owner;
        readonly Dictionary<string, NewPartStock> stocks = new Dictionary<string, NewPartStock>(StringComparer.Ordinal);
        readonly Dictionary<int, int> remaining = new Dictionary<int, int>();
        Dictionary<int,List<ProductStock>> products;
        readonly struct ProductStock{internal readonly NewPartStock Stock;internal readonly PartProductValue Product;internal ProductStock(NewPartStock stock,PartProductValue product){Stock=stock;Product=product;}}

        internal ScopedSupplyLedger(ScopedMarketReadSet scope, PlayerProfile profile, bool includeReserved)
        {
            parent = scope; owner = profile;
            bool reserved = CartReservations.Active(profile);
            foreach (var stock in profile.inventory.market.parts)
            {
                int initialUnits=stock.units;
                int group=scope.StockByReference(stock).IdentityGroup;
                initialUnits=scope.ReadSourceStockUnits(stock);
                remaining[group] = initialUnits;
                if (reserved)
                {
                    var definition = scope.FindDefinition(stock.definitions[0],out _,out _,out _);
                    var heldNow = Held(definition, true);
                    remaining[group] = Math.Max(0, initialUnits - heldNow) + (includeReserved ? Held(definition) : 0);
                }
                foreach (string id in stock.definitions) stocks[id] = stock;
            }
        }

        internal int Held(PartDefinition request, bool currentOnly = false)
        {
            Check();
            return parent.Held(request,currentOnly);
        }

        internal int Available(PartDefinition request)
        {
            Check();
            var stock = Find(request);
            return stock.Found ? remaining[stock.IdentityGroup] : 0;
        }

        internal ScopedSupplyQuote Quote(PartDefinition request, int units, int basePrice, int quantity = 1)
        {
            Check();
            var stock = Find(request);
            int available = stock.Found ? remaining[stock.IdentityGroup] : 0;
            return ScopedSupplyQuote.From(PartsMarket.Calculate(available, stock.Found ? stock.DeliveryPercent : 10, units, basePrice, quantity));
        }

        internal SupplyTakeResult Take(PartDefinition request, int units, int basePrice, int quantity = 1, bool consume = false)
        {
            Check();
            var stock = Find(request);
            int available = stock.Found ? remaining[stock.IdentityGroup] : 0;
            var quote = ScopedSupplyQuote.From(PartsMarket.Calculate(available, stock.Found ? stock.DeliveryPercent : 10, units, basePrice, quantity));
            if (!stock.Found) return new SupplyTakeResult(quote, null);
            int before = available;
            int after = available - quote.LocalUnits;
            remaining[stock.IdentityGroup] = after;
            if(!consume)return new SupplyTakeResult(quote,null);
            before=parent.CurrentStock(stock.IdentityGroup);
            parent.SetStockGroup(stock.IdentityGroup,after);
            return new SupplyTakeResult(quote, new ScopedQuantityDelta(stock.IdentityGroup, before, after, stock.Positions));
        }

        StockResolution Find(PartDefinition request)
        {
            Check();
            if (stocks.TryGetValue(request.id, out var stock)) return stock==null?StockResolution.Missing:Resolve(stock);
            if (products == null)
            {
                products = new Dictionary<int,List<ProductStock>>();
                foreach (var row in owner.inventory.market.parts)
                    foreach (string id in row.definitions)
                    {
                        var definition = parent.CatalogProduct(id, out var found, out _, out _);
                        if (found)
                        {
                            int key=ScopedMarketReadSet.ProductHash(definition.Value);
                            if(!products.TryGetValue(key,out var bucket))products[key]=bucket=new List<ProductStock>();
                            bool equal=false;foreach(var known in bucket)if(parent.ProductEquivalent(known.Product,definition.Value)){equal=true;break;}
                            if(!equal)bucket.Add(new ProductStock(row,definition.Value));
                        }
                    }
            }
            var product = PartProductValue.Capture(request);
            int productKey=ScopedMarketReadSet.ProductHash(product);stock=null;
            if(products.TryGetValue(productKey,out var candidates))foreach(var candidate in candidates)if(parent.ProductEquivalent(product,candidate.Product)){stock=candidate.Stock;break;}
            stocks[request.id] = stock;
            return stock == null ? StockResolution.Missing : Resolve(stock);
        }

        StockResolution Resolve(NewPartStock stock)
        {
            var snapshot = parent.StockByReference(stock);
            return snapshot.WithUnits(remaining[snapshot.IdentityGroup]);
        }

        void Check()
        {
            if (parent == null || owner == null) throw new ObjectDisposedException(nameof(ScopedSupplyLedger));
            parent.Check();
        }

        internal void CloseFromOwner()
        {
            parent = null; owner = null; stocks.Clear(); remaining.Clear(); products?.Clear(); products = null;
        }
    }
    }

    internal sealed class StockResolution
    {
        internal static readonly StockResolution Missing = new StockResolution(false, -1, -1, 0, 0, 10, Array.Empty<int>());
        readonly int[] positions;
        internal readonly bool Found;
        internal readonly int RowOrdinal, IdentityGroup, Units, InitialUnits, DeliveryPercent;
        internal int[] Positions => (int[])positions.Clone();
        internal StockResolution(bool found, int row, int group, int units, int initial, int delivery, IEnumerable<int> positions)
        { Found = found; RowOrdinal = row; IdentityGroup = group; Units = units; InitialUnits = initial; DeliveryPercent = delivery; this.positions = positions == null ? Array.Empty<int>() : new List<int>(positions).ToArray(); }
        internal StockResolution WithUnits(int units) => new StockResolution(Found, RowOrdinal, IdentityGroup, units, InitialUnits, DeliveryPercent, positions);
    }

    internal sealed class ScopedSupplyQuote
    {
        internal readonly int Cost, Fee, LocalUnits, ExpressUnits;
        internal ScopedSupplyQuote(int cost, int fee, int local, int express) { Cost = cost; Fee = fee; LocalUnits = local; ExpressUnits = express; }
        internal static ScopedSupplyQuote From(SupplyQuote quote) => new ScopedSupplyQuote(quote.cost, quote.fee, quote.localUnits, quote.expressUnits);
    }

    internal sealed class ScopedQuantityDelta
    {
        readonly int[] positions;
        internal readonly int IdentityGroup, Before, After;
        internal int[] Positions => (int[])positions.Clone();
        internal ScopedQuantityDelta(int group, int before, int after, IEnumerable<int> positions)
        { IdentityGroup = group; Before = before; After = after; this.positions = positions == null ? Array.Empty<int>() : new List<int>(positions).ToArray(); }
    }

    internal sealed class ConsumeResult
    {
        readonly ScopedQuantityDelta[] stockDeltas, holdDeltas;
        internal readonly ScopedSupplyQuote Quote;
        internal ScopedQuantityDelta[] StockDeltas => (ScopedQuantityDelta[])stockDeltas.Clone();
        internal ScopedQuantityDelta[] HoldDeltas => (ScopedQuantityDelta[])holdDeltas.Clone();
        internal ConsumeResult(ScopedSupplyQuote quote, IEnumerable<ScopedQuantityDelta> stocks, IEnumerable<ScopedQuantityDelta> holds)
        { Quote = quote; stockDeltas = stocks == null ? Array.Empty<ScopedQuantityDelta>() : new List<ScopedQuantityDelta>(stocks).ToArray(); holdDeltas = holds == null ? Array.Empty<ScopedQuantityDelta>() : new List<ScopedQuantityDelta>(holds).ToArray(); }
    }

    internal sealed class SupplyTakeResult
    {
        internal readonly ScopedSupplyQuote Quote;
        internal readonly ScopedQuantityDelta StockDelta;
        internal SupplyTakeResult(ScopedSupplyQuote quote, ScopedQuantityDelta delta) { Quote = quote; StockDelta = delta; }
    }

    internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        internal static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
        public bool Equals(T left, T right) => ReferenceEquals(left, right);
        public int GetHashCode(T value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
}
