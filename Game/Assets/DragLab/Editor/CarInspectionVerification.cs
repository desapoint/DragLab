using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class CarInspectionVerification
    {
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            var catalog = MechanicsTransactions.Catalog(); var profile = PlayerProfile.New(DateTime.UtcNow.Ticks); MechanicsOwnership.Ensure(profile, catalog); profile.cash = 100000;
            var car = profile.SelectedCar; string unchanged = JsonUtility.ToJson(profile);
            var baseline = CarInspection.Calculate(InspectionBuild.Capture(profile, car));
            check(baseline.completed && baseline.curve.Count > 40 && baseline.peakHp > 50 && baseline.peakTorque > 50, "Saved build has a complete settled dyno and bounded shared-simulation probe.");
            check(Math.Abs(baseline.wheelRadiusIn * DisplayUnits.MetresPerInch - catalog.Spec(car.vehicleId).wheelRadiusM) < 1e-8 && baseline.wheelDiameterIn == baseline.wheelRadiusIn * 2, "Wheel display is true outer tire radius in inches, with separate outer diameter.");
            check(JsonUtility.ToJson(profile) == unchanged, "Stats/dyno/probe do not change player cash, wear, tune, inventory or history.");
            var forged = CarInspection.Calculate(InspectionBuild.Capture(profile, car, definitionId: catalog.Id(car.vehicleId, PartCategory.Pistons, 1)));
            check(forged.peakHp == baseline.peakHp && forged.peakTorque == baseline.peakTorque && forged.curve.SequenceEqual(baseline.curve), "Forged internals provide strength headroom without invented horsepower.");
            var head = CarInspection.Calculate(InspectionBuild.Capture(profile, car, definitionId: catalog.Id(car.vehicleId, PartCategory.HeadIntake, 3)));
            check(head.peakHp > baseline.peakHp && head.peakTorque > baseline.peakTorque && head.curve.Last().hp > baseline.curve.Last().hp, "Head/intake preview produces real torque and horsepower curve gains.");
            check(head.warnings.Any(w => w.title.Contains("Clutch")), "Power preview warns when engine torque exceeds the existing clutch capacity.");
            var tire = CarInspection.Calculate(InspectionBuild.Capture(profile, car, definitionId: catalog.Id(car.vehicleId, PartCategory.Tires, 3)));
            check(tire.maxSlip <= baseline.maxSlip && tire.peakHp == baseline.peakHp, "Better grip reduces or preserves estimated slip without adding engine horsepower.");
            var big = car.tune.Copy(); big.wheelDiameter = 1.6f;
            var large = CarInspection.Calculate(InspectionBuild.Capture(profile, car, big));
            check(large.wheelRadiusIn > baseline.wheelRadiusIn && large.vehicleMassKg > baseline.vehicleMassKg && large.effectiveMassKg > baseline.effectiveMassKg && large.gearRedlineMps[0] > baseline.gearRedlineMps[0] && large.warnings.Any(w => w.title.Contains("Large/heavy")), "Larger outer radius exposes gearing, mass and launch tradeoffs with practical advice.");
            var flywheel = CarInspection.Calculate(InspectionBuild.Capture(profile, car, definitionId: catalog.Id(car.vehicleId, PartCategory.Flywheel, 1)));
            check(flywheel.flywheelInertia < baseline.flywheelInertia && flywheel.flywheelMassKg < baseline.flywheelMassKg && flywheel.peakHp == baseline.peakHp, "Light flywheel affects stored inertia rather than fake engine power.");
            var missing = CarInspection.Calculate(InspectionBuild.Capture(profile, car, remove: PartCategory.Pistons));
            check(!missing.source.build.Drivable && !missing.completed && missing.warnings.Any(w => w.critical), "Removal preview marks an incomplete build and skips driving it.");
            check(JsonUtility.ToJson(profile) == unchanged, "Every shop preview is isolated and does not purchase parts or assign owned identities.");
            var wornId = PartOperations.Buy(profile, catalog, car.instanceId, catalog.Id(car.vehicleId, PartCategory.Clutch, 1), false);
            profile.parts.Find(p => p.instanceId == wornId).condition.health = .2f;
            var worn = CarInspection.Calculate(InspectionBuild.Capture(profile, car, instanceId: wornId));
            var fresh = CarInspection.Calculate(InspectionBuild.Capture(profile, car, definitionId: catalog.Id(car.vehicleId, PartCategory.Clutch, 1)));
            check(worn.clutchCapacityNm < fresh.clutchCapacityNm && worn.condition == (double).2f && profile.parts.Find(p => p.instanceId == wornId).condition.health == .2f, "Inventory previews use actual wear instead of fresh shop condition and never heal it.");
            PartOperations.Buy(profile, catalog, car.instanceId, car.vehicleId + "/Gearbox/Interchangeable", true);
            PartOperations.Buy(profile, catalog, car.instanceId, car.vehicleId + "/Induction/TurboLargeFixed", true);
            var draft = new TuneDraft(car, profile.revision); string tuneBefore = JsonUtility.ToJson(car.tune); draft.values.finalDrive = 5.5f; draft.values.wheelDiameter = 1.3f; draft.values.wheelWidth = 1.2f; draft.values.turboSize = .7f; draft.paint = "#123456";
            check(JsonUtility.ToJson(car.tune) == tuneBefore && car.paintHex != draft.paint, "Tune draft is independent of saved tune and paint.");
            string ids = string.Join("/", profile.parts.Select(p => p.instanceId));
            bool committed = MechanicsTransactions.Try(profile, catalog, profile.revision, draft.ApplyTo, p => false, out var rollback, out var error);
            check(!committed && ReferenceEquals(rollback, profile) && JsonUtility.ToJson(car.tune) == tuneBefore, "Failed tune save leaves the running build and ownership unchanged.");
            committed = MechanicsTransactions.Try(profile, catalog, profile.revision, draft.ApplyTo, p => true, out var applied, out error);
            check(committed && applied.SelectedCar.tune.finalDrive == 5.5f && applied.SelectedCar.tune.wheelDiameter == 1.3f && applied.SelectedCar.tune.wheelWidth == 1.2f && applied.SelectedCar.tune.turboSize == 1.4f && applied.SelectedCar.paintHex == "#123456" && string.Join("/", applied.parts.Select(p => p.instanceId)) == ids, "Explicit apply changes supported gearset/fitment/paint adjustments atomically, while retaining the installed physical turbo size and identities.");
            check(!MechanicsTransactions.Try(applied, catalog, applied.revision, draft.ApplyTo, p => true, out var stale, out error), "Old draft cannot overwrite a newer applied build.");
            var invalid = new TuneDraft(applied.SelectedCar, applied.revision); invalid.values.finalDrive = float.NaN;
            check(!MechanicsTransactions.Try(applied, catalog, applied.revision, invalid.ApplyTo, p => true, out var rejected, out error), "Nonfinite tuning is rejected before publication.");
            var preset = applied.SelectedCar.tune.Copy(); applied.SelectedCar.SavePreset(0); applied.SelectedCar.tune.wheelDiameter = 1; applied.SelectedCar.tune.turboSize = 1; applied.SelectedCar.LoadPreset(0);
            check(applied.SelectedCar.tune.wheelDiameter == preset.wheelDiameter && MechanicsOwnership.Resolve(applied, applied.SelectedCar, catalog).tune.turboSize == 1.4f, "Presets retain fitment settings; effective turbo size still comes from installed hardware.");
            check(Math.Abs(DisplayUnits.Speed(100 / 3.6, SpeedUnit.MilesPerHour) - 62.1371192) < 1e-6 && DisplayUnits.Speed(10, SpeedUnit.MetresPerSecond) == 10, "Speed formatting converts canonical physics values correctly.");
            foreach (PressureUnit unit in Enum.GetValues(typeof(PressureUnit))) check(Math.Abs(DisplayUnits.ToBar(DisplayUnits.Pressure(2.4, unit), unit) - 2.4) < 1e-10, "Pressure conversion round trips without altering the tune: " + unit);
            string oldPath = SharedUnitSettings.VerificationPath;
            try
            {
                string folder = Path.Combine(root, "reports/car-inspection"); Directory.CreateDirectory(folder); SharedUnitSettings.VerificationPath = Path.Combine(folder, "unit-settings.json"); SharedUnitSettings.Reload();
                SharedUnitSettings.Set(SpeedUnit.MilesPerHour, PressureUnit.Psi); string bytes = File.ReadAllText(SharedUnitSettings.FilePath);
                profile.RestartKeepingPreferences(DateTime.UtcNow.Ticks); SharedUnitSettings.Reload();
                check(SharedUnitSettings.Current.speed == SpeedUnit.MilesPerHour && SharedUnitSettings.Current.pressure == PressureUnit.Psi && File.ReadAllText(SharedUnitSettings.FilePath) == bytes, "Independent shared unit preferences survive reload and progression reset.");
                check(SharedUnitSettings.Boost(.7).Contains("psi") && SharedUnitSettings.Boost(.7).Contains("bar"), "Boost remains readable in both bar and psi under either preference.");
                File.WriteAllText(Path.Combine(folder, "verification.txt"), log + "Checks: " + count + "\n");
            }
            finally { SharedUnitSettings.VerificationPath = oldPath; SharedUnitSettings.Reload(); }
            return count;
        }
    }
}
