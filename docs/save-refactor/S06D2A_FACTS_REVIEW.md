# S06d2a typed base-facts support — independent review

**Review result: PASS for the dormant typed base-facts support checkpoint only.** Reviewed the frozen implementation and verifier after the corrected Editor run `80013` (Unity Editor exit 0) and Windows build `16707` (batch exit 0). This is a source/correctness review of an unused support component, not acceptance of live UI integration, frame responsiveness, nonempty-cart preview, or completion of S06.

## Source review

- `NativePartsBaseReadSet` captures only the supported Shop, Inventory, and Installed scopes. Unsupported, empty, null, and wrong-case page names fail closed after the main-thread guard. Capture is sliced by item count and soft time; wrapper/setup and indivisible work are timed, while `TryFinish` performs the final exact same-turn comparison.
- The component retains the live profile only in this owner-thread read set. It does not publish a worker DTO or call a worker. Existing `InspectionBuild.Capture` and `CarInspection.PrepareCar` remain main-thread work; only the previously established detached `CalculatePrepared` math boundary is eligible for worker execution.
- Comparers cover the scoped tune, AWD, fuel/fluid, installed, rebuild, part/member, tire, unit, stock, dealer, reservation, cart, and supplied-price facts. Numeric state uses exact bit comparisons; ordered collections, null-versus-empty shapes, runtime member types, and source-object identity are retained where required. Shop keeps unrelated owned assets to ownership/member IDs and does not clone their wear history.
- The authored token requires the ready canonical snapshot and pins its identity, groups, alias map, original IDs and semantic definition copies. Validation performs typed comparisons and does not call `Key`, JSON encoding, or `JsonUtility`. The implementation has no full-profile copy, runtime reflection, normalization, or state write in this path.
- A nonempty cart is represented exactly and sets `RequiresTypedPreview`; it does not certify or compute planned results. The legacy full-profile cart preview remains an explicit S06 dependency. The full S06 responsiveness gate, including nonempty-cart latency, remains open; S07 must not start on the strength of this support checkpoint.

## Evidence reviewed

Editor run `80013` passed with exit 0: **1,694 base-read-set assertions and 7 timing notes**, **1,615 ready-content assertions and 2 timing notes**, and **37,707 supply assertions**. The base checks include real 1/5/20-car rosters with 39/209/807 parts, 100/684/2,153 typed members, stale mutations, all three page scopes, typed side-effect stamps, unsupported-page rejection, null positions/collections, and no extra canonical encoding. Windows batch build `16707` exited 0 (`S06F1/build-windows.log`). No fresh Android build or device verification was run for this checkpoint.

Reported descriptive helper timings for 1/5/20 cars were 9.669/10.056/11.703 ms pure capture, 0.224/0.242/0.233 ms largest capture step, and 3.967/4.272/5.675 ms final comparison. The 9,294-definition full-catalog final validation was included in those page timings; the separate 177-definition small-catalog check reported 0.225 ms. These are verifier timings with no pass threshold; they do not measure native browser publication, complete card readiness, or player-visible frame duration. The separately labeled harness wall time includes assertion-callback overhead.

## Remaining gates

Continue the authorized S06 work: integrate bounded read-only preview facts for nonempty carts, connect the base facts to bounded browser/card publication while preserving warning/action/draft/navigation fences, and measure the full native response path and all affected frames. Keep technical completion separate from user-facing UI acceptance. S00–S14 remains the overall authorized objective; this review closes only the dormant base-facts support step.
