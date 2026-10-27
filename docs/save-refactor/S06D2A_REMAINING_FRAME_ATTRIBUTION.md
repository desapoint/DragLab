# S06D2A remaining frame attribution

**Status:** Read-only analysis of native run 93863 (suffix `l`). The live run 94925 was not inspected or interrupted. The 50 ms frame limit remains in force; this note does not waive any recorded over-limit frame.

## Evidence inspected

- `reports/save-refactor/S06H1/native-93863-frames.md`
- `reports/player-fixtures/s06h1-live-browser-seed16102026-l/frame-pacing.csv`
- `reports/player-fixtures/s06h1-live-browser-seed16102026-l/verification.txt`

The report selects 4,666 frames from 140 intervals and records 10 frames above 50 ms. The CSV reports full frame time alongside `raceGUIMs`, `nativePartsMs`, per-menu counters, allocation, and GC fields. These counters are not disjoint: the report explicitly notes that Parts work is included in `raceGUIMs`. They must not be added or subtracted to assign the unexplained frame remainder.

## What the high frames show

| Frame | Test interval | Full frame | `nativePartsMs` | `raceGUIMs` | Unity allocated bytes | Evidence-bound interpretation |
| ---: | --- | ---: | ---: | ---: | ---: | --- |
| 1682 | owned-actions / prepare | 51.105 ms | 46.433 ms | 29.687 ms | 5,546,800 | Parts counter is high in this frame; frame crosses 50 ms. Overlap prevents attributing the residual to a separate cause. |
| 1688 | owned-actions / prepare | 53.096 ms | 18.748 ms | 20.187 ms | 8,313,708 | Both UI counters are lower than full frame and overlap; the counters do not explain the whole frame. |
| 1727 | owned-actions / prepare | 53.877 ms | 1.528 ms | 2.867 ms | 6,208,090 | Full-frame cost is largely outside the reported Parts/UI counters. The available capture does not identify its source. |
| 1902 | direct-install A recovery | 57.336 ms | 19.312 ms | 20.814 ms | 7,137,444 | Parts/UI counters show work, but do not account for or identify all frame time. |
| 1936 | 1920×1080 owned-detail capture | 79.708 ms | 1.428 ms | 2.742 ms | 64,439 | Capture-interval frame with low reported UI counters; source of the full-frame spike is unmeasured. |
| 1950 | owned-actions / prepare | 68.903 ms | 2.764 ms | 2.783 ms | 6,816,266 | Large frame and Unity allocation report, with low Parts/UI counters; no source attribution is available. |
| 1972 | 3840×2160 offscreen owned-detail capture | 292.544 ms | 1.446 ms | 2.695 ms | 2,960,636 | Capture-interval frame. The counters do not explain the spike; do not label it product layout or screenshot encoding without a separate measurement. |
| 2080 | owned-actions / prepare | 67.931 ms | 1.671 ms | 2.915 ms | 8,391,412 | Full-frame spike and allocation report with little measured Parts/UI work; cause unknown. |
| 2240 | owned-actions / prepare | 73.111 ms | 1.484 ms | 2.809 ms | 8,970,472 | Full-frame spike and allocation report with little measured Parts/UI work; cause unknown. |

The known 423.351 ms frame at 2112 is excluded from this analysis: it is the previously identified Move Candidate JSON stall. It remains part of the recorded over-limit set and must be addressed by the planned typed-cart work.

## Pricing, detail body, and layout attribution

The bounded Parts records measure `priceFactoryMs`, `priceMaximumRunMs`, and counts across an entire preparation interval, not as per-frame mutually exclusive timing. For example, the unit-position stale-Apply build reports 184.656 ms of price-factory work over 14 calls, a maximum run of 21.130 ms, while its current-validation total is 64.024 ms. This establishes that pricing is a meaningful cost across the build, but does not attribute any particular full-frame spike to pricing.

The owned-unit recovery detail records separate detail build CPU from open-to-ready wall time. The direct-install recovery reports 12.665 ms build CPU and 170.113 ms open-to-ready; the later move and sale recovery reports 21.673 ms / 168.646 ms and 11.042 ms / 168.330 ms respectively. The stale-modal detail recovery reports 16.647 ms build CPU and 113.447 ms open-to-ready, with 36.573 ms validation. These values show incremental completion over rendered steps; they do not identify the cost of Unity layout/repaint. Layout wait is recorded as zero in these cases, which is not evidence that UI Toolkit layout itself costs zero.

The recorded long frames during 1080p/4K detail captures have only 1.4–1.5 ms in `nativePartsMs`; the capture counters therefore cannot support a claim that detail-body construction caused those frame times. Conversely, low Parts counters do not prove that screenshot capture caused them. The report does not contain per-frame screenshot, layout, render, or player-loop attribution sufficient to decide.

## Next measurement

Keep the full raw capture and the 50 ms gate. In the next owned-action/detail run, correlate each frame ID with one non-overlapping breakdown for: price evaluation, detail-body construction, attach/publish work, UI Toolkit layout/repaint, capture/harness work, and remaining player-loop time. Record the breakdown in the same frame row (or a frame-keyed sidecar) and keep the broad `raceGUIMs` counter as inclusive context only. This is the smallest next step that can distinguish a genuine Parts/UI cost from capture or unrelated frame time without guessing from the remainder. Continue the already planned typed-cart and owned-refresh work; this analysis does not clear those gates.
