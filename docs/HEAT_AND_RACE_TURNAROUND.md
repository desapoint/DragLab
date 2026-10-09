# Heat settings, duty cycle and pre-race warm-up

The shared simulation accumulates heat and stress in races and the physical bench. Ratings are exposure inputs, not instant failure switches: delivered torque, slip work, temperature, duration, condition and wear difficulty determine damage. An undersized part can survive short duty cycles and fail under sustained overload. Extreme mechanical loads can still fail quickly.

## Difficulty controls

**Heat management ON:** temperatures evolve throughout driving. Choose temperatures at race entry:

- **Fully cooled / ambient:** explicitly start every race at the prototype's 20 C ambient. This difficulty option resets temperatures only, never wear/failures/resources.
- **Optimal / ready:** start at the shared model's component operating targets (gearbox 70 C, clutch 60 C, brakes 80 C, engine/coolant/oil 90 C), with tires at their authored compound optimum. Subsequent heat evolves normally.
- **Live / elapsed time:** retain the owned car's heat and simulate engine-off cooling over actual UTC time since its last committed run checkpoint, including garage/offline time. Retry immediately grants no fictional ten-minute break. A missing legacy timestamp preserves heat until the first checkpoint; clock rollback grants no extra cooldown.

**Heat management OFF:** temperatures remain optimal throughout driving. Race-entry choices are disabled and their remembered preference is ignored until management is enabled again. The legacy persisted `optimalThermals` flag stores this assist; its UI now says Heat management. Arcade selects heat management OFF, Balanced selects Fully cooled with real heat, and Simulation selects Live with real heat. Independent custom combinations and progression reset preserve preferences.

## Shared cooling and persistence

Live cooldown starts from retained temperatures, rather than resetting them. Components use exponential heat rejection toward ambient with authored cooling watts per C and heat capacity; individual tires include their own heat and tire mass. Oil/coolant pressure, quantities and leaks advance in the same parked fluid model. The hot transient is resolved each simulated second for the first hour, followed by an exponential/leak tail for longer offline intervals. This is a reduced-order thermal model; it does not resolve spatial heat soak or climate weather.

Cooling never repairs health, clears a failure, refills a tank, renews oil, creates parts or awards cash. Engine-off cooling burns no fuel, but existing fluid leaks and venting follow depletion difficulty. All temperature policies advance real elapsed parked fluid leakage first; thermal difficulty cannot bypass independent depletion rules. Ambient/optimal policies then assign only temperatures. Entry saves the resulting condition/resources and updated per-car timestamp atomically; failed writes roll back. Garage return simply commits actual run condition; it no longer grants an automatic 60-second break. Active simulation pause freezes dynamics. Offline cooling is evaluated lazily at race entry, not displayed as an ongoing garage animation. Bench tests retain continuous heat and explicit parked-cooldown controls.

## Optional warm-up before lights

Enable **Optional pre-race warm-up** in Settings/Driving. The car begins in neutral with the lights waiting indefinitely. W/Space gives gas, E engages/upshifts, Q downshifts and returns first to neutral, Shift controls clutch and S brakes. Assisted automatic throttle is temporarily suppressed in warm-up so the car can idle under any driving preset. Installed protection, assists, fuel, wear, stress and failures still use the normal simulation.

FWD/RWD offer a **burnout line lock** which directs braking to the non-driven axle while S is held. It changes actual brake distribution, not a tire-heat multiplier. AWD retains its normal brake/drive behavior; it is not given a fictional stationary tire spinner. Tire temperatures and gearbox/oil heat are displayed. Tire grip responds to compound temperature; overheating and burnout wear can offset any gain. Disable traction control when intentionally testing an unrestricted burnout.

Stop the car and wheels, then **Ready / Enter** begins normal neutral staging and the countdown. Warm-up heat, wear, consumed fuel and fluids persist; timing/splits begin for the actual pass. Moving/spinning wheels cannot be teleported to the start. Garage/quit/interruption commits the warm-up checkpoint without a reward. Warm-up is optional and OFF by default for existing race workflows.

## Audit corrections and evidence

Neutral/staging clear old clutch/converter dissipation; converter heat is averaged across road substeps. Factory converter Automatic/CVT gearboxes receive authored 80 W/C heat rejection instead of generic 25 W/C, while the 140 C thermal limit and failure/wear thresholds remain unchanged. The supercharged V8 had passed a two-minute steady pull but failed repeated launches with insufficient generic cooler calibration. All eight OEM endurance scenarios and destructive cases are required again, including Live/no-free-break behavior.

Physical capture model is `draglab-integrated-tools-v16`. Older exact-physics recordings require new captures; owned identities/condition/resources and legacy saves remain supported. Focused thermal and native settings/warm-up evidence is collected by the 3H full audit runner.

Audit performance correction: owned-profile normalization resolves a complete hardware specification once per car/stored engine, rather than once per installed part. The cached specification is definition-based and immutable during normalization; actual component identities, health, faults, liquid quantities and histories remain owned data. Repeated eight-car normalization is checked for exact state preservation.
