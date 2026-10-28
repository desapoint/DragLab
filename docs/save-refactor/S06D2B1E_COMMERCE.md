# S06d2b1e commerce characterization

**Status:** Editor-only diagnostic source added; no Unity compilation or execution is claimed. The parent owns shared checker wiring, frozen runs, and final evidence review. The new file does not change production commerce, reservation, UI, persistence, or runtime behavior.

## Focused scopes

`RunCommerceContracts` provides `commerce` for all three families and focused `commerce-mixed`, `commerce-owner`, and `commerce-lifetime` scopes.

- **Mixed basket:** Starts from a fixed-seed normalized authored profile. It uses actual compatible catalog offers, a real loose part bought through `PartOperations.Buy`, and actual used dealer part/unit rows from the fixture supply helper. Two existing market product rows receive bounded local quantities and 5%/10% delivery rates, then pass `MechanicsOwnership.Validate`. Six rows cover NewSet, NewUnits, a second NewSet product, used part, used unit, and sale. `CartReservations.Capture` is checked for exact row count and current-generation local holds. Total and DeliveryFee are compared with the frozen oracle and independent per-item ceiling-price, used-price, and fixed-time sale arithmetic. No ID or catalog offer is fabricated.
- **Owner and changed cart:** Captures a real stock-backed cart, checks the same-car/cart match, then compares Total and DeliveryFee for the same authored offer/rows on a second actually purchased owned car that fits the definition. Manual expected values distinguish the owner’s held local supply from the unrelated car’s unheld local/express split. After three real race completions refresh the market generation, a changed same-car cart is captured and the old used hold’s cleanup is checked. Explicit Clear is checked afterward.
- **Lifetime:** Holds an actual seeded used part and advances `AfterRace` through completion 10. Before and after each read and race transition, transient full `SourceSnapshot`/catalog strings are hashed; sidecars retain hashes/lengths plus exact typed field-path and reference-identity diffs from the normalization walkers, not repeated full state dumps. Every boundary asserts run/generation, cash, unchanged cart, non-refresh market stability, and exact held-used payload retention through race 9/refresh 3; completion 10 asserts its removal. Refresh market/dealer differences are recorded in typed diffs rather than independently reconstructed. The final absence assertion concerns the held used asset only; it does not claim all other dealer state is unchanged by refresh. Clear also asserts zero remaining held units and unchanged cash/market stock.

If a required authored offer shape is unavailable, the group fails closed with an availability assertion; it does not synthesize an offer, ID, stock row, or topology. Existing strict JSON/source-invariance failures remain visible through the shared comparison helpers. This source-only checkpoint does not close all reservation malformed shapes, all product/corner types, checkout mutation, or any complete S06/typed-planner gate.

## Checks

Only static source review was performed: the new C# file is Editor-guarded, uses existing domain/fixture APIs, and adds its Unity `.meta`. Parent-owned compilation and runtime traces are pending. No test, Unity process, or Git command was run for this increment.
