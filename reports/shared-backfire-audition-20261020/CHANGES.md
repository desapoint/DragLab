# Shared backfire generator — controlled listening checkpoint

Requested on 2026-10-20: one shared parameter-driven generator, multiple backfire
types and engine configurations in a controlled flow, one combined engine/event
recording per configuration, and user listening approval before runtime use.

## Scope

The new source, routing network, renderer and checks live under `Editor/`. There is
no runtime caller. Production `EngineSoundSynth`, `ExhaustPopSynth`, event creation,
vehicle assets, wear/fault logic, input, preferences, saves and builds are unchanged.
The previous Bison experiments remain available as historical listening references.
The architecture plan/AP-00 is not advanced by this acoustic preview checkpoint.

`RaceAudioAuditionVerification.AcousticHardware` exposes copies of the existing
versioned hardware definitions without running the old audition or preparing tire
fixtures. Normal `Cases()` still prepares tires by default. The new review does not
depend on ignored previously generated fixtures and does not overwrite the old
47-case manifest/results.

## One generator

`SharedBackfirePreviewSynth` accepts only numerical combustion inputs: fuel and
oxygen masses, heating value, stoichiometric oxygen requirement, burn efficiency,
burn/mixing times, local volume, temperature, pressure, location, route and seed.
No limiter/overrun/anti-lag/big-bang name or type switch reaches its source code.
Available fuel/oxygen limits energy; no fuel, oxygen or ignition yields no source.
The same pressure/rarefaction and filtered turbulent-release algorithm covers short
cracks and long rolling burns. Forty-eight prepared voices permit overlapping
events without abruptly restarting a preceding tail. Capacity rejection is counted;
the review matrix must lose zero voices.

Prepared one-dimensional duct segments carry pressure in both directions with
travel delay, area/temperature-dependent impedance changes, losses, outlet
reflection/radiation and component transfers. Turbine transmission has a smoothed
boost-dependent acoustic response. Actual configured catalysts, resonators and
silencers are retained with the existing upgrade level. An optional explicit leak
case permits local shell radiation/loss; healthy parts do not acquire a breakage
sound. Intake events use a separate intake path through the same source/network
implementation.

The controlled exhaust source coordinates are prepared as exact junctions before
rendering, splitting effective component transfer over the resulting lengths.
Arbitrary coordinates supported by the generic prototype interpolate between
prepared nodes; a production geometry contract still needs review. Absolute pipe
diameter also affects the calibrated aperture-radiation response, avoiding an
otherwise scale-invariant impedance-ratio comparison.

## Controlled flow

Every WAV is 64 seconds, 48 kHz stereo PCM16. It contains the existing engine and
induction synthesizer with its old pop layer disabled on a private specification
copy. The new source is combined before the existing form of output saturation.
No whole-file normalization, pitch/EQ replacement, rival voice, tire layer, spoken
cue or notification chime is added. Chapters and their control notes are in the
review page and machine-readable manifest.

| Time | Chapter |
| --- | --- |
| 0–4 | Idle baseline |
| 4–8 | Throttle blips |
| 8–12 | Hard then soft fuel limiting; no explosion fuel |
| 12–16 | Hard then partial soft ignition cuts |
| 16–20 | Ignition two-step, then fuel-cut two-step control |
| 20–24 | Loaded acceleration and two shifts |
| 24–28 | Small overrun combustion |
| 28–32 | Medium afterfire |
| 32–36 | Larger upstream afterfire |
| 36–40 | Same numerical event and seed, within silencer |
| 40–44 | Same numerical event and seed, downstream |
| 44–48 | Turbo anti-lag/manifold events, where applicable |
| 48–52 | Explicit delayed-fuel/misfire fault |
| 52–56 | Intake backfire fault, where applicable |
| 56–60 | Oxygen-starved no-explosion control |
| 60–64 | Return to idle with natural tail decay |

When no silencer is fitted, the corresponding source-location chapters describe
mid/rear pipe instead. Diesel does not receive petrol ignition-cut/intake/ordinary
overrun events. Its deliberately forced hot exhaust-fueling fault is labelled as
an experimental fault, not normal operation. Non-turbo configurations have no
anti-lag explosions. Suppressed controls still retain the engine sound.

The 34 configurations cover 3/4/5/6/8/10/12 cylinders; petrol injection,
carburetion and diesel; NA/turbo/twin turbo/supercharger/twin supercharger/
twincharged; all four existing release-valve modes for turbo/twin turbo/
twincharged; four Bison exhausts; shorter/longer/narrower/wider pipes, cooler/hotter
paths, catalyst/resonator hardware and an explicit silencer leak. Additional
configurations are comparisons of actual inputs, not separate source algorithms.

## Review and reproduction

```powershell
unity run Game --timeout 2400 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.SharedBackfireAudition.Run
python tools/Build-SharedBackfireReview.py
```

The local `index.html` supplies one player, filterable configuration selection,
chapter seeking, optional continuous playback and direct WAV download. Switching
configurations can retain the current position for comparisons. The manifest
records all explicit input packets, controls, output hashes and chapter energies.
Raw WAV/JSON/HTML/log files stay local; renderer/checker/metadata and concise
evidence/matrix are versioned. There is one WAV per configuration.

See `verification.txt` for checks actually completed. Relevant gates cover seeded
determinism, fuel/oxygen gating, overlap, source routing, component and geometry
effects, 22.05/44.1/48/96 kHz preparation, zero sample-loop allocations, bounded
PCM, engine continuity, matched source-position parameters and parameter effects.
Desktop timing excludes file writes and is not Android profiling. The initial
first-arrival assertion was corrected to end before a separate propagation path
arrived; the final checks retain a separate reflected-response assertion.

Completed gates: 78 source/routing checks; 138 full-matrix checks; all 34 WAV
headers/hashes, chapter engine continuity, event-energy/control rules, identical
source-comparison inputs/seeds and parameter differences independently checked.
The original input-receipt export missed the serializable-struct annotation; that
was corrected and JSON packet round-trip rendering was added to the core gate.
`ExportInputs` verified each recorded identity, time and energy before filling the
manifest, without rewriting PCM; all resulting WAV hashes were rechecked. The full
render gate is retained because the serialization fix did not change source DSP.

All files have unclipped output (largest mixed peak 0.373434); the 34 PCM files total
398.4 MiB. The prepared source/network sample loop allocates no managed memory.
Measured source/network processing, excluding PCM writes and the engine bed,
ranges from 68.1 to 122.4 ms per audio second on this desktop Editor. These are
prototype measurements, not a physical Android acceptance result or a frame-time
guarantee. Node JavaScript syntax and static HTML ID/player/reference checks pass.
The app accepted a queued browser-open request, but browser automation refused
`file:` page inspection under its URL policy; rendered layout, clicks and playback
were not automatically verified. No alternate browser/protocol workaround was used.

## Limits and acceptance

These are deliberately authored laboratory frames and combustion events, not an
automatic race-event prediction model. Fuel chemistry values, duct geometry, gas
conditions, burn/mixing parameters, radiation calibration and losses are explicit
approximations. The network is not full nonlinear reactive-flow/CFD simulation.
Turbine acoustics do not replace the game's turbo simulation. Component condition
is not converted into fictional damage beyond the labelled leak input.

The real simulation still needs approved event-cause/location/fuel/oxygen metadata
before production integration; nothing in the prototype makes every engine emit
every event in normal play. Human listening approval, realtime integration,
physical Android CPU/listening checks and new platform builds remain pending.
The controlled bench demonstrates hard/soft cuts even where the original donor
car would need different controller hardware; this does not grant that hardware
or those settings to the player.

Unrelated existing changes are preserved: `reports/new-parts-supply/verification.txt`,
`reports/parts-market/verification.txt`, `reports/sqlite-state/verification.txt`,
`.codex-remote-attachments/`, `Tests.ps1`, `reports/race-exit-regression/` and
`reports/shop-responsive-20261019/`. No worktree or branch switch was used.
