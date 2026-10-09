using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator StockDurabilityRun()
        {
            var log = new System.Text.StringBuilder("Stock OEM Windows races / real Update, shutdown, DSP and saved condition / isolated fixture / no OS input.\n");
            foreach (var vehicle in game.vehicles)
            {
                game.ReturnToGarage();
                if (game.Profile.cars.All(c => c.vehicleId != vehicle.spec.id)) game.Profile.cars.Add(new OwnedCar { vehicleId = vehicle.spec.id, tune = vehicle.CreateStockTune() });
                game.Profile.selectedVehicleId = vehicle.spec.id;
                MechanicsOwnership.Ensure(game.Profile, game.Profile.catalog);
                game.settings.ApplyPreset(DrivingPreset.Automatic); game.settings.ApplySimulationPreset(SimulationDifficulty.Simulation);
                game.verificationInput = () => new DriverInput { throttle = game.Race.player.state.launched ? 1 : 0 };
                game.StartPractice();
                Check(game.ScreenName == "Race" && game.Race.player.driver.WearRate == 1 && !game.Race.player.driver.optimalThermals, "Stock race uses full wear and simulated thermals.");
                yield return AwaitFinish(); yield return AwaitStop();
                var car = game.Race.player;
                Check(car.state.finished && !car.state.engineFailed && !car.state.drivetrainFailed && car.mechanics.parts.All(p => !p.condition.failed && p.condition.health > .98f), "Stock race and actual shutdown keep all OEM parts healthy: " + vehicle.spec.id);
                log.AppendLine("PASS: " + vehicle.spec.id + " / ET " + car.state.elapsed.ToString("F3") + " s / lowest health " + car.mechanics.parts.Min(p => p.condition.health).ToString("P2") + " / brakes " + car.mechanics.parts.Find(p => p.slot == "Brakes").condition.temperatureC.ToString("F1") + " C / stopped without a breakdown.");
                yield return Capture("stock-durability-" + vehicle.spec.id);
                var beforeCooldown = car.mechanics.parts.Select(p => p.Copy()).ToList(); game.ReturnToGarage();
                // Garage return advances the same car through engine-off cooldown before saving.
                // Compare that committed boundary, while separately ensuring no wear is healed.
                var actual = car.mechanics.parts.Select(p => p.Copy()).ToList();
                Check(actual.All(p => beforeCooldown.Any(before => before.instanceId == p.instanceId && p.condition.health <= before.condition.health && (!before.condition.failed || p.condition.failed))), "Garage cooldown retains actual identities and does not repair OEM wear: " + vehicle.spec.id);
                log.AppendLine("PASS: Garage cooldown retains identities and wear: " + vehicle.spec.id);
                var saved = ProfileStore.Load();
                Check(actual.All(p => saved.parts.Any(owned => owned.instanceId == p.instanceId && owned.condition.health == p.condition.health && owned.condition.failed == p.condition.failed && Math.Abs(owned.condition.temperatureC - p.condition.temperatureC) < 1e-6)), "Actual condition persists after race/shutdown/garage: " + vehicle.spec.id);
                log.AppendLine("PASS: Actual part conditions persist without repair: " + vehicle.spec.id);
            }
            game.verificationInput = null;
            Check(errors == 0, "Stock durability player has no runtime/GUI/DSP errors.");
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), log + "Runtime errors: " + errors + "\n");
        }
    }
}
