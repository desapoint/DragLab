using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Infrastructure
{
    public static partial class ProfileStore
    {
        internal static Action<string,double> SaveMeasurement;
        static bool writeBlocked;
        public static bool ResetRequired => writeBlocked;

        // Explicit user-confirmed reset only. Preserve the old database, checkpoint
        // and backups beside the save under a timestamped name for manual recovery.
        public static bool TryResetIncompatibleSave(out string error)
        {
            error = null;
            if(!CanBeginLegacyWrite()){error="A typed save operation is still retained.";return false;}
            InvalidateOwnerProof();
            if (!writeBlocked) { error = "No incompatible save requires reset."; return false; }
            var moved = new System.Collections.Generic.List<string[]>();
            string backupSuffix = ".before-reset-" + DragLab.Domain.RuntimeEntropy.UtcNow.ToString("yyyyMMddHHmmssfff");
            string[] roots = LegacyFixture ? new[]{FilePath} :
                slot=="slot-1" ? new[]{FilePath,LegacyFilePath} : new[]{FilePath};
            string[] suffixes = {"",".bak",".run",".run.bak",".run.tmp",".tmp","-wal","-shm","-journal",".reset-generation",".reset-intent",".reset-generation.tmp",".reset-intent.tmp",".backup-next"};
            try
            {
                RaceCheckpointStore.Drain();
                SqliteStateStore.DrainBackups();
                SqliteStateStore.Invalidate();
                foreach (var root in roots) foreach (var suffix in suffixes)
                {
                    string source = root + suffix;
                    if (!File.Exists(source)) continue;
                    string target = source + backupSuffix;
                    File.Move(source,target);
                    moved.Add(new[]{source,target});
                }
                writeBlocked = false;
                var fresh = PlayerProfile.NewGame(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);
                MechanicsOwnership.Ensure(fresh,MechanicsTransactions.Catalog());
                if(!TrySave(fresh))throw new IOException(Status);
                Status = "A new save was created. Previous data was archived with suffix " + backupSuffix + ".";
                return true;
            }
            catch(Exception e)
            {
                writeBlocked = true;
                error = "Reset failed; prior data is retained: " + e.Message;
                for(int i=moved.Count-1;i>=0;i--)try
                {
                    if(File.Exists(moved[i][0])) File.Move(moved[i][0],moved[i][0]+".failed-reset"+backupSuffix);
                    if(File.Exists(moved[i][1])) File.Move(moved[i][1],moved[i][0]);
                }
                catch(Exception rollback){error += " Backup restoration: " + rollback.Message;}
                Status = error;
                return false;
            }
        }
        static string slot="slot-1";
#if UNITY_EDITOR
        static string verificationPath;
        public static string VerificationPath
        {
            get=>verificationPath;
            set
            {
                if(!string.Equals(verificationPath,value,StringComparison.Ordinal))
                {
                    if(!CanChangeSelection())throw new InvalidOperationException("A save operation is still retained.");
                    InvalidateOwnerProof();verificationPath=value;selectionEpoch=checked(selectionEpoch+1);selectedCheckpointDirty=false;
                }
            }
        }
#endif
        internal static bool LegacyFixture {
            get {
#if UNITY_EDITOR
                return !string.IsNullOrEmpty(VerificationPath)&&VerificationPath.EndsWith(".json",StringComparison.OrdinalIgnoreCase);
#else
                return false;
#endif
            }
        }
        internal static bool DatabasePath(string path)=>path.EndsWith(".sqlite3",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".sqlite3.bak",StringComparison.OrdinalIgnoreCase);
        public static void UseSlot(string id){if(string.IsNullOrEmpty(id)||id.Length>48||System.Text.RegularExpressions.Regex.IsMatch(id,"[^a-zA-Z0-9_-]"))throw new ArgumentException("Invalid save slot ID.");if(!CanChangeSelection())throw new InvalidOperationException("A typed save operation is still retained.");InvalidateOwnerProof();RaceCheckpointStore.Drain();slot=id;selectionEpoch=checked(selectionEpoch+1);selectedCheckpointDirty=false;SqliteStateStore.Invalidate();writeBlocked=false;}
        public static string FilePath {
            get {
#if UNITY_EDITOR
                if(!string.IsNullOrEmpty(VerificationPath))return VerificationPath;
#endif
                var args=Environment.GetCommandLineArgs();foreach(string flag in new[]{"--draglab-verify","--draglab-bench-verify"}){int index=Array.IndexOf(args,flag);if(Debug.isDebugBuild&&index>=0&&index+1<args.Length)return Path.Combine(Path.GetFullPath(args[index+1]),"save-"+slot+".sqlite3");}
                return Path.Combine(Application.persistentDataPath,"save-"+slot+".sqlite3");
            }
        }
        internal static string LegacyFilePath=>Path.Combine(Path.GetDirectoryName(FilePath),"profile-v1.json");
        static string status;
        public static string Status {get=>SqliteStateStore.BackupWarning==null?status:status+" "+SqliteStateStore.BackupWarning;private set=>status=value;}
        public static PlayerProfile Load(bool recoverInterrupted=false)
        {
            if(!CanLoadSelectedOwner())throw new InvalidOperationException("A typed save operation is still retained.");
            InvalidateOwnerProof();
            if(LegacyFixture){var old=LegacyProfileStore.Load(recoverInterrupted);Status=LegacyProfileStore.Status;return old;}
            string loadSlot,loadPath;long loadEpoch;CaptureSelectedContext(out loadSlot,out loadPath,out loadEpoch);
            RaceCheckpointStore.Drain();MarkSelectedCheckpointVerifiedClean();SqliteStateStore.DrainBackups();SqliteStateStore.Prepare();SqliteStateStore.Invalidate();writeBlocked=false;Status=null;long now=DragLab.Domain.RuntimeEntropy.UtcNow.Ticks;var catalog=MechanicsTransactions.Catalog();
            foreach(string path in new[]{loadPath,loadPath+".bak"}){
                if(!File.Exists(path))continue;
                if(path==loadPath&&SqliteStateStore.CanInitialize(path))continue;
                try{
                var state=SqliteStateStore.ReadState(path,
                    raw=>LegacyProfileDecoder.DecodeAndValidate(raw,catalog),
                    loaded=>{
                        var profile=loaded.ValidatedMigrationProfile??JsonUtility.FromJson<PlayerProfile>(DrivingPreferences.AddToJson(loaded.LegacyJson));
                        if(profile==null)throw new InvalidDataException("Saved profile could not be decoded.");
                        profile.catalog=catalog;
                        profile.storageCommit=loaded.Commit;
                        loaded.TypedData.ApplyTo(profile);
                        LegacyProfileDecoder.ValidateLoadedV12(profile,catalog);
                        return profile;
                    });
                    var p=state.LoadedProfile;
                    if(p==null)throw new InvalidDataException("Saved profile could not be materialized.");
                    // ReadState has already validated this exact current-contract graph.
                    // Broad normalization here could silently heal durable state after
                    // the strict migration/load boundary, then persist the altered values.
                    if(path!=loadPath&&!writeBlocked)SqliteStateStore.RecoverBackup(loadPath,path);
                    Status=path==loadPath?"Save database loaded.":"Recovered previous save database.";
                    bool merged=RaceCheckpointStore.Merge(loadPath,p,true,out string warning);if(merged){Status="Save database and latest checkpoint loaded.";MarkSelectedCheckpointDirty();}if(warning!=null){Status+=" "+warning;MarkSelectedCheckpointDirty();}
                    if(recoverInterrupted&&!writeBlocked&&RunPersistence.RecoverInterrupted(p)){bool saved=TrySave(p);Status=saved?"Recovered interrupted run; wear/fuel retained without rewards.":"Interrupted resources retained in memory; "+Status;InvalidateOwnerProof();}
                    else if(!merged&&warning==null)RegisterValidatedOwner(p,loadSlot,loadPath,loadEpoch,state.OwnerReceipt);
                    return p;
                }catch(Exception error){if(error is NotSupportedException)writeBlocked=true;Debug.LogWarning("Could not read "+path+": "+error.Message);}
            }
            if(File.Exists(loadPath)&&!SqliteStateStore.CanInitialize(loadPath)||File.Exists(loadPath+".bak"))writeBlocked=true;
            if(!writeBlocked&&slot=="slot-1"&&(File.Exists(LegacyFilePath)||File.Exists(LegacyFilePath+".bak"))){
                try{return ImportLegacy(catalog,recoverInterrupted);}catch(Exception error){writeBlocked=true;Status="Legacy import failed; originals preserved: "+error.Message;Debug.LogWarning(Status);}
            }
            var fresh=PlayerProfile.NewGame(now);MechanicsOwnership.Ensure(fresh,catalog);
            if(writeBlocked){Status=Status??"Save could not be recovered; originals preserved and writes blocked.";InvalidateForFailedLoad();return fresh;}
            // Preferences are shared across slots and are independent from starting progression.
            if(File.Exists(DrivingPreferences.FilePath))fresh=JsonUtility.FromJson<PlayerProfile>(DrivingPreferences.AddToJson(JsonUtility.ToJson(fresh)));
            fresh.catalog=catalog;if(!TrySave(fresh)){Status="New game retained in memory; "+Status;InvalidateForFailedLoad();}else Status="New save database created.";return fresh;
        }
        static PlayerProfile ImportLegacy(MechanicalCatalog catalog,bool recoverInterrupted)
        {
            string folder=FilePath+".import";Directory.CreateDirectory(folder);string source=Path.Combine(folder,"profile-v1.json");
            foreach(string suffix in new[]{"",".bak",".run",".run.bak"}){string old=LegacyFilePath+suffix;if(File.Exists(old)){string original=old+".pre-sqlite";if(!File.Exists(original))File.Copy(old,original);File.Copy(old,source+suffix,true);}}
            PlayerProfile imported;try{LegacyProfileStore.OverridePath=source;imported=LegacyProfileStore.Load(recoverInterrupted);if(LegacyProfileStore.WritesBlocked)throw new NotSupportedException("Legacy source has an unsupported version or cannot be recovered.");}finally{LegacyProfileStore.OverridePath=null;}
            imported.catalog=catalog;DrivingPreferences.Ensure(imported);if(!TrySave(imported))throw new IOException(Status);Status="Legacy save imported into SQLite; originals retained.";return imported;
        }
        internal static bool TrySavePreferences(PlayerProfile profile,out string error){try{DrivingPreferences.SaveIfChanged(profile);error=null;return true;}catch(Exception e){error=e.Message;return false;}}
        public static void Flush(){RaceCheckpointStore.Drain();SqliteStateStore.DrainBackups();}
        public static void Save(PlayerProfile profile){if(!TrySave(profile))Debug.LogError(Status);}
        public static bool TrySave(PlayerProfile profile)=>TrySaveCore(profile,false);
        internal static bool TrySaveValidated(PlayerProfile profile)=>TrySaveCore(profile,true);
        static bool TrySaveCore(PlayerProfile profile,bool validated)
        {
            if(!CanBeginLegacyWrite())return false;
            if(profile==null)return false;
            if(LegacyFixture){bool ok=validated?LegacyProfileStore.TrySaveValidated(profile):LegacyProfileStore.TrySave(profile);Status=LegacyProfileStore.Status;return ok;}
            string saveSlot,savePath;long saveEpoch;CaptureSelectedContext(out saveSlot,out savePath,out saveEpoch);
            RaceCheckpointStore.Drain();try{
                if(writeBlocked)throw new InvalidDataException("Save writes blocked; existing database preserved.");
                long start=System.Diagnostics.Stopwatch.GetTimestamp();if(!validated)MechanicsOwnership.Ensure(profile,profile.catalog??MechanicsTransactions.Catalog());
                string warning;RaceCheckpointStore.Merge(savePath,profile,false,out warning);DrivingPreferences.Ensure(profile);SqliteStateStore.Prepare();var snapshot=SqliteStateStore.LegacyCaptureManaged(profile,Application.version,null);
                try{SaveMeasurement?.Invoke("database capture + validation",Elapsed(start));}catch(Exception){}
                start=System.Diagnostics.Stopwatch.GetTimestamp();Task.Run(()=>SqliteStateStore.WriteLegacy(savePath,snapshot)).GetAwaiter().GetResult();
                try{SaveMeasurement?.Invoke("database transaction + durability",Elapsed(start));}catch(Exception){}
                profile.storageCommit=snapshot.committed;
                bool retired=true;string retireError=null;try{RaceCheckpointStore.Retire(savePath,false);}catch(Exception error){retired=false;retireError=error.Message;}
                if(retired){Status="Saved.";MarkSelectedCheckpointVerifiedClean();if(snapshot.OwnerReceipt!=null&&profile.revision==snapshot.typedData.Economy.Revision&&TypedCaptureMatches(profile,snapshot.typedData))RegisterValidatedOwner(profile,saveSlot,savePath,saveEpoch,snapshot.OwnerReceipt);else InvalidateOwnerProof();}
                else{Status="Save committed; checkpoint retirement needs recovery: "+retireError;MarkSelectedCheckpointDirty();}
                return true;
            }catch(Exception error){InvalidateOwnerProof();Status="SAVE FAILED: "+error.Message;return false;}
        }
        static double Elapsed(long stamp)=>(System.Diagnostics.Stopwatch.GetTimestamp()-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        public static bool TryCheckpoint(PlayerProfile profile,RunCheckpoint checkpoint,out string error){if(!CanUseCheckpoint()){error="A typed save operation is still retained.";return false;}if(LegacyFixture)return LegacyProfileStore.TryCheckpoint(profile,checkpoint,out error);if(writeBlocked){error="Save writes blocked; original preserved.";return false;}bool queued=RaceCheckpointStore.TryQueue(FilePath,profile,checkpoint,out error);if(queued)MarkSelectedCheckpointDirty();return queued;}
        public static bool TryReset(PlayerProfile current,out PlayerProfile committed)
        {
            if(!CanBeginLegacyWrite()){committed=current;Status="A typed save operation is still retained.";return false;}
            InvalidateOwnerProof();
            if(LegacyFixture){bool ok=LegacyProfileStore.TryReset(current,out committed);Status=LegacyProfileStore.Status;return ok;}
            string resetSlot,resetPath;long resetEpoch;CaptureSelectedContext(out resetSlot,out resetPath,out resetEpoch);
            RaceCheckpointStore.Drain();SqliteStateStore.DrainBackups();committed=current;try{if(writeBlocked)throw new InvalidDataException("Save writes are blocked.");var fresh=current.RestartKeepingPreferences(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);fresh.storageCommit=current.storageCommit;MechanicsOwnership.Ensure(fresh,current.catalog??MechanicsTransactions.Catalog());SqliteStateStore.Prepare();var snapshot=SqliteStateStore.LegacyCaptureManaged(fresh,Application.version,null);Task.Run(()=>SqliteStateStore.WriteLegacy(resetPath,snapshot,true)).GetAwaiter().GetResult();fresh.storageCommit=snapshot.committed;committed=fresh;bool retired=true;string retireError=null;try{RaceCheckpointStore.Retire(resetPath,false);}catch(Exception error){retired=false;retireError=error.Message;}if(retired){MarkSelectedCheckpointVerifiedClean();Status="Game reset. Settings preserved.";if(snapshot.OwnerReceipt!=null)RegisterValidatedOwner(fresh,resetSlot,resetPath,resetEpoch,snapshot.OwnerReceipt);}else{MarkSelectedCheckpointDirty();InvalidateOwnerProof();Status="Reset committed; checkpoint retirement needs recovery: "+retireError;}return true;}catch(Exception error){InvalidateOwnerProof();Status="RESET FAILED: "+error.Message;return false;}
        }
    }
}
