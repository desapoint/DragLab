# Prototype implementation status

**2A–2L and 3A–3H developer acceptance are complete.** See [phase plan](IMPLEMENTATION_PHASES.md), [full Slice 3 audit](../reports/SLICE_3_AUDIT.md), [career](CAREER_PROGRESSION.md), [garage business](GARAGE_BUSINESS.md) and [economy balance](ECONOMY_BALANCE.md).

**Slice 4: Settings checkpoint ready for user review (2026-10-10).** The user explicitly requested the complete existing menu Settings/Driving interface together. Five populated categories now use one draft with Apply/confirmation, Revert, Cancel-to-previous-menu and independent reset/help dialogs. Slanted brushed tabs, themed choices/toggles/sliders/numeric fields and corner highlights match the racing shell. Requested trim fades and one-shot shape-aware shine are included. [Checks and review checklist](../reports/phases/4b/08-SETTINGS.md). Technical completion is separate from visual acceptance; stop for validation before another interface. No inspection drawer, controller/remapping, Android or further screen migration is included.

Source of scope: [objective.md](objective.md), section 58. The eight-car roster includes the requested pickup, three-cylinder hatch, AWD turbo sedan, inline-six GT and supercharged V8. The four new mechanical configurations reuse existing layered placeholder bodies. Mechanics and editable data take priority over finished graphics or menus.

Native UI direction is recorded in [NATIVE_UI_DESIGN.md](NATIVE_UI_DESIGN.md). The shaded grey/red shell, top navigation/resources and Settings tabs are now implemented; draft Apply/confirmation and help/reset dialogs are implemented for menu Settings. Current legacy settings remain available through F8 and retain their existing behavior.

Additional requirements: recognizable icons, info dialogs for secondary details, four primary part stats (HP/torque/weight/durability) and actual change effects; common responsive scaling and anchors for resolution/window-mode changes, including top full-width navigation and right/full-bar-height Settings. The shell has native resize/rendering checks; later widgets and real controller/mobile acceptance remain pending.

Latest selections: filled icons; clear 1080p/sharp 4K presentation with current windowed defaults and mostly stable logical element sizes; Garage/Build/Tuning/Race/Business/Data navigation; future current-car slide-in inspection overlay left of Settings. New bench navigation/presentation is deferred while legacy tools remain. Both design replies in the user-opened reference chat have now been read; [reference candidates](NATIVE_UI_REFERENCE_NOTES.md) are not blanket approval of its layouts or additional mechanics. No runtime change in this note.

Current Slice 3 progress: eight-car collection, retained engine assemblies, saved configurations, immutable paid quotes/salvage, 21 career events, exact reserved garage jobs and captured offline accrual are implemented. Full-loop funded scenarios preserve actual fuel/fluids/wear and viable recovery. Prices are calibrated prototype values, with human pacing review still required. 3H passed the integrated interruption/migration/save gate and complete domain/Windows reruns; see [ordered phases](IMPLEMENTATION_PHASES.md).

## Implemented milestone

| Objective requirement | Current implementation |
| --- | --- |
| Cars and one strip | Eight fictional definitions; one side-on strip; separately animated wheel/rotor sprites and fixed calipers |
| Quarter-mile race | 402.336 m; eighth and half mile also selectable in free races |
| Torque and gears | Interpolated Nm curve; derived HP; separate core/calculated flywheel inertia, stored energy and neutral decay; bounded clutch synchronization; individual ratios/final drive; timed shaft disconnection |
| Forced induction | Single/twin turbo, single/twin supercharger and turbo-plus-supercharger; transient spool/manifold charge, flow limits, heat/intercooling, blower drive losses and mixed-system bypass affect actual engine output |
| Launch controls / protection | Factory or upgraded two-step/preload/anti-lag; separate hard/soft/absent main limiter; fuel/ignition cuts; duration/heat/wear constraints; shared persistent overspeed/component failure |
| Wheels / gearbox hardware | Diameter/width/mass change gearing, static/rotational load and manual-clutch stall risk; Manual/DCT/converter Automatic/CVT hardware; clutch-in restart |
| Older-engine behavior | Injection/carb, cam/mixture/idle control, warmup and pump hesitation affect physical state and deeper exhaust/gulp/burble/pop/clatter audio |
| Traction | Independent four-wheel/contact rotation, mass/load transfer, differential allocation, compound/temperature/wear grip, slip through shifts and traction control |
| Failure effects / AWD | Component-specific brake/engine/common-drive/branch consequences; independent per-wheel contacts and axle torque paths; pickup AWD with fixed/open/locked/on-demand/active modes and hardware-dependent surviving-axle drive; [details](COMPONENT_FAILURES_AWD.md) |
| Manual/automatic shifting | Automatic, sequential and manual clutch; hardware/controller-based downshift protection shared by race and bench; unprotected manuals can mechanically overrev |
| Manual/automatic launch | Automatic/assisted staging holds tuned RPM until pedal override; W/Space raises revs in any mode; E / shift-up engages first (CVT Drive) for assisted/manual; Enter remains an alternate; manual clutch remains independent of transmission selection |
| Staging | Explicit neutral (N), automatic brake hold, free revs without clutch input, zero wheel torque/creep; valid launch engages first and releases staging brakes |
| AI and timing | Same physics for both cars; reaction difficulty presets; red lights; reaction plus ET; splits and trap speed |
| Braking and shutdown | Grip-limited analog brake; immediate braking after each car finishes; automatic or manual shutdown; finish times and rewards remain fixed |
| Garage and upgrades | Eight-car OEM recipes and fitted component definitions; compatible buy/install/remove/reuse/sell/donor controls; direct upgrades/downgrades without rank prerequisites; installed output replaces legacy rank authority; paint and adjustment presets |
| Asset valuations / quotes | Separate actual loose pack/element, engine, chassis and car offers; authored buyer/model/quality/history factors, shared condition/individual faults and required completeness; retained/missing assets excluded; immutable expiring quotes reject changed/reserved states; confirmed quote-backed disposal, operating receipts and policy/matrix tooling |
| Car inspection / part previews | Saved and proposed shared-model dyno curves; verbose numeric stats with min/max bars, right-aligned current → proposed, green improvements/red drawbacks/amber tradeoffs; condition-aware shop/inventory/donor/removal previews and expandable setup advice |
| Applied tuning / display units | Isolated draft adjustments, presets and paint; explicit atomic Apply/Revert and discard on exit/race entry; shared km/h/mph/m/s and bar/psi/kPa preferences, outer tire radius in inches and boost bar/psi |
| Saving | Profile schema 5 with valid empty garages, operating receipts/recovery jobs, unique vehicle/chassis/engine/part IDs, stored assembly trees and named configuration library, exclusive parents, retained condition, durable legacy migration/original backup and one-time OEM shaft addition, candidate transactions, temp-file replacement and backup recovery |
| Practice and comparison | No-fee/no-payout quarter mile against the selected car's stock build; saves valid records and last-pass tune/assist snapshots; same-distance ET/trap comparison |
| Simulation difficulty | Arcade/Balanced/Simulation, mixed thermal/clutch/stall/traction/rev-match rules, None/Partial 25%/Full wear and independent AI/shift modes; persisted through reset; isolated bench wear multiplier |
| Driving presets | Automatic, sequential and full manual; individual assists and AI difficulty remain independently adjustable |
| Rev limiter | Hysteresis-based fuel cut/recovery in staging and driving, with audio feedback |
| Engine audio | Four-stroke pulse synthesis using cylinders, displacement, layout, RPM/load, exhaust stages and induction; turbo spool, supercharger whine and recirculated/hiss/chirp/flutter valves |
| Shared audio settings | One saved master gain for racing, bench and concert; 75% default; Settings page in garage/bench and race pause; independent preferences, persisted mute and no preset/car overrides |
| Reset game | Garage Settings confirmation starts a fresh stock starter garage, clearing progression, inventory, cars, tunes/paint/presets, records and accrued idle income while preserving driving preferences, AI difficulty and shared volume; primary and recovery backup both receive the fresh baseline |
| Sound test bench | Separate EngineTest scene, manual/fixed RPM/load/gear/roller controls, persistent keyboard/slider ownership with live feedback, all engine/acoustic parameters, numeric exhaust chains, eleven repeatable scenarios, captured A/B JSON/WAV exports and full live dyno traces with six previous-run overlays and CSV |
| Configuration during keyboard driving | Hardware/engine/turbo/exhaust/gearbox/Mix dropdowns, sliders and numeric fields remain editable without changing driving source or held pedals. Only keyboard-controlled Drive inputs are feedback-only; independent engine/test options and shared volume remain editable |
| Physical driving bench | Shared car/stress simulation without race cycle; optional recording OFF on entry, continuous unrecorded driving, checkpointed recorded segments, per-part starting health/heat and isolated repair, detailed diagnostics, ordinary-input failure scenarios and deterministic replay/CSV/JSON |
| Fuel quantity / consumption / refuelling | Shared fuel flow/mass, saved quantities and empty-tank behavior; None/Reduced 25%/Realistic difficulty; quantity-capped paid refuelling in 3D, free isolated benches |
| Persistent component wear / replacement | Shared clutch, gearbox, shaft, differential, engine and per-element wear/failures, thermals/fluids and captured owned-run recovery; compatible repair/replacement retains old identities and faults |
| Modular mechanical parts / inventory | Eight complete OEM recipes and category-specific modular hardware, direct buy/install/remove/reuse/downgrade/sell/donor operations, limited service, individual components and paid economy |
| Tire compound durability / race breakdowns | Independent per-wheel slip work, compound grip/life/temperature, persistent tire failure and cause-specific engine/driveline/brake breakdowns |
| Multiple owned copies / salvage / assembly values | Actual dealer repeat purchases and exact selection; retained complete/partial engines; quote-backed incomplete chassis/car/engine disposal and non-additive assembly valuation |
| Full fuel / money / career / idle loop | Paid fuel/services/repair, 21 authored F–S events, captured rewards/entries, garage expansion/jobs and funded recovery/reinvestment routes; final 3H developer acceptance passed |

The original Slice 1 career/cash/idle scaffolding and demand-slip/fuel-mass gaps are superseded by completed 2A–2L and 3A–3G. Current acceptance uses independent wheel/contact rotation, explicit translating/rotating/fuel/fluid/component mass, modular ownership, paid operating costs and captured progression/jobs. See the dated [Slice 3 contract](SLICE_3_ACCEPTANCE.md) and [heat/warm-up contract](HEAT_AND_RACE_TURNAROUND.md).

## Added development feedback

The actual race records bounded 10 Hz telemetry for speed, RPM, gear, slip, throttle, clutch, brake pressure, acceleration, clutch slip RPM and transmitted shaft torque, plus total/stage boost, turbo spool, engine torque/HP, blower drive torque, charge temperature and airflow. Sampling ends at the player's finish, so shutdown cannot change the recorded pass. The result screen exports CSV and flags heavy launch wheelspin, bogging, extended clutch slip, rough engagement, early/late shifts and incomplete runs without inventing time-loss estimates. Each owned car retains best 60 ft, eighth, quarter, reaction and trap, plus completed-pass count. Three tune slots restore adjustments without altering purchased parts.

The Vehicle Workbench offers stock and modified dyno curves, validated vehicle JSON, assisted benchmarks, experimental telemetry and an all-car power/tire/traction CSV matrix. **Compare current build** captures a baseline and tests five launch RPMs and seven final drives, with consistent assists and CSV export. Runtime race tunes, driving settings and saved last-pass details are copies, so modifying garage data cannot alter an active race or its saved tune snapshot.

Garage **P**, result **R**, and result/paused **G** provide practice, retry and return shortcuts. Practice records valid finishes exactly once but changes neither cash nor reputation. Result comparison uses the previous pass at the same distance and includes both ET and trap changes; driver/build differences remain available in the saved snapshot.

## Current evidence

2L completed the shared mechanics foundation (8,877 checks). 3A–3D add collection, assembly/configuration ownership, valuation and paid operations. See [3D evidence](../reports/phases/3d/CHANGES.md) for current targeted gates, native controls and the separate source/Windows checkpoint. The sections below retain the earlier Slice-1/early-Slice-2 audit as historical context; its counts and then-remaining gaps do not describe the current model.

## Historical evidence and limits

See `reports/VERIFICATION.md` and linked reports for completed checks. **523 foundation checks** pass, including 32 calculated-flywheel, 58 physical-induction, 68 powertrain, 74 audio, 71 bench/exhaust, 16 exhaust-pop, 15 engine-concert, 7 shared-audio and 22 control-source/dyno checks. Audio now has an adjustable +6 dB idle boost, post-synthesis shared master gain and live output meter; real slider/PCM tests cover master, engine and turbo gains plus drag-release persistence. The bench's protected idle floor and continued combustion during manual shaft handovers have explicit checks. The eleven bench scenarios each produce identical PCM on repeat across all four stock cars. Scene integration, four-transmission Windows passes with launch/failure/stall/cold-idle checks, and gameplay/keyboard suites also have evidence. A new starter profile can beat all four local career events on Normal AI using earned cash, repeated wins, upgrades and tuning; the exact path is saved in `reports/career-path.txt`. This demonstrates a viable route, not broad career balance.

The historical early Slice 2 build passed **925 automated checks**, including 93 component-failure/AWD/free-shaft checks and retaining the original 523 foundation checks and adding 127 contracts/catalog/ownership/inventory/physical-bench, 17 reset-game, 97 shared-stress/abuse, 23 simulation-rule/graph, 24 car-inspection/draft/unit and 21 stock-durability/energy/shift checks. OEM builds now survive the short full-wear endurance matrix; STOCK_DURABILITY.md records the brake calibration and automatic-driver correction. Readable bar comparisons and explicit Apply/Revert are documented in CAR_STATS_AND_TUNING.md; reports/VERIFICATION.md distinguishes passing mouse/rendering evidence from foreground-interrupted final reruns. Desktop fixtures drive the actual player Update/OnGUI/DSP loop with isolated workspace saves. Real OS keyboard and mouse suites cover Parts transactions, physical-bench slider/keyboard ownership, shifts with held gas, complete replay/import, pause, Settings reset/cancel/reload and the race/sound regressions. No runtime errors were recorded in the final suites. These do not validate physical keyboard/controller feel or Android touch. Twenty-five WAV auditions cover physical induction/fueling, limiter methods, warm/cold idle, deeper older-car pulls, V8 neutral blips and 3,500 RPM two-step, with roughness and spectral checks; see `ENGINE_AUDIO.md` and `reports/audio-verification.txt`.

Engine-side inertia and bounded clutch capacity replace the launch RPM floor. Clutch pressure controls torque transmission; digital release has a configurable ramp while touch/mouse pedals permit partial input. Assisted launch regulates RPM through clutch load. A weak clutch can let RPM flare without corresponding acceleration; gearbox upgrades raise capacity. That early checkpoint still used demand slip; 2G–2H replaced it with independent wheel/contact rotation and physical driveline coupling. Manual clutch can stall and restart; converter/DCT/CVT hardware follows its own behavior. Rev matching applies a temporary mismatch penalty rather than simulating synchros. AI difficulty currently changes reaction rather than vehicle power or consistency. Class thresholds, economy and starter-car class placement remain provisional.

The extracted input adapter captures each finger independently and consumes launch/shift edges once on fixed ticks. W and Space are interchangeable held gas keys, independent of action edges. Race timing treats E / shift-up as first-gear engagement from staged neutral, then as upshift while driving; the six-control overlay combines these actions. CVT selects Drive and retains continuous ratios. Enter remains a launch/restart alternative. Automatic launch retains its reaction schedule, early assisted/manual engagement red-lights and the launch edge cannot also select second. Gas stays held during transmission torque interruption and is read immediately after entering/resuming a race. Pointer contacts still require release after a reset. Synthetic tests cover simultaneous throttle/clutch/brake, partial pressure and independent release. An opt-in Windows OS keyboard-event probe tests the real Unity polling path without driver-input injection. Physical hardware rollover and Android behavior remain unverified. Engine audio observes the physical compressor state and valve events; continuous combustion/turbo white noise has been removed, valve air is filtered and tire hiss is quieter. The five induction kits change live torque, HP, dyno curves and acceleration. Pausing freezes compressor/race/shutdown state and silences audio; menus/results also silence playback. Earlier version-1 saves retain hardware choices and receive compatible tuning defaults. These are reduced-order models with fictional calibration; see `INDUCTION.md`.

The prototype uses a temporary IMGUI systems overlay. Preserve the mechanics boundary and replace this overlay with uGUI/UI Toolkit when the input and application state are separated. Finished production audio, controller input, Android device acceptance, measured compressor-map calibration, individual editable ratios, XP, prize cars, much larger career content and storefront work remain later work. Shared tire thermals/burnout, durability/repair, event restrictions and the prototype operating economy are already implemented. Launch/gearbox/fueling models are detailed in `POWERTRAIN.md`; shared stress and difficulty controls are detailed in `PHYSICAL_DRIVING_BENCH.md` and `SIMULATION_SETTINGS.md`.

## Next focused slice

1. **3H — integrated economy/save checkpoint** follows completed 3A–3G. Run interruption/migration, quote/ownership/reservation, pause/resume and packaging matrices.
2. Human driving/listening and economy pacing review, then finished art/UI and larger career content.
3. Android/controller device validation remains separate acceptance work.

Historical early Slice-2 equipment note:

Parts/equipment foundation (2026-10-08): see [part effects and equipment](PART_EFFECTS_AND_EQUIPMENT.md). Category-specific ratings and descriptions, engine/coolant/radiator heat transfer, delivery limits, left/right open/LSD/welded/spool allocation, removable interior/comfort equipment and separate limiter/two-step/shift-light purchases now use the shared race/bench resolver. Hardware gates editable equipment settings; garage drafts still require Apply. Independent wheel/contact/slip-work tire life and final calibration remain in 2G–2L. Old physical captures must be recorded again for model v5.




3F adds [garage business](GARAGE_BUSINESS.md): captured rates/fractions, expanded offline caps/bays and exact car/engine/component reservations with atomic claims and cancellation. See [3F report](../reports/phases/3f/CHANGES.md). 3G balance is complete; 3H integration acceptance passed; see the full audit above.

Current 3H evidence: 13 domain gates, 18 native Windows workflows, 16,719 foundation checks and the 5,662-check / 2,429-run matrix with zero failed assertions. Overlapping totals are not summed. Heat management OFF holds optimum and disables race-entry choices; ON permits ambient/optimal-at-start/live cooling and optional physical warm-up. See [3H checkpoint](../reports/phases/3h/CHANGES.md). No later slice has been started.

## Tire feedback refinement and performance diagnosis (2026-10-09)

Contact-based rubber scrub/squeal and bounded tire smoke now run in races and the shared physical bench. The acoustic bench retains authored slip audio/particle previews and deterministic export. The eight-engine clutch audit confirms capacity-limited torque, friction power loss and reduced new tire-slip work under matched partial-clamp conditions. Rendered profiling identifies synchronous checkpoint saves and full bench CSV/export operations as major hitches; optimizations are deliberately pending the requested report/review. See [tire feedback and stutter findings](../reports/tire-feedback/CHANGES.md). This does not advance Slice 4 or alter the accepted mechanical/save model.


## Asynchronous race checkpoints (2026-10-09)

Approved after the tire-feedback stutter report: full recoverable saves remain at race entry, result, pause, exit and other explicit transaction boundaries. During driving, one copied active-car snapshot per simulated second (also on newly detected failures) is queued to a bounded background journal writer. It captures fuel, fluids, component/element condition and the thermal clock without rebuilding the garage ledger. Full saves drain pending writes and retire the journal only after durable publication; interrupted loads validate and merge it before closing the run without payout. The physical bench remains isolated. This increment does not change physics, wear, fixed-tick catch-up, CSV exports or graphs. See [implementation and verification](../reports/async-race-saves/CHANGES.md).


## Tire sound harshness refinement (2026-10-09)

User audition requests recognizable rubber-on-asphalt screech with less white-noise-like grit: replace the four wavering pure-tone voices with prominent noise-excited rubber resonances, remove the broadband hiss bed and reduce low contact texture. Preserve contact-driven intensity, smooth shift chirps and grip recovery, shared volume and pause/reset/export behavior. See [sound refinement and verification](../reports/tire-sound-refinement/CHANGES.md). Physics, smoke and asynchronous race saves remain unchanged.


## Race audio and opponent worker (2026-10-09)

Tire resonance now follows actual effective friction, load and radius as well as sliding work. Opponent engine/tire audio uses its own state, distance attenuation and a saved volume control under the shared master. Desktop races advance the opponent on a persistent worker with a fixed-tick join and the same authoritative physics. Eight-car exact parity and paired timing passed; see [checks and implementation](../reports/race-audio-worker/CHANGES.md). No later slice is advanced.


## Menu preview responsiveness (2026-10-09)

Stat previews publish dyno values immediately and calculate shared copied-car driving estimates on one bounded background task after a short debounce. Garage reuses condition-aware inspections; service prices and tuning hardware resolution are cached. Difficulty/driving gestures save only after release, while transactions and Apply retain full saves. See [implementation and checks](../reports/menu-preview/CHANGES.md).


## Opponent audio, menu responsiveness and shop/inventory extension (2026-10-09)

Approved requested refinements: modulate tire resonance using actual grip/load/radius as well as contact sliding work; opponent-specific audio with distance attenuation and a saved volume under the shared master; concurrent opponent advancement with fixed-tick joins and exact shared-physics parity; immediate dyno plus bounded background copied-car probes; no full saves from tune/part/cart previews, with definitive transaction/Apply and completed settings gestures saved. Parts now provides separate Installed/Inventory/Shop pages, a session-only cart with quantities and cumulative checked-install preview, atomic checkout preserving displaced identities/condition, and current-versus-no-wear graph/stat comparison without healing or refilling actual assets. Existing immediate replacement/service shortcuts remain. See [implementation, timing and 392 named checks](../reports/shop-inventory/CHANGES.md). This completes this spontaneous increment without advancing another planned slice. Full-save/transition pauses and simulation allocations remain documented limits.

## Shared inventory and component-set extension (2026-10-09)

Approved post-3H refinement: shared storage/car/engine/seller/cart views, retained dealer component stock, complete-set defaults with explicit unit counts, optional individual engine/tire purchases, targeted reuse, staged ownership moves and atomic net-price trade checkout. Existing complete/partial assembly valuation, salvage, driving ownership and explicit Apply remain intact. See [contract and controls](INVENTORY_COMMERCE.md) and [verification](../reports/inventory-commerce/CHANGES.md). No later planned slice is advanced.
