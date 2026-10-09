# Fuel usage difficulty

Added independent None / Reduced / Realistic consumption to persisted `DriverSettings` and shared simulation controls. Rates are 0%, 25%, 100%. Arcade selects None, Balanced Reduced and Simulation Realistic; individual changes select Custom.

`FuelDynamics.Flow` scales actual fuel demand, so racing and the physical bench use one implementation. Flow/usage/tank/mass telemetry remain consistent. None leaves the current tank quantity and weight in place; an already empty tank still needs refuelling. Refuelling and Slice 3 pricing scope remain unchanged.

New default enum value is Realistic (zero), preserving older JSON/captures and the previous default physics. Invalid saved settings recover to Realistic; invalid replay settings are rejected. Replay model remains v12. Save/load, game reset and driver snapshots retain the preference independently of wear and driving mode.

Focused `FuelVerify` passed **120 checks**: 46 existing fuel checks and 74 new consumption-rule checks. All four cars, all three rates, demand scaling, 25% repeatable burn, tank/mass accounting, exact JSON trace replay, snapshot independence, presets, defaults and reset persistence were checked.

`BuildPlayerOnly` compiled and rebuilt the Windows player successfully. `reports/player-fixtures/fuel-usage-settings` passed **21 native Windows checks**, zero runtime/GUI errors: actual preset and fuel-mode mouse selections, independent Custom setting, delivery into a real practice race, and persistence through progression reset/reload. The broader previous full foundation gate was not rerun for this additive setting; focused fuel/replay/save checks and the native settings workflow were run on the updated source.
