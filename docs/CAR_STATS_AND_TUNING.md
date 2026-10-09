# Car stats, previews and applied tuning

The **Garage** shows the saved car, settled dyno and full scrollable stats beside it. **Stats** shows the same saved build without a comparison. **Tuning** edits a draft, and **Parts** can compare a proposed component against the installed build.

## Parts and inventory

Use **Preview stats / dyno** in a compatible shop row before buying. The overlay shows saved curves in grey and proposed curves in green. Numeric stats have a minimum-to-maximum bar, with **current → proposed** values on the right. Grey shows the current amount; the changed portion is green for a favorable change, red for a drawback, or amber for a tradeoff. Both values share one scale, which expands for unusually large builds; the labelled ranges are display references, not safe operating limits. Numeric deltas and explanations accompany the bars. Setup warnings are summarized above the stats; expand them for practical advice. Inventory **Preview worn part** uses that actual instance's health/temperature; a shop preview assumes a new component. Removal previews flag incomplete assemblies. Donor previews also preserve the donor part's real condition. Viewing any preview neither buys parts nor changes cash, player condition, installed slots or the saved tune.

**Buy + install**, **Install / reuse**, Remove and donor transfer remain deliberate immediate hardware transactions with atomic saves. Buy loose changes inventory only. An unapplied tuning draft is discarded when entering Parts, so hardware transactions use the last saved tune. Leaving Parts clears its proposed selection. **Preview conservative tune defaults** opens a tuning draft instead of silently applying a tune.

The preview distinguishes direct performance changes from strength, sound and drivability. Forged internals do not invent horsepower; replacing worn internals can restore modeled output. Bigger tires can change gearing, mass and load together. More flywheel inertia slows rev changes but can smooth engagement. Tradeoffs are amber; a favorable direction for a specific stat is green and an unfavorable direction red. This is not a universal “better part” rank.

## Tuning application

Tuning sliders, valve/launch/limiter controls, tire dimensions, boost/pulley/turbo adjustments, preset previews and paint edit only a copied **draft**. **Apply changes** saves that draft atomically and updates the real build; **Revert draft** reloads saved values. Leaving Tuning, selecting another vehicle, starting a race or changing installed hardware discards unapplied edits. A race always resolves saved adjustments and installed parts. Failed saves or stale revisions cannot publish an unapplied tune.

**Preview tune 1–3** loads a preset into the draft; it still requires Apply. **Store saved tune** is available after applying/reverting and stores the saved car settings. Presets include adjustable outer size, width and turbo sizing; installed hardware remains authoritative and cannot be recreated by loading a preset. Test-bench controls remain isolated development tools and do not commit player car tunes.

## Units and explanations

The Settings page offers shared **km/h, mph, m/s** and **bar, psi, kPa** preferences. They are also available on each bench's audio/settings page. Preferences persist separately from progression, survive game reset, and immediately change display/input conversions without changing physics. Race HUD/results, garage/stats, tuning pressure, physical trace speed/boost and relevant acoustic-bench numeric fields follow those preferences. CSV/replay engineering units remain explicit and stable.

- Tire size displays the **outer radius in inches**, from axle center to the outside rubber. Outer diameter is shown separately. This is not rim diameter, which is not authored yet.
- Boost is gauge pressure above ambient and always includes **bar and psi**; kPa remains an optional primary display.
- Horsepower is hp, torque Nm, mass kg, inertia kg m², temperatures °C, shift/spool timing seconds and engine speed RPM. Stats explain the meaning and effects rather than only abbreviating a rating.
- Redline speeds are theoretical gearing limits, not aerodynamic top-speed predictions. CVTs show shortest/tallest ratio speed limits rather than fake numbered gears.

## Warnings and comparison scope

The overlay uses the actual shared induction/condition model for a settled engine curve and runs an isolated bounded quarter-mile probe through `CarSimulation`. The comparison driver shifts automatically, uses full throttle and has traction control off. Its copied parts accumulate real stress locally; no wear, fuel, money, records or ownership changes reach the player. Results explain engine power versus clutch capacity, sustained clutch slip, torque headroom, failed/missing hardware, large/heavy tire launch tradeoffs and estimated wheelspin. Suggestions include lower launch RPM/boost, gentler throttle/clutch engagement, stronger/healthier components, smaller/lighter wheels, different final drive, better grip and traction control.

The dyno is settled full-throttle capability, without transient spool or combustion cuts; the shared driving probe covers those transients. The 30-second probe is a comparison aid, not a prediction of every manual launch/downshift or long-duration thermal event. Wheel slip is still explicitly the current torque-demand estimate until 2G–2H. Full fuel, contact/tire/cooling models and durability calibration retain their later Slice 2 gates. Component prices/specifications and modeled times remain provisional.
