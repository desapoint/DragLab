# S06d2b1l1a — owned canonical cart and product values

## Purpose and boundary

L1A adds dormant, immutable snapshots for cart-row signatures and authored product equivalence. It is prerequisite infrastructure for a later exact stock overlay and complete non-sale Apply projection. It does not add a stock resolver, mutate live `CartReservations`/`PartsMarket`, redirect `Apply` or `Total`, change public `ProductKey` bytes, warm a JSON cache, or activate a caller. It contains no JSON/token-format implementation and no profile or catalog mirror.

The L0 evidence is finite and reviewed: at baseline `99ad759`, the frozen `CartReservations.Lines`/`SameCart` reference and `MarketCatalogLayout.Key` boundary passed 150/0 on Unity 6000.6.4f1. L0 observed null/empty equality for the five tested cart strings, null row/default-row equality, null/empty positions and null/empty position items, with order significant. It observed ProductKey equivalence for ID-only changes, positive/negative zero, description null/empty, and nested AWD null/default, while `MarketCatalogLayout.SameAuthoredDefinition` remained different for some key-equivalent pairs. It did not establish the full serialized-field or floating-point equivalence relation. See [L0 results](../../reports/save-refactor/S06D2B1L0_RESULTS.md) and [review](S06D2B1L0_REVIEW.md). These observations are constraints, not a complete equivalence proof.

## Owned source shape and APIs

Add two Domain types in new files only:

- `CartRowsSnapshot.cs` + metadata: `internal sealed CartRowsSnapshot`, immutable `CartRowValue`, and immutable position values.
- `PartProductValue.cs` + metadata: `internal sealed PartProductValue` and nested immutable AWD value snapshots as needed.

Proposed bounded surface:

```csharp
internal static CartRowsSnapshot Capture(IEnumerable<PartCartLine> source);
internal int Count { get; }
internal CartRowValue GetRow(int index);       // immutable owned value, or null-row marker
internal bool RawEquals(CartRowsSnapshot other);
internal bool CartSignatureEquals(CartRowsSnapshot other);

internal static PartProductValue Capture(PartDefinition source);
internal bool RawEquals(PartProductValue other);
internal bool TryProductEquivalent(PartProductValue other, out bool equivalent);
internal bool TryGetProductHashCode(out int hash);
```

These are internal value APIs, not replacements for existing public helpers. They add no line-count cap, sorting, deduplication, trimming, defaulting of authored data, validation, fallback, stock choice, or ID generation. Capture consumes each input exactly once, retains source order and repeated rows, and owns every field before returning. Empty input captures an empty snapshot. No `PlayerProfile`, `MechanicalCatalog`, `PartCartLine`, `PartDefinition`, mutable nested domain object, source list, iterator, Unity object, delegate, or reference into a source graph may escape. Strings may be retained because they are immutable; arrays/lists are private and never returned directly. Any collection accessor returns a defensive copy or immutable value sequence.

`CartRowsSnapshot.Capture(null)` must throw `ArgumentNullException("source")`, matching the frozen LINQ `Select` boundary. It does not add caller-side `SameCart` guards: the existing `p.inventory.cart != null` and owner comparison remain at their current callsite. Enumeration/copy exceptions propagate in source order. Null rows are retained as a raw null-row marker; do not filter them or eagerly convert them to a default row during capture.

`PartProductValue.Capture(null)` preserves the legacy cache-miss dereference failure (`PartDefinition.Copy` on null); it does not silently treat null as an absent product. It captures one definition's value graph only and is not an authored-catalog or player-profile snapshot.

## Two cart comparisons

The snapshot must preserve raw facts and separately expose the effective equality used by the frozen Unity signature:

**RawEquals** compares captured shape and values exactly: null-row versus a concrete row; null versus empty strings; null versus empty positions list; null versus empty position item; all primitive/enum values; list order and repeated entries. This supports source preservation and diagnostics. It must never mutate or heal a captured fact.

**CartSignatureEquals** reproduces only the observed `PartCartLine.Copy` plus `JsonUtility` equivalence. It uses an effective view without overwriting raw values:

- A null row compares as the default-constructed `PartCartLine`, not as an all-zero row. The default object includes field initializers (`quantity = 1`, default `CartAction`, `InventoryKind.Storage`, and empty `positions`). It remains distinct from a zero-quantity row.
- For L0's five tested nullable strings—`definitionId`, `preparationOwner`, `assetId`, `role`, `destinationId`—null compares equal to empty in either direction. All other fields compare by their actual captured values unless specifically established otherwise.
- `PartCartLine.Copy` maps a null positions list to an empty list. A null string item in positions compares equal to an empty string item. Position order, outer row order, and duplicate entries remain significant.
- The remaining line fields are `quantity`, `install`, `preparedBlock`, `action`, and `destination`; retain exact enum underlying values, including undefined values, and exact booleans/integers.

A null outer enumerable is an argument exception, not an equality result. `SameCart` caller short-circuits stay outside these snapshots. L1A does not alter or replace the JSON-backed `CartReservations.Lines` or `SameCart`; the new comparison is diagnostic infrastructure until its full field matrix and later live-path review pass.

## Product value and equality

`ProductEquivalent` represents exactly the authored value graph that the legacy cache-miss operation keys after copying a `PartDefinition` and setting only `id` to `""`. It therefore excludes `id` from equality and includes every other Unity-serialized field:

- Every `PartDefinition` public serialized field: strings, all enums and flags, integers, booleans, floats, and nested values (`awdHardware` and its `calibration`). The field inventory is the committed `PartDefinition` declaration plus `MarketCatalogLayout.AuthoredComparedFields`; the latter is an inventory aid, not an equivalence oracle.
- Every `AwdHardwareSpec` field (`version`, `modes`, `integratedCenter`, `adjustments`, `calibration`, `responseMin`, `responseMax`) and every `AwdSpec` field under calibration.
- `PartDefinition.tuning` is the `PartTuningOptions` enum, not a nested mutable tuning object. Do not omit it or confuse it with the separate `PartTuning` behavior class.

Keep `RawEquals` separate from ProductKey-style comparison. Raw snapshots preserve original null/reference shape and IEEE float bits. Product equivalence applies only effective rules established by differential evidence: at minimum blanked ID, signed-zero equality, description null/empty equality, and the tested AWD null/default equality. Further null/default, non-finite, adjacent-float, Unicode/string, and nested-calibration rules remain unproven until L1A tests compare against `LegacyCartCanonicalBoundaryReference.ProductCacheMiss`. A rule is authoritative only when every field has coverage, tests pin both equality and inequality pairs, and the effective view matches the frozen JSON boundary. Do not infer all string null/empty rules from the five cart fields.

Product comparison is explicitly fail-closed. `TryProductEquivalent(other, out equivalent)` returns `true` only if the complete effective comparison is supported for both captured values; otherwise it returns `false` and the out value is not evidence of equality or inequality. A null `other` is supported and returns `true` with `equivalent=false`; do not first short-circuit on `ReferenceEquals(this, other)`, because even self-comparison must return unsupported when this graph contains an unsupported value. `TryGetProductHashCode(out hash)` similarly returns `true` only for a complete supported graph; on `false`, the output integer is unspecified and cannot be consumed. Supported product equality implies equal supported hashes, but equal hashes never imply equality: every candidate match still requires exact field-by-field equality. Do not insert, bucket, select, merge, or resolve products using either result unless the relevant support call succeeded for every candidate and request value.

Do not expose a hash or canonical string as identity and do not implement this by reflecting/serializing fields at runtime. No fallback to `JsonUtility`, cache warming, `MarketCatalogLayout.Same`, `SameAuthoredDefinition`, or a probabilistic digest is permitted.

If the complete JSON equivalence cannot be reproduced from tested typed rules for any serialized field or floating-point edge, `TryProductEquivalent` and/or `TryGetProductHashCode` reports unsupported for that value. Do not make a partial comparer authoritative because authored content currently lacks a counterexample.

## Coverage, ownership, and claims

L1A checks must prove:

1. A field-coverage guard covers every serialized `PartCartLine`, `PartDefinition`, `AwdHardwareSpec`, and `AwdSpec` field; every field is perturbed independently from an authored valid baseline and compared with the frozen L0 reference. The guard itself is a test-time inventory assertion, not runtime reflection. Pin the `Copy` and default-construction closure by freezing the complete source files `PartCart.cs`, `MechanicalCatalog.cs`, `AwdHardware.cs`, and `AxleDriveDynamics.cs` before L1A tests and verifying those files remain unchanged through the checkpoint. These files are characterization dependencies; do not edit them in L1A.
2. Cart pairs cover every L0 null/empty/default/order case and separately assert `RawEquals` preserves distinctions while `CartSignatureEquals` has the observed effective results. Include null enumerable's exact exception contract, repeated rows, undefined enum values, and no snapshot output aliasing after caller mutation.
3. Product pairs include same definition, ID-only variation, every field one at a time, exact nested AWD data, null/default nested shapes at every reference level, signed zero, infinities/NaN if accepted, adjacent float values, Unicode strings and null/empty strings. Compare exact support/equality/hash behavior to the frozen JSON string oracle while logging only bounded paths/results, never key bodies.
4. Mutate source rows/definitions and returned sequences after capture; snapshots remain unchanged. Also mutate any returned defensive copy and verify no internal snapshot state changes.
5. The frozen L0 methods and production routes remain unchanged. No JSON counter/key warming is introduced by new value capture/equality, no profile/catalog copy is created, and capture is scoped synchronously on the owner thread. This does not authorize worker transfer, persistence, stale-ticket reuse, `ProductKey` byte changes, stock resolution, or live API redirection.
6. Completion is whole-catalog, not a supported-subset milestone: report the freshly loaded authored `catalog.parts.Count` before probes and separately report the exact number of probe-added alias values; test every base definition and all three required test aliases. Do not assume the `9,296` market-index count is the authored catalog count. Every required serialized field and mandatory edge vector must be supported and match the frozen oracle, with no OPEN or Unsupported product result, before L1A can be marked complete. If any authored value or required edge remains unsupported, keep the entire L1A checkpoint open and continue resolving it; do not complete the cart-row portion alone or pass a filtered/supported subset.

These types are the smallest required infrastructure checkpoint before route-specific Stock/Ledger/Held overlays. Those overlays must continue to model the separate observed algorithms; equality values alone do not pick a stock row. Later changes must also retire all public runtime JSON bridges, including `ProductKey`/`MarketCatalogLayout.Key` and row-signature serialization; an old or permanently warmed JSON cache is not an acceptable final-state exception. Complete non-sale Apply, `Total`, `DeliveryFee`, quote/checkout, caller activation, full S06/Step 1 and S07–S14 remain later gates.
