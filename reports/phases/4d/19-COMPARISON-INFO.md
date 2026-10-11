# 4D.10 — Comparison info refinement (2026-10-11)

Keep the useful general explanation from the info button and expose it beneath Tradeoff; omit redundant detail because numeric differences already appear in the comparison rows and bars.

## Delivered

- Comparison ⓘ opens one general explanation, without repeated Selected/Viewed blocks, values or bar-range boilerplate. Single-car contextual component information stays available; comparison warnings retain deduplicated actual explanations and suggestions.
- Hovering the orange Tradeoff label or changed bar shows the same explanation. The native panel surface is reused. Hints do not reflow rows, take pointer input or introduce focus/Tab navigation. They hide on exit, scroll, panel changes or opening the info dialog; placement fits the visible scroll viewport, switching above the target when needed.
- Existing comparison values, colors, curves, cached estimates, hardware/wear/resources and selection are preserved. This is a refinement of 4D.10, not another Garage workflow.

## Checks and limits

- Final Unity 6000.6.4f1 Windows development build passed. No compiler or USS errors in the final build log. Player/supporting files updated.
- Final focused dealership/comparison probe: **113 checks / 0 runtime errors**, PASS ([evidence](19-native-verification.txt)). Tests both dealer and owned comparison dialogs/hints, shared exact explanation, viewport bottom-edge placement, read-only profile/save bytes, copied estimates, cache reuse, F8/Settings return, transactions and no Tab navigation. Compact/1080p/true-4K render-target grid gates retained in this focused run.
- Reviewed final in-game Tradeoff captures on both pages at 1280×720. Human tooltip/readability review at other display sizes remains open. Raw fixtures/media/build/logs stay local and ignored.
- Prior full native regression **631/0** from [18](18-CAR-COMPARISON.md) remains a historical baseline; not rerun for this local presentation refinement. Domain mechanics/economy are unchanged.
- UTF-8/LF and diff whitespace checks passed. Build-only font serialization whitespace normalized with no content change. An intermediate compile failed on an unqualified UI Toolkit extension call; corrected before the successful build. Initial focused hint checks passed 111/0 before adding the viewport edge checks.

## Review

Compare a dealer model or non-selected owned car, open Engine stats and hover an orange Tradeoff. Open its ⓘ button: the same useful explanation should appear once, with the numeric comparison remaining on the row. Scroll to inspect hints near the viewport edge. This requested checkpoint is technically ready for review; user acceptance and further Garage steps remain separate.
