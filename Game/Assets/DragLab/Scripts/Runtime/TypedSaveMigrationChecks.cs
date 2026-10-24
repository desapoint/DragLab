#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DragLab.Domain;
using DragLab.Data;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Populated v1/v2 fixtures and exact v3 migration/load/failure checks.</summary>
    public static class TypedSaveMigrationChecks
    {
        const string StaleWalPayload = "{\"runId\":\"wal-stale-v1\",\"tick\":1}";
        static readonly string[] MigrationHooks =
        {
            "migration:3", "migration:3:source-verified", "migration:3:backfilled",
            "migration:3:legacy-stripped", "migration:3:integrity-ready",
            "migration:3:validated", "migration:3:before-commit"
        };

        static void Check(Action<bool,string> check, bool condition, string label) => check(condition, label);

        static string MigrationSql(int version, out string name)
        {
            var asset = Resources.LoadAll<TextAsset>("Persistence/Migrations")
                .Single(value => int.Parse(value.name.Substring(0, 3), CultureInfo.InvariantCulture) == version);
            name = asset.name;
            return asset.text;
        }

        static string MigrationChecksum(string sql)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(sql)));
        }

        static string ChecksumWithLineEndings(string sql, bool crlf)
        {
            string lf = sql.Replace("\r\n", "\n").Replace("\r", "\n");
            return MigrationChecksum(crlf ? lf.Replace("\n", "\r\n") : lf);
        }

        static string OppositeLineEndingChecksum(int version)
        {
            string name, sql = MigrationSql(version, out name);
            bool sourceIsCrlf = sql.IndexOf("\r\n", StringComparison.Ordinal) >= 0;
            return ChecksumWithLineEndings(sql, !sourceIsCrlf);
        }

        static void SetRecordedChecksum(string path, int version, string checksum)
        {
            using (var db = new SqliteNative(path))
                db.Exec("UPDATE schema_migrations SET checksum='" + checksum.Replace("'", "''") + "' WHERE version=" + version.ToString(CultureInfo.InvariantCulture));
        }

        static string HistoryFingerprint(string path, int throughVersion)
        {
            using (var db = new SqliteNative(path, true))
                return string.Join("|", db.Query("SELECT version,name,checksum,applied_utc FROM schema_migrations WHERE version<=" + throughVersion.ToString(CultureInfo.InvariantCulture) + " ORDER BY version")
                    .Select(row => string.Join(";", row)));
        }

        static void ApplySourceSchema(SqliteNative db, int version)
        {
            for (int n = 1; n <= version; n++)
            {
                string name, sql = MigrationSql(n, out name);
                db.Exec(sql);
                using (var statement = db.Prepare("INSERT INTO schema_migrations(version,name,checksum,applied_utc) VALUES(?,?,?,?)"))
                    statement.Execute(n, name, MigrationChecksum(sql), "2026-10-23T00:00:00.0000000Z");
            }
        }

        static void CreateEmptySchemaFixture(string path, int schemaVersion)
        {
            if (schemaVersion != 1 && schemaVersion != 2) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
            using (var db = new SqliteNative(path))
            {
                ApplySourceSchema(db, schemaVersion);
                db.Exec("PRAGMA application_id=1145848130; PRAGMA user_version=" + schemaVersion.ToString(CultureInfo.InvariantCulture));
            }
        }

        static byte[] RowDigest(StateRows.Row row)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(row.Id + "\n" + row.order.ToString(CultureInfo.InvariantCulture) + "\n" + row.json));
        }

        static string AggregateDigest(IEnumerable<StateRows.Row> rows)
        {
            var sum = new byte[32];
            foreach (var row in rows)
            {
                var digest = RowDigest(row);
                for (int i = 0; i < sum.Length; i++) sum[i] ^= digest[i];
            }
            return Convert.ToBase64String(sum);
        }

        static string AggregateTypedDigest(IEnumerable<StateRows.Row> legacy, IEnumerable<TypedStateRecord> typed)
        {
            var sum = new byte[32];
            foreach (var row in legacy)
            {
                var digest = RowDigest(row);
                for (int i = 0; i < sum.Length; i++) sum[i] ^= digest[i];
            }
            foreach (var row in typed)
            {
                var digest = row.HashBytes();
                for (int i = 0; i < sum.Length; i++) sum[i] ^= digest[i];
            }
            return string.Concat(sum.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        static void AdjustV3FluidFixture(string path, IEnumerable<TypedStateRecord> sourceTyped,
            string ownerKind, string ownerId, string changedField, long changedBits, bool deleteRow)
        {
            var records = sourceTyped.ToList();
            var index = records.FindIndex(row => row.TableName == TypedFluidStateRecord.Table &&
                row.ReadText("owner_kind") == ownerKind && row.ReadText("owner_id") == ownerId);
            if (index < 0) throw new InvalidDataException("Fixture typed fluid row was not found.");
            var oldRow = records[index];
            var fields = oldRow.Fields();
            if (deleteRow) records.RemoveAt(index);
            else
            {
                bool found = false;
                for (int i = 0; i < fields.Length; i++)
                    if (fields[i].Name == changedField)
                    {
                        fields[i] = TypedStateField.Int64(changedField, changedBits);
                        found = true;
                    }
                if (!found) throw new InvalidDataException("Fixture fluid field was not found: " + changedField);
                var replacement = new TypedStateRecord(oldRow.TableName, oldRow.RecordKey, fields);
                records[index] = replacement;
            }
            var legacy = new List<StateRows.Row>();
            using (var db = new SqliteNative(path))
            {
                foreach (var row in db.Query("SELECT collection,record_key,ordinal,payload FROM state_records ORDER BY collection,record_key"))
                    legacy.Add(new StateRows.Row { path = row[0], key = row[1], order = int.Parse(row[2], CultureInfo.InvariantCulture), json = row[3] });
                using (var update = db.Prepare(deleteRow
                    ? "DELETE FROM fluid_states WHERE owner_kind=? AND owner_id=?"
                    : "UPDATE fluid_states SET " + changedField + "=? WHERE owner_kind=? AND owner_id=?"))
                {
                    if (deleteRow) update.Execute(ownerKind, ownerId);
                    else update.Execute(changedBits, ownerKind, ownerId);
                }
                if (deleteRow)
                {
                    using (var remove = db.Prepare("DELETE FROM record_integrity WHERE family='fluid_states' AND record_key=?"))
                        remove.Execute(oldRow.RecordKey);
                }
                else
                {
                    var replacement = records[index];
                    using (var update = db.Prepare("UPDATE record_integrity SET row_hash_hex=? WHERE family='fluid_states' AND record_key=?"))
                        update.Execute(replacement.HashHex(), replacement.RecordKey);
                }
                using (var meta = db.Prepare("UPDATE state_meta SET value=? WHERE name='records_digest'"))
                    meta.Execute(AggregateTypedDigest(legacy, records));
            }
        }

        static string FileHash(string path)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }

        static string SidecarFingerprint(string path)
        {
            return string.Join("|", new[] { "-wal", "-shm", "-journal" }.Select(suffix =>
            {
                string sidecar = path + suffix;
                return suffix + ":" + (File.Exists(sidecar) ? FileHash(sidecar) : "absent");
            }));
        }

        static byte[] ReadSharedBytes(string path)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var output = new MemoryStream())
            {
                input.CopyTo(output);
                return output.ToArray();
            }
        }

        static string InstallCommittedWalArchiveFixture(string path)
        {
            byte[] mainBytes, walBytes;
            using (var db = new SqliteNative(path))
            {
                db.Exec("PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0");
                using (var update = db.Prepare("UPDATE pending_checkpoints SET payload=? WHERE run_id=?"))
                    update.Execute(StaleWalPayload, "s05a-source-checkpoint");
                db.Exec("UPDATE state_meta SET value='wal-stale-v1-build' WHERE name='game_build'");
                mainBytes = ReadSharedBytes(path);
                walBytes = ReadSharedBytes(path + "-wal");
            }
            if (walBytes.Length < 32) throw new InvalidDataException("Stale archive fixture did not retain a committed SQLite WAL.");
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
            File.WriteAllBytes(path, mainBytes);
            File.WriteAllBytes(path + "-wal", walBytes);
            using (var archived = new SqliteNative(path, true))
                if (archived.Scalar("SELECT value FROM state_meta WHERE name='game_build'") != "wal-stale-v1-build" ||
                    archived.Scalar("SELECT payload FROM pending_checkpoints WHERE run_id='s05a-source-checkpoint'") != StaleWalPayload)
                    throw new InvalidDataException("Stale archive fixture WAL does not expose its committed metadata/checkpoint changes.");
            return FileHash(path + "-wal");
        }

        static void InsertRows(SqliteNative db, IEnumerable<StateRows.Row> rows)
        {
            using (var statement = db.Prepare("INSERT INTO state_records(collection,record_key,ordinal,payload) VALUES(?,?,?,?)"))
                foreach (var row in rows) statement.Execute(row.path, row.key, row.order, row.json);
        }

        static void SetMeta(SqliteNative db, string name, string value)
        {
            using (var statement = db.Prepare("INSERT INTO state_meta(name,value) VALUES(?,?)"))
                statement.Execute(name, value);
        }

        // This is a database fixture boundary: JsonUtility supplies the historical v1/v2
        // row payloads, while expected typed values are retained independently as IEEE bits.
        internal static void CreateLegacySchemaFixture(string path, PlayerProfile profile, int schemaVersion)
            => CreateLegacySchemaFixture(path, JsonUtility.ToJson(profile), profile, schemaVersion);

        static void CreateLegacySchemaFixture(string path, string json, PlayerProfile profile, int schemaVersion)
        {
            if (schemaVersion != 1 && schemaVersion != 2) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
            var rows = StateRows.CaptureJson(json);
            using (var db = new SqliteNative(path))
            {
                // Build the historical fixture atomically. Production durability
                // settings stay FULL; this avoids a durable flush per fixture row.
                db.Exec("BEGIN IMMEDIATE");
                try
                {
                ApplySourceSchema(db, 1);
                InsertRows(db, rows);
                SetMeta(db, "commit_id", "7");
                SetMeta(db, "ledger_revision", profile.revision.ToString(CultureInfo.InvariantCulture));
                SetMeta(db, "reset_generation", "0");
                SetMeta(db, "game_build", "S05a fixture");
                SetMeta(db, "created_build", "S05a fixture");
                SetMeta(db, "content_revision", MechanicalCatalog.Version.ToString(CultureInfo.InvariantCulture));
                SetMeta(db, "physics_identity", MechanicsSchema.Model);
                SetMeta(db, "profile_contract", profile.saveVersion.ToString(CultureInfo.InvariantCulture));
                SetMeta(db, "digest_algorithm", "xor-sha256-rows-v1");
                SetMeta(db, "records_digest", AggregateDigest(rows));
                if (schemaVersion == 2)
                {
                    string name, sql = MigrationSql(2, out name);
                    db.Exec(sql);
                    using (var statement = db.Prepare("INSERT INTO schema_migrations(version,name,checksum,applied_utc) VALUES(?,?,?,?)"))
                        statement.Execute(2, name, MigrationChecksum(sql), "2026-10-23T00:00:00.0000000Z");
                }
                using (var checkpoint = db.Prepare("INSERT INTO pending_checkpoints(run_id,payload) VALUES(?,?)"))
                    checkpoint.Execute("s05a-source-checkpoint", "{\"runId\":\"s05a-source-checkpoint\",\"tick\":17}");
                db.Exec("PRAGMA application_id=1145848130; PRAGMA user_version=" + schemaVersion.ToString(CultureInfo.InvariantCulture));
                db.Exec("COMMIT");
                }
                catch { db.Exec("ROLLBACK"); throw; }
            }
        }

        static string Fingerprint(string path)
        {
            using (var db = new SqliteNative(path, true))
            {
                int version = int.Parse(db.Scalar("PRAGMA user_version"), CultureInfo.InvariantCulture);
                var parts = new List<string>
                {
                    version.ToString(CultureInfo.InvariantCulture),
                    db.Scalar("PRAGMA application_id"),
                    string.Join("|", db.Query("SELECT version,name,checksum FROM schema_migrations ORDER BY version").Select(r => string.Join(";", r))),
                    string.Join("|", db.Query("SELECT collection,record_key,ordinal,payload FROM state_records ORDER BY collection,record_key").Select(r => string.Join(";", r))),
                    string.Join("|", db.Query("SELECT run_id,payload FROM pending_checkpoints ORDER BY run_id").Select(r => string.Join(";", r))),
                    string.Join("|", db.Query("SELECT name,value FROM state_meta ORDER BY name").Select(r => string.Join(";", r)))
                };
                if (version >= 2)
                {
                    parts.Add(string.Join("|", db.Query("SELECT id,kind,definition_id FROM owners ORDER BY id").Select(r => string.Join(";", r))));
                    parts.Add(string.Join("|", db.Query("SELECT id,definition_id,owner_id,location FROM components ORDER BY id").Select(r => string.Join(";", r))));
                    parts.Add(string.Join("|", db.Query("SELECT owner_id,slot,part_id FROM installed_slots ORDER BY owner_id,slot").Select(r => string.Join(";", r))));
                }
                return string.Join("\n", parts);
            }
        }

        static string RemainingRowsFingerprint(string path)
        {
            using (var db = new SqliteNative(path, true))
                return string.Join("|", db.Query("SELECT collection,record_key,ordinal,payload FROM state_records ORDER BY collection,record_key")
                    .Select(row => string.Join(";", row)));
        }

        static string PendingFingerprint(string path)
        {
            using (var db = new SqliteNative(path, true))
                return string.Join("|", db.Query("SELECT run_id,payload FROM pending_checkpoints ORDER BY run_id")
                    .Select(row => string.Join(";", row)));
        }

        static long[] FluidBits(FluidState value) => new[]
        {
            BitConverter.DoubleToInt64Bits(value.oilLitres), BitConverter.DoubleToInt64Bits(value.coolantLitres),
            BitConverter.DoubleToInt64Bits(value.oilTemperatureC), BitConverter.DoubleToInt64Bits(value.oilPressureBar),
            BitConverter.DoubleToInt64Bits(value.coolantPressureBar), BitConverter.DoubleToInt64Bits(value.oilCondition),
            BitConverter.DoubleToInt64Bits(value.oilConsumedLitres), BitConverter.DoubleToInt64Bits(value.oilLeakedLitres),
            BitConverter.DoubleToInt64Bits(value.coolantLostLitres), BitConverter.DoubleToInt64Bits(value.serviceElapsedSeconds),
            BitConverter.DoubleToInt64Bits(value.oilBurnLps), BitConverter.DoubleToInt64Bits(value.oilLeakLps),
            BitConverter.DoubleToInt64Bits(value.coolantLeakLps), BitConverter.DoubleToInt64Bits(value.coolantVentLps)
        };

        static bool SameBits(FluidState left, FluidState right) => FluidBits(left).SequenceEqual(FluidBits(right));

        static FluidState ExactTypedFluidFixture()
        {
            double adjacent = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(80d) + 1);
            return new FluidState
            {
                oilLitres = 0.8125, coolantLitres = -0.0, oilTemperatureC = adjacent,
                oilPressureBar = 2.125, coolantPressureBar = 1.375, oilCondition = 0.734375,
                oilConsumedLitres = 0.0625, oilLeakedLitres = 0.03125, coolantLostLitres = 0.015625,
                serviceElapsedSeconds = 123.125, oilBurnLps = 0.001, oilLeakLps = 0.002,
                coolantLeakLps = 0.003, coolantVentLps = -0.0
            };
        }

        static bool SameReceipts(IList<EconomyReceipt> expected, SqliteEconomyReceipt[] actual)
        {
            if (expected.Count != actual.Length) return false;
            for (int i = 0; i < actual.Length; i++)
            {
                var left = expected[i]; var right = actual[i];
                if (left.id != right.Id || left.operation != right.Operation || left.target != right.Target ||
                    left.revision != right.Revision || left.utcTicks != right.UtcTicks ||
                    left.cashBefore != right.CashBefore || left.cashAfter != right.CashAfter ||
                    BitConverter.DoubleToInt64Bits(left.fuelAdded) != right.FuelAddedBits ||
                    BitConverter.DoubleToInt64Bits(left.oilAdded) != right.OilAddedBits ||
                    BitConverter.DoubleToInt64Bits(left.coolantAdded) != right.CoolantAddedBits ||
                    !left.disposed.SequenceEqual(right.DisposedIds()) || !left.retained.SequenceEqual(right.RetainedIds()) ||
                    right.ReceiptOrder != i) return false;
            }
            return true;
        }

        static bool SameReceipts(IList<SqliteEconomyReceipt> expected, SqliteEconomyReceipt[] actual)
        {
            if (expected.Count != actual.Length) return false;
            for (int i = 0; i < actual.Length; i++)
            {
                var left = expected[i]; var right = actual[i];
                if (left.Id != right.Id || left.Operation != right.Operation || left.Target != right.Target ||
                    left.ReceiptOrder != right.ReceiptOrder || left.Revision != right.Revision || left.UtcTicks != right.UtcTicks ||
                    left.CashBefore != right.CashBefore || left.CashAfter != right.CashAfter ||
                    left.FuelAddedBits != right.FuelAddedBits || left.OilAddedBits != right.OilAddedBits ||
                    left.CoolantAddedBits != right.CoolantAddedBits ||
                    !left.DisposedIds().SequenceEqual(right.DisposedIds()) ||
                    !left.RetainedIds().SequenceEqual(right.RetainedIds())) return false;
            }
            return true;
        }

        static PlayerProfile Materialize(SqliteStateLoadResult state, MechanicalCatalog catalog)
        {
            var profile = JsonUtility.FromJson<PlayerProfile>(DrivingPreferences.AddToJson(state.LegacyJson));
            if (profile == null) throw new InvalidDataException("S05a remaining legacy projection could not be materialized.");
            profile.catalog = catalog;
            profile.storageCommit = state.Commit;
            state.TypedData.ApplyTo(profile);
            LegacyProfileDecoder.ValidateLoadedV12(profile, catalog);
            return profile;
        }

        static PlayerProfile Fixture(VehicleDefinition vehicle, MechanicalCatalog catalog)
        {
            var profile = PlayerProfile.New(DateTime.UtcNow.Ticks);
            profile.SelectedCar.vehicleId = profile.selectedVehicleId = vehicle.spec.id;
            profile.catalog = catalog;
            MechanicsOwnership.Ensure(profile, catalog);
            profile.cash = 987654;
            profile.revision = 41;
            profile.lastEconomyUtcTicks = DateTime.UtcNow.Ticks;
            profile.SelectedCar.fluids = new FluidState
            {
                oilLitres = 0.8125, coolantLitres = 1.3125, oilTemperatureC = 80d,
                oilPressureBar = 2.125, coolantPressureBar = 1.375, oilCondition = 0.734375,
                oilConsumedLitres = 0.0625, oilLeakedLitres = 0.03125, coolantLostLitres = 0.015625,
                serviceElapsedSeconds = 123.125, oilBurnLps = 0.001, oilLeakLps = 0.002,
                coolantLeakLps = 0.003, coolantVentLps = 0.004
            };
            long receiptStart = profile.lastEconomyUtcTicks - 1000;
            profile.lastEconomyUtcTicks = receiptStart + 500;
            profile.economyReceipts = new List<EconomyReceipt>(256);
            for (int i = 0; i < 256; i++)
                profile.economyReceipts.Add(new EconomyReceipt
                {
                    id = "s05a-history-" + i.ToString("D3", CultureInfo.InvariantCulture), operation = "fixture",
                    target = "garage", revision = profile.revision,
                    utcTicks = receiptStart + i + 1, cashBefore = profile.cash, cashAfter = profile.cash,
                    fuelAdded = 0.125, oilAdded = 0.25, coolantAdded = 0.5,
                    disposed = i == 5 ? new[] { "disposed-a", "disposed-b" } : new string[0], retained = new string[0]
                });
            PaidEconomy.Record(profile, "s05a-history-overflow", "fixture", "garage", profile.cash, receiptStart + 257);
            profile.revision++;
            if (MechanicsOwnership.Validate(profile, catalog) != null)
                throw new InvalidDataException("S05a controlled source profile is invalid.");
            return profile;
        }

        public static void Run(string folder, VehicleDefinition[] vehicles, Action<bool,string> check)
        {
            if (vehicles == null || vehicles.Length == 0) throw new ArgumentException("Vehicle fixtures are required.", nameof(vehicles));
            var catalog = new MechanicalCatalog(vehicles.Select(value => value.spec));
            var vehicle = vehicles[0];
            string sourceDirectory = Path.Combine(folder, "typed-migration");
            Directory.CreateDirectory(sourceDirectory);
            string v2 = Path.Combine(sourceDirectory, "populated-v2.sqlite3");
            var original = Fixture(vehicle, catalog);
            // The migration oracle is the exact profile produced by the supported
            // legacy decoder for the persisted v2 JSON payload, not pre-encoding memory.
            string sourceJson = JsonUtility.ToJson(original);
            original = LegacyProfileDecoder.DecodeAndValidate(sourceJson, catalog);
            long[] expectedFluid = FluidBits(original.SelectedCar.fluids);
            CreateLegacySchemaFixture(v2, sourceJson, original, 2);
            string sourceFingerprint = Fingerprint(v2);
            string sourceHash;
            using (var sha = SHA256.Create()) sourceHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(v2))).Replace("-", "").ToLowerInvariant();
            Check(check, sourceFingerprint.Contains("2\n1145848130"), "S05a fixture is an actual populated v2 schema with immutable 001/002 history and a pending checkpoint");
            Check(check, sourceHash.Length == 64, "S05a fixture records raw source SHA-256 before conversion: " + sourceHash);
            using (var db = new SqliteNative(v2, true))
            {
                string expectedDigest = AggregateDigest(StateRows.CaptureJson(sourceJson));
                Check(check, db.Scalar("SELECT value FROM state_meta WHERE name='records_digest'") == expectedDigest &&
                    db.Scalar("SELECT value FROM state_meta WHERE name='digest_algorithm'") == "xor-sha256-rows-v1",
                    "v2 source aggregate is independently recomputed and verified before migration begins");
            }

            string emptyV2 = Path.Combine(sourceDirectory, "empty-v2-bootstrap.sqlite3");
            CreateEmptySchemaFixture(emptyV2, 2);
            string emptyV2History = HistoryFingerprint(emptyV2, 2);
            var emptyV2State = SqliteStateStore.ReadState(emptyV2);
            using (var db = new SqliteNative(emptyV2, true))
                Check(check, emptyV2State.TypedData.Economy == null && emptyV2State.TypedData.Fluids.Length == 0 &&
                    emptyV2State.TypedData.Receipts.Length == 0 && db.Scalar("PRAGMA user_version") == "3" &&
                    db.Query("SELECT version,name,checksum FROM schema_migrations ORDER BY version").Count == 3 &&
                    db.Scalar("SELECT value FROM state_meta WHERE name='records_digest'") != null &&
                    HistoryFingerprint(emptyV2, 2) == emptyV2History,
                    "empty v2 bootstrap migrates to a valid empty v3 state while retaining immutable 001/002 history");

            string unknownBootstrapMeta = Path.Combine(sourceDirectory, "unknown-empty-bootstrap-meta.sqlite3");
            CreateEmptySchemaFixture(unknownBootstrapMeta, 2);
            using (var db = new SqliteNative(unknownBootstrapMeta)) SetMeta(db, "unrecognized_partial_init", "present");
            string unknownBootstrapFingerprint = Fingerprint(unknownBootstrapMeta);
            string unknownBootstrapBytes = FileHash(unknownBootstrapMeta);
            string unknownBootstrapSidecars = SidecarFingerprint(unknownBootstrapMeta);
            bool unknownBootstrapRejected = false;
            try { SqliteStateStore.ReadState(unknownBootstrapMeta); }
            catch (InvalidDataException) { unknownBootstrapRejected = true; }
            Check(check, unknownBootstrapRejected && Fingerprint(unknownBootstrapMeta) == unknownBootstrapFingerprint &&
                FileHash(unknownBootstrapMeta) == unknownBootstrapBytes && SidecarFingerprint(unknownBootstrapMeta) == unknownBootstrapSidecars &&
                !File.Exists(unknownBootstrapMeta + ".schema-2.bak"),
                "unknown metadata in an otherwise empty v2 envelope rejects before bootstrap or backup mutation");

            string portableV1 = Path.Combine(sourceDirectory, "portable-history-v1.sqlite3");
            CreateLegacySchemaFixture(portableV1, sourceJson, original, 1);
            SetRecordedChecksum(portableV1, 1, OppositeLineEndingChecksum(1));
            string portableV1Fingerprint = Fingerprint(portableV1);
            string portableV1History = HistoryFingerprint(portableV1, 1);
            var portableV1Result = SqliteStateStore.ReadState(portableV1, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog));
            Check(check, portableV1Result.TypedData.Economy.Cash == original.cash &&
                Fingerprint(portableV1 + ".schema-1.bak") == portableV1Fingerprint &&
                HistoryFingerprint(portableV1, 1) == portableV1History,
                "v1 migration accepts the exact opposite-line-ending 001 checksum and preserves its original history stamp");

            string badV1Digest = Path.Combine(sourceDirectory, "bad-v1-source-digest.sqlite3");
            CreateLegacySchemaFixture(badV1Digest, sourceJson, original, 1);
            using (var db = new SqliteNative(badV1Digest))
                db.Exec("UPDATE state_records SET payload='" + (original.cash + 1).ToString(CultureInfo.InvariantCulture) + "' WHERE collection='$' AND record_key='cash'");
            string badV1Fingerprint = Fingerprint(badV1Digest);
            bool badV1DigestRejected = false;
            try { SqliteStateStore.ReadState(badV1Digest, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog)); }
            catch (InvalidDataException) { badV1DigestRejected = true; }
            bool badV1StillAtSource;
            using (var db = new SqliteNative(badV1Digest, true))
                badV1StillAtSource = db.Scalar("PRAGMA user_version") == "1" &&
                    db.Scalar("SELECT COUNT(*) FROM schema_migrations") == "1" &&
                    db.Scalar("SELECT name FROM sqlite_master WHERE type='table' AND name='owners'") == null;
            Check(check, badV1DigestRejected && badV1StillAtSource && Fingerprint(badV1Digest) == badV1Fingerprint,
                "valid-shaped v1 payload tamper fails its original aggregate digest before schema advance or source mutation");

            string missingV1Digest = Path.Combine(sourceDirectory, "missing-v1-source-digest.sqlite3");
            CreateLegacySchemaFixture(missingV1Digest, sourceJson, original, 1);
            using (var db = new SqliteNative(missingV1Digest))
                db.Exec("DELETE FROM state_meta WHERE name IN ('digest_algorithm','records_digest')");
            string missingV1Fingerprint = Fingerprint(missingV1Digest);
            bool missingV1DigestRejected = false;
            try { SqliteStateStore.ReadState(missingV1Digest, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog)); }
            catch (InvalidDataException) { missingV1DigestRejected = true; }
            Check(check, missingV1DigestRejected && Fingerprint(missingV1Digest) == missingV1Fingerprint,
                "populated v1 source with missing integrity metadata rejects rather than receiving a synthesized digest during migration");

            string portableV2 = Path.Combine(sourceDirectory, "portable-history-v2.sqlite3");
            CreateLegacySchemaFixture(portableV2, sourceJson, original, 2);
            SetRecordedChecksum(portableV2, 1, OppositeLineEndingChecksum(1));
            SetRecordedChecksum(portableV2, 2, OppositeLineEndingChecksum(2));
            string portableV2Fingerprint = Fingerprint(portableV2);
            string portableV2History = HistoryFingerprint(portableV2, 2);
            var portableV2Result = SqliteStateStore.ReadState(portableV2, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog));
            Check(check, portableV2Result.TypedData.Economy.Cash == original.cash &&
                Fingerprint(portableV2 + ".schema-2.bak") == portableV2Fingerprint &&
                HistoryFingerprint(portableV2, 2) == portableV2History,
                "v2 source accepts exact opposite-line-ending 001/002 hashes and preserves both checksum and applied-time stamps");

            string tamperedHistory = Path.Combine(sourceDirectory, "tampered-history-checksum.sqlite3");
            CreateLegacySchemaFixture(tamperedHistory, sourceJson, original, 2);
            string validChecksum = MigrationChecksum(MigrationSql(2, out _));
            char replacement = validChecksum[0] == 'A' ? 'B' : 'A';
            SetRecordedChecksum(tamperedHistory, 2, replacement + validChecksum.Substring(1));
            string tamperedFingerprint = Fingerprint(tamperedHistory);
            bool tamperedRejected = false;
            try { SqliteStateStore.ReadState(tamperedHistory, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog)); }
            catch (NotSupportedException) { tamperedRejected = true; }
            Check(check, tamperedRejected && Fingerprint(tamperedHistory) == tamperedFingerprint &&
                !File.Exists(tamperedHistory + ".schema-2.bak"),
                "a one-character migration checksum tamper is rejected before backup or migration changes the source");

            var oldPath = ProfileStore.VerificationPath;
            var oldValidatedSaveMeasurement = ProfileStore.ValidatedSaveMeasurement;
            try
            {
                ProfileStore.VerificationPath = v2;
                SqliteStateStore.Invalidate();
                SqliteStateLoadResult migrated = SqliteStateStore.ReadState(v2, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog));
                var loaded = Materialize(migrated, catalog);
                Check(check, migrated.TypedData.Economy.Cash == original.cash &&
                    migrated.TypedData.Economy.Revision == original.revision &&
                    migrated.TypedData.Economy.LastEconomyUtcTicks == original.lastEconomyUtcTicks,
                    "v2 migration returns typed cash, ledger revision and economy clock exactly");
                Check(check, FluidBits(loaded.SelectedCar.fluids).SequenceEqual(expectedFluid) &&
                    expectedFluid.Length == 14,
                    "v2 typed load preserves all 14 car-fluid bit patterns produced by the supported legacy decoder");
                var typedReceipts = migrated.TypedData.Receipts;
                Check(check, SameReceipts(original.economyReceipts, typedReceipts) && typedReceipts.Length == 256 &&
                    typedReceipts[0].Id == "s05a-history-001" && typedReceipts[4].DisposedIds()[0] == "disposed-a" &&
                    typedReceipts[4].DisposedIds()[1] == "disposed-b" && typedReceipts[4].RetainedIds().Length == 0 &&
                    typedReceipts[4].ItemStateRecords().Length == 2 && typedReceipts[255].Id == "s05a-history-overflow",
                    "legacy-compatible receipts preserve all fields/order and 256-entry cap evicts only the oldest history row");
                Check(check, !migrated.TypedData.Fluids.Any(fluid => fluid.OwnerKind == "engine"),
                    "verified attached-engine placeholder does not become a second typed fluid owner");
                using (var db = new SqliteNative(v2, true))
                    Check(check, db.Scalar("PRAGMA user_version") == "3" &&
                        db.Query("SELECT version,name,checksum FROM schema_migrations ORDER BY version").Count == 3 &&
                        db.Scalar("SELECT COUNT(*) FROM record_integrity") != null,
                        "populated v2 migration publishes v3 history and initialized typed integrity");
                Check(check, File.Exists(v2 + ".schema-2.bak") &&
                    Fingerprint(v2 + ".schema-2.bak") == sourceFingerprint,
                    "schema-2 backup preserves the exact pre-migration history and row payloads");

                string semanticInvalid = Path.Combine(sourceDirectory, "v3-nonfinite-fluid-rehashed.sqlite3");
                using (var healthy = new SqliteNative(v2, true)) healthy.Backup(semanticInvalid);
                using (var db = new SqliteNative(semanticInvalid)) db.Exec("PRAGMA journal_mode=DELETE");
                var healthyRecords = migrated.TypedData.StateRecords();
                long nanBits = BitConverter.DoubleToInt64Bits(double.NaN);
                AdjustV3FluidFixture(semanticInvalid, healthyRecords, "car", loaded.SelectedCar.instanceId,
                    "oil_litres_bits", nanBits, false);
                string nonfiniteFingerprint = Fingerprint(semanticInvalid);
                string nonfiniteBytes = FileHash(semanticInvalid);
                string nonfiniteSidecars = SidecarFingerprint(semanticInvalid);
                bool nonfiniteRejected = false;
                try { SqliteStateStore.ReadState(semanticInvalid, null, state => Materialize(state, catalog)); }
                catch (InvalidDataException) { nonfiniteRejected = true; }
                catch (ArgumentException error) when (error.ParamName == "bits") { nonfiniteRejected = true; }
                Check(check, nonfiniteRejected && Fingerprint(semanticInvalid) == nonfiniteFingerprint &&
                    FileHash(semanticInvalid) == nonfiniteBytes && SidecarFingerprint(semanticInvalid) == nonfiniteSidecars,
                    "v3 nonfinite fluid bits reject after valid row and aggregate hashes are recomputed, preserving raw source and sidecars");

                string missingTypedFluid = Path.Combine(sourceDirectory, "v3-missing-fluid-rehashed.sqlite3");
                using (var healthy = new SqliteNative(v2, true)) healthy.Backup(missingTypedFluid);
                using (var db = new SqliteNative(missingTypedFluid)) db.Exec("PRAGMA journal_mode=DELETE");
                AdjustV3FluidFixture(missingTypedFluid, healthyRecords, "car", loaded.SelectedCar.instanceId,
                    null, 0, true);
                string missingTypedFingerprint = Fingerprint(missingTypedFluid);
                string missingTypedBytes = FileHash(missingTypedFluid);
                string missingTypedSidecars = SidecarFingerprint(missingTypedFluid);
                bool missingTypedRejected = false;
                try { SqliteStateStore.ReadState(missingTypedFluid, null, state => Materialize(state, catalog)); }
                catch (InvalidDataException) { missingTypedRejected = true; }
                Check(check, missingTypedRejected && Fingerprint(missingTypedFluid) == missingTypedFingerprint &&
                    FileHash(missingTypedFluid) == missingTypedBytes && SidecarFingerprint(missingTypedFluid) == missingTypedSidecars,
                    "v3 missing authoritative fluid rejects after its integrity row and aggregate are recomputed, preserving raw source and sidecars");
                using (var db = new SqliteNative(v2, true))
                    Check(check, db.Scalar("SELECT payload FROM pending_checkpoints WHERE run_id='s05a-source-checkpoint'") == "{\"runId\":\"s05a-source-checkpoint\",\"tick\":17}",
                        "typed migration preserves the separate pending-checkpoint journal payload byte-for-byte");
                var cold = SqliteStateStore.ReadState(v2, null, state => Materialize(state, catalog));
                var coldProfile = cold.LoadedProfile;
                Check(check, SameBits(coldProfile.SelectedCar.fluids, loaded.SelectedCar.fluids) &&
                    coldProfile.cash == loaded.cash && SameReceipts(coldProfile.economyReceipts, cold.TypedData.Receipts),
                    "v3 cold reload independently restores typed authority without JSON value re-encoding");
                var normalLoad = ProfileStore.Load();
                var normalReceipts = normalLoad.economyReceipts.Select((receipt,index) => SqliteEconomyReceipt.Capture(receipt,index)).ToArray();
                Check(check, FluidBits(normalLoad.SelectedCar.fluids).SequenceEqual(expectedFluid) &&
                    normalLoad.cash == original.cash && SameReceipts(original.economyReceipts, normalReceipts),
                    "normal ProfileStore cold load applies typed values before full v12 validation");

                loaded.catalog = catalog;
                loaded.storageCommit = migrated.Commit;
                loaded.SelectedCar.fluids = ExactTypedFluidFixture();
                var capturedNullBoundaryReceipt = loaded.economyReceipts[4];
                capturedNullBoundaryReceipt.target = null;
                capturedNullBoundaryReceipt.disposed = new string[] { null, "" };
                capturedNullBoundaryReceipt.retained = new string[0];
                int capturedCash = loaded.cash;
                var capturedFluidBits = FluidBits(loaded.SelectedCar.fluids);
                var capturedPartHealth = loaded.parts[0].condition.health;
                var capturedFirstReceipt = loaded.economyReceipts[0].id;
                const int capturedBoundaryReceiptIndex = 4;
                ProfileStore.ValidatedSaveMeasurement = (phase, milliseconds) => { };
                var pendingSave = ProfileStore.BeginValidatedSave(loaded);
                loaded.cash += 50;
                loaded.SelectedCar.fluids.oilLitres += 1.25;
                loaded.parts[0].condition.health -= .125f;
                loaded.economyReceipts[0].id = "mutated-after-owner-capture";
                capturedNullBoundaryReceipt.target = "mutated-after-owner-capture";
                capturedNullBoundaryReceipt.disposed = new[] { "mutated-item" };
                var capturedResult = pendingSave.GetAwaiter().GetResult();
                var afterCaptureMutation = SqliteStateStore.ReadState(v2, null, state => Materialize(state, catalog));
                var capturedReceiptRecords = afterCaptureMutation.TypedData.Receipts;
                Check(check, capturedResult.commit == afterCaptureMutation.Commit &&
                    afterCaptureMutation.TypedData.Economy.Cash == capturedCash &&
                    capturedFluidBits[1] == long.MinValue &&
                    capturedFluidBits[2] == BitConverter.DoubleToInt64Bits(BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(80d) + 1)) &&
                    FluidBits(afterCaptureMutation.LoadedProfile.SelectedCar.fluids).SequenceEqual(capturedFluidBits) &&
                    afterCaptureMutation.LoadedProfile.parts[0].condition.health == capturedPartHealth &&
                    afterCaptureMutation.TypedData.Receipts[0].Id == capturedFirstReceipt &&
                    afterCaptureMutation.TypedData.Receipts[capturedBoundaryReceiptIndex].Target == null &&
                    afterCaptureMutation.TypedData.Receipts[capturedBoundaryReceiptIndex].DisposedIds().Length == 2 &&
                    afterCaptureMutation.TypedData.Receipts[capturedBoundaryReceiptIndex].DisposedIds()[0] == null &&
                    afterCaptureMutation.TypedData.Receipts[capturedBoundaryReceiptIndex].DisposedIds()[1] == "" &&
                    afterCaptureMutation.TypedData.Receipts[capturedBoundaryReceiptIndex].RetainedIds().Length == 0 &&
                    loaded.cash == capturedCash + 50 && loaded.economyReceipts[0].id == "mutated-after-owner-capture",
                    "v3 typed writer preserves negative-zero, adjacent-double and null/empty receipt fields from the owner capture despite later source mutation");
                var ownerCaptureTiming = capturedResult.trace == null ? default(KeyValuePair<string,double>) :
                    capturedResult.trace.stages.SingleOrDefault(stage => stage.Key == "owner complete capture");
                var workerTiming = capturedResult.trace == null ? default(KeyValuePair<string,double>) :
                    capturedResult.trace.stages.SingleOrDefault(stage => stage.Key == "entire save worker");
                Check(check, ownerCaptureTiming.Key != null && workerTiming.Key != null &&
                    capturedResult.trace.stages.Any(stage => stage.Key == "worker dispatch wait") &&
                    ownerCaptureTiming.Value >= 0 && workerTiming.Value >= 0 &&
                    !double.IsNaN(ownerCaptureTiming.Value) && !double.IsInfinity(ownerCaptureTiming.Value) &&
                    !double.IsNaN(workerTiming.Value) && !double.IsInfinity(workerTiming.Value),
                    string.Format(CultureInfo.InvariantCulture,
                        "validated-save measured owner capture {0:F2} ms and save worker {1:F2} ms (diagnostic only; no threshold)",
                        ownerCaptureTiming.Value, workerTiming.Value));
                SqliteStateStore.Invalidate();
                var exactNormalLoad = ProfileStore.Load();
                Check(check, FluidBits(exactNormalLoad.SelectedCar.fluids).SequenceEqual(capturedFluidBits) &&
                    exactNormalLoad.economyReceipts[capturedBoundaryReceiptIndex].target == null &&
                    exactNormalLoad.economyReceipts[capturedBoundaryReceiptIndex].disposed.Length == 2 &&
                    exactNormalLoad.economyReceipts[capturedBoundaryReceiptIndex].disposed[0] == null &&
                    exactNormalLoad.economyReceipts[capturedBoundaryReceiptIndex].disposed[1] == "" &&
                    exactNormalLoad.economyReceipts[capturedBoundaryReceiptIndex].retained.Length == 0,
                    "normal ProfileStore cold load restores typed IEEE bits and null/empty item boundaries without JSON re-encoding");
                string unrelatedBefore = RemainingRowsFingerprint(v2);
                // The successful legacy save retires its superseded checkpoint
                // journal. A later rejected save must preserve that committed state.
                string pendingBeforeStale = PendingFingerprint(v2);
                var staleCandidate = coldProfile;
                staleCandidate.storageCommit = migrated.Commit;
                staleCandidate.revision = loaded.revision;
                staleCandidate.cash = loaded.cash + 50;
                Check(check, !ProfileStore.TrySave(staleCandidate), "stale legacy candidate is rejected by exact storage-commit CAS at equal ledger revision");
                var afterStale = SqliteStateStore.ReadState(v2, null, state => Materialize(state, catalog));
                var afterStaleProfile = afterStale.LoadedProfile;
                string unrelatedAfter = RemainingRowsFingerprint(v2);
                Check(check, afterStale.TypedData.Economy.Cash == capturedCash && afterStaleProfile.cash == capturedCash &&
                    FluidBits(afterStaleProfile.SelectedCar.fluids).SequenceEqual(capturedFluidBits) &&
                    SameReceipts(capturedReceiptRecords, afterStale.TypedData.Receipts) &&
                    unrelatedBefore == unrelatedAfter && pendingBeforeStale == PendingFingerprint(v2),
                    "typed legacy write preserves fluids, bounded receipts, unrelated rows and pending checkpoints after stale CAS failure");
            }
            finally
            {
                ProfileStore.ValidatedSaveMeasurement = oldValidatedSaveMeasurement;
                ProfileStore.VerificationPath = oldPath;
                SqliteStateStore.Invalidate();
            }

            foreach (var hook in MigrationHooks)
            {
                string path = Path.Combine(sourceDirectory, hook.Replace(':', '-') + ".sqlite3");
                CreateLegacySchemaFixture(path, sourceJson, original, 2);
                string before = Fingerprint(path);
                bool injected = false;
                SqliteStateStore.Invalidate();
                SqliteStateStore.FailureInjection = phase =>
                {
                    if (phase != hook) return;
                    injected = true;
                    throw new IOException("S05a migration fault: " + hook);
                };
                bool failed = false;
                try { SqliteStateStore.ReadState(path, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog)); }
                catch (IOException) { failed = true; }
                finally { SqliteStateStore.FailureInjection = null; SqliteStateStore.Invalidate(); }
                Check(check, injected && failed && Fingerprint(path) == before,
                    hook + " rolls back every schema/data change to the pristine v2 fingerprint");
                Check(check, File.Exists(path + ".schema-2.bak") && Fingerprint(path + ".schema-2.bak") == before,
                    hook + " leaves an independently readable source-version backup");
                var retry = SqliteStateStore.ReadState(path, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog));
                Check(check, retry.Commit == 7 && retry.TypedData.Economy.Cash == original.cash && SameReceipts(original.economyReceipts, retry.TypedData.Receipts),
                    hook + " retry succeeds from the preserved v2 source");
            }

            string badDigest = Path.Combine(sourceDirectory, "bad-source-digest.sqlite3");
            CreateLegacySchemaFixture(badDigest, sourceJson, original, 2);
            string sourceJournalMode;
            using (var db = new SqliteNative(badDigest))
            {
                db.Exec("UPDATE state_meta SET value='invalid-digest' WHERE name='records_digest'");
                sourceJournalMode = db.Scalar("PRAGMA journal_mode");
            }
            string badDigestBefore = Fingerprint(badDigest);
            string badDigestBytesBefore = FileHash(badDigest);
            string badDigestSidecarsBefore = SidecarFingerprint(badDigest);
            bool badDigestRejected = false;
            try { SqliteStateStore.ReadState(badDigest, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog)); }
            catch (InvalidDataException) { badDigestRejected = true; }
            Check(check, badDigestRejected && sourceJournalMode == "delete" &&
                Fingerprint(badDigest) == badDigestBefore && FileHash(badDigest) == badDigestBytesBefore &&
                SidecarFingerprint(badDigest) == badDigestSidecarsBefore && !File.Exists(badDigest + ".schema-2.bak"),
                "bad v2 digest rejects before WAL/backup and preserves DELETE-journal source bytes and sidecars exactly");

            string badLedgerFence = Path.Combine(sourceDirectory, "bad-v2-ledger-fence.sqlite3");
            CreateLegacySchemaFixture(badLedgerFence, sourceJson, original, 2);
            using (var db = new SqliteNative(badLedgerFence))
                db.Exec("UPDATE state_meta SET value='0' WHERE name='ledger_revision'");
            string badLedgerFingerprint = Fingerprint(badLedgerFence);
            string badLedgerBytes = FileHash(badLedgerFence);
            string badLedgerSidecars = SidecarFingerprint(badLedgerFence);
            bool badLedgerRejected = false;
            try { SqliteStateStore.ReadState(badLedgerFence, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog)); }
            catch (InvalidDataException) { badLedgerRejected = true; }
            Check(check, badLedgerRejected && Fingerprint(badLedgerFence) == badLedgerFingerprint &&
                FileHash(badLedgerFence) == badLedgerBytes && SidecarFingerprint(badLedgerFence) == badLedgerSidecars &&
                (!File.Exists(badLedgerFence + ".schema-2.bak") || Fingerprint(badLedgerFence + ".schema-2.bak") == badLedgerFingerprint),
                "mismatched v2 ledger revision fence rejects before migration and preserves source bytes/sidecars");

            string missingLedgerFence = Path.Combine(sourceDirectory, "missing-v2-ledger-fence.sqlite3");
            CreateLegacySchemaFixture(missingLedgerFence, sourceJson, original, 2);
            using (var db = new SqliteNative(missingLedgerFence))
                db.Exec("DELETE FROM state_meta WHERE name='ledger_revision'");
            string missingLedgerFingerprint = Fingerprint(missingLedgerFence);
            string missingLedgerBytes = FileHash(missingLedgerFence);
            string missingLedgerSidecars = SidecarFingerprint(missingLedgerFence);
            bool missingLedgerRejected = false;
            try { SqliteStateStore.ReadState(missingLedgerFence, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog)); }
            catch (InvalidDataException) { missingLedgerRejected = true; }
            Check(check, missingLedgerRejected && Fingerprint(missingLedgerFence) == missingLedgerFingerprint &&
                FileHash(missingLedgerFence) == missingLedgerBytes && SidecarFingerprint(missingLedgerFence) == missingLedgerSidecars &&
                (!File.Exists(missingLedgerFence + ".schema-2.bak") || Fingerprint(missingLedgerFence + ".schema-2.bak") == missingLedgerFingerprint),
                "missing populated v2 ledger revision fence rejects before migration and preserves source bytes/sidecars");

            string staleBackupSource = Path.Combine(sourceDirectory, "stale-backup-source.sqlite3");
            CreateLegacySchemaFixture(staleBackupSource, sourceJson, original, 2);
            string staleBackupTarget = Path.Combine(sourceDirectory, "stale-backup-target.sqlite3");
            CreateLegacySchemaFixture(staleBackupTarget, sourceJson, original, 2);
            string staleBackup = staleBackupTarget + ".schema-2.bak";
            using (var source = new SqliteNative(staleBackupSource, true)) source.Backup(staleBackup);
            using (var db = new SqliteNative(staleBackup)) db.Exec("UPDATE pending_checkpoints SET payload='{\"runId\":\"stale\",\"tick\":1}' WHERE run_id='s05a-source-checkpoint'; UPDATE state_meta SET value='stale-build' WHERE name='game_build'");
            string staleExpected = Fingerprint(staleBackupTarget);
            Check(check, Fingerprint(staleBackup) != staleExpected,
                "stale schema-2 backup fixture differs in checkpoint journal despite matching commit and generation");
            SqliteStateStore.ReadState(staleBackupTarget, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog));
            Check(check, Fingerprint(staleBackupTarget + ".schema-2.bak") == staleExpected,
                "migration refreshes stale schema-2 backup using pending journal contents as source identity");

            string staleV1BackupSource = Path.Combine(sourceDirectory, "stale-v1-backup-source.sqlite3");
            CreateLegacySchemaFixture(staleV1BackupSource, sourceJson, original, 1);
            string staleV1BackupTarget = Path.Combine(sourceDirectory, "stale-v1-backup-target.sqlite3");
            CreateLegacySchemaFixture(staleV1BackupTarget, sourceJson, original, 1);
            string staleV1Backup = staleV1BackupTarget + ".schema-1.bak";
            using (var source = new SqliteNative(staleV1BackupSource, true)) source.Backup(staleV1Backup);
            string staleWalHash = InstallCommittedWalArchiveFixture(staleV1Backup);
            string staleV1Expected = Fingerprint(staleV1BackupTarget);
            Check(check, Fingerprint(staleV1Backup) != staleV1Expected && File.Exists(staleV1Backup + "-wal") &&
                FileHash(staleV1Backup + "-wal") == staleWalHash,
                "stale schema-1 archive exposes committed metadata/checkpoint changes from a retained WAL despite matching commit/generation");
            SqliteStateStore.ReadState(staleV1BackupTarget, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog));
            string[] quarantinedArchive = Directory.GetFiles(sourceDirectory,
                Path.GetFileName(staleV1BackupTarget) + ".schema-1.bak.unverified-*");
            string quarantinedWal = quarantinedArchive.FirstOrDefault(file => file.EndsWith("-wal", StringComparison.Ordinal));
            Check(check, Fingerprint(staleV1BackupTarget + ".schema-1.bak") == staleV1Expected &&
                quarantinedWal != null && FileHash(quarantinedWal) == staleWalHash,
                "v1 migration refreshes the verified source backup and quarantines its stale main/WAL pair together");
            string orphanTarget = Path.Combine(sourceDirectory, "orphan-backup-sidecar.sqlite3");
            CreateLegacySchemaFixture(orphanTarget, sourceJson, original, 2);
            string orphanBefore = Fingerprint(orphanTarget);
            string orphanBackup = orphanTarget + ".schema-2.bak";
            File.WriteAllBytes(orphanBackup + "-wal", Encoding.UTF8.GetBytes("orphan-wal-marker"));
            string orphanHash = FileHash(orphanBackup + "-wal");
            SqliteStateStore.ReadState(orphanTarget, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog));
            var orphanArchives = Directory.GetFiles(sourceDirectory, Path.GetFileName(orphanBackup) + ".unverified-*-wal");
            Check(check, Fingerprint(orphanBackup) == orphanBefore && orphanArchives.Length == 1 &&
                FileHash(orphanArchives[0]) == orphanHash,
                "orphan backup WAL is quarantined before installing a verified original-source archive");
            var missingFluidProfile = Fixture(vehicle, catalog);
            RejectV2(sourceDirectory, "missing-car-fluid", missingFluidProfile, catalog, check,
                "missing authoritative car fluids reject without defaulting or healing the v2 source",
                MissingFluidSource(missingFluidProfile, "$/cars"));
            var invalidPartProfile = Fixture(vehicle, catalog);
            invalidPartProfile.parts[0].definitionId = "missing/typed-migration-part";
            RejectV2(sourceDirectory, "invalid-part", invalidPartProfile, catalog, check,
                "invalid legacy part identity rejects migration without fabricating replacement hardware");
            var futureSchema = Path.Combine(sourceDirectory, "unknown-schema.sqlite3");
            CreateLegacySchemaFixture(futureSchema, original, 2);
            using (var db = new SqliteNative(futureSchema)) db.Exec("PRAGMA user_version=999");
            string futureBefore = Fingerprint(futureSchema);
            bool futureRejected = false;
            try { SqliteStateStore.ReadState(futureSchema, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog)); }
            catch (NotSupportedException) { futureRejected = true; }
            Check(check, futureRejected && Fingerprint(futureSchema) == futureBefore,
                "unknown future schema rejects without modifying the original database");

            string mismatchedIndex = Path.Combine(sourceDirectory, "mismatched-ownership-index.sqlite3");
            CreateLegacySchemaFixture(mismatchedIndex, sourceJson, original, 2);
            using (var db = new SqliteNative(mismatchedIndex))
                db.Exec("UPDATE owners SET definition_id='wrong/authored/identity' WHERE kind='car'");
            string mismatchBefore = Fingerprint(mismatchedIndex);
            bool mismatchRejected = false;
            try { SqliteStateStore.ReadState(mismatchedIndex, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog)); }
            catch (Exception) { mismatchRejected = true; }
            Check(check, mismatchRejected && Fingerprint(mismatchedIndex) == mismatchBefore,
                "valid-FK SQL ownership index mismatch rejects rather than syncing or healing from legacy JSON");

            var detachedProfile = Fixture(vehicle, catalog);
            string detachedId = AssemblyOperations.Detach(detachedProfile, detachedProfile.SelectedCar.instanceId);
            var detachedEngine = detachedProfile.engines.Single(value => value.instanceId == detachedId);
            detachedEngine.fluids = new FluidState
            {
                oilLitres = 1.125, coolantLitres = 2.25, oilTemperatureC = 91.5,
                oilPressureBar = 2.5, coolantPressureBar = 1.5, oilCondition = 0.625,
                oilConsumedLitres = 0.375, oilLeakedLitres = 0.1875, coolantLostLitres = 0.09375,
                serviceElapsedSeconds = 44.5, oilBurnLps = 0.004, oilLeakLps = 0.005,
                coolantLeakLps = 0.006, coolantVentLps = 0.007
            };
            var detachedExpected = LegacyProfileDecoder.DecodeAndValidate(JsonUtility.ToJson(detachedProfile), catalog);
            long[] detachedBits = FluidBits(detachedExpected.engines.Single(value => value.instanceId == detachedId).fluids);
            string detachedPath = Path.Combine(sourceDirectory, "detached-engine.sqlite3");
            CreateLegacySchemaFixture(detachedPath, detachedProfile, 2);
            var detachedMigration = SqliteStateStore.ReadState(detachedPath, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog));
            var detachedLoaded = Materialize(detachedMigration, catalog);
            Check(check, FluidBits(detachedLoaded.engines.Single(value => value.instanceId == detachedId).fluids).SequenceEqual(detachedBits) &&
                detachedMigration.TypedData.Fluids.Count(fluid => fluid.OwnerKind == "engine") == 1,
                "detached engine receives one typed fluid row with all 14 source-authoritative bits");
            var missingDetachedFluid = Fixture(vehicle, catalog);
            string missingDetachedId = AssemblyOperations.Detach(missingDetachedFluid, missingDetachedFluid.SelectedCar.instanceId);
            RejectV2(sourceDirectory, "missing-detached-fluid", missingDetachedFluid, catalog, check,
                "missing detached-engine fluids reject without inventing a replacement state",
                MissingFluidSource(missingDetachedFluid, "$/engines"));

            string v1Rollback = Path.Combine(sourceDirectory, "v1-rollback.sqlite3");
            CreateLegacySchemaFixture(v1Rollback, sourceJson, original, 1);
            string v1Before = Fingerprint(v1Rollback);
            SqliteStateStore.FailureInjection = phase =>
            {
                if (phase == "migration:3:backfilled") throw new IOException("S05a v1 rollback");
            };
            bool v1Failed = false;
            try { SqliteStateStore.ReadState(v1Rollback, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog)); }
            catch (IOException) { v1Failed = true; }
            finally { SqliteStateStore.FailureInjection = null; SqliteStateStore.Invalidate(); }
            bool v1SchemaIntact;
            using (var db = new SqliteNative(v1Rollback, true))
                v1SchemaIntact = db.Scalar("SELECT name FROM sqlite_master WHERE type='table' AND name='owners'") == null;
            Check(check, v1Failed && v1SchemaIntact && Fingerprint(v1Rollback) == v1Before,
                "v1→v2→v3 phase failure rolls every pending migration back to the genuine v1 source");
            Check(check, Fingerprint(v1Rollback + ".schema-1.bak") == v1Before,
                "v1 multi-step rollback retains an exact v1 source backup");
            var v1Retry = SqliteStateStore.ReadState(v1Rollback, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog));
            Check(check, v1Retry.TypedData.Economy.Cash == original.cash,
                "genuine v1 source retries through both legacy migrations and typed v3 after rollback");

            string v1 = Path.Combine(sourceDirectory, "populated-v1.sqlite3");
            CreateLegacySchemaFixture(v1, sourceJson, original, 1);
            string populatedV1Fingerprint = Fingerprint(v1);
            var migratedV1 = SqliteStateStore.ReadState(v1, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog));
            var v1Profile = Materialize(migratedV1, catalog);
            Check(check, migratedV1.TypedData.Economy.Cash == original.cash && SameBits(v1Profile.SelectedCar.fluids, original.SelectedCar.fluids),
                "genuine populated v1 fixture applies both immutable legacy migrations before v3 typed load");
            Check(check, Fingerprint(v1 + ".schema-1.bak") == populatedV1Fingerprint,
                "v1 migration retains its exact source-version backup");

            // The legacy JSON is used only to form explicit v1/v2 database fixtures.
        }

        static string MissingFluidSource(PlayerProfile profile, string collection)
        {
            // Explicit historical import fixture: Unity encodes a null inline
            // object as a default-valued object, so place null in the old payload.
            // Detach rows from StateRows' cache before changing fixture bytes.
            var rows = StateRows.CaptureJson(JsonUtility.ToJson(profile)).Select(row =>
                new StateRows.Row { path = row.path, key = row.key, order = row.order, json = row.json }).ToList();
            var owner = rows.First(row => row.path == collection);
            var fields = StateRows.Fields(owner.json);
            if (!fields.Any(field => field.Key == "fluids")) throw new InvalidDataException("Fixture owner lacks its fluid field.");
            owner.json = "{" + string.Join(",", fields.Select(field => "\"" + field.Key + "\":" +
                (field.Key == "fluids" ? "null" : field.Value))) + "}";
            return StateRows.Restore(rows);
        }

        static void RejectV2(string folder, string name, PlayerProfile profile, MechanicalCatalog catalog,
            Action<bool,string> check, string label, string sourceJson = null)
        {
            string path = Path.Combine(folder, name + ".sqlite3");
            CreateLegacySchemaFixture(path, sourceJson ?? JsonUtility.ToJson(profile), profile, 2);
            string before = Fingerprint(path);
            string bytesBefore = FileHash(path);
            string sidecarsBefore = SidecarFingerprint(path);
            bool rejected = false;
            try { SqliteStateStore.ReadState(path, raw => LegacyProfileDecoder.DecodeAndValidate(raw, catalog)); }
            catch (Exception) { rejected = true; }
            Check(check, rejected && Fingerprint(path) == before && FileHash(path) == bytesBefore &&
                SidecarFingerprint(path) == sidecarsBefore &&
                (!File.Exists(path + ".schema-2.bak") || Fingerprint(path + ".schema-2.bak") == before), label);
        }
    }
}
#endif
