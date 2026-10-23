# S05a1 — independent typed record and SQLite bit-roundtrip matrix

Status: implementation/test plan for dormant codec foundation only. The assigned Editor checks call the frozen `TypedStateRecord`, `TypedStateEncoding`, `TypedFluidStateRecord`, and current managed `SqliteNative` APIs. They do not exercise schema v3, migration, save/load, the refill writer, profile cloning, or runtime JSON. The expected byte/hash literals below come from an independent field-by-field codec derivation (coordinator Python `struct`/`hashlib`), never from production encoding at test time.

## Canonical record and key vectors

| Case | Input | Required assertion |
| --- | --- | --- |
| Full typed-row golden | Table `probe`, key `key`, ordered fields `Null("text")`, `Text("empty", "")`, `Int64("count", -1)`, `Text("unicode", "é·🚗")`, `Int64("zero", long.MinValue)`. | `CanonicalBytes()` equals exactly `11000000647261676c61622d7265636f72642f76310500000070726f6265030000006b65790500000004000000746578740005000000656d707479010000000005000000636f756e7402ffffffffffffffff07000000756e69636f64650108000000c3a9c2b7f09f9a97040000007a65726f020000000000000080`; `HashHex()` and `HashBytes()` equal `5b66c53abb3159ed49ed2295f6094fc9e47dfc86cbb09434efbf19ede7ff7f93`. |
| Null vs empty | Same record key/table and field name, one row with explicit `Null`, one with `Text("value", "")`. | Canonical bytes and hash differ. SQL values preserve null vs empty string. |
| Field order and culture | Use same ordered typed values under invariant, `fr-FR`, and `tr-TR` current/UI cultures; reverse two fields for a separate record. | Culture does not change bytes/hash; field reordering does. Restore both process cultures in `finally`. |
| Composite key golden | Ordered `Text("owner_kind", "car")`, `Text("owner_id", "a\nb")`. | Key equals exactly `0a0000006f776e65725f6b696e640103000000636172080000006f776e65725f69640103000000610a62`; this encoding has no record-domain prefix or field-count prefix. |
| Delimiter collision | Compare values `("car", "a:b")` and `("car:a", "b")` under a naive `string.Join(":", values)`, then encode them under the same ordered field names with `CompositeKey`. | Naive values collide (`car:a:b`); length-framed composite keys differ. Swapping ordered key fields also differs. |
| Hash contract | Independently verify the fixed expected row bytes/hash and composite bytes against `S05P_DECISIONS.md` and its recorded Python vector; inspect method outputs only as actuals. | No runtime method or helper is used to calculate the expected bytes/hash. Hex output is lowercase. |

## Construction and immutable ownership

- Copy a caller-owned `List<TypedStateField>` into a record, then replace and clear the original list. The record's fields, `SqlValues()`, canonical bytes, and hash remain unchanged.
- Mutate each returned `CanonicalBytes()`, `HashBytes()`, `Fields()` and `SqlValues()` array, then request each value again. None of those mutations changes the record or its later hash/SQL values. Field objects exposed by `Fields()` remain immutable.
- Reject duplicate field names with ordinal comparison, null fields, empty table/field names, null text values (caller must use `Null`), invalid UTF-16 such as an unpaired surrogate in a field name or text value, and an empty composite-key field list. Do not add arbitrary byte-count, string-length, field-count, or domain ID ceilings beyond checked codec representability.
- Accept an empty non-null generic record key and reject a null key; do not assume a new restriction. This foundation preserves opaque typed keys; domain-specific owner IDs remain nonempty in `TypedFluidStateRecord`.

## Typed fluid row fidelity

The test creates `FluidState` fixtures by assigning each of their 14 named fields explicitly: oil/coolant litres, oil temperature, oil/coolant pressure, oil condition, oil consumed/leaked, coolant lost, service elapsed, and oil burn/leak plus coolant leak/vent rates. It must not use reflection, `JsonUtility`, `MechanicsOwnership.Ensure`, a profile copy, or the production encoder to generate expected values.

| Case | Setup | Required assertion |
| --- | --- | --- |
| All-field exact vector | Use distinct finite values, explicitly construct negative zero with `BitConverter.Int64BitsToDouble(long.MinValue)` in coolant litres, and include `nextafter(1.0,+∞)` in another field. Save each expected bit with `BitConverter.DoubleToInt64Bits` before capture and assert coolant bits equal `long.MinValue`. | `FromFluidState` → `Restore` preserves all 14 exact `Int64` bit patterns; independently mutate each one of the 14 fields and require the typed-row hash to change. Mutating the original source after capture and mutating one restored `FluidState` do not alter the stored row or a later restore. Compare all 17 emitted field names/order against a literal list. |
| Finite out-of-domain values | Assign finite but invalid domain ranges (negative quantities/history/rates, out-of-range condition/pressure). | Codec capture/restore preserves the bit patterns without clamp, healing, or domain normalization. Existing `FluidState.Validate` remains responsible for rejecting domain-invalid finite values; the codec must not normalize them. |
| Nonfinite values | For each of the 14 fields in turn, replace one finite value with NaN and positive infinity; separately construct a typed fluid row carrying a nonfinite bit pattern and parse it. | Capture and `FromStateRecord` reject nonfinite fluid values. No normalization or replacement value is returned. |
| Typed row shape | `AsStateRecord()` for a car and an engine; attempt parse with wrong table, bad composite owner key, unsupported `value_version`, reordered fields, missing/extra field, and nonfinite bits. | Valid row has exactly 17 ordered SQL fields: owner kind, owner ID, value version, then the 14 declared `*_bits` columns. Invalid table/key/version/order/count/nonfinite cases reject before `Restore`. Car/engine owner kind only; owner ID nonempty. |

## Managed native SQLite round trips

Use the actual `SqliteNative(":memory:")` P/Invoke path. Create temporary test tables only; do not call migration or any ProfileStore API. SQLite expected values are literal input integers or independently saved bit patterns, never a production-generated expected result.

1. Bind and read `long.MinValue`, `long.MaxValue`, zero, positive/negative one, the negative-zero bit pattern, the largest finite positive-double bit pattern, and 1,000 deterministic xorshift-generated arbitrary signed 64-bit values. For each value, bind both slot and value with prepared-statement `Statement.Execute` parameters (never SQL literal concatenation), read through `column_text`, parse with `long.Parse(..., NumberStyles.Integer, CultureInfo.InvariantCulture)`, and compare exact signed bits. Seed and PRNG recurrence are fixed in source so reruns are reproducible.
2. Insert one `TypedFluidStateRecord.AsStateRecord().SqlValues()` vector into an in-memory table with the exact 17 columns. Query all columns back; compare each text/version/integer against the captured typed field. Reconstruct a fresh `TypedStateRecord` from the queried values, pass it through `FromStateRecord`, call `Restore`, and compare all 14 bit patterns to the independently captured original values.
3. Use bound prepared-statement parameters to round-trip SQL NULL, empty text, Unicode plus embedded NUL; assert they remain distinct. Assert `SELECT sqlite_version()` returns a nonempty version only as identification. This check establishes managed wrapper behavior for this host's bundled SQLite. A parent-owned prototype separately observed the current bundled SQLite in-memory INTEGER bit-pattern behavior; see [S05p proposal checks](../../reports/save-refactor/S05P_PROPOSAL_CHECKS.md) and its [reproducible probe](../../tools/probe_save_refactor_sqlite_types.py). Neither is this C# wrapper test nor a save/migration execution. It does not prove packaged Windows player behavior, Android NDK/plugin behavior, migration safety, or production persistence. Run the managed C# wrapper against Windows and Android native libraries before typed migration release.

## Entry point, fixtures, and output boundary

Parent runs `DragLab.Editor.StateCommandCheckpointVerification.RunTypedStateRecord()`. That wrapper runs `TypedStateRecordChecks.RunStandalone()` followed by the existing isolated 72-check `SqliteStateVerification.Run` coverage. S05a1's own output is only `reports/save-refactor/S05A1/typed-record-checks.txt`, written with explicit UTF-8; it must not overwrite `reports/sqlite-state/verification.txt` or other existing reports. The codec checks use only explicit in-memory values and `:memory:` SQLite; no versioned player fixture is needed.

This agent did not run Unity or mutate a database. The coordinator records the actual Editor invocation, pass count, failure count, compiler/native output, and the separate legacy-storage 72-check result in the S05a1 result/ledger. A passing dormant check authorizes no schema change, migration, writer, save caller, or Android release by itself.
