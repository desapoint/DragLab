#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Independent managed checks for the active S06c typed refill pilot.</summary>
    public static class TypedFluidRefillPilotChecks
    {
        sealed class HeldScheduler : ITypedFluidRefillScheduler
        {
            internal WaitCallback Callback;
            public bool Queue(WaitCallback callback) { if (Callback != null) return false; Callback = callback; return true; }
            internal void Release() { var callback = Callback; Callback = null; if (callback == null) throw new InvalidOperationException("No held typed worker."); ThreadPool.QueueUserWorkItem(callback); }
        }

        sealed class LostAcknowledgementExecutor : ITypedFluidRefillExecutor
        {
            readonly SqliteTypedFluidRefillExecutor actual=new SqliteTypedFluidRefillExecutor();
            internal string CommittedOperationId, ReconciledOperationId;
            public TypedFluidRefillOutcome Commit(TypedSaveSlotBinding binding,TypedFluidRefillCommand command,long now)
            {
                CommittedOperationId=command.Request.Identity.OperationId;
                actual.Commit(binding,command,now);
                return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown,null,"fixture lost the commit acknowledgement");
            }
            public TypedFluidRefillOutcome Reconcile(TypedSaveSlotBinding binding,TypedFluidRefillIntent intent)
            {
                ReconciledOperationId=intent.Request.Identity.OperationId;
                return actual.Reconcile(binding,intent);
            }
        }

        sealed class HistoricalReplayExecutor : ITypedFluidRefillExecutor
        {
            readonly SqliteTypedFluidRefillExecutor actual=new SqliteTypedFluidRefillExecutor();
            readonly OperatingPolicy policy;
            internal TypedFluidRefillOutcome CurrentObservation;
            internal string NewerOperationId;
            internal HistoricalReplayExecutor(OperatingPolicy policy){this.policy=policy;}
            public TypedFluidRefillOutcome Commit(TypedSaveSlotBinding binding,TypedFluidRefillCommand command,long now)
            {
                var first=actual.Commit(binding,command,now);
                if(first.Status!=TypedFluidRefillStatus.Committed)return first;
                var r=command.ReadSet;var p=command.Plan;long secondNow=checked(now+1);
                var secondRequest=new FluidRefillRequest(new StateCommandIdentity(binding.SaveSlot,binding.ResetGeneration,MechanicsOwnership.NewId("s06c-newer")),
                    new StateCommandVersionFence(command.Request.Versions.ExpectedLedgerRevision+1,command.Request.Versions.ExpectedStorageCommit+1,
                        command.Request.Versions.ExpectedContentVersion),command.Request.CarId,.03125,0,secondNow,
                    checked(secondNow+TimeSpan.FromSeconds(policy.quoteSeconds).Ticks));
                int nextReceiptCount=Math.Min(256,r.EconomyReceiptCount+1);
                string oldest;
                if(r.EconomyReceiptCount==0)oldest=command.Request.Identity.OperationId;
                else if(r.EconomyReceiptCount==256)
                    using(var db=new SqliteNative(binding.CanonicalPath,true))oldest=db.Scalar("SELECT id FROM economy_receipts ORDER BY receipt_order LIMIT 1");
                else oldest=r.OldestEconomyReceiptId;
                var nextReadSet=new FluidRefillReadSet(r.CarId,r.VehicleId,r.EngineAssemblyId,r.GarageJobId,r.HasRebuild,r.RebuildActive,
                    p.CashAfter,now,r.OilCapacityLitres,r.CoolantCapacityLitres,r.OilPerLitre,r.CoolantPerLitre,p.AfterFluids,
                    r.HasUnsettledRun,false,nextReceiptCount,oldest);
                var nextPlan=FluidRefillPlanner.Plan(new FluidRefillInput(secondRequest,nextReadSet,secondNow));
                if(!nextPlan.Committable)throw new InvalidOperationException("Newer-owner fixture plan failed: "+nextPlan.Error);
                var newer=TypedFluidRefillCommand.Create(binding,nextPlan,command.PhysicsIdentity,secondNow);
                var newerResult=actual.Commit(binding,newer,secondNow);
                if(newerResult.Status!=TypedFluidRefillStatus.Committed)throw new InvalidOperationException("Newer-owner fixture write failed: "+newerResult.Error);
                NewerOperationId=newer.Intent.Request.Identity.OperationId;
                CurrentObservation=actual.Reconcile(binding,command.Intent);
                return CurrentObservation;
            }
            public TypedFluidRefillOutcome Reconcile(TypedSaveSlotBinding binding,TypedFluidRefillIntent intent)=>actual.Reconcile(binding,intent);
        }

        static int passed;
        static void Check(Action<bool,string> check, bool value, string label)
        {
            check(value, label);
            if (!value) throw new InvalidOperationException("S06c pilot check failed: " + label);
            passed++;
        }

        static void Metric(Action<bool,string> check,string label,long elapsedTicks)
        {
            double ms=elapsedTicks*1000.0/System.Diagnostics.Stopwatch.Frequency;
            check(true,"METRIC [desktop fixture observation; not a player/UI guarantee] "+label+"="+
                ms.ToString("F3",CultureInfo.InvariantCulture)+" ms");
        }

        static long[] FluidBits(FluidState f) => new[] {
            f.oilLitres,f.coolantLitres,f.oilTemperatureC,f.oilPressureBar,f.coolantPressureBar,f.oilCondition,
            f.oilConsumedLitres,f.oilLeakedLitres,f.coolantLostLitres,f.serviceElapsedSeconds,
            f.oilBurnLps,f.oilLeakLps,f.coolantLeakLps,f.coolantVentLps
        }.Select(BitConverter.DoubleToInt64Bits).ToArray();

        static PlayerProfile Seed(PlayerProfile current)
        {
            var vehicles = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            if (vehicles == null || vehicles.Length == 0) throw new InvalidOperationException("S06c requires authored vehicle definitions.");
            var catalog = new MechanicalCatalog(vehicles.Select(v => v.spec));
            var p = PlayerProfile.New(DateTime.UtcNow.Ticks - TimeSpan.TicksPerDay);
            p.SelectedCar.vehicleId = p.selectedVehicleId = vehicles[0].spec.id;
            MechanicsOwnership.Ensure(p, catalog); p.catalog = catalog; p.cash = 12000;
            p.revision = current.revision; p.storageCommit = current.storageCommit;
            var f = p.SelectedCar.fluids; var cap = catalog.Spec(p.SelectedCar.vehicleId).fluids;
            f.oilLitres = Math.Min(2.1, cap.oilCapacityLitres * .55); f.coolantLitres = Math.Min(1.4, cap.coolantCapacityLitres * .5);
            f.oilTemperatureC=91.25; f.oilPressureBar=3.75; f.coolantPressureBar=1.25;
            f.oilCondition=.63; f.oilConsumedLitres=.12; f.oilLeakedLitres=.03; f.coolantLostLitres=.04;
            f.serviceElapsedSeconds=18.5; f.oilBurnLps=.0001; f.oilLeakLps=.00002; f.coolantLeakLps=.00003; f.coolantVentLps=.00004;
            // New profiles start the economy clock at zero. A valid retained receipt
            // must be no later than that clock, so seed both with the same positive
            // authored fixture timestamp instead of weakening production validation.
            long receiptTick=Math.Max(1,p.idleClaimUtcTicks);
            p.lastEconomyUtcTicks=receiptTick;
            p.economyReceipts=new List<EconomyReceipt>(256);
            for(int i=0;i<256;i++)p.economyReceipts.Add(new EconomyReceipt {
                id=i==0?"s06c-fifo-oldest":"s06c-fifo-"+i.ToString("D3",CultureInfo.InvariantCulture),operation="Fixture history",
                target="fixture",revision=p.revision,utcTicks=receiptTick,cashBefore=p.cash,cashAfter=p.cash,
                disposed=Array.Empty<string>(),retained=Array.Empty<string>()
            });
            string invalid=MechanicsOwnership.Validate(p,catalog); if(invalid!=null)throw new InvalidDataException(invalid);
            if(!ProfileStore.TrySave(p))throw new IOException("Could not persist isolated authored fixture: "+ProfileStore.Status);
            var loaded=ProfileStore.Load();
            if(!ProfileStore.TryGetCommandOwner(loaded,out _))throw new InvalidOperationException("Fixture load did not register a selected typed owner.");
            return loaded;
        }

        static string Meta(string path,string key)
        { using(var db=new SqliteNative(path,true))return db.Scalar("SELECT value FROM state_meta WHERE name=?",key); }

        static int QuoteCacheCount(TypedFluidRefillPilot pilot)
        {
            var field=typeof(TypedFluidRefillPilot).GetField("quotes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var value=(System.Collections.IDictionary)field.GetValue(pilot);return value.Count;
        }

        static void VerifyQuoteCache(PlayerProfile profile,OperatingPolicy policy,Action<bool,string> check)
        {
            long now=Math.Max(DateTime.UtcNow.Ticks,profile.lastEconomyUtcTicks+TimeSpan.TicksPerSecond);
            var pilot=new TypedFluidRefillPilot(()=>profile,()=>policy);
            TypedFluidRefillQuote q=null;
            for(int i=0;i<9;i++)
            {
                long quoteStart=System.Diagnostics.Stopwatch.GetTimestamp();
                bool quoted=pilot.TryQuote(profile,profile.SelectedCar.instanceId,.01*(i+1),0,now+i,out q,out var quoteError);
                long quoteTicks=System.Diagnostics.Stopwatch.GetTimestamp()-quoteStart;
                Check(check,quoted,"bounded quote cache accepts distinct input "+i.ToString(CultureInfo.InvariantCulture)+": "+quoteError);
                if(i==0)Metric(check,"quote admission cache miss (planner Capture + Plan)",quoteTicks);
                Check(check,QuoteCacheCount(pilot)<=8,"quote cache remains within its eight-entry bound");
            }
            Check(check,QuoteCacheCount(pilot)==1,"ninth distinct quote evicts prior cache entries instead of growing unbounded");
            long hitStart=System.Diagnostics.Stopwatch.GetTimestamp();
            bool hit=pilot.TryQuote(profile,profile.SelectedCar.instanceId,.09,0,now+10,out var cached,out var hitError);
            long hitTicks=System.Diagnostics.Stopwatch.GetTimestamp()-hitStart;
            Check(check,hit,"unchanged quote context returns cached immutable terms: "+hitError);
            Metric(check,"quote admission validated cache hit",hitTicks);
            Check(check,pilot.TryQuote(profile,profile.SelectedCar.instanceId,.25,0,now+20,out var beforeExpiry,out _),"cache expiry fixture obtains quote");
            Check(check,pilot.TryQuote(profile,profile.SelectedCar.instanceId,.25,0,beforeExpiry.Request.ExpiresUtcTicks,out var afterExpiry,out _)&&
                afterExpiry.Request.IssuedUtcTicks==beforeExpiry.Request.ExpiresUtcTicks,
                "expired cache term is refreshed at the exact expiry boundary");
            double priorPrice=policy.oilPerLitre;policy.oilPerLitre=20;
            try
            {
                Check(check,pilot.TryQuote(profile,profile.SelectedCar.instanceId,1,0,now+30,out var repriced,out _)&&
                    repriced.Cost>beforeExpiry.Cost,"policy value changes produce a fresh quote cache key and price");
            }
            finally { policy.oilPerLitre=priorPrice; }

            // Identical QuoteKey inputs do not imply the scoped live read set still
            // matches. Cache hits must validate those mutable facts before returning.
            double amount=.25;long currentNow=now+40;
            Check(check,pilot.TryQuote(profile,profile.SelectedCar.instanceId,amount,0,currentNow,out var baseline,out _),"cached live-read-set baseline quote exists");
            int cash=profile.cash;profile.cash=cash-1;
            Check(check,pilot.TryQuote(profile,profile.SelectedCar.instanceId,amount,0,currentNow+1,out var cashFresh,out _)&&
                !ReferenceEquals(baseline,cashFresh)&&cashFresh.CashBefore==cash-1,
                "same-key cash mutation invalidates cached terms and returns a fresh quote instance");
            profile.cash=cash;
            var car=profile.cars.Find(c=>c.instanceId==profile.SelectedCar.instanceId);
            double pressure=car.fluids.oilPressureBar;car.fluids.oilPressureBar=pressure+.125;
            Check(check,pilot.TryQuote(profile,car.instanceId,amount,0,currentNow+2,out var fluidFresh,out _)&&
                !ReferenceEquals(cashFresh,fluidFresh)&&fluidFresh.ReadSet.Fluids.OilPressureBar==pressure+.125,
                "same-key physical fluid mutation invalidates cache and replans the exact read set");
            car.fluids.oilPressureBar=pressure;
            string job=car.garageJobId;car.garageJobId="s06c-cache-job";
            Check(check,!pilot.TryQuote(profile,car.instanceId,amount,0,currentNow+3,out _,out var jobError)&&
                !string.IsNullOrEmpty(jobError),"cached quote cannot bypass a newly reserved garage job gate");
            car.garageJobId=job;
            Check(check,pilot.TryQuote(profile,car.instanceId,amount,0,currentNow+4,out var afterJob,out _),
                "restoring the garage-job state allows a fresh same-key quote");
            profile.runCheckpoints.Add(new RunCheckpoint { settled=false });
            try
            {
                Check(check,!pilot.TryQuote(profile,car.instanceId,amount,0,currentNow+5,out _,out var runError)&&
                    !string.IsNullOrEmpty(runError),"cached quote cannot bypass a newly unsettled run gate");
            }
            finally { profile.runCheckpoints.RemoveAll(row=>row!=null&&!row.settled); }
            Check(check,pilot.TryQuote(profile,car.instanceId,amount,0,currentNow+6,out var afterRun,out _),
                "restoring the run gate allows a fresh same-key quote");
            int receiptCount=profile.economyReceipts.Count;
            var oldest=profile.economyReceipts[0];profile.economyReceipts.RemoveAt(0);
            try
            {
                Check(check,pilot.TryQuote(profile,car.instanceId,amount,0,currentNow+7,out var receiptFresh,out _)&&
                    !ReferenceEquals(afterRun,receiptFresh)&&receiptFresh.ReadSet.EconomyReceiptCount==receiptCount-1,
                    "same-key receipt-window mutation invalidates cached read-set terms");
            }
            finally { profile.economyReceipts.Insert(0,oldest); }
        }

        static PlayerProfile VerifyUnknownAndReconcile(PlayerProfile profile,OperatingPolicy policy,string path,Action<bool,string> check)
        {
            long issued=Math.Max(DateTime.UtcNow.Ticks,profile.lastEconomyUtcTicks+TimeSpan.TicksPerSecond);
            var scheduler=new HeldScheduler();var executor=new LostAcknowledgementExecutor();
            var pilot=new TypedFluidRefillPilot(()=>profile,()=>policy,executor,scheduler);
            string carId=profile.SelectedCar.instanceId;int cash=profile.cash;long commit=profile.storageCommit;
            Check(check,pilot.TryQuote(profile,carId,.2,0,issued,out var quote,out var quoteError),"unknown-outcome fixture obtains valid quote: "+quoteError);
            Check(check,pilot.TryBegin(quote,quote.Request.IssuedUtcTicks,out var beginError),"unknown-outcome fixture dispatches once: "+beginError);
            string operation=pilot.OperationId;bool duplicateRejected=!pilot.TryBegin(quote,quote.Request.IssuedUtcTicks,out _);
            Check(check,duplicateRejected&&pilot.OperationId==operation&&scheduler.Callback!=null,"double begin is rejected while preserving the accepted operation identity and one dispatch");
            scheduler.Release();var deadline=DateTime.UtcNow+TimeSpan.FromSeconds(8);
            while(!pilot.CanRetry&&DateTime.UtcNow<deadline){pilot.Poll();Thread.Sleep(2);}
            Check(check,pilot.CanRetry&&profile.cash==cash&&profile.storageCommit==commit,"lost acknowledgement remains unknown and does not publish or appear rolled back");
            Check(check,pilot.TryReconcile(out var reconcileError),"explicit retry/reconcile is accepted: "+reconcileError);
            scheduler.Release();deadline=DateTime.UtcNow+TimeSpan.FromSeconds(8);
            while(pilot.HasPending&&DateTime.UtcNow<deadline){pilot.Poll();Thread.Sleep(2);}
            Check(check,!pilot.HasPending&&pilot.LastSucceeded&&executor.CommittedOperationId==operation&&executor.ReconciledOperationId==operation,
                "reconciliation reuses the identical operation intent and publishes the single durable result");
            Check(check,profile.cash<cash&&profile.storageCommit==commit+1&&pilot.Completion!=null&&pilot.Completion.OperationId==operation,
                "unknown then reconcile creates exactly one spend and one storage commit");
            using(var db=new SqliteNative(path,true))
                Check(check,db.Scalar("SELECT COUNT(*) FROM operation_receipts WHERE operation_id=?",operation)=="1"&&
                    db.Scalar("SELECT COUNT(*) FROM fluid_refill_results WHERE operation_id=?",operation)=="1",
                    "independent SQLite rows contain exactly one operation receipt and one refill result");
            return profile;
        }

        static PlayerProfile VerifyHistoricalReplayRecovery(PlayerProfile profile,OperatingPolicy policy,string path,Action<bool,string> check)
        {
            long issued=Math.Max(DateTime.UtcNow.Ticks,profile.lastEconomyUtcTicks+TimeSpan.TicksPerSecond);
            var scheduler=new HeldScheduler();var executor=new HistoricalReplayExecutor(policy);
            var pilot=new TypedFluidRefillPilot(()=>profile,()=>policy,executor,scheduler);
            string carId=profile.SelectedCar.instanceId;int cash=profile.cash;double oil=profile.SelectedCar.fluids.oilLitres;long commit=profile.storageCommit;
            Check(check,pilot.TryQuote(profile,carId,.0625,0,issued,out var quote,out var quoteError),"historical-replay fixture obtains valid quote: "+quoteError);
            Check(check,pilot.TryBegin(quote,quote.Request.IssuedUtcTicks,out var beginError),"historical-replay fixture dispatches: "+beginError);
            string originalOperation=pilot.OperationId;scheduler.Release();
            var deadline=DateTime.UtcNow+TimeSpan.FromSeconds(10);
            while(pilot.HasPending&&DateTime.UtcNow<deadline){pilot.Poll();Thread.Sleep(2);}
            Check(check,pilot.NeedsRecovery&&pilot.Completion!=null&&pilot.Completion.OperationId==originalOperation&&
                executor.NewerOperationId!=null&&executor.CurrentObservation!=null&&executor.CurrentObservation.Status==TypedFluidRefillStatus.Replayed&&
                executor.CurrentObservation.ObservedStorageCommit>pilot.Completion.CommittedStorageCommit,
                "historical replay with a newer current storage token is retained for recovery rather than auto-applied");
            Check(check,profile.cash==cash&&profile.storageCommit==commit&&profile.SelectedCar.fluids.oilLitres==oil,
                "historical result does not overwrite live resources when its observed token is newer");
            Check(check,!pilot.TryBegin(quote,quote.Request.IssuedUtcTicks,out _)&&pilot.NeedsRecovery&&
                pilot.Completion.OperationId==originalOperation,"rejected duplicate admission preserves the retained historical result for recovery");
            Check(check,pilot.TryRecover(out var recovered,out var recoveryError),"explicit validated recovery loads current durable owner: "+recoveryError);
            Check(check,recovered!=null&&recovered.cash==cash-quote.Cost-1&&recovered.storageCommit==commit+2&&
                recovered.SelectedCar.fluids.oilLitres>oil&&recovered.economyReceipts.Any(item=>item.id==originalOperation)&&
                recovered.economyReceipts.Any(item=>item.id==executor.NewerOperationId),
                "recovery returns the latest state containing both historical and newer committed operations");
            using(var db=new SqliteNative(path,true))
                Check(check,db.Scalar("SELECT COUNT(*) FROM operation_receipts WHERE operation_id=?",originalOperation)=="1"&&
                    db.Scalar("SELECT COUNT(*) FROM operation_receipts WHERE operation_id=?",executor.NewerOperationId)=="1",
                    "independent SQLite history retains both operation identities exactly once");
            return recovered;
        }

        static PlayerProfile VerifyDefinitePreCommitFailure(PlayerProfile profile,OperatingPolicy policy,string path,Action<bool,string> check)
        {
            long issued=Math.Max(DateTime.UtcNow.Ticks,profile.lastEconomyUtcTicks+TimeSpan.TicksPerSecond);
            var scheduler=new HeldScheduler();var pilot=new TypedFluidRefillPilot(()=>profile,()=>policy,null,scheduler);
            string carId=profile.SelectedCar.instanceId;int cash=profile.cash;long commit=profile.storageCommit,revision=profile.revision;
            var bits=FluidBits(profile.SelectedCar.fluids);int receiptCount=profile.economyReceipts.Count;
            Check(check,pilot.TryQuote(profile,carId,.125,0,issued,out var quote,out var quoteError),"pre-commit failure fixture quote is valid: "+quoteError);
            var prior=SqliteStateStore.FailureInjection;
            try
            {
                bool injected=false;
                SqliteStateStore.FailureInjection=label=>{if(!injected&&label!=null&&label.EndsWith(":before-commit",StringComparison.Ordinal)){injected=true;throw new IOException("S06c deterministic pre-COMMIT failure");}};
                Check(check,pilot.TryBegin(quote,quote.Request.IssuedUtcTicks,out var beginError),"pre-commit failure fixture dispatches: "+beginError);
                scheduler.Release();var deadline=DateTime.UtcNow+TimeSpan.FromSeconds(8);
                while(pilot.HasPending&&DateTime.UtcNow<deadline){pilot.Poll();Thread.Sleep(2);}
                Check(check,injected&&!pilot.HasPending&&pilot.Status==TypedFluidRefillStatus.Rejected,
                    "a failure before COMMIT after confirmed receipt absence is truthfully rejected");
                SqliteStateStore.FailureInjection=prior;
                Check(check,profile.cash==cash&&profile.revision==revision&&profile.storageCommit==commit&&
                    FluidBits(profile.SelectedCar.fluids).SequenceEqual(bits)&&profile.economyReceipts.Count==receiptCount&&
                    Meta(path,"commit_id")==commit.ToString(CultureInfo.InvariantCulture),
                    "pre-COMMIT rollback changes no live resources, receipts, revision or SQLite commit");
                Check(check,ProfileStore.TryGetCommandOwner(profile,out _)&&pilot.TryQuote(profile,carId,.125,0,
                    checked(quote.Request.IssuedUtcTicks+1),out _,out _),"definite non-commit restores owner proof and permits a fresh quote");
            }
            finally { SqliteStateStore.FailureInjection=prior; }
            return profile;
        }

        static PlayerProfile VerifyPublicationReadSetRecovery(PlayerProfile profile,OperatingPolicy policy,string path,Action<bool,string> check)
        {
            long issued=Math.Max(DateTime.UtcNow.Ticks,profile.lastEconomyUtcTicks+TimeSpan.TicksPerSecond);
            var scheduler=new HeldScheduler();var pilot=new TypedFluidRefillPilot(()=>profile,()=>policy,null,scheduler);
            var car=profile.SelectedCar;string carId=car.instanceId;int cash=profile.cash;long commit=profile.storageCommit;
            double oil=car.fluids.oilLitres,pressure=car.fluids.oilPressureBar;
            Check(check,pilot.TryQuote(profile,carId,.1875,0,issued,out var quote,out var quoteError),"publication-fence fixture quote is valid: "+quoteError);
            Check(check,pilot.TryBegin(quote,quote.Request.IssuedUtcTicks,out var beginError),"publication-fence fixture dispatches: "+beginError);
            scheduler.Release();var deadline=DateTime.UtcNow+TimeSpan.FromSeconds(8);
            while(Meta(path,"commit_id")!=checked(commit+1).ToString(CultureInfo.InvariantCulture)&&DateTime.UtcNow<deadline)Thread.Sleep(2);
            Check(check,Meta(path,"commit_id")==checked(commit+1).ToString(CultureInfo.InvariantCulture),"actual durable commit finishes before owner publication");
            profile.cash=cash-1;car.fluids.oilPressureBar=pressure+.125;
            pilot.Poll();
            Check(check,pilot.NeedsRecovery&&pilot.Completion!=null&&profile.cash==cash-1&&
                car.fluids.oilLitres==oil&&car.fluids.oilPressureBar==pressure+.125,
                "stale cash/physical read set blocks every pilot assignment and retains the durable result");
            var operation=pilot.OperationId;
            Check(check,!pilot.TryBegin(quote,quote.Request.IssuedUtcTicks,out _)&&pilot.NeedsRecovery&&
                pilot.Completion.OperationId==operation,"rejected admission cannot erase a committed publication-recovery result");
            Check(check,pilot.TryRecover(out var recovered,out var recoveryError),"explicit read-set recovery validates the committed owner: "+recoveryError);
            Check(check,recovered!=null&&recovered.cash==cash-quote.Cost&&recovered.storageCommit==commit+1&&
                BitConverter.DoubleToInt64Bits(recovered.SelectedCar.fluids.oilLitres)==BitConverter.DoubleToInt64Bits(oil+quote.OilAdded)&&
                recovered.SelectedCar.fluids.oilPressureBar==pressure,
                "recovery obtains the durable after-image and discards only the test's stale in-memory edits");
            return recovered;
        }

        public static void Run(Action<bool,string> check,string outputDirectory)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(outputDirectory))throw new ArgumentException("An isolated output directory is required.",nameof(outputDirectory));
            passed=0;
            string priorPath=ProfileStore.VerificationPath, priorSlot=ProfileStore.SelectedSlot;
            var priorHook=SqliteStateStore.FailureInjection; var priorMeasure=SqliteStateStore.Measurement;
            object priorStatus=typeof(ProfileStore).GetField("status",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);
            string root=Path.Combine(Path.GetFullPath(outputDirectory),"s06c-pilot-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            string path=Path.Combine(root,"selected.sqlite3");
            try
            {
                ProfileStore.VerificationPath=path;ProfileStore.UseSlot("s06c-pilot");
                var live=Seed(ProfileStore.Load()); var car=live.SelectedCar; var beforeBits=FluidBits(car.fluids);int cashBefore=live.cash;long revisionBefore=live.revision,commitBefore=live.storageCommit;
                var policy=new OperatingPolicy { oilPerLitre=6.125,coolantPerLitre=3.875,quoteSeconds=300 };
                VerifyQuoteCache(live,policy,check);
                long now=Math.Max(DateTime.UtcNow.Ticks,live.lastEconomyUtcTicks+TimeSpan.TicksPerSecond);
                var scheduler=new HeldScheduler();
                var pilot=new TypedFluidRefillPilot(()=>live,()=>policy,null,scheduler);
                double requestedOil=.375,requestedCoolant=.25;
                Check(check,pilot.TryQuote(live,car.instanceId,requestedOil,requestedCoolant,now,out var quote,out var quoteError),"valid selected authored car produces a typed quote: "+quoteError);
                Check(check,quote.Request.ExpiresUtcTicks==checked(quote.Request.IssuedUtcTicks+TimeSpan.FromSeconds(policy.quoteSeconds).Ticks),"quote expiry equals the configured policy TTL from its issue tick");
                live.cash--;
                Check(check,!pilot.TryBegin(quote,quote.Request.IssuedUtcTicks,out _)&&!pilot.HasPending&&pilot.NeedsRecovery,
                    "cash read-set change rejects the displayed quote before dispatch and requires validated owner recovery");
                live.cash=cashBefore;
                Check(check,pilot.TryRecover(out var quoteRecovery,out var quoteRecoveryError),
                    "explicit recovery revalidates the owner after a direct stale-quote mutation: "+quoteRecoveryError);
                Check(check,quoteRecovery!=null&&quoteRecovery.cash==cashBefore&&quoteRecovery.storageCommit==commitBefore,
                    "stale-quote recovery returns the unchanged official owner image");
                live=quoteRecovery;car=live.SelectedCar;beforeBits=FluidBits(car.fluids);
                cashBefore=live.cash;revisionBefore=live.revision;commitBefore=live.storageCommit;
                now=checked(now+1);
                Check(check,pilot.TryQuote(live,car.instanceId,requestedOil,requestedCoolant,now,out quote,out quoteError),"fresh unchanged context produces a refreshed quote: "+quoteError);
                var legacyPreview=PaidEconomy.Preview(live,live.catalog,policy,ValuationPolicy.Defaults(),new PaymentRequest {kind=PaymentKind.Fluids,carId=car.instanceId,litres=requestedOil,coolantLitres=requestedCoolant},now);
                Check(check,quote.Cost==legacyPreview.cost&&BitConverter.DoubleToInt64Bits(quote.OilAdded)==BitConverter.DoubleToInt64Bits(legacyPreview.oil)&&BitConverter.DoubleToInt64Bits(quote.CoolantAdded)==BitConverter.DoubleToInt64Bits(legacyPreview.coolant),"quote cost and accepted volumes match independent PaidEconomy preview");
                double oracleOilAdded,oracleCoolantAdded,oracleOilAfter,oracleCoolantAfter;var originalFluids=car.fluids;
                try
                {
                    car.fluids=originalFluids.Copy();
                    oracleOilAdded=ServiceOperations.TopUp(live,car.instanceId,true,legacyPreview.oil);
                    oracleCoolantAdded=ServiceOperations.TopUp(live,car.instanceId,false,legacyPreview.coolant);
                    oracleOilAfter=car.fluids.oilLitres;oracleCoolantAfter=car.fluids.coolantLitres;
                }
                finally { car.fluids=originalFluids; }
                Check(check,pilot.TryBegin(quote,quote.Request.IssuedUtcTicks,out var beginError),"quote begins with its exact issue/accepted tick: "+beginError);
                Check(check,pilot.HasPending&&scheduler.Callback!=null&&live.cash==cashBefore&&live.revision==revisionBefore&&live.storageCommit==commitBefore&&FluidBits(car.fluids).SequenceEqual(beforeBits),"leased delayed operation leaves live profile and all fluid values unchanged before worker completion");
                long pollStart=System.Diagnostics.Stopwatch.GetTimestamp();pilot.Poll();long pollTicks=System.Diagnostics.Stopwatch.GetTimestamp()-pollStart;
                Check(check,pollTicks<System.Diagnostics.Stopwatch.Frequency/4,"owner poll returns promptly while the worker remains held");
                Metric(check,"held-worker Poll() call",pollTicks);
                long writerRelease=System.Diagnostics.Stopwatch.GetTimestamp(),publicationPollTicks=0;
                scheduler.Release();
                var deadline=DateTime.UtcNow+TimeSpan.FromSeconds(8);
                while(pilot.HasPending&&DateTime.UtcNow<deadline)
                {
                    long callStart=System.Diagnostics.Stopwatch.GetTimestamp();pilot.Poll();long callTicks=System.Diagnostics.Stopwatch.GetTimestamp()-callStart;
                    if(!pilot.HasPending&&pilot.LastSucceeded)publicationPollTicks=callTicks;
                    else Thread.Sleep(2);
                }
                Check(check,!pilot.HasPending&&pilot.LastSucceeded&&pilot.Completion!=null,"typed SQLite write is published and acknowledged after nonblocking polling");
                Metric(check,"completion Poll() including owner publication",publicationPollTicks);
                Metric(check,"writer-release to publication elapsed (includes worker/SQLite scheduling)",System.Diagnostics.Stopwatch.GetTimestamp()-writerRelease);
                double oilAdded=oracleOilAdded;double coolantAdded=oracleCoolantAdded;
                Check(check,live.cash==cashBefore-legacyPreview.cost&&live.revision==revisionBefore+1&&live.storageCommit==commitBefore+1&&
                    BitConverter.DoubleToInt64Bits(car.fluids.oilLitres)==BitConverter.DoubleToInt64Bits(oracleOilAfter)&&
                    BitConverter.DoubleToInt64Bits(car.fluids.coolantLitres)==BitConverter.DoubleToInt64Bits(oracleCoolantAfter),
                    "published cash, revisions and exact oil/coolant quantities match the independent legacy mutation oracle");
                var afterBits=FluidBits(car.fluids);
                Check(check,Enumerable.Range(0,beforeBits.Length).Where(i=>i!=0&&i!=1).All(i=>beforeBits[i]==afterBits[i]),"all other fluid bit fields remain unchanged by refill publication");
                var receipt=live.economyReceipts.LastOrDefault();
                Check(check,receipt!=null&&receipt.id==quote.Request.Identity.OperationId&&receipt.operation==PaymentKind.Fluids.ToString()&&
                    receipt.target==car.instanceId&&receipt.cashBefore==cashBefore&&receipt.cashAfter==cashBefore-legacyPreview.cost&&
                    receipt.utcTicks==quote.Request.IssuedUtcTicks&&
                    BitConverter.DoubleToInt64Bits(receipt.oilAdded)==BitConverter.DoubleToInt64Bits(oilAdded)&&
                    BitConverter.DoubleToInt64Bits(receipt.coolantAdded)==BitConverter.DoubleToInt64Bits(coolantAdded),
                    "published business receipt matches the independently priced command identity, target, tick, cash and exact additions");
                Check(check,Meta(path,"commit_id")==live.storageCommit.ToString(CultureInfo.InvariantCulture),"SQLite commit token independently matches the published owner");
                Check(check,live.economyReceipts.Count==256&&!live.economyReceipts.Any(item=>item.id=="s06c-fifo-oldest")&&
                    live.economyReceipts.Last().id==quote.Request.Identity.OperationId,
                    "full business history evicts only its oldest item and appends the command within the 256-entry limit");
                using(var db=new SqliteNative(path,true))
                    Check(check,db.Scalar("SELECT COUNT(*) FROM economy_receipts")=="256"&&
                        db.Scalar("SELECT id FROM economy_receipts ORDER BY receipt_order LIMIT 1")!="s06c-fifo-oldest"&&
                        db.Scalar("SELECT id FROM economy_receipts ORDER BY receipt_order DESC LIMIT 1")==quote.Request.Identity.OperationId,
                        "independent typed receipt rows preserve FIFO ordering and the bounded window");

                // A separate fresh owner proves that quote terms cannot survive a new selected reference.
                ProfileStore.InvalidateOwnerProof();var replacement=ProfileStore.Load();
                Check(check,!pilot.TryBegin(quote,quote.Request.IssuedUtcTicks,out _),"quote from an old owner reference cannot dispatch after reload");
                Check(check,replacement.cash==live.cash&&replacement.SelectedCar.fluids.oilLitres==live.SelectedCar.fluids.oilLitres,"stale-owner rejection leaves durable state unchanged");
                replacement=VerifyUnknownAndReconcile(replacement,policy,path,check);
                replacement=VerifyDefinitePreCommitFailure(replacement,policy,path,check);
                replacement=VerifyPublicationReadSetRecovery(replacement,policy,path,check);
                VerifyHistoricalReplayRecovery(replacement,policy,path,check);
                UnityEngine.Debug.Log("S06c typed fluid refill pilot checks passed: "+passed.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                SqliteStateStore.FailureInjection=priorHook;SqliteStateStore.Measurement=priorMeasure;
                ProfileStore.InvalidateOwnerProof();
                typeof(ProfileStore).GetField("typedOwnerLease",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).SetValue(null,null);
                typeof(ProfileStore).GetField("controlledRecoveryLease",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).SetValue(null,null);
                try { if(ProfileStore.VerificationPath!=priorPath)ProfileStore.VerificationPath=priorPath; } catch { }
                try { if(ProfileStore.SelectedSlot!=priorSlot)ProfileStore.UseSlot(priorSlot); } catch { }
                typeof(ProfileStore).GetField("status",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).SetValue(null,priorStatus);
                SqliteStateStore.Invalidate();
            }
        }
    }
}
#endif
