# S06d2b1d normalization/source-diff contribution

Status: source draft ready for parent-owned frozen-source execution and Luna-high exact-source review. All diagnostic code is `UNITY_EDITOR`-only.

## Scope and API

Private partial helper `RunNormalizationContracts(Trace trace, string subset)` accepts `all`, `normalization`, `source-diffs`, or `ensure`; parent wiring dispatches `all` through the full collector. `ensure` runs the strict valid no-op plus all corruption/migration and optional-null Ensure probes. `source-diffs` runs only the isolated encode/cart attribution matrix. `normalization` and `all` run both groups once. Each deliberate corruption follows `CartPreviewLiteralTestClone.Copy` of an authored normalized fixture. Fixture setup does not call Ensure after corruption and does not manufacture OEM/member records.

The current valid second-Ensure probe is strict: pre-validator must accept; Ensure must not throw; all typed values/order/nulls/reference-path identities, Unity RNG, catalog values/reference must remain unchanged; and the identity observer must close without overflow and record zero GUID emissions. Each separate fault probe records the actual pre-Ensure `MechanicsOwnership.Validate` result or exact exception, Ensure result/exception, exact changed paths/values, object/list reference-path changes, RNG, runtime clock and emitted identity scope/GUIDs. It checks the original fixture after each isolated clone operation.

The finite map retains all 12 previously assigned migration/malformed families, the separate tire-member-ID case, and four optional-null families (17 independent cases total; the service migration marker + removed part is one linked family): old save version; null required profile collection; stale dealer discriminator; null fluids; null fuel; missing actual service part plus its legacy marker; missing array on owner-resolved installed Pistons; missing member ID on owner-resolved installed Pistons; missing fitted tire corners; missing fitted wheel members; missing market; active hold with missing exact supply rows; missing ID on an actual fitted tire member; null AWD tune; null last pass; null custom name; null garage-job ID.

Piston probes select an attached-engine `PartInstance` with the selected car's engine owner ID so `MechanicsOwnership.Ensure` resolves it through the production owner map. `EngineElements.Ensure` fills an absent member array but returns without repairing an ID inside a nonempty array. Tire probes use canonical `condition.tires`; `TireDynamics.Ensure` can seed a four-member array from an untouched empty pack and `MechanicsOwnership.Ensure` assigns missing tire IDs. The wheel probe uses `condition.elements=null`; `WheelFitments.Ensure` fills the four members. These are observed legacy actions, not a passing typed no-heal assertion.

Every optional-null probe is classified only by actual pre-Ensure validation. A validator-accepted shape remains valid; the proposal is to preserve null/current records and leave rule approval open. A rejected missing-required-data shape records its exact validator error/exception; only then is typed no-heal/exact-error the proposed rule. Neither behavior is approved here.

## Isolated encoding/cart attribution

The matrix separately observes ToJson on an isolated literal copy; FromJson from bytes prepared using a different disposable clone; a full To/From roundtrip; production/frozen `Total`; production/frozen `PreviewProfile`; and production/frozen sequential `Total` → `PreviewProfile` Move using an existing vehicle-owned installed part. Each operation gets its own isolated input. The sequence records input after Total separately from final input and returned candidate.

Each operation records exact profile/catalog and ordered-row values, reference-identity paths, returned candidate differences, Unity RNG, `RuntimeEntropy.Capture`, clock ticks, and emitted identity scope/GUID observations. Trace displays no more than 32 IDs; complete changed path/value proofs are local sidecars. Each operation asserts its isolated input/rows and Unity RNG remain unchanged. Any reproduced composite source mutation remains a strict finding. Generated candidate IDs remain literal in output diffs.

## Typed walker guarantees and self-probes

Every nonprimitive node now writes an explicit runtime-type marker at its exact path before descendants; arrays therefore preserve concrete element type even when empty, derived objects with identical fields remain distinct, and null-to-object differs from present-but-empty. Unity object references get a type/presence marker without traversing Unity internals. Cycle markers retain the encountered type. The self-probes cover null→empty object, derived type with equal serialized fields, `EngineElement[]` versus `TireCondition[]` both empty at the same path, aliased versus distinct equal `List<int>` references, signed float/double zero, adjacent one-ULP doubles, and list ordering.

Field records preserve null versus empty, collection count/order/index, strings, enums, integral values, exact IEEE float/double bits, object runtime types, and changed reference paths. Full changed-only values are written locally; console summaries are bounded to counts and a short path sample. Catalog comparison uses the existing typed snapshot. Missing authored fixture shapes produce `NORMALIZATION_OPEN` rather than a shrunk or invented fixture.

## Evidence status and limits

The earlier SourceDiff trace `52 PASS / 10 strict source failures` predates the runtime-type markers and is historical diagnostic evidence only; rerun `source-diffs` after the patch before interpreting its counts. This work does not complete the broader Step 1 matrix, approve typed no-heal behavior, prove runtime JSON removal, implement a planner, or clear S06/S07. Parent owns the source hash, frozen-source Unity rerun, and final result record. No Unity/process/Git operation was run by this author.
