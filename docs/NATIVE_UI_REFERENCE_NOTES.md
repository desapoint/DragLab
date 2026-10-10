# Native interface reference notes (2026-10-09)

Source: [Design Racing Game Navigation](https://chatgpt.com/c/6ac9abdf-4ff0-83e9-a4e4-7552526c48bc), read in the user-opened in-app browser. Both assistant design replies were visible in the accessibility tree. These are reference candidates, **not blanket user approval or implemented features**. Explicit requirements in [NATIVE_UI_DESIGN.md](NATIVE_UI_DESIGN.md) take precedence.

## Explicit choices already made in this chat

- Filled icons, grey/red styling, top navigation and full-screen menus.
- Garage, Build, Tuning, Race, Business, Data in that order; Settings at the far right and current-car inspection immediately to its left.
- Inspection slides in from the right over the current menu, with car preview, dyno sheet, full stats/specifications, warnings, wear and failed parts. The reference's permanently visible sidebar and main-workspace diagnostics are not selected by this requirement.
- Secondary details can use info dialogs; Settings is the first implementation target with explicit Save/confirmation. The reference's text-first parts lists and restriction of dialogs to complex actions do not override the user's icon/detail preferences.
- 1080p and crisp 4K presentation, existing windowed defaults, mostly stable logical element sizes; new bench UI deferred.

## Candidate inspection content for later control review

- Vehicle identity/configuration/class, power, torque, mass, power-to-weight, available personal performance records and recorded distance. The reference's sample car, rating, values and mileage are illustrative, not real simulation outputs.
- Resource/condition summary: actual simulated fuel, engine oil and coolant quantities/capacities, and available condition/temperature readings. Keep quantity, condition, temperature and operational fault distinct where the current solver actually provides them.
- Mechanical grouping: engine internals/valvetrain; induction/fuel; lubrication/cooling; clutch/transmission/differential/driveshafts/axles; brakes/tires/running gear; electrical/control equipment. Show actual installed component identities and individual wear/faults. A healthy average must not conceal a failed essential component.
- Contextual warning detail: what is observed, which component/system is affected, performance or operation consequences, and applicable repair/top-up/replacement guidance. Severity, group labels, thresholds, warning-click behavior and ordering will be reviewed when requested. A displayed observation must not invent a pressure sensor, diagnosis or failure model.
- Available dyno/stat detail and meaningful unit labels. Distinguish authored specifications, simulated estimates and measured run data rather than presenting example or predicted numbers as measurements.
- Read-only updates from existing state/events, with cached/bounded refresh rather than a second simulation or save from repaint. Reference update frequencies are suggestions, not selected budgets; measure during implementation.

## Suggestions that need separate selection and scope checks

- Exact secondary-page grouping, three density modes, fixed 290–340 px sidebar width, global search, additional utility Save/Load buttons and a separate Help page.
- New fluids/consumables proposed by the reference: brake/clutch fluid, transmission/differential oil, power-steering/washer fluid, nitrous, water/methanol and intercooler water. Also proposed are additional quality, contamination, viscosity, pressure, grade and service-history fields. The reference does not establish that these are simulated now or authorize adding them during UI work.
- Hidden/estimated condition until inspection, sensor availability/faults, inspection workflows, root-cause grouping, long-term storage degradation and detailed fault history. These may require new gameplay/state/persistence mechanics and cannot be introduced just to populate the drawer.
- Expanded used markets, finance/history pages, additional tuning parameters and maintenance/service operations. Match current domain capabilities first; reference navigation labels are not permission to add missing mechanics.

No candidate is implemented here. Resolve detailed choices at the relevant single-control checkpoint; keep Settings first and preserve the existing domain/simulation/save authorities.
