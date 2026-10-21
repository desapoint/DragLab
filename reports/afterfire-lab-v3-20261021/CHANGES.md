# Approved V3 backfire source — 2026-10-21

The user supplied `backfire_lab_v3.html`, requested keeping it in the project,
applying its sound behavior, using its presets to calibrate engine/exhaust
combinations, and opening the Tempest-only live game test. This checkpoint changes
afterfire audio only. Listening acceptance is pending the user's review.

## Reference and port

- Preserve the original byte-for-byte in `docs/references/audio/`, with SHA256
  `6E97825426EA143E4CF102CDB66A39E35E9D4D7D684FF531DE2A5D8D46A5CBBE`.
- `AfterfireLabSource` ports the nonperiodic shock, four cascaded noise bands,
  smooth low/mid pressure body, broad overdamped coloration, body saturation and
  DC blocker. It retains V3's 2x oversampling and 33-tap FIR; 96/192 kHz use the
  original 1x path. Causal processing adds eight output frames below 92 kHz.
- `AfterfireLabSettings` transcribes all ten sound presets. Automatic hardware
  calibration blends Street, Old-School, Turbo Pop, High-RPM and Big-Block anchors;
  measured abrupt pressure rises also blend the Single anchor. The other lab
  presets remain available references, not automatic mode-flag sound choices.
  Carburetion, displacement/cylinders, redline, turbo hardware, collector scale
  and silencer/resonator types influence calibration. Actual source temperature,
  reacting volume, pressure rise, burn duration and flow refine each flame/zone.
- Actual chemical events retain their times, seeds, fuel/oxygen/thermal gates and
  acoustic energy. Positive pressure renewals excite the shock; all continuations
  fund the finite combustion body. Same-time subdivision cannot amplify a bang.
  No HTML count/spacing loop, automatic follower or extra micro-ignition schedule
  is added to gameplay. Real secondary flame fronts still arrive from chemistry.
- Keep installed source-relative exhaust paths, turbine transfer, pressure-driven
  wall radiation and trackside propagation. Do not duplicate the HTML's demo
  stereo delays/room, peak normalization or browser compressor in the game.

This remains a reduced-order procedural acoustic model, not CFD or absolute SPL
calibration. Local sound-shape parameters are captured per flame/zone voice;
continuing reactions update actual energy and pressure funding. Existing exhaust
path temperature/boost snapshots are retained. The engine bed, injection/wear/cut
behavior, induction/BOV, tire sounds, physics, resources and saves are unchanged.
The older `ReactivePressureBurst` remains for historical diagnostic A/B tools;
normal live afterfires now use the V3 source.

## Reproducible checks and performance

Run `node Tools/Verify-AfterfireLabReference.cjs` before Unity verification. The
tool executes the supplied JavaScript DSP, extracting mono PCM before demo gain
and spatialization. Generated float fixtures/JSON stay local. The C# comparison
checks all ten presets at 24, 44.1, 48, 96 and 192 kHz, accounting for causal FIR
delay and excluding the finite reference file's last zero-padded FIR samples.

- **50** JS/C# comparisons pass. Largest absolute sample difference is about
  **1.19e-7**; largest relative RMS error is **2.65e-8**.
- **124** source gates (including the 50 comparisons) cover seeded detail,
  energy/amplitude scaling, same-time packet
  subdivision including queued renewals, no flag-created bangs, no-energy silence,
  steady combustion without repeated shock attacks, finite tails, clear/reset,
  first-reaction and warmed zero allocations, and silent filter retirement.
  Rates also include 8/16 kHz; the upper overdamped mode is stability-limited at
  those low device rates without changing the normal-rate reference response.
- **26** runtime integration and **49** fuel/mixture gates pass: unchanged
  vehicle/resources, preserved RNG, actual fuel transport, fitted gain, complete
  event/BOV delivery and zero warmed callback allocations.
- Full **47** production-DSP race replays pass, preserving **117,150** reactions,
  unchanged ET/legacy physical traces and no dropped events or stolen tails.
  This full roster precedes the final inaudible filter-floor cleanup.
- Recheck the final DSP on the **six heaviest cases** (03, 04, 14, 22, 26, 46):
  **67,027** reactions, unchanged physics and no drops/steals. Final largest local
  callback p95 is **6.1715 ms** / 400 stereo frames, against **8.333 ms** of signal.
  Tempest p95 is **4.0909 ms**. The separate hot-mixture p95 is **4.3042 ms** at
  48 kHz and passes the existing 60%-of-buffer gate.

The first port exceeded a pressure-list capacity; exact linear superposition now
accumulates its four response states with a fixed delay ring, without dropping
pressure renewals. The first CPU pass also exceeded the stress gate. Decaying
states could become CPU-slow subnormal numbers; clearing remnants below 1e-24
prevents this in pressure/envelope and wall/colour modes. A high-accuracy cubic
table evaluates the same tanh, with reference parity above. Static preparation is
forced before hardware construction returns, outside the audio callback. The
fixed buffers require no live sample-array rendering or new reaction allocations.

`before-optimization.txt` and `before-wall-floor.txt` retain the intermediate CPU
receipts; `dsp-cost.txt` is an isolated microbenchmark, not an Android measurement.
`source-verification.txt`, `verification.txt`, `mixture-core.txt`, `race-pipeline.txt`
and `race-pipeline-focused.txt` distinguish final gates from retained full-roster
evidence. Reproduce full checks/build with
`DragLab.Editor.AfterfireLabVerification.VerifyAndBuild`; pass
`--draglab-runtime-audio-cases 03,04,14,22,26,46` for the focused rerun.

## Build and listening review

Windows Development player is rebuilt under `Game/Builds/Windows`. The final
source-only rerun additionally checks zero allocation on the first reaction after
hardware construction. Build identity and the real player launch snapshot are
recorded in `listening-status.txt`.

Run `Tools/Test-RaceAudio.ps1 -SkipPrepare -SkipBuild -Case 26-launch-hard-ignition`:
the actual Windows game plays Tempest at normal speed, engine-only, with the rival
muted. The test's existing Stop/Resume/Restart controls remain test-only. A live
snapshot proves advancing playback and output, not subjective sound acceptance.

These are desktop Editor/player checks. Occasional measured maximum callbacks
exceed one signal block; p95 gates do not guarantee no scheduling spikes. Android
is not rebuilt or device-profiled in this listening checkpoint. Listen on a phone
before treating its performance or mix as accepted. Pre-existing unrelated report
edits, attachments, `Tests.ps1` and race/shop reports are preserved. No later UI or
architecture phase is advanced.
