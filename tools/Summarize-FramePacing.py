"""Summarize opt-in native timings. Frame intervals include render/present and probe work.
Simulation/GUI/checkpoint scopes overlap Update and must not be added together.
"""
import csv
import sys
from collections import defaultdict
from pathlib import Path

def percentile(values, fraction):
    values = sorted(values)
    return values[min(len(values)-1, int((len(values)-1)*fraction))] if values else 0

for name in sys.argv[1:]:
    path = Path(name)
    groups = defaultdict(list)
    with path.open(encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            groups[row["phase"]].append(row)
    print(path)
    for phase, rows in groups.items():
        rows = rows[10:]  # Exclude each scenario's scene/entry boundary.
        if not rows:
            continue
        frames = [float(r["frameMs"]) for r in rows]
        print(f"{phase}: {len(rows)} frames; median/p95/max {percentile(frames,.5):.2f}/{percentile(frames,.95):.2f}/{max(frames):.2f} ms; >33.3 ms {sum(v>33.3 for v in frames)}; GC0 {sum(int(r['gc0']) for r in rows)}")
        for key in ("simulationMs", "checkpointMs", "raceGUIMs", "benchGUIMs", "scenarioPreparationMs", "exportMs", "importAndVerificationMs", "unityGCAllocatedBytes"):
            if key not in rows[0]:
                continue
            values = [float(r[key]) for r in rows if float(r[key]) > 0]
            if values:
                print(f"  {key}: n {len(values)}, median/p95/max {percentile(values,.5):.2f}/{percentile(values,.95):.2f}/{max(values):.2f}")
        for row in sorted(rows, key=lambda r:float(r["frameMs"]), reverse=True)[:5]:
            print("  slow:", ", ".join(f"{k}={row[k]}" for k in ("frame","frameMs","simulationMs","checkpointMs","raceGUIMs","benchGUIMs","mainThreadAllocatedBytes","gc0")))
