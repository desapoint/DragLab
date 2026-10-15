# Component fault typing audit — 2026-10-15

Requested follow-up: find other instances of assigning an inappropriate fault to a component, as happened in the corrected tire valuation fixture. This is a domain/tooling checkpoint; no later interface phase advances.

## Findings and changes

1. **Another stale test fixture:** SQLite verification selected the first individual member in the profile and assigned CompressionLeak. That selection depended on asset ordering and could select a wheel. It now explicitly selects an installed piston and checks its actual type before testing persistence.
2. **Actual validation gaps:** the previous shared policy allowed CompressionLeak and Seized on wheel rims, Disabled on tires, and could accept unknown enum values through its fallback. Unsupported categories also matched unrelated concrete member classes. Explicit concrete-type registration and per-category fault lists now reject these combinations.
3. **A real bench permission mismatch:** the bench maintained its own fault list, including the generic Disabled fallback for tires. It now reads the authoritative list. Tires keep their dedicated life/temperature/failure behavior and no longer expose engine-member fault controls.
4. **Validation at ownership/import boundaries:** individual members validate their concrete type's fault permissions. Installed/captured components additionally validate their slot, and dealership cars validate each member against the part definition. Existing owned/stored parts, loose inventory, used parts and offered engines retain their category checks. Invalid state is rejected; validation does not clear faults, create replacement members or heal condition.
5. **An unrelated stale SQLite assertion exposed during the run:** the suite still expected v1/v9 logical saves to migrate automatically. The current per-tire branch intentionally requires explicit reset for these contracts. The tests now verify blocked writes, no fabricated assets and byte-exact preservation of both original and archived sources. Production migration/reset policy is unchanged. Current-contract JSON/compressed imports and actual SQLite schema migrations remain exercised.

## Supported individual faults

None is allowed for every modeled member. Components without individual members continue using their existing aggregate condition/failure state.

| Member | Other supported faults |
| --- | --- |
| Tire | None; its own failed flag, health and consumed life remain authoritative |
| Wheel rim | Disabled |
| Piston | Disabled, CompressionLeak, Seized |
| Connecting rod / bearing | Disabled, Seized |
| Piston ring pack | Disabled, CompressionLeak |
| Valve | Disabled, CompressionLeak, StuckOpen, DroppedValve |
| Valve spring | Disabled, WeakSpring |
| Valve seal / follower / ignition / spark plug / glow plug / injector | Disabled |

Reviewed fault assignments in tracked Editor/runtime/domain/infrastructure C# and searched authored assets/tool scripts for nonzero fault data. The remaining writers target the appropriate components: valve damage from springs, piston seizure/ring leakage in used cars, service/rebuild clearing and correctly targeted verification cases. Intentional negative tests remain negative tests. Existing valid fault effects, prices, identities and wear are retained.

## Checks actually run

| Gate | Result |
| --- | --- |
| ComponentFaultVerification.Run | **3,236 checks, 0 failures** |
| AxleWheelVerification.Run | **541 checks, 0 failures** |
| IndividualEngineVerification.RunStandalone | **879 engine/clutch + 102 OEM/parts checks passed** |
| ValuationVerification.RunStandalone | **4,026 checks passed** |
| Shared mechanics and abuse/replay | **71 + 215 checks passed** |
| SQLite state | **219 checks, 0 failures** |
| Windows BuildPlayerOnly | **Passed; updated `Game/Builds/Windows/DragLab.exe` and companion data** |

The new matrix enumerates every part category against all defined faults and invalid numeric values, checks the bench permission source, rejects wrong-category member insertion and exercises all 13 cars. It verifies owned/loose/dealer rejection, nonmutating failed transactions, seeded car/part/engine markets, valid tire failure and malformed ordinary/exact-continuation snapshots. Its first attempt had an empty newly constructed capture in the test itself; the fixture now captures actual simulation components and verifies the positive baseline before testing rejection.

Detailed local evidence: `fault-types-verification.txt`, `local-faults*.log` and the corresponding manifest results in this folder. Generated historical reports are archived under `local-faults-raw/` before restoring their tracked originals. No real player save is used by these isolated checks. Native mouse interaction is not claimed for this domain/bench permission change.

Pre-existing work preserved and excluded: edits to `reports/maintenance-batch/verification.txt` and `reports/shop-inventory/cart-verification.txt`, plus untracked `Tests.ps1` and `reports/race-exit-regression/`. The required tracked legacy JSON fixture remains unchanged.
