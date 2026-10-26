# S06d2a browser integration — design review

Date: 2026-10-25
Review type: independent Luna-high design/feasibility review of the bounded browser integration contract before implementation.
Scope: contract only. No source edits, Unity runs, process interaction, Git operations, or performance claims.

## Verdict

**Approved for implementation with the contract clarifications below.** The proposed owner-thread incremental builder and separately budgeted visible-card window can preserve the current browser semantics, provided the implementation converts every consumed live input and every action callback to the stated typed freshness fences. This is not approval of source, test evidence, responsiveness, user-visible acceptance, full S06, or S07.

## Freshness boundary

Capture is its own bounded phase. Prefix/resumable checks during capture may reject early, but they cannot certify the capture or replace one complete typed same-turn `TryFinish` after all facts have been captured. The complete finish must compare the whole captured read set with freshly supplied current inputs; any mixed snapshot is discarded.

After that finish, every preparation chunk that reads live owner/profile/catalog data must perform a complete `MatchesCurrent(freshInputs)` on the main thread immediately before consuming those live values. There must be no yield or mutating callback between comparison and the chunk's live reads. This validation tail is indivisible, measured separately, and is not covered by the 2–3 ms preparation budget. Do not spread it across frames or treat prefix validation as certification. Work that consumes only private captured values does not require a live-data check for those reads.

This distinction prevents A→B→A contamination: if a preparation chunk would consume B while the final state has returned to A, its pre-chunk exact comparison sees B and rejects before consumption. A final-only check after live preparation would be insufficient. Capture remains safe with a full final comparison because any B value actually captured differs from the restored A baseline; early capture checks are useful only for cancellation and cost control.

## Integration conditions

- The parent confirmed `NativeParts.cs` is core-owned and assigned the callback-fence additions in `NativePartPurchases.cs` and `NativeInventoryMoves.cs`. This resolves the initial ownership gap, subject to the frozen implementation updating every mutating path and preserving the existing `NativeCart.OpenPartPurchase` flow, which creates cart entries without a Parts token.
- External price facts cover delegate-priced `OwnedPart`, `OwnedUnit`, and `Used` entries, with stable unique `(source, assetKey)` identities and exact coverage for the delegate-priced rows. Generate them once for each owner-thread input wrapper and refresh them for current comparisons. Price-fact production is separately budgeted. New Shop offer prices should continue to come from the authoritative `Ledger` calculations over captured stock, holds, catalog and fitment facts; do not duplicate the full quote calculation in the input wrapper.
- Only the existing exact browser computations may produce entries and previews. Keep entries private until complete, retain warning/action state through invalidation, apply the latest presentation state at acceptance, and never use a blocking constructor fallback to claim bounded behavior.
- Keep JSON/full-profile preview replacement as an explicit required S06 dependency for nonempty carts. The empty-cart conversion does not close full S06. Typed cache keys, specification snapshots, card values, cache lifetimes, and action confirmation all remain in final source review.
- Measure every comparison tail, preparation step, query/sort, publish/attachment and visible-card step through first complete list. Preserve exact entry ordering and values, conditions, stock/holds, warnings, seven deltas, and action behavior. Any later frame spike remains a gate failure.

## Review state

The design and frozen dormant-backend checkpoint are approved for this limited increment. The parent recorded passing focused checks, the full frozen Editor run, the Windows build, and 70 native legacy-route checks with zero runtime errors; all 14 native screenshots matched the existing baseline byte-for-byte. The separate 50 ms native frame gate still fails on the legacy route (one of 174 frames: 118.455 ms frame / 90.100 ms Parts). Since this checkpoint leaves the native route dormant, that failure does not invalidate its backend-only result and blocks claiming live-route acceptance; the next implementation must activate and measure the bounded path. Keep full S06 open until nonempty typed `PreviewReadOnly`, owned refresh, action fences, live UI integration and all responsiveness evidence pass.

## Diagnostics parser review — narrow static checkpoint

**PASS, tool scope only.** Static review of `tools/check_parts_browser_frames.py` found strict 20-column header matching, UTF-8/BOM-safe CSV reading, contiguous frame validation, finite/nonnegative timing validation, explicit `-1` support only for Unity's unavailable recorder sentinel, and fail-closed handling for empty/missing selected phases. It keeps separate contiguous intervals when selected frame records have a gap or phase changes and reports median, nearest-rank P95, maximum frame time, maximum Parts time, every over-limit count, and allocation/GC totals/maxima. Exit `1` represents recorded-frame budget failure; invalid evidence exits `2`. The tool does not access or mutate game state.

The parser reports allocation/GC data but applies no separate allocation ceiling; it only enforces the supplied frame-time limit. A reported zero allocation total is explicitly not treated as proof of zero allocations. As documented in the script, it cannot prove scenario coverage, first-complete-list timing, visual parity, Android performance, or the cause of a stall. These limits are appropriate for this diagnostics-only checkpoint, but the tool cannot close the S06 responsiveness gate by itself.

The parent reports that inline adversarial probes for valid input, bad header, frame gap, NaN, missing phase/prefix, repeated intervals and the 50 ms boundary passed. I reviewed the implementation statically; I did not rerun those probes or Unity. This parser-only approval supplies no browser runtime evidence; the later frozen source and execution review follows below.

## Frozen source review — dormant backend only

**Source review: PASS for the dormant backend, with final parent-run evidence cross-checked below.** The frozen builder keeps the complete capture `TryFinish` and performs a full same-turn `MatchesCurrent` immediately before each live-reading preparation chunk, with another full check before publication. Planned/current mode is preserved. The model remains private until the final fence; failure is terminal and requires a fresh builder. The ready catalog token and ready-ledger constructor path avoid cold key/JSON fallback, and ready alias binding precedes reservation accounting. The scoped fitted-unit projection invokes unchanged `InventoryUnits.Install` and `InspectionBuild.Capture` with only selected-car facts, fitted part copies, the selected unit, catalog and run-checkpoint references. No new production defect was found in this audit.

The managed verification now supplements sync-vs-stepped parity with an independent test-only object-graph copy that preserves nested nulls, runtime types, aliases and exact primitive values. This avoids a Unity JSON oracle artifact: the full-JSON clone materialized a non-null default `Tune.awd` where the source value was null. Production must preserve that exact null state; no normalization change was indicated. The ready-constructor oracle covers direct-row precedence, first equivalent-row fallback, an absent product, mixed current/prior-generation holds, both `includeReserved` modes, exact quote arithmetic, cold-index preservation and zero key/encoder calls. These are test/source findings only; I did not run Unity or the Editor checks.

Final verdict: **PASS for the dormant backend checkpoint only.** Evidence is recorded in `reports/save-refactor/S06G1/native-frame-check.md` and the parent-owned S06G1 results report. The full Editor run and Windows build passed, and the old live route retained its 70-check/zero-error behavior and byte-identical screenshots. The old route still exceeds the 50 ms frame gate, with a 118.455 ms frame and 90.100 ms Parts time in 1 of 174 recorded frames. No builder activation or responsiveness pass is claimed. Full S06, the live UI/card/action path, typed cache binding, nonempty preview conversion, owned refresh, user visual acceptance, and later phases remain open.

## Final evidence cross-check

I independently checked the recorded source hashes against the frozen workspace: all 11 entries in `reports/save-refactor/S06G1/source-sha256.csv` match. Managed result files report PASS for the browser build checks (188,563), ready-ledger constructor (13,369), unit projection oracle (611), base read set (1,703), ready product groups (1,609), and Shop supply projection (37,701). The Windows build log ends with `Build Finished, Result: Success.`

I compared all 14 PNGs in `reports/player-fixtures/s06g1-dormant-native-seed16102026` to `reports/player-fixtures/s06d2a-native-seed16102026-c`; every matching filename has the same SHA-256. The native fixture’s `verification.txt` records 70 checks and zero runtime errors. The separate frame report remains a real FAIL: one over-limit frame among 174, max 118.455 ms total and 90.100 ms in Parts. This measurement is from the still-active legacy route; the dormant builder was not activated and has no new-route performance result.
