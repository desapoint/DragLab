# Condition-driven afterfire checkpoint — 2026-10-21

Requested change: eliminate mode-triggered backfires, retain seeded uncertainty,
derive explosion strength and sound duration/character from physical conditions,
then replay the complete audible in-game test suite. This is one audio checkpoint;
human listening acceptance remains separate from technical verification.

## Physical boundary and chemistry

Engine control now publishes numeric combustion advance (BTDC), ignition firing
fraction and chamber efficiency before its existing torque-efficiency aggregate.
Two-step and anti-lag still operate their fitted controllers; those controllers
legitimately change fuel delivery/cuts and timing. Their boolean names do not
ignite fuel, lower a guessed chamber-burn fraction, or select a sharper audio bank.

`CombustionExhaustBoundary` evaluates delivered fuel/air, remaining oxygen, charge
temperature, bore, RPM, ignition timing, cylinder condition and estimated exhaust
valve opening from the installed cam. A finite flame-travel/Wiebe surrogate
estimates combustion completed before the valve opens, outgoing gas temperature
and remaining flame activity. Late ignition can therefore carry burning mixture
into the exhaust; a complete ignition cut carries unburned mixture without
inventing an ignition source. Fuel cut has already reduced actual delivery.

The eight-zone exhaust transports residual fuel, oxygen, heat and a decaying
flame-progress tracer. Seeded ignition probability applies only to an eligible
mixture without an existing flame. Once lit, finite-rate burning consumes fuel and
oxygen and is correlated through transported front identities, rather than making
each numerical reaction a new independently timed explosion. Mode/cause metadata
is retained solely for diagnostics.

Local heat release, reacting-pocket dimensions, temperature-dependent sound speed,
flow/venting and burn times estimate pressure rise, acoustic energy and envelope
duration. Chemical release is split into heat and acoustic energy with conservation
ledgers. Flame progress is not extra chemical energy or a source of fuel.

## Sound

Pressure, pressure-rise time, burn duration, source position and temperature drive
the shared pressure-source excitation, attack bandwidth, duration and continuous
burn tail. Energy-normalized source mixing includes cross-correlation of adjacent
seeded variants and compensates duration stretching. Small seeded texture changes
remain; they do not determine whether fuel exists. Sustained reactions extend the
same front's tail instead of forcing another bang on an arbitrary 80 ms timer.

Installed exhaust paths/reflections/turbine loading still filter each source.
The combustion-engine oscillator, tire sound, release valve, induction physics,
vehicle torque, resource consumption, wear and existing gameplay RNG are preserved.
No UI or later architecture phase is advanced.

## Basis and limits

Control direction follows [Haltech launch-control guidance](https://www.haltech.com/news-events/how-launch-control-works/)
and [anti-lag timing/fueling controls](https://support.haltech.com/portal/en/kb/articles/anti-lag-elite).
Temperature-dependent propagation uses the ideal-gas
[speed-of-sound relation](https://www.grc.nasa.gov/www/k-12/BGP/snddrv.html).
These sources support mechanisms, not the numerical calibration or audible realism
of this implementation.

This remains a reduced-order game model, not CFD, an exhaust pressure sensor or
calibrated SPL prediction. Spark-map conversion from the existing normalized retard
control, flame-speed constants, valve-opening inference, fixed reference gas holdup,
flammability window, acoustic radiation efficiency and tube response need measured
calibration. Cylinder cuts are still represented by their aggregate delivered
charge/firing fraction; no claim of per-cylinder combustion chemistry is made.
Exhaust chemistry observes existing gameplay and does not feed a new torque model
back into the car. A future thermodynamic powertrain change requires its own
performance/handling checkpoint. Android callback allocation is checked locally;
physical phone performance and speaker listening require device review.

## Verification and listening

Reproduce with `DragLab.Editor.ConditionDrivenAfterfireVerification.Checkpoint`.
Current receipts in this folder distinguish causality checks, runtime integration,
existing regressions and the complete 47 production-DSP race replays. Build and
visible-player launch evidence is recorded after those gates pass. Raw WAVs,
fixtures, logs and binaries remain local artifacts.

Review the same limiter, launch-control, Tempest anti-lag, exhaust, diesel,
supercharger/BOV and per-tire cases. Listen for distinct short/long bangs versus a
continuous burn, sensible strength differences and retained quiet intake. Automated
causality and replay checks cannot approve the perceptual result for the user.

## Verified results and correction evidence

- Final condition/label/physical-envelope/quench/pressure-vent checks: **20**.
- Final runtime integration (44.1/48 kHz, full fuel cut, unchanged vehicle state,
  exact seeded PCM, complete delivery, pause and zero warmed allocations): **26**.
- Shared negative cases, seed and fuel/oxygen/energy conservation: **49**.
- **47/47** final production-DSP race replays: unchanged ET/legacy physical traces,
  **117,150** fuel-funded local reactions delivered, **662** pressure-voice
  excitations, zero history overflows or tail steals. The distinction prevents
  reporting each numerical combustion reaction as a separate audible bang.
- The hard ignition-cut launch example has 5,387 local reactions sharing 54
  pressure voices; the soft fuel-cut launch example has no afterfire reactions in
  the same test protocol. These counts are outputs, not imposed quotas.
- Existing regressions pass: engine **276**, isolated legacy pop **64**, induction
  **346**, powertrain **116**, tire contact/effects **39**, tire DSP **97**, pressure
  **116**, refinements **78**, transitions **19** and preferences **10**.
  This broad pass preceded the final explicit quench, weak-packet and pressure
  integration refinements; all 95 focused gates and 47 replays were rerun afterward.
  Broad receipts are retained under `regressions/` and `reports/`; historical global
  report files were restored byte-for-byte, excluding pre-existing user changes.

The first packaged accelerated check exposed an existing harness-clock defect:
physics advanced 8x while live PCM advanced 1x. Case 03 finished with 4,022
chemical reactions and zero delivered; case 04 overflowed its history. Those
receipts are failures, not evidence of live audio correctness. The live checker
now uses wall-clock physics, checks complete event delivery after shutdown, and
its launcher derives timeout from selected fixture durations. Accelerated
coverage stays in the separate synchronous production-DSP replay, where physics
and generated PCM advance together. This corrects the test rather than hiding
overflows or changing chemistry to fit a broken clock.

The rebuilt actual game passes the previously failing twin-turbo case with
**4,306/4,306** reactions rendered, audible engine output, rival silence,
Stop/Resume and exact ET/shift/cut traces. See `live-twin-turbo.txt`. The complete
normal-speed listening run remains a separate user-review gate; do not call a
launched run a completed packaged 47-case verification pass.

The actual hard ignition-cut launch also passes with **5,387/5,387** reactions
delivered (`live-launch.txt`). The complete visible listening suite is running in
`reports/player-fixtures/live-audio-20261021-081514-589`, PID 26584. Initial live
telemetry confirms advancing input and positive engine RMS with tires and rival
silent in the engine example. Stop/Resume and Next car remain available. Human
listening acceptance is pending.

Windows is rebuilt for this checkpoint. Android is not rebuilt here; the prior
package does not contain this revision. Phone performance/listening acceptance
remains pending. No raw recordings, test saves, build products or generated
performance-test assets are added to version control.

Pre-existing unrelated work is preserved: `reports/new-parts-supply/verification.txt`,
`reports/parts-market/verification.txt`, `reports/sqlite-state/verification.txt`,
`Tests.ps1`, `.codex-remote-attachments/`, `reports/race-exit-regression/` and
`reports/shop-responsive-20261019/`.
