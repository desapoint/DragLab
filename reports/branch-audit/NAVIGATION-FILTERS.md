# Navigation defaults and parts filters — 2026-10-15

Requested refinement only; no later Slice 4 phase advances. Technical completion is separate from user visual acceptance.

## Behavior

- Main navigation always resets the destination view, including a click on its already-selected menu. Garage opens the owned grid (the first-car exception opens the dealership); Build opens Shop / Engine / Current build; Tuning opens Engine / Both axles; Race opens Career; Settings opens Difficulty / Driving. Local searches, filters, sorts, scroll and stats disclosure/comparison choices reset. Engine workshop, cart, car/part detail, disposal and saved-setup subpages no longer reopen from an earlier main-menu visit.
- This only resets presentation/drafts. The selected car, owned assets, applied tune, condition, fluids, market stock and actual reserved cart contents remain intact. Returning from a race also opens the default Garage view. Legacy menu buttons reset the equivalent native view for F8 comparison.
- Internal Back and Settings Cancel preserve their contextual return. F8 still compares the same current interface. Unsaved Settings retain Apply/Discard/Stay protection; reselecting Settings resets its tab without applying or losing its draft.
- Part price/ownership and Stock filters are independent. Defaults and both Reset controls use **Compatible + In stock**, with unrestricted price, condition and advanced criteria. Partial stock is visible; a full-set express requirement does not mean no units are stocked. Zero-stock new products stay hidden until Stock → All. Used listings and owned parts remain available to their appropriate pages. No Express filter was introduced.
- Basic fit, price/ownership, Stock and condition controls remain visible. Reference, upgrade focus, subcategories, individual brands, tunability and numeric stat criteria use the shared **Advanced** disclosure. It starts collapsed, reveals already-applied advanced criteria on reopening, and retains criteria while collapsed. Filter edits apply only on Apply; Cancel discards them. Reused controls/scrollbars and theme; no separate style system.
- The browser's preview/specification caches remain reusable. Normal browsing still requires no full dyno per offer. Brand checks now give their synthetic new-stock fixture an explicit stocked quantity; performance-only query tests explicitly opt out of supply filtering. The shared native test click helper expands Advanced before targeting a filter inside it.

## Verification

- Windows `BuildPlayerOnly`: passed, updated `Game/Builds/Windows/DragLab.exe` and companion data.
- Focused native navigation/filter test: final run **46 checks / 0 runtime errors** on an existing save. The initial implementation also passed **65 checks / 0 runtime errors**, including fresh starter purchase. Final refinement confirms that the Advanced button is visible without scrolling at the initial 1280×720 window and covers unsaved-Settings protection. Final basic and expanded screenshots were visually inspected.
- Brand/focus matrix: **2,971 checks / 0 failures**.
- Native checks exercise real mouse/keyboard events against an isolated save, separate stock/price combinations, partial/zero/used/owned stock, Advanced visibility, Apply/Cancel/Reset, repeated menu visits and same-menu clicks, detail/workshop/cart/setup exits, Settings contextual return, F8 and profile/save-byte nonmutation. Actual cart reservations survive both reselecting and leaving/re-entering Build.
- Local evidence: `reports/player-fixtures/native-navigation-defaults/verification.txt`, corresponding player log, and `reports/branch-audit/navigation-build.log` / `navigation-brands.log`. Current screenshots are exported to `reports/interface-screenshots/` (`parts-filters-basic.png`, `parts-filters-advanced*.png`, `navigation-build-default.png`).

## Review checklist

1. Open a detail/subpage, leave via main navigation, and return: the default list/tab should open with cleared search/filter/sort/scroll.
2. Open parts Filters: Compatible and In stock selected, Advanced collapsed. Combine Affordable with In stock; inspect partial stock and Reset.
3. Expand/collapse Advanced, edit then Cancel; reopen, Apply an advanced filter and reopen again to see that criterion revealed.
4. Stage purchases, leave/reselect Build, then reopen Cart: purchases and reserved quantities remain. Cancel Settings from a detail returns there; main navigation opens the default view.

Only the focused native suite and brand matrix are rerun here, not the complete physics/economy/branch suite. Preserve existing user edits to the maintenance/cart reports and untracked `Tests.ps1` / race-exit regression files.
