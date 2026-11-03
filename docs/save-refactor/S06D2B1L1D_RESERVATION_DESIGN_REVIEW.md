# S06D2B1L1D — reservation design review

Date: 2026-11-03. Baseline `e261080`; query reference-preparation checkpoint `38bbdd3`.

Decision: **GO for the bounded typed reservation value/import checkpoint and its focused Editor-only checker.** The revised contract preserves the three ordered legacy outcomes: exact hash baseline, exact JSON-text baseline, then decoded structural fallback or its deferred error. This does not approve query production changes, caller activation, load/schema changes, or full L1D acceptance.

## Reviewed documents

- [Reservation contract](S06D2B1L1D_RESERVATION_CONTRACT.md), SHA-256 `6D2C4D02265626061CD914E38BE395E9D58A4AB87680080E918B63B59A1D7822`.
- [Reservation test design](S06D2B1L1D_RESERVATION_TEST_DESIGN.md), SHA-256 `C7279A636AD8116C5B4C235FA029679A5D36598A9E73B1EE3B5E7DFD39B7250F`.
- [Validation route review](S06D2B1L1D_VALIDATION_ROUTE_REVIEW.md), SHA-256 `508DECC08AFCBAB83A1D77808C797970156D8B585305AB668E3962FB503FF971`.

## Accepted bounded design

The proposed split between an owned typed `ReservedBuildValues` domain representation and a single infrastructure-only `LegacyReservedBuildImport` boundary is appropriately narrow. The design preserves persisted job fields, leaves current load/business methods untouched, requires an explicit prepared context for the dormant query, and forbids query-time JSON, reflection comparison, whole-profile copies, or fallback into `GarageBusiness.Matches`. The explicit field inventory covers the serialized reservation graph, including element runtime types, order, null structure and raw ID strings. The proposed exact hash baseline separately includes ordered binding slot labels, which are not part of `ReservedBuildSnapshot`.

The validation route review correctly keeps explicit catalogue and `profile.catalog` capabilities distinct, routes the candidate around the listing-proof cache, and pins the nested SharedInventory/PartsMarket/CartReservations/SavedSetups routes. These are finite future L1D reader changes, not permission to edit the current methods. The reservation prerequisite can remain dormant and explicit; no load hook, assignment rewrite, schema change, or live caller activation is needed for the focused typed-value/import checkpoint.

## Three ordered authorities checked

The revision now keeps the three legacy cases distinct in both the value result and the query comparison order:

- `HashBaseline` is exact typed contents plus ordered binding slot labels. It is checked first; a match accepts without consulting JSON-derived state.
- `JsonTextBaseline` is the typed contents observed when current legacy serialization exactly matched the stored payload. It is checked second with exact typed comparison and no separate slot labels.
- `JsonExpected` plus `FallbackKind` stores the decoded structural expectation, absence, or deferred failure. It is checked last with the legacy field/ULP rules or returns the recorded failure at the reserved-job validation position.

After a hash-baseline mismatch, the query verifies the stored JSON digest before using either JSON-derived view; a stale context fails closed without query-time import/decode. Successfully decoded expectations remain present even if they initially mismatch the source, allowing a later return to that expected state to match. Parse/compare errors are captured at the named import boundary but stay latent while either earlier exact baseline matches. This preserves hash-first short circuiting and the exact serialized-text branch without making importer outcomes throw early.

The three-branch ordering and deferred error timing agree with the updated test design. Its exact JSON-text/decoded split, valid contradictory payload, initial mismatch then restore, malformed payload latency, hash-only slot-label, and ULP controls exercise the important distinctions. The design also correctly refuses to claim exact Unity JSON spelling parity: a future typed state might serialize to identical bytes without matching any retained exact typed baseline. That remains the documented, narrow compatibility limitation; it does not justify dropping a stored decoded fallback.

Implementation must retain the three authorities separately; do not collapse them into a single `Values` object or generic tolerant comparator. Exact hash-baseline numeric comparison is bitwise as specified, while decoded fallback uses legacy `x == y` then ULP behavior (including its signed-zero, infinity and NaN consequences). Import-time JSON work is permitted only in the named infrastructure boundary and must be counted separately. The query must remain at zero JSON calls.

## Gate decision

**GO for a finite source checkpoint containing only the new Domain typed values/comparer, the new Infrastructure import boundary, matching `.meta` files, and a new `UNITY_EDITOR` focused checker.** Before editing, capture exact source/method hashes for the legacy snapshot/hash/Contents/matcher and relevant import/copy leaves, plus the frozen reservation oracle route. The checker must characterize the inline-null/roundtrip-shaped exact-text branch against that oracle rather than assume the outcome. The checker may be exposed through an existing public Editor `RunBatch` entry and Unity execute-method; if that is not workable, request a single explicit root-owned Editor runner exception before changing any existing runner. No edit to GarageBusiness, SnapshotEquality, load/store/decoder, caller, persistence/schema, or L1D production query paths is authorized. Do not claim query-time no-JSON or full parity until the focused runtime evidence passes. Full L1D, S06 and S07–S14 remain open.
