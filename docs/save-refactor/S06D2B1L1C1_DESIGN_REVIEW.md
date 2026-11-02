# S06D2B1L1C1 — independent design review

**Decision: GO for the single C1 design boundary and frozen-reference preparation only. No production implementation is approved by this review.** Root must verify the design-only local commit before preparation begins. Before the first Domain edit, the newly frozen catalog-call reference and its exact pre-change method/source hashes still require a separate high semantic and hash review. Do not start C1 production edits until that review passes.

## Reviewed baseline and documents

The raw Apply C0 prerequisite was committed as `67f401a`. Root captured the C1 pre-edit manifest at [baseline-source-sha256.txt](../../reports/save-refactor/S06D2B1L1C1/baseline-source-sha256.txt): 2,141 paths, SHA-256 `1850651CBA0724AFCC432FFDE2DCCA63A1899C92DDAAEF7C64547FA672BD1D1D`. Root reports no source or Game/tools changes since that capture. This is a design review; I did not run Unity, a build, or tests.

I reviewed the current contract, API design, and differential design together:

- [L1C contract](S06D2B1L1C_CONTRACT.md), SHA-256 `3F48D3470401C7D198EDB4B54DF1782E373534095D6D3EE6630E928A1992C5A3`.
- [Implementable API design](S06D2B1L1C_API.md), current SHA-256 `A99EC7077FED2709DCC5C9C764C7907ED703C65364065E42988F8AA11BE7D36F`. Root verified this changes only excess end-of-file whitespace from the reviewed `4CE574...F27C` version.
- [C1 differential test design](S06D2B1L1C1_TEST_DESIGN.md), current SHA-256 `D0DEB321EFC8AAF7B37E3F0BCBF7EB771FACB74097C21BD2917E078F072B2E3D`.

## Findings

The API describes one dormant `Evaluate` entry point and one ordered, lazy `ApplyRows` loop shared by the live adapter and candidate. The live route keeps the existing `IEnumerable<PartCartLine>` signature and public supply/identity behavior. The candidate owns one source-reference COW working set with ordered additions and tombstones, one L1B supply overlay, and one nonpublishing catalog reader. The shared rule body receives only the state, catalog, supply, and identity capabilities; it cannot access a mutable profile or catalog. No profile clone, JSON path, caller conversion, sales, quote/checkout, or additional production slices are introduced.

The catalogue capability covers the paths that the earlier draft omitted: `Models()` preserves the exact ordered model enumeration used by `PartsMarket.FitsAny`, `Reference`, and cross-model `Count`; `Recipe(model)` delegates to the existing factory recipe at its legacy point; `FindProduct` preserves stock identity-group metadata; and `Spec`, `Fits`, and `Resolve` are routed through the operation's one lookup scope. The API also specifies the independent pre-C1 `Find`/`Spec`/`Resolve`/nested-lookup freeze and hash review that must complete before any Domain edit. Public live supply calls and candidate scoped supply calls are behind one shared transition capability, with no candidate-to-live adapter call.

The state and identity boundaries are concrete enough to implement: borrowed part/unit/dealer values are read-only; edits and dealer transfers copy before mutation; K/I/J lookups use typed identity references; raw row IDs always enter as existing identities; candidate symbol fields are private, source-collision checked, never exported, and never used for raw-ID lookup. The loop does not pre-count, index, copy, or enumerate rows twice. The enumerator failure row rule is explicit. Live cash writes reach the owner at the original statement point while candidate writes remain in the private scalar overlay. The result DTO lists success/failure fields, owned snapshot contents, deletion markers and order, and the test design checks those fields and defensive copies.

The explicit member policy preserves legacy live normalization and makes the candidate no-heal boundary enforceable before any source-owned member normalization or ID allocation. New authored products can initialize their own required members; source records retain existing IDs, wear, faults, history, and missing members. The tire case preserves the raw blank NewSet member state without a final Ensure pass.

The differential design covers the full non-sale row loop, error and partial-prefix behavior, local/express pricing and reservations, source immutability, output ownership, lazy enumeration, typed identity collision, and real authored physical subtypes. It explicitly keeps the two K stored-engine part routes (`InstallEnginePartAfterStored` and `RemoveEnginePart`) pending until they execute through both `LegacyCartApplyReference.Apply` and `Evaluate`; standalone K evidence is not counted as Apply coverage. No fixture skip or unsupported-topology pass is allowed.

The final test-design revision also adds direct candidate checks for catalog `Models()`/`Recipe` fallback and full resolution under cold/warm independent catalogs, and explicitly compares `paintHex` plus the complete rebuild job/target snapshot for affected cars. I found those additions consistent with the API and source behavior; they close the catalog and snapshot detail gaps without widening the authorized production scope.

## Gate boundary

This GO approves the reviewed C1 design and preparation of the additional frozen catalog-call reference. It does not approve any production code edit. Before the first Domain change, root/test author must provide the exact pre-change source/method manifest and independent literal catalog-method references for `MechanicalCatalog.Find`, `Spec`, `Resolve`, and every nested Find-calling helper being redirected, with a reviewed hash/fidelity table. `Recipe` is currently a pure source-pinned factory helper; keep its hash and call behavior pinned, and freeze its body independently only if implementation redirects or changes it. This is an in-gate review step, not another production microslice. Then the entire C1 production checkpoint and its complete frozen-reference differential, L0/L1A/L1B/K/I/J regressions, source/process evidence, and Windows build require their own completion review.

C1 implementation, candidate parity, no-source-mutation proof, allocation behavior, performance, native/device behavior, and full S06/Step 1 remain unapproved and open.
