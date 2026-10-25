using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    /// <summary>Advancing-player checks for the S06c manual refill dialog.</summary>
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        PlayerProfile ReloadVerifiedNativeProfile()
        {
            var loaded=ProfileStore.Load();
            var profileField=typeof(PrototypeGame).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic);
            if(profileField==null)throw new InvalidOperationException("Could not install the official isolated-profile reload in native verification.");
            profileField.SetValue(game,loaded);game.settings=loaded.driving;
            return loaded;
        }

        bool PersistNativeFluidCapacityFixture(string carId,out string error)
        {
            error=null;
            var current=game.Profile;
            var car=current?.cars?.Find(c=>c.instanceId==carId);
            if(car==null||car.fluids==null){error="The selected owned car has no fluid state.";return false;}
            bool saved=MechanicsTransactions.Try(current,current.catalog,current.revision,p=>
            {
                var owned=p.cars.Find(c=>c.instanceId==carId);
                if(owned==null||owned.fluids==null)throw new InvalidOperationException("Native refill fixture car is missing.");
                var capacities=p.catalog.Spec(owned.vehicleId).fluids;
                owned.fluids.oilLitres=Math.Max(0,capacities.oilCapacityLitres-.5);
                owned.fluids.coolantLitres=Math.Max(0,capacities.coolantCapacityLitres-.5);
            },ProfileStore.TrySave,out _,out error);
            if(!saved)return false;
            var loaded=ReloadVerifiedNativeProfile();
            var restored=loaded.cars.Find(c=>c.instanceId==carId);
            if(restored==null||restored.instanceId!=loaded.selectedOwnedVehicleId||restored.fluids==null)
            {error="Persisted native refill fixture did not reload as the selected car.";return false;}
            return true;
        }

        IEnumerator RefreshTypedOwnedCar(WindowsKeyboardProbe input,string carId)
        {
            if(shell.OwnedDetailsOpen)
                typeof(NativeMenuShell).GetMethod("BackToOwnedCars",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(shell,null);
            shell.InvalidateOwnedGrid();shell.Refresh();yield return new WaitForSecondsRealtime(.2f);
            yield return Click(input,"ownedCar:"+carId);
        }
        IEnumerator TypedRefillMaintenanceClick(WindowsKeyboardProbe input,string id)
        {
            if(!shell.ModalOpen){shell.Root.Q<ScrollView>("ownedDetailScroll").ScrollTo(shell.Root.Q(id));yield return new WaitForSecondsRealtime(.15f);}
            yield return Click(input,id);
        }

        bool TypedRefillActionCaptionVisible(string id,string text)
        {
            var button=shell.Root.Q<Button>(id);var caption=button?.Q<Label>();
            if(button==null||caption==null||caption.text!=text||!string.IsNullOrEmpty(button.text)||
                caption.resolvedStyle.display==DisplayStyle.None)return false;
            var bounds=caption.worldBound;
            return bounds.width>0&&bounds.height>0;
        }

        IEnumerator TypedRefillLegacyGuiClick(WindowsKeyboardProbe input,string id)
        {
            for(int i=0;i<8&&!game.partRects.ContainsKey(id);i++)yield return new WaitForEndOfFrame();
            game.ScrollToPart(id);yield return new WaitForSecondsRealtime(.12f);yield return new WaitForEndOfFrame();
            if(!game.partRects.TryGetValue(id,out var rect)||rect.width<=0||rect.height<=0)
                throw new InvalidOperationException("F8 fluid action did not expose its legacy player control: "+id);
            Check(rect.center.y>=125&&rect.center.y<Screen.height-70,"F8 refill control is visible before the real click: "+id);
            input.MouseButtonAt((int)rect.center.x,(int)rect.center.y,true);yield return null;
            yield return new WaitForSecondsRealtime(.08f);input.MouseButtonAt((int)rect.center.x,(int)rect.center.y,false);
            yield return new WaitForSecondsRealtime(.2f);
        }

        IEnumerator TypedF8FluidRefillChecks(WindowsKeyboardProbe input)
        {
            bool restoreNative=shell.Visible;
            string carId=game.Profile.SelectedCar.instanceId;
            FramePacingProbe.OverridePhase="typed-refill/f8-capacity-fixture";
            Check(PersistNativeFluidCapacityFixture(carId,out var fixtureError),"F8 oil/coolant capacities are prepared through a persisted and reloaded owned-car fixture: "+fixtureError);
            if(restoreNative)yield return Key(input,0x77);
            Check(!shell.Visible&&game.ScreenName=="Garage","F8 shows the legacy manual Garage actions on desktop");
            int cash=game.Profile.cash;double oil=game.Profile.SelectedCar.fluids.oilLitres;
            var entered=new ManualResetEventSlim(false);var release=new ManualResetEventSlim(false);var workerDone=new ManualResetEventSlim(false);
            var prior=SqliteStateStore.FailureInjection;
            SqliteStateStore.FailureInjection=label=>
            {
                if(label==null||!label.EndsWith(":before-commit",StringComparison.Ordinal))return;
                entered.Set();try{if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("F8 refill writer release expired.");}
                finally{workerDone.Set();}
            };
            try
            {
                FramePacingProbe.OverridePhase="typed-refill/f8-oil-pending";
                yield return TypedRefillLegacyGuiClick(input,"oil-topup");
                float enterDeadline=Time.realtimeSinceStartup+5;
                while(!entered.IsSet&&Time.realtimeSinceStartup<enterDeadline)yield return null;
                Check(entered.IsSet,"F8 oil top-up button dispatches the shared typed pilot");
                Check(game.ManualRefillPending&&game.Profile.cash==cash&&game.Profile.SelectedCar.fluids.oilLitres==oil,
                    "F8 manual action shares typed pending state and does not mutate before durability");
            }
            finally { release.Set();SqliteStateStore.FailureInjection=prior; }
            FramePacingProbe.OverridePhase="typed-refill/f8-oil-publication";
            float workerDeadline=Time.realtimeSinceStartup+8;
            while(!workerDone.IsSet&&Time.realtimeSinceStartup<workerDeadline)yield return null;
            Check(workerDone.IsSet,"F8 SQLite worker exits the held-write callback before fixture cleanup");
            entered.Dispose();release.Dispose();workerDone.Dispose();
            float deadline=Time.realtimeSinceStartup+8;
            while(game.ManualRefillPending&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!game.ManualRefillPending&&game.ManualRefillLastSucceeded&&game.Profile.cash<cash&&
                game.Profile.SelectedCar.fluids.oilLitres>oil,"F8 refill publishes through the same one-effect owner path");
            FramePacingProbe.OverridePhase="typed-refill/f8-coolant";
            int coolantCash=game.Profile.cash;long completion=game.ManualRefillCompletion;
            double coolantBefore=game.Profile.SelectedCar.fluids.coolantLitres,oilAfter=game.Profile.SelectedCar.fluids.oilLitres;
            yield return TypedRefillLegacyGuiClick(input,"coolant-topup");
            float coolantDeadline=Time.realtimeSinceStartup+10;
            while(game.ManualRefillCompletion==completion&&Time.realtimeSinceStartup<coolantDeadline)yield return null;
            Check(game.ManualRefillCompletion>completion&&game.ManualRefillLastSucceeded&&
                game.Profile.cash<coolantCash&&game.Profile.SelectedCar.fluids.coolantLitres>coolantBefore&&
                game.Profile.SelectedCar.fluids.oilLitres==oilAfter,
                "F8 selected-coolant refill publishes coolant and cash while preserving oil");

            FramePacingProbe.OverridePhase="typed-refill/f8-combined-fixture";
            Check(PersistNativeFluidCapacityFixture(carId,out fixtureError),"F8 combined refill uses a fresh persisted/reloaded capacity fixture for both fluids: "+fixtureError);
            yield return null;
            int combinedCash=game.Profile.cash;long combinedCompletion=game.ManualRefillCompletion;
            double combinedOilBefore=game.Profile.SelectedCar.fluids.oilLitres;
            double combinedCoolantBefore=game.Profile.SelectedCar.fluids.coolantLitres;
            FramePacingProbe.OverridePhase="typed-refill/f8-combined";
            yield return TypedRefillLegacyGuiClick(input,"fluids-fill");
            float combinedDeadline=Time.realtimeSinceStartup+10;
            while(game.ManualRefillCompletion==combinedCompletion&&Time.realtimeSinceStartup<combinedDeadline)yield return null;
            Check(game.ManualRefillCompletion>combinedCompletion&&game.ManualRefillLastSucceeded&&
                game.Profile.cash<combinedCash&&game.Profile.SelectedCar.fluids.oilLitres>combinedOilBefore&&
                game.Profile.SelectedCar.fluids.coolantLitres>combinedCoolantBefore,
                "F8 combined fill publishes both fluid quantities and one cash effect");
            if(restoreNative&&!shell.Visible)yield return Key(input,0x77);
            FramePacingProbe.OverridePhase=null;
        }

        IEnumerator TypedFluidRefillChecks(WindowsKeyboardProbe input)
        {
            var priorFailureInjection=SqliteStateStore.FailureInjection;
            if(!shell.OwnedDetailsOpen)
            {
                shell.InvalidateOwnedGrid();shell.Refresh();yield return new WaitForSecondsRealtime(.2f);
                yield return Click(input,"ownedCar:"+game.Profile.SelectedCar.instanceId);
            }
            string carId=game.Profile.SelectedCar.instanceId;
            var car=game.Profile.cars.Find(c=>c.instanceId==carId);
            Check(car!=null&&car.fluids!=null,"typed maintenance starts from the actual selected owned car");
            var priorShift=game.Profile.driving.shifting;
            float originalAppliedMaster=SharedAudioSettings.MasterVolume;
            FramePacingProbe.OverridePhase="typed-refill/native-capacity-fixture";
            Check(PersistNativeFluidCapacityFixture(carId,out var prepareError),"native oil/coolant capacity fixture is officially saved and reloaded with the same selected owned car: "+prepareError);
            car=game.Profile.cars.Find(c=>c.instanceId==carId);int cash=game.Profile.cash;
            double oil=car.fluids.oilLitres;
            yield return RefreshTypedOwnedCar(input,carId);
            game.Profile.cash=0;
            yield return TypedRefillMaintenanceClick(input,"maintenanceOil");
            Check(shell.ModalOpen&&!shell.Root.Q<Button>("dialogConfirm").enabledInHierarchy&&
                shell.Root.Q<Label>("maintenanceError").text.Length>0,
                "insufficient cash leaves the shared confirmation disabled with an explanatory error");
            yield return Click(input,"dialogCancel");game.Profile.cash=cash;
            FramePacingProbe.OverridePhase="typed-refill/native-oil-quote-stale";
            yield return TypedRefillMaintenanceClick(input,"maintenanceOil");
            var slider=shell.Root.Q<Slider>("maintenanceQuantitySlider");var cost=shell.Root.Q<Label>("maintenanceCost");
            Check(shell.ModalOpen&&slider!=null&&cost!=null&&cost.text.Contains("Total ")&&cost.text.Contains("Cash "),
                "oil refill dialog uses the shared quantity slider and live money preview");
            yield return Capture("typed-refill-native-quote");
            slider.value=Mathf.Min(slider.highValue,Mathf.Max(.01f,(float)((slider.highValue-slider.lowValue)*.25f)));
            yield return new WaitForSecondsRealtime(.1f);
            double coolantPressure=car.fluids.coolantPressureBar;string stalePrice=cost.text;
            car.fluids.coolantPressureBar+=.125;
            yield return Click(input,"dialogConfirm");
            Check(TypedRefillActionCaptionVisible("dialogConfirm","Recover")&&game.ManualRefillNeedsRecovery,
                "stale admission immediately renders the explicit Recover action caption above the shared button surface");
            Check(!game.ManualRefillPending&&game.Profile.cash==cash&&car.fluids.oilLitres==oil&&
                shell.Root.Q<Label>("maintenanceError").text.Length>0&&cost.text==stalePrice,
                "a physical read-set change rejects the shown quote without silently repricing or charging");
            Check(cost.text==stalePrice,"recovery-required stale quote keeps its original displayed amount and price for review");
            yield return Capture("typed-refill-native-stale-quote");
            car.fluids.coolantPressureBar=coolantPressure;
            yield return new WaitForEndOfFrame();shell.UpdateTypedRefillDialog();
            if(game.ManualRefillNeedsRecovery)
            {
                yield return new WaitForEndOfFrame();shell.UpdateTypedRefillDialog();
                var confirm=shell.Root.Q<Button>("dialogConfirm");
                Check(confirm.enabledInHierarchy&&TypedRefillActionCaptionVisible("dialogConfirm","Recover"),"stale live read-set requires explicit recovery through the visible refill-dialog action");
                yield return Click(input,"dialogConfirm");
                Check(!game.ManualRefillNeedsRecovery,"dialog recovery reloads the validated current save");
                car=game.Profile.cars.Find(c=>c.instanceId==carId);cash=game.Profile.cash;oil=car.fluids.oilLitres;
            }
            yield return Click(input,"dialogCancel");
            if(game.ManualRefillNeedsRecovery)throw new InvalidOperationException("Stale quote recovery did not clear its owner fence.");
            yield return RefreshTypedOwnedCar(input,carId);
            FramePacingProbe.OverridePhase="typed-refill/native-oil-pending";
            yield return TypedRefillMaintenanceClick(input,"maintenanceOil");
            slider=shell.Root.Q<Slider>("maintenanceQuantitySlider");cost=shell.Root.Q<Label>("maintenanceCost");
            slider.value=Mathf.Min(slider.highValue,Mathf.Max(.01f,(float)((slider.highValue-slider.lowValue)*.25f)));
            yield return new WaitForSecondsRealtime(.1f);
            string shown=cost.text;int dispatches=0;var entered=new ManualResetEventSlim(false);var release=new ManualResetEventSlim(false);var workerDone=new ManualResetEventSlim(false);
            float pendingVolumeDraft=0;
            SqliteStateStore.FailureInjection=label=>
            {
                if(label==null||!label.EndsWith(":before-commit",StringComparison.Ordinal))return;
                Interlocked.Increment(ref dispatches);entered.Set();
                try{if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("Native typed refill held-writer release expired.");}
                finally{workerDone.Set();}
            };
            try
            {
                yield return Click(input,"dialogConfirm");
                float enterDeadline=Time.realtimeSinceStartup+5;
                while(!entered.IsSet&&Time.realtimeSinceStartup<enterDeadline)yield return null;
                Check(entered.IsSet,"confirmation dispatches the actual SQLite worker");
                for(int frame=0;frame<8;frame++){yield return new WaitForEndOfFrame();shell.UpdateTypedRefillDialog();}
                Check(game.Profile.cash==cash&&car.fluids.oilLitres==oil&&shell.ModalOpen&&
                    shell.Root.Q<Label>("maintenanceRefillStatus").text.Length>0&&
                    !shell.Root.Q<Button>("dialogConfirm").enabledInHierarchy,
                    "held writer keeps live resources unchanged, reports pending status, and disables duplicate confirmation over advancing frames");
                Check(dispatches==1&&cost.text==shown,"pending UI preserves its quote and does not dispatch a second purchase");
                yield return Capture("typed-refill-native-pending");
                yield return Click(input,"dialogConfirm");
                Check(dispatches==1&&game.Profile.cash==cash&&car.fluids.oilLitres==oil,
                    "a second confirmation click during the delayed write is idempotently blocked");
                var changedShift=priorShift==ShiftMode.Automatic?ShiftMode.Sequential:ShiftMode.Automatic;
                game.Profile.driving.shifting=changedShift;
                bool preferenceSaved=ProfileStore.TrySavePreferences(game.Profile,out var preferenceError);
                var storedPreferences=File.Exists(DrivingPreferences.FilePath)?JsonUtility.FromJson<DrivingPreferences.Data>(
                    File.ReadAllText(DrivingPreferences.FilePath,Encoding.UTF8)):null;
                Check(preferenceSaved&&game.ManualRefillPending&&storedPreferences!=null&&storedPreferences.driving.shifting==changedShift,
                    "a changed separate preference persists while retaining the typed refill lease: "+preferenceError);
                game.StartPractice();
                Check(game.Race==null&&game.Profile.cash==cash&&car.fluids.oilLitres==oil,
                    "race entry is blocked before it can mutate live state while refill publication is pending");
                // Navigation can close the view while the operation remains retained.
                yield return Click(input,"modalClose");
                Check(!shell.ModalOpen,"closing the refill view does not require cancelling accepted work");
                yield return Click(input,"navBuild");
                Check(game.ScreenName!="Garage"&&game.ManualRefillPending&&game.Profile.cash==cash&&car.fluids.oilLitres==oil,
                    "browsing to Build leaves delayed refill work retained without applying it");
                var browserTiming=shell.PartBrowser;
                if(browserTiming!=null)report.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "NOTE pending Build browser readyGroups={0} lookupMs={1:F3} inspectionMs={2:F3} shopPopulationMs={3:F3} ledgerSetupMs={4:F3} enumerationMs={5:F3} classificationMs={6:F3}",
                    browserTiming.UsedReadyProductGroups,browserTiming.ProductGroupLookupMilliseconds,
                    browserTiming.CurrentInspectionMilliseconds,browserTiming.ShopPopulationMilliseconds,
                    browserTiming.ShopLedgerSetupMilliseconds,browserTiming.ShopProductEnumerationMilliseconds,
                    browserTiming.ShopClassificationMilliseconds));
                if(browserTiming!=null)report.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "NOTE pending Build phase sums representativeMs={0:F3} supplyCountMs={1:F3} quoteHoldMs={2:F3} entryMs={3:F3} aliasMs={4:F3} usedMs={5:F3} stockBindingMs={6:F3} bound={7}",
                    browserTiming.ShopRepresentativeMilliseconds,browserTiming.ShopSupplyCountMilliseconds,
                    browserTiming.ShopQuoteAndHoldMilliseconds,browserTiming.ShopEntryCreationMilliseconds,
                    browserTiming.ShopAliasMilliseconds,browserTiming.ShopUsedOfferMilliseconds,
                    browserTiming.ShopStockBindingMilliseconds,browserTiming.UsedReadyStockBinding));
                FramePacingProbe.OverridePhase="typed-refill/native-settings-draft-pending";
                yield return Click(input,"settingsButton");
                yield return Click(input,"tabSound");
                var volumeDraft=shell.Root.Q<FloatField>("masterVolumeValue");
                float originalVolumeDraft=volumeDraft.value;
                var profileBeforeApply=game.Profile;
                float appliedVolumeDraft=originalVolumeDraft<50?58:42;
                volumeDraft.value=appliedVolumeDraft;
                yield return Click(input,"applySettings");
                Check(shell.ModalOpen,"native Settings change opens its real confirmation while refill publication is pending");
                yield return Click(input,"dialogApply");
                Check(!shell.ModalOpen&&ReferenceEquals(game.Profile,profileBeforeApply)&&game.ManualRefillPending&&
                    game.Profile.cash==cash&&car.fluids.oilLitres==oil&&
                    Math.Abs(SharedAudioSettings.MasterVolume-appliedVolumeDraft/100f)<.001f,
                    "separate native Settings Apply retains the same profile and pending refill resources");
                pendingVolumeDraft=appliedVolumeDraft<50?55:45;
                volumeDraft.value=pendingVolumeDraft;
                Check(shell.SettingsDirty&&Mathf.Abs(volumeDraft.value-pendingVolumeDraft)<.01f&&
                    game.ManualRefillPending&&game.Profile.cash==cash&&car.fluids.oilLitres==oil,
                    "an unsaved native Settings draft remains editable while the typed refill worker is pending");
            }
            finally
            {
                release.Set();SqliteStateStore.FailureInjection=priorFailureInjection;
            }
            FramePacingProbe.OverridePhase="typed-refill/native-oil-publication";
            float workerDeadline=Time.realtimeSinceStartup+8;
            while(!workerDone.IsSet&&Time.realtimeSinceStartup<workerDeadline)yield return null;
            Check(workerDone.IsSet,"typed refill SQLite worker exits the held-write callback before fixture cleanup");
            entered.Dispose();release.Dispose();workerDone.Dispose();
            float until=Time.realtimeSinceStartup+8;
            while(game.ManualRefillPending&&Time.realtimeSinceStartup<until)yield return null;
            FramePacingProbe.OverridePhase="typed-refill/native-publication-settings-draft";
            Check(!game.ManualRefillPending&&game.ManualRefillLastSucceeded&&game.Profile.cash<cash&&car.fluids.oilLitres>oil,
                "closed-view operation publishes once after durable success");
            Check(shell.SettingsDirty&&Mathf.Abs(shell.Root.Q<FloatField>("masterVolumeValue").value-pendingVolumeDraft)<.01f,
                "native unsaved Settings draft survives typed refill completion without being applied");
            yield return Capture("typed-refill-native-settings-draft");
            Check(game.Profile.driving.shifting!=priorShift,
                "affected refill publication preserves the preference draft/save made while the worker was pending");
            Check(game.Profile.economyReceipts.Count<=256&&game.Profile.economyReceipts.Count>0,
                "native completion retains the bounded business receipt window");
            yield return Click(input,"revertSettings");
            yield return Click(input,"navGarage");
            if(!shell.OwnedDetailsOpen)
            {
                shell.InvalidateOwnedGrid();shell.Refresh();yield return new WaitForSecondsRealtime(.2f);
                yield return Click(input,"ownedCar:"+carId);
            }
            yield return TypedRefillMaintenanceClick(input,"maintenanceOil");
            Check(shell.ModalOpen&&shell.Root.Q<Button>("dialogConfirm").enabledInHierarchy&&
                TypedRefillActionCaptionVisible("dialogConfirm","Confirm"),
                "a fresh refill dialog after prior success can quote and confirm a distinct operation");
            yield return Click(input,"dialogCancel");

            FramePacingProbe.OverridePhase="typed-refill/native-coolant-quote";
            yield return TypedRefillMaintenanceClick(input,"maintenanceCoolant");
            slider=shell.Root.Q<Slider>("maintenanceQuantitySlider");cost=shell.Root.Q<Label>("maintenanceCost");
            Check(shell.ModalOpen&&slider!=null&&cost!=null&&cost.text.Contains("Total ")&&cost.text.Contains("Cash "),
                "coolant refill dialog quotes an explicit quantity and cost");
            slider.value=Mathf.Min(slider.highValue,Mathf.Max(.01f,(float)((slider.highValue-slider.lowValue)*.25f)));
            yield return new WaitForSecondsRealtime(.1f);
            int coolantCash=game.Profile.cash;long coolantCompletion=game.ManualRefillCompletion;
            double coolantBefore=car.fluids.coolantLitres,oilBeforeCoolant=car.fluids.oilLitres;
            FramePacingProbe.OverridePhase="typed-refill/native-coolant-confirm";
            yield return Click(input,"dialogConfirm");
            float coolantCommitDeadline=Time.realtimeSinceStartup+10;
            while(game.ManualRefillCompletion==coolantCompletion&&Time.realtimeSinceStartup<coolantCommitDeadline)yield return null;
            Check(game.ManualRefillCompletion>coolantCompletion&&game.ManualRefillLastSucceeded&&
                game.Profile.cash<coolantCash&&car.fluids.coolantLitres>coolantBefore&&car.fluids.oilLitres==oilBeforeCoolant,
                "native coolant confirmation commits coolant and cash while leaving oil unchanged");
            Check(!shell.Root.Q<Button>("dialogConfirm").enabledInHierarchy&&TypedRefillActionCaptionVisible("dialogConfirm","Complete"),
                "completed native refill renders its disabled Complete caption above the shared button surface");
            yield return Capture("typed-refill-native-complete");
            yield return Click(input,"dialogCancel");
            game.Profile.driving.shifting=priorShift;
            Check(ProfileStore.TrySavePreferences(game.Profile,out var restoreError),"native verification restores the isolated profile's original driving preference: "+restoreError);
            SharedAudioSettings.SetMasterVolume(originalAppliedMaster,true);
            FramePacingProbe.OverridePhase=null;
        }
#endif
    }
}
