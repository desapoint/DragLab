# Natural intake preview — 2026-10-20

## Requested checkpoint

The user confirms the isolated carburetor intake-gulp tone is the Timber ding and
requests a natural replacement **for listening before gameplay changes**. This
checkpoint supplies Editor-only C# synthesis and comparisons. No runtime engine,
backfire, induction, tire, simulation, settings, asset or build is changed. No
integration or sound acceptance is implied by the automated checks.

## Candidate

`NaturalIntakePreview` replaces the standalone decaying sine/second harmonic with
band-limited airflow texture, short overlapping noisy valve-breath pressure pulses
and a broad, low-Q plenum response. It contains no sustained sine oscillator.
Valve-breath cadence follows RPM/cylinder count; mass flow, throttle and the
existing actual intake-gulp excitation control the response. Displacement per
cylinder colors an authored intake-cavity approximation. Seeded variation changes
individual breaths without consuming gameplay randomness.

No gulp excitation produces exact silence rather than a continuous new hiss.
Envelopes decay smoothly. Filters, pulse kernel and overlap buffer are prepared in
the constructor; sample generation reuses them without managed allocation. This
is an acoustic response prototype, not measured intake geometry, a valve/airflow
solver, or an Android device performance/listening claim. Intake geometry is not
borrowed from the exhaust runner data.

Only the old gulp excitation is removed on a private copied sound frame. The
unchanged production DSP renders every other layer, including its existing
backfires, limits/cuts, induction and mechanical sound. The new centered intake is
added through the same DC response and output soft-saturation law. Private control
frames/specs do not alter the authoritative race, fitted hardware, resources or
saved settings. The only prior-tool edit exposes the existing `WithoutGulp` frame
copy helper internally to avoid duplicating the complete frame-copy contract.

One shared gain, **2.5011026066074753**, matches the new raw intake RMS to the old
isolated intake in Timber's target 1.9–4.9 s window. That same gain is used for all
cars; there is no per-clip or per-car normalization. Final saturation has its usual
small level effect. This comparison should not win merely by turning the ding down.
After final saturation/edge fades, the target isolated candidate is **0.9600× RMS
(-0.354 dB)** and the full-engine candidate **1.0016× RMS (+0.013 dB)**. The raw
excitation match is exact by construction; the isolated output is not claimed to
be an exact perceptual-loudness match. The independent final-level gate is ±0.5 dB
for that target crop, rather than a stricter arbitrary 3% raw-amplitude bound.

## Listening files

Short Timber comparisons at the user's 0:40 and 1:28 positions each contain:

1. Three seconds before.
2. Half a second of silence.
3. The same three seconds after.

Both crop edges receive the same 5 ms fade to avoid edit clicks. There is an
engine-context version and an isolated-intake version at each position. The engine
context uses original production backfires, not the separate new mixture model.

Complete comparisons for Timber I6 carburetor, Bison V8 chambered exhaust and
Juniper injection contain **the full original → 1 s silence → the full candidate**.
The original section is copied byte for byte. All use the original real-race
idle/blip/rev/limiter/launch/shift/lift/finish-and-shutdown input sequence. Existing
backfire A/B and mixture WAVs remain intact. No HTML page or Python generator is
introduced. Generated WAVs and raw manifests remain local.

## Checks and limits

`NaturalIntakeAudition.Run` provides **26 checks**, including:

- Same seed repeats exactly; different seeds vary texture.
- Finite audible bounded samples at 44.1/48 kHz.
- Old fundamental/second-harmonic concentration drops from **0.99989** to
  **0.00519 / 0.00296** at those rates in the steady diagnostic.
- Exact zero output without excitation; tail decays after excitation ends.
- Zero managed allocation in warmed sample generation at both rates.
- All three original production recordings reproduce with **zero PCM mismatches**.
- Original race results and backfire counts remain unchanged.
- No gameplay RNG use and no clipped/non-finite full candidate samples.

`artifact-verification.txt` records independent output/header/PCM/gap/headroom/edge
and repeatability checks. The full files are repeated once while adding only crop
fades; their hashes must remain identical. Human listening determines whether this
candidate is more natural. No Windows/Android package or live sound is updated.
The CLI update check failed DNS resolution; the installed Unity CLI and Editor
completed the local verification.

To reproduce: generate actual local captures with
`DragLab.Editor.V8BackfireMatrixExport.Run --draglab-audio-wav`, then invoke
`DragLab.Editor.NaturalIntakeAudition.Run` through Unity CLI. Review the short
Timber engine-context and isolated-intake comparisons first; full sequences and
the other two cars check the effect in context. Stop for user approval before
runtime integration or later audio/architecture increments.
