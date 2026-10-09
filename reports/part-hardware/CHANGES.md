# Installed hardware and tuning

Added **128 definitions (32 per car)**; the catalog now contains 487 definitions with all existing stable IDs retained.

- Three physical turbo sizes: compact 0.75×, street 1.00×, large 1.40× reference. Single turbo, twin turbo and twincharged layouts each offer fixed-spring or adjustable-controller versions: 18 choices per car.
- Single/twin superchargers with fixed drive or interchangeable pulley sets: four choices per car.
- Heavy/balanced street flywheels; lightweight/endurance cooling; organic/race friction clutches; touring tires; endurance brakes; fixed short final-drive and interchangeable-gearset gearboxes: ten choices per car.

Size changes the shared turbo spool response, boost RPM envelope and per-unit airflow limit. Bigger does not guarantee higher peak HP: airflow headroom, delivery limits and the engine curve still matter. The CSV includes equal-peak-HP examples with different response and high-RPM torque.

Fixed turbo kits use 0.80× reference boost and a recirculation valve. Adjustable kits permit 0.80–1.20× boost above their spring floor, plus valve setup. Adjustable twincharged kits also include pulley adjustment. Fixed street blowers use 0.90× drive; pulley kits permit 0.85–1.20×, without an independent electronic boost knob.

OEM final drive is fixed. Existing upgraded gearboxes keep interchangeable-gearset access. New short fixed boxes set 115% of stock ratio (maximum 6:1); new gearset kits permit 2.50–5.50. Selection is a garage setup requiring Apply. More expensive/higher-stage hardware does not automatically unlock tuning.

Clutches separately change transferable torque and sliding-work life. Cooling changes transfer, heat rejection, thermal buffer and mass. Flywheels change inertia/neutral decay. Touring tires trade grip for life. Endurance brakes add heat buffer, life and mass. These fixed parts unlock no tuning knobs.

Definition capability flags/ranges are reconstructed by the resolver from installed identities. Draft publication and race/snapshot clamping enforce fixed values and ranges; presets cannot bypass them. Swaps sanitize incompatible knobs. Existing programmable limiter/two-step/shift-light capabilities remain separate.

Shop and physical bench show tuning permissions. Physical bench has the same parts and hardware controls, independent of pedal ownership. Raw sound/engineering experiments remain unrestricted; derived catalog permission fields are hidden there.

Existing progression, inventory identities, wear, retained parts and unit/audio preferences remain supported. Old free size edits use the fitted turbo's size; old final-drive edits on fixed boxes use that box's gearing. Physical replay model: `draglab-part-hardware-v12`; older captures need fresh recording. Prices remain provisional until Slice 3. 2K–2L remain pending.

Evidence: `verification.txt` (1,159 focused checks), `turbo-curves.csv`, `catalog.md`. Full Windows build verification passed **3,764 foundation checks** at 2026-10-08T17:34:26.9391683Z. The final player was rebuilt after a cosmetic aftermarket-quality label and native fixture coordinate correction.

Final native Windows checks: **100 passes**, zero runtime/GUI/DSP errors:

- `reports/player-fixtures/part-hardware-shop-corrected`: 20 shop checks, actual OS buy/install/preview, fixed versus adjustable slider visibility, boost draft/Apply into real race and OEM reuse.
- `reports/player-fixtures/part-hardware-tuning`: 19 inspection/tuning checks, actual mouse Apply/Revert, visible tire changes, stat/dyno comparison and persisted units.
- `reports/player-fixtures/part-hardware-sound-config`: 28 raw sound-bench checks, configuration/type/selector/mix edits while held W retains pedal ownership.
- `reports/player-fixtures/part-hardware-physical`: 33 physical-bench checks, keyboard/mouse shifts, fuel/mass/tire edits, neutral flywheel momentum, exact scenario replay and isolated export/import.

The first two shop probes exposed a test-coordinate offset in scaled scroll groups: the automated mouse hit Apply below the intended boost slider. Tuning control records now use the established root-relative conversion shared by the parts controls. The corrected probe measures a real 0.80 → 1.16× boost draft before Apply. Earlier failed fixture logs are retained. Final fixed-turbo and boost-preview screenshots were visually reviewed.

Values, relative sizes, fitment and prices are fictional calibrations; measured compressor maps are future work. The focused catalog pulls test healthy parts with wear disabled; long abuse/recovery and final calibration remain the separate 2K–2L gates.

Qualitative sizing follows [Garrett's guidance](https://www.garrettmotion.com/racing-and-performance/choosing-a-turbocharger/turbo-system-optimization/). Spring floors follow [Turbosmart's controller explanation](https://support.turbosmart.com/portal/en/kb/articles/what-is-the-purpose-of-a-boost-controller). Numerical ranges here are game-authored.
