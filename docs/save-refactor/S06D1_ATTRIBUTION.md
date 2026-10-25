# S06d1 — native refresh attribution

## Scope

This checkpoint adds opt-in timing attribution only. It does not change refresh routing, view keys, menu contents, or consumer behavior. All original fifteen CSV columns remain an exact prefix in the same order; five columns are appended after `unityGCAllocatedBytes`: `nativeOwnedMs`, `nativePartsMs`, `nativeCartMs`, `nativeOtherMenusMs`, and `nativeSurfacesMs`.

The slots measure these synchronous call groups in `NativeMenuShell`:

| Slot | Measured work |
| --- | --- |
| `nativeOwnedMs` | `RefreshOwnedCars(route)` |
| `nativePartsMs` | `RefreshParts(route)` |
| `nativeCartMs` | `RefreshNativeCart(route)` |
| `nativeOtherMenusMs` | Common visible-shell fitting/HUD refresh and dealership start; dealership/tuning, engines/disposal/race/business consumers; route/tab selection when it changes. |
| `nativeSurfacesMs` | Engine-listening tick, Escape handling, and both shared surface tick loops. |

The five scopes do not overlap one another. Existing `raceGUIMs` remains the enclosing `NativeMenuShell.LateUpdate` total, so it intentionally includes these scopes and should not be added to them. The instrumentation returns no claim that these five values account for every path: early reset/race-pause/hidden-interface exits occur before some or all scopes. `nativeOtherMenusMs` is an explicit grouping, not a residual computed by subtraction.

Consumer scopes count every call to `Refresh`, including explicit verification/capture calls and callbacks outside `LateUpdate`. Those calls are not enclosed by the old `raceGUIMs` scope; a consumer total can therefore exceed it in that frame bucket. These columns are not a strict partition of the outer total. `frameMs` is the interval between early probe Updates and can also include startup, screenshot preparation, storage-boundary verification or scheduler waits; it must not all be attributed to the measured menu consumers.

## Instrumentation properties

- Timing remains opt-in through the existing debug-player probe arguments. With no active probe, `Measure` captures no timestamp and writes no cost.
- Cost storage sizes itself from `Names.Length`. `Row` remains a fixed value type with fields for all fourteen measured slots; recording a frame copies scalar values and performs no per-frame array cloning or file I/O.
- Existing frame, phase, nine timing, allocation, and GC columns keep their ordering and meaning. The five consumer columns follow the original allocation columns.
- A scope records its creating probe and writes only if that probe is still the current singleton when disposed. This guards against a replaced probe; it does not claim special behavior for a destroyed object while the static reference remains set.

## Checks and handoff

Static source/header check passed: the original fifteen column names and order remain a prefix, the five consumer names follow them, all five shell scopes are present, costs are dynamically sized, the row has fixed fields, and header/format have twenty columns/placeholders. Unity compilation, runtime measurements, and CSV capture are intentionally deferred to the parent-run actual player before/after attribution. No measured overhead number is claimed here. Parent should use the same deterministic fixture seed and route/actions for both captures, report the new per-consumer distributions alongside `frameMs` and the existing outer UI time, and retain the captures/CSV for high review.

No consumer optimization is included in this checkpoint. The attribution is intended to discriminate cold part-browser construction, synchronous owned-car inspections, cart work, remaining menu consumers, and surface ticking before choosing a narrowly scoped cost reduction.
