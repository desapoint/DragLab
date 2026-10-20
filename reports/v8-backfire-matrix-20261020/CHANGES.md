# C# V8 backfire generator and comparison matrix — 2026-10-20

The user prefers the final light-cleanup / less-ticking sound from Graph Backfire
Sounds, requests seeded parameter-driven previews across engines/exhausts and
explicitly requires the equivalent in **C#**. Only backfire audio may change.
Production integration remains unapproved. Python/reference sources are local
comparison material only and are excluded from version control.

## Delivered checkpoint

- `V8BackfirePreviewModel.cs`: complete shared C# acoustic generator and adapter.
- `V8BackfireMatrixExport.cs`: authoritative race/controller/engine capture and
  exact original-PCM replay check, with actual fitted exhaust definitions.
- `V8BackfireAudition.cs`: C# numerical/seed/parameter checks and WAV rendering.
- Each WAV: **full original production → 1 s silence → C# light cleanup →
  1 s silence → C# less ticking**. No HTML and no clips cropped to isolated pops.
- No runtime engine, turbo, supercharger, tire, physics, mechanics, wear, cut,
  save/settings or authored vehicle source is edited. The new scripts are Editor
  tooling and are not connected to gameplay audio.

## Full C# acoustic equivalent

The source retains all preferred-generator mechanisms: the multi-lobed pressure
shock; filtered nonlinear body and grit; seeded small release impulses; seven
inharmonic damped shell modes; four early reflections; five low-Q cavity modes;
two irregular reflections; source spectral shaping and saturation. The V6 adapter
retains time/depth/bite/grain/amplitude variation, short hotspot coherence,
scattering, local cavity response, outward travel and quiet fixed stereo taps.
No layer is replaced by the earlier rejected simple pressure/wall prototypes.

| Cleanup | Micro impulses | Higher metal modes | Primary pressure |
| --- | --- | --- | --- |
| Light | .72 | .94 | 1.0 |
| Less ticking | .47 | .84 | 1.025 |

Butterworth analog prototype, low/high/band transforms and bilinear mapping are
implemented in C#, including the full fourth-order bandpass produced by a
second-order band transformation. Cavity peak filters retain the original tangent
bandwidth/Q formulation. Fixed numerical impulse-response fixtures validate the
filter transfer against the source design. This is not an approximate high-pass /
low-pass cascade substituted for its bandpass.

The C# port uses stable FNV-1a feature seeds, PCG32 uniform samples and Box–Muller
Gaussian samples. Master seed, stable configuration ID, event index and feature
tag separate position, timbre and propagation. A/B cleanup cannot move an event
or change its source/location RNG stream. Same-seed C# renders are sample-identical
in the tested Editor environment. Its integer RNG stream is explicit and portable;
bit-identical Python/NumPy noise or cross-platform PCM is not claimed. The earlier
local archive reproduction identified the correct preferred source; it is not a
claim that a different C# noise realization has identical PCM to that archive.

## Actual engine bed and event preservation

The archive's approximate Python engine is not used. Current Unity
`EngineSoundSynth` and the authoritative fixed-tick race/controller generate the
original audio and all RPM, boost, releases, shifts, limiter and pop events.
The exporter replays each captured fixed-tick command and checks every original
PCM16 sample. A private spec copy changes only `exhaustPopGain=0` to obtain the
otherwise identical engine bed, preserving cylinder/bank combustion, vibration,
exhaust, induction, BOV, cuts and existing condition response.

The replacement stem enters the existing-form final soft-saturation mapping.
There is no new engine gain, pitch, EQ, compressor or normalization. Where the
stem is zero, original dry samples are retained exactly. Original sections are
copied PCM unchanged, and the two quiet controls stay identical throughout.
Clip-edge fades match capture; the engine remains audible during shutdown.

Every new trigger is a published physical-controller event. No scripted event
generator invents extra race pops. Existing same-tick publication coalescence is
preserved and both physical and published counts are reported. This checkpoint
does not alter engine/controller behavior to manufacture more backfires.

## Parameter / fitted-hardware bridge

Actual event strength and per-car pop gain control level. Displacement, RPM,
exhaust temperature, sharpness and decay affect bounded acoustic time/timbre
parameters. Existing physical event strength remains authoritative; cylinder wear
is not double-applied or healed. Master seed changes only acoustics, never incoming
physics or timing. Profile selection does not perturb other seed streams.

Cause-based source-region priors are pruned to available fitted hardware and
renormalized. No phantom catalyst/resonator/silencer/turbine is added. Actual
component ordering, amounts and acoustic properties are used through the game's
**existing `ExhaustFilter` class**, including source-inside-component remaining
coverage and stage. Three origin probes per case validate this hardware path.

Only pre-turbine sources cross the preview boost-dependent turbine damping path;
downstream sources bypass it. Engine turbine/compressor/supercharger/BOV generation
is untouched. Temperature and preview geometry determine outward travel, and
all events exit the same outlet rather than receiving arbitrary random panning.

## Ten complete test configurations

| ID | Configuration |
| --- | --- |
| 01-i3-na | Sprig I3 NA, supported ignition-cut laboratory control |
| 19-injection-soft-fuel | Juniper I4 injection, soft fuel cut: exact quiet control |
| 07-i6-carb | Timber I6 carburetor, fitted exhaust |
| 30-exhaust-2 | Bison cross-plane V8 carburetor, chambered exhaust |
| 31-exhaust-3 | Same Bison V8, open pipe |
| 09-v8-supercharger | Bison R V8 supercharger, soft limiter |
| 26-launch-hard-ignition | Tempest I4 turbo, open pipe, ignition two-step / anti-lag |
| 14-i4-twincharged-dct | Laboratory I4 twincharged / DCT |
| 12-v12-twin | Laboratory injected V12 twin turbo, ignition cut |
| 23-diesel-soft-fuel | Timber diesel I4 turbo, soft fuel cut: exact quiet control |

These are isolated established audio-lab configurations, not player purchases or
catalogue edits. Old and new sections share exactly the same configuration/input.
Eight cases exercise audible events; fuel-cut and diesel retain their naturally
quiet behavior. Captures retain idle, throttle blips, neutral rev/limiter, idle
return, countdown, launch/two-step where fitted, acceleration/shifting, loaded
lift/release and six seconds of shutdown. These are existing full race-audition
sequences, not the archive's scripted approximate-engine 43.5-second sequence.

## C#-only reproduction

```powershell
unity run Game --timeout 1800 --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.V8BackfireAudition.Run --draglab-audio-wav
```

`Run` performs fresh captures and rendering. `V8BackfireAudition.RenderExisting`
is the explicit entry for the already-verified local `jobs.json`/PCM/engine beds,
used during source iteration. Missing captures are not silently replaced. The
pipeline requires Unity/C# only; no Python scripts, NumPy, SciPy, downloaded source
or WAV reference is required to rebuild the checkpoint.

`csharp-manifest.json` is the authoritative preview manifest, with current WAV
hashes and section durations. Older local Python comparison artifacts are
historical and are not build/test dependencies or versioned sources. The local
Python/reference folder is specifically ignored while its files remain intact.
Raw audio, JSON fixtures and logs remain local; only C# source/metadata, concise
evidence and design notes are versioned.

## Limits and approval boundary

This is a hybrid authored acoustic model, not a physical exhaust chemistry solver.
The game does not publish exact ignition coordinates, gas packets or exhaust
lengths. Region priors, part-length geometry, propagation/scattering and fixed
source pre-colour (muffle .26 / resonance .40) remain labelled acoustic assumptions.
They do not feed pressure, torque, heat, damage or fuel back into the simulation.
Actual downstream fitted filters apply separately; no overall engine muffling
stage is introduced.

The port currently renders prepared arrays offline in the Editor, with all tails
and overlaps retained. It is not yet connected to `OnAudioFilterRead`, and no
allocation-free callback or Android performance claim is made. Listening approval
must precede runtime integration/port placement, pooling and device profiling.
No player builds, worktree, branch switch, push or AP-00 progression occurs.
Pre-existing unrelated reports/attachments/tools remain preserved and uncommitted.

Final C# gate counts and results are in `csharp-verification.txt`.


## Final technical checkpoint

Final C# batch run passed **1,576 semantic assertions** and **67,651,200 finite /
non-clipped mixed PCM sample checks**. All ten configurations rendered: eight
with audible events and two exact quiet controls. The **657 physical increments /
638 published triggers** remain unchanged, including existing publication
coalescence. Actual fitted-filter probes, numerical source-filter fixtures,
seeded source/full-stem repeats, source parameter effects, full supported decay
range/retained tails, original prefixes, section placement/gaps and input/reference
immutability passed. A separate PowerShell artifact check confirms all ten
manifest hashes, PCM16 stereo/48 kHz headers and complete triple-section lengths.

No runtime engine/audio-generation source is modified. The Python/reference
folder is ignored and excluded from this commit, with local files preserved.
Listening acceptance remains pending; no runtime integration or platform build.
