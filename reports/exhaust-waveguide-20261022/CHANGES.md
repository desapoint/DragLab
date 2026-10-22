# Phase 3.5 exhaust-wave / muffler integration — 2026-10-22

## Request and boundary

Implement the useful parts of the supplied, unfinished workbench incrementally with working Approximate/Simulated flags; retain the 74-subsystem list as a plan. The first runnable checkpoint is **C1: pipe/muffler acoustics**. The broader pressure/work, gas-exchange and hardware work is explicitly pending in the [integration plan](../../docs/WORKBENCH_SIMULATION_INTEGRATION.md) and [74-item Unity tracker](../../docs/WORKBENCH_SUBSYSTEM_TRACKER.csv). No claim is made that all seven browser phases, all 74 systems, real-world calibration or physical Android performance are complete.

The [original inputs](../../docs/reference/audio-phase35/README.md) are preserved byte-for-byte with hashes. The HTML is newer than its accompanying audit and still has a 3.4 page heading despite its 3.5 waveguide code. Browser Missing labels are not applied blindly to existing Unity wear, clutch, AWD, tire or moving-listener implementations.

## Implementation

- C# ports of bidirectional fractional-delay pipes, impedance-junction and termination reflections, wall loss, temperature-dependent wave speed, expansion chamber, packing absorption and resonator. Independent flags at `sound.exhaustAcoustics.pipeMode` and `.mufflerMode`.
- Null/version-zero configuration derives a **simulated pipe + simulated muffler** profile from fitted hardware, displacement and turbo presence. Explicit version one selects modes and final geometry. `ExhaustWaveSpec.Approximate()` reproduces the previous sound path. These are authoring/diagnostic flags; no new Settings or bench interface is added.
- Open systems never gain a fabricated muffler. Physical mufflers replace fitted silencer filtering; catalyst/resonator and other component transfer remain. Simulated pipes replace fixed header/collector echoes and broad exhaust band/body EQ rather than stacking both paths. Legacy bass/brightness/body-EQ controls belong to the Approximate pipe path; physical geometry has separate explicit controls.
- A single explicit hardware-default `outputGain = 2.5` converts the new pressure path to the game's established output range. The initial pass exposed a real too-quiet regression across small engines; this common scalar corrected it without changing impulse shape, per-car normalization or weakening existing checks. Explicit HTML-default reference cases retain gain 1.
- Existing per-cylinder pressure events, wear/contributions, actual ignition/fuel cuts, limiter, fuel/air state, turbo/supercharger/BOV and mechanical/intake layers are retained. Their authoritative mechanics and seeded afterfire chemistry are not replaced with browser preview controllers.
- Owner-thread captured exhaust-zone mean temperature is published as a scalar immutable audio-frame field and included in numerical audio exports/replay. The callback does not read mutable gas zones. Older traces without the optional scalar use 650 K; Approximate audio is unaffected.
- The previously reviewed reactive afterfire source and source-relative pipe/shell routing are retained, avoiding another complete pass of already processed backfire audio. A shared cycle-resolved pressure/gas network remains a separate C3/C4 increment. The new network's downstream-injection API is tested against the reference but is not another game pop scheduler.
- Prepared per-rate reusable buffers and coefficients; no allocation/lock/Unity API in the added per-sample path. Desktop F8/UI, current player ownership/resources, content stats and physics versions are unchanged.

## Verification

- **132** focused/wrapper checks pass, including original fallback PCM for five engine configurations, every independent mode combination, clear/tail retirement, dynamic/pinned temperatures, invalid input rejection, no invented muffler, deep copies, identical actual vehicle/resource state and seeded chemistry across flags, five sample rates, arbitrary block sizes, stereo/mono and zero warmed allocations.
- Existing nested regressions pass **276 engine + 10 shared audio + 64 exhaust-pop + 346 induction** contracts. They run under an isolated report root, preserving historical receipts and the user's unrelated local report changes.
- **36 independent JS comparisons** against the actual supplied acoustic classes pass: worst absolute sample difference **5.294e-23** (only inaudible denormal retirement), all four flag combinations at 22.05/44.1/48 kHz and three downstream injection positions. This establishes numerical port behavior, not physical calibration.
- **32** extra geometry/static-original-hash/listener/diagnostic checks pass. All 16 exposed parameters alter actual transfer; inactive physical geometry leaves the Approximate path unchanged. Original hashes are explicitly versioned so verification does not require ignored raw audio fixtures.
- Measured 16-cylinder turbo / 12,000 RPM / 48 kHz / 512 stereo frames: desktop Editor p95 **0.6989 ms**, max **0.7428 ms**, **0 managed bytes**, **57,344 bytes** of additional wave delay storage. Signal budget is 10.6667 ms. These are local desktop measurements, not Android dropout results.
- Editor fitted-exhaust taps are corrected for all four flag combinations and verified to leave playback byte-identical. A non-vacuous, hot, chemistry-funded afterfire run retains exact approved PCM across flags. Windows and Android builds succeed; APK signature/alignment/ZIP/ARM64 checks pass. [Artifact hashes](build-manifest.txt). Physical phone performance remains unverified.

Receipts: [focused](verification.txt), [parameters/hashes/listener](geometry-verification.txt), [JS parity](reference-parity.txt). The initial too-quiet run and final successful logs remain local/ignored.

## Listening

Every comparison uses the same eight-second idle → rev/load → shift → lift sequence before and after, separated by silence; no loudness normalization or extra spectral processing hides differences. There are five configurations: Juniper I4, Bison carbureted V8, Tempest turbo I4, Bison supercharged V8 and Timber diesel I6. Raw DSP comparisons are `*-before-after.wav`; the `*-trackside-before-after.wav` set passes both halves through the unchanged production outdoor listener at a fixed 12 m lateral distance and includes decay tails. These are controlled diagnostic sequences, not a new full 47-race audition or measured recordings.

| Configuration | Outdoor before → after | Dry before → after |
|---|---|---|
| Juniper I4 | [WAV](starter-hatch-trackside-before-after.wav) | [WAV](starter-hatch-before-after.wav) |
| Bison carb V8 | [WAV](v8-muscle-trackside-before-after.wav) | [WAV](v8-muscle-before-after.wav) |
| Tempest turbo I4 | [WAV](rally-sedan-trackside-before-after.wav) | [WAV](rally-sedan-before-after.wav) |
| Bison supercharged V8 | [WAV](super-v8-trackside-before-after.wav) | [WAV](super-v8-before-after.wav) |
| Timber diesel I6 | [WAV](timber-diesel-trackside-before-after.wav) | [WAV](timber-diesel-before-after.wav) |

Regenerate and verify using Unity CLI `run Game -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.ExhaustWaveVerification.Run`, then `.Geometry` for parameter, layer, funded-afterfire and listener checks. The immutable original hashes are already versioned; a fresh checkout does not need local baseline PCM. Run `node tools/Validate-ExhaustWaveguide.cjs` after the Unity cases produce their native impulse files. The supplied references remain unchanged. Raw before/after PCM, WAVs, screenshots, logs and packaged players are local artifacts, not Git content.

## Review gates

- [ ] Listen to V8 / turbo / supercharged / diesel and the quieter I4; approve overall character and relative engine/backfire level.
- [ ] Compare independent modes if a geometry or muffler family sounds wrong; keep vehicle inputs and other tuning fixed.
- [ ] Check the updated Android build for clicks/dropouts and live race sound on the phone.
- [ ] Approve C1 before beginning C2 pressure/work calibration or adding model selectors to the debug bench.

C1 technical completion remains separate from listening/device acceptance. Existing debug bench implementation stays intact. The pre-existing report edits, `Tests.ps1`, attachments and unrelated report folders are preserved outside this commit.
