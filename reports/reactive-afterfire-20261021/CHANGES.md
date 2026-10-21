# Live chemistry-driven afterfire integration — 2026-10-21

User request: record the shared gas-pressure/flow direction for later, then
integrate backfires which retain the clear HTML character while responding to
the simulation. The prior request to keep changes preview-only is superseded for
this integration. Other engine/induction generators and vehicle physics remain
unchanged. [Recorded direction and deferred coupling](../../docs/AFTERFIRE_AUDIO_DIRECTION.md).

## Delivered behavior

`AfterfireLabSource.Begin` still reproduces the supplied HTML pressure/noise
source for reference tests. The production source now uses `BeginReactive` and
`React`: only actual chemical reaction packets already delivered by the stamped
history reader supply energy. No future whole-flame total is available or used.

- The first coalesced excitation supplies the compression attack. Numerical
  subdivisions delivered at the same sample combine before selecting strength;
  later packets do not restart or turn up the old coherent bang.
- Continuing combustion replenishes separate crack, body, bark, tail, colour and
  diffuse energy responses. Their state decays between reactions. The lifetime
  and shape therefore respond to actual continued burning, instead of growing
  an external gain over an already dying fixed burst.
- Burn time and reacting volume adjust decay; pressure, gas temperature, volume
  and rise time shape the compression response. No waveform resampling/pitch
  shift or general extra low-pass is used to achieve this.
- Keep the original correlated multiband texture, seeded pressure/scattering,
  broad exhaust modes and normalized nonlinear body response. Output pressure
  still scales with the square root of actual reaction energy; same-time packet
  subdivision is invariant. Control mode labels alone cannot create explosions.
- Each reacting location has one ongoing source and its actual installed path.
  Different locations within a transported flame keep their shared flame seed
  and identity while exciting the local path; packets at one location do not
  each start another attack. Source colour, turbine loss, travel, separate wall
  radiation, native bus and outdoor processing are preserved.
- Bound the acoustic ringdown by the hardware response and actual burn/volume
  history, extending it when new fuel burns. Smooth only the last 12 ms at this
  low-level response horizon; retire drained excitation and its FIR cleanly.
  This prevents indefinite quiet tails exhausting the prepared voice budget.
- Reduce backfire reference output from 0.35 to **0.25** (about **-2.92 dB**).
  Fitted `exhaustPopGain` remains effective. This addresses the overly loud
  fixed-strength audition without muting the pressure attack or changing the
  engine/intake/turbo source calibration.

No car resources, fuel/wear policy, money, save format, authored catalogue IDs,
replay physics, UI controls or opponent timing are changed. All simulation
energy/resource ledgers and physical race results remain authoritative.

## Verification

- **215 reference/funding/hardware gates pass.** All ten original HTML source
  presets match over their complete reference window at 24 / 44.1 / 48 / 96 /
  192 kHz. Baseline physical excitation retains the HTML's early attack/body
  through actual installed paths/outdoors. Later live decay is tested as dynamic
  behavior rather than incorrectly requiring the HTML demo's fixed stop time.
- **14 new live-source/Tempest gates pass:** past/future independence, actual
  continuation response, no pressure-attack retrigger, physical burn/heat/volume/
  pressure/rise response, flag independence, seeded variation, exact zero-energy
  silence, finite drain, zero first-source callback allocations, unchanged
  Tempest chemistry and unchanged pre-backfire engine opening.
- **20 causality + 49 mixture gates pass.** Fuel/oxygen/heat transport remains
  conditional on real combustion; flags alone do not substitute for mixture.
- **26 runtime integration gates pass:** exact seeded pipeline, complete event
  delivery, no pause/resume replay, fuel conservation, zero warmed allocation in
  mixture tick plus full audio callback, retained BOV/cuts/wear/resources and RNG.
- **All 47 real race-audio pipelines pass** with the same elapsed times and
  chemical traces, complete reaction energy, no lost events, pressure-buffer
  overflow or stolen tails. The roster covers cylinder counts, petrol/diesel,
  injection/carburetor, limiter styles and cut methods, exhausts, turbo/supercharger
  combinations, releases and tire-slip driving scenarios. This suite renders
  engine audio for those scenarios; the separate tire-sound generator is unchanged
  and its dedicated suite was not rerun. No unrelated retuning is performed.
- Worst roster callback **p95 4.0072 ms**, observed maximum **7.5735 ms**, against
  an **8.333 ms** / 400-stereo-frame signal budget on this Windows Editor host.
  The dedicated hot-mixture check passes its 60% budget threshold at both 44.1
  and 48 kHz. These are host measurements, not physical Android profiling.
- The broader suite initially exposed ten stolen tails in the twin-turbo case.
  Bounded reaction/hardware ringdown fixes this; the entire 47-case suite was
  rerun after that correction and final level calibration. An initial live-source
  deadline sentinel overflow was also caught and repaired before these gates.
- Hardware settings comparisons now compare actual fields rather than Unity
  JSON on a nonserializable struct. Mode-label tests retain identical physical
  inputs; they no longer incorrectly require pressure/rise/burn changes to be
  inaudible. Local reacting-zone tests reflect their distinct physical source.

The historical fixed-strength preview receipts remain historical baselines, not
claims about the new live envelope. Their WAVs are preserved; their old reference
renderers are not rerun to overwrite those accepted comparisons during integration.

## Listening and builds

Local `tempest-game-before-after.wav` contains the complete original native
Tempest mix at **0:00**, followed by one second of silence and the **actual
integrated production output at 0:37.158333**. Both sequences last 36.158333 s,
including a two-second hold of the final active engine audio frame. These are
48 kHz / 24-bit stereo, same input replay, player only, same native trackside
geometry and **one shared gain of 1**. No offline replacement source or future
energy is inserted into the after recording.

**8 independent WAV gates pass:** RIFF sizes/format, complete lengths, exact
separator, historical before PCM, bit-identical engine/turbo/intake opening,
nonzero changed live mix, stereo and unclipped samples. Compared with the rejected
fixed-strength audition, launch-window full-mix RMS is **1.12 dB lower**; full
recording peak is **0.2717** versus **0.4689** in that audition. This does not imply
human listening approval. No loudness normalization hides the comparison.

The Windows player is rebuilt. Its packaged source is independently loaded:
reactive excitation produces nonzero PCM, reference gain is 0.25, and Editor
capture hooks are absent. Windows `Assembly-CSharp.dll` SHA256:
`CA2B0959534BA4C5AB7E921206324B0D5579F8775B3DA8CC453EDE71634DD5A6`.
The Android ARM64 IL2CPP APK is rebuilt; ZIP CRC, manifest and ARM64 native
libraries pass inspection. Its status/hash is recorded in `build-verification.txt`.
Device playback/performance and human sound acceptance remain separate gates.

This is a reduced-order procedural acoustic model with an authored joule-to-audio
calibration, not a calibrated microphone/SPL prediction or full compressible-flow
solver. The broader bidirectional pressure/flow/turbine/valve feedback discussed
with the user is **not implemented here**; its scope and current gaps are recorded
in the linked direction document.

Reproduce source checks/export using Unity CLI execute method
`DragLab.Editor.ReactiveAfterfireVerification.Run`, after generating original JS
fixtures with `node tools/Verify-AfterfireLabReference.cjs reports/reactive-afterfire-20261021`.
Then run `ReactiveAfterfireVerification.CheckAllRacesAndBuild` for the full roster
and Windows, and `PlayerBuild.BuildAndroid` for Android. Independently inspect
the listening WAV with `node tools/Verify-ReactiveAfterfire.cjs`.

The archived local pre-integration PCM is only needed to export/validate the
listening A/B, not for source/chemistry/runtime tests. Required code, concise
receipts and design notes are tracked; WAV/Float32/JSON/log/build files stay local.
Unrelated report edits, attachments, Tests.ps1 and race/shop outputs are preserved.
