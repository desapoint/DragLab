# Launch controls, rolling inertia, gearboxes and fueling

These systems run at 120 Hz in the shared player/AI/benchmark simulation. Torque and sound observe the same compressor, limiter and fueling state. Current values are fictional calibrations, intended for tuning the first slice.

Calculated flywheel mass/radii, neutral RPM decay, stored energy and finite shift synchronization are described in [FLYWHEEL.md](FLYWHEEL.md). Gear selection preserves engine RPM and rolling momentum; manual clutch disconnection interrupts shaft torque while held throttle continues combustion.

## Factory defaults and upgrades

| Car | Fueling / cam | Transmission | Main limiter | Launch controls |
| --- | --- | --- | --- | --- |
| Juniper hatch | Injection, mild cam, strong idle control | Manual | Hard fuel cut | No factory two-step or anti-lag |
| Kestrel coupe | Injection, mild cam, strong idle control | DCT | Soft fuel limiting | Hard ignition two-step with launch retard/preload |
| Bison V8 | Carburetor, aggressive cam, weak idle control | Manual | No limiter | No factory launch controls |
| Timber pickup | Carburetor, mild cam, weaker idle control | Torque-converter automatic | No limiter | No factory launch controls |

Buy the **CONTROLS** package for $800 once per car to enable configurable two-step, launch preload, shift/overrun anti-lag, main limiter style/setpoint and fuel/ignition cut methods. Each function has Factory / Off / On settings. Factory equipment works without this purchase; Off disables that function. Two-step style is independently Hard or Soft. Main limiter also supports None. Launch RPM sets the lower two-step target; the main limiter is separate.

Gearbox stage 1 unlocks Factory / Manual / DCT / Automatic / CVT swaps. These change hardware behavior independently of the driver's Automatic / Sequential / Manual-clutch input preset. Only manual hardware uses the driver clutch pedal; CVT has no discrete E/Q shifts. Hardware remains installed when loading adjustment presets. Wheel size/width/mass are also treated as hardware. Controls, setpoints, cold-start preference, valve and other adjustments can be saved in tune slots; purchases cannot be restored from a slot.

## Two-step, boost preload and anti-lag

Without two-step, assisted staging retains the existing tuned-RPM hold. Explicit W/Space overrides it and free-revs toward the main limit. With two-step enabled, holding gas uses the launch limiter rather than a perfectly steady artificial RPM clamp. The car remains neutral with brakes held and zero wheel torque. At launch, the lower limit releases and compressor charge is retained.

Hard limiting switches combustion off at the target and resumes below the recovery band, producing bounce. Soft limiting progressively reduces combustion across the authored RPM band and settles near the target. Fuel cut removes combustion; ignition cut has separate exhaust-pop events. Hard and soft limiting therefore differ mechanically and acoustically. The same control package can behave differently with each engine's torque curve, inertia, cam, mixture and cut settings. A two-step alone does not guarantee useful launch boost.

Launch preload models retarded/exhaust-side combustion while staging on two-step. It adds exhaust energy to spool the turbo without adding free crankshaft torque or moving the wheels. Fuel cuts reduce that exhaust energy relative to ignition-retard cuts. Shift/overrun anti-lag can sustain rotor energy during a throttle lift or manual transmission torque interruption. DCT handover already retains some crankshaft torque and compressor drive.

Anti-lag is enabled only for turbo-equipped hardware. Both launch preload and rolling anti-lag have continuous activation-duration and exhaust-heat limits. Holding the pedal after duration lockout cannot immediately retrigger it. Release allows cooldown/recovery. Active exhaust combustion accumulates wear and produces discrete pop events. This is a reduced-order exhaust-energy model, without ignition timing maps, air-injection hardware, turbine-temperature chemistry or real turbo durability prediction.

## Limiter, overrev failure and stalling

Main limiting uses the selected style, cut method and RPM setpoint, independently of the nominal engine redline. None removes combustion limiting; prolonged revving beyond the authored safe-RPM margin accumulates damage until the engine fails and produces no driving torque or combustion audio. A fuel limiter cannot protect an engine mechanically forced beyond its safe speed. Installed gearbox/controller hardware governs downshift protection identically in races and the physical bench: ordinary manuals permit mechanical overrev, while DCT/automatic and the optional interlocked manual reject an unsafe request. A limiter cannot stop clutch back-drive; clutch assistance does not install an ECU interlock.

Assisted launch, automatic shift and CVT RPM targets respect a lowered main limiter, with additional headroom for soft limiting. These effective targets are captured for the pass without changing the saved garage tune. This prevents automatic hardware from remaining on a low limiter in first gear. Dyno/class figures still describe settled engine capability; transient limiter cuts, fueling and wear determine actual pass output.

A manual clutch can pull engine RPM below idle. A low-RPM clutch dump against a heavy rolling load can stall it, while gradual engagement or assisted clutch control can recover. **Hold Left Shift and press E or Enter** to restart a stalled manual engine. A failed engine cannot be restarted during that pass. Transient warmup/exhaust-control state starts afresh per pass, while shared stress now persists actual installed component condition, heat and failures on finish/garage/pause/quit. Retrying does not renew those parts. Full cooling, all component-specific wear effects and interrupted-run checkpoints remain later Slice 2 gates. See [persistent component wear and replacement](GAME_PLAN.md#required-persistent-mechanical-wear-and-replacement), and [modular compatible parts and inventory](GAME_PLAN.md#required-modular-components-compatibility-and-inventory). Driving stress affects the owned part's condition; removal/reuse never repairs it. Slice 3 balances service/replacement/resale costs and recovery within career progression.

## Wheel mass and effective gearing

The authored vehicle mass includes its stock wheels. For each replacement assembly:

`mass = stock assembly mass × diameter multiplier³ × width multiplier × mass multiplier`

`I = inertia factor × assembly mass × radius²`

All four assemblies contribute static mass changes and an equivalent rolling inertia load `sum(I) / radius²`. Acceleration and braking use vehicle mass plus that equivalent load. Manual clutch synchronization includes the rolling inertia reflected through gearing. Diameter also changes road-RPM coupling and wheel torque leverage. Wider wheels provide a modest diminishing-return grip adjustment, independently of compound stages.

Large/heavy wheels can therefore accelerate more slowly, require higher launch RPM, bog or stall a manually engaged engine. Wheel mass scaling is an editable gameplay approximation, rather than a claim about every wheel construction. Contact slip remains demand-based; independently integrated axle spin, tire deformation, tire heat, brake bias and lockup are future refinements. Sprite geometry currently remains the existing artwork; these controls change the mechanical build.

Independent axle angular velocity, a tire-force curve, spin retained while the clutch is open, physical grip recovery and clutch/converter coupling to actual wheel speed are explicitly required in [Slice 2](GAME_PLAN.md#required-wheelspin-and-driveline-integration-not-implemented-in-slice-1). This includes separate converter hardware torque limits and repeatable full-shift telemetry/acceptance checks. The current build does not yet simulate those contact dynamics; its shift chirps/spin remain demand-based estimates.

## Gearbox models

| Hardware | Current behavior |
| --- | --- |
| Manual | Discrete ratios, shaft torque interruption, continuing engine rotation/combustion, manual or assisted clutch, bounded rev matching and stall risk |
| DCT | Faster clutch handover, partial torque retention, no driver clutch requirement, reduced compressor interruption |
| Automatic | Fluid pump load, low-speed torque multiplication, slip, speed-based lockup and partial torque retention on shifts |
| CVT | Rate-limited continuous ratio toward target engine RPM within min/max ratios; torque capacity; modeled fluid launch and lockup |

Converter pump loading varies quadratically with RPM. The CVT HUD shows **CVT D**, its continuous ratio and operating RPM target; staging shows **CVT N**. Its internal forward-drive index remains 1 for simulation bookkeeping, rather than a fixed first-gear ratio. It does not simulate fake upshifts, show SHIFT NOW or respond to discrete E/Q gear inputs. Gearbox stages retain their shift-time/clutch-capacity effects; individual DCT, converter and CVT specs are editable. These are representative architectures: wet/dry DCT heat, planetary hydraulic maps, belt slip/wear, hybrid eCVT and non-converter CVT launch types are not modeled yet.

## Older-engine fueling and sound

**Spec → Engine Behavior** owns Injection/Carburetor, cam overlap, idle control, mixture variability, accelerator-pump effectiveness, hesitation recovery, cold roughness, warmup time and mechanical clatter. Carburetor alone does not imply an aggressive cam or badly adjusted fueling; these parameters are independent. An injected engine with an aggressive cam can also have a choppy idle.

The simulation generates idle torque irregularity and RPM wander, throttle-opening hesitation, intake-gulp strength and transition/overrun pop events. A cold-start option begins with greater roughness and hesitation, which decrease as temperature rises. Audio reads these actual states to add pronounced combustion pulses, hollow intake resonances, low bogging tones, overrun burble, bounded exhaust pops and quiet crank-related valvetrain clatter. Darker filters and stronger low bank/crank components persist into high-RPM pulls. Throttle itself excites the exhaust voice, including a strong V8 roar in neutral. Load/brightness/volume blend continuously instead of switching idle and driving voices. Continuous engine/turbo white noise remains removed. Pop tails mute on pause and do not replay when resuming. Cold engine behavior starts at idle; starter cranking/choke controls and long-lived garage warmup are future refinements.

Warm idle has audible low-mid combustion pulses with a subdued ticking layer. Hard ignition two-step uses a separately authored launch-cut cadence (nominal 36 Hz) to produce repeated pops during cuts, alongside the mechanical RPM bounce and open-throttle sound. Fuel cuts do not emit those ignition events. Two-step coloration follows actual launch-control state rather than sounding like an idle loop played at a different pitch.

## Tools and evidence

Fuel-system behavior above concerns combustion response, not tank management. No tank quantity, fuel-flow tracking, fuel weight or refill control is implemented yet. [Slice 2](GAME_PLAN.md#required-fuel-component-and-quick-refuelling) requires those systems, saved quantity and free quantity-limited refuelling; fuel prices and the complete economic loop belong to Slice 3. The [mass/load requirements](GAME_PLAN.md#required-mass-load-and-grip-model) also replace the construction-time mass approximation with explicit component/fuel placement and live mass updates. See the [audit](DRIVING_DEPTH_AUDIT.md) for current coverage.

Use the Garage controls for player builds, or **DragLab → Vehicle Workbench** for isolated test tunes. Factory control, wheel and transmission data are editable in the vehicle Inspector or validated JSON. Preparation initializes `engineBehavior.version = 1` and `transmission.version = 1` once, retaining later authoring changes. Missing save fields use stock hardware/settings, so prior parts, records and cash remain intact.

Workbench **Export launch / limiter / wheel / gearbox checks** runs meaningful scenario comparisons and exports `reports/powertrain-verification.txt`, `reports/launch-limiter-comparison.csv` and WAV auditions. Foundation verification also runs them. The new sound previews include hard/soft fuel/ignition two-step, warm/cold carb idle and injected idle. Actual race CSV adds combustion fraction, hesitation, warmup, heat/damage, stall/failure, rolling load, wheel radius, CVT ratio and converter slip. The opt-in Windows probe is `tools/Test-WindowsSlice.ps1 -PowertrainOnly -ShowPlayer`; it uses separate workspace saves.

## Reference basis

[Haltech launch-control documentation](https://support.haltech.com/portal/en/kb/articles/how-launch-control-works) describes selectable fuel/ignition cuts, hard/soft methods and launch fueling/ignition adjustments. It distinguishes launch anti-lag from air-injection rally systems. [BorgWarner transmission technologies](https://www.borgwarner.com/technologies/transmission-technologies) documents dual-clutch modules and their tunable launch/capacity characteristics. [Holley carburetor support](https://www.holley.com/support/carburetor/) and [accelerator-pump stumble guidance](https://www.holley.com/blog/post/how_to_cure_carburetor_stumble/) support independently tunable carburetor idle and throttle response. The game uses these qualitative mechanisms; numerical settings and sounds are original prototype approximations.

Simulation presets and individual thermal/clutch/stall/traction/rev-match options are documented in [simulation settings](SIMULATION_SETTINGS.md). None prevents new wear/abuse damage without repairing existing failures; Partial runs at 25%; Full uses authored rates. The isolated bench multiplier scales wear and damage without accelerating friction heat or time. Always-optimal thermals removes implemented heat penalties and charge heating in the same shared equations.

