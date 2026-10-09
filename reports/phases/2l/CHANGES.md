# 2L integrated tooling, calibration and final checkpoint

Status: **complete and accepted, 2026-10-08**. Slice 2A–2L is complete; Slice 3 remains planned.

## Scope changes retained

The original rank-based/simple-wear plan was expanded by approved requests to actual modular owned hardware; independent wheel contact and flywheel/coupling; configurable AWD/failure isolation; per-cylinder/per-valve service; oil/coolant and wet-clutch fluids; hardware-gated tuning/EFI conversion; real-time rebuild reservations; granular 25% difficulty; independent pedal ownership; units/shared audio; summary/comparison tools and isolated unlimited benches. [Plan reconciliation](../../../docs/SLICE_2_PLAN_RECONCILIATION.md) records these changes and adjusted 2K–2L gates. No final operating-cost balancing, nitrous delivery or random intermittent faults is claimed.

## New tooling and integration

- Shared capture model v15 adds measured four-wheel drive/brake/contact torque and full-tick force observations. Copy/validation/exact continuation include these values. Player ownership schema stays unchanged; new physical recordings are required for the new model.
- A single measured registry supplies 215 selectable graph channels and CSV values, including full installed component health/temperature, wheel forces/torques, coupling losses, energies, resistance, mass/fuel, tire conditions, local cylinders and fluid losses. The graph families keep controls separate from driving ownership and previous chronological overlays.
- The existing Vehicle Workbench now exposes compatible part selection, meaningful stats/tuning rights, editable authored parameter drafts, validated Apply/Discard/default restore, and a mixed-part test assembly with dependency warnings, actual resolved benchmark and reusable full physical JSON/CSV export buttons. Stable identity/fitment fields cannot be altered. Runtime catalog resolution reads optional project override JSON; replay-owned fitted definitions preserve their original hardware.
- The integrated gate checks every authored part, valid/invalid atomic authoring, actual cooling effects, immutable captured hardware, full/continued exact traces, meaningful measured wheel outputs, complete CSV columns, four-car stock ET/trap/F–S/braking, legal scripted sequential/manual comparisons, and 32 controlled mass/fuel/wheel/pressure/cold/difficulty runs.
- The current catalog-wide matrix exercises every compatible single-part replacement and all transmission/induction/wear combinations, idle/staging/blips/pedals, existing failures, wheel/flywheel extremes and supported AWD modes. EFI conversion tests now require its compatible controller rather than incorrectly expecting the conversion alone to run.
- Full Windows build action includes the current integrated matrix and scene gate. Both original test-scene entry paths remain packaged.

The integrated gate found and corrected duplicate legacy/per-part graph labels. Scripted shift calibration initially omitted launch and then shifted before clutch synchronization; fixtures now use matched automatic launch and legal state-based shift timing. Production assertions remain intact. The legacy synchronous scene fixture also held a pre-transaction profile reference and omitted Update tick accounting; it now verifies the committed profile at the correctly numbered boundary. The scene gate passes without changing save/reward behavior. JSON timing comparisons use the established 1e-10-second serialization tolerance. Native Parts assertions now await the next rendered frame after queued purchases, preventing reads of the previous inspection cache. The stock durability fixture captures conditions after the declared garage cooldown and also asserts that cooldown preserves identities and never repairs wear; comparing pre-cooldown temperatures to the post-cooldown save was incorrect. Seeded stock cars are initialized through ownership migration before preview/race entry, matching the actual garage lifecycle. These fixture corrections preserve production mechanics and ownership assertions.

## Accepted domain gates

- Full Unity foundation: **8,877 checks passed**, UTC 2026-10-09T00:43:00.6571702Z, Unity 6000.6.4f1. Includes 148 recovery checks, 741 integrated tooling checks and the current 2,746-check catalog matrix.
- Catalog matrix: **1,185 simulation runs / 17,977 simulated seconds / zero failures**. Every compatible authored part and supported transmission/induction/wear combinations are covered.
- Synchronous scene gate: accepted after updating the fixture for committed-profile identity and live boundary tick accounting. Backup recovery, exact-once rewards/practice records, legacy preferences and twelve-layer vehicle assembly pass.
- Final plot headers show the selected channel's live value and units, including when recording is off; this presentation change leaves simulation and captures unchanged.

## Accepted Windows workflows

All eleven workflows passed against the current v15 implementation, with zero runtime/GUI/DSP errors. Individually logged results are listed below; aggregate gameplay/keyboard reports do not publish per-assertion counts.

| Workflow | Result | Evidence |
| --- | --- | --- |
| Interrupted recovery | 12 logged checks; zero errors | [Accepted report](../../player-fixtures/phase-2l-final-recovery/runtime-verification.txt) |
| Full gameplay | Aggregate workflow; zero errors | [Accepted report](../../player-fixtures/phase-2l-final-gameplay/runtime-verification.txt) |
| Physical driving / graphs | 47 logged checks; zero errors | [Accepted report](../../player-fixtures/phase-2l-accepted-driving/runtime-verification.txt) |
| Destructive downshift / continuation | 14 logged checks; zero errors | [Accepted report](../../player-fixtures/phase-2l-final-driving-failure/runtime-verification.txt) |
| Parts / conversion / service / rebuild | 41 logged checks; zero errors | [Accepted report](../../player-fixtures/phase-2l-accepted-parts/runtime-verification.txt) |
| Progression reset | 21 logged checks; zero errors | [Accepted report](../../player-fixtures/phase-2l-accepted-reset/runtime-verification.txt) |
| OS keyboard / mouse driving | Aggregate workflow; zero errors | [Accepted report](../../player-fixtures/phase-2l-accepted-keyboard/runtime-verification.txt) |
| Four-car stock durability | 12 logged checks; zero errors | [Accepted report](../../player-fixtures/phase-2l-accepted-durability-boundary/runtime-verification.txt) |
| Audio PCM / sliders | 12 logged checks; zero errors | [Accepted report](../../player-fixtures/phase-2l-accepted-audio-levels-final/runtime-verification.txt) |
| Inspection / Apply / units / wheel visuals | 19 logged checks; zero errors | [Accepted report](../../player-fixtures/phase-2l-accepted-inspection-final/runtime-verification.txt) |
| Pinned comparison summary | 10 logged checks; zero errors | [Accepted report](../../player-fixtures/phase-2l-accepted-stats-final/runtime-verification.txt) |

Native unit-button hit rectangles now use the existing root-relative client-coordinate mapping in scaled scroll panels. The acoustic bench initializes that mapping each OnGUI frame. Final mouse checks select mph, psi and lb-ft, retain the tune, and reload those preferences. Graph screenshots confirm readable live unrecorded values and continuous recorded/replayed lines. Initial failed attempts are retained in their fixture folders; this table identifies the accepted runs.

The full foundation/matrix completed before scene-test, caption and native-fixture corrections. SceneCheck and BuildPlayerOnly compiled the final sources; affected Windows fixtures were rerun. No physics/part calibration changed after the full domain gate. Acceptance metadata and final compiled assembly hash are in [acceptance.json](acceptance.json).

## Current internal calibration

| OEM car | Quarter mile | Trap km/h | PP / class | Stop from 35 m/s |
| --- | --- | --- | --- | --- |
| sport-coupe | 14.823 s | 156.7 | 430 / C | 76.24 m / 4.49 s |
| starter-hatch | 16.988 s | 134.3 | 300 / E | 80.18 m / 4.73 s |
| utility-pickup | 15.776 s | 142.0 | 373 / D | 79.04 m / 4.72 s |
| v8-muscle | 14.777 s | 162.5 | 433 / C | 75.84 m / 4.48 s |

Stock results remain stable; no arbitrary timing/rating rebalancing was needed. Scripted sequential/manual driver modes on the same OEM hardware stay within 3.3% of their assisted benchmark. Braking uses the actual shared contact/heat model.

## Requirements and evidence

| Requirement / approved change | Acceptance source |
| --- | --- |
| Modular ownership, reuse/downgrade, compatible parts, retained failures | Catalog/ownership/inventory suites; current parts matrix; native Parts workflow |
| Independent wheel/contact, mass/flywheel, coupling types, AWD, failure isolation | Contact/AWD/mass/transmission suites; native physical/money-shift fixtures; current matrix |
| Fuel quantity/consumption, None/25%/Realistic, free capped refill | Fuel/fuel-usage suites; controlled runs; native Parts/bench controls |
| Wear/degradation, thermals, liquids, top-up versus repair, local faults, clutch variants | Service/individual engine/tire/mechanical suites; actual component matrix; native bench/Parts/reset |
| Owned interrupted-run recovery and retained resource history | 2K recovery suite and actual forced kill/relaunch/repair fixture |
| Hardware-gated fixed/tunable equipment and supported EFI | Hardware/conversion/effects/inspection suites; paired EFI matrix; native Parts controls |
| Pedal ownership, E launch/shift, held W/Space, independent configuration | Actual Windows race and bench input fixtures; new graph-click ownership checks |
| Units, wheel size visuals, summaries/comparison/Apply/Revert/shared volume/reset | Existing full foundation; native Parts/reset/audio controls and bench rendering |
| Full measured graph/CSV/replay and authoring | Integrated tools gate, 215 channels, four full traces, saved scenario captures and actual graph clicks |
| Timed paid rebuilds/local replacement reservations | Individual service/transaction suites, native Parts/service flow; final pricing/time still Slice 3 |
| Stock endurance and internal timing/braking/rating calibration | Full stock durability and calibration.csv; actual four-car race/shutdown checks |

Several foundation suites regenerate their legacy phase-named CSV/report paths using the current model. The immutable per-phase ZIP checkpoints preserve original versions; the final foundation report and this acceptance record identify the current run.

## Limits and next scope

Developer acceptance verifies the authored model and executable workflows; it is not manufacturer/OEM lifetime data or human sound/design approval. Calibration uses fictional cars. Costs and rebuild times remain provisional. Final paid fuel/economy/career/idle, repeat purchases, assembly valuation and salvage remain ordered Slice 3A–3H; mobile/controller, finished art, final commercial packaging and platform validation remain their later slices. Live DSP is asynchronous; exact acoustic comparisons still use generated WAV fixtures. Old model recordings must be rerecorded.

## Checkpoint and use

- Launch `Game/Builds/Windows/DragLab.exe`; distribute the complete Windows folder with its Data folder and UnityPlayer DLL.
- [Windows checkpoint](Windows-checkpoint.zip) and [source/tools/docs checkpoint](source-checkpoint.zip) archive the accepted state. Source excludes generated Library/cache directories.
- [Full foundation](unity-verification.txt), [scene gate](unity-scene-verification.txt), [integrated tools](tools-verification.txt), [catalog matrix](../../slice-2-audit/verification.txt), [calibration](calibration.csv) and native reports above provide reproducible evidence.
- Open **DragLab → Vehicle Workbench** for part authoring and mixed-build experiments; open **DrivingTest** for live full physics or **EngineTest** for acoustic/static-RPM experiments.
- Next planned phase: **3A — repeat purchases and collection**. No Slice 3 operating-cost changes were implemented here.
