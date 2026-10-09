# Mechanics-first game plan

## Product direction

Car inspection and parts/tuning previews show a verbose settled dyno/stat view, numeric deltas, tradeoffs and bounded shared-model setup warnings. Tuning and preset previews are drafts until explicitly applied; leaving discards them, while shop/inventory hardware transactions commit deliberately. Shared pressure/speed preferences and outer tire radius in inches make physical effects readable. See [car stats and applied tuning](CAR_STATS_AND_TUNING.md) for the implemented workflow and diagnostic limits.

Simulation difficulty is independently configurable from AI and launch/shift modes. The working shared foundations offer Arcade/Balanced/Simulation presets, mixed ideal-thermal/clutch/stall/traction/rev-match assists and None/Partial 25%/Full wear. The physical bench adds an isolated wear multiplier and captures the rules for repeatable tests. Extend these rules to later tire/cooling/contact/fuel systems rather than duplicating physics. Current implementation and limits are in [simulation settings](SIMULATION_SETTINGS.md).

A side-on 2D drag-racing game where torque delivery, traction, gearing and player technique explain performance. The progression loop is **race → earn → upgrade → tune → beat a rival → unlock cars/events → reinvest**. A modest garage business earns offline cash without replacing racing. Build for Windows desktop first, then validate Android against the same simulation. Commercial distribution is a destination, not a claim that this prototype is release-ready.

Use fictional brands and realistic-looking generic bodies initially. Each vehicle keeps factual reference data, when sourced, separate from gameplay adjustments. No current vehicle values are represented as real manufacturer data. Track the source and license before importing real cars, sound or third-party art.

## Player-facing layout

```text
Boot → Load/Recover profile → Garage
Garage
  ├─ Car collection/dealership → Select car
  ├─ Parts → Buy/install → Dyno updates
  ├─ Tune → Launch/ratios/pressure → Test pass
  ├─ Appearance → Paint/layered art
  ├─ Business → Claim capped revenue → Expand
  ├─ Career → Event restrictions → Race staging
  └─ Free race → Distance/opponent/assists → Race staging
Race: Staged → Amber sequence → Green → Launch → Shifts → Finish
Result: Reaction + ET + splits + trap speed → Reward once → Garage/rematch
```

Temporary UI may expose systems directly. Avoid polishing menus while physics and progression are still changing. Difficulty and transmission are separate choices. AI uses the same engine model; difficulty first changes reaction and consistency, never invisible power bonuses.

## Slice 0 — Foundation and generic art (implemented and verified)

Prepare a Unity project, scene construction, data assets, an engine-independent simulation, save wrapper and developer workbench. Include hatchback, coupe, muscle and pickup sprites. All wheel/rotor/caliper components must remain independent. Placeholders should read as believable car types and use flat cel shading; the final graphic overhaul comes after mechanics.

Acceptance: Unity imports/compiles; sprite contracts pass; cars assemble correctly at 0/45/90/180-degree wheel angles; fixed calipers do not rotate; vehicle JSON is validated; simulation, progression and idle checks produce recorded results. Only report tests actually run.

## Slice 1 — One enjoyable drag-racing loop (current playtest build)

Deliver a short local career with four events plus free racing. Include staging lights, reaction/ET separation, auto launch, assisted launch, sequential shifts, basic manual clutch, traction, four upgrade families, paint, buying cars, offline income and persistent progress. These systems are implemented in the Windows build and verified by foundation, scene, runtime and keyboard-event probes. Practice, driving presets, last-pass comparisons, limiter recovery and launch/gearing experiments improve the iteration loop. A funded starter-car route through all four Normal events is verified. Human driving feel and hands-on Editor Play Mode acceptance remain pending; use `SLICE1_PLAYTEST.md`.

Acceptance:

- A new save can stage, race, receive one reward and return to garage without restarting Unity.
- Early launch loses via a red light and receives no reward.
- Manual launch and manual shifting can win a race with the same build as automatic.
- A power upgrade changes the torque curve and observed acceleration; excessive unassisted torque produces slip.
- Tires and gearing influence elapsed time; a taller final drive is not always faster.
- Purchasing/upgrading/tuning survives a restart; corrupt main save can recover its backup.
- Idle earnings cap at eight hours, cannot be claimed twice, and tolerate clock rollback.
- Results show reaction, 60 ft, available 1/8 and 1/4 splits, and trap speed.

## Slice 2 — Driving depth and tooling

Replace demand-based slip with wheel angular velocity, inertia and a tire-force curve. Refine the existing clutch engagement/capacity, analog pedals, bogging and rev-match penalty; add tire temperature, launch consistency, configurable shift windows and mechanical stress. Spec-driven turbo spool and single/twin/mixed induction are now implemented with fictional calibrations; refine them against measured engine/compressor data. Keep assists independently configurable.

The [driving-depth audit](DRIVING_DEPTH_AUDIT.md) distinguishes working systems, reduced-order approximations and missing mechanics/tools. Slice 2 includes usable fuel management and consumption with free refuelling, persistent mechanical wear, replacement and a modular parts inventory with buy/install/remove/reuse/sell operations. Fuel prices and the complete fuel/money/career/idle/parts-cost balance belong to Slice 3. Existing career/cash/idle code remains scaffolding until that integration is complete.

Implement in the ordered [Slice 2 subphases](IMPLEMENTATION_PHASES.md#slice-2--ordered-subphases): **2A** baseline/contracts → **2B** parts/OEM/build resolver → **2C** owned instances/saves → **2D** usable inventory/replacement → **2E** physical bench/replay → **2F** mass/load → **2G** wheel/contact core → **2H** full driveline integration → **2I** fuel/refuelling → **2J** tire compounds/life → **2K** mechanical wear/breakdowns → **2L** tools/calibration/integrated checkpoint. Requirement sections below are grouped by topic, not execution order. Each subphase has a working checkpoint and exit gate; 2A–2L are complete, with final acceptance in the subphase completion record.

### Required mass, load and grip model

- Define the mass reference explicitly: current `massKg` includes stock wheels but has no declared fuel reference. Migrate to a documented dry vehicle/build mass, including installed hardware once, with driver, payload/ballast and fuel added explicitly. Preserve existing saves through a versioned conversion rather than silently adding fuel to an unknown curb-weight basis.
- Replacement wheels, flywheels and other supported hardware contribute their actual static mass deltas. Apply weight reduction only to removable vehicle components; do not scale fuel, driver, payload or installed wheel mass with a blanket body-weight multiplier.
- Keep translating mass, rotating inertia and axle normal load distinct. All installed mass travels with the car; rotational inertia additionally resists changes in angular speed. Use the existing calculated engine/core/flywheel inertia and the independently integrated wheels. Do not count a rotating component's inertia twice or use equivalent inertial mass as gravitational tire load.
- Configure component/tank/ballast fore-aft locations and heights. Recalculate total mass, centre of gravity, static front/rear load and longitudinal load transfer from the current build and fuel quantity. Show the resulting values in the tools. Preserve total normal load and remove the current artificial 15–85% axle-load clamp; constrain loads physically and handle unloaded axles stably.
- Use actual mass consistently in acceleration, brake response, rolling resistance, driveline synchronization and bog/stall calculations. Mass changes must reach the live simulation as fuel burns or is added, rather than remain cached in the current immutable wheel profile.
- Extend the tire curve with configurable load sensitivity, tire compound/temperature/pressure and dry-track surface grip. More normal load can increase available force while added mass also increases inertia; neither more fuel/ballast nor larger/heavier wheels should guarantee faster acceleration or better braking.
- Provide a simple authorable differential/torque split for FWD/RWD/AWD and an explainable load-transfer response. Keep full suspension travel, cornering and weather for later; label any axle-level differential approximation. Add controlled burnout heating and cooldown using physical slip/work rather than the sound bench's assigned slip.

### Required fuel component and quick refuelling

Fuel is a working Slice 2 driving mechanic, not merely a future economy placeholder. Initial fuel properties and consumption maps are editable fictional calibrations; record provenance before claiming measured real-car values.

- A dedicated domain fuel component owns tank capacity in litres, current quantity, configured density in kg/L, fuel mass, tank position and instantaneous/cumulative consumption. Fuel cost is absent from the Slice 2 calculation. Keep engine displacement litres and the existing Injection/Carburetor behavior separate from tank quantity.
- Compute fuel flow at the fixed simulation tick from RPM, engine load and authored consumption/efficiency parameters. Include idle, neutral revs, staging, loaded driving, shifts, burnout and engine-running shutdown. Define cold enrichment and boost/high-load enrichment separately. Ignition cuts/anti-lag can consume fuel without producing useful crank torque; actual fuel cuts and engine-off states obey their configured flow rules. Consumption must not be based on throttle alone or fall to zero simply because shaft HP is zero during a shift.
- Clamp quantity to 0–capacity. Empty fuel removes combustion/boost production and produces a distinct out-of-fuel stall; inertia, wheel motion and vehicle coasting continue. Refuelling allows an explicit restart using the existing gearbox/clutch rules; it does not repair engine failure or automatically reset speed, RPM or damage.
- Add a quick **Refuel** button with a numeric **Add litres** input in Garage and standalone development benches, including the engine/sound scene and physical driving mode. Display current quantity/capacity, free space and fuel mass. Set the input maximum to `max(0, capacity - currentQuantity)`; validate finite, nonnegative values and clamp again when clicked. Full tank disables adding fuel. Example: a 50 L tank holding 42 L permits at most 8 L. Show the actual accepted amount, never overfill or charge cash in Slice 2.
- Garage refuelling updates the selected owned car and persists its quantity. Bench refuelling changes only its captured/test state, never player fuel or money. A refuel click does not take keyboard driving ownership, release held throttle or overwrite unrelated sliders. In the physical bench, added fuel changes live mass without resetting the run.
- Carry player fuel across races/car selection/reloads. Account for fuel actually used on red lights, losses, abandonment and interrupted runs with recoverable saves; a retry must not silently refill the tank. Choose an explicit initial quantity for new cars and a safe versioned default for old saves. Pausing/offline time consumes no fuel; deterministic bench reset/replay restores the captured starting quantity without touching the profile.

Fuel/mass acceptance required for Slice 2:

- Partial refills, empty/full tanks, capacity changes, fractional litres and invalid input cannot create negative fuel or exceed capacity. Input and click-time validation agree; a full tank is a no-op.
- Identical fixed inputs and starting fuel reproduce consumption and mass traces. Flow is nonzero at engine-running idle/neutral revs, reacts to controlled RPM/load/enrichment differences, follows ignition/fuel-cut rules and is zero while paused/off. Do not assume all fuel systems use overrun fuel cut.
- Empty fuel removes combustion, preserves coast/rotational momentum and blocks an unfuelled restart. Refuel plus restart restores combustion; failed hardware stays failed. Inspect and export fuel used through staging, shift and throttle-release tails.
- Full/partial/near-empty tank A/B runs isolate mass and tank-position effects on load, grip, ET/trap and braking. Verify `fuelMass = litres × density`, total component mass and axle-load conservation. With grip held adequate and power identical, extra mass reduces acceleration; no test should require every lighter build to win a traction-limited launch.
- Compare equal static mass with different wheel/flywheel inertia, then equal inertia with different ballast location. Static fuel/ballast must not be treated as rotating mass. Fuel changes must not reset wheel/engine state or inject unaccounted drive energy.
- Player saves retain partial tanks; bench trials/refills/replays leave them untouched. Repeat/refuel/reset and holding gas while clicking the refuel control have real keyboard/mouse regression checks. Pricing is deliberately deferred.

### Required modular components, compatibility and inventory

Mechanical upgrades replace installed components; they are not irreversible rank increments. Slice 2 must support selecting any available compatible component directly, including an OEM replacement, a lower-rated alternative or a higher-rated part without buying intermediate ranks. Availability/unlocks can be balanced in Slice 3, but may not require ownership of preceding mechanical ranks.

- Separate immutable part definitions, owned part instances, engine/vehicle slot definitions, OEM build recipes, installed builds and tune settings. Definitions describe identity, category, fitment, authored performance/strength/thermal limits, mass/inertia, material/manufacturing quality and default settings. Each owned instance has a unique ID, condition/wear and service/use history. Installed builds reference those IDs; the same instance cannot be installed in two cars or simultaneously remain loose in inventory.
- Implement a representative catalog for all four starter cars covering piston/rod sets and crankshaft, supported induction/fueling/cooling/exhaust equipment, clutch/flywheel/gearbox/differential, wheel/tire assemblies and brakes. Internal sets are balanced assemblies rather than an unexplained mixture of individual pistons. Declare modeled slots and dependencies; smaller fasteners and detailed engine teardown can remain abstracted.
- Fitment checks use authored engine families/interfaces and relevant dimensions or explicit approved applications, not price, rank or equal power ratings. Include mating-component and engine-configuration dependencies: a piston set's bore/pin/compression geometry, crank/rod configuration, cylinder count/set quantity, drivetrain mounts/interfaces and induction/fueling requirements as applicable. Matching nominal specs alone does not establish fitment. Explain the exact mismatch and reject it before changing inventory or the installed build.
- Shared compatibility permits the same definition/instance to move between supported engines/cars. Engines with similar geometry can have different OEM recipes and quality: one may ship with forged internals while another has standard internals. Store distinct standard/forged definitions when their construction/limits differ, even if their fitment dimensions match; OEM is origin, not a universal lowest tier.
- Resolve engine/driveline output, safe operating limits, rotating/static mass and supported tune ranges from the actual installed assembly. Do not stack the legacy generic engine/gearbox rank bonus on top. Forged internals do not grant arbitrary horsepower: strength/reliability/mass and any authored behavior determine their tradeoffs; the induction/fueling/tune determine delivered output within the model.
- Every complete, healthy, compatible build has a working conservative default tune derived from the assembly's limiting components. Offer **Apply safe defaults** and show what changes. Downgrading forged pistons to compatible standard pistons can require less boost/RPM or a different calibration; preserve a saved experimental tune separately and warn or block it when outside the supported operating envelope. Do not silently add supporting parts or claim incompatible/incomplete assemblies run safely.
- Provide Garage inventory, compatibility filters, part details/condition, installed-slot views and buy/install/swap/remove/reuse/sell controls. A successful swap returns the old instance to inventory without repairing it. Cars missing required components are clearly non-drivable until completed. Swapping from another owned car is a move, not a copy, and may leave the donor non-drivable.
- Selling a loose owned part removes that instance and credits its displayed provisional resale value once. Selling an installed/locked part requires removal first; failed or unusable parts have an explicit scrap/sell policy. Slice 2 supplies functional transactions and authorable placeholder prices/resale rules, including condition effects; Slice 3 balances purchase/replacement/service/sale prices with career income. Free development-bench parts are never sellable player inventory.
- Buy/swap/remove/sell and their cash/inventory/build changes are validated and saved atomically with recovery. Block changes to cars/parts in active physical runs; future business assignments use the same reservation boundary. Tuning presets change settings only, never create parts or restore removed/sold hardware. New cars instantiate their declared OEM recipe once. Versioned migration converts existing mechanical ranks into documented equivalent installed instances without also retaining their old multipliers or granting duplicates on each load.

Example required workflow: two compatible engines share a piston fitment family. Car A starts with standard pistons and Car B with forged OEM pistons. Buy forged pistons directly for A, retain its original set, wear the forged set through use, then reinstall A's healthy standard set with safe defaults. The worn forged set remains owned for compatible reuse or sale; it does not become new when removed. A different bore/pin or unsupported crank/rod combination is rejected even if its advertised rating matches.

### Required persistent mechanical wear and replacement

Slice 2 replaces the current single pass-local damage scalar as the authority for supported component health. Existing engine heat/limiter/anti-lag and clutch dynamics become inputs to component-specific stress rather than an automatic reset on restaging.

- Track baseline operating wear plus additional stress from actual driving: RPM/overrev and load/boost for internals; thermal exposure/anti-lag for turbo/exhaust; clutch slip energy and torque overload; rough engagement/shift mismatch and driveline loads; tire slip work/temperature; brake work/heat. Use the installed component's authored material/quality/limits and current condition. Include temperature/cooling/lubrication support where modeled; label reduced-order approximations rather than claim real durability predictions.
- Gentle versus abusive driving must produce explainable differences. Model sustained exposure and severe overload separately; normal shifts do not arbitrarily damage components merely because a key was pressed, and limiter cuts protect only against the conditions they actually control. Avoid a single universal wear rate across all parts or a predetermined failure timer unrelated to load.
- Wear persists on the same owned instance across races, losses, red lights, abandonment, car changes, removal, reinstallation and reloads. Recoverable run checkpoints commit fuel and condition changes without double-applying wear after interruption. Pause/offline time does not accumulate driving wear. Ambient cooling cannot repair lost condition.
- Condition affects relevant capacity/performance/behavior and failure modes: reduced clutch capacity, degraded tire/brake response or component-specific engine/induction faults as authored. Do not apply one global horsepower penalty to every worn component. A failed part stays failed after race retry/refuelling; preserve coasting/rotational momentum and distinct out-of-fuel versus mechanical-failure reasons.
- Provide a component health/stress view with the cause of wear and the build's weakest limiting parts, plus an explicit replacement/swap action. A usable compatible spare can restore operation, including a lower-rated part with conservative settings. Service/repair is offered only for authored repairable damage; failed internals can require replacement. Removed parts retain their true condition and service history. No mandatory animation, wait timer or engine teardown is required for this slice.
- Mechanical diagnostics and test scenes work without career gating. Capture starting installed definitions, instance conditions, tune/fuel and input timeline; reset/replay restores the test snapshot only. Export per-component condition, wear delta, stress exposure, slip/brake/tire work, temperatures and failure cause alongside the force/fuel traces. Add repeatable gentle/aggressive, clutch-slip, limiter/overrev, anti-lag, burnout, brake-heat and downgrade/replacement scenarios.

Parts/wear acceptance required for Slice 2:

- Healthy OEM and mixed compatible builds run on declared safe defaults, including OEM forged internals. Upgrades/downgrades/rank skips do not need intermediate purchases. Unsupported interfaces/missing dependencies are rejected with useful reasons and no partial mutation.
- Equal geometry with different quality uses the intended limits and OEM recipes; a shared compatible instance transfers between engines while retaining ID/condition. Standard-to-forged-to-standard swaps retain the old parts and correctly update mass/inertia, supported tune limits and actual output.
- Controlled driving traces show greater relevant wear under increased physical stress, not unrelated damage to every part. Identical starting instances/inputs reproduce wear/failure traces; repeated reset/replay never heals or changes player-owned parts.
- Failure persists through retry/reload. Install a compatible spare, apply safe defaults and demonstrate a running car again; refuelling alone cannot repair it. Reinstalling a worn part restores its old condition rather than resetting health.
- Inventory conservation and interruption tests cover buy, swap, donor-car transfer, removal, reuse and sale. Instances/money are neither duplicated nor lost; installed/locked parts cannot be sold; sold parts cannot be resurrected by a tuning preset. Legacy saves migrate once and remain valid after repeated loads.
- The functional Garage workflow and physical bench both support inspecting wear and completing a replacement/downgrade without the race reward cycle. Actual pricing and long-term wear/service balance are acceptance work for Slice 3, but component health and inventory operations are mandatory Slice 2 functionality.

### Required tire compound wear and race breakdowns

- Tire definitions separate compound grip/force-slip response, useful temperature range, rubber/wear reserve, abrasion/thermal sensitivity and failure limits. Grip is not a durability rank: a high-grip compound may wear faster than a lower-grip street compound, and not every high-grip tire must have the same lifetime. Author these properties independently rather than deriving lifetime from `tireLevel` or its grip bonus.
- Track wear/condition on owned tire instances or explicitly identified axle-matched sets, with appropriate front/rear differences. Driving distance/work, physical slip energy, load, pressure and temperature affect compound-specific wear. Burnouts consume real tire life. Store remaining rubber/service life and relevant heat history; define worn-grip behavior and puncture/blowout limits separately, including slicks without pretending they have street tread.
- Inspect compound, current condition, operating temperature and expected comparative durability in Garage and tools. Wear curves must explain the observed grip/braking change rather than grant a hidden universal HP penalty. Removed/reinstalled/sold tires retain their actual condition; replacement accepts compatible alternatives with their own pressure/default operating settings.
- The car can mechanically break during a race in Slice 2. Overstress or accumulated wear can cause authored engine/induction, clutch/gearbox/driveline, brake or tire failures. Safe defaults reduce risk but do not grant invulnerability to abusive driving; configurable aids/bench failure protection must be visible and must not silently prevent every production race failure.
- A failure changes the affected physical output/capacity/contact behavior, reports its cause and can cause a DNF or degraded operation as appropriate. Keep coasting, wheel/flywheel momentum and any remaining brake authority physically continuous. Preserve the broken component/condition after the race, retry or reload; do not delete the entire car or magically restore health. Collision/chassis-crash simulation is not required to deliver mechanical race breakdowns.

Tire/breakdown acceptance required for Slice 2:

- At controlled equal tire work/load/temperature, independently authored soft high-grip and durable lower-grip compounds show their configured different wear rates. In full driving tests, compare actual slip/heat/wear without forcing grip and durability into the same ranking or assuming equal input creates equal dissipated work.
- A repeatable burnout uses tire life; driven and non-driven axle histories differ as physically appropriate. Worn tires retain condition through inventory reuse/reload and a compatible replacement changes grip, operating limits and wear response.
- Repeatable abusive-race scenarios break at least an engine/driveline component and a tire under controlled conditions. Failures affect the race, persist and require the declared replacement/service workflow. Benign matched scenarios remain below failure thresholds.
- Failure/coasting/stopping stays finite and stable at zero/low/high speed; no RPM/wheel-state reset or free repair occurs. Export component/compound ID, condition before/after, stress/work/heat exposure, failure tick/cause and resulting physical behavior. Bench replay remains isolated from owned items.

### Required wheelspin and driveline integration (not implemented in Slice 1)

This explicitly includes wheels continuing to spin while a manual clutch is open during a gear change, followed by tire grip slowing them or renewed drive torque increasing spin. The original Slice 1 used demand-estimated slip. Phase 2H now integrates independent wheel/contact rotation with the shared coupling; 2I adds fuel and 2J adds compound heat/life. The requirements below remain the full Slice 2 acceptance list for final 2K–2L review.

- Integrate wheel/axle angular velocity separately from vehicle speed for FWD, RWD and AWD. Wheel assembly mass, radius and rotational inertia must determine angular acceleration. Wheel rotation must not be reconstructed from vehicle speed plus an estimated slip multiplier.
- Calculate longitudinal tire slip from wheel surface speed versus ground speed, with stable contact behavior near zero speed. Use a configurable tire-force curve with a peak and sliding region, affected by axle normal load/load transfer, compound, pressure and temperature. Apply equal-and-opposite tire contact torque to wheels and longitudinal force to the vehicle.
- Integrate applied drive torque, tire reaction torque, braking torque and rolling losses. Account for wheel rotational inertia once: independent wheel integration must replace its equivalent-mass contribution rather than adding the same inertia again to vehicle acceleration.
- Couple engine/core/flywheel inertia to actual driven-wheel speed through the selected ratio and finite clutch or converter torque. Opening a clutch disconnects engine torque without resetting engine RPM, wheel angular velocity or vehicle speed. Wheelspin can continue until contact forces remove it; it need not persist for every entire shift.
- On manual clutch re-engagement, transfer torque according to capacity and engagement. RPM mismatch and stored flywheel energy can produce a post-shift spin/chirp, bog or stall depending on build and grip. Grip recovery must follow the wheel/contact dynamics rather than a timed slip decay or an immediate RPM assignment.
- Apply the same contact model to DCT handover, automatic converter slip/multiplication/lock-up and continuous CVT ratio changes. Their respective torque delivery must produce different wheelspin behavior. Give converter/automatic torque limits explicit hardware parameters instead of borrowing the manual clutch-capacity field.
- Traction assistance must regulate applied drive torque using measured slip; it must not hide wheelspin by overwriting wheel speed or the reported slip. Keep assists independently switchable for comparison.
- Drive rotating wheel/rotor sprites and tire sound/effect intensity from the computed wheel speeds/slip. Keep calipers fixed. The standalone sound bench's assigned roller/slip controls remain labelled acoustic overrides; add a repeatable physical spin-through-shift test using the real car/contact model, outside the race/reward cycle.

Development tools: expand existing tune presets, stock/build dyno, last-pass comparisons and batch reports with live force/slip/load graphs, deterministic input replays and seeded AI mistakes. Refine the implemented braking/shutdown lane before tuning brake performance; caliper/rotor sprites already support it.

### Required physical driving tools

- Add a standalone physical driving test mode using the actual `CarSimulation`/wheel-contact/fuel model without lights, race finish, payouts or progression gates. Keep the current sound bench's fixed-RPM/load/roller/slip overrides clearly labelled; those acoustic fixtures cannot validate real grip or acceleration.
- Support keyboard/mouse pedals, neutral/gear changes, reset, start/stop, brake hold, clutch engagement and controlled dyno/road load. The last-input ownership rules and live slider feedback remain consistent with the existing bench. Static RPM stays an explicitly external operating-point constraint.
- Capture immutable vehicle/installed-component definitions and starting conditions, tune/environment/starting-fuel/starting-temperature/input-timeline snapshots. Add tick-indexed input recording and replay, repeatable AI seeds, full reset and A/B overlays. Export configuration, trace and model/schema version together; identify override-driven versus physical runs. Test snapshots never transfer their part identities or condition resets into player inventory.
- Graph all 120 Hz ticks for RPM/speed/gears/pedals, front/rear slip and angular speed, axle normal loads, tire/drive/brake/contact torques, resistance forces, engine/clutch/converter state and tire temperatures. Add dry/build/fuel/total mass, fuel L/kg/flow/used and rotational energy. Retain staging, shift interruptions, throttle release and shutdown; the current 10 Hz race summary is not a complete diagnostic recording.
- Add controlled mass/fuel sweeps, tire/pressure/temperature/surface comparisons, spin-through-shift/regrip, bog/stall, converter lock-up and fuel-exhaustion scenarios. Reuse the sound generator so physical events can be heard and compared without the race cycle.
- Expose individual gear-ratio/final-drive editing and predicted shift RPM drops/redline speeds alongside live curves. Validate numeric edits; separate experimental setups from player-owned/unlocked parts. Diagnostics must distinguish actual engine torque/HP from transmitted/wheel output and any assigned load/RPM.
- Keep existing fixture exports and Windows input tests, add force/mass/energy/fuel conservation checks, then recalibrate four-car benchmarks and F–S thresholds. Human listening/driving review remains necessary even after automated traces pass.

Acceptance: replay inputs reproduce the same results; manual and automatic times remain competitive; hotter tires and clutch mismatch have explainable effects; each force can be inspected in telemetry.

Wheel/contact acceptance checks required before marking Slice 2 complete:

- A spinning driven axle retains continuous angular velocity when the clutch opens and when the selected gear changes. Contact forces determine its deceleration and grip recovery; no gear edge resets spin.
- At the same wheel speed and tire load, changing rotational inertia produces the expected different spin-up/spin-down response. Ground forces and energy accounting must not double-count wheel inertia or create energy during synchronization.
- Repeatable manual shifts cover clean grip, a post-shift chirp, sustained wheelspin, recovery after a throttle lift, and clutch-dump bog/stall. These are outcomes of controlled torque/grip/inertia settings, not predetermined sound/animation events.
- The same controlled build/input comparisons cover Manual, DCT, converter Automatic and CVT, with correct driven-axle behavior for FWD/RWD/AWD. Converter or clutch slip and tire slip must remain separately observable.
- Traction-control A/B runs demonstrate reduced physical slip through torque intervention, without changing the underlying tire grip or falsifying wheel-speed telemetry.
- A zero-speed launch, coast through a disconnected clutch, braking and low-speed stop remain numerically stable. Fixed inputs at the fixed simulation rate reproduce full traces regardless of render frame rate.
- Export engine RPM, clutch/converter slip and transmitted torque, front/rear wheel angular velocities and slip, tire forces, normal loads, drive/brake/contact torques and vehicle speed. The full shift and recovery interval remains in chronological order, including zero drive torque.

## Slice 3 — Fuel, money, career and idle progression

The approved changes to the initial mechanical/tooling plan and the adjusted 2K–2L completion gates are consolidated in [Slice 2 plan reconciliation](SLICE_2_PLAN_RECONCILIATION.md). Delivered local engine service, dry/wet/material clutch choices and provisional paid timed rebuilds must be preserved when the full economy arrives.

After 2L, implement the ordered [Slice 3 subphases](IMPLEMENTATION_PHASES.md#slice-3--ordered-subphases): **3A** repeat purchases/collection → **3B** assembly ownership/part-out preparation → **3C** valuation/quotes → **3D** paid operations/salvage/basic recovery → **3E** career/availability → **3F** garage/idle → **3G** full-loop balance/recovery → **3H** economy/save integrated checkpoint. The ownership schema is laid in 2C; repeated purchase and salvage are player-facing Slice 3 features. **3A–3H developer acceptance is complete; see the [full audit](../reports/SLICE_3_AUDIT.md).**

Delivered 3C exposes read-only buyer valuations for actual loose packs/singular elements, engine trees, chassis and complete/incomplete cars. Captured quotes explain quality, wear/individual faults, use/service history, required completeness and buyer/model factors; keeping hardware excludes its identities and value. Authored policy data, expiry/stale-state/reservation checks and matrix exports support development. 3D now uses captured quotes for confirmed sales/salvage and payments, with paid fuel/services, retained engine trees, empty-garage persistence and basic recovery. Setup-library missing purchases retain their atomic captured-price Apply. Full-loop balance remains 3G; see [paid operations](PAID_OPERATIONS_AND_RECOVERY.md). See [valuation contract and controls](VALUATION_AND_QUOTES.md).

Expand F–S event tiers, qualifications, rivals, drivetrain/power/weight restrictions, component catalogs/availability and unlocks. Add restricted repeat rewards, race records, clearer rival goals, tune slots and idle jobs that reserve assigned cars/parts. Use Slice 2's persistent wear and modular inventory; balance new/used/OEM/performance-part purchases, services, replacements and condition-dependent resale. Lower-rated compatible replacements and retained old parts must remain viable budget choices; mechanical ranks are not compulsory purchase ladders.

Use the working Slice 2 fuel component and refuel quantity control. Add authored price per litre, displayed quote/actual quantity/total cost, affordability validation and an atomic cash-plus-fuel transaction. Never deduct cash twice after interrupted saves, overfill, charge for rejected litres or retain a hidden free player-refuel bypass. Bench refills remain free and isolated for development. Price values are gameplay data, not live petrol-price claims.

The complete loop is **claim capped garage earnings → buy fuel/parts → set fuel load/tune → race → consume fuel → settle rewards and operating costs once → advance career → upgrade the garage/reinvest**. Fuel used during staging, losses or abandonment still costs resources. The player decides between carrying less fuel for a lighter car and funding fewer refuelling stops; performance comes from Slice 2 mass/consumption, never an economy-only horsepower modifier.

Balance refuelling against race entry fees, first/repeat-win rewards, losses, upgrades, repairs and idle-business expansion. Prevent an empty tank/empty wallet from permanently blocking play using an explicit, bounded recovery route. Keep developer/testing fuel separate from career transactions. Business car assignments and availability must be enforced; the passive garage business does not drain car fuel just because real-world offline time elapsed. Any later fuel-consuming job needs its own declared costs and cap.

Development tools: a configurable economy scenario runner and transaction ledger showing part instance purchase/install/removal/reuse/sale, condition/service/replacement costs, litres used/refilled, entry/reward cash, reputation, unlocks and idle claims. Run fresh-save, repeated-loss, low-fuel/low-cash, worn/broken-build, downgrade-to-OEM, alternative-build and interruption/migration routes. Capture a price/reward/parts configuration version for each balance report.

Acceptance: several viable builds per tier; a fresh save can complete the fuel/race/reward/upgrade/idle loop; fuel and paid refills persist across pause/reload/car selection; quotes, accepted litres and cash match; softlocks have a tested recovery route; transactions/rewards/idle claims cannot duplicate after interruption; business progress, fuel and race rewards are balanced against upgrade costs; older save fixtures migrate correctly. Prices and progression are calibrated with recorded full-loop routes rather than tuning fuel mechanics to fake affordability.

Parts/economy acceptance: a worn or failed performance build can recover through a compatible retained spare or affordable lower-rated replacement with safe defaults. Used-part sale credits exactly once and reflects condition; reacquisition never resets the same used instance to new. Demonstrate direct high-spec purchase, downgrade/reuse and sale across career runs without compulsory intermediate-rank spending. Mechanical driving consequences remain observable even before long-term wear and replacement costs are balanced.

### Required repeat ownership, partial salvage and assembly valuation

- Allow buying multiple copies of the same car/model in Slice 3. Separate immutable vehicle definition ID from unique owned vehicle/chassis instance ID; selection, fuel, builds, component health, records, tuning presets and business reservations refer to the owned instance. Buying another copy creates its own declared OEM part instances once and does not overwrite or share the first car's hardware.
- Support complete cars, rolling chassis, incomplete/non-running vehicles, assembled engines and loose components as explicit owned/saleable states. Incompleteness prevents driving when required, but does not prevent retention, transfer, sale or salvage of the assets that actually exist. Do not recreate missing OEM parts merely because a vehicle is selected or a save is sanitized.
- Garage provides a salvage/part-out workflow with a preview of **keep**, **sell/salvage** and **remaining assembly** results. The player can retain the entire engine assembly with its installed internals/accessories and salvage only the chassis plus specifically selected remaining contents. Preserve compatibility, condition and instance IDs of retained assets. Never include kept parts in salvage value or dispose of them with the car accidentally.
- A complete or partial assembled engine is an owned assembly referencing its actual child components; it can be transferred/stored/sold as a unit without selling each internal part first. Nested ownership is exclusive: selling/salvaging an assembly transfers/disposes only its declared included descendants once. Selling a chassis does not also sell a detached retained engine. A selected/assigned car or assembly cannot be modified mid-run/job; cleanly update selection/reservations when an instance is disposed of.
- Value loose parts, engines, chassis and complete cars using distinct authored valuation policies. Include actual installed equipment/quality, condition and failure/repair needs, completeness/verified running state, chassis/model desirability, assembly/service history and buyer/market context where modeled. A full engine or car price is **not** forced to equal the sum of component resale values or their purchase prices.
- Permit an assembled engine to sell above the same parts' loose valuation through assembly/testing/convenience value, or below it through buyer demand, faults, incomplete support equipment or disassembly/repair risk. Show the quoted assembly price and main adjustment reasons, without promising exact manufacturer or real-market prices. Value only actual assets: missing parts cannot contribute their imaginary new value.
- Use stable, reproducible quotes with captured condition/contents/price-policy version and authoritative click-time validation. Buy/sell/salvage cash and ownership mutations are one recoverable transaction; stale quotes, duplicate actions, reloads and repeated salvage cannot pay twice or revive sold assets. Balance legitimate profitable builds/part-outs separately from accidental no-work trade loops or duplicate ownership payouts.
- Development tools compare loose-part, assembled-engine, chassis and whole-car valuations against the same captured assets, with traceable adjustments. Include healthy, worn, broken, incomplete and mixed-OEM-quality examples, repeated model purchases and interrupted multi-asset salvage transactions.

Acceptance required for Slice 3:

- Own two copies of one model with different tunes/fuel/wear/builds; select, race, assign, save/reload, transfer compatible parts and sell one without altering the other. Old definition-keyed saves migrate to distinct owned IDs without rebuilding or duplicating their contents.
- Remove and keep an engine, then salvage its incomplete donor chassis. The engine and all retained children stay owned in their original condition; only selected disposed assets contribute to payment and disappear. An incomplete car is never blocked from this salvage workflow solely because it cannot race.
- Sell an assembled engine atomically and verify all included descendants leave inventory once. The same children cannot also be sold separately afterward. Cancelled/stale/failed transactions leave ownership and cash intact.
- Demonstrate authored assembled-engine quotes both above and below comparable loose-part totals, along with condition/completeness-dependent car/chassis valuations. No universal one-to-one pricing rule or linear upgrade-rank price substitutes for the valuation policy.
- Recovery routes cover the last non-running/salvaged car, retained spares and low cash. Economy routes include rational reuse, downgrade, rebuilding, complete-assembly sale and part-out decisions; persistent fuel/wear remains tied to the correct owned instances.

## Slice 4 — Interface migration, input and platform validation

Revised after Slice 3H and the approved inventory/cart, component, settings, bench, audio and performance additions. Migrate all existing player interfaces and both development benches to scalable Unity UI, preserving their actual controls, ownership, previews and shared simulation. Use shared input actions for keyboard, controller and genuine multitouch; retain remapping, accessible navigation, safe areas, focus/lifecycle handling and recoverable save timing. Establish supported hardware and budgets through measured profiling.

The authoritative [Slice 4 review plan](SLICE_4_PLAN.md) orders **4A–4Q**: coverage/layouts → shell/widgets/input → settings → garage/collection/stats → tuning/configurations → installed maintenance → inventory → shop/cart → assemblies/valuation/salvage → career/free race/records → business/recovery → race HUD/results → sound bench → physical bench → controller/accessibility → Android → integrated acceptance. One phase produces one reviewable checkpoint; **stop and wait for the user's explicit validation before starting the next**. 4A reviews layouts before package or runtime changes. All phases are currently not started.

Acceptance: feature/control coverage including advanced settings and both benches; readable summaries/comparisons and a visible hardware-aware shift light; preserved Apply/Revert, held throttle, input ownership, exact worn assets and atomic carts/quotes; controller-only workflows and simultaneous real-phone pedals/shifts; no display-FPS simulation advantage at 30/60/120; no duplicate income/rewards or advancing paused races on focus/background/orientation changes. Automated evidence and user interface approval are separate gates. Final vehicle art/content remains Slice 5, commercial hardening Slice 6.

## Slice 5 — Content and visual finish

Replace placeholders with original or licensed cel-shaded vehicle art while preserving pivots and IDs. Add configurable wheel/brake appearances, trim, paint, decals, smoke, start tree, parallax venues, engine/shift/tire audio and race feedback. Keep raw source files and provenance.

Acceptance: brake layers show through real spoke openings, rotor/wheel centers never wobble, all paint colors respect glass/trim, and audio/visual effects correspond to simulated events.

## Slice 6 — Commercial hardening

Stabilize saves and migrations; add settings/localization/accessibility, Steam integration where approved, achievement hooks, cloud save conflict handling, install/build automation and crash diagnostics. Perform balance/playtesting, device coverage, licensing checks and store preparation. Distribution fees, credentials and store publishing are separate explicit actions.

Acceptance: fresh install and upgrade-install tests, save compatibility across supported versions, desktop/Android performance targets, no unlicensed assets, complete controls/settings, reproducible builds and a playable demo with a clear onboarding path.

## Scope kept for later

Circuit racing, drifting, multiplayer, engine conversions beyond declared supported fitment, full suspension travel/cornering, weather, NOS and cosmetic part catalogs are intentionally later systems. Supported assembly retention/transfer/reassembly is part of Slice 3; unrestricted engine conversions requiring new mounting/support systems are not implied. Slice 2 implements simple load-transfer/differential/track-surface controls, fuel consumption and mass/grip integration through the shared model; current acceptance evidence is linked in the subphase completion record. Avoid a full simulation rewrite: each has a place in vehicle data and event restrictions, but drag racing quality determines the first release.

Integrated 2L tools and calibration contract: [INTEGRATED_TOOLS_CALIBRATION.md](INTEGRATED_TOOLS_CALIBRATION.md).

## Slice 3A–3B collection and configuration workflow

The dealership selects exact owned instances and sells additional copies. Stored complete or partial engines retain their own real descendants, faults and liquids; chassis remain owned when detached. The configuration library saves applied engine/associated-control hardware, whole builds or adjustments only. Load previews reuse and missing purchases; explicit Apply commits all costs/hardware/tuning atomically. Blueprints survive hardware sales. Compatibility, installed tuning permissions, engine rebuild reservations and live run ownership remain authoritative. See [controls and ownership contract](ASSEMBLIES_AND_SETUPS.md). 3C valuations and 3D priced salvage/operating services are delivered. Career/idle/full-loop balance are delivered in 3E–3G; 3H passed the final integration gate; commercial and human/device acceptance remain separate.


Approved 3H thermal extension: race-entry ambient/optimal/live temperatures, optional continuous optimal-temperature assist (disables race-entry choices), actual parked/offline elapsed cooling and optional shared physical warm-up/burnouts. See [heat and race turnaround](HEAT_AND_RACE_TURNAROUND.md). No fixed free cooldown, repair or resource refill is granted by retry.
