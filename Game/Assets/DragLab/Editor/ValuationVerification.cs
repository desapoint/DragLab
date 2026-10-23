using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Editor
{
    public static class ValuationVerification
    {
        [MenuItem("DragLab/Prepare Valuation Policy")]
        public static void PreparePolicy()
        {
            const string path = "Assets/DragLab/Resources/Data/Economy/ValuationPolicy.asset";
            if (AssetDatabase.LoadAssetAtPath<ValuationPolicyDefinition>(path) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)); AssetDatabase.Refresh();
            var asset = ScriptableObject.CreateInstance<ValuationPolicyDefinition>(); asset.policy = ValuationPolicy.Defaults(); AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssets();
        }
        [MenuItem("DragLab/Verify Valuation and Quotes")]
        public static void RunStandalone()
        {
            PreparePolicy();
            var folder = Environment.GetEnvironmentVariable("DRAGLAB_VALUATION_REPORT") ?? Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, "reports/phases/3c"); Directory.CreateDirectory(folder);
            var log = new StringBuilder(); var csv = new StringBuilder("scenario,model,buyer,kind,complete,faulty,total,loose_parts,chassis_core\n"); int checks = 0; long now = DateTime.UtcNow.Ticks;
            Action<string, string, ValuationQuote> record = (scenario, model, q) => csv.AppendLine(string.Join(",", scenario, model, q.BuyerId, q.Kind, q.Complete, q.Faulty, q.Total, q.LoosePartTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), q.ChassisCore.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)));
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); checks++; log.AppendLine("PASS: " + label); if (checks % 50 == 0) File.WriteAllText(Path.Combine(folder, "verification-progress.txt"), log.ToString()); };
            Action<Action, string> reject = (operation, label) => { bool failed = false; try { operation(); } catch (InvalidOperationException) { failed = true; } check(failed, label); };
            var catalog = MechanicsTransactions.Catalog(); var policy = Resources.Load<ValuationPolicyDefinition>("Data/Economy/ValuationPolicy").policy;
            Func<string, PlayerProfile> fresh = model => { var p = PlayerProfile.New(now); p.cars[0].vehicleId = model; p.selectedVehicleId = model; p.cash = 200000; p.reputation = 99; MechanicsOwnership.Ensure(p, catalog); return p; };
            Func<PlayerProfile, ValuationKind, string, string, ValuationQuote> quote = (p, k, id, b) => Valuations.Capture(p, catalog, policy, k, id, b, now);
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v=>v.spec.id).ToArray();
            var selected=Environment.GetEnvironmentVariable("DRAGLAB_VALUATION_MODELS")?.Split(',');
            if(selected!=null&&selected.Any(id=>!vehicles.Any(v=>v.spec.id==id)))throw new InvalidOperationException("Unknown valuation fixture model.");
            foreach (var asset in vehicles.Where(v=>selected==null||selected.Contains(v.spec.id))) {
                string model = asset.spec.id; var p = fresh(model); var car = p.SelectedCar; string engine = car.engineAssemblyId;
                string before = JsonUtility.ToJson(p);
                foreach (var buyer in policy.buyers) foreach (var kind in new[] { ValuationKind.Car, ValuationKind.Chassis, ValuationKind.Engine }) {
                    string id = kind == ValuationKind.Car ? car.instanceId : kind == ValuationKind.Chassis ? car.chassisId : engine;
                    var q = quote(p, kind, id, buyer.id);
                    check(q.Total >= 0 && q.Complete && !q.Faulty && !q.Reserved, model + " stock " + kind + " / " + buyer.id + " is complete and priced.");
                    check(Valuations.Validate(q, p, catalog, policy, now + 1) == null, "Fresh quote validates: " + model + "/" + kind + "/" + buyer.id);
                    check(q.IncludedIds.Distinct().Count() == q.IncludedIds.Count && q.Lines.Count > 0, "Unique captured assets and explanations: " + model + "/" + kind);
                    record("stock", model, q);
                }
                var e = quote(p, ValuationKind.Engine, engine, "builder"); var c = quote(p, ValuationKind.Chassis, car.chassisId, "builder"); var whole = quote(p, ValuationKind.Car, car.instanceId, "builder");
                check(e.IncludedIds.Intersect(c.IncludedIds).Count() == 0 && e.IncludedIds.Concat(c.IncludedIds).All(id => whole.IncludedIds.Contains(id)), "Engine and chassis exclusive trees partition whole build: " + model);
                check(e.Total > e.LoosePartTotal && quote(p, ValuationKind.Engine, engine, "wholesale").Total < e.LoosePartTotal, "Controlled engine offers both above and below identical loose-component total: " + model);
                check(before == JsonUtility.ToJson(p), "All quote operations are nonmutating: " + model);
                var old = whole; car.tune.launchRpm += 50; check(Valuations.Validate(old, p, catalog, policy, now + 1) != null, "Same-revision saved tune edit invalidates: " + model); car.tune.launchRpm -= 50;
                car.fuel.litres -= 1; check(Valuations.Validate(old, p, catalog, policy, now + 1) != null, "Resource edit invalidates: " + model); car.fuel.litres += 1;
                foreach (var item in p.parts.ToArray()) {
                    var d = catalog.Find(item.definitionId); var healthy = quote(p, ValuationKind.Car, car.instanceId, "enthusiast"); var original = item.condition.Copy();
                    if (d.category == PartCategory.Tires) { foreach (var t in item.condition.tires) { t.health = .5f; t.consumedLife = .5; } TireDynamics.SyncSummary(item.condition); } else item.condition.health = .5f;
                    var worn = quote(p, ValuationKind.Car, car.instanceId, "enthusiast"); check(worn.Total <= healthy.Total, "Wear never improves quote: " + model + "/" + d.category); record("worn-" + d.category, model, worn);
                    check(Valuations.Validate(healthy, p, catalog, policy, now + 1) != null, "Condition edit invalidates even without revision: " + model + "/" + d.category);
                    if (d.category == PartCategory.Tires && item.condition.tires.Length == 4) { item.condition.tires[0].failed = true; item.condition.tires[0].failureReason = "Fixture"; TireDynamics.SyncSummary(item.condition); } else { item.condition.failed = true; item.condition.failureReason = "Fixture"; } var broken = quote(p, ValuationKind.Car, car.instanceId, "enthusiast"); check(broken.Faulty && broken.Total < healthy.Total, "Failed component discounts part/assembly: " + model + "/" + d.category); record("failed-" + d.category, model, broken); item.condition = original;
                    if (original.elements.Length > 0) {
                        item.condition = original.Copy();
                        if (d.category == PartCategory.Tires) item.condition.tires[0].failed = true;
                        else item.condition.elements[0].fault = ElementFault.Disabled;
                        var bad = quote(p, ValuationKind.Car, car.instanceId, "enthusiast");
                        check(bad.Faulty && bad.Total < healthy.Total, "Single-element failure priced despite healthy aggregate: " + model + "/" + d.category);
                        item.condition = original.Copy();
                    }
                    var kept = Valuations.Capture(p, catalog, policy, ValuationKind.Car, car.instanceId, "enthusiast", now, new[] { item.instanceId });
                    record("retained-" + d.category, model, kept);
                    check(!kept.IncludedIds.Contains(item.instanceId) && item.condition.elements.All(el => !kept.IncludedIds.Contains(el.id)) && kept.Total < healthy.Total, "Keeping pack excludes actual children and value: " + model + "/" + d.category);
                    bool optional = PartSemantics.Optional(d.category, catalog.Spec(model), MechanicsOwnership.Definitions(p, car), catalog);
                    if (optional) check(kept.Complete, "Optional removal does not invent incompleteness: " + model + "/" + d.category); else check(!kept.Complete, "Required kept hardware affects completeness: " + model + "/" + d.category);
                    check(Valuations.Validate(kept, p, catalog, policy, now + 1) == null, "Partial retained quote validates: " + model + "/" + d.category);
                }
                var piston = p.parts.Find(i => catalog.Find(i.definitionId).category == PartCategory.Pistons); var hist = quote(p, ValuationKind.Engine, engine, "builder");
                piston.condition.distanceM = 2000000; piston.condition.runningSeconds = 100000; piston.condition.stressExposure = 500; piston.condition.reconditionCount = 2; piston.condition.serviceCount = 5;
                check(quote(p, ValuationKind.Engine, engine, "builder").Total < hist.Total, "Use/abuse/reconditioning history changes price: " + model);
                string json = JsonUtility.ToJson(p); var copy = JsonUtility.FromJson<PlayerProfile>(json); MechanicsOwnership.Ensure(copy, catalog); check(Valuations.Validate(quote(p, ValuationKind.Engine, engine, "builder"), copy, catalog, policy, now + 1) == null, "Quote survives exact JSON reload: " + model);
                var attached = quote(p, ValuationKind.Engine, engine, "builder"); AssemblyOperations.Detach(p, car.instanceId);
                check(Valuations.Validate(attached, p, catalog, policy, now + 1) != null, "Transfer/detach invalidates old quote: " + model);
                var detached = quote(p, ValuationKind.Engine, engine, "builder"); check(detached.Total == attached.Total && detached.IncludedIds.SequenceEqual(attached.IncludedIds), "Stored engine prices exact retained hardware equally: " + model);
                record("stored-used-history", model, detached);
                check(quote(p, ValuationKind.Chassis, car.chassisId, "builder").Total == c.Total && !quote(p, ValuationKind.Car, car.instanceId, "builder").Complete, "Kept engine excluded from chassis/incomplete car: " + model);
                AssemblyOperations.RemovePart(p, engine, PartCategory.Pistons); var partial = quote(p, ValuationKind.Engine, engine, "builder");
                check(!partial.Complete && !partial.IncludedIds.Contains(piston.instanceId) && partial.Total < detached.Total, "Partial engine never credits missing pack: " + model);
                var loose = quote(p, ValuationKind.LoosePart, piston.instanceId, "builder"); check(loose.IncludedIds.Contains(piston.instanceId), "Loose inventory independently quoted: " + model);
                foreach (var slot in p.engines.Find(en => en.instanceId == engine).slots.ToArray()) AssemblyOperations.RemovePart(p, engine, (PartCategory)Enum.Parse(typeof(PartCategory), slot.slot));
                var empty = quote(p, ValuationKind.Engine, engine, "builder"); check(empty.Total == 0 && empty.LoosePartTotal == 0 && !empty.Complete, "Empty engine identity creates no free core value: " + model);
                check(p.cash == 200000 && MechanicsOwnership.Validate(p, catalog) == null, "Quote/removal matrix preserves money and ownership: " + model);
            }
            var mixed = fresh("starter-hatch"); var oldPack = mixed.parts.Find(i => catalog.Find(i.definitionId).category == PartCategory.Pistons); oldPack.condition.health = .4f;
            PartOperations.Buy(mixed, catalog, mixed.SelectedCar.instanceId, catalog.Id("starter-hatch", PartCategory.Pistons, 1), true);
            var forged = mixed.parts.Find(i => mixed.SelectedCar.installed.slots.Any(s => s.partInstanceId == i.instanceId) && catalog.Find(i.definitionId).category == PartCategory.Pistons);
            var mixedQuote = quote(mixed, ValuationKind.Engine, mixed.SelectedCar.engineAssemblyId, "builder"); record("mixed-forged-OEM", "starter-hatch", mixedQuote);
            check(mixedQuote.IncludedIds.Contains(forged.instanceId) && !mixedQuote.IncludedIds.Contains(oldPack.instanceId) && oldPack.condition.health == .4f, "Mixed forged/OEM engine quotes only fitted hardware and leaves displaced worn pack unhealed.");
            check(mixedQuote.Lines.Any(l => l.AssetId == forged.instanceId && l.Label.Contains("Quality") && l.After > l.Before), "Actual fitted forged quality has authored positive demand adjustment.");
            var authored = JsonUtility.FromJson<ValuationPolicy>(JsonUtility.ToJson(policy)); authored.models.Find(m => m.model == "starter-hatch").engineDemand = 1.4;
            var modelQuote = Valuations.Capture(mixed, catalog, authored, ValuationKind.Engine, mixed.SelectedCar.engineAssemblyId, "builder", now);
            check(Math.Abs(modelQuote.Total - mixedQuote.Total * 1.4) < 1.5 && Valuations.Validate(modelQuote, mixed, catalog, policy, now + 1) != null, "Authored model demand changes actual quote by its explicit factor and invalidates a different policy receipt.");
            var ring = mixed.parts.Find(i => i.ownerId == mixed.SelectedCar.engineAssemblyId && catalog.Find(i.definitionId).category == PartCategory.PistonRings);
            var serviced = quote(mixed, ValuationKind.Engine, mixed.SelectedCar.engineAssemblyId, "builder"); ring.condition.serviceCount += 50;
            check(quote(mixed, ValuationKind.Engine, mixed.SelectedCar.engineAssemblyId, "builder").Total == serviced.Total, "Service counters alone cannot manufacture a resale premium.");
            ring.condition.deposits = .8; ring.condition.leakSeverity = .8; ring.condition.adjustmentError = .5;
            check(quote(mixed, ValuationKind.Engine, mixed.SelectedCar.engineAssemblyId, "builder").Total < serviced.Total, "Shared deposits/leak/adjustment faults reduce pre-failure market condition.");
            record("mixed-service-fault", "starter-hatch", quote(mixed, ValuationKind.Engine, mixed.SelectedCar.engineAssemblyId, "builder"));
            var test = fresh("starter-hatch"); var baseQuote = quote(test, ValuationKind.Car, test.SelectedCar.instanceId, "enthusiast");
            check(Valuations.Validate(baseQuote, test, catalog, policy, baseQuote.ExpiresUtcTicks) != null && Valuations.Validate(baseQuote, test, catalog, policy, now - 1) != null, "Expiry boundary and backwards clock rejected.");
            test.revision++; check(Valuations.Validate(baseQuote, test, catalog, policy, now + 1) != null, "Revision changes invalidate receipt."); test.revision--;
            int version = policy.version; policy.version++; check(Valuations.Validate(baseQuote, test, catalog, policy, now + 1) != null, "Policy version changes invalidate."); policy.version = version;
            double factor = policy.buyers[0].car; policy.buyers[0].car += .01; check(Valuations.Validate(baseQuote, test, catalog, policy, now + 1) != null, "Unversioned authored policy changes still invalidate."); policy.buyers[0].car = factor;
            var definition = catalog.Find(test.parts[0].definitionId); definition.price++; check(Valuations.Validate(baseQuote, test, catalog, policy, now + 1) != null, "Unversioned catalogue price changes invalidate."); definition.price--;
            definition.massKg++; check(Valuations.Validate(baseQuote, test, catalog, policy, now + 1) != null, "Non-price catalogue changes invalidate."); definition.massKg--;
            reject(() => quote(test, ValuationKind.Engine, "absent", "builder"), "Unknown identity rejected."); reject(() => quote(test, ValuationKind.Car, test.SelectedCar.instanceId, "absent"), "Unknown buyer rejected.");
            reject(() => quote(test, ValuationKind.LoosePart, test.parts[0].instanceId, "builder"), "Installed item cannot be quoted as loose.");
            var mutableKept = new System.Collections.Generic.List<string> { test.parts[0].instanceId }; var immutable = Valuations.Capture(test, catalog, policy, ValuationKind.Car, test.SelectedCar.instanceId, "builder", now, mutableKept); mutableKept.Clear();
            check(immutable.RetainedIds.Count == 1 && !immutable.IncludedIds.Contains(test.parts[0].instanceId) && Valuations.Validate(immutable, test, catalog, policy, now + 1) == null, "Caller request changes cannot alter immutable receipt contents.");
            reject(() => Valuations.Capture(test, catalog, policy, ValuationKind.Car, test.SelectedCar.instanceId, "builder", now, new[] { "missing" }), "Unowned retained identity rejected.");
            reject(() => Valuations.Capture(test, catalog, policy, ValuationKind.Car, test.SelectedCar.instanceId, "builder", now, new[] { test.parts[0].instanceId, test.parts[0].instanceId }), "Duplicate retained identity rejected.");
            var duplicate = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(test)); MechanicsOwnership.Ensure(duplicate, catalog); duplicate.parts.Add(duplicate.parts[0]); reject(() => quote(duplicate, ValuationKind.Car, duplicate.SelectedCar.instanceId, "builder"), "Corrupt duplicate ownership rejected before quote.");
            var invalidPolicy = JsonUtility.FromJson<ValuationPolicy>(JsonUtility.ToJson(policy)); invalidPolicy.buyers[0].engine = double.NaN; reject(() => Valuations.Capture(test, catalog, invalidPolicy, ValuationKind.Engine, test.SelectedCar.engineAssemblyId, "enthusiast", now), "Nonfinite policy rejected.");
            var pack = test.parts.Find(i => catalog.Find(i.definitionId).category == PartCategory.Pistons); string element = pack.condition.elements[0].id; EngineElementService.Replace(test, test.SelectedCar.instanceId, pack.instanceId, element);
            var single = quote(test, ValuationKind.LooseElement, element, "builder"); check(single.Total > 0 && single.IncludedIds.Count == 1, "Owned singular piston has its own quote."); EngineElementService.Sell(test, element); check(Valuations.Validate(single, test, catalog, policy, now + 1) != null, "Singular sale invalidates original quote.");
            EngineRebuilds.Start(test, test.SelectedCar.instanceId, now); var reserved = quote(test, ValuationKind.Car, test.SelectedCar.instanceId, "builder"); check(reserved.Reserved && Valuations.Validate(reserved, test, catalog, policy, now + 1) != null, "Paid rebuild reservation allows inspection but blocks disposal quote.");
            var recovery = fresh("starter-hatch"); recovery.runCheckpoints.Add(new RunCheckpoint { runId = "pending", ownedVehicleId = recovery.SelectedCar.instanceId }); var run = quote(recovery, ValuationKind.Car, recovery.SelectedCar.instanceId, "builder"); check(run.Reserved && Valuations.Validate(run, recovery, catalog, policy, now + 1) != null, "Unsettled race blocks disposal quote.");
            var loosePending = fresh("starter-hatch"); var loosePart = loosePending.parts.Find(i => catalog.Find(i.definitionId).category == PartCategory.Pistons); PartOperations.Remove(loosePending, loosePending.SelectedCar.instanceId, PartCategory.Pistons);
            reject(() => Valuations.Capture(loosePending, catalog, policy, ValuationKind.LoosePart, loosePart.instanceId, "builder", now, new[] { loosePart.instanceId }), "Loose-part quote cannot retain itself and create an empty sale.");
            loosePending.runCheckpoints.Add(new RunCheckpoint { runId = "pending-loose", ownedVehicleId = loosePending.SelectedCar.instanceId }); var looseRun = quote(loosePending, ValuationKind.LoosePart, loosePart.instanceId, "builder");
            check(looseRun.Reserved && Valuations.Validate(looseRun, loosePending, catalog, policy, now + 1) != null, "Unsettled race also blocks loose-part disposal validation.");
            File.WriteAllText(Path.Combine(folder, "valuation-matrix.csv"), csv.ToString()); File.WriteAllText(Path.Combine(folder, "verification.txt"), "3C / " + MechanicsSchema.Model + " / quote schema " + ValuationQuote.Schema + "\n" + log + "Checks: " + checks + "\n"); Debug.Log("DRAGLAB VALUATION PASS " + checks);
        }
    }
}
