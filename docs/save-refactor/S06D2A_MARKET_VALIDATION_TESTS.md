# S06d2a PartsMarket validation parity checks

## Scope

`PartsMarketValidationChecks.Run` is a focused Editor component suite for the fresh `PartsMarket.Validate` implementation. It contains an independent frozen copy of the complete original public method, including the original LINQ identity-set expression and all public calls. Each parity case compares returned first-error/acceptance and, for thrown cases, exact exception type and message. The checker does not use Unity UI, proof caching, JSON snapshots, or validation-side restoration.

The production change is limited to building the initial global ID `HashSet<string>` with ordered loops and the `AddPartIds` helper. The checker compares against the previous implementation’s full call path, so exception behavior is part of the contract.

## Fixture and checks

The suite builds the real seed `16102026` progression: create the starter market, buy its first starter, buy a second distinct new car, and require a full market with two owned cars. It pads only the isolated in-memory parts list to 1,081 rows using exact copies of a real part and unique IDs. If that seed has no parts-market engine offer, it creates one through the existing production `PartsMarket.CreateEngineOffer` factory using the real catalog and model.

The parity matrix covers:

- Valid full-roster acceptance and market-first error precedence.
- Null source collections, including an earlier malformed row combined with a later null source, to test that LINQ source argument errors happen before enumeration errors and preserve exact exception messages.
- Null rows, null nested part conditions, null unit elements, null inventory, null vehicle stock, malformed listing part collections, empty collections, and null listing rows.
- `PartInstance.ItemIds` semantics: always add `instanceId`, including null; ignore null elements and null/empty member IDs; treat a null element array as empty; and allow duplicate IDs inside the initial `HashSet` until a market engine offer later attempts to add the same ID.
- Cross-store collisions against an actual generated engine offer ID, covering owned part IDs, engine assembly IDs, loose member IDs, dealer part and unit IDs, vehicle-listing part/member IDs, offered engine assembly IDs, offered engine part IDs, and offered engine descendants. Each mutation is restored in `finally` and compared with the frozen oracle.

The suite reports ten-sample median and maximum whole-validator timings for the frozen original and current implementation on the same full-roster fixture. It does not report allocation counts because this Editor runner has no selected reliable per-thread allocation counter. These measurements are component diagnostics, not Unity frame timings and have no pass threshold.

## Execution status

The checker and standalone entry point are wired by the parent task. Unity execution is pending. Do not claim source parity or performance improvement until the focused Editor run succeeds and its report confirms all mutations were restored.