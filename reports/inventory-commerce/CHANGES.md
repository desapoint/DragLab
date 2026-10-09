# Shared inventory and component-set cart — 2026-10-09

Completes the approved post-3H inventory/cart refinement. No later planned slice is started. [Contract and controls](../../docs/INVENTORY_COMMERCE.md).

## Delivered behavior

- Shared Storage/Car/Engine/Seller/Cart container contracts and item views over authoritative records, with Garage/Storage shelves, fitted slot/engine trees, virtual catalog offers and retained dealer component stock. Inventory moves and car/engine installation reuse the existing ownership/reservation operations. Same-model destinations include instance suffixes.
- Complete sets are the default and show the actual unit count: cylinders, valves, ignition topology or four tires/corners. Optional modeled engine units and individual tires can be bought loose without selecting a fitted position. Target selection happens only for installation; unselected extras remain loose. Full kits change specification; singular replacement/reuse retains the existing matched-definition/role restriction.
- Loose sets, individual spares and stored engines show actionable inventory entries. Stage storage/car/stored-engine moves or component sales in the same cart. Existing immediate install/service, donor, assembly and captured buyer-quote controls remain available. Whole car/chassis/engine salvage and non-additive valuation remain their existing quote-backed markets; used vehicle rotations are not introduced.
- Buy new sets/units, buy back real used stock, sell loose sets/units and move items in one captured net-price transaction. Sales can fund purchases with zero starting cash. One recoverable save publishes cash, ownership and one receipt; failed writes/operations publish nothing. Component/element/tire IDs, wear, faults, histories, missing slots, fluid resources and reservations remain intact. Replaced originals remain owned. Individual tire replacements retain the other corners' temperature/life.
- `InventoryPricing` is the shared new/used/valuation boundary. Existing used-quality/condition/history/buyer/assembly calculations remain authoritative. Dealer resale markup and handling fee are authored in the valuation policy (35% and $1 initially). Cart rows show charges/credits and the net cash change; buyer detail explanations remain accessible through valuation.
- Cumulative previews apply the actual operations on an isolated profile copy and inspect it using the same saved-car calculation. No preview, role/quantity selection, position toggle or staged move saves or spends. Cached offer templates, cart totals and sale prices avoid recalculation per repaint; background driving estimates, no-wear references and Apply/Revert remain.

## Corrections and save compatibility

The first domain run exposed Unity JSON recreating empty instances for nullable serialized record fields. Explicit dealer part/unit and engine/tire unit discriminators now clear only inactive representations after load. Reload checks preserve the active record and exact condition. Storage defaults are metadata for legacy loose items; normalization grants no hardware or condition renewal. Duplicate IDs across dealer/player/descendants are rejected. Fitted tire identity appears in disposal/retention quotes without changing tire forces or calibration. Save schema stays 5 and physical model stays v16; the added inventory/location/optional tire identity data is additive.

Two exploratory fixture errors were corrected: an unavailable rank-3 piston definition and an invalid zero-price dealership offer. Existing affordability, fitment and condition assertions were not weakened. Historical reports are preserved; current rerun copies belong to this folder. Raw saves, screenshots, audio/logs and full frame CSVs remain ignored local fixtures.

## Validation actually run

| Gate | Result |
|---|---|
| InventoryCommerceVerify | 407 checks, all eight models; set/topology counts, every modeled engine-unit role, individual tire replacement, preview/installation dyno parity, exclusive storage/dealer ownership, exact condition/reload, net trades, failed-save rollback, policy/replay/destination guards, donor and engine-tree moves |
| Existing CartVerify | 67 checks; cumulative installations, insufficient cash, stale/expired/replayed quotes, conflicting slots, rollback, no-wear isolation and legacy contact state |
| PaidVerify | 226 checks; actual paid resources/services/quotes/salvage/recovery and backup/failed-write integrity |
| AssemblyVerify | 142 assembly checks plus 37 prior ownership/inventory checks (179 combined) |
| MenuPreviewVerify | 6 checks; prepared background probes, dyno parity, owned-state isolation and audio migration/gesture persistence |
| Final Windows inventory commerce | 12 named checks plus mouse visibility/wait assertions; default full kit, no preview writes, targeted unit purchase/reuse, storage move, sale, exact dealer buyback and disk reload; 0 runtime/GUI errors |
| Final Windows shop regression | 16 named checks plus helpers; multi-category quantities, cumulative dyno, retained inventory, no-wear graph, Apply-preview/gesture saves and independent opponent audio; 0 runtime/GUI errors |
| Final Windows build | BuildPlayerOnly passed; screenshots of complete-set selection, cumulative comparison and storage originals inspected |

These gates overlap and are not summed as unique scenarios. Full historical Slice 2/3 foundation, physical configuration matrix, Android/controller and human balancing/listening gates were not rerun. The inspected final native runs are `inventory-commerce-native-final` and `inventory-commerce-shop-final`; exploratory first runs also passed. No race audio/force model is changed by this increment.

After skipping the first ten frames in each recorded phase, final cart-window frame median/p95/max was 13.0/17.0/145.7 ms and GUI p95 was 14.0 ms. Final tuning-slider preview max was 24.3 ms with no frames over 33.3 ms in that window. Full confirmations/transitions still pause briefly; neither the cache nor the background probe makes transactions allocation-free. `pacing-summary.csv` records both final native runs with nearest-rank p95 values.

## Current scope limits

Cart is session-only and scoped to the selected owned car. Dealer stock retains sold loose component sets/units, not a rotating used-car dealership. Individually modeled engine components and tires support single-unit replacement; wheel/brake hardware definitions remain complete corner sets rather than permitting unsupported mixed corner specifications. New unit prices retain rounded per-set allocation, with human economic review still required. This checkpoint does not alter the shift-light HUD.
