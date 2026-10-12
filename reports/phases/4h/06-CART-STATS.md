# Full cart stat comparison (2026-10-12)

User-approved next control checkpoint: extend the cart comparison area with the existing full stat groups, disclosures, differences-only filter and shared info dialogs. The separate combined-warning presentation remains the next reviewed control.

## Delivered

- Keep the dyno and four-stat summary at the top. Add the eight existing shared groups below: Engine, Induction/exhaust, Transmission/drivetrain, Tires/wheels, Weight/balance, Cooling/fluids, Components/saved tuning and Performance/warnings. Nested gearing, exhaust, fitted components and individual elements use the same catalogue as car/part details.
- Groups initially collapse; expanded rows show actual Current → Planned values, shared change bars, units and Better/Worse/Tradeoff interpretation. Use one row per line in the narrow right-side panel. No redundant detailed-stats heading, new control theme or extra descriptions.
- Differences only filters the full list and groups through the common comparison rules. The four-stat summary remains visible. An empty cart compares Current to itself and honestly shows No differing stats when filtered.
- Shared info buttons and tradeoff hints retain the concise general explanation; numbers stay in the rows/bars. Disclosure and filter choices are held per car in session and retained across cart-preview refresh, F8 and Settings return. They are not progression/preference saves.
- Expanded groups scroll in the right panel without moving the fixed totals/checkout footer. Browsing/filtering/info does not recapture the build, edit the cart, consume supplies, heal parts or write saves. Existing cart editing, original Back, atomic checkout and no-focus/no-Tab contract remain.

Reuse the cached authoritative Current/Planned inspection; no extra driving probe or fabricated unavailable performance values. Existing Performance/warnings rows are included as part of the catalogue; a separate cumulative warning overview/suggestion workflow is not delivered. No physics, pricing or save-schema change, and no later inventory/block workflow is advanced.

## Checks

- Windows `Invoke-Unity.ps1 BuildPlayerOnly`: passed.
- Final `Test-NativeMenu.ps1 -FixtureName native-cart-full-stats-01 -CartOnly`: **302 checks, zero runtime errors**. Includes the thirteen-car fit/topology baseline and existing purchase-row/used/owner/Settings/F8/quantity/cart cancellation regression, plus all eight groups, exact values, shared info, nested tuning, filtering, retained disclosure, read-only saves, fixed-footer layout and compact/1080p/4K gates. [Evidence](native-cart-stats-verification.txt).
- Current-window expanded comparison and compact layout visually reviewed. Existing 4K render-target gate rerun; raw images/profiles/logs remain ignored in the fixture folder.
- UTF-8, report links and whitespace checks passed. Unity font serialization verified as whitespace-only and restored.

Earlier integrated Build/purchase **398/0**, cart-domain **102** and full native **1,133/0** remain historical baselines, not rerun for this shared-display-only change. No new domain or transaction implementation is included. Existing obsolete-API warnings and D3D upload-buffer notice remain in raw logs; no runtime/USS errors observed.

## Review

- Build → Cart → scroll below the four-stat summary; expand Engine and the other groups.
- Toggle Differences only, including a storage-only/empty plan; verify the full list while the summary remains available.
- Open an info button; inspect nested gearing, saved tuning and component/element values.
- Edit a cart row, use F8 or visit Settings; verify disclosure/filter state and the fixed footer.

Stop after the local commit for user visual validation before implementing the separate combined-warning view or another checkpoint.
