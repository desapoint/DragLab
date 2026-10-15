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
        IEnumerator FittedPartsChecks(WindowsKeyboardProbe input,VehicleOffer offer,string capture)
        {
            var control=shell.Root.Q<Button>("dealerFittedParts");
            Check(control.worldBound.width<200&&control.worldBound.width<control.parent.worldBound.width*.5f&&Math.Abs(control.worldBound.x-control.parent.worldBound.x)<2,"Fitted parts is a compact left-aligned action rather than a full-width fact row");
            Check(control.Q<FilledMenuIcon>()!=null&&!control.focusable&&control.tabIndex==-1,"Fitted parts reuses the filled-icon button with no keyboard traversal");
            yield return Capture(capture+"-control");
            string before=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);
            yield return Click(input,"dealerFittedParts");yield return new WaitForSecondsRealtime(.1f);
            var list=shell.Root.Q("fittedPartsList");var rows=list.Query(className:"fitted-part").ToList();
            Check(rows.Count==offer.parts.Count&&rows.Select(r=>r.name).Distinct().Count()==rows.Count&&offer.parts.All(p=>rows.Any(r=>r.name=="fittedPart:"+p.instanceId)),"Grouped fitted list shows every actual part exactly once with retained identity");
            Check(rows.All(r=>r.parent.name=="fittedPartsBody:"+NativeMenuShell.FittedPartGroup((PartCategory)r.userData)),"All part rows belong to their component system, including cooling, electronics and drivetrain");
            Check(offer.parts.All(p=>{var text=list.Q("fittedPart:"+p.instanceId).Q<Label>(className:"car-fact-value").text;return PrototypeGame.PartFailed(p)?text.StartsWith("Failed"):text==PrototypeGame.MaintenanceConditionHealth(p).ToString("P0");}),"Fitted values reflect actual member condition and failures instead of a neutral tire wrapper");
            Check(Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>().All(c=>NativeMenuShell.FittedPartGroup(c)!="Other"),"Every current component category has a named system, including diesel and AWD hardware");
            Check(new[]{"Engine","Cooling & lubrication","Drivetrain","Wheels & brakes"}.All(g=>list.Q("fittedPartsGroup:"+g)!=null),"Fitted-parts dialog has distinct engine, cooling, drivetrain and wheel/brake sections");
            Check(list.Query<Button>(className:"stat-group-toggle").ToList().All(b=>list.Q("fittedPartsBody:"+b.name.Substring("fittedPartsGroup:".Length)).ClassListContains("hidden")),"Dialog starts with a concise collapsed system overview");
            Check(rows.All(r=>r.Q<Button>("fittedPartShop:"+r.name.Substring("fittedPart:".Length)) is Button shop&&shop.Q<FilledMenuIcon>()!=null&&!shop.focusable&&shop.tabIndex==-1&&shop.enabledSelf==(game.Profile.SelectedCar!=null)),"Each fitted part has a shared Shop action, available after an owned car is selected");
            yield return Capture(capture+"-overview");yield return Click(input,"fittedPartsGroup:Engine");yield return new WaitForSecondsRealtime(.1f);
            Check(list.Q("fittedPartsBody:Engine").Children().All(r=>r.Query<Label>().ToList().All(l=>l.worldBound.xMax<=r.worldBound.xMax+2)),"Part names and failure values wrap within the dialog width");
            yield return Capture(capture+"-groups");
            yield return Click(input,"fittedPartsGroup:Engine");
            Check(list.Q("fittedPartsBody:Engine").ClassListContains("hidden")&&list.Q<Button>("fittedPartsGroup:Drivetrain").worldBound.yMax<shell.Root.Q("modalActions").worldBound.yMin,"Collapsing Engine restores access to the other system headers");
            yield return Capture(capture+"-collapsed");yield return Click(input,"fittedPartsGroup:Engine");
            Check(!list.Q("fittedPartsBody:Engine").ClassListContains("hidden")&&rows.Count==list.Query(className:"fitted-part").ToList().Count,"Reopening Engine retains all captured part rows");
            Check(before==JsonUtility.ToJson(game.Profile)&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"Opening and grouping fitted parts changes no profile assets, knowledge, cash or saved bytes");
        }
        IEnumerator UsedInspectionChecks(WindowsKeyboardProbe input)
        {
            var original=game.Profile;var field=typeof(PrototypeGame).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic);
            byte[] disk=File.ReadAllBytes(ProfileStore.FilePath),backup=File.Exists(ProfileStore.FilePath+".bak")?File.ReadAllBytes(ProfileStore.FilePath+".bak"):null;
            var fixture=DragLab.Domain.Mechanics.EngineWorkshop.Copy(original);fixture.catalog=original.catalog;fixture.cash=100000;fixture.reputation=1000;
            MechanicsOwnership.Ensure(fixture,fixture.catalog);
            var listing=fixture.vehicleStock.listings.First(o=>o.used);listing.inspected=false;listing.historyNotes.Clear();listing.historyNotes.Add("Inspection fixture overheating");
            var failed=listing.parts.Single(p=>p.slot=="Pistons");failed.condition.failed=true;failed.condition.health=.15f;failed.condition.failureReason="Inspection fixture seizure";foreach(var e in failed.condition.elements){e.health=0;e.fault=ElementFault.Seized;e.severity=1;}
            listing.parts.RemoveAll(p=>p.slot=="Interior");listing.fuel.litres=1.25;listing.fluids.oilLitres=.8;listing.fluids.coolantLitres=1.1;listing.reportedCondition=VehicleInspection.Overall(fixture.catalog,listing);VehicleInspection.Assess(fixture.catalog,listing);
            string id=listing.listingId,parts=JsonUtility.ToJson(listing.parts),liquids=JsonUtility.ToJson(listing.fluids),fuel=JsonUtility.ToJson(listing.fuel);
            try{
                field.SetValue(game,fixture);game.QueueNativeNavigation("Garage");yield return new WaitForSecondsRealtime(.3f);if(!shell.Visible)yield return Key(input,0x77);
                typeof(NativeMenuShell).GetField("dealerStockKey",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(shell,null);
                typeof(NativeMenuShell).GetField("dealerInventoryKey",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(shell,null);
                shell.InvalidateDealer();shell.Refresh();if(!shell.DealerPageOpen)yield return Click(input,"garageDealer");shell.ResetOwnedQuery();shell.Refresh();
                string before=JsonUtility.ToJson(fixture);int cash=fixture.cash;
                yield return DealerCardClick(input,id);
                var content=shell.Root.Q("dealerContent");var dyno=shell.Root.Q<NativeDynoPlot>("dealerDynoPlot");
                Check(dyno!=null&&dyno.Series.Count==2&&dyno.Series[0].Inspection.peakHp>0,"Uninspected used car retains estimated stats and two dyno curves");
                Check(content.Q("dealerStatsToggle")==null&&content.Q("dealerCarStats")==null&&content.Q("dealerFittedParts")==null&&content.Query(className:"car-reason").ToList().Count==0,"Uninspected view has no exact parts/settings or wheelspin/drivetrain warnings");
                Check(!content.Query<Label>().ToList().Any(l=>l.text.Contains("Inspection fixture")||l.text=="Fuel included"||l.text=="Oil / coolant included"),"History, causes and exact liquids cannot leak through basic text");
                Check(shell.Root.Q<Label>("dealerDisclosure:"+id).text.Contains("estimated")&&shell.Root.Q<Label>("dealerOperation:"+id).text.Contains("does not run"),"Estimate disclosure and true run/drive status are explicit");
                yield return Capture("used-uninspected");yield return Click(input,"dealerCompareCar");
                Check(shell.Root.Q("dealerCarStats")==null&&shell.Root.Q<Label>("compareSummary:HP")!=null&&content.Query(className:"car-reason").ToList().Count==0,"Comparison retains basic estimate/dyno but cannot bypass inspection");
                yield return Click(input,"dealerCompareCar");yield return Click(input,"dealerInspect");Check(shell.ModalOpen&&shell.Root.Q<Button>("dialogInspect")!=null,"Paid inspection uses the shared confirmation dialog");
                yield return Click(input,"dialogCancel");Check(before==JsonUtility.ToJson(game.Profile)&&File.ReadAllBytes(ProfileStore.FilePath).SequenceEqual(disk),"Cancelling inspection changes neither knowledge nor cash nor disk");
                yield return Key(input,0x77);game.OpenCollection();yield return new WaitForSecondsRealtime(.3f);
                Check(!shell.Visible&&game.partRects.ContainsKey("car-inspect:"+id),"Legacy/F8 dealership offers the same paid inspection instead of revealing hidden data");yield return Capture("used-uninspected-legacy");
                game.QueueNativeNavigation("Garage");yield return new WaitForSecondsRealtime(.4f);shell.Refresh();if(!InterfaceComparison.IsReplacement("Garage"))yield return Key(input,0x77);shell.Refresh();yield return new WaitForSecondsRealtime(.2f);
                yield return Capture("used-returned");var inspectButton=shell.Root.Q<Button>("dealerInspect");
                Check(shell.Visible&&shell.DealerDetailsOpen&&inspectButton.enabledInHierarchy&&inspectButton.worldBound.height>0,"Inspection remains visible/actionable after legacy / visible="+shell.Visible+" / details="+shell.DealerDetailsOpen+" / route="+game.ScreenName+" / enabled="+inspectButton.enabledInHierarchy+" / rect="+inspectButton.worldBound);
                yield return Click(input,"dealerInspect");Check(shell.ModalOpen,"Returning from legacy keeps native inspection actionable");yield return Click(input,"dialogInspect");yield return new WaitForSecondsRealtime(.3f);
                var current=VehicleListings.Find(game.Profile,id);content=shell.Root.Q("dealerContent");dyno=shell.Root.Q<NativeDynoPlot>("dealerDynoPlot");
                Check(!shell.ModalOpen&&current.inspected&&game.Profile.cash==cash-game.Costs.vehicleInspectionFee,"Confirm charges the configured fee exactly once and reveals this listing / modal="+shell.ModalOpen+" / known="+current.inspected+" / cash="+game.Profile.cash+" / fee="+game.Costs.vehicleInspectionFee+" / error="+shell.Root.Q<Label>("dealerInspectionError")?.text+" / notice="+game.NativeNotice);
                Check(game.Profile.vehicleStock.listings.Where(o=>o.used&&o.listingId!=id).All(o=>!o.inspected),"Inspecting one listing does not reveal other used cars");
                Check(dyno.Series[0].Inspection.peakHp==0&&content.Q("dealerStatsToggle")!=null&&!content.Q("dealerCarStats").ClassListContains("hidden")&&content.Query(className:"car-reason").ToList().Count>0,"Inspection replaces the estimate with actual failed output, full stats and warnings");
                Check(content.Query<Label>().ToList().Any(l=>l.text.Contains("Inspection fixture overheating"))&&shell.Root.Q("dealerInspect").ClassListContains("hidden"),"Paid history is visible and a second inspection charge is unavailable");
                Check(SnapshotEquality.Serialized(parts,current.parts)&&SnapshotEquality.Serialized(liquids,current.fluids)&&SnapshotEquality.Serialized(fuel,current.fuel),"Revealing information cannot repair/refill/reroll the car");
                var reloaded=ProfileStore.Load();Check(VehicleListings.Find(reloaded,id).inspected,"Inspection knowledge survives actual disk reload");
                double inspectedWeight=dyno.Series[0].Inspection.vehicleMassKg;yield return Capture("used-inspected");
                shell.Root.Q<ScrollView>("dealerDetailScroll").ScrollTo(content.Q("dealerFittedParts"));yield return new WaitForSecondsRealtime(.1f);yield return FittedPartsChecks(input,current,"used-fitted");
                Check(shell.Root.Q("modalContent").Query<Label>().ToList().Any(l=>l.text.Contains("Inspection fixture seizure")),"Fitted-parts report identifies the actual hidden failure cause");
                Check(shell.Root.Q("fittedPart:"+failed.instanceId).Query(className:"fitted-part-fault").ToList().Count==failed.condition.elements.Length,"Per-cylinder seized elements stay attached to their failed piston set");yield return Click(input,"dialogClose");
                yield return Click(input,"dealerBuy");yield return Click(input,"dialogConfirm");
                Check(shell.OwnedDetailsOpen&&game.Profile.parts.Single(p=>p.instanceId==failed.instanceId).condition.failed&&game.Profile.SelectedCar.usedHistory.Contains("Inspection fixture overheating"),"Buying an inspected broken car retains actual damage and history in the owned page");
                Check(SnapshotEquality.Serialized(liquids,game.Profile.SelectedCar.fluids)&&SnapshotEquality.Serialized(fuel,game.Profile.SelectedCar.fuel),"Purchase preserves the exact low liquids shown by inspection");
                Check(Math.Abs(shell.Root.Q<NativeDynoPlot>("ownedDynoPlot").Series[0].Inspection.vehicleMassKg-inspectedWeight)<.001,"Owned weight matches the inspected stripped car without invented trim");
                Check(shell.Root.Q("ownedHistory")!=null,"Owned history is available through a reusable info dialog");
                Check(MechanicsOwnership.Validate(game.Profile,game.Profile.catalog)==null&&errors==0,"Inspection commerce and owned broken-car state remain valid with no runtime errors");
                yield return Capture("used-owned");
            }finally{
                if(shell.ModalOpen)typeof(NativeMenuShell).GetMethod("CloseDialog",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);
                typeof(NativeMenuShell).GetMethod("SwitchGaragePage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,new object[]{false});
                if(shell.OwnedDetailsOpen)typeof(NativeMenuShell).GetMethod("BackToOwnedCars",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);
                field.SetValue(game,original);game.settings=original.driving;typeof(PrototypeGame).GetMethod("ShowGarageCar",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
                ProfileStore.Flush();SqliteStateStore.Invalidate();File.WriteAllBytes(ProfileStore.FilePath,disk);if(backup!=null)File.WriteAllBytes(ProfileStore.FilePath+".bak",backup);else if(File.Exists(ProfileStore.FilePath+".bak"))File.Delete(ProfileStore.FilePath+".bak");
                shell.InvalidateOwnedGrid();shell.InvalidateDealer();shell.Refresh();
            }
        }
#endif
    }
}
