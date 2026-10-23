# S01 dormant transaction contracts

Baseline: `f163454` on local main; S00 baseline `39d09b7`. Coordinated Luna medium implementers and independent Luna high review. No worktree or remote changes.

## Implemented boundary

Immutable, bounded metadata now describes slot/reset/operation identity, expected ledger/storage/content fences, record existence/version reads, and declared create/update/delete writes. It rejects missing creation fences, stale dependencies and undeclared writes. A receipt must identify the same operation and exactly the next ledger/storage versions. Cancellation is precommit only; an uncertain durable result requires reconciliation rather than being reported as rollback. The required future ownership rule is that main captures and publishes; workers may receive only affected owned records. S01 defines metadata and does not activate that state owner.

The boundary is dormant. No gameplay caller, save schema, active state authority, simulation, audio or UI changes. `ValidateCurrentFence` is the minimum pure boundary; a second no-op storage adapter was intentionally unnecessary. New contracts contain no profile payload, serializer, whole-profile copy, task or database connection. Existing runtime JSON/cloning dependencies remain open migration work.

Existing SQLite already stores reset generation, commit and ledger fences, but there is no generic durable operation receipt or per-record version provider. Later activation must provide those and bind operation ID to typed intent; receipt metadata alone cannot prove retry equivalence. Physics identity must be captured separately for physics-dependent commands. Transition rules validate metadata only; authoritative storage evidence is required to resolve an unknown outcome as rollback.

## Checks and corrections

- Unity 6000.6.4f1 batch, Windows target, no graphics, no Play Mode. Command: `unity run <repo>/Game --timeout 900 --log-file <repo>/reports/save-refactor/S01/editor.log --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.StateCommandCheckpointVerification.Run`.
- Contract checks: **75 passed**, including exact stale fences, absent creates, defensive input ownership, bounded lazy inputs, max-version boundaries and invalid receipt/lifecycle cases. Raw: `S01/state-command-contract-checks.txt`.
- Existing isolated storage regression: **72 passed**, two authored vehicles plus shared migration, interrupted reset, future-schema refusal, backup recovery, checkpoints, settings isolation and injected save-failure cases. Raw: `S01/legacy-storage-checks.txt`. Combined Editor invocation exited **0**.
- Static scope: new dormant contract is referenced only by the new test harness. Existing production files are unchanged. Test-only Editor wrapper routes existing SQLite checks into its own report-local fixtures and preserves the pre-existing sqlite-state report.
- Pre-execution source review fixed the new test's misplaced `UNITY_EDITOR` closing directive, avoiding a non-Editor unmatched brace. Version-limit documentation now distinguishes incremented ledger/storage values, nonincrementing content, and reset exhaustion. No failed runtime test is hidden by these static corrections.

No packaged platform build or phone test is required to characterize dormant metadata; no UI change or visual acceptance claim. Actual scoped command/save performance, durable receipts, crash-safe worker ordering, per-record ownership versions, all runtime JSON removal and phone latency remain later gates. S01 makes no responsiveness improvement claim.

## Independent review

Luna high **PASS**, recorded in `docs/save-refactor/S01_REVIEW.md`. Local commit records this checkpoint; S02 begins only after that commit is verified.
