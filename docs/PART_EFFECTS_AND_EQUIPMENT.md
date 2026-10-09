# Parts, removal and tuning equipment

The shared race/physical-bench model is `draglab-part-hardware-v12`; catalog version 2. Old physical recordings require recording again. Prices and numeric component specifications are fictional calibration, not OEM measurements or final economic balance.

Catalog expansion (2026-10-08): [128 additional parts and verification](../reports/part-hardware/CHANGES.md). Compact/street/large single/twin/twincharged turbo kits have fixed or adjustable controllers. Superchargers have fixed or interchangeable pulley drives. More clutch, flywheel, cooling, tire, brake and gearbox alternatives provide independent tradeoffs.

Tuning permissions are definition-owned, independently of rank, quality and price. Physical turbo size requires a part swap. Fixed boost/valve/pulley/final-drive hardware ignores incompatible presets and stored sliders; supported adjustable hardware unlocks only its controls and ranges. OEM final drive is fixed; included interchangeable gearsets permit garage setup changes. Draft Apply/Revert still governs race settings. The physical bench exposes those same installed parts and supported controls without claiming keyboard pedals; the raw sound bench retains unrestricted engineering experiments.

The category contract in `PartSemantics` authors descriptions, relevant ratings, resolver effects, removal rules and inspection rows together. A torque-bearing driveline part retains torque strength at its actual shaft. Only pistons, rods and crankshaft advertise an engine RPM limit. Cooling, fuel delivery, ignition, exhaust, upholstery and electronic modules do not impose a generic torque/RPM ceiling. The safe internal torque/RPM values are internal-engine headroom; the inspection separately checks gearbox and geared shaft demand.

## Effects and removals

| Component | Effect | Removal / wear behavior |
| --- | --- | --- |
| Pistons, rods, crankshaft | Load/RPM/thermal strength; forged parts add headroom rather than free power | Required engine core. Failure stops combustion. |
| Head/intake | Airflow changes torque curve and resulting load/heat | Required assembly. Condition can restrict power. |
| Fuel system / ignition | Explicit supported-power capacity; raises output only if old delivery is limiting it | Required for combustion. Failure stops engine without inventing damaged internals. |
| Cooling | Engine-to-coolant conductance, radiator W/°C, coolant heat capacity | Engine can run without it, with residual cooling and an overheating warning. Damage is not repaired by cooling. |
| Induction | NA/turbo/supercharger topology, boost delivery, transient lag, flow/charge heat, parasitic blower load and sound | Can remove boost hardware; factory boosted curve is de-rated to NA. |
| Exhaust | Authored catalyst/silencer/resonator acoustic transfer | Can remove; open acoustic path. No fabricated power bonus. |
| Clutch | Manual/DCT torque capacity, slip energy, heat and condition | Open/failed coupling disconnects road torque. Separate friction part is optional for converter automatic/CVT. |
| Flywheel/flexplate | Mass, inertia, stored energy, neutral RPM decay and synchronization | Required assembly; no provisional mechanical wear. Replacement mass counted separately from rotating inertia. |
| Gearbox | Transmission type, shift duration, input strength, converter/CVT capacity and downshift protection | Disconnects drive when broken/absent. Upgrading it does not secretly strengthen the separate friction clutch. |
| Differential | Open / bounded LSD / welded / purpose-built spool torque allocation, strength and replacement mass | Drive-path component; broken/absent disconnects its path. |
| Shafts, axles, transfer/coupling | Geared torque strength, branch failures; upgraded transfer hardware gives faster engagement and isolation capability | Retain common/front/rear failure behavior. |
| Wheels | Assembly mass/inertia and vehicle mass | Required for road build. No fabricated compound-grip or engine-strength bonus. |
| Tires | Compound friction independently of wheel mass | Required for road build. Persistent compound life/temperature/contact physics still need 2G–2J. |
| Brakes | Stopping-force rating, heat tolerance, actual brake-work life | Loss removes friction braking, not engine operation. |
| Structural body/panels | Structural build mass reduction | Cannot remove chassis and still have a complete road build. No provisional mechanical wear. |
| Interior | OEM 60 kg; minimal 15 kg; fully stripped 0 kg | Optional, no mechanical wear. Retained original can be reinstalled or sold. Driver seat/controls remain structural equipment. |
| Comfort accessories | OEM 20 kg; lightweight 8 kg; removed 0 kg | Optional, no mechanical wear. This category excludes radiator, starter and charging equipment. |
| Controls / ECU | Programmable turbo preload and anti-lag, compatible with installed hardware | Injected engine requires a working controller; mechanical carb/ignition can run without optional electronics. Electronic equipment has no provisional mechanical wear. |
| Rev limiter | Fixed protection or programmable threshold / hard-soft-disabled behavior / compatible cut | Separate purchase. Removing it removes added protection. Cannot prevent mechanically forced overrev on a bad downshift. |
| Two-step controller | Fixed 3500 RPM auxiliary launch cut or programmable launch threshold/style/cut | Separate purchase. Some factory cars already have programmable integrated launch hardware. Does not include main limiter or shift light. |
| Shift light | Fixed redline-minus-200 alert or programmable separate RPM threshold | Separate purchase. No automatic shift, torque or durability bonus. |

## Cooling

The same `MechanicalStress` solver runs in race and physical bench. Internal combustion heat transfers into the installed cooling reservoir; the radiator rejects reservoir heat to 20°C ambient with a road-airflow term. Upgraded cooling increases both engine heat transfer (2.2× factory) and radiator capacity (650 versus 300 W/°C) and reservoir capacity. Condition reduces conductance/rejection; failed or removed cooling leaves residual air cooling. These coefficients are provisional. “Optimal thermals” bypasses this temperature evolution explicitly. The engine sound's normalized warm-up field is distinct from component temperatures in °C.

## Differential scope

Open axle differentials are limited by the weaker left/right grip. LSD hardware allows up to 3:1 torque bias plus 35 Nm preload, reduced by wear. Welded and spool hardware permanently couple outputs and use combined available straight-line grip; they do not invent additional tire grip. Their straight-line locking behavior is the same, while mass and strength differ. On uniform grip, there is no artificial traction bonus for a differential swap.

The physical bench provides four separate left/right grip multipliers, descriptions, estimated wheel-slip diagnostics and radiator heat rejection. Race and bench call the same torque/force allocation. **Wheel angular speeds and slip are still estimates**: this increment adds meaningful left/right allocation, not the independent contact/inertia/regrip solver promised in 2G–2H. Turning scrub is outside the drag-only mode. Center LSD has bounded torque bias; disconnected-branch preload/spin behavior remains part of the full contact integration.

## Hardware-driven tuning

Parts purchases/installations commit immediately using saved adjustments. Garage tuning edits a draft, requires Apply, and leaving discards uncommitted changes. Installed hardware determines which settings appear: programmable limiter threshold/style/cut, programmable two-step threshold/style/cut, and programmable shift-light threshold. Carbureted engines use ignition cuts instead of a fictional electronic injector fuel cut. Preload/anti-lag requires turbo hardware and a programmable controller. Inactive stored settings cannot override absent/basic equipment.

The physical bench has the same hardware-specific tuning, all shop hardware, test removal and split-grip settings. These configuration controls preserve keyboard/sliders pedal ownership and reset only the isolated test setup. The acoustic bench remains an explicitly separate sound override sandbox.

Existing profiles receive the two cabin-equipment categories once, with unchanged original IDs/conditions. Existing control-upgrade profiles receive limiter/two-step modules once to preserve their previously purchased feature access. Removed slots remain empty on subsequent loads. New control purchases do not grant these separate modules automatically.

## Verification

Run `tools/Invoke-Unity.ps1 -Action PartsEffectsVerify` for focused category, cooling, removal, equipment, differential and ownership checks. `reports/part-effects/catalog.md` lists every authored definition and relevant stat; `verification.txt` records physical comparisons. Full verification and a Windows build remain required after source changes. Lighter builds can still have slower passes due to changed normal loads, spin and shift response; mass reduction is not a guaranteed timing multiplier.

Primary references for the qualitative differential rules: [Eaton open differential operation](https://videos.eaton.com/detail/videos/differentials/video/6162816705001/open-differential-operation?autoStart=true&page=2), [Eaton limited-slip](https://origin-edge.eaton.com/us/en-us/products/differentials-traction-control/limited-slip-differentials.html), [Eaton locking differential](https://automotive.eaton.com/us/en-us/products/differentials-traction-control/locking-differentials.html). Cooling background: [Holley/Earl's heat-transfer technology](https://documents.holley.com/techlibrary_temp-a-cure_tech_info.pdf). Numeric parts data above is authored game data.


Final checkpoint: 1337 automated checks, including 412 focused part checks. Windows shop/equipment/removal/save controls pass 16 checks with zero runtime/GUI/DSP errors in `reports/player-fixtures/parts-effects-v5-final-validated`. Physical-bench keyboard/sliders/held-gas/replay regression passes in `reports/player-fixtures/parts-effects-v5-driving`. The all-four-car OEM endurance matrix passes. In the controlled 480 hp / 180-second thermal fixture, OEM internals fail while upgraded cooling retains engine operation; the upgrade still accumulates wear and is not unlimited thermal immunity.

## Tuning slider gestures

All numeric garage tuning sliders, plus the shared AWD and fitted-controller sliders in the physical bench, accept normal left dragging, **right-button dragging at one-tenth sensitivity**, and **middle-click reset**. Right pressing anywhere on the track starts a relative adjustment without jumping to the pointer. Release ends the adjustment, including release outside the track. Values stay within their legal range.

Reset restores the field default, not the last saved tune: stock tire radius/width, authored final drive, 30 psi in the chosen pressure units, stock launch/automatic-shift RPM, and unity induction multipliers. Programmable limiter defaults to engine redline; shift light defaults to redline minus 200 RPM. AWD fields restore authored hardware calibration, clamped to the current mode/other-field limits. This does not buy hardware, grant unavailable controls or overwrite other fields. Garage gestures edit the draft and refresh comparison stats; **Apply** remains required, and Revert/leaving discards edits. Physical-bench configuration edits keep existing pedal ownership.

Gesture checkpoint: 1337 existing automated checks, 15 actual-mouse gesture checks in `reports/player-fixtures/tuning-slider-gestures-focused`, and 21 physical-bench input/replay checks in `reports/player-fixtures/tuning-slider-gestures-driving` pass with zero runtime/GUI/DSP errors. See `reports/VERIFICATION.md` for the separate legacy shop-preview assertion failure and test coverage limits.

## Component maintenance shortcuts

Garage and Parts show installed failed-component and worn-component counts. Failed flags and zero health both mark a failure, even when a failed component retains some health. Worn means 50% health or less; it is a service display threshold, not a new physical failure rule. Loose inventory and other cars are excluded. Parts category buttons mark affected categories as FAILED or WORN.

The **Review failures / replace components** shortcut opens a failure-first Repairs list, with component name, health bar, cause, new-part preview, **Buy same + replace** at the shown price, the healthiest usable compatible loose spare, and a link to that category's alternatives/inventory/donors. Failed spares are excluded. Fitting a used spare retains its health; buying creates a new identity. Both use the existing atomic save/ownership transaction, retain removed damaged originals in inventory, preserve the saved tune, and refresh the list. Insufficient cash disables the new purchase and shows the shortfall. No bulk purchase or automatic tuning is performed.

Race results with failed components link directly to Repairs after committing the final live wear. Physical bench diagnostics and starting-condition controls prioritize live failed components and allow selecting their condition controls; bench repairs remain isolated and do not buy player parts.

## Pinned stats summary

The car-stat panel keeps a compact summary above its dyno and scrolling details. Saved cars show power, torque, translating weight and the copied standard quarter-mile probe, plus lowest component health/drive layout. Preview cards show current → proposed only where a displayed value changes, colored green for a favorable change, red for a drawback or an unavailable result, and amber for a contextual tradeoff. Cooling, braking, clutch capacity, wheel load/radius, flywheel inertia, pressure, gearing, induction/equipment and selected-component ratings feed a short change list; up to four additional changes remain pinned and other changes remain in the detailed stats. Forged-internal ratings can increase while power stays unchanged. Incomplete probes show Unavailable, not zero-second ET. Units follow shared settings; scrolling or opening advice does not move the summary. No save/physics/calibration changes.
