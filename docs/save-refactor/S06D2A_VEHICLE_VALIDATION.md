# S06d2a fresh vehicle listing validation

## Contract

`VehicleListings.Validate` continues to execute every market, disclosure, identity, component, fitment, condition, build, fuel, and fluid check on every invocation. It returns the same first error or exception as the pre-change implementation, and does not mutate the profile, listings, catalog, or authored data. No revision certificate, persistent cache, JSON comparison, or profile/catalog snapshot is introduced.

The measured consumer path invokes this validator afresh for each quote. The coordinator measured `VehicleListings.Validate` at 21.426 ms (full quote validation cost was also reported separately) for two owned cars, 1,081 parts, 9,294 definitions, and the post-purchase full market. A per-invocation lookup memoization candidate was rejected: the preceding three-offer diagnostic was not representative and regressed from 0.905 ms to 0.931 ms. Production `VehicleListings.Validate` has been restored exactly to its pre-candidate body; this increment makes no performance claim or production behavior change.

## Parity and measurement harness

`Runtime/VehicleListingValidationChecks.cs` carries a frozen copy of the unchanged ordered `Validate` body as a parity oracle. Its Editor runner checks exact error strings, acceptance, exception types, and exception messages across valid and malformed inputs, including same-revision listing B→A and authored fitment edits. The diagnostic now follows seed `16102026` through purchase of a generated starter offer, generation of the full market, and purchase of a second distinct new-car offer. It requires the real `firstPurchasePending` transition, a roster larger than the opening three, and two purchased cars. To match the measured validator input size, the isolated harness pads only the profile part list to 1,081 with unique in-memory inventory IDs; it does not persist or use this graph as a whole-profile validation fixture.

On that full market it emits five-sample medians for the actual validator and its constituent operations: profile component-ID set creation, `VehicleInspection.Validate`, `MechanicalCatalog.Spec`, `Find`, per-offer category-map construction, combined `Spec`/`Find`/`Fits`, `ComponentCondition.Validate`, `EngineElements.FaultAllowed`, `Resolve`, fuel/fluid validation, and full `VehicleListings.Validate`. Each stage is isolated for attribution; stage times are not additive or a replacement for the whole-validator timing. The parent should use these results to identify the next dominant shared cost before authorizing any production dependency changes.

The parent-owned isolated Editor verification entry point runs this harness alongside focused Parts checks and writes raw timing into the supplied fixture directory. The updated real-market diagnostic has not yet been run. Native frame evidence and independent review remain coordinator gates.
