# Responsiveness audit and synchronous fixes — 2026-10-14

The user reported multi-second dealership/shop entry and action stalls, requested an audit and background work, then explicitly asked to revert the new threading experiment and optimize the existing execution flow instead. **The new startup/menu worker code is reverted.** Unity lifecycle, asset access and menu calculations retain their established main-thread flow; existing opponent/benchmark workers are unchanged. The intermediate worker build exposed main-thread-only asset/debug API dependencies and is not a delivered checkpoint.

## Findings and changes

1. **Dealership previews generated unrelated markets.** `VehicleCollection.Preview` initialized a complete parts/engine supply for every temporary car. Three static previews took 3,534.64 ms. Read-only temporary profiles now contain a valid empty market and current save metadata; they still normalize and validate their actual car. Exact parts, hidden-condition disclosure and pricing remain authoritative. Final measurement: 3.61 ms for the same three previews.
2. **Catalogue access repeatedly scanned 2,238 definitions.** Indexed ID lookup now handles additions, removals and same-size authoring replacements without stale identities. Authoring construction remains linear rather than rebuilding an index after every insertion; override application explicitly rebuilds it. Slot fit checks use the identical direct rules instead of repeatedly walking the slot iterator. All 13 car/slot combinations are checked against the original iterator.
3. **Resolved simulation construction authored another catalogue.** These builds already carry their fitted definitions. Mechanical stress now consumes those captured definitions, including mixed wheel/tire corners. Raw authoring simulations retain the original catalogue path. The cross-car matrix compares every captured definition against the prior authored path; no physics calibration or replay identity changes.
4. **Repeated preview hashing copied irrelevant state.** Cached planned inspections now fingerprint the actual car, fitted condition, incoming cart assets/definitions and lines, rather than serialize the complete seller stock/history on every revisit. Owned-definition membership is indexed once per browser build. Exact cache invalidation and authoritative checkout remain intact; product distinctions are not approximated or collapsed.
5. **Market generation recalculated identical product keys.** One refresh-local identity table replaces repeated JSON conversion inside coverage/quantity loops, preserving the RNG streams and seeded result ordering.
6. **Saving reconstructed the catalogue.** A normalized transaction now saves against its actual bound catalogue; missing catalogues still initialize normally. Full validation, flushed writes, backup and atomic replacement are preserved. Opt-in timing breaks out encoding, validation and disk work.
7. **UI construction did avoidable work.** Filtering previously reordered existing cards even under unchanged name/price sorting. Matching controls now retain their relative order. Identical racing surfaces share a bounded cache of exact geometry keyed by dimensions, pixel scale, kind, palette and selection state. Hover/pressed/shine rendering bypasses that static cache. Gradients, corners, brush strokes and animation remain the same; no new texture/theme is introduced.

## CPU audit

Same machine and Editor workload; timings vary with seed-dependent initial stock, compilation warm-up and disk load. The comparison shop uses a fixed seed and the same stock workload. These measurements are computation timings, **not total application launch times**.

| Workload | Before ms | After ms |
|---|---:|---:|
| Three static dealership previews | 3,534.64 | 3.61 |
| Current profile normalization | 94.76 | 11.67 |
| Current profile validation | 92.70 | 10.35 |
| Transaction clone/validation, test persister | 338.21 | 78.49 |
| Cold shop model | 229.16 | 102.92 |
| Shop model revisit | 197.88 | 80.18 |
| First planned shop | 496.12 | 163.38 |
| Planned shop revisit | 239.81 | 90.24 |
| 10,000 catalogue lookups | 82.93 | 1.30 |
| Stock refresh, including used-car assessment | 1,593.01 | 837.38 |

[Before](timings-before.txt) / [after and save breakdown](timings-after.txt). This fixture has 1,800 new stock products, 14 used items and a roughly 1.3 MB compact profile. Actual player/test profiles can be larger.

## UI, storage and remaining costs

The full packaged shop/purchase/cart regression passes **1,416 checks / zero runtime errors**. Its diagnostic matrices deliberately create many garages synchronously; those test-only pauses must not be presented as ordinary gameplay frame times. A separate short real-input audit without that matrix passes **31/0**. It measures ordinary shop entry, filtering, detail, cart, tuning and Settings paths while preserving profile/save bytes.

In the final focused run, normal frame medians were about 14 ms and p95 values about 21–23 ms. The single sampled peaks were approximately **188 ms shop entry, 21 ms search, 78 ms part details, 213 ms cart, 145 ms tuning and 35 ms Settings**. Cached surface geometry reduced the earlier same-script shop/cart/tuning peaks of roughly 394/416/337 ms. These are short local samples, include renderer/input/test overhead, and do **not** establish hitch-free performance on every machine. [Frame summary](frame-summary.txt).

A durable save took 133.64 ms in the sampled run: normalization/validation 10.76 ms, checkpoint merge 0.53 ms, JSON/UTF-8 29.11 ms, write plus forced durable flush 83.54 ms, and backup/atomic publication 8.06 ms. Durable writes remain on definitive actions, not previews. Market refresh still simulates whether newly generated used cars run/drive; it remains the largest measured one-off CPU job. Neither cost is hidden by an unsafe task move or reduced safety checks. Larger catalogues would benefit from virtualized card controls and incremental stock preparation; these are remaining implementation options, not claimed delivered work.

A SQL/database migration is **not included**: the measured faults were redundant initialization, lookup, copying, formatting and geometry. Existing authored Unity assets plus part override data remain editable source content; the existing reset generates clean player state from those defaults while retaining driving/audio preferences. Introducing another persistence format would not remove repeated simulation work or the cost of a durable flush.

## Checks and review

- Cross-car optimization equivalence: **1,183/0**.
- Shop topology/product identity/cache matrix: **975/0**.
- Seeded market lifecycle: **408/0**.
- Ownership/recovery: **22 checks passed**.
- Stable fresh-game/dealership/maintenance input check: **52/0 runtime errors**.
- Full packaged shop/purchase/cart: **1,416/0**; focused performance audit **31/0**; Windows build succeeded.
- Hover/contour/shine follow-up passes **87/0 runtime errors**, recorded in its own evidence file. Historical reports remain preserved; this folder holds current evidence.

Review normal entry into Garage/Dealership and Build/Shop, search/clear, part details, cart preview and tuning/Revert. Inspect hover/pressed states and resizing. The settings orientation, progressive finish-line braking and nine-race cart reservations are the next explicitly requested corrections; this checkpoint does not silently complete them or advance another Slice 4 page.
