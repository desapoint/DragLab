# S06d2a frame-report tooling checkpoint

2026-10-25. Diagnostics only; the live browser conversion and full S06 responsiveness gate remain open.

`tools/check_parts_browser_frames.py` reads the existing native frame probe's exact 20-column UTF-8 CSV, rejects incomplete/non-finite/non-contiguous evidence, and reports each selected contiguous interval independently. Frame limits use every recorded frame, including later tails; allocation counts are descriptive, with no zero-allocation or causation claim.

Checks actually run:

- Inline standard-Python adversarial checks: valid BOM input, wrong schema, frame gap, NaN, missing phase, missing prefix, repeated separated phase intervals and exact 50 ms boundary pass.
- Existing historical fixture `reports/player-fixtures/s06d2a-native-seed16102026-c/frame-pacing.csv`, prefix `typed-refill/native-oil-pending`, limit 50 ms: expected failure exit **1**, explicitly checked. There are 174 frames; median 14.011 ms, P95 20.982 ms, maximum 124.561 ms, maximum Parts 89.475 ms, one frame over the limit. This reproduces the previously recorded failure; it is not a new game measurement.
- The same fixture with a nonexistent phase prefix returns invalid-evidence exit **2**, explicitly checked.
- Independent Luna-high static tool review passes in `docs/save-refactor/S06D2A_BROWSER_REVIEW.md`. The reviewer did not rerun the Python probes or Unity.

Reproduce:

```powershell
python tools/check_parts_browser_frames.py reports/player-fixtures/s06d2a-native-seed16102026-c/frame-pacing.csv --prefix typed-refill/native-oil-pending --limit-ms 50
```

No Unity rerun is needed for this parser-only checkpoint. It cannot prove scenario coverage, first-complete-list timing, visual parity, phone performance or which subsystem caused a stall. No live UI change, browser correctness/performance acceptance, JSON retirement or S07 advancement is claimed.
