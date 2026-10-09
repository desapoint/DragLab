using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class SharedAudioVerification
    {
        public static int Run(string root)
        {
            int count = 0; var report = new StringBuilder("Application-wide audio preference / independent file / persisted mute / invalid data recovery.\n");
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException("SHARED AUDIO CHECK FAILED: " + label); count++; report.AppendLine("PASS: " + label); };
            string priorPath = SharedAudioSettings.VerificationPath; float priorListener = AudioListener.volume;
            string folder = Path.Combine(root, "reports/audio-settings-fixtures", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(folder); string path = Path.Combine(folder, "audio-settings.json");
            try
            {
                SharedAudioSettings.VerificationPath = path; SharedAudioSettings.ReloadForVerification();
                check(SharedAudioSettings.MasterVolume == .75f && SharedAudioSettings.DspMasterVolume == .75f && AudioListener.volume == 1, "Fresh preference publishes one 75% DSP gain with an unattenuated listener.");
                SharedAudioSettings.SetMasterVolume(.42f); SharedAudioSettings.ReloadForVerification();
                check(SharedAudioSettings.MasterVolume == .42f && SharedAudioSettings.DspMasterVolume == .42f && AudioListener.volume == 1 && File.ReadAllText(path).Contains("masterVolume"), "Master gain survives a reload from the independent settings file.");
                SharedAudioSettings.SetMasterVolume(0); SharedAudioSettings.ReloadForVerification();
                check(SharedAudioSettings.MasterVolume == 0 && SharedAudioSettings.DspMasterVolume == 0 && AudioListener.volume == 1, "An intentionally saved DSP mute stays muted after reload.");
                SharedAudioSettings.SetMasterVolume(2); SharedAudioSettings.ReloadForVerification();
                check(SharedAudioSettings.MasterVolume == 1, "Out-of-range input cannot amplify the listener beyond full volume.");
                SharedAudioSettings.SetMasterVolume(float.NaN);
                check(SharedAudioSettings.MasterVolume == .75f, "Non-finite input recovers to the shared default.");
                File.WriteAllText(path, "invalid JSON"); SharedAudioSettings.ReloadForVerification();
                check(SharedAudioSettings.MasterVolume == .75f, "A damaged settings file recovers without reading or modifying career saves.");
                File.WriteAllText(path, "{\"version\":99,\"masterVolume\":0.2}"); SharedAudioSettings.ReloadForVerification();
                check(SharedAudioSettings.MasterVolume == .75f, "Unsupported preference versions recover to the known default.");
            }
            finally
            {
                SharedAudioSettings.VerificationPath = priorPath; SharedAudioSettings.ClearVerificationCache(); AudioListener.volume = priorListener;
            }
            File.WriteAllText(Path.Combine(root, "reports/shared-audio-verification.txt"), report.AppendLine("PASS: " + count + " shared audio checks.").ToString());
            return count;
        }
    }
}
