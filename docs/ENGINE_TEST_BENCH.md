# Standalone engine and sound bench

Calculated flywheel settings are available on **Engine**, with a mass multiplier in tune settings and Garage. **FlywheelCoastDown** compares neutral decay from the same 5000 RPM; **DynamicPullAndShifts** exercises physical RPM through shifts and release. The lower dyno strip plots RPM independently of combustion HP and shaft Nm. See [FLYWHEEL.md](FLYWHEEL.md) for the model, controls and fixed-RPM fixture distinction.

The **Scenarios** tab's **Size test clutch for current torque + flywheel / 0.20 s sync** button sets the copied bench spec's clutch rating explicitly. Use it when comparing normal loaded shifts after engine/boost/flywheel changes. Keep the stock rating to investigate an under-rated clutch and prolonged slip. This never writes the factory car asset or player save.

Open **DragLab → Open Engine Sound Test Scene**, then press Play in Unity. The scene is `Game/Assets/DragLab/Scenes/EngineTest.unity`. The Windows game also has an **Engine / sound test bench** button in Garage; `tools/Open-EngineBench.ps1` launches it directly.

The bench starts without the race cycle. It has no countdown, finish, rewards or player-profile access. All edits operate on copies of vehicle definitions. Selecting a stock car restores its authored defaults and fresh engine state while retaining keyboard driving ownership if active; held gas keeps working. Reset creates fresh engine, compressor, thermal and audio state while retaining the hardware configuration. Reset returns all operating controls to neutral idle, including pedals, fixed RPM, load, roller/slip and injected warmth/wear. Importing JSON restores saved manual operating controls too.

**Settings** contains the same saved **Master volume** as the garage and race pause menu. It defaults to **75%**, works while keyboard or concert mode owns engine controls, and persists separately from the player profile. Drive and Mix display the shared level; their former independent master sliders have been removed. Hardware/configuration resets and A/B/preset imports do not change this setting. Captured JSON `volume` remains only an offline WAV reference level.

Master now scales generated samples after saturation, with a short ramp and exact mute. A **Generated output** dBFS RMS meter on Settings shows its real effect. Actual mouse drags save on release. **Mix → idleBoostDb** defaults to +6 dB (−6 to +12 available), fading with throttle/RPM and bypassed during two-step. Mix remains editable during keyboard driving and its edits preserve the active input source. Stop the concert before editing its temporary sound setup. Offline bench/concert exports use the same post-synthesis reference-gain stage.

## Dyno concert Easter egg

The **Dyno concert / random classic** button, beside **Show car / wheels**, randomly chooses a short public-domain theme: Beethoven's **Ode to Joy**, Beethoven's **Für Elise**, or Grieg's **In the Hall of the Mountain King**. Consecutive selections avoid immediately repeating the last tune. The existing engine/exhaust voice plays the notes: four-stroke firing frequency determines RPM (`note Hz × 120 / cylinders`), transposed by whole octaves to fit the selected engine's idle/redline range. Live Drive sliders and the dyno show the changing fixed RPM, throttle and load; the engine stays in neutral. Brief volume envelopes articulate notes and rests while the engine remains running.

Concert mode temporarily bypasses limiter/two-step/anti-lag cuts and mutes accessory/pop layers so the melody remains legible, retaining engine and exhaust character. It restores the copied pre-concert hardware, mix and operating controls when finished or stopped. **Pause / Esc** freezes playback; **W/Space**, other driving controls, **Use keyboard/sliders**, **Reset**, or choosing another car interrupts it. Restoring a configuration creates fresh engine/audio state, rather than rewinding mechanical history. Themes use the same fixed 120 Hz bench and actual engine DSP; no separate musical instrument or prerecorded song is used. Reproducible engine-only auditions are in `reports/dyno-concert/`.

## Manual controls

| Action | Keyboard / mouse |
| --- | --- |
| Throttle | Hold W or Space; hold the Gas button; or set the throttle slider |
| Clutch | Hold Left Shift; hold the Clutch button; or set the clutch slider |
| Brake / test resistance | Hold S; hold the Brake button; or set the brake slider |
| Neutral / gears | N; E/Q; or Neutral / Shift buttons and gear slider |
| Reset | R or Reset |
| Pause / resume | Escape or Pause / Resume |
| Static RPM | F or fixedRpm toggle; set targetRpm; brackets adjust ±100 RPM |
| Dyno load | Up/Down arrows or load slider |
| Tire spin | B toggles slip with 40 km/h rollers; roadSpeedKmh/tireSlip sliders give finer control |
| Repeatable tests | F5 current / F6 captured repeat / F7 A / F8 B |

Throttle holds remain independent of shift/clutch input. Numeric text fields allow precise values as well as sliders. **W/Space always claims driving control**, even after a numeric field retained focus. Other shortcuts remain suppressed while entering numbers; Enter leaves text editing. Mouse/keyboard pedal input interrupts a running scenario and returns to manual testing. Focus loss pauses sound.

For remote-desktop testing, the game can only observe the key events delivered to the host. If a remote client forwards a gas-key release when another key or mouse action is used, the bench correctly closes the pedal; it cannot distinguish that event from actually releasing W. The bench does not use the race's driving-assist preferences. To avoid held-key forwarding, choose **Use sliders**, assign throttle, and use the **mouse Neutral / Shift buttons**; those buttons preserve slider ownership and assigned throttle. E/Q explicitly takes keyboard ownership, so it requires correctly forwarded held gas. Compare against direct local keyboard input before changing simulation or assists. TeamViewer forwarding has not been verified by the automated local tests.

The **Drive** tab shows the current control source and live applied controls. Keyboard pedal presses/releases and scenario timelines move the displayed gas, clutch, brake, gear, load and RPM-target sliders; actual engine RPM has a separate read-only slider.

- **Sliders:** **Use sliders** explicitly selects editable, latched controls. Assigning a value holds that operating setting until Reset or a driving key/held-pedal button takes over.
- **Keyboard:** **Use keyboard** or a driving key takes over persistently, clears latched pedals, disables the previous fixed-RPM pin and releases warmth/wear overrides. Pedals follow held keys and return to zero on release; old slider gas cannot reappear. Current gear, dyno resistance and roller settings remain available. F explicitly enables/disables a diagnostic RPM pin after takeover.
- Keyboard mode makes only the **keyboard-controlled Drive fields** read-only: pedals, gear, fixed/target RPM, load and roller/slip controls show applied feedback. Engine, Induction, Exhaust, Gearbox/wheels and Mix remain editable. Hardware dropdowns, sliders, numeric fields and Apply now preserve keyboard ownership and held pedals, including after delayed application; they never restore old slider gas. Drive's engine on/off, launch-control, failure and warmth/damage test options remain editable independently. **Use sliders** explicitly hands driving control back using displayed settings. Shift buttons preserve the active pedal mode.
- **Scenario:** the captured timeline supplies controls and updates their displays. Keyboard or slider edits interrupt it. A completed scenario remains paused at its final displayed inputs until another action.

The bench remains a dyno fixture: keyboard mode evolves the engine normally, but roller speed/load are test inputs rather than complete road dynamics.

## Live dyno and previous runs

The right-hand live dyno starts recording automatically. **End run** archives a manual recording; **Record** begins another. Reset, selecting a car, applying hardware and starting/repeating a scenario separate recordings and preserve their completed traces. Scenario recording ends after the complete timeline, including the release tail. Pause freezes capture time.

Use **Axis: time / RPM** to switch views. Time makes shifts, idle and constant-RPM throttle release easy to see. RPM connects samples in their original order, including falling RPM and repeated RPM with different power; it is not a sorted peak-power curve. Every 120 Hz tick is retained from start through finish, including partial throttle, zero output and transmission interruptions. HP uses the left scale, Nm the right. All curves are continuous: thick engine HP, amber engine Nm and thin violet **Shaft Nm**, enabled by default, showing transmitted torque before gearing. Overlapping line segments and chronological extrema-preserving screen reduction keep short cuts, RPM reversals and the zero-output tail visible; exports retain every tick. Gearbox handovers can interrupt shaft torque while engine HP remains active; CSV's shift_cut flag includes this shaft interruption.

The six latest completed runs are layered in separate colors. Their checkboxes hide/show overlays; hover a label for its car/scenario/A/B label and ending reason. **Clear old** removes previous overlays while keeping the current recording. Identical repeats overlap exactly. **Show car / wheels** temporarily reveals the animated car; recording continues while the graph is hidden.

**Export** writes each retained run's full chronological CSV, captured configuration JSON and run metadata beside the bench's file-path setting. CSV includes time, RPM, engine Nm/HP, transmitted Nm, gear, pedals, dyno load, boost, shift/limiter flags, flywheel/total inertia, stored flywheel energy and angular momentum. Export while live gives a snapshot without stopping the run. Runs lasting ten minutes split into complete bounded segments; history keeps the latest six whole segments. Export before older segments leave history if you need a longer session archive. Overlays are session-only; exported files persist.

`fixedRpm` pins engine RPM rather than integrating acceleration. It still advances real fueling, limiter cuts, boost/spool, heat and pop/valve events. Disable launchControls if you want neutral RPM testing without two-step. Choose lower RPM than the main limiter when testing ordinary steady combustion. The powered-on diagnostic range starts at the car's idle RPM and extends to 1.5 times redline. Targets below idle clamp to idle; normal powered-on testing never reaches zero, even during release/braking/shifting. engineEnabled explicitly switches power off. allowFailure enables actual overrev failure; default diagnostic protection caps wear below failure and clears stalled/failed flags, including injected failure state. warmthOverride/damageOverride use -1 for physical evolution, or 0–1 to inject controlled conditions.

Dyno load is independent of neutral/forward gear and opposes crankshaft output, so loaded neutral tests can develop boost without moving a car. Roller speed and tire slip are explicit acoustic/animation fixtures; the bench does not simulate a complete moving vehicle. Gear changes apply authored ratios and transmission interruption/retention. A manual handover disconnects transmitted shaft torque while **combustion and held throttle remain active**; it does not mute the engine or pretend the limiter is cutting. Real configured limiter cuts still apply. CVT hardware retains one drive gear and updates its continuous ratio. Use the race scene for complete clutch/launch/converter/vehicle performance testing.

## Configuration tabs

Planned Slice 2 additions: tank quantity/capacity/density/placement and consumption controls; **Add litres → Refuel** limited to tank free space; fuel/mass/force telemetry; and a physical driving mode with input replays and contact/fuel scenarios. Refills/reset/replay in development tools must remain isolated from owned-car saves and money. Current assigned RPM/load/roller/slip scenarios are acoustic fixtures and do not demonstrate real tire contact or fuel consumption. See [the driving-depth audit](DRIVING_DEPTH_AUDIT.md) and [Slice 2 requirements](GAME_PLAN.md#required-physical-driving-tools).

- **Engine:** displacement, inertia, torque samples, idle/redline, fueling/cam/warmup, limiter/cuts/two-step/anti-lag and upgrade/tune controls.
- **Induction:** stock topology, valve, pressure/flow, spool/coast, atmosphere, efficiency, intercooling and losses. Tune controls also expose aftermarket single/twin turbo, single/twin blower and mixed kits, sizes/boost/pulleys/valves. Engine stage 1 unlocks aftermarket kits; controls stage 1 unlocks adjustable launch functions.
- **Exhaust:** component chain, presets, strengths and per-part acoustic parameters.
- **Gearbox / wheels:** shift/handover, continuous-ratio specs, authored gear ratios and wheel mass/inertia.
- **Mix:** cylinders/layout, exhaust stage, bass/brightness/raw-pulse balance and independent engine/turbo/blower/valve gains. Physical displacement and mechanical clatter are on Engine; obsolete migration-only sound fields are omitted.
- **Drive:** pedals, gear, fixed RPM, dyno load, rollers/slip and temperature/wear injection; the shared volume is displayed.
- **Scenarios / Files:** repeat, A/B capture, reproducible JSON and WAV exports.

Hardware edits apply after a brief pause in editing, or with Apply now. Current operating state is retained when applying valid hardware edits; Reset is available for a clean start. Invalid engineering combinations display an error while the previous valid sound remains active. For example, raising redline also requires torque samples to cover the new range. Restore valid discards invalid draft changes.

## Exhaust components

Factory cars now own an ordered `Sound → Exhaust System` chain. Up to eight components can be added, removed or reordered. Their presence is not a boolean: **amount 0–1**, attenuation in dB, low-pass cutoff in Hz, pulse softening, resonance frequency/gain/Q control their contribution. Changing a part's kind loads a starting profile, then every value remains editable.

| Kind | Starting acoustic behavior |
| --- | --- |
| Pipe | Near-open path; coefficients can add pipe coloration |
| Absorptive silencer | Attenuation, darker upper band, softened pulses |
| Chambered silencer | Different attenuation/filtering and low-band emphasis |
| Resonator | Frequency-selective reduction of an authored band |
| Catalytic converter | Separate mild attenuation/filtering/softening profile |

Quiet, Chambered, Absorptive and Open presets are starting configurations. Amount zero bypasses a component exactly; an empty chain preserves the previous acoustic path. Exhaust stages retain their existing character changes and progressively reduce component attenuation/increase cutoff. Component changes shape combustion, overrun and exhaust pops; intake gulp, mechanical clatter, compressor/valve voices and tire feedback remain separate paths. Set engine/turbo/blower/valve gains and roller slip to isolate layers.

These are original tunable acoustic approximations, not measured manufacturer exhaust systems or a gas-flow/backpressure simulation. The component approach follows the qualitative distinction between muffler sound-wave attenuation and resonator tuning described by [Borla's exhaust FAQ](https://www.borla.com/support/faqs) and [resonator profiles](https://www.borla.com/products/universal-resonator-401339). Numerical catalytic/silencer transfer parameters are fictional calibration.

## Repeat and A/B workflow

Slice 2 must additionally capture the installed component definitions and their starting conditions, show per-part wear/stress/failure causes, and support repeatable wear/replacement/downgrade scenarios. Reset/replay and experimental parts are isolated from owned-car inventory, fuel, condition and cash. The current bench has no persistent owned-parts model; see [required wear tools](GAME_PLAN.md#required-persistent-mechanical-wear-and-replacement).

1. Configure an engine and choose a scenario. **Run current / F5** captures the configuration and begins with a fresh engine/audio generator.
2. **Repeat captured / F6** restores that capture, including volume, rather than reading subsequently edited sliders.
3. Capture A, change a parameter, then capture B. Run A/B with the same selected scenario for a controlled comparison. No automatic loudness normalization hides exhaust attenuation.
4. Export matching A/B WAVs and JSON. Exported PCM repeats exactly for identical configurations/scenario on the same runtime. Each export includes a scenario description and rate metadata. Live playback uses the same fixed-tick inputs, while render/DSP timing can differ slightly from the offline 60 Hz publication fixture; WAVs are the exact comparison reference.

| Scenario | Duration / behavior |
| --- | --- |
| WarmIdle | 12 s warm idle |
| NeutralBlips | 12 s; gas at 3–3.5, 6–6.5 and 9–10 s |
| LoadedPullAndShifts | 12 s; 2–9 s loaded sweep, shifts at 5/7 s, then lift |
| TwoStep | 12 s; held gas 2–9 s using the selected launch settings |
| TurboLift | 12 s; loaded 5,000 RPM, lift at 7 s, gas again at 9 s |
| Burnout | 12 s; 4,500 RPM, 40 km/h rollers, 90% slip at 2–9 s |
| ColdWarmup | 60 s cold idle with configured warmup/fueling |
| RpmSweep | 12 s idle-to-redline sweep at fixed throttle/load |
| FixedRpmHold | 12 s using captured target RPM, throttle, load, gear, roller and slip settings |
| FlywheelCoastDown | 12 s neutral; hold 5000 RPM for 2 s, then release into physical decay to idle |
| DynamicPullAndShifts | 12 s physical RPM/load; shifts at 5/7 s and throttle lift at 9 s |

Ordinary scenarios own the input timeline and warm initial condition; ColdWarmup starts cold. FixedRpmHold uses captured manual operating settings. TwoStep tests the chosen configuration: use the helper button to equip hard ignition two-step at 3,500 RPM, or compare it with disabled/soft/fuel-cut settings. All eleven scenarios are tested on all four stock engines. JSON/WAV exports go to `EngineBenchConfigs` beside the project's Assets folder or beside the built player's data folder by default, separately from player saves.

Evidence: `reports/engine-bench-verification.txt`, paired files in `reports/engine-bench/`, and the named Windows bench fixture in `reports/VERIFICATION.md`. Run the opt-in player check with `tools/Test-WindowsSlice.ps1 -EngineBenchOnly -KeyboardOnly -ShowPlayer`. The earlier reported blurping artifact remains a separate listening issue; this bench provides repeatable controls to isolate it.


## Manual selector hardware

On **Gearbox / wheels**, choose **Manual**, then **Manual gearbox selector hardware → Sequential**. The `sequentialShiftScale` field (default 0.45) controls the faster selection duration shared with race and physical-bench simulation. These configuration controls remain editable during keyboard driving. This models a single-clutch manual with clutch-assisted shifts; it does not automatically add ECU flatshift cuts or downshift protection. Gearbox layout is shown in the live engine summary.
