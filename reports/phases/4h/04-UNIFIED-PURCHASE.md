# Unified component purchase (2026-10-12)

User-approved purchase/replacement checkpoint. Consolidate the controls into one dialog while preserving the authoritative cart, real kit/member identities and existing matched-specification rule. This does not complete 4F/G/H or authorize the next cart-page checkpoint.

## Delivered

- Shop details have one Purchase action. Remove the duplicated page-level mode/role/quantity controls and separate Add & install action. Description, compatibility, dyno, full stats, price and original Back remain.
- The shared dialog separates Buy (complete set / individual, actual role and whole quantity) from Destination (Store / Install). New offers initially default to one complete set; confirmed session choices persist per car/offer. Set size, per-set/unit price and formatted total are explicit. Storage needs no cylinder/corner selection.
- Installation uses actual fitted identities, condition, specification and faults. Valvetrain members group by cylinder and selected intake/exhaust role; tires group by axle and exact corner. Matching row clicks switch to individual replacement; other specifications retain complete-set installation. Individual spares of another fitting specification can still be bought for storage.
- All / Failed / Worn / Clear target actual matching members. Worn means at least 50% wear used, explained by its tooltip. Selection grows quantity as necessary; lowering quantity prunes excess targets in actual position order. Quantities remain whole 1–99; non-finite/fractional entries are rejected.
- Buying count and replacement count are independent. A fixed review above confirmation shows total, exact small selections, extra stored units and retained originals. Large selections show count/role; every exact target remains selected in the list and is included in the summary tooltip. Long member lists scroll without hiding the review or confirmation.
- Used units and used sets use the same destination/review flow, with quantity locked to their one real seller identity, actual price and incoming condition. Used kits stay intact; this interface does not manufacture split seller inventory. Used single-unit replacement preserves its existing wear/identity. Owned spare reuse and existing cart position editing remain available.
- Cancel/X discards local edits, including destination/quantity/selection. Add to cart only stages; checkout uses the existing atomic net transaction. Unselected condition, removed assets, spare identities, fluids and player resources are preserved. Fixed purchase review is removed when closing/replacing dialogs. Shared native controls, no Tab/focus decoration, hover shine and mouse-down feedback remain.

No domain/save schema or simulation change. Existing retained kit containers supply physical members; purchases are quoted as sets or role units using shared InventoryPricing/PartCart. Different member specifications within a fitted kit remain unsupported; the complete matched-set restriction stays explicit. Different-block installation still awaits its approved preparation wizard.

## Checks

- Final Windows `Invoke-Unity.ps1 BuildPlayerOnly`: passed.
- Final packaged `Test-NativeMenu.ps1 -FixtureName native-unified-purchase-final -PartsOnly`: **370 checks, zero runtime errors**. Includes thirteen-car topology/fit/condition models; native browser/filter/detail/Back/F8 regression; durable checkout failure/retry; unified set/unit/destination/quantity interactions; exact originals/spares/used identities; dialog cleanup; compact/1080p/4K layouts. [Recorded trace](native-unified-purchase-verification.txt).
- Current-window set/partial/used dialogs and compact valve layout visually reviewed. Fixed review remains readable above actions; member content uses the shared scrollbar. Actual 3840×2160 render target and shared scale verified. Raw images/profiles/logs remain ignored in the final fixture folder.
- UTF-8, report links and whitespace checks passed. Unity font serialization was verified as whitespace-only and restored.

The first focused diagnostic passed 296/0 before the fixed review/used coverage. A subsequent wider diagnostic passed the delivered behavior but stopped on a test assumption that used checkout returned to Shop; the test now selects Shop explicitly. Only the final 370/0 is the accepted gate. Earlier full native 1,133/0 and cart-domain 102 remain historical baselines, not rerun; domain and pricing rules are unchanged. Gates overlap and are not summed. Existing unrelated Unity obsolete-API warnings and the monitor's D3D upload-buffer notice remain in raw logs; no runtime/USS errors were observed.

## Review

- Build → Shop → fitted pistons → Purchase: confirm one set, real set size and Store default.
- Choose Install, click individual positions, increase quantity for spares, and inspect the fixed price/replacement/storage summary. Try All, Failed, Worn and Clear; Cancel/X leaves the cart unchanged.
- Repeat with valves/springs/plugs/injectors and tires; verify actual role/cylinder/corner and wear. Compare matching individual installs with another specification's required complete set.
- Buy or install a used item: quantity stays one, its incoming condition and actual price remain explicit.
- Confirm Add to cart, then review/checkout explicitly; removed originals and unused purchases remain in inventory. Check F8, small-window scrolling and the original Back control.

Stop after this locally committed checkpoint for user interface review. Later cart-page, library/service/assembly controls and full phase acceptance remain separate.
