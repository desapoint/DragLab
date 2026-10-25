using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    /// <summary>Immutable, owner-bound display terms for one manual refill.</summary>
    internal sealed class TypedFluidRefillQuote
    {
        internal readonly int Cost, CashBefore;
        internal readonly double OilAdded, CoolantAdded;
        internal readonly string CarId;
        internal readonly FluidRefillRequest Request;
        internal readonly FluidRefillReadSet ReadSet;
        internal readonly TypedSaveSlotBinding Binding;
        internal readonly SqliteStateOwnerReceipt OwnerReceipt;
        internal readonly long OwnerEpoch;
        internal readonly OperatingPolicy Policy;
        internal readonly MechanicalCatalog Catalog;
        internal readonly string OilPanPartId, CoolingPartId;
        internal readonly double OilPanPartCapacity, CoolingPartCapacity;
        internal TypedFluidRefillQuote(FluidRefillResult plan, TypedSaveSlotBinding binding,
            SqliteStateOwnerReceipt ownerReceipt, long ownerEpoch, OperatingPolicy policy,
            MechanicalCatalog catalog,string oilPanPartId,double oilPanPartCapacity,string coolingPartId,double coolingPartCapacity)
        {
            if (plan == null || binding == null || ownerReceipt == null) throw new ArgumentNullException();
            Cost=plan.Cost; CashBefore=plan.CashBefore; OilAdded=plan.OilAdded; CoolantAdded=plan.CoolantAdded;
            CarId=plan.Request.CarId; Request=plan.Request; ReadSet=plan.ReadSet; Binding=binding;
            OwnerReceipt=ownerReceipt; OwnerEpoch=ownerEpoch; Policy=TypedFluidRefillPilot.CopyPolicy(policy);
            Catalog=catalog;OilPanPartId=oilPanPartId;OilPanPartCapacity=oilPanPartCapacity;
            CoolingPartId=coolingPartId;CoolingPartCapacity=coolingPartCapacity;
        }
    }

    /// <summary>
    /// Main-thread pilot for the manual oil/coolant command. Workers receive only immutable command
    /// values. The small owner-side patch remains private and is applied only after durable proof.
    /// </summary>
    internal sealed class TypedFluidRefillPilot
    {
        const int QuoteCacheLimit = 8;
        readonly Func<PlayerProfile> currentProfile;
        readonly Func<OperatingPolicy> currentPolicy;
        readonly int ownerThreadId;
        readonly TypedFluidRefillCoordinator coordinator;
        readonly Dictionary<QuoteKey, TypedFluidRefillQuote> quotes = new Dictionary<QuoteKey, TypedFluidRefillQuote>();
        ProfileStore.TypedOwnerLease lease;
        TypedFluidRefillCommand command;
        PublicationPatch patch;
        TypedFluidRefillResult completion;
        TypedFluidRefillStatus status;
        bool lastSucceeded;
        bool needsOwnerReload;
        bool recoveringCompletion;
        string operationId, error;

        sealed class PublicationPatch
        {
            internal PlayerProfile Profile;
            internal OwnedCar Car;
            internal string Slot, Path, PhysicsIdentity;
            internal SqliteStateOwnerReceipt OwnerReceipt;
            internal long Epoch, BeforeRevision, BeforeCommit;
            internal FluidRefillReadSet ReadSet;
            internal FluidRefillFluidValues BeforeFluids;
            internal OperatingPolicy Policy;
            internal FluidRefillRequest Request;
            internal int CashBefore;
            internal List<EconomyReceipt> Receipts;
            internal List<EconomyReceipt> OriginalReceipts;
            internal int AddedReceiptIndex;
        }

        struct QuoteKey : IEquatable<QuoteKey>
        {
            readonly PlayerProfile profile; readonly long epoch, revision, commit;
            readonly TypedSaveSlotBinding binding; readonly string car, policyId;
            readonly long oil, coolant, oilPrice, coolantPrice, quoteSeconds, policyVersion;
            internal QuoteKey(PlayerProfile p,long e,long r,long c,TypedSaveSlotBinding b,string id,
                double o,double w,OperatingPolicy policy)
            { profile=p;epoch=e;revision=r;commit=c;binding=b;car=id;oil=Bits(o);coolant=Bits(w);
              oilPrice=Bits(policy.oilPerLitre);coolantPrice=Bits(policy.coolantPerLitre);quoteSeconds=policy.quoteSeconds;policyId=policy.id;policyVersion=policy.version; }
            public bool Equals(QuoteKey x)=>ReferenceEquals(profile,x.profile)&&epoch==x.epoch&&revision==x.revision&&commit==x.commit&&
                binding.Equals(x.binding)&&car==x.car&&oil==x.oil&&coolant==x.coolant&&oilPrice==x.oilPrice&&coolantPrice==x.coolantPrice&&
                quoteSeconds==x.quoteSeconds&&policyVersion==x.policyVersion&&policyId==x.policyId;
            public override bool Equals(object obj)=>obj is QuoteKey x&&Equals(x);
            public override int GetHashCode(){unchecked{return ((((((RuntimeHelpers.GetHashCode(profile)*397^(int)epoch)*397^binding.GetHashCode())*397^(car??"").GetHashCode())*397^oil.GetHashCode())*397^coolant.GetHashCode())*397^oilPrice.GetHashCode()^coolantPrice.GetHashCode()^quoteSeconds.GetHashCode())*397^policyVersion.GetHashCode()^((policyId??"").GetHashCode());}}
            static long Bits(double value)=>BitConverter.DoubleToInt64Bits(value);
        }

        internal TypedFluidRefillPilot(Func<PlayerProfile> currentProfile, Func<OperatingPolicy> currentPolicy,
            ITypedFluidRefillExecutor executor=null, ITypedFluidRefillScheduler scheduler=null)
        {
            this.currentProfile=currentProfile??throw new ArgumentNullException(nameof(currentProfile));
            this.currentPolicy=currentPolicy??throw new ArgumentNullException(nameof(currentPolicy));
            ownerThreadId=Thread.CurrentThread.ManagedThreadId;
            coordinator=new TypedFluidRefillCoordinator(executor??new SqliteTypedFluidRefillExecutor(),scheduler);
            status=TypedFluidRefillStatus.NotCommitted;
        }

        internal bool HasPending { get { CheckOwner(); return coordinator.HasPending; } }
        internal bool CanRetry { get { CheckOwner(); return coordinator.PendingState==TypedFluidRefillCoordinatorState.OutcomeUnknown; } }
        internal bool NeedsRecovery { get { CheckOwner(); return needsOwnerReload||
            coordinator.PendingState==TypedFluidRefillCoordinatorState.CommittedAwaitingHandling&&!lastSucceeded; } }
        internal TypedFluidRefillStatus Status { get { CheckOwner(); return status; } }
        internal TypedFluidRefillResult Completion { get { CheckOwner(); return completion; } }
        internal bool LastSucceeded { get { CheckOwner(); return lastSucceeded; } }
        internal string OperationId { get { CheckOwner(); return operationId; } }
        internal string Error { get { CheckOwner(); return error; } }

        internal bool TryQuote(PlayerProfile profile,string carId,double oil,double coolant,long nowTicks,
            out TypedFluidRefillQuote quote,out string quoteError)
        {
            CheckOwner(); quote=null; quoteError=null;
            try
            {
                if(profile==null||!ReferenceEquals(profile,currentProfile()))throw new InvalidOperationException("The selected profile changed.");
                if(coordinator.HasPending)throw new InvalidOperationException("A fluid refill is already pending.");
                if(!ProfileStore.TryGetCommandOwner(profile,out var binding,out var receipt))
                { RetainReloadContext(profile); throw new InvalidOperationException("The selected profile has no current typed owner proof."); }
                var policy=currentPolicy(); if(policy==null)throw new InvalidOperationException("Fluid policy is unavailable."); policy.Validate();
                var key=new QuoteKey(profile,ProfileStore.OwnerEpoch,profile.revision,profile.storageCommit,binding,carId,oil,coolant,policy);
                bool staleCachedTerms=false;
                foreach(var item in quotes) if(item.Key.Equals(key)&&nowTicks>=item.Value.Request.IssuedUtcTicks&&nowTicks<item.Value.Request.ExpiresUtcTicks)
                {
                    if(ReadSetStillCurrent(profile,item.Value,policy,binding,receipt,ProfileStore.OwnerEpoch)) { quote=item.Value; return true; }
                    // Keep the same input key available for a fresh immutable quote, but
                    // never return display terms whose scoped live facts have changed.
                    staleCachedTerms=true; break;
                }
                if(staleCachedTerms)quotes.Remove(key);
                long expiry=checked(nowTicks+TimeSpan.FromSeconds(policy.quoteSeconds).Ticks);
                var identity=new StateCommandIdentity(binding.SaveSlot,binding.ResetGeneration,MechanicsOwnership.NewId("fluid"));
                var versions=new StateCommandVersionFence(profile.revision,profile.storageCommit,MechanicalCatalog.Version);
                var request=new FluidRefillRequest(identity,versions,carId,oil,coolant,nowTicks,expiry);
                var input=FluidRefillPlanner.Capture(profile,policy,request,nowTicks);
                var plan=FluidRefillPlanner.Plan(input);
                if(!plan.Committable)throw new InvalidOperationException(plan.Error);
                CapacityPartFacts(profile,carId,PartCategory.OilPan,out string oilPanId,out double oilPanCapacity);
                CapacityPartFacts(profile,carId,PartCategory.Cooling,out string coolingId,out double coolingCapacity);
                quote=new TypedFluidRefillQuote(plan,binding,receipt,ProfileStore.OwnerEpoch,policy,
                    profile.catalog,oilPanId,oilPanCapacity,coolingId,coolingCapacity);
                if(quotes.Count>=QuoteCacheLimit)quotes.Clear();
                quotes[key]=quote; return true;
            }
            catch(Exception ex){quote=null;quoteError=ex.Message;return false;}
        }

        // This is intentionally a direct, bounded read of the facts used by the S04
        // planner. Quote rendering may call it every frame, so do not run Resolve,
        // planner Capture, JSON, or a profile clone on a cache hit. A cache miss still
        // takes the authoritative full Capture/Plan path below.
        static bool ReadSetStillCurrent(PlayerProfile profile,TypedFluidRefillQuote quote,OperatingPolicy policy,
            TypedSaveSlotBinding binding,SqliteStateOwnerReceipt receipt,long ownerEpoch)
        {
            var expected=quote.ReadSet;
            if(profile==null||expected==null||receipt==null||!quote.Binding.Equals(binding)||quote.OwnerEpoch!=ownerEpoch||
                quote.Request.Versions.ExpectedContentVersion!=MechanicalCatalog.Version||
                receipt.CommitId!=quote.OwnerReceipt.CommitId||receipt.Generation!=quote.OwnerReceipt.Generation||
                receipt.LedgerRevision!=quote.OwnerReceipt.LedgerRevision||receipt.ContentRevision!=quote.OwnerReceipt.ContentRevision||
                !string.Equals(receipt.PhysicsIdentity,quote.OwnerReceipt.PhysicsIdentity,StringComparison.Ordinal)||
                profile.cash!=expected.Cash||
                profile.lastEconomyUtcTicks!=expected.LastEconomyUtcTicks||
                profile.runCheckpoints==null||profile.economyReceipts==null||
                profile.economyReceipts.Count!=expected.EconomyReceiptCount||
                (profile.economyReceipts.Count==0?null:profile.economyReceipts[0]?.id)!=expected.OldestEconomyReceiptId||
                !Same(policy.oilPerLitre,expected.OilPerLitre)||!Same(policy.coolantPerLitre,expected.CoolantPerLitre)) return false;
            bool unsettled=false,operationRecorded=false;
            foreach(var row in profile.runCheckpoints){if(row==null)return false;if(!row.settled)unsettled=true;}
            foreach(var row in profile.economyReceipts){if(row==null)return false;if(row.id==quote.Request.Identity.OperationId)operationRecorded=true;}
            if(unsettled!=expected.HasUnsettledRun||operationRecorded!=expected.OperationAlreadyRecorded)return false;
            var car=profile.cars?.Find(row=>row!=null&&row.instanceId==expected.CarId);
            if(car==null||car.vehicleId!=expected.VehicleId||car.engineAssemblyId!=expected.EngineAssemblyId||
                car.garageJobId!=expected.GarageJobId||(car.engineRebuild!=null)!=expected.HasRebuild||
                (car.engineRebuild!=null&&car.engineRebuild.active)!=expected.RebuildActive||car.fluids==null||
                string.IsNullOrEmpty(car.engineAssemblyId)) return false;
            var engine=profile.engines?.Find(row=>row!=null&&row.instanceId==car.engineAssemblyId);
            if(engine==null||engine.parentVehicleId!=car.instanceId||engine.vehicleDefinitionId!=car.vehicleId||
                engine.slots==null||engine.slots.Count!=0) return false;
            var fluids=expected.Fluids;
            if(fluids==null||!Same(car.fluids.oilLitres,fluids.OilLitres)||!Same(car.fluids.coolantLitres,fluids.CoolantLitres)||
                !Same(car.fluids.oilTemperatureC,fluids.OilTemperatureC)||!Same(car.fluids.oilPressureBar,fluids.OilPressureBar)||
                !Same(car.fluids.coolantPressureBar,fluids.CoolantPressureBar)||!Same(car.fluids.oilCondition,fluids.OilCondition)||
                !Same(car.fluids.oilConsumedLitres,fluids.OilConsumedLitres)||!Same(car.fluids.oilLeakedLitres,fluids.OilLeakedLitres)||
                !Same(car.fluids.coolantLostLitres,fluids.CoolantLostLitres)||!Same(car.fluids.serviceElapsedSeconds,fluids.ServiceElapsedSeconds)||
                !Same(car.fluids.oilBurnLps,fluids.OilBurnLps)||!Same(car.fluids.oilLeakLps,fluids.OilLeakLps)||
                !Same(car.fluids.coolantLeakLps,fluids.CoolantLeakLps)||!Same(car.fluids.coolantVentLps,fluids.CoolantVentLps)) return false;
            if(!ReferenceEquals(profile.catalog,quote.Catalog)||profile.parts==null||car.installed?.slots==null)return false;
            return CapacityPartStillCurrent(profile,car,PartCategory.OilPan,quote.OilPanPartId,quote.OilPanPartCapacity)&&
                CapacityPartStillCurrent(profile,car,PartCategory.Cooling,quote.CoolingPartId,quote.CoolingPartCapacity);
        }

        static bool CapacityPartStillCurrent(PlayerProfile profile,OwnedCar car,PartCategory category,string expectedId,double expectedCapacity)
        {
            string slotName=category.ToString();string currentId=null;bool found=false;
            foreach(var slot in car.installed.slots)
            {
                if(slot==null)return false;
                if(slot.slot!=slotName)continue;
                if(found)return false;
                found=true;currentId=slot.partInstanceId;
            }
            if(!string.Equals(currentId,expectedId,StringComparison.Ordinal))return false;
            if(currentId==null)return true;
            var part=profile.parts.Find(row=>row!=null&&row.instanceId==currentId);
            var definition=part==null?null:profile.catalog.Find(part.definitionId);
            return definition!=null&&definition.category==category&&Same(definition.fluidCapacityLitres,expectedCapacity);
        }

        static void CapacityPartFacts(PlayerProfile profile,string carId,PartCategory category,out string partId,out double capacity)
        {
            partId=null;capacity=0;
            var car=profile.cars.Find(row=>row!=null&&row.instanceId==carId);
            if(car?.installed?.slots==null||profile.parts==null||profile.catalog==null)
                throw new InvalidOperationException("Refill capacity owner is unavailable.");
            SlotBinding found=null;
            foreach(var slot in car.installed.slots)if(slot!=null&&slot.slot==category.ToString())
            { if(found!=null)throw new InvalidOperationException("Duplicate capacity part binding.");found=slot; }
            if(found==null)return;
            var part=profile.parts.Find(row=>row!=null&&row.instanceId==found.partInstanceId);
            var definition=part==null?null:profile.catalog.Find(part.definitionId);
            if(definition==null||definition.category!=category)throw new InvalidOperationException("Refill capacity part is invalid.");
            partId=part.instanceId;capacity=definition.fluidCapacityLitres;
        }

        static bool Same(double a,double b)=>BitConverter.DoubleToInt64Bits(a)==BitConverter.DoubleToInt64Bits(b);

        internal bool TryBegin(TypedFluidRefillQuote quote,long acceptedTicks,out string beginError)
        {
            CheckOwner(); beginError=null;
            if(quote==null){beginError="A current refill quote is required.";return false;}
            if(coordinator.HasPending){beginError="A fluid refill is already retained.";return false;}
            var profile=currentProfile();
            if(profile==null||!ProfileStore.TryGetCommandOwner(profile,out var binding,out var receipt))
            { RetainReloadContext(profile); beginError="The selected profile has no current typed owner proof.";return false; }
            if(!ReferenceEquals(profile,currentProfile())||!binding.Equals(quote.Binding)||ProfileStore.OwnerEpoch!=quote.OwnerEpoch||
                receipt.CommitId!=quote.OwnerReceipt.CommitId||receipt.LedgerRevision!=quote.OwnerReceipt.LedgerRevision||
                receipt.ContentRevision!=quote.OwnerReceipt.ContentRevision||receipt.Generation!=quote.OwnerReceipt.Generation||
                !string.Equals(receipt.PhysicsIdentity,quote.OwnerReceipt.PhysicsIdentity,StringComparison.Ordinal))
            { beginError="The selected save owner or shown quote changed.";return false; }
            var policy=currentPolicy(); if(policy==null){beginError="Fluid policy is unavailable.";return false;}
            error=null; lastSucceeded=false; completion=null; needsOwnerReload=false; recoveringCompletion=false;
            PublicationPatch staged=null;
            var fallbackContext=new PublicationPatch { Profile=profile,Slot=quote.Binding.SaveSlot,Path=quote.Binding.CanonicalPath,
                Epoch=quote.OwnerEpoch,PhysicsIdentity=quote.OwnerReceipt.PhysicsIdentity,OwnerReceipt=quote.OwnerReceipt,
                BeforeRevision=profile.revision,BeforeCommit=profile.storageCommit,CashBefore=profile.cash,
                ReadSet=quote.ReadSet,BeforeFluids=quote.ReadSet.Fluids,Request=quote.Request,Policy=quote.Policy };
            try
            {
                bool began=ProfileStore.TryBeginTypedOperation(profile,coordinator,tick=>
                {
                    if(tick!=acceptedTicks||tick<quote.Request.IssuedUtcTicks||tick>=quote.Request.ExpiresUtcTicks)
                        throw new InvalidOperationException("Payment quote stale/expired.");
                    policy.Validate();
                    if(policy.id!=quote.Policy.id||policy.version!=quote.Policy.version)
                        throw new InvalidOperationException("Operating policy changed; request a new quote.");
                    var fresh=FluidRefillPlanner.Capture(profile,policy,quote.Request,tick);
                    if(!quote.Request.Equals(fresh.Request)||!quote.ReadSet.Equals(fresh.ReadSet))
                        throw new InvalidOperationException("The shown refill dependencies changed; request a new quote.");
                    var plan=FluidRefillPlanner.Plan(fresh);
                    if(!plan.Committable||plan.Cost!=quote.Cost||plan.CashBefore!=quote.CashBefore||
                        !TypedFluidRefillIntent.BitsEqual(plan.OilAdded,quote.OilAdded)||!TypedFluidRefillIntent.BitsEqual(plan.CoolantAdded,quote.CoolantAdded))
                        throw new InvalidOperationException("The shown refill terms changed; request a new quote.");
                    var car=profile.cars.Find(c=>c.instanceId==quote.CarId);
                    if(car==null||car.fluids==null||profile.economyReceipts==null||profile.economyReceipts.Count>256)
                        throw new InvalidOperationException("The refill publication target is unavailable.");
                    var receipts=new List<EconomyReceipt>(profile.economyReceipts);
                    if(receipts.Count==256)receipts.RemoveAt(0);
                    var delta=plan.Receipt;
                    receipts.Add(new EconomyReceipt { id=delta.Id,operation=delta.Operation,target=delta.Target,revision=delta.Revision,
                        utcTicks=delta.UtcTicks,cashBefore=delta.CashBefore,cashAfter=delta.CashAfter,fuelAdded=delta.FuelAdded,
                        oilAdded=delta.OilAdded,coolantAdded=delta.CoolantAdded,disposed=Array.Empty<string>(),retained=Array.Empty<string>() });
                    staged=new PublicationPatch { Profile=profile,Car=car,Slot=quote.Binding.SaveSlot,Path=quote.Binding.CanonicalPath,
                        Epoch=quote.OwnerEpoch,PhysicsIdentity=quote.OwnerReceipt.PhysicsIdentity,OwnerReceipt=quote.OwnerReceipt,
                        BeforeRevision=profile.revision,BeforeCommit=profile.storageCommit,
                        Request=quote.Request,
                        ReadSet=quote.ReadSet,BeforeFluids=new FluidRefillFluidValues(car.fluids),Policy=quote.Policy,CashBefore=profile.cash,
                        Receipts=receipts,OriginalReceipts=new List<EconomyReceipt>(profile.economyReceipts),AddedReceiptIndex=receipts.Count-1 };
                    var created=TypedFluidRefillCommand.Create(quote.Binding,plan,quote.OwnerReceipt.PhysicsIdentity,tick);
                    command=created; return created;
                },acceptedTicks,out lease,out beginError);
                if(!began)
                {
                    if(lease==null&&(!CanRestoreOwner(profile,fallbackContext)||!ProfileStore.RegisterValidatedOwner(profile,
                        quote.Binding.SaveSlot,quote.Binding.CanonicalPath,quote.OwnerEpoch,quote.OwnerReceipt)))
                    { patch=staged??fallbackContext;needsOwnerReload=true; }
                    return false;
                }
                patch=staged; operationId=command.Intent.Request.Identity.OperationId; status=TypedFluidRefillStatus.OutcomeUnknown; quotes.Clear();
                return true;
            }
            catch(Exception ex)
            {
                beginError=ex.Message;
                if(lease==null&&(!CanRestoreOwner(profile,fallbackContext)||!ProfileStore.RegisterValidatedOwner(profile,
                    quote.Binding.SaveSlot,quote.Binding.CanonicalPath,quote.OwnerEpoch,quote.OwnerReceipt)))
                {patch=staged??fallbackContext;needsOwnerReload=true;}
                return false;
            }
        }

        internal void Poll()
        {
            CheckOwner(); coordinator.Poll();
            if(!coordinator.HasPending)return;
            var outcome=coordinator.PendingOutcome;
            if(outcome==null)return;
            status=outcome.Status; error=outcome.Error;
            if(outcome.Status==TypedFluidRefillStatus.OutcomeUnknown)return;
            if(outcome.Status!=TypedFluidRefillStatus.Committed&&outcome.Status!=TypedFluidRefillStatus.Replayed)
            {
                lastSucceeded=false; completion=null; coordinator.Acknowledge(false,out _);
                if(lease!=null)
                {
                    var rejectedPatch=patch;
                    bool released=ProfileStore.ReleaseTypedLease(lease); lease=released?null:lease;
                    bool restored=released&&CanRestoreOwner(currentProfile(),rejectedPatch)&&
                        ProfileStore.RegisterValidatedOwner(rejectedPatch.Profile,rejectedPatch.Slot,rejectedPatch.Path,
                            rejectedPatch.Epoch,rejectedPatch.OwnerReceipt);
                    needsOwnerReload=!restored;
                }
                if(!needsOwnerReload)patch=null;
                return;
            }
            completion=outcome.Result; recoveringCompletion=true;
            bool publishable=false;
            try { publishable=patch!=null&&outcome.Result!=null&&outcome.ObservedStorageCommit.HasValue&&
                outcome.ObservedStorageCommit.Value==outcome.Result.CommittedStorageCommit&&
                !(outcome.Status==TypedFluidRefillStatus.Replayed&&outcome.Result.BusinessReceipt==null&&
                    outcome.ObservedStorageCommit.Value==outcome.Result.CommittedStorageCommit)&&CanPublish(patch,outcome.Result); }
            catch(Exception ex){error=ex.Message;}
            if(!publishable)
            { patch=null; lastSucceeded=false; error="The refill is durable but the selected owner changed; explicit recovery is required."; return; }
            try
            {
                var result=outcome.Result;
                // Historical replay can carry the database's original receipt. Replace only
                // this operation's row in the prebuilt bounded list, never project a profile.
                List<EconomyReceipt> publishReceipts;
                if(result.BusinessReceipt!=null)
                {
                    if(result.BusinessReceipt.Id!=result.OperationId||result.BusinessReceipt.Operation!=PaymentKind.Fluids.ToString()||
                        result.BusinessReceipt.Target!=result.CarId||result.BusinessReceipt.CashBefore!=patch.CashBefore||
                        result.BusinessReceipt.CashAfter!=result.CashAfter||
                        (long)result.BusinessReceipt.CashBefore-result.BusinessReceipt.CashAfter!=result.Cost||
                        result.BusinessReceipt.Revision!=result.CommittedLedgerRevision||result.BusinessReceipt.UtcTicks!=result.BusinessReceiptUtcTicks||
                        result.BusinessReceipt.FuelAddedBits!=BitConverter.DoubleToInt64Bits(0)||
                        result.BusinessReceipt.OilAddedBits!=BitConverter.DoubleToInt64Bits(result.OilAdded)||
                        result.BusinessReceipt.CoolantAddedBits!=BitConverter.DoubleToInt64Bits(result.CoolantAdded)||
                        result.BusinessReceipt.DisposedIds().Length!=0||result.BusinessReceipt.RetainedIds().Length!=0)
                        throw new InvalidOperationException("Stored receipt facts do not match the committed refill result.");
                    var restoredReceipt=result.BusinessReceipt.Restore();
                    var preparedReceipts=patch.Receipts;
                    preparedReceipts[patch.AddedReceiptIndex]=restoredReceipt;
                    publishReceipts=preparedReceipts;
                }
                else if(outcome.Status==TypedFluidRefillStatus.Replayed)
                    publishReceipts=patch.OriginalReceipts;
                else publishReceipts=patch.Receipts;
                var ownerReceipt=new SqliteStateOwnerReceipt(result.CommittedStorageCommit,lease.Coordinator.PendingIntent.ExpectedBinding.ResetGeneration,
                    result.CommittedLedgerRevision,MechanicalCatalog.Version,patch.PhysicsIdentity);
                var captured=patch;
                bool published=ProfileStore.TryPublishTypedRefill(lease,ownerReceipt,captured.BeforeRevision,captured.BeforeCommit,()=>
                {
                    captured.Profile.cash=result.CashAfter; captured.Profile.revision=result.CommittedLedgerRevision;
                    captured.Profile.storageCommit=result.CommittedStorageCommit; captured.Profile.lastEconomyUtcTicks=result.AcceptedTick;
                    captured.Car.fluids.oilLitres=result.OilAfter; captured.Car.fluids.coolantLitres=result.CoolantAfter;
                    captured.Profile.economyReceipts=publishReceipts;
                },out var publishError);
                if(!published){error=publishError;lastSucceeded=false;return;}
                lease=null;completion=result;lastSucceeded=true;patch=null;needsOwnerReload=false;recoveringCompletion=false;
                error=null;
            }
            catch(Exception ex){
                // If the lease was already released, the durable database remains the
                // recovery authority; preserve the completion and offer an explicit reload.
                if(lease==null)needsOwnerReload=true;
                lastSucceeded=false;error="The refill committed; publication requires explicit recovery: "+ex.Message;
            }
        }

        bool CanPublish(PublicationPatch p,TypedFluidRefillResult result)
        {
            var profile=currentProfile();
            if(!ReferenceEquals(profile,p.Profile)||lease==null||!ReferenceEquals(lease.Profile,p.Profile)||
                ProfileStore.OwnerEpoch!=p.Epoch||ProfileStore.SelectedSlot!=p.Slot||
                !string.Equals(System.IO.Path.GetFullPath(ProfileStore.FilePath),System.IO.Path.GetFullPath(p.Path),
                    System.IO.Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)||
                profile.revision!=p.BeforeRevision||profile.storageCommit!=p.BeforeCommit||profile.cash!=p.CashBefore||
                result.Cost<0||(long)result.CashAfter+result.Cost!=p.CashBefore||
                result.CommittedLedgerRevision!=p.BeforeRevision+1||result.CommittedStorageCommit!=p.BeforeCommit+1||
                result.OilAdded<0||result.CoolantAdded<0||result.OilAdded>command.Request.RequestedOilLitres||
                result.CoolantAdded>command.Request.RequestedCoolantLitres||
                !TypedFluidRefillIntent.BitsEqual(result.OilAfter,p.BeforeFluids.OilLitres+result.OilAdded)||
                !TypedFluidRefillIntent.BitsEqual(result.CoolantAfter,p.BeforeFluids.CoolantLitres+result.CoolantAdded))return false;
            var current=FluidRefillPlanner.Capture(profile,p.Policy,command.Request,command.Intent.AcceptedTick);
            return p.ReadSet.Equals(current.ReadSet)&&ReferenceEquals(p.Car,profile.cars.Find(c=>c.instanceId==result.CarId))&&
                p.BeforeFluids.Equals(current.ReadSet.Fluids);
        }

        void RetainReloadContext(PlayerProfile profile)
        {
            if(profile==null||!ProfileStore.CanLoadSelectedOwner())return;
            patch=new PublicationPatch { Profile=profile,Slot=ProfileStore.SelectedSlot,Path=System.IO.Path.GetFullPath(ProfileStore.FilePath),
                Epoch=ProfileStore.OwnerEpoch };
            needsOwnerReload=true;recoveringCompletion=false;completion=null;lastSucceeded=false;operationId=null;
        }

        bool CanRestoreOwner(PlayerProfile profile,PublicationPatch context)
        {
            if(profile==null||context==null||context.ReadSet==null||context.Request==null||context.Policy==null||
                !ReferenceEquals(profile,context.Profile)||ProfileStore.OwnerEpoch!=context.Epoch||ProfileStore.SelectedSlot!=context.Slot||
                !string.Equals(System.IO.Path.GetFullPath(ProfileStore.FilePath),System.IO.Path.GetFullPath(context.Path),
                    System.IO.Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)||
                profile.revision!=context.BeforeRevision||profile.storageCommit!=context.BeforeCommit||profile.cash!=context.CashBefore)
                return false;
            try
            {
                var current=FluidRefillPlanner.Capture(profile,context.Policy,context.Request,context.Request.IssuedUtcTicks);
                return context.ReadSet.Equals(current.ReadSet);
            }
            catch { return false; }
        }

        internal static OperatingPolicy CopyPolicy(OperatingPolicy source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            return new OperatingPolicy { id=source.id,version=source.version,quoteSeconds=source.quoteSeconds,
                oilPerLitre=source.oilPerLitre,coolantPerLitre=source.coolantPerLitre };
        }

        internal bool TryReconcile(out string reconcileError)
        {
            CheckOwner(); var ok=coordinator.TryReconcile(out reconcileError);
            if(ok){status=TypedFluidRefillStatus.OutcomeUnknown;error=null;}
            return ok;
        }

        internal bool TryRecover(out PlayerProfile recovered,out string recoveryError)
        {
            CheckOwner(); recovered=null; recoveryError=null;
            if(!NeedsRecovery){recoveryError="No retained owner state requires recovery.";return false;}
            if(lease==null)
            {
                var context=patch;
                if(!needsOwnerReload||context==null||coordinator.HasPending||ProfileStore.OwnerEpoch!=context.Epoch||
                    ProfileStore.SelectedSlot!=context.Slot||!string.Equals(System.IO.Path.GetFullPath(ProfileStore.FilePath),
                        System.IO.Path.GetFullPath(context.Path),System.IO.Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))
                { recoveryError="The selected owner context changed; this retained result cannot be reloaded here.";return false; }
                try
                {
                    var loaded=ProfileStore.Load(false);
                    if(!ProfileStore.TryGetCommandOwner(loaded,out _,out var loadedReceipt))
                    { recoveryError="The explicit load did not return a validated selected owner.";return false; }
                    if(recoveringCompletion&&completion!=null&&(loadedReceipt.Generation!=completion.ResetGeneration||
                        loadedReceipt.CommitId<completion.CommittedStorageCommit||loadedReceipt.LedgerRevision<completion.CommittedLedgerRevision||
                        loaded.storageCommit!=loadedReceipt.CommitId||loaded.revision!=loadedReceipt.LedgerRevision||
                        (loadedReceipt.CommitId==completion.CommittedStorageCommit&&!HasOperationReceipt(loaded,completion.OperationId))||
                        (HasOperationReceipt(loaded,completion.OperationId)&&!MatchesOperationReceipt(loaded,completion))))
                    { recoveryError="The explicit load did not contain a current owner at or after the committed refill.";return false; }
                    recovered=loaded; needsOwnerReload=false; patch=null; error=null;
                    if(recoveringCompletion){lastSucceeded=true;recoveringCompletion=false;}
                    return true;
                }
                catch(Exception ex){recoveryError="Explicit owner reload failed: "+ex.Message;return false;}
            }
            if(coordinator.PendingState!=TypedFluidRefillCoordinatorState.CommittedAwaitingHandling||completion==null||!recoveringCompletion)
            { recoveryError="Only a known committed refill can use controlled recovery.";return false; }
            if(!ProfileStore.TryControlledRecoveryLoad(lease,out recovered,out var receipt,out recoveryError)){recovered=null;return false;}
            if(receipt==null||receipt.Generation!=lease.Coordinator.PendingIntent.ExpectedBinding.ResetGeneration||
                receipt.CommitId<completion.CommittedStorageCommit||receipt.LedgerRevision<completion.CommittedLedgerRevision||
                recovered.storageCommit!=receipt.CommitId||recovered.revision!=receipt.LedgerRevision||
                (receipt.CommitId==completion.CommittedStorageCommit&&!HasOperationReceipt(recovered,completion.OperationId))||
                (HasOperationReceipt(recovered,completion.OperationId)&&!MatchesOperationReceipt(recovered,completion)))
            { ProfileStore.AbandonControlledRecoveryLoad(lease);recovered=null;recoveryError="Validated recovery did not return the current refill commit.";return false; }
            var profile=recovered;
            if(!ProfileStore.TryCompleteControlledRecovery(lease,profile,receipt,lease.Coordinator.PendingIntent.ExpectedBinding.ResetGeneration,
                completion.CommittedStorageCommit,completion.CommittedLedgerRevision,out recoveryError))
            { recovered=null;return false; }
            lease=null;
            patch=null;lastSucceeded=true;recoveringCompletion=false;error=null;return true;
        }

        static bool HasOperationReceipt(PlayerProfile profile,string id)
            => profile!=null&&profile.economyReceipts!=null&&profile.economyReceipts.Exists(item=>item!=null&&item.id==id);

        static bool MatchesOperationReceipt(PlayerProfile profile,TypedFluidRefillResult result)
        {
            if(!HasOperationReceipt(profile,result.OperationId))return false;
            foreach(var item in profile.economyReceipts)if(item!=null&&item.id==result.OperationId)
                return item.operation==PaymentKind.Fluids.ToString()&&item.target==result.CarId&&item.cashAfter==result.CashAfter&&
                    item.revision==result.CommittedLedgerRevision&&item.utcTicks==result.BusinessReceiptUtcTicks&&
                    (long)item.cashBefore-item.cashAfter==result.Cost&&
                    BitConverter.DoubleToInt64Bits(item.oilAdded)==BitConverter.DoubleToInt64Bits(result.OilAdded)&&
                    BitConverter.DoubleToInt64Bits(item.coolantAdded)==BitConverter.DoubleToInt64Bits(result.CoolantAdded)&&
                    BitConverter.DoubleToInt64Bits(item.fuelAdded)==BitConverter.DoubleToInt64Bits(0)&&
                    item.disposed!=null&&item.disposed.Length==0&&item.retained!=null&&item.retained.Length==0;
            return false;
        }

        void CheckOwner(){if(Thread.CurrentThread.ManagedThreadId!=ownerThreadId)throw new InvalidOperationException("Fluid refill pilot operations are main-thread-only.");}
    }
}
