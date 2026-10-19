using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Infrastructure
{
    internal static class SqliteStateStore
    {
        internal const int SchemaVersion=2,ApplicationId=0x444C4142;
        internal sealed class Migration {public int version;public string name,sql,hash;}
        internal sealed class SaveTrace
        {
            // Writer-owned until Task completion. Observers are replayed on
            // Unity's main thread, never invoked by a SQLite writer.
            internal readonly List<KeyValuePair<string,double>> stages=
                new List<KeyValuePair<string,double>>();
            internal void Value(string name,double value)=>stages.Add(
                new KeyValuePair<string,double>(name,value));
            internal void Elapsed(string name,long start)
                =>Value(name,(Stamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);
        }
        internal sealed class Snapshot {public List<StateRows.Row> rows;public List<string[]> owners,components,slots;public long revision,expectedCommit,committed;public string build,content,physics,saveVersion,json;public SaveTrace trace;}
        static readonly object gate=new object();
        static Migration[] migrations;
        static string cachedPath;static long cachedCommit=-1;static Dictionary<string,StateRows.Row> cachedRows;
        internal static long ReadCommit=>cachedCommit;
        internal static int LastChangedRows {get;private set;}
        internal static Action<string> FailureInjection;
        internal static Action<string,double> Measurement;
        static long Stamp()=>System.Diagnostics.Stopwatch.GetTimestamp();
        static void Measured(string phase,ref long start){Measurement?.Invoke(phase,(Stamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);start=Stamp();}
        static void SaveMeasured(Snapshot snapshot,string phase,ref long start)
        {
            long end=Stamp();
            double milliseconds=(end-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            snapshot.trace?.Value(phase,milliseconds);
            Measurement?.Invoke(phase,milliseconds);
            start=Stamp();
        }
        internal static void Prepare()
        {
            if(migrations!=null)return;
            migrations=Resources.LoadAll<TextAsset>("Persistence/Migrations").Select(a=>new Migration{version=int.Parse(a.name.Substring(0,3),CultureInfo.InvariantCulture),name=a.name,sql=a.text,hash=Hash(a.text)}).OrderBy(m=>m.version).ToArray();
            if(migrations.Length!=SchemaVersion||migrations.Where((m,i)=>m.version!=i+1).Any())throw new InvalidDataException("Missing ordered SQLite migrations.");
        }
        static string Hash(string text){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));}
        internal static bool IsDatabase(string path){if(!File.Exists(path))return false;using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){var data=new byte[16];return file.Read(data,0,16)==16&&Encoding.ASCII.GetString(data)=="SQLite format 3\0";}}
        static string Meta(SqliteNative db,string name)=>db.Scalar("SELECT value FROM state_meta WHERE name=?",name);
        static void SetMeta(SqliteNative db,string name,string value){using(var statement=db.Prepare("INSERT INTO state_meta(name,value) VALUES(?,?) ON CONFLICT(name) DO UPDATE SET value=excluded.value WHERE value<>excluded.value"))statement.Execute(name,value);}
        static SqliteNative Open(string path,bool create=true,Action<string> validateMigration=null)
        {
            if(File.Exists(path)&&!IsDatabase(path)){string text=File.ReadAllText(path,Encoding.UTF8);if(text.TrimStart().StartsWith("{")){var version=StateRows.Fields(text.Trim()).FirstOrDefault(f=>f.Key=="saveVersion").Value;if(int.TryParse(version,out int contract)&&contract>MechanicsOwnership.SaveVersion)throw new NotSupportedException("Unexpected future save format; original preserved.");}throw new InvalidDataException("Not a SQLite save database.");}
            if(!File.Exists(path)&&!create)throw new FileNotFoundException("Save database is missing.",path);
            var db=new SqliteNative(path);
            try{
                int version=int.Parse(db.Scalar("PRAGMA user_version"),CultureInfo.InvariantCulture),app=int.Parse(db.Scalar("PRAGMA application_id"),CultureInfo.InvariantCulture);
                if(version>SchemaVersion)throw new NotSupportedException("Newer save database schema; writes are blocked.");
                if(app!=0&&app!=ApplicationId)throw new NotSupportedException("Database belongs to another application.");
                if(version==0&&db.Scalar("SELECT name FROM sqlite_master WHERE type='table' LIMIT 1")!=null)throw new InvalidDataException("Unversioned populated database cannot be treated as a new save.");
                if(version>0){var history=db.Query("SELECT version,checksum FROM schema_migrations");if(history.Count!=version)throw new InvalidDataException("Incomplete migration history.");foreach(var row in history){int number=int.Parse(row[0],CultureInfo.InvariantCulture);if(number<1||number>migrations.Length||migrations[number-1].hash!=row[1])throw new NotSupportedException("Migration history does not match this build.");}}
                if(version>0&&version<SchemaVersion){string original=path+".schema-"+version+".bak";if(!File.Exists(original))db.Backup(original);}
                db.Exec("PRAGMA journal_mode=WAL;");
                if(version<SchemaVersion){db.Exec("BEGIN IMMEDIATE");try{foreach(var migration in migrations.Where(m=>m.version>version)){db.Exec(migration.sql);using(var statement=db.Prepare("INSERT INTO schema_migrations(version,name,checksum,applied_utc) VALUES(?,?,?,?)"))statement.Execute(migration.version,migration.name,migration.hash,DragLab.Domain.RuntimeEntropy.UtcNow.ToString("O",CultureInfo.InvariantCulture));db.Exec("PRAGMA user_version="+migration.version);FailureInjection?.Invoke("migration:"+migration.version);}db.Exec("PRAGMA application_id="+ApplicationId);var migrated=Rows(db);if(migrated.Count>0){if(validateMigration==null)throw new InvalidOperationException("Load and validate the existing save before migrating it.");validateMigration(StateRows.Restore(migrated.Values));SetMeta(db,"records_digest",Digest(migrated.Values));}db.Exec("COMMIT");}catch{db.Exec("ROLLBACK");throw;}}
                return db;
            }catch{db.Dispose();throw;}
        }
        internal static Snapshot Capture(PlayerProfile profile)
        {
            Prepare();return CaptureManaged(profile,Application.version);
        }
        internal static Snapshot CaptureManaged(PlayerProfile profile,string build,SaveTrace trace=null)
        {
            long capture=trace==null?0:Stamp();
            string json=JsonUtility.ToJson(profile);
            if(trace!=null){
                trace.Elapsed("profile serialization",capture);
                trace.Value("profile JSON characters",json.Length);
                trace.Value("profile JSON UTF8 bytes",Encoding.UTF8.GetByteCount(json));
            }
            long relationships=trace==null?0:Stamp();
            var result=new Snapshot{json=json,revision=profile.revision,expectedCommit=profile.storageCommit,build=build,content=MechanicalCatalog.Version.ToString(CultureInfo.InvariantCulture),physics=MechanicsSchema.Model,saveVersion=profile.saveVersion.ToString(CultureInfo.InvariantCulture),trace=trace,
                owners=profile.cars.Select(c=>new[]{c.instanceId,"car",c.vehicleId}).Concat(profile.engines.Select(e=>new[]{e.instanceId,"engine",e.vehicleDefinitionId})).ToList(),
                components=profile.parts.Select(p=>new[]{p.instanceId,p.definitionId,string.IsNullOrEmpty(p.ownerId)?null:p.ownerId,((int)p.location).ToString(CultureInfo.InvariantCulture)}).ToList(),
                slots=profile.cars.SelectMany(c=>c.installed.slots.Select(s=>new[]{c.instanceId,s.slot,s.partInstanceId})).Concat(profile.engines.SelectMany(e=>e.slots.Select(s=>new[]{e.instanceId,s.slot,s.partInstanceId}))).ToList()};
            if(trace!=null){
                trace.Elapsed("relationship projection",relationships);
                trace.Value("owner records",result.owners.Count);
                trace.Value("component records",result.components.Count);
                trace.Value("slot records",result.slots.Count);
            }
            return result;
        }
        internal static void Invalidate(){lock(gate){cachedPath=null;cachedRows=null;cachedCommit=-1;}}
        static string Digest(IEnumerable<StateRows.Row> rows){var sum=new byte[32];using(var sha=SHA256.Create())foreach(var row in rows){if(row.digest==null)row.digest=sha.ComputeHash(Encoding.UTF8.GetBytes(row.Id+"\n"+row.order+"\n"+row.json));for(int i=0;i<sum.Length;i++)sum[i]^=row.digest[i];}return Convert.ToBase64String(sum);}
        static Dictionary<string,StateRows.Row> Rows(SqliteNative db)=>db.Query("SELECT collection,record_key,ordinal,payload FROM state_records").Select(r=>new StateRows.Row{path=r[0],key=r[1],order=int.Parse(r[2],CultureInfo.InvariantCulture),json=r[3]}).ToDictionary(r=>r.Id,StringComparer.Ordinal);
        internal static string Read(string path,Action<string> validateMigration=null)
        {
            Prepare();lock(gate){using(var db=Open(path,false,validateMigration)){if(int.TryParse(Meta(db,"profile_contract"),out int contract)&&contract>MechanicsOwnership.SaveVersion)throw new NotSupportedException("Newer logical save contract; writes blocked.");string algorithm=Meta(db,"digest_algorithm");if(algorithm!=null&&algorithm!="xor-sha256-rows-v1")throw new NotSupportedException("Newer state integrity format; writes blocked.");if(db.Scalar("PRAGMA quick_check")!="ok")throw new InvalidDataException("SQLite integrity check failed.");if(db.Query("PRAGMA foreign_key_check").Count!=0)throw new InvalidDataException("SQLite ownership references are invalid.");long generation=long.Parse(Meta(db,"reset_generation")??"0",CultureInfo.InvariantCulture);cachedRows=Rows(db);string digest=Meta(db,"records_digest");if(digest!=null&&digest!=Digest(cachedRows.Values))throw new InvalidDataException("Database record digest mismatch.");long commit=long.Parse(Meta(db,"commit_id")??"0",CultureInfo.InvariantCulture);ResolveResetIntent(path,generation,commit);if(generation<ResetFloor(path))throw new NotSupportedException("Recovery database predates the committed reset.");cachedPath=path;cachedCommit=commit;return StateRows.Restore(cachedRows.Values);}}
        }
        static long[] ResetIntent(string path){string file=Primary(path)+".reset-intent";return File.Exists(file)?File.ReadAllText(file,Encoding.UTF8).Split('|').Select(v=>long.Parse(v,CultureInfo.InvariantCulture)).ToArray():null;}
        static long ResetFloor(string path){string marker=Primary(path)+".reset-generation";long floor=File.Exists(marker)?long.Parse(File.ReadAllText(marker,Encoding.UTF8),CultureInfo.InvariantCulture):0;var intent=ResetIntent(path);return intent==null?floor:Math.Max(floor,intent[0]);}
        static void ResolveResetIntent(string path,long generation,long commit){if(path!=Primary(path))return;var intent=ResetIntent(path);if(intent==null)return;if(intent.Length!=2)throw new InvalidDataException("Invalid reset recovery intent.");if(generation==intent[0]-1&&commit==intent[1]){File.Delete(path+".reset-intent");return;}if(generation>=intent[0])try{AtomicText(path+".reset-generation",generation.ToString(CultureInfo.InvariantCulture));File.Delete(path+".reset-intent");}catch(IOException){ /* Keep the intent as the recovery floor until cleanup succeeds. */ }}
        static string Primary(string path)=>path.EndsWith(".bak",StringComparison.Ordinal)?path.Substring(0,path.Length-4):path;
        internal static bool CanInitialize(string path){try{if(File.Exists(path)&&new FileInfo(path).Length==0)return true;if(!IsDatabase(path))return false;lock(gate){using(var db=new SqliteNative(path,true)){int version=int.Parse(db.Scalar("PRAGMA user_version"),CultureInfo.InvariantCulture);if(version==0&&db.Scalar("SELECT name FROM sqlite_master WHERE type='table' LIMIT 1")==null)return true;if(db.Scalar("PRAGMA application_id")!=ApplicationId.ToString(CultureInfo.InvariantCulture)||version>SchemaVersion)return false;return db.Scalar("SELECT COUNT(*) FROM state_records")=="0"&&(Meta(db,"commit_id")==null||Meta(db,"commit_id")=="0");}}}catch{return false;}}
        internal static void Migrate(string path){Prepare();lock(gate){using(var db=Open(path)){} }}
        internal static string ExportJson(string path){string json=Read(path);return DrivingPreferences.AddToJson(json,Path.Combine(Path.GetDirectoryName(path),"driving-settings.json"));}
        internal static void Write(string path,Snapshot snapshot,bool reset=false)
        {
            long gateStart=Stamp();
            lock(gate)
            {
                snapshot.trace?.Elapsed("writer gate wait",gateStart);
                long timing=Stamp();
                if(snapshot.rows==null)snapshot.rows=StateRows.CaptureJson(snapshot.json);
                SaveMeasured(snapshot,"record projection",ref timing);
                snapshot.trace?.Value("projected state records",snapshot.rows.Count);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var db=Open(path))
                {
                    SaveMeasured(snapshot,"open + schema checks",ref timing);
                    db.Exec("BEGIN IMMEDIATE");
                    long oldCommit=long.Parse(Meta(db,"commit_id")??"0",CultureInfo.InvariantCulture);
                    if(snapshot.expectedCommit!=oldCommit||cachedPath==path&&cachedCommit>=0&&cachedCommit!=oldCommit)
                        throw new InvalidOperationException("Save changed in another writer; reload before saving.");
                    long savedRevision=long.Parse(Meta(db,"ledger_revision")??"-1",CultureInfo.InvariantCulture);
                    if(snapshot.revision<savedRevision)throw new InvalidOperationException("Stale ledger revision cannot overwrite a newer save.");
                    var before=cachedPath==path&&cachedRows!=null?cachedRows:Rows(db);
                    var after=snapshot.rows.ToDictionary(r=>r.Id,StringComparer.Ordinal);
                    long generation=long.Parse(Meta(db,"reset_generation")??"0",CultureInfo.InvariantCulture);
                    SaveMeasured(snapshot,"delta index",ref timing);
                    snapshot.trace?.Value("previous state records",before.Count);
                    if(reset)
                    {
                        generation++;
                        AtomicText(path+".reset-intent",generation.ToString(CultureInfo.InvariantCulture)+"|"+oldCommit.ToString(CultureInfo.InvariantCulture));
                        FailureInjection?.Invoke("after-reset-intent");
                    }
                    int changed=0,removed=0,inserted=0,updated=0,unchanged=0;
                    try
                    {
                        long rowAndOwnershipStart=Stamp();
                        using(var remove=db.Prepare("DELETE FROM state_records WHERE collection=? AND record_key=?"))
                            foreach(var old in before.Values.Where(r=>!after.ContainsKey(r.Id)))
                            {remove.Execute(old.path,old.key);changed++;removed++;}
                        using(var upsert=db.Prepare("INSERT INTO state_records(collection,record_key,ordinal,payload) VALUES(?,?,?,?) ON CONFLICT(collection,record_key) DO UPDATE SET ordinal=excluded.ordinal,payload=excluded.payload"))
                            foreach(var row in snapshot.rows)
                            {
                                if(before.TryGetValue(row.Id,out var old))
                                {
                                    if(old.json==row.json&&old.order==row.order)
                                    {row.digest=old.digest;unchanged++;continue;}
                                    updated++;
                                }
                                else inserted++;
                                upsert.Execute(row.path,row.key,row.order,row.json);
                                changed++;
                            }
                        SaveMeasured(snapshot,"state row delta SQL",ref timing);
                        snapshot.trace?.Value("changed state records",changed);
                        snapshot.trace?.Value("inserted state records",inserted);
                        snapshot.trace?.Value("updated state records",updated);
                        snapshot.trace?.Value("removed state records",removed);
                        snapshot.trace?.Value("unchanged state records",unchanged);
                        Sync(db,"owners",new[]{"id","kind","definition_id"},snapshot.owners,1);
                        Sync(db,"components",new[]{"id","definition_id","owner_id","location"},snapshot.components,1);
                        Sync(db,"installed_slots",new[]{"owner_id","slot","part_id"},snapshot.slots,2);
                        SaveMeasured(snapshot,"ownership delta SQL",ref timing);
                        // Preserve the pre-existing aggregate measurement label
                        // for SQLite domain verifier and historical comparisons.
                        if(Measurement!=null)Measurement("row and ownership delta SQL",
                            (Stamp()-rowAndOwnershipStart)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                        SetMeta(db,"digest_algorithm","xor-sha256-rows-v1");
                        SetMeta(db,"records_digest",Digest(snapshot.rows));
                        SaveMeasured(snapshot,"integrity digest",ref timing);
                        SetMeta(db,"commit_id",(oldCommit+1).ToString(CultureInfo.InvariantCulture));
                        SetMeta(db,"ledger_revision",snapshot.revision.ToString(CultureInfo.InvariantCulture));
                        SetMeta(db,"reset_generation",generation.ToString(CultureInfo.InvariantCulture));
                        SetMeta(db,"game_build",snapshot.build);
                        if(Meta(db,"created_build")==null)SetMeta(db,"created_build",snapshot.build);
                        SetMeta(db,"content_revision",snapshot.content);
                        SetMeta(db,"physics_identity",snapshot.physics);
                        SetMeta(db,"profile_contract",snapshot.saveVersion);
                        SaveMeasured(snapshot,"digest + metadata",ref timing);
                        db.Exec("DELETE FROM pending_checkpoints");
                        long preCommit=Stamp();
                        FailureInjection?.Invoke("before-commit");
                        snapshot.trace?.Elapsed("precommit injection wait",preCommit);
                        timing=Stamp();
                        db.Exec("COMMIT");
                        SaveMeasured(snapshot,"durable commit",ref timing);
                    }
                    catch
                    {
                        db.Exec("ROLLBACK");
                        if(reset&&File.Exists(path+".reset-intent"))File.Delete(path+".reset-intent");
                        throw;
                    }
                    if(reset)
                    {
                        try{AtomicText(path+".reset-generation",generation.ToString(CultureInfo.InvariantCulture));File.Delete(path+".reset-intent");}
                        catch(Exception e){backupWarning="Reset is committed; recovery intent retained: "+e.Message;}
                    }
                    cachedPath=path;cachedRows=after;cachedCommit=oldCommit+1;
                    snapshot.committed=cachedCommit;LastChangedRows=changed;
                    if(oldCommit==0||reset)
                    {
                        try{db.Backup(path+".bak");}
                        catch(Exception e){backupWarning="Recovery copy refresh failed; durable primary is retained: "+e.Message;}
                    }
                    else ScheduleBackup(path);
                    SaveMeasured(snapshot,"backup scheduling",ref timing);
                }
            }
        }
        static void Sync(SqliteNative db,string table,string[] columns,List<string[]> values,int keyCount)
        {
            string Key(string[] r)=>string.Join("\n",r.Take(keyCount));var old=db.Query("SELECT "+string.Join(",",columns)+" FROM "+table).ToDictionary(Key,StringComparer.Ordinal);var next=values.ToDictionary(Key,StringComparer.Ordinal);
            using(var delete=db.Prepare("DELETE FROM "+table+" WHERE "+string.Join(" AND ",columns.Take(keyCount).Select(c=>c+"=?"))))foreach(var row in old.Where(p=>!next.ContainsKey(p.Key)))delete.Execute(row.Value.Take(keyCount).Cast<object>().ToArray());
            using(var upsert=db.Prepare("INSERT INTO "+table+"("+string.Join(",",columns)+") VALUES("+string.Join(",",columns.Select(c=>"?"))+") ON CONFLICT("+string.Join(",",columns.Take(keyCount))+") DO UPDATE SET "+string.Join(",",columns.Skip(keyCount).Select(c=>c+"=excluded."+c))))foreach(var row in values)if(!old.TryGetValue(Key(row),out var prior)||!row.SequenceEqual(prior))upsert.Execute(row.Cast<object>().ToArray());
        }
        static readonly object backupGate=new object();
        static System.Threading.Tasks.Task backupWorker;static string pendingBackup;
        static volatile string backupWarning;internal static string BackupWarning=>backupWarning;
        static void ScheduleBackup(string path){lock(backupGate){pendingBackup=path;if(backupWorker==null||backupWorker.IsCompleted)backupWorker=System.Threading.Tasks.Task.Run((Action)BackupLoop);}}
        static void BackupLoop(){while(true){string path;lock(backupGate){path=pendingBackup;pendingBackup=null;if(path==null){backupWorker=null;return;}}try{string temp=path+".backup-next";if(File.Exists(temp))File.Delete(temp);using(var source=new SqliteNative(path,true)){if(source.Scalar("PRAGMA user_version")!=SchemaVersion.ToString(CultureInfo.InvariantCulture))continue;source.Backup(temp);}using(var result=new SqliteNative(temp,true)){if(result.Scalar("PRAGMA user_version")!=SchemaVersion.ToString(CultureInfo.InvariantCulture)||long.Parse(Meta(result,"reset_generation")??"0",CultureInfo.InvariantCulture)<ResetFloor(path))continue;}if(File.Exists(path+".bak"))File.Replace(temp,path+".bak",null);else File.Move(temp,path+".bak");backupWarning=null;}catch(Exception error){backupWarning="Recovery copy refresh failed; durable primary is retained: "+error.Message;}}}
        internal static void DrainBackups(){System.Threading.Tasks.Task worker;lock(backupGate)worker=backupWorker;worker?.GetAwaiter().GetResult();}
        internal static void WriteCheckpoint(string path,string run,string json){lock(gate){using(var db=Open(path,false)){db.Exec("BEGIN IMMEDIATE");try{using(var s=db.Prepare("INSERT INTO pending_checkpoints(run_id,payload) VALUES(?,?) ON CONFLICT(run_id) DO UPDATE SET payload=excluded.payload"))s.Execute(run,json);db.Exec("COMMIT");}catch{db.Exec("ROLLBACK");throw;}}}}
        internal static string[] Checkpoints(string path){lock(gate){if(!File.Exists(path))return Array.Empty<string>();using(var db=Open(path,false))return db.Query("SELECT payload FROM pending_checkpoints ORDER BY run_id").Select(r=>r[0]).ToArray();}}
        internal static void ClearCheckpoints(string path){lock(gate){if(!File.Exists(path))return;using(var db=Open(path,false))db.Exec("DELETE FROM pending_checkpoints");}}
        internal static void RecoverBackup(string primary,string backup){lock(gate){using(var db=Open(backup,false)){string temp=primary+".recover.tmp";if(File.Exists(temp))File.Delete(temp);db.Backup(temp);string corrupt=primary+".corrupt-"+DragLab.Domain.RuntimeEntropy.UtcNow.Ticks;foreach(string suffix in new[]{"-wal","-shm","-journal"})if(File.Exists(primary+suffix))File.Move(primary+suffix,corrupt+suffix);if(File.Exists(primary))File.Replace(temp,primary,corrupt);else File.Move(temp,primary);}Invalidate();}}
        internal static void AtomicText(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){byte[] bytes=Encoding.UTF8.GetBytes(text);file.Write(bytes,0,bytes.Length);file.Flush(true);}if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
    }
}
