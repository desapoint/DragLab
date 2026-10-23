# S02b installed binding capture

Baseline: `62b2bc0` on main. One installed relation lookup family, with Luna medium implementation and Luna high checkpoint review.

The new internal operation-local capture maps fitted bindings once through the S02a sparse identity index, preserves row order and captured slot strings, and supplies a copied definition map plus borrowed actual part handles. Missing records remain missing; no replacement identities or conditions are created. The strict index fallback handles more requested/missing IDs than source records while retaining existing missing-part omission. Existing source topology validity remains a precondition; corrupt-state first-match behavior is not a promised authority.

Public Definitions and Resolve reuse one capture per invocation. Resolve consumes fitment/warning reads synchronously and disposes before returning its resolved result; only authored definition copies leave fitment capture. InspectionBuild captures definitions and explicit typed component copies once, disposes the borrowed view, then prepares its temporary physical preview. Cart previews and detached assembly preparation remain separate existing paths. No UI, physics/audio or persistence semantics change; no new worker traverses live state.

Borrowed handles are not immutable worker-safe state. The helper must not escape or remain across source mutation/callback/publication. Disposal clears references. No global profile/revision cache, full-profile copy or JSON is added. Remaining profile JSON copies and JSON-based preview/cache fingerprints are later conversion work and cannot be claimed removed by this slice; spec/tune copies that are already typed remain as-is.

## Verification

Prior full harness revision: **250 focused parity/scaling checks + 72 existing isolated storage checks passed**, combined batch exit **0**, Unity 6000.6.4f1 Windows target. Command: `unity run <repo>/Game --timeout 900 --log-file <repo>/reports/save-refactor/S02B/editor.log --no-tail -- -nographics -buildTarget Win64 -executeMethod DragLab.Editor.StateCommandCheckpointVerification.RunInstalledLookup`. Active player saves and the pre-existing global SQLite report are outside the injected test paths.

Three authored representatives (Aster petrol, Fjord diesel, Atlas AWD) cover public definition/resolution/inspection output parity and typed copied component fields. Source operation/lifetime and missing/blank/fallback/duplicate-category behavior pass. Runtime/source semantics and exception boundaries on corrupt input remain as documented; no full-topology validator was added. A subsequent final test-source revision adds truly different fitting front/rear tire-definition IDs; **220 focused parity checks passed**, exit **0**, via `InstalledPartLookupChecks.RunParityStandalone` and `editor-final-parity.log`. This overlapping subset is not added to the earlier full-run count. Production and timed algorithms are unchanged after the full run.

## Measurements

Five warmed samples, fixed logical fixtures. Helper comparison includes captured ordered references, definitions and typed part copies for both paths; legacy timed baseline reproduces original production List.Find loops, while correctness assertions use an independent ordered-scan implementation. Reflection equality is outside timed intervals. Public endpoint times are descriptive post-change measurements only; they have no separate old endpoint timing baseline. No dyno probe or whole save-transaction timing claim.

At 30,000 parts, helper median milliseconds:

| Model | Placement | Legacy scans/map/copies | Shared capture/map/copies |
| --- | --- | ---: | ---: |
| Aster | First | 0.0995 | 0.0714 |
| Aster | Last | 15.5249 | 1.6992 |
| Aster | Shuffled | 6.1655 | 1.7378 |
| Fjord | First | 0.0975 | 0.0733 |
| Fjord | Last | 15.2619 | 1.7337 |
| Fjord | Shuffled | 7.8436 | 1.7416 |
| Atlas | First | 0.1339 | 0.0935 |
| Atlas | Last | 18.4667 | 1.7791 |
| Atlas | Shuffled | 9.5693 | 1.8282 |

The versioned S02B-matrix.csv contains all 36 size/placement/model cases; raw 15-column CSV is under S02B. Independent Python validates dimensions, all expected workload groups, five repeats and finite median<=sample-max. Any measured helper regressions are listed in the final summary below. No allocation counter, total heap or physical phone/frame-tail measurement. Temporary definitions/entries/request maps retain only bounded installed relations and borrowed part handles during the synchronous capture; no new full condition graph is copied except the pre-existing inspection's needed detached components.

No packaged player/UI or physical-phone acceptance is claimed. Independent Luna-high **PASS** is recorded in docs/save-refactor/S02B_REVIEW.md; local commit closes this checkpoint.

First attempted run stopped at compilation: CS0136 in parent integration (outer binding loop reused the tire loop variable) and CS0104 in tests (System.IO/Domain DriveType ambiguity). Parent corrected the outer name and explicitly aliased the test enum. No runtime checks ran in that attempt; editor-compile-failed.log is preserved.

Independent CSV comparison found helper median regression cases: []. Small samples are descriptive and do not establish percentiles or total-game latency guarantees.

Final-source supplemental receipt: S02B/installed-part-final-parity-checks.txt (220 PASS), S02B/editor-final-parity.log (batch exit 0). The fixture changes the selected front versus rear tire **definition** IDs while retaining each physical PartInstance and individual tire/member identity. Existing full spec equality already covers the serialized per-corner definition arrays; no missing-NonSerialized-array assertion defect was found. Production and timed algorithms are unchanged since the prior 250+72 complete run. Luna-high review passed; local checkpoint commit follows.
