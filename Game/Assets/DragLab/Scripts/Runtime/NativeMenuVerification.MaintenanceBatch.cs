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
            DialogScrollBounds("maintenance "+size);
            var toggles=shell.Root.Query<Toggle>().ToList().Where(t=>t.name.StartsWith("maintenanceTarget:")).ToArray();
            Check(toggles.Length>0&&toggles.All(t=>Math.Abs(t.resolvedStyle.width-56)<.1f&&Math.Abs(t.Q<SwitchSurface>().worldBound.width-56)<.1f&&Math.Abs(t.Q<SwitchSurface>().worldBound.height-28)<.1f&&t.parent.ClassListContains("setting-row")&&t.parent.Q(className:"setting-caption").worldBound.xMax<=t.worldBound.xMin+1),"Replacement switches retain shared 56×28 geometry beside captions at "+size);
            var range=shell.Root.Q<NativeTuningRange>("tuneRange:maintenanceThreshold");
            if(range==null)return;
            var track=range.Slider.Q(className:"unity-base-slider__tracker").worldBound;var knob=range.Slider.Q(className:"unity-base-slider__dragger").worldBound;
            Check(Math.Abs(track.height-6)<.1f&&Math.Abs(knob.height-18)<.1f&&Math.Abs(track.center.y-knob.center.y)<1&&range.Slider.ClassListContains("setting-slider")&&range.Slider.Q<SliderFill>()!=null,"Threshold uses the shared thin track, centered knob and fill at "+size);
        }
        IEnumerator MaintenanceBatchChecks(WindowsKeyboardProbe input)
        {
            game.PrepareNativeCart();game.ClearNativeCart();
            var p=game.Profile;p.cash=1000000;p.reputation=1000;string id=p.SelectedCar.instanceId;var model=game.vehicles.First(v=>v.spec.id==p.SelectedCar.vehicleId);
            VehicleCollection.Buy(p,p.catalog,new VehicleOffer{model=model.spec.id,price=1,reputation=0,stock=model.CreateStockTune(),paint="#FFFFFF"});
            var car=p.cars.Find(c=>c.instanceId==id);PartInstance Part(PartCategory category)=>p.parts.Find(i=>i.instanceId==car.installed.slots.First(s=>s.slot==category.ToString()).partInstanceId);
            var pistons=Part(PartCategory.Pistons);pistons.condition.elements[0].health=.1f;pistons.condition.elements[1].health=.15f;pistons.condition.elements[2].health=.9f;pistons.condition.elements[2].fault=ElementFault.Disabled;EngineElementService.RefreshAggregate(pistons.condition);
            var cooling=Part(PartCategory.Cooling);cooling.condition.health=.1f;cooling.condition.leakSeverity=.2;
            var brakes=Part(PartCategory.Brakes);brakes.condition.failed=true;brakes.condition.failureReason="Batch UI fixture";
            foreach(var stock in p.inventory.market.parts)stock.units=0;
            void Supply(string definition,int quantity){var stock=p.inventory.market.parts.Find(s=>s.definitions.Contains(definition));stock.units=quantity;stock.initialUnits=Math.Max(stock.initialUnits,quantity);}
            Supply(brakes.definitionId,2);Supply(pistons.definitionId,1);p.revision++;p=EngineWorkshop.Copy(p);
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
            Check(shell.Root.Q<Label>("maintenanceSupply:"+brakes.instanceId).text=="Express · 2/4"&&shell.Root.Q<Label>("maintenanceSupply:"+pistons.instanceId).text=="Express · 1/2"&&PartsMarket.Available(game.Profile,p.catalog.Find(brakes.definitionId))==2,"Partial-stock pills identify missing units out of each required set without consuming preview stock");
            range.Number.value=15.1f;Check(range.Slider.value==15.1f&&shell.Root.Q("maintenanceTarget:"+boundaryKey)!=null,"Precise threshold edits synchronize the slider and eligible positions");
            var track=range.Slider.Q(className:"unity-base-slider__tracker").worldBound;
            var list=shell.Root.Q("maintenanceReplacementTargets");var stable=list.Children().ToArray();var stableBounds=range.worldBound;var stableSummary=shell.Root.Q<Label>("maintenanceReplacementSummary").text;
            input.MouseButtonAt((int)Mathf.Lerp(track.xMin,track.xMax,.4f),(int)track.center.y,true);yield return new WaitForSecondsRealtime(.1f);
            input.MoveMouseAt((int)Mathf.Lerp(track.xMin,track.xMax,.45f),(int)track.center.y);yield return new WaitForSecondsRealtime(.1f);
            Check(range.Dragging&&list.Children().SequenceEqual(stable)&&range.worldBound==stableBounds&&shell.Root.Q<Label>("maintenanceReplacementSummary").text==stableSummary&&!shell.Root.Q("dialogConfirm").enabledInHierarchy,"Held threshold drag updates its value without replacing rows, moving the control or quoting an unfinished selection");
            input.MouseButtonAt((int)Mathf.Lerp(track.xMin,track.xMax,.45f),(int)track.center.y,false);yield return new WaitForSecondsRealtime(.1f);
            Check(range.Number.value>30&&range.Number.value<50&&Math.Abs(range.Number.value-Mathf.Round(range.Number.value))<.001,"Real left-drag uses whole-percent steps");float coarse=range.Number.value;
            stable=list.Children().ToArray();input.MouseButtonAt((int)track.center.x,(int)track.center.y,true,1);yield return new WaitForSecondsRealtime(.1f);input.MoveMouseAt((int)track.center.x+35,(int)track.center.y);yield return new WaitForSecondsRealtime(.1f);
            Check(range.Dragging&&list.Children().SequenceEqual(stable),"Fine right-drag also leaves existing replacement rows intact until release");
            input.MouseButtonAt((int)track.center.x+35,(int)track.center.y,false,1);yield return new WaitForSecondsRealtime(.1f);
            Check(range.Number.value>coarse&&range.Number.value<coarse+2,"Real right-drag provides finer threshold control");
            input.MouseButtonAt((int)track.center.x,(int)track.center.y,true,2);yield return new WaitForSecondsRealtime(.1f);input.MouseButtonAt((int)track.center.x,(int)track.center.y,false,2);yield return new WaitForSecondsRealtime(.15f);
            Check(range.Number.value==15&&shell.Root.Q("maintenanceTarget:"+boundaryKey)==null,"Middle-click restores the 15% default and updates the list");yield return null;CheckReplacementControlBounds("720p");yield return Capture("maintenance-batch-worn");
            range.Number.value=100;yield return new WaitForSecondsRealtime(.2f);CheckReplacementControlBounds("long list");yield return Capture("maintenance-batch-long-list");range.Number.value=15;yield return new WaitForSecondsRealtime(.2f);
            Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(.3f);Check(shell.Root.Q("maintenanceReplacementReview").worldBound.yMax<=shell.Root.Q("modalActions").worldBound.yMin,"Replacement total stays above fixed actions at 1080p");CheckReplacementControlBounds("1080p");yield return Capture("maintenance-batch-1080p");
            var target=new RenderTexture(3840,2160,24);target.Create();shell.Panel.targetTexture=target;shell.Refresh();yield return new WaitForSecondsRealtime(.3f);Check(shell.Panel.scale==2&&shell.Root.Q("modalActions").worldBound.yMax<=shell.Root.worldBound.yMax,"Replacement review remains bounded at crisp 4K scale");CheckReplacementControlBounds("4K");var prior=RenderTexture.active;RenderTexture.active=target;var screenshot=new Texture2D(3840,2160,TextureFormat.RGB24,false);screenshot.ReadPixels(new Rect(0,0,3840,2160),0,0);screenshot.Apply();File.WriteAllBytes(Path.Combine(folder,"maintenance-batch-4k.png"),screenshot.EncodeToPNG());RenderTexture.active=prior;shell.Panel.targetTexture=null;target.Release();Destroy(target);Destroy(screenshot);
            Screen.SetResolution(1280,720,false);yield return new WaitForSecondsRealtime(.3f);yield return Click(input,"modalClose");
            Check(!shell.ModalOpen&&before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Threshold gestures, selection previews, resizing and X never apply or save replacements");
            yield return MaintenanceClick(input,"maintenanceReplaceBroken");Check(shell.Root.Q("tuneRange:maintenanceThreshold")==null&&shell.Root.Query<Toggle>().ToList().Count(t=>t.name.StartsWith("maintenanceTarget:"))==2,"Broken mode lists the failed brake set and defective cylinder without a threshold slider");CheckReplacementControlBounds("broken mode");yield return Capture("maintenance-batch-broken");
            var broken=MaintenanceReplacements.Preview(game.Profile,id,true);int money=game.Profile.cash;string fault=pistons.condition.elements[2].id;
            Check(shell.Root.Q<Label>("maintenanceSupply:"+brakes.instanceId).text=="Express · 2/4"&&shell.Root.Q<Label>("maintenanceSupply:"+brakes.instanceId).tooltip.Contains("included")&&shell.Root.Q<Label>("maintenanceSupply:"+pistons.instanceId).text=="In stock · 1","Broken-only preview uses two stocked brakes plus two express brakes, and the stocked single piston");
            yield return Click(input,"dialogConfirm");
            Check(!shell.ModalOpen&&game.Profile.cash==money&&game.Profile.selectedOwnedVehicleId==selectedCar&&game.Profile.parts.Find(i=>i.instanceId==brakes.instanceId).condition.failed,"Add to cart keeps cash, failed hardware and current-car selection unchanged");
            Check(game.CartLines.All(l=>l.install)&&game.Profile.inventory.cart.carId==id&&ProfileStore.Load().inventory.cart.lines.Count==broken.lines.Count&&shell.Root.Q("maintenanceCart")!=null,"Exact broken replacements are reserved durably with install checked and a View cart action");
            Check(CartReservations.Held(game.Profile,p.catalog.Find(brakes.definitionId))==2&&CartReservations.Held(game.Profile,p.catalog.Find(pistons.definitionId))==1&&PartsMarket.Available(game.Profile,p.catalog.Find(brakes.definitionId))==0&&PartsMarket.Available(ProfileStore.Load(),p.catalog.Find(pistons.definitionId))==0,"Adding removes the stocked units from shop availability and persists those exact reservations");
            before=JsonUtility.ToJson(game.Profile);bool wrongOwner=false;
            try{game.PreviewNativeReplacements(selectedCar,new ReplacementPlan());}catch(InvalidOperationException e){wrongOwner=e.Message.Contains("another car");}
            Check(wrongOwner&&before==JsonUtility.ToJson(game.Profile),"Maintenance cannot replace a basket reserved for another owned car");
            int expires=game.Profile.inventory.cart.expiresRun;
            yield return MaintenanceClick(input,"maintenanceReplaceWorn");var coolingToggle=shell.Root.Q<Toggle>("maintenanceTarget:"+cooling.instanceId);shell.Root.Q<ScrollView>("modalContent").ScrollTo(coolingToggle);yield return new WaitForSecondsRealtime(.15f);yield return Click(input,coolingToggle);
            Check(!coolingToggle.value&&shell.Root.Q<Label>("maintenanceReplacementSummary").text.StartsWith("6 units"),"Deselection retains the existing brake/cylinder basket while excluding the worn radiator");
            Check(shell.Root.Q<Label>("maintenanceSupply:"+brakes.instanceId).text=="Express · 2/4"&&shell.Root.Q<Label>("maintenanceSupply:"+pistons.instanceId).text=="Express · 1/2","Merging reuses the reserved supply and marks only the additional piston for express");
            var expected=game.PreviewNativeReplacements(id,MaintenanceReplacements.Preview(game.Profile,id,false,15,new[]{wornKey,pistons.instanceId+"/"+fault,brakes.instanceId}));
            before=JsonUtility.ToJson(game.Profile);disk=File.ReadAllBytes(ProfileStore.FilePath);string oldCart=game.NativeReplacementCartSnapshot(id);
            using(var locked=new FileStream(ProfileStore.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read)){yield return Click(input,"dialogConfirm");Check(shell.ModalOpen&&shell.Root.Q<Label>("maintenanceReplacementError").text.Length>0&&before==JsonUtility.ToJson(game.Profile)&&oldCart==game.NativeReplacementCartSnapshot(id)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Failed staging save preserves exact rows, selection, stock, cash and components");}
            yield return Click(input,"dialogConfirm");
            Check(!shell.ModalOpen&&game.Profile.cash==money&&game.CartLines.Single(l=>l.action==CartAction.NewUnits).positions.Count==2&&game.CartLines.All(l=>l.definitionId!=cooling.definitionId),"Retry stages only checked targets and merges a shared piston row without duplicating the failed cylinder");
            var staged=game.Profile.inventory.cart;string rows=JsonUtility.ToJson(new PartCart.Lines{entries=staged.lines});
            yield return MaintenanceClick(input,"maintenanceReplaceWorn");coolingToggle=shell.Root.Q<Toggle>("maintenanceTarget:"+cooling.instanceId);shell.Root.Q<ScrollView>("modalContent").ScrollTo(coolingToggle);yield return new WaitForSecondsRealtime(.15f);yield return Click(input,coolingToggle);yield return Click(input,"dialogConfirm");
            Check(rows==JsonUtility.ToJson(new PartCart.Lines{entries=game.Profile.inventory.cart.lines})&&game.Profile.inventory.cart.expiresRun==expires,"Adding the same maintenance selection twice does not duplicate quantities or extend its reservation");
            game.Profile.cash=0;Check(ProfileStore.TrySave(game.Profile),"Underfunded staging fixture is durable");
            yield return MaintenanceClick(input,"maintenanceReplaceWorn");Check(shell.Root.Q("dialogConfirm").enabledInHierarchy&&shell.Root.Q<Label>("maintenanceReplacementError").text.Contains("checkout"),"Insufficient funds warn about checkout but do not prevent adding to cart");yield return Click(input,"dialogCancel");
            game.Profile.cash=money;Check(ProfileStore.TrySave(game.Profile),"Checkout funds restored in the isolated fixture");
            yield return MaintenanceClick(input,"maintenanceReplaceWorn");shell.Root.Q<NativeTuningRange>("tuneRange:maintenanceThreshold").Number.value=0;Check(shell.Root.Q("dialogConfirm").enabledInHierarchy&&shell.Root.Q<Label>("maintenanceReplacementSummary").text.StartsWith("5 units"),"A zero threshold still identifies broken targets");
            // Broken targets remain candidates at zero; deselect them to test an actually empty selection.
            foreach(var toggle in shell.Root.Query<Toggle>().ToList().Where(t=>t.name.StartsWith("maintenanceTarget:")).ToArray())toggle.value=false;
            Check(!shell.Root.Q("dialogConfirm").enabledInHierarchy&&shell.Root.Q<Label>("maintenanceReplacementSummary").text.StartsWith("0 units"),"Empty selection cannot stage a basket update");yield return Click(input,"dialogCancel");
            string state=JsonUtility.ToJson(game.Profile);Check(!game.StageNativeReplacements(id,game.Profile.revision-1,state,game.NativeReplacementCartSnapshot(id),false,15,new[]{cooling.instanceId})&&state==JsonUtility.ToJson(game.Profile),"Stale staging rejects before any mutation");
            InterfaceComparison.SetReplacement("Parts",true);yield return MaintenanceClick(input,"maintenanceCart");yield return new WaitForSecondsRealtime(.4f);
            Check(shell.CartPageOpen&&game.Profile.selectedOwnedVehicleId==id&&game.NativeCartTotal()==expected.cost,"View cart explicitly selects the serviced car and shows the same combined delivered price / open "+shell.CartPageOpen+" / route "+game.ScreenName+" / total "+game.NativeCartTotal()+" / expected "+expected.cost);yield return Capture("maintenance-batch-cart");
            yield return Click(input,"cartCheckout");yield return Click(input,"dialogCancel");Check(game.Profile.cash==money&&game.Profile.parts.Find(i=>i.instanceId==brakes.instanceId).condition.failed,"Cancel checkout keeps all replacements staged and the car untouched");
            yield return Click(input,"cartCheckout");yield return Click(input,"dialogConfirm");yield return new WaitForSecondsRealtime(.2f);
            Check(!shell.ModalOpen&&game.CartCount==0&&game.Profile.cash==money-expected.cost,"Shared checkout charges the reviewed express-inclusive basket exactly once");
            Check(game.Profile.inventory.cart==null&&game.Profile.inventory.market.parts.Single(s=>s.definitions.Contains(brakes.definitionId)).units==0&&game.Profile.inventory.market.parts.Single(s=>s.definitions.Contains(pistons.definitionId)).units==0&&PartsMarket.Available(game.Profile,p.catalog.Find(brakes.definitionId))==0,"Checkout consumes the reserved shelf quantities once; express units are delivered directly without replenishing the shop");
            Check(game.Profile.looseEngineElements.Any(u=>u.element.id==fault&&u.element.fault==ElementFault.Disabled)&&game.Profile.looseEngineElements.Any(u=>u.element.id==pistons.condition.elements[0].id&&u.element.health==.1f)&&game.Profile.parts.Find(i=>i.instanceId==brakes.instanceId).location==PartLocation.Inventory,"Checkout retains the old failed set and both original worn/faulty cylinders in inventory");
            Check(game.Profile.parts.Find(i=>i.instanceId==cooling.instanceId).condition.health==.1f&&game.Profile.parts.Find(i=>i.instanceId==cooling.instanceId).condition.leakSeverity==.2&&MechanicsOwnership.Validate(game.Profile,game.Profile.catalog)==null,"Excluded radiator keeps its condition and leak with valid exclusive ownership");
            game.QueueNativeNavigation("Garage");yield return new WaitForSecondsRealtime(.3f);shell.InvalidateOwnedGrid();shell.Refresh();if(!shell.OwnedDetailsOpen)yield return Click(input,"ownedCar:"+id);
            Check(!shell.Root.Q("maintenanceReplaceBroken").enabledInHierarchy,"Broken shortcut disables only after actual checkout repairs the car");yield return Capture("maintenance-batch-after");
        }
#endif
    }
}
