# Parts commerce clarity — 2026-10-22

Status: implementation under verification; user acceptance pending.

Scope: one native parts-commerce flow checkpoint. Retain the existing domain transactions, prices, physical identities, condition, market stock/reservations and express charges.

- Explain Shop, Inventory and Installed in place. Distinguish direct individual fitting from work staged in the cart.
- Purchase configuration names the receiving car and separates installed parts from stored extras. Cart rows label set quantities and units per set explicitly.
- Staged purchases, sales and moves show an inline status with Review cart. Sale/removal labels make the cart step explicit.
- Cart rows name their real destinations. Sale proceeds and net money received use positive amounts and clear labels.
- Checkout reviews action counts, payment/proceeds and resulting balance before the existing atomic commit; failure keeps the dialog/cart retryable.
- Completed checkout shows a receipt with direct Shop / Inventory / Installed routes. These enter the normal default browser state. Empty carts explain how to continue.
- Use existing UI Toolkit controls, palette, responsive layouts, hover/pressed states and traversal rules. Preserve desktop F8 and mobile-only native UI.

## Checks

Pending Windows player build and real-input purchase/cart/workshop regressions, compact/touch screenshots and Android build.

## Interface review

- Choose a complete set or individual replacement and check installed/stored counts.
- Stage an inventory sale or move and follow Review cart.
- Review payment versus sale proceeds; cancel without applying changes.
- Confirm checkout and use the receipt to return to inventory/shop.
- Review compact layouts and the shared grey/red theme.

No later Slice 4 phase is advanced. Physical Android interaction and user visual acceptance remain separate gates.
