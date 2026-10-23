# S02b ownership audit

Baseline reviewed: `62b2bc0`. Static source audit only; no tests or runtime checks were run for this document.

## Current behavior and ownership

`MechanicsOwnership.Definitions` walks `car.installed.slots` in binding order, resolves each ID with `profile.parts.Find` (first matching part), skips missing parts, parses the slot only when a part exists, and inserts with `Dictionary.Add`. Thus duplicate resolved categories throw; a duplicate category whose part is missing is skipped. `Resolve` repeats profile scans for definitions, tire fitments, and condition warnings. `InspectionBuild.Capture` separately resolves and then makes typed `PartInstance.Copy()` values in binding order before it builds the preview. Its requested replacement `instanceId` lookup is a separate profile/dealer lookup and is not part of the installed relation.

`InstalledPartLookup.Capture(parts, slots)` now captures ordered `(slot, borrowed PartInstance)` entries and a category-to-definition map, then disposes its temporary `OwnedPartIdentityIndex`. `CopyDefinitions()` returns a new dictionary; `Dispose()` clears entry references. The API itself does not copy parts or make their handles immutable. The safe integration lifetime is strictly synchronous: use the borrowed entries to resolve/capture condition warnings or call `Copy()` into owned typed records, then dispose before returning, mutating/publishing source state, invoking callbacks, or handing work to another thread. Never retain the lookup or its borrowed entries in inspection results, caches, or tasks.

This preserves actual tire-pack/member identities and condition when the downstream typed copies are made. Tire fitment capture consumes the installed parts in order, chooses the first `Tires` pack, and derives each corner from that pack's individual `TireCondition.definitionId` (falling back to the pack definition). Do not collapse the four corner values or replace member records with averages. Catalog resolution still receives only category/definition IDs; condition and member identity remain sourced from the actual part records.

Attached engine hardware belongs to `car.installed.slots`. A car with no `engineAssemblyId` can still resolve chassis bindings, then receives the existing no-engine error. Detached engine hardware is stored separately in `EngineAssemblyInstance.slots`; `AssemblyOperations.Definitions` currently has distinct behavior (ToDictionary and a missing part dereference) and must not be silently redirected through a car lookup. Existing `InspectionBuild.Capture` returns the cart preview early, so that separate path is also outside this capture change.

## Compatibility edge cases

The old scans return the first part for duplicate part IDs; global ownership validation rejects duplicate identities in valid state. A scoped index that rejects duplicate IDs or null profile entries can therefore change behavior on corrupt input. Treat fully validated ownership as a caller precondition, or document and test an intentional rejection; do not claim byte-for-byte corrupt-state parity without evidence. Preserve the observable binding order, missing-part omission, parse-on-resolved-binding behavior, and duplicate-category failure for otherwise valid inputs. Blank binding IDs currently behave like missing parts in `Definitions`; null slot rows currently fail by dereference. Confirm whether the new helper deliberately rejects null rows and document that boundary. Do not scan or normalize all of `PlayerProfile` as part of this helper.

Borrowed references are safe only while the source graph remains owned and unchanged for the synchronous call. Existing owned-car rating work prepares `InspectionBuild`/`CarSimulation` on the main thread before `Task.Run`; workers consume prepared data. Keep that boundary. Detached settlement candidates may use pure managed preparation only on their isolated candidate, not on the live profile.

## Existing coverage and proposed untested cases

Existing `NativePartComparisonChecks` and `NativeMenuVerification.StatGroups` exercise current/applied inspection parity, worn components, mixed tire behavior and individual corner condition; `NativeMenuVerification.OwnedCars` checks that listening copies retain actual part identity, health, and faults. `NativeWorkshopVerification` covers incomplete attached-engine previews. These are broader behavior checks, not proof of lookup-specific ordering, duplicate, or disposal semantics.

The S02b regression family should add old-scan differential checks for binding order, missing and blank IDs, duplicate resolved categories, duplicate part IDs / null profile rows under the documented validated-state policy, attached versus detached engine boundaries, and no-engine cars. It should compare all four mixed tire definitions and each physical tire ID/condition after inspection capture. Add a mutation-after-capture case proving typed copies are independent, plus a lifetime case proving disposed lookup access fails and borrowed entries do not escape to async work. Existing coverage does not establish these narrow cases; no tests were run in this audit.

No persistence authority, schema, writer, or save-path behavior changes in this checkpoint. This capture is not a profile snapshot and does not authorize new JSON manipulation; remaining JSON copy/fingerprint paths stay in the separately planned conversion work.

Coordinator update: the proposed narrow differential/lifetime cases are now implemented in Runtime/InstalledPartLookupChecks.cs; this audit author did not execute them. Exact actual coverage, timings and any omissions belong to reports/save-refactor/S02B_RESULTS.md, not this static recommendation list.
