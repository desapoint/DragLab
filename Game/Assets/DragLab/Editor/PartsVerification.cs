using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Editor
{
    public static class PartsVerification
    {
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            var vehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            var catalog = new MechanicalCatalog(vehicles.Select(v => v.spec));
            var csv = new StringBuilder("vehicle_id,oem_et_s,resolved_et_s,resolved_trap_kmh\n");
            foreach (var v in vehicles)
            {
                var recipe = catalog.Recipe(v.spec.id); var r = catalog.Resolve(v.spec.id, recipe, v.CreateStockTune());
                var legacy = Benchmark.QuarterMile(v.spec, v.CreateStockTune()); var resolved = Benchmark.QuarterMile(r.spec, r.tune);
                // Raw authored specs and installed OEM hardware take different
                // normalization paths. Bound calibration by one physics step
                // and half a displayed 0.1 km/h, rather than bitwise identity.
                check(r.Drivable && Math.Abs(legacy.elapsed-resolved.elapsed)<DragMath.FixedStep && Math.Abs(legacy.trapKmh-resolved.trapKmh)<.05,
                    "OEM recipe stays within stock calibration budget: "+v.spec.id+" / ET delta="+Math.Abs(legacy.elapsed-resolved.elapsed)+" / km/h delta="+Math.Abs(legacy.trapKmh-resolved.trapKmh));
                var repeat=Benchmark.QuarterMile(r.spec,r.tune);
                check(repeat.elapsed==resolved.elapsed&&repeat.trapKmh==resolved.trapKmh,"Identical installed OEM build remains exactly deterministic: "+v.spec.id);
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:R},{2:R},{3:R}\n", v.spec.id, legacy.elapsed, resolved.elapsed, resolved.trapKmh);
                recipe[PartCategory.Pistons] = catalog.Id(v.spec.id, PartCategory.Pistons, 1);
                var forged = catalog.Resolve(v.spec.id, recipe, v.CreateStockTune());
                check(forged.Drivable && forged.tune.PowerMultiplier == r.tune.PowerMultiplier && forged.safeTorqueNm >= r.safeTorqueNm, "Forged pistons give strength, not free HP: " + v.spec.id);
                recipe[PartCategory.HeadIntake] = catalog.Id(v.spec.id, PartCategory.HeadIntake, 3);
                var mixed = catalog.Resolve(v.spec.id, recipe, v.CreateStockTune());
                var bench = Benchmark.QuarterMile(mixed.spec, mixed.tune);
                check(mixed.Drivable && bench.elapsed < resolved.elapsed && mixed.tune.PowerMultiplier == 1.48f, "Direct option 3 mixed build runs without buying intermediate options: " + v.spec.id);
                mixed.tune.engineLevel = 3; mixed.tune.tireLevel = 3; mixed.tune.gearboxLevel = 3;
                check(mixed.tune.PowerMultiplier == 1.48f && mixed.tune.GripMultiplier == 1 && mixed.tune.CapacityMultiplier == 1, "Legacy rank fields cannot stack installed output: " + v.spec.id);
                recipe[PartCategory.HeadIntake] = catalog.Id(v.spec.id, PartCategory.HeadIntake);
                check(catalog.Resolve(v.spec.id, recipe, v.CreateStockTune()).tune.PowerMultiplier == 1, "Direct downgrade restores OEM output: " + v.spec.id);
                recipe.Remove(PartCategory.Cooling); recipe[PartCategory.Induction] = v.spec.id + "/Induction/Twincharged";
                check(catalog.Resolve(v.spec.id, recipe).Drivable && catalog.Resolve(v.spec.id, recipe).warnings.Any(w => w.Contains("Cooling removed")), "Missing radiator permits unsafe operation with an overheating warning: " + v.spec.id);
            }
            var starter = catalog.Spec("starter-hatch"); var starterRecipe = catalog.Recipe(starter.id);
            starterRecipe[PartCategory.Pistons] = catalog.Id("sport-coupe", PartCategory.Pistons);
            check(!catalog.Resolve(starter.id, starterRecipe).Drivable, "Wrong piston geometry/interface rejected.");
            var piston = catalog.Find(catalog.Id(starter.id, PartCategory.Pistons)); float oldBore = piston.boreMm; piston.boreMm += 1;
            check(!catalog.Resolve(starter.id, catalog.Recipe(starter.id)).Drivable, "Wrong bore rejected even with correct interface."); piston.boreMm = oldBore;
            starterRecipe = catalog.Recipe(starter.id); starterRecipe[PartCategory.FuelSystem] = catalog.Id("sport-coupe", PartCategory.FuelSystem, 1);
            check(catalog.Resolve(starter.id, starterRecipe).Drivable, "Shared injection connector admits compatible fuel delivery from another engine.");
            check(catalog.Find(catalog.Id("sport-coupe", PartCategory.Pistons)).quality == "Forged" && catalog.Find(catalog.Id(starter.id, PartCategory.Pistons)).quality != "Forged", "OEM recipes can include different factory internal quality.");
            var factoryForged = catalog.Recipe("sport-coupe"); factoryForged[PartCategory.Pistons] = "sport-coupe/Pistons/cast";
            var regularReplacement = catalog.Resolve("sport-coupe", factoryForged);
            check(regularReplacement.Drivable && regularReplacement.tune.PowerMultiplier == 1 && catalog.Find("sport-coupe/Pistons/cast").price < catalog.Find(catalog.Id("sport-coupe", PartCategory.Pistons)).price, "Factory-forged engine accepts a cheaper compatible cast downgrade without losing OEM horsepower.");
            foreach (EngineUpgradeInduction topology in Enum.GetValues(typeof(EngineUpgradeInduction)))
            {
                var recipe = catalog.Recipe(starter.id); if (topology != EngineUpgradeInduction.Factory) recipe[PartCategory.Induction] = starter.id + "/Induction/" + topology;
                var r = catalog.Resolve(starter.id, recipe);
                check(r.Drivable && r.tune.engineLevel == 0 && new InductionProfile(r.spec, r.tune).type.ToString() == (topology == EngineUpgradeInduction.Factory ? "NaturallyAspirated" : topology.ToString()), "Induction independent of rank: " + topology);
            }
            string folder = Path.Combine(root, "reports/phases/2b"); Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "parts-verification.txt"), log + "Checks: " + count + "\n");
            File.WriteAllText(Path.Combine(folder, "oem-benchmarks.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(folder, "catalog.json"), JsonUtility.ToJson(new CatalogExport { parts = catalog.parts.ToArray() }, true));
            return count;
        }
        [Serializable] sealed class CatalogExport { public PartDefinition[] parts; }
    }
}
