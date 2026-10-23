# S01 contract check matrix

The Editor-only `DragLab.Runtime.StateCommandContractChecks.RunStandalone` entry point exercises the frozen `StateCommandContracts` metadata and lifecycle rules using managed in-memory values. It does not access a player profile, `ProfileStore`, SQLite, JSON, or game state. The entry point requires Unity batch mode outside Play Mode and writes its concise raw result to `reports/save-refactor/S01/state-command-contract-checks.txt`.

| Area | Independent checks |
| --- | --- |
| Command identity | Equality binds save slot, reset generation, and ordinal operation ID; alternate slot/generation/operation and case changes do not alias. Invalid path-like slot, negative generation, and control characters are rejected. |
| Global fences | Current slot/reset/ledger/storage/content accepted; each stale or missing fence is rejected with its specific error. Ledger and storage versions reserve room for the next commit; content version accepts max-1 and rejects the reserved maximum. Reset current generation accepts max-1; future reset must reject exhaustion before commit. |
| Manifest shape | Duplicate read/write declarations, missing write read fences, mismatched versions, update of an absent record, and create over an existing record are rejected. A create has an explicit absent/version-zero read dependency and can be observed as such. |
| Read observations | Exact read set accepted; missing, extra, duplicate, null, existence-changed, and stale-version observations rejected. |
| Write observations | Declared subsets/no-op declarations accepted; undeclared records, duplicate writes, extra writes, conflicting kind/version, and null observations rejected. |
| Metadata immutability | Constructor inputs can be mutated after construction without changing the manifest; exposed read/write collections reject mutation; record keys retain ordinal value equality. |
| Durable lifecycle | Precommit cancellation is allowed without a receipt; cancellation after commit begins is blocked. Commit-in-progress may become committed, rolled back, or outcome-unknown; unknown outcomes require matching identity and exactly-next ledger/storage receipt to reconcile. Missing, mismatched, or inappropriate receipts and terminal-state reversals are rejected. |

This is a contract-level adversarial matrix, not a gameplay command integration test, persistence adapter test, or performance result. Operation ID metadata does not itself prove payload equivalence on reuse; the future durable writer must bind IDs to stable typed intent. Profile payloads and typed write application remain later checkpoint responsibilities. The coordinator added the separate Editor-only `DragLab.Editor.StateCommandCheckpointVerification.Run` wrapper: it runs this contract suite, then the existing `SqliteStateVerification.Run` with two authored vehicles and all shared reset/recovery/failure cases in a report-local isolated folder. It does not overwrite the pre-existing sqlite-state report. The contract suite itself stays storage-free.

Executed by the coordinator in Unity 6000.6.4f1, Windows target, batch mode: **75 contract checks and 72 existing isolated storage checks passed**, exit 0. The contract author did not run Unity; this result is coordinator integration evidence. Exact command and limits are recorded in `reports/save-refactor/S01_RESULTS.md`. Combined invocation:

```text
unity run "<repo>/Game" --timeout 900 --log-file "<repo>/reports/save-refactor/S01/editor.log" --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.StateCommandCheckpointVerification.Run
```
