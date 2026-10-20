# Five Bison chambered-exhaust WAV versions — 2026-10-20

The user described the shared-generator Bison as an air burst and requested five
generator versions with the same complete limiter/backfire sequence, delivered
directly as WAVs. No HTML/page update is requested or generated in this checkpoint.
No sound direction is approved for the game; runtime integration and AP-00 remain held.

All versions use one common source implementation and the same Bison engine bed,
chambered exhaust, geometry, source locations, event packets, seeds and timeline
from the preceding 64-second controlled audition. Only numerical source calibration
changes. More weight is placed on coherent pressure/rarefaction and less on the
turbulent noise tail. An optional smoothed pressure-rise term adds an acoustic front;
it uses the pressure-envelope derivative, normalized to burn duration, rather than
another noise blast, pitched oscillator or one-sample discontinuity.

| File | Direction | Pressure coupling | Turbulence coupling | Front coupling | Front smoothing | Expansion scale |
| --- | --- | --- | --- | --- | --- | --- |
| 01-pressure-pop.wav | Pressure pop | 3.2 | 0.12 | 0.9 | 1.6 ms | 1.0 |
| 02-heavy-bang.wav | Heavier bang | 5.0 | 0.10 | 1.5 | 2.3 ms | 1.15 |
| 03-deep-boom.wav | Deeper pressure boom | 7.0 | 0.08 | 0.65 | 4.0 ms | 1.6 |
| 04-raspy-bang.wav | Sharper, rougher bang | 4.5 | 0.38 | 2.0 | 0.85 ms | 1.1 |
| 05-rolling-thump.wav | Longer pressure thump | 5.5 | 0.22 | 1.1 | 3.0 ms | 2.1 |

These coefficients are listening calibration, not measured sound-pressure levels
or distinct combustion simulations. Short cuts and longer afterfires still share
one numerical algorithm; their existing burn/mixing/energy inputs differ. New
calibration is captured in an isolated copy when the source is prepared. It is not
applied by cause name, car identity or a shipping setting.

The unconfigured constructor retains its previous sample expression and default
settings. A private `reference-default.wav` is regenerated and checked byte for
byte against the prior Bison WAV. It is a test control, not a sixth proposed sound.
The old 34-recording matrix and review page are left unchanged. No production DSP,
assets, physical event generation, saves, preferences or builds change.

Reproduce after the original controlled Bison reference exists:

```powershell
unity run Game --timeout 900 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.SharedBackfireAudition.BisonVersions
python tools/Verify-BisonBackfireVersions.py
```

Each proposed WAV contains the entire same 64-second, sixteen-chapter sequence:
idle, revs, hard/soft fuel and ignition limiting, ignition/fuel-cut two-step, shifts,
small/medium/large afterfires, silencer-location comparisons, applicable anti-lag,
fault combustion, intake backfire and oxygen-starved control. The NA Bison has no
turbo anti-lag combustion. Engine sound remains present in the control chapters.
There is no normalization or unrelated engine gain/pitch/EQ change.

Checks and measured peaks are recorded in `verification.txt`: source/routing tests,
explicit-default equivalence, deterministic pressure-front response, no sample-loop
allocation, five complete sequences, unchanged event packets and engine-only prefix,
zero voice overflow, no-explosion controls, source-reference byte equality, distinct
hashes and bounded PCM. These checks do not establish the user's acoustic preference
or Android realtime performance. WAVs/raw manifests/logs stay local; source, checker
and concise evidence are versioned. Stop for listening review.

The pre-existing changes in the three supply/market/SQLite verification reports,
attachments, Tests.ps1 and the unrelated race-exit/shop-responsive reports remain
outside this commit. No worktree, branch switch, push or user-player interruption.
