# Tire feedback and stutter diagnosis — 2026-10-09

The requested tire audio and smoke refinement is a separate increment after Slice 3H. Race/bench physics, clutch coupling, saves and the 100 ms frame-time clamp are unchanged. Stutter findings were reported before any proposed performance fix; those fixes remain pending review.

## Delivered behavior

- Separate procedural tire audio derives from each wheel's contact force, relative surface speed and signed slip magnitude. Stationary burnouts and locked-wheel braking are supported. Unloaded spinning wheels and ordinary rolling are quiet. The old single sharp tire oscillator inside engine synthesis is removed.
- Rubber scrub and irregular squeal blend with speed; attack/release envelopes soften transitions. Tires use the same persisted master gain as engine/race/benches. They can remain audible during engine failure/coasting and race shutdown. Pause/reset clear stale audio. Acoustic bench slip controls and deterministic burnout WAV export retain the new layer.
- Four contact signals emit at the visible front/rear tire patches. Tire geometry sizing moves the anchors with the existing car setup. A procedural soft sprite and fixed 96-puff pool per car avoid per-frame object creation; smoke fades after grip recovery and freezes on pause. Garage/reset rebuild clears it. The acoustic-only bench also previews smoke from its explicitly authored slip setting on the selected driven axle; physical bench/race effects always use independent contact measurements.

## Clutch findings

112 focused checks passed across eight authored engines with manual friction-coupling fixtures (converter/CVT models were explicitly converted for this clutch audit). Partial pedal engagement lowers friction torque capacity; shaft output power falls with shaft speed while positive slip work heats the clutch. Controlled low-grip road ticks with otherwise matched engine/wheel states produced less wheel torque and less new tire-slip work with partial engagement. A fully open clutch transfers zero dry-clutch torque or at most 5 Nm of wet oil drag. Independent wheel momentum persists and contact friction slows the wheels.

For example, the three-cylinder clutch at 5,000 engine RPM / 3,000 shaft RPM transmitted 159.3 Nm fully clamped versus 29.0 Nm at a 75% depressed pedal. The latter supplied about 9.1 kW at the shaft and dissipated 6.1 kW at the friction interface. This is port power, including flywheel energy; it is not a claim that current combustion alone supplies every transient. Pedal movement has a finite response time, so requested pedal position and actual engagement can differ briefly.

A slipping clutch can still exceed available tire grip, especially after gear multiplication. Flywheel synchronization can briefly add torque. Tires already spinning can continue through a disconnected shift. No extra arbitrary slip-percentage torque reduction was added; that would double-count the existing capacity and power loss.

## Measured stutters — fixes pending

The development Windows player ran real Update/OnGUI with an opt-in timing probe and isolated saves, without a simultaneous Unity build/domain stress run. The rendered race fixture held throttle for 12 real seconds with one owned car and then eight. After entry frames were excluded:

| Rendered baseline | Frame median / p95 / worst | Simulation median | HUD median | Checkpoint median / worst |
| --- | --- | --- | --- | --- |
| One owned car | 13.93 / 20.97 / 57.34 ms | 1.06 ms | 0.44 ms | 44.50 / 54.65 ms |
| Eight owned cars | 13.93 / 20.98 / 204.18 ms | 1.07 ms | 0.45 ms | 190.66 / 201.41 ms |

Checkpoints run every 120 ticks (one simulated second). Their synchronous transaction clones/normalizes/validates the complete owned ledger, serializes JSON, flushes to disk and replaces the save/backup. The slowest measured frames coincide with this scope. Unity's allocation counter reported roughly 150 KB on ordinary driving frames, rising to about 4.7 MB and 17.1 MB on the corresponding save frames, with garbage collections. Wheel contact also allocates temporary arrays/objects inside its 960 Hz substeps. These allocations are an additional pressure source, although this capture does not isolate the exact contribution of each allocator.

The current `min(unscaledDeltaTime, 0.1)` catch-up rule discards time beyond 100 ms. An eight-car save hitch therefore causes both visible interruption and some loss of simulated elapsed time. The eight-car race reached about 11.54 simulated seconds during the 12-real-second interval. This is observed behavior, not a proposed fix already applied.

The rendered physical-bench regression had a 13.94 ms median / 20.93 ms p95, with 600 ms and 1,128 ms worst interruptions across the automated workflow. These are outside measured ordinary simulation (median 1.66 ms) and bench GUI (median 5.45 ms, p95 7.75 ms). Full CSV/JSON export/import and test screenshot/replay checks run on the main thread. A second rendered regression measured the export itself at 564.48 ms, within a 1,137.68 ms frame that also performs a complete CSV comparison. The final scoped regression confirmed CSV generation at 535.66–548.03 ms, export at 555.80 ms, and a combined comparison/export frame at 1,136.13 ms. The earlier 600 ms interruption corresponds to the first full CSV generation. Ordinary full-pull scenario preparation measured only 3.88 ms; it is not the large hitch in this workflow. Seeded/destructive scenario generation can precompute a reference simulation and remains an additional profiling target. The workflow mixes these operations with paused and running states; its aggregate timings must not be presented as continuous-driving performance. Graph code scans recorded/history traces and creates reduction lists on repaint; long-recording graph scaling remains a separate profiling target.

### Recommended next increment

1. Preserve ordered, recoverable checkpoint semantics while separating lightweight snapshot preparation from durable disk writing. Avoid rebuilding/validating unrelated ledger content on every racing checkpoint; retain revision, identity and interruption guarantees.
2. Remove measured hot-loop temporary allocations without changing independent contact/energy calculations. Re-run exact replay, contact/failure and save-recovery gates.
3. Move large CSV/JSON generation and exports out of continuous driving work, profile seeded/destructive scenario preparation, then profile long recorded/history graphs and cache peak-preserving reduction results where appropriate.
4. Revisit catch-up only after these pauses are addressed; a simple clamp removal could create a much larger simulation backlog. Preserve deterministic fixed ticks.

No save, simulation, scenario or graph optimization is applied by this increment. Timing scopes are development-fixture-only and do not set frame rate or quality. Hardware/OS/remote-desktop conditions can change timings. `GC.GetAllocatedBytesForCurrentThread()` returned unsupported zeros in this Unity runtime; allocation conclusions use Unity's `GC Allocated In Frame` recorder instead. The initial hidden-window probe lacked rendered GUI samples and is retained only as exploratory evidence, not the accepted timing baseline.

## Verification and local reproduction

- `Invoke-Unity.ps1 -Action TireFeedbackDiagnose`: 112 clutch/contact checks and eight-car neutral/loaded diagnostic timing matrix.
- `Invoke-Unity.ps1 -Action TireFeedbackVerify`: 39 contact/DSP/volume/determinism/pool/export checks passed, including authored acoustic slip and driven-axle smoke.
- Rendered baseline native race pacing and physical-bench keyboard/mouse/replay/isolated-save workflow passed with zero runtime errors.
- Native FWD/RWD/AWD warm-up: 22 checks passed, including audible DSP, rendered smoke pixel comparisons, freeze/reset and grip recovery. Smoke changed 1,541 / 1,547 / 3,015 rendered pixels in the accepted final build. Driven tires use a declared low-grip fixture; AWD is allowed to move. Non-driven locked tires dragged across the road correctly emit too.
- Native acoustic preview: 6 checks passed for zero-road-speed authored slip, shared volume/mute, freeze and reset (`reports/player-fixtures/acoustic-tire-final`).
- Updated native physical bench regression: 47 checks passed with zero runtime/GUI/DSP errors, retained held W across keyboard/mouse shifts, and exact live replay. Raw evidence: `reports/player-fixtures/bench-tire-final`; an earlier comparison is retained in `bench-feedback-regression`.
- Raw local evidence: `reports/player-fixtures/pacing-rendered-baseline`, `bench-rendered-baseline`, `tire-contact-final`, `bench-tire-final`; audio audition: `reports/tire-feedback/stationary-burnout.wav`. Raw media/logs remain untracked.

Use `Test-WindowsSlice.ps1 -PacingOnly -FrameProbe -ShowPlayer -FixtureName <fresh-name>` for race timing, or `-DrivingBenchOnly -FrameProbe -ShowPlayer` for bench workflow timing. The probe records bounded in-memory numeric rows and writes CSV on exit. Summarize with `python tools/Summarize-FramePacing.py <fixture>/frame-pacing.csv`. `-TireFeedbackOnly -ShowPlayer` tests rendered physical tire effects; combine `-EngineBenchOnly -TireFeedbackOnly -ShowPlayer` for acoustic controls. Fresh fixture names preserve earlier evidence and avoid changing the player's save.

The full Slice 2/3 acceptance matrices are retained historical baselines, not rerun for this focused presentation/diagnostic increment. Human listening and extended/device profiling remain useful follow-up checks.

Accepted focused result: 112 clutch checks + 39 effect/DSP checks + 22 native physical tire checks + 6 native acoustic checks + 47 native physical-bench regressions = **226 checks**, all passed. The final Windows player was rebuilt with these runtime changes. Existing 3H source/Windows archives are preserved as historical checkpoints; the runnable current refinement is `Game/Builds/Windows/DragLab.exe`.
