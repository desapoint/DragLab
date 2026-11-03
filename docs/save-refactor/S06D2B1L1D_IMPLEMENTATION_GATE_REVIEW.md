# S06D2B1L1D implementation gate review

Status: **limited GO for independent frozen-reference preparation and its focused liveness run; HOLD for all L1D production edits.** The next review must accept that preparation commit and evidence before the complete L1D implementation gate can be considered. No caller conversion or S06/S07–S14 completion is approved here.

## Reviewed frozen documents

| Document | Reviewed SHA-256 |
|---|---|
| [Cart query design](S06D2B1L1D_CART_QUERY_DESIGN.md) | `59352C4CEF6852089E28721C5A3407CF20EC3539053C177A0E09D5315104EECB` |
| [Cart query test design](S06D2B1L1D_CART_QUERY_TEST_DESIGN.md) | `A044E4CDD473021DB3B1A180C14E73C17B43B79DB9A1CC4E11695672297EDEEF` |
| [Implementation method manifest](S06D2B1L1D_IMPLEMENTATION_MANIFEST.md) | `6A69F0E753AB0078DFC4B7566B79EB0E373634D8298853FB2FCFD16DF9E3E2DC` |

These hashes were re-read after release. The manifest is a bounded proposal with explicit pre-edit and runtime gates; its presence does not authorize production changes.

## Independent source-pin checks

The manifest’s full-file hashes match the current source for the query roots and the critical validation/physical files I checked: `PartCart.cs`, `MechanicsOwnership.cs`, `GarageBusiness.cs`, `PartsMarket.cs`, `CartReservations.cs`, `Valuation.cs`, `CarInspection.cs`, `ConcreteInstalledBuildInput.cs`, and `MechanicalCatalog.cs`. I recomputed normalized declaration/body hashes for `GarageBusiness.Validate` (104–109), `MechanicsOwnership.ValidateForQuote` (21–29), `MechanicsOwnership.ValidateCore` (217–298), `ConcreteInstalledBuildInput.FromTransitionFacts` (58–66), and `CarInspection.PrepareCar` (80–87); each matches the manifest. The six public `PartCart` roots are still at their pinned lines and the complete `PartCart.cs` hash matches its pre-edit pin, so those bodies remain unchanged. The proposed manifest correctly preserves the public roots and S07 Quote as out of scope.

## Route and ownership decision

The revised validation design resolves the earlier duplicate-rule concern. Public validation adapters and the typed query path share reader-taking validation cores. Live adapters pass `LiveCartCatalogRead` and retain public catalog/proof behavior; the candidate uses a scoped reader and bypasses proof publication. Explicit catalog and `profile.catalog` remain separate capabilities through nested checks. `GarageBusiness.Validate` factors only its metadata iteration into one shared core with a callback at the old reservation-comparison point: the live callback retains JSON-backed `Matches`; the query callback uses the already prepared R1 `ReservedBuildContext`. `Snapshot`, `Contents`, `Matches`, snapshot upgrade, assignment/finish commands, profile load/save, and `SnapshotEquality` are not edited. The candidate therefore does not maintain a second copy of the business metadata loop or call JSON at query time.

The listed `ValidateForQuoteTyped` route preserves the two-pass dealer-discriminator retry, with shared validation rules and different explicit/profile catalog readers. The sale view remains lazy at the first reached sale and preserves `policy.Validate → quote validation → revision/time checks → display arithmetic`. No sale receipt, cash mutation, quote authorization, or checkout path enters this checkpoint.

## Limited preparation GO

The following independent reference-only work is authorized before any production edit:

1. Add `Runtime/LegacyCartQueryBusinessReference.cs` and its `.meta` as a literal frozen copy of the pre-edit `GarageBusiness.Validate` metadata/order and its `Matches`/`Snapshot`/`Contents` JSON route. Keep its `SnapshotEquality.Serialized` dependency byte-pinned and unchanged.
2. Route the copied valuation oracle through that frozen business helper, so no new oracle path reaches the production validator that L1D will change. Preserve the original reference behavior and all C0/C1/R1 oracle bytes.
3. Extend only the existing `Runtime/CartQueryReferenceChecks.cs` liveness checker with the focused business-oracle vectors, preserving all existing cases and expectedness. Keep the existing `RunCartQueryReferenceLiveness` entry point and runner wiring; add no checker or Editor-runner source. The only source paths in this preparation are the new business reference plus `.meta`, the single call-routing change in `LegacyCartQueryValuationReference.cs`, and this existing checker extension.
4. Run the complete prior query-reference liveness matrix plus the new isolated authored-fixture cases, then commit the preparation checkpoint. Compare the copied helper/valuation route to the unchanged public path and cover: valid and invalid job metadata; hash-match short circuit (including malformed fallback payload remaining latent); hash miss with exact-text success; decoded fallback/tolerance; true mismatch and malformed payload’s actual legacy error; and metadata-failure precedence before reserved-content comparison. Record exact source/body pins, caller route, outputs/errors, source immutability, and actual process/Editor absence evidence. Unavailable required fixtures are failing OPEN outcomes.

This preparation does not authorize adding or editing Domain production files. Its purpose is to make the sale oracle independent before any shared validation methods move to reader cores.

## Production gate remains closed

After the reference-preparation commit, independently review its exact source/meta hashes and liveness results. Only then may the complete L1D production checkpoint be reconsidered against the released manifest. Before edits, root must refresh the complete source/path set and every listed method pin, join each reached validation leaf to the finite provenance rows, confirm the oracle is independent, and stop for review on any unlisted route. The full checkpoint still requires all four query purposes, the exact guard/mode matrix, source-preservation and no-runtime-JSON checks, required regression groups, Windows build, and final review on the exact source.

This decision makes no global project no-runtime-JSON claim, no performance/frame-budget or mobile/device claim, and no S06 caller or Quote/Checkout acceptance claim. The 428-member physical reuse inventory remains unchanged reuse/provenance, not permission to edit physics or metadata methods.
