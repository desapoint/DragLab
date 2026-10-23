# S05p independent proposal review

Date: 2026-10-23
Baseline: `1fc63d0`
Verdict: **PASS — the proposal is sufficiently specified to begin S05a, with the initial review blockers corrected.**

The initial review was blocked on the two issues recorded below. The coordinator has since resolved both in the binding decisions and proposal; this re-review records those corrections.

## Initial blocker 1 — resolved

`ProfileStore.Pending.BeginValidatedSave` originally passed the mutable `PlayerProfile candidate` by reference into `Task.Run`; the worker later called `SqliteStateStore.CaptureManaged(candidate, ...)`. Capturing typed cash/fluid/receipt values synchronously while leaving the legacy projection to read the candidate later could mix candidate states. Strict database commit CAS does not protect candidate memory during capture.

The binding decision and proposal now require capturing the complete legacy payload, relationship projection, typed DTOs and expected commit together on the owner before dispatch. The worker receives only that private completed transport and never reads a `PlayerProfile`. The transition capture cost must be measured, there is no UI responsiveness claim, and the UI pilot stays dormant until S06. This is a temporary legacy bridge for unconverted callers; it does not route the typed refill command through a full-profile capture.

## Initial blocker 2 — resolved: receipt item null fidelity

The original `economy_receipt_items.item_id TEXT NOT NULL` constraint rejected a legacy receipt that the current domain validator accepts. `PaidEconomy.ValidateState` requires non-null arrays and rejects duplicates/intersections, but permits a single null or empty-string item ID in an individual array.

The revised DDL makes `item_id` nullable. The migration/loader contract preserves order and null versus empty while retaining the existing domain validation; the typed hash codec distinguishes SQL null from empty text. The updated schema probe inserts and asserts ordered null and empty values before deleting the bounded history receipt. This resolves the schema incompatibility without healing or changing source receipts.

## Additional source finding — resolved

The frozen 1-car S00 fixture has an engine whose `parentVehicleId` matches the car binding, while its inline Unity JSON `fluids` object contains placeholder defaults (zero quantities, 80 temperature, pressure 2 and condition 1). The supported loader clears that inline field for an attached engine, so it is not an independent fluid authority.

The binding decision and proposal now say to verify the parent/car binding and apply only this narrow nonauthoritative-placeholder rule. The car’s authoritative fluid state and detached-engine rows remain exact; the migration retains the original v2 backup and does not call broad `Ensure`, alter bindings or heal state. The matrix adds a focused case for the fixture representation.

## Reviewed proposal properties and evidence

- V3 typed rows own economy, fluids and business receipts; remaining legacy rows own only unconverted fields. Exact `expectedCommit` CAS applies to both routes, and the transitional bridge has a named S13 retirement.
- The request and receipt contract binds slot, generation, operation ID, kind, ledger/storage fences, content and physics identity, target, exact quantity bits, quote times and one accepted time. Retry compares canonical intent bytes. Results survive business-history eviction and are generation-scoped.
- The v2 digest compatibility rule verifies the old Base64 XOR before conversion, retains the legacy row canonical input, seeds per-row hashes and switches the aggregate to lowercase-hex `xor-sha256-records-v2`. Ordinary commands update only declared old/new hashes; full scans remain at load/audit boundaries.
- Migration preserves all 14 fluid fields, rejects malformed/missing/nonfinite/unsupported values without normalization, keeps immutable migrations 001/002, and distinguishes full load validation from the bounded command path.
- The frozen fixture was inspected directly and confirms the attached-engine placeholder case and valid parent binding.
- Reran `probe_save_refactor_sqlite_types.py`: signed integer bit patterns round-tripped 10/10 in bundled Windows SQLite 3.54.0 `:memory:`; REAL/text preserved 9/10 because negative-zero sign was lost.
- Reran `probe_save_refactor_sqlite_schema.py`: candidate DDL, constraints, 14 fluid bit columns, owner cascade, result independence, nullable-item order/null-empty behavior, and six query-plan shapes passed. Both proposal probes and Python `py_compile` passed.

These probes cover neither the managed `SqliteNative` path nor Android, migrations, durable writes, transaction fault handling or performance. Exact managed/Android bit binding, migration parity, receipts/reset, integrity loading and bounded-row evidence remain S05 execution gates, not S05p claims.

The older domain-boundary document still contains these issues in a section labeled as historical/open findings; the binding coordinator decisions supersede those entries, and its integrated correction note records the resolved contracts. No conflicting current proposal requirement remains.

No production files, saves, Unity state, reports outside this review, or Git state were changed by this review. Existing unrelated worktree changes were preserved.
