# Deferred engine-block purchase/install workflow

Decision recorded 2026-10-12. **Deferred to 4H Parts shop/trade cart interface refinement. No runtime implementation is delivered by this decision.** The user postponed this change after clarifying the purchase behavior and objective.

Engine blocks follow the same Buy and Buy + install / Add to cart and mark install actions as other parts. Buy alone puts the component into inventory and does not disturb the fitted engine. Installation replaces the block inside the current engine; it must not silently create or exchange a separate engine assembly.

When an engine block is added to the cart for installation, ask how to prepare its companion components:

1. **Reuse fitting owned parts:** retain compatible fitted identities/condition and allow compatible inventory parts to fill the plan. Show missing/incompatible parts explicitly.
2. **Select/build parts manually:** let the player choose companion parts through the cart. Keep the current engine intact while that plan is incomplete.
3. **Required stock/OEM parts:** offer the actual compatible stock/OEM component sets for that engine, with correct cylinder/valve/unit counts and total cost.

Objective: a block purchase must not unexpectedly leave the player without a working engine. Before applying installation, validate a mechanically complete, compatible configuration and disclose existing faults that still prevent operation. Stage changes first; do not dismantle the current engine while browsing or assembling a draft. An invalid/incomplete installation plan stays unapplied and identifies what needs fixing. This is not a guarantee against existing vehicle faults, driving abuse or future wear.

Show the retained/new components, missing items, cumulative price and shared stat/dyno comparison before checkout. Explicit confirmation applies the whole plan atomically; Cancel, stale quotes and failed saves retain the current engine, cash, parts and resources. Keep the engine identity, actual reused wear/faults and removed parts in inventory. Do not silently refill fluids, repair old parts or charge for unreviewed extras.

Finalize the exact reuse fallback during the 4H control review: whether to offer missing OEM purchases automatically or have the player add them explicitly. The three choices and completeness objective are recorded; no automatic missing-part charge is approved merely by choosing reuse. Direct Buy + install should use the same preparation/confirmation rules.

Keep current native controls, dialog styling, explicit cart checkout and F8. Deliver the future dialog/action as its own user-reviewed checkpoint; this note does not authorize implementation of 4H or another page.
