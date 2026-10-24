#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Independent managed-SQLite durability checks for the dormant S05b refill writer.</summary>
    public static class TypedFluidRefillCommitChecks
    {
        const long SeedNow = 638000000000000000L;
        static readonly OperatingPolicy Policy = new OperatingPolicy { oilPerLitre = 6.125, coolantPerLitre = 3.875, quoteSeconds = 300 };
        static VehicleDefinition[] authored;
        static MechanicalCatalog catalog;
        static int passed;

        static void Check(Action<bool, string> check, bool condition, string label)
        {
            check(condition, label);
            if (!condition) throw new InvalidOperationException("S05b refill commit check failed: " + label);
            passed++;
        }

        static PlayerProfile NewProfile()
        {
            var profile = PlayerProfile.New(SeedNow - TimeSpan.TicksPerDay);
            profile.SelectedCar.vehicleId = profile.selectedVehicleId = authored[0].spec.id;
            MechanicsOwnership.Ensure(profile, catalog);
            profile.catalog = catalog;
            profile.cash = 6000;
            var fluid = profile.SelectedCar.fluids;
            fluid.oilLitres = Math.Min(2.1, catalog.Spec(profile.SelectedCar.vehicleId).fluids.oilCapacityLitres * .55);
            fluid.coolantLitres = Math.Min(1.4, catalog.Spec(profile.SelectedCar.vehicleId).fluids.coolantCapacityLitres * .5);
            fluid.oilTemperatureC = 91.25; fluid.oilPressureBar = 3.75; fluid.coolantPressureBar = 1.25;
            fluid.oilCondition = .63; fluid.oilConsumedLitres = .12; fluid.oilLeakedLitres = .03;
            fluid.coolantLostLitres = .04; fluid.serviceElapsedSeconds = 18.5;
            fluid.oilBurnLps = .0001; fluid.oilLeakLps = .00002; fluid.coolantLeakLps = .00003; fluid.coolantVentLps = .00004;
            string invalid = MechanicsOwnership.Validate(profile, catalog);
            if (invalid != null) throw new InvalidOperationException("Invalid authored fixture: " + invalid);
            return profile;
        }

        static long QuoteExpiry(long issuedTicks) => checked(issuedTicks + TimeSpan.FromSeconds(Policy.quoteSeconds).Ticks);

        static void SeedFullReceiptWindow(PlayerProfile profile)
        {
            profile.lastEconomyUtcTicks = Math.Max(profile.lastEconomyUtcTicks, SeedNow - 1);
            var receipts = new List<EconomyReceipt>(256);
            for (int i = 0; i < 256; i++)
                receipts.Add(new EconomyReceipt
                {
                    id = i == 0 ? new string('\u03bb', 180) + "\u0001-oldest" : "s05b-fifo-" + i.ToString("D3", CultureInfo.InvariantCulture),
                    operation = "legacy fixture", target = null, revision = profile.revision,
                    utcTicks = profile.lastEconomyUtcTicks, cashBefore = profile.cash, cashAfter = profile.cash,
                    disposed = i == 0 ? new[] { "s05b-disposed-old" } : Array.Empty<string>(),
                    retained = i == 0 ? new[] { "s05b-retained-old" } : Array.Empty<string>()
                });
            profile.economyReceipts = receipts;
            string invalid = PaidEconomy.ValidateState(profile);
            if (invalid != null) throw new InvalidOperationException("Invalid full-window fixture: " + invalid);
        }

        static long Generation(string path)
        {
            using (var db = new SqliteNative(path, true))
                return long.Parse(db.Scalar("SELECT value FROM state_meta WHERE name='reset_generation'"), CultureInfo.InvariantCulture);
        }

        static long Commit(string path)
        {
            using (var db = new SqliteNative(path, true))
                return long.Parse(db.Scalar("SELECT value FROM state_meta WHERE name='commit_id'"), CultureInfo.InvariantCulture);
        }

        static string Meta(string path, string key)
        {
            using (var db = new SqliteNative(path, true)) return db.Scalar("SELECT value FROM state_meta WHERE name=?", key);
        }

        static TypedFluidRefillCommand BuildCommand(string path, string operation, long now, Action<PlayerProfile> edit = null,
            string slot = "slot-1", long? generation = null, string physics = null, long? acceptedTick = null)
        {
            var profile = ProfileStore.Load();
            if (edit != null) edit(profile);
            long gen = generation ?? Generation(path);
            var binding = new TypedSaveSlotBinding(path, slot, gen);
            var request = new FluidRefillRequest(new StateCommandIdentity(slot, gen, operation),
                new StateCommandVersionFence(profile.revision, profile.storageCommit, MechanicalCatalog.Version),
                profile.SelectedCar.instanceId, .375, .25, now - 10, QuoteExpiry(now - 10));
            var input = FluidRefillPlanner.Capture(profile, Policy, request, now);
            var plan = FluidRefillPlanner.Plan(input);
            string physicsIdentity = physics ?? Meta(path, "physics_identity");
            return TypedFluidRefillCommand.Create(binding, plan, physicsIdentity, acceptedTick ?? now);
        }

        static FluidRefillReadSet ReadSetCopy(FluidRefillReadSet source, string carId = null, string garageJobId = null,
            bool hasRebuild = false, bool rebuildActive = false, int? cash = null, long? lastEconomy = null,
            double? oilPrice = null, double? coolantPrice = null, FluidRefillFluidValues fluids = null)
        {
            return new FluidRefillReadSet(carId ?? source.CarId, source.VehicleId, source.EngineAssemblyId,
                garageJobId ?? source.GarageJobId, hasRebuild, rebuildActive, cash ?? source.Cash,
                lastEconomy ?? source.LastEconomyUtcTicks, source.OilCapacityLitres, source.CoolantCapacityLitres,
                oilPrice ?? source.OilPerLitre, coolantPrice ?? source.CoolantPerLitre, fluids ?? source.Fluids,
                source.HasUnsettledRun, source.OperationAlreadyRecorded, source.EconomyReceiptCount, source.OldestEconomyReceiptId);
        }

        static TypedFluidRefillCommand CommandWithReadSet(TypedSaveSlotBinding binding, TypedFluidRefillCommand source,
            FluidRefillReadSet readSet)
        {
            var plan = FluidRefillPlanner.Plan(new FluidRefillInput(source.Request, readSet, source.Plan.PlanningNowTicks));
            return TypedFluidRefillCommand.Create(binding, plan, source.PhysicsIdentity, source.AcceptedTick);
        }

        static TypedFluidRefillCommand MakeCommand(TypedSaveSlotBinding binding, FluidRefillRequest request,
            FluidRefillReadSet readSet, string physics, long acceptedTick)
        {
            var plan = FluidRefillPlanner.Plan(new FluidRefillInput(request, readSet, acceptedTick));
            if (!plan.Committable) throw new InvalidOperationException("Intent field variant must retain a valid refill plan.");
            return TypedFluidRefillCommand.Create(binding, plan, physics, acceptedTick);
        }

        static string Fingerprint(string path)
        {
            using (var db = new SqliteNative(path, true))
            {
                var result = new StringBuilder();
                foreach (string table in db.Query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name").Select(row => row[0]))
                {
                    result.Append('[').Append(table).Append(']');
                    foreach (var row in db.Query("SELECT * FROM \"" + table + "\""))
                        result.Append(string.Join("\u001f", row.Select(value => value ?? "<NULL>"))).Append('\n');
                }
                return result.ToString();
            }
        }

        static string Sidecars(string path)
        {
            return string.Join("|", new[] { "-wal", "-shm", "-journal" }.Select(suffix =>
                suffix + ":" + (File.Exists(path + suffix) ? File.ReadAllBytes(path + suffix).Length.ToString(CultureInfo.InvariantCulture) + ":" + Hash(File.ReadAllBytes(path + suffix)) : "absent")));
        }

        static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        static byte[] FromHex(string value)
        {
            if (value == null || value.Length != 64) throw new InvalidDataException("Expected a 32-byte lowercase SHA-256 digest.");
            var bytes = new byte[32];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = byte.Parse(value.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }

        static string ToHex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));

        static void AddSyntheticLegacyRows(string path, int count)
        {
            if (count <= 0) return;
            var aggregate = FromHex(Meta(path, "records_digest"));
            using (var db = new SqliteNative(path))
            {
                db.Exec("BEGIN IMMEDIATE");
                try
                {
                    using (var row = db.Prepare("INSERT INTO state_records(collection,record_key,ordinal,payload) VALUES('$',?,?,?)"))
                    using (var integrity = db.Prepare("INSERT INTO record_integrity(family,record_key,row_hash_hex) VALUES('legacy',?,?)"))
                    {
                        for (int index = 0; index < count; index++)
                        {
                            string key = "s05b-unrelated-" + index.ToString("D5", CultureInfo.InvariantCulture);
                            string payload = "0";
                            using (var sha = SHA256.Create())
                            {
                                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("$\n" + key + "\n" +
                                    index.ToString(CultureInfo.InvariantCulture) + "\n" + payload));
                                for (int i = 0; i < aggregate.Length; i++) aggregate[i] ^= hash[i];
                                row.Execute(key, index, payload);
                                integrity.Execute("$\n" + key, ToHex(hash));
                            }
                        }
                    }
                    using (var update = db.Prepare("UPDATE state_meta SET value=? WHERE name='records_digest'"))
                        update.Execute(ToHex(aggregate));
                    db.Exec("COMMIT");
                }
                catch { db.Exec("ROLLBACK"); throw; }
            }
        }

        static void WriteText(Stream stream, string value)
        {
            byte[] text = new UTF8Encoding(false, true).GetBytes(value);
            uint length = checked((uint)text.Length);
            for (int i = 0; i < 4; i++) stream.WriteByte((byte)(length >> (8 * i)));
            stream.Write(text, 0, text.Length);
        }

        static void WriteStringField(Stream stream, string name, string value)
        {
            WriteText(stream, name); stream.WriteByte(0x01); WriteText(stream, value);
        }

        static void WriteIntField(Stream stream, string name, long value)
        {
            WriteText(stream, name); stream.WriteByte(0x02);
            for (int i = 0; i < 8; i++) stream.WriteByte((byte)((ulong)value >> (8 * i)));
        }

        static void WriteIndependentTypedField(Stream stream, TypedStateField field)
        {
            WriteText(stream, field.Name);
            switch (field.Kind)
            {
                case TypedStateValueKind.Null:
                    stream.WriteByte(0); break;
                case TypedStateValueKind.Text:
                    stream.WriteByte(1); WriteText(stream, field.TextValue); break;
                case TypedStateValueKind.Int64:
                    stream.WriteByte(2);
                    ulong value = unchecked((ulong)field.Int64Value);
                    for (int index = 0; index < 8; index++) stream.WriteByte((byte)(value >> (8 * index)));
                    break;
                default: throw new InvalidDataException("Unknown test-oracle typed value kind.");
            }
        }

        static string IndependentTypedKey(TypedStateRecord row)
        {
            var fields = row.Fields();
            if (row.TableName == "profile_economy") return "1";
            if (row.TableName == "economy_receipts") return fields.Single(field => field.Name == "id").TextValue;
            string[] keyNames;
            switch (row.TableName)
            {
                case "fluid_states": keyNames = new[] { "owner_kind", "owner_id" }; break;
                case "economy_receipt_items": keyNames = new[] { "receipt_id", "item_kind", "item_order" }; break;
                case "operation_receipts":
                case "fluid_refill_results": keyNames = new[] { "save_slot", "reset_generation", "operation_id" }; break;
                default: throw new InvalidDataException("Unknown typed table in independent key oracle: " + row.TableName);
            }
            using (var stream = new MemoryStream())
            {
                foreach (string name in keyNames) WriteIndependentTypedField(stream, fields.Single(field => field.Name == name));
                return ToHex(stream.ToArray());
            }
        }

        static byte[] IndependentTypedHash(TypedStateRecord row)
        {
            using (var stream = new MemoryStream())
            {
                WriteText(stream, "draglab-record/v1");
                WriteText(stream, row.TableName);
                WriteText(stream, IndependentTypedKey(row));
                var fields = row.Fields();
                uint count = checked((uint)fields.Length);
                for (int index = 0; index < 4; index++) stream.WriteByte((byte)(count >> (8 * index)));
                foreach (var field in fields) WriteIndependentTypedField(stream, field);
                using (var sha = SHA256.Create()) return sha.ComputeHash(stream.ToArray());
            }
        }

        static bool IndependentTypedIntegrityMatches(string path, IEnumerable<TypedStateRecord> projectedRows)
        {
            using (var db = new SqliteNative(path, true))
            {
                var rows = projectedRows.Concat(ReadDurableRefillRows(db)).ToArray();
                var expectedCounts = rows.GroupBy(row => row.TableName, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
                foreach (string table in new[] { "profile_economy", "fluid_states", "economy_receipts", "economy_receipt_items",
                    "operation_receipts", "fluid_refill_results" })
                {
                    int expected = expectedCounts.TryGetValue(table, out int count) ? count : 0;
                    if (int.Parse(db.Scalar("SELECT COUNT(*) FROM " + table), CultureInfo.InvariantCulture) != expected ||
                        int.Parse(db.Scalar("SELECT COUNT(*) FROM record_integrity WHERE family=?", table), CultureInfo.InvariantCulture) != expected) return false;
                }
                foreach (var row in rows)
                {
                    string key = IndependentTypedKey(row);
                    if (!string.Equals(key, row.RecordKey, StringComparison.Ordinal)) return false;
                    string stored = db.Scalar("SELECT row_hash_hex FROM record_integrity WHERE family=? AND record_key=?", row.TableName, key);
                    if (!string.Equals(stored, ToHex(IndependentTypedHash(row)), StringComparison.Ordinal)) return false;
                }
                return true;
            }
        }

        static byte[] IndependentIntent(TypedFluidRefillCommand command)
        {
            using (var stream = new MemoryStream())
            {
                var request = command.Request;
                WriteText(stream, "fluid-refill-intent/v1");
                WriteStringField(stream, "save_slot", request.Identity.SaveSlot);
                WriteIntField(stream, "reset_generation", request.Identity.ResetGeneration);
                WriteStringField(stream, "operation_id", request.Identity.OperationId);
                WriteStringField(stream, "command_kind", "FluidTopUp");
                WriteIntField(stream, "expected_ledger_revision", request.Versions.ExpectedLedgerRevision);
                WriteIntField(stream, "expected_storage_commit", request.Versions.ExpectedStorageCommit);
                WriteIntField(stream, "content_revision", request.Versions.ExpectedContentVersion);
                WriteStringField(stream, "physics_identity", command.PhysicsIdentity);
                WriteStringField(stream, "car_id", request.CarId);
                WriteIntField(stream, "requested_oil_bits", BitConverter.DoubleToInt64Bits(request.RequestedOilLitres));
                WriteIntField(stream, "requested_coolant_bits", BitConverter.DoubleToInt64Bits(request.RequestedCoolantLitres));
                WriteIntField(stream, "quote_issue_tick", request.IssuedUtcTicks);
                WriteIntField(stream, "quote_expiry_tick", request.ExpiresUtcTicks);
                WriteIntField(stream, "accepted_confirmation_tick", command.AcceptedTick);
                return stream.ToArray();
            }
        }

        static string Aggregate(string path)
        {
            return Aggregate(path, SqliteStateStore.ReadState(path).TypedData.StateRecords());
        }

        // operation_receipts and fluid_refill_results intentionally are not projected into
        // SqliteTypedState.StateRecords(). Read their frozen SQL columns directly so this
        // independent aggregate oracle covers every durable typed table without consulting
        // record_integrity (the value under test).
        static List<TypedStateRecord> ReadDurableRefillRows(SqliteNative db)
        {
            var rows = new List<TypedStateRecord>();
            AddDurableRows(db, rows, "operation_receipts", new[] { "save_slot", "reset_generation", "operation_id",
                "command_kind", "intent_version", "intent_hex", "intent_sha256_hex", "ledger_revision_before",
                "ledger_revision_after", "storage_commit_before", "storage_commit_after", "content_revision",
                "physics_identity", "committed_utc_ticks", "domain_receipt_id" },
                new[] { 0, 2, 3, 5, 6, 12, 14 }, new[] { 0, 1, 2 });
            AddDurableRows(db, rows, "fluid_refill_results", new[] { "save_slot", "reset_generation", "operation_id",
                "car_id", "cost", "cash_after", "oil_added_bits", "coolant_added_bits", "oil_after_bits",
                "coolant_after_bits", "domain_receipt_id" }, new[] { 0, 2, 3, 10 }, new[] { 0, 1, 2 });
            return rows;
        }

        static void AddDurableRows(SqliteNative db, List<TypedStateRecord> output, string table,
            string[] columns, int[] textColumns, int[] keyColumns)
        {
            string select = "SELECT " + string.Join(",", columns) + " FROM " + table;
            foreach (var values in db.Query(select))
            {
                var fields = new TypedStateField[columns.Length];
                for (int index = 0; index < columns.Length; index++)
                {
                    if (values[index] == null) fields[index] = TypedStateField.Null(columns[index]);
                    else if (textColumns.Contains(index)) fields[index] = TypedStateField.Text(columns[index], values[index]);
                    else fields[index] = TypedStateField.Int64(columns[index], long.Parse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture));
                }
                string[] keyNames = keyColumns.Select(index => columns[index]).ToArray();
                string key = IndependentCompositeKey(fields, keyNames);
                output.Add(new TypedStateRecord(table, key, fields));
            }
        }

        static string IndependentCompositeKey(TypedStateField[] fields, string[] keyNames)
        {
            using (var stream = new MemoryStream())
            {
                foreach (string name in keyNames) WriteIndependentTypedField(stream, fields.Single(field => field.Name == name));
                return ToHex(stream.ToArray());
            }
        }

        static string Aggregate(string path, IEnumerable<TypedStateRecord> typedRows)
        {
            var aggregate = new byte[32];
            using (var db = new SqliteNative(path, true))
            {
                foreach (var row in db.Query("SELECT collection,record_key,ordinal,payload FROM state_records"))
                {
                    string key = row[0] + "\n" + row[1];
                    byte[] hash;
                    using (var sha = SHA256.Create()) hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key + "\n" + row[2] + "\n" + row[3]));
                    for (int i = 0; i < aggregate.Length; i++) aggregate[i] ^= hash[i];
                }
                foreach (var row in ReadDurableRefillRows(db))
                {
                    byte[] hash = IndependentTypedHash(row);
                    for (int i = 0; i < aggregate.Length; i++) aggregate[i] ^= hash[i];
                }
            }
            foreach (var row in typedRows)
            {
                string key = IndependentTypedKey(row);
                if (!string.Equals(key, row.RecordKey, StringComparison.Ordinal))
                    throw new InvalidDataException("Independent typed-key oracle disagrees for " + row.TableName + ".");
                byte[] hash = IndependentTypedHash(row);
                for (int i = 0; i < aggregate.Length; i++) aggregate[i] ^= hash[i];
            }
            return string.Concat(aggregate.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        static TypedStateRecord ReplaceField(TypedStateRecord source, string name, long value)
        {
            var fields = source.Fields();
            bool found = false;
            for (int index = 0; index < fields.Length; index++)
                if (fields[index].Name == name) { fields[index] = TypedStateField.Int64(name, value); found = true; }
            if (!found) throw new InvalidDataException("Missing typed fixture field: " + name);
            return new TypedStateRecord(source.TableName, source.RecordKey, fields);
        }

        static void CheckIntegrityBoundaries(string parent, TypedSaveSlotBinding sourceBinding, Action<bool, string> check)
        {
            string stalePath = Path.Combine(parent, "stale-affected-hash.sqlite3");
            ProfileStore.VerificationPath = stalePath;
            if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not create stale-hash fixture.");
            SqliteStateStore.Invalidate();
            long now = Math.Max(SeedNow + 9000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var staleBinding = new TypedSaveSlotBinding(stalePath, sourceBinding.SaveSlot, Generation(stalePath));
            var staleCommand = BuildCommand(stalePath, "s05b-stale-row-hash", now);
            using (var db = new SqliteNative(stalePath))
                using (var update = db.Prepare("UPDATE fluid_states SET oil_temperature_c_bits=oil_temperature_c_bits+1 WHERE owner_kind='car' AND owner_id=?"))
                    update.Execute(staleCommand.Request.CarId);
            string staleBefore = Fingerprint(stalePath);
            var staleOutcome = SqliteStateStore.CommitFluidRefill(staleBinding, staleCommand, now);
            Check(check, staleOutcome.Status == TypedFluidRefillStatus.Rejected && Fingerprint(stalePath) == staleBefore,
                "stale affected fluid hash rejects a changed but finite value without further mutation");

            string semanticPath = Path.Combine(parent, "rehash-nonfinite-fluid.sqlite3");
            ProfileStore.VerificationPath = semanticPath;
            if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not create semantic fixture.");
            SqliteStateStore.Invalidate();
            now = Math.Max(SeedNow + 10000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var semanticBinding = new TypedSaveSlotBinding(semanticPath, sourceBinding.SaveSlot, Generation(semanticPath));
            var semanticCommand = BuildCommand(semanticPath, "s05b-rehashed-nonfinite", now);
            var typed = SqliteStateStore.ReadState(semanticPath).TypedData.StateRecords().ToList();
            int fluidIndex = typed.FindIndex(row => row.TableName == "fluid_states" && row.ReadText("owner_kind") == "car" &&
                row.ReadText("owner_id") == semanticCommand.Request.CarId);
            if (fluidIndex < 0) throw new InvalidDataException("Semantic fixture fluid row is missing.");
            var malformed = ReplaceField(typed[fluidIndex], "oil_litres_bits", BitConverter.DoubleToInt64Bits(double.NaN));
            typed[fluidIndex] = malformed;
            string semanticAggregate = Aggregate(semanticPath, typed);
            using (var db = new SqliteNative(semanticPath))
            {
                using (var update = db.Prepare("UPDATE fluid_states SET oil_litres_bits=? WHERE owner_kind='car' AND owner_id=?"))
                    update.Execute(BitConverter.DoubleToInt64Bits(double.NaN), semanticCommand.Request.CarId);
                using (var update = db.Prepare("UPDATE record_integrity SET row_hash_hex=? WHERE family='fluid_states' AND record_key=?"))
                    update.Execute(malformed.HashHex(), malformed.RecordKey);
                using (var update = db.Prepare("UPDATE state_meta SET value=? WHERE name='records_digest'"))
                    update.Execute(semanticAggregate);
            }
            string semanticBefore = Fingerprint(semanticPath);
            var semanticOutcome = SqliteStateStore.CommitFluidRefill(semanticBinding, semanticCommand, now);
            Check(check, semanticOutcome.Status == TypedFluidRefillStatus.Rejected && Fingerprint(semanticPath) == semanticBefore,
                "rehash plus independently corrected aggregate cannot make nonfinite authoritative fluid bits semantically valid");

            string unrelatedPath = Path.Combine(parent, "unrelated-row-corruption.sqlite3");
            ProfileStore.VerificationPath = unrelatedPath;
            var unrelatedProfile = NewProfile();
            unrelatedProfile.lastEconomyUtcTicks = SeedNow - 1;
            unrelatedProfile.economyReceipts.Add(new EconomyReceipt { id = "s05b-corrupt-unrelated-receipt", operation = "fixture",
                target = null, revision = unrelatedProfile.revision, utcTicks = unrelatedProfile.lastEconomyUtcTicks,
                cashBefore = unrelatedProfile.cash, cashAfter = unrelatedProfile.cash, disposed = Array.Empty<string>(), retained = Array.Empty<string>() });
            string invalidFixture = PaidEconomy.ValidateState(unrelatedProfile);
            if (invalidFixture != null) throw new InvalidOperationException("Invalid unrelated-corruption fixture: " + invalidFixture);
            if (!ProfileStore.TrySave(unrelatedProfile)) throw new IOException("Could not create unrelated-corruption fixture: " + ProfileStore.Status);
            SqliteStateStore.Invalidate();
            now = Math.Max(SeedNow + 11000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var unrelatedBinding = new TypedSaveSlotBinding(unrelatedPath, sourceBinding.SaveSlot, Generation(unrelatedPath));
            var unrelatedCommand = BuildCommand(unrelatedPath, "s05b-unrelated-corrupt-target", now);
            using (var db = new SqliteNative(unrelatedPath))
                db.Exec("UPDATE economy_receipts SET cash_after=cash_after+1 WHERE id='s05b-corrupt-unrelated-receipt'");
            var touched = new List<string>();
            SqliteStateStore.FluidRefillQueryObserved = (stage, rows) => touched.Add(stage);
            TypedFluidRefillOutcome unaffected;
            try { unaffected = SqliteStateStore.CommitFluidRefill(unrelatedBinding, unrelatedCommand, now); }
            finally { SqliteStateStore.FluidRefillQueryObserved = null; }
            Check(check, unaffected.Status == TypedFluidRefillStatus.Committed &&
                !touched.Any(stage => stage == "affected-row:economy_receipts" || stage == "all-receipts" || stage == "full-row-scan"),
                "ordinary commit stays scoped when an unrelated receipt hash is corrupt and does not inventory receipt rows");
            bool coldRejected = false;
            try { SqliteStateStore.ReadState(unrelatedPath); }
            catch (Exception) { coldRejected = true; }
            Check(check, coldRejected, "full integrity-checked cold read later detects unrelated row corruption");
            ProfileStore.VerificationPath = Path.Combine(parent, "primary.sqlite3");
        }

        static void CheckBoundedGrowth(string parent, string seedPath, TypedSaveSlotBinding sourceBinding,
            Action<bool, string> check)
        {
            var measurements = new List<Tuple<int, int, long, long, long>>();
            foreach (int unrelated in new[] { 0, 1, 100, 10000 })
            {
                string path = Path.Combine(parent, "growth-" + unrelated.ToString(CultureInfo.InvariantCulture) + ".sqlite3");
                CopyDatabase(seedPath, path);
                AddSyntheticLegacyRows(path, unrelated);
                ProfileStore.VerificationPath = path;
                SqliteStateStore.Invalidate();
                var profile = ProfileStore.Load();
                var binding = new TypedSaveSlotBinding(path, sourceBinding.SaveSlot, Generation(path));
                long now = Math.Max(SeedNow + 20000, profile.lastEconomyUtcTicks + 10);
                var request = new FluidRefillRequest(new StateCommandIdentity(binding.SaveSlot, binding.ResetGeneration, "s05b-growth-command"),
                    new StateCommandVersionFence(profile.revision, profile.storageCommit, MechanicalCatalog.Version),
                    profile.SelectedCar.instanceId, .125, .125, now - 10, QuoteExpiry(now - 10));
                var plan = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(profile, Policy, request, now));
                var command = TypedFluidRefillCommand.Create(binding, plan, Meta(path, "physics_identity"), now);
                var queries = new List<KeyValuePair<string, int>>();
                var costs = new List<KeyValuePair<string, long>>();
                SqliteStateStore.FluidRefillQueryObserved = (stage, rows) => queries.Add(new KeyValuePair<string, int>(stage, rows));
                SqliteStateStore.FluidRefillCostObserved = (stage, bytes) => costs.Add(new KeyValuePair<string, long>(stage, bytes));
                TypedFluidRefillOutcome outcome;
                try { outcome = SqliteStateStore.CommitFluidRefill(binding, command, now); }
                finally
                {
                    SqliteStateStore.FluidRefillQueryObserved = null;
                    SqliteStateStore.FluidRefillCostObserved = null;
                }
                Check(check, outcome.Status == TypedFluidRefillStatus.Committed,
                    "bounded-growth command commits with " + unrelated.ToString(CultureInfo.InvariantCulture) + " unrelated integrity-covered SQL rows");
                long readBytes = costs.Where(item => item.Key.StartsWith("sqlite-read-returned-column-bytes:", StringComparison.Ordinal)).Sum(item => item.Value);
                long boundBytes = costs.Where(item => item.Key.StartsWith("sqlite-bound-parameter-bytes:", StringComparison.Ordinal)).Sum(item => item.Value);
                long hashBytes = costs.Where(item => item.Key == "typed-canonical-hash-input-bytes").Sum(item => item.Value);
                long residentBytes = costs.Where(item => item.Key == "resident-cache-bytes").Sum(item => item.Value);
                Check(check, queries.Count < 40 && queries.All(item => item.Key != "full-row-scan" &&
                    item.Key != "typed-row-inventory" && item.Key != "all-receipts") &&
                    residentBytes == 0 && hashBytes > 0,
                    "bounded-growth diagnostics exclude whole-row scans and show zero retained command cache at " + unrelated.ToString(CultureInfo.InvariantCulture) + " unrelated rows");
                measurements.Add(Tuple.Create(unrelated, queries.Count, readBytes, boundBytes, hashBytes));
            }
            var baseline = measurements[0];
            Check(check, measurements.All(sample => sample.Item2 == baseline.Item2 && sample.Item3 == baseline.Item3 &&
                sample.Item4 == baseline.Item4 && sample.Item5 == baseline.Item5),
                "direct command observed reads, bindings, row hashes and query count stay flat from 0 through 10,000 unrelated records");
            UnityEngine.Debug.Log("S05b bounded diagnostics: " + string.Join("; ", measurements.Select(sample =>
                "unrelated=" + sample.Item1 + ",queries=" + sample.Item2 + ",readColumnBytes=" + sample.Item3 +
                ",boundBytes=" + sample.Item4 + ",hashInputBytes=" + sample.Item5 + ",residentCacheBytes=0")) +
                "; excludes SQLite page/WAL bytes and native allocations");
            ProfileStore.VerificationPath = seedPath;
        }

        static void CheckOutcomeUnknowns(string parent, TypedSaveSlotBinding sourceBinding, Action<bool, string> check)
        {
            string missingPath = Path.Combine(parent, "missing-result.sqlite3");
            ProfileStore.VerificationPath = missingPath;
            if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not create missing-result fixture.");
            SqliteStateStore.Invalidate();
            var binding = new TypedSaveSlotBinding(missingPath, sourceBinding.SaveSlot, Generation(missingPath));
            long now = Math.Max(SeedNow + 30000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var command = BuildCommand(missingPath, "s05b-missing-result", now);
            var committed = SqliteStateStore.CommitFluidRefill(binding, command, now);
            Check(check, committed.Status == TypedFluidRefillStatus.Committed, "missing-result fault fixture first records a successful durable commit");
            long committedId = Commit(missingPath); string resultBefore = Fingerprint(missingPath);
            using (var db = new SqliteNative(missingPath))
            {
                using (var remove = db.Prepare("DELETE FROM fluid_refill_results WHERE save_slot=? AND reset_generation=? AND operation_id=?"))
                    remove.Execute(command.Request.Identity.SaveSlot, command.Request.Identity.ResetGeneration, command.Request.Identity.OperationId);
                using (var remove = db.Prepare("DELETE FROM record_integrity WHERE family='fluid_refill_results' AND record_key=?"))
                {
                    var key = IndependentCompositeKey(new[] { TypedStateField.Text("save_slot", command.Request.Identity.SaveSlot),
                        TypedStateField.Int64("reset_generation", command.Request.Identity.ResetGeneration),
                        TypedStateField.Text("operation_id", command.Request.Identity.OperationId) },
                        new[] { "save_slot", "reset_generation", "operation_id" });
                    remove.Execute(key);
                }
            }
            string corruptedAfter = Fingerprint(missingPath);
            var missingReplay = SqliteStateStore.CommitFluidRefill(binding, command, now);
            var missingReconcile = SqliteStateStore.ReconcileFluidRefill(binding, command);
            Check(check, missingReplay.Status == TypedFluidRefillStatus.OutcomeUnknown &&
                missingReconcile.Status == TypedFluidRefillStatus.OutcomeUnknown && Commit(missingPath) == committedId &&
                Fingerprint(missingPath) == corruptedAfter && resultBefore != corruptedAfter,
                "generic receipt with missing specific result remains OutcomeUnknown without charging again or mutating corruption");

            string orphanPath = Path.Combine(parent, "orphan-result.sqlite3");
            ProfileStore.VerificationPath = orphanPath;
            if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not create orphan-result fixture.");
            SqliteStateStore.Invalidate();
            var orphanBinding = new TypedSaveSlotBinding(orphanPath, sourceBinding.SaveSlot, Generation(orphanPath));
            now = Math.Max(SeedNow + 30500, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var orphanCommand = BuildCommand(orphanPath, "s05b-orphan-result", now);
            var orphanCommit = SqliteStateStore.CommitFluidRefill(orphanBinding, orphanCommand, now);
            if (orphanCommit.Status != TypedFluidRefillStatus.Committed) throw new IOException("Orphan-result fixture command did not commit.");
            committedId = Commit(orphanPath);
            string cashBeforeOrphanRetry;
            using (var db = new SqliteNative(orphanPath))
            {
                cashBeforeOrphanRetry = db.Scalar("SELECT cash FROM profile_economy WHERE singleton=1");
                // Deliberate corrupt-file fixture: suppress this connection's
                // cascade so deleting the identity truly leaves its effect row.
                db.Exec("PRAGMA foreign_keys=OFF");
                using (var remove = db.Prepare("DELETE FROM operation_receipts WHERE save_slot=? AND reset_generation=? AND operation_id=?"))
                    remove.Execute(orphanCommand.Request.Identity.SaveSlot, orphanCommand.Request.Identity.ResetGeneration,
                        orphanCommand.Request.Identity.OperationId);
                string key = IndependentCompositeKey(new[]
                {
                    TypedStateField.Text("save_slot", orphanCommand.Request.Identity.SaveSlot),
                    TypedStateField.Int64("reset_generation", orphanCommand.Request.Identity.ResetGeneration),
                    TypedStateField.Text("operation_id", orphanCommand.Request.Identity.OperationId)
                }, new[] { "save_slot", "reset_generation", "operation_id" });
                using (var remove = db.Prepare("DELETE FROM record_integrity WHERE family='operation_receipts' AND record_key=?"))
                    remove.Execute(key);
            }
            using (var db = new SqliteNative(orphanPath, true))
                Check(check, db.Scalar("SELECT COUNT(*) FROM operation_receipts") == "0" &&
                    db.Scalar("SELECT COUNT(*) FROM fluid_refill_results") == "1",
                    "orphan fixture retains its effect while only the generic identity is missing");
            string orphaned = Fingerprint(orphanPath);
            var orphanRetry = SqliteStateStore.CommitFluidRefill(orphanBinding, orphanCommand, now);
            var orphanReconcile = SqliteStateStore.ReconcileFluidRefill(orphanBinding, orphanCommand);
            string cashAfterOrphanRetry;
            using (var db = new SqliteNative(orphanPath, true)) cashAfterOrphanRetry = db.Scalar("SELECT cash FROM profile_economy WHERE singleton=1");
            Check(check, orphanRetry.Status == TypedFluidRefillStatus.OutcomeUnknown && orphanReconcile.Status == TypedFluidRefillStatus.OutcomeUnknown &&
                Commit(orphanPath) == committedId && Fingerprint(orphanPath) == orphaned && cashAfterOrphanRetry == cashBeforeOrphanRetry,
                "specific refill result without its generic identity is incoherent and remains OutcomeUnknown without extra charge or write");

            string corruptPath = Path.Combine(parent, "corrupt-result.sqlite3");
            ProfileStore.VerificationPath = corruptPath;
            if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not create corrupt-result fixture.");
            SqliteStateStore.Invalidate();
            var corruptBinding = new TypedSaveSlotBinding(corruptPath, sourceBinding.SaveSlot, Generation(corruptPath));
            now = Math.Max(SeedNow + 31000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var corruptCommand = BuildCommand(corruptPath, "s05b-corrupt-result", now);
            var corruptCommit = SqliteStateStore.CommitFluidRefill(corruptBinding, corruptCommand, now);
            Check(check, corruptCommit.Status == TypedFluidRefillStatus.Committed, "corrupt-result fixture first records a successful durable commit");
            committedId = Commit(corruptPath);
            using (var db = new SqliteNative(corruptPath))
                db.Exec("UPDATE fluid_refill_results SET cash_after=cash_after+1 WHERE save_slot='slot-1' AND operation_id='s05b-corrupt-result'");
            corruptedAfter = Fingerprint(corruptPath);
            var corruptReplay = SqliteStateStore.CommitFluidRefill(corruptBinding, corruptCommand, now);
            Check(check, corruptReplay.Status == TypedFluidRefillStatus.OutcomeUnknown &&
                Commit(corruptPath) == committedId && Fingerprint(corruptPath) == corruptedAfter,
                "corrupt result integrity after a durable operation is OutcomeUnknown with no second effect");

            string heldPath = Path.Combine(parent, "held-writer.sqlite3");
            ProfileStore.VerificationPath = heldPath;
            if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not create held-writer fixture.");
            SqliteStateStore.Invalidate();
            var heldBinding = new TypedSaveSlotBinding(heldPath, sourceBinding.SaveSlot, Generation(heldPath));
            now = Math.Max(SeedNow + 32000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var heldCommand = BuildCommand(heldPath, "s05b-held-writer", now);
            TypedFluidRefillOutcome heldAttempt, heldReconcile;
            using (var blocker = new SqliteNative(heldPath))
            {
                blocker.Exec("BEGIN IMMEDIATE");
                heldAttempt = SqliteStateStore.CommitFluidRefill(heldBinding, heldCommand, now);
                heldReconcile = SqliteStateStore.ReconcileFluidRefill(heldBinding, heldCommand);
                blocker.Exec("ROLLBACK");
            }
            Check(check, heldAttempt.Status == TypedFluidRefillStatus.OutcomeUnknown &&
                heldReconcile.Status == TypedFluidRefillStatus.OutcomeUnknown,
                "busy external writer keeps both commit and receipt-absence reconciliation OutcomeUnknown");
            var absent = SqliteStateStore.ReconcileFluidRefill(heldBinding, heldCommand);
            var retry = SqliteStateStore.CommitFluidRefill(heldBinding, heldCommand, now);
            Check(check, absent.Status == TypedFluidRefillStatus.NotCommitted && retry.Status == TypedFluidRefillStatus.Committed,
                "after lock release a fresh write-locked absence proof returns NotCommitted and the same stable intent retries exactly once");

            string unavailablePath = Path.Combine(parent, "unavailable-after-commit.sqlite3");
            ProfileStore.VerificationPath = unavailablePath;
            if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not create unavailable-save fixture.");
            SqliteStateStore.Invalidate();
            var unavailableBinding = new TypedSaveSlotBinding(unavailablePath, sourceBinding.SaveSlot, Generation(unavailablePath));
            now = Math.Max(SeedNow + 33000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var unavailableCommand = BuildCommand(unavailablePath, "s05b-unavailable-after-commit", now);
            var durable = SqliteStateStore.CommitFluidRefill(unavailableBinding, unavailableCommand, now);
            if (durable.Status != TypedFluidRefillStatus.Committed) throw new IOException("Unavailable-save fixture command did not commit.");
            ProfileStore.Flush();
            string moved = unavailablePath + ".temporarily-unavailable";
            File.Move(unavailablePath, moved);
            TypedFluidRefillOutcome unavailable;
            try { unavailable = SqliteStateStore.CommitFluidRefill(unavailableBinding, unavailableCommand, now); }
            finally { File.Move(moved, unavailablePath); }
            Check(check, unavailable.Status == TypedFluidRefillStatus.OutcomeUnknown &&
                SqliteStateStore.ReconcileFluidRefill(unavailableBinding, unavailableCommand).Status == TypedFluidRefillStatus.Replayed,
                "database unavailable after a known commit yields OutcomeUnknown and reconciles to the durable result after restoration");
        }

        static void CheckWriterPricing(string parent, TypedSaveSlotBinding sourceBinding, Action<bool, string> check)
        {
            var billTargets = new[] { .5e-9, .999999999, 1.000000001, 2.000000001 };
            for (int index = 0; index < billTargets.Length + 1; index++)
            {
                string path = Path.Combine(parent, "price-case-" + index.ToString(CultureInfo.InvariantCulture) + ".sqlite3");
                ProfileStore.VerificationPath = path;
                if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not create writer-pricing fixture.");
                SqliteStateStore.Invalidate();
                var binding = new TypedSaveSlotBinding(path, sourceBinding.SaveSlot, Generation(path));
                var profile = ProfileStore.Load();
                var car = profile.SelectedCar;
                long now = Math.Max(SeedNow + 40000 + index, profile.lastEconomyUtcTicks + 10);
                double oilRequested = index == billTargets.Length ? 100 : billTargets[index] / Policy.oilPerLitre;
                double coolantRequested = index == billTargets.Length ? .125 : 0;
                string operationId = "s05b-price-" + index.ToString("D2", CultureInfo.InvariantCulture);
                var request = new FluidRefillRequest(new StateCommandIdentity(binding.SaveSlot, binding.ResetGeneration, operationId),
                    new StateCommandVersionFence(profile.revision, profile.storageCommit, MechanicalCatalog.Version),
                    car.instanceId, oilRequested, coolantRequested, now - 10, QuoteExpiry(now - 10));
                var plan = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(profile, Policy, request, now));
                var preview = PaidEconomy.Preview(profile, catalog, Policy, ValuationPolicy.Defaults(),
                    new PaymentRequest { kind = PaymentKind.Fluids, carId = car.instanceId,
                        litres = oilRequested, coolantLitres = coolantRequested }, now);
                int expectedCost = checked((int)Math.Ceiling(preview.oil * Policy.oilPerLitre +
                    preview.coolant * Policy.coolantPerLitre - 1e-9));
                Check(check, plan.Committable && plan.Cost == preview.cost && preview.cost == expectedCost &&
                    BitConverter.DoubleToInt64Bits(plan.OilAdded) == BitConverter.DoubleToInt64Bits(preview.oil) &&
                    BitConverter.DoubleToInt64Bits(plan.CoolantAdded) == BitConverter.DoubleToInt64Bits(preview.coolant),
                    index == billTargets.Length
                        ? "capacity-clamped request has exact independent accepted-volume and cash-ceiling parity"
                        : "near-cent writer amount " + billTargets[index].ToString("R", CultureInfo.InvariantCulture) +
                            " has exact independent Money ceiling and accepted-volume parity");
                if (index < billTargets.Length)
                    Check(check, plan.Cost == expectedCost,
                        "near-cent writer cost equals the independently evaluated integer currency result for sample " + index.ToString(CultureInfo.InvariantCulture));
                else
                    Check(check, preview.oil < oilRequested && preview.coolant == coolantRequested,
                        "capacity-clamp fixture proves oil clamps while coolant remains accepted");
                var beforeBits = FluidBits(car.fluids);
                var expected = profile;
                int beforeCash = expected.cash;
                double oilAdded = ServiceOperations.TopUp(expected, car.instanceId, true, preview.oil);
                double coolantAdded = ServiceOperations.TopUp(expected, car.instanceId, false, preview.coolant);
                expected.cash = checked(beforeCash - preview.cost);
                PaidEconomy.Record(expected, operationId, PaymentKind.Fluids.ToString(), car.instanceId,
                    beforeCash, now, oil: oilAdded, coolant: coolantAdded);
                expected.revision = checked(expected.revision + 1);
                var command = TypedFluidRefillCommand.Create(binding, plan, Meta(path, "physics_identity"), now);
                var result = SqliteStateStore.CommitFluidRefill(binding, command, now);
                Check(check, result.Status == TypedFluidRefillStatus.Committed && result.Result.Cost == expectedCost &&
                    result.Result.CashAfter == expected.cash,
                    "typed writer stores independently expected money and cash for pricing case " + index.ToString(CultureInfo.InvariantCulture));
                SqliteStateStore.Invalidate();
                var cold = ProfileStore.Load();
                long[] afterBits = FluidBits(cold.SelectedCar.fluids);
                Check(check, cold.cash == expected.cash && cold.revision == expected.revision &&
                    afterBits.SequenceEqual(FluidBits(expected.SelectedCar.fluids)) &&
                    afterBits.Where((_, field) => field > 1).SequenceEqual(beforeBits.Where((_, field) => field > 1)),
                    "pricing case cold-loads all exact fluid bits without changing the twelve nonquantity values");
            }
        }

        static void CheckResetIdentityAndFloor(string parent, TypedSaveSlotBinding sourceBinding, Action<bool, string> check)
        {
            string generationPath = Path.Combine(parent, "persisted-generation-change.sqlite3");
            ProfileStore.VerificationPath = generationPath;
            if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not create generation fixture.");
            SqliteStateStore.Invalidate();
            var oldBinding = new TypedSaveSlotBinding(generationPath, sourceBinding.SaveSlot, Generation(generationPath));
            long now = Math.Max(SeedNow + 50000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var command = BuildCommand(generationPath, "s05b-generation-change", now);
            using (var db = new SqliteNative(generationPath))
                db.Exec("UPDATE state_meta SET value='1' WHERE name='reset_generation'");
            string changedGenerationBefore = Fingerprint(generationPath);
            var oldCommit = SqliteStateStore.CommitFluidRefill(oldBinding, command, now);
            var oldRead = SqliteStateStore.ReconcileFluidRefill(oldBinding, command);
            Check(check, oldCommit.Status == TypedFluidRefillStatus.Rejected && oldRead.Status == TypedFluidRefillStatus.Rejected &&
                Fingerprint(generationPath) == changedGenerationBefore,
                "persisted reset-generation change rejects old command binding before receipt lookup or mutation");
            var currentBinding = new TypedSaveSlotBinding(generationPath, sourceBinding.SaveSlot, 1);
            var absent = SqliteStateStore.ReconcileFluidRefill(currentBinding, command);
            Check(check, absent.Status == TypedFluidRefillStatus.Rejected,
                "command captured for the old owner generation cannot cross to a newly selected generation");

            string floorPath = Path.Combine(parent, "reset-floor.sqlite3");
            ProfileStore.VerificationPath = floorPath;
            if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not create reset-floor fixture.");
            SqliteStateStore.Invalidate();
            var floorBinding = new TypedSaveSlotBinding(floorPath, sourceBinding.SaveSlot, Generation(floorPath));
            now = Math.Max(SeedNow + 51000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var floorCommand = BuildCommand(floorPath, "s05b-reset-floor", now);
            File.WriteAllText(floorPath + ".reset-generation", "1", new UTF8Encoding(false));
            string floorBefore = Fingerprint(floorPath);
            var floorCommit = SqliteStateStore.CommitFluidRefill(floorBinding, floorCommand, now);
            var floorRead = SqliteStateStore.ReconcileFluidRefill(floorBinding, floorCommand);
            Check(check, floorCommit.Status == TypedFluidRefillStatus.Rejected && floorRead.Status == TypedFluidRefillStatus.Rejected &&
                Fingerprint(floorPath) == floorBefore && File.ReadAllText(floorPath + ".reset-generation", Encoding.UTF8) == "1",
                "durable reset-generation floor sidecar blocks a stale database binding and remains intact");

            string resetPath = Path.Combine(parent, "receipt-cleared-by-reset.sqlite3");
            ProfileStore.VerificationPath = resetPath;
            if (!ProfileStore.TrySave(NewProfile())) throw new IOException("Could not create reset-receipt fixture.");
            SqliteStateStore.Invalidate();
            var resetOldBinding = new TypedSaveSlotBinding(resetPath, sourceBinding.SaveSlot, Generation(resetPath));
            now = Math.Max(SeedNow + 52000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var resetCommand = BuildCommand(resetPath, "s05b-cleared-by-reset", now);
            var committed = SqliteStateStore.CommitFluidRefill(resetOldBinding, resetCommand, now);
            if (committed.Status != TypedFluidRefillStatus.Committed) throw new IOException("Reset fixture command did not commit.");
            var resetSource = ProfileStore.Load();
            if (!ProfileStore.TryReset(resetSource, out var resetProfile)) throw new IOException("Supported profile reset failed: " + ProfileStore.Status);
            using (var db = new SqliteNative(resetPath, true))
                Check(check, Generation(resetPath) == resetOldBinding.ResetGeneration + 1 &&
                    db.Scalar("SELECT COUNT(*) FROM operation_receipts") == "0" &&
                    db.Scalar("SELECT COUNT(*) FROM fluid_refill_results") == "0" &&
                    db.Scalar("SELECT COUNT(*) FROM economy_receipts") == "0",
                    "confirmed reset advances generation and transactionally clears generic, refill-result and business receipts");
            var resetReplay = SqliteStateStore.ReconcileFluidRefill(resetOldBinding, resetCommand);
            var resetCommitAttempt = SqliteStateStore.CommitFluidRefill(resetOldBinding, resetCommand, now);
            Check(check, resetReplay.Status == TypedFluidRefillStatus.Rejected && resetCommitAttempt.Status == TypedFluidRefillStatus.Rejected,
                "old-generation command cannot replay after the supported reset cleared its durable receipt");
            ProfileStore.VerificationPath = Path.Combine(parent, "primary.sqlite3");
        }

        static long[] FluidBits(FluidState fluid) => new[] { fluid.oilLitres, fluid.coolantLitres, fluid.oilTemperatureC,
            fluid.oilPressureBar, fluid.coolantPressureBar, fluid.oilCondition, fluid.oilConsumedLitres, fluid.oilLeakedLitres,
            fluid.coolantLostLitres, fluid.serviceElapsedSeconds, fluid.oilBurnLps, fluid.oilLeakLps, fluid.coolantLeakLps,
            fluid.coolantVentLps }.Select(BitConverter.DoubleToInt64Bits).ToArray();

        static void CheckSuccessfulCommit(string path, TypedSaveSlotBinding binding, Action<bool, string> check)
        {
            var source = ProfileStore.Load();
            var car = source.SelectedCar;
            var beforeBits = FluidBits(car.fluids);
            int beforeCash = source.cash; long beforeRevision = source.revision, beforeClock = source.lastEconomyUtcTicks;
            long beforeCommit = source.storageCommit;
            long now = Math.Max(SeedNow, beforeClock + 10);
            var request = new FluidRefillRequest(new StateCommandIdentity(binding.SaveSlot, binding.ResetGeneration, "s05b-success-001"),
                new StateCommandVersionFence(beforeRevision, beforeCommit, MechanicalCatalog.Version), car.instanceId,
                .375, .25, now - 10, QuoteExpiry(now - 10));
            var plan = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(source, Policy, request, now));
            var preview = PaidEconomy.Preview(source, catalog, Policy, ValuationPolicy.Defaults(),
                new PaymentRequest { kind = PaymentKind.Fluids, carId = car.instanceId, litres = .375, coolantLitres = .25 }, now);
            Check(check, plan.Committable && plan.Cost == preview.cost &&
                BitConverter.DoubleToInt64Bits(plan.OilAdded) == BitConverter.DoubleToInt64Bits(preview.oil) &&
                BitConverter.DoubleToInt64Bits(plan.CoolantAdded) == BitConverter.DoubleToInt64Bits(preview.coolant),
                "planned accepted quantities and exact rounded cost match independent S04 Preview");

            // Independent legacy outcome is calculated only in this in-memory fixture.
            double oilAdded = ServiceOperations.TopUp(source, car.instanceId, true, preview.oil);
            double coolantAdded = ServiceOperations.TopUp(source, car.instanceId, false, preview.coolant);
            source.cash = checked(beforeCash - preview.cost);
            PaidEconomy.Record(source, request.Identity.OperationId, PaymentKind.Fluids.ToString(), car.instanceId,
                beforeCash, now, oil: oilAdded, coolant: coolantAdded);
            source.revision = checked(beforeRevision + 1);
            var expectedBits = FluidBits(car.fluids);
            Check(check, expectedBits.Where((bits, index) => index != 0 && index != 1).SequenceEqual(beforeBits.Where((bits, index) => index != 0 && index != 1)),
                "independent top-up changes only two of the fourteen fluid fields");

            var command = TypedFluidRefillCommand.Create(binding, plan, Meta(path, "physics_identity"), now);
            Check(check, command.IntentBytes.SequenceEqual(IndependentIntent(command)) && command.IntentHex ==
                string.Concat(IndependentIntent(command).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))),
                "canonical intent bytes and hex match an independently encoded S05p vector");
            var oldAggregate = Meta(path, "records_digest");
            var queryTrace = new List<KeyValuePair<string, int>>();
            SqliteStateStore.FluidRefillQueryObserved = (stage, rows) => queryTrace.Add(new KeyValuePair<string, int>(stage, rows));
            TypedFluidRefillOutcome outcome;
            try { outcome = SqliteStateStore.CommitFluidRefill(binding, command, now); }
            finally { SqliteStateStore.FluidRefillQueryObserved = null; }
            Check(check, queryTrace.Count < 40 && queryTrace.All(item => item.Key != "full-row-scan" &&
                item.Key != "typed-row-inventory" && item.Key != "all-receipts"),
                "writer query trace stays within bounded metadata, selected-owner, affected-row and receipt-window lookups");
            Check(check, outcome.Status == TypedFluidRefillStatus.Committed && outcome.Result.Cost == preview.cost &&
                outcome.Result.CashAfter == source.cash && outcome.Result.CommittedLedgerRevision == source.revision &&
                outcome.Result.CommittedStorageCommit == beforeCommit + 1,
                "typed command commits the independently calculated cash, revision and one storage commit");
            ProfileStore.Flush(); SqliteStateStore.Invalidate();
            var loaded = ProfileStore.Load();
            var afterBits = FluidBits(loaded.SelectedCar.fluids);
            Check(check, loaded.cash == source.cash && loaded.revision == source.revision && loaded.lastEconomyUtcTicks == now &&
                afterBits.SequenceEqual(expectedBits), "cold full-profile load matches all fourteen independent fluid values and economy state");
            Check(check, IndependentTypedIntegrityMatches(path, SqliteStateStore.ReadState(path).TypedData.StateRecords()),
                "independent test-local typed key and canonical-byte encoders match every typed record integrity hash and family count");
            using (var db = new SqliteNative(path, true))
            {
                string identity = request.Identity.OperationId;
                Check(check, db.Scalar("SELECT COUNT(*) FROM operation_receipts WHERE save_slot=? AND reset_generation=? AND operation_id=?",
                    binding.SaveSlot, binding.ResetGeneration, identity) == "1" &&
                    db.Scalar("SELECT COUNT(*) FROM fluid_refill_results WHERE save_slot=? AND reset_generation=? AND operation_id=?",
                    binding.SaveSlot, binding.ResetGeneration, identity) == "1",
                    "independent SQL oracle confirms both durable operation and effect rows exist under the expected composite key");
            }
            Check(check, oldAggregate != Meta(path, "records_digest") && Aggregate(path) == Meta(path, "records_digest"),
                "incremental typed aggregate XOR matches full cold-row recomputation including operation and refill-result rows");
            Check(check, loaded.economyReceipts.Count == source.economyReceipts.Count &&
                loaded.economyReceipts.Last().id == request.Identity.OperationId && loaded.economyReceipts.Last().utcTicks == now,
                "business receipt append preserves the independent S04 operation, target, time and FIFO state");

            queryTrace.Clear();
            SqliteStateStore.FluidRefillQueryObserved = (stage, rows) => queryTrace.Add(new KeyValuePair<string, int>(stage, rows));
            TypedFluidRefillOutcome duplicate;
            try { duplicate = SqliteStateStore.CommitFluidRefill(binding, command, now + TimeSpan.FromDays(1).Ticks); }
            finally { SqliteStateStore.FluidRefillQueryObserved = null; }
            Check(check, duplicate.Status == TypedFluidRefillStatus.Replayed && duplicate.Result.CashAfter == source.cash &&
                duplicate.Result.CommittedStorageCommit == beforeCommit + 1 && Commit(path) == beforeCommit + 1,
                "exact durable receipt replays after quote expiry without applying a second effect");
            Check(check, queryTrace.Any(item => item.Key == "exact-operation-receipt") &&
                queryTrace.Any(item => item.Key == "exact-refill-result") &&
                queryTrace.All(item => !item.Key.Contains("fluid_states") && !item.Key.Contains("profile_economy") && !item.Key.Contains("selected-owners")),
                "receipt replay reads operation/result records only and does not read the current car/fluid/economy rows");

            var changedFluids = plan.ReadSet.Fluids.ToDetachedState();
            changedFluids.oilTemperatureC += 7;
            var changedReadSet = new FluidRefillReadSet(plan.ReadSet.CarId, plan.ReadSet.VehicleId, plan.ReadSet.EngineAssemblyId,
                plan.ReadSet.GarageJobId, plan.ReadSet.HasRebuild, plan.ReadSet.RebuildActive, plan.ReadSet.Cash + 250,
                plan.ReadSet.LastEconomyUtcTicks, plan.ReadSet.OilCapacityLitres, plan.ReadSet.CoolantCapacityLitres,
                .01, .01, new FluidRefillFluidValues(changedFluids), plan.ReadSet.HasUnsettledRun,
                plan.ReadSet.OperationAlreadyRecorded, plan.ReadSet.EconomyReceiptCount, plan.ReadSet.OldestEconomyReceiptId);
            var laterPlan = FluidRefillPlanner.Plan(new FluidRefillInput(command.Request, changedReadSet, command.AcceptedTick));
            var laterReadsetCommand = TypedFluidRefillCommand.Create(binding, laterPlan, command.PhysicsIdentity, command.AcceptedTick);
            var stableReplay = SqliteStateStore.ReconcileFluidRefill(binding, laterReadsetCommand);
            Check(check, laterPlan.Committable && command.IntentBytes.SequenceEqual(laterReadsetCommand.IntentBytes) &&
                stableReplay.Status == TypedFluidRefillStatus.Replayed && stableReplay.Result.Cost == command.Plan.Cost &&
                stableReplay.Result.CashAfter == command.Plan.CashAfter &&
                (stableReplay.Result.Cost != laterPlan.Cost || stableReplay.Result.CashAfter != laterPlan.CashAfter),
                "same full intent replays stored historical facts when later read-set cash, price and fluid values differ without repricing");

            string beforeMismatch = Fingerprint(path);
            var sameKeyVariants = new[]
            {
                MakeCommand(binding, new FluidRefillRequest(request.Identity,
                    new StateCommandVersionFence(request.Versions.ExpectedLedgerRevision + 1, request.Versions.ExpectedStorageCommit,
                        request.Versions.ExpectedContentVersion), request.CarId, request.RequestedOilLitres, request.RequestedCoolantLitres,
                    request.IssuedUtcTicks, request.ExpiresUtcTicks), plan.ReadSet, command.PhysicsIdentity, command.AcceptedTick),
                MakeCommand(binding, new FluidRefillRequest(request.Identity,
                    new StateCommandVersionFence(request.Versions.ExpectedLedgerRevision, request.Versions.ExpectedStorageCommit + 1,
                        request.Versions.ExpectedContentVersion), request.CarId, request.RequestedOilLitres, request.RequestedCoolantLitres,
                    request.IssuedUtcTicks, request.ExpiresUtcTicks), plan.ReadSet, command.PhysicsIdentity, command.AcceptedTick),
                MakeCommand(binding, new FluidRefillRequest(request.Identity,
                    new StateCommandVersionFence(request.Versions.ExpectedLedgerRevision, request.Versions.ExpectedStorageCommit,
                        request.Versions.ExpectedContentVersion + 1), request.CarId, request.RequestedOilLitres, request.RequestedCoolantLitres,
                    request.IssuedUtcTicks, request.ExpiresUtcTicks), plan.ReadSet, command.PhysicsIdentity, command.AcceptedTick),
                MakeCommand(binding, request, plan.ReadSet, command.PhysicsIdentity + "-changed", command.AcceptedTick),
                MakeCommand(binding, new FluidRefillRequest(request.Identity, request.Versions, new string('x', 220) + "\u0001",
                    request.RequestedOilLitres, request.RequestedCoolantLitres, request.IssuedUtcTicks, request.ExpiresUtcTicks),
                    ReadSetCopy(plan.ReadSet, carId: new string('x', 220) + "\u0001"), command.PhysicsIdentity, command.AcceptedTick),
                MakeCommand(binding, new FluidRefillRequest(request.Identity, request.Versions, request.CarId,
                    request.RequestedOilLitres + .125, request.RequestedCoolantLitres, request.IssuedUtcTicks, request.ExpiresUtcTicks),
                    plan.ReadSet, command.PhysicsIdentity, command.AcceptedTick),
                MakeCommand(binding, new FluidRefillRequest(request.Identity, request.Versions, request.CarId,
                    request.RequestedOilLitres, request.RequestedCoolantLitres + .125, request.IssuedUtcTicks, request.ExpiresUtcTicks),
                    plan.ReadSet, command.PhysicsIdentity, command.AcceptedTick),
                MakeCommand(binding, new FluidRefillRequest(request.Identity, request.Versions, request.CarId,
                    request.RequestedOilLitres, request.RequestedCoolantLitres, request.IssuedUtcTicks + 1, request.ExpiresUtcTicks),
                    plan.ReadSet, command.PhysicsIdentity, command.AcceptedTick),
                MakeCommand(binding, new FluidRefillRequest(request.Identity, request.Versions, request.CarId,
                    request.RequestedOilLitres, request.RequestedCoolantLitres, request.IssuedUtcTicks, request.ExpiresUtcTicks + 1),
                    plan.ReadSet, command.PhysicsIdentity, command.AcceptedTick),
                MakeCommand(binding, request, plan.ReadSet, command.PhysicsIdentity, command.AcceptedTick + 1)
            };
            string[] variantNames = { "expected ledger revision", "expected storage commit", "content revision", "physics identity",
                "car ID", "oil request bits", "coolant request bits", "quote issue tick", "quote expiry tick", "accepted confirmation tick" };
            for (int index = 0; index < sameKeyVariants.Length; index++)
            {
                var mismatch = SqliteStateStore.CommitFluidRefill(binding, sameKeyVariants[index], sameKeyVariants[index].AcceptedTick);
                Check(check, mismatch.Status == TypedFluidRefillStatus.IntentMismatch && Fingerprint(path) == beforeMismatch,
                    "committed identity rejects changed " + variantNames[index] + " by exact intent bytes without mutation");
            }

            var otherSlot = new TypedSaveSlotBinding(path, "other-slot", binding.ResetGeneration);
            var slotRequest = new FluidRefillRequest(new StateCommandIdentity(otherSlot.SaveSlot, otherSlot.ResetGeneration, request.Identity.OperationId),
                request.Versions, request.CarId, request.RequestedOilLitres, request.RequestedCoolantLitres, request.IssuedUtcTicks, request.ExpiresUtcTicks);
            var otherSlotCommand = MakeCommand(otherSlot, slotRequest, plan.ReadSet, command.PhysicsIdentity, command.AcceptedTick);
            var nextGeneration = new TypedSaveSlotBinding(path, binding.SaveSlot, binding.ResetGeneration + 1);
            var generationRequest = new FluidRefillRequest(new StateCommandIdentity(nextGeneration.SaveSlot, nextGeneration.ResetGeneration, request.Identity.OperationId),
                request.Versions, request.CarId, request.RequestedOilLitres, request.RequestedCoolantLitres, request.IssuedUtcTicks, request.ExpiresUtcTicks);
            var generationCommand = MakeCommand(nextGeneration, generationRequest, plan.ReadSet, command.PhysicsIdentity, command.AcceptedTick);
            var otherOperationRequest = new FluidRefillRequest(new StateCommandIdentity(binding.SaveSlot, binding.ResetGeneration, request.Identity.OperationId + "-other"),
                request.Versions, request.CarId, request.RequestedOilLitres, request.RequestedCoolantLitres, request.IssuedUtcTicks, request.ExpiresUtcTicks);
            var otherOperationCommand = MakeCommand(binding, otherOperationRequest, plan.ReadSet, command.PhysicsIdentity, command.AcceptedTick);
            Check(check, !command.IntentBytes.SequenceEqual(otherSlotCommand.IntentBytes) &&
                !command.IntentBytes.SequenceEqual(generationCommand.IntentBytes) &&
                !command.IntentBytes.SequenceEqual(otherOperationCommand.IntentBytes),
                "save slot, reset generation and operation ID each alter the independently specified canonical intent vector");
        }

        static void CheckRejectedNoMutation(string path, TypedSaveSlotBinding binding, TypedFluidRefillCommand command,
            long executionNow, Action<bool, string> check, string label)
        {
            string before = Fingerprint(path), sidecars = Sidecars(path);
            long commit = Commit(path); string digest = Meta(path, "records_digest");
            var outcome = SqliteStateStore.CommitFluidRefill(binding, command, executionNow);
            SqliteStateStore.Invalidate();
            Check(check, outcome.Status == TypedFluidRefillStatus.Rejected && Fingerprint(path) == before &&
                Sidecars(path) == sidecars && Commit(path) == commit && Meta(path, "records_digest") == digest, label);
        }

        static void CheckRejectionsAndIntent(string path, TypedSaveSlotBinding binding, Action<bool, string> check)
        {
            long now = Math.Max(SeedNow + 1000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var baseline = BuildCommand(path, "s05b-reject-expiry", now);
            CheckRejectedNoMutation(path, binding, baseline, baseline.Request.ExpiresUtcTicks,
                check, "new command rejects at quote expiry without changing any logical row or sidecar");

            var staleBinding = new TypedSaveSlotBinding(Path.Combine(Path.GetDirectoryName(path), "other-copy.sqlite3"), binding.SaveSlot, binding.ResetGeneration);
            var mismatch = SqliteStateStore.CommitFluidRefill(staleBinding, baseline, now);
            Check(check, mismatch.Status == TypedFluidRefillStatus.Rejected && !File.Exists(staleBinding.CanonicalPath),
                "different canonical path rejects before opening another database or resolving a receipt");
            string clone = Path.Combine(Path.GetDirectoryName(path), "identical-database-copy.sqlite3");
            File.Copy(path, clone);
            var copiedBinding = new TypedSaveSlotBinding(clone, binding.SaveSlot, binding.ResetGeneration);
            mismatch = SqliteStateStore.CommitFluidRefill(copiedBinding, baseline, now);
            var wrongSlot = new TypedSaveSlotBinding(path, "other-slot", binding.ResetGeneration);
            var wrongGeneration = new TypedSaveSlotBinding(path, binding.SaveSlot, binding.ResetGeneration + 1);
            var slotResult = SqliteStateStore.CommitFluidRefill(wrongSlot, baseline, now);
            var generationResult = SqliteStateStore.CommitFluidRefill(wrongGeneration, baseline, now);
            Check(check, mismatch.Status == TypedFluidRefillStatus.Rejected &&
                slotResult.Status == TypedFluidRefillStatus.Rejected && generationResult.Status == TypedFluidRefillStatus.Rejected,
                "identical database copy, wrong slot and wrong reset generation cannot resolve another binding's receipt");

            var stale = BuildCommand(path, "s05b-stale-global-cas", now);
            var savedCandidate = ProfileStore.Load();
            savedCandidate.cash--;
            if (!ProfileStore.TrySave(savedCandidate)) throw new IOException("Could not create intervening legacy save for strict-CAS check.");
            SqliteStateStore.Invalidate();
            string afterInterveningSave = Fingerprint(path);
            var staleResult = SqliteStateStore.CommitFluidRefill(binding, stale, now);
            Check(check, staleResult.Status == TypedFluidRefillStatus.Rejected && Fingerprint(path) == afterInterveningSave,
                "candidate captured before a legacy typed save fails strict global storage-commit CAS even when later state is valid");

            var changedPhysics = BuildCommand(path, "s05b-physics-fence", now);
            var physicsCommand = TypedFluidRefillCommand.Create(binding, changedPhysics.Plan, changedPhysics.PhysicsIdentity + "-changed", changedPhysics.AcceptedTick);
            CheckRejectedNoMutation(path, binding, physicsCommand, now, check,
                "changed physics identity rejects a new operation without modifying typed state");

            var contentProfile = ProfileStore.Load();
            var contentRequest = new FluidRefillRequest(new StateCommandIdentity(binding.SaveSlot, binding.ResetGeneration, "s05b-content-fence"),
                new StateCommandVersionFence(contentProfile.revision, contentProfile.storageCommit, MechanicalCatalog.Version + 1),
                contentProfile.SelectedCar.instanceId, .2, .2, now - 10, QuoteExpiry(now - 10));
            // Bypass the already-tested owner content guard only in this negative
            // fixture, so the storage fence itself receives the stale request.
            var contentPlan = FluidRefillPlanner.Plan(new FluidRefillInput(contentRequest, changedPhysics.ReadSet, now));
            var contentCommand = TypedFluidRefillCommand.Create(binding, contentPlan, Meta(path, "physics_identity"), now);
            CheckRejectedNoMutation(path, binding, contentCommand, now, check,
                "changed content revision rejects before any typed row/hash/receipt write");

            var contextCommand = BuildCommand(path, "s05b-owner-context", now);
            var busyCar = CommandWithReadSet(binding, contextCommand, ReadSetCopy(contextCommand.ReadSet, garageJobId: "active-garage-job"));
            CheckRejectedNoMutation(path, binding, busyCar, now, check,
                "nonempty garage job in the owner read-set rejects before write");
            var rebuilding = CommandWithReadSet(binding, contextCommand, ReadSetCopy(contextCommand.ReadSet, hasRebuild: true, rebuildActive: true));
            CheckRejectedNoMutation(path, binding, rebuilding, now, check,
                "active engine rebuild in the owner read-set rejects before write");
            string longCarId = new string('c', 220) + "\u0001";
            var oldIdRequest = new FluidRefillRequest(contextCommand.Request.Identity, contextCommand.Request.Versions,
                longCarId, contextCommand.Request.RequestedOilLitres, contextCommand.Request.RequestedCoolantLitres,
                contextCommand.Request.IssuedUtcTicks, contextCommand.Request.ExpiresUtcTicks);
            var oldIdPlan = FluidRefillPlanner.Plan(new FluidRefillInput(oldIdRequest,
                ReadSetCopy(contextCommand.ReadSet, carId: longCarId), contextCommand.AcceptedTick));
            var longCarCommand = TypedFluidRefillCommand.Create(binding, oldIdPlan, contextCommand.PhysicsIdentity, contextCommand.AcceptedTick);
            CheckRejectedNoMutation(path, binding, longCarCommand, now, check,
                "legacy persisted car ID with Unicode/control bytes and length over 192 reaches owner lookup then rejects without a fabricated token limit");

            string inactivePath = Path.Combine(Path.GetDirectoryName(path), "inactive-rebuild-state.sqlite3");
            CopyDatabase(path, inactivePath);
            ProfileStore.VerificationPath = inactivePath;
            SqliteStateStore.Invalidate();
            var inactiveBinding = new TypedSaveSlotBinding(inactivePath, binding.SaveSlot, Generation(inactivePath));
            var inactiveSource = BuildCommand(inactivePath, "s05b-inactive-rebuild", now);
            var inactiveCommand = CommandWithReadSet(inactiveBinding, inactiveSource,
                ReadSetCopy(inactiveSource.ReadSet, hasRebuild: true, rebuildActive: false));
            var inactiveOutcome = SqliteStateStore.CommitFluidRefill(inactiveBinding, inactiveCommand, now);
            Check(check, inactiveOutcome.Status == TypedFluidRefillStatus.Committed,
                "inactive rebuild presence alone retains legacy S04 eligibility semantics");
            ProfileStore.VerificationPath = path;
            SqliteStateStore.Invalidate();

            var checkpointCommand = BuildCommand(path, "s05b-pending-checkpoint", now + 1);
            SqliteStateStore.WriteCheckpoint(path, "s05b-pending-run", "{\"runId\":\"s05b-pending-run\",\"tick\":4}");
            CheckRejectedNoMutation(path, binding, checkpointCommand, now + 1, check,
                "pending checkpoint blocks a direct refill and remains byte-for-byte present");
            Check(check, SqliteStateStore.Checkpoints(path).Single() == "{\"runId\":\"s05b-pending-run\",\"tick\":4}",
                "direct refill does not merge, rewrite or retire a pending race checkpoint");
            SqliteStateStore.ClearCheckpoints(path);

            var bytes = baseline.IntentBytes; bytes[bytes.Length - 1] ^= 0x01;
            Check(check, !bytes.SequenceEqual(baseline.IntentBytes) && baseline.IntentSha256.Length == 32 &&
                baseline.IntentHex == string.Concat(baseline.IntentBytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture))),
                "intent equality evidence uses defensive full bytes with SHA recorded only as an additional check");
        }

        static void CheckFaults(string path, TypedSaveSlotBinding binding, Action<bool, string> check)
        {
            foreach (string phase in new[] { "fluid-refill:after-state-rows", "fluid-refill:after-receipts", "fluid-refill:after-eviction",
                "fluid-refill:before-commit", "fluid-refill:after-commit-before-cache", "fluid-refill:after-commit" })
            {
                // Each failure case starts from its own valid saved profile and database.
                string casePath = Path.Combine(Path.GetDirectoryName(path), phase.Replace(':', '-') + ".sqlite3");
                ProfileStore.VerificationPath = casePath;
                var fixture = NewProfile();
                SeedFullReceiptWindow(fixture);
                if (!ProfileStore.TrySave(fixture)) throw new IOException("Could not create fault fixture: " + ProfileStore.Status);
                SqliteStateStore.Invalidate();
                var caseBinding = new TypedSaveSlotBinding(casePath, binding.SaveSlot, Generation(casePath));
                long now = Math.Max(SeedNow + 1000, ProfileStore.Load().lastEconomyUtcTicks + 10);
                var command = BuildCommand(casePath, "s05b-fault-" + phase.Replace(':', '-'), now);
                string before = Fingerprint(casePath), sidecars = Sidecars(casePath);
                SqliteStateStore.FailureInjection = name => { if (name == phase) throw new IOException("S05b injected " + phase); };
                TypedFluidRefillOutcome failed;
                try { failed = SqliteStateStore.CommitFluidRefill(caseBinding, command, now); }
                finally { SqliteStateStore.FailureInjection = null; }
                SqliteStateStore.Invalidate();
                bool lostAck = phase == "fluid-refill:after-commit-before-cache" || phase == "fluid-refill:after-commit";
                if (!lostAck)
                {
                    Check(check, (failed.Status == TypedFluidRefillStatus.NotCommitted || failed.Status == TypedFluidRefillStatus.Rejected) &&
                        Fingerprint(casePath) == before && Sidecars(casePath) == sidecars,
                        "fault " + phase + " rolls back rows, hash, digest and commit exactly");
                    var retry = SqliteStateStore.CommitFluidRefill(caseBinding, command, now);
                    Check(check, retry.Status == TypedFluidRefillStatus.Committed,
                        "unchanged command identity retries successfully after definite rollback at " + phase);
                }
                else
                {
                    SqliteStateStore.Invalidate();
                    var reopened = SqliteStateStore.ReconcileFluidRefill(caseBinding, command);
                    Check(check, failed.Status == TypedFluidRefillStatus.Replayed &&
                        reopened.Status == TypedFluidRefillStatus.Replayed && Commit(casePath) == command.Request.Versions.ExpectedStorageCommit + 1,
                        "post-COMMIT lost acknowledgement is reconciled through a fresh connection without resubmitting or publishing twice at " + phase);
                }
                using (var db = new SqliteNative(casePath, true))
                {
                    var ordered = db.Query("SELECT receipt_order,id FROM economy_receipts ORDER BY receipt_order");
                    Check(check, ordered.Count == 256 && ordered.Select((row, index) =>
                        row[0] == (index + 1).ToString(CultureInfo.InvariantCulture) &&
                        row[1] == (index == 255 ? command.Request.Identity.OperationId : "s05b-fifo-" + (index + 1).ToString("D3", CultureInfo.InvariantCulture))).All(value => value),
                        "bounded FIFO drops the >128-character Unicode/control ID and appends at max order without renumbering survivors");
                }
            }
            ProfileStore.VerificationPath = path;
        }

        static void CopyDatabase(string source, string target)
        {
            ProfileStore.Flush();
            using (var db = new SqliteNative(source, true)) db.Backup(target);
            SqliteStateStore.Invalidate();
        }

        static void CheckEveryStatementBoundary(string parent, TypedSaveSlotBinding sourceBinding, Action<bool, string> check)
        {
            string source = Path.Combine(parent, "statement-fault-source.sqlite3");
            ProfileStore.VerificationPath = source;
            var profile = NewProfile(); SeedFullReceiptWindow(profile);
            if (!ProfileStore.TrySave(profile)) throw new IOException("Could not initialize statement-fault fixture: " + ProfileStore.Status);
            ProfileStore.Flush(); SqliteStateStore.DrainBackups(); SqliteStateStore.Invalidate();

            string probe = Path.Combine(parent, "statement-fault-probe.sqlite3");
            CopyDatabase(source, probe);
            ProfileStore.VerificationPath = probe;
            var probeBinding = new TypedSaveSlotBinding(probe, sourceBinding.SaveSlot, Generation(probe));
            long now = Math.Max(SeedNow + 12000, ProfileStore.Load().lastEconomyUtcTicks + 10);
            var probeCommand = BuildCommand(probe, "s05b-statement-discovery", now);
            var labels = new List<string>();
            SqliteStateStore.FailureInjection = label => labels.Add(label);
            TypedFluidRefillOutcome probeOutcome;
            try { probeOutcome = SqliteStateStore.CommitFluidRefill(probeBinding, probeCommand, now); }
            finally { SqliteStateStore.FailureInjection = null; }
            Check(check, probeOutcome.Status == TypedFluidRefillStatus.Committed && labels.Count > 0 &&
                labels.Contains("fluid-refill:after-begin") && labels.Contains("fluid-refill:after-fences") &&
                labels.Contains("fluid-refill:after-data-upsert:profile_economy") && labels.Contains("fluid-refill:after-integrity-upsert:profile_economy") &&
                labels.Contains("fluid-refill:after-data-upsert:fluid_states") && labels.Contains("fluid-refill:after-integrity-upsert:fluid_states") &&
                labels.Contains("fluid-refill:after-data-insert:economy_receipts") && labels.Contains("fluid-refill:after-data-insert:operation_receipts") &&
                labels.Contains("fluid-refill:after-data-insert:fluid_refill_results") && labels.Contains("fluid-refill:after-integrity-upsert:fluid_refill_results") &&
                labels.Contains("fluid-refill:after-data-delete:economy_receipt_items") && labels.Contains("fluid-refill:after-integrity-delete:economy_receipt_items") &&
                labels.Contains("fluid-refill:after-data-delete:economy_receipts") && labels.Contains("fluid-refill:after-integrity-delete:economy_receipts") &&
                labels.Contains("fluid-refill:after-meta:records_digest") && labels.Contains("fluid-refill:after-meta:commit_id") &&
                labels.Contains("fluid-refill:after-meta:ledger_revision") && labels.Contains("fluid-refill:before-commit") &&
                labels.Contains("fluid-refill:after-commit-before-cache") && labels.Contains("fluid-refill:after-commit"),
                "success-path statement hook stream exposes every direct row, hash, eviction and metadata mutation boundary");
            int itemDeletes = labels.Count(label => label == "fluid-refill:after-data-delete:economy_receipt_items");
            int itemHashDeletes = labels.Count(label => label == "fluid-refill:after-integrity-delete:economy_receipt_items");
            Check(check, itemDeletes == 2 && itemHashDeletes == 2,
                "full-window fixture exercises a failure boundary after each distinct disposed/retained receipt child deletion");

            string pristineFingerprint = Fingerprint(source), pristineSidecars = Sidecars(source);
            for (int faultIndex = 0; faultIndex < labels.Count; faultIndex++)
            {
                string casePath = Path.Combine(parent, "statement-fault-" + faultIndex.ToString("D3", CultureInfo.InvariantCulture) + ".sqlite3");
                CopyDatabase(source, casePath);
                ProfileStore.VerificationPath = casePath;
                var caseBinding = new TypedSaveSlotBinding(casePath, sourceBinding.SaveSlot, Generation(casePath));
                var command = BuildCommand(casePath, "s05b-statement-fault", now);
                string before = Fingerprint(casePath), sidecars = Sidecars(casePath);
                int occurrence = 0;
                string injected = labels[faultIndex];
                SqliteStateStore.FailureInjection = label =>
                {
                    if (occurrence++ == faultIndex) throw new IOException("S05b boundary fault " + injected);
                };
                TypedFluidRefillOutcome outcome;
                try { outcome = SqliteStateStore.CommitFluidRefill(caseBinding, command, now); }
                finally { SqliteStateStore.FailureInjection = null; }
                SqliteStateStore.Invalidate();
                bool postCommit = injected == "fluid-refill:after-commit-before-cache" || injected == "fluid-refill:after-commit";
                bool beforeReceiptLookup = injected == "fluid-refill:after-begin";
                if (!postCommit)
                {
                    Check(check, (beforeReceiptLookup ? outcome.Status == TypedFluidRefillStatus.OutcomeUnknown :
                        outcome.Status == TypedFluidRefillStatus.Rejected || outcome.Status == TypedFluidRefillStatus.NotCommitted) &&
                        Fingerprint(casePath) == before && Sidecars(casePath) == sidecars,
                        "statement fault " + faultIndex.ToString(CultureInfo.InvariantCulture) + " (" + injected + ") atomically restores all rows, hashes, aggregate, metadata and sidecars");
                    if (beforeReceiptLookup)
                    {
                        var absent = SqliteStateStore.ReconcileFluidRefill(caseBinding, command);
                        Check(check, absent.Status == TypedFluidRefillStatus.NotCommitted,
                            "fresh reconciliation proves receipt absence after an early outcome-unknown fault");
                    }
                    var retry = SqliteStateStore.CommitFluidRefill(caseBinding, command, now);
                    Check(check, retry.Status == TypedFluidRefillStatus.Committed,
                        "same stable intent retries once after statement rollback " + faultIndex.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    var freshConnection = SqliteStateStore.ReconcileFluidRefill(caseBinding, command);
                    Check(check, outcome.Status == TypedFluidRefillStatus.Replayed && freshConnection.Status == TypedFluidRefillStatus.Replayed &&
                        Commit(casePath) == command.Request.Versions.ExpectedStorageCommit + 1,
                        "statement fault after COMMIT is resolved from a fresh SQLite connection without a second effect at " + injected);
                }
            }
            Check(check, Fingerprint(source) == pristineFingerprint && Sidecars(source) == pristineSidecars,
                "every isolated SQL fault case leaves the pristine source database and checkpoint untouched");
        }

        static string BusinessLogicalFingerprint(string path)
        {
            using (var db = new SqliteNative(path, true))
            {
                var rows = db.Query("SELECT id,operation,target,revision,utc_ticks,cash_before,cash_after,fuel_added_bits,oil_added_bits,coolant_added_bits FROM economy_receipts ORDER BY receipt_order")
                    .Select(row => string.Join("\u001f", row)).ToList();
                rows.AddRange(db.Query("SELECT receipt_id,item_kind,item_order,item_id FROM economy_receipt_items ORDER BY receipt_id,item_kind,item_order")
                    .Select(row => string.Join("\u001f", row)));
                return string.Join("\n", rows);
            }
        }

        static string[] BusinessIds(string path)
        {
            using (var db = new SqliteNative(path, true))
                return db.Query("SELECT id FROM economy_receipts ORDER BY receipt_order").Select(row => row[0]).ToArray();
        }

        static void CheckLongHistoryAndLegacyInterop(string root, string primary, TypedSaveSlotBinding sourceBinding,
            Action<bool, string> check)
        {
            string path = Path.Combine(Path.GetDirectoryName(primary), "long-history.sqlite3");
            ProfileStore.VerificationPath = path;
            var live = NewProfile();
            if (!ProfileStore.TrySave(live)) throw new IOException("Could not initialize long-history fixture: " + ProfileStore.Status);
            SqliteStateStore.Invalidate();
            live = ProfileStore.Load();
            var binding = new TypedSaveSlotBinding(path, sourceBinding.SaveSlot, Generation(path));
            var initialBits = FluidBits(live.SelectedCar.fluids);
            string firstIntentHex = null, firstPhysics = null, firstOperation = null, firstCar = null;
            long firstGeneration = 0, firstLedger = 0, firstStorage = 0, firstContent = 0, firstIssued = 0, firstExpires = 0, firstAccepted = 0;
            double firstOilRequested = 0, firstCoolantRequested = 0, firstOilAfter = 0, firstCoolantAfter = 0, firstOilAdded = 0, firstCoolantAdded = 0;
            int firstCost = 0, firstCashAfter = 0;
            long firstCommittedRevision = 0, firstCommittedStorage = 0;
            const int commandCount = 257;
            for (int index = 0; index < commandCount; index++)
            {
                long now = Math.Max(SeedNow + 5000 + index, live.lastEconomyUtcTicks + 10);
                string operationId = "s05b-long-" + index.ToString("D3", CultureInfo.InvariantCulture);
                var request = new FluidRefillRequest(new StateCommandIdentity(binding.SaveSlot, binding.ResetGeneration, operationId),
                    new StateCommandVersionFence(live.revision, live.storageCommit, MechanicalCatalog.Version),
                    live.SelectedCar.instanceId, .001, .001, now - 10,
                    QuoteExpiry(now - 10));
                var plan = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(live, Policy, request, now));
                var quote = PaidEconomy.Preview(live, catalog, Policy, ValuationPolicy.Defaults(),
                    new PaymentRequest { kind = PaymentKind.Fluids, carId = live.SelectedCar.instanceId, litres = .001, coolantLitres = .001 }, now);
                Check(check, plan.Committable && plan.Cost == quote.cost &&
                    BitConverter.DoubleToInt64Bits(plan.OilAdded) == BitConverter.DoubleToInt64Bits(quote.oil) &&
                    BitConverter.DoubleToInt64Bits(plan.CoolantAdded) == BitConverter.DoubleToInt64Bits(quote.coolant),
                    "long-history command " + index.ToString(CultureInfo.InvariantCulture) + " matches independent S04 quantity/price oracle");
                var command = TypedFluidRefillCommand.Create(binding, plan, Meta(path, "physics_identity"), now);
                var committed = SqliteStateStore.CommitFluidRefill(binding, command, now);
                Check(check, committed.Status == TypedFluidRefillStatus.Committed &&
                    committed.Result.CommittedStorageCommit == live.storageCommit + 1,
                    "long-history command " + index.ToString(CultureInfo.InvariantCulture) + " commits under exact expected-commit CAS");
                if (index == 0)
                {
                    firstIntentHex = command.IntentHex; firstPhysics = command.PhysicsIdentity; firstOperation = request.Identity.OperationId;
                    firstCar = request.CarId; firstGeneration = request.Identity.ResetGeneration;
                    firstLedger = request.Versions.ExpectedLedgerRevision; firstStorage = request.Versions.ExpectedStorageCommit;
                    firstContent = request.Versions.ExpectedContentVersion; firstIssued = request.IssuedUtcTicks;
                    firstExpires = request.ExpiresUtcTicks; firstAccepted = command.AcceptedTick;
                    firstOilRequested = request.RequestedOilLitres; firstCoolantRequested = request.RequestedCoolantLitres;
                    firstCost = committed.Result.Cost; firstCashAfter = committed.Result.CashAfter;
                    firstOilAdded = committed.Result.OilAdded; firstCoolantAdded = committed.Result.CoolantAdded;
                    firstOilAfter = committed.Result.OilAfter; firstCoolantAfter = committed.Result.CoolantAfter;
                    firstCommittedRevision = committed.Result.CommittedLedgerRevision;
                    firstCommittedStorage = committed.Result.CommittedStorageCommit;
                }

                int beforeCash = live.cash;
                double oilAdded = ServiceOperations.TopUp(live, live.SelectedCar.instanceId, true, quote.oil);
                double coolantAdded = ServiceOperations.TopUp(live, live.SelectedCar.instanceId, false, quote.coolant);
                live.cash = checked(beforeCash - quote.cost);
                PaidEconomy.Record(live, operationId, PaymentKind.Fluids.ToString(), live.SelectedCar.instanceId,
                    beforeCash, now, oil: oilAdded, coolant: coolantAdded);
                live.revision = checked(live.revision + 1);
                live.storageCommit = checked(live.storageCommit + 1);

            }
            Check(check, FluidBits(live.SelectedCar.fluids).Where((bits, i) => i > 1).SequenceEqual(initialBits.Where((bits, i) => i > 1)),
                "257 independent top-ups retain all twelve nonquantity fluid bits");
            using (var db = new SqliteNative(path, true))
            {
                var window = db.Query("SELECT receipt_order,id FROM economy_receipts ORDER BY receipt_order");
                Check(check, window.Count == 256 && window.Select((row, index) =>
                    row[0] == (index + 1).ToString(CultureInfo.InvariantCulture) &&
                    row[1] == "s05b-long-" + (index + 1).ToString("D3", CultureInfo.InvariantCulture)).All(value => value),
                    "257 uninterrupted commands evict only the oldest receipt and preserve surviving orders one through 256");
            }
            string receiptFactsBeforeSave = BusinessLogicalFingerprint(path);
            string[] receiptIdsBeforeSave = BusinessIds(path);
            SqliteStateStore.Invalidate();
            if (!ProfileStore.TrySave(live)) throw new IOException("Legacy typed bridge interop save failed: " + ProfileStore.Status);
            Check(check, receiptFactsBeforeSave == BusinessLogicalFingerprint(path) &&
                receiptIdsBeforeSave.SequenceEqual(BusinessIds(path)) && live.economyReceipts.Select(receipt => receipt.id).SequenceEqual(receiptIdsBeforeSave),
                "legacy save after more than 256 typed commands preserves receipt facts, FIFO identity order and all logical history");
            live.storageCommit = Commit(path);

            long nextNow = live.lastEconomyUtcTicks + 10;
            var nextRequest = new FluidRefillRequest(new StateCommandIdentity(binding.SaveSlot, binding.ResetGeneration, "s05b-long-257"),
                new StateCommandVersionFence(live.revision, live.storageCommit, MechanicalCatalog.Version),
                live.SelectedCar.instanceId, .001, .001, nextNow - 10, QuoteExpiry(nextNow - 10));
            var nextPlan = FluidRefillPlanner.Plan(FluidRefillPlanner.Capture(live, Policy, nextRequest, nextNow));
            var nextCommand = TypedFluidRefillCommand.Create(binding, nextPlan, Meta(path, "physics_identity"), nextNow);
            var nextCommit = SqliteStateStore.CommitFluidRefill(binding, nextCommand, nextNow);
            Check(check, nextCommit.Status == TypedFluidRefillStatus.Committed && nextCommit.Result.CommittedStorageCommit == live.storageCommit + 1,
                "direct writer resumes after legacy save with the exact incremented commit token");
            int cashBeforeNext = live.cash;
            double addedOil = ServiceOperations.TopUp(live, live.SelectedCar.instanceId, true, nextPlan.OilAdded);
            double addedCoolant = ServiceOperations.TopUp(live, live.SelectedCar.instanceId, false, nextPlan.CoolantAdded);
            live.cash = checked(cashBeforeNext - nextPlan.Cost);
            PaidEconomy.Record(live, nextRequest.Identity.OperationId, PaymentKind.Fluids.ToString(), live.SelectedCar.instanceId,
                cashBeforeNext, nextNow, oil: addedOil, coolant: addedCoolant);
            live.revision = checked(live.revision + 1); live.storageCommit = checked(live.storageCommit + 1);
            using (var db = new SqliteNative(path, true))
            {
                var window = db.Query("SELECT receipt_order,id FROM economy_receipts ORDER BY receipt_order");
                Check(check, window.Count == 256 && window.Select((row, index) =>
                    row[0] == (index + 1).ToString(CultureInfo.InvariantCulture) &&
                    row[1] == (index == 255 ? nextRequest.Identity.OperationId : "s05b-long-" + (index + 2).ToString("D3", CultureInfo.InvariantCulture))).All(value => value),
                    "post-legacy direct commit evicts one oldest row and appends once without rewriting the surviving receipt order");
            }
            SqliteStateStore.Invalidate();
            var cold = ProfileStore.Load();
            Check(check, cold.cash == live.cash && cold.revision == live.revision &&
                FluidBits(cold.SelectedCar.fluids).SequenceEqual(FluidBits(live.SelectedCar.fluids)) &&
                cold.economyReceipts.Select(receipt => receipt.id).SequenceEqual(live.economyReceipts.Select(receipt => receipt.id)),
                "single final cold load matches independent live expected values after 257 commits and legacy interop");
            long saleNow = cold.lastEconomyUtcTicks + 10;
            var salePolicy = ValuationPolicy.Defaults();
            var sale = Valuations.Capture(cold, catalog, salePolicy, ValuationKind.Car,
                cold.SelectedCar.instanceId, "wholesale", saleNow);
            PaidEconomy.Sell(cold, catalog, salePolicy, sale, saleNow);
            cold.revision = checked(cold.revision + 1);
            SqliteStateStore.Invalidate();
            if (!ProfileStore.TrySave(cold)) throw new IOException("Car-sale fixture save failed: " + ProfileStore.Status);
            using (var db = new SqliteNative(path, true))
                Check(check, db.Scalar("SELECT COUNT(*) FROM fluid_states WHERE owner_kind='car' AND owner_id=?", firstCar) == "0",
                    "supported car sale removes its old typed fluid owner while retaining durable operation history");
            var reconstructedRequest = new FluidRefillRequest(new StateCommandIdentity(binding.SaveSlot, firstGeneration, firstOperation),
                new StateCommandVersionFence(firstLedger, firstStorage, firstContent), firstCar, firstOilRequested,
                firstCoolantRequested, firstIssued, firstExpires);
            var reconstructedIntent = TypedFluidRefillIntent.Create(binding, reconstructedRequest, firstPhysics, firstAccepted);
            Check(check, reconstructedIntent.IntentHex == firstIntentHex,
                "cold restart reconstructs byte-identical intent from owner primitives without retaining a Plan or read-set");
            var replayQueries = new List<KeyValuePair<string, int>>();
            SqliteStateStore.FluidRefillQueryObserved = (stage, rows) => replayQueries.Add(new KeyValuePair<string, int>(stage, rows));
            TypedFluidRefillOutcome replay;
            SqliteStateStore.Invalidate();
            try { replay = SqliteStateStore.ReconcileFluidRefill(binding, reconstructedIntent); }
            finally { SqliteStateStore.FluidRefillQueryObserved = null; }
            Check(check, replay.Status == TypedFluidRefillStatus.Replayed && replay.Result.BusinessReceipt == null &&
                replay.Result.Cost == firstCost && replay.Result.CashAfter == firstCashAfter &&
                replay.Result.CommittedLedgerRevision == firstCommittedRevision && replay.Result.CommittedStorageCommit == firstCommittedStorage &&
                BitConverter.DoubleToInt64Bits(replay.Result.OilAdded) == BitConverter.DoubleToInt64Bits(firstOilAdded) &&
                BitConverter.DoubleToInt64Bits(replay.Result.CoolantAdded) == BitConverter.DoubleToInt64Bits(firstCoolantAdded) &&
                BitConverter.DoubleToInt64Bits(replay.Result.OilAfter) == BitConverter.DoubleToInt64Bits(firstOilAfter) &&
                BitConverter.DoubleToInt64Bits(replay.Result.CoolantAfter) == BitConverter.DoubleToInt64Bits(firstCoolantAfter),
                "plan-free stored result hydrates its exact durable effect after history eviction, cold reload and car sale");
            Check(check, replayQueries.Any(item => item.Key == "exact-operation-receipt") &&
                replayQueries.Any(item => item.Key == "exact-refill-result") &&
                replayQueries.All(item => !item.Key.StartsWith("affected-row:", StringComparison.Ordinal) &&
                    item.Key != "selected-owners" && item.Key != "optional-business-receipt-items"),
                "durable replay after car sale reads operation/result rows without looking up a removed car, fluid owner or evicted business receipt");
            ProfileStore.VerificationPath = primary;
        }

        public static void Run(Action<bool, string> check, string outputDirectory)
        {
            if (check == null) throw new ArgumentNullException(nameof(check));
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("An isolated output directory is required.", nameof(outputDirectory));
            passed = 0;
            string oldPath = ProfileStore.VerificationPath;
            string temp = Path.Combine(Path.GetFullPath(outputDirectory), "s05b-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            string primary = Path.Combine(temp, "primary.sqlite3");
            try
            {
                authored = Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
                if (authored == null || authored.Length == 0) throw new InvalidOperationException("S05b requires authored vehicle definitions.");
                catalog = new MechanicalCatalog(authored.Select(vehicle => vehicle.spec));
                ProfileStore.VerificationPath = primary;
                var source = NewProfile();
                if (!ProfileStore.TrySave(source)) throw new IOException("Could not initialize typed v3 fixture: " + ProfileStore.Status);
                SqliteStateStore.Invalidate();
                var binding = new TypedSaveSlotBinding(primary, "slot-1", Generation(primary));
                CheckSuccessfulCommit(primary, binding, check);
                CheckRejectionsAndIntent(primary, binding, check);
                CheckLongHistoryAndLegacyInterop(temp, primary, binding, check);
                CheckEveryStatementBoundary(temp, binding, check);
                CheckIntegrityBoundaries(temp, binding, check);
                CheckBoundedGrowth(temp, primary, binding, check);
                CheckOutcomeUnknowns(temp, binding, check);
                CheckWriterPricing(temp, binding, check);
                CheckResetIdentityAndFloor(temp, binding, check);
                UnityEngine.Debug.Log("S05b typed fluid refill checks passed: " + passed.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                SqliteStateStore.FailureInjection = null;
                SqliteStateStore.FluidRefillQueryObserved = null;
                SqliteStateStore.FluidRefillCostObserved = null;
                SqliteStateStore.Invalidate();
                ProfileStore.VerificationPath = oldPath;
                authored = null; catalog = null;
            }
        }
    }
}
#endif
