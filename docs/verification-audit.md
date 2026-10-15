# DragLab verification suites

Use `tools/Run-Validation.ps1` for release checks. The raw `Invoke-Unity.ps1` action list also contains builders, authoring operations, timing diagnostics and overlapping convenience composites; running every action is not a meaningful independent test count.

| Group | Contents |
| --- | --- |
| Smoke (9) | Typed tires/wheels, markets, rival staging, checkpoints, reset, actual scene and SQLite |
| Functional (46) | Commerce, maintenance, rebuilds, pricing, component faults, mechanics, presentation and focused branch regressions |
| Extended (12) | Ownership/assemblies, paid operations, economy integration, durable recovery, performance correctness and physics stress matrices |
| All (67) | Smoke, Functional and Extended |
| Quarantined (4) | Overlapping convenience composites: PartsMarketRegression, Verify, VerifyRemaining, VerifyRemainingSimulation |
| Diagnostics (5) | Signal diagnosis, SQLite timings and menu performance measurements |

The ownership, assembly, paid-operation, economy-integration and residual-performance suites now test current-contract persistence separately from unsupported legacy contracts. Old schema 1/3/4/9 fixtures must remain unchanged with writes blocked until explicit reset; tests must not demand fabricated OEM hardware or automatic migration. Modern feature assertions run before and after those negative compatibility checks. The required static v1 fixture remains versioned.

`BranchAuditVerify` covers reset-marker archival/rollback, monotonic tire wear, exhausted-tire failure/valuation/inspection and obsolete trace rejection. `ComponentFaultVerify` checks category-specific fault permissions. `EngineRebuildFlowVerify` and `SupplyPricingVerify` cover rebuild lifecycle and stock/express charges. These are normal Functional gates, not optional diagnostics.

The suites have overlapping assertions: Assembly includes ownership/inventory checks; Integrated includes shared-tool scenarios. Report named entry points and scenario counts separately rather than adding them together as unique independent coverage. The four excluded composites add no missing unique release path; retain them for manual diagnosis.

## Execution

```powershell
.\tools\Run-Validation.ps1 -Group Smoke
.\tools\Run-Validation.ps1 -Group Functional -BatchSize 7 -ContinueOnFailure
.\tools\Run-Validation.ps1 -Group Extended
.\tools\Invoke-Unity.ps1 -Action BuildPlayerOnly
.\tools\Test-NativeMenu.ps1 -FixtureName release-navigation -NavigationOnly
```

Close the Unity Editor before batch validation. Default batches run nine entry points sequentially in one Editor process; use `-BatchSize 1` to investigate shared-static-state interference. `-StartAt` resumes a group at a named entry point. Assertions, abnormal Editor exits and missing results fail the runner; later unexecuted actions are never marked passed.

The full Slice 2 audit is expensive. When only its fixture setup changes, retain the unchanged full-matrix assertions and rerun `SliceTwoChangedVerify` for the corrected failure/AWD scenarios; report this as combined evidence, never a second complete run. `DurabilityAudit` throws when a collected scenario fails.

Each batch immediately writes PASS/FAIL, elapsed time and encoded exceptions to `reports/validation-batches/*.results.tsv`, plus a Unity log and per-run CSV. Diagnostics report COMPLETED, not feature PASS. Slow physical matrices can take minutes; batching saves Editor startup time, not simulation time.

Do not execute asset-authoring actions as verification prerequisites without an explicit content change. `Open`, `Prepare`, `VehicleVarietyPrepare`, `TireSoundBaseline`, `BuildWindows` and `BuildPlayerOnly` are not tests. A successful build is separate from test success.

For packaged UI coverage use isolated `Test-NativeMenu.ps1` fixtures and `Test-WindowsSlice.ps1`. They preserve player assets and export normal-resolution screenshots at `reports/interface-screenshots`, with alternate resolutions in `resolution-tests`. Failed focus/input is a failed check, not a visual pass.

## Current evidence

The final branch audit records exact executed gates, reruns after fixes, native player coverage, build and limitations in [the release audit](../reports/branch-audit/RELEASE-AUDIT.md). Earlier failures remain in [the original audit](../reports/branch-audit/AUDIT.md). Source review alone and historical results do not certify an unexecuted candidate.
