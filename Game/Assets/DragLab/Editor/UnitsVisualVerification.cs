using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using DragLab.Presentation;

namespace DragLab.Editor
{
    public static class UnitsVisualVerification
    {
        public static void RunStandalone()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string folder = Path.Combine(root, "reports/units-wheel-visuals"); Directory.CreateDirectory(folder);
            var log = new StringBuilder(); int count = 0;
            Action<bool, string> check = (ok, message) => { if (!ok) throw new InvalidOperationException(message); count++; log.AppendLine("PASS: " + message); };
            string oldPath = SharedUnitSettings.VerificationPath;
            try {
                SharedUnitSettings.VerificationPath = Path.Combine(folder, "unit-settings.json");
                File.WriteAllText(SharedUnitSettings.FilePath, "{\"version\":1,\"speed\":1,\"pressure\":1}"); SharedUnitSettings.Reload();
                check(SharedUnitSettings.Current.torque == TorqueUnit.NewtonMetres && SharedUnitSettings.Current.speed == SpeedUnit.MilesPerHour, "Legacy unit save defaults to Nm and retains other preferences.");
                var invalid = new UnitPreferences { torque = (TorqueUnit)99 }; invalid.Sanitize();
                check(invalid.torque == TorqueUnit.NewtonMetres, "Invalid torque preference safely defaults to Nm.");
                check(Math.Abs(DisplayUnits.Torque(100, TorqueUnit.PoundFeet) - 73.7562149277) < 1e-8, "100 Nm displays as 73.7562149277 lb-ft.");
                foreach (TorqueUnit unit in Enum.GetValues(typeof(TorqueUnit))) foreach (double torque in new[] { -500.0, 0, 1, 350, 2000 })
                    check(Math.Abs(DisplayUnits.ToNewtonMetres(DisplayUnits.Torque(torque, unit), unit) - torque) < 1e-10, "Torque conversion round trip: " + unit + " / " + torque);
                SharedUnitSettings.Set(SpeedUnit.KilometresPerHour, PressureUnit.Bar, TorqueUnit.PoundFeet);
                SharedUnitSettings.Set(SpeedUnit.MilesPerHour, PressureUnit.Psi); SharedUnitSettings.Reload();
                check(SharedUnitSettings.Current.torque == TorqueUnit.PoundFeet && SharedUnitSettings.Current.pressure == PressureUnit.Psi, "Torque preference persists; older two-unit callers retain it.");
                check(SharedUnitSettings.TorqueText("Clutch 350 Nm versus 500 Nm.") == "Clutch " + SharedUnitSettings.Torque(350, "0.##") + " versus " + SharedUnitSettings.Torque(500, "0.##") + ".", "Embedded warning/description torque displays in selected units.");
                var profile = PlayerProfile.New(DateTime.UtcNow.Ticks); string unitsBytes = File.ReadAllText(SharedUnitSettings.FilePath);
                profile.RestartKeepingPreferences(DateTime.UtcNow.Ticks); SharedUnitSettings.Reload();
                check(SharedUnitSettings.Current.torque == TorqueUnit.PoundFeet && unitsBytes == File.ReadAllText(SharedUnitSettings.FilePath), "Progression reset preserves torque units.");
                foreach (var car in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) {
                    var obj = new GameObject("Visual verification " + car.name);
                    try {
                        var view = obj.AddComponent<LayeredCarView>(); view.Build(car.spec.artId, car.defaultPaint);
                        var wheel = obj.transform.Find("front-wheel"); var rotor = obj.transform.Find("front-rotor"); var caliper = obj.transform.Find("front-caliper"); var body = obj.transform.Find("body-paint"); var shadow = obj.transform.Find("shadow");
                        Vector3 rotorSize = rotor.localScale, caliperSize = caliper.localScale, bodySize = body.localScale, shadowPosition = shadow.localPosition;
                        var tune = car.CreateStockTune(); string specBytes = JsonUtility.ToJson(car.spec);
                        tune.wheelDiameter = (float)(14 * DisplayUnits.MetresPerInch / car.spec.wheelRadiusM); view.ApplySetup(car.spec, tune);
                        float size14 = wheel.localScale.x, y14 = wheel.localPosition.y, bodyY14 = body.localPosition.y;
                        tune.wheelDiameter = (float)(18 * DisplayUnits.MetresPerInch / car.spec.wheelRadiusM); view.ApplySetup(car.spec, tune);
                        check(Math.Abs(wheel.localScale.x / size14 - 18f / 14) < 1e-6, car.name + ": 14→18 outer radius enlarges wheel sprite by 28.57%.");
                        check(rotor.localScale == rotorSize && caliper.localScale == caliperSize && body.localScale == bodySize, car.name + ": independent brakes and body retain their dimensions.");
                        check(wheel.localPosition.y > y14 && Math.Abs((wheel.localPosition.y - y14) - (body.localPosition.y - bodyY14)) < 1e-6 && shadow.localPosition == shadowPosition, car.name + ": tire growth raises axle and body while shadow stays grounded.");
                        Vector3 position = wheel.localPosition, size = wheel.localScale; for (int i = 0; i < 100; i++) view.ApplySetup(car.spec, tune);
                        check(wheel.localPosition == position && wheel.localScale == size && JsonUtility.ToJson(car.spec) == specBytes, car.name + ": repeated rendering does not accumulate transforms or mutate physics.");
                        view.Animate(new CarState { frontWheelAngleDegrees = 90, rearWheelAngleDegrees = 180 });
                        check(Quaternion.Angle(wheel.localRotation, rotor.localRotation) < .001 && Quaternion.Angle(caliper.localRotation, Quaternion.identity) < .001, car.name + ": wheel/rotor rotate together and caliper remains fixed after resizing.");
                        var resolved = car.spec.Copy(); resolved.fittedParts = new[] { new PartDefinition { category = PartCategory.Wheels }, new PartDefinition { category = PartCategory.Tires }, new PartDefinition { category = PartCategory.Body } }; tune.partsResolved = true; view.ApplySetup(resolved, tune);
                        check(!rotor.GetComponent<SpriteRenderer>().enabled && !caliper.GetComponent<SpriteRenderer>().enabled && wheel.GetComponent<SpriteRenderer>().enabled, car.name + ": removing brakes hides brake layers only.");
                        resolved.fittedParts = resolved.fittedParts.Where(part => part.category != PartCategory.Wheels).ToArray(); view.ApplySetup(resolved, tune);
                        check(!wheel.GetComponent<SpriteRenderer>().enabled && body.GetComponent<SpriteRenderer>().enabled, car.name + ": removed wheel hardware is absent in preview; body remains visible.");
                    } finally { UnityEngine.Object.DestroyImmediate(obj); }
                }
                int inspectionChecks = CarInspectionVerification.Run(root);
                log.AppendLine("Existing car inspection/save/parts/tune checks: " + inspectionChecks);
                File.WriteAllText(Path.Combine(folder, "verification.txt"), log + "Targeted checks: " + count + "\nTotal including inspection: " + (count + inspectionChecks) + "\n");
                Debug.Log("Units/visual verification passed: " + (count + inspectionChecks) + " checks.");
            } finally { SharedUnitSettings.VerificationPath = oldPath; SharedUnitSettings.Reload(); }
        }
    }
}
