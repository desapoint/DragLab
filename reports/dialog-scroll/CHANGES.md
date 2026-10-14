# Shared dialog scrolling and stable threshold gestures — 2026-10-14

The shared native modal now keeps its title/X row from shrinking under long content. Content starts sixteen logical points below the header, and the existing eight-point themed scrollbar sits ten points from the dialog's right edge. Reviews/actions remain fixed. Opening any new modal resets its content scroll to the top and hides horizontal scrolling. This covers all native dialogs using the existing shared host, including maintenance, Settings, warnings, filters and purchases; it does not create a separate control theme.

The maintenance threshold still updates its shared slider/fill/number while held. The parts list, chosen positions and cumulative quote refresh once the drag ends, so the dialog cannot shift under the pointer on every value change. Confirm is disabled while the shown quote is awaiting that refresh. Right-drag fine adjustment, middle-click default, precise typed values, exclusions, retained wear/identities, express pricing, cancellation and explicit durable confirmation remain.

## Checks actually run

- Current Windows `BuildPlayerOnly`: passed; packaged build includes the SQLite checkpoint.
- Native maintenance: **63 checks / 0 runtime errors**. Real left/right held drags verify unchanged row instances, slider bounds and summary until release. Typed/middle reset eligibility, shared thin track/knob/switch geometry, long-list scrollbar inset/header clearance, 1080p/4K sizing, failed-save retry and exact original-condition retention pass. [Evidence](maintenance.txt).
- Native Settings: **146 checks / 0 runtime errors**, including shared header clearance, Apply/Cancel, reset, hover and compact/4K checks. [Evidence](settings.txt).
- Native warnings: **283 checks / 0 runtime errors**, including shared scrollbar/right inset, reachable fixed actions, wrapped long content at compact/1080p/4K and no mutation from previews. [Evidence](warnings.txt).
- Inspected the rendered long maintenance dialog: rail begins below X, has a narrow right gutter and leaves the footer review/actions clear.

These are current focused native gates. The SQLite matrix remains the preceding committed 205/0 baseline; it was not unnecessarily rerun for this layout/gesture change. Technical checks do not substitute for user acceptance or authorize another Slice 4 step.

## Review

Open Replace worn and increase the threshold to show a long list. Hold a coarse or fine drag: the numeric value should move while the list stays put; releasing updates the targets and price. Try scrolling, Cancel/X and a Settings confirmation. Confirm the existing shared styling feels consistent.

Local screenshots remain outside Git under `reports/player-fixtures/dialog-scroll-maintenance/`, `dialog-scroll-settings/` and `dialog-scroll-warnings/`; compact/4K exports are kept in the screenshot tool's resolution subfolder. [Long-list preview](../player-fixtures/dialog-scroll-maintenance/maintenance-batch-long-list.png).
