# S06d2b typed cart preview characterization matrix

Status: characterization design only, paired with the [source closure inventory](S06D2B_CART_READ_WRITE_CLOSURE.md) and the reviewed [preview contract](S06D2B_TYPED_CART_PREVIEW_CONTRACT.md). This document specifies oracle cases and evidence to collect. No oracle run, Editor run, Unity run, code change, performance measurement, implementation approval or S07 work is claimed.

## Oracle setup and comparison rule

Freeze the exact source revision used for the first matrix run. Before changing any preview source later, preserve a test-only frozen reference of the current `PartCart.Total` / `PreviewProfile` path. The reference must retain the original LINQ/JSON/`Ensure` order. Record its source revision and source checksums in the eventual report. The existing `InventoryCommerceVerification`, `CartVerification`, `CartReservationVerification`, `PartsMarketVerification`, `AxleWheelVerification` and `MechanicsOwnership` / PartsMarket checks provide fixture precedents; their passing status does not substitute for this new projection parity run.

Build one authoritative normalized baseline per case, then create isolated oracle and candidate input states from that same baseline. Test harness deep copies may use the existing JSON facility solely to isolate the frozen oracle; that does not grant a JSON copy to preview implementation. Preserve `storageCommit` and all fixture IDs. Do not call `MechanicsOwnership.Ensure` after a deliberately malformed fixture is made. Run the unchanged legacy call graph (`PartCart.Total` then the exact caller's `PartCart.PreviewProfile`/inspection) and the typed operation against logically identical source facts.

Use the same explicit `now` value for every `Total`, quote, policy and price calculation. Use `PlayerProfile.New(fixedTicks)`, `MechanicsTransactions.Catalog()`, and `VehicleListings.Initialize(profile, catalog, explicitSeed)` as the base factories, then perform one initial `MechanicsOwnership.Ensure` and assert full global validation succeeds. Set cash, reputation, market stock and reservations deliberately after baseline creation. Use `PartsMarket.AddUsedSupply` with an explicit local `uint` seed and stable supplied identity prefix for used part/unit cases. Add a second owned car with the existing `VehicleCollection.Buy` path and create a detached engine with the authoritative assembly operations. Keep authored vehicle/part definitions sourced from the real loaded catalogue; do not hand-build approximate topology definitions.

Market generation may use its own explicit seed; do not rely on editor wall-clock time or `RuntimeEntropy.MarketSeed`. Pass fixed `now` to all production methods that accept it. `RuntimeEntropy` is deterministic only in the supported Windows Development capture process with the required command-line flags; ordinary Editor checks use fresh GUIDs. Therefore exact equality applies to every pre-existing physical ID and descendant ID. For any hypothetical newly purchased parts/units, compare by a typed, bijective correspondence keyed by `(cart row index, action, produced record kind, quantity index, member role, member ordinal/position)`. Each expected new record maps to exactly one actual record, every actual new record maps back, and no mapping may point at an existing ID. Compare all nonidentity fields and descendant relationships exactly after applying this mapping. Do not compare arbitrary newly generated GUID strings and do not consume gameplay RNG to make a preview output appear deterministic.

## Typed probes and result record

For each row, retain an immutable test input of all source fields in source order: action, definition and asset IDs, quantity, install flag, prepared-block flag/owner, role, destination kind/ID and ordered positions. Retain malformed nulls in dedicated error probes. Store the full pre-call owner state and a separate captured result after execution.

Record these output groups:

| Output group | Exact comparison |
| --- | --- |
| Acceptance and errors | Success/failure; exception full type and exact message; first failing row/phase. Do not compare stack trace or timing text. |
| Quote | Exact `PartCart.Total`, `DeliveryFee`, local/express units and fees by market product where applicable; exact sale amount by physical asset; policy and timestamp. |
| Physical graph | Existing identities remain byte-for-byte equal. New identity mapping is bijective by row/member key. Compare owner/location/slot/storage, bindings, position order, component count, duplicate behavior, and displaced original records. |
| Condition/history | Bitwise finite numeric fields and nulls for part aggregate, every member, tire corner, wheel override, fault/severity, health/failed reason, use history, temperatures, consumed life, distance/time, fluids, and service fields. No member is dropped or reset. |
| Derived mechanics | For each changed car/engine: exact resolution completeness/error/warning order, fitted definition map, applied tune/rank fields, fluids and fuel, inspection stats, dyno curve samples/peaks and warning payload. Compare via the same `MechanicsOwnership.Resolve`, `InspectionBuild` and `CarInspection` paths. |
| Stock/resources | Cash, reputation, new-market quantities/initial units/delivery %, active cart reservation contents and generation, used/dealer stock identities/paid prices, engines, parts, units, run state and receipts. Read-only preview must leave the actual source profile identical. |
| Isolation | Canonical serialized profile snapshot plus typed checks for source reference/list identity, original IDs, reservation/market values and catalogue values before/after `Total` and preview. Capture `UnityEngine.Random.state`; do not mutate profile or gameplay RNG while previewing. |

The frozen preview has nondeterministic generated IDs, so establish the identity map from its private result and the typed row/member path, not by rewriting either profile. The typed implementation may use only request-local virtual identity values. Verify those values remain private, stable for a row/member within the ticket, unique against existing IDs, and never become persistent IDs or influence RNG.

## Case matrix

All cases that claim parity start from a valid normalized profile and valid catalog. Error cases are independent so a first error does not mask a later row. Run row-order permutations where listed; matching totals alone are insufficient when order changes state.

### 1. Single-action baselines and exact physical identity

| Case | Fixture and rows | Assertions |
| --- | --- | --- |
| Empty/ineligible calls | No rows; 0 and 101 rows; 100 rows; null row source; missing/rebuild-reserved target car | Exact earlier-stage throw/result type, message and order. Valid bounds: 1–100 rows. |
| New set | One noninstall set, one install set, quantity >1 as spare; one install in a slot with a worn original | Price/fees and all generated member count; installed vs loose location; displaced physical ID and wear retained; source untouched. |
| New units | One loose unit; one selected individual position; quantity > positions leaving extras loose; multiple valid positions in supplied order | Role/count/price exact; selected identities map to exact target member positions; unused units stay loose; no other members/conditions change. |
| Buy used part/unit | Seeded unique used part and unit dealer rows with nonzero wear/fault/paid price; buy loose and install variants | Exact markup/handling price; same incoming physical identity/condition after purchase; exact dealer row removal; old slot member retained loose; unit role is still checked. |
| Sell part/unit | Owned loose worn/faulted part and loose engine/tire unit; sale rows one at a time | Exact wholesale amount under fixed policy/time; no installed item can be sold; current display quote succeeds/fails at the same global validation point. Separate checkout reference establishes actual dealer/cash transfer. |
| Storage moves | Part, loose unit, and stored engine moved between Garage and Storage containers | IDs, member condition, owner/storage, bindings and source/output containers exact; no purchase, sale or supply change. |
| Move/install part | Fitted part moved to Storage, another car, and a stored engine; loose part moved into a car with an occupied slot | Exact validation order; displaced item returns to storage; source/target bindings and tune ranks match authoritative operations. Preserve stored-engine `EngineElements.Ensure` behavior for valid and malformed records as separate cases. |

### 2. Ordered mixed carts and maximum supported size

Use separate valid ordered scenarios plus error permutations:

- One cart with sale → purchase → move; then the inverse valid order where supported. Confirm the order-sensitive `Total` reads and the checkout rule that sales are validated/credited first, followed by non-sale `Apply` in original row order. A sale row must not be treated as an `Apply` mutation.
- Multiple moves of different assets where the first move displaces a slot item later moved again; moves between two cars and a standalone engine; engine detach then attach; move a part into the newly stored engine; and destination/category collision pairs. Compare each source/destination graph after every logical row in a diagnostic oracle trace, while the product preview remains all-or-nothing.
- Mixed new sets, individual units, used purchases, sale rows and moves that share assets, definitions, slots and positions. Check duplicate offer/asset/position errors in the exact first-error order. Include two rows competing for a slot and a whole set plus a single-unit install of that same category.
- A legal 100-row basket using 100 distinct valid source offers/definition-role keys, without conflicting installs. Include a separate quantity-99 row and a row at quantity 100 that must fail. The fixture should use the live authored catalog to select eligible unique keys and assert it found the needed count; do not silently shrink the matrix if the catalog lacks enough keys.
- Check exact sequential local-stock depletion across multiple rows of one product. For each quantity, record each local/express split and per-item rounded fee; include absent stock, partly available complete sets, per-unit delivery, 5% and 10% delivery endpoints, and overflow/error behavior for checked arithmetic.

### 3. Repeated-member, tire/wheel, brake and block topologies

Build fixtures using the actual fitted authored specs and helper methods, not assumed member counts:

- Select a multi-cylinder engine; exercise a piston row, a multi-valve row and another member role. Use exact `InventoryUnits.Count`, `Roles` and `Positions` outcomes as topology oracle. Install one member, a subset, and every matching position. Keep aggregate/member health, fault, severity, role/cylinder/valve and untouched neighbors exact.
- Exercise brakes when the selected catalog car has modeled brake members. Record its actual fitted member/position count and all roles; do not invent four corners when the definition models a different topology.
- Exercise a 4-corner tire set with mixed health, heat, distance, consumed life, failure reason and different corner definitions if authored overrides permit. Install front/rear subsets and check displaced tires retain their IDs/history; incoming tires preserve physical IDs, copy correct condition to the chosen corners, and update only the same aggregate fields as the authoritative service.
- Exercise 4-corner wheels including `WheelFitments.HasOverrides`, mixed corner definitions, health/fault differences and wheel swap. Check every corner definition/ID and the complete aggregate/failure/service result.
- Cylinder block change on the current car and another car: missing `preparedBlock`, wrong owner, valid reuse/manual/OEM plan, missing required cylinder component, incompatible donor, complete compatible set, duplicate block/parts and multiple target blocks. Capture line order, `preparationOwner`, exact selected physical/replacement inputs, `PartCart.Total` first error, `PreviewProfile`/`Resolve` first non-drivable error, and all fluids/tune/build warnings.
- Include an engine with missing/invalid fluids or required slots as a deliberately invalid fixture; report legacy `Ensure` healing separately from a typed no-heal rejection (see section 6).

### 4. Multi-car, detached-engine and owner graph closure

Use a normalized profile with at least two different owned vehicle instances and one detached stored engine containing real descendants. Include:
- transfer of an installed engine between cars after detaching the donor; attach into a compatible empty target; incompatible mount; occupied target; same-engine/same-car early return; target/donor with active rebuild or garage job; unsettled run;
- transfer a chassis part between cars when target already has the category, including a non-engine and an engine-slot category;
- install/remove a stored engine part, moving parts between car, engine and storage; inspect both donor and target and all displaced parts;
- a unit positioned on the selected car whose line is evaluated after a prior move changes the relevant fitted pack; exact order semantics;
- target car not selected, selected-car change, a stored engine's tune/fluids and a source car with no attached engine.

Capture all affected owners and exact tune/fluid moves. Display-only labels/carousels are not input facts unless their actual caller consumes them. Conversely, any source owner or target record read by `SharedInventory.Move`, `ProjectRanks`, `InventoryUnits.Positions`, block resolution or inspection must be in the narrow typed closure. The global integrity validator remains an independent fresh dependency; it is not modeled by copying unrelated profile subgraphs into the preview.

### 5. Stock, reservations, sales and race-lifetime boundaries

Adapt the existing `CartReservationVerification` fixture, keeping an explicit seed and a fixed timestamp:

1. Set all market stock to zero, then supply exact quantities for multiple products with explicitly chosen delivery percent. Add one seeded used physical part and one used physical unit with `PartsMarket.AddUsedSupply`.
2. Build mixed rows for new set, new unit, used stock, and a sale. Capture the cart and compare its exact four rows, requirements and reserved supply without buying. Verify reserved shelf vs held quantities, local/express price, cost and fee match `Total`/`DeliveryFee`.
3. Compare a same-cart preview (may use own reservation) to an unrelated cart preview. Preserve current owner-reservation semantics, exact supply generations and used-stock hold identity.
4. Advance completed runs through the nine eligible races and each third-race refresh. Verify the cart/hold remains active through race 9 and refresh 3; verify the next completion expires it and removes only eligible stale used stock. Capture state before and after every boundary.
5. Test explicit clear, changed-cart capture, another car's cart, expired hold, stale-generation supply and duplicate/missing reservation rows. Distinguish quote/preview reads from checkout mutation. Preview must not change `profile.inventory.market.parts`, used stock, cash, cart lines/supply or run counters.

Use the existing semantic endpoints exactly: `CartReservations.LifetimeRaces` is 9; the ninth completion and its third refresh retain the hold, which expires only after the following completion. Do not paraphrase this as an eight-race lifetime or renew a stale hold during preview.

### 6. Malformed input, first-error order and Ensure divergence

Create one-fault-at-a-time cases. Capture full exception type/message and first validation stage for:

- target car absent/rebuild-locked, null/empty/>100 row source, null row, undefined action, quantities 0/100, duplicate offer/asset and row-order conflict;
- null positions, empty position list, too many positions, duplicate/reordered/wrong-role/wrong-spec positions, nonexistent destination, destination-kind mismatch and duplicate destination slot;
- missing or duplicate owned/dealer identities, null part condition/elements/member, absent unit element/tire, conflicting dealer payloads, stale discriminator, negative price, invalid/missing stock product and malformed market row;
- missing/invalid block prep owner/line, duplicate/partial build and first failing target in a multi-target resolve;
- sale with installed/missing/duplicate asset, invalid policy/time/buyer, malformed tire wear/history and invalid-but-structurally-readable global state.

Preserve the observed layered order: target availability; row materialization and line count; ledger construction; each row's checks and lazy full validation at the first sale; post-loop set/unit conflict; block preparation; per-target `Resolve`. Within each original operation retain its internal check order. `ValidateForQuote` remains strict-first and only retries discriminator tolerance for dealer-labelled errors. Never use “both threw” as parity: record exact type/message and which check fired first.

For a current-schema valid profile that was already normalized by its initial fixture setup, snapshot every serialized field and exact ordered ID/member/resource arrays, call `MechanicsOwnership.Ensure` a second time, then compare before/after. A valid `Ensure` no-op claim requires no changes to serialized player state, nested component/tire/wheel IDs and condition, tune/fluids/fuel, reservations, market rows, selected identity or revision. Confirm nested helpers do not rewrite records. This trace is pending.

Make separate old/incomplete fixtures and record legacy mutations, not expected typed output: absent legacy collections, old save/schema versions, null fluids/fuel, missing service/driveline/equipment parts, incomplete component conditions, missing tire/wheel IDs/members, missing selected identity, inactive dealer discriminator, old/missing market metadata and expired reservation. Run frozen legacy preview on isolated copies and diff before/after to identify precisely what `Ensure` heals, creates, clears, fills, or expires. The approved typed no-heal path must reject with the authoritative validation/missing-data error and preserve the actual input untouched. Do not weaken the no-heal requirement to match a healed clone.

### 7. Profile/source immutability and stale-ticket probes

For every valid preview case, compare the original source before/after `Total`, each preview request step and final result. Include exact cash, revision, engine/parts/unit/dealer lists, nested member arrays, wheel/tire records, market quantities, cart reservation, `UnityEngine.Random.state`, run checkpoints and catalogue definitions. `Total`/preview may build local dictionaries and results; it may not publish live indexes, alter stock, normalize dealer wrappers, mutate conditions or keep references to source records across a yield.

For future bounded preview ticket checks, add typed same-revision A→B and A→B→A pairs at every yielded boundary: edit a member health/fault, wheel corner definition, tire history, engine tune/fluid, source/destination binding, market availability/percent, reservation supply/line order, cash/REP/policy, authored definition and selected owner. The next consumer must fresh-fence and cancel/retire the prior ticket; restoring A does not revive it. Keep these stale-read cases separate from step-1 semantic oracle completion; they do not justify carrying old values into a later step.

### 8. Actual native Move dialog and callback envelope

These cases exercise the exact `OpenInventoryMove` closure as well as the direct domain oracle. Keep actual callback order and candidate rows in the trace; no simulated callback may bypass the originating action fence.

| Case | Required trace / assertion |
| --- | --- |
| Open and initial destination | Existing Move for the same asset initializes only a still-present container; no existing move leaves destination unselected and Add disabled. Record actual filtered storage/car/detached-engine choices for a whole part and a loose unit. |
| Edit and row order | Seed earlier/later same-asset and unrelated rows. Capture Candidate removing all same-asset rows then appending its replacement last; compare downstream order-sensitive effects and exact first error. |
| Destination sequence | Actual storage → other car → detached engine callbacks each run `Current`, assign a destination and recompute. Preserve selected-car inspection/dyno/warnings separately from the affected target car. |
| Changed block | Same installed definition follows normal candidate flow; a changed block targeting the selected car opens preparation instead; another car rejects with the existing exact error. Capture preparation's editing row and originating guard. |
| Stale origin | At destination callback, update and final Add, change a same-revision consumed physical/price/cart input. The converted origin must reject before candidate publication or cart staging. Preserve native modal draft and source identities. Characterize origin-null legacy behavior separately. |
| Stale final revision/cart | Change revision, row order, quantities, positions, destinations or preparation flags after open. Actual final Add must reject the captured revision/signature and leave profile/cart unchanged. Trace that destination selection has only the optional-origin check, while Add also checks revision/signature. |
| Successful final Add | It reruns the candidate before `StageInventory`, closes only on success, and invalidates appropriate views. Record exact staged row and absence of profile/stock/resource changes until checkout. |
| Whole-flow encoder retirement | Count both legacy signature encodes in the baseline, plus Total/PreviewProfile encodes. Converted open/update/Add must use typed ordered-row equality and zero runtime encode/decode; no whole-profile copy or legacy fallback. |
| Actual clock | Record the default `RuntimeEntropy.UtcNow` used by Candidate; use existing deterministic capture clock where available or retain exact native timestamps. Compare direct fixed-now probes separately; do not conflate their pricing with different-time native sale quotes. |

All of these caller traces remain pending. The existing native stale Move confirmation check is relevant baseline evidence, not a substitute for the full ordered input/projection comparison.

## Method-level timing/allocation record

Use the same fixed fixture and action sequence for baseline and candidate, with a warmed Editor/runtime and an idle frame before the measured window. Store raw trace values in the root-owned report, not this design file.

Measure independent spans and counts for:
- `PartCart.Total`, split by no-sale/no-move, sale-price first-use, move first-use/additional move, mixed ledger and block-validation route;
- legacy profile JSON To/From, first `MechanicsOwnership.Ensure`, ordered `Apply`, second `Ensure`, and each target `MechanicsOwnership.Resolve`/inspection;
- `SharedInventory.Move` by item class; `InventoryUnits.Buy`/`Install`; `PartOperations.Buy`/`Replace`; market `Ledger` construction, `Take` and express calculation; valuation validation, each loose price lookup, and block checks;
- allocated managed bytes/`GC.Alloc` and collection events for each span and for the whole rendered frame, plus operation count, row count, physical member count and catalog size.

For public entry points, bracket the exact call with `Stopwatch`/Profiler. To attribute private legacy subspans without production changes, use a frozen test-only oracle routine that is an exact source copy of the current sequence and mark its copied stages; independently compare its final output with the production legacy call. Do not claim those diagnostic spans are a production path. The actual native trace must separately measure the live dialog/preparation frame from first request through final fully actionable dyno/warning publication. Count shifted work, cancellation, attach and layout; do not move an over-budget step off the recorded interval.

Keep the existing 50 ms indivisible frame limit. Report median, high percentile and maximum plus fixture sizes and GC counts. Source-level hypotheses, stopwatch-only method time, whole-frame allocation, and Editor-only measurements are not evidence of native frame success. All managed oracle, Unity/Editor, native-frame and full supported-input gates remain pending root execution.

## Completion boundary

Step 1 is complete only after the frozen oracle matrix runs against valid and malformed inputs, full typed output/state parity and the `Ensure` no-op/heal-divergence checks are reviewed, and source reads/writes are concrete enough for high/root to approve the next narrow design. This document is not that result. No planner code, caller activation, S07 transaction work, implementation or performance pass follows from writing this matrix.
