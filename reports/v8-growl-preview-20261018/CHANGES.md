# Deeper 5.0 V8 audition — preview only

Current Bison 5.0 crossplane V8 compared with a deeper acoustic profile, using the actual production DSP extracted into a standalone offline renderer. No production sound code or authored vehicle asset is written. Firing frequency, cylinder order, RPM/load trace, exhaust hardware, organic random seed and duration are identical on both sides. These are scripted healthy-engine idle, neutral blips and a three-gear loaded pass/lift, not a recording of a simulated race.

The proposed deep profile uses bass 2.8, brightness 0.28, raw pulse 0.45 and engine drive 0.32. Reduced drive avoids creating extra bright harmonics by saturating the exhaust stage when reinforcing the low bank pulse. The balanced alternative uses 2.15 / 0.42 / 0.65 / 0.42. All values exist only in preview JSON.

Candidate A demonstrated that bass boost alone could add upper harmonics through saturation; retained locally, rejected. B is the balanced correction; C is the stronger deep proposal. User listening approval is pending. None is installed in the game or Android player.

## Verification

- Six finite bounded repeated renders for candidate C; private-seed re-renders are sample-identical.
- Matched RMS 0.12 per before/after clip; common peak headroom and 10 ms edge fades. Peaks are below 0.36, with no PCM clipping.
- Low-bank 40–250 Hz relative to 300–1000 Hz increases 2.18× for revving and 1.99× for the loaded pass. Loaded spectral centroid falls 106.1→99.1 Hz. This supports the tonal direction, not a claim of human listening quality.
- Five production DSP/asset hashes remain unchanged after preview generation. Audio randomness never touches gameplay state.
- No Unity scenes, tuning permissions, engine performance or runtime audio settings change.

Reproduce: `python tools/audio-audition-matrix/deep-v8-preview.py --variant deep --output reports/NEW-UNIQUE-FOLDER`. Use `--variant balanced` for B. A new output path is required to preserve earlier auditions.

Local review: `reports/v8-growl-preview-20261018-c/index.html`; combined loaded-pass clip: `pass-comparison.wav` (current, short silence, deeper). Idle and neutral rev clips are also included. Raw WAV/PCM, standalone build outputs and JSON are ignored; source tooling and this concise report are versioned.

## Latest review direction: neutral character

The user prefers the initial in-game character with slightly lower/throatier revs, and identifies the problem particularly during neutral revving. D retained more of the original midrange. The final **G** preview targets unloaded revving specifically; it supersedes the broad C profile as the current listening candidate.

Tracing the DSP corrects the initial load-only hypothesis: `voiceLoad` already follows throttle-weighted excitation, so neutral is not simply losing all bank voice. G uses actual `frame.load` to smoothly blend a modest neutral-only harmonic balance adjustment, compensating drive into the nonlinear exhaust stage. It retains all authored sound controls, the original bank irregularity, actual RPM/firing timing and cut gates. It does not add choppy modulation to imitate growl.

Eight bounded repeatable renders pass. Idle and the loaded pass are sample-identical to current production output. Neutral-blip low/upper energy ratio increases 1.28× and steady-neutral ratio 1.13×; centroids fall 81.3→76.8 Hz and 96.0→92.2 Hz. Matched RMS is 0.12, peaks below 0.43, with edge fades. Five production source/asset hashes remain unchanged. These metrics support a small timbre adjustment; the user decides whether it sounds better.

Review `reports/v8-growl-preview-20261018-g/index.html`, especially `rev-comparison.wav` (current → proposed) and `hold-comparison.wav` (steady 2500/4500 RPM, no limiter/cut events or second engine). The inputs remain scripted auditions rather than a recorded physical race. Run `--variant neutral-consistent` to reproduce into a new folder. No runtime audio change is authorized or applied; listening approval remains pending.
