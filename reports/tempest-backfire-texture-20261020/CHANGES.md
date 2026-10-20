# Exhaust-wall texture on preferred versions 4 and 5 — 2026-10-20

The user selected the direction of sharper versions 4 and 5 and requested a
little exhaust crackle/resonance as the exhaust vibrates in response to pops.
This checkpoint is an Editor-only audition; no runtime integration is approved.
No HTML, save, player settings, platform build or architecture-plan work changes.

## Preserved input and comparison

Replay the same Tempest capture, unchanged engine bed and 250 physical pops /
243 published triggers over 34.025 seconds. Production reference bytes and every
replayed production PCM sample must match. Events/flags/packets retain the previous
field comparison gates (1e-12 floating-point JSON tolerance; IDs/flags exact).
No new explosion, random event schedule, worn/faulted exhaust or continuous noise
source is introduced. Prior WAVs and both preferred untextured baselines remain
intact. The original in-game reference accompanies the five new WAV candidates.

## Optional pressure-driven exhaust-wall proxy

A shared wall model receives a surface-weighted sum of the actual forward/reverse
pressures in the preview exhaust duct. Existing turbine, silencer, source-position
and reflection processing still determines these pressures. Weighting sums the
length-times-diameter surfaces so adding an injection split does not multiply the
excitation. This is a separate exterior shell-radiation path, not an unfiltered
copy of the tailpipe sound or a change to the dry engine.

Three broad damped bandpass modes have Q values 0.85, 1.15 and 1.35. Their nominal
frequencies vary with assembled exhaust length and mean diameter; the formula is
an assumed calibration, not measured steel eigenmodes, thickness, mounting or
material data. The mode response has a mild nonlinear flex residual; its filtered
high-frequency contribution gives a brief irregular crackly edge under pressure.
This does not simulate hanger impacts, broken parts, detailed contact mechanics or
additional fuel ignitions. There is no independent hiss/noise generator or pitched
bell oscillator. The response decays when the pressure excitation disappears and
feeds the same existing exterior reflection decay.

| Option | Preferred source | Shell coupling | Crackle coupling |
| --- | --- | --- | --- |
| 01 Light texture | Version 4 / fast crack | 0.12 | 0.18 |
| 02 Rich texture | Version 4 / fast crack | 0.24 | 0.40 |
| 03 Light texture | Version 5 / long release | 0.12 | 0.18 |
| 04 Rich texture | Version 5 / long release | 0.24 | 0.40 |
| 05 Warmer resonance | Version 5 / long release | 0.38 | 0.22 |

Every other voicing value matches its preferred baseline. Common source gain,
engine gain, event packets and times remain unchanged. New wall coupling defaults
to zero, retaining earlier outputs. The modes, coefficient arrays and surface
weights are prepared before rendering, with no per-sample allocation.

## Checks and reproduction

```powershell
unity run Game --timeout 1200 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.TempestBackfireComparison.RunExhaustTexture
python tools/Verify-TempestBackfireComparison.py reports/tempest-backfire-texture-20261020
```

Requires the preceding original local fixture/reference and sharper-version
manifest. Missing artifacts fail rather than substituting a different race.
Core checks verify active shell/crackle contributions, independent nonlinear
contribution, geometry response, determinism, finite output, zero-input silence,
decay and zero managed sample-loop allocations. Full-WAV checks retain production
reference/packet parity, heat accounting, matched duration/engine prefix, distinct
alternatives, no dropped voices and no PCM clipping. Only the new shell/crackle
parameters may differ from the chosen version-4/5 baseline.

The numerical distinct-option floor is 0.0001 RMS for this subtle texture audition,
compared with 0.004 in the earlier deliberately large attack comparison. This
prevents identical files passing without forcing a subtle added layer to the
level of a replacement combustion transient. Human audibility/quality approval is
still required. Android realtime cost is unprofiled; runtime audio remains intact.
The CLI update check failed DNS resolution; the installed CLI was used.

Actual results are recorded in `verification.txt`. Source, tools and concise
reports are versioned; generated audio, fixture JSON and logs stay local. Existing
unrelated reports and artifacts are preserved and excluded from staging. No branch
switch, worktree, push or production integration.


## Final results

Unity rendering passed all **97** source/routing checks. Independent WAV checks
passed the unchanged reference and captured events, exact production PCM parity,
all five preferred-baseline voicing comparisons and nonzero wall/crackle outputs.
Peaks are 0.822, 0.830, 0.659, 0.667 and 0.669 of full scale; no clipping or dropped
voice was detected. Additional PCM differences versus the chosen untextured
baseline are recorded in `verification.txt`. The core sample loop allocated zero
managed bytes; the desktop timing is diagnostic, not Android evidence. Listening
approval is pending, and the game still uses its existing audio implementation.
