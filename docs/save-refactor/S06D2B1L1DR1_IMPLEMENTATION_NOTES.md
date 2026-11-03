# L1D R1 typed reservation values: implementation notes

This checkpoint adds the bounded typed value/context model and its explicit legacy import boundary. It does not activate a query caller, edit profile loading, or change garage business behavior.

## New source surface

- `Game/Assets/DragLab/Scripts/Domain/ReservedBuildValues.cs` contains private owned typed copies, explicit field comparers, ordered slot-label baselines, per-job context/results, and the branch outcome helper.
- `Game/Assets/DragLab/Scripts/Infrastructure/LegacyReservedBuildImport.cs` contains the only new `JsonUtility` and legacy hash route. `FromLegacyOwner` is for an already materialized/validated profile; `FromLegacy` is available for one explicit row.
- Matching Unity metadata files were added for both source files.

`ReservedBuildValues` owns copied `FuelState`, `FluidState`, `Tune`/`AwdSpec`, and ordered `PartInstance`/condition/element/clutch values. Its only accessors return further copies or scalar strings. It does not retain the input profile, car, part, installed build, job, source arrays, or exceptions. Capture does not validate, normalize, seed, repair, allocate IDs, or ensure missing members.

The typed comparer explicitly covers the committed field inventory. It distinguishes exact hash-baseline comparison (bitwise float/double payloads) from JSON fallback comparison (`Single.Equals`, and doubles equal by `==` or within four ULPs when finite and same-sign). It compares all four tire elements through the serialized `elements` array and ignores nonserialized compatibility views. Element runtime types must match.

## Import and query-side result order

The importer duplicates the frozen legacy `Snapshot` and `Contents` algorithms in their original ordering: fuel, fluids, tune, paint, ordinal part-instance ordering, first matching profile part, and the existing `JsonUtility` calls. It records import exceptions as owned type/message/parameter strings rather than retaining exception instances. `FromLegacyOwner` requires an already materialized and legacy-validated profile with non-null job rows and valid job-to-car references; it rejects violations as invalid importer preconditions rather than inventing identifiers or masking the earlier business-validation failure. It is not the decoder or validator for malformed pre-validation profile state.

`ReservedBuildImportResult.MatchCurrent` exposes the observed state as one of `HashError`, `CaptureError`, `HashMatched`, `StalePayload`, `JsonTextMatched`, `JsonExpectedMatched`, `FallbackAbsent`, `FallbackError`, or `Mismatch`. It checks, in order:

1. deferred error from the literal legacy hash calculation;
2. exact hash baseline, only when import hash matched or the context was created from typed values;
3. a separately recorded typed-baseline capture error if the hash matched but that additional typed capture could not be made;
4. stored payload digest after a hash-baseline miss;
5. exact typed JSON-text baseline;
6. decoded typed fallback, absent outcome, or deferred fallback error.

The `HashMatched` result does not inspect the JSON digest or fallback. `FallbackMatchedAtImport` is diagnostic only. A decoded `JsonExpected` is retained even when it did not match at import. A null and an empty `snapshotJson` have distinct digest bindings (a separate null flag plus the SHA-256 of the empty byte sequence) but both map to legacy fallback-absent behavior. Import-owned value objects are deep-copied once from mutable inputs and then safely shared among immutable baselines; any getter returns a defensive copy. Per-match comparisons read the candidate's single owned capture directly without allocating more snapshots.

The outcome helper reports stored errors as metadata; it does not construct or throw an arbitrary exception type. A future query adapter must map those outcomes at the existing row position and use only an explicitly reviewed exception mapping. No current caller was changed to do so.

## Validation status and limits

Coordinator validation after source release: E passes 7,730 helper assertions with zero failures/OPEN, actual Unity exit 0 and empty runner-error. A Windows build passes on unchanged runtime feature sources; post-build checker changes are wholly Editor-only and exclusion is proved. Independent Luna-high gives bounded R1 GO. See the [results](../../reports/save-refactor/S06D2B1L1DR1_RESULTS.md) and [review](S06D2B1L1DR1_IMPLEMENTATION_REVIEW.md); earlier A–D diagnostics remain retained with their limits.

No Unity, build, process, Git, or test run was performed during this source lease. The parent owns Unity execution after all source leases are released and the full source set is frozen. The R1 checker is independently owned by the test author and must verify the actual outcome helper against separate frozen references and literal vectors.

This is a dormant prerequisite only. `GarageBusiness`, `SnapshotEquality`, profile stores, assignment, finish, validation, and query callers remain unchanged. The importer is not a runtime/cache-miss fallback. The model also does not claim to reproduce every future Unity JSON spelling collision from typed data alone.
