# Sharper Tempest backfire comparison — 2026-10-20

Requested listening revision only: sharper bangs, longer decay and less muffling,
with the exact prior production reference and event sequence. No game sound has
been integrated or approved. No HTML, build, player save or settings change.

## Preserved comparison

Replay the previous `tempest-turbo-open.race.json` rather than generating a new
race. Copy the prior production WAV unchanged and compare its SHA-256 and every
replayed production PCM sample. Compare all event times, flags and acoustic gas
packets to the prior manifest, including seed, fuel, oxygen, source position and
burn duration. The captured run remains 250 physical pops / 243 published triggers,
34.025 seconds, with the same dry production engine and common source coupling 4.
Previous WAVs and raw evidence remain intact in their original folder.

## Source and decay revision

The earlier thermal pulse and 0.4–1.5 ms attack smoothing strongly favoured lower
frequencies. New audition calibrations scale the internal heat-release time to
8–16% of each unchanged event packet's duration and smooth its front over only
0.04–0.15 ms. Fuel/oxygen energy and source locations do not change. Pressure rise
has more high-frequency content before the same turbine/hardware propagation;
no post-EQ or turbine bypass brightens the engine or reference.

Pressure-driven turbulence is reduced to 0.01–0.025 coupling so the tail is not
created by extending airflow hiss. A separate optional exterior-reflection network
receives the already-radiated pressure. Four unequal delay lines with an orthogonal
feedback matrix and damped high frequencies extend the decay without a new noise
source or a pitched ringing oscillator. Each feedback path is below unity; its
calibrated decay is 0.18–0.40 seconds. This is an assumed acoustic bloom for the
listening prototype, not a measured track impulse response or additional physical
fuel combustion. It changes the source acoustics, not the race event schedule.

| Option | Burn scale | Front smoothing | Reflection decay | Reflection mix |
| --- | --- | --- | --- | --- |
| 01 Pressure pop | 0.12 | 0.08 ms | 0.18 s | 0.40 |
| 02 Combustion bang | 0.10 | 0.10 ms | 0.24 s | 0.50 |
| 03 Deep report | 0.16 | 0.15 ms | 0.30 s | 0.55 |
| 04 Fast crack | 0.08 | 0.04 ms | 0.20 s | 0.45 |
| 05 Long release | 0.12 | 0.08 ms | 0.40 s | 0.65 |

Defaults preserve previous behavior. Reflection buffers and coefficients are
prepared in the constructor. Processing uses no new per-sample allocations.
Fuel/oxygen, gas and body-response assumptions retain the limitations documented
in the preceding comparison. This is not a reactive-flow or shock-wave solver.
Human listening approval and Android performance remain pending.

## Checks and reproduction

```powershell
unity run Game --timeout 1200 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.TempestBackfireComparison.RunSharper
python tools/Verify-TempestBackfireComparison.py reports/tempest-backfire-sharp-20261020
```

Requires the previous local raw fixture/reference. It fails if those are absent;
it never silently substitutes a different run. Core checks additionally compare
relative energy above 2 kHz, 100–300 ms tail energy, deterministic output, unchanged
arrival time and zero-input silence. A longer decay must not pre-echo the attack.
Reference/packet equality, exact production PCM parity, heat accounting, matched
WAV format/duration, distinct alternatives and dropped-voice/clipping checks must
pass before delivery. Actual results are recorded in `verification.txt`.

Unity CLI update check was attempted; its CDN hostname could not resolve, so the
installed CLI was used. No platform player build is needed for this Editor-only
listening checkpoint. Unrelated pre-existing reports/attachments/tools remain
untouched and excluded from staging. No branch/worktree change or push.


## Final checkpoint

Unity batch rendering passed all **89** shared-source checks. The five revisions
passed independent 48 kHz stereo PCM checks, with peaks of 0.601, 0.706, 0.534,
0.815 and 0.654 respectively (full scale 1). No hard clipping or dropped voice
was detected. The production reference matches exactly, and every event retains
its original timing/flags/seed and physical/acoustic inputs.

An initial whole-JSON-string equality gate rejected round-trip representation
changes. It was replaced with explicit field comparisons: booleans/IDs exact,
floating-point values within 1e-12 relative/absolute tolerance, far below one
sample of timing. The independent Python verifier performs the same nested
comparison. Reference WAV bytes and replayed production PCM still require exact
equality, with no tolerance. The final render and verifier passed after this
test-harness correction; no simulated event was moved to satisfy the gate.

The isolated revised source exceeds the preceding source's relative high-band
energy by at least 1.5x, and the reflected source retains at least 1.5x its direct
source's 100–300 ms energy. These are engineering checks, not a substitute for
listening. No auditory quality claim or Android profiling result is inferred.
