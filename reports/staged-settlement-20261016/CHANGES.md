# Staged finish settlement — 2026-10-16

The user explicitly approved the previously blocked staged settlement/background-save proposal. This checkpoint addresses the measured finish-line save stall after the separately committed shared selection highlight (`ed60537`). Technical verification passes; user visual acceptance remains separate.

## Implementation and authority

The former finish path performed a whole-profile clone, normalization, optional seeded market refresh, snapshot capture and synchronous wait for a durable SQLite write in one race update. Recorded successful copied-save baselines were about 215 ms practice and 1.10 seconds with a cold market refresh. The camera snap was already corrected in `867c8b1`.

Finish now captures the beam/checkpoint, clones plain frozen save data on an isolated task, and advances the existing normalization/outcome/seeded market preparation through main-thread steps. Catalogue keys are batched; each offer yields; captured vehicle-inspection tasks are polled rather than awaited on a race frame. Existing synchronous generation callers exhaust the same iterator, retaining their established behavior and seed roll order.

Asset/catalogue access, UnityEngine.Object operations, UI, live physics and ledger publication stay on the main thread. Only plain-data JSON cloning/serialization and the existing SQLite write execute off-thread. Unity explicitly supports JSON serialization of supported data from a background thread: [Unity JSON serialization manual](https://docs.unity.com/en-us/engine/6000.5/manual/scripting/compilation-and-code-reload/script-serialization/json-serialization).

The candidate remains isolated until its SQLite transaction succeeds. Main-thread publication then applies the captured reward/profile once; later shutdown resources use the existing checkpoint path. Failed writes retain the old profile and actionable retry warning, with a bounded retry delay and retained resource checkpoint. Conflicting results actions stay disabled while pending. Explicit pause/quit/Garage boundaries drain pending work before final shutdown capture; ordinary finish frames never wait for the save task. There is no save-format, physics/replay version or brake-policy change.

## Verification approach

- Held-write native probes deliberately stop the writer before commit, check that frames/shutdown still advance, verify the writer is off the main thread, require old cash/receipt until commit and exercise Garage draining a still-pending paid refresh.
- The actual failed-save/retry and export/focus/pause/exit checks remain in the packaged race suite.
- Seed/guarantee equivalence compares staged versus synchronous stock. Fresh random GUIDs are excluded only from the equality projection; actual identity/fitment/condition validation is still required.
- Captured authored career terms compare synchronous/staged win/loss, first bonus, reputation and duplicate rejection. The existing seeded-parts matrix is rerun, with its historical report restored byte-for-byte and the new output copied here.

The first held-write run passed live-frame and publication checks, but its paid refresh had a 63.5 ms main step. The catalogue-key pass was then subdivided; the original failed local fixture remains evidence. The performance bound is retained, not widened. Tests run without simultaneous Editor simulation benchmarks when collecting player timing.

## Final checks and measured result

- Final Windows development build succeeds (Unity 6000.6.4f1); installed in `Game/Builds/Windows`. Fingerprints are in `build-manifest.txt`.
- Packaged focused pacing: **53 checks / 0 runtime errors**. Maximum measured settlement main step: **14.981 ms practice / 31.646 ms paid refresh**. Observed finish frames: 24.591 / 35.728 ms maximum. During a deliberately held SQLite write, 72 / 73 rendered frames and physical shutdown continue in half a second.
- Packaged full race/HUD/results regression: **200 checks / 0 runtime errors**. Includes actual failed commit and successful retry, visible error retention, no extra payout, export, F8, input, pause/focus, suspend, pending Garage exit, frozen board timing and continuous camera motion. Its held-write cases measure **11.804 / 32.376 ms** maximum settlement main steps, **23.504 / 42.166 ms** maximum observed finish frames and **0.049 / 0.054** maximum camera displacement per frame. See `native-verification.txt` and `pacing-verification.txt`.
- Domain policy/equivalence: **136 checks / 0 failures**, including three market seeds, guarantees 0/5/10, every authored career event in win/loss cases, captured fee/reward/first bonus/reputation, duplicate rejection and actual market validation. Existing seeded-parts matrix: **408 checks / 0 failures**. Those tests ran after the product-key subdivision; subsequent runtime focus/retry changes do not change domain generation.
- Direct SQLite recovery: **9 checks / 0 failures**. Commit-before-publication reload retains the exact committed cash, component identities, condition, fluids and ownership; repeated load cannot duplicate the delta. Injected pre-commit failure rolls back; a stale writer cannot overwrite the newer commit. Uses a disposable copied save; player saves are untouched.

The initial sandboxed full-suite attempt could not send Windows keyboard input; it stopped at two setup checks. The complete run above used native input outside the sandbox and passed. The full frame trace includes intentional 4K captures, synchronous explicit exit/suspend drains, startup and verification work; its overall maximum is not a normal finish-frame measurement. `frame-summary.txt` retains those costs rather than excluding them from the evidence. The focused held-write probes isolate ordinary finish preparation. This removes the known hundreds-of-milliseconds synchronous finish transaction; it does not claim every game frame is below 16.7 ms.

Reproduce with `tools/Test-NativeMenu.ps1 -FinishPacingOnly -FrameProbe -FixtureName <fresh-name>`, then `-RaceHudOnly -FrameProbe`. Run `tools/VerifyStagedSettlement.cs` through Unity `run_script` with entry `VerifyStagedSettlement.Run` or `.Recovery`. Normal interface captures are refreshed under `reports/interface-screenshots`; resolution captures remain in its resolution subfolders. Raw images, traces and fixture databases remain local and ignored.

## Limits and review

This targets known finish-time blockers rather than promising a universal frame rate on every machine. Explicit close/suspend boundaries may still wait for durability. Later UI phases, debug-bench redesign and unrelated pending test/report edits are outside this change. Review actual moving races in both finish orders, including a third paid race that refreshes stock; the timing slip/car/board composition and selected-card highlight remain separate visual choices.
