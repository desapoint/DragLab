# New shop products and market refresh rules

2026-10-13. User-requested shop correction following the committed all-mode AWD checkpoint. No later interface phase advances.

## Duplicate new offers

The catalogue authors separate stable definitions for each car/trim. Shared engine/platform parts can have identical specifications and the same fictional supplier/product name, so listing every definition produced repeated new cards.

The current catalogue has **2,199 definitions → 1,770 distinct new products**, removing **429** repeated cards. The native Build shop now groups new offers by the complete serialized PartDefinition except its ID. Fitment, every mechanical/thermal/wear/tuning field, price, reputation requirement, quality, brand and description must agree. Matching four headline facts or a name is insufficient. All original definitions/IDs remain available to saves, installed hardware, inventories, carts and setups. The card prefers the planned/fitted ID, then the current vehicle's definition, then a deterministic ID order. Owned status includes every equivalent alias; searching an alias ID still finds the grouped card. Exact-ID authoritative purchases remain in use.

Only new shop cards are grouped. Separate used assets retain their identities, histories/conditions and individual buyback prices, including equal-condition duplicates. Installed/inventory views continue showing actual owned assets. The historical F8 legacy listing is retained as a reference; this change targets the native shop shown by the user. The UI theme, filters, member selection, explicit cart confirmation and cache authority remain unchanged.

## Current market lifecycle (verified implementation, no policy change)

- Vehicle seed: VehicleListings.Ensure initializes a missing market with Guid.NewGuid().GetHashCode(), replacing zero with one. Normal new-game/reset setup calls Ensure before the initial save. It is generated once per market, persisted with listings/counters, and retained on load. A legacy save gaining a missing market gets its own seed; corrupt modern seed data is rejected rather than silently rerolled.
- Starter stock: three fixed, fully disclosed used choices with fixed condition/mileage rolls (101, 102 and 103), independent of the random market seed for the mechanical offer contents. Buying the first car immediately opens normal stock.
- Normal vehicle stock: all thirteen new car/trim choices plus seven seeded used offers. Generation uses the saved base seed XOR ((generation + 1) * 2654435761), then a local xorshift sequence. The base seed is not regenerated at refresh.
- Refresh: after each valid, completed non-practice race, the completed-run counter advances. Every third such race increments generation and regenerates vehicle stock. Career/free wins and losses qualify; practice, interrupted runs, disqualification and incomplete/mechanical DNFs do not. Opening a menu, elapsed real time and loading a save do not refresh it.
- Buying stock: a purchased used-car listing disappears until a later market generation; new-car stock is replenished immediately. Buying a starter is the special immediate-generation transition. Inspection does not reroll offers.
- Used parts: there is currently no procedural stock generation, market seed, timer or race refresh. Selling loose sets/individual units transfers the actual retained asset into the dealer inventory; buying it back removes that exact asset. Stock persists in the save. A recurring seeded used-parts market would be an additional feature, not something this checkpoint claims exists.
- New parts: catalogue offers have no stock refresh seed. The shop groups equivalent new definitions; prices/availability remain catalogue- and reputation-based.

## Verification and review

Passed: **403** shop product/fitment/cache/individual-member/plan/identity checks across thirteen cars; **106** vehicle-market lifecycle/pricing/save/reload checks; packaged native Build/shop/purchase/cart **859/0 runtime errors**; Windows `BuildPlayerOnly`. Counts overlap. Catalogue grouping reduces 2,199 definitions to 1,770 new products before used stock and section filters.

The native suite refreshed 41 review PNGs through the existing screenshot exporter: normal 1280×720 images at `reports/interface-screenshots`, alternate resolutions at `reports/interface-screenshots/resolution-tests`. Fixture/log: `reports/player-fixtures/shop-products-20261013-01`. The focused native run covers current controls, fitment/metric filters, exact individual tire/rim/piston/valve purchase, cart checkout/edit/cancellation, no idle churn, and compact/1080p/4K layouts. No unrelated full-menu suite rerun is claimed. User visual acceptance remains pending.

Initial diagnostic test stopped because adding 1 to a 100-million float wear budget did not change its representable value. The fixture now uses a representable relative delta; all final 403 checks and the packaged rerun passed. Production behavior did not require a workaround.

Review Build → Shop → Engine with the Head/intake filter: equivalent new offers should appear once. Different specifications/fitment remain available. Check an installed matched-set individual replacement, a staged plan and a used buyback. Existing shared controls and resolution behavior remain unchanged; user visual acceptance is separate from automated completion.

Sources: Scripts/Runtime/NativePartBrowserModel.cs; Domain/VehicleListings.cs; Domain/CareerProgression.cs; Domain/SharedInventory.cs; Domain/PaidEconomy.cs; Domain/PartCart.cs; Infrastructure/ProfileStore.cs. Fresh generated fixtures/logs/screenshots remain local and ignored. Historical reports stay intact.
