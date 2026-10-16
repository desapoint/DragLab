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
        IEnumerator RebuildFlowChecks(WindowsKeyboardProbe input)
        {
            var p=game.Profile;p.cash=1000000;p.reputation=1000;var car=p.SelectedCar;string carId=car.instanceId;
            var pistons=p.parts.Single(i=>i.instanceId==car.installed.slots.First(s=>s.slot=="Pistons").partInstanceId);
            pistons.condition.health=.2f;foreach(var member in pistons.condition.elements)member.health=.2f;
            pistons.condition.elements[0].fault=ElementFault.Seized;string removedId=pistons.instanceId;
            p=EngineWorkshop.Copy(p);typeof(PrototypeGame).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,p);
            Check(ProfileStore.TrySave(p),"Rebuild UI probe has a durable isolated damaged-engine fixture");
            InterfaceComparison.SetReplacement("Garage",true);game.QueueNativeNavigation("Garage");yield return new WaitForSecondsRealtime(.3f);
            shell.InvalidateOwnedGrid();shell.Refresh();if(shell.OwnedDetailsOpen)yield return Click(input,"ownedBack");yield return Click(input,"ownedCar:"+carId);
            Check(shell.Root.Q("maintenanceRebuild").enabledInHierarchy,"Broken engine still exposes the rebuild action");
            string before=JsonUtility.ToJson(game.Profile);byte[] disk=File.ReadAllBytes(ProfileStore.FilePath);int cash=game.Profile.cash;
            yield return MaintenanceClick(input,"maintenanceRebuild");DialogScrollBounds("rebuild confirmation");
            int price=PaidEconomy.Preview(game.Profile,game.Profile.catalog,game.Costs,game.PricingPolicy,new PaymentRequest{kind=PaymentKind.Rebuild,carId=carId},DragLab.Domain.RuntimeEntropy.UtcNow.Ticks).cost;
            Check(shell.Root.Q("dialogConfirm").enabledInHierarchy&&shell.Root.Q<Label>("maintenanceCost").text=="Total "+DisplayUnits.Money(price)+" · Cash "+DisplayUnits.Money(cash),"Rebuild dialog shows the actual payable price and enabled confirmation");
            yield return Capture("rebuild-quote");yield return Click(input,"dialogCancel");
            Check(before==JsonUtility.ToJson(game.Profile)&&disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)),"Cancelling rebuild performs no payment or save");
            yield return MaintenanceClick(input,"maintenanceRebuild");yield return Click(input,"dialogConfirm");
            var job=game.Profile.SelectedCar.engineRebuild;
            Check(!shell.ModalOpen&&job!=null&&job.active&&game.Profile.cash==cash-price,"Confirmed rebuild charges once and creates the reserved job");
            Check(!shell.Root.Q("maintenanceRebuildCollect").enabledInHierarchy&&!shell.Root.Q("maintenanceFuel").enabledInHierarchy,"Waiting state disables premature collection and conflicting service");
            yield return Capture("rebuild-waiting");
            var loaded=ProfileStore.Load();Check(loaded.SelectedCar.engineRebuild.id==job.id&&loaded.SelectedCar.engineRebuild.readyUtcTicks==job.readyUtcTicks,"Paid rebuild deadline survives an actual SQLite reload");
            before=JsonUtility.ToJson(game.Profile);
            Check(!game.CommitNativeService(carId,game.Profile.revision,before,job.id)&&before==JsonUtility.ToJson(game.Profile),"Premature native collection rejects without publishing state");
            // Advance only this isolated fixture's shop clock; domain checks cover
            // the exact saved deadline boundary without a real multi-minute wait.
            job.startedUtcTicks=DragLab.Domain.RuntimeEntropy.UtcNow.AddHours(-1).Ticks;job.readyUtcTicks=DragLab.Domain.RuntimeEntropy.UtcNow.AddSeconds(-1).Ticks;
            Check(ProfileStore.TrySave(game.Profile),"Ready-state test clock is durable in the isolated save");
            shell.InvalidateOwnedGrid();shell.Refresh();yield return new WaitForSecondsRealtime(.2f);
            Check(shell.Root.Q("maintenanceRebuildCollect").enabledInHierarchy,"Ready job enables collection");
            yield return MaintenanceClick(input,"maintenanceRebuildCollect");yield return Capture("rebuild-ready");yield return Click(input,"dialogConfirm");
            Check(!shell.ModalOpen&&game.Profile.SelectedCar.engineRebuild==null&&game.Profile.cash==cash-price,"Collect completes the existing job without a second charge");
            var fitted=game.Profile.parts.Single(i=>i.instanceId==game.Profile.SelectedCar.installed.slots.First(s=>s.slot=="Pistons").partInstanceId);
            Check(fitted.instanceId!=removedId&&game.Profile.parts.Single(i=>i.instanceId==removedId).location==PartLocation.Inventory,"Completed UI rebuild retains old damaged parts and installs replacements");
            Check(ProfileStore.Load().SelectedCar.engineRebuild==null,"Collected rebuild remains complete after SQLite reload");
            yield return Capture("rebuild-collected");
        }
#endif
    }
}
