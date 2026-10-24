"""Validate documented S05p or actual migration003 DDL in SQLite :memory: only.

This is a proposal syntax/index/constraint probe, not a production migration,
managed-wrapper test, transaction implementation or performance benchmark.
"""
from __future__ import annotations

import ctypes as c
import argparse
from pathlib import Path
import re


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--asset", action="store_true", help="Probe the actual new migration003 asset, still only in :memory:")
    options = parser.parse_args()
    if options.asset:
        ddl = (root / "Game/Assets/DragLab/Resources/Persistence/Migrations/003-typed-economy-fluids-integrity.sql.txt").read_text(encoding="utf-8-sig")
    else:
        proposal = (root / "docs/save-refactor/S05P_TYPED_STORAGE_PROPOSAL.md").read_text(encoding="utf-8-sig")
        blocks = re.findall(r"```sql\s*\n(.*?)```", proposal, re.S)
        if len(blocks) != 1:
            raise AssertionError("Expected one reviewed candidate DDL block")
        ddl = blocks[0]
    library = root / "Game/Assets/DragLab/Plugins/SQLite/Windows/x86_64/draglab_sqlite3.dll"
    sqlite = c.CDLL(str(library))
    ptr = c.c_void_p
    callback_type = c.CFUNCTYPE(c.c_int, ptr, c.c_int, c.POINTER(c.c_char_p), c.POINTER(c.c_char_p))

    def api(name, result, *args):
        function = getattr(sqlite, name)
        function.restype, function.argtypes = result, list(args)
        return function

    open_db = api("sqlite3_open_v2", c.c_int, c.c_char_p, c.POINTER(ptr), c.c_int, ptr)
    close_db = api("sqlite3_close_v2", c.c_int, ptr)
    execute = api("sqlite3_exec", c.c_int, ptr, c.c_char_p, callback_type, ptr, ptr)
    error = api("sqlite3_errmsg", c.c_char_p, ptr)
    version = api("sqlite3_libversion", c.c_char_p)
    db = ptr()
    if open_db(b":memory:", c.byref(db), 2 | 4 | 0x10000, None):
        raise RuntimeError("Could not open isolated memory database")

    def sql(statement: str, rejected: bool = False):
        rows = []

        @callback_type
        def collect(_, count, values, names):
            rows.append(tuple(values[i].decode("utf-8") if values[i] is not None else None for i in range(count)))
            return 0

        code = execute(db, statement.encode("utf-8"), collect, None, None)
        if rejected:
            if code == 0:
                raise AssertionError("Expected constraint rejection: " + statement)
        elif code:
            raise RuntimeError(error(db).decode("utf-8") + ": " + statement)
        return rows

    try:
        sql("PRAGMA foreign_keys=ON; CREATE TABLE owners(id TEXT PRIMARY KEY, kind TEXT NOT NULL, definition_id TEXT NOT NULL) WITHOUT ROWID;")
        sql(ddl)
        expected = {"owners", "profile_economy", "fluid_states", "economy_receipts", "economy_receipt_items", "operation_receipts", "fluid_refill_results", "record_integrity"}
        assert {r[0] for r in sql("SELECT name FROM sqlite_master WHERE type='table'")} == expected
        print(f"sqlite={version().decode('utf-8')} database=:memory: source={'migration003' if options.asset else 'proposal'} ddl=accepted")
        sql("INSERT INTO profile_economy VALUES(1,1000,1,1)")
        sql("INSERT INTO profile_economy VALUES(2,1000,1,1)", rejected=True)
        sql("UPDATE profile_economy SET cash=-1", rejected=True)
        sql("INSERT INTO owners VALUES('car-a','car','definition-a')")
        columns = [row[1] for row in sql("PRAGMA table_info(fluid_states)")]
        assert len([column for column in columns if column.endswith('_bits')]) == 14
        values = "'car','car-a',1," + ",".join("0" for _ in range(14))
        sql("INSERT INTO fluid_states VALUES(" + values + ")")
        sql("INSERT INTO fluid_states VALUES(" + values.replace("'car-a'", "'absent'") + ")", rejected=True)
        sql("INSERT INTO fluid_states VALUES(" + values.replace("'car','car-a'", "'bad','car-a'") + ")", rejected=True)
        sql("INSERT INTO economy_receipts VALUES('history-a',0,'FluidTopUp','car-a',2,2,1000,990,0,0,0)")
        sql("INSERT INTO economy_receipt_items VALUES('history-a','disposed',0,'part-a')")
        sql("INSERT INTO economy_receipt_items VALUES('history-a','disposed',1,NULL)")
        sql("INSERT INTO economy_receipt_items VALUES('history-a','retained',0,'')")
        assert sql("SELECT item_id FROM economy_receipt_items WHERE receipt_id='history-a' AND item_kind='disposed' ORDER BY item_order") == [('part-a',), (None,)]
        assert sql("SELECT item_id FROM economy_receipt_items WHERE receipt_id='history-a' AND item_kind='retained'") == [('',)]
        sql("INSERT INTO operation_receipts(reset_generation,operation_id,command_kind,intent_version,intent_hex,intent_sha256_hex,save_slot,ledger_revision_before,ledger_revision_after,storage_commit_before,storage_commit_after,content_revision,physics_identity,committed_utc_ticks,domain_receipt_id) VALUES(0,'op-a','FluidTopUp',1,'00','" + "0" * 64 + "','default',1,2,1,2,1,'physics',2,'history-a')")
        sql("INSERT INTO fluid_refill_results(save_slot,reset_generation,operation_id,car_id,cost,cash_after,oil_added_bits,coolant_added_bits,oil_after_bits,coolant_after_bits,domain_receipt_id) VALUES('default',0,'op-a','car-a',10,990,0,0,0,0,'history-a')")
        sql("DELETE FROM economy_receipts WHERE id='history-a'")
        assert sql("SELECT COUNT(*) FROM economy_receipt_items") == [('0',)]
        assert sql("SELECT domain_receipt_id FROM fluid_refill_results") == [('history-a',)]
        sql("DELETE FROM owners WHERE id='car-a'")
        assert sql("SELECT COUNT(*) FROM fluid_states") == [('0',)]
        assert sql("SELECT COUNT(*) FROM fluid_refill_results") == [('1',)]
        queries = {
            "economy": "SELECT cash,revision FROM profile_economy WHERE singleton=1",
            "fluid": "SELECT * FROM fluid_states WHERE owner_kind='car' AND owner_id='car-a'",
            "operation": "SELECT intent_hex FROM operation_receipts WHERE save_slot='default' AND reset_generation=0 AND operation_id='op-a'",
            "result": "SELECT * FROM fluid_refill_results WHERE save_slot='default' AND reset_generation=0 AND operation_id='op-a'",
            "oldest_history": "SELECT id FROM economy_receipts ORDER BY receipt_order LIMIT 1",
            "integrity": "SELECT row_hash_hex FROM record_integrity WHERE family='fluid_states' AND record_key='car-a'",
        }
        for label, query in queries.items():
            detail = " | ".join(row[3] for row in sql("EXPLAIN QUERY PLAN " + query))
            assert "TEMP B-TREE" not in detail
            assert "SEARCH" in detail or (label == "oldest_history" and "USING" in detail and "INDEX" in detail)
            print(f"plan {label}: {detail}")
        assert sql("PRAGMA foreign_key_check") == []
        print("PASS documented tables/14 bit fields, constraints, owner cascade, durable-result independence, 6 index plans")
        print("Scope: isolated DDL only; no managed/native Android, production migration/writer or performance evidence.")
    finally:
        if close_db(db):
            raise RuntimeError("Failed closing isolated database")


if __name__ == "__main__":
    main()
