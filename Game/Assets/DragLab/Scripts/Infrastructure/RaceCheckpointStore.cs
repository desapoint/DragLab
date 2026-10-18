using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    // A bounded, single-writer journal. Only copied managed data reaches the worker.
    // Main-thread callers serialize queueing/full saves; a full save is a drain/retirement barrier.
    public static class RaceCheckpointStore
    {
        [Serializable] sealed class Data { public int version=1; public long baseRevision; public RunCheckpoint checkpoint; }
        [Serializable] sealed class Envelope { public string payload, sha256; }
        sealed class Request { public string path; public Data data; public string json; }
        static readonly object gate=new object();
        static Request pending;
        static Task worker;
        static string failure;
        static string lastPath, lastRun;
        static long lastTick=-1;
        static RunCheckpoint lastSnapshot;
        static bool prepared;
        public static string Error { get { lock(gate)return failure; } }
        public static bool Busy { get { lock(gate)return worker!=null&&!worker.IsCompleted; } }
        public static string JournalPath(string profilePath)=>profilePath+".run";
        public static void Prepare()
        {
            if(prepared)return;
            // Pay thread-pool/serializer/hash initialization at the full-save entry boundary.
            Task.Run(()=>Hash(JsonUtility.ToJson(new Data()))).GetAwaiter().GetResult();
            prepared=true;
        }

        public static bool TryQueue(string profilePath, PlayerProfile profile, RunCheckpoint checkpoint, out string error)
        {
            try {
                var receipt=profile.runCheckpoints.Find(r=>r.runId==checkpoint.runId);
                if(receipt==null||receipt.settled||checkpoint.settled)throw new InvalidOperationException("Journal requires an entered, open run.");
                MechanicsTransactions.ValidateCheckpoint(profile,checkpoint);
                // Protect against callers subsequently changing a captured snapshot.
                var copy=new RunCheckpoint { runId=checkpoint.runId, ownedVehicleId=checkpoint.ownedVehicleId, version=checkpoint.version,
                    model=checkpoint.model, tick=checkpoint.tick, thermalUtcTicks=checkpoint.thermalUtcTicks, hasFuel=checkpoint.hasFuel,
                    fuel=new FuelState { litres=checkpoint.fuel.litres,cumulativeUsedLitres=checkpoint.fuel.cumulativeUsedLitres,flowLitresPerSecond=checkpoint.fuel.flowLitresPerSecond },
                    fluids=checkpoint.fluids.Copy(), components=checkpoint.components.Select(p=>p.Copy()).ToList() };
                lock(gate) {
                    if(lastPath==profilePath&&lastRun==copy.runId&&copy.tick<=lastTick)throw new InvalidOperationException("Journal tick must advance.");
                    if(lastPath==profilePath&&lastRun==copy.runId)ValidateProgress(lastSnapshot,copy);
                    if(lastPath!=null&&lastPath!=profilePath&&worker!=null&&!worker.IsCompleted)throw new InvalidOperationException("Another profile journal is still writing.");
                    var data=new Data {baseRevision=profile.revision,checkpoint=copy};pending=new Request {path=profilePath,data=data,json=JsonUtility.ToJson(data)};
                    lastPath=profilePath;lastRun=copy.runId;lastTick=copy.tick;lastSnapshot=copy;
                    if(worker==null||worker.IsCompleted)worker=Task.Run((Action)WriteLoop);
                }
                error=null;return true;
            } catch(Exception e){error=e.Message;return false;}
        }
        static void WriteLoop()
        {
            // Keep the task alive until pending is empty under the same lock as TryQueue.
            while(true) {
                Request request;
                lock(gate){request=pending;pending=null;if(request==null){worker=null;return;}}
                try {
                    if(ProfileStore.DatabasePath(request.path)){SqliteStateStore.WriteCheckpoint(request.path,request.data.checkpoint.runId,request.json);lock(gate)failure=null;continue;}
                    string path=JournalPath(request.path),temp=path+".tmp";
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    string payload=request.json;
                    var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope {payload=payload,sha256=Hash(payload)}));
                    using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                    if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
                    lock(gate)failure=null;
                }catch(Exception e){lock(gate)failure="Race checkpoint write failed: "+e.Message;}
            }
        }
        public static void Drain()
        {
            Task task;lock(gate)task=worker;
            task?.GetAwaiter().GetResult();
        }
        static string Hash(string text)
        {using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));}
        static void ValidateProgress(RunCheckpoint old,RunCheckpoint next)
        {
            var a=old.fuel;var b=next.fuel;var x=old.fluids;var y=next.fluids;
            if(next.ownedVehicleId!=old.ownedVehicleId||b.litres>a.litres+1e-9||RunPersistence.HistoryLess(b.cumulativeUsedLitres,a.cumulativeUsedLitres)
                ||y.oilLitres>x.oilLitres+1e-9||y.coolantLitres>x.coolantLitres+1e-9||y.oilCondition>x.oilCondition+1e-9
                ||RunPersistence.HistoryLess(y.oilConsumedLitres,x.oilConsumedLitres)||RunPersistence.HistoryLess(y.oilLeakedLitres,x.oilLeakedLitres)
                ||RunPersistence.HistoryLess(y.coolantLostLitres,x.coolantLostLitres)||RunPersistence.HistoryLess(y.serviceElapsedSeconds,x.serviceElapsedSeconds))
                throw new InvalidOperationException("Journal cannot rewind queued resources.");
            foreach(var part in next.components) {
                var prior=old.components.Find(p=>p.instanceId==part.instanceId);
                if(prior==null||!RunPersistence.ConditionAdvances(prior.condition,part.condition)||!EngineElements.CanAdvance(prior.condition,part.condition))
                    throw new InvalidOperationException("Journal cannot rewind queued component condition.");
            }
        }

        // Called only after draining, and after full-ledger ownership normalization.
        // Exact base revision and an existing run receipt bind recovery to the original entry.
        public static bool Merge(string profilePath, PlayerProfile profile, bool loading, out string warning)
        {
            warning=null;
            if(ProfileStore.DatabasePath(profilePath)){
                try{foreach(string json in SqliteStateStore.Checkpoints(profilePath))if(ApplyRecovered(profile,JsonUtility.FromJson<Data>(json),loading))return true;}catch(Exception e){warning="Database checkpoint ignored: "+e.Message;}
            }else foreach(var path in new[]{JournalPath(profilePath),JournalPath(profilePath)+".bak"}) {
                if(!File.Exists(path))continue;
                try{var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(path,Encoding.UTF8));if(envelope==null||envelope.payload==null||envelope.sha256!=Hash(envelope.payload))throw new InvalidDataException("Checkpoint checksum mismatch.");if(ApplyRecovered(profile,JsonUtility.FromJson<Data>(envelope.payload),loading))return true;}catch(Exception e){warning="Race checkpoint ignored: "+e.Message;}
            }
            return false;
        }
        static bool ApplyRecovered(PlayerProfile profile,Data data,bool loading)
        {
                    if(data==null||data.version!=1||data.checkpoint==null)throw new InvalidDataException("Unknown checkpoint format.");
                    var cp=data.checkpoint;var receipt=profile.runCheckpoints.Find(r=>r.runId==cp.runId);
                    // A full committed result/reset/new entry supersedes an old journal.
                    if(receipt==null||receipt.ownedVehicleId!=cp.ownedVehicleId||receipt.settled||receipt.tick>=cp.tick)return false;
                    if(data.baseRevision!=profile.revision && (loading||data.baseRevision+1!=profile.revision))return false;
                    // v21 recalibrates tires and retains sub-float mechanical wear;
                    // resource history remains the same contract.
                    // Recover known monotonic wear/fuel only; never replay obsolete driving inputs.
                    if(MechanicsSchema.Model=="draglab-variety-v21"&&(cp.model=="draglab-variety-v18"||cp.model=="draglab-variety-v19"||cp.model=="draglab-variety-v20"))cp.model=MechanicsSchema.Model;
                    MechanicsTransactions.ApplyCheckpoint(profile,cp);
                    // Journal ticks do not create ledger revisions until a full commit.
                    profile.runCheckpoints.Find(r=>r.runId==cp.runId).revision=profile.revision;
                    return true;
        }
        public static void Retire(string profilePath,bool clearDatabase=true)
        {
            Drain();
            if(clearDatabase&&ProfileStore.DatabasePath(profilePath)){try{SqliteStateStore.ClearCheckpoints(profilePath);}catch(IOException){ /* Already superseded by the durable ledger transaction. */ }}
            foreach(var path in new[]{JournalPath(profilePath),JournalPath(profilePath)+".bak",JournalPath(profilePath)+".tmp"})
                try {if(File.Exists(path))File.Delete(path);}catch(IOException){ /* A superseded journal is harmless and can be retired on the next save. */ }
                catch(UnauthorizedAccessException){ /* Cleanup failure must not roll back an already durable full commit. */ }
            lock(gate){lastPath=null;lastRun=null;lastTick=-1;lastSnapshot=null;failure=null;}
        }
    }
}
