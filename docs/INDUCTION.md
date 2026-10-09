# Spec-driven forced induction

Forced induction runs at the same fixed 120 Hz as engine, clutch, traction and timing. Live pressure changes delivered torque and horsepower before clutch transmission. Player, AI, benchmarks and audio use the same state. Audio no longer builds its own pressure estimate.

## Setups and tuning

Buy engine stage 1 to select a kit in Garage. Existing stage torque improvements remain; the chosen hardware adds pressure, response and drive-load effects.

| Setup | Response and tradeoff |
| --- | --- |
| Single turbo | Exhaust-driven spool, threshold/full-boost RPM, airflow limit, pressure loss on lift/shifts, progressive recovery |
| Twin turbo | Two parallel compressors sharing manifold pressure; greater total flow capacity; matched small-twin response is an editable preset |
| Single supercharger | Fast mechanical response; compressor work and drive friction subtract from torque |
| Twin supercharger | Parallel compressors at a shared pressure target; greater flow capacity and additional drive friction |
| Turbo + supercharger | One of each in series; blower fills the low-RPM gap and automatically bypasses as turbo pressure develops |

Twins do not automatically double boost or power. Hardware size, total flow, pressure target, heating, drive work and traction determine whether a build is faster. A high dyno number can still lose time through lag, clutch slip or wheelspin.

Garage exposes **turbo size** (larger raises threshold/spool time and flow capacity), **boost target multiplier**, **supercharger pulley** (more pressure and drive work), and the turbo valve style. Tune slots restore boost/pulley/valve adjustments while preserving the installed kit, turbo size, engine stages and exhaust stages. All parameters save with the car and last-pass snapshot.

## Torque model

The authored `VehicleSpec.torque` curve defaults to steady factory brake torque. `torqueCurveIncludesFactoryInduction = true` removes the factory induction contribution to obtain the underlying engine curve, then applies the selected hardware once. Factory steady dyno values therefore retain the original curve; a factory turbo delivers less torque while spooling. Set the flag false when authoring an unboosted engine curve to which factory induction should be added.

Turbo rotor energy approaches an RPM/load/throttle-dependent target using spool and coast times. Manifold filling and valve discharge have separate response times. Ordinary neutral revs provide only low exhaust loading. Optional two-step launch retard/preload can build charge without wheel torque; upgraded or factory anti-lag can sustain exhaust energy through lifts/manual shifts, with duration/heat/wear limits. See `POWERTRAIN.md`. Otherwise shifts and fuel cuts stop boost production; the rotor coasts and subsequent acceleration must rebuild pressure. A charged lift or fully interrupted shift records one release event. DCT handover retains some torque and boost. Recirculation retains more rotor energy than vented profiles in this model.

Superchargers respond quickly to crank speed and demand, with bypass at low loading. Net output subtracts compression work and drive friction. The mixed setup multiplies active stage pressure ratios and blends out the blower as turbo capability arrives. A combined pressure limit prevents two stages from exceeding the authored limit. Parallel units share pressure and sum their rated airflow capacity.

Charge temperature is estimated from pressure ratio and compressor efficiency, then reduced by intercooler effectiveness. Torque scales with charge density and deducts turbo backpressure/charger drive load. Compressor airflow limits constrain usable charge mass. Live HP is always `live torque × RPM / 7127`. This is a reduced-order game model; it does not solve real compressor maps, knock, fuel limits, exhaust gas temperature, surge or component wear.

## Authoring and tools

Use **Vehicle Workbench → Select definition in Inspector → Spec → Induction**. `displacementLitres` is the physical engine size; zero falls back to the sound-profile litres for compatibility. Sound cylinder/layout data controls voice generation.

| Spec fields | What they control |
| --- | --- |
| Factory / factory valve / curve-includes-induction | Stock system, valve behavior and torque-curve interpretation |
| Turbo boost / threshold / full boost | Pressure target and RPM envelope |
| Spool / coast / manifold fill / valve release seconds | Lag, rotor persistence, pressure response and discharge |
| Twin turbo spool factor | Response of the authored twin-unit preset; not a universal advantage |
| Per-unit flow capacity | Total compressor capacity; twins have two units |
| Supercharger boost / full-boost RPM / response | Mechanical boost envelope and response |
| Supercharger efficiency / friction | Compression work and drive losses |
| Ambient pressure/temperature / intercooler / efficiency | Charge density and heat |
| Max boost | Shared pressure ceiling |

Preparation initializes missing mechanical profiles and migrates factory valve data using `induction.version = 2`; it preserves later Inspector edits and existing torque tables. Stable serialized kit IDs 0/1/2 retain Factory/single turbo/single supercharger; new modes are appended. Absent tune parameters use nominal values of 1. Existing saves and records are retained, although new induction physics changes times for boosted builds.

Workbench dyno shows stock, current steady WOT and unboosted build curves. **Export all induction dyno / response / race comparisons** writes six setups at multiple RPMs, including torque/HP, pressure at 0.1/0.5/3 seconds, drive load, quarter-mile time and trap speed. Steady dyno curves show settled capability; actual passes include lag. Benchmarks and AI use the same neutral staging history as an equivalent automatic player pass. Shift suggestions are cached from the captured build's steady curves.

Actual race CSVs include total/stage boost, spool, live engine torque/HP, blower drive torque, charge temperature and airflow. See `reports/induction-verification.txt` and `reports/induction-comparison.csv`. The opt-in Windows test `tools/Test-WindowsSlice.ps1 -InductionOnly -ShowPlayer` races all five kits with isolated saves and captures live telemetry, listener audio, pause behavior and screenshots.

## Reference basis and limits

Garrett distinguishes [parallel twin units from series compound pressure](https://www.garrettmotion.com/ja/news/newsroom/article/turbo-tech-compound-turbo-systems-multiplying-boost-for-racing-applications/). Eaton describes [mechanical low-speed boost and turbo/supercharger compound applications](https://www.eaton.com/gb/en-gb/products/engine-solutions/superchargers/TVS-technology-applications/tvs-performance-automotive.html), and [bypass/clutch strategies to reduce drive losses](https://www.eaton.com/ie/en-gb/products/engine-solutions/superchargers/tvs-overview.html). These support the model's qualitative behaviors; all current numeric vehicle/kit calibrations are fictional gameplay data. Twin superchargers use a parallel arrangement chosen for this game. Sequential turbo switching, compound twin turbos, centrifugal blower families and measured engine/compressor maps remain later refinements.
