# Offline engine audio auditions

## Current production verification (pressure core, 2026-10-19)

`python tools/audio-audition-matrix/verify-integration.py` now runs the production
pressure-core verifier: all 52 authored configurations, a synthetic 1-16-cylinder /
layout / induction grid, sample-rate coverage, replay/callback invariance, reviewed
NA-core PCM parity, cuts/wear and warmed allocation/timing checks. Pass `--output`
to choose a fresh report folder. This tests actual production DSP; Unity/player
and physical Android checks remain separate. See `docs/ENGINE_AUDIO_PIPELINE.md`.

The three-car audition tool pins its pre-integration synth to `892ea8c` so the
reviewed before/after reference remains reproducible. Earlier harmonic matrices
and reference-pressure experiments below are historical auditions; their original
source versions and accepted 13-car catalogue are retained in Git. The old
`--published-raw` harmonic parity gate was superseded by the explicit pressure-core
integration request. It is available from the earlier checkpoint rather than
silently relabelling old PCM as the current voice.

## Historical harmonic matrix

Generates an isolated, filterable before/after review library. Does not alter game code, assets, builds, profiles, settings, saves or the debug bench. Candidate voices are prototypes requiring explicit user approval before integration.

Requires Python with NumPy, .NET 10 and FFmpeg. This workstation's FFmpeg is `C:/tools/FFMPEG/ffmpeg.exe`; adjust `package.py` if using a different installation. Run from the repository root in PowerShell:

```powershell
python tools/audio-audition-matrix/prepare.py
$env:DOTNET_CLI_HOME = "$PWD/reports/audio-matrix-20261015/.dotnet"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet run --configuration Release --project reports/audio-matrix-20261015/Preview.csproj -- "$PWD/reports/audio-matrix-20261015"
python tools/audio-audition-matrix/package.py
```

Open `reports/audio-matrix-20261015/index.html`. `listening-matrix.zip` includes the viewer, all unique lossless audio, manifest, coverage and verification. Extract it before opening the viewer. `highlights/` also contains WAV A/Bs for every factory car/trim. Media, copied/generated DSP, raw buffers and build products are local ignored artifacts. The source generator, renderer, package and viewer template are versioned here.

`prepare.py` asserts authored-engine, fueling, induction, exhaust, stock-reference and cylinder-count coverage and records source hashes. The before DSP is pinned to the reviewed pre-integration commit `becf7d1d39e2715ebb08eedb0b92ae8d7793981c` so integrating the approved sound does not overwrite the reference or apply the proposal twice. A separate adapter captures audio profile parameters; it is not Unity deserialization or the full mechanics/fitment resolver. `Renderer.cs` uses shared scripted RPM/throttle/load inputs for both sides with phase-continuous DSP. Its authored-size/spool/boost trajectories are audition controls, not a second production physics engine or a claim to reproduce full race behavior. No player state is loaded. The proposal is generated independently in `AuditionSynth.cs` under the report folder.

All 13 authored definitions cross their stock fueling (and both available carb-to-EFI conversions) with six induction layouts and five exhaust selections: factory plus the four current presets. Focused rows add all three catalogue turbo sizes with four petrol release valves, four representative pulley ratios and three boost calibration values; other controls are held fixed. These focused sweeps are not a Cartesian product of every continuous setting. Factory means the actual authored topology, valve and exhaust, not every row labelled with factory exhaust.

Experimental cylinder sweeps cover the generator's accepted 1–16 range under each fuel-system enum; additional V/crossplane layout references are separate. They are labelled laboratory examples and hidden initially. They do not add buyable engines, establish component compatibility or claim realism for every mathematical input. Fuel kinds remain petrol/diesel. No unimplemented petrol direct injection, ethanol, supercharger rotor families or direct octane/cetane tone differences are fabricated.

The current four-stroke generator and its accepted-input limitations remain the before reference. The proposal gives crossplane V8s a deep bank voice, softens healthy petrol clatter, retains diesel mechanical texture, adds small private seeded pulse-strength variation and restrains pure compressor tones. Added third supercharger harmonics are suppressed above the sampling guard. Existing blow-off voicing is retained. TBI and port EFI can legitimately produce identical healthy scripted audio; exact identical pairs share one file without dropping their matrix entries.

Checks: finite bounded buffers for every pair; repeat/different-seed checks sampled every hundred cases; full raw-file dimensions; constant-gain loudness matching toward −20 LUFS with a −3 dBTP ceiling; every unique FLAC decoded and compared sample-for-sample with its packaged PCM; output hashes; unchanged source hashes. Each pair is before 0–10 s, gap, after 10.7–20.7 s. Each side includes idle, unloaded rev, loaded pull and lift. Passing those checks does not certify auditory realism, production callback cost or Unity playback. Full game simulation and Unity builds are deliberately outside this preview-only checkpoint.

## Physically driven effects section

The separately requested effects extension adds 124 before/after examples across all 13 authored cars. Capture real 120 Hz `CarSimulation.IndependentStep` behavior and the production `EngineSoundController` first:

```powershell
unity run "$PWD/Game" --editor-version 6000.6.4f1 --timeout 600 -- -nographics -executeMethod DragLab.Editor.EngineEffectsAudition.Run -logFile "$PWD/reports/audio-matrix-20261015/effects-capture.log"
python tools/audio-audition-matrix/prepare-effects.py
python tools/audio-audition-matrix/package.py
node tools/audio-audition-matrix/verify-viewer.cjs reports/audio-matrix-20261015
```

These fixtures are isolated healthy cars with programmable controls, disabled wear/resource consumption and deliberate throttle/shift/staging inputs. They do not load or alter player assets, purchase components, duplicate physics or force audio pop/cut flags. Captured exact binary frames include actual cylinder contributions, limiter/cut fractions, pop/release identifiers and pause state. Profile doubles are exported as round-trip strings. The standalone renderer replays identical frames through the pre-integration sound and current production sound, with engine-only audio and no tire mix.

Effects include carburetor overrun/backfires, ordinary throttle lift, hard/soft fuel and ignition rev limiting, two-step, petrol-turbo preload/anti-lag, actual shift handovers, cold idle and pause/resume. Unsupported carburetor fuel cuts and diesel ignition/anti-lag are omitted. Captures assert that their labelled limiting, two-step, backfire and anti-lag events actually occur and that the engine survives. Cold idle does not invent a starter sound; manual shifts do not invent ignition cuts. Effects use the authored factory exhaust and these discrete fixtures, not every possible tune/exhaust combination.

`effects-index.json`, `effects-source-hashes.json`, capture traces and raw PCM are local generated artifacts. The package retains the original 1,326 core cases, adds the effects, and verifies the current captured source hashes alongside the original reference provenance. Per-effect A/B timing is before 0–12 s and after 12.7–24.7 s, shown dynamically in the player. Strong transients can require a lower shared loudness target to preserve the −3 dBTP ceiling: both sides are matched to that same achievable level without dynamic compression. The new page previews existing effect behavior; it does not redesign the pressure-pop generator or change any further production sound.

For later HTML-only edits, `python tools/audio-audition-matrix/package.py --refresh-viewer` verifies existing media hashes and rebuilds just the page/archive without re-reading raw PCM or rerunning sound generation/normalization.

Use `Renderer.cs`'s optional second argument and `package.py --limit N` for a small smoke run. Limits select the first N cases; a smoke package must not be reported as complete coverage. The package can resume from verified hash-matched lossless files. Re-run the renderer after changing synthesis or input logic; don't reuse stale raw buffers.

After integration, `python tools/audio-audition-matrix/verify-integration.py` compares the live production synth with the hash-pinned approved reference over the full matrix. When the published raw audition cache exists, it also compares every production sample to those original buffers. On a fresh checkout the versioned generator/reference hash provides the gate without requiring ignored fixtures. This check writes its isolated compiler harness under `reports/audio-integration-20261015/`, not into Unity. It never regenerates or overwrites the listening library.
