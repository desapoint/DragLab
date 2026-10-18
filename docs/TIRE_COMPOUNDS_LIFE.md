# Tire compounds, heat and persistent life

## Current calibration — 2026-10-18

The approved performance-tire target is **40–60 clean quarter-mile passes at Full wear**, including the shutdown lane. This is drag-game pacing, not real tire mileage. Actual life still depends on distance, axle load, pressure, tire size, sliding work and heat. Partial retains the approved 25% wear rate; None prevents new wear without repairing existing wear or faults. The isolated bench uses the same model and its existing multiplier.

| Compound | Grip factor | Reference rolling life | Sliding life / tire | Best / damage temperature | New full-set base price |
| --- | ---: | ---: | ---: | ---: | ---: |
| Touring | 0.95 | 250 km | 60 MJ | 45 / 130 °C | $500 |
| Road | 1.00 | 150 km | 40 MJ | 45 / 130 °C | Existing OEM pricing |
| Sport | 1.12 | 65 km | 30 MJ | 60 / 140 °C | $600 |
| Club drag radial | 1.24 | 35 km | 16 MJ | 75 / 150 °C | $1,200 |
| Club drag slick | 1.36 | 30 km | 10 MJ | 90 / 160 °C | $1,800 |
| Street semi-slick | 1.55 | 50 km | 8 MJ | 50 / 135 °C | $1,900 |
| Endurance drag radial | 1.75 | 40 km | 16 MJ | 65 / 170 °C | $2,400 |
| Pro drag radial | 2.10 | 32 km | 10 MJ | 75 / 170 °C | $2,800 |
| Medium competition slick | 2.45 | 30 km | 8 MJ | 85 / 170 °C | $3,400 |
| Soft competition slick | 2.90 | 27 km | 5 MJ | 95 / 170 °C | $4,100 |
| Qualifying super-soft slick | 3.35 | 16 km | 3 MJ | 100 / 170 °C | $5,200 |

Prices are authored set prices before supply fees; check the actual Shop/cart quote. Grip factors multiply the underlying contact model and are not guaranteed road friction coefficients. Greater grip is independent of life: the semi-slick sacrifices sliding/heat tolerance, while soft and qualifying compounds sacrifice life and cost more. Qualifying tires intentionally last less than the normal performance-tire target. Correct throttle, clutch capacity, temperatures, tire dimensions and differential still matter; no tire promises unlimited torque.

All thirteen models retain their existing five definitions and gain six fitted variants. Compound properties are fixed hardware. Existing front/rear pressure and dimensions remain adjustable; each corner can retain a different compound, identity and condition. Single replacements, axle selections, whole sets, inventory reuse and saved setups use the existing purchase flow. New definitions follow the existing seeded stock rules and appear with the next regular refresh (every three eligible races); current stock/reservations are preserved. Hard difficulty still has no fitting-offer guarantee.

Mechanical wear also retains losses smaller than a health float can represent, using the existing saved double remainders. This corrects lost ordinary wear without globally increasing engine damage or disabling durability limits. Loading does not retroactively invent missing past wear. Existing tire consumed-life fractions, faults, identities and inventory are retained; only future use follows the new definition calibration. Physics/replay model is `draglab-variety-v21`; older input recordings require recapture. Known v18–v20 interrupted-run resource journals can recover monotonic fuel/wear history without replaying old physics.

Current before/after matrices and validation are in [the delivery report](../reports/tires-wear-20261018/CHANGES.md).

## Historical 2J checkpoint

The text below records the original 2J implementation and calibration. Current individual tire purchasing, fitment and life values are described above; the original matrices remain archived.


Race and physical bench use the same four independent wheel/contact solver. A tire pack now owns FL/FR/RL/RR condition records: remaining life, failure, temperature, cumulative sliding work, distance and a double precision consumed-life accumulator. Sliding work comes from each tire contact impulse, including powered spin, shifts, neutral recovery and locked-wheel braking. Differential constraint losses are not misreported as tire heat. Pressure and dynamic axle load affect abrasion; compounds author grip separately from durability.

| Compound | Friction factor | Reference rolling life | Sliding life per tire | Optimal / damage temperature |
| --- | --- | --- | --- | --- |
| Road | 1.00 | 40,000 km | 40 MJ | 45 / 130 °C |
| Sport | 1.12 | 25,000 km | 30 MJ | 60 / 140 °C |
| Drag radial | 1.24 | 10,000 km | 16 MJ | 75 / 150 °C |
| Drag slick | 1.36 | 5,000 km | 10 MJ | 90 / 160 °C |

These are fictional game calibrations, not manufacturer tire ratings or lifespan promises. Sliding heat is divided between tire and road: the tire receives 45%, with heat capacity scaled by rubber mass and airflow cooling. Temperature is one bulk approximation per tire; surface/core gradients and carcass deformation are not independently solved. Thermal life loss is gradual above the threshold rather than an automatic whole-car failure. A failed tire retains a small residual contact coefficient; it reduces its own grip without switching off combustion or disconnecting the driveline. Left/right differential and AWD behavior respond to the resulting contact changes. There is no separate puncture or explosive blowout model.

None wear prevents new life loss while heat/contact still run. Partial uses the approved 25% rate, Full 100%, and the bench multiplier scales life loss. Optimal thermals holds each tire at its own compound optimum. Worn/cold/overheated tires affect contact. Larger rubber carries more heat and abrasion reserve alongside its mass/inertia cost. Lightweight rim hardware reduces the rim mass only; it cannot shrink the tire rubber. The radius/width sizing law and rim/tire mass split are authored approximations. Factory gearbox mass now follows its actual coupling type, and differential replacement/removal moments follow the driven axle/center position.

The existing inventory owns and swaps the entire tire pack. Removing, reusing, downgrading and selling it preserves all four histories and the original instance identity. A fresh compatible replacement restores fresh tire contact and retains the worn original in inventory. Legacy aggregate tire condition expands once to four records without healing; subsequent loads retain the individual records. Simulation construction/repair aligns the contact counter without reapplying historic sliding work. Checkpoints deep-copy and encode every new double exactly alongside readable JSON. Physics model `draglab-tire-life-v10` rejects older model recordings with the existing re-record message.

The physical bench shows four life/temperature/work/grip readouts. Starting wear includes independent tire life and temperature sliders; explicit edits reset the isolated test while keeping keyboard pedal ownership. Repair resets isolated tire wear/heat, with no player-save access. Recorded CSV includes FL/FR/RL/RR life, temperature and sliding-work columns at every sample. Full-road compound and burnout captures are in reports/phases/2j. Garage inspection/part previews show condition-adjusted grip and separate rolling/sliding life. Existing colored current→new comparisons show the grip/life tradeoff before buy/install or applying a tune.

Development regression found an overly aggressive initial calibration: all contact heat entered the rubber and a short sliding-life reserve caused stock pickup/V8 endurance failures. The corrected tire/road heat split, independent compound reserves and gradual thermal aging preserve the original durability assertions. The broad audit also caught existing tire failure causes being overwritten: continued simulation now preserves the recorded cause. A native summary regression exposed derived mass fields crowding out an edited tire pressure: direct tune changes now lead the pinned summary. Baseline failures remain in reports/phases/2j/baseline. Final evidence is recorded in the checkpoint and integrated audit; do not treat an intermediate build as the final result.

Recovery of interrupted active-run fuel/wear and final all-system calibration remain 2K–2L. Fuel costs, repair economy and broader progression balance remain Slice 3. Subjective driving/listening and Android device behavior require separate human/device review.
