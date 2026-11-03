# S06D2B1L1D — implementation-preparation review

Date: 2026-11-03. Baseline: production `e261080`; reference preparation commit `7cd5839`; semantic-audit tooling `d032e96`/`6a0876c`.

Decision: **accept Reference B as bounded legacy-liveness evidence; hold all L1D production edits.** This review does not approve the full source closure, new validation seams, typed query implementation, caller conversion, or any S06/S07–S14 completion claim.

## Reviewed evidence

- [Reference results](../../reports/save-refactor/S06D2B1L1D_REFERENCE_RESULTS.md), SHA-256 `956335CB9E4BED39128828580BF8BB9559370D03414008B9962F921F1585AC9E`.
- [Semantic closure audit](../../reports/save-refactor/S06D2B1L1D_SEMANTIC_CLOSURE_AUDIT.md), SHA-256 `34F92DB5E6B64F5ED688EC2CC75384B3EFC593BA28CFA960DA16781E93D2B77A`.
- [Candidate test gaps](S06D2B1L1D_CANDIDATE_TEST_GAPS.md), SHA-256 `F9C9874485AB030E1D70623DA46C651ADE3442BF5396A7F0609012CA0FCD048E`.
- [Physical dispatch proposal](S06D2B1L1D_PHYSICAL_DISPATCH_PROPOSAL.md), SHA-256 `6D865902B3C58553AE2FEA629631C01C8AFDCB0EBF6CA79A02C2F65EF8725937`.
- [Validation seam proposal](S06D2B1L1D_VALIDATION_SEAMS_PROPOSAL.md), SHA-256 `C9616F4AFE52B363AEEEB8EE9D481EC39CDCDF07E65B4A49A9FEC732A0E8FEF2`.
- [Reference tree](S06D2B1L1D_REFERENCE_TREE.md), SHA-256 `F02D0C19193DC160B09A4B4F28FF15AC6C165B8FA0A117F8B4EF653FABEFBBE8`; [method provenance](../../reports/save-refactor/S06D2B1L1D/PRE_EDIT_METHOD_PROVENANCE.md), SHA-256 `014D1E3E8B19173F9E45A779938FC85B15FD137E770A3B5900B1B21A550B1D89`.

## Reference B review

I inspected the complete `Reference B` trace at `reports/save-refactor/S06D2B1/cart-query-reference-20261103-133719509/S06D2B1L1D_REFERENCE_LIVENESS.txt`: 19,958 lines; 730 passing assertions; zero `ASSERT|FAIL`, zero `OPEN|` and zero runner exceptions. It contains 49 Total, 13 Fee, 42 Apply/PreviewProfile/enumerator, eight Preview, ten direct-price and four validation-price records. It also retains 29 explicit `LEGACY_PURITY|FAIL` diagnostics. These document the old methods' observed mutation and are required future-candidate divergences, not tolerated candidate behavior.

The vectors and outcomes are independently routed to old live methods versus the frozen references, not to a typed candidate. Results report zero post-run mismatches across 2,169 source paths, 266 Domain files and 28 prior reference files, plus no actual Editor/player process after terminal. I independently recomputed the current frozen-reference file hashes: `LegacyCartQueryReference.cs` `6686A8D884A41B8B01CE2666546E56DAF3A59D28534B3FBD104740A817BBB754`, valuation reference `7610C015AA6812F88BF1C6C44679EE0239B7B6C9D058C2F058E9F06CA9F078E7`, checker `ECC69BD08CE934235826CF587B5EF493619A156CBDA1985FF4A789FBC34030DA`. The report records that formatting cleanup happened after the 730/0 run; Roslyn/token equivalence and Compile E are reported for final bytes, but the full matrix was not rerun after that whitespace-only cleanup. Keep the runtime claim tied to the executed bytes and the documented equivalence evidence.

This accepts **legacy reference liveness only**. The 29 purity failures, JSON preview effects, and candidate-specific no-heal tests remain explicit; Reference B is not a runtime JSON-removal, purity, typed parity, physical calculation, performance, player, device, or caller result.

## Production closure and method-allowlist blockers

The current design's initial exact path table is not yet a finite method-by-method production edit allowlist for the requested behavior. The validation seam proposal now provides 22 exact reader/price/reservation declaration pins, but correctly labels them review targets rather than edit permission. The semantic audit has 1,559 unresolved dispatch/body records overall; the tagged physical calculation path includes 428 members across 43 source files, with 154 unresolved dispatch records. The physical proposal correctly identifies the `CarInspection.PrepareCar`/`CalculatePrepared` path, its `CarSimulation` construction, the conditional `MechanicalStress` catalogue-loading route, `hasInstalledParts` marker and concrete-component collection boundary. These are material production dependencies, but the old `PartCart.Preview` result is an `InspectionBuild` from `LegacyCapture`/`LegacyResolve`; it does not execute `CarInspection.Calculate*` or `CarSimulation`. Reference B therefore does not close or validate the requested physical output calculation graph.

Before source edits, produce one finite literal method manifest for the candidate paths that will actually be changed or newly routed. For every exact declaration include path, signature, line range, declaration/body and file hashes, purpose, and copied/reused/reader-overload treatment. Resolve the relevant physical callback/virtual/implicit-disposal paths; identify the exact no-JSON typed simulation boundary and prove it supplies completed resolution plus a non-null component sequence (including genuinely empty builds). Keep unresolved graph rows explicit and justify which are unreachable in the candidate path. The existing broad file list and the graph inventory alone do not grant edits to 43 files or to “all transitive helpers.”

The separate `PartCart.Quote`/`InventoryPricing.Sell` receipt and checkout transaction path is S07 scope. L1D's sale read is `Valuations.LoosePriceView` plus its lazy validation and display arithmetic; do not imply that Quote receipt capture is implemented, edited, or proven by Reference B. Pin any unchanged Quote boundary only if the approved L1D method list actually reaches it.

## Blocking active-business-job JSON path

The scoped price view reaches full-owner `MechanicsOwnership.ValidateForQuote`. With active garage jobs, the path includes `GarageBusiness.Validate -> Matches -> Snapshot` and possibly `SnapshotEquality.Serialized<T>`. `Snapshot` serializes current fuel, fluids, tune and slot-ordered parts before hashing. The fallback serializes current contents, decodes the stored `snapshotJson`, and recursively compares reflected public fields with a four-ULP double tolerance except `health`. The original L1D allowlist omitted `GarageBusiness.cs` and `SnapshotEquality.cs`; the new proposal pins the directly relevant declarations but does not yet approve edits to them. Reference B's empty-job fixtures cannot prove this path. The proposal also correctly records that `ValidateForQuote` passes an explicit catalog to its main/Core/VehicleListings path while several other validators read `profile.catalog`; a future typed route cannot silently conflate those two identities.

No acceptable source-only shortcut is established. Reusing the public validator leaves runtime JSON; skipping business validation or using only the hash weakens existing behavior; a differently named JSON serializer remains prohibited. The validation seam proposal correctly refuses those shortcuts and proposes a typed reserved-build snapshot prepared at a named import/creation boundary. That is not yet an implementation contract. Before any code, settle and review:

- the lifecycle owner and persisted/loaded typed representation for new and existing jobs;
- the hash-only legacy case, and the compatibility path for `snapshotJson` records, including malformed/null payloads and exact first-error ordering;
- an explicit nonreflective field comparison reproducing list/null/type behavior and the existing ULP/`health` comparison rule;
- how the current state used by `Matches` is captured and compared without serializing it in the query;
- migration/versioning and cache-miss behavior, without query-time decode, silent acceptance, dropped jobs, healing, or unreviewed S10/S11 activation;
- a frozen independent legacy branch before changing any shared method, plus active-unchanged, changed-field, hash-only, serialized, malformed, duplicate-identity, and catalogue-cold/warm vectors.

If the only semantics-preserving typed representation requires a separately authorized persistence/import prerequisite, keep that prerequisite distinct and locally reviewable. Do not begin L1D production edits until it exists and its exact paths/methods are added to the high-reviewed allowlist.

## Recommended bounded next step and gate

**GO for preparation-only acceptance and a bounded documentation follow-up:** accept Reference B as the legacy-liveness checkpoint, then author a concrete typed reserved-build import/migration contract and focused test design, explicitly split L1D display pricing from S07 receipt capture, and reconcile the exact method-level edit/reuse manifest for the resulting L1D paths. No Game/Domain implementation or schema/migration change is approved by this review. Root/high must independently approve that contract and the complete manifest, including the typed physical calculation route and validation readers, before any source write. Re-freeze source/method hashes and legacy oracle routing before edits; candidate-wide JSON-call/source-purity tests and the full TotalOnly/FeeOnly/PhysicalOnly/Combined matrix remain later gates.

The current candidate test-gaps note is appropriately explicit that the old purity failures are not expected candidate outputs and that the semantic OPEN rows remain unresolved. Its candidate tests are plans, not passing evidence. Full S06 caller/freshness/native/performance work and S07–S14 remain open.
