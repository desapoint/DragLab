# S06d2b1i — whole-part transition core

## Scope and API

`SharedInventory.Move` now uses a common run/storage preamble and unit → engine → part classifier. Its existing unit and whole-engine branches retain their previous operations. Whole-part operations enter `InstalledPartTransition.MovePartAfterPreamble`; the separate dormant `WholePartMoveProjection.Evaluate(profile, assetId, destination, destinationId, catalog)` runs the same preamble, classifier, and mutation core with copy-on-read state. No UI or cart caller uses this projection.

The projection returns `WholePartMoveResult`: success includes owned copies of the requested part, displaced destination part, explicitly changed part records, and affected owner snapshots; failure includes only exception full type name and exact message. Owner snapshots are source owner first, destination owner second, deduplicated by source object reference. They always include copied owner identity/context, bindings, and tune (plus car fuel/fluids). Optional owner parts, definition maps, and the car H `InspectionBuild` are materialized separately; `Materialized` and `BuildErrorType/Message` record failures without changing the successful move outcome or fabricating missing parts. No source/profile/catalog reference escapes. `ChangedParts` reports records written by the transition core; assigning an already-equal storage ID does not report a changed record.

## Shared behavior

The core preserves guard order and legacy partial-mutation order. It removes through actual owner bindings, retains storage IDs for displaced parts, recalculates the destination car's tune ranks only, and does not recalculate a source car after removal. Engine installs share the existing `EngineElements.Ensure` path in the live adapter. Projection uses `EnsureExisting`, which shares the authored member count and type validation but refuses only when a required member array is null or empty with `Typed part move projection cannot initialize missing engine members.` Nonempty typed arrays and zero-count definitions retain legacy behavior. Projection failures publish no changed graph.

Car definition capture still uses `InstalledPartLookup` and its ordered duplicate/dangling behavior; engine definition capture retains `AssemblyOperations.Definitions` dictionary semantics. The temporary parts-list adapter returns existing requested copies where present and otherwise borrows source records read-only during lookup, avoiding a full inventory copy. Copy-on-read maps parts/cars/engines by source reference, never ID, and releases the profile reference and caches in `finally`.

Public `MechanicsOwnership.Remove/Replace/ProjectRanks` and `AssemblyOperations.RemovePart/InstallPart` now use the same underlying mutation helpers. Public methods taking an `OwnedCar` continue to operate on that exact passed object. The SharedInventory source path retains its first car-by-binding search followed by first car-by-ID relookups for the mutable-car and remove-operation checks.

## Verification status and limits

Source review only. The Editor tests agent and coordinator own compilation and the bounded reference suite; no Unity/build/process/Git checks were run by this author. This is an owning-thread synchronous projection over affected facts, not a retained snapshot, detached catalogue, worker-safe planner, or UI activation. Loose-unit/whole-engine projection, cart pricing/holds/sales/checkout, and broader S06 gates remain open.
