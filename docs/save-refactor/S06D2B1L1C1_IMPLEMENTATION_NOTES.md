# C1 implementation coordination — work in progress

Baseline: main `b6f1e2c`. The bounded typed non-sale Apply checkpoint now passes its full matrix, regressions and Windows build and has independent Luna-high technical approval. The chronology below retains failed and pending intermediate states; the final decision supersedes those states. The complete S00–S14 objective remains active.

## Requirements and review findings

- Use concrete typed snapshot DTOs with named fields and owned nested arrays. The initial generic reflection/field-name snapshot tree was rejected and removed. No runtime reflection serializer, JSON manipulation, full-profile mirror or catalogue copy is an acceptable replacement.
- Preserve the complete source/DTO field inventory, identities, alias relationships, ordering and deletion facts. C1 tests must compare actual snapshot values against the independent frozen result; shape/provenance checks and prior C0 parity are insufficient.
- Snapshot arrays are a final write-set in first-write order. A candidate-only part, loose unit or dealer row appended and then removed in the same evaluation is omitted from the final snapshot array because it never existed as a committed row; its birth identity remains in `Identities`, and any installed member reference remains in the changed part snapshot. A removed source row is emitted as a deletion-only tombstone with its real `SourceOrdinal`, `InsertionOrdinal=-1`, and null/default payload. Surviving additions use compact zero-based insertion ordinals among surviving additions in that row family; omissions do not reorder the remaining first-write sequence.
- Error metadata includes nullable `ErrorParameterName`, captured directly from `ArgumentException.ParamName`, alongside type, message and row index. Do not parse localized exception text.
- External input identity strings always have Existing provenance. Private virtual symbols must not escape through new-record identity fields; raw metadata or user names that legitimately equal the same text must be preserved. Leak checks must distinguish origins rather than reject every matching string.
- Public live Apply returns void: its effect sink is empty, and shared rules guard unused effect construction with `CaptureEffects`. Candidate effects remain complete. Move and sale skips have metadata-only effects and no cash/token/supply effects.
- The explicit catalogue argument to public PartOperations calls must be honored, including explicit null. Only the one-argument live state uses the owner's catalogue lazily. Retain the original car-guard/lookup failure order.
- Raw NewSet Tires leaves its pack empty. A later individual tire install can initialize four blank members in a genuinely new candidate-owned pack; only its displaced blank corner gets a new token. An existing source pack is not healed. Copy-on-write ownership alone does not make a record newly created.

## Ownership and execution

Root owns Git, Unity/build/player/process operations and the Editor runner. Game/tools edits freeze before every execution and remain frozen until terminal status, complete source hashes/path set and actual Editor/player absence are verified. No worktrees, pushes or unrelated edits are authorized by this checkpoint.

- Primary Luna-medium: typed contracts/results/state/evaluator, catalogue and market reader seams, K/I/J state/transition integration, SharedInventory and PartsMarket reader overloads.
- Rules Luna-medium: shared Apply/acquisition/unit-install rules and PartCart/PartOperations/InventoryUnits/member helper wrappers.
- Independent tests Luna-medium: Editor-only candidate-versus-frozen checker, actual authored fixtures, complete semantic snapshot comparison and corresponding metadata.
- Luna-high: independent checkpoint review before advancement. No C1 completion or performance claim may precede full differential/regression/build evidence and a local commit.

All twelve pre-production Legacy reference sources are immutable. Their captured hashes are recorded locally in `reports/save-refactor/S06D2B1L1C1/pre-production-oracle-sha256.txt`; the complete production-start source baseline is the 2,149-path `production-baseline-source-sha256.txt`, SHA256 `A82E334D72C296123F463345A4441C620F57DEB72DD5AF44754A2C8CF71038F2`.

The usage-limit interruption preserved unfinished files. All three medium agents resumed after the reset; no interrupted work is counted as a passing checkpoint. Full C1, S06 and S07–S14 remain open.

## Compile-only A — intermediate failed gate

Root ran only `CheckOrderedCartApplyCompilation`, with no candidate/profile/test execution. Unity exited 1 with fourteen unique compiler errors: eight unadapted catalogue-reader call sites, one illegal protected clone call, one unassigned tire index, one C# language-version delegate inference issue, and three checker local-name conflicts. The source snapshot has 2,163 paths, SHA256 `9D8107D8B247705D7BD8A0FC62877161064A04697F83850C133B4B2A9E837920`. Every hash/path and all twelve frozen oracle hashes remained unchanged afterward. Actual Editor/player absence was verified before reopening edits; the remaining AppData Unity CLI helper is not the Editor.

The log is local at `reports/save-refactor/S06D2B1L1C1/compile-a.log`. Those errors were corrected before compile-only B. This failed attempt remains part of the evidence; it is not a passing checkpoint.

## Compile-only B and functional A — gates in progress

Compile-only B exited 0 and emitted its explicit compile-only marker on the 2,163-path manifest `5C0ECDADC22DEFA6A583A912AE00B5F91892F8998E0FA3D15D5AEF803EB91907`. No runtime checks were executed by that method. All source paths and twelve frozen oracle sources were verified unchanged, and actual Editor/player absence was checked before edits resumed.

After the final candidate-only unit-owner metadata and checker refinements, Luna-high approved static preflight for the full functional matrix. Functional A ran on the 2,163-path manifest `6FAEF17B93A63232A994CEF4965BF8F69012F8EF42C09E277A8A7DD7F8315EA2`. Its source files remained frozen through terminal status, full hash/path-set verification and actual Editor/player absence. Failures were diagnosed read-only before edits reopened; they are not waived or treated as passing evidence.

The user reiterated that completion must eliminate runtime JSON encode/decode for data manipulation. This applies to the complete refactor, including save preparation, copies, equality/cache keys, previews, validation and settings drafts; zero JSON in this dormant Apply projection alone cannot satisfy the full requirement. Full functional/regression/build evidence, independent final review, live caller conversion and the remaining S06/S07–S14 work are still required.

Functional A terminated with exit 1: 1,058 passing assertions and 50 failures, including a runner exception after the first malformed-tire case. The remaining malformed cases did not execute. The complete 2,163-path source snapshot and all twelve frozen oracle sources remained byte-identical, and actual Editor/player absence was verified before reopening checker edits. Its checks SHA256 is `5533899CF1E09FCFDEF404D6064AE74E32A78EB2F30011967877BB27D0071238`; trace SHA256 is `97C104AF04C22E6B8BC588BD6B31236794702732EAEE78B56EC4B47BEFAC2928`.

Independent medium/high review traced the assertion failures to checker defects: reversed cash-delta sign (35), unsupported target-engine owner expectations (7), BuyUsed subtype checks demanding virtual rather than exact existing identities (2), missing derived-member aliases in frozen sequential replay (1), and preview-only tire emission expectations reused for raw Apply (four mapping/field-parity assertions). The final failure is the malformed fixture runner exception. These are diagnoses, not waived failures. Corrected tests must complete the entire matrix, including new two-corner/repeated-corner tire chronology controls, before functional acceptance.

## Functional B/C — full rerun pending

Only `OrderedCartApplyChecks.cs` changed after Functional A. Its reviewed SHA256 is `260D87B068228AADB32A20135847A147F809E9B177BD00D0156756AC5F325DE9`; metadata, all Domain sources and all twelve frozen oracle sources remain unchanged. The checker now derives raw Apply emissions separately from older preview expectations, interleaves displaced-tire identities by row/quantity, and adds two-corner/repeated-corner controls. Malformed fixtures clone valid independent graphs before applying the same corruption in each graph; their reference collector inspects serialized fields and the private tire-view reference without invoking the strict convenience getter.

Luna-high independently approved a full rerun only. Before execution, root verified the 2,163-path manifest `1F24748E987FBCCE669C3731E93780A90AEA1CB6F568F58E4CD99927CC5E9226`, all twelve immutable references and actual Editor/player absence. Functional B stalled in Unity licensing initialization with denied system-information access, a channel timeout and a licensing-client mutex conflict; no cart checks ran. Root stopped only that launch's verified Editor and licensing child, waited for terminal exit, then verified unchanged source/oracle hashes and actual Editor/player absence.

Functional C retries the same reviewed, byte-identical sources with elevated licensing access, using local log `reports/save-refactor/S06D2B1L1C1/functional-c.log`. No passing result is claimed yet. All Game/tools files remain frozen.

Functional C subsequently exited 1 with 1,157 passing assertions and one fixture runner failure. Every assertion defect from A is resolved, including the two new tire chronology controls. Null-array, length-one and null-member malformed guards execute and pass. The wrong-type fixture then attempts to store a non-tire member into a runtime `TireCondition[]`, causing `ArrayTypeMismatchException` before candidate execution; wrong-type, empty, selected-blank and unselected-blank cases remain incomplete. Source hashes/path set and all twelve frozen references remained unchanged, with actual Editor/player absence verified before reopening only the checker and its Editor runner.

Checks SHA256: `CCB4DE40A9359DD8C3798C3DCD5B1819DE064D9FEC19689B1225E834F2006B95`; trace SHA256: `C50E4DAF7C717A262ACFD52B68F6A812A9AAC5724D4B8FF4207B170B28D83F6B`. The correction will explicitly create an `EngineElement[]` containing the graph's original member references before inserting its independent wrong-type member. A separately labelled focused malformed run will validate all seven fixtures before another complete functional matrix; it does not replace the full matrix, regressions, build or final review.

## Focused malformed A — passed; full D pending

The focused seven-case diagnostic exited 0 with 27 passing assertions and no failures. All source paths/hashes, twelve immutable references and actual Editor/player absence were verified afterward. Source manifest: `27BD75E0FDA4266B76D9FA8CD958CFA803A71FE2F9FB8DA6F9BF5DD7897E9996`; checker: `584F6B29F55C20C267A77438A6C653D8FD520B50366E53C9B6D5E6514A020489`. Checks SHA256: `FD31E60DD2F7AEF1176A9DCE94E2719E4D17A8D6B455F058441CD83DF69C0657`; focused trace: `4DB9C07F1DD34EEE9180AA35BDE0D0056501C1751B6746CBEFCFFB3CF3C367D9`. The unselected-blank control also passes complete quote, write-set and field parity.

Functional D now runs the entire matrix on those same byte-identical sources. The focused result is not full C1 acceptance. Regression, Windows build and final independent checkpoint review remain pending; no Domain implementation changed after Functional A.

## Full functional D — passed; regression/build pending

Functional D exited 0 with 1,175 passing assertions and zero failures. Its trace contains 61 `C1_CASE` records plus seven malformed-source fixture records, as well as null/lazy/preview/live-override controls. All 2,163 source hashes/path set and twelve frozen oracle sources were verified unchanged, with actual Editor/player absence before the next execution. Checks SHA256: `715F7C910A68CD0CDBCAF18A5B5E503D25803B2F2AEF7A6FBE19488522E7FC40`; trace: `2AEA9F47C38080B8B5AA9A9A1F9668B497B074392636D41C4614CA677E7EA6E2`.

The required combined reference/L1B/L1A/L0/K/J/I/H/Parts regression now runs on the same manifest `27BD75E0FDA4266B76D9FA8CD958CFA803A71FE2F9FB8DA6F9BF5DD7897E9996`. Windows build and final independent acceptance remain pending. The full refactor, live caller conversion and S06/S07–S14 are not complete.

## Combined regression A — passed; Windows build pending

The combined regression exited 0 with 219,338 passing helper assertions and zero failures. Its checks SHA256 is `98755AF2531BE8A90EB6E485B6B12E55BB46935BC2DF5825740B31383FBB9CA6`. L1B, L1A and L0 traces match their earlier accepted baselines byte-for-byte. Source hashes/path set and all twelve frozen references remained unchanged; actual Editor/player absence was verified before the Windows build. These helper assertions are not native frame/device or user-interface acceptance.

The Windows build runs on the same 2,163-path source manifest. Final independent checkpoint acceptance and local commit remain pending; all Game/tools sources stay frozen.

## Windows A — passed; final review pending

Windows A exited 0 with both Unity and DragLab build-success markers. Afterward, all 2,163 source paths/hashes and twelve frozen reference files remained unchanged; elevated process inspection found no actual Editor or player (only the AppData CLI helper). `Game/Builds/Windows/DragLab.exe` SHA256 is `437691663C5FB21231524F4886E60A10B1841F13E42FBBC93EE39FBD827930CD`; the freshly built `Assembly-CSharp.dll` is 4,088,320 bytes, SHA256 `8D0A7E554A6D2214624115C2DA40D3B7A4D52B1AA7B01C57355440CBC2DE60D0`. The unchanged launcher hash is expected; the managed game assembly is new.

All technical execution gates are now complete for the bounded C1 checkpoint. Final Luna-high acceptance and the mandatory local commit remain pending. No source edits, caller conversion, native/Android/device acceptance or full S06/S07–S14 completion is implied. [Concise results](../../reports/save-refactor/S06D2B1L1C1_RESULTS.md).

## Final independent decision

Luna-high approves the bounded C1 technical checkpoint for local commit after independently reviewing the implementation, exact null/error contract, DTO ownership, metadata, complete functional/regression evidence, Windows build and unchanged-source/oracle/process checks. The source/meta static checks also validate all seven new script GUIDs and companions, and edited reports pass the UTF-8/mojibake and whitespace checks. Root records this coherent increment locally; no push or worktree is part of it. Full cart/query/caller conversion, no-JSON completion across the remaining runtime, native/device/performance acceptance, full S06 and S07–S14 remain open.
