# S06c native manual refill UI

## Scope

Engine oil and coolant refill now take an early typed branch in the owned-car maintenance dialog, before the legacy payment-request clone and full-profile JSON snapshot. Fuel refill, oil change, coolant flush, repairs and all other maintenance actions retain their existing legacy route as specified by `S06C_DECISIONS.md`.

The typed dialog uses the existing native modal, `NativeTuningRange`, shared slider fill, delayed numeric input, right-button fine drag and middle-button reset. The range retains its `tuneRange:` component name so touch adjustments keep resolving; its test-facing wrapper has the maintenance ID. It obtains immutable typed quotes through `TryQuoteManualRefill` and submits the terms shown to the player; confirmation does not silently refresh/reprice a stale quote. The facade validates the submitted quote against current state. The dialog displays pending, retry, recovery and completion status and disables the range while a retained operation is unresolved. Oil/coolant maintenance buttons remain available as `Check save` or `Recover save` entry points while retry/recovery is retained, even if the fluid is already at capacity or other service availability is false. That recovery-only dialog has no refill quote and disables quantity controls; after resolution it instructs the player to reopen maintenance to review current capacity. A dialog opened while another view owns that operation exposes only its retry/recovery state; it does not inherit completion identity or auto-confirm. After the block clears, the currently shown quote refreshes once when no range gesture is active, including definite recovery/non-commit transitions that do not advance the completion counter. Terminal rejection/failure remains visible and refreshes quote terms before another explicit confirmation, so the same retained operation or stale quote is not silently resubmitted. The existing dialog is only updated in place; its controls are not rebuilt during owner-frame polling. Accepted operations are tracked by their operation ID and do not cause navigation when the originating view closes.

## Review checklist

- [x] Oil/coolant manual entry exits before `JsonUtility.ToJson` and `FromJson` in the legacy dialog path.
- [x] Reuses the established native modal, input range, palette classes and tab-traversal exclusion.
- [x] Confirmation is explicit; pending disables duplicate submission and keeps retry/recovery explicit.
- [x] Owner-frame update changes labels/buttons only and has no view rebuild or navigation action.
- [x] Native verification now authors a persisted/reloaded two-fluid capacity fixture, coolant confirmation, delayed publication, preference/settings-draft preservation, and desktop F8 oil, coolant, and combined-fill paths.
- [x] Parent-run native70/0 and F8 52/0 prove real manual actions, stale recovery, pending closure/navigation, actual Settings Apply and draft retention. Visible Recover/Complete child labels and geometry are checked and captures inspected.
- [x] Windows build and independent functional source review pass; the coordinator commits the checkpoint.
- [ ] Unknown/retry/failed-recovery and interruption/resume coverage plus the measured full-frame performance gap remain mandatory S06d work before S07. No human visual/phone acceptance is claimed.

The advancing verification records native quote, stale quote, pending, retained-settings-draft, and completed-dialog screenshots. While the SQLite worker is held, it applies a real separate native Settings change and checks the same profile reference and unchanged pending refill resources, then creates another unsaved draft and verifies it remains after refill publication. It restores the original shared audio volume and driving preference through their separate preference files. Frame phases cover fixture setup, quote/stale validation, pending writer, publication, native Settings draft retention, and each F8 action; they do not omit operation-completion or navigation frames. The UI author did not run Unity, a player process, or Git.

Automation and source inspection do not represent human visual acceptance.
