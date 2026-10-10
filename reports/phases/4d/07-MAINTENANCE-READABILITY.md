# 4D.4 — Maintenance readability correction (2026-10-10)

- Shared native sliders retain their red fill and centered thumb; unfilled tracks now use visible muted grey with a subtle border. This is shared with Settings rather than a separate dialog theme.
- Oil and wet-clutch fluid-condition percentages omit the redundant quality suffix. The quantity stays neutral; the percentage alone indicates remaining condition: green above 50%, amber above 25% through 50%, red at 25% or below. Failed parts stay red; other installed-part condition percentages follow the same thresholds.
- No simulation, payment, profile, draft/confirmation or quantity limits change. Existing rounded controls and hover/mouse-down behavior remain.

## Verification

- Windows development build passed (Unity 6000.6.4f1, `Invoke-Unity.ps1 -Action BuildPlayerOnly`). Updated the local Windows executable and supporting files.
- Existing native player probe passed **338 checks / 0 runtime errors** on rerun, covering Settings sliders, maintenance quantity/confirmation, paid operations and F8 guards. Evidence: `07-native-verification.txt`. No new implementation-mirroring tests were added for this visual correction.
- The initial probe stopped at the existing real Shift+Tab assertion before reaching maintenance. The same build passed that assertion and all remaining checks on rerun; cause of the initial input failure was not established.
- Reviewed final refill and post-service captures at 1280: visible unfilled track, centered thumb, fresh green percentages and no quality suffix. The degraded wet-clutch percentage is amber in the refill capture. Existing compact/4K suite checks also passed; no separate physical 4K maintenance review.
- UTF-8 and whitespace checks passed. Build-only font serialization whitespace was removed; raw builds/logs/captures remain ignored. Historical domain/economy simulation gates and separate comparison probe were retained, not rerun. User visual acceptance pending.

## Review

Open a refill dialog and inspect the unfilled track at low and high quantities. Check fresh versus degraded oil and wet-clutch condition percentages, plus worn/failed component condition. Review only this correction; remaining Garage controls are separate checkpoints. User acceptance pending.
