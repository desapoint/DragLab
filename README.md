# DragLab — Unity drag-racing first slice

Catalog expansion: **128 additional parts**, including compact/street/large turbo kits, fixed/adjustable boost and pulley setups, and more clutch, cooling, flywheel, tire, brake and gearbox alternatives. **Parts** identifies each part's tuning access. Turbo size is hardware; boost, pulley and final drive are adjustable only when the installed part supports them. See [parts and tuning details](reports/part-hardware/CHANGES.md). Focused validation: `tools/Invoke-Unity.ps1 -Action PartHardwareVerify`.

Fuel usage is selectable under **Settings → Simulation difficulty** and **Driving**, and in the physical bench: None (0%), Reduced (25%), Realistic (100%). Arcade/Balanced/Simulation select those respective rates. Fuel remaining in the tank still contributes to weight. The preference survives reset/reload and is captured for replay. See [fuel usage checks](reports/fuel-usage/CHANGES.md).

A mechanics-first foundation for a commercial 2D drag-racing game. The project is in `Game/`, prepared for the locally installed Unity **6000.6.4f1**. Eight fictional cars are available. Four original layered placeholder bodies cover hatchback, coupe, muscle car and pickup; the new three-cylinder hatch, AWD turbo sedan, inline-six GT and supercharged V8 reuse those bodies during this mechanics pass. No asset-store or third-party runtime packages are required.

The scope follows `docs/objective.md`, especially its first playable milestone (section 58). See `docs/SLICE_STATUS.md` for what is implemented, verified and deferred.

## Collection, assemblies and configurations

**Garage → Owned car collection / dealership** buys and selects exact car instances, including repeated copies. **Engine assemblies** stores/transfers complete or partial engines with actual condition and liquids. **Save / load engine configurations and tunes** saves named hardware or tuning blueprints, previews inventory reuse and missing purchases, then explicitly applies the setup. Selling its parts does not erase the blueprint. See [assembly/setup controls](docs/ASSEMBLIES_AND_SETUPS.md). Prices remain provisional. 3D adds paid fuel/services and confirmed chassis salvage retaining the exact engine tree; see [paid operations and recovery](docs/PAID_OPERATIONS_AND_RECOVERY.md).

## Play the Windows slice

Launch `Game/Builds/Windows/DragLab.exe`. Keep the adjacent `DragLab_Data`, `MonoBleedingEdge` and DLLs beside it. The build opens in a resizable window. Choose **Career → First pass** to try the complete loop, or press **P** in the Garage for a quarter-mile practice pass with no entry fee or payout. Practice still saves timing records. **Race setup** offers configurable free races. See `docs/SLICE1_PLAYTEST.md` for a short test route. Close the window or use Alt+F4 to quit.

## Start in Unity

**Valuation previews (3C):** Garage → **Value car / engine / chassis / loose parts** compares buyer offers for the exact owned assets. Show every price adjustment, quote a chassis while keeping its engine, or exclude individual fitted parts. Collection, Parts inventory and stored-engine rows also link to quotes. Quotes now support confirmed sale/salvage in 3D. Garage/Collection also opens timed recovery work for low cash and empty garages. Prices remain provisional. Edit the Resources `ValuationPolicy` asset in Unity to tune buyer/model/quality/history factors. See [valuation and quotes](docs/VALUATION_AND_QUOTES.md).

1. In Unity Hub, choose **Add → Add project from disk**, and select this repository's `Game` directory.
2. Open with the installed Unity 6 editor and allow asset import to finish.
3. Choose **DragLab → Prepare First Slice**. This creates vehicle/event assets and `Assets/DragLab/Scenes/FirstSlice.unity`. Re-running preserves existing definitions and scenes.
4. Open that scene and press **Play**. Garage, Parts, Race setup, Career, Driving and Settings are temporary systems UI.
5. Open **DragLab → Vehicle Workbench** to view torque/power curves, change test tunes, run simulated quarter miles and export vehicle JSON or telemetry CSV. **Compare current build** tests a captured baseline, five launch RPMs and seven final drives with the same assists; export the comparison CSV to inspect ET/trap changes.

If a scene already exists, the preparation tool preserves it. After adding new vehicles, update the `Vehicles` list on the scene's `DragLab First Slice` object, or clear that list to let the runtime load all definitions.

## Engine and sound test scene

For road dynamics, choose **DragLab → Open Physical Driving Test Scene**, or use **Physical driving bench / this installed build** in Garage. It uses the shared car simulation without a race cycle, records all 120 Hz ticks and offers manual controls, test parts, seeded scenarios, previous-run overlays and replay JSON/CSV. See [physical bench controls](docs/PHYSICAL_DRIVING_BENCH.md).

Choose **DragLab → Open Engine Sound Test Scene**, then Play. In the Windows game, use **Engine / sound test bench** in Garage, or run `tools/Open-EngineBench.ps1` for direct access. It provides endless manual neutral/rev/gear/load/roller-slip tests, fixed RPM, every engine/induction/acoustic setting, configurable silencer/resonator/catalytic-converter chains, eleven repeatable scenarios, captured A/B configurations and exact-repeat WAV exports. **Use keyboard / Use sliders** selects driving input ownership. W/Space claims driving even after numeric editing. Keyboard-controlled Drive fields show read-only feedback; engine, turbo, exhaust, gearbox and Mix settings remain editable without changing input ownership or cancelling held gas. Powered-on protected testing stays above idle, and manual bench shifts interrupt shaft torque while retaining combustion and held gas. A live dyno overlays six previous full runs, including shifts and throttle release, with time/RPM axes and CSV exports. It uses copies and never accesses player saves. See [engine bench controls and comparison workflow](docs/ENGINE_TEST_BENCH.md).

## Manual gearbox hardware

**Parts → Gearbox** offers an H-pattern manual and a **sequential manual gearbox** for each car. Sequential selection defaults to 45% of an equivalent H-pattern handover duration, multiplied by the same part's shift-duration factor. It retains the single-clutch manual simulation and authored gear ratios. Full-manual driving still requires the clutch; automatic flatshift cuts are not included in this variant.

**Shift myself** is a driving assist/control choice and does not change gearbox hardware. In the engine bench, select **Gearbox / wheels → type: Manual → Manual gearbox selector hardware: Sequential**; `sequentialShiftScale` controls the duration factor. Physical driving bench test parts offer the same installed hardware. Stats, race HUD and bench diagnostics identify the layout and show effective shift time.

## Units and visible setup

**Settings** shares speed (km/h, mph, m/s), pressure (bar, psi, kPa) and torque (Nm, lb-ft) preferences across races, stats, shops and both test benches. Torque edits convert back to Nm; exported engineering data stays in SI.

Tire outer radius visibly scales the wheel layers and raises the car. Part/tune previews show the proposed appearance; **Apply changes** commits a tune and leaving discards it. Brakes retain independent size and animation. The current wheel sprite combines rim and tire; the radius setting includes rubber. Paint previews and removal of visible wheel/brake/body hardware also update the car. See [verification and current artwork limits](reports/units-wheel-visuals/CHANGES.md).

## Controls

| Action | Desktop | Prototype touch |
| --- | --- | --- |
| Throttle | Hold W or Space during staging/driving; optional automatic throttle | Hold middle/right of throttle; slide left for partial |
| Launch / engage first | E after green (Enter also works); optional automatic launch | Combined shift-up / N → 1 button |
| Shift up/down | E / Q or arrow up/down | Shift buttons |
| Clutch | Hold Left Shift to disengage; configurable release rate | Hold clutch independently of throttle; slide left for partial |
| Brake | Hold S; automatic shutdown braking enabled by default | Independent analog brake pedal |
| Pause | Escape | Pause button |
| Practice from Garage | P | Practice button |
| Retry finished race | R | Run again button |
| Return to Garage | G when finished or paused | Garage button |

Staging is **neutral (N) with the brakes held automatically**. W/Space revs the engine without driving the wheels; no clutch press is needed to rev in neutral. Press **E / shift up** after green to engage first gear and release the staging brake hold; later E presses upshift. CVT selects Drive instead. Assisted/manual launch waits in neutral until this input; an early attempt retains neutral/brakes and records a red light. Automatic launch keeps its scheduled reaction delay. Enter remains an alternate launch key. The same combined button restarts a stalled engine (clutch in for manual hardware).

Driving offers **Drive automatically**, **Shift myself** and **Full manual** presets, with individual assists still adjustable. They preserve your audio, clutch release rate and shutdown-braking preferences; AI difficulty stays separate.

Hold **W or Space** to raise RPM while the lights count down, then keep holding while driving and changing gears. **E** handles assisted/manual launch after green, then upshifts. Ordinary staging gas overrides the tuned-RPM hold; enabled two-step limits revs at launch RPM using its hard/soft cut settings. Factory or upgraded launch preload can build turbo charge while neutral/brake hold keeps the wheels still. For a manual run choose **Shift myself**, press E at green and again near the displayed shift point. Manual hardware in Full manual additionally requires Left Shift for clutch control; DCT/automatic hardware uses its own clutch/converter behavior, and CVT varies ratio continuously after selecting Drive. Shifting preserves held gas. Main limiter settings determine hard bounce, smooth limiting or unrestricted revs; unprotected overrev can fail an engine for that pass. Clutch in + E (or Enter) restarts a stalled manual engine. See `docs/POWERTRAIN.md` for factory defaults, upgrades and limits.

Held keyboard pedals are read immediately when entering/resuming a race. Touch pedals capture independent fingers, so throttle, clutch and shift controls can be used together. Middle/right is full pedal; slide toward the left edge for partial input. Mouse uses the same hit zones. Physical Android testing is still pending.

Each car enters the shutdown lane as soon as it crosses the finish. Braking slows the wheels and car while the recorded ET, splits and trap speed stay fixed. Disable **Automatic braking in shutdown lane after finish** in Driving to use S or the brake pedal yourself. Escape also pauses shutdown motion. Garage and Run again remain available without waiting for a stop.

## Included foundation

- A fixed 120 Hz simulation shared by player, AI and development benchmarks.
- Interpolated engine torque, derived horsepower, spec-driven turbo lag/boost and supercharger drive losses, ratios, final drive, shift interruption, aerodynamic drag, rolling resistance and drivetrain-dependent load transfer.
- A lightweight demand-based traction/slip model, optional traction control, launch RPM, tire pressure and manual/automatic driving.
- Red-light disqualification, separate reaction and elapsed times, split times and trap speed.
- Grip-limited braking during races and automatic/manual shutdown after finish; vehicle brake deceleration is editable in the definition Inspector or JSON (zero selects the modeled 8 m/s² default).
- Four local career events, free races over 1/8, 1/4 and 1/2 mile, three AI reaction presets.
- Individual compatible parts, OEM recipes, buy/install/remove/reuse/sell/donor/downgrade controls and provisional resale. Failed engine-critical parts block race entry; brake and drive failures instead affect their real subsystem. Pickup AWD offers configurable bias/center/coupling and hardware-dependent fallback, with free-shaft power/rotation for open-center disconnections. See [component failures and AWD](docs/COMPONENT_FAILURES_AWD.md).
- F–S performance classes estimated from the same assisted quarter-mile simulation. Thresholds are provisional balance data.
- Independent body, shading, glass, trim, arch backs, wheels, rotors and fixed calipers, with placement manifests.
- Unity editor preparation, validation, dyno, benchmark and JSON/CSV tools.
- Three tune slots per car, per-car timing records and persistent driving settings.
- Saved last-pass tune/assist snapshots and same-distance ET/trap comparisons on results; practice allows experiments without changing cash or reputation.
- Bounded run telemetry and practical post-race feedback; export the actual pass from the result screen.
- Stock-versus-tuned dyno curves and a 128-combination power/tire/traction balance CSV export in the Vehicle Workbench.
- Thirteen-run launch/gearing comparisons with a captured baseline and selectable traction control.
- Visible starting tree and finish line, consistent track scrolling, focus-loss pause and input clearing.
- Engine-side rotational inertia, clutch capacity/engagement, adjustable digital clutch release and bogging/RPM-flare feedback.
- Calculated flywheel mass/radius inertia, neutral RPM decay, stored energy/angular momentum and finite clutch/gear synchronization; editable in Garage and the bench, with repeatable coast-down/dynamic-shift tests and a separate RPM trace. See [flywheel model and testing](docs/FLYWHEEL.md).
- Separate keyboard/mouse/touch input adapter with fixed-tick edge consumption and independent analog touch pedals.
- Procedural four-stroke engine audio responds to cylinder count, displacement, layout, RPM, load, exhaust upgrades and engine stages. Turbo spool, supercharger whine and four pressure-release valve sounds are driven by installed hardware. Volume is saved; audio stops during pause, garage and results.
- A single saved **Master volume** (75% default) controls racing, the engine bench and dyno concert. Open **Settings** in the garage/bench or **Audio settings** from race pause. Cars, presets and bench configurations cannot reset the master.
- Garage **Settings → Reset game... → Reset progression** starts over with $1,200 and one stock starter car. It clears purchased cars, parts inventory, tunes/presets, paint, records, career wins, reputation, business upgrades and accrued idle income. Driving preferences, AI difficulty and volume settings stay as you set them. Cancel leaves the save untouched; reset is unavailable during a race.
- Single/twin turbo, single/twin supercharger and turbo-plus-supercharger kits change live torque/HP, dyno curves and race times. Compressor flow, charge heating, intercooling, boost control, size and pulley tuning introduce tradeoffs. Audio reads physical boost/release states; continuous engine/turbo white noise is removed.
- Factory or upgraded two-step/launch preload and shift/overrun anti-lag; independent hard/soft/absent rev limiters, fuel/ignition cuts, heat/wear limits and pass-local overrev failure.
- Wheel diameter, width and assembly mass change effective gearing, rolling inertia and manual-clutch bog/stall risk. Clutch in + Enter restarts a stalled engine.
- Manual, DCT, torque-converter automatic and CVT hardware behavior, with gearbox-stage transmission swaps separate from driving assists.
- Injection/carburetor, cam, mixture and temperature-dependent idle/hesitation, intake gulp, overrun/pops and quiet mechanical clatter. Darker exhaust filters and lower bank/crank pulses persist through loaded pulls, especially on carbureted/V8 engines.

These are prototype systems. Independent axle-spin/tire-force integration, tire temperature, clutch heat/wear, persistent engine repairs, measured compressor maps, production audio, controller input, production UI, cloud saves and storefront integrations remain future work. Specifications are fictional modeled values, not verified factory data. This build is for testing mechanics; full release scope remains in the objective.

Independent wheelspin through open-clutch gear changes and physical grip recovery are explicitly scoped to [Slice 2](docs/GAME_PLAN.md#required-wheelspin-and-driveline-integration-not-implemented-in-slice-1), with drivetrain coupling, traction-control and telemetry acceptance checks. Current slip remains an estimate from torque demand.

## Files and tools

- `docs/GAME_PLAN.md`: intended game, slice order and acceptance criteria.
- `docs/IMPLEMENTATION_PHASES.md`: ordered Slice 2A–2L and Slice 3A–3H increments, dependencies, runnable checkpoints and exit gates.
- `docs/DRIVING_DEPTH_AUDIT.md`: implemented mechanics/tools versus gaps; Slice 2 fuel/consumption/refill/mass requirements and Slice 3 economy boundary.
- `docs/ARCHITECTURE.md`: project structure, ownership and development rules.
- `docs/ART_GUIDE.md`: sprite contract, pivots, rotation and art replacement.
- `docs/ANDROID_AND_RELEASE.md`: Android preparation and commercial-release checklist.
- `docs/ENGINE_AUDIO.md`: sound hardware, garage controls, developer tuning and WAV auditions.
- `docs/ENGINE_TEST_BENCH.md`: standalone engine scene, manual controls, component exhaust acoustics, deterministic scenarios and A/B exports.
- `docs/INDUCTION.md`: physical boost/lag, compressor topologies, authoring and measured comparison reports.
- `docs/POWERTRAIN.md`: launch controls, limiters/failure, wheel inertia, gearbox hardware and older-engine fueling/sound.
- `art/previews/sprite-kit-overview.png`: all four assembled cars and isolated mechanics.
- `art/vehicles/`: full sprite source packages, manifests, provenance and review evidence.
- `art/revisions/v1/`: preserved first body revision and the approved mechanical artwork.
- `tools/generate_placeholder_sprites.py`: deterministic sprite generation, requires Pillow 10+.
- `tools/Invoke-Unity.ps1`: repeatable import, validation and build commands.
- `reports/`: actual Unity logs and test evidence.

Saves live in Unity's `Application.persistentDataPath` as `profile-v1.json`, with a previous-save backup. The workbench's experimental tunes do not change the player's saved car. Do not change shipped vehicle IDs once save files exist.

Verification commands: `tools/Invoke-Unity.ps1 -Action Verify` checks the data/simulation/sprite foundation; `-Action SceneCheck` checks scene initialization, a complete race, rewards and isolated save/recovery fixtures. `-Action BuildWindows` checks mechanics and builds the player. `tools/Test-WindowsSlice.ps1 -ShowPlayer` runs the actual Windows Update/OnGUI loop with simulated driver inputs and screenshot captures; the window needs to be shown for framebuffer capture. Add `-KeyboardOnly` for a separate Windows OS keyboard-event test through Unity's actual input polling, including held gas through sequential and clutch shifts. This opt-in development test focuses its own player window and never sends key-downs to another app. Each test uses its own fixture save. Neither test covers physical hardware rollover, human driving feel or Android device input. Avoid reusing a fixture name, as an existing test profile will not represent a new player.

`tools/Test-WindowsSlice.ps1 -ShiftHoldOnly -ShowPlayer` runs a focused bench regression with real host W/Space events, E/Q and mouse shift buttons, repeated held-key downs, all four transmissions and frame telemetry. It does not test remote-desktop key forwarding. When testing through TeamViewer, **Use sliders** plus mouse shift buttons avoids depending on forwarded held keyboard pedals; see the bench guide.

Master volume now scales the final procedural audio output, rather than relying on listener-only attenuation. Settings shows a live dBFS RMS meter; slider drags save on release. Mix has adjustable `idleBoostDb` (+6 dB default), fading as RPM/throttle rises and bypassed during two-step. Engine gain also controls two-step/overrun/hesitation color; turbo/blower/valve gains retain their own layers. Mix can be edited while keyboard driving stays active. `tools/Test-WindowsSlice.ps1 -AudioLevelsOnly -ShowPlayer` tests real master, engine and turbo slider drags against actual listener PCM. Four paired idle auditions are in `reports/idle-level-comparison/`.


Parts/equipment foundation (2026-10-08): see [part effects and equipment](docs/PART_EFFECTS_AND_EQUIPMENT.md). Category-specific ratings and descriptions, engine/coolant/radiator heat transfer, delivery limits, left/right open/LSD/welded/spool allocation, removable interior/comfort equipment and separate limiter/two-step/shift-light purchases now use the shared race/bench resolver. Hardware gates editable equipment settings; garage drafts still require Apply. Independent wheel/contact/slip-work tire life and final calibration remain in 2G–2L. Old physical captures must be recorded again for model v5.


## Local version control

The local Git repository versions the Unity project source/assets/metadata, packages/settings, source art, tools, docs and concise review evidence. Builds, caches and raw captures remain local. Every completed slice, step or requested change requires a commit under [AGENTS.md](AGENTS.md). See [version-control scope and workflow](docs/VERSION_CONTROL.md).
