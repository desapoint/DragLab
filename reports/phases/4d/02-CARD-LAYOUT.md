# 4D.1 correction — Two-column rectangular cards (2026-10-10)

User-requested correction within the owned-car grid, not the next Garage control. Human review remains pending.

- Exactly two cards per row. Rectangular, neutral borders replace the rounded red corner accents; the CURRENT marker is neutral text. Status colors remain meaningful warnings.
- Car preview on the left; HP, torque and weight use labeled rows on the right, followed by class, condition, drivetrain/fuel and essential fault information. Preview proportions adapt to available card width.
- Reuse the shared brushed panel/palette/type/scrollbar. A `neutral-panel` modifier suppresses only the decorative red corners and rounding for these cards. Settings/dialog/header treatments remain as before.
- Keep actual instance identities, installed hardware/paint/wheel previews, copied-build class probes, read-only browsing, F8 and scroll retention. No search/filter/sort/selection/dealership/drawer control added.

## Verification

- Unity 6000.6.4f1 Windows development build passed (`tools/Invoke-Unity.ps1 -Action BuildPlayerOnly`); local Windows player updated.
- Native player probe passed **262 checks, 0 runtime errors** (`tools/Test-NativeMenu.ps1 -FixtureName native-owned-cards-two-column`). [Retained verification](02-native-verification.txt). Two-column rows and right-side summaries fit at 1024×768, 1920×1080 and a true 3840×2160 offscreen target using the existing 2× UI scale. Existing Settings/header/dialog behavior, hover/mouse-down, F8, duplicate/failure/missing-engine states, background ratings, scroll retention and durable-save isolation pass.
- Inspected single-card 1280 and populated compact/1080p/4K captures. Existing preview art and proportions are preserved. Raw captures/logs remain ignored under `reports/player-fixtures/native-owned-cards-two-column`.
- UTF-8 and whitespace checks passed; no metadata/GUID changes. Unity-generated trailing whitespace in the two font assets was normalized back to their tracked contents.

Limits: 4K uses an offscreen target, not physical 4K input hardware. Prior 48-check standalone F8 comparison remains the retained baseline, not rerun here; the native probe exercises F8/save isolation again. Mechanics/economy gates remain historical baselines. Human visual acceptance is pending.

## Review

Use F8 in Garage to review two columns, neutral rectangular outlines, preview proportions and right-side facts. Check compact/fullscreen resizing and condition warnings. This correction does not imply acceptance of the grid or completion of 4D.
