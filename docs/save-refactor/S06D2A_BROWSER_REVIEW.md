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

Design approval is limited to the contract and conditions above. Final independent approval is pending frozen implementation, focused/native evidence, and source review. Keep the full S06 gate open until nonempty typed `PreviewReadOnly`, owned refresh, action fences, and all responsiveness evidence pass.

## Diagnostics parser review — narrow static checkpoint

**PASS, tool scope only.** Static review of `tools/check_parts_browser_frames.py` found strict 20-column header matching, UTF-8/BOM-safe CSV reading, contiguous frame validation, finite/nonnegative timing validation, explicit `-1` support only for Unity's unavailable recorder sentinel, and fail-closed handling for empty/missing selected phases. It keeps separate contiguous intervals when selected frame records have a gap or phase changes and reports median, nearest-rank P95, maximum frame time, maximum Parts time, every over-limit count, and allocation/GC totals/maxima. Exit `1` represents recorded-frame budget failure; invalid evidence exits `2`. The tool does not access or mutate game state.

The parser reports allocation/GC data but applies no separate allocation ceiling; it only enforces the supplied frame-time limit. A reported zero allocation total is explicitly not treated as proof of zero allocations. As documented in the script, it cannot prove scenario coverage, first-complete-list timing, visual parity, Android performance, or the cause of a stall. These limits are appropriate for this diagnostics-only checkpoint, but the tool cannot close the S06 responsiveness gate by itself.

The parent reports that inline adversarial probes for valid input, bad header, frame gap, NaN, missing phase/prefix, repeated intervals and the 50 ms boundary passed. I reviewed the implementation statically; I did not rerun those probes or Unity. This approval does not change the pending browser source/evidence review.
