# S06d2b1j — whole-engine transition verification

Baseline main `1c76d62`. This is the shared whole-engine Move/Detach/Attach prerequisite and dormant private projection. Full Step1/S06, cart planning/freshness/caller conversion, native/device/performance gates and S07–S14 remain open. No new save or UI caller is activated. Runtime JSON manipulation and whole-profile save/preview copies remain forbidden final-state dependencies.

## Provenance

- Unity 6000.6.4f1, CLI 1.0.0-beta.14 confirmed current. Parent owns all execution; both Luna-medium authors release before source freeze. Independent Luna-high design/static review is in J_REVIEW.
- Frozen independent engine reference SHA256 `B4704709F463E06453872AEAE0E1B3B1BC00034D7AAE846579708D85A268F012`, before extraction. Earlier I reference/rank helpers are retained; no new engine core is called by the reference.
- Source-a contains 1,564 distinct paths, manifest SHA256 `7E4A4DC474EA1BD542E136E170D98534C70550E4D754FC7C0DCF2D5045926C47`. `focused-a.log` exits 1 before tests: two core compile errors from a void MutableCar overload used as a return value, plus two test float literals. Manifest capture yielded during CLI startup; all authors were already under source freeze and all 1,564 hashes remained unchanged at terminal verification. No test evidence is credited to this run.
- After actual Editor/player exit and hash verification, root corrects the overload to return its single already-looked-up/guarded car, corrects 2.5f/-1.25f, and prepares the controlled tune/fluid transfer target using the frozen detach before setting overrides. No domain guard/order or expected outcome is relaxed. Reviewer verifies IState SHA256 `4563D133BBD3D97733A52182E5CF6DF1C61F47C089A89FF31428BDB15F1CC8D9` and checker `F798663BA0D21B3AE146BCE12AF8BDB51E7107BEACD006D454F96B6AC1C924DD`.
- Source-b contains 1,564 paths, manifest SHA256 `15D7BF75D821B4DB241F177B223ED077D666677A8C33E74F874C07D375F0CA03`, confirmed terminal before launching `focused-b.log`. It exits 1 with 460 PASS/1 FAIL, folder `S06D2B1/whole-engine-move-20261029-164543560`. The only failure is OPEN: the incompatible-target fixture had not acquired a different-model car. All other executed assertions pass. All 1,564 hashes remain unchanged and no Editor/player remains before thaw. This run is not a passing checkpoint.
- Root repairs only that fixture: use existing AddSecondOwnedCar to buy an actual seeded new-car listing when no other model is already owned; verify the model differs, restore the original selected car, and detach through the frozen reference. No new part/car data is fabricated and no production source or assertion is weakened. Checker SHA256 becomes `BD5376DC17531ED15804D05390218BE4AB17653BE882B9F74020203205332804`.
- Source-c manifest SHA256 `724D57064647681B67D591EA381BE0B0BB3899E48DA87C41B180719F71128C9E` covers 1,564 paths and completes before launching `focused-c.log`. It exits 0 with **473 PASS/0 FAIL**, folder `S06D2B1/whole-engine-move-20261029-165415746`; the trace contains 28 finite vectors and no OPEN cases. All 1,564 hashes remain unchanged, and no Editor/player remains before regression. Independent high review verifies the final focused artifacts.

Raw manifests/logs and isolated generated fixtures remain local ignored artifacts; this concise report is versioned. No actual Editor/player remained before the b correction/restart; a resident CLI helper is distinguished from Unity Editor/player.

## Focused evidence and remaining gates

Focused checks hash: `1128BF254F554D80C5C3EFA71DE69765B714391AA679E6B004BB571E1E5F4D9B`; projection trace: `19449697001DB3D7ADA8B8EFAA205F57AC61AF473E0B4AD118235DB3C2CA7DB1`. The current live wrapper matches the independent frozen full typed state and exact first error, including historical partial writes. The private path preserves source values/reference topology, catalogue, RNG and identity/entropy streams, calls no instrumented JSON route, returns no partial failure graph, retains nulls and isolates owned output mutations. Same-parent no-op with a missing target and null engine payload executes without target lookup. Attached descendant queries remain unavailable explicitly, while affected-car inspection follows the existing H rules; query failure never changes MoveSuccess. Changed engine order includes the different car-referenced engine on the malformed success fixture.

`regression-c.log` exits 0, folder `S06D2B1/whole-engine-move-regression-20261029-170217027`: **214,713 PASS/0 FAIL**, comprising I whole-part 393, H installed-build 287 and existing Parts-browser 214,033 helper assertions. The I and H trace summaries independently confirm 393/0 and 287/0. All 1,564 source hashes remain unchanged after terminal exit; no Editor/player remains before build. These counts are helper assertions over finite workloads, not independent gameplay/interface scenarios.

`windows-c.log` exits 0 and explicitly records `Build Finished, Result: Success.` at line 8134. Output is Game/Builds/Windows/DragLab.exe. All 1,564 source hashes remain unchanged after the build and no actual Editor/player remains. No test player is launched. Final Luna-high evidence approval is required before the local checkpoint commit.

| Artifact | SHA256 |
| --- | --- |
| Combined regression checks | 3036210D5046F7B50E77EA8E1661A85BE8054E3F904813B2CA1B8222E0255ABF |
| Regression I trace | FED1D87C06511E5789DE2763DB47FCC4226525B992EFF8529B4E28B181E56AF3 |
| Regression H trace | C759F203A9FABDB3FFA6476B5E3EA26D301FA4D2C0C3DBEC0C25A0AD0984959B |
| Windows Assembly-CSharp.dll | 86A95CE6B1961046F176B1987A69F95CB04BA889CAF6595F570B2564EC741C89 |
| Windows launcher | 437691663C5FB21231524F4886E60A10B1841F13E42FBBC93EE39FBD827930CD |
| Unchanged frozen v1 cart trace | 422CFEB051D05B7D6BB4BC9690F07B401AE7C6C9E6DCDE865CD44FB855581423 |

## Limits and interface checklist

No transition timing/allocation or real native/mobile/visual acceptance is implied by these Editor checks. Ordinary transfer values and source-reference topology are exercised; reference-preserving Tune/Fluid maps are also source-reviewed. Arbitrary shared-input alias graphs are not exhaustively generated. The finite 28-vector suite does not establish every malformed graph, car variant or transmission configuration.

This checkpoint changes no bench, physics/audio, authored content or UI control/theme. Existing live routes use the shared operations; the new projection is dormant. Desktop F8/mobile behavior is not changed by source edits. Real mouse/resolution/Android tests were not rerun; existing native frame-time failures remain open. Unique GUID checks pass for the four new C# assets; final staged whitespace and explicit-path review precede commit.

Step1/S06 still requires remaining supported families, ordered typed cart composition/identities/effects, complete readset/freshness tickets, UI caller conversion, owned refresh and native/device/frame gates. S07–S14 remain pending. This owning-thread borrowed-catalogue scope is not a detached immutable worker input or persistent save command.

Pre-existing unrelated entries are left unstaged: reports/new-parts-supply/verification.txt, reports/parts-market/verification.txt, reports/race-audio-audition/coverage-matrix.csv, reports/race-audio-audition/verification.txt, reports/sqlite-state/verification.txt; Tests.ps1; .codex-remote-attachments/; reports/race-exit-regression/; reports/shop-responsive-20261019/. Raw logs, screenshots, fixtures, manifests and build outputs remain local ignored artifacts.
