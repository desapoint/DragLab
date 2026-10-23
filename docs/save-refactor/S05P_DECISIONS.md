# S05p coordinator decisions

Proposal checkpoint, baseline `1fc63d0`. These decisions resolve the three author proposals; they do not claim production implementation. The final user gates remain **no full-profile clone to save** and **no JSON encode/decode to manipulate runtime state**. The transitional legacy path is unfinished debt, never an acceptable final fallback.

## One authority while callers are converted

V3 typed tables alone own cash, revision, economy clock, fluid state and economy receipts. Remaining `state_records` owns only unconverted fields. Do not mark runtime profile fields nonserialized: existing quote/equality consumers still observe them until their own checkpoints. Do not store competing JSON mirrors.

The existing full-capture save route is renamed/isolated as `LegacyCaptureManaged`; it must not be called by the new refill command. It may temporarily retain its current whole-profile JSON projection for **unconverted callers only**. This permits no new clone or new full-profile replacement format. At this transition boundary, capture the converted values directly into private typed DTOs from the already-owned candidate; strip their fields from the remaining legacy SQL row projection. Do not extract canonical typed values by decoding that JSON. The same SQL transaction writes both projections and the incremental integrity representation. This bridge is explicitly removed at S13 after the command conversions; retaining it fails completion.

Every write uses the existing single SQLite gate and exact `expectedCommit` CAS, including the transitional route. A candidate captured before a successful typed command must fail its old commit token, even when its ledger revision happens to match; never silently patch it with current database values or overwrite newer typed state. For every transitional save, including `BeginValidatedSave`, complete the entire remaining legacy payload/relationship capture and converted typed DTO capture together on the owner before `Task.Run`. The worker receives only that private completed transport, never a `PlayerProfile` reference or a callback that reads it. Existing whole capture is temporary debt, not a new deep clone. Moving its capture off a worker may expose existing capture cost on the owner: measure this transition, make no no-regression claim, and retire it via the planned command conversions. Do not activate the UI pilot before S06 admission/publication tests. A partial owner DTO capture plus later worker JSON capture is expressly prohibited. S06 adds admission/queue ownership before switching gameplay. S05 keeps the new writer dormant until that handoff is proven.

Named compatibility entry points and retirement owners:

| Entry point / caller | Transition behavior | Retirement checkpoint |
| --- | --- | --- |
| `ProfileStore.TrySaveCore` / `TrySaveValidated`, `PartsGarage` and `MechanicsTransactions.Try` | Existing commands may use isolated legacy capture; typed DTOs come from that validated candidate, strict commit CAS remains mandatory | S07 cart, S08 repair, S09 ownership/workshop, S10 remaining commands; bridge removed S13 |
| `ProfileStore.BeginValidatedSave` / `CompleteValidatedSave`, `PrototypeGame.FinishSettlement` | Complete both projection captures on owner before dispatch; worker never reads candidate, snapshot token is fixed; commit acknowledgement precedes publication; mixed typed commits cause stale rejection | S12 settlement; no whole-profile persistence left at S13 |
| `NativeSetupManagement`, direct `PrototypeGame` save calls for preferences/cart/exit | Map each action to its command family; no generic save fallback after conversion | S07 reservations / S10 setups and lifecycle / S13 callsite audit |
| `ProfileStore.Load`, logical migration, interrupted-run recovery | Decode remaining legacy fields once at load; assign typed fields directly before validation; no typed-to-JSON reconstruction | S11 family migrations then S13 typed loader |
| `ProfileStore.TryReset`, first creation and legacy import | Full boundary initialization is allowed; typed and remaining legacy rows replace together under existing reset intent/floor and commit fencing | S10 reset, S11 migrations; JSON remains only explicit legacy import |
| `SqliteStateStore.ExportJson` | Explicit external export assembles JSON at that boundary only; do not use export internally as a state copy | Remains external boundary |
| `LegacyProfileStore` Editor JSON fixture route | Historical migration/testing boundary, not a production save fallback | Final allowlist records test-only reachability |
| Checkpoint merge / retire in `ProfileStore` | Existing ordered merge must precede capture; typed refill cannot clear journal rows; retirement follows durable merged state | S12 typed checkpoint path |

Current `Sync` only deletes absent owner IDs and upserts changed identities; it does not delete/reinsert unchanged owners. A removed owner cascades its fluid row; transitional projection must explicitly reconcile typed owner rows after relationship changes in the same transaction. Attached engines have no fluid row; detached engines and all cars do. Existing Unity JSON encodes an attached engine's null inline fluid object as placeholder values (the frozen S00 fixture has zero quantities, temperature 80, pressure 2 and condition 1). After verifying the actual car/engine parent binding, treat that attached-engine field as nonauthoritative exactly as the supported old loader does; do not require physical absence or migrate it as a second fluid owner. The car retains every authoritative fluid bit. This narrow legacy representation rule must not call broad Ensure, heal real fluids, create parts or change any bindings. Preserve original bytes in the schema backup. Do not re-create removed owner state from a stale projection. Test engine attach/detach, sale and reset with the FK enabled.

Typed load returns an envelope: remaining legacy payload plus typed economy/fluid/receipt DTOs and commit/generation metadata. Decode remaining payload, then assign typed values directly. No JSON encoding of the envelope or typed overlay. Full validation and integrity scanning belong at load/migration/recovery; the direct command must not invoke them.

After a typed COMMIT invalidate `cachedRows` only, preserve/update the actual path and committed token. Do not call current whole-cache `Invalidate()` if it also clears the token needed by `ReadCommit`. Any next transitional writer may rebuild its old row cache; that cost is legacy debt, not hidden refill work. The command's typed caches publish only after durable acknowledgement or exact receipt reconciliation.

## Fixed request and retry identity

One owner-confirmed accepted UTC tick is sampled before enqueue, validated and frozen. It determines the domain receipt/economy effect time; retry never samples a substitute. Execution still checks observed current time against quote expiry for a **new** command. A committed receipt is replayable after expiry. Changed accepted time or any changed immutable request field requires a new operation ID.

Canonical intent `fluid-refill-intent/v1` includes, in this order: save slot, reset generation, operation ID, command kind `FluidTopUp`, expected ledger revision, expected storage commit, content revision, physics identity, car ID, requested oil bits, requested coolant bits, quote issue tick, quote expiry tick, accepted confirmation tick. This deliberately binds the entire immutable command, not only a pricing hash. Read-set observations remain independent transaction dependencies. Compare full canonical bytes, not SHA alone. Existing S04 fence needs owner-supplied physics identity and accepted time before the writer can construct this intent; do not invent either from catalogue version.

Before receipt lookup verify selected path/slot, actual reset generation and durable floor. Then exact receipt lookup may resolve an old commit/ledger fence. Only an absent receipt proceeds to new-command CAS/read-set/expiry validation. Wrong generation/slot never retrieves an old receipt. Results and generic operation receipts survive all 256-entry business-history evictions for their reset generation; reset clears them transactionally. No bounded retention horizon is invented. This increases disk history with committed operations within a generation; queue and resident caches remain separately bounded.

`fluid_refill_results.domain_receipt_id` is an immutable value, **not a foreign key** to bounded history. Both operation/result keys include `(save_slot, reset_generation, operation_id)`; the result references its operation through that composite key. Do not cascade-delete reconciliation when a car or bounded history entry is removed.

## Exact representation and integrity codec

`economy_receipt_items.item_id` is nullable TEXT. Existing `PaidEconomy.ValidateState` permits a single null/empty ID while requiring non-null arrays and uniqueness/disjointness; preserve null versus empty, order and array boundaries. Empty arrays reconstruct from zero child rows. Typed boundary validation retains these exact domain rules; SQL UNIQUE does not detect duplicate nulls, so do not substitute it for domain validation.

Each fluid double is a separate signed INTEGER containing its IEEE-754 bits. No packed profile blob and no SQLite REAL conversion. Migration value authority is the exact current supported v2 loader's decoded value, not an assumed alternative decimal parser. Preserve raw v2 source/backup and unrelated payload bytes. Compare raw-token parsing against that authority; if they differ, use the supported decoder's exact bits and record the boundary result. Never normalize/heal to get a passing migration. Malformed, nonfinite, missing or unsupported state is rejected. Direct new typed saves/reloads must preserve every bit.

Freeze a small explicit codec; no reflection/general serializer:

- Byte values below are hexadecimal; all numeric widths are fixed, little endian.
- `Text(s)` = unsigned 32-bit UTF-8 byte count followed by exact bytes, strict UTF-8; reject malformed strings. Null is not empty.
- A field = `Text(columnName)` then type tag: `00` null; `01` plus `Text(value)`; or `02` plus signed 64-bit integer. Double bits use tag `02`; integer columns use tag `02`; no culture formatting enters hashes. Format/version/identity strings use tag `01`.
- A typed row hash input = `Text("draglab-record/v1")`, `Text(tableName)`, `Text(recordKey)`, unsigned 32-bit column count, then fields in exact DDL declaration order, including primary keys. SHA-256 gives 32 bytes. No hash of `record_integrity` itself.
- A single-column text primary key uses that exact string as `recordKey`. Composite primary keys use lowercase hex of their ordered field encodings (the key column names/tags/values), with no lossy delimiter concatenation. The singleton key is `1`.
- `record_integrity.family` equals the typed table name; legacy rows use family `legacy` and their existing `collection + LF + key` identity. Legacy hash input stays byte-for-byte the current v1 `collection + LF + key + LF + ordinal + LF + payload` formula.
- Intent uses `Text("fluid-refill-intent/v1")` followed by named fields in the order above with the same tags/widths; stored `intent_hex` is lowercase hex of these bytes. Bound encoded bytes to 2048; reject before writes if larger. Its SHA column is lowercase hex SHA-256 of the bytes.
- Aggregate XOR includes all logical legacy and typed row hashes, excluding `record_integrity`, metadata and the separate checkpoint journal. The v3 algorithm name is `xor-sha256-records-v2`; aggregate is **64-character lowercase hex**, explicitly converted from v2's Base64 representation during migration. Old v2 integrity must be verified before conversion. Migration removes converted legacy fields, so affected remaining car/engine hashes are recalculated; untouched legacy rows keep their original hashes.

This is corruption detection, not authentication against an attacker rewriting data and hashes. Identity is included in each hash. Ordinary writes verify affected old records/hashes and update only declared keys. Full load/audit recomputes all hashes and aggregate, detecting corruption in unrelated rows then. Do not claim every ordinary command verifies unrelated disk contents.

## Required checkpoint sequence

1. S05p: align these documents, isolated native type/DDL/query checks, independent Luna-high review, local commit. No production migration yet.
2. S05a: append v3 migration and typed loader/legacy compatibility only, keep pilot dormant; prove exact round trips, migration rollback/source digest/reset/backup/old caller parity and local commit after review.
3. S05b: typed direct refill writer, incremental hashes, exact receipt reconciliation/faults and bounded query evidence; dormant, review and local commit.
4. S06: ordered worker/admission/publication and actual pilot UI switch with same-save mixed-call safety; review/build/commit. No public synchronous full-profile fallback.

Managed Windows and Android native paths must verify integer bit binding/reading before an Android release containing migration. The isolated ctypes probe only supports the representation choice; it cannot waive either gate. Each later family is still a separately reviewed/committed increment. S13 final callsite audit includes both new UI and desktop F8; S14 must not declare completion while runtime manipulation or normal save projection still relies on JSON or full-profile cloning.
