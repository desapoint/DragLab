# Independent review: S06d2b1 managed cart characterization subset

## Decision

**Do not approve this checkpoint as a passing characterization run.** The two initial type errors and subsequent undefined `HashText` call are corrected; the trace sidecar hashes the written UTF-8/LF trace with `HashFile(path)`. The source-immutability failure from Editor `60108` is now localized by a separate Editor diagnostic. It reveals a legacy serializer-side source mutation that must be recorded as a defect and removed through a typed projection. It does not permit normalizing or exempting the changed field, weakening the later candidate-side no-mutation invariant, or claiming S06 completion.

Do not mark S06d2b step 1 complete or approve a typed preview candidate/shared seam from this subset. Full Step 1 still requires the committed matrix, actual caller cases and independent baseline review. The full live no-JSON / no-profile-save-clone requirement and S00–S14 plan remain active.

## Source review

- The oracle, fixture factory and checker are wrapped in `#if UNITY_EDITOR`. The Editor wrapper rejects non-batch or Play Mode use and writes under an isolated `reports/save-refactor/S06D2B1` run directory. No profile store or active player save is selected. Production `PartCart` and helper sources are unchanged.
- The frozen oracle exposes only `Total`, `DeliveryFee`, `Apply`, and `PreviewProfile`; it is a test reference, not a player fallback or typed candidate. Current `PartCart` vs copied-oracle parity checks validate only this legacy bridge, not new typed semantics.
- Fixture initialization uses real authored catalog/market operations, a fixed seed and timestamp, one initial `Ensure`, then current global validation. The malformed null-fluids probe occurs after normalization and checks source preservation without normalizing the malformed source. Actual `NativeInventoryMoves.Candidate` default-clock/dialog behavior is correctly left open.
- The typed walker compares serialized field names/types, nulls, list order, enum/integer values, exact float/double bits and literal ordinary strings. Identity aliases apply only to named identity fields and generated physical paths. Self-checks include signed zero, adjacent float values, one-ULP double differences, order/null and ordinary-string preservation. Source and Unity random state are checked around exercised calls.
- The current checker is an explicit subset: one ordered mixed trace, selected manual error vectors, 100-row and quantity 99/100 boundaries, reservation ninth-race lifetime, valid-Ensure no-op, and one null-fluids healing divergence. It does not establish all coverage in `S06D2B_CHARACTERIZATION_MATRIX.md`; the checker marks untested gates open.

## Provenance and snapshot review

`RecordSourceHashes` requires and hashes all 16 pinned domain dependencies plus the oracle, fixture, checker and Editor wrapper. The trace is normalized to UTF-8/LF and has a separate SHA-256 sidecar; timings remain in their own file. `SourceSnapshot` includes serialized profile state, catalog parts, original private catalog spec values read through editor-only reflection, and `AuthoringRevision`, without using the copy-returning `Spec()` API. These changes resolve the prior review gaps.

The checker explicitly leaves **input-row/action/quantity identity correspondence open**. Its current alias mapping is keyed by output topology paths, which is acceptable only as a limited legacy parity aid for this subset. It cannot serve as the complete row-origin mapping required for a future typed candidate.

## Mutation-boundary diagnostic

The isolated Editor diagnostic `42254` completed six probes with these observed source-change results:

- `ToJson-only`: changed
- `ToJson+FromJson`: changed
- `FromJson-only`: unchanged
- `Total-no-move`: unchanged
- `Total-move`: changed
- `Preview-no-move`: changed

The `ToJson-only` snapshot has the same first change as the original failure: dealer row 1's inactive `unit` payload changes from null to an empty `LooseEngineElement`. `FromJson-only` does not alter a fresh source, while serialization alone does; this identifies the JSON serialization operation as the mutation boundary in this fixture. `Total-move` and `Preview-no-move` traverse that serializer boundary.

The diagnostic's `DIAGNOSTIC COMPLETE count=6` confirms only that the probes ran. Its report explicitly retains `GATES|FAIL`, and the original `Total` source-immutability assertion remains a failure. The frozen legacy baseline therefore includes this source mutation. Characterization must report it as legacy behavior/defect rather than claim the old path is immutable. The diagnostic does not close the wider Ensure-healing inventory or full Step 1 matrix.

## Evidence and limits

Editor attempts `99697` and `34541` stopped at compilation; retain both logs. Editor run `60108` compiled, passed 26 assertions including provenance, typed-walker and valid-Ensure checks, then failed `Total` source immutability on the mixed-row case (actual and oracle both returned 260). Dealer row 1 changed from `unit == null` before the call to an empty `LooseEngineElement`; actual and oracle post-call snapshots match byte-for-byte (SHA-256 `8B53509F91A38F4B8E5A918434A5D2F3A151A188C6E9DECB53A34712A27027F2`). Boundary run `42254` explains this as serializer materialization in the legacy path. Neither run passes the characterization gate: `60108` retains the assertion failure, and `42254` is diagnostic-only with a FAIL gate.

The actual native `NativeInventoryMoves.Candidate` clock/callback trace, the full topologies/error/stock matrix, and complete Ensure-healing divergence remain open. No typed planner, caller switch, player behavior, performance, S06 or S07 claim follows from this characterization subset.

No Unity, process, or Git command was run by this reviewer, and no Game file was changed.

## Sequencing recommendation

The proposed collect-all characterization mode is faithful to the approved plan, with these conditions: record the discovered serialization mutation as a failing legacy-baseline invariant and a required typed-candidate divergence; continue only independent characterization groups; retain an explicit failed gate/nonzero final result while any required baseline case is unresolved; and do not advance to the planner/shared seam until the complete S06d2b baseline matrix, actual native caller trace and known-defect dispositions have been reviewed. The later typed path must still satisfy strict source immutability and no-runtime-JSON requirements. The old implementation is not required to satisfy the future typed invariant before it can be characterized, but its observed violation cannot be hidden or waived.

Collect-all must append each failed assertion to the trace before reporting it to the runner, avoid turning unexpected fixture exceptions into apparent passes, and distinguish `FAIL` from intentional `OPEN`/`SKIP`. A failure should skip only dependent subcases with an explicit reason; unrelated groups should continue. The final editor wrapper must exit as failed if any assertion failed, even if diagnostic collection completed.

For valid parity cases, run the current `PartCart` operation and frozen oracle against independent deep-isolated copies of the same pre-call baseline. Snapshot each source before and after its own operation and retain the strict per-operation mutation result. This keeps the actual operation's known serializer mutation from changing the oracle's input and making equality a self-comparison on different states. The isolated copies are test-only and do not authorize JSON/profile copies in the eventual typed implementation.

This sequencing allows baseline characterization to document legacy defects before designing their replacement. It does not declare the UI component complete, make the current source path immutable, authorize a source mutation exemption, or close full S06/S07–S14.
