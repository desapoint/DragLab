# S06d2b1h — concrete installed-build resolver prerequisite

Baseline `f22b94d`, main. Prior goal turn made progress with a reviewed local diagnostic commit. Continue the whole authorized S00–S14 refactor. High's read-only gate permits this bounded prerequisite despite retained legacy reds; it does not close Step1/S06, authorize a cart planner/UI activation or waive native/device performance gates.

## Outcome and ownership

Add a private typed concrete-installed-build input and shared authoritative resolution helpers. The input owns one selected build's facts, never a profile/catalogue mirror. New capture remains dormant: no cart/UI caller consumes it in this increment. Existing legacy entrypoints retain their public signatures and borrowed-lookup route, using shared extracted helpers with exact behavior and no additional broad copying.

| Owner | Exclusive writable paths |
| --- | --- |
| Luna medium / s06_identity_fix_medium | New Domain/ConcreteInstalledBuildInput.cs +meta; new Domain/InstalledBuildResolution.cs +meta; existing Domain/MechanicsOwnership.cs and Domain/CarInspection.cs; S06D2B1H_CORE.md |
| Luna medium / s06_projection_contract_medium | New Runtime/InstalledBuildProjectionChecks.cs +meta; S06D2B1H_TESTS.md |
| Luna high / s06_identity_high | S06D2B1H_REVIEW.md only |
| Coordinator | Frozen Editor-only legacy reference +meta; Editor runner; decisions/results/status; process/source freezes/checks/builds/Git |

Code paths are relative to Game/Assets/DragLab/Scripts, notes to docs/save-refactor. No worker runs, new save writer, UI/theme, bench, physics/audio or catalogue rule edits. Parent alone operates Unity/builds/players/Git; authors release before freezing. No Game/tools edits overlap those processes. Verify terminal status and source hashes before thaw. Preserve unrelated files, explicit UTF-8 and stable metadata.

## Input and shared authority

`ConcreteInstalledBuildInput` must encapsulate private readonly copied facts: car/model IDs; engine attachment and rebuild/job context; copied tune; exact ordered binding slot/physical IDs, including missing/blank targets; private exact typed physical part/condition/member/tire-corner copies; separate copied fluids and legacy starting fuel litres. It must retain no PlayerProfile, OwnedCar, MechanicalCatalog, borrowed record/collection, Unity object or delegate closure. No public mutable arrays/parts/tune escape; each mutable returned resolution/inspection owns its output so caller mutation cannot corrupt the captured input or later evaluation.

Capture adapter may synchronously read Profile/Car via existing InstalledPartLookup. Preserve exact null/order/type/identity/history values; no Ensure, JSON, ID generation, defaults, missing OEM synthesis or implicit healthy conversion. Retained size is proportional to installed bindings/member data; existing lookup scanning cost must remain visible and cannot be claimed proportional to selected records if it still scans unrelated parts.

New `Inspection` has an explicit post-resolution materialization precondition: absent fluids throw `InvalidOperationException("Inspection requires captured fluid state.")`, then absent fuel throws `InvalidOperationException("Inspection requires captured fuel state.")`. Capture keeps absence and `Resolve` remains available without display resources. This new-API refusal prevents downstream default simulation; the legacy public inspection route retains its original null-reference behavior. Pin both-null precedence as fluids first.

Evaluation takes MechanicalCatalog as an explicit synchronous borrowed argument, never retained or copied. Catalogue.Resolve remains the sole authored rule authority and is unchanged. This is **owning-thread only**, not a worker-safe detached catalogue/planner/freshness certificate. A later narrow authored capture and fresh fence remain required before background use or live activation.

Share the current condition/tire/context behavior through extracted helpers rather than independently reimplementing it. Preserve error/warning order and route-specific wording: MechanicsOwnership.Resolve includes rebuild/job locks; InspectionBuild.Capture has its own missing-engine/failure messages and no added lock errors. Preserve existing adjustments/replacement/removal/healthy branches of the legacy inspection adapter; no extra capture there solely to route through the new dormant input. New concrete-input methods support normal owned resolution/inspection only. Slot mutation/cart effects, pricing, sale/stock/holds, block preparation and checkout are outside this increment.

## Independent proof

Before extraction, coordinator freezes the original Resolve and InspectionBuild.Capture bodies from f22b94d into an Editor-only reference. It reuses unchanged catalogue authority but never calls the new shared helpers. This is an explicit independent wrapper reference, not a forever-frozen whole simulation. Keep the earlier pinned v1 cart trace untouched.

Five finite actual-fixture families: normalized installed car; actual compatible same-family purchase/install plus applicable member fault/history; real per-corner tires/wheels; actual detached engine with exact car fluids/fuel and contextual rebuild/job subvariants; ordered/dangling/blank/null/duplicate installed-binding edge cases with no synthesis and exact old errors where evaluation reaches them. Create disposable actual fixtures through existing operations, then never Ensure malformed variants. Compare both original public routes, refactored routes and new input routes where applicable: full spec/tune/safe limits, ordered errors/warnings, exact physical/member/condition identity, fuel/fluids, and authoritative inspection curves/stats/warnings.

Assert source, Unity RNG and entropy stability; typed source/returned-output alias isolation; no retained profile/catalogue/borrowed graphs; no JSON calls/Ensure/identity allocation in capture/evaluation by transitive source audit and diagnostic probes. Missing data is preserved or explicitly refused at the new capture boundary, never repaired; document any deliberate new-API precondition separately from unchanged legacy entrypoint parity. Do not silently run whole-profile Validate as a production capture shortcut.

Use bounded reports and explicit first-stage errors. Reference fixture copies may be broad only in labelled test isolation; production capture must not use them. Report capture/evaluation/inspection timings with setup outside the clock, calibration-gated allocations or NA, and retained binding/member counts. Do not inflate this into another full commerce matrix or infer native/device acceptance from Editor measurements.

Independent high source/evidence review precedes local checkpoint commit. Existing Parts/installed lookup regression checks and a Windows build are selected according to the actual shared-core diff; no unrelated suite sweep. All legacy JSON/source/output reds, future typed ticket/planner/caller conversion, remaining consumer refresh and full S06 gates, then S07–S14 remain active.
