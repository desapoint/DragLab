# Exact vehicle-listing validation component

Date: 2026-10-26. Scope: the optional positive certificate around the unchanged `VehicleListings.Validate` leaf. This does not activate the pending native Parts UI or complete S06.

## Evidence

- Standalone Editor85888 exits0 and passes1821 logged checks, including exact acceptance/first-error/exception parity, all enumerated public fields, null/populated clutch and AWD shapes, member-array runtime types, same-revision edits, certificate retirement, owner/catalog identity and operation-scoped encoder counters.
- Full fixture:9294 definitions,2 owned cars,1081 owned parts,59 vehicle listings and2362 listing parts. Ten unchanged-core samples: median21.362 ms, max23.9794 ms. Ten exact proof hits: median4.2927 ms, max6.8542 ms. Each hit includes the complete current authored token comparison. One-time changed/restored miss+core+store costs34.4852/35.4919 ms. These are descriptive Editor timings, not native frame acceptance.
- The original validator body is text-identical inside `ValidateCore`; an independent Python extraction checked it against HEAD before this change. UTF-8 and matching Unity metadata were checked.
- Frozen source manifest: ignored `S06H1/vehicle-proof-compile-fix-source-sha256.txt`. Standalone log: `S06H1/vehicle-proof-compile-fix-editor.log`; detailed checks/timings: `S06H1/vehicle-proof-component/`.

Full managed Editor54385 exits0 on the same frozen source. Logged totals:7 encoder,37 cache,1821 vehicle validation,81 pricing,214035 browser,13381 ready ledger,1685 base readset,1596 catalogue and37699 supply. Some counts include timing notes and bounded-loop checks. Log: `S06H1/vehicle-proof-full-editor.log`.

Windows build50844 exits0 with `Build Successful` and `DRAGLAB BUILD SUCCESS` in `S06H1/vehicle-proof-windows-build.log`. The frozen source manifest still matches after build. The later staged whitespace check removes one extra terminal blank line from the proof source and adds the review document's terminal newline; a static comparison confirms no non-whitespace change. This compiles the whole current worktree, including pending UI sources; the standalone component dependency review establishes independence from those callers.

Independent Luna-high component review is **PASS** in [the scoped review](../../docs/save-refactor/S06D2A_VEHICLE_PROOF_REVIEW.md), after source, standalone, full managed and same-source Windows evidence. Nothing in this report grants live/UI, full-S06, Android or human acceptance.

## Failed attempts retained

- Editor67293: probe7/cache37 pass, then authored `wearEnergyJ` mutation assertion fails. Test used `+0.125f` on a value with an8-unit ULP, leaving identical bits. The fixture now flips a guaranteed-different representation bit and restores the exact original; the production token was not weakened.
- Editor56627: licensing startup fails after denied system-information access; no tests execute. Exact isolated Editor is stopped and the same frozen source rerun with ordinary licensing access.
- Editor57545: new checker compile errors only (ambiguous `DriveType`, missing directory propagation into a nested helper). The namespace and helper parameter are corrected before85888. No production behavior is changed for those corrections.

## Boundaries

Only the stock-validation leaf and its ordered owned-part-ID dependency are copied into an optional typed certificate. No whole profile, owned-car graph, price table or save snapshot is cloned. A hit still requires exact current fields, null/order/runtime-type/numeric-bit equality, catalog identity and current authored content. Mismatch retires the observed certificate before the original validator runs; invalid results never certify. Cold authored content retains original validation and does not trigger synchronous warmup. Scoped encoder tests cover known instrumented boundaries, not interception of every possible serializer.

The uncommitted live Parts integration still has known native frame failures and remaining gates. All later runtime JSON/full-profile-copy conversions and S07–S14 remain required.
