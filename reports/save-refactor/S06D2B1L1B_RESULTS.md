# S06d2b1l1b — scoped stock and reservation checkpoint

Baseline: main `88a9b66`. Status: **bounded checkpoint approved by independent Luna-high review** after focused verification, combined regression and Windows build. This report does not close full S06/Step 1 or S07–S14.

## Implementation and ownership

Luna medium authored the domain implementation and independent diagnostics; Luna high reviewed the contract, source and corrected test expectations. Root owns the frozen reference, runner, execution, manifests and Git. See [contract](../../docs/save-refactor/S06D2B1L1B_CONTRACT.md), [test design](../../docs/save-refactor/S06D2B1L1B_TEST_DESIGN.md), [core](../../docs/save-refactor/S06D2B1L1B_CORE.md), [tests](../../docs/save-refactor/S06D2B1L1B_TESTS.md) and [review](../../docs/save-refactor/S06D2B1L1B_REVIEW.md).

The new dormant `ScopedMarketReadSet` models Stock, Available, public Held, Quote, Consume and the normal Ledger through a synchronous borrowed scope. Stock and Ledger preserve their distinct exact-ID/product precedence, duplicate and miss-cache behavior. Private quantity overlays preserve repeated source-reference identity, hold generations, checked pricing and legacy unchecked quantity arithmetic. Owned results expose no mutable source records; disposal clears borrowed parent, child and catalogue references. No profile mirror, JSON key/round-trip, normalization, gameplay ID, RNG or clock is used by the new operations.

Existing live callers remain unchanged. `PartsMarket.Calculate` changes accessibility only; the existing Calculate, Find and RebuildIndexes bodies are identical after newline normalization. A new nested catalogue lookup shadows index effects without publishing them. The ready-group Ledger overload, cart Apply/Total/DeliveryFee/checkout and all caller activation remain pending.

The independent Editor-only reference intentionally retains legacy JSON product comparison and mutations on disposable fixtures. Its pre-extraction eleven-file closure is recorded in `S06D2B1L1B/pre-extraction-closure-sha256.txt` (SHA256 `4CABE78D2B8EC29B7DA38DC60EA77F7ABCA182AC252F5BB893C2EF197269DB5C`). A compiler correction qualifies the frozen outer Held name; no reference arithmetic or ordering changes. Corrected reference SHA256: `AE960159340F24C10ED32136B6C6F125154A33355AAF5496D2FAFED5DBD09DCE`.

## Verification

Unity `6000.6.4f1`; CLI `1.0.0-beta.15`, checked current before execution. All Game/tools sources freeze during each Editor/build run. Root verifies the complete manifest and actual Editor/player absence before edits or another launch; the resident AppData CLI helper is distinguished from the actual Unity Editor.

| Attempt | Result | Disposition |
| --- | --- | --- |
| A | Compile failure: checker EOF closures missing | No probes ran; source hashes unchanged. |
| B | Compile failure: frozen Held name shadow, missing private COW setter, quote formatter overload | Repaired only those compilation seams; source hashes unchanged. |
| C | 269 pass / 4 fail | Retained failed attempt. Incorrect success-only assertion on an expected error, fixture flag toggles inside a source snapshot, wrong old-reservation stock expectation, and post-disposal observation mistakes were independently reviewed and corrected. |
| D focused | **273 pass / 0 fail**, exit 0 | Complete corrected suite, including retained old-only consumption and an added current-holds plus unreserved-stock case; no case discarded. |
| D regression | **2,591 pass / 0 fail**, exit 0 | L1A 2,441 and L0 150; both traces byte-identical to their previous accepted runs. |
| D Windows | **Success**, exit 0 | BuildPlayerOnly; explicit build-success log at lines 8,147 and 8,163. |

Focused folder: `S06D2B1/scoped-supply-20261102-121730488`.

- Checks SHA256: `5B6B35E8AF707146727F5736778B73F61815144C132980EBDEA7DD1A27CC0483`.
- Trace SHA256: `4B80401E4622B2F0B4E6660E09A9C267427E73F9D58F3A4078512FA53C737A90`.
- Frozen D source manifest: **2,137 paths**, SHA256 `FDA59062A8CF07B95A3AD37F6C492CBADBC19DAE048D92DFA66408AD9CC35413`; every path unchanged after focused execution, regression and build. Full source path-set differences are zero after regression and build. It covers all tracked/new nonignored Game assets, packages, project settings and tools, a broader file set than earlier manifests. Actual Editor/player absence was verified after each terminal exit.

Regression folder: `S06D2B1/scoped-supply-regression-20261102-122005058`.

- Checks SHA256: `8AB65979ABE10D37A20213855777EE58EF06C2745C1E1B5A8490D97647B87EBF`.
- L0 trace SHA256: `E3233662F204C401EF0D953021584ED32C1AAE819984BA39924B0B8C0F04E9D3`.
- L1A trace SHA256: `8A842342BE6B70FD7F00F020A900663634A348E2489C6E586C54BCD00C414902`.

Windows output: `Game/Builds/Windows/DragLab.exe` (ignored local artifact).

- Launcher SHA256: `437691663C5FB21231524F4886E60A10B1841F13E42FBBC93EE39FBD827930CD`.
- Managed assembly SHA256: `D94726CBD130C8BD5C34EF9A1BF530A6BD11A3C3DC92CF7621B50AD32D3F00EB`.

The earlier I/H/J/K and broad Parts suites were not rerun: their live algorithm bodies/callers did not change. Their historical outcomes, including unresolved native frame failures, are retained rather than relabelled as current passes.

## Remaining scope

No native/device frame, allocation, Android or visual acceptance is claimed. This changes no interface, simulation, audio, save publication or transaction caller. Existing S06 native frame failures remain open. Runtime JSON in legacy callers is still work to remove; the completed refactor must have no JSON manipulation or full-profile save/preview clone.

Next: freeze the remaining non-sale cart Apply mutator closure and integrate typed ordered planning against it. Ready Ledger, full cart quote/checkout/sales, dependency/freshness fences, caller conversion and owned refresh, native/device gates and S07–S14 remain required. Unrelated user changes remain untouched.
