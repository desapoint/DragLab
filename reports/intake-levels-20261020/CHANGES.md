# Intake level previews — 2026-10-20

The user tentatively likes the revised intake texture but finds it much too loud.
This checkpoint keeps that exact generator, seed, envelope and input sequence,
and supplies two lower intake-only gains: **-9 dB** and **-15 dB**, relative to the
previous calibrated rush voice. The previously matched RMS was a timbre diagnostic,
not an approved final mix. No runtime audio or platform build changes are made.

## Implementation and listening

`NaturalIntakeAudition.RunRushLevels` replays the same Timber I6 carburetor, Bison
V8 chambered and Juniper injection captures. It multiplies only the prepared intake
signal by 10^(dB/20) before summing through the established output soft saturation.
The engine bed, backfires, induction, RPM, race outcome and gameplay randomness
remain unchanged. There is no whole-mix gain reduction or per-car normalization.
Final output RMS attenuation can differ slightly from the input dB value because
of output soft saturation. Other preview entry points explicitly reset gain to 0.

Each `minus9db` / `minus15db` folder contains full original-game-to-candidate files
plus short `timber-at-{40s,88s}-{engine,intake}-quieter.wav` comparisons. Those short
files contain **3 seconds previous rush -> 0.5 seconds silence -> 3 seconds quieter
rush**, copied directly from the candidate portions with identical crop fades.
Use the engine-context version to judge balance; isolated clips are diagnostic.
All earlier WAVs stay intact. Generated WAVs/manifests remain local and ignored.

## Checks and limits

See each variant's `verification.txt` for the unchanged synthesis/replay checks.
`artifact-verification.txt` records independent WAV headers, lengths, exact source
copying, original reference PCM, silent gaps, headroom and attenuation measurements.
The attenuation check compares inverse-soft-saturated isolated PCM, allowing only
PCM16 quantization error. No new timbre model or physics change is introduced.
Listening approval is pending; do not integrate or advance the architecture plan.
No Windows/Android build or Android performance/device claim is made.

Final results: **28/28 Unity checks for each level; 107 independent artifact gates pass**. Both candidates preserve source
texture to within the PCM16 quantization tolerance. Listen in engine context;
there is no silent normalization that raises either quieter variant again.
