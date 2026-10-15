# DragLab verification-suite review

**Status: source-reviewed, not Unity-runtime certified.** Updated against the per-tire-authority branch after the October 2026 tire polymorphism consolidation. Tests must still be executed in the installed Unity Editor to prove their assertions. Do not treat a passing compilation as a passing test.

## Why the previous 69-action list was inappropriate

The raw `Invoke-Unity.ps1` action list mixes isolated functional checks, large composite suites that rerun other checks, old migration fixtures, timing measurements, asset generation, and Windows builds. Treating all of these as 69 independent release gates exaggerates coverage, wastes time, and obscures actual failures.

Use `tools/Run-Validation.ps1` to select one of these groups:

| Group | Purpose | Release gate? |
| --- | --- | --- |
| Smoke | Tire polymorphism, wheel/setup flows, market, rival staging, checkpoints, reset, scene, SQLite | Yes, after runtime results |
| Functional | Individual domain, commerce, career, driving, presentation and mechanics scenarios | Yes, after runtime results |
| Extended | Heavy stress and special-case physics/economy checks | Yes, after runtime results |
| All | Smoke + Functional + Extended, sequential, no known duplicate composite actions | Yes, after runtime results |
| Quarantined | Old-save migration assumptions or aggregate composite suites | **No**; review/rewrite separately |
| Diagnostics | Timings, UI optimization or signal diagnosis | Informational, not PASS criteria |

The six setup/build/baseline operations (`Open`, `Prepare`, `VehicleVarietyPrepare`, `TireSoundBaseline`, `BuildWindows`, `BuildPlayerOnly`) are deliberately not grouped as tests.

## Confirmed and corrected stale assumptions

- **Standalone test catalog:** `TireFailureVerify` built a V8-only catalog but indirectly generated all vehicle listings, throwing for `city-three`. Fixed by supplying the full catalog; the user subsequently reported `TIRE FAILURE PASS: 5`.
- **Serialization check:** `AxleWheelVerify` searched the full JSON for `"tire"`, inadvertently matching the **value** of `role`. Corrected to check the `"tire":` property. The next Unity failure progressed beyond this assertion.
- **Typed tire condition:** revised direct test mutations in `AxleWheelVerification`, `IndividualEngineVerification`, `PartsMarketVerification`, and `ValuationVerification`. Tire wear belongs to `TireCondition` members; the `ComponentCondition` parent is a neutral shell. Re-normalize the shell when directly editing fixtures.
- **Current schema fixtures:** removed old version-7 and version-8 migrations from market/cart reservation checks and version-5 migration from career expansion verification. Replaced them with assertions about current save normalization and empty career progression.
- **Diagnostic assertions:** `AxleWheelVerify` saved-setup restoration now tests the rear tire fitment separately from the ownership invariant, including the actual failing value in the message.
- **Asset authoring isolation:** career rivals, expanded career, variety vehicles, used-car inspection, garage business, economy balance, and paid-operations verifications no longer silently call `Prepare()`. They report missing prerequisites; authoring has to be explicitly requested from the Unity menu or dedicated preparation action.

## Quarantined tests requiring additional changes

- `OwnershipVerify`: asserts backup/reconstruction from a v1 save fixture.
- `AssemblyVerify`: includes v1 migration tests.
- `PaidVerify`: includes older v4 save migration.
- `EconomyIntegrationVerify`: asserts old v1/v4 save migration.
- `ResidualPerformanceVerify`: asserts v9 file migration.
- `PartsMarketRegression`: composite convenience gate that reruns `PartsMarketVerify`, `VehicleMarketVerify`, `CartVerify`, `InventoryCommerceVerify`, `PaidVerify`, `AssemblyVerify`, and shop/setup preview checks; not an independent test.
- `Verify`, `VerifyRemaining`, `VerifyRemainingSimulation`: composite legacy foundation gates, substantially rerun standalone tests; `VerifyRemainingSimulation` includes `OwnershipVerification.Run` with old migrations.

These are **not deleted** because their modern portions can still be valuable. Their obsolete migrations should be removed or moved to separate archived compatibility tests before they can return to the release gate. Unreleased save compatibility is not a current product requirement, but unsupported schema rejection and the new reset-dialog behavior remain relevant.

## Run policy

1. Pull the latest `refactor/per-tire-authority` commit and close Unity.
2. List the curated smoke tasks: `.\tools\Run-Validation.ps1 -Group Smoke -ListOnly`.
3. Execute `.\tools\Run-Validation.ps1 -Group Smoke`; it fails fast, prints the exception text, and creates a per-run summary CSV in `reports/`.
4. Diagnose the exact assertion before changing logic. For `AxleWheelVerify`, note whether the rear specifications or `MechanicsOwnership.Validate` fails.
5. Only after smoke results are green, proceed to `-Group Functional`, then `-Group Extended`. Avoid the quarantined composites unless specifically testing compatibility. Asset-authoring prerequisites must be prepared deliberately; a test no longer generates or modifies these fixtures in order to pass.
6. Use in-editor manual validation for race staging, post-race tire wear, profile persistence, and the UI Toolkit reset dialog.

**Runtime status at review time:** `TireVerify` and `TireFailureVerify` passed in the user's Unity installation before the latest fixture cleanup; `AxleWheelVerify` is still unconfirmed after the latest changes. No automated claim is made for unexecuted actions.

## Batched Unity execution

The curated validation runner now launches **one Unity Editor process per batch of nine actions** by default, instead of reopening the Editor per check. The shared entry point executes verification methods **sequentially**; this does not make simulations multithreaded. Smoke uses one Unity launch rather than nine. All 57 curated actions need seven launches instead of 57 if no test stops early.

Examples:

- `.\tools\Run-Validation.ps1 -Group Smoke` — one Editor launch for nine checks.
- `.\tools\Run-Validation.ps1 -Group Functional -BatchSize 6` — smaller batches limit shared-static-state and memory risk.
- `.\tools\Run-Validation.ps1 -Group Smoke -StartAt AxleWheelVerify` — resume at a previously failed action.
- `.\tools\Run-Validation.ps1 -Group Smoke -BatchSize 1` — isolate failures in separate Editor processes.
- `.\tools\Run-Validation.ps1 -Group Smoke -ContinueOnFailure` — explicitly continue after assertions, acknowledging possible shared state.

Each batch writes `reports/unity-validation-*.log`. Unity immediately appends individual PASS/FAIL, elapsed time and exceptions to `reports/validation-batches/*.results.tsv` after every action. PowerShell converts these into a CSV summary. If Unity crashes, recorded passes remain and only the first unfinished action is failed; later actions are not marked executed. Exit codes remain failures if a test fails.

Standalone `Invoke-Unity.ps1 -Action ...` calls still work. Use `-BatchSize 1` when diagnosing cross-test interference. Batching minimizes startup overhead, not the cost of running physics scenarios. **Unity execution of the new harness remains unverified** until tested on the installed Unity Editor.
