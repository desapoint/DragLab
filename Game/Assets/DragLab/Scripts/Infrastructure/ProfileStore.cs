using System;
using System.IO;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    public static class ProfileStore
    {
        static bool recoveredBackup;
        static bool writeBlocked;
#if UNITY_EDITOR
        // Editor checks isolate their fixtures from the actual player's save.
        public static string VerificationPath;
#endif
        public static string FilePath {
            get {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(VerificationPath)) return VerificationPath;
#endif
                string[] args = Environment.GetCommandLineArgs();
                int probe = Array.IndexOf(args, "--draglab-verify");
                if (probe < 0) probe = Array.IndexOf(args, "--draglab-bench-verify");
                if (Debug.isDebugBuild && probe >= 0 && probe + 1 < args.Length)
                    return Path.Combine(Path.GetFullPath(args[probe + 1]), "profile-v1.json");
                return Path.Combine(Application.persistentDataPath, "profile-v1.json");
            }
        }
        public static string Status { get; private set; }
        public static PlayerProfile Load(bool recoverInterrupted=false)
        {
            long now = DateTime.UtcNow.Ticks;
            recoveredBackup = false;
            writeBlocked = false;
            foreach (string path in new[] { FilePath, FilePath + ".bak" })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    string json = File.ReadAllText(path);
                    var profile = JsonUtility.FromJson<PlayerProfile>(json);
                    if (profile == null) throw new InvalidDataException("Empty profile.");
                    if (profile.saveVersion > MechanicsOwnership.SaveVersion || profile.mechanicsVersion > MechanicsSchema.Version)
                    { writeBlocked = true; throw new InvalidDataException("Newer save schema; writing is disabled to preserve it."); }
                    // Optional version-1 additions: keep old saves audible and smoothly drivable.
                    if (profile.driving == null) profile.driving = new DriverSettings();
                    if (json.IndexOf("\"wearMode\"", StringComparison.Ordinal) < 0) profile.driving.wearMode = WearMode.Full;
                    if (json.IndexOf("\"wearMultiplier\"", StringComparison.Ordinal) < 0) profile.driving.wearMultiplier = 1;
                    if (json.IndexOf("\"simulationDifficulty\"", StringComparison.Ordinal) < 0) profile.driving.simulationDifficulty = SimulationDifficulty.Custom;
                    if (json.IndexOf("\"clutchReleaseSeconds\"", StringComparison.Ordinal) < 0) profile.driving.clutchReleaseSeconds = .18f;
                    if (json.IndexOf("\"soundVolume\"", StringComparison.Ordinal) < 0) profile.driving.soundVolume = .35f;
                    if (json.IndexOf("\"shutdownBrakeAssist\"", StringComparison.Ordinal) < 0) profile.driving.shutdownBrakeAssist = true;
                    profile.Sanitize(now);
                    recoveredBackup = path.EndsWith(".bak");
                    if (profile.mechanicsVersion == 0)
                    {
                        MechanicsOwnership.Ensure(profile, MechanicsTransactions.Catalog());
                        string legacy = FilePath + ".legacy-v1";
                        if (!File.Exists(legacy)) File.WriteAllText(legacy, json);
                        // Publish migrated IDs only after they are durable. Loading the same v1
                        // profile again before a gameplay save must not assign a new identity tree.
                        if (!writeBlocked && !TrySave(profile)) throw new InvalidDataException("Migration save failed; original backup retained.");
                    }
                    else
                    {
                        int priorVersion = profile.saveVersion;
                        bool migrate = profile.saveVersion < MechanicsOwnership.SaveVersion || profile.drivelineVersion == 0;
                        MechanicsOwnership.Ensure(profile, MechanicsTransactions.Catalog());
                        if (migrate)
                        {
                            string original = FilePath + ".legacy-v" + priorVersion;
                            if (!File.Exists(original)) File.WriteAllText(original, json);
                            if (!writeBlocked && !TrySave(profile)) throw new InvalidDataException("Driveline migration save failed; original retained.");
                        }
                    }
                    recoveredBackup = path.EndsWith(".bak");
                    Status = path.EndsWith(".bak") ? "Recovered previous save." : "Save loaded.";
                    if(recoverInterrupted && !writeBlocked && RunPersistence.RecoverInterrupted(profile)){
                        bool saved=TrySave(profile);Status=saved?"Recovered interrupted run: committed wear/fuel retained; no reward added.":"Interrupted run retained in memory; "+Status;
                    }
                    return profile;
                }
                catch (Exception e) { Debug.LogWarning("Could not read " + path + ": " + e.Message); }
            }
            if (File.Exists(FilePath) || File.Exists(FilePath + ".bak")) writeBlocked = true;
            Status = writeBlocked ? "Save could not be recovered; originals preserved and writing disabled." : "New garage.";
            var fresh = PlayerProfile.New(now); MechanicsOwnership.Ensure(fresh, MechanicsTransactions.Catalog()); return fresh;
        }
        public static void Save(PlayerProfile profile)
        {
            if (!TrySave(profile)) Debug.LogError(Status);
        }
        public static bool TryReset(PlayerProfile current, out PlayerProfile committed)
        {
            committed = current;
            string primaryTemp = FilePath + ".reset.tmp", backupTemp = FilePath + ".reset-backup.tmp";
            string rollback = FilePath + ".reset-rollback.tmp", backup = FilePath + ".bak";
            bool backupChanged = false, rollbackFailed = false, hadBackup = File.Exists(backup);
            try
            {
                if (writeBlocked) throw new InvalidDataException("Save writes are blocked; existing save preserved.");
                var fresh = current.RestartKeepingPreferences(DateTime.UtcNow.Ticks);
                MechanicsOwnership.Ensure(fresh, MechanicsTransactions.Catalog());
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                WriteDurable(primaryTemp, fresh); WriteDurable(backupTemp, fresh);
                // Replace recovery data first: a successful reset must never recover
                // pre-reset cars/cash from the old backup after primary corruption.
                if (hadBackup) File.Copy(backup, rollback, true);
                Publish(backupTemp, backup); backupChanged = true;
                Publish(primaryTemp, FilePath);
                recoveredBackup = false;
                committed = fresh; Status = "Game reset. Driving and volume settings kept.";
                return true;
            }
            catch (Exception e)
            {
                if (backupChanged)
                {
                    try { if (hadBackup) Publish(rollback, backup); else File.Delete(backup); }
                    catch (Exception recovery) { rollbackFailed = true; Debug.LogWarning("Reset backup rollback failed; recovery copy retained: " + recovery.Message); }
                }
                Status = "RESET FAILED: " + e.Message;
                return false;
            }
            finally
            {
                foreach (string path in new[] { primaryTemp, backupTemp, rollback })
                    try { if (path != rollback || !rollbackFailed) { if (File.Exists(path)) File.Delete(path); } } catch (Exception) { }
            }
        }
        static void WriteDurable(string path, PlayerProfile profile)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(profile, true));
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
        }
        static void Publish(string temp, string path)
        {
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        public static bool TrySave(PlayerProfile profile)
        {
            try
            {
                if (writeBlocked) throw new InvalidDataException("Save writes blocked because a newer schema was encountered.");
                MechanicsOwnership.Ensure(profile, MechanicsTransactions.Catalog());
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + ".tmp";
                // Flush to disk before replacing the previous complete profile.
                WriteDurable(temp, profile);
                if (File.Exists(FilePath) && !recoveredBackup) File.Copy(FilePath, FilePath + ".bak", true);
                // Temp is in the same directory, permitting an atomic file rename/replace.
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
                else File.Move(temp, FilePath);
                recoveredBackup = false;
                Status = "Saved.";
                return true;
            }
            catch (Exception e)
            {
                Status = "SAVE FAILED: " + e.Message;
                return false;
            }
        }
    }
}
