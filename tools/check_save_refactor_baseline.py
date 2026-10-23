"""Verify isolated S00 artifacts; JSON here is a diagnostic file boundary only."""
from pathlib import Path
import csv
import gzip
import hashlib
import json
import math
import statistics
import struct

ROOT = Path(__file__).resolve().parents[1]
FIXTURES = ROOT / "tools/fixtures/save-refactor-s00"
REPORT = ROOT / "reports/save-refactor/S00"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def compare_import(expected, actual, path=()):
    if isinstance(expected,bool) or isinstance(actual,bool):
        assert type(expected) is type(actual) and expected == actual, path
        return 0
    if isinstance(expected, dict):
        assert isinstance(actual, dict) and expected.keys() == actual.keys(), path
        return sum(compare_import(value, actual[key], path + (key,)) for key, value in expected.items())
    if isinstance(expected, list):
        assert isinstance(actual, list) and len(expected) == len(actual), path
        return sum(compare_import(a, b, path + (index,)) for index, (a, b) in enumerate(zip(expected, actual)))
    if expected == actual:
        return 0
    assert path[-1] in {"runningSeconds", "distanceM", "consumedLife"}, (path, expected, actual)
    assert type(expected) in (float, int) and type(actual) in (float, int), path
    assert math.isfinite(expected) and math.isfinite(actual) and expected >= 0 and actual >= 0, path
    bits = lambda value: struct.unpack("!Q", struct.pack("!d", float(value)))[0]
    assert abs(bits(expected) - bits(actual)) <= 4, (path, expected, actual)
    return 1


def main():
    with (FIXTURES / "manifest.csv").open(encoding="utf-8-sig", newline="") as stream:
        fixtures = list(csv.DictReader(stream))
    assert len(fixtures) == 9
    imports = {}
    adjusted = 0
    for fixture in fixtures:
        packed = (FIXTURES / fixture["fixture"]).read_bytes()
        raw = gzip.decompress(packed)
        assert len(raw) == int(fixture["raw_bytes"]) and digest(raw) == fixture["raw_sha256"]
        assert len(packed) == int(fixture["gzip_bytes"]) and digest(packed) == fixture["gzip_sha256"]
        name = fixture["fixture"].removeprefix("fixture-").removesuffix(".json.gz")
        imported = (REPORT / ("import-" + name + ".json")).read_bytes()
        adjusted += compare_import(json.loads(raw.decode("utf-8-sig")), json.loads(imported.decode("utf-8-sig")))
        cars, spares = name.replace("cars-", ",").replace("spares", "").split(",")
        imports[(cars, spares)] = digest(imported)
    with (REPORT / "baseline.csv").open(encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    assert len(rows) == 342, len(rows)
    groups = {}
    for row in rows:
        key = (row["action"], row["cars"], row["looseParts"])
        assert row["profileSha256"] == imports[key[1:]], key
        assert math.isfinite(float(row["wallMs"])) and float(row["wallMs"]) >= 0
        groups.setdefault(key, []).append(float(row["wallMs"]))
        if row["action"] in {"inventory-read", "cart-quote-stock-express"}:
            assert not row["phases"] and not row["sqliteRowsChanged"], row
        if row["action"] == "failed-save-rollback-parity":
            assert row["sqliteRowsChanged"] == "0" and "sqlite/" in row["phases"], row
    assert len(groups) == 57 and all(len(values) == 6 for values in groups.values())
    with (REPORT / "summary.csv").open(encoding="utf-8-sig", newline="") as stream:
        summaries = list(csv.DictReader(stream))
    assert len(summaries) == 57
    for summary in summaries:
        values = groups[(summary["action"], summary["cars"], summary["looseParts"])]
        assert int(summary["n"]) == len(values)
        assert math.isclose(float(summary["medianMs"]), statistics.median(values), abs_tol=1e-8)
        assert math.isclose(float(summary["maxMs"]), max(values), abs_tol=1e-8)
    manifest = (REPORT / "manifest.txt").read_text(encoding="utf-8-sig")
    if "allocationCounter=unavailable" in manifest:
        assert all(not row["callingThreadAllocatedBytes"] for row in rows)
    assert "fixtureSource=versioned" in manifest and "physicsModel=draglab-variety-v22" in manifest
    with (REPORT / "rollback-verification.csv").open(encoding="utf-8-sig", newline="") as stream:
        rollback = list(csv.DictReader(stream))
    assert len(rollback) == 9 and {(row["cars"],row["looseParts"]) for row in rollback} == set(imports)
    assert all(row["action"] == "full-table-rollback" and row["sqliteRowsChanged"] == "0" for row in rollback)
    print(f"PASS: {len(fixtures)} frozen inputs, {adjusted} bounded legacy JSON history-rounding fields, {len(rows)} samples, {len(groups)} groups")
    print("PASS: 9 final-source all-table rollback cases; every workload scale covered.")
    print("Every other imported value, identity and health field matches exactly; normalization is separately asserted exact in Unity.")


if __name__ == "__main__":
    main()
