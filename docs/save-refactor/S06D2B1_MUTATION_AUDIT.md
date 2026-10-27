# S06D2B1 cart-preview source mutation audit

**Status:** Read-only source audit of Editor attempt `60108`; root is adding a minimal probe to isolate the serializer boundary. This is not a characterization pass, and it does not authorize a production behavior change.

## Observed source difference

The mixed-row-order bridge check returned the same total from production and the frozen oracle (`260`), but failed source immutability. Root provided the typed diff artifact `source-diff-mixed-row-order-first.txt` from run `20261027-110003626`: `profile.inventory.dealer[1].unit` changed from `null` to an empty `LooseEngineElement`. The artifact's full filesystem path was not supplied here. The production call and subsequent oracle call had identical post-state. The existing checker compares before/after on the same normalized source, so the oracle call did not repair or hide this first change.

This is a real typed graph change even though the inactive dealer payload is semantically empty. It must remain a hard failure until the precise boundary is isolated and the approved no-mutation contract is satisfied.

## Relevant source path

The fixture's mixed rows are ordered sale, new set, new unit, used part, used unit, then Move. `PartCart.Total` handles these rows without applying purchase or sale mutations. On the first Move row only, it creates the private move preview using:

```csharp
RecordRuntimeJsonCall();
RecordRuntimeJsonCall();
movePreview = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));
MechanicsOwnership.Ensure(movePreview, p.catalog);
SharedInventory.Move(movePreview, ...);
```

`SharedInventory.Ensure` can normalize the *preview profile's* dealer wrapper, but `SharedInventory.Move` receives that preview. The source code does not explicitly assign the original dealer row's inactive `unit` payload in this branch.

The other relevant operations inspected do not contain a write to that dealer field:

- `Valuations.LoosePriceView` calls read-only `MechanicsOwnership.ValidateForQuote`; its returned display-price closure performs lookups/arithmetic and checks reservation/run state.
- `CartReservations.Matches` reads the active reservation and serializes ordered cart lines, not the dealer collection.
- `PartsMarket.Ledger` constructs local dictionaries and remaining-quantity values. `Total` calls `Take` with its default `consume=false`, so the method does not write live `NewPartStock.units`.
- `SharedInventory.Move` mutates the profile argument it receives; in this `Total` path that argument is `movePreview`.

These source observations do not prove whether the inactive field changes during `JsonUtility.ToJson(p)`, `JsonUtility.FromJson<PlayerProfile>`, a later helper, or the diagnostic snapshot boundary. Unity's inline-null serialization behavior is a test hypothesis only, not an established cause.

## Minimal isolation probe

Use a fresh deterministic normalized fixture for each branch, capture a typed source snapshot immediately before and after each operation, and never restore the mutated live source to make an assertion pass:

1. Call only `JsonUtility.ToJson(source)`; compare the original source afterward.
2. On a separate fresh source, call `ToJson` and then `FromJson<PlayerProfile>`; snapshot the source after each call and separately inspect the copy's inactive payload.
3. On separate fresh sources, call `PartCart.Total` with the same valid rows but omit the Move, then include the Move. Capture state immediately after each call. Keep the frozen oracle isolated to a later independent profile so it cannot consume already-mutated source state.

Record the exact dealer row index, original `unit` reference/nullness, empty-object fields, and the first operation boundary at which they differ. Preserve the existing source immutability assertion and 260 total as observations; do not weaken either check. If `ToJson` alone reproduces the change, isolate Unity serializer semantics and obtain a reviewed remedy before changing the frozen oracle or production route. If only the Move path reproduces it, instrument the clone and `Ensure` boundaries without changing their behavior.

No Unity/Editor rerun, production edit, fixture edit, checker edit, source repair, or Git command was performed by this auditor. Root owns the diagnostic rerun.

## Test-only literal isolation helper

`Game/Assets/DragLab/Scripts/Runtime/CartPreviewLiteralTestClone.cs` adds `CartPreviewLiteralTestClone.Copy(PlayerProfile)` under `#if UNITY_EDITOR`. It exists only to give production and oracle calls separate equivalent input graphs after the mutation above is resolved. It does not change or repair either source, and it is not a runtime/save/preview-planner API.

The helper memoizes objects by reference identity, clones arrays and exact `List<T>` shapes recursively, and copies serializable DTO fields marked public, `[SerializeField]` or `[SerializeReference]`. It allocates DTOs with `FormatterServices.GetUninitializedObject`, so constructors and normalization do not run. It preserves existing identity strings, nulls, field bits, collection order, subtype, and alias topology. The `PlayerProfile.catalog` reference is intentionally shared because it is nonserialized authored input; persistent authored `UnityEngine.Object` references are also shared. Other nonserialized fields, including `storageCommit`, are omitted. The cart preview functions under audit do not consume storage commit state.

The checker must create both actual/oracle inputs from one unchanged baseline using this helper, assert that their typed snapshots match the baseline and their profile roots differ, then run the calls independently. It must still snapshot the shared catalogue around both calls. If any shared authored object changes, do not treat the copies as isolated and leave that dependent case open. Any unsupported mutable node, collection, or nonpersistent Unity object throws with its graph path; there is no JSON or constructor fallback. The helper has not been compiled or run by this agent, so runtime support remains a root-owned Editor check.

The agreed API is only `internal static PlayerProfile CartPreviewLiteralTestClone.Copy(PlayerProfile source)`. It does not copy `PartCartLine` inputs; the checker owns a separate row copier that must preserve null `positions`, row order, and all input field values. The helper supports scalar immutable values, serializable DTOs, arrays, and exact `List<T>` instances. Dictionaries, sets, other collection types, unsupported mutable shapes, and nonpersistent Unity objects throw; the checker records those dependent cases as OPEN/SKIP rather than reporting parity. The copy has no path to `ProfileStore` and must never be reused by production code.
