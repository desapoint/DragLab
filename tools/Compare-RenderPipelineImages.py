"""Compare matching Unity captures using Pillow and NumPy; never align away layout changes."""
import argparse
import csv
import hashlib
import html
import json
import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageEnhance, ImageDraw


def compare(root: Path):
    before = {p.relative_to(root / "before").as_posix(): p for p in (root / "before").rglob("*.png")}
    after = {p.relative_to(root / "after").as_posix(): p for p in (root / "after").rglob("*.png")}
    output = root / "comparison"
    output.mkdir(parents=True, exist_ok=True)
    suites = []
    suite_names = {p.parent.name for phase in ("before", "after") for p in (root / phase).glob("*/status.txt")}
    for name in sorted(suite_names):
        suite = {"suite": name}
        for phase in ("before", "after"):
            folder = root / phase / name
            status = folder / "status.txt"
            suite[phase + "_status"] = status.read_text(encoding="utf-8-sig").strip() if status.exists() else "missing"
            suite[phase + "_images"] = len(list(folder.rglob("*.png")))
            report = next((folder / n for n in ("verification.txt", "runtime-verification.txt") if (folder / n).exists()), None)
            content = report.read_text(encoding="utf-8-sig") if report else ""
            checks = re.search(r"Checks: (\d+)", content)
            suite[phase + "_checks"] = int(checks[1]) if checks else len(re.findall(r"^PASS:", content, re.M))
            suite[phase + "_reported_errors"] = sum(map(int, re.findall(r"runtime errors: (\d+)", content, re.I)))
            if re.search(r"^FAIL:", content, re.M):
                suite[phase + "_status"] = "FAIL in report"
        suites.append(suite)
    if suites:
        with (root / "coverage-matrix.csv").open("w", encoding="utf-8", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=list(suites[0]))
            writer.writeheader()
            writer.writerows(suites)
    rows = []
    for key in sorted(before.keys() | after.keys()):
        row = {"image": key, "status": "missing before" if key not in before else "missing after" if key not in after else "paired"}
        if row["status"] == "paired":
            a, b = Image.open(before[key]).convert("RGB"), Image.open(after[key]).convert("RGB")
            row.update(width=a.width, height=a.height)
            if a.size != b.size:
                row["status"] = "size mismatch"
            else:
                aa, bb = np.asarray(a, dtype=np.int16), np.asarray(b, dtype=np.int16)
                delta = np.abs(aa - bb)
                mask = delta.max(axis=2) > 8
                row.update(changed_percent=round(float(mask.mean() * 100), 5),
                           mean_channel_error=round(float(delta.mean()), 5), max_channel_error=int(delta.max()))
                # Pink must be reviewed, not automatically called a broken shader: the UI intentionally uses red.
                pink_a = (aa[:, :, 0] > 220) & (aa[:, :, 1] < 50) & (aa[:, :, 2] > 220)
                pink_b = (bb[:, :, 0] > 220) & (bb[:, :, 1] < 50) & (bb[:, :, 2] > 220)
                row["new_magenta_pixels"] = int((pink_b & ~pink_a).sum())
                ys, xs = np.where(mask)
                row["changed_bounds"] = "" if not len(xs) else f"{xs.min()},{ys.min()},{xs.max()+1},{ys.max()+1}"
                for label, box in {"header": (0, 0, a.width, min(66, a.height)),
                                   "content": (0, min(66, a.height), a.width, max(66, a.height - 32)),
                                   "footer": (0, max(0, a.height - 32), a.width, a.height)}.items():
                    x0, y0, x1, y1 = box
                    region = delta[y0:y1, x0:x1]
                    row[label + "_mean_error"] = round(float(region.mean()), 5) if region.size else None
                dest = output / key
                dest.parent.mkdir(parents=True, exist_ok=True)
                Image.blend(a, b, .5).save(dest.with_name(dest.stem + "-overlay.png"))
                ImageEnhance.Brightness(ImageChops.difference(a, b)).enhance(4).save(dest.with_name(dest.stem + "-difference.png"))
                row["overlay"] = dest.with_name(dest.stem + "-overlay.png").relative_to(root).as_posix()
                row["difference"] = dest.with_name(dest.stem + "-difference.png").relative_to(root).as_posix()
        rows.append(row)
    fields = list(dict.fromkeys(k for row in rows for k in row))
    with (output / "image-matrix.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)
    (root / "image-matrix.csv").write_bytes((output / "image-matrix.csv").read_bytes())
    (output / "image-matrix.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")
    paired = [r for r in rows if r["status"] == "paired"]
    worst = sorted(paired, key=lambda r: r["mean_channel_error"], reverse=True)[:40]
    sheet = Image.new("RGB", (1000, ((len(worst) + 3) // 4) * 170), "#14181c")
    draw = ImageDraw.Draw(sheet)
    for i, row in enumerate(worst):
        shot = Image.open(after[row["image"]]).convert("RGB")
        shot.thumbnail((244, 138))
        x, y = (i % 4) * 250, (i // 4) * 170
        sheet.paste(shot, (x, y))
        draw.text((x + 2, y + 139), row["image"][:38], fill="white")
        draw.text((x + 2, y + 153), f'{row["changed_percent"]:.2f}% / MAE {row["mean_channel_error"]:.3f}', fill="#bbbbbb")
    if worst:
        sheet.save(output / "largest-differences.jpg", quality=90)
    validation_path = root / "validation/results.json"
    validation = json.loads(validation_path.read_text(encoding="utf-8")) if validation_path.exists() else None
    verdicts = {r["image"]: r for r in validation["captures"]} if validation else {}
    viewer_rows = []
    stale = False
    for row in rows:
        checked = verdicts.get(row["image"], {})
        current = bool(checked) and all((root / phase / row["image"]).is_file() and
            hashlib.sha256((root / phase / row["image"]).read_bytes()).hexdigest() == checked.get(phase + "_sha256")
            for phase in ("before", "after"))
        stale |= validation is not None and not current
        viewer_rows.append(dict(row, automatic_validation=checked["verdict"] if current else "not run / stale",
                                validation_reason=checked.get("reason", "") if current else "rerun validation for these exact files"))
    banner = ('<p>Automatic validation: <a href="validation/index.html">'
              + html.escape(("STALE — rerun validation" if stale else validation["verdict"]) + " · " + str(validation["counts"]))
              + '</a>. REVIEW and FAIL both block automatic acceptance.</p>') if validation else ''
    data = json.dumps(viewer_rows).replace("</", "<\\/")
    viewer = '''<!doctype html><meta charset="utf-8"><title>DragLab pipeline comparison</title>
<style>body{margin:20px;background:#14181c;color:#eee;font:15px system-ui}select{max-width:85vw}img{max-width:100%;display:block}#stage{position:relative}#top{position:absolute;inset:0;opacity:.5}button,input,select{margin:6px}#detail{font-family:monospace}a{color:#eee}</style>
VALIDATION_BANNER<input id="search" placeholder="Find interface"><select id="pick"></select><button id="prev">Previous</button><button id="next">Next</button>
<div><button data-mode="before">Before</button><button data-mode="after">After</button><button data-mode="overlay">Overlay</button><button data-mode="difference">Difference ×4</button><label>After opacity <input id="alpha" type="range" min="0" max="1" step=".01" value=".5"></label></div>
<p id="detail"></p><div id="stage"><img id="base"><img id="top"></div><p>Matching filenames and dimensions. No resizing, registration, masking or color normalization is applied. Animated race values, hover shine and simulation traces can differ with frame timing. Inspect those separately from rendering regressions.</p>
<script>const rows=DATA;const pick=document.querySelector('#pick'),base=document.querySelector('#base'),top=document.querySelector('#top');let mode='overlay';rows.forEach((r,i)=>pick.add(new Option('['+r.automatic_validation+'] '+r.image+' — '+(r.changed_percent??r.status)+(r.changed_percent!==undefined?'%':''),i)));function url(p){return p.split('/').map(encodeURIComponent).join('/')}function show(){const r=rows[+pick.value];document.querySelector('#detail').textContent=r.status==='paired'?`${r.width} × ${r.height} · ${r.changed_percent}% changed pixels · mean channel difference ${r.mean_channel_error} / 255 · new magenta pixels ${r.new_magenta_pixels} · ${r.automatic_validation}: ${r.validation_reason}`:r.status;top.style.display='none';if(r.status!=='paired'){base.removeAttribute('src');return}base.src=url(mode==='difference'?r.difference:mode==='after'?'after/'+r.image:'before/'+r.image);if(mode==='overlay'){top.src=url('after/'+r.image);top.style.display='block';top.style.opacity=document.querySelector('#alpha').value}}pick.onchange=show;document.querySelector('#search').oninput=()=>{const term=document.querySelector('#search').value.toLowerCase();pick.replaceChildren();rows.forEach((r,i)=>{if((r.image+' '+r.automatic_validation).toLowerCase().includes(term))pick.add(new Option('['+r.automatic_validation+'] '+r.image+' — '+(r.changed_percent??r.status)+(r.changed_percent!==undefined?'%':''),i))});if(pick.options.length)show();else{base.removeAttribute('src');top.style.display='none';document.querySelector('#detail').textContent='No matches'}};document.querySelectorAll('[data-mode]').forEach(b=>b.onclick=()=>{mode=b.dataset.mode;show()});document.querySelector('#alpha').oninput=show;document.querySelector('#prev').onclick=()=>{if(pick.options.length){pick.selectedIndex=Math.max(0,pick.selectedIndex-1);show()}};document.querySelector('#next').onclick=()=>{if(pick.options.length){pick.selectedIndex=Math.min(pick.options.length-1,pick.selectedIndex+1);show()}};function jump(){const key=decodeURIComponent(location.hash.slice(1));const index=rows.findIndex(r=>r.image===key);if(index>=0){document.querySelector('#search').value='';document.querySelector('#search').oninput();pick.value=index}show()}window.onhashchange=jump;jump();</script>'''.replace("DATA", data).replace("VALIDATION_BANNER", banner)
    (root / "index.html").write_text(viewer, encoding="utf-8")
    summary = {"before": len(before), "after": len(after), "paired": len(paired),
               "unpaired_or_size_mismatch": len(rows) - len(paired),
               "exact": sum(r["max_channel_error"] == 0 for r in paired),
               "pairs_with_new_magenta": sum(r["new_magenta_pixels"] > 0 for r in paired)}
    summary["suites"] = len(suites)
    summary["failed_or_missing_suites"] = sum(any(r[p + "_status"] != "PASS" or r[p + "_reported_errors"] for p in ("before", "after")) for r in suites)
    (output / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary))
    if not paired:
        raise SystemExit("No matching captures")
    if summary["unpaired_or_size_mismatch"]:
        raise SystemExit("Missing/size-mismatched captures need review")
    if summary["failed_or_missing_suites"]:
        raise SystemExit("Failed/missing capture suites need review")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    compare(parser.parse_args().root.resolve())
