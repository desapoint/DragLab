# S06d2b step1 characterization — findings in progress

Baseline `d541ad8`. This is **not** a passing characterization checkpoint, completion of step1, approval of a typed planner, or a full S06 result. Production source is unchanged by this increment; new reference/check/isolation code is UNITY_EDITOR-only. No player save is selected or written. Full S00–S14, no runtime JSON manipulation and no full-profile save/preview clone remain binding.

## Actual attempts

| Parent-owned attempt | Result | Scope / evidence |
| --- | --- | --- |
| Editor99697 / initial-editor.log | Exit1, compilation only | Array Count member-group and nonexistent per-stock generation field; no tests run |
| Editor34541 / corrected-editor.log | Exit1, compilation only | Parent trace-digest call used a removed HashText helper; no tests run |
| Editor60108 / digest-fixed-editor.log | Exit1 after26 passing assertions | Actual and frozen mixed Total both260, strict source-immutability FAIL |
| Editor15342 / mutation-diagnostic-editor.log | Exit1, same strict failure | Failure-only complete typed source snapshots and first difference preserved |
| Editor42254 / boundary-editor.log | Exit0, six diagnostic operations complete | Historical diagnostic-only mode; GATES FAIL, not characterization PASS |
| Editor48914 / collect-editor.log | Exit1:112 PASS /15 FAIL | All eight groups run; eleven source-mutation failures plus four null/empty test-preflight failures |
| Editor45739 / collect-preflight-fixed-editor.log | Exit1:162 PASS /11 FAIL | All eight groups complete; no PAIR_SKIP/GROUP_EXCEPTION; only identified legacy source-mutation assertions fail |
| Editor81984 / collect-repeat-editor.log | Exit1:162 PASS /11 FAIL | Byte-identical trace to45739; same strict failures, zero frozen-source drift |

All raw logs, source manifests and isolated run folders remain under local `reports/save-refactor/S06D2B1/`. Failed attempts are not erased or counted as passing gates. The four copied oracle method bodies match d541ad8 exactly before any new planner exists; this is provenance, not an independently implemented candidate.

## Confirmed legacy source mutation

The first observed difference is an inactive dealer row payload: `unit` changes from null to an empty LooseEngineElement with null definition/storage/element and zero unit price. The post-call actual/oracle states match each other, but both violate source immutability. Do not exempt the field, initialize it to hide the defect, or infer that equal totals make the source safe.

The isolated boundary run uses a fresh normalized fixture for each operation:

| Operation | Source changes? |
| --- | --- |
| Unity ToJson only | Yes |
| ToJson followed by FromJson | Yes |
| FromJson only, unrelated fresh source untouched | No |
| Total without Move | No |
| Total with Move | Yes |
| PreviewProfile without Move | Yes |

This localizes the mutation to source serialization in the old whole-profile clone paths, rather than market Ledger consumption or a disagreement between oracle and candidate. One normalized fixture serializes to4,523,408 UTF-16 string characters; the historical diagnostic's `bytes` label is inaccurate; current code correctly labels `chars`. This is evidence of broad work, not a standalone production timing or allocation measurement. Current native94925 already measures410.077ms at the actual Move interaction; the new tests do not claim a performance fix.

## Current next checks and hard limits

Collect-all mode now retains every failed assertion, continues independent groups with explicit dependent skips, and exits nonzero on failures. Editor45739 completes all eight groups with no skip/exception after null/empty preflight correction. Its strict red result is162 passes/11 source-mutation failures; this is not a game fix or step1 pass. Strict mode keeps its first-failure behavior. Current/oracle inputs must be separately isolated from exactly the same pre-call state, so one legacy serialization cannot contaminate the other's starting input. The null-preserving reflection copier exists only for Editor test isolation, never runtime saves/previews; shared authored catalogue values and original source are separately audited on successful and failed preflights. Editor45739 has zero drift across1,042 frozen source/settings files. Its trace SHA-256 is422cfeb051d05b7d6bb4bc9690f07b401ae7c6c9e6dcde865cd44fb855581423 and matches the sidecar. Repeat81984 confirms byte-identical UTF-8/LF trace output with that same digest and162/11 result. This proves reproducibility for the current subset, not full-matrix correctness or source immutability.

Typed comparison includes actual float/double bits, concrete types, nulls, ordered serialized fields/collections, literal existing IDs and scoped new-ID aliases. Original authored vehicle specs/revision and part definitions are observed without Spec()'s JSON copy. The larger input-row/action/quantity identity mapping, all missing matrix rows and actual native caller traces remain OPEN. No shared planning seam/candidate may be advanced merely because this initial subset or diagnostic runs.

The serializer mutation is a required typed-replacement divergence: the eventual runtime path must avoid JSON and preserve original nulls/physical identities/resources. Capturing this legacy defect is useful characterization; it does not fix it or clear the failing source-immutability gate.

## Intermediate commit boundary

Independent Luna-high evidence review recommends preserving this as an intermediate diagnostic infrastructure commit with the red162/11 result. It does not approve Step1 completion, a planner/shared seam, native coverage or full S06. The unchanged-source repeat81984 confirms byte-identical output and the same162/11 result. The small immutable red reference is explicitly versioned at fixtures/cart-preview-legacy-trace-v1.txt; its README records provenance and remaining limits. Future typed runtime operations must satisfy the still-failing source-immutability contract; the Editor reference's known mutation is never an exemption for production. No large raw snapshot, log, generated profile or screenshot belongs in Git; only explicit source/metadata and concise reports/blueprints are versioned.
