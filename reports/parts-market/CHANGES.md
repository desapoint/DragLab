# Seeded parts and engine supply — 2026-10-13

## Behavior

- Parts share the car dealership's saved seed and generation. A fresh game/migration creates the base seed from a new GUID once; the persisted base seed stays fixed as generation advances. Regular refresh is every third eligible completed non-practice race; the initial starter purchase begins the regular market too. Menus, previews, reloads and settings edits never refresh stock.
- One global random pool supplies used parts (0–24 base product selections) and complete OEM engines (0–6 offers, new/used mix). There is no per-model quota. The used pool can be empty with guarantees disabled; engines always can be absent.
- Market difficulty controls the fitting-choice floor for each new/used parts pool across the entire owned garage collectively: Easy 10, Normal 5, Hard 0, custom 0–20. Used guarantees count distinct fitting, unlocked products, independently of player sales. They can suit different owned cars and promise neither ideal performance nor affordability. A setting change applies at the next normal refresh; engines have no guarantee. Base randomness uses seed/epoch; guarantee selection also uses the owned platforms, reputation and saved rule at refresh.
- Separate saved-seed streams select products and then quantity/condition. Repeated new components stock one/two/three full matching sets at 50%/35%/15%; single-piece products favour quantity one. Used batches favour full sets (70%), with partial batches (20%) and singles (10%) also possible. Two-member sets use singles for the remaining 30%. Engines roll independently from fitting guarantees; new engine quantities favour one, while each used engine is unique.
- New equivalent catalogue IDs share physical-unit quantities. Stocked products initially cover the maximum whole-set requirement among their compatible platforms. Sets and individual purchases consume the same supply. Trade-ins keep their exact condition and identities until refresh, then expire from seller stock only.
- Missing new supply is available immediately through express delivery. The charge is 5–10% of the absent portion's base price, rounded up to whole currency. Cart rows/totals, saved builds, worn-part replacements, singular replacements, stored-engine preparation and oil/filter/rebuild purchases use the same supply rules. Quoting and previewing consume nothing.
- Build → Engines → Engine shop buys complete stock-config engines into storage. Used engines retain their exact descendants and wear/fluids; new engines receive new identities. Existing physical mount rules still govern swaps. Foreign-platform spare parts can be bought into storage for later builds.
- Thirty-nine long-service replacement definitions add rings, bearings and valve seals across the 13 models: fixed OEM performance ratings and 35% higher wear-energy capacity at a 20% base-price premium.
- Profile schema advances to 8. Older profiles gain stock once while retaining assets, condition, car-market rolls and trade-ins. Missing market state in a current save is rejected for backup recovery rather than replenished. New ignition purchases use the actual engine topology, including stored engines.

## Deferred interface refinement

The requested separate **Replace broken** and **Replace under X% remaining condition** actions remain planned, not implemented in this checkpoint. The latter defaults to **15%**, with a shared slider/precise input, exact member/corner selection and aggregate stock/delivery/cash review before confirmation. Existing maintenance controls remain available.

## Validation

- Final seeded market matrix: **408 / 0**, including cross-order seed reproducibility, 13 engine platforms, quantity distributions, used singles, expiry, delivery, migration and rollback.
- Shop/cache/topology matrix: **975 / 0** across 13 models. Shelf quantities use one read-only topology projection per browser rebuild, checked against actual incoming-part resolution; full build comparisons remain authoritative.
- Full assembly/setup gate: **251 / 0**, including nested ownership/inventory checks. Reset/recovery: **17 / 0**.
- Integration rerun before the final independent quantity/seed refinements: car market **106**, cart **102**, inventory commerce **679**, paid operations **351**, saved-setup loading **468**. These gates verify the shared supply/price transactions; later quantity/ordering changes are covered by the final market and packaged runs.
- Windows player builds successfully. Packaged shop/purchase checks reached **1,372 passes / zero runtime errors** before an old assertion counted the delivery note as an item; final focused cart **287/0**, engine shop/workshop **496/0**, and full native menus/Settings/maintenance/dealership **1,543/0** pass. Counts overlap and are not summed.
- The audit corrected two outdated test assumptions (gifted reset car and base-only prices), actual incoming ignition topology, and checkpoint handling of null/empty inherited member specifications. Real member specification changes remain rejected.
- Existing historical phase reports are preserved; this folder records the current rerun evidence. No full driving/physics stress sweep is claimed: driving behavior is unchanged by the market.

One isolated final-matrix quote plus atomic candidate transaction averaged **562.7 ms**, with a 1,241,317-character profile and a successful in-memory persistence callback. This excludes disk IO and is not a menu-frame or hardware-independent latency guarantee. Whole-profile transactions remain a cost on definitive actions; previews do not save.

## Interface review

Technical checks are complete; user visual acceptance is pending. Review Build → Shop quantities and In stock / Express filters, Build → Engines → Engine shop, and Settings → Difficulty → Market difficulty / Minimum fitting offers. Confirm Settings stays a draft and current stock changes only on the next normal refresh. Review cancellation and the delivery-inclusive cart footer before buying.

Fresh normal-resolution captures are in `reports/interface-screenshots`; alternate sizes are in `resolution-tests`. The engine offers in the workshop verification images are explicit test fixtures; production stock remains random and may contain no engines. This requested market increment does not complete or advance another Slice 4 page. The separately requested broken/15%-threshold maintenance controls are the next authorized increment.
