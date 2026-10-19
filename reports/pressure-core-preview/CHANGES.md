# Three-car combustion/exhaust core audition

2026-10-19. Requested checkpoint: retain the current audio architecture and provide
2-3 car before/after examples of a pressure-event combustion/exhaust core. This is
an isolated listening preview; no game source, authored car or save was changed.

## Comparisons

Final page: `reports/v8-youtube-reference-20261018-core-c/index.html`.

| Authored stock configuration | Engine | Induction |
|---|---|---|
| Juniper 1.6 | Inline four, 1.6 L | Naturally aspirated |
| Tempest AWD 2.0T | Inline four, 2.0 L | Turbo |
| Bison 5.0 | Crossplane V8, 5.0 L | Naturally aspirated / carburetor |

Each 14-second recording contains idle, neutral rev, loaded pull and lift-off.
The combined clips play before, 0.7 seconds of silence, then after. Inputs are
captured once and shared by both synths. These are scripted diagnostic controls,
not recordings of a player race or a prediction of car performance. Reviewed WAVs
are mono like the current game output, 48 kHz PCM16, at matched 0.10 RMS; raw gains
are retained in the comparison matrix so normalization does not hide calibration.

## Implementation

The harness extracts the current `EngineSoundSynth` and immutable frame/profile
types. The baseline is unchanged. A second extracted class replaces only the
harmonic-led combustion region and inserts the pressure core at that point.
The existing gain controls, installed `ExhaustFilter`, induction/release layers,
exhaust-pop events, clatter, overrun, master gain and output protection remain.
New block/intake vibration is added outside the installed exhaust filter.

The core uses the full user-provided Python script as a reference, archived as
`tools/audio-audition-matrix/reference-v8-user.py`. It generates pressure rise and
rarefaction pulses, runner propagation, independent bank headers, signed header
and collector reflection taps, body resonances and separate block/intake response.
It generates no broadband noise bed and reads no sampled audio.

Cylinder events use the current mechanical firing order and each actual cylinder's
contribution. Inline engines share one bank; this preview's crossplane V8 uses
alternating odd/even cylinder numbering for its two banks. Even firing positions
cover these three authored engines. The reference's specific V8 firing order is
not imposed on the current Bison mechanics. Runner lengths scale with displacement
as an authored approximation, not measured geometry of fictitious cars.

The core is a streaming C# prototype with precomputed RPM/throttle pulse tables,
interpolation, persistent filters and reusable ring buffers. No SciPy, Python,
whole-recording convolution or file access runs during audio processing. Hardware
bass/brightness/raw-pulse controls are retained. Individual failed cylinders and
cut frames suppress their combustion events while mechanical rotation can remain.

It is not a sample-exact Python port: stock car data/firing order, current output
architecture, uniform bounded microvariation, table interpolation and filter
approximations differ. The 48 kHz setup is constructed before playback. Live Unity
session/sample-rate lifecycle integration remains a later checkpoint.

## Checks actually run

- Three full current/core pairs compile and render with finite bounded samples.
- Exact same-seed replay; another private seed changes the output.
- Exact PCM invariance across 400- and 128-sample callback subdivisions.
- Correct 3000 RPM event rate, balanced cylinder sequence, individual/all-disabled
  cylinder response and suppression of cut combustion.
- Installed exhaust changes and exhaust-pop events affect all three candidates;
  turbo and release controls still affect the Tempest candidate.
- Zero measured managed allocation in warmed core `Sample` and candidate `Fill`.
- Core numeric pulse/ring/history buffers: 718,128 bytes per voice, excluding CLR
  object overhead. The adjacent performance matrix records the time for two audio seconds in a
  warmed desktop .NET run; this is not a physical Android/IL2CPP benchmark.
- Final WAV duration, sample rate, matched RMS and unclipped peak checks.
- Production source/asset SHA-256 values unchanged; Python syntax checks passed.
- Standalone build has one benign unused inherited-field compiler warning.

The selected stock paths are versioned in the source manifest. Generated media,
control traces, raw PCM and temporary C# projects stay local and ignored. No Unity
build or bench change was needed. Sound acceptance, physical Android performance,
stereo outlet presentation, other engine topologies and production integration
remain unverified; no further interface phase is advanced.

## Reproduction

Requires .NET 10 and Python with NumPy. The reference Python file is archived for
provenance and is not executed by this harness.

```powershell
python tools/audio-audition-matrix/pressure-car-comparison.py `
  --output reports/v8-youtube-reference-20261018-core-next
```

The tool refuses to overwrite an earlier output directory. Listen to the final C
page before deciding whether to adopt or adjust the candidate. Technical checks
do not establish perceptual quality. Existing unrelated worktree files are intact.
