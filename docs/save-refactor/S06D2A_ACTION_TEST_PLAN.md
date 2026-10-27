# S06d2a Parts Action and State-Retention Test Plan

## Status and boundary

This is the focused native Parts interaction plan and current implementation status. The native verifier now exercises owned unit position Apply, direct-install confirmation, inventory move confirmation, sale staging, tire-swap confirmation, and all seven detail-preparation phases using actual cards, modal controls, and guarded callbacks. These checks are implemented but still require the parent's Unity/native run before they can be reported as passing. Do not accept a cart checkout, purchase, installation, tire swap, or other durable operation; each action-under-test must be rejected as stale before it stages or commits. Preserve the exact source condition and fault data in every temporary fixture, and restore only the temporary references and IDs in `finally`.

The current native suite covers a hit-tested real Shop card open/Back/reopen, stale stock and authored-price rejection at the purchase confirmation, and stale `partAdd` callbacks across same-revision cash, stock and selected-car fuel edits. The new owned-action batch uses the existing reversible 36-row Inventory fixture (actual copied part and loose engine member) and an exact copied installed tire only when no retained tire exists. The detail lifecycle batch holds each phase after real production work, checks that phase 6 remains attached-hidden-disabled through layout, then verifies cancellation and newest-ticket ownership at every phase boundary0–6, including unchanged global surface registration and published callback references before publication and after cancellation. Warning discovery prioritizes diverse in-stock induction, piston, ignition, gearbox and clutch entries, then used offers, and inspects up to 48 Shop and 48 Installed entries using the real evaluator under the separate `test-fixture/parts-warning-discovery` label. If the bounded scan finds no natural warning candidate, the report leaves that gate OPEN without manufacturing one or claiming the entire market is warning-free.

## Minimal interaction batch

### 1. Warning text and severity through stale and failed preparation

1. From the accepted current Shop or Installed model, select a real entry whose proposed build contains at least one actual warning. Prefer a naturally retained used-part condition or existing installed condition. Do not alter wear or invent a fault to manufacture a warning.
2. Open its detail through the visible card. Record the warning dialog text, `partWarnings` caption, `car-warning` / `car-critical` class, and any visible `partAvailability` text. Keep the warning-bearing detail open.
3. Change selected-car fuel at the same profile revision and use the existing held-preparation/freshness path to make the detail stale. Before restoring A, compare the visible warning text and level to the captured values; require stale actions to be disabled and the detail to remain mounted.
4. Exercise the existing explicit terminal-failure probe while the same detail remains open. Require the failure notice to appear without replacing the detail warning text/severity. Restore the original fuel, retry explicitly, and require the rebuilt detail to show the same warning payload and level again.

If the live market/model has no warning-bearing entry, report that the warning fixture is unavailable and leave the gate open. Do not synthesize a warning or a critical fault for this gate.

### 2. Owned unit position Apply and direct-install confirmation

Use an actual retained loose engine unit that has a compatible target position in the selected car. If the isolated profile has none, a temporary test row may be copied from a real fitted `EngineElement`; preserve its definition, health, severity, fault, wear remainder and all condition fields, changing only the unique unit/member IDs needed to represent a separate retained identity. Remove only the temporary row in `finally`.

For position Apply, open the unit detail from the current Inventory card, open `partPosition`, and choose a real row from the rendered `partUnitPosition:*` choices while A is current. Change fuel at the same revision, then click the dialog's real Apply button. Require the captured position to remain unapplied, the modal rejection notice to remain visible, the profile revision/cart/cash and exact unit identity/location to remain unchanged, and the real action-origin guard to reject the stale origin. Restore A, cancel if needed, and wait for the same unit detail to recover.

For direct install, reopen the current unit detail, click `partPlanInstall`, and confirm the actual `Install unit?` modal while the detail origin is current. Between opening and clicking Install, change selected-car fuel at the same revision. Require the confirmation callback to reject, keep the modal open with its notice, and leave installed member IDs, loose-unit ID, condition, cash, cart and profile revision unchanged. Restore A and cancel. Never click Install while A is current in this stale-callback case.

### 3. Move and sale staging

Use a real owned inventory part from the existing nonempty Inventory fixture. That helper clones an actual fitted HeadIntake and PistonElement while retaining their wear; use the same temporary-object/cleanup pattern if a fixture row is needed.

For Move, open the detail, click `partMove`, select an actual destination, then change same-revision fuel and click the modal's Add move confirmation. Require the dialog's stale-origin error, unchanged cart, location, cash and profile revision, and no staged move line. Restore A and cancel.

For sale, capture the currently installed guarded `partPlanSale` callback while its accepted detail is current. Change a same-revision source field, then invoke that exact callback in the same owner-thread turn, before a later refresh can disguise the result by disabling the button. Require the action guard rejection count to advance and cart contents, inventory location/identity, cash and profile revision to remain unchanged.

The verifier already exposes `CapturePartDetailActionForChecks()` for the purchase callback. If it cannot retain the sale callback through the existing button construction seam, add one test-only, name-scoped capture seam that returns the real guarded callback bound to the visible action. Do not reimplement the guard or call `StageInventory` directly in the test.

### 4. Tire-swap dialog confirmation

Open the existing Parts-level `partsTireSwap` dialog and use a real stored tire candidate. If the isolated save has no stored tire, prepare a temporary loose tire by copying one actual fitted tire's complete `TireCondition` (including wear, failure state, fault, severity, and history), assigning a distinct ID, and removing only that temporary unit in `finally`. Do not heal or alter its condition.

Select an actual stock group and one rendered corner row. Change selected-car fuel at the same revision before clicking the real Swap tires confirmation. Require its current-origin fence to reject before `CommitParts`, leaving installed tire IDs/conditions, loose tire IDs/conditions, cash, cart and profile revision unchanged. Restore A, cancel if needed, and wait for a current ready Parts view.

### 5. Settings and tuning drafts versus Parts invalidation

There is no current UI state in which a Settings or tuning draft and a visible Parts builder coexist. Settings navigation from a dirty page prompts and `Stay` retains the draft; leaving Settings requires an explicit choice. Tuning navigation calls `DiscardPreviews()` and entering Tuning starts a new draft. The Parts page also blocks repair actions while those drafts are dirty. Do not use reflection to attach a private Settings/Tune draft to a Parts route or weaken those boundaries just to create a combined fixture.

Keep the existing native Settings and tuning draft gates as the correct coverage for those page-local contracts. In the new Parts stale/failure cases, snapshot the visible Parts warning text/severity and the Parts dialog drafts (destination, selected position, tire corner selection) instead. If product requirements later call for drafts to survive route changes, that needs a separate interface contract and implementation review before a cross-route test is valid.

## Evidence and pass conditions

For each stale callback, record the exact user-visible confirmation/notice and the relevant action rejection count. Compare only the affected asset rows and installed-slot identities, plus cart count/line identities, cash and profile revision. Keep raw data encoding diagnostics outside click timing intervals. A case passes only when the actual confirmation/callback runs and rejects; a disabled-button no-op is not evidence. After each case, restore the source field, dismiss the preserved draft explicitly, wait for the latest current model/detail, and verify the original state before continuing.

## Required test-only seam review

The source guards position Apply, direct install confirmation, move confirmation and tire-swap confirmation with the current part origin. Sale staging is a synchronous `Guarded(...)` callback that becomes disabled as soon as the detail is stale. A development-only name-scoped callback capture now returns the actual bound `partPlanSale` callback; it does not reimplement the guard. The parent task owns all Unity/native execution.
