# S06d2a Vehicle Validation Proof Checks

## Scope

This report describes the focused Editor verification added for the positive certificate around `VehicleListings.Validate`. The frozen original validator remains the independent compatibility oracle (`LegacyValidate`); proof-specific checks exercise the public validator and its explicitly named test seams. The Editor runner loads actual authored resources and changes only isolated fixtures. It does not drive native UI or establish UI acceptance.

## Proof contract under test

- A hit is scoped to the exact `PlayerProfile` object and exact `MechanicalCatalog` object.
- A hit requires a current ready authored-content token and exact current equality of the validation leaf: pending-first-purchase state, stock header and listing order, every offer and nested part/resource/tune/condition/member value, and the ordered owned-part identity sequence.
- Any mismatch retires the prior certificate before unchanged full validation runs. Restoring the old values does not revive that certificate; a later successful core run may issue a new one.
- Only a successful full core result can be certified. Missing or stale authored readiness stays on the full path; validation does not warm the catalog.
- The proof-hit operation must not invoke the scoped JSON or legacy-key encoders. Counters are monotonic and checked by before/after deltas.

## Focused checks

`VehicleListingValidationChecks.Run` retains the frozen pre-change validator and exact result/exception comparisons. It now also verifies:

1. A ready token is prepared before the measured call; the first public validation executes core and publishes a matching certificate.
2. An unchanged next invocation records one proof hit, does not increment core runs, and does not record JSON or legacy-key encoding in the operation scope.
3. A same-revision listing price edit invalidates the proof, preserves the legacy first error, and is not cached. Restoring the prior price still executes full core before a new certificate is issued.
4. A new profile object sharing the original stock and parts references cannot borrow the old profile's certificate.
5. A cold catalog with no ready token validates using core, remains un-warmed by that call, and continues to use core on later calls.
6. A reflection-driven field matrix uses the actual post-purchase, 59-listing market and probes one live instance per runtime type. Float and double probes flip one exact representation bit, so large values such as `wearEnergyJ` are guaranteed to change even when a small arithmetic delta would round away. It requires stock, offer, tune, AWD, fuel, fluid, part, component condition, clutch condition, tire condition, and concrete engine-member types in the fixture. Every public field found on those values, including inherited engine-member fields, must be supported by the mutation probe; unsupported fields fail the check. The pure matcher must reject each changed field and match after restoration.
7. Every entry in `MarketCatalogLayout.AuthoredComparedFields()` is resolved and mutated on a representative authored definition, AWD hardware spec or nested AWD calibration. The ready token must reject each same-revision edit and match again after restoration.
8. Listing, part and history order; owned part IDs; first-purchase state; NaN float/double bit values; null engine members; and available engine-member subtype replacements are checked directly against the pure matcher.
9. Populated and null `ComponentCondition.clutch` shapes are required in the market fixture. A non-AWD listing supplies a compatible `Tune.awd` case; if its source value is populated, the test temporarily sets it to null, confirms frozen-oracle and unchanged-core acceptance, then checks null/populated transitions and restores it. Both ordinary `EngineElement[]` and `TireCondition[]` runtime shapes are required in the market. A valid tire-member sequence is copied into the ordinary array shape and checked against both the frozen oracle and unchanged core, then restored to the typed tire array and revalidated.

The existing old-path parity fixtures continue covering generated starter listings, post-purchase full-roster listings, null and malformed values, non-finite resource values, same-revision authored fitment edits, and shared-model listings. The native-like performance fixture uses seed `16102026`, performs the real starter purchase and second distinct vehicle purchase, then pads the in-memory owned-part list to 1,081 objects; authored catalog size, listing count and listing part count are emitted by the harness alongside five-repeat stage medians.

The proof checker also records one warmup followed by ten measured samples on that same full fixture: unchanged `ValidateCore` through the proof-bypass seam and proof-hit `Validate`. It separately records two one-time normal miss/core/store calls used to populate the certificate. Each distribution reports median and maximum; proof-hit counters and scoped encoder counters are checked around the hit samples. These are diagnostics, with no timing threshold.

## Execution status

The checker is wired into the existing isolated Editor verification runner and a standalone component entry point, `VehicleListingValidationVerification.Run`. Standalone Editor85888 exits0 with1821 checks; exact results and timing limits are in [the component report](../../reports/save-refactor/S06D2A_VEHICLE_PROOF_RESULTS.md). The live native UI/frame gate remains separate and open.
