# Independent review: S06d2b characterization plan

## Decision

The contract and matrix provide a strong, appropriately bounded plan for characterizing the `PartCart` domain behavior. The source inventory accurately describes the main `Total` order, sale quote validation, move-copy behavior, ordered `Apply`, preview-only `Ensure` mutations, reservation ledger, and the ownership/condition consequences of `SharedInventory.Move`. The matrix asks for exact errors, identity mapping, condition and ownership parity, stock/hold immutability, mixed row order, malformed inputs, and separate method and full-frame measurements. It also clearly says that the oracle, no-op proof, and performance evidence are pending.

The source closure and matrix now include the actual `NativeInventoryMoves` dialog envelope and its ordered candidate construction, destination and block-preparation branches, stale-action boundaries, and encoder retirement. That closes the caller-level gap identified in the first review. The plan is concrete enough to execute as step 1, but step 1 itself remains pending; no runtime characterization, implementation, build, or caller switch is approved here. The existing S06 plan continues under its whole-plan authorization and technical gates.

## Verified domain behavior

- [`PartCart.cs`](../../Game/Assets/DragLab/Scripts/Domain/PartCart.cs) first resolves the target car and checks rebuild availability, materializes the rows, enforces the 1–100 bound, creates the reservation-aware ledger, and validates rows in order. The sale validator is lazy at the first sale. The first move creates one JSON copy for `Total`'s move checks. Prepared block checks can invoke `PreviewProfile` and resolve each affected target after the row and slot checks.
- `PreviewProfile` roundtrips the whole profile, runs `MechanicsOwnership.Ensure` before and after `Apply`, raises copied cash, compares reservation lines, and clears a matching copied reservation. `Apply` skips sales and applies non-sale rows in source order. The closure correctly distinguishes this legacy preview from checkout's sale-first mutation; the typed preview must not mistake the legacy omission of sale effects for checkout semantics.
- [`SharedInventory.cs`](../../Game/Assets/DragLab/Scripts/Domain/SharedInventory.cs) confirms the described source/destination effects: unit storage changes, engine detach/attach and tune/fluid transfer, part removal/replacement, displaced part storage, and stored-engine installation. The required identity, multi-owner, engine, and condition cases are represented in the matrix.
- The plan correctly leaves the source graph untouched, forbids normalization in the typed preview, treats `Ensure` no-op/healing as a pending oracle question, and requires exact values plus ordered, error-specific comparisons. It does not claim that the current source-only inventory or prior domain tests complete step 1.

## `NativeInventoryMoves` caller closure checked

[`NativeInventoryMoves.cs`](../../Game/Assets/DragLab/Scripts/Runtime/NativeInventoryMoves.cs) adds behavior that is not currently represented in the closure table or a focused matrix case:

1. Opening the move dialog captures `profile.revision` and a JSON signature of the current ordered cart; the signature is serialized again on the final Add move callback. `Current()` revalidates the originating card action when there is an action origin.
2. The selected destination may be initialized from an existing move for the same asset. `Candidate()` removes every current cart row for that asset and appends the new move row. This deliberately changes that row's position in the candidate sequence, which can change the ordered result of later cart rows.
3. The target choices come from `SharedInventory.Containers` and are filtered by item type and detached-engine state. Destination changes recompute the candidate. A cylinder-block move to a car takes a separate `NeedsPreparation`/`OpenBlockPreparation` path; it does not run `Candidate()` there.
4. `Candidate()` calls `PartCart.Total` using the selected car and the default clock, then `PreviewProfile`; the dialog resolves and displays the selected car's proposed inspection/dyno/warnings. The final callback repeats the candidate, checks revision and cart signature, then stages the move. Failures must not stage it.

The updated closure and matrix now require this actual callback flow: initial destination and existing-move edit; candidate row order before/after replacement; destination changes across storage, other car, and detached engine; selected-versus-target car effects; the block-preparation branch; and stale revision, cart contents, or originating card action at destination selection and final Add. They require capturing the actual candidate input and verifying failures leave `game.CartLines` and the source profile unchanged. Those probes are still pending execution.

The updated matrix includes both dialog cart-signature encodes in the no-JSON retirement requirement. The signature currently uses `PartCart.RecordRuntimeJsonCall()` and `JsonUtility.ToJson`; replace it with exact typed ordered-row equality as part of the converted callback flow. Do not leave a legacy JSON signature as a hidden preview-route dependency while claiming the converted operation executes no JSON.

For reproducibility, the updated matrix correctly distinguishes the actual dialog's default `RuntimeEntropy.UtcNow` from direct fixed-`now` probes. Record the native clock context for the caller trace; do not silently compare sale-inclusive candidates generated at different times.

## Gate boundary

The matrix is a design for step 1, not evidence that step 1 has run. Keep all source checksums, frozen-oracle execution, valid-graph `Ensure` no-op proof, invalid-graph healing divergence, caller-flow probes, and performance/native measurements pending until root runs and reviews them. No planner implementation, partial production route, legacy fallback for unsupported carts, or S07 checkout change follows from this review.
