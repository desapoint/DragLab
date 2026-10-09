# 2H checkpoint

Shared car model: `draglab-wheel-drive-v8`; wheel contact core: `draglab-wheel-contact-v3`.

Unity 6000.6.4f1 full foundation/build gate: **2,149 passing checks**, UTC 2026-10-08T14:48:48.0709377Z. Includes 595 contact-core cases, 140 driving-contact checks across the four cars/gearboxes, 9 measured AWD/gear-protection cases and the established foundation. Existing shared bench suite: 120 passes. Component/AWD failures: 93 passes. All twelve OEM durability scenarios pass (ten quarter-mile runs, two-minute pull, five-minute idle per car); lowest retained health 96.9%. Starter career completes all four events with earned parts/tunes, no injected funds.

The 1,749-check/834-run matrix passed during this integration before the subsequent center-LSD implementation; the measured LSD/disconnected-shaft cases and full final foundation gate cover that follow-up. The integrated matrix will be rerun after 2J adds fuel/tire condition feedback. No claim of a final 2K/2L calibration gate is made.

Changes: true four-wheel motion/contact in the shared car; finite clutch/converter synchronization, momentum through neutral/shifts/shutdown, measured-slip traction assist, actual brake/clutch work, physical center LSD, correct wheel-imposed downshift protection, complete wheel telemetry and exact binary checkpoint numbers alongside JSON.

Fixed during verification: launch assist spinning wheels beyond engine speed; JSON rounding differences; seeded test body speed without matching wheel momentum; obsolete brake-energy and automatic-shift tire comparisons; obsolete estimated-slip CSV assertions.

Native Windows driving/input evidence is in reports/player-fixtures/slice2h-driving. The complete executable/data folder and source are archived in this folder once the native gate passes. Editor contact-workbench manual visual review remains a declared limit; all its core controls compile and use the verified solver. Fictional longitudinal gearbox/AWD calibration; no reverse/cornering model and no per-gearshaft DCT assembly model.
