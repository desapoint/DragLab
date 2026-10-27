# S06d2a Inventory fresh-price allocation audit

Date: 2026-10-26. Scope: source audit and one isolated allocation-removal change in the fresh Inventory display-price validation path. No performance pass is claimed; parity and fresh profiling remain pending.

## Native evidence and boundaries

The current suffix-i run (`59513`) reports two remaining over-budget frames while opening an owned Inventory detail:

- First owned detail: 92.739 ms total, 21.196 ms attributed to Parts, one GC, 8.469 MB whole-frame allocation.
- Sale recovery: 53.470 ms total, 22.707 ms Parts, no GC, 5.308 MB whole-frame allocation.
- Repeated populated-Inventory full fresh fences and price-factory calls account for about 2.7 MB and 12–23 ms per rendered frame.

The fixture has 63 owned parts, 12 loose units, 59 vehicle listings, and 9,294 authored definitions. The exact `VehicleListings.Validate` proof is already committed and reused; its exact-current authored/catalogue comparison is about 4 ms in this fixture. The listed whole-frame bytes include UI, engine/runtime, and other work; they are not a per-method allocation measurement. The 2.7 MB / 12–23 ms figure is the reported price-factory/fresh-validation attribution, but the available evidence does not split it by individual validator method. Treat the source allocation finding below as a high-cardinality hypothesis pending a method-level profiler capture.

## Executed call chain

`NativeParts.CapturePartInputs` lazily obtains one sale-price delegate for a price-backed Inventory capture via `game.NativeSalePrices()`. It then reads current prices for each non-installed Inventory part and every loose unit: up to 63 part reads plus 12 unit reads in this fixture. The delegate is fresh for that capture; it is not retained across rendered frames.

`NativePartsAdapter.NativeSalePrices` creates `Valuations.LoosePriceView(profile, policy, "wholesale", now)`. `LoosePriceView` validates the policy, calls `MechanicsOwnership.ValidateForQuote` once for the view, captures the current profile revision, and returns a delegate that rejects a changed revision. Each delegate invocation resolves the current item and applies the existing `PartValue` or `LooseElementValue` arithmetic and rounding. No quote receipt, persistent price table, or whole-profile copy is produced on this path.

`ValidateForQuote` first calls strict `MechanicsOwnership.Validate`. Only if that returns a dealer-labelled error does it rerun the complete validation using the read-only dealer-discriminator tolerance. The current fixture directly validates successfully, so that second validation is not the measured cause. The first pass keeps the global first-error order:

1. Profile version/cash and selected-car reference.
2. Vehicle listings.
3. Paid economy, career, and garage-business state.
4. Scoped ownership indexes, duplicate identities and installed/stored graph checks.
5. Shared inventory/dealer validation.
6. Parts-market validation and remaining tire/wheel/rebuild/run checks.

The vehicle-listing positive proof skips only the unchanged `VehicleListings.ValidateCore` after exact leaf equality succeeds. It still compares all 9,294 current authored definitions and the current listing/owned-part leaf. It does not bypass the remaining global validators below it.

## High-cardinality allocation candidate

`MechanicsOwnership.ValidateCore` reaches `PartsMarket.Validate` after the vehicle-listing proof and shared inventory checks. `PartsMarket.Validate` constructs its initial global identity set with one nested `SelectMany`/`Concat` expression over:

- `profile.parts`, expanding each `PartInstance.ItemIds`;
- engines and loose units;
- dealer part/unit rows;
- every part in every vehicle listing, again expanded through `ItemIds`.

`PartInstance.ItemIds` is an iterator block. It yields the instance ID unconditionally, then visits each condition element in array order and yields only non-null, nonempty member IDs. In this fixture that creates iterator state for the owned-part sequence and roughly 2,362 listing-part records on every fresh validator call. The nested `SelectMany` chain adds iterator machinery, and the dealer-unit selector constructs a one-element array per unit row. `HashSet` construction also grows as thousands of descendant IDs are inserted. This check is required: later market-engine validation uses the set to reject collisions with all current owned, dealer, and listed identities.

This source path matches the validation boundary and cardinality of the reported repeated allocations, but it has not yet been isolated with a method-level allocator. The exact `VehicleListings` proof itself does not store/clone these listing IDs and does not replace this separate market collision check.

## Narrow optimization implemented, parity/performance pending

High review approved an allocation-free enumeration change inside `PartsMarket.Validate`, with these constraints:

- Keep the same single `HashSet<string>` and add IDs in the exact current source order: owned parts and their members; engines; loose units; dealer rows; then vehicle-listing parts and their members.
- Replace `PartInstance.ItemIds` iterator use with a normal non-iterator helper/loop. Add `instanceId` even when it is null. Then visit `condition.elements` by index and add only non-null, nonempty element IDs, in the same order. A null element array contributes no members; do not create an empty array per part.
- For dealer rows preserve the current `IsPart` then `IsUnit` selection. A unit contributes its `element.id` even when null; rows matching neither contribute nothing. Do not filter or normalize malformed records before they reach the existing validation/error path.
- Preserve null-sensitive exceptions and their order. In particular, do not add defensive skips for a null item, condition, dealer payload, listing, or listing part. The loop must fail at the same first access that the present enumerable would fail.
- Leave the subsequent market-engine validation loop and its first-error order unchanged. The initial set construction has no duplicate error of its own; duplicate behavior is decided by existing validators and the later `ids.Add` checks.

Implementation is limited to `Game/Assets/DragLab/Scripts/Domain/PartsMarket.cs`: the initial set is now built by `ExistingMarketIdentitySet` and `AddPartIds`. The remaining `Validate` body is unchanged. The helper preflights source collections in original LINQ expression-construction order so null-source exceptions occur before row enumeration, then inserts the same IDs in the same order. No performance claim is made until the independent parity check and matched allocation/frame run finish.

This change should remove per-part iterator/enumerable allocation without changing the set's members or authority. Keep the full fresh `LoosePriceView`, `ValidateForQuote`, exact vehicle proof, and final typed Parts fence. Do not retain this set or price delegate across a capture/frame. No global revision shortcut, profile/catalogue snapshot, new cache, or validation bypass is justified here.

## Other observed costs and limits

The remainder of `MechanicsOwnership.ValidateCore` still creates short-lived hash sets and `OwnershipValidationLookup` dictionaries for a profile with 2 cars and 63 owned parts, then scans shared inventory, business/economy state, and market rows. `VehicleListingsValidationProof` must continue its exact current-data and authored-token comparisons. These are real costs, but this audit does not propose weakening or caching them.

For each of 63 part display reads, `Valuations.LoosePart` scans the current part list by ID; each unit read similarly scans loose units. This is bounded and allocation-light but repeated. Replacing those scans with an operation-local index would require a separate proof that same-revision direct list replacement/reordering cannot make the view stale; do not introduce one as part of the approved set-enumeration change. `DisplayLooseTotal` also calls `GarageBusiness.Reserved` and checks `runCheckpoints.Any` for each displayed amount. Their results are currently discarded; removing them may change exception behavior after same-revision public-field edits, so this audit does not classify them as safe dead work.

## Verification gates before a performance claim

The next report should retain the same native scenario and split the price factory and each validator stage if instrumentation permits. Compare source before/after for exact IDs and traversal order. Add focused parity checks for the full validator's valid result, malformed input first error/exception behavior, and exact part/unit display prices across wear, faults, tire members, and unavailable values. Measure allocations and maximum indivisible frame time, including the actual first owned detail and sale-recovery frames. The existing 50 ms limit and full-global-validation requirement remain unchanged.