using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Infrastructure
{
    // A write-ahead undo record makes the three existing preference authorities one
    // recoverable operation. No live cache is published before its commit point.
    internal static class PreferenceCommit
    {
        [Serializable] sealed class Entry { public bool exists; public string text; }
        [Serializable] sealed class Journal { public bool committed; public Entry[] entries; }
        [Serializable] sealed class Audio { public int version = 1; public float masterVolume, opponentVolume, compressorNoise; }
        static string JournalPath => Path.Combine(Path.GetDirectoryName(ProfileStore.FilePath), "settings-commit.json");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RecoverBeforeScenes() { if (!Recover(out var error)) Debug.LogError(error); }
        static string[] Paths => new[] { DrivingPreferences.FilePath, SharedAudioSettings.FilePath, SharedUnitSettings.FilePath };
        static void Atomic(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".preferences.tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { byte[] data = Encoding.UTF8.GetBytes(text); stream.Write(data, 0, data.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        static void Undo(Journal journal)
        {
            if (journal.entries == null || journal.entries.Length != Paths.Length&&journal.entries.Length!=4) throw new InvalidDataException("Invalid settings recovery record.");
            var paths = journal.entries.Length==4?new[]{ProfileStore.LegacyFilePath,ProfileStore.LegacyFilePath+".bak",SharedAudioSettings.FilePath,SharedUnitSettings.FilePath}:Paths;
            for (int i = 0; i < paths.Length; i++)
            { if (journal.entries[i].exists) Atomic(paths[i], journal.entries[i].text); else if (File.Exists(paths[i])) File.Delete(paths[i]); }
        }
        internal static bool Recover(out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(JournalPath)) return true;
                var journal = JsonUtility.FromJson<Journal>(File.ReadAllText(JournalPath, Encoding.UTF8));
                if (journal == null) throw new InvalidDataException("Invalid settings recovery record.");
                if (!journal.committed) Undo(journal);
                File.Delete(JournalPath); DrivingPreferences.ReloadControls(); return true;
            }
            catch (Exception e) { error = "Settings recovery failed: " + e.Message; return false; }
        }
        internal static bool TryApply(PlayerProfile candidate, UnitPreferences units, float master, float opponent, out string error, bool? reverseTouchShifting=null,bool? onboardingComplete=null,float? compressorNoise=null)
        {
            error = null;
            if (!Recover(out error)) return false;
            RaceCheckpointStore.Drain();
            var paths = Paths; var journal = new Journal { entries = new Entry[paths.Length] };
            bool prepared = false;string appliedDriving=null;
            float noise=compressorNoise??SharedAudioSettings.CompressorNoise;
            try
            {
                if (float.IsNaN(master) || float.IsInfinity(master) || float.IsNaN(opponent) || float.IsInfinity(opponent) || master < 0 || master > 1 || opponent < 0 || opponent > 1)
                    throw new InvalidDataException("Volume must be between 0 and 100%.");
                if(float.IsNaN(noise)||float.IsInfinity(noise)||noise<0||noise>1)throw new InvalidDataException("Compressor noise must be between 0 and 100%.");
                for (int i = 0; i < paths.Length; i++) journal.entries[i] = new Entry { exists = File.Exists(paths[i]), text = File.Exists(paths[i]) ? File.ReadAllText(paths[i], Encoding.UTF8) : null };
                Atomic(JournalPath, JsonUtility.ToJson(journal)); prepared = true;
                appliedDriving=DrivingPreferences.Encode(candidate.driving,candidate.difficulty,reverseTouchShifting,onboardingComplete);
                Atomic(DrivingPreferences.FilePath,appliedDriving);
                Atomic(SharedAudioSettings.FilePath, JsonUtility.ToJson(new Audio { masterVolume = master, opponentVolume = opponent, compressorNoise=noise }, true));
                Atomic(SharedUnitSettings.FilePath, JsonUtility.ToJson(units, true));
                // Settings never serialize or commit progression/database state.
                journal.committed = true; Atomic(JournalPath, JsonUtility.ToJson(journal));
            }
            catch (Exception e)
            {
                error = e.Message;
                if (prepared) try { Undo(journal); File.Delete(JournalPath); } catch (Exception rollback) { error += " / Recovery required: " + rollback.Message; }
                return false;
            }
            // Once the durable marker is written, cleanup failure cannot turn a
            // successfully committed change into an apparent failure.
            try { File.Delete(JournalPath); } catch (Exception) { }
            DrivingPreferences.PublishControls(appliedDriving); SharedAudioSettings.Reload(); SharedUnitSettings.Reload(); return true;
        }
    }
}
