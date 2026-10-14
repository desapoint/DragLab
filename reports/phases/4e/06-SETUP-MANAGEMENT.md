# 4E.6 saved-setup rename and delete — 2026-10-14

Selected setup previews now place the existing filled pencil control beside the actual setup name; Delete sits beside Load in the fixed footer. Rename uses the shared text dialog with 1–64 character validation, trimming and explicit Save. Delete names the exact blueprint and states that owned cars/parts remain. Cancel/X, unchanged names and failed writes do not publish. Stale revision/content/identity is rejected.

Only blueprint metadata is changed transactionally. This path deliberately avoids the generic part-operation preview reset: it preserves staged cart rows and rebases an existing tuning draft to the new metadata revision without applying its values. Actual IDs, worn components, money, fluids, applied tunes and other setups remain unchanged. Library names/sort/search and selected actions update from committed state. Existing blueprint load/missing-part purchasing remain authoritative and separate.

## Checks

Windows BuildPlayerOnly passed. Packaged native **48 checks / 0 runtime errors** passed: actual blueprint creation, dirty-draft/cart coexistence, no-op/blank-name validation, X/Cancel, locked rename/delete failure and retry, Unicode name/identity preservation, exact profile-only deltas, stale rename rejection, F8, compact/1080p/true 4K layouts, deletion isolation, SQLite reload, retained draft actually applying after metadata revisions, and the remaining setup actually loading through the existing confirmation. [Evidence](06-native.txt). Rendered selected-name/pencil/footer layout reviewed. The initial harness used an obsolete generic load-button ID after all management assertions; it was corrected to the established setupLoadConfirm control before the passing final run.

No new schema or physics fields are required. Earlier full Tuning/physics/economy suites remain historical baselines; this report does not claim rerunning them. Raw images remain in reports/player-fixtures/setup-management-final/ and the current gallery; resolution exports are separate.

## Review

Tuning → Load setup → select a blueprint. Use its pencil or Delete, test Cancel/X and confirm only on a disposable blueprint. Unapplied tuning and pending cart rows should survive metadata edits; loading still requires resolving them. This finishes the user-authorized sequence after sale/salvage and Race selection. All are technically checked; user visual acceptance is still pending. Stop before another phase/control.
