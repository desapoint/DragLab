using System;
using System.IO;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    internal static class LegacyProfileStore
    {
        internal static Action<string,double> SaveMeasurement;
        static long SaveStamp()=>SaveMeasurement==null?0:System.Diagnostics.Stopwatch.GetTimestamp();
        static void SavedPhase(string phase,long stamp){if(stamp!=0)SaveMeasurement?.Invoke(phase,(System.Diagnostics.Stopwatch.GetTimestamp()-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency);}
        static bool recoveredBackup;
        static bool writeBlocked;
        internal static string OverridePath;
        public static string FilePath=>OverridePath??ProfileStore.FilePath;
        internal static bool WritesBlocked=>writeBlocked;
        public static string Status { get; private set; }
        public static PlayerProfile Load(bool recoverInterrupted=false)
        {
            RaceCheckpointStore.Drain();
            long now = DateTime.UtcNow.Ticks;
            recoveredBackup = false;
            writeBlocked = false;
            foreach (string path in new[] { FilePath, FilePath + ".bak" })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    string json = ProfileSaveCodec.ReadJson(path);
                    var profile = JsonUtility.FromJson<PlayerProfile>(json);
                    if (profile == null) throw new InvalidDataException("Empty profile.");
                    if (profile.saveVersion != MechanicsOwnership.SaveVersion || profile.mechanicsVersion > MechanicsSchema.Version)
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
                    string checkpointWarning;
                    if(RaceCheckpointStore.Merge(FilePath,profile,true,out checkpointWarning))Status="Save and latest race checkpoint loaded.";
                    if(checkpointWarning!=null){Status+=" "+checkpointWarning;Debug.LogWarning(checkpointWarning);}
                    if(recoverInterrupted && !writeBlocked && RunPersistence.RecoverInterrupted(profile)){
                        bool saved=TrySave(profile);Status=saved?"Recovered interrupted run: committed wear/fuel retained; no reward added.":"Interrupted run retained in memory; "+Status;
                    }
                    return profile;
                }
                catch (Exception e) { if(e is NotSupportedException)writeBlocked=true;Debug.LogWarning("Could not read " + path + ": " + e.Message); }
            }
            if (File.Exists(FilePath) || File.Exists(FilePath + ".bak")) writeBlocked = true;
            Status = writeBlocked ? "Save could not be recovered; originals preserved and writing disabled." : "New garage.";
            var fresh = PlayerProfile.NewGame(now); MechanicsOwnership.Ensure(fresh, MechanicsTransactions.Catalog()); if(!writeBlocked&&!TrySave(fresh))Status="New game retained in memory; "+Status; return fresh;
        }
        public static void Save(PlayerProfile profile)
        {
            if (!TrySave(profile)) Debug.LogError(Status);
        }
        public static bool TryCheckpoint(PlayerProfile profile, RunCheckpoint checkpoint, out string error)
        {
            if(writeBlocked){error="Save writes blocked; existing save preserved.";return false;}
            return RaceCheckpointStore.TryQueue(FilePath,profile,checkpoint,out error);
        }
        public static bool TryReset(PlayerProfile current, out PlayerProfile committed)
        {
            RaceCheckpointStore.Drain();
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
                RaceCheckpointStore.Retire(FilePath);
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
                long stamp=SaveStamp();string json=JsonUtility.ToJson(profile, false);SavedPhase("save JSON",stamp);
                stamp=SaveStamp();var bytes=ProfileSaveCodec.Encode(json,profile.saveVersion);SavedPhase("save compression + UTF-8",stamp);
                stamp=SaveStamp();stream.Write(bytes, 0, bytes.Length); stream.Flush(true);SavedPhase("save write + durable flush",stamp);
            }
        }
        static void Publish(string temp, string path)
        {
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        public static bool TrySave(PlayerProfile profile)=>TrySaveCore(profile,false);
        // Only transaction candidates already normalized and validated on this main-thread call stack.
        internal static bool TrySaveValidated(PlayerProfile profile)=>TrySaveCore(profile,true);
        static bool TrySaveCore(PlayerProfile profile,bool validated)
        {
            RaceCheckpointStore.Drain();
            try
            {
                if (writeBlocked) throw new InvalidDataException("Save writes blocked because a newer schema was encountered.");
                long stamp=SaveStamp();if(!validated)MechanicsOwnership.Ensure(profile, profile.catalog ?? MechanicsTransactions.Catalog());SavedPhase(validated?"save validated candidate":"save normalization + validation",stamp);
                string checkpointWarning;
                stamp=SaveStamp();RaceCheckpointStore.Merge(FilePath,profile,false,out checkpointWarning);SavedPhase("save checkpoint merge",stamp);
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + ".tmp";
                // Flush to disk before replacing the previous complete profile.
                WriteDurable(temp, profile);
                stamp=SaveStamp();if (File.Exists(FilePath) && !recoveredBackup) File.Copy(FilePath, FilePath + ".bak", true);
                // Temp is in the same directory, permitting an atomic file rename/replace.
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
                else File.Move(temp, FilePath);
                RaceCheckpointStore.Retire(FilePath);SavedPhase("save backup + atomic publication",stamp);
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
