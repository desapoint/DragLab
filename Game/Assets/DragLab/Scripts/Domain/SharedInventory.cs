using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    public enum InventoryKind { Storage, Car, Engine, Seller, Cart }
    [Serializable] public sealed class InventoryContainer
    {
        public InventoryKind kind;
        public string id, name;
    }
    [Serializable] public sealed class DealerStock
    {
        public bool isUnit;
        public PartInstance part;
        public LooseEngineElement unit;
        public int paidPrice;
        // Unity JSON can materialize an empty inactive inline payload.
        // Select the merchandise by its real identity, never wrapper nullness.
        public bool HasPart => part != null && !string.IsNullOrWhiteSpace(part.instanceId);
        public bool HasUnit => unit != null && unit.element != null && !string.IsNullOrWhiteSpace(unit.element.id);
        public bool IsPart => HasPart && !HasUnit && !isUnit;
        public bool IsUnit => HasUnit && !HasPart && isUnit;
        public string Id => IsPart ? part.instanceId : IsUnit ? unit.element.id : null;
        public string DefinitionId => IsPart ? part.definitionId : IsUnit ? unit.definitionId : null;
    }
    [Serializable] public sealed class InventoryState
    {
        public List<InventoryContainer> storage = new List<InventoryContainer>();
        public List<DealerStock> dealer = new List<DealerStock>();
        public PartsStockState market;
        public CartReservationState cart;
        [NonSerialized] public bool usingCartReservation;
    }
    public sealed class InventoryEntry
    {
        public string id, definitionId, label;
        public PartInstance part;
        public LooseEngineElement unit;
        public EngineAssemblyInstance engine;
        public bool installed;
    }
    // Containers index existing records. Only checkout or an explicit move changes ownership.
    public static class SharedInventory
    {
        public const string Garage = "garage", Storage = "storage", Seller = "parts-dealer";
        public static string StorageOf(string id) => string.IsNullOrEmpty(id) ? Garage : id;
        public static void Ensure(PlayerProfile p)
        {
            if(p.inventory == null) p.inventory = new InventoryState();
            if(p.inventory.storage == null) p.inventory.storage = new List<InventoryContainer>();
            if(p.inventory.dealer == null) p.inventory.dealer = new List<DealerStock>();
            // Unity's inline JSON serializer can materialize an empty object in the
            // unused member. Prefer the real saved identity over the discriminator,
            // which may be stale. Never discard two populated assets or fabricate
            // one from an empty record: Validate will reject both cases.
            foreach(var stock in p.inventory.dealer)
            {
                if(stock==null)continue;
                bool hasPart=stock.HasPart;
                bool hasUnit=stock.HasUnit;
                if(hasPart==hasUnit)continue;
                stock.isUnit=hasUnit;
                if(hasUnit)stock.part=null;
                else stock.unit=null;
            }
            if(p.inventory.storage.Count == 0) {
                p.inventory.storage.Add(new InventoryContainer {kind=InventoryKind.Storage,id=Garage,name="Garage shelves"});
                p.inventory.storage.Add(new InventoryContainer {kind=InventoryKind.Storage,id=Storage,name="Storage shelves"});
            }
        }
        public static IEnumerable<InventoryContainer> Containers(PlayerProfile p)
        {
            foreach(var s in p.inventory.storage) yield return s;
            foreach(var car in p.cars) yield return new InventoryContainer {kind=InventoryKind.Car,id=car.instanceId,name=VehicleCollection.Label(p.catalog,car)+" / fitted"};
            foreach(var engine in p.engines) yield return new InventoryContainer {kind=InventoryKind.Engine,id=engine.instanceId,name=AssemblyOperations.DisplayName(p,engine)+" / #"+engine.instanceId.Substring(Math.Max(0,engine.instanceId.Length-6))};
            yield return new InventoryContainer {kind=InventoryKind.Seller,id=Seller,name="Parts dealer"};
            yield return new InventoryContainer {kind=InventoryKind.Cart,id="cart",name="Planned transactions"};
        }
        public static IEnumerable<InventoryEntry> Entries(PlayerProfile p, InventoryKind kind, string id)
        {
            IEnumerable<PartInstance> parts = Enumerable.Empty<PartInstance>();
            if(kind == InventoryKind.Storage) parts=p.parts.Where(i=>i.location==PartLocation.Inventory && StorageOf(i.storageId)==id);
            if(kind == InventoryKind.Car) {var car=p.cars.Find(c=>c.instanceId==id);if(car!=null)parts=car.installed.slots.Select(s=>p.parts.Find(i=>i.instanceId==s.partInstanceId));}
            if(kind == InventoryKind.Engine) parts=p.parts.Where(i=>i.location==PartLocation.Assembly && i.ownerId==id);
            if(kind == InventoryKind.Seller) parts=p.inventory.dealer.Where(s=>s.IsPart).Select(s=>s.part);
            foreach(var part in parts.Where(i=>i!=null)) yield return new InventoryEntry {id=part.instanceId,definitionId=part.definitionId,label=p.catalog.Find(part.definitionId).name,part=part,installed=part.location!=PartLocation.Inventory};
            var units=kind==InventoryKind.Storage?p.looseEngineElements.Where(u=>StorageOf(u.storageId)==id):kind==InventoryKind.Seller?p.inventory.dealer.Where(s=>s.IsUnit).Select(s=>s.unit):Enumerable.Empty<LooseEngineElement>();
            foreach(var unit in units) yield return new InventoryEntry {id=unit.element.id,definitionId=unit.definitionId,label=unit.element.role,unit=unit};
            if(kind==InventoryKind.Storage)foreach(var e in p.engines.Where(e=>string.IsNullOrEmpty(e.parentVehicleId)&&StorageOf(e.storageId)==id))yield return new InventoryEntry {id=e.instanceId,label=AssemblyOperations.DisplayName(p,e),engine=e};
        }
        public static void Move(PlayerProfile p,string assetId,InventoryKind target,string targetId)
        {
            if(p.runCheckpoints.Any(r=>!r.settled))throw new InvalidOperationException("Close the active run before moving inventory.");
            if(target==InventoryKind.Storage&&!p.inventory.storage.Any(s=>s.id==targetId))throw new InvalidOperationException("Unknown storage container.");
            var unit=p.looseEngineElements.Find(u=>u.element.id==assetId);
            if(unit!=null){if(target!=InventoryKind.Storage)throw new InvalidOperationException("Choose a fitted position to install an individual unit.");unit.storageId=targetId;return;}
            var engine=p.engines.Find(e=>e.instanceId==assetId);
            if(engine!=null){if(target==InventoryKind.Car){if(engine.parentVehicleId==targetId)return;if(!string.IsNullOrEmpty(engine.parentVehicleId))AssemblyOperations.Detach(p,engine.parentVehicleId);AssemblyOperations.Attach(p,assetId,targetId);return;}if(target!=InventoryKind.Storage)throw new InvalidOperationException("Engine destination must be storage or a car.");if(!string.IsNullOrEmpty(engine.parentVehicleId))AssemblyOperations.Detach(p,engine.parentVehicleId);engine.storageId=targetId;return;}
            var item=p.parts.Find(i=>i.instanceId==assetId)??throw new InvalidOperationException("Owned item no longer exists.");
            if(target!=InventoryKind.Storage&&target!=InventoryKind.Car&&target!=InventoryKind.Engine)throw new InvalidOperationException("Use a quote/cart to buy or sell.");
            if(item.location!=PartLocation.Inventory){var owner=p.cars.Find(c=>c.installed.slots.Any(s=>s.partInstanceId==assetId));var category=p.catalog.Find(item.definitionId).category;if(owner!=null){AssemblyOperations.MutableCar(p,owner.instanceId);PartOperations.Remove(p,owner.instanceId,category);}else AssemblyOperations.RemovePart(p,item.ownerId,category);}
            if(target==InventoryKind.Car)PartOperations.Install(p,p.catalog,targetId,assetId);
            else if(target==InventoryKind.Engine)AssemblyOperations.InstallPart(p,targetId,assetId);
            else item.storageId=targetId;
        }
        public static string Validate(PlayerProfile p,HashSet<string> identities)
        {
            if(p.inventory==null||p.inventory.storage==null||p.inventory.dealer==null)return "Missing inventory state.";
            var containers=new HashSet<string>();foreach(var s in p.inventory.storage)if(s==null||s.kind!=InventoryKind.Storage||string.IsNullOrWhiteSpace(s.id)||string.IsNullOrWhiteSpace(s.name)||!containers.Add(s.id))return "Invalid/duplicate storage container.";
            if(!containers.Contains(Garage))return "Garage storage is required.";
            foreach(var item in p.parts.Where(i=>i.location==PartLocation.Inventory))if(!containers.Contains(StorageOf(item.storageId)))return "Unknown part storage.";
            foreach(var unit in p.looseEngineElements)if(!containers.Contains(StorageOf(unit.storageId))||!ValidUnit(p,unit))return "Invalid unit storage/condition.";
            foreach(var e in p.engines.Where(e=>string.IsNullOrEmpty(e.parentVehicleId)))if(!containers.Contains(StorageOf(e.storageId)))return "Unknown engine storage.";
            for(int index=0;index<p.inventory.dealer.Count;index++){
                var stock=p.inventory.dealer[index];
                if(stock==null)return "Invalid dealer stock at row "+index+": missing listing.";
                if(stock.paidPrice<0)return "Invalid dealer stock at row "+index+": negative purchase price.";
                bool hasPart=stock.HasPart;
                bool hasUnit=stock.HasUnit;
                if(hasPart==hasUnit)return "Invalid dealer stock at row "+index
                    +(hasPart?": conflicting part and unit payloads.":": missing part/unit identity.");
                if(stock.isUnit!=hasUnit)return "Invalid dealer stock at row "+index+": wrong part/unit discriminator.";
                if(hasPart){
                    var i=stock.part;
                    string id=i.instanceId;
                    if(i.location!=PartLocation.Inventory)return "Invalid dealer part at row "+index+" ("+id+"): stored part is marked "+i.location+".";
                    string conditionError=i.Validate();
                    if(conditionError!=null)return "Invalid dealer part at row "+index+" ("+id+"): "+conditionError;
                    var definition=p.catalog.Find(i.definitionId);
                    if(definition==null)return "Invalid dealer part at row "+index+" ("+id+"): unknown definition "+i.definitionId+".";
                    if(!identities.Add(id))return "Duplicate dealer part identity at row "+index+": "+id+".";
                    if(definition.category!=PartCategory.Clutch&&i.condition.clutch!=null)return "Dealer part at row "+index+" has unrelated clutch state: "+id+".";
                    foreach(var e in i.condition.elements){
                        if(!EngineElements.FaultAllowed(definition.category,e))return "Invalid dealer descendant at row "+index+" ("+id+"): mismatched type or fault "+e.id+".";
                        if(!identities.Add(e.id))return "Duplicate dealer descendant at row "+index+" ("+id+"): "+e.id+".";
                        if(!string.IsNullOrEmpty(e.definitionId)&&(p.catalog.Find(e.definitionId)==null||p.catalog.Find(e.definitionId).category!=definition.category))
                            return "Invalid dealer descendant specification at row "+index+" ("+id+"): "+e.definitionId+".";
                    }
                    foreach(var t in i.condition.tires)
                        if(!string.IsNullOrEmpty(t.definitionId)&&(p.catalog.Find(t.definitionId)==null||p.catalog.Find(t.definitionId).category!=PartCategory.Tires))
                            return "Invalid dealer tire specification at row "+index+" ("+id+"): "+t.definitionId+".";
                }
                else {var u=stock.unit;if(!ValidUnit(p,u)||!identities.Add(u.element.id))return "Invalid/duplicate dealer unit.";}
            }
            return null;
        }
        static bool ValidUnit(PlayerProfile p,LooseEngineElement u)
        {
            var d=u==null?null:p.catalog.Find(u.definitionId);if(d==null||u.element==null||u.element.Validate()!=null||u.unitPrice<0)return false;
            if(u.tireUnit)return d.category==PartCategory.Tires&&u.element is TireCondition&&u.tire.Validate()==null&&(string.IsNullOrEmpty(u.tire.definitionId)||u.tire.definitionId==u.definitionId);
            return EngineElements.Supported(d.category)&&EngineElements.FaultAllowed(d.category,u.element)&&u.tire==null&&(string.IsNullOrEmpty(u.element.definitionId)||u.element.definitionId==u.definitionId);
        }
    }
    public static class InventoryPricing
    {
        public static int NewSet(PartDefinition d) => d.price;
        public static int NewUnit(PartDefinition d,int count) => Math.Max(1,checked((int)Math.Ceiling(d.price/(double)Math.Max(1,count))));
        public static int BuyUsed(DealerStock stock,ValuationPolicy policy=null) => checked((int)Math.Ceiling(stock.paidPrice*(1+(policy?.dealerResaleMarkup??.35)))+(policy?.dealerHandlingFee??1));
        public static ValuationQuote Sell(PlayerProfile p,string id,bool unit,ValuationPolicy policy,string buyer,long now) => Sell(p,unit?ValuationKind.LooseElement:ValuationKind.LoosePart,id,policy,buyer,now);
        public static ValuationQuote Sell(PlayerProfile p,ValuationKind kind,string id,ValuationPolicy policy,string buyer,long now,IEnumerable<string> retained=null) => Valuations.Capture(p,p.catalog,policy,kind,id,buyer,now,retained);
    }
}
