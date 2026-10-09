using System;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
namespace DragLab.Editor
{
    public static class VehicleRoster
    {
        [MenuItem("DragLab/Prepare Expanded Vehicle Roster")]
        public static void Prepare()
        {
            var hatch = Resources.Load<VehicleDefinition>("Data/Vehicles/StarterHatch");
            var coupe = Resources.Load<VehicleDefinition>("Data/Vehicles/SportCoupe");
            var muscle = Resources.Load<VehicleDefinition>("Data/Vehicles/V8Muscle");
            if (hatch == null || coupe == null || muscle == null) throw new InvalidOperationException("Prepare the original vehicle assets first.");
            if (hatch.purchasePrice == 0) { hatch.purchasePrice = 1400; EditorUtility.SetDirty(hatch); }
            var city = hatch.spec.Copy(); city.id = "city-three"; city.displayName = "Sprig 1.2";
            city.massKg = 920; city.engineBoreMm = 75; city.displacementLitres = 1.2f; city.sound.cylinders = 3; city.sound.displacementLitres = 1.2f;
            city.redlineRpm = 6500; city.finalDrive = 4.25f; city.wheelRadiusM = .275f; city.wheelbaseM = 2.4f; city.dragAreaM2 = .59f;
            city.gears = new[] { 3.54f, 1.91f, 1.28f, .97f, .76f }; city.fuel.capacityLitres = 40; city.fuel.idleLitresPerHour = .6f;
            city.flywheel.massKg = 6; city.wheels.assemblyMassKg = 18;
            city.torque = Curve(6500, 65, 95, 112, 118, 105, 85);
            Create("CityThree", city, new Color(.88f, .42f, .4f), 900, 0);
            var rally = coupe.spec.Copy(); rally.id = "rally-sedan"; rally.displayName = "Tempest AWD 2.0T";
            rally.artId = "coupe"; rally.massKg = 1450; rally.drive = DriveType.AWD; rally.engineBoreMm = 86;
            rally.frontWeightFraction = .58f; rally.wheelbaseM = 2.7f; rally.dragAreaM2 = .74f;
            rally.awd = new AwdSpec { mode = AwdMode.LimitedSlipCenter, frontBias = .45f, preload = .15f, isolateFailedAxle = true };
            rally.finalDrive = 3.9f; rally.tireFriction = 1.13f; rally.induction.turboBoostBar = .9f; rally.sound.boostBar = .9f;
            rally.induction.turboUnitFlowKgS = .28f; rally.induction.turboSpoolSeconds = .55f;
            rally.torque = Curve(7200, 120, 235, 350, 355, 310, 225);
            Create("RallySedan", rally, new Color(.4f, .48f, .88f), 8000, 6);
            var gt = hatch.spec.Copy(); gt.id = "touring-six"; gt.displayName = "Meridian GT 3.0"; gt.artId = "coupe";
            gt.drive = DriveType.RWD; gt.massKg = 1410; gt.frontWeightFraction = .51f; gt.wheelbaseM = 2.75f;
            gt.displacementLitres = 3; gt.engineBoreMm = 84; gt.sound.cylinders = 6; gt.sound.displacementLitres = 3; gt.sound.layout = EngineLayout.Inline;
            gt.induction.turboUnitFlowKgS = gt.induction.superchargerUnitFlowKgS = .315f;
            gt.gears = new[] { 3.83f, 2.2f, 1.52f, 1.22f, 1f, .81f }; gt.finalDrive = 3.46f; gt.wheelRadiusM = .315f;
            gt.flywheel.massKg = 10; gt.wheels.assemblyMassKg = 24; gt.fuel.capacityLitres = 60;
            gt.torque = Curve(7000, 135, 195, 270, 295, 280, 225);
            Create("TouringSix", gt, new Color(.68f, .7f, .76f), 6800, 4);
            var charged = muscle.spec.Copy(); charged.id = "super-v8"; charged.displayName = "Bison R 5.0 SC";
            charged.massKg = 1710; charged.engineBoreMm = 99; charged.engineBehavior = coupe.spec.engineBehavior.Copy(); charged.engineBehavior.camOverlap = .3f;
            charged.sound.induction = InductionType.Supercharger; charged.induction.factory = InductionType.Supercharger;
            charged.induction.superchargerBoostBar = .55f; charged.induction.superchargerUnitFlowKgS = .56f; charged.induction.superchargerFrictionNm = 10;
            charged.sound.boostBar = .55f; charged.transmission.type = TransmissionType.Automatic; charged.transmission.converterStallRpm = 2400;
            charged.finalDrive = 3.55f; charged.fuel.capacityLitres = 65; charged.tireFriction = 1.15f;
            charged.torque = Curve(6500, 270, 410, 555, 600, 555, 425);
            Create("SuperV8", charged, new Color(.73f, .3f, .43f), 11000, 10);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        static TorquePoint[] Curve(float redline, float idle, float a, float b, float c, float d, float end)
        { return new[] { new TorquePoint(900,idle), new TorquePoint(2000,a), new TorquePoint(3500,b), new TorquePoint(4500,c), new TorquePoint(5500,d), new TorquePoint(redline,end) }; }
        static void Create(string file, VehicleSpec spec, Color paint, int price, int rep)
        {
            string path = "Assets/DragLab/Resources/Data/Vehicles/" + file + ".asset";
            if (AssetDatabase.LoadAssetAtPath<VehicleDefinition>(path) != null) return;
            if (spec.Validate() != null) throw new InvalidOperationException(spec.id + ": " + spec.Validate());
            var asset = ScriptableObject.CreateInstance<VehicleDefinition>(); asset.spec = spec; asset.defaultPaint = paint; asset.purchasePrice = price; asset.reputationRequired = rep;
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
