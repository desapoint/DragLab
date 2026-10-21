# Pressure-led afterfire correction — 2026-10-21

## Requested checkpoint

The user hears large airy bursts under soft ignition limiting and less distinct
afterfires after the trackside revision. Correct the live source and relaunch
only the existing Tempest hard-ignition/two-step case for listening. Keep the
engine sound, car simulation and exhaust/induction hardware behavior intact.

## Cause and correction

`ReactivePressureBurst` previously treated continued flame reactions as mostly
noise: when pressure was steady or falling, 82% of each new reaction packet went
to its flow-texture reservoir. Its coherent output followed a slow smoothed
envelope. Installed exhaust filters and outdoor propagation further softened
that envelope, leaving the noisy release more prominent than the compression
edge. An overwritten per-sample `pending` decay did not shape an independent
release and has been removed.

The first edge-only correction passed its technical gates but was rejected by
the user for thin, high-pitched sound without a bang. Spectral inspection confirms
the missing body: much of that source's energy remained below 65 Hz, while its
audible edge/ringing was exposed. Passing delivery/headroom tests was not sound
acceptance; that intermediate source is not the final implementation.

The revised source separates acoustic compression/rarefaction release from the
longer chemical burn. Actual renewed overpressure funds the compression front;
remaining reaction energy funds a dense, variance-normalized 65–1,400 Hz
combustion body, with a longer finite decay and subordinate high grit/flow.
This restores audible low/mid blast energy instead of an isolated tick or a
subsonic envelope. Seeded irregularity has no pitched carrier. Rise, overpressure,
burn duration and fitted decay/sharpness still affect the response. Steady
burning retains rough body without a new bang at each numerical tick; genuinely
renewed pressure can produce another larger front. Simultaneous packet splitting
preserves source funding and output.

The user then heard the restored body but found the backfires too intense/noisy.
Final balancing retains that pulse, duration and wall model: body gain is reduced
from .32 to .18 and overall afterfire mix from .45 to .35 (about 7.2 dB less body
component gain together). High grit drops from .018 to .012 and residual flow
from .0015 to .0008. Neither reaction probability nor timing is changed to make
the sound quieter.

`AfterfireAudioSynth` now has parallel outlet and local wall-radiation paths.
The outlet obeys downstream filters and in-pipe travel delay. Local structural
sound precedes that longer route and is not filtered through all downstream
silencers. A small energy-envelope share feeds broad, heavily damped modes using
the nearest fitted component's kind, resonance and distance. The three common
410/940/2,100 Hz higher-Q ringing modes are removed. Both branches still pass
through the common outdoor listener model.

This is wall vibration radiating sound, not invented gas leakage or a change
to part condition. [COMSOL's acoustic–shell coupling example](https://www.comsol.com/model/absorptive-muffler-with-shells-14717)
and [shell-radiated emission analysis](https://www.comsol.com/blogs/predicting-the-sound-emission-of-a-muffler-design-via-simulation)
support the parallel-path distinction. The project does not author shell
thickness/material/mounts; local coupling and broad modes are explicit
surrogates, not a finite-element structural solution. Listener geometry also
retains one representative emitter position rather than separate wall emitters.

These are calibrated reduced-order acoustic transfer coefficients, not an
absolute SPL calibration or a CFD pressure-wave solver. The event energy remains
the amplitude funding input; PCM squared-energy values are not claimed to equal
physical joules. Pressure/texture diagnostic samples make their relative energy
testable without changing the production mix.

Fuel/oxygen transport, reaction timing, seeded ignition, chemistry, per-cylinder
engine synthesis, BOV, turbo and outdoor listener remain unchanged. The installed
outlet response is retained alongside the new local structural path.
This does not add ECU fuel maps or repair the acknowledged petrol/diesel
flammability approximation. It cannot certify physical fueling accuracy.

## Verification and review

`DragLab.Editor.AfterfireBangVerification.CaptureBefore` records the old installed
afterfire-only Bison and Tempest paths. `Checkpoint` records the same inputs after
the correction, verifies the new source, then reruns outdoor, causality, mixture,
runtime integration and production-DSP race replays. Captures use unchanged
listener geometry and retain each version's actual source calibration; no
separate loudness normalization is applied. Raw WAVs/logs
are local ignored artifacts.

New source gates cover 24, 44.1 and 48 kHz: audible blast-body/air balance, seeded
variation confined to texture, physical rise/overpressure and burn duration,
square-root energy/amplitude scaling, simultaneous reaction subdivision,
zero-energy silence, steady-flame behavior, depletion/clear and zero warmed
event/sample allocations. Wall gates verify actual fuel-funded excitation,
independence from downstream attenuation, earlier local arrival, a bounded
outlet/wall sum and finite decay without a sustained bell. Existing checks cover exact vehicle traces, fuel
conservation, event delivery, exhaust gain and callback budget.

## Technical checks

- **48** source/isolated-replay gates, **33** outdoor/pressure gates, **20**
  condition/causality gates, **26** runtime integration gates and **49** mixture
  gates pass (**176** total).
- All **47** body/wall production-DSP race replays pass before the final gain-only
  balance, preserving elapsed/legacy
  physical traces. All **117,150** reactions are delivered without history drops
  or stolen tails. Complete mixed output peaks at **0.4963**; the largest local
  callback p95 is **1.3201 ms** per 400 stereo frames against an **8.333 ms**
  budget. These are Editor observations, not an Android device benchmark.
- After the gain-only balance, rerun all **176** gates and five focused production
  replays: Bison soft ignition, diesel soft fuel cut, Tempest two-step ignition,
  absorptive and open exhaust. All pass with unchanged reaction counts/ET,
  complete delivery and finite output. This reuses the full 47-case topology
  baseline rather than claiming it was repeated at the final gain calibration.
- A fresh Windows Development build succeeds. Runtime assembly SHA256:
  `3E5B0050A0DBEA2D9DA3F90E51A2915BC0F9C41470FF1F5680EF140E60A1884B`.
  Earlier edge-only build/live receipts are superseded, not validation of the
  body/wall revision.
- Before the gain-only balance the packaged body/wall Tempest run also passes:
  **5,387/5,387** reactions, exact ET/shifts/cuts/releases, Stop/Resume and
  tire/rival silence. `live-tempest-before-balance.txt` retains that receipt.
  Final lower-gain playback is a normal listening run; it is not presented as
  another completed automated packaged verification.
- The before/after Bison and Tempest isolated captures retain **32,358 / 5,387**
  reactions and identical elapsed times. The isolated paths bypass the production
  mix's DC blocker/saturation, so their raw peaks are diagnostics, not game output
  loudness. See `capture-metrics.txt` and the complete pipeline receipts.

Only the existing `26-launch-hard-ignition` Tempest fixture is reopened in normal
listening mode through `Tools/Test-RaceAudio.ps1 -SkipPrepare -SkipBuild -Case
'26-launch-hard-ignition'`. The production engine and all other sound layers are
preserved. User listening acceptance remains separate; no later phase advances.
Android is not rebuilt by this requested Windows listening checkpoint.

Unrelated pre-existing supply/parts-market/SQLite report changes, `Tests.ps1`,
remote attachments and race-exit/shop-responsive reports are left intact.
