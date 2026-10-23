# S02c ownership-validation lookup

Status: one internal lookup scope is integrated into the read-only `MechanicsOwnership.Validate` stage. This helper is not an ownership validator, normalizer, repair routine, or canonical owner. It creates no new rejection rules and does not change the selected-car precheck or other domain validators.

## Existing validation boundary

`MechanicsOwnership.Validate` first checks profile version/revision/cash and the selected-car reference, then runs the existing vehicle-listing, economy, career, and business validators. The lookup capture belongs after those gates. Its purpose is to replace repeated first-match list scans later in the existing read-only validation body, without changing the order or messages of those gates.

The current body uses `profile.engines.Find` for each car's engine, `profile.parts.Find` for each installed car/engine binding, and later `cars.Any` / `cars.FirstOrDefault(...slots.Any(...))` for car identity and part ownership checks. These scans rely on first list match. The full validator remains authoritative for cross-record uniqueness, parent/slot/category/fitment rules, orphan detection, and its existing error order.

## Frozen API

`DragLab.Domain.Mechanics.OwnershipValidationLookup` is an internal sealed `IDisposable` type:

- `Capture(IList<OwnedCar> cars, IList<EngineAssemblyInstance> engines, IList<PartInstance> parts)` builds first-match ordinal ID maps for each non-null list element. It captures actual record references only; it does not copy profile records or conditions.
- `FindCar(string id)`, `FindEngine(string id)`, and `FindPart(string id)` return the same first record an ordered `List.Find(record => record.instanceId == id)` scan would return.
- `HasCarID(string id)` preserves the corresponding ordered `Any` result.
- `CarContainingPart(string partId)` lazily builds the installed relation in car order and binding order, preserving the first `FirstOrDefault(car => car.installed.slots.Any(...))` match. The relation map is created only when this method is first used, after the caller has passed existing ownership/binding gates.
- `Dispose()` clears all lookup maps, relation entries, and captured object references. Methods after disposal throw `ObjectDisposedException`.

The map uses `StringComparer.Ordinal`, matching C# string `==` identity comparison. Null IDs use explicit first-match slots because `Dictionary<string, T>` cannot accept a null key; empty and whitespace IDs remain ordinary keys. Duplicate IDs do not cause capture-time rejection: maps keep the first entry, leaving the full validator's ordered `unique` checks to return the existing error. For any source list containing a null record, that family falls back to the original ordered scan at lookup time. It does not skip the null record or throw during capture, so an earlier match still wins and a later lookup still encounters the original null dereference at its original scan position. A null source list is likewise deferred until a caller first performs a lookup that would touch it.

If the lazy car/slot relation builder encounters a null car, installed build, slot list, or binding, it discards its partial relation map and delegates to the original nested scan. This preserves its early-match and malformed-row behavior. The helper does not heal or skip malformed relation rows. It also does not validate owner/location/category/slot relationships; those remain the existing validator's responsibility.

## Lifetime and caller contract

The capture is for one synchronous validation read stage on the owner of the supplied lists. A live profile remains main-owned; a worker may use the helper only on an already-private detached candidate. Do not cache the maps by profile revision, list size, or generation: these lists and IDs are public mutable fields, and direct changes need not advance revision. Dispose on every return or exception before mutations, callbacks, publication, or handoff. Do not let borrowed record references escape the validation scope. No full-profile copy, JSON, Unity API, threading primitive, worker dispatch, or global cache belongs in this helper.

The root validator integration must retain the pre-existing top-level validation order, capture after those gates, and dispose around the remaining pure read stage. The helper itself does not change callers, error strings, or which malformed records are accepted/rejected. `JsonUtility`-based quote validation and `AssemblyOperations` stored-engine lookups are separate families and remain outside S02c. No S03 view/cache work is included.

## Verification status

The corrected full Windows Unity Editor run passed 132 exact parity/source-preservation checks plus 72 isolated storage/recovery checks; supplemental final-source lifetime/relation/median probes passed 10, all exit 0. The frozen oracle body is independently matched to the baseline and both test files are Editor-only. Full timings include capture and all prelookup validation, with 180 alternating pairs retained. Large fixtures improve, while the five-car/no-spares separate median is slower by 8.7%; the paired median is effectively neutral. No blanket no-regression, allocation or phone claim. See [measured evidence and remaining work](../../reports/save-refactor/S02C_RESULTS.md). Independent Luna-high review and local commit close this checkpoint, not this helper document alone.
