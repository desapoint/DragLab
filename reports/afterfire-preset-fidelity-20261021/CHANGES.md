# Preserve the approved HTML preset sound in gameplay — 2026-10-21

The user rejected the previous live integration as gunfire-like and requested
the HTML presets' timbre, with simulation controlling when and how strongly a
backfire sounds. This supersedes the live adaptation in `3a5d3f0`. Listening
acceptance remains separate from technical delivery.

## What was wrong

The earlier comparisons validated `BeginReference`, while gameplay replaced the
six preset envelopes, compressed width/decay from reaction-packet time scales,
blended in Shotgun Single and added another attack on positive pressure updates.
Each exhaust zone also owned its own attack for the same transported flame.
Passing source-only parity did not validate that altered gameplay output.

## Correction

- Remove the alternate reference/live envelope modes and reaction-driven source
  remapping. One `AfterfireLabSource.Begin` generates the original shock, separate
  envelopes, seeded within-burst follower/crackle, colour and smooth turbulence.
  No fixed recording bank is used. Oversampling and antialias filtering remain.
- Select an **exact HTML preset** from installed hardware. Tempest uses **Turbo
  Pop & Flame**; carbureted engines use Old-School, large V8s Big-Block, high-rev
  naturally aspirated engines High-RPM, chambered V8s Old-School and remaining
  engines Street. Native calibration can select any of the ten presets.
- A real chemical flame ID starts one running preset envelope. Other zones
  carrying that flame share its sample phase instead of restarting the bang.
  Each zone retains its own header/turbine/silencer/wall/arrival path. Each actual
  reaction contributes its acoustic energy to that zone's pressure-amplitude gain.
  Gain is proportional to square-root energy; later changes fade over 5 ms to
  avoid clicks. Simultaneous packet splitting preserves the initial waveform.
- A new chemical flame can start another burst. Continuing packets, including
  positive numerical pressure renewals, do not restart the preset. The explosion
  envelope is finite: steady continuing flame funding does not create a repeating
  preset loop. HTML micro-pressure texture belongs to that one funded acoustic
  burst; it does not invent chemical reactions or consume fictional fuel.
- Keep the existing real chemical model, seeded occurrence, fuel/air/heat,
  physical energy ledger, event times and parts/exhaust simulation. Limiter,
  two-step and anti-lag labels do not schedule audio or select presets.
- The preset already supplies its attack spectrum; remove the duplicate
  reaction-rise low-pass, using a 12 kHz source guard before installed filtering.
  Preserve hardware attenuation/reflections and existing outdoor propagation.
  Do not duplicate the HTML's demo room, normalize every event to peak volume or
  add its browser compressor over the game mix.
- Crop the causal FIR's negative-time startup skirt. The HTML's look-ahead FIR
  discards it; gameplay now emits silence during the eight-frame latency and
  matches the reference from its first intended sample through its padded tail.

Engine/induction/BOV/tire audio, physics, wear, resources, saves and UI are not
changed. The original HTML remains byte-identical in `docs/references/audio`.

## Verification and review

The reference tool now extracts both the original mono signal and its coherent
pressure component. It takes an optional output directory, keeping earlier
checkpoint evidence intact:

`node tools/Verify-AfterfireLabReference.cjs reports/afterfire-preset-fidelity-20261021`

`AfterfireLabVerification` records known reactions into the real stamped history
and runs the normal `Connect`/history-reader/burst/zone/path code. Reference PCM
passes through the same installed exhaust and outdoor processing. No test-only
render mode is selected. Both paths have identical fixed gain; no loudness match
or shifted error tolerance hides tonal changes.

- **157** fidelity/funding checks pass: **50** full source comparisons and **50**
  actual gameplay/exhaust/outdoor comparisons, all ten presets at 24/44.1/48/96/
  192 kHz; first/warmed zero allocations; low-rate 8/16 kHz stability; energy-only
  scaling; simultaneous packet subdivision; no flag/pressure/rise/burn remapping;
  no-energy silence; one envelope for continuing packets and transported zones;
  new-flame triggering; finite tails and reset.
- Maximum source error is **1.19e-7**. Maximum actual gameplay/outdoor error is
  **1.49e-8**, comparing full files including the startup and final padded tail.
- The comparisons exposed and fixed the real startup-skirt difference. An
  intermediate test run was interrupted by a Windows mapped-file receipt-write
  conflict, not an audio assertion; fidelity receipts now write once on completion
  or failure instead of repeatedly truncating a large monitored file.
- **26** production integration and **49** fuel/mixture gates pass, with the same
  warmed allocation, event/BOV delivery, fitted-gain, RNG and physics contracts.
- All **47** actual race replays pass on the final runtime code: **117,150**
  reactions retain their physical traces and acoustic funding, with no lost events
  or stolen tails. Largest local callback p95 is **6.2174 ms** / 400 stereo frames
  against **8.333 ms** of signal; Tempest p95 is **4.5990 ms**. Individual measured
  maximum callbacks can exceed the buffer; these desktop p95 checks are not a
  no-stutter guarantee or a phone measurement.
- The older causality suite's reaction-descriptor assertions are relabelled as
  diagnostics, so they no longer purport to test gameplay envelope reshaping.
  Current waveform claims are checked through the live path above. Its output
  can be directed to this checkpoint, preserving historical causality receipts.
  All **20** causality gates pass, including real ignition retard/cut behavior,
  fuel/chemical-energy conservation, label-independent histories/PCM, cold-gas
  quenching, trace-fuel rejection and continuous pressure venting.

Real race inputs use the existing
`DragLab.Editor.RaceAudioAuditionVerification.PrepareInputs` generator when its
local `.race.json` fixtures are absent. With those and the Node references ready,
`DragLab.Editor.AfterfireLabVerification.VerifyAndBuild` runs the current gates,
all 47 replays and Windows build. Generated PCM/fixtures/logs remain local.

Windows Development build succeeds, runtime assembly SHA256:
`7E7DE1A4A0F0413578B9684EAA7929D256D06E5BDCE369E38F4CBE66F78F5CCD`.
Only subsequent Editor test-label/report-output changes occur after that build;
they do not alter the packaged runtime. The Tempest launch snapshot is recorded
separately in `listening-status.txt`.

The production audio/chemistry checks, full race replay results, build identity
and real Tempest playback snapshot are recorded alongside this report. Desktop
measurements do not certify Android device performance; this checkpoint does
not rebuild Android. Existing unrelated report edits, attachments, `Tests.ps1`
and race/shop reports remain untouched. No later architecture/UI phase advances.
