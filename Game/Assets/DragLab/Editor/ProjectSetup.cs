using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Runtime;
using DriveType = DragLab.Domain.DriveType;

namespace DragLab.Editor
{
    public static class ProjectSetup
    {
        const string Root = "Assets/DragLab/";

        [MenuItem("DragLab/Prepare First Slice")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Root + "Resources/Data/Vehicles");
            Directory.CreateDirectory(Root + "Resources/Data/Events");
            Directory.CreateDirectory(Root + "Scenes");
            AssetDatabase.Refresh();
            var hatch = MakeVehicle("StarterHatch", new VehicleSpec(), new Color(.35f, .81f, .74f), 0, 0);
            var coupe = MakeVehicle("SportCoupe", new VehicleSpec {
                id = "sport-coupe", displayName = "Kestrel 2.0T", artId = "coupe", drive = DriveType.RWD,
                massKg = 1320, frontWeightFraction = .53f, wheelbaseM = 2.65f, drivetrainEfficiency = .86f,
                redlineRpm = 7200, finalDrive = 3.7f, tireFriction = 1.12f,
                gears = new[] { 3.6f, 2.18f, 1.52f, 1.18f, .94f, .78f },
                torque = new[] { new TorquePoint(900, 110), new TorquePoint(2000, 190), new TorquePoint(3500, 275),
                    new TorquePoint(5000, 270), new TorquePoint(6200, 235), new TorquePoint(7200, 185) }
            }, new Color(.42f, .67f, .97f), 4200, 4);
            var muscle = MakeVehicle("V8Muscle", new VehicleSpec {
                id = "v8-muscle", displayName = "Bison 5.0", artId = "muscle", drive = DriveType.RWD,
                massKg = 1580, frontWeightFraction = .54f, wheelbaseM = 2.8f, centerOfGravityM = .52f,
                wheelRadiusM = .32f, dragAreaM2 = .79f, drivetrainEfficiency = .85f,
                redlineRpm = 6500, finalDrive = 3.55f, tireFriction = 1.08f,
                gears = new[] { 2.97f, 2.1f, 1.46f, 1.0f, .8f, .65f },
                torque = new[] { new TorquePoint(900, 230), new TorquePoint(2000, 340), new TorquePoint(3500, 425),
                    new TorquePoint(4500, 440), new TorquePoint(5500, 390), new TorquePoint(6500, 315) }
            }, new Color(.94f, .63f, .3f), 7500, 8);
            var pickup = MakeVehicle("UtilityPickup", new VehicleSpec {
                id = "utility-pickup", displayName = "Timber Utility 4.2", artId = "pickup", drive = DriveType.AWD,
                awd = new AwdSpec { mode = AwdMode.OnDemand, primaryFront = false, frontBias = .5f, maximumSecondary = .5f, responseSeconds = .2f, preload = .1f, isolateFailedAxle = true },
                massKg = 1920, frontWeightFraction = .56f, wheelbaseM = 3.15f, centerOfGravityM = .67f,
                wheelRadiusM = .35f, dragAreaM2 = 1.02f, drivetrainEfficiency = .84f,
                redlineRpm = 6000, finalDrive = 3.73f, tireFriction = 1.0f,
                gears = new[] { 3.8f, 2.25f, 1.5f, 1.0f, .78f },
                torque = new[] { new TorquePoint(900, 210), new TorquePoint(2000, 315), new TorquePoint(3500, 385),
                    new TorquePoint(4500, 365), new TorquePoint(5500, 300), new TorquePoint(6000, 255) }
            }, new Color(.74f, .57f, .51f), 6200, 8);

            var events = new[] {
                MakeEvent("01-FirstPass", "first-pass", "First pass", "Mika", hatch, 0, 500, 2, 0, 0),
                MakeEvent("02-ClubRun", "club-run", "Club run", "Jules", hatch, 2, 700, 2, 1, 1),
                MakeEvent("03-CoupeRival", "coupe-rival", "Coupe rival", "Ash", coupe, 4, 1000, 4, 0, 0),
                MakeEvent("04-LocalFinal", "local-final", "Local final", "Rowan", muscle, 8, 1600, 4, 0, 1)
            };
            string scenePath = Root + "Scenes/FirstSlice.unity";
            if (!File.Exists(scenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("DragLab First Slice").AddComponent<PrototypeGame>();
                root.vehicles = new[] { hatch, coupe, muscle, pickup }; root.career = events;
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            PrepareEngineBenchAssets();
            PaidOperationsVerification.Prepare();
            PlayerSettings.companyName = "DragLab";
            PlayerSettings.productName = "DragLab Prototype";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.draglab.prototype");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.draglab.prototype");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            AssetDatabase.SaveAssets();
            Debug.Log("DRAGLAB READY: scene, four cars, four career events and import settings prepared.");
            Verification.Run();
            if (!Application.isBatchMode) EditorSceneManager.OpenScene(scenePath);
        }

        static VehicleDefinition MakeVehicle(string file, VehicleSpec spec, Color paint, int price, int rep)
        {
            string path = Root + "Resources/Data/Vehicles/" + file + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(path);
            if (existing != null) { PrepareSound(existing); PrepareInduction(existing); PreparePowertrain(existing); PrepareExhaust(existing); return existing; } // Preserve subsequent engineering/art tuning.
            var asset = ScriptableObject.CreateInstance<VehicleDefinition>();
            asset.spec = spec; asset.defaultPaint = paint; asset.purchasePrice = price; asset.reputationRequired = rep;
            PrepareSound(asset);
            PrepareInduction(asset);
            PreparePowertrain(asset);
            PrepareExhaust(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        static void PrepareSound(VehicleDefinition asset)
        {
            if (asset.spec.sound != null && asset.spec.sound.version > 0) return;
            var sound = new EngineSoundSpec { version = 1 };
            switch (asset.spec.id)
            {
                case "sport-coupe": sound.displacementLitres = 2; sound.induction = InductionType.Turbo; sound.blowOff = BlowOffType.Hiss; break;
                case "v8-muscle": sound.cylinders = 8; sound.displacementLitres = 5; sound.layout = EngineLayout.Crossplane; break;
                case "utility-pickup": sound.cylinders = 6; sound.displacementLitres = 4.2f; sound.layout = EngineLayout.Inline; break;
            }
            asset.spec.sound = sound; EditorUtility.SetDirty(asset);
        }
        static void PrepareExhaust(VehicleDefinition asset)
        {
            if (asset.spec.sound.exhaustSystem != null && asset.spec.sound.exhaustSystem.version > 0) return;
            asset.spec.sound.exhaustSystem = ExhaustSetup.Preset(asset.spec.id == "v8-muscle" ? 1 : asset.spec.id == "utility-pickup" ? 2 : 0);
            EditorUtility.SetDirty(asset);
        }
        public static void PrepareEngineBenchAssets()
        {
            VehicleRoster.Prepare();
            foreach (var car in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) { PrepareExhaust(car); PrepareFlywheel(car); }
            const string path = Root + "Scenes/EngineTest.unity";
            if (!File.Exists(path))
            {
                var mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
                var root = new GameObject("Engine Sound Test Bench").AddComponent<EngineTestScene>();
                SceneManager.MoveGameObjectToScene(root.gameObject, scene);
                root.vehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
                EditorSceneManager.SaveScene(scene, path);
                if (mode == NewSceneMode.Additive) EditorSceneManager.CloseScene(scene, true);
            }
            PrepareDrivingBench();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "Scenes/FirstSlice.unity", true), new EditorBuildSettingsScene(path, true), new EditorBuildSettingsScene(Root + "Scenes/DrivingTest.unity", true) };
            AssetDatabase.SaveAssets();
        }
        [MenuItem("DragLab/Open Engine Sound Test Scene")]
        public static void OpenEngineTest()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PrepareEngineBenchAssets(); EditorSceneManager.OpenScene(Root + "Scenes/EngineTest.unity");
        }
        static void PrepareDrivingBench()
        {
            const string path = Root + "Scenes/DrivingTest.unity";
            if (File.Exists(path)) return;
            var mode = Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            var root = new GameObject("Physical Driving Bench").AddComponent<PhysicalDrivingBench>(); SceneManager.MoveGameObjectToScene(root.gameObject, scene);
            EditorSceneManager.SaveScene(scene, path); if (mode == NewSceneMode.Additive) EditorSceneManager.CloseScene(scene, true);
        }
        [MenuItem("DragLab/Open Physical Driving Test Scene")]
        public static void OpenDrivingTest()
        { if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return; PrepareEngineBenchAssets(); EditorSceneManager.OpenScene(Root + "Scenes/DrivingTest.unity"); }
        static void PrepareInduction(VehicleDefinition asset)
        {
            if (asset.spec.induction != null && asset.spec.induction.version > 1) return;
            var sound = asset.spec.sound ?? new EngineSoundSpec();
            if (asset.spec.induction != null && asset.spec.induction.version == 1)
            {
                asset.spec.induction.factoryValve = sound.blowOff; asset.spec.induction.version = 2;
                EditorUtility.SetDirty(asset); return;
            }
            asset.spec.displacementLitres = sound.displacementLitres;
            var data = new InductionSpec { version = 2, factory = sound.induction, factoryValve = sound.blowOff,
                turboUnitFlowKgS = sound.displacementLitres * .105f, superchargerUnitFlowKgS = sound.displacementLitres * .105f };
            if (sound.induction == InductionType.Turbo) data.turboBoostBar = sound.boostBar;
            asset.spec.induction = data; EditorUtility.SetDirty(asset);
        }
        static void PreparePowertrain(VehicleDefinition asset)
        {
            if (asset.spec.engineBehavior != null && asset.spec.engineBehavior.version > 0) return;
            var engine = new EngineBehaviorSpec { version = 1 };
            var box = new TransmissionSpec { version = 1 };
            switch (asset.spec.id)
            {
                case "sport-coupe":
                    box.type = TransmissionType.DCT;
                    engine.limiter = LimiterStyle.Soft; engine.twoStep = engine.launchPreload = true;
                    engine.launchLimiter = LimiterStyle.Hard; engine.launchDropRpm = 280; engine.antiLagMaxSeconds = 5;
                    break;
                case "v8-muscle":
                    engine.fueling = FuelSystem.Carburetor; engine.limiter = LimiterStyle.None;
                    engine.camOverlap = .65f; engine.idleControl = .12f; engine.mixtureVariation = .4f;
                    engine.acceleratorPump = .65f; engine.mechanicalClatter = .4f;
                    break;
                case "utility-pickup":
                    box.type = TransmissionType.Automatic; box.converterStallRpm = 2300;
                    engine.fueling = FuelSystem.Carburetor; engine.limiter = LimiterStyle.None;
                    engine.camOverlap = .15f; engine.idleControl = .2f; engine.mixtureVariation = .2f;
                    engine.acceleratorPump = .8f; engine.mechanicalClatter = .3f;
                    break;
            }
            asset.spec.engineBehavior = engine;
            if (asset.spec.transmission == null || asset.spec.transmission.version == 0) asset.spec.transmission = box;
            if (asset.spec.wheels == null) asset.spec.wheels = new WheelSpec();
            EditorUtility.SetDirty(asset);
        }
        static void PrepareFlywheel(VehicleDefinition asset)
        {
            if (asset.spec.flywheel != null && asset.spec.flywheel.version > 0) return;
            asset.spec.flywheel = new FlywheelSpec { version = 1,
                massKg = asset.spec.id == "v8-muscle" ? 12 : asset.spec.id == "utility-pickup" ? 14 : asset.spec.id == "sport-coupe" ? 9 : 8,
                outerRadiusM = asset.spec.id == "v8-muscle" || asset.spec.id == "utility-pickup" ? .17f : .15f };
            EditorUtility.SetDirty(asset);
        }
        static CareerEventDefinition MakeEvent(string file, string id, string title, string rival, VehicleDefinition car, int rep, int cash, int winRep, int engine, int tires)
        {
            string path = Root + "Resources/Data/Events/" + file + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<CareerEventDefinition>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<CareerEventDefinition>();
            asset.stableId = id; asset.displayName = title; asset.rivalName = rival; asset.opponent = car;
            asset.reputationRequired = rep; asset.cashWin = cash; asset.reputationWin = winRep;
            asset.opponentTune = car.CreateStockTune(); asset.opponentTune.engineLevel = engine; asset.opponentTune.tireLevel = tires;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
