# DragLab Audio Workbench — Independent Simulation Audit and Operating Guidance

**Scope:** The **standalone browser audio and vehicle simulation workbench** only. This is *not* an audit of the DragLab Unity game's current behavior or equivalence, and neither the status labels nor the example physical coefficients imply real-world calibration.

**Source inspected:** `DragLab_Audio_Workbench_Drivetrain_Graphs.html`, including its current acoustic-profile calculation and export controls; existing 74-row audit and recorded validation results. **No HTML, DSP, or simulation change is made by this document.**

## 1. Why does the Acoustic preset selector exist?

It is a **persistent, active DSP voicing multiplier**, not merely a quick button that populates the visible sliders. This behavior predates the most recent graph-instrumentation update and was already present in the Phase 1 workbench.

Three systems exist simultaneously:

| Setting | Actual effect | Stored in full JSON? |
|---|---|---|
| **Vehicle profile** / **Restore preset** | Replaces the whole vehicle/workbench parameter set, including acoustic preset, all slider values, engine and fuel hardware, modes, and most other config | Yes, as the resulting values |
| **Acoustic preset** | Picks an acoustic profile that multiplies *six* independent DSP parameters before the signal-processing stages | Yes, as `acousticPreset` |
| **Acoustic geometry sliders** | Scale those six preset-adjusted parameters (`runnerScale`, `collectorScale`, `resonanceScale`, `attackScale`, `blowdownScale`, `rarefactionScale`) | Yes |

The `Automatic` acoustic choice is **not** a neutral/flat setting: it resolves to Diesel, ClassicV8, HighRev, Compact or ModernV based on fuel system, layout, cylinder count and redline. The name therefore hides important sound-shaping differences. The preset is applied inside `profileFor(p)` and combined with the visible slider scales at DSP initialization; it does **not** update those slider positions to reflect the final values.

### Exact acoustic profile multipliers

These values are transcribed from the current workbench's `profileFor(p)`:

| Profile | Runner | Collector | Resonance | Attack | Blowdown | Rarefaction |
|---|---:|---:|---:|---:|---:|---:|
| Compact | 0.90 | 0.92 | 1.12 | 1.00 | 0.92 | 1.00 |
| ClassicV8 | 1.08 | 1.08 | 0.94 | 1.00 | 1.10 | 1.06 |
| ModernV | 0.94 | 0.96 | 1.05 | 0.94 | 1.00 | 1.00 |
| HighRev | 0.82 | 0.84 | 1.20 | 0.82 | 0.85 | 0.90 |
| Diesel | 1.04 | 1.05 | 0.88 | 0.86 | 1.16 | 1.12 |

**Example:** At `ClassicV8` with `runnerScale = 1`, the effective runner factor is still **1.08**. Changing the acoustic selector to `HighRev` with identical sliders changes the sound. The underlying base durations are also fixed in `profileFor`: attack **0.00020 s**, blowdown **0.0030 s**, rarefaction **0.0092 s**, each multiplied by the matching profile and user scale.

### Do we need presets if full configuration already saves everything?

**No, not for persistence.** `Export full preset` serializes the full `params` object into the `settings` field, which includes both the acoustic preset name and its sliders. Save/Load A/B snapshots also retain the values; the separate `Export Unity spec` mapping includes both acoustic preset and six scale fields. Thus the configuration already handles saving and loading.

**Recommended future UI simplification (not performed):** Make acoustic presets *one-time quick-setting recipes*. Selecting a recipe should populate the **visible sliders with the computed effective values**, then use a flat/neutral DSP base. Rename the control to `Apply acoustic starting point`, or remove it after migrating presets. There should not be hidden multipliers in the final active configuration if the goal is to isolate the effect of individual acoustic variables. Preserve the old sound by resolving the effective values before migration, and compare PCM before/after.

**Current workaround:** Keep the same acoustic preset selected when comparing other sliders or simulation modes. A slider value of 1.0 is *neutral relative to that preset*, not neutral overall. Changing **Vehicle profile** or pressing **Restore preset** intentionally changes many settings at once; do not use them for a one-variable A/B experiment.

## 2. What is in the CSV? Two different exports

**`DragLab_Workbench_Subsystem_Audit.csv`** is the *74-row simulation feature inventory* given below. Its six fields are:

1. `Area` — system category (Engine, Fuel, Transmission, Audio, etc.).
2. `Subsystem` — feature or physical mechanism assessed.
3. `Current_workbench_behavior` — what currently works or is represented.
4. `Status` — Complete (preview scope), Needs refining, or Missing.
5. `Required_refinement_or_missing_work` — recommended technical improvement.
6. `Priority` — urgency for future simulation development; blank for completed preview features.

This file is **not a log of physical telemetry and does not contain frame-by-frame RPM/boost**. The separate **CSV export inside the workbench's graph panel** is a *time-series telemetry recording*, containing per-frame samples with air/fuel charge, valve/phase metrics, driveline, wheel slip, engine speed, torque/HP proxies and other plotted values. The prior graph update reports 142 time-series CSV columns; that is a different export from this audit table.

### Audit status legend

| Status | Precise meaning | Count |
|---|---|---:|
| **Complete (preview scope)** | End-to-end implemented for the experimental browser tool; **not** an assertion of real-world fidelity | 14 |
| **Needs refining** | Has a working model or proxy but lacks meaningful coupling, measured calibration, or important physical mechanisms | 45 |
| **Missing** | No dedicated, functional simulation for that mechanism beyond a possible static indicator/proxy | 15 |
| **Total** | Inventory entries; not test-case counts | **74** |

Priorities refer to improving the standalone simulator as a tool for **hearing, tuning and comparing** physical audio causes, *not* to any game release plan.

## 3. What the current live charts mean

There are **22 time-series/simulation dashboard charts** plus an audio waveform scope and spectrum view (24 canvases total):

| Chart | Meaning / caution |
|---|---|
| RPM / limiter / target RPM | Actual browser RPM trajectory versus the limiter and gearbox target. Target is not necessarily achieved. |
| Turbo / manifold boost | Simulated manifold boost and normalized spool. Approximate and simulated induction differ. |
| Torque | **Estimated browser engine torque**, not a measured dynamometer output. Wheel torque also includes simplified transmission assumptions. |
| Horsepower | Derived from the estimated torque and RPM; **not independently simulated cylinder work**. |
| Throttle / load / clutch | Requested pedal, modeled effective plate, clutch engagement; not identical controls. |
| Clutch slip | Signed **gearbox-target minus engine RPM** (phase dependent) plus engagement indicator. Sign and engagement matter. |
| Shift transient | Relative jolt and pressure/engagement proxy; not measured forces or shock loads. |
| FL/FR/RL/RR tire slip | Signed longitudinal ratios: positive wheelspin, negative wheel lock. No cornering/slip-angle model. |
| Tire sound slip demand | Processed input actually delivered to tire sound voices. May be zero when raw contact slip is small. |
| Wheel tread versus road speed | Each modeled wheel surface speed and vehicle road speed. Use to recognize wheelspin/lock. |
| Traction / grip utilization | Estimated contact forces, friction capacity use, when detailed wheel simulation is enabled. **Unavailable in approximate wheel mode**. |
| Road speed / longitudinal acceleration | Browser speed and approximate derived acceleration, including braking. |
| Trapped charge / valve filling | Simulated valve-gated air accumulation and an indicative filling factor; not measured cylinder pressure. |
| Cylinder air charge | Estimated or accumulated mg per cylinder intake/working cycle depending on cylinder mode. |
| Cylinder fuel charge | Estimated or delivered mg per cylinder working cycle after the selected fuel model. |
| Air / fuel flow | Air in g/s; fuel in mg/s. Pay attention to separate unit scales. |
| Lambda versus target | Mixture relative to stoichiometry; compare actual and mapped target while fuel is flowing. DFCO makes lambda interpretation special. |
| Manifold / fuel rail pressure | Intake manifold pressure kPa and injection rail pressure bar use different axes. Some hardware has no relevant rail. |
| Fuel film / combustion | Manifold wetting reservoir and effective burn fraction. Approximate combustion may not follow every cylinder event. |
| Engine / glow / exhaust temperature | Reduced-order lumped temperatures, not full head/coolant/runner thermal maps. |
| Cylinder comparisons | Per-cylinder estimated/accumulated charges; do not treat approximate equal distribution as measured cylinder data. |
| Cam / valve timing | 720-degree four-stroke crank cycle, cam at half crank speed. Valve lift comes from authored mathematical shapes, **not measured lobe profiles**. |

**Important for tests:** Use consistent vehicle, map, model fidelity, sound stages, acoustic profile and RNG seed. Run the deterministic full sequence from reset before comparing; use freeze to inspect a transient, and export time-series CSV for numeric differences. Switching to detailed valvetrain or fuel models changes the upstream signal; it does **not** make the downstream sound physically calibrated.

## 4. How to test meaningfully

| Question | Control or setup | What to inspect | Interpretation |
|---|---|---|---|
| Does detailed fuel delivery matter? | Change **Fuel** Approximate ↔ Simulated only | Fuel flow, lambda, fuel-film, PCM A/B | Keep combustion, afterfire, cylinder and acoustic modes unchanged |
| Does simulated valve motion change charge? | Change **Cylinder charge / valvetrain** only | Intake lift/phase, trapped air, per-cylinder fuel/air | Helps isolate the cost/benefit of valve-gated filling |
| Is wheelspin physical or an audio artifact? | Set **Wheels** Simulated; vary road grip | Four signed wheel slip, contact force/grip, audio slip demand | Sound threshold and physical slip are distinct |
| Is clutch synchronization improved? | Use 3rd→2nd downshift; rev-match Off versus factory/Assist | RPM mismatch, clutch engagement, shift jolt, PCM | Compare identical speed, RPM and clutch setting |
| Does exhaust chemistry justify CPU cost? | Change **Afterfire** only | Chemical reaction packets, audible bursts, WAV, render time | Packet count and audible pops need not match |
| Is the engine sound too quiet on overrun? | Zero throttle at high RPM and repeat | RPM, combustion, manifold, engine audio RMS | Fuel cut can be real without total engine silence |
| Are results stable? | Same full-sequence start and seed twice | CSV time-series and WAV PCM | Drift indicates state not reset or unintended mode coupling |

**What a successful test does not establish:** a calibrated HP figure, OEM injection timing, real measured tire friction, actual cylinder pressure, realistic acoustic propagation, or full realtime AudioWorklet CPU safety. Those remain explicitly separated in the inventory.

## 5. Recommended development order

1. **Close cylinder work → torque → RPM:** compute indicated cylinder work and pumping/friction losses, drive crank rotational inertia, and derive torque/HP from physical work rather than preview curves. *Why first:* it makes other system adjustments causally change engine response.
2. **Make air, fuel and valve flow mass-conserving end-to-end:** intake plenum, valve curtain/pressure-driven mass flux, fuel film, trapped residual and cylinder combustion. *Why:* improves AFR, torque, exhaust pulse and backfire physics together.
3. **Add clutch torque/energy and nonlinear tire contact:** include clutch friction energy/heat, engine/wheel inertia, slip-force curves, ABS pressure response and torque transfer. *Why:* improves launches, shifts and tire sounds with fewer arbitrary terms.
4. **Connect cycle-resolved combustion pressure to the audio pulses:** use each cylinder's charge and pressure event for sound rather than an independent synthesized pressure envelope. *Why:* makes acoustic differences the result of simulation changes.
5. **Benchmark physics separately from DSP, then calibrate:** track physics-only CPU, PCM CPU, memory and audio dropouts across repeatable scenes. *Why:* only then can you judge whether a detailed model is worth its runtime cost.

Less urgent additions include VVT hardware, knock/ignition faults, starter/accessory loads, catalyst/DPF/SCR, surface/wear/thermal tire behavior, gearbox whine, and moving-listener Doppler; their scope and priorities are recorded below.

## 6. What was validated, and what was not

The existing drivetrain-graph update reported that the live browser graphs, 22 chart canvases, telemetry CSV, four-wheel slip and clutch channels worked and that sample-matched PCM output was unchanged relative to the immediately preceding workbench in the tested modes. The present documentation task **inspected the files and CSV but did not rerun all those audio and browser tests**. No physical model, sound coefficient or UI behavior was changed.

The labels **Complete / Needs refining / Missing** reflect source and workbench test observations and are **not** calibrated engineering certification. No comparison with the external DragLab game is implied.

---

## 7. Full 74-subsystem inventory

| Area | Subsystem | Current behavior | Status | Remaining work | Priority |
|---|---|---|---|---|---|
| Workbench | Per-system Approximate / Simulated modes | Eight independent selectors: wheels, drivetrain, limiter, induction, fuel, cylinder charge, combustion, afterfire | Complete (preview scope) | Keep A/B isolation tests as further components arrive |  |
| Workbench | Seeded procedural reproducibility | Seeded DSP and chemical event streams; reset at each full-sequence start | Complete (preview scope) | Check parity when adding new physics |  |
| Workbench | Full driving sequence | Repeatable warm 18.5-second suite; optional cold preheat/crank prelude; coverage markers | Complete (preview scope) | Add test cases that isolate rare and failure states |  |
| Workbench | Interactive driver controls | Throttle, race/neutral, up/down shifts, launch and brake, manual sound injection | Complete (preview scope) | Add analog brake/clutch controls for detailed tests |  |
| Workbench | A/B sound rendering and WAV export | Matched scripted PCM output with selectable category and stage isolation | Complete (preview scope) | Add per-subsystem physics-only benchmark and perceptual metrics |  |
| Workbench | Live graphs / CSV | 22 charts and per-cylinder CSV with time, slip, fuel, combustion, RPM, power, pressure and timing | Complete (preview scope) | Add interval/event bookmarks and replay scrubbing |  |
| Workbench | Reference data replay | Can replay imported reference RPM/boost/gear/sound events, not full vehicle state | Needs refining | Extend trace schema for mass flow, clutch, tire, torque and valve/pressure events | Medium |
| Workbench | Runtime performance measurement | Offline PCM render timing comparison includes physics and DSP combined | Needs refining | Measure physics CPU, DSP CPU, allocations and realtime dropout separately | High |
| Workbench | Sound/physics model calibration | Tunable authored constants and vehicle presets; no measured calibration basis | Needs refining | Fit parameter families against independent dyno, telemetry and recordings | High |
| Engine | RPM response / free rev | Idle control and throttle-driven RPM integrator with drag/limits | Needs refining | Conserve rotational energy with inertia and torque curves, accessory loads | High |
| Engine | Crankshaft/flywheel dynamics | Simplified acceleration and gear-coupled RPM transfer, not an inertia solver | Needs refining | Implement engine inertia, clutch torque and load work as shared state | High |
| Engine | Torque versus RPM | Preview torque baseline and parabolic speed/load modifier | Needs refining | Map torque/IMEP from delivered combustion energy, pumping/friction and volumetric efficiency | Critical |
| Engine | Horsepower | Computed directly from preview torque and RPM; not calibrated | Needs refining | Use physically consistent torque before treating HP as a performance result | High |
| Engine | Engine braking | Throttle-dependent road drag and RPM decay, qualitative only | Needs refining | Model pumping losses, friction mean effective pressure and driveline reaction | High |
| Engine | Limiter control | Hard/soft thresholding, ignition/fuel cut and two-step cadence are selectable | Needs refining | Use per-cycle cut strategy and realistic spark/fuel event scheduling | Medium |
| Engine | Idle / start / stop | Warm idle, cold-soak start attempts, fuel-dependent starter thresholds | Needs refining | Starter torque, starter sound, idle control loops, failed-start wetting | Medium |
| Engine | Accessory loads | Approximate drag folded into total engine resistance | Missing | Explicit alternator, pump, A/C and cooling-fan loads | Low |
| Engine | Cylinder-to-cylinder crank torque | Pressure-event audio and charge accumulators do not integrate cylinder pressure into crank torque | Missing | Per-cylinder pressure/work and crank inertia coupling | High |
| Transmission | Manual clutch engagement | Disconnected during shift; progressive engagement and signed RPM mismatch | Needs refining | Torque capacity, friction slip energy/heat and bite-point mapping | High |
| Transmission | Clutch damage and heat | Clutch jolt is relative; no actual thermal capacity or wear state | Missing | Slip power to heat, glazing, wear and failure thresholds | Medium |
| Transmission | Manual rev matching | Optional factory auto-blip and driver assist change throttle/RPM | Needs refining | Pedal/clutch timing, heel-toe, real synchronizer resistance | Medium |
| Transmission | DCT handover | Approximate retained torque and RPM synchronization | Needs refining | Independent odd/even clutch torques, preselection and real shift timings | Medium |
| Transmission | Torque-converter automatic | Retention and generic re-engagement; no hydraulic converter solution | Needs refining | Pump/turbine speed, torque multiplication, lockup and shift logic | Medium |
| Transmission | CVT | Simple continuously adjusted effective ratio within bounds | Needs refining | Belt/pulley pressure, torque limit, slip, efficiency and thermal behavior | Medium |
| Transmission | Gearbox synchronizers / gear whine | Ratio/shift delays only; no torque-dependent meshing or sound | Missing | Input-shaft inertia, synchronizer friction, bearing and gear-mesh noise | Medium |
| Driveline | Differential and AWD torque split | AWD/FWD/RWD fixed shares in wheel-contact model | Needs refining | Open/LSD/locked diffs, front/rear shaft inertia and traction-dependent torque split | Medium |
| Driveline | Longitudinal vehicle speed | Integrated from contact-limited forces or simple acceleration surrogate | Needs refining | Torque-to-wheel force via full ratio/inertia and grade/aerodynamic drag | High |
| Driveline | Aerodynamic drag | Speed-dependent heuristic, not CdA and air density model | Needs refining | CdA, air density, downforce, wind and grade | Medium |
| Tires | Four-wheel longitudinal surface speeds | Four individual tread speeds, friction constraints and signed slip estimates | Needs refining | Slip curves, relaxation length and torque balance with actual wheel inertia | High |
| Tires | Per-wheel tire sound | Independent procedural tire voices driven by slip/demand and wheel speed | Complete (preview scope) | Calibrate tonal textures and contact patches from recordings |  |
| Tires | Tire physical slip versus sound intensity | Signed physical proxy separated from smoothed squeal demand; manual forced slip | Complete (preview scope) | Retain separation and add independent lock-up/rolling noises |  |
| Tires | Static weight transfer | Approximate longitudinal CG-based load redistribution | Needs refining | Pitch dynamics, springs/dampers and corner-load measurements | Medium |
| Tires | ABS braking | Caps brake force below friction threshold in simulated wheel contact | Needs refining | Slip feedback controller and pulsating brake-pressure dynamics | Medium |
| Tires | Tire temperature, pressure and wear | Not advanced by the driving model | Missing | Thermal network, tread wear, pressure and compound-specific grip | High |
| Tires | Lateral/cornering forces | Longitudinal-only contact, no steering/yaw | Missing | Combined-slip tire model with slip angle and aligning torque | Low for drag |
| Tires | Surface conditions | One overall grip parameter; no varying track surface | Needs refining | Track prep, water, surface temperature and per-wheel road friction | Medium |
| Fuel | Selectable fuel-meter fidelity | Legacy approximation and multi-technology fuel-map simulation independently selectable | Complete (preview scope) | Maintain old audio/event behavior in A/B |  |
| Fuel | Gasoline VE / lambda maps | Interpolated RPM/load surfaces and three air-metering strategies | Needs refining | Measured VE calibration, transient correction and sensor error | High |
| Fuel | Carburetor delivery | Two-/four-barrel differences, bowl, accelerator pump and manifold film | Needs refining | Venturi pressure/fuel circuits, jets, choke, air bleeds and vaporization | Medium |
| Fuel | Throttle-body / port EFI | Injector limits, rail state, fuel-film and closed-loop estimate | Needs refining | Cranking tables, latency, fuel trims and individual injector pulse scheduling | Medium |
| Fuel | Gasoline direct injection | Direct-cylinder fuel with rail estimate and near-zero manifold film | Needs refining | Injection timing/split, spray/evaporation, knock/stratification | High |
| Fuel | Mechanical / rotary diesel pumps | RPM-demand quantity maps with different rack response | Needs refining | Pump delivery curves, mechanical governors and timing advance | Medium |
| Fuel | Common-rail diesel | Rail dynamics proxy, smoke limit and pilot/main/post split settings | Needs refining | Hydraulic rail balance, injector needle timing and real injection rate | High |
| Fuel | Fuel film conservation | Fuel reservoir tracks incoming, retained and delivered masses | Complete (preview scope) | Integrate with separate plenum/cylinder conservation for end-to-end budget |  |
| Fuel | Fuel pump / starvation / cavitation | Some injector flow and rail limits; no tank line/pickup supply dynamics | Missing | Tank pickup, filters, vapor-lock, pressure drop and pump heating | Medium |
| Fuel | Ethanol/flex-fuel / gaseous fuels | Gasoline/diesel chemical parameter families only | Missing | Fuel density, heating value, stoichiometry, vapor pressure and flex sensor | Low |
| Intake | Throttle / intake charge | Plate, boost, simple speed-density/MAF/Alpha-N airflow estimates | Needs refining | Plenum filling conservation, individual runner pressure and reversion | High |
| Intake | Turbo compressor spool / manifold pressure | Rotor and manifold states independently switchable | Needs refining | Compressor map, shaft inertia, wastegate flow, surge and air temperature | High |
| Intake | Supercharger and twincharged operation | Boost and tone preview with basic pulley effects | Needs refining | Mechanical power draw, bypass maps, intercooler and staging | Medium |
| Intake | Intercooler / charge cooling | Authored charge temperature; no full intercooler thermal mass | Missing | Charge-air cooler effectiveness, heat soak and pressure loss | Medium |
| Valve train | Cam phase and valve-lift profiles | 720-degree crank/cam phases, analytic lift envelopes and port imbalance | Needs refining | Cam lobes by cylinder/bank, measured lift-vs-angle and true timing events | High |
| Valve train | Valve-flow-gated cylinder mass | Per-cylinder intake/exhaust gating with air and fuel accumulation | Needs refining | Pressure-difference mass flow, valve curtain area, residual EGR and overlap/reversion | Critical |
| Valve train | Valve float | Lift/fill effectiveness falls above modeled spring-RPM limit | Needs refining | Follower dynamics, spring mass, bounce and valve lash | Medium |
| Valve train | Variable valve timing/lift hardware | Fixed adjustable offsets only; no RPM/load-actuated VVT control | Missing | VVT phaser map, cylinder-bank phasing and multi-profile switching | Medium |
| Combustion | Chamber burn fraction / timing | Fuel/air, advance, mixture, quality and flame-speed surrogates | Needs refining | Cycle-resolved heat release, ignition delay and indicated work | Critical |
| Combustion | Ignition and cylinder failures | Fuel/ignition limiter and general cold-quality factors | Needs refining | Independent ignition coils/plugs, misfire probabilities and partial burns | Medium |
| Combustion | Knock, pre-ignition and detonation | No live pressure-temperature knock solver or timing correction | Missing | Knock tendency and ECU response, pre-ignition/damage states | High |
| Combustion | Diesel auto-ignition | Cetane, cold quality and pilot/main/post influenced combustion surrogate | Needs refining | Compression pressure/temp, ignition-delay kinetics and injection rate | High |
| Combustion | Glow-plug thermal preheat | Heating state influences cold start, not a spark event | Needs refining | Ambient/head temperature, individual plugs, post-glow controller/failure | Medium |
| Thermals | Engine warm-up | One lumped engine thermal state and startup quality | Needs refining | Coolant/oil/head/liner thermal nodes and losses | Medium |
| Thermals | Component wear and failures | Not physically evolved for engine, fuel, wheel, clutch and exhaust components | Missing | Thermomechanical wear and service states affecting simulation | Low for audio |
| Exhaust | Eight-zone transport | Fuel/O2/heat/flame/pressure and deterministic ignition with event packets | Needs refining | Calibrated turbulence/ignition and geometry per header/catalyst/muffler | High |
| Exhaust | Exhaust pulses and flow acoustics | Cylinder pressure pulse envelopes, runner delays, collector, filter parts | Needs refining | Pressure/flow acoustic network with measured area/impedance losses | High |
| Exhaust | Procedural V3 backfire timbre | Editable multilayer burst generator with seed variation and export | Complete (preview scope) | Calibrate realism by recordings; sound quality is subjective |  |
| Exhaust | Afterfire mode selection | Original probabilistic event scheduler or chemistry-driven event mapping | Complete (preview scope) | A/B events using identical underlying driving input |  |
| Exhaust | Catalyst / DPF / SCR chemistry | No dedicated conversion, oxygen storage, diesel particulate or aftertreatment | Missing | Aftertreatment masses, temperatures, backpressure and emissions | Medium |
| Audio | Combustion audio pulse scheduler | Cylinder count/order and pressure-body tables with stochastic pulse color | Needs refining | Drive each cylinder from the new independent pressure/combustion state | High |
| Audio | Intake rush and mechanical clatter | Noise/filters and tonal excitation tied to throttle, load and fuel type | Needs refining | Valve events, mechanical impulse sources and intake acoustics | Medium |
| Audio | Turbo, supercharger, BOV voices | Tonal/filter noise and valve envelopes with separately switchable processing | Needs refining | Blade pass order, maps, surge/flutter and geometric exhaust interaction | Medium |
| Audio | Tire skidding audio | Four unchanged tire voices; force slip tests and wheel contact inputs | Complete (preview scope) | Sound tuning/recorded comparison still recommended |  |
| Audio | Distance / reflections / stereo | Direct and fixed reflected paths, attenuation and air loss | Needs refining | Moving listener/doppler, geometry, occlusion and variable environments | Medium |
| Audio | Road noise / gear whine / wind / chassis | No dedicated complete rolling road/gearbox/chassis vibration voices | Missing | Add isolated road, wind, gear whine and drivetrain vibration buses | Medium |
| Audio | PCM limiter / output safety | DC removal, saturation, master gain and finite clamp in renderer | Complete (preview scope) | AudioWorklet realtime stress/dropout assessment |  |
| Audio | Listener movement and Doppler | Fixed listener relative to source; no moving sound geometry | Missing | Dynamic position, relative velocity, and time-varying propagation | Medium |


---

## 8. Related artifacts

- `DragLab_Workbench_Subsystem_Audit.csv` — the machine-readable 74-row feature inventory above.
- `DragLab_Audio_Workbench_Drivetrain_Graphs.html` — standalone browser workbench with the existing graphs and sound tools. No changes to this file in the documentation task.
- `DragLab_Drivetrain_Graphs_Preview.png` — reference image of the dashboard.
- `DragLab_Drivetrain_Graphs_Audit.md` — earlier audit-only Markdown from the graph update.

**Suggested acoustic preset change is only a design recommendation; it has not been implemented.**
