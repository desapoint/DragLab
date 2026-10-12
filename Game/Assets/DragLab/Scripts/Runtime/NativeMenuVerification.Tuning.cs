using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        void TuningBounds()
        {
            var left=shell.Root.Q("tuningEditor").worldBound;var right=shell.Root.Q("tuningPreview").worldBound;var actions=shell.Root.Q("tuningActions").worldBound;
            Check(left.width>0&&right.width>0&&left.xMax<right.xMin&&right.yMax<=actions.yMin,"Tuning columns and anchored actions fit at "+Screen.width+" / scale "+shell.Panel.scale);
            Check(shell.Root.Q("tuningTabs").Query<Button>().ToList().All(b=>b.worldBound.xMin>=left.xMin&&b.worldBound.xMax<=left.xMax),"All category tabs fit the left column");
            Check(shell.Root.Q<NativeDynoPlot>("tuningDynoPlot").worldBound.xMax<=right.xMax,"Shared dyno fits inside the comparison viewport");
            Check(shell.Root.Query<VisualElement>().ToList().All(e=>e.tabIndex==-1),"Tuning reuses the no-Tab navigation contract");
        }
        IEnumerator TuningLayoutChecks(WindowsKeyboardProbe input)
        {
            var original=game.Profile;var field=typeof(PrototypeGame).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic);
            byte[] disk=File.ReadAllBytes(ProfileStore.FilePath),backup=File.Exists(ProfileStore.FilePath+".bak")?File.ReadAllBytes(ProfileStore.FilePath+".bak"):null;
            try{
                yield return Click(input,"navTuning");yield return new WaitForSecondsRealtime(.2f);
                string baseline=JsonUtility.ToJson(game.Profile);int captures=shell.TuningPreviewCaptureCount;
                var panel=shell.Root.Q<NativeStatsPanel>("tuningStats");var plot=shell.Root.Q<NativeDynoPlot>("tuningDynoPlot");
                Check(shell.Visible&&!shell.Root.Q("tuningPage").ClassListContains("hidden")&&shell.Root.Q<Button>("navTuning").ClassListContains("selected"),"Tuning navigation displays its native page frame");
                Check(plot.Series.Count==3&&plot.Series[0].Title=="Current"&&plot.Series[1].Title=="Proposed"&&ReferenceEquals(plot.Series[0].Inspection,panel.Current.Inspection)&&ReferenceEquals(plot.Series[1].Inspection,panel.Proposed.Inspection),"Summary and dyno reuse identical Current/Proposed inspections");
                Check(plot.Series[2].Title=="No wear"&&plot.Series[2].Inspection.condition==1&&SnapshotEquality.Serialized(JsonUtility.ToJson(plot.Series[0].Inspection.source.startingFluids),plot.Series[2].Inspection.source.startingFluids),"Existing no-wear reference remains visible without refilling real or reference supplies");
                Check(!shell.Root.Q<Button>("tuningApply").enabledSelf&&!shell.Root.Q<Button>("tuningRevert").enabledSelf,"Unchanged tune has disabled Apply/Revert");
                Check(shell.Root.Q("tuningControls").Query<Slider>().ToList().Count==0&&shell.Root.Q("tuningControls").Query<FloatField>().ToList().Count==0,"First checkpoint adds no tuning sliders or inputs");
                TuningBounds();yield return Capture("tuning-layout");
                foreach(string tab in new[]{"Induction","Drivetrain","Wheels","Engine"}){
                    yield return Click(input,"tuningTab"+tab);Check(shell.Root.Q<Button>("tuningTab"+tab).ClassListContains("selected")&&!shell.Root.Q("tuningArea"+tab).ClassListContains("hidden"),"Category switches locally: "+tab);
                }
                Check(shell.TuningPreviewCaptureCount==captures&&baseline==JsonUtility.ToJson(game.Profile)&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"Category browsing does not recalculate previews, apply tunes or save");
                var current=InspectionBuild.Capture(game.Profile,game.Profile.SelectedCar);string fluids=JsonUtility.ToJson(current.startingFluids),parts=JsonUtility.ToJson(current.components);
                yield return Key(input,0x77);Check(!shell.Visible&&game.ScreenName=="Tuning","F8 exposes legacy tuning on the same route");
                game.DraftValues.driverMassKg+=70;game.DraftValues.finalDrive*=.85f;var pending=game.DraftValues;yield return Key(input,0x77);
                panel=shell.Root.Q<NativeStatsPanel>("tuningStats");plot=shell.Root.Q<NativeDynoPlot>("tuningDynoPlot");
                Check(ReferenceEquals(game.DraftValues,pending)&&game.DraftDirty&&shell.Root.Q<Button>("tuningApply").enabledSelf,"F8 preserves the single authoritative draft and enables explicit Apply");
                Check(panel.Proposed.Inspection.vehicleMassKg>panel.Current.Inspection.vehicleMassKg+69,"Draft driver mass produces a real comparison difference");
                Check(panel.Proposed.Tune.finalDrive==panel.Current.Tune.finalDrive&&game.DraftValues.finalDrive!=panel.Current.Tune.finalDrive,"Preview respects the fixed OEM gearset even when the draft requests another ratio");
                Check(SnapshotEquality.Serialized(parts,panel.Proposed.Inspection.source.components)&&SnapshotEquality.Serialized(fluids,panel.Proposed.Inspection.source.startingFluids),"Proposed snapshots retain all fitted identities, wear/faults and actual liquids");
                Check(baseline==JsonUtility.ToJson(game.Profile)&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"Draft comparison changes no live or saved assets");
                yield return Capture("tuning-draft");
                var rect=plot.worldBound;input.MoveMouseAt((int)rect.center.x,(int)rect.center.y);yield return new WaitForSecondsRealtime(.2f);Check(plot.HoverRpm.HasValue,"Shared dyno keeps pointer inspection/intersection markers");
                var before=game.DraftValues;yield return Click(input,"statInfo:tuningWeight");Check(shell.ModalOpen&&shell.Root.Q<Label>("modalTitle").text=="Weight","Summary info opens the shared read-only explanation dialog");yield return Click(input,"dialogClose");Check(ReferenceEquals(before,game.DraftValues),"Info dialog retains draft ownership");
                yield return Click(input,"tuningRevert");Check(!game.DraftDirty&&!shell.Root.Q<Button>("tuningApply").enabledSelf&&baseline==JsonUtility.ToJson(game.Profile),"Revert restores applied values without saving");
                game.DraftValues.driverMassKg+=70;shell.Refresh();yield return new WaitForSecondsRealtime(.1f);string failedBefore=JsonUtility.ToJson(game.Profile);
                using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)){
                    yield return Click(input,"tuningApply");Check(game.DraftDirty&&!string.IsNullOrEmpty(shell.Root.Q<Label>("tuningError").text)&&failedBefore==JsonUtility.ToJson(game.Profile),"Failed durable Apply retains profile and editable draft, displaying the error");
                }
                long revision=game.Profile.revision;yield return Click(input,"tuningApply");
                Check(!game.DraftDirty&&game.Profile.revision==revision+1&&Math.Abs(game.Profile.SelectedCar.tune.driverMassKg-(original.SelectedCar.tune.driverMassKg+70))<.001,"Explicit Apply commits the existing authoritative transaction once");
                var loaded=ProfileStore.Load();Check(loaded.SelectedCar.tune.driverMassKg==game.Profile.SelectedCar.tune.driverMassKg,"Applied tune survives real disk reload");
                Check(shell.Root.Q<Label>("tuningError").text==""&&!shell.Root.Q<Button>("tuningApply").enabledSelf,"Successful Apply clears errors and returns to a clean preview");
                game.DraftValues.finalDrive*=.85f;shell.Refresh();yield return Click(input,"navGarage");yield return Click(input,"navTuning");Check(!game.DraftDirty&&game.DraftValues.finalDrive==game.Profile.SelectedCar.tune.finalDrive,"Leaving the page discards unapplied changes");
                yield return Click(input,"tuningTabWheels");yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");Check(game.ScreenName=="Tuning"&&shell.Root.Q<Button>("tuningTabWheels").ClassListContains("selected"),"Settings Cancel returns to Tuning with its category retained");
                Screen.SetResolution(1024,768,false);yield return new WaitForSecondsRealtime(.5f);TuningBounds();yield return Capture("tuning-compact");
                Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.5f);TuningBounds();yield return Capture("tuning-1080p");
                var target=new RenderTexture(3840,2160,24);target.Create();shell.Panel.targetTexture=target;shell.Refresh();yield return new WaitForSecondsRealtime(.3f);Check(shell.Panel.scale==2,"Tuning uses shared crisp 4K scaling");TuningBounds();
                var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(3840,2160,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"tuning-4k.png"),image.EncodeToPNG());RenderTexture.active=previous;shell.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(image);
                Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.4f);
                var detached=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(game.Profile));detached.catalog=original.catalog;MechanicsOwnership.Ensure(detached,detached.catalog);AssemblyOperations.Detach(detached,detached.SelectedCar.instanceId);field.SetValue(game,detached);shell.Refresh();yield return new WaitForSecondsRealtime(.2f);
                string retained=JsonUtility.ToJson(detached);Check(shell.Root.Q<NativeDynoPlot>("tuningDynoPlot")==null&&!shell.Root.Q("tuningUnavailable").ClassListContains("hidden")&&!shell.Root.Q<Button>("tuningApply").enabledSelf,"Engine-less chassis shows an honest unavailable preview with no invented curve");yield return Capture("tuning-no-engine");Check(retained==JsonUtility.ToJson(game.Profile),"Unavailable inspection cannot fabricate an engine or refill the car");
                var empty=PlayerProfile.NewGame(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(empty,original.catalog);field.SetValue(game,empty);shell.Refresh();yield return new WaitForSecondsRealtime(.2f);Check(shell.Root.Q<Label>("tuningCarName").text=="No car"&&shell.Root.Q<NativeStatsPanel>("tuningStats")==null&&empty.cars.Count==0,"Empty garage remains empty and directs selection through Garage");yield return Capture("tuning-no-car");
                Check(errors==0,"Native Tuning checkpoint has no runtime errors");
            }finally{
                if(shell.ModalOpen)typeof(NativeMenuShell).GetMethod("CloseDialog",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);
                field.SetValue(game,original);game.settings=original.driving;game.DiscardPreviews();File.WriteAllBytes(ProfileStore.FilePath,disk);if(backup!=null)File.WriteAllBytes(ProfileStore.FilePath+".bak",backup);else if(File.Exists(ProfileStore.FilePath+".bak"))File.Delete(ProfileStore.FilePath+".bak");shell.InvalidateOwnedGrid();shell.InvalidateDealer();game.QueueNativeNavigation("Garage");
            }
            yield return new WaitForSecondsRealtime(.3f);
        }
#endif
    }
}
