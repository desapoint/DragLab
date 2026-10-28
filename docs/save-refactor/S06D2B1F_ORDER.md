# S06d2b1f — sale/checkout and occupied-slot order characterization

**Status:** Editor-only characterization source; execution and independent review pending. This is a finite Step 1 supplement, not a typed planner, no-heal change, checkout conversion, or S06 approval. It does not change the main save path or S07 transaction authority. Known legacy JSON/source and output differences remain strict.

## Scope

The shared partial entry point is `RunOrderSupplement(Trace,string)`. Subsets are `order` (both families), `order-sales` (cash-constrained sale/purchase/move), and `order-chain` (occupied-slot displacement). Every fixture starts from `CartPreviewCharacterizationFixtures.CreateNormalized` and uses the current authored catalogue, market, actual physical identities, and literal profile copies. Missing authored shapes emit a named OPEN and failed fixture assertion; the checker does not invent parts or repair malformed inputs.

### Sale, purchase, and move

The sale fixture buys two distinct real compatible single-part offers for later sale and move, plus one authored multi-member set for purchase. It controls that real product’s shelf to `memberCount - 1`, captures the exact local cart hold, and leaves one set member to express delivery. It then sets cash to the nonnegative net cart amount: current cash is below gross purchase price, while the fixed-time sale value covers the shortage. The sale-first rows are SellPart → NewSet → Move; the supported permutation is NewSet → Move → SellPart. Each order receives its own matching current-generation reservation, so the inverse order is not incorrectly priced as an unrelated cart.

Both row orders run production and frozen `Total`/`PreviewProfile`/`Apply` comparisons, `DeliveryFee`, a direct display preview, and actual isolated `PartCart.Quote` + `PartCart.Checkout`. Exact cash, price/fee, shelf/hold/express counts, paid sale payload, moved physical ID, purchased definition/identity, receipt fields, input/row/RNG differences, and generated identity event summaries are recorded. For both previews, the moved part’s full typed record is compared with its original facts while permitting only the explicit `storageId` destination update; null/empty fields stay strict. The display preview is pinned to its current behavior: large cash sentinel, sale row skipped, and only non-sale rows applied. The separate direct `Apply` parity control starts with the fixture’s real cash, skips sale rows, and has no preview sentinel, so it is expected to reject the unaided gross purchase with the exact `InvalidOperationException: Insufficient cash.` Checkout is pinned separately to its authoritative sale-first credit and ordered non-sale Apply route. Quote or Checkout rejection records the exact stage and exception and fails the supported fixture assertion; no JSON warm-up is used to conceal it.

### Occupied-slot displacement chain

The chain selects an actual installed non-block engine-slot part and a distinct compatible authored replacement purchased through `PartOperations.Buy`. It runs Move replacement → Move the exact displaced physical ID to Storage, and the supported reverse order. It records exact part field diffs after each operation, owner/slot/location/storage facts, untouched condition, car and engine fluids, tune changes, `MechanicsOwnership.Resolve`, and `InspectionBuild.Capture`. Both orders also run through production and frozen `Total`/`PreviewProfile`/`Apply` comparisons. No replacement, owner, or component is fabricated.

## Interpretation

Rows and sale order are intentionally characterized separately: checkout captures sales for sale-first credit, then preserves the relative order of all non-sale rows. `PreviewProfile` currently skips sale mutation and uses sentinel cash; its output is not checkout truth. The two physical-move orders are expected to be supported; any exact rejection remains a strict failure, not parity by “both throw.”

This slice does not complete the broader stock, block, malformed, identity, stale-callback, native frame, or Step 1 matrix. It does not approve sale preview behavior or remove runtime JSON/profile cloning. Parent owns the source freeze, Unity execution, result integration, and Git checkpoint.
