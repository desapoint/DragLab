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
- Freeze exact price-fact coverage before implementation: facts must have unique stable `(source, assetKey)` identities, cover every Shop/Inventory row that consumes a price, preserve current price semantics, and be regenerated from the current owner-thread state for freshness comparisons. Price production is separately budgeted; a delegate or incomplete fact array cannot stand in for the current exact values.
- Only the existing exact browser computations may produce entries and previews. Keep entries private until complete, retain warning/action state through invalidation, apply the latest presentation state at acceptance, and never use a blocking constructor fallback to claim bounded behavior.
- Keep JSON/full-profile preview replacement as an explicit required S06 dependency for nonempty carts. The empty-cart conversion does not close full S06. Typed cache keys, specification snapshots, card values, cache lifetimes, and action confirmation all remain in final source review.
- Measure every comparison tail, preparation step, query/sort, publish/attachment and visible-card step through first complete list. Preserve exact entry ordering and values, conditions, stock/holds, warnings, seven deltas, and action behavior. Any later frame spike remains a gate failure.

## Review state

Design approval is limited to the contract and conditions above. Final independent approval is pending frozen implementation, focused/native evidence, and source review. Keep the full S06 gate open until nonempty typed `PreviewReadOnly`, owned refresh, action fences, and all responsiveness evidence pass.
