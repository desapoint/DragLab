using System;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Editor
{
    public static class MechanicsVerification
    {
        public static int Run(string root)
        {
            int count = 0;
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; };
            var original = new VehicleSpec(); var tune = new Tune(); var driver = new DriverSettings();
            var race = RaceSessionFactory.Create(original, tune, driver, original, tune, driver, 402.336);
            original.torque[0].torqueNm = 999; original.gears[0] = 99; original.flywheel.massKg = 33;
            tune.finalDrive = 6; driver.reactionSeconds = 9;
            check(race.player.spec.torque[0].torqueNm != 999 && race.player.spec.gears[0] != 99 && race.player.spec.flywheel.massKg != 33 && race.player.tune.finalDrive != 6 && race.player.driver.reactionSeconds != 9, "Session snapshot isolates factory/tune/driver mutations.");
            race.player.spec.torque[0].torqueNm = 888;
            check(race.opponent.spec.torque[0].torqueNm != 888, "Player/opponent specification snapshots are independent.");
            var item = new PartInstance { instanceId = "part-1", definitionId = "piston-1" };
            check(item.Validate() == null, "Loose part contract is valid.");
            item.ownerId = "car-1"; check(item.Validate() != null, "Double-location ownership is rejected.");
            item.location = PartLocation.Vehicle; item.slot = "pistons";
            var copy = item.Copy(); copy.condition.health = .2f;
            check(item.condition.health == 1 && copy.Validate() == null, "Condition snapshots cannot heal/change the original instance.");
            item.condition.failed = true; check(item.Validate() != null, "Failure must retain a cause.");
            item.condition.failureReason = "fixture overload"; check(item.Validate() == null, "Failure cause persists in a valid instance.");
            var decoded = JsonUtility.FromJson<PartInstance>(JsonUtility.ToJson(item));
            check(decoded.instanceId == item.instanceId && decoded.condition.failed && decoded.condition.failureReason == item.condition.failureReason, "Versioned identity/condition round-trip.");
            var fuel = new FuelState { litres = 10 }; var fuelSpec = new FuelSpec();
            check(fuel.Validate(fuelSpec) == null && Math.Abs(fuel.MassKg(fuelSpec) - 10 * fuelSpec.densityKgPerLitre) < 1e-9, "Fuel contract separates litres and mass.");
            fuel.litres = 51; check(fuel.Validate(fuelSpec) != null, "Fuel contract rejects overfill.");
            fuel.litres = double.NaN; check(fuel.Validate(fuelSpec) != null, "Fuel contract rejects nonfinite quantity.");
            var mass = new MassBreakdown { dryInstalledKg = 1000, fuelKg = 20, wheelInertiaKgM2 = 40 };
            check(mass.TranslatingKg == 1020, "Mass contract does not treat rotational inertia as vehicle weight.");
            string output = Path.Combine(root, "reports/phases/2a"); Directory.CreateDirectory(output);
            var csv = new StringBuilder("vehicle_id,elapsed_s,trap_kmh,class\n");
            foreach (var definition in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
            {
                var result = Benchmark.QuarterMile(definition.spec, definition.CreateStockTune());
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:R},{2:R},{3}\n", definition.spec.id, result.elapsed, result.trapKmh, result.performanceClass);
                File.WriteAllText(Path.Combine(output, definition.spec.id + "-baseline.json"), JsonUtility.ToJson(definition.spec, true));
            }
            File.WriteAllText(Path.Combine(output, "baseline.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(output, "contracts-verification.txt"), "PASS: " + count + " contract/session checks. Additive contracts only; saveVersion remains 1.\n");
            return count;
        }
    }
}
