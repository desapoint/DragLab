# S05a1 typed scalar and fluid-row foundation

Baseline `ad4ed4e`. Scope is dormant typed row/key/hash encoding and one immutable fluid row. No production migration, schema-version change, save caller, UI, physics or audio changes. This checkpoint does not convert a live save path.

Two Luna-medium owners implemented the codec and independent checks; the coordinator integrated the isolated Editor wrapper. Final checkpoint review is assigned to a fresh Luna-high reviewer. The approved no-full-profile-save-clone and no-runtime-JSON completion gates remain open for the overall refactor.

## Actual checks

The first Unity 6000.6.4f1 batch compiled and completed **89 focused checks** (`S05A1/typed-record-checks.txt`). They compare exact bytes and SHA-256 with an independent Python struct/hashlib golden; check strict UTF-8, null/empty, field order, culture independence, delimiter-safe composite identity and defensive arrays; preserve all 14 fluid-field bits and per-field hash dependencies; reject unsupported row/key/version/order and nonfinite fluid values without healing finite invalid values.

The actual managed `SqliteNative` wrapper opens only `:memory:` for focused checks. Prepared-statement binding and invariant column-text parsing preserve 1,007 signed integer probes, including extrema, negative-zero bits and 1,000 seeded arbitrary bit patterns. All 17 fluid columns are bound and read back, reconstructing all 14 original doubles exactly. Bound null, empty string, Unicode and embedded NUL remain distinct. This is Windows Editor evidence, not Android or packaged-player evidence.

Command: `unity.exe run Game --editor-version 6000.6.4f1 --timeout 600 -- -executeMethod DragLab.Editor.StateCommandCheckpointVerification.RunTypedStateRecord -logFile <repo>/reports/save-refactor/S05A1/editor.log`.

The same batch passed **72 isolated legacy storage checks** (`S05A1/legacy-storage-checks.txt`) and exited **0**. Expected corrupt/unsupported legacy-fixture rejection warnings are part of those checks, not codec failures. Do not cite the pre-existing `reports/sqlite-state/verification.txt` as this run: its user-owned contents are unchanged. Our storage log is exclusively `S05A1/legacy-storage-checks.txt` and fixtures use a unique disposable subdirectory.

Scoped UTF-8/whitespace/literal-escape checks, the codec's no-JSON/profile/reflection/SQL boundary, and uniqueness of the three added Unity GUIDs pass. An initial static guard also scanned Markdown that described a literal escape; restricting the code-only rule to C# corrected that diagnostic, without changing the source to conceal it.

## Corrections and limits

Before the first Unity run, integration caught and corrected an inaccessible helper, wrong fluid namespace, missing length frame on the record-domain prefix, literal PowerShell escapes in generated test source, and a test that inserted SQL integer literals while claiming to test binding. The final test uses bound parameters and an explicit negative-zero bit fixture. Immutable field objects are shared safely; only their private collection/returned arrays are copied, avoiding redundant field cloning and UTF-8 work.

No performance improvement is claimed for this dormant codec. Android managed/native exact-bit checks remain required before an Android release containing the typed migration. Migration rollback, full-load parity, incremental durable writes and operation-receipt reconciliation belong to the next checkpoints. Current JSON-backed production save paths are still temporary unconverted debt.

Fresh independent Luna-high review is **PASS** (`docs/save-refactor/S05A1_REVIEW.md`). Local commit is pending. No interface review or player rebuild is required for this unreferenced infrastructure helper; no visual acceptance is claimed.
