# Intake-rush preview — 2026-10-20

The user rejects the lower valve-body candidate as fart-like. This Editor-only
revision removes its coherent pressure-pulse carrier. Runtime audio, turbo and
wastegate layers, backfires, simulation and platform builds are unchanged.
Both earlier voices and all their listening files are retained. Human acceptance
is pending; do not integrate this candidate or advance the architecture plan.

## Cause and candidate

The previous valve kernel was multiplied by a strictly positive noise factor,
retaining a repeating pressure shape at valve cadence. Random gains did not
remove that coherent buzz. `Voice.IntakeRush` instead uses zero-mean valve noise
only as a faint modulation (at most 10%) of band-limited turbulent airflow.
There is no additive periodic pulse train or sine oscillator in the new path.

Positive changes in the actual gulp excitation charge the same short 75 ms
opening envelope. Decreasing gulp/closing throttle cannot charge a new release.
The filter rejects more low bass (260 Hz high-pass stage), with a broad cavity
response centered around 720 Hz and scaled by displacement per cylinder. Actual
RPM, cylinder count, airflow, throttle and volume remain inputs. Randomness stays
private and seeded. Filters/kernel/buffers are prepared once; samples reuse them.
This is an authored acoustic approximation, not measured intake geometry or an
intake valve/airflow solver. The shared gain is calibrated against original-tone
RMS in the same target window; it is not a physical SPL/perceived-loudness claim.

## Listening protocol

`timber-at-{40s,88s}-{engine,intake}-body-to-rush.wav` contains three seconds of
the rejected valve-body candidate, half a second silence, then the same three
seconds with the revised intake. The compared portions are copied exactly from
their candidate recordings, including the same crop-edge fades, with no extra
per-clip normalization. Engine-context clips retain all other production layers.

Full original-game -> one second silence -> candidate sequences cover Timber I6
carburetor, Bison V8 chambered exhaust and Juniper injection. Additional short
original-game-to-candidate comparisons remain local. Generated WAVs/manifests are
ignored; no Python generator is added to the repository.

## Verification

Reproduce with Unity CLI executing `DragLab.Editor.NaturalIntakeAudition.RunRush`
after the existing real-race captures. See `verification.txt` for synthesis/replay
checks and `artifact-verification.txt` for independent PCM/format/copy checks.
The new cadence diagnostic compares 210/420 Hz energy concentration against the
rejected body voice using the same seed, 4,200 RPM and six-cylinder fixture.
It measures the coherent buzz, not perceived naturalness. Listening decides that.
No Android device/profiling or Windows/Android build is claimed.

Final run: **28 synthesis/replay checks + 53 independent artifact gates** pass.
The valve-cadence pair fraction falls from 0.17009 to 0.00253 at 44.1 kHz
and 0.17249 to 0.00199 at 48 kHz (about 98.5% / 98.8% lower). One global
post-saturation RMS calibration gain is 13.701294250176943. Target isolated
revised/prior RMS is 1.000000 (no per-crop normalization). Later
windows may differ in level because the waveform/envelope differ. These numbers
do not establish a successful listening result. An initial compile-name collision
was corrected; the 100 ms experimental tail failed the existing decay guard and
was shortened to the preserved 75 ms opening envelope before this passing run.
