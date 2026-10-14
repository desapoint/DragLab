using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Infrastructure
{
    public static class ProfileStore
    {
        internal static Action<string,double> SaveMeasurement;
        static bool writeBlocked;
        static string slot="slot-1";
#if UNITY_EDITOR
        public static string VerificationPath;
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
        public static void UseSlot(string id){if(string.IsNullOrEmpty(id)||id.Length>48||System.Text.RegularExpressions.Regex.IsMatch(id,"[^a-zA-Z0-9_-]"))throw new ArgumentException("Invalid save slot ID.");RaceCheckpointStore.Drain();slot=id;SqliteStateStore.Invalidate();writeBlocked=false;}
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
            if(LegacyFixture){var old=LegacyProfileStore.Load(recoverInterrupted);Status=LegacyProfileStore.Status;return old;}
            RaceCheckpointStore.Drain();SqliteStateStore.DrainBackups();SqliteStateStore.Prepare();SqliteStateStore.Invalidate();writeBlocked=false;Status=null;long now=DateTime.UtcNow.Ticks;var catalog=MechanicsTransactions.Catalog();
            foreach(string path in new[]{FilePath,FilePath+".bak"}){
                if(!File.Exists(path))continue;
                if(path==FilePath&&SqliteStateStore.CanInitialize(path))continue;
                try{
                    string json=SqliteStateStore.Read(path,raw=>{var staged=JsonUtility.FromJson<PlayerProfile>(DrivingPreferences.AddToJson(raw));MechanicsOwnership.Ensure(staged,catalog);string invalid=MechanicsOwnership.Validate(staged,catalog);if(invalid!=null)throw new InvalidDataException("Migration validation failed: "+invalid);});var p=JsonUtility.FromJson<PlayerProfile>(DrivingPreferences.AddToJson(json));p.storageCommit=SqliteStateStore.ReadCommit;
                    if(p==null||p.saveVersion>MechanicsOwnership.SaveVersion||p.mechanicsVersion>MechanicsSchema.Version)throw new NotSupportedException("Newer profile contract; database writes are blocked.");
                    bool contractMigration=p.saveVersion<MechanicsOwnership.SaveVersion||p.mechanicsVersion<MechanicsSchema.Version;p.Sanitize(now);MechanicsOwnership.Ensure(p,catalog);if(MechanicsOwnership.Validate(p,catalog)!=null)throw new InvalidDataException("Invalid saved ownership/state.");
                    if(path!=FilePath&&!writeBlocked)SqliteStateStore.RecoverBackup(FilePath,path);
                    Status=path==FilePath?"Save database loaded.":"Recovered previous save database.";if(contractMigration&&!writeBlocked&&!TrySave(p))throw new IOException("Logical save-contract migration could not commit: "+Status);
                    string warning;if(RaceCheckpointStore.Merge(FilePath,p,true,out warning))Status="Save database and latest checkpoint loaded.";if(warning!=null)Status+=" "+warning;
                    if(recoverInterrupted&&!writeBlocked&&RunPersistence.RecoverInterrupted(p)){bool saved=TrySave(p);Status=saved?"Recovered interrupted run; wear/fuel retained without rewards.":"Interrupted resources retained in memory; "+Status;}
                    return p;
                }catch(Exception error){if(error is NotSupportedException)writeBlocked=true;Debug.LogWarning("Could not read "+path+": "+error.Message);}
            }
            if(File.Exists(FilePath)&&!SqliteStateStore.CanInitialize(FilePath)||File.Exists(FilePath+".bak"))writeBlocked=true;
            if(!writeBlocked&&slot=="slot-1"&&(File.Exists(LegacyFilePath)||File.Exists(LegacyFilePath+".bak"))){
                try{return ImportLegacy(catalog,recoverInterrupted);}catch(Exception error){writeBlocked=true;Status="Legacy import failed; originals preserved: "+error.Message;Debug.LogWarning(Status);}
            }
            var fresh=PlayerProfile.NewGame(now);MechanicsOwnership.Ensure(fresh,catalog);
            if(writeBlocked){Status=Status??"Save could not be recovered; originals preserved and writes blocked.";return fresh;}
            // Preferences are shared across slots and are independent from starting progression.
            if(File.Exists(DrivingPreferences.FilePath))fresh=JsonUtility.FromJson<PlayerProfile>(DrivingPreferences.AddToJson(JsonUtility.ToJson(fresh)));
            fresh.catalog=catalog;if(!TrySave(fresh))Status="New game retained in memory; "+Status;else Status="New save database created.";return fresh;
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
            if(LegacyFixture){bool ok=validated?LegacyProfileStore.TrySaveValidated(profile):LegacyProfileStore.TrySave(profile);Status=LegacyProfileStore.Status;return ok;}
            RaceCheckpointStore.Drain();try{
                if(writeBlocked)throw new InvalidDataException("Save writes blocked; existing database preserved.");
                long start=System.Diagnostics.Stopwatch.GetTimestamp();if(!validated)MechanicsOwnership.Ensure(profile,profile.catalog??MechanicsTransactions.Catalog());
                string warning;RaceCheckpointStore.Merge(FilePath,profile,false,out warning);DrivingPreferences.Ensure(profile);var snapshot=SqliteStateStore.Capture(profile);SaveMeasurement?.Invoke("database capture + validation",Elapsed(start));
                string path=FilePath;start=System.Diagnostics.Stopwatch.GetTimestamp();Task.Run(()=>SqliteStateStore.Write(path,snapshot)).GetAwaiter().GetResult();SaveMeasurement?.Invoke("database transaction + durability",Elapsed(start));
                profile.storageCommit=snapshot.committed;RaceCheckpointStore.Retire(FilePath,false);Status="Saved.";return true;
            }catch(Exception error){Status="SAVE FAILED: "+error.Message;return false;}
        }
        static double Elapsed(long stamp)=>(System.Diagnostics.Stopwatch.GetTimestamp()-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        public static bool TryCheckpoint(PlayerProfile profile,RunCheckpoint checkpoint,out string error){if(LegacyFixture)return LegacyProfileStore.TryCheckpoint(profile,checkpoint,out error);if(writeBlocked){error="Save writes blocked; original preserved.";return false;}return RaceCheckpointStore.TryQueue(FilePath,profile,checkpoint,out error);}
        public static bool TryReset(PlayerProfile current,out PlayerProfile committed)
        {
            if(LegacyFixture){bool ok=LegacyProfileStore.TryReset(current,out committed);Status=LegacyProfileStore.Status;return ok;}
            RaceCheckpointStore.Drain();SqliteStateStore.DrainBackups();committed=current;try{if(writeBlocked)throw new InvalidDataException("Save writes are blocked.");var fresh=current.RestartKeepingPreferences(DateTime.UtcNow.Ticks);fresh.storageCommit=current.storageCommit;MechanicsOwnership.Ensure(fresh,current.catalog??MechanicsTransactions.Catalog());var snapshot=SqliteStateStore.Capture(fresh);string path=FilePath;Task.Run(()=>SqliteStateStore.Write(path,snapshot,true)).GetAwaiter().GetResult();fresh.storageCommit=snapshot.committed;RaceCheckpointStore.Retire(FilePath,false);committed=fresh;Status="Game reset. Settings preserved.";return true;}catch(Exception error){Status="RESET FAILED: "+error.Message;return false;}
        }
    }
}
