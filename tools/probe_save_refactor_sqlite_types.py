"""Probe the bundled Windows SQLite scalar API using an in-memory DB only.

No save is opened, created or migrated. This validates the S05p storage proposal,
not Unity's managed wrapper or the Android build. Uses Python's standard library.
"""
from __future__ import annotations

import ctypes as c
import math
from pathlib import Path
import struct


def bits(value: float) -> int:
    return struct.unpack("<q", struct.pack("<d", value))[0]


def main() -> None:
    library = Path(__file__).resolve().parents[1] / "Game/Assets/DragLab/Plugins/SQLite/Windows/x86_64/draglab_sqlite3.dll"
    sqlite = c.CDLL(str(library))

    def api(name: str, result: object, *args: object):
        fn = getattr(sqlite, name)
        fn.restype, fn.argtypes = result, list(args)
        return fn

    ptr = c.c_void_p
    version = api("sqlite3_libversion", c.c_char_p)
    open_db = api("sqlite3_open_v2", c.c_int, c.c_char_p, c.POINTER(ptr), c.c_int, ptr)
    close_db = api("sqlite3_close_v2", c.c_int, ptr)
    execute = api("sqlite3_exec", c.c_int, ptr, c.c_char_p, ptr, ptr, ptr)
    prepare = api("sqlite3_prepare_v2", c.c_int, ptr, c.c_char_p, c.c_int, c.POINTER(ptr), ptr)
    bind_int = api("sqlite3_bind_int64", c.c_int, ptr, c.c_int, c.c_int64)
    bind_real = api("sqlite3_bind_double", c.c_int, ptr, c.c_int, c.c_double)
    step = api("sqlite3_step", c.c_int, ptr)
    finalize = api("sqlite3_finalize", c.c_int, ptr)
    get_real = api("sqlite3_column_double", c.c_double, ptr, c.c_int)
    get_int = api("sqlite3_column_int64", c.c_int64, ptr, c.c_int)
    get_text = api("sqlite3_column_text", c.c_char_p, ptr, c.c_int)

    def require(code: int, expected: int = 0) -> None:
        if code != expected:
            raise RuntimeError(f"SQLite returned {code}, expected {expected}")

    values = (0.0, -0.0, 0.1, 1 / 3, 2.1, math.nextafter(2.1, math.inf),
              91.25, 0.0001, 0.63, math.nextafter(0.63, 0.0))
    db = ptr()
    require(open_db(b":memory:", c.byref(db), 2 | 4 | 0x10000, None))
    try:
        require(execute(db, b"CREATE TABLE probe(id INTEGER PRIMARY KEY, value REAL NOT NULL, value_bits INTEGER NOT NULL)", None, None, None))
        print(f"library={library.name} sqlite={version().decode('utf-8')} database=:memory:")
        print("sample,source_hex,real_roundtrip_exact,text_roundtrip_exact,int64_bits_exact")
        totals = [0, 0, 0]
        for index, value in enumerate(values):
            stmt = ptr()
            sql = b"INSERT INTO probe VALUES(?,?,?)"
            require(prepare(db, sql, len(sql), c.byref(stmt), None))
            try:
                require(bind_int(stmt, 1, index)); require(bind_real(stmt, 2, value)); require(bind_int(stmt, 3, bits(value)))
                require(step(stmt), 101)
            finally:
                require(finalize(stmt))
            stmt = ptr()
            sql = b"SELECT value,value_bits FROM probe WHERE id=?"
            require(prepare(db, sql, len(sql), c.byref(stmt), None))
            try:
                require(bind_int(stmt, 1, index)); require(step(stmt), 100)
                flags = (bits(get_real(stmt, 0)) == bits(value),
                         bits(float(get_text(stmt, 0).decode("utf-8"))) == bits(value),
                         get_int(stmt, 1) == bits(value))
                if not flags[2]:
                    raise AssertionError("Dedicated INTEGER field failed exact scalar round trip")
                totals = [count + int(flag) for count, flag in zip(totals, flags)]
                print(f"{index},{bits(value) & 0xffffffffffffffff:016X}," + ",".join(str(int(flag)) for flag in flags))
            finally:
                require(finalize(stmt))
        print(f"PASS samples={len(values)} exact_real={totals[0]} exact_text={totals[1]} exact_int64_bits={totals[2]}")
        print("Scope: bundled Windows C API only; managed wrapper, Android and migrations remain untested.")
    finally:
        require(close_db(db))


if __name__ == "__main__":
    main()
