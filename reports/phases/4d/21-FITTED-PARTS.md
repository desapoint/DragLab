# 4D — Fitted-parts control and grouping (2026-10-12)

The requested dealership Fitted parts action stretched across the facts column, and its dialog was a flat hardware list.

## Delivered

- Compact left-aligned action with the existing filled Build icon and Stats-button treatment. Shared native palette, surfaces, hover/mouse-down behavior and no Tab/focus decoration are retained.
- Read-only shared dialog with collapsible Engine, Cooling & lubrication, Drivetrain, Wheels & brakes, Body & interior and Electronics sections. Headers show fitted component/set counts; all start closed so systems are visible together. Empty systems are omitted; future unclassified hardware has an Other fallback.
- Actual instance identities appear once. Names/categories and health or failure cause remain together; cylinder/valve faults appear beneath their own part. Long names and failure values wrap within the dialog.
- Uninspected used cars still hide this action and exact hardware. First-choice, new and inspected cars retain access. No purchase, engine-block installation, pricing, save, simulation or repair behavior changes.

## Checks and limits

- Final Unity 6000.6.4f1 Windows development player build succeeded. No compiler or USS errors in the final build log. Local player/supporting files updated.
- Final focused real-mouse native inspection probe: **57 checks / 0 runtime errors**, PASS ([evidence](21-native-verification.txt)). Covers compact button/icon/no traversal, each fitted identity exactly once, category coverage including diesel/AWD hardware, system placement, collapsed overview, Engine collapse/reopen, wrapped labels, individual seized elements, exact read-only live/disk state, inspection gates, F8, paid disclosure and broken-car purchase transfer.
- Reviewed final 1280×720 captures of the compact starter action, collapsed overview and expanded healthy/failed engine sections. Other physical resolutions were not rerun for this scoped change; existing responsive/shared-control behavior is retained.
- Prior full native regression **728 / 0** and inspection/market/payment/integration domain gates **155 / 42 / 351 / 810** are retained historical baselines, not rerun. Domain code and mechanics are unchanged.
- UTF-8/LF, local document links and diff whitespace checks passed. Build-only font serialization whitespace restored after verifying content equivalence. Raw fixtures/logs/screenshots/builds remain local and ignored.

## Interface review checklist

- Open Garage → Dealership → a known car and scroll to Fitted parts: the action should be compact with the wrench icon.
- Open the dialog: all available system headers and counts should be visible. Expand/collapse Engine, Drivetrain and Wheels & brakes; inspect part names, condition and faults, then close via Close or X.
- Uninspected used cars should keep exact fitted parts hidden until inspection.

Technical completion is separate from user acceptance. This requested control is ready for review; no later phase is advanced.
