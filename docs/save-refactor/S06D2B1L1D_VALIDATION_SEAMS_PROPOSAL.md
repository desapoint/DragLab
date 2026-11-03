# L1D sale validation: reader seams and reservation prerequisite

Status: **source findings and proposed scope only, 2026-11-03**. No production edit, checkpoint acceptance, migration, caller cutover or new runtime result is authorized by this note. Read it with the [query design](S06D2B1L1D_CART_QUERY_DESIGN.md), [reference results](../../reports/save-refactor/S06D2B1L1D_REFERENCE_RESULTS.md), [semantic audit](../../reports/save-refactor/S06D2B1L1D_SEMANTIC_CLOSURE_AUDIT.md) and [physical proposal](S06D2B1L1D_PHYSICAL_DISPATCH_PROPOSAL.md).

## Concrete source findings

The Total sale branch lazily creates `Valuations.LoosePriceView`, which validates the entire owner through `MechanicsOwnership.ValidateForQuote` before returning a price delegate. That delegate also retains the existing revision guard. Replacing only the valuation arithmetic would leave the following live catalogue reads and JSON manipulation reachable.

The semantic graph rooted at `ValidateForQuote` reaches 234 source declarations. Its relevant direct catalogue/JSON boundary rows include the validation methods below. This is conservative static reachability, not an executed call count or a complete production edit permission. The analysis uses the already-produced v13a semantic tables; no new Unity run was performed.

| Existing validation seam | Required proposed change or retained rule |
|---|---|
| `MechanicsOwnership.ValidateForQuote`, `Validate`, `ValidateCore` | Thread the scoped catalogue capability through the entire quote-only path. Preserve the false pass, dealer-labelled-error test, true pass, all short-circuit expressions and first-error order. Keep the legacy public adapters unchanged until their own conversion. |
| `VehicleListings.Validate` / `ValidateCore` | Use scoped Find/Spec/Fits/Resolve. Preserve listing validation before economy/career/business validation. A pure query must not publish or invalidate the live `VehicleListingsValidationProof` certificate; an unchanged proof may only be read if its own reads are proved pure. Otherwise use an equivalent private validation path. |
| `SavedSetups.Validate` | Use the same scoped reader for base and per-position wheel/tire definitions; retain slot, fitment, tune and AWD checks. Its finite-float field inspection is validation, not permission for a generic reflective serializer. |
| `SharedInventory.Validate` (three arguments), `ValidUnit` | Thread the reader through loose and dealer units, descendant specifications and the tolerated-discriminator pass. Preserve shared identity-set accumulation, row numbering, duplicate checks and exact errors. Never rewrite the persisted discriminator. |
| `PartsMarket.Validate` | Thread Find/Spec/Fits/Recipe into parts and complete-engine validation, then into reservation validation. Preserve epoch checks, identity-set order, quantities, descendants and complete-engine requirements. |
| `CartReservations.Validate` | Use scoped definition lookup for cart lines and held supply. Preserve clock, owner, active-hold and original-supply checks; do not inherit Total's stronger line validation into Fee. |
| `Valuations.LoosePriceView`, `DisplayLooseTotal`, `PartValue`, `LooseElementValue` | Provide the scoped quote-validation/definition path while sharing unchanged price arithmetic and loose-asset eligibility leaves. Keep policy-first validation, lazy creation, revision guard, buyer/time limits, checked currency rounding and per-position tire/wheel prices. |

`ICartCatalogRead` already has FindDefinition, Recipe, Spec, Resolve and Fits. A new catalogue mirror, source index prewarming, swapping `profile.catalog`, or constructing a temporary profile are not solutions. Added overloads must preserve separate context identities: existing validation explicitly receives one catalogue in some places and uses `profile.catalog` in others. The design must either retain those observed identities or reject unsupported combinations at its documented API boundary, with independent tests; silently conflating them is not parity.

## Active business jobs reveal a separate JSON dependency

The existing call chain is:

`LoosePriceView -> ValidateForQuote -> ValidateCore -> GarageBusiness.Validate (104–109) -> Matches (89) -> Snapshot (82–87)`.

`Snapshot` calls `JsonUtility.ToJson` for fuel, fluids, tune and the ordered fitted parts before hashing their text. If that hash does not match, `Matches` can call `SnapshotEquality.Serialized<T>` (19), which serializes the current contents and decodes the stored JSON, then performs reflective comparison. Empty-job fixtures avoid this branch; they do not prove its removal. Active valid jobs, changed reserved contents and invalid jobs must be covered.

This exceeds the original L1D Game edit allowlist. `LegacyCartQueryValuationReference` currently calls the live `GarageBusiness.Validate`; business/snapshot edits would therefore also invalidate oracle independence unless the exact reached legacy branch is frozen first. No such production edit has been made.

### Proposed prerequisite for review

Prepare a bounded typed representation of **reserved build contents**, not another profile snapshot. Existing serialized reservation payloads can be decoded once at a named load/import/migration boundary into that representation, with their original bytes and validation outcome retained as migration evidence. Runtime quote validation must receive or read the already-imported typed reservation view and compare explicit owned fields without JSON, reflective serialization or a fallback to the old snapshot helper.

Before implementation, independent review must settle the exact lifecycle owner, field authority, existing hash-only reservations, legacy numeric-tolerance semantics, missing/malformed payload behavior, generation invalidation, and persistence/upgrade contract. A cache miss must not silently accept a reservation, parse JSON inside a query, drop a job or heal its contents. The current source must remain runnable until the complete typed route and its independent reference tests are ready. Do not activate S10 business commands or an unreviewed S11 schema as an incidental query fix.

This is a proposed dependency correction for the authorized refactor, not a request to relax its completion gates. If a smaller exact typed route can preserve the same semantics, review that concrete route instead. A new serializer producing legacy JSON under a different name is prohibited by the user's runtime-data requirement.

## Required proof before approving implementation

- Freeze the affected legacy business/snapshot closure independently before changing any shared method; retain its actual short-circuit and numeric behavior.
- Test an active unchanged reservation, changed fuel/fluids/tune/paint/parts, missing/duplicate identities, hash-only and serialized legacy records, and malformed payloads. Preserve error precedence relative to vehicle stock, economy, career and later inventory validation.
- Exercise cold and ready catalogue indexes, positive/stale listing certificates, both dealer-discriminator passes, partially stocked/reserved supplies and different explicit/profile catalogue identities. Assert source values, references, indexes and certificates remain unchanged.
- Count JSON calls across the **entire reached** typed query, including active jobs and malformed controls. Editor-only legacy references may use their frozen serializer; the typed path may not.
- Require the full TotalOnly/FeeOnly/PhysicalOnly/Combined candidate matrix and source-purity checks. Reference B's 730/0 is legacy liveness; its 29 legacy purity failures remain findings, not candidate expectations.

The initial finite reader seam list above still needs exact declaration pins and an approved edit allowlist, including any newly added overloads and new typed prerequisite paths. Until then this is preparation only. Full L1D, S06 and S07–S14 remain open; no runtime JSON manipulation and no full-profile save cloning remain mandatory.

## Existing declaration pins for review

These 22 declarations are the finite reader/price/reservation **review targets**, not an accepted production edit allowlist or permission to change every reachable dependency. Syntax hashes and exact file bytes are independently rechecked against the v13a table. No source was edited. Full S07 transaction Quote/InventoryPricing receipt capture is outside L1D; the semantic Quote root supplies context only.

| Source under Domain | Existing declaration | Lines | Declaration SHA-256 | File SHA-256 |
|---|---|---:|---|---|
| CartReservations.cs | `CartReservations.Validate(DragLab.Domain.PlayerProfile)` | 54–61 | E9F19782448E5BD1FE6E47AF2EA5CC35BCD063B2EEC26F9E90B61EE87F177C9F | 17B8BF9FCA5994AB13569B42BDF38447634FA52BB127CC221AAD667DDEAF7F17 |
| GarageBusiness.cs | `GarageBusiness.Validate(DragLab.Domain.PlayerProfile)` | 104–109 | DC8981E24B5044C8BC49BC71D8FBD2964101257C2BF074A1C5C28D48A8F2BF76 | DD95ED2C2C957B1E0D40C2491079908E9DC3DD0F595B1969EDA4B8FAC1C7F732 |
| GarageBusiness.cs | `GarageBusiness.Snapshot(DragLab.Domain.PlayerProfile, DragLab.Domain.OwnedCar)` | 82–87 | 29F747BC68DB19F44C0BAE3E632EA6C2BF62AB2B347D497A8E3C922EA171F6E0 | DD95ED2C2C957B1E0D40C2491079908E9DC3DD0F595B1969EDA4B8FAC1C7F732 |
| GarageBusiness.cs | `GarageBusiness.Contents(DragLab.Domain.PlayerProfile, DragLab.Domain.OwnedCar)` | 88–88 | 5354B07A52EB947D898A514B032D99A945DA895D3936FF7551B01753D1A87CED | DD95ED2C2C957B1E0D40C2491079908E9DC3DD0F595B1969EDA4B8FAC1C7F732 |
| GarageBusiness.cs | `GarageBusiness.Matches(DragLab.Domain.PlayerProfile, DragLab.Domain.OwnedCar, DragLab.Domain.Mechanics.AssignedGarageJob)` | 89–89 | 06773E7A010152608FD6E81BFC34355AC4D39154F1531E3ED1768F333B616A6B | DD95ED2C2C957B1E0D40C2491079908E9DC3DD0F595B1969EDA4B8FAC1C7F732 |
| MechanicsOwnership.cs | `MechanicsOwnership.ValidateForQuote(DragLab.Domain.PlayerProfile, DragLab.Domain.Mechanics.MechanicalCatalog)` | 21–29 | 92EABC1AE7DCFB68BFE434061E8A54143C03989B45C97E4C42A0EE1A0BE06DEE | 26E58FADB059138250F6E1AD801CB12564BCED39640BC7C67330EA19AE2B039B |
| MechanicsOwnership.cs | `MechanicsOwnership.Validate(DragLab.Domain.PlayerProfile, DragLab.Domain.Mechanics.MechanicalCatalog)` | 214–215 | 3C3BCDBBD9032A7B7E7523A627692F8D7072508EC873E720BC37B1194D9ADC9F | 26E58FADB059138250F6E1AD801CB12564BCED39640BC7C67330EA19AE2B039B |
| MechanicsOwnership.cs | `MechanicsOwnership.ValidateCore(DragLab.Domain.PlayerProfile, DragLab.Domain.Mechanics.MechanicalCatalog, bool)` | 217–298 | 1FD7BC920BFF0DED117FC84A99F2C38A4CD3F87A13E264589B1CA68C903C62DF | 26E58FADB059138250F6E1AD801CB12564BCED39640BC7C67330EA19AE2B039B |
| PartsMarket.cs | `PartsMarket.Validate(DragLab.Domain.PlayerProfile)` | 474–484 | 84976BC7965EC9661E376E2BED7E878EC84C0200985654FC097857EA084B3879 | A7EDB1055D4C5B60BD3E97413023C207D199C2B05C1FBC6D2A4B353587FB235E |
| SavedSetups.cs | `SavedSetups.Validate(DragLab.Domain.Mechanics.SavedVehicleSetup, DragLab.Domain.Mechanics.MechanicalCatalog)` | 132–146 | 1FAF19A2D927FC62FEF10783F71612ADED35F5D52BD028E67EB6FF56C60834FF | 21B70EC86562F023C03BFA6A590A81E074FFB92225DDC1522A267D0246BD3DC4 |
| SharedInventory.cs | `SharedInventory.ValidUnit(DragLab.Domain.PlayerProfile, DragLab.Domain.Mechanics.LooseEngineElement)` | 150–155 | 2CD2EAD608BA716D0E202E13E83D7CE7580FFAFB7F4B16CB454F1ACD9F7A74E2 | 8D85CF724547D48A6F1A9A2E58C35EE6A8B97AF087AE699C1F6DB85CE9B9E5A5 |
| SharedInventory.cs | `SharedInventory.Validate(DragLab.Domain.PlayerProfile, System.Collections.Generic.HashSet<string>, bool)` | 109–149 | A210EDDD072831C3084FEA269C8C9ED50E264DA53838943C1EF7856DABF92C5B | 8D85CF724547D48A6F1A9A2E58C35EE6A8B97AF087AE699C1F6DB85CE9B9E5A5 |
| SnapshotEquality.cs | `SnapshotEquality.Serialized<T>(string, T)` | 19–19 | 923C403BEDFD93142848EC6053826FC399750145415E7D9FBBD08B10B75CD2C4 | A7DBAF5C217A62D950D37646B933F1B23DBCCE6CFABAE20D3A23DD8820FDBA80 |
| SnapshotEquality.cs | `SnapshotEquality.Same(object, object, string)` | 20–31 | B782BC0BABDCAAAB86113ABC4B5E325D309B28AED7FF7BEAE820E42AE34A03EE | A7DBAF5C217A62D950D37646B933F1B23DBCCE6CFABAE20D3A23DD8820FDBA80 |
| Valuation.cs | `Valuations.PartValue(DragLab.Domain.Mechanics.PartInstance, DragLab.Domain.Mechanics.PartDefinition, DragLab.Domain.Mechanics.ValuationPolicy, DragLab.Domain.Mechanics.BuyerPolicy, System.Collections.Generic.List<DragLab.Domain.Mechanics.ValuationLine>, DragLab.Domain.Mechanics.MechanicalCatalog)` | 112–152 | 0A08E927C7FB3A6CFB4CC45A9B7C1B176648326CE836B0D2998FB21649A3B50D | 41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1 |
| Valuation.cs | `Valuations.LoosePriceView(DragLab.Domain.PlayerProfile, DragLab.Domain.Mechanics.ValuationPolicy, string, long)` | 162–167 | 17A0A955AE7AC69B926ED1C4B469A9281DCE56AC048D6541255DAD30EDCAF5FB | 41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1 |
| Valuation.cs | `Valuations.DisplayLooseTotal(DragLab.Domain.PlayerProfile, DragLab.Domain.Mechanics.MechanicalCatalog, DragLab.Domain.Mechanics.ValuationPolicy, DragLab.Domain.Mechanics.ValuationKind, string, string, long)` | 168–187 | 31C76CF32BA64BD8C02663F22E767892C4BFCF63334D1FC8E742FE1273D75B4E | 41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1 |
| Valuation.cs | `Valuations.LooseElementValue(DragLab.Domain.Mechanics.LooseEngineElement, DragLab.Domain.Mechanics.MechanicalCatalog, DragLab.Domain.Mechanics.ValuationPolicy, DragLab.Domain.Mechanics.BuyerPolicy, System.Collections.Generic.List<DragLab.Domain.Mechanics.ValuationLine>, out bool)` | 188–212 | A003B34BAC4E6C55DB51AD70AADA6EA9CC8DAD3A8397E242834BC0FB1C9BA747 | 41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1 |
| Valuation.cs | `Valuations.LoosePart(DragLab.Domain.PlayerProfile, string)` | 110–110 | 0871B8467C5A020538681D1C35B0E098A152F55C4CBAE5F4D20F4C7654ECAE31 | 41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1 |
| Valuation.cs | `Valuations.LooseElement(DragLab.Domain.PlayerProfile, string)` | 111–111 | 63C17D4F2F37CA4670FA05B6467F2EC42E818588FD6A814A1464D8C992A318D1 | 41D6F02366E0C6FE7C6B303937BE33CA56F8A5B5494567C90F13047D457180C1 |
| VehicleListings.cs | `VehicleListings.Validate(DragLab.Domain.PlayerProfile, DragLab.Domain.Mechanics.MechanicalCatalog)` | 89–96 | 929B476ECEF48855B39C3A2121668FC9E29E28AC85731DFF3324B3DD93826F24 | FAB879227D1003690CC83193CA5E4E69C66B1247CF67634299162597C6AC7250 |
| VehicleListings.cs | `VehicleListings.ValidateCore(DragLab.Domain.PlayerProfile, DragLab.Domain.Mechanics.MechanicalCatalog)` | 97–99 | 6A57F9DCCF4974C7803A69F6347BB9C58282C249475953A48EADD47012388B82 | FAB879227D1003690CC83193CA5E4E69C66B1247CF67634299162597C6AC7250 |
