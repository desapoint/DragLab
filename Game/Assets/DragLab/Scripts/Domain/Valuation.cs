using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace DragLab.Domain.Mechanics
{
    public enum ValuationKind { LoosePart, Engine, Chassis, Car, LooseElement }
    [Serializable] public sealed class BuyerPolicy
    {
        public string id, name;
        public double parts = 1, engine = 1, chassis = 1, car = 1, incomplete = .7, faulty = .6;
    }
    [Serializable] public sealed class ModelValue
    {
        public string model;
        public int bareChassis = 200;
        public double engineDemand = 1, chassisDemand = 1, carDemand = 1;
    }
    [Serializable] public sealed class QualityValue { public string quality; public double factor = 1; }
    [Serializable] public sealed class ValuationPolicy
    {
        public string id = "prototype-used-market";
        public int version = 1, quoteSeconds = 300;
        public double usedPartFraction = .45, scrapFraction = .08, minimumWearFactor = .1;
        public double dealerResaleMarkup = .35;
        public int dealerHandlingFee = 1;
        public double distanceDiscountPerKm = .00002, hourDiscount = .0005, stressDiscountPerSecond = .0001, reconditionDiscount = .01, historyDiscountLimit = .2;
        public int defaultBareChassis = 200;
        public List<BuyerPolicy> buyers = new List<BuyerPolicy>();
        public List<ModelValue> models = new List<ModelValue>();
        public List<QualityValue> qualities = new List<QualityValue>();
        public static ValuationPolicy Defaults()
        {
            var p = new ValuationPolicy();
            p.buyers.Add(new BuyerPolicy { id = "enthusiast", name = "Enthusiast", engine = 1.18, car = 1.1, incomplete = .65, faulty = .55 });
            p.buyers.Add(new BuyerPolicy { id = "builder", name = "Engine builder", engine = 1.3, chassis = .8, car = .95, incomplete = .9, faulty = .8 });
            p.buyers.Add(new BuyerPolicy { id = "wholesale", name = "Parts wholesaler", engine = .85, chassis = .85, car = .85, incomplete = .8, faulty = .65 });
            p.buyers.Add(new BuyerPolicy { id = "recycler", name = "Recycler", parts = .5, engine = .6, chassis = .65, car = .6, incomplete = 1, faulty = .95 });
            p.qualities.Add(new QualityValue { quality = "OEM", factor = 1 });
            p.qualities.Add(new QualityValue { quality = "Forged", factor = 1.08 });
            p.qualities.Add(new QualityValue { quality = "Aftermarket", factor = 1.04 });
            string[] models = { "starter-hatch", "city-three", "sport-coupe", "utility-pickup", "v8-muscle", "rally-sedan", "touring-six", "super-v8" };
            int[] cores = { 280, 180, 840, 1240, 1500, 1600, 1360, 2200 };
            for (int i = 0; i < models.Length; i++) p.models.Add(new ModelValue { model = models[i], bareChassis = cores[i] });
            return p;
        }
        internal void Validate()
        {
            if(!MechanicsSchema.Finite(dealerResaleMarkup)||dealerResaleMarkup<0||dealerResaleMarkup>10||dealerHandlingFee<1)throw new InvalidOperationException("Invalid dealer spread/handling fee.");
            if (string.IsNullOrWhiteSpace(id) || version < 1 || quoteSeconds < 1 || quoteSeconds > 86400 || defaultBareChassis < 0) throw new InvalidOperationException("Invalid valuation policy identity/lifetime/core value.");
            foreach (var v in new[] { usedPartFraction, scrapFraction, minimumWearFactor, historyDiscountLimit }) if (!MechanicsSchema.Finite(v) || v < 0 || v > 1) throw new InvalidOperationException("Invalid valuation policy fraction.");
            if (scrapFraction > 1 || buyers == null || buyers.Count == 0 || models == null || qualities == null) throw new InvalidOperationException("Missing valuation policy tables.");
            foreach (var v in new[] { distanceDiscountPerKm, hourDiscount, stressDiscountPerSecond, reconditionDiscount }) Factor(v);
            Unique(buyers.Select(b => b == null ? null : b.id)); Unique(models.Select(m => m == null ? null : m.model)); Unique(qualities.Select(q => q == null ? null : q.quality));
            foreach (var b in buyers) { if (string.IsNullOrWhiteSpace(b.name)) throw new InvalidOperationException("Buyer needs a name."); foreach (var v in new[] { b.parts, b.engine, b.chassis, b.car, b.incomplete, b.faulty }) Factor(v); if (b.incomplete > 1 || b.faulty > 1) throw new InvalidOperationException("Incomplete/faulty factors must be discounts."); }
            foreach (var m in models) { if (m.bareChassis < 0) throw new InvalidOperationException("Invalid chassis core price."); Factor(m.engineDemand); Factor(m.chassisDemand); Factor(m.carDemand); }
            foreach (var q in qualities) Factor(q.factor);
        }
        static void Factor(double v) { if (!MechanicsSchema.Finite(v) || v < 0 || v > 10) throw new InvalidOperationException("Invalid valuation factor."); }
        static void Unique(IEnumerable<string> ids) { var seen = new HashSet<string>(); foreach (var id in ids) if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) throw new InvalidOperationException("Duplicate/empty valuation table identity."); }
    }

    // Detached immutable receipts. Requests are copied; callers cannot change quoted money/contents.
    public sealed class ValuationLine
    {
        public string AssetId { get; } public string Label { get; } public string Explanation { get; }
        public double Before { get; } public double After { get; } public double Delta => After - Before;
        internal ValuationLine(string asset, string label, double before, double after, string reason) { AssetId = asset; Label = label; Before = before; After = after; Explanation = reason; }
    }
    public sealed class ValuationQuote
    {
        public const int Schema = 1;
        public string Id { get; } public string PolicyId { get; } public int PolicyVersion { get; }
        public ValuationKind Kind { get; } public string AssetId { get; } public string BuyerId { get; } public string BuyerName { get; }
        public long Revision { get; } public long IssuedUtcTicks { get; } public long ExpiresUtcTicks { get; }
        public int Total { get; } public double LoosePartTotal { get; } public double ChassisCore { get; }
        public bool Complete { get; } public bool Faulty { get; } public bool Reserved { get; }
        public ReadOnlyCollection<string> IncludedIds { get; } public ReadOnlyCollection<string> RetainedIds { get; }
        public ReadOnlyCollection<string> Warnings { get; } public ReadOnlyCollection<ValuationLine> Lines { get; }
        internal string Fingerprint { get; }
        internal string ProfileJson { get; }
        internal ValuationQuote(ValuationKind kind, string asset, string buyer, ValuationPolicy p, PlayerProfile profile, long now, string fingerprint, List<string> included, string[] kept, List<string> warnings, List<ValuationLine> lines, double loose, double core, double total, bool complete, bool faulty, bool reserved)
        {
            Id = Guid.NewGuid().ToString("N"); PolicyId = p.id; PolicyVersion = p.version; Kind = kind; AssetId = asset; BuyerId = buyer; BuyerName = p.buyers.Find(b => b.id == buyer).name;
            Revision = profile.revision; IssuedUtcTicks = now; ExpiresUtcTicks = checked(now + TimeSpan.FromSeconds(p.quoteSeconds).Ticks); Fingerprint = fingerprint;ProfileJson=JsonUtility.ToJson(profile);
            IncludedIds = included.AsReadOnly(); RetainedIds = Array.AsReadOnly((string[])kept.Clone()); Warnings = warnings.AsReadOnly(); Lines = lines.AsReadOnly();
            LoosePartTotal = loose; ChassisCore = core; Total = checked((int)Math.Round(total, MidpointRounding.AwayFromZero)); Complete = complete; Faulty = faulty; Reserved = reserved;
        }
    }
    public static class Valuations
    {
        static double Clamp(double x, double low, double high) => Math.Max(low, Math.Min(high, x));
        static bool Fault(ComponentCondition c) => c.tires != null && c.tires.Length == 4 ? c.tires.Any(t => t.failed || t.health <= 0) : c.failed || c.health <= 0 || c.elements.Any(e => e.Failed || e.fault != ElementFault.None);
        static double ElementHealth(EngineElement e) => e.Failed ? 0 : e.health * (e.fault == ElementFault.None ? 1 : 1 - .7 * Clamp(e.severity, 0, 1));
        static double Health(ComponentCondition c)
        {
            double h = c.health;
            if (c.elements.Length > 0) h = Math.Min(h, c.elements.Average(ElementHealth));
            if (c.tires.Length > 0) h = Math.Min(h, c.tires.Average(t => t.failed ? 0 : Math.Min(t.health, Clamp(1 - t.consumedLife, 0, 1))));
            return Clamp(h * (1 - .25 * Clamp(c.deposits, 0, 1)) * (1 - .25 * Clamp(c.leakSeverity, 0, 1)) * (1 - .15 * Clamp(c.adjustmentError, 0, 1)), 0, 1);
        }
        static double Change(List<ValuationLine> lines, string asset, string label, double value, double factor, string why) { double after = value * factor; lines.Add(new ValuationLine(asset, label, value, after, why + " / ×" + factor.ToString("F3"))); return after; }
        internal static double PartValue(PartInstance part, PartDefinition def, ValuationPolicy policy, BuyerPolicy buyer, List<ValuationLine> lines,MechanicalCatalog catalog=null)
        {
            var c = part.condition;
            if (def.category == PartCategory.Tires && c.tires != null && c.tires.Length == 4)
            {
                // A tyre set is a purchase/fitment wrapper only. Each tyre's identity,
                // wear, specification, faults and distance determine its own resale.
                double total = 0;
                for (int i = 0; i < 4; i++)
                {
                    var t = c.tires[i];
                    var spec = catalog == null ? def : catalog.Find(TireFitments.DefinitionId(part, i)) ?? def;
                    double baseValue = spec.price * .25 * policy.usedPartFraction;
                    var grade = policy.qualities.Find(q => q.quality == spec.quality);
                    double tread = t.failed ? 0 : Clamp(Math.Min(t.health, 1 - t.consumedLife), 0, 1);
                    double tireHistory = Clamp(t.distanceM / 1000 * policy.distanceDiscountPerKm + t.runningSeconds / 3600 * policy.hourDiscount, 0, policy.historyDiscountLimit);
                    double usedValue = baseValue * (grade == null ? 1 : grade.factor)
                        * (policy.minimumWearFactor + (1 - policy.minimumWearFactor) * tread)
                        * (t.failed || tread <= 0 ? policy.scrapFraction : 1) * (1 - tireHistory);
                    total += usedValue;
                    lines.Add(new ValuationLine(string.IsNullOrEmpty(t.id) ? part.instanceId + "/tire:" + i : t.id,
                        spec.name + " / " + new[] { "Front left", "Front right", "Rear left", "Rear right" }[i],
                        0, usedValue, "Individual compound, wear, failure and " + (t.distanceM / 1000).ToString("F1") + " km / " + (t.runningSeconds / 3600).ToString("F1") + " hours; no condition inherited from other tyres"));
                }
                return Change(lines, part.instanceId, "Loose-part buyer", total, buyer.parts, buyer.name);
            }
            double cataloguePrice=def.price;
            if(def.category==PartCategory.Tires&&c.tires.Length==4&&catalog!=null)cataloguePrice=Enumerable.Range(0,4).Sum(i=>catalog.Find(TireFitments.DefinitionId(part,i)).price*.25);
            if(def.category==PartCategory.Wheels&&WheelFitments.HasOverrides(part)&&catalog!=null)cataloguePrice=Enumerable.Range(0,4).Sum(i=>catalog.Find(WheelFitments.DefinitionId(part,i)).price*.25);
            double value = cataloguePrice * policy.usedPartFraction;
            lines.Add(new ValuationLine(part.instanceId, def.name, 0, value, "Catalogue " + DisplayUnits.Money(cataloguePrice) + " × used-market " + policy.usedPartFraction.ToString("P0") + "; actual item " + part.instanceId));
            var quality = policy.qualities.Find(q => q.quality == def.quality);
            value = Change(lines, part.instanceId, "Quality demand / " + def.quality, value, quality == null ? 1 : quality.factor, "Price already reflects construction; this is secondary buyer demand");
            value = Change(lines, part.instanceId, "Wear / individual condition", value, policy.minimumWearFactor + (1 - policy.minimumWearFactor) * Health(c), "Pack health, individual cylinder/valve/tire life and deposits/leaks/adjustment");
            if (Fault(c)) value = Change(lines, part.instanceId, "Fault / core recovery", value, c.failed || c.health <= 0 ? policy.scrapFraction : .65, "Failed pack or individual fault; no healthy replacement assumed");
            double history = Clamp(c.distanceM / 1000 * policy.distanceDiscountPerKm + c.runningSeconds / 3600 * policy.hourDiscount + c.stressExposure * policy.stressDiscountPerSecond + c.reconditionCount * policy.reconditionDiscount, 0, policy.historyDiscountLimit);
            value = Change(lines, part.instanceId, "Use / service history", value, 1 - history, (c.distanceM / 1000).ToString("F1") + " km; " + (c.runningSeconds / 3600).ToString("F1") + " h; " + c.stressExposure.ToString("F1") + " abuse seconds; " + c.serviceCount + " services, " + c.reconditionCount + " reconditions. Service itself grants no resale bonus");
            return Change(lines, part.instanceId, "Loose-part buyer", value, buyer.parts, buyer.name);
        }
        public static ValuationQuote Capture(PlayerProfile profile, MechanicalCatalog catalog, ValuationPolicy policy, ValuationKind kind, string assetId, string buyerId, long nowUtcTicks, IEnumerable<string> retain = null)
        {
            if (profile == null || catalog == null || policy == null || !Enum.IsDefined(typeof(ValuationKind), kind) || nowUtcTicks <= 0 || nowUtcTicks > DateTime.MaxValue.Ticks - TimeSpan.FromDays(1).Ticks) throw new InvalidOperationException("Invalid valuation request.");
            policy.Validate(); string invalid = MechanicsOwnership.ValidateForQuote(profile, catalog); if (invalid != null) throw new InvalidOperationException(invalid);
            var buyer = policy.buyers.Find(b => b.id == buyerId); if (buyer == null) throw new InvalidOperationException("Unknown buyer.");
            string[] kept = (retain ?? Enumerable.Empty<string>()).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (kept.Distinct().Count() != kept.Length) throw new InvalidOperationException("Duplicate retained asset.");
            var lines = new List<ValuationLine>(); var warnings = new List<string>(); var ids = new List<string>(); var items = new List<PartInstance>();
            OwnedCar car = null; EngineAssemblyInstance engine = null; VehicleSpec spec = null; bool complete = true, faulty = false, reserved = false; double core = 0, loose = 0;
            if (kind == ValuationKind.LoosePart) {
                var item = profile.parts.Find(p => p.instanceId == assetId && p.location == PartLocation.Inventory);
                if (item == null || kept.Length > 0) throw new InvalidOperationException("Quote requires an owned loose part without retained descendants."); items.Add(item);
            } else if (kind == ValuationKind.LooseElement) {
                var element = profile.looseEngineElements.Find(e => e.element.id == assetId);
                if (element == null || kept.Length > 0) throw new InvalidOperationException("Quote requires an owned loose engine element without retained descendants.");
                ids.Add(assetId);
                loose = element.unitPrice * policy.usedPartFraction;
                if(element.tireUnit && element.tire != null) {
                    // A stored tire's value belongs to that physical tire, not its
                    // previous four-tire set or the generic EngineElement wrapper.
                    var t=element.tire;
                    faulty=t.failed||t.health<=0||t.consumedLife>=1;
                    lines.Add(new ValuationLine(assetId, "Individual tire / "+catalog.Find(element.definitionId).name, 0, loose, "Owned tire / unique identity "+assetId));
                    double remaining=faulty?0:Clamp(Math.Min(t.health,1-t.consumedLife),0,1);
                    loose=Change(lines,assetId,"Individual tire wear",loose,policy.minimumWearFactor+(1-policy.minimumWearFactor)*remaining,"Remaining life of this physical tire");
                    if(faulty)loose=Change(lines,assetId,"Failed tire recovery",loose,policy.scrapFraction,"Damaged tire, not a failed set");
                    double history=Clamp(t.distanceM/1000*policy.distanceDiscountPerKm+t.runningSeconds/3600*policy.hourDiscount,0,policy.historyDiscountLimit);
                    loose=Change(lines,assetId,"Tire usage history",loose,1-history,(t.distanceM/1000).ToString("F1")+" km / "+(t.runningSeconds/3600).ToString("F1")+" operating hours");
                }else{
                    faulty = element.element.Failed || element.element.fault != ElementFault.None;
                    lines.Add(new ValuationLine(assetId, element.element.role + " / cylinder " + (element.element.cylinder + 1), 0, loose, "Owned single element / unit catalogue " + DisplayUnits.Money(element.unitPrice)));
                    loose = Change(lines, assetId, "Individual wear / fault", loose, (policy.minimumWearFactor + (1 - policy.minimumWearFactor) * ElementHealth(element.element)) * (faulty ? .65 : 1), "Actual element health/fault");
                }
                loose = Change(lines, assetId, "Loose-element buyer", loose, buyer.parts, buyer.name);
            } else {
                if (kind == ValuationKind.Engine) {
                    engine = profile.engines.Find(e => e.instanceId == assetId); if (engine == null) throw new InvalidOperationException("Unknown owned engine.");
                    car = profile.cars.Find(c => c.instanceId == engine.parentVehicleId); spec = catalog.Spec(engine.vehicleDefinitionId);
                    var bindings = car == null ? engine.slots : car.installed.slots.Where(b => MechanicalCatalog.EngineSlot(AssemblyOperations.Parse(b.slot))).ToList();
                    items.AddRange(bindings.Select(b => profile.parts.Find(p => p.instanceId == b.partInstanceId))); ids.Add(engine.instanceId);
                } else {
                    car = profile.cars.Find(c => kind == ValuationKind.Chassis ? c.chassisId == assetId : c.instanceId == assetId); if (car == null) throw new InvalidOperationException("Unknown owned car/chassis.");
                    spec = catalog.Spec(car.vehicleId); ids.Add(car.chassisId);
                    if (kind == ValuationKind.Car) { ids.Add(car.instanceId); if (!string.IsNullOrEmpty(car.engineAssemblyId)) ids.Add(car.engineAssemblyId); else { complete = false; warnings.Add("No engine attached; stored engines are excluded."); } }
                    items.AddRange(car.installed.slots.Where(b => kind == ValuationKind.Car || !MechanicalCatalog.EngineSlot(AssemblyOperations.Parse(b.slot))).Select(b => profile.parts.Find(p => p.instanceId == b.partInstanceId)));
                    var model = policy.models.Find(m => m.model == spec.id); core = model == null ? policy.defaultBareChassis : model.bareChassis;
                    lines.Add(new ValuationLine(car.chassisId, "Bare chassis identity", 0, core, "Authored chassis-only base; not whole-car purchase price. Body/road hardware priced separately"));
                }
                reserved = car != null && car.engineRebuild != null || profile.runCheckpoints.Any(r => !r.settled);
                if (reserved) warnings.Add("Reserved by an engine rebuild or unsettled run. Inspection only; quote cannot authorize disposal.");
                warnings.Add("Fuel/oil/coolant retained in the captured state, valued at " + DisplayUnits.Money(0) + " in 3C. External controls/coupling remain with chassis for engine quotes.");
            }
            if (kept.Any(id => !items.Any(i => i.instanceId == id))) throw new InvalidOperationException("Retained IDs must be actual parts inside the quoted asset.");
            foreach (var item in items.Where(i => !kept.Contains(i.instanceId)).OrderBy(i => i.instanceId, StringComparer.Ordinal)) {
                var d = catalog.Find(item.definitionId); if (d.price < 0) throw new InvalidOperationException("Invalid catalogue price.");
                loose += PartValue(item, d, policy, buyer, lines,catalog); ids.AddRange(item.ItemIds); faulty |= Fault(item.condition);
            }
            if (spec != null) {
                var defs = items.Where(i => !kept.Contains(i.instanceId)).ToDictionary(i => catalog.Find(i.definitionId).category, i => i.definitionId);
                foreach (var category in MechanicalCatalog.Slots(spec).Where(c => kind == ValuationKind.Engine ? MechanicalCatalog.EngineSlot(c) : kind == ValuationKind.Chassis ? !MechanicalCatalog.EngineSlot(c) : true))
                    if (!defs.ContainsKey(category) && !PartSemantics.Optional(category, spec, defs, catalog)) { complete = false; warnings.Add("Missing required " + PartSemantics.Friendly(category.ToString()) + ": contributes " + DisplayUnits.Money(0) + "."); }
                if (kind == ValuationKind.Car && kept.Length == 0) { var resolution = MechanicsOwnership.Resolve(profile, car, catalog); if (!resolution.Drivable) { complete = false; warnings.AddRange(resolution.errors); } warnings.AddRange(resolution.warnings); }
            }
            double total = loose + core;
            if (spec != null && total > 0) {
                double context = kind == ValuationKind.Engine ? buyer.engine : kind == ValuationKind.Chassis ? buyer.chassis : buyer.car;
                total = Change(lines, assetId, "Assembly / buyer context", total, context, buyer.name + " / " + kind + "; independent from loose-part offers");
                var model = policy.models.Find(m => m.model == spec.id); double demand = model == null ? 1 : kind == ValuationKind.Engine ? model.engineDemand : kind == ValuationKind.Chassis ? model.chassisDemand : model.carDemand;
                total = Change(lines, assetId, "Model demand", total, demand, spec.displayName);
                if (!complete) total = Change(lines, assetId, "Incomplete assembly", total, buyer.incomplete, "Required missing hardware or non-running build; optional omissions alone do not cause this discount");
                if (faulty) total = Change(lines, assetId, "Assembly fault risk", total, buyer.faulty, "Buyer diagnosis/rebuild risk in addition to individual damaged item values");
            }
            if (kept.Length > 0) warnings.Add(kept.Length + " retained parts and their individual descendants excluded; no replacement parts credited.");
            if(GarageBusiness.Reserved(profile,assetId)||car!=null&&GarageBusiness.Reserved(profile,car.instanceId)){reserved=true;warnings.Add("Reserved by an assigned garage job; claim or cancel before disposal.");}
            if (profile.runCheckpoints.Any(r => !r.settled) && !reserved) { reserved = true; warnings.Add("Unsettled run: inspection only; quote cannot authorize disposal."); }
            if (!MechanicsSchema.Finite(total) || total < 0 || total > int.MaxValue) throw new InvalidOperationException("Quote total outside supported currency range.");
            return new ValuationQuote(kind, assetId, buyerId, policy, profile, nowUtcTicks, Fingerprint(profile, catalog, policy), ids, kept, warnings, lines, loose, core, total, complete, faulty, reserved);
        }
        public static string Validate(ValuationQuote quote, PlayerProfile profile, MechanicalCatalog catalog, ValuationPolicy policy, long nowUtcTicks)
        {
            if (quote == null) return "No quote. Request a fresh quote.";
            if (nowUtcTicks < quote.IssuedUtcTicks || nowUtcTicks >= quote.ExpiresUtcTicks) return "Quote expired or clock moved backwards. Refresh.";
            if (profile.revision != quote.Revision) return "Profile revision changed. Refresh.";
            try {
                policy.Validate();
                if (policy.id != quote.PolicyId || policy.version != quote.PolicyVersion || !SnapshotEquality.Profile(quote.ProfileJson,profile,catalog)||Fingerprint(profile, catalog, policy,quote.ProfileJson) != quote.Fingerprint) return "Owned assets, condition, resources, configuration, catalogue or pricing policy changed. Refresh.";
                var current = Capture(profile, catalog, policy, quote.Kind, quote.AssetId, quote.BuyerId, quote.IssuedUtcTicks, quote.RetainedIds);
                if (current.Reserved) return "Asset reserved by a rebuild or unsettled run. Inspection only.";
                if (current.Total != quote.Total || !current.IncludedIds.SequenceEqual(quote.IncludedIds)) return "Quote contents changed. Refresh.";
            } catch (Exception e) { return "Quote unavailable: " + e.Message; }
            return null;
        }
        internal static string Fingerprint(PlayerProfile p, MechanicalCatalog catalog, ValuationPolicy policy,string profileJson=null)
        {
            // Conservative whole-profile lock also detects same-revision edits from benches/services.
            // Never hash UI drafts; only the authoritative owned profile and authored catalogue/policy.
            var payload = new StringBuilder("valuation-schema-" + ValuationQuote.Schema + "/catalog-" + MechanicalCatalog.Version);
            payload.Append(profileJson??JsonUtility.ToJson(p)).Append(JsonUtility.ToJson(policy));
            // Only definitions that can contribute to owned assets; serializing all shop offers every
            // UI second is unnecessary and makes a catalogue expansion slow quote inspection down.
            foreach (var id in p.parts.Select(i => i.definitionId).Concat(p.looseEngineElements.Select(e => e.definitionId)).Distinct().OrderBy(id => id, StringComparer.Ordinal)) payload.Append(JsonUtility.ToJson(catalog.Find(id)));
            foreach (var model in p.cars.Select(c => c.vehicleId).Concat(p.engines.Select(e => e.vehicleDefinitionId)).Distinct().OrderBy(id => id, StringComparer.Ordinal)) payload.Append(JsonUtility.ToJson(catalog.Spec(model)));
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString())));
        }
    }
}
