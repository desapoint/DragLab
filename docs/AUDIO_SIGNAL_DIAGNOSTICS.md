# Fixed-tuning workbench signal comparison

User-approved development audition, 2026-10-21. Keep this in the browser workbench;
do not change game physics/audio or integrate a preferred candidate yet.

## Corrected-stat refresh, 2026-10-21–22

The latest local review is `reports/audio-corrected-stats-20261021/index.html`.
It retains the original two-car input clock and all frozen sound settings,
replays current v22 physics, and compares fresh PCM with the retained baseline.
All eight native source/engine/tire/combined WAVs and all 28 fixed-tuning
diagnostic WAVs remain byte-identical. These healthy configurations do not
exercise the part-capacity/rating issues corrected in `14d2703`.

The new review adds driver-input → full-simulation A/B WAVs, phase-level signal
measurements and RPM/pedal/load/cut graphs. No level matching is applied. See
[findings and validation](../reports/audio-corrected-stats-20261021/CHANGES.md).
Native trace stage labels now persist through empty-string JSON normalization;
this is report metadata only and does not change a physical signal or sample.

To refresh without overwriting the historical baseline (use Node 18+; this run
used the bundled Node 24 for UI Blob contracts):

```powershell
node tools/audio-workbench/sync-embedded-dsp.cjs
./tools/Export-UnityAudioReference.ps1 -Cases '03-i4-turbo-hiss,08-v8-carb' -InputFolder reports/unity-audio-reference-20261021 -Output reports/audio-corrected-stats-20261021/reference -Rebase
node tools/audio-workbench/preview_replay_modes.cjs reports/audio-corrected-stats-20261021/modes reports/audio-corrected-stats-20261021/reference
node tools/audio-workbench/run_signal_diagnostics.cjs reports/audio-corrected-stats-20261021/diagnostics reports/audio-corrected-stats-20261021/reference
node tools/audio-workbench/verify_signal_diagnostics_ui.cjs reports/audio-corrected-stats-20261021 reports/audio-corrected-stats-20261021/reference
node tools/audio-workbench/verify_signal_preservation.cjs reports/audio-corrected-stats-20261021
python tools/audio-workbench/compare_corrected_stats.py
```

`-Rebase` reconstructs current authored hardware, requires the same controlled
spec/components/tuning, preserves every original driver tick and recomputes the
expected results through current physics before capture. It aborts on an
unreviewed hardware change rather than silently substituting an old build or
changing sound tuning. Fixtures are written below the new output's `inputs/`;
the historical source is checked unchanged. The comparison script intentionally
asserts the observed PCM equality for this v22 checkpoint. A future sound/stat
change must review/update that contract rather than ignore a failing assertion.

## Original diagnostic workflow

Open `reports/audio-signal-diagnostics-20261021/index.html`. Tempest and the
carbureted V8 have pre-rendered comparisons. Use the configuration selector to
change car, then **Fixed-tuning signal diagnostics**. Select a pass and play its
WAV, or **Play previous → selected A/B**. Each adjacent A/B has exactly one second
of silence. Download selected WAV retains the individual full sequence.

The two chapter links below the player compare all passes in order using the
same 5–9 s neutral-rev/limiter window or 14–19 s acceleration/shift window from
each recording. Those are Unity's stage labels; the browser source is explicitly
in Race drive and does not reproduce Unity's neutral/countdown state transitions.
Each chapter has a one-second gap. There is no spoken label or added sound.

**Build comparison** captures the current workbench sound tuning, seed and
selected **Browser source driving** mode once. Choose Race drive or Neutral/dyno
to match the browser-input rendition being investigated. Changing live controls
while rendering or listening does not alter an already rendered comparison.
Rebuild to compare a different tuning or driving mode. No loudness normalization
is applied; level changes are part of the evidence. Stop the native reference
WAV/live generator when listening; the workbench enforces exclusive playback.

| Pass | Cumulative change from the preceding pass |
| --- | --- |
| 00 Browser | Cached browser driving, engine core only, dry |
| 01 RPM | Recorded Unity RPM |
| 02 Pedal | Recorded pedal used as throttle plate, matching the present adapter |
| 03 Load | Recorded Unity load |
| 04 Boost | Recorded boost and turbo spool |
| 05 Airflow | Recorded mass flow, normalized as in full replay |
| 06 Cylinders | Actual per-cylinder contribution values |
| 07 Fraction | Actual aggregate combustion fraction fed to pulse path and overall gain |
| 08 Cuts | Actual cut/two-step state and shift-pressure target |
| 09 Induction | Restore frozen intake, turbo, blower, mechanical and clatter layers |
| 10 History | Reproduce the existing full adapter's zero previous-throttle history |
| 11 Events | Actual Unity reaction funding and BOV release history |
| 12 Outdoor | Restore frozen distance, air absorption, reflections and stereo processing |
| 13 Endpoint | Existing full Unity replay with the same tuning and actual tires |

Pass 13 is an endpoint reference, not a single-signal substitution. It also uses
the full adapter's continuous RNG instead of the diagnostic source-boundary RNG
anchors. Passes 00–12 isolate the listed inputs/layers. The pipeline deliberately
reproduces current mappings; it does not fix the suspected issues while measuring
them. In particular, per-cylinder contributions can take precedence over aggregate
fraction inside the existing pulse core. Do not infer double attenuation merely
because the fraction is supplied at both interfaces.

## Isolation and limits

The source browser driver is rendered once from the original Unity throttle and
shift commands, using the existing input-only model. Its physical states and
pre-audio RNG state are cached at control-update boundaries. Candidate playback
only reads this cache. Replacing RPM cannot change later source boost, clutch
behaviour or shifts. Matching the cache's acoustic RNG state at each boundary
keeps the source baseline sample-identical despite the original browser sharing
physics and audio random draws. No substituted sound affects this source.

Hardware, engine displacement/layout, firing/runner geometry, exhaust chain,
pulse envelopes, gains, listener values and seed stay fixed. Only the documented
signals and explicit layer/processing masks vary. The input cache, trace and
settings are verified unchanged after rendering. The frozen tuning and SHA256
are retained in generated manifests/measurement receipts.

Intermediate combinations can be physically inconsistent, such as Unity RPM
with browser cut timing. They are diagnostic ablations, not proposed gameplay
states. A single change sounding worse does not establish that its simulation
value is incorrect; it may expose an acoustic mapping or an interaction. RMS and
waveform differences measure changes, not realism or user preference. No gain,
envelope, muffling, limiter, fuel map or car physics is corrected in this step.

The source cache/render is 48 kHz only. Workbench output uses the existing PCM16
writer; pre-rendered Node WAVs use PCM24. Device playback resampling is handled
by the browser's audio element. Real listening/browser/device acceptance is
separate from Node/module/UI checks. Android performance is not inferred from
this offline diagnostic tool.

## Reproduce

Requires the existing reference capture and original `.race.json` recordings:

```powershell
node tools/audio-workbench/sync-embedded-dsp.cjs
node tools/audio-workbench/run_signal_diagnostics.cjs
node tools/audio-workbench/verify_signal_diagnostics_ui.cjs
node tools/audio-workbench/verify_signal_preservation.cjs
node tools/audio-workbench/verify_reference_ui.cjs reports/unity-audio-reference-20261021 reports/audio-signal-diagnostics-20261021
```

Generated audio, manifests, HTML and raw JSON stay local. Version the source,
concise measurement CSV and verification/review report. See the
[reference capture contract](UNITY_AUDIO_REFERENCE.md) and
[delivery evidence](../reports/audio-signal-diagnostics-20261021/CHANGES.md).
