# S06d2b1g bounded legacy cart cost attribution

This Editor-only characterization records descriptive costs of the current legacy cart/domain operations. It does not change the runtime cart, save path, checkout behavior, or the frozen parity oracle. It is not a performance acceptance gate and does not claim that the legacy route meets the future native-frame budget.

## Runner and sampling

`CartPreviewCharacterizationChecks.RunCostAttribution(check, reportFolder, subset)` is the runtime-assembly entry point. The Editor wrapper owns batch routing. Supported subsets are `totals`, `stages`, `operations`, and `block`; `block` runs only the prepared-block Total route. A null subset runs the first three groups.

Each operation records one separately labelled first observation, five warm-ups, and 31 measured calls. “Cold” here means the first timed route call before this harness's warm-up loop; some fixture builders necessarily call the same underlying APIs while preparing a real scenario, so it does not claim process-cold/JIT-cold timing. Every invocation receives a fresh literal profile copy and fresh copied rows or ledger, prepared before the stopwatch starts. The operation call is the only code inside the timed span. The harness consumes scalar/reference results, reads collection counts, and writes reports after stopping the clock. It retains no list of prepared copies; live benchmark state is bounded by the immutable scenario source, one prepared input, and the current result.

The fixed base seed is `20261029`; fixture families use documented offsets `+1` through `+12` and Commerce's established deterministic used-supply seed constants. All cart routes use `CartPreviewCharacterizationFixtures.FixedNowTicks`; the sale/move route uses the established `+1` tick used by the sale-and-checkout characterization. Default valuation policy and the authored mechanics/vehicle catalogue are used. Each cold sample records the actual car, cash, reputation, IDs, definitions, rows, and market generation outside the timed span. Physical IDs returned by actual setup purchases are recorded as observed fixture facts; the harness does not invent parts, stock, or IDs.

The CSV contains raw Stopwatch ticks, derived milliseconds, per-call Gen0/1/2 collection deltas, result type/scalar, and any exception. Summary uses median, nearest-rank p95 (sample rank 30 of 31), maximum, and collection totals/counts. `GC.GetAllocatedBytesForCurrentThread` is reported only if the same-run 8,192-byte known-allocation calibration observes a delta of at least 8,192 bytes. Otherwise allocation is `NA`, never zero. `ProfilerRecorder` remains a separate whole-frame native measure; this harness does not infer method allocation or callback-to-actionable frame cost from it.

The harness performs no extra profile/catalog/row snapshots, hashes, reflection, logging, fixture creation, assertions, or serialization work inside a measured callback. Serialization that the legacy operation itself performs is included in the full-method span, including `PreviewProfile` and the prepared-block `Total` route. The independent JSON stage timings are diagnostic and must not be summed as an additive decomposition.

## Measured routes

### `totals`

- `total.sale-first` and `total.move-first`: the same actual owned loose sale, actual purchased loose Move asset, and authored stocked additional set offer, with the sale/move ordering changed. The offer selector filters actual reputation/fit/stock and real sequential cash quotes.
- `total.additional-move-two-rows`: two actual installed non-block parts moved to the existing Storage container, in row order.
- `total.purchase-install`: one authored compatible, reputation-qualified, currently stocked non-block set installation.
- `total.mixed-ledger-six-rows`: the characterized six-row mixed basket shape using actual set/unit offers, actual dealer part/unit rows, sale, sequential real stock ledger, and current-generation owner reservation. The fixture uses the same controlled stock fields and order as the existing mixed-commerce characterization.
- `total.prepared-block-resolve`: a real authored replacement block and fitted required companion staged through Garage; `BlockPreparation.Build` in `Reuse` mode provides the actual preparation rows. The core row carries `preparedBlock=true`; owner-matched companion rows can validly have `preparedBlock=false`. The fixture log records each real row's definition, physical ID, owner-marker match, preparation marker, and destination before accepting the plan. `PartCart.Total` therefore exercises its prepared block preview and Resolve path. If the authored market/component graph cannot produce a ready, owner-scoped plan, the route is recorded as failed/open, not substituted with a synthetic plan. The `block` subset reruns only this route after source review.

The existing 100-row and quantity-99 boundaries remain correctness characterization; they are not duplicated as cost cases in this increment.

### `stages`

- Full routes: `preview.purchase` and `preview.move`, using actual compatible installed-purchase and two-row Move inputs.
- Independent stages: `preview.stage.ToJson`, `FromJson`, `Ensure1`, `Apply`, and `Ensure2`. These use one real two-row Move fixture and the actual Preview prefix:
  - ToJson returns the serialized string; no parse occurs in that span.
  - FromJson consumes JSON prepared on a throwaway literal fixture before timing.
  - Ensure1 receives the actual FromJson result with the shared authored catalogue restored, before cash-sentinel/row application.
  - Apply receives FromJson → Ensure1 → cash sentinel and reservation flag setup; only `PartCart.Apply` is timed.
  - Ensure2 receives the complete FromJson → Ensure1 → cash sentinel/flags → ordered Apply → reservation/flag-clear prefix; only the final Ensure is timed.

The stage inputs are prepared independently outside their respective clocks. Stage timings describe separate legacy costs; interactions, cache state, and allocations mean they do not constitute a mathematically additive breakdown of `PreviewProfile`.

### `operations`

- `Ensure.normalized`, `Apply.ordered`.
- `Move.sharedInventory.part`, `.unit`, and `.engine`, each using an actual owned physical ID and a valid destination. The engine case starts from an actual detached engine in Storage and reattaches it to its owner car.
- `Buy.part`, `Install.part`, `Replace.mechanics` using actual eligible authored offers and real physical parts.
- `Ledger.ctor` and `Ledger.Take`; Take gets a fresh profile and freshly constructed ledger outside its stopwatch.
- `Valuation.LoosePriceView.construct`, `.lookup`, and `Valuation.Capture` on an actual loose part.
- `Resolve.mechanics` and `InspectionBuild.Capture` on the actual fixture car.

Any missing authored offer, stock shape, compatible physical asset, or ready block plan is reported with a named `COST_OPEN` failure. Such a row remains incomplete evidence and must not be described as a successful or measured route.

## Output and limits

The isolated report folder contains `cost-raw.csv`, `cost-summary.txt`, and `cost-trace.txt`. These results describe the current source and fixed local authored data. Fixture construction and literal cloning are intentionally excluded from operation timing; the harness does not claim their cost is free. GC collection deltas are observations around the measured call and can include unrelated same-process collections. Native request-to-actionable frame timing and whole-frame GC remain separate evidence from `FramePacingProbe` and real UI interaction.

No Unity execution, player build, native callback run, or Git operation was performed by this author. Root owns source freeze, targeted Editor runs, result hashes, and commit; independent high review precedes those runs.
