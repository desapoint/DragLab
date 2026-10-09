using System;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;
using DragLab.Presentation;
using DragLab.Runtime;

namespace DragLab.Editor
{
    // Synchronous batch check of scene wiring and application boundaries.
    // This is not a substitute for Play Mode/UI or real-device testing.
    public static class SceneVerification
    {
        static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string fixtures = Path.Combine(root, "reports/scene-fixtures", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            ProfileStore.VerificationPath = Path.Combine(fixtures, "profile-v1.json");
            try
            {
                EditorSceneManager.OpenScene("Assets/DragLab/Scenes/FirstSlice.unity");
                var game = UnityEngine.Object.FindFirstObjectByType<PrototypeGame>();
                Check(game != null, "Scene contains PrototypeGame.");
                Invoke(game, "Start");
                Check(game.vehicles.Length >= 8 && game.career.Length == 4, "Scene loads the expanded vehicle catalog and four existing events.");
                var profile = Field<PlayerProfile>(game, "profile");
                int startCash = profile.cash;
                game.StartRace(game.career[0]);
                var race = Field<RaceSimulation>(game, "race");
                int guard = 15000;
                while (race.phase != RacePhase.Finished && --guard > 0) race.Step(new DriverInput());
                Debug.Log("SCENE DUEL: player reaction " + race.player.state.reaction + " ET " + race.player.state.elapsed +
                    "; opponent reaction " + race.opponent.state.reaction + " ET " + race.opponent.state.elapsed);
                Check(guard > 0 && race.playerWon, "Starter can complete and win the first event.");
                // Direct domain stepping bypasses Update's tick counter. Publish the matching
                // boundary tick, then inspect the committed profile rather than its old draft.
                SetField(game, "liveRunTick", (long)(15000 - guard));
                Invoke(game, "FinishRace");
                profile = Field<PlayerProfile>(game, "profile");
                Check(profile.cash == startCash + game.career[0].cashWin && profile.reputation == game.career[0].reputationWin,
                    "Win grants the configured cash and first-win reputation.");
                Invoke(game, "FinishRace");
                Check(profile.cash == startCash + game.career[0].cashWin && profile.SelectedCar.completedRuns == 1, "Repeated result settlement cannot duplicate cash or records.");
                var loaded = ProfileStore.Load();
                Check(loaded.cash == profile.cash && loaded.careerWins.Contains(game.career[0].stableId), "Result persists through save/reload.");
                Check(File.Exists(ProfileStore.FilePath + ".bak"), "Previous save backup exists.");
                File.WriteAllText(ProfileStore.FilePath, "corrupt fixture data");
                var recovered = ProfileStore.Load();
                Check(recovered.cash == startCash && ProfileStore.Status.Contains("Recovered"), "Invalid main save recovers its previous backup.");
                ProfileStore.Save(recovered);
                File.WriteAllText(ProfileStore.FilePath, "corrupt fixture again");
                Check(ProfileStore.Load().cash == startCash && ProfileStore.Status.Contains("Recovered"), "Saving after recovery does not overwrite the valid backup with corruption.");
                var view = Field<LayeredCarView>(game, "playerView");
                Check(view.transform.childCount == 12, "Runtime car assembles all twelve manifest layers.");
                File.WriteAllText(ProfileStore.FilePath, "{\"saveVersion\":1,\"cash\":1234,\"driving\":{\"shifting\":1,\"launch\":1}} ");
                var legacy = ProfileStore.Load();
                Check(legacy.SelectedCar.lastPass == null, "Earlier saves have no invented last-pass record.");
                Check(legacy.cash == 1234 && legacy.driving.shifting == ShiftMode.Sequential && legacy.driving.clutchReleaseSeconds == .18f && legacy.driving.soundVolume == .35f && legacy.driving.shutdownBrakeAssist,
                    "Earlier version-1 driving saves receive the new pedal/audio defaults without losing cash or modes.");
                legacy.driving.soundVolume = 0; legacy.driving.clutchReleaseSeconds = .4f; legacy.driving.shutdownBrakeAssist = false; ProfileStore.Save(legacy);
                Check(ProfileStore.Load().driving.soundVolume == 0 && ProfileStore.Load().driving.clutchReleaseSeconds == .4f && !ProfileStore.Load().driving.shutdownBrakeAssist, "Explicit muted audio, manual brakes and pedal preferences survive reload.");
                int practiceCash = profile.cash, practiceRep = profile.reputation, practiceRuns = profile.SelectedCar.completedRuns;
                game.StartPractice(); race = Field<RaceSimulation>(game, "race"); guard = 15000;
                while (race.phase != RacePhase.Finished && --guard > 0) race.Step(new DriverInput());
                SetField(game, "liveRunTick", (long)(15000 - guard));
                Invoke(game, "FinishRace"); Invoke(game, "FinishRace");
                profile = Field<PlayerProfile>(game, "profile");
                Check(guard > 0 && profile.cash == practiceCash && profile.reputation == practiceRep && profile.SelectedCar.completedRuns == practiceRuns + 1,
                    "Practice records exactly one valid pass without paying cash/reputation.");
                var savedPass = ProfileStore.Load().SelectedCar.lastPass;
                Check(savedPass != null && Math.Abs(savedPass.elapsedSeconds - race.player.state.elapsed) < 1e-10 && savedPass.tune != profile.SelectedCar.tune &&
                    savedPass.driving.launch == race.player.driver.launch, "Last-pass timing, tune and assists persist independently.");
                game.ReturnToGarage(); Check(Field<string>(game, "screen") == "Garage", "Completed practice returns to garage without restarting.");
                string report = "PASS: scene initialization, eight-car/four-event wiring, first race, reward/save roundtrip, backup recovery and twelve-layer runtime car assembly.\n" +
                    "Practice settles once with no payout, last-pass snapshots persist, legacy saves have no invented pass, and return to garage succeeds.\n" +
                    "Editor: " + Application.unityVersion + "\nFixture: " + fixtures + "\nNo actual player save was accessed.\n" +
                    "Scope: synchronous scene integration; UI input, rendering, audio and device behavior still require Play Mode/device QA.\n";
                File.WriteAllText(Path.Combine(root, "reports/unity-scene-verification.txt"), report);
                Debug.Log("DRAGLAB SCENE VERIFICATION PASS");
            }
            finally { ProfileStore.VerificationPath = null; }
        }
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("SCENE CHECK FAILED: " + message); }
        static T Field<T>(PrototypeGame game, string field) { return (T)typeof(PrototypeGame).GetField(field, PrivateInstance).GetValue(game); }
        static void SetField(PrototypeGame game, string field, object value) { typeof(PrototypeGame).GetField(field, PrivateInstance).SetValue(game, value); }
        static void Invoke(PrototypeGame game, string method) { typeof(PrototypeGame).GetMethod(method, PrivateInstance).Invoke(game, null); }
    }
}
