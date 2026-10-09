# Individual engine components, rebuilds and clutch hardware

Approved extension to Slice 2K–2L, 2026-10-08. Shared race/physical-bench model `draglab-cylinder-service-v14`. 3G calibrates shop labour/filter prices with recorded funded scenarios; final human pacing and integrated acceptance remain 3H.

## Individual engine condition

Installed sets retain their catalog/assembly identity and contain identified individual elements. Supported: pistons, rods, ring packs, rod-bearing pairs, valves, springs/retainers, followers/rockers, valve seals, spark plugs/leads, ignition coils and compatible port injectors. A carburetor/throttle-body injection unit stays shared. Valve and ignition architecture are authored engine metadata, not inferred from the number of currently working cylinders.

Local structural wear, compression leakage, disabled ignition/injection, weak springs and stuck valves change the affected cylinder. A weak spring has RPM-dependent valve float; deterministic prolonged float can damage the matching valve. Dropped valves, seized parts and broken rods are destructive. These faults are separate from aggregate crank/timing/block damage. Shared/distributor coils affect all cylinders; wasted-spark coils affect their paired cylinders; individual coils affect one. Fuel delivered is tracked separately from successful combustion; disabling a plug does not silently stop its injector. Local rings/seals influence oil consumption.

Cylinder contribution changes crank torque, simplified torque ripple and the sound generator's original firing-order event envelope. The original cylinder count remains intact. OEM firing orders for 4/6/8-cylinder profiles are authored game defaults, not manufacturer-specific validated data. This is a lumped mechanical model, not individual gas-exchange/combustion CFD. Exact physics replay includes component identities/faults and crank phase; live audio remains asynchronous DSP.

## Replacement and inventory

Parts / Repairs: select a cylinder under an installed supported set and replace one element. Unit price is the set price divided by its element count, rounded up; normal play charges this component price, with no additional labour for this targeted action. Isolated bench service remains free. Removed individual elements are retained separately for reuse/sale with their identity, health and fault. Reuse requires the same matched definition and role to avoid freely mixing incompatible piston masses or valve profiles. Full compatible kits can still upgrade/downgrade via existing inventory. Whole-set structural failures remain distinct from a single fault. Replacing a piece does not renew its neighbours, other assemblies or fluids.

## Engine rebuild shop

Garage fluid/service section and Repairs expose a quote, Pay + send, remaining time and Collect. Quotes include core/valvetrain reconditioning, replacement of failed/unsalvageable matched sets, fresh engine oil and a filter. Current 3G labour is $150 + $25 per cylinder, authorable in OperatingPolicy, plus replacement/service costs. Standard / extended-life filters cost $25 / $80. Existing paid jobs keep their captured price and deadline. Duration is five real minutes plus one per worn/failed set. The paid quote, assembly identity, captured contents and UTC deadline persist across reload/offline time. The car/installed parts remain reserved until collection; insufficient funds, early/repeated collection, changed contents and edits/removal/racing during reservation are rejected. Existing atomic profile transactions roll back failed saves. No automatic second payment at collection.

Reconditioning brings eligible intact pieces to at least 95%; replacement retains old sets in inventory. Saved tune, fuel, tires, brakes, turbo, cooling and drivetrain remain separate. Missing required core parts must be assembled before requesting a rebuild. 3D supplies atomic paid service; 3G validates a timed damaged-engine rebuild/sale margin and funded recovery. See [economy evidence](../reports/phases/3g/CHANGES.md); 3H remains the integrated acceptance gate.

## Clutch hardware

Existing factory/street/race clutches plus six additional assemblies per car: dry twin organic, dry twin ceramic, dry triple sintered-metal, wet multi-plate organic, wet multi-plate race, and dry centrifugal. Hardware specifies dry/wet environment, lining material, plate count, torque-capacity multiplier, thermal buffer, cooling, sliding-work life and engagement response. Ceramic/sintered engagement is sharper; greater torque/heat tolerance does not imply longer slip life. The centrifugal option uses RPM-dependent clamp ramp; shift disengagement remains supported.

Wet assemblies include a matched housing/pump conversion and a separate 2 L clutch-oil bath. Authored installed mass includes the filled bath; drained oil subtracts its mass and moment once. Fluid quantity/condition affect capacity/cooling/viscous drag and respond to fluid difficulty settings. Engine oil is not substituted for clutch oil. Manual/DCT friction coupling uses these definitions; converter Automatic/CVT keeps its existing coupling. Plate count describes a matched complete assembly; it does not multiply the authored torque limit a second time.

## Bench controls and repeatability

Physical DrivingTest → starting wear / component selection → individual cylinder/valve controls. Set health, an applicable deterministic fault and severity while paused or before launch. Current cylinder contributions/float/fuel fraction are shown, and individual cylinder output, valve float and clutch-oil quantity are available as graph channels and CSV columns. Replace one test component without touching player inventory. Wet clutch oil amount/condition are configurable in the same tool. Independent configuration controls preserve pedal input ownership. Existing recording/replay captures fault state, conditions, inputs and continuation state.

Random intermittent faults and nitrous delivery remain deferred. Automated current-model durability and final 2K–2L integration acceptance are recorded in the [2L report](../reports/phases/2l/CHANGES.md). Human listening, measured production durability and Android device checks remain separate.

Engineering references: [NGK shared/wasted-spark ignition](https://ngksparkplugs.com/en/resources/waste-spark-ignitions), [Motorservice valve damage](https://www.ms-motorservice.com/int/en/technipedia/valve-breakage-at-skirt-end), [Schaeffler wet double-clutch systems](https://www.schaeffler.com/remotemedien/media/_shared_media/08_media_library/01_publications/schaeffler_2/symposia_1/downloads_11/Schaeffler_Kolloquium_2014_17_en.pdf), [EXEDY multi-plate clutch hardware](https://www.exedy-racing.com/en/products/hyper-multi-en/). Numerical clutch profiles and service quotes are authored gameplay values.

Current integrated capture model is v15; the v14 model above describes this feature’s original checkpoint. See [2L tooling](INTEGRATED_TOOLS_CALIBRATION.md).
