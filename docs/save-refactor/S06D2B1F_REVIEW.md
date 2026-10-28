# S06d2b1f independent review

**Status:** Static source preflight complete; runtime evidence pending parent-owned Unity checks. This review is diagnostic only. It does not approve Step 1, S06 completion, typed-preview parity, or production changes.

## Reviewed source

- `CartPreviewCharacterizationValidation.cs` SHA-256 `C9B51772FD19D37FA84F90467329DAA47FCFD5E9B3BD70E14AA4491557FEAE73`.
- `CartPreviewCharacterizationOrder.cs` SHA-256 `45392C374363F4FFF0866478F6F620DA829A8FDF679BFCFBC3562C2DE3B9C9B1`.
- Corresponding supplement reports and the increment decisions were read. No Unity, process, build, or Git command was run by this reviewer.

## Findings

No remaining static source blocker found after the reviewed corrections.

- Validation fixtures preserve actual authored state and physical IDs. Sale setup now selects a compatible multi-member offer whose quoted two-purchase cost fits the current cash; the wrong-destination unit case also checks its actual quote against remaining cash. The wrong-spec position is taken from a different real installed piston pack, and its definition must fit the target car. Absent-stock is produced by removing an actual compatible market row on a literal clone, clearing the market index, validating the resulting profile, and checking all-express cost. Malformed cases retain exact expected first stage/type/message; valid stale-discriminator and reservation controls remain distinct.
- The 100-offer × q99 check uses distinct real authored eligible definitions, actual stock/holds, and manually accumulated rounded local/express fees. It compares the exact `Int32` result/overflow boundary without synthesizing offers, prices, or supply.
- Order cases use real buy-created physical identities, an exact captured hold for each sale-row permutation, quoted setup costs and REP eligibility, and exact non-sale Apply order. Their sale-first checkout expectation follows `PartCart.Total` and `Checkout`: sales credit the cash before ordered non-sale Apply. `PreviewProfile` is separately asserted to retain the sale, skip its transfer/credit, use its cash sentinel, and apply the purchase/move rows.
- The occupied-slot chain pins each actual Move step, including the displaced part's original `storageId` after replacement and its Garage value only after the storage Move. The reverse sequence expects Garage as soon as the displaced item is moved first. Full physical condition and fluid expectations remain strict.
- JSON/source differences, unsupported actual fixture shapes, and any failed strict runtime checks remain visible; no finding here is a waiver or claimed runtime pass.

## Gate status

This is preflight only. Parent-owned compile/runtime runs, exact source-manifest checks, and independent result review remain required before an intermediate diagnostic checkpoint can advance. Native destination/action coverage, method/frame/GC attribution, the remaining Step 1 matrix, all caller conversions, and future typed-candidate/fresh-ticket/performance gates remain outstanding as scoped in the decision and contract documents.

## Runtime evidence: validation sales

Parent-run `validation-sales` completed on the frozen source with **63 PASS / 0 FAIL**. The compact trace is `reports/save-refactor/S06D2B1/supplemental-validation-sales-20261028-223919265/S06D2B1_LEGACY_TRACE_V4.txt`, SHA-256 `4e1ef928314e3263a0ef222f9194d7bf0056cfce8325be81cf5c93689b4be18f`.

The actual setup selected the authored `juniper-club/SparkPlugs/0` offer, count 4, and bought two actual parts for a quoted total of 78 (8 express units, 0 local) from fixture cash 100,000,000. All three first-error probes matched production and frozen oracle exactly: invalid policy → `Invalid dealer spread/handling fee.`; invalid time → `Invalid valuation request.`; missing wholesale buyer → `Unknown buyer.`. Source/oracle input graphs, row values, and Unity RNG remained unchanged in all three cases. This is scoped validation-sales evidence only; integrity, stock, positions, order, native, broader Step 1, and typed/freshness gates remain pending.

## Runtime evidence: validation integrity

Parent-run `validation-integrity` completed with **114 PASS / 0 FAIL**. Trace: `reports/save-refactor/S06D2B1/supplemental-validation-integrity-20261028-224110357/S06D2B1_LEGACY_TRACE_V4.txt`, SHA-256 `091c6ab2225f4a3861927ae082e442d6326efc1925fbd107bf4689bcb8a66b5a`.

The trace records the same actual sale fixture (`juniper-club/SparkPlugs/0`, two-purchase quote 78) and exact first errors for duplicate owned identity, null owned condition, null owned member, conflicting dealer payloads, missing dealer payload, negative dealer paid price, and duplicate dealer physical identity. The discriminator-only stale control reports strict validation error `Invalid dealer stock at row 75: wrong part/unit discriminator.`, while quote validation succeeds and `Total`/frozen oracle both return `-16`. No validation OPEN was present. Production/frozen input, row, and RNG guards passed throughout. This result closes only the scoped integrity group; stock, positions, order, and later gates are still pending.

## Runtime evidence: validation stock and reservations

Parent-run `validation-stock` completed with **134 PASS / 0 FAIL**. Trace: `reports/save-refactor/S06D2B1/supplemental-validation-stock-20261028-224409904/S06D2B1_LEGACY_TRACE_V4.txt`, SHA-256 `b5725c11a37325f30ab6bb056e305327e7ff16bc0e525e7eb4d872ff31a1d4f9`.

Exact first-error probes passed for a null market row (ledger-construction `NullReferenceException` with the captured runtime message), invalid stock units and delivery percentage, future-generation reservation, and a reservation exceeding current shelf supply. The actual absent-product control remained valid after removing a real row and quoted `aster-sport/FuelSystem/0` at 198 (= 180 base + 18 express fee, 10%). Duplicate zero-unit and missing reservation supply rows were accepted controls; an older-generation hold was valid and contributed zero current held units. The actual 100-distinct-offer × q99 basket returned the manually accumulated 50,842,591. The trace also records a conservative all-new-purchase upper bound of 56,628,000 from max authored price 5,200, below `Int32.MaxValue`; this does not cover used sales, custom policies, or later checkout arithmetic. No OPEN was present; source/oracle/row/RNG guards passed. This is scoped stock/reservation evidence only; positions, order, native and broader gates remain pending.

## Runtime evidence: validation positions and Move failures

The initial `validation-positions` run used a second owned car with no distinct piston offer fitting the target and emitted a failed wrong-spec fixture precondition. That fixture was corrected to buy an actual same-model car, install an affordable authored D2 piston pack distinct from target D1, validate the graph, and use one real D2 member ID. The corrected run `supplemental-validation-positions-20261028-225141445` is the current result: **58 PASS / 6 FAIL**, trace SHA-256 `ee9ad24310e06fed1c195cf86cc7da776fd224264c64fd140f416a9953d6a390`. It has no OPEN. The D1/D2 case passed with `starter-hatch/Pistons/0` versus `juniper-club/Pistons/0`; the actual donor member position produced the exact `Select unique fitted positions for installed units, matching the fitted specification. Unselected extras remain loose.` error.

The six failures are strict and remain red: for each of the two real Move-negative cases, production Total and frozen Total both return the exact expected first error (`Choose a fitted position to install an individual unit.`; `Conflicting slot installations.`), but each call mutates the input graph. Thus each case fails actual input immutability, frozen input immutability, and bridge parity. Their Unity RNG and row-value guards pass. Do not count exact matched error messages as a pass for the source/bridge requirements. This result closes only the wrong-spec fixture precondition; it does not close the Move mutation red or the wider positions/matrix gates.

## Runtime evidence: sale-order rerun and displacement chain

The full `order` run on source before the two expected-Apply-error pins produced **168 PASS / 30 FAIL**. Its two sale-order `Apply` parity failures were expectation-only: both actual and frozen `Apply` threw exactly `System.InvalidOperationException | Insufficient cash.` because direct `Apply` does not perform sale credit or set PreviewProfile's sentinel. The full run's other strict failures were retained; no source/input mutation assertion was relaxed.

After adding only those two exact expected-error arguments to the sale `ComparePreview` callsites, the focused parent-run `order-sales` rerun produced **114 PASS / 16 FAIL**. Trace: `reports/save-refactor/S06D2B1/supplemental-order-sales-20261028-230053285/S06D2B1_LEGACY_TRACE_V4.txt`, SHA-256 `6a802e43ef5bfa551fe3815a950ad415c8bdf64843a448eefe8512140c4a286a`. Both direct Apply negatives now pass exact exception and partial-graph parity. Both sale-permutation Preview/Quote/Checkout probes still show 756 typed field paths and 344 reference-topology paths changed on their isolated inputs. The 16 failures are strict Total/Preview/Quote input immutability findings; standalone Apply errors, manual sentinel preview output, both actual Quote/Checkout receipts, cash/stock/dealer payloads, row ordering, RNG, and sale/move field expectations pass. No ORDER_OPEN or CHECKOUT_OPEN was present.

The occupied-slot chain remains supported by the unchanged chain source and its full `order` run in `supplemental-order-20261028-225314741` (trace SHA-256 `2daa578b1290c7f11905aed43e02511f0cdf3202253a6e6092b192b5f674ae27`). Both Move row orders passed exact per-step old/new identity, owner/slot/location/storage, condition and fluid checks; the reverse order correctly records the temporary missing-piston Resolve state before reinstall and both orders finish Drivable with Resolve/InspectionBuild agreement. The chain run's strict Preview/Total source-mutation failures remain red; no chain output success is used to claim source immutability.
