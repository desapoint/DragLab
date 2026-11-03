# S06D2B1L1D — independent cart-query design review

Date: 2026-11-03. Baseline: `e261080` on local `main`.

Decision: **approved for the documentation checkpoint and independent frozen query-closure preparation only.** This approves the design contract, not a production source freeze, source edit, or implementation. Before production changes, the exact query method/dependency closure and frozen oracle routing still require a separate review and GO.

## Reviewed documents

- API design: [S06D2B1L1D_CART_QUERY_DESIGN.md](S06D2B1L1D_CART_QUERY_DESIGN.md), SHA-256 `ED37A8813CF3555A3CBECD95DB84424F4002916ECBD5A1470B3B7768B7F9C53D`.
- Test design: [S06D2B1L1D_CART_QUERY_TEST_DESIGN.md](S06D2B1L1D_CART_QUERY_TEST_DESIGN.md), SHA-256 `D61ACA7055A7EDA0EDAE5A810711097C0AEAE5DE3866FEB3CDFA76B1CFE9EB1D`.

I independently recomputed both hashes and checked the final request-error, output, clock, and setup sections in both documents.

## Review findings

- **Request boundary is concrete and consistent.** Ordinary misuse returns `RequestError` at `ApiRequest/-1`, with no mode result slots and no owner/catalog/clock/provider/scope reads. Both documents specify the same ordered guards and exact exception type, message, and parameter: invalid purpose; null input; null output; purpose/output mismatch; unsupported `validateBlocks`; unsupported `includeOwnerReservation`. Combined therefore has no ambiguous aggregate error case. A null owner or car ID remains a valid request and follows each mode's own legacy first-read/setup path.
- **Modes preserve distinct semantics.** `TotalOnly`, `FeeOnly`, and `PhysicalOnly` have separate phase order, outputs, and forbidden work. `Combined` executes all three in that order with independent overlays, ledgers, errors, and nested identity namespaces; one mode's failure does not suppress its siblings. The weaker fee traversal, lazy sale validation, Move-only scratch, block readiness, and physical preview path remain distinct.
- **Input and output ownership are specified.** Captured rows and nested positions are copied once with null/empty distinctions retained. The output request is sealed and immutable, `None` is the canonical null-target/all-false singleton, and target arrays are defensively copied at construction and access. Null versus empty target selection, ordering, duplicates, requested-only inspection, and absent unrequested output are covered.
- **Clock behavior is bounded.** A readonly clock-input struct accepts nonzero ticks or an optional caller-owned synchronous provider. Only Total can invoke the provider, at the legacy point after target/rebuild and row-count checks; missing or throwing providers become `TotalDefaultTime/-1` errors with no Total outputs. The caller's input is unchanged, the provider is cleared from scope-local state, and no result retains it. Fee/Physical and pre-time Total failures perform no clock read.
- **Ownership, freshness, and scope limits are appropriate.** No profile/catalog serialization or clone, mutable domain record, source collection, candidate symbol, or mutable operation context escapes. Physical previews are explicitly requested and owned. L1D is same-turn synchronous with no yield; its dependency token is not a later-frame freshness ticket, and no A→B→A guarantee is claimed without a future writer-generation/action-fence design. The exact initial path list and pre-edit method/range/hash closure expansion requirement are explicit.

No blocking design gap remains in these released documents. The tests are a proposed implementation gate, not evidence that any query code has been built or run.

## Authorized next boundary and limits

This decision permits the independent frozen query-closure/oracle preparation and its review. It does not authorize production edits until the reviewed method-level ownership map, frozen copied-helper provenance, and oracle routing are complete and separately approved. It does not convert callers, change Quote/Checkout, publish sales or reservations, or complete S06 or S07–S14.

No L1D runtime, compile, build, performance, frame-cost, or device result is claimed here. Those gates remain required after implementation. Native frame/performance and device acceptance remain separate, and the prior C1 checkpoint is not evidence for L1D behavior.
