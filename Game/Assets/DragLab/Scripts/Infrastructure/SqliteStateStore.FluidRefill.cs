using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    // S05b direct affected-record writer. This is intentionally dormant: S06 owns
    // admission, queue ordering, and live-state publication.
    internal static partial class SqliteStateStore
    {
        const int FluidReceiptLimit = 256;
        // Optional diagnostics for isolated tests; a null observer adds no call/allocation.
        internal static Action<string, int> FluidRefillQueryObserved;
        internal static Action<string, long> FluidRefillCostObserved;

        internal static TypedFluidRefillOutcome CommitFluidRefill(TypedSaveSlotBinding actualBinding,
            TypedFluidRefillCommand command, long executionNowTicks)
        {
            if (command == null) return Rejected("Fluid refill command is unavailable.");
            if (!BindingMatches(command.Intent, actualBinding, out string bindingError)) return Rejected(bindingError);
            try
            {
                lock (gate)
                {
                    if (migrations == null)
                        return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null,
                            "Save storage must be prepared by the profile owner before commands run.");
                    ValidateBinding(command.Intent, actualBinding);
                    if (!File.Exists(actualBinding.CanonicalPath) || !IsDatabase(actualBinding.CanonicalPath))
                        return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null,
                            "Selected save database is unavailable for receipt lookup.");
                    using (var db = OpenCurrentForCommand(actualBinding.CanonicalPath))
                    {
                        VerifyV3HeaderForCommand(db);
                        db.Exec("BEGIN IMMEDIATE");
                        bool committed = false;
                        bool commitStarted = false;
                        bool receiptChecked = false;
                        try
                        {
                        FailureInjection?.Invoke("fluid-refill:after-begin");
                        long generation = ReadNonnegativeMeta(db, "reset_generation");
                        long commit = ReadNonnegativeMeta(db, "commit_id");
                        ResolveResetIntent(actualBinding.CanonicalPath, generation, commit);
                        if (generation < ResetFloor(actualBinding.CanonicalPath))
                            throw new RefillRejectedException("Save generation is below the durable reset floor.");
                        if (generation != actualBinding.ResetGeneration)
                            throw new RefillRejectedException("Selected save generation changed.");
                        var prior = ReadReceipt(db, command.Intent);
                        var storedResult = ReadOptionalRefillResult(db, command.Intent);
                        if (prior == null && storedResult != null)
                        {
                            db.Exec("ROLLBACK");
                            return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null,
                                "An effect result exists without its operation receipt.");
                        }
                        if (prior != null)
                        {
                            var replay = ReplayOutcome(db, command.Intent, prior, storedResult);
                            if (replay.Status == TypedFluidRefillStatus.OutcomeUnknown)
                            {
                                db.Exec("ROLLBACK");
                                return replay;
                            }
                            db.Exec("COMMIT");
                            committed = true;
                            if (replay.Status == TypedFluidRefillStatus.Replayed)
                            {
                                InvalidateRowsAfterTypedCommit(actualBinding.CanonicalPath, ReadNonnegativeMeta(db, "commit_id"));
                                ObserveCost("resident-cache-bytes", 0);
                            }
                            return replay;
                        }
                        // Only absence of both durable records proves this is new work.
                        receiptChecked = true;
                            if (cachedPath == actualBinding.CanonicalPath && cachedCommit >= 0 && cachedCommit != commit)
                                throw new RefillRejectedException("Save changed in another writer; reload before saving.");
                            if (executionNowTicks <= 0 || executionNowTicks > DateTime.MaxValue.Ticks ||
                                executionNowTicks < command.AcceptedTick || executionNowTicks >= command.Request.ExpiresUtcTicks)
                                throw new RefillRejectedException("Payment quote stale/expired.");
                            // Repeat all admission fences under the write transaction.
                            if (ReadNonnegativeMeta(db, "reset_generation") != generation ||
                                ReadNonnegativeMeta(db, "commit_id") != commit)
                                throw new RefillRejectedException("Save generation or commit changed.");
                            if (RefillScalar(db, "checkpoint-existence", "SELECT 1 FROM pending_checkpoints LIMIT 1") != null)
                                throw new RefillRejectedException("A pending race checkpoint must be reconciled before refilling.");
                            if (command.ReadSet.HasUnsettledRun || command.ReadSet.OperationAlreadyRecorded)
                                throw new RefillRejectedException("Close the active run before changing assets or resources.");
                            if (!string.IsNullOrEmpty(command.ReadSet.GarageJobId) || command.ReadSet.RebuildActive ||
                                !string.Equals(command.ReadSet.CarId, command.Request.CarId, StringComparison.Ordinal))
                                throw new RefillRejectedException("Vehicle is unavailable for fluid service.");
                            var identity = command.Request.Identity;
                            if (identity.SaveSlot != actualBinding.SaveSlot || identity.ResetGeneration != generation)
                                throw new RefillRejectedException("Save slot or reset generation changed.");
                            if (commit != command.Request.Versions.ExpectedStorageCommit ||
                                ReadNonnegativeMeta(db, "ledger_revision") != command.Request.Versions.ExpectedLedgerRevision)
                                throw new RefillRejectedException("State command version fence is stale.");
                            if (ReadNonnegativeMeta(db, "content_revision") != command.Request.Versions.ExpectedContentVersion ||
                                RefillScalar(db, "metadata-excluded:physics_identity", "SELECT value FROM state_meta WHERE name='physics_identity'") != command.PhysicsIdentity)
                                throw new RefillRejectedException("Content or physics identity changed.");
                            var affected = VerifyReadSetRows(db, command);

                            byte[] aggregate = FromHex(RefillScalar(db, "metadata-excluded:records_digest", "SELECT value FROM state_meta WHERE name='records_digest'"));
                            if (RefillScalar(db, "metadata-excluded:digest_algorithm", "SELECT value FROM state_meta WHERE name='digest_algorithm'") != "xor-sha256-records-v2")
                                throw new InvalidDataException("Unsupported typed integrity aggregate.");
                            int count = int.Parse(RefillScalar(db, "bounded-receipt-count", "SELECT COUNT(*) FROM economy_receipts"), CultureInfo.InvariantCulture);
                            if (count != command.ReadSet.EconomyReceiptCount || count > FluidReceiptLimit)
                                throw new RefillRejectedException("Business receipt window changed.");
                            string oldestId = RefillScalar(db, "oldest-receipt", "SELECT id FROM economy_receipts ORDER BY receipt_order LIMIT 1");
                            if (!string.Equals(oldestId, command.ReadSet.OldestEconomyReceiptId, StringComparison.Ordinal))
                                throw new RefillRejectedException("Business receipt window changed.");
                            FailureInjection?.Invoke("fluid-refill:after-fences");

                            var oldEconomy = affected.Economy;
                            var oldFluid = affected.Fluid;
                            Xor(aggregate, oldEconomy.HashBytes());
                            Xor(aggregate, oldFluid.HashBytes());
                            long nextRevision = checked(command.Request.Versions.ExpectedLedgerRevision + 1);
                            long nextCommit = checked(commit + 1);
                            var newEconomy = new SqliteEconomyState(command.Plan.CashAfter, nextRevision,
                                command.AcceptedTick).AsStateRecord();
                            var newFluid = TypedFluidStateRecord.FromFluidState("car", command.Request.CarId,
                                command.Plan.AfterFluids.ToDetachedState()).AsStateRecord();
                            ObserveHashInput(newEconomy); ObserveHashInput(newFluid);
                            Xor(aggregate, newEconomy.HashBytes());
                            Xor(aggregate, newFluid.HashBytes());
                            UpsertAffected(db, oldEconomy, newEconomy);
                            UpsertAffected(db, oldFluid, newFluid);
                            FailureInjection?.Invoke("fluid-refill:after-state-rows");

                            string maxOrderText = RefillScalar(db, "receipt-order-max", "SELECT MAX(receipt_order) FROM economy_receipts");
                            long maxOrder = maxOrderText == null ? -1 : long.Parse(maxOrderText, CultureInfo.InvariantCulture);
                            long receiptOrder = checked(maxOrder + 1);
                            var delta = command.Plan.Receipt;
                            var businessReceipt = new SqliteEconomyReceipt(delta.Id, receiptOrder, delta.Operation,
                                delta.Target, delta.Revision, delta.UtcTicks, delta.CashBefore, delta.CashAfter,
                                BitConverter.DoubleToInt64Bits(delta.FuelAdded), BitConverter.DoubleToInt64Bits(delta.OilAdded),
                                BitConverter.DoubleToInt64Bits(delta.CoolantAdded), Array.Empty<string>(), Array.Empty<string>());
                            var businessRecord = businessReceipt.AsStateRecord();
                            ObserveHashInput(businessRecord);
                            var opReceipt = OperationReceipt(command, nextRevision, nextCommit);
                            var refillResult = RefillResultRow(command);
                            ObserveHashInput(opReceipt); ObserveHashInput(refillResult);
                            InsertAffected(db, businessRecord);
                            InsertAffected(db, opReceipt);
                            InsertAffected(db, refillResult);
                            FailureInjection?.Invoke("fluid-refill:after-receipts");

                            var inserted = new[] { businessRecord, opReceipt, refillResult };
                            foreach (var row in inserted) Xor(aggregate, row.HashBytes());
                            if (count == FluidReceiptLimit)
                                EvictOldestReceipt(db, aggregate, oldestId);
                            SetFluidMeta(db, "records_digest", Hex(aggregate));
                            SetFluidMeta(db, "commit_id", nextCommit.ToString(CultureInfo.InvariantCulture));
                            SetFluidMeta(db, "ledger_revision", nextRevision.ToString(CultureInfo.InvariantCulture));
                            FailureInjection?.Invoke("fluid-refill:before-commit");
                            commitStarted = true;
                            db.Exec("COMMIT");
                            committed = true;
                            ScheduleBackup(actualBinding.CanonicalPath);
                            FailureInjection?.Invoke("fluid-refill:after-commit-before-cache");
                            InvalidateRowsAfterTypedCommit(actualBinding.CanonicalPath, nextCommit);
                            ObserveCost("resident-cache-bytes", 0);
                            var result = NewResult(command, nextRevision, nextCommit, businessReceipt);
                            FailureInjection?.Invoke("fluid-refill:after-commit");
                            return new TypedFluidRefillOutcome(TypedFluidRefillStatus.Committed, result, null);
                        }
                        catch (RefillRejectedException error)
                        {
                            if (!committed) db.Exec("ROLLBACK");
                            return Rejected(error.Message);
                        }
                        catch (IntentMismatchException error)
                        {
                            if (!committed) db.Exec("ROLLBACK");
                            return new TypedFluidRefillOutcome(TypedFluidRefillStatus.IntentMismatch, null, error.Message);
                        }
                        catch (Exception error)
                        {
                            if (!committed)
                            {
                                try { db.Exec("ROLLBACK"); } catch { }
                                if (!commitStarted)
                                    return receiptChecked ? Rejected(error.Message) :
                                        new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null, error.Message);
                                var reconciled = ReconcileFluidRefill(actualBinding, command);
                                if (reconciled.Status == TypedFluidRefillStatus.Replayed) return reconciled;
                                if (reconciled.Status == TypedFluidRefillStatus.NotCommitted) return Rejected(error.Message);
                                return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null, error.Message);
                            }
                            return ReconcileFluidRefill(actualBinding, command);
                        }
                    }
                }
            }
            catch (RefillRejectedException error) { return Rejected(error.Message); }
            catch (IntentMismatchException error) { return new TypedFluidRefillOutcome(TypedFluidRefillStatus.IntentMismatch, null, error.Message); }
            catch (Exception error) { return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null, error.Message); }
        }

        internal static TypedFluidRefillOutcome ReconcileFluidRefill(TypedSaveSlotBinding actualBinding,
            TypedFluidRefillCommand command)
            => command == null ? Rejected("Fluid refill command is unavailable.") : ReconcileFluidRefill(actualBinding, command.Intent);

        internal static TypedFluidRefillOutcome ReconcileFluidRefill(TypedSaveSlotBinding actualBinding,
            TypedFluidRefillIntent command)
        {
            if (command == null) return Rejected("Fluid refill command is unavailable.");
            if (!BindingMatches(command, actualBinding, out string bindingError)) return Rejected(bindingError);
            try
            {
                lock (gate)
                {
                    if (migrations == null)
                        return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null,
                            "Save storage is not prepared for receipt reconciliation.");
                    ValidateBinding(command, actualBinding);
                    if (!File.Exists(actualBinding.CanonicalPath) || !IsDatabase(actualBinding.CanonicalPath))
                        return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null, "Save database is unavailable for receipt reconciliation.");
                    using (var db = OpenCurrentForCommand(actualBinding.CanonicalPath))
                    {
                        VerifyV3HeaderForCommand(db);
                        db.Exec("BEGIN IMMEDIATE");
                        bool finished = false;
                        try
                        {
                            long generation = ReadNonnegativeMeta(db, "reset_generation");
                            long commit = ReadNonnegativeMeta(db, "commit_id");
                            ResolveResetIntent(actualBinding.CanonicalPath, generation, commit);
                            if (generation < ResetFloor(actualBinding.CanonicalPath) || generation != actualBinding.ResetGeneration)
                                throw new RefillRejectedException("Save generation changed before receipt reconciliation.");
                            var prior = ReadReceipt(db, command);
                            var storedResult = ReadOptionalRefillResult(db, command);
                            if (prior == null && storedResult == null)
                            {
                                db.Exec("COMMIT"); finished = true;
                                return new TypedFluidRefillOutcome(TypedFluidRefillStatus.NotCommitted, null, null);
                            }
                            if (prior == null || storedResult == null)
                            {
                                db.Exec("ROLLBACK"); finished = true;
                                return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null, "Operation receipt and refill result are inconsistent.");
                            }
                            var outcome = ReplayOutcome(db, command, prior, storedResult);
                            db.Exec("COMMIT"); finished = true;
                            if (outcome.Status == TypedFluidRefillStatus.Replayed)
                            {
                                InvalidateRowsAfterTypedCommit(actualBinding.CanonicalPath, ReadNonnegativeMeta(db, "commit_id"));
                                ObserveCost("resident-cache-bytes", 0);
                            }
                            return outcome;
                        }
                        catch
                        {
                            if (!finished) try { db.Exec("ROLLBACK"); } catch { }
                            throw;
                        }
                    }
                }
            }
            catch (RefillRejectedException error) { return Rejected(error.Message); }
            catch (IntentMismatchException error) { return new TypedFluidRefillOutcome(TypedFluidRefillStatus.IntentMismatch, null, error.Message); }
            catch (Exception error) { return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null, error.Message); }
        }

        sealed class RefillRejectedException : Exception { internal RefillRejectedException(string message) : base(message) { } }
        static TypedFluidRefillOutcome Rejected(string error) => new TypedFluidRefillOutcome(TypedFluidRefillStatus.Rejected, null, error);

        static bool BindingMatches(TypedFluidRefillIntent command, TypedSaveSlotBinding binding, out string error)
        {
            if (binding == null || !command.MatchesActualBinding(binding)) { error = "Selected save binding changed."; return false; }
            error = null; return true;
        }
        static void ValidateBinding(TypedFluidRefillIntent command, TypedSaveSlotBinding binding)
        {
            if (!command.MatchesActualBinding(binding) || command.Request.Identity.SaveSlot != binding.SaveSlot ||
                command.Request.Identity.ResetGeneration != binding.ResetGeneration ||
                !string.Equals(Path.GetFullPath(binding.CanonicalPath), binding.CanonicalPath,
                    Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new RefillRejectedException("Selected save binding changed.");
        }
        static void VerifyV3HeaderForCommand(SqliteNative db)
        {
            if (RefillScalar(db, "schema-header-excluded", "PRAGMA user_version") != SchemaVersion.ToString(CultureInfo.InvariantCulture) ||
                RefillScalar(db, "schema-header-excluded", "PRAGMA application_id") != ApplicationId.ToString(CultureInfo.InvariantCulture))
                throw new InvalidDataException("Direct typed refill requires the current migrated save schema.");
        }
        static long ReadNonnegativeMeta(SqliteNative db, string key)
        {
            string value = RefillScalar(db, "metadata-excluded:" + key, "SELECT value FROM state_meta WHERE name=?", key);
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) || result < 0)
                throw new InvalidDataException("Invalid saved metadata: " + key);
            return result;
        }
        sealed class AffectedRows { internal TypedStateRecord Economy, Fluid; }
        static AffectedRows VerifyReadSetRows(SqliteNative db, TypedFluidRefillCommand command)
        {
            var read = command.ReadSet;
            var owners = RefillQuery(db, "selected-owners", "SELECT id,kind,definition_id FROM owners WHERE id IN (?,?) ORDER BY id",
                command.Request.CarId, read.EngineAssemblyId);
            if (owners.Count != 2 || owners.Any(row => row[0] == command.Request.CarId && (row[1] != "car" || row[2] != read.VehicleId)) ||
                owners.Any(row => row[0] == read.EngineAssemblyId && (row[1] != "engine" || row[2] != read.VehicleId)))
                throw new RefillRejectedException("Vehicle or attached engine identity changed.");
            var economy = ReadExact(db, "profile_economy", "1");
            var fluid = ReadExact(db, "fluid_states", "car", command.Request.CarId);
            VerifyIntegrity(db, economy); VerifyIntegrity(db, fluid);
            var typedEconomy = SqliteEconomyState.FromStateRecord(economy);
            var typedFluid = TypedFluidStateRecord.FromStateRecord(fluid).Restore();
            if (typedEconomy.Cash != read.Cash || typedEconomy.Revision != command.Request.Versions.ExpectedLedgerRevision ||
                typedEconomy.LastEconomyUtcTicks != read.LastEconomyUtcTicks || !SameFluids(typedFluid, read.Fluids))
                throw new RefillRejectedException("Cash or target fluid values changed.");
            return new AffectedRows { Economy = economy, Fluid = fluid };
        }
        static bool SameFluids(FluidState actual, FluidRefillFluidValues expected)
        {
            return BitConverter.DoubleToInt64Bits(actual.oilLitres) == BitConverter.DoubleToInt64Bits(expected.OilLitres) &&
                BitConverter.DoubleToInt64Bits(actual.coolantLitres) == BitConverter.DoubleToInt64Bits(expected.CoolantLitres) &&
                BitConverter.DoubleToInt64Bits(actual.oilTemperatureC) == BitConverter.DoubleToInt64Bits(expected.OilTemperatureC) &&
                BitConverter.DoubleToInt64Bits(actual.oilPressureBar) == BitConverter.DoubleToInt64Bits(expected.OilPressureBar) &&
                BitConverter.DoubleToInt64Bits(actual.coolantPressureBar) == BitConverter.DoubleToInt64Bits(expected.CoolantPressureBar) &&
                BitConverter.DoubleToInt64Bits(actual.oilCondition) == BitConverter.DoubleToInt64Bits(expected.OilCondition) &&
                BitConverter.DoubleToInt64Bits(actual.oilConsumedLitres) == BitConverter.DoubleToInt64Bits(expected.OilConsumedLitres) &&
                BitConverter.DoubleToInt64Bits(actual.oilLeakedLitres) == BitConverter.DoubleToInt64Bits(expected.OilLeakedLitres) &&
                BitConverter.DoubleToInt64Bits(actual.coolantLostLitres) == BitConverter.DoubleToInt64Bits(expected.CoolantLostLitres) &&
                BitConverter.DoubleToInt64Bits(actual.serviceElapsedSeconds) == BitConverter.DoubleToInt64Bits(expected.ServiceElapsedSeconds) &&
                BitConverter.DoubleToInt64Bits(actual.oilBurnLps) == BitConverter.DoubleToInt64Bits(expected.OilBurnLps) &&
                BitConverter.DoubleToInt64Bits(actual.oilLeakLps) == BitConverter.DoubleToInt64Bits(expected.OilLeakLps) &&
                BitConverter.DoubleToInt64Bits(actual.coolantLeakLps) == BitConverter.DoubleToInt64Bits(expected.CoolantLeakLps) &&
                BitConverter.DoubleToInt64Bits(actual.coolantVentLps) == BitConverter.DoubleToInt64Bits(expected.CoolantVentLps);
        }

        static TypedStateRecord ReadExact(SqliteNative db, string table, params string[] key)
        {
            string[] columns = TableColumns(table);
            string[] primary = PrimaryKeyColumns(table);
            if (key.Length != primary.Length) throw new ArgumentException("Wrong typed key width.");
            string where = string.Join(" AND ", primary.Select(name => name + "=?"));
            var rows = RefillQuery(db, "affected-row:" + table, "SELECT " + string.Join(",", columns) + " FROM " + table + " WHERE " + where, key.Cast<object>().ToArray());
            if (rows.Count != 1) throw new InvalidDataException("Missing affected typed row: " + table);
            return CreateCommandRecord(table, rows[0]);
        }
        static void VerifyIntegrity(SqliteNative db, TypedStateRecord record)
        {
            string saved = RefillScalar(db, "affected-integrity:" + record.TableName, "SELECT row_hash_hex FROM record_integrity WHERE family=? AND record_key=?", record.TableName, record.RecordKey);
            if (saved == null || saved != record.HashHex()) throw new InvalidDataException("Affected typed row integrity mismatch: " + record.TableName + "/" + record.RecordKey);
        }
        static void UpsertAffected(SqliteNative db, TypedStateRecord oldRow, TypedStateRecord next)
        {
            if (FluidRefillCostObserved != null) ObserveParameters("typed-data-upsert:" + next.TableName, next.SqlValues());
            UpsertTypedRecord(db, next);
            FailureInjection?.Invoke("fluid-refill:after-data-upsert:" + next.TableName);
            SetIntegrity(db, next);
        }
        static void InsertAffected(SqliteNative db, TypedStateRecord row)
        {
            if (FluidRefillCostObserved != null) ObserveParameters("typed-data-insert:" + row.TableName, row.SqlValues());
            InsertTypedRecords(db, new[] { row });
            FailureInjection?.Invoke("fluid-refill:after-data-insert:" + row.TableName);
            SetIntegrity(db, row);
        }
        static void SetIntegrity(SqliteNative db, TypedStateRecord row)
        {
            string hash = row.HashHex();
            if (FluidRefillCostObserved != null) ObserveParameters("integrity-upsert:" + row.TableName,
                new object[] { row.TableName, row.RecordKey, hash });
            using (var statement = db.Prepare("INSERT INTO record_integrity(family,record_key,row_hash_hex) VALUES(?,?,?) ON CONFLICT(family,record_key) DO UPDATE SET row_hash_hex=excluded.row_hash_hex"))
                statement.Execute(row.TableName, row.RecordKey, hash);
            FailureInjection?.Invoke("fluid-refill:after-integrity-upsert:" + row.TableName);
        }
        static void SetFluidMeta(SqliteNative db, string name, string value)
        {
            if (FluidRefillCostObserved != null) ObserveParameters("metadata-upsert:" + name, new object[] { name, value });
            SetMeta(db, name, value);
            FailureInjection?.Invoke("fluid-refill:after-meta:" + name);
        }

        static TypedStateRecord OperationReceipt(TypedFluidRefillCommand command, long revision, long commit)
        {
            var identity = command.Request.Identity;
            var fields = new[] {
                TypedStateField.Text("save_slot", identity.SaveSlot), TypedStateField.Int64("reset_generation", identity.ResetGeneration),
                TypedStateField.Text("operation_id", identity.OperationId), TypedStateField.Text("command_kind", "FluidTopUp"),
                TypedStateField.Int64("intent_version", 1), TypedStateField.Text("intent_hex", command.IntentHex),
                TypedStateField.Text("intent_sha256_hex", Hex(command.IntentSha256)),
                TypedStateField.Int64("ledger_revision_before", command.Request.Versions.ExpectedLedgerRevision),
                TypedStateField.Int64("ledger_revision_after", revision),
                TypedStateField.Int64("storage_commit_before", command.Request.Versions.ExpectedStorageCommit),
                TypedStateField.Int64("storage_commit_after", commit),
                TypedStateField.Int64("content_revision", command.Request.Versions.ExpectedContentVersion),
                TypedStateField.Text("physics_identity", command.PhysicsIdentity),
                TypedStateField.Int64("committed_utc_ticks", command.AcceptedTick),
                TypedStateField.Text("domain_receipt_id", command.Plan.Receipt.Id)
            };
            return new TypedStateRecord("operation_receipts", TypedStateEncoding.CompositeKey(fields[0], fields[1], fields[2]), fields);
        }
        static TypedStateRecord RefillResultRow(TypedFluidRefillCommand command)
        {
            var identity = command.Request.Identity; var plan = command.Plan;
            var fields = new[] {
                TypedStateField.Text("save_slot", identity.SaveSlot), TypedStateField.Int64("reset_generation", identity.ResetGeneration),
                TypedStateField.Text("operation_id", identity.OperationId), TypedStateField.Text("car_id", command.Request.CarId),
                TypedStateField.Int64("cost", plan.Cost), TypedStateField.Int64("cash_after", plan.CashAfter),
                TypedStateField.Int64("oil_added_bits", BitConverter.DoubleToInt64Bits(plan.OilAdded)),
                TypedStateField.Int64("coolant_added_bits", BitConverter.DoubleToInt64Bits(plan.CoolantAdded)),
                TypedStateField.Int64("oil_after_bits", BitConverter.DoubleToInt64Bits(plan.AfterFluids.OilLitres)),
                TypedStateField.Int64("coolant_after_bits", BitConverter.DoubleToInt64Bits(plan.AfterFluids.CoolantLitres)),
                TypedStateField.Text("domain_receipt_id", plan.Receipt.Id)
            };
            return new TypedStateRecord("fluid_refill_results", TypedStateEncoding.CompositeKey(fields[0], fields[1], fields[2]), fields);
        }

        static void EvictOldestReceipt(SqliteNative db, byte[] aggregate, string id)
        {
            var receipt = ReadExact(db, "economy_receipts", id);
            VerifyIntegrity(db, receipt); Xor(aggregate, receipt.HashBytes());
            var children = RefillQuery(db, "evicted-receipt-items", "SELECT receipt_id,item_kind,item_order,item_id FROM economy_receipt_items WHERE receipt_id=? ORDER BY item_kind,item_order", id)
                .Select(row => CreateCommandRecord("economy_receipt_items", row)).ToArray();
            foreach (var child in children)
            {
                VerifyIntegrity(db, child); Xor(aggregate, child.HashBytes());
                if (FluidRefillCostObserved != null) ObserveParameters("typed-data-delete:economy_receipt_items",
                    new object[] { child.ReadText("receipt_id"), child.ReadText("item_kind"), child.ReadInt64("item_order") });
                using (var remove = db.Prepare("DELETE FROM economy_receipt_items WHERE receipt_id=? AND item_kind=? AND item_order=?"))
                    remove.Execute(child.ReadText("receipt_id"), child.ReadText("item_kind"), child.ReadInt64("item_order"));
                FailureInjection?.Invoke("fluid-refill:after-data-delete:economy_receipt_items");
                DeleteIntegrity(db, child);
            }
            if (FluidRefillCostObserved != null) ObserveParameters("typed-data-delete:economy_receipts", new object[] { id });
            using (var remove = db.Prepare("DELETE FROM economy_receipts WHERE id=?")) remove.Execute(id);
            FailureInjection?.Invoke("fluid-refill:after-data-delete:economy_receipts");
            DeleteIntegrity(db, receipt);
            FailureInjection?.Invoke("fluid-refill:after-eviction");
        }
        static void DeleteIntegrity(SqliteNative db, TypedStateRecord row)
        {
            if (FluidRefillCostObserved != null) ObserveParameters("integrity-delete:" + row.TableName,
                new object[] { row.TableName, row.RecordKey });
            using (var remove = db.Prepare("DELETE FROM record_integrity WHERE family=? AND record_key=?")) remove.Execute(row.TableName, row.RecordKey);
            FailureInjection?.Invoke("fluid-refill:after-integrity-delete:" + row.TableName);
        }

        sealed class ReceiptRecord { internal string IntentHex; internal long RevisionAfter, CommitAfter; internal string DomainReceiptId; }
        static ReceiptRecord ReadReceipt(SqliteNative db, TypedFluidRefillIntent command)
        {
            var identity = command.Request.Identity;
            var rows = RefillQuery(db, "exact-operation-receipt", "SELECT save_slot,reset_generation,operation_id,command_kind,intent_version,intent_hex,intent_sha256_hex,ledger_revision_before,ledger_revision_after,storage_commit_before,storage_commit_after,content_revision,physics_identity,committed_utc_ticks,domain_receipt_id FROM operation_receipts WHERE save_slot=? AND reset_generation=? AND operation_id=?",
                identity.SaveSlot, identity.ResetGeneration, identity.OperationId);
            if (rows.Count == 0) return null;
            var receipt = CreateCommandRecord("operation_receipts", rows[0]); VerifyIntegrity(db, receipt);
            string intent = receipt.ReadText("intent_hex");
            if (receipt.ReadText("command_kind") != "FluidTopUp" || receipt.ReadInt64("intent_version") != 1 ||
                intent != command.IntentHex || receipt.ReadText("intent_sha256_hex") != Hex(command.IntentSha256) ||
                receipt.ReadInt64("ledger_revision_before") != command.Request.Versions.ExpectedLedgerRevision ||
                receipt.ReadInt64("storage_commit_before") != command.Request.Versions.ExpectedStorageCommit ||
                receipt.ReadInt64("content_revision") != command.Request.Versions.ExpectedContentVersion ||
                receipt.ReadText("physics_identity") != command.PhysicsIdentity ||
                receipt.ReadInt64("committed_utc_ticks") != command.AcceptedTick)
                throw new IntentMismatchException("Operation identity was already used for different intent.");
            if (receipt.ReadInt64("ledger_revision_after") != checked(receipt.ReadInt64("ledger_revision_before") + 1) ||
                receipt.ReadInt64("storage_commit_after") != checked(receipt.ReadInt64("storage_commit_before") + 1))
                throw new InvalidDataException("Committed operation versions are inconsistent.");
            return new ReceiptRecord { IntentHex = intent, RevisionAfter = receipt.ReadInt64("ledger_revision_after"),
                CommitAfter = receipt.ReadInt64("storage_commit_after"), DomainReceiptId = receipt.ReadText("domain_receipt_id") };
        }
        sealed class IntentMismatchException : Exception { internal IntentMismatchException(string message) : base(message) { } }
        static TypedFluidRefillOutcome ReplayOutcome(SqliteNative db, TypedFluidRefillIntent command, ReceiptRecord receipt,
            TypedStateRecord stored = null)
        {
            try
            {
                var result = stored ?? ReadOptionalRefillResult(db, command);
                if (result == null) throw new InvalidDataException("The committed operation has no durable effect result.");
                VerifyIntegrity(db, result);
                if (result.ReadText("domain_receipt_id") != receipt.DomainReceiptId ||
                    result.ReadText("car_id") != command.Request.CarId) throw new InvalidDataException("Durable operation result does not match its recorded intent.");
                var optional = ReadOptionalBusinessReceipt(db, receipt.DomainReceiptId);
                return new TypedFluidRefillOutcome(TypedFluidRefillStatus.Replayed, NewStoredResult(command, receipt.RevisionAfter, receipt.CommitAfter, result, optional), null);
            }
            catch (IntentMismatchException error) { return new TypedFluidRefillOutcome(TypedFluidRefillStatus.IntentMismatch, null, error.Message); }
            catch (Exception error) { return new TypedFluidRefillOutcome(TypedFluidRefillStatus.OutcomeUnknown, null, error.Message); }
        }
        static TypedStateRecord ReadOptionalRefillResult(SqliteNative db, TypedFluidRefillIntent command)
        {
            var identity = command.Request.Identity;
            var rows = RefillQuery(db, "exact-refill-result", "SELECT save_slot,reset_generation,operation_id,car_id,cost,cash_after,oil_added_bits,coolant_added_bits,oil_after_bits,coolant_after_bits,domain_receipt_id FROM fluid_refill_results WHERE save_slot=? AND reset_generation=? AND operation_id=?",
                identity.SaveSlot, identity.ResetGeneration, identity.OperationId);
            if (rows.Count == 0) return null;
            var record = CreateCommandRecord("fluid_refill_results", rows[0]); VerifyIntegrity(db, record); return record;
        }
        static SqliteEconomyReceipt ReadOptionalBusinessReceipt(SqliteNative db, string id)
        {
            var rows = RefillQuery(db, "optional-business-receipt", "SELECT id,receipt_order,operation,target,revision,utc_ticks,cash_before,cash_after,fuel_added_bits,oil_added_bits,coolant_added_bits FROM economy_receipts WHERE id=?", id);
            if (rows.Count == 0) return null;
            var record = CreateCommandRecord("economy_receipts", rows[0]); VerifyIntegrity(db, record);
            var children = ReadReceiptChildren(db, id);
            foreach (var child in children) VerifyIntegrity(db, child);
            SqliteEconomyReceipt.ReadItems(children, id, out var disposed, out var retained);
            return SqliteEconomyReceipt.FromStateRecord(record, disposed, retained);
        }
        static TypedStateRecord[] ReadReceiptChildren(SqliteNative db, string id) => RefillQuery(db, "optional-business-receipt-items",
            "SELECT receipt_id,item_kind,item_order,item_id FROM economy_receipt_items WHERE receipt_id=? ORDER BY item_kind,item_order", id)
            .Select(row => CreateCommandRecord("economy_receipt_items", row)).ToArray();
        static TypedFluidRefillResult NewResult(TypedFluidRefillCommand command, long revision, long commit,
            SqliteEconomyReceipt businessReceipt)
        {
            var plan = command.Plan;
            return TypedFluidRefillResult.ForCommitted(command, revision, commit, command.Request.CarId,
                plan.Cost, plan.CashAfter, plan.OilAdded, plan.CoolantAdded,
                plan.AfterFluids.OilLitres, plan.AfterFluids.CoolantLitres, plan.Receipt.Id, businessReceipt);
        }
        static TypedFluidRefillResult NewStoredResult(TypedFluidRefillIntent command, long revision, long commit,
            TypedStateRecord stored, SqliteEconomyReceipt optionalReceipt)
        {
            return TypedFluidRefillResult.FromStored(command, revision, commit, stored.ReadText("car_id"),
                checked((int)stored.ReadInt64("cost")), checked((int)stored.ReadInt64("cash_after")),
                BitConverter.Int64BitsToDouble(stored.ReadInt64("oil_added_bits")),
                BitConverter.Int64BitsToDouble(stored.ReadInt64("coolant_added_bits")),
                BitConverter.Int64BitsToDouble(stored.ReadInt64("oil_after_bits")),
                BitConverter.Int64BitsToDouble(stored.ReadInt64("coolant_after_bits")),
                stored.ReadText("domain_receipt_id"), optionalReceipt);
        }
        static System.Collections.Generic.List<string[]> RefillQuery(SqliteNative db, string stage,
            string sql, params object[] parameters)
        {
            if (FluidRefillCostObserved != null) ObserveParameters("query:" + stage, parameters);
            var rows = db.Query(sql, parameters);
            if (FluidRefillCostObserved != null)
            {
                long bytes = 0;
                for (int i = 0; i < rows.Count; i++)
                    for (int j = 0; j < rows[i].Length; j++)
                        if (rows[i][j] != null) bytes = checked(bytes + Encoding.UTF8.GetByteCount(rows[i][j]));
                ObserveCost("sqlite-read-returned-column-bytes:" + stage, bytes);
            }
            var observer = FluidRefillQueryObserved;
            if (observer != null)
            {
                try { observer(stage, rows.Count); }
                catch { /* Diagnostics must never alter transaction outcomes. */ }
            }
            return rows;
        }
        static TypedStateRecord CreateCommandRecord(string table, string[] values)
        {
            var record = CreateTypedRecord(table, values);
            ObserveHashInput(record);
            return record;
        }
        static void ObserveHashInput(TypedStateRecord record)
        {
            if (FluidRefillCostObserved == null) return;
            ObserveCost("typed-canonical-hash-input-bytes", record.CanonicalBytes().LongLength);
        }
        static void ObserveParameters(string stage, object[] values)
        {
            long bytes = 0;
            foreach (var value in values)
            {
                if (value == null) continue;
                if (value is string text) bytes = checked(bytes + Encoding.UTF8.GetByteCount(text));
                else bytes = checked(bytes + sizeof(long));
            }
            ObserveCost("sqlite-bound-parameter-bytes:" + stage, bytes);
        }
        static void ObserveCost(string stage, long value)
        {
            var observer = FluidRefillCostObserved;
            if (observer == null) return;
            try { observer(stage, value); }
            catch { /* Diagnostics must never alter transaction outcomes. */ }
        }
        static string RefillScalar(SqliteNative db, string stage, string sql, params object[] parameters)
        {
            var rows = RefillQuery(db, stage, sql, parameters);
            return rows.Count == 0 ? null : rows[0][0];
        }
    }
}
