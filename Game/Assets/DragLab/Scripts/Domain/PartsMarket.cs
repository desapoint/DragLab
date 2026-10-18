using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Data;

namespace DragLab.Domain.Mechanics
{
    [Serializable] public sealed class NewPartStock
    {
        public List<string> definitions = new List<string>();
        public int units, initialUnits, deliveryPercent;
    }
    [Serializable] public sealed class EngineOffer
    {
        public string id, model;
        public bool used;
        public int quantity, price;
        public EngineAssemblyInstance engine;
        public List<PartInstance> parts = new List<PartInstance>();
    }
    [Serializable] public sealed class PartsStockState
    {
        public int version, seed, generation, guaranteeAtRefresh;
        public List<NewPartStock> parts = new List<NewPartStock>();
        public List<EngineOffer> engines = new List<EngineOffer>();
        [NonSerialized] internal Dictionary<string,NewPartStock> index;
    }
    public sealed class SupplyQuote
    {
        public int cost, fee, localUnits, expressUnits;
        public string Summary => expressUnits == 0 ? "In stock" : "Express delivery · +" + DisplayUnits.Money(fee);
    }
    // One persisted market epoch. Previews use a disposable ledger, never replenish live stock.
    public static class PartsMarket
    {
        public static Action<string,double> Measurement;
        static long Stamp()=>Measurement==null?0:System.Diagnostics.Stopwatch.GetTimestamp();
        static void Measured(string phase,long stamp){if(stamp!=0)Measurement?.Invoke(phase,(System.Diagnostics.Stopwatch.GetTimestamp()-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency);}
        public static string ProductKey(PartDefinition d) => MarketCatalogLayout.Key(d);
        // Avalanche adjacent save seeds before splitting the product and quantity streams.
        static uint Stream(PartsStockState state,uint salt)
        {
            unchecked{uint value=(uint)state.seed^((uint)(state.generation+1)*2246822519u)^salt;value^=value>>16;value*=0x85ebca6bu;value^=value>>13;value*=0xc2b2ae35u;value^=value>>16;return value==0?1:value;}
        }
        static double Next(ref uint s) { s^=s<<13;s^=s>>17;s^=s<<5;return s/(double)uint.MaxValue; }
        public static int Count(VehicleSpec spec,PartDefinition d) {if(d.category==PartCategory.Ignition&&!CombustionHardware.Diesel(spec)){spec=spec.Copy();spec.combustion.separateSparkPlugs=true;}if(d.category==PartCategory.FuelSystem&&d.fuelDelivery!=FuelDeliveryHardware.Factory){spec=spec.Copy();FuelingHardware.Apply(spec,d);}return Math.Max(d.category==PartCategory.Tires||d.category==PartCategory.Brakes?4:1,EngineElements.Count(spec,d));}
        public static int Count(PlayerProfile p,OwnedCar car,PartDefinition d) {if(d.category==PartCategory.Tires||d.category==PartCategory.Wheels||d.category==PartCategory.Brakes)return 4;if(!EngineElements.Supported(d.category))return 1;if(car!=null&&p.catalog.Fits(p.catalog.Spec(car.vehicleId),d))return InventoryUnits.Count(p,car,d);return Models(p.catalog).Any(m=>p.catalog.Fits(p.catalog.Spec(m),d))?Count(Reference(p,d),d):1;}
        // Read-only topology projection for a single browser rebuild. Checkout still resolves the actual build.
        public static Func<PartDefinition,int> Counter(PlayerProfile p,OwnedCar car)
        {
            var selected=car==null?null:p.catalog.Spec(car.vehicleId);var specs=Models(p.catalog).Select(p.catalog.Spec).ToArray();
            return d=>{if(d.category==PartCategory.Tires||d.category==PartCategory.Wheels||d.category==PartCategory.Brakes)return 4;if(!EngineElements.Supported(d.category))return 1;var spec=selected!=null&&p.catalog.Fits(selected,d)?selected:specs.FirstOrDefault(v=>p.catalog.Fits(v,d));return spec==null?1:Count(spec,d);};
        }
        public static bool FitsAny(PlayerProfile p,PartDefinition d)=>d!=null&&Models(p.catalog).Any(m=>p.catalog.Fits(p.catalog.Spec(m),d));
        public static VehicleSpec Reference(PlayerProfile p,PartDefinition d) {string model=Models(p.catalog).FirstOrDefault(m=>p.catalog.Fits(p.catalog.Spec(m),d));if(model==null)throw new InvalidOperationException("No supported engine or vehicle accepts this part.");return p.catalog.Resolve(model,p.catalog.Recipe(model),new Tune()).spec;}
        static string[] Models(MechanicalCatalog c) => c.parts.Where(d=>d.category==PartCategory.CylinderBlock).Select(d=>d.id.Split('/')[0]).Distinct().OrderBy(s=>s,StringComparer.Ordinal).ToArray();
        public static void Ensure(PlayerProfile p,bool allowMigration=false)
        {
            if(p.inventory.market==null||p.inventory.market.version==0&&p.inventory.market.seed==0&&p.inventory.market.generation==0&&(p.inventory.market.parts==null||p.inventory.market.parts.Count==0)&&(p.inventory.market.engines==null||p.inventory.market.engines.Count==0)){if(!allowMigration)throw new InvalidOperationException("Missing current parts market; recover the save backup.");Generate(p,false);}
            else if(p.inventory.market.seed!=p.vehicleStock.seed||p.inventory.market.generation!=p.vehicleStock.generation)throw new InvalidOperationException("Parts and car market epochs disagree.");
        }
        public static void Refresh(PlayerProfile p) { if(p.inventory!=null)Generate(p,true); }
        static void Generate(PlayerProfile p,bool expire){foreach(var unused in GenerateSteps(p,expire)){} }
        public static IEnumerable<object> RefreshSteps(PlayerProfile p)=>GenerateSteps(p,true);
        static IEnumerable<object> GenerateSteps(PlayerProfile p,bool expire)
        {
            if(expire){var retained=p.inventory.dealer.Where(s=>s.Id!=null&&CartReservations.HoldsUsed(p,s.Id)).ToArray();p.inventory.dealer.Clear();p.inventory.dealer.AddRange(retained);}
            var state=new PartsStockState{version=1,seed=p.vehicleStock.seed,generation=p.vehicleStock.generation,guaranteeAtRefresh=p.driving.guaranteedFittingParts};p.inventory.market=state;
            uint random=Stream(state,0x91e10da5u),newQuantity=Stream(state,0x27d4eb2fu),usedRandom=Stream(state,0x165667b1u),usedQuantity=Stream(state,0x85ebca6bu),engineRandom=Stream(state,0xc2b2ae35u),engineQuantity=Stream(state,0xd3a2646cu),coverageRandom=Stream(state,0xfd7046c5u),coverageQuantity=Stream(state,0xb55a4f09u);
            long stamp=Stamp();var productKeys=new Dictionary<string,string>(StringComparer.Ordinal);int indexed=0;foreach(var definition in p.catalog.parts){productKeys.Add(definition.id,ProductKey(definition));if(++indexed%32==0)yield return "parts/product keys";}Measured("product keys",stamp);stamp=Stamp();yield return "parts/product keys";
            var models=Models(p.catalog);var specs=models.Select(p.catalog.Spec).ToArray();
            var selectedNew=new List<KeyValuePair<NewPartStock,int>>();
            var layout=MarketCatalogLayout.Get(p.catalog,productKeys,specs);
            // Select products before quantities, from one global catalogue rather than one quota per car.
            int newCount=Math.Min(layout.groups.Length,24+(int)(Next(ref random)*21)%21);
            var newCandidates=layout.groups.ToList();var chosen=new HashSet<MarketCatalogLayout.Group>();
            for(int n=0;n<newCount;n++){int pick=(int)(Next(ref random)*newCandidates.Count)%newCandidates.Count;chosen.Add(newCandidates[pick]);newCandidates.RemoveAt(pick);}
            foreach(var group in layout.groups){
                var row=new NewPartStock{definitions=new List<string>(group.ids),deliveryPercent=5+(int)(Next(ref random)*6)%6};state.parts.Add(row);if(chosen.Contains(group))selectedNew.Add(new KeyValuePair<NewPartStock,int>(row,group.maximum));
            }
            foreach(var selected in selectedNew){double roll=Next(ref newQuantity);int sets=selected.Value>1?(roll<.5?1:roll<.85?2:3):(roll<.8?1:roll<.97?2:3);selected.Key.units=selected.Key.initialUnits=selected.Value*sets;}
            Measured("new product selection/quantities",stamp);stamp=Stamp();yield return "parts/layout and selection";
            // A single shared pool: no model quotas for used parts or engines.
            var owned=p.cars.Select(c=>c.vehicleId).Distinct().ToArray();var targets=owned.Length==0?specs:owned.Select(p.catalog.Spec).ToArray();
            var fitting=state.parts.Where(row=>row.definitions.Any(id=>{var d=p.catalog.Find(id);return d.reputationRequired==0&&targets.Any(spec=>p.catalog.Fits(spec,d));})).ToList();
            while(fitting.Count(row=>row.units>0)<Math.Min(state.guaranteeAtRefresh,fitting.Count)){
                var missing=fitting.Where(row=>row.units==0).ToArray();var row=missing[(int)(Next(ref coverageRandom)*missing.Length)%missing.Length];var d=p.catalog.Find(row.definitions[0]);int max=specs.Where(spec=>p.catalog.Fits(spec,d)).Select(spec=>Count(spec,d)).DefaultIfEmpty(1).Max();row.units=row.initialUnits=max*(1+(int)(Next(ref coverageQuantity)*3)%3);
            }
            Measured("new fitting guarantee",stamp);stamp=Stamp();yield return "parts/new guarantee";string prefix="market/"+state.seed+"/"+state.generation+"/";
            var usedCandidates=layout.groups.Where(g=>PartSemantics.Wears(g.definition.category)).ToList();
            int usedCount=Math.Min(usedCandidates.Count,state.parts.Count(row=>row.units>0)+12+(int)(Next(ref usedRandom)*13)%13);
            var selectedUsed=new List<Tuple<VehicleSpec,PartDefinition>>();
            for(int n=0;n<usedCount;n++){
                int pick=(int)(Next(ref usedRandom)*usedCandidates.Count)%usedCandidates.Count;var group=usedCandidates[pick];usedCandidates.RemoveAt(pick);
                var d=p.catalog.Find(group.ids[(int)(Next(ref usedRandom)*group.ids.Count)%group.ids.Count]);
                string model=d.id.Split('/')[0];selectedUsed.Add(Tuple.Create(layout.Stock(p.catalog,model),d));
            }
            for(int n=0;n<selectedUsed.Count;n++){AddUsedSupply(p,selectedUsed[n].Item1,selectedUsed[n].Item2,prefix+"used/"+n,ref usedQuantity);yield return "parts/used supply";}
            if(owned.Length>0&&state.guaranteeAtRefresh>0){
                var generated=p.inventory.dealer.Where(row=>row.Id.StartsWith(prefix+"used/",StringComparison.Ordinal)).ToArray();
                var present=new HashSet<string>(generated.Where(row=>{var d=p.catalog.Find(row.DefinitionId);return d.reputationRequired<=p.reputation&&targets.Any(spec=>p.catalog.Fits(spec,d));}).Select(row=>productKeys[row.DefinitionId]),StringComparer.Ordinal);
                var candidates=p.catalog.parts.Where(d=>PartSemantics.Wears(d.category)&&d.reputationRequired<=p.reputation&&targets.Any(spec=>p.catalog.Fits(spec,d))).GroupBy(d=>productKeys[d.id],StringComparer.Ordinal).OrderBy(g=>g.Min(d=>d.id),StringComparer.Ordinal).Select(g=>g.OrderBy(d=>d.id,StringComparer.Ordinal).First()).ToList();
                while(present.Count<state.guaranteeAtRefresh){var remaining=candidates.Where(d=>!present.Contains(productKeys[d.id])).ToArray();if(remaining.Length==0)break;var d=remaining[(int)(Next(ref coverageRandom)*remaining.Length)%remaining.Length];var platforms=targets.Where(spec=>p.catalog.Fits(spec,d)).ToArray();var source=platforms[(int)(Next(ref coverageRandom)*platforms.Length)%platforms.Length];var spec=layout.Stock(p.catalog,source.id);AddUsedSupply(p,spec,d,prefix+"used/guaranteed/"+present.Count,ref coverageQuantity);present.Add(productKeys[d.id]);yield return "parts/used guarantee";}
            }
            Measured("used parts/guarantee",stamp);stamp=Stamp();int engines=Next(ref engineRandom)<.25?0:1+(int)(Next(ref engineRandom)*6)%6;var newModels=new HashSet<string>();var selectedEngines=new List<Tuple<string,bool>>();
            for(int n=0;n<engines;n++){
                string model=models[(int)(Next(ref engineRandom)*models.Length)%models.Length];bool used=Next(ref engineRandom)<.6;if(!used&&!newModels.Add(model))used=true;selectedEngines.Add(Tuple.Create(model,used));
            }
            for(int n=0;n<selectedEngines.Count;n++){state.engines.Add(CreateEngineOffer(p,selectedEngines[n].Item1,selectedEngines[n].Item2,prefix+"engine/"+n,ref engineQuantity));yield return "parts/engine offer";}
            Measured("engine offers",stamp);
        }
        public static EngineOffer CreateEngineOffer(PlayerProfile p,string model,bool used,string id,ref uint random)
        {
            var definition=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").First(v=>v.spec.id==model);
            double quantityRoll=Next(ref random);var offer=new EngineOffer{id=id,model=model,used=used,quantity=used?1:quantityRoll<.75?1:quantityRoll<.95?2:3};
            float health=used?(float)(Math.Floor((.45+Next(ref random)*.5)*1024)/1024):1;
            var tune=definition.CreateStockTune();var recipe=p.catalog.Recipe(model);var resolved=p.catalog.Resolve(model,recipe,tune);
            offer.engine=new EngineAssemblyInstance{instanceId=offer.id+"/assembly",vehicleDefinitionId=model,storageId=SharedInventory.Garage,fluids=FluidState.Full(resolved.spec.fluids),tune=resolved.tune.Copy()};
            if(used){offer.engine.fluids.oilCondition=health;offer.engine.fluids.oilLitres*=.65+Next(ref random)*.35;offer.engine.fluids.coolantLitres*=.65+Next(ref random)*.35;offer.engine.fluids.oilLitres=Math.Floor(offer.engine.fluids.oilLitres*1024)/1024;offer.engine.fluids.coolantLitres=Math.Floor(offer.engine.fluids.coolantLitres*1024)/1024;}
            foreach(var pair in recipe.Where(v=>MechanicalCatalog.EngineSlot(v.Key)).OrderBy(v=>v.Key)){
                var d=p.catalog.Find(pair.Value);var part=CreatePart(resolved.spec,d,offer.id+"/"+pair.Key,PartSemantics.Wears(d.category)?health:1);
                part.location=PartLocation.Assembly;part.ownerId=offer.engine.instanceId;part.slot=pair.Key.ToString();offer.parts.Add(part);offer.engine.slots.Add(new SlotBinding{slot=part.slot,partInstanceId=part.instanceId});
            }
            offer.price=Math.Max(1,(int)Math.Ceiling(offer.parts.Sum(i=>(double)p.catalog.Find(i.definitionId).price)*(used?.4+.6*health:1.2)));return offer;
        }
        public static void AddUsedSupply(PlayerProfile p,VehicleSpec spec,PartDefinition d,string id,ref uint random)
        {
            float health=(float)(Math.Floor((.35+Next(ref random)*.6)*1024)/1024);var pack=CreatePart(spec,d,id,health);int count=pack.condition.elements.Length>0?pack.condition.elements.Length:d.category==PartCategory.Tires?4:1;double roll=Next(ref random);
            if(count<=1||roll<.7){p.inventory.dealer.Add(new DealerStock{part=pack,paidPrice=Math.Max(1,(int)Math.Ceiling(d.price*.48*health))});return;}
            int quantity=roll<.9&&count>2?2+(int)(Next(ref random)*Math.Min(3,count-2))%Math.Min(3,count-2):1;
            var positions=Enumerable.Range(0,count).ToList();
            for(int n=0;n<quantity;n++){
                int pick=(int)(Next(ref random)*positions.Count)%positions.Count,index=positions[pick];positions.RemoveAt(pick);
                var tire=d.category==PartCategory.Tires?pack.condition.tires[index].Copy():null;
                var element=tire==null?pack.condition.elements[index].Copy():(EngineElement)tire; if(tire!=null)element.cylinder=element.valve=-1;
                int unitPrice=InventoryPricing.NewUnit(d,count);var unit=new LooseEngineElement{definitionId=d.id,unitPrice=unitPrice,element=element,tireUnit=tire!=null,tire=tire};p.inventory.dealer.Add(new DealerStock{isUnit=true,unit=unit,paidPrice=Math.Max(1,(int)Math.Ceiling(unitPrice*.48*health))});
            }
        }
        static PartInstance CreatePart(VehicleSpec spec,PartDefinition d,string id,float health)
        {
            if(d.category==PartCategory.FuelSystem&&d.fuelDelivery!=FuelDeliveryHardware.Factory){spec=spec.Copy();FuelingHardware.Apply(spec,d);}
            var item=new PartInstance{instanceId=id,definitionId=d.id,condition=new ComponentCondition{health=health,distanceM=Math.Round((1-health)*80000000),runningSeconds=Math.Round((1-health)*1000000)}};
            EngineElements.Ensure(spec,d,item);ClutchHardware.Ensure(d,item.condition);
            if(d.category==PartCategory.Tires){TireDynamics.SeedNew(item.condition);for(int i=0;i<4;i++){var t=item.condition.tires[i];t.id=id+"/tire/"+i;t.definitionId=d.id;}}
            return item;
        }
        public sealed class Ledger
        {
            readonly PlayerProfile owner;
            readonly Dictionary<string,NewPartStock> stocks=new Dictionary<string,NewPartStock>(StringComparer.Ordinal);
            readonly Dictionary<NewPartStock,int> remaining=new Dictionary<NewPartStock,int>();
            public Ledger(PlayerProfile p,bool includeReserved=false){owner=p;bool reserved=CartReservations.Active(p);foreach(var s in p.inventory.market.parts){remaining[s]=s.units;if(reserved){var d=p.catalog.Find(s.definitions[0]);remaining[s]=Math.Max(0,s.units-CartReservations.Held(p,d,true))+(includeReserved?CartReservations.Held(p,d):0);}foreach(string id in s.definitions)stocks[id]=s;}}
            public SupplyQuote Take(PartDefinition d,int units,int basePrice,int quantity=1,bool consume=false)
            {
                stocks.TryGetValue(d.id,out var stock);if(stock==null)stock=Stock(owner,d);int available=stock==null?0:remaining[stock];var q=Calculate(available,stock?.deliveryPercent??10,units,basePrice,quantity);
                if(stock!=null){remaining[stock]=available-q.localUnits;if(consume)stock.units=remaining[stock];}return q;
            }
        }
        internal static NewPartStock Stock(PlayerProfile p,PartDefinition d)
        {
            var m=p.inventory.market;if(m.index==null){m.index=new Dictionary<string,NewPartStock>(StringComparer.Ordinal);foreach(var s in m.parts)foreach(string id in s.definitions)m.index.Add(id,s);}if(m.index.TryGetValue(d.id,out var stock))return stock;string key=ProductKey(d);stock=m.parts.FirstOrDefault(row=>row.definitions.Any(id=>{var existing=p.catalog.Find(id);return existing!=null&&ProductKey(existing)==key;}));if(stock!=null)m.index[d.id]=stock;return stock;
        }
        static SupplyQuote Calculate(int available,int percent,int units,int basePrice,int quantity)
        {
            if(available<0||percent<5||percent>10||units<1||basePrice<0||quantity<1||quantity>99)throw new InvalidOperationException("Invalid supply request.");var q=new SupplyQuote();
            for(int i=0;i<quantity;i++){int local=Math.Min(units,available),express=units-local;available-=local;int fee=express==0?0:checked((int)Math.Ceiling(basePrice*(express/(double)units)*percent/100.0));q.localUnits+=local;q.expressUnits+=express;q.fee=checked(q.fee+fee);q.cost=checked(q.cost+basePrice+fee);}return q;
        }
        public static SupplyQuote Quote(PlayerProfile p,PartDefinition d,int count,bool unit=false,int quantity=1){var stock=Stock(p,d);int available=Available(p,d)+(p.inventory.usingCartReservation?CartReservations.Held(p,d):0);return Calculate(available,stock?.deliveryPercent??10,unit?1:count,unit?InventoryPricing.NewUnit(d,count):d.price,quantity);}
        public static SupplyQuote Consume(PlayerProfile p,PartDefinition d,int count,bool unit=false)
        {
            var q=Quote(p,d,count,unit);var stock=Stock(p,d);int left=q.localUnits;
            if(p.inventory.usingCartReservation&&CartReservations.Active(p))foreach(var hold in p.inventory.cart.supply.Where(s=>s.definitionId==d.id||stock!=null&&stock.definitions.Contains(s.definitionId)).OrderBy(s=>s.generation)){int take=Math.Min(left,hold.units);hold.units-=take;left-=take;if(stock!=null&&hold.generation==p.inventory.market.generation)stock.units-=take;}
            if(stock!=null)stock.units-=left;return q;
        }
        public static int Available(PlayerProfile p,PartDefinition d) => Math.Max(0,(Stock(p,d)?.units??0)-CartReservations.Held(p,d,true));
        public static int EnginePrice(EngineOffer offer) => checked(offer.price+(offer.quantity==0&&!offer.used?(int)Math.Ceiling(offer.price*.1):0));
        public static string BuyEngine(PlayerProfile p,string id,int expectedPrice)
        {
            if(p.runCheckpoints.Any(r=>!r.settled))throw new InvalidOperationException("Close the active run first.");
            var offer=p.inventory.market.engines.Find(o=>o.id==id)??throw new InvalidOperationException("Engine stock changed.");
            int price=EnginePrice(offer);if(offer.used&&offer.quantity==0||price!=expectedPrice||p.cash<price||offer.parts.Any(i=>p.reputation<p.catalog.Find(i.definitionId).reputationRequired))throw new InvalidOperationException("Engine unavailable, price changed or insufficient cash/REP.");
            var engine=JsonUtility.FromJson<EngineAssemblyInstance>(JsonUtility.ToJson(offer.engine));string newId=offer.used?engine.instanceId:MechanicsOwnership.NewId("engine");engine.instanceId=newId;engine.slots.Clear();
            foreach(var original in offer.parts){var item=JsonUtility.FromJson<PartInstance>(JsonUtility.ToJson(original));string old=item.instanceId;item.instanceId=offer.used?item.instanceId:MechanicsOwnership.NewId("part");item.ownerId=newId;
                foreach(var element in item.condition.elements)element.id=item.instanceId+element.id.Substring(old.Length);
                foreach(var tire in item.condition.tires)if(!string.IsNullOrEmpty(tire.id))tire.id=item.instanceId+tire.id.Substring(old.Length);
                p.parts.Add(item);engine.slots.Add(new SlotBinding{slot=item.slot,partInstanceId=item.instanceId});
            }
            p.engines.Add(engine);p.cash-=price;if(offer.quantity>0)offer.quantity--;if(offer.used)p.inventory.market.engines.Remove(offer);return newId;
        }
        public static string Validate(PlayerProfile p)
        {
            var m=p.inventory.market;
            if(m==null||m.version!=1||m.seed!=p.vehicleStock.seed||m.generation!=p.vehicleStock.generation||m.guaranteeAtRefresh<0||m.guaranteeAtRefresh>20||m.parts==null||m.engines==null)return "Invalid parts market epoch.";
            var ids=new HashSet<string>();foreach(var s in m.parts)if(s==null||s.definitions==null||s.definitions.Count==0||s.units<0||s.units>s.initialUnits||s.initialUnits<0||s.deliveryPercent<5||s.deliveryPercent>10||s.definitions.Any(id=>p.catalog.Find(id)==null||!ids.Add(id)))return "Invalid new-parts stock.";
            string reservationError=CartReservations.Validate(p);if(reservationError!=null)return reservationError;
            ids=new HashSet<string>(p.parts.SelectMany(i=>i.ItemIds).Concat(p.engines.Select(e=>e.instanceId)).Concat(p.looseEngineElements.Select(u=>u.element.id)).Concat(p.inventory.dealer.SelectMany(s=>s.IsPart?s.part.ItemIds:s.IsUnit?new[]{s.unit.element.id}:Enumerable.Empty<string>())).Concat(p.vehicleStock.listings.SelectMany(o=>o.parts.SelectMany(i=>i.ItemIds))));foreach(var o in m.engines){if(o==null||string.IsNullOrWhiteSpace(o.id)||!ids.Add(o.id)||!ids.Add(o.engine?.instanceId??"")||o.quantity<0||o.quantity>3||o.used&&o.quantity>1||o.price<1||o.engine==null||o.engine.vehicleDefinitionId!=o.model||!string.IsNullOrEmpty(o.engine.parentVehicleId)||o.parts==null||o.parts.Count==0||o.engine.slots==null||o.engine.slots.Count!=o.parts.Count)return "Invalid engine stock.";
                var map=new Dictionary<PartCategory,string>();foreach(var i in o.parts){var d=p.catalog.Find(i.definitionId);if(d==null||!MechanicalCatalog.EngineSlot(d.category)||!p.catalog.Fits(p.catalog.Spec(o.model),d)||i.Validate()!=null||!ids.Add(i.instanceId)||i.ownerId!=o.engine.instanceId||i.location!=PartLocation.Assembly||map.ContainsKey(d.category)||!o.engine.slots.Any(s=>s.partInstanceId==i.instanceId&&s.slot==d.category.ToString()))return "Invalid offered engine component.";map.Add(d.category,d.id);foreach(var e in i.condition.elements)if(!ids.Add(e.id)||!EngineElements.FaultAllowed(d.category,e))return "Invalid offered engine descendant.";}
                if(p.catalog.Recipe(o.model).Any(v=>MechanicalCatalog.EngineSlot(v.Key)&&!map.ContainsKey(v.Key))||o.engine.fluids==null||o.engine.fluids.Validate(p.catalog.Spec(o.model).fluids)!=null||o.engine.tune==null)return "Incomplete offered engine.";
            }return null;
        }
    }
}
