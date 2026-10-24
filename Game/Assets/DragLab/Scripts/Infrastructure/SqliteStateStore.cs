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
        internal const int SchemaVersion=3,ApplicationId=0x444C4142;
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
        internal sealed class Snapshot {public List<StateRows.Row> rows;public List<string[]> owners,components,slots;public SqliteTypedProfileData typedData;public long revision,expectedCommit,committed;public string build,content,physics,saveVersion;public SaveTrace trace;}
        static readonly object gate=new object();
        static Migration[] migrations;
        static string cachedPath;static long cachedCommit=-1;static Dictionary<string,StateRows.Row> cachedRows;
        internal static long ReadCommit=>cachedCommit;
        internal static int LastChangedRows {get;private set;}
        internal static Action<string> FailureInjection;
        internal static Action<string,double> Measurement;
        internal static int LastMigrationFluidTokenDiscrepancies {get;private set;}
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
        static bool HistoryMatches(Migration migration,string checksum)
        {
            if(migration==null||checksum==null)return false;
            if(migration.version==3)return migration.sql.IndexOf('\r')<0&&migration.hash==checksum;
            if(migration.version!=1&&migration.version!=2)return migration.hash==checksum;
            // 001/002 were historically hashed from CRLF resources. Git normalizes
            // those immutable files to LF on checkout, so accept only the two
            // hashes of the exact same SQL text with either historical newline form.
            string lf=migration.sql.Replace("\r\n","\n").Replace("\r","\n");
            string lfHash=Hash(lf),crlfHash=Hash(lf.Replace("\n","\r\n"));
            return checksum==lfHash||checksum==crlfHash;
        }
        static void VerifySchema1State(SqliteNative db,string path,bool comparePrimary)
        {
            if(db.Scalar("PRAGMA user_version")!="1"||db.Scalar("PRAGMA application_id")!=ApplicationId.ToString(CultureInfo.InvariantCulture))
                throw new InvalidDataException("Schema-1 migration source identity is invalid.");
            if(db.Scalar("PRAGMA quick_check")!="ok"||db.Query("PRAGMA foreign_key_check").Count!=0)
                throw new InvalidDataException("Schema-1 migration source integrity check failed.");
            var history=db.Query("SELECT version,checksum FROM schema_migrations ORDER BY version");
            if(history.Count!=1||history[0][0]!="1"||!HistoryMatches(migrations[0],history[0][1]))
                throw new InvalidDataException("Schema-1 migration source history is invalid.");
            var rows=Rows(db);
            string algorithm=Meta(db,"digest_algorithm"),digest=Meta(db,"records_digest");
            long commit=long.Parse(Meta(db,"commit_id")??"0",CultureInfo.InvariantCulture);
            long generation=long.Parse(Meta(db,"reset_generation")??"0",CultureInfo.InvariantCulture);
            if(commit<0||generation<0)throw new InvalidDataException("Schema-1 commit or reset generation is negative.");
            bool emptyBootstrap=rows.Count==0&&commit==0&&generation==0&&algorithm==null&&digest==null;
            if(!emptyBootstrap&&(algorithm!="xor-sha256-rows-v1"||digest==null||digest!=Digest(rows.Values)))
                throw new InvalidDataException("Schema-1 legacy row digest verification failed.");
            if(comparePrimary)
            {
                ResolveResetIntent(path,generation,commit);
                if(generation<ResetFloor(path))throw new NotSupportedException("Schema-1 migration source predates the committed reset floor.");
            }
        }
        static void VerifySchema2State(SqliteNative db,string path,bool comparePrimary)
        {
            if(db.Scalar("PRAGMA user_version")!="2"||db.Scalar("PRAGMA application_id")!=ApplicationId.ToString(CultureInfo.InvariantCulture))
                throw new InvalidDataException("Schema-2 migration source identity is invalid.");
            if(db.Scalar("PRAGMA quick_check")!="ok"||db.Query("PRAGMA foreign_key_check").Count!=0)
                throw new InvalidDataException("Schema-2 migration source integrity or ownership check failed.");
            var history=db.Query("SELECT version,checksum FROM schema_migrations ORDER BY version");
            if(history.Count!=2||history[0][0]!="1"||history[1][0]!="2"||
                !HistoryMatches(migrations[0],history[0][1])||!HistoryMatches(migrations[1],history[1][1]))
                throw new InvalidDataException("Schema-2 migration source history is invalid.");
            var rows=Rows(db);
            string algorithm=Meta(db,"digest_algorithm");
            string digest=Meta(db,"records_digest");
            long commit=long.Parse(Meta(db,"commit_id")??"0",CultureInfo.InvariantCulture);
            if(commit<0)throw new InvalidDataException("Schema-2 commit fence is negative.");
            long generation=long.Parse(Meta(db,"reset_generation")??"0",CultureInfo.InvariantCulture);
            if(generation<0)throw new InvalidDataException("Schema-2 reset generation is negative.");
            if(rows.Count==0&&(commit!=0||!EmptyBootstrapMetadata(db,rows.Count,commit,generation)))
                throw new InvalidDataException("Schema-2 empty source has a nonzero commit or partial profile state.");
            bool emptyUncommitted=rows.Count==0&&commit==0&&algorithm==null&&digest==null;
            if(!emptyUncommitted&&(algorithm!="xor-sha256-rows-v1"||digest==null||digest!=Digest(rows.Values)))
                throw new InvalidDataException("Schema-2 migration source digest verification failed.");
            if(comparePrimary)
            {
                ResolveResetIntent(path,generation,commit);
                if(generation<ResetFloor(path))throw new NotSupportedException("Schema-2 migration source predates the committed reset floor.");
            }
        }
        static bool EmptyBootstrapMetadata(SqliteNative db,int legacyRowCount,long commit,long generation)
        {
            if(legacyRowCount!=0||commit!=0||generation!=0)return false;
            var metadata=db.Query("SELECT name,value FROM state_meta ORDER BY name").ToDictionary(row=>row[0],row=>row[1],StringComparer.Ordinal);
            foreach(var pair in metadata)
            {
                switch(pair.Key)
                {
                    case "commit_id":if(pair.Value!="0")return false;break;
                    case "reset_generation":if(pair.Value!="0")return false;break;
                    case "digest_algorithm":if(pair.Value!="xor-sha256-rows-v1"&&pair.Value!="xor-sha256-records-v2")return false;break;
                    case "records_digest":break;
                    default:return false;
                }
            }
            string algorithm=Meta(db,"digest_algorithm"),digest=Meta(db,"records_digest");
            if((algorithm==null)!=(digest==null))return false;
            if(algorithm=="xor-sha256-rows-v1"&&digest!=Digest(Enumerable.Empty<StateRows.Row>()))return false;
            if(algorithm=="xor-sha256-records-v2"&&digest!=AggregateV2(Enumerable.Empty<StateRows.Row>(),Enumerable.Empty<TypedStateRecord>()))return false;
            if(db.Scalar("SELECT COUNT(*) FROM pending_checkpoints")!="0")return false;
            foreach(string table in new[]{"owners","components","installed_slots"})
                if(db.Scalar("SELECT COUNT(*) FROM "+table)!="0")return false;
            return true;
        }
        static bool EmptyTypedBootstrap(SqliteNative db,int legacyRowCount,long commit,long generation)
        {
            if(!EmptyBootstrapMetadata(db,legacyRowCount,commit,generation))return false;
            foreach(string table in new[]{"profile_economy","fluid_states","economy_receipts","economy_receipt_items","operation_receipts","fluid_refill_results","record_integrity"})
                if(db.Scalar("SELECT COUNT(*) FROM "+table)!="0")return false;
            return true;
        }
        static void VerifyOrCreateOriginalVersionBackup(string path,SqliteNative source,int version)
        {
            if(version==1)VerifySchema1State(source,path,true);
            else if(version==2)VerifySchema2State(source,path,true);
            else throw new ArgumentOutOfRangeException(nameof(version));
            string backup=path+".schema-"+version.ToString(CultureInfo.InvariantCulture)+".bak";
            long sourceCommit=long.Parse(Meta(source,"commit_id")??"0",CultureInfo.InvariantCulture);
            long sourceGeneration=long.Parse(Meta(source,"reset_generation")??"0",CultureInfo.InvariantCulture);
            bool valid=false;
            if(File.Exists(backup))
            {
                try
                {
                    using(var prior=new SqliteNative(backup,true))
                    {
                        if(version==1)VerifySchema1State(prior,path,false);
                        else VerifySchema2State(prior,path,false);
                        valid=Meta(prior,"commit_id")==sourceCommit.ToString(CultureInfo.InvariantCulture)&&
                            Meta(prior,"reset_generation")==sourceGeneration.ToString(CultureInfo.InvariantCulture)&&
                            Meta(prior,"records_digest")==Meta(source,"records_digest")&&
                            RowsEqual(prior.Query("SELECT collection,record_key,ordinal,payload FROM state_records ORDER BY collection,record_key"),source.Query("SELECT collection,record_key,ordinal,payload FROM state_records ORDER BY collection,record_key"))&&
                            RowsEqual(prior.Query("SELECT name,value FROM state_meta ORDER BY name"),source.Query("SELECT name,value FROM state_meta ORDER BY name"))&&
                            RowsEqual(prior.Query("SELECT run_id,payload FROM pending_checkpoints ORDER BY run_id"),source.Query("SELECT run_id,payload FROM pending_checkpoints ORDER BY run_id"))&&
                            RowsEqual(prior.Query("SELECT version,name,checksum,applied_utc FROM schema_migrations ORDER BY version"),source.Query("SELECT version,name,checksum,applied_utc FROM schema_migrations ORDER BY version"))&&
                            (version==1||
                            (RowsEqual(prior.Query("SELECT id,kind,definition_id FROM owners ORDER BY id"),source.Query("SELECT id,kind,definition_id FROM owners ORDER BY id"))&&
                            RowsEqual(prior.Query("SELECT id,definition_id,owner_id,location FROM components ORDER BY id"),source.Query("SELECT id,definition_id,owner_id,location FROM components ORDER BY id"))&&
                            RowsEqual(prior.Query("SELECT owner_id,slot,part_id FROM installed_slots ORDER BY owner_id,slot"),source.Query("SELECT owner_id,slot,part_id FROM installed_slots ORDER BY owner_id,slot"))));
                    }
                }
                catch{valid=false;}
            }
            if(valid)return;
            string temp=backup+".next-"+Guid.NewGuid().ToString("N");
            source.Backup(temp);
            using(var check=new SqliteNative(temp,true)){if(version==1)VerifySchema1State(check,path,false);else VerifySchema2State(check,path,false);}
            if(new[]{"","-wal","-shm","-journal"}.Any(suffix=>File.Exists(backup+suffix)))
            {
                string preserved=backup+".unverified-"+Guid.NewGuid().ToString("N");
                var moved=new List<KeyValuePair<string,string>>();
                try
                {
                    foreach(string suffix in new[]{"","-wal","-shm","-journal"})
                    {
                        string original=backup+suffix;
                        if(!File.Exists(original))continue;
                        string destination=preserved+suffix;
                        File.Move(original,destination);
                        moved.Add(new KeyValuePair<string,string>(original,destination));
                    }
                    File.Move(temp,backup);
                }
                catch
                {
                    foreach(var item in moved.AsEnumerable().Reverse())
                        if(File.Exists(item.Value)&&!File.Exists(item.Key))File.Move(item.Value,item.Key);
                    throw;
                }
                return;
            }
            File.Move(temp,backup);
        }
        static string RawObjectField(string json,string name)
        {
            if(json==null||json.Length<2||json[0]!='{'||json[json.Length-1]!='}')throw new InvalidDataException("Malformed source object during fluid token verification.");
            var field=StateRows.Fields(json).FirstOrDefault(value=>value.Key==name);
            if(field.Key==null)throw new InvalidDataException("Missing source fluid field: "+name);
            return field.Value;
        }
        static int CompareRawFluidTokens(string sourceJson,PlayerProfile decoded)
        {
            string[] names={"oilLitres","coolantLitres","oilTemperatureC","oilPressureBar","coolantPressureBar","oilCondition","oilConsumedLitres","oilLeakedLitres","coolantLostLitres","serviceElapsedSeconds","oilBurnLps","oilLeakLps","coolantLeakLps","coolantVentLps"};
            var root=StateRows.Fields(sourceJson);
            var cars=StateRows.Items(root.First(field=>field.Key=="cars").Value);
            var engines=StateRows.Items(root.First(field=>field.Key=="engines").Value);
            if(cars.Length!=decoded.cars.Count||engines.Length!=decoded.engines.Count)throw new InvalidDataException("Fluid token source owner count changed during decode.");
            int mismatches=0;
            void Compare(string ownerJson,FluidState fluid)
            {
                if(fluid==null)throw new InvalidDataException("Missing supported fluid state during typed migration.");
                string sourceFluids=RawObjectField(ownerJson,"fluids");
                if(sourceFluids=="null")throw new InvalidDataException("Null authoritative fluid state during typed migration.");
                double[] values={fluid.oilLitres,fluid.coolantLitres,fluid.oilTemperatureC,fluid.oilPressureBar,fluid.coolantPressureBar,fluid.oilCondition,fluid.oilConsumedLitres,fluid.oilLeakedLitres,fluid.coolantLostLitres,fluid.serviceElapsedSeconds,fluid.oilBurnLps,fluid.oilLeakLps,fluid.coolantLeakLps,fluid.coolantVentLps};
                for(int i=0;i<names.Length;i++)
                {
                    string token=RawObjectField(sourceFluids,names[i]);
                    if(token=="null"||!double.TryParse(token,NumberStyles.Float,CultureInfo.InvariantCulture,out double parsed)||double.IsNaN(parsed)||double.IsInfinity(parsed))
                        throw new InvalidDataException("Malformed/nonfinite raw fluid token: "+names[i]);
                    if(BitConverter.DoubleToInt64Bits(parsed)!=BitConverter.DoubleToInt64Bits(values[i]))mismatches++;
                }
            }
            for(int i=0;i<cars.Length;i++)Compare(cars[i],decoded.cars[i].fluids);
            for(int i=0;i<engines.Length;i++)
            {
                var engine=decoded.engines[i];
                if(string.IsNullOrEmpty(engine.parentVehicleId))Compare(engines[i],engine.fluids);
                else
                {
                    // Attached engine fluid JSON is a verified nonauthoritative Unity placeholder.
                    // The parent car row remains the sole migrated fluid authority.
                    // The verified attached-engine row has no independent fluid authority.
                }
            }
            return mismatches;
        }
        static void ApplyTypedMigration(SqliteNative db,string path,Migration migration,Func<string,PlayerProfile> validateMigration,ref PlayerProfile migratedProfile)
        {
            VerifySchema2State(db,path,false);
            var sourceRows=Rows(db);
            string oldDigest=Meta(db,"records_digest");
            long sourceCommit=long.Parse(Meta(db,"commit_id")??"0",CultureInfo.InvariantCulture);
            long sourceGeneration=long.Parse(Meta(db,"reset_generation")??"0",CultureInfo.InvariantCulture);
            bool emptyBootstrap=EmptyBootstrapMetadata(db,sourceRows.Count,sourceCommit,sourceGeneration);
            bool emptyUncommitted=sourceRows.Count==0&&sourceCommit==0&&Meta(db,"digest_algorithm")==null&&oldDigest==null&&emptyBootstrap;
            if(sourceRows.Count==0&&!emptyBootstrap)
                throw new InvalidDataException("Empty schema-2 source contains committed or partial profile state.");
            if(!emptyUncommitted&&(Meta(db,"digest_algorithm")!="xor-sha256-rows-v1"||oldDigest==null||oldDigest!=Digest(sourceRows.Values)))
                throw new InvalidDataException("Schema-2 source digest failed before typed migration.");
            ResolveResetIntent(path,sourceGeneration,sourceCommit);
            if(sourceGeneration<ResetFloor(path))throw new NotSupportedException("Schema-2 migration source predates the committed reset floor.");
            if(sourceRows.Count>0)
            {
                if(validateMigration==null)throw new InvalidOperationException("A full supported-profile migration validator is required before typed conversion.");
                migratedProfile=validateMigration(StateRows.Restore(sourceRows.Values));
                if(migratedProfile==null)throw new InvalidDataException("Legacy migration validator returned no decoded profile.");
                string ledgerFence=Meta(db,"ledger_revision");
                if(!long.TryParse(ledgerFence,NumberStyles.Integer,CultureInfo.InvariantCulture,out long sourceLedgerRevision)||sourceLedgerRevision!=migratedProfile.revision)
                    throw new InvalidDataException("Schema-2 ledger revision fence disagrees with the validated source profile.");
                LastMigrationFluidTokenDiscrepancies=CompareRawFluidTokens(StateRows.Restore(sourceRows.Values),migratedProfile);
                if(LastMigrationFluidTokenDiscrepancies>0)
                    Debug.LogWarning("Typed migration retained the supported profile decoder's exact fluid values; independent JSON token parsing differed for "+LastMigrationFluidTokenDiscrepancies.ToString(CultureInfo.InvariantCulture)+" fluid fields.");
                VerifyProfileRelations(db,migratedProfile);
            }
            FailureInjection?.Invoke("migration:3:source-verified");
            db.Exec(migration.sql);
            using(var statement=db.Prepare("INSERT INTO schema_migrations(version,name,checksum,applied_utc) VALUES(?,?,?,?)"))
                statement.Execute(migration.version,migration.name,migration.hash,DragLab.Domain.RuntimeEntropy.UtcNow.ToString("O",CultureInfo.InvariantCulture));
            db.Exec("PRAGMA user_version=3");
            db.Exec("PRAGMA application_id="+ApplicationId);
            FailureInjection?.Invoke("migration:3");
            if(migratedProfile!=null)
            {
                var typed=SqliteTypedProfileData.Capture(migratedProfile);
                InsertTypedRecords(db,typed.StateRecords());
            }
            FailureInjection?.Invoke("migration:3:backfilled");
            StripLegacyTypedProjection(db);
            FailureInjection?.Invoke("migration:3:legacy-stripped");
            var remaining=Rows(db);
            var typedRows=ReadAllTypedRecords(db);
            RebuildIntegrity(db,remaining.Values,typedRows);
            var current=typedRows.Length==0?null:SqliteTypedProfileData.FromStateRecords(typedRows);
            if(current==null&&!EmptyTypedBootstrap(db,remaining.Count,sourceCommit,sourceGeneration))
                throw new InvalidDataException("Typed migration produced an incomplete empty bootstrap.");
            if(current!=null)SetMeta(db,"ledger_revision",current.Economy.Revision.ToString(CultureInfo.InvariantCulture));
            SetMeta(db,"digest_algorithm","xor-sha256-records-v2");
            SetMeta(db,"records_digest",AggregateV2(remaining.Values,typedRows));
            FailureInjection?.Invoke("migration:3:integrity-ready");
            VerifyV3Integrity(db,remaining.Values);
            VerifyV3Relations(db,typedRows);
            if(db.Scalar("PRAGMA quick_check")!="ok"||db.Query("PRAGMA foreign_key_check").Count!=0)
                throw new InvalidDataException("Typed migration produced invalid SQLite structure or references.");
            if(Meta(db,"records_digest")!=AggregateV2(remaining.Values,ReadAllTypedRecords(db)))
                throw new InvalidDataException("Typed migration aggregate verification failed.");
            FailureInjection?.Invoke("migration:3:validated");
            FailureInjection?.Invoke("migration:3:before-commit");
        }
        static SqliteNative Open(string path,bool create=true,Func<string,PlayerProfile> validateMigration=null)
            =>Open(path,create,validateMigration,out _);
        static SqliteNative Open(string path,bool create,Func<string,PlayerProfile> validateMigration,out PlayerProfile migratedProfile)
        {
            migratedProfile=null;
            if(File.Exists(path)&&!IsDatabase(path)&&!(create&&new FileInfo(path).Length==0)){string text=File.ReadAllText(path,Encoding.UTF8);if(text.TrimStart().StartsWith("{")){var version=StateRows.Fields(text.Trim()).FirstOrDefault(f=>f.Key=="saveVersion").Value;if(int.TryParse(version,out int contract)&&contract>MechanicsOwnership.SaveVersion)throw new NotSupportedException("Unexpected future save format; original preserved.");}throw new InvalidDataException("Not a SQLite save database.");}
            if(!File.Exists(path)&&!create)throw new FileNotFoundException("Save database is missing.",path);
            var db=new SqliteNative(path);
            try{
                int version=int.Parse(db.Scalar("PRAGMA user_version"),CultureInfo.InvariantCulture),app=int.Parse(db.Scalar("PRAGMA application_id"),CultureInfo.InvariantCulture);
                if(version>SchemaVersion)throw new NotSupportedException("Newer save database schema; writes are blocked.");
                if(app!=0&&app!=ApplicationId)throw new NotSupportedException("Database belongs to another application.");
                if(version==0&&db.Scalar("SELECT name FROM sqlite_master WHERE type='table' LIMIT 1")!=null)throw new InvalidDataException("Unversioned populated database cannot be treated as a new save.");
                if(version>0){var history=db.Query("SELECT version,checksum FROM schema_migrations ORDER BY version");if(history.Count!=version)throw new InvalidDataException("Incomplete migration history.");for(int i=0;i<history.Count;i++){if(int.Parse(history[i][0],CultureInfo.InvariantCulture)!=i+1||!HistoryMatches(migrations[i],history[i][1]))throw new NotSupportedException("Migration history does not match this build.");}}
                if(version==1)VerifySchema1State(db,path,true);
                if(version==2)VerifySchema2State(db,path,true);
                // Existing v3 sources keep their journal mode while validating.
                // Rejected reads must not rewrite a DELETE-mode source's header.
                if(version>0&&version<SchemaVersion)
                {
                    VerifyOrCreateOriginalVersionBackup(path,db,version);
                }
                if(version<SchemaVersion)
                {
                    int sourceVersion=version;
                    db.Exec("BEGIN IMMEDIATE");
                    try
                    {
                        foreach(var migration in migrations.Where(m=>m.version>sourceVersion))
                        {
                            if(migration.version==3)
                                ApplyTypedMigration(db,path,migration,validateMigration,ref migratedProfile);
                            else
                            {
                                db.Exec(migration.sql);
                                using(var statement=db.Prepare("INSERT INTO schema_migrations(version,name,checksum,applied_utc) VALUES(?,?,?,?)"))statement.Execute(migration.version,migration.name,migration.hash,DragLab.Domain.RuntimeEntropy.UtcNow.ToString("O",CultureInfo.InvariantCulture));
                                db.Exec("PRAGMA user_version="+migration.version);
                                if(migration.version==2)
                                {
                                    var initialRows=Rows(db);
                                    string algorithm=Meta(db,"digest_algorithm"),digest=Meta(db,"records_digest");
                                    if(algorithm==null&&digest==null&&initialRows.Count==0&&
                                        long.Parse(Meta(db,"commit_id")??"0",CultureInfo.InvariantCulture)==0&&
                                        long.Parse(Meta(db,"reset_generation")??"0",CultureInfo.InvariantCulture)==0)
                                    {
                                        SetMeta(db,"digest_algorithm","xor-sha256-rows-v1");
                                        SetMeta(db,"records_digest",Digest(initialRows.Values));
                                    }
                                    else if(algorithm!="xor-sha256-rows-v1"||digest==null||digest!=Digest(initialRows.Values))
                                        throw new InvalidDataException("Schema-1 digest changed during ownership migration.");
                                }
                                FailureInjection?.Invoke("migration:"+migration.version);
                            }
                            if(migration.version==1)db.Exec("PRAGMA application_id="+ApplicationId);
                        }
                        db.Exec("COMMIT");
                    }
                    catch{db.Exec("ROLLBACK");migratedProfile=null;throw;}
                    db.Exec("PRAGMA journal_mode=WAL;");
                }
                return db;
            }catch{db.Dispose();throw;}
        }
        internal static Snapshot Capture(PlayerProfile profile)
        {
            Prepare();return LegacyCaptureManaged(profile,Application.version);
        }
        internal static Snapshot CaptureManaged(PlayerProfile profile,string build,SaveTrace trace=null)
            =>LegacyCaptureManaged(profile,build,trace);
        internal static Snapshot LegacyCaptureManaged(PlayerProfile profile,string build,SaveTrace trace=null)
        {
            long capture=trace==null?0:Stamp();
            string json=JsonUtility.ToJson(profile);
            if(trace!=null){
                trace.Elapsed("profile serialization",capture);
                trace.Value("profile JSON characters",json.Length);
                trace.Value("profile JSON UTF8 bytes",Encoding.UTF8.GetByteCount(json));
            }
            long relationships=trace==null?0:Stamp();
            var result=new Snapshot{revision=profile.revision,expectedCommit=profile.storageCommit,build=build,content=MechanicalCatalog.Version.ToString(CultureInfo.InvariantCulture),physics=MechanicsSchema.Model,saveVersion=profile.saveVersion.ToString(CultureInfo.InvariantCulture),trace=trace,
                owners=profile.cars.Select(c=>new[]{c.instanceId,"car",c.vehicleId}).Concat(profile.engines.Select(e=>new[]{e.instanceId,"engine",e.vehicleDefinitionId})).ToList(),
                components=profile.parts.Select(p=>new[]{p.instanceId,p.definitionId,string.IsNullOrEmpty(p.ownerId)?null:p.ownerId,((int)p.location).ToString(CultureInfo.InvariantCulture)}).ToList(),
                slots=profile.cars.SelectMany(c=>c.installed.slots.Select(s=>new[]{c.instanceId,s.slot,s.partInstanceId})).Concat(profile.engines.SelectMany(e=>e.slots.Select(s=>new[]{e.instanceId,s.slot,s.partInstanceId}))).ToList(),
                typedData=SqliteTypedProfileData.Capture(profile)};
            var legacyRows=StateRows.CaptureJson(json).Select(row=>new StateRows.Row{path=row.path,key=row.key,order=row.order,json=row.json,digest=row.digest==null?null:(byte[])row.digest.Clone()}).ToList();
            StripSnapshotTypedProjection(legacyRows);
            result.rows=legacyRows;
            if(trace!=null){
                trace.Elapsed("relationship projection",relationships);
                trace.Value("owner records",result.owners.Count);
                trace.Value("component records",result.components.Count);
                trace.Value("slot records",result.slots.Count);
            }
            return result;
        }
        static void StripSnapshotTypedProjection(List<StateRows.Row> rows)
        {
            rows.RemoveAll(row=>(row.path=="$"&&new[]{"cash","revision","lastEconomyUtcTicks","economyReceipts"}.Contains(row.key,StringComparer.Ordinal))||row.path=="$/economyReceipts"||row.path.StartsWith("$/economyReceipts/",StringComparison.Ordinal));
            foreach(var row in rows.Where(row=>row.path=="$/cars"||row.path=="$/engines"))row.json=RemoveObjectField(row.json,"fluids",row.path=="$/cars");
        }
        internal static void Invalidate(){lock(gate){cachedPath=null;cachedRows=null;cachedCommit=-1;}}
        static void InvalidateRowsAfterTypedCommit(string path,long commit){cachedPath=path;cachedRows=null;cachedCommit=commit;}
        static string Digest(IEnumerable<StateRows.Row> rows){var sum=new byte[32];using(var sha=SHA256.Create())foreach(var row in rows){if(row.digest==null)row.digest=sha.ComputeHash(Encoding.UTF8.GetBytes(row.Id+"\n"+row.order+"\n"+row.json));for(int i=0;i<sum.Length;i++)sum[i]^=row.digest[i];}return Convert.ToBase64String(sum);}
        static Dictionary<string,StateRows.Row> Rows(SqliteNative db)=>db.Query("SELECT collection,record_key,ordinal,payload FROM state_records").Select(r=>new StateRows.Row{path=r[0],key=r[1],order=int.Parse(r[2],CultureInfo.InvariantCulture),json=r[3]}).ToDictionary(r=>r.Id,StringComparer.Ordinal);
        static byte[] LegacyHash(StateRows.Row row)
        {
            using(var sha=SHA256.Create())return sha.ComputeHash(Encoding.UTF8.GetBytes(row.Id+"\n"+row.order.ToString(CultureInfo.InvariantCulture)+"\n"+row.json));
        }
        static string Hex(byte[] bytes)=>TypedStateEncoding.ToLowerHex(bytes);
        static bool RowsEqual(List<string[]> left,List<string[]> right)
        {
            if(left.Count!=right.Count)return false;
            for(int i=0;i<left.Count;i++)if(!left[i].SequenceEqual(right[i],StringComparer.Ordinal))return false;
            return true;
        }
        static byte[] FromHex(string value)
        {
            if(value==null||value.Length!=64)throw new InvalidDataException("Invalid typed integrity hash length.");
            var bytes=new byte[32];for(int i=0;i<bytes.Length;i++)bytes[i]=byte.Parse(value.Substring(i*2,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture);return bytes;
        }
        static string AggregateV2(IEnumerable<StateRows.Row> legacy,IEnumerable<TypedStateRecord> typed)
        {
            var sum=new byte[32];
            foreach(var row in legacy)Xor(sum,LegacyHash(row));
            foreach(var row in typed)Xor(sum,row.HashBytes());
            return Hex(sum);
        }
        static void Xor(byte[] target,byte[] value){for(int i=0;i<target.Length;i++)target[i]^=value[i];}
        static void InsertTypedRecords(SqliteNative db,IEnumerable<TypedStateRecord> source)
        {
            foreach(var row in source)
            {
                var fields=row.Fields();
                string sql="INSERT INTO "+row.TableName+"("+string.Join(",",fields.Select(field=>field.Name))+") VALUES("+string.Join(",",fields.Select(_=>"?"))+")";
                using(var statement=db.Prepare(sql))statement.Execute(row.SqlValues());
            }
        }
        static string[] PrimaryKeyColumns(string table)
        {
            switch(table)
            {
                case "profile_economy":return new[]{"singleton"};
                case "fluid_states":return new[]{"owner_kind","owner_id"};
                case "economy_receipts":return new[]{"id"};
                case "economy_receipt_items":return new[]{"receipt_id","item_kind","item_order"};
                case "operation_receipts":return new[]{"save_slot","reset_generation","operation_id"};
                case "fluid_refill_results":return new[]{"save_slot","reset_generation","operation_id"};
                default:throw new ArgumentException("Unknown typed record table.",nameof(table));
            }
        }
        static void DeleteTypedRecord(SqliteNative db,TypedStateRecord row)
        {
            var fields=row.Fields();var key=PrimaryKeyColumns(row.TableName);
            string predicate=string.Join(" AND ",key.Select(name=>name+"=?"));
            using(var statement=db.Prepare("DELETE FROM "+row.TableName+" WHERE "+predicate))
                statement.Execute(key.Select(name=>fields.Single(field=>field.Name==name).SqlValue()).ToArray());
        }
        static void UpsertTypedRecord(SqliteNative db,TypedStateRecord row)
        {
            var fields=row.Fields();var key=PrimaryKeyColumns(row.TableName);
            string names=string.Join(",",fields.Select(field=>field.Name));
            string placeholders=string.Join(",",fields.Select(_=>"?"));
            string conflict=string.Join(",",key);
            string updates=string.Join(",",fields.Where(field=>!key.Contains(field.Name,StringComparer.Ordinal)).Select(field=>field.Name+"=excluded."+field.Name));
            string sql="INSERT INTO "+row.TableName+"("+names+") VALUES("+placeholders+") ON CONFLICT("+conflict+") DO UPDATE SET "+updates;
            using(var statement=db.Prepare(sql))statement.Execute(row.SqlValues());
        }
        static TypedStateRecord[] SyncTypedProfileState(SqliteNative db,SqliteTypedProfileData source)
        {
            var tables=new HashSet<string>(new[]{"profile_economy","fluid_states","economy_receipts","economy_receipt_items"},StringComparer.Ordinal);
            var old=ReadAllTypedRecords(db).Where(row=>tables.Contains(row.TableName)).ToArray();
            var next=source.StateRecords();
            var after=next.ToDictionary(row=>row.TableName+"\n"+row.RecordKey,StringComparer.Ordinal);
            foreach(var row in old.Where(row=>!after.ContainsKey(row.TableName+"\n"+row.RecordKey))
                .OrderBy(row=>row.TableName=="economy_receipt_items"?0:row.TableName=="fluid_states"?1:2))DeleteTypedRecord(db,row);
            var prior=old.ToDictionary(row=>row.TableName+"\n"+row.RecordKey,StringComparer.Ordinal);
            foreach(var row in next)
            {
                string key=row.TableName+"\n"+row.RecordKey;
                if(prior.TryGetValue(key,out var oldRow)&&oldRow.HashHex()==row.HashHex())continue;
                UpsertTypedRecord(db,row);
            }
            return ReadAllTypedRecords(db);
        }
        static string[] TableColumns(string table)
        {
            switch(table)
            {
                case "profile_economy":return new[]{"singleton","cash","revision","last_economy_utc_ticks"};
                case "fluid_states":return new[]{"owner_kind","owner_id","value_version","oil_litres_bits","coolant_litres_bits","oil_temperature_c_bits","oil_pressure_bar_bits","coolant_pressure_bar_bits","oil_condition_bits","oil_consumed_litres_bits","oil_leaked_litres_bits","coolant_lost_litres_bits","service_elapsed_seconds_bits","oil_burn_lps_bits","oil_leak_lps_bits","coolant_leak_lps_bits","coolant_vent_lps_bits"};
                case "economy_receipts":return new[]{"id","receipt_order","operation","target","revision","utc_ticks","cash_before","cash_after","fuel_added_bits","oil_added_bits","coolant_added_bits"};
                case "economy_receipt_items":return new[]{"receipt_id","item_kind","item_order","item_id"};
                case "operation_receipts":return new[]{"save_slot","reset_generation","operation_id","command_kind","intent_version","intent_hex","intent_sha256_hex","ledger_revision_before","ledger_revision_after","storage_commit_before","storage_commit_after","content_revision","physics_identity","committed_utc_ticks","domain_receipt_id"};
                case "fluid_refill_results":return new[]{"save_slot","reset_generation","operation_id","car_id","cost","cash_after","oil_added_bits","coolant_added_bits","oil_after_bits","coolant_after_bits","domain_receipt_id"};
                default:throw new ArgumentException("Unknown typed record table.",nameof(table));
            }
        }
        static bool IsTextColumn(string table,int index)
        {
            switch(table)
            {
                case "profile_economy":return false;
                case "fluid_states":return index<2;
                case "economy_receipts":return index==0||index==2||index==3;
                case "economy_receipt_items":return index==0||index==1||index==3;
                case "operation_receipts":return index==0||index==2||index==3||index==5||index==6||index==12||index==14;
                case "fluid_refill_results":return index==0||index==2||index==3||index==10;
                default:throw new ArgumentException("Unknown typed record table.",nameof(table));
            }
        }
        static TypedStateRecord CreateTypedRecord(string table,string[] values)
        {
            var columns=TableColumns(table);if(values.Length!=columns.Length)throw new InvalidDataException("Typed SQL row column count is invalid.");
            var fields=new TypedStateField[columns.Length];
            for(int i=0;i<columns.Length;i++)
            {
                if(values[i]==null)fields[i]=TypedStateField.Null(columns[i]);
                else if(IsTextColumn(table,i))fields[i]=TypedStateField.Text(columns[i],values[i]);
                else fields[i]=TypedStateField.Int64(columns[i],long.Parse(values[i],NumberStyles.Integer,CultureInfo.InvariantCulture));
            }
            string key;
            switch(table)
            {
                case "profile_economy":key="1";break;
                case "fluid_states":key=TypedStateEncoding.CompositeKey(fields[0],fields[1]);break;
                case "economy_receipts":key=values[0];break;
                case "economy_receipt_items":key=TypedStateEncoding.CompositeKey(fields[0],fields[1],fields[2]);break;
                case "operation_receipts":key=TypedStateEncoding.CompositeKey(fields[0],fields[1],fields[2]);break;
                case "fluid_refill_results":key=TypedStateEncoding.CompositeKey(fields[0],fields[1],fields[2]);break;
                default:throw new ArgumentException("Unknown typed record table.",nameof(table));
            }
            return new TypedStateRecord(table,key,fields);
        }
        static TypedStateRecord[] ReadAllTypedRecords(SqliteNative db)
        {
            var rows=new List<TypedStateRecord>();
            foreach(string table in new[]{"profile_economy","fluid_states","economy_receipts","economy_receipt_items","operation_receipts","fluid_refill_results"})
            {
                string[] columns=TableColumns(table);
                foreach(var values in db.Query("SELECT "+string.Join(",",columns)+" FROM "+table))rows.Add(CreateTypedRecord(table,values));
            }
            return rows.ToArray();
        }
        static void RebuildIntegrity(SqliteNative db,IEnumerable<StateRows.Row> legacy,IEnumerable<TypedStateRecord> typed)
        {
            db.Exec("DELETE FROM record_integrity");
            using(var insert=db.Prepare("INSERT INTO record_integrity(family,record_key,row_hash_hex) VALUES(?,?,?)"))
            {
                foreach(var row in legacy)insert.Execute("legacy",row.Id,Hex(LegacyHash(row)));
                foreach(var row in typed)insert.Execute(row.TableName,row.RecordKey,row.HashHex());
            }
        }
        static void VerifyV3Integrity(SqliteNative db,IEnumerable<StateRows.Row> source)
        {
            var expected=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var row in source)expected.Add("legacy\n"+row.Id,Hex(LegacyHash(row)));
            foreach(var row in ReadAllTypedRecords(db))expected.Add(row.TableName+"\n"+row.RecordKey,row.HashHex());
            var actual=db.Query("SELECT family,record_key,row_hash_hex FROM record_integrity");
            if(actual.Count!=expected.Count)throw new InvalidDataException("Typed row integrity index has missing or extra records.");
            foreach(var row in actual)
            {
                string key=row[0]+"\n"+row[1];
                if(!expected.TryGetValue(key,out var hash)||hash!=row[2])throw new InvalidDataException("Typed row integrity mismatch: "+row[0]+"/"+row[1]);
            }
        }
        static void VerifyV3Relations(SqliteNative db,IEnumerable<TypedStateRecord> typed)
        {
            var fluids=typed.Where(row=>row.TableName==TypedFluidStateRecord.Table).ToArray();
            var owners=db.Query("SELECT id,kind FROM owners").ToDictionary(row=>row[0],row=>row[1],StringComparer.Ordinal);
            foreach(var fluid in fluids)
            {
                string id=fluid.ReadText("owner_id"),kind=fluid.ReadText("owner_kind");
                if(!owners.TryGetValue(id,out var ownerKind)||ownerKind!=kind)throw new InvalidDataException("Typed fluid owner identity/kind mismatch.");
            }
        }
        static void VerifyProfileRelations(SqliteNative db,PlayerProfile profile)
        {
            if(profile==null)throw new ArgumentNullException(nameof(profile));
            var expectedOwners=profile.cars.Select(car=>new[]{car.instanceId,"car",car.vehicleId})
                .Concat(profile.engines.Select(engine=>new[]{engine.instanceId,"engine",engine.vehicleDefinitionId}))
                .OrderBy(row=>row[0],StringComparer.Ordinal).ToList();
            var actualOwners=db.Query("SELECT id,kind,definition_id FROM owners ORDER BY id");
            if(!RowsEqual(actualOwners,expectedOwners))throw new InvalidDataException("SQLite owner index disagrees with the validated legacy profile.");

            var expectedComponents=profile.parts.Select(part=>new[]{part.instanceId,part.definitionId,string.IsNullOrEmpty(part.ownerId)?null:part.ownerId,((int)part.location).ToString(CultureInfo.InvariantCulture)})
                .OrderBy(row=>row[0],StringComparer.Ordinal).ToList();
            var actualComponents=db.Query("SELECT id,definition_id,owner_id,location FROM components ORDER BY id");
            if(!RowsEqual(actualComponents,expectedComponents))throw new InvalidDataException("SQLite component index disagrees with the validated legacy profile.");

            var expectedSlots=profile.cars.SelectMany(car=>car.installed.slots.Select(slot=>new[]{car.instanceId,slot.slot,slot.partInstanceId}))
                .Concat(profile.engines.SelectMany(engine=>engine.slots.Select(slot=>new[]{engine.instanceId,slot.slot,slot.partInstanceId})))
                .OrderBy(row=>row[0],StringComparer.Ordinal).ThenBy(row=>row[1],StringComparer.Ordinal).ToList();
            var actualSlots=db.Query("SELECT owner_id,slot,part_id FROM installed_slots ORDER BY owner_id,slot");
            if(!RowsEqual(actualSlots,expectedSlots))throw new InvalidDataException("SQLite installed-slot index disagrees with the validated legacy profile.");
        }
        static void StripLegacyTypedProjection(SqliteNative db)
        {
            using(var delete=db.Prepare("DELETE FROM state_records WHERE (collection='$' AND record_key IN ('cash','revision','lastEconomyUtcTicks','economyReceipts')) OR collection='$/economyReceipts' OR collection LIKE '$/economyReceipts/%'"))delete.Execute();
            var entities=db.Query("SELECT collection,record_key,ordinal,payload FROM state_records WHERE collection IN ('$/cars','$/engines') ORDER BY collection,ordinal");
            using(var update=db.Prepare("UPDATE state_records SET payload=? WHERE collection=? AND record_key=?"))
                foreach(var row in entities)
                {
                    string stripped=RemoveObjectField(row[3],"fluids",row[0]=="$/cars");
                    if(stripped!=row[3])update.Execute(stripped,row[0],row[1]);
                }
        }
        static string RemoveObjectField(string json,string field,bool required)
        {
            if(json==null||json.Length<2||json[0]!='{'||json[json.Length-1]!='}')throw new InvalidDataException("Expected an object payload while stripping typed fields.");
            int cursor=1,previousComma=-1;bool found=false;
            while(cursor<json.Length-1)
            {
                while(cursor<json.Length-1&&(char.IsWhiteSpace(json[cursor])||json[cursor]==',')){if(json[cursor]==',')previousComma=cursor;cursor++;}
                if(cursor>=json.Length-1)break;
                int memberStart=cursor;
                int keyEnd=ScanString(json,cursor);string key=DecodeJsonString(json.Substring(cursor,keyEnd-cursor));
                cursor=SkipWhitespace(json,keyEnd);if(cursor>=json.Length||json[cursor++]!=':')throw new InvalidDataException("Malformed JSON object in legacy migration.");
                cursor=SkipWhitespace(json,cursor);int valueStart=cursor;int valueEnd=ScanValue(json,cursor);
                if(string.Equals(key,field,StringComparison.Ordinal))
                {
                    if(found)throw new InvalidDataException("Duplicate typed field in legacy JSON payload.");
                    found=true;
                    int removeStart,removeEnd;
                    int after=SkipWhitespace(json,valueEnd);
                    if(after<json.Length&&json[after]==','){removeStart=memberStart;removeEnd=after+1;}
                    else if(previousComma>=0){removeStart=previousComma;removeEnd=valueEnd;}
                    else{removeStart=memberStart;removeEnd=valueEnd;}
                    string changed=json.Remove(removeStart,removeEnd-removeStart);
                    if(required&&!found)throw new InvalidDataException("Missing migrated typed field.");
                    return changed;
                }
                cursor=valueEnd;int next=SkipWhitespace(json,cursor);
                if(next<json.Length&&json[next]==','){previousComma=next;cursor=next+1;}else cursor=next;
            }
            if(required&&!found)throw new InvalidDataException("Missing migrated typed field: "+field);
            return json;
        }
        static int SkipWhitespace(string value,int index){while(index<value.Length&&char.IsWhiteSpace(value[index]))index++;return index;}
        static int ScanString(string value,int index)
        {
            if(index>=value.Length||value[index]!='"')throw new InvalidDataException("Malformed JSON string in legacy migration.");
            for(int i=index+1;i<value.Length;i++)if(value[i]=='\\')i++;else if(value[i]=='"')return i+1;
            throw new InvalidDataException("Unterminated JSON string in legacy migration.");
        }
        static int ScanValue(string value,int index)
        {
            if(index>=value.Length)throw new InvalidDataException("Missing JSON value in legacy migration.");
            if(value[index]=='"')return ScanString(value,index);
            if(value[index]=='{'||value[index]=='[')
            {
                char opening=value[index],closing=opening=='{'?'}':']';int depth=0;bool quoted=false;
                for(int i=index;i<value.Length;i++)
                {
                    char c=value[i];if(quoted){if(c=='\\')i++;else if(c=='"')quoted=false;continue;}
                    if(c=='"'){quoted=true;continue;}if(c==opening)depth++;else if(c==closing&&--depth==0)return i+1;
                }
                throw new InvalidDataException("Unbalanced JSON value in legacy migration.");
            }
            int end=index;while(end<value.Length&&value[end]!=','&&value[end]!=']'&&value[end]!='}'&&!char.IsWhiteSpace(value[end]))end++;
            return end;
        }
        static string DecodeJsonString(string token)
        {
            if(token.Length<2||token[0]!='"'||token[token.Length-1]!='"')throw new InvalidDataException("Invalid JSON property name.");
            var output=new StringBuilder();
            for(int i=1;i<token.Length-1;i++)
            {
                char c=token[i];if(c!='\\'){output.Append(c);continue;}
                if(++i>=token.Length-1)throw new InvalidDataException("Invalid escaped JSON property name.");
                switch(token[i]){case '"':output.Append('"');break;case '\\':output.Append('\\');break;case '/':output.Append('/');break;case 'b':output.Append('\b');break;case 'f':output.Append('\f');break;case 'n':output.Append('\n');break;case 'r':output.Append('\r');break;case 't':output.Append('\t');break;case 'u':if(i+4>=token.Length)throw new InvalidDataException("Invalid Unicode escape.");output.Append((char)int.Parse(token.Substring(i+1,4),NumberStyles.HexNumber,CultureInfo.InvariantCulture));i+=4;break;default:throw new InvalidDataException("Invalid JSON escape.");}
            }
            return output.ToString();
        }
        internal static SqliteStateLoadResult ReadState(string path,Func<string,PlayerProfile> validateLegacyMigration=null,Func<SqliteStateLoadResult,PlayerProfile> materializeLoaded=null)
        {
            Prepare();lock(gate)
            {
                PlayerProfile migratedProfile;
                using(var db=Open(path,false,validateLegacyMigration,out migratedProfile))
                {
                    db.Exec("BEGIN");
                    try
                    {
                        if(int.TryParse(Meta(db,"profile_contract"),out int contract)&&contract>MechanicsOwnership.SaveVersion)
                            throw new NotSupportedException("Newer logical save contract; writes blocked.");
                        if(Meta(db,"digest_algorithm")!="xor-sha256-records-v2")
                            throw new NotSupportedException("Unsupported state integrity format.");
                        if(db.Scalar("PRAGMA quick_check")!="ok")throw new InvalidDataException("SQLite integrity check failed.");
                        if(db.Query("PRAGMA foreign_key_check").Count!=0)throw new InvalidDataException("SQLite ownership references are invalid.");
                        long generation=long.Parse(Meta(db,"reset_generation")??"0",CultureInfo.InvariantCulture);
                        long commit=long.Parse(Meta(db,"commit_id")??"0",CultureInfo.InvariantCulture);
                        if(generation<0||commit<0)throw new InvalidDataException("Saved commit or reset generation is negative.");
                        var legacyRows=Rows(db);
                        var typedRows=ReadAllTypedRecords(db);
                        VerifyV3Integrity(db,legacyRows.Values);
                        string digest=Meta(db,"records_digest");
                        if(digest==null||digest!=AggregateV2(legacyRows.Values,typedRows))throw new InvalidDataException("Database record digest mismatch.");
                        var typedData=SqliteTypedProfileData.FromStateRecords(typedRows);
                        if(typedData.Economy==null)
                        {
                            if(!EmptyTypedBootstrap(db,legacyRows.Count,commit,generation))
                                throw new InvalidDataException("Database has no typed economy row outside the empty bootstrap state.");
                        }
                        else if(Meta(db,"ledger_revision")!=typedData.Economy.Revision.ToString(CultureInfo.InvariantCulture))
                            throw new InvalidDataException("Typed economy revision disagrees with the saved ledger fence.");
                        // The only rootless read allowed is the fully verified empty bootstrap:
                        // no legacy rows, no typed economy, and EmptyTypedBootstrap already passed.
                        // This supplies an empty JSON object to initialization without creating a player.
                        string legacyJson=legacyRows.Count==0&&typedData.Economy==null
                            ?"{}":StateRows.Restore(legacyRows.Values);
                        var result=new SqliteStateLoadResult(legacyJson,typedData,commit,generation,migratedProfile);
                        if(materializeLoaded!=null)
                        {
                            var profile=materializeLoaded(result);
                            if(profile==null)throw new InvalidDataException("Typed profile materializer returned no profile.");
                            VerifyProfileRelations(db,profile);
                            result.LoadedProfile=profile;
                        }
                        db.Exec("COMMIT");
                        ResolveResetIntent(path,generation,commit);
                        if(generation<ResetFloor(path))throw new NotSupportedException("Recovery database predates the committed reset.");
                        cachedPath=path;cachedRows=legacyRows;cachedCommit=commit;
                        return result;
                    }
                    catch{try{db.Exec("ROLLBACK");}catch{}throw;}
                }
            }
        }
        static long[] ResetIntent(string path){string file=Primary(path)+".reset-intent";return File.Exists(file)?File.ReadAllText(file,Encoding.UTF8).Split('|').Select(v=>long.Parse(v,CultureInfo.InvariantCulture)).ToArray():null;}
        static long ResetFloor(string path){string marker=Primary(path)+".reset-generation";long floor=File.Exists(marker)?long.Parse(File.ReadAllText(marker,Encoding.UTF8),CultureInfo.InvariantCulture):0;var intent=ResetIntent(path);return intent==null?floor:Math.Max(floor,intent[0]);}
        static void ResolveResetIntent(string path,long generation,long commit){if(path!=Primary(path))return;var intent=ResetIntent(path);if(intent==null)return;if(intent.Length!=2)throw new InvalidDataException("Invalid reset recovery intent.");if(generation==intent[0]-1&&commit==intent[1]){File.Delete(path+".reset-intent");return;}if(generation>=intent[0])try{AtomicText(path+".reset-generation",generation.ToString(CultureInfo.InvariantCulture));File.Delete(path+".reset-intent");}catch(IOException){ /* Keep the intent as the recovery floor until cleanup succeeds. */ }}
        static string Primary(string path)=>path.EndsWith(".bak",StringComparison.Ordinal)?path.Substring(0,path.Length-4):path;
        internal static bool CanInitialize(string path)
        {
            try
            {
                if(File.Exists(path)&&new FileInfo(path).Length==0)return true;
                if(!IsDatabase(path))return false;
                lock(gate)
                {
                    using(var db=new SqliteNative(path,true))
                    {
                        int version=int.Parse(db.Scalar("PRAGMA user_version"),CultureInfo.InvariantCulture);
                        if(version==0&&db.Scalar("SELECT name FROM sqlite_master WHERE type='table' LIMIT 1")==null)return true;
                        if(db.Scalar("PRAGMA application_id")!=ApplicationId.ToString(CultureInfo.InvariantCulture)||version>SchemaVersion)return false;
                        long commit=long.Parse(Meta(db,"commit_id")??"0",CultureInfo.InvariantCulture);
                        long generation=long.Parse(Meta(db,"reset_generation")??"0",CultureInfo.InvariantCulture);
                        if(commit!=0||generation!=0||db.Scalar("SELECT COUNT(*) FROM state_records")!="0")return false;
                        if(version>=3)
                        {
                            if(!EmptyTypedBootstrap(db,0,commit,generation)||Meta(db,"digest_algorithm")!="xor-sha256-records-v2"||
                                Meta(db,"records_digest")!=AggregateV2(Enumerable.Empty<StateRows.Row>(),ReadAllTypedRecords(db)))return false;
                        }
                        return true;
                    }
                }
            }
            catch{return false;}
        }
        internal static void Migrate(string path){Prepare();lock(gate){using(var db=Open(path)){} }}
        internal static string ExportJson(string path)
        {
            string preferencePath=Path.Combine(Path.GetDirectoryName(path),"driving-settings.json");
            var state=ReadState(path,null,result=>
            {
                if(result.ValidatedMigrationProfile!=null)return result.ValidatedMigrationProfile;
                var profile=JsonUtility.FromJson<PlayerProfile>(DrivingPreferences.AddToJson(result.LegacyJson,preferencePath));
                if(profile==null)throw new InvalidDataException("Unable to decode profile for external JSON export.");
                result.TypedData.ApplyTo(profile);return profile;
            });
            return JsonUtility.ToJson(state.LoadedProfile);
        }
        internal static void WriteLegacy(string path,Snapshot snapshot,bool reset=false)=>Write(path,snapshot,reset);
        internal static void Write(string path,Snapshot snapshot,bool reset=false)
        {
            long gateStart=Stamp();
            lock(gate)
            {
                snapshot.trace?.Elapsed("writer gate wait",gateStart);
                long timing=Stamp();
                if(snapshot.rows==null||snapshot.typedData==null)throw new InvalidOperationException("Legacy save transport was not completed on its owner.");
                SaveMeasured(snapshot,"record projection",ref timing);
                snapshot.trace?.Value("projected state records",snapshot.rows.Count);
                if(snapshot.trace!=null)
                    snapshot.trace.Value("market chunk records",snapshot.rows.Count(row=>StateRows.MarketChunkPath(row.path)));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var db=Open(path))
                {
                    SaveMeasured(snapshot,"open + schema checks",ref timing);
                    db.Exec("BEGIN IMMEDIATE");
                    long oldCommit=long.Parse(Meta(db,"commit_id")??"0",CultureInfo.InvariantCulture);
                    if(oldCommit<0)throw new InvalidDataException("Saved commit fence is negative.");
                    long nextCommit=checked(oldCommit+1);
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
                        generation=checked(generation+1);
                        AtomicText(path+".reset-intent",generation.ToString(CultureInfo.InvariantCulture)+"|"+oldCommit.ToString(CultureInfo.InvariantCulture));
                        FailureInjection?.Invoke("after-reset-intent");
                    }
                    int changed=0,removed=0,inserted=0,updated=0,unchanged=0,marketChanged=0;
                    try
                    {
                        long rowAndOwnershipStart=Stamp();
                        using(var remove=db.Prepare("DELETE FROM state_records WHERE collection=? AND record_key=?"))
                            foreach(var old in before.Values.Where(r=>!after.ContainsKey(r.Id)))
                            {remove.Execute(old.path,old.key);changed++;removed++;if(StateRows.MarketChunkPath(old.path))marketChanged++;}
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
                                if(StateRows.MarketChunkPath(row.path))marketChanged++;
                            }
                        SaveMeasured(snapshot,"state row delta SQL",ref timing);
                        snapshot.trace?.Value("changed state records",changed);
                        snapshot.trace?.Value("inserted state records",inserted);
                        snapshot.trace?.Value("updated state records",updated);
                        snapshot.trace?.Value("removed state records",removed);
                        snapshot.trace?.Value("unchanged state records",unchanged);
                        snapshot.trace?.Value("changed market chunk records",marketChanged);
                        Sync(db,"owners",new[]{"id","kind","definition_id"},snapshot.owners,1);
                        Sync(db,"components",new[]{"id","definition_id","owner_id","location"},snapshot.components,1);
                        Sync(db,"installed_slots",new[]{"owner_id","slot","part_id"},snapshot.slots,2);
                        if(snapshot.typedData.Economy.Revision!=snapshot.revision)
                            throw new InvalidOperationException("Typed economy and legacy transaction revision disagree.");
                        if(reset)db.Exec("DELETE FROM operation_receipts");
                        var typedRows=SyncTypedProfileState(db,snapshot.typedData);
                        SaveMeasured(snapshot,"ownership delta SQL",ref timing);
                        // Preserve the pre-existing aggregate measurement label
                        // for SQLite domain verifier and historical comparisons.
                        if(Measurement!=null)Measurement("row and ownership delta SQL",
                            (Stamp()-rowAndOwnershipStart)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                        RebuildIntegrity(db,snapshot.rows,typedRows);
                        SetMeta(db,"digest_algorithm","xor-sha256-records-v2");
                        SetMeta(db,"records_digest",AggregateV2(snapshot.rows,typedRows));
                        SaveMeasured(snapshot,"integrity digest",ref timing);
                        SetMeta(db,"commit_id",nextCommit.ToString(CultureInfo.InvariantCulture));
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
                    InvalidateRowsAfterTypedCommit(path,nextCommit);
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
