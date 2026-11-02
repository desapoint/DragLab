# S06d2b1l1c1 — independent catalogue reference preparation

Baseline: main `069f3bd`. Status: **Editor liveness passed; independent Luna-high reference-preparation review approved**. No Domain implementation or full C1 completion is claimed.

Separately named C1 Apply/whole-part/whole-engine references preserve the old C0/I/J/K/L1B oracle bytes. Their private Editor-only catalogue adapter freezes the old Find/RebuildIndexes/Spec/Resolve behavior and nested lookup helpers. Unchanged Fits, Recipe and numerical/copy helpers remain source-pinned; public Find must remain unchanged while the old L1B supply reference uses it.

Luna-medium authors own the new reference files and provenance documents. Luna-high reviewed the call closure, cache/error order and copy routing. Root owns the scoped checker hook, runner, execution, source manifests and Git. The checker serializes the complete wrapper, rejects reentrancy, resets the override in `finally` and checks cleanup on error and success. Three direct null-receiver comparisons supplement the unchanged 642-assertion matrix, giving an expected 647 checks. The default C0 route remains intact.

## Current execution

- Unity `6000.6.4f1`, CLI `1.0.0-beta.15`.
- Frozen A source manifest: **2,149 paths**, SHA256 `A82E334D72C296123F463345A4441C620F57DEB72DD5AF44754A2C8CF71038F2`.
- Before launch: all source hashes/path set, 127 Domain entries, ten prior dependency entries and eight old oracle files verified unchanged; actual Editor/player absence verified through CIM. These dependency groups overlap and are not additive distinct-file counts.
- Reference A: **647 pass / 0 fail**, terminal exit 0, at `S06D2B1/cart-catalog-reference-20261102-180014114`. All 2,149 source hashes and the complete path set remain unchanged after execution, as do the Domain/dependency/oracle manifests. Actual Editor/player absence is verified after terminal completion.
- Checks SHA256: `756F953765879886EC07C9FCC8CD091A352DF3C9EFC9EC83EC466A9A85881585`.
- Raw trace SHA256: `3EDEE64F3421A0A6E57CF8EC3289CC082F64D92DF2E6415EED1F879C277128A6`.

Against the accepted C0 trace, all 761 lines match except the selected oracle-name header and one PASS ordering diagnostic that prints two independently generated new root GUIDs. Normalizing only those declared diagnostic fields produces SHA256 `AAA13535F29D410C6EE33E2E6764729B0578A602EDD143812D3918F77EF5777F` for both traces. No assertion or state comparison is normalized away. This is not a raw byte-identical trace claim or a newly executed default-C0 regression.

Exact normalization: read with UTF-8 (optional BOM), preserve LF and the final newline; replace `oracle=LegacyC1CartApplyReference.Apply;` with `oracle=LegacyCartApplyReference.Apply;`. Only on the line beginning `ASSERT|PASS|reversing actual purchase/Move rows changes the installed root / `, replace regex `(?<==)part-[0-9a-f]{32}` with the literal `<generated-root>`. Join the original lines with their endings, encode UTF-8 without BOM, and hash those bytes. No other line/value is replaced.

Root changed four explicit null-exception constructions into natural receiver dereferences, then added the missing Domain namespace import. Adapter source SHA256 is `2C82E8503226148C8F5C310A62DEA61495A329C396825E798630CD45B0370112`. The namespace addition after semantic review changes no method body. New metadata GUIDs are unique, and five changed/new Runtime source files pass UTF-8/whitespace/EOF checks.

See the [catalogue freeze](../../docs/save-refactor/S06D2B1L1C1_CATALOG_FREEZE.md), [reference tree](../../docs/save-refactor/S06D2B1L1C1_REFERENCE_TREE.md) and [independent review](../../docs/save-refactor/S06D2B1L1C1_DESIGN_REVIEW.md). No new player build is needed for these Editor-only additions; the prior production build is retained rather than rerun. The existing L0/L1A/L1B/K/I/J suites were not rerun in this preparation step because their frozen helpers and production bodies are unchanged. No native/device performance or visual result is claimed. Full C1, S06 and S07–S14 remain open; production edits require the verified local preparation commit and intact pre-edit baseline under the conditional Luna-high GO.
