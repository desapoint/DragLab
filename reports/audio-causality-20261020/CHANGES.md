# Audio causality and model-detail audit — 2026-10-20

## Conclusion and checkpoint boundary

**The existing audio is partly physics-driven, but it is not yet a consistently
coupled physical audio simulation. Do not sign off backfire timing or acoustic
realism from the listening matrix or passing DSP tests.** This audit covers the
production sources and the newer C# preview separately. “Physical pops” in older
capture reports means increments of the authoritative simulation counter; it does
not establish that fuel, oxygen and ignition actually caused each increment.

The only runtime correction in this checkpoint rejects fresh pop requests from a
failed/stalled engine or unavailable engine supply, and non-finite strengths. The
new source remains Editor-only. Engine, turbo, blower and tire synthesis, vehicle
torque, fuel use, wear, limiter behavior and exhaust hardware are unchanged. No
Windows/Android package is rebuilt and no listening choice is implicitly approved.

The prior boundary remains: the user reviews the new backfire audio before it is
connected to gameplay. This audit does not advance the architecture plan or PR.

## Reproduced defect and fix

`EngineBehaviorDynamics.BeginTick` evaluates carburetor throttle edges and ignition
limiter entry before its final failed/stalled shutdown. These paths could increment
the pop counter on a stopped engine. `AudioCausalityVerification.Run` reproduced
**six failing cases** (failed, stalled, unavailable supply × opening/closing gas).
The common `Pop` entry now rejects these fresh requests. Its comment explicitly
distinguishes them from a future retained hot-gas/afterfire model: residual gas
combustion after shutdown must not be implemented as a new throttle-edge request.

The original sound controller already muted failed/stalled engines, so this was
primarily an inconsistent event/telemetry path, not evidence of audible explosions
from every stopped engine. Supply-unavailable state alone was not checked there.

Before evidence is preserved in `before-verification.txt`. Corrected focused gates
are in `verification.txt`; broader suite results are in `suite-summary.txt`.

## Production/preview detail matrix

| Layer | Actual inputs and implemented response | Missing detail / validation limit |
|---|---|---|
| Combustion pulses | `PressureCombustionCore`: four-stroke event rate, a firing-order table, cylinder contributions, load/throttle/boost-dependent prepared pulse envelopes, fractional runner arrival and independent banks. | Audio integrates its own smoothed-RPM phase rather than consuming simulation crank-event timestamps. Orders are selected by cylinder count; bank assignment is index parity, not individually authored engine geometry. Even firing is assumed. Flat-plane/odd-fire architectures cannot be certified from a cylinder count. Pulse pressure is normalized/calibrated, not cylinder-pressure units. |
| Wear/misfire | Published per-cylinder contribution affects pulse amplitude; shared mechanical state affects available torque/load. | Reduced contribution is not a discrete failed-ignition/fuel-delivery event carrying unburned mixture into the exhaust. It cannot yet predict afterfire chemistry. |
| Limiter/shift cuts | Controller publishes actual active cut method, combustion fraction and explicit transmission cut. Full cuts suppress pulses; soft limiting preserves partial sound. | Fractional cuts are largely amplitude control, not a shared per-cylinder cut schedule. Fuel versus ignition does not currently create distinct retained fuel/oxygen packets. Mechanical gear disconnection must not be equated to an ignition cut. |
| Carb/injection/diesel | Shared fueling behavior controls roughness/hesitation and limiter permissions; diesel uses fuel cut and distinct clatter. | Carb opening currently sends a `Pop` to the exhaust layer; no intake-versus-exhaust event location is represented. Fuel-system enum/coloring is not a fuel-specific combustion/ignition-delay solver. Do not invent petrol-style BOV/backfires on the diesel to fill an example. |
| Backfire occurrence | Carb opening/lift edges, ignition-limiter entry, ignition two-step cadence and anti-lag cadence increment simulation state. Fuel-cut entry does not itself request an ignition pop. | Fixed timers/edge heuristics; no unburned-fuel inventory, oxygen availability, transport/residence, ignition condition, fuel-limited released energy or explicit source site. Cold exhaust/hot exhaust and fuel-cut recovery cannot be accurately inferred from the event counter. The new eligibility guard fixes only invalid running-engine requests. |
| Event delivery | Immutable sound frames cross the main/DSP boundary. Counters prevent repeatedly inventing events from unchanged state. | **Measured: three physical requests become one published trigger; five increments between updates also become one.** Last strength overwrites earlier strengths. Latest-frame publication and DSP block boundaries add another coalescence/latency point. BOV release transport has the same counter-to-single-trigger structure. No lossless timestamped queue exists. |
| Production pop waveform | `ExhaustPopSynth`: seeded pressure transient + turbulent band, displacement/sharpness/decay, continuous short retrigger tail; fitted exhaust transfer follows it. | One retriggerable voice with a short continuity tail, not independent overlapping energy/tails. No event chemistry/site/path metadata. Its private fixed seed is reproducible but not the new per-event seed scheme. |
| New C# backfire preview | `V8BackfirePreviewModel`: pressure front, nonlinear body/grit, secondary texture, shell/cavity modes, reflections, fitted exhaust filtering and reproducible per-event streams. | **Acoustic model only.** Uses current published heuristic triggers. Cause is inferred from flags/nearby shifts, gas temperature is estimated, source region is weighted/randomized and section lengths are assumptions. Load/airflow fields being captured does not mean a fuel/oxygen/energy model consumes them. Secondary crackles are source texture, not independently simulated ignitions. Cannot certify correct event timing by changing this waveform. |
| Turbo | Shared `InductionDynamics` provides rotor/spool proxy, actual boost, charge temperature/airflow, hardware/count, release events and anti-lag exhaust energy. `TurboAcoustics` uses those inputs; hot-side damping changes with pressure/flow. | Spool is normalized first-order response, not measured shaft RPM or turbine/compressor maps. Blade frequencies and turbulent bands are calibrated. Release shape/type is an envelope, not a solved pressure wave/surge cycle. Anti-lag energy itself is a control proxy. |
| Supercharger/twincharge | Actual blower boost/drive torque, RPM, ratio/count and shared bypass behavior affect whine/load; twincharge keeps separate turbo/blower inputs. | Ratio and harmonics synthesize a voice; tooth/lobe geometry, outlet pulses, belt modes and acoustic bypass flow are not solved. Two-rotor detuning is artistic calibration. |
| Exhaust/fixtures | Fitted serial components apply attenuation, low-pass/softening and resonant EQ. Combustion uses runner delay, collector reflections and bank width. | Not a coupled wave/mean-flow/material solver. Geometry, temperature and wall vibration mostly use prepared approximations. New preview shell vibration is not a measured fixture/mount model. Upstream/downstream source location matters and cannot be replaced by one global EQ. |
| Intake/block | Cylinder-related mechanical pulses, roughness/hesitation/intake-gulp/clatter layers use engine state. | Several supplementary sine/noise colors, including a two-step sine layer, are not emergent mechanical vibration/acoustic propagation. Preserve engine sound pending separate approval; label these honestly. |
| Tires/road | Independent FL/FR/RL/RR contacts; relative tread/road speed, force and slip gate intensity. Braking skids count; unloaded spin/rolling do not create screech. Compound, width, radius, normal load and effective friction color four voices. | Friction-work magnitude is an excitation proxy, not a solved stick-slip/carcass/tread-vibration model. One authored Asphalt profile. No rolling-noise, loose-surface or detailed tire-cavity solver. No sound is inferred just from requested engine torque. |
| Finish/pause/settings/device | Separate tire source can survive engine failure; finished running lanes remain audible. Immutable frames and prepared buffers protect DSP ownership. Shared levels/category mute are presentation controls. | Latest-frame delivery must be fixed for discrete events. Existing pause/device transitions intentionally clear state; retained hot-gas tails need an explicit policy. Editor timing/allocation tests do not establish Android callback or speaker quality. |

## Required causal contract before backfire integration

1. **Produce events in shared simulation, not the sound generator.** Track actual
   cylinder fuel delivery, burned fraction and oxygen-bearing flow through bounded
   exhaust zones. Distinguish fuel cut, ignition cut, retarded burn, misfire and
   throttle/shift actions. A shift alone is not evidence of an explosion.
2. **Carry residual mixture forward.** Advect/retain and consume fuel/oxygen using
   mass flow, temperature, hardware path and time. Derive ignition eligibility,
   location and finite released energy. Fresh fuel cut must not fabricate fuel;
   remaining fuel after cut/recovery must not be discarded. Distinguish intake
   backfire and exhaust afterfire instead of routing both through the tailpipe.
   Start with an explicitly calibrated reduced-order model, not pretend CFD.
3. **Transport every event.** Bounded prepared event history/queue with sequence,
   simulation timestamp, cause, location, energy/pressure, duration and seed. Define
   overflow counters, reset/replay/session ownership and safe publication. Preserve
   multiple events per fixed tick and intervening ticks; schedule within DSP blocks
   and sum independently decaying voices. Do not fix loss merely by incrementing
   `popId` by a larger number: that still supplies only one strength/time.
4. **Render from those values.** Reuse the approved C# pressure/texture core and
   actual source-to-outlet path. Seed bounded acoustic variation independently of
   career/AI randomness. Randomness cannot substitute for absent fuel, oxygen,
   temperature or a physically reachable source region. Keep the engine bed and
   induction voices intact while reviewing backfires.
5. **Apply the same accountability to every layer.** Share cylinder cut/crank
   scheduling and author firing/bank geometry where necessary; retain physical
   induction/contact state. Treat calibrated resonances/noise as acoustic response,
   not new simulation forces. Equal quality means consistent causality and explicit
   accuracy limits, not equally expensive solvers for all layers.

Acceptance cases: no fuel/oxygen/ignition eligibility → no new afterfire; residual
mixture can burn after a control change only while inventory permits; ignition cut
versus fuel cut, carb versus injection/diesel, cold/hot path, normal/manual/DCT
shifts, anti-lag/preload and locks, single/overlapping events, source before/after
each fitted component/turbine, stall/failure, finish/coast, pause/restart, replay,
8–120 Hz publication and varied DSP blocks/rates. Assert all event IDs delivered
once, no energy creation, zero callback allocation, same-seed repeatability and
no gameplay RNG/state mutations. Compare audible results separately with the user.

Use measured references/validated hardware presets for physical accuracy claims;
response-to-parameter and finite-PCM tests alone cannot prove real-world fidelity.

## Verification and scope

`AudioCausalityVerification.FullAudit` runs the focused gates, isolated existing
engine/pop/induction/powertrain suites, per-contact effects, pressure/refinement/
transition suites and tire DSP checks. See the saved summaries for actual counts
and callback measurements. These tests validate bounded output, state ownership,
parameter response, cuts, determinism and contact behavior. Known model gaps above
are reported as **OPEN**, never counted as passing physical-accuracy tests.

No new live phone listening/profiling, measured vehicle microphone comparison,
runtime new-generator test or Android build is claimed. Old listening WAVs remain
intact. No UI review is required for this source/audit checkpoint; review consists
of the model gaps and new causality contract, with audio acceptance still pending.

Rerun results: **14 focused**, **276 engine**, **64 existing pop**, **346 induction**,
**116 powertrain**, **39 contact/effects**, **116 pressure**, **78 refinement**,
**19 transition** and **97 tire DSP** checks pass. All **47 real-race input cases**
also pass with exact pre-correction physical/timing/cut traces and event counts;
the new guard changes only unavailable-engine requests. The pressure callback
p95 is **0.5116 ms**, maximum **0.5958 ms**, zero managed bytes on this Editor host.
Captured current engine/pressure evidence is copied here; historical report bytes
are retained. Unity CLI update checking failed DNS resolution, so the installed
CLI was used successfully. The initial red six-case reproduction is an expected
before-fix result, not an unresolved failure of the corrected checkpoint.

## Engineering references

These support the distinctions in the proposed contract, not validation of this
game's coefficients or a claim that a full FEM/CFD solver is required on Android:

- [COMSOL automotive muffler model](https://doc.comsol.com/6.4/doc/com.comsol.help.models.mph.automotive_muffler/automotive_muffler.html): duct/chamber propagation and temperature-dependent properties.
- [COMSOL aeroacoustics interface selection](https://doc.comsol.com/6.3/doc/com.comsol.help.aco/aco_ug_aero.08.108.html): background flow, temperature and acoustic model scope.
- [COMSOL acoustic/structural muffler example](https://www.comsol.com/blogs/multiphysics-simulation-provides-accurate-muffler-designs): coupled wall vibration is additional physics, not merely EQ.
- [SAE flame-type afterburner abstract](https://saemobilus.sae.org/papers/development-flame-type-afterburner-620399): exhaust composition/temperature/configuration affect sustained combustion. Abstract only; not used to select numerical thresholds.
