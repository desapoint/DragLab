# S05b independent final review

**Decision: PASS for the dormant S05b direct refill storage checkpoint.** Reviewed the final source against baseline `ff9102e`, the frozen S05p/S05b contracts, and the final Unity 6000.6.4f1 evidence. This approves the typed oil/coolant transaction and durable reconciliation as implemented. It does not approve routing gameplay or UI through it; S06 must provide the selected-owner binding and ordered admission/publication before activation.

## Source review

- `TypedFluidRefillIntent` is independently constructible for recovery without a successful plan. Its stable canonical bytes cover the frozen request, fences, binding identity, physics identity, and accepted tick; replay compares the complete bytes as well as the stored digest. The path remains an actual owner-supplied binding, not an inferred filename.
- The writer validates the actual path/slot/generation before opening storage, requires the prepared current schema, and starts a serialized SQLite transaction before resolving reset state and the operation/result pair. Existing receipts are resolved before new-operation freshness and quote checks. A new operation requires both durable records absent and checks the strict commit/ledger/content/physics and affected typed-row fences.
- Successful writes are limited to the economy and selected fluid rows, their integrity entries, the operation/result pair, the bounded business receipt window, and required metadata. Oldest-only eviction and monotonic append avoid rewriting the surviving history. Affected/evicted hashes are XORed incrementally. Source inspection found no JSON, full-profile capture, complete typed-row inventory, global integrity rebuild, relationship sync, or synchronous whole-state backup in the direct writer/intent transport.
- Reconciliation rebuilds from intent alone and returns only persisted result facts. Missing, inconsistent, busy, or unavailable storage remains `OutcomeUnknown`; `NotCommitted` follows a serialized proof that both operation and result are absent. The bounded business receipt is optional after eviction, and replay does not fabricate untouched fluid values or history order.
- Failure hooks cover the actual SQL statement boundaries, including each data/integrity/meta write or delete and both post-COMMIT acknowledgement points. Diagnostics now include read-query bindings as well as write bindings. Their byte definitions and exclusions are documented in the storage contract and results report.

## Evidence reviewed

Final run `77134` exited 0. The raw outputs report:

- 664 focused refill checks passed, including all 27 discovered transaction-failure occurrences and retries, replay and orphan-result outcomes, 257-entry history behavior, restart recovery after car sale/history eviction, exact pricing/clamping, reset-floor handling, and independent typed-row/hash/aggregate verification.
- 66 typed migration checks passed.
- 72 isolated legacy storage regression checks passed.
- At 0, 1, 100, and 10,000 unrelated integrity-covered rows, the command remained at 22 labeled queries, 699 returned-column UTF-8 bytes, 3,206 bound-scalar bytes, 4,636 canonical hash-input bytes, and 0 retained typed-persistence-cache bytes.

The evidence is in `reports/save-refactor/S05B/typed-refill-checks.txt`, `typed-migration-checks.txt`, `legacy-storage-checks.txt`, and `editor.log`. I checked the final focused result and the independent migration and legacy results. Earlier failed harness runs are retained and described in `S05B_RESULTS.md`; they are not counted as final evidence. Their fixture/oracle corrections did not relax production validation.

## Limits carried forward

The writer remains dormant. S06 must obtain and revalidate the actual selected save binding from the owner and serialize queued commands with live mutations, legacy saves, resets, and race checkpoints. The typed owner table does not encode an explicit parent-engine relation, and the writer does not decode legacy car JSON to reconstruct it; the frozen owner-proof and strict-CAS boundary remains the contract until S06 integration.

This is not a player/UI, Android durability, responsiveness, or visual-acceptance gate. Existing full-profile legacy projection and runtime JSON/clone retirement remain later S06–S14 work. The overall save-state refactor is not complete.
