# Slice 2 audit — 2026-10-08

The audited starting scope is completed phases 2A–2E plus the requested provisional wear, cooling, AWD, difficulty, equipment, inventory, tuning and inspection extensions. This is not a claim that planned 2F–2L were already implemented.

## Baseline and corrections

Baseline matrix: **834 simulation runs, 1,745 checks, 14,425 simulated seconds**. Every compatible catalog replacement is resolved and exercised; combinations cover all four cars, five gearbox selections (factory plus manual/DCT/automatic/CVT), six induction selections, all three wear modes, idle/staging/neutral blips/mixed throttle-and-brake, installed-part failures, tire radius extremes, two flywheels and all six AWD modes on the authored AWD car. Checks collect failures rather than stopping on the first car. Unsafe modifications may fail physically: absence of failure is not the assertion for every boosted configuration.

Sixteen baseline checks failed: twelve internal-upgrade names still used generic option labels, and neutral blips heated/wore stationary downstream driveline components on all four cars. Corrected names describe factory/reinforced material and function. Gearbox/driveline heat now follows transmitted shaft power, rather than unrelated combustion power while uncoupled. Failed differential flags override positive health when evaluating coupling capability. Existing internal failures do not automatically destroy healthy other internals; None wear cannot create collateral damage.

Shop descriptions now identify removal consequences as **If removed**, rather than incorrectly describing installed parts as missing. Controller wording distinguishes compatible preload/anti-lag from separately purchased limiter/two-step/shift-light modules. Human-readable category/topology labels retain stable part IDs and inventory compatibility.

The existing broad inspection mouse fixture was also diagnosed: cached GUI-to-screen rectangles had an offset inside scaled nested scroll panels. It missed the short preview button. Development test handles now convert relative to the same GUI event's root coordinates; Windows native mouse coordinates remain unchanged. This corrects test targeting, not a claimed simulation defect.

## Verification evidence

- Existing foundation gate: **1,337 checks pass** on Unity 6000.6.4f1, model `draglab-audited-stress-v6`.
- Final expanded matrix: **1,749 checks / 834 runs / zero failures**, including four pre-existing-failure/None-wear cases.
- Actual Windows inventory workflow: 10 checks, zero runtime/GUI errors; `player-fixtures/slice2-audit-parts-mouse/`.
- Actual Windows inspection workflow: 14 checks, zero runtime/GUI/DSP errors; previews, draft/Revert/Apply, real race build, direct buy/install and pressure/speed unit persistence pass in `player-fixtures/slice2-audit-inspection-root/`.
- Original failing evidence remains in `slice-2-audit/baseline/` and the earlier inspection fixture folders. Do not count those attempts as passes.

## Scope and limits

Catalog ratings are fictional authored game data. A larger fuel or ignition system raises supported-power capacity, not horsepower below that capacity. Forged internals add strength rather than power. Cooling changes heat transfer/rejection rather than arbitrary RPM/torque ratings. Exhaust options change acoustics, not invented horsepower. Differential torque allocation depends on surface, topology and health. Body/interior/accessory removal and retained inventory identities are distinct.

Independent wheel angular/contact dynamics, live fuel consumption and compound-specific tire life remain the next requested 2F–2J work, with separate checkpoint evidence. Full wear/interruption calibration and final all-system sign-off remain 2K–2L. Automated tests cannot certify real-car accuracy, subjective engine timbre, physical controller feel or Android device behavior.

Matrix files: [checks](slice-2-audit/verification.txt), [runs](slice-2-audit/runs.csv), [part ratings and output](slice-2-audit/parts.csv).
