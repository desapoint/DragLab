# Unity sound reference and browser replay

Requested checkpoint: implement the WAV/trace/reference-replay workflow from the
user's pasted “Reproduction HTML audio” exchange. The supplied
`DragLab_Audio_Workbench_DrivetrainFix.zip` is imported unchanged in `40d475f`
before adding replay. This work changes Editor/development tooling, not game
physics, production audio, saves, player controls or packaged builds.

## Generate and review

From the repository root:

```powershell
./tools/Export-UnityAudioReference.ps1
```

The default five cases are ordinary Tempest turbo/hiss, Bison carbureted V8,
supercharged Bison V8, Bison line-lock burnout, and Tempest ignition-cut two-step.
The script runs `DragLab.Editor.UnityAudioReferenceExport.Run` through Unity CLI,
then the independent Node checks and standalone review-page packaging. Existing
`.race.json` fixtures are read, hashed and left unchanged. Generate those with
`RaceAudioAuditionVerification.PrepareInputs` if they are absent; this exporter
does not silently regenerate/overwrite the live audition's inputs or reports.

Outputs are local under `reports/unity-audio-reference-20261021/` by default:

| Suffix | Contents |
| --- | --- |
| `.audio-trace.json` | Standalone sound inputs, events, resolved hardware and listener |
| `.race.json` | Exact source driver-input fixture, copied without changes |
| `.unity-source.wav` | Production engine/induction/afterfire DSP before outdoor propagation |
| `.unity-engine.wav` | Same engine bus through production trackside propagation |
| `.unity-tires.wav` | Four actual wheel contacts through the separate tire listener path |
| `.unity.wav` | Engine + tire buses; unchanged gains |
| `.browser.wav` | The same recorded timeline through editable browser DSP |
| `.unity-browser-AB.wav` | Complete Unity output, one second silence, complete browser output |
| `.comparison.png` | Common-scale waveform envelopes, RMS and spectrogram comparison |

All WAVs are 48 kHz / 24-bit stereo, with no per-recording normalization or
boundary fades. A frame-zero idle block binds the physical acoustic transport
before stepping the race; the final 240 audio frames hold the final active sound
inputs while published events and reflections drain. Those final frames do not
advance physics. This explicit clock differs from older audition exports that
started their controller after the first physics step.

`index.html` preloads Tempest, its matching Unity WAV and a configuration chooser.
Each configuration also has a standalone HTML page. These work directly from a
local folder; no server or external dependency is needed. Start audio auditions
the editable browser generator; the separate native WAV player auditions Unity.
They stop each other to avoid accidental overlay. Restart recorded sequence
rewinds replay. Free simulation re-enables the original driving controls.

The source workbench under `tools/audio-workbench/` can import any matching trace
and WAV manually. Physical controls and manual event injection are disabled in
reference mode; acoustic controls, solos, A/B snapshots and browser WAV exports
remain available. Hardware rebuilds restart the recorded timeline.

## Trace v1 contract

`schema = draglab-unity-audio-trace-v1`. The export is ordinary UTF-8 JSON containing
only numeric/string/boolean snapshots, never Unity object references or mutable
runtime chemistry objects.

- `sampleRate=48000`, `physicsRate=120`, `samplesPerFrame=400`, stereo. Every
  `frames[i].sample` is `i*400`; that snapshot supplies the next 400 audio samples.
- `frames[].engine` includes **every public numerical production
  EngineSoundFrame input**: RPM, pedal, load, combustion fraction, each cylinder,
  active/cut methods, spool, boost, physical airflow, charger load, intake/carb
  transients, volumes and compatibility counters. A completeness gate fails if
  production adds a field without a trace snapshot.
- `frames[].state` records actual gear, clutch, shift time, engine time, limit,
  distance/elapsed and state flags. `frames[].tires.fl/fr/rl/rr` retain separate
  intensity, sliding speed, slip, effective grip, load, dimensions and compound.
- `reactions[]` retain every `ExhaustMixtureModel.Event`, including quiet
  continuations, exact chemical time, location/zone, front identity, fuel/oxygen,
  energy, heat, pressure, rise/burn time, volume, flow and probability. `settings`
  contains the resolved hardware colour at that reaction.
- Reaction `index` is the production **zero-based event index**; BOV `index` is
  the **one-based release-history sequence**. Both are continuous.
- Reaction `seed` is a **decimal UInt64 string**, because a JavaScript Number
  cannot exactly represent all 64-bit integers. Fold to the procedural source
  seed using low32 XOR high32 of the first reaction of each front. No new browser
  random draw determines whether or when a recorded explosion occurs.
- `availableSample` retains publication/causality as well as event time. Deliver
  an event at the later of publication and `ceil(time * sampleRate)`; do not
  preload future reactions or add future energy at ignition.
- `hardware` includes all resolved public EngineSoundProfile values, per-cylinder
  runner lengths, actual firing order, bank assignment, exhaust hardware and
  path length. `enumNames` maps the serialized numeric enums to their names.
- `seeds`, `listener`, authored normalized `spec`/`tune`, input SHA256, source
  revision, Unity version and bus gains describe the capture context. The current
  listener follows the car at a fixed 12 m lateral offset, not a fixed spectator
  station while the car travels 400 m. Tire source height is separate.

Double values use 17 significant decimal digits; float values use nine. The
Editor verification reader uses exact nearest/ties-to-even decimal conversion
because older Mono/Unity JSON parsing can round some values by one ULP. This is
development-only; it adds no parser, allocations or processing to a game/player.

## Verification and fidelity boundary

The exporter replays the **serialized JSON** through fresh production C# DSP,
without calling physics, the controller or mixture stepping again. It compares
every float sample of dry engine, propagated engine and propagated tires exactly,
and verifies original ET, all events, energy funding and history/voice budgets.
It also verifies preserved gameplay RNG and source fixtures.

The browser adapter consumes the same 120 Hz snapshots, four contacts, physical
event times, seeds and delivered acoustic energy. It bypasses browser physics,
limiter/shift heuristics and probability triggers. Continuing reaction packets
replenish a source per flame/location instead of inventing a new bang per packet.
Audio callbacks can run at a different device rate; trace time stays in its
original 48 kHz clock. Offline comparison is always 48 kHz.

**Timing parity does not mean acoustic parity.** The supplied browser uses
different acoustic/core approximations, nonlinear mixing, induction layers,
afterfire envelope/pipe responses, per-wheel filtering and outdoor absorption.
It is not a port of the current C# reactive generator. The recorded physical
inputs are authoritative; the matching Unity WAV remains the authoritative
sound. A future request can align individual browser/C# transfer functions using
these repeatable inputs; this checkpoint does not silently replace game audio.

Useful checks:

```powershell
node tools/audio-workbench/verify_unity_reference.cjs
node tools/audio-workbench/verify_reference_ui.cjs
node tools/audio-workbench/verify_drivetrain_lab.cjs
node tools/audio-workbench/verify_drivetrain_limiter.cjs
node tools/audio-workbench/verify_drivetrain_shift.cjs
node tools/audio-workbench/verify_drivetrain_tires.cjs
python tools/audio-workbench/analyze_unity_reference.py
node tools/audio-workbench/package_unity_reference.cjs
```

The analysis script requires NumPy, SciPy and Matplotlib and only inspects PCM.
Use `node tools/audio-workbench/sync-embedded-dsp.cjs` after editing standalone
DSP/reference modules so the no-server HTML embeds the same inspected code.
Automated UI wiring/worklet checks do not replace browser/device listening or
visual acceptance. No Windows/Android rebuild is necessary for Editor-only tools.
