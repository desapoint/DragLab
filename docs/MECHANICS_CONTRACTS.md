# Mechanics contracts — 2A

Historical 2A contract. The current implementation uses independently integrated contact, full captured hardware/resources/conditions and model v15; follow [plan reconciliation](SLICE_2_PLAN_RECONCILIATION.md) and [integrated tools](INTEGRATED_TOOLS_CALIBRATION.md). The future-tense statements below describe the original checkpoint.
Schema 1 is additive. Part definitions describe compatible hardware; part instances own identity and condition. A loose instance has no owner or slot. An installed instance has exactly one owner and slot. Build slots reference instances rather than duplicate them. Engine assemblies and chassis receive distinct IDs in 2C.

Conditions retain health, failure cause and accumulated history. Swapping, resetting a bench or retrying a transaction must never renew an existing condition. Fuel is measured in litres with density and placement separate from rotating inertia. Consumption and persistent wear remain 2I and 2J–2K.

Runs capture copied vehicle, tune and driver data. Later snapshots add installed instance IDs, starting conditions, fuel, model version, seed and tick-indexed input. A run checkpoint belongs to one vehicle and run, with a revision and settlement flag. The contact model currently remains `draglab-demand-contact-v1`; independent wheel speed arrives in 2G–2H.

The race-construction factory copies both vehicles before simulation begins. Factory data or garage changes cannot rewrite an active race. Trace schema 1 will record chronological fixed ticks, including neutral, shifts and braking; it will not sort samples by RPM.

Evidence: `reports/phases/2a/` contains the four-car baseline, contract verification and original build manifest; `before/legacy-earned-profile-v1.json` is an isolated earned-save fixture. Player saves are not converted by 2A.
