# Native Build browser — 4F/G/H browsing checkpoint (2026-10-12)

The user explicitly requested the shared Build categories, subcategory/change filters, sorting and current/planned compatibility together. This is one authorized browsing checkpoint spanning the foundations of 4F, 4G and 4H; those full phases are not complete or user accepted. Stop for interface review after this checkpoint.

## Delivered

- Build: Installed / Inventory / Shop, with Engine, Induction, Drivetrain, Wheels & brakes and Body & chassis. Exactly two rounded, red-accent part cards per row reuse the native surfaces, palette, type, scrolling, hover shine and pressed states. No focus highlight or Tab traversal.
- Search, multi-select subcategories, owned/affordable/unaffordable, new/used shop stock and adjustable/fixed hardware. Filter categories intersect; selected subcategories combine with OR. Default fit filter is Compatible; All exposes foreign-fit and unavailable offers. Independent query state is retained per source/group through details, F8 and other menus in the UI session.
- HP, torque, physical weight, part-condition percentage-point, applicable torque/RPM strength and radiator heat-rejection change directions and minimum absolute magnitudes. Price and each change metric support ascending/descending sort with stable identity ties. Torque follows Settings, using canonical Nm internally. Unavailable/inapplicable measurements stay unavailable and sort last; they do not match zero/unchanged filters.
- Compatibility and performance use Current build or the shared cart's Planned build. Pending installs, removals and moves affect the copied reference. Invalid plans show their error instead of silently reverting to Current. Sales do not change the fitted build.
- **Compatibility means physical fit**, including real cam/follower mating requirements. Weak components, worn/failed hardware, spring travel, valve float, clearance, knock, cooling, wheel/clutch-slip and other operating risks do not exclude a fitting part from Compatible. They remain separate warnings. A missing engine prevents a performance estimate, not fit classification for the chassis. A loose unit also remains Compatible when it physically fits but needs a matching installed set before the existing single-unit replacement operation can be used. An invalid planned reference cannot claim compatibility.
- Inline part details reuse Current/Planned → With part → Current no wear dyno, pointer inspection, summary delta bars, collapsible complete stats and info dialogs. No road probe is added. Individual retained/used units have an explicit target-position selector; preview-only planned identities cannot be installed into the current car.
- Existing full-set purchases, retained seller stock, exact owned moves/removals/sales and cumulative cart review bridge the same authoritative transaction classes and F8-shared cart. Full-set purchase remains the default. Browsing and staging do not save or modify fitted assets; Checkout captures an immutable quote and publishes once through the existing durable transaction. Failed saves retain assets, money and cart.
- Different-block native installation remains disabled pending the previously deferred reuse/manual/OEM preparation dialog. Buying a loose block is possible; this checkpoint does not implement that wizard or claim a safe complete block conversion.

## Corrections found during verification

The initial grid repeatedly queried and reordered hundreds of controls after each candidate result. An 869-offer Engine group could fail the 45-second loading gate. Foreign fits are now classified cheaply before curve work, results use bounded main-thread batches (up to eight candidates or a three-millisecond budget between candidates), and grid order is cached with direct card lookup. Sorting/filtering use the cached model, without recapturing builds. The budget is not a preemption guarantee for a single candidate. Category tabs also now retain their full control height rather than shrinking under the grid. The individual-unit matrix caught Unity JSON reconstruction of a null tire field on a piston; copied unit previews now use the existing SharedInventory normalization before installation. This is applied only to the isolated preview, preserving the actual spare and fitted elements. Final review also corrected the grid card refresh after choosing a different cylinder; a real mouse check now verifies its condition delta after returning from details.

## Checks

- Windows `BuildPlayerOnly`: passed with the final runtime sources.
- Focused `Test-NativeMenu.ps1 -FixtureName native-build-browser-04 -PartsOnly`: **157 checks, zero runtime errors**, including all thirteen vehicles across five systems; current/planned damage warnings; physical loose-unit fit versus install availability; two chosen-cylinder condition deltas; absent-engine fit; unavailable measurements; real mouse filter/detail/sort/cart actions; F8; durable-save failure/retry; retained ownership; compact/1080p/4K rendering and no-Tab policy. [Focused evidence](native-part-verification.txt).
- `Invoke-Unity.ps1 CartVerify`: **102 checks passed**, using all thirteen cars and existing mixed cart/quote/save/ownership regressions. [Domain evidence](../../shop-inventory/cart-verification.txt).
- `Test-NativeMenu.ps1 -FixtureName native-build-browser-regression`: **1,133 checks, zero runtime errors** across existing Tuning, Garage/dealership, comparison/stats/dyno, maintenance, Settings, reset and F8 workflows. This ran before the final isolated unit-card refresh correction; the focused Build gate is rerun on that final correction. Raw full evidence is local at `reports/player-fixtures/native-build-browser-regression/verification.txt`.
- `git diff --check`, UTF-8 reads and matching script metadata: passed. Unity-generated font asset changes were verified as whitespace-only and restored; no substantive font changes were discarded.

The final focused run includes the fresh-starter purchase path and real mouse selection of the second piston, verifying both detail and grid feedback without installation. Gates overlap and are not summed. Raw screenshots, isolated saves and player/editor logs remain local ignored artifacts.

## Review checklist

- Check the five Build groups, two-column density and concise card facts at the current window size and 1080p.
- Try Compatible with a risky cam or weak/worn component; it remains listed with warnings. Switch between Current and Planned after staging supporting hardware.
- Combine subcategories, HP/torque gain/loss, minimum magnitude, affordability and adjustable/fixed filters; return from details and compare with F8.
- Inspect Current/With part/No wear curves, expand stats and use the information controls. Check units and individual replacement positions.
- Stage a set, sale or removal, review the cumulative cart and explicitly confirm checkout. Cancel leaves the car untouched.

Remaining controls are separately reviewed: complete service/rebuild tree migration, storage/donor destination editing, native new individual-unit purchase/quantity editing, saved configuration library, assemblies and the deferred block wizard. Existing development interfaces remain reachable through F8. No human visual/gameplay acceptance is inferred from passing checks.
