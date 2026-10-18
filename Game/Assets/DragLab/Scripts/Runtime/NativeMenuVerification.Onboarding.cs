using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator OnboardingChecks(WindowsKeyboardProbe input)
        {
            if(!game.Profile.firstPurchasePending||DrivingPreferences.OnboardingComplete){Check(!shell.ModalOpen,"Completed onboarding does not reopen on relaunch or an existing garage");yield break;}
            Check(shell.ModalOpen&&shell.Root.Q<Label>("modalTitle").text=="Welcome to DragLab","First start opens preset onboarding before shopping");
            Check(game.settings.shifting==ShiftMode.Sequential&&game.settings.simulationDifficulty==SimulationDifficulty.Simulation&&game.settings.guaranteedFittingParts==5,"Fresh defaults are Shift myself / Simulation / Normal market");
            string profile=JsonUtility.ToJson(game.Profile),stock=JsonUtility.ToJson(game.Profile.vehicleStock),preferences=File.ReadAllText(DrivingPreferences.FilePath);
            byte[] database=File.ReadAllBytes(ProfileStore.FilePath);
            yield return Capture("onboarding-presets");DialogScrollBounds("onboarding presets");
            yield return Click(input,"onboardingDriving:Automatic");yield return Click(input,"onboardingSimulation:Balanced");yield return Click(input,"onboardingNext");
            Check(shell.Root.Q<Label>("modalTitle").text=="Market difficulty"&&shell.Root.Q<Button>("onboardingMarket:Normal · 5").ClassListContains("selected"),"Presets precede independent Normal market selection");
            Check(JsonUtility.ToJson(game.Profile)==profile&&File.ReadAllText(DrivingPreferences.FilePath)==preferences,"Next keeps all settings as unsaved drafts");
            yield return Click(input,"onboardingMarket:Hard · 0");yield return Click(input,"onboardingBack");
            Check(shell.Root.Q<Button>("onboardingDriving:Automatic").ClassListContains("selected")&&shell.Root.Q<Button>("onboardingSimulation:Balanced").ClassListContains("selected"),"Back retains both preset drafts");
            yield return Click(input,"onboardingDriving:Shift myself");yield return Click(input,"onboardingSimulation:Simulation");yield return Click(input,"onboardingNext");
            Check(shell.Root.Q<Button>("onboardingMarket:Hard · 0").ClassListContains("selected"),"Returning to market retains its independent draft");
            yield return Click(input,"onboardingMarket:Normal · 5");yield return Capture("onboarding-market");DialogScrollBounds("onboarding market");
            using(var locked=new FileStream(DrivingPreferences.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)){
                yield return Click(input,"onboardingApply");
                Check(shell.ModalOpen&&!string.IsNullOrEmpty(shell.Root.Q<Label>("onboardingError").text)&&!DrivingPreferences.OnboardingComplete,"Failed preference write keeps onboarding open and retryable");
                Check(JsonUtility.ToJson(game.Profile)==profile&&File.ReadAllText(DrivingPreferences.FilePath)==preferences,"Failed onboarding preserves original state and preferences");
            }
            yield return Click(input,"onboardingApply");
            Check(!shell.ModalOpen&&DrivingPreferences.OnboardingComplete,"Explicit confirmation persists completion and returns to starter selection");
            Check(game.settings.simulationDifficulty==SimulationDifficulty.Simulation&&game.settings.shifting==ShiftMode.Sequential&&game.settings.guaranteedFittingParts==5,"Normal market applies after Simulation rather than becoming Hard");
            Check(database.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath))&&stock==JsonUtility.ToJson(game.Profile.vehicleStock)&&profile==JsonUtility.ToJson(game.Profile),"Onboarding changes preferences only; exact starter stock, money and progression remain intact");
            var loaded=ProfileStore.Load();Check(loaded.driving.simulationDifficulty==SimulationDifficulty.Simulation&&loaded.driving.guaranteedFittingParts==5&&loaded.driving.shifting==ShiftMode.Sequential,"Confirmed presets reload independently from SQLite progression");
            yield return Capture("onboarding-applied-starters");
        }
#endif
    }
}
