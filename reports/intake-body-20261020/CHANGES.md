# Intake-body listening revision — 2026-10-20

The user reports that the previous intake-ding replacement sounds like a wastegate.
This checkpoint revises only the Editor listening prototype. Runtime audio,
backfires, turbo/wastegate layers, simulation, assets and builds remain unchanged.
Human listening approval is pending; no architecture phase advances.

## Candidate and comparison

`NaturalIntakePreview.Voice.ValveBody` removes the continuously mixed airflow bed.
A shorter 75 ms opening envelope excites noisy valve-cadence pulses with a broader,
lower cavity response. Positive changes of the existing gulp excitation charge
that envelope; a closing throttle does not create a new release burst. RPM,
cylinder count, displacement, actual flow and volume still shape the response.
Prepared buffers/filters and a private seeded stream are retained. This is an
authored acoustic prototype, not a valve/airflow solver or measured car sound.
The previous Airflow voice remains available and is the default of the old tool.

One shared gain, 57.139232165491194, matches post-saturation isolated intake RMS
against the original Timber tone in the 1.9–4.9 s target window. That large scalar
compensates for the much shorter/weaker raw excitation; it is not a physical SPL
calibration. It is reused for all cars/windows without per-clip normalization.
Against the rejected preview, target intake RMS is 1.041637×; at the second window
it is 1.259975×. Neither is a claim of equal perceived loudness. The isolated
>1500 Hz energy fraction is 0.227296 -> 0.239034 in the first window and
0.236015 -> 0.252862 in the second. Thus these measures do not establish a lower
high-frequency fraction: the intended difference is pulsed body versus sustained
airflow, and listening must determine whether that resolves the complaint.

Four `timber-at-{40s,88s}-{engine,intake}-hiss-to-body.wav` files contain the exact
previous candidate for 3 seconds, 0.5 seconds silence, then the revised candidate
for the same 3 seconds. No extra normalization/fades are added beyond the common
5 ms crop-edge fades already applied to each source. Original-game-to-new short
comparisons and full Timber I6 / Bison V8 / Juniper injection sequences are also
available locally. All other production layers, including original backfires,
remain in the engine-context versions. Existing listening WAVs are preserved.

## Verification and reproduction

Unity CLI `DragLab.Editor.NaturalIntakeAudition.RunBody` completes with **26 checks**:
seed repeatability/variation, finite bounded output, >85% reduction of the old
150/300 Hz pair concentration, silent zero excitation, decay, zero warmed
per-sample managed allocation at 44.1/48 kHz, exact original production PCM,
unchanged race results/pop counters and no gameplay RNG consumption.
The pair measurement uses the initial 0.25 s for this transient voice rather than
measuring a fully decayed tail. It is a tone-concentration diagnostic, not a
naturalness criterion.

Independent local artifact verification passes **53 gates**: 48 kHz stereo PCM16,
full/crop lengths, exact original/reference PCM, headroom, silent gaps, faded
edges, exact rejected/revised crop copying and unchanged prior full-file hashes.
See `verification.txt` and `artifact-verification.txt`. Generated WAVs/manifests
remain ignored. No Python generator is added. No Android profiling/device test,
Windows/Android build or runtime integration is claimed. Stop for listening review.
