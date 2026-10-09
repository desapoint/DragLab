# Slice 2 audit and 2F–2J checkpoints — 2026-10-08

The requested audit is complete, and phases **2F through 2J are implemented and verified in sequence**. Current shared model: `draglab-tire-life-v10`, wheel core `draglab-wheel-contact-v3`. **2K–2L remain planned**; this is not a declaration that all Slice 2 or the commercial game is complete.

The project and all game work run locally on E:. The restricted command launcher failed while enumerating an unrelated Z: network volume before it could run commands. An alternate approved command launcher works directly in this workspace; no Z: contents, mappings or credentials were needed or changed.

## Final evidence

| Gate | Result | Evidence |
| --- | --- | --- |
| Full Unity 6000.6.4f1 foundation | 2,334 checks pass, UTC 2026-10-08T16:09:57.5141657Z | unity-verification.txt; phases/2j/unity-verification.txt |
| Varied-car integrated audit | 1,749 checks, 834 runs, 14,425 simulated seconds; **zero failures**, UTC 2026-10-08T16:17:11.2846483Z | slice-2-audit/verification.txt, runs.csv, parts.csv |
| Compound/contact/wear/replay | 139 checks included in foundation | phases/2j/verification.txt; compound road CSVs |
| Physical tire failure/save/replacement | 5 additional checks | phases/2j/failure-verification.txt, physical-tire-failure.json |
| Stock endurance | All 12 scenarios pass; lowest carried component health 94.38% | stock-durability/verification.txt, parts.csv |
| Native physical bench | 33 checks, zero errors | player-fixtures/slice2j-final-driving |
| Native destructive downshift and continuation | 14 checks, zero errors | player-fixtures/slice2j-final-driving-failure |
| Native pinned stats/tire tradeoffs | 10 checks, zero errors | player-fixtures/slice2j-final-stats-summary |
| Native inventory mouse workflow | 10 checks, zero errors | player-fixtures/slice2j-final-parts |
| Native inspection/Apply/Revert/units | 14 checks, zero errors | player-fixtures/slice2j-inspection |
| Actual Windows race/shutdown/save | All four stock cars healthy above 98%, zero errors; four persisted tire histories validated separately | player-fixtures/slice2j-durability; phases/2j/native-save-validation.txt |

Counts are separate gates with overlapping scenarios; do not add them as unique test coverage. The final physics gate includes 68 mass, 595 contact-core, 140 driving-contact, nine AWD-contact, 46 fuel and 139 tire checks alongside prior ownership, inventory, audio, reset, tuning, wear, failure and career gates. UI-only follow-ups were compiled after the physics gate and checked in the final native fixtures. Full Windows data and source checkpoints for every phase are in phases/2f through phases/2j.

The integrated matrix exercises all compatible catalog replacements, four authored cars, five gearbox selections (factory/manual/DCT/automatic/CVT), six induction selections, three wear modes, idle/staging/neutral blips/mixed throttle/coast/brake, installed failure flags even at positive health, tire radius extremes, flywheel changes and all six AWD modes on the actual AWD pickup. It validates finite four-wheel state and all component/tire conditions. Unsafe boosted modifications may legitimately fail; universal survival is not the assertion. Separate contact matrices cover inertia, split friction, axle/center constraints and energy closure.

## Corrections found by the audit

The initial A–E audit found sixteen failed checks: generic internal option names and downstream wear/heat during uncoupled neutral revs. Names now describe material/function; driveline heat follows transmitted shaft power. Existing failed internals do not collateral-destroy healthy parts with wear disabled. Failure flags are authoritative even with positive health. Installed-part descriptions distinguish removal consequences, compatible controller functions and separately purchased limiter/two-step/shift-light modules. Nested-scroll mouse targeting was corrected in the test harness. The earlier report remains in slice-2-audit/AUDIT_PRE_2F.md; original failures remain in slice-2-audit/baseline.

2H integration found assisted launch torque continuing after wheels overtook the engine, premature launch-assist release on wheel spin, shift protection using crank flare, a missing physical center-LSD transfer and Unity JSON double decoding altering checkpoint continuation by one ULP. Shared coupling now respects finite engine/wheel inertia and ground-speed engagement, protection uses the wheel-imposed target, center LSD transfers bounded friction torque from measured axle differences, and exact checkpoint numbers accompany readable JSON. Braking heat follows actual dissipated brake work; neutral and open-clutch shifts preserve wheel spin until contact removes it.

2I found raw carbureted factory cut behavior differing from its installed build. Both now consistently use modeled ignition cuts. Empty fuel removes combustion and permits engine momentum to decay without marking an engine failure; refuelling does not repair or automatically restart it. Refuel/ballast controls keep keyboard pedal ownership and motion. The diagnostics background was made opaque so the car sprite cannot obscure text.

2J stock endurance exposed excess heat/life loss: initially all contact heat entered the rubber and the sliding-work life budget was too short. Tires now receive a declared 45% heat partition, have independently authored compound reserves, and suffer gradual thermal aging. No endurance assertion was weakened. A subsequent 834-run matrix caught four existing failure causes being overwritten; those causes are now retained. Native summary checks caught calculated mass changes pushing out a direct pressure edit; direct tuning edits now lead the pinned summary. Failing 2J evidence is preserved in phases/2j/baseline and player-fixtures/slice2j-stats-summary.

## Changes delivered sequentially

| Phase | Delivered behavior | Archived checkpoint |
| --- | --- | --- |
| 2F | Dry build/driver/payload/fuel mass; part replacement/removal moments; CG and dynamic axle loads; rotating inertia counted separately; independent bench mass controls and retained replay state | phases/2f: 1,405 foundation checks, native controls, full source/Windows archives |
| 2G | Isolated four-wheel angular/contact solver, measured slip/regrip, axle/center constraints and work/energy accounting; contact workbench and optional CSV | phases/2g: 595 core checks; isolated increment before race integration |
| 2H | One shared race/bench engine–coupling–wheel model; manual/DCT/converter automatic/CVT behavior; spin through shifts, real braking work, physical AWD/failed shaft paths and exact replay | phases/2h: 2,149 foundation checks, native controls, source/Windows archives |
| 2I | Live fuel consumption at idle/load/revs/cuts/anti-lag, empty-tank behavior, actual fuel mass, owned quantity/history and capped free-refuel controls | phases/2i: 2,195 foundation checks, 28 native checks, source/Windows archives |
| 2J | Four persistent tire histories within each owned tire pack; compound grip/life tradeoffs, pressure/load/slip-work heat/wear, cooling and failure contact; per-wheel bench setup/repair, CSV and shop stats | phases/2j: final evidence above and full source/Windows archives |

2J also separates rim and rubber mass: lightweight wheels reduce rim mass/inertia without reducing tire thermal/life reserve. Factory gearbox mass follows its actual transmission type, and differential mass placement follows the driven axle/center. Tire life uses double precision accumulation so small rolling-wear increments are not discarded. Reusing or selling a pack retains all four histories; fresh replacement retains the worn/failed original in inventory.

Current stock standard benchmarks: sport coupe 14.804 s / 156.7 km/h (C); hatch 16.987 s / 134.3 km/h (E); pickup 15.775 s / 142.1 km/h (D); V8 14.777 s / 162.5 km/h (C). These are fixture results with authored driver inputs, not real-car predictions. The starter still completes all four authored career events using earned purchases/tuning, without injected cash or weakened rivals; the final event probe is 14.050 s versus 14.387 s. It checks progression reachability, not the future fuel/repair economy balance.

## Preserved behavior, remaining scope and questions

Held W/Space survives E/Q/mouse shifting; E engages from neutral. Keyboard owns only driving pedals until explicit takeover/reset; independent setup edits retain its mode. Physical race and bench share the same model and installed downshift protections. Recording defaults off, simulation continues without a ten-minute stop, and recorded segments retain full chronology and exact replay. None/Partial 25%/Full wear, multipliers, optimal thermals, isolated starting conditions and repair remain. Shared audio, driving/unit settings, progression reset, draft Apply/Revert and modular direct upgrade/downgrade/reuse are retained. Fuel remains free in Slice 2.

No unresolved behavior questions remain from the earlier spontaneous requests. The tire inventory item currently represents a four-tire pack with four individual conditions; it does not introduce independent single-tire purchases.

2K still owns recoverable interrupted-active-run fuel/wear and final mechanical stress/failure calibration. 2L still owns complete authoring/telemetry/gear-edit coverage, all-system calibration, benchmark/class review and human/editor/device validation. Fuel pricing and the full career/cash/idle/operating-cost loop remain Slice 3. Procedural acoustic timbre complaints explicitly deferred by the user were not treated as fixed merely because DSP tests pass.

Parts, sizing, heat and consumption are fictional game calibrations. This is a longitudinal drag model with bulk tire temperature, approximate DCT handover and authored rim/tire mass distribution; no manufacturer accuracy, puncture model, separate tire surface/core temperatures, reverse/cornering or full suspension model is claimed. Contact-workbench code compiles, but its editor mouse/visual review is not automated. Windows fixtures do not validate Android devices or subjective engine sound/controller feel. Source/build archives are recoverable checkpoints, not a Steam release certification.
