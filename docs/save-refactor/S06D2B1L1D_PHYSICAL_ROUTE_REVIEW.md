# S06D2B1L1D — physical route review

Date: 2026-11-03. Baseline `e261080`; reference-preparation checkpoint `38bbdd3`.

Decision: **bounded physical reuse route documentation is accepted for preparation.** The input-assembly edge is now explicitly pinned and joined to the candidate route. This approves no physical candidate implementation and does not close the sale-validation or reservation prerequisite.

## Reviewed evidence

- [Physical closure audit](S06D2B1L1D_PHYSICAL_CLOSURE_AUDIT.md), SHA-256 `9519F249DE2C48E45FDCAA51D8BC9E167C6FF85BB5ECEE3F921F1D444C652325`.
- [Physical dispatch proposal](S06D2B1L1D_PHYSICAL_DISPATCH_PROPOSAL.md), SHA-256 `6D865902B3C58553AE2FEA629631C01C8AFDCB0EBF6CA79A02C2F65EF8725937`.
- [Semantic closure audit](../../reports/save-refactor/S06D2B1L1D_SEMANTIC_CLOSURE_AUDIT.md), SHA-256 `34F92DB5E6B64F5ED688EC2CC75384B3EFC593BA28CFA960DA16781E93D2B77A`.
- [Reservation validation proposal](S06D2B1L1D_VALIDATION_SEAMS_PROPOSAL.md), SHA-256 `C9616F4AFE52B363AEEEB8EE9D481EC39CDCDF07E65B4A49A9FEC732A0E8FEF2` and [reservation test design](S06D2B1L1D_RESERVATION_TEST_DESIGN.md), SHA-256 `4B4AB88389A5D2B70B3DFDD1F2A550AD20F84008B437925549986B5C1AA44895`.
- The compact physical inventory contains 428 member rows and 43 file rows. I recomputed the 43 current source-file hashes against `PHYSICAL_REACHABLE_FILES.tsv`: zero mismatches and zero missing paths.

## What the route establishes

The audit correctly separates the old reference from the future candidate path. Frozen `PartCart.Preview` returns an `InspectionBuild` through its local capture and resolution; it does not run `CarInspection.CalculatePrepared`, construct `CarSimulation`, or prove the full requested inspection DTO. Reference B therefore cannot stand in for physical-calculation parity.

The proposed unchanged reuse path is appropriately concrete: scoped catalogue lookup/resolution and `InstalledBuildResolution.Resolve(ICartCatalogRead)` feed an owned `InspectionBuild`; `CarInspection.PrepareCar` and `CalculatePrepared` then use the authoritative physical calculation. The semantic inventory pins the 428 declarations across 43 files as unchanged reuse. Candidate edits are limited to explicit typed input/output and catalogue/state boundaries; the 428-member inventory is not blanket edit permission.

The candidate must preserve the real call arguments that determine behavior: `PrepareCar` supplies the unassisted inspection `DriverSettings` and applies the captured starting fuel/fluid state; `CalculatePrepared` receives the requested `runProbe` and a null `steadyOutput` delegate so the existing authoritative curve/probe path runs. No static estimate or alternate callback is evidenced as equivalent. The returned `CarInspection` and `InspectionBuild` are operation-local inputs; only the named owned DTO fields may escape.

The audit's 154 physical OPEN dispatch records do not all require individual source rewrites. The report classifies the project-specific virtual targets as `EngineElement.Failed` and `TireCondition.Failed`; the exact base/override declarations and sealed runtime registry are pinned in the dispatch proposal. Candidate validation must retain those routes and test their worn/failed and malformed states. The remaining inventory includes enumerable/disposal dispatch, enum formatting, a conditional `Func` callback, and reflection metadata. The candidate path can bound these without broad framework edits: use concrete owned arrays/lists, pass no steady-output callback, and exclude the conditional catalogue fallback. The reached `FluidState.Validate` reflection is existing finite-field validation on copied fluids, not reflection-based snapshot capture; preserve and test its current outcome. Do not count static exclusions as proof that a future callback, empty sequence, or malformed member cannot reach a fallback.

The physical dispatch and test-gap documents correctly require the two fallback guards: resolved `spec.hasInstalledParts` must be true, and simulation input components must be a non-null concrete sequence, including an explicit empty sequence for a legitimately empty installation. Otherwise `MechanicalStress` can load authored catalogue JSON or fabricate OEM simulation parts. Existing component completeness must be checked before simulation because `EngineElements.Ensure`/`ClutchHardware.Ensure` operate on the simulation's private copies. The private copy boundary does not permit healing a persisted blank member/tire shape or changing an existing identity/condition.

## Route correction checked

The updated closure audit now pins `ConcreteInstalledBuildInput.FromTransitionFacts(...)` at `Game/Assets/DragLab/Scripts/Domain/ConcreteInstalledBuildInput.cs:58–66`, normalized declaration SHA-256 `6E4CB9055CD1E108FBC1A34FA5880B322AF51989AE49E49482CBA54824868B75`, file SHA-256 `977A56E9A266909F07CAAB765302C8BE5EC58160FFC4DCA2EC27C79A6B13D1A8`. I confirmed the method signature and body against the source. It receives explicit transition facts, deep-copies component parts, tune and fluids, copies the definition map, preserves component order, and captures fuel as presence/litres. The audit connects this owned input through `Inspection(ICartCatalogRead)` and `InstalledBuildResolution.Resolve(ICartCatalogRead,...)`, with `ResolveCore` setting `hasInstalledParts=true`.

The null-components fallback in `FromTransitionFacts` remains a risk boundary: the candidate must validate required capture before calling it, so a missing component capture cannot become a legitimate empty installation. A genuinely empty installation must pass an explicit non-null empty sequence. The proposed route also correctly excludes the public profile/catalogue capture and index-publication paths. The sale-value row now distinguishes L1D `Total`'s lazy `Valuations.LoosePriceView` from S07 `Quote` receipt capture, without authorizing either route to bypass Total's reached validation.

The route is accepted as a finite documentation/preparation boundary. Its candidate test still needs to compare ordered projection-derived input with independent frozen results, cover empty and detached builds plus installed wear/faults, assert aliases/order/typed DTO fields, prove no profile/catalogue/object references escape, and reject malformed existing shapes before private simulation preparation. A genuinely new C1-created part must follow its authored creation path.

## Boundaries and next gate

`PhysicalOnly` must not run Total sale validation to avoid its active-business-job JSON dependency, nor may Combined suppress Total's own validation. The reservation proposal separately describes the typed import prerequisite and its unresolved lifecycle/legacy-payload contract. This review does not close that path.

**GO for bounded physical-route documentation and reference-preparation planning. HOLD all candidate source edits until the complete finite method allowlist and independent reservation contract are approved.** No physics helper edits, simulation run, player build, candidate purity/parity result, caller conversion, or S06 acceptance is approved here. Full S06 and S07–S14 remain open.
