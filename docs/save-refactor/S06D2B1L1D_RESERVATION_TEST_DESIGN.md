# L1D reserved-build validation prerequisite — test design

Status: **design only; no implementation, schema migration, command rewrite, or query approval**. This note defines the smallest test boundary needed to replace active garage-job JSON comparison in quote validation. It does not authorize a new business command flow or a general save migration.

## Source behavior that must remain distinguishable

`GarageBusiness.Validate` checks the job/car reservation metadata and then calls `Matches`. `Matches` is ordered:

1. Compare `GarageBusiness.Snapshot(profile, car)` with `job.snapshot`. If equal, return true without examining `snapshotJson`.
2. Otherwise, only a nonempty `snapshotJson` permits fallback to `SnapshotEquality.Serialized<ReservedBuildSnapshot>` against `Contents(profile, car)`.
3. Empty/missing `snapshotJson` after a hash mismatch fails the reservation.

The legacy hash is SHA-256 over UTF-8 bytes of serialized fuel, fluids and tune, followed by `paintHex`, then each fitted part in ordinal `partInstanceId` order as `slot + serialized PartInstance`. It is not the same shape as `ReservedBuildSnapshot`: that payload contains fuel, fluids, tune, paint and the ordered part array, but no slot strings.

The serialized fallback first accepts exact `JsonUtility.ToJson(current) == storedJson`. Otherwise it decodes the stored `ReservedBuildSnapshot` and recursively compares public instance fields, excluding `[NonSerialized]` fields. The comparison requires identical runtime types, exact primitive/string/enum/decimal values, exact sequence length/order, and null symmetry. `double` fields are exact when equal; otherwise both values must be finite, same-sign, and within four representable ULPs. A double field named `health` is always exact. Floats receive no ULP allowance. Malformed JSON follows the existing decoder/comparer failure behavior and must be characterized from the frozen legacy reference, not converted to success.

`MechanicsOwnership.Ensure` also calls `GarageBusiness.UpgradeLegacySnapshots` late in normalization. For a job with empty `snapshotJson`, it writes serialized `Contents` only if the current legacy hash equals the job hash. That normalization is mutating and is not part of query validation. `ProfileStore.Load` has named decode/validation paths (`LegacyProfileDecoder.DecodeAndValidate` for migrated raw state and `ValidateLoadedV12` for current state); `ProfileStore.ImportLegacy` routes through `LegacyProfileStore.Load`, whose normalization behavior must be pinned before selecting the typed-view import boundary.

These distinctions prevent two false conclusions: an active job with a matching legacy hash does not prove the serialized fallback is correct, and an empty-job or already-upgraded fixture does not prove active-job validation is JSON-free.

## Narrow typed-view boundary to review

The query should read an immutable, owned reserved-build view already available from the named profile load/import path. Newly assigned jobs must receive the same typed view at the existing creation boundary; this is only the minimum necessary to make the query path independent of JSON, not authorization to redesign Assign/Finish, rewards, receipts, release, or job persistence. Keep the existing `snapshot` and `snapshotJson` meanings available as migration evidence until compatibility is independently approved.

High must settle these exact points before implementation:

- Which load/import boundary constructs the view for typed and legacy saves, and whether a current contract profile is validated or rejected when the view is absent.
- For a hash-only job, permit conversion only if the existing hash matches the current source contents at that boundary; otherwise preserve it as unresolved/invalid. Never synthesize acceptance from the current car when its hash mismatches.
- For a job with `snapshotJson`, decode/validate once at the boundary into the typed representation, preserving the source string/hash and whether exact-string or structural-tolerance equality accepted it. Malformed nonempty payloads must fail with the frozen legacy first error; do not silently fall back to the hash when that hash mismatches.
- On creation, capture the immutable view from the actual assigned car and fitted parts. Do not re-run normalization, manufacture components, or clone the owner/profile.
- Keep persisted fields and versioning unchanged unless independent review proves a new serialized field is unavoidable. A schema change is outside this bounded prerequisite.

The query must never parse `snapshotJson`, call `SnapshotEquality.Serialized`, recompute legacy JSON snapshots, or fall back to `GarageBusiness.Validate`/`Matches`. Import-boundary decoding is explicit and separately measured; it is not query JSON manipulation. The view must contain only the reserved car contents needed for comparison, with concrete typed fields and immutable ordered part/member state. It must not retain a `PlayerProfile`, live `OwnedCar`, `PartInstance`, catalog, or mutable collection reference.

## Finite fixture matrix

Use actual authored valid cars, parts, and assigned jobs. Seed any legacy bytes/fields explicitly on isolated test profiles; do not invent physical parts. Each test first validates the unrelated listing, economy, career, inventory, and market gates so the intended business branch is reached. Add one paired control with an earlier failure to pin precedence.

| Case | Frozen legacy expectation to pin | Typed-view/query assertion |
|---|---|---|
| Newly assigned unchanged job: hash and nonempty JSON both present | Hash branch succeeds first; record whether the JSON decoder was not reached. | Creation captures the same reserved values once; repeated Total sale validation and Combined use the owned view and issue zero query JSON calls. |
| Matching hash with malformed nonempty `snapshotJson` | Hash success short-circuits malformed JSON; validation succeeds without decoding fallback. | Import boundary behavior is separately explicit; runtime view lookup never parses the malformed string. Do not change the old hash-first compatibility rule. |
| Hash-only job, current contents match | `Ensure` migration fills `snapshotJson` only at the documented normalization boundary; read-only `Validate` itself does not write it. | Import converts to a typed view without mutating the source job during a query. Record which boundary is permitted to materialize this legacy state. |
| Hash-only job, current contents differ | `Matches` rejects; `UpgradeLegacySnapshots` must not fill a payload because the hash mismatches. | Typed import leaves the reservation unresolved/rejected; no current-content snapshot is substituted and no query healing occurs. |
| Hash mismatch, serialized snapshot exact match | Structural fallback succeeds via exact serialized equality. | Typed imported view succeeds with exact field/order parity and no query serializer/decoder. |
| Hash mismatch, serialized double differs by 1 ULP and by exactly 4 ULPs | Fallback succeeds for eligible finite, same-sign, non-`health` doubles. | Typed comparer preserves the same tolerance without converting through JSON or changing either value. |
| Hash mismatch, serialized double differs by 5 ULPs, opposite sign, NaN/infinity, or `health` differs by 1 ULP | Fallback rejects. | Typed comparer rejects each case with exact source values and references unchanged. |
| Float/primitive/string/null/order/type controls | Fallback requires exact float and primitive values, exact sequence ordering/length, runtime type identity, and null symmetry. | Typed view comparison matches each rule; include reordered fitted parts and null-vs-empty arrays where the old representation can express them. |
| Missing car, broken `garageJobId`, stale engine ID, part-ID mismatch, duplicate job/car identity | Pin the first exact `GarageBusiness.Validate` error/result before and after each single corruption. | Typed view must not bypass metadata validation. A multi-corruption fixture pins its precedence relative to the earlier `VehicleListings`, `PaidEconomy`, and `CareerProgression` gates. |
| Missing, empty, malformed, and valid-but-stale `snapshotJson` | Pin the old exact false/exception result for hash mismatch; separately prove a matching hash short-circuits all four payload variants. | Import rejects or marks unresolved using an explicit contract; query has no fallback or data repair. |
| Active job plus ordinary sale, and active job plus malformed reservation | Frozen quote validation reaches business validation only after earlier global checks; capture exact first error and whether later inventory validation ran. | For valid reservation, typed Total/Combined succeeds to the next expected gate with zero query JSON. For invalid reservation, typed validation returns the same accepted error semantics without parsing/repairing source data. |

For ULP fixtures, create representable neighbors from the exact source `double` using bit-level next-value helpers in the Editor test only. Pin signs, finite status and `health` field name explicitly. The legacy JSON roundtrip may change decimal spelling; keep the original serialized payload fixed and vary only the current typed value. Do not calculate the expected tolerance with the candidate comparer.

## Error ordering and independent oracle closure

`ValidateForQuote` runs `Validate` first and only retries `ValidateCore(..., true)` when the first error text contains “dealer”. `ValidateCore` orders selected-car checks, `VehicleListings.Validate`, paid-economy validation, career validation, then `GarageBusiness.Validate`, before shared inventory and market validation. Keep both dealer-discriminator passes intact. Business tests must use otherwise-valid fixtures to reach the intended job branch, then add a separate pair of simultaneous failures to show that an earlier gate still wins.

Before changing the live business/snapshot path, freeze the exact old `GarageBusiness.Validate`, `Reserved`, `Matches`, `Snapshot`, `Contents`, and `SnapshotEquality.Serialized`/`Same` bodies used by the old validation oracle. `LegacyCartQueryValuationReference` currently reaches live `GarageBusiness.Validate`; that is not an independent oracle for a changed business implementation. Its reference closure must be pinned/routed independently before any shared production edit. The frozen reference may use JSON on isolated fixtures; the typed query may not.

## Acceptance checks for this prerequisite

- Run the matrix through the named decode/import/creation boundary, then all candidate purposes that can reach owner validation; verify typed-view values and source-state invariance field by field.
- Count `JsonUtility.ToJson` and `FromJson` over the full query, including active valid, hash-only, stale, malformed, and fallback-shaped jobs. Query count must be zero. Keep import-boundary decode counts separate and explicit.
- Assert source job strings/hash, job/car links, fitted bindings, fuel/fluids/tune/paint, actual part/member IDs and values, public catalog indexes, and references remain unchanged on both success and failure.
- Assert the view and query result remain valid after input reuse and scope disposal but retain no owner/job/car/part/catalog/provider references.
- Keep the 730/0 Reference B result labeled legacy liveness only and all 29 `LEGACY_PURITY|FAIL` observations as required candidate divergences. A passing old oracle does not waive candidate no-JSON or no-source-mutation requirements.

This is a finite prerequisite for the active-job validation seam only. It does not authorize rewriting `GarageBusiness.Assign`, `Finish`, business income, receipts, UI, or schema; it does not complete L1D, S06, or later S07–S14 work.
