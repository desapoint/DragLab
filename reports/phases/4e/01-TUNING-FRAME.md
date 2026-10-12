# 4E.1 — Tuning page frame and shared comparison (2026-10-12)

The user approved the proposed first checkpoint: native page layout and comparison area, then review before any tuning control.

## Delivered

- Selected car name; Engine, Induction, Drivetrain and Wheels category tabs; empty left control areas for subsequent individually reviewed inputs. No redundant Tuning title/subtitle, native tuning slider, configuration library or shop workflow is added.
- Right-side shared dyno with labelled Current, Proposed and No wear curves, existing colors/styles/peaks, pointer RPM/value inspection and intersection circles. The no-wear reference shares Garage's cached copied-hardware inspection and retains actual supplies; it never refills/heals player assets or fabricates missing parts.
- Compact two-column HP, torque, weight and condition summary with existing min/max bars, current → proposed values, delta interpretation and info dialogs. All detailed stat groups and warnings remain available through the same collapsible catalogue and Differences only filter. Vehicle units follow shared Settings.
- One existing `PrototypeGame` tuning draft across old/new F8 views. Native previews resolve the same installed hardware and restrictions as legacy Tuning; a requested ratio stays fixed when the OEM gearbox lacks tuning permission. Actual fitted identities, wear/faults and fluids remain in Proposed.
- Explicit Apply/Revert use the existing atomic tuning/save path for legacy-created drafts. Clean actions are disabled; failed durable save retains the editable draft/profile and shows its error; successful Apply updates the comparison and persisted car once. Leaving Tuning discards unapplied edits. Category/filter/disclosure state remains local to the UI session.
- Shared grey/red panels, tab surfaces, typography, scrollbars, hover/mouse-down states and no focus/Tab decoration. Left/right columns resize; actions remain anchored below them. Empty garage and engine-less chassis show useful unavailable states without invented dyno output.
- Previews are cached by profile/revision/draft/units; category changes, hovering, scrolling and resizing do not recapture. This checkpoint uses steady dyno/inspection calculations without running a driving probe. Dynamic pass/slip measurements in detailed stats remain unavailable until a real estimate is supplied; no zero measurement is fabricated.

## Checks and limits

- Final Unity 6000.6.4f1 Windows development build passed; no compiler/USS errors in final log. Player/supporting files updated.
- Focused native Tuning probe: **70 checks / 0 runtime errors**, PASS ([evidence](01-tuning-focused-verification.txt)). Includes fixed starter purchase, frame/tab/cache/read-only behavior, shared inspection/graph data, no-wear isolation, F8 draft ownership, hardware gates, true mass delta, pointer/info, Revert, failed/actual durable Apply, disk reload, exit discard, Settings return, no engine/car and compact/1080p/true-4K render-target layout.
- Final full native regression: **809 checks / 0 runtime errors**, PASS ([evidence](01-full-native-verification.txt)). Includes Tuning, shell/nav/shine/focus rules, Settings/difficulty/units/reset, owned cars/maintenance/dyno/all grouped stats, dealership/budget/comparison/commerce, paid used disclosure and F8. Focused counts overlap with this full gate; do not sum them.
- Reviewed populated/dirty 1280×720, 1080p and missing-engine captures; 4K target assertions/capture passed. A known D3D12 upload-buffer warning occurred during 4K capture; bounds and native assertions passed. Physical 4K monitor, controller/touch and subjective appearance remain for human review.
- Initial verification corrected a mouse-helper name and a test expectation that incorrectly assumed the fixed OEM gearset was adjustable. Hardware behavior was preserved rather than loosened.
- UTF-8/LF, matching new script metadata, local links and staged whitespace checks passed. Build-only font whitespace restored after content-equivalence checks. Raw logs/screenshots/fixtures/builds remain local/ignored; older phase archives are retained.
- Simulation, pricing, hardware semantics, career and inventory/cart mechanics are unchanged. Prior domain/economy gates are retained historical evidence, not rerun. This delivers only 4E.1; it does not complete 4E or authorize 4F–4H/deferred engine-block controls.

## Review checklist

1. Open Tuning on an owned car. Review the two-column layout, tabs, compact summary and graph legends/hover; scroll through detailed groups/info.
2. Press F8, change an allowed value in legacy Tuning, then F8 back. Review Current → Proposed bars and the unapplied state. Revert should discard; Apply should save through the existing workflow.
3. Check resizing, Settings Cancel return and navigation discard. The left control areas intentionally remain empty pending the next requested control.

Technical checkpoint: ready for review. User visual acceptance: pending. Stop before the first tuning slider.
