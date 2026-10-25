# S06d1 — attribution and native lifecycle verification

Baseline `2b6687c`, shared main. **The attribution/lifecycle infrastructure passes independent Luna-high review.** The full S06 performance gate remains open: measured consumer stalls require S06d2 before S07. This checkpoint measures and verifies; it does not claim that the stalls, complete JSON retirement or full-profile save cloning are fixed.

## Actual evidence

| Gate | Result | Local artifact |
| --- | --- | --- |
| Pinned pre-change native sequence | 70/0, exit0; seed16102026 using the S06c player | `reports/player-fixtures/s06d-before-native-seed16102026/` |
| Windows development build94501 | exit0 | `S06D1/build-windows.log` |
| Native lifecycle73554 | 89/0, exit0:40 shared onboarding/starter +49 lifecycle checks | `reports/player-fixtures/s06d-lifecycle-seed16102026-a/verification.txt` |
| Pinned normal native29011 | 70/0, exit0 | `reports/player-fixtures/s06d-attribution-native-seed16102026-a/verification.txt` |
| Final Editor26545 | exit0;78 pilot assertions +5 timing notes,83 owner,66 migration,72 storage | `S06D1/editor.log`, `pilot-checks.txt`, `owner-regression-checks.txt`, `typed-migration-checks.txt`, `legacy-storage-checks.txt` |
| Positional CSV compatibility | original15 columns are an exact prefix; five appended columns,20 total | pinned before/current `frame-pacing.csv` |
| Visual parity | all five typed refill PNG files are SHA256-identical between the pinned before/current runs | quote, stale, pending, Settings-draft and completed captures in those fixtures |

The later Editor-only test clock/routing changes do not alter the player Runtime assembly tested in build94501. Existing S06c F8 52/0 is retained historical evidence; this step did not rerun the complete F8 suite or Android/device tests. No human acceptance is inferred from automated capture or developer inspection.

Reproduce a fresh normal player fixture with `tools/Test-NativeMenu.ps1 -FixtureName <fresh> -TypedRefillOnly -FrameProbe -DeterministicCapture`; lifecycle uses `-TypedRefillLifecycleOnly`. Use the same seed/entropy flags for comparisons. Run the Editor through `DragLab.Editor.StateCommandCheckpointVerification.RunTypedFluidRefillPilotCheckpoint --draglab-save-checkpoint S06D1` with the log under that checkpoint. The new entry point requires a safe explicit report name before any work; historical S06c entry remains available for intentional S06c reruns.

## What is proven

The probe appends `nativeOwnedMs`, `nativePartsMs`, `nativeCartMs`, `nativeOtherMenusMs`, and `nativeSurfacesMs`. Existing columns retain their positions. Fixed row fields and bounded buffers add no per-frame array clone or file I/O. The larger fixed probe rows retain roughly4 MB more capacity at100,000 rows by source layout; this opt-in diagnostic cost is not a measured phone-memory guarantee. No active probe means no timestamp capture. Multiple refresh callers can run outside LateUpdate, so consumer totals need not be a strict partition of the old outer UI total.

The new native cases use the actual selected-owner SQLite writer. They prove: one real commit followed by a lost acknowledgement remains unpublished until same-ID reconciliation; actual pre-COMMIT failure leaves exact durable rows/resources unchanged and requires a distinct explicitly confirmed next operation; failed controlled recovery retains the committed receipt/lease before a successful validated reload; and `SuspendForMobile()` plus the actual resume callback retain the same held operation through later owner publication. Workers carry typed commands and synchronization values, not live Unity/profile references. The failure hook exists only in Editor/development compilation and runs at the controlled-load boundary. Invoking methods/callbacks is not OS process-death coverage.

Parent inspected the lost-ack and failed-recovery captures: the actual child-label captions visibly read Retry status and Recover above the existing shared surface. The tests inspect visible labels rather than hidden Button.text. Acknowledged fixture operations remain durable; cleanup never overwrites them with old database bytes and does not swap away a pending/recovery-required pilot.

## Measured cause and limits

The fixed-seed sequence pins initial market/time/identities and has identical captured visuals. This is one pair of process runs, not a statistical overhead bound or an optimization benchmark. Example normal-flow observations:

| Stage | Before frame max ms | Instrumented frame max ms | Enclosing UI ms on instrumented peak | Attributed consumer ms |
| --- | ---: | ---: | ---: | ---: |
| Pending oil / Build navigation | 168.673 | 169.081 | 151.049 | Parts146.617 |
| Coolant confirm/completion | 64.033 | 52.151 | 40.725 | Owned cars39.258 |

Those differences do not prove improvement. Both runs still exceed50 ms. Median inter-Update times for these stages remain approximately14 ms; no sampling tail or setup/recovery row was suppressed. Startup, screenshot preparation, verifier SQL/load and scheduler gaps can produce large inter-Update intervals with little measured menu work. They must not be attributed wholesale to UI or SQLite. Explicit stale recovery remains a known blocking validated-load boundary, separately classified from ordinary publication.

The final managed fixture separately records one observation each: quote miss5.902 ms, validated hit1.297 ms, held poll0.631 ms, completion/publication1.769 ms and writer-release-to-publication35.656 ms. These are not device distributions. The first quote still exceeds the provisional2–3 ms target. S06d2 must reduce and remeasure the expensive exact consumers; all S06 gates must close before S07.

## Corrected test clock and evidence provenance

The first explicit-root run66072 passed the pilot but failed at owner check51; it did not reach migration/storage. Its fixture sampled UTC a second time inside a command factory, which could put accepted time after execution time. The fixture now passes the single coordinator admission tick through planning/writing, retains strict production validation, and reports the outcome/ticks. Final run26545 reports Committed/no error with identical admission, factory and accepted ticks. The earlier failing outcome error was not recorded, so no missing error string is reconstructed. Failed evidence remains in `S06D1/regression66072/`.

Earlier run78498 passed all four suites but used the old hardcoded S06c output root. Parent detected this after the original S06c pilot text had been overwritten. That verbatim raw transcript could not be recovered. The original S06C/editor.log, committed result/review and their observed count/timings remain; the other three original raw checks were preserved before replacement and restored. The S06c pilot path now contains an honest provenance marker. Fresh78498 outputs/log are preserved under `S06D1/regression78498/`. The final explicit-root rerun26545 writes S06D1 directly and proves routing without replacing another checkpoint's receipts. No player state, source history or test outcome was fabricated/restored to hide the mistake.

The unrelated SQLite report retains SHA256 `666cc352121c3f631d4f1df0cbf159aa9fdc141db228afbc85bf438d5c804b55`; other pre-existing reports/artifacts remain excluded. [Independent scoped review](../../docs/save-refactor/S06D1_REVIEW.md) and the execution ledger distinguish this checkpoint from S06d2 and the complete S00–S14 goal.
