# S06d2b1j — whole-engine projection tests

The Editor-only `RunWholeEngineMove(check, folder)` verifier compares the dormant typed projection with the frozen `LegacyWholeEngineMoveReference` (SHA256 `B4704709F463E06453872AEAE0E1B3B1BC00034D7AAE846579708D85A268F012`). Disposable actual-profile fixtures use the pre-existing literal test clone only to isolate the independent oracle and public live wrapper. Setup detach/attach operations use the frozen methods; multi-car cases use actual owned cars and authored model availability.

## Finite vectors

| Group | Cases |
| --- | --- |
| Valid transfers | Storage→Storage; stored engine→bare compatible car; attached engine→Storage; attached engine A→bare compatible car B |
| No-op | Same parent; same-parent car under garage-job and rebuild unavailability; same-parent missing car with null engine slots/tune/fluids, pinning no target query and raw null preservation |
| Ordered guards | Unsettled run before unknown Storage and separately before missing asset; unknown Storage before missing asset; missing asset; invalid engine destination before detach; missing parent car; car-referenced engine missing |
| Late target failures | Occupied target; actual incompatible model; target garage job; active rebuild; null engine slots causing late `AddRange(null)` argument failure after Attach writes IDs while preserving target fluids/bindings/tune and engine slot/fluid/tune values |
| Transfer facts | Nondefault fluid/tune history and unrelated chassis bindings; null engine fluids/tune into a compatible bare target; missing-fuel optional car query after a successful attach; preexisting engine slots overwritten at detach |
| Malformed ordering | Parent/car-engine disagreement (successful Storage move writes the car-selected engine before the requested engine); invalid slot parse; null car tune after detach writes; late rank-resolution failure |

Every executed request is evaluated against the frozen Move on a literal test-only clone. The public `SharedInventory.Move` wrapper is also run on an independent clone and compared with the frozen result using the full typed profile snapshot, including live partial effects and exact first error. Successful private outputs compare requested/changed engine values, ordered bindings, tune/fluid nulls, actual descendant identities/conditions, and source/destination car snapshots. Failures must return only exact exception type/message and no graph, while preserving all input values and reference topology. The suite records JSON probe, Unity RNG, RuntimeEntropy identity/mode/clock and catalogue stability. Nested output mutations are followed by source and fresh-evaluation fingerprints. Readonly owner fields and map contents use explicit named assertions; Unity serialized reflection is not treated as observing them.

Optional owner queries retain raw bindings when materialization fails. An attached engine reports that its descendants are represented by the owning car before checking a null slot list. Car inspection retains bindings but reports the native H precondition error for missing fluids first, then missing fuel; valid-fluid/fuel inspection remains compared to the frozen installed-build capture. These optional query errors do not change a successful move or allow missing values to be synthesized.

## Limits

This finite family does not cover loose-unit moves, whole-part behavior beyond the retained I gate, cart composition, freshness tickets, caller activation, native frame/device performance, or later S06/S07–S14 gates. Each authored fixture precondition is a failing OPEN if unavailable, never a passing skip. Root owns Unity execution, source freezes, regression aggregation, builds, independent review and Git. No test result or implementation completion is implied by this test plan.
