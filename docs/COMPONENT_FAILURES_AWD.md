# Component failures and AWD

Race and physical bench use the same `CarSimulation`, mechanical stress and axle-force solver. Physics identity is `draglab-shared-axles-v4`; older recordings must be recorded again. Existing owned parts keep their condition and identities. A one-time migration adds applicable OEM branch components; intentional removals after migration stay removed.

| Component | Result |
| --- | --- |
| Brakes | Health/temperature reduce actual braking. Complete failure/missing brakes remove friction braking and its heat; engine/propulsion still operate and rolling/aero coast remain. Garage warns rather than declaring engine failure. |
| Common gearbox, drive hub/shaft/differential | Wheel torque disconnects; engine free-revs, momentum persists, healthy brakes still stop the car. Legacy Differential/Driveshaft slots represent common drive links on AWD; new front/rear slots represent branches. |
| Friction clutch | Manual/DCT lose coupling; converter Automatic/CVT do not use this clutch as their propulsion link. |
| Front/rear axle, branch shaft/differential | Only the corresponding drive path disconnects. AWD fallback depends on center/coupling hardware. Axle stress includes final-drive multiplication; branch shafts/differentials use input torque before final drive. |
| Transfer case/secondary coupling | Common center modes lose both outputs. On-demand loses secondary connection while direct primary drive remains. |
| Pistons/rods/crankshaft | Internal failure disables the engine. |
| Fuel/ignition | Combustion stops without automatically destroying rotating internals. |
| Cooling | Heat rejection falls; subsequent heat/stress can damage internals rather than an instant engine failure. |

Brakes currently represent the aggregate brake system, not individual hydraulic circuits/calipers. Broken axles mean disconnected drive, not a seized/jammed wheel. No wall/crash model is present, so brake failure does not invent collision damage. Existing damaged internals are not healed by this change.

The pickup is now the stock AWD example, using rear-primary on-demand coupling. Select it in Garage, edit **Tuning → Drive layout**, then **Apply**. The same controls appear in Physical Driving Bench; isolated setup edits reset the test while preserving pedal ownership.

| AWD mode | Torque and failure behavior |
| --- | --- |
| Controlled fixed split | Configurable requested bias including 50/50. Separate branch grip limits; a failed branch loses its assigned share unless isolation hardware redirects drive. |
| Open center | Healthy branches use a shared torque ceiling. A broken branch free-spins with explicit shaft inertia/drag; geometric torque shares remain, while most output power enters the fast free shaft. Healthy-axle torque is limited by accelerating that light shaft and overcoming its small drag. Isolation-capable hardware can retain useful propulsion. |
| Locked center | Locked-path model distributes available torque against axle grip; retains a connected surviving axle. |
| On demand | Front/rear-primary; slip threshold, throttle preload, maximum normal secondary share and engagement response. Primary break requires isolation hardware for fallback; broken coupling retains primary drive. |
| Active bias | Controller varies front share within authored bounds using axle slip estimates and response time. Optional isolation enables fallback. |

These are prototype hardware/controller configurations, not universal settings available on every real car. A later commercial catalog should bind topology/isolation to installed hardware. Maximum secondary share controls normal coupling; supported isolated-failure fallback can route drive to one axle.

Actual axle force caps use static weight, acceleration weight transfer, tire/pressure modifiers and available paths. Live diagnostics and both CSV exports show requested torque shares, delivered forces, both slip estimates, path availability and brake capacity. Replays/checkpoints capture coupling state and component conditions.

Open-center disconnected branches additionally expose shaft RPM, torque and watts. Tune/bench controls include remaining shaft inertia and friction/viscous drag; shaft coast-down in neutral uses those values. Crank/clutch synchronization reflects that free inertia rather than the mass of the whole car. Converter impulse is bounded by crank/shaft inertias to avoid artificial oscillation and propulsion. Equal torque does not mean equal power: output power is torque multiplied by shaft angular speed. Tests compare positive transmitted work over a full pull because limiter cut/back-drive can return energy momentarily.

The carbureted pickup retains its authored lack of an OEM limiter. Sustained wide-open revving after losing load can therefore cause an actual overrev failure. That is distinct from a branch failure directly destroying the engine. Install Controls and select a suitable main limiter to protect prolonged unloaded test runs; tests cover both ungoverned damage and a protected working pickup.

Independent wheel angular velocity, left/right differential behavior, physical contact slip and torque feedback remain explicit **2G–2H** work. Slip is still estimated from torque demand. This update implements axle distribution and failure semantics, not the later wheel/contact solver.

Reference architectures: [Audi variable rear-biased quattro](https://www.audi-mediacenter.com/en/the-audi-a4-major-upgrade-for-the-bestseller-2019-11884/drive-and-suspension-11894), [Toyota front-primary on-demand AWD](https://newsroom.toyota.eu/2019-2-the-new-rav4/), [Eaton open-differential operation](https://videos.eaton.com/detail/videos/differentials/video/6162816705001/open-differential-operation?autoStart=true). Game specifications/calibrations remain fictional.

Verification: `AxleFailureVerification` covers brake capacity/no-use wear, common and branch failure, fallback topology, coupling response, separate grip caps/force conservation, actual overload, ownership migration/removal, tune copies/presets and exact replay/continuation. Reports: `reports/axle-failures/verification.txt` and `trace.csv`.

Final checkpoint: **925 full-suite checks / 93 focused checks**, rebuilt Windows player, and `reports/player-fixtures/awd-component-failures-v4-protected-final` actual race/shutdown/persistence tests with zero runtime/GUI/DSP errors. The final Windows open-center test installs a suitable limiter, independently of the retained OEM no-limiter overrev-abuse test.
