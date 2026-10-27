# S06D2B step 1: frozen cart oracle and fixtures

**Status:** Implementation handoff, not a characterization result. Source is pinned to baseline `d541ad8`. No Unity/Editor or player run has been performed here. The checker and root-owned Editor wrapper must run the matrix and preserve the resulting reviewed baseline trace before any production preview source changes.

## Frozen oracle boundary

`Game/Assets/DragLab/Scripts/Runtime/LegacyCartPreviewOracle.cs` is wholly guarded by `#if UNITY_EDITOR`. It copies the `PartCart.Total`, `DeliveryFee`, `Apply`, and `PreviewProfile` method bodies from baseline `d541ad8`, with the oracle class name substituted. The diagnostic JSON-call hook delegates to the baseline `PartCart.RecordRuntimeJsonCall`; the copied methods preserve its call positions and the original JSON/Ensure/mutation order. It is an Editor characterization reference, never a player fallback. `Quote`, `Checkout`, and unrelated cart APIs are outside this oracle.

Source provenance at handoff (SHA-256):

| Source | SHA-256 |
| --- | --- |
| `Game/Assets/DragLab/Scripts/Domain/PartCart.cs` at `d541ad8` | `C7914B365EAD6B348D7E3241415FB8ECC54E9ED369A337D95146BF719777F562` |
| Frozen oracle file at handoff | `E691597B6EE61220436401EBCA90B04B19D4AFFB73FBB096E5575359816391F3` |
| Fixture factory file at handoff | `7F073E8335BDA82229FFBB780FA04F7FACBB731FF3230D61EFCDDB3D5DAAC715` |

These hashes identify the exact source checked at handoff; recompute them after any edit before recording runtime evidence. The existing `PartCart` copy comparison is a compatibility check, not an independent golden by itself. Step 1 must emit a stable legacy trace from the frozen oracle using fixed fixture seeds, explicit `now`, ordered row inputs, exact error type/message, exact integer totals/fees, typed output graph summaries and identity correspondence. Preserve pre-existing IDs literally. Map generated IDs bijectively by row/action/quantity/member role/position; never rewrite a live or malformed source graph. Record the trace digest and have the independent checkpoint reviewer inspect the literal quote/error invariants and trace before the typed candidate is implemented. A later helper change must not silently change both the oracle result and the accepted baseline trace.

The copied methods still call domain helpers. Their exact file dependencies at handoff are pinned below so future edits can be detected and the independent trace remains authoritative. This does not attempt to duplicate the entire domain call graph.

| Dependency file | Referenced behavior | SHA-256 at handoff |
| --- | --- | --- |
| `Game/Assets/DragLab/Scripts/Domain/SharedInventory.cs` | ordered `Move` effects and ownership transfer | `9265955454930DC8431CC9AF6169BF620846B487B9D391AC86B75AF2EBCF9D74` |
| `Game/Assets/DragLab/Scripts/Domain/MechanicsOwnership.cs` | `Ensure`, `Definitions`, `Resolve` and validation | `91305E39E34EF6680A10FB9312EBB683CF782749C847E03C4E4FE87B100F52C2` |
| `Game/Assets/DragLab/Scripts/Domain/PartsMarket.cs` | reservation-aware `Ledger`, `Take`, used supply | `61560ACC9A491FB35355B24970E0300F13E18A56004C94C838F7977D3BD71D69` |
| `Game/Assets/DragLab/Scripts/Domain/Valuation.cs` | loose sale-price capture and validation | `41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1` |
| `Game/Assets/DragLab/Scripts/Domain/InventoryUnits.cs` | role, count, position, buy and install | `510FC425E85D62F37BDBF0DD2804EA6277474DB72BC0A3524CE93F376BCCACCF` |
| `Game/Assets/DragLab/Scripts/Domain/PartOperations.cs` | purchased set effects | `62F8A07E45759C03CF177B597207499C6645124EF92F3710F3006368FDD7D2AE` |
| `Game/Assets/DragLab/Scripts/Domain/EngineElementService.cs` | `EngineRebuilds.RequireAvailable` | `B3F756FBF09AF8E0C8C2D5672106950C488E0D1F1986B4B006B2E2194F2CB561` |
| `Game/Assets/DragLab/Scripts/Domain/EngineElements.cs` | valid part/member construction and topology | `5BAE8172E1C04599E3BEF8A62E84214FFC0E1B9E034C68DC42BE14830E582585` |
| `Game/Assets/DragLab/Scripts/Domain/AssemblyOperations.cs` | engine attach/detach operations reached by moves | `EB46DE2EF0ACF871C9BCF0069775267C7023915C1E932C7470C56827192F4B1E` |
| `Game/Assets/DragLab/Scripts/Domain/CartReservations.cs` | `Matches`, `SameCart`, and `Clear` | `17B8BF9FCA5994AB13569B42BDF38447634FA52BB127CC221AAD667DDEAF7F17` |
| `Game/Assets/DragLab/Scripts/Domain/MechanicalCatalog.cs` | authored spec/fitment and part lookup | `DE235EA66F0472AD905898608CA26F86CD2158D646EF1AA55414FE16A4728C21` |
| `Game/Assets/DragLab/Scripts/Domain/RuntimeEntropy.cs` | default clock when callers omit `now` | `FCE9F16C872AC46B4D324C55CB69E4332974FD99894A66F5AE9FF4E115F365A6` |
| `Game/Assets/DragLab/Scripts/Domain/RuntimeDataEncodingProbe.cs` | Editor JSON-call diagnostics | `B98AF34F8F25A7E268D68B77774DF4D3CC6AE9F9F513045430E836340AB4B1D8` |
| `Game/Assets/DragLab/Scripts/Domain/VehicleCollection.cs` | real fixture car purchase | `FB46544D163148798CEC3759782D7219D0D4C64F73F9997D7358811D9D9EA65A` |
| `Game/Assets/DragLab/Scripts/Domain/VehicleListings.cs` | seeded authored fixture market | `FAB879227D1003690CC83193CA5E4E69C66B1247CF67634299162597C6AC7250` |

## Fixture API and boundaries

`CartPreviewCharacterizationFixtures` is also wholly editor-only. `CreateNormalized(out PlayerProfile, out string, uint seed)` builds an in-memory profile with a fixed clock, actual `MechanicsTransactions.Catalog()` authored definitions, explicit vehicle-market seed, then one initial `MechanicsOwnership.Ensure`. That normalizes `PlayerProfile.New`'s real starter-car definition into actual owned chassis, engine, parts, members and selected identity. It creates one real used part through `PartsMarket.AddUsedSupply` with a deterministic local seed and checks the resulting profile with the independent current global validator. It does not call `ProfileStore`, access user saves, or accept profile data from the running game.

The auxiliary fixture helpers accept only this disposable normalized profile and intentionally mutate it through existing domain operations:

- `AddSecondOwnedCar(profile)` purchases an actual distinct new listing and returns the resulting `OwnedCar`.
- `DetachOwnedEngineToStorage(profile, carId)` uses `SharedInventory.Move` on that car's actual attached engine and returns the same engine record.
- `AddUsedPart(profile, idPrefix, ref seed)` and `AddUsedUnit(profile, idPrefix, ref seed)` use `PartsMarket.AddUsedSupply` against authored compatible definitions. The unit helper returns an actual valid modeled dealer unit; it preserves its generated physical identity and condition. Both fail loudly if the authored catalogue cannot produce the required shape.

These helpers do not clone full profiles and do not call `Ensure` after returning the fixture or after later deliberate corruption. Test-only fixture initialization may use normalization; every malformed/error probe must mutate an already-normalized isolated fixture and then invoke the frozen path without healing it first. A valid `Ensure` no-op case must snapshot before/after all serialized fields and typed identity/order facts; a separate invalid legacy-heal case records the exact mutation and expected typed rejection.

## Characterization handoff

Checker API owner: `VehicleListingValidationChecks` remains unchanged; the new checker owner exposes `CartPreviewCharacterizationChecks.Run(Action<bool,string> check, string reportFolder)` and may call the frozen oracle/factory APIs above. The matrix remains full scope: all 100 rows and quantity 99/100 boundary; authored new sets/units/positions; used physical part and unit; sale, move, mixed ordering; multiple cars and detached engines; actual block-preparation flow; partial stock/express fees/reservations through nine races; malformed input and exact first errors; source immutability; valid Ensure no-op versus legacy healing; and virtual-new-ID bijection. If an authored topology is unavailable, record the exact missing candidate and leave that case open rather than shrinking or fabricating it.

Use the fixed `CartPreviewCharacterizationFixtures.FixedNowTicks` or an explicit checked timestamp for direct oracle calls. The actual `NativeInventoryMoves.Candidate` path currently uses the default `RuntimeEntropy.UtcNow`; its actual clock context must be captured independently by the caller-level check, without changing a production API for test determinism. Source/runtime JSON is permitted only in the frozen oracle, isolated test copies, and diagnostic trace; this implementation adds no player path, typed planner, caller switch or S07 behavior.

## Checks at handoff

- Static source readback confirmed all four requested `PartCart` method bodies are present in the oracle and the fixture API uses only isolated authored state.
- Both `.meta` files carry distinct GUIDs and Unity `MonoImporter` metadata.
- No Unity/Editor execution, player build, process launch, Git command, or characterization result was produced by this agent. Root owns the Editor wrapper/run and final integration.
