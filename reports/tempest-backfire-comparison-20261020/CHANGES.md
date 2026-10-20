# Actual Tempest reference plus five generator options — 2026-10-20

The user requested a different engine/exhaust combination that actually produces
many backfires, its unchanged in-game sound as reference, and five matched options.
The earlier WAV-only delivery requirement remains in force: no HTML/page update.
No sound is approved for production integration and AP-00 remains held.

## Actual physical input sequence

Capture the existing Tempest inline-four turbo launch-ignition test with a
free-flow pipe, main ignition-cut limiter, ignition-cut two-step and anti-lag.
These are isolated laboratory hardware/control choices; no owned car, catalogue
entry, preference or player resource is edited. The normal fixed-tick simulation
and existing test driver generate the quarter-mile run, shifts, throttle lift,
shutdown and all pop counters. No artificial event schedule or extra pop is added.

The run has **250 physical pop increments / 243 published audio triggers** in
**34.025 seconds**. Existing `EngineSoundController` coalesces simultaneous same-tick
increments; all alternatives deliberately follow its published triggers so the
comparison matches the current game. Preserving multiple events/source metadata
is a later integration concern, not silently changed in the reference.

`00-in-game-reference.wav` is copied from the untouched production renderer for
this configuration. A second exact input replay renders that production synth
again and checks every faded PCM16 sample against the reference. The reference is
not assembled from the new generator and has no changed engine/master mix.
The five options use the exact same player states/controller frames, real trigger
times and dry production engine bed, with only the old pop layer disabled on a
private specification copy. The rival and tires are silent, as in prior engine
auditions. Clip-edge fades match the original renderer; finishing the race does
not cut the running engine.

## More connected source, still an approximation

The old larger-event fixture spread its burn across roughly 65 ms, making a smooth
pressure push/noise tail easy to hear as air. Here rapid cuts use about 0.8–1.3 ms
burn constants and other real events use 2–4 ms, independently of the longer vent
and propagation decay. These are explicit acoustic packet assumptions, not measured
per-event burn times from the game.

One shared optional source path now releases fuel/oxygen-limited energy through
a normalized gamma heat-release pulse. Released heat raises a lumped gas-pressure
state; a geometry/temperature-dependent discharge-time proxy relaxes that pressure.
The radiating pressure-rise front is smoothed over multiple samples. Turbulence
follows pressure-driven discharge, rather than an independently sustained noise
envelope. A small, overdamped two-pole body response shares the pressure excitation;
it has no standalone pitched oscillator or resonant bell tail. The existing engine
combustion/mechanical-vibration bed is retained, not replaced or falsely described
as being driven by the new source.

The prepared bidirectional exhaust path retains pipe geometry, turbine damping,
hardware transfers, reflection and outlet response. A common fixed source coupling
of **4** is an explicit audition-level calibration relative to the existing engine
mix; it is the same for all five candidates. No whole-file normalization, engine
gain/pitch/EQ change or reference gain adjustment is applied. This calibration is
not a measured sound-pressure ratio.


The preview routing audit also corrected turbine subdivision: each split duct now
receives its fraction of turbine attenuation/filtering instead of applying a
whole turbine at every injection junction. The prepared path checks that the
fractions total one equivalent turbine. Non-cut exhaust events use an explicit
prepared downstream junction, avoiding the old no-silencer fallback that could
place the source inside the turbine. These fixes affect only the Editor preview
path; the production reference remains sample-identical.

| Option | Pressure | Turbulence | Front | Smoothing | Body | Discharge scale |
| --- | --- | --- | --- | --- | --- | --- |
| 01 Pressure pop | 1.8 | 0.10 | 0.9 | 0.65 ms | 0.10 | 1.0 |
| 02 Combustion bang | 3.0 | 0.08 | 1.25 | 0.85 ms | 0.15 | 1.2 |
| 03 Deep report | 4.0 | 0.05 | 0.55 | 1.5 ms | 0.20 | 1.8 |
| 04 Fast crack | 2.0 | 0.15 | 1.8 | 0.4 ms | 0.08 | 0.85 |
| 05 Long release | 3.0 | 0.20 | 1.0 | 1.1 ms | 0.15 | 2.0 |

All use the same mechanism and event packets, not separate event-type recipes.
Defaults leave the prior shared-source path unchanged. No production source code,
simulation, save, authored car asset or build is changed.

Important limits: the actual game supplies event strength and controller/vehicle
state, but does not supply trapped fuel/oxygen, ignition coordinates, local exhaust
gas pressure/temperature or burn duration. Those packet values are inferred,
labelled laboratory inputs. The gas cell, discharge, normalized radiation and
structural response are reduced-order approximations, not reactive CFD, a validated
shock model, engine-block dynamics or pressure-to-combustion feedback. Released
heat accounting is checked; this does not establish a complete thermodynamic
energy balance. Human listening and Android realtime performance remain unverified.

The separation of heat-release excitation from acoustic propagation is supported
by [COMSOL's flame-model description](https://doc.comsol.com/6.3/doc/com.comsol.help.aco/aco_ug_pressure.05.015.html)
and [its transient pressure-acoustics equations](https://doc.comsol.com/6.4/doc/com.comsol.help.aco/aco_ug_pressure.05.168.html).
Our coefficients and reduced-order gas/body proxies are not validated fits to
those models. No measured backfire sample is used.

## Checks and reproduction

```powershell
unity run Game --timeout 1200 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.TempestBackfireComparison.Run --draglab-audio-wav
python tools/Verify-TempestBackfireComparison.py
```

The core checks cover seeded determinism, finite output, energy/input suppression,
heat release integrating to available fuel/oxygen energy, coupled pressure/noise/
body responses, prepared sample rates, source/component routing and zero sample-loop
allocation. The comparison checks every production-reference sample, all five
shared event counts, heat ledgers and absence of dropped voices. Independent PCM
checks verify reference bytes/hashes, equal durations, unchanged pre-pop engine
samples, distinct options, bounded samples, adequate numerical differences and
the absence of an HTML page. Counts/peaks are recorded in `verification.txt`.

The first render showed candidate differences too deeply masked by the engine;
only the common new-source audition coupling was adjusted before the final render.
The reference and dry engine bed retain their original levels. Previous Bison
recordings and the 34-case review page remain untouched. Source/tools/metadata and
concise evidence are tracked; raw WAVs, replay JSON, manifests and logs remain local.
Pre-existing unrelated supply/market/SQLite reports and local attachments/tools/
race-exit/shop-responsive artifacts remain outside this commit. No worktree or
branch change, push, or interruption of a user-running player.
