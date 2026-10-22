"""Compare exported PCM, never synthesize or alter the reference recordings."""
from pathlib import Path
import csv
import json
import sys
import numpy as np
from scipy.io import wavfile
from scipy.signal import spectrogram
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2] / "reports/unity-audio-reference-20261021"
rows = []
for trace_file in sorted(root.glob("*.audio-trace.json")):
    trace = json.loads(trace_file.read_text(encoding="utf-8-sig"))
    case = trace["id"]
    signals = []
    for suffix in (".unity.wav", ".browser.wav"):
        rate, pcm = wavfile.read(root / (case + suffix))
        assert rate == trace["sampleRate"] and pcm.shape == (trace["totalSamples"], 2)
        assert pcm.dtype == np.int32  # scipy reads 24-bit PCM left aligned
        signal = pcm.astype(np.float64) / 2147483648
        assert np.isfinite(signal).all()
        signals.append(signal)
    fig, axes = plt.subplots(3, 2, figsize=(15, 10), constrained_layout=True)
    labels = ("Unity production DSP", "Browser DSP · recorded Unity timeline")
    for col, (label, signal) in enumerate(zip(labels, signals)):
        mono = signal.mean(axis=1)
        peak, rms = float(np.abs(signal).max()), float(np.sqrt(np.mean(signal ** 2)))
        window = rate // 50
        n = len(signal) // window
        sections = signal[:n * window].reshape(n, window, 2)
        peaks = np.max(np.abs(sections), axis=(1, 2))
        time = np.arange(n) / 50
        axes[0, col].fill_between(time, -peaks, peaks, color="#476a9c", alpha=.75)
        axes[0, col].set(title=label, ylabel="Stereo peak envelope", ylim=(-1, 1), xlim=(0, len(signal) / rate))
        level = 20 * np.log10(np.maximum(1e-7, np.sqrt(np.mean(sections ** 2, axis=(1, 2)))))
        axes[1, col].plot(time, level, color="#ac3446", lw=1)
        axes[1, col].set(ylabel="20 ms RMS (dBFS)", ylim=(-70, 0), xlim=(0, len(signal) / rate))
        hz, sec, power = spectrogram(mono, rate, nperseg=1024, noverlap=768, scaling="spectrum", mode="magnitude")
        picture = axes[2, col].pcolormesh(sec, hz, 20 * np.log10(np.maximum(1e-6, power)), vmin=-100, vmax=-10, shading="auto", cmap="magma")
        axes[2, col].set(yscale="log", ylim=(40, 16000), ylabel="Frequency (Hz)", xlabel="Audio time (seconds)")
        axes[0, col].text(.02, .94, f"Peak {peak:.4f} · RMS {rms:.5f}", transform=axes[0, col].transAxes, va="top")
        rows.append({"case": case, "renderer": label, "seconds": len(signal) / rate, "peak": peak, "rms": rms, "rms_dbfs": 20 * np.log10(max(1e-12, rms)), "reactions": len(trace["reactions"]), "releases": len(trace["releases"])})
    fig.colorbar(picture, ax=axes[2, :].tolist(), label="Spectral level (dBFS; same scale)")
    fig.suptitle(trace["title"] + "\nSame inputs and event schedule; no level matching. Browser acoustic equivalence is not claimed.", fontsize=13)
    fig.savefig(root / (case + ".comparison.png"), dpi=140)
    plt.close(fig)
with (root / "comparison-matrix.csv").open("w", encoding="utf-8", newline="") as file:
    writer = csv.DictWriter(file, fieldnames=rows[0].keys())
    writer.writeheader()
    writer.writerows(rows)
print(f"Compared {len(rows) // 2} complete pairs; waveform envelopes, RMS and common-scale spectrograms saved.")
