# Automatic visual validation — 2026-10-16

The requested automatic gate is implemented as a separate tested tooling checkpoint. It does not change the game or installed build.

## Result on the migration captures

**293 PASS / 92 FAIL / 26 REVIEW**, covering all 411 expected pairs and all 15 suites. Required suite/report evidence is present; baseline fingerprints and image dimensions match. No new magenta pixels were found. See [the automatic report](validation/index.html) and [per-image verdicts](validation-matrix.csv).

The gate correctly returns nonzero; the migration is not retroactively declared an automatic visual pass. FAIL means a numerical difference outside the fixed full-frame or local-tile budget. Small label/hover differences can exceed the local budget even when their full-frame percentage is tiny. REVIEW records previously documented live race/bench, generated quote/path or regenerated market data. These declarations do not turn differences into passes. The remaining 118 flagged pairs require synchronized capture state or further investigation, rather than higher thresholds chosen to make this dataset green. Earlier manual review remains separate evidence.

## Contract and checks

- Explicit expected filenames/dimensions and before-file SHA256 prevent silently dropping comparisons or replacing baselines.
- Both phases require every declared suite and a successful report with actual checks. Missing, extra, corrupt or resized captures fail.
- Fixed budgets: channel difference above 8 counts as changed; at most 0.5% full-frame changed pixels and 0.25 / 255 mean channel error. Overlapping 32-pixel tiles allow at most 8% changed pixels and 12 / 255 mean error. Any newly magenta pixel fails. These are conservative numerical tolerances, not claims of perceptual perfection.
- Local tiles detect small missing controls that would be diluted by a full-image average. Low-amplitude whole-image tint shifts are caught by mean error even below the pixel threshold.
- Known nonrepeatable comparisons are explicitly listed with reasons; they remain inconclusive when over budget. There are no ignored image regions, automatic rebaselines or alignment corrections.
- The main gallery displays verdicts and links to this report; searching FAIL or REVIEW narrows the list.

Run `tools/Test-RenderPipelineImages.ps1 -Python <python-executable>`. Exit 0 is PASS, 1 is FAIL, 2 is REVIEW. It rebuilds the gallery even after a failed/inconclusive gate. Both 1 and 2 block automation. The Python entry point can also be called directly with `--contract tools/render-pipeline-capture-contract.json`.

**24 automated tool tests pass**, covering missing controls, blank frames, a low-amplitude global tint, magenta, noisy pixels, invalid suite evidence, missing one/both images, baseline replacement, changed sizes, unexpected files, unsafe paths, dynamic REVIEW and actual command-line exit codes. The PowerShell wrapper is exercised end to end for all three outcomes. A gallery test verifies that changing captures invalidates an older validation verdict; image hashes prevent stale PASS labels. The complete 411-pair wrapper run returns exit 1 with the counts above and refreshes the report/gallery. A game rebuild or physics rerun is unnecessary for this tooling-only change. User visual acceptance and the existing debug-bench input limitations remain unchanged.
