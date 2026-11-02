# S06D2B1L1B independent design review

**Status: bounded L1B checkpoint approved.** The design gate below was approved before implementation. The released core passed independent source review; the focused diagnostic passed 273/0; the L1A/L0 regression passed 2,591/0; and the Windows build succeeded. Root owns Unity execution, source-manifest/process verification and build-log capture. This approval is limited to the dormant L1B increment; it does not activate callers or close full S06/Step 1 or S07–S14.

## Source and frozen-reference review

I read the objective, implementation phases, current L1A results, L1B contract, and current stock/reservation/catalogue/pricing source. The root-owned frozen `LegacyScopedSupplyReference` matches the current literal `PartsMarket.Stock`, public `CartReservations.Active/Held`, `PartsMarket.Available/Quote/Consume`, `InventoryPricing.NewUnit`, `PartsMarket.Calculate`, and normal non-ready Ledger constructor/`Find`/`Available`/`Quote`/`Take` bodies and ordering. Its ProductKey boundary delegates to the frozen L0 cache-miss reference. I found no transcription mismatch. The internal ready-group Ledger path is distinct and explicitly excluded from L1B.

The revised contract at [S06D2B1L1B_CONTRACT.md](S06D2B1L1B_CONTRACT.md) now specifies:

- `OpenBorrowed(owner)` stores the owner without eager graph traversal; input operations take the original `PartDefinition` to preserve null/error ordering.
- A synchronous owner-thread `IDisposable` working set borrows source references privately, lazily captures product values only on legacy product-comparison branches, and uses copy-on-write only for touched stock/hold quantities. Disposal severs references; owned results contain scalars, row ordinals, identity groups, and immutable deltas only.
- The normal Ledger constructor retains its eager row traversal and first-error order, while ready-product-group bindings stay out of scope.
- Stock and Ledger keep distinct lookup/cache semantics, including repeated object-reference alias groups and warm mappings.
- A nested `MechanicalCatalog.LookupScope` shadows cold/warm/stale `Find` branches without changing the existing `Find`/`RebuildIndexes` bodies or publishing index/revision updates.
- One authoritative Calculate body remains; its only seam is accessibility after freezing an independent literal oracle. `NewUnit` remains unchanged.

The corrected contract distinguishes two easily confused publication rules: cold `Stock` assigns `market.index` before `Dictionary.Add`, so the legacy fixture retains a partial live prefix on duplicate failure; catalogue `RebuildIndexes` publishes only after its temporary map completes. The candidate never writes either source, but preserves those outcomes in its private overlays.

## Test-design reconciliation

The test design now resolves the earlier blockers:

- Cold Stock errors record that the legacy source had already received the partial dictionary; the candidate preserves the prefix privately and leaves its borrowed market index unchanged.
- Calculate tests require only reachable overflow: cost overflow is covered, while impossible per-unit-fee and earlier accumulated-fee overflow vectors are explicitly excluded; unchecked `expressUnits` wrap is pinned with valid inputs.
- The resolved-stock Held overload is explicitly outside this normal-Ledger checkpoint; Ready Ledger remains excluded.
- The old open-constraints list is removed. The design now identifies the test-only authoring-mode fixture, private-index inspection, repeated reference groups, and the narrow nested lookup seam while keeping existing `Find`/`RebuildIndexes` bodies unchanged.

The fee design preserves `InventoryPricing.NewUnit` and delegates arithmetic to the one `PartsMarket.Calculate` body after the frozen independent reference is pinned. Oracle liveness is checked against unchanged live methods on independent fixtures within the completed diagnostic suite; no separate pre-implementation Unity run is needed.

## Review disposition

GO is limited to implementation of `ScopedMarketReadSet`, the scoped default Ledger/delta types, the nested nonpublishing `MechanicalCatalog.LookupScope`, Calculate visibility-only seam, and the reviewed Editor diagnostics. Keep all caller routes dormant. No user-facing activation, broader Apply work, automatic generation fence, worker payload, or full S06/S07 completion is authorized by L1B.

## Independent implementation source review

I inspected the released `ScopedMarketReadSet` implementation, its nested default-Ledger scope and owned result types, the `MechanicalCatalog.LookupScope`, and the relevant legacy methods. I found no source blocker in the required behaviors:

- Owner access is deferred until an operation; product capture remains on product-comparison branches. Cold market indexes and successful aliases stay in private overlays, and warm dictionaries retain their comparer and mappings.
- Stock and hold overlays use reference identity, so repeated object references share one private quantity. `Consume` and Ledger `Take` produce final owned deltas; `Take(..., consume:false)` changes only the Ledger remainder, while consuming Take starts its source delta from the parent scope's current stock overlay.
- The normal Ledger constructor preserves its reservation check, row order, active-row definition lookup/Held calls, and exact-ID assignment order. Ready-group construction remains untouched and out of scope.
- Disposal closes child ledgers, clears the borrowed owner/catalog/index/quantity references, and makes scoped operations reject use. Result position arrays are defensively copied.
- `LookupScope` mirrors the existing `Find` null-ID, authoring, warm/stale-index, fallback and private-rebuild branches without publishing source index/revision state. Root's source-seam proof reports existing `Find`, `RebuildIndexes`, and `Calculate` bodies unchanged; the only existing-domain seams are the added nested lookup scope/factory and `Calculate` accessibility.

The focused checker compile attempt failed before executing probes because `ScopedSupplyChecks.cs` ended without the required closing class/namespace braces and `#endif`. Root reports the source manifest unchanged and has thawed only the checker and its test note for the assigned repair. This is a test-source compile blocker, not evidence against the reviewed production core. I have not run Unity, a build, process inspection, or Git; those remain root-owned. After the corrected checker is released, the full diagnostic, source manifest, regression/build evidence, and any further fixes must still pass before L1B can be called complete.

## Targeted checker-failure review

The next focused run completed 269 checks and reported four failures. I independently checked the fixtures and accepted the targeted checker corrections without relaxing frozen-reference comparisons:

- The catalog malformed-error branch now verifies both matching exceptions and null outcomes (`actual.Value` and `expected.Value`), so it explicitly checks that no result escaped.
- Quote source fingerprints are captured after each `usingCartReservation` toggle, matching the state each operation observes.
- The original old-generation-only Consume vector remains and now correctly expects no stock delta: its request is fully met by the generation-2 and generation-3 holds. A separate vector consumes old holds, current holds, and unreserved stock, asserting the frozen 8-to-3 stock result and every hold result.
- The lifetime check retains the nested lookup reference before disposing its parent and inspects the child set's `Count` property after disposal.

These are test corrections for faulty expectations/observation timing, not changes to the candidate or reduced parity scope. Static review of the corrected checker is clear for a fresh root-owned run; the 269/4 result remains historical and is not a passing checkpoint. Unity evidence is still pending.

## Focused diagnostic review

The corrected root-run diagnostic completed with **273 passed, 0 failed** in `reports/save-refactor/S06D2B1/scoped-supply-20261102-121730488`. I independently read the bounded checks and report, counted 273 `ASSERT|PASS` records and no failure records, and recomputed SHA-256 values: `checks.txt` = `5B6B35E8AF707146727F5736778B73F61815144C132980EBDEA7DD1A27CC0483`; `S06D2B1L1B_SCOPED_SUPPLY.txt` = `4B80401E4622B2F0B4E6660E09A9C267427E73F9D58F3A4078512FA53C737A90`. These match the hashes reported for this run.

The suite confirms frozen-oracle liveness against unchanged live operations, cold/warm stock and catalogue lookup semantics, default Ledger construction and aliases, reservation boundaries, owned Consume/Take deltas, all quote fields and reachable arithmetic cases, source nonmutation, and disposal. The corrected old-only Consume vector produces no stock delta, while the additional vector exercises current holds plus unreserved stock and checks the exact 8-to-3 stock change. Candidate operations report no JSON/key-cache/ID/RNG/entropy effects. Root reports all 2,137 source paths unchanged for the run; I did not independently run the Unity process or source-manifest capture.

The focused result supersedes neither the failed A/B compile attempts nor the earlier C run with 269 passed/4 failed; those remain historical.

## Combined regression review

The root-run combined regression completed with **2,591 passed, 0 failed** in `reports/save-refactor/S06D2B1/scoped-supply-regression-20261102-122005058`. I independently counted 2,591 `PASS` records and no `FAIL` records, and recomputed the checks SHA-256 as `8AB65979ABE10D37A20213855777EE58EF06C2745C1E1B5A8490D97647B87EBF`. The embedded L0 boundary trace hash is `E3233662F204C401EF0D953021584ED32C1AAE819984BA39924B0B8C0F04E9D3`, and the L1A canonical-values trace hash is `8A842342BE6B70FD7F00F020A900663634A348E2489C6E586C54BCD00C414902`; I compared both with their previously accepted standalone traces and verified byte-identical hashes.

The D source manifest is `reports/save-refactor/S06D2B1L1B/source-d-sha256.txt`, SHA-256 `FDA59062A8CF07B95A3AD37F6C492CBADBC19DAE048D92DFA66408AD9CC35413`. I independently checked all 2,137 listed paths: each exists, each content hash matches, and there are no duplicate entries. Root reports the complete source path-set diff is empty and that no actual Editor/player process remained; I did not inspect processes.

## Windows build review

The D `BuildPlayerOnly` log is `reports/save-refactor/S06D2B1L1B/windows-d.log`. I independently confirmed `Build Finished, Result: Success.` at line 8,147 and `DRAGLAB BUILD SUCCESS` at line 8,163. I also independently hashed the ignored local output and confirmed the reported values: `Game/Builds/Windows/DragLab.exe` = `437691663C5FB21231524F4886E60A10B1841F13E42FBBC93EE39FBD827930CD`; `Game/Builds/Windows/DragLab_Data/Managed/Assembly-CSharp.dll` = `D94726CBD130C8BD5C34EF9A1BF530A6BD11A3C3DC92CF7621B50AD32D3F00EB`. Root reports the full source path-set unchanged after build and no actual Editor/player process; I did not inspect processes.

Full stock/ready-group conversion, Apply/Total/DeliveryFee/checkout, native/device performance, full S06/Step 1, and S07–S14 remain outside this checkpoint.
