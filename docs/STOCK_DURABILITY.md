# Stock durability calibration

The stock-car regression uses the same `CarSimulation` as races and the physical bench. The original four OEM recipes ran with full wear, multiplier 1, simulated temperatures and no traction assistance. It does not turn off failure or repair parts between passes.

## Reproduced problems

The baseline reports are preserved under `reports/stock-durability/before-*`. Brakes failed after 2–5 completed quarter-mile passes with braking and a 60-second neutral coast/idle period between runs. Their generic 180 °C failure threshold was inappropriate for the modeled brake assembly. Brake heat also used the configured deceleration rating instead of the actual grip-limited friction work at the road.

The classic V8 lost roughly 13% of internal-part health during a two-minute pull and roughly 32% over its first three passes. Its factory configuration deliberately has no rev limiter. The automatic driver held full throttle through manual-gearbox clutch disconnection, producing unnecessary overspeed during ordinary upshifts.

## Corrections

- OEM brake definitions have a provisional 550 °C limit, 60 W/°C cooling coefficient and 3 GJ aggregate friction-energy budget. The upgrade has a 650 °C limit and 6 GJ budget. These are authored game calibrations, not certified real-vehicle durability figures.
- Road integration publishes brake friction power from actual brake force and mean step speed. Both powered driving and neutral/shutdown use this quantity for shared component heat and wear. Grip caps apply; holding a stopped car produces no friction heat.
- An automatic driver operating a manual gearbox lifts through clutch-open handover and reapplies held gas afterward. This changes the simulated driver action, not the keyboard input latch. Sequential/manual pedal input remains direct. The classic V8 retains its factory absence of a limiter; sustained neutral overrev and bad downshifts remain damaging.
- Component identities, installed ownership and existing health remain unchanged. The catalog updates ratings of newly resolved builds; it does not repair previously failed parts. Use replacement/service or the physical bench's isolated wear reset for an already damaged car.
- Physical recordings now identify `draglab-shared-stress-v3`. Record new scenarios for this model; v2 recordings are rejected with an explanation instead of being silently replayed under changed physics. Player progression and part ownership schemas are unaffected.

## Regression coverage

Each car must survive ten quarter miles, with actual braking/cooldown and carried part condition, a two-minute full-throttle pull, and five minutes of idle. No part may fail; at least 90% health must remain over these short stock-service scenarios. Additional checks cover brake-energy conservation/heat coupling, stationary holding, real thermal failure, automatic lift/held-gas return and manual neutral-overrev failure. Existing destructive downshift, weak-shaft and clutch-abuse checks remain required.

After calibration, minimum part health after ten passes is:

| OEM car | Lowest part health |
| --- | ---: |
| Starter hatch | 98.10% |
| Sport coupe | 97.02% |
| Pickup | 97.66% |
| Classic V8 | 94.80% |

`reports/stock-durability/parts.csv` records per-part health, peak temperature, engine/shaft torque and RPM. Run `./tools/Invoke-Unity.ps1 -Action DurabilityAudit` for diagnostic results, or `-Action Verify` for the enforced full regression suite. This remains provisional durability calibration; long-term tire life, full cooling and remaining component-specific behavior retain their later Slice 2 gates.

For actual Windows Update/DSP/shutdown and persistence checks, run `./tools/Test-WindowsSlice.ps1 -FixtureName stock-durability-review -DurabilityOnly -ShowPlayer`. It uses isolated workspace saves and driver-boundary inputs; no OS keyboard/mouse events are sent. The visible window is needed for Unity's screenshot capture. It runs one actual stock race and stop per car, verifies all parts remain healthy, and checks the saved conditions after returning to Garage.


3H extension (2026-10-09): the enforced gate now covers all eight OEM models. Neutral no longer reuses stale converter heat, converter dissipation is averaged across substeps, and factory converter gearboxes have authored cooler rejection of 80 W/C. Thermal/wear limits and the ten-pass/90%-health assertions remain unchanged. The no-free-break neutral-coast durability fixture remains separate from player race-entry difficulty. New ambient/optimal/live start policies, continuous heat management and shared pre-race burnouts are specified in [heat and race turnaround](HEAT_AND_RACE_TURNAROUND.md). The original four-car health table above is historical; current eight-car outcomes are collected in the Slice 3 audit.
