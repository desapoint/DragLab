# Wear, valvetrain, fluids and service — implementation report

Implemented the approved proposal in the Unity project and rebuilt `Game/Builds/Windows/DragLab.exe` on 2026-10-08. This advances Slice 2K–2L; it does not declare the remaining integrated slice gates complete. Full cost/career/idle balancing remains Slice 3.

## Changes

- Added **132 compatible part definitions** across 16 valvetrain/service categories; catalog now has 625 definitions. Cam profiles change the torque curve, fuel demand and idle voice. Spring control depends on cam demand and condition; valve float can reduce output and damage valves. Adjustable timing drives unlock ±6 crank degrees, with fixed hardware retaining its calibration and normal draft Apply rules.
- Added deterministic pre-failure losses for relevant components: sealing, ignition, breathing/deposits, cam/follower/timing behavior, bearing friction, metering waste and oil pressure. Durable new hardware has actual service-life/thermal effects. No universal wear-to-fuel multiplier or duplicate clutch-slip fuel penalty was added.
- Added persistent oil/coolant quantities, temperature/condition, pressures and loss counters; actual liquid mass affects the car once. Pans, pumps, filters, radiators, caps, hoses and oil coolers have applicable capacity/flow/cooling effects. A serviceable leak remains distinct from a destroyed component. Simulated engine-off cooldown allows ongoing leaks without offline wall-clock drain.
- Added capped top-ups, oil/filter changes, coolant renewal, excess-oil draining, cleaning, clearance adjustment, resealing and limited reconditioning. Ring-only service needs a healthy enough bore. Structural wear remains after leak repair; used filters/parts remain inventory identities. Severely damaged/failed hardware needs replacement. Failed springs/followers stop combustion without automatically destroying the entire engine; complete core damage cannot be masked by an unset failure flag.
- Added independent None/25%/Full fluid depletion/aging and condition-effect preferences, plus manual/automatic pre-race top-ups. Reset retains preferences. All physical simulation runs use the same solver; isolated bench authoring, live diagnostics, graph tabs and complete CSV/replay state cover fluids and faults without changing pedal ownership.
- Added capacity/cam/spring summary comparisons and practical low-fluid/leak/spring warnings. Per-part service and failure labels distinguish serviceable faults from depleted health. Pressure follows the shared unit preference.
- One-time migration initializes factory service equipment and fluids while retaining previously owned identities and conditions; later loads preserve empty fluids and deliberately removed parts. Atomic transactions/checkpoints reject failed saves, unauthorized refills and rewinds.

## Where to use it

**Garage → fluids** or **Parts → Repairs**: choose top-up quantities, fill installed capacities, change oil/filter, renew coolant and inspect repair actions. Parts categories expose the new hardware and previews. Garage cam-timing tuning needs an adjustable timing drive and Apply.

**Settings / Driving**: fluid depletion, oil aging, condition effects and service assistance are independent of fuel use, mechanical wear and driving assists.

**Physical driving bench → Show detailed diagnostics**: oil/coolant sliders, temperature/condition, live pressures/losses, top-ups and cooldown. Per-part starting deposits/leaks/clearance appear where applicable before engagement or while paused. Keyboard control remains authoritative for pedals. Wear reset leaves liquid quantities unchanged. Disable detailed diagnostics to view the selected live graph; enable recording for full traces and overlays.

Captures use `draglab-service-fluids-v13`. Record fresh scenarios for the new model; older recordings explicitly report incompatibility. Live DSP callbacks retain their existing timing limits; exact PCM comparisons remain in the acoustic WAV bench.

## Verification

- Full regression/build gate: **4,901 foundation checks passed**, including the initial 536 service checks, followed by a successful Windows development build. [Full gate](../unity-verification.txt).
- The final standalone part-effects gate also passed, and confirmed the updated Unity process-wait helper completes normally.
- Final failure-boundary refinement: **548 focused service checks passed**, followed by a fresh Windows player build. This targeted gate covers the final valvetrain/core-failure behavior; the 4,901-check run precedes that small refinement. [Focused checks](verification.txt), [four-car scenario matrix](matrix.csv).
- Four stock quarter-mile passes per car with retained component/fluid state and physical thermals. Every new definition resolves and ticks with finite stats; tests also cover worn output/efficiency, cam/spring/timing behavior, leaks/cooldown/venting, oil starvation, failed oil coolers, repair eligibility, save migration, empty quantities, part swaps, rollback and exact JSON replay.
- **34 real Windows shop checks**, including mouse top-up/reseal/oil service, retained filters, cam/spring warnings and actual race/save integration. [Shop fixture](../player-fixtures/service-fluids-shop-final/runtime-verification.txt).
- **38 real Windows physical-bench checks**, including held W through shifts, keyboard/mouse takeover, paused oil sliders/top-ups, non-refilling wear reset, full chronological traces and exact replay. [Bench fixture](../player-fixtures/service-fluids-bench-final/runtime-verification.txt).
- **21 real Windows reset/settings checks**, including preserved driving/service/volume preferences and a usable fresh starter. [Settings/reset fixture](../player-fixtures/service-fluids-reset-final/runtime-verification.txt).
- All three native fixtures: **zero runtime/DSP/GUI errors**. Captured shop, cam-warning and physical-fluid screens were inspected visually.

Regression fixes: captured explicit starting fluids to avoid Unity JSON turning an unspecified fluid object into an empty system; updated fuel mass-conservation assertions to include independent oil/coolant losses. Fuel usage None preserves fuel weight while independently selected service-fluid losses remain possible. The Unity launch helper now waits for the editor process itself, avoiding waits on surviving compiler/license children after a successful exit.

## Limits / retained future scope

Hardware curves, capacities, wear and fluid rates remain fictional/provisional calibrations, not measured OEM engineering or a detailed thermofluid solver. Cooling recovery/venting and lubrication use deterministic approximations. Labour and fluid top-ups are free in Slice 2; replacement parts use provisional catalog prices.

Random faults remain deferred as requested. Nitrous reserves wet/dry/EFI compatibility metadata only; delivery, bottle behavior, fuel addition and power/stress effects are not implemented. Android/device validation and human listening review are separate from the Windows automated evidence.

See [full implementation details](../../docs/WEAR_FLUIDS_AND_SERVICE.md).
