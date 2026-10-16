using System;
using System.IO;
using System.Collections;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator ResetGameRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log = new System.Text.StringBuilder("Settings Reset game / actual Windows mouse / isolated save.\n");
            Action<bool, string> require = (ok, label) => { Check(ok, label); log.AppendLine("PASS: " + label); };
            var original = game.Profile;
            original.cash = 54321; original.reputation = 80; original.garageLevel = 4;
            original.careerWins.Add("fixture-win"); original.bestQuarterSeconds = 11;
            original.driving.fluidDepletion=ConsumptionMode.Reduced;original.driving.fluidAging=ConsumptionMode.None;original.driving.conditionEffects=ConditionEffectMode.Reduced;original.driving.serviceAssist=ServiceAssist.AutoTopUp;
            original.driving.ApplyPreset(DrivingPreset.Manual); original.driving.clutchReleaseSeconds = .32f;
            original.driving.shutdownBrakeAssist = false; original.difficulty = 2;
            original.cars.Add(new OwnedCar { vehicleId = "sport-coupe" }); MechanicsOwnership.Ensure(original, original.catalog);
            PartOperations.Buy(original, original.catalog, original.SelectedCar.instanceId, original.catalog.Id("starter-hatch", PartCategory.Pistons, 1), true);
            original.SelectedCar.SavePreset(0); original.SelectedCar.tune.finalDrive = 5;
            original.SelectedCar.lastPass = new PassRecord { elapsedSeconds = 11 }; original.SelectedCar.completedRuns = 7;
            original.SelectedCar.paintHex = "#111111";
            ProfileStore.Save(original); SharedAudioSettings.SetMasterVolume(.42f);
            game.OpenAudioSettings(); yield return new WaitForSecondsRealtime(.3f);
            using (var mouse = new WindowsKeyboardProbe())
            {
                yield return ClickSimulation(mouse, "Arcade");
                require(game.settings.wearMode == WearMode.None && game.settings.fuelUsage == FuelUsageMode.None && game.settings.optimalThermals && game.settings.clutchAssist && game.settings.stallPrevention, "Actual Arcade button applies shared no-wear/no-fuel-usage, optimal-temperature and driver-assist rules.");
                yield return ClickSimulation(mouse, "Balanced");
                require(game.settings.wearMode == WearMode.Partial && game.settings.WearRate == .25 && game.settings.fuelUsage == FuelUsageMode.Reduced && game.settings.FuelUsageRate == .25 && !game.settings.optimalThermals && game.settings.clutchAssist && game.settings.shifting == ShiftMode.ManualClutch && game.settings.launch == LaunchMode.Manual && game.Profile.difficulty == 2 && SharedAudioSettings.MasterVolume == .42f, "Actual Balanced button selects 25% wear/fuel and simulated heat without changing shift/launch, AI or volume preferences.");
                foreach (var mode in new[] { FuelUsageMode.None, FuelUsageMode.Reduced, FuelUsageMode.Realistic }) {
                    yield return ClickFuelUsage(mouse, mode);
                    require(game.settings.fuelUsage == mode && game.settings.simulationDifficulty == SimulationDifficulty.Custom && game.settings.wearMode == WearMode.Partial, "Actual fuel setting independently selects " + mode + " and marks difficulty Custom.");
                }
                yield return ClickFuelUsage(mouse, FuelUsageMode.Reduced);
                game.StartPractice(); require(game.Race.player.driver.fuelUsage == FuelUsageMode.Reduced, "Real practice race receives the selected Reduced fuel rule.");
                game.ReturnToGarage(); original = game.Profile; game.OpenAudioSettings(); yield return new WaitForSecondsRealtime(.3f);
            }
            SavedSetups.Save(original, original.SelectedCar.instanceId, "Reset fixture setup", SetupScope.WholeCar);
            AssemblyOperations.Detach(original, original.cars.First(c => c.vehicleId == "sport-coupe").instanceId);
            ProfileStore.Save(original);
            string before = JsonUtility.ToJson(original), saved = File.ReadAllText(ProfileStore.FilePath);
            string prefs = JsonUtility.ToJson(original.driving), audio = File.ReadAllText(SharedAudioSettings.FilePath);
            game.OpenAudioSettings(); yield return new WaitForSecondsRealtime(.3f);
            using (var mouse = new WindowsKeyboardProbe())
            {
                yield return ClickReset(mouse, "request");
                require(ReferenceEquals(game.Profile, original) && File.ReadAllText(ProfileStore.FilePath) == saved, "Opening reset confirmation does not change or save progression.");
                yield return Capture("reset-confirmation");
                yield return ClickReset(mouse, "cancel");
                require(before == JsonUtility.ToJson(game.Profile) && File.ReadAllText(ProfileStore.FilePath) == saved, "Actual Cancel keeps all progress, inventory and tunes.");
                yield return ClickReset(mouse, "request");
                // Leaving the page also dismisses confirmation and invalidates its action.
                game.ReturnToGarage();
                require(!game.CommitGameReset(original.revision) && ReferenceEquals(game.Profile, original), "An old confirmation cannot reset from Garage or an active race.");
                game.OpenAudioSettings(); yield return new WaitForSecondsRealtime(.3f);
                yield return ClickReset(mouse, "request"); yield return ClickReset(mouse, "confirm");
            }
            var fresh = game.Profile;
            require(!ReferenceEquals(fresh, original) && fresh.cash == 4700 && fresh.reputation == 0 && fresh.garageLevel == 1 && fresh.careerWins.Count == 0, "Actual Reset progression starts fresh cash, career and garage business.");
            require(fresh.cars.Count==0&&fresh.parts.Count==0&&fresh.engines.Count==0&&fresh.firstPurchasePending&&fresh.vehicleStock.listings.Count==3,"Reset opens dealership with fixed choices and no gifted ownership.");
            require(fresh.savedSetups.Count==0&&fresh.bestQuarterSeconds==0,"Saved setups and race records cleared.");
            require(JsonUtility.ToJson(game.settings) == prefs && ReferenceEquals(game.settings, fresh.driving) && fresh.difficulty == 2, "Live driving settings and AI difficulty remain bound to the preserved copy.");
            require(SharedAudioSettings.MasterVolume == .42f && File.ReadAllText(SharedAudioSettings.FilePath) == audio, "Live volume and its persisted settings are unchanged.");
            require(game.Race == null && fresh.runCheckpoints.Count == 0 && fresh.PendingIdle(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks) == 0, "Old race state, run settlements and banked offline income are cleared.");
            var reloaded = ProfileStore.Load();
            require(reloaded.firstPurchasePending&&reloaded.cars.Count==0 && reloaded.cash == 4700 && JsonUtility.ToJson(reloaded.driving) == prefs && reloaded.difficulty == 2, "Reload retains the reset garage and both driving preferences.");
            SharedAudioSettings.Reload(); require(SharedAudioSettings.MasterVolume == .42f, "Reload retains the shared master volume.");
            require(!game.CommitGameReset(original.revision) && ReferenceEquals(game.Profile, fresh), "Double or stale reset action cannot create another identity tree.");
            yield return Capture("reset-complete");
            VehicleCollection.Buy(fresh,fresh.catalog,fresh.vehicleStock.listings.First(o=>o.model=="starter-hatch"));ProfileStore.Save(fresh);game.ReturnToGarage();game.StartPractice();
            require(game.ScreenName == "Race" && game.Race.player.state.gear == 0 && game.Race.player.driver.shifting == ShiftMode.ManualClutch, "The fresh starter can enter neutral practice with preserved manual settings.");
            require(!game.CommitGameReset(fresh.revision), "Reset is blocked during a race.");
            game.ReturnToGarage();
            require(errors == 0, "Reset Settings UI and fresh garage have no runtime/GUI errors.");
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), log + "Runtime errors: " + errors + "\n");
#else
            yield return null; throw new InvalidOperationException("Reset UI probe requires Windows development player.");
#endif
        }
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator ClickFuelUsage(WindowsKeyboardProbe mouse, FuelUsageMode mode)
        {
            yield return ScrollSettingsControl(()=>SimulationSettingsView.fuelUsageRect);
            Rect rect = SimulationSettingsView.fuelUsageRect;
            if (rect.center.y < 125 || rect.center.y >= Screen.height - 60) throw new InvalidOperationException("Fuel setting is not visible.");
            int choice = mode == FuelUsageMode.None ? 0 : mode == FuelUsageMode.Reduced ? 1 : 2;
            int x = (int)(rect.x + rect.width * (choice + .5f) / 3), y = (int)rect.center.y;
            mouse.MouseButtonAt(x,y,true);yield return new WaitForSecondsRealtime(.06f);mouse.MouseButtonAt(x,y,false);yield return new WaitForSecondsRealtime(.25f);
        }
        IEnumerator ScrollSettingsControl(Func<Rect> control)
        {
            var field=typeof(PrototypeGame).GetField("scroll",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(int attempt=0;attempt<5;attempt++){
                yield return new WaitForEndOfFrame();var rect=control();
                if(rect.height>0&&rect.center.y>=125&&rect.center.y<Screen.height-60)yield break;
                var value=(Vector2)field.GetValue(game);value.y=Mathf.Max(0,value.y+rect.center.y-Screen.height*.5f);field.SetValue(game,value);
                yield return new WaitForSecondsRealtime(.1f);
            }
        }
        IEnumerator ClickSimulation(WindowsKeyboardProbe mouse, string key)
        {
            yield return new WaitForEndOfFrame();
            Rect rect; if (!SimulationSettingsView.presetRects.TryGetValue(key, out rect) || rect.center.y >= Screen.height) throw new InvalidOperationException("Simulation setting not visible: " + key);
            mouse.MouseButtonAt((int)rect.center.x, (int)rect.center.y, true); yield return new WaitForSecondsRealtime(.06f); mouse.MouseButtonAt((int)rect.center.x, (int)rect.center.y, false); yield return new WaitForSecondsRealtime(.2f);
        }
        IEnumerator ClickReset(WindowsKeyboardProbe mouse, string key)
        {
            yield return new WaitForEndOfFrame();
            Rect rect; Check(game.resetRects.TryGetValue(key, out rect) && rect.height > 0 && rect.center.y < Screen.height, "Visible reset control: " + key);
            mouse.MouseButtonAt((int)rect.center.x, (int)rect.center.y, true); yield return new WaitForSecondsRealtime(.06f);
            mouse.MouseButtonAt((int)rect.center.x, (int)rect.center.y, false); yield return new WaitForSecondsRealtime(.25f);
        }
#endif
    }
}
