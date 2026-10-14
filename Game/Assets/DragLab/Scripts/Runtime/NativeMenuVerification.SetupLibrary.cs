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
        IEnumerator SetupStatClick(WindowsKeyboardProbe input,string id)
        {var target=shell.Root.Q<NativeStatsPanel>("setupLibraryStats").Q(id);shell.Root.Q<ScrollView>("setupLibraryPreview").ScrollTo(target);yield return new WaitForSecondsRealtime(.15f);yield return Click(input,target);}
        void SetupLibraryBounds()
        {
            var page=shell.Root.Q("setupLibraryPage").worldBound;var columns=shell.Root.Q("setupLibraryColumns").worldBound;
            Check(columns.width>0&&columns.yMax<=page.yMax&&columns.xMax<=page.xMax,"Setup list/comparison columns fit the viewport at "+Screen.width+" / "+shell.Panel.scale);
            Check(shell.Root.Q<ScrollView>("setupLibraryList").worldBound.xMax<shell.Root.Q("setupLibraryPreview").worldBound.xMin,"Setup list and preview remain separate anchored columns");
        }
        IEnumerator SetupLibraryChecks(WindowsKeyboardProbe input)
        {
            NativeSetupPreviewChecks.Run(game.vehicles.ToArray(),Check);
            var original=game.Profile;var field=typeof(PrototypeGame).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            var p=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(original));MechanicsOwnership.Ensure(p,original.catalog);p.savedSetups.Clear();p.cash=1000000;p.reputation=1000;var car=p.SelectedCar;string carId=car.instanceId;
            p.componentsPart(PartCategory.HeadIntake).condition.health=.65f;car.fuel.litres=7;car.fluids.oilLitres=2;car.fluids.coolantLitres=3;
            string engine=SavedSetups.Save(p,carId,"Sprint engine",SetupScope.Engine);SavedSetups.Save(p,carId,"Road build",SetupScope.WholeCar);SavedSetups.Save(p,carId,"Wet tune",SetupScope.TuneOnly);SavedSetups.Save(p,carId,"Wet tune",SetupScope.TuneOnly);
            var oem=p.componentsPart(PartCategory.HeadIntake);string upgraded=PartOperations.Buy(p,p.catalog,carId,p.catalog.Id(car.vehicleId,PartCategory.HeadIntake,1),true);string track=SavedSetups.Save(p,carId,"Track build",SetupScope.WholeCar);PartOperations.Install(p,p.catalog,carId,oem.instanceId);PartOperations.Sell(p,p.catalog,upgraded);
            var other=game.vehicles.First(v=>v.spec.id!=car.vehicleId);VehicleCollection.Buy(p,p.catalog,new VehicleOffer{model=other.spec.id,price=1,reputation=0,stock=other.CreateStockTune(),paint="#FFFFFF"});string wrong=SavedSetups.Save(p,p.SelectedCar.instanceId,"Other platform",SetupScope.WholeCar);VehicleCollection.Select(p,carId);
            field.SetValue(game,p);game.BeginTuneDraft();game.DraftValues.driverMassKg+=12;shell.Refresh();yield return new WaitForSecondsRealtime(.2f);
            string before=JsonUtility.ToJson(p);var draft=game.DraftValues;
            try{
                Check(shell.Root.Q("tuningLoadSetup").Q<FilledMenuIcon>()!=null,"Load setup reuses a filled-icon shared action");yield return Click(input,"tuningLoadSetup");
                Check(shell.SetupLibraryOpen&&shell.Root.Q("tuningPage").ClassListContains("hidden")&&!shell.Root.Q("setupLibraryPage").ClassListContains("hidden"),"Load setup replaces Tuning content with the saved-setup library");
                Check(shell.Root.Q("setupLibraryGrid").Children().Count()==6&&shell.Root.Q<Label>("setupLibraryCount").text=="6 / 6","Saved scopes, platforms and duplicate names retain distinct cards");SetupLibraryBounds();yield return Capture("setup-library-list");
                yield return Click(input,"setupLibraryScope:Engine + controls");Check(shell.Root.Q<Label>("setupLibraryCount").text=="1 / 6","Scope filter distinguishes engine configurations");
                yield return Click(input,"setupLibraryScope:Tune only");Check(shell.Root.Q<Label>("setupLibraryCount").text=="2 / 6","Scope filter retains independent same-name tuning setups");
                shell.Root.Q<TextField>("setupLibrarySearch").value="not found";Check(!shell.Root.Q("setupLibraryEmpty").ClassListContains("hidden"),"Search has an honest no-matches state");yield return Capture("setup-library-no-match");shell.Root.Q<TextField>("setupLibrarySearch").value="";yield return Click(input,"setupLibraryScope:All");
                shell.Root.Q<ScrollView>("setupLibraryList").ScrollTo(shell.Root.Q("setupCard:"+track));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"setupCard:"+track);
                var panel=shell.Root.Q<NativeStatsPanel>("setupLibraryStats");var plot=shell.Root.Q<NativeDynoPlot>("setupLibraryDyno");
                Check(panel!=null&&plot.Series.Count==3&&ReferenceEquals(plot.Series[0].Inspection,panel.Current.Inspection)&&ReferenceEquals(plot.Series[1].Inspection,panel.Proposed.Inspection),"Setup dyno and full stats share exact Current / Saved snapshots");
                Check(shell.Root.Q<Label>("setupPlanSummary").text.Contains("1 missing")&&shell.Root.Q<Label>("setupPlanPart:HeadIntake").text.Contains(DisplayUnits.Money(p.catalog.Find(p.catalog.Id(car.vehicleId,PartCategory.HeadIntake,1)).price)),"Missing hardware is named with its estimated actual set price");
                Check(panel.Proposed.Inspection.source.startingFuel.litres==7&&panel.Proposed.Inspection.source.startingFluids.oilLitres==2&&panel.Current.Inspection.source.components.Any(i=>i.definitionId==oem.definitionId&&i.condition.health==.65f),"Library comparison retains current wear, fuel and liquids");yield return Capture("setup-library-preview");
                int captures=shell.SetupPreviewCaptureCount;yield return new WaitForSecondsRealtime(.5f);shell.Root.Q<TextField>("setupLibrarySearch").value="track";Check(shell.SetupPreviewCaptureCount==captures&&shell.Root.Q<Label>("setupLibraryCount").text=="1 / 6","Idle and list filtering do not recapture previews");shell.Root.Q<TextField>("setupLibrarySearch").value="";
                var bounds=plot.worldBound;input.MoveMouseAt((int)bounds.center.x,(int)bounds.center.y);yield return new WaitForSecondsRealtime(.2f);Check(plot.HoverRpm.HasValue,"Saved-setup dyno retains shared pointer point inspection");
                yield return SetupStatClick(input,"statInfo:tuningWeight");Check(shell.ModalOpen,"Setup summary information uses the shared dialog");yield return Click(input,"modalClose");
                yield return SetupStatClick(input,"statsGroup:Engine");Check(!panel.Q("statsBody:Engine").ClassListContains("hidden"),"Full setup stats use shared collapsible groups");yield return SetupStatClick(input,"statsDifferencesOnly");Check(panel.DifferencesOnly,"Saved-setup comparisons support Differences only");yield return Capture("setup-library-stats");
                shell.Root.Q<ScrollView>("setupLibraryPreview").ScrollTo(shell.Root.Q("setupLibraryWarnings"));yield return new WaitForSecondsRealtime(.2f);yield return Click(input,"setupLibraryWarnings");Check(shell.ModalOpen&&shell.Root.Q<ScrollView>("modalContent").childCount>0,"Setup warnings reuse the combined current/proposed dialog");yield return Capture("setup-library-warnings");yield return Click(input,"modalClose");
                yield return Key(input,0x77);yield return Key(input,0x77);Check(shell.SetupLibraryOpen&&ReferenceEquals(panel,shell.Root.Q<NativeStatsPanel>("setupLibraryStats"))&&ReferenceEquals(draft,game.DraftValues)&&game.DraftDirty,"F8 retains library selection, comparison and the unapplied tuning draft");
                Screen.SetResolution(1024,768,false);yield return new WaitForSecondsRealtime(.4f);SetupLibraryBounds();yield return Capture("setup-library-compact");Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.4f);SetupLibraryBounds();yield return Capture("setup-library-1080p");
                var target=new RenderTexture(3840,2160,24);target.Create();shell.Panel.targetTexture=target;shell.Refresh();yield return new WaitForSecondsRealtime(.3f);SetupLibraryBounds();Check(shell.Panel.scale==2,"Setup library uses the shared crisp 4K scale");var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(3840,2160,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,3840,2160),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"setup-library-4k.png"),image.EncodeToPNG());RenderTexture.active=previous;shell.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(image);Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
                yield return Click(input,"setupLibraryBack");Check(!shell.SetupLibraryOpen&&ReferenceEquals(draft,game.DraftValues)&&game.DraftDirty,"Back restores the same editable tuning draft without applying a setup");yield return Click(input,"tuningRevert");yield return Click(input,"tuningLoadSetup");
                yield return Click(input,"settingsButton");yield return Click(input,"tabSound");shell.Root.Q<Slider>("masterVolume").value=.63f;yield return Click(input,"cancelSettings");Check(shell.SetupLibraryOpen&&shell.Root.Q<NativeStatsPanel>("setupLibraryStats")!=null,"Settings Cancel returns to the same saved-setup comparison");
                shell.Root.Q<ScrollView>("setupLibraryList").ScrollTo(shell.Root.Q("setupCard:"+wrong));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"setupCard:"+wrong);Check(shell.Root.Q("setupLibraryDyno")==null&&shell.Root.Q<Label>("setupPreviewUnavailable").text.Contains("another platform"),"Other-platform setups show a clear unavailable comparison instead of invented figures");yield return Capture("setup-library-other-platform");
                Check(before==JsonUtility.ToJson(p)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Every setup-library interaction is read-only for physical assets, resources, blueprints and saves");
                yield return SetupLoadInteractionChecks(input,track,engine);
                yield return Key(input,0x1B);Check(!shell.SetupLibraryOpen,"Escape returns from the library to Tuning");yield return Click(input,"tuningLoadSetup");shell.Root.Q<ScrollView>("setupLibraryList").ScrollTo(shell.Root.Q("setupCard:"+engine));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"setupCard:"+engine);
                var detached=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));MechanicsOwnership.Ensure(detached,p.catalog);AssemblyOperations.Detach(detached,detached.SelectedCar.instanceId);field.SetValue(game,detached);game.BeginTuneDraft();shell.Refresh();yield return new WaitForSecondsRealtime(.2f);Check(shell.Root.Q("setupLibraryDyno")==null&&shell.Root.Q<Label>("setupPreviewUnavailable").text.Contains("engine"),"Engine-less chassis has no fabricated setup dyno");yield return Capture("setup-library-no-engine");
                detached.savedSetups.Clear();detached.revision++;shell.Refresh();yield return new WaitForSecondsRealtime(.2f);Check(shell.Root.Q<Label>("setupLibraryEmpty").text=="No saved setups"&&shell.Root.Q("setupLibraryDyno")==null,"Empty setup library removes its obsolete selection/preview");yield return Capture("setup-library-empty");
                Check(shell.Root.Query<VisualElement>().ToList().All(e=>e.tabIndex==-1)&&errors==0,"Setup library keeps the no-Tab contract and has no runtime errors");
            }finally{
                if(shell.ModalOpen)typeof(NativeMenuShell).GetMethod("CloseDialog",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);
                typeof(NativeMenuShell).GetMethod("BackFromSetupLibrary",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);field.SetValue(game,original);game.settings=original.driving;game.BeginTuneDraft();shell.Refresh();
            }
        }
        IEnumerator SetupLoadInteractionChecks(WindowsKeyboardProbe input,string track,string engine)
        {
            var original=game.Profile;var field=typeof(PrototypeGame).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);byte[] backup=File.Exists(ProfileStore.FilePath+".bak")?File.ReadAllBytes(ProfileStore.FilePath+".bak"):null;
            try{
                shell.Root.Q<ScrollView>("setupLibraryList").ScrollTo(shell.Root.Q("setupCard:"+track));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"setupCard:"+track);
                var quote=NativeSetupLoadQuote.Capture(game.Profile,track);string before=JsonUtility.ToJson(game.Profile);
                game.DraftValues.driverMassKg+=1;shell.Refresh();yield return null;
                Check(!shell.Root.Q<Button>("setupLoad").enabledSelf&&!game.LoadNativeSetup(quote)&&game.DraftDirty&&before==JsonUtility.ToJson(game.Profile),"Setup loading preserves and blocks unapplied tuning rather than silently discarding it");game.BeginTuneDraft();shell.Refresh();yield return null;
                game.AddToCart(game.Profile.catalog.Id(game.Profile.SelectedCar.vehicleId,PartCategory.HeadIntake,1));shell.Refresh();yield return null;Check(!shell.Root.Q<Button>("setupLoad").enabledSelf&&!game.LoadNativeSetup(quote)&&game.CartCount==1,"A pending cart must be resolved before direct setup loading");game.ClearNativeCart();shell.Refresh();yield return null;
                Check(shell.Root.Q<Button>("setupLoad").enabledSelf&&shell.Root.Q("setupLoadActions").worldBound.yMax<=shell.Root.Q("setupLibraryPage").worldBound.yMax,"Load action remains available in a fixed footer beneath the scrolling comparison");
                yield return Click(input,"setupLoad");Check(shell.ModalOpen&&shell.Root.Q<Label>("setupLoadTotal").text.Contains(DisplayUnits.Money(quote.Cost))&&shell.Root.Q<NativeDynoPlot>("setupLoadDyno")!=null,"Buy/load review shows the quoted total, balance and shared resulting dyno");yield return Capture("setup-load-buy-review");yield return Click(input,"dialogCancel");Check(before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Cancelling setup load preserves assets, resources and durable bytes");
                yield return Click(input,"setupLoad");var info=shell.Root.Q<NativeStatsPanel>("setupLoadSummary").Q("statInfo:tuningWeight");shell.Root.Q<ScrollView>("modalContent").ScrollTo(info);yield return new WaitForSecondsRealtime(.15f);yield return Click(input,info);yield return Click(input,"modalClose");yield return new WaitForSecondsRealtime(.2f);Check(shell.ModalOpen&&shell.Root.Q("setupLoadConfirm")!=null,"Setup stat info returns to the same uncommitted load review");
                using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)){yield return Click(input,"setupLoadConfirm");Check(shell.ModalOpen&&shell.Root.Q<Label>("setupLoadError").text.Length>0&&before==JsonUtility.ToJson(game.Profile),"Failed setup save retains confirmation, cash, hardware and blueprint for retry");}
                long revision=game.Profile.revision;int cash=game.Profile.cash;string old=game.Profile.componentsPart(PartCategory.HeadIntake).instanceId;
                yield return Click(input,"setupLoadConfirm");Check(!shell.ModalOpen&&shell.SetupLibraryOpen&&game.Profile.revision==revision+1&&game.Profile.cash==cash-quote.Cost&&!game.DraftDirty,"Explicit Buy & load commits once and refreshes the selected setup comparison");
                Check(game.Profile.parts.Any(i=>i.instanceId==old&&i.location==PartLocation.Inventory&&i.condition.health==.65f)&&game.Profile.SelectedCar.fuel.litres==7&&game.Profile.SelectedCar.fluids.oilLitres==2&&game.Profile.SelectedCar.fluids.coolantLitres==3,"Loading retains the exact worn original and existing fuel/oil/coolant");
                string applied=JsonUtility.ToJson(game.Profile);Check(!game.LoadNativeSetup(quote)&&applied==JsonUtility.ToJson(game.Profile),"Replaying a confirmed native load cannot charge or install twice");Check(ProfileStore.Load().cash==game.Profile.cash&&ProfileStore.Load().SelectedCar.installed.slots.Any(slot=>game.Profile.parts.Any(i=>i.instanceId==slot.partInstanceId&&i.definitionId==game.Profile.catalog.Id(game.Profile.SelectedCar.vehicleId,PartCategory.HeadIntake,1))),"Confirmed missing-part load survives actual profile reload");
                shell.Root.Q<ScrollView>("setupLibraryList").ScrollTo(shell.Root.Q("setupCard:"+engine));yield return new WaitForSecondsRealtime(.15f);yield return Click(input,"setupCard:"+engine);yield return Click(input,"setupLoad");Check(shell.Root.Q<Label>("setupLoadTotal").text.Contains(DisplayUnits.Money(0)),"Owned-part load clearly quotes zero purchase cost");yield return Capture("setup-load-reuse-review");cash=game.Profile.cash;yield return Click(input,"setupLoadConfirm");Check(game.Profile.cash==cash&&game.Profile.componentsPart(PartCategory.HeadIntake).instanceId==old&&game.Profile.componentsPart(PartCategory.HeadIntake).condition.health==.65f,"Owned setup reuses the exact retained worn part without a new purchase or repair");
                yield return Click(input,"setupLoad");double fuel=game.Profile.SelectedCar.fuel.litres;game.Profile.SelectedCar.fuel.litres-=1;string changed=JsonUtility.ToJson(game.Profile);yield return Click(input,"setupLoadConfirm");Check(shell.ModalOpen&&shell.Root.Q<Label>("setupLoadError").text.Contains("changed")&&changed==JsonUtility.ToJson(game.Profile),"Unrevisioned car changes invalidate the reviewed setup without overwriting newer state");game.Profile.SelectedCar.fuel.litres=fuel;yield return Click(input,"dialogCancel");
                Screen.SetResolution(1024,768,false);yield return new WaitForSecondsRealtime(.4f);yield return Click(input,"setupLoad");Check(shell.Root.Q("setupLoadConfirm").worldBound.yMax<=shell.Root.worldBound.yMax&&shell.Root.Q<ScrollView>("modalContent").resolvedStyle.height>0,"Compact setup confirmation retains scrolling review and visible fixed actions");yield return Capture("setup-load-compact");yield return Click(input,"modalClose");Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);
            }finally{
                if(shell.ModalOpen)typeof(NativeMenuShell).GetMethod("CloseDialog",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);
                field.SetValue(game,original);game.settings=original.driving;game.ClearNativeCart();game.BeginTuneDraft();File.WriteAllBytes(ProfileStore.FilePath,disk);if(backup!=null)File.WriteAllBytes(ProfileStore.FilePath+".bak",backup);else if(File.Exists(ProfileStore.FilePath+".bak"))File.Delete(ProfileStore.FilePath+".bak");shell.Refresh();
            }
        }
#endif
    }
}
