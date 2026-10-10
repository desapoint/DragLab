# 4D.1 correction — Restore card corners (2026-10-10)

The previous correction misunderstood the requested card treatment. Restore the original rounded brushed-panel outline and fading red upper-left/lower-right corner accents. Keep the elongated rectangular proportions, exactly two cards per row, preview left and common specs/condition right. CURRENT remains a neutral text marker.

Use the original shared Panel renderer; remove the unused neutral-panel exception. No change to header, Settings, domains, card actions or input behavior. This is a correction of the current grid, not permission for the next browser control. Human acceptance remains pending.

## Verification

- Unity 6000.6.4f1 Windows development build passed; local player updated.
- Native player probe passed **262 checks, 0 runtime errors** (`tools/Test-NativeMenu.ps1 -FixtureName native-owned-cards-restored-corners`). [Retained verification](03-native-verification.txt). This reruns Settings/F8/save-isolation, true identities/condition, two-column and right-side summary checks at 1024, 1920 and offscreen 4K, caching and scroll retention.
- Visually inspected single-card and populated 1280 captures: original rounded red corner treatment is restored and two-column/side-by-side content retained. Raw evidence stays ignored under `reports/player-fixtures/native-owned-cards-restored-corners`.
- UTF-8/report links and whitespace checks passed. No asset GUID or metadata changes; normalized Unity-generated font trailing whitespace back to the tracked contents.

Limits: 4K is an offscreen target, not a physical 4K input test. Prior standalone F8 and mechanics/economy gates are retained historical baselines, not rerun here. Human visual acceptance remains pending.

## Review

In Garage, press F8 and check restored rounded red corner accents, two columns and right-side facts. Resize the window; review condition warnings. Stop for user review.
