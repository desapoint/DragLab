# S05a1 — typed row codec foundation

Status: infrastructure foundation only. These two small immutable value types define typed-row canonicalization and the v1 fluid row shape. They do not read or write SQLite, alter a migration/schema version, load a profile, or activate the refill writer. S05a1 must remain dormant until the separately reviewed migration/loader and writer checkpoints.

## Frozen API

- `TypedStateField.Null(name)`, `.Text(name, value)`, and `.Int64(name, value)` create immutable fields. A text field cannot carry SQL NULL; use `Null` for that. `Name`, `Kind`, `TextValue`, and `Int64Value` are readonly. `Copy()` returns an equivalent independent immutable field.
- `TypedStateRecord(tableName, recordKey, fields)` copies the supplied fields and rejects null fields, duplicate names, empty table/field names, a null key, invalid UTF-16/strict UTF-8 conversion, or unrepresentable buffer/count sizes. It retains exact field order in a private array and shares only immutable `TypedStateField` instances. It exposes a cloned field array via `Fields()` (`TypedStateField[]`), `SqlValues()` (`object[]` containing only null/string/boxed `long`), `CanonicalBytes()`, `HashBytes()`, lowercase `HashHex()`, and typed `ReadText(name)` / `ReadInt64(name)` accessors. Returned arrays are defensive copies. Invalid UTF-16 throws `EncoderFallbackException`; invalid names/arguments and duplicate names throw argument exceptions; missing fields throw `KeyNotFoundException`; wrong read type throws `InvalidOperationException`.
- `TypedStateEncoding.CompositeKey(orderedKeyFields)` concatenates each named type-tag/value encoding exactly in supplied order, without a domain prefix or field count, then lowercases the hexadecimal bytes. `SingleTextKey(value)` returns the exact string for a single text primary key.
- `TypedFluidStateRecord.FromFluidState(ownerKind, ownerId, source)` captures one immutable v1 row; `AsStateRecord()` returns fields in the proposed `fluid_states` DDL order; `FromStateRecord(row)` validates table, composite key, version and exact field ordering before reading; `Restore()` returns a new detached `FluidState`. `car` and `engine` are the only owner kinds. This layer rejects nonfinite fields on capture and typed-row reconstruction, but leaves domain range/relationship validation to existing authoritative mechanics validation. It does not normalize or heal values.

## Canonical encoding

The exact codec is the one pinned in `S05P_DECISIONS.md`:

- `Text(s)` is unsigned 32-bit byte length (little endian) followed by strict UTF-8 bytes. Null is represented only by the field null tag, never as an empty string.
- A typed row hashes `Text("draglab-record/v1")`, `Text(tableName)`, `Text(recordKey)`, uint32 field count, then each field in order: `Text(fieldName)`, tag `00` for null; tag `01` plus `Text(value)` for text; tag `02` plus signed 64-bit little-endian integer for integer values. Double values use their signed IEEE-754 binary64 bits in that Int64 representation. `SHA256` covers the full canonical row bytes.
- Composite row keys are lowercase hex of ordered key field encodings (name, tag, and value); no delimiter-joined key strings. Fluid keys encode `owner_kind`, then `owner_id`.
- The fluid DDL order is owner kind, owner ID, value version, then the fourteen `*_bits` columns in listed schema order. Each field is stored independently; this is not a whole-profile blob. Capture/restore uses `BitConverter.DoubleToInt64Bits` / `Int64BitsToDouble`, retaining signed zero and every finite value's exact representation. Lower-level Int64 fields permit every 64-bit pattern.

Only strict encoding and representability checks are imposed here. This foundation does not invent a GUID/ID length limit or narrow existing domain ranges. The supplied record has to fit the process's checked byte-array/memory limits and the codec's uint32 length/count framing; overflow throws before a partially constructed record can be used.

## Verification boundary

Static source review confirms the codec has no JSON/reflection serialization or SQLite dependency. The S05a1 test owner should verify deterministic golden row/key vectors, strict UTF-8 failures, null-versus-empty, duplicate field rejection, defensive copies, all 64-bit integer values, and exact round-trip of all fourteen fluid fields including negative zero and adjacent values. These tests have not been run by this author. Managed Windows SQLite and Android interop remain native gates; no migration or database execution is claimed here.
