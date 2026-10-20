using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace DragLab.Infrastructure
{
    // Application preference, independent of vehicle tunes, scenarios and career saves.
    // Procedural sources consume one published output gain, after synthesis.
    public static class SharedAudioSettings
    {
        [Serializable] sealed class Settings { public int version = 1; public float masterVolume = DefaultVolume; public float opponentVolume = 1; public float compressorNoise=1; }
        public const float DefaultVolume = .75f;
        static bool loaded, dirty;
        static Settings settings;
        static float publishedMaster = DefaultVolume;
        static float publishedCompressorNoise=1;
        // DSP must never trigger lazy loading, file I/O or a Unity API call.
        public static float DspMasterVolume => Volatile.Read(ref publishedMaster);
        public static float DspCompressorNoise=>Volatile.Read(ref publishedCompressorNoise);
        public static string Status { get; private set; }
#if UNITY_EDITOR
        public static string VerificationPath;
#endif
        public static string FilePath
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(VerificationPath)) return VerificationPath;
#endif
                var args = Environment.GetCommandLineArgs();
                foreach (string flag in new[] { "--draglab-verify", "--draglab-bench-verify", "--draglab-driving-verify" })
                {
                    int index = Array.IndexOf(args, flag);
                    if (Debug.isDebugBuild && index >= 0 && index + 1 < args.Length)
                        return Path.Combine(Path.GetFullPath(args[index + 1]), "audio-settings.json");
                }
                return Path.Combine(Application.persistentDataPath, "audio-settings.json");
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { loaded = dirty = false; settings = null; Status = null; Volatile.Write(ref publishedMaster, DefaultVolume); Volatile.Write(ref publishedCompressorNoise,1); }
        public static float MasterVolume { get { EnsureLoaded(); return settings.masterVolume; } }
        public static float OpponentVolume { get { EnsureLoaded(); return settings.opponentVolume; } }
        public static float CompressorNoise {get{EnsureLoaded();return settings.compressorNoise;}}
        public static float OpponentGain(double separationMetres) => (float)(.65 / (1 + Math.Abs(separationMetres) / 35));
        public static void SetOpponentVolume(float value, bool save = true)
        {
            EnsureLoaded(); value = float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Clamp01(value);
            if (settings.opponentVolume != value) { settings.opponentVolume = value; dirty = true; }
            if (save) Save();
        }
        public static void EnsureLoaded()
        {
            if (loaded) { AudioListener.volume = 1; return; }
            settings = new Settings(); loaded = true; dirty = false;
            Status = "Audio settings use the shared 75% default.";
            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath, System.Text.Encoding.UTF8);
                    var saved = JsonUtility.FromJson<Settings>(json);
                    if (saved == null || saved.version != 1 || float.IsNaN(saved.masterVolume) || float.IsInfinity(saved.masterVolume))
                        throw new InvalidDataException("Invalid audio settings.");
                    settings.masterVolume = Mathf.Clamp01(saved.masterVolume); Status = "Shared audio settings loaded.";
                    settings.opponentVolume = !json.Contains("opponentVolume") ? 1 : float.IsNaN(saved.opponentVolume) || float.IsInfinity(saved.opponentVolume) ? 1 : Mathf.Clamp01(saved.opponentVolume);
                    settings.compressorNoise=!json.Contains("compressorNoise")?1:float.IsNaN(saved.compressorNoise)||float.IsInfinity(saved.compressorNoise)?1:Mathf.Clamp01(saved.compressorNoise);
                }
                catch (Exception) { Status = "Unreadable audio settings; using the shared default."; }
            }
            Volatile.Write(ref publishedMaster, settings.masterVolume);
            Volatile.Write(ref publishedCompressorNoise,settings.compressorNoise);
            AudioListener.volume = 1;
        }
        public static void SetMasterVolume(float volume, bool save = true)
        {
            EnsureLoaded();
            if (float.IsNaN(volume) || float.IsInfinity(volume)) volume = DefaultVolume;
            volume = Mathf.Clamp01(volume);
            if (settings.masterVolume != volume) { settings.masterVolume = volume; dirty = true; }
            Volatile.Write(ref publishedMaster, volume);
            AudioListener.volume = 1;
            if (save) Save();
        }
        public static void Reload() { ResetSession(); EnsureLoaded(); }
        public static void Save()
        {
            if (!loaded || !dirty) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(settings, true),new System.Text.UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
                else File.Move(temp, FilePath);
                dirty = false; Status = "Shared audio settings saved.";
            }
            catch (Exception e) { Status = "Audio settings could not be saved: " + e.Message; }
        }
#if UNITY_EDITOR
        public static void ReloadForVerification() { Reload(); }
        public static void ClearVerificationCache() { ResetSession(); }
#endif
    }
}
