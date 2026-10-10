using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator RevealClick(WindowsKeyboardProbe input,string id)
        {
            var target=shell.Root.Q(id); shell.Root.Q<ScrollView>("settingsScroll").ScrollTo(target);
            yield return new WaitForSecondsRealtime(.15f); yield return Click(input,id);
        }
        IEnumerator SettingsChecks(WindowsKeyboardProbe input)
        {
            var before=JsonUtility.ToJson(game.Profile); var applied=game.settings.Copy();
            var originalUnits=JsonUtility.ToJson(SharedUnitSettings.Current);
            float master=SharedAudioSettings.MasterVolume,opponent=SharedAudioSettings.OpponentVolume;
            var root=shell.Root;
            foreach(string id in new[]{"Difficulty","Sound","Units","Controls","General"})
            {
                yield return Click(input,"tab"+id); shell.Root.Q<ScrollView>("settingsScroll").scrollOffset=Vector2.zero;
                yield return new WaitForSecondsRealtime(.15f); yield return Capture("populated-"+id);
                Check(root.Q("page"+id).resolvedStyle.display!=DisplayStyle.None,"Populated category "+id+" visible");
                Check(root.Q("cancelSettings").worldBound.yMax<Screen.height-30 && root.Q("applySettings").worldBound.yMax<Screen.height-30,"Apply/Cancel remain outside scrolling panel: "+id);
            }
            yield return Click(input,"tabDifficulty");
            foreach(var field in typeof(DriverSettings).GetFields())
                if(field.Name!="reactionSeconds" && field.Name!="soundVolume" && field.Name!="wearMultiplier" && field.Name!="optimalThermals")
                    Check(root.Q(field.Name+"Choices")!=null,"Existing driving preference mapped: "+field.Name);
            Check(root.Q("heatManagementChoices")!=null,"Optimal thermals mapped as Heat management");
            yield return RevealClick(input,"shifting2"); yield return RevealClick(input,"launch2"); yield return RevealClick(input,"opponentDifficulty2");
            yield return RevealClick(input,"simulationDifficulty0");
            Check(shell.DraftDriver.wearMode==WearMode.None && shell.DraftDriver.fuelUsage==FuelUsageMode.None,"Arcade preset edits shared draft");
            Check(!root.Q("raceTemperaturesChoices").enabledInHierarchy,"Heat management off disables race-start temperature only");
            var remembered=shell.DraftDriver.raceTemperatures;
            yield return RevealClick(input,"heatManagement");
            Check(!shell.DraftDriver.optimalThermals && root.Q("raceTemperaturesChoices").enabledInHierarchy && shell.DraftDriver.raceTemperatures==remembered,"Re-enabling heat preserves remembered race-start preference");
            yield return RevealClick(input,"wearMode1"); yield return RevealClick(input,"fuelUsage1");
            Check(shell.DraftDriver.WearRate==.25 && shell.DraftDriver.FuelUsageRate==.25,"Reduced wear/fuel retain approved 25% rates");
            yield return Capture("difficulty-lower-options");
            yield return Click(input,"tabSound");
            var slider=root.Q<Slider>("masterVolume");
            Check(slider.Q<SliderFill>().worldBound.width>100,"Slider fill has visible track width");
            var numericInput=root.Q<FloatField>("masterVolumeValue").Q(className:"unity-base-field__input");
            Check(numericInput!=null && numericInput.resolvedStyle.backgroundColor.r<.3f,"Numeric entry uses readable dark themed background");
            var tracker=slider.Q(className:"unity-base-slider__tracker").worldBound;
            input.MouseButtonAt((int)Mathf.Lerp(tracker.xMin,tracker.xMax,.32f),(int)tracker.center.y,true); yield return new WaitForSecondsRealtime(.1f);
            input.MouseButtonAt((int)Mathf.Lerp(tracker.xMin,tracker.xMax,.32f),(int)tracker.center.y,false);yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Abs(slider.value-.32f)<.06f && SharedAudioSettings.MasterVolume==master && SharedAudioSettings.DspMasterVolume==master,"Real volume slider edits draft without publishing audio gain");
            root.Q<FloatField>("opponentVolumeValue").value=41;
            Check(root.Q<Slider>("opponentVolume").value==.41f && SharedAudioSettings.OpponentVolume==opponent,"Numeric input and slider stay synchronized without live publication");
            yield return Click(input,"masterVolumeValue"); input.Key(0x11,true);yield return Key(input,0x41);input.Key(0x11,false);
            yield return Key(input,0x33);yield return Key(input,0x32);yield return Key(input,0x0d);
            Check(Mathf.Abs(root.Q<FloatField>("masterVolumeValue").value-32)<.01f && SharedAudioSettings.MasterVolume==master,"Real keyboard numeric entry edits only sound draft");
            yield return Key(input,0x77);Check(shell.Visible,"Native numeric text focus blocks F8");
            yield return Click(input,"tabUnits");yield return Click(input,"speed1");yield return Click(input,"pressure1");yield return Click(input,"torque1");
            Check(JsonUtility.ToJson(game.Profile)==before && JsonUtility.ToJson(SharedUnitSettings.Current)==originalUnits,"Driving/units/category edits leave applied profile untouched");
            yield return Key(input,0x77);Check(!shell.Visible,"F8 keeps dirty draft while showing legacy Settings");
            yield return Key(input,0x77);Check(shell.Visible && shell.SettingsDirty,"F8 restores same dirty Settings draft");
            yield return Click(input,"applySettings"); Check(shell.ModalOpen,"Apply opens actual-change confirmation");
            Check(root.Q<ScrollView>("modalContent").Q<Label>().text.Contains("Master") && root.Q<ScrollView>("modalContent").Q<Label>().text.Contains("Mechanical wear"),"Confirmation names actual pending changes");
            yield return Capture("settings-apply-dialog");yield return Key(input,0x77);Check(shell.Visible && shell.ModalOpen,"Confirmation blocks F8");
            yield return Click(input,"modalClose");Check(!shell.ModalOpen && shell.SettingsDirty && JsonUtility.ToJson(game.Profile)==before,"X dismisses only dialog and keeps draft");
            yield return Click(input,"navGarage");Check(shell.ModalOpen && game.ScreenName=="Settings","Dirty navigation asks Apply/Discard/Stay");
            yield return Click(input,"dialogStay");Check(game.ScreenName=="Settings" && shell.SettingsDirty,"Stay retains draft");
            yield return Click(input,"applySettings"); yield return Click(input,"dialogApply");
            Check(!shell.ModalOpen && !shell.SettingsDirty && game.settings.shifting==ShiftMode.ManualClutch && game.settings.launch==LaunchMode.Manual,"Confirmed Apply publishes whole driving draft");
            Check(game.settings.wearMode==WearMode.Partial && game.settings.fuelUsage==FuelUsageMode.Reduced && game.Profile.difficulty==2,"Reduced rules and opponent difficulty persist together");
            Check(Mathf.Abs(SharedAudioSettings.MasterVolume-.32f)<.06f && SharedAudioSettings.OpponentVolume==.41f,"Confirmed Apply publishes shared audio gains");
            SharedAudioSettings.Reload();SharedUnitSettings.Reload();var loaded=ProfileStore.Load();
            Check(SharedAudioSettings.OpponentVolume==.41f && SharedUnitSettings.Current.torque==TorqueUnit.PoundFeet && SharedUnitSettings.Current.pressure==PressureUnit.Psi && SharedUnitSettings.Current.speed==SpeedUnit.MilesPerHour,"Audio and all units survive reload");
            Check(loaded.driving.shifting==ShiftMode.ManualClutch && loaded.driving.wearMode==WearMode.Partial && loaded.difficulty==2,"Applied driving settings survive profile reload");
            string current=JsonUtility.ToJson(game.Profile);
            var failedDriver=game.settings.Copy();failedDriver.clutchReleaseSeconds=.39f;
            string profileDisk=File.ReadAllText(ProfileStore.FilePath),backupDisk=File.Exists(ProfileStore.FilePath+".bak")?File.ReadAllText(ProfileStore.FilePath+".bak"):null,audioDisk=File.ReadAllText(SharedAudioSettings.FilePath),unitDisk=File.ReadAllText(SharedUnitSettings.FilePath);
            float liveGain=SharedAudioSettings.MasterVolume;
            using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                Check(!game.ApplyNativeSettings(failedDriver,0,new UnitPreferences(),.91f,.92f,out var error) && error!=null,"Locked profile rejects settings commit");
                Check(JsonUtility.ToJson(game.Profile)==current && SharedAudioSettings.MasterVolume==liveGain && SharedAudioSettings.OpponentVolume==.41f,"Failed commit never publishes new profile or audio cache");
                Check(File.Exists(Path.Combine(folder,"settings-commit.json")),"Interrupted rollback retains durable recovery journal");
            }
            Check(PreferenceCommit.Recover(out var recoveryError) && recoveryError==null,"Incomplete settings write recovers after lock released");
            Check(File.ReadAllText(ProfileStore.FilePath)==profileDisk && (File.Exists(ProfileStore.FilePath+".bak")?File.ReadAllText(ProfileStore.FilePath+".bak"):null)==backupDisk && File.ReadAllText(SharedAudioSettings.FilePath)==audioDisk && File.ReadAllText(SharedUnitSettings.FilePath)==unitDisk,"Recovery restores all three files and profile backup exactly");
            Check(!File.Exists(Path.Combine(folder,"settings-commit.json")),"Successful recovery retires journal");
            yield return Click(input,"cancelSettings");Check(game.ScreenName=="Garage","Clean Cancel returns to prior Garage");
            yield return Click(input,"navBusiness");yield return Click(input,"settingsButton");yield return Click(input,"tabSound");
            root.Q<FloatField>("masterVolumeValue").value=91;yield return Click(input,"cancelSettings");
            Check(game.ScreenName=="Business" && SharedAudioSettings.MasterVolume==liveGain,"Dirty Cancel discards draft and returns to actual previous menu");
            yield return Click(input,"settingsButton");yield return Click(input,"tabSound");
            Check(root.Q<FloatField>("masterVolumeValue").value<40,"Reopened Settings starts from applied preferences");
            root.Q<FloatField>("masterVolumeValue").value=93;
            SharedUnitSettings.Set(SpeedUnit.MetresPerSecond,PressureUnit.Bar,TorqueUnit.NewtonMetres);
            yield return new WaitForSecondsRealtime(.4f);
            Check(root.Q<FloatField>("masterVolumeValue").value==93,"Definitive external unit change preserves unrelated local sound draft");
            yield return Click(input,"tabUnits");Check(root.Q("speed2").ClassListContains("selected"),"Definitive external units reconcile into native draft");
            SharedAudioSettings.SetMasterVolume(.58f);yield return new WaitForSecondsRealtime(.4f);
            Check(!root.Q("applySettings").enabledInHierarchy && root.Q<Label>("settingsStatus").text.Contains("Revert"),"Conflicting definitive preference edit blocks stale Apply");
            yield return Click(input,"revertSettings");yield return Click(input,"tabSound");
            Check(Mathf.Abs(root.Q<FloatField>("masterVolumeValue").value-58)<.01f && !shell.SettingsDirty,"Revert loads latest applied settings");
            root.Q<FloatField>("masterVolumeValue").value=99;
            yield return Click(input,"tabGeneral");yield return Click(input,"resetGame");Check(shell.ModalOpen,"Reset opens separate destructive dialog");
            yield return Key(input,0x1b);Check(!shell.ModalOpen && JsonUtility.ToJson(game.Profile)==current,"Escape cancels reset without changing progression");
            yield return Click(input,"resetGame");yield return Click(input,"dialogResetgame");
            Check(game.Profile.cash==1200 && game.Profile.reputation==0 && game.settings.shifting==ShiftMode.ManualClutch,"Reset clears progression and retains applied driving preferences");
            Check(SharedAudioSettings.MasterVolume==.58f && shell.SettingsDirty,"Reset retains applied sound and does not publish unsaved sound draft");
            yield return Click(input,"cancelSettings");Check(game.ScreenName=="Business" && SharedAudioSettings.MasterVolume==.58f,"Cancel after reset discards only Settings draft");
            // Clicking must neither restart nor interrupt a running hover sweep.
            var gear=root.Q("settingsButton");var surface=gear.Q<RacingSurface>();
            input.MoveMouseAt(800,500);yield return new WaitForSecondsRealtime(.7f);
            input.MoveMouseAt((int)gear.worldBound.center.x,(int)gear.worldBound.center.y);yield return new WaitForSecondsRealtime(.12f);
            float progress=surface.ShineProgress;
            input.MouseButtonAt((int)gear.worldBound.center.x,(int)gear.worldBound.center.y,true);yield return new WaitForSecondsRealtime(.04f);
            input.MouseButtonAt((int)gear.worldBound.center.x,(int)gear.worldBound.center.y,false);yield return new WaitForSecondsRealtime(.06f);
            Check(surface.ShineActive && surface.ShineProgress>progress,"Click and selected-state change continue original hover sweep");
            input.MoveMouseAt(800,500);yield return new WaitForSecondsRealtime(.12f);
            Check(surface.ShineActive,"Pointer leave does not interrupt full hover sweep");
            yield return new WaitForSecondsRealtime(.6f);Check(!surface.ShineActive,"Sweep ends once after original hover interval");
            yield return Click(input,"tabDifficulty");shell.Root.Q<ScrollView>("settingsScroll").scrollOffset=Vector2.zero;
            yield return Capture("settings-final-difficulty");
            Screen.SetResolution(1024,768,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.7f);yield return Capture("settings-final-compact");
            Check(root.Q("applySettings").worldBound.xMax<=Screen.width && root.Q("cancelSettings").worldBound.yMax<Screen.height-30,"Populated Settings actions remain reachable at compact resolution");
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.7f);
            var target=new RenderTexture(3840,2160,24);target.Create();shell.Panel.targetTexture=target;
            yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
            Check(shell.Panel.scale==2 && root.Q("applySettings").worldBound.xMax<=1920 && root.Q("settingsBody").worldBound.yMax<root.Q("settingsActions").worldBound.yMin,"Populated 4K layout retains panel/action separation with shared 2x scale");
            var prior=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(3840,2160,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"settings-populated-4k.png"),image.EncodeToPNG());
            RenderTexture.active=prior;shell.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(image);
        }
#endif
    }
}
