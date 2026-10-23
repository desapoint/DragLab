# S01 — storage contract mapping

Baseline reviewed: S00 checkpoint after `f163454` is finalized. This is a storage-side mapping of existing SQLite behavior to the S01 command boundary. It does not activate a new writer, change `ProfileStore`/`SqliteStateStore`, or add persistent authority.

## Existing storage facts

- `ProfileStore.FilePath` selects the active slot path. Slot identity is external to `PlayerProfile` and `SqliteStateStore.Snapshot`; neither carries a slot token for a queued command to validate later.
- `PlayerProfile.revision` is captured in `Snapshot.revision` and persisted as `state_meta.ledger_revision`. `SqliteStateStore.Write` rejects revisions older than the saved revision.
- `PlayerProfile.storageCommit` is captured as `Snapshot.expectedCommit`. `Write` compares it with `state_meta.commit_id` (and the in-process path cache token), then advances `commit_id` in the transaction. On return, `ProfileStore` copies the committed token into the candidate/profile.
- The persisted content_revision and physics_identity remain distinct (MechanicalCatalog.Version versus MechanicsSchema.Model). A command's ExpectedContentVersion fences catalogue content only; physics-dependent planners need a typed physics identity/version dependency before activation.
- `state_meta.reset_generation` is durable and currently read inside the transaction. Reset increments it in the same transaction as the state rewrite. A sibling `.reset-intent` file fences crash recovery, and `.reset-generation` records the durable recovery floor. Backup recovery checks that floor. This protects reset recovery but is not a command-carried generation fence checked when a command is enqueued.
- SQLite writes use one process-wide gate and `BEGIN IMMEDIATE`; row deltas, owner/component/slot projections, digest, commit/revision/version metadata, deletion of the SQL `pending_checkpoints` rows and COMMIT share one transaction. The separate race checkpoint journal is retired by `ProfileStore` after the successful write. The API is still invoked through individual synchronous calls or the specialized finish-settlement task; there is no common ordered command writer.
- Profile-level `economyReceipts`, `careerRuns`, and `runCheckpoints` are durable gameplay records. They are serialized with profile state and participate in the same SQLite transaction. Checkpoints are a separate resource journal and are retired by successful full saves/reset.

## Missing command durability fields

There is no durable generic command receipt keyed by operation ID, no receipt lookup/reconciliation API after a caller loses its acknowledgement, and no command-scoped reset generation or slot identity in the candidate/snapshot. Existing gameplay receipts describe economic or race outcomes; they do not prove whether an arbitrary command transaction committed and cannot be used as a universal idempotency record without changing their domain meaning. `state_meta` has no last-operation ID or reset-operation receipt. Do not infer a receipt from an incremented commit token: another command may have committed first, and a post-COMMIT acknowledgement may be lost.

The reset generation and recovery intent/floor do exist durably as described above. S01 must map them as storage fences and clearly distinguish them from the currently absent command-carried generation and reset-operation receipt. S01 must not fabricate either field or claim exact-once retry reconciliation is available.

## Proposed dormant mapping

Map the frozen API in `DragLab.Domain.Mechanics.StateCommandContracts.cs` to storage as follows. S01 provides no storage adapter; these are the responsibilities the future writer must satisfy:

| Contract value | Existing source / intended boundary | Storage behavior in S01 |
| --- | --- | --- |
| Save slot identity | `ProfileStore.FilePath` selection | Carry an opaque slot identity in the command/storage envelope; resolve it to the existing path in the owner. Reject an envelope targeting a different active slot. Do not store a competing slot authority in profile rows. |
| Expected reset generation | `state_meta.reset_generation`, checked against the durable recovery floor | Capture the current generation with the command; compare at the transaction fence. Ordinary command writes preserve it. Reset advances it atomically with its reset state. S01 cannot offer this check until a contract field is frozen. |
| Expected ledger revision | `StateCommandVersionFence.ExpectedLedgerRevision`, mapped from `PlayerProfile.revision` / `state_meta.ledger_revision` | Preserve the existing monotonic stale-revision rejection. Manifest read dependencies carry per-record versions for future state-owner validation; current production state does not yet provide those versions. |
| Expected storage commit | `StateCommandVersionFence.ExpectedStorageCommit`, mapped from `PlayerProfile.storageCommit` / `state_meta.commit_id` | Preserve compare-and-swap behavior, including the in-process cache check. A future writer must advance exactly one commit for a command using the receipt contract and return the token only after COMMIT. |
| Typed writes/deletes and relation changes | Existing `StateRows.Row` payloads and owner/component/installed-slot tables | A future writer must accept only explicit affected typed records and relation changes. S01 boundary remains dormant; it must not serialize a full profile or turn JSON into command transport. Existing full capture remains untouched while callers are unmigrated. |
| Operation receipt value | `StateCommandReceipt` | This is metadata only. It requires exact `expected ledger revision + 1` and `expected storage commit + 1`, but no generic durable receipt, lookup or reconciliation exists. Identity and next-version values cannot prove that a retried operation has the same intent; a future writer must bind the ID to stable typed intent and reject mismatched reuse. Existing business/race receipts continue as domain records. |
| Durable result / acknowledgement | `Snapshot.committed` assigned after SQL COMMIT | Expose a result only after storage confirms COMMIT. If the receipt is absent, an uncertain acknowledgement cannot yet be reconciled by operation ID. |
| Checkpoint ordering | `RaceCheckpointStore.Drain/Merge/Retire` around `ProfileStore` writes | Keep coordination owned by `ProfileStore` in S01. A future writer must not open another connection or bypass checkpoint drain/merge/retirement. |
| Reset | `ProfileStore.TryReset` + reset-intent/floor and backup drain | Do not alter reset code in S01. Future queue integration must fence/drain old commands before changing generation and must retain the present recoverable reset sequence. |

## Boundary constraints and review blockers

S01 includes no storage adapter: `StateCommandManifest.ValidateCurrentFence` is the useful pure, dormant boundary. No SQL, profile capture, JSON, connection ownership, gameplay wiring, durable receipt or retry-safe exact-once behavior is added. A later writer must reconcile ambiguous post-COMMIT outcomes by a durable receipt bound to stable typed intent.

The existing storage path still encodes the whole profile and projects every owned relationship before row-level SQL deltas. That remains a documented legacy behavior for unconverted callers, not a permitted implementation for any new command adapter. S01 makes no schema or write-path change and makes no claim that the hard no-full-profile-clone/no-runtime-JSON requirements are already met. The content version fence is catalogue-only; physics identity remains a separate typed dependency required before any physics-dependent planner is activated.

## Validation performed

Static source review only: `ProfileStore`, `ProfileStore.Pending`, `SqliteStateStore`, `StateRows`, and S00 storage audit. No Unity run, test command, schema change, save write, or storage implementation was performed for this mapping.
