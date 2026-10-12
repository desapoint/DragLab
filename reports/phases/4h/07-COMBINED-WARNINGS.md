# Shared combined warnings (2026-10-12)

## Delivered

The approved next checkpoint adds one read-only native warning overview to the cumulative cart, the owned-car grid and owned-car details. It retains the shared metal panels, red corners, scrollbar, Close/X actions, filled icons, hover shine, pressed feedback and mouse-only button navigation.

- Cart: Introduced / Remaining / Resolved groups compare the actual Current and Planned static snapshots. Changed measurements and severity remain visible together. The heading button counts all combined entries, including resolved issues; its colour follows active planned severity.
- Owned grid: each card has a compact severity-coloured warning icon. Its click opens that car's warnings independently of opening its details or selecting it. Details reuse the same snapshot and dialog, including a nonselected car's own faults.
- Each issue shows its authoritative title, explanation and suggestion, plus the related component system when identifiable. Exact repeated generic messages collapse; different generic component messages remain separate. Remaining titles match despite changed figures/severity. Resolved issues use green and retain their previous explanation.
- Missing planned inspection displays the reason and current issues, never fabricated resolution. Engine-less/invalid owned builds expose their actual build errors rather than another car's warnings. No selected car clears/disables cart warning access.

No new diagnostics, simulation, parts, pricing, save schema or transaction behavior is introduced. Existing risks do not redefine physical compatibility, add supporting parts or block valid checkout. No wear, supplies, fitted identities or resources change when browsing; opening the overview does not save or start another driving simulation. Dealer warnings stay behind the existing inspection workflow.

## Scope and limits

The dialog explicitly says **Static inspection**. It reuses the existing authoritative hardware/condition/fluid checks; dynamic wheelspin and sustained thermal-abuse findings still require the existing driving tools. Related-system labels are presentation associations from diagnostic titles/messages, not newly calculated fault attribution. Unknown associations say Build. Issue identity follows existing diagnostic titles, with the exact source explanation distinguishing repeated generic component warnings.

## Checks

- Final Windows `Invoke-Unity.ps1 BuildPlayerOnly`: passed.
- Final `Test-NativeMenu.ps1 -FixtureName native-combined-warnings-final -WarningsOnly`: **288 checks / zero runtime errors**. Covers repeated-message identity, severity/measurement changes, independent real card clicks, nonselected engine-less warnings, details consistency, actual faulty-piston resolution, Close/X, read-only profile/cart/disk state, no additional inspection work, F8 and compact/1080p/4K gates. Includes the shared thirteen-car fit/topology/preview checks. [Focused evidence](native-combined-warnings-verification.txt).
- Final `Test-NativeMenu.ps1 -FixtureName native-cart-warning-regression -CartOnly`: **302 checks / zero runtime errors**. Existing full-stat/row/member/used-stock editing, Settings draft return, F8, cached preview, quantity bounds, clear/cancel and compact/4K regression passed. [Cart evidence](native-cart-warning-regression-verification.txt). Counts overlap the warnings-only shared baseline; do not add them.
- Actual current-warning and combined-dialog images visually reviewed at normal, compact and 1080p layouts. A true 3840×2160 render-target layout gate and capture passed.
- UTF-8/whitespace/local-link checks passed; Unity font serialization was verified as whitespace-only and restored.

 Earlier full native 1,133/0 and cart-domain 102 are retained historical baselines, not rerun for this presentation-only checkpoint. Raw fixture screenshots/logs/profiles remain ignored.

## Interface review

1. Garage → Owned cars: click a warning triangle, including a nonselected or engine-less car. Close/X should return to the same list without selecting it.
2. Open car details → Warnings; verify the same current issues and suggestions.
3. Build → stage a replacement for a worn/faulty part → Cart → Warnings. Check resolved issues alongside any remaining/introduced risks, including changed severity/figures.
4. Check scrolling at a compact window, F8 returning to the cart, and Close/X. Buying/installing still requires the existing definitive confirmation.

Technical completion is separate from user visual acceptance. Stop after committing this checkpoint; do not advance the remaining inventory/block or other Slice 4 controls without user validation.
