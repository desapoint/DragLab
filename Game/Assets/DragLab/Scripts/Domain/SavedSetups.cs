using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    public enum SetupScope { Engine, WholeCar, TuneOnly }
    [Serializable] public sealed class SetupPart { public string slot, definitionId; }
    [Serializable] public sealed class SavedVehicleSetup
    {
        public string id, name, vehicleDefinitionId;
        public SetupScope scope;
        public Tune tune = new Tune();
        public List<SetupPart> parts = new List<SetupPart>();
    }
    public sealed class SetupPlanLine
    {
        public PartCategory category;
        public string definitionId, itemId;
        public bool purchase, installed;
        public int cost;
    }
    public sealed class SetupPlan
    {
        public long revision;
        public string carId, setupId;
        public int cost;
        public readonly List<SetupPlanLine> lines = new List<SetupPlanLine>();
        public readonly List<string> errors = new List<string>(), warnings = new List<string>();
        public Tune tune;
        public BuildResolution resolution;
        public int Missing { get { return lines.Count(l => l.purchase); } }
    }
    public static class SavedSetups
    {
        public static bool InScope(SetupScope scope, PartCategory category)
        { return scope == SetupScope.WholeCar || scope == SetupScope.Engine && (MechanicalCatalog.EngineSlot(category) || category == PartCategory.Controls || category == PartCategory.RevLimiter || category == PartCategory.TwoStep || category == PartCategory.ShiftLight); }
        public static string Save(PlayerProfile p, string carId, string name, SetupScope scope)
        {
            var car = AssemblyOperations.MutableCar(p, carId);
            name = (name ?? "").Trim();
            if (name.Length < 1 || name.Length > 64 || !Enum.IsDefined(typeof(SetupScope), scope)) throw new InvalidOperationException("Give the setup a name of 1-64 characters.");
            var setup = new SavedVehicleSetup { id = MechanicsOwnership.NewId("setup"), name = name, vehicleDefinitionId = car.vehicleId, scope = scope, tune = car.tune.Copy() };
            foreach (var slot in car.installed.slots.Where(s => InScope(scope, AssemblyOperations.Parse(s.slot))))
                setup.parts.Add(new SetupPart { slot = slot.slot, definitionId = p.parts.Find(i => i.instanceId == slot.partInstanceId).definitionId });
            p.savedSetups.Add(setup); return setup.id;
        }
        public static void Delete(PlayerProfile p, string id)
        { var setup = Find(p, id); p.savedSetups.Remove(setup); }
        static SavedVehicleSetup Find(PlayerProfile p, string id)
        { return p.savedSetups.Find(s => s.id == id) ?? throw new InvalidOperationException("Saved setup no longer exists."); }
        static bool Usable(PartInstance item)
        { return !item.condition.failed && item.condition.health > 0 && !(item.condition.elements ?? new EngineElement[0]).Any(e => e.Failed || e.fault != ElementFault.None); }
        public static SetupPlan Preview(PlayerProfile p, string carId, string setupId)
        {
            var car = p.cars.Find(c => c.instanceId == carId) ?? throw new InvalidOperationException("Car no longer exists.");
            var setup = Find(p, setupId);
            var plan = new SetupPlan { revision = p.revision, carId = carId, setupId = setupId };
            if (setup.vehicleDefinitionId != car.vehicleId) { plan.errors.Add("This setup uses another platform. Select a matching owned car."); return plan; }
            if (car.engineRebuild != null || p.runCheckpoints.Any(r => !r.settled)) plan.errors.Add("Close the run / collect the rebuilt engine before applying a setup.");
            if (setup.scope != SetupScope.TuneOnly && setup.parts.Any(s => MechanicalCatalog.EngineSlot(AssemblyOperations.Parse(s.slot))) && string.IsNullOrEmpty(car.engineAssemblyId)) plan.errors.Add("Attach a stored engine first. A blueprint does not create an engine assembly.");
            var map = MechanicsOwnership.Definitions(p, car);
            foreach (var key in map.Keys.Where(c => InScope(setup.scope, c)).ToArray()) map.Remove(key);
            foreach (var desired in setup.parts)
            {
                var d = p.catalog.Find(desired.definitionId);
                if (d == null || !p.catalog.Fits(p.catalog.Spec(car.vehicleId), d)) { plan.errors.Add("Unavailable or incompatible part: " + desired.definitionId); continue; }
                map[d.category] = d.id;
                var binding = car.installed.slots.Find(s => s.slot == desired.slot);
                var fitted = binding == null ? null : p.parts.Find(i => i.instanceId == binding.partInstanceId);
                var owned = fitted != null && fitted.definitionId == d.id && Usable(fitted) ? fitted :
                    p.parts.Where(i => i.location == PartLocation.Inventory && i.definitionId == d.id && Usable(i)).OrderByDescending(i => i.condition.health).ThenBy(i => i.instanceId, StringComparer.Ordinal).FirstOrDefault();
                var line = new SetupPlanLine { category = d.category, definitionId = d.id, itemId = owned == null ? null : owned.instanceId, installed = owned != null && owned == fitted, purchase = owned == null, cost = owned == null ? d.price : 0 };
                if(line.purchase && p.reputation<d.reputationRequired)plan.errors.Add(d.name+": requires REP "+d.reputationRequired);
                plan.cost = checked(plan.cost + line.cost); plan.lines.Add(line);
                if (owned != null && owned.condition.health < .95) plan.warnings.Add(d.name + ": reuse at " + owned.condition.health.ToString("P0") + " health; wear is retained.");
            }
            var requested = car.tune.Copy();
            if (setup.scope == SetupScope.Engine) AssemblyOperations.EngineAdjustments(requested, setup.tune); else requested.ApplyAdjustments(setup.tune);
            plan.resolution = p.catalog.Resolve(car.vehicleId, map, requested); plan.tune = plan.resolution.tune.Copy();
            plan.errors.AddRange(plan.resolution.errors); plan.warnings.AddRange(plan.resolution.warnings);
            if (setup.scope == SetupScope.TuneOnly) {
                // Tuning snapshots can never grant hardware; use the live installed resolver.
                plan.warnings.Add("Tuning only: installed hardware permissions/clamps remain in effect.");
                plan.errors.Clear();
                plan.warnings.AddRange(MechanicsOwnership.Resolve(p, car, p.catalog).errors.Select(e => "Build remains unavailable: " + e));
                if (car.engineRebuild != null) plan.errors.Add("Collect the reserved rebuilt engine first.");
                if(!string.IsNullOrEmpty(car.garageJobId))plan.errors.Add("Claim or cancel the assigned garage job first.");
                if (p.runCheckpoints.Any(r => !r.settled)) plan.errors.Add("Close the active run first.");
            }
            if (p.cash < plan.cost) plan.errors.Add("Insufficient cash: need " + DisplayUnits.Money(plan.cost) + ". No parts or tune will change.");
            return plan;
        }
        // Recompute inside the existing atomic transaction; never trust UI prices or retained object references.
        public static void Apply(PlayerProfile p, string carId, string setupId, long quotedRevision, bool buyMissing, int quotedCost = -1)
        {
            if (p.revision != quotedRevision) throw new InvalidOperationException("Setup preview is stale. Preview again.");
            var car = AssemblyOperations.MutableCar(p, carId); var setup = Find(p, setupId); var plan = Preview(p, carId, setupId);
            if (quotedCost >= 0 && plan.cost != quotedCost) throw new InvalidOperationException("Catalog prices changed. Preview the setup again.");
            if (plan.errors.Count > 0) throw new InvalidOperationException(string.Join("; ", plan.errors));
            if (plan.Missing > 0 && !buyMissing) throw new InvalidOperationException("Missing parts: preview and use Buy missing + apply.");
            foreach (var binding in car.installed.slots.Where(s => InScope(setup.scope, AssemblyOperations.Parse(s.slot)) && !setup.parts.Any(d => d.slot == s.slot)).ToArray()) MechanicsOwnership.Remove(p, car, binding.slot);
            foreach (var line in plan.lines.Where(l => !l.installed))
            {
                string id = line.purchase ? PartOperations.Buy(p, p.catalog, carId, line.definitionId, false) : line.itemId;
                PartOperations.Install(p, p.catalog, carId, id);
            }
            // Resolve after all hardware is present, rather than copying intermediate per-part clamps.
            var requested = car.tune.Copy();
            if (setup.scope == SetupScope.Engine) AssemblyOperations.EngineAdjustments(requested, setup.tune); else requested.ApplyAdjustments(setup.tune);
            var resolved = p.catalog.Resolve(car.vehicleId, MechanicsOwnership.Definitions(p, car), requested);
            car.tune.ApplyAdjustments(resolved.tune); car.tune.ClampTo(resolved.spec); MechanicsOwnership.ProjectRanks(p, car, p.catalog);
            foreach (var binding in car.installed.slots) { var item = p.parts.Find(i => i.instanceId == binding.partInstanceId); EngineElements.Ensure(resolved.spec, p.catalog.Find(item.definitionId), item); }
        }
        public static string Validate(SavedVehicleSetup setup, MechanicalCatalog catalog)
        {
            if (setup == null || string.IsNullOrWhiteSpace(setup.id) || string.IsNullOrWhiteSpace(setup.name) || setup.name.Length > 64 || setup.tune == null || setup.parts == null || !Enum.IsDefined(typeof(SetupScope), setup.scope)) return "Invalid saved setup.";
            var spec = catalog.Spec(setup.vehicleDefinitionId); var slots = new HashSet<string>();
            foreach (var s in setup.parts) {
                PartCategory category;
                if (s == null || !Enum.TryParse(s.slot, out category) || !InScope(setup.scope, category) || !slots.Add(s.slot)) return "Invalid saved setup slot.";
                var d = catalog.Find(s.definitionId); if (d == null || d.category != category || !catalog.Fits(spec, d)) return "Invalid saved setup part fitment.";
            }
            foreach (var f in typeof(Tune).GetFields()) if (f.FieldType == typeof(float) && !MechanicsSchema.Finite((float)f.GetValue(setup.tune))) return "Nonfinite saved tuning.";
            if (setup.tune.awdConfigured && (setup.tune.awd == null || setup.tune.awd.Validate() != null)) return "Invalid saved AWD tuning.";
            return null;
        }
    }
}
