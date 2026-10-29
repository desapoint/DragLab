# S06d2b1g native callback and envelope checks

This diagnostic increment adds two independent native routes to `NativeCartPreviewCharacterization.cs`. It does not change the Move dialog, cart, inventory, preview, save or domain APIs. Parent-owned routes are `CartPreviewDestinationChecks(input)` (`--draglab-native-cart-preview-destinations-only-verify`) and `CartPreviewPassiveEnvelopeChecks(input)` (`--draglab-native-cart-preview-passive-envelope-only-verify`).

## Destination correctness route

The route requires an isolated deterministic-capture profile and empty cart. It declares disposable budgets of **1,000,000 cash** and **1,000 reputation** and uses real `PartOperations.Buy`, `InventoryUnits.Buy`, current vehicle listings, `VehicleCollection.Buy`, `PartsMarket.BuyEngine`, and `AssemblyOperations.Detach` operations. All resulting part, unit, car and engine identities come from those domain operations. Missing authored stock, compatible offers, or affordable quotes fail the requested fixture instead of inventing inventory.

The five exercised families are:

1. A first Move opens with no destination and disabled Add. A real Storage move is staged; reopening restores its still-valid destination. After removing the empty storage container referenced by that row, reopening leaves the destination unselected and Add disabled. A rendered destination choice replaces the old row with one new row after the unrelated row.
2. An actual owned loose engine unit exposes only Storage targets and stages through the rendered target and Add controls.
3. A whole engine-slot part reopens the Move dialog separately for Storage, a same-model other car, and a detached same-model engine. Each real target selection recomputes the preview. Selected-car inspection identity and actual destination-owner identity are logged separately.
4. A real duplicate of the selected car's fitted cylinder-block definition follows normal Move staging. A fresh real changed-block Move opens preparation without staging. The visible Add Plan action then stages exactly one prepared Move row with the real block ID and selected-car destination.
5. Selecting an actual nonselected same-model car for the changed block displays the existing “Select this car before preparing its block installation.” rejection.

Actual candidate-scope snapshots are enabled only for the destination route. Per-family counts cover 14 actual Candidate scopes, including valid prior-row reopen previews and both destination selection and Add validation where applicable. The observer emits complete finite summaries with the actual cart rows (at most four in these five families), source equality, JSON counter delta, RNG/capture-clock/sequence results, selected car, physical asset location, target-owner facts before and after, and any exception. The observer record contains no full profile snapshot; no global character clamp truncates its trailing evidence fields. Any strict source or JSON findings remain failures; they are not normalized away. The previous native baseline's nine strict findings remain historical evidence.

Owned-part navigation closes an open dialog and uses the rendered Back control for an existing detail before waiting for the bounded parts list. This keeps accepted list readiness separate from detail navigation.

After staging the prepared Move, the isolated route clears its test cart, reopens preparation, changes the real block's Storage location with `SharedInventory.Move`, and clicks the actual Manual mode control. The existing detail-origin `Current()` guard must show its exact stale-read-set rejection, leave the cart empty, and leave `PartActionGuardRejectedCount` unchanged: this modal path calls `RejectPartAction` directly rather than the rendered detail callback's `GuardPartAction`. The block is moved back to its original container and the native view is refreshed. The route finishes with an actual nonselected-car rejection. The historical 118-check baseline separately covers stale rendered `partMove` callback behavior. This route does not exercise attach or a later checkout of the prepared block.

## Passive envelope route

This route can run in the genuine capture or noncapture process. It declares a disposable **250,000 cash** budget and buys one real authored engine-slot part before timing. Fixture selection and reporting happen before the interval. Both `NativeCartPreviewCandidateProbe.Completed` and `.Observed` are saved, cleared, and explicitly checked as `null` before timing. They remain `null` from before the first rendered `partMove` request through the actionable destination preview, actual Cancel, and a final continuation frame. No candidate-source snapshot or report output is generated in that interval; both observers are restored afterward.

`FramePacingProbe.OverridePhase` labels `cart-preview-passive-envelope` from the first pointer request until after the post-Cancel frame's probe update. The native report stores fixed boundary frame and stopwatch values, request-to-actionable time, the separate Cancel span, capture state, effective `RuntimeEntropy.UtcNow` ticks/kind, and actual `DateTime.UtcNow` ticks/kind. The actionable result requires a rendered enabled target and Add button, the visible dyno, and its visible `Warnings · …` label. A missing whole-frame probe fails the requested route. The whole-frame CSV is the frame evidence; callback elapsed time includes the test input helper's short pointer/layout waits and is not itself a performance pass.

The rendered Cancel path is exercised. An attach/cancellation frame family remains **OPEN**. Native frame records are diagnostic only and do not claim a 50 ms acceptance pass. The historical uninstrumented 410.077 ms observation and snapshot-heavy correctness route remain separate.

## Run state

Source changes are ready for independent source review. Unity compilation, native execution, and whole-frame CSV evidence have not been run by this author; the parent owns those operations and will record the exact terminal outcome. This file records no runtime pass. S06/Step 1, typed cart conversion, removal of runtime JSON/full-profile preview copies, and S07–S14 remain open.
