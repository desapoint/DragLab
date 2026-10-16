# Finish-line stutter audit and partial correction — 2026-10-16

Status: camera correction and redundant work removal only. **The full stutter request is not resolved.** Synchronous settlement still blocks the race thread. Approval is required for the proposed follow-up below.

## Findings

The results transition instantly moved the camera X from 2 to 4.4 world units. This shifted both cars/scenery together and could make the rival appear to teleport. The camera now eases toward the same final composition with a capped time step. Physics positions, finish order, board timing and HUD dimensions are unchanged.

The full-profile transaction also runs synchronously while shutdown simulation/rendering continue. On disposable copied saves, the initial Editor probe measured:

- Practice: 52.9 ms clone, 15.0 + 11.1 ms transaction normalization, 37.0 ms save capture/validation, 96.7 ms durable write; about **214.6 ms total**.
- Paid race with cold stock refresh: 49.8 ms clone, 678.2 ms settlement/market operation, 29.0 ms capture/validation, 317.6 ms durable write; about **1,098.5 ms total**.

The packaged before trace also catches long race-update frames. Its focused result test deliberately injects failed commits, so its largest frames are not presented as normal-play averages. The Editor probe independently measures a successful practice and successful third paid race. These are local samples, not a universal performance guarantee.

The safe save correction calls `TrySaveValidated` after `MechanicsTransactions.Try` has already normalized and validated the candidate. It skips the third duplicate ownership normalization but retains the same synchronous SQLite transaction, durability barrier, checkpoint merge and publish-after-success behavior. It cannot eliminate the measured disk/market stall. The follow-up sample still measured about **274.6 ms** practice and **1,153.1 ms** paid refresh; it ran while the player regression suite was active, so CPU/disk contention makes a total-time before/after speedup claim invalid. The removed duplicate validation is evident in the code, but the remaining stall is substantial and unresolved.

## Remaining work and approval

Automatic approval review rejected applying the broader incremental settlement/background persistence rewrite because changed save sequencing could corrupt state or produce inconsistent rewards. The incomplete preparatory edits were withdrawn. [Proposed stages, authority barriers, risk and validation matrix](PROPOSED-SETTLEMENT.md) are reviewable before approval. No such rewrite is included here.

## Verification

The Windows player is rebuilt for the safe correction. The native race suite adds a per-frame camera movement bound and final-framing check, retaining real failed-save/retry, once-only rewards, pause/focus, export and exit checks. The suite passes **171 checks / 0 runtime errors**. The measured camera-step check passes; final framing, actual failed-save recovery, once-only practice rewards, focus/pause, export and durable exit remain valid. `native-verification.txt` and `frame-summary.txt` retain the evidence. Frame traces still expose blocking save work. The user also reports the build remains very laggy; this is not recorded as acceptance or as a complete fix. Broader economy/mechanical suites are not represented as rerun.

Reproduce the copied-save timing tool using `unity command run_script --file ../tools/FinishSettlementTiming.cs --entry FinishSettlementTiming.Run --args '[true]' --project-path Game`. It uses isolated local fixture databases. Capture input/render evidence with `tools/Test-NativeMenu.ps1 -RaceHudOnly -FrameProbe -FixtureName <fresh-name>`.

The temporary Editor is closed after the test. Generated-only font/pipeline whitespace and the known URP runtime-stripping cache are restored separately; pre-existing user files are preserved. Updated Windows build fingerprints are recorded in `build-manifest.txt`.
