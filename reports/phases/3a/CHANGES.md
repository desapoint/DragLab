# 3A — collection checkpoint (2026-10-08)

Repeat purchases and selection now use unique owned vehicle identities. Duplicate cars retain independent installed hardware, fuel, fluids, tunes, presets, condition and records. Purchases are atomic and charge provisional catalog prices; only the initial gifted starter is free. Selection/purchases reject unsettled run receipts.

Four new fictional OEM configurations expand the roster to eight: Sprig 1.2 (three-cylinder FWD), Tempest AWD 2.0T (DCT/center LSD), Meridian GT 3.0 (inline-six RWD), and Bison R 5.0 SC (injected supercharged V8/converter automatic). Their current art reuses existing layered body/wheel/brake placeholders. Bore metadata makes their component fitment explicit. The original four race baselines remain unchanged.

399 targeted collection/ownership/inventory checks pass; all eight stock quarter-mile and full pull/shift/coast/brake scenarios pass without mechanical failures. Calibration: calibration.csv. SceneCheck and Windows build pass. The native phase-3a-collection-diagnose fixture passes 12 explicit checks, including actual OS mouse purchase/selection, a real race on the second copy, save reload and purchase of all four new configurations; zero runtime/GUI/DSP errors. The first native attempt missed a selection click; the final fixture explicitly asserts selection before and after the race and passed. Screenshots were reviewed.

Player schema remains 3; shared simulation model remains v15. The copied unity-verification.txt is the PRIOR 2L 8,877-check baseline, not a newly rerun 3A suite. This checkpoint's current gate is verification.txt plus scene/player evidence above.

The complete Windows player and source/tools/docs are archived separately before 3B. Fuel/operating prices, final valuations, salvage and full career/idle balance remain later Slice 3 phases. Android devices, commercial balance and final artwork are not validated by these automated gates.
