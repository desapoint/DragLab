# S06b independent review

## Verdict

**PASS for the S06b owner and persistence-fence checkpoint.** Reviewed final source against `S06B_DECISIONS.md`, `S06B_OWNER.md`, `S06B_STORAGE.md`, the owner test matrix, and the final raw Unity results. This pass does not approve S06c or the full S06 gameplay/UI acceptance gate.

## Evidence

Final Unity 6000.6.4f1 run 81172 exited with code 0. The focused selected-owner suite passed 83 checks, typed migration passed 66, and isolated legacy SQLite storage passed 72. Raw evidence is in `reports/save-refactor/S06B/owner-checks.txt`, `typed-migration-checks.txt`, `legacy-storage-checks.txt`, and `editor.log`; the run summary and corrections are recorded in `reports/save-refactor/S06B_RESULTS.md`.

## Reviewed invariants

- Validated load metadata comes from the existing read transaction; write receipts use the actual values written and are exposed only after COMMIT. No follow-up metadata query or schema change was introduced.
- Selected-owner proof is main-thread-only and binds the exact profile reference, slot, canonical path, selection epoch, profile revision/storage token, and durable receipt. Cached admission performs no SQL or JSON conversion by source review.
- Owner leases are acquired before command-factory execution and remain fenced through in-flight, unknown, and unhandled durable results. Legacy save/reset/archive, selection/load, and checkpoint entry points refuse before work while leased. Preferences remain separately writable.
- Async legacy save worker data and results contain no live profile or owner registry. Completion validates the captured selection and candidate, retires only the captured checkpoint path, and keeps durable success truthful after post-COMMIT diagnostic failure.
- A merged checkpoint or any checkpoint warning withholds owner proof. The focused corrupt-checkpoint test verifies that payload and base resources remain unchanged.
- Legacy dirty-candidate comparison directly checks the captured economy, fluid, and receipt values in capture order with exact fluid bits. It is linear over cars and detached engines; owner reordering conservatively invalidates proof.

## Limits carried forward

The comparison's immutable-row and item accessors still make bounded array copies; this is not an allocation-free claim. Parts/hardware changes outside the captured typed subset are not detected when they bypass revision discipline. S06c must fence live actions and revalidate the planner read set before publication. Admission's no-SQL claim is source-reviewed, not measured by a query observer. No UI route, player/mobile build, visual acceptance, or responsiveness gate was exercised.
