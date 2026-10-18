# Starting variety and onboarding - 2026-10-18

Requested checkpoint: expand distinct models and starter choices, add independent used-component wear, increase initial funds, restore opening first-win bonuses, increase seeded used-parts selection, and introduce preset/market onboarding.

## Content and starter checkpoint

25 distinct model families / 52 authored configurations, at least two trims per model. Existing IDs and assets are retained. New models vary displacement, torque curve, drive layout, mass, weight distribution, body type, gearing and budget. They currently reuse the existing vehicle artwork families; these are new mechanical models, not 18 newly illustrated bodies. See roster-matrix.csv.

A new save receives three distinct models from a 15-model low-budget pool, selected from its persisted initial seed. Reloading/browsing preserves the shortlist, exact part identities, condition, mileage and price. Existing starter selections and owned cars are not rerolled. Initial cash is $6,500, with most options leaving maintenance/upgrade funds; the serviced Sable costs nearly the entire budget. No career outcome is forced.

Generated used condition is no longer clustered near an overall average: component selection mixes older, intermediate and recently replaced hardware. Repeated components can differ individually. Every generated starter contains a component/member near 30-40% and another above 90%. Starter engines are inspected/serviceable and the replacement clutch is disclosed; worn chassis, cooling/exhaust/brake/tire equipment and individual engine members remain visible. The near-budget serviced option retains a stronger engine. Regular used cars retain concealed-history/fault and variable-fluid rules and can be worse purchases. New generation rules never normalize or heal existing assets.

New-player defaults are Shift myself / Simulation / Normal market. The two presets apply first, then the independent market choice. Existing players and progression resets retain saved preferences. Early first-clear bonuses are $150 / $225 / $300 / $400; repeat rewards and later-event bonuses retain their prior pacing. Captured race terms and previously recorded wins are preserved.

Validation and remaining UI/market work are recorded as each checkpoint passes. User visual/phone acceptance is separate from automated checks.

Content checkpoint checks: Unity compilation and 360 roster/32-seed/actual-race assertions pass, including all 15 starters, wide per-component/member wear, full-wear clean passes and a real opening win for the near-budget Sable. The deterministic clean-driver fixture uses automatic shift, traction and stall assistance with full wear/consumption/condition rules; it is an achievable benchmark, not an override of player difficulty or a guaranteed outcome. First-win settlement checks: 164 pass, including repeat-win/captured-term consistency. Existing UI tests are updated in the following onboarding checkpoint. No packaged build claimed at this intermediate commit.

## Market checkpoint

Select 24-44 distinct new products globally before rolling full-set quantities. After fitting top-ups, generate 12-24 more distinct used products than stocked new products, sampling without replacement from wearable products; member batches can still create multiple physical rows. Normal/Easy guarantees remain 5/10 fitting choices collectively across owned cars in each pool; Hard has no fitting guarantee but still gets global used supply. Independent product/quantity streams, 0-6 engine offers, maximum-compatible set quantities, 5-10% express fees, nine-race cart reservations and synchronized three-race refresh are retained. Existing current stock changes only at its next scheduled refresh. The wider catalogue supplies additional engine/platform-specific definitions without duplicate equivalent new listings.

Market verification: 1,455 assertions pass across all authored vehicle configurations and 32 seeds, including used variety exceeding new variety, fitting guarantees, quantities/engine topology, exact identity transfers, shared aliases, stock/delivery accounting and refresh behavior. See market-verification.txt.

## Onboarding checkpoint

A two-step shared native dialog asks Driving and Simulation presets, then Market difficulty. Next/Back retains drafts; only Apply & choose car commits preferences and completion together through the existing recoverable preference journal. X dismisses without applying. The completion flag lives in the separate settings file, is preserved by subsequent settings saves and progression resets, and does not change the SQLite schema. Existing owned garages skip onboarding. The starter seed/list, money, stock and progression are not rewritten by this settings-only action. Old preferences without the flag remain readable.

Packaged onboarding checks: 15 pass, including a forced locked-file failure with no published changes, successful retry and exact database/stock preservation. The initial touch regression: 171 pass, including onboarding, starter purchase, fuel service, safe-area resizing and multi-touch race controls; final-build reruns follow. Preset/market screenshots were visually inspected at 1280x720 and use the existing dialog, joined choices, corners and action styles. Screenshots remain in the local interface-screenshots gallery with the normal set at its root and alternate sizes in resolution subfolders.

## Final regression scope

The complete seven-gate batch passes: authoring, starter matrix (360 checks plus 164 reward consistency assertions), all factory builds/passes (152), market/ownership/pricing (265), inspection/privacy (155), progression reset, and SQLite (765 across all 52 configurations, including slot independence, recovery and migrations). See regression-summary.txt. The separate full parts-market gate passed 1,455 assertions.

An actual seed-1 Cinder can be purchased, fitted with gearbox/clutch/intake/body changes, stripped of optional cabin mass and launch-tuned using $6,500 starting funds. Four real opening wins within the bounded test retain wear/fluids/stock and leave $942. No money, win or performance override is used. A blind four-part strategy in a different worn Juniper did not win; starter selection, service and tuning matter. The near-budget Sable won its separate stock benchmark. These are achievable automated-driver examples, not guaranteed human results. The historical complete-career robot now explicitly selects the seeded Cinder and automatic controls; its opening segment was rerun, while the complete ladder ledger remains a retained historical baseline.

The expanded roster exposed old test assumptions about a fixed Juniper, $4,700 cash, and every compared car having different gearing. Fixtures now select real seeded offers, use $6,500 and compare the actual stat definitions; identical gearing stays hidden under Differences only and retains its actual disclosure state. Native comparison behavior was preserved.

starter-wear-matrix.csv records component-set summaries from a read-only view of an actual saved starting market. Repeated-member outliers are additionally covered by the typed domain checks; the set summary is not every member's condition.

## Final packages and review gate

Final packaged suites pass: 270 dealership checks and 171 touch/mobile checks, zero runtime errors. These cover fresh onboarding, settings-write failure/retry, starter purchase, retained condition, fitted members, filtering, both gearing comparison cases, purchase rollback, 1080p/4K reflow, touch servicing, safe areas and race controls. A separate relaunch check confirmed completed onboarding stays closed. A reused deterministic-capture fixture failed its repeat-purchase assertion during concurrent packaging; the cause of that reused-fixture-only failure was not proven. The acceptance run used a fresh isolated save after all build activity finished and passed. Product purchase and ownership checks also pass the domain market gates.

Windows and ARM64 Android builds succeed. APK signing, CRC, SQLite payload and 16 KiB ZIP/ELF alignment pass; Android packaging preserves the tested Windows runtime hash. See build-manifest.txt and packaging-verification.txt. Updated local artifacts: Game/Builds/Windows/DragLab.exe and Game/Builds/Android/DragLab.apk. No phone is attached; actual device review remains pending. Verified build-only Unity serialization was archived locally and restored.

Review: start a new game to see three different seeded choices, inspect individual wear, step Back/Next through onboarding and confirm Simulation + Shift myself + Normal market, then review the larger used-part pool. Existing saved offers/progression/preferences are preserved; supply changes apply on the next normal refresh. Current new models reuse existing art, so mechanical/content variety is complete for this checkpoint while distinct new car artwork is deferred. Technical completion is separate from user acceptance; no later interface phase is started.

Pre-existing maintenance-batch/verification.txt, shop-inventory/cart-verification.txt, .codex-remote-attachments/, Game/Assets/Resources.meta, Tests.ps1 and reports/race-exit-regression/ were preserved and excluded from this change.
