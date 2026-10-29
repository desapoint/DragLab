# S06d2b1h — concrete installed-build core

## Scope

Added dormant `ConcreteInstalledBuildInput.Capture(PlayerProfile, OwnedCar)`, `.Resolve(MechanicalCatalog)`, and `.Inspection(MechanicalCatalog)`. No cart, UI, save, checkout, or worker calls the new capture. The caller supplies the catalogue for each synchronous evaluation; the input stores no profile, car, catalogue, Unity object, borrowed collection, or delegate.

The private input owns the selected car/model/context identifiers, copied tune, copied definition map, and ordered per-binding slot/physical-ID/component copies. `BindingCount`, `ResolvedPartCount`, and `MissingBindingCount` expose only scalar counts; missing means a blank or unresolved physical target, retained in its original ordered row. Fuel is retained as its legacy starting litres; fluids and every physical component's condition/member/tire data are copied without normalization. Resolution and inspection return fresh mutable output objects and do not expose the input's dictionaries, arrays, tune, fluids, or components.

`InstalledBuildResolution` is the shared synchronous authority around unchanged `MechanicalCatalog.Resolve`: tire fitment capture, reservation/missing-engine context, and failed-part/tire diagnostics. `MechanicsOwnership.Resolve` continues to use its borrowed `InstalledPartLookup` entries and copied definition map, with no new component copy. `InspectionBuild.Capture` retains its existing copied component list and all override, remove, healthy, and cart branches; only its ordinary installed-build resolution/diagnostic block now calls the extracted helper. The two routes keep their prior error text and ordering.

## Capture and limits

Capture never calls `MechanicsOwnership.Ensure`, JSON encoding, ID generation, or defaults. A null binding row is refused by `InstalledPartLookup.Capture` with its existing exception. Blank, missing, and dangling binding IDs remain ordered with no synthesized component; resolved definitions/components are copied only from actual installed records. `Resolve(catalog)` can evaluate without fluids/fuel because it does not inspect simulation resources. `Inspection(catalog)` runs shared build resolution first, then explicitly refuses missing fluids with `InvalidOperationException("Inspection requires captured fluid state.")`, followed by missing fuel with `InvalidOperationException("Inspection requires captured fuel state.")`. This is a new-input precondition; public legacy `InspectionBuild.Capture` retains its original null-dereference behavior and is not changed to default or heal these fields.

The owned input's retained facts scale with installed binding and component/member count. Capture still uses `InstalledPartLookup.Capture` to locate parts in the profile list. Its sparse requested-ID index scans the list prefix through the last requested match, and may scan the whole list for dangling targets; this increment does not claim capture cost is independent of unrelated parts. The explicit catalogue is borrowed only while resolving on the caller thread. No detached catalogue or worker-safety guarantee is introduced.

## Verification status

Source review only. Unity compilation, the independent Editor reference comparisons, parity cases, Windows build, and runtime probes are parent-owned and have not been run by this author. This dormant prerequisite does not establish cart-projection parity, remove the legacy preview serialization, or close S06.
