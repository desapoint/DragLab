# S05p — typed storage and incremental integrity proposal

Status: proposal only, based on `1fc63d0` / S04. No migration, SQLite mutation, production edit, Unity run, or writer implementation was performed. This is a minimal prerequisite for S04 ordinary oil/coolant top-up; it does not authorize converting other record families or implementing S05 before an independent Luna-high review.

## Authority and compatibility boundary

Keep `001-records.sql.txt` and `002-ownership.sql.txt` immutable, including their checksums and history. Append one versioned `003-typed-economy-fluids-integrity.sql.txt`; keep the application ID. `SchemaVersion` advances to 3 only with that asset and reviewed migration code. V3 migration is transactional and versioned; before changing a v2 database, make and verify the existing `.schema-2.bak` backup. On any unsupported contract, malformed typed value, failed validation, or injected failure, roll back the SQLite transaction, leave the v2 primary usable, preserve the backup, and refuse writes rather than initialize defaults. The proposal applies to the current supported profile contract (save version 12, mechanics version 1); earlier supported logical migrations must complete first. Future versions remain blocked.

Before v3, `StateRows.Capture` stores cash, revision, and `lastEconomyUtcTicks` as three separate root rows (`collection="$"`, keys `cash`, `revision`, and `lastEconomyUtcTicks`); `economyReceipts` is another root row, and each car payload in split `cars` rows contains fluids. After v3, the canonical owners for cash/revision/economy clock, fluids, and business receipts are the typed rows below. Migration removes those exact root rows and moved fluid fields, not an imagined single root payload. Do not maintain stale JSON mirrors. Existing `state_records` remains the canonical store for every unmigrated profile field. The `ledger_revision` value in `state_meta` remains a transaction-fence mirror of `profile_economy.revision`; read and write both in the same transaction and reject disagreement. `commit_id`, `reset_generation`, reset intent/floor, `content_revision`, and `physics_identity` keep their current separate meanings.

The complete legacy payload/relations and typed DTOs must be captured together on owner before dispatch for every transitional caller, including BeginValidatedSave. No worker may retain/read the mutable candidate. This temporary owner capture cost must be measured; S05 does not claim a responsive UI conversion. For the transition, `SqliteStateStore.Read` returns the remaining legacy logical rows plus typed rows; `ProfileStore` constructs the runtime profile and assigns typed fields directly before full validation. V3 migration removes `fluids` from car/engine JSON and removes root `cash`, `revision`, `lastEconomyUtcTicks`, and `economyReceipts` rows. Remaining `state_records` JSON covers all other unconverted content: car/engine fields, parts, inventory/market state, career/business state, setups, run records/checkpoints and other profile families. Settings remain in their existing settings file. Existing quote/equality JSON uses remain separate command-conversion work; none is allowed in the S04 path. The exact old-caller bridge, callers, field omission and S11/S13 retirement are governed by `S05P_DECISIONS.md`; that coordinator decision is binding and supersedes any bridge mechanics suggested here. The migration and direct pilot path still require one canonical typed authority and may not persist duplicate JSON copies.

The current `PaidEconomy.Record` is also called by non-fluid payments, sales, cart checkout, business claims/upgrades and job completion. Until those commands are converted, their cash/revision/clock/business receipt writes must reach the typed canonical rows through the coordinator-approved compatibility path, never reconstruct them from stale `state_records`. The existing profile serializer is still used by quote/equality callers, so v3 must not simply mark moved fields nonserialized and silently change those fingerprints. The S04 writer never calls the legacy path. Its exact caller set, name, permitted projection behavior, and retirement at S11/S13 are defined by binding `S05P_DECISIONS.md`; if that decision requires a new whole-profile capture or persisted duplicate fields, stop and revise the contract before implementation. Legacy JSON import/export and settings/diagnostic boundaries remain explicit; they can construct external JSON from the typed runtime profile. No generic JSON or binary full-profile replacement is introduced.

Reset must replace all typed rows and metadata in the same existing recoverable reset flow, increment generation, and retain the intent/floor and backup guarantees. Race checkpoint drain/merge/retirement remains owned by `ProfileStore`; the refill writer joins its single ordering gate and cannot consume or delete `pending_checkpoints` independently. It rejects an unsettled run as the current payment guard does. A committed typed write invalidates or updates targeted typed caches only after COMMIT; it must not repopulate `cachedRows` by reading all `state_records`.

## Existing implementation blockers

The current `SqliteStateStore.Read` path rejects any `digest_algorithm` other than `xor-sha256-rows-v1`, calls `Rows(db)` for the full table, and recomputes `Digest(cachedRows.Values)`. Current `Write` chooses cached or full rows, captures a `StateRows` snapshot, and writes a full-table digest. Therefore v3 cannot merely add tables: the loader needs an explicit typed envelope/full-load validator for v2/v3 and v2 digest verification before migration, while the S05 writer needs the separate declared-row integrity path above. The current full validation at load/recovery remains a requirement; do not route the direct command through it. Current native binding also means all non-string values are passed through `Convert.ToInt64`, and query values are returned as text, so bit columns need explicit long parsing/reinterpretation and the managed Windows/Android binding gate.

The existing legacy writer's commit CAS must stay strict. As resolved in `S05P_DECISIONS.md`, `LegacyCaptureManaged` may temporarily capture an unconverted caller's already-owned candidate, but typed DTO values are captured directly from that candidate and typed writes strip converted fields from its remaining legacy projection. A stale candidate must fail its exact expected-commit check; do not patch from a fresh database read or publish before durable acknowledgement. After typed commit, invalidate only `cachedRows` while preserving actual path and committed token needed by `ReadCommit`; current whole-cache `Invalidate()` also clears those, so it is unsuitable for this publication step. These are concrete S05 integration blockers, not permission to use JSON/full-profile capture for the pilot.

## Proposed v3 tables

All floating state uses one signed `INTEGER` per double containing the exact IEEE-754 binary64 bits reinterpreted as `Int64`. `SqliteNative.Statement.Bind` currently converts every non-string to Int64 and exposes query columns as text; it has no double or blob binding. Therefore do not declare `REAL`, bind fractional values through the existing wrapper, or pack an entire profile into a blob. `long` is rebound/read as signed decimal text and reinterpreted to recover the original bits, including negative zero. The coordinator reports an isolated Windows in-memory probe against bundled SQLite 3.54.0: 10/10 tested bit patterns round-tripped with integer bits; `REAL` through text columns preserved 9/10 and lost signed zero. That is coordinator evidence, not an execution by this proposal author. Android round-trip remains an implementation gate before a migration asset ships.

```sql
CREATE TABLE profile_economy (
  singleton INTEGER PRIMARY KEY CHECK(singleton = 1),
  cash INTEGER NOT NULL CHECK(cash BETWEEN 0 AND 2147483647),
  revision INTEGER NOT NULL CHECK(revision >= 0),
  last_economy_utc_ticks INTEGER NOT NULL CHECK(last_economy_utc_ticks BETWEEN 0 AND 3155378975999999999)
) WITHOUT ROWID;

CREATE TABLE fluid_states (
  owner_kind TEXT NOT NULL CHECK(owner_kind IN ('car','engine')),
  owner_id TEXT NOT NULL,
  value_version INTEGER NOT NULL CHECK(value_version = 1),
  oil_litres_bits INTEGER NOT NULL,
  coolant_litres_bits INTEGER NOT NULL,
  oil_temperature_c_bits INTEGER NOT NULL,
  oil_pressure_bar_bits INTEGER NOT NULL,
  coolant_pressure_bar_bits INTEGER NOT NULL,
  oil_condition_bits INTEGER NOT NULL,
  oil_consumed_litres_bits INTEGER NOT NULL,
  oil_leaked_litres_bits INTEGER NOT NULL,
  coolant_lost_litres_bits INTEGER NOT NULL,
  service_elapsed_seconds_bits INTEGER NOT NULL,
  oil_burn_lps_bits INTEGER NOT NULL,
  oil_leak_lps_bits INTEGER NOT NULL,
  coolant_leak_lps_bits INTEGER NOT NULL,
  coolant_vent_lps_bits INTEGER NOT NULL,
  PRIMARY KEY(owner_kind, owner_id),
  FOREIGN KEY(owner_id) REFERENCES owners(id) ON DELETE CASCADE
) WITHOUT ROWID;

CREATE TABLE economy_receipts (
  id TEXT PRIMARY KEY NOT NULL,
  receipt_order INTEGER NOT NULL UNIQUE CHECK(receipt_order >= 0),
  operation TEXT NOT NULL CHECK(length(operation) > 0),
  target TEXT,
  revision INTEGER NOT NULL CHECK(revision >= 0),
  utc_ticks INTEGER NOT NULL CHECK(utc_ticks > 0),
  cash_before INTEGER NOT NULL CHECK(cash_before BETWEEN 0 AND 2147483647),
  cash_after INTEGER NOT NULL CHECK(cash_after BETWEEN 0 AND 2147483647),
  fuel_added_bits INTEGER NOT NULL,
  oil_added_bits INTEGER NOT NULL,
  coolant_added_bits INTEGER NOT NULL
) WITHOUT ROWID;
CREATE TABLE economy_receipt_items (
  receipt_id TEXT NOT NULL REFERENCES economy_receipts(id) ON DELETE CASCADE,
  item_kind TEXT NOT NULL CHECK(item_kind IN ('disposed','retained')),
  item_order INTEGER NOT NULL CHECK(item_order >= 0),
  item_id TEXT,
  PRIMARY KEY(receipt_id, item_kind, item_order),
  UNIQUE(receipt_id, item_kind, item_id)
) WITHOUT ROWID;

CREATE TABLE operation_receipts (
  save_slot TEXT NOT NULL,
  reset_generation INTEGER NOT NULL CHECK(reset_generation >= 0),
  operation_id TEXT NOT NULL,
  command_kind TEXT NOT NULL,
  intent_version INTEGER NOT NULL CHECK(intent_version >= 1),
  intent_hex TEXT NOT NULL CHECK(length(intent_hex) BETWEEN 2 AND 4096),
  intent_sha256_hex TEXT NOT NULL CHECK(length(intent_sha256_hex) = 64),
  ledger_revision_before INTEGER NOT NULL CHECK(ledger_revision_before >= 0),
  ledger_revision_after INTEGER NOT NULL CHECK(ledger_revision_after >= 0),
  storage_commit_before INTEGER NOT NULL CHECK(storage_commit_before >= 0),
  storage_commit_after INTEGER NOT NULL CHECK(storage_commit_after >= 0),
  content_revision INTEGER NOT NULL CHECK(content_revision >= 0),
  physics_identity TEXT NOT NULL CHECK(length(physics_identity) > 0),
  committed_utc_ticks INTEGER NOT NULL CHECK(committed_utc_ticks > 0),
  domain_receipt_id TEXT,
  PRIMARY KEY(save_slot, reset_generation, operation_id),
  CHECK(ledger_revision_after = ledger_revision_before + 1),
  CHECK(storage_commit_after = storage_commit_before + 1)
) WITHOUT ROWID;

CREATE TABLE fluid_refill_results (
  save_slot TEXT NOT NULL,
  reset_generation INTEGER NOT NULL,
  operation_id TEXT NOT NULL,
  car_id TEXT NOT NULL,
  cost INTEGER NOT NULL CHECK(cost >= 0),
  cash_after INTEGER NOT NULL CHECK(cash_after BETWEEN 0 AND 2147483647),
  oil_added_bits INTEGER NOT NULL,
  coolant_added_bits INTEGER NOT NULL,
  oil_after_bits INTEGER NOT NULL,
  coolant_after_bits INTEGER NOT NULL,
  domain_receipt_id TEXT NOT NULL UNIQUE,
  PRIMARY KEY(save_slot, reset_generation, operation_id),
  FOREIGN KEY(save_slot, reset_generation, operation_id)
    REFERENCES operation_receipts(save_slot, reset_generation, operation_id) ON DELETE CASCADE
) WITHOUT ROWID;

CREATE TABLE record_integrity (
  family TEXT NOT NULL,
  record_key TEXT NOT NULL,
  row_hash_hex TEXT NOT NULL CHECK(length(row_hash_hex) = 64),
  PRIMARY KEY(family, record_key)
) WITHOUT ROWID;
```

The S04 refill write set is one `profile_economy` row; one `fluid_states` row for the target car; one inserted `economy_receipts` row and its child entries (empty disposed/retained lists for refill); one optional deleted oldest receipt and its children if the old window is at 256; one `operation_receipts` row; one `fluid_refill_results` row; corresponding `record_integrity` rows and the digest metadata. The car's other JSON payload fields are not read/rewritten by the pilot. Attached engines have no fluid row (the prior loader clears their nonauthoritative inline-null placeholders; migration follows the narrow rule in S05P_DECISIONS); every car owns its fluid state, including zero-fluid engine-less chassis; detached engines have a row. Full profile validation enforces owner kind/parent consistency because the SQL foreign key can only enforce owner identity. All 14 `FluidState` double fields are listed above; no service, wear, or resource-history field is averaged, reset, or healed.

`economy_receipts` retains the existing domain record exactly: ID, operation, nullable target, revision, UTC ticks, cash before/after, fuel/oil/coolant added, and ordered `disposed` / `retained` ID arrays. `economy_receipt_items` preserves array order and values without JSON, including a nullable item ID distinct from an empty string as permitted by current validation. Domain duplicate/disjoint checks remain required because SQL UNIQUE treats null separately. Keep the current 256 business-receipt bound and oldest-first eviction. The independent `operation_receipts` table is never subject to that eviction; scope its primary key by reset generation so stale operations from an old generation cannot collide or be replayed after reset. The generic operation receipt is written only for committed commands; uncommitted work leaves no receipt. Retain operation receipts for the lifetime of their reset generation; pruning requires a separately reviewed durable idempotency horizon. The pilot result table stores the exact cost, additions, resulting quantities, cash and business receipt ID needed to acknowledge an already-committed refill after a lost response. It deliberately stores that ID without a foreign key because the bounded domain receipt may later be evicted while the generic idempotency result remains durable. It is typed command result data, not a serialized snapshot.

The reset transaction clears operation receipts/results for the previous generation while advancing `reset_generation`; a request from that generation fails the generation fence before lookup. This bounds generic receipt retention by the active reset generation and makes replay scope explicit. The key includes slot even though each slot currently resolves to a distinct path, so copied/misrouted databases cannot transfer command identity across slots. `value_version` in `fluid_states` versions the typed row format only; it is not an optimistic row version. No row version is inferred or incremented from a commit. Exact global ledger/commit fences plus the bounded serialized writer provide stale-write exclusion for this pilot.

## Migration 003 and exact-value conversion

1. Before migration, drain the existing checkpoint and backup workers through `ProfileStore`, create/verify `.schema-2.bak`, resolve any reset intent through the current `ProfileStore` recovery rules, and verify the selected primary is at or above the durable reset floor. Open validates app ID and the full ordered migration history/checksums as today.
2. Start one `BEGIN IMMEDIATE`. Before transforming data, verify the v2 `records_digest` using its current base64-encoded XOR-of-SHA256 algorithm. Apply immutable `003` DDL and record migration version/checksum. Validate the current save contract and full logical profile before modifying canonical values. Do not call `Ensure` to hydrate missing fluid rows or reset faults as part of storage normalization.
3. Decode each legacy row only at this explicit v2-to-v3 migration boundary. The source of migrated fluid values is the current v2 decoder's exact binary64 values, read from a fully validated current-contract profile before typed assignment. Compare each value to an independent invariant raw-token parse and record/inspect any discrepancy; do not assume raw token parsing is the authoritative value if it differs from the current loader. Convert the validated loaded value directly to its IEEE-754 `Int64` bits. Do not serialize through Unity JSON or SQLite `REAL`. Reject null/malformed/nonfinite values and abort without healing. Require car fluid presence and detached-engine fluid presence. Attached engines have no independent fluid authority; handle Unity inline-null placeholder serialization using the narrow verified-parent rule in S05P_DECISIONS, not an absence assertion or broad Ensure. Preserve the exact array order and every nullable/string/integer field in business receipts. Require the current valid 0–256 receipt window.
4. Insert `profile_economy`, `fluid_states`, `economy_receipts` and child items. Populate `operation_receipts` empty; prior domain receipt IDs do not prove generic command commit status. Remove only the canonical values moved to the typed tables from `state_records`, retaining unrelated rows byte-for-byte. Existing authored content is not involved.
5. Build persisted hashes for every remaining legacy row and every new typed logical row. Verify the old base64 v1 aggregate before conversion, then replace `records_digest` with lowercase hex for the v2 aggregate and set `digest_algorithm = 'xor-sha256-records-v2'` in the same transaction. Validate the new typed projection and whole logical profile, foreign keys, row hashes and aggregate before setting schema version 3 and COMMIT. Injected failure or validation error rolls back all SQL work; keep the v2 primary/backup recovery path. No data is accepted by fabricating absent OEM parts, changing IDs, or filling missing fluid state.

The migration code must be able to validate both sides without using a lossy JSON round-trip. Preserve raw legacy row bytes for fields left in `state_records`; assemble a typed profile view by assigning decoded typed rows after decoding the remaining ordinary profile records. For migration candidates, compare every FluidState double by exact 64-bit representation against the current v2 decoder. If the decoder's representation differs for any supported value, refuse migration and revise the conversion path before shipping. Never silently accept a one-ULP change.

## Integrity version and bounded writer contract

Keep the existing v1 algorithm for legacy rows: `SHA256(UTF8(collection + "\n" + key + "\n" + ordinal + "\n" + payload))`, matching `StateRows.Row.Id` (`collection + LF + key`). Store each existing row's 32-byte result as lowercase 64-character hex in `record_integrity`. Typed hash canonicalization is pinned by `S05P_DECISIONS.md`: `Text(s)` is unsigned 32-bit UTF-8 byte length plus strict UTF-8 bytes; null is distinct from empty. A typed row hash input is `Text("draglab-record/v1")`, table name, record key, field count, then fields in exact DDL order including primary keys. Each field is `Text(columnName)` and its type tag (`00` null, `01` plus `Text(value)` for strings, `02` plus fixed-width little-endian signed 64-bit value); doubles use their signed IEEE-754 bit pattern in the same integer representation. Composite keys use lowercase hex of their ordered encoded key fields. SHA-256 covers these bytes. No JSON, reflection serializer, platform-dependent culture formatting, or whole-profile blob participates.

Set `state_meta.digest_algorithm = 'xor-sha256-records-v2'`. `state_meta.records_digest` remains a 64-character hex encoding of XOR over all persisted row hashes. The v3 migration may scan all existing rows once to seed it. On a targeted transaction, fetch only the old hashes for declared keys, XOR those values out, hash and upsert/delete only changed typed records and their integrity rows, XOR new hashes in, and store the result in the same transaction. The ordinary loader still scans and validates all rows/relations and recomputes the aggregate at the full-load recovery boundary; this does not make load partial or weaken corruption detection. `pending_checkpoints` remains its separate journal. Do not reuse full-table `Rows(db)`, `cachedRows`, `StateRows.Capture/CaptureJson`, global `Sync`, or all-record `Digest(snapshot.rows)` in the refill path. After COMMIT invalidate the legacy all-row `cachedRows` cache without repopulating it; its next legacy caller may perform its existing full load until that caller is converted.

The ordered S05 transaction uses the existing save owner and SQLite gate:

1. Owner verifies the request still targets the active slot/path; drains or orders behind checkpoint work; refuses reset in progress; supplies the actual current reset generation. No planner-generated generation, ledger value or commit token is authoritative.
2. `BEGIN IMMEDIATE`; first verify the active slot/path and current reset generation/floor against the request. The active slot/path comes from the `ProfileStore` owner at enqueue and transaction time; it is not inferred from profile JSON or operation ID. Next look up `(save_slot, reset_generation, operation_id)`: compare full canonical `intent_hex`; equal intent returns the stored result even though its original ledger/storage fences are now old, and mismatched intent is rejected. Only if no receipt exists, compare exact ledger revision, exact storage commit, catalogue content revision and physics identity with the request's captured fences. Check integrity of affected old rows only.
3. Validate typed target/cash/economy clock and command read dependencies, current quote issue/expiry, no active unsettled run, exact price/capacity policy, and the target car/attached engine relationship. No whole-profile `Ensure`, JSON quote copy, global relation scan or unrelated row projection.
4. Insert the business receipt and required empty item sets; evict its oldest predecessor if the bound would exceed 256. Update `fluid_states` and `profile_economy`; advance ledger revision and `commit_id` exactly once. Insert the durable operation receipt and typed refill result. Update only the affected row hashes and incremental XOR digest.
5. Run the precommit injection point and COMMIT. Publish the in-memory state/cache only after COMMIT. A lost post-COMMIT acknowledgement is resolved by operation ID + exact intent, not inferred from the newer commit number. Existing reset and backup scheduling remain owned by the ordered persistence coordinator.

For `intent_hex`, use the exact coordinator codec in `S05P_DECISIONS.md`: `Text("fluid-refill-intent/v1")`, then named fields in this order: save slot, reset generation, operation ID, command kind `FluidTopUp`, expected ledger revision, expected storage commit, content revision, physics identity, car ID, requested oil bits, requested coolant bits, quote issue tick, quote expiry tick, accepted confirmation tick. Each field name is `Text(name)` and then typed tag/value; strings use tag `01` + `Text(value)`, numeric values tag `02` + fixed-width little-endian signed 64-bit value. Required identity strings are non-null; null is distinct from empty. Oil/coolant are exact signed IEEE-754 bit patterns. Lowercase hex of canonical bytes is stored in `intent_hex`, bounded to 2048 bytes before hex encoding; SHA-256 is supplementary and never substitutes for full-byte equality. The accepted tick is sampled once before enqueue and reused for retries; issue/expiry remain admission evidence and current expiry is checked only for new commands. S04's `StateCommandVersionFence` currently has no physics identity or accepted-tick field, so adding owner-supplied physics identity and a fixed-on-acceptance tick are required contract changes before this encoding can be constructed. Do not substitute catalogue/save version for physics identity.

The bounded read path is key/index based: read the singleton economy row; read the one `(car, carId)` fluid row and its integrity entry; read the `(saveSlot, resetGeneration, operationId)` receipt before stale-fence rejection; on a new operation, check the existing `owners` row by exact ID/kind and query the economy-receipt count (at most 256) plus oldest ID by `ORDER BY receipt_order LIMIT 1`; fetch integrity only for keys in the write/delete set. No query reads all `state_records`, ownership relations, business receipts or hashes. Updates use primary-key predicates for the singleton, target fluid row and exact receipt keys; integrity XOR is updated from the old/new hashes. The operation receipt and typed result use their composite key. Query plans should show primary-key/unique-index access except the explicitly bounded count of at most 256 business receipts.

S05 must add failure injection at transaction boundaries after `BEGIN`/fence, after domain receipt/eviction, after fluid/economy updates, after operation/result receipt, after integrity update, immediately before COMMIT, and at simulated lost acknowledgement after COMMIT. Each pre-COMMIT failure must roll back all typed values and hashes. For a post-COMMIT lost acknowledgement, retry must return the exact stored result once, and a mismatched intent must fail without writes. Existing `before-commit`, `migration:3`, backup/recovery, reset intent/floor, and checkpoint tests remain in force.

## Required decision before implementation

Luna-high review must approve or revise: the typed canonical row names/authority, bit-exact integer double encoding and native Windows/Android bind/read evidence, JSON migration precision gate, exact intent representation, v2 digest compatibility and incremental update rule, legacy bridge retirement/field omission, reset/checkpoint/backup ordering, and operation-receipt retry behavior. If the type projection cannot be made compatible without stale duplicate fields or all-row work on the pilot path, stop and resolve the contract before adding migration or writer code. Keep other hot families and their migrations out of this proposal.
