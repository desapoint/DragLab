# Finish-board segment joins — 2026-10-16

The user accepted the timing-slip layout and reported rough/overlapping seven-segment bars. This checkpoint corrects that display only; the subsequent suggestion to allow stronger shutdown braking is recorded separately. The 60% pressure cap and 160 m stop target are unchanged.

Vertical bars previously extended into the top, middle and bottom bars. Each bar now ends before its neighbor with a consistent gap. Beveled polygons use a thin transparent fringe to smooth edges locally. Digit masks, decimal positions, cached mesh rebuilding, independent finish posting and world placement remain authoritative and unchanged. No global shader/pipeline setting changes.

## Verification

- Isolated Unity/URP captures render 0–9 at both ET and RT sizes at 1280×720, 1920×1080 and 3840×2160.
- `verification.txt` checks connected lit regions in the actual rendered pixels, including diagonal contact. The original overlaps pass only 6/60 cases; the corrected digits pass **60/60**. This is a narrow separation test, not blanket visual approval.
- Whole-board and close-up images were reviewed locally; raw captures remain ignored. Normal scene composition is also checked by the packaged race suite.
- Windows player rebuilt in `Game/Builds/Windows`; packaged race/HUD/results checks pass **163 / 0 runtime errors**, including independent posting, frozen times, F8, actual stopping, telemetry export, retry/reset, durable exit and compact/1080p/4K UI bounds. See `native-verification.txt`. The complete physics/economy matrices were not rerun for this geometry-only change.

Reproduce the captures with `unity command run_script --file ../tools/VerifyFinishSegments.cs --entry VerifyFinishSegments.Capture --args '["after"]' --project-path Game`, then run `tools/verify_finish_segments.py`. Capturing `before` requires the prior source revision; saved raw captures are local evidence, not tracked fixtures. `tools/Test-NativeMenu.ps1 -RaceHudOnly -FixtureName <fresh-name>` exercises the actual race/results controls and refreshes its normal screenshot gallery.

## Review

Inspect the bar gaps, bevel tips and readability at normal scale. Later gauges and braking policy are separate checkpoints. The prior layout approval does not automatically approve this correction.

The temporary Editor was closed after verification. Only its whitespace reserialization and known derived URP runtime-stripping cache were restored; authored pipeline settings are unchanged. Pre-existing user test/report files remain untouched.
