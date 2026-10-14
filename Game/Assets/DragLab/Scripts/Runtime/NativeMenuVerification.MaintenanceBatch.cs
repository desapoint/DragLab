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
        void CheckReplacementControlBounds(string size)
        {
            var toggles=shell.Root.Query<Toggle>().ToList().Where(t=>t.name.StartsWith("maintenanceTarget:")).ToArray();
            Check(toggles.Length>0&&toggles.All(t=>Math.Abs(t.resolvedStyle.width-56)<.1f&&Math.Abs(t.Q<SwitchSurface>().worldBound.width-56)<.1f&&Math.Abs(t.Q<SwitchSurface>().worldBound.height-28)<.1f&&t.parent.ClassListContains("setting-row")&&t.parent.Q(className:"setting-caption").worldBound.xMax<=t.worldBound.xMin+1),"Replacement switches retain shared 56×28 geometry beside captions at "+size);
            var range=shell.Root.Q<NativeTuningRange>("tuneRange:maintenanceThreshold");
            if(range==null)return;
            var track=range.Slider.Q(className:"unity-base-slider__tracker").worldBound;var knob=range.Slider.Q(className:"unity-base-slider__dragger").worldBound;
            Check(Math.Abs(track.height-6)<.1f&&Math.Abs(knob.height-18)<.1f&&Math.Abs(track.center.y-knob.center.y)<1&&range.Slider.ClassListContains("setting-slider")&&range.Slider.Q<SliderFill>()!=null,"Threshold uses the shared thin track, centered knob and fill at "+size);
        }
        IEnumerator MaintenanceBatchChecks(WindowsKeyboardProbe input)
        {
            var p=game.Profile;p.cash=1000000;p.reputation=1000;string id=p.SelectedCar.instanceId;var model=game.vehicles.First(v=>v.spec.id==p.SelectedCar.vehicleId);
            VehicleCollection.Buy(p,p.catalog,new VehicleOffer{model=model.spec.id,price=1,reputation=0,stock=model.CreateStockTune(),paint="#FFFFFF"});
            var car=p.cars.Find(c=>c.instanceId==id);PartInstance Part(PartCategory category)=>p.parts.Find(i=>i.instanceId==car.installed.slots.First(s=>s.slot==category.ToString()).partInstanceId);
            var pistons=Part(PartCategory.Pistons);pistons.condition.elements[0].health=.1f;pistons.condition.elements[1].health=.15f;pistons.condition.elements[2].health=.9f;pistons.condition.elements[2].fault=ElementFault.Disabled;EngineElementService.RefreshAggregate(pistons.condition);
            var cooling=Part(PartCategory.Cooling);cooling.condition.health=.1f;cooling.condition.leakSeverity=.2;
            var brakes=Part(PartCategory.Brakes);brakes.condition.failed=true;brakes.condition.failureReason="Batch UI fixture";
            foreach(var stock in p.inventory.market.parts)stock.units=0;p.revision++;p=EngineWorkshop.Copy(p);
            typeof(PrototypeGame).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,p);Check(ProfileStore.TrySave(p),"Batch probe has a durable isolated profile");
            car=p.cars.Find(c=>c.instanceId==id);pistons=Part(PartCategory.Pistons);cooling=Part(PartCategory.Cooling);brakes=Part(PartCategory.Brakes);string selectedCar=p.selectedOwnedVehicleId;
            InterfaceComparison.SetReplacement("Garage",true);game.QueueNativeNavigation("Garage");yield return new WaitForSecondsRealtime(.25f);shell.InvalidateOwnedGrid();shell.Refresh();if(shell.OwnedDetailsOpen)yield return Click(input,"ownedBack");yield return Click(input,"ownedCar:"+id);
            Check(shell.Root.Q("maintenanceReplaceBroken").enabledInHierarchy&&shell.Root.Q("maintenanceReplaceWorn").enabledInHierarchy,"Inspected car exposes separate broken and under-15% actions");
            string before=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            yield return MaintenanceClick(input,"maintenanceReplaceWorn");var range=shell.Root.Q<NativeTuningRange>("tuneRange:maintenanceThreshold");
            string wornKey=pistons.instanceId+"/"+pistons.condition.elements[0].id,boundaryKey=pistons.instanceId+"/"+pistons.condition.elements[1].id;
            Check(range.Number.value==15&&range.Step==1&&range.FineStep==.1f,"Threshold defaults to 15% remaining condition with shared precise gestures");
            Check(shell.Root.Q("maintenanceTarget:"+wornKey)!=null&&shell.Root.Q("maintenanceTarget:"+boundaryKey)==null,"Default list identifies actual worn positions and excludes exactly 15%");
            Check(shell.Root.Q<Label>("maintenanceReplacementDelivery").text.Contains("Fee")&&shell.Root.Q<Label>("maintenanceReplacementCost").text.Contains("Balance after"),"Review shows cumulative express fee, total and balance before payment");
            range.Number.value=15.1f;Check(range.Slider.value==15.1f&&shell.Root.Q("maintenanceTarget:"+boundaryKey)!=null,"Precise threshold edits synchronize the slider and eligible positions");
            var track=range.Slider.Q(className:"unity-base-slider__tracker").worldBound;
            input.MouseButtonAt((int)Mathf.Lerp(track.xMin,track.xMax,.4f),(int)track.center.y,true);yield return new WaitForSecondsRealtime(.1f);input.MouseButtonAt((int)Mathf.Lerp(track.xMin,track.xMax,.4f),(int)track.center.y,false);yield return new WaitForSecondsRealtime(.1f);
            Check(range.Number.value>30&&range.Number.value<50&&Math.Abs(range.Number.value-Mathf.Round(range.Number.value))<.001,"Real left-drag uses whole-percent steps");float coarse=range.Number.value;
            input.MouseButtonAt((int)track.center.x,(int)track.center.y,true,1);yield return new WaitForSecondsRealtime(.1f);input.MoveMouseAt((int)track.center.x+35,(int)track.center.y);yield return new WaitForSecondsRealtime(.1f);input.MouseButtonAt((int)track.center.x+35,(int)track.center.y,false,1);yield return new WaitForSecondsRealtime(.1f);
            Check(range.Number.value>coarse&&range.Number.value<coarse+2,"Real right-drag provides finer threshold control");
            input.MouseButtonAt((int)track.center.x,(int)track.center.y,true,2);yield return new WaitForSecondsRealtime(.1f);input.MouseButtonAt((int)track.center.x,(int)track.center.y,false,2);yield return new WaitForSecondsRealtime(.15f);
            Check(range.Number.value==15&&shell.Root.Q("maintenanceTarget:"+boundaryKey)==null,"Middle-click restores the 15% default and updates the list");yield return null;CheckReplacementControlBounds("720p");yield return Capture("maintenance-batch-worn");
            Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.3f);Check(shell.Root.Q("maintenanceReplacementReview").worldBound.yMax<=shell.Root.Q("modalActions").worldBound.yMin,"Replacement total stays above fixed actions at 1080p");CheckReplacementControlBounds("1080p");yield return Capture("maintenance-batch-1080p");
            var target=new RenderTexture(3840,2160,24);target.Create();shell.Panel.targetTexture=target;shell.Refresh();yield return new WaitForSecondsRealtime(.3f);Check(shell.Panel.scale==2&&shell.Root.Q("modalActions").worldBound.yMax<=shell.Root.worldBound.yMax,"Replacement review remains bounded at crisp 4K scale");CheckReplacementControlBounds("4K");var prior=RenderTexture.active;RenderTexture.active=target;var screenshot=new Texture2D(3840,2160,TextureFormat.RGB24,false);screenshot.ReadPixels(new Rect(0,0,3840,2160),0,0);screenshot.Apply();File.WriteAllBytes(Path.Combine(folder,"maintenance-batch-4k.png"),screenshot.EncodeToPNG());RenderTexture.active=prior;shell.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(screenshot);
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);yield return Click(input,"modalClose");
            Check(!shell.ModalOpen&&before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Threshold gestures, selection previews, resizing and X never apply or save replacements");
            yield return MaintenanceClick(input,"maintenanceReplaceBroken");Check(shell.Root.Q("tuneRange:maintenanceThreshold")==null&&shell.Root.Query<Toggle>().ToList().Count(t=>t.name.StartsWith("maintenanceTarget:"))==2,"Broken mode lists the failed brake set and defective cylinder without a threshold slider");CheckReplacementControlBounds("broken mode");yield return Capture("maintenance-batch-broken");
            var broken=MaintenanceReplacements.Preview(game.Profile,id,true);int money=game.Profile.cash;string fault=pistons.condition.elements[2].id;yield return Click(input,"dialogConfirm");
            Check(!shell.ModalOpen&&game.Profile.cash==money-broken.cost&&game.Profile.selectedOwnedVehicleId==selectedCar&&game.Profile.parts.Find(i=>i.instanceId==brakes.instanceId).location==PartLocation.Inventory,"Broken confirmation pays once, retains the failed set and preserves current-car selection");
            Check(game.Profile.looseEngineElements.Any(u=>u.element.id==fault&&u.element.fault==ElementFault.Disabled)&&game.Profile.parts.Find(i=>i.instanceId==pistons.instanceId).condition.elements[0].health==.1f,"Broken replacement keeps the original fault and leaves merely worn cylinders alone");
            yield return MaintenanceClick(input,"maintenanceReplaceWorn");var coolingToggle=shell.Root.Q<Toggle>("maintenanceTarget:"+cooling.instanceId);shell.Root.Q<ScrollView>("modalContent").ScrollTo(coolingToggle);yield return new WaitForSecondsRealtime(.15f);yield return Click(input,coolingToggle);
            Check(!coolingToggle.value&&shell.Root.Q<Label>("maintenanceReplacementSummary").text.StartsWith("1 unit"),"Individual deselection updates the reviewed purchase rather than replacing everything");
            var one=MaintenanceReplacements.Preview(game.Profile,id,false,15,new[]{wornKey});before=JsonUtility.ToJson(game.Profile);disk=File.ReadAllBytes(ProfileStore.FilePath);money=game.Profile.cash;
            using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)){yield return Click(input,"dialogConfirm");Check(shell.ModalOpen&&shell.Root.Q<Label>("maintenanceReplacementError").text.Length>0&&before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Failed batch save preserves exact selection, stock, cash and all components");}
            yield return Click(input,"dialogConfirm");Check(!shell.ModalOpen&&game.Profile.cash==money-one.cost&&game.Profile.parts.Find(i=>i.instanceId==cooling.instanceId).condition.health==.1f&&game.Profile.parts.Find(i=>i.instanceId==cooling.instanceId).condition.leakSeverity==.2,"Retry commits only the checked position and keeps excluded part wear/leaks");
            Check(game.Profile.looseEngineElements.Any(u=>u.element.id==pistons.condition.elements[0].id&&u.element.health==.1f)&&MechanicsOwnership.Validate(game.Profile,game.Profile.catalog)==null,"Worn original remains reusable/sellable with valid exclusive ownership");yield return Capture("maintenance-batch-after");
            Check(!shell.Root.Q("maintenanceReplaceBroken").enabledInHierarchy,"Broken shortcut disables when no failed or defective components remain");
            yield return MaintenanceClick(input,"maintenanceReplaceWorn");shell.Root.Q<NativeTuningRange>("tuneRange:maintenanceThreshold").Number.value=0;Check(!shell.Root.Q("dialogConfirm").enabledInHierarchy&&shell.Root.Q<Label>("maintenanceReplacementSummary").text.StartsWith("0 units"),"An empty threshold selection cannot create a charge or save");yield return Click(input,"dialogCancel");
            string state=JsonUtility.ToJson(game.Profile);Check(!game.CommitNativeReplacements(id,game.Profile.revision-1,state,false,15,new[]{cooling.instanceId},1)&&state==JsonUtility.ToJson(game.Profile),"Stale batch confirmation rejects before any mutation");
        }
#endif
    }
}
