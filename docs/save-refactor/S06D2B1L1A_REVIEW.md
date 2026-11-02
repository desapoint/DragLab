# S06D2B1L1A independent design review

**Status: GO for the bounded L1A implementation.** The amended contract and test design now require full freshly loaded catalogue coverage, explicit fail-closed product comparison/hash APIs, and null/empty oracle pairs in both directions for each included serialized product string. Scope remains new dormant owned value types and their diagnostics; no existing helper or live route changes are authorized.

## Review scope

Reviewed `S06D2B1L1A_CONTRACT.md`, `S06D2B1L1A_TEST_DESIGN.md`, and the current declarations in `MechanicalCatalog.cs`, `AwdHardware.cs`, `AxleDriveDynamics.cs`, `PartCart.cs`, plus `MarketCatalogLayout.AuthoredComparedFields()`.

`AuthoredComparedFields()` currently enumerates the `PartDefinition` fields, including `id`, and all declared fields of `AwdHardwareSpec` and `AwdSpec`. It is a useful explicit inventory, but it does not by itself guard the independent `PartCartLine` inventory or guarantee the new value's mappings. Keep the implementation's field mapping and test-time expected inventory explicit and compare the sets exactly; do not use runtime reflection as the production mapping.

The source confirms the default-sensitive boundary: `PartCartLine` has field initializers (`quantity = 1`, `destination = Storage`, and an empty `positions` list); `Copy()` preserves other values and copies positions with null mapped to empty. `PartDefinition.Copy()` copies `awdHardware`, whose `Copy()` copies `calibration`. Pin these exact helper/default dependencies in the pre-extraction source manifest and keep them outside the new implementation call graph.

## Final re-review

The two outstanding design points are resolved. The test design now uses the observed count from the freshly loaded authored catalogue and explicitly names `name`, `fitment`, `brand`, `quality`, `followerInterface`, `description`, and `provenance` for both null-to-empty and empty-to-null comparisons; `id` remains excluded by product equivalence. It requires full actual-catalogue and mandatory-edge parity, with no unsupported subset or cart-only completion.

Fail-closed semantics are explicit: unsupported comparison/hash results cannot be consumed; unsupported self-comparison does not short-circuit; comparison to null is supported and not-equal; hashes only narrow candidate buckets and exact typed comparison decides equality. The full Apply closure is deferred until immediately before mutator extraction. The declared source field inventory matches the current `PartDefinition`, `AwdHardwareSpec`, and `AwdSpec` declarations; keep the separate `PartCartLine` inventory explicit in its test guard.

GO is limited to adding the two new Domain value files with metadata and focused Editor diagnostics. Keep all existing helpers, JSON boundaries, ProductKey, catalog/stock paths, cart routes, Apply/Total, and callers unchanged. The checkpoint cannot be marked complete without the contract's full parity, source-preservation, and no-unsupported-result gates. No baseline Unity run before implementation is needed; root must freeze the specified sources before diagnostics.

No production edits or runtime/build/process/Git actions were made for this review.

## Core source preflight

**Status: source preflight GO; test acceptance remains pending.** Reviewed `CartRowsSnapshot.cs`, `PartProductValue.cs`, and `S06D2B1L1A_CORE.md`. The cart capture enumerates once, owns its position strings, distinguishes raw null row/list/item facts, and separately applies the frozen default-row and effective null/empty rules. The returned row is immutable; its only array accessor clones. The product capture retains the ID only for raw equality, maps all declared product/AWD scalar fields explicitly, keeps raw float bits and nested null-shape flags, and returns a defensive field-path array. Product equality and hashing use the same effective string/float/scalar sequence; hash matches still require exact comparison, and null `other` is supported/not-equal without an unsupported-self shortcut.

The core adds only the two approved Domain files; this review made no edits to existing helpers. Static source review is not evidence that JSON edge behavior or authored partition parity passes. The mandatory focused diagnostics must still verify the field guard, every entry in the freshly loaded actual catalogue plus the three aliases, complete frozen-oracle partition and edge parity, ownership/non-mutation, and zero unsupported/open results. No Unity/compiler/run was performed as part of this source review.

## Checker source preflight

**Status: static GO to launch the frozen Unity diagnostic.** Reviewed `CartCanonicalValueChecks.cs` and `S06D2B1L1A_TESTS.md` after the independent field-metadata guard was added. The test-time reflection is limited to serialized field name/type inventory; value capture and equality remain explicit typed code. Independent literal inventories cover all four current classes and the checker compares them with `PartProductValue.FieldPaths`. The authored partition uses a fresh `MechanicalCatalog`, records its observed count, adds exactly three aliases, compares exact frozen signatures, and resolves hash buckets through the typed equality API. Cart null/default/order and single-pass cases, product field mutations, string null/empty pairs, nested AWD shape, float/string edges, ownership, source references, bounded logging, and JSON/RNG/identity instrumentation are represented. I found no remaining static blocker in the released checker.

This is permission to run the agreed diagnostic only, not acceptance of its results. Keep every source path frozen through execution; any failed assertion, unsupported required value, oracle error not matching the documented outcome, or incomplete report keeps L1A open. No Unity/compiler/run was performed by this review.

## Post-C diagnostic correction review

Run C is retained as **1,250 passed / 1 failed**, not as a completed checkpoint. The sole failure was a required product-string pair: an isolated high surrogate and U+FFFD produced equal frozen Unity key strings while the typed comparison considered them different. The authored partition itself was bijective for 9,294 generated definitions plus three aliases (9,297 total; 6,238 equivalence classes), but that does not override the required edge failure.

The correction introduces a shared allocation-free effective UTF-16 character view used by cart/product signature equality and product hashing, while raw snapshot equality retains original code units. It maps isolated high/low surrogates to U+FFFD and leaves valid surrogate pairs unchanged. The checker now probes both directions for isolated high and low surrogates against U+FFFD across every product string and cart string, plus position items, valid supplementary pairs, and mixed malformed-plus-valid sequences. The correction preserves the API, field order, and hash/equality coupling. Static re-review found no source blocker.

**Status: GO for the frozen D rerun only; L1A remains open.** Root must rerun the complete diagnostic and inspect all results; no test or build was run by this reviewer. Any remaining failure, unsupported value, or incomplete evidence keeps the checkpoint open.

## D Unicode observation probe review

Run D is recorded as **1,852 passed / 26 failed**. All 26 failures are the mixed malformed-surrogate-plus-valid-supplementary-pair cases; ordinary malformed-surrogate replacement pairs and the complete authored partition still pass. The underlying report states 9,294 actual generated definitions plus three aliases, 9,297 values total, with 6,238 oracle and typed equivalence classes. Retain D as a failed checkpoint.

The next checker-only change adds observation records for the exact input UTF-16 code units and the frozen JSON token for that one known field, capped at 96 code units with total length. It adds no pass/fail expectations, does not emit full ProductKey strings or unrelated JSON fields, and does not modify the frozen reference or core value types. This is adequate to learn Unity's conversion behavior before making another semantic core change.

**Status: GO for the frozen E diagnostic run, including its known 26 failing assertions.** Treat E as characterization evidence only, not a passing checkpoint. Keep the product core and all other source files frozen. Use the bounded field-token observations to justify a subsequent narrow correction and expand mandatory vectors only where the observed conversion requires it. L1A remains open until a later full run passes every required assertion.

## E observations and stream-reader correction

Run E remains **1,852 passed / 26 failed**; it is characterization evidence, not a pass. I inspected the bounded frozen-token observations. They support these exact cases: `high + high + low` emits two replacement characters; `high + ASCII` emits one replacement character (the ASCII code unit is consumed); two highs emit one replacement; a standalone low emits one replacement while a following valid pair is preserved. The failures are the expected mixed malformed-plus-valid-pair comparisons, consistently across the five cart strings, seven included product strings, and position items.

The correction uses a lazy effective-character reader: a high surrogate consumes the next code unit, preserving it as a two-unit pair only when that unit is low; otherwise it emits one U+FFFD and discards that next unit. A standalone low emits U+FFFD. Equality streams those effective characters without assuming the raw lengths match, and product hashing uses the same reader and effective output count. Raw equality still compares the original strings. The checker now pins positive and negative controls against the frozen oracle for all affected fields, including the formerly failing mixed pair. These additions retain strict oracle assertions; they do not waive or filter failures.

**Status: GO for the frozen F rerun only; L1A remains open.** Static review found the new reader/hash pairing consistent with the exact E observations and the expanded test cases. The diagnostic must run the full suite with all source paths frozen; any failure remains a blocker to checkpoint completion. No Unity/compiler/process action was performed by this reviewer.

## F diagnostic evidence review

Run F reports **2,441 passed / 0 failed**. I independently checked the bounded report header, failure/open markers, and partition summary: it records 52 loaded vehicle assets, 9,294 actual generated catalogue definitions, three aliases (9,297 total), and a bijective 6,238-class frozen/typed partition. The report explicitly omits raw key bodies and has no FAIL or OPEN rows. The covered groups include the corrected malformed UTF-16 controls, raw/effective cart comparisons, field inventories/mutations, nested AWD values, float/string edges, hash collision handling, source preservation, and instrumentation.

At that point this approved the focused diagnostic only; the final regression/build decision is recorded below. No regression/build/process/Git action was performed by this reviewer.

## Final bounded checkpoint verdict

**Status: APPROVED for the L1A canonical-value checkpoint.** I independently confirmed F's 2,441/0 header, no failed/open assertions, and 9,297-entry, 6,238-class partition summary. I also counted 150 passes in the unchanged L0 regression report and recomputed its checks/trace hashes; they exactly match the previously committed L0 checks (`325730818087B124E012F9D8E22DA57E6CB53C5F3DEDCB54EB898C067AD25F90`) and trace (`E3233662F204C401EF0D953021584ED32C1AAE819984BA39924B0B8C0F04E9D3`).

The parent execution record documents a successful Windows build (`Build Finished, Result: Success`), launcher SHA-256 `437691663C5FB21231524F4886E60A10B1841F13E42FBBC93EE39FBD827930CD`, assembly SHA-256 `BC431DF8FFB6FFC8C590A5971D63A238B8926481B480AE9D1FF3E14D7F181DD4`, unchanged 1,582-path source manifest after build, and no actual Unity Editor/player process. I did not run the build or inspect process state myself; these items are attributed to the root result record.

Approval is limited to the two dormant owned value types and their diagnostics. ProductKey/row JSON bridge retirement, stock resolver overlays, frozen mutating Apply and complete non-sale Apply, Total/DeliveryFee/quote/checkout, UI caller activation, full S06/Step 1, and S07–S14 remain open. No timing, allocation, native/device, visual, or broader gameplay acceptance is implied.

## Post-build whitespace-only cleanup

The final checker now ends directly after `#endif` with one normal final newline. Root reports that the only post-build delta from the tested checker was removal of one blank line at EOF: zero insertions and one deletion, with an ignore-blank-lines diff empty. The current checker SHA-256 is `F7959A861FE7630B8952391CDE468171F522B42A31CB7CF05A2841B89BD863B4`. This is a whitespace-only edit after the successful F run/build; no rerun is needed for that cleanup. L1A approval remains unchanged and applies to the tested semantic content.
