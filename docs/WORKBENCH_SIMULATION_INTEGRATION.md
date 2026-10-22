# Workbench simulation integration — 2026-10-22

The user supplies the Phase 3.5 waveguide/muffler HTML, a 74-subsystem inventory and operating guidance, and requests incremental implementation with real Approximate/Simulated choices. Preserve the authoritative Unity simulation, ownership, wear, failures, actual hardware permissions, resource budgets and seeded behavior. The browser is a reference, not a replacement game solver or an engineering calibration standard.

## Reference scope and discrepancies

The [unmodified inputs and hashes](reference/audio-phase35/README.md) are versioned. The HTML implements newer mechanical output, gas exchange and pipe/muffler work than the accompanying older audit describes; its visible title still says 3.4. The supplied guidance audits a standalone browser, not Unity. Its **Missing** labels for clutch wear, component failures, tire thermals, advanced AWD and moving-listener Doppler must not be copied to the game: relevant Unity implementations already exist.

[All 74 items](WORKBENCH_SUBSYSTEM_TRACKER.csv) retain the original browser status separately from Unity source evidence, the remaining question and the proposed next checkpoint. This is a first-pass mapping; locating a source family is not a completed behavioral audit. No row claims calibration or all-platform performance certification.

The supplied files do not define all four remaining browser phases through phase 7. The Unity checkpoints below are an explicit implementation proposal, not invented historical approvals or a claim that the whole browser model is complete.

## Model flags and authority

- Only expose an Approximate/Simulated choice after both paths actually work. No disabled physics disguised as a simulated setting.
- C1 flags live at `VehicleSpec.sound.exhaustAcoustics.pipeMode` and `.mufflerMode`. `version = 1` means explicit modes/geometry; null/version 0 resolves current fitted hardware to both simulated acoustic models. `ExhaustWaveSpec.Approximate()` selects the exact earlier sound path. The resolved playback profile contains explicit final geometry/loss/output values.
- These are independent acoustic flags, not driving difficulty or a menu preset. They are authoring/diagnostic controls for this checkpoint; no debug bench or Settings UI is redesigned.
- Immutable audio frames carry the actual owner-thread exhaust-zone mean temperature. The audio callback never reads mutable zone temperatures or Unity objects. Explicit positive `gasTemperatureK` pins a diagnostic temperature; zero follows the captured physical value.
- Simulated pipe propagation replaces the old fixed header/collector taps and broad exhaust-band/body EQ. Simulated mufflers replace only fitted silencer filters; existing catalyst/resonator/non-silencer transfer remains. No silencer may appear on an open setup. Existing legacy body-voicing EQ controls belong to the Approximate pipe path; simulated geometry has its own explicit parameters.
- Fuel, cuts, boost, wear, driver actions, seed streams and race/resource outcomes remain authoritative. C1 changes acoustics only. Afterfire chemistry, approved reactive source and source-relative outlet/shell path are retained; do not send that already processed result through another complete pipe model.
- Acoustic-only changes do not justify changing torque statistics, save schemas, physics/replay versions or healing assets. A later physical solver needs separate calibration/versioning/migration decisions.

## Ordered Unity checkpoints

Work on one checkpoint at a time, validate and commit a runnable result, report limits and stop for user review.

| Checkpoint | Increment | Required gate |
|---|---|---|
| **C1 — Phase 3.5 pipe and muffler** | Port bidirectional fractional-delay pipes, impedance junction reflections, temperature-dependent travel, expansion chamber, packing/absorption and resonator; independent flags; preserve current simulation and earlier sound fallback. | Numerical comparison to supplied JS; exact pre-change fallback PCM; independent mode combinations and geometry/temperature response; no invented fitted parts; stable stereo/mono/chunks; zero callback allocations; performance receipt; varied-engine before/after samples; Windows/Android build and listening review. |
| **C2 — Cylinder work and crank energy** | Audit existing rated torque, friction, flywheel, engine braking and clutch reaction against the browser pressure-volume/crank code. Introduce an independently selectable pressure/work solver only after the work/energy budget closes. | Indicated work, pumping/friction/accessories and crank kinetic energy balance under idle, free rev, load, shifts, cuts and coast. Preserve nominal HP/torque and catalogue performance through explicit calibration; compare without changing unrelated audio tuning. |
| **C3 — Conserved intake/cylinder/exhaust exchange** | Audit current air/fuel accounting; implement valve pressure-difference flow, plenum/header storage, trapping, residual/reversion and exhaust pumping feedback in small separate increments. | End-to-end mass/energy ledgers, valve timing/overlap/spring limits, cold/hot and boosted/NA cases; no double-counted mass or work. Shared race and bench solver, stable time-step convergence. |
| **C4 — Cylinder events to acoustics** | Feed actual per-cylinder burn/pressure/valve events into pressure pulses, with finite partial burns, individual wear/ignition cuts and causal source positions. Review shared afterfire pressure/flow coupling separately. | Identical physics/seeded event stream for A/B; audio variations follow funded physical events; no canned mode-trigger bursts, duplicated pipe coloration or changed chemistry to force a preferred sound. Preserve approved backfire character and independent induction/mechanical paths. |
| **C5 — Fuel/induction/thermal hardware gaps** | Refine actual missing metering maps/technologies, film/rail/injector timing, diesel injection/glow/ignition delay, knock response, intercooler heat soak and aftertreatment. Each hardware family is a separate checkpoint. | Fuel and energy conservation; petrol/diesel and carb/port/direct/pump differences arise from hardware; real wear/limits/temperatures; no fictitious spark plugs on diesel or simplified turbo substitution. |
| **C6 — Remaining drivetrain/tire/environment gaps** | Audit existing clutch/dual-clutch/converter/CVT, axle/center AWD, per-wheel contact, compound/dimensions/heat/wear and moving propagation. Add only verified gaps such as road/gear/chassis voices. | Retain actual hardware capabilities, individual component identity and contact forces; isolate sounds and modes. No downgrade to browser fixed torque shares/shared slip. Lateral racing remains outside drag V1. |
| **C7 — Calibration and runtime proof** | Benchmark physics versus DSP separately, complete trace/replay coverage and tune physical/acoustic families against independent references. | Repeatability, bounded buffers and voice counts, no callback allocation/locks, worst-case thermal/limiter/afterfire stress, device dropout measurements on Android, real recordings/telemetry and user acceptance. Desktop timing alone is not Android proof. |

## Current checkpoint

C1 implementation and verification are recorded in [the delivery report](../reports/exhaust-waveguide-20261022/CHANGES.md). Technical completion and user listening acceptance are tracked separately. C2–C7 are pending and are not silently started by a passing C1 test.
