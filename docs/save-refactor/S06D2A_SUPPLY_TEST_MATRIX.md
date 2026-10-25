# S06d2a supply projection test matrix

Verifier entry: `ShopSupplyProjectionChecks.Run(Action<bool, string>, string)`.
The parent owns the Editor route and all Unity/native execution. This matrix
does not claim a build or test result.

| Area | Oracle |
| --- | --- |
| Fitting quantity | The verifier retains a local implementation of the pre-index `Counter` selection rule: fixed four-unit categories, unsupported categories as one, selected vehicle only when it fits, then first ordinal model that fits, using the existing exact `Fits` and `Count`. It compares every catalog product for a selected car and no selected car against both the unchanged public `Counter` and `CounterIndexed`. A product incompatible with the selected vehicle but compatible with another model is asserted explicitly. |
| Readiness fence | A cold catalog yields no groups. The warm iterator must remain unavailable before `parts/shop ready`, publish both groups and alias index at that marker, and retain readiness while later fit/layout warmup yields continue. |
| Row selection | Independent fallback `Ledger` and ready-prebound `Ledger` are compared for direct IDs, direct aliases, an unlisted equivalent alias, and a missing product. The fixture arranges an earlier equivalent row and a later row containing the representative ID to test direct-ID precedence separately from first-equivalent-row fallback. |
| Quote arithmetic | Compare cost, fee, local units and express units against hand-computed quantity/fee oracles for partial stock, missing stock, sequential `Take`, and a changed delivery fee/base price. |
| Live state and reservations | Compare new ledgers after row-quantity changes. With an active selected-car cart hold, compare the ordinary availability view against the reserved-owner view and assert read-only quotes preserve the hold. |
| Cache isolation | On a fresh no-hold fixture, binding ready groups must not populate the profile’s global market index. The production binder is checked through the ready ledger API and the fallback remains the original ledger path. |

All stock and profile data are in-memory test fixtures. No player save is
written or restored. Parent execution will append actual results after the
source freeze.
