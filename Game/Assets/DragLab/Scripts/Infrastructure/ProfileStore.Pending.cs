using System;
using System.Threading.Tasks;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;
namespace DragLab.Infrastructure
{
    public static partial class ProfileStore
    {
        // Observer for the verified player-baseline fixture only. Publishing on
        // the main thread prevents writer callbacks from racing the report.
        internal static Action<string,double> ValidatedSaveMeasurement;
        internal sealed class ValidatedSaveResult
        {
            internal long commit;
            internal SqliteStateStore.SaveTrace trace;
            internal SqliteStateOwnerReceipt ownerReceipt;
            internal string path, slot;
            internal long epoch;
        }
        sealed class ValidatedSaveTicket
        {
            internal PlayerProfile Candidate;
            internal string Path, Slot;
            internal long Epoch;
            internal long CandidateRevision, CandidateStorageCommit, ExpectedCommit;
            internal SqliteTypedProfileData CapturedTypedData;
            internal Task<ValidatedSaveResult> Task;
        }
        static ValidatedSaveTicket pendingValidatedSave;
        static bool IsValidatedSavePending() => pendingValidatedSave != null;

        internal static Task<ValidatedSaveResult> BeginValidatedSave(PlayerProfile candidate)
        {
            CheckCommandOwnerThread();
            if(candidate==null)throw new ArgumentNullException(nameof(candidate));
            if(pendingValidatedSave!=null)throw new InvalidOperationException("A validated save is already pending completion.");
            if(!CanBeginLegacyWrite()||writeBlocked)throw new InvalidOperationException("Save writes are blocked or another save operation is retained.");
            string saveSlot, path;long saveEpoch;CaptureSelectedContext(out saveSlot,out path,out saveEpoch);
            string build=Application.version;
            RaceCheckpointStore.Drain();string warning;
            RaceCheckpointStore.Merge(path,candidate,false,out warning);
            DrivingPreferences.Ensure(candidate);SqliteStateStore.Prepare();
            bool profileSave=(ValidatedSaveMeasurement!=null);
            var trace=profileSave?new SqliteStateStore.SaveTrace():null;
            long captureStart=profileSave?System.Diagnostics.Stopwatch.GetTimestamp():0;
            var snapshot=SqliteStateStore.LegacyCaptureManaged(candidate,build,trace);
            if(trace!=null)trace.Elapsed("owner complete capture",captureStart);
            long dispatched=profileSave?System.Diagnostics.Stopwatch.GetTimestamp():0;
            var task=Task.Run(()=>{
                long workStart=profileSave?System.Diagnostics.Stopwatch.GetTimestamp():0;
                if(trace!=null)trace.Elapsed("worker dispatch wait",dispatched);
                SqliteStateStore.WriteLegacy(path,snapshot);
                if(trace!=null)trace.Elapsed("entire save worker",workStart);
                return new ValidatedSaveResult{commit=snapshot.committed,trace=trace,ownerReceipt=snapshot.OwnerReceipt,path=path,slot=saveSlot,epoch=saveEpoch};
            });
            pendingValidatedSave=new ValidatedSaveTicket{Candidate=candidate,Path=path,Slot=saveSlot,Epoch=saveEpoch,
                CandidateRevision=candidate.revision,CandidateStorageCommit=candidate.storageCommit,ExpectedCommit=snapshot.expectedCommit,
                CapturedTypedData=snapshot.typedData,Task=task};
            return task;
        }
        internal static void CompleteValidatedSave(PlayerProfile candidate,Task<ValidatedSaveResult> save)
        {
            CheckCommandOwnerThread();
            var ticket=pendingValidatedSave;
            if(ticket==null||!ReferenceEquals(ticket.Candidate,candidate)||!ReferenceEquals(ticket.Task,save))
                throw new InvalidOperationException("Validated save completion does not match its captured candidate and ticket.");
            try
            {
                var completed=save.GetAwaiter().GetResult();
                if(completed==null||completed.path!=ticket.Path||completed.slot!=ticket.Slot||completed.epoch!=ticket.Epoch||
                    !ContextMatches(ticket.Slot,ticket.Path,ticket.Epoch)||!ReferenceEquals(candidate,ticket.Candidate))
                    throw new InvalidOperationException("Validated save completion no longer matches the selected owner context.");
                candidate.storageCommit=completed.commit;
                bool retired=true;string retireError=null;try{RaceCheckpointStore.Retire(ticket.Path,false);}catch(Exception error){retired=false;retireError=error.Message;}
                if(retired)
                {
                    MarkSelectedCheckpointVerifiedClean();
                    bool candidateStillMatches = candidate.revision==ticket.CandidateRevision &&
                        ticket.CandidateStorageCommit==ticket.ExpectedCommit && TypedCaptureMatches(candidate,ticket.CapturedTypedData);
                    if(candidateStillMatches&&completed.ownerReceipt!=null)RegisterValidatedOwner(candidate,ticket.Slot,ticket.Path,ticket.Epoch,completed.ownerReceipt);
                    else InvalidateOwnerProof();
                    Status="Saved.";
                }
                else{MarkSelectedCheckpointDirty();InvalidateOwnerProof();Status="Save committed; checkpoint retirement needs recovery: "+retireError;}
                // Diagnostic reporting must never affect a successful durable commit.
                var observer=ValidatedSaveMeasurement;
                if(observer!=null&&completed.trace!=null)
                    foreach(var sample in completed.trace.stages)
                        try{observer(sample.Key,sample.Value);}catch(Exception){}
            }
            catch
            {
                InvalidateOwnerProof();
                throw;
            }
            finally { pendingValidatedSave=null; }
        }

        static bool TypedCaptureMatches(PlayerProfile candidate, SqliteTypedProfileData captured)
        {
            if(candidate==null||captured==null)return false;
            try
            {
                var economy=captured.Economy;
                if(economy==null||candidate.cash!=economy.Cash||candidate.revision!=economy.Revision||
                    candidate.lastEconomyUtcTicks!=economy.LastEconomyUtcTicks)return false;

                // Compare only the already-captured typed subset in its canonical
                // capture order: all cars, then detached engines. Reordered owners
                // conservatively fail proof instead of allocating an index. This is
                // linear in those lists, with exact IEEE bits and no full projection.
                var fluids=captured.Fluids;
                if(candidate.cars==null||candidate.engines==null)return false;
                int cursor=0;
                for(int i=0;i<candidate.cars.Count;i++)
                {
                    var car=candidate.cars[i];
                    if(car==null||cursor>=fluids.Length)return false;
                    var row=fluids[cursor++];
                    if(row==null||row.OwnerKind!="car"||!string.Equals(row.OwnerId,car.instanceId,StringComparison.Ordinal)||
                        !MatchesFluid(row,car.fluids))return false;
                }
                for(int i=0;i<candidate.engines.Count;i++)
                {
                    var engine=candidate.engines[i];
                    if(engine==null)return false;
                    if(!string.IsNullOrEmpty(engine.parentVehicleId))continue;
                    if(cursor>=fluids.Length)return false;
                    var row=fluids[cursor++];
                    if(row==null||row.OwnerKind!="engine"||!string.Equals(row.OwnerId,engine.instanceId,StringComparison.Ordinal)||
                        !MatchesFluid(row,engine.fluids))return false;
                }
                if(cursor!=fluids.Length)return false;

                // Receipt order and all nullable item IDs are compared directly.
                // The persisted window is capped at 256 records.
                var receipts=captured.Receipts;
                if(candidate.economyReceipts==null||candidate.economyReceipts.Count!=receipts.Length)return false;
                for(int i=0;i<receipts.Length;i++)
                {
                    if(!MatchesReceipt(receipts[i],candidate.economyReceipts[i],i))return false;
                }
                return true;
            }
            catch { return false; }
        }

        static bool MatchesFluid(TypedFluidStateRecord row, FluidState state)
        {
            return state!=null&&row.ValueVersion==TypedFluidStateRecord.CurrentValueVersion&&
                row.OilLitresBits==BitConverter.DoubleToInt64Bits(state.oilLitres)&&
                row.CoolantLitresBits==BitConverter.DoubleToInt64Bits(state.coolantLitres)&&
                row.OilTemperatureCBits==BitConverter.DoubleToInt64Bits(state.oilTemperatureC)&&
                row.OilPressureBarBits==BitConverter.DoubleToInt64Bits(state.oilPressureBar)&&
                row.CoolantPressureBarBits==BitConverter.DoubleToInt64Bits(state.coolantPressureBar)&&
                row.OilConditionBits==BitConverter.DoubleToInt64Bits(state.oilCondition)&&
                row.OilConsumedLitresBits==BitConverter.DoubleToInt64Bits(state.oilConsumedLitres)&&
                row.OilLeakedLitresBits==BitConverter.DoubleToInt64Bits(state.oilLeakedLitres)&&
                row.CoolantLostLitresBits==BitConverter.DoubleToInt64Bits(state.coolantLostLitres)&&
                row.ServiceElapsedSecondsBits==BitConverter.DoubleToInt64Bits(state.serviceElapsedSeconds)&&
                row.OilBurnLpsBits==BitConverter.DoubleToInt64Bits(state.oilBurnLps)&&
                row.OilLeakLpsBits==BitConverter.DoubleToInt64Bits(state.oilLeakLps)&&
                row.CoolantLeakLpsBits==BitConverter.DoubleToInt64Bits(state.coolantLeakLps)&&
                row.CoolantVentLpsBits==BitConverter.DoubleToInt64Bits(state.coolantVentLps);
        }

        static bool MatchesReceipt(SqliteEconomyReceipt stored, EconomyReceipt current, int order)
        {
            if(stored==null||current==null||stored.ReceiptOrder!=order||stored.Id!=current.id||
                stored.Operation!=current.operation||stored.Target!=current.target||stored.Revision!=current.revision||
                stored.UtcTicks!=current.utcTicks||stored.CashBefore!=current.cashBefore||stored.CashAfter!=current.cashAfter||
                stored.FuelAddedBits!=BitConverter.DoubleToInt64Bits(current.fuelAdded)||
                stored.OilAddedBits!=BitConverter.DoubleToInt64Bits(current.oilAdded)||
                stored.CoolantAddedBits!=BitConverter.DoubleToInt64Bits(current.coolantAdded))return false;
            return SameStrings(stored.DisposedIds(),current.disposed)&&SameStrings(stored.RetainedIds(),current.retained);
        }

        static bool SameStrings(string[] expected,string[] actual)
        {
            if(expected==null||actual==null||expected.Length!=actual.Length)return false;
            for(int i=0;i<expected.Length;i++)if(!string.Equals(expected[i],actual[i],StringComparison.Ordinal))return false;
            return true;
        }
    }
}
