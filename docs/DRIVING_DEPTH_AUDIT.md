# Driving depth and tooling audit

Historical audit: its missing/planned labels describe the earlier checkpoint. Current execution status and approved additions are in [IMPLEMENTATION_PHASES.md](IMPLEMENTATION_PHASES.md) and [plan reconciliation](SLICE_2_PLAN_RECONCILIATION.md).
Audited 2026-10-08 against `objective.md` and the user's fuel/mass, shared wear, bench and simulation-difficulty requirements. **2A–2E are complete.** The current build has 925 automated checks, including 93 component-failure/AWD/free-shaft checks plus reset, shared stress/destructive replay, simulation-rule, car-inspection/draft/unit and stock-durability/energy/shift checks added after the archived 650-check 2E checkpoint; the original 523-check Slice 1 build is preserved in the 2A baseline. Counts validate covered behavior, not completion of the remaining Slice 2 mechanics.

The engine/driveline and sound development foundation is usable. We do **not** yet have the complete driving depth or physical development tooling requested. Slice 2 is the required mechanics/tooling milestone, including persistent component wear/replacement and modular parts inventory. Slice 3 integrates and balances the full fuel/money/career/idle loop and part purchase/service/resale costs.

## Current coverage

| Requirement | Present behavior / evidence | Missing work and assigned slice |
| --- | --- | --- |
| Throttle, staging and shifting | Held W/Space, neutral/brake-held staging, E first engagement/upshift, manual clutch, pause/focus handling; real Windows input probe | Human driving review; controller/Android validation in Slice 4 |
| Torque/HP, ratios and engine momentum | Authored curves, derived HP, per-gear ratios/final drive, engine/core/flywheel inertia, finite synchronization; foundation/flywheel checks | Calibrate the revised wheel-contact coupling and expose player ratio tuning in Slice 2 |
| Turbo/supercharger depth | Single/twin turbo, single/twin blower and mixed hardware; lag/preload/anti-lag, limiter/two-step, heat and shared persistent internal-part failure | Measured-data calibration and complete contact integration in Slice 2; persistent repair economics in Slice 3 |
| Manual/DCT/automatic/CVT | Separate handover behavior, clutch/converter slip/lock-up and continuous ratios | Converter capacity still borrows manual clutch hardware; actual independently spinning driven wheels required in Slice 2 |
| Vehicle and wheel weight | `WheelProfile` includes wheel static mass deltas and an equivalent rotational load; race equations include axle transfer/grip, drag, rolling resistance and braking | Explicit dry/build/driver/fuel/ballast mass basis, component placement and live mass updates in Slice 2 |
| Flywheel mass | Mass/radius produce calculated inertia, RPM response, energy and neutral decay | Replacement flywheel mass does not currently alter translating vehicle mass; add that static delta in Slice 2 |
| Grip and wheelspin | Pressure/upgrade/width/load modifiers; FWD/RWD/AWD; slip estimated from excess requested force | Independent angular velocities, force-slip curve/load sensitivity, physical regrip and measured-slip traction control in Slice 2 |
| Component failures / AWD paths | Separate brake/internal/common-drive/branch consequences; front/rear grip caps and requested torque bias; fixed/open/locked/on-demand/active modes; hardware-dependent fallback; open-center disconnected-shaft inertia, free rotation, torque and power | Wheel-contact replacement and left/right differential detail in 2G–2H; individual brake circuits, seizure and wall/crash damage remain later features |
| Tire setup and burnout | Authored tire friction, pressure and simple width/stage multipliers; sound bench has an assigned-slip Burnout scenario | Tire temperature/cooldown, actual slip-driven burnout, dry surface and simple differential/load response in Slice 2; weather/full suspension later |
| Tire compound durability / race breakdowns | Tire definitions set grip; engine/driveline stress can break installed parts during racing and persist their condition | Compound-specific physical life/temperature in 2J and persistent mechanical breakdowns in 2K |
| Fuel management | Existing `FuelSystem` distinguishes injection/carburetor and limiter cuts; it is not a tank or consumption model | Tank quantity/capacity/density/mass, consumption, empty-tank behavior, saved quantity and free quantity-limited refuel control in Slice 2 |
| Component wear and replacement | Shared provisional clutch heat/slip-energy wear, gearbox/shaft/differential strength and engine-internal stress failures; race commits part conditions and replacement retains originals; isolated starting-condition/repair tools | Tire life and complete category-specific wear/cooling/interrupted-run checkpoint matrix in 2J–2K |
| Modular parts / compatibility / inventory | Four OEM recipes, shared connector fitment, separate quality/geometry, unique component identities and retained condition; actual mouse buy/install/remove/downgrade/reuse/sale workflow; atomic candidate transactions | Physics-driven condition effects/wear in 2J–2K, full calibration/tooling in 2L and price/resale balance in Slice 3 |
| Fuel cost and progression | Cash, four-event career, upgrades, records and capped idle income already exist as prototype scaffolding | Paid refuelling, operating-cost balance and complete career/business progression loop in Slice 3 |
| Repeat car ownership / partial salvage / assembly pricing | Unique vehicle/chassis/engine IDs and exclusive parents; isolated same-model fixtures work. Garage still offers one purchase per model. No salvage or assembly valuation | Repeated player purchases in 3A; detached assemblies in 3B; quotes/paid partial salvage in 3C–3D |
| Engine/sound development bench | Acoustic scene with fixed RPM/load/roller overrides and exact WAV scenarios; separate physical scene uses shared car dynamics, isolated parts, repeatable input timelines, destructive manual-downshift engine failure and optional detailed diagnostics; live driving continues through checkpointed recording segments | Independent wheel/contact and fuel behavior in 2G–2I; assigned acoustic slip/load remains outside grip validation; complete component-specific stress/cooling/checkpoint calibration remains 2J–2K |
| Live dyno and exports | Every 120 Hz sound-bench tick, chronological shifts/release, six previous-run overlays and configuration/CSV exports | Live tire forces/axle loads/slip/energy/fuel graphs from real road dynamics in Slice 2 |
| Vehicle workbench and batch tests | Stock/build curves, gear redline speed/shift suggestions, launch/final-drive trials, induction and power/tire/traction matrix | Mass/fuel/temperature/surface sweeps, individual-ratio editing and consistent physical trace comparisons in Slice 2 |
| Car stats / parts comparisons / tuning | Shared dyno and isolated driving probe; condition-aware inventory/shop previews; min/max stat bars, concrete current → proposed values and direction/tradeoff colors; practical warnings; explicit Apply/Revert drafts and shared speed/pressure units | Final simulation calibration and warnings based on independent wheel contact, fuel and full cooling as those systems arrive in later Slice 2 phases |
| Race telemetry and replay | Race summary remains 10 Hz. Physical bench records every 120 Hz input/sample, captures setup/conditions/seed and replays complete stage/shift/coast/brake traces | Integrate contact/fuel/stress fields and race AI timelines at later gates; current seeded provider is a bench foundation, not production AI mistakes |

Simulation presets and mixed parameter assists now use the shared model; None/Partial 25%/Full wear and the isolated bench multiplier affect actual condition loss. Optimal temperatures affect actual charge torque and clutch fade. AI, launch/shift modes and installed ECU protections remain independent. Apply these rules to subsequent contact, tire, fuel and cooling systems. See [simulation settings](SIMULATION_SETTINGS.md).

## What the present weight model actually does

In `Scripts/Domain/PowertrainDynamics.cs`, the current wheel profile computes:

```text
vehicleMass = stock mass × weight-upgrade multiplier + replacement wheel mass delta
effectiveMass = vehicleMass + sum(wheel inertia) / wheel radius²
```

`CarSimulation.Step` uses vehicle mass for simplified axle load transfer, traction capacity, rolling resistance and brake force; acceleration divides by effective mass. Thus wheel diameter/weight already affect gearing, acceleration, braking and clutch load. Larger/heavier wheels have verified slower-pass and manual bog/stall cases. Flywheel inertia affects engine RPM response and coupling separately.

The present grip model scales force from normal load and a coefficient, clamps front load to 15–85% and caps reported slip when traction assistance is on. It has no independent wheel speed, tire load-sensitivity curve, tire heat, fuel location or live fuel mass. Its mass profile is captured at construction. It is an approximation with specific missing interfaces, not proof that all moving-mass effects are handled.

Fuel contributes static/translating mass, not wheel or flywheel rotational inertia. A heavier build can have more normal load and still accelerate more slowly; where the weight sits also matters. Controlled comparisons must separate mass, placement, rotating inertia and tire properties rather than assume that adding weight always helps grip or removing it always improves a traction-limited launch.

## Implementation order and completion gates

Use [IMPLEMENTATION_PHASES.md](IMPLEMENTATION_PHASES.md) as the single execution order. 2A–2E have archived passing checkpoints. 2F–2L and all Slice 3 phases remain planned.

- **2A–2E:** baseline/contracts, definitions/OEM resolver, owned identities/recoverable saves, usable replacement/inventory, then the physical bench/replay foundation. This avoids calibrating opaque physics or introducing failure without a replacement path.
- **2F–2L:** mass/load, independent wheel/contact core, complete driveline coupling, fuel/refuelling, compound heat/life, other mechanical stress/failure, then integrated tools/calibration. The old demand-slip authority retires at the coupled 2H gate. Persistent tire/mechanical wear uses observed physical work.
- **3A–3H:** repeat ownership, assembly retention/part-out preparation, valuation/quotes, paid transactions/salvage/recovery, career, garage/idle, full-loop balance, then integration. Vehicle/assembly identity foundations arrive in 2C; their repeated-purchase/salvage features stay in Slice 3. Preserve physical fuel/wear instead of altering them to fake affordability.

Detailed mandatory behavior and acceptance checks are in [Slice 2](GAME_PLAN.md#slice-2--driving-depth-and-tooling) and [Slice 3](GAME_PLAN.md#slice-3--fuel-money-career-and-idle-progression). Do not mark these complete until their required mechanics, UI controls, telemetry and scenarios are implemented and verified. Current source/evidence: `DragSimulation.cs`, `PowertrainDynamics.cs`, `ClutchDynamics.cs`, `FlywheelDynamics.cs`, `Progression.cs`, `EngineTestBench.cs`, `RaceTelemetry.cs`, `Editor/DragWorkbench.cs` and [verification reports](../reports/VERIFICATION.md).


