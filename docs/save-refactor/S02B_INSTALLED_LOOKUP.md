# S02b installed-part relation lookup

Status: scoped helper and coordinator caller integration are implemented; actual checks and final review remain the gate. This increment covers only the installed-slot-to-owned-part relation. It adds no canonical ownership authority, global/profile-revision cache, normalization, missing-part repair, or new full-graph copy.

## Baseline lookup behavior before S02b

`MechanicsOwnership.Definitions(profile, car)` in `Domain/MechanicsOwnership.cs` walks `car.installed.slots` in order and calls `profile.parts.Find` for each `partInstanceId`. A missing part is skipped. For each found part it parses the binding's `slot` as `PartCategory` and calls `Dictionary.Add`; an invalid category throws and duplicate categories retain the existing `Add` exception. It does not repair owner/location metadata.

`MechanicsOwnership.Resolve` builds that definition map, calls `MechanicalCatalog.Resolve`, then repeats part-list lookups to capture tire/wheel fitment specifications and append part-condition warnings. A missing engine remains a reported condition; no OEM parts are fabricated. `CarInspection.Capture` separately builds the definition map, repeats binding lookups, skips missing parts and copies only the found actual parts in binding order before creating its inspection build. The coordinator now integrates these consumers with one shared capture per invocation; the helper author did not edit those callers.

## Frozen API

`DragLab.Domain.Mechanics.InstalledPartLookup` is an internal sealed disposable scope:

- `Capture(IList<PartInstance> parts, IList<SlotBinding> slots)` returns one entry for every binding row in original order.
- `Count` returns the captured binding-row count while active.
- `EntryAt(int index)` returns an `Entry` value with readonly `Slot` and nullable borrowed `Part` fields. `Part` is the exact actual `PartInstance` matched by ID; a missing/blank binding ID yields a null part entry.
- `CopyDefinitions()` returns a small independent category-to-definition dictionary for the caller's planner/resolver. It parses the binding slot and calls `Add` only for a found part, preserving missing-part skips, invalid-category failure, and duplicate-category failure.
- `Dispose()` clears captured part references and the internal definition map. Access through the scope after disposal throws `ObjectDisposedException`.

Capture collects distinct nonblank binding IDs for its index request, then builds one index. When the distinct ID count is at most the parts count, it uses `OwnedPartIdentityIndex.BuildRequested` and stores only requested positions. If distinct nonblank binding IDs exceed the parts count, it falls back to one strict full `OwnedPartIdentityIndex.Build`; this bounded malformed/incomplete-graph case preserves the legacy behavior of treating absent bindings as missing rather than rejecting the request-size mismatch. No nested index is built within a capture. Repeated binding rows remain repeated ordered entries; the request set is deduplicated only for lookup efficiency.

No missing `PartInstance` is created or inferred. The caller remains responsible for validated ownership topology and unique source IDs. Sparse-index prefix checks do not certify an unscanned source tail. The helper also does not validate or normalize the found part's owner, location, category, condition, or slot beyond the legacy category-map behavior. Null `parts`/`slots` and null binding rows are rejected explicitly; a null source part or duplicate requested source ID is rejected by the underlying index when encountered. Invalid slot text is parsed only when that binding resolves to a part, matching legacy behavior.

## Lifetime and ownership

The view captures binding-slot strings and borrowed references to actual mutable parts. It does not retain the source list, copy condition/member graphs, call JSON, or use Unity APIs. It is not an immutable worker snapshot. Use it synchronously on the owner of the supplied candidate (main for live profile state; a worker may only use its already-private detached candidate). Do not allow an `Entry.Part`, entry array, or copied map to escape the capture operation's intended local scope. Dispose before any structural/identity mutation, callback, publication, or candidate handoff. Do not cache by profile revision, list count, or binding count; public mutable fields do not advance a reliable generation.

An intended `Resolve` caller builds one capture, copies definitions once, resolves the catalog build, then uses the same ordered entries for fitment capture and per-binding warnings before disposing. `CarInspection.Capture` builds once, copies definitions once, and copies only found `Entry.Part` records in entry order before disposing. The returned inspection components remain that existing capture's owned copies. Preview replacement lookup and unrelated engine/storage relations remain separate and out of scope.

The coordinator has changed `MechanicsOwnership.Definitions`, `Resolve`, and `InspectionBuild.Capture` to consume this helper; technical completion still requires their independent parity checks and review. It does not remove the remaining profile-level JSON bridge or full-profile clone. Those final hard gates remain open.

## Coordinator integration status

The author-only status above predates integration. Current Definitions and Resolve each capture once; Resolve reuses entries for synchronous fitment/warnings. InspectionBuild.Capture uses one view to build the definition map and typed component copies, then disposes it before subsequent preview work. Independent harness and actual result/limits are tracked in reports/save-refactor/S02B_RESULTS.md. No runtime execution is claimed by the helper author.
