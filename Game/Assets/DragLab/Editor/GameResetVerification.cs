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
    public static class GameResetVerification
    {
        public static void RunStandalone(){Debug.Log("Reset checks: "+Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../.."))));}
        public static int Run(string root)
        {
            int count = 0; var log = new StringBuilder();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException(label); count++; log.AppendLine("PASS: " + label); };
            string folder = Path.Combine(root, "reports/game-reset"); Directory.CreateDirectory(folder);
            string oldPath = ProfileStore.VerificationPath, oldAudio = SharedAudioSettings.VerificationPath;
            try
            {
                ProfileStore.VerificationPath = Path.Combine(folder, "profile-v1.json");
                SharedAudioSettings.VerificationPath = Path.Combine(folder, "audio-settings.json");
                foreach (string suffix in new[] { "", ".bak", ".legacy-v1", ".tmp", ".reset.tmp", ".reset-backup.tmp", ".reset-rollback.tmp" })
                    if (File.Exists(ProfileStore.FilePath + suffix)) File.Delete(ProfileStore.FilePath + suffix);
                var p = ProfileStore.Load(); VehicleCollection.Buy(p,p.catalog,p.vehicleStock.listings.First(o=>o.model=="starter-hatch")); p.cash = 98765; p.reputation = 100; p.garageLevel = 4; p.careerWins.Add("first-event");
                p.driving.ApplyPreset(DrivingPreset.Manual); p.driving.clutchReleaseSeconds = .31f; p.driving.shutdownBrakeAssist = false;
                p.driving.soundVolume = .19f; p.difficulty = 2;
                p.driving.ApplySimulationPreset(SimulationDifficulty.Balanced); p.driving.optimalThermals = true;
                p.bestQuarterSeconds = 12; p.SelectedCar.bestQuarterSeconds = 12; p.SelectedCar.completedRuns = 9;
                p.SelectedCar.lastPass = new PassRecord { elapsedSeconds = 12 }; p.SelectedCar.SavePreset(0);
                p.SelectedCar.paintHex = "#123456"; p.SelectedCar.tune.finalDrive = 5;
                p.cars.Add(new OwnedCar { vehicleId = "sport-coupe" }); MechanicsOwnership.Ensure(p, p.catalog);
                PartOperations.Buy(p, p.catalog, p.cars[0].instanceId, p.catalog.Id("starter-hatch", PartCategory.Pistons, 1), true);
                p.parts[0].condition.health = .25f; p.parts[0].condition.failed = true; p.parts[0].condition.failureReason = "fixture failure";
                p.runCheckpoints.Add(new RunCheckpoint { runId = "old-run", ownedVehicleId = p.cars[0].instanceId });
                check(ProfileStore.TrySave(p) && ProfileStore.TrySave(p), "Earned multi-car fixture and recovery backup saved in isolation.");
                string before = JsonUtility.ToJson(p), beforeDisk = File.ReadAllText(ProfileStore.FilePath), beforeBackup = File.ReadAllText(ProfileStore.FilePath + ".bak");
                string preferences = JsonUtility.ToJson(p.driving); var oldIds = p.parts.Select(x => x.instanceId).ToArray();
                using (var locked = new FileStream(ProfileStore.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    check(!ProfileStore.TryReset(p, out var failed) && ReferenceEquals(failed, p), "Locked primary rejects reset without publishing a fresh in-memory garage.");
                }
                check(JsonUtility.ToJson(p) == before && File.ReadAllText(ProfileStore.FilePath) == beforeDisk && File.ReadAllText(ProfileStore.FilePath + ".bak") == beforeBackup, "Failed reset preserves original profile and restores original backup.");
                SharedAudioSettings.ReloadForVerification(); SharedAudioSettings.SetMasterVolume(.47f); string audio = File.ReadAllText(SharedAudioSettings.FilePath);
                long start = DateTime.UtcNow.Ticks;
                check(ProfileStore.TryReset(p, out var fresh), "Reset commits a new garage and matching recovery baseline.");
                check(fresh.cash == 4700 && fresh.reputation == 0 && fresh.garageLevel == 1 && fresh.careerWins.Count == 0 && fresh.bestQuarterSeconds == 0, "Cash, reputation, career, business and global records reset to new-game values.");
                check(fresh.cars.Count==0&&fresh.engines.Count==0&&fresh.parts.Count==0&&fresh.firstPurchasePending&&fresh.vehicleStock.listings.Count==3,"Reset clears owned assets and offers fixed starter choices.");
                check(fresh.vehicleStock.listings.SelectMany(o=>o.parts).All(x=>!oldIds.Contains(x.instanceId)),"Seller components cannot alias previous owned identities.");
                check(fresh.savedSetups.Count==0&&fresh.selectedOwnedVehicleId==""&&fresh.selectedVehicleId=="","Saved tunes and selected-car references are cleared.");
                check(fresh.runCheckpoints.Count == 0 && fresh.idleClaimUtcTicks >= start && fresh.PendingIdle(DateTime.UtcNow.Ticks) == 0, "Old run settlements and accrued idle income are cleared.");
                check(JsonUtility.ToJson(fresh.driving) == preferences && fresh.difficulty == 2 && !ReferenceEquals(fresh.driving, p.driving), "All driving preferences and difficulty survive as an independent copy.");
                check(File.ReadAllText(SharedAudioSettings.FilePath) == audio && SharedAudioSettings.MasterVolume == .47f, "Reset does not rewrite volume preferences or change live master gain.");
                check(before == JsonUtility.ToJson(p) && fresh.revision == p.revision + 1, "Original object is untouched and old transaction revisions are invalidated.");
                var loaded = ProfileStore.Load();
                check(loaded.firstPurchasePending&&loaded.cars.Count==0&&SnapshotEquality.Serialized(JsonUtility.ToJson(fresh.vehicleStock),loaded.vehicleStock) && loaded.cash == 4700 && JsonUtility.ToJson(loaded.driving) == preferences && loaded.difficulty == 2, "Reload retains the exact new ownership tree and preserved preferences.");
                File.WriteAllText(ProfileStore.FilePath, "corrupt-after-reset");
                var recovered = ProfileStore.Load();
                check(recovered.firstPurchasePending&&recovered.cars.Count==0 && recovered.cash == 4700 && recovered.careerWins.Count == 0 && recovered.parts.Count==0, "Backup recovery cannot resurrect pre-reset progression or inventory.");
                check(ProfileStore.TrySave(recovered), "Recovered reset baseline can save normally.");
                SharedAudioSettings.SetMasterVolume(0); audio = File.ReadAllText(SharedAudioSettings.FilePath);
                check(ProfileStore.TryReset(recovered, out var muted) && SharedAudioSettings.MasterVolume == 0 && File.ReadAllText(SharedAudioSettings.FilePath) == audio, "Persisted mute survives another reset unchanged.");
                File.WriteAllText(ProfileStore.FilePath, "{\"saveVersion\":999}"); ProfileStore.Load();
                check(!ProfileStore.TryReset(muted, out _) && File.ReadAllText(ProfileStore.FilePath).Contains("999"), "Unsupported newer-save write protection also applies to reset.");
                File.WriteAllText(ProfileStore.FilePath, JsonUtility.ToJson(muted)); ProfileStore.Load();
            }
            finally
            {
                ProfileStore.VerificationPath = oldPath; SharedAudioSettings.VerificationPath = oldAudio; SharedAudioSettings.ClearVerificationCache();
            }
            File.WriteAllText(Path.Combine(folder, "verification.txt"), log + "Checks: " + count + "\n"); return count;
        }
    }
}
