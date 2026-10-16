# Physically driven effects listening section — 2026-10-15

The user requested a place in the existing HTML library for backfires, rev limiting and other missing event examples after the approved engine voices were integrated. `index.html` now has separate **Engine matrix** and **Effects** sections, with effect filtering, effective cut type, real pop/limit/release counters and case-specific timing. Returning between sections resets the filters. All original engine cases and factory references remain available.

## Examples

| Effect | Before/after cases |
| --- | ---: |
| Carburetor backfire / overrun | 2 |
| Other throttle-lift examples | 11 |
| Hard rev limiter, applicable fuel/ignition cuts | 22 |
| Soft rev limiter, applicable fuel/ignition cuts | 22 |
| Two-step, applicable fuel/ignition cuts | 22 |
| Actual transmission handovers | 13 |
| Cold idle | 13 |
| Pause / resume | 13 |
| Petrol turbo anti-lag on lift | 3 |
| Petrol turbo two-step preload | 3 |
| **New effects cases** | **124** |

The complete library now has **1,450 labelled cases / 939 unique audio pairs**. Lossless media totals 407,965,125 bytes; the updated ZIP is approximately **410 MB**. Effects use **Before 0–12 s / After 12.7–24.7 s**; the original engine matrix retains its 10-second halves. The player displays each example's own input timeline and uses the correct playback boundaries.

Each event capture runs the real **120 Hz `CarSimulation.IndependentStep`**, followed by the production **60 Hz `EngineSoundController`**. Exact immutable audio frames, including cylinder contributions, combustion fraction, cut/pop/release identifiers and pause state, are captured once. The standalone renderer then feeds identical frames to the previous sound generator and the current approved production generator. There is no duplicate physics implementation and no directly forced pop/cut sound flags.

Fixtures are isolated healthy authored cars with programmable controls, no wear/resource depletion and deliberately chosen throttle, neutral/staging, shift and pause inputs. They do not load, buy, repair or save player assets. The exporter asserts that labelled limiting/two-step/anti-lag/backfire events really occur and that engines survive. Carburetors retain ignition cuts; diesel examples retain fuel cuts and exclude anti-lag. Turbo preload/anti-lag use the three actual petrol factory-turbo definitions. Cold idle has no invented starter-motor layer. Shift examples follow their actual transmission behavior; any accompanying limiter pops remain limiter events, not invented manual shift cuts. Effects keep factory exhausts rather than claiming every possible effect/tune/exhaust combination.

## Completed checks

- Final Unity capture: **124 physically driven event cases**, all labelled-event and surviving-engine assertions pass. Summary: `effects-capture-verification.txt`; detailed binary frames/profile captures and Editor logs remain local.
- Offline rendering: **124 finite bounded before/after pairs**, including **1,560 publication frames of exact silence** across pause fixtures.
- Full library: every unique FLAC decodes sample-for-sample to its packaged 16-bit PCM; media hashes, source hashes, raw lengths and clipping/headroom checks pass. Updated ZIP CRC and all **1,450** media references pass.
- Shared loudness target difference: **0.00 LUFS** for every pair. Initial per-side peak limiting revealed up to 3.42 LUFS of level mismatch for transient-heavy effects. Packaging now lowers both sides to the same achievable target when extra headroom is necessary; no dynamic compression is used. Unchanged matched engine files are reused.
- Throttle-lift inputs were refined to short real blips so their overrun can be heard without prolonged limiter bouncing; the final capture and audio render were rerun afterward.
- Viewer logic: **21 in-memory checks** pass, including both sections, event/fuel filtering, diesel cut permissions, anti-lag eligibility, actual backfire events, correct 12/12.7-second effect boundaries and retained core navigation/filter behavior. Browser-rendered playback remains unverified by automation because the browser tool blocks local `file:` URLs.
- Python source compilation, scoped/staged whitespace and pre-existing user-file hash checks pass. All original user report edits, `Tests.ps1` and race-exit diagnostics remain intact.

The earlier approved voice integration is committed separately as `6d83070`, with **476 Unity checks**, Windows build and packaged race **141/0**. The current playable build remains `Game/Builds/Windows/DragLab.exe`; this follow-up adds only Editor/offline export tooling and review artifacts. Production event behavior, the pressure-pop algorithm and the debug bench UI are unchanged. The page now describes Before as the previous sound and After as the approved sound already in-game.
