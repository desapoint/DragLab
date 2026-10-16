# Offline engine audio auditions

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

`prepare.py` asserts authored-engine, fueling, induction, exhaust, stock-reference and cylinder-count coverage, records source hashes and extracts the existing DSP without editing it. A separate adapter captures audio profile parameters; it is not Unity deserialization or the full mechanics/fitment resolver. `Renderer.cs` uses shared scripted RPM/throttle/load inputs for both sides with phase-continuous DSP. Its authored-size/spool/boost trajectories are audition controls, not a second production physics engine or a claim to reproduce full race behavior. No player state is loaded. The proposal is generated independently in `AuditionSynth.cs` under the report folder.

All 13 authored definitions cross their stock fueling (and both available carb-to-EFI conversions) with six induction layouts and five exhaust selections: factory plus the four current presets. Focused rows add all three catalogue turbo sizes with four petrol release valves, four representative pulley ratios and three boost calibration values; other controls are held fixed. These focused sweeps are not a Cartesian product of every continuous setting. Factory means the actual authored topology, valve and exhaust, not every row labelled with factory exhaust.

Experimental cylinder sweeps cover the generator's accepted 1–16 range under each fuel-system enum; additional V/crossplane layout references are separate. They are labelled laboratory examples and hidden initially. They do not add buyable engines, establish component compatibility or claim realism for every mathematical input. Fuel kinds remain petrol/diesel. No unimplemented petrol direct injection, ethanol, supercharger rotor families or direct octane/cetane tone differences are fabricated.

The current four-stroke generator and its accepted-input limitations remain the before reference. The proposal gives crossplane V8s a deep bank voice, softens healthy petrol clatter, retains diesel mechanical texture, adds small private seeded pulse-strength variation and restrains pure compressor tones. Added third supercharger harmonics are suppressed above the sampling guard. Existing blow-off voicing is retained. TBI and port EFI can legitimately produce identical healthy scripted audio; exact identical pairs share one file without dropping their matrix entries.

Checks: finite bounded buffers for every pair; repeat/different-seed checks sampled every hundred cases; full raw-file dimensions; constant-gain loudness matching toward −20 LUFS with a −3 dBTP ceiling; every unique FLAC decoded and compared sample-for-sample with its packaged PCM; output hashes; unchanged source hashes. Each pair is before 0–10 s, gap, after 10.7–20.7 s. Each side includes idle, unloaded rev, loaded pull and lift. Passing those checks does not certify auditory realism, production callback cost or Unity playback. Full game simulation and Unity builds are deliberately outside this preview-only checkpoint.

Use `Renderer.cs`'s optional second argument and `package.py --limit N` for a small smoke run. Limits select the first N cases; a smoke package must not be reported as complete coverage. The package can resume from verified hash-matched lossless files. Re-run the renderer after changing synthesis or input logic; don't reuse stale raw buffers.
