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
        IEnumerator ControlStyleChecks(WindowsKeyboardProbe input)
        {
            var root=shell.Root;var scroll=root.Q<ScrollView>("settingsScroll");scroll.scrollOffset=Vector2.zero;
            yield return new WaitForSecondsRealtime(.15f);
            int rows=0;root.Q("pageDifficulty").Query(className:"setting-row").ForEach(row=>
            {if(row.worldBound.yMin>=scroll.worldBound.yMin && row.worldBound.yMax<=scroll.worldBound.yMax)rows++;});
            Check(rows>=9,"Compact Settings exposes at least nine full driving rows at 1280x720: "+rows);
            Check(root.Q("cancelSettings").worldBound.height<=35 && root.Q("applySettings").worldBound.height<=35,"Page actions use compact matching heights");
            var first=root.Q("shifting0");var middle=root.Q("shifting1");var last=root.Q("shifting2");
            Check(first.resolvedStyle.borderTopLeftRadius>0 && first.resolvedStyle.borderTopRightRadius==0 && middle.resolvedStyle.borderTopLeftRadius==0 && middle.resolvedStyle.borderTopRightRadius==0 && last.resolvedStyle.borderTopLeftRadius==0 && last.resolvedStyle.borderTopRightRadius>0,"Choice group rounds only its two outer ends");
            Check(Mathf.Abs(first.worldBound.xMax-middle.worldBound.xMin)<1 && Mathf.Abs(middle.worldBound.xMax-last.worldBound.xMin)<1,"Joined choices have no gap between segments");
            var bar=scroll.verticalScroller;
            Check(bar.worldBound.width<=9 && bar.lowButton.resolvedStyle.display==DisplayStyle.None && bar.highButton.resolvedStyle.display==DisplayStyle.None,"Scrollbar has a slim eight-point column and no stock arrow buttons: "+bar.worldBound.width);
            Check(bar.slider.Q(className:"unity-base-slider__dragger").worldBound.width<=9,"Scrollbar thumb fits its slim column without overflowing");
            yield return new WaitForEndOfFrame();var pixels=ScreenCapture.CaptureScreenshotAsTexture();var panel=root.Q("settingsBody").worldBound;
            float CornerRed(bool lower)
            {
                float best=0;for(int x=0;x<16;x++)for(int y=0;y<16;y++)
                {var c=pixels.GetPixel((int)(lower?panel.xMax-1-x:panel.xMin+x),Screen.height-1-(int)(lower?panel.yMax-1-y:panel.yMin+y));best=Mathf.Max(best,c.r-c.g);}
                return best;
            }
            Check(CornerRed(false)>.3f && CornerRed(true)>.3f,"Both rounded panel corners visibly render red accents");
            float TopRed(float offset)
            {float best=0;for(int y=0;y<4;y++){var c=pixels.GetPixel((int)(panel.xMin+offset),Screen.height-1-(int)(panel.yMin+y));best=Mathf.Max(best,c.r-c.g);}return best;}
            Check(TopRed(24)>.15f && TopRed(120)<.08f && TopRed(panel.width/2)<.08f,"Panel highlights fade locally within the first 80 points, leaving neutral long edges");
            Check(Mathf.Abs(panel.xMax-bar.worldBound.xMax-10)<1.5f && Mathf.Abs(bar.worldBound.yMin-panel.yMin-12)<1.5f && Mathf.Abs(panel.yMax-bar.worldBound.yMax-12)<1.5f,"Scrollbar aligns within the frame with a ten-point right gap and symmetric twelve-point ends: "+panel+" / "+bar.worldBound);
            Color accent=RacingPalette.Accent(root);
            Check(root.customStyle.TryGetValue(RacingPalette.AccentProperty,out var configuredAccent) && Vector4.Distance(configuredAccent,accent)<.01f && Vector4.Distance(accent,(Color)new Color32(245,34,53,255))<.01f,"Native palette stylesheet supplies the shared accent token");
            foreach(string id in new[]{"navBar","settingsButton","navGarage","settingsBody","tabDifficulty","heatManagement","masterVolume"})
                Check(RacingPalette.TryRead(root.Q(id),RacingPalette.AccentProperty,out var inheritedAccent) && Vector4.Distance(accent,inheritedAccent)<.01f,"Shared palette accent reaches native element: "+id);
            Check(Vector4.Distance(root.Q<Label>(className:"brand").resolvedStyle.color,accent)<.01f,"Header wordmark uses the same palette accent as procedural content");
            Destroy(pixels);
            foreach(string id in new[]{"tabSound","settingsButton"})
            {
                var button=root.Q(id);var surface=button.Q<RacingSurface>();
                input.MoveMouseAt(400,650);yield return new WaitForSecondsRealtime(.7f);yield return new WaitForEndOfFrame();
                var before=ScreenCapture.CaptureScreenshotAsTexture();var bounds=button.worldBound;
                int x=(int)(id=="settingsButton"?bounds.xMin+3:bounds.xMax-2),y=Screen.height-1-(int)(id=="settingsButton"?bounds.yMin+8:bounds.yMax-3);
                Color outside=before.GetPixel(x,y);Destroy(before);
                input.MoveMouseAt((int)bounds.center.x,(int)bounds.center.y);yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();
                var after=ScreenCapture.CaptureScreenshotAsTexture();Color hovered=after.GetPixel(x,y);Destroy(after);
                Check(button.resolvedStyle.backgroundColor.a==0 && surface.UsesClippedContour && Vector3.Distance(new Vector3(outside.r,outside.g,outside.b),new Vector3(hovered.r,hovered.g,hovered.b))<.035f,"Hover and shine leave the clear slanted corner unchanged: "+id);
                Check(Mathf.Abs(surface.AntiAliasWidth*surface.scaledPixelsPerPoint-1)<.01f,"Angled outline uses a one-physical-pixel alpha fringe: "+id);
                yield return Capture("polished-hover-"+id);
            }
            var toggle=root.Q<Toggle>("heatManagement");scroll.ScrollTo(toggle);yield return new WaitForSecondsRealtime(.2f);
            var decoration=toggle.Q<SwitchSurface>();float initial=decoration.Progress;bool initialValue=toggle.value;
            // Keep down/up on separate frames so the native input poll observes both edges.
            IEnumerator Tap() {var b=toggle.worldBound;input.MouseButtonAt((int)b.center.x,(int)b.center.y,true);yield return null;input.MouseButtonAt((int)b.center.x,(int)b.center.y,false);yield return null;}
            yield return Tap();yield return new WaitForSecondsRealtime(.055f);
            Check(toggle.value!=initialValue && decoration.Progress>0 && decoration.Progress<1 && decoration.Animating,"Real toggle click animates thumb and track through intermediate state");
            // Measure at the actual input event; OS delivery/frame latency makes the pre-click sample stale.
            float reversedFrom=float.NaN;
            EventCallback<ChangeEvent<bool>> reversed=evt=>{if(evt.newValue==initialValue)reversedFrom=decoration.Progress;};
            toggle.RegisterValueChangedCallback(reversed);yield return Tap();yield return new WaitForSecondsRealtime(.02f);toggle.UnregisterValueChangedCallback(reversed);
            Check(toggle.value==initialValue && reversedFrom>0 && reversedFrom<1 && (initialValue?decoration.Progress>=reversedFrom:decoration.Progress<=reversedFrom),"Rapid toggle reverses from its intermediate position and moves toward the original endpoint");
            yield return new WaitForSecondsRealtime(.25f);Check(decoration.Progress==initial && !decoration.Animating,"Toggle settles exactly at original state after reversal");
            yield return Tap();yield return new WaitForSecondsRealtime(.06f);yield return Capture("polished-switch-transition");
            Check(decoration.Progress==(initialValue?0:1),"Toggle reaches opposite endpoint without continual animation");
            yield return Capture("polished-switch-opposite");yield return Tap();yield return new WaitForSecondsRealtime(.25f);
            input.MoveMouseAt(400,650);yield return new WaitForSecondsRealtime(.2f);int paints=decoration.PaintCount;
            yield return new WaitForSecondsRealtime(.3f);Check(decoration.PaintCount==paints,"Settled switch geometry stays cached");
            yield return Click(input,"revertSettings");scroll.scrollOffset=Vector2.zero;
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
                if(id=="General")
                {
                    var reset=root.Q("resetGame");var row=root.Q("gameDataChoices").worldBound;
                    Check(Mathf.Abs(reset.worldBound.xMax-row.xMax)<1 && reset.worldBound.yMin>=row.yMin,"Reset game is aligned to the right of its General row");
                    Check(!reset.ClassListContains("primary-action") && reset.ClassListContains("reset-action"),"Reset entry is a subdued action, separate from primary Apply");
                }
                Check(root.Q("cancelSettings").worldBound.yMax<Screen.height-30 && root.Q("applySettings").worldBound.yMax<Screen.height-30,"Apply/Cancel remain outside scrolling panel: "+id);
            }
            yield return Click(input,"tabDifficulty");
            var presetGroup=root.Q("presetGroup");
            Check(root.Q("drivingPresetRow").parent==presetGroup && root.Q("simulationDifficultyRow").parent==presetGroup && root.Q("shiftingRow").parent!=presetGroup,"Bulk driving/simulation presets share a distinct group separate from individual controls");
            Check(presetGroup.worldBound.yMax<root.Q("shiftingRow").worldBound.yMin && root.Q<Label>("presetHeading").text=="Presets" && root.Q<Label>("individualHeading").text=="Individual settings","Preset group and individual settings have separate concise visible labels and spacing");
            int marketMinimum=game.settings.guaranteedFittingParts;string stockBefore=JsonUtility.ToJson(game.Profile.inventory);yield return RevealClick(input,"marketAvailability2");Check(shell.DraftDriver.guaranteedFittingParts==0&&game.settings.guaranteedFittingParts==marketMinimum&&stockBefore==JsonUtility.ToJson(game.Profile.inventory),"Hard market difficulty drafts zero guarantees without changing or rerolling live stock");
            root.Q<Slider>("guaranteedFittingParts").value=7.7f;Check(shell.DraftDriver.guaranteedFittingParts==8&&root.Q<FloatField>("guaranteedFittingPartsValue").value==8,"Market guarantee slider and numeric input use whole offers");yield return Click(input,"revertSettings");Check(shell.DraftDriver.guaranteedFittingParts==marketMinimum&&!shell.SettingsDirty,"Revert restores the saved market rule without changing seller stock");
            yield return ControlStyleChecks(input);
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
            var knob=slider.Q(className:"unity-base-slider__dragger").worldBound;
            Check(Mathf.Abs(tracker.center.y-knob.center.y)<1,"Slider thumb is vertically centered on the track: "+tracker+" / "+knob);
            Check(numericInput.Q<TextElement>().resolvedStyle.unityFontDefinition.fontAsset==root.resolvedStyle.unityFontDefinition.fontAsset,"Numeric input uses the shared condensed UI font");
            input.MouseButtonAt((int)Mathf.Lerp(tracker.xMin,tracker.xMax,.32f),(int)tracker.center.y,true); yield return new WaitForSecondsRealtime(.1f);
            input.MouseButtonAt((int)Mathf.Lerp(tracker.xMin,tracker.xMax,.32f),(int)tracker.center.y,false);yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Abs(slider.value-.32f)<.06f && SharedAudioSettings.MasterVolume==master && SharedAudioSettings.DspMasterVolume==master,"Real volume slider edits draft without publishing audio gain");
            root.Q<FloatField>("opponentVolumeValue").value=41;
            Check(root.Q<Slider>("opponentVolume").value==.41f && SharedAudioSettings.OpponentVolume==opponent,"Numeric input and slider stay synchronized without live publication");
            yield return Click(input,"masterVolumeValue"); input.Key(0x11,true);yield return Key(input,0x41);input.Key(0x11,false);
            yield return Key(input,0x33);yield return Key(input,0x32);yield return Key(input,0x0d);
            Check(Mathf.Abs(root.Q<FloatField>("masterVolumeValue").value-32)<.01f && SharedAudioSettings.MasterVolume==master,"Real keyboard numeric entry edits only sound draft");
            var numericFocus=root.focusController.focusedElement;
            yield return Key(input,0x09);
            Check(root.focusController.focusedElement==numericFocus && Mathf.Abs(root.Q<FloatField>("masterVolumeValue").value-32)<.01f,"Tab leaves mouse-focused numeric editing and draft value intact: "+numericFocus+" → "+root.focusController.focusedElement+" / "+root.Q<FloatField>("masterVolumeValue").value);
            input.Key(0x10,true);yield return Key(input,0x09);input.Key(0x10,false);
            Check(root.focusController.focusedElement==numericFocus,"Shift+Tab leaves numeric focus intact");
            yield return Key(input,0x77);Check(shell.Visible,"Native numeric text focus blocks F8");
            yield return Click(input,"tabUnits");yield return Click(input,"speed1");yield return Click(input,"pressure1");yield return Click(input,"torque1");
            Check(JsonUtility.ToJson(game.Profile)==before && JsonUtility.ToJson(SharedUnitSettings.Current)==originalUnits,"Driving/units/category edits leave applied profile untouched");
            yield return Key(input,0x77);Check(!shell.Visible,"F8 keeps dirty draft while showing legacy Settings");
            yield return Key(input,0x77);Check(shell.Visible && shell.SettingsDirty,"F8 restores same dirty Settings draft");
            yield return Click(input,"applySettings"); Check(shell.ModalOpen,"Apply opens actual-change confirmation");
            var modalFocus=root.focusController.focusedElement;
            yield return Key(input,0x09);
            input.Key(0x10,true);yield return Key(input,0x09);input.Key(0x10,false);
            Check(root.focusController.focusedElement==modalFocus && shell.ModalOpen,"Tab and Shift+Tab do not traverse dynamically generated confirmation buttons");
            Check(root.Q<ScrollView>("modalContent").Q<Label>().text.Contains("Master") && root.Q<ScrollView>("modalContent").Q<Label>().text.Contains("Mechanical wear"),"Confirmation names actual pending changes");
            Check(root.Q<ScrollView>("modalContent").verticalScroller.ClassListContains("settings-scrollbar") && root.Q<ScrollView>("modalContent").verticalScroller.lowButton.resolvedStyle.display==DisplayStyle.None,"Confirmation details use the same themed scrollbar");
            DialogScrollBounds("Settings confirmation");
            yield return new WaitForEndOfFrame();
            var dialogPixels=ScreenCapture.CaptureScreenshotAsTexture(); var dialogBounds=root.Q("modalCard").worldBound;
            Color dialogTop=Pixel(dialogPixels,dialogBounds.xMin+14,dialogBounds.yMin+1);
            Color dialogBottom=Pixel(dialogPixels,dialogBounds.xMax-14,dialogBounds.yMax-1);
            Color dialogLeft=Pixel(dialogPixels,dialogBounds.xMin+1,dialogBounds.center.y);
            Check(root.Q("modalCard").Q<RacingSurface>()!=null && root.Q("modalCard").resolvedStyle.borderLeftWidth==0,"Dialog reuses the shared brushed panel surface without a full left accent border");
            Check(dialogTop.r-dialogTop.g>.1f && dialogBottom.r-dialogBottom.g>.1f && dialogLeft.r-dialogLeft.g<.08f,"Rendered dialog has top-left/bottom-right corner highlights and a neutral middle edge"); Destroy(dialogPixels);
            yield return Capture("settings-apply-dialog");yield return Key(input,0x77);Check(shell.Visible && shell.ModalOpen,"Confirmation blocks F8");
            var close=root.Q("modalClose");var closeBounds=close.worldBound;
            input.MoveMouseAt(100,650);yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();
            var closeNormal=ScreenCapture.CaptureScreenshotAsTexture();Color normalCloseFace=Pixel(closeNormal,closeBounds.xMin+5,closeBounds.center.y);Destroy(closeNormal);
            input.MoveMouseAt((int)closeBounds.center.x,(int)closeBounds.center.y);yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();
            var closeHovered=ScreenCapture.CaptureScreenshotAsTexture();Color hoveredCloseFace=Pixel(closeHovered,closeBounds.xMin+5,closeBounds.center.y);Destroy(closeHovered);
            Check(hoveredCloseFace.grayscale>normalCloseFace.grayscale+.04f,"Mouse hover visibly lights dialog X even while it has modal focus");
            yield return Capture("dialog-close-hover");
            input.MouseButtonAt((int)closeBounds.center.x,(int)closeBounds.center.y,true);yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();
            var closeDown=ScreenCapture.CaptureScreenshotAsTexture();Color downCloseFace=Pixel(closeDown,closeBounds.xMin+5,closeBounds.center.y);Destroy(closeDown);
            Check(downCloseFace.grayscale<hoveredCloseFace.grayscale-.06f && shell.ModalOpen,"Dialog X retains distinct mouse-down styling before release");
            input.MouseButtonAt((int)closeBounds.center.x,(int)closeBounds.center.y,false);yield return new WaitForSecondsRealtime(.2f);
            Check(!shell.ModalOpen && shell.SettingsDirty && JsonUtility.ToJson(game.Profile)==before,"X dismisses only dialog and keeps draft");
            yield return Click(input,"applySettings");input.MoveMouseAt((int)closeBounds.center.x,(int)closeBounds.center.y);yield return new WaitForSecondsRealtime(.2f);
            input.MoveMouseAt(100,650);yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();
            var closeLeft=ScreenCapture.CaptureScreenshotAsTexture();Color leftCloseFace=Pixel(closeLeft,closeBounds.xMin+5,closeBounds.center.y);Destroy(closeLeft);
            Check(Mathf.Abs(leftCloseFace.grayscale-normalCloseFace.grayscale)<.02f,"Leaving dialog X removes hover despite retained focus");
            yield return Click(input,"modalClose");
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
            string drivingDisk=File.ReadAllText(DrivingPreferences.FilePath);string profileDisk=File.ReadAllText(ProfileStore.FilePath),backupDisk=File.Exists(ProfileStore.FilePath+".bak")?File.ReadAllText(ProfileStore.FilePath+".bak"):null,audioDisk=File.ReadAllText(SharedAudioSettings.FilePath),unitDisk=File.ReadAllText(SharedUnitSettings.FilePath);
            float liveGain=SharedAudioSettings.MasterVolume;
            using(var locked=new FileStream(DrivingPreferences.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                Check(!game.ApplyNativeSettings(failedDriver,0,new UnitPreferences(),.91f,.92f,out var error) && error!=null,"Locked preference file rejects settings commit independently of progression");
                Check(JsonUtility.ToJson(game.Profile)==current && SharedAudioSettings.MasterVolume==liveGain && SharedAudioSettings.OpponentVolume==.41f,"Failed commit never publishes new profile or audio cache");
                Check(File.Exists(Path.Combine(folder,"settings-commit.json")),"Interrupted rollback retains durable recovery journal");
            }
            Check(PreferenceCommit.Recover(out var recoveryError) && recoveryError==null,"Incomplete settings write recovers after lock released");
            Check(File.ReadAllText(DrivingPreferences.FilePath)==drivingDisk&&File.ReadAllText(ProfileStore.FilePath)==profileDisk && (File.Exists(ProfileStore.FilePath+".bak")?File.ReadAllText(ProfileStore.FilePath+".bak"):null)==backupDisk && File.ReadAllText(SharedAudioSettings.FilePath)==audioDisk && File.ReadAllText(SharedUnitSettings.FilePath)==unitDisk,"Recovery restores all three files and profile backup exactly");
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
            Check(game.Profile.cash==6500 && game.Profile.reputation==0 && game.Profile.cars.Count==0 && game.Profile.firstPurchasePending && game.Profile.vehicleStock.listings.Count==3 && game.settings.shifting==ShiftMode.ManualClutch,"Reset clears progression and retains applied driving preferences");
            Check(Mathf.Abs(SharedAudioSettings.MasterVolume-.58f)<.00001f && !shell.SettingsDirty && game.ScreenName=="Garage","Reset keeps applied sound, discards unsaved draft and opens the starter dealership");
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
            var target=new RenderTexture(3840,2160,24);target.Create();shell.Panel.targetTexture=target;shell.Refresh();
            yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
            float layoutUntil=Time.realtimeSinceStartup+3;
            while(Time.realtimeSinceStartup<layoutUntil&&(shell.Panel.scale!=2||root.Q("applySettings").worldBound.xMax>1920||root.Q("settingsBody").worldBound.yMax>=root.Q("settingsActions").worldBound.yMin))yield return null;
            Check(shell.Panel.scale==2 && root.Q("applySettings").worldBound.xMax<=1920 && root.Q("settingsBody").worldBound.yMax<root.Q("settingsActions").worldBound.yMin,"Populated 4K layout retains panel/action separation with shared 2x scale / scale "+shell.Panel.scale+" apply "+root.Q("applySettings").worldBound+" body "+root.Q("settingsBody").worldBound+" actions "+root.Q("settingsActions").worldBound);
            Check(Mathf.Abs(root.Q("settingsBody").Q<RacingSurface>().AntiAliasWidth-.5f)<.01f,"True 4K target keeps its edge fringe at one physical pixel");
            var prior=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(3840,2160,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"settings-populated-4k.png"),image.EncodeToPNG());
            RenderTexture.active=prior;shell.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(image);
        }
#endif
    }
}
