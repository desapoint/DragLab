# S06d2b step1: characterization decisions

Baseline: `d541ad8`, with `c4dab7d` approved closure/matrix and `a4489bd` final JSON boundary clarification. This increment preserves and measures legacy behavior before any new planner or caller switch. The live Parts component is functionally committed (Windows2937/native94925:451/0), but its frame gate remains FAIL13/6324 and actual Move410.077ms is not solved. S06 and the complete S00–S14 objective remain active.

## Test and reference boundary

All new runtime-assembly fixture/oracle/check source is wrapped in UNITY_EDITOR. This permits access to existing internal domain helpers while omitting the legacy JSON reference from Windows/Android players. The parent-owned Editor runner is batch-only, outside Play Mode, and writes solely to an isolated diagnostic directory. It never selects, persists or replaces a player's save.

Freeze the current readonly Total/DeliveryFee/Apply/Preview/Profile method logic and record source hashes. Frozen methods still call existing helpers; independently recorded contract assertions/traces and helper hashes must prevent future helper extraction from silently changing both oracle and candidate. Do not label a self-comparison of identical implementations as proof of a new typed planner. There is no typed candidate in this first checkpoint.

Initial valid fixture creation may use authoritative Ensure and JSON isolation for the Editor reference only. Deliberate invalid conditions are introduced afterward. Measure whether Ensure is actually a no-op on valid fixtures; preserve its observed changes and explicitly classify legacy healing on invalid graphs as future typed rejection, never an accepted normalization shortcut. No production API, transaction, save, market probability or simulation change is authorized by this test increment.

## Exact comparisons and determinism

Keep existing source IDs literal and exact across source/candidate operations; compare resources, ownership, locations, member order/positions, numeric bits, nulls, faults, fluids, warnings and first errors. Copies for a frozen reference are test isolation only. New generated physical IDs can differ between runs: compare them through a bijective operation/member topology mapping, preserving every cross-reference and uniqueness constraint. Diagnostic cross-run aliases must never rewrite the actual domain graph or hide a changed existing identity.

Use explicit timestamps/seeds for direct domain probes. Record that the actual native Move Candidate omits `now` and consumes RuntimeEntropy.UtcNow. A direct fixed-clock probe does not prove actual native dialog replay; preserve that distinction and mark unexecuted caller/clock cases OPEN. Capture remove-all-same-asset then append-last ordering as actual native semantics, not an invented in-place replacement.

## Scope, evidence and next gate

Characterize all supported row classes, ordered mixtures, maximum100 rows/quantity99, units/sets and positions, tires/wheels/brakes, multiple cars and detached engines, block preparation, sale ordering, stock/partial express and reservation lifetimes, malformed inputs and first-error order. Do not shrink the supported cart to easy successful examples. Negative and mismatch outcomes are evidence, not permission to weaken the oracle.

Record separate Total/Preview/Ensure/Move/Resolve timings, available allocation counters and fixture sizes. Copied-oracle subspan timings are diagnostic, not production-frame proof. The actual native frame gate remains50ms and must include interaction, preparation and final attachment/layout when the future typed caller is switched. A stopwatch-only Editor result cannot clear it.

Complete and release both author files before root runs one Editor. Parent exclusively controls Unity/player/process/Git and freezes all Game source during checks. Independent Luna-high review, actual tests and a local commit are required before the shared domain-planning seam. Step1 stays OPEN while any required coverage is merely described or unresolved; no S07 advancement follows from source preparation.

## Observed legacy serializer defect and audit collection

Editor60108/15342 compile and reach26 passing assertions, then correctly fail mixed Total source immutability despite equal260 totals. Diagnostic42254 isolates Unity ToJson: inactive dealer payload unit changes null to an empty object. ToJson-only, ToJson+FromJson, Total-with-Move and Preview mutate; FromJson-only and Total-without-Move do not. Six diagnostic operations completing is not a source-immutability pass.

Do not normalize/exempt that field or claim the game fixed. Keep this defect as a required divergence for the typed replacement. Add collect-all characterization to retain every red assertion, continue independent groups with explicit dependent skips, and exit nonzero if any assertion fails. Strict mode still stops at its first failure. This allows discovery without converting a known failing invariant to green or prematurely advancing a planner.

Actual/oracle calls must receive independent copies of the same pre-call data. An Editor-only literal reflection copier preserves nulls, exact fields/IDs and reference aliases without invoking Unity JSON or DTO constructors; it is test input isolation only and must not ship or be used for saving/runtime previews. Shared authored catalogue references are audited for immutability, and a changed catalogue makes dependent oracle comparisons invalid/skipped. The no-runtime-full-profile-copy requirement remains unchanged.
