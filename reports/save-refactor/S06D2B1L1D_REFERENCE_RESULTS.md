# S06d2b1l1d independent query reference — execution in progress

2026-11-03. Production baseline `e261080`; approved design `a75964d`. This is independent Editor-only reference preparation. **Not a completed checkpoint, typed query, caller cutover, native/performance/device acceptance or full S06.** The complete S00–S14 objective, no full-profile save cloning and no JSON encode/decode runtime manipulation remain mandatory.

## Executed attempts

| Attempt | Actual result | Source evidence |
|---|---|---|
| Compile A | Exit 1: two checker compiler errors, duplicate local `clock` and consequent `Func<long>`/bool mismatch. No runtime checker executed. | 2,169 source paths; manifest `003C51B129D9006E304DEDF0B6AC8F4826ADB7E971AD79B9FF7721044B4A8889`. Post-run source/path set, Domain 266 and existing references 28 unchanged; actual Editor/player absent. |
| Compile B | Exit 0; `DRAGLAB L1D REFERENCE COMPILE-ONLY PASS` at `compile-b.log:911`. | Rename only the checker boolean to `clockMatches`; manifest `D9EB28F99730CD1632FCFABC87AA2319DCD8A6101EE5690E748C2CCDD2397DBB`. Same post-run checks pass. |
| Reference A | Exit 1; **593 passed, 63 failed assertions**, two OPEN prerequisites. Full matrix completed; no runner escape. | `S06D2B1/cart-query-reference-20261103-125854654`; same 2,169-path manifest as Compile B. Checks SHA `7305A59C5688CC922C2D9F5191B777282EED5A1A6E95B5027ED13504190C0D1B`; trace SHA `2B889EF2CA563799577706BD059C329B14B649C93E0F396D5ABB93F2E0BF9B9B`. Same post-run checks pass. |
| Compile C | Exit 0; compile-only marker at `compile-c.log:908`. | Corrected checker `ECC69BD08CE934235826CF587B5EF493619A156CBDA1985FF4A789FBC34030DA`; source manifest `E708EF230D1CB20482DC12028B1CA1FB1778F6CC9CA4DAEC30DB586B6B82251E`. Same post-run checks pass. |
| Compile D | Exit 0; no-change repeat after a conservative formatting guard rejected an EOF-trivia equivalence probe. No file was written by that rejected probe. | Same source manifest as Compile C; marker `compile-d.log:359`; post-run source/reference/path checks pass and actual Editor/player absent. |
| Compile E | Exit 0 after whitespace cleanup; marker `compile-e.log:887`. | Final 2,169-path source manifest `E6EEE5CDB6F0B79C16FB309DE5B842E896E7634264161725949041AB930736CA`; post-run all source/path, Domain 266 and old-reference 28 checks pass; actual Editor/player absent. |
| Reference B | Exit 0; **730 passed, zero failed assertions, zero OPEN prerequisites and no runner exception**. Corrected source/evidence remains pending independent review. | `S06D2B1/cart-query-reference-20261103-133719509`; same source manifest as Compile C. Checks SHA `7E10431881E54CF4546D50B0ABFB0FFD285116F725436E3E7154FCC04D8BB069`; trace SHA `01D414E4A79271F05467CB2AE3018D9F7A1D239ECF55033BC160C2E334BB6041`. Post-run all 2,169 source paths/path set, Domain 266 and existing references 28 unchanged; actual Editor/player absent. |

Raw execution logs/full execution-source manifests and generated graph traces remain local. The pre-query 266-file Domain and complete 28-file existing-reference hash manifests are required versioned oracle-provenance fixtures for future independent drift checks, explicitly separate from generated execution output. The process checks use elevated CIM, distinguishing the AppData Unity CLI helper from the actual Unity Editor/player. The full source manifest covers Game/Assets, Packages, ProjectSettings and tools, including newly untracked source paths.

## Findings and correction scope

Reference A is retained unchanged. Its reds include independent-ID literal comparisons in the reused topology helper; platform-specific natural versus constructed NRE messages; inappropriate expectations that historical JSON previews preserve input fields; two missing fixture prerequisites (actual fitted position and positive stock); raw Apply's distinct undefined-action fallback; and missing explicit fresh-tire initialization in the valuation setup, which caused an earlier global-validation error to mask intended quote outcomes.

Root corrected only the new Editor checker. Full graph values and generated physical-ID bijections remain mandatory alongside the topology-only check; alias/scalar negative controls were added. Historical source field/reference mutations now have exact paired deltas and explicit `LEGACY_PURITY|FAIL` diagnostics. They are required divergences for the future pure typed candidate, never accepted candidate behavior. Only the newly purchased untouched tire pack is initialized, with `CanSeed` asserted. No existing wear, fault, part, stock count, definition or installed member is repaired or manufactured. The Domain and all 28 prior references stay frozen.

The details are in `docs/save-refactor/S06D2B1L1D_REFERENCE_TESTS.md`. The executable reference syntax remains unchanged; post-run whitespace cleanup changed only their bytes, mapped exactly in the provenance artifact (`24E79E52…BA15` → `6686A8D8…B754`, `18A72CB0…F5F3C` → `7610C015…78E7`). All three new Runtime files have enclosing `UNITY_EDITOR` guards and verified unique metadata; their legacy JSON reference/isolation logic does not ship in players.

## Independent review and next gates

Luna-high approved the initial reference/static closure and checker `233946…FD86` for liveness only. After attempt A, root's checker corrections and new evidence still require independent Luna-high review; that agent is temporarily rate-limited until the reported 11:30 AM reset. No production edit authorization is inferred. The root physical-closure audit also identifies methods beyond the old Preview return path; the complete finite production method allowlist remains open before any candidate edit.

Reference B is terminal and its post-run checks pass. Obtain Luna-high review of the corrected checker, finite executed coverage, source-mutation findings and the still-open physical calculation closure before accepting preparation. Only after an accepted preparation checkpoint and local commit may the full L1D candidate implementation proceed under its separately reviewed method allowlist. The C1 Windows player is retained; no new player build is needed or claimed for these Editor-only changes. All later S06 caller/freshness/native gates and S07–S14 remain open.


## Reference-B scope and strict purity findings

The trace contains 49 Total records, 13 Fee records, 42 Apply/PreviewProfile/enumerator records, eight Preview records, ten direct-price records and four validation-price records. All 730 assertions pass. No OPEN prerequisite or runner exception is present.

All 29 old preview source-effect observations have exact live/frozen field/reference delta parity, but **all 29 retain `LEGACY_PURITY|FAIL`**. Across those observations the diagnostic lists 16,037 changed field paths and 2,840 changed reference paths (repeated scenarios, not distinct global records). A liveness pass is not a purity pass. These are explicit input changes the typed candidate must eliminate; the result does not waive any no-JSON/no-source-mutation requirement.

The retained C1 Windows managed player assembly remains byte-identical: `8D0A7E554A6D2214624115C2DA40D3B7A4D52B1AA7B01C57355440CBC2DE60D0`. No new player build or visual/device/performance run occurred. Root's source-provenance/audit checks and unique Editor-only metadata checks pass. Any local commit of this preparation is an **intermediate diagnostic commit only** while corrected-source Luna-high review and the complete production method allowlist remain open; do not advance to production/callers from it.


Final source cleanup is verified with Roslyn complete-tree equivalence (`UNITY_EDITOR`) and identical token kind/text sequences (4,752 + 7,030 tokens), followed by compile E. The 730/0 runtime result remains tied to its original pre-format byte hashes; no post-format full matrix run is claimed. Only syntax trivia/metadata EOF whitespace changed, stable GUIDs are retained, and the new checker itself remains `ECC69BD0…030DA`. Both the exact formatting mapping and final corrected-source review are still pending Luna-high acceptance.
