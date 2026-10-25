"""Inspect complete, opt-in native UI frame intervals; no game-state access.

Example: python tools/check_parts_browser_frames.py frame-pacing.csv \
    --prefix bounded-parts/ --limit-ms 50
Exit 1 means a selected interval exceeds the supplied limit. Missing/invalid
evidence exits 2. This checks recorded intervals only, not visual acceptance,
complete scenario coverage, or causation of a measured frame stall.
"""

import argparse
import csv
import math
from pathlib import Path
import statistics
import sys


HEADER = [
    "frame", "phase", "frameMs", "raceUpdateMs", "benchUpdateMs", "simulationMs",
    "checkpointMs", "raceGUIMs", "benchGUIMs", "scenarioPreparationMs", "exportMs",
    "importAndVerificationMs", "mainThreadAllocatedBytes", "gc0",
    "unityGCAllocatedBytes", "nativeOwnedMs", "nativePartsMs", "nativeCartMs",
    "nativeOtherMenusMs", "nativeSurfacesMs",
]
TIMINGS = [name for name in HEADER if name.endswith("Ms")]


def load_frames(path):
    with Path(path).open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        if reader.fieldnames != HEADER:
            raise ValueError("Unexpected frame-probe header; expected the established 20 columns")
        rows = []
        last_frame = None
        for line, raw in enumerate(reader, start=2):
            if None in raw or any(value is None for value in raw.values()):
                raise ValueError(f"Incomplete frame row at line {line}")
            try:
                row = {name: raw[name] if name == "phase" else
                       float(raw[name]) if name in TIMINGS else int(raw[name])
                       for name in HEADER}
            except ValueError as error:
                raise ValueError(f"Invalid number at line {line}") from error
            if any(not math.isfinite(row[name]) or row[name] < 0 for name in TIMINGS):
                raise ValueError(f"Non-finite or negative time at line {line}")
            if not row["phase"] or row["frame"] < 1:
                raise ValueError(f"Missing phase/frame at line {line}")
            if last_frame is not None and row["frame"] != last_frame + 1:
                raise ValueError(f"Non-contiguous frame evidence at line {line}")
            if row["mainThreadAllocatedBytes"] < 0 or row["gc0"] < 0 or row["unityGCAllocatedBytes"] < -1:
                raise ValueError(f"Invalid allocation/GC evidence at line {line}")
            rows.append(row)
            last_frame = row["frame"]
    if not rows:
        raise ValueError("Empty frame evidence")
    return rows


def p95(values):
    ordered = sorted(values)
    return ordered[max(0, math.ceil(len(ordered) * .95) - 1)]


def inspect(rows, prefixes, limit):
    # Keep repeated phase intervals separate: returning to a menu must not hide
    # its later stall in an aggregate from an earlier visit.
    intervals = []
    for row in rows:
        if not any(row["phase"].startswith(prefix) for prefix in prefixes):
            continue
        if not intervals or intervals[-1][-1]["frame"] + 1 != row["frame"] or intervals[-1][-1]["phase"] != row["phase"]:
            intervals.append([])
        intervals[-1].append(row)
    if not intervals:
        raise ValueError("No frames match the requested phase prefixes")
    lines = ["# Native Parts frame interval check", "",
             f"Limit: {limit:g} ms. Selected {sum(map(len, intervals))} recorded frames in {len(intervals)} intervals.", "",
             "| Phase | Frames | Count | Median ms | P95 ms | Max ms | Max Parts ms | Over limit | Reported main bytes | Max Unity bytes | GC0 |",
             "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |"]
    failures = 0
    for interval in intervals:
        values = [row["frameMs"] for row in interval]
        slow = sum(value > limit for value in values)
        failures += slow
        phase = interval[0]["phase"].replace("|", "\\|")
        lines.append(f"| {phase} | {interval[0]['frame']}-{interval[-1]['frame']} | {len(interval)} | "
                     f"{statistics.median(values):.3f} | {p95(values):.3f} | {max(values):.3f} | "
                     f"{max(row['nativePartsMs'] for row in interval):.3f} | {slow} | "
                     f"{sum(row['mainThreadAllocatedBytes'] for row in interval)} | "
                     f"{max(row['unityGCAllocatedBytes'] for row in interval)} | {sum(row['gc0'] for row in interval)} |")
    lines += ["", f"Recorded-interval result: {'FAIL' if failures else 'PASS'} ({failures} frames over limit).",
              "", "This does not prove scenario coverage, first-complete-list timing, visual parity, Android performance or the cause of a stall. A zero reported allocation count is not proof of zero allocations; recorder availability and platform limitations require separate evidence. Retain the full raw CSV and the scenario verification report."]
    return "\n".join(lines) + "\n", bool(failures)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("csv", type=Path)
    parser.add_argument("--prefix", action="append", required=True)
    parser.add_argument("--limit-ms", type=float, default=50)
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    try:
        if not math.isfinite(args.limit_ms) or args.limit_ms <= 0 or any(not prefix for prefix in args.prefix):
            raise ValueError("Use a finite positive limit and nonempty prefixes")
        report, failed = inspect(load_frames(args.csv), args.prefix, args.limit_ms)
        if args.out:
            args.out.write_text(report, encoding="utf-8")
        else:
            sys.stdout.write(report)
        return int(failed)
    except (OSError, ValueError) as error:
        print(f"Invalid frame evidence: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
