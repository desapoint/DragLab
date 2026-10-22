"""Audit a fresh fixed-tuning audition against its retained pre-stat baseline.

Observes traces/PCM only. Does not change tuning, simulation, or game audio.
"""
from pathlib import Path
import csv
import hashlib
import html
import json
import math
import sys
import wave

import numpy as np
from scipy.io import wavfile
from scipy.signal import welch
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / "reports/audio-corrected-stats-20261021"
OLD = ROOT / "reports/audio-signal-diagnostics-20261021"
OLD_REFERENCE = ROOT / "reports/unity-audio-reference-20261021"
CASES = ("03-i4-turbo-hiss", "08-v8-carb")
checks, contracts, signals, measurements, sections = [], [], [], [], []


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check(value, label):
    if not value:
        raise AssertionError(label)
    checks.append("PASS " + label)


def write_csv(name, rows):
    with (OUT / name).open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=rows[0].keys())
        writer.writeheader()
        writer.writerows(rows)


def raw_ab(first, second, target):
    with wave.open(str(first), "rb") as a, wave.open(str(second), "rb") as b:
        check(a.getparams()[:3] == b.getparams()[:3] == (2, 3, 48000), target.name + " synchronized PCM24 stereo")
        data_a, data_b = a.readframes(a.getnframes()), b.readframes(b.getnframes())
    gap = bytes(48000 * 6)
    with wave.open(str(target), "wb") as result:
        result.setnchannels(2)
        result.setsampwidth(3)
        result.setframerate(48000)
        result.writeframes(data_a + gap + data_b)
    with wave.open(str(target), "rb") as result:
        check(result.readframes(result.getnframes()) == data_a + gap + data_b, target.name + " exact inputs then simulation PCM, one-second gap, no normalization")


for case in CASES:
    reference, diagnostic, modes = OUT / "reference", OUT / "diagnostics", OUT / "modes"
    previous, current = read(OLD_REFERENCE / (case + ".audio-trace.json")), read(reference / (case + ".audio-trace.json"))
    original_inputs, inputs = read(OLD_REFERENCE / (case + ".race.json")), read(reference / (case + ".race.json"))
    old_manifest, manifest = read(OLD / (case + ".diagnostic-manifest.json")), read(diagnostic / (case + ".diagnostic-manifest.json"))
    check(current["physicsModel"] == "draglab-variety-v22", case + " captured corrected v22 physics")
    check(inputs["ticks"] == original_inputs["ticks"], case + " original throttle, shifts, staging and tick clock retained")
    check(current["hardware"] == previous["hardware"] and current["tune"] == previous["tune"] and current["seeds"] == previous["seeds"], case + " Unity acoustic hardware, tuning and seeds unchanged")
    check(manifest["settings"] == old_manifest["settings"] and manifest["settingsSha256"] == old_manifest["settingsSha256"], case + " every browser acoustic setting and seed matches the original frozen audition")
    for suffix in ("unity-source.wav", "unity-engine.wav", "unity-tires.wav", "unity.wav"):
        unchanged = sha(reference / (case + "." + suffix)) == sha(OLD_REFERENCE / (case + "." + suffix))
        contracts.append(dict(case=case,artifact=suffix,identical=unchanged,sha256=sha(reference / (case + "." + suffix))))
        check(unchanged, case + " fresh " + suffix + " is byte-identical to pre-stat reference")
    physical_frames = lambda trace: [{k:v for k,v in f.items() if k!="stage"} for f in trace["frames"]]
    check(physical_frames(current) == physical_frames(previous) and current["reactions"] == previous["reactions"] and current["releases"] == previous["releases"], case + " every physical audio frame and reaction/release is unchanged (stage labels excluded)")
    check(all(f["stage"] for f in current["frames"]),case + " every current frame retains its stage label")
    for i, row in enumerate(manifest["rows"]):
        prior = old_manifest["rows"][i]
        identical = sha(diagnostic / manifest["items"][i]["filename"]) == sha(OLD / old_manifest["items"][i]["filename"])
        check(identical, case + " rerendered " + row["pass"] + " PCM matches fixed-tuning baseline")
        measurements.append(dict(case=case,stage=row["pass"],rms=row["rms"],peak=row["peak"],previous_stage_db=20*math.log10(row["rms"]/manifest["rows"][max(0,i-1)]["rms"]),pre_stat_rms=prior["rms"],identical_pcm=identical,fixed_settings_sha256=manifest["settingsSha256"]))
    timeline = read(diagnostic / (case + ".browser-controls.json"))
    frames = current["frames"][:current["physicsTicks"]+1]
    time = np.array([f["sample"] / 48000 for f in frames])
    index = np.searchsorted([f["sample"] for f in timeline["frames"]], [f["sample"] for f in frames], side="right") - 1
    browser = [timeline["frames"][i] for i in index]
    for stage in dict.fromkeys(f["stage"] for f in frames):
        selected = np.array([f["stage"] == stage for f in frames])
        for label, source in (("Unity", [f["engine"] for f in frames]), ("Browser inputs / Race drive", browser)):
            values = lambda key: np.array([float(f[key]) for f in source])[selected]
            signals.append(dict(case=case,stage=stage,source=label,seconds=selected.sum()/120,rpm_mean=values("rpm").mean(),rpm_p95=np.percentile(values("rpm"),95),pedal_mean=values("throttle").mean(),load_mean=values("load").mean(),boost_mean=values("boost").mean(),cut_percent=100*values("cut").mean(),combustion_fraction_mean=values("combustionFraction").mean() if label=="Unity" else "not published"))
    fig, axes = plt.subplots(4,1,figsize=(14,10),sharex=True,layout="constrained")
    for axis,key,label in zip(axes,("rpm","throttle","load","cut"),("RPM","Pedal / plate command","Published load","Full cut (0/1)")):
        axis.plot(time,[f["engine"][key] for f in frames],label="Unity simulation",color="#bf3445",lw=1)
        axis.plot(time,[f[key] for f in browser],label="Browser from Unity inputs",color="#267d9a",lw=1,alpha=.8)
        axis.set_ylabel(label)
        axis.grid(alpha=.2)
    axes[0].legend(loc="upper right")
    axes[-1].set_xlabel("Original input clock (seconds)")
    fig.suptitle(case + " — identical acoustic tuning; driving models differ (browser Race vs Unity staged/neutral)")
    fig.savefig(OUT / (case + ".signals.png"),dpi=140)
    plt.close(fig)
    # A measured brightness lead, not an auditory realism/perceptual score.
    for stage in ("events", "outdoor"):
        rate, pcm = wavfile.read(diagnostic / (case + ".signal-" + stage + ".wav"))
        mono = (pcm.astype(np.float64)/2147483648).mean(axis=1)
        hz,power = welch(mono,rate,nperseg=4096)
        signals.append(dict(case=case,stage="whole sequence "+stage,source="spectral measurement",seconds=len(mono)/rate,rpm_mean="",rpm_p95="",pedal_mean="",load_mean="",boost_mean="",cut_percent="",combustion_fraction_mean="",above_2khz_power_percent=100*power[hz>=2000].sum()/power.sum()))
    target = OUT / (case + ".inputs-then-simulation.wav")
    raw_ab(modes / (case + ".unity-inputs.wav"),modes / (case + ".unity-free-tuning.wav"),target)
    rev_rows = [r for r in signals if r["case"]==case and r["stage"]=="Neutral rev / limiter"]
    source_table = "".join(f"<tr><td>{html.escape(r['source'])}</td><td>{r['rpm_mean']:.0f}</td><td>{r['load_mean']:.2f}</td><td>{r['cut_percent']:.1f}%</td></tr>" for r in rev_rows)
    gains = "".join(f"<tr><td>{html.escape(r['pass'])}</td><td>{20*math.log10(r['rms']/manifest['rows'][max(0,i-1)]['rms']):+.2f} dB</td></tr>" for i,r in enumerate(manifest["rows"]))
    sections.append(f'''<section><h2>{html.escape(current['title'])}</h2>
<p><a href="diagnostics/{case}.html">Interactive fixed-tuning diagnostics: all 14 passes</a> · <a href="modes/{case}.html">Three-mode workbench</a></p>
<p>Unity inputs → one second silence → Unity simulation. Same original Free acoustic preset and seed; natural levels. Each includes its own tire model.</p>
<audio controls preload="none" src="{target.name}"></audio>
<p>Compare engine-only passes first for cleaner isolation. Browser Race drive does not reproduce Unity neutral/countdown. Hybrid passes measure mappings and interactions, not physically consistent alternative cars.</p>
<p>Over the same 5–9 s neutral-rev window, the models feed different excitation to the identical sound tuning:</p><table><tr><th>Source</th><th>Mean RPM</th><th>Mean load</th><th>Full-cut frames</th></tr>{source_table}</table>
<p>The Unity audio pedal can include automatic launch behaviour. Inputs mode deliberately replays only the original driver commands; it does not replay that launch assistance or neutral/countdown states.</p>
<p><a href="diagnostics/{case}.rev-pass-chain.wav">Neutral-rev / limiter window: all stages</a> · <a href="diagnostics/{case}.shift-pass-chain.wav">Acceleration / shifts window: all stages</a></p>
<img src="{case}.signals.png" alt="RPM, pedal, load and cut signals on the shared original input clock">
<details><summary>Measured adjacent-stage level changes (not a quality score)</summary><table><tr><th>Stage</th><th>RMS change</th></tr>{gains}</table></details></section>''')

for row in signals:
    row.setdefault("above_2khz_power_percent", "")
write_csv("baseline-contracts-matrix.csv",contracts)
write_csv("signal-source-matrix.csv",signals)
write_csv("measurement-matrix.csv",measurements)
(OUT / "comparison-verification.txt").write_text("\n".join(checks)+f"\nChecks: {len(checks)}\nLIMIT Objective equality and signal measurements; subjective sound quality/browser/device listening remains user review. No game audio or stats changed.\n",encoding="utf-8")
(OUT / "index.html").write_text('''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Corrected stats — fixed-tuning audio comparison</title>
<style>body{background:#111b29;color:#eaf3ff;font:16px system-ui;max-width:1100px;margin:30px auto;padding:0 18px}a{color:#5ad4ee}audio,img{width:100%}section{border-top:1px solid #4f5963;padding:20px 0}td,th{padding:5px 20px;text-align:left}p{line-height:1.55}</style>
<h1>Corrected stats: audio signal comparison</h1><p>Fresh Unity v22 captures, original driver inputs and frozen sound tuning. Tempest turbo and Bison V8 are sample-identical to their retained pre-stat references. These healthy stock fixtures do not exercise the capacity/part-rating defects that were corrected. All 28 diagnostic renditions also reproduce the earlier fixed-tuning PCM exactly. The remaining preference is not explained by those stat corrections.</p>
<p>Listen for the earliest unwanted change in the signal diagnostics, especially fraction → cuts, induction layers, and outdoor propagation. RMS changes identify leads; they cannot tell us which sound is realistic or preferred. No production audio, physics or packaged player was changed.</p>
''' + "".join(sections) + "</html>",encoding="utf-8")
print(f"PASS {len(checks)} comparison contracts; review {OUT / 'index.html'}")
