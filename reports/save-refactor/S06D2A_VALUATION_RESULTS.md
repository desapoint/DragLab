# S06d2a pure valuation component

This checkpoint does not activate the pending Parts UI. Full S06d2a and the ordered S00–S14 refactor remain open. In particular, there is no native frame-time, Android, complete JSON-retirement or complete save-clone-retirement claim.

## Change

`Valuations.LoosePriceView` preserves its existing API, complete fresh validation, current-record resolution, revision rejection, error behavior and checked integer rounding. Display amounts use the same scalar part/member arithmetic as full valuation receipts, without allocating receipt explanations. It introduces no price cache, stale revision certificate or profile snapshot.

`MechanicsOwnership.ValidateForQuote` no longer JSON-copies the player profile to recover stale dealer metadata. The strict first validation remains. A dealer-specific first error permits a second full read-only validation that tolerates only disagreement between `isUnit` and the sole populated merchandise identity. Regular validation stays strict. Missing/conflicting payloads, invalid condition, negative prices, duplicate assets/descendants and other errors remain rejected. The source is not normalized or mutated.

The development encoder probe attributes explicitly instrumented calls to synchronous thread-local scopes. It is supporting instrumentation, not a claim that every runtime serializer has been converted or intercepted. Background authored warmup, full payment receipts, later command families and unconverted browser paths remain explicit work.

## Evidence

| Run | Result |
| --- | --- |
| Full managed12204 | Terminal exit0:7 encoder,37 cache/query,54 listing-validation,81 display-price,188567 browser,13370 ready-ledger,1693 base-readset,1609 catalogue-group and37704 supply logged checks PASS. The component's7/81 checks are a subset; other UI source remains uncommitted. |
| Existing valuation regression20516 | Terminal exit0,951 checks PASS. Existing `ValuationVerification.RunStandalone` constrained by `DRAGLAB_VALUATION_MODELS=starter-hatch,sport-coupe,utility-pickup`, with output redirected under local `S06H1/valuation-regression`. Covers whole-car/chassis/engine/member quotes, actual wear/fault/history, retained assets, reservations, same-revision changes and invalid inputs. This is three representative models, not the full roster. |
| Standalone component92570 | Terminal exit0,7 encoder and81 display-price checks PASS. `NativeDisplayPriceVerification.Run` depends only on the component checks, not the pending native browser implementation. |
| Windows40468 | Terminal exit0, Build Success. The current-worktree build includes pending UI work; independent source review separately checks that the staged component has no dependency on those uncommitted callers. No isolated-checkout build or Android acceptance is claimed. |

Raw logs, fixtures and measurements remain ignored under `reports/save-refactor/S06H1/`. Diagnostic JSON snapshots in tests are external verification boundaries; they never restore or manipulate production state. Metadata, UTF-8 and GUID checks pass for the new source assets; staged checks and independent component review still precede commit.

## Performance limits

The complete fresh validator is still expensive. Corrected five-sample diagnostics cover59 real post-purchase car listings /2362 listing parts, two purchased cars and a validator-only1081-part input shape: full listing validation20.371 ms, Resolve9.203 ms, condition validation2.593 ms, combined Spec/Find/Fits2.355 ms. Separate stages are not additive. The attempted per-call dictionary optimization showed no useful gain and was removed; production `VehicleListings.Validate` is unchanged.

The previous native39283 run still fails after107 passing checks at a test hold, with two populated-Inventory frames above50 ms. That evidence predates dealer-copy removal and the latest hold correction. It remains a failed historical live run, not validation of this component or the activation. The live checkpoint must still complete its native interactions, frame gate, captures and high review before its own commit. Typed nonempty/multi-position previews, owned refresh, S07–S14 and the final no-runtime-JSON/no-full-profile-save-clone gates remain mandatory.
