using System;
using System.Collections;
using System.IO;
using System.Linq;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator RacePauseChecks(WindowsKeyboardProbe input)
        {
            game.ReturnToGarage();game.Profile.driving.launch=LaunchMode.Assisted;game.Profile.driving.throttleAssist=false;
            game.StartPractice();yield return new WaitForSecondsRealtime(.2f);
            var race=game.Race;var hud=game.RaceHud;
            game.HandleApplicationInterruption();yield return null;
            Check(game.NativeRacePause&&shell.ModalOpen,"Application interruption still pauses an unfinished race");
            yield return Click(input,"dialogResume");
            var pauseSurface=hud.Root.Q("racePause").Q<RacingSurface>();
            Check(pauseSurface.PaintCount>0&&pauseSurface.contentRect.width>30&&pauseSurface.contentRect.height>20,"Pause icon renders the shared shaded button surface rather than a default unstyled button");
            yield return Click(input,hud.Root.Q("racePause"));
            Check(game.NativeRacePause&&shell.Visible&&shell.ModalOpen&&shell.RacePausePage=="menu"&&hud.Visible,"Visible pause button opens the shared dialog above the dashboard");
            Check(shell.Root.Q("navBar").resolvedStyle.display==DisplayStyle.None&&shell.Root.Q(className:"workspace").resolvedStyle.display==DisplayStyle.None,"Race pause hides the normal-menu navigation, pages and full Settings interface");
            DialogScrollBounds("race pause");yield return Capture("race-pause-menu");
            double time=race.time,rpm=race.player.state.rpm,distance=race.player.state.distance;
            input.Key(0x57,true);yield return Key(input,0x45);yield return Key(input,0x47);input.Key(0x57,false);
            yield return Key(input,0x20);
            Check(game.Race==race&&game.NativeRacePause&&race.time==time&&race.player.state.rpm==rpm&&race.player.state.distance==distance,"Pedals, shifts and G cannot advance or quit the race behind the pause dialog");
            yield return Key(input,0x77);Check(game.NativeRacePause&&InterfaceComparison.IsReplacement("Race")&&shell.ModalOpen,"F8 cannot switch presentation behind an open pause dialog");
            yield return Click(input,"pauseControls");
            Check(shell.RacePausePage=="controls"&&shell.Root.Q<ScrollView>("modalContent").Query<Label>().ToList().Any(l=>l.text=="W / Space"),"Controls are available as a contained read-only page with actual driving bindings");
            DialogScrollBounds("race controls");yield return Capture("race-pause-controls");
            yield return Key(input,0x1B);Check(game.NativeRacePause&&shell.RacePausePage=="menu","Escape from a child page returns to pause without resuming");
            float originalMaster=SharedAudioSettings.MasterVolume,originalOpponent=SharedAudioSettings.OpponentVolume;
            string profile=JsonUtility.ToJson(game.Profile),driver=JsonUtility.ToJson(race.player.driver),units=JsonUtility.ToJson(SharedUnitSettings.Current);
            byte[] database=File.ReadAllBytes(ProfileStore.FilePath);
            yield return Click(input,"pauseSound");
            var content=shell.Root.Q<ScrollView>("modalContent");var master=content.Q<NativeTuningRange>("tuneRange:pauseMaster");
            Check(content.Query<NativeTuningRange>().ToList().Count==2&&content.Q("resetGame")==null&&content.Q("shiftingChoices")==null&&game.ScreenName=="Race","Contained race settings expose only master/opponent sound, without reset or driving-rule changes");
            master.Slider.value=67;
            Check(SharedAudioSettings.MasterVolume==originalMaster&&shell.Root.Q("dialogApply").enabledInHierarchy,"Shared sound slider is a draft until explicit Apply");
            Check(master.Slider.ClassListContains("setting-slider")&&master.Slider.Q(className:"unity-base-slider__dragger").resolvedStyle.width==18,"Pause reuses the shared thin slider and round thumb");
            yield return Capture("race-pause-sound-draft");yield return Click(input,"modalClose");
            Check(game.NativeRacePause&&shell.RacePausePage=="menu"&&SharedAudioSettings.MasterVolume==originalMaster,"Sound X discards the draft and returns to pause");
            yield return Click(input,"pauseSound");master=shell.Root.Q<NativeTuningRange>("tuneRange:pauseMaster");master.Slider.value=58;shell.Root.Q<NativeTuningRange>("tuneRange:pauseOpponent").Slider.value=24;
            using(var locked=new FileStream(DrivingPreferences.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                yield return Click(input,"dialogApply");
                Check(shell.RacePausePage=="sound"&&SharedAudioSettings.MasterVolume==originalMaster&&!string.IsNullOrEmpty(shell.Root.Q<Label>("pauseSoundError").text)&&Math.Abs(master.Slider.value-58)<.01,"Failed preference save keeps sound drafts, applied audio and paused race intact");
            }
            yield return Click(input,"dialogApply");SharedAudioSettings.Reload();
            Check(game.NativeRacePause&&shell.RacePausePage=="menu"&&Math.Abs(SharedAudioSettings.MasterVolume-.58)<.001&&Math.Abs(SharedAudioSettings.OpponentVolume-.24)<.001,"Retry applies and durably reloads the two sound settings without resuming");
            Check(profile==JsonUtility.ToJson(game.Profile)&&driver==JsonUtility.ToJson(race.player.driver)&&units==JsonUtility.ToJson(SharedUnitSettings.Current)&&database.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Sound apply preserves progression, database bytes, applied units and captured simulation rules");
            yield return Click(input,"pauseQuit");Check(game.NativeRacePause&&shell.RacePausePage=="quit","Quit race opens an explicit shared confirmation");yield return Capture("race-pause-quit");yield return Click(input,"dialogCancel");
            Check(game.Race==race&&game.NativeRacePause&&shell.RacePausePage=="menu","Cancelling quit preserves the same paused session");
            foreach(int width in new[]{1024,1920})
            {
                Screen.SetResolution(width,width==1024?768:1080,false);yield return new WaitForSecondsRealtime(.3f);
                Check(shell.Root.Q("modalCard").worldBound.yMin>=0&&shell.Root.Q("modalCard").worldBound.yMax<=shell.Root.worldBound.yMax,"Shared pause card fits the viewport at "+width);DialogScrollBounds("pause "+width);yield return Capture("race-pause-"+width);
            }
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
            yield return Click(input,"modalClose");Check(!game.NativeRacePause&&!shell.ModalOpen&&game.Race==race,"Main pause X resumes the same race");
            yield return Key(input,0x1B);Check(game.NativeRacePause&&shell.RacePausePage=="menu","Escape reopens the shared pause menu");yield return Click(input,"dialogResume");Check(!game.NativeRacePause&&hud.Visible,"Resume closes the dialog without restarting the car");
            Check(!game.ApplyRaceAudio(.2f,.2f,out _),"Quick audio command is rejected outside a paused race");
            yield return Key(input,0x1B);yield return Click(input,"pauseQuit");
            using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                yield return Click(input,"dialogQuitrace");
                Check(game.NativeRacePause&&game.Race==race&&!string.IsNullOrEmpty(shell.Root.Q<Label>("pauseQuitError").text),"Failed durable exit remains in the quit review with the live race retained");
            }
            double finalFuel=race.player.state.fuelLitres;
            var finalParts=race.player.mechanics.parts.ToDictionary(p=>p.instanceId,p=>p.condition.health);
            yield return Click(input,"dialogQuitrace");
            Check(game.ScreenName=="Garage"&&game.Race==null&&!shell.ModalOpen,"Confirmed quit uses the existing durable interrupted-race exit and returns to Garage");
            Check(Math.Abs(game.Profile.SelectedCar.fuel.litres-finalFuel)<1e-7&&finalParts.All(pair=>Math.Abs(game.Profile.parts.First(p=>p.instanceId==pair.Key).condition.health-pair.Value)<1e-7),"Quit retains exact live fuel and component health instead of refilling or repairing");
            var loaded=ProfileStore.Load();Check(Math.Abs(loaded.SelectedCar.fuel.litres-finalFuel)<1e-7,"Retained race fuel survives SQLite reload after quitting");
        }
#endif
    }
}
