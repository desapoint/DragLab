# L1D semantic closure preparation — draft for independent review

2026-11-03. Baseline `7cd5839`; production Domain remains `e261080`. **Tooling/source-analysis evidence only. No production edit authorization, complete call closure, typed-query parity, frame/performance, player or device acceptance.** The full S00–S14/no-runtime-JSON/no-profile-save-clone scope remains unchanged. The required Luna-high review is still pending.

## Reproducible source analysis

`tools/Audit-CartQueryClosure.ps1` loads the selected Unity Editor's bundled Roslyn SDK and runs `tools/CartQueryClosureAudit.cs`. The input is the actual `Game/Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.rsp`: **416 source files and 307 explicit references**, with zero semantic compiler errors. The parser explicitly adds `-noconfig` and `-nostdlib+` to prevent an implicit host-framework reference; it preserves the response file's source list, parse/compilation options, defines and explicit metadata references. Only helper-host CS1701/1702 framework-unification warnings are suppressed; project compiler errors still fail closed.

The tool reads PE metadata without loading or executing game assemblies. It emits no compilation assembly/player, edits no Game source, and executes no generator, analyzer or ILPP. Its small language fixture is written, parsed and deliberately changed/restored only under the ignored report folder. Source bytes are captured before parsing; source, reference, response and tool bytes are checked at completion. An external source freeze is required: these endpoint checks are not an A→B→A concurrent-write guarantee.

The graph follows resolved invocation, construction, method-group, property, static-field, operator, conversion, initializer and foreach routes. Constructor identity includes MethodKind/MetadataName, so `.ctor` and `.cctor` cannot collide. Static/instance field and property initializers, partial types and unique exact parameterless implicit base constructors are included. Optional/params base resolution, interface/virtual/delegate dispatch, implicit using disposal, awaits, missing operation bodies, metadata and synthesized boundaries remain explicit review obligations. Property getter/setter routes and conditional branches are conservatively unpruned; recorded reachability does not prove runtime feasibility or grant an edit to every reached member. Edge rows are deduplicated reachability by root/caller/source-line/kind/symbol, not runtime call counts or a dataflow/control-flow proof.

## Executed final evidence

Final runs `physical-closure-semantic-v13a` and `v13b` both exit 0. Fourteen main evidence artifacts are byte-identical between them, including the exact source/reference captures, member/edge tables, unresolved routes, compact physical inventory, self-checks and tool provenance. All TSV row widths match their headers.

| Tagged root group | Source members | Source files | Remaining OPEN records |
|---|---:|---:|---|
| `CarInspection.PrepareCar` / `CalculatePrepared` | 428 | 43 | 154 dispatch records |
| Legacy `InspectionBuild.Capture` context | 950 | 89 | 613 dispatch, 46 no-body, 8 implicit-disposal |
| `PartCart.Quote` context | 1,012 | 88 | 641 dispatch, 46 no-body, 10 implicit-disposal |
| Existing scoped typed catalogue `Resolve` seam | 122 | 22 | 26 dispatch |
| Existing concrete `Inspection(ICartCatalogRead)` seam | 34 | 16 | 12 dispatch, 3 no-body |

The union is **1,038 source members and 16,849 edges**. There are 1,073 no-source-declaration boundaries: 789 metadata and 284 compiler-synthesized records. The 1,559 OPEN records are retained, not counted as passed closure. Members include fields, constructors and properties, not only ordinary methods. The two typed-seam tags are available dormant infrastructure, not an activated candidate.

Checks actually run: 23 independent language/input/table probes, two full zero-error semantic runs with byte-identical outputs, TSV-shape checks, and an expected-failure output-boundary test rejecting `Game/Assets` (exit 1 for the intended guard). Probes cover explicit and implicit/static constructors, distinct initializer routes, getters/setters, method groups, operators, foreach enumeration/current/move/disposal, unresolved interface/delegate/optional-base routes, changed input-byte rejection and malformed-table rejection. No game simulation was executed by these probes.

Initial tool attempts are retained locally as diagnostics. A language probe caught constructor-key collision and omitted instance initialization; those were fixed. Other probe corrections distinguish direct ref-struct disposal from interface-dispatch disposal and normalized generic delegate names. A table review caught inconsistent OPEN-column widths; output now validates table width before writing and tests rejection. These earlier drafts are not acceptance evidence.

| Final artifact under `v13b` | SHA-256 |
|---|---|
| Source capture | `9EDC7436E3605542777C3E9FA5DBD859096525F623B835798988FF6437DF4994` |
| Reference capture | `EDBB74810FC71729B5034931029C381982C7663E3AEA564C3D7A3F6958763C8E` |
| Member table | `191CDCA78ED8DC3B9B27277255C8B75094702630008FBED76E3D68C76E3935F2` |
| Edge table | `F5C3C48BE110A2E48F0F08E5568C190CCD0082A0D8C2703B5A66D4037591F1E3` |
| OPEN table | `D731E6004F0C518A13DB28D48751D8386F3C6ABC956A83B6F91A416141389F21` |
| Compact physical member fixture | `C2FD6ABBB4AFD4691771C65F4CBA5B0753D85A36AF4BEA638993080C0922B9AB` |
| Compact physical file fixture | `307274997CECEF00D113553BCA67CD613575D8FCF008A5F7018F6F8EA84BD79A` |

The two compact TSVs are explicitly versioned draft provenance fixtures at `S06D2B1L1D/PHYSICAL_REACHABLE_MEMBERS.tsv` and `PHYSICAL_REACHABLE_FILES.tsv`; joining on path gives exact declarations, ranges, symbol identity, declaration hash and full-file hash. All 428 rows join exactly to the 43 files and match live source hashes. Implicit constructors use their containing type declaration/initializer provenance, not an invented emitted method body. The large raw graph, metadata captures, failed attempts and generated language fixture remain local. Exact `.gitattributes` entries pin LF for only the two new TSV fixtures, preserving their recorded byte hashes on checkout.

## Concrete conditional JSON/content-load witness

The unpruned physical graph contains this real route:

1. `CarInspection.cs:83` constructs `CarSimulation`.
2. `DragSimulation.cs:576` constructs `MechanicalStress`.
3. `MechanicalStress.cs:24` constructs `MechanicalCatalog` **when `spec.hasInstalledParts` is false**.
4. `MechanicalCatalog.cs:79` calls `PartCatalogOverrides.Load`.
5. `PartCatalogOverrides.cs:12` loads authored Resources data and calls `JsonUtility.FromJson`.

Keep authored override loading at content initialization. A typed query must use its captured catalogue and completed installed-build resolution. Current `ScopedCartCatalogRead.Resolve` delegates to `MechanicalCatalog.ResolveCore`, which sets `result.spec.hasInstalledParts = true` at line 379 before returning. `ConcreteInstalledBuildInput.Inspection(ICartCatalogRead)` already provides the typed capture seam. Neither typed-seam tag has a direct Resources/JSON metadata boundary in its resolved edges, but callbacks remain OPEN; that is not yet a complete no-JSON proof.

Before production approval, bind those seams explicitly, prove the completed marker and non-null concrete component collection at the simulation boundary, and test healthy, detached/no-engine, empty-cart and cold-catalogue cases. Reject malformed existing-source members before private simulation initialization; private copies do not justify healing missing persisted parts. Resolve the concrete catalogue-reader callback and source virtual/cleanup routes, retain independent body hashes, and preserve the full shared physics/probe behavior rather than replacing it with a static estimate.

## Use and next gate

Run from the repository with a fresh report folder:

```powershell
pwsh -NoProfile -File tools/Audit-CartQueryClosure.ps1 -OutputFolder reports/save-refactor/S06D2B1L1D/review-run
```

The Editor installation and response file are explicit optional parameters. Require exit 0 plus the current input hashes, zero compiler errors and self-check evidence; an old summary file alone is not a completed run. This tool is not a concurrency or runtime freshness fence.

Next: Luna-medium/high review must reconcile the corrected 730/0 reference checkpoint, source/dispatch/branch/lifetime mapping, proposed copied/reused/reader-overload treatment and a finite production edit allowlist (including any exact additional validation-reader paths). No candidate, Quote/Checkout, UI, storage, worker or simulation cutover proceeds from this draft. Full S06 and S07–S14 remain open. The previous C1 Windows build and reference-runtime evidence are retained; no unnecessary Unity/player rerun was made for tooling outside Game.


Post-audit source verification: all 2,169 existing Game/tools paths retain their final compile-E byte hashes, and the complete path set differs only by the two explicit new audit tools (2,171 paths total). All 266 baseline Domain and 28 original-reference files match, and Git shows no Game source/package/settings diff. The tools and compact draft fixtures require independent review; this is a local diagnostic preparation increment, not acceptance or advancement of L1D. Unrelated reports and attachments are preserved.


### Override-dispatch correction after the initial tooling commit

`d032e96` retains the first verified tooling snapshot. Subsequent source-target review found that Roslyn marks an override with `IsOverride`, separately from `IsVirtual`; the conservative OPEN predicate now includes it. A nonsealed override/more-derived language probe requires that the override body is recorded, the unresolved dispatch remains OPEN, and no more-derived implementation is guessed. This widens actual OPEN evidence from 1,472 to 1,559; source member/edge/inventory bytes remain unchanged. The physical source targets are now the base `EngineElement.Failed.get` (five sites) and `TireCondition.Failed.get` (one site). All other physical OPEN target names are framework/delegate calls; they still need explicit boundary treatment. This follow-up changes tooling/evidence only, not gameplay or an acceptance gate.
