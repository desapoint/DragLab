using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    /// <summary>Typed economy singleton as stored in schema version 3.</summary>
    internal sealed class SqliteEconomyState
    {
        internal const string Table = "profile_economy";
        internal readonly int Cash;
        internal readonly long Revision;
        internal readonly long LastEconomyUtcTicks;

        internal SqliteEconomyState(int cash, long revision, long lastEconomyUtcTicks)
        {
            Cash = cash;
            Revision = revision;
            LastEconomyUtcTicks = lastEconomyUtcTicks;
        }

        internal static SqliteEconomyState Capture(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return new SqliteEconomyState(profile.cash, profile.revision, profile.lastEconomyUtcTicks);
        }

        internal static SqliteEconomyState FromStateRecord(TypedStateRecord row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (row.TableName != Table || row.RecordKey != "1") throw new InvalidDataException("Unexpected profile_economy row identity.");
            ValidateNames(row, "singleton", "cash", "revision", "last_economy_utc_ticks");
            if (row.ReadInt64("singleton") != 1) throw new InvalidDataException("Unexpected profile_economy singleton.");
            long cash = row.ReadInt64("cash");
            if (cash < 0 || cash > int.MaxValue) throw new InvalidDataException("Invalid typed profile cash.");
            long revision=row.ReadInt64("revision"),ticks=row.ReadInt64("last_economy_utc_ticks");
            if(revision<0||ticks<0||ticks>DateTime.MaxValue.Ticks)throw new InvalidDataException("Invalid typed economy revision or clock.");
            return new SqliteEconomyState((int)cash, revision, ticks);
        }

        internal TypedStateRecord AsStateRecord()
        {
            return new TypedStateRecord(Table, "1", new[]
            {
                TypedStateField.Int64("singleton", 1),
                TypedStateField.Int64("cash", Cash),
                TypedStateField.Int64("revision", Revision),
                TypedStateField.Int64("last_economy_utc_ticks", LastEconomyUtcTicks)
            });
        }

        static void ValidateNames(TypedStateRecord row, params string[] names)
        {
            var fields = row.Fields();
            if (fields.Length != names.Length) throw new InvalidDataException("Unexpected profile_economy field count.");
            for (int i = 0; i < names.Length; i++)
                if (fields[i].Name != names[i] || fields[i].Kind != TypedStateValueKind.Int64)
                    throw new InvalidDataException("Unexpected profile_economy field order or type.");
        }
    }

    /// <summary>Immutable typed business receipt and its ordered nullable item arrays.</summary>
    internal sealed class SqliteEconomyReceipt
    {
        internal const string Table = "economy_receipts";
        internal readonly string Id;
        internal readonly long ReceiptOrder;
        internal readonly string Operation;
        internal readonly string Target;
        internal readonly long Revision;
        internal readonly long UtcTicks;
        internal readonly int CashBefore, CashAfter;
        internal readonly long FuelAddedBits, OilAddedBits, CoolantAddedBits;
        readonly string[] disposed, retained;

        internal SqliteEconomyReceipt(string id, long receiptOrder, string operation, string target,
            long revision, long utcTicks, int cashBefore, int cashAfter,
            long fuelAddedBits, long oilAddedBits, long coolantAddedBits,
            IEnumerable<string> disposedIds, IEnumerable<string> retainedIds)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            Id = id; ReceiptOrder = receiptOrder; Operation = operation; Target = target;
            Revision = revision; UtcTicks = utcTicks; CashBefore = cashBefore; CashAfter = cashAfter;
            FuelAddedBits = fuelAddedBits; OilAddedBits = oilAddedBits; CoolantAddedBits = coolantAddedBits;
            disposed = (disposedIds ?? throw new ArgumentNullException(nameof(disposedIds))).ToArray();
            retained = (retainedIds ?? throw new ArgumentNullException(nameof(retainedIds))).ToArray();
        }

        internal static SqliteEconomyReceipt Capture(EconomyReceipt receipt, int order)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            return new SqliteEconomyReceipt(receipt.id, order, receipt.operation, receipt.target,
                receipt.revision, receipt.utcTicks, receipt.cashBefore, receipt.cashAfter,
                BitConverter.DoubleToInt64Bits(receipt.fuelAdded), BitConverter.DoubleToInt64Bits(receipt.oilAdded),
                BitConverter.DoubleToInt64Bits(receipt.coolantAdded), receipt.disposed, receipt.retained);
        }

        internal static SqliteEconomyReceipt FromStateRecord(TypedStateRecord row, IEnumerable<string> disposedIds, IEnumerable<string> retainedIds)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (row.TableName != Table || row.RecordKey != row.ReadText("id")) throw new InvalidDataException("Unexpected economy receipt row identity.");
            var fields = row.Fields();
            string[] names = { "id", "receipt_order", "operation", "target", "revision", "utc_ticks", "cash_before", "cash_after", "fuel_added_bits", "oil_added_bits", "coolant_added_bits" };
            if (fields.Length != names.Length) throw new InvalidDataException("Unexpected economy receipt field count.");
            for (int i = 0; i < names.Length; i++) if (fields[i].Name != names[i]) throw new InvalidDataException("Unexpected economy receipt field order.");
            string target = fields[3].Kind == TypedStateValueKind.Null ? null : row.ReadText("target");
            if (fields[3].Kind != TypedStateValueKind.Null && fields[3].Kind != TypedStateValueKind.Text) throw new InvalidDataException("Invalid nullable economy receipt target.");
            return new SqliteEconomyReceipt(row.ReadText("id"), row.ReadInt64("receipt_order"), row.ReadText("operation"), target,
                row.ReadInt64("revision"), row.ReadInt64("utc_ticks"), checked((int)row.ReadInt64("cash_before")), checked((int)row.ReadInt64("cash_after")),
                row.ReadInt64("fuel_added_bits"), row.ReadInt64("oil_added_bits"), row.ReadInt64("coolant_added_bits"), disposedIds, retainedIds);
        }

        internal string[] DisposedIds() => (string[])disposed.Clone();
        internal string[] RetainedIds() => (string[])retained.Clone();

        internal static void ReadItems(TypedStateRecord[] records, string receiptId, out string[] disposedIds, out string[] retainedIds)
        {
            disposedIds = ReadItems(records, receiptId, "disposed");
            retainedIds = ReadItems(records, receiptId, "retained");
        }

        static string[] ReadItems(TypedStateRecord[] records, string receiptId, string kind)
        {
            var selected = records.Where(row => row.TableName == "economy_receipt_items" &&
                row.ReadText("receipt_id") == receiptId && row.ReadText("item_kind") == kind)
                .OrderBy(row => row.ReadInt64("item_order")).ToArray();
            for (int i = 0; i < selected.Length; i++)
            {
                var fields = selected[i].Fields();
                if (fields.Length != 4 || fields[0].Name != "receipt_id" || fields[1].Name != "item_kind" ||
                    fields[2].Name != "item_order" || fields[3].Name != "item_id" || selected[i].ReadInt64("item_order") != i)
                    throw new InvalidDataException("Invalid typed economy receipt item order or shape.");
                string expectedKey=TypedStateEncoding.CompositeKey(fields[0],fields[1],fields[2]);
                if(selected[i].RecordKey!=expectedKey)throw new InvalidDataException("Typed economy receipt item key does not match its fields.");
            }
            return selected.Select(row =>
            {
                var field = row.Fields()[3];
                if (field.Kind == TypedStateValueKind.Null) return null;
                if (field.Kind != TypedStateValueKind.Text) throw new InvalidDataException("Invalid nullable receipt item id.");
                return row.ReadText("item_id");
            }).ToArray();
        }

        internal EconomyReceipt Restore() => new EconomyReceipt
        {
            id = Id, operation = Operation, target = Target, revision = Revision, utcTicks = UtcTicks,
            cashBefore = CashBefore, cashAfter = CashAfter,
            fuelAdded = BitConverter.Int64BitsToDouble(FuelAddedBits),
            oilAdded = BitConverter.Int64BitsToDouble(OilAddedBits),
            coolantAdded = BitConverter.Int64BitsToDouble(CoolantAddedBits),
            disposed = DisposedIds(), retained = RetainedIds()
        };

        internal TypedStateRecord AsStateRecord() => new TypedStateRecord(Table, Id, new[]
        {
            TypedStateField.Text("id", Id),
            TypedStateField.Int64("receipt_order", ReceiptOrder),
            TypedStateField.Text("operation", Operation),
            Target == null ? TypedStateField.Null("target") : TypedStateField.Text("target", Target),
            TypedStateField.Int64("revision", Revision),
            TypedStateField.Int64("utc_ticks", UtcTicks),
            TypedStateField.Int64("cash_before", CashBefore),
            TypedStateField.Int64("cash_after", CashAfter),
            TypedStateField.Int64("fuel_added_bits", FuelAddedBits),
            TypedStateField.Int64("oil_added_bits", OilAddedBits),
            TypedStateField.Int64("coolant_added_bits", CoolantAddedBits)
        });

        internal TypedStateRecord[] ItemStateRecords()
        {
            var rows = new List<TypedStateRecord>(disposed.Length + retained.Length);
            AddItems(rows, Id, "disposed", disposed);
            AddItems(rows, Id, "retained", retained);
            return rows.ToArray();
        }

        static void AddItems(List<TypedStateRecord> rows, string receiptId, string kind, string[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                var keyFields = new[] { TypedStateField.Text("receipt_id", receiptId), TypedStateField.Text("item_kind", kind), TypedStateField.Int64("item_order", i) };
                string key = TypedStateEncoding.CompositeKey(keyFields);
                rows.Add(new TypedStateRecord("economy_receipt_items", key, new[]
                {
                    TypedStateField.Text("receipt_id", receiptId),
                    TypedStateField.Text("item_kind", kind),
                    TypedStateField.Int64("item_order", i),
                    ids[i] == null ? TypedStateField.Null("item_id") : TypedStateField.Text("item_id", ids[i])
                }));
            }
        }
    }

    /// <summary>Typed rows for one profile; arrays are private copies.</summary>
    internal sealed class SqliteTypedProfileData
    {
        internal readonly SqliteEconomyState Economy;
        readonly TypedFluidStateRecord[] fluids;
        readonly SqliteEconomyReceipt[] receipts;

        // An empty, zero-commit database is a schema bootstrap, not a player.
        // Only the store's strict bootstrap checks may admit this envelope.
        SqliteTypedProfileData()
        {
            fluids = Array.Empty<TypedFluidStateRecord>();
            receipts = Array.Empty<SqliteEconomyReceipt>();
        }

        internal static SqliteTypedProfileData Empty() => new SqliteTypedProfileData();

        internal SqliteTypedProfileData(SqliteEconomyState economy, IEnumerable<TypedFluidStateRecord> fluidRows, IEnumerable<SqliteEconomyReceipt> receiptRows)
        {
            Economy = economy ?? throw new ArgumentNullException(nameof(economy));
            fluids = (fluidRows ?? throw new ArgumentNullException(nameof(fluidRows))).ToArray();
            receipts = (receiptRows ?? throw new ArgumentNullException(nameof(receiptRows))).ToArray();
        }

        internal TypedFluidStateRecord[] Fluids => (TypedFluidStateRecord[])fluids.Clone();
        internal SqliteEconomyReceipt[] Receipts => (SqliteEconomyReceipt[])receipts.Clone();

        internal static SqliteTypedProfileData FromStateRecords(IEnumerable<TypedStateRecord> source)
        {
            var rows = (source ?? throw new ArgumentNullException(nameof(source))).ToArray();
            if (rows.Length == 0) return Empty();
            var economyRow = rows.SingleOrDefault(row => row.TableName == SqliteEconomyState.Table);
            if (economyRow == null) throw new InvalidDataException("Missing typed profile economy row.");
            var typedReceipts = rows.Where(row => row.TableName == SqliteEconomyReceipt.Table)
                .Select(row =>
                {
                    SqliteEconomyReceipt.ReadItems(rows, row.ReadText("id"), out var disposedIds, out var retainedIds);
                    return SqliteEconomyReceipt.FromStateRecord(row, disposedIds, retainedIds);
                }).OrderBy(receipt => receipt.ReceiptOrder).ToArray();
            if (typedReceipts.Length > 256) throw new InvalidDataException("Typed business receipt window exceeds the supported bound.");
            var economy=SqliteEconomyState.FromStateRecord(economyRow);
            var receiptIds=new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < typedReceipts.Length; i++)
            {
                var receipt=typedReceipts[i];
                if(receipt.ReceiptOrder!=i)throw new InvalidDataException("Typed business receipt order is not contiguous.");
                if(string.IsNullOrEmpty(receipt.Id)||!receiptIds.Add(receipt.Id)||string.IsNullOrEmpty(receipt.Operation)||receipt.Revision<0||receipt.Revision>economy.Revision||receipt.UtcTicks<=0||receipt.UtcTicks>economy.LastEconomyUtcTicks||receipt.UtcTicks>DateTime.MaxValue.Ticks||receipt.CashBefore<0||receipt.CashAfter<0)
                    throw new InvalidDataException("Invalid typed business receipt scalar fields.");
                foreach(long bits in new[]{receipt.FuelAddedBits,receipt.OilAddedBits,receipt.CoolantAddedBits})
                {double amount=BitConverter.Int64BitsToDouble(bits);if(double.IsNaN(amount)||double.IsInfinity(amount)||amount<0)throw new InvalidDataException("Invalid typed business receipt quantity.");}
                var disposed=receipt.DisposedIds();var retained=receipt.RetainedIds();
                if(disposed.Distinct(StringComparer.Ordinal).Count()!=disposed.Length||retained.Distinct(StringComparer.Ordinal).Count()!=retained.Length||disposed.Intersect(retained,StringComparer.Ordinal).Any())
                    throw new InvalidDataException("Invalid typed business receipt item identities.");
            }
            return new SqliteTypedProfileData(economy,
                rows.Where(row => row.TableName == TypedFluidStateRecord.Table).Select(TypedFluidStateRecord.FromStateRecord), typedReceipts);
        }

        internal static SqliteTypedProfileData Capture(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var fluidRows = new List<TypedFluidStateRecord>();
            foreach (var car in profile.cars)
                fluidRows.Add(TypedFluidStateRecord.FromFluidState("car", car.instanceId, car.fluids));
            foreach (var engine in profile.engines)
            {
                // A fitted engine's fluid field is a legacy duplicate of its parent car.
                // Only detached engines own an independent typed fluid row.
                if (!string.IsNullOrEmpty(engine.parentVehicleId)) continue;
                fluidRows.Add(TypedFluidStateRecord.FromFluidState("engine", engine.instanceId, engine.fluids));
            }
            return new SqliteTypedProfileData(SqliteEconomyState.Capture(profile), fluidRows,
                profile.economyReceipts.Select(SqliteEconomyReceipt.Capture));
        }

        internal TypedStateRecord[] StateRecords()
        {
            if (Economy == null) return Array.Empty<TypedStateRecord>();
            var rows = new List<TypedStateRecord> { Economy.AsStateRecord() };
            rows.AddRange(fluids.Select(fluid => fluid.AsStateRecord()));
            foreach (var receipt in receipts)
            {
                rows.Add(receipt.AsStateRecord());
                rows.AddRange(receipt.ItemStateRecords());
            }
            return rows.ToArray();
        }

        internal void ApplyTo(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (Economy == null) throw new InvalidDataException("An empty database cannot materialize a player profile.");
            if(profile.cars==null||profile.engines==null)throw new InvalidDataException("Typed fluid owners are missing from the legacy projection.");
            profile.cash = Economy.Cash;
            profile.revision = Economy.Revision;
            profile.lastEconomyUtcTicks = Economy.LastEconomyUtcTicks;
            profile.economyReceipts = receipts.Select(receipt => receipt.Restore()).ToList();
            var expected=new HashSet<string>(StringComparer.Ordinal);
            foreach(var car in profile.cars)
                if(car==null||string.IsNullOrEmpty(car.instanceId)||!expected.Add("car\n"+car.instanceId))throw new InvalidDataException("Invalid or duplicate car fluid owner in legacy projection.");
            foreach(var engine in profile.engines)
            {
                if(engine==null||string.IsNullOrEmpty(engine.instanceId))throw new InvalidDataException("Invalid engine fluid owner in legacy projection.");
                if(!string.IsNullOrEmpty(engine.parentVehicleId))engine.fluids=null;
                else if(!expected.Add("engine\n"+engine.instanceId))throw new InvalidDataException("Duplicate detached engine fluid owner in legacy projection.");
            }
            var actual=new HashSet<string>(StringComparer.Ordinal);
            foreach (var fluid in fluids)
            {
                string ownerKey=fluid.OwnerKind+"\n"+fluid.OwnerId;
                if(!expected.Contains(ownerKey)||!actual.Add(ownerKey))throw new InvalidDataException("Unexpected or duplicate typed fluid owner.");
                if (fluid.OwnerKind == "car")
                {
                    var car = profile.cars.FirstOrDefault(value => value.instanceId == fluid.OwnerId);
                    if (car == null) throw new InvalidDataException("Typed car fluid owner is missing from the legacy projection.");
                    car.fluids = fluid.Restore();
                }
                else
                {
                    var engine = profile.engines.FirstOrDefault(value => value.instanceId == fluid.OwnerId);
                    if (engine == null || !string.IsNullOrEmpty(engine.parentVehicleId)) throw new InvalidDataException("Typed engine fluid owner is missing or attached in the legacy projection.");
                    engine.fluids = fluid.Restore();
                }
            }
            if(!expected.SetEquals(actual))throw new InvalidDataException("Typed fluid state is missing for a car or detached engine.");
        }
    }

    /// <summary>Result of an integrity-checked v3 database load.</summary>
    internal sealed class SqliteStateLoadResult
    {
        internal readonly string LegacyJson;
        internal readonly SqliteTypedProfileData TypedData;
        internal readonly long Commit;
        internal readonly long Generation;
        internal readonly PlayerProfile ValidatedMigrationProfile;
        internal PlayerProfile LoadedProfile { get; set; }

        internal SqliteStateLoadResult(string legacyJson, SqliteTypedProfileData typedData, long commit, long generation, PlayerProfile validatedMigrationProfile = null)
        {
            LegacyJson = legacyJson ?? throw new ArgumentNullException(nameof(legacyJson));
            TypedData = typedData ?? throw new ArgumentNullException(nameof(typedData));
            Commit = commit;
            Generation = generation;
            ValidatedMigrationProfile = validatedMigrationProfile;
        }
    }

}
