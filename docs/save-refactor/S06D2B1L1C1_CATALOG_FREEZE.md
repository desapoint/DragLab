# S06D2B1L1C1 — frozen mechanical catalog reference

Status: C1-only Editor reference adapter prepared for independent Apply/Move reference copies. Root-owned Unity liveness now passes 647/0 (642 unchanged matrix checks plus five controls); independent Luna-high reference-preparation review is approved. The frozen C0/I/J/K/L1B reference files are untouched.

## Boundary

`LegacyMechanicalCatalogReference` lives under `UNITY_EDITOR` and is used only by the new `LegacyC1CartApplyReference`, `LegacyC1WholeEngineMoveReference`, and `LegacyC1WholePartMoveReference`. Its public-to-the-assembly entry points are `Find(catalog, id)`, `Spec(catalog, id)`, and `Resolve(catalog, model, installed, adjustments)`. It does not call `MechanicalCatalog.Find`, `RebuildIndexes`, `Spec`, `Resolve`, `OpenLookupScope`, or a candidate reader/core. It reflects only the private state necessary to preserve the pre-C1 Find/RebuildIndexes/Spec behavior on disposable legacy-reference catalogs.

The copy is intentionally narrow. `Recipe`, `Fits`, slot/category helpers, tuning, mass, fuel/controller, service, and AWD semantics remain existing hash-pinned dependencies. No full catalog or simulator copy is introduced. The new adapter's complete source SHA-256 is `2C82E8503226148C8F5C310A62DEA61495A329C396825E798630CD45B0370112`; the source catalog SHA-256 at capture is `8D5895D45904080633AC45A784B115C8D2232A0FDE0F8FC0FBEB066C6D9D3E52`.

## Old-source fingerprints

Fingerprints below are normalized declaration-plus-body hashes from the C0 provenance ledger; they are provenance pointers, not claims that requalified adapter text is byte-identical. The ledger extraction rules and line ranges are documented in `reports/save-refactor/S06D2B1L1C0/S06D2B1L1C0_METHOD_PROVENANCE.md`.

| Old method | Source lines | SHA-256 | C1 handling |
|---|---:|---|---|
| `MechanicalCatalog.Find` | 109–115 | `1316E3E549C98FEB639F625EE20C0C9080FD6EA5E4EB1674B4FA39905FC344A2` | Literal lookup branches reimplemented against reflected legacy fields. |
| `MechanicalCatalog.RebuildIndexes` | 104–107 | `119F29D5AF09F01CC0A9A22A8D157FB9A76EEAB1A81B302CB068EDDF90ABBD7C` | Private dictionary construction/publication copied, including first duplicate wins and revision/count update order. |
| `MechanicalCatalog.Spec` | 108 | `7602678DB5EF358D7A7AE4396E18218348F7490B0A625903790F049FC82C87A6` | Typed `VehicleSpec.Copy` returned from the reflected original dictionary. |
| `MechanicalCatalog.Resolve` | 305–380 | `9755B8B333D8D44D178458C9B5AB5EB6BD8170A9B6F00C79226442D06F083D9` | Legacy resolution body copied; each reached Find/Spec route redirected to this adapter. |
| `MechanicalCatalog.Fits` | 292 | `23FB2B64E3994E8BF7E57512B124262889F0C697E51EFC0D4B17F083FCE87567` | Existing method remains pinned and called by the copied resolver. |
| `MechanicalCatalog.Recipe` | 265–285 | `1403E012A60499A8F8C793FA0FBBA5EDBE236C06D5E194253B15B50DB230DA1C` | Existing method remains pinned and called by the copied resolver. |

## Reflection surface and error ordering

Root replaced four explicit `new NullReferenceException()` receiver checks with an ordinary `catalog.parts` dereference so the runtime supplies its natural message. Three direct null-receiver comparisons cover Find, Spec and Resolve; no exception wording is fabricated.

The adapter reads only `specs`, `authoring`, `partIndex`, `indexedPartCount`, `authoringRevision`, and `indexLock`, plus nested `IndexedPart.value` and `.position`. It does not retain a second catalog or publish candidate lookup state. The current source uses a private `Dictionary<string, IndexedPart>` with `StringComparer.Ordinal`; reflection accesses it as `IDictionary` without wrapping invocation failures.

`Find` preserves the null-id and authoring paths, stale/null index rebuild trigger, position/reference/id validation, authored-list fallback, and conditional rebuild after fallback. Rebuild constructs the next dictionary locally, leaves the old published index in place if enumeration/insertion fails, uses first duplicate wins, then publishes index, count, and unchecked revision in source order under the catalog lock. This preserves natural null-row/null-key exceptions and their timing. `Spec` keeps dictionary indexer failure behavior and calls the existing typed copy. These semantics are for disposable legacy oracle objects only.

## Reached nested lookup adapters

Only helpers reached by copied `Resolve` that perform catalog lookup were copied with lookup redirected to adapter `Find`:

- `PartSemantics.Transmission`, `Optional`, and `RemovalEffect`.
- `FuelingHardware.Type` behavior (locally named `FuelingType` to avoid shadowing the existing type).
- `CombustionHardware.ValidateCombination`.

Their arithmetic, predicates, strings, and ordering were copied from the current source. `FuelingHardware.ControllerSupports`, `CombustionHardware.Diesel`, `ServiceHardware.Optional`, `PartSemantics.Apply/ApplyRemoval/ApplyMassDelta`, `PartTuning`, `AwdHardware`, `MechanicalCatalog.Slots/RoadOnly`, and all other reached helpers remain current hash-pinned dependencies. The copied lookup helpers are private to the adapter and do not alter production domain methods.

## Routed reference scope and review gates

| New C1-only reference | Adapter use | Source SHA-256 |
|---|---|---|
| `LegacyC1CartApplyReference.cs` | `Find`, `Spec`, `Resolve`; no call into live public lookup/resolution | `14169EF64DF9A0F3DA550A5670FB64ED2049F5BA19D56224FD437BC5506B412F` |
| `LegacyC1WholeEngineMoveReference.cs` | `Find`, `Spec`, `Resolve` where reached | `F94E73C0C6FAC0C1F16AB0AAB810EE29638D8949FD3A70549BCE840AF8529D76` |
| `LegacyC1WholePartMoveReference.cs` | `Find`, `Spec`, `Resolve` where reached | `6F18E40E6075E2C3B6BA5BDD5ABE30CE0F86C86151686CE103FCBD7302EF491F` |

Old C0/I/J/K/L1B oracle files stay byte-for-byte frozen. The existing L1B supply oracle still reaches the public Find path; that path must remain unchanged during C1 or receive a separately reviewed route. High semantic review approved the liveness run; root-owned compilation and liveness pass 647/0 on unchanged 2,149-path sources. Independent preparation review is approved; root owns the local commit; no candidate or production implementation is claimed.
