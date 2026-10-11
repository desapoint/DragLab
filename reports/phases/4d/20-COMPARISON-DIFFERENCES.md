# 4D.10 — Differences-only stats (2026-10-11)

The user requested a filter to show only different stats in the complete comparison list.

## Delivered

- A shared Differences only button appears above the complete comparison stats on dealership and non-selected owned-car details. Existing grey/red selected, hover and mouse-down styling is reused. Default is all stats; clicking the button filters, clicking again restores all rows.
- Compare canonical numeric values with the delta-bar tolerance (0.00001); compare text settings exactly. Missing versus actual values are different, including unavailable versus zero; two missing values match. Filter nested gearing, exhaust sections, components, individual elements, saved tuning and warnings through the same definitions.
- Hide unchanged rows and groups with no differing descendants, without altering disclosure state. Identical snapshots show No differing stats. The list reflows within the existing two-column layout.
- Enabling the filter requests the existing cached copied-build performance estimates, so differences in a collapsed Performance group can still resolve. Reapply row/group filtering when estimates finish, without starting new driving probes on repeated filter gestures.
- Retain the filter preference in the UI session across comparisons, F8 and Settings return. Single-car stats stay complete and have no filter control. Summaries, dyno curves, selection, resources, condition and save bytes remain unchanged.

## Checks and limits

- Final Unity 6000.6.4f1 Windows development build passed; no compiler or USS errors in the final build log. Player/supporting files updated.
- Final focused dealership/comparison probe: **141 checks / 0 runtime errors**, PASS ([evidence](20-native-verification.txt)). Real native clicks cover both filter buttons, changed numeric and unchanged text rows, nested gearing, restore/disclosure behavior, UI-session preference, F8/Settings return, exact read-only live/disk state, cache reuse and no Tab navigation. Additional shared-panel checks cover identical builds/empty groups, refreshed Performance rows, missing versus zero, two missing values and numerical tolerance. Existing dealership transaction and compact/1080p/true-4K render-target grid gates are included. The initial run passed 139/0 before adding button layout/captures.
- Reviewed final filter-button and filtered-stat captures for both pages at 1280×720. Human readability review at other physical display sizes remains open. A D3D12 upload-buffer warning appeared during the 4K render-target capture; assertions passed and this is not counted as a managed runtime error.
- Prior full native regression **631/0** from [18](18-CAR-COMPARISON.md) is a retained historical baseline, not rerun for this requested filter. Domain mechanics/economy are unchanged.
- UTF-8/LF and diff whitespace checks passed. Build-only font serialization whitespace normalized without a content change. Raw logs, fixtures, screenshots and build remain local/ignored.

## Review

Compare a car, show Stats and click Differences only above the groups. Inspect Engine, gearing, component/element disclosures and Performance; matching rows and empty groups should disappear. Click again to restore all stats with expanded groups retained. F8/Settings Cancel and later comparisons should retain the filter preference. This requested control is technically ready for review; no later phase advanced.
