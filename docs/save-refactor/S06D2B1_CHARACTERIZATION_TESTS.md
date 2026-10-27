# S06d2b step 1 characterization harness

**Status:** the first successful managed run failed strict source immutability before completing the matrix. The production and frozen totals both returned `260`; the mixed `Total` input graph changed. The source-mutation probe separately observed changes from `JsonUtility.ToJson`, ToJson/FromJson, `Total` with a move, and `PreviewProfile`. These remain failures; the checker does not normalize null wrappers or exclude them. The checker now provides `RunCollectAll` so independent groups can finish reporting while the Editor wrapper still returns nonzero for any failed assertion. The collect-all rerun has not happened yet. Earlier compile errors and the parent’s digest compile error are recorded below. This is test-only, guarded by `UNITY_EDITOR`, and does not switch production preview callers or advance S07.

## Runner and artifacts

`CartPreviewCharacterizationChecks.Run(Action<bool,string> check, string reportFolder)` remains strict. `RunCollectAll(Action<bool,string> check, string reportFolder)` uses the same check callback but continues across independent groups. Each group has an exception boundary; clone or shared-catalog failures produce explicit `PAIR_SKIP` entries for dependent comparisons. The Editor wrapper records every false assertion and throws at completion, so collection does not turn failure into acceptance.

Both APIs write files only into the caller-supplied isolated report folder:

- `S06D2B1_LEGACY_TRACE_V1.txt` is the versioned characterization record. It includes source SHA-256 values for the frozen oracle, fixture, and current cart dependencies; fixed fixture seeds and clock; assertion results; actual input order; outcome or exact exception type/message; identity mapping results; manual invariants; and explicit open gates. No timing samples or per-run generated profile GUIDs are placed in this trace.
- `S06D2B1_METHOD_TIMINGS.txt` holds per-call elapsed ticks and current-thread allocated bytes. These are run diagnostics, not deterministic golden values and not full-frame evidence.

Every paired `Total`, fee, `PreviewProfile`, and `Apply` comparison now starts actual and frozen operations from separate copies made by the editor-only literal clone helper. The helper preserves nulls, exact values and in-graph aliases; it shares only the original authored catalog. Typed preflight checks copied values, separation and graph-reference topology. Query and preview inputs are checked for mutation; `Apply` target copies are expected to mutate and are compared as outputs. Every operation also checks the original fixture, shared catalog, input rows and random state. A shared catalog mutation fails and skips the dependent oracle call. JSON remains in the frozen legacy methods and in the explicit serialization mutation probe; it is not used to make test inputs.

Snapshots and source-difference files are Editor diagnostics only. They do not participate in any production decision. The frozen oracle, fixture, and clone helper source checksums are emitted so changes cannot silently move both sides of the comparison.

## Current automated coverage

The harness currently probes a deterministic normalized starter profile and the following subset:

- normalized `MechanicsOwnership.Ensure` no-op, compared by a typed field walk over serialized profile/catalog data and Unity random state;
- one mixed ordered quote containing a real sale, new set, new units, used part, used unit, and move-to-storage; also a unit-install position when the actual selected vehicle exposes one;
- `Total`, delivery fee, `PreviewProfile`, and ordered `Apply` bridge parity against the frozen baseline methods;
- actual source immutability around quote/preview calls; new physical IDs are checked with a bijection keyed by physical role, while identities already present in the source remain literal;
- manual first-error vectors for null source, empty rows, null row, quantity 0/100, duplicate new offer, earlier invalid quantity before later duplicate, and missing-target precedence over null row-source materialization;
- the 100-line count gate and quantity 99/100 bounds against authored definitions; each result is recorded even if a later stock or arithmetic error occurs;
- reservation lifetime through the ninth completed race and expiration on the following completion;
- one private-copy Ensure healing divergence for null car fluids, with the deliberately incomplete source graph checked for immutability;
- a typed comparator self-check for signed zero, adjacent float values, list order, root/reference identity aliasing, and ordinary-string literal preservation.

The typed comparator records runtime type, serialized field names, null values, list counts/order, enum/integer values, and exact float/double bit patterns. It walks public Unity-serialized fields and fields marked `[SerializeField]` or `[SerializeReference]`, excluding static, readonly, and `[NonSerialized]` fields. Only known physical identity field names can use a role-mapped alias; ordinary strings are compared literally. An uncovered randomized identity reference remains unequal and fails the comparison. JSON is used by the frozen legacy operation and deliberate serialization-mutation probes; actual/oracle test input cloning uses the literal field copier. JSON equality is not used as the typed state oracle.

## Open characterization work

This checker is a subset and does **not** complete S06d2b step 1. It does not yet cover the full multi-car plus detached-engine topology, every authored wheel/tire/brake or multi-position branch, all partial-stock/express and reservation race combinations, duplicate asset/position/destination/member/condition/block error ordering, full Ensure healing divergence inventory, or an actual native `NativeInventoryMoves` callback trace. Native default-clock behavior, exact same-asset row removal and append-last ordering, selected-car inspection, captured callback rejection behavior, and successful staging remain **OPEN**.

The full managed allocation distributions, native frame gate, interaction tests, and complete S06 requirements also remain **OPEN** until the parent runs them and records the actual results. A passing subset or generated trace alone is not permission to implement a typed preview candidate, change a caller, or claim S06 complete.

No static profile fixture is added in this checkpoint. Any tracked fixture needs parent approval of its exact path and versioned provenance.

## Parent pre-run audit corrections

The first compile stops before execution (`initial-editor.log`); the array access and market-generation trace field are corrected against actual types. The first managed run then fails early at the mixed-cart source-invariance assertion (`run-20261027-110003626`). The parent’s serialization boundary probe records JSON and legacy operation mutation cases in `mutation-boundary.txt`; those are source-invariance failures, not accepted behavior. The parent expanded provenance to every pinned direct dependency, both frozen-reference sources, the literal clone helper, and the checker/Editor wrapper. Missing files fail; source hashes are recorded. The trace is UTF-8/LF with a separate SHA-256 sidecar, while timings remain separate. Cross-run stability still requires repeat-run evidence.

The read-only source snapshot includes the serialized profile, all original part definitions, catalogue authoring revision, and the original private authored vehicle-spec set read through explicit Editor-only reflection. It does not call Spec(), whose copied values could obscure raw-bit differences. Index caches/locks are outside the serialized-state contract. Extra self-checks construct negative-zero from bits and exercise a one-ULP double difference.

New-identity comparison currently uses result topology paths. The stronger complete input-row/action/quantity/member identity correspondence is explicitly OPEN alongside native callbacks and the larger matrix; this initial subset cannot close step1 or authorize a planner switch.

Editor34541 also terminates before tests: the parent digest addition calls a removed HashText helper. The corrected sidecar hashes the actual completed trace file through the existing HashFile routine. Both failed compile logs remain preserved; neither is a test result.

Collector 48914 completed 112 assertions successfully and recorded 15 failures. Eleven are strict source-immutability failures from legacy JSON-backed operations and remain red. Four were harness preflight failures: the row-copy check rejected matching null sources and zero-length row arrays, which prevented the intended first-error checks from running. The missing-target/null-row case reaches the existing `Service vehicle no longer exists.` guard before row materialization. The checker now preserves null row-source shape, allocates distinct arrays for every non-null row source including empty arrays, and rechecks the original profile, catalog, and random state after literal-copy success, exceptions, and preflight exits. These corrections have not yet been rerun in Unity; the 112/15 result remains the latest measured result.
