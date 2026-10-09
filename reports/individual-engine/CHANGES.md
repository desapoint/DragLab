# Individual engine service and clutch expansion

2026-10-08. Approved addition to the ongoing Slice 2K–2L work, following `docs/objective.md`. Shared model: `draglab-cylinder-service-v14`.

## Delivered

- Individual identified pistons, rods, ring packs, rod-bearing pairs, valves/guides, springs/retainers, followers/rockers, valve seals, spark plugs/leads, coils and compatible port injectors. Shared carburetors/TBI remain shared components. Distributor, coil-on-plug and wasted-spark fault scope follows engine metadata.
- Deterministic local loss of compression, combustion or injection; RPM-dependent spring float and destructive valve/rod/seizure faults. Cylinder contributions influence shared crank torque, induction energy, fuel/oil use and existing firing-order audio. A failed plug still receives fuel. Random intermittent failures remain deferred.
- Lower-cost replacement of one element without renewing its neighbors. Retained removed elements preserve identity, wear and faults and can be reused with the same matched definition/role or sold. Existing complete-kit upgrade/downgrade and ownership rules remain in place.
- Paid, persistent timed engine rebuild quote, reservation and collection. Offline UTC time advances the job. Reserved engines cannot race or have their installed contents changed. Collection reconditions intact core/valvetrain, replaces unsalvageable matched sets, retains old sets and services engine oil/filter. Fuel, saved tune and unrelated hardware remain separate. Prices, labor and five-minute-plus-work duration are provisional Slice 3 economy values.
- Six additional clutch assemblies for each of four cars: dry twin organic, dry twin ceramic, dry triple sintered-metal, wet multi-plate organic, wet multi-plate race and dry centrifugal. Existing single-plate choices remain. Definitions affect capacity, engagement, cooling, thermal buffer and sliding-work life. Centrifugal clamp depends on RPM.
- Separate wet-clutch oil quantity/condition, leakage, fluid-difficulty behavior, reduced capacity/cooling with insufficient oil, bounded viscous drag and missing-fluid mass counted once. Automatic torque converters and CVT keep their transmission-specific coupling.
- Physical DrivingTest starting-condition controls for individual health/fault/severity and wet-clutch oil, isolated replacement, live cylinder contributions, added graph/CSV channels and exact fault replay. Configuration edits retain keyboard pedal ownership. Expanded graph selectors now have readable row height.

## Verification

- Full `BuildWindows`: **5,242 foundation checks passed**, Unity 6000.6.4f1, 2026-10-08 22:57 UTC. Includes **257 focused individual-engine/clutch checks** across all four cars, fault/replacement/save/reservation cases, wet-fluid thermal/mass tests and exact replay through shifts and coast.
- Final UI-only changes were compiled and packaged with `BuildPlayerOnly`; simulation code was unchanged after the full gate.
- Windows Parts fixture `individual-engine-shop-final`: **41 checks passed**, zero runtime/GUI errors. Actual OS clicks cover single-piston replacement/sale, wet multi-plate purchase, paid rebuild reservation, racing refusal and one-time collection. Offline readiness is injected only into the isolated fixture clock; player jobs use real UTC deadlines.
- Windows driving fixture `individual-engine-bench-final`: **38 checks passed**, zero runtime/DSP/GUI errors, including held W through shifts, independent configuration, mass/fuel/fluid controls and full chronological replay.
- Final Windows driving fixture `individual-engine-bench-ui-final`: **45 checks passed**, zero runtime/DSP/GUI errors. Adds actual mouse edits of one ignition element, disabled-plug fault selection and isolated single-element replacement; verifies one-cylinder combustion loss/recovery, pedal ownership and player-save isolation. Screenshots verify readable graph selectors.
- Windows reset fixture `individual-engine-reset-final`: **21 checks passed**, zero runtime/GUI errors. Reset/cancel/reload clears progression and owned hardware while retaining driving and shared volume preferences. The final Parts, driving and reset fixtures total **107 native UI checks**.

The tests also caught and fixed additive-save placeholder migration, tune preservation during rebuild collection, raw-factory versus owned-OEM clutch mismatch and loss of tiny per-element wear increments. Open wet-clutch drag is intentionally bounded rather than asserted to be exactly zero; wear reset preserves separately aged/drained clutch oil.

## Scope and limits

This is a lumped engine model with authored firing-order defaults and matched clutch assemblies. It does not resolve combustion CFD, individual DCT pack/plate wear or locked-crank collision shocks. Individual spares currently require an exact matched kit definition. Full-kit part-out and final shop/economy balance remain later work. Human sound review, Android devices and remaining integrated Slice 2K–2L acceptance gates are still explicit; this addition does not mark those phases complete.

## Files and controls

- Contract: `docs/INDIVIDUAL_ENGINE_AND_CLUTCHES.md`.
- Runtime: `Game/Builds/Windows/DragLab.exe`, with the complete adjacent player data/DLL folders.
- Garage → Parts → relevant installed set: cylinder selection, individual replacement/reuse/sale. Clutch choices appear in the Clutch shop category. Garage/Repairs shows the engine rebuild shop; installed wet clutches expose dedicated oil service.
- DrivingTest → starting wear/component controls: individual faults/health and clutch-fluid controls. Pause or use neutral staging before assigning starting condition. Recording remains optional.
- Focused evidence: `reports/individual-engine/verification.txt`. Full gate: `reports/unity-verification.txt`. Native images/reports: `reports/player-fixtures/individual-engine-*`.
