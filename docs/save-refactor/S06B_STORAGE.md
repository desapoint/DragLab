# S06b storage metadata plumbing

## API

`SqliteStateOwnerReceipt` is an immutable storage-only value with `CommitId`, `Generation`, `LedgerRevision`, numeric `ContentRevision` and `PhysicsIdentity`. It contains no profile, Unity object, selected-slot or owner-context reference.

`SqliteStateLoadResult.OwnerReceipt` is captured during the existing validated `ReadState` transaction. The read verifies schema, integrity, typed economy and relationships first, then reads the commit, generation, ledger, content and physics metadata before committing that same read transaction. Ledger and numeric content revisions must parse as nonnegative integers. No follow-up query is added. A fully validated empty bootstrap has no typed economy row and returns a null receipt; a populated state missing or containing invalid durable ledger/content/physics metadata is rejected instead of receiving fabricated values.

`SqliteStateStore.Snapshot.OwnerReceipt` is null until `WriteLegacy` has returned successfully from SQLite `COMMIT`. It is then set from the exact metadata values written in that transaction, including `nextCommit` and the incremented generation for a reset. Content revision is parsed and validated as a nonnegative integer before writing. No readback SELECTs are added. The snapshot's existing `committed` field is also populated from that durable commit. A pre-COMMIT failure leaves the receipt unset. Diagnostic observer failure after COMMIT is reported through `BackupWarning` and cannot turn a durable save into a false rollback result.

## Invariants retained

- No schema or migration change; migrations 001–003 and their checksums remain untouched.
- Load continues through the existing integrity, relation, migration, recovery-floor and reset-intent checks.
- Write continues through the existing strict commit/revision CAS, typed projection, aggregate integrity update, checkpoint clearing and reset intent/floor handling.
- The load receipt uses validated metadata read from the current transaction. The write receipt uses the exact commit/generation/ledger/content/physics values written by that transaction. It does not infer a generation from a path, default an ordinary populated save to generation zero, or add post-write metadata queries.
- Empty bootstrap cannot prove a selected typed owner.

## Verification boundary

Parent-run Unity6000.6.4f1 verification passes83 selected-owner +66 migration +72 isolated legacy storage checks, exit0. The focused suite covers actual populated load/write metadata, reset generation, pre-COMMIT rollback and post-COMMIT observer failure; existing migration/bootstrap coverage is retained separately. See [S06B_RESULTS](../../reports/save-refactor/S06B_RESULTS.md) for exact scope and limitations. Independent review/local commit remain separate gates; no complete S06 UI/publication, responsiveness or Android acceptance is claimed.
