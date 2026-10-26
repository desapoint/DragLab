# S06d2a dormant Parts browser core

2026-10-25. Baseline `7ab4811` (runtime facts baseline `7d6ee35`). This is a working backend checkpoint, **not live browser activation or the full S06 responsiveness gate**. Editor, Windows, existing-interface parity and independent Luna-high review pass for this limited scope.

## Implementation boundary

- A private, main-owned empty-cart builder captures typed inputs, runs an atomic exact finish check, and advances the existing entry/classification operations under an item cap and soft time budget. Every subsequent live-reading chunk and final publication performs a complete same-turn typed freshness check. Failed/cancelled results remain private and cannot revive after A→B→A edits.
- Externally valued rows have unique typed source/asset price facts. Ready catalogue groups and the new ready-before-holds ledger path avoid product-key/JSON fallbacks, including active reservations while the local UI cart is empty. Invalid ready bindings fail closed.
- Fitted individual-unit previews copy only the selected car/bindings/fitted parts and selected unit, then invoke unchanged authoritative installation and inspection. Run locks and null checkpoint state are fenced. Part specification summaries use typed definition snapshots instead of JSON keys.
- No whole-profile clone is introduced into a gameplay or save path. The independent full-fixture copier is private to the opt-in verifier. Typed preview-cache reuse, live card/action publication and nonempty `PreviewReadOnly` are still required follow-up work inside S06.

## Final frozen verification

| Run | Actual result |
| --- | --- |
| Focused unit oracle, session80595 | Exit0;611 logged checks. Exact typed complete fixture + unchanged Install/Capture agrees on full inspection, curves, warnings and all seven deltas. |
| Focused ready constructor, session74244 | Exit0;13369 logged checks. Cold index, direct-ID/first-row alias resolution, missing stock, current/prior-generation holds, both reserved modes, exact fees, no key/encoder calls, no stock/hold mutation and actual one-item stale rejection/retry. |
| Final combined Editor84128 | Exit0: browser188563;ready-constructor13369;base read-set1703;ready catalogue1609;supply37701 logged checks. Totals include descriptive timing/yield notes; they are not all distinct scenarios. |
| Windows build74791 | Exit0; final frozen source builds successfully. |
| Native64679 existing-route parity | Exit0;70 checks/0 runtime errors. All14 deterministic PNGs are byte-identical to `s06d2a-native-seed16102026-c`. This route exercises the unchanged live synchronous browser, not activation of the new builder. |
| Independent Luna-high review | PASS for the dormant backend only, after source and parent-run evidence review. Full S06 and live activation acceptance remain open. |

All eleven recorded source SHA256 values match the final Editor run. Unity metadata check passes with962 unique asset GUIDs. Existing standalone storage/owner fault suites retain their previously reported baselines; they were not rerun by the combined Editor entry above. Android/device acceptance and human visual acceptance are not claimed.

Logged totals above include three browser timing notes, seven read-set timing notes and two catalogue timing notes; the remaining entries are assertions, including step/yield assertions whose counts can vary with soft scheduling.

The rebuilt live legacy route still fails the50 ms frame budget: `typed-refill/native-oil-pending` has174 frames, median13.934 ms, P95 20.995 ms, maximum118.455 ms, maximum Parts90.100 ms and one over-limit frame. The checker returns expected exit1. This is the unconverted synchronous consumer, so the whole S06 performance gate stays FAIL/OPEN; these results do not demonstrate the dormant builder's native latency. Reported main-thread allocation bytes are zero on this probe, while maximum Unity allocation is13588052 bytes; zero is not evidence of no allocations.

Interface review checklist: onboarding/starter/dealer fitted-parts and the five refill captures match byte-for-byte; native mouse input, F8 starter comparison, shared dialog layout, durable refill completion and unsaved Settings draft retention pass the existing70 checks. There is no new visual design to accept at this dormant checkpoint. Live card/action UI and Android/human acceptance remain future gates.

Managed timing is descriptive. First complete Shop preparation (6288 entries, cap1) recorded18948 calls,15.549 ms capture total,3.394 ms maximum capture step,64635.418 ms total repeated validation,11.531 ms maximum validation,2.972 ms model setup,228.687 ms preparation total,12.277 ms maximum preparation step and5.023 ms final validation. Its69684.910 ms outer wall time includes verifier callbacks and pathological repeated whole-catalogue comparisons, not in-game load time. Other parity/retry builds request cap32; actual cap1 held-mutation probes remain. A soft2 ms loop budget does not bound indivisible setup/validation tails. No native latency pass follows from these figures.

## Retained failed/partial attempts

- `S06G1/attempt-71645`: CLI timed out at600 s (exit1). Browser sub-suite passed261679 checks, but later regressions did not run. Never treated as a full pass. Repeated all-Shop cap1 verification was replaced with one exhaustive cap1 run plus cap32 parity/retries and real cap1 held probes.
- `S06G1/attempt-63795`: exit1 after44120 assertions at the new independent dealer-unit oracle.
- `S06G1/attempt-21068`: focused exit1 after600 assertions identified the oracle defect: Unity JSON materialized null `Tune.awd` as a default object. Production correctly preserved null. The lossy test clone was replaced with a test-only exact typed graph copier; production was not normalized to the artifact.
- `S06G1/attempt-98696`: earlier combined exit0 (132817/1690/1606/37701) before the final targeted constructor/one-item additions. The final84128 run supersedes it.

Raw logs, checks, isolated fixtures and source hashes remain local under `reports/save-refactor/S06G1/`. The new full-fixture oracle checks detached records, exact values/nulls and an unchanged live fixture; it is never used for gameplay previews or saving.

## Remaining gates

The live `NativeParts.RefreshParts`, preview cache binding/keys and presentation context still use legacy synchronous/JSON paths. Multi-position new-unit preview and nonempty-cart preview still use `PartCart.PreviewProfile`; they must be replaced within S06 before accepting its full gate. Authored warmup's existing canonical encoding and all remaining live command/serialization paths remain required final retirement work, not approved completion. Complete bounded UI/query/card/action activation, typed read-only planned preview, owned refresh and matched native frame verification before S07; then continue S07–S14 in order.
