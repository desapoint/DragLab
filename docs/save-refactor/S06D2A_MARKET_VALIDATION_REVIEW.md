# Independent review: Parts-market identity-set loops

## Verdict and scope

The isolated `PartsMarket.Validate` identity-set change passes independent source review, focused parity, full managed regression, and the fresh Windows build. This approves only the loop-based identity-set component and its checker. It does not approve the in-progress Parts UI, typed cart/move previews, the live S06 gate, or later slices.

The production change replaces only the transient nested LINQ identity-set construction in `PartsMarket.Validate` with `ExistingMarketIdentitySet` and `AddPartIds`. The later market-engine validation loop remains unchanged. No validation predicate, profile field, price, ID set, or result is cached or skipped.

## Source review

The new source preflights outer collections in the original LINQ expression-construction order: `parts`, `engines`, loose units, dealer rows, then vehicle listings. Each null source throws `ArgumentNullException("source")` before enumeration starts, matching the LINQ source checks. It then traverses each source in the original order.

`AddPartIds` inserts `instanceId` unconditionally, including null and empty values, then walks condition elements in index order and inserts only non-null elements with non-null/nonempty IDs. Null part/condition/member behavior preserves the old iterator's dereference point. Dealer rows retain `IsPart` before `IsUnit`; unit IDs are added even if null. Null listing/list-part failures occur when the listing sequence reaches them. I reviewed the corrected test expectations against the direct `PartsMarket.Validate` boundary; the checker does not claim this method invokes `VehicleListings.Validate`.

The previous nested LINQ expression is retained as a frozen full-method oracle in `PartsMarketValidationChecks`. It compares returned first errors, acceptance, exception type, and exact exception message. The checker exercises malformed/null inputs, empty sources, ID filtering and duplicate behavior, and cross-source collisions against actual market engine offers, including IDs from owned parts/members, stored engines, loose units, dealer parts/units, vehicle listing parts/members, and offered engine components/members.

## Verification evidence

- Standalone Editor run `42758`: exit 0, 217 checks passed. Fixture: seed 16102026; 2 cars; 1,081 owned parts; 59 vehicle listings; 2,362 listing parts; 4 market engine offers.
- Ten direct full-validator timing samples after one warmup: frozen implementation median 7.8011 ms, max 11.1546 ms; current implementation median 6.2214 ms, max 9.9181 ms. The report explicitly says allocation bytes were not measured. These are directional Editor timings only.
- Source inspection confirms neither direct validator call in the timing window performs JSON serialization or a profile copy. This is source evidence, not an instrumented global serializer-counter assertion.
- Full managed run `63595`: exit 0. Logged checks: 7 encoder, 37 typed-cache, 1,821 vehicle-listing, 81 display-price, 214,037 browser, 13,384 ready-ledger, 1,691 base-readset, 1,615 catalogue, and 37,702 supply.
- Windows build `2041`: exit 0; `Build Successful` and `DRAGLAB BUILD SUCCESS` logged. Focused, full managed, and Windows runs matched the `market-validation-corrected-source-sha256.txt` manifest.

The first standalone attempt `7633` is retained as a failed checker run: the test incorrectly expected a VehicleListings error from direct `PartsMarket.Validate`. The expectation was corrected against the frozen original method; no production source changed for that correction. The corrected standalone run passed.

## Limits

The focused runner did not measure allocation bytes, so the iterator-allocation hypothesis is not quantified. It also does not establish a native frame improvement. Native run `59513` remains a full-frame failure: two frames exceeded 50 ms in the selected bounded prefix, plus five additional failed raw `menu/Parts` rows outside the prior helper labels (frames 2946, 3028, 3063, 3482, and 3551, including the 458 ms stall). Expanded phase scopes are required to cover those interactive tails. Raw nested UI/Parts measurements are not additive. The interactive move-preview path still uses whole-profile JSON copies and remains required S06 conversion work. Keep that frame cost in the live gate.

Full S06, typed nonempty/multi-position preview, owned refresh, human visual acceptance, and S07–S14 remain open.