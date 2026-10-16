"""Fail-closed visual regression gate for an explicit, versioned capture contract.

Exit 0: all captures pass; 1: visual/capture failure; 2: inconclusive comparisons.
No alignment, resizing, masking or automatic baseline updates are performed.
"""
import argparse
import csv
import fnmatch
import hashlib
import html
import json
import re
from collections import Counter
from pathlib import Path

import numpy as np
from PIL import Image


def pixels(before, after, limits):
    a, b = np.asarray(before, dtype=np.int16), np.asarray(after, dtype=np.int16)
    delta = np.abs(a - b)
    changed = delta.max(axis=2) > limits["channel_tolerance"]
    tile = limits["tile_size"]
    worst_percent = worst_mean = 0.0
    # Half-tile overlap catches a missing small control crossing a tile boundary.
    height, width = changed.shape
    for y in range(0, height, tile // 2):
        for x in range(0, width, tile // 2):
            y0, x0 = min(y, max(0, height - tile)), min(x, max(0, width - tile))
            region = delta[y0:y0 + tile, x0:x0 + tile]
            worst_percent = max(worst_percent, float(changed[y0:y0 + tile, x0:x0 + tile].mean() * 100))
            worst_mean = max(worst_mean, float(region.mean()))
    pink_a = (a[:, :, 0] > 220) & (a[:, :, 1] < 50) & (a[:, :, 2] > 220)
    pink_b = (b[:, :, 0] > 220) & (b[:, :, 1] < 50) & (b[:, :, 2] > 220)
    metrics = dict(changed_percent=float(changed.mean() * 100), mean_channel_error=float(delta.mean()),
                   worst_tile_changed_percent=worst_percent, worst_tile_mean_error=worst_mean,
                   new_magenta_pixels=int((pink_b & ~pink_a).sum()))
    failures = [key for key, value in metrics.items() if value > limits["max_" + key]]
    return metrics, failures


def validate(root, contract):
    if contract.get("version") != 1 or not contract.get("captures") or not contract.get("suites"):
        raise ValueError("An explicit nonempty version-1 capture/suite contract is required")
    expected = {row["image"]: row for row in contract["captures"]}
    if len(expected) != len(contract["captures"]):
        raise ValueError("Duplicate expected capture")
    if {Path(key).parts[0] for key in expected} != set(contract["suites"]):
        raise ValueError("Capture contract must cover exactly its declared suites")
    for key in expected:
        if Path(key).is_absolute() or ".." in Path(key).parts or "\\" in key or ":" in key:
            raise ValueError("Capture paths must stay within the phase folders")
    limits = contract["limits"]
    if not isinstance(limits["tile_size"], int) or limits["tile_size"] < 2:
        raise ValueError("tile_size must be an integer of at least two")
    if any(value < 0 or not np.isfinite(value) for value in limits.values()):
        raise ValueError("Limits must be finite nonnegative numbers")
    evidence = []
    for phase in ("before", "after"):
        folder = root / phase
        actual = {p.relative_to(folder).as_posix() for p in folder.rglob("*.png")}
        for key in sorted(actual - expected.keys()):
            evidence.append(f"{phase}: unexpected capture {key}")
        actual_suites = {p.parent.name for p in folder.glob("*/status.txt")}
        if actual_suites != set(contract["suites"]):
            evidence.append(f"{phase}: suite inventory differs from contract")
        for suite in contract["suites"]:
            base = folder / suite
            status = base / "status.txt"
            report = next((base / name for name in ("verification.txt", "runtime-verification.txt")
                           if (base / name).is_file()), None)
            text = report.read_text(encoding="utf-8-sig") if report else ""
            has_checks = re.search(r"Checks: [1-9]\d*|^PASS:", text, re.M)
            if (not status.is_file() or status.read_text(encoding="utf-8-sig").strip() != "PASS"
                    or not has_checks or re.search(r"^FAIL:|runtime errors: [1-9]", text, re.M | re.I)):
                evidence.append(f"{phase}/{suite}: missing or failed capture evidence")
    rows = []
    for key, item in expected.items():
        row = dict(image=key, verdict="PASS", reason="within fixed image and local-tile budgets")
        try:
            paths = [root / phase / key for phase in ("before", "after")]
            if not all(p.is_file() for p in paths):
                raise ValueError("missing before/after image")
            row["before_sha256"] = hashlib.sha256(paths[0].read_bytes()).hexdigest()
            row["after_sha256"] = hashlib.sha256(paths[1].read_bytes()).hexdigest()
            if row["before_sha256"] != item["before_sha256"]:
                raise ValueError("baseline changed since contract creation; automatic rebaselining is forbidden")
            with Image.open(paths[0]) as original, Image.open(paths[1]) as candidate:
                if original.size != (item["width"], item["height"]) or candidate.size != original.size:
                    raise ValueError("dimension mismatch; images were not resampled")
                metrics, failures = pixels(original.convert("RGB"), candidate.convert("RGB"), limits)
            row.update({key: round(value, 6) for key, value in metrics.items()})
            if failures:
                row.update(verdict="FAIL", reason="exceeded: " + ", ".join(failures))
                dynamic = next((r for r in contract.get("nonrepeatable", [])
                                if fnmatch.fnmatchcase(key, r["pattern"])), None)
                # A known changing fixture is inconclusive, never silently accepted.
                # Missing data, size changes and magenta remain failures even there.
                if dynamic and not metrics["new_magenta_pixels"]:
                    row.update(verdict="REVIEW", reason=dynamic["reason"] + "; " + row["reason"])
        except (OSError, ValueError) as error:
            row.update(verdict="FAIL", reason=str(error))
        rows.append(row)
    counts = Counter(r["verdict"] for r in rows)
    verdict = "FAIL" if evidence or counts["FAIL"] else "REVIEW" if counts["REVIEW"] else "PASS"
    result = dict(verdict=verdict, counts={key: counts[key] for key in ("PASS", "FAIL", "REVIEW")},
                  evidence_failures=evidence, limits=limits, captures=rows)
    return result


def write_report(root, result):
    output = root / "validation"
    output.mkdir(parents=True, exist_ok=True)
    (output / "results.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    fields = list(dict.fromkeys(key for row in result["captures"] for key in row))
    with (root / "validation-matrix.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(result["captures"])
    e = html.escape
    body = []
    for row in sorted(result["captures"], key=lambda r: (r["verdict"] == "PASS", r["image"])):
        key = row["image"]
        body.append(f'<tr class="{row["verdict"]}"><td>{row["verdict"]}</td>'
                    f'<td><a href="../index.html#{e(key)}">{e(key)}</a></td><td>{e(row["reason"])}</td></tr>')
    page = '<!doctype html><meta charset="utf-8"><title>Automatic visual validation</title>'
    page += '<style>body{background:#14181c;color:#eee;font:15px system-ui;margin:24px}a{color:#ddd}'
    page += 'td{padding:8px;border-bottom:1px solid #444}.PASS td:first-child{color:#8d9}'
    page += '.FAIL td:first-child{color:#f88}.REVIEW td:first-child{color:#fc8}</style>'
    page += f'<h1>{result["verdict"]}</h1><p>{e(str(result["counts"]))}</p>'
    page += '<p>Fixed full-image and overlapping local-tile checks. No masking or alignment. '
    page += 'REVIEW is inconclusive and blocks the gate; FAIL flags a changed image or invalid evidence, '
    page += 'not a proven gameplay bug. PASS is numerical equivalence within budgets, not design approval.</p>'
    page += ''.join(f'<p>{e(message)}</p>' for message in result["evidence_failures"])
    page += '<table><tr><th>Result</th><th>Capture</th><th>Reason</th></tr>' + ''.join(body) + '</table>'
    (output / "index.html").write_text(page, encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    parser.add_argument("--contract", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = validate(args.root.resolve(), json.loads(args.contract.read_text(encoding="utf-8-sig")))
        write_report(args.root.resolve(), result)
    except (OSError, ValueError, KeyError) as error:
        parser.exit(1, f"Invalid validation input: {error}\n")
    print(json.dumps({key: result[key] for key in ("verdict", "counts", "evidence_failures")}))
    raise SystemExit({"PASS": 0, "FAIL": 1, "REVIEW": 2}[result["verdict"]])
