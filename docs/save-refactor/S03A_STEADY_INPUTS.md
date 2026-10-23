# S03a typed steady-output inputs

Status: the immutable input value and capture path are integrated into `NativeSteadyOutputCache`. Other preview-cache keys remain unchanged in this slice.

## Frozen API

`InductionDynamics.CaptureSteadyOutputInputs(InductionProfile source)` returns an internal sealed `SteadyOutputInputs : IEquatable<SteadyOutputInputs>`. `Evaluate(double rpm)` returns a fresh `CarState` by calling the existing `InductionDynamics.SteadyOutput` implementation with a private captured profile. No torque/boost equation is reimplemented here. The value exposes no source profile, arrays, mutable records, or Unity objects.

The evaluator preserves the source profile's actual `data` object values rather than rebuilding from `vehicle.induction`. It copies the already-computed `type`, valve, turbo/supercharger counts, size, boost setting, pulley and displacement litres, and freezes the exact evaluated `tune.PowerMultiplier`. It recursively captures the separate `factoryReference` profile; the nested factory profile's own `factoryReference` is omitted because the current steady-output equations never read it. This bounds capture depth at two profiles.

## Equality and dependency scope

Equality compares typed values with bit-exact float/double comparisons. The hash is computed once at capture from those same typed dependencies, so a cache can use the input as a dictionary key without rescanning the copied curve for each lookup. The captured dependency set includes:

- every authored torque point, in order;
- vehicle redline, fuel/ignition power limits, fueling type, cam torque bias/timing and combustion/reference compression;
- computed induction type, compressor counts, size, boost setting, pulley, litres and tune power multiplier;
- the `InductionSpec` factory type/curve-inclusion flag and atmosphere, boost envelopes/limits, flow capacities, efficiencies, intercooling, volumetric efficiency, backpressure and supercharger friction values read by `SteadyOutput`, `BaseTorque`, or `Charge`;
- the same dependencies from the independent factory-reference profile.

Unrelated vehicle identity/presentation, chassis, tires, gearbox, fuel quantity, condition, wear, runtime transients and unused tune settings do not participate. The private VehicleSpec and InductionSpec objects only hold these narrowed values; their torque-point arrays are copied and inaccessible outside the input value. Source edits after capture do not change either the key or evaluation result.

## Ownership and cost

The caller captures on the owner thread and may pass only `SteadyOutputInputs` to its cache/evaluator worker. The capture does not hold the source `InductionProfile`, `Tune`, `VehicleSpec`, or source curve arrays. Each capture creates a small bounded set of typed objects and copies the torque curve (and the separate factory curve when present); it is not allocation-free. Cache-hit capture and typed equality costs must be included in the parent cache benchmark. No full vehicle/profile graph copy, JSON, reflection serializer, or physics equation duplication is introduced.

## Verification boundary

This report records the frozen API and dependency contract, not test results or cache integration. The independent test owner should verify direct `SteadyOutput` parity across factory-induction and modified builds, equality/hash changes for each dependency, equality retention for unrelated fields, and frozen evaluation after source mutation. The coordinator owns cache integration, Unity execution and performance evidence. No car-signature, part-entry, or plan JSON key is converted in S03a.

Runtime verification: the combined batch Editor run passed 95 focused checks and 72 isolated storage/recovery checks, exit 0. [Capture-inclusive measurements, trade-offs and limits](../../reports/save-refactor/S03A_RESULTS.md) retain the cold overhead and all raw samples. Independent Luna-high approval/local commit, not this author document, close the checkpoint.
