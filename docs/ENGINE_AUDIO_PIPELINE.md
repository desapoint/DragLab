# Engine audio: pressure events within the existing pipeline

User direction, 2026-10-19: integrate the reviewed pressure core across the current
engine roster. Retain the existing audio architecture, wear, cuts, exhaust upgrades
and induction sounds. Add missing acoustic detail from the supplied turbo script;
do not replace or simplify the existing induction simulation.

## Simulation stays authoritative

`InductionDynamics` retains size-dependent spool/coast, exhaust-energy and load
response, unit flow capacities, compressor efficiency, charge temperature,
intercooling, backpressure loss, twin-turbo factors, supercharger drive losses,
series twincharging/bypass and pressure-release events. That implementation and
the engine/vehicle physics are unchanged by this audio checkpoint.

`EngineSoundController` publishes immutable RPM, throttle, load, per-cylinder
contribution, combustion/cut state, turbo spool/boost, blower boost/drive load,
release/pop counters and actual intake mass flow. DSP never writes physics,
health, resources or gameplay RNG. The Python demo's turbo-state integrator,
scripted rev trajectory and independent recording normalization are not runtime
simulation or gain controls.

## Audio path

```mermaid
flowchart LR
    P[Simulation snapshot] --> C[Per-cylinder pressure events]
    C --> H[Runners, banks and collectors]
    H --> T[Boost-dependent turbine response]
    T --> E[Installed exhaust filters]
    P --> A[Existing pop and overrun events]
    A --> E
    P --> I[Compressor, blower and release layers]
    H --> I
    E --> M[Existing master gain and output protection]
    I --> M
    C --> B[Block and intake vibration]
    B --> M
```

The source uses the current mechanical firing order and actual cylinder identity.
Current inline engines share one collector bank; the current crossplane V8 uses
odd/even banks. No random missing combustion, fake limiter event, shifted RPM or
healed cylinder is introduced. Synthetic 1-16-cylinder/layout combinations are
stress coverage, not additional authored engines or measured real-world geometry.

## Turbo additions

| Detail | Added acoustic response |
|---|---|
| Exhaust/turbine interaction | Pressure-derived lower-mid turbine texture; boost/load affect pulse strength and runner arrival |
| Compressor layering | Shaft-following fundamental/blade partials, two filtered airflow bands, size-dependent acoustic pitch |
| Boost-dependent exhaust | Smooth pressure-dependent hot-side damping and pulse character; use turbo boost separately from blower pressure |

Actual mass flow includes the simulation's existing density, temperature, capacity
and wear constraints. Audio-only smoothing interpolates snapshots; it does not
reintegrate a competing shaft, manifold or boost state. Hardware counts, blower
ratios and current blow-off valve modes remain supported. These remain authored
acoustic approximations, not CFD or measured turbo blade geometry.

## Runtime preparation and Android

Pulse tables are shared by sample rate with a bounded cache. Voice filters,
reflection/ring buffers and coefficients are prepared before playback. Live
`EngineAudio` sessions prepare only the device's rate. A device-rate change is
published on the main thread while retaining the controller and current snapshot;
the callback silences any intervening mismatched block. It does not allocate or
construct tables from the audio thread.

The public synth keeps prepared 44.1/48 kHz compatibility for existing offline
callers. Other rates must be prepared explicitly before `Fill`; supported rates
are 8-192 kHz. Development players expose callback timing counters without adding
a new user-facing interface. Physical Android profiling remains distinct from
desktop/Editor timing and successful ARM64 compilation. See the checkpoint report
for the checks and build evidence actually obtained.
