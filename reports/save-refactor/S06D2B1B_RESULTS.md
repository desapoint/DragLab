# S06d2b1b characterization expansion — in progress

Status: intermediate diagnostic progress; no passing implementation/Step1/S06 checkpoint.

This is Editor-only diagnostic infrastructure. No production caller, save format, preview, ownership behavior or user state changes. The no-runtime-JSON/no-full-profile-save-copy end-state remains mandatory. Step1, the typed planner, full S06 and S07–S14 remain open.

## Attempt 1: incomplete timeout, no result credited

Unity6000.6.4f1 / current CLI1.0.0-beta.14, session86343, `RunCoreRows`, timeout900 seconds, exit1. Source compiled; the run reached sale/move cases but the CLI stopped it before a final checks/trace file. No assertion count, complete-group claim, determinism or runtime pass is credited. Raw log: `reports/save-refactor/S06D2B1B/core-rows-editor.log`; artifacts: `reports/save-refactor/S06D2B1/core-rows-20261027-155853445/`. Existing source-difference files retain known JSON materialization findings. These files/logs remain local and ignored.

Pre-run manifest: 1,046 files, `expansion-source-sha256.txt`, SHA256 ddaa02f9185822c4694de7bb1593eb9d0d024197eb30e2536e1cf0494fe395fb. Game source was frozen by exclusive ownership while running. An immediate post-run hash comparison was not captured before the subsequent permitted edits, so no before/after manifest-equality claim is made for this incomplete attempt. Exact terminal process evidence confirmed no Editor/import process remained before thaw.

Read-only author audit identified a test expectation issue: a used member from another cylinder must retain identity/condition while adopting the explicit destination cylinder/valve. The old incoming-equality assertion would falsely reject that legitimate assignment; no final run result was available to attribute a count. The bounded correction changes only expected position fields and chooses actual nonfailed stock, preserving all other exact checks. No game behavior changed.

## Bounded rerun design

The original combined run was too large for900 seconds. Existing all-group/full-collector paths remain available. Five explicit subsets partition added coverage: CorePurchases, CoreSalesStock, TopologyMembersCorners, TopologyOwnersParts, TopologyBlocksLocks. Unknown subset values throw. Each uses the same strict assertions/oracle and nonzero failure result. A separate `progress-v2.txt` stream retains incomplete evidence on later interruption, clearly separate from a final complete trace. This is diagnostic scheduling, not reduced coverage or a green full gate.

Final-source subset results and independent Luna-high evidence review are pending. No new checkpoint is complete.

## Attempt 2: complete sales/stock subset, known red and uncovered stock case

Session82200 / `RunCoreSalesStock`, folder `core-rows-sales-stock-20261027-161725462`, exit1 after185 PASS /9 FAIL (194 assertions). No group exception, PAIR_SKIP or CORE_ROWS_SKIP. All9 failures are strict legacy source-mutation findings:3 each in part/unit sale previews,3 in move-before-sale Total. Exact rejection messages match. Trace SHA25619862798fcde8536de7e34bc60fed3318106419e01c9fc2832fb4195c38dd655. All1,046 frozen manifest files matched after terminal exit; no process remained.

The sequential-stock case is explicitly OPEN: the selected HeadIntake has no individual role. This is a fixture-selection gap, not successful partial-stock coverage. Before the next freeze, the selector was corrected to an actual compatible stocked multi-member catalogue product with individual roles (the same authored constraints used in the new-unit cases). No part/stock row is fabricated; controlled quantities still undergo global validation. This requires a fresh run; the preceding result is retained unchanged.

## Attempt 3: corrected stock case passes

Session46972 / `RunCoreStock`, folder `core-rows-stock-20261027-162547662`, exit0,48 PASS /0 FAIL. Trace SHA256ec14fd2ac62e2ee0f455b00b530499efe419eb979907b461365ac77d3ead5ef6. All1,046 final manifest files match after terminal completion. The selected actual product is juniper-club/SparkPlugs/0: existing stock0/0 controlled to5/5 and globally validated. Four local units cover the set, then1 local+1 express cover the two unit purchases; Total54, express fee1, local stock consumed5, remainder0. No OPEN/PAIR_SKIP/group exception in this subset. This is a passing stock subcase only, not Step1/S06 completion.

Current frozen manifest `final-expansion-source-sha256.txt` has1,046 files, SHA25661d6e829b4c958f1fd418e55bade12ac74cbec74dbcd1d7fd7b97c797cb06536. The subsequent CorePurchases session6611 uses the same source; results pending.

## Attempt 4: complete purchase subset, two manual expectations under audit

Session6611 / `RunCorePurchases`, folder `core-rows-purchases-20261027-162718542`, terminal exit1:259 PASS /26 FAIL. All1,046 final manifest files match after exit. Trace SHA256acfa137886cc8981a656c73e381f54cdd3c54b06364826b403cd67d7b340723d. No CORE_OPEN, CORE_ROWS_SKIP, PAIR_SKIP or group exception. Twenty-four failures are the strict legacy JSON mutation assertions across eight previews. Two additional failures concern manual HeadIntake root/member count and installed location expectations; they remain red pending source audit, not presumed game bugs or passing cases. Corrected generic used-member destination-position, condition, identity, displaced-member and untouched-neighbor checks pass.

Next: unchanged-source block/lock topology diagnostics. The two manual purchase assertions must be dispositioned and corrected/verified before a checkpoint claim. No Game source is thawed during these runs.

## Attempt 5: block preparation and service locks complete, known red

Session43585 / `RunTopologyBlocksLocks`, folder `topology-blocks-locks-20261027-163604285`, exit1:84 PASS /8 FAIL, trace SHAbce49df62a9168d15d3f94baf42be90643b8fe40f103999029d917e8add1e9c3. All1,046 final manifest files matched after terminal exit; no Unity process remained before thaw. No OPEN/PAIR_SKIP/group exception. All8 failures are strict JSON input-mutation assertions in isolated BlockBuild/Total/Preview paths. Actual Reuse produces ready block plan Total1177 with exact legacy output parity; a deliberately removed required piston pack produces an incomplete-plan error. Garage and rebuild lock first-error checks pass.

Read-only purchase audit confirmed two manual harness expectations, not game bugs: HeadIntake has0 modeled members although purchase Count fallback is1, and fitted HeadIntake belongs to the engine Assembly rather than Vehicle. Their narrow correction and targeted rerun are pending; previous two red assertions stay in the retained Attempt4 result.

## Attempt 6: corrected set expectations verified

Session83790 / `RunCoreNewSets`, folder `core-rows-new-sets-20261027-164043306`, terminal exit1:95 PASS /6 FAIL (101 assertions). All6 failures are the retained strict preview-source JSON mutation findings. Both former manual expectation failures are resolved. Modeled-member count0, exact new Assembly root binding/engine owner, displaced root condition/identity, complete loose second root and full ownership validation without Ensure all pass. No OPEN/PAIR_SKIP/group exception. Trace SHA25611aabe674cbf2f81791b839edca85e305a3bdc0d22f9ba9fb0e2d82a2a4ff9d9. Reviewed1,046-file manifest matches after exit, raw manifest SHA25602777a96d8ecba8443b904739f1f1c1aa229381db95bda85b01c8d5b055ae31d.

Remaining topology subsets run on that same reviewed source. Prior source manifests/results remain historical evidence; no claim that every earlier subset ran on this later binary, and no broad pass follows from correcting two manual assertions.

## Measurement limits

These are semantic Editor diagnostics, not native frame/performance acceptance. The method file retains raw Stopwatch ticks; no new millisecond comparison is claimed without its frequency record. Unity Editor allocation counters may return0 and are not treated as zero-allocation evidence. The large typed audit/snapshot/difference cost is test infrastructure, not added gameplay work. V2 includes some literal physical-ID diagnostic labels; no byte-identical cross-run V2 replay claim or new frozen V2 fixture is made. Existing v1 reference remains unchanged. The stronger input-row/action/quantity/member identity map and real native clock/identity-stream observations remain open.

## Attempt 7: member/corner topology complete, five additional findings pending audit

Session88459 / `RunTopologyMembersCorners`, folder `topology-members-corners-20261027-164318421`, exit1:225 PASS /30 FAIL. All1,046 reviewed-manifest files match after terminal completion. Trace SHA256ee3f56743965fc4cc4ab73e3a854851383dee51dbcc041a4da46456746924182. No group exception or PAIR_SKIP. Twenty-five failures are known strict source-mutation findings. Five additional strict failures remain undispositioned: second-car aster/Ignition member count, plus exact untouched-corner comparisons for tires0/2 and wheels0/2. Output parity alone does not establish preservation versus original input; no untouched-corner success is claimed. Root previously reported that preservation too early in commentary and corrected it once full results were read.

Explicit OPEN strata: optional second car shares cylinder count; individual brakes have no modeled roles/positions (two corner notes plus group summary). No fictitious brake members or additional cylinder-count gate is introduced. Read-only source diagnosis is underway; no Game edits until the next owner-transfer run is terminal. Original failures stay red.

## Attempt 8: owner/part transfers complete, output differences pending exact fields

Session81640 / `RunTopologyOwnersParts`, folder `topology-owners-parts-20261027-165213014`, exit1:143 PASS /38 FAIL. All1,046 reviewed-manifest files matched after terminal exit; no process remained before thaw. Trace SHA256d8a64df303cb78c78dd3828a902645e1fceb9bd3b9d60606d98e018c4b356024. No OPEN/PAIR_SKIP/group exception. Thirty-six failed assertions include known JSON source mutations (two collision parity assertions also carry the wrong manually expected message). Two additional strict output assertions fail: exact same-engine return fluid/tune state and exact selected-car graph during a cross-owner move. Their values were not recorded, so the cause is pending focused instrumentation.

Collision expected-message fixture error is concrete in source: both destinations are the selected car, so the earlier selected-slot check throws `Conflicting slot installations.` before the later `Conflicting destination slot installations.` branch. The exact manual expectation will be corrected; no production check order changes. Ignition-count expectation will use the actual resolved second-car build. Corner and owner output comparisons remain strict; new before/after typed output records will diagnose, never normalize or exempt, their differences.

## Attempt 9: exact corner differences identified, ignition fixture fixed

Session99339 / `RunTopologyMembersCorners`, folder `topology-members-corners-20261027-170635253`, terminal exit1:226 PASS /29 FAIL. Ignition count now passes against the resolved Aster build. Remaining25 source-mutation assertions stay red; four untouched-corner assertions also remain red. Trace SHA25624b7baa46b7d201484e7770b010d29ed1c30949dab8c9214911f80d995679bba. All1,046 `output-facts-source-sha256.txt` files match after terminal completion (manifest SHAc3fec9f29bce1796b504edb7a81d41134a43927783347774d7de59b51a9647fa). No PAIR_SKIP/group exception; earlier explicit optional-cylinder and unsupported-individual-brake OPEN strata remain.

Immutable canonical before-facts were captured before compared calls. For all12 untouched members (three neighbors in each of tires0/2 and wheels0/2), the complete captured after-value equals the before-value with exactly `definitionId<System.String>=null;` changed to `definitionId<System.String>=System.String{0:};`. Offline analysis checks the rest of the canonical strings byte-for-byte: runtime type, physical ID, numeric bits, wear/fault/history and remaining fields agree. This is a precise legacy JSON output-normalization finding, not a domain wheel/tire wear mutation. The four assertions are not normalized or waived. New typed projection must preserve accepted nulls and untouched values exactly. Local field proof: `corner-output-field-proof.txt` (no game state modified by that offline diagnostic comparison).

Final owner-state output diagnostics run on the same frozen source next. No full Step1/S06 gate is closed.

## Attempt 10: exact owner output differences identified; first-error fixture corrected

Session52667 / `RunTopologyOwnersParts`, folder `topology-owners-parts-20261027-171420440`, terminal exit1:143 PASS /39 FAIL (182 assertions). Trace SHA2569ad396072e9d8095d4be033b04467ae0c8674bb4233fa840c62442a71303835d. All1,046 output-facts manifest files matched after exit; no Editor remained. No OPEN/PAIR_SKIP/group exception. The expected selected-slot collision message now matches both observed production and oracle exceptions. Thirty-six source-invariant assertions remain red; three strict output-preservation assertions also remain red (one additional selected-car check was deliberately added, not a new failure hidden by changed counts).

Complete immutable before/after records account for all output differences:

- Target and non-target fluid objects are exactly unchanged.
- Target and non-target tune records differ only by absent `awd` becoming a materialized `AwdSpec`. `awdConfigured` remains false on both sides; every remaining captured tune field is exact.
- Both selected-car graph comparisons differ only by `garageJobId`/`customName` null-to-empty strings, absent `awd` materialization, and absent `lastPass` becoming a default `PassRecord`. Every other captured graph field agrees. The pass record was absent, so this is not a recorded driven pass or an approved initialization.

The offline field proof checks that exactly these explicitly identified substitutions reproduce each complete after-string. It does not modify domain state or normalize any assertion. Local proof: `owner-output-field-proof.txt`. All3 output assertions stay strict failures. Future typed projection must preserve accepted nulls, inactive optional structures, history absence and unrelated records exactly. No cleanup/healing of existing player saves is authorized by these findings.

## Intermediate source/evidence disposition

All newly added bounded groups have now been exercised; observed manual fixture mistakes were corrected and the changed cases rerun. Earlier results and exact source manifests remain historical, not relabelled as the later binary. This is an intermediate diagnostic source commit candidate, not a passing implementation checkpoint, full Step1, S06 or planner gate. The legacy implementation still fails preservation contracts. No production caller or save behavior changed; no Windows/Android rebuild or human visual acceptance is claimed for these Editor-only changes. Retain the d541ad8 production build/live evidence as historical, including its failed frame gate.

| Current scoped evidence | Passing assertions | Strict failures retained | Scope |
| --- | ---: | ---: | --- |
| Stock46972 |48|0|Exact set/partial unit local stock, express fee and cash arithmetic |
| Corrected sets83790 |95|6|Modeled payload, binding/assembly owner, displacement and spare roots; JSON source failures |
| Purchases6611 historical |259|26|24 JSON failures plus2 manual set expectations subsequently corrected above; no new count aggregation |
| Sales82200 historical |185|9|JSON failures; its uncovered stock case subsequently exercised above |
| Blocks43585 historical |84|8|Ready/incomplete block and service locks; JSON input failures |
| Members/corners99339 |226|29|25 JSON input failures +4 exact null-string corner output failures |
| Owners52667 |143|39|36 source-invariant failures +3 explicit optional/history/text output failures |

These overlapping subsets must not be summed as unique tests. All completed runs recorded final checks/traces and nonzero exit on false assertions. The first900s timeout has no completed count. Independent Luna-high final review approves only the intermediate diagnostic source/evidence local commit. It independently verifies all12 corner and6 owner output pairs, the owner trace hash/sidecar and strict red outcomes. This is not a passed implementation checkpoint, Step1, planner or S06 approval; see docs/save-refactor/S06D2B1B_REVIEW.md.

Remaining Step1 work stays explicit: full supported valid/malformed/first-error matrix closure, input-row/action/quantity/member identity bijection, real native Move callback/cart/clock/identity-stream trace and the relevant no-op/healing dispositions. The shared narrow planner and typed preview remain later S06 work; full owned-refresh/frame gates and S07–S14/no-runtime-JSON/no-profile-save-copy requirements are unchanged.

Static checks before the local commit: both new runtime-folder files are fully UNITY_EDITOR guarded, both metadata GUIDs are unique in Assets, required v1 fixture SHA422cfeb0…581423 is unchanged, edited text is explicitly UTF-8, and no unexpected Game/Packages/ProjectSettings drift was observed in completed source-manifest comparisons. The explicit12-file staged stat and git diff --cached --check pass before commit; unrelated changes remain excluded. No new player build is needed for Editor-only characterization; platform/runtime gates remain later required work.
