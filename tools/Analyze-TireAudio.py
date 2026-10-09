"""Measure audition WAVs; spectral metrics support tuning, not a substitute for listening.

Requires NumPy (available in the configured bundled workspace Python runtime).
Usage: python tools/Analyze-TireAudio.py reports/tire-sound-refinement [--check]
Generate current auditions with Invoke-Unity.ps1 -Action TireSoundVerify.
Optional local before/scrub/screech-gritty WAVs add comparison rows; they are not required fixtures.
"""
import csv
import sys
import wave
from pathlib import Path

import numpy as np


def analyze(path):
    with wave.open(str(path), "rb") as stream:
        assert stream.getsampwidth() == 2 and stream.getnchannels() == 1
        rate = stream.getframerate()
        audio = np.frombuffer(stream.readframes(stream.getnframes()), dtype="<i2").astype(float) / 32768
    # Exclude fade-in; do not treat a short chirp's silent tail as sustained sound.
    clip = audio[int(rate * .25):int(rate * 3.75)]
    size = 8192
    window = np.hanning(size)
    spectra = [np.abs(np.fft.rfft(clip[start:start+size] * window))**2
               for start in range(0, len(clip)-size+1, size//2)]
    power = np.mean(spectra, axis=0)
    frequencies = np.fft.rfftfreq(size, 1/rate)
    power[frequencies < 50] = 0
    total = power.sum()
    peak = power.argmax()
    # 27-Hz window identifies sustained tonal dominance; broad friction remains distributed.
    band = np.abs(frequencies-frequencies[peak]) < 13.5
    return [np.sqrt(np.mean(audio**2)), np.abs(audio).max(),
            frequencies[peak], (power*frequencies).sum()/total,
            power[frequencies > 2000].sum()/total, power[band].sum()/total,
            power[(frequencies >= 1000) & (frequencies <= 3500)].sum()/total,
            power[frequencies > 6000].sum()/total]


folder = Path(sys.argv[1])
rows = []
for revision in ("before", "scrub", "screech-gritty", "after"):
    for scenario in ("stationary-burnout", "acceleration-slip", "locked-braking", "shift-chirp"):
        path = folder / f"{revision}-{scenario}.wav"
        if revision != "after" and not path.exists():
            continue
        rows.append([revision, scenario, *analyze(path)])
with (folder/"audio-matrix.csv").open("w", encoding="utf-8", newline="") as stream:
    writer = csv.writer(stream)
    writer.writerow(["revision", "scenario", "rms", "peak", "spectral_peak_hz", "centroid_hz", "power_above_2khz_fraction", "strongest_27hz_fraction", "screech_1_to_3_5khz_fraction", "hiss_above_6khz_fraction"])
    writer.writerows(rows)
for row in rows:
    print(row[0], row[1], "/ RMS %.4f / screech band %.2f%% / >6k hiss %.2f%% / narrow band %.2f%%" % (row[2],100*row[8],100*row[9],100*row[7]))
if "--check" in sys.argv[2:]:
    for row in rows:
        if row[0] != "after":
            continue
        assert row[8] > .55, f"Recognizable screech band missing: {row[1]}"
        assert row[9] < .035, f"Upper-frequency hiss budget exceeded: {row[1]}"
        assert row[7] < .25, f"Single narrow-band dominance exceeded: {row[1]}"
    print("PASS: 12 screech-presence/hiss/narrow-band checks")
