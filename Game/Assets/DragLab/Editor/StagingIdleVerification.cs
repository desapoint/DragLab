using System;
using System.IO;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using UnityEngine;

namespace DragLab.Editor
{
    public static class StagingIdleVerification
    {
        public static void Powertrain() => Debug.Log("POWERTRAIN PASS: " + PowertrainVerification.Run(Path.GetFullPath(Path.Combine(Application.dataPath, "../.."))));
        public static void Run()
        {
            int checks = 0; var log = new StringBuilder();
            void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks++; log.AppendLine("PASS: " + label); }
            foreach (var asset in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
            foreach (var mode in new[] { LaunchMode.Assisted, LaunchMode.Manual })
            {
                var driver = new DriverSettings { launch = mode, throttleAssist = false, wearMode = WearMode.None, optimalThermals = true };
                var car = new CarSimulation(asset.spec, asset.CreateStockTune(), driver);
                string id = asset.spec.id + " / " + mode;
                Check(car.state.rpm == car.spec.idleRpm, id + ": first displayed state starts at idle.");
                var race = new RaceSimulation(car, new CarSimulation(asset.spec, asset.CreateStockTune(), new DriverSettings()), 402.336);
                for (int i = 0; i < 240; i++) race.Step(new DriverInput());
                Check(Math.Abs(car.state.rpm - car.spec.idleRpm) < car.spec.idleRpm * .17 && car.state.throttle == 0 && car.state.engineTime > 1,
                    id + ": untouched countdown ticks the running engine at idle without hidden throttle: " + car.state.rpm + "/" + car.spec.idleRpm + ", gas=" + car.state.throttle + ", engineTime=" + car.state.engineTime);
                for (int i = 0; i < 48; i++) race.Step(new DriverInput { throttle = .7 });
                Check(car.state.rpm > car.spec.idleRpm * 1.2, id + ": gas raises RPM before launch.");
                for (int i = 0; i < 720; i++) car.Stage(DragMath.FixedStep, new DriverInput());
                Check(car.state.rpm < car.spec.idleRpm * 1.17 && !car.state.launched && car.state.speed == 0,
                    id + ": released gas returns to idle and keeps the car staged.");
                car.state.stalled = true;
                for (int i = 0; i < 360; i++) car.Stage(DragMath.FixedStep, new DriverInput());
                Check(car.state.rpm < 1 && car.state.stalled, id + ": real stalls remain stopped rather than being hidden by the gauge.");
            }
            var automatic = new CarSimulation(new VehicleSpec(), new Tune(), new DriverSettings());
            automatic.Stage(DragMath.FixedStep, new DriverInput());
            Check(automatic.state.rpm == automatic.tune.launchRpm, "Automatic driver retains its explicit assisted launch target.");
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/staging-idle"));
            Directory.CreateDirectory(dir); log.AppendLine("Checks: " + checks);
            File.WriteAllText(Path.Combine(dir, "verification.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("STAGING IDLE PASS: " + checks);
        }
    }
}
