# Slice 2 plan reconciliation and final gates

2026-10-08. The original design in `objective.md` remains the vision. This document records user-approved changes to implementation scope and their effect on the final two Slice 2 phases; it does not silently move requirements into later slices.

| Original approach / earlier limitation | Approved final behavior | Evidence / final gate |
| --- | --- | --- |
| Linear upgrade ranks | Unique owned assemblies, compatible modular parts, direct upgrades/downgrades, retained inventory and individual engine elements | 2B–2D and individual-component checkpoints; 2K tests reuse/replacement after real damage |
| Estimated wheelspin / equivalent rotating mass | Independently integrated wheel contact, crank/flywheel and coupling, separate translating and rotating mass | 2F–2H; 2L confirms contact/energy and transmission regressions |
| Generic AWD / whole-car failure | Authored bias/open/LSD/locked/on-demand center and axle differentials; failures disable only their affected path; a free broken branch can consume open-center torque | AWD/driveline reports; preserve in 2K–2L matrix |
| Engine and bench-specific behavior | One authoritative road simulation in races and the physical bench; explicit acoustic overrides only in the sound bench | Shared-model and exact-replay gates |
| Recording always bounded to ten minutes | Recording optional/off by default; recorded segments continue with complete state; unrecorded runs remain bounded in memory | Bench continuation checks; 2L documents tools |
| Input or configuration selection interrupts held throttle | W/Space persist through keyboard/mouse shifts; E engages first; last pedal source owns driving sliders; independent configuration stays editable | Native race/bench checks at 2L |
| Simple wear and fuel switches | Independent None/25%/Full wear, None/25%/Realistic fuel/fluid depletion and aging, condition effects, optimal thermals and assist rules | Capture/persist difficulty; 2K tests relevant new systems |
| General damage or maintenance | Component stress/failure, independent tire life/heat, pre-failure degradation, oil/coolant quantities/leaks, limited repair and top-ups which do not heal | 2K cause, persistence, failure/recovery gates |
| Whole engine internals only | Per-cylinder/per-valve elements, ignition topology, deterministic local faults, single-element replacement/reuse/sale; paid timed offline-persistent core rebuild | 2K local/whole failure and reservation tests; final costs/time balance stays 3D/3G |
| Friction clutch treated uniformly | Dry/wet, single/multi-plate, material profiles and centrifugal clamp; separate wet oil; Automatic/CVT keep their own coupling | Individual/clutch gate plus 2K abuse/heat and 2L telemetry |
| Arbitrary tune edits | Hardware-owned fixed/tunable limits, draft Apply/Revert, buy/install auto-apply, purchased limiter/two-step/shift-light and supported EFI conversion | Part-hardware/effects and native UI gates |
| Uniform engine audio | Cylinder/layout/displacement/load/RPM, carburetor/injection, graded exhaust hardware, turbo/supercharger and release-valve profiles; limiter/two-step/fault firing envelopes remain distinct | Existing sound/DSP/WAV and live audio suites; human listening remains explicit |
| Generic transmission/upgrades | Manual H-pattern versus faster sequential manual, DCT, converter automatic and CVT; physical turbo sizes/topologies and fixed versus tunable hardware | Transmission/induction/hardware gates and the current 2L catalog matrix |
| Bench or game resets mixed with preferences | Progression/inventory/vehicles/tunes reset keeps driving/audio/units; isolated bench resets never affect owned conditions/resources | Reset/ownership/native regression gates |
| Bench controls only as ad hoc experiments | Manual/static acoustic controls, deterministic repeatable physical/sound scenarios, layered full chronological traces and three-piece engine-music easter egg | Physical/audio scenario and engine-music suites; full trace/authoring integration in 2L |
| Sparse stats and separate volume controls | Shared saved master volume, pressure/speed/torque units, summary and comparison bars/dyno, actionable warnings, visible wheel sizing and targeted maintenance | 2L UI and authoring coverage |
| Full operating costs in physics slice | Fuel consumption/mass and capped free refill now; quoted paid rebuild exception now; fuel prices, assembly valuation/salvage, repeat purchases and full career/idle balancing later | Preserve implemented service; 3A–3H remain planned |

## 2K: mechanical stress, condition and recoverable owned runs

Preserve all delivered mechanics above. Close live race checkpoint integration: capture the actual run owner, model, tick, full installed conditions, fuel and service fluids; persist periodic progress and critical failures, pause, finish/abandon/quit boundaries. Recover committed wear/fuel after interruption without resuming/paying an abandoned race. Failed saves must not publish a candidate or discard the live car; retries remain possible. Stale/duplicate/foreign/healing checkpoints are rejected. Document the finite periodic loss window for a forced process termination; do not claim disk writes every physics tick.

Acceptance includes stock survival, relevant gentle/abusive work pairs, wet/dry clutch and fluid cases, local ignition/valve versus destructive failures, brakes versus driveline isolation, finite coasting/DNF, retained failed originals, regular/compatible replacements, rebuild reservations and loaded difficulty. Export per-component definition/identity, before/after condition, work/heat/stress and failure tick/cause. Existing random intermittent faults stay deferred.

## 2L: integrated authoring, telemetry and calibration checkpoint

Close meaningful graph and authoring coverage: full chronological 120 Hz traces, engine versus shaft output, pedals/gears, measured slip/wheel speeds, normal loads, road/contact/braking forces, coupling losses, mass/fuel, tire/mechanical heat/condition, local cylinders and fluids. Provide selectable graph channels and reusable batch scenarios; reuse the existing Vehicle Workbench. Authoring and experimental bench controls must remain separate from paid hardware permissions and owned inventory. Preserve all UI/input and isolation guarantees.

Run the integrated four-car catalog/build/gearbox/induction/difficulty/failure matrix with current model data. Record stock timing/braking/rating outcomes, manual versus automatic comparisons and changed results. Validate authoring assets/scenes, migrations and final Windows controls, package source plus complete player folders, and publish requirement-to-evidence coverage. Automated developer acceptance and human driving/listening or real-device review must be reported separately. Android/mobile/controller modernization remains Slice 4; art finish remains Slice 5. Random faults, nitrous delivery and detailed combustion/locked-crank collision physics are explicitly deferred rather than claimed complete.

Integrated 2L tools and calibration contract: [INTEGRATED_TOOLS_CALIBRATION.md](INTEGRATED_TOOLS_CALIBRATION.md).

## Accepted completion

**2K and 2L are complete (2026-10-08)** against the revised gates above. [2K acceptance](../reports/phases/2k/CHANGES.md) records durable owned-run recovery. [2L acceptance](../reports/phases/2l/CHANGES.md) records the 8,877-check foundation, 1,185-run current matrix, eleven Windows workflows, tooling/calibration and packaged checkpoints. All retained approved behaviors are mapped to evidence; no behavior clarification remained necessary. Slice 3A is the next planned increment.
