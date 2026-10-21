# One hardware acoustic response for afterfires — 2026-10-21

Requested correction: preserve the supplied V3 preset character, remove duplicated
processing, keep simulation and installed hardware meaningful, then relaunch the
real Tempest audition. This is a backfire-only checkpoint; listening approval is
pending. No architecture/UI phase is advanced.

## Diagnosis and boundary

The user's six-second 48 kHz / 24-bit Turbo Pop & Flame export reproduces the
original default preset/sequence to quantization/dither precision (15 hits;
maximum float discrepancy 2.09e-7 in the diagnostic decode). The original HTML
remains byte-identical. Its broad tube/body coloration already voices an exhaust.
The old game then applied another source rise filter, installed muffler filters,
turbine low-pass, header modes, runner/collector echoes and generic soft/scatter
smoothing. Even an open path acquired that extra softening. The earlier source
comparison passed, and the end-to-end comparison filtered both inputs equally;
that could not establish parity with raw HTML timbre.

## Implementation

- Keep the original streaming V3 pressure/noise/envelope generator and its seeded
  texture. No WAV playback bank, forced backfire sequencer or engine-bed rewrite.
- Prepare the hardware response as generator parameters in
  `AfterfireHardwareAcoustics`: installed component amount/cutoff/softening and
  resonance influence air/crack/smoothness, bark/tube/focus and decay. Actual
  runner/collector geometry and authored sharpness/decay remain effective.
  Upstream explosions receive boost-dependent turbine coloration once in those
  parameters; downstream explosions do not pass through a fictional turbine.
- Make the reference anchors explicit: stock turbo/street exhaust, chambered
  carb exhaust, and open competition exhaust. The stock Tempest downstream
  generator retains the exact approved Turbo Pop & Flame parameters. Hardware
  modifications change that one response relative to its anchor. This is a
  calibrated reduced-order acoustic model, not measured manufacturer data.
- Capture source parameters at the first funded reaction of a chemical flame.
  Transported zones retain its phase/envelope without another initial attack.
  Their actual cumulative acoustic energy still controls amplitude independently.
- Reduce `AfterfirePipeResponse` to physical downstream attenuation, upstream
  turbine power loss where applicable, hot-gas travel delay and separate
  coherent-pressure-driven local shell vibration. Do not repeat outlet EQ,
  turbine low-pass, header/runner echoes or generic smoothing after the generator.
  Shell radiation remains a distinct path driven by pressure, not a dry bypass
  for noisy turbulence.
- Keep the existing whole-vehicle outdoor propagation once. Do not add the HTML
  demo's room, artificial event normalization or browser compressor. Fixed mix
  gain, engine/induction/BOV/tire generation and actual chemical model are intact.
  Fuel/oxygen/temperature/seeded occurrence, resources, wear, saves and physical
  race traces are unchanged.

## Verification

The new outlet regression has an independent expectation: raw JavaScript PCM
with gain and integer travel delay only. It does not send that expectation
through the same response class, so an extra filter now fails the test. Source,
full routing/outdoor, funding, shared flame and allocation checks remain.
The old runner-reflection test is replaced by a strict single-impulse outlet test;
runner effects are checked at the actual generator parameters where they now live.

Reproduce fixtures:
`node tools/Verify-AfterfireLabReference.cjs reports/afterfire-single-stage-20261021`

Unity checkpoint:
`DragLab.Editor.AfterfireLabVerification.VerifySingleStageAndBuild`

- **215** fidelity/funding/hardware gates pass. All ten presets at
  24/44.1/48/96/192 kHz retain numerical source parity (maximum error 1.19e-7).
  Fifty independent outlet checks allow only attenuation and travel delay;
  production-default Tempest is also checked without a calibration override.
- **46** routing gates pass, including exact single-impulse outlet, physical
  turbine boundary, boost-dependent loss, post-turbine routing, coherent shell
  response, clear and zero warmed allocations.
- **20** physical causality, **26** runtime integration and **49** mixture gates
  pass, including fuel/oxygen/heat behavior, real BOV histories and seeded RNG.
- All **47** actual race pipelines pass: **117,150** chemical events retain their
  physical traces and acoustic funding, with no dropped events or stolen tails.
  Tempest retains **5,387** events, **54** flame bursts and **132.02810505063448 J**
  funded acoustic energy, with two BOV releases and unchanged ET.
- Largest local callback p95 is **3.1427 ms** / 400 stereo frames (8.333 ms of
  audio); Tempest p95 is **2.6110 ms**, maximum **6.0820 ms**. The previous checkpoint
  measured 6.2174 ms / 4.5990 ms respectively. These local measurements are not
  Android device profiling or a guarantee against scheduler spikes; the largest
  individual callback across all configurations was **6.082 ms**.
- Windows Development build succeeds. Packaged `Assembly-CSharp.dll` SHA256:
  `30052998DC38E199277326EB7971EFF6818CE09A874E6E4E1D6FF6425629AD35`.
- Original HTML SHA256 remains `6E97825426EA143E4CF102CDB66A39E35E9D4D7D684FF531DE2A5D8D46A5CBBE`.

Real-player Tempest-only launch evidence is recorded in `listening-status.txt`.
The user must still judge the sound; automation is not listening acceptance.
Review: limiter bursts and loaded lift should retain the approved V3 crack/body
character; hardware and distance still affect power/arrival/colour without a
second full muffler response. Engine bed, induction and BOV should retain their
existing voices.
Generated PCM, fixtures, build output and full Unity logs remain local and ignored.
Earlier checkpoint receipts are preserved. Existing unrelated report edits,
attachments, Tests.ps1 and race/shop reports are left untouched.
