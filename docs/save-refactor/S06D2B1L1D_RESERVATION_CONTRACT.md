# L1D reserved-build values and import contract

Status: **bounded prerequisite proposal; documentation only**. Baseline production remains `e261080`. Read with the [validation seam proposal](S06D2B1L1D_VALIDATION_SEAMS_PROPOSAL.md), [reservation test design](S06D2B1L1D_RESERVATION_TEST_DESIGN.md), [query design](S06D2B1L1D_CART_QUERY_DESIGN.md), [implementation-preparation review](S06D2B1L1D_IMPLEMENTATION_PREPARATION_REVIEW.md), and [Reference B results](../../reports/save-refactor/S06D2B1L1D_REFERENCE_RESULTS.md). This proposal authorizes no source edit, query caller activation, save/schema migration, or S10 garage-business rewrite.

## Decision and bounded goal

Introduce an owned typed representation of the reserved build and one explicitly named legacy import boundary. A dormant query receives that prepared reservation context as an explicit input. It never parses `snapshotJson`, regenerates a legacy hash, serializes current contents, calls `SnapshotEquality`, or falls back to `GarageBusiness.Matches` after entry.

Keep the current `GarageBusiness` and `SnapshotEquality` methods byte-identical in this prerequisite. Keep their current validation and persistence behavior until a later reviewed caller cutover. This small prerequisite supplies a testable typed importer/comparer and does not create new business behavior. It does not activate any live load or assignment hook: test fixtures and later owner adapters explicitly prepare the context before calling the dormant query.

## Current source and storage facts

`GarageBusiness` declares `AssignedGarageJob` with `id`, `offerId`, `name`, `carId`, `engineId`, `snapshot`, `snapshotJson`, `partIds`, start/ready ticks, and reward. `ReservedBuildSnapshot` is the existing serialized shape `{ fuel, fluids, tune, paint, parts }`. `Contents` orders fitted slots by ordinal `partInstanceId`, resolves each part from `profile.parts`, and serializes the resulting ordered part array. The separate legacy hash includes the serialized fuel, fluids, tune, `paintHex`, then each ordered slot name and serialized `PartInstance`.

`Matches` is hash-first: if the current hash equals `job.snapshot`, it returns true without reading `snapshotJson`. Only on hash mismatch with a nonempty `snapshotJson` does it call `SnapshotEquality.Serialized`. That fallback first compares exact JSON text; otherwise it decodes `ReservedBuildSnapshot` and compares public instance fields recursively. It requires exact runtime types, primitive/string/enum/decimal equality, null symmetry, and exact list/array order and length. Doubles match exactly first, otherwise only when finite, same-sign, within four ULPs, and the field name is not `health`. Floats have no tolerance. `health` uses exact equality. Malformed payload behavior and exception ordering remain characterized by the frozen legacy reference; the importer must retain the result rather than silently accept or repair it.

`LegacyProfileDecoder.DecodeAndValidate` and `ValidateLoadedV12` run before `ProfileStore.Load` hands out a current typed profile. `ValidateLoadedV12` installs the catalogue and calls current `MechanicsOwnership.Validate`, so existing load validation can reach the legacy job matcher. The current SQLite loader materializes the profile JSON projection, applies `SqliteTypedProfileData`, then calls `ValidateLoadedV12`; the typed projection currently owns economy, fluid rows and receipts, not garage-job snapshots. `LegacyProfileStore.Load` performs its existing normalization path, including `MechanicsOwnership.Ensure` on supported saves. These load-time behaviors remain unchanged here.

No persistence field needs to be added for this prerequisite. The current job hash and JSON string remain untouched in their existing profile projection. The typed reservation context is ephemeral and reference-free; it is rebuilt from those persisted values at a named import boundary. This plan does not bump the profile/mechanics contract or SQLite schema version. Persisting a new typed reservation record, retiring either legacy field, or changing the storage projection requires a separately reviewed later S11 migration with rollback and integrity coverage.

## Proposed owned API

Add a pure-value runtime type file, `Game/Assets/DragLab/Scripts/Domain/ReservedBuildValues.cs` (+ matching `.meta`), and an import-only infrastructure file, `Game/Assets/DragLab/Scripts/Infrastructure/LegacyReservedBuildImport.cs` (+ matching `.meta`):

```csharp
internal sealed class ReservedBuildValues
{
    internal static ReservedBuildValues CaptureCreated(PlayerProfile owner, OwnedCar car);
    internal bool MatchesCurrent(ReservedBuildValues current,
        ReservedBuildComparisonMode mode);
}

internal sealed class ReservedBuildHashBaseline
{
    internal ReservedBuildValues Values { get; }
    internal string[] OrderedSlotLabelsCopy();
    internal bool MatchesCurrent(ReservedBuildValues current,
        string[] orderedSlotLabels);
}

internal sealed class ReservedBuildContext
{
    internal static ReservedBuildContext FromCreatedJobs(
        PlayerProfile owner, IEnumerable<AssignedGarageJob> createdJobs);
    internal static ReservedBuildContext FromImportedResults(
        IEnumerable<ReservedBuildImportResult> orderedResults);
    internal bool TryGet(string jobId, string carId, string snapshotHash,
        out ReservedBuildImportResult result);
}

internal sealed class ReservedBuildImportResult
{
    internal string JobId { get; }
    internal string CarId { get; }
    internal string SnapshotHash { get; }
    internal string SnapshotJsonDigest { get; }
    internal ReservedBuildImportKind Kind { get; }
    internal ReservedBuildValues JsonTextBaseline { get; }
    internal ReservedBuildValues JsonExpected { get; }
    internal ReservedBuildHashBaseline HashBaseline { get; }
    internal ReservedBuildFallbackKind FallbackKind { get; }
    internal bool FallbackMatchedAtImport { get; }
    internal bool FallbackBindingMatches(string snapshotJson);
    internal string HashErrorType { get; }
    internal string HashErrorMessage { get; }
    internal string HashErrorParameterName { get; }
    internal string ErrorType { get; }
    internal string ErrorMessage { get; }
    internal string ErrorParameterName { get; }
}

internal enum ReservedBuildImportKind
{
    CreatedTyped, HashMatchedAtImport, HashMismatchAtImport
}

internal enum ReservedBuildFallbackKind
{
    Absent, Values, Error
}

internal enum ReservedBuildComparisonMode
{
    ExactHashBaseline, LegacyJsonFallback
}

// In Infrastructure/LegacyReservedBuildImport.cs only:
internal static class LegacyReservedBuildImport
{
    internal static ReservedBuildContext FromLegacyOwner(PlayerProfile owner);
    internal static ReservedBuildImportResult FromLegacy(
        PlayerProfile owner, OwnedCar car, AssignedGarageJob job);
}
```

Names are concrete proposal surface, subject to high review before implementation. `FromLegacyOwner` visits only existing job rows and their referenced cars/installed parts; it does not make a profile copy. Each result is bound to the exact job ID, car ID and `snapshot` value, plus a SHA-256 digest of raw `snapshotJson` bytes for the stored fallback. `HashBaseline` is present only for `HashMatchedAtImport` and `CreatedTyped`; it is null for `HashMismatchAtImport`, so a currently mismatching build can never be reclassified as an accepted hash baseline. Query validation first verifies the job/car/hash key. It checks the JSON digest only after the hash baseline misses and before using either JSON fallback observation; a matching hash baseline remains independent of any JSON payload change. It never trusts a caller-supplied `bool valid`, accepts a context for a different job, or treats a missing view as success. The context owns an ordered array of results and retains no owner, job, car, part, catalogue, delegate, exception, or mutable collection reference.

`CaptureCreated` copies the exact current reserved contents directly into typed values. `FromCreatedJobs` wraps those values for a just-completed assignment; it is an owner-adapter boundary, not a change to `GarageBusiness.Assign`. A future live owner integration must refresh that context immediately after assignment and rebuild it after every successful ProfileStore/LegacyProfileStore load or reload, before the profile is offered to the query. Until that caller conversion is separately approved, the new query remains dormant and requires a test- or caller-owned context explicitly.

`Infrastructure/LegacyReservedBuildImport.FromLegacy` is the only new method allowed to call `JsonUtility.ToJson`/`FromJson` or compute the old serialized hash. `FromLegacyOwner` walks existing jobs and calls `FromLegacy` at an already validated load/import boundary. These methods run synchronously before query entry. The importer reproduces the old hash and `Contents` field ordering as a separately pinned legacy boundary; it does not modify or route through `GarageBusiness.Snapshot`, `Contents`, `Matches`, or `SnapshotEquality`. For every job it precomputes (a) an exact typed hash baseline where available, (b) an optional typed baseline for the legacy exact-JSON-text branch, and (c) the decoded JSON fallback result or its deferred error. Those branches are stored separately and are consulted in legacy order. It does not write `snapshot`, `snapshotJson`, job metadata, source part values, or any save. It retains only its typed result and scalar binding data after returning. If implementation cannot keep this boundary isolated from query evaluation, stop and revise the method boundary before source edits.

## Owned field inventory and comparison rules

`ReservedBuildValues` matches exactly the `ReservedBuildSnapshot` content: nullable `FuelState`, nullable `FluidState`, nullable `Tune`, paint string, and a nullable ordered `PartInstance[]`. Preserve null versus empty arrays and null entries where the legacy JSON representation can express them. Preserve the source `Contents` ordering (parts ordered ordinally by their installed `partInstanceId`). Do not sort or normalize a second time in the typed comparer. The legacy hash has one additional input absent from `ReservedBuildSnapshot`: the ordered binding slot strings. `ReservedBuildHashBaseline` therefore owns the bounded values plus a parallel ordered copy of `InstalledBuild.slots[].slot` in the same ordinal `partInstanceId` order as the hash routine. It contains no other car, binding or profile state.

Capture uses explicit typed copies only: `FuelState.Copy`, `FluidState.Copy`, `Tune.Copy` (including its nested `AwdSpec.Copy`), and `PartInstance.Copy` with `ComponentCondition.Copy`, nested clutch copy, and an ordered copy of concrete element values. The resulting objects are private, operation-owned copies; the API exposes no domain-object getter, returns only further defensive copies for diagnostics, and never gives those internal mutable objects to a shared rule or result DTO. These methods are typed and avoid a generic serializer. The owned comparer must nevertheless compare fields explicitly; it may not rely on `SnapshotEquality`, reflection, JSON, or a generic field walker.

The comparison schema is the full current public serialized field set for these types, pinned at implementation preflight:

- `FuelState`: `litres`, `cumulativeUsedLitres`, `flowLitresPerSecond`.
- `FluidState`: `oilLitres`, `coolantLitres`, `oilTemperatureC`, `oilPressureBar`, `coolantPressureBar`, `oilCondition`, `oilConsumedLitres`, `oilLeakedLitres`, `coolantLostLitres`, `serviceElapsedSeconds`, `oilBurnLps`, `oilLeakLps`, `coolantLeakLps`, `coolantVentLps`.
- `Tune`: `camTimingDegrees`, `driverMassKg`, `payloadMassKg`, `payloadPositionM`, `payloadHeightM`, `launchRpm`, `finalDrive`, `tirePressurePsi`, `shiftRpm`, `engineLevel`, `tireLevel`, `weightLevel`, `gearboxLevel`, `exhaustLevel`, `inductionUpgrade`, `blowOffValve`, `turboSize`, `boostSetting`, `superchargerPulley`, `controlLevel`, `twoStep`, `launchPreload`, `antiLag`, `limiter`, `twoStepStyle`, `limiterCut`, `twoStepCut`, `revLimitRpm`, `wheelDiameter`, `wheelWidth`, `wheelMass`, `frontWheelDiameter`, `rearWheelDiameter`, `frontWheelWidth`, `rearWheelWidth`, `frontTirePressurePsi`, `rearTirePressurePsi`, `coldStart`, `transmissionUpgrade`, `flywheelMass`, `awd`, `awdConfigured`, `frontLeftGrip`, `frontRightGrip`, `rearLeftGrip`, `rearRightGrip`, `partsResolved`, `partLimiterAdjustable`, `partTwoStepAdjustable`, `partShiftLight`, `partShiftLightAdjustable`, `partTuningConfigured`, `partBoostAdjustable`, `partPulleyAdjustable`, `partValveAdjustable`, `partFinalDriveAdjustable`, `partTurboSize`, `partFixedBoost`, `partBoostMin`, `partBoostMax`, `partFixedPulley`, `partPulleyMin`, `partPulleyMax`, `partFixedFinalDrive`, `partFinalDriveMin`, `partFinalDriveMax`, `partFixedValve`, `shiftLightRpm`, `partGearCapacity`, `partPower`, `partGrip`, `partMass`, `partShift`, `partCapacity`, and `partBoost`. `AwdSpec` compares all its public fields: `mode`, `centerSpeedRatio`, `torqueBiasRatio`, `centerPreloadNm`, `frontBias`, `minimumFront`, `maximumFront`, `primaryFront`, `isolateFailedAxle`, `maximumSecondary`, `responseSeconds`, `slipThreshold`, `preload`, `freeShaftInertiaKgM2`, `freeShaftDragNm`, and `freeShaftViscousDrag`.
- `PartInstance`: `version`, `instanceId`, `definitionId`, `storageId`, `location`, `ownerId`, `slot`, and `condition`.
- `ComponentCondition`: health, failed, failure reason, running seconds, distance, stress exposure, temperature, slip energy, deposits, leak severity, adjustment error, service-wear remainder, service/recondition counts, last-service string, `elements[]`, and nullable `clutch`. `clutchFluidVersion`, `clutchOilLitres`, `clutchOilCondition`, and `tires` are properties, not serialized backing fields; compare their represented fields through `clutch` and `elements[]`, not computed compatibility accessors. Ignore the `[NonSerialized] tireView` cache.
- `ClutchConditionState`: version, oil litres and oil condition.
- Each element compares exact runtime type plus inherited `PartElement` identity/definition/health fields. `EngineElement` adds role, cylinder, valve, severity, wear remainder and fault. `TireCondition` adds failed, temperature, slip energy, distance, last contact work, consumed life, running seconds and failure reason. The supported concrete subclasses (`PistonElement`, `ConnectingRodElement`, `PistonRingElement`, `BearingElement`, `ValveElement`, `ValveSpringElement`, `ValveSealElement`, `FollowerElement`, `WheelElement`, `IgnitionElement`, `SparkPlugElement`, `GlowPlugElement`, `FuelSystemElement`, `TireCondition`, and base `EngineElement` where persisted) retain exact type identity; no conversion between a tire member and another element type is allowed.

For JSON fallback, compare all scalar/string/enum fields as `SnapshotEquality.Same` does: primitive floats use boxed `Single.Equals` (including its NaN and signed-zero behavior); doubles use `x == y` first, then finite, same-sign values within four ULPs, except any double field named `health` is exact. Null symmetry, element runtime type, sequence length and order are exact. The hash baseline uses bitwise equality for every float/double field, including signed zero and NaN payloads, plus exact equality for other scalar fields and ordered slot labels; this avoids accepting a typed change merely because CLR numeric equality collapses bit patterns. These comparisons cannot reproduce all Unity JSON spelling collisions, which are bounded in the next section. Do not compare computed properties or nonserialized caches. If the live schema adds a persisted field, update this explicit DTO/comparer and its tests before changing quote validation.

## Import branch contract

The importer creates a per-job result without throwing a validation outcome ahead of the query's ordinary business-validation position:

| Import case | Import action and retained typed value | Later query behavior |
|---|---|---|
| `LegacySnapshot(profile, car) == job.snapshot` | `HashMatchedAtImport`; retain the exact typed hash baseline, including ordered slot labels. Independently precompute `JsonTextBaseline` when current serialization equals the payload, and decoded `FallbackKind`/`JsonExpected` or deferred error. | Compare current contents and ordered slot labels to `HashBaseline` exactly. Exact match succeeds without consulting either fallback or payload digest. Otherwise check the stored JSON digest, then compare to `JsonTextBaseline` exactly (no separate slot labels); if that misses, consult decoded fallback: `Values` uses legacy rules, `Absent` fails, and `Error` reproduces the stored exception only at this job's validation position. |
| Hash mismatch; `snapshotJson` is empty/null | `HashMismatchAtImport`; precompute `FallbackKind.Absent`, with no expected values. | Once this job is reached, check the bound payload digest, then return the existing invalid/dangling reservation result. Do not synthesize a reservation from current contents. |
| Hash mismatch; nonempty JSON exactly equals legacy `JsonUtility.ToJson(currentContents)` | `HashMismatchAtImport`; store `JsonTextBaseline` as a typed copy of current contents. Independently precompute decoded `FallbackKind`/`JsonExpected` or a deferred error and set `FallbackMatchedAtImport` from the exact-text branch. | Check the bound payload digest, then compare `JsonTextBaseline` exactly, with no separate slot labels. On mismatch, consult the decoded fallback. |
| Hash mismatch; nonempty JSON differs | `HashMismatchAtImport`; decode once to the existing `ReservedBuildSnapshot` shape at this named importer; explicit comparer must reproduce legacy type/null/order/primitive and ULP rules. If decoding succeeds, always retain the typed `JsonExpected` with `FallbackKind.Values`, even when it does not match current contents; record `FallbackMatchedAtImport` separately. If payload is malformed/unreadable, retain `FallbackKind.Error`. | Check the bound payload digest, then compare `JsonTextBaseline` exactly if present. Otherwise compare against `JsonExpected` with `LegacyJsonFallback`, including the legacy four-ULP double allowance. Binding slot labels are not part of these payload branches. A later state that returns to the decoded expected contents must be able to match. No cache miss or later contents change invokes a decoder. |
| JSON fallback decode/comparison throws | Store `FallbackKind.Error` and owned exception type/message/parameter strings, not an exception object. When the hash matched at import, this error remains latent while either exact typed baseline matches. | If neither exact baseline matches, check the payload digest, then surface the stored failure only at the same job validation position; preserve first-error ordering. Never convert an exception into success or an empty snapshot. |
| Newly assigned job | `CaptureCreated`; copy actual contents and ordered slot labels at the assignment boundary directly, with no JSON roundtrip and no identity allocation. The produced record has `HashBaseline`, `JsonTextBaseline`, `JsonExpected`, and `FallbackKind.Values`. | Compare the exact hash baseline first. After it misses, validate the bound payload digest, then compare the exact JSON-text typed baseline, then compare decoded/created expected values with legacy tolerance. A later save/reload reconstructs these from unchanged legacy fields at the import boundary. |

`LegacySnapshot` in the table above is the exact existing hash calculation, including slot-name ordering and the old serializer behavior. The new import implementation must be source-pinned against `GarageBusiness.Snapshot` and tested against the frozen legacy oracle; it must not claim equivalence from merely matching the 64-character hash format.

If computing the legacy hash or capturing its typed baseline fails at import, store `HashErrorType`, `HashErrorMessage`, and `HashErrorParameterName` rather than throwing during context construction. The query replays that error only when it reaches this job's hash comparison, before evaluating either fallback. Failures while computing the exact-text baseline or decoding/comparing its fallback are stored in `FallbackKind.Error` with the same owned error strings; these errors are consulted only after both typed baselines miss. Row-scoped import preparation errors never short-circuit an earlier query guard or row.

### Exact remaining Unity JSON spelling boundary

The import boundary precomputes and stores all reachable branches. At query time the hash baseline is checked first with exact typed equality and ordered slot labels; a match succeeds while both fallback observations remain latent. After a hash-baseline miss, the query verifies the stored payload digest before consulting either fallback view. A digest mismatch means the explicit context is stale: fail closed at this job-validation point and require the owner adapter to rebuild it outside the query; never re-import or decode on this path. Then check the exact-JSON-text typed baseline, which represents the first `JsonUtility.ToJson(currentContents) == storedJson` branch without relying on object decoding. If that also misses, use only the precomputed decoded fallback: the same four-ULP typed comparison, false outcome or captured exception that the legacy fallback would have reached. A hash-matched malformed payload is therefore accepted while its hash baseline matches; its parse error is surfaced only if the baseline misses and the fallback branch is reached. A valid contradictory payload is likewise ignored while the hash baseline matches and becomes the fallback expectation after a baseline change. `FallbackMatchedAtImport` is diagnostic only and never gates later comparisons; retaining `JsonExpected` even after an initial mismatch preserves a later state that returns to the reserved values.

The remaining compatibility restriction is a Unity JSON spelling collision: typed contents can change while serializing to the same JSON bytes. If the hash-only payload has no fallback, a bitwise typed baseline mismatch must fail closed even though the legacy hash might remain equal. The exact-text baseline preserves the observed accepted contents, and the decoded fallback preserves the standard field/tolerance branch. A typed comparer still cannot reproduce every case where future JSON serialization text equals the stored payload despite values outside the field comparer. Signed zero and float NaN equality are explicit controls for this boundary. This narrow byte-spelling edge is the only claimed parity limitation; do not broaden it into dropping the saved fallback branch, and do not claim exact serialized-hash parity without runtime JSON or a separately proven serializer-equivalent encoding.

## Handoff and freshness contract

This prerequisite exposes two explicit producers, not a live hook:

1. Tests call `Infrastructure.LegacyReservedBuildImport.FromLegacyOwner(owner)` on an already materialized, legacy-validated profile. This exercises the bounded importer without changing `ProfileStore`, `LegacyProfileStore`, or `LegacyProfileDecoder`.
2. An assignment adapter calls `ReservedBuildValues.CaptureCreated(owner, car)` after the existing `GarageBusiness.Assign` operation has selected the actual car/parts and created its job. It publishes a new immutable `ReservedBuildContext` for that owner/job set. It does not rerun `Assign`, normalize the profile, alter old JSON fields, or create parts.

Before any caller activation, explicitly wire the equivalent handoffs at these concrete producer points: `ProfileStore.Load` after SQLite typed materialization and strict load validation, the `LegacyFixture`/`LegacyProfileStore.Load` return path, `ProfileStore.ImportLegacy` after its existing import/normalization and save result, and the in-session `GarageBusiness.Assign` completion path. A later successful reload replaces the prior context; a removed/finished job drops its result; assignment adds its newly captured result. These are future caller-integration scopes, not part of this typed-value prerequisite and not permission to edit those files now.

The query design assigns purpose dispatch to the new `OrderedCartQueryProjection.Evaluate` entry point with the arguments shown in [the API proposal](S06D2B1L1D_CART_QUERY_DESIGN.md#proposed-api-and-ownership). This contract proposes a required `ReservedBuildContext reservations` argument immediately after `input`; the API and test-design documents must be updated together before implementation. Preserve the existing six request-guard priorities exactly, then add `ArgumentNullException("reservations")` as the final request guard before any owner/catalog/clock reads. A non-null context is prepared before evaluation. Missing context must not trigger automatic import, current-content capture, legacy fallback, or eager owner access. The context is tied to ordered job IDs and scalar snapshot fingerprints; each validation verifies the current job/car/snapshot scalars before comparing contents. Results and errors never return the context or its values by reference. Snapshot JSON is only hashed as opaque UTF-8 bytes for staleness binding; no query serializer or JSON parser is called.

Context validity is same-owner, same-turn and synchronous. This proposal does not claim cross-frame freshness, prevent concurrent mutation, or supply an A→B→A revision fence. Before caller activation, the owner adapter must either synchronously rebuild the context after every job/content write or add a reviewed monotonic generation to the owner lifecycle. A profile revision alone is not asserted to cover every physical/business write. Do not add a stale-cache fallback to the query.

## Finite implementation and test path proposal

| Path | Proposed responsibility | Explicit exclusion |
|---|---|---|
| New `Domain/ReservedBuildValues.cs` + `.meta` | Owned value/hash-baseline DTOs, deep typed capture, exact field comparer and ordered per-owner context. This file has no JSON or infrastructure dependency. | No schema field, profile copy, profile mutation, business command, persistent cache, runtime query parser or reflection serializer. |
| New `Infrastructure/LegacyReservedBuildImport.cs` + `.meta` | The only serializer/hash boundary; explicit `FromLegacyOwner` and `FromLegacy` load/import factories. It may transiently read an existing `PlayerProfile`, `OwnedCar` and `AssignedGarageJob`, but returns only owned typed values and scalar status. Its duplicated hash/snapshot source must be pinned to the unchanged `GarageBusiness.Snapshot`/`Contents` methods before use. | Not called by a query, cache miss, price getter, assignment command body or per-request fallback. No writes, profile copy or generic deserializer. |
| New `Runtime/ReservedBuildValuesChecks.cs` + `.meta` (`UNITY_EDITOR`) | Import/comparer vectors against separate isolated legacy fixtures and frozen legacy reference; no candidate helper supplies expected values. | No Unity player/editor automation changes beyond the test file; no edits to old reference files. |
| Existing `GarageBusiness.cs`, `SnapshotEquality.cs`, `ProfileStore.cs`, `LegacyProfileStore.cs`, `LegacyProfileDecoder.cs`, `SqliteStateStore.cs`, `SqliteTypedState.cs` | Read-only pre-edit pins for `Snapshot`, `Contents`, `Matches`, `Assign`, `Validate`, `UpgradeLegacySnapshots`, load/materialization ordering and existing JSON fallback. | No source edits in this prerequisite. A future caller-integration checkpoint must independently freeze/review exact methods and route the frozen legacy oracle before changing any of them. |

The focused checker should be a new `UNITY_EDITOR` file, `Game/Assets/DragLab/Scripts/Runtime/ReservedBuildValuesChecks.cs` (+ `.meta`). It must cover: matching hash with malformed and contradictory `snapshotJson` (initial accepted result must not expose a fallback parse error); changing away from that baseline to exercise latent absent/value/error outcomes; matching and mismatching hash-only jobs; exact JSON fallback; exact typed fallback; 1/4/5-ULP doubles, opposite sign, nonfinite values and `health`; float/double signed-zero and NaN bit-pattern controls; every float/control primitive; null versus empty arrays, null entries, order and runtime-type changes; duplicate/missing part IDs; active unchanged and changed reservations; and import errors held until the query reaches that job. Compare `ReservedBuildValues` independently against both the frozen old result and direct typed field expectations. Verify defensive ownership by mutating every original input after capture and every returned copy after inspection; neither may change the owned result. Query-side probes must report zero `JsonUtility` reads/writes and zero `SnapshotEquality`/`GarageBusiness.Matches` calls.

## Go / hold conditions

This is a concrete prerequisite recommendation, not implementation GO. High review must approve the importer branch contract and the explicitly documented hash-spelling limitation. Before implementation, pin the source methods above against the current baseline, confirm supported concrete element types/serialized fields, and finalize the frozen-reference test route. If an active profile cannot be imported without changing existing load validation order, keep the context explicit and dormant; do not insert a broad load hook or weaken validation to force adoption.

After the typed-value prerequisite passes its focused checks, a separate L1D production gate still needs an approved method-level allowlist for reader-backed quote validation, the physical calculation path, serialized business-job removal, all four query purposes, source purity, freshness and later caller activation. This contract does not close the `GarageBusiness.Validate -> Matches -> Snapshot(ToJson)/SnapshotEquality.Serialized(FromJson)` dependency by itself. Full S06 and S07–S14 remain open.
