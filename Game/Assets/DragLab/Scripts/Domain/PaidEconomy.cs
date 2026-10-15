using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace DragLab.Domain.Mechanics
{
    [Serializable] public sealed class OperatingPolicy
    {
        public string id = "prototype-operating-costs"; public int version = 1, quoteSeconds = 300;
        public double fuelPerLitre = 2, oilPerLitre = 6, coolantPerLitre = 3, clutchOilPerLitre = 6;
        public int vehicleInspectionFee=50;
        public int repairLabour = 15, oilChangeLabour = 25, coolantFlushLabour = 20;
        public int rebuildBaseLabour=150,rebuildPerCylinder=25;
        public double repairPriceFraction = .08, dealerSpread = .1;
        public double newVehiclePremium = .5;
        public int recoverySeconds = 60, recoveryReward = 250, recoveryCashLimit = 10000;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || version < 1 || quoteSeconds < 1 || quoteSeconds > 86400 || vehicleInspectionFee<0 || repairLabour < 0 || oilChangeLabour < 0 || coolantFlushLabour < 0 || rebuildBaseLabour<0||rebuildPerCylinder<0||recoverySeconds < 1 || recoveryReward < 1 || recoveryCashLimit < 1) throw new InvalidOperationException("Invalid operating policy.");
            foreach (double v in new[] { fuelPerLitre, oilPerLitre, coolantPerLitre, clutchOilPerLitre, repairPriceFraction, dealerSpread }) if (!MechanicsSchema.Finite(v) || v < 0 || v > 1000) throw new InvalidOperationException("Invalid operating price/factor.");
            if(!MechanicsSchema.Finite(newVehiclePremium)||newVehiclePremium<0||newVehiclePremium>10)throw new InvalidOperationException("Invalid new-vehicle premium.");
        }
    }
    [Serializable] public sealed class EconomyReceipt
    {
        public string id, operation, target; public long revision, utcTicks;
        public int cashBefore, cashAfter; public double fuelAdded, oilAdded, coolantAdded;
        public string[] disposed = new string[0], retained = new string[0];
    }
    [Serializable] public sealed class RecoveryWork
    { public string id; public long startedUtcTicks, readyUtcTicks; public int reward; }
    public enum PaymentKind { Fuel, Fluids, OilChange, CoolantFlush, Repair, ClutchOil, Part, Car, ElementNew, ElementReuse, Rebuild, VehicleInspection }
    [Serializable] public sealed class PaymentRequest
    {
        public bool preserveTireFitments;
        public PaymentKind kind; public string carId, targetId, auxiliaryId, spareId; public bool install;
        public double litres, coolantLitres; public ServiceAction service; public VehicleOffer offer;
    }
    public sealed class PaymentAmounts
    {
        public int cost; public double fuel, oil, coolant; public string description;
    }
    public sealed class PaymentQuote
    {
        public string Id { get; } public long Revision { get; } public long Issued { get; } public long Expires { get; }
        public int Cost { get; } public double Fuel { get; } public double Oil { get; } public double Coolant { get; } public string Description { get; }
        internal string RequestJson { get; } internal string StateHash { get; } internal string ProfileJson { get; }
        internal PaymentQuote(PlayerProfile p, OperatingPolicy policy, PaymentRequest request, PaymentAmounts a, string hash, long now)
        { Id = MechanicsOwnership.NewId("payment"); Revision = p.revision; Issued = now; Expires = checked(now + TimeSpan.FromSeconds(policy.quoteSeconds).Ticks); Cost = a.cost; Fuel = a.fuel; Oil = a.oil; Coolant = a.coolant; Description = a.description; RequestJson = JsonUtility.ToJson(request); StateHash = hash; ProfileJson=JsonUtility.ToJson(p); }
    }
    public static class PaidEconomy
    {
        public const int StateVersion = 1;
        public static void Ensure(PlayerProfile p)
        {
            if (p.economyVersion > StateVersion) throw new InvalidOperationException("Unsupported economy version.");
            if (p.economyReceipts == null) p.economyReceipts = new List<EconomyReceipt>();
            // Unity materializes null inline serializable classes as an entirely empty object.
            var w = p.recoveryWork;
            if (w != null && string.IsNullOrEmpty(w.id) && w.startedUtcTicks == 0 && w.readyUtcTicks == 0 && w.reward == 0) p.recoveryWork = null;
            p.economyVersion = StateVersion;
        }
        public static string ValidateState(PlayerProfile p)
        {
            if (p.economyVersion != StateVersion || p.economyReceipts == null || p.economyReceipts.Count > 256 || p.lastEconomyUtcTicks < 0 || p.lastEconomyUtcTicks > DateTime.MaxValue.Ticks) return "Invalid economy state.";
            var seen = new HashSet<string>();
            foreach (var r in p.economyReceipts) if (r == null || string.IsNullOrEmpty(r.id) || !seen.Add(r.id) || string.IsNullOrEmpty(r.operation) || r.revision < 0 || r.revision > p.revision || r.utcTicks <= 0 || r.utcTicks > p.lastEconomyUtcTicks || r.cashBefore < 0 || r.cashAfter < 0 || r.disposed == null || r.retained == null || r.disposed.Distinct().Count() != r.disposed.Length || r.retained.Distinct().Count() != r.retained.Length || r.disposed.Intersect(r.retained).Any() || !MechanicsSchema.Finite(r.fuelAdded) || !MechanicsSchema.Finite(r.oilAdded) || !MechanicsSchema.Finite(r.coolantAdded) || r.fuelAdded < 0 || r.oilAdded < 0 || r.coolantAdded < 0) return "Invalid/duplicate economy receipt.";
            var w = p.recoveryWork; if (w != null && (string.IsNullOrEmpty(w.id) || w.startedUtcTicks <= 0 || w.readyUtcTicks <= w.startedUtcTicks || w.readyUtcTicks > DateTime.MaxValue.Ticks || w.reward < 1 || seen.Contains(w.id))) return "Invalid recovery work.";
            return null;
        }
        static void Guard(PlayerProfile p, long now, string id)
        {
            if (now <= 0 || now > DateTime.MaxValue.Ticks - TimeSpan.TicksPerDay || now < p.lastEconomyUtcTicks) throw new InvalidOperationException("Clock moved backwards or outside supported range. Wait for the saved clock before paying/claiming.");
            if (p.runCheckpoints.Any(r => !r.settled)) throw new InvalidOperationException("Close the active run before changing assets or resources.");
            if (p.economyReceipts.Any(r => r.id == id)) throw new InvalidOperationException("Operation already settled.");
        }
        static OwnedCar Car(PlayerProfile p, string id)
        { var c = p.cars.Find(v => v.instanceId == id); EngineRebuilds.RequireAvailable(c); return c; }
        static PartInstance Installed(PlayerProfile p, OwnedCar c, string id)
        { var item = p.parts.Find(i => i.instanceId == id); if (item == null || !c.installed.slots.Any(s => s.partInstanceId == id)) throw new InvalidOperationException("Service item no longer installed."); return item; }
        static int Money(double value)
        { if (!MechanicsSchema.Finite(value) || value < 0 || value > int.MaxValue) throw new InvalidOperationException("Price outside supported currency range."); return checked((int)Math.Ceiling(value - 1e-9)); }
        static double Quantity(double requested, double free)
        { if (!MechanicsSchema.Finite(requested) || requested < 0) throw new InvalidOperationException("Quantity must be finite and nonnegative."); return Math.Min(requested, Math.Max(0, free)); }
        public static int DealerPrice(MechanicalCatalog catalog, OperatingPolicy costs, ValuationPolicy values, VehicleOffer offer)
        {
            costs.Validate(); values.Validate(); if (offer == null || offer.price <= 0 || offer.cataloguePrice<0 || offer.stock == null || offer.reputation < 0) throw new InvalidOperationException("Invalid dealership offer.");
            var spec = catalog.Spec(offer.model); var model = values.models.Find(m => m.model == offer.model);
            double bare = model == null ? values.defaultBareChassis : model.bareChassis;
            double engine = 0, road = 0;
            foreach (var slot in catalog.Recipe(offer.model, offer.stock)) {
                var d = catalog.Find(slot.Value); var quality = values.qualities.Find(q => q.quality == d.quality);
                double loose = d.price * values.usedPartFraction * (quality == null ? 1 : quality.factor);
                if (MechanicalCatalog.EngineSlot(slot.Key)) engine += loose; else road += loose;
            }
            // A provisional package floor prevents buying cheap OEM cars merely to dispose of the
            // same stock contents. Retains authored MSRP whenever it already exceeds this floor.
            double engineOffer = values.buyers.Max(b => engine * b.parts * Math.Max(1, b.engine) * (model == null ? 1 : model.engineDemand));
            double chassisOffer = values.buyers.Max(b => (road * b.parts + bare) * Math.Max(1, b.chassis) * (model == null ? 1 : model.chassisDemand));
            double carOffer = values.buyers.Max(b => ((engine + road) * b.parts + bare) * Math.Max(1, b.car) * (model == null ? 1 : model.carDemand));
            if(offer.parts!=null&&offer.parts.Count>0){
                engineOffer=chassisOffer=carOffer=0;
                foreach(var buyer in values.buyers){double e=0,r=0;foreach(var p in offer.parts){var d=catalog.Find(p.definitionId);double value=Valuations.PartValue(p,d,values,buyer,new List<ValuationLine>(),catalog);if(MechanicalCatalog.EngineSlot(d.category))e+=value;else r+=value;}
                    engineOffer=Math.Max(engineOffer,e*Math.Max(1,buyer.engine)*(model?.engineDemand??1));chassisOffer=Math.Max(chassisOffer,(r+bare)*Math.Max(1,buyer.chassis)*(model?.chassisDemand??1));carOffer=Math.Max(carOffer,(e+r+bare)*Math.Max(1,buyer.car)*(model?.carDemand??1));}
            }
            double suppliedFluids = (offer.parts!=null&&offer.parts.Count>0?offer.fuel.litres:spec.fuel.capacityLitres) * costs.fuelPerLitre + (offer.parts!=null&&offer.parts.Count>0?offer.fluids.oilLitres:spec.fluids.oilCapacityLitres) * costs.oilPerLitre + (offer.parts!=null&&offer.parts.Count>0?offer.fluids.coolantLitres:spec.fluids.coolantCapacityLitres) * costs.coolantPerLitre;
            double packageFloor=Math.Max(engineOffer + chassisOffer, carOffer) * (1 + costs.dealerSpread) + suppliedFluids;
            // Prior ownership remains used even after every component has been replaced.
            // Keep MSRP separate from the saved ask so repeated quotes cannot compound the premium.
            if(!offer.used)packageFloor=Math.Max(offer.cataloguePrice>0?offer.cataloguePrice:offer.price,packageFloor)*(1+costs.newVehiclePremium);
            return Money(Math.Max(offer.price,packageFloor));
        }
        public static PaymentAmounts Preview(PlayerProfile p, MechanicalCatalog cat, OperatingPolicy policy, ValuationPolicy prices, PaymentRequest request, long now)
        {
            policy.Validate(); if (request == null || !Enum.IsDefined(typeof(PaymentKind), request.kind)) throw new InvalidOperationException("Invalid payment request.");
            var a = new PaymentAmounts { description = request.kind.ToString() };
            if (request.kind == PaymentKind.Car) { VehicleListings.RequireCurrent(p,request.offer);a.cost = DealerPrice(cat, policy, prices, request.offer); a.fuel = request.offer.parts!=null&&request.offer.parts.Count>0?request.offer.fuel.litres:cat.Spec(request.offer.model).fuel.capacityLitres; a.oil = request.offer.parts!=null&&request.offer.parts.Count>0?request.offer.fluids.oilLitres:cat.Spec(request.offer.model).fluids.oilCapacityLitres; a.coolant = request.offer.parts!=null&&request.offer.parts.Count>0?request.offer.fluids.coolantLitres:cat.Spec(request.offer.model).fluids.coolantCapacityLitres; return a; }
            if(request.kind==PaymentKind.VehicleInspection){VehicleListings.RequireCurrent(p,request.offer);if(string.IsNullOrEmpty(request.offer.listingId)||VehicleInspection.Known(request.offer))throw new InvalidOperationException("This listing is already fully disclosed.");a.cost=policy.vehicleInspectionFee;a.description="Inspect used vehicle";return a;}
            var car = Car(p, request.carId); var build = MechanicsOwnership.Resolve(p, car, cat);
            if (request.kind == PaymentKind.Part) { var d = cat.Find(request.targetId); if (d == null || !cat.Fits(cat.Spec(car.vehicleId), d) || d.price < 0 || p.reputation<d.reputationRequired) throw new InvalidOperationException("Incompatible/unavailable part purchase; check reputation requirement."); a.cost = request.preserveTireFitments&&request.install&&d.category==PartCategory.Tires?TireFitments.ReplacementCost(p,car,d.id):PartsMarket.Quote(p,d,InventoryUnits.Count(p,car,d)).cost; a.description="Part replacement · stock / express delivery included in total";return a; }
            if (request.kind == PaymentKind.Fuel) { a.fuel = Quantity(request.litres, cat.Spec(car.vehicleId).fuel.capacityLitres - car.fuel.litres); a.cost = Money(a.fuel * policy.fuelPerLitre); return a; }
            if (request.kind == PaymentKind.Fluids || request.kind == PaymentKind.OilChange || request.kind == PaymentKind.CoolantFlush) {
                if (string.IsNullOrEmpty(car.engineAssemblyId)) throw new InvalidOperationException("No attached engine to service.");
                if (request.kind == PaymentKind.Fluids) { a.oil = Quantity(request.litres, build.spec.fluids.oilCapacityLitres - car.fluids.oilLitres); a.coolant = Quantity(request.coolantLitres, build.spec.fluids.coolantCapacityLitres - car.fluids.coolantLitres); a.cost = Money(a.oil * policy.oilPerLitre + a.coolant * policy.coolantPerLitre); }
                if (request.kind == PaymentKind.OilChange) { a.oil = build.spec.fluids.oilCapacityLitres; a.cost = checked(Money(a.oil * policy.oilPerLitre) + policy.oilChangeLabour + PartsMarket.Quote(p,ServiceOperations.OilFilter(p,car),1).cost); }
                if (request.kind == PaymentKind.CoolantFlush) { a.coolant = build.spec.fluids.coolantCapacityLitres; a.cost = checked(Money(a.coolant * policy.coolantPerLitre) + policy.coolantFlushLabour); }
                return a;
            }
            if (request.kind == PaymentKind.Rebuild) { a.cost = checked(EngineRebuilds.Quote(p, car, now,policy).paidPrice + Money(build.spec.fluids.oilCapacityLitres * policy.oilPerLitre)); return a; }
            var item = Installed(p, car, request.targetId); var definition = cat.Find(item.definitionId);
            if (request.kind == PaymentKind.Repair) { if (!ServiceOperations.Available(definition,item.condition,ServiceOperations.BoreHealth(p,car)).Contains(request.service)) throw new InvalidOperationException("Service is not applicable to this damage."); a.cost = checked(policy.repairLabour + Money(definition.price * policy.repairPriceFraction)); a.description = ServiceOperations.Description(request.service); }
            if (request.kind == PaymentKind.ClutchOil) { if (definition.category != PartCategory.Clutch || definition.clutchEnvironment != ClutchEnvironment.Wet) throw new InvalidOperationException("No wet clutch oil bath to service."); a.cost = checked(policy.repairLabour + Money(definition.clutchOilCapacityLitres * policy.clutchOilPerLitre)); }
            if (request.kind == PaymentKind.ElementNew || request.kind == PaymentKind.ElementReuse) { if(definition.category==PartCategory.Tires)throw new InvalidOperationException("Use per-corner tire replacement instead of generic engine-element service."); if (!item.condition.elements.Any(e => e.id == request.auxiliaryId)) throw new InvalidOperationException("Individual element changed."); a.cost = request.kind == PaymentKind.ElementNew ? PartsMarket.Quote(p,definition.category==PartCategory.Wheels?cat.Find(WheelFitments.DefinitionId(item,Array.FindIndex(item.condition.elements,e=>e.id==request.auxiliaryId))):definition,item.condition.elements.Length,true).cost : 0; }
            return a;
        }
        static string Hash(PlayerProfile p, MechanicalCatalog cat, OperatingPolicy policy, ValuationPolicy values, PaymentRequest r,string profileJson=null,string requestJson=null)
        {
            string extra = requestJson??JsonUtility.ToJson(r);
            if (r.kind == PaymentKind.Part) {extra += JsonUtility.ToJson(cat.Find(r.targetId));if(r.preserveTireFitments){var car=Car(p,r.carId);var b=car.installed.slots.Find(s=>s.slot=="Tires");var pack=b==null?null:p.parts.Find(i=>i.instanceId==b.partInstanceId);if(pack!=null&&pack.condition.tires.Length==4)for(int i=0;i<4;i++)extra+=JsonUtility.ToJson(cat.Find(TireFitments.DefinitionId(pack,i)));}}
            if (r.kind == PaymentKind.Car || r.kind==PaymentKind.VehicleInspection) { extra += JsonUtility.ToJson(cat.Spec(r.offer.model)); foreach (var id in (r.offer.parts==null||r.offer.parts.Count==0?cat.Recipe(r.offer.model,r.offer.stock).Values:r.offer.parts.Select(i=>i.definitionId)))extra+=JsonUtility.ToJson(cat.Find(id)); }
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(Valuations.Fingerprint(p, cat, values,profileJson) + JsonUtility.ToJson(policy) + extra)));
        }
        public static PaymentQuote Quote(PlayerProfile p, MechanicalCatalog cat, OperatingPolicy policy, ValuationPolicy values, PaymentRequest request, long now)
        {
            Guard(p, now, "");
            string invalid = MechanicsOwnership.ValidateForQuote(p, cat);
            if (invalid != null) throw new InvalidOperationException(invalid);
            var a = Preview(p, cat, policy, values, request, now); return new PaymentQuote(p, policy, request, a, Hash(p, cat, policy, values, request), now);
        }
        public static void Pay(PlayerProfile p, MechanicalCatalog cat, OperatingPolicy policy, ValuationPolicy values, PaymentQuote q, long now, VehicleOffer currentOffer = null)
        {
            if (q == null) throw new InvalidOperationException("Request a payment quote first."); Guard(p, now, q.Id);
            if (p.revision != q.Revision || now < q.Issued || now >= q.Expires) throw new InvalidOperationException("Payment quote stale/expired.");
            var r = JsonUtility.FromJson<PaymentRequest>(q.RequestJson); if (r.kind == PaymentKind.Car || r.kind==PaymentKind.VehicleInspection) { if (currentOffer == null) throw new InvalidOperationException("Refresh the authored dealership offer.");if(!SnapshotEquality.Serialized(JsonUtility.ToJson(r.offer),currentOffer))throw new InvalidOperationException("Dealership listing contents changed. Refresh."); r.offer = currentOffer; }
            if (!SnapshotEquality.Profile(q.ProfileJson,p,cat)||Hash(p, cat, policy, values, r,q.ProfileJson,q.RequestJson) != q.StateHash) throw new InvalidOperationException("Payment assets/quantity/catalogue/policy changed. Refresh.");
            var amounts = Preview(p, cat, policy, values, r, now); if (amounts.cost != q.Cost || p.cash < q.Cost) throw new InvalidOperationException("Insufficient cash or changed price; nothing changed.");
            if ((r.kind == PaymentKind.Fuel && q.Fuel <= 0) || (r.kind == PaymentKind.Fluids && q.Oil + q.Coolant <= 0)) throw new InvalidOperationException("No free capacity / quantity to purchase.");
            int before = p.cash;
            // Some earlier operations already pay their component cost; charge only their new labour/liquids remainder.
            switch (r.kind) {
                case PaymentKind.VehicleInspection: VehicleInspection.Reveal(p,r.offer);break;
                case PaymentKind.Fuel: FuelDynamics.Refuel(Car(p, r.carId).fuel, cat.Spec(Car(p, r.carId).vehicleId).fuel, q.Fuel); break;
                case PaymentKind.Fluids: ServiceOperations.TopUp(p, r.carId, true, q.Oil); ServiceOperations.TopUp(p, r.carId, false, q.Coolant); break;
                case PaymentKind.OilChange: ServiceOperations.ChangeOil(p, r.carId); break;
                case PaymentKind.CoolantFlush: ServiceOperations.FlushCoolant(p, r.carId); break;
                case PaymentKind.Repair: ServiceOperations.Repair(p, r.carId, r.targetId, r.service); break;
                case PaymentKind.ClutchOil: var item = Installed(p, Car(p, r.carId), r.targetId); item.condition.clutchOilLitres = cat.Find(item.definitionId).clutchOilCapacityLitres; item.condition.clutchOilCondition = 1; item.condition.serviceCount++; item.condition.lastService = "Wet clutch oil service"; break;
                case PaymentKind.Part: if(r.preserveTireFitments&&r.install&&cat.Find(r.targetId).category==PartCategory.Tires)TireFitments.ReplaceSame(p,Car(p,r.carId),r.targetId);else PartOperations.Buy(p, cat, r.carId, r.targetId, r.install); break;
                case PaymentKind.Car: VehicleCollection.Buy(p, cat, r.offer, q.Cost); break;
                case PaymentKind.ElementNew: EngineElementService.Replace(p, r.carId, r.targetId, r.auxiliaryId); break;
                case PaymentKind.ElementReuse: EngineElementService.Replace(p, r.carId, r.targetId, r.auxiliaryId, r.spareId); break;
                case PaymentKind.Rebuild: EngineRebuilds.Start(p, r.carId, now,policy); p.cars.Find(c => c.instanceId == r.carId).engineRebuild.paidPrice = q.Cost; break;
                default: throw new InvalidOperationException("Unsupported paid operation.");
            }
            p.cash = checked(before - q.Cost); Record(p, q.Id, r.kind.ToString(), r.carId ?? (r.kind==PaymentKind.VehicleInspection?r.offer?.listingId:r.offer?.model), before, now, q.Fuel, q.Oil, q.Coolant);
        }
        public static void Sell(PlayerProfile p, MechanicalCatalog cat, ValuationPolicy policy, ValuationQuote q, long now)
        {
            if (q == null) throw new InvalidOperationException("Request a sale quote first."); Guard(p, now, q.Id);
            string invalid = Valuations.Validate(q, p, cat, policy, now); if (invalid != null) throw new InvalidOperationException(invalid);
            int before = p.cash; int after = checked(before + q.Total); var kept = q.RetainedIds.ToArray(); var keptTree = new List<string>();
            if (q.Kind == ValuationKind.LoosePart) {var item=p.parts.Find(i=>i.instanceId==q.AssetId);p.parts.Remove(item);p.inventory.dealer.Add(new DealerStock{part=item,paidPrice=q.Total});}
            else if (q.Kind == ValuationKind.LooseElement) {var item=p.looseEngineElements.Find(e=>e.element.id==q.AssetId);p.looseEngineElements.Remove(item);p.inventory.dealer.Add(new DealerStock{isUnit=true,unit=item,paidPrice=q.Total});}
            else if (q.Kind == ValuationKind.Engine) {
                var engine = p.engines.Find(e => e.instanceId == q.AssetId); if (!string.IsNullOrEmpty(engine.parentVehicleId)) AssemblyOperations.Detach(p, engine.parentVehicleId);
                foreach (string id in kept) AssemblyOperations.RemovePart(p, engine.instanceId, cat.Find(p.parts.Find(i => i.instanceId == id).definitionId).category);
                p.parts.RemoveAll(i => q.IncludedIds.Contains(i.instanceId)); p.engines.Remove(engine);
            } else {
                var car = p.cars.Find(c => q.Kind == ValuationKind.Car ? c.instanceId == q.AssetId : c.chassisId == q.AssetId);
                string engineId = car.engineAssemblyId;
                if (!string.IsNullOrEmpty(engineId)) AssemblyOperations.Detach(p, car.instanceId);
                if (q.Kind == ValuationKind.Chassis && !string.IsNullOrEmpty(engineId)) {
                    var keptEngine = p.engines.Find(e => e.instanceId == engineId); keptTree.Add(engineId);
                    foreach (var b in keptEngine.slots) { var part = p.parts.Find(i => i.instanceId == b.partInstanceId); keptTree.AddRange(part.ItemIds); }
                }
                foreach (string id in kept) {
                    var item = p.parts.Find(i => i.instanceId == id); var category = cat.Find(item.definitionId).category;
                    if (MechanicalCatalog.EngineSlot(category)) AssemblyOperations.RemovePart(p, engineId, category); else PartOperations.Remove(p, car.instanceId, category);
                }
                p.parts.RemoveAll(i => q.IncludedIds.Contains(i.instanceId));
                if (q.Kind == ValuationKind.Car && !string.IsNullOrEmpty(engineId)) p.engines.RemoveAll(e => e.instanceId == engineId);
                p.runCheckpoints.RemoveAll(r => r.ownedVehicleId == car.instanceId); p.cars.Remove(car);
                if (p.selectedOwnedVehicleId == car.instanceId) { var next = p.cars.FirstOrDefault(); p.selectedOwnedVehicleId = next?.instanceId ?? ""; p.selectedVehicleId = next?.vehicleId ?? ""; }
            }
            p.cash = after;
            var retained = new List<string>(); foreach (string id in kept) { var item = p.parts.Find(i => i.instanceId == id);if(item!=null)retained.AddRange(item.ItemIds);else retained.Add(id); }
            retained.AddRange(keptTree);
            Record(p, q.Id, "Sell " + q.Kind, q.AssetId, before, now, 0, 0, 0, q.IncludedIds.ToArray(), retained.ToArray());
        }
        public static void Record(PlayerProfile p, string id, string operation, string target, int before, long now, double fuel = 0, double oil = 0, double coolant = 0, string[] disposed = null, string[] retained = null)
        {
            p.economyReceipts.Add(new EconomyReceipt { id = id, operation = operation, target = target, cashBefore = before, cashAfter = p.cash, utcTicks = now, revision = checked(p.revision + 1), fuelAdded = fuel, oilAdded = oil, coolantAdded = coolant, disposed = disposed ?? new string[0], retained = retained ?? new string[0] });
            if (p.economyReceipts.Count > 256) p.economyReceipts.RemoveAt(0); p.lastEconomyUtcTicks = now;
        }
        public static void StartRecovery(PlayerProfile p, OperatingPolicy policy, long now)
        {
            policy.Validate(); Guard(p, now, ""); if (p.recoveryWork != null || p.cash >= policy.recoveryCashLimit) throw new InvalidOperationException("Recovery work already active or cash above assistance limit.");
            p.recoveryWork = new RecoveryWork { id = MechanicsOwnership.NewId("recovery"), startedUtcTicks = now, readyUtcTicks = checked(now + TimeSpan.FromSeconds(policy.recoverySeconds).Ticks), reward = Math.Min(policy.recoveryReward, policy.recoveryCashLimit - p.cash) }; p.lastEconomyUtcTicks = now;
        }
        public static void ClaimRecovery(PlayerProfile p, string jobId, long now)
        {
            Guard(p, now, jobId); var job = p.recoveryWork; if (job == null || job.id != jobId || now < job.readyUtcTicks) throw new InvalidOperationException("Recovery job not ready or already claimed.");
            int before = p.cash; p.cash = checked(p.cash + job.reward); p.recoveryWork = null; Record(p, jobId, "Recovery work", "garage", before, now);
        }
    }
}
