# Cart page and editable component rows (2026-10-12)

User approved the next cart-page checkpoint and explicitly required piston and other repeated-component editing from the cart. Deliver the shared page frame and row-edit pattern for every existing purchase type; retain the old cumulative preview and checkout. Later full grouped comparison, donor transfer and engine-block preparation controls remain separately reviewed.

## Delivered

- Cart replaces Build content with a full page under the persistent native navigation. Use the original compact Back visual, shared metal/red panels and themed scrollbars. Back restores the previous browser/detail context; F8 retains the same shared cart and page state. Settings temporarily covers the cart and returns to it without bypassing the existing unsaved-preference guard.
- Scrollable rows display actual part name, set/unit/role, quantity, destination, each price and line total. Individual installations identify exact targets and spare quantity; extra full sets stay in storage. Existing sale and movement rows remain visible with Remove, preserving their actual shared transaction semantics.
- New-offer quantity fields accept whole 1–99 values on Enter/blur. Changes replace that row's quantity. Invalid/non-finite input, insufficient quantity for selected targets and unavailable offers are rejected. Exact used assets remain quantity one.
- Edit reuses the unified purchase dialog, initialized from the actual row rather than the last Shop choice. Complete sets can convert to modeled individual units; role, Store/Install and exact piston/cylinder/valve/tire positions remain editable with actual fitted wear/type/faults and All/Failed/Worn/Clear shortcuts. Used units retain seller identity/condition; used kits remain intact. Different individual specifications still require a complete matched set for installation.
- Dialog Apply replaces the original row rather than staging another purchase. Cancel/X changes nothing. Row/owner signatures, real fitted positions, quantity, roles and conflicting offer/slot checks prevent stale edits, duplicate charges and silent loss/merging of other rows. Cart editing does not rewrite the next Shop purchase draft.
- Retain the shared Current/Planned/No wear dyno on the right and expose the existing four-stat summary there. Actual purchases, sale credits, net total and balance after are formatted in a fixed footer with Clear and Checkout. Invalid/unaffordable plans explain why checkout is disabled. Clear has confirmation; cancelling checkout returns to the same page. Durable checkout still applies/charges once through the existing atomic transaction.
- Cache page inspection/rows until profile, car, cart or unit state changes. Hidden Shop offers stop evaluating while the cart is open. Idle and dialog editing do not recalculate the cumulative preview. Player supplies/condition/ownership and saved files remain untouched until checkout.

No physics, pricing-policy or save-schema changes. No new per-screen control theme or focus/Tab navigation. Different-block installation remains gated. Full grouped cart stats/warning exploration, additional storage/donor destinations and later inventory/assembly migration are not advanced by this checkpoint.

## Checks

- Final Windows `Invoke-Unity.ps1 BuildPlayerOnly`: passed.
- Integrated `Test-NativeMenu.ps1 -FixtureName native-cart-page-02 -PartsOnly`: **398 checks, zero runtime errors**, covering Build/detail/purchase regressions, exact set/unit/used checkout, retained originals/spares, durable save failure/retry and cart row editing/layout. This run preceded the additional Settings-return/used-row/owner assertions, corresponding isolated stock fixture and final null/changed-car guard. The final focused gate covers those additions; other runtime behavior is unchanged.
- Final packaged `Test-NativeMenu.ps1 -FixtureName native-cart-page-final-guard -CartOnly`: **289 checks, zero runtime errors**, including the thirteen-car model baseline, exact cart pricing, kit→unit editing without duplicates, valve roles, tire corners, used identities/condition, stale/duplicate edits, F8, Settings drafts/return, idle caching, read-only saved assets and compact/1080p/4K layouts. [Combined evidence](native-cart-page-verification.txt). Gates overlap and are not summed.
- Current-window cart and valve-edit dialog visually reviewed; compact viewport and actual 3840×2160 render-target captures verified. Raw screenshots/profiles/logs remain ignored in the named fixture directories.
- UTF-8, report links and staged whitespace checks passed. Unity font serialization verified as whitespace-only and restored.

The first focused cart diagnostic passed 284/0 before the additional Settings/used assertions. An earlier compile identified a missing namespace import in the new verifier; it was corrected before player testing. These are diagnostic history, not extra accepted gates. Historical full native 1,133/0 and cart-domain 102 remain retained baselines; no unrelated physics matrix was rerun for this interface checkpoint. Existing Unity obsolete-API warnings and the monitor's D3D upload-buffer notice remain in raw logs; no runtime/USS errors were observed.

## Review

- Build → Cart: check Back, row spacing, right-side dyno, fixed prices/balance and scrollbar alignment.
- Edit a piston kit: choose Individual and exact positions, add spares, then Apply. Reopen it to verify the selected targets. Cancel/X must preserve the row. Repeat with valves/springs/plugs/injectors and tire corners.
- Change a new row's quantity; attempt fractions or an amount below selected replacement count. Edit a used item and verify its locked quantity and retained incoming condition.
- Add sales alongside purchases and inspect the net price; cancel Checkout/Clear, then confirm them explicitly. Back/F8/Settings should preserve the planned cart and return context.

Stop for user visual validation after the local commit. This is the requested cart-frame/row-edit checkpoint, not completion or acceptance of the remaining 4F/G/H workflows.
