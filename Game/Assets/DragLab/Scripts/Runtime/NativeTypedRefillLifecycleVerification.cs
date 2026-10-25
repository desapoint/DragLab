using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    /// <summary>Native S06d lifecycle checks against the actual isolated SQLite owner.</summary>
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        sealed class NativeLifecycleExecutor : ITypedFluidRefillExecutor
        {
            readonly SqliteTypedFluidRefillExecutor actual=new SqliteTypedFluidRefillExecutor();
            internal bool LoseAcknowledgement, HoldAfterCommit;
            internal readonly ManualResetEventSlim Committed=new ManualResetEventSlim(false);
            internal readonly ManualResetEventSlim Release=new ManualResetEventSlim(false);
            internal int CommitCalls, ReconcileCalls;
            internal string OperationId, ReconciledOperationId;

            public TypedFluidRefillOutcome Commit(TypedSaveSlotBinding binding,TypedFluidRefillCommand command,long now)
            {
                Interlocked.Increment(ref CommitCalls);OperationId=command.Request.Identity.OperationId;
                var outcome=actual.Commit(binding,command,now);
                if(HoldAfterCommit&&(outcome.Status==TypedFluidRefillStatus.Committed||outcome.Status==TypedFluidRefillStatus.Replayed))
                {Committed.Set();if(!Release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("S06d post-commit acknowledgement gate expired.");}
                if(LoseAcknowledgement&&outcome.Status==TypedFluidRefillStatus.Committed)
                    return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown,null,"Native lifecycle fixture lost only the real commit acknowledgement.");
                return outcome;
            }

            public TypedFluidRefillOutcome Reconcile(TypedSaveSlotBinding binding,TypedFluidRefillIntent intent)
            {Interlocked.Increment(ref ReconcileCalls);ReconciledOperationId=intent.Request.Identity.OperationId;return actual.Reconcile(binding,intent);}
        }

        FieldInfo NativeRefillPilotField()=>typeof(PrototypeGame).GetField("manualRefillPilot",BindingFlags.Instance|BindingFlags.NonPublic)
            ??throw new MissingFieldException(typeof(PrototypeGame).FullName,"manualRefillPilot");

        TypedFluidRefillPilot InstallLifecyclePilot(NativeLifecycleExecutor executor)
        {
            var pilot=new TypedFluidRefillPilot(()=>game.Profile,()=>game.Costs,executor);
            NativeRefillPilotField().SetValue(game,pilot);return pilot;
        }

        TypedFluidRefillQuote NativeDisplayedRefillQuote()
        {
            var field=typeof(NativeMenuShell).GetField("typedRefillQuote",BindingFlags.Instance|BindingFlags.NonPublic);
            if(field==null)throw new MissingFieldException(typeof(NativeMenuShell).FullName,"typedRefillQuote");
            return field.GetValue(shell) as TypedFluidRefillQuote;
        }

        int NativeOperationReceiptCount(string operationId)
        {
            using(var db=new SqliteNative(ProfileStore.FilePath,true))
                return int.Parse(db.Scalar("SELECT COUNT(*) FROM operation_receipts WHERE operation_id=?",operationId),System.Globalization.CultureInfo.InvariantCulture);
        }

        string[] NativeOperationReceipt(TypedFluidRefillQuote quote)
        {
            using(var db=new SqliteNative(ProfileStore.FilePath,true))
            {
                var rows=db.Query("SELECT command_kind,ledger_revision_before,ledger_revision_after,storage_commit_before,storage_commit_after,domain_receipt_id FROM operation_receipts WHERE save_slot=? AND reset_generation=? AND operation_id=?",
                    quote.Request.Identity.SaveSlot,quote.Request.Identity.ResetGeneration,quote.Request.Identity.OperationId);
                return rows.Count==1?rows[0]:null;
            }
        }

        string[] NativeRefillResult(TypedFluidRefillQuote quote)
        {
            using(var db=new SqliteNative(ProfileStore.FilePath,true))
            {
                var rows=db.Query("SELECT car_id,cost,cash_after,oil_added_bits,coolant_added_bits,oil_after_bits,coolant_after_bits,domain_receipt_id FROM fluid_refill_results WHERE save_slot=? AND reset_generation=? AND operation_id=?",
                    quote.Request.Identity.SaveSlot,quote.Request.Identity.ResetGeneration,quote.Request.Identity.OperationId);
                return rows.Count==1?rows[0]:null;
            }
        }

        long[] NativeDurableOwnerState(string carId)
        {
            using(var db=new SqliteNative(ProfileStore.FilePath,true))
            {
                var economy=db.Query("SELECT cash,revision FROM profile_economy WHERE singleton=1");
                var fluid=db.Query("SELECT oil_litres_bits,coolant_litres_bits FROM fluid_states WHERE owner_kind='car' AND owner_id=?",carId);
                if(economy.Count!=1||fluid.Count!=1)throw new InvalidDataException("S06d could not read the exact durable economy/fluid owner rows.");
                return new[]{long.Parse(economy[0][0],System.Globalization.CultureInfo.InvariantCulture),long.Parse(economy[0][1],System.Globalization.CultureInfo.InvariantCulture),
                    long.Parse(fluid[0][0],System.Globalization.CultureInfo.InvariantCulture),long.Parse(fluid[0][1],System.Globalization.CultureInfo.InvariantCulture),
                    long.Parse(db.Scalar("SELECT value FROM state_meta WHERE name='commit_id'"),System.Globalization.CultureInfo.InvariantCulture),
                    long.Parse(db.Scalar("SELECT value FROM state_meta WHERE name='reset_generation'"),System.Globalization.CultureInfo.InvariantCulture)};
            }
        }

        string NativeSaveHash()
        {
            using(var stream=new MemoryStream())
            {
                foreach(string path in new[]{ProfileStore.FilePath,ProfileStore.FilePath+"-wal"})
                {
                    byte[] bytes=File.Exists(path)?File.ReadAllBytes(path):Array.Empty<byte>();
                    byte[] size=BitConverter.GetBytes((long)bytes.Length);stream.Write(size,0,size.Length);stream.Write(bytes,0,bytes.Length);
                }
                using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","");
            }
        }

        bool NativeCaption(string id,string text)=>TypedRefillActionCaptionVisible(id,text);

        IEnumerator LifecyclePrepareOil(WindowsKeyboardProbe input,string carId)
        {
            yield return RefreshTypedOwnedCar(input,carId);
            yield return TypedRefillMaintenanceClick(input,"maintenanceOil");
            var slider=shell.Root.Q<Slider>("maintenanceQuantitySlider");
            if(slider==null||!shell.ModalOpen)throw new InvalidOperationException("S06d could not open the real oil refill dialog.");
            slider.value=Mathf.Min(slider.highValue,Mathf.Max(.01f,slider.lowValue+(slider.highValue-slider.lowValue)*.35f));
            yield return new WaitForSecondsRealtime(.12f);
        }

        IEnumerator LifecycleWaitUntil(Func<bool> condition,float seconds,string what)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!condition()&&Time.realtimeSinceStartup<deadline){yield return null;shell.UpdateTypedRefillDialog();}
            Check(condition(),"bounded advancing-frame wait: "+what);
        }

        IEnumerator TypedFluidRefillLifecycleChecks(WindowsKeyboardProbe input)
        {
            var oldFailure=SqliteStateStore.FailureInjection;
            var oldRecoveryFailure=ProfileStore.ControlledRecoveryFailureInjection;
            var pilotField=NativeRefillPilotField();object oldPilot=pilotField.GetValue(game);
            string carId=game.Profile.SelectedCar.instanceId;
            ManualResetEventSlim activeGate=null;
            try
            {
                Check(PersistNativeFluidCapacityFixture(carId,out var fixtureError),"S06d uses a saved/reloaded isolated owned-car capacity fixture: "+fixtureError);
                Check(shell.OwnedDetailsOpen||game.Profile.SelectedCar!=null,"selected fixture owner remains available to the native shell");

                // Real commit followed by lost acknowledgement; UI retry reconciles that same ID.
                var lost=new NativeLifecycleExecutor {LoseAcknowledgement=true};
                var pilot=InstallLifecyclePilot(lost);
                yield return LifecyclePrepareOil(input,carId);
                var profile=game.Profile;var car=profile.cars.Find(c=>c.instanceId==carId);
                var quote=NativeDisplayedRefillQuote();
                if(quote==null)throw new InvalidOperationException("S06d did not capture the displayed native refill quote.");
                int cashBefore=profile.cash;long revisionBefore=profile.revision,commitBefore=profile.storageCommit;
                double oilBefore=car.fluids.oilLitres,coolantBefore=car.fluids.coolantLitres;
                string slotBefore=ProfileStore.SelectedSlot;long ownerEpochBefore=ProfileStore.OwnerEpoch;
                long generationBefore=quote.Request.Identity.ResetGeneration;
                string fileBefore=NativeSaveHash();
                Check(quote.Request.RequestedOilLitres>0&&quote.OilAdded>0&&quote.OilAdded<=quote.Request.RequestedOilLitres,
                    "native quote captures distinct positive requested and accepted oil quantities");
                yield return Click(input,"dialogConfirm");
                yield return LifecycleWaitUntil(()=>game.ManualRefillCanRetry||game.ManualRefillNeedsRecovery,10,"lost acknowledgement becomes explicit retry state");
                string lostId=game.ManualRefillOperationId;
                Check(!string.IsNullOrEmpty(lostId)&&lostId==lost.OperationId,"retry retains the original issued operation ID");
                Check(quote.Request.Identity.SaveSlot==slotBefore&&ProfileStore.SelectedSlot==slotBefore&&ProfileStore.OwnerEpoch==ownerEpochBefore&&
                    quote.Request.Identity.ResetGeneration==generationBefore,
                    "lost-ack retry retains the exact selected save slot, reset generation and owner epoch");
                Check(NativeCaption("dialogConfirm","Retry status")&&game.ManualRefillCanRetry,"visible dialog retry caption is a child label on the shared button surface");
                Check(profile.cash==cashBefore&&car.fluids.oilLitres==oilBefore&&car.fluids.coolantLitres==coolantBefore&&profile.revision==revisionBefore&&profile.storageCommit==commitBefore,
                    "lost acknowledgement does not publish before the original operation is reconciled");
                Check(NativeOperationReceiptCount(lostId)==1&&lost.CommitCalls==1,"real SQLite committed exactly one operation receipt before its acknowledgement was masked");
                var durableLost=NativeDurableOwnerState(carId);
                var durableReceipt=NativeOperationReceipt(quote);var durableResult=NativeRefillResult(quote);
                long expectedOilBits=BitConverter.DoubleToInt64Bits(quote.ReadSet.Fluids.OilLitres+quote.OilAdded);
                Check(durableLost[0]==cashBefore-quote.Cost&&durableLost[1]==revisionBefore+1&&durableLost[2]==expectedOilBits&&
                    durableLost[3]==BitConverter.DoubleToInt64Bits(coolantBefore)&&durableLost[4]==commitBefore+1&&durableLost[5]==quote.Request.Identity.ResetGeneration,
                    "real SQLite already contains the exact quoted cash/fluid effect and expected commit generation while memory awaits retry");
                Check(durableReceipt!=null&&durableReceipt[0]=="FluidTopUp"&&long.Parse(durableReceipt[1],System.Globalization.CultureInfo.InvariantCulture)==revisionBefore&&
                    long.Parse(durableReceipt[2],System.Globalization.CultureInfo.InvariantCulture)==revisionBefore+1&&
                    long.Parse(durableReceipt[3],System.Globalization.CultureInfo.InvariantCulture)==commitBefore&&
                    long.Parse(durableReceipt[4],System.Globalization.CultureInfo.InvariantCulture)==commitBefore+1&&durableReceipt[5]==lostId,
                    "independent operation-receipt SQL returns the same identity and exact version fences");
                Check(durableResult!=null&&durableResult[0]==carId&&int.Parse(durableResult[1],System.Globalization.CultureInfo.InvariantCulture)==quote.Cost&&
                    int.Parse(durableResult[2],System.Globalization.CultureInfo.InvariantCulture)==cashBefore-quote.Cost&&
                    long.Parse(durableResult[3],System.Globalization.CultureInfo.InvariantCulture)==BitConverter.DoubleToInt64Bits(quote.OilAdded)&&
                    long.Parse(durableResult[4],System.Globalization.CultureInfo.InvariantCulture)==BitConverter.DoubleToInt64Bits(quote.CoolantAdded)&&
                    long.Parse(durableResult[5],System.Globalization.CultureInfo.InvariantCulture)==expectedOilBits&&
                    long.Parse(durableResult[6],System.Globalization.CultureInfo.InvariantCulture)==BitConverter.DoubleToInt64Bits(coolantBefore)&&durableResult[7]==lostId,
                    "independent refill-result SQL matches displayed price, accepted quantities, exact after-bits and receipt ID");
                yield return Capture("typed-refill-lifecycle-lost-ack");
                yield return Click(input,"dialogConfirm");
                yield return LifecycleWaitUntil(()=>!pilot.HasPending&&pilot.LastSucceeded,10,"real SQLite reconciliation publishes and acknowledges the existing refill");
                Check(lost.ReconcileCalls==1&&lost.ReconciledOperationId==lostId&&lost.CommitCalls==1,"retry reconciles the exact retained ID without dispatching a second commit");
                Check(profile.cash<cashBefore&&profile.revision==revisionBefore+1&&profile.storageCommit==commitBefore+1&&car.fluids.oilLitres>oilBefore&&car.fluids.coolantLitres==coolantBefore,
                    "one committed oil effect publishes with exact owner revision and leaves coolant unchanged");
                var publishedLost=NativeDurableOwnerState(carId);
                Check(publishedLost[0]==profile.cash&&publishedLost[1]==profile.revision&&publishedLost[2]==BitConverter.DoubleToInt64Bits(car.fluids.oilLitres)&&
                    publishedLost[3]==BitConverter.DoubleToInt64Bits(car.fluids.coolantLitres)&&publishedLost[4]==profile.storageCommit&&
                    publishedLost[5]==generationBefore&&ProfileStore.SelectedSlot==slotBefore&&ProfileStore.OwnerEpoch==ownerEpochBefore,
                    "published in-memory owner exactly matches independent durable SQLite rows and the retained owner identity");
                Check(pilot.Completion!=null&&pilot.Completion.OperationId==quote.Request.Identity.OperationId&&pilot.Completion.Cost==quote.Cost&&
                    BitConverter.DoubleToInt64Bits(car.fluids.oilLitres)==BitConverter.DoubleToInt64Bits(pilot.Completion.OilAfter)&&
                    BitConverter.DoubleToInt64Bits(pilot.Completion.OilAdded)==BitConverter.DoubleToInt64Bits(quote.OilAdded)&&
                    cashBefore-profile.cash==pilot.Completion.Cost,
                    "reconciled owner cash and exact oil bits match the original displayed quote and durable operation result");
                Check(NativeOperationReceiptCount(lostId)==1&&NativeSaveHash()!=fileBefore,"isolated SQLite owner changed once and contains one durable receipt");
                Check(NativeCaption("dialogConfirm","Complete"),"successful retry leaves the actual disabled Complete child caption visible");
                yield return Capture("typed-refill-lifecycle-reconciled");
                yield return Click(input,"dialogCancel");

                // A genuine SQLite pre-COMMIT injected failure is definite and leaves the owner image untouched.
                Check(PersistNativeFluidCapacityFixture(carId,out fixtureError),"definite-failure branch starts from a fresh persisted fixture: "+fixtureError);
                profile=game.Profile;car=profile.cars.Find(c=>c.instanceId==carId);cashBefore=profile.cash;revisionBefore=profile.revision;commitBefore=profile.storageCommit;
                oilBefore=car.fluids.oilLitres;coolantBefore=car.fluids.coolantLitres;
                var failed=new NativeLifecycleExecutor();pilot=InstallLifecyclePilot(failed);
                int preCommitFaults=0;
                SqliteStateStore.FailureInjection=label=>{if(label=="fluid-refill:before-commit"&&Interlocked.Increment(ref preCommitFaults)==1)throw new IOException("S06d injected pre-COMMIT writer failure.");};
                yield return LifecyclePrepareOil(input,carId);
                var failedQuote=NativeDisplayedRefillQuote();
                yield return Click(input,"dialogConfirm");
                yield return LifecycleWaitUntil(()=>!pilot.HasPending,10,"definite pre-COMMIT failure reaches terminal owner handling");
                string failedId=failed.OperationId;
                SqliteStateStore.FailureInjection=oldFailure;
                Check(preCommitFaults==1&&failedQuote!=null&&failedId==failedQuote.Request.Identity.OperationId,"the actual SQLite writer hit exactly one before-COMMIT fault for the displayed operation");
                Check(!game.ManualRefillLastSucceeded&&profile.cash==cashBefore&&car.fluids.oilLitres==oilBefore&&car.fluids.coolantLitres==coolantBefore&&profile.revision==revisionBefore&&profile.storageCommit==commitBefore,
                    "pre-COMMIT failure leaves in-memory cash, fluids and owner fences unchanged");
                var durableFailed=NativeDurableOwnerState(carId);
                Check(durableFailed[0]==cashBefore&&durableFailed[1]==revisionBefore&&durableFailed[2]==BitConverter.DoubleToInt64Bits(oilBefore)&&
                    durableFailed[3]==BitConverter.DoubleToInt64Bits(coolantBefore)&&durableFailed[4]==commitBefore&&
                    NativeOperationReceiptCount(failed.OperationId)==0,
                    "pre-COMMIT rollback preserves exact durable economy/fluid rows, commit fence and receipt count");
                Check(!string.IsNullOrEmpty(shell.Root.Q<Label>("maintenanceRefillStatus").text)&&shell.Root.Q<Label>("maintenanceRefillStatus").text.Contains("S06d injected pre-COMMIT"),
                    "definite writer failure is visibly reported before any fresh confirmation");
                yield return Capture("typed-refill-lifecycle-definite-failure");
                // The definite terminal result releases its lease; a fresh dialog must quote a new operation.
                yield return Click(input,"dialogCancel");
                long completionBefore=game.ManualRefillCompletion;
                yield return LifecyclePrepareOil(input,carId);
                var nextQuote=NativeDisplayedRefillQuote();
                string nextOperationId=nextQuote?.Request?.Identity?.OperationId;
                Check(!string.IsNullOrEmpty(nextOperationId)&&nextOperationId!=failedId&&NativeCaption("dialogConfirm","Confirm"),
                    "fresh quote after visible definite failure has a distinct identity and waits for its own explicit confirmation");
                yield return Capture("typed-refill-lifecycle-fresh-quote");
                yield return Click(input,"dialogConfirm");
                yield return LifecycleWaitUntil(()=>game.ManualRefillCompletion>completionBefore&&game.ManualRefillLastSucceeded,10,"fresh confirmed attempt succeeds after the definite failure");
                string successId=game.ManualRefillOperationId;
                Check(!string.IsNullOrEmpty(failedId)&&successId!=failedId&&NativeOperationReceiptCount(failedId)==0&&NativeOperationReceiptCount(successId)==1,
                    "fresh quote uses a new identity and creates exactly one new durable receipt");
                var successfulFresh=NativeDurableOwnerState(carId);
                Check(successfulFresh[0]==profile.cash&&successfulFresh[1]==profile.revision&&successfulFresh[2]==BitConverter.DoubleToInt64Bits(car.fluids.oilLitres)&&
                    successfulFresh[3]==BitConverter.DoubleToInt64Bits(car.fluids.coolantLitres)&&successfulFresh[4]==profile.storageCommit&&
                    NativeOperationReceiptCount(successId)==1,
                    "fresh confirmation publishes an owner image matching independently queried durable rows");
                yield return Click(input,"dialogCancel");

                // Commit really occurs; a changed captured dependency prevents publication; one failed controlled reload retains the lease.
                Check(PersistNativeFluidCapacityFixture(carId,out fixtureError),"recovery branch starts from a fresh persisted fixture: "+fixtureError);
                profile=game.Profile;car=profile.cars.Find(c=>c.instanceId==carId);cashBefore=profile.cash;revisionBefore=profile.revision;commitBefore=profile.storageCommit;
                oilBefore=car.fluids.oilLitres;coolantBefore=car.fluids.coolantLitres;
                double pressureBefore=car.fluids.coolantPressureBar;
                string recoveryCommittedHash=null;
                var recovery=new NativeLifecycleExecutor {HoldAfterCommit=true};pilot=InstallLifecyclePilot(recovery);activeGate=recovery.Release;
                yield return LifecyclePrepareOil(input,carId);
                yield return Click(input,"dialogConfirm");
                yield return LifecycleWaitUntil(()=>recovery.Committed.IsSet,8,"real SQLite commit reaches the post-commit acknowledgement gate");
                string recoveryId=recovery.OperationId;
                recoveryCommittedHash=NativeSaveHash();
                car.fluids.coolantPressureBar+=.125;
                recovery.Release.Set();activeGate=null;
                yield return LifecycleWaitUntil(()=>game.ManualRefillNeedsRecovery,10,"stale physical read set enters retained controlled recovery");
                Check(game.ManualRefillOperationId==recoveryId&&NativeCaption("dialogConfirm","Recover"),"recovery action retains the committed refill ID and exposes its actual child caption");
                Check(NativeOperationReceiptCount(recoveryId)==1,"recovery fixture has a real durable receipt before reload");
                ProfileStore.ControlledRecoveryFailureInjection=()=>throw new IOException("S06d one-shot validated-load failure.");
                yield return Click(input,"dialogConfirm");
                Check(game.ManualRefillNeedsRecovery&&game.ManualRefillBlocksMutation&&ReferenceEquals(game.Profile,profile)&&
                    car.fluids.coolantPressureBar==pressureBefore+.125,
                    "failed controlled recovery keeps the same owner and lease fenced without mutating the live profile");
                Check(NativeCaption("dialogConfirm","Recover")&&NativeOperationReceiptCount(recoveryId)==1,"failed recovery remains visibly retryable and leaves the durable receipt intact");
                Check(NativeSaveHash()==recoveryCommittedHash,"failed recovery leaves the committed isolated database/WAL bytes intact");
                yield return Capture("typed-refill-lifecycle-recovery-failed");
                ProfileStore.ControlledRecoveryFailureInjection=oldRecoveryFailure;
                yield return Click(input,"dialogConfirm");
                yield return LifecycleWaitUntil(()=>!game.ManualRefillNeedsRecovery&&ReferenceEquals(game.Profile,profile)==false,10,"real validated selected-owner recovery clears the retained lease");
                profile=game.Profile;car=profile.cars.Find(c=>c.instanceId==carId);
                Check(profile.cash<cashBefore&&profile.revision==revisionBefore+1&&profile.storageCommit==commitBefore+1&&car.fluids.oilLitres>oilBefore&&car.fluids.coolantLitres==coolantBefore,
                    "successful recovery installs the exact committed fluid/cash owner image");
                Check(NativeOperationReceiptCount(recoveryId)==1,"recovery does not replay or duplicate its durable command");
                yield return Click(input,"dialogCancel");

                // Real pre-COMMIT SQLite worker held across the application's mobile suspension boundary.
                Check(PersistNativeFluidCapacityFixture(carId,out fixtureError),"suspend branch starts from a fresh persisted fixture: "+fixtureError);
                profile=game.Profile;car=profile.cars.Find(c=>c.instanceId==carId);cashBefore=profile.cash;revisionBefore=profile.revision;commitBefore=profile.storageCommit;
                oilBefore=car.fluids.oilLitres;coolantBefore=car.fluids.coolantLitres;
                var shiftBefore=profile.driving.shifting;
                var entered=new ManualResetEventSlim(false);var release=new ManualResetEventSlim(false);var exited=new ManualResetEventSlim(false);activeGate=release;
                SqliteStateStore.FailureInjection=label=>
                {
                    if(label!="fluid-refill:before-commit")return;
                    entered.Set();try{if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("S06d suspend worker gate expired.");}
                    finally{exited.Set();}
                };
                var suspend=new NativeLifecycleExecutor();pilot=InstallLifecyclePilot(suspend);
                yield return LifecyclePrepareOil(input,carId);
                var suspendQuote=NativeDisplayedRefillQuote();
                if(suspendQuote==null)throw new InvalidOperationException("S06d could not capture the suspension refill's visible quote.");
                yield return Click(input,"dialogConfirm");
                yield return LifecycleWaitUntil(()=>entered.IsSet,8,"real SQLite writer reaches the held pre-COMMIT gate");
                string suspendedId=game.ManualRefillOperationId;int suspendedCash=profile.cash;double suspendedOil=car.fluids.oilLitres;
                string suspendedSlot=ProfileStore.SelectedSlot;long suspendedEpoch=ProfileStore.OwnerEpoch;
                float suspendStarted=Time.realtimeSinceStartup;game.SuspendForMobile();float suspendElapsed=Time.realtimeSinceStartup-suspendStarted;
                Check(suspendElapsed<.25f&&game.ManualRefillPending&&game.ManualRefillOperationId==suspendedId&&
                    profile.cash==suspendedCash&&car.fluids.oilLitres==suspendedOil&&profile.revision==revisionBefore&&profile.storageCommit==commitBefore,
                    "actual SuspendForMobile returns promptly and retains the same pending owner/operation without publishing");
                Check(suspendedId==suspendQuote.Request.Identity.OperationId&&suspendQuote.Request.Identity.SaveSlot==suspendedSlot&&
                    suspendQuote.Request.Identity.ResetGeneration==suspendQuote.OwnerReceipt.Generation&&ProfileStore.OwnerEpoch==suspendedEpoch,
                    "suspend retains the quote operation ID, selected slot, reset generation and selected-owner epoch");
                Check(profile.driving.shifting==shiftBefore,"mobile suspension preserves the selected driving preference while the refill remains pending");
                yield return Capture("typed-refill-lifecycle-suspended-worker");
                release.Set();activeGate=null;SqliteStateStore.FailureInjection=oldFailure;
                game.SendMessage("OnApplicationPause",false);
                yield return LifecycleWaitUntil(()=>exited.IsSet,8,"held SQLite callback exits after the gate is released");
                yield return LifecycleWaitUntil(()=>!pilot.HasPending&&pilot.LastSucceeded,10,"resumed Unity frames poll and publish the same worker result");
                Check(game.ManualRefillOperationId==suspendedId&&profile.cash<cashBefore&&profile.revision==revisionBefore+1&&profile.storageCommit==commitBefore+1&&
                    car.fluids.oilLitres>oilBefore&&car.fluids.coolantLitres==coolantBefore&&NativeOperationReceiptCount(suspendedId)==1&&
                    ProfileStore.SelectedSlot==suspendedSlot&&ProfileStore.OwnerEpoch==suspendedEpoch,
                    "resume publishes exactly one same-ID durable refill with unchanged coolant and expected owner fences");
                var resumedDurable=NativeDurableOwnerState(carId);
                Check(pilot.Completion!=null&&pilot.Completion.OperationId==suspendQuote.Request.Identity.OperationId&&
                    cashBefore-profile.cash==suspendQuote.Cost&&BitConverter.DoubleToInt64Bits(car.fluids.oilLitres)==BitConverter.DoubleToInt64Bits(pilot.Completion.OilAfter)&&
                    resumedDurable[0]==profile.cash&&resumedDurable[1]==profile.revision&&resumedDurable[2]==BitConverter.DoubleToInt64Bits(car.fluids.oilLitres)&&
                    resumedDurable[3]==BitConverter.DoubleToInt64Bits(car.fluids.coolantLitres)&&resumedDurable[4]==profile.storageCommit,
                    "resumed publication matches the original displayed quote, durable result and final SQLite owner rows");
                yield return Capture("typed-refill-lifecycle-resumed");
                entered.Dispose();release.Dispose();exited.Dispose();activeGate=null;
                yield return Click(input,"dialogCancel");
            }
            finally
            {
                activeGate?.Set();SqliteStateStore.FailureInjection=oldFailure;
                ProfileStore.ControlledRecoveryFailureInjection=oldRecoveryFailure;
                var current=pilotField.GetValue(game) as TypedFluidRefillPilot;
                if(current==null||!current.HasPending&&!current.NeedsRecovery)pilotField.SetValue(game,oldPilot);
            }
        }
#endif
    }
}
