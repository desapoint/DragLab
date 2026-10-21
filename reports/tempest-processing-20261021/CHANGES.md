# Tempest generator and processing auditions — 2026-10-21

Requested scope: export the real Tempest scenario per generator and in multiple
combinations, repeating identical PCM with raw, cumulative and bypassed processing
stages. This is an offline listening/debug checkpoint. No sound calibration,
physics, settings UI, player resources or saved profile is changed.

## Delivered recordings

**50 local WAVs**, 48 kHz / 24-bit stereo PCM, plus CUE chapter indices, embedded
sample-accurate cue/LIST labels, per-file pass CSVs and a linked
[LISTENING_GUIDE](LISTENING_GUIDE.md). Raw audio and generated CUE/JSON/fixtures stay
local and ignored. The authored tools and concise evidence/matrices are versioned.

- 23 generator/combination recordings in both fixed-level and level-matched form.
  Each repeats the same 50.158333-second capture over **22** processing passes,
  with one second of digital silence between them.
- Generators: combustion/exhaust, block/manifold, cut/control colour, clatter,
  intake rush, turbo whistle, compressor airflow, turbine interaction, real BOV,
  backfire, pressure-driven shell, each of four tires and the combined tires.
- Overlays: engine/intake; engine/turbo; engine/backfire; induction/BOV/backfire;
  full engine; full vehicle; backfire/BOV. Engine and tire bus limiters are applied
  at their real locations before their independent outdoor paths are summed.
- Two additional engine-internal WAVs show cylinder pressure, headers, collectors,
  outlet band limiting, boost-dependent turbine filtering, body EQ, generator
  saturation and the fitted exhaust filter. These expose intrinsic engine shaping
  separately from the extra bus/outdoor processing.
- Two short launch/lift comparisons repeat race time 13.5–19.5 seconds for quicker
  backfire/full-mix review. Each pass begins seven seconds after the previous;
  full processing starts at **00:56**. They retain whole-run matching gains;
  strict equal-RMS comparisons use the full level-matched recordings.

The 22 passes are raw output; cumulative hardware/fitted exhaust, source power
loss, travel/shell, final engine DC, bus limiters, direct distance/delay/stereo,
air absorption and reflected paths; then full-minus-hardware, loss, travel,
shell, DC, limiters, spatial delays/pan, air and reflections; raw-plus-DC,
raw-plus-limiters, raw-plus-outdoor, and full without both DC and limiters.

## Controlled source capture

The original `26-launch-hard-ignition` input replay is preserved, including idle,
throttle blips, neutral limiter, two-step countdown, acceleration/shifts, loaded
lift/pressure release and finish/shutdown. The original physical trace retains
5,387 chemical reactions, 54 flame bursts and two BOV releases. Its inputs SHA256:
`6906758BF5F0DF0087E4BF62D5D5C4207C746455D20A77FFF479862F34E203E6`.

The original race has traction assistance ON and no audible tire slip. To avoid
fabricating tire sounds or changing that reference, append a separate labelled
14-second physical Tempest launch/braking test: assistance OFF, street tires and
local grip multipliers 0.20. Its real contact model produces 2,754 slipping
wheel-ticks and nonzero sound at every wheel. This low-grip diagnostic is not a
new gameplay calibration. The final two seconds of the original replay are a
held final frame; the two independent simulation/DSP sessions reset at the join.

Capture hooks are guarded by UNITY_EDITOR. They passively expose production
values; raw afterfire comparison uses an independent identically seeded V3 source,
with actual physical event timing/energy. Every subsequent audition pass reuses
those frozen captures rather than rerolling noise or re-running different races.
The default source/mix/atmosphere path is unchanged. The original V3 HTML remains
untouched; the Python tool validates PCM, it does not synthesize sound.

Raw means the defined generator before extra routing/mix/outdoor stages, with its
intrinsic physical pulse/noise/envelope/anti-alias synthesis intact. Hardware-source
bypass is distinct from the engine's intrinsic pressure core, which has its own
comparison WAV. Listener geometry is fixed at a 12 m lateral offset, .55 m engine
and .08 m tire source height, following the car longitudinally. This avoids camera
motion as an uncontrolled Doppler variable. Native DSP is included; OS/driver/
speaker processing is not. Master/category/compressor-noise gains are fixed at 1;
stored user preferences are not loaded. Chapter/session joins are editing boundaries.

Fixed-level files retain a single gain across all their passes, reduced only to
avoid clipping a raw peak. CSVs record it. Level-matched files equalize whole-pass
RMS with one common peak guard; subtle components can consequently be much louder
than their normal gameplay balance. No per-event normalization, new compression,
fade or dither is added. Solos do not simply sum after nonlinear limiting to equal
a limited mixture: each combination is processed at the correct bus boundaries.
The full final vehicle pass uses the actual captured production engine/tire PCM.

## Verification and reproduction

- **38** capture/export gates pass. Enabled and disabled capture generate
  bit-identical engine/tire PCM over both original and appended scenarios.
  Stems reconstruct the production mix; maximum engine PCM error is 2.98e-8.
- Independent Python validation passes all **50** WAVs, covering **1,072** chapter
  sections and **1,022** exact one-second silent gaps, RIFF sizes, 48 kHz / 24-bit
  stereo format, cue labels/offsets, finite unclipped peaks and RMS matching.
  Maximum quantized RMS discrepancy versus recorded gains is **9.81e-10**.
  All 50 files have nonzero audio. Generated file hashes are in local wav-manifest.json.
- Re-run **215** preset/funding/hardware gates, **20** causality gates, **26** runtime
  integration gates and **49** mixture gates: all pass. Focused actual Bison
  carburetor and Tempest race replays pass with their physical traces and chemical
  funding preserved. The prior full 47-case result is retained in the previous
  checkpoint, not claimed as rerun here.
- Windows Development build succeeds. Audit capture types, turbo audit fields,
  outdoor audit flags and the Editor exporter are absent from the packaged player
  assembly. Assembly-CSharp.dll SHA256:
  `540C8BFC8E90831173E00EE7E978CAE4CAF665DC0805F69AF7B25AA1947086F8`.
  No Android build/device profiling is part of this export checkpoint.

Recreate audio in Unity:
`DragLab.Editor.TempestProcessingAudition.Run`

Validate WAVs (numpy):
`python tools/Verify-TempestProcessingWavs.py`

For gates/build, first generate the JavaScript references into
`reports/tempest-processing-20261021/regression` with
`tools/Verify-AfterfireLabReference.cjs`, then run
`DragLab.Editor.TempestProcessingAudition.VerifyCaptureAndBuild` with
`--draglab-runtime-audio-cases 22-,26-`.

No acceptance of sound quality is inferred from automation. Review the raw,
progressive, omitted-stage and overlaid versions before changing the actual audio.
Unrelated report edits, attachments, Tests.ps1 and race/shop reports are preserved.
