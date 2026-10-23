# S00 integration baseline — 2026-10-23

Local main starts at `6d19aae`. No production behavior changes are part of S00. The current Windows/Android cel-shaded build is a historical functional checkpoint, not this refactor's performance baseline.

## PR #4 disposition

GitHub metadata read on 2026-10-23: [PR #4](https://github.com/desapoint/DragLab/pull/4) remains **open, draft, unmerged**, head `1ac43ccfc97e6efba865b472e50e3adc1de7eedc`, base `d69490f9dad417e0f2706c97801dcebdae5c85e4`. Local main is not descended from that reviewed head. Comparing source confirms the authored `FitmentIndex` class and detached new/used supply reuse from that PR are **absent** on main; older cached catalogue layouts and worker preparation are present. Shared history is not proof of integrated PR functionality.

Decision for this save/state refactor: **continue from current main; defer the separate market-fitment PR**. It changes authored-model lookup and requires its own matched seeded/frame validation; it does not remove global transaction cloning, state projection or synchronous save waiting. A blind merge of a draft old market branch would add a separate unvalidated variable to this infrastructure baseline. No PR is merged, closed, rebased or pushed by S00. Preserve its potential index reuse as a later explicit market checkpoint rather than claiming it already exists.

The earlier architecture document's historical merge-first recommendation is superseded for this execution by this concrete current-code disposition, subject to the S00 Luna-high review. No safety/performance gate is marked passed by that decision.

## Baseline evidence

State/storage audits and the isolated baseline harness are complete for the bounded Editor scope. The final 342-sample frozen run, 9-fixture all-table rollback probe and independent artifact validator pass. [Actual medians, hashes, failures, unavailable allocation counter and deferred coverage](../../reports/save-refactor/S00_RESULTS.md). Physical Android timings remain unavailable (ADB has no device). Luna-high review passed for this bounded S00 scope; S01 follows only after the local checkpoint commit.
