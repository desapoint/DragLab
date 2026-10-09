using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;

namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator AxleFailureRun()
        {
            var log = new StringBuilder("Component failures / actual Windows race Update, OnGUI, DSP and persistence / isolated fixture / driver-boundary inputs, no OS input.\n");
            var catalog = game.Profile.catalog; var owned = game.Profile.SelectedCar;
            var brakes = game.Profile.parts.Find(p => p.ownerId == owned.instanceId && p.slot == "Brakes");
            brakes.condition.health = 0; brakes.condition.failed = true; brakes.condition.failureReason = "Runtime brake fixture";
            game.settings.ApplyPreset(DrivingPreset.Automatic); game.settings.ApplySimulationPreset(SimulationDifficulty.Simulation);
            game.verificationInput = () => new DriverInput { throttle = game.Race.player.state.launched ? 1 : 0 };
            game.StartPractice();
            Check(game.ScreenName == "Race", "Failed brake system no longer blocks entering a practice race.");
            yield return AwaitFinish(); yield return new WaitForSecondsRealtime(1);
            var car = game.Race.player;
            Check(car.state.finished && !car.state.engineFailed && !car.state.drivetrainFailed && car.state.engineDamage == 0 && car.state.brakesFailed && car.state.speed > 10 && car.state.brakeFrictionPowerW == 0, "Failed brakes allow race completion and engine operation, but automatic shutdown cannot brake.");
            log.AppendLine("PASS: Brake failure leaves engine/propulsion healthy; actual shutdown coasts without braking heat.");
            File.WriteAllText(Path.Combine(output, "brake-failure.csv"), game.Race.telemetry.Csv()); yield return Capture("brake-failure-shutdown");
            string brakeId = brakes.instanceId; game.ReturnToGarage();
            Check(ProfileStore.Load().parts.Any(p => p.instanceId == brakeId && p.condition.failed), "Brake failure persists as brake condition, not a destroyed engine.");
            log.AppendLine("PASS: Brake failure is individually persisted after return to garage.");
            var vehicle = game.vehicles.First(v => v.spec.id == "utility-pickup");
            game.Profile.cars.Add(new OwnedCar { vehicleId = vehicle.spec.id, tune = vehicle.CreateStockTune() }); MechanicsOwnership.Ensure(game.Profile, catalog);
            owned = game.Profile.cars.First(v => v.vehicleId == vehicle.spec.id); game.Profile.selectedVehicleId = owned.vehicleId; game.Profile.selectedOwnedVehicleId = owned.instanceId;
            var draft = new TuneDraft(owned, game.Profile.revision); draft.values.awdConfigured = true; draft.values.awd = new AwdSpec { mode = AwdMode.LockedCenter }; draft.ApplyTo(game.Profile);
            game.StartPractice(); float until = Time.realtimeSinceStartup + 15;
            while (game.Race.player.state.speed < 15 && Time.realtimeSinceStartup < until) yield return null;
            Check(game.Race.player.spec.drive == Domain.DriveType.AWD && game.Race.player.awd.mode == AwdMode.LockedCenter && game.Race.player.state.speed >= 15, "Saved AWD configuration is used by the actual race.");
            car = game.Race.player; var frontShaft = car.mechanics.parts.Find(p => p.slot == "FrontDriveshaft"); string shaftId = frontShaft.instanceId;
            frontShaft.condition.health = 0; frontShaft.condition.failed = true; frontShaft.condition.failureReason = "Runtime disconnected front shaft";
            yield return new WaitForSecondsRealtime(.5f);
            Check(!car.state.frontDriveAvailable && car.state.rearDriveAvailable && !car.state.drivetrainFailed && !car.state.engineFailed && car.state.frontDriveForceN == 0 && car.state.frontTorqueShare == 0, "Broken front shaft disconnects its branch; locked AWD retains rear drive and engine operation.");
            yield return AwaitFinish(); yield return AwaitStop();
            Check(car.state.finished && car.state.speed == 0 && !car.state.engineFailed && !car.state.drivetrainFailed && !car.state.brakesFailed, "Surviving-axle AWD finishes the actual race and stops using its healthy brakes.");
            log.AppendLine("PASS: Mid-race front shaft failure retains rear propulsion, completes the quarter and stops normally.");
            File.WriteAllText(Path.Combine(output, "awd-branch-failure.csv"), game.Race.telemetry.Csv()); yield return Capture("awd-surviving-rear-drive"); game.ReturnToGarage();
            Check(ProfileStore.Load().parts.Any(p => p.instanceId == shaftId && p.condition.failed), "Individual AWD branch failure persists without failing healthy branches.");
            log.AppendLine("PASS: AWD branch identity and failure persist through save/reload.");
            // The saved broken front shaft now exercises an open center's loss of reaction torque.
            // The carbureted pickup has no OEM limiter. Install protection so sustained
            // wide-open unloaded revving is not confused with a drive-path failure.
            owned = game.Profile.SelectedCar;
            PartOperations.Buy(game.Profile, catalog, owned.instanceId, catalog.Id(owned.vehicleId, PartCategory.Controls, 1), true);
            PartOperations.Buy(game.Profile, catalog, owned.instanceId, catalog.Id(owned.vehicleId, PartCategory.RevLimiter, 1), true);
            draft = new TuneDraft(owned, game.Profile.revision); draft.values.awd = new AwdSpec { mode = AwdMode.OpenCenter }; draft.values.awdConfigured = true;
            draft.values.limiter = LimiterStyle.Hard; draft.values.revLimitRpm = vehicle.spec.redlineRpm - 200; draft.ApplyTo(game.Profile);
            game.StartPractice(); yield return new WaitForSecondsRealtime(6);
            car = game.Race.player;
            double freeWork = game.Race.telemetry.samples.Sum(s => Math.Max(0, s.frontShaftPowerW)), roadWork = game.Race.telemetry.samples.Sum(s => Math.Max(0, s.rearShaftPowerW));
            File.WriteAllText(Path.Combine(output, "awd-open-center-state.json"), JsonUtility.ToJson(car.state, true));
            File.WriteAllText(Path.Combine(output, "awd-open-center-free-shaft.csv"), game.Race.telemetry.Csv());
            Check(car.state.launched && car.state.openCenterFreeBranch && !car.state.engineFailed && car.state.rpm > car.spec.idleRpm && car.state.frontShaftRpm > 1000 && freeWork > Math.Max(1, roadWork) * 5 && car.state.speed < 2, "Open center routes most power into broken free shaft while engine runs and useful propulsion collapses in the actual race.");
            yield return Capture("awd-open-center-disconnected"); game.ReturnToGarage();
            log.AppendLine("PASS: Same saved failure with open center spins the free shaft, consuming most shaft power while preserving engine combustion.");
            game.verificationInput = null; Check(errors == 0, "No runtime, GUI or DSP errors in component-failure tests.");
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), log + "Runtime errors: " + errors + "\n");
        }
    }
}
