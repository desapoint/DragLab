# Condition, valvetrain, fluids and service

Approved extension to Slice 2K–2L, implemented in the shared car simulation. Operating costs and price balancing remain Slice 3. This document does not declare the remaining 2K–2L gates complete.

## Hardware and wear

The four vehicle families gain 132 definitions across camshaft sets, springs/retainers, valves/guides, followers/lifters/rockers, timing drives, rings, cylinder blocks, bearings, stem seals, oil pans, oil pumps, oil filters, coolant pumps, pressure caps, hoses and optional oil coolers. Catalog identifiers are appended; existing paid components, ranks, fitment and retained originals remain valid. Names describe complete compatible sets where the engine requires multiple components.

Factory cams preserve the authored torque curve. Street/race cams trade low-speed torque and fuel efficiency for higher-speed filling, change idle overlap and require suitable springs. Wear and inappropriate springs can cause valve float, loss of output and valve damage. An adjustable timing drive unlocks ±6 crank degrees; fixed hardware rejects that tuning value. Garage changes remain drafts until Apply. Physical cam dimensions require swapping the cam, rather than free tuning sliders.

Condition is component-specific. Sealing and ignition losses reduce useful output and combustion efficiency; intake deposits reduce breathing; worn bearings add friction and reduce oil pressure. Follower wear and timing clearance affect delivery. Fuel metering contamination/waste affects consumption. For equal throttle, less useful output need not consume more fuel; recovering equal output needs more fuel. Clutch slip continues through the original friction/load model without an extra universal fuel penalty.

New hardware uses category-specific thermal/service-life metadata. Better durability slows deterministic wear; strength, tuning capability and horsepower remain distinct. No random faults or noise-driven torque fluctuations are introduced.

## Fluids

Owned cars persist oil and coolant quantities, oil temperature/condition, pressure and consumed/lost totals. Installed pans/radiators expose fill capacities; pan minimum quantity/baffling, pumps, filters, caps and oil coolers have applicable effects. Quantity changes fluid mass and center of gravity once; factory curb weight already includes the reference fluid fill. Hardware swaps retain quantities, so a larger pan does not create oil. A smaller pan can leave an excess to drain.

Oil can burn through worn rings, stem seals and turbo sealing, leak from the pan, age under use/heat, lose pressure through low quantity, hot viscosity, restrictions, pump/bearing wear and poor pickup under acceleration. Low lubrication progressively damages relevant components; a failed pump does not magically seize the whole engine in the same instant.

Cooling combines radiator/engine heat transfer with retained quantity, pump circulation and pressure. A serviceable leak is separate from structural failure: the radiator can retain cooling while liquid remains. Leaks persist during an explicitly simulated 60-second engine-off cooldown when returning to Garage. No unbounded offline wall-clock drain is applied. Ordinary warm-up does not consume healthy coolant; sufficiently high temperature can vent liquid without immediate radiator destruction. Rates and temperature behavior are provisional engineering approximations, not measured OEM data or a detailed fluid-flow solver.

## Service and repairs

Garage → fluids and Parts → Repairs expose capped quantity top-ups, fill both systems, oil/filter replacement, coolant renewal and excess-oil draining. Fluids/labour are free during Slice 2; replacing the oil filter buys its provisional catalog part and retains the dirty original in inventory.

- Cleaning removes deposits while retaining structural wear.
- Adjustment corrects follower/timing clearance while retaining wear.
- Resealing repairs an eligible radiator, hose, pan or induction leak; topping up never seals it.
- Reconditioning gives limited structural recovery for serviceable rings, bores, valves or followers. Ring-only service requires a sufficiently healthy bore; failed or excessively damaged hardware requires replacement. Repeated machining has a decreasing recovery ceiling.
- Stem-seal replacement and other hardware replacement use the existing inventory/shop workflow. There is no generic whole-engine healing action.

All owned operations use the same atomic save/revision rules as part installation. Failed saves roll back. One-time migration installs factory service hardware and initializes fluids, while later reloads preserve removed parts and deliberately empty quantities.

## Difficulty

Independent shared preferences: fluid depletion None/Reduced 25%/Realistic; oil aging with the same rates; condition performance effects Assisted/Reduced 25%/Full; manual service or automatic top-ups before a race. Auto top-up retains faults, oil aging and part wear. Wear and optimal thermals remain independently selectable.

Arcade chooses no depletion/aging, Assisted condition effects and auto top-up. Balanced chooses 25% depletion/aging/condition effects. Simulation chooses full rates and manual service. Existing launch, shifting, volume and other preferences remain independent and survive progression reset.

## Physical bench and diagnostics

Physical bench → detailed diagnostics exposes isolated quantities, oil condition/temperature, pressures, circulation and live losses. Starting per-part deposits, leaks and clearance are available only where relevant, alongside existing health/temperature controls. Test top-ups, cooldown and eligible service actions retain pedal ownership and driving dynamics. Wear reset deliberately leaves fluid quantities unchanged.

Live graphs and CSV include oil/coolant quantity, oil temperature/pressure/condition, losses, valve float and combustion efficiency. Existing chronological traces and overlays remain intact. Full starting fluid/fault state is captured; continuation checkpoints carry exact double values. Physics model is `draglab-service-fluids-v13`: record fresh scenarios for this model; older recordings report incompatibility rather than silently using different physics.

Car inspection and shop/tuning previews show capacities and cam/spring changes, installed service metadata and practical low-fluid/leak/cam warnings. Settled dyno resolves RPM-dependent valve control, rather than applying one idle loss value throughout the curve. Pressure follows the shared bar/psi/kPa preference.

## Deferred scope

Randomness remains deferred. A later proposal should use bounded, condition-driven, seeded persistent cylinder/fault variation captured in replays, rather than fresh random torque each frame. Nitrous has reserved wet/dry/EFI compatibility metadata only; bottle/fuel delivery, power, stresses, parts and controls remain unimplemented until their own phase. Economics and the full gas/money/career/idle loop remain Slice 3.

Verification evidence: [service matrix](../reports/service-fluids/verification.txt), [scenario results](../reports/service-fluids/matrix.csv). The final implementation report records regression and native UI results separately.

Current integrated capture model is v15; the v13 service model above describes this feature’s original checkpoint. See [2L tooling](INTEGRATED_TOOLS_CALIBRATION.md).

Current-model integration and native top-up/repair/service acceptance: [2L checkpoint report](../reports/phases/2l/CHANGES.md).

Current 3D: normal top-ups, oil/filter changes, coolant flushes, applicable repair labour and wet-clutch oil are paid through captured atomic transactions. Rebuild quotes include oil supplied at collection. The isolated benches remain free. Physical fault/aging/wear behavior is unchanged; see [paid operations and recovery](PAID_OPERATIONS_AND_RECOVERY.md).
