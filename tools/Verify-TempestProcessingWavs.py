"""Validate the native Tempest DSP audit exports, including chapter gaps/levels.

Requires numpy. Does not synthesize, EQ, normalize or modify any audio.
Run after DragLab.Editor.TempestProcessingAudition.Run has exited successfully.
"""
from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path
import struct
import sys

import numpy as np


def chunks(path: Path) -> dict:
    result = {}
    with path.open("rb") as f:
        header = f.read(12)
        assert header[:4] == b"RIFF" and header[8:] == b"WAVE", path
        assert struct.unpack_from("<I", header, 4)[0] + 8 == path.stat().st_size, path
        while f.tell() < path.stat().st_size:
            ident, size = struct.unpack("<4sI", f.read(8))
            offset = f.tell()
            if ident == b"fmt ":
                fmt = struct.unpack("<HHIIHH", f.read(16))
                assert fmt == (1, 2, 48000, 288000, 6, 24), (path, fmt)
            elif ident == b"data":
                result.update(offset=offset, bytes=size, frames=size // 6)
            elif ident == b"cue ":
                count = struct.unpack("<I", f.read(4))[0]
                result["cues"] = [struct.unpack("<II4sIII", f.read(24))[-1] for _ in range(count)]
            elif ident == b"LIST":
                labels = f.read(size)
                assert labels.startswith(b"adtl"), path
                result["labels"] = labels.count(b"labl")
            f.seek(offset + size + (size & 1))
    return result


def stats(f, offset: int, begin: int, frames: int) -> tuple[float, float]:
    f.seek(offset + begin * 6)
    energy, peak, samples = 0.0, 0.0, 0
    while frames:
        n = min(frames, 240000)
        b = np.frombuffer(f.read(n * 6), dtype=np.uint8).reshape(-1, 3)
        x = b[:, 0].astype(np.int32) | (b[:, 1].astype(np.int32) << 8) | (b[:, 2].astype(np.int32) << 16)
        x = ((x ^ 0x800000) - 0x800000).astype(np.float64) / 8388607.0
        peak = max(peak, float(np.max(np.abs(x))))
        energy += float(np.dot(x, x))
        samples += len(x)
        frames -= n
    return peak, float(np.sqrt(energy / samples))


def main() -> None:
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "reports/tempest-processing-20261021"
    lines, summary = [], []
    files = sorted(root.glob("*/*.wav"))
    assert len(files) == 50, f"Expected 50 WAVs, found {len(files)}"
    max_rms_error = 0.0
    for path in files:
        meta = chunks(path)
        internal = path.stem == "24-engine-internal-filters"
        quick = path.parent.name == "quick"
        table_name = "24-engine-internal-filters-passes.csv" if internal else path.stem.replace("-launch-and-lift", "") + "-passes.csv"
        with (root / table_name).open(encoding="utf-8-sig", newline="") as f:
            table = list(csv.DictReader(f))
        count = 8 if internal else 22
        length = 6 * 48000 if quick else round((float(table[1]["start_seconds"]) - 1) * 48000)
        assert len(table) == count and meta["frames"] == count * length + (count - 1) * 48000, path
        assert meta["cues"] == [i * (length + 48000) for i in range(count)], path
        assert meta["labels"] == count and path.with_suffix(".cue").is_file(), path
        rms_values, peak = [], 0.0
        with path.open("rb") as f:
            for i, row in enumerate(table):
                begin = i * (length + 48000)
                p, rms = stats(f, meta["offset"], begin, length)
                assert p < .901, (path, i, p)
                peak = max(peak, p)
                rms_values.append(rms)
                if not internal and not quick:
                    gain = float(row["fixed_gain"] if path.parent.name == "fixed-level" else row["matched_gain"])
                    error = abs(rms - float(row["source_rms"]) * gain)
                    max_rms_error = max(error, max_rms_error)
                    assert error < 1e-7, (path, i, error)
                if i + 1 < count:
                    f.seek(meta["offset"] + (begin + length) * 6)
                    assert not any(f.read(48000 * 6)), (path, i, "gap is not exact digital silence")
        if path.parent.name == "level-matched" and max(rms_values) > 1e-10:
            assert max(rms_values) - min(rms_values) < 2e-7, (path, "whole-segment RMS mismatch")
        with path.open("rb") as f:
            digest = hashlib.file_digest(f, "sha256").hexdigest()
        summary.append({"file": str(path.relative_to(root)), "seconds": meta["frames"] / 48000, "chapters": count, "peak": peak, "sha256": digest})
        lines.append(f"PASS {path.relative_to(root)}: 48 kHz / 24-bit stereo; {count} exact chapter markers; silent gaps; no clipping; levels verified")
        print(lines[-1], flush=True)
    lines += [f"PASS {len(files)} WAVs; maximum quantized RMS discrepancy {max_rms_error:.9g}", "Checks: 50 file validations plus per-segment metadata, silence, peak and RMS assertions"]
    (root / "wav-verification.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    (root / "wav-manifest.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    link_guide(root)


def link_guide(root: Path) -> None:
    guide_path = root / "LISTENING_GUIDE.md"
    guide = guide_path.read_text(encoding="utf-8-sig")
    linked = []
    for line in guide.splitlines():
        if line.startswith("- ") and ".wav:" in line and "[fixed levels](" not in line:
            filename, description = line[2:].split(":", 1)
            line = f"- {filename}: [fixed levels](fixed-level/{filename}) / [level matched](level-matched/{filename}) —{description}"
        linked.append(line)
    guide = "\n".join(linked) + "\n"
    if "Export master/category gains" not in guide:
        guide += "\nExport master/category gains are fixed at 1, with compressor-noise control at 1. Stored user preferences are not loaded. These constant gain choices do not add another tone filter. The diagnostic files do not capture OS, speaker or driver processing. Chapter/session boundaries are edits with hard digital-silence gaps; judge source texture inside each repeated section.\n"
    if "Quick excerpts retain" not in guide:
        guide += "\nQuick excerpts retain the whole-run matching gains. Use the full level-matched recordings for strict equal-RMS comparisons over the whole scenario.\n"
    guide_path.write_text(guide, encoding="utf-8")


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--guide-only":
        link_guide(Path(sys.argv[2]))
    else:
        main()
