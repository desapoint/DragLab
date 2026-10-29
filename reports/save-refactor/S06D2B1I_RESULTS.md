# S06d2b1i — whole-part transition verification

Baseline main `17c03a9`. This is a shared production transition extraction plus a dormant private typed preview seam. No new cart/UI caller, detached worker plan, ticket/freshness gate or persistent command family is activated. Full Step1/S06 and S07–S14 remain open.

## Run provenance

- Unity 6000.6.4f1; Unity CLI 1.0.0-beta.14, confirmed current with self-update check.
- Source-a: 1,556 distinct paths, manifest SHA256 `F39ACFA72521BAA53E5898ABA1B652771680A85FAF3EEEF9C112DCC9327B9644`. `focused-a.log` exits 1 before test execution: eleven unique Editor-only compile errors (one accidental collection-call rename in the frozen extraction; ten reference-map type/comparison errors in the harness). All 1,556 source hashes remained unchanged after terminal exit. No test result is credited.
- The reference correction restores literal `List.Remove` and changes its SHA256 to `30A75892AA306E8A061454019DEC4AEA9A211569DF0F99B63F9CB197DC76F2BF`. Test-only repairs preserve reference identity through the existing per-key reference diff and make preparation/expected owner maps independent of the new core. No production file changed between a and b.
- Source-b: 1,556 distinct paths, manifest SHA256 `9112AA0B938B851F315E5F4AE731B15A27C45C55D1602ECD09F7C080A9CFD17B`. `focused-b.log` exits 1 with 356 PASS /17 FAIL, folder `S06D2B1/whole-part-move-20261029-114240565`. All 1,556 hashes remain unchanged; no Editor/player remains. Failed labels concern expected owner order/facts and two binding-view comparisons. Source/JSON/RNG/identity, direct moved/displaced/changed-record and currently exercised live-vs-frozen assertions pass. The expected owner-order helper incorrectly matches an inventory part's empty owner ID to a detached car's empty engine ID; other owner representation comparisons are under bounded investigation. This failed run remains recorded and is not a passing checkpoint.
- Source-c: manifest SHA256 `3154BD2E366783DC6BAD3E7A9D794CF449BF3FA91B70A9596B93B3B4F9CAB910`; focused folder `S06D2B1/whole-part-move-20261029-115058308`. `focused-c.log` exits 0 with 391 PASS/0 FAIL. All 1,556 hashes remain unchanged; no Editor/player remains. The real tire probe records source `TireCondition[]`, existing PartInstance.Copy output `EngineElement[]`, snapshot `EngineElement[]`, and exactCopiedValues=True. Correcting the expected owned-copy representation, inventory-owner inference and array/list comparison removes the b failures without production changes or relaxed field checks. Frozen setup/maps and live-wrapper failure parity are now exercised.
- Source-d: manifest SHA256 `A4EBA25667E7BC097E8C7EC5A2C252B790D57B743298606FF9D31FEB7C68FE0E`. Only the Editor checker changes from c: explicit named-field fingerprints include readonly owner fields and ordered definition-map key/value contents, with two actual mutation-observability assertions. Global serialization helpers and production files are unchanged. `focused-d.log` exits 0, folder `S06D2B1/whole-part-move-20261029-115642647`, **393 PASS/0 FAIL**. All 1,556 hashes remain unchanged and no Editor/player remains before starting regression. The independent high review clears the final harness source.

Local raw logs, source manifests and fixtures remain under ignored reports/save-refactor folders. This concise report is versioned. The frozen v1 cart trace remains unchanged. A resident CLI helper is distinguished from the actual Editor/player process; no Editor/player was running before source-a corrections.

## Completion gates

The focused group passes eight actual successful move directions plus bounded rejection, member-policy and malformed-owner diagnostics. Current live wrappers match the independent frozen reference, including exact first errors, legacy member synthesis and historical partial effects. The private scope preserves complete source values/reference topology, catalogue, RNG and entropy/identity streams, performs no instrumented JSON calls, rejects required missing engine members, and exposes no partial output on failure. Explicit owner maps and nested output mutations are observed and isolated. These are helper assertions and finite fixtures, not 393 independent gameplay scenarios.

`regression-d.log` exits 0, folder `S06D2B1/whole-part-move-regression-20261029-120057291`: **214,320 PASS/0 FAIL**, comprising 287 installed-build checks and 214,033 existing Parts-browser helper assertions. The H trace confirms 287/0. Shop holds, the separately labelled legacy nonempty-cart route and multi-position unit previews remain covered. All 1,556 source hashes remain unchanged after terminal exit; no Editor/player remains before the build.

`windows-d.log` exits 0 and explicitly records `Build Finished, Result: Success.` at line 8129. Output is Game/Builds/Windows/DragLab.exe; no test player is launched. All 1,556 source hashes remain unchanged after the build; no actual Editor/player remains (a resident CLI helper is separate). Final independent Luna-high approval is recorded in the linked review before local commit.

| Artifact | SHA256 |
| --- | --- |
| Focused-d checks.txt | F290E2383CE5A136C0A94E1411B0E98F9FDC280A0572796AE15DA6AB8F6F99EF |
| Focused-d projection trace | 91BF509FFF9C2AE8B524075A15454E28141D2B9E1F38B573FF33544F6ADD6086 |
| Combined regression checks.txt | 0D8C6CB86EBFDB798169AEFE9F3AB2C6882D3000ECDD19820A0C722DF7F59C11 |
| Regression H trace | EADF24D4AFFCCCD9A9F524D56F433EAEA878AA9CA2903D89D3BC2D74A9237B80 |
| Windows Assembly-CSharp.dll | 80E555B6572DA4A11146981EC113FB228F61DDAD83E4C4059EE0C7490FB37438 |
| Windows launcher | 437691663C5FB21231524F4886E60A10B1841F13E42FBBC93EE39FBD827930CD |
| Unchanged frozen v1 cart trace | 422CFEB051D05B7D6BB4BC9690F07B401AE7C6C9E6DCDE865CD44FB855581423 |

No transition timing/allocation series was run for this prerequisite. Existing native frame-time failures remain open; Editor correctness cannot establish performance, native/mobile or visual acceptance. The concrete initial part-copy chain is audited as null-preserving, with no data-dependent first-error change found; allocation failure is outside the contract. Engine fluids are preserved by source/live-graph checks, not exported as unused engine snapshot data.

## Interface checklist and remaining work

No UI markup, styling, controls, bench, physics, audio or authored content changed. The existing live entrypoints use the shared transition; the new private projection remains dormant. Desktop F8/mobile interface behavior is untouched by source changes. Real mouse/resolution/Android checks were not rerun for this prerequisite, and no new visual acceptance is claimed.

Step1's remaining characterization, typed multi-row cart planning/identities/effects, loose-unit and whole-engine families, freshness/tickets, caller activation, owned refresh, full native/device/performance gates and S07–S14 remain required. Runtime JSON manipulation and whole-profile save/preview copies are still prohibited final-state dependencies; this component does not retire every existing legacy path.

Pre-existing unrelated entries remain unstaged: reports/new-parts-supply/verification.txt, reports/parts-market/verification.txt, reports/race-audio-audition/coverage-matrix.csv, reports/race-audio-audition/verification.txt, reports/sqlite-state/verification.txt; Tests.ps1; .codex-remote-attachments/; reports/race-exit-regression/; reports/shop-responsive-20261019/. Local raw build/test artifacts remain ignored.
