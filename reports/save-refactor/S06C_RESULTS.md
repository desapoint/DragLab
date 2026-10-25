# S06c — manual refill functional activation

Baseline `fb98b87`, shared `main`; no worktree or push. Manual engine-oil/coolant refills now use typed quotes, a background affected-record SQLite writer and targeted owner publication. This is a functional checkpoint. **The S06 frame-time gate remains open; S07 may not start until S06d resolves the consumer-refresh and lifecycle gaps below.** The complete no-runtime-JSON/no-full-profile-save-clone goal remains unfinished.

## Actual checks and build

| Gate | Result | Local evidence |
| --- | --- | --- |
| Managed pilot, real SQLite | 78 assertions + 5 timing notes pass | `S06C/pilot-checks.txt` |
| Selected-owner regression | 83 pass | `S06C/owner-regression-checks.txt` |
| Typed migration | 66 pass | `S06C/typed-migration-checks.txt` |
| Isolated storage regression | 72 pass | `S06C/legacy-storage-checks.txt` |
| Complete managed run57046 | Unity6000.6.4f1 exit0 | `S06C/editor.log` |
| Windows player build30013 | exit0 | `S06C/build-windows-caption.log` |
| Actual native clicks, run13795 | 70 checks / 0 runtime errors; exit0 | `reports/player-fixtures/s06c-native-20261024-d/verification.txt` |
| Actual desktop F8 clicks, run24755 | 52 checks / 0 runtime errors; exit0 | `reports/player-fixtures/s06c-f8-20261024-c/verification.txt` |

Each player suite includes the same40 onboarding/starter checks: native adds30 focused checks and F8 adds12. Counts are not all new refill assertions. The pilot wrapper counts5 timing notes as successful entries; only78 are assertions. The final core managed source is unchanged by subsequent dialog/diagnostic fixture refinements, which compile and run in build30013. Earlier build48293 also succeeded; its first native run37540 stopped at41 checks because a fixture reflected public `settings` with private-only flags.

Reproduce managed checks with `unity run Game --editor-version 6000.6.4f1 --timeout 600 -- -executeMethod DragLab.Editor.StateCommandCheckpointVerification.RunTypedFluidRefillPilot -logFile <absolute>/reports/save-refactor/S06C/editor.log`. Build with `DragLab.Editor.PlayerBuild.BuildPlayerOnly` and `-buildTarget Win64`. Run `tools/Test-NativeMenu.ps1 -FixtureName <fresh> -TypedRefillOnly -FrameProbe`, then another fresh fixture with `-TypedRefillF8Only -FrameProbe`; tests use real input and run sequentially. Generated databases, logs, CSVs and PNGs remain local artifacts.

## Behavior and evidence

Native oil/coolant dialogs and desktop F8 oil/coolant/combined actions branch into the typed pilot before legacy JSON. Within the typed controller/writer, quotes, normal admission, polling and publication do not capture/copy/project a whole profile. Existing UI consumer rebuilds are not included in that per-command claim; their remaining JSON/fingerprint work stays in the S06d/S13 audit. A bounded eight-entry typed cache validates exact scoped live dependencies and actual selected-owner metadata. Confirm validates the original shown terms; stale input cannot silently reprice the purchase.

Managed checks cover independent legacy price/quantity parity, all14 persisted fluid bit fields, bounded256-receipt FIFO, actual SQL tokens, cache expiry/dependency changes, delayed writes, double admission, pre-COMMIT rollback, unknown/lost acknowledgement reconciliation, stale publication, historical replay with a newer current commit, and explicit validated recovery. They preserve the strict production validator.

The advancing native run proves insufficient cash, stale physical input/no charge, explicit recovery, pending status and disabled duplicate confirmation, closing the original dialog, browsing Build, blocked race entry, separate preference saving, actual Settings Apply retaining the same live owner, and a second unsaved Settings draft surviving publication. A new refill dialog remains usable after prior success. Coolant changes its own quantity/cash while retaining oil. The F8 run proves the actual visible oil, coolant and combined buttons use the same pilot and publish one effect.

Pending, unknown and durable-unpublished work retains its operation identity and lease. Entry guards precede competing cart staging, tuning/setup/reset/race and legacy transaction work. Settings only publish independently saved fields on the same authoritative progression object. A proven refill-only publication preserves an unrelated tuning draft. Controlled recovery keeps the lease through validation and acknowledgement; its current validated-load implementation remains an exceptional legacy boundary. Automatic pre-race fluid assistance remains named S10 race-entry work.

## Measured limits — S06d is required

Single authored desktop managed fixture observations: first cache miss6.011 ms, validated hit1.334 ms, held-worker poll0.625 ms, completion/publication poll2.056 ms, worker-release to publication36.905 ms. These are individual observations, not distributions or phone guarantees. The first quote exceeds the provisional2–3 ms capture target.

Percentiles use nearest rank (ceil(0.95N)); medians shown are the lower middle observation for even N. Native frame CSVs include navigation, operation completion, screenshot work, explicit recovery and fixture preparation. `FramePacingProbe.OverridePhase` preserves scenario labels through normal menu refresh; no sample is suppressed.

| Final player scenario | Frames | Median ms | p95 ms | Maximum ms | Owner Update max ms | UI refresh max ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| typed-refill/native-capacity-fixture | 98 | 13.962 | 21.827 | 1675.120 | 8.041 | 2.291 |
| typed-refill/native-coolant-confirm | 62 | 13.952 | 20.962 | 55.373 | 8.367 | 43.225 |
| typed-refill/native-coolant-quote | 43 | 13.954 | 20.930 | 21.810 | 2.561 | 1.855 |
| typed-refill/native-oil-pending | 160 | 13.956 | 20.990 | 177.117 | 6.902 | 158.907 |
| typed-refill/native-oil-publication | 4 | 13.833 | 20.883 | 20.883 | 1.046 | 1.486 |
| typed-refill/native-oil-quote-stale | 173 | 13.963 | 21.631 | 990.030 | 9.504 | 48.569 |
| typed-refill/native-publication-settings-draft | 163 | 13.974 | 20.971 | 48.823 | 6.530 | 11.821 |
| typed-refill/native-settings-draft-pending | 102 | 14.012 | 21.000 | 53.277 | 5.922 | 8.759 |
| typed-refill/f8-capacity-fixture | 12 | 23.844 | 1774.320 | 1774.320 | 6.059 | 110.647 |
| typed-refill/f8-combined | 38 | 20.259 | 32.807 | 35.911 | 3.558 | 28.786 |
| typed-refill/f8-combined-fixture | 1 | 1762.094 | 1762.094 | 1762.094 | 2.979 | 23.280 |
| typed-refill/f8-coolant | 20 | 23.591 | 37.335 | 40.016 | 8.145 | 31.359 |
| typed-refill/f8-oil-pending | 21 | 21.517 | 35.347 | 39.438 | 7.843 | 30.108 |
| typed-refill/f8-oil-publication | 1 | 39.396 | 39.396 | 39.396 | 3.104 | 28.844 |

Final native coolant completion contains43.225 ms in the existing UI refresh scope, and pending Build navigation158.907 ms. Explicit stale recovery includes the known blocking validated-load boundary. Fixture rows include real persisted/reloaded preparation and are reported rather than presented as ordinary command cost. Settings Apply and screenshot work are included; not every spike is SQLite. Fresh fixtures selected different seeded starter models (earlier Wren, final native Sprig), so these are observations, not matched before/after benchmarks; S06d must pin its workload for comparisons.

These samples **do not pass the no-transaction-caused >50 ms UI-stall target**. S06d must identify and reduce refill-triggered broad view rebuilds, measure capture/publication and complete consumer frames with sufficient samples, and add actual unknown/retry/failed-recovery and interruption/resume coverage before S07. Generic forced-termination/platform soak remains S14. Do not claim all responsiveness is fixed by the small owner-publication timing.

## Interface review and remaining boundaries

Quote, stale, pending, Settings-draft and completed-dialog PNGs are in the native fixture and copied to `reports/interface-screenshots/`. Parent inspected pending, stale and completed captures: the shared slider/modal are retained and Recover/Complete visibly render on the child label above the button surface. Earlier tests read hidden Button.text and falsely passed that caption; the final tests inspect the visible child text/geometry and screenshots. This is developer inspection, not human acceptance. Desktop F8 remains available; no mobile Old/New control is added.

No Android build/device validation is claimed. Prior schema3 platform-release constraints remain. Fuel, oil change/flush, cart, remaining maintenance, workshop, race entry and other commands still require S07–S14 conversion. JSON is permitted only at the eventual named legacy/settings/diagnostic/external boundaries; remaining runtime JSON and profile clones are required retirement work.

Earlier failures are retained: compile errors (nonexistent fixture field/missing import), receipt-fixture clock ordering, stale-cash fixture missing explicit recovery, and native reload reflection. No production validation/assertion was removed to pass. The unrelated SQLite report retains SHA256 `666cc352121c3f631d4f1df0cbf159aa9fdc141db228afbc85bf438d5c804b55`; other pre-existing user files/reports remain excluded. Independent Luna-high review and the local commit are separate gates recorded in the execution ledger.
