# S06d2b1c — row identities and actual Move callbacks

Baseline: `28ccdf1`, main. This is a diagnostic checkpoint in the mandatory S06 cart-preview conversion, not a passing Step 1, S06, runtime JSON retirement, or S07 gate. Previous goal turn made progress through committed characterization; this increment adds independently reviewed actual-ID observation and callback evidence. Unrelated reports, fixtures, attachments and `Tests.ps1` are excluded.

## Source and release checks

The optional partial hook observes the exact GUID already returned by the allocator, outside its sequence lock. It does not choose IDs, reset or consume sequences, or expose callbacks under that lock. The bounded thread-local lease is opt-in and absent in release. An optimized standalone C# compilation without Editor/Development symbols passes metadata checks: no observer members, nested types or partial hook remain; the public allocator API remains present. No allocator was invoked by that check. This is not a Unity release-player, Android or performance result.

Both changed PowerShell tools pass AST parsing. Unity CLI 1.0.0-beta.14 was verified current. Source manifests contain 1,054 files (DragLab C#/metadata, Packages, ProjectSettings and the two tools). The pinned v1 trace remains unchanged, SHA256 `422cfeb051d05b7d6bb4bc9690f07b401ae7c6c9e6dcde865cd44fb855581423`.

## Editor evidence

All runs use Unity 6000.6.4f1, `CartPreviewCharacterizationVerification.RunIdentity`. Raw output remains local under `reports/save-refactor/S06D2B1/identity-*`; logs/manifests are under `reports/save-refactor/S06D2B1C/`.

| Run | Actual result | Disposition |
| --- | --- | --- |
| a | Compile fails: three CS0103 references to `usedPhysicalId` with parameter named `usedPartId`; no characterization execution credited | Corrected name. All 1,054 manifest files were unchanged after failure before source thaw. |
| b / `identity-20261028-114623794` | 223 PASS /21 FAIL, one OPEN | 18 compound preview input-invariant failures retained; three tire metadata expectations corrected. Same-definition used-unit fixture selected a different piston spec from the installed one, yielding no valid positions; corrected through a real setup purchase. Frozen source unchanged before thaw. |
| c / `identity-20261028-115446525` | **255 PASS /20 FAIL**, no OPEN, SKIP or group exception | Ten preview scenarios prove complete row/quantity/member identity bijections and exact typed output parity with the frozen reference. Twenty compound production/reference preview source/catalog/row/RNG invariants remain strict failures. This run does not itself emit per-field diffs; prior JSON-normalization evidence is retained separately, not substituted for new field-level proof. Frozen source unchanged after terminal completion. |

Final c trace SHA256: `f314c37bce3b92bf64fd9741ad1f4b8e7037e1f2dc3e56a85c4a44e2e558551e`.

The ten scenarios cover forward/reversed mixed rows, three identical loose sets and units, selected members plus loose extras, two same-definition set rows in both orders (with the normal duplicate-offer Total rejection separately asserted), a same-definition loose-set/installed-used-unit combination, two new tire sets, and wheel/tire selected corners with extras. A separate malformed missing-tire-ID Apply case proves the independently emitted displaced tire ID; it is not a valid-preview or no-heal exception. Negative bijection and lease lifecycle/overflow/foreign-thread/exception probes also pass. Existing physical IDs remain literal; only actual newly emitted/parent-derived IDs receive input-keyed aliases. No output-list ordering shortcut or replacement GUID generator is used.

The corrected same-definition fixture uses a real authored offer and `PartOperations.Buy(..., install:true)` before taking the preview input snapshot. The purchase uses express delivery with zero local stock, cash `100000000 -> 99999802`; dealer generation then supplies a genuine matching unit and position. The trace retains setup stock, cash, installed and dealer identities. Wheel members retain destination cylinder indices; incoming tires retain `cylinder=-1`, with corners represented by their array position. These were test expectations, not gameplay changes.

Do not add overlapping b/c totals or compare literal GUID traces across Editor runs as deterministic-byte proof. Raw stopwatch ticks and zero reported allocation bytes are not a native timing/allocation acceptance result.

## Native/build evidence

Windows c compilation exposed two native-only revision snapshots declared int rather than long. After exact source/process verification these declarations were corrected. Windows d/e/f/g/h/i then built successfully. All 1,054 frozen manifest files remained unchanged after each completed build and each native run, before any thaw. No Editor identity source changed after Editor c. Final Windows i managed assembly SHA256: `dc8e92d3818f0871a539b95c1c162d455623935b35aa18611ae4dfb40f18b359`.

All native attempts use separate isolated fixtures `s06d2b1c-native-seed16102026-a` through `-e`, real native input, deterministic capture and frame instrumentation. Do not add their overlapping checks.

| Native run / build | Actual result | Disposition |
| --- | --- | --- |
| a /d | 43 checks /0 runtime errors; OPEN before Move | Fixture incorrectly required three full sets locally stocked. Replaced with a real three-purchase local/express quote and budget; no stock fabrication. |
| b /f | 44 checks /0 runtime errors; actual two setup buys, then card miss | Corrected real page/category/window readiness and hit-test checks. No Move evidence credited. |
| c /g | 52 checks /0 runtime errors; accepted window has no matching cards | The fixture used FitsAny, which fits any catalog model. Corrected it to the actual selected resolved build, preserving Compatible filtering. No Move evidence credited. |
| d /h | 105 checks /0 runtime errors; five actual candidates; then mixed-Shop card miss | Every candidate logs four JSON calls, unchanged source/RNG/clock/sequences and no exception. Corrected mixed selection to active query/section/Compatible/stock/budget, and stale-cart oracle to the actual dialog guard. This is partial flow evidence. |
| e /i | **118 successful checks /0 runtime errors; full flow ends strict-red** | Seven candidates each make four JSON calls; two mixed NewSet candidates also advance the actual part-ID sequence. Nine findings deliberately fail the final contract assertion. No probe faults or OPEN/skipped paths. |

Final e exercises empty-cart Cancel/Add, stale origin/revision/cart rejection, normal Shop NewSet staging and second Move through the nonempty-cart legacy route. Stale origin uses the explicitly labeled captured real part callback; stale revision/cart use rendered Add. Stale-cart dispatch proof requires an enabled button and empty-to-exact inline error, retained modal and unchanged injected row. The outer part-card counter stays zero because the dialog-local guard is a different path. The mixed item `reed/CoolingCap/0` is Compatible, active-query matching, stocked (one unit), price50 with cash2680. Final staged order is Move /NewSet /Move. Only the test's cart rows are cleaned up; three real purchased spare assets remain in the isolated fixture as declared setup.

All seven final scopes report unchanged serialized source, Unity RNG and exact fixed clock, with no exception. The first five sequence maps are unchanged; the last two advance only `part`, 4→5 and 5→6. Source review confirms PreviewProfile→Apply→Buy→NewId("part"): the observer does not generate those IDs. Candidate elapsed times are 370.091 /370.055 /349.180 /372.975 /371.523 /378.372 /404.086 ms. These are diagnostic synchronous spans, not a frame or allocation acceptance result; large typed audits also add test-only overhead.

The probe reads cumulative current-thread JSON counts inside an opt-in synchronous scope, covering nested callbacks independently of global counter publication. Full serialized-profile snapshots are diagnostic only. Authored catalog contents are nonserialized/excluded; reference/revision labels cannot prove their immutability. The script exits failure because the read-only contract remains unmet. No Android build/device run, normal-game performance pass or human visual acceptance is credited.

Final raw report: `reports/player-fixtures/s06d2b1c-native-seed16102026-e/verification.txt`, 348,761,252 bytes, SHA256 `050495956da15fb17d6329e1c6e22513a006d33598c7fb0760d74b5fb153ba16`. Inputs, frame CSV, screenshots, logs and manifests remain local/ignored. Dedicated-suite console projection bounds long display lines; full raw evidence is untouched. An independent streaming replay of the earlier d report verifies its original 258,740,624 bytes and SHA256 `af11043fb38b15edfc5c20844ce1a72777c1babbdab813a5bf63f1f37ed44f96` before/after (172 lines,14 shortened,maximum displayed length1748).

## Exact staged-source repeat

The initial staged check exposed seven trailing spaces in the new native diagnostic file. After the completed i/e run and source verification, only those spaces were removed; the pre-cleanup text remains locally archived. No Editor identity/domain source changed. Windows j successfully builds the exact final staged source, managed SHA256 `286c0fafd94f590c2627aff799d60347d8702e4a302ae1c77e0f0964e1e2a501`. All 1,054 source-j files remain unchanged after build and native f; no Editor/player process remains.

Native f repeats **118 successful checks /0 runtime errors /nine strict findings**, with the same seven JSON deltas and two part-sequence advances. Its raw report is 348,761,252 bytes, SHA256 `37f75608ea57df9992fceda4c62ebfe86143ce34651176cfa92ff11afd667e85`. A streaming e/f comparison confirms exact equality of14 selected semantic records: seven ordered candidate rows with JSON/source/RNG/capture/clock/entropy/exception facts; three stale guard records; mixed NewSet stage; final ordered rows/cash/revision/entropy; findings; and check totals. Timing values, raw typed snapshots and per-process catalog reference hashes are deliberately excluded. This is scoped seeded repeatability, not byte-identical reports or complete profile parity.

## Remaining gates

The complete valid/malformed matrix, 100-row/quantity-99 coverage, per-field no-op/heal dispositions, shared typed planner/resolver design, all-caller nonempty preview activation, owned refresh and the full native frame gate remain required before S07. S07–S14 and every no-runtime-JSON/no-whole-profile-save-copy conversion remain active. This source neither changes checkout/save authority nor supplies a legacy runtime fallback for a converted caller.

Interface review checklist: verify normal Move open/destination/Cancel/Add; stale input/revision/cart rejection; actual nonempty legacy route; no production debug UI; desktop F8 retained; mobile unaffected. Automated execution is technical evidence only, with no new human visual acceptance claimed.
