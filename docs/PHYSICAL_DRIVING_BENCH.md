# Physical driving bench

See [Component failures and AWD](COMPONENT_FAILURES_AWD.md). Select the pickup for AWD controls; detailed diagnostics show both drive paths, torque shares, delivered axle forces, slip estimates, brake capacity and all installed component conditions. Race and bench share the same behavior. Current physics identity is `draglab-integrated-tools-v15`; rerecord older scenarios. Full selectable channels and developer tools are documented in [integrated tooling](INTEGRATED_TOOLS_CALIBRATION.md).

Open **DragLab → Open Physical Driving Test Scene** in Unity and press Play. In the Windows game, use **Physical driving bench / this installed build** in Garage. The sound bench also links to this scene. `tools/Test-WindowsSlice.ps1 -DrivingBenchOnly -ShowPlayer` runs an isolated automated test; it is not the normal play launcher.

This scene uses `CarSimulation` for the engine, flywheel, clutch/converter, gearing, acceleration and braking. There are no race lights, distance finish, rewards or profile writes. Garage entry copies installed hardware and condition. Direct scene entry creates an isolated OEM configuration. Test-part changes reset only the bench and leave recorded configurations and player assets intact.

## Manual operation

- Hold **W / Space** for gas, **Left Shift** for clutch, **S** for brake.
- **E** engages first from neutral, then upshifts. **Q** downshifts and selects neutral from first; **N** explicitly selects neutral. Full manual hardware requires clutch for first engagement and forward shifts unless clutch assistance is enabled.
- Neutral revs independently while road speed coasts. Initial staging holds the brakes until drive is selected. Flywheel energy/RPM continue through shifts and neutral release.
- **R** resets to neutral idle; **Esc** pauses ticks and audio. A physically stalled engine can restart in neutral with **Enter**; a failed engine needs reset in this isolated tool.
- Keyboard activity owns pedals until reset or **Use sliders**. The disabled pedal sliders show live feedback. Explicit slider takeover latches analog values while simulation continues. Shift buttons preserve the current pedal source and held gas. Vehicle, test-parts and assist edits remain available in keyboard mode; their isolated hardware reset preserves keyboard ownership and held gas. For slider driving, hardware resets retain assigned pedals too.

Choose automatic, sequential or manual-clutch driving in the setup panel. Automatic means automatic shifting; throttle remains under your control. The separate **sound bench** provides static RPM/load/roller-slip overrides. Those acoustic overrides do not claim to validate road physics.

## Simulation difficulty and wear controls

**Settings** and **Driving** expose the shared simulation rules. The physical bench has the same controls further down its setup scroll panel, with an additional **test wear multiplier** (slider 0–100x and 1x/10x/100x buttons). Bench changes affect only the isolated run, retain keyboard/slider ownership and reset to the chosen starting setup. Changing vehicles retains the selected driving rules. Garage-to-bench entry copies your saved preferences.

| Preset | Wear | Thermals | Clutch / stall / traction / rev-match assists |
| --- | --- | --- | --- |
| Arcade | None | Always optimal | Enabled |
| Balanced | Partial: 25% | Simulated | Enabled |
| Simulation | Full: 100% | Simulated | Disabled |

These presets keep your launch/shifting modes, throttle preference, sound level and independent AI difficulty. Individual overrides show **Custom**. Wear, thermal, clutch, stall, traction and rev-match options can be mixed independently. The game reset retains these preferences. ECU/gearbox downshift protection remains installed hardware behavior for every preset; no bench-only protection override is added.

**None** prevents new simulated condition loss and overrev/anti-lag damage; existing worn/broken parts remain worn/broken. **Partial** multiplies normal condition loss and abuse damage by **0.25**, with failures still possible. **Full** uses the normal authored rate. The bench multiplier compounds with that rate, so Partial at 10x gives 2.5x normal wear. It scales health/damage, not elapsed running time, recorded distance or friction heating. A 0x multiplier suppresses new wear. No-wear cars can still stall or suffer heat fade unless those separate assists are enabled.

**Always optimal thermals** starts the engine warm, holds current component temperatures at their nominal test targets, removes heat-related clutch fade/exhaust heat accumulation, and treats the charge as ideally cooled to ambient. Ideal charge temperature affects actual air density/torque, not just the displayed temperature. Cold carburetor roughness is therefore suppressed, while spool delay, limiter behavior, existing wear and mechanical overrev remain unless their separate rules change. This covers the currently implemented thermal model; tire temperatures and the complete cooling circuit still follow at later Slice 2 gates.

## Destructive tests and diagnostics

Choose compatible **Manual** gearbox hardware and **Manual clutch** driving. Hold Left Shift, use Q to select a lower gear, and release the clutch. Excessive road-coupled RPM can back-drive the engine beyond its safe speed even with gas released or the combustion limiter active. Protection is installed hardware behavior, identical in race and bench: ordinary manual gearboxes have no downshift interlock; DCT/automatic and the optional actuated/interlocked manual gearbox reject predicted overrev. There is no bench-only override. This distinction follows controller-request protection such as [Haltech's transmission settings](https://support.haltech.com/portal/en/kb/articles/transmission-control); the game calibrations remain fictional.

**Manual money-shift / engine failure test** installs an isolated manual test gearbox, selects manual controls and generates a repeatable launch, upshift, high-speed downshift and clutch release. It uses ordinary control inputs; it does not assign RPM or damage. Failure is calculated from overspeed exposure and component strength. A failed engine cannot be repaired by restarting; neutral, momentum and braking still work. In the bench, Reset restores the configured starting condition. In races, simulated component condition and failures persist when finishing, returning to Garage, pausing or quitting; replacing a part does not renew its retained original.

The setup scroll panel offers **Previous part / Next part**, **Starting health** and **Starting temperature** for every fitted component. Assign these before drive engagement or pause first. The assigned test reset keeps pedal ownership and never changes player parts. Live health, temperature and failure cause appear alongside authored torque/RPM/heat limits. **Repair wear / heat (isolated)** below the drive controls clears all copied wear/failures/thermal histories, preserving road momentum and input source. It also updates the bench's next-reset baseline. Player repairs still use compatible replacement in Garage.

Shared provisional stress supports clutch friction energy/heat, heat-related clutch capacity fade, gearbox/converter heat, torque-dependent gearbox/output-shaft/differential failure, engine-internal overspeed/strength/thermal damage and brake/cooling thermal exposure. Shaft strength is compared against gearbox-output torque before final drive. Assigned conditions on other categories remain inspectable; the complete compound/contact and component-specific condition effects are later 2J–2K work. Parameters are authored on `PartDefinition`; no bench duplicates the stress formulas. Independent wheel speeds/contact, a full cooling circuit, detailed axle torsion and long-term durability calibration are not implemented by this extension.

**Show detailed engine / clutch / slip diagnostics** replaces the graph with live crank and estimated gearbox-input RPM, signed synchronization mismatch, clutch capacity/coupling, converter slip/lock, CVT ratio, estimated wheel slip, combustion, limiter, safe RPM, damage, temperature, exhaust heat and turbo spool. Disable it to compare graph overlays. Graph channels include HP, RPM, speed, shaft torque, estimated wheel slip, signed clutch mismatch magnitude, damage and boost. CSV retains the signed mismatch as well as combustion fraction and active limiter settings.

## Repeat and comparison

The full-pull scenario records initial idle, staged revs, drive engagement, two shift commands, throttle release, neutral coasting and braking over 1,920 input ticks. Neutral-flywheel and seeded-driver scenarios use the same fixed-tick input provider. A seed changes the authored input schedule, never adds hidden torque. Completed runs retain up to six overlays.

**End run** captures a manual run. **Replay last run** restores its copied specification, tune, driver settings, part IDs/conditions, fuel snapshot and seed, then applies the captured input at every 120 Hz tick. Configuration changes never rewrite that capture. Replay is guaranteed/tested on the same runtime/model; importing an unsupported model fails explicitly.

**Export CSV + replay JSON** writes beneath `DrivingBenchExports` beside the running player (or the project's Game folder in Editor). CSV contains every chronological sample, including tick zero and coast/brake tails. It is not sorted by RPM. JSON contains the complete starting snapshot, simulation rules/multiplier and serialized input structs. **Import latest exported replay** starts the most recent capture. Graphs use continuous overlapping thick line segments on an opaque background. Screen reduction preserves chronological bucket endpoints and XY extrema, including brief shift cuts, torque dips and RPM reversals; exported samples remain complete. Negative shaft torque has a signed scale. The sound bench also uses continuous HP/engine/shaft torque traces distinguished by color and thickness, with no deliberate dashed gaps.

**Recording is optional and OFF on entry.** Physics and wear continue without a new input timeline, recorded trace history or automatic disk writes; only the latest diagnostics are retained. There is no ten-minute driving limit. Enabling **Record input/trace** begins at the current physical state rather than restarting. Repeatable scenario/replay buttons explicitly enable recording for comparisons.

While recording, each ten-minute segment exports complete CSV/replay JSON into a `continuous-*` subfolder, then continues with a checkpoint of all dynamics and part conditions. Pedals, ownership, momentum, damage and absolute time survive. Each segment replays independently; **End run** and **Export** save the current tail. Six recorded segments/runs remain in graph history; older completed segments remain on disk. Explicit scenario/replay playback pauses at its captured end. Reset after a continuation/import returns to tick zero and the configured neutral starting state.

Trace fields include time/segment, RPM/gear, speed/distance, pedals, engine Nm/HP, shaft Nm, boost/spool, estimated slip, clutch/converter slip, CVT ratio, flywheel energy, acceleration, warmup/exhaust heat, shifts, two-step, overspeed damage/stall/failure, clutch temperature/health, gearbox temperature, driveshaft health and driveline failure. Engine power and transmitted torque are separate: an open-clutch shift can retain combustion while shaft torque drops. Live sliders show requested pedals, while coupling opening and staging brake hold appear separately in diagnostics.

Physics replay repeats the captured tick timeline. Live DSP callback timing is not locked to those ticks; use the sound bench's deterministic WAV exports for exact PCM comparisons.

## Scope boundary

The initial shared-stress checkpoint used model v3 and demand-estimated slip. It has been superseded: 2H now uses independent wheel/contact rotation, 2I fuel and 2J tire heat/life; local engine and wet-clutch service use v14. Those earlier estimates are historical, not current simulation behavior. Fresh captures use the current `MechanicsSchema.Model`; older model recordings are rejected. Owned fuel/wear/fluids and the final 2K–2L recovery/tooling gates follow [plan reconciliation](SLICE_2_PLAN_RECONCILIATION.md). Save migration retains original files and IDs and does not respawn deliberately removed hardware.



Parts/equipment foundation (2026-10-08): see [part effects and equipment](PART_EFFECTS_AND_EQUIPMENT.md). Category-specific ratings and descriptions, engine/coolant/radiator heat transfer, delivery limits, left/right open/LSD/welded/spool allocation, removable interior/comfort equipment and separate limiter/two-step/shift-light purchases now use the shared race/bench resolver. Hardware gates editable equipment settings; garage drafts still require Apply. Independent wheel/contact and tire life are integrated in 2H–2J. Final acceptance evidence follows the 2K–2L checkpoints. Model v5 was an earlier checkpoint.

## Manual selector hardware

Select **Sequential manual gearbox** under isolated Gearbox test parts to use the faster selector in the same road simulation. **Shift myself** is the driving control/assist choice; it does not replace the gearbox. Sequential manual selection defaults to 45% of an equivalent H-pattern gearbox's shift duration. Full-manual clutch use, wear and unsafe downshift behavior remain active. See [implementation and verification](../reports/manual-gearboxes/CHANGES.md).


## Condition, fluids and service extension

Service/fluids originally arrived in model v13; individual cylinders/clutch fluids extended it in v14. Use the current model shown in the bench and record fresh scenarios after a model change. Detailed diagnostics expose isolated oil/coolant quantity, oil condition/temperature, pressures, circulation and losses; graph tabs include oil quantity, coolant quantity, oil temperature, oil pressure, oil condition and valve float. New CSV fields also carry combustion efficiency and individual loss rates. Per-part deposits, leaks and clearance inputs appear where relevant and may be assigned before engagement or while paused. Test top-ups, cooldown and service actions retain driving ownership; wear reset leaves fluid quantities unchanged. Race and bench advance these systems through the same car solver. See [service documentation](WEAR_FLUIDS_AND_SERVICE.md).
