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
                if(!shell.Visible)yield return Key(input,0x77);
                yield return Click(input,"navTuning");yield return new WaitForSecondsRealtime(.2f);
                if(!shell.Visible)yield return Key(input,0x77);
                string baseline=JsonUtility.ToJson(game.Profile);int captures=shell.TuningPreviewCaptureCount;
                var panel=shell.Root.Q<NativeStatsPanel>("tuningStats");var plot=shell.Root.Q<NativeDynoPlot>("tuningDynoPlot");
                Check(shell.Visible&&!shell.Root.Q("tuningPage").ClassListContains("hidden")&&shell.Root.Q<Button>("navTuning").ClassListContains("selected"),"Tuning navigation displays its native page frame");
                Check(plot.Series.Count==3&&plot.Series[0].Title=="Current"&&plot.Series[1].Title=="Proposed"&&ReferenceEquals(plot.Series[0].Inspection,panel.Current.Inspection)&&ReferenceEquals(plot.Series[1].Inspection,panel.Proposed.Inspection),"Summary and dyno reuse identical Current/Proposed inspections");
                Check(plot.Series[2].Title=="No wear"&&plot.Series[2].Inspection.condition==1&&SnapshotEquality.Serialized(JsonUtility.ToJson(plot.Series[0].Inspection.source.startingFluids),plot.Series[2].Inspection.source.startingFluids),"Existing no-wear reference remains visible without refilling real or reference supplies");
                Check(!shell.Root.Q<Button>("tuningApply").enabledSelf&&!shell.Root.Q<Button>("tuningRevert").enabledSelf,"Unchanged tune has disabled Apply/Revert");
                Check(shell.Root.Q("tuningControls").Query<Slider>().ToList().Count>=12&&shell.Root.Q("tuningControls").Query<FloatField>().ToList().Count>=12,"Tuning tabs contain stepped sliders and matching numeric inputs");
                TuningBounds();yield return Capture("tuning-layout");
                foreach(string tab in new[]{"Induction","Drivetrain","Wheels","Engine"}){
                    yield return Click(input,"tuningTab"+tab);Check(shell.Root.Q<Button>("tuningTab"+tab).ClassListContains("selected")&&!shell.Root.Q("tuningArea"+tab).ClassListContains("hidden"),"Category switches locally: "+tab);
                    Check(shell.Root.Q("tuningArea"+tab).Query<Button>(className:"choice-button").ToList().All(b=>b.worldBound.height>=30&&b.worldBound.width>20),"Joined choices retain usable rendered dimensions: "+tab);
                }
                Check(shell.TuningPreviewCaptureCount==captures&&baseline==JsonUtility.ToJson(game.Profile)&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"Category browsing does not recalculate previews, apply tunes or save");
                yield return TuningGestureChecks(input);
                yield return AxleTuningChecks(input);
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
                var before=game.DraftValues;yield return Click(input,"statInfo:tuningWeight");Check(shell.ModalOpen&&shell.Root.Q<Label>("modalTitle").text=="Weight","Summary info opens the shared read-only explanation dialog");
                string help=shell.Root.Q("modalContent").Query<Label>().ToList().First(l=>l.text.StartsWith("More: ")).text;
                Check(help.Contains("\n\nLess: ")&&help.Contains("Drawbacks in this comparison: The compared value is higher.")&&help.Contains("slow acceleration"),"Heavier tuning draft explains both directions and its actual acceleration drawback");
                yield return Capture("stat-help-tuning-weight");yield return Click(input,"dialogClose");Check(ReferenceEquals(before,game.DraftValues),"Info dialog retains draft ownership");
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
                yield return TuningHardwareChecks(input);
                yield return SetupSaveChecks(input);
                yield return SetupLibraryChecks(input);
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

        IEnumerator RevealTune(WindowsKeyboardProbe input,string tab,string id)
        {
            yield return Click(input,"tuningTab"+tab);var area=shell.Root.Q<ScrollView>("tuningArea"+tab);area.ScrollTo(shell.Root.Q("tuneRow:"+id));yield return new WaitForSecondsRealtime(.15f);
        }
        bool Increment(float value,float step)=>Math.Abs(value/step-Math.Round(value/step))<.005;
        IEnumerator TuningGestureChecks(WindowsKeyboardProbe input)
        {
            string baseline=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            yield return RevealTune(input,"Engine","launch");var range=shell.Root.Q<NativeTuningRange>("tuneRange:launch");var track=range.Slider.Q(className:"unity-base-slider__tracker").worldBound;
            input.MouseButtonAt((int)Mathf.Lerp(track.xMin,track.xMax,.65f),(int)track.center.y,true);yield return new WaitForSecondsRealtime(.12f);input.MouseButtonAt((int)Mathf.Lerp(track.xMin,track.xMax,.65f),(int)track.center.y,false);yield return new WaitForSecondsRealtime(.15f);
            Check(Increment(game.DraftValues.launchRpm,100)&&game.DraftDirty&&range.Number.value==game.DraftValues.launchRpm,"Real left slider uses 100 RPM increments and synchronizes the draft number");
            float coarse=game.DraftValues.launchRpm;
            input.MouseButtonAt((int)track.center.x,(int)track.center.y,true,1);yield return new WaitForSecondsRealtime(.12f);
            Check(game.DraftValues.launchRpm==coarse&&range.Dragging,"Right-down preserves the current value and captures fine adjustment");
            input.MoveMouseAt((int)track.center.x+35,(int)track.center.y);yield return new WaitForSecondsRealtime(.2f);input.MouseButtonAt((int)track.center.x+35,(int)track.center.y,false,1);yield return new WaitForSecondsRealtime(.15f);
            Check(game.DraftValues.launchRpm>coarse&&game.DraftValues.launchRpm<coarse+100&&Increment(game.DraftValues.launchRpm,10)&&!range.Dragging,"Real right-drag uses relative 10 RPM increments and releases ownership");
            input.MouseButtonAt((int)track.center.x,(int)track.center.y,true,2);yield return new WaitForSecondsRealtime(.1f);input.MouseButtonAt((int)track.center.x,(int)track.center.y,false,2);yield return new WaitForSecondsRealtime(.15f);
            var defaults=game.vehicles.First(v=>v.spec.id==game.Profile.SelectedCar.vehicleId).CreateStockTune();
            Check(game.DraftValues.launchRpm==defaults.launchRpm&&range.Number.value==defaults.launchRpm,"Middle-click resets the launch field to its car default");
            yield return Click(input,"tuneNumber:launch");input.Key(0x11,true);yield return Key(input,0x41);input.Key(0x11,false);yield return Key(input,0x33);yield return Key(input,0x34);yield return Key(input,0x37);yield return Key(input,0x30);
            var text=range.Number.Q<TextElement>().text;yield return new WaitForSecondsRealtime(.4f);
            Check(range.Number.Q<TextElement>().text==text&&game.DraftValues.launchRpm==defaults.launchRpm,"Frame refresh preserves delayed typed text without changing the draft");yield return Key(input,0x0d);
            Check(game.DraftValues.launchRpm==3470&&range.Slider.value==3470,"Real numeric entry commits a precise value on Enter");
            range.Number.value=float.NaN;Check(game.DraftValues.launchRpm==3470&&range.Number.value==3470&&!string.IsNullOrEmpty(shell.Root.Q<Label>("tuningError").text),"Non-finite input is rejected and restored without corrupting the tune");
            range.Number.value=999999;Check(game.DraftValues.launchRpm==range.Slider.highValue,"Numeric input clamps to the permitted maximum");
            Check(!shell.Root.Q<Slider>("tuneSlider:limiterRpm").enabledInHierarchy&&!shell.Root.Q<FloatField>("tuneNumber:camTiming").enabledInHierarchy,"Stock limiter and fixed timing hardware keep unsupported tuning locked");
            yield return Click(input,"tuneInfo:camTiming");Check(shell.ModalOpen&&shell.Root.Q<Label>("modalTitle").text=="Cam timing","Locked hardware retains its accessible explanation dialog");yield return Click(input,"dialogClose");
            yield return RevealTune(input,"Wheels","radius");var originalLayers=shell.Root.Q("tuningWheelPreview").Query<Image>().ToList().Select(i=>i.style.height.value.value).ToArray();
            var radiusRange=shell.Root.Q<NativeTuningRange>("tuneRange:radius");float untouchedRadius=radiusRange.Number.value;var radiusTrack=radiusRange.Slider.Q(className:"unity-base-slider__tracker").worldBound;
            input.MouseButtonAt((int)radiusTrack.center.x,(int)radiusTrack.center.y,true,1);yield return new WaitForSecondsRealtime(.1f);input.MouseButtonAt((int)radiusTrack.center.x,(int)radiusTrack.center.y,false,1);yield return new WaitForSecondsRealtime(.1f);
            Check(radiusRange.Number.value==untouchedRadius,"Right-click alone preserves an unaligned converted factory radius without rounding or jumping");
            shell.Root.Q<FloatField>("tuneNumber:radius").value=15.23f;yield return new WaitForSecondsRealtime(.25f);
            Check(Math.Abs(game.DraftValues.WheelDiameter-15.23f/(game.Profile.catalog.Spec(game.Profile.SelectedCar.vehicleId).wheelRadiusM/DisplayUnits.MetresPerInch))<.001,"Outer tire radius converts inches into the physical wheel scale");
            var revisedLayers=shell.Root.Q("tuningWheelPreview").Query<Image>().ToList().Select(i=>i.style.height.value.value).ToArray();
            Check(revisedLayers.Length==originalLayers.Length&&revisedLayers.Where((v,i)=>Math.Abs(v-originalLayers[i])>.01).Any(),"Draft outer radius visibly changes the rendered layered wheel proportions");yield return Capture("tuning-wheel-draft");
            yield return RevealTune(input,"Wheels","payload");shell.Root.Q<FloatField>("tuneNumber:payload").value=32.5f;Check(game.DraftValues.payloadMassKg==32.5f,"Ballast numeric entry supports its half-kilogram fine increment");
            Check(baseline==JsonUtility.ToJson(game.Profile)&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"All real slider, fine-drag, reset and numeric gestures leave applied assets and disk bytes untouched");
            yield return Click(input,"tuningRevert");yield return Click(input,"tuningTabEngine");Check(!game.DraftDirty,"Revert clears every gesture edit in the shared draft");
        }

        IEnumerator TuningHardwareChecks(WindowsKeyboardProbe input)
        {
            var original=game.Profile;var field=typeof(PrototypeGame).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic);var units=SharedUnitSettings.Current;var speed=units.speed;var pressure=units.pressure;var torque=units.torque;
            byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            try{
                foreach(var vehicle in game.vehicles){
                    var fixture=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(original));fixture.catalog=original.catalog;fixture.cash=10000000;fixture.reputation=10000;
                    VehicleCollection.Buy(fixture,fixture.catalog,new VehicleOffer{model=vehicle.spec.id,price=vehicle.purchasePrice,reputation=vehicle.reputationRequired,stock=vehicle.CreateStockTune(),paint="#DA3448"});
                    field.SetValue(game,fixture);game.BeginTuneDraft();shell.Refresh();yield return new WaitForSecondsRealtime(.15f);
                    string clean=JsonUtility.ToJson(fixture);Check(!game.DraftDirty&&clean==JsonUtility.ToJson(fixture)&&shell.Root.Q("tuningPreviewError")==null,"Stock tuning controls preserve the complete build: "+vehicle.spec.id);
                    var car=fixture.SelectedCar;var catalog=fixture.catalog;var spec=catalog.Spec(car.vehicleId);
                    foreach(var category in new[]{PartCategory.RevLimiter,PartCategory.TwoStep,PartCategory.ShiftLight,PartCategory.TimingDrive,PartCategory.Gearbox,PartCategory.Controls})PartOperations.Buy(fixture,catalog,car.instanceId,catalog.Id(car.vehicleId,category,1),true);
                    fixture.revision++;
                    foreach(var kit in new[]{EngineUpgradeInduction.TwinTurbo,EngineUpgradeInduction.TwinSupercharger,EngineUpgradeInduction.Twincharged}){
                        var part=catalog.Find(car.vehicleId+"/Induction/"+kit);if(part==null||!catalog.Fits(spec,part))continue;
                        PartOperations.Buy(fixture,catalog,car.instanceId,part.id,true);fixture.revision++;game.BeginTuneDraft();shell.Refresh();yield return new WaitForSecondsRealtime(.12f);
                        var build=MechanicsOwnership.Resolve(fixture,car,catalog);var profile=new InductionProfile(build.spec,build.tune);
                        Check(shell.Root.Q<Slider>("tuneSlider:boost")!=null&&shell.Root.Q<Slider>("tuneSlider:finalDrive").enabledInHierarchy&&shell.Root.Q<Slider>("tuneSlider:camTiming").enabledInHierarchy,"Installed induction/gearset/timing capabilities project into controls: "+vehicle.spec.id+" / "+kit);
                        Check(shell.Root.Q<Slider>("tuneSlider:limiterRpm").enabledInHierarchy&&shell.Root.Q<Slider>("tuneSlider:shiftLight").enabledInHierarchy&&shell.Root.Q<Button>("tuneChoice:twoStep:2").enabledInHierarchy,"Programmable equipment unlocks limiter, shift light and two-step: "+vehicle.spec.id);
                        bool injection=build.spec.engineBehavior.fueling==FuelSystem.Injection;Check(shell.Root.Q<Button>("tuneChoice:limiterCut:1").enabledInHierarchy==injection,"Cut editing respects carburetor/diesel/injection hardware: "+vehicle.spec.id);
                        Check((shell.Root.Q<Slider>("tuneSlider:pulley")!=null)==(profile.superchargerCount>0)&&(shell.Root.Q<Button>("tuneChoice:valve:1")!=null)==(profile.turboCount>0),"Only fitted compressor types expose pulley and pressure-release controls: "+kit);
                        if(profile.turboCount>0)Check(shell.Root.Q<Button>("tuneChoice:antiLag:2").enabledInHierarchy==(build.spec.engineBehavior.fueling!=FuelSystem.Diesel),"Anti-lag editing follows installed fueling capability: "+vehicle.spec.id);
                        yield return Click(input,"tuningTabInduction");if(vehicle==game.vehicles[0])yield return Capture("tuning-"+kit);
                    }
                    game.BeginTuneDraft();shell.Refresh();yield return Click(input,"tuningTabEngine");
                    shell.Root.Q<FloatField>("tuneNumber:camTiming").value=1.7f;shell.Root.Q<FloatField>("tuneNumber:limiterRpm").value=spec.redlineRpm-130;
                    Check(game.DraftValues.camTimingDegrees==1.7f&&game.DraftDirty&&shell.Root.Q("tuningPreviewError")==null,"Hardware-enabled fields produce a valid draft preview: "+vehicle.spec.id);
                    if(spec.drive==DragLab.Domain.DriveType.AWD){
                        yield return Click(input,"tuningTabDrivetrain");
                        foreach(var mode in new[]{AwdMode.FixedSplit,AwdMode.OpenCenter,AwdMode.LockedCenter,AwdMode.OnDemand,AwdMode.ActiveBias,AwdMode.LimitedSlipCenter}){
                            yield return RevealTune(input,"Drivetrain","awdMode");var modeButton=shell.Root.Q<Button>("tuneChoice:awdMode:"+(int)mode);shell.Root.Q<ScrollView>("tuningAreaDrivetrain").ScrollTo(modeButton);yield return new WaitForSecondsRealtime(.2f);
                            var picked=shell.Root.panel.Pick(modeButton.worldBound.center);Check(picked==modeButton||modeButton.Contains(picked),"AWD test target is visible and hittable: "+mode+" / "+vehicle.spec.id+" / hit "+picked?.name+" / bounds "+modeButton.worldBound);
                            yield return Click(input,modeButton);
                            if(vehicle.spec.id=="rally-sedan"&&mode==AwdMode.FixedSplit)yield return Capture("tuning-awd-fixed");
                            Check(game.DraftValues.awdConfigured&&game.DraftValues.awd.mode==mode&&shell.Root.Q("tuningPreviewError")==null,"AWD mode builds its controls and authoritative preview: "+mode+" / "+vehicle.spec.id+" / configured "+game.DraftValues.awdConfigured+" / actual "+game.DraftValues.awd?.mode+" / enabled "+shell.Root.Q<Button>("tuneChoice:awdMode:"+(int)mode).enabledInHierarchy+" / error "+shell.Root.Q<Label>("tuningPreviewError")?.text);
                            Check((shell.Root.Q<Slider>("tuneSlider:shaftInertia")!=null)==(mode==AwdMode.OpenCenter)&&(shell.Root.Q<Slider>("tuneSlider:slipThreshold")!=null)==(mode==AwdMode.OnDemand),"Topology-specific AWD controls match installed draft mode: "+mode);
                            if(mode==AwdMode.LockedCenter)Check(!shell.Root.Q<Slider>("tuneSlider:centerRatio").enabledInHierarchy,"Ordinary locked-center hardware cannot tune the speed ratio");
                            if(mode==AwdMode.ActiveBias){shell.Root.Q<FloatField>("tuneNumber:minimumFront").value=85;shell.Root.Q<FloatField>("tuneNumber:maximumFront").value=5;Check(game.DraftValues.awd.minimumFront<=game.DraftValues.awd.maximumFront,"Active AWD front-share bounds remain ordered under conflicting numeric edits");}
                        }
                        PartOperations.Buy(fixture,catalog,car.instanceId,catalog.Id(car.vehicleId,PartCategory.Differential,1),true);fixture.revision++;game.BeginTuneDraft();shell.Refresh();yield return new WaitForSecondsRealtime(.15f);Check(!shell.Root.Q<Button>("tuneChoice:awdMode:0").enabledInHierarchy,"Upgraded center hardware fixes its AWD topology");
                        PartOperations.Buy(fixture,catalog,car.instanceId,car.vehicleId+"/TransferCase/RatioLocked",true);fixture.revision++;game.BeginTuneDraft();shell.Refresh();yield return new WaitForSecondsRealtime(.15f);
                        yield return RevealTune(input,"Drivetrain","centerRatio");Check(shell.Root.Q<Slider>("tuneSlider:centerRatio").enabledInHierarchy,"Special transfer-case hardware unlocks the shared ratio control: "+car.vehicleId);
                        shell.Root.Q<FloatField>("tuneNumber:centerRatio").value=1.5f;game.DraftValues.frontWheelDiameter=.8f;game.DraftValues.rearWheelDiameter=1.2f;shell.Refresh();yield return new WaitForSecondsRealtime(.2f);
                        var ratioPreview=shell.Root.Q<NativeStatsPanel>("tuningStats").Proposed;if(car.vehicleId=="rally-sedan")yield return Capture("tuning-awd-geared-lock");Check(game.DraftDirty&&Math.Abs(ratioPreview.Awd.LockedRatio-1.5)<.00001&&!ratioPreview.Inspection.warnings.Any(w=>w.title=="AWD tire rolling sizes differ"),"Ratio tuning previews matched rolling sizes without saving or hiding other warnings: "+car.vehicleId+" / dirty "+game.DraftDirty+" / draft "+game.DraftValues.awd?.centerSpeedRatio+" / preview "+ratioPreview.Awd.LockedRatio+" / "+string.Join("; ",ratioPreview.Inspection.warnings.Select(w=>w.title)));
                    }
                    game.BeginTuneDraft();shell.Refresh();float stored=game.DraftValues.tirePressurePsi;
                    SharedUnitSettings.Set(speed,PressureUnit.Psi,TorqueUnit.PoundFeet);shell.Refresh();yield return new WaitForSecondsRealtime(.1f);
                    Check(Math.Abs(shell.Root.Q<FloatField>("tuneNumber:pressure").value-stored)<.001&&game.DraftValues.tirePressurePsi==stored&&!game.DraftDirty,"Changing display units preserves physical tune values: "+vehicle.spec.id);
                    SharedUnitSettings.Set(speed,PressureUnit.Bar,torque);shell.Refresh();yield return new WaitForSecondsRealtime(.1f);
                    Check(Math.Abs(shell.Root.Q<FloatField>("tuneNumber:pressure").value-stored/DisplayUnits.PsiPerBar)<.001&&!game.DraftDirty,"Bar conversion is silent and physically equivalent: "+vehicle.spec.id);
                    SharedUnitSettings.Set(speed,PressureUnit.Kilopascals,torque);shell.Refresh();yield return new WaitForSecondsRealtime(.1f);
                    Check(Math.Abs(shell.Root.Q<FloatField>("tuneNumber:pressure").value-stored/DisplayUnits.PsiPerBar*100)<.001&&!game.DraftDirty,"kPa conversion retains the same physical pressure: "+vehicle.spec.id);
                }
                Check(File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"Hardware matrix never saves its experimental purchases or tunes");
            }finally{SharedUnitSettings.Set(speed,pressure,torque);field.SetValue(game,original);game.BeginTuneDraft();shell.Refresh();}
            yield return new WaitForSecondsRealtime(.2f);
        }
#endif
    }
}
