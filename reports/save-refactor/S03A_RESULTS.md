# S03a typed steady-output preview cache

Baseline `34abd7f` on main. One dependency-specific preview cache; no gameplay transaction or writer switch.

`NativeSteadyOutputCache` now uses private typed `SteadyOutputInputs` keys instead of four JSON serializations. The inputs preserve the actual already-computed induction values and separate factory reference, and copy only the engine dependencies consumed by the existing authoritative `InductionDynamics.SteadyOutput` equations. No equations are changed or duplicated. The hash is captured once. There is no retained source profile, tune, spec or curve reference; the private evaluator cannot be modified by a caller.

This also fixes borrowed-input cache poisoning: changing source curve/data/tune after key capture, even before the first evaluated point, cannot insert a new-source result under an old key. Relevant fresh captures use new entries; irrelevant presentation/chassis/tire/unused-tune changes retain equivalent engine keys. Wear, current fluids and mechanical failures remain applied separately by CarInspection on every calculation, as before.

The outer cache is LRU-bounded to 32 configurations. Each entry has a FIFO limit of 512 RPM points. An evicted delegate remains usable and owns its private bounded entry, without reinserting it into the outer cache. Callers retaining arbitrarily many delegates can retain those independent bounded entries; this is not a global heap bound. One existing gate protects cache access; only owner-thread capture and private worker evaluation are authorized.

## Checks actually run

Windows Unity 6000.6.4f1 batch `StateCommandCheckpointVerification.RunSteadyOutputCache` exited 0: **95 focused checks and 72 isolated legacy storage/recovery checks passed**. Checks compare exact torque/boost against the authoritative direct evaluator across NA, turbo, twin-turbo, supercharger, twin-supercharger, twincharged and an authored diesel. They cover actual-data versus source-induction divergence, computed input/key changes and excluded fields, equivalent-key/hash reuse, mutation before the first evaluated point and after a hit, separate factory-reference capture, both eviction limits, evicted-delegate operation and concurrent private managed reads.

Before running, independent review corrected a false test dependency on unused `Tune.camTimingDegrees` (the engine uses resolved `VehicleSpec.valvetrain.camTimingDegrees`), expanded capture timing, required conventional even-sample medians and added before-first-point mutation coverage. These were test corrections before the first runtime run; the equations were not changed to satisfy tests. Redline remains a real conditional dependency through ServiceDynamics.CamMultiplier when cam bias/timing is nonzero.

The run did not write an active player save. The pre-existing sqlite-state report SHA-256 remains `666cc352121c3f631d4f1df0cbf159aa9fdc141db228afbc85bf438d5c804b55`. Both new GUIDs are unique, new source is UTF-8, the harness is Editor-only, and a scoped static scan confirms no JSON/reflection serializer/profile reference in the new key/steady-cache path. Other JSON callsites in the same file remain open later work.

## Capture-inclusive measured cost

[Raw twenty-pair matrix](S03A-matrix.csv), one twincharged 57-point curve using the actual CarInspection idle-to-redline 100-RPM grid. Ten direct-first and ten cache-first orders; no samples removed. Correctness exercises precede this descriptive benchmark, but benchmark-lambda/JIT/ordinary runtime variance and the first samples remain included. These are arithmetic medians and sample maxima, not percentiles, allocation measurements or phone/frame timings.

| Timed work | Median ms | Sample maximum ms |
| --- | --- | --- |
| capture_only_ms | 0.00515 | 0.06960 |
| direct_full_curve_ms | 0.07715 | 2.33490 |
| for_plus_cold_full_curve_ms | 0.09245 | 3.25360 |
| for_plus_hit_full_curve_ms | 0.00940 | 0.13100 |
| held_delegate_hit_full_curve_ms | 0.00270 | 0.05840 |

`For` includes typed object/curve capture, hash/equality and cache lookup. Its cache-hit full curve is about eight times faster than direct evaluation in this bounded Editor sample. A cold full curve is slower than direct by about 0.0153 ms median because it captures and fills the cache; the cold maximum is 3.2536 ms. These costs are recorded, not omitted. Held-delegate hits are shown separately and must not be presented as end-to-end cache cost. No prior-JSON-key timing, full inspection/UI latency, allocation bytes, retained-heap or Android/device result is claimed.

## Remaining work

Independent Luna-high review is **PASS** in [S03A_REVIEW.md](../../docs/save-refactor/S03A_REVIEW.md); local commit is pending. Car signatures, part-entry keys, plan/profile cloning, quote validation, normal row persistence and other runtime JSON manipulation remain assigned later conversion work. This checkpoint does **not** satisfy the final no-full-profile-save-clone or no-runtime-JSON gates. No UI/audio/physics tuning is changed, no packaged build is released here, and no later phase is started.
