# S06D2B1L1C1 — independent reference tree

Status: reference preparation only. These C1 copies are separate Editor-only oracle code, not the candidate and not a production transition. The frozen C0/I/J/K/L1B/canonical sources listed below remain byte-identical. This preparation adds no checker, runner, Domain or tool wiring; root owns the checker and scoped oracle override, while the test author owns the frozen catalog adapter.

## Copy and routing map

| Frozen source | New C1 copy | Routing and provenance |
|---|---|---|
| `Runtime/LegacyCartApplyReference.cs` | `Runtime/LegacyC1CartApplyReference.cs` | Literal C0 Apply/acquisition/unit/install closure. Class name and calls to frozen whole-part/whole-engine references are renamed to the C1 copies. Direct `MechanicalCatalog.Find`, `Spec`, and `Resolve` calls route to `LegacyMechanicalCatalogReference` using its frozen entry points. `Fits`, `Recipe`, typed `Copy` methods, pricing, and `LegacyScopedSupplyReference` remain at their frozen live call boundaries. |
| `Runtime/LegacyWholePartMoveReference.cs` (I) | `Runtime/LegacyC1WholePartMoveReference.cs` | Literal I closure. Class name is renamed; direct catalog `Find`, `Spec`, and `Resolve` calls route to the independent C1 catalog adapter. Shared unchanged `Fits`, `Recipe`, typed copies and other C0-pinned pure helpers remain unchanged. |
| `Runtime/LegacyWholeEngineMoveReference.cs` (J, including the ordered K Move dispatcher used by raw Apply) | `Runtime/LegacyC1WholeEngineMoveReference.cs` | Literal J/ordered-Move closure. Class name and part-reference calls are renamed to C1. Direct catalog `Find`, `Spec`, and `Resolve` calls route to the independent C1 catalog adapter. The C1 Apply calls this `Move`; no duplicate `LegacyOrderedInventoryMoveReference` copy is needed because this J method contains the exact ordered dispatch used by Apply. |
| `Runtime/LegacyMechanicalCatalogReference.cs` (created by the test author) | Not owned here | Required independent C1 catalog route: `Find(MechanicalCatalog,string)`, `Spec(MechanicalCatalog,string)`, and `Resolve(MechanicalCatalog,string,IDictionary<PartCategory,string>,Tune=null)`. Its Resolve owns exact nested lookup-helper copies. C1 references call these methods and do not call public `Find`/`Spec`/`Resolve` in the copied closure. |

The `LegacyC1CartApplyReference` acquisition paths retain the frozen `LegacyScopedSupplyReference` for L1B stock/hold semantics. That old helper directly calls public `MechanicalCatalog.Find` in product-cache misses and is required byte-identical. Therefore public `Find` must remain behaviorally and byte-identically unchanged during this C1 reference preparation and candidate implementation. If a later source seam would change it, stop for high review and route the C1 supply reference through an independently frozen equivalent before claiming liveness; do not silently let the oracle reach a changed candidate body.

## File hashes and exact transformation

Hashes below are SHA-256 of captured working-tree source bytes at the committed baseline and the new C1 copy bytes; normalized method hashes use the separate C0 ledger rule. Each copied file preserves its source line endings and method bodies, with only the listed class/reference renames, direct `Find`/`Spec`/`Resolve` adapter rewrites, and C1 provenance-comment corrections. No frozen source file is edited.

| Source | Source SHA-256 | C1 copy | C1 copy SHA-256 |
|---|---|---|---|
| `LegacyCartApplyReference.cs` | `9727A6F65368C12C040E620C1054EC84831AE97D33A06BB53F2153E83901BB8C` | `LegacyC1CartApplyReference.cs` | `14169EF64DF9A0F3DA550A5670FB64ED2049F5BA19D56224FD437BC5506B412F` |
| `LegacyWholePartMoveReference.cs` | `30A75892AA306E8A061454019DEC4AEA9A211569DF0F99B63F9CB197DC76F2BF` | `LegacyC1WholePartMoveReference.cs` | `6F18E40E6075E2C3B6BA5BDD5ABE30CE0F86C86151686CE103FCBD7302EF491F` |
| `LegacyWholeEngineMoveReference.cs` | `B4704709F463E06453872AEAE0E1B3B1BC00034D7AAE846579708D85A268F012` | `LegacyC1WholeEngineMoveReference.cs` | `F94E73C0C6FAC0C1F16AB0AAB810EE29638D8949FD3A70549BCE840AF8529D76` |

The three new `.meta` files use unique GUIDs derived deterministically from their C1 asset paths. Their SHA-256 values are:

| New metadata file | SHA-256 |
|---|---|
| `LegacyC1CartApplyReference.cs.meta` | `23F65AD988B97B66AFC9AF07C3C27CA3F2392EE45335D2728B4797B39514E401` |
| `LegacyC1WholePartMoveReference.cs.meta` | `567E7BA4CBE072946891ACD637542D04FA22D83640EBAF0A3CF05BB69BA432A3` |
| `LegacyC1WholeEngineMoveReference.cs.meta` | `3EDA024F69C4179E8E8C5681B3D4E6DDCC59B88D63CB596CF943646B24C58D2A` |

## C1 catalog method pins

Before any Domain edit, root/test author records the complete source/method manifest and independently freezes the pre-C1 implementations of `MechanicalCatalog.Find`, `Spec`, `Recipe`, `Resolve`, and all nested Find-calling Resolve helpers. The new catalog adapter must preserve exact declaration/body ranges, first-match behavior, typed `Spec` copies, Recipe category and optional legacy-tune behavior, Resolve lookup order, and nested helper order. `Fits`, `Recipe`, `PartDefinition.Copy`, `VehicleSpec.Copy`, `Tune.Copy`, `FluidState`, and static numerical/content helpers remain unchanged dependencies and are hash-pinned. The adapter must not publish indexes or call candidate lookup code. High semantic/hash review of this source boundary is required before any Domain changes.

The unchanged Recipe source pin is `MechanicalCatalog.cs`, method declaration and body inclusive lines 265–285, using the same normalized inclusive-line hashing rule as the committed C0 method ledger: SHA-256 `1403E012A60499A8F8C793FA0FBBA5EDBE236C06D5E194253B15B50DB230DA1C`. The containing source file SHA-256 is `8D5895D45904080633AC45A784B115C8D2232A0FDE0F8FC0FBEB066C6D9D3E52`. Keep this method body unchanged; the reader delegates to it and returns the fresh dictionary it creates.

The I/J copy hashes changed between the first generated copies and the final files shown in the table. The initial I/J hashes (`1AFB4D39C82B14C856D396014C38A9018CA0A01ADA31AA1F2C840961195F9B21`, `CF940C503A39BD7C0A85798ACB992D6A807CF77C24BD453CBA292E3CEE63DDD6`) already included the C1 catalog `Find`/`Spec`/`Resolve` adapter rewrites and C1 class routing. The final I/J hashes (`6F18E40E6075E2C3B6BA5BDD5ABE30CE0F86C86151686CE103FCBD7302EF491F`, `F94E73C0C6FAC0C1F16AB0AAB810EE29638D8949FD3A70549BCE840AF8529D76`) differ only in the leading provenance-comment blocks clarifying those already-present routes; no method body or routing changed between the two hash sets. Deterministic source-to-copy replay confirms the final files equal the documented rename, adapter-rewrite, and comment-correction transformation.

## Reference independence conditions

* `LegacyC1CartApplyReference`, `LegacyC1WholePartMoveReference`, and `LegacyC1WholeEngineMoveReference` are compiled only under `UNITY_EDITOR` and mutate only the disposable oracle fixture.
* The C1 Apply driver uses C1 I/J method names for all copied Move and replacement work. It never calls the candidate transition or live `PartCart.Apply`.
* Every direct catalog `Find`/`Spec`/`Resolve` call in the three C1 copies uses the test-author-owned `LegacyMechanicalCatalogReference` entry points. Nested Resolve lookup helpers remain inside that adapter.
* C0 `LegacyCartApplyReference`, I/J/K references, L1B supply reference, canonical references, their metadata, and their checker call sites are not edited. Root will wire the new C1 checker/oracle override separately.
* This reference-tree preparation makes no runtime-pass claim. Root owns the 642-case liveness diagnostic, regression runs, process/source hashes, and Editor/player absence check before any Domain edit.
