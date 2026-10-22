# Unity reference capture/replay checkpoint — 2026-10-21

Implements the pasted WAV/trace/reference-replay recommendation from
“Reproduction HTML audio.” The supplied drivetrain workbench archive was first
imported unchanged in `40d475f`; its four supplied Node suites passed before
extension. Supplied ZIP SHA256:
`60df548b218c88eae5a5c7a40cfd6032421926944ea2fe02671bbed5fbdacdd2`.

## Delivered

- Editor-only production simulation/DSP capture plus standalone numerical traces.
  Capture RPM/load/combustion/cylinder/induction inputs, each wheel, shift state,
  exact reaction/release times and publication, physical reaction properties,
  lossless 64-bit seeds, resolved hardware and listener configuration.
- Five complete sequences: Tempest turbo/hiss, Bison carburetor, supercharged
  Bison, Bison burnout, Tempest ignition-cut two-step. Native PCM is 48 kHz /
  24-bit stereo. Each case has dry engine, propagated engine, propagated tires
  and their combined mix. No normalization or source-fixture overwrite.
- The supplied HTML gains trace/WAV import and reference/free modes. Reference
  mode bypasses simplified browser driving, limiter/shift rolls and invented
  events. It supplies captured per-cylinder/per-wheel data and actual seeded
  chemical funding; numerical continuations share a flame/location voice.
  Physical controls are locked; acoustic controls, solos and same-timeline A/B
  rendering remain available. Separate native-WAV and generated playback stop
  each other rather than accidentally overlaying.
- Ready-to-open pages preload each case; index.html starts with ordinary Tempest.
  Unity/browser A/B WAVs include the complete Unity sequence, one second silence,
  then the complete browser sequence. Common-scale plots compare peak envelopes,
  20 ms RMS and spectrograms. [Contract and reproduction](../../docs/UNITY_AUDIO_REFERENCE.md).

## Checks actually run

**57 Unity gates pass**, including reconstruction of serialized JSON through fresh
production C# DSP with **bit-identical dry engine, propagated engine and tire
PCM** for every sample of all five recordings. Replay does not rerun physics or
chemistry. Exact original ET, reaction history/energy, seeds, release history,
voice/history budgets, gameplay RNG and fixture hashes are preserved.

Initial verification caught one-ULP errors in Unity/Mono JSON parsing/formatting,
including a tire normal-load value. Explicit 17-digit double / nine-digit float
JSON and an Editor-only exact decimal reader resolve this. The final complete
five-case capture/replay was rerun after correction; failed intermediate exports
are not claimed as checkpoints.

**46 independent Node artifact/replay gates pass**: synchronized 24-bit WAVs,
unchanged bus sums, audible isolated burnout tires, finite/unclipped browser
output, exact event scheduling/publication, no missing/invented releases or
reactions, captured RPM/gear/boost, finite stop, deterministic replay and immunity
to manual input, all-off silence and rejected malformed/precision-losing traces.
The comparison exposed a browser adapter bug allowing healthy per-cylinder values
to override a recorded full combustion cut. Corrected the priority, added a direct
pressure-core gate and reran the **complete final five-case browser suite**.

**7 UI/worklet contract gates pass** using the actual embedded AudioWorklet
processor, source modules and UI bindings: reference/free switching, locked
physical versus editable acoustic controls, mutually exclusive players, export
routing, advancing recorded frames/meters and blocked manual invented events.
These are code/contract checks, not native browser listening/visual automation.
Automatic in-app opening of a local file URL was blocked by browser policy; no
server, alternate browser or other workaround was used to bypass that restriction.

The supplied drivetrain, limiter, shift and tire suites all pass again after the
final changes. Standard NumPy/SciPy/Matplotlib analysis and five comparison plots
were generated; the Tempest plot was inspected. Raw PCM/JSON/HTML/PNG/ZIP remain
ignored local artifacts. The concise checks and comparison matrix are versioned.

## Results and practical limits

The browser's overall RMS is respectively **0.90 / 0.46 / 0.09 / 0.20 / 0.82 dB
lower** than Unity across the ordered five cases. These similar overall levels
are **not** proof of matching timbre: spectra, limiter envelopes, induction and
afterfire/pipe/outdoor processing still differ. The supplied browser DSP is an
acoustic approximation, not a port of the current C# reactive generator. This
checkpoint supplies controlled authoritative inputs and recordings for the next
comparison; it does not claim PCM equivalence between browser and Unity.

Validation uses 48 kHz. The adapter maps other browser device-rate clocks, but
those device paths were not listened to/profiled here. Both production code and
packaged Windows/Android players are unchanged. Windows packaged assembly hash
remains `CA2B0959534BA4C5AB7E921206324B0D5579F8775B3DA8CC453EDE71634DD5A6`.
No rebuild or broader unrelated game regression rerun was needed for Editor/web
tooling. Prior 47-race production/Android build evidence remains historical.

Unrelated existing market/save/race-audition report edits, attachments, Tests.ps1
and race-exit/shop folders are preserved and excluded from this commit.

## User review

- Open index.html; listen to its native Unity WAV and the recorded browser replay.
- Compare limiter, launch, loaded lift/BOV, shifts and shutdown on the same clock.
- Change an acoustic control or solo, restart and export A/B without changing
  the captured driving/events. Switch to Free simulation for manual controls.
- Review another engine/exhaust from the configuration selector and the separate
  burnout tire WAV. The supercharged case has no physical exhaust reactions in
  this sequence; none are invented to make the example busier.

Technical tooling checkpoint reached; human listening/browser acceptance and
any later sound-generator alignment remain separate. No later architecture/UI
phase is advanced.
