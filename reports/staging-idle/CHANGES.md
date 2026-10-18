# Staging idle - 2026-10-18

Assisted launch used to initialize at launch RPM and hold a hidden full throttle until the first pedal input. It now starts at idle and follows the pedal throughout staging; assisted clutch operation, explicit launch inputs and the fully automatic driver's launch preparation are retained. No gauge-side idle substitution: actual stalls/failures and modeled cam/carburetor idle roughness remain visible.

Checks: 521 focused assertions across all 52 authored configurations and Assisted/Manual launch, including untouched countdown ticks, pedal revving, lift-off and real stalls. Full manual-gearbox and powertrain regressions pass (their current verification reports are updated). An initial verification tolerance incorrectly rejected the V8's existing idle roughness; corrected to the simulation's bounded 16% pulse. A test runner entry referenced a nonexistent Powertrain RunStandalone method; reran the actual suite through the checked-in wrapper, successfully. This was a test invocation error, not a game exception.

No packaged build at this intermediate checkpoint; the following requested visual increment will produce the combined builds. Existing user report/test changes remain excluded.
