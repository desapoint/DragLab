# S06D2B1L1C independent design review

**Status: GO for the finite L1C0 reference-freeze and liveness increment only.** Do not edit production domain code in C0. C1 is not approved by this review; it needs a fresh source/API gate after C0 has passed and been locally committed.

## C0 scope and source baseline

The revised contract and test design define a bounded prerequisite: add only `LegacyCartApplyReference.cs` and its metadata, `CartApplyReferenceChecks.cs` and its metadata, plus root-owned runner/evidence. Freeze the current raw `PartCart.Apply` closure before any helper extraction or redirection. The reference must not call `LegacyCartPreviewOracle.Apply` as its sole authority because that wrapper still reaches live acquisition, installation and replacement helpers that may be changed in C1.

I independently checked the baseline manifest at `reports/save-refactor/S06D2B1L1C0/baseline-source-sha256.txt`: it has 2,137 unique entries and SHA-256 `FDA59062A8CF07B95A3AD37F6C492CBADBC19DAE048D92DFA66408AD9CC35413`. The pre-extraction Domain manifest hashes to `DBAC194537761DF67A66FDF89A2AA377B60E588C77990DF1E78E5CB81151D61E`. Root reports that no actual Unity Editor/player process was active at capture; I did not inspect processes or run Unity.

## Catalog isolation correction

The first test-design draft reused the literal profile clone's shared `MechanicalCatalog` across live and reference profiles. That would let one side publish `partIndex` or advance `authoringRevision` before the other side runs, masking cold/stale lookup behavior and partial cache effects.

The corrected test design at [S06D2B1L1C_TEST_DESIGN.md](S06D2B1L1C_TEST_DESIGN.md), SHA-256 `0680342D2F1D1D439483321F4B4BB1069A56DACAA4F054C162D875731F188450`, now requires independent catalogs for paired fixtures, exact authored definition/spec order and value checks, independent initial cache/index state, and separately reproduced aliases/malformed-index layouts. If profile data is literal-cloned to preserve IDs, the clone must be assigned its own freshly built actual catalog before either side runs. This closes the independence issue.

## C0 fidelity and mandatory evidence

I checked the described dispatch against the current `PartCart.Apply`: sales skip, Move occurs once, NewSet installs only on the first iteration, NewUnits installs at the supplied per-iteration positions, and all other action values fall through to used-stock acquisition. The test matrix preserves raw zero/negative quantity behavior, ordering, legacy partial mutations and first errors. It does not use `Total`, checkout, or the preview profile as an expected result.

The physical fixture requirement is sound. `InventoryUnits.Buy` uses `EngineElements.Create(category)`; the Tires factory creates a real `TireCondition`, and `LooseEngineElement.tire` returns the exact tire member through its typed accessor. Wheels use the actual `WheelElement` factory. Existing used-unit fixture helpers do not guarantee these subtypes, so the bounded authored-supply search remains necessary for BuyUsed subtype cases; an unavailable real fit/topology stays OPEN, never replaced with synthetic hardware.

For C0 completion, the frozen closure report must include a method-by-method table for every method in the contract's transitive closure. Each row must state whether the reference contains a literal copy, reuses a previously frozen independent reference, or pins an unchanged helper by source hash. It must also name the C0 fixture/vector that exercises each mutating or conditionally reached route; unchanged helpers must be explicitly marked hash-pinned. Any changed or redirected helper without independent reference coverage keeps C0 open. This is required alongside exact provenance, method/body hashes, source manifests, and liveness comparisons.

With that evidence requirement, C0 is a single coherent frozen-reference gate and does not grow into further characterization slices. Its implementation may proceed within the listed runtime reference/checker paths. The reference should record catalogue cache changes per independently owned catalog rather than suppressing them through shared fixture state.

## C1 architecture boundary

The eventual C1 scope is substantial but coherent: one dormant ordered raw non-sale Apply projection covering NewSet, NewUnits, BuyUsed, Move and sales skips, with one state across all rows. Existing callers remain untouched. No partial Total/fee/checkout slice is authorized here.

The proposed operation-local catalogue read facade is a reasonable direction for a later design gate. It should borrow immutable authored parts/specs by reference, own a private nonpublishing index/revision view, and route every nested `Find`/`Resolve` lookup through the same operation context. It must not copy the catalogue, expose a mutable `MechanicalCatalog` or profile to shared transitions, or leave any nested public `Find` path able to publish. Freeze the relevant existing resolver and helper closures in C0 first; then specify exact facade and transition interfaces in C1. This review does not approve that implementation or any C1 production edits.

Full Apply parity, caller activation, Total, DeliveryFee, PreviewProfile, checkout/sales, native/device performance, full S06/Step 1 and S07–S14 remain open.

## Final independent C0 evidence review — 2026-11-02

**Decision: GO for the frozen L1C0 reference/checker checkpoint only.** This closes the C0 review gate after the root-owned local commit is verified. It grants no approval to edit production Domain code or begin the typed C1 extraction; C1 still needs its own API/source review after C0 is committed.

I reviewed the current raw `PartCart.Apply` path and its transitive acquisition, installation, supply, catalogue, and Move closure against the contract, test design, frozen reference, checker, closure map, and method/body provenance ledger. The reference retains the row loop and legacy branch order; its acquisition/install copies are isolated from live helpers, while the established independent I/J/K Move and L1B supply references are reused. The tire path is reachable through the concrete `LooseEngineElement.tire` accessor and preserves raw NewSet empty-member state followed by later unit initialization/displacement without a final normalization pass. The corrected BuyUsed-unit fixture obtains real dealer units with bounded `AddUsedSupply` attempts, constrains them to the installed definition and role, checks the authored target, and removes failed attempts from the disposable fixture. No remaining fidelity, independence, fixture-validity, or mandatory-method-closure blocker was found.

I independently checked all 240 declaration/body hash rows in the method provenance ledger; every recorded hash matched its normalized source range. The ledger now identifies literal reference methods, reused frozen methods, unchanged source-pinned dependencies, current K adapter/state methods, and routes explicitly marked not live. The closure distinguishes static call linkage from runtime method instrumentation. No required contract helper was missing from the mapped or explicitly source-pinned closure.

Evidence reviewed:

- Focused C terminal trace: **642 pass / 0 fail**, exit 0; checks SHA-256 `A40641322DF065DB81DFFF7684563A311BF721B1B5CCE792F01E2DE1EBA67BE0`, trace SHA-256 `BC7859FB5CD2C757BCFC44ACE0A2AE2EE7F8CFB186B2149D71D01695B38CD35D`.
- Combined C regression: **2,864 pass / 0 fail**, exit 0 (L1B 273, L1A 2,441, L0 150); checks SHA-256 `2FB13B95C57DEFCEB7D3E595082186DFB8310980DC3FBE8D81F3298E94FF50BD`. The accepted L1B/L1A/L0 trace hashes remained byte-identical.
- Frozen source manifest: 2,141 paths, SHA-256 `1850651CBA0724AFCC432FFDE2DCCA63A1899C92DDAAEF7C64547FA672BD1D1D`; root verified the full path/hash set and 137 prior dependencies unchanged, and verified actual Unity Editor/player absence after both terminal runs.
- The 2,141-path manifest differs from the 2,137-path pre-C0 baseline by the four intended C0 Runtime `.cs`/`.meta` additions and one Editor runner change; root reports no other source changes. New files and runner remain Editor-guarded. The pre-extraction Domain manifest remains 127 paths and unchanged.
- No production Windows/Android build was rerun because the additions and runner change are Editor-only and production bodies/callers are unchanged. The previous `6cbc2dd` Windows build remains the production checkpoint; this review makes no new build, native performance, or device claim.

Attempt A's checker compiler errors and Attempt B's one invalid fixture selection remain historical evidence; neither is counted as a pass. The corrected C run passes the complete focused matrix. Runtime evidence and limits are recorded in [C0 results](../../reports/save-refactor/S06D2B1L1C0_RESULTS.md), with exact closure routes in [C0 closure](S06D2B1L1C0_CLOSURE.md) and body hashes in the [provenance ledger](../../reports/save-refactor/S06D2B1L1C0/S06D2B1L1C0_METHOD_PROVENANCE.md).

This decision closes only the C0 prerequisite. It does not prove a future typed candidate's parity, ownership, no-heal behavior, allocation behavior, or output isolation. C1 remains subject to its separate design gate and full differential/regression requirements.
