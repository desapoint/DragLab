# S06d2b1l — ordered non-sale cart projection design

Status: initial proposal, blocked as written by [independent review](S06D2B1L_REVIEW.md). This is not implementation GO. [L0 decisions](S06D2B1L0_DECISIONS.md) and actual boundary results precede a corrected Apply-only contract. The proposed Total route below is deferred; it must not be implemented from this draft.

## Goal and boundary

L extends the K invocation-local ordered move scope into the non-sale `PartCart.Apply` families: `NewSet`, `NewUnits`, `BuyUsed`, and `Move`, in input order. It must represent purchase/displacement effects without JSON, `Ensure` on existing data, a whole-profile mirror, `MechanicsOwnership.NewId`, gameplay RNG, or activation from UI callers. This is a design contract; it does not claim implementation or test evidence.

Preserve two distinct legacy contracts. `PartCart.Apply` is an ordered mutation procedure: it skips sale rows, executes each other row and quantity in order, and debits the cash/market amounts performed by the underlying operation. `PartCart.Total` is an ordered validation/valuation pass: it builds a supply ledger, validates each source line, uses a separate lazy Move preview, and adds checked prices/fees; it does not apply purchases or sales to that move preview. Do not implement one by running the other or by treating checkout's sales-first commit rule as preview order.

L should expose two dormant typed routes backed by shared row facts and transition rules:

```csharp
OrderedCartApplyProjection.Evaluate(profile, carId, IReadOnlyList<CartProjectionRow> rows, catalog)
OrderedCartTotalProjection.Evaluate(profile, carId, IReadOnlyList<CartProjectionRow> rows,
                                    policy, now, catalog, validateBlocks, includeOwnerReservation)
```

`CartProjectionRow` is an owned immutable value record (all `PartCartLine` fields and ordered positions), copied before evaluation; null rows and exact indexes are retained. The APIs are separate because the legacy Apply route does not prevalidate like Total. Apply adds no quote/checkout line-count or uniqueness checks and skips sale rows exactly as its loop does. Total retains its target/rebuild check, materialization, 1–100 bound, ledger construction, row-by-row validation, post-loop conflicts and block validation in the observed first-error order; it calculates sale amounts only. A failed private evaluation exports exact type/message/index only and discards candidate graph effects. Success emits owned, path-attributed changes and per-row effects; no borrowed references escape.

The pure Apply evaluator receives an immutable context for the caller's initial cash and reservation-use flag. The normal Apply route uses those source values; PreviewProfile supplies `int.MaxValue/2` and the exact `CartReservations.Matches` result before applying. The legacy `Apply(..., preview)` parameter itself does not alter transitions. Both routes use the same ordered effects and neither edits source cash or `usingCartReservation`. Do not make Total simulate these writes: it has independent supply-ledger and Move-preview behavior.

Neither route publishes to `PlayerProfile`, commits, reserves/releases live stock, writes receipts, saves, or activates a caller. `PartCartQuote`, quote serialization/freshness, sale validation and atomic checkout remain subsequent work.

## Reuse and authoritative transitions

Reuse K's one `ProfileInstalledPartTransitionState` for all rows and its common Move dispatcher. Add scoped candidate operations for newly acquired assets and dealer removals; do not fork Move, fitment, ownership, tire/wheel, or rank rules in a planner adapter. Extract the actual mutation semantics into shared domain transitions called by both existing live entry points and the private projection:

- `PartOperations.Buy`: offer/fit/REP/availability checks, authored member creation for this intentional new purchase, market consumption, cash debit, part insertion, and optional `MechanicsOwnership.Replace`.
- `InventoryUnits.Buy`: eligibility, fitted spec/template/role/count, local/express price and fee, stock consumption, cash debit, and loose unit creation.
- `InventoryUnits.Install` and the existing shared wheel/tire/engine-element operations: resolve selected position at that row, install the physical unit, preserve or return the displaced physical member, update aggregates only through the current authoritative helper, and preserve exact validation order.
- `BuyUsed` application: resolve the first dealer row matching the ID, then perform the legacy payload-discriminator check; debit `InventoryPricing.BuyUsed`, remove that exact row, retain its physical payload and condition, and move it to Garage. A used part installs through the shared Move path; a used unit installs through `InventoryUnits.Install`. The caller must not reconstruct the payload from its definition.
- `PartsMarket.Ledger` / `Calculate`: share calculation and product matching with Total; the private path overlays remaining supply rather than mutating source stock.

Car, part, engine, loose-unit, bindings, and nested member state must remain source-reference keyed as in I/J/K. Candidate additions, removals and changed rows are tracked in first-write order by identity, not by identifier string. Required owned outputs include changed/deleted source part, engine, unit and dealer row identities; generated part/unit/member identities and path map; affected car/engine owners; cash delta; and per-row local/express units, fee and cost. Total output separately contains the legacy computed total, fee, split supply rows, and exact validation stage. These are typed owned values, not a serialized replacement profile.

Preserve operation-specific quantity semantics: Apply loops `quantity` times; only the first NewSet iteration receives `install`, while NewUnits installs each bought unit only when the corresponding supplied position exists. Total treats a used purchase as quantity exactly one, but standalone Apply retains its current repeated lookup/removal behavior if asked for a larger quantity. Total prices BuyUsed as checked unit price times quantity; new purchases go through the supply ledger once per requested quantity with the existing fee rounding. Do not front-load Apply validation by calling Total.

## Virtual identities for intentional new purchases

The legacy operations call `MechanicsOwnership.NewId` for purchased parts/units and may derive descendant member IDs from the part ID. A pure preview cannot call it: that would advance gameplay identity/RNG state, and exposing the resulting IDs would make temporary objects look persistent. Add a per-invocation virtual identity scope that allocates deterministic opaque string tokens from `(cart row index, quantity ordinal, member path)` without RNG or clock access. It checks collisions against all relevant source identities and prior tokens in that scope. The token namespace is never written to the source, storage, save, receipt, quote, or persistent transaction. Repeated acquisitions get distinct tokens. Outputs include `(row, quantity, member path) → virtual token` mappings so inspection can bind only within that evaluation. Existing `PartCartLine.assetId` cannot refer to an as-yet-uncreated random purchase; L must not invent that cart behavior. A later evaluation creates a new scope and no identity survives it.

Refactor new-purchase creation to accept a narrow internal identity allocator while keeping existing public buy methods on their current persistent allocator. Newly purchased content may initialize its own authored members exactly as `EngineElements.Ensure` / `TireDynamics.Ensure` do today. Existing damaged, missing or malformed source members are never repaired. For source-derived displaced members, preserve exact physical identity and condition; if a legacy path would synthesize an ID for a malformed old member, that exact branch is an error/no-heal divergence in the typed projection, not permission to allocate a replacement.

## Three individual-unit installation paths

Keep and test all three actual `InventoryUnits.Install` paths; dispatch on the authored incoming record and target topology rather than a generic slot approximation:

1. **Engine-element member:** non-wheel unit without a tire payload delegates to `EngineElementService.Replace`. It must target an actual fitted member, retain the incoming element's identity and subtype, and return the displaced member through the authoritative service path.
2. **Wheel:** a wheel-category unit delegates to `WheelFitments.Install`, preserving corner definitions/IDs and its existing destination cylinder metadata rules.
3. **Tire:** a unit with a real tire payload uses the tire replacement branch, preserving the incoming identity/history, returning the displaced corner as a loose physical tire, and syncing aggregate summaries exactly once. A newly bought tire unit's actual payload/path must be observed; do not fabricate a payload to force this branch.

New-unit quantities install only the selected positions in order; extras remain loose. Preserve exact caller-provided position order, duplicate checks in Total, and unselected member equality. BuyUsed unit role checks remain in Total; Apply itself retains its narrower existing checks.

## Cold product aliases, reservations, and row equality

The typed routes must not call `PartsMarket.Stock` on a cold source: it writes `inventory.market.index`. Nor may they call `CartReservations.SameCart`/`Lines`, which encode rows with `JsonUtility`. Add a nonmutating operation-local `PartsMarketProductView` that borrows catalog/market rows synchronously and reproduces exact `Stock` resolution:

- If `market.index` is already non-null, honor its current lookup behavior without modifying it, including its existing first/direct mapping.
- If cold, build an ephemeral index in the same row/definition order using the same `Dictionary.Add` behavior and failure point; do not publish it. On a direct miss, use the same `ProductKey` alias equivalence and first matching market row as the existing fallback.
- Track per-row remaining units locally. `Take`/`Quote` call the same shared `Calculate` implementation, including quantity-loop fee rounding, 5–10% delivery, and checked cost/fee arithmetic. Current-generation held units and owner-cart preview exceptions must use this same resolved stock row.

Replace the JSON-only row equality used by `CartReservations.SameCart` with one shared typed equality over the effective `PartCartLine.Copy` values, in exact field/position order and ordinal string comparison. Preserve null rows, Copy's null-`positions` to empty-list behavior, `Enum`/default field values, and existing line ordering. The live reservation API and typed projections must share this comparator so a cold typed path cannot warm any cart serialization cache or see a different “same cart” decision. Before replacing the old comparator, capture cases for null lines/positions, default-valued rows, all fields, reordered positions, and repeated identical lines; require exact results against the frozen JSON comparison.

Reservation reads remain read-only: derive active/expiry using the same run endpoints, identify the same cart from exact ordered lines, and calculate same-generation holds by resolved product row/alias. Do not implement `Capture`, `Clear`, hold renewal, stale-used release, or race advancement inside preview. Total must preserve its distinction between `CartReservations.Matches` and `includeOwnerReservation && profile.inventory.cart?.carId == carId`; this second additive-draft exception is not a substitute for `Matches`.

## Frozen reference closure and allowed ownership

Before extracting shared code, root captures and hashes an independent test-only closure at the current committed K source. At minimum freeze the exact bodies/order of `PartCart.Apply`, `PartCart.Total`, `PartCart.DeliveryFee`, and the pre-L preview route; `PartOperations.Buy`; `InventoryUnits.Buy`, `Install`, and `Positions`; `PartsMarket.Ledger`, `Calculate`, `Stock`, `ProductKey`, `Consume`; `CartReservations.Matches`, `SameCart`, `Held`; `InventoryPricing`; `MechanicsOwnership.Replace`; `EngineElementService.Replace`; `WheelFitments.Install`; and the tire replacement/sync helpers. Preserve the K frozen Move driver for every Move line. This must be independent of the shared helper after extraction and must retain exact runtime first-error/partial-mutation order.

Recommended implementation ownership: new `OrderedCartApplyProjection` and `OrderedCartTotalProjection` domain files; shared internal purchase/install transition file(s); `PartOperations.cs`, `InventoryUnits.cs`, `PartsMarket.cs`, `CartReservations.cs`, and only the specific authoritative install helpers needed to delegate existing live routes; the K state/dispatcher and snapshots for scoped records; focused Editor checks and an L-specific design/core/results note owned by the coordinator. Freeze all source before Unity/build runs. No UI, native harness, quote/checkout, global JSON cleanup or worker files belong in L.

## Required finite gates

- Row-order baselines: purchase then Move of a distinct pre-existing asset and Move then purchase; NewSet/NewUnits/BuyUsed mixed in multiple orders; used part/unit loose and install; quantity greater than one; occupied-slot displacement then a later Move of the displaced real part. Do not make a cart row reference a generated asset the user could not identify when entering the cart.
- Identity/topology: actual multi-member engine pack, actual wheel pack with overrides if authored, actual tire payload path if available, role/position mismatch, source and destination owners, installed and detached-engine destination, and exact displaced wear/IDs.
- Supply: cold and already-warm index; exact product and alias; absent, local-only, mixed local/express, exhausted, 5% and 10%; multiple rows depleting one alias row; owner hold versus unrelated hold; expired/stale generation; source market/index unchanged. Exercise exact quantity fee rounding and checked overflow/error order.
- Reservation equality: typed comparator versus frozen JSON for every serialized line field, null row/positions, ordering and defaults; no JSON counter increase in typed route.
- Failure atomicity and source invariants: each malformed row independent; source serialized values, object/list/reference identities, market index nullness/contents, cart, stock, cash, conditions, catalogue, Unity RNG and entropy unchanged; failure outputs contain no candidate graph.
- Apply and Total are compared separately to their own frozen entrypoints. Preserve the intentionally different cash/supply/mutation behavior; do not mark one as parity merely because the final inspection looks alike.

Performance evidence is Editor-only and bounded by measured candidate rows/quantities; don't infer native frame acceptance. A failed or unavailable authored topology remains OPEN with actual source counts. After independent review and a passing source manifest, differential, existing Parts/I/J/K regressions and Windows build, L can feed the later complete typed preview caller. L does not complete checkout, stale-ticket freshness, native activation, all S06 gates, or S07.
