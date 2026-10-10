# Shared money formatting correction (2026-10-10)

- Use `DisplayUnits.Money` for all game-facing currency labels. Keep the established dollar prefix; group thousands and use decimal separators from the current locale. Whole-dollar prices/balances/rewards show no cents; per-litre fuel pricing and detailed valuation figures retain two decimal places. Signed receipts and valuation adjustments place the sign before the currency symbol.
- Cover native navigation, maintenance buttons, quantity/service/rebuild confirmations, current balances, shortfall warnings and reset descriptions. Cover legacy headers, dealership, shop/cart (including credits and cash after checkout), saved-setup purchases, component/individual service, sale/salvage breakdowns, career rewards/entry fees, business and recovery displays. Displayed eligibility and valuation explanations share the formatter too.
- Preserve amounts, pricing formulas, stored numeric values, resources, draft/confirmation behavior and atomic saves. This is the requested formatting correction; it does not advance the remaining stats groups or Slice 4 phases.

## Verification

- Final Windows development build passed with Unity 6000.6.4f1; executable/supporting files are updated.
- Final isolated native probe: **399 checks / zero runtime errors**. Evidence: `13-native-verification.txt`. Twelve added assertions cover English (en-US) and French (fr-CA/fr-FR) grouping, cents, signs, zero and wide intermediate totals; two added live assertions compare refill/rebuild totals and balances against the shared formatter. The existing shortfall gate now checks exact formatting and a zero balance. All prior Settings, Engine/dyno, ownership, maintenance and failed-save gates passed.
- Reviewed the actual 1280×720 refill capture: `Cash $952 100` matches the dimmed navigation, the total remains `$6`, and the controls fit the existing dialog. Compact maintenance reachability and existing 1080p/4K menu render gates passed.

- Runtime/static audit: dollar signs outside verification code remain only in the common formatter and two regex replacement strings. No raw currency concatenation or hardcoded amount remains in the game-facing C#/UXML/JSON audit. The business expansion's previously unprefixed hourly rate now uses the same currency format.
- First native run passed the locale/refill checks but stopped at an added assertion assuming a post-repair rebuild quote exceeded 1,000. That unsupported test assumption was removed; the test now checks the actual authoritative quote and grouped balance. No economy calibration was changed to satisfy it.
- UTF-8/whitespace checks passed; build-only trailing-space serialization noise in the two bundled font assets was normalized. Raw captures, player builds/logs and isolated fixture saves remain ignored. Historical simulation/economy gates are retained; they were not rerun for this display-only change. Legacy money call sites were audited and compiled, not individually clicked through.

## Review

Open Garage → owned car → refill/service/rebuild confirmation: balance should match the navigation, totals remain correctly formatted after moving quantity sliders, and insufficient funds show a formatted shortfall. Compare legacy shop/cart/business/valuation through F8. Confirm/cancel behavior and underlying amounts stay the same. Human review is separate from automated checks.
