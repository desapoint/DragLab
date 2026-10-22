# Fixed-tuning signal diagnostic checkpoint — 2026-10-21

Requested comparison only, following `dd32554`. Adds the diagnostic audition to
the existing browser workbench. No production Unity code, player builds, physics,
live game audio, assets, settings or saves changed. Existing unrelated market,
save and race-audition reports, Tests.ps1 and attachment/test folders are retained.

## Delivered

Tempest turbo and Bison carbureted V8 each have 14 full-sequence, natural-level
WAV passes, 13 adjacent A/B WAVs, and two shorter chapter sequences. The workbench
preloads them and can rebuild from current frozen tuning with explicit browser
Race drive or Neutral/dyno. Pass/player descriptions and download links identify
the selected input/layer. Playback stops the other workbench audio players.

Source browser driving is computed once, before any substitution. Capture the
actual raw controls and RNG state at control boundaries; replay cannot affect
later driving. The baseline is sample-identical to the direct input replay with
the same engine-core/dry processing mask. Hardware, seed, gains, pulse settings,
exhaust and listener values are fixed; only named signals and layer masks change.
The final existing-adapter endpoint includes actual tires and continuous RNG,
and is explicitly labelled as an endpoint rather than another isolated input.

See [usage, pass definitions and fidelity limits](../../docs/AUDIO_SIGNAL_DIAGNOSTICS.md).
Raw WAV/HTML/JSON remain ignored local artifacts. Source and concise evidence
are versioned. There is no automatic application of a preferred sound.

## Checks actually run

- 12 complete two-car diagnostic gates pass, covering all 28 rendered passes:
  exact cached baseline, signal substitution boundaries, frozen acoustic fields,
  layer masks, actual event times/seeds, unchanged source/cache/settings,
  deterministic repetition, excluded-later-input isolation and rejected mismatches.
  Newly rendered PCM is finite and unclipped.
- Eight diagnostic UI contracts pass using actual workbench functions: build all
  passes, preset changes during async yields, selection/download routing,
  exclusive playback, exact stored-PCM A/B concatenation and one-second gap,
  preloaded files and pair validation. Embedded modules match inspected sources.
- Ten existing UI/embedded AudioWorklet contracts pass after extending exclusive
  playback to the diagnostic/preview players. The old assertion that assumed
  Stop was the final call was updated to also require the new player pause.
- All four supplied browser drivetrain, limiter, shift and tire suites pass.
  The existing complete five-case reference verifier passes all 46 gates again.
- All four complete 15.9-second Free sequences at 24 kHz are sample-identical
  to the pre-diagnostic `dd32554` browser DSP. Diagnostic hooks do not alter
  ordinary playback. All 54 pre-rendered individual/pair file references have
  valid 48 kHz stereo PCM24 headers and packaged playback links resolve.

These are technical checks, not browser visual/listening acceptance. No real
browser automation, phone profiling or Unity player rebuild was performed.

## Measurement leads, not listening verdicts

Measured whole-sequence RMS changes from the immediately preceding pass:

| Added input/layer | Tempest | V8 |
| --- | ---: | ---: |
| Aggregate combustion fraction | -2.53 dB | -1.07 dB |
| Actual cuts / two-step / shift target | +3.72 dB | +3.02 dB |
| Intake / induction / ancillary engine layers | +2.83 dB | +0.49 dB |
| Actual backfires / BOV | +2.13 dB | +0.02 dB |
| Outdoor processing | -4.25 dB | -3.11 dB |

Recorded load has a much smaller overall-level effect in these two sequences
(-0.14 / +0.04 dB), which does not rule out local timbre changes. Boost/airflow
correctly leave the naturally aspirated V8 unchanged. Setting previous throttle
to zero has only a small whole-sequence level effect; it remains a separate
audible test. Measurements are retained in measurement-matrix.csv with the frozen
settings hash. Waveform differences include phase and propagation delay and
must not be interpreted as perceptual error scores.

Listen first around passes 06–08 and 11–12, while also checking RPM/pedal changes.
The partially substituted states are deliberately hybrid; their cut/load timing
need not be physically consistent. These results do not establish bad simulation
or justify removing real cuts/combustion effects. No attempted correction is
included in this checkpoint.

## Review checklist

Open index.html and use **Fixed-tuning signal diagnostics**. Compare the previous
and selected pass in both cars, especially the rev/limiter and shift chapter
links. Identify the earliest step where the preferred sound changes undesirably.
The pre-rendered source is Race drive; choose Neutral/dyno and Build comparison
if that matches the input-only rendition being reviewed. Keep tuning unchanged
within a comparison. User listening approval remains pending; stop here before
any integration, calibration or architecture-plan step.
